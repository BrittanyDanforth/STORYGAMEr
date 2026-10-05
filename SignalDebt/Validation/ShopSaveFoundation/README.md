# Shop and save foundation evidence

This directory records the save/transaction increment after the delivered PC
milestone r2. It is not acceptance of a playable shop or the full master plan.
See [the implementation scope](../../Documentation/Shop_Save_Foundation.md).

These files preserve the historical `shop-foundation-r1` checkpoint before the
ongoing desktop/dialogue redesign. Their 147-file production manifest identifies
the tested source; later UI changes need separate evidence. No new player ZIP
was released for this checkpoint.

- `domain-tests.json`: 391 assertions using the actual domain source with .NET
  8.0.425. The source hashes identify the tested model and tests. This is not a
  Unity runtime test.
- `save-store-checks-result.json`: 97 assertions in 19 cases using Unity
  6000.0.62f1, real `JsonUtility` and temporary files. The suite tests fresh and
  purchased round-trips, explicit legacy migration, recovery, incompatible and
  corrupt saves, and failed-purchase/payment publication.
- `save-store-checks-source.json`: SHA-256 for the four actual source files
  copied into the isolated Unity test project. They matched production source
  when the checks completed.
- `save-store-checks.log`: actual Unity batch log, exit 0. Expected warning
  messages from deliberately rejected writes are part of the failure fixtures.
- `windows-shop-foundation-r1-build-result.json` and
  `linux-shop-foundation-r1-build-result.json`: successful real Unity 6000.0.62f1
  x64 Mono builds, each with exit 0 and per-file hashes. The complete Windows
  player has 134 files; the Linux player has 116. The matching build logs are
  retained alongside these reports.
- `shop-foundation-r1-source-manifest.json`: the final 147 production-file
  hashes, shared by both builds. The pre-import manifest records 143 files;
  the four additional files are genuine Unity metadata preserved in source.
  No production source changed during the build.
- [native/acceptance.json](native/acceptance.json): the matching uninstrumented
  Linux player passed six native runs across four case families. It binds the
  runtime assembly and source-manifest hashes, 25 captures and six logs. Their
  report-relative directory structure is retained under `native/`, and all
  copied bytes were checked against the report's SHA-256 values.

The native cases verified legacy-to-v2 migration with original primary/backup
bytes and modification times unchanged; fresh apartment/laptop interaction; a
complete repair, one 120-credit payout and 50-credit rent payment; and a restart
preserving the resulting 150 credits and 400 rent. Malformed and future-format
saves produced recovery screens, copied the full folder path through the real
X11 clipboard and exited via Escape with their save directories unchanged. All
six player exits were 0. The logs retain audio-device failures but contain no
reported C# exceptions or save-error lines.

The serializer suite uses temporary fixtures. Native checks used isolated
campaign directories and a copy of the earlier campaign; the original campaign
was not launched or changed. The repository retains only the native acceptance
report, its captures and logs: no campaign saves, keys, caches, Unity preferences
or input drivers were copied.

This revision's Windows player was not run. The separately recorded user result
for the earlier Windows r2 delivery does not validate its later save changes.
Audio output was unavailable, and llvmpipe is not target-hardware performance
evidence. Recovery visuals were checked at 1280×800; long paths were clipped in
the field, while Copy preserved the complete path. No shop scene, shopkeeper,
purchase UI or physical handoff was exercised. The earlier
[PC runtime proof](../PCMilestone/README.md) and subsequent desktop redesign
remain separate source scopes.
