using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the U2-2 craft interaction (ticket 86ca8bdaq) is SERIALIZED into the Boot
    /// scene the exe ships — not added at Awake (the editor-vs-runtime serialization trap,
    /// unity-conventions.md, would mangle/drop an Awake-built component). Sibling of
    /// WarmthNeedSceneTests; same regression-guard intent: drop the bootstrap wiring (the Survival
    /// Inventory/InventoryReadout adds, or MovementCameraScene.BuildCraftSpot) and this goes RED in
    /// headless CI, rather than the shipped build silently lacking the loop's entry interaction.
    /// </summary>
    public class CraftSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesInventory_Serialized_StartingEmpty()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            Inventory inv = FindInScene<Inventory>(scene);
            Assert.IsNotNull(inv,
                "the Boot scene must carry the Inventory component serialized into the scene — the " +
                "held-resource ledger (HasAxe/WoodCount) the loop writes and U2-5's HUD reads ships from " +
                "this scene, not an Awake add (unity-conventions.md editor-vs-runtime trap)");
            Assert.IsFalse(inv.HasAxe, "the ledger ships empty — the loop begins by crafting the axe");
            Assert.AreEqual(0, inv.WoodCount, "no wood seeded before the chop step (U2-3)");
        }

        [Test]
        public void BootScene_CarriesSurvivalHud_WiredToTheLedger()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            SurvivalHud hud = FindInScene<SurvivalHud>(scene);
            Assert.IsNotNull(hud,
                "the Boot scene must carry the U2-5 SurvivalHud (it SUPERSEDES the U2-2 InventoryReadout " +
                "placeholder) so the crafted axe is VISIBLE in the shipped exe (success test: 'sees it')");
            Assert.IsNotNull(hud.inventory,
                "the HUD's Inventory reference must be wired editor-time (serialized), so the ledger paints " +
                "without an Awake-time FindObjectOfType in the build");
        }

        [Test]
        public void BootScene_CarriesCraftSpot_WiredToInventoryAndPlayer()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            CraftSpot spot = FindInScene<CraftSpot>(scene);
            Assert.IsNotNull(spot,
                "the Boot scene must carry the CraftSpot — the loop's entry interaction the castaway " +
                "click-moves to (unity-conventions.md editor-vs-runtime trap: serialized, not Awake-built)");
            Assert.IsNotNull(spot.inventory,
                "CraftSpot's Inventory reference must be wired editor-time so reaching the spot writes the " +
                "crafted axe without an Awake-time scene search in the build");
            Assert.IsNotNull(spot.player,
                "CraftSpot's player reference (the moving agent root) must be wired editor-time so the " +
                "proximity check has a target without an Awake-time scene search in the build");
            Assert.Greater(spot.craftRadius, 0f, "craftRadius must be positive — a zero radius never crafts");
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
