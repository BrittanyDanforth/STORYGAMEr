# Three Slop Games Worth Building

A design dossier: teardown of the ball-drop game that's printing money, the reusable
engine underneath it, the 2026 policy rules that constrain it, and three concepts
that reuse the same backend with three different viral money-shots.

---

## 0. What "150k Robux a day" actually is

Work the number backwards before designing anything, because it sets the CCU target.

| Step | Value |
|---|---|
| Net Robux/day (creator keeps 70%) | 150,000 |
| Gross player spend/day | ~214,000 R$ |
| DevEx at standard $0.0038/R$ | **~$570/day** |
| DevEx at US-18+ verified rate $0.0054/R$ | **~$810/day** |
| Typical luck-sim ARPDAU (gross) | 3–6 R$ |
| → Daily active users needed | **~36k–71k DAU** |
| → CCU at ~10:1 DAU:CCU | **~3.5k–7k concurrent** |

So the target is a top-few-hundred game, not a top-10 game. That is reachable by one
person. Everything below is aimed at that number.

**Second revenue stream most people forget:** Creator Rewards pays roughly 5 R$/day for
each "Active Spender" (someone who has spent $9.99+ on Roblox in the last two months)
who plays your game 10+ minutes — but only for the first three experiences they open
that day. Two design consequences, both free money:

1. Build a reason to be the **first** thing they open (offline earnings that cap, a
   daily chest, a claim streak).
2. Build a reason to stay **past 10 minutes** (a chest that unlocks at minute 10, a
   session-length luck ramp).

An idle-friendly game collects this on top of gamepass revenue. A twitchy PvP game
mostly doesn't.

---

## 1. Teardown — why the ball-drop game works

Strip the theme off and it's nine systems. Every one of them is a retention or
monetization surface, and none of them is hard to build.

| # | System | What it actually does |
|---|---|---|
| 1 | **2–5 second core action** | Drop → land → number pops. Short enough to be a 6-second clip, long enough to feel like an event. |
| 2 | **Two RNG layers** | Micro (every drop pays something) + macro (1-in-7.91B collectible). Short-term dopamine + long-term chase. |
| 3 | **Exponential currency** | K → M → B → T → Qa. Numbers inflate so progress always feels fast even when it isn't. |
| 4 | **~50 upgrades on a skill tree** | The tree screenshot *is* marketing. Turns "buy upgrade" into "build a strategy". |
| 5 | **Rebirth** | Wipes progress, multiplies output. Converts a finished player into a new player. |
| 6 | **Index / collection** | Completionism. The only system that retains players who've maxed everything. |
| 7 | **Passives / offline income** | The D1 retention engine — and D1/D7 return behavior is what the 2026 discovery algorithm actually ranks on. |
| 8 | **Overlapping timers** | "Mystery event 6:05", "Luck x25 1:12", "Tickets x2 5:26", "Jackpot lowered 0:08". You can never cleanly leave. |
| 9 | **Auto-everything** | Auto-spin/auto-drop. Removing the gameplay *increases* retention and sells as a gamepass. |

**The single smartest thing in those screenshots:** `ADD 3 MINUTES — R$45`. A micro-priced,
infinitely repeatable dev product that extends a *server-wide* jackpot timer. It converts
because it's cheap, because it's sold at a moment of urgency, and because the whole
server sees you do it. That's status, not just a purchase. Every game below has an
equivalent.

**The pattern to copy:** the payer isn't buying power, they're buying *the removal of a
wait* or *a visible moment in front of other people*.

---

## 2. The rules you have to build around (2026)

These are not optional and most first-time devs get moderated for them.

**Simulated gambling is banned.** No wagering virtual chips or currency on an outcome
where you can lose the stake. No bet-size input. No poker/blackjack/roulette-with-bets.
Unplayable gambling *scenery* is fine.

**The line that keeps luck games legal:** a chance-based **reward** is not a wager.
If the action is free (or costs a non-purchasable, regenerating resource) and *always*
returns something, it's a reward mechanic. The moment a player can select an amount from
their balance and lose it, it's a wager.

Three concrete rules that keep you safe:
- Use **two currencies**: a regenerating, non-purchasable resource to *act* with, and a
  separate currency you *earn*. Never let players spend earned currency for a chance to
  win more earned currency.
- Never debit a persistent wallet on a loss. Risk unbanked, in-run progress only.
- Frame every loss **diegetically** — the floor collapses, the guard catches you, the
  ball misses — never "you lost your bet".

**Paid random items must disclose odds.** Since the global rollout in June 2026 (driven by
Korea's loot-box law), every paid random outcome needs each result's probability shown as
a percentage, summing to exactly 100%, *before* purchase. This includes:
- Anything bought with Robux, **and** anything bought with in-game currency that can be
  purchased with Robux.
- Luck potions / lucky gamepasses — you must numerically explain how they shift the odds.

Build the odds-disclosure popup as a reusable UI component on day one. Every crate, egg,
capsule and luck boost routes through it.

---

## 3. The three games

All three run on the same backend (Section 4). They differ in the six-second clip they
generate, which is the only thing that determines whether you get discovered.

---

### GAME 1 — PUSH A FORTUNE
*Coin pusher + the full luck engine. The flagship.*

**The clip:** a forty-coin avalanche pouring off the ledge in slow-mo, with a rare capsule
tipping over right at the end.

**Why this one:** coin pushers already exist on Roblox and they're all toys — no rebirth,
no luck stat, no index, no persistence. The arcade psychology is the most proven in the
world (near-miss is built into the *physics*: there is always a coin teetering) and nobody
has bolted the 2026 luck engine onto it. You're not competing with a dominant #1.

**Core loop**
- **Tokens** regenerate (+1 / 3s, cap 50, accrues offline). Not purchasable — this is what
  keeps the game policy-clean.
- Drop tokens onto the shelf. The pusher slides on a 2.2s cycle. Coins fall off the front
  → **Cash**.
- Rare **Prize Capsules** spawn on the shelf. Push one off → roll the Index collectible.
- The pile **persists per player** across sessions (serialize up to ~120 coin positions).
  You log back in and your pile is exactly where you left it — an unusually strong return
  hook, because your progress lives in the world instead of in a number.

**The social layer:** a **MEGA PUSHER** in the plaza. Everyone feeds the same machine,
everyone sees a shared jackpot meter, and when the ledge dumps, the payout splits by
contribution. A whale's spending is physically visible to the whole server.

**Monetization**

| Product | Price | Type | Why it converts |
|---|---|---|---|
| **SHAKE THE MACHINE** | 35 R$ | Dev product, 1 per 5 min | The killer. Sold at the exact second a coin is teetering. |
| Golden Token (heavy, plows the pile) | 25 / 199 (×10) | Dev product | Impulse buy, instant spectacle. |
| Auto-Drop | 199 R$ | Gamepass | Converts the game to idle. |
| 2× Cash / 2× Luck | 149 R$ each | Gamepass | Standard, always sells. |
| +Token Cap & regen | 99 R$ | Gamepass | Sells to the AFK crowd. |
| VIP (all of the above + shelf skin) | 499 R$ | Gamepass | Top of the ladder. |
| Starter Pack | 99 R$, first 24h only | Dev product | Limited-time offers convert ~2× permanent ones. |

**Build effort: 3/5.** The only genuinely hard part is physics performance on mobile.
Mitigations: hard-cap coins per machine (~120 parts) and auto-absorb the oldest into pile
value; drive the pusher with `TweenService`/CFrame rather than a physics constraint so it's
deterministic; simple cylinder coins with tuned `CustomPhysicalProperties`; one shared
mega-pusher instance per server, not per player.

**Policy: clean.** Tokens regenerate and can't be bought; every drop returns value; the
capsule roll is a paid random item, so it goes through the odds popup.

---

### GAME 2 — ROLL A GIANT
*Katamari/Hole.io on Roblox, with theft drama.*

**The clip:** you roll over an entire building — then eat another player and their whole
ball joins yours.

**Why this one:** Roblox has no serious katamari. The mechanic is proven at massive scale
on mobile (Hole.io, Big Big Baller), and *theft drama* is the single strongest viral engine
on Roblox right now — the whole "Steal a ___" wave runs on it. This makes theft physical
and spectacular instead of a UI interaction. It also needs zero art budget: the money-shot
is scale, which is free.

**Core loop**
- 3-minute rounds, 12 players, one compact city arena (~300×300 studs).
- Touch anything at or below your absorb tier → it sticks, you grow.
- At 1.25× another player's mass you can absorb **them** — you take their mass. This is the
  clip and the rage-quit and the reason they queue again.
- Round ends → mass converts to Cash at a multiplier → leaderboard moment.
- The **lobby holds the entire luck engine**: upgrades, rebirth, index, quests. Rounds
  generate the content; the lobby does the retention and the monetization.

**Round-based scoping is the trick** that makes this buildable: props reset every round, so
there's no world persistence, no streaming, and no save complexity in the arena at all.

**Monetization**

| Product | Price | Type | Why it converts |
|---|---|---|---|
| **GROWTH SERUM** (+50% size, rest of round) | 45 R$ | Dev product | Sold in the two seconds before a bigger player eats you. |
| Head Start (spawn larger) | 149 R$ | Gamepass | Fixes the worst part of the experience. |
| Magnet radius | 199 R$ | Gamepass | Pure convenience, no PvP complaints. |
| 2× Mass / 2× Cash | 149 R$ each | Gamepass | Standard. |
| Ball skins & auras | Index rolls | Paid random | **Must show odds.** Cosmetic = cheap to produce, safe, and flexes in every round. |

**Build effort: 4/5.** The hard part is welding hundreds of parts to a rolling ball.
Mitigations: only keep the most recent ~60 absorbed props visually welded, everything else
becomes size only; use `Model:ScaleTo()`; server owns mass, client owns the visual sticking;
prop pool respawns per round so nothing leaks.

**Policy: clean.** No wagering anywhere. The only paid-random surface is cosmetics.

---

### GAME 3 — THE VAULT
*Press-your-luck. The cheapest to build, the loudest clips.*

**The clip:** two kinds, and you want both. The x900 cash-out — and the run that dies at
x899. **Loss clips travel further than win clips.**

**Why this one:** the highest emotional amplitude per line of code on this list. There is
no physics, no map, no combat. One room prefab, three doors, a multiplier counter, a
particle burst. A working version is a weekend; the rest is the shared engine.

**Core loop**
- Entering is **free**. You descend floors.
- Each floor: three doors. Behind them — a multiplier bump, a bonus, or the **ALARM**.
- Multipliers compound against your run haul. **Bank** at any moment → it goes to the wallet.
- Alarm = caught. The run's haul is gone. **Your wallet is never touched.**
- Deeper floors: fatter multipliers, higher alarm rate. Luck stat shifts the door odds.
- Rare loot found on floors feeds the Index.

**Retention:** a **Daily Vault** — one free run per day at a 5× base multiplier. That single
feature is the "be their first game of the day" hook that also farms Creator Rewards.

**Monetization**

| Product | Price | Type | Why it converts |
|---|---|---|---|
| **SAVE MY RUN** | 49 → 99 → 199 R$, escalating within a run | Dev product | Sold at peak emotional pain. The best-converting product type that exists. |
| 2× Haul | 149 R$ | Gamepass | Standard. |
| Luck (shifts door odds) | 149 R$ | Gamepass | **Odds impact must be numerically disclosed.** |
| Extra free daily run | 99 R$ | Gamepass | Cheap, recurring value. |
| Auto-Bank at chosen multiplier | 99 R$ | Gamepass | Sells to cautious players; also an idle enabler. |
| VIP | 499 R$ | Gamepass | Top of the ladder. |

**Build effort: 1/5.** Lowest on the list by a wide margin.

**Policy: needs care, and the care is easy.** Entry is free, so there's no stake. Never
debit the persistent wallet. Never offer a bet-size selector. Present the loss as being
*caught by security*, not as losing a wager. Under those four rules this is a roguelike
run, which is unambiguously a game mechanic — the same as losing your carried loot on death
in any dungeon crawler.

---

## 4. The shared engine — build it once

All three games are the same backend with a different 3-second action bolted on top. Build
this first and each additional game costs weeks, not months.

**Systems**
- `ProfileStore` for saves — session locking, no data loss, no exceptions.
- **Big-number module** (mantissa + exponent). You pass 2^53 fast; native Luau numbers will
  silently corrupt your currency once you hit quadrillions.
- **Server-authoritative RNG.** The client never rolls, never decides a payout, and never
  reports a result. It requests, the server rolls, the client animates the outcome it's told.
- **Rate-limit every RemoteEvent.** Uncapped remotes are how these games get destroyed.
- `CollectionService` tags for props/upgrades so content is data, not code.
- **Odds-disclosure component**, reusable, wired into every random purchase.
- **Mobile-first UI.** Most of your players are on phones — design the HUD at phone size
  first and check thumb reach, then scale up.
- Playtest at a full server, on a mid-range phone, before launch.

**Content structure:** upgrades, rarities, zones and index entries all live in module tables
so you can ship a content update in ten minutes. Near-daily updates are what hold the
algorithm — you need the update pipeline to be trivial.

---

## 5. Launch

**Discovery in 2026 ranks return behavior** — D1 and D7 — not CCU or session length. Offline
income, daily chests and streaks aren't just retention nice-to-haves, they're literally the
ranking input.

- **TikTok is the discovery engine.** 3–5 clips a day. Money-shot inside the first half
  second. Text overlay states the promise flatly, exactly like the reference clip: *"get 15
  passive rolls and earn 10 per rebirth."* Not clever — specific.
- **Sponsored ads seed the signal, they don't replace it.** You pay to get first players in;
  their D1/D7 and payer conversion is what the algorithm actually promotes on.
- **Micro-influencers (10k–50k)** consistently beat big creators on ROI for this genre.
- **Ship codes with every update.** Code posts are free traffic on every Roblox codes site,
  and those sites index new games aggressively.
- **Near-daily updates** for the first two months.

---

## 6. Recommended build order

1. **Build the shared engine** (~2 weeks). Save system, big numbers, upgrades, rebirth,
   index, offline income, odds popup, mobile HUD.
2. **Ship THE VAULT first** (~1 week on top of the engine). It's the cheapest possible test
   of the engine, the marketing pipeline and your update cadence — with real revenue and
   the loudest organic clips. Learn on a game you can afford to have fail.
3. **Then PUSH A FORTUNE as the flagship** (~3–4 weeks). It has the best long-term retention
   of the three — persistent pile, genuine idle loop, ASMR-adjacent watchability — and the
   least credible competition. This is the one aimed at holding 5k CCU.
4. **ROLL A GIANT last**, and only if the first two funded it. Highest ceiling, highest build
   cost, and the only one whose retention depends on a live player population.

---

## Sources

- [Ball Game codes / mechanics — PCGamesN](https://www.pcgamesn.com/ball-game/codes), [Pocket Tactics](https://www.pockettactics.com/ball-game-codes), [Pro Game Guides](https://progameguides.com/roblox/ball-game-codes/)
- [2026 DevEx cheat sheet — RoHire](https://rohire.dev/blog/2026-devex-cheat-sheet) · [Roblox DevEx help](https://en.help.roblox.com/hc/en-us/articles/13061189551124-Developer-Exchange-Help-and-Information-Page)
- [Roblox discovery algorithm 2026 — ROLearn](https://rolearn.dev/insights/roblox-game-discovery-algorithm-2026/) · [BLOXG marketing guide](https://bloxg.com/guides/roblox-marketing)
- [Creator Rewards payout guide — ROLearn](https://rolearn.dev/insights/roblox-creator-rewards-payout-guide/) · [Creator Rewards docs](https://create.roblox.com/docs/creator-rewards)
- [Roblox Community Standards](https://about.roblox.com/community-standards) · [Paid random items policy](https://create.roblox.com/docs/production/monetization/paid-random-items) · [Korea loot-box rules push global odds disclosure — TechTimes](https://www.techtimes.com/articles/319148/20260626/koreas-loot-box-rules-push-roblox-disclose-item-odds-worldwide.htm)
- [Monetizing a Roblox game in 2026 — Medium](https://medium.com/@andy.a.g/the-complete-guide-to-monetizing-a-roblox-game-in-2026-c5e915a7c778) · [Most profitable genres — RobloxDesk](https://www.robloxdesk.com/most-profitable-roblox-game-genres-2026/)
- [Roblox genre trends 2026 — KitsBlox](https://kitsblox.com/blog/popular-roblox-game-genres-2026) · [Creator landscape 2026 — RoLearn](https://rolearn.dev/trend-reports/roblox-content-creator-landscape-2026/)
