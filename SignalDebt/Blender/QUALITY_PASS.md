# Character garment refinement

This pass reshapes the patrol and SWAT sleeves, rounds the shoulder cap, narrows the armhole, smooths the skin-weight transition, and keeps the inner underarm cloth attached to the ribs. Patrol epaulets are thin ribbons fitted to the actual shirt surface. SWAT sleeve panels project onto the finished sleeve and inherit interpolated cloth weights, so narrowing the sleeve does not leave the panels floating.

The source uses the existing 52-bone contract and authored grip/animation poses. Player, Mara and Judge meshes, skin weights, materials and anchors are preserved; their three FBX files remain unchanged. The police mesh totals are 61,514 patrol triangles and 76,472 SWAT triangles (previously 55,440 and 70,380). These are the source assets; runtime LOD and target-hardware performance still need work.

## Rebuild and inspect

Use the verified official Blender installation. For this scoped police pass:

```sh
blender --background --factory-startup --disable-autoexec --threads 4 --python-exit-code 1 --python Blender/build_characters.py -- --police-only --skip-render
blender --background --factory-startup --disable-autoexec --threads 4 --python-exit-code 1 --python Blender/animate_characters.py -- --no-previews
```

`--police-only` requires an existing `Characters.blend`, copies its nonpolice collections through a temporary local library, and only rewrites the two police base FBXs. The animation builder writes four clips at 30 fps. Embedded source scripts are disabled when opening Blender files. The scripts use the Blender 5 layered Action API while retaining Blender 4 compatibility.

`render_quality_pass.py` produces actual Cycles frames. `finalize_quality_pass.py` only lays out the comparison sheet and encodes those frames into GIFs. GIFs display every second source frame at 15 fps; they are not video interpolation or synthesized motion. Rejected trials and temporary motion frames are kept outside the deliverable.

The `Before` images come from the uploaded animation source. `After` patrol images retain the hash of the exact file from which they were rendered. A later SWAT-only patch-seating correction is covered by `quality_pass_patrol_actor_equivalence.json`; the original patrol render hashes are retained, and the verifier checks exact positions, topology, weights, materials, actions and all patrol pose frames against the later full file. Decoded normals and unused UV coordinates differ by floating-point roundoff. The proof uses predefined 1e-6 component bounds for each, records the measured maxima (normal 1.240734e-7; UV 5.960465e-8), and confirms every material uses only Principled/Output shader nodes with no UV/image inputs. It does not claim byte-identical mesh storage. The corrected SWAT stills and kick GIF use the final source directly.

## Evidence and limits

The current `Validation/quality_pass_*.json` reports record Blender version, source/export hashes, all nine FBX reimports, sampled animation fidelity, unchanged nonpolice assets, surface depth and exact triangle-crossing diagnostics. The before/after surface audit uses the same verifier and Blender version for both sources. Synthetic renderer-seam audit caps are reported separately from actual garment surfaces.

The complete shoulder silhouette and grip were inspected from front, side, rear and top cameras. The shoulder shelf and floating sleeve panels were corrected. Small proximal patrol armhole folds still remain; the exact crossing diagnostic reports them rather than approving the garment as free of intersections. This is an improved stylized source build, not a completed realistic character production pipeline. Facial animation, final retopology, runtime animation blending, cloth behavior, Unity Play Mode and GTX 1080 Ti performance remain unverified.
