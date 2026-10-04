using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SignalDebt
{
    /// <summary>
    /// Optional, local-only NPC relay transport. Never stores an OpenAI credential.
    /// All game state remains owned by the caller; replies are presentation proposals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SignalDebtNpcClient : MonoBehaviour
    {
        public const string RelayOrigin = "http://127.0.0.1:8765";
        public const string CopyShopNpcId = "copy_shop_client";
        private const int TimeoutSeconds = 8;
        private const int MaxRequestBytes = 32768;
        private const int MaxResponseBytes = 16384;

        [SerializeField, Tooltip("Enable only when the local NPC relay is running.")]
        private bool relayEnabled;

        private readonly HashSet<PendingRequest> pending = new HashSet<PendingRequest>();
        private readonly Dictionary<string, string> activeDialogues = new Dictionary<string, string>();
        private readonly HashSet<string> acknowledgedEvents = new HashSet<string>();
        private readonly Dictionary<string, EventReportRequest> eventCopies = new Dictionary<string, EventReportRequest>();
        private int cancellationGeneration;
        [SerializeField] private SignalDebtBootstrap game;
        private SignalDebtSaveData campaignState;
        private string campaignId;
        private bool syncing;
        private bool chatting;
        private bool lastSyncSucceeded;
        private int syncSequence;
        private int chatSequence;

        [Serializable]
        public sealed class SourceEvent
        {
            public string event_id;
            public int world_revision;
            public string description;
            public string source = "observed";
            public string[] known_by = new string[0];
            public string[] tags = new string[0];
            public bool commitment;
            public string status = "active";
        }

        [Serializable]
        public sealed class EventReportRequest
        {
            public string campaign_id;
            public SourceEvent @event;
        }

        [Serializable]
        public sealed class EventReportReply
        {
            public string event_id;
            public bool duplicate;
            public int world_revision;
        }

        public sealed class ReportResult
        {
            public bool Success;
            public string Status;
            public EventReportReply Reply;
        }

        [Serializable]
        public sealed class NpcContext
        {
            public string name;
            public string role;
            public string disposition;
            public string[] goals;
            public string current_state;
        }

        [Serializable]
        public sealed class DialogueRequest
        {
            public string campaign_id;
            public string npc_id;
            public string request_id;
            public int world_revision;
            public string player_text;
            public NpcContext npc_context;
            public string[] focus_tags = new string[0];
            public string[] allowed_intents = { "continue", "ask_question", "decline", "end_conversation" };
            public bool escalate;
            // Client-side validation data; never serialized to the relay as invented facts.
            [NonSerialized] public string[] PermittedFactIds;
        }

        [Serializable]
        public sealed class DialogueReply
        {
            public string request_id;
            public string npc_id;
            public string speaker;
            public string text;
            public string[] used_fact_ids;
            public string proposed_intent;
            public int world_revision;
            public bool stale;
            public string source_mode;
            public string fallback_reason;
        }

        public sealed class DialogueResult
        {
            public bool Accepted;
            public bool IsFallback;
            public bool IsStale;
            public string Text;
            public string Status;
            public DialogueReply Reply;
        }

        [Serializable]
        private sealed class CancelRequest
        {
            public string campaign_id;
            public string request_id;
        }

        [Serializable]
        private sealed class ErrorReply { public string error; }

        public bool RelayEnabled
        {
            get { return relayEnabled; }
            set
            {
                relayEnabled = value;
                if (!value) CancelAllRequests();
            }
        }

        public string LastStatus { get; private set; }

        /// <summary>Reports only committed game events. Repeating the same immutable ID is safe.</summary>
        public IEnumerator ReportEvent(EventReportRequest report, Action<ReportResult> completed)
        {
            if (report == null || report.@event == null || !ValidId(report.campaign_id) ||
                !ValidId(report.@event.event_id) || report.@event.world_revision < 0)
            {
                completed?.Invoke(new ReportResult { Status = "invalid_event" });
                yield break;
            }
            TransportResult transport = null;
            yield return PostJson("/event", JsonUtility.ToJson(report), value => transport = value);
            EventReportReply reply = null;
            if (transport != null && string.IsNullOrEmpty(transport.Error))
                reply = Parse<EventReportReply>(transport.Body);
            bool valid = reply != null && reply.event_id == report.@event.event_id &&
                reply.world_revision >= report.@event.world_revision;
            completed?.Invoke(new ReportResult
            {
                Success = valid, Reply = reply,
                Status = valid ? "reported" : (transport?.Error ?? "invalid_response")
            });
        }

        /// <summary>
        /// Requests wording, never a game mutation. The caller supplies its live revision and
        /// a truthful authored fallback. Stale or cancelled results have Accepted=false and no text.
        /// This method makes at most one dialogue request and never retries a paid call.
        /// </summary>
        public IEnumerator Ask(DialogueRequest request, Func<int> currentWorldRevision,
            string authoredFallback, Action<DialogueResult> completed)
        {
            if (request == null || currentWorldRevision == null || !ValidId(request.campaign_id) ||
                !ValidId(request.npc_id) || !ValidId(request.request_id) || request.world_revision < 0 ||
                request.player_text == null || request.player_text.Length > 2000 || request.npc_context == null)
            {
                completed?.Invoke(Fallback(authoredFallback, "invalid_request"));
                yield break;
            }
            if (currentWorldRevision() != request.world_revision)
            {
                completed?.Invoke(Stale("stale_world_revision"));
                yield break;
            }
            if (activeDialogues.ContainsKey(request.request_id))
            {
                completed?.Invoke(Fallback(authoredFallback, "request_pending"));
                yield break;
            }

            int generation = cancellationGeneration;
            activeDialogues.Add(request.request_id, request.campaign_id);
            TransportResult transport = null;
            try
            {
                yield return PostJson("/dialogue", JsonUtility.ToJson(request), value => transport = value);
            }
            finally { activeDialogues.Remove(request.request_id); }

            if (generation != cancellationGeneration || currentWorldRevision() != request.world_revision)
            {
                SendCancellationBestEffort(request.campaign_id, request.request_id);
                completed?.Invoke(Stale("cancelled_or_stale"));
                yield break;
            }
            if (transport == null || !string.IsNullOrEmpty(transport.Error))
            {
                string reason = transport?.Error ?? "relay_unavailable";
                if (reason == "timeout" || reason == "relay_unavailable")
                    SendCancellationBestEffort(request.campaign_id, request.request_id);
                completed?.Invoke(reason == "stale_world_revision" ? Stale(reason) : Fallback(authoredFallback, reason));
                yield break;
            }
            DialogueReply reply = Parse<DialogueReply>(transport.Body);
            if (reply == null || reply.request_id != request.request_id || reply.npc_id != request.npc_id ||
                reply.speaker != request.npc_id || reply.world_revision != request.world_revision)
            {
                completed?.Invoke(Fallback(authoredFallback, "invalid_response"));
                yield break;
            }
            if (reply.stale || reply.source_mode == "stale" || reply.source_mode == "cancelled")
            {
                completed?.Invoke(Stale("stale_response"));
                yield break;
            }
            if (reply.source_mode == "authored_fallback")
            {
                completed?.Invoke(Fallback(authoredFallback, reply.fallback_reason ?? "authored_fallback"));
                yield break;
            }
            if (reply.source_mode != "model" || string.IsNullOrWhiteSpace(reply.text) || reply.text.Length > 1500 ||
                reply.used_fact_ids == null || reply.used_fact_ids.Length > 24 ||
                request.allowed_intents == null || Array.IndexOf(request.allowed_intents, reply.proposed_intent) < 0 ||
                !FactsAllowed(request.PermittedFactIds, reply.used_fact_ids))
            {
                completed?.Invoke(Fallback(authoredFallback, "invalid_response"));
                yield break;
            }
            completed?.Invoke(new DialogueResult
            {
                Accepted = true, Text = reply.text, Status = "model", Reply = reply
            });
        }

        private void OnEnable()
        {
            if (game == null) game = GetComponent<SignalDebtBootstrap>();
            if (game != null)
            {
                game.GameEventCommitted -= OnGameEventCommitted;
                game.GameEventCommitted += OnGameEventCommitted;
            }
        }

        private void Update()
        {
            if (game != null && game.State != null && !ReferenceEquals(campaignState, game.State))
                EnsureCampaign();
        }

        private void EnsureCampaign()
        {
            if (game == null || game.State == null || ReferenceEquals(campaignState, game.State)) return;
            CancelAllRequests();
            campaignState = game.State;
            // A fresh load branch prevents an older restored save inheriting future relay memories.
            string save = ValidId(campaignState.saveId) ? campaignState.saveId : "local";
            if (save.Length > 40) save = save.Substring(0, 40);
            campaignId = save + ":load:" + Guid.NewGuid().ToString("N");
            acknowledgedEvents.Clear();
            eventCopies.Clear();
        }

        private void OnGameEventCommitted(SignalDebtLedgerEvent entry)
        {
            EnsureCampaign();
            if (RelayEnabled && !syncing && campaignState != null) StartCoroutine(SyncLedger());
        }

        public void ToggleRelay()
        {
            RelayEnabled = !RelayEnabled;
            if (game != null) game.ShowToast(RelayEnabled ? "Optional NPC relay enabled." : "NPC dialogue uses authored offline lines.");
        }

        /// <summary>Optional SendMessage/UI entry point for the copy-shop clerk.</summary>
        public void BeginClientChat(string playerText)
        {
            if (game == null) game = GetComponent<SignalDebtBootstrap>();
            if (game == null || game.State == null) return;
            EnsureCampaign();
            if (chatting)
            {
                game.ShowToast("The clerk is still answering. You can keep moving.");
                return;
            }
            StartCoroutine(ClientChat(playerText ?? "How is the service looking?"));
        }

        private IEnumerator ClientChat(string playerText)
        {
            chatting = true;
            int sequence = ++chatSequence;
            int generation = cancellationGeneration;
            SignalDebtSaveData state = campaignState;
            int revision = state.revision;
            string fallback = ClientFallback(state);
            try
            {
                if (!RelayEnabled)
                {
                    game.ShowToast("Clerk: " + fallback);
                    yield break;
                }
                while (syncing && generation == cancellationGeneration) yield return null;
                if (!SameScene(state, revision, generation)) yield break;
                yield return SyncLedger();
                if (!SameScene(state, revision, generation)) yield break;
                if (!lastSyncSucceeded)
                {
                    game.ShowToast("Clerk: " + fallback);
                    yield break;
                }

                var facts = new List<string>();
                foreach (EventReportRequest entry in eventCopies.Values)
                    if (entry.@event.world_revision <= revision && acknowledgedEvents.Contains(entry.@event.event_id) &&
                        Array.IndexOf(entry.@event.known_by, CopyShopNpcId) >= 0)
                        facts.Add(entry.@event.event_id);
                var request = new DialogueRequest
                {
                    campaign_id = campaignId, npc_id = CopyShopNpcId,
                    request_id = Guid.NewGuid().ToString("N"), world_revision = revision,
                    player_text = playerText,
                    npc_context = new NpcContext
                    {
                        name = "Southbank Copy Shop clerk", role = "copy shop client",
                        disposition = "Busy, practical, speaks casually in one or two short sentences.",
                        goals = new[] { "Keep the shop's service working", "Describe only observed work and agreed commitments" },
                        current_state = "At the copy shop counter. Use the supplied source-linked facts for repair history."
                    },
                    focus_tags = new[] { "repair", "job" }, PermittedFactIds = facts.ToArray()
                };
                yield return Ask(request,
                    () => ReferenceEquals(game.State, state) ? game.WorldRevision : -1,
                    fallback, result =>
                    {
                        if (result.Accepted && SameScene(state, revision, generation))
                            game.ShowToast("Clerk: " + result.Text);
                    });
            }
            finally { if (sequence == chatSequence) chatting = false; }
        }

        private bool SameScene(SignalDebtSaveData state, int revision, int generation)
        {
            return game != null && ReferenceEquals(game.State, state) && game.WorldRevision == revision &&
                generation == cancellationGeneration && isActiveAndEnabled;
        }

        private IEnumerator SyncLedger()
        {
            if (syncing || campaignState == null) yield break;
            syncing = true;
            int sequence = ++syncSequence;
            lastSyncSucceeded = false;
            int generation = cancellationGeneration;
            SignalDebtSaveData state = campaignState;
            try
            {
                // Report in recorded order. Never send the NPC's generated prose as a new fact.
                for (int i = 0; i < state.ledger.Count; i++)
                {
                    if (generation != cancellationGeneration || !ReferenceEquals(game.State, state)) yield break;
                    SignalDebtLedgerEvent entry = state.ledger[i];
                    if (entry == null || acknowledgedEvents.Contains(entry.id)) continue;
                    EventReportRequest report;
                    if (!eventCopies.TryGetValue(entry.id, out report))
                    {
                        bool clientKnows = IsClientEvent(entry.type);
                        report = new EventReportRequest
                        {
                            campaign_id = campaignId,
                            @event = new SourceEvent
                            {
                                event_id = entry.id,
                                world_revision = entry.worldRevision > 0 ? entry.worldRevision : Math.Min(i + 1, state.revision),
                                description = Clip(entry.summary, 1600),
                                source = entry.type == "job_accepted" ? "message" : (entry.type == "job_paid" ? "record" : "observed"),
                                known_by = clientKnows ? new[] { CopyShopNpcId } : new string[0],
                                tags = clientKnows ? new[] { "repair", "job", entry.type } : new[] { "private_game_event" },
                                commitment = false, status = "resolved"
                            }
                        };
                        eventCopies.Add(entry.id, report);
                    }
                    ReportResult result = null;
                    yield return ReportEvent(report, value => result = value);
                    if (generation != cancellationGeneration || result == null || !result.Success) yield break;
                    acknowledgedEvents.Add(entry.id);
                }
                // Advance global revision without giving this NPC private rent/balance knowledge.
                int revision = state.revision;
                string barrierId = "revision:" + revision;
                if (!acknowledgedEvents.Contains(barrierId))
                {
                    ReportResult result = null;
                    yield return ReportEvent(new EventReportRequest
                    {
                        campaign_id = campaignId,
                        @event = new SourceEvent
                        {
                            event_id = barrierId, world_revision = revision,
                            description = "The authoritative game revision advanced.", source = "record"
                        }
                    }, value => result = value);
                    if (generation != cancellationGeneration || result == null || !result.Success) yield break;
                    acknowledgedEvents.Add(barrierId);
                }
                lastSyncSucceeded = true;
            }
            finally { if (sequence == syncSequence) syncing = false; }
        }

        private static bool IsClientEvent(string type)
        {
            // The clerk made the deal, opened their Order Folder for the test, and paid.
            // Inspection and reconnection happened privately and grant no clerk knowledge.
            return type == "job_accepted" || type == "service_test_passed" || type == "job_paid";
        }

        private static string ClientFallback(SignalDebtSaveData state)
        {
            if (state.jobPaid) return "Yep, the service is working again. I paid you the agreed 120 credits. Thanks for sorting it out.";
            if (state.serviceTestPassed) return "The service passed its test. Let's settle the repair payment.";
            if (state.jobAccepted) return "Thanks for taking the job. Let me know when you're ready for me to test the Order Folder.";
            return "Our service is down. I can pay 120 credits if you diagnose it, reconnect it and pass the test.";
        }

        private static string Clip(string text, int maximum)
        {
            text = text ?? string.Empty;
            return text.Length <= maximum ? text : text.Substring(0, maximum);
        }

        private static bool ValidId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 96) return false;
            foreach (char c in value)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
                    c == '_' || c == '.' || c == ':' || c == '-')) return false;
            return true;
        }

        private static T Parse<T>(string json) where T : class
        {
            try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<T>(json); }
            catch (Exception) { return null; }
        }

        private static bool FactsAllowed(string[] permitted, string[] used)
        {
            if (permitted == null) return true; // Generic callers can rely on relay validation.
            foreach (string id in used) if (Array.IndexOf(permitted, id) < 0) return false;
            return true;
        }

        private static DialogueResult Fallback(string line, string reason)
        {
            return new DialogueResult { Accepted = true, IsFallback = true, Text = Clip(line, 1500), Status = reason };
        }

        private static DialogueResult Stale(string reason)
        {
            return new DialogueResult { IsStale = true, Text = string.Empty, Status = reason };
        }

        /// <summary>Call before loading a save or abandoning the current conversation.</summary>
        public void CancelAllRequests()
        {
            cancellationGeneration++;
            syncSequence++;
            chatSequence++;
            foreach (KeyValuePair<string, string> dialogue in activeDialogues)
                SendCancellationBestEffort(dialogue.Value, dialogue.Key);
            activeDialogues.Clear();
            foreach (PendingRequest item in new List<PendingRequest>(pending))
                item.AbortAndDispose();
            pending.Clear();
            chatting = false;
            syncing = false;
        }

        private void OnDisable()
        {
            if (game != null) game.GameEventCommitted -= OnGameEventCommitted;
            CancelAllRequests();
        }
        private void OnDestroy() { CancelAllRequests(); }

        private void SendCancellationBestEffort(string campaign, string requestId)
        {
            if (!ValidId(campaign) || !ValidId(requestId)) return;
            var cancel = new UnityWebRequest(RelayOrigin + "/cancel", UnityWebRequest.kHttpVerbPOST);
            cancel.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                new CancelRequest { campaign_id = campaign, request_id = requestId })));
            cancel.downloadHandler = new BoundedDownloadHandler(1024);
            cancel.timeout = TimeoutSeconds;
            cancel.redirectLimit = 0;
            cancel.SetRequestHeader("Content-Type", "application/json");
            try { cancel.SendWebRequest().completed += ignored => cancel.Dispose(); }
            catch (Exception) { cancel.Dispose(); }
        }

        private IEnumerator PostJson(string route, string json, Action<TransportResult> completed)
        {
            if (!relayEnabled || !isActiveAndEnabled)
            {
                completed(new TransportResult { Error = "offline" });
                yield break;
            }

            byte[] body = Encoding.UTF8.GetBytes(json);
            if (body.Length > MaxRequestBytes)
            {
                completed(new TransportResult { Error = "request_too_large" });
                yield break;
            }

            int generation = cancellationGeneration;
            var download = new BoundedDownloadHandler(MaxResponseBytes);
            var request = new UnityWebRequest(RelayOrigin + route, UnityWebRequest.kHttpVerbPOST);
            var item = new PendingRequest(request);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = download;
            request.timeout = TimeoutSeconds;
            request.redirectLimit = 0;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            pending.Add(item);

            UnityWebRequestAsyncOperation operation = null;
            bool started = false;
            try
            {
                operation = request.SendWebRequest();
                started = true;
            }
            catch (Exception)
            {
                // Never show an exception body: relay output is untrusted and may be large.
            }

            var result = new TransportResult();
            float startedAt = Time.realtimeSinceStartup;
            try
            {
                if (!started)
                    result.Error = "connection_failed";
                else
                {
                    while (!item.Disposed && generation == cancellationGeneration && !operation.isDone)
                    {
                        if (Time.realtimeSinceStartup - startedAt >= TimeoutSeconds)
                        {
                            request.Abort();
                            result.Error = "timeout";
                            break;
                        }
                        yield return null;
                    }

                    if (item.Disposed || generation != cancellationGeneration)
                        result.Error = "cancelled";
                    else if (download.ExceededLimit)
                        result.Error = "response_too_large";
                    else if (string.IsNullOrEmpty(result.Error))
                    {
                        result.HttpStatus = request.responseCode;
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            ErrorReply error = Parse<ErrorReply>(download.ReadText());
                            result.Error = error != null && ValidId(error.error) ? error.error : "relay_unavailable";
                        }
                        else
                            result.Body = download.ReadText();
                    }
                }
            }
            finally
            {
                pending.Remove(item);
                item.Dispose();
            }

            LastStatus = string.IsNullOrEmpty(result.Error) ? "received" : result.Error;
            completed(result);
        }

        private sealed class TransportResult
        {
            public string Body;
            public string Error;
            public long HttpStatus;
        }

        private sealed class PendingRequest
        {
            private readonly UnityWebRequest request;
            public bool Disposed { get; private set; }
            public PendingRequest(UnityWebRequest value) { request = value; }
            public void AbortAndDispose()
            {
                if (Disposed) return;
                request.Abort();
                Dispose();
            }
            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                request.Dispose();
            }
        }

        private sealed class BoundedDownloadHandler : DownloadHandlerScript
        {
            private readonly byte[] bytes;
            private int count;
            public bool ExceededLimit { get; private set; }
            public BoundedDownloadHandler(int limit) : base(new byte[4096])
            {
                bytes = new byte[limit];
            }
            protected override void ReceiveContentLengthHeader(ulong contentLength)
            {
                if (contentLength > (ulong)bytes.Length) ExceededLimit = true;
            }
            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength < 0 || dataLength > data.Length) return false;
                if (ExceededLimit || dataLength > bytes.Length - count)
                {
                    ExceededLimit = true;
                    return false;
                }
                Buffer.BlockCopy(data, 0, bytes, count, dataLength);
                count += dataLength;
                return true;
            }
            protected override byte[] GetData()
            {
                var result = new byte[count];
                Buffer.BlockCopy(bytes, 0, result, 0, count);
                return result;
            }
            public string ReadText() { return Encoding.UTF8.GetString(bytes, 0, count); }
        }
    }
}
