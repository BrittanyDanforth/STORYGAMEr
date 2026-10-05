#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SignalDebt.Editor
{
    // Actual JsonUtility + temporary filesystem tests. No player data, API key,
    // scene, network call or fabricated Unity serialization is involved.
    public static class SignalDebtSaveLibraryChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int assertions, cases;
            public string unity_version, failure;
            public bool genuine_unity_json = true, real_temporary_files = true;
            public bool unix_symlink_cases_executed;
            public string scope = "Immutable manual checkpoints, independent campaign activation, safety snapshots, archive migration, stale/missing/invalid selections and filesystem failure preservation. UI/native interaction is separate.";
        }

        // Real earlier formats: no proof fields, and no device before schema3.
        [Serializable] private class LegacyV1
        {
            public int schemaVersion = 1;
            public string saveId = "legacy-library-campaign";
            public int revision, balance = 80, rentDue = 450, day = 1, rentDeadlineDay = 4;
            public bool extensionUsed, jobAccepted, serviceInspected, serviceConnected, serviceTestPassed, jobPaid;
            public List<OldEvent> ledger = new List<OldEvent>();
            public List<string> clientMemories = new List<string>();
        }
        [Serializable] private class LegacyV2 : LegacyV1
        { public List<SignalDebtPurchase> shopPurchases = new List<SignalDebtPurchase>(); }
        [Serializable] private sealed class LegacyV3 : LegacyV2
        { public SignalDebtClientDevice clientDevice; }
        [Serializable] private sealed class OldEvent
        {
            public string id, type, summary, utc, actorId, deviceId, recordId;
            public int amount, day, worldRevision, deviceRevision;
        }
        private sealed class FileSnapshot { public byte[] bytes; public long ticks; }
        private static int assertions, cases;
        private static string root;
        private static bool symlinks;

        [MenuItem("Signal Debt/Validation/Run Save Library Checks")]
        public static void Run()
        { Report report = Execute(); if (!report.passed) throw new InvalidOperationException(report.failure); }
        public static void RunBatch()
        { Report report = Execute(); EditorApplication.Exit(report.passed ? 0 : 1); }

        private static Report Execute()
        {
            assertions = cases = 0; symlinks = false;
            root = Path.Combine(Path.GetTempPath(), "SignalDebtSaveLibraryChecks-" + Guid.NewGuid().ToString("N"));
            var report = new Report { unity_version = Application.unityVersion, failure = "" };
            try
            {
                Directory.CreateDirectory(root);
                EmptyAndPagination();
                DetachedCheckpointAndIndependentCampaigns();
                EarlierAndPaidCheckpoints();
                PendingOwnerWorkRoundTrip();
                InvalidAndStaleSelections();
                ActiveBarriers();
                WriteFailures();
                ArchiveMigrationAndRecovery();
                EarlierInstallationRemainsVisible();
                SelectedSnapshotDate();
                TraversalAndLinks();
                report.passed = true;
            }
            catch (Exception exception) { report.failure = exception.ToString(); }
            finally
            {
                report.assertions = assertions; report.cases = cases;
                report.unix_symlink_cases_executed = symlinks;
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (Exception exception) { report.passed = false; report.failure += "\nCleanup failed: " + exception.Message; }
            }
            try
            {
                string path = ReportPath(); Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Debug.Log((report.passed ? "PASS" : "FAIL") + ": " + report.assertions
                    + " save-library assertions across " + report.cases + " filesystem cases. Report: " + path);
            }
            catch (Exception exception) { report.passed = false; report.failure += "\nReport write failed: " + exception.Message; }
            if (!report.passed) Debug.LogError(report.failure);
            return report;
        }

        private static string ReportPath()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
                if (arguments[index] == "-signalDebtSaveLibraryReport")
                {
                    if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException("-signalDebtSaveLibraryReport requires a path.");
                    return Path.GetFullPath(arguments[index + 1]);
                }
            return Path.Combine(Path.GetTempPath(), "SignalDebt-save-library-checks.json");
        }

        private static string Fixture(string name)
        { cases++; string path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
        private static void Check(bool value, string description)
        { assertions++; if (!value) throw new InvalidOperationException("FAIL: " + description); }
        private static string Json(SignalDebtSaveData state) { return JsonUtility.ToJson(state); }
        private static SignalDebtSaveStore Store(string directory)
        { return new SignalDebtSaveStore(Path.Combine(directory, "Saves")); }
        private static SignalDebtSaveLibrary.Entry Checkpoint(SignalDebtSaveLibrary library, SignalDebtSaveData state, string label)
        {
            Check(library.TryCreateCheckpoint(state, label, out SignalDebtSaveLibrary.Entry entry, out string error), "checkpoint created: " + error);
            Check(entry != null && entry.CanLoad && entry.Kind == SignalDebtSaveLibrary.EntryKind.Checkpoint, "checkpoint returns immutable available metadata");
            return entry;
        }

        private static void EmptyAndPagination()
        {
            string parent = Fixture("empty-pagination"); string directory = Path.Combine(parent, "not-created");
            var library = new SignalDebtSaveLibrary(directory);
            Check(library.List(0, 3).Total == 0 && !Directory.Exists(directory), "listing an absent library creates no campaign or directories");
            Check(!library.TryCreateCheckpoint(null, "", out _, out _) && !Directory.Exists(directory), "null checkpoint writes nothing");
            var state = new SignalDebtSaveData(); Check(Store(directory).Save(state), "current campaign exists for paging");
            for (int index = 0; index < 9; index++) { Checkpoint(library, state, "Day " + state.day); state.AdvanceDay(); }
            var ids = new HashSet<string>(); int pages = 0;
            for (int index = 0; index < 4; index++)
            {
                SignalDebtSaveLibrary.Page page = library.List(index, 3); pages++;
                Check(page.Error == "" && page.Total == 10 && page.Entries.Count == (index == 3 ? 1 : 3), "all checkpoints remain reachable across pages");
                foreach (SignalDebtSaveLibrary.Entry entry in page.Entries) Check(ids.Add(entry.Id), "page entries never duplicate");
            }
            Check(ids.Count == 10 && pages == 4 && library.List(0, 3).Entries[0].Kind == SignalDebtSaveLibrary.EntryKind.Current, "current campaign stays first and no entries are truncated");
            Check(library.List(int.MaxValue, 100).Entries.Count == 0 && library.List(-1, 0).Entries.Count == 1, "invalid paging cannot overflow or hide the initial entry");
            Dictionary<string, FileSnapshot> before = Capture(directory);
            library.List(1, 3); library.List(0, 100);
            Check(Same(directory, before), "listing never rewrites saves, metadata or timestamps");
        }

        private static void DetachedCheckpointAndIndependentCampaigns()
        {
            string directory = Fixture("independent-campaigns"); var library = new SignalDebtSaveLibrary(directory);
            var first = new SignalDebtSaveData(); first.AdvanceDay();
            string frozen = Json(first);
            SignalDebtSaveLibrary.Entry entry = Checkpoint(library, first, "  Before\n<b>rent</b>  ");
            Check(entry.Label.IndexOf('\n') < 0 && entry.Label.IndexOf('<') < 0 && entry.Day == 2, "card label is a bounded single line without markup");
            first.AdvanceDay(); first.clientMemories.Add("local later change");
            Check(entry.Day == 2, "later live-state changes cannot mutate metadata");
            var second = new SignalDebtSaveData(); second.ExtendRentDeadline();
            Check(second.saveId != first.saveId && Store(directory).Save(second), "separate current campaign has an independent identity and ledger");
            WriteSettingsSentinels(directory); Dictionary<string, FileSnapshot> settings = CaptureSettings(directory);
            string liveBefore = Json(second); Dictionary<string, FileSnapshot> checkpoints = Capture(Path.Combine(directory, "Checkpoints"));
            Check(library.TryActivate(entry, second, out SignalDebtSaveData activated, out string error), "independent selected campaign activates: " + error);
            Check(Json(activated) == frozen && Json(Store(directory).Load()) == frozen, "activation restores the exact selected ledger without merging current events");
            Check(Json(second) == liveBefore && !ReferenceEquals(second, activated), "service publishes only a detached result; live object remains caller-owned");
            Check(SameSettings(directory, settings) && ContainsUnchanged(Path.Combine(directory, "Checkpoints"), checkpoints), "settings and original checkpoint bytes/timestamps are untouched");
            SignalDebtSaveLibrary.Entry safety = FindByLabel(library, "Before loading another save");
            Check(safety != null && library.TryActivate(safety, activated, out SignalDebtSaveData restored, out error) && Json(restored) == liveBefore, "automatic safety checkpoint restores the displaced independent campaign");
        }

        private static void EarlierAndPaidCheckpoints()
        {
            string directory = Fixture("earlier-paid"); var library = new SignalDebtSaveLibrary(directory);
            var live = new SignalDebtSaveData();
            SignalDebtSaveLibrary.Entry earlier = Checkpoint(library, live, "Before first repair");
            CompleteRepair(live);
            Check(live.AcceptProofRecovery() != null && live.BeginClientSession() != null
                && live.RestoreProofRevision(SignalDebtProofDocument.RevisionId(2)) != null
                && live.RequestProofCheck() != null && live.StepProofTask() != null && live.ClaimProofPayment() != null && live.Validate(), "fixture completes the actual proof export and payment");
            SignalDebtSaveLibrary.Entry paid = Checkpoint(library, live, "Both jobs paid");
            Check(Store(directory).Save(live), "paid current campaign saves");
            string paidJson = Json(live);
            Check(library.TryActivate(earlier, live, out SignalDebtSaveData active, out _), "earlier same-campaign checkpoint activates");
            Check(active.saveId == live.saveId && active.balance == 80 && active.ledger.Count == 0 && !active.jobPaid && !active.proofPaid,
                "same identity rewinds exactly without retaining later money or receipts");
            Check(active.ClaimRepairPayment() == null && active.ClaimProofPayment() == null, "rewinding cannot claim payment before doing the work");
            Check(library.TryActivate(paid, active, out SignalDebtSaveData paidAgain, out _) && Json(paidAgain) == paidJson, "paid checkpoint restores its own exact history");
            Dictionary<string, FileSnapshot> before = Capture(Path.Combine(directory, "Saves"));
            Check(!paidAgain.TryCommitChange(s => s.ClaimRepairPayment(), Store(directory).Save, out _, out _)
                && !paidAgain.TryCommitChange(s => s.ClaimProofPayment(), Store(directory).Save, out _, out _)
                && paidAgain.balance == 360 && Json(paidAgain) == paidJson && Same(Path.Combine(directory, "Saves"), before),
                "restored paid campaign cannot pay either job twice or emit another receipt");
        }

        private static void CompleteRepair(SignalDebtSaveData state)
        {
            Check(state.AcceptRepair() != null && state.BeginClientSession() != null && state.InspectRepair() != null
                && state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId) != null
                && state.TestRepair() != null && state.StepClientTask() != null && state.ClaimRepairPayment() != null
                && state.EndClientSession() != null && state.Validate(), "fixture completes actual device work and one payment");
        }

        private static void PendingOwnerWorkRoundTrip()
        {
            string directory = Fixture("pending-owner-work"); var library = new SignalDebtSaveLibrary(directory);
            var state = new SignalDebtSaveData(); CompleteRepair(state);
            Check(state.AcceptProofRecovery() != null && state.BeginClientSession() != null
                && state.RestoreProofRevision(SignalDebtProofDocument.RevisionId(1)) != null
                && state.RequestProofCheck() != null && state.Validate(), "a wrong source has a genuine queued owner task");
            string pending = Json(state); SignalDebtSaveLibrary.Entry entry = Checkpoint(library, state, "Owner check pending");
            Check(library.TryActivate(entry, null, out SignalDebtSaveData restored, out _) && Json(restored) == pending,
                "loading from main menu with no current campaign retains the exact queued task");
            Check(restored.StepProofTask() != null && restored.proofDocument.LastOwnerRead().result == SignalDebtProofResult.DateMismatch
                && restored.proofDocument.LatestExport() == null && !restored.CanClaimProofPayment, "restored task reads the selected wrong revision rather than inventing success");
            Check(Json(state) == pending, "executing loaded task cannot mutate checkpoint source or caller state");
        }

        private static void InvalidAndStaleSelections()
        {
            foreach (string alteration in new[] { "missing-directory", "missing-family", "changed-primary", "future-backup", "changed-label", "malformed-metadata" })
            {
                string directory = Fixture(alteration); var library = new SignalDebtSaveLibrary(directory);
                var active = new SignalDebtSaveData(); Check(Store(directory).Save(active), "active save created");
                var selectedState = new SignalDebtSaveData(); SignalDebtSaveLibrary.Entry entry = Checkpoint(library, selectedState, "Target");
                string selected = CheckpointDirectory(directory, entry); var selectedStore = new SignalDebtSaveStore(selected);
                if (alteration == "missing-directory") Directory.Delete(selected, true);
                else if (alteration == "missing-family") File.Delete(selectedStore.SavePath);
                else if (alteration == "future-backup") File.WriteAllText(selectedStore.SavePath + ".bak", "{\"schemaVersion\":999}");
                else if (alteration == "changed-label") File.WriteAllText(Path.Combine(selected, "checkpoint.json"),
                    "{\"schemaVersion\":1,\"label\":\"Changed after confirmation\",\"createdUtc\":\"2025-01-01T00:00:00Z\"}");
                else if (alteration == "malformed-metadata") File.WriteAllText(Path.Combine(selected, "checkpoint.json"), "broken metadata");
                else { selectedState.AdvanceDay(); File.WriteAllText(selectedStore.SavePath, Json(selectedState)); }
                Dictionary<string, FileSnapshot> before = Capture(directory); string current = Json(active);
                Check(!library.TryActivate(entry, active, out SignalDebtSaveData result, out _) && result == null,
                    "stale/missing/future selection cannot execute from an old confirmation");
                Check(Same(directory, before) && Json(active) == current, "rejected selection makes no files, safety snapshot or live-state changes");
                if (alteration == "missing-directory") Check(!Directory.Exists(selected), "missing checkpoint is never recreated as a fresh campaign");
            }
            foreach (string payload in new[] { "broken-json", "{\"schemaVersion\":999}" })
            {
                string directory = Fixture(payload == "broken-json" ? "invalid-archive" : "future-archive");
                string archive = Path.Combine(directory, "Backups", "Old campaign"); Directory.CreateDirectory(archive);
                File.WriteAllText(new SignalDebtSaveStore(archive).SavePath, payload);
                var library = new SignalDebtSaveLibrary(directory); Dictionary<string, FileSnapshot> before = Capture(directory);
                SignalDebtSaveLibrary.Page page = library.List(0, 3);
                Check(page.Total == 1 && !page.Entries[0].CanLoad && page.Entries[0].Status.Length > 0, "invalid/future archive remains visible with a disabled load and reason");
                Check(!library.TryActivate(page.Entries[0], null, out _, out _) && Same(directory, before), "disabled archive cannot be activated or repaired implicitly");
            }
        }

        private static void ActiveBarriers()
        {
            foreach (string barrier in new[] { "corrupt", "future-primary", "future-backup", "future-temp", "live-disk-mismatch" })
            {
                string directory = Fixture("active-" + barrier); var library = new SignalDebtSaveLibrary(directory);
                var current = new SignalDebtSaveData(); var store = Store(directory); Check(store.Save(current), "barrier fixture current save exists");
                SignalDebtSaveLibrary.Entry target = Checkpoint(library, new SignalDebtSaveData(), "Independent target");
                if (barrier == "corrupt") File.WriteAllText(store.SavePath, "unreadable");
                else if (barrier == "live-disk-mismatch") { var changed = current.Copy(); changed.AdvanceDay(); Check(store.Save(changed), "external current state advanced"); }
                else File.WriteAllText(store.SavePath + (barrier == "future-backup" ? ".bak" : barrier == "future-temp" ? ".tmp" : ""), "{\"schemaVersion\":999}");
                Dictionary<string, FileSnapshot> before = Capture(directory); string live = Json(current);
                Check(!library.TryActivate(target, current, out _, out _), "active preservation barrier rejects campaign replacement");
                Check(Same(directory, before) && Json(current) == live, "active barrier keeps every original byte/timestamp and live state unchanged");
            }
        }

        private static void WriteFailures()
        {
            string directory = Fixture("checkpoint-parent-file"); var library = new SignalDebtSaveLibrary(directory);
            File.WriteAllText(Path.Combine(directory, "Checkpoints"), "keep this obstruction");
            Dictionary<string, FileSnapshot> before = Capture(directory);
            Check(!library.TryCreateCheckpoint(new SignalDebtSaveData(), "Unwritable", out _, out _) && Same(directory, before), "checkpoint staging failure cannot change surrounding files");

            directory = Fixture("activation-checkpoint-failure"); library = new SignalDebtSaveLibrary(directory);
            var current = new SignalDebtSaveData(); Check(Store(directory).Save(current), "active exists before safety failure");
            string archive = Path.Combine(directory, "Backups", "Campaign-test"); var archiveStore = new SignalDebtSaveStore(archive);
            Check(archiveStore.Save(new SignalDebtSaveData()), "archive target exists");
            File.WriteAllText(Path.Combine(directory, "Checkpoints"), "block safety checkpoint");
            SignalDebtSaveLibrary.Entry target = FindKind(library, SignalDebtSaveLibrary.EntryKind.Archive);
            before = Capture(directory);
            Check(!library.TryActivate(target, current, out _, out _) && Same(directory, before), "activation stops before replacing current if safety creation fails");

            directory = Fixture("active-publication-failure"); library = new SignalDebtSaveLibrary(directory);
            current = new SignalDebtSaveData(); Check(Store(directory).Save(current), "active exists before publication failure");
            target = Checkpoint(library, new SignalDebtSaveData(), "Original target");
            Directory.CreateDirectory(Store(directory).SavePath + ".bak"); WriteSettingsSentinels(directory);
            Dictionary<string, FileSnapshot> currentFiles = Capture(Path.Combine(directory, "Saves"));
            Dictionary<string, FileSnapshot> targetFiles = Capture(CheckpointDirectory(directory, target));
            Dictionary<string, FileSnapshot> settings = CaptureSettings(directory); string live = Json(current);
            int originalCount = library.List(0, 100).Total;
            Check(!library.TryActivate(target, current, out SignalDebtSaveData activated, out string error) && activated == null,
                "actual atomic replacement failure is returned to caller");
            Check(error.Contains("safety checkpoint") && library.List(0, 100).Total == originalCount + 1, "already-created safety checkpoint remains discoverable and is reported honestly");
            Check(Same(Path.Combine(directory, "Saves"), currentFiles) && Same(CheckpointDirectory(directory, target), targetFiles)
                && SameSettings(directory, settings) && Json(current) == live, "failed replacement preserves active/selected/settings bytes and live state");
            Check(Directory.GetFiles(Path.Combine(directory, "Saves"), "*.stage-*").Length == 0
                && Directory.GetDirectories(Path.Combine(directory, "Checkpoints"), ".creating-*").Length == 0, "failed publication leaves no recoverable candidate or abandoned checkpoint staging");
            Check(Json(Store(directory).Load()) == live, "current campaign remains loadable after failed activation");
        }

        private static void ArchiveMigrationAndRecovery()
        {
            foreach (int version in new[] { 1, 2, 3 })
            {
                string directory = Fixture("legacy-archive-v" + version);
                string archive = Path.Combine(directory, "Backups", "Campaign-legacy-" + version); Directory.CreateDirectory(archive);
                var archiveStore = new SignalDebtSaveStore(archive);
                string path = version == 1 ? archiveStore.LegacySavePath : version == 2 ? archiveStore.VersionTwoSavePath : archiveStore.PreviousSavePath;
                string json;
                if (version == 1) json = JsonUtility.ToJson(new LegacyV1());
                else if (version == 2) json = JsonUtility.ToJson(new LegacyV2 { schemaVersion = 2 });
                else
                {
                    var repaired = new SignalDebtSaveData(); CompleteRepair(repaired);
                    var previous = new LegacyV3 {
                        schemaVersion = 3, saveId = repaired.saveId, revision = repaired.revision, balance = repaired.balance,
                        jobAccepted = true, serviceInspected = true, serviceConnected = true, serviceTestPassed = true, jobPaid = true,
                        clientMemories = new List<string>(repaired.clientMemories), clientDevice = repaired.clientDevice.Copy()
                    };
                    foreach (SignalDebtLedgerEvent item in repaired.ledger) previous.ledger.Add(new OldEvent {
                        id = item.id, type = item.type, summary = item.summary, amount = item.amount, day = item.day,
                        worldRevision = item.worldRevision, utc = item.utc, actorId = item.actorId, deviceId = item.deviceId,
                        recordId = item.recordId, deviceRevision = item.deviceRevision
                    });
                    json = JsonUtility.ToJson(previous);
                }
                File.WriteAllText(path, json); File.SetLastWriteTimeUtc(path, new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc));
                Dictionary<string, FileSnapshot> originals = Capture(archive);
                var library = new SignalDebtSaveLibrary(directory); SignalDebtSaveLibrary.Entry entry = FindKind(library, SignalDebtSaveLibrary.EntryKind.Archive);
                Check(entry != null && entry.CanLoad && Same(archive, originals), "legacy archive listing migrates only in memory");
                Check(library.TryActivate(entry, null, out SignalDebtSaveData restored, out _) && restored.Validate()
                    && restored.schemaVersion == 4 && !restored.proofAccepted && !restored.proofPaid, "legacy archive activates current schema without invented proof work");
                Check(Same(archive, originals) && File.Exists(Store(directory).SavePath), "activation preserves all original legacy archive bytes and mtimes");
                if (version == 3) Check(restored.jobPaid && restored.balance == 200
                    && restored.clientDevice.OwnerHasCompletedFirstOrder && restored.ClaimRepairPayment() == null,
                    "v3 archive keeps real persistent device work and once-only repair receipt");
            }
            string recoveryDirectory = Fixture("archive-backup-recovery");
            string recoveryArchive = Path.Combine(recoveryDirectory, "Backups", "Interrupted campaign"); Directory.CreateDirectory(recoveryArchive);
            var recoveryStore = new SignalDebtSaveStore(recoveryArchive); var expected = new SignalDebtSaveData(); expected.AdvanceDay();
            File.WriteAllText(recoveryStore.SavePath, "corrupt primary"); File.WriteAllText(recoveryStore.SavePath + ".bak", Json(expected));
            var recoveryLibrary = new SignalDebtSaveLibrary(recoveryDirectory); Dictionary<string, FileSnapshot> recoveryBefore = Capture(recoveryArchive);
            SignalDebtSaveLibrary.Entry recoveredEntry = FindKind(recoveryLibrary, SignalDebtSaveLibrary.EntryKind.Archive);
            Check(recoveredEntry.CanLoad && recoveredEntry.Status.Contains("backup")
                && recoveryLibrary.TryActivate(recoveredEntry, null, out SignalDebtSaveData recovered, out _) && Json(recovered) == Json(expected),
                "archive uses SaveStore's valid backup recovery without resetting campaign identity");
            Check(Same(recoveryArchive, recoveryBefore), "recovering an archive never repairs or rewrites the archived originals");
        }

        private static void EarlierInstallationRemainsVisible()
        {
            string parent = Fixture("earlier-installation");
            string portable = Path.Combine(parent, "Portable", "UserData"), legacy = Path.Combine(parent, "Old account data");
            var oldState = new SignalDebtSaveData(); oldState.AdvanceDay();
            var oldStore = new SignalDebtSaveStore(legacy); Check(oldStore.Save(oldState), "earlier account campaign exists");
            File.WriteAllText(Path.Combine(legacy, "private-settings.json"), "PRIVATE NON-CAMPAIGN FIXTURE");
            Dictionary<string, FileSnapshot> original = Capture(legacy);
            var library = new SignalDebtSaveLibrary(portable, legacy);
            SignalDebtSaveLibrary.Entry entry = FindByLabel(library, "Earlier installation");
            Check(entry != null && entry.CanLoad && entry.Id == "earlier-installation" && !Directory.Exists(portable),
                "legacy-only installation is visible through one fixed read-only entry without creating portable saves");
            Check(new SignalDebtSaveLibrary(portable).List(0, 3).Total == 0, "unset legacy path never looks up actual account data");
            var fresh = new SignalDebtSaveData(); Check(Store(portable).Save(fresh), "New Game creates an independent portable campaign");
            Check(library.List(0, 3).Total == 2 && FindByLabel(library, "Earlier installation") != null,
                "new portable campaign does not hide the earlier installation");
            Check(library.TryActivate(entry, fresh, out SignalDebtSaveData loaded, out _) && Json(loaded) == Json(oldState),
                "earlier installation restores through the same validated activation contract");
            Check(Same(legacy, original) && !File.Exists(Path.Combine(portable, "private-settings.json")),
                "old campaign/private settings remain byte and timestamp identical; unrelated files are never imported");
        }

        private static void SelectedSnapshotDate()
        {
            string directory = Fixture("selected-date"); var library = new SignalDebtSaveLibrary(directory);
            var state = new SignalDebtSaveData(); var store = Store(directory); Check(store.Save(state), "date fixture primary saved");
            DateTime primaryDate = new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            DateTime backupDate = primaryDate.AddDays(-8), unrelatedDate = primaryDate.AddYears(1);
            File.SetLastWriteTimeUtc(store.SavePath, primaryDate);
            File.WriteAllText(store.SavePath + ".tmp", Json(new SignalDebtSaveData())); File.SetLastWriteTimeUtc(store.SavePath + ".tmp", unrelatedDate);
            File.WriteAllText(store.PreviousSavePath, "unrelated older family"); File.SetLastWriteTimeUtc(store.PreviousSavePath, unrelatedDate);
            Check(library.List(0, 3).Entries[0].SavedUtc == primaryDate, "primary card date ignores newer temporary/older-family mtimes");
            File.WriteAllText(store.SavePath + ".bak", Json(state)); File.SetLastWriteTimeUtc(store.SavePath + ".bak", backupDate);
            File.WriteAllText(store.SavePath, "corrupted later"); File.SetLastWriteTimeUtc(store.SavePath, unrelatedDate);
            Check(library.List(0, 3).Entries[0].SavedUtc == backupDate, "recovered card date belongs to the selected older backup, not corrupt primary");
            Check(store.Load() != null && store.LoadedPath == store.SavePath + ".bak", "SaveStore exposes exact selected recovery path");
            File.WriteAllText(store.SavePath, "{\"schemaVersion\":999}");
            Check(store.Load() == null && store.LoadedPath == null, "a later failed load clears earlier selected-path provenance");
        }

        private static void TraversalAndLinks()
        {
            string directory = Fixture("traversal"); var library = new SignalDebtSaveLibrary(directory);
            var current = new SignalDebtSaveData(); Check(Store(directory).Save(current), "traversal fixture current save exists");
            SignalDebtSaveLibrary.Entry valid = Checkpoint(library, current, "Safe target");
            foreach (string id in new[] { "checkpoint:../../Saves", "checkpoint:..\\..\\Saves", "/tmp/foreign", "archive:../foreign", "current/../foreign" })
            {
                var forged = (SignalDebtSaveLibrary.Entry)Activator.CreateInstance(typeof(SignalDebtSaveLibrary.Entry),
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { id, valid.Kind, valid.Label, valid.SavedUtc, current, valid.Status, valid.Fingerprint }, null);
                Dictionary<string, FileSnapshot> before = Capture(directory);
                Check(!library.TryActivate(forged, current, out _, out _) && Same(directory, before), "opaque selection rejects traversal/arbitrary paths without writes");
            }
            if (Environment.OSVersion.Platform != PlatformID.Unix) return;
            // Real filesystem links on the Linux validation host; no shell,
            // platform elevation or fake File API. Windows checks run separately.
            string outside = Fixture("link-outside"); var outsideStore = new SignalDebtSaveStore(outside); Check(outsideStore.Save(new SignalDebtSaveData()), "outside linked campaign exists");
            string backups = Path.Combine(directory, "Backups"); Directory.CreateDirectory(backups);
            string link = Path.Combine(backups, "Linked campaign"); MakeLink(outside, link);
            try
            {
                Dictionary<string, FileSnapshot> outsideBefore = Capture(outside);
                SignalDebtSaveLibrary.Entry linked = FindKind(library, SignalDebtSaveLibrary.EntryKind.Archive);
                Check(linked != null && !linked.CanLoad && !library.TryActivate(linked, current, out _, out _)
                    && Same(outside, outsideBefore), "linked archive is disabled and cannot read/write an outside campaign");
            }
            finally { File.Delete(link); }
            string checkpointPath = CheckpointDirectory(directory, valid);
            string savedPath = new SignalDebtSaveStore(checkpointPath).SavePath;
            File.Delete(savedPath); MakeLink(outsideStore.SavePath, savedPath);
            try
            {
                Check(!library.TryActivate(valid, current, out _, out _), "save-file symlink introduced after confirmation is rejected");
            }
            finally { File.Delete(savedPath); }
            symlinks = true;
        }

        private static void MakeLink(string target, string link)
        {
            var start = new System.Diagnostics.ProcessStartInfo("/bin/ln") { UseShellExecute = false, RedirectStandardError = true };
            start.ArgumentList.Add("-s"); start.ArgumentList.Add(target); start.ArgumentList.Add(link);
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(start))
            { process.WaitForExit(); Check(process.ExitCode == 0, "real symbolic link fixture created"); }
        }
        private static string CheckpointDirectory(string directory, SignalDebtSaveLibrary.Entry entry)
        { return Path.Combine(directory, "Checkpoints", entry.Id.Substring("checkpoint:".Length)); }
        private static SignalDebtSaveLibrary.Entry FindKind(SignalDebtSaveLibrary library, SignalDebtSaveLibrary.EntryKind kind)
        { foreach (SignalDebtSaveLibrary.Entry entry in library.List(0, 100).Entries) if (entry.Kind == kind) return entry; return null; }
        private static SignalDebtSaveLibrary.Entry FindByLabel(SignalDebtSaveLibrary library, string label)
        { foreach (SignalDebtSaveLibrary.Entry entry in library.List(0, 100).Entries) if (entry.Label == label) return entry; return null; }

        private static Dictionary<string, FileSnapshot> Capture(string directory)
        {
            var files = new Dictionary<string, FileSnapshot>();
            if (Directory.Exists(directory))
                foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                    files.Add(path.Substring(directory.Length + 1), new FileSnapshot { bytes = File.ReadAllBytes(path), ticks = File.GetLastWriteTimeUtc(path).Ticks });
            return files;
        }
        private static bool Same(string directory, Dictionary<string, FileSnapshot> files)
        { return Capture(directory).Count == files.Count && ContainsUnchanged(directory, files); }
        private static bool ContainsUnchanged(string directory, Dictionary<string, FileSnapshot> files)
        {
            foreach (KeyValuePair<string, FileSnapshot> item in files)
            {
                string path = Path.Combine(directory, item.Key);
                if (!File.Exists(path) || File.GetLastWriteTimeUtc(path).Ticks != item.Value.ticks) return false;
                byte[] actual = File.ReadAllBytes(path), expected = item.Value.bytes;
                if (actual.Length != expected.Length) return false;
                for (int index = 0; index < actual.Length; index++) if (actual[index] != expected[index]) return false;
            }
            return true;
        }
        private static void WriteSettingsSentinels(string directory)
        {
            // Deliberately not actual credentials or credential-shaped strings.
            File.WriteAllText(Path.Combine(directory, "ai-settings.json"), "PRIVATE SETTINGS FIXTURE - NEVER A REAL KEY");
            File.WriteAllText(Path.Combine(directory, "desktop-settings.json"), "DESKTOP PREFERENCE FIXTURE");
        }
        private static Dictionary<string, FileSnapshot> CaptureSettings(string directory)
        {
            var settings = new Dictionary<string, FileSnapshot>();
            foreach (string name in new[] { "ai-settings.json", "desktop-settings.json" })
            { string path = Path.Combine(directory, name); settings.Add(name, new FileSnapshot { bytes = File.ReadAllBytes(path), ticks = File.GetLastWriteTimeUtc(path).Ticks }); }
            return settings;
        }
        private static bool SameSettings(string directory, Dictionary<string, FileSnapshot> settings)
        { return ContainsUnchanged(directory, settings); }
    }
}
#endif
