using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace SignalDebt
{
    public sealed class SignalDebtSaveStore
    {
        public string SavePath { get; private set; }
        public string PreviousSavePath { get; private set; }
        public string VersionTwoSavePath { get; private set; }
        public string LegacySavePath { get; private set; }
        public string LoadedPath { get; private set; }
        public string LastStatus { get; private set; }
        public bool HasSaveError { get; private set; }
        public bool LoadBlocked { get; private set; }

        [Serializable] private sealed class SaveHeader { public int schemaVersion = 0; }
        [Serializable] private sealed class CurrentDeviceHeader
        {
            // Deliberate null default: a current save must contain a valid device.
            // SaveData's new-campaign defaults must not repair missing JSON fields.
            public SignalDebtClientDevice clientDevice = null;
        }
        [Serializable] private sealed class CurrentProofHeader
        {
            public SignalDebtProofDocument proofDocument = null;
        }
        private enum ReadStatus { Missing, Valid, Invalid, Unsupported, Unreadable }
        private sealed class Snapshot
        {
            public string path;
            public ReadStatus status;
            public SignalDebtSaveData state;
        }

        public SignalDebtSaveStore(string saveDirectory = null)
        {
            string directory = Path.GetFullPath(saveDirectory ?? Application.persistentDataPath);
            // Earlier players must not rewrite proof state through an older
            // schema. Older families are imported without replacing them.
            SavePath = Path.Combine(directory, "signal-debt-save-v4.json");
            PreviousSavePath = Path.Combine(directory, "signal-debt-save-v3.json");
            VersionTwoSavePath = Path.Combine(directory, "signal-debt-save-v2.json");
            LegacySavePath = Path.Combine(directory, "signal-debt-save.json");
        }

        public SignalDebtSaveData Load()
        {
            LoadedPath = null;
            LoadBlocked = false;
            HasSaveError = false;
            Snapshot[] family = FindExistingFamily();
            if (LoadBlocked) return null;
            if (family != null)
            {
                Snapshot selected = FirstValid(family);
                if (selected == null)
                    return Block("The selected save family and its recovery files are unreadable. They were preserved; no older or new campaign was substituted.");
                LoadedPath = selected.path;
                LastStatus = family[0].path != SavePath ? "Earlier campaign imported. Its original save files remain unchanged."
                    : selected.path == SavePath ? "Local save restored"
                    : selected.path.EndsWith(".bak", StringComparison.Ordinal) ? "Recovered last good backup"
                    : "Recovered interrupted save";
                return selected.state;
            }

            LastStatus = "New local game";
            return new SignalDebtSaveData();
        }

        // Load and every Save use exactly the same precedence and preservation
        // barriers. A present higher family can never fall through to older data.
        private Snapshot[] FindExistingFamily()
        {
            string[] paths = { SavePath, PreviousSavePath, VersionTwoSavePath, LegacySavePath };
            int[] versions = { SignalDebtSaveData.CurrentSchemaVersion, 3, 2, 1 };
            for (int index = 0; index < paths.Length; index++)
            {
                Snapshot[] family = ReadFamily(paths[index], versions[index]);
                if (HasPreservationBarrier(family) || HasAnyFile(family)) return family;
            }
            return null;
        }

        private static Snapshot[] ReadFamily(string path, int expectedVersion)
        {
            return new[] { Read(path, expectedVersion), Read(path + ".bak", expectedVersion), Read(path + ".tmp", expectedVersion) };
        }

        private static Snapshot Read(string path, int expectedVersion)
        {
            var result = new Snapshot { path = path, status = ReadStatus.Missing };
            try
            {
                // File.Exists returns false for some access errors. An unreadable
                // file cannot safely be treated as an absent campaign.
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) != 0)
                { result.status = ReadStatus.Invalid; return result; }
                string json = File.ReadAllText(path);
                result.status = ReadStatus.Invalid;
                SaveHeader header = JsonUtility.FromJson<SaveHeader>(json);
                if (header == null) return result;
                if (header.schemaVersion > expectedVersion)
                { result.status = ReadStatus.Unsupported; return result; }
                if (header.schemaVersion != expectedVersion) return result;
                if (expectedVersion >= 3)
                {
                    // Version is checked separately first: a future payload may
                    // use an incompatible device shape and must still block rollback.
                    CurrentDeviceHeader required = JsonUtility.FromJson<CurrentDeviceHeader>(json);
                    if (required == null || required.clientDevice == null || !required.clientDevice.Validate()) return result;
                }
                if (expectedVersion == SignalDebtSaveData.CurrentSchemaVersion)
                {
                    CurrentProofHeader required = JsonUtility.FromJson<CurrentProofHeader>(json);
                    if (required == null || required.proofDocument == null || !required.proofDocument.Validate()) return result;
                }
                SignalDebtSaveData parsed = JsonUtility.FromJson<SignalDebtSaveData>(json);
                if (parsed == null) return result;
                SignalDebtSaveData candidate = expectedVersion == 1 ? parsed.MigrateV1()
                    : expectedVersion == 2 ? SignalDebtSaveData.MigrateV2(parsed)
                    : expectedVersion == 3 ? SignalDebtSaveData.MigrateV3(parsed) : parsed;
                if (candidate == null || !candidate.Validate()) return result;
                result.state = candidate;
                result.status = ReadStatus.Valid;
            }
            catch (FileNotFoundException) { result.status = ReadStatus.Missing; }
            catch (DirectoryNotFoundException) { result.status = ReadStatus.Missing; }
            catch (ArgumentException) { result.status = ReadStatus.Invalid; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { result.status = ReadStatus.Unreadable; }
            return result;
        }

        private bool HasPreservationBarrier(Snapshot[] family)
        {
            foreach (Snapshot snapshot in family)
            {
                if (snapshot.status == ReadStatus.Unsupported)
                {
                    Block("A save file uses a newer format than its filename supports. It and its recovery files were preserved. Open the campaign with a compatible version.");
                    return true;
                }
                if (snapshot.status == ReadStatus.Unreadable)
                {
                    Block("A save file could not be read safely. It and its recovery files were preserved. Resolve the file-access problem before continuing.");
                    return true;
                }
            }
            return false;
        }

        private static bool HasAnyFile(Snapshot[] family)
        {
            foreach (Snapshot snapshot in family) if (snapshot.status != ReadStatus.Missing) return true;
            return false;
        }

        private static Snapshot FirstValid(Snapshot[] family)
        {
            foreach (Snapshot snapshot in family) if (snapshot.status == ReadStatus.Valid) return snapshot;
            return null;
        }

        private SignalDebtSaveData Block(string message)
        {
            LoadBlocked = true;
            HasSaveError = true;
            LastStatus = message;
            return null;
        }

        public bool Save(SignalDebtSaveData state)
        {
            if (LoadBlocked) { HasSaveError = true; return false; }
            if (state == null || !state.Validate()) return SaveFailure("Save rejected: inconsistent game state");

            // Check before every write, including a Save made without Load.
            // Unsupported primary, backup and temporary files must be preserved.
            Snapshot[] family = FindExistingFamily();
            if (LoadBlocked) return false;
            if (family != null && FirstValid(family) == null)
            {
                Block("Existing save files could not be validated. They were preserved; saving is blocked.");
                return false;
            }

            string staging = SavePath + ".stage-" + Guid.NewGuid().ToString("N");
            try
            {
                string json = JsonUtility.ToJson(state, true);
                SignalDebtSaveData serialized = JsonUtility.FromJson<SignalDebtSaveData>(json);
                if (serialized == null || !serialized.Validate())
                    return SaveFailure("Save rejected: serialized candidate failed verification");

                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                // A failed staging write cannot destroy an existing primary,
                // backup or the only recoverable temporary snapshot.
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (Read(staging, SignalDebtSaveData.CurrentSchemaVersion).status != ReadStatus.Valid)
                    return SaveFailure("Save rejected: written candidate failed verification");

                if (File.Exists(SavePath))
                {
                    // A corrupt primary recovered from .bak must not replace that
                    // good backup. Only rotate a verified current primary into it.
                    string backup = family != null && family[0].path == SavePath
                        && family[0].status == ReadStatus.Valid ? SavePath + ".bak" : null;
                    File.Replace(staging, SavePath, backup);
                }
                else File.Move(staging, SavePath);

                LastStatus = "Saved locally";
                HasSaveError = false;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
                || error is ArgumentException || error is NotSupportedException)
            {
                // An IO failure must not trigger a destructive rename fallback.
                // Never publish a failed purchase as recoverable .tmp data.
                return SaveFailure("Could not save: " + error.Message);
            }
            finally
            {
                // Remove only the uniquely allocated staging file for this attempt.
                try { if (File.Exists(staging)) File.Delete(staging); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                { Debug.LogWarning("Could not remove an incomplete save staging file: " + error.Message); }
            }
        }

        private bool SaveFailure(string message)
        {
            LastStatus = message;
            HasSaveError = true;
            Debug.LogWarning(message);
            return false;
        }
    }
}
