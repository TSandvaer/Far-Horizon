using System.Collections.Generic;
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
    ///
    /// Scene list: the build ships the ENABLED scenes from EditorBuildSettings.scenes (whatever
    /// the editor / bootstrap registered), NOT a hardcoded list. This means a diagnostic build
    /// configuration set up in Build Settings (e.g. a repro scene) is honoured instead of being
    /// silently overridden — the silent-divergence failure class the shipped-build gate exists to
    /// catch (ticket 86ca8a0uz). Only when the enabled list is EMPTY does it fall back to the boot
    /// scene, with a logged warning. In CI, BootstrapProject.Run registers Boot.unity into
    /// EditorBuildSettings before this runs, so the shipped scene set is unchanged.
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

            string[] scenes = ResolveScenes(EditorBuildSettings.scenes);
            Debug.Log($"[FarHorizonBuilder] building {scenes.Length} scene(s): {string.Join(", ", scenes)}");

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
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

        /// <summary>
        /// Resolve the scene paths to ship: the ENABLED scenes from EditorBuildSettings, in order.
        /// Falls back to the boot scene (with a logged warning) when no enabled scenes are
        /// configured, so a headless build is never accidentally sceneless. Pure + side-effect-free
        /// (apart from the warning log) so EditMode tests can exercise the resolution directly.
        /// </summary>
        public static string[] ResolveScenes(EditorBuildSettingsScene[] buildSettingsScenes)
        {
            var enabled = new List<string>();
            if (buildSettingsScenes != null)
            {
                foreach (var s in buildSettingsScenes)
                {
                    if (s != null && s.enabled && !string.IsNullOrEmpty(s.path))
                        enabled.Add(s.path);
                }
            }

            if (enabled.Count == 0)
            {
                Debug.LogWarning(
                    "[FarHorizonBuilder] EditorBuildSettings has no enabled scenes — falling back to " +
                    $"the boot scene ({BootScene}). Register scenes in Build Settings (or run " +
                    "BootstrapProject.Run) to ship a custom scene set.");
                return new[] { BootScene };
            }

            return enabled.ToArray();
        }
    }
}
