# PC interaction milestone — native Unity game tested

The target remains the entire **320-feature master-plan backlog**. This delivery
finishes a tested apartment/PC/first-repair increment; it is not the completed
city, shop, investigation, court or custody game.

## What changed

The physical laptop now carries one live ASTER desktop. Press E to sit; the body
and first-person camera move toward the desk, hands reach the keyboard, and the
camera approaches the actual screen before desktop input opens. Esc or Stand up
backs out and returns to first-person movement. Early exit cancels the approach.

Inbox, Service, EXODISS and History call the actual game actions. The desktop
has a launcher, taskbar, draggable window, maximize/restore, minimize, app close,
scrolling and notifications. App selection, scroll positions and window layout
use bounded, versioned cosmetic preferences. Campaign saves remain separate.
The physical screen retains the same application when the player stands up.
Apartment notices, rest and pause use separate dialogs.

History displays the player's activity and receipts. Internal event identifiers
and NPC memory records are no longer presented as a desktop application.
Underlying NPC memory and save data are preserved.

Real Unity-generated asset metadata, Apartment scene, input/graphics settings and
dependency lockfile are now preserved in the editable project. Its `.gitignore`
excludes Unity caches and player build products.

## Checks actually performed

Evidence is in [Validation/PCMilestone](../Validation/PCMilestone/README.md).

| Check | Result and limit |
| --- | --- |
| Official toolchain | Unity 6000.0.62f1 and Windows Mono support installed with official package checksum verification. |
| Real project import/compiler | Passed with actual Unity APIs, supplied FBX assets and generated Apartment scene. |
| Windows x64 Mono build | Final r2 build passed, exit 0: 134 player files, 103,304,555 bytes. |
| Windows user test | On 5 October, the user answered “Launches and works” to the r2 launch/laptop/first-person-return check. This is user-reported evidence, with no Windows log or performance measurements supplied. |
| Linux x64 Mono build | Final r2 build passed, exit 0: 116 player files, 97,837,199 bytes. |
| Native game operation | An uninstrumented Linux player ran under Xvfb with real X11 keyboard/mouse input and llvmpipe rendering. Sixteen bounded observations are recorded, with actual framebuffer captures. |
| Laptop sequence | Walk/aim/E prompt, animated seating and screen approach, live physical screen, focused desktop, Esc return, repeated entry and an interrupted entry were observed. Captured seated hands and downward body views were reviewed. |
| Actual UI progression | Accepted the repair, inspected, rejected Print Queue, connected Order Index, tested and received 120 credits through mouse input. EXODISS displayed 200 credits and the receipt; paying 50 left 150 credits and 400 rent. |
| Persistence and layout | A fresh process restored the completed repair, one payout receipt, money/rent and desktop state. Native minimize, restore, maximize and dragging worked. 1600×900 and 1200×900 layouts were inspected. |
| Portable checks | 148 economy/repair assertions, 2,330 computer-session assertions, 27 relay tests and 12 source-packaging tests passed. These have their own narrower scopes. |

The original native clickthrough predates only the final History/Rest text cleanup.
The r2 acceptance addendum records the separate check of that final UI revision.
Neither run injects an automation component into the shipped game.

The editor's graphical UI and graphical batch Play Mode requests failed licensing.
The supported headless builder succeeded, and the resulting standalone Linux game
was tested directly. The package-free Editor menu **Signal Debt > Validation >
Run Computer Play Mode Checks** remains available for a licensed editor, but that
fixture suite did not execute here. Native game testing must not be relabeled as
an Editor Play Mode pass.

The computer-session runner is available from the repository root:

```bash
bash SignalDebt/Tools/run-domain-tests.sh
bash SignalDebt/Tools/run-computer-tests.sh
python3 -m unittest discover -s SignalDebt/Tools -p 'test_package_source.py' -v
```

## Remaining acceptance

- The user confirmed the r2 Windows launch/laptop flow. Later save changes need
  their own Windows check. Profile the actual GPU and driver;
  software rendering in this cloud is not a GTX 1080 Ti benchmark.
- Verify sound on an audio-capable machine. This cloud has no output device;
  FMOD reported initialization errors, so audio is unverified.
- Finish exhaustive camera/body/chair collision, blocked standing destinations,
  Alt-Tab and teardown checks. The recorded views establish only the sampled paths.
- Test exact scroll-position retention across aspect changes with overflowing
  content; the 4:3 run verified appearance and app/layout state, not scroll telemetry.
- Exercise corrupted/unwritable saves and later migrations on the target platform.
- Review patrol animation FBX self-intersection warnings before accepting those
  assets for later police gameplay. Headless build logs also retain nonfatal
  licensing and package-registry messages.

## Run or rebuild

For a delivered Windows player, extract the entire ZIP and run `SIGNALDEBT.exe`
inside it. Keep the `_Data` directory, UnityPlayer.dll and runtime files together.
Unity does not need to be installed on the player's PC.

To rebuild from source, use Unity 6000.0.62f1 with Windows Build Support and choose
**Signal Debt > Build Windows Player**. The output defaults to
`UnityProject/Builds/Windows`. The same entry point supports headless builds:

```text
-batchmode -nographics -quit -projectPath <absolute UnityProject path>
-executeMethod SignalDebt.Editor.SignalDebtMasterBuild.BuildWindows
-signalDebtOutput <absolute output directory> -logFile <absolute build log>
```

The next implementation increment is the starter street and a useful shop:
approach Jon, inspect without spending, confirm a purchase, receive an owned item
and receipt, return home and reload. Continue the full catalog through the
ordered milestones in [Backlog_Status.md](Backlog_Status.md).
