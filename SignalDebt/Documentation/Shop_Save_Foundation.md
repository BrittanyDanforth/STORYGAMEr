# Shop transactions and save foundation

This is the historical `shop-foundation-r1` checkpoint, captured before the
ongoing desktop redesign and automatic dialogue connection work. Its evidence
is bound to 147 recorded production files; it does not validate later interface
changes. No new player ZIP was released for this checkpoint.

This increment prepares one used graphics-card purchase and makes campaign
changes durable before gameplay reports success. It does **not** add the shop
room, Jon, a carried object, a purchase interface or a completed shop encounter.
The previously delivered `SignalDebt_PC_Milestone_Windows_r2.zip` predates this
increment and continues to use the original save format.

## One purchase, one saved commitment

`SignalDebtShopCatalog` identifies one used Aster S8 graphics card sold by Jon for
90 credits. Inspecting it changes nothing. A quote is bound to the campaign,
world revision, stock, physical unit and price. A changed campaign or transaction
requires a fresh inspection; the opening 80-credit balance is insufficient.

Confirmation builds a detached next snapshot containing the debit, finite-stock
ownership and receipt linked to one ledger event. The save must succeed before
the live campaign changes or `GameEventCommitted` fires. Duplicate confirmation
cannot spend or award the same card twice, including after reload. The receipt
retains the price actually paid rather than depending on future catalog prices.

Repair acceptance, diagnosis, connection, test, payment, rent, extension and rest
now use the same save-before-publish boundary. A failed write leaves live money,
flags, memories, deadlines and day unchanged. The campaign object keeps its
identity so the optional conversation client does not mistake each successful
transaction for loading another world. A shop purchase does not add knowledge
to the unrelated copy-shop client's memory.

## Save compatibility and recovery

Current campaigns use `signal-debt-save-v2.json`, with supported `.bak` and `.tmp`
recovery candidates. If no current-family file exists, the game can import the
old `signal-debt-save.json` family. Migration preserves campaign identity and
commitments and writes subsequent saves to the new filename. Original legacy
files remain byte-for-byte unchanged, so the previously distributed executable
cannot overwrite a new purchase using its older loader.

A current save takes precedence over legacy data. Unreadable or newer-version
files block loading and saving; all-invalid current data cannot silently revert
to an older campaign. A blocked launch presents a recovery screen with the save
folder, a copy-path action and a close action before creating the player or
autosaving. No reset or deletion happens automatically.

Each save serializes and verifies a complete candidate, writes a uniquely named
staging file, flushes it, verifies it again and publishes it with a filesystem
replace or move. A failed attempt cleans only its own uncommitted staging file.
It cannot replace the only recoverable temporary snapshot with a rejected
purchase. A corrupt primary never rotates into the good backup.

Purchases are serialized as a list constrained to zero or one record. A real
Unity test showed that a nullable inline class becomes an empty receipt object
on JSON round-trip; the list preserves absence correctly. Empty, duplicate,
orphaned and inconsistent receipts are rejected. Current-format validation is
read-only; only explicit legacy migration supplies the new field.

## Verification

- 391 portable domain assertions cover existing economy behavior, purchase
  refusals, stale quotes, save failure, duplicate confirmation, receipt
  consistency, migration purity and all eight existing command failure/retry
  paths. These checks do not run Unity.
- 97 assertions across 19 cases passed in Unity 6000.0.62f1 using real
  `JsonUtility` and temporary filesystem fixtures. They cover actual empty and
  purchased round-trips, legacy migration, byte preservation, current precedence,
  backup/temp recovery, corrupt/future barriers and failed publication. A failed
  repair payment reloads unpaid; a failed sale cannot appear through temporary
  recovery.
- Both Windows x64 Mono and Linux x64 Mono production builds completed in Unity
  6000.0.62f1 with exit code 0. Their matching source manifest records 147 files,
  including four genuine Unity-generated metadata files preserved after import.
  The Windows player contains 134 files; the Linux player contains 116. Neither
  runtime includes the native input automation or editor test fixtures.
- The actual Linux player passed six native runs across migration, fresh-game
  progression/restart, malformed-save recovery and future-format recovery. A
  copied legacy campaign imported with 150 credits and 400 rent, preserving the
  legacy primary/backup bytes and modification times. A fresh campaign completed
  the repair, collected 120 credits once, paid 50 rent and restarted with the
  resulting 150-credit balance and 400 rent intact. Native movement, laptop entry
  and first-person return were exercised with the new save boundary.
- Both blocked-save cases displayed the real recovery screen, copied the full
  save-folder path to the X11 clipboard and exited on Escape without changing
  their save directories. All six player processes exited 0. The retained
  [native report](../Validation/ShopSaveFoundation/native/acceptance.json) binds
  its 25 actual captures and six logs by SHA-256 to the corresponding Linux
  assembly and source manifest. Only the report, captures and logs were copied;
  campaign saves, preferences and test storage remain outside the repository.

This checkpoint's Windows executable was built but has not been run on Windows.
The user's earlier r2 Windows result does not cover these schema-two changes.
Cloud audio output was unavailable, and software-rendered Linux checks do not
establish target-PC performance. Recovery was viewed at 1280×800: long folder
paths were clipped in the field, while Copy returned the complete paths. There
is still no native shop encounter or purchase interface; purchase logic and
serializer checks have their separate scope above. See the
[evidence index](../Validation/ShopSaveFoundation/README.md) for the build and
source records.

Run portable checks with `bash SignalDebt/Tools/run-domain-tests.sh`. In Unity,
use **Signal Debt > Validation > Run Save Store Checks**, or invoke
`SignalDebt.Editor.SignalDebtSaveStoreChecks.RunBatch` in batch mode with
`-signalDebtSaveReport /absolute/report.json`. These tests create isolated
temporary fixtures and never use the player's save directory.

The ongoing desktop and dialogue redesign needs its own matching build and
runtime evidence. The later integrated shop step remains the walk to SHP-01,
inspection, explicit confirmation, physical handoff and return home. It must
prove the same receipt and ownership through an actual game restart before this
foundation is called a playable shop.
