--[[
	ServerInit — the runner that actually builds the machine.

	Setup in Studio (two objects, that's all):
	1. ReplicatedStorage > ModuleScript named exactly "PusherMachine",
	   containing all of src/PusherMachine.luau.
	2. ServerScriptService > Script (a regular Script, NOT a ModuleScript,
	   NOT a LocalScript), containing this file.
	Press Play. The machine builds at the origin on the baseplate.
]]

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local PusherMachine = require(ReplicatedStorage:WaitForChild("PusherMachine"))

local machine = PusherMachine.build(CFrame.new(0, 0, 0))
machine.Parent = workspace
PusherMachine.start(machine)

-- A short rain of live tokens on top of the display pile, so it's visibly alive.
task.spawn(function()
	for _ = 1, 20 do
		PusherMachine.dropCoin(machine, math.random(-45, 45) / 10)
		task.wait(0.4)
	end
end)

print("[PusherMachine] built — PusherMachine.clearDecor(machine) removes the display pile")
