using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the U2-3 chop interaction (ticket 86ca8bdd8) is SERIALIZED into the Boot
    /// scene the exe ships — not added at Awake (the editor-vs-runtime serialization trap,
    /// unity-conventions.md, would mangle/drop an Awake-built component or visual). Sibling of
    /// CraftSceneTests; same regression-guard intent: drop MovementCameraScene.BuildChopTree (or its
    /// wiring) and this goes RED in headless CI, rather than the shipped build silently lacking the
    /// "do work in the world" beat.
    /// </summary>
    public class ChopSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesChopTree_WiredToInventoryAndPlayer()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            ChopTree tree = FindInScene<ChopTree>(scene);
            Assert.IsNotNull(tree,
                "the Boot scene must carry the ChopTree — the loop's 'do work in the world' beat the " +
                "castaway click-moves to (unity-conventions.md editor-vs-runtime trap: serialized, not " +
                "Awake-built)");
            Assert.IsNotNull(tree.inventory,
                "ChopTree's Inventory reference must be wired editor-time so reaching the tree (with an " +
                "axe) writes the chopped wood without an Awake-time scene search in the build");
            Assert.IsNotNull(tree.player,
                "ChopTree's player reference (the moving agent root) must be wired editor-time so the " +
                "proximity check has a target without an Awake-time scene search in the build");
            Assert.IsNotNull(tree.visual,
                "ChopTree's visual reference must be wired editor-time so the felling tween animates the " +
                "serialized tree mesh (the thin-but-felt feedback)");
        }

        [Test]
        public void BootScene_ChopTree_ShipsStandingAndUnchopped()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            ChopTree tree = FindInScene<ChopTree>(scene);
            Assert.IsNotNull(tree, "the Boot scene must carry the ChopTree");

            Assert.IsFalse(tree.IsFelled, "the tree ships STANDING — the player fells it by chopping");
            Assert.AreEqual(0, tree.Chops, "no chops landed before play");
            Assert.Greater(tree.chopRadius, 0f, "chopRadius must be positive — a zero radius never chops");
            Assert.Greater(tree.chopsToFell, 0, "chopsToFell must be positive — else the tree fells on chop 0");
            Assert.Greater(tree.woodPerChop, 0, "woodPerChop must be positive — a chop must yield wood");
        }

        [Test]
        public void BootScene_ShipsInventoryEmpty_NoWoodSeededByChopAuthoring()
        {
            // The chop authoring must NOT pre-seed wood (the ledger ships empty; wood comes from chopping).
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Inventory inv = FindInScene<Inventory>(scene);
            Assert.IsNotNull(inv, "the Boot scene must carry the Inventory");
            Assert.AreEqual(0, inv.WoodCount, "no wood seeded — wood is yielded only by chopping (U2-3)");
            Assert.IsFalse(inv.HasAxe, "no axe seeded — the axe is crafted (U2-2), then gates the chop");
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
