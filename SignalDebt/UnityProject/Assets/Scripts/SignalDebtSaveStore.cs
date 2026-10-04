using System;
using System.IO;
using UnityEngine;

namespace SignalDebt
{
    public sealed class SignalDebtSaveStore
    {
        public string SavePath { get; private set; }
        public string LastStatus { get; private set; }
        public bool HasSaveError { get; private set; }

        public SignalDebtSaveStore()
        {
            SavePath = Path.Combine(Application.persistentDataPath, "signal-debt-save.json");
        }

        public SignalDebtSaveData Load()
        {
            SignalDebtSaveData result;
            if (TryRead(SavePath, out result))
            {
                LastStatus = "Local save restored";
                return result;
            }
            if (TryRead(SavePath + ".bak", out result))
            {
                LastStatus = "Recovered last good backup";
                return result;
            }
            // A .tmp may be complete if the process stopped before the final rename.
            if (TryRead(SavePath + ".tmp", out result))
            {
                LastStatus = "Recovered interrupted save";
                return result;
            }
            LastStatus = File.Exists(SavePath) ? "Unreadable save; starting a new local game" : "New local game";
            return new SignalDebtSaveData();
        }

        private static bool TryRead(string path, out SignalDebtSaveData result)
        {
            result = null;
            if (!File.Exists(path)) return false;
            try
            {
                result = JsonUtility.FromJson<SignalDebtSaveData>(File.ReadAllText(path));
                return result != null && result.Validate();
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        public bool Save(SignalDebtSaveData state)
        {
            if (state == null || !state.Validate())
            {
                LastStatus = "Save rejected: inconsistent game state";
                HasSaveError = true;
                return false;
            }
            string temporary = SavePath + ".tmp";
            string backup = SavePath + ".bak";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(temporary, JsonUtility.ToJson(state, true));
                // Verify the entire candidate before replacing the last good snapshot.
                SignalDebtSaveData verified;
                if (!TryRead(temporary, out verified)) throw new IOException("Candidate failed verification");
                if (File.Exists(SavePath))
                {
                    try { File.Replace(temporary, SavePath, backup); }
                    catch (PlatformNotSupportedException) { ReplaceWithBackup(temporary, backup); }
                    catch (IOException) { ReplaceWithBackup(temporary, backup); }
                }
                else File.Move(temporary, SavePath);
                LastStatus = "Saved locally";
                HasSaveError = false;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                LastStatus = "Could not save: " + ex.Message;
                HasSaveError = true;
                Debug.LogWarning(LastStatus);
                return false;
            }
        }

        private void ReplaceWithBackup(string temporary, string backup)
        {
            // Fallback is recoverable rather than guaranteed atomic on every platform.
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(SavePath, backup);
            File.Move(temporary, SavePath);
        }
    }
}
