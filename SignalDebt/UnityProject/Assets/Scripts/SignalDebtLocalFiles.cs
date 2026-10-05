using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace SignalDebt
{
    // Portable player data lives beside the executable, never in the shipped
    // assets. In the editor this resolves to UnityProject/UserData.
    public static class SignalDebtLocalFiles
    {
        public static string DataDirectory
        {
            get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UserData"); }
        }
        public static string SaveDirectory { get { return Path.Combine(DataDirectory, "Saves"); } }

        private static string LocalPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name
                || name == "." || name == ".." || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                throw new ArgumentException("A local settings filename is required.");
            return Path.Combine(DataDirectory, name);
        }

        public static bool TryReadText(string name, int maximumLength, out string text)
        {
            text = null;
            try
            {
                string path = LocalPath(name);
                if (!File.Exists(path) || maximumLength < 1 || new FileInfo(path).Length > maximumLength * 4L) return false;
                string value = File.ReadAllText(path);
                if (value.Length > maximumLength) return false;
                text = value;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
                || error is ArgumentException || error is NotSupportedException) { return false; }
        }

        public static bool TryWriteText(string name, string text, out string error)
        {
            error = "";
            string staging = null;
            try
            {
                string path = LocalPath(name);
                Directory.CreateDirectory(DataDirectory);
                staging = path + ".stage-" + Guid.NewGuid().ToString("N");
                byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
                using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(staging, path, null);
                else File.Move(staging, path);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException)
            { error = "The UserData folder is not writable. Move the extracted game to a writable folder."; return false; }
            finally
            {
                try { if (staging != null && File.Exists(staging)) File.Delete(staging); }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException) { }
            }
        }

        public static bool HasCampaign(string directory)
        {
            foreach (string name in new[] { "signal-debt-save-v4.json", "signal-debt-save-v3.json", "signal-debt-save-v2.json", "signal-debt-save.json" })
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    if (File.Exists(Path.Combine(directory, name + suffix)) || Directory.Exists(Path.Combine(directory, name + suffix))) return true;
            return false;
        }

        public static bool ArchiveCampaign(out string error)
        {
            error = "";
            try
            {
                if (!Directory.Exists(SaveDirectory)) return true;
                string backups = Path.Combine(DataDirectory, "Backups");
                Directory.CreateDirectory(backups);
                // One directory rename keeps the whole campaign family together.
                // API settings and desktop preferences are outside this directory.
                string destination = Path.Combine(backups, "Campaign-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")
                    + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.Move(SaveDirectory, destination);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException)
            { error = "Your current campaign could not be backed up. No new game was started."; return false; }
        }
    }
}
