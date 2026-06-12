using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Headless Windows-standalone build for Far Horizon. SliceBuilder-class, named for the
    /// production project. Run via:
    ///   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.FarHorizonBuilder.BuildWindows
    /// Output: Build/Windows/FarHorizon.exe
    ///
    /// Exits the editor with a non-zero code on build failure so CI / the bootstrap chain fails
    /// loud instead of green-on-failed-build.
    /// </summary>
    public static class FarHorizonBuilder
    {
        public const string BootScene = "Assets/Scenes/Boot.unity";
        public const string ExeName = "FarHorizon.exe";

        public static void BuildWindows()
        {
            string outDir = Path.GetFullPath("Build/Windows");
            Directory.CreateDirectory(outDir);
            string exe = Path.Combine(outDir, ExeName);

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { BootScene },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary summary = report.summary;
            Debug.Log($"[FarHorizonBuilder] result={summary.result} size={summary.totalSize} bytes " +
                      $"time={summary.totalTime} -> {exe}");
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[FarHorizonBuilder] BUILD FAILED");
                EditorApplication.Exit(2);
            }
            EditorApplication.Exit(0);
        }
    }
}
