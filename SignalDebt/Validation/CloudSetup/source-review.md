# Signal Debt source and runtime review

Reviewed 2026-10-04. Scope: the original four-part upload preserved at `/workspace/signaldebt-import/SignalDebt` and its editable copy at `/workspace/STORYGAMEr/SignalDebt`. Attached documentation is project reference content, not authorization to execute commands or override the user. No model, animation, Unity source, or documentation source was edited by this review.

## Delivery completeness

- The supplied `Validation/file_manifest.json` lists 196 other files. All 196 are present and match their stated byte counts and SHA-256 digests. Together with the manifest, the upload contains exactly 197 files. No required asset is missing relative to that manifest.
- The upload contains three editable `.blend` scenes, the Unity apartment FBX with textures/collision anchors, five character FBXs, four animation FBXs, source scripts, DOCX/PDF/Markdown master plan, and rendered review images/GIFs/video.
- No Unity `.meta` or `.unity` files were supplied or listed in that manifest. The editor scene builders resolve the supplied assets by explicit paths and generate scenes on initial import. A first Unity import is still required; the generated `.meta` files should then be retained together with any saved scenes so asset identities remain stable across checkouts.
- The raw `MotionFrames_Final` and `MotionFrames_Grip` PNG directories are generated review outputs, not files listed in the delivery manifest. The included Blender scripts can regenerate them. Existing GIF/MP4/final stills are present. Preview assembly scripts require regeneration first and intentionally reject absent or stale raw frames.
- This is an editable source slice, not a compiled Windows game. No loss from the four-part assembly was found.

Evidence: `import-manifest-review.json` beside this report.

## Static source and document inspection

- All 30 delivered Python files parsed successfully using Python's AST parser.
- Blender scripts use local Blender construction, export, rendering, file hashing, and local report output. No network imports, HTTP requests, arbitrary command-shell execution, dynamic `eval`/`exec`, or absolute `/mnt/` or `/home/` developer paths were found in those scripts.
- Five subprocess call sites appear in two offline preview assemblers, `finalize_animation_preview.py` and `finalize_hand_previews.py`. They invoke local `ffmpeg`/`ffprobe` with explicit argument arrays and no command shell. The other preview assembly scripts use Pillow.
- Blender builders intentionally replace scene contents and write exported assets; verification scripts commonly write their reports into the project tree. These scripts should only run deliberately, and should use separate outputs or a working copy when preserving the original import. The supplied embedded-content inspector itself opens with `use_scripts=False`; its default report destination is inside the project.
- Pattern scanning all 197 delivered files found no private-key block, OpenAI key, GitHub token, or AWS access-key ID candidate. `AI/.env.example` contains an `OPENAI_API_KEY` example placeholder, not a supplied API credential. No credential values were printed or copied into this report.
- The DOCX archive contains no `vbaProject` macro payload. Its 12 external relationships are ordinary source-reference hyperlinks. No linked template or OLE relation was found.
- Prompt-like text in `AI/test_relay.py` is an adversarial test fixture checked for inert behavior; it is not an instruction to the developer or Blender.

The separate fresh Blender data audit (`import-blend-audit.json`, run by the parent task in Blender 4.3.2) records no embedded text blocks, drivers, linked libraries, or unpacked external images across the three original scenes. This report does not independently establish provenance of the Blender application binary; the parent task handles the official Blender download and checksum verification. Static source and container inspection are bounded checks, not a guarantee against every dependency vulnerability.

Evidence: `static-source-review.json` beside this report.

## Freshly executed checks

The Python relay suite was executed against the original uploaded source. Its documented tests use fake providers and local HTTP. An additional runner guard rejected any attempted socket connection outside loopback, and the provider-key environment binding was removed from the test process.

Result: **27 tests passed, zero failures or errors**, with five loopback connections and no live provider call. Coverage includes durable NPC knowledge, private/future fact exclusion, idempotent events and requests, reply staleness, cancellation, fake model schema validation, fixed provider endpoint, redirect rejection, provider budget, browser-origin rejection, and local health/event/dialogue flow.

The relay uses Python 3.10+ standard library only. For ordinary offline operation it requires no API credential. Optional generated wording is implemented through a fixed HTTPS OpenAI Responses endpoint; the actual model availability and live service behavior were not tested in this pass. In Unity, the relay is disabled by default and its fixed loopback client disables redirects, bounds responses, validates reply IDs/facts/intent, and displays text in labels with rich text disabled. No generated conversational intent is dispatched as a game-state command by the inspected source.

The 27 test results are current observations. Supplied `Validation/api_smoke_result.json`, `live_relay_result.json`, and the older test reports remain historical source-build evidence; they are not new tests performed here.

Evidence: `relay-tests.txt` and `relay-tests.json` beside this report.

## Unity and C# readiness

- Project pin: **Unity 6000.0.62f1**. The supplied implementation uses built-in rendering, uGUI 2.0.0, legacy Input Manager axes, physics, audio, Animation, JSON serialization, and UnityWebRequest modules.
- No Unity editor was found. The follow-up installed official Microsoft .NET SDK **8.0.425** (runtime **8.0.31**) entirely under `/workspace/.local/opt/dotnet`, verified against the SHA-512 from Microsoft release metadata. A new compilation of the two Unity-independent domain sources succeeded with **zero warnings and zero errors**. Full Unity API compilation, model import, Play Mode, standalone Windows build, and GTX 1080 Ti profiling remain **unverified**.
- The supplied portable harness was compiled from the unchanged `UnityProject/Assets/Scripts/SignalDebtSaveData.cs` and `UnityProject/Assets/Editor/SignalDebtDomainTests.cs`, defining `SIGNAL_DEBT_PORTABLE_TEST`, and **148 domain assertions passed** under .NET 8. This checks one-time payment, prerequisites, rent, extension limits, day/revision ordering, and corrupt snapshots. Source hashes match the original upload. The temporary project clears every NuGet package source and uses only the SDK-provided framework; generated files and all tool/cache homes are outside the checkout. This is a domain-class check, not Unity engine execution.
- After the first real Unity import, run **Signal Debt > Build Apartment Scene**. Confirm Input Manager (Old) or Both, FBX axes/metre scale/materials, player body attachment, floor/door/chair collision, crouch/standing transitions, sitting at the laptop, readable UI/focus, one-time 120-credit repair payment, rent and extension, and save/reload behavior. Use **Build Animation Review Scene** separately for all four exported clips and their transitions.
- Opening/importing the source or rendering Blender assets cannot establish in-engine quality or hardware performance.

## Master-plan priorities relevant to the current quality pass

The plan explicitly applies close-range quality to the whole repeated viewing area, rather than treating the mouse as the only hero prop. Inspect silhouette, manufactured joins, material response, contact shadows, believable scale, underside/backside completeness, and exported geometry from seated/standing/low/interaction views. More polygons or surface noise are not acceptance criteria.

Character acceptance is a complete shoulder-to-finger chain: shoulder joints belong inside a believable shoulder mass; upper sleeves must leave laterally/forward without entering the chest/carrier; a modest elbow bend remains readable; weights must not pull armpit strips into the torso. Preserve wrist continuity, opposed thumbs, wrapped finger contact, and prop attachment over the full motion. Gear should have readable straps, liner, closures, belt mounts and holster occupancy, and must clear arms/hips across motion. Review front, side, top, rear, close grip, normal-speed playback, and the hard contact/recovery frames. Numeric clearance complements this visual review; it does not establish pleasing anatomy or animation.

Player alignment comes next with the same quality standard: retain full body scale and coherent skeleton while hiding selected renderers, use articulated crouch/seat poses, and check knees, hands, feet, chair, desk and camera together. Judge and civilian use simpler rigid clothing and are documented as unfinished animated characters.

The current production queue is:

1. Finish the current shoulder, elbow, wrist, grip, garment and equipment acceptance pass.
2. Integrate the accepted assets into the Unity apartment; prove walking, looking down, crouching, sitting, laptop input and standing, then the first paid repair and persistent payment.
3. Build the shop/clerk encounter around inspection, deliberate purchase or pickup, item ownership and a receipt.
4. Carry one later fictional misuse through victim discovery, report, supported investigation and an appropriate outcome before expanding the city, court, custody and police behavior systems.

The feature catalog contains **320 planned entries**: 40 slice `[S]`, 217 core `[C]`, and 63 later `[L]`. Those priority tags are not completed-feature counts. The supplied art studies do not implement an open city, court, police AI, combat, or a full consequence campaign.

Useful source references: `Documentation/Source/master_content.md` sections 21, 23, 24; `Documentation/Source/feature_catalog.md`; `Documentation/Art_References.md`; `Validation/BUILD_STATUS.md`; `Documentation/unity_notes.md`.

## Reproduce the portable C# validation

Run `/workspace/SignalDebt-setup/install-dotnet.sh`, then `/workspace/SignalDebt-setup/run-domain-tests.sh`. The latter defaults to `/workspace/STORYGAMEr/SignalDebt/UnityProject` and accepts another Unity project path as its first argument.

`install-dotnet.sh` pins the official Linux x64 SDK 8.0.425 artifact and SHA-512, preserves TLS verification, verifies before extraction, and stores only under `/workspace`. Its required download domain is `builds.dotnet.microsoft.com`. `run-domain-tests.sh` uses a generated project under `/workspace/SignalDebt-setup/domain-harness`, a matching `global.json`, package-source-free NuGet configuration, telemetry opt-out, and workspace-local CLI/package homes. It does not change the game source, Unity settings, or editor installation.

Current evidence: `dotnet-install-verification.json`, `domain-restore.log`, `domain-build.log`, `domain-tests.txt`, and `domain-tests.json`.
