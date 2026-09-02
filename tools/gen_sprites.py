#!/usr/bin/env python3
"""Particle sprite pack for PusherMachine.SpriteArt.

Every sprite is white/near-white art over a soft DARK halo. The halo exists
for Roblox image moderation (white-on-alpha particle art has been
false-flagged as empty/suspicious); in-game the emitters run LightEmission 1
(additive), so dark pixels contribute nothing and the halo vanishes.

    python3 tools/gen_sprites.py            # writes assets/sprites/*.png + _sheet.png

Upload each PNG as a Decal/Image in the Creator Dashboard, then paste the
resulting rbxassetid into PusherMachine.SpriteArt under the same key. The six
NEW keys (heart, diamond, flame, spiral, leaf, coin) ship as
"rbxassetid://0" and fall back to an existing sprite until pasted.
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter

SIZE = 256
C = SIZE // 2
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets", "sprites")

WHITE = (255, 255, 255, 255)
SOFT = (235, 240, 255, 255)


def canvas():
    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def halo(radius=0.62, alpha=170):
    """Soft dark disc behind the art (moderation-visible, additive-invisible)."""
    im = canvas()
    d = ImageDraw.Draw(im)
    r = int(SIZE * radius / 2)
    d.ellipse((C - r, C - r, C + r, C + r), fill=(18, 16, 28, alpha))
    return im.filter(ImageFilter.GaussianBlur(22))


def glow(layer, blur=14, boost=1.0):
    """Additive-looking bloom: a blurred copy under the crisp layer."""
    g = layer.filter(ImageFilter.GaussianBlur(blur))
    if boost != 1.0:
        a = g.split()[3].point(lambda v: min(255, int(v * boost)))
        g.putalpha(a)
    out = canvas()
    out.alpha_composite(g)
    out.alpha_composite(layer)
    return out


def compose(art, halo_radius=0.62, halo_alpha=170, blur=14, boost=1.2):
    out = halo(halo_radius, halo_alpha)
    out.alpha_composite(glow(art, blur, boost))
    return out


def polar(cx, cy, r, ang):
    return (cx + r * math.cos(ang), cy + r * math.sin(ang))


def star(points, r_out, r_in, rot=-math.pi / 2, cx=C, cy=C):
    pts = []
    for i in range(points * 2):
        r = r_out if i % 2 == 0 else r_in
        pts.append(polar(cx, cy, r, rot + i * math.pi / points))
    return pts


# ---------------------------------------------------------------- shapes
def sp_glowOrb():
    art = canvas()
    d = ImageDraw.Draw(art)
    for i in range(40, 0, -1):
        r = int(70 * i / 40)
        a = int(255 * (1 - i / 40) ** 1.6)
        d.ellipse((C - r, C - r, C + r, C + r), fill=(255, 255, 255, a))
    return compose(art, blur=10, boost=1.0)


def sp_star4():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.polygon(star(4, 96, 22), fill=WHITE)
    d.polygon(star(4, 40, 10, rot=-math.pi / 4), fill=SOFT)
    return compose(art)


def sp_sparkDust():
    art = canvas()
    d = ImageDraw.Draw(art)
    import random
    random.seed(7)
    for _ in range(14):
        x, y = random.gauss(C, 34), random.gauss(C, 34)
        r = random.choice([3, 4, 5, 7, 9])
        d.ellipse((x - r, y - r, x + r, y + r), fill=WHITE)
    d.polygon(star(4, 30, 7), fill=WHITE)
    return compose(art, blur=8)


def sp_ringFlare():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.ellipse((C - 92, C - 92, C + 92, C + 92), outline=WHITE, width=14)
    d.ellipse((C - 52, C - 52, C + 52, C + 52), outline=SOFT, width=4)
    return compose(art, blur=12)


def sp_bokeh():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.ellipse((C - 84, C - 84, C + 84, C + 84), fill=(255, 255, 255, 120))
    d.ellipse((C - 84, C - 84, C + 84, C + 84), outline=WHITE, width=10)
    return compose(art, blur=10, boost=0.9)


def sp_smokeWisp():
    art = canvas()
    d = ImageDraw.Draw(art)
    for (x, y, r) in [(C - 30, C + 20, 50), (C + 26, C - 4, 58), (C - 6, C - 40, 42), (C + 40, C + 40, 34)]:
        d.ellipse((x - r, y - r, x + r, y + r), fill=(255, 255, 255, 150))
    art = art.filter(ImageFilter.GaussianBlur(9))
    return compose(art, blur=18, boost=0.9)


def sp_ember():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.polygon([(C, C - 90), (C + 40, C + 10), (C, C + 70), (C - 40, C + 10)], fill=WHITE)
    d.ellipse((C - 30, C - 20, C + 30, C + 40), fill=WHITE)
    return compose(art, blur=12)


def sp_snowflake():
    art = canvas()
    d = ImageDraw.Draw(art)
    for k in range(6):
        a = k * math.pi / 3
        x1, y1 = polar(C, C, 96, a)
        d.line((C, C, x1, y1), fill=WHITE, width=10)
        for t in (0.45, 0.7):
            bx, by = polar(C, C, 96 * t, a)
            for s in (-1, 1):
                ex, ey = polar(bx, by, 26, a + s * math.pi / 3)
                d.line((bx, by, ex, ey), fill=WHITE, width=7)
    d.ellipse((C - 12, C - 12, C + 12, C + 12), fill=WHITE)
    return compose(art, blur=8)


def sp_petal():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.polygon([(C, C - 100), (C + 62, C - 20), (C + 20, C + 90), (C - 20, C + 90), (C - 62, C - 20)], fill=WHITE)
    art = art.filter(ImageFilter.GaussianBlur(3))
    return compose(art, blur=10)


def sp_bubble():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.ellipse((C - 88, C - 88, C + 88, C + 88), outline=WHITE, width=9)
    d.ellipse((C - 88, C - 88, C + 88, C + 88), fill=(255, 255, 255, 40))
    d.ellipse((C - 62, C - 68, C - 26, C - 32), fill=WHITE)
    return compose(art, blur=8, boost=0.9)


def sp_bolt():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.polygon([(C + 22, C - 104), (C - 34, C + 4), (C + 4, C + 4), (C - 22, C + 104), (C + 42, C - 18), (C + 4, C - 18)], fill=WHITE)
    return compose(art, blur=10)


def sp_crescent():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.ellipse((C - 84, C - 84, C + 84, C + 84), fill=WHITE)
    cut = canvas()
    ImageDraw.Draw(cut).ellipse((C - 52, C - 96, C + 116, C + 72), fill=(0, 0, 0, 255))
    art = Image.composite(canvas(), art, cut.split()[3])
    return compose(art, blur=10)


def sp_shard():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.polygon([(C, C - 108), (C + 30, C - 10), (C, C + 108), (C - 30, C - 10)], fill=WHITE)
    d.polygon([(C, C - 80), (C + 12, C - 10), (C, C + 60), (C - 12, C - 10)], fill=SOFT)
    return compose(art, blur=9)


def sp_drip():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.polygon([(C, C - 96), (C + 46, C + 10), (C, C + 40), (C - 46, C + 10)], fill=WHITE)
    d.ellipse((C - 50, C - 30, C + 50, C + 70), fill=WHITE)
    return compose(art, blur=10)


def sp_note():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.ellipse((C - 62, C + 22, C - 6, C + 68), fill=WHITE)
    d.rectangle((C - 14, C - 96, C, C + 46), fill=WHITE)
    d.polygon([(C - 14, C - 96), (C + 52, C - 60), (C + 52, C - 24), (C - 14, C - 60)], fill=WHITE)
    return compose(art, blur=9)


# ------------------------------------------------------------- NEW six
def sp_heart():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.ellipse((C - 84, C - 74, C - 4, C + 6), fill=WHITE)
    d.ellipse((C + 4, C - 74, C + 84, C + 6), fill=WHITE)
    d.polygon([(C - 80, C - 20), (C + 80, C - 20), (C, C + 92)], fill=WHITE)
    return compose(art, blur=10)


def sp_diamond():
    art = canvas()
    d = ImageDraw.Draw(art)
    top = [(C - 70, C - 40), (C - 34, C - 84), (C + 34, C - 84), (C + 70, C - 40)]
    d.polygon(top + [(C, C + 92)], fill=WHITE)
    # facets as dark seams (vanish additively, read as cuts on the sheet)
    for p in ((C - 34, C - 84), (C, C - 84), (C + 34, C - 84)):
        d.line((p, (C, C - 40)), fill=(40, 40, 60, 255), width=3)
    d.line(((C - 70, C - 40), (C + 70, C - 40)), fill=(40, 40, 60, 255), width=3)
    d.line(((C - 36, C - 40), (C, C + 92)), fill=(40, 40, 60, 255), width=3)
    d.line(((C + 36, C - 40), (C, C + 92)), fill=(40, 40, 60, 255), width=3)
    return compose(art, blur=9)


def sp_flame():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.polygon([(C, C - 104), (C + 26, C - 50), (C + 54, C - 10), (C + 40, C + 60), (C, C + 90), (C - 40, C + 60), (C - 54, C - 10), (C - 20, C - 44)], fill=WHITE)
    d.polygon([(C + 6, C - 40), (C + 26, C + 4), (C + 14, C + 56), (C - 12, C + 56), (C - 24, C + 4)], fill=(40, 40, 60, 255))
    return compose(art, blur=12)


def sp_spiral():
    art = canvas()
    d = ImageDraw.Draw(art)
    pts = []
    for i in range(0, 720, 4):
        a = math.radians(i)
        r = 10 + 90 * (i / 720)
        pts.append(polar(C, C, r, a))
    d.line(pts, fill=WHITE, width=12, joint="curve")
    return compose(art, blur=10)


def sp_leaf():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.polygon([(C - 90, C + 60), (C - 30, C - 40), (C + 60, C - 90), (C + 40, C + 10), (C - 20, C + 70)], fill=WHITE)
    d.line(((C - 90, C + 60), (C + 60, C - 90)), fill=(40, 40, 60, 255), width=4)
    return compose(art, blur=10)


def sp_coin():
    art = canvas()
    d = ImageDraw.Draw(art)
    d.ellipse((C - 86, C - 86, C + 86, C + 86), fill=WHITE)
    d.ellipse((C - 66, C - 66, C + 66, C + 66), outline=(40, 40, 60, 255), width=5)
    d.polygon(star(5, 42, 18), fill=(40, 40, 60, 255))
    return compose(art, blur=9)


SPRITES = {
    "glowOrb": sp_glowOrb, "star4": sp_star4, "sparkDust": sp_sparkDust,
    "ringFlare": sp_ringFlare, "bokeh": sp_bokeh, "smokeWisp": sp_smokeWisp,
    "ember": sp_ember, "snowflake": sp_snowflake, "petal": sp_petal,
    "bubble": sp_bubble, "bolt": sp_bolt, "crescent": sp_crescent,
    "shard": sp_shard, "drip": sp_drip, "note": sp_note,
    # new
    "heart": sp_heart, "diamond": sp_diamond, "flame": sp_flame,
    "spiral": sp_spiral, "leaf": sp_leaf, "coin": sp_coin,
}


def main():
    os.makedirs(OUT, exist_ok=True)
    names = list(SPRITES)
    for name in names:
        SPRITES[name]().save(os.path.join(OUT, name + ".png"))
    # contact sheet on a dark ground so the halos read
    cols = 7
    rows = (len(names) + cols - 1) // cols
    cell = 140
    sheet = Image.new("RGBA", (cols * cell, rows * cell), (12, 12, 20, 255))
    d = ImageDraw.Draw(sheet)
    for i, name in enumerate(names):
        im = Image.open(os.path.join(OUT, name + ".png")).resize((112, 112), Image.LANCZOS)
        x, y = (i % cols) * cell + 14, (i // cols) * cell + 6
        sheet.alpha_composite(im, (x, y))
        d.text((x, y + 116), name, fill=(200, 205, 225, 255))
    sheet.save(os.path.join(OUT, "_sheet.png"))
    print("wrote %d sprites + _sheet.png to %s" % (len(names), OUT))


if __name__ == "__main__":
    main()
