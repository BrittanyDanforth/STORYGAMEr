# STORYGAMEr — Roblox slop-game workshop

Design research and buildable code for a Roblox coin-pusher luck game.

- **`docs/slop-game-dossier.md`** — the full design dossier: economics, teardown of the
  ball-drop formula, 2026 policy constraints, game concepts, saturation map, and the
  walk-around form-factor argument. `docs/dossier.html` is the styled version.
- **`src/PusherMachine.luau`** — builds a complete coin pusher cabinet from Roblox
  primitives. No mesh assets required.
- **`scripts/command-bar-build.lua`** — paste into Roblox Studio's command bar to
  generate a working, moving machine in about ten seconds. Generated from the module,
  so the two can't drift apart.

## Running it — step by step

**Important: the ModuleScript alone does nothing.** A ModuleScript is a library; it only
runs when something `require`s it. If you put the module in ReplicatedStorage, press
Play, and nothing appears — that's why. You need both objects below.

**Path A — the real setup (two objects):**

1. In the Explorer, hover **ReplicatedStorage** → **+** → **ModuleScript**. Rename it
   exactly `PusherMachine`. Delete its placeholder contents and paste in all of
   `src/PusherMachine.luau`.
2. Hover **ServerScriptService** → **+** → **Script** (a regular Script — *not* a
   LocalScript, *not* another ModuleScript). Delete its placeholder contents and paste
   in all of `scripts/ServerInit.server.lua`.
3. Press **Play**. The cabinet builds at the origin, the pusher starts cycling, and
   tokens rain in.

**Path B — zero setup (10 seconds):** open **View → Command Bar**, paste the entire
contents of `scripts/command-bar-build.lua` into the command bar (the one-line text box
at the bottom — not into any script), press Enter. Same machine, no objects needed.
Note the command bar builds it at edit time, so the pusher only cycles and coins only
fall once you press Play.

**If it still doesn't work**, open **View → Output** and read the red line — it names
the script and line number. The two classic mistakes: the ModuleScript isn't named
exactly `PusherMachine` (the runner waits forever on `WaitForChild`), or the runner was
pasted into a LocalScript/ModuleScript instead of a Script (it never executes).

Every dimension lives in the `CFG` table at the top of the module. Edit and rebuild.

## Seeing it without Studio

The repo carries its own preview pipeline so the cabinet can be designed and reviewed
headlessly:

```
lua5.1 tools/dump_parts.lua > parts.json          # runs the real builder against a Roblox stub
python3 tools/render_preview.py parts.json hero.png        # shaded 3/4 view
python3 tools/render_preview.py parts.json front.png front  # straight-on view
```

`tools/roblox_stub.lua` mocks just enough of the Roblox API (Instance tree, CFrame math,
Enums) to execute `PusherMachine.build()` outside Studio; the renderer draws the captured
geometry with flat shading, neon glow and glass transparency. Renders of the current
geometry live in `docs/pusher-hero.png` and `docs/pusher-front.png`. Two known renderer
approximations: glow bleeds through walls (Neon doesn't emit light in Studio), and
SurfaceGui text draws on top rather than being occluded.

## What can be built from primitives, and what can't

**No modelling tool needed — all of this is boxes, cylinders and spheres:**

- the cabinet, glass, marquee and neon trim
- the playfield, the servo-driven pusher, the payout ledge and tray
- coins and prize capsules
- the arcade hub itself: walls, floors, machine housings, signage
- all UI — built from Roblox GUI objects, not images

The arcade look comes from `Material = Enum.Material.Neon` on thin parts plus `Glass`
and `Metal` on the shell. That is the entire art direction, and it costs nothing.

**Needs an actual artist or a marketplace asset:**

- **Index prize collectibles** (the plushies, hats and auras players chase). These want
  real meshes. Three workable options: build them chunky out of primitives so they match
  the arcade aesthetic; pull free models from the Creator Store; or commission a set.
- **Image textures and decals.** Anything that is a picture rather than geometry has to
  be authored in an image editor and uploaded.

**Not possible from here at all:** uploading anything to Roblox. Assets, meshes, images
and the place file all have to go through your own Studio session and Roblox's
moderation pipeline.

## Implementation notes worth keeping

- **The pusher is unanchored and driven by a `PrismaticConstraint` servo, not by
  CFrame.** An anchored part moved by CFrame does not reliably push unanchored parts —
  coins tunnel through it and jitter. The constraint keeps it on rails and pushes the
  pile properly. This is the single detail that decides whether the machine feels real.
- **Coin elasticity is near zero** (`PhysicalProperties.new(2, 0.4, 0.05, 1, 1)`).
  Bouncier coins turn the pile into popcorn.
- **The deck is low-friction** so the pile slides forward instead of grinding to a halt.
- **Roblox's `Cylinder` shape puts its circular faces on the local X axis**, so a coin
  has to be rolled 90° about Z to lie flat. That's the `FLAT_COIN` constant.
- **The camera mount is a separate part**, so walking up tweens the camera into the
  cabinet while the avatar keeps standing there — passers-by still see them playing.
