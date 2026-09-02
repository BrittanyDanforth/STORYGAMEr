-- dump_parts.lua — build the machine headlessly and emit part geometry as JSON.
-- Usage: lua5.1 tools/dump_parts.lua > /path/parts.json  (run from repo root)

os.setlocale("C", "numeric")
dofile("tools/roblox_stub.lua")
local M = dofile("src/PusherMachine.luau")

local model = M.build(CFrame.new(0, 0, 0))

local out = {}
local function esc(s) return (s:gsub('["\\]', '\\%0')) end

local function walk(obj)
	local class = rawget(obj, "__class")
	local p = rawget(obj, "__props")
	if class == "Part" or class == "WedgePart" then
		local cf, size, color = p.CFrame, p.Size, p.Color or { R = 0.6, G = 0.6, B = 0.6 }
		local shape = class == "WedgePart" and "Wedge"
			or (p.Shape and p.Shape.Name) or "Block"
		-- Attached gui text + lights, for the renderer.
		local text = nil
		local lights = {}
		for _, child in ipairs(rawget(obj, "__children")) do
			local cc = rawget(child, "__class")
			if cc == "SurfaceGui" then
				for _, g in ipairs(rawget(child, "__children")) do
					if rawget(g, "__class") == "TextLabel" then
						local tc = g.__props.TextColor3 or { R = 1, G = 1, B = 1 }
						text = string.format('{"text":"%s","r":%.3f,"g":%.3f,"b":%.3f}',
							esc(g.__props.Text or ""), tc.R, tc.G, tc.B)
					end
				end
			elseif cc == "PointLight" or cc == "SurfaceLight" then
				local lc = child.__props.Color or { R = 1, G = 1, B = 1 }
				lights[#lights + 1] = string.format(
					'{"kind":"%s","r":%.3f,"g":%.3f,"b":%.3f,"range":%s,"brightness":%s}',
					cc, lc.R, lc.G, lc.B,
					tostring(child.__props.Range or 8), tostring(child.__props.Brightness or 1))
			end
		end
		out[#out + 1] = string.format(
			'{"name":"%s","shape":"%s","size":[%.4f,%.4f,%.4f],' ..
			'"pos":[%.4f,%.4f,%.4f],"rot":[%s],' ..
			'"color":[%.3f,%.3f,%.3f],"material":"%s","transparency":%s,' ..
			'"canCollide":%s' ..
			'%s%s}',
			esc(p.Name or "?"), shape,
			size.X, size.Y, size.Z,
			cf.p[1], cf.p[2], cf.p[3],
			table.concat({ string.format("%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f",
				cf.r[1], cf.r[2], cf.r[3], cf.r[4], cf.r[5], cf.r[6], cf.r[7], cf.r[8], cf.r[9]) }),
			color.R, color.G, color.B,
			(p.Material and p.Material.Name) or "SmoothPlastic",
			tostring(p.Transparency or 0),
			-- Roblox default is CanCollide = true; decorate() / sensors set it false.
			tostring(p.CanCollide ~= false),
			text and (',"text":' .. text) or "",
			#lights > 0 and (',"lights":[' .. table.concat(lights, ",") .. "]") or "")
	end
	for _, child in ipairs(rawget(obj, "__children")) do
		-- Skip templates: they live outside world space.
		if (child.__props.Name or "") ~= "Templates" then walk(child) end
	end
end

walk(model)
print("[" .. table.concat(out, ",\n") .. "]")
