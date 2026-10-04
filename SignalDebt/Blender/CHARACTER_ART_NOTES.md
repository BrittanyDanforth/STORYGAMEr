# Police character refinement — v0.9

The patrol and SWAT source characters have tailored cloth silhouettes, fitted equipment and authored shoulder weights. All source meshes are original local geometry. No downloaded character, plug-in, executable or texture was introduced.

## Geometry and deformation

The shoulder line descends from the neck into the deltoid. The old raised tube cap was reduced and smoothed. Upper shoulder vertices follow the clavicle; the lower deltoid progressively follows the upper arm. Chest-to-arm weighting is explicit and continuous, including a feathered lower transition at the rear armpit. The epaulets and SWAT loop panels use the same cloth-weight field, so they do not float as rigid accessories when the shoulder moves.

The patrol collar, pockets, pocket flaps, buttons and placket follow the curved shirt. The pocket layout and microphone position were adjusted after actual animated-surface checks identified their contact with the upper sleeves. The earlier wrist/finger anatomy and articulated hand rig are retained. Joint positions, 52 bone names, player view markers and body-region contract remain unchanged.

## Equipment attachment contract

| Item | Attachment | Appearance and occupancy |
|---|---|---|
| Patrol pockets, badge, microphone | Chest | Fitted and shifted inward to clear the posed sleeves |
| Patrol epaulets | Blended Chest / Shoulder / UpperArm | Surface skin weights, not rigid floating tabs |
| SWAT plate carrier, pouches and webbing | Chest | Tapered upper corners, separate bound edges, low-profile webbing and closed pouch flaps |
| SWAT radio pouch, case and cable | Chest | Distinct cloth and molded materials |
| SWAT helmet, rail, ear protection and chin retention | Head | Connected layers and fitted retention pad |
| SWAT sleeve loop panels | Blended shoulder cloth weights | Thin panels aligned to the arm surface |
| Knee cups | LowerLeg | Shaped shell over cloth backing |
| Belt, medical pouch and molded holster | Hips | Anatomical-right holster; patrol empty, SWAT occupied |
| SWAT holstered prop | Hips | Inert exterior only; no mechanism, bore or firing behavior |

Equipment is merged into the existing eight skinned renderer regions. Semantic materials and the rig's `equipment_manifest_json` support inspection after merging. The patrol animation supplies its separate held prop; its base holster is empty, avoiding a duplicate sidearm.

## Files and evidence

- `build_characters.py` regenerates `Characters.blend`, all five base FBXs, the rig contract and character statistics.
- `verify_characters.py` actually reimports all five exported FBXs and checks structure, handedness, normalized skin weights and deformation.
- `verify_police_surface_orientation.py` checks signed volumes of closed mesh components. Mirrored panel faces are oriented outward for engine backface culling; open skin-region seams are outside that volume check's scope.
- `render_police_character_details.py` produces the rest-mesh upper-body and equipment previews.
- `Renders/Characters_Roster.png` shows the current full cast.
- `Renders/CharacterDetails/character_detail_manifest.json` binds new close previews to the source hash.
- `Validation/character_roster_translation_fix.json` records a later patrol display-platform translation with exact local asset equivalence and byte-identical base FBXs. Retained detail/player previews keep their actual render-source hash; the corrected full roster was rendered again.
- Animation clearance and grip evidence are separate from the rest-mesh previews; see the animation and whole-body validation reports.

These are editable stylized adult characters. The judge and Mara retain their earlier rigid-region animation stage. Facial animation, final production retopology, runtime cloth collision and polished locomotion remain unfinished. A Blender mesh/pose pass is not Unity compilation, Play Mode validation, or a hardware performance result.
