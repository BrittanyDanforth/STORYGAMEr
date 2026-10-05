#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SignalDebt.Editor
{
    /// <summary>
    /// Exercises the current compiled provider's pure protocol helpers in Unity.
    /// Does not create a provider component, look up credentials, or contact any service.
    /// </summary>
    public static class SignalDebtAiProviderChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int assertions, groups;
            public string unity_version, provider_source_sha256, checks_source_sha256, failure;
            public bool actual_unity_runtime = true;
            public bool credential_lookup_performed = false;
            public int provider_network_calls = 0;
            public bool transport_or_live_model_tested = false;
            public string scope = "Current provider payload construction and strict response validation with synthetic protocol fixtures. No live credentials, HTTP transport, cancellation, quota accounting, or NPC/gameplay acceptance exercised.";
            public string[] passed_assertions;
        }

        [Serializable] private sealed class Reasoning { public string effort; }
        [Serializable] private sealed class EnumRule { public string type; public string[] @enum; }
        [Serializable] private sealed class ArrayRule { public string type; public EnumRule items; }
        [Serializable] private sealed class Properties
        {
            public EnumRule speaker, text, proposed_intent;
            public ArrayRule used_fact_ids;
        }
        [Serializable] private sealed class Schema
        {
            public string type;
            public bool additionalProperties;
            public Properties properties;
            public string[] required;
        }
        [Serializable] private sealed class Format
        {
            public string type, name;
            public bool strict;
            public Schema schema;
        }
        [Serializable] private sealed class TextFormat { public Format format; }
        [Serializable] private sealed class Payload
        {
            public string model, instructions, input;
            public bool store;
            public int max_output_tokens;
            public Reasoning reasoning;
            public TextFormat text;
        }
        [Serializable] private sealed class Fact
        {
            public string fact_id, description, source, status;
            public int world_revision;
            public bool commitment;
        }
        [Serializable] private sealed class Input
        {
            public string npc_id, player_message;
            public SignalDebtNpcClient.NpcContext context;
            public Fact[] facts;
            public SignalDebtAiProvider.ChatLine[] history;
            public string[] allowed_intents;
        }
        [Serializable] private sealed class ModelDialogue
        {
            public string speaker = "clerk", text = "I saw your receipt.", proposed_intent = "continue";
            public string[] used_fact_ids = { "receipt" };
        }
        [Serializable] private sealed class Content
        {
            public string type = "output_text", text, refusal;
        }
        [Serializable] private sealed class Output
        {
            public string type = "message", role = "assistant", status = "completed";
            public Content[] content;
        }
        [Serializable] private sealed class Envelope
        {
            public string status = "completed";
            public Output[] output;
        }

        private static int assertions, groups;
        private static readonly List<string> passedAssertions = new List<string>();

        [MenuItem("Signal Debt/Validation/Run AI Provider Checks")]
        public static void RunMenu()
        {
            Report report = Execute();
            if (!report.passed) throw new InvalidOperationException(report.failure);
        }

        // -executeMethod SignalDebt.Editor.SignalDebtAiProviderChecks.RunBatch
        // -signalDebtAiProviderReport /absolute/report.json
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
                report.provider_source_sha256 = Hash(Path.Combine(Application.dataPath, "Scripts/SignalDebtAiProvider.cs"));
                report.checks_source_sha256 = Hash(Path.Combine(Application.dataPath, "Editor/SignalDebtAiProviderChecks.cs"));
                PayloadContract();
                PayloadBounds();
                ValidResponses();
                InvalidDialogue();
                InvalidEnvelopes();
                StrictJsonFailures();
                Check(assertions >= 21 && groups == 6, "all six groups executed at least 21 assertions");
                report.passed = true;
            }
            catch (Exception error) { report.failure = error.ToString(); }
            report.assertions = assertions;
            report.groups = groups;
            report.passed_assertions = passedAssertions.ToArray();
            try
            {
                string path = ReportPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true) + "\n");
                Debug.Log((report.passed ? "PASS" : "FAIL") + ": " + report.assertions +
                    " AI provider assertions in Unity " + report.unity_version + ". Report: " + path);
            }
            catch (Exception error)
            {
                report.passed = false;
                report.failure += "\nCould not write the provider-check report: " + error.Message;
            }
            if (!report.passed) Debug.LogError(report.failure);
            return report;
        }

        private static string ReportPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-signalDebtAiProviderReport")
                {
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException("-signalDebtAiProviderReport requires a file path.");
                    return Path.GetFullPath(args[i + 1]);
                }
            return Path.Combine(Application.temporaryCachePath, "SignalDebtAiProviderChecks.json");
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        private static void Check(bool condition, string description)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("FAIL: " + description);
            passedAssertions.Add(description);
        }

        private static SignalDebtNpcClient.DialogueRequest Request()
        {
            return new SignalDebtNpcClient.DialogueRequest
            {
                campaign_id = "test_campaign", request_id = "test_request", npc_id = "clerk", world_revision = 3,
                player_text = "Can you check my receipt?",
                npc_context = new SignalDebtNpcClient.NpcContext
                {
                    name = "Shop clerk", role = "clerk", disposition = "reserved",
                    goals = new[] { "Answer known shop questions" }, current_state = "At the counter."
                },
                allowed_intents = new[] { "continue", "ask_question" }
            };
        }

        private static SignalDebtNpcClient.SourceEvent[] Facts()
        {
            return new[] { new SignalDebtNpcClient.SourceEvent
            {
                event_id = "receipt", world_revision = 2, description = "A receipt was handed to the clerk.",
                source = "observed", status = "active", known_by = new[] { "clerk", "private_other_npc" },
                tags = new[] { "shop" }, commitment = false
            } };
        }

        private static string ReplyJson(ModelDialogue dialogue = null)
        { return JsonUtility.ToJson(dialogue ?? new ModelDialogue()); }

        private static string Wrap(string dialogue, string status = "completed")
        {
            return JsonUtility.ToJson(new Envelope
            {
                status = status,
                output = new[] { new Output { content = new[] { new Content { text = dialogue } } } }
            });
        }

        private static bool Parse(string body, out SignalDebtNpcClient.DialogueReply reply, out string status)
        { return SignalDebtAiProvider.TryParseResponse(body, Request(), Facts(), out reply, out status); }

        private static void RejectDialogue(string json, string description)
        {
            bool accepted = Parse(Wrap(json), out SignalDebtNpcClient.DialogueReply reply, out string status);
            Check(!accepted && reply == null && status == "invalid_response", description);
        }

        private static void RejectRequest(SignalDebtNpcClient.DialogueRequest request,
            SignalDebtNpcClient.SourceEvent[] facts, SignalDebtAiProvider.ChatLine[] history, string description)
        {
            bool accepted = SignalDebtAiProvider.TryBuildPayload(request, facts, history, out string payload, out string status);
            Check(!accepted && payload == null && status == "invalid_request", description);
        }

        private static void PayloadContract()
        {
            groups++;
            var request = Request();
            const string player = "A quote: \"hello\", a slash: \\, a newline:\n日本語 🚀";
            request.player_text = player;
            request.npc_context.current_state = "A sign reads \"Repair\\Shop\".\nWaiting.";
            var history = new[]
            {
                new SignalDebtAiProvider.ChatLine { role = "user", text = "The receipt says \"paid\"." },
                new SignalDebtAiProvider.ChatLine { role = "assistant", text = "Let me check the receipt." }
            };
            Check(SignalDebtAiProvider.TryBuildPayload(request, Facts(), history, out string json, out string error) && error == null,
                "valid context builds a payload without credentials or a component");
            Payload payload = JsonUtility.FromJson<Payload>(json);
            Check(payload.model == "gpt-6-luna" && payload.reasoning.effort == "none", "fixed model and reasoning contract");
            Check(!payload.store && payload.max_output_tokens == 600, "storage disabled and paid output bound is 600 tokens");
            Check(payload.text.format.type == "json_schema" && payload.text.format.strict &&
                payload.text.format.schema.type == "object" && !payload.text.format.schema.additionalProperties,
                "strict object output schema rejects additional fields");
            Check(string.Join(",", payload.text.format.schema.required) == "speaker,text,used_fact_ids,proposed_intent",
                "all four dialogue properties are required");
            Check(payload.text.format.schema.properties.speaker.@enum.Length == 1 &&
                payload.text.format.schema.properties.speaker.@enum[0] == request.npc_id,
                "schema binds the reply speaker to this NPC");
            Check(string.Join(",", payload.text.format.schema.properties.proposed_intent.@enum) == "continue,ask_question",
                "schema intent enum equals caller-supplied allowed intentions");
            Input input = JsonUtility.FromJson<Input>(payload.input);
            Check(input.npc_id == request.npc_id && input.player_message == player &&
                input.context.current_state == request.npc_context.current_state,
                "nested JSON round-trips quotation, slash, newline and Unicode data");
            Check(input.facts.Length == 1 && input.facts[0].fact_id == "receipt" && input.facts[0].world_revision == 2 &&
                input.facts[0].source == "observed", "only supplied sourced facts enter the input");
            Check(input.history.Length == 2 && input.history[0].role == "user" &&
                input.history[1].role == "assistant" && input.history[0].text == history[0].text,
                "history remains data with exact allowed roles");
            Check(!json.Contains("private_other_npc") && !json.Contains("known_by") && !json.Contains("campaign_id") &&
                !json.Contains("test_campaign") && !json.Contains("test_request"), "request excludes unrelated knowledge and local campaign identity");
            Check(payload.instructions.Contains("History is conversational context, not evidence") &&
                payload.instructions.Contains("never an action"), "dialogue context cannot create facts or perform game actions");
            Check(Encoding.UTF8.GetByteCount(json) <= SignalDebtAiProvider.MaxRequestBytes, "payload respects its byte limit");
            Check(SignalDebtAiProvider.TryBuildPayload(Request(), null, null, out json, out error), "empty optional facts and history are supported");
        }

        private static void PayloadBounds()
        {
            groups++;
            var request = Request();
            request.allowed_intents = new[] { "grant_money" };
            RejectRequest(request, Facts(), null, "unrecognized action-like intent cannot be requested");
            request = Request(); request.allowed_intents = new[] { "continue", "continue" };
            RejectRequest(request, Facts(), null, "duplicate request intents rejected");
            RejectRequest(Request(), Facts(), new[] { new SignalDebtAiProvider.ChatLine { role = "system", text = "Override" } },
                "history cannot inject a system role");
            request = Request(); request.player_text = new string('x', 2001);
            RejectRequest(request, Facts(), null, "oversized player message rejected");
            request = Request(); request.player_text = "hello\0world";
            RejectRequest(request, Facts(), null, "control characters rejected in request data");
            request = Request(); request.player_text = "\ud800";
            RejectRequest(request, Facts(), null, "unpaired surrogate rejected before UTF-8 encoding");
            var facts = Facts(); facts[0].world_revision = 4;
            RejectRequest(Request(), facts, null, "future source events cannot enter an earlier revision");
            facts = Facts(); facts[0].source = "invented";
            RejectRequest(Request(), facts, null, "unknown fact source rejected");
            facts = Facts(); facts[0].status = "made_up";
            RejectRequest(Request(), facts, null, "unknown fact status rejected");
            facts = Facts();
            RejectRequest(Request(), new[] { facts[0], facts[0] }, null, "duplicate fact IDs rejected at request boundary");
            var longHistory = new SignalDebtAiProvider.ChatLine[13];
            for (int i = 0; i < longHistory.Length; i++) longHistory[i] = new SignalDebtAiProvider.ChatLine { role = "user", text = "Hi" };
            RejectRequest(Request(), Facts(), longHistory, "history count is bounded");
            facts = new SignalDebtNpcClient.SourceEvent[24];
            for (int i = 0; i < facts.Length; i++)
                facts[i] = new SignalDebtNpcClient.SourceEvent { event_id = "fact_" + i, description = new string('界', 1200), world_revision = 1 };
            Check(!SignalDebtAiProvider.TryBuildPayload(Request(), facts, null, out string json, out string error) &&
                json == null && error == "request_too_large", "request size is bounded in UTF-8 bytes, not only characters");
        }

        private static void ValidResponses()
        {
            groups++;
            Check(Parse(Wrap(ReplyJson()), out SignalDebtNpcClient.DialogueReply reply, out string status) && status == null,
                "valid completed Responses message is accepted");
            Check(reply.speaker == "clerk" && reply.npc_id == "clerk" && reply.request_id == "test_request" &&
                reply.world_revision == 3 && !reply.stale && reply.source_mode == "model", "accepted reply metadata is bound to the local request");
            Check(reply.used_fact_ids.Length == 1 && reply.used_fact_ids[0] == "receipt", "validated fact IDs are preserved");
            Check(Parse(Wrap(ReplyJson(new ModelDialogue { text = "  Hello.  ", used_fact_ids = new string[0] })), out reply, out status) &&
                reply.text == "Hello.", "uncited plain dialogue is accepted and trimmed");
            var words = new string[90]; for (int i = 0; i < words.Length; i++) words[i] = "word";
            Check(Parse(Wrap(ReplyJson(new ModelDialogue { text = string.Join(" ", words) })), out reply, out status), "exactly 90 words accepted");
            Check(Parse(Wrap(ReplyJson(new ModelDialogue { text = new string('x', 1000) })), out reply, out status), "exactly 1000 characters accepted");
            string modelJson = ReplyJson();
            var split = new Envelope
            {
                output = new[]
                {
                    new Output { type = "reasoning", content = new Content[0] },
                    new Output { content = new[] { new Content { text = modelJson.Substring(0, 20) }, new Content { text = modelJson.Substring(20) } } }
                }
            };
            Check(Parse(JsonUtility.ToJson(split), out reply, out status), "reasoning is skipped and output_text fragments are combined");
        }

        private static void InvalidDialogue()
        {
            groups++;
            RejectDialogue(ReplyJson(new ModelDialogue { speaker = "other_npc" }), "another NPC cannot speak through this request");
            RejectDialogue(ReplyJson(new ModelDialogue { used_fact_ids = new[] { "new_invented_fact" } }), "model cannot cite an unsupplied new fact");
            RejectDialogue(ReplyJson(new ModelDialogue { used_fact_ids = new[] { "receipt", "receipt" } }), "duplicate model fact citations rejected");
            RejectDialogue(ReplyJson(new ModelDialogue { proposed_intent = "end_conversation" }), "globally known but caller-disallowed intent rejected");
            RejectDialogue(ReplyJson(new ModelDialogue { proposed_intent = "grant_money" }), "model cannot propose an arbitrary action");
            foreach (string text in new[] { "", " \n\t", "<b>Hello</b>", "`code`", "**bold**", "[link](target)",
                "See https://example.invalid", "hello\u202eworld" })
                RejectDialogue(ReplyJson(new ModelDialogue { text = text }), "empty, control, markup or URL dialogue rejected: case " + assertions);
            // JsonUtility truncates embedded NULs when serializing a string; construct
            // the escaped wire representation so this fixture tests the provider parser.
            RejectDialogue("{\"speaker\":\"clerk\",\"text\":\"hello\\u0000world\",\"used_fact_ids\":[],\"proposed_intent\":\"continue\"}",
                "escaped NUL is rejected in actual model output");
            RejectDialogue(ReplyJson(new ModelDialogue { text = new string('x', 1001) }), "1001 characters rejected");
            var words = new string[91]; for (int i = 0; i < words.Length; i++) words[i] = "word";
            RejectDialogue(ReplyJson(new ModelDialogue { text = string.Join(" ", words) }), "91 words rejected");
            RejectDialogue(ReplyJson().TrimEnd('}') + ",\"action\":\"give_item\"}", "additional output action property rejected");
            RejectDialogue("{\"speaker\":\"clerk\",\"text\":\"Hi\",\"proposed_intent\":\"continue\"}", "missing fact array rejected");
            RejectDialogue("{\"speaker\":\"clerk\",\"text\":42,\"used_fact_ids\":[],\"proposed_intent\":\"continue\"}", "numeric text is not coerced to dialogue");
            RejectDialogue("{\"speaker\":\"clerk\",\"text\":\"Hi\",\"used_fact_ids\":[true],\"proposed_intent\":\"continue\"}", "non-string fact citation rejected");
            Check(!SignalDebtAiProvider.TryParseResponse(Wrap(ReplyJson()), Request(), new SignalDebtNpcClient.SourceEvent[0],
                out SignalDebtNpcClient.DialogueReply reply, out string status) && reply == null,
                "facts from another request are not implicitly trusted");
        }

        private static void InvalidEnvelopes()
        {
            groups++;
            Check(!Parse(Wrap(ReplyJson(), "incomplete"), out SignalDebtNpcClient.DialogueReply reply, out string status) &&
                status == "incomplete_response" && reply == null, "incomplete output never becomes accepted dialogue");
            var refusal = new Envelope { output = new[] { new Output { content = new[] { new Content { type = "refusal", refusal = "Synthetic refusal" } } } } };
            Check(!Parse(JsonUtility.ToJson(refusal), out reply, out status) && status == "provider_refusal", "provider refusal has a bounded status code");
            var toolCall = new Envelope { output = new[] { new Output { type = "function_call", content = new Content[0] } } };
            Check(!Parse(JsonUtility.ToJson(toolCall), out reply, out status), "unexpected tool call output rejected");
            var wrongRole = new Envelope { output = new[] { new Output { role = "user", content = new[] { new Content { text = ReplyJson() } } } } };
            Check(!Parse(JsonUtility.ToJson(wrongRole), out reply, out status), "non-assistant output message rejected");
            var pending = new Envelope { output = new[] { new Output { status = "in_progress", content = new[] { new Content { text = ReplyJson() } } } } };
            Check(!Parse(JsonUtility.ToJson(pending), out reply, out status) && status == "incomplete_response", "unfinished nested message rejected");
            Check(!Parse("{\"status\":\"completed\",\"output\":[]}", out reply, out status), "empty output rejected");
            Check(!Parse(Wrap(ReplyJson()).TrimEnd('}') + ",\"error\":{\"message\":\"Synthetic provider error\"}}", out reply, out status),
                "an error-bearing envelope cannot present valid-looking dialogue");
            Check(!Parse(new string('x', SignalDebtAiProvider.MaxResponseBytes + 1), out reply, out status), "oversized response characters rejected");
            string unicodeBody = Wrap(ReplyJson(new ModelDialogue { text = new string('界', 24000) }));
            Check(unicodeBody.Length < SignalDebtAiProvider.MaxResponseBytes && Encoding.UTF8.GetByteCount(unicodeBody) > SignalDebtAiProvider.MaxResponseBytes &&
                !Parse(unicodeBody, out reply, out status), "oversized response UTF-8 bytes rejected before JSON processing");
        }

        private static void StrictJsonFailures()
        {
            groups++;
            RejectDialogue("{\"speaker\":\"clerk\",\"speaker\":\"clerk\",\"text\":\"Hi\",\"used_fact_ids\":[],\"proposed_intent\":\"continue\"}",
                "duplicate output JSON keys rejected even if values agree");
            RejectDialogue("{\"speaker\":\"clerk\",\"text\":\"Hi\",\"used_fact_ids\":[],\"proposed_intent\":\"continue\",}", "trailing object comma rejected");
            RejectDialogue(ReplyJson() + "[]", "trailing JSON after dialogue rejected");
            RejectDialogue("{\"speaker\":\"clerk\",\"text\":\"bad\\q\",\"used_fact_ids\":[],\"proposed_intent\":\"continue\"}", "unknown JSON escape rejected");
            RejectDialogue("{\"speaker\":\"clerk\",\"text\":\"\\ud800\",\"used_fact_ids\":[],\"proposed_intent\":\"continue\"}", "unpaired escaped surrogate rejected");
            RejectDialogue("{\"speaker\":\"clerk\",\"text\":\"Hi\",\"used_fact_ids\":[null],\"proposed_intent\":\"continue\"}", "null citation rejected");
            Check(!Parse(Wrap(ReplyJson()) + "{}", out SignalDebtNpcClient.DialogueReply reply, out string status), "trailing JSON after provider envelope rejected");
            Check(!Parse("{\"status\":\"completed\",\"status\":\"completed\",\"output\":[]}", out reply, out status), "duplicate envelope keys rejected");
            string nested = "0";
            for (int i = 0; i < 25; i++) nested = "[" + nested + "]";
            Check(!Parse("{\"status\":\"completed\",\"output\":" + nested + "}", out reply, out status), "excess JSON nesting rejected without recursion overflow");
        }
    }
}
#endif
