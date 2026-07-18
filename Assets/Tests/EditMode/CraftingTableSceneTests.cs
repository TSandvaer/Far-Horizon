using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the crafting-table place-to-build system (ticket 86camz9uz / crafting-redesign ①)
    /// is SERIALIZED into Boot.unity (unity-conventions.md §editor-vs-runtime — not an Awake build; the
    /// component-in-source-but-not-in-scene + committed-asset-staleness traps). Replaces the retired
    /// CraftSpot scene guard. Asserts: the table ships INVISIBLE (invisible-until-placed, spec §2); the
    /// ghost, menu, and placement all serialize + are wired to the scene's Inventory/player/table.
    /// </summary>
    public class CraftingTableSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesCraftingTable_InvisibleUntilPlaced()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var table = FindInScene<CraftingTable>(scene);
            Assert.IsNotNull(table,
                "the Boot scene must carry the CraftingTable (serialized, not Awake-built — editor-vs-runtime trap)");
            Assert.IsFalse(table.IsBuilt, "the table ships UNBUILT (invisible-until-placed, spec §2)");
            Assert.IsNotNull(table.visual, "the table's visual root must be wired editor-time");
            foreach (var r in table.visual.GetComponentsInChildren<Renderer>(true))
                Assert.IsFalse(r.enabled,
                    "the table's renderers must be serialized DISABLED — invisible until the player places it");
        }

        [Test]
        public void BootScene_CarriesCraftingTablePlacement_Wired()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var place = FindInScene<CraftingTablePlacement>(scene);
            Assert.IsNotNull(place, "the Boot scene must carry the CraftingTablePlacement (place-to-build driver)");
            Assert.IsNotNull(place.inventory, "placement.inventory must be wired editor-time (the material debit)");
            Assert.IsNotNull(place.player, "placement.player must be wired editor-time (the ghost tracks in front of it)");
            Assert.IsNotNull(place.table, "placement.table must be wired editor-time (the table it reveals)");
            Assert.IsNotNull(place.ghost, "placement.ghost must be wired editor-time (the translucent preview)");
            Assert.IsNotNull(place.menu, "placement.menu must be wired editor-time (the menu opened on placement)");
            Assert.AreEqual(CraftingRecipeBook.TableWoodCost, place.woodCost, "table wood cost = the §5 default");
            Assert.AreEqual(CraftingRecipeBook.TableStoneCost, place.stoneCost, "table stone cost = the §5 default");
            Assert.AreNotEqual(0, place.groundMask.value, "placement.groundMask must be wired (the ghost ground snap)");

            // The ghost ships hidden too (shown only during placement).
            foreach (var r in place.ghost.GetComponentsInChildren<Renderer>(true))
                Assert.IsFalse(r.enabled, "the placement ghost must be serialized hidden (shown only while placing)");
        }

        [Test]
        public void BootScene_CarriesCraftingMenu_WiredToInventoryAndTable()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var menu = FindInScene<CraftingMenuUI>(scene);
            Assert.IsNotNull(menu, "the Boot scene must carry the CraftingMenuUI (the recipe menu)");
            Assert.IsNotNull(menu.document, "menu.document (UIDocument) must be wired — without it the menu never renders");
            Assert.IsNotNull(menu.document.panelSettings,
                "menu UIDocument must have PanelSettings on a resolving runtime theme (a base-less theme hangs the exe)");
            Assert.IsNotNull(menu.inventory, "menu.inventory must be wired editor-time (the craft debit/grant)");
            Assert.IsNotNull(menu.table, "menu.table must be wired editor-time (opens only on the built table near)");
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
