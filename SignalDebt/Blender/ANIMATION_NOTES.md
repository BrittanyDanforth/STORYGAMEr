# Signal Debt character performance studies

The source authoring script reads `Characters.blend` and writes a separate `Character_Animation_Showcase.blend`. It never overwrites the base character source. Source-open calls explicitly disable embedded script execution. The delivered gameplay does not run these offline authoring scripts.

## Actions and export contract

| Action / FBX basename | Frames | Rate | Duration | Loop |
| --- | --- | --- | --- | --- |
| SWAT_DoorKick | 1–61 | 30 fps | 2.0 seconds | No |
| Patrol_LowReadyToAim | 1–46 | 30 fps | 1.5 seconds | No |
| Patrol_AimToLowReady | 1–46 | 30 fps | 1.5 seconds | No |
| Patrol_LowReadyIdle | 1–61 | 30 fps | 2.0 seconds | Yes |

FBX files are in `UnityProject/Assets/Art/Animations`. Each exports a single active action, the character, its Generic armature, attachment anchors and the fictional sidearm where applicable. Source coordinates are meters, Z up and -Y forward; the FBX convention matches the base cast, -Z forward and Y up. The named rig roots are `SWAT_Officer_Rig` and `Patrol_Officer_Rig`. No Humanoid compatibility claim is made. Root motion remains in place.

Keyframes are sampled every frame. Their target curves use smoothstep easing, and quaternion signs are kept continuous before writing keys. The baked clip samples use linear interpolation to preserve the authored per-frame performance. These are short, stylized animation studies, not a complete locomotion controller.

## Kick study

The named moments are anticipation at frame 9, lift at 17, contact at 25, recovery at 38 and settled at 61. A two-segment solver keeps the Left support ankle planted while the Right leg performs the gesture. It limits extension to avoid a locked or hyperextended knee. Contact position is derived from the evaluated shoe sole geometry, not guessed solely from the ankle target. `animation_manifest.json` records the measured contact gap, support-sole height, maximum support-ankle drift and minimum knee flexion.

The wrist correction makes the hand's long axis follow the actual forearm direction throughout the kick. The earlier world-fixed hand pose produced a visibly broken wrist angle and is superseded. Fingers close into a compact fist; thumb opposition is posed separately from the four fingers. The original lower-body trajectory and contact timing are preserved.

`Validation/kick_trajectory_retention.json` compares the source matrices at all 61 frames with the pre-shoulder-fix delivery: the root, torso/head chain and both complete leg/foot/toe chains match exactly. It records both source hashes. The shoulder and arm chains are intentionally different.

The hinged demo door is a separate, nondestructive Blender presentation prop. Its visual action responds after frame 25. It has no destructive geometry, physics force, tactical instruction or gameplay door command. A later game integration must explicitly authorize its own door-state transition; the animation marker itself does not open a real game door.

## Patrol hold study

The officer uses a two-handed prop hold with animated transitions between lowered and raised poses. The index finger is authored along the exterior of the prop. The prop is a fictional solid silhouette with simple exterior shapes; it has no trigger mechanism, ammunition, moving internals or firing behavior. Grip quality must be assessed from the close-up render in addition to wrist-target measurements.

The elbow plane is fixed outward and downward relative to the torso. The earlier palm-only solver reduced wrist angle by bringing the elbows almost onto the sternum; that pulled the upper sleeves through the chest. The revised solver rotates the clavicles forward at unchanged bone length and positions the entire grip farther forward. Patrol clavicle protraction moves continuously from 16 degrees at low-ready to 24 degrees when raised; SWAT uses 8 degrees. These values are authored for this character's proportions, not a universal anatomical or retargeting rule.

The hand and finger transforms remain fixed relative to the fictional prop while the whole reach changes. Each finger's three segments are posed separately. Digit orientation is expressed in the moving prop's reference frame, so fingers and nail surfaces retain their roll while the prop is raised and lowered. The hold remains authored animation rather than a physics contact solver. Both sides of the grip and the full lowered/raised poses have separate visual checks.

`animation_manifest.json` records every sampled elbow position, clavicle angle, elbow bend, palm-to-forearm angle, and hand-target error. Authoring fails if a hand cannot reach its target within 0.01 mm or if a patrol palm-to-forearm angle exceeds the authored 32-degree envelope. These limits check pose consistency; they do not establish anatomical correctness or sleeve clearance. The evaluated sleeve meshes require their own independent test and front, side, top, and rear visual review, including low-ready, mid-raise, aim, kick guard, contact, and recovery.

The hand and finger actions target the revised, anatomically named 52-bone cast. Anatomical Left is source +X; Right is -X. In the relaxed rest pose, thumbs sit laterally, palms face -Y and nails face +Y. Animation authoring reads actual bind bone coordinates. The final proof renders and FBXs use the corrected hand anatomy; the earlier mirrored-hand candidate is superseded.

## Evidence and limits

`Blender/Renders/Animation` contains renders of the actual character meshes and a motion review video generated from the authored frames. The source retains editable actions; the FBX contains baked motion for the Unity Animation Review scene. No AI-generated image is used as proof of a modeled or animated asset.

Further production work includes animation blending under arbitrary locomotion, foot placement on uneven terrain, mesh intersections under extreme poses, additional body sizes, retargeting and runtime performance checks in Unity. These studies do not add police combat AI or new runtime networking.

`Validation/History/wrist_grip_review_before_whole_body_fix.json` records the earlier focused wrist/grip follow-up. It is historical evidence and does not certify later shoulder changes. The current `Validation/wrist_grip_reimport.json` records hashes and readback of the newly exported clips. `Validation/arm_body_clearance.json` records the separate evaluated surface checks, their anatomical regions, their sampling limitations, and the visual result for the actual current source. The hand geometry remains broad and stylized. A wrist-only approval is insufficient: shoulder folds, disconnected cloth, and chest penetration must be assessed in the complete pose.

To repeat the technical readback, run `blender --background --disable-autoexec --python Blender/verify_wrist_animation.py`. It writes a separate `Validation/wrist_grip_reimport.json`; it does not replace the recorded visual acceptance. The audit clears Blender-importer-inferred bone connections and uses zero animation offset to preserve the original unconnected-bone and translation contract. This is not a Unity import test.

## Reproduce the offline preview

Use Blender 4.0.2 or a compatible Blender 4.x authoring installation. From the project folder, run:

```text
blender --background --disable-autoexec --python Blender/animate_characters.py -- --no-previews
blender --background --disable-autoexec --python Blender/render_animation_stills.py
blender --background --disable-autoexec --python Blender/render_animation_motion.py
blender --background --disable-autoexec --python Blender/render_patrol_grip_motion.py
python Blender/finalize_animation_preview.py
python Blender/finalize_hand_previews.py
```

The finalizer requires Pillow, FFmpeg and FFprobe in the offline authoring environment. It fully reads every expected PNG, rejects missing or stale frames, creates the two frame strips, encodes the MP4, then decodes the whole video and writes `Validation/animation_preview_validation.json`. `render_animation_stills.py` can refresh the proof stills from the frozen showcase source without re-saving that source or re-exporting its FBXs.

The motion preview renders every authored frame at 30 fps; it is not a slideshow or interpolated still-image clip. The 61 displayed frames include both action endpoints, so the encoded review video lasts about 2.03 seconds while the underlying action time range is 2.0 seconds. The preview has a modest 864×640 resolution; final stills use more rendering samples. `MotionFrames_Final` is temporary evidence output and can be omitted from distribution after encoding. Start with an empty frame directory and wait for Blender to exit successfully before running the finalizer. No runtime game script invokes Blender or FFmpeg.

The additional grip preview renders the actual raise action followed by the lower action in a fixed close-up camera, omitting the duplicated high endpoint. Its 91 displayed frames span a three-second authored sequence. GIF previews loop for convenient viewing; they are format conversions of these Blender frames.
