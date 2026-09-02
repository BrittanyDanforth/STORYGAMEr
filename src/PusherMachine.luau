--!nonstrict
--[[
	PusherMachine v2.1 — builds a complete coin pusher cabinet out of Roblox
	primitives, plus the dark arcade room and cinematic lighting around it.
	Now fully THEMED: every color, material, and light in the build reads from
	a merged theme table, so a zone retheme is data, not code.

	Usage (ModuleScript in ReplicatedStorage):

		local PusherMachine = require(path.to.PusherMachine)
		PusherMachine.buildEnvironment(CFrame.new(), themeIdOrTable)
		local machine = PusherMachine.build(CFrame.new(), "CoinPusher", themeIdOrTable)
		machine.Parent = workspace
		PusherMachine.seedPile(machine, 45)      -- REAL physics coins (server only)
		PusherMachine.start(machine)
		PusherMachine.dropCoin(machine, xOffset)
		PusherMachine.dropCapsule(machine, xOffset)

	Themes: PusherMachine.Themes[1..7] (zone ladder). Pass an index or a table
	of overrides; nil means Themes[1] ("Neon Nights", the shipped default).

	Coins are 3-part welded assemblies (rim body + two proud face discs) —
	only the body collides. Capsules are PLANETS: a ball with two tilted
	Saturn rings, themed per zone. The old anchored deck decor is gone: the
	pile is seeded from real physics coins so nothing interpenetrates.

	Anti-z-fight rule used throughout: no two visible faces of different
	colors may share a plane. Decorative overlays float 0.01–0.05 proud of
	their host surface, never flush.

	This file must stay plain-Lua parseable (no `+=`, no `continue`) so the
	offline preview pipeline (tools/roblox_stub.lua + theme lab) can execute it.
]]

local TweenService = game:GetService("TweenService")

local PusherMachine = {}

--------------------------------------------------------------------------------
-- Defaults (Zone 1 — "Neon Nights"). Every key a theme may override.
--------------------------------------------------------------------------------

local DEFAULTS = {
	-- Playfield.
	deckTop     = 5.0,
	deckBack    = 6.5,
	ledgeZ      = -5.0,

	-- Pusher.
	pusherSize   = Vector3.new(11.4, 1.6, 4.2),
	pusherRestZ  = 3.5,
	stroke       = 3,
	cycleSeconds = 2.2,
	pusherForce  = 1e6,

	-- Coins.
	coinDiameter  = 1.1,
	coinThickness = 0.25,
	capsuleSize   = 1.2, -- MUST stay under the 1.3-stud 25X blade channels: 1.6 capsules wedged and plugged the lanes (stress-test log)

	-- Camera (consumed by PusherClient). Look-target height 5.4 frames the
	-- dropper gantry at ~78% frame height while the ledge holds the bottom.
	-- Pulled back + tilted down vs the old (0,10.9,-12.4)/(0,5.4,2.2)/52:
	-- the payout slot lanes at the tray mouth must be in frame.
	camEye  = Vector3.new(0, 12.2, -13.8),
	camLook = Vector3.new(0, 3.7, 2.2),
	camFov  = 55,

	-- Cabinet palette.
	colBody    = Color3.fromRGB(22, 26, 42),
	colBody2   = Color3.fromRGB(33, 39, 62),
	colDark    = Color3.fromRGB(13, 15, 24),
	colDeck    = Color3.fromRGB(58, 68, 98),
	colDeckLane= Color3.fromRGB(70, 82, 116),
	colGold    = Color3.fromRGB(255, 196, 74),
	colGoldHot = Color3.fromRGB(255, 226, 140),
	colGoldDim = Color3.fromRGB(96, 74, 30),
	colTeal    = Color3.fromRGB(54, 232, 214),
	colMagenta = Color3.fromRGB(255, 79, 216),
	colLane25  = Color3.fromRGB(255, 150, 70), -- 25X lane accent (no 1X lane any more)
	colRoom    = Color3.fromRGB(16, 17, 24),
	colFloor   = Color3.fromRGB(20, 21, 30),

	-- Coin look (rim body + inset-toned face discs).
	coinRim     = Color3.fromRGB(255, 196, 74),
	coinFace    = Color3.fromRGB(222, 166, 56),
	coinReflect = 0.1,

	-- Planet capsule (ball + two Saturn rings).
	capPlanet = Color3.fromRGB(255, 79, 216),
	capRing   = Color3.fromRGB(54, 232, 214),
	capRing2  = Color3.fromRGB(255, 196, 74),

	-- Deck material + text.
	deckMaterial  = Enum.Material.Metal,
	marqueeTitle  = "PLANET DROPPER",

	-- Ambient sparkle over the pile.
	sparkleColor = Color3.fromRGB(255, 226, 140),
	sparkleRate  = 3,

	-- Stadium Steps rework palette + data.
	dimTone       = 0.45, -- lane color rest tone (jackpot 0.62, hardcoded at use)
	colApron      = Color3.fromRGB(46, 38, 26),
	colBasin      = Color3.fromRGB(12, 12, 20),
	colSign       = Color3.fromRGB(16, 18, 30), -- lane sign faces: dark in EVERY theme (lightbox contrast)
	colFeverFill  = Color3.fromRGB(255, 200, 80), -- colGoldHot tone family
	colFeverHot   = Color3.fromRGB(255, 79, 216),
	colFeverPeak  = Color3.fromRGB(255, 240, 200),

	-- Room lighting rig.
	lighting = {
		clockTime = 0,
		brightness = 0.9,
		ambient = Color3.fromRGB(16, 18, 28),
		outdoorAmbient = Color3.fromRGB(7, 8, 14),
		fogColor = Color3.fromRGB(8, 9, 15),
		fogStart = 45,
		fogEnd = 130,
		keyColor = Color3.fromRGB(255, 216, 150),
		rimColor = Color3.fromRGB(140, 225, 235),
		fillColor = Color3.fromRGB(255, 120, 220),
		bloomIntensity = 0.4,
		tint = Color3.fromRGB(238, 242, 255),
	},
}

--------------------------------------------------------------------------------
-- Zone themes (ladder order; gates/multipliers live in the economy config)
--------------------------------------------------------------------------------

PusherMachine.Themes = {
	{ -- 1: the daylight default — the "normal colored" machine. Cream cabinet,
		-- grass deck, sky-blue room, bright afternoon light. Neon parts read as
		-- soft glow accents here, like ring lights in daylight.
		id = "SunnyPusher", displayName = "SUNNY PUSHER",
		colBody = Color3.fromRGB(242, 240, 232), colBody2 = Color3.fromRGB(213, 219, 227),
		colDark = Color3.fromRGB(88, 98, 114),
		colDeck = Color3.fromRGB(124, 198, 118), colDeckLane = Color3.fromRGB(148, 214, 142),
		colGoldDim = Color3.fromRGB(186, 158, 92),
		colTeal = Color3.fromRGB(84, 182, 255),
		colMagenta = Color3.fromRGB(255, 116, 96),
		colRoom = Color3.fromRGB(200, 228, 248), colFloor = Color3.fromRGB(126, 192, 112),
		openRoom = true,
		coinRim = Color3.fromRGB(255, 200, 80), coinFace = Color3.fromRGB(226, 170, 60),
		capPlanet = Color3.fromRGB(255, 150, 190), capRing = Color3.fromRGB(255, 255, 255),
		capRing2 = Color3.fromRGB(84, 182, 255),
		sparkleColor = Color3.fromRGB(255, 255, 255), sparkleRate = 2,
		lighting = {
			clockTime = 14, brightness = 1.25,
			ambient = Color3.fromRGB(146, 146, 140), outdoorAmbient = Color3.fromRGB(136, 140, 148),
			exposure = 0.1,
			keyBrightness = 2.0, contrast = 0.06, saturation = 0.08,
			fogColor = Color3.fromRGB(190, 220, 245), fogStart = 60, fogEnd = 220,
			keyColor = Color3.fromRGB(255, 245, 225), rimColor = Color3.fromRGB(200, 230, 255),
			fillColor = Color3.fromRGB(255, 220, 180),
			bloomIntensity = 0.12, tint = Color3.fromRGB(255, 252, 248),
		},
	},
	{ -- 2: the neon-arcade art (all DEFAULTS values).
		id = "NeonPusher", displayName = "NEON PUSHER",
	},
	{ -- 2
		id = "GoldenVault", displayName = "GOLDEN VAULT",
		marqueeTitle = "GOLDEN VAULT",
		colBody = Color3.fromRGB(44, 35, 24), colBody2 = Color3.fromRGB(64, 51, 34),
		colDark = Color3.fromRGB(22, 17, 11),
		colDeck = Color3.fromRGB(82, 66, 44), colDeckLane = Color3.fromRGB(104, 86, 58),
		colTeal = Color3.fromRGB(255, 234, 180),   -- brass-white accent
		colMagenta = Color3.fromRGB(255, 122, 40), -- ember
		colRoom = Color3.fromRGB(28, 22, 15), colFloor = Color3.fromRGB(34, 27, 18),
		coinRim = Color3.fromRGB(255, 208, 92), coinFace = Color3.fromRGB(208, 154, 50),
		capPlanet = Color3.fromRGB(255, 170, 60), capRing = Color3.fromRGB(255, 238, 190),
		capRing2 = Color3.fromRGB(180, 120, 40),
		sparkleColor = Color3.fromRGB(255, 236, 170),
		lighting = {
			ambient = Color3.fromRGB(26, 21, 13), outdoorAmbient = Color3.fromRGB(12, 9, 5),
			fogColor = Color3.fromRGB(14, 11, 6),
			brightness = 0.85, fogEnd = 110, bloomIntensity = 0.45,
			tint = Color3.fromRGB(255, 232, 205), keyBrightness = 1.5,
			keyColor = Color3.fromRGB(255, 208, 130), rimColor = Color3.fromRGB(255, 190, 110),
			fillColor = Color3.fromRGB(255, 140, 60),
		},
	},
	{ -- 3
		id = "SakuraParlor", displayName = "SAKURA PARLOR",
		marqueeTitle = "PETAL PUSHER",
		colBody = Color3.fromRGB(88, 48, 70), colBody2 = Color3.fromRGB(116, 66, 92),
		colDark = Color3.fromRGB(42, 22, 34),
		colDeck = Color3.fromRGB(214, 176, 190), colDeckLane = Color3.fromRGB(232, 198, 210),
		colGold = Color3.fromRGB(255, 178, 132), colGoldHot = Color3.fromRGB(255, 214, 186),
		colGoldDim = Color3.fromRGB(120, 76, 58),
		colTeal = Color3.fromRGB(255, 226, 240),
		colMagenta = Color3.fromRGB(255, 94, 150),
		colRoom = Color3.fromRGB(52, 32, 44), colFloor = Color3.fromRGB(64, 40, 54),
		coinRim = Color3.fromRGB(255, 186, 146), coinFace = Color3.fromRGB(236, 158, 126),
		capPlanet = Color3.fromRGB(255, 150, 190), capRing = Color3.fromRGB(255, 240, 246),
		capRing2 = Color3.fromRGB(255, 110, 160),
		sparkleColor = Color3.fromRGB(255, 200, 220), sparkleRate = 5,
		lighting = {
			ambient = Color3.fromRGB(38, 26, 32), outdoorAmbient = Color3.fromRGB(18, 11, 15),
			fogColor = Color3.fromRGB(30, 18, 25),
			brightness = 1.1, exposure = 0.1, fogEnd = 140,
			tint = Color3.fromRGB(255, 228, 238), keyBrightness = 1.6,
			keyColor = Color3.fromRGB(255, 214, 200), rimColor = Color3.fromRGB(255, 170, 210),
			fillColor = Color3.fromRGB(255, 150, 180), bloomIntensity = 0.35,
		},
	},
	{ -- 4
		id = "AbyssReef", displayName = "ABYSS REEF",
		marqueeTitle = "DEEP FORTUNE",
		colBody = Color3.fromRGB(14, 42, 58), colBody2 = Color3.fromRGB(22, 60, 80),
		colDark = Color3.fromRGB(7, 22, 32),
		colDeck = Color3.fromRGB(28, 70, 90), colDeckLane = Color3.fromRGB(42, 94, 118),
		colGold = Color3.fromRGB(255, 214, 120), colGoldHot = Color3.fromRGB(255, 238, 180),
		colTeal = Color3.fromRGB(80, 255, 230),
		colMagenta = Color3.fromRGB(255, 110, 120), -- coral
		colRoom = Color3.fromRGB(6, 18, 26), colFloor = Color3.fromRGB(9, 25, 35),
		coinRim = Color3.fromRGB(214, 232, 242), coinFace = Color3.fromRGB(180, 202, 218),
		capPlanet = Color3.fromRGB(60, 110, 255), capRing = Color3.fromRGB(90, 245, 235),
		capRing2 = Color3.fromRGB(200, 230, 255),
		sparkleColor = Color3.fromRGB(150, 240, 240),
		lighting = {
			ambient = Color3.fromRGB(10, 22, 30), outdoorAmbient = Color3.fromRGB(4, 10, 16),
			fogColor = Color3.fromRGB(5, 14, 22), fogStart = 35, fogEnd = 100, brightness = 0.7,
			bloomIntensity = 0.5, tint = Color3.fromRGB(195, 222, 255), keyBrightness = 1.2,
			keyColor = Color3.fromRGB(170, 230, 240), rimColor = Color3.fromRGB(90, 200, 230),
			fillColor = Color3.fromRGB(80, 140, 220),
		},
	},
	{ -- 5
		id = "MidnightCircuit", displayName = "MIDNIGHT CIRCUIT",
		marqueeTitle = "MIDNIGHT RUN",
		colBody = Color3.fromRGB(15, 13, 26), colBody2 = Color3.fromRGB(26, 22, 44),
		colDark = Color3.fromRGB(7, 6, 13),
		colDeck = Color3.fromRGB(23, 19, 40), colDeckLane = Color3.fromRGB(44, 36, 74),
		colTeal = Color3.fromRGB(70, 140, 255),
		colRoom = Color3.fromRGB(8, 7, 12), colFloor = Color3.fromRGB(11, 9, 16),
		coinRim = Color3.fromRGB(255, 230, 90), coinFace = Color3.fromRGB(214, 190, 72),
		capPlanet = Color3.fromRGB(140, 80, 255), capRing = Color3.fromRGB(80, 150, 255),
		capRing2 = Color3.fromRGB(255, 79, 216),
		sparkleColor = Color3.fromRGB(150, 170, 255),
		lighting = {
			ambient = Color3.fromRGB(13, 12, 24), outdoorAmbient = Color3.fromRGB(6, 5, 12),
			fogColor = Color3.fromRGB(7, 6, 13),
			brightness = 0.65, fogEnd = 95, tint = Color3.fromRGB(208, 212, 255), keyBrightness = 1.1,
			keyColor = Color3.fromRGB(210, 190, 255), rimColor = Color3.fromRGB(110, 150, 255),
			fillColor = Color3.fromRGB(200, 90, 255), bloomIntensity = 0.55,
		},
	},
	{ -- 6
		id = "StarfallBoardwalk", displayName = "STARFALL BOARDWALK",
		marqueeTitle = "STARFALL SWEEP",
		colBody = Color3.fromRGB(28, 20, 52), colBody2 = Color3.fromRGB(44, 32, 80),
		colDark = Color3.fromRGB(13, 10, 27),
		colDeck = Color3.fromRGB(35, 29, 64), colDeckLane = Color3.fromRGB(68, 56, 118),
		colGoldHot = Color3.fromRGB(255, 246, 210),
		colTeal = Color3.fromRGB(120, 140, 255),
		colMagenta = Color3.fromRGB(255, 120, 250),
		colRoom = Color3.fromRGB(10, 8, 22), colFloor = Color3.fromRGB(13, 11, 27),
		coinRim = Color3.fromRGB(255, 244, 200), coinFace = Color3.fromRGB(214, 204, 168),
		capPlanet = Color3.fromRGB(70, 60, 160), capRing = Color3.fromRGB(255, 250, 230),
		capRing2 = Color3.fromRGB(150, 130, 255),
		sparkleColor = Color3.fromRGB(255, 250, 230), sparkleRate = 6,
		lighting = {
			ambient = Color3.fromRGB(10, 9, 20), outdoorAmbient = Color3.fromRGB(5, 4, 10),
			fogColor = Color3.fromRGB(9, 6, 20),
			brightness = 0.7, fogEnd = 95, bloomIntensity = 0.5,
			tint = Color3.fromRGB(205, 210, 255),
			keyBrightness = 1.1, rimBrightness = 0.9, fillBrightness = 0.4,
			keyColor = Color3.fromRGB(235, 225, 255), rimColor = Color3.fromRGB(150, 160, 255),
			fillColor = Color3.fromRGB(255, 140, 250),
		},
	},
	{ -- 7
		id = "AuroraVault", displayName = "AURORA VAULT",
		marqueeTitle = "FROZEN FORTUNE",
		colBody = Color3.fromRGB(30, 52, 80), colBody2 = Color3.fromRGB(52, 86, 122),
		colDark = Color3.fromRGB(13, 25, 42),
		colDeck = Color3.fromRGB(182, 214, 236), colDeckLane = Color3.fromRGB(150, 190, 220),
		colGold = Color3.fromRGB(214, 238, 255), colGoldHot = Color3.fromRGB(240, 250, 255),
		colGoldDim = Color3.fromRGB(90, 116, 140),
		colTeal = Color3.fromRGB(120, 255, 240),
		colMagenta = Color3.fromRGB(170, 120, 255),
		colRoom = Color3.fromRGB(8, 14, 26), colFloor = Color3.fromRGB(16, 27, 42),
		coinRim = Color3.fromRGB(235, 245, 255), coinFace = Color3.fromRGB(198, 218, 236),
		capPlanet = Color3.fromRGB(225, 240, 255), capRing = Color3.fromRGB(120, 255, 180),
		capRing2 = Color3.fromRGB(140, 200, 255),
		deckMaterial = Enum.Material.Ice,
		sparkleColor = Color3.fromRGB(210, 250, 255), sparkleRate = 8,
		lighting = {
			-- Arctic night, NOT aquarium: the underwater look came from murky
			-- teal fog hugging the camera (start 30) plus a green-cyan tint.
			-- Air must be crisp — fog pushed far out and turned deep indigo,
			-- tint gone blue-violet; aurora green survives only in the rim.
			ambient = Color3.fromRGB(19, 22, 40), outdoorAmbient = Color3.fromRGB(10, 12, 26),
			fogColor = Color3.fromRGB(22, 24, 52), fogStart = 70, fogEnd = 240,
			brightness = 0.8, bloomIntensity = 0.32, tint = Color3.fromRGB(226, 232, 255),
			contrast = 0.12, saturation = 0.05, keyBrightness = 1.35,
			keyColor = Color3.fromRGB(225, 240, 255), rimBrightness = 0.8,
			rimColor = Color3.fromRGB(150, 235, 205), fillBrightness = 0.5,
			fillColor = Color3.fromRGB(165, 140, 255),
		},
	},
}

--------------------------------------------------------------------------------
-- Theme resolution
--------------------------------------------------------------------------------

local function shallowMerge(base, over)
	local out = {}
	for k, v in pairs(base) do out[k] = v end
	for k, v in pairs(over) do
		if k == "lighting" and type(v) == "table" then
			local lit = {}
			for lk, lv in pairs(base.lighting) do lit[lk] = lv end
			for lk, lv in pairs(v) do lit[lk] = lv end
			out.lighting = lit
		else
			out[k] = v
		end
	end
	return out
end

local function resolveTheme(theme)
	if type(theme) == "number" then
		return PusherMachine.Themes[theme] or {}
	elseif type(theme) == "table" then
		return theme
	end
	return {}
end

local function themedConfig(theme)
	return shallowMerge(DEFAULTS, resolveTheme(theme))
end

--------------------------------------------------------------------------------
-- Helpers
--------------------------------------------------------------------------------

local function newPart(props, parent)
	local part = Instance.new(props.Shape == "Wedge" and "WedgePart" or "Part")
	props.Shape = props.Shape ~= "Wedge" and props.Shape or nil
	part.Anchored = true
	part.CanCollide = true
	part.TopSurface = Enum.SurfaceType.Smooth
	part.BottomSurface = Enum.SurfaceType.Smooth
	part.Material = Enum.Material.SmoothPlastic
	for key, value in pairs(props) do
		part[key] = value
	end
	part.Parent = parent
	return part
end

-- Roblox's Cylinder shape puts its circular faces on the local X axis, so a coin
-- must be rolled 90 degrees about Z to sit flat.
local FLAT = CFrame.Angles(0, 0, math.rad(90))

-- Deterministic pseudo-random so every build is identical.
local function makeRng(seed)
	local state = seed % 2147483647
	if state == 0 then state = 1 end
	return function()
		state = (state * 16807) % 2147483647
		return state / 2147483647
	end
end

local function pointLight(parent, color, brightness, range)
	local light = Instance.new("PointLight")
	light.Color = color
	light.Brightness = brightness
	light.Range = range
	light.Shadows = false
	light.Parent = parent
	return light
end

-- Stamp every part of a client-animated rig with its rest offset from the
-- rig's rest pose ("RO" attribute), plus the rig's total part count. Drivers
-- write each part's ABSOLUTE CFrame as rest * motion * RO every frame, so a
-- part that streams in late — or was displaced by anything at all — is
-- re-seated on the very next frame. (PivotTo moves by delta and permanently
-- strands parts that replicate after animation starts.)
local function stampRig(rig, restCF)
	local inv = restCF:Inverse()
	local count = 0
	for _, piece in ipairs(rig:GetChildren()) do
		if piece:IsA("BasePart") then
			piece:SetAttribute("RO", inv * piece.CFrame)
			-- Base size too: pulse/flare effects scale Size, and a bind retry
			-- must never sample a mid-pulse size as the new base.
			piece:SetAttribute("BaseSize", piece.Size)
			count = count + 1
		end
	end
	rig:SetAttribute("PartCount", count)
end

local function weldTo(anchor, part)
	local weld = Instance.new("WeldConstraint")
	weld.Part0 = anchor
	weld.Part1 = part
	weld.Parent = anchor
end

-- Decorative sub-parts: visible, but invisible to physics, queries, and Touched.
local function decorate(part)
	part.CanCollide = false
	part.CanTouch = false
	part.CanQuery = false
	part.Massless = true
	return part
end

--------------------------------------------------------------------------------
-- Coin factory: rim body + two proud face discs. Only the body collides.
--------------------------------------------------------------------------------

local function makeCoin(cfg, cf, anchored, parent)
	local body = newPart({
		Name = "Coin",
		Anchored = anchored,
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(cfg.coinThickness, cfg.coinDiameter, cfg.coinDiameter),
		CFrame = cf,
		Color = cfg.coinRim,
		Material = Enum.Material.Metal,
		Reflectance = cfg.coinReflect,
		-- A LITTLE bounce (owner order). 0.22 against the 0-elasticity deck
		-- averages to ~0.11 on landing — visible hops, not popcorn (0.05 was
		-- dead; past ~0.4 the pile genuinely pops).
		CustomPhysicalProperties = PhysicalProperties.new(2, 0.4, 0.22, 1, 1),
	}, parent)
	-- ONE face disc per coin, floating 0.02 proud: rim ring / darker field —
	-- the minted read at 3 parts per coin. Part count is the whole ball game
	-- for physics performance: a 5-part coin at pile scale is ~200 simulated
	-- parts and it visibly costs framerate.
	local half = cfg.coinThickness / 2
	for _, side in ipairs({ -1, 1 }) do
		local face = decorate(newPart({
			Name = "CoinFace",
			Anchored = anchored,
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.06, cfg.coinDiameter * 0.80, cfg.coinDiameter * 0.80),
			CFrame = cf * CFrame.new(side * (half - 0.005), 0, 0),
			Color = cfg.coinFace,
			Material = Enum.Material.Metal,
			Reflectance = 0.04,
		}, body))
		weldTo(body, face)
	end
	return body
end

--------------------------------------------------------------------------------
-- Planet capsule factory: ball + two tilted Saturn rings. Only the ball collides.
--------------------------------------------------------------------------------

local RING_TILT = CFrame.Angles(math.rad(24), 0, math.rad(14))

local function makeCapsule(cfg, cf, anchored, parent)
	local ball = newPart({
		Name = "Capsule",
		Anchored = anchored,
		Shape = Enum.PartType.Ball,
		Size = Vector3.new(cfg.capsuleSize, cfg.capsuleSize, cfg.capsuleSize),
		CFrame = cf,
		Color = cfg.capPlanet,
		Material = Enum.Material.SmoothPlastic,
		Reflectance = 0.05,
		-- Same bounce pairing as the planets: one pile, one feel.
		CustomPhysicalProperties = PhysicalProperties.new(1.2, 0.5, 0.5, 1, 6),
	}, parent)
	-- Outer ring glows; inner ring is metal for contrast. Both are discs whose
	-- plane cuts the ball's equator, tilted like Saturn. Kept to 1.45x the
	-- ball so a capsule reads as a planet, not a hula hoop, at pile scale.
	-- No PointLight: Neon already reads, and lights are the expensive part.
	local ringA = decorate(newPart({
		Name = "CapsuleRing",
		Anchored = anchored,
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(0.09, cfg.capsuleSize * 1.45, cfg.capsuleSize * 1.45),
		CFrame = cf * RING_TILT * FLAT,
		Color = cfg.capRing,
		Material = Enum.Material.Neon,
	}, ball))
	local ringB = decorate(newPart({
		Name = "CapsuleRing2",
		Anchored = anchored,
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(0.15, cfg.capsuleSize * 1.2, cfg.capsuleSize * 1.2),
		CFrame = cf * RING_TILT * FLAT,
		Color = cfg.capRing2,
		Material = Enum.Material.Metal,
	}, ball))
	weldTo(ball, ringA)
	weldTo(ball, ringB)
	return ball
end

--------------------------------------------------------------------------------
-- Player planets: the DROPPED object. Small Saturn-ringed balls in a weighted
-- variation roster, like the ball-drop games — every drop rolls a look, rare
-- ones glow. The gold coin pile stays as the hoard these shove off the ledge.
--------------------------------------------------------------------------------

-- 1.15: a touch bigger than a coin, and big enough that the ring (1.3x) can
-- never reach the deck in any rest orientation worth noticing — the old
-- 0.95-ball/1.5x-ring combo let the ring sink visibly into the ground.
local PLANET_DIAMETER = 1.15

-- ============================== BALL CATALOG ==============================
-- Five rarity tiers; a ball's weight and sell value come from its tier
-- (a spec may override with w). Luck scales tier RARE and above, so buying
-- Luck genuinely shifts the table toward good pulls. Particle sprites are
-- HAND-DRAWN (canvas art pushed through the asset pipeline) — no stock
-- engine particle textures on catalog balls.
PusherMachine.Tiers = {
	{ name = "COMMON",     w = 24,   payout = 1,   color = Color3.fromRGB(176, 186, 202) },
	{ name = "RARE",       w = 6,    payout = 3,   color = Color3.fromRGB(86, 196, 255) },
	{ name = "ULTRA RARE", w = 1.5,  payout = 10,  color = Color3.fromRGB(188, 110, 255) },
	{ name = "LEGENDARY",  w = 0.25, payout = 40,  color = Color3.fromRGB(255, 196, 74) },
	{ name = "SECRET",     w = 0.05, payout = 250, color = Color3.fromRGB(255, 80, 120) },
}

-- Particle styles. NO uploaded textures — Roblox image moderation
-- false-flags white-on-alpha particle art (it nearly struck the user's
-- account), so every style composes ENGINE built-in textures with a
-- hand-tuned motion signature: what makes "snowflake" read as snow is that
-- it FALLS and tumbles; embers RISE and die fast; bubbles drift up on
-- drag; bolts burst. Motion carries the identity, not the bitmap.
-- Fields: tex texture, sz start size, spd speed range, life lifetime
-- range, ay Y-acceleration (rise/fall), rot spin speed, burst emit shape.
-- HAND-DRAWN sprites, the complete set (canvas art over a dark halo so
-- image moderation sees real content; the halo vanishes in-game under
-- LightEmission 1 additive rendering). Every style gets its own bespoke
-- bitmap PLUS its motion signature. star4 doubles as the UI sparkle.
PusherMachine.SpriteArt = {
	glowOrb = "rbxassetid://106851728262797",
	star4 = "rbxassetid://72209155868311",
	sparkDust = "rbxassetid://138584187674974",
	ringFlare = "rbxassetid://75351003766934",
	bokeh = "rbxassetid://125753043488758",
	smokeWisp = "rbxassetid://133601491595601",
	ember = "rbxassetid://121431720148840",
	snowflake = "rbxassetid://123928381314647",
	petal = "rbxassetid://74612898209292",
	bubble = "rbxassetid://116799523946515",
	bolt = "rbxassetid://84925914266510",
	crescent = "rbxassetid://100050471697544",
	shard = "rbxassetid://81373250314744",
	drip = "rbxassetid://122765420894916",
	note = "rbxassetid://133776696184298",
	-- Pending hand-drawn sprites. "rbxassetid://0" means NOT UPLOADED YET:
	-- spriteTex() below swaps in a motion-kin stand-in from the set above
	-- until the real id is pasted here. assets/sprites/README.md explains
	-- the upload + paste steps (draw, push through the asset pipeline,
	-- paste the id here — nothing else changes).
	heart = "rbxassetid://0",
	diamond = "rbxassetid://0",
	flame = "rbxassetid://0",
	spiral = "rbxassetid://0",
	leaf = "rbxassetid://0",
	coin = "rbxassetid://0",
}
local ART = PusherMachine.SpriteArt
-- Sprite fallback resolver: a key whose id is still the "rbxassetid://0"
-- placeholder renders with fallbackKey's art (heart->petal, diamond->star4,
-- flame->ember, spiral->smokeWisp, leaf->petal, coin->bokeh) — the motion
-- signature still carries the identity, so no style ever renders blank.
-- Existing styles keep direct ART refs.
local function spriteTex(key, fallbackKey)
	local id = ART[key]
	if id == nil or id == "rbxassetid://0" then
		return ART[fallbackKey]
	end
	return id
end
local SPRITE_STYLE = {
	glowOrb = { tex = ART.glowOrb, sz = 0.42, spd = { 0.2, 0.5 }, life = { 0.7, 1.3 }, ay = 0, rot = 15 },
	star4 = { tex = ART.star4, sz = 0.3, spd = { 0.25, 0.6 }, life = { 0.5, 1.0 }, ay = 0, rot = 120 },
	sparkDust = { tex = ART.sparkDust, sz = 0.28, spd = { 0.4, 0.9 }, life = { 0.4, 0.8 }, ay = 0, rot = 60 },
	ringFlare = { tex = ART.ringFlare, sz = 0.55, spd = { 0.1, 0.3 }, life = { 0.8, 1.4 }, ay = 0, rot = 8 },
	bokeh = { tex = ART.bokeh, sz = 0.4, spd = { 0.1, 0.35 }, life = { 0.9, 1.5 }, ay = 0.1, rot = 5 },
	smokeWisp = { tex = ART.smokeWisp, sz = 0.55, spd = { 0.2, 0.5 }, life = { 0.9, 1.6 }, ay = 0.55, rot = 25 },
	ember = { tex = ART.ember, sz = 0.3, spd = { 0.5, 1.1 }, life = { 0.35, 0.7 }, ay = 0.9, rot = 45 },
	snowflake = { tex = ART.snowflake, sz = 0.24, spd = { 0.15, 0.4 }, life = { 1.0, 1.7 }, ay = -0.7, rot = 70 },
	petal = { tex = ART.petal, sz = 0.26, spd = { 0.2, 0.5 }, life = { 0.9, 1.5 }, ay = -0.4, rot = 90 },
	bubble = { tex = ART.bubble, sz = 0.24, spd = { 0.15, 0.35 }, life = { 0.8, 1.4 }, ay = 0.5, rot = 10 },
	bolt = { tex = ART.bolt, sz = 0.34, spd = { 1.2, 2.2 }, life = { 0.2, 0.4 }, ay = 0, rot = 200 },
	crescent = { tex = ART.crescent, sz = 0.3, spd = { 0.15, 0.35 }, life = { 0.9, 1.5 }, ay = 0.15, rot = 20 },
	shard = { tex = ART.shard, sz = 0.26, spd = { 0.6, 1.2 }, life = { 0.35, 0.7 }, ay = -0.25, rot = 160 },
	drip = { tex = ART.drip, sz = 0.2, spd = { 0.2, 0.5 }, life = { 0.5, 0.9 }, ay = -0.9, rot = 0 },
	note = { tex = ART.note, sz = 0.28, spd = { 0.25, 0.55 }, life = { 0.8, 1.3 }, ay = 0.35, rot = 40 },
	-- Pending-art styles (see SpriteArt): tex resolves through spriteTex.
	-- heart: floaty rise, wobbling like a petal blown upward.
	heart = { tex = spriteTex("heart", "petal"), sz = 0.28, spd = { 0.15, 0.4 }, life = { 1.0, 1.6 }, ay = 0.35, rot = 40 },
	-- diamond: crisp slow spin like star4, but bigger and longer-lived.
	diamond = { tex = spriteTex("diamond", "star4"), sz = 0.4, spd = { 0.2, 0.5 }, life = { 0.6, 1.1 }, ay = 0, rot = 60 },
	-- flame: rises like an ember but lives twice as long (a tongue, not a spark).
	flame = { tex = spriteTex("flame", "ember"), sz = 0.34, spd = { 0.4, 0.9 }, life = { 0.7, 1.2 }, ay = 0.8, rot = 30 },
	-- spiral: slow rot 30, drifts on a lazy updraft.
	spiral = { tex = spriteTex("spiral", "smokeWisp"), sz = 0.5, spd = { 0.1, 0.3 }, life = { 1.0, 1.8 }, ay = 0.15, rot = 30 },
	-- leaf: falls tumbling like a snowflake, a touch faster.
	leaf = { tex = spriteTex("leaf", "petal"), sz = 0.26, spd = { 0.15, 0.4 }, life = { 1.0, 1.7 }, ay = -0.6, rot = 110 },
	-- coin: pops up fast, then falls hard.
	coin = { tex = spriteTex("coin", "bokeh"), sz = 0.26, spd = { 0.9, 1.6 }, life = { 0.5, 0.9 }, ay = -1.6, rot = 90 },
}

-- Compact spec format (300 entries must stay terse):
--   n=name  t=tier(1..5)  b={r,g,b} body  m=material  rf=reflectance
--   tp=transparency  ov={s="bands"|"cap"|"swirl"|"core", c={{rgb},...}}
--   rg={s="classic"|"double"|"halo"|"crossed"|"orbit"|"shards",
--       c={rgb}, c2={rgb}, m="Neon"|...}
--   fx={s=spriteName, c={rgb}, r=rate}  tr={rgb} trail
--   lt={c={rgb}, b=brightness}  sh=true shimmer glaze  w=weight override
--   Showpiece layers (LEGENDARY/SECRET; decor-rule parts, physics-neutral):
--   fx2={s=spriteName, c={rgb}, r=rate}  second emitter. fx keeps its 8/s
--       cap; fx2 gets what is left of a shared 10/s budget (the form +2
--       lands on fx only)
--   aura={c={rgb}, tp=0.6..0.85, sz=1.2..1.5}  one Neon Ball at the body's
--       center, size D*sz, Transparency tp, no shadow: a soft glowing shell
--   moons={n=1|2|3, c={rgb}, m=material, sz=0.16..0.26}  n static Ball
--       moonlets (D*sz, Neon unless m) evenly spaced at radius D*0.85 on
--       a plane tilted 20 deg off the equator
PusherMachine.Planets = {
	-- ======== COMMON (130) ========
	{ n = "Bubblegum", t = 1, b = { 255, 120, 170 }, m = "SmoothPlastic", rf = 0.1 },
	{ n = "Mint", t = 1, b = { 152, 230, 190 }, m = "SmoothPlastic" },
	{ n = "Cream Soda", t = 1, b = { 238, 196, 130 }, m = "Glass", rf = 0.15, tp = 0.25 },
	{ n = "Sky", t = 1, b = { 130, 190, 240 }, m = "SmoothPlastic" },
	{ n = "Butterscotch", t = 1, b = { 230, 160, 50 }, m = "Glass", rf = 0.2, tp = 0.2 },
	{ n = "Root Beer", t = 1, b = { 88, 48, 26 }, m = "Glass", tp = 0.25 },
	{ n = "Licorice", t = 1, b = { 30, 28, 30 }, m = "SmoothPlastic" },
	{ n = "Lemonade", t = 1, b = { 250, 226, 90 }, m = "Glass", tp = 0.3 },
	{ n = "Grape Soda", t = 1, b = { 136, 70, 190 }, m = "Glass", tp = 0.3 },
	{ n = "Pretzel", t = 1, b = { 164, 108, 58 }, m = "Pebble" },
	{ n = "Marshmallow", t = 1, b = { 250, 248, 244 }, m = "SmoothPlastic" },
	{ n = "Waffle", t = 1, b = { 214, 164, 92 }, m = "Granite" },
	{ n = "Cocoa", t = 1, b = { 122, 84, 58 }, m = "SmoothPlastic" },
	{ n = "Matcha", t = 1, b = { 150, 168, 112 }, m = "Sand" },
	{ n = "Taffy", t = 1, b = { 244, 170, 196 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 252, 244, 246 }, { 252, 244, 246 } } } },
	{ n = "Gumdrop", t = 1, b = { 222, 60, 54 }, m = "Sand" },
	{ n = "Peppermint", t = 1, b = { 250, 248, 246 }, m = "SmoothPlastic", rf = 0.15,
		ov = { s = "swirl", c = { { 214, 32, 38 } } } },
	{ n = "Honey", t = 1, b = { 240, 178, 52 }, m = "SmoothPlastic", rf = 0.3 },
	{ n = "Jawbreaker", t = 1, b = { 246, 244, 246 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 255, 150, 180 }, { 140, 190, 255 }, { 255, 224, 120 } } } },
	{ n = "Espresso", t = 1, b = { 46, 30, 22 }, m = "SmoothPlastic", rf = 0.25 },
	{ n = "Blueberry", t = 1, b = { 58, 74, 150 }, m = "Pebble" },
	{ n = "Peach", t = 1, b = { 248, 178, 138 }, m = "Sand" },
	{ n = "Mango", t = 1, b = { 255, 168, 36 }, m = "SmoothPlastic", rf = 0.15 },
	{ n = "Cherry", t = 1, b = { 158, 18, 34 }, m = "Glass", rf = 0.25, tp = 0.2 },
	{ n = "Boba", t = 1, b = { 214, 178, 140 }, m = "Glass", tp = 0.4,
		ov = { s = "core", c = { { 70, 46, 34 } } } },
	{ n = "Caramel", t = 1, b = { 172, 110, 50 }, m = "SmoothPlastic", rf = 0.2 },
	{ n = "Slushie", t = 1, b = { 92, 190, 244 }, m = "Ice", tp = 0.2 },
	{ n = "Cotton Candy", t = 1, b = { 170, 214, 248 }, m = "Sand" },
	{ n = "Nougat", t = 1, b = { 234, 222, 198 }, m = "Pebble" },
	{ n = "Custard", t = 1, b = { 250, 228, 150 }, m = "SmoothPlastic", rf = 0.2 },
	{ n = "Creamsicle", t = 1, b = { 255, 196, 140 }, m = "Ice", tp = 0.15 },
	{ n = "Drizzle", t = 1, b = { 136, 152, 168 }, m = "SmoothPlastic", rf = 0.15 },
	{ n = "Golden Hour", t = 1, b = { 232, 168, 74 }, m = "Foil" },
	{ n = "Overcast", t = 1, b = { 190, 192, 196 }, m = "SmoothPlastic" },
	{ n = "Fog", t = 1, b = { 225, 228, 230 }, m = "Glass", tp = 0.4 },
	{ n = "Frost", t = 1, b = { 214, 235, 245 }, m = "Ice", tp = 0.2 },
	{ n = "Hail", t = 1, b = { 240, 244, 248 }, m = "Glacier" },
	{ n = "Dusk", t = 1, b = { 94, 70, 122 }, m = "SmoothPlastic", rf = 0.1 },
	{ n = "Dawn", t = 1, b = { 244, 196, 190 }, m = "Glass", tp = 0.25 },
	{ n = "Thunderhead", t = 1, b = { 72, 68, 84 }, m = "Granite" },
	{ n = "Rainbow", t = 1, b = { 250, 250, 250 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 255, 90, 90 }, { 255, 210, 80 }, { 90, 150, 255 } } } },
	{ n = "High Noon", t = 1, b = { 255, 244, 180 }, m = "SmoothPlastic", rf = 0.35 },
	{ n = "Mirage", t = 1, b = { 226, 196, 140 }, m = "ForceField", tp = 0.25 },
	{ n = "Monsoon", t = 1, b = { 58, 84, 88 }, m = "Slate" },
	{ n = "Blue Moon", t = 1, b = { 158, 172, 196 }, m = "Pebble" },
	{ n = "Smog", t = 1, b = { 148, 138, 102 }, m = "SmoothPlastic" },
	{ n = "Heatwave", t = 1, b = { 255, 96, 64 }, m = "Neon" },
	{ n = "Puddle", t = 1, b = { 110, 124, 140 }, m = "Slate", rf = 0.3 },
	{ n = "Sundog", t = 1, b = { 244, 226, 170 }, m = "SmoothPlastic", rf = 0.15,
		rg = { s = "halo", c = { 220, 240, 255 }, m = "Ice" } },
	{ n = "Twister", t = 1, b = { 178, 152, 120 }, m = "Sand",
		ov = { s = "swirl", c = { { 120, 98, 74 } } } },
	{ n = "Tidepool", t = 1, b = { 26, 86, 82 }, m = "Glass", rf = 0.2, tp = 0.2 },
	{ n = "Seafoam", t = 1, b = { 214, 232, 222 }, m = "Sand" },
	{ n = "Kelp", t = 1, b = { 86, 102, 38 }, m = "SmoothPlastic", rf = 0.25 },
	{ n = "Coral", t = 1, b = { 240, 110, 110 }, m = "Pebble" },
	{ n = "Lagoon", t = 1, b = { 44, 190, 190 }, m = "Glass", tp = 0.35 },
	{ n = "Sea Glass", t = 1, b = { 150, 200, 185 }, m = "Glass", tp = 0.4 },
	{ n = "Riptide", t = 1, b = { 70, 120, 160 }, m = "Glacier" },
	{ n = "Sandbar", t = 1, b = { 226, 206, 168 }, m = "Sand" },
	{ n = "Barnacle", t = 1, b = { 214, 208, 196 }, m = "Granite" },
	{ n = "Buoy", t = 1, b = { 204, 42, 36 }, m = "SmoothPlastic", rf = 0.2,
		ov = { s = "cap", c = { { 245, 245, 240 } } } },
	{ n = "Urchin", t = 1, b = { 46, 28, 58 }, m = "Pebble" },
	{ n = "Shipwreck", t = 1, b = { 74, 66, 52 }, m = "Granite" },
	{ n = "Minnow", t = 1, b = { 168, 196, 180 }, m = "Foil" },
	{ n = "Starfish", t = 1, b = { 235, 150, 90 }, m = "Sand" },
	{ n = "Abyss", t = 1, b = { 6, 18, 26 }, m = "SmoothPlastic" },
	{ n = "Moss", t = 1, b = { 64, 92, 50 }, m = "Granite" },
	{ n = "Clover", t = 1, b = { 66, 176, 66 }, m = "SmoothPlastic" },
	{ n = "Maple", t = 1, b = { 204, 72, 32 }, m = "SmoothPlastic" },
	{ n = "Birch", t = 1, b = { 238, 236, 230 }, m = "Marble" },
	{ n = "Pumpkin", t = 1, b = { 226, 120, 36 }, m = "Pebble" },
	{ n = "Lavender", t = 1, b = { 178, 158, 214 }, m = "SmoothPlastic" },
	{ n = "Wheat", t = 1, b = { 216, 180, 110 }, m = "Sand" },
	{ n = "Mushroom", t = 1, b = { 198, 52, 44 }, m = "SmoothPlastic",
		ov = { s = "cap", c = { { 240, 230, 214 } } } },
	{ n = "Acorn", t = 1, b = { 188, 140, 88 }, m = "SmoothPlastic", rf = 0.1,
		ov = { s = "cap", c = { { 110, 74, 46 } } } },
	{ n = "Sequoia", t = 1, b = { 107, 58, 42 }, m = "Granite" },
	{ n = "Tumbleweed", t = 1, b = { 186, 170, 138 }, m = "Pebble" },
	{ n = "Cactus", t = 1, b = { 116, 150, 96 }, m = "Pebble" },
	{ n = "Riverbed", t = 1, b = { 124, 116, 108 }, m = "Slate", rf = 0.25 },
	{ n = "Bramble", t = 1, b = { 74, 44, 58 }, m = "Granite" },
	{ n = "Robin's Egg", t = 1, b = { 168, 216, 208 }, m = "Granite" },
	{ n = "Tin", t = 1, b = { 150, 152, 158 }, m = "Metal", rf = 0.1 },
	{ n = "Copper", t = 1, b = { 184, 110, 66 }, m = "Metal", rf = 0.25 },
	{ n = "Brass", t = 1, b = { 196, 158, 72 }, m = "Metal", rf = 0.25 },
	{ n = "Soapstone", t = 1, b = { 176, 184, 170 }, m = "Marble" },
	{ n = "Jade", t = 1, b = { 46, 128, 86 }, m = "Marble", rf = 0.2 },
	{ n = "Opal", t = 1, b = { 236, 232, 240 }, m = "Foil" },
	{ n = "Fool's Gold", t = 1, b = { 212, 175, 86 }, m = "Foil", rf = 0.3 },
	{ n = "Rust", t = 1, b = { 146, 74, 40 }, m = "Granite" },
	{ n = "Chrome", t = 1, b = { 200, 205, 212 }, m = "Metal", rf = 0.4 },
	{ n = "Turquoise", t = 1, b = { 48, 180, 178 }, m = "Granite" },
	{ n = "Obsidian", t = 1, b = { 18, 16, 20 }, m = "Glass", rf = 0.3, tp = 0.1 },
	{ n = "Geode", t = 1, b = { 180, 160, 210 }, m = "Glass", tp = 0.4,
		ov = { s = "core", c = { { 150, 60, 220 } } } },
	{ n = "Flint", t = 1, b = { 72, 74, 78 }, m = "Slate" },
	{ n = "Tiger's Eye", t = 1, b = { 166, 112, 48 }, m = "Marble",
		ov = { s = "bands", c = { { 92, 58, 26 }, { 92, 58, 26 } } } },
	{ n = "Rose Quartz", t = 1, b = { 232, 184, 192 }, m = "Marble" },
	{ n = "Boardwalk", t = 1, b = { 168, 148, 118 }, m = "Slate" },
	{ n = "Jukebox", t = 1, b = { 190, 44, 70 }, m = "Foil", rf = 0.3 },
	{ n = "Static", t = 1, b = { 158, 158, 160 }, m = "Granite" },
	{ n = "Mixtape", t = 1, b = { 96, 86, 78 }, m = "Glass", tp = 0.3 },
	{ n = "Marquee", t = 1, b = { 255, 238, 190 }, m = "Neon" },
	{ n = "Asphalt", t = 1, b = { 52, 52, 56 }, m = "Granite" },
	{ n = "Taxi", t = 1, b = { 244, 196, 32 }, m = "SmoothPlastic", rf = 0.1,
		ov = { s = "bands", c = { { 24, 24, 24 }, { 24, 24, 24 } } } },
	{ n = "Cue Ball", t = 1, b = { 248, 246, 240 }, m = "SmoothPlastic", rf = 0.3 },
	{ n = "Vinyl", t = 1, b = { 20, 20, 22 }, m = "SmoothPlastic", rf = 0.25 },
	{ n = "Brick", t = 1, b = { 178, 74, 52 }, m = "Granite" },
	{ n = "Lava Lamp", t = 1, b = { 58, 44, 120 }, m = "Glass", tp = 0.4,
		ov = { s = "core", c = { { 255, 120, 40 } } } },
	{ n = "Streetball", t = 1, b = { 196, 98, 42 }, m = "Pebble",
		ov = { s = "bands", c = { { 26, 26, 26 } } } },
	{ n = "Graffiti", t = 1, b = { 140, 140, 144 }, m = "Slate",
		ov = { s = "swirl", c = { { 255, 60, 180 }, { 60, 220, 255 } } } },
	{ n = "Motel", t = 1, b = { 255, 60, 160 }, m = "Neon" },
	{ n = "Traffic Cone", t = 1, b = { 255, 112, 26 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 245, 245, 245 }, { 245, 245, 245 } } } },
	{ n = "Red Planet", t = 1, b = { 198, 96, 58 }, m = "Sand" },
	{ n = "Meteor", t = 1, b = { 70, 52, 44 }, m = "CrackedLava" },
	{ n = "Eclipse", t = 1, b = { 16, 14, 18 }, m = "SmoothPlastic",
		rg = { s = "halo", c = { 255, 200, 90 }, m = "Neon" } },
	{ n = "Satellite", t = 1, b = { 176, 180, 188 }, m = "Metal", rf = 0.3,
		rg = { s = "orbit", c = { 130, 134, 142 } } },
	{ n = "Midnight", t = 1, b = { 22, 32, 74 }, m = "SmoothPlastic", rf = 0.2 },
	{ n = "Chalkboard", t = 1, b = { 44, 66, 58 }, m = "Slate" },
	{ n = "Blueprint", t = 1, b = { 30, 72, 168 }, m = "SmoothPlastic" },
	{ n = "Ink", t = 1, b = { 26, 28, 66 }, m = "Glass", rf = 0.2, tp = 0.15 },
	{ n = "Cork", t = 1, b = { 196, 156, 108 }, m = "Granite" },
	{ n = "Cardboard", t = 1, b = { 180, 142, 96 }, m = "SmoothPlastic" },
	{ n = "Porcelain", t = 1, b = { 250, 250, 248 }, m = "SmoothPlastic", rf = 0.25,
		ov = { s = "bands", c = { { 40, 80, 160 }, { 40, 80, 160 } } } },
	{ n = "Denim", t = 1, b = { 92, 118, 155 }, m = "Slate" },
	{ n = "Ghost", t = 1, b = { 230, 240, 250 }, m = "ForceField", tp = 0.3 },
	{ n = "Glowstick", t = 1, b = { 170, 255, 60 }, m = "Neon" },
	{ n = "Bubble", t = 1, b = { 235, 245, 255 }, m = "Glass", rf = 0.35, tp = 0.5 },
	{ n = "Magnet", t = 1, b = { 168, 172, 180 }, m = "Metal", rf = 0.2,
		ov = { s = "cap", c = { { 210, 44, 44 } } } },
	{ n = "Domino", t = 1, b = { 246, 244, 238 }, m = "SmoothPlastic", rf = 0.15,
		ov = { s = "cap", c = { { 24, 24, 26 } } } },
	{ n = "Pinata", t = 1, b = { 232, 58, 120 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 255, 150, 40 }, { 60, 200, 190 }, { 255, 220, 70 } } } },
	{ n = "Balloon", t = 1, b = { 222, 50, 58 }, m = "SmoothPlastic", rf = 0.25 },
	{ n = "Tennis", t = 1, b = { 200, 224, 70 }, m = "Sand" },
	-- ======== RARE (90) ========
	{ n = "Peppermint", t = 2, b = { 248, 248, 246 }, m = "SmoothPlastic", rf = 0.15,
		ov = { s = "swirl", c = { { 220, 40, 50 }, { 220, 40, 50 } } },
		rg = { s = "classic", c = { 220, 40, 50 }, m = "Foil" } },
	{ n = "Honey", t = 2, b = { 230, 160, 40 }, m = "Glass", rf = 0.1, tp = 0.25,
		ov = { s = "swirl", c = { { 255, 200, 80 } } },
		rg = { s = "halo", c = { 255, 210, 100 }, m = "Foil" } },
	{ n = "Kiwi", t = 2, b = { 140, 190, 60 }, m = "Sand",
		ov = { s = "swirl", c = { { 240, 235, 200 } } },
		rg = { s = "classic", c = { 120, 90, 55 }, m = "Foil" } },
	{ n = "Cola", t = 2, b = { 80, 40, 15 }, m = "Glass", tp = 0.2,
		ov = { s = "cap", c = { { 245, 242, 235 } } },
		rg = { s = "classic", c = { 210, 30, 40 }, m = "Foil" } },
	{ n = "Butterscotch", t = 2, b = { 210, 140, 50 }, m = "Marble", rf = 0.1,
		ov = { s = "swirl", c = { { 250, 200, 110 } } },
		rg = { s = "classic", c = { 235, 180, 70 }, m = "Foil" } },
	{ n = "Licorice", t = 2, b = { 25, 22, 25 }, m = "SmoothPlastic", rf = 0.2,
		ov = { s = "bands", c = { { 215, 35, 45 } } },
		rg = { s = "halo", c = { 215, 35, 45 }, m = "Neon" } },
	{ n = "Taffy", t = 2, b = { 245, 170, 190 }, m = "SmoothPlastic",
		ov = { s = "swirl", c = { { 255, 240, 245 } } },
		rg = { s = "double", c = { 250, 190, 205 }, c2 = { 255, 245, 235 }, m = "Foil" } },
	{ n = "Gumball", t = 2, b = { 225, 35, 55 }, m = "SmoothPlastic", rf = 0.3,
		rg = { s = "halo", c = { 255, 255, 255 }, m = "Foil" } },
	{ n = "Marmalade", t = 2, b = { 240, 140, 40 }, m = "Glass", tp = 0.25,
		ov = { s = "bands", c = { { 200, 90, 20 } } },
		rg = { s = "halo", c = { 255, 170, 60 }, m = "Glass" } },
	{ n = "Espresso", t = 2, b = { 60, 38, 25 }, m = "SmoothPlastic", rf = 0.1,
		ov = { s = "swirl", c = { { 210, 175, 130 } } },
		rg = { s = "classic", c = { 150, 100, 55 } } },
	{ n = "Matcha", t = 2, b = { 135, 160, 100 }, m = "Sand",
		ov = { s = "cap", c = { { 240, 245, 230 } } },
		rg = { s = "halo", c = { 190, 215, 160 }, m = "Foil" } },
	{ n = "Lemonade", t = 2, b = { 250, 235, 140 }, m = "Glass", tp = 0.3,
		ov = { s = "swirl", c = { { 255, 252, 235 } } },
		rg = { s = "halo", c = { 255, 255, 250 }, m = "Glass" } },
	{ n = "Sorbet", t = 2, b = { 250, 240, 225 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 230, 80, 120 }, { 245, 150, 60 }, { 250, 220, 100 } } },
		rg = { s = "halo", c = { 255, 255, 255 }, m = "Foil" } },
	{ n = "Jawbreaker", t = 2, b = { 248, 248, 248 }, m = "SmoothPlastic", rf = 0.15,
		ov = { s = "bands", c = { { 230, 60, 150 }, { 60, 190, 230 }, { 250, 215, 70 } } },
		rg = { s = "crossed", c = { 240, 110, 170 }, c2 = { 100, 190, 235 }, m = "Foil" } },
	{ n = "Blue Razz", t = 2, b = { 35, 110, 230 }, m = "SmoothPlastic", rf = 0.2,
		ov = { s = "swirl", c = { { 220, 240, 255 } } },
		rg = { s = "halo", c = { 90, 220, 255 }, m = "Neon" } },
	{ n = "Grape Soda", t = 2, b = { 130, 60, 180 }, m = "Glass", tp = 0.25,
		ov = { s = "cap", c = { { 245, 240, 250 } } },
		rg = { s = "halo", c = { 200, 160, 240 }, m = "Glass" } },
	{ n = "Rock Candy", t = 2, b = { 245, 190, 205 }, m = "Ice", tp = 0.25,
		rg = { s = "shards", c = { 255, 215, 225 }, m = "Ice" } },
	{ n = "Marshmallow", t = 2, b = { 250, 247, 240 }, m = "Pebble",
		ov = { s = "cap", c = { { 200, 140, 70 } } },
		rg = { s = "halo", c = { 255, 252, 245 }, m = "Foil" } },
	{ n = "Waffle Cone", t = 2, b = { 205, 160, 100 }, m = "Granite",
		ov = { s = "swirl", c = { { 100, 60, 35 } } },
		rg = { s = "classic", c = { 220, 180, 120 }, m = "Foil" } },
	{ n = "Boba", t = 2, b = { 220, 190, 155 }, m = "Glass", tp = 0.35,
		ov = { s = "core", c = { { 110, 60, 25 } } } },
	{ n = "Thunderhead", t = 2, b = { 90, 95, 105 }, m = "Granite",
		ov = { s = "swirl", c = { { 220, 225, 235 } } },
		rg = { s = "shards", c = { 170, 180, 195 } } },
	{ n = "Heatwave", t = 2, b = { 225, 110, 45 }, m = "Sand",
		rg = { s = "halo", c = { 255, 140, 50 }, m = "Neon" }, sh = true },
	{ n = "Overcast", t = 2, b = { 165, 170, 178 }, m = "Slate",
		ov = { s = "bands", c = { { 230, 232, 236 }, { 210, 214, 220 } } },
		rg = { s = "halo", c = { 220, 224, 230 }, m = "Foil" } },
	{ n = "Dusk", t = 2, b = { 45, 40, 90 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 240, 120, 50 } } },
		rg = { s = "classic", c = { 110, 70, 130 }, m = "Foil" } },
	{ n = "High Noon", t = 2, b = { 252, 245, 220 }, m = "SmoothPlastic", rf = 0.2,
		rg = { s = "halo", c = { 255, 200, 60 }, m = "Neon" } },
	{ n = "Sunshower", t = 2, b = { 125, 185, 240 }, m = "SmoothPlastic",
		ov = { s = "swirl", c = { { 255, 215, 110 } } },
		rg = { s = "double", c = { 255, 205, 90 }, c2 = { 255, 255, 250 }, m = "Foil" },
		fx = { s = "sparkDust", c = { 255, 220, 130 }, r = 2 } },
	{ n = "First Snow", t = 2, b = { 245, 250, 255 }, m = "Ice",
		ov = { s = "cap", c = { { 195, 220, 245 } } },
		rg = { s = "halo", c = { 210, 235, 255 }, m = "Ice" } },
	{ n = "Fogbank", t = 2, b = { 210, 214, 220 }, m = "Glass", tp = 0.4,
		ov = { s = "bands", c = { { 240, 242, 246 }, { 228, 231, 236 } } }, sh = true },
	{ n = "Monsoon", t = 2, b = { 60, 100, 110 }, m = "Slate",
		ov = { s = "swirl", c = { { 200, 215, 225 } } },
		rg = { s = "crossed", c = { 160, 175, 185 } } },
	{ n = "Afterglow", t = 2, b = { 235, 130, 110 }, m = "Marble",
		rg = { s = "halo", c = { 240, 170, 140 }, m = "Foil" },
		fx = { s = "bokeh", c = { 255, 180, 150 }, r = 2 } },
	{ n = "Blue Hour", t = 2, b = { 30, 50, 110 }, m = "Glass", tp = 0.15,
		rg = { s = "halo", c = { 150, 190, 255 }, m = "Glass" } },
	{ n = "Sundog", t = 2, b = { 240, 245, 250 }, m = "Ice",
		rg = { s = "crossed", c = { 225, 235, 250 }, c2 = { 250, 240, 220 }, m = "Ice" }, sh = true },
	{ n = "Sea Glass", t = 2, b = { 150, 210, 190 }, m = "Glass", tp = 0.4,
		rg = { s = "halo", c = { 240, 250, 246 }, m = "Glass" } },
	{ n = "Abalone", t = 2, b = { 80, 150, 160 }, m = "Foil", rf = 0.2,
		ov = { s = "swirl", c = { { 190, 160, 220 }, { 220, 235, 230 } } }, sh = true },
	{ n = "Lagoon", t = 2, b = { 45, 180, 190 }, m = "SmoothPlastic", rf = 0.1,
		ov = { s = "swirl", c = { { 240, 225, 190 } } },
		rg = { s = "classic", c = { 230, 215, 180 }, m = "Foil" } },
	{ n = "Kelp", t = 2, b = { 35, 70, 55 }, m = "Slate",
		ov = { s = "bands", c = { { 110, 130, 60 } } },
		rg = { s = "orbit", c = { 80, 110, 70 } } },
	{ n = "Coral", t = 2, b = { 245, 120, 100 }, m = "Pebble",
		ov = { s = "swirl", c = { { 255, 240, 235 } } },
		rg = { s = "halo", c = { 255, 250, 245 }, m = "Foil" } },
	{ n = "Riptide", t = 2, b = { 20, 45, 90 }, m = "SmoothPlastic",
		ov = { s = "swirl", c = { { 235, 245, 250 } } },
		rg = { s = "shards", c = { 240, 248, 252 }, m = "Foil" } },
	{ n = "Nautilus", t = 2, b = { 240, 230, 210 }, m = "Marble",
		ov = { s = "swirl", c = { { 160, 95, 55 } } },
		rg = { s = "classic", c = { 170, 110, 60 } } },
	{ n = "Buoy", t = 2, b = { 248, 246, 242 }, m = "SmoothPlastic", rf = 0.15,
		ov = { s = "bands", c = { { 210, 40, 45 } } },
		rg = { s = "halo", c = { 185, 190, 195 } } },
	{ n = "Angelfish", t = 2, b = { 250, 215, 60 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 25, 25, 28 }, { 25, 25, 28 } } },
		rg = { s = "halo", c = { 200, 205, 210 } } },
	{ n = "Deep End", t = 2, b = { 30, 130, 200 }, m = "SmoothPlastic", rf = 0.15,
		ov = { s = "bands", c = { { 245, 250, 252 } } },
		rg = { s = "halo", c = { 255, 255, 255 }, m = "Foil" } },
	{ n = "Umbra", t = 2, b = { 15, 15, 18 }, m = "SmoothPlastic",
		rg = { s = "halo", c = { 255, 235, 180 }, m = "Neon" }, sh = true },
	{ n = "Observatory", t = 2, b = { 25, 35, 70 }, m = "SmoothPlastic",
		ov = { s = "swirl", c = { { 230, 190, 90 } } },
		rg = { s = "classic", c = { 190, 150, 80 } } },
	{ n = "Satellite", t = 2, b = { 190, 195, 200 }, m = "Metal", rf = 0.25,
		rg = { s = "orbit", c = { 160, 168, 178 } } },
	{ n = "Asteroid", t = 2, b = { 55, 52, 50 }, m = "Pebble",
		rg = { s = "shards", c = { 120, 115, 110 } } },
	{ n = "Stardust", t = 2, b = { 45, 35, 90 }, m = "SmoothPlastic",
		rg = { s = "halo", c = { 190, 170, 240 }, m = "Foil" },
		fx = { s = "sparkDust", c = { 240, 240, 255 }, r = 2 } },
	{ n = "Crater", t = 2, b = { 185, 182, 178 }, m = "Pebble",
		ov = { s = "cap", c = { { 130, 127, 124 } } },
		rg = { s = "classic", c = { 150, 148, 145 } } },
	{ n = "Comet", t = 2, b = { 200, 225, 250 }, m = "Ice", tp = 0.2,
		rg = { s = "shards", c = { 240, 248, 255 }, m = "Ice" } },
	{ n = "Cat's Eye", t = 2, b = { 200, 150, 60 }, m = "Marble", rf = 0.15,
		ov = { s = "bands", c = { { 70, 45, 20 } } },
		rg = { s = "halo", c = { 240, 195, 90 }, m = "Foil" } },
	{ n = "Malachite", t = 2, b = { 30, 130, 85 }, m = "Marble",
		ov = { s = "swirl", c = { { 10, 70, 45 }, { 90, 190, 130 } } },
		rg = { s = "halo", c = { 230, 190, 100 }, m = "Foil" } },
	{ n = "Rose Gold", t = 2, b = { 230, 170, 150 }, m = "Foil", rf = 0.3,
		rg = { s = "halo", c = { 240, 185, 160 }, m = "Foil" } },
	{ n = "Gunmetal", t = 2, b = { 70, 75, 85 }, m = "Metal", rf = 0.3,
		rg = { s = "double", c = { 110, 118, 128 } } },
	{ n = "Copper", t = 2, b = { 185, 105, 60 }, m = "Metal", rf = 0.25,
		ov = { s = "swirl", c = { { 80, 160, 140 } } },
		rg = { s = "classic", c = { 195, 115, 70 } } },
	{ n = "Turquoise", t = 2, b = { 60, 190, 185 }, m = "Granite",
		ov = { s = "swirl", c = { { 110, 80, 55 } } },
		rg = { s = "classic", c = { 200, 205, 210 } } },
	{ n = "Obsidian", t = 2, b = { 20, 18, 24 }, m = "Glass", rf = 0.35,
		rg = { s = "shards", c = { 60, 55, 70 }, m = "Glass" } },
	{ n = "Jade", t = 2, b = { 130, 185, 140 }, m = "Marble", rf = 0.1,
		ov = { s = "bands", c = { { 240, 235, 215 } } },
		rg = { s = "halo", c = { 235, 200, 110 }, m = "Foil" } },
	{ n = "Hematite", t = 2, b = { 120, 125, 135 }, m = "Metal", rf = 0.4,
		rg = { s = "halo", c = { 210, 215, 225 } } },
	{ n = "Lapis", t = 2, b = { 35, 60, 150 }, m = "Granite",
		ov = { s = "swirl", c = { { 240, 200, 90 } } },
		rg = { s = "classic", c = { 245, 205, 95 }, m = "Foil" } },
	{ n = "Opal", t = 2, b = { 245, 242, 240 }, m = "Foil", rf = 0.2,
		ov = { s = "swirl", c = { { 170, 220, 230 }, { 240, 180, 210 } } },
		fx = { s = "bokeh", c = { 220, 200, 240 }, r = 1 }, sh = true },
	{ n = "Moonstone", t = 2, b = { 215, 225, 245 }, m = "Glass", tp = 0.25,
		rg = { s = "halo", c = { 190, 210, 250 }, m = "Glass" }, sh = true },
	{ n = "Fern", t = 2, b = { 35, 90, 50 }, m = "SmoothPlastic",
		ov = { s = "swirl", c = { { 120, 200, 110 }, { 80, 160, 80 } } } },
	{ n = "Birch", t = 2, b = { 242, 240, 232 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 50, 48, 45 }, { 50, 48, 45 } } },
		rg = { s = "classic", c = { 210, 212, 208 } } },
	{ n = "Maple", t = 2, b = { 210, 90, 40 }, m = "Marble",
		ov = { s = "swirl", c = { { 245, 180, 70 } } },
		rg = { s = "classic", c = { 230, 150, 60 }, m = "Foil" } },
	{ n = "Clover", t = 2, b = { 70, 160, 60 }, m = "Pebble",
		ov = { s = "cap", c = { { 248, 250, 245 } } },
		rg = { s = "halo", c = { 255, 255, 255 }, m = "Foil" } },
	{ n = "Ladybug", t = 2, b = { 215, 40, 35 }, m = "SmoothPlastic", rf = 0.2,
		ov = { s = "cap", c = { { 20, 20, 22 } } },
		rg = { s = "halo", c = { 30, 30, 32 } } },
	{ n = "Monarch", t = 2, b = { 245, 130, 30 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 25, 22, 20 }, { 25, 22, 20 } } },
		rg = { s = "crossed", c = { 35, 32, 30 } } },
	{ n = "Pinecone", t = 2, b = { 110, 70, 40 }, m = "Granite",
		ov = { s = "bands", c = { { 170, 125, 80 }, { 150, 105, 65 } } },
		rg = { s = "classic", c = { 160, 110, 60 } } },
	{ n = "Morel", t = 2, b = { 245, 238, 220 }, m = "SmoothPlastic",
		ov = { s = "cap", c = { { 210, 45, 40 } } },
		rg = { s = "halo", c = { 255, 252, 245 }, m = "Foil" } },
	{ n = "Cactus", t = 2, b = { 95, 140, 90 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 140, 180, 130 } } },
		rg = { s = "halo", c = { 240, 120, 170 }, m = "Foil" } },
	{ n = "Harvest", t = 2, b = { 220, 180, 90 }, m = "Sand",
		ov = { s = "bands", c = { { 190, 150, 75 }, { 235, 205, 130 } } },
		rg = { s = "classic", c = { 230, 200, 120 }, m = "Foil" } },
	{ n = "Skipping Stone", t = 2, b = { 130, 135, 140 }, m = "Slate",
		ov = { s = "bands", c = { { 240, 240, 238 } } },
		rg = { s = "halo", c = { 200, 205, 208 } } },
	{ n = "Pinball", t = 2, b = { 200, 205, 212 }, m = "Metal", rf = 0.35,
		ov = { s = "bands", c = { { 220, 40, 50 } } },
		rg = { s = "halo", c = { 255, 60, 70 }, m = "Neon" } },
	{ n = "Eight Ball", t = 2, b = { 18, 18, 20 }, m = "SmoothPlastic", rf = 0.25,
		ov = { s = "cap", c = { { 248, 248, 248 } } },
		rg = { s = "halo", c = { 255, 255, 255 }, m = "Foil" } },
	{ n = "Gutterball", t = 2, b = { 25, 30, 80 }, m = "Marble", rf = 0.15,
		ov = { s = "swirl", c = { { 130, 80, 190 } } },
		rg = { s = "classic", c = { 195, 200, 208 } } },
	{ n = "Cassette", t = 2, b = { 110, 110, 115 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 75, 50, 35 } } },
		rg = { s = "double", c = { 60, 60, 65 } } },
	{ n = "Lava Lamp", t = 2, b = { 120, 60, 170 }, m = "Glass", tp = 0.4,
		ov = { s = "core", c = { { 255, 120, 40 } } } },
	{ n = "Disco", t = 2, b = { 215, 218, 225 }, m = "Foil", rf = 0.4,
		rg = { s = "halo", c = { 255, 255, 255 }, m = "Foil" },
		fx = { s = "bokeh", c = { 255, 255, 255 }, r = 2 } },
	{ n = "Test Pattern", t = 2, b = { 235, 233, 228 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 240, 210, 60 }, { 70, 200, 215 }, { 220, 70, 160 } } },
		rg = { s = "halo", c = { 150, 152, 155 } } },
	{ n = "Motel", t = 2, b = { 20, 28, 55 }, m = "SmoothPlastic",
		rg = { s = "halo", c = { 255, 70, 160 }, m = "Neon" } },
	{ n = "Traffic Cone", t = 2, b = { 245, 110, 25 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 250, 250, 250 } } },
		rg = { s = "halo", c = { 205, 210, 215 } } },
	{ n = "Corduroy", t = 2, b = { 140, 85, 50 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 180, 125, 80 }, { 180, 125, 80 }, { 180, 125, 80 } } },
		rg = { s = "classic", c = { 195, 160, 85 } } },
	{ n = "Boombox", t = 2, b = { 45, 45, 50 }, m = "Slate",
		ov = { s = "bands", c = { { 180, 185, 190 } } },
		rg = { s = "double", c = { 200, 205, 212 } } },
	{ n = "Roller Rink", t = 2, b = { 200, 150, 90 }, m = "Marble",
		ov = { s = "swirl", c = { { 240, 90, 170 }, { 60, 200, 190 } } },
		rg = { s = "halo", c = { 170, 80, 220 }, m = "Neon" } },
	{ n = "Terracotta", t = 2, b = { 190, 95, 60 }, m = "Granite",
		ov = { s = "bands", c = { { 240, 228, 205 } } },
		rg = { s = "classic", c = { 205, 110, 70 }, m = "Foil" } },
	{ n = "Chalkboard", t = 2, b = { 40, 75, 60 }, m = "Slate",
		ov = { s = "swirl", c = { { 240, 240, 235 } } },
		rg = { s = "halo", c = { 235, 235, 230 }, m = "Foil" } },
	{ n = "Blueprint", t = 2, b = { 25, 70, 160 }, m = "SmoothPlastic",
		ov = { s = "swirl", c = { { 235, 242, 252 } } },
		rg = { s = "crossed", c = { 245, 248, 252 }, m = "Foil" } },
	{ n = "Porcelain", t = 2, b = { 248, 248, 246 }, m = "SmoothPlastic", rf = 0.2,
		ov = { s = "swirl", c = { { 30, 70, 160 } } },
		rg = { s = "classic", c = { 35, 75, 165 }, m = "Glass" } },
	{ n = "Stained Glass", t = 2, b = { 20, 110, 70 }, m = "Glass", tp = 0.3,
		ov = { s = "bands", c = { { 170, 30, 50 }, { 230, 185, 80 } } },
		rg = { s = "halo", c = { 235, 195, 95 }, m = "Foil" } },
	{ n = "Newsprint", t = 2, b = { 238, 234, 224 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 130, 130, 128 }, { 160, 160, 158 } } },
		rg = { s = "classic", c = { 145, 145, 143 } } },
	-- ======== ULTRA (42 + 1 noRoll special) ========
	{ n = "Root Beer", t = 3, b = { 96, 52, 22 }, m = "Glass", tp = 0.25,
		ov = { s = "cap", c = { { 250, 240, 215 } } },
		rg = { s = "classic", c = { 250, 240, 215 }, m = "Foil" },
		fx = { s = "bubble", c = { 255, 245, 220 }, r = 4 } },
	{ n = "Gobstopper", t = 3, b = { 250, 248, 245 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 255, 80, 120 }, { 255, 200, 60 }, { 80, 190, 255 } } },
		rg = { s = "orbit", c = { 255, 120, 160 }, m = "Foil" },
		fx = { s = "sparkDust", c = { 255, 190, 220 }, r = 3 } },
	{ n = "Snow Cone", t = 3, b = { 225, 240, 255 }, m = "Ice",
		ov = { s = "swirl", c = { { 255, 70, 90 }, { 60, 130, 255 } } },
		fx = { s = "snowflake", c = { 235, 245, 255 }, r = 3 }, sh = true },
	{ n = "Rock Candy", t = 3, b = { 255, 160, 200 }, m = "Granite", rf = 0.1,
		rg = { s = "halo", c = { 255, 235, 245 }, m = "Ice" },
		fx = { s = "shard", c = { 255, 190, 220 }, r = 3 } },
	{ n = "Lava Cake", t = 3, b = { 58, 32, 22 }, m = "CrackedLava", tp = 0.4,
		ov = { s = "core", c = { { 255, 120, 30 } } },
		fx = { s = "drip", c = { 255, 130, 40 }, r = 3 }, lt = { c = { 255, 120, 40 }, b = 1.2 } },
	{ n = "Pop Rocks", t = 3, b = { 255, 80, 140 }, m = "SmoothPlastic",
		rg = { s = "shards", c = { 255, 200, 230 }, m = "Foil" },
		fx = { s = "sparkDust", c = { 255, 240, 150 }, r = 8 } },
	{ n = "Dark Roast", t = 3, b = { 52, 34, 26 }, m = "Marble", rf = 0.15,
		rg = { s = "classic", c = { 214, 178, 132 }, m = "Foil" },
		fx = { s = "smokeWisp", c = { 240, 230, 215 }, r = 2 } },
	{ n = "Honeycomb", t = 3, b = { 255, 176, 48 }, m = "Glass", tp = 0.3,
		ov = { s = "core", c = { { 255, 214, 90 } } },
		fx = { s = "drip", c = { 255, 200, 70 }, r = 2 }, lt = { c = { 255, 190, 80 }, b = 1 } },
	{ n = "Thai Tea", t = 3, b = { 228, 205, 175 }, m = "Glass", tp = 0.3,
		rg = { s = "classic", c = { 245, 235, 220 }, m = "Glass" },
		fx = { s = "bubble", c = { 62, 42, 32 }, r = 4 } },
	{ n = "Nebula", t = 3, b = { 240, 228, 205 }, m = "Marble",
		ov = { s = "swirl", c = { { 190, 140, 90 }, { 255, 255, 255 } } },
		rg = { s = "classic", c = { 220, 205, 180 }, m = "Foil" },
		fx = { s = "star4", c = { 255, 255, 255 }, r = 3 } },
	{ n = "Squall", t = 3, b = { 88, 98, 112 }, m = "Slate",
		ov = { s = "swirl", c = { { 220, 225, 235 } } },
		fx = { s = "bolt", c = { 255, 230, 90 }, r = 3 }, lt = { c = { 255, 235, 140 }, b = 1 } },
	{ n = "Totality", t = 3, b = { 18, 16, 22 }, m = "SmoothPlastic",
		rg = { s = "halo", c = { 255, 240, 200 }, m = "Neon" },
		fx = { s = "crescent", c = { 255, 240, 200 }, r = 2 }, lt = { c = { 255, 235, 190 }, b = 1.5 } },
	{ n = "Dust Devil", t = 3, b = { 204, 168, 120 }, m = "Sand",
		rg = { s = "shards", c = { 190, 158, 112 } },
		fx = { s = "smokeWisp", c = { 214, 184, 140 }, r = 3 } },
	{ n = "Hailstorm", t = 3, b = { 70, 78, 92 }, m = "Slate",
		rg = { s = "crossed", c = { 210, 230, 245 }, m = "Ice" },
		fx = { s = "shard", c = { 225, 240, 250 }, r = 4 } },
	{ n = "Scorcher", t = 3, b = { 255, 96, 48 }, m = "SmoothPlastic",
		rg = { s = "halo", c = { 255, 170, 90 }, m = "Neon" },
		fx = { s = "ember", c = { 255, 150, 60 }, r = 3 }, lt = { c = { 255, 120, 60 }, b = 0.8 }, sh = true },
	{ n = "Midnight Sun", t = 3, b = { 225, 235, 240 }, m = "Ice", tp = 0.3,
		ov = { s = "core", c = { { 255, 205, 110 } } },
		rg = { s = "halo", c = { 255, 215, 140 }, m = "Neon" },
		fx = { s = "bokeh", c = { 255, 220, 160 }, r = 2 }, lt = { c = { 255, 205, 120 }, b = 1 } },
	{ n = "Downpour", t = 3, b = { 20, 110, 120 }, m = "Glass", tp = 0.3,
		ov = { s = "swirl", c = { { 220, 240, 245 } } },
		rg = { s = "double", c = { 200, 220, 230 }, m = "Glass" },
		fx = { s = "drip", c = { 170, 220, 235 }, r = 5 } },
	{ n = "Plankton", t = 3, b = { 12, 24, 44 }, m = "SmoothPlastic",
		ov = { s = "swirl", c = { { 60, 255, 220 } } },
		fx = { s = "bubble", c = { 80, 255, 230 }, r = 3 }, tr = { 60, 255, 220 }, lt = { c = { 60, 255, 210 }, b = 1 } },
	{ n = "Jellyfish", t = 3, b = { 225, 190, 255 }, m = "Glass", tp = 0.5,
		ov = { s = "core", c = { { 200, 120, 255 } } },
		fx = { s = "smokeWisp", c = { 220, 170, 255 }, r = 3 }, lt = { c = { 200, 130, 255 }, b = 0.8 }, sh = true },
	{ n = "Maelstrom", t = 3, b = { 58, 80, 100 }, m = "Slate",
		ov = { s = "swirl", c = { { 235, 240, 245 } } },
		rg = { s = "crossed", c = { 210, 225, 235 }, m = "Glass" },
		fx = { s = "smokeWisp", c = { 225, 235, 240 }, r = 3 } },
	{ n = "Shipwreck", t = 3, b = { 108, 66, 44 }, m = "Metal",
		rg = { s = "shards", c = { 255, 196, 80 } },
		fx = { s = "bubble", c = { 190, 220, 230 }, r = 2 } },
	{ n = "Red Giant", t = 3, b = { 255, 50, 30 }, m = "Neon",
		rg = { s = "halo", c = { 255, 150, 60 }, m = "Neon" },
		fx = { s = "ember", c = { 255, 120, 50 }, r = 4 }, lt = { c = { 255, 80, 40 }, b = 1.5 } },
	{ n = "Meteor", t = 3, b = { 92, 82, 78 }, m = "CrackedLava",
		rg = { s = "shards", c = { 120, 110, 105 } },
		fx = { s = "ember", c = { 255, 140, 50 }, r = 3 }, tr = { 255, 140, 60 } },
	{ n = "Pulsar", t = 3, b = { 20, 30, 70 }, m = "Glass", tp = 0.4,
		ov = { s = "core", c = { { 255, 255, 255 } } },
		rg = { s = "halo", c = { 255, 255, 255 }, m = "Neon" },
		fx = { s = "bolt", c = { 200, 220, 255 }, r = 3 }, lt = { c = { 220, 235, 255 }, b = 2 } },
	{ n = "Amethyst", t = 3, b = { 120, 116, 112 }, m = "Granite", tp = 0.4,
		ov = { s = "core", c = { { 190, 90, 255 } } },
		fx = { s = "shard", c = { 200, 130, 255 }, r = 3 }, lt = { c = { 180, 100, 255 }, b = 1 } },
	{ n = "Pearl", t = 3, b = { 244, 242, 240 }, m = "Marble",
		ov = { s = "swirl", c = { { 255, 170, 190 }, { 130, 220, 210 } } },
		fx = { s = "bokeh", c = { 255, 220, 230 }, r = 2 }, sh = true },
	{ n = "Fool's Gold", t = 3, b = { 212, 168, 70 }, m = "Metal", rf = 0.35,
		rg = { s = "double", c = { 230, 190, 90 } },
		fx = { s = "sparkDust", c = { 255, 230, 140 }, r = 3 } },
	{ n = "Stained Glass", t = 3, b = { 40, 60, 150 }, m = "Glass", tp = 0.35,
		ov = { s = "bands", c = { { 200, 40, 60 }, { 240, 180, 50 }, { 40, 140, 80 } } },
		rg = { s = "classic", c = { 70, 70, 75 } },
		fx = { s = "bokeh", c = { 255, 230, 180 }, r = 2 }, lt = { c = { 255, 220, 160 }, b = 0.8 } },
	{ n = "Quicksilver", t = 3, b = { 210, 214, 220 }, m = "Metal", rf = 0.4,
		rg = { s = "halo", c = { 235, 238, 242 } },
		fx = { s = "drip", c = { 225, 230, 235 }, r = 3 }, tr = { 220, 225, 232 } },
	{ n = "Uranium", t = 3, b = { 110, 255, 140 }, m = "Glass", tp = 0.4,
		ov = { s = "core", c = { { 80, 255, 90 } } },
		rg = { s = "halo", c = { 120, 255, 120 }, m = "Neon" },
		fx = { s = "glowOrb", c = { 140, 255, 140 }, r = 3 }, lt = { c = { 100, 255, 110 }, b = 1.2 } },
	{ n = "Glowworm", t = 3, b = { 24, 48, 36 }, m = "Glass", tp = 0.4,
		ov = { s = "cap", c = { { 150, 150, 155 } } },
		fx = { s = "glowOrb", c = { 200, 255, 120 }, r = 5 }, lt = { c = { 190, 255, 120 }, b = 1 } },
	{ n = "Rime", t = 3, b = { 228, 240, 248 }, m = "Glacier",
		rg = { s = "halo", c = { 240, 250, 255 }, m = "Ice" },
		fx = { s = "snowflake", c = { 245, 252, 255 }, r = 2 }, sh = true },
	{ n = "Greenhouse", t = 3, b = { 210, 235, 220 }, m = "Glass", tp = 0.5,
		ov = { s = "core", c = { { 90, 150, 70 } } },
		fx = { s = "petal", c = { 130, 190, 90 }, r = 2 } },
	{ n = "Hornet", t = 3, b = { 255, 190, 40 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 30, 28, 26 }, { 30, 28, 26 } } },
		rg = { s = "orbit", c = { 40, 36, 32 } },
		fx = { s = "sparkDust", c = { 255, 220, 90 }, r = 3 } },
	{ n = "Hi-Fi", t = 3, b = { 200, 205, 212 }, m = "Metal", rf = 0.3,
		ov = { s = "bands", c = { { 255, 60, 180 }, { 60, 220, 255 } } },
		fx = { s = "note", c = { 255, 120, 200 }, r = 3 }, lt = { c = { 255, 100, 200 }, b = 0.8 } },
	{ n = "Marquee", t = 3, b = { 230, 180, 90 }, m = "Foil",
		rg = { s = "double", c = { 255, 215, 130 } },
		fx = { s = "glowOrb", c = { 255, 230, 160 }, r = 5 }, lt = { c = { 255, 215, 140 }, b = 1.2 } },
	{ n = "Multiball", t = 3, b = { 215, 218, 224 }, m = "Metal", rf = 0.35,
		ov = { s = "bands", c = { { 220, 40, 50 } } },
		rg = { s = "orbit", c = { 200, 204, 210 } },
		fx = { s = "bolt", c = { 255, 240, 120 }, r = 3 }, tr = { 200, 210, 220 } },
	{ n = "Blacklight", t = 3, b = { 96, 20, 160 }, m = "Neon",
		ov = { s = "swirl", c = { { 170, 255, 60 } } },
		fx = { s = "bokeh", c = { 180, 80, 255 }, r = 3 }, lt = { c = { 150, 60, 255 }, b = 1 } },
	{ n = "Mixtape", t = 3, b = { 44, 44, 48 }, m = "SmoothPlastic",
		ov = { s = "bands", c = { { 255, 70, 160 }, { 70, 220, 255 } } },
		fx = { s = "note", c = { 255, 90, 170 }, r = 3 }, tr = { 255, 80, 170 } },
	{ n = "White Noise", t = 3, b = { 190, 192, 198 }, m = "ForceField",
		rg = { s = "halo", c = { 230, 232, 238 }, m = "Neon" },
		fx = { s = "bolt", c = { 255, 255, 255 }, r = 4 }, sh = true },
	{ n = "Oil Slick", t = 3, b = { 22, 22, 26 }, m = "Foil", rf = 0.3,
		ov = { s = "swirl", c = { { 0, 200, 180 }, { 255, 60, 200 } } },
		fx = { s = "bokeh", c = { 160, 220, 255 }, r = 2 }, sh = true },
	{ n = "Magnet", t = 3, b = { 150, 155, 165 }, m = "Metal", rf = 0.25,
		ov = { s = "cap", c = { { 220, 50, 60 }, { 50, 90, 220 } } },
		rg = { s = "crossed", c = { 180, 185, 195 } },
		fx = { s = "bolt", c = { 255, 220, 80 }, r = 2 } },
	-- Special grant: GOLD PLANET tree node (UpGoldPlanet). Never rolled.
	{ n = "Gilded Terra", t = 3, noRoll = true, b = { 255, 190, 60 }, m = "Metal", rf = 0.35,
		ov = { s = "swirl", c = { { 214, 140, 30 }, { 255, 226, 140 } } },
		rg = { s = "double", c = { 255, 215, 100 }, c2 = { 200, 140, 40 }, m = "Foil" },
		fx = { s = "ember", c = { 255, 170, 50 }, r = 4 }, tr = { 255, 190, 70 }, lt = { c = { 255, 190, 90 }, b = 1.2 } },
	-- ======== LEGENDARY (29 + 2 noRoll specials) ========
	-- Every LEGENDARY carries fx + tr + lt; about half add fx2/aura/moons.
	{ n = "THE MOON", t = 4, b = { 225, 228, 235 }, m = "Glacier",
		rg = { s = "halo", c = { 255, 255, 255 }, m = "Ice" },
		fx = { s = "sparkDust", c = { 255, 255, 255 }, r = 3 }, tr = { 220, 225, 240 }, lt = { c = { 235, 240, 255 }, b = 1.2 },
		aura = { c = { 230, 236, 255 }, tp = 0.82, sz = 1.3 } },
	{ n = "Foundry", t = 4, b = { 45, 35, 32 }, m = "CrackedLava",
		rg = { s = "shards", c = { 80, 70, 66 } },
		fx = { s = "ember", c = { 255, 140, 40 }, r = 5 }, tr = { 255, 120, 40 }, lt = { c = { 255, 110, 30 }, b = 1.5 },
		fx2 = { s = "smokeWisp", c = { 70, 60, 56 }, r = 2 } },
	{ n = "Pocketwatch", t = 4, b = { 190, 150, 80 }, m = "Metal", rf = 0.3,
		ov = { s = "bands", c = { { 110, 80, 40 } } },
		rg = { s = "orbit", c = { 205, 170, 90 } },
		fx = { s = "sparkDust", c = { 255, 220, 140 }, r = 2 }, tr = { 230, 190, 110 }, lt = { c = { 255, 210, 130 }, b = 0.8 } },
	{ n = "Prism", t = 4, b = { 245, 248, 255 }, m = "Glass", rf = 0.3, tp = 0.4,
		ov = { s = "swirl", c = { { 255, 120, 180 }, { 120, 200, 255 } } },
		rg = { s = "crossed", c = { 255, 255, 255 }, m = "Glass" },
		fx = { s = "star4", c = { 255, 255, 255 }, r = 3 }, tr = { 200, 220, 255 }, lt = { c = { 255, 240, 255 }, b = 1 }, sh = true,
		fx2 = { s = "diamond", c = { 255, 190, 240 }, r = 2 } },
	{ n = "Supercell", t = 4, b = { 70, 75, 85 }, m = "Slate",
		ov = { s = "swirl", c = { { 50, 55, 65 }, { 95, 100, 110 } } },
		rg = { s = "halo", c = { 255, 230, 80 }, m = "Neon" },
		fx = { s = "bolt", c = { 255, 235, 100 }, r = 4 }, tr = { 200, 205, 220 }, lt = { c = { 255, 235, 120 }, b = 1.3 },
		fx2 = { s = "drip", c = { 180, 200, 235 }, r = 3 } },
	{ n = "Golden Hour", t = 4, b = { 235, 170, 90 }, m = "Foil", rf = 0.25,
		ov = { s = "bands", c = { { 255, 200, 120 } } },
		rg = { s = "classic", c = { 255, 190, 110 }, m = "Foil" },
		fx = { s = "bokeh", c = { 255, 210, 140 }, r = 3 }, tr = { 255, 200, 130 }, lt = { c = { 255, 190, 120 }, b = 1.2 },
		aura = { c = { 255, 205, 130 }, tp = 0.8, sz = 1.3 } },
	{ n = "Jukebox", t = 4, b = { 200, 30, 50 }, m = "SmoothPlastic", rf = 0.2,
		ov = { s = "bands", c = { { 255, 220, 90 }, { 60, 220, 255 } } },
		rg = { s = "double", c = { 60, 220, 255 }, c2 = { 255, 90, 200 }, m = "Neon" },
		fx = { s = "note", c = { 255, 220, 90 }, r = 3 }, tr = { 60, 220, 255 }, lt = { c = { 255, 120, 180 }, b = 1.2 } },
	{ n = "Whiteout", t = 4, b = { 240, 248, 255 }, m = "Ice", tp = 0.2,
		rg = { s = "shards", c = { 220, 240, 255 }, m = "Ice" },
		fx = { s = "snowflake", c = { 255, 255, 255 }, r = 6 }, tr = { 235, 245, 255 }, lt = { c = { 210, 235, 255 }, b = 1 },
		aura = { c = { 255, 255, 255 }, tp = 0.78, sz = 1.32 } },
	{ n = "Blossom", t = 4, b = { 255, 200, 215 }, m = "Marble",
		ov = { s = "cap", c = { { 255, 240, 245 } } },
		rg = { s = "classic", c = { 235, 195, 130 }, m = "Foil" },
		fx = { s = "petal", c = { 255, 180, 200 }, r = 4 }, tr = { 255, 215, 225 }, lt = { c = { 255, 200, 215 }, b = 0.8 },
		fx2 = { s = "leaf", c = { 150, 210, 120 }, r = 2 } },
	{ n = "Breaker", t = 4, b = { 20, 140, 160 }, m = "Glass", tp = 0.3,
		ov = { s = "swirl", c = { { 240, 255, 255 } } },
		rg = { s = "classic", c = { 150, 230, 235 }, m = "Glass" },
		fx = { s = "drip", c = { 225, 250, 252 }, r = 4 }, tr = { 120, 230, 230 }, lt = { c = { 80, 220, 220 }, b = 1 } },
	{ n = "Comet", t = 4, b = { 200, 225, 255 }, m = "Ice", rf = 0.2, tp = 0.25,
		rg = { s = "orbit", c = { 180, 210, 255 }, m = "Ice" },
		fx = { s = "shard", c = { 220, 240, 255 }, r = 4 }, tr = { 180, 220, 255 }, lt = { c = { 190, 220, 255 }, b = 1.1 },
		fx2 = { s = "sparkDust", c = { 200, 225, 255 }, r = 3 } },
	{ n = "Milky Way", t = 4, b = { 240, 230, 215 }, m = "Marble",
		ov = { s = "swirl", c = { { 190, 140, 90 }, { 255, 250, 240 } } },
		rg = { s = "classic", c = { 220, 200, 170 }, m = "Foil" },
		fx = { s = "sparkDust", c = { 255, 250, 230 }, r = 3 }, tr = { 240, 225, 200 }, lt = { c = { 255, 245, 225 }, b = 0.9 },
		moons = { n = 2, c = { 255, 250, 235 }, m = "Neon", sz = 0.16 } },
	{ n = "Geode", t = 4, b = { 110, 100, 110 }, m = "Granite", tp = 0.4,
		ov = { s = "core", c = { { 190, 120, 255 } } },
		rg = { s = "shards", c = { 190, 130, 255 }, m = "Glass" },
		fx = { s = "shard", c = { 200, 140, 255 }, r = 3 }, tr = { 180, 120, 250 }, lt = { c = { 190, 130, 255 }, b = 1.2 } },
	{ n = "Mercury", t = 4, b = { 200, 205, 215 }, m = "Metal", rf = 0.4,
		rg = { s = "halo", c = { 230, 235, 240 } },
		fx = { s = "drip", c = { 210, 215, 225 }, r = 3 }, tr = { 200, 210, 220 }, lt = { c = { 220, 225, 235 }, b = 0.8 }, sh = true },
	{ n = "Terrarium", t = 4, b = { 80, 130, 70 }, m = "Pebble",
		ov = { s = "cap", c = { { 115, 165, 95 } } },
		rg = { s = "classic", c = { 200, 230, 200 }, m = "Glass" },
		fx = { s = "glowOrb", c = { 220, 255, 140 }, r = 3 }, tr = { 150, 220, 120 }, lt = { c = { 200, 255, 150 }, b = 0.8 } },
	{ n = "Pinball", t = 4, b = { 230, 232, 238 }, m = "SmoothPlastic", rf = 0.35,
		ov = { s = "bands", c = { { 255, 60, 60 }, { 60, 120, 255 } } },
		rg = { s = "double", c = { 255, 80, 80 }, c2 = { 80, 140, 255 }, m = "Neon" },
		fx = { s = "star4", c = { 255, 240, 150 }, r = 4 }, tr = { 255, 220, 120 }, lt = { c = { 255, 120, 120 }, b = 1.2 },
		moons = { n = 2, c = { 230, 232, 238 }, m = "Metal", sz = 0.18 } },
	{ n = "Creme Brulee", t = 4, b = { 235, 215, 180 }, m = "CrackedLava",
		rg = { s = "classic", c = { 200, 150, 80 }, m = "Foil" },
		fx = { s = "ember", c = { 255, 180, 90 }, r = 2 }, tr = { 245, 210, 160 }, lt = { c = { 255, 190, 110 }, b = 0.9 } },
	{ n = "Champagne", t = 4, b = { 245, 225, 170 }, m = "Glass", tp = 0.35,
		rg = { s = "halo", c = { 255, 235, 180 }, m = "Foil" },
		fx = { s = "bubble", c = { 255, 245, 200 }, r = 6 }, tr = { 250, 235, 190 }, lt = { c = { 255, 235, 180 }, b = 1 }, sh = true },
	{ n = "Midnight", t = 4, b = { 20, 26, 58 }, m = "SmoothPlastic",
		rg = { s = "halo", c = { 200, 210, 235 } },
		fx = { s = "star4", c = { 240, 245, 255 }, r = 3 }, tr = { 90, 110, 200 }, lt = { c = { 150, 170, 255 }, b = 0.7 },
		moons = { n = 1, c = { 245, 242, 220 }, m = "Neon", sz = 0.2 } },
	{ n = "Static", t = 4, b = { 180, 182, 188 }, m = "Granite",
		ov = { s = "bands", c = { { 30, 30, 34 }, { 240, 240, 244 } } },
		rg = { s = "halo", c = { 255, 255, 255 }, m = "Neon" },
		fx = { s = "shard", c = { 255, 255, 255 }, r = 5 }, tr = { 220, 220, 230 }, lt = { c = { 240, 240, 255 }, b = 1.1 } },
	{ n = "Wisp", t = 4, b = { 180, 255, 210 }, m = "ForceField", tp = 0.3,
		ov = { s = "core", c = { { 140, 255, 180 } } },
		rg = { s = "halo", c = { 160, 255, 190 }, m = "Neon" },
		fx = { s = "smokeWisp", c = { 180, 255, 210 }, r = 3 }, tr = { 170, 255, 200 }, lt = { c = { 150, 255, 190 }, b = 1 },
		aura = { c = { 160, 255, 200 }, tp = 0.8, sz = 1.3 } },
	{ n = "Lodestone", t = 4, b = { 60, 62, 70 }, m = "Metal", rf = 0.15,
		rg = { s = "crossed", c = { 95, 100, 110 } },
		fx = { s = "bolt", c = { 120, 180, 255 }, r = 2 }, tr = { 100, 150, 255 }, lt = { c = { 120, 170, 255 }, b = 0.9 } },
	{ n = "Disco", t = 4, b = { 225, 228, 235 }, m = "Foil", rf = 0.4,
		rg = { s = "double", c = { 255, 90, 200 }, c2 = { 90, 200, 255 }, m = "Neon" },
		fx = { s = "bokeh", c = { 255, 180, 240 }, r = 5 }, tr = { 200, 160, 255 }, lt = { c = { 255, 140, 220 }, b = 1.4 },
		fx2 = { s = "star4", c = { 255, 255, 255 }, r = 3 } },
	-- Showpieces: every layer the interpreter knows, on primitives alone.
	{ n = "Dragon Egg", t = 4, b = { 120, 20, 24 }, m = "CrackedLava",
		ov = { s = "cap", c = { { 255, 200, 80 } } },
		fx = { s = "ember", c = { 255, 120, 30 }, r = 4 }, fx2 = { s = "flame", c = { 255, 180, 50 }, r = 3 },
		aura = { c = { 255, 90, 30 }, tp = 0.8, sz = 1.3 },
		tr = { 255, 110, 40 }, lt = { c = { 255, 120, 40 }, b = 1.3 } },
	{ n = "Candy Nebula", t = 4, b = { 255, 150, 210 }, m = "Glass", rf = 0.2, tp = 0.3,
		ov = { s = "swirl", c = { { 120, 190, 255 }, { 255, 230, 250 } } },
		rg = { s = "halo", c = { 180, 200, 255 }, m = "Neon" },
		fx = { s = "heart", c = { 255, 130, 190 }, r = 4 }, fx2 = { s = "bokeh", c = { 150, 200, 255 }, r = 3 },
		tr = { 255, 170, 220 }, lt = { c = { 255, 170, 220 }, b = 1.2 }, sh = true },
	{ n = "Rainbow Road", t = 4, b = { 240, 240, 245 }, m = "Foil", rf = 0.35,
		ov = { s = "bands", c = { { 255, 70, 70 }, { 80, 220, 100 }, { 90, 140, 255 } } },
		rg = { s = "double", c = { 255, 90, 90 }, c2 = { 90, 150, 255 }, m = "Neon" },
		fx = { s = "star4", c = { 255, 255, 255 }, r = 4 }, fx2 = { s = "diamond", c = { 255, 220, 120 }, r = 3 },
		tr = { 255, 200, 90 }, lt = { c = { 255, 240, 200 }, b = 1.3 } },
	-- Tier echo of the ULTRA RARE Lava Cake (findSpec disambiguates by tier).
	{ n = "Lava Cake", t = 4, b = { 88, 52, 34 }, m = "Marble",
		ov = { s = "core", c = { { 255, 130, 30 } } },
		rg = { s = "classic", c = { 210, 150, 70 }, m = "Foil" },
		fx = { s = "drip", c = { 255, 120, 40 }, r = 4 }, fx2 = { s = "ember", c = { 255, 160, 60 }, r = 3 },
		tr = { 255, 130, 50 }, lt = { c = { 255, 130, 50 }, b = 1.3 } },
	{ n = "Bee Hive", t = 4, b = { 250, 200, 50 }, m = "Sand",
		ov = { s = "bands", c = { { 36, 30, 26 }, { 36, 30, 26 } } },
		rg = { s = "classic", c = { 200, 140, 40 }, m = "Sand" },
		fx = { s = "glowOrb", c = { 255, 220, 90 }, r = 3 }, fx2 = { s = "coin", c = { 255, 190, 50 }, r = 3 },
		moons = { n = 2, c = { 255, 210, 60 }, m = "Neon", sz = 0.16 },
		tr = { 255, 200, 60 }, lt = { c = { 255, 200, 80 }, b = 1.2 } },
	{ n = "Thunder Egg", t = 4, b = { 70, 72, 84 }, m = "Slate",
		ov = { s = "core", c = { { 120, 200, 255 } } },
		rg = { s = "crossed", c = { 120, 200, 255 }, c2 = { 255, 240, 140 }, m = "Neon" },
		fx = { s = "bolt", c = { 140, 210, 255 }, r = 4 }, fx2 = { s = "shard", c = { 200, 230, 255 }, r = 3 },
		tr = { 120, 200, 255 }, lt = { c = { 140, 210, 255 }, b = 1.3 } },
	-- Special grant: DIAMOND PLANET tree node (UpDiamondPlanet). Never rolled.
	{ n = "Diamond Nova", t = 4, noRoll = true, b = { 235, 245, 255 }, m = "Glass", rf = 0.45, tp = 0.35,
		rg = { s = "crossed", c = { 150, 210, 255 }, c2 = { 210, 240, 255 }, m = "Glass" },
		fx = { s = "shard", c = { 220, 245, 255 }, r = 4 }, tr = { 190, 225, 255 }, lt = { c = { 200, 235, 255 }, b = 1.3 }, sh = true,
		fx2 = { s = "diamond", c = { 255, 255, 255 }, r = 3 }, aura = { c = { 200, 235, 255 }, tp = 0.8, sz = 1.35 } },
	-- Special grant: 7-star daily constellation prize. Never rolled.
	{ n = "Constellation Prime", t = 4, noRoll = true, b = { 16, 24, 64 }, m = "SmoothPlastic",
		ov = { s = "core", c = { { 90, 110, 220 } } },
		rg = { s = "orbit", c = { 255, 210, 110 }, c2 = { 255, 230, 160 }, m = "Foil" },
		fx = { s = "star4", c = { 255, 240, 190 }, r = 5 }, tr = { 255, 210, 110 }, lt = { c = { 190, 205, 255 }, b = 1.2 },
		moons = { n = 3, c = { 255, 230, 160 }, m = "Neon", sz = 0.16 } },
	-- ======== SECRET (14) ========
	-- Every SECRET: aura + fx + fx2 + tr + lt (b >= 1.4); most carry moons.
	{ n = "THE SUN", t = 5, b = { 255, 138, 20 }, m = "Neon",
		ov = { s = "swirl", c = { { 200, 70, 0 }, { 255, 200, 60 } } },
		rg = { s = "halo", c = { 255, 235, 170 }, m = "Neon" },
		fx = { s = "ember", c = { 255, 110, 0 }, r = 6 }, fx2 = { s = "flame", c = { 255, 200, 60 }, r = 3 },
		aura = { c = { 255, 150, 30 }, tp = 0.7, sz = 1.45 },
		moons = { n = 2, c = { 90, 140, 255 }, m = "SmoothPlastic", sz = 0.16 },
		tr = { 255, 120, 0 }, lt = { c = { 255, 150, 40 }, b = 2 } },
	{ n = "ECLIPSE", t = 5, b = { 14, 14, 18 }, m = "Metal", rf = 0.1,
		rg = { s = "halo", c = { 255, 240, 200 }, m = "Neon" },
		fx = { s = "ringFlare", c = { 255, 232, 180 }, r = 2 }, fx2 = { s = "sparkDust", c = { 255, 240, 200 }, r = 3 },
		aura = { c = { 255, 220, 150 }, tp = 0.82, sz = 1.22 },
		moons = { n = 1, c = { 30, 30, 36 }, m = "Slate", sz = 0.2 },
		tr = { 255, 225, 160 }, lt = { c = { 255, 230, 180 }, b = 1.4 }, sh = true },
	{ n = "THE DEEP", t = 5, b = { 8, 28, 58 }, m = "Glass", tp = 0.45,
		ov = { s = "core", c = { { 0, 255, 200 } } },
		fx = { s = "bubble", c = { 120, 215, 255 }, r = 4 }, fx2 = { s = "glowOrb", c = { 0, 255, 200 }, r = 2 },
		aura = { c = { 0, 200, 170 }, tp = 0.8, sz = 1.3 },
		moons = { n = 3, c = { 236, 240, 248 }, m = "SmoothPlastic", sz = 0.18 },
		tr = { 0, 150, 140 }, lt = { c = { 0, 210, 175 }, b = 1.4 } },
	{ n = "VESUVIUS", t = 5, b = { 58, 24, 18 }, m = "CrackedLava",
		rg = { s = "shards", c = { 42, 36, 34 }, c2 = { 255, 90, 0 } },
		fx = { s = "ember", c = { 255, 90, 0 }, r = 7 }, fx2 = { s = "smokeWisp", c = { 70, 56, 52 }, r = 3 },
		aura = { c = { 255, 80, 0 }, tp = 0.8, sz = 1.3 },
		moons = { n = 2, c = { 70, 40, 34 }, m = "CrackedLava", sz = 0.18 },
		tr = { 255, 70, 0 }, lt = { c = { 255, 85, 0 }, b = 1.6 } },
	{ n = "EVEREST", t = 5, b = { 198, 224, 240 }, m = "Glacier", rf = 0.2,
		ov = { s = "cap", c = { { 255, 255, 255 } } },
		rg = { s = "halo", c = { 180, 220, 255 }, m = "Ice" },
		fx = { s = "snowflake", c = { 255, 255, 255 }, r = 3 }, fx2 = { s = "star4", c = { 255, 255, 255 }, r = 2 },
		aura = { c = { 255, 255, 255 }, tp = 0.8, sz = 1.3 },
		tr = { 225, 240, 255 }, lt = { c = { 185, 212, 255 }, b = 1.4 }, sh = true },
	{ n = "THE STORM", t = 5, b = { 45, 50, 62 }, m = "Slate",
		ov = { s = "swirl", c = { { 92, 100, 115 }, { 66, 74, 90 } } },
		rg = { s = "double", c = { 70, 76, 90 } },
		fx = { s = "bolt", c = { 255, 240, 120 }, r = 3 }, fx2 = { s = "drip", c = { 170, 200, 240 }, r = 4 },
		aura = { c = { 120, 130, 160 }, tp = 0.8, sz = 1.35 },
		moons = { n = 2, c = { 60, 66, 80 }, m = "Slate", sz = 0.18 },
		tr = { 150, 160, 185 }, lt = { c = { 255, 240, 150 }, b = 1.4 } },
	{ n = "JACKPOT", t = 5, b = { 255, 200, 40 }, m = "Foil", rf = 0.4,
		ov = { s = "bands", c = { { 220, 40, 50 } } },
		rg = { s = "double", c = { 255, 215, 80 }, c2 = { 255, 160, 30 }, m = "Foil" },
		fx = { s = "star4", c = { 255, 255, 185 }, r = 5 }, fx2 = { s = "coin", c = { 255, 215, 90 }, r = 4 },
		aura = { c = { 255, 210, 80 }, tp = 0.7, sz = 1.35 },
		moons = { n = 3, c = { 255, 205, 60 }, m = "Foil", sz = 0.2 },
		tr = { 255, 200, 60 }, lt = { c = { 255, 210, 80 }, b = 1.4 }, sh = true },
	{ n = "GHOST", t = 5, b = { 220, 230, 255 }, m = "ForceField",
		ov = { s = "core", c = { { 160, 200, 255 } } },
		rg = { s = "halo", c = { 200, 220, 255 }, m = "Glass" },
		fx = { s = "smokeWisp", c = { 205, 218, 255 }, r = 3 }, fx2 = { s = "bokeh", c = { 220, 230, 255 }, r = 3 },
		aura = { c = { 200, 215, 255 }, tp = 0.8, sz = 1.4 },
		moons = { n = 2, c = { 200, 220, 255 }, m = "Neon", sz = 0.16 },
		tr = { 190, 210, 255 }, lt = { c = { 172, 202, 255 }, b = 1.4 }, sh = true },
	-- Showpieces.
	{ n = "BLACK HOLE", t = 5, b = { 10, 8, 14 }, m = "Metal", rf = 0.05,
		rg = { s = "orbit", c = { 150, 80, 255 }, c2 = { 255, 190, 255 }, m = "Neon" },
		fx = { s = "spiral", c = { 170, 90, 255 }, r = 4 }, fx2 = { s = "bokeh", c = { 200, 150, 255 }, r = 3 },
		aura = { c = { 140, 60, 255 }, tp = 0.7, sz = 1.5 },
		moons = { n = 3, c = { 24, 18, 34 }, m = "Slate", sz = 0.18 },
		tr = { 140, 60, 255 }, lt = { c = { 150, 70, 255 }, b = 1.5 } },
	{ n = "GALAXY CORE", t = 5, b = { 18, 16, 48 }, m = "Glass", tp = 0.35,
		ov = { s = "core", c = { { 120, 80, 255 } } },
		rg = { s = "orbit", c = { 225, 200, 255 }, c2 = { 255, 240, 200 }, m = "Foil" },
		fx = { s = "star4", c = { 255, 250, 230 }, r = 5 }, fx2 = { s = "spiral", c = { 170, 140, 255 }, r = 3 },
		aura = { c = { 240, 235, 255 }, tp = 0.8, sz = 1.4 },
		moons = { n = 2, c = { 255, 245, 220 }, m = "Neon", sz = 0.16 },
		tr = { 170, 140, 255 }, lt = { c = { 190, 160, 255 }, b = 1.5 } },
	{ n = "PHOENIX", t = 5, b = { 255, 110, 20 }, m = "Neon",
		ov = { s = "swirl", c = { { 255, 210, 60 }, { 220, 30, 20 } } },
		rg = { s = "halo", c = { 255, 210, 90 }, m = "Neon" },
		fx = { s = "flame", c = { 255, 170, 40 }, r = 5 }, fx2 = { s = "ember", c = { 255, 90, 10 }, r = 4 },
		aura = { c = { 255, 50, 20 }, tp = 0.7, sz = 1.4 },
		tr = { 255, 90, 10 }, lt = { c = { 255, 120, 30 }, b = 1.8 } },
	{ n = "DIAMOND HEART", t = 5, b = { 245, 240, 250 }, m = "Glass", rf = 0.5, tp = 0.3,
		ov = { s = "core", c = { { 255, 120, 180 } } },
		rg = { s = "crossed", c = { 255, 255, 255 }, c2 = { 255, 190, 220 }, m = "Glass" },
		fx = { s = "diamond", c = { 255, 255, 255 }, r = 4 }, fx2 = { s = "heart", c = { 255, 150, 200 }, r = 3 },
		aura = { c = { 255, 200, 225 }, tp = 0.8, sz = 1.3 },
		tr = { 255, 190, 225 }, lt = { c = { 255, 210, 235 }, b = 1.4 }, sh = true },
	{ n = "FROST KING", t = 5, b = { 170, 215, 245 }, m = "Ice", rf = 0.2, tp = 0.15,
		ov = { s = "cap", c = { { 255, 255, 255 } } },
		rg = { s = "double", c = { 235, 248, 255 }, c2 = { 160, 210, 255 }, m = "Ice" },
		fx = { s = "snowflake", c = { 255, 255, 255 }, r = 4 }, fx2 = { s = "shard", c = { 200, 235, 255 }, r = 3 },
		aura = { c = { 180, 225, 255 }, tp = 0.75, sz = 1.35 },
		moons = { n = 3, c = { 225, 245, 255 }, m = "Ice", sz = 0.2 },
		tr = { 200, 235, 255 }, lt = { c = { 190, 230, 255 }, b = 1.5 } },
	{ n = "GOLDEN GOD", t = 5, b = { 255, 205, 50 }, m = "Foil", rf = 0.5,
		ov = { s = "swirl", c = { { 255, 240, 170 }, { 220, 160, 30 } } },
		rg = { s = "double", c = { 255, 225, 120 }, c2 = { 230, 170, 40 }, m = "Foil" },
		fx = { s = "coin", c = { 255, 225, 110 }, r = 5 }, fx2 = { s = "star4", c = { 255, 255, 200 }, r = 4 },
		aura = { c = { 255, 200, 60 }, tp = 0.7, sz = 1.4 },
		moons = { n = 3, c = { 255, 215, 90 }, m = "Foil", sz = 0.22 },
		tr = { 255, 210, 70 }, lt = { c = { 255, 215, 90 }, b = 1.8 }, sh = true },
}
local function tierOf(spec)
	return PusherMachine.Tiers[spec.t or 1] or PusherMachine.Tiers[1]
end

-- Look up a catalog spec by name (+ tier to disambiguate tier echoes).
function PusherMachine.findSpec(name, tier)
	for _, spec in ipairs(PusherMachine.Planets) do
		if spec.n == name and (tier == nil or (spec.t or 1) == tier) then
			return spec
		end
	end
	return nil
end


-- Paid boosts are a tier-scoped magnitude B (100/1000/3500). Capping B per
-- tier is what makes the ladder honest: 100X maxes out tier 2, 1000X maxes
-- out tier 3, and only tier 4/5 ride the full magnitude. B = 0 (or nil) is
-- no boost; tier 1 never boosts.
local function boostMultOf(tier, boostMag)
	local B = boostMag or 0
	if B <= 0 then
		return 1
	end
	if tier == 2 then
		return math.min(B, 100)
	elseif tier == 3 then
		return math.min(B, 1000)
	elseif tier >= 4 then
		return B
	end
	return 1
end

-- mods (optional, DEEP LUCK tree): { magnet = rareMagnetPct total,
-- deep = deepResonancePct total, scout = scoutPct total,
-- owned = set of "name|tier" the player owns }. Both the server roll and
-- every odds display pass the SAME mods, so shown odds are rolled odds.
local function effectiveWeight(spec, freeLuck, boostMag, mods)
	-- noRoll entries (special grants) are never rollable at any luck.
	if spec.noRoll then
		return 0
	end
	local w = spec.w or tierOf(spec).w
	local t = spec.t or 1
	if t == 1 then
		-- Rare Magnet SHRINKS common weight (instead of inflating rare
		-- weight): strictly better odds for every rare tier, no dilution.
		if mods and mods.magnet and mods.magnet > 0 then
			w = w / (1 + mods.magnet)
		end
		return w
	end
	w = w * math.max(freeLuck or 1, 1) * boostMultOf(t, boostMag)
	if mods then
		if t >= 4 and mods.deep and mods.deep > 0 then
			w = w * (1 + mods.deep) -- Legend Resonance: LEGENDARY+ only
		end
		if mods.scout and mods.scout > 0 and mods.owned
			and not mods.owned[spec.n .. "|" .. t] then
			w = w * (1 + mods.scout) -- Star Scout: undiscovered bias
		end
	end
	return w
end
PusherMachine.effectiveWeight = effectiveWeight

local function c3(t)
	return Color3.fromRGB(t[1], t[2], t[3])
end

-- Single source of truth for both the server roll and the Index display —
-- the odds a player reads are, by construction, the odds the server rolls.
-- Callers pass BOTH the free-luck scalar and the paid boost magnitude.
function PusherMachine.planetChances(freeLuck, boostMag, mods)
	local total = 0
	for _, spec in ipairs(PusherMachine.Planets) do
		total = total + effectiveWeight(spec, freeLuck, boostMag, mods)
	end
	local out = {}
	for _, spec in ipairs(PusherMachine.Planets) do
		-- noRoll grants have no odds; UIs list them from GameConfig.SpecialPlanets.
		if not spec.noRoll then
			local w = effectiveWeight(spec, freeLuck, boostMag, mods)
			table.insert(out, {
				name = spec.n,
				ball = c3(spec.b),
				ring = spec.rg and c3(spec.rg.c) or c3(spec.b),
				oneIn = total / w,
				tier = spec.t or 1,
				rare = (spec.t or 1) >= 3,
			})
		end
	end
	return out
end

-- minTier (optional) restricts the roll to tier >= minTier while keeping
-- relative weights — this is the pity guarantee. The reported "1 in N" is
-- still against the FULL table so the feed shows the ball's true rarity.
local function rollPlanet(freeLuck, minTier, boostMag, mods)
	local total, fullTotal = 0, 0
	for _, spec in ipairs(PusherMachine.Planets) do
		if not spec.noRoll then
			local w = effectiveWeight(spec, freeLuck, boostMag, mods)
			fullTotal = fullTotal + w
			if not minTier or (spec.t or 1) >= minTier then
				total = total + w
			end
		end
	end
	if total <= 0 then total = fullTotal minTier = nil end
	local pick = math.random() * total
	for _, spec in ipairs(PusherMachine.Planets) do
		if not spec.noRoll and (not minTier or (spec.t or 1) >= minTier) then
			pick = pick - effectiveWeight(spec, freeLuck, boostMag, mods)
			if pick <= 0 then return spec, fullTotal / effectiveWeight(spec, freeLuck, boostMag, mods) end
		end
	end
	return PusherMachine.Planets[1], 1
end

-- The roller's server-side pull: returns spec + display odds.
function PusherMachine.rollOnce(freeLuck, minTier, boostMag, mods)
	return rollPlanet(freeLuck, minTier, boostMag, mods)
end

-- Interpreter for the compact catalog format. Every decor part is welded,
-- massless, and non-colliding (decorate), so a SECRET with a dozen parts
-- rolls with exactly the same physics as a bare COMMON. form (0|1|2) layers the
-- fusion dressing (§ FORM DRESSING below) on top of the spec's own look —
-- form parts obey the same decorate/weld rule, so a CELESTIAL rolls with
-- exactly the same physics as its BASE.
local function makePlanet(spec, cf, anchored, parent, form)
	form = (form == 1 or form == 2) and form or 0
	local D = PLANET_DIAMETER
	local R = D / 2
	local mat = Enum.Material[spec.m or "SmoothPlastic"]
	local ball = newPart({
		Name = "Planet",
		Anchored = anchored,
		Shape = Enum.PartType.Ball,
		Size = Vector3.new(D, D, D),
		CFrame = cf,
		Color = c3(spec.b),
		Material = mat,
		Transparency = spec.tp or 0,
		Reflectance = spec.rf or 0.03,
		-- BOUNCY (owner: "they're not bouncy!!!"). Roblox pairs elasticity by
		-- a WEIGHTED average, and the deck is 0 at weight 1 — so a 0.5 planet
		-- at weight 1 landed at 0.25 (a 0.36-stud hop off a 5.5 drop, i.e.
		-- nothing), and the 0.3 pass at 0.15 was invisible. The planet's own
		-- number now dominates the pair: elasticity 0.5 at weight 6 lands on
		-- the deck at 0.43 (a 1.0-stud first hop, settled in three), on a
		-- coin at 0.46, on another planet at 0.5. The mouth funnel parts
		-- carry weight 100 at 0 so the throat stays dead and nothing pinballs
		-- there; the JACKPOT CUP's lucky hop is an impact launch off a
		-- front-row piece and gets livelier with this, not rarer.
		CustomPhysicalProperties = PhysicalProperties.new(1.8, 0.6, 0.5, 1, 6),
	}, parent)
	ball:SetAttribute("Variation", spec.n)
	ball:SetAttribute("Tier", spec.t or 1)
	ball:SetAttribute("Payout", tierOf(spec).payout)
	-- Base stamp; the server re-stamps Payout = fusionPayout(...) at spawn.
	ball:SetAttribute("Form", form)
	if spec.m == "ForceField" or (spec.tp or 0) > 0 then
		-- DevForum ForceField guidance: a solid blocky shadow undermines
		-- the energy/glass read.
		ball.CastShadow = false
	end
	local function deco(props)
		props.Anchored = anchored
		local part = decorate(newPart(props, ball))
		weldTo(ball, part)
		return part
	end

	-- Surface overlays.
	local ov = spec.ov
	if ov and ov.s == "bands" then
		local lanes = { { 0 }, { -0.16, 0.16 }, { -0.26, 0, 0.26 } }
		local set = lanes[math.min(#ov.c, 3)]
		for i, hy in ipairs(set) do
			local lat = math.sqrt(math.max(1 - (hy / R) * (hy / R), 0.2))
			deco({
				Name = "Band" .. i,
				Shape = Enum.PartType.Cylinder,
				Size = Vector3.new(0.1, D * lat + 0.04, D * lat + 0.04),
				CFrame = cf * CFrame.new(0, hy, 0) * FLAT,
				Color = c3(ov.c[math.min(i, #ov.c)]),
				Material = ball.Material,
			})
		end
	elseif ov and ov.s == "cap" then
		for i, side in ipairs({ 1, -1 }) do
			deco({
				Name = "Cap" .. i,
				Shape = Enum.PartType.Ball,
				Size = Vector3.new(D * 0.5, D * 0.5, D * 0.5),
				CFrame = cf * CFrame.new(0, side * R * 0.66, 0),
				Color = c3(ov.c[math.min(i, #ov.c)]),
				Material = ball.Material,
			})
		end
	elseif ov and ov.s == "swirl" then
		for i = 1, 2 do
			local tilt = CFrame.Angles(math.rad(math.random() * 360),
				math.rad(math.random() * 360), 0)
			deco({
				Name = "Swirl" .. i,
				Shape = Enum.PartType.Cylinder,
				Size = Vector3.new(0.05, D * 0.85, D * 0.85),
				CFrame = cf * tilt * CFrame.new(R * 0.28, 0, 0),
				Color = c3(ov.c[math.min(i, #ov.c)]),
				Material = ball.Material,
			})
		end
	elseif ov and ov.s == "core" then
		deco({
			Name = "Core",
			Shape = Enum.PartType.Ball,
			Size = Vector3.new(D * 0.55, D * 0.55, D * 0.55),
			CFrame = cf,
			Color = c3(ov.c[1]),
			Material = Enum.Material.Neon,
		})
	end

	-- Rings. Random yaw + tilt scatter so no two planets read as clones.
	-- ringTilt/ringColor are hoisted out of the rg branch: the fusion form
	-- dressing reuses the same ring plane (and falls back to the body color
	-- for ringless specs, matching planetChances' ring fallback).
	local rg = spec.rg
	local ringTilt = CFrame.Angles(0, math.rad(math.random() * 360), 0)
		* CFrame.Angles(math.rad(18 + math.random() * 14), 0, math.rad(8 + math.random() * 10))
	local ringColor = rg and c3(rg.c) or c3(spec.b)
	if rg then
		local rmat = Enum.Material[rg.m or "Metal"]
		local style = rg.s or "classic"
		local function disc(name, dia, thick, color, matOverride, offset)
			return deco({
				Name = name,
				Shape = Enum.PartType.Cylinder,
				Size = Vector3.new(thick, dia, dia),
				CFrame = cf * ringTilt * CFrame.new(0, offset or 0, 0) * FLAT,
				Color = color,
				Material = matOverride or rmat,
			})
		end
		if style == "classic" then
			disc("PlanetRing", D * 1.3, 0.07, c3(rg.c))
		elseif style == "double" then
			disc("PlanetRing", D * 1.38, 0.05, c3(rg.c), nil, 0.05)
			disc("PlanetRing2", D * 1.12, 0.05, c3(rg.c2 or rg.c), nil, -0.05)
		elseif style == "halo" then
			disc("PlanetRing", D * 1.2, 0.04, c3(rg.c), Enum.Material.Neon)
		elseif style == "crossed" then
			disc("PlanetRing", D * 1.26, 0.05, c3(rg.c))
			deco({
				Name = "PlanetRing2",
				Shape = Enum.PartType.Cylinder,
				Size = Vector3.new(0.05, D * 1.26, D * 1.26),
				CFrame = cf * ringTilt * CFrame.Angles(0, 0, math.rad(90)) * FLAT,
				Color = c3(rg.c2 or rg.c),
				Material = rmat,
			})
		elseif style == "orbit" then
			disc("PlanetRing", D * 1.3, 0.06, c3(rg.c))
			for i = 1, 2 do
				local a = math.rad(math.random() * 360)
				deco({
					Name = "Moonlet" .. i,
					Shape = Enum.PartType.Ball,
					Size = Vector3.new(D * 0.18, D * 0.18, D * 0.18),
					CFrame = cf * ringTilt
						* CFrame.new(math.cos(a) * D * 0.65, 0, math.sin(a) * D * 0.65),
					Color = c3(rg.c2 or rg.c),
					Material = rmat,
				})
			end
		elseif style == "shards" then
			for i = 1, 4 do
				local a = math.rad(i * 90 + math.random() * 40 - 20)
				deco({
					Name = "Shard" .. i,
					Size = Vector3.new(0.3, 0.05, 0.13),
					CFrame = cf * ringTilt
						* CFrame.new(math.cos(a) * D * 0.62, 0, math.sin(a) * D * 0.62)
						* CFrame.Angles(0, -a + math.rad(90), math.rad(math.random() * 24 - 12)),
					Color = c3(rg.c),
					Material = rmat,
				})
			end
		end
	end

	-- Shimmer glaze: fresnel force-field shell over the body.
	if spec.sh then
		local glaze = deco({
			Name = "Glaze",
			Shape = Enum.PartType.Ball,
			Size = Vector3.new(D * 1.06, D * 1.06, D * 1.06),
			CFrame = cf,
			Color = c3(spec.b),
			Material = Enum.Material.ForceField,
			Transparency = 0.35,
		})
		glaze.CastShadow = false
	end

	-- Aura: one Neon shell over the body, translucent enough that Neon reads
	-- as a halo, not a second ball. Its radius (0.6-0.75 D) clears the glaze
	-- (0.53 D), the caps (reach 0.58 D) and every core (<= 0.28 D), so no
	-- sphere surfaces fight; rings pass straight through it, which is the look.
	local aura = spec.aura
	if aura then
		-- Clamped hard: Neon blooms, and a deck full of one equipped SECRET is
		-- 30 shells stacked — at the spec'd 0.6-0.75 they fused into a white
		-- cloud that hid the pile (owner screenshot). Floor 0.86 / cap 1.35 D
		-- keeps a single ball haloed and a pile readable.
		local asz = D * math.min(aura.sz or 1.3, 1.35)
		local shell = deco({
			Name = "Aura",
			Shape = Enum.PartType.Ball,
			Size = Vector3.new(asz, asz, asz),
			CFrame = cf,
			Color = c3(aura.c or spec.b),
			Material = Enum.Material.Neon,
			Transparency = math.max(aura.tp or 0.86, 0.86),
		})
		shell.CastShadow = false
	end

	-- Moons: n tiny balls parked at fixed, evenly spaced spots on a circle
	-- of radius 0.85 D (outside every spec ring, 0.56-0.69 D; form 2's outer
	-- band shares the radius, and a moon on it simply reads as riding it).
	-- The circle is tilted 20 deg off the equator on a random yaw, so the
	-- moons never line up with the ring plane the same way twice. Static —
	-- no driver, welded like every other decor part.
	local moons = spec.moons
	if moons then
		local count = math.max(1, math.min(moons.n or 1, 3))
		local msz = D * (moons.sz or 0.2)
		local plane = CFrame.Angles(0, math.rad(math.random() * 360), 0)
			* CFrame.Angles(math.rad(20), 0, 0)
		local phase = math.rad(math.random() * 360)
		for i = 1, count do
			local a = phase + (i - 1) * (2 * math.pi / count)
			deco({
				Name = "Moon" .. i,
				Shape = Enum.PartType.Ball,
				Size = Vector3.new(msz, msz, msz),
				CFrame = cf * plane
					* CFrame.new(math.cos(a) * D * 0.85, 0, math.sin(a) * D * 0.85),
				Color = c3(moons.c or spec.b),
				Material = Enum.Material[moons.m or "Neon"],
			})
		end
	end

	----------------------------------------------------------------------------
	-- FORM DRESSING (fusion §8). Layered ON TOP of the spec's own dressing.
	-- Budgets: form 1 adds 4 parts, form 2 adds 12 (13 LEGENDARY / 14 SECRET).
	-- Axial offsets keep every new face clear of the spec ring planes
	-- (anti-z-fight rule), and orientations dodge the spec ring styles.
	----------------------------------------------------------------------------
	if form >= 1 then
		-- FORM 1 "ASCENDED": crossed ring perpendicular to the spec's ring
		-- plane — rotated 90 deg off the "crossed" style's second ring so
		-- both stay visible on Magnet/Prism-family specs.
		deco({
			Name = "AscRing",
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.07, D * 1.38, D * 1.38),
			CFrame = cf * ringTilt * CFrame.Angles(math.rad(90), 0, 0) * FLAT,
			Color = ringColor:Lerp(Color3.new(1, 1, 1), 0.35),
			Material = Enum.Material.Neon,
		})
		-- Two moonlets on opposite sides of the ring plane at 0.75 D.
		local ma = math.rad(math.random() * 360)
		for i, side in ipairs({ 1, -1 }) do
			deco({
				Name = "AscMoonlet" .. i,
				Shape = Enum.PartType.Ball,
				Size = Vector3.new(D * 0.16, D * 0.16, D * 0.16),
				CFrame = cf * ringTilt * CFrame.new(
					side * math.cos(ma) * D * 0.75, 0, side * math.sin(ma) * D * 0.75),
				Color = ringColor,
				Material = Enum.Material.Neon,
			})
		end
		-- Inner core glow (nested inside any spec "core" overlay; different
		-- radii, so the sphere surfaces can never fight).
		deco({
			Name = "AscCore",
			Shape = Enum.PartType.Ball,
			Size = Vector3.new(D * 0.4, D * 0.4, D * 0.4),
			CFrame = cf,
			Color = c3(spec.b):Lerp(Color3.new(1, 1, 1), 0.5),
			Material = Enum.Material.Neon,
			Transparency = 0.25,
		})
	end
	if form >= 2 then
		-- FORM 2 "CELESTIAL": ring stack — three parallel Neon bands, tints
		-- body -> ring -> white, stepped 0.09 apart along the ring axis (a
		-- same-diameter spec ring would otherwise share its curved surface
		-- with the 1.30x band).
		local stack = {
			{ dia = 1.30, off = 0.09, col = c3(spec.b) },
			{ dia = 1.50, off = 0, col = ringColor },
			{ dia = 1.70, off = -0.09, col = Color3.new(1, 1, 1) },
		}
		for i, band in ipairs(stack) do
			deco({
				Name = "CelBand" .. i,
				Shape = Enum.PartType.Cylinder,
				Size = Vector3.new(0.055, D * band.dia, D * band.dia),
				CFrame = cf * ringTilt * CFrame.new(0, band.off, 0) * FLAT,
				Color = band.col,
				Material = Enum.Material.Neon,
			})
		end
		-- Four orbit shards at 45-degree steps (offset from the spec
		-- "shards" style's 90-degree lattice), farther out at 0.85 D.
		for i = 1, 4 do
			local sa = math.rad(45 + (i - 1) * 90)
			deco({
				Name = "CelShard" .. i,
				Size = Vector3.new(0.22, 0.05, 0.12),
				CFrame = cf * ringTilt
					* CFrame.new(math.cos(sa) * D * 0.85, 0, math.sin(sa) * D * 0.85)
					* CFrame.Angles(0, -sa + math.rad(90), math.rad(45)),
				Color = ringColor,
				Material = Enum.Material.Metal,
			})
		end
		-- Aurora band: thin see-through Neon torus, cyan-shifted, 65 deg.
		local aurora = deco({
			Name = "CelAurora",
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.04, D * 1.9, D * 1.9),
			CFrame = cf * ringTilt * CFrame.Angles(math.rad(65), 0, 0) * FLAT,
			Color = ringColor:Lerp(Color3.fromRGB(80, 255, 230), 0.5),
			Material = Enum.Material.Neon,
			Transparency = 0.55,
		})
		aurora.CastShadow = false
		pointLight(ball, ringColor, 1.2, 5)
		-- Bespoke crowns for the top tiers.
		local tier = spec.t or 1
		if tier == 4 then
			-- LEGENDARY: one vertical gold halo (FLAT-crossed, no tilt).
			deco({
				Name = "CelHalo",
				Shape = Enum.PartType.Cylinder,
				Size = Vector3.new(0.05, D * 1.6, D * 1.6),
				CFrame = cf * CFrame.Angles(0, 0, math.rad(90)) * FLAT,
				Color = Color3.fromRGB(255, 196, 74),
				Material = Enum.Material.Neon,
			})
		elseif tier == 5 then
			-- SECRET: two counter-tilted halos + a star4 emitter (rate 6 —
			-- the one sanctioned extra emitter above the spec's own fx/fx2).
			for i, tiltDeg in ipairs({ 28, -28 }) do
				deco({
					Name = "CelHalo" .. i,
					Shape = Enum.PartType.Cylinder,
					Size = Vector3.new(0.05, D * 1.6, D * 1.6),
					CFrame = cf * CFrame.Angles(math.rad(tiltDeg), 0, 0)
						* CFrame.Angles(0, 0, math.rad(90)) * FLAT,
					Color = Color3.fromRGB(255, 196, 74),
					Material = Enum.Material.Neon,
				})
			end
			local star = Instance.new("ParticleEmitter")
			star.Texture = ART.star4
			star.Color = ColorSequence.new(Color3.fromRGB(255, 240, 190))
			star.LightEmission = 1
			star.Rate = 6
			star.Lifetime = NumberRange.new(0.5, 1.0)
			star.Speed = NumberRange.new(0.25, 0.6)
			star.SpreadAngle = Vector2.new(180, 180)
			star.Rotation = NumberRange.new(-40, 40)
			star.RotSpeed = NumberRange.new(-120, 120)
			star.Size = NumberSequence.new({
				NumberSequenceKeypoint.new(0, 0.3),
				NumberSequenceKeypoint.new(1, 0),
			})
			star.Parent = ball
		end
	end

	if spec.lt then
		pointLight(ball, c3(spec.lt.c), spec.lt.b or 0.8, 4.5)
	end
	-- Sprite emitter in a style's motion signature (shared by fx and fx2).
	local function emit(style, color, rate)
		local emitter = Instance.new("ParticleEmitter")
		emitter.Texture = style.tex
		emitter.Color = ColorSequence.new(color)
		emitter.LightEmission = 1
		emitter.Rate = rate
		emitter.Lifetime = NumberRange.new(style.life[1], style.life[2])
		emitter.Speed = NumberRange.new(style.spd[1], style.spd[2])
		emitter.Acceleration = Vector3.new(0, style.ay or 0, 0)
		emitter.SpreadAngle = Vector2.new(180, 180)
		emitter.Rotation = NumberRange.new(-40, 40)
		emitter.RotSpeed = NumberRange.new(-style.rot, style.rot)
		emitter.Size = NumberSequence.new({
			NumberSequenceKeypoint.new(0, style.sz),
			NumberSequenceKeypoint.new(1, 0),
		})
		emitter.Parent = ball
		return emitter
	end
	-- Ascended+ forms sparkle: +2 rate on the spec's emitter (hard cap 8
	-- stands); a spec with no fx of its own gets a rate-2 glowOrb in the
	-- ring color instead. The +2 lands on fx only, never on fx2.
	local fxRate = 0
	if spec.fx or form >= 1 then
		local fx = spec.fx
		local style = (fx and SPRITE_STYLE[fx.s]) or SPRITE_STYLE.glowOrb
		local rate = fx and (fx.r or 4) or 0
		if form >= 1 then
			rate = rate + 2
		end
		fxRate = math.min(rate, 8)
		emit(style, fx and c3(fx.c) or ringColor, fxRate)
	end
	-- Second emitter: fx and fx2 share a 10/s budget, so fx2 gets whatever
	-- fx left over (and never more than the 8 per-emitter cap).
	local fx2 = spec.fx2
	if fx2 then
		local rate2 = math.min(fx2.r or 3, 8, 10 - fxRate)
		if rate2 > 0 then
			local style2 = SPRITE_STYLE[fx2.s] or SPRITE_STYLE.glowOrb
			emit(style2, c3(fx2.c or spec.b), rate2)
		end
	end
	-- Ascended+ forms always leave a trail; ring color when the spec has none.
	local trailColor = spec.tr and c3(spec.tr) or (form >= 1 and ringColor or nil)
	if trailColor then
		local a0 = Instance.new("Attachment")
		a0.Position = Vector3.new(0, PLANET_DIAMETER * 0.3, 0)
		a0.Parent = ball
		local a1 = Instance.new("Attachment")
		a1.Position = Vector3.new(0, -PLANET_DIAMETER * 0.3, 0)
		a1.Parent = ball
		local trail = Instance.new("Trail")
		trail.Attachment0 = a0
		trail.Attachment1 = a1
		trail.Color = ColorSequence.new(trailColor)
		trail.LightEmission = 1
		trail.Lifetime = 0.3
		trail.FaceCamera = true
		trail.Transparency = NumberSequence.new({
			NumberSequenceKeypoint.new(0, 0.3),
			NumberSequenceKeypoint.new(1, 1),
		})
		trail.WidthScale = NumberSequence.new({
			NumberSequenceKeypoint.new(0, 1),
			NumberSequenceKeypoint.new(1, 0),
		})
		trail.Parent = ball
	end
	return ball
end

-- Build a specific catalog ball by name (highest tier wins a name tie, or
-- pass tier). For showcases, index previews, and tutorials. form (0|1|2)
-- renders the fusion dressing.
function PusherMachine.spawnSpecimen(name, tier, cf, anchored, parent, form)
	local found = nil
	for _, spec in ipairs(PusherMachine.Planets) do
		if spec.n == name and (tier == nil or (spec.t or 1) == tier) then
			if not found or (spec.t or 1) > (found.t or 1) then found = spec end
		end
	end
	if not found then return nil end
	return makePlanet(found, cf, anchored ~= false, parent or workspace, form)
end

-- Drop a rolled planet variation from the dropper mouth. Server-side.
-- Spawn position gets a small random scatter and the ball a random spin:
-- identical spam-drops used to land dead-center on each other and build a
-- perfect standing tower; scatter + spin make a stable stack impossible.
-- forcedSpec (optional): drop THIS exact ball instead of rolling — the
-- equipped-ball economy path. Rolling only happens in the gacha roller.
-- form (0|1|2): the fusion form to dress the dropped ball with (the server
-- passes the owner's EquippedForm).
function PusherMachine.dropPlanet(model, xOffset, luck, minTier, forcedSpec, form)
	local dropZone = model.Playfield:FindFirstChild("DropZone")
	if not dropZone then return nil end
	local spec, oneIn
	if forcedSpec then
		spec, oneIn = forcedSpec, 1
	else
		spec, oneIn = rollPlanet(luck, minTier)
	end
	local jitterX = (math.random() - 0.5) * 0.8
	local jitterZ = (math.random() - 0.5) * 0.7
	local x = math.clamp((xOffset or 0) + jitterX, -4.6, 4.6)
	local ball = makePlanet(spec,
		dropZone.CFrame * CFrame.new(x, 0, jitterZ), false, model, form)
	ball.AssemblyAngularVelocity = Vector3.new(
		(math.random() - 0.5) * 8,
		(math.random() - 0.5) * 8,
		(math.random() - 0.5) * 8)
	pcall(function() ball:SetNetworkOwner(nil) end)
	return ball, spec, oneIn
end

--------------------------------------------------------------------------------
-- Environment: dark arcade room + Lighting rig (themed)
--------------------------------------------------------------------------------

function PusherMachine.buildEnvironment(originCFrame, theme)
	originCFrame = originCFrame or CFrame.new()
	local cfg = themedConfig(theme)
	local lit = cfg.lighting
	local Lighting = game:GetService("Lighting")

	pcall(function()
		Lighting.Technology = Enum.Technology.Future
	end)
	Lighting.ClockTime = lit.clockTime
	Lighting.Brightness = lit.brightness
	Lighting.Ambient = lit.ambient
	Lighting.OutdoorAmbient = lit.outdoorAmbient
	Lighting.ExposureCompensation = lit.exposure or 0
	Lighting.EnvironmentDiffuseScale = 0.2
	Lighting.EnvironmentSpecularScale = 0.55
	Lighting.GlobalShadows = true
	pcall(function()
		Lighting.ShadowSoftness = 0.2
	end)
	Lighting.FogColor = lit.fogColor
	Lighting.FogStart = lit.fogStart
	Lighting.FogEnd = lit.fogEnd

	-- Clear template post-effects that fight the rig (Atmosphere overrides
	-- classic fog entirely), plus our own named effects so the build is
	-- idempotent. The Sky stays.
	for _, effectName in ipairs({
		"PusherBloom", "PusherColor", "PusherDoF", "PusherAtmo",
		"Atmosphere", "SunRays", "Bloom", "DepthOfField",
	}) do
		local old = Lighting:FindFirstChild(effectName)
		if old then old:Destroy() end
	end

	local bloom = Instance.new("BloomEffect")
	bloom.Name = "PusherBloom"
	bloom.Intensity = lit.bloomIntensity
	bloom.Size = 24
	bloom.Threshold = 1.35
	bloom.Parent = Lighting

	local color = Instance.new("ColorCorrectionEffect")
	color.Name = "PusherColor"
	color.Contrast = lit.contrast or 0.1
	color.Saturation = lit.saturation or 0.12
	color.TintColor = lit.tint
	color.Parent = Lighting

	local dof = Instance.new("DepthOfFieldEffect")
	dof.Name = "PusherDoF"
	dof.FocusDistance = 16
	dof.InFocusRadius = 22
	dof.FarIntensity = 0.3
	dof.NearIntensity = 0.12
	dof.Parent = Lighting

	----------------------------------------------------------------------------
	-- Room shell
	----------------------------------------------------------------------------
	local old = workspace:FindFirstChild("ArcadeRoom")
	if old then old:Destroy() end

	local room = Instance.new("Model")
	room.Name = "ArcadeRoom"

	local function at(x, y, z, rot)
		local cf = originCFrame * CFrame.new(x, y, z)
		if rot then cf = cf * rot end
		return cf
	end

	-- Floor top at y 0.06 (never z-fights the default baseplate at y 0).
	newPart({
		Name = "Floor",
		Size = Vector3.new(64, 0.32, 64),
		CFrame = at(0, -0.1, -6),
		Color = cfg.colFloor,
		Material = Enum.Material.Concrete,
	}, room)

	-- Twin neon runway. Bottom face 0.01 above the floor top: no shared plane.
	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "RunwayStrip",
			Size = Vector3.new(0.28, 0.12, 17),
			CFrame = at(side * 5.4, 0.13, -19),
			Color = cfg.colTeal,
			Material = Enum.Material.Neon,
			CanCollide = false,
		}, room)
	end

	-- Gold stage frame around the plinth (floats 0.01 above the floor).
	newPart({ Name = "StageFrame", Size = Vector3.new(17.6, 0.12, 0.3), CFrame = at(0, 0.13, -9.6),
		Color = cfg.colGold, Material = Enum.Material.Metal, CanCollide = false }, room)
	newPart({ Name = "StageFrame", Size = Vector3.new(17.6, 0.12, 0.3), CFrame = at(0, 0.13, 9.0),
		Color = cfg.colGold, Material = Enum.Material.Metal, CanCollide = false }, room)
	for _, side in ipairs({ -1, 1 }) do
		newPart({ Name = "StageFrame", Size = Vector3.new(0.3, 0.12, 18.9), CFrame = at(side * 8.65, 0.13, -0.3),
			Color = cfg.colGold, Material = Enum.Material.Metal, CanCollide = false }, room)
	end

	-- Walls + ceiling.
	local wallHeight, wallY = 24, 12
	local walls = {
		{ Vector3.new(62, wallHeight, 1), at(0, wallY, 15) },
		{ Vector3.new(62, wallHeight, 1), at(0, wallY, -37) },
		{ Vector3.new(1, wallHeight, 52), at(-31, wallY, -11) },
		{ Vector3.new(1, wallHeight, 52), at(31, wallY, -11) },
	}
	-- openRoom themes (daylight zones): glass walls and NO ceiling, so the
	-- skybox shows and real sunlight reaches the machine.
	for _, spec in ipairs(walls) do
		newPart({
			Name = "Wall", Size = spec[1], CFrame = spec[2], Color = cfg.colRoom,
			Material = cfg.openRoom and Enum.Material.Glass or Enum.Material.SmoothPlastic,
			Transparency = cfg.openRoom and 0.7 or 0,
		}, room)
	end
	if not cfg.openRoom then
		newPart({
			Name = "Ceiling",
			Size = Vector3.new(62, 0.6, 52),
			CFrame = at(0, wallHeight + 0.3, -11),
			Color = cfg.colDark,
		}, room)
	end

	-- Twin neon rails wrapping the walls.
	local railSpecs = {
		{ Vector3.new(60.5, 0.14, 0.14), at(0, 7.6, 14.4) },
		{ Vector3.new(60.5, 0.14, 0.14), at(0, 8.3, 14.4) },
		{ Vector3.new(60.5, 0.14, 0.14), at(0, 7.6, -36.4) },
		{ Vector3.new(60.5, 0.14, 0.14), at(0, 8.3, -36.4) },
		{ Vector3.new(0.14, 0.14, 50.5), at(-30.4, 7.6, -11) },
		{ Vector3.new(0.14, 0.14, 50.5), at(-30.4, 8.3, -11) },
		{ Vector3.new(0.14, 0.14, 50.5), at(30.4, 7.6, -11) },
		{ Vector3.new(0.14, 0.14, 50.5), at(30.4, 8.3, -11) },
	}
	for i, spec in ipairs(railSpecs) do
		newPart({
			Name = "WallRail",
			Size = spec[1],
			CFrame = spec[2],
			Color = (i % 2 == 1) and cfg.colTeal or cfg.colMagenta,
			Material = Enum.Material.Neon,
			CanCollide = false,
		}, room)
	end

	----------------------------------------------------------------------------
	-- RankBoard: physical TOP EARNERS board on the +x wall's inner face, where
	-- spectators see it (the z +15 wall is BEHIND the machine). The server
	-- finds these labels BY NAME (RankBoard/BoardGui/Row1..Row5/RankLabel+
	-- NameLabel+ValueLabel) and writes them 1x/90s — names are a contract.
	-- Rebuilt with the room, so it inherits the ArcadeRoom idempotency.
	----------------------------------------------------------------------------
	do
		local board = Instance.new("Model")
		board.Name = "RankBoard"
		board.Parent = room

		-- Wall inner face x 30.5; panel back floats 0.01 proud (no shared
		-- plane), so panel spans x 30.19..30.49. Front face points -x (rotated
		-- 90 about Y: local -z -> world -x), 10 wide along world z.
		local panelRot = CFrame.Angles(0, math.rad(90), 0)
		local panel = decorate(newPart({
			Name = "BoardPanel",
			Size = Vector3.new(10, 7, 0.3),
			CFrame = at(30.34, 8, -6, panelRot),
			Color = cfg.colDark,
			Material = Enum.Material.Metal,
		}, board))

		-- Gold frame trim: 4 edge strips, 0.12 thick, front face 0.03 proud of
		-- the panel front (x 30.16, center 30.22 — back sinks into the panel,
		-- never coplanar with it). Every strip stays 0.02+ inside the panel
		-- silhouette (edges y 4.5/11.5, z -1/-11), and the verticals stop 0.01
		-- short of the horizontals' ends: no shared plane anywhere.
		local frameSpecs = {
			{ Vector3.new(0.12, 0.12, 9.88), at(30.22, 11.42, -6) },
			{ Vector3.new(0.12, 0.12, 9.88), at(30.22, 4.58, -6) },
			{ Vector3.new(0.12, 6.72, 0.12), at(30.22, 8, -1.13) },
			{ Vector3.new(0.12, 6.72, 0.12), at(30.22, 8, -10.87) },
		}
		for _, spec in ipairs(frameSpecs) do
			decorate(newPart({
				Name = "BoardFrame",
				Size = spec[1],
				CFrame = spec[2],
				Color = cfg.colGold,
				Material = Enum.Material.Neon,
			}, board))
		end

		local boardGui = Instance.new("SurfaceGui")
		boardGui.Name = "BoardGui"
		boardGui.Face = Enum.NormalId.Front
		boardGui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
		boardGui.PixelsPerStud = 64
		boardGui.LightInfluence = 0
		boardGui.Brightness = 2
		boardGui.Parent = panel

		local title = Instance.new("TextLabel")
		title.Name = "Title"
		title.Size = UDim2.fromScale(1, 0.15)
		title.Position = UDim2.fromScale(0, 0)
		title.BackgroundTransparency = 1
		title.Text = "TOP EARNERS"
		title.TextColor3 = cfg.colGoldHot
		title.TextScaled = true
		title.Font = Enum.Font.FredokaOne
		title.Parent = boardGui

		-- Rows 1-3 get medal-metal rank tints; 4-5 plain white.
		local rankTints = {
			cfg.colGold,
			Color3.fromRGB(200, 205, 215),
			Color3.fromRGB(205, 145, 90),
		}
		for i = 1, 5 do
			local row = Instance.new("Frame")
			row.Name = "Row" .. i
			row.Size = UDim2.fromScale(1, 0.14)
			row.Position = UDim2.fromScale(0, 0.17 + (i - 1) * 0.155)
			row.BackgroundTransparency = 1
			row.Parent = boardGui

			local rank = Instance.new("TextLabel")
			rank.Name = "RankLabel"
			rank.Size = UDim2.fromScale(0.18, 1)
			rank.Position = UDim2.fromScale(0, 0)
			rank.BackgroundTransparency = 1
			rank.Text = "#" .. i
			rank.TextColor3 = rankTints[i] or Color3.new(1, 1, 1)
			rank.TextScaled = true
			rank.Font = Enum.Font.FredokaOne
			rank.Parent = row

			local nameLabel = Instance.new("TextLabel")
			nameLabel.Name = "NameLabel"
			nameLabel.Size = UDim2.fromScale(0.52, 1)
			nameLabel.Position = UDim2.fromScale(0.18, 0)
			nameLabel.BackgroundTransparency = 1
			nameLabel.Text = "—"
			nameLabel.TextColor3 = Color3.new(1, 1, 1)
			nameLabel.TextXAlignment = Enum.TextXAlignment.Left
			nameLabel.TextScaled = true
			nameLabel.Font = Enum.Font.FredokaOne
			nameLabel.Parent = row

			local valueLabel = Instance.new("TextLabel")
			valueLabel.Name = "ValueLabel"
			valueLabel.Size = UDim2.fromScale(0.30, 1)
			valueLabel.Position = UDim2.fromScale(0.70, 0)
			valueLabel.BackgroundTransparency = 1
			valueLabel.Text = "—"
			valueLabel.TextColor3 = cfg.colGold
			valueLabel.TextXAlignment = Enum.TextXAlignment.Right
			valueLabel.TextScaled = true
			valueLabel.Font = Enum.Font.FredokaOne
			valueLabel.Parent = row
		end
	end

	----------------------------------------------------------------------------
	-- Stage lights (themed)
	----------------------------------------------------------------------------
	local function spot(name, pos, target, lightColor, brightness, angle, range, shadows)
		local host = newPart({
			Name = name,
			Size = Vector3.new(0.4, 0.4, 0.4),
			CFrame = CFrame.lookAt((originCFrame * CFrame.new(pos)).Position,
				(originCFrame * CFrame.new(target)).Position),
			Transparency = 1, CanCollide = false, CanQuery = false,
		}, room)
		local light = Instance.new("SpotLight")
		light.Face = Enum.NormalId.Front
		light.Color = lightColor
		light.Brightness = brightness
		light.Angle = angle
		light.Range = range
		light.Shadows = shadows and true or false
		light.Parent = host
		return light
	end

	spot("KeyLight", Vector3.new(0, 21, -14), Vector3.new(0, 5, -1), lit.keyColor, lit.keyBrightness or 1.6, 42, 42, false)
	spot("RimLight", Vector3.new(0, 20, 12), Vector3.new(0, 6, -3), lit.rimColor, lit.rimBrightness or 1.0, 45, 40, false)
	spot("FillLight", Vector3.new(-18, 13, -10), Vector3.new(0, 5, 0), lit.fillColor, lit.fillBrightness or 0.45, 55, 40, false)

	room.Parent = workspace
	return room
end

--------------------------------------------------------------------------------
-- Build
--------------------------------------------------------------------------------

function PusherMachine.build(originCFrame, name, theme)
	originCFrame = originCFrame or CFrame.new()
	local cfg = themedConfig(theme)

	local model = Instance.new("Model")
	model.Name = name or "CoinPusher"

	local folders = {}
	for _, folderName in ipairs({ "Cabinet", "Playfield", "Payout", "Mounts", "Decor", "Templates", "FX" }) do
		local folder = Instance.new("Folder")
		folder.Name = folderName
		folder.Parent = model
		folders[folderName] = folder
	end
	local cabinet, playfield, payout, fx = folders.Cabinet, folders.Playfield, folders.Payout, folders.FX

	local function at(x, y, z, rot)
		local cf = originCFrame * CFrame.new(x, y, z)
		if rot then cf = cf * rot end
		return cf
	end

	local function neonStrip(size, cf, color, parent, partName)
		return newPart({
			Name = partName or "Trim", Size = size, CFrame = cf, Color = color,
			Material = Enum.Material.Neon, CanCollide = false,
		}, parent or cabinet)
	end

	----------------------------------------------------------------------------
	-- Plinth and under-deck body
	----------------------------------------------------------------------------

	newPart({
		Name = "Plinth",
		Size = Vector3.new(16.0, 1.0, 17.4),
		CFrame = at(0, 0.5, -0.3),
		Color = cfg.colDark,
	}, cabinet)

	-- Teal underglow skirting the plinth.
	neonStrip(Vector3.new(15.6, 0.18, 0.18), at(0, 0.96, -9.02), cfg.colTeal)
	for _, side in ipairs({ -1, 1 }) do
		neonStrip(Vector3.new(0.18, 0.18, 17.2), at(side * 8.02, 0.96, -0.3), cfg.colTeal)
	end

	-- Body block under the deck. Top at 4.49 (clear of the deck underside at
	-- 4.46); front at -4.29 (recessed behind the reclined header wall, which
	-- stands 0.28 in front of it); back at 7.99 (clear of the tower back at
	-- 8.00). No shared planes anywhere. PAIRED with the client camera-origin
	-- constant (PusherClient derives origin = Body.CFrame * (0,-2.745,-1.85)).
	newPart({
		Name = "Body",
		Size = Vector3.new(13.4, 3.49, 12.28),
		CFrame = at(0, 2.745, 1.85),
		Color = cfg.colBody,
	}, cabinet)

	-- Bevel trim on the plinth's exposed top edges (front + both sides).
	local plinthBevelFront = newPart({
		Name = "PlinthBevel",
		Shape = "Wedge",
		Size = Vector3.new(15.6, 0.22, 0.4),
		CFrame = at(0, 1.115, -8.79),
		Color = cfg.colBody2,
	}, cabinet)
	decorate(plinthBevelFront)
	for _, side in ipairs({ -1, 1 }) do
		-- Yaw -side*90: the wedge's high edge lands inboard, so the slope
		-- falls away toward the plinth's outer edge like a machined chamfer.
		local bevel = newPart({
			Name = "PlinthBevel",
			Shape = "Wedge",
			Size = Vector3.new(16.4, 0.22, 0.4),
			CFrame = at(side * 7.79, 1.115, -0.3, CFrame.Angles(0, math.rad(-side * 90), 0)),
			Color = cfg.colBody2,
		}, cabinet)
		decorate(bevel)
	end

	----------------------------------------------------------------------------
	-- Prize tray cheeks (the old flat tray floor is gone — the STADIUM STEPS
	-- apron + basin below replace it)
	----------------------------------------------------------------------------

	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "TrayCheek",
			Size = Vector3.new(1.7, 3.2, 3.2),
			CFrame = at(side * 5.85, 2.6, -6.6),
			Color = cfg.colBody,
		}, cabinet)
		-- Ramp on the cheek top: the ledge is 12 wide but the mouth only 10,
		-- so pieces falling at |x| > 5.0 used to land on this flat top and
		-- sit there stranded forever. The wedge slopes inward, so they slide
		-- into the mouth (and through the Collector) instead.
		-- Z-window SHRUNK to -6.9..-5.1: the old full-depth ramp (z -8.2..-5.0)
		-- funneled pieces down FORWARD of the PoolRim (-6.96) where the basin
		-- no longer exists — they fell clean out of the cabinet (owner
		-- screenshot). Pieces landing on the flat cheek top behind the ramp
		-- are stranded-rescued by housekeeping at full Payout value.
		newPart({
			Name = "CheekRamp",
			Shape = "Wedge",
			Size = Vector3.new(1.8, 0.9, 1.7),
			CFrame = at(side * 5.85, 4.64, -6.0, CFrame.Angles(0, math.rad(side * 90), 0)),
			Color = cfg.colBody,
		}, cabinet)
		-- Dress panel on the cheek's spectator face.
		local cheekPanel = newPart({
			Name = "CheekPanel",
			Size = Vector3.new(1.5, 2.6, 0.06),
			CFrame = at(side * 5.85, 2.7, -8.23),
			Color = cfg.colBody2,
		}, cabinet)
		decorate(cheekPanel)
	end

	-- 13.36 wide: its end planes must not coincide with the tray cheeks' outer
	-- faces at x ±6.7.
	newPart({
		Name = "TrayLip",
		Size = Vector3.new(13.36, 0.5, 0.4),
		CFrame = at(0, 1.25, -8.2),
		Color = cfg.colGold,
		Material = Enum.Material.Metal,
	}, cabinet)

	----------------------------------------------------------------------------
	-- STADIUM STEPS: apron + basin + payout lanes. Where a piece crosses the
	-- mouth sets its multiplier (matches the server bands at |x| 0.55 / 1.9 /
	-- 3.3 — FROZEN). The center JACKPOT lane is deliberately narrower than a
	-- planet, so hitting it takes real aim. Pieces roll down the reclined
	-- header wall, toboggan the 31-degree apron between the lane blades, and
	-- drop into the enclosed basin pool through the Collector band.
	----------------------------------------------------------------------------
	local SLOT_HEADERS = {
		{ x = -4.15, w = 1.7, text = "3X", color = cfg.colTeal },
		{ x = -2.6, w = 1.4, text = "10X", color = cfg.colMagenta },
		{ x = -1.225, w = 1.35, text = "25X", color = cfg.colLane25 },
		{ x = 0, w = 1.1, text = "JACKPOT", color = cfg.colGoldHot },
		{ x = 1.225, w = 1.35, text = "25X", color = cfg.colLane25 },
		{ x = 2.6, w = 1.4, text = "10X", color = cfg.colMagenta },
		{ x = 4.15, w = 1.7, text = "3X", color = cfg.colTeal },
	}
	-- Per-lane rest tone: everything dim until a hit flashes it to full color.
	local function laneTone(slot)
		local tone = (slot.text == "JACKPOT") and 0.62 or cfg.dimTone
		return Color3.new(slot.color.R * tone, slot.color.G * tone, slot.color.B * tone)
	end

	-- Apron: 31-degree slope from the header wall's foot down to the basin
	-- (top y 2.35 @ z -5.30 -> 1.90 @ z -6.05), on a hidden riser stack.
	newPart({
		Name = "ApronWedge",
		Shape = "Wedge",
		Size = Vector3.new(9.96, 0.45, 0.75),
		CFrame = at(0, 2.125, -5.675),
		Color = cfg.colApron,
		Material = Enum.Material.Metal,
		CustomPhysicalProperties = PhysicalProperties.new(0.7, 0.05, 0, 1, 100),
	}, cabinet)
	newPart({
		Name = "ApronRiser",
		Size = Vector3.new(9.96, 0.5, 0.75),
		CFrame = at(0, 1.65, -5.675),
		Color = cfg.colDark,
		Material = Enum.Material.Metal,
	}, cabinet)
	newPart({
		Name = "ApronInfill",
		Size = Vector3.new(9.96, 0.4, 0.75),
		CFrame = at(0, 1.2, -5.675),
		Color = cfg.colDark,
	}, cabinet)

	-- Basin pool: enclosed floor + front rim wall. From the locked camera the
	-- gold bottom Mullion reads as the pool's front lip; the rim itself serves
	-- spectators and containment.
	newPart({
		Name = "BasinFloor",
		Size = Vector3.new(9.96, 0.35, 0.80),
		CFrame = at(0, 1.375, -6.45),
		Color = cfg.colBasin,
		Material = Enum.Material.Metal,
	}, cabinet)
	newPart({
		Name = "BasinSkirt",
		Size = Vector3.new(9.96, 0.2, 0.8),
		CFrame = at(0, 1.1, -6.45),
		Color = cfg.colDark,
	}, cabinet)
	newPart({
		Name = "PoolRim",
		Size = Vector3.new(9.96, 1.1, 0.22),
		CFrame = at(0, 2.0, -6.96),
		Color = cfg.colBody2,
	}, cabinet)
	newPart({
		Name = "RimSkirt",
		Size = Vector3.new(9.96, 0.45, 0.22),
		CFrame = at(0, 1.225, -6.96),
		Color = cfg.colDark,
	}, cabinet)
	-- 10.06 wide: embeds 0.03 into each cheek so its end planes never share
	-- the cheek inner faces. Spectator-side glow cap on the rim.
	newPart({
		Name = "PoolRimCap",
		Size = Vector3.new(10.06, 0.12, 0.30),
		CFrame = at(0, 2.61, -6.96),
		Color = cfg.colGold,
		Material = Enum.Material.Neon,
	}, cabinet)
	-- FRONT WALL — invisible containment, VERTICAL, in front of the pool rim.
	-- History: three generations of an invisible "catch" lived in the lip
	-- corridor (a leanable pane, a tilted fence, then a 32-degree deflector
	-- slide whose top edge sat at y 4.65 / z -5.86). Each was tuned for the
	-- piece sizes of its day; when planets grew to 1.15 the slide's top edge
	-- landed INSIDE the arc a ball sweeps as it tips off the LipRamp, and a
	-- ball came to rest cradled between the ramp edge behind it and the slab
	-- edge in front — a stable two-point rest, never scored, never rescued
	-- (owner screenshot: a full front row parked on it). The rule now: NO
	-- collidable geometry within a ball diameter of the lip's tipping arc.
	-- Containment is this wall at z -7.20 (0.04 clear of the PoolRimCap's
	-- front face at -7.11, no shared plane): overfliers hit a frictionless,
	-- dead vertical face and drop; a ball hugging it has its center at
	-- z >= -6.63, i.e. OVER the basin, so it can only fall into the pocket or
	-- onto the rim cap — both inside the Collector band. Runs y 1.0..8.0 so
	-- nothing slips under or lobs over it. frictionWeight 100 keeps Roblox's
	-- pair-averaging from lending it any grip.
	-- Split around the JACKPOT CUP: its bowl cantilevers past this plane
	-- (front wall outer face z -7.37), so the centre span is open below
	-- y 6.15 — the solid pedestal and the cup's own 0.5-tall walls are the
	-- containment there, and the 0.15 between the cup rim (6.00) and this
	-- panel's bottom (6.15) passes nothing. Above the rim the panel is the
	-- bowl's BACKBOARD: a bounce that overshoots the bowl hits it and drops
	-- back in. Side panels start at |x| 1.10, 0.03 clear of the flanks.
	-- (The Glass at z -7.45 seals everything above y 4.45 behind all three.)
	for _, wall in ipairs({
		{ size = Vector3.new(4.7, 7.0, 0.10), x = -3.45, y = 4.5 },
		{ size = Vector3.new(4.7, 7.0, 0.10), x = 3.45, y = 4.5 },
		{ size = Vector3.new(2.4, 2.0, 0.10), x = 0, y = 7.15 },
	}) do
		newPart({
			Name = "FrontWall",
			Size = wall.size,
			CFrame = at(wall.x, wall.y, -7.20),
			Transparency = 1,
			CustomPhysicalProperties = PhysicalProperties.new(0.7, 0, 0, 100, 100),
			CanQuery = false, CanTouch = false,
		}, cabinet)
	end
	newPart({
		Name = "PoolGlowPlate",
		Size = Vector3.new(9.9, 0.04, 0.85),
		CFrame = at(0, 1.575, -6.45),
		Color = cfg.colGoldDim,
		Material = Enum.Material.Neon,
		Transparency = 0.35,
		CanCollide = false, CanQuery = false, CanTouch = false,
	}, cabinet)

	-- Colored lane strips laid on the apron slope, one per payout band, plus
	-- a vertical energy film at each basin entry the pieces pierce. Both keep
	-- the LaneStrip attribute contract: the hit-flash restores to BaseColor,
	-- so rapid hits can't stick.
	local FILM_WIDTHS = { 1.5, 1.2, 1.15, 0.9, 1.15, 1.2, 1.5 }
	for laneIndex, slot in ipairs(SLOT_HEADERS) do
		local dimColor = laneTone(slot)
		local strip = newPart({
			Name = "LaneStrip" .. laneIndex,
			Size = Vector3.new(slot.w - 0.16, 0.05, 0.68),
			-- rad(-31) lays it on the apron wedge slope (atan(0.45/0.75)),
			-- floated proud along the slope normal.
			CFrame = at(slot.x, 2.151, -5.660, CFrame.Angles(math.rad(-31), 0, 0)),
			Color = dimColor,
			Material = Enum.Material.Neon,
			CanCollide = false, CanQuery = false, CanTouch = false,
		}, cabinet)
		strip:SetAttribute("BaseColor", strip.Color)
		strip:SetAttribute("FlashColor", slot.color)
		local film = newPart({
			Name = "AbsorbFilm" .. laneIndex,
			Size = Vector3.new(FILM_WIDTHS[laneIndex], 1.0, 0.05),
			CFrame = at(slot.x, 2.05, -6.15),
			Color = dimColor,
			Material = Enum.Material.Neon,
			Transparency = 0.7,
			CanCollide = false, CanQuery = false, CanTouch = false,
		}, cabinet)
		film:SetAttribute("BaseColor", film.Color)
		film:SetAttribute("FlashColor", slot.color)
	end

	-- Lane blades: slope-parallel dividers over the apron at the outer
	-- scoring band edge |x| 3.3 only. The inner pair at |x| 1.9 is GONE: with
	-- the JACKPOT CUP's flanks at |x| 1.07 they left a 0.78 channel that a
	-- 1.15 planet shed by the prow would wedge in. Lanes are bucketed by x
	-- at the Collector, so dividers were never load-bearing; the 25X channel
	-- now runs 1.07..3.25 (2.18 wide) and nothing can rest mid-mouth.
	for _, bladeX in ipairs({ -3.3, 3.3 }) do
		newPart({
			Name = "LaneBlade",
			-- Shorter + steeper (-40 vs the plates' -25): the old -31 blades'
			-- top edges converged with the plate faces into a V-groove that
			-- statically cradled planets (stress-log clusters at y~3.1-3.4,
			-- z~-6.4). Divergent edges self-clear; lane dividing needs only
			-- the low channel anyway.
			Size = Vector3.new(0.1, 0.9, 1.5),
			CFrame = at(bladeX, 2.35, -6.15, CFrame.Angles(math.rad(-40), 0, 0)),
			Color = cfg.colGoldDim,
			Material = Enum.Material.Metal,
			CustomPhysicalProperties = PhysicalProperties.new(0.7, 0.05, 0, 1, 100),
			CanQuery = false, CanTouch = false,
		}, cabinet)
		local tip = newPart({
			Name = "BladeTip",
			Size = Vector3.new(0.06, 0.06, 1.42),
			CFrame = at(bladeX, 2.85, -6.22, CFrame.Angles(math.rad(-40), 0, 0)),
			Color = cfg.colGoldDim,
			Material = Enum.Material.Neon,
		}, cabinet)
		decorate(tip)
	end
	----------------------------------------------------------------------------
	-- JACKPOT CUP — the jackpot is a HOLE, a raised gold bowl that floats
	-- ABOVE the ledge line, and only a LUCKY BOUNCE gets a piece in.
	-- V1 lived at y 3.05 in the mouth and was invisible; v2 (floor 4.20)
	-- cleared the LedgeFillet in world y but, being 1.4 studs nearer the
	-- locked camera than the lip, still drew BELOW the ledge line on screen
	-- (owner: "higher still, more like 45% more"); v3 lifted the assembly
	-- 2.10 (rim 6.80) and read too high (owner: "20% lower than now"); v4
	-- at 80% of that lift (rim 6.38) was "a bit too high still". v5 keeps
	-- 62%: floor 5.50, rim 6.00. From camEye (0, 12.2, -13.8) the front rim
	-- at z -7.37 sits at 43.9 degrees of depression and the back rim at
	-- 38.3, against the lip edge (4.80, -5.11) at 40.4 — the bowl's mouth
	-- straddles the ledge line and opens above it.
	-- HOW IT IS FED: nothing the pusher shoves off the lip can climb 1.5
	-- studs, so the tongue ramp is gone. A piece enters only by bouncing —
	-- a fresh drop hitting the front pile (ball-on-ball elasticity 0.5
	-- rebounds ~24 studs/s; a second-layer rebound starts ABOVE the rim)
	-- and hopping forward over the low back lip (6.55). Anything that
	-- overshoots meets the FrontWall backboard and drops in. The bowl's
	-- interior 1.90 x 1.50 (x by z) fits a ringed planet (1.5); the
	-- JackpotHole sensor inside (built with the payout sensors) scores lane
	-- 4 the instant a piece is in the bowl. A solid pedestal fills the
	-- centre of the basin beneath (nothing gets under — the collector's
	-- centre x would otherwise read as lane 4 for a plain drop), and the V
	-- prow runs the pedestal's full height so a centre piece coming off the
	-- lip is split into the 25X channels instead of wedging against the
	-- pedestal's back face. The JACKPOT core "card" stands above the bowl's
	-- front wall as a crown (see the core block).
	----------------------------------------------------------------------------
	local CUP_Z = -6.50 -- bowl centre; interior z -7.25 .. -5.75
	local CUP_FLOOR = 5.50 -- bowl floor top (rim 6.00, back lip 5.75)
	local cupFloor = newPart({
		Name = "JackpotCupFloor",
		Size = Vector3.new(2.14, 0.20, 1.74),
		CFrame = at(0, CUP_FLOOR - 0.10, CUP_Z),
		Color = cfg.colDark,
		Material = Enum.Material.Metal,
		CustomPhysicalProperties = PhysicalProperties.new(0.7, 0.3, 0.1, 1, 1),
	}, cabinet)
	pointLight(cupFloor, cfg.colGoldHot, 1.3, 3.5)
	-- Pedestal: y 1.55 up to the floor's underside.
	local PEDESTAL_H = (CUP_FLOOR - 0.20) - 1.55
	newPart({
		Name = "JackpotCupPedestal",
		Size = Vector3.new(2.14, PEDESTAL_H, 1.74),
		CFrame = at(0, 1.55 + PEDESTAL_H / 2, CUP_Z),
		Color = cfg.colBody2,
		Material = Enum.Material.Metal,
	}, cabinet)
	-- Bowl walls: inner faces at x +-0.95 and z -5.75 / -7.25. The BACK wall
	-- (deck side) is a low 0.25 lip a hop clears; the rest rise 0.5 so a
	-- hop that overshoots still drops back in.
	for _, wall in ipairs({
		{ name = "JackpotCupBack", size = Vector3.new(2.14, 0.25, 0.12), x = 0, y = CUP_FLOOR + 0.125, z = CUP_Z + 0.81 },
		{ name = "JackpotCupFront", size = Vector3.new(2.14, 0.5, 0.12), x = 0, y = CUP_FLOOR + 0.25, z = CUP_Z - 0.81 },
		{ name = "JackpotCupLeft", size = Vector3.new(0.12, 0.5, 1.74), x = -1.01, y = CUP_FLOOR + 0.25, z = CUP_Z },
		{ name = "JackpotCupRight", size = Vector3.new(0.12, 0.5, 1.74), x = 1.01, y = CUP_FLOOR + 0.25, z = CUP_Z },
	}) do
		newPart({
			Name = wall.name,
			Size = wall.size,
			CFrame = at(wall.x, wall.y, wall.z),
			Color = cfg.colGold,
			Material = Enum.Material.Metal,
			CustomPhysicalProperties = PhysicalProperties.new(0.7, 0, 0.2, 100, 1),
		}, cabinet)
	end
	-- Neon rim ring on top of the walls (decor).
	neonStrip(Vector3.new(2.26, 0.05, 0.10), at(0, CUP_FLOOR + 0.28, CUP_Z + 0.81), cfg.colGoldHot, cabinet, "JackpotRimBack")
	neonStrip(Vector3.new(2.26, 0.05, 0.10), at(0, CUP_FLOOR + 0.53, CUP_Z - 0.81), cfg.colGoldHot, cabinet, "JackpotRimFront")
	neonStrip(Vector3.new(0.10, 0.05, 1.74), at(-1.01, CUP_FLOOR + 0.53, CUP_Z), cfg.colGoldHot, cabinet, "JackpotRimLeft")
	neonStrip(Vector3.new(0.10, 0.05, 1.74), at(1.01, CUP_FLOOR + 0.53, CUP_Z), cfg.colGoldHot, cabinet, "JackpotRimRight")
	-- V prow: two frictionless blades meeting at (0, z -5.15) — the apex
	-- stands at the lip edge, so a centre piece is deflected while it is
	-- still sliding off the ramp — and fanning out to x +-0.92 at z -6.07
	-- against the cup's flanks. Full pedestal height (y 1.55 up to the
	-- floor top): the pedestal's back face is never exposed, at any height
	-- a piece can arrive at. Knife-edge tops, nothing balances.
	local PROW_H = CUP_FLOOR - 1.55
	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "JackpotProw",
			Size = Vector3.new(0.12, PROW_H, 1.30),
			CFrame = at(side * 0.46, 1.55 + PROW_H / 2, -5.61, CFrame.Angles(0, math.rad(side * 45), 0)),
			Color = cfg.colGold,
			Material = Enum.Material.Metal,
			CustomPhysicalProperties = PhysicalProperties.new(0.7, 0, 0, 100, 100),
			CanQuery = false, CanTouch = false,
		}, cabinet)
	end

	----------------------------------------------------------------------------
	-- Header wall: seven reclined billboard plates (25 degrees, stepped
	-- skyline) standing at the tray mouth where the locked camera actually
	-- looks. Plates are COLLIDABLE — balls roll down the faces; the normal
	-- has no x component, so a rolling piece can never hop lanes.
	----------------------------------------------------------------------------
	local RECL = CFrame.Angles(math.rad(25), 0, 0)
	local reclCos = math.cos(math.rad(25))
	local reclSin = math.sin(math.rad(25))
	-- Plate heights per lane (3X / 10X / 25X / core): tops step 3.71 -> 4.12,
	-- all under the 4.05..4.46 deck-lip sight cutoff band.
	-- Plate hinge line z -5.71: the plate planes ride ~0.18 PROUD of the
	-- backer's front face (hinge -5.31 buried them 0.22 BEHIND the backer,
	-- which hid every sign from the camera — found on the first live build).
	local HEADER_HEIGHTS = { 1.55, 1.75, 1.90, 2.00, 1.90, 1.75, 1.55 }
	local HEADER_HINGE_Z = -5.71
	newPart({
		Name = "HeaderBacker",
		Size = Vector3.new(9.96, 2.3, 0.12),
		CFrame = at(0, 3.31, -5.06, RECL),
		Color = cfg.colDark,
		Material = Enum.Material.Metal,
		CanQuery = false, CanTouch = false,
	}, cabinet)
	local jackpotPlateCF = nil
	for laneIndex, slot in ipairs(SLOT_HEADERS) do
		local h = HEADER_HEIGHTS[laneIndex]
		local pw = slot.w - 0.12
		local dimColor = laneTone(slot)
		local plateCF = at(slot.x, 2.31 + (h / 2) * reclCos, HEADER_HINGE_Z + (h / 2) * reclSin, RECL)
		local plate = newPart({
			Name = "LaneHeader" .. laneIndex,
			Size = Vector3.new(pw, h, 0.16),
			CFrame = plateCF,
			Color = cfg.colSign,
			-- Frictionless: planets must SLIDE down the 25-degree faces; at
			-- default friction the steeper-friction planets could rest here
			-- and seed a mouth dam.
			CustomPhysicalProperties = PhysicalProperties.new(0.7, 0.05, 0, 1, 100),
			CanQuery = false, CanTouch = false,
		}, cabinet)
		plate:SetAttribute("BaseColor", dimColor)
		plate:SetAttribute("FlashColor", slot.color)
		plate:SetAttribute("LaneIndex", laneIndex)
		local headerLight = pointLight(plate, slot.color, 0, 6)
		headerLight.Name = "HeaderLight"

		-- Sign face: big tier glyph + live counter (client-owned text). Lane 4
		-- keeps its HeaderGui but EMPTY: the plate is now the dark backdrop of
		-- the JACKPOT CORE assembly (built after this loop from its plateCF) —
		-- the old star TierLabel, Divider, and CountLabel are gone, and the
		-- live pot amount moved to the core's PotLabel.
		local headerGui = Instance.new("SurfaceGui")
		headerGui.Name = "HeaderGui"
		headerGui.Face = Enum.NormalId.Front
		headerGui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
		headerGui.PixelsPerStud = 128
		headerGui.LightInfluence = 0
		headerGui.Brightness = 2.5
		headerGui.Parent = plate
		if laneIndex == 4 then
			jackpotPlateCF = plateCF
		else
			local tierLabel = Instance.new("TextLabel")
			tierLabel.Name = "TierLabel"
			tierLabel.Size = UDim2.fromScale(1, 0.58)
			tierLabel.Position = UDim2.fromScale(0, 0.06)
			tierLabel.BackgroundTransparency = 1
			tierLabel.Text = slot.text
			tierLabel.TextColor3 = slot.color
			tierLabel.TextScaled = true
			tierLabel.Font = Enum.Font.FredokaOne
			tierLabel.Parent = headerGui
			local divider = Instance.new("Frame")
			divider.Name = "Divider"
			divider.Size = UDim2.fromScale(0.7, 0.02)
			divider.Position = UDim2.fromScale(0.15, 0.65)
			divider.BackgroundColor3 = dimColor
			divider.BorderSizePixel = 0
			divider.Parent = headerGui
			local countLabel = Instance.new("TextLabel")
			countLabel.Name = "CountLabel"
			countLabel.Size = UDim2.fromScale(1, 0.24)
			countLabel.Position = UDim2.fromScale(0, 0.68)
			countLabel.BackgroundTransparency = 1
			countLabel.Text = ""
			countLabel.TextColor3 = Color3.fromRGB(255, 236, 200)
			countLabel.TextScaled = true
			countLabel.Font = Enum.Font.FredokaOne
			countLabel.Parent = headerGui
		end

		-- Neon frame strips wrapping the plate edges, 0.03 proud of the sign
		-- face along its normal — flash targets that revert to the dim tone.
		local frameSpecs = {
			{ key = "T", size = Vector3.new(pw, 0.07, 0.07), off = CFrame.new(0, h / 2, -0.11) },
			{ key = "B", size = Vector3.new(pw, 0.07, 0.07), off = CFrame.new(0, -h / 2, -0.11) },
			{ key = "L", size = Vector3.new(0.07, h, 0.07), off = CFrame.new(-pw / 2, 0, -0.11) },
			{ key = "R", size = Vector3.new(0.07, h, 0.07), off = CFrame.new(pw / 2, 0, -0.11) },
		}
		for _, fs in ipairs(frameSpecs) do
			local frameStrip = newPart({
				Name = "HeaderFrame" .. fs.key .. laneIndex,
				Size = fs.size,
				CFrame = plateCF * fs.off,
				Color = dimColor,
				Material = Enum.Material.Neon,
				CanCollide = false, CanQuery = false, CanTouch = false,
			}, cabinet)
			frameStrip:SetAttribute("BaseColor", dimColor)
			frameStrip:SetAttribute("FlashColor", slot.color)
		end
		local led = newPart({
			Name = "LaneLED" .. laneIndex,
			Size = Vector3.new(slot.w - 0.24, 0.07, 0.07),
			CFrame = at(slot.x, 2.481, -5.77, RECL),
			Color = slot.color,
			Material = Enum.Material.Neon,
			Transparency = 0.4,
			CanCollide = false, CanQuery = false, CanTouch = false,
		}, cabinet)
		led:SetAttribute("BaseColor", dimColor)
		led:SetAttribute("FlashColor", slot.color)
	end

	----------------------------------------------------------------------------
	-- JACKPOT CORE — the card. Three stacked plates growing out of the cup's
	-- front rim: the JACKPOT title, an 8-pip CHARGE BAR (the jackpot window
	-- / pot-charge gauge the client lights pip by pip, strobing at 6+), and
	-- the live pot readout. The glowing planet that used to sit here is gone
	-- (owner: "remove the planet") — the CoreLight it carried now rides an
	-- invisible CoreGlowHost behind the bar, and the ember vent sits on the
	-- bar's crest, so the client's anticipation breathing, pip strobe and
	-- HIT eruption all still have their targets. No CoreOrb / CoreRing /
	-- Moonlets are built; the client guards every one of those binds. EVERY
	-- piece is decorate()'d (no collide/query/touch): the bowl's physics
	-- never sees the card.
	----------------------------------------------------------------------------
	do
		-- Anchor: a reclined billboard just beyond the bowl's front wall,
		-- its lowest edge (PotPlate bottom) ~0.09 above the 6.00 rim.
		local coreCF = at(0, 6.43, -7.15, RECL)
		-- Charge bar: a dark backing strip with 8 dim-gold notches; lit
		-- notches go Neon NEAR-WHITE (255, 244, 214) — never colGoldHot,
		-- gold-on-gold made the FULL state (the one that sells) the lowest-
		-- contrast state.
		decorate(newPart({
			Name = "ChargeBar",
			Size = Vector3.new(1.02, 0.12, 0.06),
			CFrame = coreCF * CFrame.new(0, 0.06, -0.19),
			Color = cfg.colDark,
		}, cabinet))
		for segIndex = 1, 8 do
			local seg = decorate(newPart({
				Name = "ChargeSeg" .. segIndex,
				Size = Vector3.new(0.09, 0.06, 0.06),
				CFrame = coreCF * CFrame.new(-0.42 + (segIndex - 1) * 0.12, 0.06, -0.22),
				Color = cfg.colGoldDim,
			}, cabinet))
			seg:SetAttribute("BaseColor", cfg.colGoldDim)
			seg:SetAttribute("FillColor", Color3.fromRGB(255, 244, 214))
		end
		-- Glow host: the CoreLight the client breathes (1.5 -> 3.0) during
		-- anticipation and flares to 9 on a HIT.
		local glowHost = decorate(newPart({
			Name = "CoreGlowHost",
			Size = Vector3.new(0.10, 0.10, 0.10),
			CFrame = coreCF * CFrame.new(0, 0.06, -0.30),
			Transparency = 1,
		}, cabinet))
		local coreLight = pointLight(glowHost, cfg.colGoldHot, 1.5, 3.5)
		coreLight.Name = "CoreLight"
		-- Ember vent: invisible host on the bar's crest. Both emitters build
		-- at Rate 0 — the client drives Rate 4/2 during anticipation and
		-- Emit(30)/Emit(12) on the jackpot eruption. Low speed + short
		-- lifetime keeps the plume near the bar (particles aren't parts).
		local emberVent = decorate(newPart({
			Name = "EmberVent",
			Size = Vector3.new(0.10, 0.10, 0.10),
			CFrame = coreCF * CFrame.new(0, 0.14, -0.26),
			Transparency = 1,
		}, cabinet))
		local emberEmit = Instance.new("ParticleEmitter")
		emberEmit.Name = "EmberEmit"
		emberEmit.Texture = ART.ember
		emberEmit.Color = ColorSequence.new(cfg.colGoldHot)
		emberEmit.LightEmission = 1
		emberEmit.Rate = 0
		emberEmit.Lifetime = NumberRange.new(0.35, 0.7)
		emberEmit.Speed = NumberRange.new(0.3, 0.7)
		emberEmit.Acceleration = Vector3.new(0, 0.6, 0)
		emberEmit.SpreadAngle = Vector2.new(35, 35)
		emberEmit.Rotation = NumberRange.new(-40, 40)
		emberEmit.RotSpeed = NumberRange.new(-45, 45)
		emberEmit.Size = NumberSequence.new({
			NumberSequenceKeypoint.new(0, 0.16),
			NumberSequenceKeypoint.new(1, 0),
		})
		emberEmit.Parent = emberVent
		local dustEmit = Instance.new("ParticleEmitter")
		dustEmit.Name = "DustEmit"
		dustEmit.Texture = ART.sparkDust
		dustEmit.Color = ColorSequence.new(cfg.colGold)
		dustEmit.LightEmission = 1
		dustEmit.Rate = 0
		dustEmit.Lifetime = NumberRange.new(0.4, 0.8)
		dustEmit.Speed = NumberRange.new(0.25, 0.6)
		dustEmit.Rotation = NumberRange.new(-40, 40)
		dustEmit.RotSpeed = NumberRange.new(-60, 60)
		dustEmit.Size = NumberSequence.new({
			NumberSequenceKeypoint.new(0, 0.15),
			NumberSequenceKeypoint.new(1, 0),
		})
		dustEmit.Parent = emberVent
		-- Pot readout: the live jackpot amount (client-owned text), the
		-- card's bottom plate, right above the cup's rim. Center local z
		-- -0.24 (0.11 proud of the reclined plane).
		local potPlate = decorate(newPart({
			Name = "PotPlate",
			Size = Vector3.new(1.02, 0.44, 0.10),
			CFrame = coreCF * CFrame.new(0, -0.24, -0.24),
			Color = cfg.colSign,
		}, cabinet))
		local potGui = Instance.new("SurfaceGui")
		potGui.Name = "PotGui"
		potGui.Face = Enum.NormalId.Front
		potGui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
		potGui.PixelsPerStud = 128
		potGui.LightInfluence = 0
		potGui.Brightness = 2.5
		potGui.Parent = potPlate
		local potLabel = Instance.new("TextLabel")
		potLabel.Name = "PotLabel"
		potLabel.Size = UDim2.fromScale(1, 1)
		potLabel.BackgroundTransparency = 1
		potLabel.Text = "1,750"
		potLabel.TextColor3 = cfg.colGold
		potLabel.TextScaled = true
		potLabel.Font = Enum.Font.FredokaOne
		potLabel.Parent = potGui
		-- Title plate on top.
		local coreTitle = decorate(newPart({
			Name = "CoreTitle",
			Size = Vector3.new(1.02, 0.26, 0.08),
			CFrame = coreCF * CFrame.new(0, 0.36, -0.20),
			Color = cfg.colSign,
		}, cabinet))
		local titleGui = Instance.new("SurfaceGui")
		titleGui.Name = "TitleGui"
		titleGui.Face = Enum.NormalId.Front
		titleGui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
		titleGui.PixelsPerStud = 128
		titleGui.LightInfluence = 0
		titleGui.Brightness = 2.5
		titleGui.Parent = coreTitle
		local titleLabel = Instance.new("TextLabel")
		titleLabel.Name = "TitleLabel"
		titleLabel.Size = UDim2.fromScale(1, 1)
		titleLabel.BackgroundTransparency = 1
		titleLabel.Text = "JACKPOT"
		titleLabel.TextColor3 = cfg.colGoldHot
		titleLabel.TextScaled = true
		titleLabel.Font = Enum.Font.FredokaOne
		titleLabel.Parent = titleGui
	end

	-- Crown: seals the plate-top -> deck gap. Now a frictionless WEDGE that
	-- sheds forward (33 degrees) — the old flat-top block left a restable
	-- corner sliver right at the ledge line; planets chocked against the
	-- deck face on it, dammed the mouth, and the whole ledge piled up
	-- (owner screenshot). High edge y 4.42 tucks 0.04 under the deck
	-- bottom; the exposed front is pure slope, nothing can rest.
	newPart({
		Name = "HeaderCrown",
		Shape = "Wedge",
		Size = Vector3.new(9.96, 0.40, 0.62),
		CFrame = at(0, 4.22, -4.78),
		Color = cfg.colBody2,
		CustomPhysicalProperties = PhysicalProperties.new(0.7, 0.05, 0, 1, 100),
		CanQuery = false, CanTouch = false,
	}, cabinet)

	-- Pool light: warm wash over the basin + a client-pulsed payout glow.
	local poolLightHost = newPart({
		Name = "PoolLightHost",
		Size = Vector3.new(0.4, 0.4, 0.4),
		CFrame = at(0, 2.3, -6.45),
		Transparency = 1, CanCollide = false, CanQuery = false,
	}, cabinet)
	pointLight(poolLightHost, Color3.fromRGB(255, 186, 110), 3.0, 7)
	local poolGlow = pointLight(poolLightHost, cfg.colGoldHot, 1.2, 5)
	poolGlow.Name = "PoolGlow"

	----------------------------------------------------------------------------
	-- Console: dropped and pulled toward the player so it no longer occludes
	-- the tray mouth from the locked camera (its highest edge sits below the
	-- frame bottom — the physical DROP button is for spectators; screen UI
	-- carries input).
	----------------------------------------------------------------------------

	newPart({
		Name = "ConsoleBase",
		Size = Vector3.new(10.8, 0.5, 1.6),
		CFrame = at(0, 4.30, -9.5),
		Color = cfg.colBody2,
	}, cabinet)
	-- Yaw 180: the wedge rises toward the camera.
	newPart({
		Name = "ConsoleSlope",
		Shape = "Wedge",
		Size = Vector3.new(10.8, 0.6, 1.6),
		CFrame = at(0, 4.85, -9.5, CFrame.Angles(0, math.rad(180), 0)),
		Color = cfg.colBody2,
	}, cabinet)
	-- 10.7 wide, not 10.8: its end faces must not share the slope's side planes.
	newPart({
		Name = "ConsoleTrim",
		Size = Vector3.new(10.7, 0.1, 0.14),
		CFrame = at(0, 5.12, -10.24),
		Color = cfg.colGold,
		Material = Enum.Material.Metal,
		CanCollide = false,
	}, cabinet)

	local buttonTilt = CFrame.Angles(math.rad(-20.6), 0, 0)
	newPart({
		Name = "ButtonBezel",
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(0.3, 2.7, 2.7),
		CFrame = at(0, 4.86, -9.45) * buttonTilt * FLAT,
		Color = cfg.colDark,
	}, cabinet)
	newPart({
		Name = "ButtonHalo",
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(0.14, 2.05, 2.05),
		CFrame = at(0, 4.97, -9.43) * buttonTilt * FLAT,
		Color = cfg.colMagenta,
		Material = Enum.Material.Neon,
	}, cabinet)
	local dome = newPart({
		Name = "DropButton",
		-- A flat tilted cap, not a 1.5-stud sphere: the old gold ball dome
		-- read as a giant stuck planet parked in front of the tray.
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(0.18, 1.72, 1.72),
		CFrame = at(0, 5.03, -9.42) * buttonTilt * FLAT,
		Color = cfg.colGold,
		Material = Enum.Material.Neon,
	}, cabinet)
	dome:SetAttribute("BaseSize", dome.Size)
	pointLight(dome, cfg.colGold, 1.0, 3.5)

	newPart({
		Name = "SlotPlate",
		Size = Vector3.new(1.6, 0.5, 0.12),
		CFrame = at(3.8, 4.42, -10.32),
		Color = cfg.colDark,
	}, cabinet)
	neonStrip(Vector3.new(0.5, 0.09, 0.1), at(3.8, 4.42, -10.40), cfg.colTeal)

	----------------------------------------------------------------------------
	-- Fever tube: a 20-segment comet thermometer climbing the left glass edge
	-- (floated 0.35 off the mullion plane it used to intersect). The client
	-- paints the segments from the local player's fever meter; balanced by
	-- three gold rings on the right mullion.
	----------------------------------------------------------------------------

	local feverTilt = CFrame.Angles(math.rad(10.6), 0, 0)
	for i = 1, 20 do
		local seg = newPart({
			Name = "FeverSeg" .. i,
			Size = Vector3.new(0.36, 0.30, 0.36),
			CFrame = at(-5.85, 5.2 + 0.329 * (i - 0.5), -8.43 + 0.0616 * (i - 0.5), feverTilt),
			Color = cfg.colDark,
			CanCollide = false, CanQuery = false, CanTouch = false,
		}, fx)
		seg:SetAttribute("FeverIndex", i)
		seg:SetAttribute("BaseColor", cfg.colDark)
		if i <= 14 then
			seg:SetAttribute("FillColor", cfg.colFeverFill)
		elseif i <= 19 then
			seg:SetAttribute("FillColor", cfg.colFeverHot)
		else
			seg:SetAttribute("FillColor", cfg.colFeverPeak)
		end
	end
	newPart({
		Name = "FeverTube",
		Size = Vector3.new(0.44, 6.8, 0.44),
		CFrame = at(-5.85, 8.5, -7.81, feverTilt),
		Color = cfg.colDark,
		Transparency = 0.78,
		CanCollide = false, CanQuery = false, CanTouch = false,
	}, fx)
	local feverComet = newPart({
		Name = "FeverComet",
		Shape = Enum.PartType.Ball,
		Size = Vector3.new(0.8, 0.8, 0.8),
		CFrame = at(-5.85, 12.18, -7.14),
		Color = cfg.colGoldHot,
		Material = Enum.Material.Neon,
		CanCollide = false, CanQuery = false, CanTouch = false,
	}, fx)
	pointLight(feverComet, cfg.colGoldHot, 1.5, 6)
	local feverPlate = newPart({
		Name = "FeverPlate",
		Size = Vector3.new(1.6, 0.5, 0.12),
		CFrame = at(-5.85, 5.05, -8.55),
		Color = cfg.colDark,
		CanCollide = false, CanQuery = false, CanTouch = false,
	}, fx)
	local feverGui = Instance.new("SurfaceGui")
	feverGui.Name = "FeverGui"
	feverGui.Face = Enum.NormalId.Front
	feverGui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
	feverGui.PixelsPerStud = 128
	feverGui.LightInfluence = 0
	feverGui.Brightness = 2.5
	feverGui.Parent = feverPlate
	local feverLabel = Instance.new("TextLabel")
	feverLabel.Name = "Label"
	feverLabel.Size = UDim2.fromScale(1, 1)
	feverLabel.BackgroundTransparency = 1
	feverLabel.Text = "METEOR"
	feverLabel.TextColor3 = cfg.colGoldHot
	feverLabel.TextScaled = true
	feverLabel.Font = Enum.Font.FredokaOne
	feverLabel.Parent = feverGui
	for _, ringY in ipairs({ { y = 6.0, z = -8.27 }, { y = 8.5, z = -7.81 }, { y = 11.0, z = -7.35 } }) do
		local ring = newPart({
			Name = "MullionRing",
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.12, 0.5, 0.5),
			CFrame = at(5.85, ringY.y, ringY.z, CFrame.Angles(math.rad(10.6), 0, math.rad(90))),
			Color = cfg.colGold,
			Material = Enum.Material.Metal,
		}, fx)
		decorate(ring)
	end

	----------------------------------------------------------------------------
	-- Frame: pillars, side shells, back tower, roof
	----------------------------------------------------------------------------

	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "Pillar",
			Size = Vector3.new(1.1, 12.0, 1.1),
			CFrame = at(side * 6.3, 7.0, -7.9),
			Color = cfg.colBody,
		}, cabinet)
		neonStrip(Vector3.new(0.18, 10.4, 0.18), at(side * 6.3, 7.2, -8.5), cfg.colTeal)
		newPart({
			Name = "PillarCap",
			Size = Vector3.new(1.3, 0.35, 1.3),
			CFrame = at(side * 6.3, 12.45, -7.9),
			Color = cfg.colGold,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, cabinet)

		newPart({
			Name = "SideShell",
			Size = Vector3.new(0.5, 11.6, 16.4),
			CFrame = at(side * 6.95, 6.8, -0.5),
			Color = cfg.colBody,
		}, cabinet)
		-- 16.32 deep: end planes clear of the SideShell's ends at z -8.7 / 7.7.
		newPart({
			Name = "SidePanelLow",
			Size = Vector3.new(0.9, 4.5, 16.32),
			CFrame = at(side * 6.95, 3.25, -0.5),
			Color = cfg.colDark,
		}, cabinet)
		newPart({
			Name = "Pinstripe",
			Size = Vector3.new(0.12, 0.28, 16.0),
			CFrame = at(side * 7.26, 5.9, -0.5),
			Color = cfg.colGold,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, cabinet)
	end

	newPart({
		Name = "BackTower",
		Size = Vector3.new(13.9, 11.6, 1.2),
		CFrame = at(0, 6.8, 7.4),
		Color = cfg.colBody,
	}, cabinet)

	----------------------------------------------------------------------------
	-- Jackpot screen (screen proud of bezel: primitives have no holes)
	----------------------------------------------------------------------------

	newPart({
		Name = "ScreenBezel",
		Size = Vector3.new(9.3, 3.8, 0.25),
		CFrame = at(0, 9.2, 6.7),
		Color = cfg.colDark,
	}, cabinet)
	local screen = newPart({
		Name = "JackpotScreen",
		Size = Vector3.new(8.5, 3.0, 0.3),
		CFrame = at(0, 9.2, 6.55),
		Color = Color3.fromRGB(6, 7, 12),
	}, cabinet)
	-- Frame strips 0.015 clear of the bezel's front plane.
	neonStrip(Vector3.new(8.9, 0.12, 0.12), at(0, 7.55, 6.50), cfg.colMagenta)
	neonStrip(Vector3.new(8.9, 0.12, 0.12), at(0, 10.85, 6.50), cfg.colMagenta)

	local screenGui = Instance.new("SurfaceGui")
	screenGui.Name = "JackpotGui"
	screenGui.Face = Enum.NormalId.Front
	screenGui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
	screenGui.PixelsPerStud = 64
	screenGui.LightInfluence = 0
	screenGui.Brightness = 2
	screenGui.Parent = screen

	local screenTitle = Instance.new("TextLabel")
	screenTitle.Name = "Title"
	screenTitle.Size = UDim2.fromScale(1, 0.3)
	screenTitle.Position = UDim2.fromScale(0, 0.05)
	screenTitle.BackgroundTransparency = 1
	screenTitle.Text = "J A C K P O T"
	screenTitle.TextColor3 = cfg.colMagenta
	screenTitle.TextScaled = true
	screenTitle.Font = Enum.Font.Arcade
	screenTitle.Parent = screenGui

	local screenValue = Instance.new("TextLabel")
	screenValue.Name = "Value"
	screenValue.Size = UDim2.fromScale(1, 0.55)
	screenValue.Position = UDim2.fromScale(0, 0.38)
	screenValue.BackgroundTransparency = 1
	screenValue.Text = "1,750"
	screenValue.TextColor3 = cfg.colGold
	screenValue.TextScaled = true
	screenValue.Font = Enum.Font.Arcade
	screenValue.Parent = screenGui

	local jackpotValue = Instance.new("IntValue")
	jackpotValue.Name = "JackpotValue"
	jackpotValue.Value = 1750
	jackpotValue.Parent = model

	for _, side in ipairs({ -1, 1 }) do
		neonStrip(Vector3.new(0.5, 6.5, 0.2), at(side * 5.9, 8.3, 6.7), cfg.colMagenta)
	end

	-- Vent slats on the tower band visible just above the pusher's top edge.
	for _, ventY in ipairs({ 6.85, 7.10 }) do
		newPart({
			Name = "TowerVent",
			Size = Vector3.new(8.4, 0.12, 0.06),
			CFrame = at(0, ventY, 6.77),
			Color = cfg.colDark,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, cabinet)
	end

	----------------------------------------------------------------------------
	-- Roof
	----------------------------------------------------------------------------

	newPart({
		Name = "Roof",
		Size = Vector3.new(15.0, 0.7, 16.2),
		CFrame = at(0, 13.0, -0.4),
		Color = cfg.colBody,
	}, cabinet)
	newPart({
		Name = "RoofCap",
		Shape = "Wedge",
		Size = Vector3.new(15.0, 1.5, 6.0),
		CFrame = at(0, 14.1, -5.3),
		Color = cfg.colBody,
	}, cabinet)
	newPart({
		Name = "RoofTrim",
		Size = Vector3.new(15.2, 0.3, 0.4),
		CFrame = at(0, 12.75, -8.35),
		Color = cfg.colGold,
		Material = Enum.Material.Metal,
		CanCollide = false,
	}, cabinet)

	local roofLight = Instance.new("SurfaceLight")
	roofLight.Face = Enum.NormalId.Bottom
	roofLight.Color = Color3.fromRGB(170, 220, 235)
	roofLight.Brightness = 0.6
	roofLight.Range = 12
	roofLight.Parent = cabinet:FindFirstChild("Roof")

	for _, side in ipairs({ -1, 1 }) do
		local host = newPart({
			Name = "PileSpotHost",
			Size = Vector3.new(0.3, 0.3, 0.3),
			CFrame = CFrame.lookAt(at(side * 3.4, 12.2, -3.4).Position, at(0, 5, -1.5).Position),
			Transparency = 1, CanCollide = false, CanQuery = false,
		}, cabinet)
		local light = Instance.new("SpotLight")
		light.Face = Enum.NormalId.Front
		light.Color = Color3.fromRGB(255, 206, 130)
		light.Brightness = 1.5
		light.Angle = 50
		light.Range = 14
		light.Shadows = false
		light.Parent = host
	end

	----------------------------------------------------------------------------
	-- Coin dropper: a roof-hung gantry over the drop zone. The rail and
	-- hangers are static; the carriage head (DropperHead + its child parts)
	-- is slid along the rail CLIENT-side to track the player's aim, so coins
	-- visibly fall from its glowing mouth. Sized and placed so the mouth's
	-- sightline from the locked camera clears the jackpot screen's top edge.
	----------------------------------------------------------------------------

	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "DropperHanger",
			Size = Vector3.new(0.18, 0.55, 0.18),
			CFrame = at(side * 5.0, 12.38, -2.4),
			Color = cfg.colDark,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, cabinet)
	end
	newPart({
		Name = "DropperRail",
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(11.6, 0.28, 0.28),
		CFrame = at(0, 12.15, -2.4),
		Color = cfg.colGold,
		Material = Enum.Material.Metal,
		CanCollide = false,
	}, cabinet)

	-- The carriage is a rig: every part carries a build-stamped rest offset
	-- ("RO") and drivers write ABSOLUTE CFrames (rest * motion * RO) each
	-- frame. Two prior schemes both tore: live-CFrame offset capture (a bind
	-- racing a half-streamed machine captured displaced "rest" poses) and
	-- Model:PivotTo (moves by delta, so a part streaming in AFTER animation
	-- starts is stranded at whatever offset the rig held when it arrived).
	-- Absolute writes re-seat every part every frame — self-healing.
	local carriage = Instance.new("Model")
	carriage.Name = "DropperCarriage"
	carriage.Parent = fx
	local dropperHead = newPart({
		Name = "DropperHead",
		Size = Vector3.new(1.5, 0.9, 1.1),
		CFrame = at(0, 11.8, -2.4),
		Color = cfg.colBody2,
		Material = Enum.Material.Metal,
		CanCollide = false, CanQuery = false, CanTouch = false,
	}, carriage)
	newPart({
		Name = "DropperNeck",
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(0.5, 1.0, 1.0),
		CFrame = at(0, 11.15, -2.4) * FLAT,
		Color = cfg.colDark,
		Material = Enum.Material.Metal,
		CanCollide = false, CanQuery = false, CanTouch = false,
	}, carriage)
	local mouth = newPart({
		Name = "DropperMouth",
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(0.12, 1.32, 1.32),
		CFrame = at(0, 10.86, -2.4) * FLAT,
		Color = cfg.colGold,
		Material = Enum.Material.Neon,
		CanCollide = false, CanQuery = false, CanTouch = false,
	}, carriage)
	pointLight(mouth, cfg.colGoldHot, 1.2, 5)
	-- Teal hairline across the carriage face so it pops against the dark band.
	newPart({
		Name = "DropperTrim",
		Size = Vector3.new(1.3, 0.08, 0.08),
		CFrame = at(0, 11.8, -2.99),
		Color = cfg.colTeal,
		Material = Enum.Material.Neon,
		CanCollide = false, CanQuery = false, CanTouch = false,
	}, carriage)
	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "DropperWheel",
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.14, 0.44, 0.44),
			CFrame = at(side * 0.62, 12.15, -2.4),
			Color = cfg.colGoldHot,
			Material = Enum.Material.Metal,
			CanCollide = false, CanQuery = false, CanTouch = false,
		}, carriage)
	end
	carriage.PrimaryPart = dropperHead
	carriage:SetAttribute("RestCF", dropperHead.CFrame)
	stampRig(carriage, dropperHead.CFrame)

	----------------------------------------------------------------------------
	-- Sloped front glass with gold mullions
	----------------------------------------------------------------------------

	-- Leans back: top edge at ~(12.59, -6.70), bottom edge at ~(6.21, -7.90).
	-- SmoothPlastic, not Glass: SurfaceGuis behind Glass vanish at high quality.
	-- The glass runs DOWN to ~4.5 so its bottom rail sits below the sight
	-- line to the payout mouth — the old bottom rail at 6.21 was a gold bar
	-- straight across the badges from the locked camera.
	local glassTilt = CFrame.Angles(math.rad(10.6), 0, 0)
	newPart({
		Name = "Glass",
		Size = Vector3.new(11.8, 8.2, 0.22),
		CFrame = at(0, 8.55, -7.45, glassTilt),
		Color = Color3.fromRGB(160, 215, 230),
		Material = Enum.Material.SmoothPlastic,
		Transparency = 0.92,
		Reflectance = 0.03,
	}, cabinet)
	newPart({
		Name = "Mullion",
		Size = Vector3.new(11.9, 0.24, 0.3),
		CFrame = at(0, 12.59, -6.70, glassTilt),
		Color = cfg.colGold, Material = Enum.Material.Metal, CanCollide = false,
	}, cabinet)
	newPart({
		Name = "Mullion",
		Size = Vector3.new(11.9, 0.24, 0.3),
		CFrame = at(0, 4.53, -8.21, glassTilt),
		Color = cfg.colGold, Material = Enum.Material.Metal, CanCollide = false,
	}, cabinet)
	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "Mullion",
			Size = Vector3.new(0.24, 8.3, 0.3),
			CFrame = at(side * 5.85, 8.55, -7.45, glassTilt),
			Color = cfg.colGold, Material = Enum.Material.Metal, CanCollide = false,
		}, cabinet)
	end

	-- Magenta LED "payline" chase riding just above the lowered bottom rail.
	for i = 0, 7 do
		neonStrip(Vector3.new(1.25, 0.15, 0.15), at(-5.25 + i * 1.5, 4.72, -8.15),
			cfg.colMagenta, fx, "TrayLed")
	end

	----------------------------------------------------------------------------
	-- Marquee
	----------------------------------------------------------------------------

	local marquee = newPart({
		Name = "Marquee",
		Size = Vector3.new(13.0, 3.2, 0.9),
		CFrame = at(0, 15.0, -7.9),
		Color = cfg.colBody2,
	}, cabinet)
	newPart({
		Name = "MarqueeBase",
		Size = Vector3.new(13.0, 0.5, 2.2),
		CFrame = at(0, 13.3, -7.6),
		Color = cfg.colBody2,
	}, cabinet)

	local marqueeGui = Instance.new("SurfaceGui")
	marqueeGui.Name = "MarqueeGui"
	marqueeGui.Face = Enum.NormalId.Front
	marqueeGui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
	marqueeGui.PixelsPerStud = 64
	marqueeGui.LightInfluence = 0
	marqueeGui.Brightness = 3
	marqueeGui.Parent = marquee
	local title = cfg.marqueeTitle
	local letterCount = #title
	for i = 1, letterCount do
		local letter = Instance.new("TextLabel")
		letter.Name = "L" .. i
		letter.LayoutOrder = i
		letter.Size = UDim2.fromScale(1 / letterCount, 0.8)
		letter.Position = UDim2.fromScale((i - 1) / letterCount, 0.1)
		letter.BackgroundTransparency = 1
		letter.Text = string.sub(title, i, i)
		letter.TextColor3 = cfg.colGold
		letter.TextScaled = true
		letter.Font = Enum.Font.FredokaOne
		letter.Parent = marqueeGui
	end

	neonStrip(Vector3.new(13.6, 0.35, 1.0), at(0, 16.75, -7.9), cfg.colGold)
	neonStrip(Vector3.new(13.6, 0.35, 1.0), at(0, 13.25, -7.9), cfg.colGold)
	-- Ringed planet orbs bookending the marquee (replace the old drum caps).
	for _, orb in ipairs({
		{ key = "L", side = -1, orbColor = cfg.colTeal, ringColor = cfg.colGold },
		{ key = "R", side = 1, orbColor = cfg.colMagenta, ringColor = cfg.colTeal },
	}) do
		local planetOrb = newPart({
			Name = "PlanetOrb" .. orb.key,
			Shape = Enum.PartType.Ball,
			Size = Vector3.new(2.3, 2.3, 2.3),
			CFrame = at(orb.side * 6.05, 15.0, -7.9),
			Color = orb.orbColor,
		}, cabinet)
		decorate(planetOrb)
		local orbRing = newPart({
			Name = "OrbRing" .. orb.key,
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.16, 3.4, 3.4),
			CFrame = at(orb.side * 6.05, 15.0, -7.9, CFrame.Angles(math.rad(66), 0, 0)) * FLAT,
			Color = orb.ringColor,
			Material = Enum.Material.Neon,
		}, cabinet)
		decorate(orbRing)
	end
	neonStrip(Vector3.new(12.6, 0.12, 0.12), at(0, 13.62, -8.28), cfg.colGoldDim,
		cabinet, "MarqueeUnderglow")

	for i = 0, 8 do
		local lit = (i % 2 == 0)
		newPart({
			Name = "Bulb" .. i,
			Shape = Enum.PartType.Ball,
			Size = Vector3.new(0.45, 0.45, 0.45),
			CFrame = at(-5.6 + i * 1.4, 16.75, -8.5),
			Color = lit and cfg.colGoldHot or cfg.colGoldDim,
			Material = lit and Enum.Material.Neon or Enum.Material.SmoothPlastic,
			CanCollide = false,
		}, fx)
	end

	pointLight(marquee, cfg.colGold, 1.0, 7)

	----------------------------------------------------------------------------
	-- Playfield
	----------------------------------------------------------------------------

	local fieldLightHost = newPart({
		Name = "FieldLightHost",
		Size = Vector3.new(0.4, 0.4, 0.4),
		CFrame = at(0, 9.5, -2),
		Transparency = 1, CanCollide = false, CanQuery = false,
	}, playfield)
	pointLight(fieldLightHost, Color3.fromRGB(255, 200, 120), 1.3, 12)

	-- Deck 0.54 thick: its underside (4.46) sits clear of the body top (4.49
	-- is above it — wait, body top is 4.495; deck bottom 4.46 tucks beneath,
	-- leaving no shared plane). Low friction so the pile slides.
	-- Deck front face PULLED BACK to z -4.4: the frictionless LipRamp is
	-- now the floor's actual last 0.7 studs (the first ramp attempt sat
	-- UNDER the full-length deck and pieces never touched it — probe).
	newPart({
		Name = "Deck",
		Size = Vector3.new(12, 0.54, 10.9),
		CFrame = at(0, 4.73, 1.05),
		Color = cfg.colDeck,
		Material = cfg.deckMaterial,
		CustomPhysicalProperties = PhysicalProperties.new(1, 0.25, 0, 1, 1),
	}, playfield)

	-- Lane hairlines (embedded 0.01, proud 0.05: crosses the deck plane, never
	-- coincides with it).
	for _, laneX in ipairs({ -2, 2 }) do
		newPart({
			Name = "LaneLine",
			Size = Vector3.new(0.08, 0.06, 10.2),
			CFrame = at(laneX, 5.02, 1.05),
			Color = cfg.colDeckLane,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, playfield)
	end

	-- Gold fillet under the ledge lip (the old under-ledge chute wall is gone —
	-- the header wall's backer and crown seal that region now). Bottom 4.43
	-- clears the HeaderCrown top 4.42 by 0.01.
	local ledgeFillet = newPart({
		Name = "LedgeFillet",
		Size = Vector3.new(11.9, 0.14, 0.14),
		CFrame = at(0, 4.50, -5.06),
		Color = cfg.colGold,
		Material = Enum.Material.Metal,
	}, cabinet)
	decorate(ledgeFillet)

	-- 11.96 wide: end planes clear of the deck's side faces at x ±6.
	newPart({
		Name = "LedgeEdge",
		Size = Vector3.new(11.96, 0.14, 0.24),
		CFrame = at(0, 4.80, -5.08, CFrame.Angles(math.rad(-16), 0, 0)),
		Color = cfg.colGold,
		Material = Enum.Material.Neon,
		CanCollide = false,
	}, playfield)
	-- LIP RAMP — the anti-perch fix. Overhanging balls at the lip go to
	-- SLEEP (Roblox physics) and never re-check their tipping torque, so a
	-- whole front row could sit frozen half over the edge until the pusher
	-- woke it (owner capture). This frictionless ~13° downhill strip covers
	-- the deck's last 0.58: nothing can REST on it, so nothing can sleep on
	-- it — anything the pusher delivers to the lip slides off on its own.
	-- Its back edge meets the deck top flush at z -4.42; the pusher head
	-- rides the deck plane above it and never touches it.
	-- The ramp IS the floor's last strip (deck now ends at z -4.4): back
	-- edge flush with the deck top, descending 16° past z -5.1 into the
	-- drop. frictionWeight 100: Roblox AVERAGES pair friction by weight —
	-- against a 0.4-friction coin a plain low-friction surface still
	-- combined to ~0.21 (= tan 13°) and pieces rested anyway (live probe).
	-- Weight 100 makes the ramp's ZERO dominate: combined ~0.004, nothing
	-- can hold on, so nothing can sleep here — anything the pusher delivers
	-- to the lip slides off on its own.
	-- Angle is NEGATIVE: +16 built the slope UPHILL toward the mouth and dug
	-- a piece-trapping valley at the deck junction (live orientation probe:
	-- back 4.80 / front 5.00). -16 reads back 5.00 / front 4.80 — downhill.
	newPart({
		Name = "LipRamp",
		Size = Vector3.new(11.96, 0.12, 0.72),
		CFrame = at(0, 4.842, -4.75, CFrame.Angles(math.rad(-16), 0, 0)),
		Color = cfg.colDeck,
		Material = cfg.deckMaterial,
		CustomPhysicalProperties = PhysicalProperties.new(0.7, 0, 0, 100, 100),
		CanQuery = false, CanTouch = false,
	}, playfield)
	-- Scoring-lane divider fins at the lip (lane boundaries x ±3.4 / ±0.8).
	-- Purely visual: the ramp surface descends across their span while the fin
	-- tops stay level, so a collidable fin tip pokes above the ramp near the
	-- front and catches on-edge coin rims (lane bucketing is by X at collection,
	-- so collision here adds nothing but a snag point).
	for _, finX in ipairs({ -3.4, -0.8, 0.8, 3.4 }) do
		newPart({
			Name = "LaneFin",
			Size = Vector3.new(0.08, 0.24, 0.6),
			CFrame = at(finX, 4.78, -5.06),
			Color = cfg.colGold,
			Material = Enum.Material.Metal,
			CanCollide = false, CanQuery = false, CanTouch = false,
		}, playfield)
	end
	-- Win line floats 0.015 above the deck plane — never flush (z-fight rule).
	neonStrip(Vector3.new(12, 0.06, 0.15), at(0, 5.045, -3.6), cfg.colMagenta, fx, "WinLine")

	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "FieldWall",
			Size = Vector3.new(0.5, 2.6, 11.5),
			CFrame = at(side * 6.25, 6.3, 0.75),
			Color = cfg.colBody2,
		}, playfield)
		-- Segment bottoms 0.01 above the wall's top plane (7.60): never flush.
		for i = 0, 9 do
			neonStrip(Vector3.new(0.28, 0.22, 1.0), at(side * 6.25, 7.72, 5.85 - i * 1.135),
				cfg.colTeal, fx, (side == -1 and "ChaseL" or "ChaseR") .. i)
		end
	end

	----------------------------------------------------------------------------
	----------------------------------------------------------------------------
	-- Back hood — THE jam fix, not decoration.
	--
	-- The pusher's back face sits at z 5.6 retracted and z 2.6 extended, but
	-- the deck runs back to z 6.5. Every stroke therefore opened a trough up
	-- to 3.9 studs wide behind the block, coins fell into it, and nothing
	-- could ever reach them again — they piled up and stalled the servo (the
	-- "pusher goes forward and gets stuck" report). This solid hood roofs
	-- that entire region, so a coin can never enter it in the first place.
	--
	-- Clearances: hood underside 7.00 vs pusher rib tops 6.85; hood top 7.70
	-- passes under the jackpot screen's sightline from the locked camera
	-- (ray to the screen's lowest text is y 7.92 at this depth), so it hides
	-- nothing the player reads. Front edge z 1.2 leaves the drop path clear.
	----------------------------------------------------------------------------
	newPart({
		Name = "BackHood",
		Size = Vector3.new(11.9, 0.7, 5.15),
		CFrame = at(0, 7.35, 3.775),
		Color = cfg.colBody2,
		Material = Enum.Material.Metal,
	}, playfield)
	-- Gold brow on the hood's leading edge so it reads as machined housing.
	newPart({
		Name = "BackHoodTrim",
		Size = Vector3.new(11.8, 0.1, 0.12),
		CFrame = at(0, 7.05, 1.14),
		Color = cfg.colGold,
		Material = Enum.Material.Metal,
		CanCollide = false,
	}, playfield)

	-- Scraper blade at the hood's mouth — the second half of the jam fix.
	-- Bottom at 6.92: the pusher's ribs (top 6.85) and lip (top 6.89) glide
	-- underneath, but a coin lying on the pusher top (7.01+) physically
	-- cannot pass, so the ribs ratchet top-riding coins forward and off the
	-- block instead of dragging them under the hood. Real machines have
	-- exactly this part. Anchored, so nothing can ever wedge the drive.
	newPart({
		Name = "Scraper",
		Size = Vector3.new(11.8, 0.43, 0.25),
		CFrame = at(0, 7.135, 1.365),
		Color = cfg.colGold,
		Material = Enum.Material.Metal,
	}, playfield)

	-- Pusher: machined block, FULLY KINEMATIC. Every part is anchored and the
	-- drive (see start()) moves the whole set along a sinusoid that is a pure
	-- function of server time. A physics-coupled drive (servo/motor) is
	-- force-limited and therefore stalls, twitches, and loses amplitude as
	-- the pile grows; an anchored block moved 0.07 studs/frame cannot be
	-- slowed by any load, ever. Overlays float proud of their host faces —
	-- nothing coplanar, no shimmer.
	----------------------------------------------------------------------------

	local pusherTopY = cfg.deckTop + 0.05 + cfg.pusherSize.Y      -- 6.65
	local pusherMidY = cfg.deckTop + 0.05 + cfg.pusherSize.Y / 2  -- 5.85
	local frontZ = cfg.pusherRestZ - cfg.pusherSize.Z / 2         -- 1.4

	-- TWIN VAULT PRESS: two half-width presses running half a cycle out of
	-- phase — the left drives while the right retracts, so the machine is
	-- always in motion somewhere and aiming left vs right is a real choice.
	-- A static gold divider blade seals the center seam (0.05 clearance per
	-- side: nothing can enter), outer skirts seal the wall gaps, and the
	-- shared hood + scraper guarantees survive unchanged because both halves
	-- sweep the same envelope the single block did.
	--
	-- Grouping contract: each half is a MODEL named PressL/PressR under
	-- Playfield, PrimaryPart = its body, with attributes PhaseShift (0 / 0.5),
	-- RestCF (rest pose stamped at build), PartCount, and a per-part "RO"
	-- rest offset. The driver in start() and the client mirror both write
	-- ABSOLUTE part CFrames (rest * motion * RO) every frame — no offset
	-- capture and no PivotTo deltas, so neither a mid-motion bind nor a
	-- late-streaming part can ever tear the assembly.

	local HALF_W = 5.53
	-- Top deck style, chosen from theme-lab previews:
	-- 1 machined sled · 2 chamfer hood · 3 tron deck · 4 stepped armor ·
	-- 5 glass crown
	local PRESS_TOP_STYLE = 3
	for _, half in ipairs({
		{ key = "L", side = -1, shift = 0 },
		{ key = "R", side = 1, shift = 0.5 },
	}) do
		local cx = half.side * 2.935
		local press = Instance.new("Model")
		press.Name = "Press" .. half.key
		press.Parent = playfield
		local function joinGroup(part, collides)
			part.CanTouch = false
			part.CanQuery = false
			if collides == false then part.CanCollide = false end
			return part
		end

		local body = joinGroup(newPart({
			Name = "Pusher" .. half.key,
			Size = Vector3.new(HALF_W, cfg.pusherSize.Y, cfg.pusherSize.Z),
			CFrame = at(cx, pusherMidY, cfg.pusherRestZ),
			Color = cfg.colBody2,
			Material = Enum.Material.Metal,
		}, press))

		-- Top deck. Height budget: nothing may rise above 6.90 (scraper
		-- underside is 6.92, and the scraper guarantee — any top-riding coin
		-- tops out over 7.0 and gets swept — holds for ALL of these because
		-- the deck stays low). Styles are previewed in the theme lab; the
		-- shipped style is PRESS_TOP_STYLE below.
		if PRESS_TOP_STYLE == 1 then
			-- Machined sled: dark inset deck, one polished lengthwise spine
			-- running WITH the stroke direction, thin gold nosings on the
			-- outer edges. Minimal, lets the pile and lighting star.
			joinGroup(newPart({
				Name = "PusherTop" .. half.key,
				Size = Vector3.new(HALF_W - 0.23, 0.12, 3.9),
				CFrame = at(cx, pusherTopY + 0.05, cfg.pusherRestZ),
				Color = cfg.colDark,
				Material = Enum.Material.Metal,
			}, press))
			joinGroup(newPart({
				Name = "PusherSpine" .. half.key,
				Size = Vector3.new(0.34, 0.06, 3.7),
				CFrame = at(cx, pusherTopY + 0.13, cfg.pusherRestZ),
				Color = cfg.colGold,
				Material = Enum.Material.Metal,
				Reflectance = 0.14,
			}, press))
			for _, edge in ipairs({ -1, 1 }) do
				joinGroup(newPart({
					Name = "PusherNose" .. half.key .. (edge == -1 and "A" or "B"),
					Size = Vector3.new(0.08, 0.05, 3.7),
					CFrame = at(cx + edge * (HALF_W / 2 - 0.2), pusherTopY + 0.125, cfg.pusherRestZ),
					Color = cfg.colGold,
					Material = Enum.Material.Metal,
				}, press))
			end
		elseif PRESS_TOP_STYLE == 2 then
			-- Chamfer hood: two low-pitched slabs meeting at a dark center
			-- ridge — an armored, coin-shedding rooftop.
			for _, wing in ipairs({ { z = -0.95, tilt = -5 }, { z = 0.95, tilt = 5 } }) do
				joinGroup(newPart({
					Name = "PusherHood" .. half.key .. (wing.tilt < 0 and "F" or "B"),
					Size = Vector3.new(HALF_W - 0.23, 0.08, 2.0),
					CFrame = at(cx, pusherTopY + 0.07, cfg.pusherRestZ + wing.z)
						* CFrame.Angles(math.rad(wing.tilt), 0, 0),
					Color = cfg.colBody2,
					Material = Enum.Material.Metal,
				}, press))
			end
			joinGroup(newPart({
				Name = "PusherRidge" .. half.key,
				Size = Vector3.new(HALF_W - 0.33, 0.07, 0.24),
				CFrame = at(cx, pusherTopY + 0.185, cfg.pusherRestZ),
				Color = cfg.colDark,
				Material = Enum.Material.Metal,
			}, press))
		elseif PRESS_TOP_STYLE == 3 then
			-- Tron deck: dark slab with two lengthwise accent light channels
			-- guarded by gold micro-rails — glow lines pointing at the drop.
			joinGroup(newPart({
				Name = "PusherTop" .. half.key,
				Size = Vector3.new(HALF_W - 0.23, 0.12, 3.9),
				CFrame = at(cx, pusherTopY + 0.05, cfg.pusherRestZ),
				Color = cfg.colDark,
				Material = Enum.Material.Metal,
			}, press))
			for _, lane in ipairs({ -1.2, 1.2 }) do
				joinGroup(newPart({
					Name = "PusherChannel" .. half.key .. (lane < 0 and "A" or "B"),
					Size = Vector3.new(0.1, 0.04, 3.7),
					CFrame = at(cx + lane, pusherTopY + 0.125, cfg.pusherRestZ),
					Color = cfg.colTeal,
					Material = Enum.Material.Neon,
				}, press), false)
				for _, edge in ipairs({ -0.14, 0.14 }) do
					joinGroup(newPart({
						Name = "PusherRail" .. half.key .. (lane < 0 and "A" or "B") .. (edge < 0 and "1" or "2"),
						Size = Vector3.new(0.06, 0.06, 3.7),
						CFrame = at(cx + lane + edge, pusherTopY + 0.125, cfg.pusherRestZ),
						Color = cfg.colGold,
						Material = Enum.Material.Metal,
					}, press))
				end
				-- Brighter, wider glow tick where the channel meets the lip,
				-- so the light visibly pours off the leading edge in motion.
				joinGroup(newPart({
					Name = "PusherTick" .. half.key .. (lane < 0 and "A" or "B"),
					Size = Vector3.new(0.18, 0.05, 0.16),
					CFrame = at(cx + lane, pusherTopY + 0.13, cfg.pusherRestZ - 1.86),
					Color = cfg.colTeal,
					Material = Enum.Material.Neon,
				}, press), false)
			end
		elseif PRESS_TOP_STYLE == 4 then
			-- Stepped armor: three plates rising toward the back with gold
			-- nosing on every step edge — heavy industrial sled.
			for stepIndex, step in ipairs({
				{ z = 1.3, y = 6.70, h = 0.10, color = cfg.colBody2 },
				{ z = 0, y = 6.73, h = 0.16, color = cfg.colDark },
				{ z = -1.3, y = 6.76, h = 0.22, color = cfg.colBody2 },
			}) do
				joinGroup(newPart({
					Name = "PusherStep" .. half.key .. stepIndex,
					Size = Vector3.new(HALF_W - 0.23, step.h, 1.3),
					CFrame = at(cx, step.y, cfg.pusherRestZ + step.z),
					Color = step.color,
					Material = Enum.Material.Metal,
				}, press))
				-- Nosing hugs the step's front-top corner: proud 0.02 in z so
				-- its face never shares the step's plane, sunk into the top.
				joinGroup(newPart({
					Name = "PusherNosing" .. half.key .. stepIndex,
					Size = Vector3.new(HALF_W - 0.33, 0.06, 0.1),
					CFrame = at(cx, step.y + step.h / 2 - 0.01, cfg.pusherRestZ + step.z + 0.62),
					Color = cfg.colGold,
					Material = Enum.Material.Metal,
				}, press))
			end
		else
			-- Glass crown: dark deck with a lit glass vitrine bar down the
			-- center — a jewel-case spine with a glowing core.
			joinGroup(newPart({
				Name = "PusherTop" .. half.key,
				Size = Vector3.new(HALF_W - 0.23, 0.12, 3.9),
				CFrame = at(cx, pusherTopY + 0.05, cfg.pusherRestZ),
				Color = cfg.colDark,
				Material = Enum.Material.Metal,
			}, press))
			joinGroup(newPart({
				Name = "PusherCore" .. half.key,
				Size = Vector3.new(0.12, 0.04, 3.3),
				CFrame = at(cx, pusherTopY + 0.13, cfg.pusherRestZ),
				Color = cfg.colTeal,
				Material = Enum.Material.Neon,
			}, press), false)
			joinGroup(newPart({
				Name = "PusherVitrine" .. half.key,
				Size = Vector3.new(0.5, 0.14, 3.5),
				CFrame = at(cx, pusherTopY + 0.17, cfg.pusherRestZ),
				Color = cfg.colTeal,
				Material = Enum.Material.Glass,
				Transparency = 0.45,
			}, press), false)
			for _, endZ in ipairs({ -1.85, 1.85 }) do
				joinGroup(newPart({
					Name = "PusherCap" .. half.key .. (endZ < 0 and "A" or "B"),
					Size = Vector3.new(0.54, 0.12, 0.2),
					CFrame = at(cx, pusherTopY + 0.18, cfg.pusherRestZ + endZ),
					Color = cfg.colGold,
					Material = Enum.Material.Metal,
				}, press))
			end
		end

		-- Face: dark machined plate, framed in gold, with a stamping medallion
		-- at its center (the client flashes it at this half's stroke apex).
		joinGroup(newPart({
			Name = "PusherPlate" .. half.key,
			Size = Vector3.new(HALF_W - 0.04, 1.5, 0.25),
			CFrame = at(cx, pusherMidY, frontZ - 0.1),
			Color = cfg.colDark,
			Material = Enum.Material.Metal,
		}, press))
		joinGroup(newPart({
			Name = "PusherBrow" .. half.key,
			Size = Vector3.new(HALF_W - 0.12, 0.12, 0.1),
			CFrame = at(cx, pusherMidY + 0.62, frontZ - 0.24),
			Color = cfg.colGold,
			Material = Enum.Material.Metal,
		}, press), false)
		joinGroup(newPart({
			Name = "PusherBase" .. half.key,
			Size = Vector3.new(HALF_W - 0.12, 0.12, 0.1),
			CFrame = at(cx, pusherMidY - 0.62, frontZ - 0.24),
			Color = cfg.colGold,
			Material = Enum.Material.Metal,
		}, press), false)
		for _, edge in ipairs({ -1, 1 }) do
			joinGroup(newPart({
				Name = "PusherJamb" .. half.key .. (edge == -1 and "A" or "B"),
				Size = Vector3.new(0.12, 1.12, 0.1),
				CFrame = at(cx + edge * (HALF_W / 2 - 0.12), pusherMidY, frontZ - 0.24),
				Color = cfg.colGold,
				Material = Enum.Material.Metal,
			}, press), false)
		end
		joinGroup(newPart({
			Name = "PusherMedal" .. half.key,
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.1, 0.72, 0.72),
			CFrame = at(cx, pusherMidY, frontZ - 0.27) * CFrame.Angles(0, math.rad(90), 0),
			Color = cfg.colGoldHot,
			Material = Enum.Material.Metal,
			Reflectance = 0.1,
		}, press), false)
		joinGroup(newPart({
			Name = "PusherStamp" .. half.key,
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.06, 0.95, 0.95),
			CFrame = at(cx, pusherMidY, frontZ - 0.25) * CFrame.Angles(0, math.rad(90), 0),
			Color = cfg.colMagenta,
			Material = Enum.Material.Neon,
		}, press), false)

		-- Slim neon lip on the leading edge — a lit blade, not a bar. Real
		-- pushers read as a plain plate with one bright edge; the fat
		-- 0.28-square bar dominated the whole silhouette.
		local lip = neonStrip(
			Vector3.new(HALF_W - 0.08, 0.13, 0.13),
			at(cx, pusherTopY + 0.055, frontZ - 0.055),
			cfg.colTeal, press)
		lip.Name = "PusherLip" .. half.key
		pointLight(lip, cfg.colTeal, 0.75, 4.5)
		joinGroup(lip, false)

		-- Outer skirt: load-bearing. The wall gap is 0.3 and a coin is 0.25;
		-- at 0.24 thick the remaining slit is 0.06 — nothing fits.
		joinGroup(newPart({
			Name = "PusherSkirt" .. half.key,
			Size = Vector3.new(0.24, 1.5, 3.9),
			CFrame = at(half.side * (cfg.pusherSize.X / 2 + 0.12), pusherMidY, cfg.pusherRestZ),
			Color = cfg.colDark,
			Material = Enum.Material.Metal,
		}, press))

		-- The whole half is driven as one rig: absolute per-part writes
		-- against this stamped rest pose, on server and client alike.
		press.PrimaryPart = body
		press:SetAttribute("PhaseShift", half.shift)
		press:SetAttribute("RestCF", body.CFrame)
		stampRig(press, body.CFrame)
	end

	-- Static divider blade sealing the center seam across the full sweep
	-- (z -1.7..6.6). Tops out 0.01 below the hood's underside plane.
	newPart({
		Name = "DividerBlade",
		Size = Vector3.new(0.24, 2.0, 8.3),
		CFrame = at(0, 5.99, 2.45),
		Color = cfg.colGold,
		Material = Enum.Material.Metal,
	}, playfield)
	neonStrip(Vector3.new(0.1, 1.8, 0.08), at(0, 6.0, -1.76), cfg.colMagenta, playfield, "DividerTip")

	-- Dim gold rails skimming the field walls above the press skirts. Top at
	-- 6.97: 0.03 under the BackHood underside (7.00), clear of the skirt top
	-- band (6.60) and the scraper ends.
	for _, side in ipairs({ -1, 1 }) do
		local pressRail = neonStrip(Vector3.new(0.06, 0.18, 10.9),
			at(side * 5.96, 6.88, 0.75), cfg.colGoldDim, cabinet, "PressRail")
		pressRail.Transparency = 0.35
	end

	-- Piston rods: static rails the halves slide along (z 2.6..8.0 keeps them
	-- buried through the full stroke; far end flush inside the tower), with
	-- gold entry sleeves at the tower face.
	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "PistonRod",
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(5.4, 0.35, 0.35),
			CFrame = at(side * 4.5, 6.2, 5.3) * CFrame.Angles(0, math.rad(90), 0),
			Color = cfg.colGold,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, playfield)
		newPart({
			Name = "RodSleeve",
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.55, 0.6, 0.6),
			CFrame = at(side * 4.5, 6.2, 6.55) * CFrame.Angles(0, math.rad(90), 0),
			Color = cfg.colGoldDim,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, playfield)
	end

	-- Twin pressure dials on the hood face: gold rim ring proud of the hood
	-- front plane, darker face disc proud of the rim.
	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "HoodGauge",
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.12, 0.46, 0.46),
			CFrame = at(side * 3.4, 7.45, 1.13) * CFrame.Angles(0, math.rad(90), 0),
			Color = cfg.colGold,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, playfield)
		newPart({
			Name = "HoodGaugeFace",
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.06, 0.32, 0.32),
			CFrame = at(side * 3.4, 7.45, 1.06) * CFrame.Angles(0, math.rad(90), 0),
			Color = cfg.colDark,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, playfield)
	end

	-- (No constraint drive. The kinematic driver lives in PusherMachine.start.)

	----------------------------------------------------------------------------
	-- Payout sensors and effects hosts
	----------------------------------------------------------------------------

	-- Band y 1.5..2.95, z -7.9..-5.2: EVERY rest surface in the mouth puts a
	-- 1.15 ball's underside inside it — apron top 1.90..2.35, basin pocket
	-- (ball center 2.13), a ball bridging two LaneBlade tops (bottom ~2.6),
	-- a ball perched on the PoolRimCap (bottom 2.67), flat coin top 1.80.
	-- Scored lane is always the landing channel, and nothing rests unscored.
	-- The old 2.4 top left rim-cap perches and blade bridges un-collected;
	-- 2.95 still keeps the header-wall roll visible before the collect.
	newPart({
		Name = "Collector",
		Size = Vector3.new(10, 1.45, 2.7),
		CFrame = at(0, 2.225, -6.55),
		Transparency = 1, CanCollide = false,
	}, payout)
	-- JACKPOT HOLE: the sensor inside the cup bowl (x +-0.9, y 5.53..5.98,
	-- z -7.20..-5.80 — mirrors CUP_FLOOR 5.50 in the cabinet block). A piece
	-- that lands in the bowl touches it on arrival; the server scores it as
	-- lane 4 (the jackpot lane) regardless of x.
	newPart({
		Name = "JackpotHole",
		Size = Vector3.new(1.8, 0.45, 1.4),
		CFrame = at(0, 5.755, -6.50),
		Transparency = 1, CanCollide = false,
	}, payout)

	-- Just below the dropper's mouth (10.86): coins visibly fall FROM it.
	newPart({
		Name = "DropZone",
		Size = Vector3.new(11, 0.4, 1.5),
		CFrame = at(0, cfg.deckTop + 5.55, -2.4),
		Transparency = 1, CanCollide = false,
	}, playfield)

	local sparkleHost = newPart({
		Name = "SparkleHost",
		Size = Vector3.new(9, 0.4, 7),
		CFrame = at(0, cfg.deckTop + 2.2, -1),
		Transparency = 1, CanCollide = false, CanQuery = false,
	}, fx)
	local sparkle = Instance.new("ParticleEmitter")
	sparkle.Name = "PileSparkle"
	sparkle.Texture = "rbxasset://textures/particles/sparkles_main.dds"
	sparkle.Color = ColorSequence.new(cfg.sparkleColor)
	sparkle.LightEmission = 1
	sparkle.Size = NumberSequence.new({
		NumberSequenceKeypoint.new(0, 0.12),
		NumberSequenceKeypoint.new(0.5, 0.3),
		NumberSequenceKeypoint.new(1, 0),
	})
	sparkle.Transparency = NumberSequence.new({
		NumberSequenceKeypoint.new(0, 1),
		NumberSequenceKeypoint.new(0.3, 0.25),
		NumberSequenceKeypoint.new(1, 1),
	})
	sparkle.Rate = cfg.sparkleRate
	sparkle.Lifetime = NumberRange.new(1.2, 2.2)
	sparkle.Speed = NumberRange.new(0.2, 0.6)
	sparkle.SpreadAngle = Vector2.new(180, 180)
	sparkle.Parent = sparkleHost

	-- Raised above the mullion-blocked depression band so payout bursts erupt
	-- inside the locked camera's view.
	local burstHost = newPart({
		Name = "PayBurstHost",
		Size = Vector3.new(6, 0.4, 1.5),
		CFrame = at(0, 2.55, -6.35),
		Transparency = 1, CanCollide = false, CanQuery = false,
	}, fx)
	local burst = Instance.new("ParticleEmitter")
	burst.Name = "PayBurst"
	burst.Texture = "rbxasset://textures/particles/sparkles_main.dds"
	burst.Color = ColorSequence.new(cfg.colGold, cfg.colGoldHot)
	burst.LightEmission = 1
	burst.Size = NumberSequence.new({
		NumberSequenceKeypoint.new(0, 0.35),
		NumberSequenceKeypoint.new(1, 0),
	})
	burst.Rate = 0
	burst.Lifetime = NumberRange.new(0.4, 0.8)
	burst.Speed = NumberRange.new(2, 5)
	burst.SpreadAngle = Vector2.new(60, 60)
	burst.Enabled = false
	burst.Parent = burstHost

	local clink = Instance.new("Sound")
	clink.Name = "Clink"
	clink.SoundId = "rbxasset://sounds/electronicpingshort.wav"
	clink.Volume = 0.35
	clink.RollOffMaxDistance = 60
	clink.Parent = burstHost

	-- (No anchored decor coins: the old tray floor they dressed is gone, and
	-- the basin pool is fed by real scored pieces. The Decor folder remains
	-- for clearDecor compatibility.)

	----------------------------------------------------------------------------
	-- Mounts
	----------------------------------------------------------------------------

	local camPos = originCFrame * CFrame.new(cfg.camEye)
	local camLook = originCFrame * CFrame.new(cfg.camLook)
	local cameraMount = newPart({
		Name = "CameraMount",
		Size = Vector3.new(0.4, 0.4, 0.4),
		Transparency = 1, CanCollide = false, CanQuery = false,
	}, folders.Mounts)
	cameraMount.CFrame = CFrame.lookAt(camPos.Position, camLook.Position)

	local standPart = newPart({
		Name = "StandHere",
		Size = Vector3.new(4, 0.2, 3),
		CFrame = at(0, 0.1, -10.6),
		Transparency = 1, CanCollide = false,
	}, folders.Mounts)

	local prompt = Instance.new("ProximityPrompt")
	prompt.Name = "PlayPrompt"
	prompt.ActionText = "Play"
	prompt.ObjectText = "Coin Pusher"
	prompt.HoldDuration = 0
	prompt.MaxActivationDistance = 12
	prompt.RequiresLineOfSight = false
	prompt.Parent = standPart

	----------------------------------------------------------------------------
	-- Templates (hidden below the world; spawn* clones them)
	----------------------------------------------------------------------------

	local coinTemplate = makeCoin(cfg, at(0, -50, 0), true, folders.Templates)
	coinTemplate.CanCollide = false
	local capsuleTemplate = makeCapsule(cfg, at(0, -54, 0), true, folders.Templates)
	capsuleTemplate.CanCollide = false

	model:SetAttribute("Version", "2.1")
	model:SetAttribute("ThemeId", (resolveTheme(theme).id or "NeonNights"))
	model.PrimaryPart = cabinet:FindFirstChild("Body")
	return model
end

--------------------------------------------------------------------------------
-- Live pile seeding — REAL coins, deterministically layered so the pile reads
-- settled even before physics touches it. Server-side only (physics).
--------------------------------------------------------------------------------

function PusherMachine.seedPile(model, coinCount, capsuleCount)
	coinCount = coinCount or 32
	capsuleCount = capsuleCount or 1
	-- Recover the machine's origin frame from the deck (deck sits at
	-- origin * (0, 4.73, 0.75)) and its theme from the model attribute.
	local deck = model.Playfield:FindFirstChild("Deck")
	if not deck then return end
	local origin = deck.CFrame * CFrame.new(0, -4.73, -0.75)
	local cfg = DEFAULTS
	local themeId = model:GetAttribute("ThemeId")
	for _, t in ipairs(PusherMachine.Themes) do
		if t.id == themeId then
			cfg = shallowMerge(DEFAULTS, t)
			break
		end
	end

	local rng = makeRng(1337)
	local placedXZ = {}
	local spawned = 0
	local attempts = 0
	-- Three loose layers, front-biased like a real pile. Min spacing slightly
	-- over a coin diameter per layer so nothing starts interpenetrating.
	local layers = { { y = 5.16, n = math.floor(coinCount * 0.55), zMin = -4.4, zMax = 0.55 },
		{ y = 5.44, n = math.floor(coinCount * 0.30), zMin = -4.2, zMax = 0.0 },
		{ y = 5.72, n = coinCount, zMin = -4.0, zMax = -0.8 } }
	for layerIndex, layer in ipairs(layers) do
		local layerPlaced = {}
		local target = (layerIndex == 3) and (coinCount - spawned) or layer.n
		local placedInLayer = 0
		attempts = 0
		while placedInLayer < target and attempts < 3000 do
			attempts = attempts + 1
			local x = (rng() * 2 - 1) * 4.9
			local z = layer.zMin + rng() * (layer.zMax - layer.zMin)
			local ok = true
			for _, p in ipairs(layerPlaced) do
				if (p[1] - x) ^ 2 + (p[2] - z) ^ 2 < 1.35 then ok = false break end
			end
			if ok then
				table.insert(layerPlaced, { x, z })
				local cf = origin * CFrame.new(x, layer.y, z)
					* CFrame.Angles(0, rng() * math.pi, (rng() - 0.5) * math.rad(8)) * FLAT
				local coin = makeCoin(cfg, cf, false, model)
				pcall(function() coin:SetNetworkOwner(nil) end)
				placedInLayer = placedInLayer + 1
				spawned = spawned + 1
			end
		end
	end

	for i = 1, capsuleCount do
		local cf = origin * CFrame.new((rng() * 2 - 1) * 3.4, 5.75, -3.4 + rng() * 1.2)
		local cap = makeCapsule(cfg, cf, false, model)
		pcall(function() cap:SetNetworkOwner(nil) end)
	end
end

--------------------------------------------------------------------------------
-- Runtime
--------------------------------------------------------------------------------

-- Kinematic pusher drive. Position is a pure function of the shared server
-- clock: z = rest - stroke * (0.5 - 0.5*cos(2pi*t/cycle)). A sinusoid eases
-- at both ends like a crank-driven block, and because every pusher part is
-- anchored, NO amount of pile load, spam-dropping, or physics throttling can
-- slow, stall, or twitch it. The client re-derives the identical curve from
-- the DriveT0 attribute for frame-smooth rendering.
function PusherMachine.start(model)
	local playfield = model:FindFirstChild("Playfield")
	if not playfield then
		return function() end
	end

	-- Collect the press models stamped by the builder.
	local driven = {}
	for _, press in ipairs(playfield:GetChildren()) do
		if press:IsA("Model") and press.Name:sub(1, 5) == "Press" then
			local rest = press:GetAttribute("RestCF")
			if rest then
				local parts = {}
				for _, piece in ipairs(press:GetChildren()) do
					local ro = piece:GetAttribute("RO")
					if ro then table.insert(parts, { part = piece, ro = ro }) end
				end
				table.insert(driven, {
					rest = rest,
					shift = press:GetAttribute("PhaseShift") or 0,
					parts = parts,
				})
			end
		end
	end
	if #driven == 0 then
		return function() end
	end

	local stroke = PusherMachine.Config.stroke
	local cycle = PusherMachine.Config.cycleSeconds
	local t0 = workspace:GetServerTimeNow()
	model:SetAttribute("DriveT0", t0)
	-- Pusher speed contract (protocol §3.1): the server rebases these three
	-- attributes on every speed change (SpeedCycles0 = cycle progress at the
	-- change, SpeedMultAt = change time), so the sinusoid speeds up or slows
	-- down with NO phase jump. The client press mirror reads the same attrs.
	model:SetAttribute("SpeedMult", 1)
	model:SetAttribute("SpeedMultAt", t0)
	model:SetAttribute("SpeedCycles0", 0)

	local conn
	conn = game:GetService("RunService").Heartbeat:Connect(function()
		if not model.Parent then
			conn:Disconnect()
			return
		end
		-- Read the speed attrs fresh every frame: fever can change them at
		-- any moment, and the curve must follow the rebased timeline.
		local now = workspace:GetServerTimeNow()
		local mult = model:GetAttribute("SpeedMult") or 1
		local multAt = model:GetAttribute("SpeedMultAt") or t0
		local cycles0 = model:GetAttribute("SpeedCycles0") or 0
		local cyclePos = cycles0 + (now - multAt) * mult / cycle
		for _, group in ipairs(driven) do
			local theta = 2 * math.pi * (cyclePos + group.shift)
			local phase = 0.5 - 0.5 * math.cos(theta)
			local target = group.rest * CFrame.new(0, 0, -stroke * phase)
			-- Surface-velocity hint (the anchored-conveyor pattern): CFrame
			-- writes do NO overlap resolution, so without this the solver
			-- only reacts AFTER a ball has sunk into the advancing face and
			-- then pops it out. An anchored part can carry a nonzero
			-- AssemblyLinearVelocity; stamping the true kinematic velocity
			-- (scaled by SpeedMult) makes contacts resolve against the
			-- moving face immediately.
			local vel = group.rest:VectorToWorldSpace(
				Vector3.new(0, 0, -stroke * math.pi * math.sin(theta) * mult / cycle))
			for _, it in ipairs(group.parts) do
				it.part.CFrame = target * it.ro
				it.part.AssemblyLinearVelocity = vel
			end
		end
	end)

	return function()
		conn:Disconnect()
	end
end

local function spawnFrom(model, templateName, xOffset, rot)
	local template = model.Templates:FindFirstChild(templateName)
	if not template then return nil end

	local dropZone = model.Playfield.DropZone
	local item = template:Clone()
	item.Name = templateName
	item.CanCollide = true
	item.Parent = model
	-- Unanchor FIRST so the welds form one assembly at the template location,
	-- THEN teleport: setting CFrame on a joined assembly moves it rigidly.
	-- (Teleporting while anchored would strand the welded dress parts.)
	for _, sub in ipairs(item:GetChildren()) do
		if sub.ClassName == "Part" then
			sub.Anchored = false
		end
	end
	item.Anchored = false
	item.CFrame = dropZone.CFrame * CFrame.new(xOffset or 0, 0, 0) * (rot or CFrame.new())
	pcall(function() item:SetNetworkOwner(nil) end)
	return item
end

-- Drop a token in at the back. xOffset lets the player aim left/right.
function PusherMachine.dropCoin(model, xOffset)
	return spawnFrom(model, "Coin", xOffset, FLAT)
end

function PusherMachine.dropCapsule(model, xOffset)
	return spawnFrom(model, "Capsule", xOffset, nil)
end

-- Remove the static tray decor.
function PusherMachine.clearDecor(model)
	local decor = model:FindFirstChild("Decor")
	if decor then decor:Destroy() end
end

-- Tween a player's camera into the cabinet. Call from a LocalScript.
function PusherMachine.focusCamera(model, camera, seconds)
	camera.CameraType = Enum.CameraType.Scriptable
	local info = TweenInfo.new(seconds or 0.6, Enum.EasingStyle.Quad, Enum.EasingDirection.Out)
	TweenService:Create(camera, info, { CFrame = model.Mounts.CameraMount.CFrame }):Play()
end

function PusherMachine.releaseCamera(camera)
	camera.CameraType = Enum.CameraType.Custom
end

PusherMachine.Config = DEFAULTS

-- Themed palette for a zone index (1-based, clamped). The client rebinds its
-- CFG to this on every zone switch so cosmetics repaint in the ZONE's colors,
-- not zone 1's ambers (PusherMachine.Config stays the raw DEFAULTS). Same
-- resolution the builder uses: the zone index IS the Themes index.
function PusherMachine.themedConfigFor(zoneIndex)
	if type(zoneIndex) ~= "number" or not PusherMachine.Themes[zoneIndex] then
		zoneIndex = 1
	end
	return themedConfig(zoneIndex)
end

return PusherMachine
