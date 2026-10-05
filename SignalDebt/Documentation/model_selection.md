# NPC model recommendation — checked 4 October 2026

**Provisional primary: `gpt-6-luna`. Optional escalation: `gpt-6.1-sol`.** Remove GPT-4.1 mini from the plan. Astra is excluded. This selection matches Luna's documented high-volume role and low token price; it is not a claim that untested NPC dialogue is automatically better. Verify character consistency, slang comprehension, hidden-fact leakage, latency, and refusal/fallback behavior using representative scenes before enabling it by default. [S1, S3, S6]

## Current comparison

Standard text rates in USD per million tokens; ordinary short-context requests. Cost example uses 3,000 uncached input tokens plus 200 output tokens, before additional reasoning, cache writes, retries, regional premiums, hosting, audio, or taxes. [S1–S5]

| Model | Context limit | Input / output rate | Example per turn | 1,000 examples | Proposed role |
|---|---:|---:|---:|---:|---|
| `gpt-6-luna` | 1,050,000 | $0.10 / $0.50 | $0.00040 | $0.40 | Ordinary short NPC dialogue and intent |
| `gpt-6-sol` | 1,050,000 | $2.00 / $10.00 | $0.00800 | $8.00 | Earlier Sol comparison candidate |
| `gpt-6.1-sol` | 1,050,000 | $2.00 / $10.00 | $0.00800 | $8.00 | Rare complicated scenes, if evaluation justifies it |
| `gpt-5.4-mini` | 400,000 | $0.75 / $4.50 | $0.00315 | $3.15 | Alternative only if game-specific tests favor it |
| `gpt-5.4` | 1,050,000 | $2.50 / $15.00 | $0.01050 | $10.50 | Higher-cost comparison baseline |

The official GPT-6 Sol page identifies GPT-6.1 Sol as its newer version. Including it prevents choosing an older escalation model merely because it was named first. No listed context advantage justifies GPT-5.4 over Luna for this workload. Full-campaign quality remains unverified; Luna access and basic event recall passed the two small live checks recorded in `Validation`. Sol access has not been tested. [S2–S6]

These figures are arithmetic examples, not guaranteed scene costs. Reasoning tokens are charged as output. GPT-6.1 Sol cannot disable reasoning, so its 200-output example is especially optimistic if 200 means only visible text. Measure `usage.output_tokens`, including reasoning. For GPT-6 models, prompts above 272,000 input tokens attract higher full-request rates. Keep NPC packets compact rather than routinely using the maximum context. [S1–S3, S8]

## Suggested configuration

Primary: Responses API, `model: "gpt-6-luna"`, `reasoning: {"effort":"none"}` for routine dialogue. Luna also supports low, medium, high, xhigh, and max; its documented default is medium, so set none explicitly. [S1]

Escalation: `model: "gpt-6.1-sol"`, `reasoning: {"effort":"low"}`. Its supported efforts are low, medium, high, xhigh, and max; neither none nor minimal is accepted. Omit temperature, top_p, and log-probability parameters whenever reasoning is enabled. [S3, S6]

The delivered relay uses a 600-token total output cap. A later, separately budgeted Sol evaluation can trial a 4,096-token cap; this larger cap is not enabled in the delivered default. These are project tuning hypotheses, not vendor recommendations or latency guarantees. The latter includes hidden reasoning and may still truncate; incomplete output must not commit actions. Do not escalate ordinary greetings or retry a refusal on another model to bypass it. [S8]

Request a strict Responses JSON shape through `text.format`, not Chat Completions' differently shaped `response_format`. Example schema configuration, authored for this game: [S7]

```json
{
  "text": {
    "format": {
      "type": "json_schema",
      "name": "npc_turn",
      "strict": true,
      "schema": {
        "type": "object",
        "properties": {
          "line": {"type": "string"},
          "intent": {"type": "string", "enum": ["answer", "clarify", "decline"]},
          "fact_ids": {"type": "array", "items": {"type": "string"}}
        },
        "required": ["line", "intent", "fact_ids"],
        "additionalProperties": false
      }
    }
  }
}
```

Require every schema field; reject unsupported facts and stale world revisions locally. Handle refusals and incomplete responses outside the schema. Structured Outputs constrains form; it does not prove statements are true. Neither model should directly decide guilt, transfer money, invent evidence, or write permanent memories. [S7]

The game's database remains NPC memory. Retrieve that person's authorized facts, relevant event-linked memories, commitments, current scene and recent dialogue for each request. A million-token window is capacity, not automatic persistent personal knowledge. Shared API models can serve distinct NPC state safely when contexts and validation remain isolated.

## Sources opened and inspected

- S1: https://developers.openai.com/api/docs/models/gpt-6-luna
- S2: https://developers.openai.com/api/docs/models/gpt-6-sol
- S3: https://developers.openai.com/api/docs/models/gpt-6.1-sol
- S4: https://developers.openai.com/api/docs/models/gpt-5.4-mini
- S5: https://developers.openai.com/api/docs/models/gpt-5.4
- S6: https://developers.openai.com/api/docs/guides/latest-model
- S7: https://developers.openai.com/api/docs/guides/structured-outputs
- S8: https://developers.openai.com/api/docs/guides/reasoning
- Pricing cross-check: https://developers.openai.com/api/docs/pricing

Documentation research informed this configuration. A separate live provider smoke test is recorded in Validation/api_smoke_result.json.
