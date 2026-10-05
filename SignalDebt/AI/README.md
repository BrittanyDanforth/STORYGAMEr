# Signal Debt NPC dialogue relay

This folder preserves the older localhost relay for development and its tests. The current game uses **Settings → Connect** and direct Inbox dialogue; it does not require Python or a relay-enable button. See [the current in-game setup](../Documentation/UI_Menu_Update.md). The instructions below describe this older adapter.

The relay uses Python 3.10 or later and only the standard library. It returns authored fallback when no key is configured, the provider is unavailable, or a reply fails validation. This is separate from the current Unity provider's visible connection-error feedback.

The implementation has **durable NPC knowledge, bounded retrieval, dialogue validation and stale-reply protection**. It does not yet implement a city population planner, detective strategy, court decisions or autonomous agents. Unity remains authoritative for every event, reward and consequence.

## Start without an API key

From this folder:

```powershell
python relay.py
```

It binds only `http://127.0.0.1:8765`. `GET /health` reports whether a provider is configured without revealing a key. The initial mode is authored fallback unless `OPENAI_API_KEY` exists in the relay process environment. `.env.example` documents environment variables; the relay deliberately does not read `.env` files automatically.

Run the checks:

```powershell
python -m unittest -v test_relay.py
```

All tests use fake providers or localhost HTTP. They make no paid API calls.

## Enable optional generated dialogue

Set a key for this terminal session, then launch the relay. PowerShell can request it without displaying your input:

```powershell
$secret = Read-Host 'OpenAI API key' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $env:OPENAI_API_KEY = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
}
python relay.py
Remove-Item Env:OPENAI_API_KEY
```

The Unity project contains no API credential. This prototype is a local single-player integration; a distributable commercial game would need a separately operated authenticated service rather than a shared key in game files. The relay rejects browser-origin requests and never enables CORS.

## Runtime network and prompt boundaries

The relay's only outbound application request is `POST https://api.openai.com/v1/responses`, and only when optional NPC generation is configured and requested. The destination is a code constant, not supplied by player text, model output, an environment base URL or a config file. HTTPS uses certificate verification. A dedicated opener rejects every redirect and every other destination, path or scheme; it ignores environment proxy settings. Systems that require a corporate proxy will therefore use authored fallback until their networking setup is changed deliberately. Normal operating-system DNS resolution for `api.openai.com` is still needed.

Inbound game traffic stays on loopback `127.0.0.1:8765` by default: `GET /health` and `POST /event`, `/dialogue`, `/cancel`. These local calls do not contact a website. The relay contains no web browsing, telemetry, downloads, arbitrary URL fetching, shell/process execution, model tools or runtime package installation. With no API key, its dialogue path makes no outbound API request.

Player text, NPC facts and model text are untrusted data. The model receives no tools or executable game commands. Its JSON must pass speaker, length, field, intent and known-fact checks. Even instruction-like words or URLs in a valid dialogue string remain plain presentation text: they do not cause a request, money transfer, evidence creation, game action or code execution. The Unity client must keep displaying that text without rich text or clickable link behavior and must never execute `proposed_intent` directly.

These boundaries limit what a prompt injection can affect; they do not guarantee that generated wording is always faithful, appropriate or immune to manipulation. A model may still produce misleading dialogue, which must never become authoritative evidence. The local relay also assumes a trusted desktop: it does not authenticate other native processes running on the same computer. Such a process could submit events or spend the configured local request budget. Keep it loopback-only; it is not an internet-facing service. This audit and its offline tests are not a claim that the whole project is vulnerability-free.

## Model and latency settings

Default: `gpt-6-luna`, reasoning `none`. The optional `gpt-6.1-sol` escalation uses reasoning `low` and is disabled unless `SIGNAL_DEBT_AI_ENABLE_ESCALATION=1`. A request must also explicitly carry `escalate:true`; no automatic fallback to a more expensive model occurs. Availability is subject to the API project used when the relay is run.

The model supplies wording. Remembering that a particular promise was made comes from saved event records and knowledge grants, not a claim that a model inherently remembers every previous exchange. A larger model does not repair an incomplete or incorrectly scoped event ledger.

| Limit | Default |
| --- | --- |
| Provider timeout | 6 seconds |
| Unity client timeout | 8 seconds |
| Automatic paid retries | Zero |
| Maximum provider output | 600 tokens |
| Maximum dialogue length | 90 whitespace-separated words and 1,500 characters |
| Dialogue requests | 20 per minute across the local relay |
| Simultaneous provider requests | 2 |
| Provider call budget | 100 attempts per relay run |
| HTTP request body | 64 KiB |
| Retrieved facts | 24 |
| Provider response body | 1 MiB |

Each request uses the Responses API with `store:false`, a strict JSON schema, no tools and no arbitrary commands. HTTP/provider failures switch to authored dialogue. A six-second timeout is a socket timeout, not a guarantee that the provider completed within six seconds; the independent eight-second Unity timeout ends the player's wait. Requests already sent to a provider can still incur usage after a local cancel. There is no automatic retry.

For developer experiments, set `SIGNAL_DEBT_AI_TIMEOUT` up to 30 seconds and adjust the Unity client timeout intentionally. Model, reasoning, port, database path, request rate and call budget are configurable. Other model IDs may require different capabilities; the relay returns authored fallback on an incompatible response rather than guessing parameters.

## Protocol version 1

All POST requests use `Content-Type: application/json`. IDs allow letters, digits, `_`, `.`, `:`, `-` and are at most 96 characters. No credentials go in request bodies. The Unity client is a native localhost client, not a browser.

### Record an observed event

`POST /event`

```json
{
  "campaign_id": "save-id.load-session-id",
  "event": {
    "event_id": "job_paid-1",
    "world_revision": 4,
    "description": "The client paid 120 after the repaired service passed its test.",
    "source": "observed",
    "known_by": ["copy_shop_client"],
    "tags": ["repair", "payment"],
    "commitment": false,
    "status": "resolved",
    "supersedes": []
  }
}
```

Sources are `observed`, `message`, `testimony`, `record`, `public`. These labels preserve how a fact was learned; `testimony` remains a claim, and `public` does not automatically grant knowledge to every NPC. The game explicitly supplies `known_by`. An empty array can advance the authoritative revision while granting no NPC knowledge. Never upload the full hidden world in `npc_context`.

Replies include `event_id`, `duplicate`, `world_revision`. Identical repeated events are idempotent. A reused event ID with changed contents returns `409 event_id_conflict`; new events older than the current revision return `409 stale_world_revision`. Multiple events may share one committed revision. Record events in order. Internally, every new event also advances a persisted ledger generation, so a same-revision resolution still invalidates a dialogue snapshot. Duplicate events do not advance that generation.

Optional `supersedes` lists earlier event IDs in the same campaign. A resolution is appended; original events remain in the ledger. An NPC stops retrieving the old commitment only after learning the resolution. This permits mistaken beliefs without rewriting history. The default is an empty list.

### Request dialogue

`POST /dialogue`

```json
{
  "campaign_id": "save-id.load-session-id",
  "npc_id": "copy_shop_client",
  "request_id": "conversation-7-turn-1",
  "world_revision": 4,
  "player_text": "Is the repair holding up?",
  "npc_context": {
    "name": "Mara",
    "role": "copy shop client",
    "disposition": "relieved but cautious",
    "goals": ["Keep the shop working"],
    "current_state": "At the shop counter"
  },
  "focus_tags": ["repair"],
  "allowed_intents": ["continue", "ask_question", "decline", "end_conversation"],
  "escalate": false
}
```

`allowed_intents` must be a nonempty subset of `continue`, `ask_question`, `decline`, `end_conversation`, `offer_help`, `express_concern`. These are suggestions for conversation only. Never automatically execute an effect from model text or an intent.

The response is flat and compatible with Unity `JsonUtility`:

```json
{
  "request_id": "conversation-7-turn-1",
  "npc_id": "copy_shop_client",
  "speaker": "copy_shop_client",
  "world_revision": 4,
  "text": "It's running again. I just hope it lasts through the afternoon rush.",
  "used_fact_ids": ["job_paid-1"],
  "proposed_intent": "continue",
  "source_mode": "model",
  "fallback_reason": "",
  "stale": false
}
```

`source_mode` is `model`, `authored_fallback`, `stale` or `cancelled`. Fallback is explicit. A stale or cancelled reply has an empty `text` and must not be displayed. The client must additionally compare the request ID, active NPC/conversation and current world revision before using any reply.

Exact repeated request IDs return the cached completed result without another paid call or another rate-limit slot, provided both the world revision and internal ledger generation still match. If events changed at the same revision, the cached response becomes an empty `stale` reply; use a new request ID for new work. Reusing an ID with different input returns `409 request_id_conflict`. The same ID while pending returns `409 request_pending`. All errors are `{ "error": "stable_code" }`; provider raw errors are suppressed.

### Cancel pending dialogue

`POST /cancel`

```json
{ "campaign_id": "save-id.load-session-id", "request_id": "conversation-7-turn-1" }
```

Response: `{ "request_id": "conversation-7-turn-1", "cancelled": true }`. Cancelling discards a pending result; it does not guarantee that an in-flight provider request was stopped or unbilled. Always drop late replies at the Unity boundary too.

## Memory and save behavior

SQLite stores immutable events, explicit per-NPC knowledge grants, current revisions, internal ledger generations and idempotent response records at `AI/data/npc_memory.sqlite3` by default. This directory is excluded from source control. The database has no API key or Authorization header. Player messages are not stored as dialogue facts automatically. The game must commit an explicit event when a conversation creates a promise, lie, grievance or disclosure that an NPC should remember. Older relay databases are migrated automatically; cached replies without a recorded generation are invalidated.

For each load, the Unity client uses a fresh campaign namespace combining its saved ID and a load-session ID. It hydrates the saved committed event ledger in revision order. This prevents a loaded older save from inheriting memories from a future branch. The relay database can survive restarts, while the game save remains the authoritative replayable history. Use a new namespace for a new game and never reset an existing campaign's revision backward. Old test namespaces can be removed by deleting the local database while the relay is stopped.

Retrieval first filters the NPC's known events and maximum revision, applies only resolutions that NPC actually knows, then prioritizes the requested topic. Active commitments within that topic come first; unrelated commitments fill only remaining slots. This prevents a large list of unrelated promises from crowding out directly relevant history. With no topic supplied, active commitments and recent events lead. Facts and their generation are captured together, and both completion and cache retrieval check that generation. This avoids using a global story summary as every NPC's brain. The current implementation scans that NPC's known ledger before selecting 24 facts; profiling and indexed topic retrieval are needed before scaling to thousands of active NPCs.

The validator rejects unknown fact IDs, wrong speakers, extra fields, unsupported intents, overlong dialogue and malformed output. It cannot prove that every sentence is a faithful interpretation of its cited facts. The authoritative game must not treat model prose as evidence or state mutation. Exact investigative and financial facts remain deterministic game records.

## Official implementation references

- [Structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs)
- [Responses API migration and store behavior](https://developers.openai.com/api/docs/guides/migrate-to-responses)
- [GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna)
- [GPT-6.1 Sol](https://developers.openai.com/api/docs/models/gpt-6.1-sol)

The automated suite makes no paid calls. Two separate authorized live smoke tests succeeded during this build: a structured provider reply, then a reply through this relay after reopening its SQLite memory. The second retrieved the client's repair and payment facts while excluding another NPC's private fact. Results are in `../Validation/api_smoke_result.json` and `../Validation/live_relay_result.json`. These small checks do not establish full-campaign dialogue quality or Unity runtime integration.
