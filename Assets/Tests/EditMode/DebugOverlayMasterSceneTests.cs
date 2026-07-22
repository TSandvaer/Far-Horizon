using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the F10 DEBUG-OVERLAY MASTER (ticket 86caju054 — re-homed off the RETIRED
    /// SneakIsolationTool). When the Sponsor retired the sneak-isolation panel ("the sneak isolation panel should
    /// die"), the F10 master that flips the shared <see cref="DebugOverlays.Visible"/> layer moved to the neutral
    /// <see cref="DebugOverlayMaster"/> so F10 keeps toggling the remaining overlays (WorldLookNudgeTool, AxeNudge,
    /// FloatDiagnostic, the held-weapon/pond panels). These pin the load-bearing contracts:
    ///   1. SCENE PRESENCE — the master must SERIALIZE onto Boot (the component-in-source-but-not-in-scene trap;
    ///      a missing component ships the F10 toggle inert → the whole overlay layer becomes unreachable). CI
    ///      bootstraps before EditMode, so the freshly-baked scene carries it; a bare LOCAL run against a stale
    ///      committed Boot.unity may red here until the scene is regenerated (unity-conventions.md
    ///      §"Run BootstrapProject.Run BEFORE any LOCAL EditMode run").
    ///   2. F10 IS THE MASTER + F1 STAYS FREE — the toggle key is F10 (grouped with the WorldLookNudgeTool, also
    ///      F10) and NOT F1 (F1 is the dev console). A regression that re-binds this to F1 (re-consuming the
    ///      console key) or drops F10 fails HERE.
    ///
    /// (Sibling guard: CaptureGateSceneTests.BootScene_F2Unbound_F10IsTheOverlayMaster asserts F2 stays UNBOUND
    /// while this master rides F10.)
    /// </summary>
    public class DebugOverlayMasterSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        private Scene OpenBoot()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            return scene;
        }

        private T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        [Test]
        public void BootScene_HasDebugOverlayMaster_OnBoot()
        {
            var scene = OpenBoot();
            var master = FindInScene<DebugOverlayMaster>(scene);
            Assert.IsNotNull(master,
                "the Boot scene must carry the DebugOverlayMaster (the F10 debug-overlay master, re-homed off the " +
                "retired SneakIsolationTool in 86caju054) — a missing component ships the F10 toggle inert, making " +
                "the whole dev-overlay layer unreachable (the component-in-source-but-not-in-scene trap; this " +
                "scene-presence assert is the authoritative reader for the binary scene).");
        }

        [Test]
        public void DebugOverlayMaster_ShowHide_IsF10_GroupedWithWorldLook_F1IsFree()
        {
            var scene = OpenBoot();
            var master = FindInScene<DebugOverlayMaster>(scene);
            Assert.IsNotNull(master);
            Assert.AreEqual(KeyCode.F10, master.overlayToggleKey,
                "the debug-overlay show/hide must be F10 — grouped with the WorldLookNudgeTool (also F10) so F10 " +
                "toggles the debug overlays together (86cah90cp / re-homed 86caju054).");
            Assert.AreNotEqual(KeyCode.F1, master.overlayToggleKey,
                "F1 must NOT be consumed by the debug-overlay master — F1 is the dev console (86cah90cp).");
            // Same F10 as the WorldLookNudgeTool's default toggle — the grouping is the point.
            var world = new GameObject("WorldTool").AddComponent<WorldLookNudgeTool>();
            try
            {
                Assert.AreEqual(world.toggleKey, master.overlayToggleKey,
                    "the debug-overlay master must share the WorldLookNudgeTool's F10 (the grouping).");
            }
            finally { Object.DestroyImmediate(world.gameObject); }
        }
    }
}
