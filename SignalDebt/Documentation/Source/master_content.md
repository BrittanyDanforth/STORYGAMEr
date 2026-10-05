# Open World Hacker Game Master Plan

Game vision systems and production roadmap

Prepared for Elijah • Version 0.9 • 4 October 2026

## 1 The game we are building

Build an underworld-first, first-person 3D hacker crime and fraud simulation with connected life systems. You start behind on rent and close to losing your room, with limited money, basic equipment, and contacts offering a dangerous way forward. Hacking is the main profession, progression system, and source of income. Your home, relationships, spending, reputation, and personal choices give that career context and consequences in a city full of people who remember what happened. You can see your arms and legs, sit at a real desk, learn on your laptop, take jobs, get up, and live in the world your work changes. A careless decision can hurt a person, create evidence, attract an investigator, lead to a court case, and change your life after release from jail. Stolen information, fictional ransomware, laundering, crew payouts, and spending belong to one persistent economy. A viable white-hat route lets the player earn a living and build a different future inside the same dangerous world.

The working project name is Signal Debt. This is a placeholder, not a final brand. EXODISS is the requested fictional finance app on the laptop. I am interpreting the Exodus reference as a wallet and transaction dashboard; its name and functionality can remain while its artwork, layout, and fictional currencies are original. If EXODISS was intended as an entire browser, the desktop architecture can accommodate that without changing the core game.

The engine choice is Unity 6 long-term support, abbreviated LTS. The initial source fixture uses Built-in rendering; Universal Render Pipeline, abbreviated URP, remains the production candidate to benchmark before expansion. This is a production recommendation, not a measured claim that Unity always runs faster than Godot. The target is a polished 1080p experience on your reported GTX 1080 Ti, subject to testing on the actual CPU, RAM, storage, and drivers. Start with single-player Windows, an offline-complete campaign, and optional small-model AI dialogue. No Astra model is required.

This document defines the full ambition and the order in which to prove it. AAA quality is the standard for responsiveness, consistency, presentation, and depth; a complete AAA-scale city is a large, multi-discipline production. An API and a long feature list cannot replace animation, environment art, design iteration, QA, and reliable engineering. The first deliverable in development should be one excellent playable neighborhood that demonstrates the entire consequence chain.

### What makes this game distinctive

Your laptop is part of your life. Closing it does not erase a promise, payment, investigation, or damaged relationship. The same person can appear in your inbox, at the shop, in a police statement, and later as a witness or friend. The world changes through specific causes that the player can understand.

Progression means learning to handle richer situations. Early jobs teach one clear concept. Later jobs combine technical decisions, incomplete information, people, time, and competing obligations. More advanced play should demand judgment, not obscure command memorization or looking up an answer outside the game.

Failure produces another chapter. Losing a client, getting charged, or serving a sentence can change available work and relationships without deleting the character. The main campaign follows the rise from desperate solo jobs to an operator with equipment, brokers, a crew, rivals, and investigative exposure. The lawful route has its own complete progression and endings without displacing that underworld focus.

### Working assumptions

The setting is an original fictional city with invented organizations, networks, currency, computer tools, and laws. Hacking and fraud operate inside a closed game simulation. They never connect to arbitrary real targets, run real exploit tools, or move real money. This makes authored puzzles reliable and lets the design concentrate on choices and consequences.

The initial scope assumes one developer or a small team, with specialist assets or help added as needed. Multiplayer, a fully drivable metropolitan region, procedural interiors for every building, and unrestricted AI-generated quests are later possibilities. None is required to establish the central experience.

## 2 The first paid repair and the door into the underworld

Begin broke at home. The room is dark, cramped, and overdue for repairs. Rain taps the window; the refrigerator struggles in the next room. An overdue rent notice lies beside an old laptop. Its screen lights up with a message. The player already has control and can look down at their body, cross the room, and sit. No opening montage, compulsory course, or advanced hacking menu stands between them and the first useful action.

The notification offers a real beginner repair for a real person in the simulation. The player learns by completing paid work. This practical start makes later betrayal meaningful: they first understand what legitimate access is, how someone trusts them, and what the equipment actually does. The surrounding game remains underworld-first; its dangerous opportunities emerge as the player becomes capable of using them.

### Pressure that the player understands

For initial tuning, start with 80 available, 450 overdue rent, and three in-game days before a stated housing consequence. The first repair pays 120. Two visible follow-up jobs paying 160 and 180 make lawful survival possible. These are fictional balance figures. Show the exact shortfall, due date, and available payment arrangement. Pause the clock during instruction and substantial reading.

Rent, electricity, connection service, and equipment expenses remain relevant. Missing a bill leads through notices, restrictions, negotiation, or a recoverable housing change. It does not silently destroy the save. The pressure should make money matter without making the tutorial a race against eviction.

### The first thirty minutes

**Minutes 0–3: The screen lights up.** The message reads, “shop computer wont open the order folder. can pay today if u fix it.” Press E to sit. A concise job card identifies the client, the problem, the agreed fee, and the permitted workspace. Opening EXODISS reveals the balance and rent shortfall. The laptop’s glow, visible hands, worn keyboard, and room sounds establish the mood while the player acts.

**Minutes 3–8: Inspect an actual problem.** Accepting opens the client’s authorized repair session. This is their persistent simulated computer, not a separate training level. The client continues messaging and trying to work. Teach three actions in context: open the device panel, inspect a status, and compare the expected connection with the current state. One clearly labeled fictional service is unavailable. The interface explains the relevant rule without presenting a page of jargon.

**Minutes 8–14: Fix what you now understand.** The player restores the service using a short visual connection puzzle and runs a test. Every needed concept has already been demonstrated. A wrong choice produces specific feedback and a reversible result. Hints progress from identifying the relevant status to showing one useful next action. The client can now open their order folder; its successful use is a world event, not merely a green completion banner.

**Minutes 14–18: Notice a boundary.** Another workspace is visible beside the repair panel. The client says, “job folder only. dont touch the other stuff.” The player can leave it alone, ask about it, or inspect its access label. This introduces curiosity and boundaries without granting unexplained powers. Copying restricted material, installing an implant, and altering financial records are not beginner tools. No hidden action is committed by looking at the label.

**Minutes 18–22: Receive money that counts.** Close out the repair and give a short explanation through an authored response. The client confirms the result. EXODISS records the agreed payment; the client resumes their task and remembers the assistance. Choose whether to make a partial rent payment or retain cash for a known expense. Show the remaining balance and what each decision changes.

**Minutes 22–26: The tempting message.** A contact responds to the completed job through a credible referral or job-board record: “small fixes pay small. got other work when ur ready.” A restricted listing advertises substantially more money, with an unfamiliar requirement and unclear client interests. Reply with interest, ask what the work involves, or decline. Interest creates a future conversation, not acceptance of an unseen crime. The familiar repair tools cannot perform the advertised operation yet.

**Minutes 26–30: Give the next step a physical purpose.** Visit the nearby repair counter to inspect a cheap tower, discuss a component, or collect a basic tool. E prompts name each action; inspecting never buys automatically. The seller offers a credible lead on further work. End with earned money, one learned skill, a recurring client, a visible equipment goal, and a dangerous opportunity waiting beyond the next few jobs.

These intervals are pacing estimates. Slower readers receive the same opportunities. No immediate arrest, surprise malware installation, or mandatory criminal act completes the tutorial.

### Escalation through earned capability

The next jobs add diagnosis, permissions, file handling, and device-state changes one concept at a time. Courses become short, searchable help inside that work. Once the player has learned the relevant fictional tools, a later legitimate commission presents a deliberate betrayal opportunity: misuse access, copy a specific story object, or perform an unauthorized simulated action. Respecting the client also advances a viable career.

That choice opens a layered criminal ecosystem: small dishonest jobs; repeat brokers; specialized fraud and information deals; crew operations; disputed proceeds; and leadership with obligations. Abstract laundering contracts can later involve fees, delays, counterparty disputes, and legal exposure. They never teach real financial routing or erase the money’s recorded origin. Larger earnings buy useful hardware and breathing room, while unfinished commitments remain.

### Consequences that feel personal

Fear should come from specific connections. A message quotes a detail known to a former collaborator. A client reports the exact intrusion they discovered. A supplier mentions a legitimate inquiry about an identified purchase. A crew member with a known appointment arrives demanding an overdue share. Each event requires knowledge and a causal history; nobody acquires the player’s address or guilt by magic.

Alternate paid work, upgrades, new opportunities, and developing consequences. Do not interrupt every success with disaster. The repeatable loop becomes learn, earn, improve, choose, and live with what follows. No unrelated hobby or domestic chore is needed to keep that life active.

## 3 Engine choice and hardware plan

Use Unity 6 LTS with C# as the starting point. The first importable apartment fixture uses the Built-in Render Pipeline and Standard materials to minimize import dependencies. Evaluate URP with the same scene before expanding the final lighting and material pipeline. URP is designed for scalable rendering across hardware classes [R1]. The reason to prefer this route is the combined workflow for a 3D character, detailed interiors, UI, content streaming, profiling, and an expanding production team. The evidence does not establish an automatic frame-rate advantage over Godot.

Godot remains a credible alternative. Its Forward+, Mobile, and Compatibility renderers have different feature and performance tradeoffs; the official documentation specifically distinguishes these choices rather than promising one universal winner [R2]. Its MIT license is attractive if open-source control is a priority [R3]. If Godot is substantially easier for the developer, that productivity matters more than theoretical engine rankings.

Your reported GTX 1080 Ti has 11 GB of video memory according to NVIDIA [R4]. The approximate 30 GB RAM figure is unconfirmed; do not assume a particular CPU or SSD. System RAM and graphics memory are different budgets. More RAM will not fix expensive shadows, too many active NPC decisions, or blocking disk loads.

### The test that decides

Make one representative street, the furnished apartment, a visible player body, three enterable spaces, the working laptop, configurable crowds, and the actual investigation simulation. Start with a stress configuration of 25 nearby pedestrians, six moving vehicles, and two active police agents. These are test inputs, not promised final capacities.

Run a packaged Windows build on the real machine at 1920 by 1080. Use a repeatable ten-minute route through the apartment, street, busy intersection, laptop, and a pursuit or police response. Compare the same content and settings if a second engine is evaluated. Test three passes after warm-up and separately record first-load behavior. Unity supports profiling an application on its target device [R5].

Initial targets are average frame time at or below 16.7 milliseconds, 1 percent low frame rate at or above 45 FPS, and no recurring traversal stall above 100 milliseconds. Reserve headroom for later content. These are proposed acceptance targets, not measurements or guarantees. Inspect main-thread time, GPU time, memory, animation cost, navigation, garbage collection, and asset loading before changing engines.

Use baked indirect light, controlled dynamic lights, reflection probes, mesh and animation LOD, occlusion, streamed interiors, and adjustable shadows. The baseline does not depend on ray tracing or DLSS. Prioritize a coherent art style, hand animation, readable screens, and strong sound over expensive effects that do little for play.

### Simulation budgets

Nearby characters use full movement, animation, and perception. Other characters in a loaded area use cheaper schedules. Distant residents remain persistent data with event-driven appointments and outcomes. A resident becoming distant must not reset their identity, inventory, promise, or case involvement.

Initial scheduling assumptions are local decision checks a few times per second, distant schedule evaluation every few in-game minutes, and investigations updated when evidence or deadlines change. Tune these rates through profiling. Do not run every detective, resident, browser window, and economy simulation every rendered frame.

## 4 First person body home and city

The player has a coherent underlying body rig. First-person view shows arms, legs, and feet. Hands align with doors, the laptop, a phone, packages, and desk objects. Sitting changes posture and eye height. Movement includes walking, sprinting, crouching, stairs, turning, and simple obstacle traversal. Swimming and elaborate parkour are not prerequisites.

Use a stable gameplay camera with body animation following it. Hide head, neck, and torso renderers for the player camera while retaining the full skeleton. If separate first-person hands are used, suppress overlapping body arms and preserve consistent sleeves, skin, shadows, and held objects. Test extreme look angles, narrow doorways, chair transitions, and different aspect ratios.

The laptop has two presentation modes: a physical screen in the world and a focused, readable desktop view. The same application state backs both. Focus mode enlarges text without changing the rules. One key releases mouse focus, one action stands up, and urgent physical events have understandable audio and visual cues. Motion blur, head bob, camera shake, and body visibility are adjustable.

### The apartment

The starting home is a cramped, poorly maintained rental with a bed, small kitchen and bathroom, desk, worn laptop, limited storage, and an intercom. An overdue notice and a clear housing deadline establish the pressure. Objects earn their complexity by supporting atmosphere, a decision, or a system. Daily-life props support the atmosphere; dedicated food and household-chore minigames are outside the scope.

The main interactive expansion is a hardware workbench with a desktop tower, screwdriver, fastener tray, spare components, packaging, cable connections, and a diagnostic display. Rest remains a time-skip option. The kitchen and bathroom establish a believable apartment without creating unrelated daily maintenance systems.

Rent, equipment deliveries, neighbors, maintenance, visitors, and home-business activity connect the apartment to the city. Upgrades improve organization, workspace, storage, comfort, or available jobs. A better chair should not magically increase hacking success. A second monitor can improve task visibility; better fictional hardware can expand approved simulation capacity without removing the need for judgment.

Persistent changes make the room yours: furniture, computer parts, client gifts, awards, unpaid notices, case notes, and a laptop left open on yesterday’s task. A seized laptop, missed rent payment, or supportive friend can change the space through a specific event.

### A dense city before a huge city

Start with one neighborhood that can be crossed in a few minutes. The full core target is three connected districts with different income, institutions, workplaces, and routines. Prioritize useful interiors over map area: homes, café, repair shop, coworking space, library, clinic, small businesses, police office, courthouse, legal aid office, and detention facility.

Use foot travel and public transit first. Later vehicles must serve activities that justify their production cost. Fast travel to discovered places advances game time by a stated amount and warns about known appointments. Story-critical changes cannot happen invisibly during a loading screen without a recap.

Each important location needs a reason to return: a person, service, schedule, evolving story, or player-owned resource. Most background buildings can be façades. A district should feel inhabited through light, sound, deliveries, changing signs, conversations, and recognizable people.

### Press E to talk and interact

Use a clear focus-based interaction system. Looking at an accessible NPC within a starting test distance of roughly two meters shows E Talk to the clerk. A part shows E Inspect GPU, a parcel shows E Collect order, and a laptop shows E Sit and use. The prompt names the actual target and action. Require a clear reach or line of sight so the player cannot interact through walls or select a hidden object behind a shopkeeper.

Tap E performs the displayed action. Hold E reveals alternatives only when the object has several useful actions, with a visible hold indicator. Talking and inspecting never spend money. Buying shows the item, condition, compatibility, price, and remaining balance before one deliberate purchase action. Esc exits the interface; keyboard input inside chat or the laptop cannot accidentally activate an object in the room. Rebinding and controller prompts remain supported.

Keep dialogue in the physical scene when practical. The clerk can look at the player, retrieve an item, remember a previous dispute, or explain why stock is unavailable. Conversation choices include browse, ask about an order, request a diagnosis, sell a part, dispute a fault, and discuss a relevant job. Do not make every employee a generic quest dispenser.

### Shops with different purposes

The new-parts shop offers dependable stock, compatibility advice, warranties, and ordered components. The used dealer offers cheaper parts, trade-ins, uncertain condition reports, and occasional questionable provenance. The repair counter offers diagnosis, a paid service, short technical jobs, and a workbench when the player’s own machine is unavailable.

A pickup desk or locker handles physical deliveries with persistent order records. A landlord office or building terminal handles rent, notices, repairs, and recorded payment arrangements. A later specialist contact offers unusual fictional hardware or access to a restricted job circle. Each place needs useful staff, opening hours, stock or service state, and reasons to return.

Inspect stock in first person, compare it with the saved build plan, agree on a price, receive a package and receipt, and bring it home or arrange delivery. Pack small components together to avoid repeated hauling. Optional queue skipping keeps a busy shop believable without making the player wait through every customer.

Money, stock, ownership, the receipt, and any seller promise update in one transaction. The clerk remembers what they personally sold or discussed. A used GPU can later connect a supplier dispute, a customer repair, or a police question, but an ordinary purchase is not proof of a crime.

### Activity traces that connect the physical and digital world

An action can leave an observation or record without anyone yet knowing that a crime occurred. Store visits, pickups, payments, messages, device sessions, shared files, and crew discussions create different trace opportunities. The world stores these records according to the action and location; investigators cannot read the hidden event ledger directly.

Use a five-step chain: an action occurs; a source retains an observation; someone notices a relevant problem; an investigator or other actor obtains that source through a permitted game action; a claim is evaluated against supporting and contradicting evidence. Skipping those steps is the main source of unfair, omniscient policing.

| Activity | Possible retained source | What it cannot establish alone |
| --- | --- | --- |
| Buying or collecting hardware | Shop ledger, receipt, observed customer, item identity. | Why the buyer wanted it or what they later did with it. |
| Messaging a target or broker | A recipient’s received thread and shared attachments. | The truth of every statement or the sender’s real identity. |
| Receiving a game payment | Transaction record, settlement state, contract reference. | Whether every associated act was criminal or who knew what. |
| Using a simulated remote session | A scenario-defined device event or observable interruption. | Who caused it without a valid attribution link. |
| Sharing a record with a crew | Membership-scoped access, supplied copy, conversation history. | Knowledge by members who never received or accessed it. |
| Visiting a location | A witness observation or a functioning scene-specific recording. | Events outside that observer’s view or an exact motive. |

Each trace has an ID, source, represented claims, event time or interval, location, subject links, retention rules, discoverability, acquisition requirements, reliability, and access permissions. When collected into a case, preserve that history. Repeated copies share their original source family and do not become independent corroboration.

Detection, suspicion, attribution, and proof remain different states. A target can notice a problem without identifying anyone. A clerk can recognize the player without knowing their online alias. A detective can have a plausible hypothesis that fails a required charge element. Contradictory evidence matters at every review.

Give the player understandable feedback about known exposure: a message was received, a payment is disputed, a clerk has called, a technician is examining a device, or counsel has disclosed a collected record. Avoid a magical universal heat percentage or a checklist promising zero traces. Ordinary records are part of the world, and their significance can change later.

For example, an underworld payout funds a GPU purchase. Later, a broker dispute produces a message thread; a relevant authorized inquiry produces a merchant record; the two may support a question about timing or possession. They still do not establish the whole offense. The case must show the missing connection, and the player can have a legitimate explanation supported by other records.


## 5 The laptop and fictional internet

Build a simulated operating system with a persistent desktop, taskbar, app windows, notifications, file viewer, messages, notes, calendar, course portal, mission workspace, browser, and settings. Windows remember their position and content. Opening the laptop resumes work reliably after saving or leaving home.

The browser renders authored in-game pages and state-driven widgets. It does not need a real web browser engine or arbitrary internet access. Search indexes public fictional pages and discovered records; it cannot reveal private case facts because the player typed the right phrase. URLs, attachment types, account identities, and timestamps use a coherent invented convention.

### EXODISS

EXODISS shows fictional balances, pending and cleared transactions, invoices, escrow, payment requests, account notices, and a searchable activity history. Money moves only through validated game transactions with a unique ID and source. A chat message saying “paid” does not increase the balance.

A payment record links to the relevant contract, person, case, and receipt where the player is allowed to see them. A disputed job may delay an escrow release. A court order can restrict funds under the fictional law. A mistaken payment can be investigated and returned. The player sees the practical effect and any known reason.

For the first build use one fictional currency and a simple wallet. Multiple speculative assets and market simulations are later systems. Price movements must have bounded rules and readable causes; no actual crypto wallets, seed phrases, exchange accounts, or real financial connection are needed.

### The website ecosystem

Create twelve initial website types: an underground job exchange, broker profiles, crew channels, EXODISS, courses, lawful contracts, parts listings, local news, a search portal, housing and bills, city services, and legal services. The underground exchange, crew channels, and broker histories are substantial recurring interfaces with offers, disputes, references, and changing reputations. All pages belong to the game simulation.

Sites evolve. A clinic posts a service notice after an incident. An employer changes a listing after your work. A forum debate quotes only public information. A course receives a new advanced exercise when an authored story unlocks it. The world’s history should be visible on the computer as well as the street.

Use clear ownership and permission labels on files. A notebook stores citations, theories, and personal annotations separately. A player theory remains a theory until supported; dragging two cards together never creates a fact in the simulation.

## 6 Hacking that becomes deeper as you improve

The technical system is a collection of interacting puzzle grammars. Each has explicit rules, a learnable visual language, and several difficulty parameters. The foundations support fictional intrusion, restricted access, record manipulation, extraction of story objects, defense, repair, and investigation. Legal status depends on consent, scope, intent, and what happened, not the color of a minigame.

Every challenge exposes its goal, accessible information, relevant tools, known constraints, and available help. Early tasks contain no hidden prerequisite. Later tasks can have uncertainty, but missing information must be discoverable through play. A failure explanation identifies what the player observed and which rule was violated.

### Eight stages of learning

Stage 1 teaches orientation and recovery. Sort fictional records, find a missing project, and reconstruct a simple timeline. A guided exercise highlights the relevant panel; mistakes cost a small amount of time and can be reversed.

Stage 2 teaches connections. Route a service through a graph of invented devices with limited capacity and blocked links. Explain why each connection is valid. Difficulty adds dependencies and alternative routes rather than requiring real network commands.

Stage 3 teaches access and scope. Arrange fictional capability tokens to reach a requested resource. Introduce expiration, ownership, and shared resources. Teach the distinction between a tool being capable of an action and a client permitting that action. Later contracts let the player deliberately cross the same clearly shown boundary.

Stage 4 teaches diagnosis. Compare device behavior, activity logs, and accounts of an incident. Early anomalies are clean; later cases include innocent explanations, missing intervals, and contradictory witnesses. Reward careful uncertainty.

Stage 5 teaches service dependencies. Route limited fictional processing capacity between linked services and predict what becomes unavailable when one is stopped. Repair, intrusion, and later disruption contracts reuse the same understood rules with different objectives. Timers are generous or pausable according to difficulty settings.

Stage 6 teaches automation. Assemble bounded visual blocks such as inspect, compare, filter, mark, and request approval. Test the tool against synthetic examples. Reusable tools remove mastered repetition without becoming real executable attack code.

Stage 7 teaches social and technical uncertainty. Make or challenge a role claim using authored conversation intents, known records, and the other person's verification options. An explicitly dishonest choice is available in the training fiction. The system distinguishes a claim from a verified fact; confident language is not a universal persuasion bonus.

Stage 8 teaches operations. Coordinate several connected systems, a physical visit, another person, and a deadline. Multiple approaches remain viable. Advanced play changes dependencies, objectives, and consequences, not just puzzle length.

For example, an advanced crew operation uses six capacity slots across observation, reconstruction, and transfer of a designated story archive. An active NPC changes applications, a teammate has a limited task window, and a scheduled shutdown affects the plan. The player arranges task blocks, assigns roles, and tests a synthetic preview. One route completes the original objective quickly; another accepts a partial result to meet a crew commitment. A changed client demand creates a new decision. The interface explains dependencies and known constraints so pressure comes from planning rather than guessing. The same puzzle framework also supports lawful recovery work.

### Courses and assistance

Each course has a short demonstration, guided practice, independent exercise, and optional challenge. The first hint names a concept, the second locates a useful clue, and the third shows one valid next action. Hints remain free in normal story play. A sandbox allows rehearsal without jeopardizing a contract.

Use practical credentials to unlock work, but also track performance, reliability, and experience. A certificate does not guarantee success. Repeatedly mastered work unlocks delegation or abbreviated resolution. Players who enjoy manual solving can retain it.

### Difficulty and detection

Separate puzzle complexity, time pressure, information uncertainty, economic pressure, and legal consequences into adjustable settings. A player may want hard reasoning with relaxed timers. Difficulty changes should not silently rewrite the rules of evidence.

During a risky fictional operation, display what is known: a monitored component, an expired permission, a noisy action, or a reporting warning. Do not display a universal countdown that pretends every system has identical detection. A technical alert, a human report, and attribution to the player are different events.

## 7 Black hat progression and the choice to go legitimate

The default tone and main campaign are underworld-first: precarious money, questionable brokers, active targets, high-paying promises, compromised information, crew politics, and investigators connecting events. Black-hat work is a developed career with several job families, escalating responsibility, and sustained progression. White-hat play is a full alternative from the beginning, not a late moral correction forced by the story.

Use separate reputations for technical competence, reliability, discretion, public credibility, underground standing, and individual trust. An underground broker can respect your work while distrusting your promises. A lawful client can hire a skilled outsider without knowing their private history. No universal good-or-evil score determines every interaction.

### The criminal career ladder

Begin as an unknown accepting small, bounded jobs. Early work teaches the fictional device, record, payment, and conversation systems. Reliable results unlock repeat brokers and a crew trial. Midgame adds operations with several dependencies, negotiated shares, active NPC targets, and competing jobs. Later play involves running a crew, choosing projects, funding equipment, and deciding which relationships and exposures to carry forward.

Advancement depends on completed commitments and outcomes, not merely money earned. A technically successful job can damage a broker relationship if the agreed scope was changed. A careful refusal can preserve a useful contact. A failed job can still reveal a lead, create a debt, or open a different alliance.

### Fraud as a full operation

A fraud job has an offered objective, synthetic records, participating actors, a claim being represented, a game payment process, a discovery model, and a branching aftermath. The player investigates the supplied material, identifies contradictions under the invented rules, chooses a plan, completes a technical or social challenge, and decides whether to commit the represented false claim. This is a closed game system with no usable real-world payment or identity workflow.

The payoff can be immediate, pending, disputed, reversed under game rules, or withheld by the broker. An account limit, an accomplice’s unfinished task, or a target’s unexpected response can change the plan while it is underway. Expose the relevant clue and allow a response rather than inventing an untelegraphed failure at the end.

Use eight repeatable mission families with authored variations. A false-contract job centers on a disputed completion claim and its promised payout. A counterfeit-record job centers on inconsistencies in a synthetic document set. A marketplace job centers on a misrepresented fictional item, delivery, and a buyer’s response. A misdirected-payment story centers on a represented transfer and who receives the result. A compromised-information deal centers on authenticity, a buyer, and limited access. A remote-device commission centers on what an active simulated PC can actually reveal. A coercion contract centers on a deliberate demand and a target with agency. A crew betrayal job centers on false contribution claims, missing proceeds, or a member’s conflicting commitment.

These are design families, not real criminal procedures. Every template must specify a distinctive player decision, a solvable technical challenge, a human complication, a trace-generating event, and at least one delayed callback. Changing names and payment amounts alone is not a new mission.

### Remote access and social engineering belong to the operation

Some jobs require interpreting another character’s routine, interacting with a simulated device, or asking a person to make a consequential decision. The device can go idle, the owner can return, a message can arrive, and a crew member can finish their part while the player is elsewhere. A mission should react to those states instead of waiting in a frozen window.

Social engineering uses the declared role, supported or unsupported claims, NPC beliefs, and available checks. A person may agree, refuse, verify, delay, or report. The challenge comes from the game situation and the consequences of chosen intents. A generated paragraph does not create permission or guarantee compliance.

### Blackmail and sensitive records

Track the information’s provenance, authenticity, relevant subjects, copies, and what the target has actually received. Private-photo cards, commercial records, and evidence of possible misconduct have different meanings. Sensitive adult material remains permanently obscured. A category label alone establishes neither guilt nor a guaranteed payout.

An explicit coercive action can lead to refusal, limited compliance, a dispute, an intermediary, a report, or a later change of mind. The target continues working, contacting others, and making decisions. A payment can be a successful immediate outcome while still leaving messages, a dispute, or an ongoing relationship behind.

### Criminal markets and consequences that arrive later

The criminal economy connects the offer, operation, buyer, payment, spending, and people affected. A stolen archive is a persistent story object. A successful payout can stabilize rent, fund a desktop upgrade, or secure crew loyalty. Those gains are real even when unresolved exposure remains. Do not make every profitable operation end in immediate reversal or increase living costs automatically to erase success.

### Stolen personal and company records

Record bundles contain synthetic category cards rather than real personal information: customer correspondence, staff schedules, commercial plans, internal allegations, or private adult placeholders. Store the owner, subjects, source versions, completeness, authenticity, recipients, and permitted interpretations. A company document can contain an innocent employee's details; selling it changes that person's exposure without establishing their guilt.

Buyers have specific authored demands, budgets, reputations, and existing commitments. One pays for the agreed archive; another disputes its completeness; a third changes the requested object. Delivery, acceptance, payment, and subsequent disclosure are separate events. A buyer cannot distribute material never received. Copies retain their source relationships, and a later correction reaches only recipients who actually receive it.

The player can complete the original agreement, refuse an expansion, negotiate, retain the unpaid object, or choose another available story branch. These choices change income, contacts, disputes, and future work. Criminal success should unlock increasingly demanding operations rather than merely larger values on the same screen.

### Abstract laundering and uncertain money

Represent laundering as an explicitly illicit, entirely invented EXODISS concealment contract. The player commits a defined parcel of fictional credits to a broker's symbolic board. The puzzle uses colored certificate tiles, capacity slots, and invented compatibility rules; it has no real institutions, transaction routes, financial instruments, or evasion procedure.

The meaningful decisions concern the broker's fee, promised completion window, crew share, available working funds, and trustworthiness. Committing money can leave insufficient liquidity for rent or an urgent part. A broker may complete the agreement, request a renegotiation, fail, or withhold proceeds according to established circumstances. Progress produces intermediate status messages and opportunities to respond rather than an unexplained final roll.

Completion changes the parcel's fictional presentation status and which game contracts accept it. It does not make the original conduct lawful, erase provenance, or lower every investigator's knowledge. Keep actual source, represented source, settlement, usability, ownership, and investigative knowledge distinct. A broker's reassuring message is only a claim until the authoritative transaction occurs. A laundering allegation requires its own supported acts and intent under the fictional rules.

### Fictional ransomware and independent company responses

Use a service-lock puzzle on invented application nodes. A committed criminal action can place specified game services into a locked state and create a demand event. No executable payload, real encryption workflow, delivery technique, or threat script exists. The puzzle concerns competing service dependencies and the consequences of the selected game action.

The affected organization immediately begins its own plan using available staff, money, recovery resources, and known facts. It may restore a recorded recovery point, operate manually, obtain outside help, refuse, negotiate through an intermediary, or make a report. Payment and restoration are separate events. Neither guarantees deletion of stolen copies, silence, or forgiveness.

Departments can disagree. A manager prioritizes reopening; a technician preserves relevant records; employees need shifts and wages; a director fears a public dispute. Their tasks reserve time and resources. Recovery can succeed while investigation continues. A business closure, canceled appointment, or employee message gives the incident a physical presence beyond the laptop.

### Linkage, attribution, and fair fear

Represent careless fictional OPSEC through story-level linkage: the same invented operation stamp appearing in two incidents, a known crew conversation referring to a purchase, or an identified item connecting a pickup with later events. These are game correlations, not a checklist of real evasion practices. A matching stamp can be copied or shared; a legitimate purchase can explain possession.

Linkage suggests that records belong together. Attribution concerns who performed an act. Proof concerns whether each charge meets its defined standard. None automatically implies the next. Investigators must obtain discoverable records, evaluate independence, examine contradictions, and satisfy separate thresholds for interviews, orders, arrests, and charges. The model cannot invent missing links.

Build fear from increasingly specific developments: a contact stops answering, a payment becomes disputed, a shopkeeper mentions an interview, counsel identifies a collected record, or officers arrive under a valid order. Every consequential cue has a cause. Ambient knocks and passing patrols need ordinary explanations and variation; constant fake alerts destroy suspense. An investigator can be persistent and competent while still reaching an unsupported conclusion that review rejects.

### Dependency tree for implementation

| Foundation | Systems that depend on it | New consequences this enables |
| --- | --- | --- |
| IDs and time | Devices, ownership, files, transactions, commitments | The same object and promise survive scenes and saves. |
| Devices and records | Intrusion, data theft, service locks, recovery | Owners notice actual changes and respond to the affected services. |
| Money and contracts | Buyers, crew shares, symbolic laundering | Settlement delays, withheld proceeds, cash shortages, and disputes. |
| Personal knowledge | Victim, employee, broker, and detective plans | Different people act independently on what they actually learned. |
| Traces and access | Collection, interviews, competing hypotheses | A discoverable clue becomes evidence only through a valid acquisition. |
| Evidence and rules | Authorizations, charges, court decisions | A link can prompt inquiry without proving identity or guilt. |
| Outcomes and history | Custody, restitution, release, future offers | Success, unresolved cases, sentences, and renewed careers persist. |

Knowledge filtering also constrains investigation and court. Transactions remain authoritative during restitution; custody never replaces the shared history. Implement and verify these dependencies before adding more criminal mission families.

### Worked branching scenario: The delayed delivery

**Stage 1 — The commission.** A broker offers enough money to clear overdue rent for a restricted archive from a fictional parts distributor. One crew member accepts archive reconstruction; another supplies an onsite observation from an earlier shop pickup. Their access and knowledge are recorded separately. The player accepts the archive job, requests different terms, or declines.

**Stage 2 — Success and escalation.** Completing the taught puzzle delivers the specified archive and can produce a genuine payout. The broker then proposes a service-lock demand. Accepting creates a new participation record; refusing leaves the original successful theft intact. A teammate cannot silently commit the player to the added offense.

**Stage 3 — The company notices.** Staff discover either a document discrepancy or, if committed, a service outage. A technician investigates observable symptoms. Successful recovery restores operations; a missing recovery resource extends disruption. The company may report immediately, report after verification, or remain focused on reopening. Nobody automatically learns the player's identity.

**Stage 4 — Money and disagreement.** The player spends settled funds, holds them, or enters the symbolic concealment contract. A broker delay clashes with an agreed crew payout. The teammate can accept an extension, leave, or disclose records they possess. Their anger alone creates neither evidence of unseen actions nor an arrest order.

**Stage 5 — Leads separate.** A report gives detectives the company record. A cooperating witness may supply the pickup observation. A genuine message may connect a participant to the archive. If sources cannot support identity or intent, the case remains unresolved or closes. Independent corroboration can instead justify further authorized inquiry. Contradictory facts must change that assessment.

**Stage 6 — Different futures.** Outcomes include retained profit with an unresolved case, a crew split without police attribution, repayment alongside continuing investigation, supported charges for the archive alone, or separate supported service-lock charges. Arrest requires its own threshold; conviction requires proven elements. Release, custody, restored housing, and future broker access follow actual outcomes. No hidden countdown guarantees capture simply because the player chose crime.

### A good route inside the same world

White-hat income comes from authorized testing, incident recovery, digital investigation, diagnostics, credible disclosure work, and consultancy. Lawful groups can investigate corruption through sourced findings and permitted records. These jobs can be demanding and profitable; they should not require committing a crime first or grinding trivial repairs indefinitely.

The opening provides enough legitimate work to meet the stated bills. Criminal offers often promise a larger or faster immediate return, with greater payment uncertainty, dependency on unreliable people, and investigative exposure. Lawful growth relies more on credentials, quality, repeat business, and professional relationships. Tune both using complete playthroughs.

Switching routes is possible, but history persists. A criminal contact may object to a new lawful partnership. A professional employer may question a documented finding. The player can refuse future crimes, repay an obligation, change crew, or continue deeper into the underground network. The game presents choices and follows their consequences without deciding the player’s identity for them.


## 8 Conversation understanding and natural language

Do not build this around thousands of trigger words. A keyword list cannot reliably distinguish “he threatened me” from a threat by the player, or a joke from an actual demand. The useful model is contextual intent plus a validated game action.

Use an intent vocabulary such as ask, clarify, agree, refuse, apologize, request payment, report, accuse, disclose, make a promise, and make a coercive demand. Resolve speaker, recipient, referenced incident, ownership, negation, quotation, and current conversation state. Always include an unknown or ambiguous result.

Offline dialogue uses authored choices, synonym support for optional text, and contextual clarification. AI mode can propose an intent and phrase a response. In both modes the same game rules decide whether an action is available and what it changes. Never treat the language model’s self-reported confidence as calibrated probability; measure ambiguity handling on a labeled test set.

### The action contract

Input text becomes a proposed intent associated with allowed entities and source records. The validator checks context and requirements. For ambiguous free text or an irreversible high-impact proposal, show the exact interpreted action before commitment. A clearly labeled authored choice can itself be the commitment; do not add a second confirmation to every file operation, risky line, or contract. Routine questions need no extra confirmation.

A response record includes request ID, conversation ID, proposed intent, allowed subject IDs, references to known facts, proposed dialogue text, and whether clarification is required. It cannot contain an arbitrary money change, new criminal charge, invented mission completion, or private world fact. Unsupported references cause rejection or fallback.

### Voice direction

Use concise everyday speech. A worried neighbor might say, “yeah i clicked it. thought it was from work. can u help?” A technician might say, “The backup is clean. The live copy isn’t.” A detective can be plain and direct without sounding like a tutorial. Formal documents remain readable and more structured than casual chats.

Default chat turns should usually be one to three short sentences, with an Expand option for detail. Give each character a stable vocabulary, punctuation style, emotional baseline, and limits on slang. Occasional typos are personality, not a requirement to make every line difficult to read. Critical objectives, amounts, deadlines, and choices remain unambiguous.

## 9 Police investigations and detective reasoning

Separate four systems: local detection of an incident, investigative interest, evidence supporting a hypothesis, and immediate physical police response. A reported anomaly is not an identified suspect; an identified suspect is not a convicted person. The game needs this separation to make both clever play and fair consequences possible.

Every report starts with a discoverable event. A victim may notice a loss, an employee may report a discrepancy, a monitoring system may record an anomaly, or a witness may describe an interaction. Delay and reporting likelihood depend on relevant circumstances. Police receive what was reported, not the player’s omniscient activity history.

### Case progression

A case can move through reported, triaged, assigned, active, awaiting results, ready for review, charged, closed, or dormant. Closure can mean resolved, unsupported, withdrawn where the fictional rules allow, or redirected. Dormant cases can reopen when genuinely new evidence arrives. Cases have deadlines, personnel, priority, and unresolved leads.

Detectives choose among permitted actions: review a report, interview a reachable witness, request an authorized record, compare existing evidence, consult a specialist, seek an appropriate order, or defer the case. Choice weighs expected information value, cost, urgency, and legal constraints. An investigation advances only when the selected task returns a result.

Detective personalities affect patience, communication, workload management, and preferred investigative approaches. They do not grant access to hidden truth. Every belief carries supporting evidence and uncertainty. At least one plausible innocent explanation should remain represented until contradicted when the evidence allows it.

### Evidence records

An evidence item stores ID, source event, creator or observer, subject links, collection time, collector, authorization basis, integrity, reliability, original or derivative status, custody history, and permitted viewers. A copied rumor and three people repeating it count as one underlying source, not four independent confirmations.

An evidence graph separates facts, claims, and inferences. Witnesses can be mistaken or self-interested. A record can be accurate about an event but ambiguous about identity. A contradiction creates a lead to examine; it does not automatically prove deception.

Use separate hypothesis scores for investigation priority and explicit rule checks for charging or court use. A threshold in an internal score must not silently substitute for all required elements of a charge. Review must consider all eligible supporting and contradicting evidence already available to the decision-maker. Known contradictions cannot be ignored to advance a stronger allegation. Case tools expose enough explanation for designers to diagnose why an investigator acted.

### Physical policing

Officers respond to dispatched information and their own perception. Witness descriptions can be incomplete. A local response uses routes, arrival time, line of sight, reachable doors, and communicated updates. Losing sight does not teleport police to the player, and changing districts does not erase an outstanding case.

Encounters support identification requests, consensual conversation where applicable in the fictional rules, counsel choices, arrest, transport, booking, and release or detention. Stops, searches, arrests, and continued detention require separate rule checks. A legal-order record stores the supporting evidence, scope, permitted actions, and expiry; one approved search cannot unlock unrelated records. Force and pursuit are secondary to the investigative fantasy. A player should be able to experience the full system without a combat build.

Player-facing feedback includes calls, notices, witness contact, known interview requests, counsel summaries, and a journal of confirmed information. Do not reveal secret evidence prematurely. After a resolved case, optional explanations can show the causal chain that produced the result.

## 10 Courts defense and sentencing

The court is a fictional justice system designed for understandable play. Its rules must be stated in-game and remain internally consistent. This is not a simulation of Massachusetts or any real jurisdiction.

Each charge has defined elements, eligible evidence, a required standard under the fictional rules, possible defenses, and a bounded sentencing range. Evaluate charges separately. A player may be cleared of one allegation while another continues. A payment dispute alone is not automatically a criminal offense.

The flow covers notice of allegations, representation, evidence disclosure, pre-hearing decisions, an optional negotiated resolution, hearing or trial, verdict, sentencing, and limited review or appeal. Make the full sequence available through short playable scenes and summaries so it does not become hours of mandatory dialogue.

### The player role

Choose counsel, review a plain-language case file, identify disputed facts, provide existing records, choose whether to speak, authorize a negotiation, and decide which lawful arguments to pursue. Counsel summarizes uncertainty and possible consequences. Guarantee a free core defense route even when the player has no accessible money. Paid specialists can add preparation options or convenience without buying exemption from evidence requirements.

The player can point out mistaken identity, unsupported inference, unreliable testimony, lack of required intent, permission, or an evidence-handling defect when those are supported by game facts. Smooth wording alone does not erase documented events. A prosecutor or judge cannot invent evidence through generated dialogue.

Legal minigames focus on chronology, relevance, contradiction, and the difference between observation and assumption. The interface shows what a selected argument claims and which known records support it. Players can use an assisted mode that explains terms without deciding for them. A failed charge-element check cannot be overridden by randomness, judge personality, reputation, or poor minigame performance. Randomness can affect discovery opportunities and permitted sentencing variation, never manufacture unsupported guilt.

### Results that continue the story

Outcomes include dismissal, acquittal, a reduced charge, fine, restitution, community work, supervision, detention, or a custodial sentence as permitted by the setting. Aggravating and mitigating factors are explicit. Severity, established harm, prior findings, cooperation, and repair efforts can matter under defined rules.

Record publicity, actual conviction, employer policy, and individual belief separately. An accusation may affect relationships even if dismissed, but the official record must state what happened accurately. An appeal requires a valid issue and changes only what the new decision supports.

## 11 Jail prison and life after release

Distinguish temporary detention before a decision from imprisonment after a sentence. Both are playable states with different rights and routines in the fictional setting. Booking records held items and funds; release restores whatever should be returned. Losing or duplicating inventory through a scene transition is a critical bug.

Provide a small set of strong activities: calls and visits, legal preparation, education, assigned work, requests to staff, relationships with residents, institutional disputes, and release planning. Characters have motives, boundaries, and histories. The player should not need repetitive violence to progress.

### Time that respects the player

Offer Play the day, Skip to the next meaningful event, and Complete the remaining quiet period. Before committing, show known obligations and routine forecasts for housing, bills, and contact. Simulate developments during the skip and reveal them afterward only through information the player could receive. Hidden investigations remain hidden. Stop for a player decision that cannot be delegated. Long sentences are narrative spans, not hours of forced waiting.

Choices affect privileges, relationships, education, legal preparation, and later work under stated rules. Good behavior is not a universal magic sentence eraser. Any release condition follows an authored rule and is visible through an authorized source.

### Reentry

Release can involve changed housing, frozen or reduced resources, strained relationships, restricted work, unfinished obligations, and new contacts. Let the player rebuild an underground crew, switch allegiances, seek lawful work, or pursue unfinished disputes. Provide temporary workspace access and at least one viable paid task. A sentence changes opportunities without choosing the character's future morals or creating an unrecoverable loop of poverty and forced crime.

Supervision uses specific conditions, appointments, permitted activities, and review procedures. Record intentional violation, misunderstanding, and an excused conflict differently. A missed check-in should have context and a visible response, not silently reset the entire criminal history.

## 12 People money factions and a living world

Persistent important NPCs have a home or base, work role, schedule, goals, relevant traits, resources, relationships, beliefs, and memories. Background crowds provide atmosphere cheaply; they can become persistent if they meaningfully enter a story. The game must not pretend every passerby has a fully authored life.

Memory records who did what, to whom, when, how the character learned it, and whether it is certain. A person can remember that you broke a promise without knowing the secret reason. Rumors travel through defined social links and can lose accuracy. Public news reaches people differently from a private conversation.

### Relationships and factions

Track trust, respect, obligation, fear, affection where appropriate, and shared history. Personality affects which experiences matter. An NPC can respect skill while refusing another favor. Forgiveness may require restitution or time rather than buying repeated gifts.

Initial groups include two distinct criminal crews, an independent broker network, a mixed-ethics collective, and a credible lawful research and recovery community. Local businesses, advocates, police, and larger organizations act as clients, sources, rivals, or affected parties. Group membership grants scoped access and obligations. Members can disagree and do not automatically share identical knowledge or goals.

Promises become scheduled obligations with an owner, recipient, due window, stakes, and permitted renegotiation. Relevant NPCs remember completion or failure. Give the player a calendar and advance warning, especially when a court date or important client deadline competes with another commitment.

### Economy

The player begins without reliable income. Overdue rent, bills, partial payments, and limited equipment make offers matter immediately. Criminal work often promises a larger or faster immediate return, with payment disputes, accomplice obligations, and investigative exposure. Lawful starter opportunities must make survival achievable, and reliable professional income is built through completed work. Later wealth should genuinely relieve early pressure instead of causing every bill to scale endlessly upward.

Costs include rent, equipment, transport, training where appropriate, business expenses, counsel, fines, and restitution. Difficulty settings can soften recurring bills. Equipment improves capabilities and workflow rather than replacing player skill. No real-money purchases or loot boxes are required by this plan.

All transfers use a transaction ledger. Track pending, settled, reversed, disputed, and restricted funds distinctly. Save/load and repeated callbacks must never double-pay a contract. Persistent transactions are essential because the economy, investigations, and courts can reference the same event.

### Events that remember

An event definition includes requirements, participants, location, state changes, evidence outputs, memory outputs, follow-up hooks, expiry, priority, cooldown, and incompatible events. A world director selects eligible events using current history. It cannot rewrite past facts to make a twist work.

Scale content through reusable structures with meaningful differences. Forty incident families with ten combinations each can yield hundreds of valid situations, but they are not hundreds of unique authored stories. Track authored arcs, templates, variants, and generated lines separately. Expansion toward thousands of variations is a content pipeline goal, not a promise that an AI will generate endless coherent missions.

## 13 Optional AI architecture and cost control

ChatGPT subscription billing and ordinary API billing are separate [R6]. This design does not assume that a Pro subscription supplies an API key, API credits, or automatic access to this conversation’s memory. The game must send the relevant game context itself. No API setup or paid request is needed to read or use this plan.

Use GPT-6 Luna as the provisional primary for short NPC dialogue and intent interpretation, with GPT-6.1 Sol available for separately budgeted difficult scenes after evaluation. Astra is excluded. Luna supports structured output and a 1,050,000-token context window, with standard short-context text rates of 0.10 US dollars per million input tokens and 0.50 per million output tokens [R7]. Sol 6.1 has the same listed context capacity at 2.00 input and 10.00 output [R12]. These are documented capabilities and rates, not measured game quality. Test both against actual character scenes and verify account access before enabling cloud dialogue.

### Durable memory comes from saved events

Give each important NPC a separate persistent memory view over the game event ledger. A remembered event includes its stable ID, time, participants, source, knowledge permissions, commitments, and whether it is observation, testimony, or inference. For example, the repair client can retain that the player fixed their order-folder service and received 120 credits. A shopkeeper knows a purchase they witnessed. A detective learns those facts only after a valid acquisition; the shared model does not share private NPC knowledge.

Retrieve relevant commitments, relationship facts, recent messages, and source-linked events for every dialogue turn. Older important acts remain queryable even when they leave the recent conversation. Save/load preserves them. The model can phrase a memory but cannot manufacture its source event. A large context window does not automatically remember prior requests, and changing models does not repair an absent memory system.

Set routine Luna calls to reasoning effort none explicitly. A rare Sol 6.1 trial uses low reasoning; that model does not accept none. Keep escalation off by default and use a separate allowance. The prototype starts with a 600-token routine output ceiling, an eight-second client timeout, and concise responses. Production values must be measured, especially when reasoning consumes output tokens [R7, R12].

### What the model is allowed to do

The model can phrase dialogue, interpret a proposed conversational intent, summarize supplied history, vary low-stakes ambient messages, and optionally nominate a high-level NPC action from a supplied valid candidate list. The simulation checks every proposal and determines facts, evidence, payments, permissions, guilt, sentences, mission completion, and allowed NPC actions. Structured output helps constrain response shape; it does not establish that the content is true [R8].

For an NPC turn, retrieve a compact character voice, current situation, relationship facts, a handful of relevant memories, allowed knowledge, current goal, and permitted response intents. Do not send the complete world database. Do not send secret evidence the NPC has not learned. An API request cannot inherit knowledge merely because the player previously discussed it with ChatGPT.

### Request lifecycle

The client asks the game dialogue service for a response. A context builder filters records by the character’s knowledge. A developer-operated relay makes an API request. The reply is parsed, schema-checked, checked against allowed IDs and facts, and either accepted as a proposed response or replaced with an authored fallback. The authoritative simulation applies an approved action once and then the UI displays the matching line.

Tag each request with the world revision and conversation state. If the player leaves, loads a save, the NPC is arrested, or the situation changes before the reply arrives, discard or regenerate the stale response. Use request IDs for deduplication. A late response must not revive a dead mission, pay twice, or overwrite newer memories.

Store committed structured memories from actual events. A generated summary may help retrieval, but it is not the source of truth. Consequential statements carry an approved set of claims marked as observation, inference, rumor, uncertainty, or scenario-authorized deception. Evidence consumes those claims, never generated prose reinterpreted as truth. Use authored wording for admissions, waivers, and negotiated agreements. A conversation claiming that a payment happened cannot create a payment event. Test contradiction handling, invented names, unavailable actions, and attempts to make an NPC reveal hidden facts.

### Keys privacy and reliability

Keep the developer API key on a controlled server or local developer relay, never inside a distributed Unity build, saved game, public repository, or laptop texture. OpenAI’s production guidance recommends securing keys outside code [R9]. A later bring-your-own-key mode needs separate storage and threat design; it is not the default release architecture.

Gameplay is offline except optional NPC dialogue: Unity to its loopback relay, relay to OpenAI’s fixed HTTPS endpoint. Block redirects. No Wi-Fi scanning, telemetry, URL opening, real targets, or shell execution. Treat player text, NPC replies, and retrieved facts as untrusted data. Models cannot change files, money, evidence, permissions, or game state; validated game rules retain authority.

Use cancellation, bounded retries with backoff, output limits, caching of appropriate ambient lines, and rate limits per session. Start with a configurable timeout around five seconds before presenting an authored fallback. This is a UX target; actual network latency must be measured. The game remains playable during outages, quota exhaustion, refusal, or malformed output.

### An illustrative text budget

For a short-context example of 3,000 uncached input tokens and 200 total output tokens, Luna costs approximately 0.0004 US dollars per turn, or 0.40 per thousand turns. Sol 6.1 costs approximately 0.008 per turn, or 8.00 per thousand at the same token counts. A 95 percent Luna and 5 percent Sol mix would be 0.78 per thousand under that simplified assumption. Sol reasoning tokens are also output tokens, so 200 visible words or tokens would not establish its total cost. These figures exclude additional reasoning, cache writes, retries, regional premiums, audio, tools, hosting, taxes, and other services; they are arithmetic illustrations rather than a game-session quote [R7, R12].

A prototype relay should enforce its own daily and monthly spending ceiling, reserve estimated cost before requests, reconcile actual usage, and fall back locally when the remaining allowance is too small. Do not rely only on a warning email or a dashboard budget. Measure average calls per player-hour before choosing a commercial funding model.

## 14 Technical architecture and content pipeline

Keep the game state independent of scene objects and generated text. The authoritative single-player simulation lives locally in the game process; the online relay returns language and nonauthoritative action proposals. A multiplayer design would require a separate authority and synchronization plan.

Core modules are WorldState, ClockAndScheduler, CharacterRegistry, KnowledgeAndMemory, InteractionRules, ContractSystem, PuzzleRuntime, TransactionLedger, TraceStore, EvidenceGraph, CaseSystem, CourtSystem, CustodySystem, EventDirector, DialogueGateway, Persistence, and Presentation. The expanded design adds DeviceRegistry, ActivityPlanner, RemoteSessionRules, SensitiveRecordPolicy, HardwareAssembly, ComponentCompatibility, and CrewOperations. Each owns a specific set of records and exposes validated commands.

### The event contract

An event carries a stable event ID, simulation time, actor IDs, subject IDs, location, event type, validated payload, visibility rules, and related mission or case IDs. Commands request an action; events record that it actually happened. Save a snapshot with a bounded event history sufficient for active cases and important memories. Archive summarized older history without dropping referenced evidence.

An authorized repair can create a repair-completed event, a ledger payment, a client memory, and an employer reputation change. A harmful action can create victim effects and potential evidence. The case system sees evidence only when a collection or report event gives it access. The dialogue system reads the resulting state and describes it.

Use deterministic random seeds for meaningful simulation rolls so the same saved state and committed action can be reproduced during debugging. Save the resulting decisions, not just the seed. Avoid tying outcomes to render-frame rate or the arrival order of cloud responses.

### Minimum record types

Person records hold identity, current location or schedule, relationships, beliefs, commitments, and resources. Contract records hold scope, milestones, payment rules, participants, permissions, deadlines, and completion state. Incident records hold cause, affected systems, harm, detection opportunities, and available recovery. TraceStore holds world traces, their locations, retention rules, and discoverability even when nobody has collected them. EvidenceGraph holds acquired exhibits, provenance, reliability, and competing interpretations. Collection links a trace to an exhibit without retroactively changing the trace. Cases link hypotheses, tasks, evidence, charges, and outcomes.

Court records contain the allegations, charge elements, permitted evidence, rulings, verdicts, and sentence. Custody records contain status, held property, conditions, release criteria, and institutional events. Transactions contain balanced entries, settlement state, and the cause event. Dialogue records contain only approved intents and committed lines plus their source revision.

Device records contain ownership, current users, power and connectivity, application state, record versions, scheduled jobs, and active session IDs. Remote-session records contain source event, capabilities, conditions, expiry, owner awareness where known, and removal state. Sensitive records contain category, adult eligibility where relevant, consent, provenance, authenticity, context, permitted views, and copy relationships. NPC plans contain their goal, known facts, candidate actions, reserved resources, next review time, and reason for revision.

Hardware records contain unique part IDs, compatibility tags, dimensions, condition, owner, installed slot, connection state, fastener state, and inventory location. Crew records contain membership, roles, project visibility, accepted commitments, task ownership, source-linked findings, treasury entries, payout agreements, and disputes. A physical part cannot occupy two machines, and membership cannot silently grant access to every shared record.

### Save reliability

Use versioned schemas, atomic save writes, a last-good backup, rotating autosaves, and a migration path. Save on meaningful transitions without freezing the game. Autosave before irreversible story decisions, hearings, and time skips. Preserve multiple manual slots unless the player chooses an ironman mode later.

Loading must cancel outstanding AI work, restore pending deadlines, rebuild derived UI state, and preserve unique IDs. Replaying an event must be idempotent: the same payment, evidence collection, sentence, or released item cannot apply twice. An interrupted save should recover to a known valid snapshot.

### Content authoring

Represent missions and incidents as data plus reusable action handlers. Authors define prerequisites, roles, graph nodes, choices, state effects, evidence, memory changes, fail-forward outcomes, and completion conditions. Tools should flag missing actors, impossible prerequisites, unreachable endings, contradictory state changes, and callbacks that reference deleted events.

A mission preview tool should show one cause-and-effect timeline for each branch. A character inspector should show what that character knows and why. A case inspector should reconstruct the evidence available at each decision. A scenario runner should reproduce a saved seed and report invariant failures. These tools directly reduce the cost of maintaining a large story library.

## 15 Social engineering and human decisions

Social engineering is a major gameplay system for fictional deceptive-role encounters, contested claims, broker negotiations, and consequential requests, not a single persuasion stat. The player can interact with a person whose responsibilities, beliefs, relationships, current work, and permitted actions shape what they will do. Honest persuasion, requests for help, disputed authority, deception, and an explicit coercive action are different game intents. The system supports lawful investigative interviews as well as fictional criminal story branches.

Every encounter has a human objective, a specific requested game action, what the speaker claims, what the listener currently believes, and any independent check the listener can perform. A relationship can create willingness to listen without creating permission. A believable character can be friendly and still refuse access to a restricted record.

### Human judgment instead of a magic success percentage

Give the listener a current goal, a verification habit, an attention budget, institutional rules, past experiences, and boundaries. Track whether the request is relevant, supported, understood, and within their authority. Evaluate these factors against the available actions. A character can ask a question, check an authorized source, seek a supervisor, defer until later, decline, report a concern, or agree to a limited request.

Use distinct response states: engaged, uncertain, checking, interrupted, committed, refusing, or seeking help. These states can change during a conversation when an appointment begins, another person joins, a contradictory fact arrives, or a previously agreed condition is satisfied. The player sees believable reactions and available follow-ups; the developer inspector sees the actual reason.

Successful conversation means achieving an appropriate objective, not always obtaining the requested item. Accepting a refusal, clarifying a misunderstanding, or negotiating a legitimate alternative can produce valuable information and trust. A staff member should sometimes be willing but unable to help. A truthful request can fail because the person is busy; an alternative appointment can then succeed without a new personality roll.

### Playable forms

An interview reconstructs what someone observed and separates certainty from assumption. A service request asks for a defined task within the person’s role. A negotiation changes a contract, deadline, payment plan, or promise. A deceptive-role scenario is an authored identity-claim choice evaluated against the listener’s knowledge and checking options. A coercive scenario uses an explicit, abstract game action and records the resulting harm and evidence.

Do not write these missions as real-world scam scripts or procedural instructions. All identities, services, requests, and resulting computer actions belong to the closed simulation. The challenge is reading the game situation, managing commitments, judging credibility, and accepting uncertainty.

### Teaching and feedback

Add a human-systems course after the first technical lesson. Teach role claims, trust, verification, and ambiguity through a repair request, a contested identity, and an authored deceptive-role exercise. The player can attempt the fictional false claim, answer honestly, or decline the encounter. Later modules combine records with interviews, broker bargaining, and competing crew interests. Every assessed move uses the game's bounded choices, with feedback explaining how the other character interpreted it.

After a resolved encounter, explain the known reason: “They needed the owner’s approval,” “They remembered the missed appointment,” or “The record contradicted your claim.” Do not expose a secret personality score or the precise phrase that would guarantee compliance. Learning mode can review alternative game approaches and their tradeoffs without exposing hidden future decisions.

Free text remains optional. Players can choose an intent and subject, adjust a proposed amount or deadline where relevant, and receive natural dialogue. The action validator owns the commitment. A model can paraphrase a question, but a careless generated sentence cannot silently create an admission, transfer, disclosure, or crime.

## 16 Persistent NPC computers and fictional remote access

Important NPCs own or share simulated devices with persistent files, applications, messages, account roles, current tasks, operating states, and history. A computer is not a static loot container that becomes interesting only when the player opens it. Its owner uses it as part of an ongoing life, and that activity produces game records over time.

The fictional RAT system represents an unauthorized remote-access implant inside this simulation. It uses an invented installation encounter and an explicit scenario result; the game never distributes a functioning RAT, executes malware, or connects to a real person’s computer. The useful gameplay is what the simulated access reveals, how incomplete it is, and how the people and institutions respond.

An installation encounter links a fictional artifact, target device, authored contact channel, declared intent, recipient decision, and toy device-rule check. In a game email scenario, the recipient can ignore the artifact, ask someone about it, report it, or open it according to their knowledge and circumstances. Opening it is not automatically successful access: the scenario’s invented device rules resolve the result. The interface uses story objects and choices, with no operational payload or reusable deceptive message.

### Device owners behave differently

| Owner profile | Typical simulated activity | A plausible change in routine |
| --- | --- | --- |
| Trader | Checks fictional markets, records decisions, discusses work, and takes breaks during scheduled sessions. | An unexplained discrepancy leads to reconciliation, a support request, or a paused session. |
| Shop owner | Updates stock, reviews invoices, prepares payroll, and messages suppliers. | A payment dispute changes cash flow and creates a conversation with an employee. |
| Freelance artist | Edits project files, shares approved drafts, invoices clients, and tracks deadlines. | An unexpected file change creates version checks and a request for technical help. |
| Student | Works on assignments, chats with classmates, plays games, and uses shared devices. | A missed deadline or disputed shared file creates a social and technical investigation. |
| Clinic administrator | Uses a work role for schedules and an ordinary personal device off duty. | A service warning prompts an incident report through the proper workplace channel. |
| Investigator | Writes permitted notes on a work device and maintains a separate personal life. | A new assigned case changes work tasks; a personal PC never automatically contains the case database. |

These are starting profiles rather than stereotypes. Each person also has hobbies, relationships, constraints, exceptions, and a changing workload. A trader can stop trading to visit family. A shop owner can hand a shared workstation to an employee. A person’s occupation does not guarantee wealth, gullibility, secrets, or criminal conduct.

### Activity records and a live view

A task definition names the actor, application, required role, scheduled window, input records, possible outputs, interruptions, and completion rules. The actor selects a valid task from current needs and commitments. Completing the task can create a synthetic document, change a fictional order, send a game message, or update a calendar.

For example, a simulated trader might review a watchlist, update a journal, send a work message, and leave for lunch. A fictional remote view can display those application events if that session has the required game visibility. It cannot see the person’s thoughts or future actions. Other activities on another device remain unavailable unless separately observed through valid game events.

Render this view from semantic application state. There is no reason to run an entire second real desktop or stream actual video for every citizen. Reuse the laptop’s UI components and synthesize the visible app state, cursor movements, and task transitions. Near a physical computer, the same state drives its screen in the world.

### Remote session lifecycle

| Session or device state | What the player can know or do |
| --- | --- |
| No access | See only previously discovered public or legitimately obtained information. |
| Pending scenario result | Wait for the authored access encounter to resolve under game rules. |
| Reachable | Open a permitted fictional session and see its declared capabilities. |
| User active | Observe allowed application activity while the owner continues their current tasks. |
| Idle or asleep | See only what the simulated device exposes in that state; activity may pause. |
| Disconnected or powered off | Receive a connection failure; old snapshots are clearly labeled with their age. |
| Under examination | A technician or investigator can inspect simulated incident records within their authority. |
| Removed or replaced | The old session ends; a replacement device does not inherit the connection automatically. |

Keep implant state, device power, connectivity, owner awareness, and session authorization separate. A reachable device does not mean the player can view every application or file. A lost connection does not prove the owner discovered anything. A suspicious owner may continue ordinary activity while waiting for help.

Distinguish technical capability from the owner’s consent. A game action can be mechanically possible and still unauthorized under the fictional rules. The interface should communicate both, and evidence should record what was actually done rather than assuming that access implied permission.

Permitted remote-session actions are bounded game operations such as view the simulated app, inspect an available synthetic record, note a timestamp, copy a story object, or perform a scenario-authorized repair. Each checks current state and produces an event. No arbitrary command execution, real keystroke collection, camera access, or real credentials are involved.

### Persistence without wasting the player’s time

Important activities occur whether the player is watching or not. Save the current task, task progress, device state, relevant record versions, and queued appointments. A computer cannot stay eternally active because the player keeps its panel open. It can go idle, be shared, close an app, lose connectivity, or return the next game day.

Let players bookmark relevant observations and request a summary of accessible changes. Show the difference between live information and an old snapshot. Avoid rewarding hours of passive watching; surface meaningful developments through the mission’s rules and permit the player to do other things.

At distance, run scheduled semantic events rather than rendering desktops or simulating every keystroke. Promote a device to detailed presentation when viewed; do not regenerate its history. Cached screens and event logs must agree with the owner’s actual timeline.

## 17 Private material and sensitive information

Use abstract content cards for private material. A card can show a strongly blurred synthetic placeholder, a neutral category label, a fictional owner, an acquisition time, and the known context. Create the placeholder independently rather than blurring an actual explicit image; there must be no hidden original to recover or unblur.

All characters associated with intimate material are explicitly adults. The game does not display actual nudity or sexual acts. Labels such as Private photos or Adult intimate material communicate the story without requiring explicit artwork. Offer a setting that replaces intimate categories with a generic Sensitive personal material label while preserving the same mission structure and consequences.

### Category is not proof or a coercion score

| Category | What it establishes by itself |
| --- | --- |
| Private personal photos | A privacy interest; no automatic evidence of wrongdoing. |
| Adult intimate material | Especially personal content; consent, authenticity, and disclosure matter independently. |
| Financial or commercial records | A record to interpret; unusual activity can have an innocent explanation. |
| Alleged illegal activity | A claim requiring context, corroboration, and the fictional offense’s defined elements. |
| Fabricated or misleading material | A potential falsehood or misrepresentation whose origin and use may become a separate incident. |

Track authenticity, subject identity, source, acquisition authority, context completeness, sensitivity, publication status, copies, and who actually knows about the record. A genuine image can be miscaptioned. A real payment can be unrelated to an allegation. A sensitive item is not a universal “more leverage” upgrade.

The player can flag a claim, request consent, preserve a relevant record, protect unrelated information, seek an authorized review, return a story object, or commit an explicit coercive or disclosure action where the authored scenario allows it. Outcomes distinguish the underlying conduct from the player’s handling of the information.

### People respond to the intrusion

The affected person may care about privacy, authenticity, family, employment, resources, or stopping further contact. They may refuse, seek advice, document the incident, contact authorities, speak to someone they trust, or disengage. Their choice follows their knowledge and circumstances. Payment or compliance is never guaranteed by selecting a category.

Do not model another person’s distress as a score to maximize. Represent concrete stakes and decisions: a missed workday, a support conversation, a changed device, a report, an employer inquiry, a damaged relationship, or a recovery step. Some people remain resilient, have strong support, or decide to challenge a false allegation publicly.

Disclosure can harm uninvolved people and can create new witnesses, complaints, and records. Deleting one local copy does not remove copies already created or reports already made. The player can still reduce further harm through legitimate recovery choices, while the history records what occurred.

### Evidence handling

Sensitive records remain sealed behind appropriate in-game roles. A court interface normally summarizes the relevant claim and handling history rather than displaying the private content. A detective cannot gain unrestricted personal access simply because a case exists. Logs record authorized reviews and any scenario-defined mishandling as distinct events.

The system tracks what the material actually supports: identity, timing, possession, an allegation, or nothing relevant to the charge. Possessing a private record is not automatic proof that the person depicted committed an offense. A false allegation and a privacy violation can be investigated independently.

## 18 Independent NPC brains and unfolding reactions

### Every person has a separate persistent brain

Every consequential NPC has a separate persistent person record. Background people use stable compact identities and routine seeds; when they witness an event, transact, or enter a relationship, promote them to a full record without changing what already happened. Each has their own identity, resources, relationships, commitments, current goals, knowledge, memories, emotional context, and unfinished actions. Shared behavior code provides reusable abilities; separate state makes the decisions personal. Two employees can receive the same announcement and make different choices because one has a deadline, one trusts the sender, and one remembers a previous problem. Leaving the street does not erase those differences.

“Their own brain” does not mean a separate large language model running continuously for every person. The local game simulation owns decisions and consequences. A small optional language model can express a character's approved thoughts in dialogue or nominate a permitted next action at a significant turning point. Ordinary work, movement, purchases, reports, interviews, and scheduled follow-ups continue with no cloud service.

### What a person actually knows

Separate world facts from personal knowledge and beliefs. An observation records what this person perceived, when and where it happened, its source, and which details were visible. A remembered statement records who said it; it does not turn the statement into proven truth. Beliefs connect claims to supporting and contradicting observations, confidence, and the last review time. A message spreading through five coworkers remains one originating claim unless someone contributes independent information.

Memories have relevance, importance, and relationships to source events. Routine details can be summarized, but promises, transactions, testimony, injuries, significant relationships, and case-linked events keep their authoritative references. Personal recollection can become uncertain without rewriting the underlying event. An NPC can say “I think that was Tuesday” while the receipt still records the exact date. Important encounters cannot disappear merely because a dialogue summary became shorter.

### Goals turn knowledge into action

Reconsider plans when something meaningful changes: a shift begins, a message arrives, a payment fails, a colleague asks for help, a suspicious discrepancy becomes visible, or an appointment is missed. First identify feasible actions from the person's role and current knowledge. Then weigh urgency, commitments, relationships, money, expected benefit, perceived danger, and personality. Reserve necessary resources and schedule the selected action. Its actual completion creates events that other people may subsequently observe.

A plan has a next step, preconditions, expected duration, interruption policy, and a reason for reconsideration. Waiting for a technician's report is a real pending state. Another incident can interrupt it; an unrelated passerby cannot. Use commitment periods and meaningful changes to prevent people switching goals every few seconds. Failed actions produce understandable alternatives, such as rescheduling when the repair shop is closed.

### Fear changes behavior without granting hidden knowledge

Model perceived safety, immediate concern, uncertainty, and available support as distinct inputs. A threatening encounter can make someone avoid a location, ask a friend to accompany them, interrupt work, seek advice, preserve a message, or report what happened. Fear does not identify an unknown offender or guarantee compliance. A cautious person may respond decisively; a worried person may still finish an urgent obligation before seeking help.

Communicate these changes through behavior and short natural dialogue rather than exposing a distress meter to exploit. Concern can ease after a successful repair or supportive conversation while distrust remains. For the player, tension comes from remembered encounters, delayed developments, and incomplete knowledge: a technician has arrived, a shopkeeper mentions an interview, or an investigator requests a conversation. Every development needs a valid causal path; none implies omniscient police.

### One incident, independent people

A company clerk notices a mismatch while reconciling a fictional order. She knows the mismatch, not its cause, and checks an allowed earlier record. A manager receives her report, prioritizes restoring operations, and books technical help. The affected account holder learns only what the company communicates and separately decides whether to pause work, seek support, or complain. Company procedure coordinates these people without merging their private knowledge.

The technician examines the authorized device and produces findings. A detective receives the complaint and permitted findings later, compares alternative explanations, and schedules a justified follow-up. Another investigator can inherit the case through an explicit handover. Unsupported attribution can remain unresolved or be disproved. The manager's urgency, the victim's fear, and the detective's evidence assessment are independent states that influence different actions.

### Scale and persistence

Nearby people use full perception, navigation, body animation, and immediate reactions. Other loaded people use reduced presentation and scheduled decisions. Distant people advance semantic appointments and task events without walking every meter or rendering their computers. Population records can store compact schedules and exceptions, but witnessed encounters and commitments remain persistent. Changing simulation detail cannot regenerate history or complete an impossible journey instantly.

Save goals, beliefs, source-linked memories, task progress, resource reservations, emotional context, pending events, and committed decisions. Process simultaneous events in a defined order, with reproducible random choices recorded when committed. Ordinary play and time skips obey the same causal rules. Cancel outstanding model requests on load; reject replies from stale revisions. Missing, invalid, or late AI output falls back to the local planner and authored dialogue. No API failure may delete a memory, manufacture evidence, reset fear, or freeze a case.

## 19 Two deeper living world scenarios

### The trader whose computer kept working

Mara is an adult fictional trader with morning market activity, an afternoon reconciliation task, and an evening appointment with a friend. The device simulation creates a watchlist change, a journal entry, and a work chat. These records exist whether the player watches them or not.

The headline mission is an underworld commission involving a fictional remote session. The player can accept its stated objective, refuse it, report relevant information, or separately seek authorized support work. The remote view shows only allowed simulated applications. A private-photo card appears in a discovered story folder; its existence says nothing about trading misconduct. A separate ledger discrepancy may be an innocent adjustment or an authored irregularity that needs corroboration.

Mara continues working until an observable anomaly or a message gives her a reason to react. She might defer the issue until reconciliation, contact technical support immediately, or ask a trusted colleague. Her schedule, available money, and prior experience influence that choice. She does not automatically know who is responsible.

If the player helps legitimately, a technician can remove the fictional implant and preserve a report. If the player chooses coercion or a privacy violation, Mara may refuse, preserve the relevant message, seek support, or make a report. Those actions create their own records. The game does not supply a real threat script or guarantee that private material produces compliance.

A detective later receives only collected sources. The market discrepancy, private material, unauthorized session, and coercive act are separate questions. One can be disproved while another remains supported. Several game days later, Mara may use a replacement device, change her work arrangements, become a referral source after a successful repair, or refuse further contact. The visible result depends on the actual branch.

### The employee who checked the request

Jon manages a shop’s shared workstation and has a limited role. He is waiting for a supplier confirmation and must finish payroll before leaving. The player wants access to a fictional business record for a selected job, which may be criminal, mixed, or legitimate. Jon can discuss public information, request the owner’s approval, or schedule a meeting; he cannot grant permissions he does not have.

The player can make an honest request, supply an existing authorization, ask for an alternate contact, or choose an authored deceptive identity claim. Jon may check the request through an in-game source, notice a conflict with an earlier conversation, be interrupted by work, or defer the matter. Repeating increasingly forceful lines is not a reliable solution.

A verified request can open a scoped record and a new professional relationship. A false claim may be declined and forgotten, retained as a concern, or reported if it connects to a known incident. A mistaken refusal can be corrected when the owner confirms permission. A social failure does not automatically trigger the most severe police response.

Days later, Jon may recognize the player, explain why he was cautious, mention that the shop changed its approval process, or become a witness to the exact interaction he observed. He cannot testify to an unseen remote session on another computer. The episode demonstrates a living person with work, limits, memory, and agency rather than a lock that opens when a persuasion bar fills.


## 20 Hardware crews and life as a hacker

Hacking is the main activity, progression system, and income source. Life outside the laptop should connect to that identity: buying and building equipment, meeting a contact, visiting a client, working with a crew, managing a workspace, or dealing with what a previous operation caused. The world gives the work a physical setting and lasting personal stakes.

Use a relevance test for new features. Does this help the player prepare for work, perform it, understand someone involved, build a professional relationship, spend its rewards, or experience its consequences? If the connection is weak, leave the feature out. Dedicated cooking, sports, musical instruments, creative clubs, gardening, pet care, and ordinary-job career trees are outside this design. Background citizens can still have ordinary lives without requiring the player to participate in separate hobby systems.

### From a starter laptop to a custom desktop

Begin with a worn laptop, little cash, overdue rent, and a clear view of better machines you cannot yet afford. The laptop supports courses, messages, starter jobs, and basic fictional analysis. Earning money opens a meaningful hardware path: buy a used desktop, assemble a new one, improve individual parts, add useful peripherals, and eventually build a specialized workspace. Keep the laptop valuable for portability, meetings, and authorized onsite work.

A desktop is a persistent physical object, not a single upgrade button. Bring the case to the workbench, remove its panel screws, place the panel aside, inspect the interior, remove an installed component, fit a compatible replacement, reconnect required cables, secure the assembly, and test the boot. Hands, tools, parts, and the case remain visible throughout.

CPU, GPU, memory, storage, motherboard, power supply, cooling, and case upgrades have different requirements. A new graphics card may need more clearance or power; a processor may require a compatible motherboard; extra memory may require free slots. Use fictional brands and transparent compatibility data. The store and build planner show known conflicts before purchase, while the physical interaction still has to be completed by the player.

### Component properties and useful tradeoffs

| Component | Fictional build properties | Effect on hacking gameplay |
| --- | --- | --- |
| Case | Board sizes, component clearance, mounts, and access layout. | Determines which build fits and how accessible it is to service. |
| Motherboard | CPU socket, memory generation, slots, and device connections. | Defines compatible upgrades and attached device capacity. |
| CPU | Processing capacity, cooling need, and power demand. | Handles appropriate simulation, automation, and analysis queues. |
| GPU | Size, connection needs, memory, and supported analysis profiles. | Improves specific fictional parallel-analysis workloads. |
| RAM | Capacity, generation, and installed slot arrangement. | Supports more concurrent workspaces and larger active datasets. |
| Storage | Capacity, interface, condition, and stored contents. | Holds case archives, software, backups, and working data. |
| Power supply | Capacity, connectors, and condition. | Determines whether the installed system can run reliably. |
| Cooling | Compatible mounts, sustained capacity, airflow, and noise. | Affects sustained work and the physical workspace’s sound. |

Hardware improves capacity and workflow. It does not automatically solve a reasoning puzzle, persuade a person, or remove legal consequences. A fast fictional GPU does not change the real computer’s frame rate. The game must distinguish the player’s simulated equipment from the actual machine running Unity.

Offer more than a linear best-part ladder. A compact quiet system can suit a small apartment; an expandable case can support later upgrades; a used part can save money but require a condition test. The job planner shows which workload is limiting a contract so the player can make an informed upgrade.

### Physical assembly and disassembly

Create separate interaction targets for panels, fasteners, retention clips, slots, connectors, and removable components. A screwdriver action turns a selected fastener through a short continuous animation. Parts enter a tray or the player’s hands and preserve their identity. A removed GPU remains an actual inventory object that can be tested, stored, sold, or installed elsewhere.

CPU replacement includes removing the obstructing cooling assembly, opening the fictional retention mechanism, exchanging the compatible processor, and refitting the cooling assembly. GPU replacement includes its visible securing point, connection state, and slot. Memory and storage have their own readable seating interactions. Cable routing uses authored paths and attachment points rather than expensive unrestricted cable physics.

Use forgiving alignment and camera control so a minor mouse movement does not destroy an expensive part. An incompatible component refuses to seat with an explanation. Incorrect connections produce a diagnosable state. Meaningful risky choices are explicit; random hidden damage is not the source of difficulty.

Manual work is the default. The first build teaches each important step. Later, an optional powered screwdriver can shorten fastening animations, and accessibility assistance can complete a local group of identical screws. Component selection, removal, placement, connections, and diagnosis remain real actions. Do not turn every screw revolution into a separate click or silently replace the whole build with a menu.

### Boot problems and diagnosis

The machine evaluates its actual installed state. It can fail to power on, report an unsupported part, miss a storage device, start with reduced capability, or become unstable during a sustained test. Causes include a required connection left open, incomplete seating, incompatible parts, an existing defect, missing fictional system software, or inadequate cooling.

The diagnostic loop is inspect, form a hypothesis, run a useful test, change one relevant condition, and verify. Indicators, visible fans, sounds, and a fictional diagnostic screen provide evidence. Once a fault is fixed, the same fault does not randomly reappear. Tests should teach the component relationship while preserving the pleasure of figuring it out.

Finish with a stable boot, recognized components, a bounded workload test, and restoration of the player’s saved workspace. Drive contents and backups matter: replacing storage without transferring files leaves those files on the old drive. Moving a component cannot duplicate it or erase its ownership history.

### Buying parts and building a workspace

Use a hardware shop, repair counter, fictional classifieds, delivery service, and contacts who offer used equipment. Listings show relevant specs, known condition, included accessories, and the seller’s established history. A disputed sale creates a receipt, diagnosis, and possible refund conversation. Sellers remember honest returns and false claims.

Travel has a reason: inspect an expensive used component, collect an urgent replacement, deliver a repaired workstation, or visit a client whose equipment needs onsite access. A physical box has size, handling, and a destination without requiring a full delivery-job simulator.

Spend earned money on a better workbench, organized parts storage, another monitor, a useful test device, backup capacity, a more reliable connection plan, or a larger workspace. Improvements should change a real limitation or interaction. A decorative purchase can express taste, but it does not need its own progression system.

Later, a small office can support shared crew work, client meetings, secure game-record storage, and equipment service. Rent, operating costs, noise, deliveries, and access permissions give the space tradeoffs. A search or seizure affects specific identified equipment under the fictional rules rather than wiping the entire upgrade tree.

### Online hacker crews

The player can join or create a crew through the game’s fictional internet. In the first release, members are persistent NPCs with their own computers, schedules, skills, clients, financial needs, loyalties, and boundaries. The crew feels online inside the game; real-player multiplayer remains a separate future architecture decision.

Give the crew an identity, purpose, charter, membership rules, and a shared workspace. Its tools include group chat, project channels, a sourced case board, task assignments, deadline calendar, equipment requests, and a treasury connected to EXODISS. Role permissions control who can read a project, add findings, invite others, contact a client, spend shared funds, or publish a report.

Members specialize in technical puzzles, recovery, evidence analysis, social investigation, onsite work, or coordination. They contribute actual task results after spending time. A specialist can find a useful contradiction or complete a delegated recovery task; they do not apply a universal success bonus. Their availability and commitments matter.

Recruit through underworld referrals, completed jobs, crew trials, course discussions, and useful contributions. Previous disputes and reliability under pressure affect who trusts the player. Track competence separately from reliability and principles. A talented member can be difficult to work with. An inexperienced but careful recruit can improve through shared assignments and mentoring. The player’s reputation affects who is willing to join.

### Criminal crews and competing principles

A criminal crew can pursue fictional fraud contracts, stolen-data commissions, restricted-access jobs, sabotage, and coercive story projects. Members have preferences about clients, risk, payment reliability, and who gets hurt. A member who tolerates company theft may refuse a personal extortion job. Another may care chiefly about getting their agreed share. These differences create recruitment and leadership choices rather than a single good-to-evil membership score.

A lawful crew can earn money through authorized assessments, incident response, recovery, consultancy, and sourced investigations for clients. An anti-corruption group can investigate a fictional supplier, protect a source, establish facts, and decide how to report findings. Members can demand changes, leave, or report misconduct. Calling a goal anti-corruption does not automatically authorize every method or prove every allegation.

Set the contract scope, responsibilities, expenses, and payout shares before a job. Pay members through real game-ledger transactions. Delays, withheld credit, changed scope, and unpaid expenses affect trust for specific reasons. The player can renegotiate rather than being forced into a surprise betrayal.

Each consequential operation requires a participation decision. A teammate cannot silently make the player agree to a crime. Investigators evaluate each person’s actual acts, knowledge, intent, and available evidence. Shared membership can generate a lead; it does not automatically establish guilt.

Conflict grows from recorded motives and opportunities. A rival offer, financial pressure, a values dispute, or an unfair payout can lead to withdrawal or disclosure. A member can reveal only information they actually received. Clues can be ambiguous, and accusations can be wrong. Loyalty, reconciliation, and successful long-term partnerships need as much support as betrayal.

### An operation that connects the whole game

A broker commissions a fictional company archive theft. At home, inspect the offered target, agree on crew shares, and assign a member to verify the buyer's requested story objects. Your current rig cannot comfortably run the larger puzzle alongside active tasks. Close a task, delegate it, or buy a compatible used component. Visit the dealer, press E to discuss its condition, bring it home, unscrew the case, fit it, and verify a stable boot.

The company workstation remains active. Its user finishes a work task, closes an application, and changes the state of a shared record. The crew must adapt to this taught puzzle condition instead of completing a frozen terminal. A teammate discovers that the buyer now wants unrelated personal material. Accept the changed scope, renegotiate, deliver only the original objects, or withdraw. Each member decides whether to continue participating.

A successful delivery earns a real game-ledger payment, then settlement uncertainty, crew shares, and a decision about spending or retaining disputed proceeds. The broker may be reliable; success need not be a guaranteed double-cross. The company's later discovery depends on what changed and what staff inspect. A complaint can generate a case. The dealer knows only the sale and conversation they witnessed; police must obtain relevant records and build a supported connection rather than knowing the mission script.

This operation ties hacking income, active PCs, physical upgrades, useful shops, crew leadership, disputed money, and later investigation together. The player can finish richer and remain unidentified, face a buyer dispute, repair a damaged crew relationship, or encounter a developing case. A lawful branch can use the same technical system for a scoped recovery contract with different objectives and aftermath.


## 21 Blender production and the physical build plan

Build the environment and character-support assets through a deliberate Blender pipeline. The finished world needs believable architecture, coherent materials, purposeful props, responsive animation, and controlled lighting. The apartment is the first production asset set. Institutional rooms and the full character roster follow the inventory and gates below; the asset delivery status is tracked in the project README.

Prototype geometry is temporary. The apartment and one street must receive an art pass before the project treats them as a quality benchmark. More buildings cannot compensate for an unconvincing room the player visits every session.

### Establish a visual target

Use grounded contemporary architecture and stylized adult characters with believable proportions, restrained materials, and recognizable neighborhood identities. Choose a time of day and weather condition for the benchmark scene, then verify the same space in other lighting. Aim for consistent quality across architecture, props, clothing, and screens. A detailed laptop inside an empty box-shaped room will not satisfy the intended standard.

Create a reference board and room plan before modeling. Define plausible floor height, doorway and stair proportions, furniture sizes, circulation, window placement, structural thickness, service spaces, and visible exterior connections. Use the player’s eye height and collision capsule to test these relationships in-engine. This is fictional architectural plausibility, not a claim of real building-code compliance.

### The starting apartment as a finished example

Model a simple main room with a real entrance and corridor, windows with depth, skirting, sockets, curtains, desk storage, cables, and signs that someone lives there. Kitchen and bathroom expansion follow after the first room works; they do not require domestic chore systems. Select props that describe the character’s finances and habits. Use repetition carefully: a few shared assets are normal, but major silhouettes and focal objects should be distinctive.

The desk is the most scrutinized location. Give the laptop, keyboard, hinge, chair, desk edge, sleeves, hands, and nearby objects enough detail for first-person viewing. Align interaction points and seated posture to the actual dimensions. Opening the laptop must not pass through a monitor, mug, or arm. Keep the screen readable without requiring the player to change a display setting every session.

Give the desktop assembly equal attention. Author separate case panels, visible fasteners, board mounts, card brackets, slots, retention clips, cooling components, and connectors in Blender. Standardize interchangeable part envelopes and hand targets. Test removal clearances and tool poses in first person. The case interior must hold up during a real upgrade, not just look convincing through a closed glass panel.

Create wear through plausible use: chair movement, frequently touched handles, work surfaces, and weather-exposed materials. Avoid applying the same dirt texture everywhere. Use restrained clutter with clear movement paths and readable interactable objects.

### Close view quality applies to every asset

Treat a complaint about one prop as evidence of a general quality gap. Review the whole repeated viewing area: silhouette, construction seams, contact with supporting surfaces, shading, material response, and believable scale. The chair, keyboard, hands, cables, tool tray, and door hardware need the same care as the most attractive room camera. Additional polygons and surface noise are useful only when they improve what the player can actually see.

Approve hero props at a normal seated view, a close interaction view, and a low side view. Rounded manufactured shells need controlled profiles, deliberate joins, and consistent bevels. Keyboard spacing, cable exits, part thickness, fastener placement, and contact shadows must remain plausible together. A grainy wide render cannot establish close-view quality; provide a clean detail render and inspect the exported mesh as well.

### Character movement and first person alignment

The current character priority is a body that holds up while moving. Review the complete shoulder, upper arm, elbow, wrist, and hand as one chain. Place the shoulder joint inside a believable shoulder mass; allow the clavicle and upper arm to move without pulling the sleeve into the chest. Keep a modest, pose-appropriate elbow bend and room beside the ribs. Adjust the pose, joint placement, clothing shape, or weights where needed. Moving only the wrist cannot repair a collapsed shoulder.

Sleeves and trousers need continuous deforming surfaces across elbows and knees, with weights that preserve joint volume. Hands need separate wrist motion, finger joints, and usable grip shapes. Rigid equipment can follow attachment bones, but clothing must not open into disconnected pieces when a limb bends. Inspect every authored frame, transitions, and extreme intended poses. Record chest, vest, belt, and hand intersections rather than accepting a clean neutral roster as proof.

The player camera shows arms, legs, and feet by default. Hide only unwanted renderers; disabling torso or head bone objects can also disable their child limbs. Retain a coherent full skeleton, consistent limb lengths, and optional body shadows. Standing, turning, crouching, and sitting change articulated pose rather than vertically shrinking the person. The camera follows a stable eye-height contract while the visible limbs stay attached to the same body origin.

At the desk, feet and knees must clear the chair and desktop. Elbows bend toward the keyboard; wrists meet the work surface without floating or passing through it. Standing restores the previous grounded position. Inspect steep downward views and transitions from both moving and stationary states. Renderer hiding is not an acceptable substitute for correcting detached arms or incorrect leg placement.

Police animation starts as a controlled art and integration test. Give the patrol character a fictional, nonfunctional prop with authored grip anchors, supporting-hand placement, wrist alignment, and finger poses. Review relaxed and raised holds from the front, side, and above. Fingers wrap the intended grip surfaces with visible contact; thumbs, knuckles, and fingertips must not twist upward or pass through the prop. Wrists continue the forearm naturally through the entire transition. The prop follows its attachment consistently, without drifting or jumping between independent tracks. This prototype does not imply a completed weapon or combat system.

SWAT clothing needs a coherent silhouette and construction: helmet with a fitted suspension or liner, protective vest with shaped panels and closures, gloves, knee protection, boots, and an organized belt. Keep details readable at the player's viewing distance. Holsters and pouches attach to defined mounts and clear the hip and forearm during motion. A held prop leaves its matching holster empty; a holstered prop appears only once. Additional gear must not conceal a broken shoulder or create new clipping.

The SWAT door-kick study needs readable anticipation, a continuous swing, a timed contact pose, and balanced recovery. Keep the supporting foot planted during the intended contact interval, preserve joint bend direction, and avoid sudden pelvis or prop jumps. Use an authored contact marker to coordinate a demonstration door. Gameplay separately decides whether a door is allowed to move; an animation marker cannot grant entry, create a police warrant, or bypass an investigation rule.

| Animation proof | Review evidence | Completion gate |
| --- | --- | --- |
| Player standing and walking | First-person and external alignment views | Arms and legs share the body origin; shoulder movement stays coherent; soles meet the floor. |
| Player crouch and desk pose | Transition sequence and close hand view | Articulated body; elbows, knees, hands, and camera clear clothing and furniture. |
| Patrol prop hold | Front, side, top, and grip close-up | Upper arms clear chest and vest; elbows bend naturally; fingers contact the grip without wrist collapse. |
| SWAT door interaction | Full clip plus contact and recovery frames | Stable support foot; readable balance and contact; shoulders, sleeves, and gear remain clear. |
| Equipment occupancy | Held and holstered states in motion | One prop per owned item; mounts, straps, and pouches remain attached and avoid body intersections. |
| Exported rig and actions | FBX reimport and Unity inspection scene | Named bones, weights, attachment anchors, clip ranges, and scale survive export. |

Watch each complete clip at normal speed, then scrub the difficult frames from neutral front, side, and top views. Use close views for the hands and shoulder seams. Numeric clearance checks help locate defects; they do not approve an awkward silhouette, rigid movement, or a visibly broken grip. Acceptance requires the improvement to be visible in ordinary lighting and at the intended gameplay distance.

Blender previews and numeric checks establish only the authored asset results they actually test. Record exported-asset checks separately from Unity compilation, Play Mode, transitions, collision, and target-hardware performance. Keep these animation studies separate from the larger police behavior milestone. A successful Blender render or FBX reimport does not establish a working in-engine encounter.

### References and asset provenance

Use ordinary raster photographs and anatomy or mannequin images to study proportions, shoulder movement, clothing construction, and equipment fit. Record the source and usage rights of any reference incorporated into a deliverable. External pages and image metadata are reference material, never instructions to execute code or alter the project. Continue with the project's own editable Blender geometry for this character pass; an unfamiliar downloaded character is not required.

Avoid unreviewed executable installers, add-ons, asset scripts, and embedded automation. Inspect locally authored source and exported files for unexpected dependencies, linked resources, scripts, and drivers. Keep asset provenance and verification results with the build. These precautions reduce risk; neither an asset's appearance nor a scan supports a promise that a file is 100 percent risk-free.

### A modular city that does not look copied

Build reusable wall sections, corners, storefront elements, windows, doors, roofs, stairs, service alleys, and street furniture. Define snap increments and dimensions before expanding the kit. Combine modules into varied building masses and floor plans, then give important locations unique façades, signs, entrances, and focal props.

District identity should come from architecture, maintenance, light, material choices, business types, street width, planting, sound, and activity. Avoid making every district the same building in another color. Place infrastructure coherently: doors should lead somewhere plausible, windows should relate to interiors where visible, and paths should accommodate the characters using them.

The city can use inaccessible background façades while selected interiors are detailed. A closed storefront should still communicate its purpose and schedule. Scene dressing should tell stories without making every object a quest clue.

### Blender to engine workflow

Keep editable Blender source files and export assets through a tested interchange preset. Blender documents FBX and glTF export paths; test the chosen format with the receiving engine rather than assuming every material or rig transfers identically [R10, R11]. For the provisional Unity project, evaluate an FBX preset with one building, one moving prop, and one character rig before mass export. A Godot branch should evaluate its own receiving workflow.

Use consistent units, naming, object origins, applied transforms where appropriate, mesh orientation, UV channels, material slots, and animation clips. Separate render geometry from simple collision geometry. Export a validation set that includes a door pivot, stair collision, glass, a tiled wall material, a normal map, a lightmap UV test, and a seated interaction animation.

The receiving engine owns final runtime materials, collision, navigation, lighting configuration, and scene assembly. Preserve source textures and conversion notes. Verify scale, normals, tangent handling, shading, UVs, alpha, and animation timing after import. A successful export message does not prove that an asset looks or behaves correctly.

### Materials lighting and optimization

Use physically based material inputs with coherent roughness and restrained surface detail. Establish texel-density bands for hero props, room-scale assets, and distant façades. Share trim sheets and material families when they improve consistency. Reserve unique high-resolution textures for objects the player actually sees close up.

Bevel visible hard edges enough to catch light where appropriate. Maintain clean shading and avoid accidental smoothing across structural corners. Provide suitable UVs for the chosen lighting workflow and inspect baked results for seams, leaks, stretched lightmaps, and inconsistent brightness.

Prepare lower-detail versions for distant assets and simplified collision for movement. Profile material count, draw calls, transparent layers, shadow casters, and texture memory in the representative scene. Set numerical asset budgets after profiling the real machine; do not invent a universal triangle limit that guarantees 60 FPS.

Lighting should guide the eye and support mood while preserving navigation and readable faces. Use practical sources in the apartment, controlled exterior light, and deliberate day-night variants. Audio should support the space through footsteps, neighboring rooms, traffic, weather, appliance hum, chair movement, and laptop handling. Repeated sounds need variation and sensible distance behavior.

### Art approval gates

Review a room-scale blockout for navigation, believable dimensions, camera clearance, and layout before detailed modeling. Review the finished apartment in first person at standing and seated height, including close inspection of the desk and low angles. Review the street from the player’s actual routes, not only from a flattering editor camera.

Check for stretched textures, visible seams, floating props, thin walls, intersecting objects, repeated façades, missing collision, foot sliding, hand misalignment, light leaks, illegible screens, and unmotivated clutter. Compare important locations for consistent quality. A high-quality screenshot is insufficient if normal movement reveals unfinished backsides or broken interactions.

The development deliverables for each approved asset set are editable Blender sources, exported runtime assets, source textures where applicable, collision and detail-level information, documented import settings, and an in-engine validation scene. Track supplied files and completed checks in the build manifest. A listed requirement remains pending until its evidence exists.

The apartment and street pass only when visual review and measured performance both succeed. Maintain the same standard as the city expands instead of replacing one polished demonstration with a much larger unfinished environment.


### Asset inventory and room construction order

Build the starter room first, then the shop encounter, then the rooms needed to carry one criminal operation through investigation, court, and custody. The first room is the current production target; its implementation status is recorded alongside the delivered assets. Courtroom, interrogation room, jail cell, police, and SWAT are explicitly planned production sets. The initial player is short of money and equipment; the room should communicate that through restrained wear and a few useful belongings, not unrelated hobby systems.

Use a coherent mid-poly, stylized treatment: believable proportions, readable faces, simplified surface detail, and carefully shaped silhouettes. Avoid photoreal skin, exaggerated cartoon heads, and buying unrelated character packs with visibly different styles. This direction replaces photorealism as the character target while retaining plausible architecture and satisfying first-person object detail.

### Shared dimensions, camera, and asset rules

All dimensions below are proposed interior game dimensions in meters, not real building-code specifications. Document room coordinates with Blender X increasing east, Y north, and Z upward, with the room centered at the origin. Exported engine coordinates use Y upward and the tested FBX conversion; named anchors remain authoritative. Blender sources use meters and the established export conversion. Keep object origins at meaningful pivots; apply the tested export preset instead of repairing inconsistent scale inside individual scenes.

Use the chosen character's eye anchor for standing view; approximately 1.6 to 1.7 is an initial adult scale range, not a fixed camera offset. Start with a seated view near 1.18 and a movement capsule approximately 0.56 wide and 1.75 high, then tune these against the rig and furniture. Keep primary paths at least 0.9 wide, with 1.1 preferred around frequently used furniture. Test crouching and downward viewing before adding clutter. The camera must not pass through the torso, chair back, desk edge, or laptop screen.

Use simple colliders: boxes for walls and furniture, a capsule for the player, and a moving box for a hinged door. Small desk props usually need interaction bounds rather than obstructive physics. Interaction anchors describe where hands, eyes, tools, and the seated body belong. Store anchors under the asset so replacing a mesh does not break the interaction.

### Starter room APT-01: a simple corner layout

The first room measures 6.4 east-west by 5.2 north-south, with a 2.8 ceiling. Blender bounds are X -3.2 to 3.2 and Y -2.6 to 2.6. Put the entrance near the southeast corner, centered at X 2.1 in the south wall. Its hinge is near X 1.6. Keep the inward swing and corridor connection clear.

Place the bed in the northwest corner, centered approximately at X -1.85, Y 1.28. Place the desk against the north wall in the northeast corner, centered approximately at X 1.83, Y 1.87. The chair sits south of the desk, around X 1.68, Y 0.91. The laptop faces the seated player. A window in the west wall gives the room depth and nighttime light. Keep the center open for a direct route from entrance to bed and workstation.

Use authoritative named anchors PlayerSpawn, DeskSeat, LaptopFocus, DoorHinge, DoorInteract, BedInteract, and BillInteract. PlayerSpawn and DeskSeat are floor-level body positions; LaptopFocus is on the visible screen. DoorLeaf is a child of the true hinge pivot. Kitchen and bathroom are later expansions, not misleading unfinished doorways.

| Asset ID | Concrete asset and starting size | Required setup |
| --- | --- | --- |
| APT-A01 | Floor, ceiling, four walls; 6.4 × 5.2 × 2.8 interior | Separate doorway/window openings; simple collision. |
| APT-A02 | Door leaf 0.96 × 2.10; frame and handle | Hinge pivot, handle anchor, open/closed state, obstruction check. |
| APT-A03 | Window frame, glass, sill; 1.3 × 1.1 opening | Interior depth, exterior backing, controlled reflections. |
| APT-A04 | Bed; approximately 1.6 × 2.2 × 0.55 | Frame, mattress, pillow, rumpled blanket; sit/rest anchor. |
| APT-A05 | Desk; 2.14 × 0.83 × 0.81 | Top, legs, one drawer; knee clearance and drawer limit. |
| APT-A06 | Chair; approximately 0.6 × 0.6 footprint | Seat at 0.46; seated root, stand destination, hand supports. |
| APT-A07 | Laptop; approximately 0.44 × 0.30 | Separate lid, hinge, keyboard, screen, power state. |
| APT-A08 | Laptop charger and authored cable | Connector anchors; no loose cable simulation required. |
| APT-A09 | Small desk lamp; 0.35 tall | Switch prompt and restrained local light. |
| APT-A10 | Parts shelf; 0.55 × 1.2 × 1.5 | Three shelves, box placement slots, stable collision. |
| APT-A11 | Two cardboard parts boxes | Open/closed variants; item ownership and inspect bounds. |
| APT-A12 | Rent notice and envelope | Readable close-up document; persistent payment status. |
| APT-A13 | Intercom and wall switch | E interaction, speaker position, call/listen animation. |
| APT-A14 | Skirting, socket plates, curtain, wastebasket | Reusable trim and restrained dressing; no chore mechanics. |

APT-A07 uses LaptopFocus now and later adds LeftHandRest, RightHandInput, and LidGrip. The chair uses DeskSeat and two validated standing destinations. Approaching, sitting, using the laptop, exiting screen focus, and standing are one tested interaction chain. A blocked standing destination selects the other safe destination rather than placing the character inside furniture.

### Small shop set SHP-01

Build one 5-by-4 room with a south entrance, a counter along the north side, two display shelves, and a pickup shelf behind the clerk. Keep a 1.2-wide customer route. Inventory: SHP-A01 room shell, SHP-A02 counter, SHP-A03 two shelf modules, SHP-A04 packaged GPU, SHP-A05 boxed workstation, SHP-A06 receipt prop, and SHP-A07 clerk terminal. Reuse the apartment door and suitable trim.

Place anchors for customer conversation, clerk standing, item inspection, package handoff, and receipt delivery. The clerk can see only the space their perception permits. The package, receipt, transaction, and conversation create the physical traces needed for the first criminal pickup mission.

### Interrogation set INT-01

Use a 3.6-by-4.0 room with a 2.7 ceiling. Put a 0.95-wide door in the south wall near the east corner. Center a 1.6-by-0.8 table, with the player south and detective north. Keep the exit path clear around its east side. Add a wall observation pane and simple institutional lighting.

Inventory: INT-A01 shell, INT-A02 table, INT-A03 two chairs, INT-A04 observation pane, INT-A05 wall clock, INT-A06 case folder, INT-A07 recording indicator, and INT-A08 water cup. The indicator reflects the fictional interview state; it does not record real user audio. Anchors cover both seats, document presentation, eye targets, door waiting, and a lawyer's optional third chair. Keep both faces visible at the seated camera height.

### Courtroom set CRT-01

Use a 9-by-7 room with a 3.2 ceiling and a 1.6-wide south entrance. A central aisle connects the doorway to the hearing area. Place the judge's bench across the north side, approximately 5.0 wide and 0.9 deep, on a modest 0.25 platform. Defense and prosecution tables sit left and right of the aisle, facing north. Put the witness position northwest and clerk position northeast. Two short audience rows flank the rear aisle.

Inventory: CRT-A01 shell, CRT-A02 judge bench/platform, CRT-A03 judge chair, CRT-A04 two counsel tables, CRT-A05 four counsel chairs, CRT-A06 witness stand/chair, CRT-A07 clerk desk/chair, CRT-A08 four audience benches, CRT-A09 case display, and CRT-A10 evidence folder set. Reuse plain doors and fixtures. Supply judge, clerk, witness, lawyer, defendant, officer, and audience anchors. The player must see the judge and the currently relevant evidence without a camera clipping through another character.

### Jail cell and custody corridor JAL-01

Use a 3.0-by-3.4 cell with a 2.7 ceiling and a 1.5-wide corridor outside. The south wall contains a 0.95-wide barred sliding doorway. Place a 0.85-by-2.0 bunk along the west wall, a compact toilet/sink unit in the northeast corner, and a shallow writing shelf on the east wall. Keep the center open for movement and conversation.

Inventory: JAL-A01 cell/corridor shell, JAL-A02 barred door and track, JAL-A03 bunk/mattress, JAL-A04 toilet/sink prop, JAL-A05 writing shelf, JAL-A06 stool, JAL-A07 overhead fixture, JAL-A08 property bag, and JAL-A09 meal-tray prop. Plumbing and eating remain background functions. Required interactions are sleep/time advance, writing/calls through scheduled scenes, officer conversation, property transfer, and entering/leaving custody. Door collision, permissions, and animation must agree.

### Modular character roster

Create one compatible adult skeleton and three proportion variants, with compatible clothing cuts rather than stretching one outfit arbitrarily. Use six reusable expression poses, eye targets, blinking, and simple jaw movement. Start around 20,000–35,000 triangles for a complete close-view character, including clothing, as a profiling proposal; produce reduced-detail variants for distance. Final budgets follow the representative scene test.

| Character ID | Role and visual identity | First required behavior |
| --- | --- | --- |
| CHR-P01 | Player: inexpensive trousers, hoodie, shoes | Walk, crouch, look down, sit, type, handle objects. |
| CHR-C01 | Jon, shop clerk: work shirt and apron | Greet, inspect order, hand over package, recall visit. |
| CHR-V01 | Mara, adult victim: distinct face and everyday clothing | Use own PC, answer message, notice anomaly, seek help. |
| CHR-B01 | Broker/crew contact: contrasting silhouette | Meet, negotiate, react to payout dispute. |
| CHR-D01 | Detective: plain clothes and case folder | Sit, present evidence, take notes, leave. |
| CHR-O01 | Patrol officer: recognizable uniform | Approach, speak, escort, property handoff, and authored two-hand prop holds. |
| CHR-O02 | Second patrol variant | Support an encounter without duplicating the same face. |
| CHR-J01 | Judge: robe over compatible base clothing | Seated hearing, gaze shifts, announce outcome. |
| CHR-L01 | Defense lawyer | Consult, indicate document, address judge. |
| CHR-K01 | Court clerk/custody staff variants | Record outcome, guide movement, return property. |
| CHR-S01 | SWAT outfit set: helmet, protective vest, equipment mounts | Door-contact animation study; later authorized high-risk response only. |

SWAT uses the shared animation foundation but receives its own silhouette, equipment clearance, and doorway tests. It is a later escalation asset, not the default response to the first disputed payment. Prioritize believable posture, hands, gaze, and short grounded reactions over elaborate tactical animation sets.

### Production sequence and completion gates

Pass 1 builds APT-A01–A07 in simple geometry, the player body, and the complete walk–sit–laptop–stand loop. Pass 2 finishes the starter room's materials, lighting, APT-A08–A14, close-view meshes, and sound. Pass 3 adds SHP-01, Jon, Mara, receipts, and the first pickup consequence. Pass 4 builds INT-01 and the detective. Pass 5 builds CRT-01 and the hearing cast. Pass 6 builds JAL-01 and the release transition. Police variants and SWAT expand only after the ordinary consequence chain works.

The next production queue begins with the current body correction. Finish the shoulder, elbow, wrist, grip, and equipment acceptance sequence above, then bring those accepted assets into the Unity apartment slice. Verify movement, downward views, crouching, sitting, laptop input, and standing in Play Mode, followed by the first paid repair and persistent payment. Resolve importer and interaction defects before expanding the location count. Isolated prop polish continues only where the playable review exposes a concrete problem.

Next, build the shop encounter around a useful purchase or pickup: approach Jon, press E to talk, inspect without purchasing, confirm a transaction, and receive an owned item and receipt. Connect one later fictional misuse to a victim's discovery, a report, and an investigator's supported inference. Then construct the interrogation room, courtroom, and custody space around that same case, including an inconclusive or lawful outcome. This proves one connected consequence chain before more criminal mission families, districts, and high-risk police responses are added.

Keep three states visible in production records: planned content, supplied source or assets, and verified runtime behavior. The current editable apartment, character studies, and Unity source establish a starting build. They do not establish finished character art, a complete city, or working court and police systems. Unity Play Mode and GTX 1080 Ti performance require their own recorded checks. The 320-feature catalog remains the full backlog; progress is measured by completed player actions and persistent consequences.

Every finished asset includes editable Blender source, export, materials, collision, interaction anchors, necessary animation clips, and an in-engine inspection scene. A room passes when all required paths work, doors operate correctly, hands reach their targets, seated views remain clear, objects persist through saving, and normal camera angles reveal no missing backsides or broken shading. Judge success by the playable sequence and measured performance, not a single attractive screenshot.

## 22 Criminal mission chains and white hat alternatives

### Pending balance

A broker offers enough money to make a serious dent in the overdue rent. The contract asks the player to knowingly submit a false claim inside the game’s invented settlement workspace. Compare supplied story records, resolve a bounded consistency puzzle, and choose whether to commit. The result may be rejected, accepted pending review, or paid according to established scenario state.

Successful payment is real progress: reduce arrears, reserve a component, or fund the next job. Later, a business reconciliation can reveal a discrepancy. An employee asks a question before police know anything. The broker may dispute the agreed cut, offer another job, or distance themselves. Repayment, a dispute, continued participation, and seeking counsel create different continuations; none retroactively deletes the original act.

### The pickup favor

A crew contact promises a fee for collecting a boxed workstation tied to an already established fraudulent order. The player knows the proposed role before accepting. Visit the pickup counter, press E to speak, inspect the order object, and decide whether to complete the collection when the clerk mentions a discrepancy.

The package, receipt, payment promise, employee observation, and order history remain distinct records. The contact can pay, delay, or demand an additional favor. A later customer dispute can produce a report and a question about who collected the machine. Being seen at the counter does not establish knowledge of every earlier act, but the player’s actual messages and choices may establish more.

### The six slot window

A crew accepts an illicit commission involving a specific restricted story archive on a live simulated device. The technical board uses six capacity slots across observation, reconstruction, and transfer of designated game objects. The owner continues their routine, a crew member has a limited task window, and a scheduled shutdown can interrupt the operation.

The player can close other tasks, delegate a supported stage, or install a compatible hardware upgrade before beginning. During play, one route prioritizes completing the original objective; another yields a partial result while keeping a commitment to a teammate. If the client changes the objective, accept, renegotiate, or stop. The puzzle preview explains dependencies and known constraints. The target’s later response begins only when an observable event gives them a reason to react.

### Private leverage

An underground contact commissions a coercive demand involving a confidential story record. Its authenticity and context are not fully settled. The player can investigate first, decline, disclose through an appropriate route, or commit a clearly labeled coercive action. The target may refuse, seek advice, comply within the scenario, report, or change their mind later.

The contact then offers an altered copy to someone else. The player’s original communication and the contact’s later fabrication are separate incidents. A case can support one allegation and reject another. Keep private adult imagery permanently obscured; the gameplay is the information, commitments, money, and responses, not explicit content or composing a real threat.

### The buyer changed the deal

A restricted-data commission pays on delivery of a specific synthetic archive. The player completes an access-and-reconstruction puzzle and discovers that the buyer now wants an unrelated record. The original job, the new demand, and any prior promise to a crew member conflict.

Deliver only the agreed object, renegotiate, accept the expanded job, sell through an available story branch, or walk away with an unpaid claim. The buyer can accept, complain to a broker, withhold funds, or become a rival based on their established resources and goals. Each recipient receives only the record actually shared. A source later contacting support can open an investigative thread without automatically revealing every participant.

### The missing cut

A successful criminal operation pays the crew, but a member claims that the player kept more than agreed. Review the treasury, task board, messages, and itemized expenses. The discrepancy can be an honest timing issue, a member’s lie, or an opportunity the player deliberately exploited.

Negotiate repayment, challenge a false accusation, admit the diversion, side with a faction, or leave. A member can disclose information they possess, seek outside help, or accept a new agreement. The resolution changes future recruitment, shared hardware access, work allocation, and loyalties. The crew’s own dispute can create an investigative lead, but a group name never substitutes for proof of each member’s acts.

### White hat alternative I clicked something

A delivery worker followed a link in the fictional email app and lost access to work. Reconstruct a timeline, identify affected services, preserve useful records, and restore the account through the game’s recovery puzzle. The worker needs a shift back, making the choice between fast limited recovery and thorough diagnosis meaningful.

Respect the agreed scope, explain uncertainty, and get paid for actual work. The same incident can later connect to a broker or crew the player knows, giving a lawful hacker a reason to navigate the underground world. The worker may recommend the player, request further help, or become a witness to events they genuinely observed.

### White hat alternative The replacement contract

A crew is paid to investigate repeated failures after a supplier upgrade. Visit the building, interview its caretaker, inspect the installed equipment, and compare it with supplied records. Back home, combine findings with a teammate’s work and restore service through a dependency puzzle.

One discrepancy is a mistake; another may support a corruption allegation if corroborated. Decide what the report establishes, protect unrelated private details, and handle disagreement over publication. The job earns money, credibility, and future work while interacting with the same suppliers, devices, witnesses, and institutions as the criminal campaign.


## 23 Content scale and production gates

The complete vision is a backlog, not a promise to implement everything simultaneously. The catalog uses S for the first end-to-end slice, C for expansion of the core game, and L for later ambition. These tags indicate priority, not an estimate of equal effort. A visible player body can take much more work than one additional page in the browser.

### Gate 0 The technical proof

Prove body visibility, camera comfort, sitting at the desk, a readable laptop, a street traversal, save/load, and one simple puzzle. Profile on the actual computer. Compare engines only if there is a concrete performance or workflow reason. Exit when the experience is stable enough to build on and the engine choice has evidence.

### Gate 1 The complete consequence slice

Build one apartment, one short street, a repair shop, and compact reusable police, court, and custody spaces. Use about eight recurring story NPCs plus background population for testing. Include the rent-pressure opening, a paid beginner repair followed by an unlocked criminal contract, a viable lawful continuation, one profitable outcome, one independent victim response, one evidence-driven case, one hearing, and a short custodial or release path.

The entire sequence should work with no API. It must save and load at each important stage. Play both a lawful success and a criminal consequence path, then prove that returning home preserves the outcomes. This slice demonstrates every major promise at small scale before expansion.

The expanded slice also needs one social request with acceptance, verification, and refusal outcomes; one active NPC computer; one bounded fictional remote session; and one permanently obscured private-record card. Reuse the same NPC and case where possible. Demonstrate that the NPC continues a task, closes an app, and seeks help without the player ordering each step. Include one physical desktop upgrade and boot diagnosis, one basic NPC crew collaboration, and an authorized onsite visit; finish the apartment through the Blender art gate. A lawful hacking route must be playable alongside the crime-and-consequence demonstration. The full catalog spans 32 categories; the slice implements only the minimum proofs specified here.

### Gate 2 The first useful neighborhood

Target 15 to 20 authored contracts, 12 course exercises, four connected story arcs, 20 to 30 important NPCs, and six useful interiors in addition to required institutional spaces. These are proposed production targets, adjustable after throughput is known. Establish repeatable event authoring and two or three puzzle families with real depth.

Use two criminal arcs, one mixed or crew-conflict arc, and one complete lawful hacker-group arc. One criminal arc must connect stolen story records, a real payout, an abstract laundering contract, crew shares, and discoverable traces. The other must prove a service-lock incident with independent company recovery and branching attribution. A sourced anti-corruption investigation can belong to the lawful or mixed route. Expand the basic desktop proof into a small compatibility-driven component catalog and support persistent crew roles and payment agreements. These take priority over unrelated lifestyle mechanics.

A polished Gate 2 neighborhood, retaining the complete investigation-to-reentry chain from Gate 1, is a possible complete first release. Gate 3 expands that functioning game. This release boundary keeps the full ambition available without making three districts a prerequisite for finishing anything.

### Gate 3 The systemic core

Expand toward three districts, around 20 useful interiors, 60 substantial contracts or authored episodes, 12 longer arcs, and 40 incident families. Add broader careers, factions, legal variation, relationships, reentry, and the economy. Each expansion must pass regression scenarios from the original slice.

### Gate 4 Optional AI and content growth

Run AI against a labeled set of game conversations before enabling it by default. Measure hidden-fact leakage, invented actions, intent errors, fallback rate, cost per player-hour, and latency. Grow variants through validated templates and editorial review. Add voice only after text works and costs are understood.

### Gate 5 Polish and release readiness

Complete animation passes, sound, visual consistency, accessibility, onboarding, stability, save compatibility, and performance profiling. Replace placeholder assets deliberately. Test long campaigns, repeated loading, time skips, and complicated overlapping cases. Do not set a public release date until production velocity and remaining risk are measured.

### How to estimate the work

The needed disciplines include gameplay and tools programming, technical animation, environment art, UI design, narrative and systems design, audio, and QA. One person can cover several roles, but the work still exists. AI assistance can accelerate drafts and implementation; it does not verify the game by itself.

Estimate after the slice by recording hours for a representative mission, interior, character, puzzle, and case. Multiply by the remaining content, add integration and revision work, then reserve contingency for uncertain systems. Track a lean playable release separately from the full ambition. No honest budget or completion date can be fixed from a GPU model and this feature list alone.

### What to postpone first if scope expands

Postpone multiplayer, a huge drivable map, every-building interiors, procedural voice for everyone, deep speculative markets, elaborate combat, and unlimited AI-authored storylines. Unrelated hobby systems and non-hacking career ladders are excluded. Preserve hacking as the primary income and progression, the visible body, home and laptop, recurring people, white and black hat choice, and a working investigation-to-reentry chain. Those are the game’s identity.

## 24 Acceptance criteria and risks

Quality should be verified through complete scenarios. A feature is finished when it behaves correctly with the other systems, communicates clearly, survives saving, and meets its performance budget. A button or attractive screen alone does not complete a system.

Body and interaction: look down, crouch, turn, walk stairs, sit, use the laptop, and stand without camera clipping, duplicate arms, or lost input focus. E targets the visible reachable person or object; chat typing never activates room interactions. Inspection cannot purchase an item. A deliberate sale changes money, stock, ownership, and receipt exactly once. Essential actions remain usable with reduced motion and enlarged text.

Learning and opening: a new player completes a paid repair on a persistent NPC computer, can explain the relevant rule, and knows where to get a hint. The first thirty minutes produce a useful skill, payment, an equipment goal, and a future underground offer. The rent clock pauses during teaching. The lawful route can meet the stated deadline. A later criminal job can pay successfully without mandatory arrest; unlearned implants and extortion are not tutorial actions.

Intent handling: quoted threats, reports of harm, negation, slang, and typos never commit a high-impact action automatically. Unknown meaning asks for clarification. An explicit confirmation is bound to the exact subject and action shown.

Investigation: detectives act only on accessible evidence. Repeating the same rumor through several witnesses does not multiply independent proof. An unsupported case can close. A legitimate new lead can reopen it without changing past facts.

Court and custody: each charge has an explainable result. Acquittal, conviction, release, held property, money restrictions, and sentence completion persist across saves. A player can continue after release and has at least one viable recovery path.

AI reliability: unplugging the network, exhausting the allowance, malformed JSON, a refusal, a late response, or an invented event does not corrupt the save or block progression. The generated response cannot reveal facts outside the supplied knowledge set or directly grant rewards.

Active computers: physical and remote screens show the same authoritative device state. Shutdown, app closure, revoked consent, removal of a fictional implant, and device replacement change access immediately according to the game rules. Simultaneous owner and remote actions resolve once in a deterministic order. Returning to an unloaded location cannot regenerate different historical records.

Hardware and crews: save and reload halfway through disassembly without losing a component, screw group, or cable state. Compatibility feedback matches physical fit, and a failed boot has a repeatable cause. Stored files remain attached to the correct drive. Shared crew tasks, access roles, agreed payouts, refusals, and individual participation survive loading and time skips. A teammate cannot spend or disclose outside their granted scope without a distinct scenario-authorized event.

Autonomy and sensitive material: an NPC can complete a work task, verify a request, seek help, and change a plan while the player is elsewhere. No action requires private global knowledge. Intimate placeholders have verified adult fictional subjects and contain no recoverable explicit media. Category labels never establish guilt or guarantee compliance; cloud dialogue cannot expand them into explicit descriptions.

Criminal economy: stolen copies retain source relationships; a buyer can only distribute objects received. Service locks affect defined game services while recovery and investigation proceed independently. A symbolic laundering contract applies its stated fee, delay, ownership, and settlement once, preserves actual provenance, and cannot erase collected evidence. A player can remain unidentified when attribution is unsupported.

Economy and persistence: one hundred repetitions of a scripted save/load and callback sequence never duplicate payments, items, evidence, or sentences. A forced interruption during a write recovers to a valid save. Version migration retains active cases and obligations.

Performance: the representative build meets the agreed target on the actual PC, including the busiest intended scene and laptop use. Measure loading separately from steady-state play. A two-hour session should show no unbounded growth in active NPCs, UI objects, memory records, or pending requests.

### The largest production risks

Scope growth can prevent a finished game; the remedy is a complete narrow slice and explicit phase tags. Repetitive puzzles can undermine the fantasy; the remedy is different goals, social stakes, and earned automation. Omniscient police can feel unfair; the remedy is provenance and bounded knowledge. AI inconsistency can break trust; the remedy is code-owned facts and tested fallback dialogue.

Save corruption is especially damaging in a game built on history; persistence must exist before large content production. The art workload can dwarf programming; modular environments and a consistent visual style need an early plan. Unknown CPU and storage performance remain a hardware risk until the representative build is tested.

## 25 Prioritized feature catalog

The following 320 entries are the implementation backlog: 40 slice priorities, 217 core expansion priorities, and 63 later priorities. S marks the first playable consequence slice, C marks core expansion, and L marks later ambition. Counts express breadth of design, not completed work. Every phase inherits the correctness and accessibility foundations described above; a later feature tag does not postpone a rule required by an earlier system. The detailed chapters define how the systems connect and what successful implementation means.

{{FEATURE_CATALOG}}

## 26 Decisions to confirm during prototyping

Confirm the actual CPU model, installed RAM, SSD or hard drive, and Windows version before setting minimum requirements. Confirm whether development is solo, with a team, or primarily assisted by coding tools. These answers affect production pace and performance work, but they do not block the design in this document.

The visual direction is plausible contemporary interiors with consistent stylized characters and detailed first-person props. Refine it after the room and street test. Confirm whether EXODISS means the wallet, the browser brand, or both. Confirm the desired level of explicit content and whether the game is for private use or commercial distribution before final writing, asset acquisition, and ratings planning.

The next concrete build is Gate 0, followed immediately by the complete consequence slice. It should be possible to walk around your home, look down at your body, sit at the laptop, choose the first job, receive a meaningful payoff, visit a useful shop, and see the world react before producing a large city.

## 27 Research notes and sources

External technical facts were checked on 4 October 2026. Engine performance, mission counts, timing, feature priorities, legal rules, and architecture choices are proposed design decisions unless explicitly identified as source facts. Sources describe available capabilities; they do not validate an unbuilt game’s performance. Model pricing, product availability, licensing, and compatible package versions must be checked again when implementation begins.

[R1] Unity documentation. Using the Universal Render Pipeline. Supports the scalable rendering description. The documentation branch is a source, not the chosen project version.
https://docs.unity.com/en-us/engine/6000.5/manual/render-pipelines/universal-render-pipeline

[R2] Godot documentation. Overview of renderers. Supports the differences among Forward+, Mobile, and Compatibility.
https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html

[R3] Godot. License. Supports the MIT licensing statement and required notices.
https://godotengine.org/license/

[R4] NVIDIA. Introducing the GeForce GTX 1080 Ti. Supports the 11 GB graphics memory specification.
https://www.nvidia.com/en-us/geforce/news/nvidia-geforce-gtx-1080-ti/

[R5] Unity documentation. Unity Profiler. Supports profiling applications on target hardware.
https://docs.unity.com/en-us/engine/6000.0/manual/analysis/profiler

[R6] OpenAI Help Center. Managing billing for ChatGPT and the API platform. Supports separate billing for ordinary API use.
https://help.openai.com/en/articles/9039756-managing-billing-for-chatgpt-and-the-api-platform

[R7] OpenAI API documentation. GPT 6 Luna model. Supports the listed capabilities, context, reasoning options, and token rates. Cost examples are arithmetic, not measured usage.
https://developers.openai.com/api/docs/models/gpt-6-luna

[R8] OpenAI API documentation. Structured model outputs. Supports schema-constrained responses; the game-specific truth checks are proposed architecture.
https://developers.openai.com/api/docs/guides/structured-outputs

[R9] OpenAI API documentation. Production best practices. Supports keeping API keys out of code and securing credentials.
https://developers.openai.com/api/docs/guides/production-best-practices

[R10] Blender manual. glTF 2.0 export. Supports the available export workflow. This versioned manual is a source reference, not the chosen Blender release.
https://docs.blender.org/manual/en/5.3/addons/scene_gltf2.html

[R11] Blender manual. FBX export. Supports the available export workflow and its conversion options. Validate the importer with the actual selected engine version.
https://docs.blender.org/manual/en/4.4/addons/import_export/scene_fbx.html

[R12] OpenAI API documentation. GPT 6.1 Sol model. Supports the escalation candidate capabilities, reasoning options, and token rates.
https://developers.openai.com/api/docs/models/gpt-6.1-sol
