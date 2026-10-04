# Signal Debt — apartment and character motion build

The current cloud continuation is documented in [Quality pass — 4 October 2026](Documentation/Quality_Pass_2026-10-04.md). It uses verified official Blender **5.2.2 LTS**. Fresh environment and import checks are in `Validation/CloudSetup`; the original delivery reports below describe the earlier build unless explicitly regenerated.

[Open the labelled comparisons, updated GIFs and apartment previews](REVIEW.md).

This package starts the game with the apartment and the first paid repair. It contains actual Blender scene files and exports, a Unity 6 source project, and an optional local NPC dialogue relay. The courtroom, interrogation room, jail cell, shops, and larger city are the next environment milestones in the master plan.

The supplied 68-page master plan v0.9 is included in `Documentation/Hacker_Game_Master_Plan.docx` and `.pdf`; all 320 backlog features remain. Read [Validation/README.md](Validation/README.md) for current evidence. `Validation/BUILD_STATUS.md` records the earlier upload's checks and Unity acceptance plan.

`Documentation/SECURITY.md` records the runtime network boundary and security review: offline gameplay, optional loopback NPC relay, and the fixed OpenAI endpoint. Model replies are plain text with no authority to execute commands or alter game state.

**This is a source project, not a Windows executable. Unity compilation and Play Mode still need to be checked in Unity on your computer.** The included validation report distinguishes the checks run here from those remaining in the editor.

## Open the game project

1. Unzip the whole package into a writable folder.
2. In Unity Hub, choose **Add project from disk** and select `UnityProject`. The project records Unity `6000.0.62f1`; use that Unity 6 version or a compatible newer Unity 6 LTS editor. Let package import finish.
3. In the editor, choose **Signal Debt > Build Apartment Scene** if the scene has not already been created. Open `Assets/Scenes/Apartment.unity`, then press **Play**.

The first scene uses the Built-in Render Pipeline with Standard materials. The production plan retains a measured URP comparison before expanding the lighting pipeline. No paid asset pack, Cinemachine package, or AI account is needed for the offline repair loop.

## What to try first

- Walk around the room and look down at the visible arms, legs, and feet.
- Look at the laptop and press **E** to sit and use it.
- Read the copy-shop message, accept the job, inspect the service status, restore the correct connection, and test it.
- Close the completed repair and receive the agreed **120 credits once**.
- Open **EXODISS** to inspect the receipt and make a partial rent payment.
- Leave the desk, inspect the rent notice, open the entrance, or rest at the bed.
- Stop and restart Play Mode to check that the repair, money, rent, and client history persist.

Movement uses WASD, mouse look, Shift to move faster, and Ctrl or C to crouch. E interacts with the focused object. Esc leaves the laptop or opens the pause interface. The editor builder creates the required legacy input axes. Input must be set to **Input Manager (Old)** or **Both** if you change project input settings later.

## Edit the apartment in Blender

Open `Blender/SignalDebt_Apartment.blend`. It contains the room, furniture, named interaction anchors, materials, and preview cameras. The room is 6.4 by 5.2 meters with a 2.8-meter ceiling. Keep the door, bed, and workstation in their clear corner arrangement when iterating.

`Blender/build_apartment.py` regenerates the scene and exports. With Blender available on your PATH:

```powershell
blender --background --factory-startup --disable-autoexec --python-exit-code 1 --threads 4 --python Blender/build_apartment.py -- --samples 64
```

The Unity interchange file is `UnityProject/Assets/Art/Apartment/Apartment.fbx`. The same folder includes a GLB, an OBJ fallback, base-color textures, and a collision/anchor manifest. `Blender/Previews` contains actual Blender renders of the modeled scene.

Preserve the named anchors `PlayerSpawn`, `DeskSeat`, `LaptopFocus`, `DoorHinge`, `DoorInteract`, `BedInteract`, and `BillInteract`. The first two describe floor-level body positions. `DoorLeaf` belongs under the hinge pivot. Do not move the mesh away from its pivot to compensate for an import problem.

`Blender/Characters.blend` contains five editable stylized adult characters: player, civilian, judge, patrol officer, and SWAT officer. Separate FBXs are in `UnityProject/Assets/Art/Characters`. The revised rigs have 52 bones, including articulated fingers and toes. The player, patrol, and SWAT have continuous, weighted sleeves and trousers; the judge and civilian retain simpler rigid clothing regions and need a later deformation pass. See the rig contract and round-trip report in that folder.

The first-person controller keeps the full skeleton but hides head, neck, and chest from the camera. Arms, legs, hands, shoes, and the trouser waist remain visible; closed sleeve surfaces prevent exposed mesh interiors when looking down. Crouching and seating bend the limb joints while preserving body scale. Standing, seated, and crouched poses still require their final camera, chair, and clipping checks in Unity.

`Blender/Character_Animation_Showcase.blend` and `UnityProject/Assets/Art/Animations` contain a door-kick study and patrol lowered/raised hold transitions. Current motion previews and close-ups are in `Blender/Renders/QualityPass`; `Blender/Renders/Animation` preserves the uploaded previews. `Blender/ANIMATION_NOTES.md` describes the clip timing and validation scope. These assets are animation studies, not functioning police AI or combat.

The body correction addresses the upper arms folding into the chest as well as the earlier wrist and finger problems. The patrol elbow plane is body-aware, and the police garments have explicit shoulder weighting and revised sleeve roots. The SWAT outfit has a shaped carrier, fitted straps, layered pouches, helmet components, ear protection, radio, and a belt-mounted holstered prop. `Documentation/Art_References.md` records the reference-only approach; all delivered geometry remains authored in this project.

Current `Validation/quality_pass_*.json` reports record the revised source, surface-clearance, visual, and exported-asset evidence. `Validation/whole_body_review.json` belongs to the original upload. Wrist checks alone do not establish shoulder quality. The models remain stylized production studies, with Unity playback and final art acceptance still distinct from the offline checks.

In Unity, choose **Signal Debt > Build Animation Review Scene**, then press Play. Use the clip buttons or keys **1–4**, **Space** to pause, right-drag to orbit, and the mouse wheel outside the controls to zoom. Each actor has a timeline scrubber; playback speed and front/side views are adjustable. The builder saves `Assets/Scenes/AnimationReview.unity` and preserves the apartment build settings. Its clip import and playback still need the Unity acceptance pass.

## Optional NPC dialogue

The offline game works first. To enable the optional relay, see `AI/README.md`, run `python AI/relay.py`, then enable the local relay through the client-chat interface. The relay listens at `127.0.0.1:8765` and reads `OPENAI_API_KEY` from its own process environment. No key belongs in Unity assets or a save file.

The configured primary is GPT-6 Luna, with reasoning explicitly off for short replies. GPT-6.1 Sol is an optional, separately enabled escalation candidate. NPC memories come from source-linked saved events, not the model's ability to retain this development conversation. The first client's knowledge includes their contract, the successful test they witnessed, and the payment; private rent events remain excluded.

The included tests cover memory persistence, knowledge filtering, relevant-history retrieval, stale responses, duplicate events and requests, cancellation, fallback, and limits. A live provider smoke-test result is included separately; it is not a substitute for testing a full campaign.

## Next construction milestones

1. Validate movement, seating, material import, the repair, save/load, and optional dialogue in the Unity editor.
2. Tune the revised body and animation clips in Unity, then add the nearby shop and its clerk.
3. Connect a later criminal choice to victim discovery and a supported investigation.
4. Build the interrogation, court, and custody sets from the master-plan inventories.

The source assets are the first production pass. Full facial animation, a city population planner, advanced hardware assembly, police tactics, courtroom gameplay, and the wider crime economy are not implemented in this package.
