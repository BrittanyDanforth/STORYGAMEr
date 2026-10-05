# Menu, laptop and local data update

This records the apartment/PC interface increment delivered as Windows r3. It adds a
main menu, simpler laptop screens and in-game direct AI settings. It does not
complete the 320-item game plan or add a playable shop encounter. Current source
and implementation behavior are described below; validation status is separate
at the end.

**Later finding:** a subsequent standalone test exposed a serializer failure
when **Save key** was checked. The broad and targeted runs below did not cover
that operation. Leave Save key unchecked in the r3 download. The replacement
codec and its validation are tracked in [NPC dialogue update](NPC_Dialogue_Update.md);
the reports below remain historical evidence for their recorded source.

## Starting and pausing

The game opens to **Continue**, **New Game**, **Settings** and **Quit**. Continue
is available when a campaign candidate exists. New Game asks for confirmation
when a campaign exists and archives the local campaign before creating a new
one. A failed archive/save prevents starting a replacement campaign.

Escape opens Pause during gameplay. Resume returns to the current activity:
pausing at the focused laptop keeps the player at the desk and preserves the
desktop. Settings opens above the menu or Pause; Back/Escape closes Settings
first. Use the desktop's leave-computer action to animate back to first person.
Escape during a seating/standing transition first returns to a standing state
before showing Pause.

The laptop retains its physical seat/camera transition and window controls. Its
apps are **Inbox**, **Repair**, **EXODISS** and **History**. Inbox uses a compact
client conversation, one relevant job action and a message composer. Repair
shows one stage at a time: inspect the Order Folder, choose its missing Order
Index service, test the restored connection, then collect 120 credits. The other
service choices give a short correction rather than changing the campaign.
Payment and rent remain rule-controlled, durably saved actions.

## Files beside the game

Extract the complete player folder to a writable location before running the
executable. The Windows/Linux player creates these local paths as needed:

| Path | Purpose |
| --- | --- |
| `UserData/Saves/` | Current campaign and supported backup/recovery candidates. |
| `UserData/Backups/Campaign-…/` | Local campaign directory preserved when New Game archives it. |
| `UserData/desktop.json` | Cosmetic desktop preferences, separate from progress. |
| `UserData/ai-settings.json` | Optional, readable plaintext API key; created only when explicitly saved. |

Current campaigns use `signal-debt-save-v2.json`. Continue prefers local save
families. If none exists locally, it can read an older campaign from Unity's
application-data location, migrate supported legacy data and save the result
under `UserData/Saves`. The old application-data files are not changed.
Malformed, unreadable or newer-format local candidates do not silently fall
back to an older campaign. Continue reports the load problem instead of starting
a player or overwriting the rejected campaign.

After import, progress belongs to the local current-format campaign. Playing an
older executable later does not merge its progress into this campaign. New Game
archives local Saves, while desktop preferences and AI settings remain separate.
Do not share a `UserData` folder containing a saved API key.

## AI settings without a relay

Open Settings, enter an OpenAI API key in the masked field and press **Connect**.
The field starts empty even when a key is already available. Connect applies the
entered key and sends one small connection test; provider charges may apply.
The default choice keeps the key for this running session only. Checking **Save
key in UserData folder** writes readable plaintext to `ai-settings.json` so the
game can use it on later launches. There is no Windows account credential store.

A session-only replacement does not erase an older saved key. The status says
whether the active key is saved or available only for the session. **Remove saved
key** clears the saved and session keys when removal succeeds; an explicitly
configured environment fallback, if present, remains outside that action.

Once configured, Inbox Send uses the key directly. There is no relay-enable
toggle, Python service or extra launch command. Opening the game or Settings
alone does not send a paid request. Only **Send** and **Connect** initiate direct
AI requests. Missing keys and connection errors produce visible status text;
the authored job, repair and wallet can still be used.

The fixed destination is OpenAI's Responses HTTPS endpoint. The client sends the
message, bounded conversation and only its three known job-event types. AI
replies provide dialogue wording and cannot award payment or change world state.
Requests time out after 30 seconds, have size/output and per-session rate limits,
and are never automatically retried. Leaving the computer cancels dialogue;
closing Settings cancels its connection test. See [the security notes](SECURITY.md)
for exact boundaries and storage
details.

## Validation status

The earlier [schema-two checkpoint](Shop_Save_Foundation.md) passed its domain,
real Unity save-store, Windows/Linux build and six native Linux checks. It is
historical evidence before this UI change. The user's successful Windows r2
launch also belongs to the older delivered executable.

The final `ui-menu-r2` Windows and Linux players built successfully from the same
161-file source snapshot. [Retained validation](../Validation/UIMenuUpdate/README.md)
includes a broad `ui-menu-r1` native run (20 checks, 30 captures, four clean exits)
and targeted final `ui-menu-r2` runs (13 checks, 23 captures, two clean exits).
The latter covers seating interruption, repeated pause/settings/resume, readable
History/receipts, missing-key feedback without a stuck reply, and importing an
older campaign while preserving its original files byte-for-byte and timestamp.
The r1 run separately covers the full four-stage repair, restart, New Game
archive/cancel, and malformed/future-save handling. No player instrumentation
or graphical Editor Play Mode was used for these checks.

Real Unity checks passed 73 provider-contract assertions and 13 isolated local
key-store serializer/filesystem assertions. Another 173 portable key-store
assertions passed under .NET. These fabricated-data checks do not establish a
successful real API request or key persistence in the Windows player. Live
direct AI remains unverified because a credential has not been securely bound
for automation. No credential from chat was copied into files. The new Windows
player, audio output and target-PC performance remain unverified at runtime.
This evidence covers the menu increment, not completion of the master backlog.
