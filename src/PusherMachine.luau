--!nonstrict
--[[
	PusherMachine — builds a complete coin pusher cabinet out of Roblox primitives.

	No mesh assets, no uploads, no modelling tool. The art direction is geometry +
	materials: Neon strips for trim, Glass for the front, Metal for the deck, and
	a disciplined palette. A decorative coin pile ships with the build so the
	machine looks alive before any gameplay exists (delete via clearDecor).

	Usage (ModuleScript in ReplicatedStorage or ServerStorage):

		local PusherMachine = require(path.to.PusherMachine)
		local machine = PusherMachine.build(CFrame.new(0, 0, 0))
		machine.Parent = workspace
		PusherMachine.start(machine)          -- begin the push cycle
		PusherMachine.dropCoin(machine)       -- drop a token in
		PusherMachine.clearDecor(machine)     -- remove the static display pile

	Coordinate convention: origin on the floor at the centre of the cabinet.
	The player stands on the -Z side; the pusher travels toward -Z and coins
	fall off the front ledge into the lit tray.
]]

local TweenService = game:GetService("TweenService")

local PusherMachine = {}

--------------------------------------------------------------------------------
-- Configuration
--------------------------------------------------------------------------------

local CFG = {
	-- Playfield.
	deckTop     = 5.0,    -- Y of the deck surface coins rest on
	deckBack    = 6.5,    -- Z where the deck ends (back of the machine)
	ledgeZ      = -5.0,   -- Z of the payout ledge; coins past this fall

	-- Pusher.
	pusherSize   = Vector3.new(11.4, 1.6, 4.2),
	pusherRestZ  = 3.5,   -- Z of the pusher block centre when fully retracted
	stroke       = 3,     -- how far forward it travels
	cycleSeconds = 2.2,   -- one full out-and-back cycle
	pusherForce  = 1e6,   -- servo force; must dwarf the weight of the pile

	-- Coins.
	coinDiameter  = 1.1,
	coinThickness = 0.25,
	capsuleSize   = 1.6,

	-- Palette. Deep navy cabinet, gold money accents, teal neon, magenta prize.
	colBody    = Color3.fromRGB(24, 28, 44),
	colBody2   = Color3.fromRGB(34, 40, 62),   -- lighter panel tone
	colDark    = Color3.fromRGB(15, 17, 26),   -- kick plate / screens
	colDeck    = Color3.fromRGB(58, 66, 92),   -- steel-blue playfield
	colGold    = Color3.fromRGB(255, 190, 60),
	colGoldDim = Color3.fromRGB(122, 92, 34),  -- unlit bulbs
	colTeal    = Color3.fromRGB(64, 230, 214),
	colMagenta = Color3.fromRGB(236, 88, 214),
}

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
-- (or a button puck) must be rolled 90 degrees about Z to sit flat.
local FLAT = CFrame.Angles(0, 0, math.rad(90))

-- Deterministic pseudo-random for the decorative pile, so every build (and every
-- offline render) produces the same machine.
local function makeRng(seed)
	local state = seed
	return function()
		state = (state * 1103515245 + 12345) % 2147483648
		return state / 2147483648
	end
end

--------------------------------------------------------------------------------
-- Build
--------------------------------------------------------------------------------

function PusherMachine.build(originCFrame, name)
	originCFrame = originCFrame or CFrame.new()

	local model = Instance.new("Model")
	model.Name = name or "CoinPusher"

	local folders = {}
	for _, folderName in ipairs({ "Cabinet", "Playfield", "Payout", "Mounts", "Decor", "Templates" }) do
		local folder = Instance.new("Folder")
		folder.Name = folderName
		folder.Parent = model
		folders[folderName] = folder
	end
	local cabinet, playfield, payout = folders.Cabinet, folders.Playfield, folders.Payout

	local function at(x, y, z, rot)
		local cf = originCFrame * CFrame.new(x, y, z)
		if rot then cf = cf * rot end
		return cf
	end

	local function neonStrip(size, cf, color, parent)
		return newPart({
			Name = "Trim", Size = size, CFrame = cf, Color = color,
			Material = Enum.Material.Neon, CanCollide = false,
		}, parent or cabinet)
	end

	----------------------------------------------------------------------------
	-- Plinth and under-deck body
	----------------------------------------------------------------------------

	-- Kick plinth the whole cabinet sits on, in the darkest tone.
	newPart({
		Name = "Plinth",
		Size = Vector3.new(14.8, 1.0, 16.6),
		CFrame = at(0, 0.5, -0.3),
		Color = CFG.colDark,
	}, cabinet)

	-- Teal underglow skirting the plinth. This is most of the "arcade floor" look.
	neonStrip(Vector3.new(14.2, 0.18, 0.18), at(0, 0.14, -8.7), CFG.colTeal)
	for _, side in ipairs({ -1, 1 }) do
		neonStrip(Vector3.new(0.18, 0.18, 16.2), at(side * 7.45, 0.14, -0.3), CFG.colTeal)
	end

	-- Body block under the deck, behind the tray.
	newPart({
		Name = "Body",
		Size = Vector3.new(13.4, 3.5, 13.0),
		CFrame = at(0, 2.75, 1.5),
		Color = CFG.colBody,
	}, cabinet)

	----------------------------------------------------------------------------
	-- Prize tray (the lit mouth under the ledge)
	----------------------------------------------------------------------------

	-- Sloped tray floor: WedgePart is full height at its +Z side, so the default
	-- orientation already slopes down toward the player.
	newPart({
		Name = "TrayFloor",
		Shape = "Wedge",
		Size = Vector3.new(9.6, 0.9, 3.0),
		CFrame = at(0, 1.45, -6.5),
		Color = CFG.colBody2,
		Material = Enum.Material.Metal,
	}, cabinet)

	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "TrayCheek",
			Size = Vector3.new(1.4, 3.2, 3.2),
			CFrame = at(side * 5.5, 2.6, -6.6),
			Color = CFG.colBody,
		}, cabinet)
	end

	-- Gold lip across the tray front, and a header over the mouth with teal trim.
	newPart({
		Name = "TrayLip",
		Size = Vector3.new(10.4, 0.5, 0.4),
		CFrame = at(0, 1.25, -8.2),
		Color = CFG.colGold,
		Material = Enum.Material.Metal,
	}, cabinet)
	newPart({
		Name = "TrayHeader",
		Size = Vector3.new(10.4, 0.8, 0.6),
		CFrame = at(0, 4.3, -8.0),
		Color = CFG.colBody,
	}, cabinet)
	neonStrip(Vector3.new(9.8, 0.15, 0.15), at(0, 3.85, -8.1), CFG.colGold)

	-- Warm light spilling out of the tray mouth.
	local trayLightHost = newPart({
		Name = "TrayLightHost",
		Size = Vector3.new(0.4, 0.4, 0.4),
		CFrame = at(0, 2.9, -6.4),
		Transparency = 1, CanCollide = false, CanQuery = false,
	}, cabinet)
	local trayLight = Instance.new("PointLight")
	trayLight.Color = Color3.fromRGB(255, 186, 110)
	trayLight.Brightness = 1.6
	trayLight.Range = 7
	trayLight.Parent = trayLightHost

	----------------------------------------------------------------------------
	-- Control deck (button panel the player stands at)
	----------------------------------------------------------------------------

	newPart({
		Name = "ControlBase",
		Size = Vector3.new(10.8, 0.7, 2.4),
		CFrame = at(0, 5.05, -8.3),
		Color = CFG.colBody2,
	}, cabinet)
	newPart({
		Name = "ControlSlope",
		Shape = "Wedge",
		Size = Vector3.new(10.8, 0.8, 2.4),
		CFrame = at(0, 5.8, -8.3),
		Color = CFG.colBody2,
	}, cabinet)

	-- Big glowing DROP button, tilted to match the panel slope.
	local buttonTilt = CFrame.Angles(math.rad(-18), 0, 0)
	newPart({
		Name = "ButtonRing",
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(0.3, 2.3, 2.3),
		CFrame = at(0, 5.92, -8.15) * buttonTilt * FLAT,
		Color = CFG.colDark,
	}, cabinet)
	newPart({
		Name = "DropButton",
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(0.55, 1.8, 1.8),
		CFrame = at(0, 6.06, -8.15) * buttonTilt * FLAT,
		Color = CFG.colGold,
		Material = Enum.Material.Neon,
	}, cabinet)

	-- Token slot detail on the flat front of the panel.
	newPart({
		Name = "SlotPlate",
		Size = Vector3.new(1.6, 0.5, 0.12),
		CFrame = at(3.8, 5.05, -9.56),
		Color = CFG.colDark,
	}, cabinet)
	neonStrip(Vector3.new(0.5, 0.09, 0.1), at(3.8, 5.05, -9.63), CFG.colTeal)

	----------------------------------------------------------------------------
	-- Frame: pillars, side shells, back tower, roof
	----------------------------------------------------------------------------

	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "Pillar",
			Size = Vector3.new(1.1, 12.0, 1.1),
			CFrame = at(side * 6.3, 7.0, -7.9),
			Color = CFG.colBody,
		}, cabinet)
		neonStrip(Vector3.new(0.18, 10.4, 0.18), at(side * 6.3, 7.2, -8.5), CFG.colTeal)

		newPart({
			Name = "SideShell",
			Size = Vector3.new(0.5, 11.6, 14.6),
			CFrame = at(side * 6.95, 6.8, 0.4),
			Color = CFG.colBody,
		}, cabinet)
		-- Two-tone lower panel + gold pinstripe running the side.
		newPart({
			Name = "SidePanelLow",
			Size = Vector3.new(0.54, 3.6, 14.6),
			CFrame = at(side * 6.96, 2.8, 0.4),
			Color = CFG.colDark,
		}, cabinet)
		newPart({
			Name = "Pinstripe",
			Size = Vector3.new(0.12, 0.28, 14.2),
			CFrame = at(side * 7.28, 5.1, 0.4),
			Color = CFG.colGold,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, cabinet)
	end

	newPart({
		Name = "BackTower",
		Size = Vector3.new(13.9, 11.6, 1.2),
		CFrame = at(0, 6.8, 7.4),
		Color = CFG.colBody,
	}, cabinet)

	-- Jackpot display inside the cabinet, facing the player through the glass.
	local screen = newPart({
		Name = "JackpotScreen",
		Size = Vector3.new(8.5, 3.0, 0.3),
		CFrame = at(0, 9.2, 6.7),
		Color = CFG.colDark,
	}, cabinet)
	local screenGui = Instance.new("SurfaceGui")
	screenGui.Face = Enum.NormalId.Front
	screenGui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
	screenGui.PixelsPerStud = 64
	screenGui.LightInfluence = 0
	screenGui.Brightness = 2
	screenGui.Parent = screen
	local screenText = Instance.new("TextLabel")
	screenText.Size = UDim2.fromScale(1, 1)
	screenText.BackgroundTransparency = 1
	screenText.Text = "JACKPOT  1,750x"
	screenText.TextColor3 = CFG.colGold
	screenText.TextScaled = true
	screenText.Font = Enum.Font.Arcade
	screenText.Parent = screenGui

	-- Magenta accent columns flanking the screen.
	for _, side in ipairs({ -1, 1 }) do
		neonStrip(Vector3.new(0.25, 6.5, 0.15), at(side * 5.9, 8.3, 6.75), CFG.colMagenta)
	end

	newPart({
		Name = "Roof",
		Size = Vector3.new(15.0, 0.7, 16.2),
		CFrame = at(0, 13.0, -0.4),
		Color = CFG.colBody,
	}, cabinet)
	newPart({
		Name = "RoofTrim",
		Size = Vector3.new(15.2, 0.3, 0.4),
		CFrame = at(0, 12.75, -8.35),
		Color = CFG.colGold,
		Material = Enum.Material.Metal,
		CanCollide = false,
	}, cabinet)

	-- Cool wash over the playfield from under the roof.
	local roofLight = Instance.new("SurfaceLight")
	roofLight.Face = Enum.NormalId.Bottom
	roofLight.Color = Color3.fromRGB(220, 235, 255)
	roofLight.Brightness = 1.1
	roofLight.Range = 12
	roofLight.Parent = cabinet:FindFirstChild("Roof")

	----------------------------------------------------------------------------
	-- Sloped front glass
	----------------------------------------------------------------------------

	-- Leans back ~10.6 degrees: bottom edge at the control deck, top at the roof.
	newPart({
		Name = "Glass",
		Size = Vector3.new(11.8, 6.5, 0.22),
		CFrame = at(0, 9.4, -7.3, CFrame.Angles(math.rad(10.6), 0, 0)),
		Color = Color3.fromRGB(170, 225, 235),
		Material = Enum.Material.Glass,
		Transparency = 0.72,
	}, cabinet)

	----------------------------------------------------------------------------
	-- Marquee
	----------------------------------------------------------------------------

	local marquee = newPart({
		Name = "Marquee",
		Size = Vector3.new(13.0, 3.2, 0.9),
		CFrame = at(0, 15.0, -7.9),
		Color = CFG.colBody2,
	}, cabinet)
	local marqueeGui = Instance.new("SurfaceGui")
	marqueeGui.Face = Enum.NormalId.Front
	marqueeGui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
	marqueeGui.PixelsPerStud = 64
	marqueeGui.LightInfluence = 0
	marqueeGui.Brightness = 3
	marqueeGui.Parent = marquee
	local marqueeText = Instance.new("TextLabel")
	marqueeText.Size = UDim2.fromScale(1, 1)
	marqueeText.BackgroundTransparency = 1
	marqueeText.Text = "PUSH A FORTUNE"
	marqueeText.TextColor3 = CFG.colGold
	marqueeText.TextScaled = true
	marqueeText.Font = Enum.Font.FredokaOne
	marqueeText.Parent = marqueeGui

	-- Gold neon frame bars and rounded end caps.
	neonStrip(Vector3.new(13.6, 0.35, 1.0), at(0, 16.75, -7.9), CFG.colGold)
	neonStrip(Vector3.new(13.6, 0.35, 1.0), at(0, 13.25, -7.9), CFG.colGold)
	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "MarqueeCap",
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(0.9, 3.9, 3.9),
			CFrame = at(side * 6.75, 15.0, -7.9),
			Color = CFG.colGold,
			Material = Enum.Material.Neon,
			CanCollide = false,
		}, cabinet)
	end

	-- Bulb row across the top bar: alternating lit / unlit, arcade-style.
	for i = 0, 8 do
		local lit = (i % 2 == 0)
		newPart({
			Name = "Bulb",
			Shape = Enum.PartType.Ball,
			Size = Vector3.new(0.45, 0.45, 0.45),
			CFrame = at(-5.6 + i * 1.4, 16.75, -8.5),
			Color = lit and CFG.colGold or CFG.colGoldDim,
			Material = lit and Enum.Material.Neon or Enum.Material.SmoothPlastic,
			CanCollide = false,
		}, cabinet)
	end

	local marqueeLight = Instance.new("PointLight")
	marqueeLight.Color = CFG.colGold
	marqueeLight.Brightness = 1.0
	marqueeLight.Range = 7
	marqueeLight.Parent = marquee

	----------------------------------------------------------------------------
	-- Playfield
	----------------------------------------------------------------------------

	-- Low friction so the pile slides forward instead of grinding to a halt.
	newPart({
		Name = "Deck",
		Size = Vector3.new(12, 0.5, 11.5),
		CFrame = at(0, 4.75, 0.75),
		Color = CFG.colDeck,
		Material = Enum.Material.Metal,
		CustomPhysicalProperties = PhysicalProperties.new(1, 0.25, 0, 1, 1),
	}, playfield)

	-- Gold edge on the payout ledge, and a teal "win line" inlaid in the deck.
	newPart({
		Name = "LedgeEdge",
		Size = Vector3.new(12, 0.18, 0.3),
		CFrame = at(0, 4.95, -4.95),
		Color = CFG.colGold,
		Material = Enum.Material.Metal,
		CanCollide = false,
	}, playfield)
	neonStrip(Vector3.new(12, 0.06, 0.2), at(0, 5.03, -3.6), CFG.colTeal, playfield)

	-- Interior side walls that keep the pile on the deck, with teal top trim.
	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "FieldWall",
			Size = Vector3.new(0.5, 2.6, 11.5),
			CFrame = at(side * 6.25, 6.3, 0.75),
			Color = CFG.colBody2,
		}, playfield)
		neonStrip(Vector3.new(0.28, 0.22, 11.5), at(side * 6.25, 7.7, 0.75), CFG.colTeal, playfield)
	end

	-- The pusher is unanchored and driven by a PrismaticConstraint servo. This
	-- matters: an anchored part moved by CFrame does not reliably push
	-- unanchored parts — coins tunnel through it and jitter. A servo-driven
	-- constraint stays on rails and pushes the pile properly.
	local pusher = newPart({
		Name = "Pusher",
		Anchored = false,
		Size = CFG.pusherSize,
		CFrame = at(0, CFG.deckTop + 0.05 + CFG.pusherSize.Y / 2, CFG.pusherRestZ),
		Color = CFG.colBody2,
		Material = Enum.Material.Metal,
		CustomPhysicalProperties = PhysicalProperties.new(8, 0.3, 0, 1, 1),
	}, playfield)

	-- Lighter face plate and a neon lip along the pusher's leading edge.
	local frontZ = CFG.pusherRestZ - CFG.pusherSize.Z / 2
	local plate = newPart({
		Name = "PusherPlate",
		Anchored = false,
		Size = Vector3.new(CFG.pusherSize.X, CFG.pusherSize.Y, 0.25),
		CFrame = at(0, CFG.deckTop + 0.05 + CFG.pusherSize.Y / 2, frontZ - 0.1),
		Color = CFG.colDeck,
		Material = Enum.Material.Metal,
	}, playfield)
	local lip = neonStrip(
		Vector3.new(CFG.pusherSize.X, 0.14, 0.14),
		at(0, CFG.deckTop + 0.05 + CFG.pusherSize.Y, frontZ - 0.1),
		CFG.colTeal, playfield)
	lip.Anchored = false
	for _, attachedPart in ipairs({ plate, lip }) do
		local weld = Instance.new("WeldConstraint")
		weld.Part0 = pusher
		weld.Part1 = attachedPart
		weld.Parent = pusher
	end

	local body = cabinet:FindFirstChild("Body")
	local railAtt = Instance.new("Attachment")
	railAtt.Name = "PusherRail"
	railAtt.CFrame = body.CFrame:ToObjectSpace(pusher.CFrame)
	railAtt.Axis = Vector3.new(0, 0, -1)  -- positive TargetPosition drives forward
	railAtt.Parent = body

	local pusherAtt = Instance.new("Attachment")
	pusherAtt.Name = "PusherAnchor"
	pusherAtt.Axis = Vector3.new(0, 0, -1)
	pusherAtt.Parent = pusher

	local slide = Instance.new("PrismaticConstraint")
	slide.Name = "PusherDrive"
	slide.Attachment0 = railAtt
	slide.Attachment1 = pusherAtt
	slide.ActuatorType = Enum.ActuatorType.Servo
	slide.ServoMaxForce = CFG.pusherForce
	slide.Speed = (CFG.stroke * 2) / CFG.cycleSeconds
	slide.TargetPosition = 0
	slide.LimitsEnabled = true
	slide.LowerLimit = 0
	slide.UpperLimit = CFG.stroke
	slide.Parent = pusher

	----------------------------------------------------------------------------
	-- Payout sensors
	----------------------------------------------------------------------------

	newPart({
		Name = "Collector",
		Size = Vector3.new(10, 0.6, 2.6),
		CFrame = at(0, 2.4, -6.5),
		Transparency = 1, CanCollide = false,
	}, payout)

	newPart({
		Name = "DropZone",
		Size = Vector3.new(11, 0.4, 1.5),
		CFrame = at(0, CFG.deckTop + 4, CFG.deckBack - 0.75),
		Transparency = 1, CanCollide = false,
	}, playfield)

	----------------------------------------------------------------------------
	-- Decorative coin pile — the machine should look alive before it works.
	----------------------------------------------------------------------------

	local rng = makeRng(42)
	local placed = {}
	local function scatterCoin(x, y, z, rot, color)
		newPart({
			Name = "DecorCoin",
			Shape = Enum.PartType.Cylinder,
			Size = Vector3.new(CFG.coinThickness, CFG.coinDiameter, CFG.coinDiameter),
			CFrame = at(x, y, z) * rot,
			Color = color or CFG.colGold,
			Material = Enum.Material.Metal,
			CanCollide = false,
		}, folders.Decor)
	end

	-- Base layer, biased toward the ledge where a real pile builds up.
	local count = 0
	while count < 26 do
		local z = -4.5 + 5.6 * (rng() ^ 1.6)
		local x = (rng() * 2 - 1) * 5.1
		local ok = true
		for _, p in ipairs(placed) do
			if (p[1] - x) ^ 2 + (p[2] - z) ^ 2 < 0.85 then ok = false break end
		end
		if ok then
			placed[#placed + 1] = { x, z }
			scatterCoin(x, CFG.deckTop + 0.13, z,
				CFrame.Angles(0, rng() * math.pi, 0) * FLAT)
			count = count + 1
		end
	end

	-- Second layer resting on the first, some tilted.
	for i = 1, 12 do
		local src = placed[1 + math.floor(rng() * 20)]
		local tilt = (rng() - 0.5) * math.rad(28)
		scatterCoin(src[1] + (rng() - 0.5) * 0.6, CFG.deckTop + 0.38, src[2] + (rng() - 0.5) * 0.6,
			CFrame.Angles(tilt, rng() * math.pi, 0) * FLAT)
	end

	-- The tease: coins hanging over the ledge, mid-teeter.
	for _, x in ipairs({ -2.6, 0.9, 3.4 }) do
		scatterCoin(x, CFG.deckTop + 0.10, -5.05,
			CFrame.Angles(math.rad(-22), rng() * math.pi, 0) * FLAT)
	end

	-- A few winnings already in the tray (tilted with the tray slope).
	for i = 1, 7 do
		scatterCoin((rng() * 2 - 1) * 4.2, 1.95 - rng() * 0.25, -5.9 - rng() * 1.4,
			CFrame.Angles(math.rad(-15), rng() * math.pi, 0) * FLAT)
	end

	-- Two prize capsules nestled in the pile.
	for _, cap in ipairs({ { 2.2, -1.6, CFG.colMagenta }, { -3.6, 0.8, CFG.colTeal } }) do
		newPart({
			Name = "DecorCapsule",
			Shape = Enum.PartType.Ball,
			Size = Vector3.new(CFG.capsuleSize, CFG.capsuleSize, CFG.capsuleSize),
			CFrame = at(cap[1], CFG.deckTop + 0.85, cap[2]),
			Color = cap[3],
			Material = Enum.Material.Neon,
			CanCollide = false,
		}, folders.Decor)
	end

	----------------------------------------------------------------------------
	-- Mounts
	----------------------------------------------------------------------------

	local camPos = originCFrame * CFrame.new(0, 10.5, -16.5)
	local camLook = originCFrame * CFrame.new(0, 6.4, -1)
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
	-- Templates
	----------------------------------------------------------------------------

	newPart({
		Name = "Coin",
		Anchored = false,
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(CFG.coinThickness, CFG.coinDiameter, CFG.coinDiameter),
		Color = CFG.colGold,
		Material = Enum.Material.Metal,
		-- Near-zero elasticity, or the pile turns into popcorn.
		CustomPhysicalProperties = PhysicalProperties.new(2, 0.4, 0.05, 1, 1),
	}, folders.Templates)

	newPart({
		Name = "Capsule",
		Anchored = false,
		Shape = Enum.PartType.Ball,
		Size = Vector3.new(CFG.capsuleSize, CFG.capsuleSize, CFG.capsuleSize),
		Color = CFG.colMagenta,
		Material = Enum.Material.Neon,
		CustomPhysicalProperties = PhysicalProperties.new(1.2, 0.5, 0.1, 1, 1),
	}, folders.Templates)

	model.PrimaryPart = cabinet:FindFirstChild("Body")
	return model
end

--------------------------------------------------------------------------------
-- Runtime
--------------------------------------------------------------------------------

-- Oscillate the servo between retracted and extended. Returns a stop function.
function PusherMachine.start(model)
	local slide = model.Playfield.Pusher:FindFirstChild("PusherDrive")
	if not slide then
		return function() end
	end

	local running = true
	task.spawn(function()
		while running and slide.Parent do
			slide.TargetPosition = PusherMachine.Config.stroke
			task.wait(PusherMachine.Config.cycleSeconds / 2)
			if not running then break end
			slide.TargetPosition = 0
			task.wait(PusherMachine.Config.cycleSeconds / 2)
		end
	end)

	return function()
		running = false
	end
end

local function spawnFrom(model, templateName, xOffset, rot)
	local template = model.Templates:FindFirstChild(templateName)
	if not template then return nil end

	local dropZone = model.Playfield.DropZone
	local item = template:Clone()
	item.Name = templateName
	item.CFrame = dropZone.CFrame * CFrame.new(xOffset or 0, 0, 0) * (rot or CFrame.new())
	item.Parent = model
	return item
end

-- Drop a token in at the back. xOffset lets the player aim left/right.
function PusherMachine.dropCoin(model, xOffset)
	return spawnFrom(model, "Coin", xOffset, FLAT)
end

function PusherMachine.dropCapsule(model, xOffset)
	return spawnFrom(model, "Capsule", xOffset, nil)
end

-- Remove the static display pile (call when real gameplay takes over).
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

PusherMachine.Config = CFG

return PusherMachine
