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

---

# Round 2 — more ideas, and the ones to avoid

Written after PUSH A FORTUNE was picked as the favourite. Every mechanic below was
saturation-checked before being recommended, including the ones that turned out to be dead.

## The saturation map

| Mechanic | Verdict | Evidence |
|---|---|---|
| **Coin pusher** | **OPEN** | Still the standout after round 2. Existing Roblox pushers are toys — no rebirth, no luck stat, no index, no persistence. Confirmed: the good version doesn't exist. |
| **Launch / fling distance** | **HOT WAVE — no entrenched #1** | Corrected after a bad first read. Launcher sims are *surging* on Roblox in 2026, not open: Chicken Rocket (The Woods Company) has 2.67M visits, ~432 CCU, a 97.5% rating and 43k upvotes; Launch! (Moneybag Games) is in beta and climbing. Nobody owns the ceiling yet. |
| Playable arcade tycoon | Crowded, differentiable | Build an Arcade (25M+ visits) and Arcade Tycoon are *builders* — machines generate passive income but you don't play them. The playable-machine angle is open. |
| Bowling / knockdown | Fragmented | KingPin, Bowling Simulator, Ball Throwing Simulator — several attempts, none broke out. Money-shot isn't novel enough for TikTok. |
| Stacking / tower height | Fragmented | Many small games, no mega-hit, weak money-shot. |
| **Claw machine** | **AVOID** | Claw Machine Master: 8.4M+ visits, 800+ plushies, 4 worlds. Plus Claw Machine Simulator (RB Battles), Claw Machine Arcade, Tokyo Claw Machine. Entrenched, and someone already shipped exactly the luck-engine version. |
| **Crusher / hydraulic press** | **AVOID** | Car Crushers 2: 1.31B visits, ~3k CCU. You would be launching into a mega-incumbent. |
| **Vacuum / suck-up** | **AVOID** | Commodity space — dozens of near-identical clones including brainrot tie-ins. Nothing to differentiate on. |
| **Domino chain reaction** | **AVOID** | Domino Chaos already ships the exact concept: cash per domino toppled, upgrades, trails, skins, "most satisfying experience on Roblox". |

The avoid list matters more than the idea list. Claw, crusher, vacuum and dominoes all *sound*
like great picks and all four would have been three wasted weeks.

## The rubric — why the pusher is the right favourite

Score any machine idea out of 5. The pusher scores 5/5, which is why it stood out:

1. **Built-in near-miss** — the machine generates tension for free, with no design work. There is always a coin teetering.
2. **Progress lives in the 3D world** — a pile you can see and film, not a number in a HUD. This is also the return hook.
3. **Idle-able** — auto-drop turns it into an idle game, which is what farms Creator Rewards and D1.
4. **Readable on a phone in one frame** — a TikTok viewer understands it instantly with no context.
5. **Cheap** — one machine, no map, no combat, no live population needed.

Anything scoring below 4 isn't worth building. Bowling scores 3 (near-miss yes, world-progress no).
Vacuum scores 2. Use this to kill your own ideas fast.

---

## New concept — FLING IT
*Launch-distance incremental. Entering a surging genre, not an empty one.*

**The clip:** the distance counter screaming upward as you smash through a zone gate at 400M studs and the screen shifts to a whole new biome.

**Read this first — the honest framing.** My first pass called this an open niche. It isn't.
Launcher sims are one of the genres actively surging on Roblox in 2026: **Chicken Rocket**
(2.67M visits, ~432 CCU, 97.5% rating, 43k upvotes) and **Launch!** by Moneybag Games (in
beta, climbing) are both live right now.

That changes the recommendation, but it doesn't kill it — arguably it improves it:

- **A rising wave with no entrenched #1 beats a genuinely empty niche.** Empty niches are
  usually empty because the format doesn't retain. This one is demonstrably retaining.
- **The audience is already educated.** You don't have to teach TikTok what a launcher is,
  which is the expensive part of launching any new format.
- **The specific gap is the engine, not the mechanic.** Chicken Rocket is physics-chaos plus
  items and skins; Launch! is straight progression. Neither runs the full luck engine —
  1-in-a-billion index chases, rebirth tiers, passives, server events, an odds-disclosed
  gacha. That's what PUSH A FORTUNE would bring to the format.
- **Chicken Rocket's 12.27 minute average session is the proof point that matters.** It clears
  the 10-minute Creator Rewards bar comfortably, which confirms the launcher session shape is
  the right one for this monetization model.

**The cost of entering:** you're racing active competitors instead of walking into an empty
room, so speed and differentiation matter far more here than on the pusher. Only take this
one if you can ship in weeks and commit to the update cadence.

**Core loop**
- One launch: aim, power, fire. Then 20–40s of flight — bouncing, boosting, breaking through gates.
- Distance converts to Cash. Cash buys launch power, bounce, boost fuel, glide, magnet.
- **Zone gates** every X studs: each one you break unlocks a new biome permanently, so the track itself is your progress bar — visible, filmable world-progress.
- **Near-miss is free:** landing 200 studs short of the next gate is agonising and happens constantly.
- Rebirth resets upgrades but permanently multiplies distance and unlocks the next launcher tier (slingshot → cannon → railgun → orbital).
- Rare mid-flight pickups feed the Index.

**Killer product:** `BOOST MID-FLIGHT — 25 R$`, repeatable, sold in the two seconds where you can see you're about to fall short of a gate. Same emotional slot as SHAKE THE MACHINE.

**Other monetization:** Auto-Launch (199) · 2× Distance / 2× Cash (149 ea) · Starting Height (149) · VIP (499).

**Effort: 2/5.** One long track, a velocity curve, and parallax biomes. No physics simulation needed — fake the flight entirely with a tuned curve so it's deterministic and mobile-cheap.

---

## New concept — THE ARCADE
*Not a different game. The expansion path for PUSH A FORTUNE — and the highest-leverage move available.*

Ship the pusher. Then add machines to the same lobby, one per content update. Every new machine:

- shares one **Ticket** currency and one **prize counter**, so it deepens the existing economy instead of splitting it;
- gets its own launch wave of TikToks, because it's a brand new money-shot;
- costs a fraction of a new game, because saves, big numbers, rebirth, index, luck and the odds popup are already built;
- gives lapsed players a reason to come back that isn't "number is bigger now".

**Machine menu, in build order:**
1. **Coin pusher** — the flagship, ships first.
2. **The Pop** — a balloon pump. Each pump multiplies the payout, and the balloon visibly strains. Bank or keep pumping. Press-your-luck with the best near-miss animation in the building, and it's maybe two days of work.
3. **Skee-ball / roller** — timing-bar based. A moving bar you stop in the green zone; "PERFECT" hits chain a multiplier. Timing bars are nearly free to build and generate near-miss on every single input.
4. **The Drop** — your reference game's mechanic, but as *one machine among many* rather than the whole product.
5. **Mega Pusher** — the shared server machine, as a live spectator event.

**Why this beats building a second game:** a second game splits your marketing, your update cadence and your player base. A second machine compounds all three.

---

## New concept — PLAYABLE ARCADE TYCOON
*Own the arcade. Machines earn while you're offline — and you can actually play them.*

**The clip:** a walk-through of an arcade stacked wall-to-wall with glowing machines, ending on the owner playing the one that just hit a jackpot.

**The gap:** Build an Arcade (25M+ visits) and Arcade Tycoon already own the *builder* framing — but in those games machines are furniture that prints money. Nobody has made the machines **playable** with real RNG loops behind them. Tycoon is one of Roblox's most durable genres and it has perfect offline income, which is exactly what the discovery algorithm and Creator Rewards both pay for.

**Loop:** place machines → they earn passively (including offline) → play any machine yourself for a much higher rate → cash funds better machines → expand the building → prestige into a bigger venue.

**Monetization:** the tycoon standards convert reliably — 2× Income (149), Auto-Collect (199), instant-build skips (dev product, repeatable), exclusive machine skins, VIP (499).

**Effort: 3/5.** Grid placement, an offline-income tick, and one playable machine to start. Reuses everything from the pusher.

**Caveat, stated plainly:** this is the most crowded room on the list. Only build it if the playable-machine hook is genuinely the centrepiece, not a bolt-on.

---

## New concept — DEEPER
*THE VAULT with motion. Same press-your-luck maths, far better clips.*

**The clip:** a mine cart accelerating into a blur as the depth counter and multiplier climb together — then either a triumphant brake, or a wall.

**Why:** the press-your-luck loop is the cheapest high-emotion mechanic there is, but a static room full of doors films badly. Put it on rails and every run becomes watchable. Speed, motion blur and rising numbers are what make a six-second clip legible on a phone.

**Loop:** ride down, depth multiplies your haul, brake to bank at any moment, crash and lose the run's haul (never your wallet). Depth zones change biome so progress is visible. Luck shifts obstacle density.

**Monetization:** `SAVE MY RUN` at 49→99→199 escalating in-run, plus 2× Haul (149), Luck (149), Auto-Brake at a chosen multiplier (99).

**Effort: 2/5.** A procedural track, a speed curve and one cart model.

**Policy:** identical rules to THE VAULT — free entry, no stake, wallet never debited, and the loss is a crash, not a lost bet.

---

## Five upgrades to PUSH A FORTUNE worth more than any new game

1. **Make rebirth change the machine's physical scale.** Don't just multiply a number — go tabletop pusher → warehouse pusher → stadium pusher → planet pusher. Rebirth becomes a money-shot generator instead of a menu action, and every tier is a fresh TikTok.
2. **A prize room other players can walk through.** Your Index becomes a physical room in the world that friends can visit. Collection turns into social flex, which is the strongest retention force on the platform, and it costs almost nothing to build.
3. **Add The Pop as machine #2.** Two days of work, a completely different money-shot, and it deepens the same ticket economy.
4. **One Ticket currency across every machine.** This is what turns a pile of minigames into an arcade, and it's the difference between an expansion and a distraction.
5. **Run the Mega Pusher as a scheduled live event.** Not an always-on feature — a countdown the whole server gathers for. Scheduled events are the single best D1 mechanism, because they give players a specific time to come back to.


---

# Round 3 — make it walk-around

The reference game is a *screen game*: you sit in a menu, there's no avatar, nobody can see
you. That's the cheap, fast lane — and it isn't the lane the durable money is in.

## The platform pays for co-play, not for mechanics

- Roblox ranks **retention and co-play** above almost everything else. As the trade write-ups
  put it: *an empty city full of friends beats a packed dungeon full of strangers, every time.*
- Social hangouts sustain **45+ minute sessions**. People stay for friends, not mechanics —
  and session length clears the 10-minute Creator Rewards bar with room to spare.
- The top-grossing games — Brookhaven, Adopt Me, Pet Simulator — are **all walk-around social
  worlds**. Screen games make good money fast; walkable ones make more money for longer.

**PUSH A FORTUNE was already designed this way.** The plaza, the Mega Pusher everyone gathers
at, the prize room friends walk through — that's the walk-around layer, and it's the structural
edge it already had over the game you were looking at.

## The pattern: the hub wraps the screen game

You don't replace the tight 3-second loop. You wrap it.

| | Reference game | The arcade |
|---|---|---|
| Modes | One: machine view | Two: hub + machine view |
| Avatar | None | You stand at the machine, visible to everyone |
| Exit from the loop | Boredom | Walk out into the hub |
| What it buys | The clip, the conversion moment | ...plus session length, social proof, flex, a reason to return |

**The technique is trivial.** Walk up to a machine and the camera tweens into it: set
`Camera.CameraType = Enum.CameraType.Scriptable`, `TweenService` the camera `CFrame` to a mount
part on the machine over ~0.6s, and on exit tween back and restore `Custom`. Your avatar stays
standing there the whole time, so **everyone walking past sees you playing it** — which is
exactly what the reference game can never have.

## What the hub buys you, concretely

1. **Your pile becomes status.** The persistent coin pile stops being private progress and becomes something others walk past and judge.
2. **The prize room is a place.** Your Index becomes a room friends physically walk through. Collection turns into flex.
3. **The Mega Pusher gets a crowd.** A server event people *gather at* hits differently from a progress bar. Whale spending becomes public theatre.
4. **Sessions get long enough to pay.** Hangout behaviour pushes past the 10-minute Creator Rewards bar without the player grinding for it.
5. **Unlocks become rooms.** A new machine tier is a wing you walk into, not a menu row.
6. **Rebirth becomes spatial.** Combined with scaling the machine physically: tabletop → warehouse → stadium. The venue changes around you.

## The three costs, stated honestly

- **Perf.** Physics machines *plus* avatars on mobile is a real budget problem. Only simulate
  coins on machines with a player at them; freeze the rest to a static mesh and resume on
  approach. Distance-cull aggressively. This decides your machine count per room.
- **Cost.** A hub is real work the reference game didn't have to do — but **keep it small**.
  One arcade room, not a city. Walk-around does not mean open world, and a tight room full of
  people beats a big map that feels abandoned.
- **Risk.** Walk-around games feel dead at low CCU, and you launch at low CCU. Mitigate with
  **small servers (12–16)** so they always look full, and put machines close together so the
  few players present are visible to each other.
