-- roblox_stub.lua — minimal headless mock of the Roblox API, just enough to
-- execute PusherMachine.build() outside Studio and capture part geometry.
-- Conventions mirrored: CFrame columns are Right/Up/Back axes, points transform
-- as world = R * local + p, CFrame.Angles composes Rx*Ry*Rz (Z applied first),
-- LookVector is -Z.

local function vec(x, y, z) return { X = x or 0, Y = y or 0, Z = z or 0 } end

Vector3 = {}
local V3 = {}
V3.__index = V3
function Vector3.new(x, y, z) return setmetatable(vec(x, y, z), V3) end
function V3.__add(a, b) return Vector3.new(a.X + b.X, a.Y + b.Y, a.Z + b.Z) end
function V3.__sub(a, b) return Vector3.new(a.X - b.X, a.Y - b.Y, a.Z - b.Z) end
function V3.__mul(a, b)
	if type(a) == "number" then return Vector3.new(a * b.X, a * b.Y, a * b.Z) end
	return Vector3.new(a.X * b, a.Y * b, a.Z * b)
end
function V3:Cross(o)
	return Vector3.new(self.Y * o.Z - self.Z * o.Y, self.Z * o.X - self.X * o.Z, self.X * o.Y - self.Y * o.X)
end
function V3:Dot(o) return self.X * o.X + self.Y * o.Y + self.Z * o.Z end
function V3:magnitude() return math.sqrt(self:Dot(self)) end
V3.__index = function(t, k)
	if k == "Unit" or k == "unit" then
		local m = math.sqrt(t.X * t.X + t.Y * t.Y + t.Z * t.Z)
		return Vector3.new(t.X / m, t.Y / m, t.Z / m)
	end
	if k == "Magnitude" then return math.sqrt(t.X * t.X + t.Y * t.Y + t.Z * t.Z) end
	return V3[k]
end

CFrame = {}
local CF = {}
CF.__index = CF
-- r = {r00,r01,r02, r10,r11,r12, r20,r21,r22} row-major; columns are the axes.
local function cfnew(px, py, pz, r)
	return setmetatable({ p = { px, py, pz }, r = r or { 1, 0, 0, 0, 1, 0, 0, 0, 1 } }, CF)
end
function CFrame.new(x, y, z) return cfnew(x or 0, y or 0, z or 0) end
local function matmul(a, b)
	local c = {}
	for i = 0, 2 do
		for j = 0, 2 do
			c[i * 3 + j + 1] = a[i * 3 + 1] * b[j + 1] + a[i * 3 + 2] * b[j + 4] + a[i * 3 + 3] * b[j + 7]
		end
	end
	return c
end
function CFrame.Angles(rx, ry, rz)
	local cx, sx = math.cos(rx), math.sin(rx)
	local cy, sy = math.cos(ry), math.sin(ry)
	local cz, sz = math.cos(rz), math.sin(rz)
	local Rx = { 1, 0, 0, 0, cx, -sx, 0, sx, cx }
	local Ry = { cy, 0, sy, 0, 1, 0, -sy, 0, cy }
	local Rz = { cz, -sz, 0, sz, cz, 0, 0, 0, 1 }
	return cfnew(0, 0, 0, matmul(Rx, matmul(Ry, Rz)))
end
function CFrame.lookAt(pos, target)
	local f = { target.X - pos.X, target.Y - pos.Y, target.Z - pos.Z }
	local m = math.sqrt(f[1] ^ 2 + f[2] ^ 2 + f[3] ^ 2)
	local z = { -f[1] / m, -f[2] / m, -f[3] / m }         -- back axis
	local upv = { 0, 1, 0 }
	local x = { upv[2] * z[3] - upv[3] * z[2], upv[3] * z[1] - upv[1] * z[3], upv[1] * z[2] - upv[2] * z[1] }
	local xm = math.sqrt(x[1] ^ 2 + x[2] ^ 2 + x[3] ^ 2)
	x = { x[1] / xm, x[2] / xm, x[3] / xm }
	local y = { z[2] * x[3] - z[3] * x[2], z[3] * x[1] - z[1] * x[3], z[1] * x[2] - z[2] * x[1] }
	return cfnew(pos.X, pos.Y, pos.Z, { x[1], y[1], z[1], x[2], y[2], z[2], x[3], y[3], z[3] })
end
function CF.__mul(a, b)
	if getmetatable(b) == CF then
		local r = matmul(a.r, b.r)
		local px = a.r[1] * b.p[1] + a.r[2] * b.p[2] + a.r[3] * b.p[3] + a.p[1]
		local py = a.r[4] * b.p[1] + a.r[5] * b.p[2] + a.r[6] * b.p[3] + a.p[2]
		local pz = a.r[7] * b.p[1] + a.r[8] * b.p[2] + a.r[9] * b.p[3] + a.p[3]
		return cfnew(px, py, pz, r)
	end
	local px = a.r[1] * b.X + a.r[2] * b.Y + a.r[3] * b.Z + a.p[1]
	local py = a.r[4] * b.X + a.r[5] * b.Y + a.r[6] * b.Z + a.p[2]
	local pz = a.r[7] * b.X + a.r[8] * b.Y + a.r[9] * b.Z + a.p[3]
	return Vector3.new(px, py, pz)
end
function CF:Inverse()
	local r = self.r
	local t = { r[1], r[4], r[7], r[2], r[5], r[8], r[3], r[6], r[9] }
	local px = -(t[1] * self.p[1] + t[2] * self.p[2] + t[3] * self.p[3])
	local py = -(t[4] * self.p[1] + t[5] * self.p[2] + t[6] * self.p[3])
	local pz = -(t[7] * self.p[1] + t[8] * self.p[2] + t[9] * self.p[3])
	return cfnew(px, py, pz, t)
end
function CF:ToObjectSpace(other) return self:Inverse() * other end
CF.__index = function(t, k)
	if k == "Position" then return Vector3.new(t.p[1], t.p[2], t.p[3]) end
	return CF[k]
end

Color3 = {}
function Color3.fromRGB(r, g, b) return { R = r / 255, G = g / 255, B = b / 255 } end

Enum = setmetatable({}, { __index = function(t, k)
	local sub = setmetatable({}, { __index = function(st, sk)
		local v = { EnumType = k, Name = sk }
		rawset(st, sk, v)
		return v
	end })
	rawset(t, k, sub)
	return sub
end })

PhysicalProperties = { new = function(...) return { ... } end }
UDim2 = { fromScale = function(x, y) return { x, y } end }
TweenInfo = { new = function(...) return { ... } end }

task = { spawn = function() end, wait = function() end }

game = {}
function game:GetService(_)
	local svc = {}
	function svc:Create(...)
		local tween = {}
		function tween:Play() end
		return tween
	end
	return svc
end

-- Instance tree ------------------------------------------------------------

Instance = {}
local Obj = {}
local function isObj(v) return type(v) == "table" and rawget(v, "__class") ~= nil end

Obj.__index = function(t, k)
	local methods = rawget(Obj, "methods")
	if methods[k] then return methods[k] end
	local props = rawget(t, "__props")
	if props[k] ~= nil then return props[k] end
	for _, child in ipairs(rawget(t, "__children")) do
		if child.__props.Name == k then return child end
	end
	return nil
end
Obj.__newindex = function(t, k, v)
	if k == "Parent" then
		local old = rawget(t, "__parent")
		if old then
			for i, c in ipairs(old.__children) do
				if c == t then table.remove(old.__children, i) break end
			end
		end
		rawset(t, "__parent", v)
		if v then table.insert(v.__children, t) end
		return
	end
	rawget(t, "__props")[k] = v
end

Obj.methods = {}
function Obj.methods:FindFirstChild(name)
	for _, child in ipairs(rawget(self, "__children")) do
		if child.__props.Name == name then return child end
	end
	return nil
end
function Obj.methods:GetChildren()
	local out = {}
	for i, c in ipairs(rawget(self, "__children")) do out[i] = c end
	return out
end
function Obj.methods:Destroy()
	local parent = rawget(self, "__parent")
	if parent then
		for i, c in ipairs(parent.__children) do
			if c == self then table.remove(parent.__children, i) break end
		end
	end
end
function Obj.methods:Clone()
	local copy = Instance.new(rawget(self, "__class"))
	for k, v in pairs(rawget(self, "__props")) do copy.__props[k] = v end
	for _, child in ipairs(rawget(self, "__children")) do
		local cc = child:Clone()
		cc.Parent = copy
	end
	return copy
end

function Instance.new(class)
	return setmetatable(
		{ __class = class, __props = { Name = class }, __children = {}, __parent = nil }, Obj)
end
