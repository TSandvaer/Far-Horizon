using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the BUILD MENU (ticket 86catpvpa) is SERIALIZED into Boot.unity (unity-conventions.md
    /// §editor-vs-runtime — not an Awake build; the component-in-source-but-not-in-scene trap). Asserts: the
    /// BuildMenuUI ships wired to a UIDocument on a resolving-theme PanelSettings; C is its open key (the SINGLE
    /// build entry point); the editor-authored placeable rows include BOTH a CraftingTablePlacement AND a
    /// ForgePlacement (each an IBuildPlaceable); and the -verifyBuildMenu shipped-build capture is serialized.
    /// </summary>
    public class BuildMenuSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesBuildMenu_WiredToDocumentAndPanelSettings()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var menu = FindInScene<BuildMenuUI>(scene);
            Assert.IsNotNull(menu, "the Boot scene must carry the BuildMenuUI (the C build menu)");
            Assert.IsNotNull(menu.document, "menu.document (UIDocument) must be wired — without it the menu never renders");
            Assert.IsNotNull(menu.document.panelSettings,
                "menu UIDocument must have PanelSettings on a resolving runtime theme (a base-less theme hangs the exe)");
            Assert.AreEqual(KeyCode.C, menu.openKey,
                "C must be the build-menu open key — the SINGLE build entry point (Danish-layout-safe)");
        }

        [Test]
        public void BootScene_BuildMenu_ListsTableAndForgeRows()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var menu = FindInScene<BuildMenuUI>(scene);
            Assert.IsNotNull(menu, "the Boot scene must carry the BuildMenuUI");
            Assert.IsNotNull(menu.placeableSources, "menu.placeableSources must be wired editor-time (the rows)");
            Assert.GreaterOrEqual(menu.placeableSources.Length, 2,
                "the menu must list at least the crafting table + forge rows");

            bool hasTable = false, hasForge = false;
            foreach (var src in menu.placeableSources)
            {
                Assert.IsInstanceOf<IBuildPlaceable>(src,
                    "every placeableSources element must implement IBuildPlaceable (the shared build-menu seam)");
                if (src is CraftingTablePlacement) hasTable = true;
                if (src is ForgePlacement) hasForge = true;
            }
            Assert.IsTrue(hasTable, "the build menu must register the CraftingTablePlacement row (① table)");
            Assert.IsTrue(hasForge, "the build menu must register the ForgePlacement row (③ forge)");
        }

        [Test]
        public void BootScene_CarriesBuildMenuVerifyCapture_Wired()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var cap = FindInScene<BuildMenuVerifyCapture>(scene);
            Assert.IsNotNull(cap,
                "the Boot scene must carry BuildMenuVerifyCapture serialized (the -verifyBuildMenu shipped-build " +
                "capture is inert + HANGS the exe if the scene never carries it — the component-not-serialized trap)");
            Assert.IsNotNull(cap.menu, "capture.menu must be wired editor-time (the menu it opens)");
            Assert.IsNotNull(cap.tablePlacement, "capture.tablePlacement must be wired (the row it selects)");
            Assert.IsNotNull(cap.inventory, "capture.inventory must be wired (the material grant for the ghost-flow proof)");
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
