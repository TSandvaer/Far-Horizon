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

        /// <summary>Default (release-shape) output dir — the canonical soak/CI artifact location.</summary>
        public const string ReleaseOutDir = "Build/Windows";

        /// <summary>Development-build output dir (86cahhfp4 S2a). DELIBERATELY separate from
        /// <see cref="ReleaseOutDir"/> so a profiling build can never be mistaken for (or overwrite) the
        /// canonical soak exe — the two would carry the same HUD stamp sha, so the build-stamp ritual could
        /// NOT tell them apart if they shared a path. Both live under gitignored Build/.</summary>
        public const string DevelopmentOutDir = "Build/WindowsDev";

        /// <summary>The -development launch arg (86cahhfp4 S2a): pass it on the BuildWindows command line
        /// (Unity forwards unknown args) to build a Development player — profiler-connectable, accepts
        /// -profiler-log-file/-profiler-capture-frame-count, DEVELOPMENT_BUILD defined. Without it the build
        /// is byte-for-byte the same release-shape build as before (no default-behavior change).</summary>
        public const string DevelopmentArg = "-development";

        public static void BuildWindows()
        {
            bool development = ResolveDevelopmentFlag(System.Environment.GetCommandLineArgs());
            string outDir = Path.GetFullPath(ResolveOutputDir(development));
            Directory.CreateDirectory(outDir);
            string exe = Path.Combine(outDir, ExeName);

            string[] scenes = ResolveScenes(EditorBuildSettings.scenes);
            Debug.Log($"[FarHorizonBuilder] building {scenes.Length} scene(s): {string.Join(", ", scenes)}" +
                      (development ? " [DEVELOPMENT build — profiler-connectable, NOT a soak/ship artifact]" : ""));

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = ResolveBuildOptions(development)
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary summary = report.summary;
            Debug.Log($"[FarHorizonBuilder] result={summary.result} size={summary.totalSize} bytes " +
                      $"time={summary.totalTime} development={development} -> {exe}");
            // R5 (86cahne3d) — informational strip report riding build.log (no new CI step; the shader-variant
            // detail Unity emits inline as "Compiling shader ... N variants"). BuildReport.strippingInfo lists
            // the engine modules kept + why; logging it gives a before/after artifact for the unpin's effect on
            // build content. Null on some build shapes (guarded) — informational only, never asserted.
            LogStrippingInfo(report);
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[FarHorizonBuilder] BUILD FAILED");
                EditorApplication.Exit(2);
            }
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// R5 (86cahne3d) — log the build's StrippingInfo (engine modules kept + inclusion reasons) into
        /// build.log. Informational: it is the cheap artifact for confirming the AlwaysIncludedShaders unpin
        /// changed what the build carries, alongside the size=/time= line above and Unity's own inline
        /// "Compiling shader ... N variants" lines. strippingInfo can be null (non-IL2CPP / some shapes) —
        /// guarded so a null never faults the build. Never asserted; a diff of two build logs is the signal.
        /// </summary>
        private static void LogStrippingInfo(BuildReport report)
        {
            // Wholly defensive: this is informational logging that runs AFTER a successful build, so an
            // uncaught throw here would fail the batchmode -executeMethod step despite a good build. Guard
            // everything (null report / null strippingInfo / null modules) + swallow any exception.
            try
            {
                var info = report != null ? report.strippingInfo : null;
                if (info == null)
                {
                    Debug.Log("[FarHorizonBuilder] strippingInfo: none reported (build shape does not expose it)");
                    return;
                }
                var modules = info.includedModules;
                if (modules == null)
                {
                    Debug.Log("[FarHorizonBuilder] strippingInfo: present but includedModules is null");
                    return;
                }
                int count = 0;
                var sb = new System.Text.StringBuilder();
                foreach (var m in modules)
                {
                    count++;
                    sb.Append(m).Append("; ");
                }
                Debug.Log($"[FarHorizonBuilder] strippingInfo: {count} included module(s): {sb}");
            }
            catch (System.Exception e)
            {
                Debug.Log("[FarHorizonBuilder] strippingInfo: skipped (informational; " + e.GetType().Name + ")");
            }
        }

        /// <summary>
        /// Whether the command line asks for a DEVELOPMENT build (86cahhfp4 S2a — the honest-profiling
        /// enabler: unity6-mastery §4 mandates profiling the built player, and a release-shape player
        /// carries no profiler). Pure + parameterized (same testability pattern as
        /// <see cref="ResolveScenes"/>) so EditMode guards pin both sides without a real command line.
        /// </summary>
        public static bool ResolveDevelopmentFlag(string[] args)
        {
            if (args == null) return false;
            foreach (string a in args)
                if (a == DevelopmentArg) return true;
            return false;
        }

        /// <summary>Output dir per build shape — dev builds land in <see cref="DevelopmentOutDir"/> so the
        /// canonical <see cref="ReleaseOutDir"/> soak artifact is never silently replaced by a profiling
        /// build (pure; EditMode-pinned).</summary>
        public static string ResolveOutputDir(bool development) =>
            development ? DevelopmentOutDir : ReleaseOutDir;

        /// <summary>BuildOptions per build shape (pure; EditMode-pinned). Development adds ONLY
        /// BuildOptions.Development — no AllowDebugging (script-debugger overhead would skew profiles) and
        /// no ConnectWithProfiler (captures here run via -profiler-log-file on the player, not an editor
        /// auto-connect).</summary>
        public static BuildOptions ResolveBuildOptions(bool development) =>
            development ? BuildOptions.Development : BuildOptions.None;

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
