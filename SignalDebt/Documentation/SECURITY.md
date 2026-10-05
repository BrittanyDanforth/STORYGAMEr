# Runtime security boundaries

The current menu and laptop implementation uses a direct, optional AI dialogue
connection. The game still simulates hacking through fictional data and rules:
it has no real target scanner, Wi-Fi control, operating-system exploitation,
remote-control payload, financial integration or general web browser. See
[the current UI and menu scope](UI_Menu_Update.md) for player-facing behavior.

## Current application connections

| Component | Intended connection |
| --- | --- |
| Campaign rules, repair, wallet, saves and desktop | No application network connection required. |
| Inbox Send or Settings Connect, with a configured key | Fixed `https://api.openai.com/v1/responses` using Unity's normal TLS certificate validation. Redirects are disabled. |
| Older development relay path, if explicitly enabled in development | Fixed `http://127.0.0.1:8765`, only `/event`, `/dialogue` and `/cancel`; redirects disabled. The shipping menu does not enable or require this path. |

The current menu calls `SignalDebtAiProvider` through `SignalDebtNpcClient`.
There is no Python process, local relay setup or separate enable switch in this
flow. Once a key is available, Send uses it automatically. Merely starting the
game, opening Settings, loading a campaign or accepting/completing a repair does
not initiate a paid AI request. Connect explicitly sends one small test request.
Failures produce a fixed, readable status; the player can continue the authored
gameplay. A failed request is not presented as a successful model reply.

The provider receives the NPC's authored personality/current concern, the player's
message, up to 12 recent visible dialogue lines and facts the copy-shop client knows.
The history includes authored client messages and excludes private Notice rows.
It is session-scoped conversational context, not persistent social memory.
The current client admits only `job_accepted`, `service_test_passed` and
`job_paid` ledger events. Money, rent, shopping and private inspection/reconnection
events are excluded from that client's context. The credential is used only for
the Authorization header, never as model context or campaign data.

The transport has a 30-second timeout measured independently of pause, a 32 KiB
request limit and a 64 KiB response limit. Dialogue output is capped at 600
tokens; the connection test uses 96. Both share a limit of 20 attempted requests
per minute and 200 per running process. There are no automatic retries. These
limits bound ordinary requests; they are not a monetary budget or a guarantee
that aborting a request prevents provider charges. Requests use `store: false`.

Normal DNS/TLS traffic accompanies the API connection. Unity installation,
engine package import and operating-system traffic are separate from this
authored application path. No analytics, advertising or remote-configuration
package is declared by this project. Actual player traffic for the new direct
connection has not yet been traced.

## Local data and API keys

The Windows and Linux players use `UserData` beside the executable. Campaigns
live in `UserData/Saves`, archived campaigns in `UserData/Backups`, and cosmetic
desktop preferences in `UserData/desktop.json`. An opted-in remembered key lives
separately in `UserData/ai-settings.json`. New Game archives the local campaign
directory; it does not archive keys or desktop preferences with that campaign.

The key field is masked and starts empty. A loaded key is never prefilled into
the editable field, logged by authored code or included in displayed transport
errors. Entered keys are session-only by default. Choosing **Save key in
UserData folder** explicitly stores a readable plaintext key; this is not
encryption or Windows account-bound storage. Anyone able to read that file can
use the key. Do not include it when sharing the game folder or a bug report.

There is no Windows Credential Manager, registry credential or PlayerPrefs key
storage. A session-only replacement leaves an existing remembered file unchanged
for the next launch. The status label reflects whether the currently active key
matches the remembered file. Remove saved key clears the remembered and session
keys when removal succeeds. The existing `OPENAI_API_KEY` environment fallback
is lower priority than entered/session and remembered keys; removal does not
modify a process environment binding. No environment setup is needed for the
in-game key entry flow.

Key writes use a unique staging file and replace/move publication without a
credential backup. Ordinary write failures retain the prior saved/session key.
Unix file permissions are restricted where the runtime supports the local chmod
call; this does not change the plaintext storage contract. No credentials are
bundled in the player or required in source control.

Continue prefers any local save-family candidate over older application-data
storage, including invalid local candidates that must not be bypassed. Only
when no local save-family candidate exists can it read the older application-data
campaign and publish the imported campaign under `UserData/Saves`. The old
source files remain read-only. The schema-two save foundation and its recovery
limits are documented [separately](Shop_Save_Foundation.md).

## Model text has no action authority

Player messages, conversation history and facts are untrusted data. Prompt
instructions guide dialogue, while code enforces the following boundaries:

- No model tools, shell/process launcher, dynamic code evaluator, model-selected
  native plugin or arbitrary URL fetcher is exposed.
- A bounded JSON parser rejects duplicate properties, malformed encoding and
  excessive depth. Dialogue requires the exact schema, expected speaker,
  permitted intent and permitted fact IDs. Unknown citations and extra command
  fields are rejected. Text is limited to 90 words and 1,000 characters.
- Replies enter plain-text labels with rich text disabled. Markup, code and URL
  forms are rejected; no model link opens a browser.
- Proposed intent is wording only. Replies cannot award money, create evidence,
  change inventory, commit ledger events or write campaign files. Valid prose
  can still be mistaken or misleading.
- Campaign identity, world revision and cancellation checks suppress stale
  replies. Leaving the computer, changing campaign or opening key settings
  cancels outstanding dialogue; closing Settings aborts its connection test.

## Evidence and its scope

The [schema-two checkpoint](../Validation/ShopSaveFoundation/README.md) passed
391 portable domain assertions, 97 real Unity serializer/filesystem assertions,
Windows and Linux builds and six actual Linux player runs. Those records bind
147 production files before the current menu/direct-AI revision; they do not
validate its later UI or network changes. The user's earlier Windows r2 report
likewise covers that older delivery only.

For the earlier UI/menu revision, both final `ui-menu-r2` Windows/Linux builds
succeeded from the same 161-file source snapshot. [Retained checks](../Validation/UIMenuUpdate/README.md)
separate 20 broad native Linux checks on `ui-menu-r1` from 13 targeted final-r2
checks. They cover local campaign storage, archive/cancel, rejected-save
preservation, older-save import, menu navigation and missing-key feedback.
The new Windows player has not been executed here. Real Unity passed 73 provider
contract assertions and 13 key-store serializer/filesystem assertions using
isolated fabricated data; a separate .NET suite passed 173 key-store assertions.
Those checks did not cover remembering a key in the standalone player. A later
native test found that the old serializer threw before saving; the shipped r3
download should use session-only keys. The replacement codec passed actual Linux
Remember/restart/Remove checks with fabricated data. One invalid-key request
produced the expected readable rejection; that is not successful authentication
or generated dialogue. Matching-source evidence is tracked in
[NPC dialogue update](NPC_Dialogue_Update.md).
The user reported working direct AI replies but generic behavior. Agent-run
authenticated dialogue remains unverified because a credential has not been
securely bound for automated validation. No key from chat was copied into code,
documentation or fixtures. Graphical Editor Play Mode remains license-blocked;
the actual Linux standalone route is separate evidence. Cloud audio output is
unavailable, and software-rendered Linux runs do not establish PC performance.

Earlier relay and asset reports remain historical evidence. The Python relay's
redirect/proxy hardening and fake-provider adversarial tests cover that adapter,
not the new Unity transport. Two earlier live memory checks preceded the final
relay hardening. Development relay code remains compatible with that old path;
its loopback listener must not be exposed as a public service.

The previous local character/animation inspections used Blender with automatic
script execution disabled. Their reports recorded no embedded text, drivers,
linked libraries or network references in the inspected assets. Animation import
clears clip events; animation frames do not control campaign state. Asset hashes
and the v0.9 unchanged-runtime report describe their recorded revisions, not an
unchanged runtime claim for today's menu and direct provider.

This is a bounded source review and revision-specific testing. Authenticated
dialogue, cancellation under network load and the current Windows player still
need their own matching runtime evidence.
