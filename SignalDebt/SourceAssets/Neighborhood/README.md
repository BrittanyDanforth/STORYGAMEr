# Neighborhood source archive — work in progress

This folder preserves the external Southbank neighborhood study for continued work. It sits outside `UnityProject/Assets` and is **not imported or active in the game**. Geometry/composition was reviewed in Blender; the export is not ready for runtime use.

## Contents and provenance

- `Source/`: original generator and exporter, unchanged.
- `Plan/`: measured layout, illustrative import-contract sample, and export plan. The contract sample is not measured production data; the export plan records the intended gate, not completed acceptance.
- `Export/`: final neighborhood-only FBX, one editable export-review `.blend`, collision/material manifest, original export proof, and all 56 shared texture maps for 37 materials.
- `Review/`: six reviewed geometry-r4 PNGs. These precede the final small join fixes and material export; they are not Unity screenshots.
- `archive-manifest.json`: original paths, sizes and SHA256 values for every copied file. `portability-report.json` records the copied Blender file's dependencies and relocation check.

The source was `/workspace/SignalDebt-neighborhood-prototype/`: scripts/plans from its root, final assets from `export-r5/`, and images from `geometry-r4/renders/`. No intermediate scenes, caches or old rejected courtyard assets were copied. The final FBX is 59,978,124 bytes. No archived file reaches 95 MiB.

Blender 5.2.2 LTS, build `d13f752e3b9c`, produced the study. The editable scene retains a copied apartment reference; the FBX export selection excluded all 838 reference objects. The authoritative apartment remains `../../Blender/SignalDebt_Apartment.blend`, unchanged. Independent FBX readback has not verified the export selection yet.

## Known blockers before import

1. Recompute rotated shelf **box** colliders from final renderer transforms; their recorded initial positions/sizes are stale.
2. Add measured `rotation_delta_design_xyzw` for the moved fuse box and floor label. Absolute target rotations are present but do not replace the requested rotation deltas.
3. Resolve 201 FBX n-gon tangent warnings, including triangulation/tangent calculation, then independently read back the FBX and calibrate its four basis anchors.
4. Validate native Unity Built-in/Standard materials, transparent glass, daylight, collision/floor seams, every public route and boundary, and the existing apartment/laptop regression. Performance and final art remain unverified.

No clerk, shop transaction, police casework/custody gameplay or NPC schedules were implemented by this art pass. Do not infer runtime readiness from Blender images or export counts.

## Opening and rebuilding

Open `Export/Southbank_Neighborhood_Export_Review.blend` with Blender 5.2.2 and automatic script execution disabled. Keep its adjacent `Textures/` folder: baked images use relative paths, while the apartment reference images and non-default font are packed. No archival path remapping or geometry/material regeneration was needed.

The scripts retain their historical default paths. To rebuild portably, run the following **from the `SignalDebt` directory**, supplying a new absolute output directory; never overwrite this archive as a side effect:

```sh
blender --disable-autoexec -b -t 4 --python SourceAssets/Neighborhood/Source/build_neighborhood.py -- --apartment-source Blender/SignalDebt_Apartment.blend --output /path/to/new-neighborhood-output --export --no-render
```

Both `--apartment-source` and `--output` overrides are intentional. The exporter is beside the generator and is loaded by `--export`. Rebuilding reproduces the current WIP, including its documented blockers; it does not fix them or import anything into Unity.
