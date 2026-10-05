using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SignalDebt
{
    // The browser never exposes paths or mutable campaign objects. Checkpoints
    // are published once, and every load revalidates the selected disk contents.
    public sealed class SignalDebtSaveLibrary
    {
        public enum EntryKind { Current, Checkpoint, Archive }

        public sealed class Entry
        {
            public string Id { get; }
            public EntryKind Kind { get; }
            public string Label { get; }
            public DateTime SavedUtc { get; }
            public int Day { get; }
            public int Balance { get; }
            public string ProgressSummary { get; }
            public bool CanLoad { get; }
            public string Status { get; }
            public string Fingerprint { get; }

            internal Entry(string id, EntryKind kind, string label, DateTime savedUtc,
                SignalDebtSaveData state, string status, string fingerprint)
            {
                Id = id; Kind = kind; Label = label; SavedUtc = savedUtc;
                Day = state == null ? 0 : state.day;
                Balance = state == null ? 0 : state.balance;
                CanLoad = state != null;
                ProgressSummary = state == null ? "Progress unavailable" :
                    "Repair " + (state.jobPaid ? "paid" : state.jobAccepted ? "in progress" : "not accepted")
                    + " · Proof " + (state.proofPaid ? "paid" : state.proofAccepted ? "in progress" : "not accepted");
                Status = status; Fingerprint = fingerprint;
            }
        }

        public sealed class Page
        {
            public IReadOnlyList<Entry> Entries { get; }
            public int Total { get; }
            public string Error { get; }
            internal Page(List<Entry> entries, int total, string error)
            { Entries = entries.AsReadOnly(); Total = total; Error = error; }
        }

        [Serializable] private sealed class CheckpointMetadata
        {
            public int schemaVersion = 1;
            public string label, createdUtc;
        }

        private sealed class Inspected
        {
            public Entry entry;
            public SignalDebtSaveData state;
        }

        private static readonly string[] SaveNames = {
            "signal-debt-save-v4.json", "signal-debt-save-v3.json",
            "signal-debt-save-v2.json", "signal-debt-save.json"
        };
        private static readonly string[] Suffixes = { "", ".bak", ".tmp" };
        private const long MaximumSaveBytes = 16 * 1024 * 1024;
        private readonly string dataDirectory, savesDirectory, checkpointsDirectory, backupsDirectory, legacyDirectory;

        public SignalDebtSaveLibrary(string dataDirectory = null, string legacyDirectory = null)
        {
            this.dataDirectory = Path.GetFullPath(dataDirectory ?? SignalDebtLocalFiles.DataDirectory);
            savesDirectory = Path.Combine(this.dataDirectory, "Saves");
            checkpointsDirectory = Path.Combine(this.dataDirectory, "Checkpoints");
            backupsDirectory = Path.Combine(this.dataDirectory, "Backups");
            // The caller opts in to one known earlier installation. Tests never
            // default to inspecting the real account's persistent-data folder.
            this.legacyDirectory = legacyDirectory == null ? null : Path.GetFullPath(legacyDirectory);
        }

        public Page List(int page, int pageSize)
        {
            page = Math.Max(0, page);
            pageSize = Math.Max(1, Math.Min(100, pageSize));
            var entries = new List<Entry>();
            string error = "";
            try
            {
                EnsureSafePath(savesDirectory);
                if (SignalDebtLocalFiles.HasCampaign(savesDirectory) || File.Exists(savesDirectory))
                    entries.Add(Inspect("current", EntryKind.Current, savesDirectory).entry);
            }
            catch (Exception exception) when (IsFileError(exception))
            { error = "The current campaign could not be read safely."; }
            AddEntries(checkpointsDirectory, EntryKind.Checkpoint, entries, ref error);
            AddEntries(backupsDirectory, EntryKind.Archive, entries, ref error);
            if (legacyDirectory != null && !SamePath(legacyDirectory, savesDirectory))
            {
                try
                {
                    EnsureSafePath(legacyDirectory);
                    if (SignalDebtLocalFiles.HasCampaign(legacyDirectory))
                        entries.Add(Inspect("earlier-installation", EntryKind.Archive, legacyDirectory).entry);
                }
                catch (Exception exception) when (IsFileError(exception))
                { error = "The earlier installation could not be read safely. Its files were preserved."; }
            }
            entries.Sort((left, right) => {
                if (left.Kind == EntryKind.Current && right.Kind != EntryKind.Current) return -1;
                if (right.Kind == EntryKind.Current && left.Kind != EntryKind.Current) return 1;
                int date = right.SavedUtc.CompareTo(left.SavedUtc);
                return date != 0 ? date : string.CompareOrdinal(left.Id, right.Id);
            });
            int total = entries.Count;
            long first = (long)page * pageSize;
            if (first >= total) entries.Clear();
            else entries = entries.GetRange((int)first, Math.Min(pageSize, total - (int)first));
            return new Page(entries, total, error);
        }

        public bool TryCreateCheckpoint(SignalDebtSaveData current, string label, out Entry entry, out string error)
        {
            entry = null; error = "";
            if (current == null || !current.Validate())
            { error = "There is no valid current campaign to checkpoint."; return false; }
            string staging = null;
            try
            {
                SignalDebtSaveData detached = current.Copy();
                EnsureSafePath(checkpointsDirectory);
                Directory.CreateDirectory(checkpointsDirectory);
                string token = Guid.NewGuid().ToString("N");
                staging = Path.Combine(checkpointsDirectory, ".creating-" + token);
                Directory.CreateDirectory(staging);
                EnsureSafePath(staging);
                var store = new SignalDebtSaveStore(staging);
                if (!store.Save(detached))
                { error = "The checkpoint could not be written. Current progress is unchanged."; return false; }
                var metadata = new CheckpointMetadata {
                    label = CleanLabel(label), createdUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                };
                EnsureSafePath(staging);
                WriteNewFile(Path.Combine(staging, "checkpoint.json"), JsonUtility.ToJson(metadata));
                string destination = Path.Combine(checkpointsDirectory, token);
                Inspected verified = Inspect("checkpoint:" + token, EntryKind.Checkpoint, staging);
                if (!verified.entry.CanLoad || StateFingerprint(verified.state) != StateFingerprint(detached))
                { error = "The checkpoint failed verification. Current progress is unchanged."; return false; }
                EnsureSafePath(staging); EnsureSafePath(destination);
                Directory.Move(staging, destination);
                staging = null;
                // The bytes just verified move together; avoid a fallible read
                // after publication that could falsely report a failed save.
                entry = verified.entry;
                return true;
            }
            catch (Exception exception) when (IsFileError(exception))
            { error = "The checkpoint could not be saved. Check that UserData is writable."; return false; }
            finally
            {
                if (staging != null)
                {
                    try { EnsureSafePath(staging); if (Directory.Exists(staging)) Directory.Delete(staging, true); }
                    catch (Exception exception) when (IsFileError(exception)) { }
                }
            }
        }

        public bool TryActivate(Entry selection, SignalDebtSaveData currentOrNull,
            out SignalDebtSaveData activated, out string error)
        {
            activated = null; error = "";
            if (selection == null || !selection.CanLoad || string.IsNullOrEmpty(selection.Fingerprint))
            { error = "Choose an available saved campaign."; return false; }
            if (currentOrNull != null && !currentOrNull.Validate())
            { error = "Current progress cannot be preserved safely. No campaign was loaded."; return false; }
            Entry safety = null;
            try
            {
                string selectedDirectory;
                EntryKind selectedKind;
                if (!TryResolve(selection.Id, out selectedDirectory, out selectedKind))
                { error = "That saved campaign is no longer available. Refresh the list."; return false; }
                Inspected target = Inspect(selection.Id, selectedKind, selectedDirectory);
                if (!target.entry.CanLoad || target.entry.Fingerprint != selection.Fingerprint)
                { error = "That saved campaign changed or cannot be read. Refresh the list before loading."; return false; }

                EnsureSafePath(savesDirectory);
                string activeFingerprint = FamilyFingerprint(savesDirectory, out bool activeExists);
                SignalDebtSaveData previous = currentOrNull == null ? null : currentOrNull.Copy();
                if (activeExists)
                {
                    var activeStore = new SignalDebtSaveStore(savesDirectory);
                    SignalDebtSaveData saved = activeStore.Load();
                    if (saved == null || activeStore.LoadBlocked)
                    { error = "The current save needs recovery before another campaign can replace it. Its files were preserved."; return false; }
                    if (previous != null && StateFingerprint(previous) != StateFingerprint(saved))
                    { error = "The current save changed outside this session. Reload it before switching campaigns."; return false; }
                    previous = saved;
                }
                if (previous != null && !TryCreateCheckpoint(previous, "Before loading another save", out safety, out error))
                { error = "Current progress could not be checkpointed. No campaign was loaded. " + error; return false; }

                // Recheck both ends after preserving current progress. Store.Save
                // also enforces future/corrupt-family barriers immediately before
                // atomic publication. Nothing is merged into the selected ledger.
                Inspected rechecked = Inspect(selection.Id, selectedKind, selectedDirectory);
                if (FamilyFingerprint(savesDirectory, out _) != activeFingerprint
                    || !rechecked.entry.CanLoad || rechecked.entry.Fingerprint != selection.Fingerprint)
                { error = "Save files changed while loading. No campaign was switched."; return false; }
                var store = new SignalDebtSaveStore(savesDirectory);
                SignalDebtSaveData candidate = target.state.Copy();
                EnsureSafePath(savesDirectory);
                if (!store.Save(candidate))
                { error = "The selected campaign could not replace the current save. Current progress and the selected save are unchanged."; return false; }
                activated = candidate;
                return true;
            }
            catch (Exception exception) when (IsFileError(exception))
            { error = "The saved campaign could not be accessed safely. No campaign was loaded."; return false; }
            finally
            {
                if (activated == null && safety != null)
                    error += " A safety checkpoint of current progress was retained.";
            }
        }

        private void AddEntries(string directory, EntryKind kind, List<Entry> entries, ref string error)
        {
            try
            {
                EnsureSafePath(directory);
                if (File.Exists(directory)) throw new IOException();
                if (!Directory.Exists(directory)) return;
                foreach (string child in Directory.GetDirectories(directory))
                {
                    string name = Path.GetFileName(child);
                    if (kind == EntryKind.Checkpoint && !IsCheckpointToken(name)) continue;
                    string id = kind == EntryKind.Checkpoint ? "checkpoint:" + name : "archive:" + Hash(name);
                    entries.Add(Inspect(id, kind, child).entry);
                }
            }
            catch (Exception exception) when (IsFileError(exception))
            { error = "Some saved campaigns could not be listed safely. Their files were preserved."; }
        }

        private bool TryResolve(string id, out string directory, out EntryKind kind)
        {
            directory = null; kind = EntryKind.Current;
            if (id == "current") { directory = savesDirectory; return true; }
            if (id == "earlier-installation" && legacyDirectory != null)
            { directory = legacyDirectory; kind = EntryKind.Archive; return true; }
            if (id != null && id.StartsWith("checkpoint:", StringComparison.Ordinal))
            {
                string token = id.Substring("checkpoint:".Length);
                if (!IsCheckpointToken(token)) return false;
                directory = Path.Combine(checkpointsDirectory, token); kind = EntryKind.Checkpoint;
                return true;
            }
            if (id == null || !id.StartsWith("archive:", StringComparison.Ordinal) || id.Length != 72) return false;
            EnsureSafePath(backupsDirectory);
            if (!Directory.Exists(backupsDirectory)) return false;
            foreach (string child in Directory.GetDirectories(backupsDirectory))
                if ("archive:" + Hash(Path.GetFileName(child)) == id)
                { directory = child; kind = EntryKind.Archive; return true; }
            return false;
        }

        private Inspected Inspect(string id, EntryKind kind, string directory)
        {
            string label = id == "earlier-installation" ? "Earlier installation" : kind == EntryKind.Current ? "Current campaign"
                : kind == EntryKind.Archive ? "Previous campaign" : "Manual checkpoint";
            DateTime timestamp = DateTime.MinValue;
            string fingerprint = "";
            try
            {
                fingerprint = InspectionFingerprint(directory, kind, out bool exists);
                if (!exists)
                    return Unavailable(id, kind, label, timestamp, fingerprint, "No save files remain in this folder.");
                var store = new SignalDebtSaveStore(directory);
                SignalDebtSaveData state = store.Load();
                if (state == null || store.LoadBlocked)
                    return Unavailable(id, kind, label, timestamp, fingerprint, store.LastStatus);
                // Date belongs to the selected primary/backup/temporary file,
                // never an unrelated corrupt or older-family member's mtime.
                timestamp = File.GetLastWriteTimeUtc(store.LoadedPath);
                if (kind == EntryKind.Checkpoint) ReadMetadata(directory, ref label, ref timestamp);
                // The fingerprint must describe the same immutable read we show
                // in the card, including recovery files and legacy families.
                if (InspectionFingerprint(directory, kind, out _) != fingerprint)
                    return Unavailable(id, kind, label, timestamp, "", "Save files changed while being read. Refresh the list.");
                return new Inspected { state = state, entry = new Entry(id, kind, label, timestamp, state, store.LastStatus, fingerprint) };
            }
            catch (Exception exception) when (IsFileError(exception))
            { return Unavailable(id, kind, label, timestamp, fingerprint, "This save could not be read safely. Its files were preserved."); }
        }

        private static Inspected Unavailable(string id, EntryKind kind, string label,
            DateTime timestamp, string fingerprint, string status)
        { return new Inspected { entry = new Entry(id, kind, label, timestamp, null, status, fingerprint) }; }

        private string FamilyFingerprint(string directory, out bool exists)
        {
            EnsureSafePath(directory);
            exists = false;
            if (File.Exists(directory)) throw new IOException();
            var content = new StringBuilder();
            foreach (string name in SaveNames)
                foreach (string suffix in Suffixes)
                {
                    string path = Path.Combine(directory, name + suffix);
                    EnsureSafePath(path);
                    FileAttributes attributes;
                    try { attributes = File.GetAttributes(path); }
                    catch (FileNotFoundException) { content.Append(name).Append(suffix).Append(":missing;"); continue; }
                    catch (DirectoryNotFoundException) { content.Append(name).Append(suffix).Append(":missing;"); continue; }
                    exists = true;
                    content.Append(name).Append(suffix).Append(':');
                    if ((attributes & FileAttributes.Directory) != 0) content.Append("directory;");
                    else
                    {
                        var info = new FileInfo(path);
                        if (info.Length > MaximumSaveBytes) throw new IOException();
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (SHA256 sha = SHA256.Create()) content.Append(Hex(sha.ComputeHash(stream))).Append(';');
                    }
                }
            return Hash(content.ToString());
        }

        private string InspectionFingerprint(string directory, EntryKind kind, out bool exists)
        {
            string fingerprint = FamilyFingerprint(directory, out exists);
            if (kind != EntryKind.Checkpoint) return fingerprint;
            string path = Path.Combine(directory, "checkpoint.json");
            EnsureSafePath(path);
            if (Directory.Exists(path)) throw new IOException();
            if (!File.Exists(path)) return Hash(fingerprint + ":metadata-missing");
            if (new FileInfo(path).Length > 4096) throw new IOException();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 sha = SHA256.Create()) return Hash(fingerprint + ":metadata:" + Hex(sha.ComputeHash(stream)));
        }

        private void ReadMetadata(string directory, ref string label, ref DateTime timestamp)
        {
            string path = Path.Combine(directory, "checkpoint.json");
            EnsureSafePath(path);
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length > 4096) throw new IOException();
            CheckpointMetadata metadata = JsonUtility.FromJson<CheckpointMetadata>(File.ReadAllText(path));
            if (metadata == null || metadata.schemaVersion != 1) throw new IOException();
            label = CleanLabel(metadata.label);
            if (DateTime.TryParse(metadata.createdUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime created))
                timestamp = created.ToUniversalTime();
        }

        private void EnsureSafePath(string path)
        {
            string full = Path.GetFullPath(path);
            if (!Within(full, dataDirectory) && (legacyDirectory == null || !Within(full, legacyDirectory))) throw new IOException();
            // Reject links/junctions present at inspection, including ancestors.
            // Recheck before writes/publication/cleanup. Ordinary managed file
            // APIs do not guarantee isolation from hostile concurrent OS edits.
            for (string current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException();
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        private static bool SamePath(string left, string right)
        { return string.Equals(left.TrimEnd(Path.DirectorySeparatorChar), right.TrimEnd(Path.DirectorySeparatorChar),
            Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal); }
        private static bool Within(string path, string directory)
        {
            StringComparison comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            string prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return SamePath(path, directory) || path.StartsWith(prefix, comparison);
        }

        private static bool IsCheckpointToken(string token)
        {
            if (token == null || token.Length != 32) return false;
            foreach (char value in token) if (!(value >= '0' && value <= '9') && !(value >= 'a' && value <= 'f')) return false;
            return true;
        }

        private static string CleanLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return "Manual checkpoint";
            var clean = new StringBuilder();
            foreach (char value in label.Trim())
            {
                if (clean.Length == 64) break;
                // UI text is not markup; eliminate line/control characters and
                // rich-text delimiters in imported checkpoint labels.
                clean.Append(char.IsControl(value) || value == '<' || value == '>' ? ' ' : value);
            }
            return string.IsNullOrWhiteSpace(clean.ToString()) ? "Manual checkpoint" : clean.ToString().Trim();
        }

        private static void WriteNewFile(string path, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        }

        private static string StateFingerprint(SignalDebtSaveData state) { return Hash(JsonUtility.ToJson(state)); }
        private static string Hash(string text)
        { using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text))); }
        private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
        private static bool IsFileError(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException
                || exception is System.Security.SecurityException;
        }
    }
}
