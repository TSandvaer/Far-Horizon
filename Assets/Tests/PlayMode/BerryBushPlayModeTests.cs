using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the BERRY BUSH (ticket 86caa5zz3) — the harvest-&gt;inventory, regrow, and
    /// eat-CONSUME beats actually firing through BerryBush.Update / its public seams.
    ///
    /// We drive the player transform + the public Harvest()/EatBerry() seams directly, isolating the
    /// bush's proximity + harvest + regrow + consume logic from pathfinding (NavMesh/click-move is covered
    /// by the shipped-build capture). Proves:
    ///   - proximity (no tool) harvests a ripe bush -&gt; berries land in the inventory + STACK (AC3/AC6);
    ///   - a harvested bush goes BARE and REGROWS its berries after the (tweakable) timer (AC4/AC6) —
    ///     the BUSH persists, only the berries toggle;
    ///   - the eat-action CONSUMES exactly one berry (AC5/AC5a), the no-berry case is a clean no-op
    ///     (no negative inventory), and eating before a HungerNeed exists is graceful (AC5b — no null-ref).
    ///
    /// SCOPE (AC5a): this ticket OWNS only the consume side. The ATOMIC eat-&gt;hunger restore test
    /// ("berry −1 AND hunger +restore in one action") lives in 86caamkp8 (HungerNeedTests) — it is NOT
    /// duplicated here (that duplication, or each side assuming the other owns it, IS the dual-spawn gap).
    /// </summary>
    public class BerryBushPlayModeTests
    {
        private GameObject _invGo;
        private GameObject _playerGo;
        private GameObject _bushGo;
        private Inventory _inv;
        private BerryBush _bush;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the bush

            _bushGo = new GameObject("BerryBush");
            _bushGo.transform.position = Vector3.zero;
            _bush = _bushGo.AddComponent<BerryBush>();
            _bush.inventory = _inv;
            _bush.player = _playerGo.transform;
            _bush.hasBerries = true;
            _bush.harvestRadius = 2.0f;
            _bush.berriesPerHarvest = 3;
            _bush.regrowMinSeconds = 0.2f; // fast so the regrow lands within a test wall-clock window
            _bush.regrowMaxSeconds = 0.3f;
            _bush.regrowSeed = 12345;      // deterministic regrow roll
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_bushGo);
        }

        private int Berries => _inv.Model.CountItem(ItemCatalog.BerryId);

        // === AC3/AC6: reaching a ripe bush (no tool) harvests berries into the inventory ===
        [UnityTest]
        public IEnumerator ReachRipeBush_HarvestsBerriesIntoInventory()
        {
            Assert.IsTrue(_bush.IsRipe, "precondition: bush ships ripe");
            Assert.AreEqual(0, Berries, "precondition: no berries held");

            // Walk the player onto the bush; the harvest fires edge-triggered on arrival.
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            float start = Time.time;
            while (Time.time - start < 0.5f && _bush.IsRipe) yield return null;

            Assert.IsFalse(_bush.IsRipe, "reaching the ripe bush harvests it -> it goes BARE");
            Assert.AreEqual(_bush.berriesPerHarvest, Berries,
                "harvesting yields berriesPerHarvest into the inventory (the item-model AddItem seam)");
        }

        // === AC6: harvested berries STACK (a second harvest tops up the same stack, not a new item) ===
        [UnityTest]
        public IEnumerator TwoHarvests_StackBerries()
        {
            // First harvest at the bush.
            _bush.Harvest();
            Assert.AreEqual(3, Berries, "first harvest -> 3 berries");

            // Let the berries regrow, then harvest again — the second batch STACKS onto the first.
            float start = Time.time;
            while (Time.time - start < 1f && !_bush.IsRipe) yield return null;
            Assert.IsTrue(_bush.IsRipe, "berries regrew within the window");

            _bush.Harvest();
            Assert.AreEqual(6, Berries, "second harvest STACKS onto the first (3 + 3 = 6, one item kind)");
        }

        // === AC4/AC6: the bush PERSISTS; only the berries deplete + regrow after the (tweakable) timer ===
        [UnityTest]
        public IEnumerator HarvestedBush_Regrows_BerriesAfterTimer()
        {
            _bush.Harvest();
            Assert.IsFalse(_bush.IsRipe, "harvest -> bare (berries depleted)");

            // The bush GameObject persists (only the berries toggle).
            Assert.IsNotNull(_bushGo, "the bush itself persists through harvest (only berries deplete)");

            float start = Time.time;
            while (Time.time - start < 1f && !_bush.IsRipe) yield return null;

            Assert.IsTrue(_bush.IsRipe, "berries regrow after the regrow timer (random within [min,max])");

            // And the regrown berries are harvestable anew.
            int before = Berries;
            _bush.Harvest();
            Assert.AreEqual(before + _bush.berriesPerHarvest, Berries, "the regrown berries harvest again");
        }

        // === AC4: regrow time is RANDOM within [min,max] — never below min, never above max ===
        [Test]
        public void RegrowTime_FallsWithinTheConfiguredRange()
        {
            _bush.regrowMinSeconds = 5f;
            _bush.regrowMaxSeconds = 9f;
            _bush.regrowSeed = 777;
            // Harvest schedules a regrow at Time.time + a random delay in [min,max].
            float t0 = Time.time;
            _bush.Harvest();
            float delay = _bush.RegrowAt - t0;
            Assert.GreaterOrEqual(delay, 5f - 0.01f, "regrow delay is never below regrowMinSeconds");
            Assert.LessOrEqual(delay, 9f + 0.01f, "regrow delay is never above regrowMaxSeconds");
        }

        // === AC5/AC5a: the eat-action consumes EXACTLY ONE berry (consume side, owned here) ===
        [Test]
        public void EatBerry_ConsumesExactlyOne()
        {
            _bush.Harvest(); // 3 berries
            Assert.AreEqual(3, Berries);

            bool ate = _bush.EatBerry((HungerNeed)null); // no hunger need -> consume only (AC5b graceful)
            Assert.IsTrue(ate, "eating with a berry held succeeds");
            Assert.AreEqual(2, Berries, "eat consumes EXACTLY one berry (consume side)");
        }

        // === AC5a no-berry case: eating with 0 berries is a clean no-op (no negative inventory) ===
        [Test]
        public void EatBerry_WithNoBerries_IsNoOp_NoNegativeInventory()
        {
            Assert.AreEqual(0, Berries, "precondition: no berries");
            bool ate = _bush.EatBerry((HungerNeed)null);
            Assert.IsFalse(ate, "eating with no berries returns false");
            Assert.AreEqual(0, Berries, "no negative inventory — all-or-nothing consume debits nothing");
        }

        // === AC5b graceful no-HungerNeed: eating before the hunger need exists consumes + no null-ref ===
        [Test]
        public void EatBerry_WithoutHungerNeed_ConsumesGracefully()
        {
            _bush.Harvest(); // 3 berries
            // No HungerNeed component anywhere — must NOT null-ref on a missing AddFood; just consume.
            Assert.DoesNotThrow(() => _bush.EatBerry((HungerNeed)null));
            Assert.AreEqual(2, Berries, "graceful no-HungerNeed: the berry is still consumed");
        }

        // === AC5b with a HungerNeed: the eat routes through the atomic seam + consumes the berry ===
        // (The RESTORE amount is asserted in 86caamkp8 HungerNeedTests — NOT duplicated here, AC5a.)
        [Test]
        public void EatBerry_WithHungerNeed_ConsumesTheBerry()
        {
            var hungerGo = new GameObject("Hunger");
            var hunger = hungerGo.AddComponent<HungerNeed>();
            try
            {
                _bush.Harvest(); // 3 berries
                bool ate = _bush.EatBerry(hunger);
                Assert.IsTrue(ate, "eating with a HungerNeed + a berry succeeds");
                Assert.AreEqual(2, Berries, "the berry is consumed through the atomic seam (consume side)");
            }
            finally { Object.DestroyImmediate(hungerGo); }
        }

        // === Out of range never harvests (proximity is required) ===
        [UnityTest]
        public IEnumerator OutOfRange_DoesNotHarvest()
        {
            for (int i = 0; i < 10; i++) yield return null; // player stays far away
            Assert.IsTrue(_bush.IsRipe, "out of range -> the bush stays ripe (not harvested)");
            Assert.AreEqual(0, Berries, "out of range -> no berries");
        }

        // === A plain (non-berry) bush never yields berries ===
        [Test]
        public void PlainBush_NeverHarvests()
        {
            _bush.hasBerries = false;
            int yielded = _bush.Harvest();
            Assert.AreEqual(0, yielded, "a plain bush yields no berries");
            Assert.AreEqual(0, Berries, "a plain bush adds nothing to the inventory");
        }
    }
}
