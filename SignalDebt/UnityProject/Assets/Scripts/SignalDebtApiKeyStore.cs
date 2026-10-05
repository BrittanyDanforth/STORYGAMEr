using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace SignalDebt
{
    // Remembering a key is an explicit opt-in to a readable local file. This is
    // separate from the campaign and never uses PlayerPrefs or an account store.
    public static class SignalDebtApiKeyStore
    {
        private static readonly LocalStore Store = new LocalStore(() => SignalDebtLocalFiles.DataDirectory);

        public static bool TryLoad(out string key) { return Store.TryLoad(out key); }
        public static bool TrySave(string key, out string message) { return Store.TrySave(key, out message); }
        public static bool SetSessionKey(string key, out string message) { return Store.SetSessionKey(key, out message); }
        public static bool TryDelete(out string message) { return Store.TryDelete(out message); }
        public static bool HasRememberedKey { get { return Store.HasRememberedKey; } }
        public static bool CurrentKeyIsRemembered { get { return Store.CurrentKeyIsRemembered; } }
        public static string StorageDescription { get { return Store.StorageDescription; } }

        // The instance seam keeps filesystem tests away from the player's folder
        // and accepts a test environment provider without changing process state.
        internal sealed class LocalStore
        {
            private const int SchemaVersion = 1, MaximumFileBytes = 8192;
            private const int MinimumKeyCharacters = 20, MaximumKeyCharacters = 1024;
            private const string FileName = "ai-settings.json";
            private const string InvalidKeyMessage = "Enter a single-line OpenAI API key beginning with sk- (20 to 1,024 characters).";
            private readonly object gate = new object();
            private readonly Func<string> directory;
            private readonly Func<string> environment;
            private char[] sessionKey;

            internal LocalStore(Func<string> directory, Func<string> environment = null)
            {
                if (directory == null) throw new ArgumentNullException(nameof(directory));
                this.directory = directory;
                this.environment = environment ?? (() => Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            }

            public bool HasRememberedKey
            {
                get { lock (gate) { return TryReadRemembered(out string ignored); } }
            }

            public bool CurrentKeyIsRemembered
            {
                get
                {
                    lock (gate)
                    {
                        if (!TryReadRemembered(out string remembered)) return false;
                        if (sessionKey == null) return true;
                        // Check the actual file instead of retaining a saved flag:
                        // the player may replace or remove it while the game runs.
                        if (sessionKey.Length != remembered.Length) return false;
                        int difference = 0;
                        for (int index = 0; index < sessionKey.Length; index++)
                            difference |= sessionKey[index] ^ remembered[index];
                        return difference == 0;
                    }
                }
            }

            public string StorageDescription
            {
                get
                {
                    return "Remembering saves a readable plaintext key in UserData/ai-settings.json beside the game. "
                        + "Anyone who can read that file can use the key. Session-only keys are not written to disk."
                        + EnvironmentDescription();
                }
            }

            public bool TryLoad(out string key)
            {
                lock (gate)
                {
                    // An explicitly entered session key takes precedence without
                    // changing what the next game launch will remember.
                    if (sessionKey != null) { key = new string(sessionKey); return true; }
                    if (TryReadRemembered(out key)) return true;
                    return TryReadEnvironment(out key);
                }
            }

            public bool SetSessionKey(string key, out string message)
            {
                message = InvalidKeyMessage;
                if (!TryNormalize(key, out string normalized)) return false;
                lock (gate)
                {
                    ReplaceSession(normalized.ToCharArray());
                    message = "API key set for this game session only. The remembered file, if any, is unchanged."
                        + EnvironmentDescription();
                    return true;
                }
            }

            public bool TrySave(string key, out string message)
            {
                message = InvalidKeyMessage;
                if (!TryNormalize(key, out string normalized)) return false;
                lock (gate)
                {
                    string staging = null;
                    char[] replacement = normalized.ToCharArray();
                    byte[] serialized = null;
                    try
                    {
                        // Normalization restricts the value to ASCII letters,
                        // digits, '-' and '_', so it cannot need JSON escaping.
                        // Avoid serializers that require desktop configuration
                        // sections unavailable in a standalone Unity player.
                        serialized = Encoding.UTF8.GetBytes("{\"schemaVersion\":"
                            + SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + ",\"apiKey\":\"" + normalized + "\"}");
                        string folder = directory();
                        string path = Path.Combine(folder, FileName);
                        Directory.CreateDirectory(folder);
                        staging = Path.Combine(folder, FileName + ".stage-" + Guid.NewGuid().ToString("N"));
                        using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            // On Unix, restrict the empty file before writing any
                            // key bytes. The renamed file retains the same mode.
                            RestrictUnixPermissions(staging);
                            stream.Write(serialized, 0, serialized.Length);
                            stream.Flush(true);
                        }
                        // No backup or recoverable temporary credential remains.
                        // Replacement failure leaves the old remembered key intact.
                        if (File.Exists(path)) File.Replace(staging, path, null);
                        else File.Move(staging, path);
                        ReplaceSession(replacement);
                        replacement = null;
                        message = "API key remembered as readable plaintext in UserData/ai-settings.json."
                            + EnvironmentDescription();
                        return true;
                    }
                    catch (Exception error) when (IsStorageError(error))
                    {
                        message = "Could not remember the API key. The previous saved and session keys are unchanged. "
                            + "Check that the game's UserData folder is writable.";
                        return false;
                    }
                    finally
                    {
                        if (replacement != null) Array.Clear(replacement, 0, replacement.Length);
                        if (serialized != null) Array.Clear(serialized, 0, serialized.Length);
                        if (staging != null)
                        {
                            try { if (File.Exists(staging)) File.Delete(staging); }
                            catch (Exception error) when (IsStorageError(error)) { }
                        }
                    }
                }
            }

            public bool TryDelete(out string message)
            {
                lock (gate)
                {
                    try
                    {
                        string path = Path.Combine(directory(), FileName);
                        if (Directory.Exists(path)) throw new IOException("Settings path is not a file.");
                        try { File.Delete(path); } // Missing file is already forgotten.
                        catch (DirectoryNotFoundException) { }
                    }
                    catch (Exception error) when (IsStorageError(error))
                    {
                        message = "Could not remove the remembered API key. The saved and session keys are unchanged.";
                        return false;
                    }
                    ReplaceSession(null);
                    message = "Remembered and session API keys removed." + EnvironmentDescription();
                    return true;
                }
            }

            private bool TryReadRemembered(out string key)
            {
                key = null;
                byte[] bytes = null;
                try
                {
                    string path = Path.Combine(directory(), FileName);
                    if (!File.Exists(path)) return false;
                    int length = 0;
                    bytes = new byte[MaximumFileBytes + 1];
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (stream.Length < 1 || stream.Length > MaximumFileBytes) return false;
                        while (length < bytes.Length)
                        {
                            int read = stream.Read(bytes, length, bytes.Length - length);
                            if (read == 0) break;
                            length += read;
                        }
                    }
                    if (length < 1 || length > MaximumFileBytes) return false;
                    // Invalid UTF-8 must not be silently repaired into different
                    // settings. The parser consumes exactly one bounded object.
                    string json = new UTF8Encoding(false, true).GetString(bytes, 0, length);
                    return new SettingsJsonReader(json).TryRead(out key);
                }
                catch (Exception error) when (IsStorageError(error)) { return false; }
                finally { if (bytes != null) Array.Clear(bytes, 0, bytes.Length); }
            }

            private bool TryReadEnvironment(out string key)
            {
                key = null;
                try { return TryNormalize(environment(), out key); }
                catch (SecurityException) { return false; }
            }

            private string EnvironmentDescription()
            {
                return TryReadEnvironment(out string ignored)
                    ? " An environment-provided key remains available as fallback; forgetting keys does not clear that binding."
                    : " No usable environment-provided key is available.";
            }

            private void ReplaceSession(char[] replacement)
            {
                if (sessionKey != null) Array.Clear(sessionKey, 0, sessionKey.Length);
                sessionKey = replacement;
            }

            private static bool TryNormalize(string value, out string normalized)
            {
                normalized = null;
                if (string.IsNullOrEmpty(value) || value.Length > MaximumKeyCharacters + 64) return false;
                foreach (char character in value)
                    if (char.IsControl(character) || character == '\u2028' || character == '\u2029') return false;
                string candidate = value.Trim();
                if (candidate.Length < MinimumKeyCharacters || candidate.Length > MaximumKeyCharacters
                    || !candidate.StartsWith("sk-", StringComparison.Ordinal)) return false;
                foreach (char character in candidate)
                    if (!((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z')
                        || (character >= '0' && character <= '9') || character == '-' || character == '_')) return false;
                normalized = candidate;
                return true;
            }

            private static bool IsStorageError(Exception error)
            {
                return error is IOException || error is UnauthorizedAccessException || error is SecurityException
                    || error is ArgumentException || error is NotSupportedException;
            }

            private static void RestrictUnixPermissions(string path)
            {
                PlatformID platform = Environment.OSVersion.Platform;
                if (platform != PlatformID.Unix && platform != PlatformID.MacOSX) return;
                try
                {
                    if (Chmod(path, 384) != 0) throw new IOException("Could not restrict local settings permissions."); // 0600
                }
                // Some non-Windows runtimes do not expose libc. In those runtimes
                // the local file uses the platform's normal file permissions.
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            }

            // A deliberately small, non-recursive reader for our two-field
            // schema, not a general JSON deserializer. Unknown/duplicate fields,
            // wrong types, trailing content and unsupported versions fail closed.
            private sealed class SettingsJsonReader
            {
                private readonly string text;
                private int position;

                public SettingsJsonReader(string text)
                {
                    this.text = text;
                    // Accept a UTF-8 BOM from ordinary local text editors.
                    if (text.Length > 0 && text[0] == '\uFEFF') position = 1;
                }

                public bool TryRead(out string key)
                {
                    key = null;
                    string candidate = null;
                    int fields = 0;
                    if (!Take('{')) return false;
                    for (int field = 0; field < 2; field++)
                    {
                        if (field > 0 && !Take(',')) return false;
                        if (!ReadString(out string name) || !Take(':')) return false;
                        if (name == "schemaVersion")
                        {
                            if ((fields & 1) != 0) return false;
                            SkipWhitespace();
                            string version = SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            for (int digit = 0; digit < version.Length; digit++)
                                if (position >= text.Length || text[position++] != version[digit]) return false;
                            fields |= 1;
                        }
                        else if (name == "apiKey")
                        {
                            if ((fields & 2) != 0 || !ReadString(out candidate)) return false;
                            fields |= 2;
                        }
                        else return false;
                    }
                    // A following digit, exponent, decimal point, third field or
                    // trailing comma cannot match the required delimiter.
                    if (fields != 3 || !Take('}')) return false;
                    SkipWhitespace();
                    return position == text.Length && TryNormalize(candidate, out key);
                }

                private bool Take(char expected)
                {
                    SkipWhitespace();
                    if (position >= text.Length || text[position] != expected) return false;
                    position++;
                    return true;
                }

                private void SkipWhitespace()
                {
                    while (position < text.Length)
                    {
                        char value = text[position];
                        if (value != ' ' && value != '\t' && value != '\r' && value != '\n') break;
                        position++;
                    }
                }

                private bool ReadString(out string value)
                {
                    value = null;
                    if (!Take('"')) return false;
                    var decoded = new StringBuilder();
                    while (position < text.Length)
                    {
                        char character = text[position++];
                        if (character == '"') { value = decoded.ToString(); return true; }
                        if (character < 32) return false;
                        if (character == '\\')
                        {
                            if (position >= text.Length) return false;
                            switch (text[position++])
                            {
                                case '"': character = '"'; break;
                                case '\\': character = '\\'; break;
                                case '/': character = '/'; break;
                                case 'b': character = '\b'; break;
                                case 'f': character = '\f'; break;
                                case 'n': character = '\n'; break;
                                case 'r': character = '\r'; break;
                                case 't': character = '\t'; break;
                                case 'u':
                                    if (!ReadHexCharacter(out character)) return false;
                                    if (char.IsHighSurrogate(character))
                                    {
                                        if (position + 1 >= text.Length || text[position++] != '\\'
                                            || text[position++] != 'u' || !ReadHexCharacter(out char low)
                                            || !char.IsLowSurrogate(low)) return false;
                                        decoded.Append(character); decoded.Append(low);
                                        continue;
                                    }
                                    if (char.IsLowSurrogate(character)) return false;
                                    break;
                                default: return false;
                            }
                        }
                        // Strict UTF-8 already rejects malformed raw surrogates;
                        // retain this guard so the string grammar is self-contained.
                        if (char.IsHighSurrogate(character))
                        {
                            if (position >= text.Length || !char.IsLowSurrogate(text[position])) return false;
                            decoded.Append(character); decoded.Append(text[position++]);
                        }
                        else
                        {
                            if (char.IsLowSurrogate(character)) return false;
                            decoded.Append(character);
                        }
                    }
                    return false;
                }

                private bool ReadHexCharacter(out char character)
                {
                    character = '\0';
                    if (position + 4 > text.Length) return false;
                    int value = 0;
                    for (int index = 0; index < 4; index++)
                    {
                        char digit = text[position++];
                        int number = digit >= '0' && digit <= '9' ? digit - '0'
                            : digit >= 'a' && digit <= 'f' ? digit - 'a' + 10
                            : digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : -1;
                        if (number < 0) return false;
                        value = (value << 4) | number;
                    }
                    character = (char)value;
                    return true;
                }
            }

            // Used only to set permissions on the local Unix file; no credential
            // manager, Windows registry or native Windows API is involved.
            [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
            private static extern int Chmod(string path, uint mode);
        }
    }
}
