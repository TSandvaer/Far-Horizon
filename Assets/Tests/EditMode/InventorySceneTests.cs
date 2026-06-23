using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the inventory/belt UI (ticket 86caa4bya) is SERIALIZED into the Boot scene the
    /// exe ships — not added at Awake (the component-in-source-but-not-in-scene silent-inert class,
    /// unity-conventions.md §editor-vs-runtime: CaptureGate.cs existed in source but Boot had no component,
    /// so the gate produced zero frames). A binary scene can't be GUID-grepped, so this EditMode scene
    /// read is the only authoritative reader. Drop the BuildInventoryUI / BuildAxePickup wiring and this
    /// reds in headless CI, rather than the shipped build silently lacking the inventory.
    /// </summary>
    public class InventorySceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesInventoryUI_WiredWithUIDocumentAndInventory()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            InventoryUI ui = FindInScene<InventoryUI>(scene);
            // No-bootstrap precondition (86cacyg63): a bare local EditMode run skips BootstrapProject.Run, so the
            // committed Boot.unity lacks the bootstrap-authored InventoryUI wiring + EnsureRuntimeTheme .tss →
            // these refs go null. Report Inconclusive with a "run bootstrap first" hint INSTEAD of a misleading
            // null/assertion red. Inert once bootstrap has run (all refs non-null) — the real asserts below stand.
            BootstrapPrecondition.Require(ui, "InventoryUI component in Boot.unity");
            BootstrapPrecondition.Require(ui.document, "InventoryUI.document (UIDocument)");
            BootstrapPrecondition.Require(ui.document.panelSettings, "InventoryUI UIDocument.panelSettings");
            BootstrapPrecondition.Require(ui.document.panelSettings.themeStyleSheet,
                "InventoryUI PanelSettings.themeStyleSheet (EnsureRuntimeTheme .tss)");

            Assert.IsNotNull(ui,
                "the Boot scene must carry the InventoryUI component serialized into the scene (the UI " +
                "Toolkit inventory pack + belt hotbar) — not an Awake add (editor-vs-runtime trap)");
            Assert.IsNotNull(ui.document,
                "InventoryUI's UIDocument must be wired editor-time so the panel renders in the built exe");
            Assert.IsNotNull(ui.document.panelSettings,
                "the UIDocument must have a PanelSettings (or UI Toolkit renders nothing in the build)");
            // BLOCKER-1 regression guard (PR #90): a PanelSettings with a NULL themeStyleSheet THROWS
            // during panel init in the shipped player → the capture gate produced ZERO frames on run
            // 27810143643 (the prior code loaded the theme from two non-existent package paths). The
            // panelSettings-non-null assert above did NOT catch it — the asset existed, its THEME was null.
            var theme = ui.document.panelSettings.themeStyleSheet;
            Assert.IsNotNull(theme,
                "the inventory PanelSettings must carry a NON-NULL themeStyleSheet — a UIDocument whose " +
                "PanelSettings has no theme throws at startup in the build and zeroes the capture gate " +
                "(EnsureRuntimeTheme must resolve or create one)");
            // ROUND 2 (PR #90): a NON-NULL theme is NECESSARY but NOT SUFFICIENT. An EMPTY .tss imports to
            // a non-null BUT BASE-LESS ThemeStyleSheet — the panel resolves its first repaint against no
            // base styles and NEVER completes the first frame's layout → the exe HANGS ("did not self-quit
            // within 120s", 0 frames, run 27810923815). The non-null assert was a FALSE-GREEN PROXY. Guard
            // the RESOLVING condition: the theme's SOURCE .tss must @import the runtime default base theme
            // (the one line that makes a .tss a usable runtime theme). The imported object is non-null
            // either way, so the .tss source text is the only honest signal (perceptual-vs-proxy guard,
            // unity-conventions.md §editor-vs-runtime "guard the percept, not a proxy").
            string themePath = UnityEditor.AssetDatabase.GetAssetPath(theme);
            Assert.IsFalse(string.IsNullOrEmpty(themePath),
                "the inventory theme must be a project asset with a resolvable .tss path");
            Assert.IsTrue(System.IO.File.Exists(themePath),
                "the inventory theme .tss must be a readable on-disk project asset at " + themePath +
                " (EnsureRuntimeTheme assigns either a verified project theme or the one it creates)");
            string themeSrc = System.IO.File.ReadAllText(themePath);
            StringAssert.Contains("unity-theme://default", themeSrc,
                "the inventory theme .tss must @import url(\"unity-theme://default\") so it carries Unity's " +
                "runtime base styles — an EMPTY/base-less .tss is non-null but HANGS the shipped exe at " +
                "panel init (capture gate 0 frames, run 27810923815). EnsureRuntimeTheme must write the " +
                "import content, not an empty file.");
            Assert.IsNotNull(ui.inventory,
                "InventoryUI's Inventory reference must be wired editor-time so it binds to the model " +
                "without an Awake-time scene search in the build");
            Assert.IsNotNull(ui.panelUxml,
                "the panel UXML must be wired editor-time so the shell serializes (not built fresh in code)");
        }

        [Test]
        public void BootScene_CarriesAxePickup_WiredToInventoryAndPlayer()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            AxePickup pickup = FindInScene<AxePickup>(scene);
            // No-bootstrap precondition (86cacyg63) — see the sibling test above. Inert once bootstrap has run.
            BootstrapPrecondition.Require(pickup, "AxePickup component in Boot.unity");
            Assert.IsNotNull(pickup,
                "the Boot scene must carry the AxePickup — the AC3 PoC pickable world axe (serialized, " +
                "not Awake-built)");
            Assert.IsNotNull(pickup.inventory,
                "AxePickup's Inventory reference must be wired editor-time (auto-places the axe in belt slot 1)");
            Assert.IsNotNull(pickup.player,
                "AxePickup's player reference must be wired editor-time so the proximity check has a target");
            Assert.Greater(pickup.pickupRadius, 0f, "pickupRadius must be positive — a zero radius never picks up");
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }
    }
}
