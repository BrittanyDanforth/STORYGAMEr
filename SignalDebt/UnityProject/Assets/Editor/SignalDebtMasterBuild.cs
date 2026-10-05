#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SignalDebt.Editor
{
    // One reproducible entry point for the complete source package. This calls
    // Unity's real importer/compiler/player builder; packaging a ZIP is not a build.
    public static class SignalDebtMasterBuild
    {
        [MenuItem("Signal Debt/Build Windows Player")]
        public static void BuildWindows()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("Stop Play Mode before building SIGNALDEBT.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Install Windows Build Support for this Unity editor in Unity Hub.");
            string output = Path.GetFullPath(OutputDirectory());
            string assets = Path.GetFullPath(Application.dataPath);
            if (output == assets || output.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Choose a build output outside Assets.");
            if (!SignalDebtSceneBuilder.BuildMasterScene()) return;
            Directory.CreateDirectory(output);
            PlayerSettings.productName = "SIGNALDEBT";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Apartment.unity" },
                locationPathName = Path.Combine(output, "SIGNALDEBT.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("SIGNALDEBT build failed: " + report.summary.result +
                    " (" + report.summary.totalErrors + " errors). See the Unity build log.");
            Debug.Log("SIGNALDEBT Windows build completed: " + options.locationPathName +
                ". Play Mode and target-machine acceptance remain separate checks.");
        }

        private static string OutputDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
                if (arguments[i] == "-signalDebtOutput")
                {
                    if (i + 1 == arguments.Length || arguments[i + 1].StartsWith("-"))
                        throw new BuildFailedException("-signalDebtOutput requires a directory.");
                    return arguments[i + 1];
                }
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Windows");
        }
    }
}
#endif
