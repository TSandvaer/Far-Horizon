using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Guards FarHorizonBuilder.ResolveScenes — the scene-list resolution that respects
    /// EditorBuildSettings.scenes instead of hardcoding Boot.unity (ticket 86ca8a0uz).
    ///
    /// Regression guard: if a future change reverts to a hardcoded single-scene list, the
    /// "honours a multi-scene enabled list" and "drops disabled scenes" cases fail; if the
    /// empty-list fallback is removed, the fallback case fails. ResolveScenes takes its input
    /// as a parameter (not the live EditorBuildSettings) so these run without mutating project
    /// state.
    /// </summary>
    public class FarHorizonBuilderTests
    {
        private static EditorBuildSettingsScene Scene(string path, bool enabled) =>
            new EditorBuildSettingsScene(path, enabled);

        [Test]
        public void Resolve_EnabledScenes_PassedThroughInOrder()
        {
            var input = new[]
            {
                Scene("Assets/Scenes/Boot.unity", true),
                Scene("Assets/Scenes/Repro.unity", true),
            };

            var resolved = FarHorizonBuilder.ResolveScenes(input);

            Assert.AreEqual(
                new[] { "Assets/Scenes/Boot.unity", "Assets/Scenes/Repro.unity" },
                resolved,
                "enabled scenes must ship in their Build Settings order, not a hardcoded list");
        }

        [Test]
        public void Resolve_DisabledScenes_AreDropped()
        {
            var input = new[]
            {
                Scene("Assets/Scenes/Boot.unity", true),
                Scene("Assets/Scenes/Disabled.unity", false),
            };

            var resolved = FarHorizonBuilder.ResolveScenes(input);

            Assert.AreEqual(new[] { "Assets/Scenes/Boot.unity" }, resolved,
                "only ENABLED scenes ship; a disabled Build Settings entry must be excluded");
        }

        [Test]
        public void Resolve_EmptyList_FallsBackToBootSceneWithWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("falling back to.*boot scene"));

            var resolved = FarHorizonBuilder.ResolveScenes(new EditorBuildSettingsScene[0]);

            Assert.AreEqual(new[] { FarHorizonBuilder.BootScene }, resolved,
                "an empty enabled-scene list must fall back to the boot scene so the build is never sceneless");
        }

        [Test]
        public void Resolve_AllDisabled_FallsBackToBootSceneWithWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("falling back to.*boot scene"));

            var input = new[] { Scene("Assets/Scenes/Boot.unity", false) };
            var resolved = FarHorizonBuilder.ResolveScenes(input);

            Assert.AreEqual(new[] { FarHorizonBuilder.BootScene }, resolved,
                "a list with no ENABLED scenes is effectively empty — fall back to the boot scene");
        }

        [Test]
        public void Resolve_NullInput_FallsBackToBootSceneWithWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("falling back to.*boot scene"));

            var resolved = FarHorizonBuilder.ResolveScenes(null);

            Assert.AreEqual(new[] { FarHorizonBuilder.BootScene }, resolved,
                "null EditorBuildSettings.scenes must fall back, not throw");
        }

        // === -development build shape (86cahhfp4 S2a) ====================================================
        // The flag turns the SAME entry point into a profiler-connectable Development build routed to a
        // SEPARATE output dir; without it, the release shape is pinned byte-identical to the pre-S2a
        // behavior (Build/Windows + BuildOptions.None) — the "no default-build behavior change" constraint.

        [Test]
        public void DevelopmentFlag_Absent_ReleaseShape_IsUnchanged()
        {
            string[] typicalCiArgs =
            {
                "Unity.exe", "-batchmode", "-quit", "-projectPath", "X",
                "-executeMethod", "FarHorizon.EditorTools.FarHorizonBuilder.BuildWindows",
            };

            Assert.IsFalse(FarHorizonBuilder.ResolveDevelopmentFlag(typicalCiArgs),
                "without -development the build must stay the release shape (no default-behavior change)");
            Assert.AreEqual("Build/Windows", FarHorizonBuilder.ResolveOutputDir(false),
                "the release build must keep landing at the canonical Build/Windows soak/CI artifact path");
            Assert.AreEqual(BuildOptions.None, FarHorizonBuilder.ResolveBuildOptions(false),
                "the release build must keep BuildOptions.None — S2a adds a NEW shape, it does not alter the old one");
        }

        [Test]
        public void DevelopmentFlag_Present_BuildsDevelopmentShape_InSeparateDir()
        {
            string[] args =
            {
                "Unity.exe", "-batchmode", "-quit", "-projectPath", "X",
                "-executeMethod", "FarHorizon.EditorTools.FarHorizonBuilder.BuildWindows",
                FarHorizonBuilder.DevelopmentArg,
            };

            Assert.IsTrue(FarHorizonBuilder.ResolveDevelopmentFlag(args),
                "-development on the command line must select the development build shape");
            Assert.AreEqual("Build/WindowsDev", FarHorizonBuilder.ResolveOutputDir(true),
                "the dev build must land in its OWN dir — sharing Build/Windows would let a profiling build " +
                "silently replace the canonical soak exe (same HUD stamp sha; the stamp ritual cannot tell them apart)");
            Assert.AreEqual(BuildOptions.Development, FarHorizonBuilder.ResolveBuildOptions(true),
                "the dev shape adds exactly BuildOptions.Development (profiler-connectable) — no AllowDebugging " +
                "(script-debugger overhead skews profiles), no ConnectWithProfiler (captures use -profiler-log-file)");
        }

        [Test]
        public void DevelopmentFlag_NullArgs_DefaultsToReleaseShape()
        {
            Assert.IsFalse(FarHorizonBuilder.ResolveDevelopmentFlag(null),
                "null args must resolve to the release shape, not throw");
        }
    }
}
