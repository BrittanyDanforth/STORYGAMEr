#!/usr/bin/env python3
"""render_preview.py — offline preview renderer for the machine geometry.

Reads the parts.json produced by tools/dump_parts.lua and paints a shaded
perspective view with PIL: painter's algorithm, flat lambert shading, additive
glow for Neon parts and lights, alpha compositing for Glass, and text drawn
onto SurfaceGui faces. Purpose: see the cabinet before Studio does.

Usage: render_preview.py parts.json out.png [front]
"""
import json, math, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

W, H = 1600, 1300
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

def v_sub(a,b): return (a[0]-b[0], a[1]-b[1], a[2]-b[2])
def v_add(a,b): return (a[0]+b[0], a[1]+b[1], a[2]+b[2])
def v_scale(a,s): return (a[0]*s, a[1]*s, a[2]*s)
def v_dot(a,b): return a[0]*b[0]+a[1]*b[1]+a[2]*b[2]
def v_cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def v_norm(a):
    m = math.sqrt(v_dot(a,a)) or 1.0
    return (a[0]/m, a[1]/m, a[2]/m)

class Camera:
    def __init__(self, eye, look, F):
        self.eye, self.F = eye, F
        f = v_norm(v_sub(look, eye))
        self.fwd = f
        self.right = v_norm(v_cross(f, (0,1,0)))
        self.up = v_cross(self.right, f)
    def project(self, p):
        d = v_sub(p, self.eye)
        z = v_dot(d, self.fwd)
        if z < 0.5: z = 0.5
        x = v_dot(d, self.right)/z*self.F + W/2
        y = -v_dot(d, self.up)/z*self.F + H/2 + 60
        return (x, y, z)

def part_verts_faces(p):
    """World-space faces for one part: list of (verts, normal_hint)."""
    sx, sy, sz = [s/2 for s in p["size"]]
    r, pos = p["rot"], p["pos"]
    def xf(v):
        x, y, z = v
        return (r[0]*x+r[1]*y+r[2]*z+pos[0],
                r[3]*x+r[4]*y+r[5]*z+pos[1],
                r[6]*x+r[7]*y+r[8]*z+pos[2])
    shape = p["shape"]
    faces = []
    if shape == "Block":
        c = [xf((X,Y,Z)) for X in (-sx,sx) for Y in (-sy,sy) for Z in (-sz,sz)]
        # index: 4*(x>0)+2*(y>0)+(z>0)
        idx = [ (0,1,3,2), (4,6,7,5),      # -x, +x
                (0,4,5,1), (2,3,7,6),      # -y, +y
                (0,2,6,4), (1,5,7,3) ]     # -z, +z
        faces = [[c[i] for i in f] for f in idx]
    elif shape == "Wedge":
        # WedgePart: full height at +Z, slope descending toward -Z.
        b = [xf((X,-sy,Z)) for X in (-sx,sx) for Z in (-sz,sz)]  # 0:-x-z 1:-x+z 2:+x-z 3:+x+z
        t = [xf((-sx,sy,sz)), xf((sx,sy,sz))]
        faces = [ [b[0],b[2],b[3],b[1]],            # bottom
                  [b[1],b[3],t[1],t[0]],            # back (+z)
                  [b[0],b[1],t[0]],                 # -x side
                  [b[2],b[3],t[1]],                 # +x side
                  [b[0],b[2],t[1],t[0]] ]           # slope
    elif shape == "Cylinder":
        n = 14
        ring0, ring1 = [], []
        for i in range(n):
            a = 2*math.pi*i/n
            y, z = math.cos(a)*sy, math.sin(a)*sz
            ring0.append(xf((-sx, y, z)))
            ring1.append(xf((sx, y, z)))
        faces = [ring0[::-1], ring1]
        for i in range(n):
            j = (i+1) % n
            faces.append([ring0[i], ring0[j], ring1[j], ring1[i]])
    elif shape == "Ball":
        return None  # handled as billboard
    return faces

def face_key(verts, cam):
    cx = sum(v[0] for v in verts)/len(verts)
    cy = sum(v[1] for v in verts)/len(verts)
    cz = sum(v[2] for v in verts)/len(verts)
    return v_dot(v_sub((cx,cy,cz), cam.eye), cam.fwd)

LIGHT = v_norm((-0.45, 1.0, -0.75))

def shade(color, normal, neon):
    r, g, b = [c*255 for c in color]
    if neon:
        f = 1.18
    else:
        lam = max(0.0, v_dot(normal, LIGHT))
        f = 0.48 + 0.56*lam
    return (min(255,int(r*f)), min(255,int(g*f)), min(255,int(b*f)))

def render(parts, out_path, front=False):
    cam = Camera((-13, 18, -28), (0, 5.2, 1.5), 1500) if not front else \
          Camera((0, 8.6, -48), (0, 8.3, 0), 2050)

    img = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(img)
    # Backdrop: vertical gradient arcade-dark.
    for y in range(H):
        t = y/H
        d.line([(0,y),(W,y)], fill=(int(10+14*t), int(12+16*t), int(20+24*t)))
    # Floor line + shadow ellipse.
    fy = cam.project((0,0,0))[1]
    d.rectangle([0, fy, W, H], fill=(16,18,28))
    pts = []
    for i in range(24):
        a = 2*math.pi*i/24
        pts.append(cam.project((math.cos(a)*9.5, 0, -0.3+math.sin(a)*10.5))[:2])
    shadow = Image.new("RGBA", (W,H), (0,0,0,0))
    ImageDraw.Draw(shadow).polygon(pts, fill=(0,0,0,130))
    shadow = shadow.filter(ImageFilter.GaussianBlur(18))
    img.paste(Image.new("RGB",(W,H),(0,0,0)), (0,0), shadow)

    glow = Image.new("RGB", (W, H), (0,0,0))
    gd = ImageDraw.Draw(glow)

    # Collect drawables: (depth, kind, payload)
    draw_list = []
    for p in parts:
        if float(p["transparency"]) >= 0.95:
            continue
        neon = p["material"] == "Neon"
        alpha = 1.0 - float(p["transparency"])
        if p["shape"] == "Ball":
            c = cam.project(tuple(p["pos"]))
            rad = p["size"][0]/2 * cam.F / c[2]
            draw_list.append((c[2], "ball", (c, rad, p, neon)))
            continue
        faces = part_verts_faces(p)
        if not faces: continue
        for verts in faces:
            # backface cull
            if len(verts) >= 3:
                n = v_norm(v_cross(v_sub(verts[1], verts[0]), v_sub(verts[2], verts[0])))
                cx = tuple(sum(v[i] for v in verts)/len(verts) for i in range(3))
                if v_dot(n, v_sub(cx, cam.eye)) > 0:
                    n = v_scale(n, -1)
                depth = face_key(verts, cam)
                draw_list.append((depth, "face", (verts, n, p, neon, alpha)))

    draw_list.sort(key=lambda e: -e[0])

    overlay_needed = []
    for depth, kind, payload in draw_list:
        if kind == "ball":
            c, rad, p, neon = payload
            col = tuple(min(255,int(ch*255*(1.16 if neon else 0.95))) for ch in p["color"])
            box = [c[0]-rad, c[1]-rad, c[0]+rad, c[1]+rad]
            d.ellipse(box, fill=col)
            hi = rad*0.35
            d.ellipse([c[0]-rad*0.35-hi/2, c[1]-rad*0.4-hi/2, c[0]-rad*0.35+hi/2, c[1]-rad*0.4+hi/2],
                      fill=tuple(min(255,int(v*1.4+40)) for v in col))
            if neon:
                gd.ellipse([box[0]-rad*.6, box[1]-rad*.6, box[2]+rad*.6, box[3]+rad*.6], fill=col)
            continue
        verts, n, p, neon, alpha = payload
        pts = [cam.project(v)[:2] for v in verts]
        col = shade(p["color"], n, neon)
        if alpha >= 0.99:
            d.polygon(pts, fill=col)
            if neon:
                gd.polygon(pts, fill=col)
        else:
            layer = Image.new("RGBA", (W,H), (0,0,0,0))
            a8 = int(alpha * 0.42 * 255)
            ImageDraw.Draw(layer).polygon(pts, fill=col + (a8,))
            img.paste(Image.new("RGB",(W,H),col), (0,0), layer.split()[3])

    # Lights: radial blobs on the glow layer.
    for p in parts:
        for L in p.get("lights", []):
            c = cam.project(tuple(p["pos"]))
            rad = L["range"]*0.34 * cam.F / c[2]
            col = tuple(int(x*255*0.33) for x in (L["r"], L["g"], L["b"]))
            gd.ellipse([c[0]-rad, c[1]-rad, c[0]+rad, c[1]+rad], fill=col)

    glow = glow.filter(ImageFilter.GaussianBlur(22))
    img = ImageChops.screen(img, glow)

    # SurfaceGui text on -Z faces.
    d = ImageDraw.Draw(img)
    for p in parts:
        if "text" not in p: continue
        sx, sy, sz = [s/2 for s in p["size"]]
        r, pos = p["rot"], p["pos"]
        def xf(v):
            x, y, z = v
            return (r[0]*x+r[1]*y+r[2]*z+pos[0],
                    r[3]*x+r[4]*y+r[5]*z+pos[1],
                    r[6]*x+r[7]*y+r[8]*z+pos[2])
        quad = [xf((sx,sy,-sz)), xf((-sx,sy,-sz)), xf((-sx,-sy,-sz)), xf((sx,-sy,-sz))]
        spts = [cam.project(v)[:2] for v in quad]
        wpx = math.dist(spts[0], spts[1]); hpx = math.dist(spts[1], spts[2])
        t = p["text"]
        size = int(hpx*0.52)
        if size < 8: continue
        font = ImageFont.truetype(FONT, size)
        while font.getlength(t["text"]) > wpx*0.9 and size > 8:
            size -= 2; font = ImageFont.truetype(FONT, size)
        cx = sum(pt[0] for pt in spts)/4; cy = sum(pt[1] for pt in spts)/4
        ang = -math.degrees(math.atan2(spts[1][1]-spts[0][1], spts[1][0]-spts[0][0]))
        tw = font.getlength(t["text"]); th = size*1.3
        tile = Image.new("RGBA", (int(tw)+8, int(th)+8), (0,0,0,0))
        ImageDraw.Draw(tile).text((4,4), t["text"], font=font,
            fill=tuple(int(c*255) for c in (t["r"],t["g"],t["b"])))
        tile = tile.rotate(ang, expand=True, resample=Image.BICUBIC)
        img.paste(tile, (int(cx-tile.width/2), int(cy-tile.height/2)), tile)

    img.save(out_path)
    print("wrote", out_path)

if __name__ == "__main__":
    parts = json.load(open(sys.argv[1]))
    render(parts, sys.argv[2], front=(len(sys.argv) > 3 and sys.argv[3] == "front"))
