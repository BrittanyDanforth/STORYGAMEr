--!strict-off
--[[
	PusherMachine — builds a complete coin pusher cabinet out of Roblox primitives.

	No mesh assets, no uploads, no modelling tool. A coin pusher is boxes and
	cylinders, which is exactly why it's the right first machine to build.

	Usage (ModuleScript in ReplicatedStorage or ServerStorage):

		local PusherMachine = require(path.to.PusherMachine)
		local machine = PusherMachine.build(CFrame.new(0, 0, 0))
		machine.Parent = workspace
		PusherMachine.start(machine)          -- begin the push cycle
		PusherMachine.dropCoin(machine)       -- drop a token in

	Everything is parametric — edit CFG and rebuild to retune the cabinet.

	Coordinate convention: the model's origin sits on the floor at the centre of
	the cabinet. The player stands on the -Z side; the pusher travels toward -Z
	and coins fall off the front ledge into the tray.
]]

local TweenService = game:GetService("TweenService")

local PusherMachine = {}

--------------------------------------------------------------------------------
-- Configuration
--------------------------------------------------------------------------------

local CFG = {
	-- Cabinet footprint, in studs.
	width       = 12,     -- interior width between the side walls (X)
	depth       = 14,     -- overall cabinet depth (Z)
	cabinetTop  = 13,     -- height of the cabinet roof

	-- Playfield.
	deckTop     = 4.5,    -- Y of the deck surface coins rest on
	deckBack    = 6.5,    -- Z where the deck begins (back of the machine)
	ledgeZ      = -5,     -- Z of the payout ledge; coins past this fall
	wallHeight  = 3,      -- how far the side walls rise above the deck

	-- Pusher.
	pusherSize   = Vector3.new(11.6, 1.2, 4),
	pusherRestZ  = 3.5,   -- Z of the pusher when fully retracted
	stroke       = 3,     -- how far forward it travels
	cycleSeconds = 2.2,   -- one full out-and-back cycle
	pusherForce  = 1e6,   -- servo force; must dwarf the weight of the pile

	-- Coins.
	coinDiameter  = 1.1,
	coinThickness = 0.25,
	capsuleSize   = 1.6,

	-- Palette. Deliberately arcade: warm gold prizes, cool cabinet, neon trim.
	colGold    = Color3.fromRGB(255, 186, 48),
	colCabinet = Color3.fromRGB(28, 33, 48),
	colDeck    = Color3.fromRGB(44, 52, 74),
	colTrim    = Color3.fromRGB(64, 226, 210),
	colCapsule = Color3.fromRGB(226, 84, 214),
}

-- Derived once so the geometry below stays readable.
local deckLength = CFG.deckBack - CFG.ledgeZ          -- 11.5
local deckCentreZ = (CFG.deckBack + CFG.ledgeZ) / 2   -- 0.75
local trayFrontZ = -CFG.depth / 2 - 0.5               -- -7.5
local trayCentreZ = (CFG.ledgeZ + trayFrontZ) / 2     -- -6.25
local trayLength = CFG.ledgeZ - trayFrontZ            -- 2.5
local shellWidth = CFG.width + 1                      -- 13

--------------------------------------------------------------------------------
-- Helpers
--------------------------------------------------------------------------------

local function newPart(props, parent)
	local part = Instance.new("Part")
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
-- has to be rolled 90 degrees about Z to lie flat on the deck.
local FLAT_COIN = CFrame.Angles(0, 0, math.rad(90))

--------------------------------------------------------------------------------
-- Build
--------------------------------------------------------------------------------

function PusherMachine.build(originCFrame, name)
	originCFrame = originCFrame or CFrame.new()

	local model = Instance.new("Model")
	model.Name = name or "CoinPusher"

	local cabinet = Instance.new("Folder")
	cabinet.Name = "Cabinet"
	cabinet.Parent = model

	local playfield = Instance.new("Folder")
	playfield.Name = "Playfield"
	playfield.Parent = model

	local payout = Instance.new("Folder")
	payout.Name = "Payout"
	payout.Parent = model

	local mounts = Instance.new("Folder")
	mounts.Name = "Mounts"
	mounts.Parent = model

	-- Local placement helper: everything below is authored in machine space and
	-- transformed into world space by the origin CFrame.
	local function at(x, y, z, rot)
		local offset = CFrame.new(x, y, z)
		if rot then
			offset = offset * rot
		end
		return originCFrame * offset
	end

	----------------------------------------------------------------------------
	-- Cabinet shell
	----------------------------------------------------------------------------

	-- The body under the deck. It stops short of the ledge so the payout tray
	-- below the front is open and the falling coins stay visible.
	local bodyLength = CFG.deckBack + 1 - CFG.ledgeZ
	local base = newPart({
		Name = "Base",
		Size = Vector3.new(shellWidth, CFG.deckTop - 0.5, bodyLength),
		CFrame = at(0, (CFG.deckTop - 0.5) / 2, (CFG.deckBack + 1 + CFG.ledgeZ) / 2),
		Color = CFG.colCabinet,
	}, cabinet)

	newPart({
		Name = "TrayFloor",
		Size = Vector3.new(shellWidth, 1, trayLength),
		CFrame = at(0, 0.5, trayCentreZ),
		Color = CFG.colCabinet,
	}, cabinet)

	newPart({
		Name = "BackPanel",
		Size = Vector3.new(shellWidth, CFG.cabinetTop - CFG.deckTop, 0.5),
		CFrame = at(0, (CFG.cabinetTop + CFG.deckTop) / 2, CFG.deckBack + 1.25),
		Color = CFG.colCabinet,
	}, cabinet)

	newPart({
		Name = "Roof",
		Size = Vector3.new(shellWidth, 0.5, CFG.depth + 2),
		CFrame = at(0, CFG.cabinetTop + 0.25, 0),
		Color = CFG.colCabinet,
	}, cabinet)

	-- Front glass. Sits ahead of the tray so you watch the coins drop through it.
	newPart({
		Name = "Glass",
		Size = Vector3.new(shellWidth, CFG.cabinetTop - 1, 0.3),
		CFrame = at(0, (CFG.cabinetTop + 1) / 2, trayFrontZ),
		Color = Color3.fromRGB(200, 240, 255),
		Material = Enum.Material.Glass,
		Transparency = 0.75,
		CanCollide = true,
	}, cabinet)

	-- Side walls: outer shell above the deck, plus the inner lips that keep the
	-- pile on the playfield.
	for _, side in ipairs({ -1, 1 }) do
		newPart({
			Name = "SideShell",
			Size = Vector3.new(0.5, CFG.cabinetTop - CFG.deckTop, CFG.depth + 2),
			CFrame = at(side * (shellWidth / 2 + 0.25), (CFG.cabinetTop + CFG.deckTop) / 2, 0),
			Color = CFG.colCabinet,
		}, cabinet)

		newPart({
			Name = "PlayfieldWall",
			Size = Vector3.new(0.5, CFG.wallHeight, deckLength),
			CFrame = at(side * (CFG.width / 2 + 0.25), CFG.deckTop + CFG.wallHeight / 2, deckCentreZ),
			Color = CFG.colDeck,
		}, cabinet)

		-- Neon trim along the top of each wall. This is the whole arcade look:
		-- Neon material on a thin part, no textures required.
		newPart({
			Name = "Trim",
			Size = Vector3.new(0.3, 0.3, deckLength),
			CFrame = at(side * (CFG.width / 2 + 0.25), CFG.deckTop + CFG.wallHeight, deckCentreZ),
			Color = CFG.colTrim,
			Material = Enum.Material.Neon,
			CanCollide = false,
		}, cabinet)
	end

	-- Marquee above the glass, lit and captioned with a SurfaceGui.
	local marquee = newPart({
		Name = "Marquee",
		Size = Vector3.new(shellWidth, 2.5, 0.4),
		CFrame = at(0, CFG.cabinetTop + 1.5, trayFrontZ),
		Color = CFG.colGold,
		Material = Enum.Material.Neon,
		CanCollide = false,
	}, cabinet)

	local sign = Instance.new("SurfaceGui")
	sign.Face = Enum.NormalId.Front
	sign.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
	sign.PixelsPerStud = 64
	sign.Parent = marquee

	local signText = Instance.new("TextLabel")
	signText.Size = UDim2.fromScale(1, 1)
	signText.BackgroundTransparency = 1
	signText.Text = "PUSH A FORTUNE"
	signText.TextColor3 = Color3.fromRGB(20, 16, 8)
	signText.TextScaled = true
	signText.Font = Enum.Font.FredokaOne
	signText.Parent = sign

	----------------------------------------------------------------------------
	-- Playfield
	----------------------------------------------------------------------------

	-- Low friction so the pile slides forward instead of grinding to a halt.
	local deck = newPart({
		Name = "Deck",
		Size = Vector3.new(CFG.width, 0.5, deckLength),
		CFrame = at(0, CFG.deckTop - 0.25, deckCentreZ),
		Color = CFG.colDeck,
		Material = Enum.Material.Metal,
		CustomPhysicalProperties = PhysicalProperties.new(1, 0.25, 0, 1, 1),
	}, playfield)

	-- The pusher is unanchored and driven by a PrismaticConstraint servo. This
	-- matters: an anchored part moved by CFrame does not reliably push
	-- unanchored parts — coins tunnel through it and jitter. A servo-driven
	-- constraint stays on rails and pushes the pile properly.
	local pusher = newPart({
		Name = "Pusher",
		Anchored = false,
		Size = CFG.pusherSize,
		CFrame = at(0, CFG.deckTop + 0.05 + CFG.pusherSize.Y / 2, CFG.pusherRestZ),
		Color = CFG.colDeck,
		Material = Enum.Material.Metal,
		CustomPhysicalProperties = PhysicalProperties.new(8, 0.3, 0, 1, 1),
	}, playfield)

	local railAtt = Instance.new("Attachment")
	railAtt.Name = "PusherRail"
	railAtt.CFrame = base.CFrame:ToObjectSpace(pusher.CFrame)
	railAtt.Axis = Vector3.new(0, 0, -1)  -- positive TargetPosition drives forward
	railAtt.Parent = base

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
	-- Payout
	----------------------------------------------------------------------------

	-- Sensor across the tray. Anything that lands here has cleared the ledge.
	newPart({
		Name = "Collector",
		Size = Vector3.new(CFG.width, 0.6, trayLength - 0.2),
		CFrame = at(0, 1.3, trayCentreZ),
		Transparency = 1,
		CanCollide = false,
	}, payout)

	-- Where new tokens enter the machine, at the back above the deck.
	newPart({
		Name = "DropZone",
		Size = Vector3.new(CFG.width - 1, 0.4, 1.5),
		CFrame = at(0, CFG.deckTop + 4, CFG.deckBack - 0.75),
		Transparency = 1,
		CanCollide = false,
	}, playfield)

	----------------------------------------------------------------------------
	-- Mounts
	----------------------------------------------------------------------------

	-- Where the camera tweens to when a player walks up and starts playing.
	-- The avatar keeps standing at the cabinet, so passers-by still see them.
	local camPos = originCFrame * CFrame.new(0, 11, -15)
	local camLook = originCFrame * CFrame.new(0, 5.5, 0)
	local cameraMount = newPart({
		Name = "CameraMount",
		Size = Vector3.new(0.4, 0.4, 0.4),
		Transparency = 1,
		CanCollide = false,
		CanQuery = false,
	}, mounts)
	cameraMount.CFrame = CFrame.lookAt(camPos.Position, camLook.Position)

	local standPart = newPart({
		Name = "StandHere",
		Size = Vector3.new(4, 0.2, 3),
		CFrame = at(0, 0.1, trayFrontZ - 3),
		Transparency = 1,
		CanCollide = false,
	}, mounts)

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

	local templates = Instance.new("Folder")
	templates.Name = "Templates"
	templates.Parent = model

	newPart({
		Name = "Coin",
		Anchored = false,
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(CFG.coinThickness, CFG.coinDiameter, CFG.coinDiameter),
		Color = CFG.colGold,
		Material = Enum.Material.Metal,
		-- Near-zero elasticity, or the pile turns into popcorn.
		CustomPhysicalProperties = PhysicalProperties.new(2, 0.4, 0.05, 1, 1),
	}, templates)

	newPart({
		Name = "Capsule",
		Anchored = false,
		Shape = Enum.PartType.Ball,
		Size = Vector3.new(CFG.capsuleSize, CFG.capsuleSize, CFG.capsuleSize),
		Color = CFG.colCapsule,
		Material = Enum.Material.Neon,
		CustomPhysicalProperties = PhysicalProperties.new(1.2, 0.5, 0.1, 1, 1),
	}, templates)

	model.PrimaryPart = base
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
			slide.TargetPosition = CFG.stroke
			task.wait(CFG.cycleSeconds / 2)
			if not running then break end
			slide.TargetPosition = 0
			task.wait(CFG.cycleSeconds / 2)
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
	return spawnFrom(model, "Coin", xOffset, FLAT_COIN)
end

function PusherMachine.dropCapsule(model, xOffset)
	return spawnFrom(model, "Capsule", xOffset, nil)
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
