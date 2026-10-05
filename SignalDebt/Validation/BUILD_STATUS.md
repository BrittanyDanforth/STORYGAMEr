# Apartment and character motion milestone — 4 October 2026

This document records the earlier apartment/character delivery below. **Current update:** Unity 6000.0.62f1 is installed; actual SIGNALDEBT asset import, C# compilation and the Windows player build have succeeded. The native Linux apartment/PC/first-repair sequence and save/reload have been tested. Graphical Editor Play Mode licensing failed; Windows execution, audio and GTX 1080 Ti performance remain unverified. See [the PC interaction milestone](../Documentation/PC_Interaction_Milestone.md) for the current gameplay continuation. Historical Blender checks below keep their original scope.

The v0.9 body pass corrects the patrol elbow plane and police shoulder weighting, revises garment fit, and refines SWAT and patrol equipment. It preserves the earlier wrist/finger work while reviewing the arms against the actual torso and carrier surfaces. See `whole_body_review.json` for the current evidence and its explicit limits. The previous focused wrist approval did not establish shoulder clearance. Gameplay source and the apartment remain unchanged; character assets and the master plan are updated.

## What was built

- Furnished 6.4 × 5.2 m apartment: bed, desk, chair, laptop, service tower, window, radiator, lighting fixtures, cables, rent notice, hinged entrance, and short corridor.
- Actual `.blend` sources, FBX/GLB/OBJ exports, original color maps, named interaction anchors, simple collision plan, and Blender previews including a close workstation detail view.
- Five stylized adult characters with revised 52-bone rigs. Player, patrol, and SWAT have continuous weighted arms and legs, plus articulated fingers. Judge and civilian retain simpler rigid clothing and are not presented as finished animated characters.
- Authored SWAT door-kick and patrol lowered/raised hold studies, baked FBX clips, editable Blender animation source, contact/grip renders, and a motion preview. These do not implement police AI or combat.
- First-person source now uses articulated limbs for crouching/seating and preserves body scale. Arms, hands, legs, shoes, and the trouser waist are camera-visible; head, neck, and chest renderers are hidden while the full skeleton remains active.
- Unity source for first-person movement, visible body, seating, E interactions, notification sound/glow, laptop interface, guided repair, EXODISS receipt, one-time payment, rent, resting, and local saves.
- Optional localhost dialogue relay using saved, NPC-scoped event knowledge. Game state remains authoritative. Offline authored replies are included.
- Updated 68-page master plan v0.9 with concrete room inventories, body/equipment acceptance gates, the next production sequence, and the full 320-feature backlog. Backlog entries are planned work, not completed features.

## Checks actually run

| Check | Result and scope |
| --- | --- |
| Blender construction and export | Ran in Blender 4.0.2; actual editable sources and exports produced. |
| Apartment FBX round-trip | Required anchors match source positions within 0.0001 m; door remains under its hinge. GLB length and anchor nodes pass. See `../Blender/asset_verification.json`. |
| Character FBX round-trip | Revised rigs, anatomical sides, weights, and attachment markers checked after export. See `../UnityProject/Assets/Art/Characters/fbx_roundtrip_verification.json`. |
| Whole-body correction | All four clips are sampled across 214 authored frames, with actual mesh, equipment, multi-angle views, and nine exported FBX files reviewed separately. `whole_body_review.json` records the bounded result. A small inner-armpit attachment fold remains; this is not a claim that every joined cloth surface is disjoint. |
| Blender embedded-content inventory | See `../Blender/asset_security_check.json` for final source hashes and executable-content/reference inventory. Inspection disables automatic execution and leaves files unchanged; this is not a Blender binary vulnerability audit. |
| Visual inspection | Actual Blender renders checked at room, prop, limb-joint, grip, and contact distances. Preview lighting and motion are rendered in Blender, not captured from Unity. |
| C# syntax | See `csharp_syntax.json` for the current source inventory. Parsing does not validate Unity API references or behavior. |
| Progression/economy domain | Actual domain class and portable harness compiled with Mono; 148 assertions passed, including repeated payout rejection and invalid transitions. No Unity runtime used. |
| AI relay suite | 27 tests passed using fake providers or localhost HTTP; no paid calls in the suite. |
| Live AI smoke tests | Two separate provider calls succeeded. The relay test reopened SQLite, recalled the client's repair/payment, and excluded a private fact known by someone else. These preceded final transport hardening, which was tested offline. |
| Security review | Fixed provider redirect handling; endpoint and injection regressions pass. Authored runtime code exposes only the optional loopback/API path. See `../Documentation/SECURITY.md` for scope and limits. |
| Master document | Version 0.9 has 68 pages. Fifty-three content pages matched the previously inspected rendering; all 15 changed pages were inspected at full resolution. All 320 feature entries are retained. |

## Required Unity acceptance pass

1. Import with the recorded Unity 6 editor, confirm the console has no errors, and run **Signal Debt > Build Apartment Scene**.
2. Check FBX orientation, metre scale, textures, lights, collider alignment, open doorway, and chair clearance.
3. Walk, crouch, look down, sit, and stand. Check arm/leg visibility, anatomical sides, shoe contact, knee clearance, hand orientation, and clipping at the desk. The procedural poses need in-engine tuning.
4. Build the separate Animation Review scene using its editor menu. Check clip names, model orientation, the planted support foot, kick contact, finger placement, and all hold transitions. This scene is a review tool; it does not replace the apartment scene or implement an arrest.
5. Aim at the laptop, bed, rent notice, and door from normal angles. Confirm each E action and the open door's close action.
6. Complete the repair. Try incorrect ordering, the wrong connection, and repeated payment clicks. Confirm the balance changes from 80 to 200 once.
7. Pay rent, use the single extension, rest, restart Play Mode, and verify money, bill state, and history persist. Check backup recovery and unwritable-save feedback on the target platform.
8. Try client chat with the relay absent, offline, and enabled. Close the conversation during a request; confirm late replies are discarded and long replies remain readable.
9. Build for Windows, trace connections with AI off/on, and profile on the actual PC before making network-behavior, frame-rate, or capacity claims.

## Not implemented in this milestone

The open city, shops and shop NPC gameplay, physical component replacement, criminal operations, social-engineering operations, laundering economy, active victim computers, investigations, court hearings, custody gameplay, police tactics, and autonomous population simulation remain upcoming work. Their design and build dependencies are in the master plan. The character models do not yet imply functioning police or court systems.

The recorded domain and AI suites remain applicable to their unchanged sources; `unchanged_regression_sources.json` records earlier byte comparisons and `v09_unchanged_runtime.json` records the current pass. No paid AI call was made for this character pass, and no third-party character model or add-on was imported. Reference pages informed the shapes only.

Close-range garment seam shading and fit still need production polish. The corrected arm and elbow paths, equipment, and hand grip are a bounded source-art improvement. Do not treat numerical clearance, successful file import, or a rendered preview as final AAA art acceptance or runtime cloth collision.

The next gameplay proof is the nearby shop and one later criminal choice that a victim discovers, creating a supported investigation. Expand from that complete consequence chain after the apartment acceptance pass succeeds.
