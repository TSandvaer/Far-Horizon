using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the END-TO-END WATER ACQUISITION loop (ticket 86cafc6vx AC3/AC6) — the whole point
    /// of the ticket: walk to pond → E (water in inventory) → select water in belt → left-click → thirst rises.
    /// Drives the INPUT-INDEPENDENT seams (PickableLooter.RequestLoot + LeftClickConsume.RequestUseClick — the
    /// headless analogs of a real E / left-click, mirroring ChopTree.RequestChopClick) so the chain is proven
    /// without a key/mouse device. Proves:
    ///   - the player AT the pond, E → exactly ONE water enters the inventory (the GET side, AC1);
    ///   - then select water in the belt + left-click → thirst RISES by the per-scoop amount (the USE side, AC3);
    ///   - the pond is INFINITE: a second E yields a second water (never depletes, AC5);
    ///   - the player FAR from the pond, E → NO water (the proximity / not-auto guard — silent-killer, AC6).
    /// This is the water sibling of PickableLooterPlayModeTests (the loot surface) + DrinkActionPlayModeTests.
    /// </summary>
    public class WaterAcquisitionPlayModeTests
    {
        private GameObject _invGo;
        private GameObject _thirstGo;
        private GameObject _playerGo;
        private GameObject _pondGo;
        private Inventory _inv;
        private ThirstNeed _thirst;
        private FreshwaterPond _pond;
        private PickableLooter _looter;
        private LeftClickConsume _consume;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _thirstGo = new GameObject("Thirst");
            _thirst = _thirstGo.AddComponent<ThirstNeed>();
            _thirst.max = 100f;
            _thirst.decayPerSecond = 0f;       // freeze decay so the test asserts ONLY the drink delta
            _thirst.waterScoopAmount = 14f;
            _thirst.startFull = false;
            _thirst.startFraction01 = 0.3f;    // pressured-with-headroom so a drink visibly climbs

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // start FAR from the pond

            _pondGo = new GameObject("FreshwaterPond");
            _pondGo.transform.position = Vector3.zero;
            _pond = _pondGo.AddComponent<FreshwaterPond>();
            _pond.inventory = _inv;
            _pond.thirst = _thirst;
            _pond.player = _playerGo.transform;
            _pond.pondSurfaceRadius = 2.6f;
            _pond.drinkRadius = 2.0f;          // loot range ~4.6u

            // The player-side looter (the GET side) — discovers the pond IPickable.
            _looter = _playerGo.AddComponent<PickableLooter>();
            _looter.inventory = _inv;
            _looter.player = _playerGo.transform;

            // The USE side (left-click the selected belt item — drink water). No InventoryUI / over-UI guard in
            // the bare rig (null is tolerated — the modal-panel + RMB guards still apply, both false here).
            _consume = _playerGo.AddComponent<LeftClickConsume>();
            _consume.inventory = _inv;
            _consume.thirst = _thirst;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_thirstGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_pondGo);
        }

        // Move the looted water to a belt slot + select it, so the left-click consume targets it. Water is a
        // belt-eligible Consumable (#152), so AddItem already fills the belt — but be robust: select the belt
        // slot that holds water.
        private bool SelectWaterInBelt()
        {
            var belt = _inv.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
            {
                if (!belt[i].IsEmpty && belt[i].Def.Id == ItemCatalog.WaterId)
                {
                    _inv.Model.SelectBelt(i);
                    return true;
                }
            }
            return false;
        }

        // === AC3/AC6: AT the pond, E loots ONE water; then left-click the selected water -> thirst RISES ===
        [UnityTest]
        public IEnumerator AtPond_PressE_GetsWater_ThenLeftClickDrink_RaisesThirst()
        {
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // walk onto the pond
            _looter.DiscoverPickables();                                // pick up the pond IPickable
            yield return null;

            // --- GET side: E loots exactly one water into the inventory (AC1) ---
            Assert.AreEqual(0, _inv.Model.CountItem(ItemCatalog.WaterId), "precondition: no water held");
            _looter.RequestLoot();
            yield return null; // the looter consumes the latch on Update
            Assert.AreEqual(1, _inv.Model.CountItem(ItemCatalog.WaterId),
                "E at the pond loots exactly ONE water into the inventory (the GET side that closes the loop, AC1)");

            // --- USE side: select the water in the belt + left-click -> thirst rises (the drink, AC3) ---
            Assert.IsTrue(SelectWaterInBelt(), "the looted water must be belt-eligible + selectable (the USE side)");
            float before01 = _thirst.Current01;
            float lastChanged = -1f;
            _thirst.Changed += v => lastChanged = v;

            _consume.RequestUseClick();
            yield return null; // the consume consumes the latch on Update

            Assert.AreEqual(0, _inv.Model.CountItem(ItemCatalog.WaterId),
                "the left-click drink consumed the one water unit (the USE side removes one — #156)");
            Assert.Greater(_thirst.Current01, before01,
                "the thirst bar VISIBLY rises after the left-click drink (the whole point of the ticket — AC3)");
            Assert.AreEqual(before01 + _thirst.waterScoopAmount / _thirst.max, _thirst.Current01, 0.001f,
                "thirst rose by exactly the per-scoop amount (ThirstNeed.AddWater — the UNCHANGED restore, AC3)");
            Assert.AreEqual(_thirst.Current01, lastChanged, 0.001f,
                "the drink fired Changed with the new Current01 (the HUD subscribe-never-poll seam, AC3)");
        }

        // === AC5: the pond is INFINITE — a second E yields a second water (the well never runs dry) ===
        [UnityTest]
        public IEnumerator AtPond_RepeatedE_EachYieldsOneWater_PondInfinite()
        {
            _playerGo.transform.position = new Vector3(1f, 0f, 0f);
            _looter.DiscoverPickables();
            yield return null;

            _looter.RequestLoot();
            yield return null;
            _looter.RequestLoot();
            yield return null;

            Assert.AreEqual(2, _inv.Model.CountItem(ItemCatalog.WaterId),
                "two E presses each yield one water -> 2 water (the pond is an INFINITE standing source, AC5)");
        }

        // === AC6: FAR from the pond, E loots NO water (the proximity / not-auto guard — silent-killer) ===
        [UnityTest]
        public IEnumerator FarFromPond_PressE_GetsNoWater_ProximityGuard()
        {
            // player stays FAR (20,0,20) — well outside the pond's loot range
            _looter.DiscoverPickables();
            yield return null;

            _looter.RequestLoot();
            yield return null;

            Assert.AreEqual(0, _inv.Model.CountItem(ItemCatalog.WaterId),
                "E FAR from the pond loots NO water — the looter's nearest-in-range resolve finds nothing in reach " +
                "(the proximity guard; a pond loot-able from anywhere green-passes 'got water' but breaks the fiction)");
        }

        // === AC6: standing in range WITHOUT pressing E gets NO water (NOT proximity-auto) ===
        [UnityTest]
        public IEnumerator AtPond_NoEPress_GetsNoWater_NotProximityAuto()
        {
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // in range
            _looter.DiscoverPickables();

            for (int i = 0; i < 20; i++) yield return null; // many frames, NO RequestLoot

            Assert.AreEqual(0, _inv.Model.CountItem(ItemCatalog.WaterId),
                "standing in range with no E press gets NOTHING — water acquisition is an ACTIVE input, not " +
                "proximity-auto (the load-bearing not-auto rule, AC6)");
        }
    }
}
