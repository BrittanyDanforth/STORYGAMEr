# SIGNALDEBT full-backlog implementation and acceptance status

All **320 master-plan requirements** remain in scope, including the 63 later priorities. The [machine-readable ledger](../Validation/backlog-status.json) contains each catalog ID exactly once, with its original text, category, priority, current evidence, remaining scenario and next dependencies. This tracking document is **not completion of the backlog**. No entry is marked accepted.

Reviewed 5 October 2026 against [the feature catalog](Source/feature_catalog.md) and [master-plan sections 3, 21, 23 and 24](Source/master_content.md). The catalog has 40 slice, 217 core and 63 later entries; those counts describe priority, not progress or equal effort.

The supplied game is an apartment/first-repair slice with Blender assets, saved economy rules, a main menu, compact laptop apps and optional direct AI settings. Physical computer transitions and the shared laptop desktop, F002 and F041, have **bounded native Unity runtime evidence**. Current Esc opens Pause; the desktop's Stand up action animates back to first person. Local campaign saves, desktop preferences and an explicitly remembered plaintext key use separate paths under beside-game `UserData`. Full feature acceptance still requires the remaining scenarios in the ledger. A source package called a master build must not be described as the entire completed game.

Unity **6000.0.62f1 produced current `npc-key-r2` Windows and Linux Mono builds with exit code 0**, using the same unchanged 165-file production manifest. [The NPC/key update](NPC_Dialogue_Update.md) records the current source and the corrected remembered-key regression. Earlier `ui-menu-r2` builds used 161 files and remain historical. [The menu evidence](../Validation/UIMenuUpdate/README.md) separates frozen r1's **20 broad native checks, 30 captures and four clean exits** from final r2's **13 targeted checks, 23 captures and two clean exits**. R1 covers the complete compact repair/payment/rent/restart route, New Game archive/cancel and malformed/future-save preservation. R2 covers confirmed seating interruption/reentry, no-key Send without save changes, repeated Pause/Settings/Resume, current confirmation text, legacy AppData import with byte/time preservation, and History/receipt inspection. The full r1 matrix was not repeated on r2; two ambiguous early r2 captures are explicitly excluded as proof of their intended scenarios.

The user answered **“Launches and works”** when asked whether [the earlier Windows PC-r2 delivery launches, permits laptop use and returns to first person](../Validation/PCMilestone/windows-r2-user-report.json). That report does not validate the newer menu Windows player and supplies no Windows captures, logs or hardware measurements. Graphical Editor Play Mode remains license-blocked; the direct checks used standalone Linux players. Current Windows runtime, audio, measured target-PC performance, direct lifecycle-race injection and full campaign/art acceptance remain outstanding.

## How to read the ledger

- **Planned:** no direct gameplay implementation identified; a contextual prop does not change this status.
- **Partial source:** a bounded portion exists; the missing acceptance scenario identifies the next concrete work.
- **Source in progress:** implementation is being changed and has not passed integrated acceptance.
- **Portable validated portion:** only the cited domain or relay behavior has executed non-Unity evidence.
- **Asset validated portion:** only the cited Blender/interchange geometry or selected views have recorded checks.
- **Runtime validated portion:** actual native-game evidence covers only the listed portion; full acceptance remains open.
- **User-reported runtime:** the user confirms the stated behavior for a specific delivered build; the scope stays separate from directly observed runs and measured performance.
- **Unity contract validated portion:** actual Unity executed synthetic provider or isolated key-storage checks; this does not establish live HTTP, authentication or native gameplay acceptance.
- **Runtime pending:** no accepted game-runtime evidence is attached for that feature.

Every feature also inherits scenario integration, save/load/recovery, offline/accessibility, runtime/art and target-performance criteria from the JSON. Dependency references describe shared integration contracts, not a promise that a referenced feature is done or a strict acyclic implementation order. For example, the first repair and the first persistent client device should be built together at their smallest useful scale.

The [148-assertion domain report](../Validation/CloudSetup/domain-tests.json) is historical evidence for the original schema-one repair/economy source associated with the earlier PC work. Its hashes do not identify the current shop/save source. The [391 portable assertions](../Validation/ShopSaveFoundation/domain-tests.json) cover repair/economy regression, one used-GPU purchase, receipt integrity, stale/duplicate rejection, migration and saving before state/events are published. The [97 assertions across 19 genuine Unity JSON/filesystem cases](../Validation/ShopSaveFoundation/save-store-checks-result.json) prove bounded serialization, migration, byte-preserved legacy files, recovery and failed-publication behavior. Those tested units still match their recorded source hashes. [Six historical shop/save native runs](../Validation/ShopSaveFoundation/native/acceptance.json) also passed migration/recovery/repair/rent/restart checks. They precede the menu revision and do not establish a playable shop.

Final r2 direct-provider [73 assertions](../Validation/NPCDialogueUpdate/npc-key-r2-provider.json) and [55 profile/history assertions](../Validation/NPCDialogueUpdate/npc-key-r2-conversation.json) executed in genuine Unity with synthetic data and no provider calls. The current copy-shop profile supplies temperament, ordinary interests and recent visible history; its 12-line context is session-only. Final instructions allow personality-dependent trust, suspicion and misunderstanding without changing verified world facts. Unused Jon/Mara profiles add no actors or relationships. The [nine-case semantic rubric](../Validation/NPCDialogueUpdate/npc-social-evaluation-cases.json) is **NOT RUN**. The user reports receiving generic replies in the earlier delivery. A controlled live evaluation remains pending; this does not establish new-profile quality.

A later [ui-menu-r2 native Remember test failed](../Validation/NPCDialogueUpdate/HistoricalRememberFailure/acceptance-r2-settings.json) inside the old DataContract serializer despite the earlier 13 Editor/173 portable passes. The replacement pure-C# codec passes [220 portable assertions](../Validation/NPCDialogueUpdate/key-store-portable-result.json) and [13 Unity Editor assertions](../Validation/NPCDialogueUpdate/npc-key-r1-key-store.json). Its [detailed npc-key-r1 native regression](../Validation/NPCDialogueUpdate/NativeKeyR1/acceptance-npc-key-r1.json) passed 11 checks, eight captures and three exits: explicit Remember with 0600 permissions, restart with blank input/saved status, removal without creating a campaign, one fabricated invalid-key authentication result, and a bounded paid-Inbox/no-key/Stand route. The key store is unchanged in final r2, whose [separate native smoke](../Validation/NPCDialogueUpdate/NativeFinalR2/acceptance-npc-key-r2.json) passed five checks, four captures and one clean exit for menu/paid Inbox/no-key/Stand behavior. No real key or successful generated reply was tested in these agent runs. Provider choice, usage estimates, spending controls and measured semantic quality remain unfinished.

The existing [27 relay tests](../Validation/CloudSetup/relay-tests.json) cover the older fake-provider/loopback implementation, not the new direct provider's live transport. [Apartment checks](../Validation/apartment_quality_validation.json) and [recorded visual review](../Validation/apartment_quality_review.json) cover Blender/interchange work. Historical pose/asset reports apply to their recorded sources; changed meshes/controllers need current checks. The compact repair now presents stage guidance and wrong-choice feedback; the old three-tier hint controls are no longer present, so F062 remains planned.

## Ordered milestones

1. **Complete remaining PC and apartment technical gates (Gate 0).** Preserve the tested seat/live-screen/desktop/Stand up route and current Pause/Settings behavior. Review blocked stand positions, repeated early/late cancellations, downward body and hand/clothing clipping, camera comfort and complete app/file state. Add the initial street traversal, validate the current Windows player and profile the actual hardware.
2. **Prove the paid repair and viable next work.** Move the repair from a status fixture onto an authoritative owner-operated device. Teach inspection, connection and verification; commit the fee once; expose a real referral and lawful work sufficient to meet the known debt. Keep invitation, interest and criminal acceptance separate actions.
3. **Build one useful shop transaction.** The bounded data foundation now validates one 90-credit used GPU, a read-only quote, stock/item/receipt identity and saving before ownership or money changes are published. Add the street, shop shell, Jon, visible inspection, deliberate confirmation and package/receipt handoff around that contract. The physical shop remains pending; the resulting transaction can later supply a discoverable trace.
4. **Complete one consequence chain (Gate 1).** Add one deliberate fictional misuse, independent victim discovery/replanning, a report, legitimately acquired evidence and competing/unsupported hypotheses. Build interrogation, hearing and custody/release spaces around the same case. Play lawful, inconclusive and criminal branches offline; return home with outcomes intact.
5. **Complete the expanded slice proofs.** Integrate request acceptance/verification/refusal, an autonomous NPC computer, bounded live remote support, a permanently obscured adult record, physical memory replacement with failed-boot diagnosis and one scoped crew collaboration. Do not replace these with a prop, text label or automatic payout.
6. **Deepen the useful neighborhood (Gate 2).** Produce the proposed 15–20 contracts, 12 exercises, four arcs, 20–30 important NPCs and six useful interiors plus institutional spaces through validated authoring. Preserve the entire consequence chain. Add record theft/settlement/shares, reversible service locks with independent recovery, a lawful arc, hardware compatibility and persistent crew agreements.
7. **Finish the systemic core and later requirements (Gate 3).** Extend proven systems toward the proposed three districts, about 20 useful interiors, 60 episodes, 12 arcs and 40 incident families. Schedule every remaining core and later catalog entry in reviewed increments. The possible smaller first-release boundary in the plan does not remove the user’s full-backlog requirement.
8. **Evaluate optional AI and release quality (Gates 4–5).** Run labeled language/knowledge/cost/latency evaluations and retain the offline campaign. Finish animation, sound, accessibility, localization/extension work where specified, migration and long-session regressions. Record Windows gameplay and real-PC performance before claiming readiness.

## Coverage and immediate dependencies

Each category has ten original requirements; ranges below identify coverage without duplicating the full catalog. Source evidence is limited to the portions described in the ledger.

| IDs | Category | Next dependency |
| --- | --- | --- |
| F001–F010 | First person body and controls | presentation; persistence |
| F011–F020 | Threatened home and hacker workspace | persistence; clock; ownership |
| F021–F030 | Useful city shops and travel | presentation; clock; ownership |
| F031–F040 | Citizens and living population | knowledge; planner; clock |
| F041–F050 | Laptop operating system and browser | presentation; devices; persistence |
| F051–F060 | EXODISS fictional wallet and finance interface | ownership; records; contracts |
| F061–F070 | Courses and guided skill development | contracts; content |
| F071–F080 | Hacking puzzles and mission mechanics | contracts; devices; content |
| F081–F090 | Viable white hat alternatives | contracts; ownership; social |
| F091–F100 | Underworld fraud blackmail and criminal progression | contracts; records; knowledge |
| F101–F110 | Conversation intent and contextual interpretation | social; knowledge; persistence |
| F111–F120 | Relationships, factions, and reputation | social; knowledge; clock |
| F121–F130 | Police response and public enforcement | cases; knowledge; planner |
| F131–F140 | Evidence and detective investigations | records; knowledge; cases |
| F141–F150 | Courts and legal outcomes | cases; clock; social |
| F151–F160 | Jail, sentences, and reentry | cases; clock; ownership |
| F161–F170 | Payouts bills suppliers and equipment finance | ownership; contracts; clock |
| F171–F180 | Story director and consequence driven events | contracts; clock; knowledge |
| F181–F190 | Offline and optional online AI | ai; knowledge; persistence |
| F191–F200 | Production, performance, accessibility, and saves | persistence; content; performance |
| F201–F210 | Independent citizen computer activity | devices; planner; clock |
| F211–F220 | Persistent devices and digital spaces | devices; records; persistence |
| F221–F230 | Fictional remote sessions | devices; contracts; knowledge |
| F231–F240 | Social roles and conversation scenarios | social; knowledge; ownership |
| F241–F250 | Distrust and human verification | social; knowledge; planner |
| F251–F260 | Sensitive adult record cards | records; content; social |
| F261–F270 | Authenticity and information custody | records; knowledge; persistence |
| F271–F280 | Autonomous aftermath and action planning | planner; knowledge; clock |
| F281–F290 | Hands on PC assembly and upgrades | hardware; ownership; devices |
| F291–F300 | Underworld hacker crews and investigative alternatives | crew; contracts; knowledge |
| F301–F310 | Blender architecture and lived in interiors | presentation; content; performance |
| F311–F320 | Presentation and art validation | presentation; content; performance |

## Small, independently implementable steps after the PC proof

- **Versioned data and transactions:** extend the tested single-item identity/receipt and v1-to-v2 migration foundation into explicit contract dependencies and later records. Preserve save-before-publication behavior and legacy files. General custody, settlement and a broader inventory model remain unimplemented.
- **Persistent device commands:** one client computer, app state, document revisions, owner actions, consent and deterministic ordering. Validate offline command sequences before connecting the shared physical/remote view.
- **Clock, perception and NPC plans:** one scheduled worker and one victim; record what each observes, reserve finite time, revise a plan on a new fact and process a time skip once. The relay memory store is not an autonomous NPC brain.
- **Shop art and physical interaction:** build SHP-01 and Jon against measured anchors, then inspect, confirm and receive an item/receipt. Keep the art pass bounded to the route used by that transaction.
- **Trace and evidence foundations:** source-family identity, discovery/access, custody and claim uncertainty. Test dependent rumors, missing attribution and contradictory evidence before adding detective choices or courtroom outcomes.

These work areas can be developed separately once their interfaces are agreed, then integrated one milestone at a time. Larger mission families, city expansion, crews and legal outcomes depend on these foundations; adding menus for them does not discharge the requirements.

## Acceptance still required

- **Runtime:** retain the distinct earlier PC, historical shop/save, broad menu-r1 and targeted menu-r2 scopes. The complete menu-r1 transaction/archive/failure matrix has not been rerun on later players. Corrected npc-key-r1 separately passed the remembered-key regression with fabricated input; final npc-key-r2 differs only in Provider instructions and separately passed a five-check native smoke. Its unchanged campaign bytes do not establish a Send-only mtime invariant because the baseline precedes Continue. Direct component lifecycle/callback-race injection and controlled live semantic evaluation remain untested. The physical shop, current Windows execution and full art/gameplay acceptance stay open; the user's Windows PC-r2 report covers only that earlier delivery.
- **Art:** normal standing, seated, downward and close interaction views; hands, clothing, furniture, lid/screen, doors and reachable backfaces. Selected Blender renders cannot close an in-engine room gate.
- **Hardware:** confirm CPU, RAM, storage and drivers on the reported GTX 1080 Ti system. Profile the packaged 1920×1080 ten-minute representative route over three warm passes and measure first load separately. Proposed targets remain mean frame time ≤16.7 ms, 1% low ≥45 FPS and no recurring traversal stall >100 ms.
- **Campaign correctness:** save before/during/after key actions; repeat load/callback scenarios 100 times without duplicate money, items, evidence or sentences; test interrupted writes, migrations, lawful survival, unsupported-case closure and recovery after release.
- **Release quality:** essential accessibility controls, complete offline paths, overlapping cases and a two-hour session without unbounded growth. A full source archive, a build-success log or this ledger alone cannot establish these outcomes.

Update a feature only when its cited code/assets and actual result support the new status. Attach the specific scenario, build/version, test or capture, remaining limitations and target-machine measurement where relevant. Keep the original feature text and priority intact; remove a pending acceptance item only after its evidence exists.
