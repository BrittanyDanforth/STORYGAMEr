# Validation provenance

The upload included validation reports from an earlier build. Preserve those as
history; their success labels do not validate the current source.

Current checks from this cloud continuation are:

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
`package-manifest.json` next to the exported ZIPs.

Unity import, Play Mode, runtime animation blending and target-hardware
performance have not been verified. See the [current quality notes](../Documentation/Quality_Pass_2026-10-04.md)
and [visual review](../REVIEW.md).
