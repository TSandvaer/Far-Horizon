using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the I-2 MINE interaction (ticket 86cakkmr0) is SERIALIZED into the Boot scene the exe
    /// ships — not added at Awake (the editor-vs-runtime serialization trap would mangle/drop an Awake-built
    /// component or visual). Sibling of ChopSceneTests; same regression-guard intent: drop
    /// MovementCameraScene.BuildOreNodes / BuildPickaxePickup (or its wiring) and this goes RED in headless CI,
    /// rather than the shipped build silently lacking the "mine iron ore" beat.
    /// </summary>
    public class MineSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesMineOre_WiredToInventoryPlayerCharacterNodesAndSpawner()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            MineOre mine = FindInScene<MineOre>(scene);
            Assert.IsNotNull(mine,
                "the Boot scene must carry the MineOre — the iron-chain 'mine ore' beat (serialized, not Awake-built)");
            Assert.IsNotNull(mine.inventory,
                "MineOre.inventory must be wired editor-time so breaking a node drops ore toward the belt without an " +
                "Awake scene-search in the build");
            Assert.IsNotNull(mine.player,
                "MineOre.player must be wired editor-time so the proximity/mine check has a target");
            Assert.IsNotNull(mine.character,
                "MineOre.character must be wired editor-time so each strike plays the melee swing (TriggerChop) in " +
                "the shipped build");
            Assert.IsNotNull(mine.nodeRoot,
                "MineOre.nodeRoot must be wired editor-time so the manager discovers the authored ore-node pool");
            Assert.IsNotNull(mine.orePileSpawner,
                "MineOre.orePileSpawner must be wired editor-time so a broken node drops a lootable ore pile");
        }

        [Test]
        public void BootScene_CarriesOrePileSpawner_AndOreNodePool()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var spawner = FindInScene<OrePileSpawner>(scene);
            Assert.IsNotNull(spawner, "the Boot scene must carry the OrePileSpawner (the ore-drop factory)");
            Assert.Greater(spawner.OreYield, 0, "OreYield must be positive — a broken node must drop ore");

            MineOre mine = FindInScene<MineOre>(scene);
            Assert.IsNotNull(mine, "the Boot scene must carry the MineOre");
            Assert.IsNotNull(mine.nodeRoot, "MineOre.nodeRoot must be wired");

            // The authored pool must carry several OreNode children (the seeded organic pool). Count them under the
            // node root (Bar 1 — the pool exists; the runtime rarity dial enables the first ActiveNodeCount).
            int nodes = CountOreNodes(mine.nodeRoot);
            Assert.Greater(nodes, 4,
                "the ore-node pool must carry several OreNode instances (the seeded pool, so the rarity dial has " +
                "nodes to enable) — found " + nodes);
        }

        [Test]
        public void BootScene_MineOre_ShipsWithSaneStrikeAndRarityDefaults()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            MineOre mine = FindInScene<MineOre>(scene);
            Assert.IsNotNull(mine, "the Boot scene must carry the MineOre");

            Assert.Greater(mine.mineRadius, 0f, "mineRadius must be positive — a zero radius never mines");
            Assert.GreaterOrEqual(mine.strikesToBreak, MineOre.StrikesToBreakMin,
                "strikesToBreak must be >= the floor — else a node breaks on strike 0");
            Assert.LessOrEqual(mine.strikesToBreak, MineOre.StrikesToBreakMax,
                "strikesToBreak must be <= the ceiling");
            Assert.Greater(mine.activeNodeCount, 0,
                "the active node count (the ore-rarity default) must be positive so nodes are enabled at ship");
        }

        // REGRESSION GUARD (the PR #224 chop-capture class): the PickaxePickup is a PROXIMITY-AUTO pickup. If placed
        // within pickupRadius of the player spawn, it would auto-grab the pickaxe into belt slot 0 (the default-
        // selected slot) at spawn — de-selecting anything else and confusing the mine gate. This pins the SCENE
        // GEOMETRY: the pickaxe pickup must sit CLEAR of the spawn by MORE than its own pickupRadius.
        [Test]
        public void BootScene_PickaxePickup_ClearOfPlayerSpawn_CannotAutoGrabSlot0()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var pickup = FindInScene<PickaxePickup>(scene);
            Assert.IsNotNull(pickup,
                "the Boot scene must carry the PickaxePickup (the I-2 mine-loop soak-enabler) — serialized editor-time");
            Assert.IsNotNull(pickup.player,
                "PickaxePickup.player must be wired editor-time so the proximity check has the spawn target");

            Vector3 pickPos = pickup.transform.position;
            Vector3 spawnPos = pickup.player.position;
            float planarDist = Vector2.Distance(
                new Vector2(pickPos.x, pickPos.z), new Vector2(spawnPos.x, spawnPos.z));

            Assert.Greater(planarDist, pickup.pickupRadius,
                $"the PickaxePickup ({pickPos}) must sit CLEAR of the player spawn ({spawnPos}) by MORE than its " +
                $"pickupRadius ({pickup.pickupRadius}u) — else it auto-grabs the pickaxe into belt slot 0 at spawn " +
                $"(planarDist={planarDist:F2}u). This IS the PR #224 proximity-auto-grab regression class.");
        }

        private static int CountOreNodes(Transform root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == MineOre.OreNodeName) n++;
            return n;
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
