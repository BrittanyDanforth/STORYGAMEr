--[[
	ServerInit v6 — builds the machine and owns the whole game loop:
	persistence (PlayerData), the upgrade tree + thrones, Meteor Rain
	(internals keep their fever* names), auto-roll/auto-drop, Star Dust +
	fusion (with forge locks), daily Galaxy missions (CLAIMED via ClaimQuest,
	connected-loop phase), DAY STREAK + PLAYTIME claims, leaderboards
	(3 OrderedDataStores + the physical RankBoard), offline earnings, the
	phase-2 product ladder (ProcessReceipt + personal/server boosts), tickets,
	zone switching (live retheme rebuild), the Gold Rush event,
	planet-roll announcements, payouts, analytics (AnalyticsService funnel +
	economy events), and housekeeping.

	Setup in Studio (five objects):
	1. ReplicatedStorage > ModuleScript "PusherMachine"  (src/PusherMachine.luau)
	2. ReplicatedStorage > ModuleScript "GameConfig"     (src/GameConfig.luau)
	3. ServerScriptService > Script                      (this file)
	4. ServerScriptService > ModuleScript "PlayerData"   (src/PlayerData.luau)
	5. StarterPlayer > StarterPlayerScripts > LocalScript "PusherClient"
	   (src/PusherClient.client.luau)

	Player state lives in replicated player attributes (Tickets, StarDust,
	tree node levels named by their GameConfig ids, ThroneLevel, ...) so the
	client UI needs no state sync remotes. All tuning data + shared formulas
	live in GameConfig — never redeclared here (docs/specs/final-contract.md).
]]

local Players = game:GetService("Players")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local MarketplaceService = game:GetService("MarketplaceService")
local PolicyService = game:GetService("PolicyService")
local DataStoreService = game:GetService("DataStoreService")
local RunService = game:GetService("RunService")
local AnalyticsService = game:GetService("AnalyticsService")

local PusherMachine = require(ReplicatedStorage:WaitForChild("PusherMachine"))
local GameConfig = require(ReplicatedStorage:WaitForChild("GameConfig"))
local PlayerData = require(script.Parent:WaitForChild("PlayerData"))

--------------------------------------------------------------------------------
-- Remotes
--------------------------------------------------------------------------------

local remotes = Instance.new("Folder")
remotes.Name = "PusherRemotes"
remotes.Parent = ReplicatedStorage

local function makeRemote(name)
	local remote = Instance.new("RemoteEvent")
	remote.Name = name
	remote.Parent = remotes
	return remote
end

local dropRemote = makeRemote("DropCoin")
local paidRemote = makeRemote("CoinPaid")
local rolledRemote = makeRemote("PlanetRolled")
local buyRemote = makeRemote("BuyUpgrade")
local zoneRemote = makeRemote("SelectZone")
local spinRemote = makeRemote("SpinLuck")
local rollRemote = makeRemote("RollBall")
local equipRemote = makeRemote("EquipBall")
local invRemote = makeRemote("InvSync")
local claimGroupRemote = makeRemote("ClaimGroup")
local perkRemote = makeRemote("BuyPerk")
local jackpotRemote = makeRemote("JackpotHit")
local rebirthRemote = makeRemote("DoRebirth")
local slotRemote = makeRemote("SlotHit")
local combineRemote = makeRemote("CombineBall")
local combineAllRemote = makeRemote("CombineAll")
local craftRemote = makeRemote("CraftCopy")
local combineDoneRemote = makeRemote("CombineDone")
local setToggleRemote = makeRemote("SetToggle")
local feverBlastRemote = makeRemote("FeverBlast")
local boostAnnounceRemote = makeRemote("BoostAnnounce")
local chestRemote = makeRemote("ChestOpened")
local dailyToastRemote = makeRemote("DailyToast")
local throneRemote = makeRemote("ThroneClaimed")
-- Connected loop (loop-final §B + loop-addendum §3/§4):
local claimQuestRemote = makeRemote("ClaimQuest") -- C->S (i: 1..3 quest, 4 = all-3 chest)
local claimStreakRemote = makeRemote("ClaimStreak") -- C->S ()
local streakClaimedRemote = makeRemote("StreakClaimed") -- S->C (day, tickets, dust, bonusName?, bonusTier?)
local claimPlaytimeRemote = makeRemote("ClaimPlaytime") -- C->S (idx into GameConfig.Playtime)
local toggleLockRemote = makeRemote("ToggleLock") -- C->S (name, tier): flip inv lk
local leaderboardSyncRemote = makeRemote("LeaderboardSync") -- S->ALL (+once to late joiners)
local laneMutateRemote = makeRemote("LaneMutate") -- S->ALL: lane mutation / jackpot window choreography

-- Folder attributes the client reads: global fever window, server-wide boost
-- windows, and one dev-product id per ladder key: "PId_<key>" — published
-- from GameConfig.Products (id 0 = unconfigured -> client renders "SOON").
remotes:SetAttribute("FeverUntil", 0)
remotes:SetAttribute("FeverBy", "")
remotes:SetAttribute("SrvLuckMag", 0)
remotes:SetAttribute("SrvLuckUntil", 0)
remotes:SetAttribute("SrvTixMag", 0)
remotes:SetAttribute("SrvTixUntil", 0)
for _, product in ipairs(GameConfig.Products) do
	remotes:SetAttribute("PId_" .. product.key, product.id or 0)
end

--------------------------------------------------------------------------------
-- Analytics (AnalyticsService): the ONBOARDING funnel (joined -> first_drop
-- -> first_roll -> starter_gift -> claim_100x -> boosted_roll -> first_forge),
-- the "purchase" funnel, and economy flows (tickets / robux). Every call is
-- pcall'd — telemetry must never error gameplay. Roblox drops events past
-- its per-server budget (~120/min + 20/min per player), so the per-coin
-- ticket credit is coalesced (see creditTickets); everything else fires on
-- rare moments.
--------------------------------------------------------------------------------

-- player -> { [step] = true }: a step fires once per session. The persisted
-- flags at each call site already make it once EVER — this turns the repeat
-- checks into a table hit instead of an API call.
local onboardingSent = {}

local function logOnboarding(player, step, name)
	local sent = onboardingSent[player]
	if not sent then
		sent = {}
		onboardingSent[player] = sent
	end
	if sent[step] then return end
	sent[step] = true
	pcall(AnalyticsService.LogOnboardingFunnelStepEvent, AnalyticsService, player, step, name)
end

-- Custom funnel step: funnel = its name, sessionId = one string per run.
local function logFunnel(player, funnel, sessionId, step, name)
	pcall(AnalyticsService.LogFunnelStepEvent, AnalyticsService, player, funnel, sessionId, step, name)
end

-- Economy flow: flow = Enum.AnalyticsEconomyFlowType.Source/Sink, txType =
-- an Enum.AnalyticsEconomyTransactionType item (the API takes its .Name),
-- amount/balance in whole units (a zero amount is not an event).
local function logEconomy(player, flow, currency, amount, balance, txType, sku)
	amount = math.floor(amount or 0)
	if amount <= 0 then return end
	local tx = (type(txType) == "string") and txType or txType.Name
	pcall(AnalyticsService.LogEconomyEvent, AnalyticsService, player, flow, currency, amount,
		math.max(0, math.floor(balance or 0)), tx, sku ~= nil and tostring(sku) or nil)
end

--------------------------------------------------------------------------------
-- Per-player session state (persisted state lives in docByPlayer / attributes)
--------------------------------------------------------------------------------

local invByPlayer = {} -- player -> { ["Name|tier"] = {n,t,c,f1,f2} } (authoritative)
local treeByPlayer = {} -- player -> { nodeId = level } (authoritative; attrs mirror)
local docByPlayer = {} -- player -> loaded PlayerData doc (baseline for snapshots)
local sessionStartAt = {} -- player -> os.time() at load (playtime accounting)
local nextAutoSaveAt = {} -- player -> os.time() of the next autosave
local saving = {} -- player -> true while the leave-save runs (autosave skips)
local leftSaved = {} -- userId -> true once the leave-save finished (close skips)
local policyByPlayer = {} -- player -> cached PolicyService info
local feverPending = {} -- player -> unflushed comet meter points
local chestQueue = {} -- player -> {purchased=bool}: owed a Galaxy Chest at fever end
local bigHitAt = {} -- player -> os.clock() of their last 10X/JACKPOT collect
local lastDropByPlayer = {} -- player -> os.clock() of their last manual drop
local lastAutoDropAt = {} -- player -> os.clock() of their last auto-drop
local lastPlayStamp = {} -- player -> os.time() of the last playtime accrual tick
local lbLastSent = {} -- player -> { earned/rebirth/found = last value written to ODS }

-- Attribute getter for GameConfig's shared formulas (client passes its own).
local function getterOf(player)
	return function(name)
		return player:GetAttribute(name)
	end
end

local function feverActive()
	return (remotes:GetAttribute("FeverUntil") or 0) > workspace:GetServerTimeNow()
end

-- Server-wide luck boost magnitude (0 outside its window).
local function srvLuckMagNow()
	if (remotes:GetAttribute("SrvLuckUntil") or 0) > workspace:GetServerTimeNow() then
		return remotes:GetAttribute("SrvLuckMag") or 0
	end
	return 0
end

-- Effective paid-boost magnitude B for a player's rolls: strongest of the
-- personal boost and the server-wide window (same-type boosts never
-- multiply). Passed to PusherMachine.rollOnce alongside the FREE luck L.
local function rollBoostMag(player)
	return math.max(GameConfig.boostMagOf(getterOf(player)), srvLuckMagNow())
end

-- Ticket boost multiplier: max(personal TixBoost, server SrvTix), never
-- below 1. The Tickets2x durable pass multiplies ON TOP (it's a pass).
local function tixBoostMult(player)
	local now = workspace:GetServerTimeNow()
	local mult = 1
	if (player:GetAttribute("TixBoostUntil") or 0) > now then
		mult = math.max(mult, player:GetAttribute("TixBoost") or 1)
	end
	if (remotes:GetAttribute("SrvTixUntil") or 0) > now then
		mult = math.max(mult, remotes:GetAttribute("SrvTixMag") or 1)
	end
	return mult
end

-- Meter points accumulate here and are flushed by the 1s heartbeat (one
-- FeverMeter attribute write per player per second, never per touch).
local function addFever(player, points)
	feverPending[player] = (feverPending[player] or 0) + points
end

local function ownsProduct(player, key)
	local doc = docByPlayer[player]
	return doc ~= nil and doc.meta.products[key] == true
end

local function specialTier(name)
	for _, sp in ipairs(GameConfig.SpecialPlanets) do
		if sp.n == name then
			return sp.t
		end
	end
	return 1
end

--------------------------------------------------------------------------------
-- Ball inventory: pulls from the roller land here; the EQUIPPED ball + form
-- is what the player's drops spawn (tier + fusion form set the payout)
--------------------------------------------------------------------------------

local function invKey(name, tier)
	return name .. "|" .. tier
end

local function syncInv(player)
	local inv = invByPlayer[player]
	if not inv then return end
	local list = {}
	for _, entry in pairs(inv) do
		table.insert(list, { n = entry.n, t = entry.t, c = entry.c, f1 = entry.f1 or 0, f2 = entry.f2 or 0, lk = entry.lk or 0 })
	end
	invRemote:FireClient(player, list)
end

-- Fusion dupe rule: a dupe just increments c (collecting copies IS the
-- point) — EXCEPT when the planet is maxed (owns a CELESTIAL, f2 >= 1):
-- then the copy auto-converts to Star Dust (perDupeByTier x Dust Refinery x
-- throne). Returns the dust gained (0 for any copy that was kept).
local onDiscovery -- forward: assigned after the lane engine + creditTickets
local coreChargeAdd -- forward: the lane engine assigns it (jackpot core CP)

local function grantBall(player, name, tier)
	local inv = invByPlayer[player]
	if not inv then return 0 end
	local key = invKey(name, tier)
	local dustGained = 0
	local entry = inv[key]
	if entry then
		if (entry.f2 or 0) >= 1 then
			local get = getterOf(player)
			local throne = GameConfig.throneOf(player:GetAttribute("ThroneLevel") or 0)
			local throneDust = throne and throne.dustMult or 1
			local perDupe = GameConfig.Dust.perDupeByTier[tier] or 1
			dustGained = math.floor(perDupe * (1 + GameConfig.statTotal(get, "dustPct")) * throneDust)
			player:SetAttribute("StarDust", (player:GetAttribute("StarDust") or 0) + dustGained)
		else
			entry.c = entry.c + 1
		end
	else
		inv[key] = { n = name, t = tier, c = 1, f1 = 0, f2 = 0, lk = 0 }
		-- IndexFound: DERIVED discovery count (loop-index §1). This is the one
		-- choke point every grant flows through — new key = new discovery.
		player:SetAttribute("IndexFound", (player:GetAttribute("IndexFound") or 0) + 1)
		-- Discovery hooks (core charge + Finder's Fee) live behind a forward
		-- local: creditTickets and the lane engine are defined further down.
		if onDiscovery then
			onDiscovery(player, tier)
		end
	end
	syncInv(player)
	return dustGained
end

--------------------------------------------------------------------------------
-- Achievements + ticket crediting
--------------------------------------------------------------------------------

local creditTickets -- forward: checkAchievements pays rewards through it

-- Achievements auto-grant off stat attributes (GameConfig.Achievements);
-- the earned flag ALSO lands in doc.attrs.Ach so it persists.
local function checkAchievements(player)
	local doc = docByPlayer[player]
	for _, ach in ipairs(GameConfig.Achievements) do
		if not player:GetAttribute("Ach" .. ach.id)
			and (player:GetAttribute(ach.stat) or 0) >= ach.need then
			player:SetAttribute("Ach" .. ach.id, true)
			if doc then
				doc.attrs.Ach[ach.id] = true
			end
			creditTickets(player, ach.reward, true)
		end
	end
end

local currentZone = 1 -- declared here so creditTickets closes over the LOCAL
local lastJackpotAt = -math.huge -- server-wide jackpot lockout clock

-- Analytics: a collect credits per COIN (several a second under a good push,
-- a torrent in Meteor Rain), so ticket credits are coalesced into ONE Source
-- event per player per TIX_FLOW_SEC — a per-coin event would blow the
-- AnalyticsService budget and starve the funnel steps. Remainder flushes on
-- leave.
local tixFlowPending = {} -- player -> tickets credited since the last event
local tixFlowAt = {} -- player -> os.clock() of the last event
local TIX_FLOW_SEC = 15

local function flushTixFlow(player)
	local pending = tixFlowPending[player] or 0
	tixFlowPending[player] = 0
	tixFlowAt[player] = os.clock()
	if pending > 0 then
		logEconomy(player, Enum.AnalyticsEconomyFlowType.Source, "tickets", pending,
			player:GetAttribute("Tickets") or 0, Enum.AnalyticsEconomyTransactionType.Gameplay, "tickets")
	end
end

-- rawReward skips multipliers (achievement/jackpot/offline payouts are flat).
-- Scaled chain (phase 2): zone x throne x tree x rebirth x perk x group x
-- fever x max(TixBoost, SrvTix) x Tickets2x pass, floored once at the end.
-- Same-type ticket boosts never multiply (strongest wins); the durable pass
-- multiplies ON TOP. Returns the credited amount. Never credits a player
-- whose doc has not loaded (their attributes would be clobbered by
-- PlayerData.apply).
creditTickets = function(player, amount, rawReward)
	if not player or not docByPlayer[player] then return 0 end
	if not rawReward then
		local get = getterOf(player)
		local throne = GameConfig.throneOf(player:GetAttribute("ThroneLevel") or 0)
		amount = amount
			* (GameConfig.Zones.mults[currentZone] or 1)
			* (throne and throne.tixMult or 1)
			* GameConfig.ticketTreeMult(get)
			* GameConfig.rebirthTicketMult(player:GetAttribute("Rebirth") or 0)
			* (player:GetAttribute("TicketMult") or 1)
		if player:GetAttribute("GroupPerk") then
			amount = amount * 1.25
		end
		if feverActive() then
			amount = amount * GameConfig.Fever.payoutMult
		end
		amount = amount * tixBoostMult(player)
		if ownsProduct(player, "Tickets2xPass") then
			amount = amount * 2
		end
		amount = math.floor(amount)
	end
	player:SetAttribute("Tickets", (player:GetAttribute("Tickets") or 0) + amount)
	player:SetAttribute("TotalEarned", (player:GetAttribute("TotalEarned") or 0) + amount)
	tixFlowPending[player] = (tixFlowPending[player] or 0) + amount
	if os.clock() - (tixFlowAt[player] or 0) >= TIX_FLOW_SEC then
		flushTixFlow(player)
	end
	checkAchievements(player)
	return amount
end

--------------------------------------------------------------------------------
-- Daily Galaxy missions (connected loop): counters set READY flags — the
-- payout waits for ClaimQuest. The all-3 chest (ClaimQuest 4) carries the
-- constellation star + the 7-star Constellation Prime prize. DAY STREAK and
-- PLAYTIME claims live here too. Every claim gate is a DOC field written
-- before any grant, and every grant is non-yielding (loop-final §D): a
-- spammed remote always sees the closed gate. Attributes are display mirrors.
--------------------------------------------------------------------------------

-- Post-claim save (autosave cadence is 120s — a claim is worth persisting
-- now). Snapshot synchronously, save async, exactly the autosave pattern.
local function saveAfterClaim(player, reason)
	local doc = docByPlayer[player]
	if not doc or saving[player] then return end
	-- A fresh fallback doc must never overwrite the real account; the
	-- client already shows PROGRESS WON'T SAVE.
	if player:GetAttribute("DataLoadFailed") == true then return end
	local snapshot = PlayerData.snapshot(player, invByPlayer[player],
		treeByPlayer[player], doc, sessionStartAt[player])
	local userId = player.UserId
	task.spawn(function()
		PlayerData.save(userId, snapshot, reason)
	end)
end

-- Mission goal reached -> ready flag (doc + DailyR<i> mirror). No payout here.
local function checkDailyMissions(player)
	local doc = docByPlayer[player]
	if not doc then return end
	for index, mission in ipairs(GameConfig.Daily.missions) do
		local flag = "r" .. index
		if not doc.daily[flag] and (player:GetAttribute(mission.statAttr) or 0) >= mission.goal then
			doc.daily[flag] = true
			player:SetAttribute("DailyR" .. index, true)
		end
	end
	-- The chest goes ready when all three are REACHED (claimed not required).
	if not doc.daily.r4 and doc.daily.r1 and doc.daily.r2 and doc.daily.r3 then
		doc.daily.r4 = true
		player:SetAttribute("DailyR4", true)
	end
end

-- Counters only climb while below goal; the ready flag fires on the crossing.
local function bumpDaily(player, index, amount)
	local doc = docByPlayer[player]
	if not doc then return end
	local mission = GameConfig.Daily.missions[index]
	if not mission then return end
	local cur = player:GetAttribute(mission.statAttr) or 0
	if cur >= mission.goal then return end
	player:SetAttribute(mission.statAttr, math.min(mission.goal, cur + amount))
	checkDailyMissions(player)
end

-- UTC day boundary: reset counters + ready/claimed flags (legacy p flags too).
-- Stars are NOT reset (streak-free constellation), neither is playtime
-- (lifetime milestones). Runs on load + the 60s heartbeat ticker.
local function rolloverDaily(player)
	local doc = docByPlayer[player]
	if not doc then return end
	local today = math.floor(os.time() / 86400)
	if (player:GetAttribute("DailyStamp") or 0) == today then return end
	player:SetAttribute("DailyStamp", today)
	player:SetAttribute("DailyM1", 0)
	player:SetAttribute("DailyM2", 0)
	player:SetAttribute("DailyM3", 0)
	doc.daily.stamp = today
	doc.daily.m1 = 0
	doc.daily.m2 = 0
	doc.daily.m3 = 0
	doc.daily.p1 = false
	doc.daily.p2 = false
	doc.daily.p3 = false
	for i = 1, 4 do
		doc.daily["r" .. i] = false
		doc.daily["c" .. i] = false
		player:SetAttribute("DailyR" .. i, false)
		player:SetAttribute("DailyC" .. i, false)
	end
end

-- ClaimQuest(i): 1..3 = a mission's reward (needs r<i>, once via c<i>);
-- 4 = the all-3 chest (needs r1..r3 REACHED) — pays GameConfig.Daily.chest
-- and IS the star carrier now: +1 star, Constellation Prime (or repeat dust)
-- at 7. Doc-field gate written FIRST; everything below it is non-yielding.
claimQuestRemote.OnServerEvent:Connect(function(player, index)
	local doc = docByPlayer[player]
	if not doc then return end
	index = tonumber(index)
	if not index or index ~= math.floor(index) or index < 1 or index > 4 then return end
	if index <= 3 then
		if not doc.daily["r" .. index] or doc.daily["c" .. index] then return end
		doc.daily["c" .. index] = true -- WRITE THE GATE FIRST
		player:SetAttribute("DailyC" .. index, true)
		local mission = GameConfig.Daily.missions[index]
		local credited = creditTickets(player, mission.tickets, false)
		player:SetAttribute("StarDust", (player:GetAttribute("StarDust") or 0) + mission.dust)
		dailyToastRemote:FireClient(player, "mission", credited, mission.dust)
	else
		if not (doc.daily.r1 and doc.daily.r2 and doc.daily.r3) or doc.daily.c4 then return end
		doc.daily.c4 = true -- WRITE THE GATE FIRST
		doc.daily.r4 = true
		player:SetAttribute("DailyR4", true)
		player:SetAttribute("DailyC4", true)
		local chest = GameConfig.Daily.chest
		local credited = creditTickets(player, chest.tickets, false)
		player:SetAttribute("StarDust", (player:GetAttribute("StarDust") or 0) + chest.dust)
		dailyToastRemote:FireClient(player, "chest", credited, chest.dust)
		local stars = (player:GetAttribute("DailyStars") or 0) + (chest.stars or 1)
		player:SetAttribute("DailyStars", stars)
		dailyToastRemote:FireClient(player, "star", 0, 0)
		if stars >= GameConfig.Daily.starsToPrize then
			local prize = GameConfig.Daily.prizePlanet
			local inv = invByPlayer[player]
			if inv and inv[invKey(prize.n, prize.t)] then
				local dust = GameConfig.Daily.repeatPrizeDust
				player:SetAttribute("StarDust", (player:GetAttribute("StarDust") or 0) + dust)
				dailyToastRemote:FireClient(player, "prize", 0, dust)
			else
				grantBall(player, prize.n, prize.t)
				dailyToastRemote:FireClient(player, "prize", 0, 0)
			end
			player:SetAttribute("DailyStars", 0)
		end
	end
	saveAfterClaim(player, "quest")
end)

-- DAY STREAK claim (loop-daily §4 — the ORDER IS THE SPEC: no yield between
-- the sLast read and write; grants all non-yielding; save spawned LAST;
-- os.time() everywhere, matching DailyStamp).
claimStreakRemote.OnServerEvent:Connect(function(player)
	local doc = docByPlayer[player]
	if not doc then return end
	local streak = GameConfig.Streak
	local now = os.time()
	if now - doc.daily.sLast < streak.minGapHours * 3600 then return end
	if now - doc.daily.sLast > streak.breakHours * 3600 then
		doc.daily.sDay = 0 -- missed ~2 days: the count restarts (day 1 next)
	end
	doc.daily.sLast = now -- WRITE THE GATE FIRST
	doc.daily.sDay = doc.daily.sDay + 1
	player:SetAttribute("StreakDay", doc.daily.sDay)
	player:SetAttribute("StreakReady", false)
	player:SetAttribute("StreakLast", now) -- client countdown mirror
	local slot = ((doc.daily.sDay - 1) % 7) + 1
	local weeks = math.floor((doc.daily.sDay - 1) / 7)
	local mult = math.min(1 + 0.1 * weeks, streak.weekMultCap)
	local day = streak.days[slot]
	local credited = creditTickets(player, math.floor(day.tickets * mult), false)
	if day.dust > 0 then
		player:SetAttribute("StarDust", (player:GetAttribute("StarDust") or 0) + day.dust)
	end
	local bonusName, bonusTier = nil, nil
	if day.rollMinTier then
		local spec = PusherMachine.rollOnce(
			GameConfig.computeRollLuck(getterOf(player)), day.rollMinTier, 0)
		bonusName = spec.n
		bonusTier = spec.t or 1
		grantBall(player, bonusName, bonusTier) -- syncInv fires; no PlanetRolled
	end
	if coreChargeAdd then
		coreChargeAdd(player, GameConfig.JackpotCore.charge.streak, "direct")
	end
	streakClaimedRemote:FireClient(player, doc.daily.sDay, credited, day.dust, bonusName, bonusTier)
	saveAfterClaim(player, "streak") -- autosave AFTER state is consistent
end)

-- PLAYTIME: claimable-milestone bitmask over the persisted lifetime total.
local function playReadyMask(doc)
	local ready = 0
	local total = doc.playtime.total or 0
	local mask = doc.playtime.mask or 0
	for i, milestone in ipairs(GameConfig.Playtime.milestones) do
		if total >= milestone and bit32.band(mask, bit32.lshift(1, i - 1)) == 0 then
			ready = bit32.bor(ready, bit32.lshift(1, i - 1))
		end
	end
	return ready
end

claimPlaytimeRemote.OnServerEvent:Connect(function(player, index)
	local doc = docByPlayer[player]
	if not doc then return end
	index = tonumber(index)
	if not index or index ~= math.floor(index)
		or index < 1 or index > #GameConfig.Playtime.milestones then
		return
	end
	local flag = bit32.lshift(1, index - 1)
	if (doc.playtime.total or 0) < GameConfig.Playtime.milestones[index] then return end
	if bit32.band(doc.playtime.mask or 0, flag) ~= 0 then return end
	doc.playtime.mask = bit32.bor(doc.playtime.mask or 0, flag) -- GATE FIRST
	player:SetAttribute("PlayClaimed", doc.playtime.mask)
	player:SetAttribute("PlayReady", playReadyMask(doc))
	local rewardDust = GameConfig.Playtime.dust[index] or 0
	local credited = creditTickets(player, GameConfig.Playtime.tickets[index] or 0, false)
	player:SetAttribute("StarDust", (player:GetAttribute("StarDust") or 0) + rewardDust)
	dailyToastRemote:FireClient(player, "playtime", credited, rewardDust)
	saveAfterClaim(player, "playtime")
end)

--------------------------------------------------------------------------------
-- Thrones: center-spine milestones, auto-claimed the moment the rebirth gate
-- is met (checked on load, on rebirth, on zone select). Permanent.
--------------------------------------------------------------------------------

local function checkThrones(player)
	local level = player:GetAttribute("ThroneLevel") or 0
	local rebirth = player:GetAttribute("Rebirth") or 0
	for _, throne in ipairs(GameConfig.Spine) do
		if throne.level > level and rebirth >= (throne.gate and throne.gate.rebirth or math.huge) then
			level = throne.level
			player:SetAttribute("ThroneLevel", level)
			throneRemote:FireClient(player, throne.level, throne.name, throne.desc)
		end
	end
end

--------------------------------------------------------------------------------
-- Upgrade tree engine: generic buy over GameConfig.UpgradeTree
--------------------------------------------------------------------------------

buyRemote.OnServerEvent:Connect(function(player, nodeId)
	if type(nodeId) ~= "string" then return end
	local tree = treeByPlayer[player]
	if not tree then return end
	local node = GameConfig.TreeById[nodeId]
	if not node then return end
	local level = tree[nodeId] or 0
	if level >= node.maxLv then return end
	for _, req in ipairs(node.requires) do
		if (tree[req.id] or 0) < req.lv then return end
	end
	if node.gate then
		local rebirth = player:GetAttribute("Rebirth") or 0
		if node.gate.rebirth and rebirth < node.gate.rebirth then return end
		if node.gate.zone and rebirth < (GameConfig.Zones.reqs[node.gate.zone] or 0) then return end
	end
	local cost = GameConfig.treeCost(node, level)
	local tickets = player:GetAttribute("Tickets") or 0
	if tickets < cost then return end
	player:SetAttribute("Tickets", tickets - cost)
	logEconomy(player, Enum.AnalyticsEconomyFlowType.Sink, "tickets", cost, tickets - cost,
		Enum.AnalyticsEconomyTransactionType.Shop, nodeId)
	tree[nodeId] = level + 1 -- table first, attribute second (protocol §6.4)
	player:SetAttribute(nodeId, level + 1)
	-- Special planet nodes grant their ball on purchase.
	if node.effect and node.effect.key == "grantPlanet" and node.effect.planet then
		grantBall(player, node.effect.planet, specialTier(node.effect.planet))
	end
end)

--------------------------------------------------------------------------------
-- Machine lifecycle: build / teardown, so zones can retheme live
--------------------------------------------------------------------------------

local ZONE_COUNT = #PusherMachine.Themes
-- (currentZone is declared above creditTickets, which closes over it.)
local current = { machine = nil, stopDrive = nil, conns = {} }
local liveCoins = {}
local eventDoublePay = false

local LIVE_COIN_CAP = 48

-- FULL in-place compaction, not a head-prune: the collector destroys pieces
-- from the MIDDLE of this array, and dead references counting toward the
-- 48 cap made long sessions degrade to "one planet respawns per click"
-- (the cap hit permanently, absorb ate the only live ball) — found live.
local function compactLiveCoins()
	local kept = 0
	for index = 1, #liveCoins do
		local coin = liveCoins[index]
		liveCoins[index] = nil
		if coin.Parent then
			kept = kept + 1
			liveCoins[kept] = coin
		end
	end
end

local function pruneDeadHead()
	compactLiveCoins()
	while #liveCoins > 0 and not liveCoins[1].Parent do
		table.remove(liveCoins, 1)
	end
end

local function firstPlayer()
	local list = Players:GetPlayers()
	return list[1]
end

local function ownerOf(piece)
	local userId = piece:GetAttribute("OwnerId")
	if userId then
		local player = Players:GetPlayerByUserId(userId)
		if player then return player end
	end
	return nil -- house pieces (and departed owners) pay nobody
end

-- Phase-continuous pusher speed change (protocol §3.1): bank the cycles
-- elapsed at the old speed into SpeedCycles0, move the epoch, THEN write
-- SpeedMult last so the client mirror only ever snaps, never drifts.
local function setPusherSpeed(machine, mult)
	local now = workspace:GetServerTimeNow()
	local cycle = PusherMachine.Config.cycleSeconds
	local c0 = machine:GetAttribute("SpeedCycles0") or 0
	local at = machine:GetAttribute("SpeedMultAt") or now
	local m = machine:GetAttribute("SpeedMult") or 1
	machine:SetAttribute("SpeedCycles0", c0 + (now - at) * m / cycle)
	machine:SetAttribute("SpeedMultAt", now)
	machine:SetAttribute("SpeedMult", mult)
end

-- Jackpot pot authority lives HERE, not in the machine: zone rebuilds
-- destroy the model (and its JackpotValue IntValue), and the pot — paid
-- PotBoost credit included — must survive that. The IntValue is only the
-- replicated mirror the client odometer reads.
local potValue = nil -- workspace IntValue mirror, re-seeded on rebuild
local potAmount = nil -- authoritative; nil until the first machine build seeds it

local function getPot()
	return potAmount
end

local function setPot(v)
	potAmount = v
	if potValue then
		potValue.Value = v
	end
end

--------------------------------------------------------------------------------
-- DYNAMIC LANES / MUTATION DECK / JACKPOT CORE (owner brief: the board must
-- not stay the same forever). Server-authoritative: laneState is the single
-- truth; clients render from PusherRemotes attributes + the LaneMutate
-- event. Paid roll luck NEVER enters this system. 5 logical lanes ride the
-- 7 physical signs (GameConfig.Lanes.physicalToLogical); JACKPOT is a
-- charged STATE of the center, not a permanent sign.
--------------------------------------------------------------------------------

local laneState = {
	base = { 3, 10, 25, 10, 3 },
	mult = { 3, 10, 25, 10, 3 },
	mut = nil, -- active mutation payload (see fireLaneEvent)
	hist = { "", "" }, -- last two deck ids (never three in a row)
	cdUntil = {}, -- deckId -> serverNow cooldown
	globalCdUntil = 0,
	charge = 0, -- jackpot core, 0..100 CP
	pendDrop = 0, -- per-second cap buckets (flushed by the 1s heartbeat)
	pendHits = 0,
	pendDirect = 0, -- discovery/forge/streak — uncapped
	igniter = nil, -- last player whose charge moved the meter
	seq = 0,
	jackpot = { state = "idle", windowUntil = 0, hitsLeft = 0, coolingUntil = 0 },
}

local function pushLaneAttrs()
	for i = 1, 5 do
		remotes:SetAttribute("Lane" .. i .. "Mult", laneState.mult[i])
	end
	local jp = laneState.jackpot
	remotes:SetAttribute("JackpotState", jp.state)
	remotes:SetAttribute("JackpotWindowUntil", jp.windowUntil)
	remotes:SetAttribute("JackpotHitsLeft", jp.hitsLeft)
	remotes:SetAttribute("JackpotCoolingUntil", jp.coolingUntil)
end

local function fireLaneEvent(kind, id, lanes, old, new, dur, hitsLeft, byName)
	laneState.seq += 1
	local nowT = workspace:GetServerTimeNow()
	local payload = {
		seq = laneState.seq, kind = kind, id = id, lanes = lanes,
		old = old, new = new,
		animStartAt = nowT, effectiveAt = nowT,
		expiresAt = dur and (nowT + dur) or 0,
		rewindEndAt = dur and (nowT + dur + GameConfig.Lanes.rewindSec) or 0,
		hitsLeft = hitsLeft or 0,
		pot = getPot() or 0,
		byName = byName or "", serverNow = nowT,
	}
	laneMutateRemote:FireAllClients(payload)
	return payload
end

-- Effective multiplier of a LOGICAL lane at server time t. The payout rule
-- (owner order: the player never loses value to a transitioning sign):
-- upgrades pay the NEW value from the moment the count-up starts;
-- expiries keep paying the boosted value until the rewind animation ENDS.
local function laneMultAt(logical, t)
	local m = laneState.mut
	if m then
		for i, ln in ipairs(m.lanes) do
			if ln == logical and t >= m.effectiveAt and t <= m.rewindEndAt then
				return m.new[i]
			end
		end
	end
	return laneState.base[logical]
end

-- Charge contribution. bucket: "drop" | "hits" (per-second capped) or
-- "direct" (discovery/forge/streak — uncapped). Player nil = house (never
-- charges; same legitimacy rule as the fever meter). Assigned to the
-- forward local above grantBall so early code paths can reach it.
coreChargeAdd = function(player, cp, bucket)
	if not player or cp <= 0 then return end
	local jp = laneState.jackpot
	if jp.state ~= "idle" then return end
	if workspace:GetServerTimeNow() < jp.coolingUntil then return end
	local get = getterOf(player)
	if get then
		cp = cp * (1 + GameConfig.statTotal(get, "coreChargePct"))
	end
	if feverActive() then
		cp = cp * GameConfig.JackpotCore.feverChargeMult
	end
	laneState.igniter = player
	if bucket == "drop" then
		laneState.pendDrop += cp
	elseif bucket == "hits" then
		laneState.pendHits += cp
	else
		laneState.pendDirect += cp
	end
end

local openJackpotWindow -- forward

local function closeJackpotWindow(reason)
	local C = GameConfig.JackpotCore
	local jp = laneState.jackpot
	if jp.state == "idle" then return end
	local wasOverload = jp.state == "overload"
	jp.state = "idle"
	jp.windowUntil = 0
	jp.hitsLeft = 0
	-- Cooling: Double Jackpot halves it; the friendliest coolingAdd among
	-- present players applies (floor 15s).
	local coolAdd, halfCool = 0, false
	for _, plr in ipairs(Players:GetPlayers()) do
		local get = getterOf(plr)
		if get then
			coolAdd = math.min(coolAdd, GameConfig.statTotal(get, "coolingAdd"))
			if (get("UpDoubleJackpot") or 0) > 0 then halfCool = true end
		end
	end
	local cool = math.max(15, (halfCool and C.coolingSec / 2 or C.coolingSec) + coolAdd)
	jp.coolingUntil = workspace:GetServerTimeNow() + cool
	-- Charge Keeper: the best keepChargePct among players survives the close.
	local keep = 0
	for _, plr in ipairs(Players:GetPlayers()) do
		local get = getterOf(plr)
		if get then keep = math.max(keep, GameConfig.statTotal(get, "keepChargePct")) end
	end
	laneState.charge = math.min(30, C.meterMax * keep)
	pushLaneAttrs()
	fireLaneEvent(wasOverload and "overload_close" or "jackpot_close",
		wasOverload and "OVERLOAD" or "JACKPOT", { 3 },
		{ wasOverload and C.overload.mult or 25 }, { 25 }, nil, 0, reason or "")
end

openJackpotWindow = function(igniter)
	local C = GameConfig.JackpotCore
	local jp = laneState.jackpot
	if jp.state ~= "idle" then return end
	-- COSMIC OVERLOAD roll — per-igniter pity, persisted in the doc so
	-- rejoining can neither re-roll a miss nor reset progress.
	local isOverload = false
	if igniter and igniter.Parent then
		local doc = docByPlayer[igniter]
		if doc then
			doc.lane = doc.lane or { mutDrops = 0, mutDry = 0, ovPity = 0 }
			doc.lane.ovPity += 1
			local get = getterOf(igniter)
			local pityAt = math.max(10, C.overload.pityAt
				+ (get and GameConfig.statTotal(get, "ovPityAdd") or 0))
			local p = C.overload.baseChance
				+ math.max(0, doc.lane.ovPity - C.overload.rampAfter) * C.overload.rampPer
			if doc.lane.ovPity >= pityAt or math.random() < p then
				isOverload = true
				doc.lane.ovPity = 0
			end
			igniter:SetAttribute("OverloadPity", doc.lane.ovPity)
		end
	end
	local nowT = workspace:GetServerTimeNow()
	if isOverload then
		jp.state = "overload"
		jp.windowUntil = nowT + C.overload.windowSec
		jp.hitsLeft = C.overload.windowHits
	else
		jp.state = "active"
		jp.windowUntil = nowT + C.windowSec
		jp.hitsLeft = C.windowHits
	end
	laneState.charge = 0
	pushLaneAttrs()
	fireLaneEvent(isOverload and "overload_open" or "jackpot_open",
		isOverload and "OVERLOAD" or "JACKPOT", { 3 }, { 25 },
		{ isOverload and C.overload.mult or 0 },
		isOverload and C.overload.windowSec or C.windowSec, jp.hitsLeft,
		igniter and igniter.Name or "?")
end

-- Called by the 1s heartbeat: applies the per-second caps, opens windows,
-- times them out, and mirrors the meter attribute (max 1 write/s).
local function laneHeartbeat()
	local C = GameConfig.JackpotCore
	local jp = laneState.jackpot
	local nowT = workspace:GetServerTimeNow()
	if jp.state ~= "idle" and nowT > jp.windowUntil + GameConfig.Lanes.rewindSec then
		closeJackpotWindow("time")
	end
	local add = math.min(laneState.pendDrop, C.capDropPerSec)
		+ math.min(laneState.pendHits, C.capHitsPerSec)
		+ laneState.pendDirect
	laneState.pendDrop, laneState.pendHits, laneState.pendDirect = 0, 0, 0
	if add > 0 and jp.state == "idle" and nowT >= jp.coolingUntil then
		laneState.charge = math.min(C.meterMax, laneState.charge + add)
		if laneState.charge >= C.meterMax then
			openJackpotWindow(laneState.igniter)
		end
	end
	remotes:SetAttribute("JackpotCharge", math.floor(laneState.charge * 10) / 10)
end

-- One legit drop volley = one mutation-cadence tick for the dropper.
local function tryMutation(player)
	local doc = docByPlayer[player]
	if not doc then return end
	doc.lane = doc.lane or { mutDrops = 0, mutDry = 0, ovPity = 0 }
	local L = doc.lane
	L.mutDrops += 1
	player:SetAttribute("MutPity", L.mutDrops)
	local M = GameConfig.Mutations
	local get = getterOf(player)
	local start = math.max(8, M.pityStart
		+ (get and GameConfig.statTotal(get, "mutStartAdd") or 0))
	if L.mutDrops < start then return end
	local p = M.pityRamp * (L.mutDrops - start + 1)
	if L.mutDrops < M.pityCap and math.random() >= p then return end
	local nowT = workspace:GetServerTimeNow()
	-- Parked (M keeps its value, retries next volley) while the board is
	-- busy: an active mutation, the global cooldown, or a jackpot window.
	if laneState.mut ~= nil or nowT < laneState.globalCdUntil
		or laneState.jackpot.state ~= "idle" then
		return
	end
	-- Legal deck for THIS trigger: per-entry cooldowns, tree unlocks
	-- (TWIN SURGE / OUTER CHARGE — survive Rebirth), center quiet while the
	-- core cools, and never the same id three times running.
	local legal, totalW = {}, 0
	for _, entry in ipairs(M.deck) do
		local ok = nowT >= (laneState.cdUntil[entry.id] or 0)
		if ok and entry.unlock then
			ok = get ~= nil and GameConfig.statTotal(get, entry.unlock) > 0
		end
		if ok and entry.target == "center" and nowT < laneState.jackpot.coolingUntil then
			ok = false
		end
		if ok and entry.id == laneState.hist[1] and entry.id == laneState.hist[2] then
			ok = false
		end
		if ok then
			local w = entry.weight
			if entry.id == "double10" or entry.id == "rim3" or entry.id == "prime25" then
				w = w * (M.dryWeightRamp ^ (L.mutDry or 0)) -- dry-streak pity
			end
			totalW += w
			table.insert(legal, { entry = entry, w = w })
		end
	end
	if #legal == 0 then return end
	local pick = math.random() * totalW
	local chosen
	for _, cand in ipairs(legal) do
		pick -= cand.w
		if pick <= 0 then
			chosen = cand.entry
			break
		end
	end
	chosen = chosen or legal[#legal].entry
	L.mutDrops = 0
	player:SetAttribute("MutPity", 0)
	if chosen.id == "single10" or chosen.id == "outer3" then
		L.mutDry = (L.mutDry or 0) + 1
	else
		L.mutDry = 0
	end
	local lanes, old, new
	if chosen.target == "one10" then
		local ln = math.random() < 0.5 and 2 or 4
		lanes, old, new = { ln }, { 10 }, { chosen.to }
	elseif chosen.target == "both10" then
		lanes, old, new = { 2, 4 }, { 10, 10 }, { chosen.to, chosen.to }
	elseif chosen.target == "one3" then
		local ln = math.random() < 0.5 and 1 or 5
		lanes, old, new = { ln }, { 3 }, { chosen.to }
	elseif chosen.target == "both3" then
		lanes, old, new = { 1, 5 }, { 3, 3 }, { chosen.to, chosen.to }
	else
		lanes, old, new = { 3 }, { 25 }, { chosen.to }
	end
	local dur = chosen.dur + (get and GameConfig.statTotal(get, "mutDurAdd") or 0)
	laneState.cdUntil[chosen.id] = nowT + chosen.cd
	laneState.globalCdUntil = nowT + M.globalCooldown
	table.insert(laneState.hist, 1, chosen.id)
	laneState.hist[3] = nil
	local payload = fireLaneEvent("mutate", chosen.id, lanes, old, new, dur, 0, player.Name)
	laneState.mut = payload
	for i, ln in ipairs(lanes) do
		laneState.mult[ln] = new[i]
	end
	pushLaneAttrs()
	remotes:SetAttribute("LaneMutSeq", payload.seq)
	remotes:SetAttribute("LaneMutId", chosen.id)
	remotes:SetAttribute("LaneMutLanes", table.concat(lanes, ","))
	remotes:SetAttribute("LaneMutStartAt", payload.animStartAt)
	remotes:SetAttribute("LaneMutExpiresAt", payload.expiresAt)
	local mySeq = payload.seq
	task.delay(dur, function()
		-- Announce the rewind; the payout keeps the boosted value until
		-- rewindEndAt (laneMultAt), so the sign is never stingier than the pay.
		if laneState.mut and laneState.mut.seq == mySeq then
			fireLaneEvent("revert", chosen.id, lanes, new, old, nil, 0, "")
		end
	end)
	task.delay(dur + GameConfig.Lanes.rewindSec, function()
		if laneState.mut and laneState.mut.seq == mySeq then
			laneState.mut = nil
			for _, ln in ipairs(lanes) do
				laneState.mult[ln] = laneState.base[ln]
			end
			pushLaneAttrs()
			remotes:SetAttribute("LaneMutId", "")
		end
	end)
end

pushLaneAttrs()

-- Discovery hooks (forward-declared above grantBall): core charge +
-- Finder's Fee, both only possible now that the engine + creditTickets exist.
onDiscovery = function(player, tier)
	coreChargeAdd(player, GameConfig.JackpotCore.charge.discovery, "direct")
	local get = getterOf(player)
	if get then
		local per = GameConfig.statTotal(get, "finderTixPer")
		if per > 0 then
			local tierInfo = PusherMachine.Tiers[tier]
			creditTickets(player, per * ((tierInfo and tierInfo.payout) or 1), false)
		end
	end
end

-- Studio-only test hook: force lane events so visual proofs don't need an
-- hour of organic play. Never exists in a published server.
if game:GetService("RunService"):IsStudio() then
	local debugLane = makeRemote("DebugLane")
	debugLane.OnServerEvent:Connect(function(player, what)
		local doc = docByPlayer[player]
		if not doc then return end
		doc.lane = doc.lane or { mutDrops = 0, mutDry = 0, ovPity = 0 }
		if what == "mutation" then
			doc.lane.mutDrops = GameConfig.Mutations.pityCap
			laneState.globalCdUntil = 0
			tryMutation(player)
		elseif what == "jackpot" then
			laneState.igniter = player
			laneState.jackpot.coolingUntil = 0
			openJackpotWindow(player)
		elseif what == "overload" then
			doc.lane.ovPity = 99
			laneState.jackpot.coolingUntil = 0
			openJackpotWindow(player)
		end
	end)
end

local function wireCollector(machine)
	local collector = machine.Payout.Collector
	local burstHost = machine.FX:FindFirstChild("PayBurstHost")
	local burst = burstHost and burstHost:FindFirstChild("PayBurst")
	local clink = burstHost and burstHost:FindFirstChild("Clink")

	local conn = collector.Touched:Connect(function(hit)
		if hit.Parent ~= machine then return end
		if hit:GetAttribute("Paid") then return end

		local owner = ownerOf(hit)
		local get = owner and getterOf(owner) or nil

		local amount = nil
		if hit.Name == "Planet" then
			-- Rarity pays: tier payout stamped on the ball at spawn time
			-- (COMMON 1 ... SECRET 250, boosted by the fusion form).
			amount = hit:GetAttribute("Payout") or 1
		elseif hit.Name == "Coin" then
			-- House coins: 1 + Golden Rim, boosted by House Payday.
			amount = 1
			if get then
				amount = (1 + GameConfig.statTotal(get, "houseCoinValueAdd"))
					* (1 + GameConfig.statTotal(get, "houseCoinPct"))
			end
		elseif hit.Name == "Capsule" then
			amount = 10
		end
		if not amount then return end

		-- Payout slots: where the piece crosses the mouth decides the
		-- PHYSICAL lane (bands FROZEN — blades and pins agree). The 7 signs
		-- map onto 5 LOGICAL lanes; the effective multiplier comes from the
		-- server's laneState timeline (mutations, jackpot windows, overload).
		local relXS = collector.CFrame:PointToObjectSpace(hit.Position).X
		local relX = math.abs(relXS)
		local lane
		if relX < 0.55 then
			lane = 4
		elseif relX < 1.9 then
			lane = relXS < 0 and 3 or 5
		elseif relX < 3.3 then
			lane = relXS < 0 and 2 or 6
		else
			lane = relXS < 0 and 1 or 7
		end
		local logical = GameConfig.Lanes.physicalToLogical[lane]
		local coreStrike = lane == 4
		local nowT = workspace:GetServerTimeNow()
		local jp = laneState.jackpot
		local mult = laneMultAt(logical, nowT)
		local jackpotHit = false
		local windowPay = nil
		local baseAmount = amount
		-- House/orphaned pieces never enter the window: an ownerless coin
		-- must not drain the pot, burn hitsLeft, or broadcast a "?" jackpot —
		-- it falls through to the ordinary resting-sign payment below.
		if logical == 3 and (jp.state == "active" or jp.state == "overload")
			and nowT <= jp.windowUntil + GameConfig.Lanes.rewindSec
			and jp.hitsLeft > 0
			and owner ~= nil and hit:GetAttribute("OwnerId") ~= nil then
			local C = GameConfig.JackpotCore
			if jp.state == "overload" then
				-- COSMIC OVERLOAD: flat x1000 through the normal chain.
				windowPay = baseAmount * C.overload.mult
				jackpotHit = true
				jp.hitsLeft -= 1
			elseif coreStrike then
				-- GRAND SLAM: the remaining pot (amplified), never less than
				-- what the resting sign would have paid.
				local prize = math.max(getPot() or 0, mult * baseAmount)
				if get then
					prize = math.floor(prize * (1 + GameConfig.statTotal(get, "jackpotAmpPct")))
				end
				windowPay = prize
				setPot(C.potSeed)
				jackpotHit = true
				jp.hitsLeft = 0
			else
				-- Pot share per center hit; the odometer visibly bleeds.
				local share = math.max(C.shareMin,
					math.floor((getPot() or 0) * C.sharePct))
				windowPay = math.max(share, mult * baseAmount)
				setPot(math.max(C.potSeed, (getPot() or 0) - share))
				jackpotHit = true
				jp.hitsLeft -= 1
			end
			pushLaneAttrs()
			fireLaneEvent("jackpot_hit", jp.state == "overload" and "OVERLOAD" or "JACKPOT",
				{ 3 }, {}, {}, nil, jp.hitsLeft, owner and owner.Name or "?")
			if jp.hitsLeft <= 0 then
				task.defer(closeJackpotWindow, "hits")
			end
		end
		if windowPay then
			amount = windowPay
		else
			amount = baseAmount * mult
			-- INTEGRITY: by construction the paid mult can never fall below
			-- the resting sign. Any occurrence is a real bug — log it loud.
			if mult < laneState.base[logical] then
				warn(("[LANE-INTEGRITY] logical=%d paid=x%d base=x%d t=%.2f")
					:format(logical, mult, laneState.base[logical], nowT))
			end
		end
		-- Jackpot core charge from OWNED collects only (house pieces never
		-- charge — same legitimacy rule as the fever meter).
		if owner and hit:GetAttribute("OwnerId") then
			local C = GameConfig.JackpotCore
			local cp
			if logical == 3 then
				cp = C.charge.hit25 * (1 + (get and GameConfig.statTotal(get, "nearMissPct") or 0))
				if coreStrike then cp += C.charge.coreStrike end
			elseif logical == 2 or logical == 4 then
				cp = C.charge.hit10
			else
				cp = C.charge.hit3
				if get and GameConfig.statTotal(get, "outerCharge") > 0 then
					cp *= 2 -- OUTER CHARGE hex
				end
			end
			local form = hit:GetAttribute("Form") or 0
			if form > 0 and get then
				cp *= 1 + form * GameConfig.statTotal(get, "formChargePct")
			end
			if hit:GetAttribute("Comet") then
				cp += C.charge.discovery -- Comet Planet super-charge
			end
			coreChargeAdd(owner, cp, "hits")
		end

		-- Comet meter fill + mission + QuickCharge hooks (flush is 1x/s).
		-- Fill comes ONLY from pieces the player put in play (OwnerId): house
		-- coins feeding the meter let each fever's meteor shower refuel the
		-- next one — live testing chain-fired fevers back to back.
		if owner then
			if hit:GetAttribute("OwnerId") then
				local fill = GameConfig.Fever.fillByLane[lane] or 1
				if hit.Name == "Capsule" then
					fill = fill + GameConfig.Fever.fillCapsuleBonus
				end
				addFever(owner, fill)
			end
			if lane == 2 or lane == 6 then
				bumpDaily(owner, 2, 1) -- HIT 3X LANE
			end
			if lane == 3 or lane == 4 or lane == 5 then
				bigHitAt[owner] = os.clock() -- QuickCharge window opens
			end
		end

		if not jackpotHit then
			-- Third arg = the honest multiplier paid (floaters/tallies);
			-- fourth = whose piece, so only the owner's client floats it.
			slotRemote:FireAllClients(lane, false, mult, owner and owner.UserId or nil)
		end
		if eventDoublePay then amount = amount * 2 end

		hit:SetAttribute("Paid", true)
		if jackpotHit then
			-- Window payout: `amount` already IS the share / grand slam /
			-- overload prize. Pot money pays FLAT (it is ticket-denominated);
			-- the overload x1000 rides the normal scaled chain.
			local credited = creditTickets(owner, amount, jp.state ~= "overload")
			local shown = math.floor((jp.state == "overload" and credited or amount) + 0.5)
			-- Jackpot/BestDrop stats: only pieces the player put in play.
			if owner and hit:GetAttribute("OwnerId") then
				owner:SetAttribute("JackpotsHit", (owner:GetAttribute("JackpotsHit") or 0) + 1)
				if shown > (owner:GetAttribute("BestDrop") or 0) then
					owner:SetAttribute("BestDrop", shown)
				end
			end
			slotRemote:FireAllClients(lane, true, jp.state == "overload" and 1000 or 0,
				owner and owner.UserId or nil)
			paidRemote:FireAllClients(shown)
			jackpotRemote:FireAllClients(owner and owner.Name or "?", shown)
			if burst then burst:Emit(60) end
			if clink then
				clink.PlaybackSpeed = 0.7
				clink:Play()
			end
			task.delay(1.4, function()
				if hit.Parent then hit:Destroy() end
			end)
			return
		end
		-- Mini Pot: a 10X hit can skim 10% of the pot, flat (pot untouched,
		-- no JackpotHit fanfare — just the CoinPaid feed).
		if (lane == 2 or lane == 6) and get and getPot() then
			if math.random() < GameConfig.statTotal(get, "miniPotPct") then
				local skim = math.floor(getPot() * 0.1)
				if skim > 0 then
					creditTickets(owner, skim, true)
					paidRemote:FireAllClients(skim)
				end
			end
		end
		if burst then burst:Emit(amount <= 2 and 8 or 30) end
		if clink then
			clink.PlaybackSpeed = 0.9 + math.random() * 0.25
			clink:Play()
		end
		if getPot() then
			-- Pot feed slowed 25x: the odometer now IS the prize (windows
			-- drain it via shares; grand slams reseed it).
			setPot(getPot() + math.max(1, math.floor(amount)))
		end
		local credited = creditTickets(owner, amount)
		-- BestDrop = biggest single credited collect from the player's own piece.
		if owner and hit:GetAttribute("OwnerId")
			and credited > (owner:GetAttribute("BestDrop") or 0) then
			owner:SetAttribute("BestDrop", credited)
		end
		paidRemote:FireAllClients(math.floor(amount + 0.5))
		-- Collected = gone: despawn after the burst covers it.
		task.delay(0.55, function()
			if hit.Parent then hit:Destroy() end
		end)
	end)
	table.insert(current.conns, conn)
end

local function spawnHouseCoin()
	pruneDeadHead()
	if not current.machine or #liveCoins >= LIVE_COIN_CAP then return end
	local coin = PusherMachine.dropCoin(current.machine, (math.random() * 2 - 1) * 4)
	if coin then table.insert(liveCoins, coin) end
end

local function teardownMachine()
	for _, conn in ipairs(current.conns) do
		conn:Disconnect()
	end
	current.conns = {}
	if current.stopDrive then
		current.stopDrive()
		current.stopDrive = nil
	end
	if current.machine then
		current.machine:Destroy()
		current.machine = nil
	end
	potValue = nil -- mirror died with the model; potAmount carries the pot
	liveCoins = {}
end

local function setupMachine(zoneIndex)
	currentZone = zoneIndex
	remotes:SetAttribute("Zone", zoneIndex)
	PusherMachine.buildEnvironment(CFrame.new(0, 0, 0), zoneIndex)
	local machine = PusherMachine.build(CFrame.new(0, 0, 0), nil, zoneIndex)
	machine.Parent = workspace
	-- Starter Pile: best seed bonus across the server's players.
	local seedExtra = 0
	for _, player in ipairs(Players:GetPlayers()) do
		seedExtra = math.max(seedExtra, GameConfig.statTotal(getterOf(player), "seedPileAdd"))
	end
	PusherMachine.seedPile(machine, 32 + seedExtra, 1)
	current.machine = machine
	-- Pot authority: the first build's seed becomes the truth; every later
	-- rebuild writes the surviving pot back into the fresh mirror.
	potValue = machine:FindFirstChild("JackpotValue")
	if potAmount == nil then
		potAmount = potValue and potValue.Value or 0
	elseif potValue then
		potValue.Value = potAmount
	end
	current.stopDrive = PusherMachine.start(machine)
	wireCollector(machine)
	-- A rebuild mid-fever must keep the sped-up pusher.
	if feverActive() then
		setPusherSpeed(machine, GameConfig.Fever.speedMult)
	end
	for _, child in ipairs(machine:GetChildren()) do
		if child.Name == "Coin" and child:IsA("BasePart") then
			table.insert(liveCoins, child)
		end
	end
end

setupMachine(1)

-- Hide the default SpawnLocation pad (it keeps working).
for _, child in ipairs(workspace:GetChildren()) do
	if child:IsA("SpawnLocation") then
		child.Transparency = 1
		local decal = child:FindFirstChildOfClass("Decal")
		if decal then decal:Destroy() end
	end
end

local zoneSwitchAt = 0
local repaintRankBoard -- forward: the leaderboard section assigns it

zoneRemote.OnServerEvent:Connect(function(player, zoneIndex)
	zoneIndex = tonumber(zoneIndex)
	if not zoneIndex or zoneIndex ~= zoneIndex then return end
	zoneIndex = math.clamp(math.floor(zoneIndex), 1, ZONE_COUNT)
	-- Denials answer back — the client toasts the reason.
	local req = GameConfig.Zones.reqs[zoneIndex] or 0
	if (player:GetAttribute("Rebirth") or 0) < req then
		zoneRemote:FireClient(player, "denied", "NEEDS " .. req .. " REBIRTHS")
		return
	end
	if zoneIndex == currentZone then return end -- already there: stay silent
	local now = os.clock()
	if now - zoneSwitchAt < 3 then
		zoneRemote:FireClient(player, "denied", "ZONE JUST SWITCHED — ONE SEC")
		return
	end
	zoneSwitchAt = now
	teardownMachine()
	setupMachine(zoneIndex)
	-- The rebuild replaced ArcadeRoom's RankBoard: repaint from the cache
	-- now instead of leaving it blank until the next 90s tick.
	if repaintRankBoard then repaintRankBoard() end
	for _, plr in ipairs(Players:GetPlayers()) do
		if docByPlayer[plr] then
			checkThrones(plr)
		end
	end
end)

--------------------------------------------------------------------------------
-- Meteor Rain (fever internals keep their names): trigger, meteor shower,
-- and the Galaxy Chest at fever end
--------------------------------------------------------------------------------

-- Deterministic chest, granted at fever END (F = lifetime FeverCount).
-- Tickets credited SCALED; every 5th chest rolls RARE+ (20th ULTRA+),
-- replaced by flat dust for restricted players on a PURCHASED fever.
local function grantGalaxyChest(player, purchased)
	local get = getterOf(player)
	local chest = GameConfig.Fever.chest
	local feverCount = player:GetAttribute("FeverCount") or 0
	local bonus = 1 + GameConfig.statTotal(get, "chestBonusPct")
	local tickets = math.floor((chest.baseTickets
		+ chest.ticketsPerFever * math.min(feverCount, chest.ticketsFeverCap)) * bonus + 0.5)
	local credited = creditTickets(player, tickets, false)
	-- Dust Refinery applies to chest dust too (phase 2 desc: "duplicates
	-- and chests").
	local dust = math.floor((chest.baseDust
		+ chest.dustPerFever * math.min(feverCount, chest.dustFeverCap))
		* bonus * (1 + GameConfig.statTotal(get, "dustPct")) + 0.5)
	player:SetAttribute("StarDust", (player:GetAttribute("StarDust") or 0) + dust)
	if feverCount > 0 and feverCount % chest.rarePlusEvery == 0 then
		local minTier = (feverCount % chest.ultraPlusEvery == 0) and 3 or 2
		if purchased and player:GetAttribute("PaidRandomRestricted") then
			-- No purchased randomness for restricted players: flat dust.
			player:SetAttribute("StarDust", (player:GetAttribute("StarDust") or 0) + 250)
			dust = dust + 250
		else
			local spec, oneIn = PusherMachine.rollOnce(GameConfig.computeRollLuck(get), minTier, rollBoostMag(player))
			local tier = spec.t or 1
			local dustGained = grantBall(player, spec.n, tier)
			player:SetAttribute("TotalRolls", (player:GetAttribute("TotalRolls") or 0) + 1)
			if tier > (player:GetAttribute("BestTier") or 1) then
				player:SetAttribute("BestTier", tier)
			end
			checkAchievements(player)
			rolledRemote:FireClient(player, spec.n, math.floor(oneIn + 0.5), tier, true, dustGained)
		end
	end
	chestRemote:FireClient(player, credited, dust)
end

-- Meter freezes at 100, the global window opens (FeverBy BEFORE FeverUntil,
-- protocol §6.3), the pusher speeds up, and meteors rain. The chest is owed
-- at fever end (chestQueue). `purchased` marks the INSTANT METEOR RAIN
-- product for the restricted-region chest-roll substitution.
local function triggerFever(player, purchased)
	local get = getterOf(player)
	local now = workspace:GetServerTimeNow()
	local dur = GameConfig.Fever.durationBase + GameConfig.statTotal(get, "feverDurAdd")
	if chestQueue[player] then
		-- Already riding their own fever (product re-trigger): extend only.
		remotes:SetAttribute("FeverBy", player.Name)
		remotes:SetAttribute("FeverUntil",
			math.max(remotes:GetAttribute("FeverUntil") or 0, now + dur))
		return
	end
	player:SetAttribute("FeverMeter", GameConfig.Fever.meterMax) -- frozen at 100
	player:SetAttribute("FeverCount", (player:GetAttribute("FeverCount") or 0) + 1)
	bumpDaily(player, 3, 1) -- START A METEOR RAIN
	checkAchievements(player)
	local wasActive = feverActive()
	remotes:SetAttribute("FeverBy", player.Name)
	remotes:SetAttribute("FeverUntil",
		math.max(remotes:GetAttribute("FeverUntil") or 0, now + dur))
	if not wasActive and current.machine then
		setPusherSpeed(current.machine, GameConfig.Fever.speedMult)
	end
	feverBlastRemote:FireAllClients(player.Name)
	chestQueue[player] = { purchased = purchased == true }
	-- Meteor shower: house coins + a prize capsule over a few seconds.
	task.spawn(function()
		local coins = GameConfig.Fever.meteorCoins + GameConfig.statTotal(get, "meteorCoinsAdd")
		local step = GameConfig.Fever.meteorOverSec / math.max(1, coins)
		for i = 1, coins do
			spawnHouseCoin()
			if i == math.ceil(coins / 2) and current.machine then
				for _ = 1, GameConfig.Fever.meteorCapsules do
					PusherMachine.dropCapsule(current.machine, (math.random() * 2 - 1) * 3.5)
				end
			end
			task.wait(step)
		end
	end)
end

--------------------------------------------------------------------------------
-- Dropping: spawns the equipped ball, respects the coin budget
--------------------------------------------------------------------------------

local function absorbOldestFieldCoin()
	for index = 1, #liveCoins do
		local coin = liveCoins[index]
		if coin.Parent and coin.Position.Z > -5 and not coin:GetAttribute("Paid") then
			table.remove(liveCoins, index)
			coin:SetAttribute("Paid", true)
			creditTickets(firstPlayer(), 1)
			paidRemote:FireAllClients(1)
			coin:Destroy()
			return
		end
	end
end

local function spawnPlayerPlanet(player, xOffset)
	if not current.machine then return end
	pruneDeadHead()
	if #liveCoins >= LIVE_COIN_CAP then
		absorbOldestFieldCoin()
	end
	-- Drops spawn the player's EQUIPPED ball + form — rolling happens only
	-- in the gacha roller. Payout rides the ball's tier + fusion form.
	local equipped = player:GetAttribute("EquippedBall") or "Bubblegum|1"
	local eqName, eqTier = equipped:match("^(.*)|(%d+)$")
	eqName = eqName or "Bubblegum"
	eqTier = tonumber(eqTier) or 1
	local spec = PusherMachine.findSpec(eqName, eqTier) or PusherMachine.Planets[1]
	local form = player:GetAttribute("EquippedForm") or 0
	local ball = PusherMachine.dropPlanet(current.machine, xOffset, 1, nil, spec, form)
	if ball then
		ball:SetAttribute("OwnerId", player.UserId)
		-- Fusion re-stamp: ASCENDED x1.5 / CELESTIAL x2.5 over the tier
		-- payout makePlanet stamped; Form drives the client-side dressing.
		ball:SetAttribute("Payout", GameConfig.fusionPayout(ball:GetAttribute("Payout") or 1, form))
		ball:SetAttribute("Form", form)
		-- HEAVY: 2.5x density physics helper — payout untouched, the node
		-- card says so. COMET: super-charges the core when collected.
		local get = getterOf(player)
		if get then
			if math.random() < GameConfig.statTotal(get, "heavyPct") then
				-- Heavies stay thuddy on purpose (their identity is the shove),
				-- just not fully dead now that everything else bounces.
				ball.CustomPhysicalProperties = PhysicalProperties.new(4.5, 0.6, 0.12, 1, 1)
				ball:SetAttribute("Heavy", 1)
			elseif math.random() < GameConfig.statTotal(get, "cometPct") then
				ball:SetAttribute("Comet", 1)
			end
		end
		table.insert(liveCoins, ball)
	end
end

-- Shared drop body (manual handler + auto-drop): spawn fan, stats, comet
-- fill, daily M1, and the Capsule Call chance.
local function doDrop(player, xOffset)
	if not current.machine then return end
	local get = getterOf(player)
	local count = GameConfig.dropCount(get)
	for i = 1, count do
		local fan = (count > 1) and (i - (count + 1) / 2) * 0.9 or 0
		spawnPlayerPlanet(player, math.clamp(xOffset + fan, -4.4, 4.4))
	end
	if (player:GetAttribute("TotalDrops") or 0) == 0 then
		logOnboarding(player, 2, "first_drop")
	end
	player:SetAttribute("TotalDrops", (player:GetAttribute("TotalDrops") or 0) + count)
	checkAchievements(player)
	addFever(player, GameConfig.Fever.fillPerDrop * count)
	bumpDaily(player, 1, count) -- DROP 25 PLANETS
	if math.random() < GameConfig.statTotal(get, "capsulePct") then
		PusherMachine.dropCapsule(current.machine, (math.random() * 2 - 1) * 3.5)
	end
end

dropRemote.OnServerEvent:Connect(function(player, xOffset)
	if not docByPlayer[player] then return end
	local get = getterOf(player)
	local cooldown = GameConfig.dropCooldown(get)
	-- QuickCharge: half cooldown for a short window after a 10X/JACKPOT.
	local window = GameConfig.statTotal(get, "quickChargeSec")
	if window > 0 and bigHitAt[player] and os.clock() - bigHitAt[player] < window then
		cooldown = cooldown * 0.5
	end
	local now = os.clock()
	if lastDropByPlayer[player] and now - lastDropByPlayer[player] < cooldown then
		return
	end
	lastDropByPlayer[player] = now

	xOffset = tonumber(xOffset) or 0
	if xOffset ~= xOffset then xOffset = 0 end -- NaN guard; clamp passes NaN through
	-- The client aims by TIMING a shared deterministic sweep — the server
	-- clamps to its own sample so an exploiter cannot place drops, only
	-- time them.
	local quick = player:GetAttribute("SweepQuick") == true
	local sx = GameConfig.sweepX(workspace:GetServerTimeNow(), quick)
	local s = GameConfig.Sweep
	xOffset = math.clamp(xOffset, sx - s.tolerance, sx + s.tolerance)
	xOffset = math.clamp(xOffset, -s.limit, s.limit)
	doDrop(player, xOffset)
	-- Twin/Triple Drop: the whole volley re-fires (staggered so the live-cap
	-- absorb path digests each wave). Extra volleys do NOT tick the mutation
	-- cadence — one cooldown-gated request = one legit volley.
	local r = math.random()
	local triple = GameConfig.statTotal(get, "tripleDropPct")
	local twin = GameConfig.statTotal(get, "twinDropPct")
	local volleys = r < triple and 3 or (r < triple + twin and 2 or 1)
	for v = 2, volleys do
		task.delay(0.28 * (v - 1), function()
			if player.Parent and docByPlayer[player] then
				doDrop(player, xOffset)
			end
		end)
	end
	coreChargeAdd(player, GameConfig.JackpotCore.charge.drop, "drop")
	tryMutation(player)
end)

--------------------------------------------------------------------------------
-- The gacha roller: free-when-ready, shared by manual rolls and auto-roll.
-- Always returns a ball (reward mechanic, not a wager); luck + boost + pity
-- shape the tier; only MAXED dupes (f2 >= 1) convert to Star Dust.
--------------------------------------------------------------------------------

local function resolveRoll(player, isAuto)
	local get = getterOf(player)
	local pity = player:GetAttribute("Pity") or 0
	-- Pity Focus shortens the guarantee (floor 30). The client pity strip
	-- reads the same formula, so "N rolls to guaranteed" stays exact.
	local pityLimit = math.max(30, GameConfig.Roll.pityLimit
		+ GameConfig.statTotal(get, "pityLimitAdd"))
	local minTier = (pity >= pityLimit) and GameConfig.Roll.pityMinTier or nil
	-- Tutorial (boost step): the free 100X's roll — the first MANUAL roll
	-- while LuckBoost > 0 — is guaranteed RARE+ so the boost provably
	-- matters (the un-boosted first roll is forced COMMON on purpose, below;
	-- two COMMONs in a row would read as "the boost did nothing"). Once
	-- ever (TutBoosted persisted), never before TutorialDone so a boost
	-- bought early can't collide with the forced-basic pull. Pity still
	-- wins when it asks for more.
	if not isAuto and player:GetAttribute("TutorialDone") == true
		and GameConfig.boostMagOf(get) > 0 -- ACTIVE boost only: LuckBoost never zeroes on expiry
		and player:GetAttribute("TutBoosted") ~= true then
		minTier = math.max(minTier or 1, 2)
		player:SetAttribute("TutBoosted", true)
		logOnboarding(player, 6, "boosted_roll")
	end
	-- DEEP LUCK weight mods: the SAME mods feed every odds display, so the
	-- numbers a player reads are the numbers the server rolls.
	local inv = invByPlayer[player]
	local mods = nil
	do
		local magnet = GameConfig.statTotal(get, "rareMagnetPct")
		local deep = GameConfig.statTotal(get, "deepResonancePct")
		local scout = GameConfig.statTotal(get, "scoutPct")
		if magnet > 0 or deep > 0 or scout > 0 then
			local owned = nil
			if scout > 0 and inv then
				owned = {}
				for k in pairs(inv) do
					owned[k] = true
				end
			end
			mods = { magnet = magnet, deep = deep, scout = scout, owned = owned }
		end
	end
	local spec, oneIn = PusherMachine.rollOnce(
		GameConfig.computeRollLuck(get, feverActive()), minTier, rollBoostMag(player), mods)
	local tier = spec.t or 1
	-- Dupe Diversifier: a duplicate may become a SAME-TIER planet the player
	-- doesn't own. Honest: within a tier all specs share one weight, so tier
	-- odds and the displayed 1-in-N are unchanged — only WHICH planet shifts.
	local dupPct = GameConfig.statTotal(get, "dupRerollPct")
	if dupPct > 0 and inv and inv[invKey(spec.n, tier)] and math.random() < dupPct then
		local unowned = {}
		for _, s2 in ipairs(PusherMachine.Planets) do
			if not s2.noRoll and (s2.t or 1) == tier
				and not inv[invKey(s2.n, s2.t or 1)] then
				table.insert(unowned, s2)
			end
		end
		if #unowned > 0 then
			spec = unowned[math.random(#unowned)]
		end
	end
	-- Tutorial: the FIRST manual roll is deliberately BASIC and deliberately
	-- UN-boosted — the free 100X is claimed AFTER it, so the boost's roll is
	-- a separate, visibly better moment; the starter forge gift (performRoll)
	-- rides this ball. (If 25 tries never hit COMMON the roll stands —
	-- harmless.)
	if not isAuto and player:GetAttribute("TutorialDone") ~= true then
		for _ = 1, 25 do
			if (spec.t or 1) == 1 then break end
			-- Bare-table roll: no luck, no boost, no fever, no mods — a
			-- server-wide 3500X window bought by someone else must not turn
			-- the tutorial pull rare (COMMON is ~75% per try at base weights,
			-- so 25 tries never fail in practice).
			spec, oneIn = PusherMachine.rollOnce(1, nil, 0, nil)
		end
		tier = spec.t or 1
	end
	player:SetAttribute("Pity", tier >= GameConfig.Roll.pityMinTier and 0 or pity + 1)
	-- Lucky Momentum + Dry Streak Shield mirrors (read by computeRollLuck on
	-- the NEXT roll; shown live on the Roll rail).
	local nowT = workspace:GetServerTimeNow()
	local lastAt = player:GetAttribute("LastRollAt") or 0
	local stacks = player:GetAttribute("MomentumStacks") or 0
	player:SetAttribute("MomentumStacks",
		(nowT - lastAt <= 45) and math.min(stacks + 1, 5) or 1)
	player:SetAttribute("LastRollAt", nowT)
	local streak = player:GetAttribute("CommonStreak") or 0
	player:SetAttribute("CommonStreak", tier == 1 and streak + 1 or 0)
	local dustGained = grantBall(player, spec.n, tier)
	player:SetAttribute("TotalRolls", (player:GetAttribute("TotalRolls") or 0) + 1)
	if tier > (player:GetAttribute("BestTier") or 1) then
		player:SetAttribute("BestTier", tier)
	end
	checkAchievements(player)
	rolledRemote:FireClient(player, spec.n, math.floor(oneIn + 0.5), tier, isAuto, dustGained)
	return spec
end

local function performRoll(player, isAuto)
	if not player:GetAttribute("DataLoaded") then return end
	local now = workspace:GetServerTimeNow()
	if now < (player:GetAttribute("NextRollAt") or 0) then return end
	local get = getterOf(player)
	player:SetAttribute("NextRollAt", now + GameConfig.autoRollInterval(get))
	local rolledSpec = resolveRoll(player, isAuto)
	if not isAuto and not player:GetAttribute("TutorialDone") then
		player:SetAttribute("TutorialDone", true) -- unlocks auto-roll
		logOnboarding(player, 3, "first_roll")
		-- Starter forge gift: enough copies to complete ONE forge recipe of
		-- the first-rolled ball — the forge step of the tutorial. Granted
		-- once ever (TutGift persisted). Copies are added directly and
		-- deliberately WITHOUT dust (grantBall's dust rule is for rolled
		-- dupes); skipped silently if the entry is missing — never error a
		-- roll.
		if player:GetAttribute("TutGift") ~= true and rolledSpec then
			local tier = rolledSpec.t or 1
			local inv = invByPlayer[player]
			local entry = inv and inv[invKey(rolledSpec.n, tier)]
			local req = GameConfig.forgeReq(get, tier)
			local need = math.max(0, (req or 10) - (entry and entry.c or 1))
			if entry and need > 0 then
				entry.c = entry.c + need
				syncInv(player)
				player:SetAttribute("TutGift", true)
				dailyToastRemote:FireClient(player, "gift", 0, 0,
					("+%d %s"):format(need, rolledSpec.n))
				logOnboarding(player, 4, "starter_gift")
			end
		end
	end
	-- Duplicate Roll: one free chained roll, never re-chains.
	if math.random() < GameConfig.statTotal(get, "dupRollPct") then
		resolveRoll(player, true)
	end
end

rollRemote.OnServerEvent:Connect(function(player)
	performRoll(player, false)
end)

-- Whitelisted client toggles.
setToggleRemote.OnServerEvent:Connect(function(player, key, value)
	if key == "AutoRoll" then
		player:SetAttribute("AutoRoll", value == true)
	elseif key == "AutoDrop" then
		player:SetAttribute("AutoDropOn", value == true)
	elseif key == "SweepQuick" then
		player:SetAttribute("SweepQuick", value == true)
	end
end)

-- EquipBall(name, tier, form): form 0/1/2 must be a form the player owns at
-- least one copy of on that ball (missing form arg = base).
equipRemote.OnServerEvent:Connect(function(player, name, tier, form)
	if type(name) ~= "string" or type(tier) ~= "number" then return end
	form = tonumber(form) or 0
	if form ~= math.floor(form) or form < 0 or form > 2 then return end
	local inv = invByPlayer[player]
	local entry = inv and inv[invKey(name, tier)]
	if not entry then return end
	local count = entry.c
	if form == 1 then
		count = entry.f1
	elseif form == 2 then
		count = entry.f2
	end
	if (count or 0) < 1 then return end
	player:SetAttribute("EquippedBall", invKey(name, tier))
	player:SetAttribute("EquippedForm", form)
end)

--------------------------------------------------------------------------------
-- Fusion: combine reqByTier[t] copies of a form into 1 of the next form;
-- Star Dust crafts extra BASE copies of discovered planets (the dust sink)
--------------------------------------------------------------------------------

-- One fusion step. Returns newForm (or nil if not affordable/valid). If the
-- consumed form was equipped and its count hits 0, the server AUTO-EQUIPS
-- the newly created form — a player is never stranded on an empty form.
local function doCombine(player, name, tier, fromForm)
	local inv = invByPlayer[player]
	if not inv then return nil end
	local entry = inv[invKey(name, tier)]
	if not entry then return nil end
	-- Owner protection (loop-addendum §2): locked entries never forge. The
	-- client confirms and calls ToggleLock first — the server stays strict.
	if (entry.lk or 0) == 1 then return nil end
	local req = GameConfig.Fusion.reqByTier[tier]
	if not req then return nil end
	-- Forge Mastery (survives Rebirth): COMMON/RARE recipes need fewer
	-- copies, floor 8. The client forge page reads the same formula.
	do
		local get = getterOf(player)
		if get and tier <= 2 then
			req = math.max(8, req + GameConfig.statTotal(get, "forgeReqAdd"))
		end
	end
	local field = (fromForm == 0) and "c" or "f1"
	local toField = (fromForm == 0) and "f1" or "f2"
	local have = entry[field] or 0
	if have < req then return nil end
	entry[field] = have - req
	entry[toField] = (entry[toField] or 0) + 1
	local newForm = fromForm + 1
	coreChargeAdd(player, GameConfig.JackpotCore.charge.forge, "direct")
	-- Tutorial completion reward, once ever (FirstForgeDone persisted): the
	-- first forge is the tutorial's last step — a flat 500-ticket bonus and
	-- the TUTORIAL COMPLETE celebration (kind "tutorial"; the 5th arg is the
	-- display line).
	if player:GetAttribute("FirstForgeDone") ~= true then
		player:SetAttribute("FirstForgeDone", true)
		creditTickets(player, 500, true)
		dailyToastRemote:FireClient(player, "tutorial", 500, 0, "FIRST FORGE!")
		logOnboarding(player, 7, "first_forge")
	end
	if entry[field] == 0
		and player:GetAttribute("EquippedBall") == invKey(name, tier)
		and (player:GetAttribute("EquippedForm") or 0) == fromForm then
		player:SetAttribute("EquippedForm", newForm)
	end
	return newForm
end

local function fusedPayoutOf(tier, form)
	local tierInfo = PusherMachine.Tiers[tier] or PusherMachine.Tiers[1]
	return GameConfig.fusionPayout(tierInfo.payout, form)
end

local function validFusionArgs(name, tier, fromForm)
	if type(name) ~= "string" or type(tier) ~= "number" then return false end
	if tier ~= math.floor(tier) or tier < 1 or tier > 5 then return false end
	if fromForm ~= 0 and fromForm ~= 1 then return false end
	return true
end

combineRemote.OnServerEvent:Connect(function(player, name, tier, fromForm)
	fromForm = tonumber(fromForm) or -1
	if not validFusionArgs(name, tier, fromForm) then return end
	local newForm = doCombine(player, name, tier, fromForm)
	if not newForm then return end
	syncInv(player)
	combineDoneRemote:FireClient(player, name, tier, newForm, fusedPayoutOf(tier, newForm))
end)

-- CombineAll (FORGE MAX): repeat while affordable (hard cap 20). One
-- CombineDone at the end so the celebration plays once, not twenty times.
-- Bulk NEVER consumes the equipped form's last copy (loop-addendum §2) —
-- only an explicit, client-confirmed CombineBall may do that (auto-requip).
combineAllRemote.OnServerEvent:Connect(function(player, name, tier, fromForm)
	fromForm = tonumber(fromForm) or -1
	if not validFusionArgs(name, tier, fromForm) then return end
	local inv = invByPlayer[player]
	local entry = inv and inv[invKey(name, tier)]
	if not entry then return end
	-- Effective recipe size — Forge Mastery included, same req doCombine
	-- consumes, or the equipped-form guard below stops the bulk one short.
	local req = GameConfig.forgeReq(getterOf(player), tier)
	if not req then return end
	local field = (fromForm == 0) and "c" or "f1"
	local newForm = nil
	for _ = 1, 20 do
		if player:GetAttribute("EquippedBall") == invKey(name, tier)
			and (player:GetAttribute("EquippedForm") or 0) == fromForm
			and ((entry[field] or 0) - req) < 1 then
			break -- would zero the equipped form: bulk stops one short
		end
		local got = doCombine(player, name, tier, fromForm)
		if not got then break end
		newForm = got
	end
	if not newForm then return end
	syncInv(player)
	combineDoneRemote:FireClient(player, name, tier, newForm, fusedPayoutOf(tier, newForm))
end)

-- ToggleLock(name, tier): flip the entry's lk field (0/1, persisted). Locked
-- entries are refused by doCombine (both FORGE! and FORGE MAX paths).
toggleLockRemote.OnServerEvent:Connect(function(player, name, tier)
	if type(name) ~= "string" or type(tier) ~= "number" then return end
	if tier ~= math.floor(tier) or tier < 1 or tier > 5 then return end
	local inv = invByPlayer[player]
	local entry = inv and inv[invKey(name, tier)]
	if not entry then return end
	entry.lk = ((entry.lk or 0) == 1) and 0 or 1
	syncInv(player)
end)

-- CraftCopy(name, tier): Star Dust -> +1 BASE copy of a DISCOVERED planet.
craftRemote.OnServerEvent:Connect(function(player, name, tier)
	if type(name) ~= "string" or type(tier) ~= "number" then return end
	if tier ~= math.floor(tier) or tier < 1 or tier > 5 then return end
	local inv = invByPlayer[player]
	local entry = inv and inv[invKey(name, tier)]
	if not entry then return end
	local cost = GameConfig.DustCraft.costByTier[tier]
	if not cost then return end
	local dust = player:GetAttribute("StarDust") or 0
	if dust < cost then return end
	player:SetAttribute("StarDust", dust - cost)
	entry.c = entry.c + 1
	syncInv(player)
end)

-- On join, push the starting inventory to the client (initPlayer also
-- re-fires syncInv once DataLoaded lands, in case load took > 2s).
task.spawn(function()
	task.wait(2)
	for _, player in ipairs(Players:GetPlayers()) do syncInv(player) end
end)
Players.PlayerAdded:Connect(function(player)
	task.wait(2)
	syncInv(player)
end)

--------------------------------------------------------------------------------
-- Leaderboards (loop-final §B/§E + loop-addendum §4): 3 OrderedDataStores,
-- dirty-checked batched writes (autosave piggyback + leave + close + rebirth
-- moments), ONE 90s reader task caching top-50 per board -> LeaderboardSync
-- to all (+ once to late joiners), and the physical TOP EARNERS RankBoard.
-- Studio without API access: pcall'd — the whole system no-ops, boards empty.
--------------------------------------------------------------------------------

local LB_DEFS = { -- board key -> source attr -> ODS name (loop-leaderboard §1)
	{ board = "earned", attr = "TotalEarned", store = "PD_LB_Earned_v1" },
	{ board = "rebirth", attr = "Rebirth", store = "PD_LB_Rebirth_v1" },
	{ board = "found", attr = "IndexFound", store = "PD_LB_Found_v1" },
}

local lbStores = nil
do
	local ok, stores = pcall(function()
		local acquired = {}
		for _, def in ipairs(LB_DEFS) do
			acquired[def.board] = DataStoreService:GetOrderedDataStore(def.store)
		end
		return acquired
	end)
	if ok then
		lbStores = stores
	else
		warn("[Leaderboard] OrderedDataStore unavailable — ranks disabled")
	end
end

local lbCache = { earned = {}, rebirth = {}, found = {} } -- last assembled top-50s
local lbHash = "" -- cheap change hash: top-10 userIds+values per board
local lbNameCache = {} -- userId -> resolved name (session-permanent)

-- Any "Studio" op error kills the system for the session (PlayerData pattern).
local function lbStudioCheck(err)
	if string.find(tostring(err), "Studio", 1, true) then
		lbStores = nil
		warn("[Leaderboard] no API access in Studio — ranks disabled")
	end
end

-- Dirty-checked batched submit of the player's 3 values. Values are read
-- synchronously (safe from PlayerRemoving); writes ride one spawned thread.
local function submitBoards(player)
	if not lbStores then return end
	if not player:GetAttribute("DataLoaded") then return end
	local sent = lbLastSent[player]
	local writes = {}
	for _, def in ipairs(LB_DEFS) do
		local value = math.clamp(math.floor(tonumber(player:GetAttribute(def.attr)) or 0), 0, 2 ^ 62)
		if not sent or sent[def.board] ~= value then
			table.insert(writes, { board = def.board, store = lbStores[def.board], value = value })
		end
	end
	if #writes == 0 then return end
	local key = tostring(player.UserId)
	task.spawn(function()
		for _, write in ipairs(writes) do
			local ok, err = pcall(function()
				write.store:SetAsync(key, write.value)
			end)
			if ok then
				-- Record only AFTER the write lands: a throttled SetAsync
				-- must not silence this stat for the rest of the session.
				if sent then
					sent[write.board] = write.value
				end
			else
				lbStudioCheck(err)
				if not lbStores then return end
			end
		end
	end)
end

local function lbShortValue(value)
	if value >= 1e12 then
		return string.format("%.1fT", value / 1e12)
	elseif value >= 1e9 then
		return string.format("%.1fB", value / 1e9)
	elseif value >= 1e6 then
		return string.format("%.1fM", value / 1e6)
	elseif value >= 1e4 then
		return string.format("%.1fK", value / 1e3)
	end
	return tostring(value)
end

-- Physical TOP EARNERS board (machine agent builds it in buildEnvironment;
-- nil-guarded until it lands, and re-found every cycle — zone switches
-- rebuild ArcadeRoom). Writes rows 1-5 of the EARNED cache directly.
local function updateRankBoard()
	local room = workspace:FindFirstChild("ArcadeRoom")
	local board = room and room:FindFirstChild("RankBoard")
	local gui = board and board:FindFirstChild("BoardGui", true)
	if not gui then return end
	for i = 1, 5 do
		local row = gui:FindFirstChild("Row" .. i, true)
		if row then
			local data = lbCache.earned[i]
			local rankLabel = row:FindFirstChild("RankLabel")
			local nameLabel = row:FindFirstChild("NameLabel")
			local valueLabel = row:FindFirstChild("ValueLabel")
			if rankLabel then
				rankLabel.Text = "#" .. i
			end
			if nameLabel then
				nameLabel.Text = data and data.name or "---"
			end
			if valueLabel then
				valueLabel.Text = data and lbShortValue(data.value) or ""
			end
		end
	end
end

-- Zone-switch hook (forward local up top): only worth a repaint once the
-- reader task has actually filled the cache.
repaintRankBoard = function()
	if #lbCache.earned > 0 then
		updateRankBoard()
	end
end

-- One reader task: 90s cadence, skipped on an empty server. GetSorted budget
-- 3 per 90s = 2/min vs 5+2P/min — holds from P=1 (loop-final §E). Names:
-- online short-circuit to DisplayName, cold misses staggered 0.1s, cached
-- for the session.
task.spawn(function()
	task.wait(5) -- first fetch runs early so the page is warm
	while true do
		if lbStores and #Players:GetPlayers() > 0 then
			local boards = {}
			for _, def in ipairs(LB_DEFS) do
				if not lbStores then break end
				local list = {}
				local ok, page = pcall(function()
					return lbStores[def.board]:GetSortedAsync(false, 50)
				end)
				if ok and page then
					for _, row in ipairs(page:GetCurrentPage()) do
						local userId = tonumber(row.key)
						if userId then
							table.insert(list, { userId = userId, value = row.value, name = "" })
						end
					end
				elseif not ok then
					lbStudioCheck(page)
					list = lbCache[def.board] or list -- transient failure: keep last good page
				end
				boards[def.board] = list
			end
			if lbStores then
				for _, def in ipairs(LB_DEFS) do
					for _, row in ipairs(boards[def.board]) do
						local name = lbNameCache[row.userId]
						if not name then
							local online = Players:GetPlayerByUserId(row.userId)
							if online then
								name = online.DisplayName
							else
								local ok, fetched = pcall(function()
									return Players:GetNameFromUserIdAsync(row.userId)
								end)
								-- A failed lookup stays UNcached — the next
								-- 90s cycle retries instead of freezing "?".
								name = ok and fetched or nil
								task.wait(0.1) -- stagger cold lookups
							end
							if name then
								lbNameCache[row.userId] = name
							end
						end
						row.name = name or "?" -- render-only fallback
					end
				end
				local parts = {}
				for _, def in ipairs(LB_DEFS) do
					local list = boards[def.board]
					for i = 1, math.min(10, #list) do
						table.insert(parts, list[i].userId .. ":" .. list[i].value)
					end
					table.insert(parts, "|")
				end
				local hash = table.concat(parts, ",")
				lbCache = boards
				if hash ~= lbHash then
					lbHash = hash
					leaderboardSyncRemote:FireAllClients(lbCache)
				end
				updateRankBoard()
			end
		end
		task.wait(90)
	end
end)

-- Final board write for everyone at shutdown (own binding; the doc-save
-- BindToClose runs alongside — both fit the 30s window).
game:BindToClose(function()
	if not lbStores then return end
	for _, player in ipairs(Players:GetPlayers()) do
		submitBoards(player)
	end
	task.wait(2) -- give the spawned SetAsyncs a moment
end)

-- Lucky Spin: the reel is showmanship — the outcome is a GUARANTEED 100X
-- rare-weight boost (LuckBoost magnitude 100, tier-capped by the machine;
-- no paid random outcome, so no odds-disclosure obligation; the Index shows
-- the live boosted odds regardless). First spin free; after that the client
-- routes through the dev-product popup.
spinRemote.OnServerEvent:Connect(function(player)
	if not docByPlayer[player] then return end
	if player:GetAttribute("FreeSpinUsed") then return end
	-- Tutorial order is authoritative: the free 100X is claimed AFTER the
	-- first (deliberately un-boosted) roll, so the boost can never burn down
	-- during the coaching steps or taint the forced-basic pull.
	if player:GetAttribute("TutorialDone") ~= true then return end
	player:SetAttribute("FreeSpinUsed", true)
	player:SetAttribute("LuckBoost", 100)
	-- 3s of reel theater + 90s of boost.
	player:SetAttribute("LuckBoostUntil", workspace:GetServerTimeNow() + 93)
	-- The claim flows STRAIGHT into the boosted roll: waiting out a roll
	-- cooldown while a 90s boost burns down is the worst feeling in the
	-- funnel (and the tutorial's ROLL AGAIN step must be pressable now).
	player:SetAttribute("NextRollAt", workspace:GetServerTimeNow())
	logOnboarding(player, 5, "claim_100x")
end)

-- Shop: JOIN THE GROUP claim (+25% luck & +25% tickets forever). GroupId
-- comes from GameConfig; while unconfigured (0) the claim only works in
-- Studio playtests — live it fails closed (a free perk with no group to
-- join would be handed out to anyone who fires the remote).
claimGroupRemote.OnServerEvent:Connect(function(player)
	if player:GetAttribute("GroupPerk") then return end
	local groupId = GameConfig.GroupId or 0
	if groupId > 0 then
		local ok, inGroup = pcall(function()
			return player:IsInGroup(groupId)
		end)
		if not (ok and inGroup) then return end
	elseif not RunService:IsStudio() then
		return -- fail closed: no group configured, not a playtest
	end
	player:SetAttribute("GroupPerk", true)
end)

-- Rebirth: pay the ladder cost (GameConfig.RebirthCosts), lose tickets +
-- every tree node, keep thrones/dust/fusion/collection/perks, gain
-- +75% tickets forever per rebirth and unlock deeper zones.
rebirthRemote.OnServerEvent:Connect(function(player)
	if not docByPlayer[player] then return end
	local rebirth = player:GetAttribute("Rebirth") or 0
	local cost = GameConfig.rebirthCost(rebirth)
	if not cost then return end -- maxed out
	local tickets = player:GetAttribute("Tickets") or 0
	if tickets < cost then return end
	player:SetAttribute("Tickets", 0)
	-- The whole balance is the sink (rebirth wipes it; cost is only the gate).
	logEconomy(player, Enum.AnalyticsEconomyFlowType.Sink, "tickets", tickets, 0,
		Enum.AnalyticsEconomyTransactionType.Shop, "rebirth")
	local tree = treeByPlayer[player]
	if tree then
		-- Filtered wipe: nodes flagged resetsOnRebirth = false (TWIN SURGE,
		-- OUTER CHARGE, Forge Mastery) are permanent ratchets and survive.
		for nodeId in pairs(tree) do
			local node = GameConfig.TreeById[nodeId]
			if not node or node.resetsOnRebirth ~= false then
				tree[nodeId] = nil
				player:SetAttribute(nodeId, nil)
			end
		end
	end
	player:SetAttribute("Rebirth", rebirth + 1)
	checkThrones(player)
	checkAchievements(player)
	submitBoards(player) -- milestone moment: the REBIRTHS board stays fresh
end)

-- Shop: one-time permanent multipliers, paid in tickets (GameConfig.Perks).
perkRemote.OnServerEvent:Connect(function(player, perkId)
	if not docByPlayer[player] then return end
	for _, perk in ipairs(GameConfig.Perks) do
		if perk.id == perkId then
			if (player:GetAttribute(perk.attr) or 1) >= perk.value then return end
			local tickets = player:GetAttribute("Tickets") or 0
			if tickets < perk.cost then return end
			player:SetAttribute("Tickets", tickets - perk.cost)
			logEconomy(player, Enum.AnalyticsEconomyFlowType.Sink, "tickets", perk.cost,
				tickets - perk.cost, Enum.AnalyticsEconomyTransactionType.Shop, perk.id)
			player:SetAttribute(perk.attr, perk.value)
			return
		end
	end
end)

--------------------------------------------------------------------------------
-- Monetization: ProcessReceipt (idempotent via doc.meta.receipts) + policy
--------------------------------------------------------------------------------

-- Every boost grant announces itself server-wide with an HONEST label
-- (server-scoped grants say THIS SERVER).
local function announceBoost(player, label)
	boostAnnounceRemote:FireAllClients(player.Name, label)
end

-- Personal timed boost, extend-or-replace stacking: buying the SAME
-- magnitude again EXTENDS the timer (+duration); buying a DIFFERENT one
-- REPLACES it (remaining time discarded — the shop labels this).
local function grantTimedBoost(player, magAttr, untilAttr, mag, duration)
	local now = workspace:GetServerTimeNow()
	local curMag = player:GetAttribute(magAttr) or 0
	local curUntil = player:GetAttribute(untilAttr) or 0
	if curUntil > now and curMag == mag then
		player:SetAttribute(untilAttr, curUntil + duration)
	else
		player:SetAttribute(magAttr, mag)
		player:SetAttribute(untilAttr, now + duration)
	end
end

-- Server-wide timed boost on the remotes folder (server clock epochs —
-- late joiners see the remaining time automatically). Same stacking rule.
local function grantServerBoost(magAttr, untilAttr, mag, duration)
	local now = workspace:GetServerTimeNow()
	local curMag = remotes:GetAttribute(magAttr) or 0
	local curUntil = remotes:GetAttribute(untilAttr) or 0
	if curUntil > now and curMag == mag then
		remotes:SetAttribute(untilAttr, curUntil + duration)
	else
		remotes:SetAttribute(magAttr, mag)
		remotes:SetAttribute(untilAttr, now + duration)
	end
end

-- Luck products are refused server-side for restricted regions (the client
-- hides them there anyway — defense in depth, fail-closed).
local function refuseIfRestricted(player)
	if player:GetAttribute("PaidRandomRestricted") then
		error("PaidRandomRestricted")
	end
end

local function productByKey(key)
	for _, product in ipairs(GameConfig.Products) do
		if product.key == key then
			return product
		end
	end
	return nil
end

local function grantPersonalLuck(player, key)
	refuseIfRestricted(player)
	local product = productByKey(key)
	grantTimedBoost(player, "LuckBoost", "LuckBoostUntil", product.mag, product.duration)
	announceBoost(player, product.name)
end

local function grantPersonalTix(player, key)
	local product = productByKey(key)
	grantTimedBoost(player, "TixBoost", "TixBoostUntil", product.mag, product.duration)
	announceBoost(player, product.name)
end

local function grantStarterPack(player, doc)
	if doc.meta.products.Starter then return end -- one-time
	doc.meta.products.Starter = true
	player:SetAttribute("StarterOwned", true)
	local product = productByKey("Starter")
	creditTickets(player, product and product.tickets or 1500, true)
	player:SetAttribute("StarDust",
		(player:GetAttribute("StarDust") or 0) + (product and product.dust or 150))
	local planet = product and product.planet or "Gilded Terra"
	grantBall(player, planet, specialTier(planet))
end

-- METEOR RAIN FOR EVERYONE: the buyer triggers a full rush (window, meter
-- freeze, meteor shower) and EVERY other online loaded player is owed a
-- Galaxy Chest at rush end too.
local function grantSrvRush(player, _doc)
	triggerFever(player, true)
	for _, plr in ipairs(Players:GetPlayers()) do
		if plr ~= player and docByPlayer[plr] and plr:GetAttribute("DataLoaded")
			and not chestQueue[plr] then
			-- keepMeter: a recipient's own meter was mid-fill, not frozen at
			-- 100 — rush end must not carry-reset it.
			chestQueue[plr] = { purchased = false, keepMeter = true }
		end
	end
	announceBoost(player, "METEOR RAIN FOR THIS SERVER")
end

local PRODUCT_GRANT_FNS = {
	Tix2x15 = function(player, _doc)
		grantPersonalTix(player, "Tix2x15")
	end,
	Tix3x15 = function(player, _doc)
		grantPersonalTix(player, "Tix3x15")
	end,
	Luck100 = function(player, _doc)
		grantPersonalLuck(player, "Luck100")
	end,
	Luck1000 = function(player, _doc)
		grantPersonalLuck(player, "Luck1000")
	end,
	Luck3500 = function(player, _doc)
		grantPersonalLuck(player, "Luck3500")
	end,
	RushNow = function(player, _doc)
		triggerFever(player, true)
		announceBoost(player, "INSTANT METEOR RAIN")
	end,
	PotBoost = function(player, _doc)
		local product = productByKey("PotBoost")
		-- Paid credit lands on the authority — a zone rebuild can't eat it.
		setPot((getPot() or 0) + (product and product.amount or 5000))
		announceBoost(player, "JACKPOT +5,000 FOR THIS SERVER")
	end,
	MegaPack = function(player, _doc)
		-- Composite: contains a luck boost, so the whole bundle is
		-- restricted-region refused (client hides it there).
		refuseIfRestricted(player)
		local tix = productByKey("Tix3x15")
		local luck = productByKey("Luck1000")
		grantTimedBoost(player, "TixBoost", "TixBoostUntil", tix.mag, tix.duration)
		grantTimedBoost(player, "LuckBoost", "LuckBoostUntil", luck.mag, luck.duration)
		triggerFever(player, true)
		announceBoost(player, "MEGA BUNDLE — 3X TIX + 1000X LUCK + RAIN")
	end,
	SrvLuck100 = function(player, _doc)
		refuseIfRestricted(player)
		local product = productByKey("SrvLuck100")
		grantServerBoost("SrvLuckMag", "SrvLuckUntil", product.mag, product.duration)
		announceBoost(player, "100X LUCK FOR THIS SERVER — 90 SEC")
	end,
	SrvTix2x = function(player, _doc)
		local product = productByKey("SrvTix2x")
		grantServerBoost("SrvTixMag", "SrvTixUntil", product.mag, product.duration)
		announceBoost(player, "2X TICKETS FOR THIS SERVER — 10 MIN")
	end,
	SrvRush = grantSrvRush,
	Starter = grantStarterPack,
	Tickets2xPass = function(player, doc)
		doc.meta.products.Tickets2xPass = true
		player:SetAttribute("Tickets2xOwned", true)
	end,
	AutoDropPass = function(player, doc)
		doc.meta.products.AutoDropPass = true
		player:SetAttribute("AutoDropOwned", true)
	end,
}

MarketplaceService.ProcessReceipt = function(receiptInfo)
	local player = Players:GetPlayerByUserId(receiptInfo.PlayerId)
	if not player then return Enum.ProductPurchaseDecision.NotProcessedYet end
	local doc = docByPlayer[player]
	if not doc then return Enum.ProductPurchaseDecision.NotProcessedYet end
	-- A fresh fallback doc must never overwrite the real account: don't
	-- grant into data that can't save — the purchase retries after a
	-- healthy load instead of vanishing.
	if player:GetAttribute("DataLoadFailed") == true then
		return Enum.ProductPurchaseDecision.NotProcessedYet
	end
	-- Idempotency: receipt history rides the doc (no separate store).
	for _, purchaseId in ipairs(doc.meta.receipts) do
		if purchaseId == receiptInfo.PurchaseId then
			return Enum.ProductPurchaseDecision.PurchaseGranted
		end
	end
	-- Resolve the grant at call time — PId_<key> attrs may be set post-boot.
	local grantFn, matched = nil, nil
	for _, product in ipairs(GameConfig.Products) do
		local id = remotes:GetAttribute("PId_" .. product.key) or 0
		if id ~= 0 and id == receiptInfo.ProductId then
			grantFn = PRODUCT_GRANT_FNS[product.key]
			matched = product
			break
		end
	end
	if not grantFn then return Enum.ProductPurchaseDecision.NotProcessedYet end
	local ok, err = pcall(grantFn, player, doc)
	if not ok then
		-- Restricted-region refusal is PERMANENT for this player:
		-- NotProcessedYet would redeliver forever (charged, granted
		-- nothing, repeatedly). Client + server both prevent the prompt
		-- in the first place — this is the last-resort stop.
		if string.find(tostring(err), "PaidRandomRestricted", 1, true) then
			warn(("receipt %s for restricted player %d swallowed (PaidRandomRestricted)")
				:format(tostring(receiptInfo.PurchaseId), receiptInfo.PlayerId))
			return Enum.ProductPurchaseDecision.PurchaseGranted
		end
		return Enum.ProductPurchaseDecision.NotProcessedYet
	end
	-- PURCHASE ACTIVE moment: toast before the save so it lands instantly.
	dailyToastRemote:FireClient(player, "product", 0, 0, matched.name)
	-- Analytics: the grant landed (the save below only decides the retry).
	logEconomy(player, Enum.AnalyticsEconomyFlowType.Source, "robux", matched.robux or 0, 0,
		Enum.AnalyticsEconomyTransactionType.IAP, matched.key)
	logFunnel(player, "purchase", tostring(receiptInfo.PurchaseId), 1, "granted")
	table.insert(doc.meta.receipts, receiptInfo.PurchaseId)
	if #doc.meta.receipts > 50 then table.remove(doc.meta.receipts, 1) end
	local snapshot = PlayerData.snapshot(player, invByPlayer[player],
		treeByPlayer[player], doc, sessionStartAt[player])
	if PlayerData.save(player.UserId, snapshot, "receipt") then
		return Enum.ProductPurchaseDecision.PurchaseGranted
	end
	-- Save failed: retried later; the receipts history dedupes the re-grant.
	return Enum.ProductPurchaseDecision.NotProcessedYet
end

--------------------------------------------------------------------------------
-- Persistence lifecycle: load (protocol §6.1 order), save on leave/auto/close
--------------------------------------------------------------------------------

local function initPlayer(player)
	-- A leave-then-rejoin in the same server must re-arm the shutdown save:
	-- clear the stale leave markers before anything else.
	leftSaved[player.UserId] = nil
	saving[player] = nil

	-- §6.1 step 1: static session attrs first (before any yield). Boosts are
	-- session-only: LuckBoost is a magnitude (100/1000/3500, 0 = none).
	player:SetAttribute("LuckBoost", 0)
	player:SetAttribute("LuckBoostUntil", 0)
	player:SetAttribute("TixBoost", 0)
	player:SetAttribute("TixBoostUntil", 0)
	player:SetAttribute("FeverMeter", 0)
	player:SetAttribute("OfflinePaid", 0)
	player:SetAttribute("PaidRandomRestricted", true) -- fail-closed default
	task.spawn(function()
		local ok, info = pcall(function()
			return PolicyService:GetPolicyInfoForPlayerAsync(player)
		end)
		if ok and info and player.Parent then
			policyByPlayer[player] = info
			player:SetAttribute("PaidRandomRestricted", info.ArePaidRandomItemsRestricted == true)
		end
	end)

	-- §6.1 step 2: load (yields).
	local doc, ok = PlayerData.load(player.UserId)
	if player.Parent == nil then return end -- left mid-load: discard, never save
	player:SetAttribute("DataLoadFailed", not ok)

	-- §6.1 step 3: doc -> attributes (tree nodes, daily, EquippedForm inside).
	local invMap = PlayerData.apply(player, doc)
	player:SetAttribute("NextRollAt", workspace:GetServerTimeNow()) -- first roll hot

	-- §6.1 step 4: session tables, daily rollover, thrones, offline payout.
	invByPlayer[player] = invMap
	local tree = {}
	for nodeId, level in pairs(doc.tree) do
		tree[nodeId] = level
	end
	treeByPlayer[player] = tree
	docByPlayer[player] = doc
	sessionStartAt[player] = os.time()
	nextAutoSaveAt[player] = os.time() + 120 + player.UserId % 31
	feverPending[player] = 0
	lastPlayStamp[player] = os.time()
	lbLastSent[player] = {}
	-- Connected-loop derived attrs (apply set the raw mirrors):
	-- IndexFound rebuilt from the inv map — never persisted (loop-index §1).
	local found = 0
	for _ in pairs(invMap) do
		found = found + 1
	end
	player:SetAttribute("IndexFound", found)
	player:SetAttribute("StreakReady",
		os.time() - (doc.daily.sLast or 0) >= GameConfig.Streak.minGapHours * 3600)
	player:SetAttribute("StreakLast", doc.daily.sLast or 0) -- client countdown mirror
	player:SetAttribute("PlayReady", playReadyMask(doc))
	rolloverDaily(player)
	checkThrones(player)
	if ownsProduct(player, "AutoDropPass") then
		player:SetAttribute("AutoDropOwned", true)
	end
	if ownsProduct(player, "Tickets2xPass") then
		player:SetAttribute("Tickets2xOwned", true)
	end
	if ownsProduct(player, "Starter") then
		player:SetAttribute("StarterOwned", true)
	end
	-- Offline earnings (economy §8): a fraction of a modest baseline, from
	-- the SAVED tree levels (just applied to attributes). Credited RAW —
	-- the rate below is already fully multiplied.
	local get = getterOf(player)
	local paid = 0
	local lastSeen = doc.meta.lastSeen or 0
	if lastSeen > 0 then
		local hours = math.min((os.time() - lastSeen) / 3600,
			GameConfig.Offline.baseCapHours + GameConfig.statTotal(get, "offlineCapHoursAdd"))
		if hours > 0 then
			local throne = GameConfig.throneOf(player:GetAttribute("ThroneLevel") or 0)
			local ratePerHour = GameConfig.Offline.baseTPM * 60
				* (GameConfig.Zones.mults[currentZone] or 1)
				* GameConfig.rebirthTicketMult(player:GetAttribute("Rebirth") or 0)
				* (throne and throne.tixMult or 1)
				* GameConfig.ticketTreeMult(get)
			paid = math.floor(hours * ratePerHour
				* (GameConfig.Offline.baseRateFrac + GameConfig.statTotal(get, "offlineRateAdd")))
			if paid > 0 then
				creditTickets(player, paid, true)
			end
			local dustPay = math.floor(hours * GameConfig.statTotal(get, "offlineDustPerHour"))
			if dustPay > 0 then
				player:SetAttribute("StarDust", (player:GetAttribute("StarDust") or 0) + dustPay)
			end
		end
	end
	player:SetAttribute("OfflinePaid", paid)

	-- §6.1 step 5: DataLoaded STRICTLY LAST, then a fresh inventory push
	-- (the 2s join push may have fired before the doc landed) and the cached
	-- leaderboard table once (late joiners don't wait for the 90s tick).
	player:SetAttribute("DataLoaded", true)
	syncInv(player)
	leaderboardSyncRemote:FireClient(player, lbCache)
	logOnboarding(player, 1, "joined")
end

Players.PlayerAdded:Connect(function(player)
	task.spawn(initPlayer, player)
end)
for _, player in ipairs(Players:GetPlayers()) do
	task.spawn(initPlayer, player)
end

-- Consolidated leave: snapshot synchronously (pure reads), save async, THEN
-- clear session tables (protocol §6.2 — the snapshot needs the inventory).
Players.PlayerRemoving:Connect(function(player)
	local doc = docByPlayer[player]
	-- DataLoadFailed: a fresh fallback doc must never overwrite the real
	-- account (the client already shows PROGRESS WON'T SAVE).
	if doc and not saving[player]
		and player:GetAttribute("DataLoadFailed") ~= true then
		saving[player] = true -- never cleared: autosave skips leavers
		submitBoards(player) -- final board write (values read synchronously)
		local snapshot = PlayerData.snapshot(player, invByPlayer[player],
			treeByPlayer[player], doc, sessionStartAt[player])
		local userId = player.UserId
		task.spawn(function()
			PlayerData.save(userId, snapshot, "leave")
			-- Only stamp "already saved" if they have NOT rejoined this
			-- server while the save was in flight (retries span seconds) —
			-- otherwise BindToClose would skip their whole new session.
			local back = Players:GetPlayerByUserId(userId)
			if not (back and docByPlayer[back]) then
				leftSaved[userId] = true
			end
		end)
	end
	invByPlayer[player] = nil
	treeByPlayer[player] = nil
	docByPlayer[player] = nil
	sessionStartAt[player] = nil
	nextAutoSaveAt[player] = nil
	policyByPlayer[player] = nil
	feverPending[player] = nil
	chestQueue[player] = nil
	bigHitAt[player] = nil
	lastDropByPlayer[player] = nil
	lastAutoDropAt[player] = nil
	lastPlayStamp[player] = nil
	lbLastSent[player] = nil
	-- Analytics: flush the coalesced ticket credits, drop the session set.
	flushTixFlow(player)
	tixFlowPending[player] = nil
	tixFlowAt[player] = nil
	onboardingSent[player] = nil
end)

-- Autosave: 120s + per-player jitter so a full server doesn't burst-write.
task.spawn(function()
	while true do
		task.wait(10)
		for _, player in ipairs(Players:GetPlayers()) do
			if docByPlayer[player] and not saving[player]
				-- fallback doc: never overwrite the real account
				and player:GetAttribute("DataLoadFailed") ~= true
				and os.time() >= (nextAutoSaveAt[player] or math.huge) then
				nextAutoSaveAt[player] = os.time() + 120 + player.UserId % 31
				submitBoards(player) -- leaderboard piggyback (dirty-checked)
				local snapshot = PlayerData.snapshot(player, invByPlayer[player],
					treeByPlayer[player], docByPlayer[player], sessionStartAt[player])
				local userId = player.UserId
				task.spawn(function()
					PlayerData.save(userId, snapshot, "auto")
				end)
			end
		end
	end
end)

game:BindToClose(function()
	local pending = 0
	for _, player in ipairs(Players:GetPlayers()) do
		local doc = docByPlayer[player]
		if doc and not saving[player] and not leftSaved[player.UserId]
			-- fallback doc: never overwrite the real account
			and player:GetAttribute("DataLoadFailed") ~= true then
			saving[player] = true
			pending += 1
			local snapshot = PlayerData.snapshot(player, invByPlayer[player],
				treeByPlayer[player], doc, sessionStartAt[player])
			local userId = player.UserId
			task.spawn(function()
				PlayerData.save(userId, snapshot, "close")
				pending -= 1
			end)
		end
	end
	local deadline = os.clock() + 25
	while pending > 0 and os.clock() < deadline do
		task.wait(0.2)
	end
end)

--------------------------------------------------------------------------------
-- The 1s heartbeat: fever end, meter flush + trigger, auto-roll, auto-drop,
-- and the 60s daily-rollover ticker. One loop, five duties.
--------------------------------------------------------------------------------

local heartbeatTick = 0

task.spawn(function()
	while true do
		task.wait(1)
		heartbeatTick += 1
		local now = workspace:GetServerTimeNow()

		-- Jackpot core: apply the per-second charge caps, open/expire
		-- windows, mirror the meter attribute (one write/s).
		laneHeartbeat()

		-- Fever END: slow the pusher back down FIRST, then open the owed
		-- Galaxy Chests and restart meters at the Comet Chain carryover.
		local feverUntil = remotes:GetAttribute("FeverUntil") or 0
		if feverUntil ~= 0 and now >= feverUntil then
			if current.machine then
				setPusherSpeed(current.machine, 1)
			end
			remotes:SetAttribute("FeverUntil", 0)
			remotes:SetAttribute("FeverBy", "")
			for player, entry in pairs(chestQueue) do
				chestQueue[player] = nil
				if player.Parent then
					grantGalaxyChest(player, entry.purchased)
					-- SrvRush recipients keep their own (frozen) meter; the
					-- rush owner's meter restarts at the Comet Chain carry.
					if not entry.keepMeter then
						local carry = math.floor(GameConfig.Fever.meterMax
							* GameConfig.statTotal(getterOf(player), "feverCarryPct") + 0.5)
						player:SetAttribute("FeverMeter", math.min(GameConfig.Fever.meterMax - 1, carry))
					end
				end
			end
		end

		for _, player in ipairs(Players:GetPlayers()) do
			if player:GetAttribute("DataLoaded") then
				local get = getterOf(player)

				-- Comet meter flush: capped per second, scaled by Fever
				-- Charge, ONE attribute write; meterMax triggers the fever.
				-- Frozen (no fill) while the player rides their own fever.
				-- The cap RATE-LIMITS instead of discarding: a 10-fill core
				-- collect drains in over several seconds, so aiming for the
				-- middle finally outfills grinding the rims. Meter runs
				-- fractional; the client's %d display floors it.
				local pendingFill = feverPending[player] or 0
				if chestQueue[player] then
					feverPending[player] = 0 -- frozen while riding their own rain
				elseif pendingFill > 0 then
					local gain = math.min(pendingFill, GameConfig.Fever.fillCapPerSec)
					-- Bank at most ~15s of trailing fill: the meter should lag
					-- ACTIVE play a little, not fire a rain half a minute after
					-- the player walked away.
					feverPending[player] = math.min(pendingFill - gain, 30)
					gain = gain * (1 + GameConfig.statTotal(get, "feverFillPct"))
					local meter = player:GetAttribute("FeverMeter") or 0
					local newMeter = math.min(GameConfig.Fever.meterMax, meter + gain)
					if newMeter ~= meter then
						player:SetAttribute("FeverMeter", newMeter)
					end
					if newMeter >= GameConfig.Fever.meterMax then
						triggerFever(player, false)
					end
				end

				-- Auto-roll: free once the tutorial (first manual roll) is done.
				if player:GetAttribute("AutoRoll") and player:GetAttribute("TutorialDone")
					and now >= (player:GetAttribute("NextRollAt") or 0) then
					performRoll(player, true)
				end

				-- Auto-drop: node or durable product, at 1.5x the drop
				-- cooldown (Auto Tempo trims toward 1.0x; catch-up capped so
				-- a lag spike can't burst).
				if player:GetAttribute("AutoDropOn")
					and ((player:GetAttribute("UpAutoDrop") or 0) > 0
						or ownsProduct(player, "AutoDropPass")) then
					local interval = GameConfig.dropCooldown(get)
						* math.max(1.0, 1.5 + GameConfig.statTotal(get, "autoDropRateAdd"))
					local lastAt = lastAutoDropAt[player] or 0
					local due = math.floor((os.clock() - lastAt) / interval)
					if due > 0 then
						for _ = 1, math.min(due, 3) do
							-- Hands-free drops fall where that player's
							-- carriage visibly is (shared sweep).
							doDrop(player, GameConfig.sweepX(workspace:GetServerTimeNow(),
								player:GetAttribute("SweepQuick") == true))
							-- Auto volleys are legit volleys: they charge the
							-- core and tick the mutation cadence like manual.
							coreChargeAdd(player, GameConfig.JackpotCore.charge.drop, "drop")
							tryMutation(player)
						end
						lastAutoDropAt[player] = os.clock()
					end
				else
					lastAutoDropAt[player] = os.clock()
				end

				-- Playtime accrual: os.time deltas, mutated on the doc in place
				-- (the persisted source of truth); attrs mirror 1x/30s below.
				local doc = docByPlayer[player]
				if doc then
					local stamp = os.time()
					local last = lastPlayStamp[player] or stamp
					if stamp > last then
						doc.playtime.total = doc.playtime.total + (stamp - last)
					end
					lastPlayStamp[player] = stamp
					if heartbeatTick % 30 == 0 then
						player:SetAttribute("PlaySecs", doc.playtime.total)
						player:SetAttribute("PlayReady", playReadyMask(doc))
					end
				end

				-- Daily rollover + streak-ready ticker (UTC day boundary; the
				-- rollover never touches sDay/sLast — no ticker/claim race).
				if heartbeatTick % 60 == 0 then
					rolloverDaily(player)
					if doc then
						player:SetAttribute("StreakReady",
							os.time() - (doc.daily.sLast or 0) >= GameConfig.Streak.minGapHours * 3600)
					end
				end
			end
		end
	end
end)

--------------------------------------------------------------------------------
-- GOLD RUSH event: every ~5 minutes, 20 seconds of double payouts + coin rain
--------------------------------------------------------------------------------

task.spawn(function()
	remotes:SetAttribute("NextEventAt", workspace:GetServerTimeNow() + 180)
	while true do
		local nextAt = remotes:GetAttribute("NextEventAt")
		while workspace:GetServerTimeNow() < nextAt do
			task.wait(1)
		end
		eventDoublePay = true
		remotes:SetAttribute("EventEndsAt", workspace:GetServerTimeNow() + 20)
		task.spawn(function()
			for _ = 1, 12 do
				spawnHouseCoin()
				task.wait(0.6)
			end
		end)
		task.wait(20)
		eventDoublePay = false
		remotes:SetAttribute("EventEndsAt", 0)
		remotes:SetAttribute("NextEventAt", workspace:GetServerTimeNow() + 300)
	end
end)

--------------------------------------------------------------------------------
-- Jackpot ticker + occasional prize capsule
--------------------------------------------------------------------------------

task.spawn(function()
	while true do
		task.wait(2 + math.random() * 3)
		if current.machine and getPot() then
			setPot(getPot() + math.random(1, 13))
		end
	end
end)

task.spawn(function()
	while true do
		task.wait(45 + math.random() * 30)
		if current.machine then
			PusherMachine.dropCapsule(current.machine, (math.random() * 2 - 1) * 3.5)
		end
	end
end)

--------------------------------------------------------------------------------
-- House refill: keep the gold hoard stocked as planets displace it
--------------------------------------------------------------------------------

local function refillTarget()
	local best = 24
	for _, player in ipairs(Players:GetPlayers()) do
		best = math.max(best, 24 + GameConfig.statTotal(getterOf(player), "houseCoinCountAdd"))
	end
	return best
end

task.spawn(function()
	while true do
		task.wait(4)
		local machine = current.machine
		if machine then
			local count = 0
			for _, child in ipairs(machine:GetChildren()) do
				if child.Name == "Coin" then count = count + 1 end
			end
			if count < refillTarget() then
				spawnHouseCoin()
				spawnHouseCoin()
			end
		end
	end
end)

--------------------------------------------------------------------------------
-- Housekeeping: cull strays, rescue side-stranded pieces, cap the tray
--------------------------------------------------------------------------------

task.spawn(function()
	-- Mouth stuck-detector state: piece -> { pos, strikes }. This is a
	-- SAFETY NET on top of correct funnel geometry (frictionless crown/
	-- plates/apron): strike 1 nudges the piece toward the basin so a real
	-- jam surface shows itself in motion; strike 2 rescues at full value.
	-- It never teleports and never fakes a respawn.
	local stuckInfo = {}
	while true do
		task.wait(4)
		local machine = current.machine
		if machine then
			local trayCoins = {}
			local seen = {}
			for _, child in ipairs(machine:GetChildren()) do
				if (child.Name == "Coin" or child.Name == "Planet" or child.Name == "Capsule")
					and child:IsA("BasePart") then
					local pos = child.Position
					-- LIP WAKER: engine sleep freezes low-velocity pieces even
					-- on the frictionless lip ramp (a slept assembly ignores
					-- gravity torque — owner capture: a whole front row parked
					-- half over the edge). A real pusher vibrates; this is the
					-- machine's vibration, at housekeeping cadence: any
					-- motionless piece in the ramp band gets a hair's-width lift
					-- and gravity does the rest. (Velocity writes are DISCARDED
					-- on deep-sleeping assemblies — measured 0.00 movement; a
					-- CFrame offset is the one write that always lands and the
					-- resulting free-fall re-enters the solver awake.)
					if pos.Z > -5.15 and pos.Z < -4.3 and pos.Y > 4.5 and pos.Y < 5.6
						and child.AssemblyLinearVelocity.Magnitude < 0.05 then
						child.CFrame = child.CFrame + Vector3.new(0, 0.06, -0.05)
					end
					if pos.Y < 0.9 or math.abs(pos.X) > 9 or pos.Z > 9 or pos.Z < -12
							or (pos.Y > 7.6 and pos.Z > 0.8) then
						child:Destroy()
					elseif pos.Z < -5.0 and pos.Y > 3.2 and math.abs(pos.X) > 4.3 then
						-- Stranded beside the tray mouth: auto-collect at the
						-- piece's real value (a stranded SECRET pays 250, not 1).
						if not child:GetAttribute("Paid") then
							child:SetAttribute("Paid", true)
							local rescue = child:GetAttribute("Payout") or 1
							local rescuer = ownerOf(child)
							if rescuer then -- house pieces pay nobody
								creditTickets(rescuer, rescue)
							end
							paidRemote:FireAllClients(rescue)
						end
						child:Destroy()
					elseif pos.Z < -5.2 and pos.Y > 1.2 and pos.Y < 5.4 then
						-- Band is z < -5.2 (past the LipRamp's end at -5.11 — a
						-- frictionless strip nothing can rest on) so the y ceiling
						-- could rise to 5.4 without ever touching the deck's front
						-- row at z >= -5.0: a still piece with its center out here
						-- is off the floor and on geometry.
						-- Mouth funnel region: PAST the ledge plane only — the
						-- first -4.4 threshold reached onto the deck lip and
						-- "rescued" legitimate slow pile coins (stress log).
						-- Anything nearly motionless here two sweeps running
						-- is wedged on geometry.
						-- DO NOT widen this band. A "y > 5.2" clause was tried
						-- to catch pieces that sit still at y 5.5-5.8 near
						-- z -5.0; a raycast proved they rest on the DECK (top
						-- y 5.00, front edge z -5.00) — that is the pusher's
						-- FRONT ROW waiting to be shoved, not a jam. Catching
						-- it auto-collected live coins and flooded this log.
						seen[child] = true
						local info = stuckInfo[child]
						if info then
							-- A piece that MOVED is doing fine: reset its strike
							-- count and leave it alone. (Dropping this check
							-- rescued pieces merely falling THROUGH the band —
							-- the housekeeping log flooded and live drops were
							-- being cashed out mid-flight.)
							if (pos - info.pos).Magnitude >= 0.2 then
								info.strikes = 0
								info.pos = pos
							else
								info.strikes = info.strikes + 1
								info.pos = pos
								if info.strikes >= 1 and info.nudges < 2 then
									-- `nudges` deliberately does NOT reset on
									-- movement: a piece that keeps re-settling on
									-- the same shelf would otherwise loop between
									-- nudge and rest forever, never escalating.
									info.nudges = info.nudges + 1
									warn(string.format("[Housekeeping] nudging wedged %s at (%.2f, %.2f, %.2f)",
										child.Name, pos.X, pos.Y, pos.Z))
									child.AssemblyLinearVelocity = Vector3.new(-math.sign(pos.X) * 0.8, 2.5, -3.5)
								elseif info.nudges >= 2 then
									warn(string.format("[Housekeeping] rescuing wedged %s at (%.2f, %.2f, %.2f)",
										child.Name, pos.X, pos.Y, pos.Z))
									if not child:GetAttribute("Paid") then
										child:SetAttribute("Paid", true)
										local rescue = child:GetAttribute("Payout") or 1
										local rescuer = ownerOf(child)
										if rescuer then -- house pieces pay nobody
											creditTickets(rescuer, rescue)
										end
										paidRemote:FireAllClients(rescue)
									end
									child:Destroy()
									stuckInfo[child] = nil
								end
							end
						else
							stuckInfo[child] = { pos = pos, strikes = 0, nudges = 0 }
						end
						if pos.Z < -5.4 and pos.Y < 3 then
							table.insert(trayCoins, child)
						end
					elseif pos.Z < -5.4 and pos.Y < 3 then
						table.insert(trayCoins, child)
					end
				end
			end
			for piece in pairs(stuckInfo) do
				if not seen[piece] then stuckInfo[piece] = nil end
			end
			if #trayCoins > 40 then
				table.sort(trayCoins, function(a, b) return a.Position.Y < b.Position.Y end)
				for i = 1, #trayCoins - 40 do
					trayCoins[i]:Destroy()
				end
			end
		end
	end
end)

--------------------------------------------------------------------------------
-- Opening: a short rain of planets so the variation roster shows itself
--------------------------------------------------------------------------------

task.spawn(function()
	task.wait(1)
	for _ = 1, 6 do
		local player = firstPlayer()
		if player and current.machine then
			spawnPlayerPlanet(player, math.random(-38, 38) / 10)
		end
		task.wait(0.4)
	end
end)

print("[PusherMachine] v6 — the connected loop is live: claims, streak, playtime, ranks, forge locks")
