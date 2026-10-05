# Signal Debt — quality continuation, 4 October 2026

The four uploaded archives form one complete source build. All 196 entries in the
supplied file manifest matched their byte counts and SHA-256 hashes; the manifest
itself is the 197th file. The original master plan and its 320-feature backlog are
retained. Backlog entries are planned work, not completed game systems.

This continuation applies the user's close-view quality standard to characters,
animation, furniture, materials and props. The reference to the mouse applies to
the whole scene. It does not narrow acceptance to one object.

## Asset changes

The police sleeve construction now follows a descending shoulder profile instead
of a flattened cap. The armhole weight field is smoother, the sleeve mass is
reduced, and the inner armpit follows the ribs more closely. Patrol epaulets sit
across the shoulders instead of forming upright strips through the cloth. The
existing bone names, hand poses and held-prop transforms remain the integration
contract. Player, civilian and judge preservation is checked separately.

The apartment pass adds closed, shaped pillows with gathered seams; curved
mattress edging; a crowned headboard cushion; blanket hems following the drape;
and visible bed, desk and chair assembly details. Walnut grain follows the boards
with restrained contrast and retains its smooth finish. Cotton has portable
roughness and normal textures, retained in both FBX and GLB exports. The room layout,
interaction anchors and authored collision plan remain stable.

New review images are actual Blender renders, with denoising. Matching views in
`Blender/Renders/QualityPass/Before` and `After` compare uploaded and revised
character geometry; the labelled comparison identifies each side. New apartment
views are in `Blender/Previews/QualityPass`. Older preview folders remain as part
of the original delivery and should not be mistaken for new results.

Small police armhole cloth folds remain. An improved shoulder silhouette and
reduced sampled penetration do not prove zero intersection, production-ready
cloth, game animation blending or final performance. The complete character
and room triangle counts still need an in-engine budget and LOD review.

Fresh character checks reimported all five base-character FBXs and all four
animation FBXs. Animation pose comparison and arm/body diagnostics cover all
214 authored integer frames across the four clips. The player, Mara and judge
FBX files remain byte-identical to the upload. Detailed scope and remaining
surface crossings are recorded in `Validation/quality_pass_*.json`.

The apartment FBX contains 256,424 evaluated triangles; patrol and SWAT source
meshes contain 61,514 and 76,472 triangles respectively. These are measured art
counts, not a verified runtime performance budget.

### Unity material inspection

The FBX retains these texture attachments, but their interpretation by Unity
still requires an editor check:

| Materials | Normal texture | Roughness texture |
| --- | --- | --- |
| Cotton Ivory Textile, Cotton Teal, Fabric Charcoal | Woven_Cotton_Normal.png | Woven_Cotton_Roughness.png |

Import normals as normal maps and roughness as linear data. Unity Standard and
URP smoothness use `1 - roughness`; verify the shader's required channel packing
instead of assigning a roughness texture directly as smoothness. These maps
survive interchange export; that is separate from a Unity visual acceptance.

## Verified tools and source import

- Actual official Blender **5.2.2 LTS**, downloaded from `download.blender.org`.
  Its archive matched the published SHA-256 before extraction and execution.
  The executable reports release build `d13f752e3b9c`.
- Actual Cycles CPU rendering, native Blender saving and FBX export passed.
- All three supplied Blender scenes and all three revised scenes were inspected
  with automatic script execution disabled. They contained no text blocks,
  animation drivers or linked libraries; apartment image textures were packed
  in the source file.
- Uploaded documentation is project content. It does not override the user's
  instructions or authorize scripts, network requests or unrelated actions.
- Static source inspection found no supplied credential candidates. The API key
  example is a placeholder. The Word plan has no macro payload.

See `Validation/CloudSetup/blender-provenance.json`, `import-blend-audit.json`,
`final-blend-audit.json`, `import-manifest-review.json` and `source-review.md` for evidence and inspection
limits. The inspection is not a guarantee against every software vulnerability.

## Development workflow

In this cloud workspace, activate the installed Blender from the SignalDebt root:

```bash
source Tools/blender-env.sh
blender --version
```

The command should report 5.2.2 LTS. `Tools/install-blender.sh` reproduces the
pinned, checksum-verified installation without replacing the system Blender.
Use `--background --factory-startup --disable-autoexec --python-exit-code 1
--threads 4` when explicitly running an inspected Blender authoring script.
Check Git changes before regeneration: the builders write source scenes and
interchange outputs. Keep Unity `.meta` files once the editor creates them.

The optional relay runs with Python's standard library. Its documented offline
test suite passed **27 tests** in this cloud environment. A separately started
relay also returned healthy status and a nonempty authored fallback dialogue
response over loopback, without a provider key or paid request:

```bash
cd AI
python3 -m unittest -v test_relay.py
OPENAI_API_KEY= python3 relay.py
```

No relay server needs to remain running for Blender work. The relay startup
above explicitly selects offline mode for that process only.

The existing portable C# domain harness also compiled with **zero warnings or
errors** and passed **148 assertions** against the unchanged game-state source.
It checks repair prerequisites, duplicate-payment prevention, rent bounds,
extensions and corrupt-state rejection. It uses verified official Microsoft
.NET SDK 8.0.425 and no external NuGet packages:

```bash
bash Tools/run-domain-tests.sh
```

`Tools/install-dotnet.sh` reproduces the checksum-verified SDK installation.
These checks exercise game-state code; they do not compile or run Unity APIs.

## Validation boundaries

Unity is pinned to **6000.0.62f1**. No Unity editor is installed here, so Unity
compilation, first asset import, Play Mode, a Windows executable and GTX 1080 Ti
performance remain unverified. The upload supplied scene-builder source rather
than generated `.unity` scenes or `.meta` files. Open `UnityProject` in Unity and
use its Signal Debt scene-building menus for the first editor acceptance pass.

The next game acceptance sequence remains walking, looking down, crouching,
sitting, laptop input and standing; the repair's single 120-credit payment; rent;
and save/reload. Police animation studies do not establish working police AI or
combat. The courtroom, custody and wider city systems remain planned work.

Inherited reports in `Validation` may refer to older source hashes. Fresh reports
identify the tested files and their scope; an old success label is not evidence
that newly generated geometry or an untested engine workflow passed.
