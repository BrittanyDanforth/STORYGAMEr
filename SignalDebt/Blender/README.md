# Apartment source and exports

`build_apartment.py` builds actual editable geometry for the Signal Debt starter apartment. It creates the packed source file `SignalDebt_Apartment.blend`, Unity interchange assets, original texture maps, a collision manifest, and five rendered views. The geometry and textures are generated from a fixed seed; no external asset downloads or generated illustration stand-ins are used.

Run with a normal Blender 4.x installation:

```sh
blender -b --factory-startup --disable-autoexec -t 4 --python build_apartment.py -- --samples 256 --adaptive-threshold .015 --width 1600 --height 900
```

Use `--no-render` to regenerate only the source and interchange assets. Use `--render-only` to render the existing saved source. For the closest inspection view, add `--view Preview_05_Desk_Detail --samples 384 --adaptive-threshold .015 --width 1600 --height 900`. All rendering uses Cycles CPU with four worker threads. Denoising is disabled for compatibility with Blender distributions compiled without OpenImageDenoiser. The entrance view poses the door open for review; the source and exports retain its closed default position.

The quality pass refreshes the room, workstation and desk detail views at 1600 × 900. The detail uses a 384-sample cap and .015 adaptive threshold; the wider views use a 256-sample cap and .02 adaptive threshold. The bed/window and entrance views retain their previous 1280 × 720, 128-sample renders because none of their visible assets changed. `visual_qa.json` records the final settings and inspection results. Adaptive sampling can converge earlier in individual pixels. The previews are unretouched Blender renders; some sampling grain remains.

## Workstation quality pass

The 0.2 asset keeps the room arrangement, warm/cool lighting and gameplay anchor positions. It adds a contoured mouse with separate buttons, ribbed wheel, underside, glide pads and a connected USB cable; a 19 mm pitch laptop keyboard with modifiers, numpad and non-emissive legends; case seams, ports and support feet; and shaped chair cushions with piped edges and smooth shell normals. Supporting props now include a recessed parts tray, grounded screwdriver with grip and tip, tower feet and framed front I/O, and a notebook with covers, spine and page block. Desk grain follows the worktop length with restrained contrast. These are actual editable meshes and curves, preserved in the exported model.

The three refreshed previews show the approved 0.2 forms immediately before a final recessed mouse cable core was added to bridge a 0.37 mm shell seam. This small assembly correction is present in the final source and exports; the rendered room layout, materials and visible prop shapes are otherwise unchanged. `mouse_contact_verification.json` records actual surface intersections joining the cable, relief and shell.

## Layout and units

The interior is **6.4 × 5.2 × 2.8 metres**. Blender coordinates are X across the room, Y toward the north wall, and Z upward. The mattress and headboard are northwest; the desk and laptop are northeast. A real opening at the southeast corner leads into a short modeled corridor. Its door hinge sits at X 1.6, Y −2.63, and the leaf opens inward.

The source contains separately editable floorboards, wall segments, beveled trim, a window with real depth and mullions, curtains, radiator, exterior facade meshes, bed frame and draped blanket mesh, desk and drawers, five-caster articulated chair, laptop keyboard and hinged display, customer tower with internal components, sockets and cables, lamps, and a physical overdue rent notice.

## Unity assets

The principal Unity model is `../UnityProject/Assets/Art/Apartment/Apartment.fbx`. Export axes are forward −Z and up Y, with metre scale. Cameras and lights are excluded; named empty anchors and all visible meshes are included. The `.glb` is an additional portable inspection format, and `.obj`/`.mtl` are fallback interchange files.

Use `apartment_collision_plan.json` for deliberately simple box collision rather than adding colliders to every screw, key, or floorboard. The manifest provides Blender centres/sizes and their Unity room-space equivalents. The detailed mesh should remain visual geometry. The chair's collider should not block a seated player's controller when the interaction system places them at `DeskSeat`.

| Anchor | Purpose |
| --- | --- |
| `PlayerSpawn` | Player feet on the floor; standing eye offset 1.68 m. |
| `DeskSeat` | Seated player's feet on the floor; seated eye offset 1.18 m. |
| `LaptopFocus` | Actual screen point; use as the seated look target. |
| `DoorHinge` | Door pivot and parent of `DoorLeaf` and moving door details. |
| `DoorInteract` | Interaction position near the inside handle. |
| `BedInteract` | Accessible position beside the bed. |
| `BillInteract` | Interaction position in front of the overdue notice. |
| `PCBenchTarget` | Optional customer computer interaction target on the desk. |

The FBX uses portable base colors and PNG color textures for wood, plaster, and fabric. Blender's authored warm/cool lighting is in the source and previews. Unity's chosen rendering pipeline needs its own lights and may require emission and transparency setup for screens, lamps, glass, and distant lit windows. The PNG textures are real exported maps, not Blender-only procedural textures.

## Scope

This is a detailed original first-pass apartment, with reproducible construction and geometry ready for integration and iteration. It has not been profiled inside a running Unity build and is not represented as final AAA production art. The editable source preserves text, curve cables, cloth meshes, bevel modifiers, materials, lighting, and preview cameras; interchange exports convert lettering and curves to meshes.

`asset_verification.json` records a successful FBX round-trip: required anchors retain their source metre positions and `DoorLeaf` remains parented to `DoorHinge`. It also verifies GLB structure and the clearance between the draped blanket and mattress.
