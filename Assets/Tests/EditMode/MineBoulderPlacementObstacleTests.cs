using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for 86catr49m — the boulder pool registers as PlacementObstacles so a crafting-table ghost
    /// reads RED over a STANDING boulder, and STOPS blocking once the boulder is mined away, then RE-blocks on
    /// regrow. Drives the REAL MineBoulder registration logic (SyncPlacementObstacles, keyed on each node's
    /// IsMineable) synchronously via the InitializePoolForTest seam — no MonoBehaviour lifecycle runs in edit mode.
    ///
    /// WHY the register lifecycle keys on IsMineable, NOT a PlacementObstacle component (Drew's #303 trace): a
    /// boulder's break→fade→regrow cycle NEVER toggles GameObject.SetActive, so a component's OnEnable/OnDisable
    /// would register once and never unregister a mined-away boulder → it would wrongly keep blocking placement
    /// at that empty spot until regrow. These tests pin the correct transitions.
    ///
    /// REGRESSION GUARD: if a future change authors the boulder pool but drops the registry wire-up (removes
    /// SyncPlacementObstacles or its Start/Update call), Standing_RegistersNoBuildZones REDS in EditMode — the
    /// silent-killer coverage gap Tess flagged on #302 (a boulders-don't-block break would red no gate).
    /// </summary>
    public class MineBoulderPlacementObstacleTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Transform> _boulders = new List<Transform>();

        private MineBoulder BuildPool(int count)
        {
            _boulders.Clear();
            var root = new GameObject("Boulders");
            _spawned.Add(root);
            for (int i = 0; i < count; i++)
            {
                var b = new GameObject(MineBoulder.BoulderNodeName);
                b.transform.SetParent(root.transform, false);
                // Well-separated (5u) so each boulder's ~1.2 no-build zone tests independently, never cross-overlaps.
                b.transform.position = new Vector3(10f + i * 5f, 0f, 0f);
                var mesh = new GameObject("BoulderMesh"); // a child so MineableNodeState has a visual to fade
                mesh.transform.SetParent(b.transform, false);
                _boulders.Add(b.transform);
            }

            var go = new GameObject("MineBoulder");
            _spawned.Add(go);
            var mine = go.AddComponent<MineBoulder>();
            mine.boulderRoot = root.transform;
            mine.strikesToBreak = 2;
            mine.regrowSeed = 91577;
            mine.activeNodeCount = -1;
            mine.placementObstacleRadius = 1.2f;
            mine.InitializePoolForTest(); // discover pool + register the standing set (Start does not fire in edit mode)
            return mine;
        }

        [TearDown]
        public void TearDown()
        {
            // Keep the STATIC PlacementObstacleRegistry hermetic across tests (no [RuntimeInitializeOnLoadMethod]
            // reset fires in EditMode). Unregister explicitly (OnDisable is not guaranteed in edit mode) + destroy.
            foreach (var b in _boulders)
                if (b != null) PlacementObstacleRegistry.Unregister(b);
            _boulders.Clear();
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        [Test]
        public void Standing_RegistersNoBuildZones()
        {
            var mine = BuildPool(3);
            Assert.AreEqual(3, mine.NodeCount, "the 3-boulder pool was discovered");
            foreach (var b in _boulders)
                Assert.IsTrue(
                    PlacementObstacleRegistry.IsFootprintBlocked(b.position, 0.55f),
                    "a STANDING boulder registers a no-build zone → a table footprint on it reads RED (86catr49m). " +
                    "If this reds, the boulder→registry wire-up was dropped (the #302 silent-killer gap).");
        }

        [Test]
        public void MinedAway_StopsBlocking_ThenRegrowReRegisters()
        {
            var mine = BuildPool(1);
            Vector3 pos = _boulders[0].position;
            Assert.IsTrue(PlacementObstacleRegistry.IsFootprintBlocked(pos, 0.55f), "standing boulder → blocks");

            // Break it: strikesToBreak=2 immediate Mine strikes → not mineable → Sync unregisters.
            mine.Mine();
            mine.Mine();
            Assert.IsTrue(mine.IsBrokenOn(0), "two strikes break the boulder");
            mine.SyncPlacementObstacles();
            Assert.IsFalse(PlacementObstacleRegistry.IsFootprintBlocked(pos, 0.55f),
                "a mined-away boulder STOPS blocking placement — the empty spot is buildable (Drew #303 trace: NOT " +
                "a stuck OnEnable registration that would block until regrow)");

            // Regrow analog (deterministic, clock-free): the pool-dial round-trip resets the node to STANDING —
            // the SAME IsMineable→true transition regrow drives, so Sync re-registers.
            mine.SetActiveNodeCount(0);
            mine.SetActiveNodeCount(-1);
            mine.SyncPlacementObstacles();
            Assert.IsTrue(PlacementObstacleRegistry.IsFootprintBlocked(pos, 0.55f),
                "a regrown / standing-again boulder RE-REGISTERS its no-build zone");
        }
    }
}
