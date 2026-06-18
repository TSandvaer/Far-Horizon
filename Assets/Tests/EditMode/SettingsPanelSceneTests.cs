using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Scene-presence guard for the SETTINGS PANEL (ticket 86caa4bqp) — the component-in-source-but-not-
    /// serialized-into-scene failure class (unity-conventions.md §editor-vs-runtime). The UI Toolkit panel
    /// only ships if the SettingsPanel + UIDocument + the live-target REFERENCES are actually serialized
    /// into Boot.unity by MovementCameraScene — an Awake-only add (or an unwired binding) would ship the
    /// panel inert / unbound. Binary scenes can't be GUID-grepped, so this EditMode reader is authoritative.
    ///
    /// Regression guard: delete the BuildSettingsPanel call in MovementCameraScene.Author (or drop the
    /// orbit/wasd wiring) and this goes red.
    /// </summary>
    public class SettingsPanelSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        private static SettingsPanel FindPanel(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var p = root.GetComponentInChildren<SettingsPanel>(true);
                if (p != null) return p;
            }
            return null;
        }

        [Test]
        public void BootScene_CarriesSettingsPanel_WithUIDocument()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var panel = FindPanel(scene);
            Assert.IsNotNull(panel,
                "the Boot scene must carry SettingsPanel serialized (else Esc opens nothing — the " +
                "component-not-serialized trap; unity-conventions.md)");
            Assert.IsNotNull(panel.GetComponent<UIDocument>(),
                "SettingsPanel needs a UIDocument on the same GameObject (the UI Toolkit host)");
        }

        [Test]
        public void BootScene_SettingsPanel_BindsLiveTargets_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var panel = FindPanel(scene);
            Assert.IsNotNull(panel, "SettingsPanel must be present");

            Assert.IsNotNull(panel.orbit,
                "SettingsPanel.orbit must be wired editor-time (zoom + view-angle ranges bind to it — AC3); " +
                "an unwired ref would force an Awake FindObjectOfType in the build, which the editor-vs-runtime " +
                "discipline forbids as the ship path");
            Assert.IsNotNull(panel.wasd,
                "SettingsPanel.wasd must be wired editor-time (walk + run speed bind to it — AC3)");
        }

        [Test]
        public void BootScene_SettingsPanel_HasUIAssetsSerialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var panel = FindPanel(scene);
            Assert.IsNotNull(panel, "SettingsPanel must be present");

            var doc = panel.GetComponent<UIDocument>();
            Assert.IsNotNull(doc.panelSettings,
                "the UIDocument must carry a PanelSettings asset (else UI Toolkit renders nothing in the build)");
            Assert.IsNotNull(panel.panelUxml,
                "the panel UXML must be serialized (the shell ships from the asset, not rebuilt blind)");
            Assert.IsNotNull(panel.paletteUss,
                "the carved-wood Palette.uss must be serialized (the panel reads as on-tone, not unstyled)");
            Assert.IsNotNull(panel.panelUss,
                "SettingsPanel.uss must be serialized (the workbench-drawer layout + archetype row classes)");
        }
    }
}
