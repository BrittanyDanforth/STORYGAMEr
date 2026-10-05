# Validation provenance

The upload included validation reports from an earlier build. Preserve those as
history; their success labels do not validate the current source.

Current checks from this cloud continuation are:

- [ProofRecovery/](ProofRecovery/README.md): persistent client workstation and
  separate notice recovery, schema-4 migration, real Unity checks, current builds,
  native Linux recovery/payment and targeted final Inbox/reload acceptance.
  The archived neighborhood remains unintegrated work in progress.

- [NPCDialogueUpdate/](NPCDialogueUpdate/README.md): personality and visible-history
  dataflow, the discovered standalone key-saving failure and its verified Linux
  correction, plus the final Windows/Linux builds and native smoke check. Live
  dialogue quality remains pending; synthetic context checks do not establish it.

- [UIMenuUpdate/](UIMenuUpdate/README.md): launch and pause menus, compact laptop
  apps, portable local data, and the direct AI provider/key-store checks. Native
  reports identify their frozen revisions; synthetic AI checks do not establish
  a successful live API request.

- [ShopSaveFoundation/](ShopSaveFoundation/README.md): current transaction and
  schema migration checks, including genuine Unity serialization/filesystem
  recovery. These do not establish a playable shop encounter.

- [PCMilestone/](PCMilestone/README.md): actual Unity import/compiler and Windows/Linux
  builds, native standalone input, apartment/PC/first-repair progression and
  save/reload evidence. These are distinct from Editor Play Mode tests.

- `CloudSetup/`: official tool provenance, original archive inspection, relay
  tests and portable C# domain tests. `import-blend-audit.json` describes the
  uploaded scenes; `final-blend-audit.json` describes the revised scenes.
- `apartment_quality_*.json`: current apartment geometry, export contracts,
  preview settings and source/image hashes.
- `quality_pass_*.json`: revised characters, original-source comparisons,
  export reimports, motion rendering and preservation checks. Read each report's
  scope and limitations; a diagnostic report is not an unconditional pass.

`file_manifest.json` is the **original uploaded file manifest**, retained for
import provenance. It is not a manifest of the revised project. The source ZIP
packager records the committed revision and each archive's SHA-256 in a separate
`package-manifest.json` next to the exported ZIPs. Explicit working-tree packages
identify their uncommitted snapshot instead of claiming a new commit.

Unity import and the native apartment/PC sequence now have the bounded evidence
above. The user reported a working Windows r2 launch/laptop flow; later Windows
revisions, graphical Editor Play Mode, audio output and target-hardware performance
remain unverified. See the [current quality notes](../Documentation/Quality_Pass_2026-10-04.md)
and [visual review](../REVIEW.md).
