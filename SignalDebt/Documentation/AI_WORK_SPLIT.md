# SIGNALDEBT — shared-folder work plan

The user wants two AIs working in the **same project folder**. Codex owns UI, saves, validation and delivery. The other AI leads the world and deeper gameplay. Work only on SIGNALDEBT, in small reviewed increments. The entire 320-feature master plan remains the goal; the existing game is not complete.

## Read first

1. `Source/master_content.md` — read the complete design, not only the first repair.
2. `Source/feature_catalog.md` — all 320 requirements and their priorities.
3. This handoff — it describes newer unfinished work that older status documents do not yet cover.
4. Relevant source before editing. Existing designs and tests are evidence to examine, not proof that the game feels good.

Repository: `/workspace/STORYGAMEr`

Current branch: `codex/signaldebt-pc-master`

The save-browser increment builds on published baseline `85909f3`; see `git log` for the current revision.

**Preserve the current work.** This handoff was created while substantial work was uncommitted; the user's later packaging request committed that checkpoint. Inspect `git status` for any newer changes. Both AIs see the same files immediately. Do not reset, clean, switch the shared branch, or replace the project with an older archive. A separate Git worktree does not automatically include uncommitted changes.

## Ownership

| Area | Owner | Files and responsibility |
| --- | --- | --- |
| Laptop presentation, concise apps, menus/settings | Codex | `Assets/Scripts/SignalDebtUI.cs`, `SignalDebtClientDesktop.cs`, `SignalDebtProofDesktop.cs`, `SignalDebtMainMenu.cs`, `SignalDebtSaveMenu.cs`, `SignalDebtAiSettings.cs`, `SignalDebtDesktopWindow.cs`, `SignalDebtComputerDisplay.cs` |
| Save integration and campaign publication | Codex | `SignalDebtSaveData.cs`, `SignalDebtSaveStore.cs`, `SignalDebtSaveLibrary.cs`, `SignalDebtLocalFiles.cs`, `SignalDebtApiKeyStore.cs`; Copy/migration methods in `SignalDebtShop.cs`; registration in `SignalDebtBootstrap.cs` |
| Checks, builds, packaging and acceptance records | Codex | `Assets/Editor/*Checks.cs`, existing domain test runners, build/package scripts, validation and delivery documentation |
| Neighborhood art, layout, import and navigation | Other AI | `Assets/Editor/SignalDebtSceneBuilder.cs`, `SignalDebtNeighborhoodBuilder.cs`, new `Assets/Art/Neighborhood/`; external neighborhood generator/exporter and source art |
| NPC personalities, knowledge, tasks, schedules and relationships | Other AI | `SignalDebtNpcCharacter.cs`, `SignalDebtNpcClient.cs`, `SignalDebtAiProvider.cs`, relevant `SignalDebt/AI/` code and new dedicated NPC modules |
| Deeper gameplay and physical systems | Other AI | New dedicated modules for permission jobs, referrals, physical hardware, shop work, investigation and consequences; existing device/domain modules when needed |

Paths in the table beginning with `Assets` are inside `SignalDebt/UnityProject`. Script filenames without a directory are in `Assets/Scripts` unless otherwise stated.

`SignalDebtShop.cs` contains both domain and save code: Codex owns its current Copy/migration work. Put new shop behavior in separate files first. Likewise, send required Bootstrap/SaveData/UI registration changes to Codex instead of both AIs editing those shared files. Codex will not independently redesign the other AI's map or gameplay rules.

The existing proof document/recovery modules now have bounded native acceptance. Codex owns their UI/save integration; coordinate before changing their API. New gameplay belongs to the other AI's lane.

## Work order

### First: finish two concrete increments in parallel

**Codex: save/load browser — bounded r2 acceptance complete.**

- Main-menu and Pause browsing covers current progress, optionally named manual checkpoints, New Game archives and the read-only earlier installation.
- Checkpoints live under `UserData/Checkpoints`; existing archives remain under `UserData/Backups`. Loading validates the captured selection, preserves current progress in a safety checkpoint, then publishes the selected campaign. Settings remain separate and schema 4 is unchanged.
- Actual Unity and native Linux r1 checks passed. The final r2 player passed targeted failure-message, long-name, retry and paid-campaign reload checks; both Linux and Windows builds passed. The r1 suites were not rerun for this SaveMenu-only change, and the Windows player was not executed here.
- This is a partial contribution to F191, with no new world/NPC behavior. The completed 160-credit proof-recovery checkpoint and its evidence remain below.

**Other AI: finish the compact neighborhood and prove it is walkable.**

- Review the existing proposal and unfinished importer before rewriting anything.
- Finish export metadata/material defects listed below, then validate in an isolated Unity snapshot.
- Preserve the authored apartment. Open its corridor only after valid world geometry/collision is present.
- Walk apartment → parts shop across the crossing → separate Copy Shop → police reception → side/back routes → apartment. Check doors, thresholds, window passage, floor support, collision, daylight and glass.
- Review normal first-person views, looking down, shop interior and laptop sit/focus/stand. Fix the observed problems before adding more streets or decoration.

### Second: give the places useful work

Other AI leads the distinct 180-credit permission job, an earned referral and a useful physical shop visit. Agree the state/permission/payment contract before presentation. Codex adds focused screens and save integration around that behavior.

The lawful opening must remain viable: 80 starting credits + 120 repair + 160 recovery + 180 permission job = 540, against 450 rent. Do not turn the existing optional 90-credit GPU into a mandatory tax or pretend an uninstalled part gives a benefit.

### Third: people and consequences

Other AI builds recurring people with their own limited knowledge, work and interruptions, then a complete consequence chain. Social interaction must affect believable people without letting generated dialogue directly grant money, permissions or world facts. Police need discoverable evidence and a process; a shop visit or purchase is not automatic guilt.

Codex supplies understandable interfaces, persistence, reproducible checks and playable builds for each accepted increment. After these foundations, continue the remaining catalog by dependencies, rather than attempting all systems at once.

## Shared-folder coordination

- One owner per file. Announce a needed ownership change before editing a reserved file.
- Do not run simultaneous Unity editors against one project. Validation uses an isolated source snapshot so active edits cannot change a build halfway through.
- At each handoff, give changed files, what actually works, checks run, known defects and the next concrete action.
- Keep current tested packages immutable. A build passing does not establish native gameplay, visual quality or successful AI dialogue.
- Do not mark a whole catalog feature complete from a narrow prototype or a count of passing assertions.

## Tested baseline versus unfinished source

### Save browser has bounded final r2 acceptance

`SignalDebtSaveLibraryChecks` passed 178 assertions across 28 real filesystem cases; existing SaveStore checks passed 358/51 and conversation checks 128/7. Native Linux r1 exercised named checkpoints, cancel, seated loading without a leftover desktop, safety snapshots, pagination, stale-selection rejection and Refresh, failed-write preservation, exact New Game archival/restoration, a paid-proof fixture load at 360 without another payment, and backup recovery with the selected snapshot's correct date and untouched originals.

Final r2 passed native Linux checks: a 64-character wide name fits using measured ellipsis while confirmation facts remain visible and full saved metadata is retained; a failed save stays prominent without clearing the name or changing the active campaign; retry creates one checkpoint; Continue restores the prior paid 360-credit state exactly, with read-only laptop content and no duplicate payment. The player exited with code 0 and all 116 runtime files were unchanged. Both final Linux and Windows builds passed against the same 185 frozen source inputs; Windows execution remains unverified here. The r1 suites above were retained, not rerun, because only SaveMenu changed. See [SaveMenu evidence](../Validation/SaveMenu/README.md) for the exact revisions and limits. This does not complete F191 or the wider master plan, and introduces no gameplay-schema, neighborhood or NPC-design changes.

### Windows r5 is an earlier built PC checkpoint

`/workspace/SignalDebt-deliveries/SignalDebt_PC_Milestone_Windows_r5.zip`

SHA256: `f4c83049928d9acc50fddc34c243de00ba1d98531ab72d18ae5da70ac1a76936`

It contains the schema-3 persistent client workstation: actual order records, saved service connections, reversible wrong connections, owner use of the folder, once-only 120 payment and paid read-only access. Native Linux checks passed; the Windows build/archive passed, but this Windows version was not executed here. It does **not** include the new neighborhood or notice-recovery job.

### Proof job and schema 4 have bounded native acceptance

Current source adds `SignalDebtProofDocument.cs`, `SignalDebtProofRecovery.cs`, `SignalDebtProofDesktop.cs`, UI/Bootstrap wiring, NPC observation context and schema-4 persistence. The document preserves every revision, exports actual version content, and uses a separate grant and once-only 160 receipt. Private restores do not give the clerk knowledge before an owner check.

Portable model checks passed 13 scenarios / 827 assertions; campaign checks passed 9 / 782. Unity save checks passed 358 assertions, conversation checks 128 and offline provider checks 73. A real UGUI probe checked both workspace widths. Linux and Windows builds passed. Native Linux r2 covered the actual wrong-restore/undo/recovery/export/payment path; final r3 corrected a stale Inbox scroll preference and verified the latest offer, paid reload, read-only behavior and settings. See `../Validation/ProofRecovery/README.md` for exact source/build scope. This is not full-feature or whole-game acceptance, and Windows execution remains unverified here.

Evidence and model handoff: `/workspace/SignalDebt-proof-recovery/INTEGRATION.md`, `test-results.json`, `campaign/campaign-results.json`. Genuine Unity fixtures are `SignalDebtSaveStoreChecks.RunBatch`, `SignalDebtConversationChecks.RunBatch` and `SignalDebtAiProviderChecks.RunBatch` in namespace `SignalDebt.Editor`.

### Neighborhood handoff to the other AI

External working directory: `/workspace/SignalDebt-neighborhood-prototype/`

- Source: `build_neighborhood.py`, `export_neighborhood.py`, `layout-v2.json`.
- Reviewed images: `geometry-r4/renders/{ApartmentExit,ShopInterior,PoliceApproach,WindowPassage,DeliveryReturn,StreetReverse}.png`.
- Export: `export-r5/Neighborhood.fbx`, `neighborhood_collision_anchors.json`, `Textures/`, `Southbank_Neighborhood_Export_Review.blend`, `export-proof.json`.
- Contract example: `export-contract-sample.json`.
- Design reasoning: `/workspace/SignalDebt-neighborhood-design/master-gap-review.md`, `pc-loop-audit.md`, `measured-neighborhood-proposal.md`.

The six-view composition was reviewed; it is not runtime acceptance. The older courtyard/shop blockout was rejected and must not be substituted. No new neighborhood assets have been imported into Unity. The later full-project packaging step preserves a separate copy under `SignalDebt/SourceAssets/Neighborhood/`.

`SignalDebtNeighborhoodBuilder.cs` is a **compiled draft without neighborhood import/runtime acceptance**. `SignalDebtSceneBuilder.cs` already has its integration hook, conditional daylight and original apartment screen assignment. The current builds use its room-only path because neighborhood assets are outside Unity. Review the importer before using it with the unfinished export.

Known export blockers:

1. Recompute box collider centers/sizes from final transforms, including rotated shelf components.
2. Supply measured `rotation_delta_design_xyzw` for both moved `Corridor_Fuse_Box` and `Corridor_Floor_Label`.
3. Perform FBX/material readback. Export logs contain 201 tangent warnings for non-triangulated polygons, including gutters; assess/fix instead of assuming the normal maps will render correctly.

The external export completed and Blender stopped. It contains 56 baked maps for 37 materials. FBX SHA256: `67685f92535e632892c15490911e805c1fb47d9e3822a6ef970659ff8397f9d4`. These are export facts, not Unity appearance or performance results.

Retain four unique references: Origin `(0,0,0)`, East `(2,0,0)`, North `(0,0,3)`, Up `(0,4,0)`. Source-apartment-to-design is recorded in the manifest; compose with the existing apartment transform and solve the imported basis, including signed scale. Do not add a guessed mirror flip. Mesh walk-surface winding must face upward in Unity axes.

Only after full validation should the importer remove both the visible `Corridor_End` and its separate collider, relocate the two fixtures, and disable the old apartment exterior backdrop meshes. Keep room geometry, the real window and its passage. Explicitly bind the original `Laptop_Screen`; do not search the entire scene for whichever screen name appears first.

The navigation increment has static storefront art. It does not yet supply shop staff, police behavior, a working court or physical computer tasks in those interiors.

## Tools and live-AI limit

Unity: `/workspace/.local/opt/unity6000/Editor/Unity` (6000.0.62f1); source `/workspace/SignalDebt-setup/unity/unity-env.sh`. Windows/Linux Mono build modules are installed. Native Linux players can run under the existing private Xvfb setup. Graphical Editor Play Mode is license-blocked; report standalone testing accurately.

Blender: source `SignalDebt/Tools/blender-env.sh`. .NET: `/workspace/.local/opt/dotnet`.

The last environment API attempt reached the official service but returned HTTP 401. No successful live-response quality evaluation was completed here. Do not retry an unchanged binding, dump environment values, or put credentials in source/artifacts. The user's in-game key remains their separate local setting.

## Message to give the other AI

> Work with Codex in this same folder on SIGNALDEBT only. Read `SignalDebt/Documentation/AI_WORK_SPLIT.md`, then the full master content and feature catalog. You own the neighborhood, NPC systems and deeper gameplay; Codex owns UI, saves, tests and builds. Preserve all existing uncommitted work. Start with reviewing and completing the unfinished neighborhood export/import, then prove the real walking route and fix its quality issues before adding systems. Do not edit Codex-owned Bootstrap/SaveData/UI files concurrently; provide the small integration contract instead. Report unfinished work honestly and keep progressing step by step through the master plan.
