using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the STICK / BRANCH E-loot (ticket 86caa96rd) — the LOW-yield wood source — going
    /// through the REAL shared E-loot surface (<see cref="IPickable"/> + <see cref="PickableLooter"/>, the
    /// merged 86caf7a6q foundation). Mirrors the BerryBush E-loot test shape; proves the stick's own
    /// IPickable contract + the not-auto rule through the looter:
    ///   - E in range of a stick LOOTS exactly ONE wood into the inventory (AC2/AC3) + the stick is CONSUMED;
    ///   - standing in range WITHOUT pressing E loots NOTHING (AC6 — not proximity-auto; the silent-killer);
    ///   - E OUT of range loots nothing (the nearest-in-range resolve finds nothing in reach);
    ///   - a looted (consumed) stick is NOT loot-able again (no double-loot off one stick);
    ///   - a stick into a FULL pack is a clean no-op AND the stick is NOT consumed (re-loot-able later);
    ///   - the looted wood STACKS onto existing wood (AC6 — the canonical WoodId, not a parallel id).
    ///
    /// We drive the player transform + the looter's RequestLoot() programmatic edge (the headless/shipped-
    /// build seam), isolating the loot logic from pathfinding (NavMesh/click-move is covered by the
    /// shipped-build capture). The WoodId path is asserted by reading Inventory.WoodCount / the model.
    /// </summary>
    public class StickPropPlayModeTests
    {
        private GameObject _invGo;
        private GameObject _playerGo;
        private GameObject _stickGo;
        private Inventory _inv;
        private StickProp _stick;
        private PickableLooter _looter;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the stick

            _stickGo = new GameObject("LP_Stick");
            _stickGo.transform.position = Vector3.zero;
            _stick = _stickGo.AddComponent<StickProp>();
            _stick.inventory = _inv;
            _stick.lootRadius = 1.6f;
            _stick.woodPerStick = 1;

            // The E-LOOT interactor — the player side of the shared surface. It discovers the stick (an
            // IPickable) at runtime and loots it on E (RequestLoot, the programmatic edge). Wired to the same
            // inventory + player so "press E" drives the real loot path the build uses.
            _looter = _playerGo.AddComponent<PickableLooter>();
            _looter.inventory = _inv;
            _looter.player = _playerGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            if (_stickGo != null) Object.Destroy(_stickGo);
        }

        private int Wood => _inv.Model.CountItem(ItemCatalog.WoodId);

        // === AC2/AC3: pressing E in range of a stick LOOTS exactly ONE wood + the stick is CONSUMED ===
        [UnityTest]
        public IEnumerator PressE_InRangeOfStick_LootsOneWood_AndConsumesStick()
        {
            Assert.IsTrue(_stick.IsAvailable, "precondition: the stick is present");
            Assert.AreEqual(0, Wood, "precondition: no wood held");

            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // within range
            _looter.DiscoverPickables();
            _looter.RequestLoot();
            yield return null; // looter Update consumes the latch + loots

            Assert.AreEqual(1, Wood,
                "E in range of a stick loots EXACTLY ONE wood (the low-yield contrast — AC3), via WoodId");
            Assert.IsFalse(_stick.IsAvailable, "the looted stick is CONSUMED (removed from the world — AC2/AC5)");
            Assert.IsFalse(_stickGo.activeSelf, "the consumed stick's GameObject is deactivated (visibly gone)");
        }

        // === AC6 (the load-bearing not-auto proof): standing in range WITHOUT pressing E loots NOTHING ===
        [UnityTest]
        public IEnumerator InRange_WithoutPressingE_LootsNothing_NotProximityAuto()
        {
            _playerGo.transform.position = new Vector3(0.2f, 0f, 0.2f); // right on the stick
            _looter.DiscoverPickables();
            for (int i = 0; i < 20; i++) yield return null; // many frames, NO RequestLoot

            Assert.IsTrue(_stick.IsAvailable,
                "standing in range with NO E press leaves the stick present — it does NOT auto-loot (AC6)");
            Assert.AreEqual(0, Wood,
                "proximity ALONE loots nothing — walking up is not enough, the player must press E (AC6)");

            // The SAME standing position DOES loot once E is pressed (proves it was the input, not the
            // position, gating the loot).
            _looter.RequestLoot();
            yield return null;
            Assert.IsFalse(_stick.IsAvailable, "pressing E from the same spot now loots — input is the gate");
            Assert.AreEqual(1, Wood, "E from in range loots the one wood");
        }

        // === E OUT of range loots nothing (the nearest-in-range resolve finds no stick in reach) ===
        [UnityTest]
        public IEnumerator PressE_OutOfRange_LootsNothing()
        {
            // Player is far from the stick (20,_,20 from SetUp). Press E repeatedly — nothing in reach.
            _looter.DiscoverPickables();
            for (int i = 0; i < 5; i++) { _looter.RequestLoot(); yield return null; }

            Assert.IsTrue(_stick.IsAvailable, "E out of range -> the stick stays present (nothing in reach)");
            Assert.AreEqual(0, Wood, "E out of range -> no wood (nearest-in-range resolve finds nothing)");
        }

        // === A consumed stick is NOT loot-able again (no double-loot off one stick) ===
        [UnityTest]
        public IEnumerator ConsumedStick_IsNotLootableAgain()
        {
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            _looter.DiscoverPickables();

            _looter.RequestLoot();
            yield return null;
            Assert.AreEqual(1, Wood, "first E loots one wood");

            // Re-discover (the looter caches the set) + press E again — the consumed stick is skipped.
            _looter.DiscoverPickables();
            _looter.RequestLoot();
            yield return null;
            Assert.AreEqual(1, Wood, "a consumed stick yields NO second wood (CanLoot false -> skipped)");
            Assert.IsFalse(_stick.CanLoot, "the consumed stick reports not loot-able");
        }

        // === A stick into a FULL pack is a clean no-op AND the stick is NOT consumed (re-loot-able later) ===
        [Test]
        public void TryLoot_IntoFullInventory_AddsNothing_AndStickNotConsumed()
        {
            // Fill the inventory completely with wood so a 1-wood stick loot can't land.
            var woodDef = _inv.Catalog.ById(ItemCatalog.WoodId);
            Assert.IsNotNull(woodDef, "precondition: the catalog defines the wood item");
            int cap = woodDef.MaxStack * _inv.Model.InventorySlots.Count;
            _inv.Model.AddItem(woodDef, cap); // pack completely full
            Assert.AreEqual(cap, Wood, "precondition: inventory full of wood");

            bool looted = _stick.TryLoot(_inv);

            Assert.IsFalse(looted, "a full pack -> TryLoot declines (a clean false no-op)");
            Assert.AreEqual(cap, Wood, "no over-credit / no negative store on a full-pack decline");
            Assert.IsTrue(_stick.IsAvailable,
                "the stick is NOT consumed on a declined loot — the player can come back for it (AC2)");
        }

        // === AC6: the looted wood STACKS onto existing wood (the canonical WoodId, not a parallel id) ===
        [Test]
        public void LootedWood_StacksOntoExistingWood_SameWoodId()
        {
            // Pre-load some wood (e.g. from a prior chop / stick) so the stick's wood stacks onto it.
            _inv.AddWood(4);
            Assert.AreEqual(4, Wood, "precondition: 4 wood held");

            bool looted = _stick.TryLoot(_inv);

            Assert.IsTrue(looted, "looting a stick into a non-full pack succeeds");
            Assert.AreEqual(5, Wood,
                "the stick's 1 wood STACKS onto the existing wood (4 + 1 = 5, one item kind via WoodId)");
        }

        // === LootRange scales with the stick size (a longer branch is loot-able a touch farther) ===
        [Test]
        public void LootRange_ScalesWithStickSize()
        {
            _stick.lootRadius = 1.6f;
            _stickGo.transform.localScale = Vector3.one * 1.5f;
            Assert.AreEqual(1.6f * 1.5f, _stick.LootRange, 0.001f,
                "LootRange = lootRadius * localScale.x (a bigger stick is loot-able from a touch farther)");
        }
    }
}
