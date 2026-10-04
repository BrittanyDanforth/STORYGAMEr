# Runtime security boundaries

The game simulates hacking inside fictional data and rules. It has no real target scanner, Wi-Fi control, operating-system exploitation, remote-control payload, financial integration, or general web browser.

## Allowed connections

| Component | Intended runtime connections |
| --- | --- |
| Offline game | None. The optional relay is disabled by default. |
| Unity NPC client, when enabled | Fixed `http://127.0.0.1:8765`, only `/event`, `/dialogue`, and `/cancel`. Redirects disabled. |
| Local relay listener | Binds to loopback only. Native-client Host checks; browser Origin requests rejected; no CORS permission. |
| AI provider adapter, when configured | Fixed `https://api.openai.com/v1/responses`, with ordinary TLS certificate verification. No caller-supplied endpoint, redirects, or environment proxy routing. |

The provider request contains only the compact NPC dialogue context and permitted fictional facts. The API credential is read from the relay's process environment and used only in the provider Authorization header. It is not included in model context, Unity assets, saved game data, or this package.

Normal DNS/TLS traffic needed to reach the approved API accompanies that connection. Unity Hub/editor installation, engine package import, and the user's operating system are separate software; this source audit cannot promise that those programs never contact their vendors. No analytics, advertising, or remote-configuration package is declared by this project.

## Model text is untrusted

Player messages, NPC replies, retrieved memories, and facts are data. Prompt instructions help maintain the character, but do not enforce security by themselves. The code enforces the boundaries:

- No model tools, shell/process launcher, dynamic code evaluation, native plugin loading, or arbitrary URL fetcher is exposed.
- JSON output has a strict schema. Speaker, allowed intent, cited fact IDs, lengths, and world revision are validated. Unknown facts and extra command fields are rejected.
- Replies reach plain-text UI labels with rich text disabled. There are no clickable model links or embedded HTML/browser controls.
- Proposed conversational intent is never dispatched as a game action. Dialogue cannot award money, install a fictional implant, create evidence, commit ledger events, or alter files.
- Game rules produce events. The relay cannot promote arbitrary conversation text into authoritative memory. Per-NPC knowledge filtering occurs before provider context is constructed.
- The game rejects stale and cancelled replies. Offline authored dialogue covers missing keys, network failures, refusals, invalid responses, and exhausted limits.

## What was reviewed and tested

Authored Unity and Blender scripts were searched and read for network APIs, URL opening, shell execution, dynamic evaluation, plugin loading, and model-derived action dispatch. The only authored game network surface found is the optional NPC client described above. Blender generators perform local asset construction and export.

The character revision adds local geometry, bone poses, imported animation clips, and a separate visual review scene. The animation importer clears clip events; contact frames have no authority to change doors, combat, evidence, or saves. Blender source opens explicitly disable embedded script execution. `Blender/verify_asset_security.py` records a read-only inventory of the final apartment, character, and animation sources, including embedded text, drivers, linked assets, and network references.

The review found a real transport weakness in Python's default redirect behavior: a provider redirect could move an authenticated request away from the fixed endpoint. The delivered provider adapter rejects redirects and disables environment-selected proxies. Regression tests cover this boundary without making paid calls.

Adversarial fake-provider checks cover instruction-like player text, forged hidden-fact citations, extra command fields, and command/URL-looking response prose. Such prose stays inert; it creates no game events and performs no network fetch. See `Validation` for exact results. The two earlier live memory smoke tests preceded the final transport hardening; the hardened transport was subsequently checked with offline tests, not another paid call.

The v0.9 shoulder and equipment pass uses original local meshes and public reference pages only. No third-party character, rig, add-on, or texture pack was imported. The final three Blender files were opened with automatic execution disabled and inspected read-only: zero embedded text blocks, drivers, linked libraries, and network references were found. `Blender/asset_security_check.json` pins the inspected files by hash. `Validation/v09_unchanged_runtime.json` verifies that the player controller, NPC client, AI relay, and apartment source remain unchanged. This pass made no paid AI call and added no runtime networking.

## Limits and next verification

This is a bounded source review and regression suite, not proof that every dependency or possible input is vulnerability-free. It does not guarantee that model prose is truthful or cannot contain misleading suggestions. Keeping prose powerless limits the effect of those failures.

The loopback service assumes the user's local machine is trusted. It is not a hardened multi-user server and must not be exposed through port forwarding or a public bind. A compromised process on the same machine can still cause local interference; per-run provider call and request limits reduce accidental cost, not all local abuse.

Unity Play Mode and a built Windows player remain untested here. Before release, trace the actual player's connections with AI disabled and enabled, confirm only the stated application destinations, exercise malformed local responses and timeouts, and repeat checks after dependency or network-code changes. The game's fictional hacking systems must retain this offline boundary as they expand.
