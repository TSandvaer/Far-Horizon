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
            // No-bootstrap precondition (86cacyg63): a bare local EditMode run skips BootstrapProject.Run, so the
            // committed Boot.unity lacks the bootstrap-authored SettingsPanel → FindPanel returns null. Report
            // Inconclusive with a "run bootstrap first" hint INSTEAD of a regression-looking red. Inert once
            // bootstrap has run (panel present) — the real asserts below stand.
            BootstrapPrecondition.Require(panel, "SettingsPanel in Boot.unity");
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
            // No-bootstrap precondition (86cacyg63) — see BootScene_CarriesSettingsPanel_WithUIDocument.
            BootstrapPrecondition.Require(panel, "SettingsPanel in Boot.unity");
            Assert.IsNotNull(panel, "SettingsPanel must be present");

            Assert.IsNotNull(panel.orbit,
                "SettingsPanel.orbit must be wired editor-time (zoom + view-angle ranges bind to it — AC3); " +
                "an unwired ref would force an Awake FindObjectOfType in the build, which the editor-vs-runtime " +
                "discipline forbids as the ship path");
            Assert.IsNotNull(panel.wasd,
                "SettingsPanel.wasd must be wired editor-time (walk + run speed bind to it — AC3)");
        }

        [Test]
        public void BootScene_SettingsPanel_BerryBushes_WiredEditorTime_Serialized()
        {
            // 86cabn67w (Devon NIT #1) — the `Berry regrowth time` row's bush set must be wired EDITOR-TIME
            // (serialized by MovementCameraScene.WireBerryBushes POST-scatter) rather than left to the runtime
            // Awake FindObjectsByType fallback — the same editor-vs-runtime ship-path discipline the orbit/wasd/
            // thirst/hunger/stone targets follow (the stone-respawner runtime-Find that went DEAD is the
            // cautionary precedent). Drop the WireBerryBushes call and this goes red.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var panel = FindPanel(scene);
            // No-bootstrap precondition (86cacyg63) — see BootScene_CarriesSettingsPanel_WithUIDocument.
            BootstrapPrecondition.Require(panel, "SettingsPanel in Boot.unity");
            Assert.IsNotNull(panel, "SettingsPanel must be present");

            Assert.IsNotNull(panel.berryBushes,
                "SettingsPanel.berryBushes must be wired editor-time (WireBerryBushes serializes the full bush " +
                "set so the regrowth row fans out in the shipped build via the ship-path, not the Awake fallback)");
            Assert.IsTrue(panel.berryBushes.Length > 0,
                "the serialized berryBushes set must be non-empty (the fixed-position wired bush + the scatter " +
                "LP_BerryBush instances) — an empty set would force the runtime Awake FindObjectsByType fallback");
            foreach (var b in panel.berryBushes)
                Assert.IsNotNull(b,
                    "every serialized berryBushes entry must be a real BerryBush ref (no null holes from a stale wire)");
        }

        [Test]
        public void BootScene_SettingsPanel_FKeyMigrationSeams_WiredEditorTime_Serialized()
        {
            // 86caber95 — the F9 arm-pose rows + the F10 world-look rows must be wired EDITOR-TIME (serialized by
            // MovementCameraScene.BuildSettingsPanel (armPose) + WireWorldLookConsole (worldLook, post-environment))
            // rather than left to the runtime Awake FindObjectOfType fallback — the same editor-vs-runtime ship-path
            // discipline the orbit/wasd/berry targets follow (the stone-respawner runtime-Find that went DEAD is the
            // cautionary precedent). Drop either wire and this goes red. (The F7 camera-follow rows bind to
            // panel.orbit — already asserted above; ground-Y binds to panel.chopCharacter — wired by WireChopTree.)
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var panel = FindPanel(scene);
            BootstrapPrecondition.Require(panel, "SettingsPanel in Boot.unity");
            Assert.IsNotNull(panel, "SettingsPanel must be present");

            Assert.IsNotNull(panel.armPose,
                "SettingsPanel.armPose must be wired editor-time (the F9 arm-pose + run-lower rows bind to " +
                "CastawayArmPose — 86caber95 AC1); an unwired ref would force a runtime FindObjectOfType");
            Assert.IsNotNull(panel.worldLook,
                "SettingsPanel.worldLook must be wired editor-time (the F10 fog/sky/cloud/mountain/sun rows bind " +
                "to the WorldLookTunables seam — 86caber95 AC2; WireWorldLookConsole serializes it post-environment)");
        }

        [Test]
        public void BootScene_CarriesWorldLookTunablesSeam_Serialized()
        {
            // 86caber95 AC2 — the WorldLookTunables seam (the F10 migration's binding surface) must ship in
            // Boot.unity (the component-in-source-but-not-in-scene trap). BootstrapProject adds it to hudGo during
            // BuildEnvironment. Drop that AddComponent and the F10 world-look rows go dead in the build → red here.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            WorldLookTunables seam = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                seam = root.GetComponentInChildren<WorldLookTunables>(true);
                if (seam != null) break;
            }
            BootstrapPrecondition.Require(seam, "WorldLookTunables seam in Boot.unity");
            Assert.IsNotNull(seam,
                "the Boot scene must carry a WorldLookTunables seam serialized (else the F10-migrated world-look " +
                "console rows have no binding target in the shipped build — 86caber95 AC2)");
        }

        [Test]
        public void BootScene_SettingsPanel_WarmthWiredAndSplitKeys_Serialized()
        {
            // 86cah8ukr (F1/F3 split) — the split's editor-time wiring must reach the shipped Boot.unity (the
            // stale-committed-scene trap; unity-procedural-committed-assets-go-stale memory): (1) the WARMTH
            // target — the player-facing F1 warmth on/off toggle + warmth decay-rate slider bind to it — must be
            // serialized, not left to the runtime Awake FindObjectOfType fallback (the ship-path discipline the
            // orbit/wasd/berry/armPose/worldLook targets follow); (2) F1 opens the PLAYER Settings drawer and
            // F3 the DEV console (Sponsor-confirmed 2026-07-03), both wired editor-time. Drop the panel.warmth
            // wire or the toggle-key assignments in MovementCameraScene.BuildSettingsPanel and this goes red —
            // the guard that keeps the committed Boot.unity from silently regressing to a pre-split snapshot.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var panel = FindPanel(scene);
            BootstrapPrecondition.Require(panel, "SettingsPanel in Boot.unity");
            Assert.IsNotNull(panel, "SettingsPanel must be present");

            Assert.IsNotNull(panel.warmth,
                "SettingsPanel.warmth must be wired editor-time (the player-facing F1 warmth on/off + decay-rate " +
                "rows bind to it — 86cah8ukr); an unwired ref would force a runtime FindObjectOfType (ship-path).");
            Assert.AreEqual(KeyCode.F1, panel.toggleKey,
                "F1 opens the PLAYER Settings drawer (86cah8ukr split), serialized editor-time");
            Assert.AreEqual(KeyCode.F3, panel.devToggleKey,
                "F3 opens the DEV console (Sponsor-confirmed 2026-07-03), serialized editor-time");
        }

        [Test]
        public void BootScene_SettingsPanel_HasUIAssetsSerialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var panel = FindPanel(scene);
            // No-bootstrap precondition (86cacyg63) — see BootScene_CarriesSettingsPanel_WithUIDocument.
            BootstrapPrecondition.Require(panel, "SettingsPanel in Boot.unity");
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
            // No-bootstrap precondition (86cacyg63) — see BootScene_CarriesSettingsPanel_WithUIDocument.
            BootstrapPrecondition.Require(panel, "SettingsPanel in Boot.unity");
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
