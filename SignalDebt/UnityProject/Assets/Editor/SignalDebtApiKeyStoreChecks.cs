#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace SignalDebt.Editor
{
    /// <summary>Small Unity Editor settings-codec/filesystem compatibility probe, using an
    /// isolated LocalStore, an explicitly empty environment provider, and fabricated data.</summary>
    public static class SignalDebtApiKeyStoreChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int assertions;
            public string unity_version, store_source_sha256, checks_source_sha256, failure;
            public bool actual_unity_runtime = true;
            public bool actual_local_filesystem = true;
            public bool actual_pure_csharp_settings_codec = true;
            public bool real_credentials_accessed = false;
            public int provider_network_calls = 0;
            public string scope = "Isolated fabricated-data LocalStore session/no-write, remember, fresh-instance pure-C# JSON codec roundtrip, replacement and deletion under actual Unity Editor Mono. Not standalone-player, live API or user settings acceptance.";
        }

        private static int assertions;

        [MenuItem("Signal Debt/Validation/Run API Key Store Compatibility Checks")]
        public static void RunMenu()
        {
            Report report = Execute();
            if (!report.passed) throw new InvalidOperationException(report.failure);
        }

        // -executeMethod SignalDebt.Editor.SignalDebtApiKeyStoreChecks.RunBatch
        // -signalDebtApiKeyStoreReport /absolute/report.json
        public static void RunBatch()
        {
            Report report = Execute();
            EditorApplication.Exit(report.passed && report.assertions > 0 ? 0 : 1);
        }

        private static Report Execute()
        {
            assertions = 0;
            string directory = Path.Combine(Path.GetTempPath(), "SignalDebtSyntheticKeyStore-" + Guid.NewGuid().ToString("N"));
            var report = new Report { unity_version = Application.unityVersion, failure = "" };
            try
            {
                report.store_source_sha256 = Hash(Path.Combine(Application.dataPath, "Scripts/SignalDebtApiKeyStore.cs"));
                report.checks_source_sha256 = Hash(Path.Combine(Application.dataPath, "Editor/SignalDebtApiKeyStoreChecks.cs"));
                const string fabricated = "sk-test-only-not-a-real-key-0000000000";
                const string replacement = "sk-test-only-not-a-real-key-1111111111";
                // Never use the static default Store. This nested instance receives both
                // an isolated directory and a provider that cannot read process secrets.
                Type localStore = typeof(SignalDebtApiKeyStore).GetNestedType("LocalStore", BindingFlags.NonPublic);
                Check(localStore != null, "isolated LocalStore seam exists");
                ConstructorInfo constructor = localStore.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(Func<string>), typeof(Func<string>) }, null);
                Check(constructor != null, "isolated directory and environment providers are injectable");
                Func<string> location = () => directory;
                Func<string> emptyEnvironment = () => null;
                object store = constructor.Invoke(new object[] { location, emptyEnvironment });
                Check(Call(store, "SetSessionKey", new object[] { fabricated, null }), "synthetic session value is accepted");
                Check(!Directory.Exists(directory), "session-only entry writes no folder or file");
                object[] loaded = { null };
                Check(Call(store, "TryLoad", loaded) && (string)loaded[0] == fabricated, "session-only value loads within its instance");
                Check(Call(store, "TrySave", new object[] { fabricated, null }), "remember writes through the production pure-C# settings codec");
                string path = Path.Combine(directory, "ai-settings.json");
                Check(File.Exists(path) && Directory.GetFiles(directory).Length == 1 && File.ReadAllText(path).Contains(fabricated),
                    "explicit remember creates exactly one readable local settings file");
                object reopened = constructor.Invoke(new object[] { location, emptyEnvironment });
                loaded = new object[] { null };
                Check(Call(reopened, "TryLoad", loaded) && (string)loaded[0] == fabricated,
                    "fresh LocalStore decodes remembered data in actual Unity Editor Mono");
                Check(Call(reopened, "TrySave", new object[] { replacement, null }), "existing remembered file replacement succeeds on this platform");
                object replaced = constructor.Invoke(new object[] { location, emptyEnvironment });
                loaded = new object[] { null };
                Check(Call(replaced, "TryLoad", loaded) && (string)loaded[0] == replacement && Directory.GetFiles(directory).Length == 1,
                    "replacement roundtrip leaves no staging or backup credential file");
                Check(Call(replaced, "TryDelete", new object[] { null }), "forget removes isolated settings");
                loaded = new object[] { null };
                Check(!File.Exists(path) && !Call(replaced, "TryLoad", loaded) && loaded[0] == null,
                    "forget clears remembered and session data with no environment fallback");
                Check(assertions >= 12, "all intended compatibility checks ran");
                report.passed = true;
            }
            catch (Exception error)
            {
                // No values or exception bodies from the credential store enter reports.
                report.failure = error is InvalidOperationException ? error.Message :
                    "Synthetic compatibility probe failed: " + error.GetType().Name;
            }
            finally
            {
                report.assertions = assertions;
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
                catch (Exception) { report.passed = false; report.failure += " Synthetic fixture cleanup failed."; }
            }
            try
            {
                string path = ReportPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true) + "\n");
                Debug.Log((report.passed ? "PASS" : "FAIL") + ": " + report.assertions +
                    " synthetic key-store compatibility assertions in Unity " + report.unity_version + ". Report: " + path);
            }
            catch (Exception) { report.passed = false; report.failure += " Compatibility report could not be written."; }
            if (!report.passed) Debug.LogError(report.failure);
            return report;
        }

        private static bool Call(object store, string name, object[] arguments)
        {
            MethodInfo method = store.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            if (method == null) throw new InvalidOperationException("Missing isolated LocalStore method: " + name);
            return (bool)method.Invoke(store, arguments);
        }

        private static void Check(bool condition, string description)
        { assertions++; if (!condition) throw new InvalidOperationException("FAIL: " + description); }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        private static string ReportPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-signalDebtApiKeyStoreReport")
                {
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException("-signalDebtApiKeyStoreReport requires a file path.");
                    return Path.GetFullPath(args[i + 1]);
                }
            return Path.Combine(Application.temporaryCachePath, "SignalDebtApiKeyStoreChecks.json");
        }
    }
}
#endif
