# Unity starter implementation

This is the first apartment and paid-repair slice, with an articulated first-person body and a separate police-animation review scene. It is source for Unity 6 (6000.0.62f1), built-in rendering, uGUI 2.0.0, and Unity's Animation module. It does not yet implement the larger city, crime campaign, investigations, courts, prison, PC assembly, or a finished character animation set.

## Open and build

Open `UnityProject` in the stated Unity editor. Allow model and script imports to finish. Use **Signal Debt → Build Apartment Scene**, open `Assets/Scenes/Apartment.unity`, and press Play. The editor script may create this scene automatically on a clean first import when the apartment FBX exists; it deliberately avoids replacing an existing, saved, or modified editing scene. Rebuild through the menu after changing source models or the collision manifest. A manual rebuild replaces the generated scene, so retain custom scene edits in a separate scene.

The builder accepts `Assets/Art/Apartment/Apartment.fbx` or `SignalDebt_Apartment.fbx`. It creates collision boxes from `apartment_collision_plan.json`, mapping supplied coordinates against actual imported anchors. This avoids detailed mesh colliders on every prop. Dynamic door collision follows `DoorHinge`; the interaction controller rotates around world up and returns to the imported closed rotation. The laptop and bed's solid collision surfaces themselves accept interaction, in addition to the visible-object anchor triggers.

Required apartment anchors: `PlayerSpawn`, `DeskSeat`, `LaptopFocus`, `DoorHinge`, `DoorInteract`, `BedInteract`, and `BillInteract`. Missing anchors have fallback positions intended for debugging. Missing art falls back to a basic room, rather than representing finished art. Imported unsupported/pink materials receive built-in fallback materials; review glass, emission, and textures in the editor.

The builder configures legacy Input Manager axes: Horizontal, Vertical, Mouse X/Y, Mouse ScrollWheel, Submit, and Cancel. Project **Active Input Handling must be Input Manager (Old) or Both**; this starter uses `UnityEngine.Input`. It has no Cinemachine or new Input System dependency.

## Controls and first loop

- WASD moves; mouse looks; Ctrl or C crouches; Shift moves faster. Look down to see arms, hands, trousers, and feet. The clothed waist keeps the trousers continuous; the chest, neck, and head are hidden from the first-person view.
- E interacts with the laptop, rent notice, bed, and door. At the desk, mouse input controls the laptop interface. Esc or Leave closes the interface, releases the seated pose, and returns to the previous standing position.
- Accept Southbank Copy Shop's scoped repair. Inspect Order Folder status, connect Order Index, run the client test, and complete the job. Wrong resources and premature actions explain the missing step without penalties. The agreed 120-credit fee is awarded once.
- EXODISS starts with 80 credits and 450 overdue rent. After the fee, balance is 200. Partial payments are capped by available funds and outstanding debt. One two-day extension is available. Rest advances a day without inventing income or clearing bills.
- History records actual state changes and client memories. The slice remains playable after the rent deadline; eviction and later consequences are not implemented.

A short synthesized incoming-message beep and laptop glow introduce the job. The runtime uGUI interface uses built-in LegacyRuntime font, with Arial fallback. Main content scrolls; long relay messages retain full text in a scrollable Inbox card and use a short, bounded notification preview. No web browser engine or outside financial account is involved.

## Body and interaction contracts

`Assets/Art/Characters/PlayerBody.fbx` is an approximately 1.753 m adult avatar with 52 bones and eight skinned body-region meshes. `Player_Arm_L/R`, `Player_Leg_L/R`, and `Player_Hips` remain visible. The hips region is the clothed waist, retained to close the trouser silhouette when looking down. Player-only sleeve closures belong to the visible arm regions. `Player_Chest`, `Player_Neck`, and `PlayerHead` use `ShadowsOnly`; their bones and objects stay active. This fixes exposed garment partition rims in the standing/crouched views without masking them through camera changes. The renderer policy does not delete bones or detach limbs. The runtime disables the imported Animator because this player is posed procedurally. Body renderers use Ignore Raycast and have collision removed; the CharacterController handles locomotion.

Orientation is derived from the prefixed `BodyOrigin`, `BodyForward`, and `BodyUp` markers, with a foot/toe fallback. The controller checks that the resulting left-side joints are on player-local negative X, verifies the height, grounds the measured shoe bounds, and records imported chain lengths in `RigSummary`. Exact source coordinates, region names, anatomical side convention, and marker names are in `Assets/Art/Characters/rig_contract.json`. Player upper/lower arm lengths are approximately 0.260/0.227 m; upper/lower leg lengths are 0.352/0.369 m. Invalid rigs fall back to an articulated arms-and-legs model.

Each limb uses an analytic two-bone solve with preserved segment lengths and rest rotations. Crouching lowers the pelvis by up to 0.42 m, bends knees, and leans the spine; no body scale is changed. During walking, extra pelvis lowering keeps the requested ankle positions within reach. Wrists and ankles compensate for upstream rotations. This is procedural locomotion without world-space foot locking, terrain adaptation, clothing collision, or a production animation blend tree; some horizontal foot sliding remains possible.

The free-look camera uses the artist's `Player_EyeView` marker and `Head` bone pivot. In the grounded rest pose the pupil center is approximately 1.614 m high and 0.099 m forward, with the head pivot at 1.482 m. Looking down rotates this eye offset about the real head joint rather than rotating a camera above and behind the eyes. The crouched eye derives from the actual pelvis/spine pose, with the head counter-rotating the 15-degree spine lean to maintain the requested view pitch. Only the extra procedural stride reach-drop is canceled at the camera to avoid forced walking bob. Vertical FOV remains 76 degrees; look limits remain −75 to +86 degrees. Seated presentation retains its separately approved camera contract below.

At the laptop, the pelvis lowers 0.40 m and moves 0.25 m toward the desk, with a 26-degree spine lean and bent knees. The seated camera is 1.18 m above the seat's floor anchor, 0.34 m forward. The apartment builder creates `KeyboardWristLeft` and `KeyboardWristRight` targets approximately 0.884 m above the room floor—about 28 mm above the modeled key tops. The 3 mm clearance adjustment follows a posed mesh check that found a slight finger/key overlap. `PalmTip` and `PalmBack` markers define a complete hand orientation frame so the fingers face forward and the backs of the hands face up. Leaving the desk restores the previous position, view, and crouch height.

Numeric reach checks and Blender pose images are geometry checks, not Unity Play Mode validation. Body facing after actual Unity import, near-plane intersections, skinned-mesh bounds, shoulder seams, finger/key clearance, transitions into sitting, headroom checks, and moving-door collisions still require the editor pass.

## Separate animation review

Use **Signal Debt → Build Animation Review Scene** to create `Assets/Scenes/AnimationReview.unity`. This explicit command preserves the apartment's build settings and never runs automatically. It imports the four FBXs in `Assets/Art/Animations`: `SWAT_DoorKick`, `Patrol_LowReadyIdle`, `Patrol_LowReadyToAim`, and `Patrol_AimToLowReady`.

`SignalDebtRigImporter` configures Generic avatars, preserves named bones and markers with `optimizeGameObjects = false`, and retains each FBX take's actual frame limits. Only LowReadyIdle loops. Imported animation events are empty, and Animator root motion is disabled. The review scene uses its own PlayableGraph, with a 0.20-second blend between selected clips; it has no reference to game saves, police decisions, or apartment door state. The door-sized frame is a static contact reference for the authored kick.

Press the named clip buttons to play. Keys 1–4 select kick, raise, lower, and idle. Each actor has a scrub bar; Space or Pause/Resume pauses and resumes playback. Completed clips restart when resumed. Playback speed ranges from 0.1× to 1.5×. Right-drag orbits the camera, the wheel outside the control panel zooms, Focus SWAT/Focus patrol selects an actor, and Front/Side buttons set inspection views. Both actors restores the wide view. Side inspection approaches the selected actor from outside the pair so the other officer does not obstruct it. The review controls scroll on smaller displays. Playables are destroyed when the scene closes.

This scene provides a review tool, not verified in-engine animation footage. Inspect skin deformation, palm/prop contact, support feet, kick contact and recovery, take binding, clip timing, and blending after Unity imports the FBXs. The clips do not implement a police simulation or weapon behavior.

## State, saving, and optional dialogue

`SignalDebtSaveData.cs` is independent of Unity. It owns guarded repair, payment, rent, extension, and day transitions. Every successful transition appends a unique event with a monotonically increasing world revision. Repeated repair completion cannot award money or duplicate memories. Validation rejects inconsistent prerequisite chains, paid-state/receipt disagreement, null or duplicate ledger records, and discontinuous revisions.

`SignalDebtSaveStore.cs` writes `signal-debt-save.json` in `Application.persistentDataPath`. A complete temporary snapshot is serialized, read back, and validated before replacement. A `.bak` snapshot and `.tmp` recovery path support interrupted writes. File replacement is used where available; the fallback is recoverable, not a promise of atomic or durable filesystem behavior on every platform. Save errors remain visible in subsequent feedback and the laptop status rather than being replaced with an unconditional success claim. Game state may continue in memory if disk saving fails.

`SignalDebtNpcClient.cs` is a separate optional local-relay adapter. It is disabled by default and contains no API key. Generated wording cannot award money, change mission state, or create evidence. The UI sends bounded client questions; leaving it cancels pending requests. Client knowledge is limited to accepted work, the client-observed test, and their payment. Private rent and diagnostic events advance revision without becoming client knowledge. The full conversation is not persisted by this starter; committed game events and authored client memories are persisted.

## Validation and remaining check

Portable domain scenario harness: `UnityProject/Assets/Editor/SignalDebtDomainTests.cs`. On a normal Mono installation, from `UnityProject`:

```sh
mcs -define:SIGNAL_DEBT_PORTABLE_TEST -out:/tmp/signal-debt-domain-tests.exe Assets/Scripts/SignalDebtSaveData.cs Assets/Editor/SignalDebtDomainTests.cs
mono /tmp/signal-debt-domain-tests.exe
```

The harness exercises missing prerequisites, wrong-resource behavior, exact reward, 100 duplicate reward attempts, partial and capped payments, insufficient funds, extension limits, day advancement, event revision ordering, and corrupt snapshots. It is excluded from the normal Unity build unless that explicit compilation symbol is set. It does not test Unity physics, JsonUtility, rendering, audio, runtime layout, imported FBX animation, or platform-specific file replacement.

Before calling this slice Play Mode verified, run the actual editor: walk around the room, look down, crouch below furniture, open and close the door from both sides, target laptop and bed solids, sit and leave, finish the repair, try duplicate completion, pay rent, restart Play Mode to confirm restore, and inspect the UI at 1080p. Then test writable and unwritable save locations and a long dialogue response. No Unity engine execution or 1080 Ti performance measurement is claimed by source review or the portable harness.
