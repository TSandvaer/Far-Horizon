using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards that the FPS counter (ticket 86cahmxmt) actually SHIPS — the two serialized-state
    /// traps a component-in-source can die of:
    ///
    ///  1. component-not-serialized-into-scene (unity-conventions §editor-vs-runtime): the counter is only
    ///     in the shipped exe if BootstrapProject.BuildBootScene's AddComponent is SAVED into Boot.unity —
    ///     an Awake-only add would not ship. Delete that line and the first test goes red.
    ///  2. the dead-knob class (the stone-respawner lesson): the `FPS counter` console row is only LIVE in
    ///     the shipped build if BuildSettingsPanel WIRED the panel's fpsHud target serialized — a row whose
    ///     target relies solely on a runtime FindObjectOfType is one refactor away from silently dead.
    ///     Delete the panel.fpsHud wire in MovementCameraScene.BuildSettingsPanel and the third test goes red.
    ///
    /// Also pins the shipped DEFAULT = ENABLED (ON): this first build deliberately shows the counter so the
    /// Sponsor sees it at soak ("default — Sponsor-soak tunes" — he dials it via the F1 console row, which
    /// persists). A future default flip is a deliberate edit HERE, not an accident.
    ///
    /// (CI always bootstraps before EditMode, so these read the freshly-baked scene; a BARE local run
    /// without bootstrap false-REDs scene-presence tests — unity-conventions §Headless rituals.)
    /// </summary>
    public class FpsCounterHudSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        private static T FindInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        [Test]
        public void BootScene_CarriesFpsCounterHud_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var fps = FindInScene<FpsCounterHud>(scene);
            Assert.IsNotNull(fps,
                "the Boot scene must carry FpsCounterHud serialized (the shipped exe only shows the FPS " +
                "readout if BuildBootScene's AddComponent is SAVED into the scene — the component-not-" +
                "serialized trap; unity-conventions.md)");
        }

        [Test]
        public void BootScene_FpsCounterShipsEnabled_TheDefaultOnContract()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var fps = FindInScene<FpsCounterHud>(scene);
            Assert.IsNotNull(fps, "precondition: the scene carries the counter (guarded above)");
            Assert.IsTrue(fps.enabled,
                "the FPS counter ships ENABLED (default ON — deliberate for this first build so the Sponsor " +
                "sees it at soak; he tunes always-on vs off via the F1 console's `FPS counter` row, 86cahmxmt). " +
                "Flipping the shipped default is a deliberate edit here + in the bootstrap, not an accident.");
        }

        [Test]
        public void BootScene_SettingsPanelFpsTarget_IsWiredSerialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var panel = FindInScene<SettingsPanel>(scene);
            Assert.IsNotNull(panel, "precondition: the Boot scene carries the SettingsPanel (dev console)");
            Assert.IsNotNull(panel.fpsHud,
                "BuildSettingsPanel must wire panel.fpsHud SERIALIZED (the editor-vs-runtime ship-path " +
                "discipline the stone-respawner dead-knob taught) — a row relying solely on the Awake " +
                "FindObjectOfType fallback is one refactor away from a silently dead `FPS counter` toggle");
        }
    }
}
