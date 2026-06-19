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
            // panelSettings-non-null assert above did NOT catch it — the asset existed, its THEME was
            // null. Assert the theme too so a theme-less panel reds here, not as a silent zero-frame
            // capture (guard the failing condition, not a proxy — unity-conventions.md §editor-vs-runtime).
            Assert.IsNotNull(ui.document.panelSettings.themeStyleSheet,
                "the inventory PanelSettings must carry a NON-NULL themeStyleSheet — a UIDocument whose " +
                "PanelSettings has no theme throws at startup in the build and zeroes the capture gate " +
                "(EnsureRuntimeTheme must resolve or create one)");
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
