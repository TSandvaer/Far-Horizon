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

        [Test]
        public void BootScene_UIDocument_HasNoVisualTreeAsset_SinglePanelOwnsTheClone()
        {
            // Regression guard for the codereview-#83 DOUBLE-CLONE: if the serialized UIDocument carries a
            // visualTreeAsset, it auto-clones the shell on enable AND SettingsPanel.BuildView CloneTree's the
            // SAME panelUxml again → two settings-scrim copies, the second an always-visible orphan overlay
            // Q("settings-scrim") never binds/hides. The panel must own the SINGLE clone, so the UIDocument's
            // visualTreeAsset stays UNASSIGNED while panel.panelUxml carries the shell asset.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var panel = FindPanel(scene);
            Assert.IsNotNull(panel, "SettingsPanel must be present");

            var doc = panel.GetComponent<UIDocument>();
            Assert.IsNull(doc.visualTreeAsset,
                "the UIDocument must NOT carry a visualTreeAsset — SettingsPanel.BuildView owns the single " +
                "CloneTree; assigning it here double-clones the shell into a duplicate orphan settings-scrim " +
                "overlay (codereview #83)");
            Assert.IsNotNull(panel.panelUxml,
                "the panel still needs panelUxml serialized so BuildView's single clone renders the shell " +
                "(the build-safety-net BuildShellInCode only fires when this is null)");
        }
    }
}
