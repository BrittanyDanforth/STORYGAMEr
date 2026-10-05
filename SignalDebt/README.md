# Signal Debt — current game project and editable asset sources

The game opens to a main menu and includes the apartment, physical laptop, persistent client workstation, a paid service repair, notice recovery and local saves. Sit at the laptop to animate into its screen, then stand up to return to first person. The [shared-folder work plan](Documentation/AI_WORK_SPLIT.md) assigns UI/save/build work to Codex and world/deeper gameplay work to the other AI. The full 320-feature backlog remains the target and is **not complete**.

The current proof-recovery checkpoint passed Unity compilation, save, conversation-boundary and layout checks, followed by Linux and Windows builds. The actual recovery/payment route was played on Linux; a final pass verified the corrected Inbox view and paid-campaign reload. See [current validation](Validation/ProofRecovery/README.md) for the exact revisions and scope, and [the evidence index](Validation/README.md) for historical results. The last live dialogue attempt returned HTTP 401, so natural-response quality remains unverified. Windows execution, audio output and target-PC performance are not established by cloud Linux testing.

The [shop and save foundation](Documentation/Shop_Save_Foundation.md) provides versioned migration, purchase receipts and save-before-publish money changes. Its domain/save checks are separate from the shop encounter and art, which are still to be built. There is no playable shop in this increment.

The earlier art continuation is documented in [Quality pass — 4 October 2026](Documentation/Quality_Pass_2026-10-04.md). The official Blender **5.2.2 LTS** toolchain is now restored in the cloud workspace; its CPU render, native save and FBX export smoke checks passed. This establishes tool readiness, not final art acceptance. Historical environment, import and delivery reports describe their recorded source unless explicitly regenerated.

[Open the labelled comparisons, updated GIFs and apartment previews](REVIEW.md).

This source package includes editable Blender scenes and exports. [Neighborhood work in progress](SourceAssets/Neighborhood/README.md) contains the newer street/shop/police art, source scripts, baked maps and reviewed images outside Unity's import folder. Its remaining export/import defects are documented; it is not yet a playable map. Institutional gameplay and the larger city remain unfinished.

The supplied 68-page master plan v0.9 is included in `Documentation/Hacker_Game_Master_Plan.docx` and `.pdf`; all 320 backlog features remain. Read [Validation/README.md](Validation/README.md) for current evidence. `Validation/BUILD_STATUS.md` records the earlier upload's checks and Unity acceptance plan.

[The security notes](Documentation/SECURITY.md) describe the direct OpenAI request boundary and local key storage. The authored repair works offline. Model replies are plain text with no authority to execute commands, award money or alter game state.

**This folder is the editable source project. Runnable players are built separately with Unity 6000.0.62f1.** See the [menu update](Documentation/UI_Menu_Update.md) for current behavior and validation boundaries; a successful source package or earlier build does not certify a final player.

## Open the game project

1. Unzip the whole package into a writable folder.
2. In Unity Hub, choose **Add project from disk** and select `UnityProject`. The project records Unity `6000.0.62f1`; use that Unity 6 version or a compatible newer Unity 6 LTS editor. Let package import finish.
3. In the editor, choose **Signal Debt > Build Apartment Scene** if the scene has not already been created. Open `Assets/Scenes/Apartment.unity`, then press **Play** to open the main menu.

The first scene uses the Built-in Render Pipeline with Standard materials. The production plan retains a measured URP comparison before expanding the lighting pipeline. No paid asset pack, Cinemachine package, or AI account is needed for the offline repair loop.

## What to try first

- Choose **New Game**, or **Continue** when a saved campaign is available. Starting over asks for confirmation and archives the existing local campaign.
- Walk around the room and look down at the visible arms, legs, and feet.
- Look at the laptop and press **E** to sit; the camera moves into the ASTER desktop.
- Open **Inbox**, read the copy-shop message and accept the job. The desktop also has **Client PC**, **Proofs**, **EXODISS** and **History**.
- In **Client PC**, open the authorized session, inspect the Order Folder's input and compare the service outputs. Connections persist, including wrong ones; Undo and Unplug change the saved wiring. Ask the client to use the folder, then collect **120 credits once**. A successful owner check prepares order 1042 and leaves 1043 unchanged.
- Accept the separate **160-credit notice-recovery offer** in Inbox. In **Proofs**, compare actual saved pages with the owner's reference, restore a version, and ask the owner to export the current notice. Incorrect drafts remain recoverable; all versions are preserved. Only a verified current export permits payment, after which the document becomes read-only.
- Open **EXODISS** to inspect the receipt and make a partial rent payment.
- Choose **Stand up** to animate out of the laptop and return to first person. Inspect the rent notice, open the entrance, or rest at the bed.
- Quit and use **Continue** on the next launch to resume the saved repair, money, rent and event history. Recent chat is kept only during the current game session.

Movement uses WASD, mouse look, Shift to move faster, and Ctrl or C to crouch. E interacts with the focused object. Esc opens Pause; Resume at the focused laptop returns to that desktop. Settings can be opened from the main menu or Pause, and Back/Esc closes Settings first. The editor builder creates the required legacy input axes. Input must be set to **Input Manager (Old)** or **Both** if you change project input settings later.

## Local files and starting over

Extract the entire player folder to a writable location. The game creates `UserData` beside the executable; in the editor it uses `UnityProject/UserData`.

| Path | Contents |
| --- | --- |
| `UserData/Saves/` | Campaign progress and supported recovery files. |
| `UserData/Backups/Campaign-…/` | The local campaign archived by New Game. |
| `UserData/desktop.json` | Desktop preferences. |
| `UserData/ai-settings.json` | Optional readable plaintext key, created only when explicitly saved in Settings. |

Continue can import a supported older campaign from Unity's application-data folder when no local campaign exists. It preserves the old files and saves the imported campaign locally. Unrecoverable or newer-format local save families show an error instead of silently starting over. New Game archives local Saves while keeping desktop and AI settings separate; a failed archive/save prevents replacement. See the [menu update](Documentation/UI_Menu_Update.md) for migration details. Do not include personal `UserData` in shared builds or source packages.

The current campaign filename is `signal-debt-save-v4.json`. Versions 1–3 migrate without replacing their original files. Old first-repair receipts remain paid, and migration starts the separate proof job unaccepted rather than inventing recovery work.

## Edit the apartment in Blender

Open `Blender/SignalDebt_Apartment.blend`. It contains the room, furniture, named interaction anchors, materials, and preview cameras. The room is 6.4 by 5.2 meters with a 2.8-meter ceiling. Keep the door, bed, and workstation in their clear corner arrangement when iterating.

Use Blender **5.2.2 LTS**. In the cloud workspace, `source Tools/blender-env.sh` activates the verified pinned installation; `Tools/install-blender.sh` restores it if needed. `Blender/build_apartment.py` regenerates the scene and exports. With that version available on your PATH:

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

Open **Settings** from the main menu or Pause, enter an OpenAI API key in the masked field, then choose **Connect**. Connect applies the key and sends one small connection test; provider charges may apply. The default keeps the key for the current session. **Save key in UserData folder** explicitly saves readable plaintext to `UserData/ai-settings.json` for later launches. Keep that file private. A session-only replacement leaves an older saved key unchanged; **Remove saved key** clears saved and session keys when removal succeeds.

Once a key is available, **Inbox Send** uses the fixed OpenAI Responses endpoint directly. There is no relay toggle or Python service to start for this flow. Opening the game or Settings alone sends no AI request; Send and Connect are the explicit request actions. Missing keys or connection failures show status text, and the authored job, repair and wallet remain available.

The configured model is GPT-6 Luna with reasoning off. Requests include the clerk's personality, current concern and up to 12 recent visible dialogue lines, alongside source-linked job events the clerk knows. Private notices and rent events are excluded. Instructions distinguish reacting to an insult or joke from knowing whether a player's claim is true. This is recent conversational context, not persistent relationships or an autonomous NPC schedule. Replies change dialogue wording, not campaign state. An optional cloud `OPENAI_API_KEY` binding is separate from entering a key on the player's PC and must be supplied through secure environment settings.

The retained relay tests and historical live-provider smoke report cover the older relay implementation. They do not validate this direct provider. The user reported that direct AI replies worked but felt generic. Contract, context and key-store tests do not establish the revised dialogue's behavioral quality; its live semantic evaluation remains separate. See the [NPC dialogue update](Documentation/NPC_Dialogue_Update.md) for the current test scope.

## Next construction milestones

1. Review the revised dialogue on the user's PC and complete remaining Windows, camera/body and performance acceptance. Preserve the passing native menu, local-save and key-storage behavior.
2. Tune the revised body and animation clips in Unity, then add the nearby shop and its clerk.
3. Connect a later criminal choice to victim discovery and a supported investigation.
4. Build the interrogation, court, and custody sets from the master-plan inventories.

The source assets are the first production pass. Full facial animation, a city population planner, advanced hardware assembly, police tactics, courtroom gameplay, and the wider crime economy are not implemented in this package.
