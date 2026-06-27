using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the small-STONE E-loot (ticket 86caa4c96) — the 1-stone gather — going through
    /// the REAL shared E-loot surface (<see cref="IPickable"/> + <see cref="PickableLooter"/>, the merged
    /// 86caf7a6q foundation). Mirrors the StickProp E-loot test shape; proves the stone's own IPickable
    /// contract + the not-auto rule through the looter + the RESPAWN (AC3, the stick/stone delta):
    ///   - E in range of a stone LOOTS exactly ONE stone into the inventory (AC2) + the stone goes EMPTY;
    ///   - standing in range WITHOUT pressing E loots NOTHING (AC5 — not proximity-auto; the silent-killer);
    ///   - E OUT of range loots nothing (the nearest-in-range resolve finds nothing in reach);
    ///   - a looted (empty) stone is NOT loot-able again UNTIL it respawns (no double-loot off one spot);
    ///   - the stone RESPAWNS after the (tweakable) window elapses and is loot-able anew (AC3);
    ///   - a stone into a FULL pack is a clean no-op AND the stone is NOT consumed (re-loot-able later);
    ///   - the looted stone STACKS onto existing stone (AC2 — the canonical StoneId, not a parallel id).
    ///
    /// We drive the player transform + the looter's RequestLoot() programmatic edge (the headless/shipped-
    /// build seam), isolating the loot logic from pathfinding. The StoneId path is asserted by reading the
    /// inventory model count.
    /// </summary>
    public class StonePropPlayModeTests
    {
        private GameObject _invGo;
        private GameObject _playerGo;
        private GameObject _stoneGo;
        private GameObject _visualGo;
        private Inventory _inv;
        private StoneProp _stone;
        private PickableLooter _looter;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the stone

            _stoneGo = new GameObject("LP_Stone");
            _stoneGo.transform.position = Vector3.zero;
            // A child visual so the StoneProp toggles JUST the visual on loot (the component stays active to
            // run its respawn timer — the real build's idiom).
            _visualGo = new GameObject("StoneMesh");
            _visualGo.transform.SetParent(_stoneGo.transform, false);
            _stone = _stoneGo.AddComponent<StoneProp>();
            _stone.inventory = _inv;
            _stone.stoneVisual = _visualGo.transform;
            _stone.lootRadius = 1.6f;
            _stone.stonePerPickup = 1;
            // A TIGHT, deterministic respawn window so the respawn assertion runs in a bounded test (no shared
            // respawner wired -> the per-instance fallback band is used). respawnSeed pins the roll.
            _stone.respawnMinFallback = 0.10f;
            _stone.respawnMaxFallback = 0.12f;
            _stone.respawnSeed = 4242;

            // The E-LOOT interactor — the player side of the shared surface. It discovers the stone (an
            // IPickable) at runtime and loots it on E (RequestLoot, the programmatic edge).
            _looter = _playerGo.AddComponent<PickableLooter>();
            _looter.inventory = _inv;
            _looter.player = _playerGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            if (_stoneGo != null) Object.Destroy(_stoneGo);
        }

        private int Stone => _inv.Model.CountItem(ItemCatalog.StoneId);

        // === AC2: pressing E in range of a stone LOOTS exactly ONE stone + the spot goes EMPTY ===
        [UnityTest]
        public IEnumerator PressE_InRangeOfStone_LootsOneStone_AndSpotGoesEmpty()
        {
            Assert.IsTrue(_stone.IsAvailable, "precondition: the stone is present");
            Assert.AreEqual(0, Stone, "precondition: no stone held");

            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // within range
            _looter.DiscoverPickables();
            _looter.RequestLoot();
            yield return null; // looter Update consumes the latch + loots

            Assert.AreEqual(1, Stone,
                "E in range of a stone loots EXACTLY ONE stone (the small-stone gather — AC2), via StoneId");
            Assert.IsFalse(_stone.IsAvailable, "the looted stone goes EMPTY (consumed from the spot — AC2)");
            Assert.IsFalse(_visualGo.activeSelf, "the looted stone's VISUAL is hidden (visibly gone)");
            Assert.IsTrue(_stoneGo.activeSelf,
                "the stone GameObject stays ACTIVE (only the visual toggles) so the respawn timer keeps running");
        }

        // === AC5 (the load-bearing not-auto proof): standing in range WITHOUT pressing E loots NOTHING ===
        [UnityTest]
        public IEnumerator InRange_WithoutPressingE_LootsNothing_NotProximityAuto()
        {
            _playerGo.transform.position = new Vector3(0.2f, 0f, 0.2f); // right on the stone
            _looter.DiscoverPickables();
            for (int i = 0; i < 20; i++) yield return null; // many frames, NO RequestLoot

            Assert.IsTrue(_stone.IsAvailable,
                "standing in range with NO E press leaves the stone present — it does NOT auto-loot (AC5)");
            Assert.AreEqual(0, Stone,
                "proximity ALONE loots nothing — walking up is not enough, the player must press E (AC5)");

            // The SAME standing position DOES loot once E is pressed (proves it was the input, not the
            // position, gating the loot).
            _looter.RequestLoot();
            yield return null;
            Assert.IsFalse(_stone.IsAvailable, "pressing E from the same spot now loots — input is the gate");
            Assert.AreEqual(1, Stone, "E from in range loots the one stone");
        }

        // === E OUT of range loots nothing (the nearest-in-range resolve finds no stone in reach) ===
        [UnityTest]
        public IEnumerator PressE_OutOfRange_LootsNothing()
        {
            // Player is far from the stone (20,_,20 from SetUp). Press E repeatedly — nothing in reach.
            _looter.DiscoverPickables();
            for (int i = 0; i < 5; i++) { _looter.RequestLoot(); yield return null; }

            Assert.IsTrue(_stone.IsAvailable, "E out of range -> the stone stays present (nothing in reach)");
            Assert.AreEqual(0, Stone, "E out of range -> no stone (nearest-in-range resolve finds nothing)");
        }

        // === An empty (respawning) stone is NOT loot-able again until it respawns (no double-loot) ===
        [UnityTest]
        public IEnumerator EmptyStone_IsNotLootableAgain_UntilRespawn()
        {
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            _looter.DiscoverPickables();

            _looter.RequestLoot();
            yield return null;
            Assert.AreEqual(1, Stone, "first E loots one stone");
            Assert.IsFalse(_stone.CanLoot, "the empty stone reports not loot-able (mid-respawn)");

            // Press E again immediately — the empty stone is skipped (still mid-respawn), no second stone.
            _looter.DiscoverPickables();
            _looter.RequestLoot();
            yield return null;
            Assert.AreEqual(1, Stone, "an empty stone yields NO second stone before it respawns (CanLoot false)");
        }

        // === AC3: the stone RESPAWNS after the (tweakable) window and is loot-able anew ===
        [UnityTest]
        public IEnumerator Stone_RespawnsAfterWindow_ThenLootableAgain()
        {
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            _looter.DiscoverPickables();

            _looter.RequestLoot();
            yield return null;
            Assert.AreEqual(1, Stone, "first E loots one stone");
            Assert.IsFalse(_stone.IsAvailable, "the spot is empty after the loot");
            Assert.Greater(_stone.RespawnAt, Time.time,
                "the respawn is scheduled in the future, within the tweakable window (AC3)");

            // Wait past the (tight) respawn window. Real-time wait so StoneProp.Update fires Respawn().
            float deadline = Time.realtimeSinceStartup + 2f; // ample for the 0.10-0.12s test window
            while (!_stone.IsAvailable && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsTrue(_stone.IsAvailable, "the stone RESPAWNS after the window elapses (AC3)");
            Assert.IsTrue(_visualGo.activeSelf, "the respawned stone's visual is shown again (visibly back)");
            Assert.IsTrue(_stone.CanLoot, "the respawned stone is loot-able anew");

            // And it can be looted AGAIN (the spot recharged).
            _looter.DiscoverPickables();
            _looter.RequestLoot();
            yield return null;
            Assert.AreEqual(2, Stone, "the respawned stone loots a SECOND stone (the spot recharged — AC3)");
        }

        // === A stone into a FULL pack is a clean no-op AND the stone is NOT consumed (re-loot-able later) ===
        [Test]
        public void TryLoot_IntoFullInventory_AddsNothing_AndStoneNotConsumed()
        {
            // Fill the inventory completely with stone so a 1-stone loot can't land.
            var stoneDef = _inv.Catalog.ById(ItemCatalog.StoneId);
            Assert.IsNotNull(stoneDef, "precondition: the catalog defines the stone item");
            int cap = stoneDef.MaxStack * _inv.Model.InventorySlots.Count;
            _inv.Model.AddItem(stoneDef, cap); // pack completely full
            Assert.AreEqual(cap, Stone, "precondition: inventory full of stone");

            bool looted = _stone.TryLoot(_inv);

            Assert.IsFalse(looted, "a full pack -> TryLoot declines (a clean false no-op)");
            Assert.AreEqual(cap, Stone, "no over-credit / no negative store on a full-pack decline");
            Assert.IsTrue(_stone.IsAvailable,
                "the stone is NOT consumed on a declined loot — the player can come back for it (AC2)");
        }

        // === AC2: the looted stone STACKS onto existing stone (the canonical StoneId, not a parallel id) ===
        [Test]
        public void LootedStone_StacksOntoExistingStone_SameStoneId()
        {
            // Pre-load some stone so the looted stone stacks onto it.
            var stoneDef = _inv.Catalog.ById(ItemCatalog.StoneId);
            _inv.Model.AddItem(stoneDef, 4);
            Assert.AreEqual(4, Stone, "precondition: 4 stone held");

            bool looted = _stone.TryLoot(_inv);

            Assert.IsTrue(looted, "looting a stone into a non-full pack succeeds");
            Assert.AreEqual(5, Stone,
                "the looted stone STACKS onto the existing stone (4 + 1 = 5, one item kind via StoneId)");
        }

        // === LootRange scales with the stone size (a bigger small-stone is loot-able a touch farther) ===
        [Test]
        public void LootRange_ScalesWithStoneSize()
        {
            _stone.lootRadius = 1.6f;
            _stoneGo.transform.localScale = Vector3.one * 0.7f;
            Assert.AreEqual(1.6f * 0.7f, _stone.LootRange, 0.001f,
                "LootRange = lootRadius * localScale.x (a bigger stone is loot-able from a touch farther)");
        }

        // === The shared StoneRespawner window drives the respawn (AC3a — one source retunes every stone) ===
        [Test]
        public void SharedRespawner_DrivesTheRespawnWindow()
        {
            var respGo = new GameObject("StoneRespawner");
            var resp = respGo.AddComponent<StoneRespawner>();
            resp.RespawnMinSeconds = 30f;
            resp.RespawnMaxSeconds = 30f; // pin to a single value so the rolled delay is exactly 30s
            _stone.respawner = resp;

            // Loot the stone, then assert the scheduled respawn used the SHARED window, not the fallback band.
            bool looted = _stone.TryLoot(_inv);
            Assert.IsTrue(looted, "the stone loots into the empty pack");
            Assert.AreEqual(Time.time + 30f, _stone.RespawnAt, 0.5f,
                "the respawn delay came from the SHARED StoneRespawner window (30s), not the fallback (AC3a)");

            Object.Destroy(respGo);
        }
    }
}
