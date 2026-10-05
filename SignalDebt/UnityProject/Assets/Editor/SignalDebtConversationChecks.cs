#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace SignalDebt.Editor
{
    /// <summary>Synthetic wired-context regression checks. No campaign initialization,
    /// credential lookup, API calls, live model replies, or behavioral quality claims.</summary>
    public static class SignalDebtConversationChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int assertions, groups;
            public string unity_version, profiles_source_sha256, ui_source_sha256;
            public string provider_source_sha256, checks_source_sha256, failure;
            public string save_source_sha256, repair_source_sha256, device_source_sha256, shop_source_sha256, client_source_sha256;
            public bool actual_unity_editor = true;
            public bool campaign_loaded = false, credential_lookup_performed = false;
            public int provider_network_calls = 0;
            public bool behavioral_ai_quality_tested = false;
            public string scope = "Synthetic profile and visible-conversation dataflow through the production UI capture and provider payload builder in Unity Editor. No scene initialization, campaign persistence, credentials, HTTP, live AI behavior, or standalone-player acceptance.";
            public string[] passed_assertions;
        }

        [Serializable] private sealed class Payload { public string input; }
        [Serializable] private sealed class InputFact
        {
            public string fact_id, description, source;
            public int world_revision;
        }
        [Serializable] private sealed class Input
        {
            public string player_message;
            public SignalDebtNpcClient.NpcContext context;
            public SignalDebtAiProvider.ChatLine[] history;
            public InputFact[] facts;
        }

        private static int assertions, groups;
        private static readonly List<string> passedAssertions = new List<string>();
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Signal Debt/Validation/Run Conversation Boundary Checks")]
        public static void RunMenu()
        {
            Report report = Execute();
            if (!report.passed) throw new InvalidOperationException(report.failure);
        }

        // -executeMethod SignalDebt.Editor.SignalDebtConversationChecks.RunBatch
        // -signalDebtConversationReport /absolute/report.json
        public static void RunBatch()
        {
            Report report = Execute();
            EditorApplication.Exit(report.passed && report.assertions > 0 ? 0 : 1);
        }

        private static Report Execute()
        {
            assertions = groups = 0;
            passedAssertions.Clear();
            var report = new Report { unity_version = Application.unityVersion, failure = "" };
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Run conversation fixtures outside Play Mode.");
                report.profiles_source_sha256 = SourceHash("Scripts/SignalDebtNpcCharacter.cs");
                report.ui_source_sha256 = SourceHash("Scripts/SignalDebtUI.cs");
                report.provider_source_sha256 = SourceHash("Scripts/SignalDebtAiProvider.cs");
                report.checks_source_sha256 = SourceHash("Editor/SignalDebtConversationChecks.cs");
                report.save_source_sha256 = SourceHash("Scripts/SignalDebtSaveData.cs");
                report.repair_source_sha256 = SourceHash("Scripts/SignalDebtClientRepair.cs");
                report.device_source_sha256 = SourceHash("Scripts/SignalDebtClientDevice.cs");
                report.shop_source_sha256 = SourceHash("Scripts/SignalDebtShop.cs");
                report.client_source_sha256 = SourceHash("Scripts/SignalDebtNpcClient.cs");
                ProfilePayloads();
                ProfileKnowledgeBoundary();
                OwnerObservationKnowledge();
                RepeatedCheckPayload();
                ProofKnowledgeBoundary();
                using (var fixture = new ConversationFixture())
                {
                    VisibleHistory(fixture);
                    HistoryBounds(fixture);
                }
                Check(groups == 7, "all seven conversation-boundary groups completed");
                report.passed = true;
            }
            catch (Exception error)
            {
                // Fixtures contain only authored context and synthetic text.
                report.failure = error.ToString();
            }
            report.assertions = assertions;
            report.groups = groups;
            report.passed_assertions = passedAssertions.ToArray();
            try
            {
                string path = ReportPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true) + "\n");
                Debug.Log((report.passed ? "PASS" : "FAIL") + ": " + assertions
                    + " synthetic conversation-boundary assertions. Report: " + path);
            }
            catch (Exception) { report.passed = false; report.failure += " Conversation report could not be written."; }
            if (!report.passed) Debug.LogError(report.failure);
            return report;
        }

        private static void ProfilePayloads()
        {
            groups++;
            var state = new SignalDebtSaveData();
            string before = JsonUtility.ToJson(state);
            var profiles = new[]
            {
                SignalDebtNpcCharacter.CopyShop(state), SignalDebtNpcCharacter.CopyShop(null),
                SignalDebtNpcCharacter.Jon(), SignalDebtNpcCharacter.Mara()
            };
            for (int index = 0; index < profiles.Length; index++)
            {
                Input input = Build(profiles[index], new SignalDebtAiProvider.ChatLine[0], "Synthetic greeting.");
                Check(JsonUtility.ToJson(input.context) == JsonUtility.ToJson(profiles[index]),
                    "profile " + index + " fits production payload limits and survives JSON encoding");
            }
            Check(JsonUtility.ToJson(state) == before, "profile construction does not mutate the campaign fixture");
            string original = JsonUtility.ToJson(profiles[0]);
            profiles[0].goals[0] = "Synthetic caller mutation.";
            Check(JsonUtility.ToJson(SignalDebtNpcCharacter.CopyShop(state)) == original,
                "copy-shop profiles do not share mutable caller-owned goals");
        }

        private static void ProfileKnowledgeBoundary()
        {
            groups++;
            var state = new SignalDebtSaveData();
            var stages = new List<SignalDebtNpcClient.NpcContext>();
            stages.Add(SignalDebtNpcCharacter.CopyShop(state));
            state.AcceptRepair();
            stages.Add(SignalDebtNpcCharacter.CopyShop(state));
            string accepted = JsonUtility.ToJson(stages[1]);

            // These are real in-memory domain actions, not writes or direct public-state overrides.
            state.ApplyRentPayment(10);
            state.ExtendRentDeadline();
            state.AdvanceDay();
            state.BeginClientSession();
            state.InspectRepair();
            state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId);
            Check(state.balance != 80 && state.rentDue != 450 && state.serviceInspected && state.serviceConnected,
                "private-state fixture actually changes money, rent, inspection and connection");
            Check(JsonUtility.ToJson(SignalDebtNpcCharacter.CopyShop(state)) == accepted,
                "private balance, rent, deadline, day and repair work do not leak into clerk context");

            Check(state.TestRepair() != null && !state.serviceTestPassed && state.clientDevice.ownerCheckQueued,
                "requesting the owner check does not fabricate a successful observation");
            Check(JsonUtility.ToJson(SignalDebtNpcCharacter.CopyShop(state)) == accepted,
                "a queued check cannot make the clerk claim to have opened the folder already");
            Check(state.EndClientSession() != null && state.StepClientTask() != null
                && state.clientDevice.CurrentOwnerVerified && state.clientDevice.OwnerHasCompletedFirstOrder,
                "the owner really opens the same folder and prepares an order after the remote view closes");
            stages.Add(SignalDebtNpcCharacter.CopyShop(state));
            state.ClaimRepairPayment();
            stages.Add(SignalDebtNpcCharacter.CopyShop(state));
            for (int index = 1; index < stages.Count; index++)
            {
                Check(stages[index].current_state != stages[index - 1].current_state,
                    "public job stage " + index + " changes the clerk's current concern");
                Check(stages[index].name == stages[0].name && stages[index].role == stages[0].role
                    && stages[index].disposition == stages[0].disposition,
                    "public job stage " + index + " preserves character identity and voice");
                Build(stages[index], new SignalDebtAiProvider.ChatLine[0], "Synthetic follow-up.");
            }
        }

        private static bool ClientKnows(SignalDebtLedgerEvent entry)
        {
            if (entry == null) throw new InvalidOperationException("The knowledge-boundary fixture did not produce a real event.");
            MethodInfo method = typeof(SignalDebtNpcClient).GetMethod("IsClientEvent", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Missing production client-event filter.");
            return (bool)method.Invoke(null, new object[] { entry.type });
        }

        private static void OwnerObservationKnowledge()
        {
            groups++;
            var state = new SignalDebtSaveData();
            state.AcceptRepair(); state.BeginClientSession();
            SignalDebtLedgerEvent inspection = state.InspectRepair();
            SignalDebtLedgerEvent connection = state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId);
            SignalDebtLedgerEvent firstRequest = state.TestRepair();
            SignalDebtLedgerEvent success = state.StepClientTask();
            Check(state.Validate() && state.clientDevice.CurrentOwnerVerified && state.clientDevice.OwnerHasCompletedFirstOrder,
                "knowledge fixture has completed actual owner work on the shared client device");
            Check(!ClientKnows(inspection) && !ClientKnows(connection) && ClientKnows(firstRequest) && ClientKnows(success),
                "production event filter shares the requested check and observed result but excludes private inspection and routing");
            var successfulProfile = SignalDebtNpcCharacter.CopyShop(state);
            string successfulContext = JsonUtility.ToJson(successfulProfile);
            Check(successfulProfile.current_state.Contains("1042") && successfulProfile.current_state.Contains("earlier test"),
                "clerk context describes its completed order and qualifies the successful check as historical");

            SignalDebtLedgerEvent reroute = state.ConnectRepair(SignalDebtClientDevice.PrintQueueOutputPortId);
            Check(reroute != null && !state.clientDevice.CurrentOwnerVerified && !state.CanClaimRepairPayment,
                "the fixture actually invalidates current verification after the old successful check");
            Check(JsonUtility.ToJson(SignalDebtNpcCharacter.CopyShop(state)) == successfulContext && !ClientKnows(reroute),
                "an unseen reroute cannot reveal new device health or private technical work to the clerk");
            Input beforeObservation = Build(SignalDebtNpcCharacter.CopyShop(state), new SignalDebtAiProvider.ChatLine[0], "Synthetic health question.");
            Check(JsonUtility.ToJson(beforeObservation.context) == successfulContext,
                "the provider receives only historical success until the owner makes another observation");

            SignalDebtLedgerEvent request = state.TestRepair();
            Check(request != null && ClientKnows(request) && !state.clientDevice.CurrentOwnerVerified
                && JsonUtility.ToJson(SignalDebtNpcCharacter.CopyShop(state)) == successfulContext,
                "a new known check request still cannot fabricate knowledge of its future result");
            state.EndClientSession();
            SignalDebtLedgerEvent failure = state.StepClientTask();
            Check(failure != null && ClientKnows(failure) && state.serviceTestPassed
                && state.clientDevice.lastOwnerReadResult == SignalDebtFolderResult.PrintService,
                "a later actual failed read becomes client knowledge while the earlier success remains historical");
            var failedProfile = SignalDebtNpcCharacter.CopyShop(state);
            string failedContext = JsonUtility.ToJson(failedProfile);
            Check(failedContext != successfulContext && failedProfile.current_state.Contains("failing")
                && failedProfile.current_state.Contains("1042") && !state.CanClaimRepairPayment,
                "the latest-failure profile preserves the already prepared order without claiming current success or payment");
            Check(failedProfile.name == successfulProfile.name && failedProfile.role == successfulProfile.role
                && failedProfile.disposition == successfulProfile.disposition,
                "a failed task changes the clerk's observation without replacing their identity or personality");
            Input afterFailure = Build(failedProfile, new SignalDebtAiProvider.ChatLine[0], "Synthetic follow-up about the failed check.");
            Check(JsonUtility.ToJson(afterFailure.context) == failedContext,
                "production payload carries the latest failed observation and retained prior work together");
            using (var reopened = new ConversationFixture(state.Copy()))
            {
                var firstCapture = reopened.Ui.CaptureClientHistoryForAi(null);
                var secondCapture = reopened.Ui.CaptureClientHistoryForAi(null);
                Check(firstCapture.Length == 2
                    && firstCapture[1].text == "still won't open. it's getting print jobs where the order list should be.",
                    "a fresh conversation on an existing failed campaign presents the latest actual owner outcome");
                Check(Matches(secondCapture, firstCapture, 0, firstCapture.Length),
                    "repeated capture after a native failure cannot append a stale legacy-success line");
            }

            int completedAt = state.clientDevice.firstOrderCompletedAtCheck;
            state.BeginClientSession();
            SignalDebtLedgerEvent restored = state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId);
            Check(restored != null && !ClientKnows(restored)
                && JsonUtility.ToJson(SignalDebtNpcCharacter.CopyShop(state)) == failedContext,
                "privately restoring the connection does not erase what the clerk last observed");
            state.TestRepair();
            SignalDebtLedgerEvent observedRecovery = state.StepClientTask();
            var recoveredProfile = SignalDebtNpcCharacter.CopyShop(state);
            Check(observedRecovery != null && ClientKnows(observedRecovery) && state.clientDevice.CurrentOwnerVerified
                && state.clientDevice.firstOrderCompletedAtCheck == completedAt && state.clientDevice.orders[0].version == 2,
                "only a new owner observation restores current verification without repeating the completed order");
            Check(JsonUtility.ToJson(recoveredProfile) != failedContext && recoveredProfile.current_state.Contains("1042"),
                "the recovered profile retains the real completed order while updating the owner's observation");
            Build(recoveredProfile, new SignalDebtAiProvider.ChatLine[0], "Synthetic recovery follow-up.");
        }

        private static SignalDebtLedgerEvent[] SelectClientEvents(SignalDebtSaveData state, int revision)
        {
            MethodInfo method = typeof(SignalDebtNpcClient).GetMethod("SelectClientEvents", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Missing production client-event selection.");
            return (SignalDebtLedgerEvent[])method.Invoke(null, new object[] { state, revision });
        }

        private static void RepeatedCheckPayload()
        {
            groups++;
            var state = new SignalDebtSaveData();
            state.AcceptRepair(); state.BeginClientSession(); state.InspectRepair();
            int earlierRevision = 0;
            string oldestObservation = null, latestObservation = null;
            bool commandsWorked = true;
            for (int index = 0; index < 16; index++)
            {
                string endpoint = index % 2 == 0 ? SignalDebtClientDevice.PrintQueueOutputPortId
                    : SignalDebtClientDevice.OrderIndexOutputPortId;
                commandsWorked &= state.ConnectRepair(endpoint) != null && state.TestRepair() != null;
                SignalDebtLedgerEvent observation = state.StepClientTask();
                commandsWorked &= observation != null;
                if (observation != null)
                {
                    if (oldestObservation == null) oldestObservation = observation.id;
                    latestObservation = observation.id;
                }
                if (index == 11) earlierRevision = state.revision;
            }
            commandsWorked &= state.ClaimRepairPayment() != null;
            SignalDebtLedgerEvent privateRent = state.ApplyRentPayment(10);
            Check(commandsWorked && privateRent != null && state.Validate() && state.clientDevice.ownerCheckCount == 16,
                "bounded-context fixture performs repeated real owner checks, one payment and private rent activity");
            string before = JsonUtility.ToJson(state);
            SignalDebtLedgerEvent[] selected = SelectClientEvents(state, state.revision);
            var ids = new HashSet<string>();
            bool privateExcluded = true, ordered = true;
            int previousRevision = -1;
            foreach (SignalDebtLedgerEvent entry in selected)
            {
                privateExcluded &= ClientKnows(entry) && entry.id != privateRent.id;
                ordered &= entry.worldRevision > previousRevision && entry.worldRevision <= state.revision;
                previousRevision = entry.worldRevision;
                ids.Add(entry.id);
            }
            Check(selected.Length == 24 && ids.Count == selected.Length && ordered && privateExcluded,
                "production selection caps long history at 24 unique chronological client-known facts");
            Check(ids.Contains("repair-job-accepted") && ids.Contains("repair-job-payment")
                && ids.Contains(latestObservation) && !ids.Contains(oldestObservation),
                "the bounded context retains the agreement, settlement and newest observation while retiring old checks");
            SignalDebtLedgerEvent[] earlier = SelectClientEvents(state, earlierRevision);
            bool cutoffRespected = earlier.Length <= 24 && earlier.Length > 0;
            foreach (SignalDebtLedgerEvent entry in earlier)
                cutoffRespected &= entry.worldRevision <= earlierRevision && entry.id != "repair-job-payment";
            Check(cutoffRespected && JsonUtility.ToJson(state) == before,
                "selection respects the requested historical revision and never mutates the campaign");

            var facts = new SignalDebtNpcClient.SourceEvent[selected.Length];
            for (int index = 0; index < selected.Length; index++)
            {
                SignalDebtLedgerEvent entry = selected[index];
                facts[index] = new SignalDebtNpcClient.SourceEvent
                {
                    event_id = entry.id, world_revision = entry.worldRevision,
                    description = entry.summary, known_by = new[] { SignalDebtNpcClient.CopyShopNpcId },
                    source = entry.type == "job_paid" ? "record"
                        : entry.type == "job_accepted" || entry.type == "client_check_requested" ? "message" : "observed",
                    status = "resolved", tags = new[] { "repair", "job" }
                };
            }
            Input payload = Build(SignalDebtNpcCharacter.CopyShop(state), new SignalDebtAiProvider.ChatLine[0],
                "Synthetic conversation after repeated checks.", facts, state.revision);
            bool payloadMatches = payload.facts != null && payload.facts.Length == selected.Length;
            if (payloadMatches)
                for (int index = 0; index < selected.Length; index++)
                    payloadMatches &= payload.facts[index].fact_id == selected[index].id
                        && payload.facts[index].world_revision == selected[index].worldRevision;
            Check(payloadMatches, "the provider accepts the bounded real-event selection and preserves each selected identity and revision");
        }

        private static void ProofKnowledgeBoundary()
        {
            groups++;
            using (var fixture = new ConversationFixture())
            {
                SignalDebtSaveData state = fixture.State;
                state.AcceptRepair(); state.BeginClientSession(); state.InspectRepair();
                state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId);
                state.TestRepair(); state.StepClientTask(); state.ClaimRepairPayment();
                var offered = SignalDebtNpcCharacter.CopyShop(state);
                Check(offered.current_state.Contains("has not accepted") && !state.proofAccepted,
                    "the follow-up offer does not invent document permission");
                SignalDebtLedgerEvent accepted = state.AcceptProofRecovery();
                string acceptedContext = JsonUtility.ToJson(SignalDebtNpcCharacter.CopyShop(state));
                var offeredHistory = fixture.Ui.CaptureClientHistoryForAi(null);
                SignalDebtLedgerEvent restore = state.RestoreProofRevision(SignalDebtProofDocument.RevisionId(1));
                Check(accepted != null && ClientKnows(accepted) && restore != null && !ClientKnows(restore)
                    && JsonUtility.ToJson(SignalDebtNpcCharacter.CopyShop(state)) == acceptedContext,
                    "explicit document permission is known while a private wrong-date restore is not");
                Check(Matches(fixture.Ui.CaptureClientHistoryForAi(null), offeredHistory, 0, offeredHistory.Length),
                    "private proof edits do not insert spoken messages into UI or AI history");
                SignalDebtLedgerEvent request = state.RequestProofCheck();
                Check(ClientKnows(request) && JsonUtility.ToJson(SignalDebtNpcCharacter.CopyShop(state)) == acceptedContext,
                    "requesting a proof check cannot give the owner advance knowledge of its result");
                state.EndClientSession();
                SignalDebtLedgerEvent failed = state.StepProofTask();
                var failedProfile = SignalDebtNpcCharacter.CopyShop(state);
                Check(ClientKnows(failed) && failedProfile.current_state.Contains("old date") && !state.CanClaimProofPayment,
                    "the owner learns the wrong date from its actual read with the player session closed");
                var failedHistory = fixture.Ui.CaptureClientHistoryForAi(null);
                Check(failedHistory[failedHistory.Length - 1].text.Contains("old date"),
                    "the actual failed proof outcome joins visible conversation");
                state.BeginClientSession();
                state.RestoreProofRevision(SignalDebtProofDocument.RevisionId(2));
                Check(JsonUtility.ToJson(SignalDebtNpcCharacter.CopyShop(state)) == JsonUtility.ToJson(failedProfile),
                    "a private recovery cannot replace the owner's last failed observation");
                state.RequestProofCheck();
                SignalDebtLedgerEvent exported = state.StepProofTask();
                Check(ClientKnows(exported) && !string.IsNullOrEmpty(exported.exportId)
                    && SignalDebtNpcCharacter.CopyShop(state).current_state.Contains("exported"),
                    "actual owner export grants knowledge of output without claiming physical printing");
                SignalDebtLedgerEvent paid = state.ClaimProofPayment();
                SignalDebtLedgerEvent rent = state.ApplyRentPayment(10);
                Check(ClientKnows(paid) && !ClientKnows(rent) && state.Validate(),
                    "the owner knows the separate 160-credit receipt, not private rent activity");
                var ids = new HashSet<string>();
                foreach (SignalDebtLedgerEvent entry in SelectClientEvents(state, state.revision))
                {
                    Check(ClientKnows(entry), "proof dialogue selection contains only personally known events");
                    ids.Add(entry.id);
                }
                Check(ids.Contains(SignalDebtSaveData.ProofAcceptanceEventId) && ids.Contains(SignalDebtSaveData.ProofPaymentEventId)
                    && ids.Contains(exported.id) && !ids.Contains(restore.id) && !ids.Contains(rent.id),
                    "production selection preserves both job agreements and receipts without leaking document edits");
                Build(SignalDebtNpcCharacter.CopyShop(state), fixture.Ui.CaptureClientHistoryForAi(null), "Synthetic proof follow-up.");
            }
        }

        private static void VisibleHistory(ConversationFixture fixture)
        {
            groups++;
            SignalDebtUI ui = fixture.Ui;
            var opening = ui.CaptureClientHistoryForAi(null);
            Check(opening.Length == 1 && opening[0].role == "assistant" && !string.IsNullOrWhiteSpace(opening[0].text),
                "the actual authored opening enters AI history as an assistant line");
            const string repeatedQuestion = "Synthetic question asked twice.";
            fixture.Append(repeatedQuestion, true);
            ui.ShowClientReply("Synthetic model response.");
            fixture.Append(repeatedQuestion, true);
            SignalDebtAiProvider.ChatLine[] visible = fixture.Visible();
            SignalDebtAiProvider.ChatLine[] captured = ui.CaptureClientHistoryForAi(repeatedQuestion);
            Check(captured.Length == visible.Length - 1, "only the current appended user turn is excluded from history");
            Check(Matches(captured, visible, 0, captured.Length), "authored, earlier user and model lines keep visible order and roles");
            Check(captured[1].role == "user" && captured[1].text == repeatedQuestion,
                "an identical earlier user message is retained");
            Check(Matches(ui.CaptureClientHistoryForAi("A different pending message."), visible, 0, visible.Length),
                "a nonmatching pending message does not remove the last visible line");

            Input payload = Build(SignalDebtNpcCharacter.CopyShop(fixture.State), captured, repeatedQuestion);
            Check(payload.player_message == repeatedQuestion && Matches(payload.history, captured, 0, captured.Length),
                "provider receives visible prior history and current message in their separate fields");
            captured[0].text = "Synthetic mutation outside UI.";
            captured[0].role = "user";
            captured[1] = null;
            Check(Matches(ui.CaptureClientHistoryForAi(repeatedQuestion), visible, 0, visible.Length - 1),
                "mutating returned history elements and array slots cannot alter UI conversation");

            ui.ShowClientReply(repeatedQuestion);
            visible = fixture.Visible();
            Check(Matches(ui.CaptureClientHistoryForAi(repeatedQuestion), visible, 0, visible.Length),
                "an assistant line matching current text is never removed as a user turn");

            fixture.State.AcceptRepair();
            fixture.State.BeginClientSession();
            fixture.State.InspectRepair();
            fixture.State.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId);
            int beforeMilestone = visible.Length;
            Check(fixture.State.TestRepair() != null && !fixture.State.serviceTestPassed,
                "visible-conversation fixture has queued owner work without a success milestone");
            captured = ui.CaptureClientHistoryForAi(null);
            Check(fixture.Visible().Length == beforeMilestone && Matches(captured, visible, 0, visible.Length),
                "queuing work cannot insert an invented successful-test message into visible or AI history");
            Check(fixture.State.StepClientTask() != null && fixture.State.clientDevice.OwnerHasCompletedFirstOrder,
                "the successful-test message follows actual owner work on the shared device");
            captured = ui.CaptureClientHistoryForAi(null);
            visible = fixture.Visible();
            Check(visible.Length == beforeMilestone + 1 && captured[captured.Length - 1].role == "assistant"
                && captured[captured.Length - 1].text == "yeah, orders are back. got 1042 ready for the customer. that's a relief."
                && Matches(captured, visible, 0, visible.Length),
                "the actual first-order completion line joins visible and AI history together");
            fixture.State.ConnectRepair(SignalDebtClientDevice.PrintQueueOutputPortId);
            captured = ui.CaptureClientHistoryForAi(null);
            Check(Matches(captured, visible, 0, visible.Length), "private rerouting cannot invent a new spoken owner observation");
            fixture.State.TestRepair(); fixture.State.StepClientTask();
            captured = ui.CaptureClientHistoryForAi(null);
            visible = fixture.Visible();
            Check(visible.Length == beforeMilestone + 2
                && captured[captured.Length - 1].text == "still won't open. it's getting print jobs where the order list should be."
                && Matches(captured, visible, 0, visible.Length),
                "the actual failed folder read adds its specific authored outcome to visible and AI history");
            fixture.State.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId);
            fixture.State.TestRepair(); fixture.State.StepClientTask();
            captured = ui.CaptureClientHistoryForAi(null);
            visible = fixture.Visible();
            Check(visible.Length == beforeMilestone + 3 && captured[captured.Length - 1].text == "opened it again. the orders are still there."
                && Matches(captured, visible, 0, visible.Length),
                "a real later success reports a recheck without pretending to prepare the completed order again");
            fixture.State.ClaimRepairPayment();
            captured = ui.CaptureClientHistoryForAi(null);
            visible = fixture.Visible();
            Check(visible.Length == beforeMilestone + 5
                && captured[captured.Length - 2].text == "sent the 120. thanks for getting that folder open."
                && captured[captured.Length - 1].text.Contains("160 to recover it")
                && Matches(captured, visible, 0, visible.Length),
                "the actual settlement and separate recovery offer follow owner outcomes in visible and AI history");

            // Long world toasts are visible as Notice rows but are not things this
            // client said or learned. They must not become conversational knowledge.
            var spokenBeforeNotice = captured;
            const string privateNotice = "Synthetic private notice: hidden rent balance 7319.";
            fixture.Append(privateNotice, false, "Notice");
            visible = fixture.Visible();
            Check(visible[visible.Length - 1].text == privateNotice, "private Notice fixture really exists in visible UI data");
            captured = ui.CaptureClientHistoryForAi(null);
            Check(Matches(captured, spokenBeforeNotice, 0, spokenBeforeNotice.Length),
                "non-conversation Notice rows cannot enter the clerk's AI history");
            payload = Build(SignalDebtNpcCharacter.CopyShop(fixture.State), captured, "Synthetic public follow-up.");
            Check(Matches(payload.history, spokenBeforeNotice, 0, spokenBeforeNotice.Length),
                "private UI notices remain excluded through production payload serialization");

            var legacy = new SignalDebtSaveData
            {
                schemaVersion = 2, clientDevice = null, jobAccepted = true,
                serviceInspected = true, serviceConnected = true, serviceTestPassed = true
            };
            legacy.Record("job_accepted", "Accepted the old repair.", 0, "repair-job-accepted");
            legacy.Record("service_inspected", "Inspected the old service.", 0, "repair-service-inspected");
            legacy.Record("service_connected", "Restored the old connection.", 0, "repair-service-connected");
            legacy.Record("service_test_passed", "Client opened the order folder during the old test.", 0, "repair-service-test");
            SignalDebtSaveData imported = SignalDebtSaveData.MigrateV2(legacy);
            Check(imported != null && imported.Validate() && imported.clientDevice.ownerCheckCount == 0,
                "legacy-dialogue fixture imports real historical milestones without new owner work");
            using (var legacyFixture = new ConversationFixture(imported))
            {
                var history = legacyFixture.Ui.CaptureClientHistoryForAi(null);
                Check(history.Length == 2 && history[1].text == "opened when i tested it. the orders were all there."
                    && !imported.clientDevice.OwnerHasCompletedFirstOrder,
                    "legacy success remains a past test rather than an invented new order-completion line");
                Check(imported.ClaimRepairPayment() != null, "the imported unpaid fee settles through its preserved entitlement");
                history = legacyFixture.Ui.CaptureClientHistoryForAi(null);
                Check(history.Length == 4 && history[2].text == "sent the 120. thanks for getting that folder open."
                    && history[3].text.Contains("160 to recover it") && imported.clientDevice.ownerCheckCount == 0
                    && !imported.clientDevice.OwnerHasCompletedFirstOrder && !imported.clientDevice.CurrentOwnerVerified
                    && !imported.proofAccepted && !imported.proofPaid && imported.proofDocument.LastOwnerRead() == null
                    && imported.proofDocument.LatestExport() == null,
                    "legacy settlement retains its payment and separate unaccepted recovery offer without inventing owner work");
            }
        }

        private static void HistoryBounds(ConversationFixture fixture)
        {
            groups++;
            for (int index = 0; index < 30; index++)
                fixture.Append("Synthetic bounded line " + index + ".", index % 2 == 0);
            var visible = fixture.Visible();
            var captured = fixture.Ui.CaptureClientHistoryForAi(null);
            Check(visible.Length == 12 && captured.Length == 12, "long conversations retain at most twelve visible and captured lines");
            Check(Matches(captured, visible, 0, visible.Length), "bounded history retains the UI's actual ordering");
            fixture.Ui.ShowClientReply(new string('x', 1500));
            visible = fixture.Visible();
            captured = fixture.Ui.CaptureClientHistoryForAi(null);
            Check(visible[visible.Length - 1].text.Length == 1500 && captured[captured.Length - 1].text.Length == 1000,
                "provider history clips a long line without changing its visible UI text");
            Check(Matches(captured, visible, 0, visible.Length), "bounded line text remains the matching visible prefix");
            Build(SignalDebtNpcCharacter.CopyShop(fixture.State), captured, "Synthetic next turn.");
        }

        private sealed class ConversationFixture : IDisposable
        {
            public readonly SignalDebtSaveData State;
            public SignalDebtUI Ui { get; private set; }
            private GameObject root;
            private readonly SignalDebtBootstrap originalInstance;

            public ConversationFixture(SignalDebtSaveData state = null)
            {
                State = state ?? new SignalDebtSaveData();
                originalInstance = SignalDebtBootstrap.Instance;
                root = new GameObject("Synthetic conversation boundary fixture");
                root.hideFlags = HideFlags.HideAndDontSave;
                // Inactivity prevents Awake/Start from owning the world or creating a store.
                root.SetActive(false);
                try
                {
                    var game = root.AddComponent<SignalDebtBootstrap>();
                    game.enabled = false;
                    Check(SignalDebtBootstrap.Instance == originalInstance && Field(typeof(SignalDebtBootstrap), "store").GetValue(game) == null,
                        "inactive fixture does not initialize Bootstrap or a campaign store");
                    PropertyInfo property = typeof(SignalDebtBootstrap).GetProperty("State");
                    MethodInfo setter = property == null ? null : property.GetSetMethod(true);
                    if (setter == null) throw new InvalidOperationException("Missing synthetic campaign injection seam.");
                    setter.Invoke(game, new object[] { State });
                    Ui = root.AddComponent<SignalDebtUI>();
                    Ui.enabled = false;
                    Field(typeof(SignalDebtUI), "game").SetValue(Ui, game);
                    Check(!(bool)Field(typeof(SignalDebtUI), "preferencesReady").GetValue(Ui)
                        && Field(typeof(SignalDebtUI), "computerCanvas").GetValue(Ui) == null,
                        "UI fixture has no initialized desktop, world canvas or preference persistence");
                }
                catch { Dispose(); throw; }
            }

            public void Append(string text, bool fromPlayer, string speaker = null)
            {
                MethodInfo method = typeof(SignalDebtUI).GetMethod("AppendClientLine", PrivateInstance);
                if (method == null) throw new InvalidOperationException("Missing UI conversation append seam.");
                method.Invoke(Ui, new object[] { speaker ?? (fromPlayer ? "You" : "Southbank"), text, fromPlayer });
            }

            public SignalDebtAiProvider.ChatLine[] Visible()
            {
                var lines = (IList)Field(typeof(SignalDebtUI), "conversation").GetValue(Ui);
                var result = new SignalDebtAiProvider.ChatLine[lines.Count];
                for (int index = 0; index < result.Length; index++)
                {
                    object line = lines[index];
                    Type type = line.GetType();
                    result[index] = new SignalDebtAiProvider.ChatLine
                    {
                        role = (bool)type.GetField("fromPlayer").GetValue(line) ? "user" : "assistant",
                        text = (string)type.GetField("text").GetValue(line)
                    };
                }
                return result;
            }

            public void Dispose()
            {
                if (root == null) return;
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                Check(SignalDebtBootstrap.Instance == originalInstance, "fixture cleanup preserves the existing Bootstrap singleton");
            }
        }

        private static Input Build(SignalDebtNpcClient.NpcContext context, SignalDebtAiProvider.ChatLine[] history, string current,
            SignalDebtNpcClient.SourceEvent[] facts = null, int revision = 0)
        {
            var request = new SignalDebtNpcClient.DialogueRequest
            {
                campaign_id = "synthetic_conversation", request_id = "synthetic_turn",
                npc_id = SignalDebtNpcClient.CopyShopNpcId, world_revision = revision,
                npc_context = context, player_text = current
            };
            bool built = SignalDebtAiProvider.TryBuildPayload(request, facts ?? new SignalDebtNpcClient.SourceEvent[0], history,
                out string json, out string status);
            Check(built && status == null, "production provider accepts the synthetic context/history payload");
            Payload envelope = JsonUtility.FromJson<Payload>(json);
            Input input = JsonUtility.FromJson<Input>(envelope.input);
            Check(input != null && input.context != null && input.history != null, "production payload is parseable using actual Unity JSON");
            return input;
        }

        private static bool Matches(SignalDebtAiProvider.ChatLine[] actual, SignalDebtAiProvider.ChatLine[] expected, int start, int count)
        {
            if (actual == null || actual.Length != count) return false;
            for (int index = 0; index < count; index++)
            {
                var line = expected[start + index];
                string text = line.text.Length <= 1000 ? line.text : line.text.Substring(0, 1000);
                if (actual[index] == null || actual[index].role != line.role || actual[index].text != text) return false;
            }
            return true;
        }

        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, PrivateInstance);
            if (field == null) throw new InvalidOperationException("Missing synthetic fixture field: " + name);
            return field;
        }

        private static void Check(bool condition, string description)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("FAIL: " + description);
            passedAssertions.Add(description);
        }

        private static string SourceHash(string relativePath)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath, relativePath))))
                    .Replace("-", "").ToLowerInvariant();
        }

        private static string ReportPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length; index++)
                if (args[index] == "-signalDebtConversationReport")
                {
                    if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException("-signalDebtConversationReport requires a file path.");
                    return Path.GetFullPath(args[index + 1]);
                }
            return Path.Combine(Application.temporaryCachePath, "SignalDebtConversationChecks.json");
        }
    }
}
#endif
