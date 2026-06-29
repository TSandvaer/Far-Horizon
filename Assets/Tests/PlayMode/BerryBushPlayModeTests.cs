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
        private PickableLooter _looter;

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

            // 86caf7a6q: the E-LOOT interactor — the player side of the shared loot surface. It discovers the
            // bush (an IPickable) at runtime and loots it on E (RequestLoot, the programmatic edge). Wired to
            // the same inventory + player so a "press E" drives the real loot path the build uses.
            _looter = _playerGo.AddComponent<PickableLooter>();
            _looter.inventory = _inv;
            _looter.player = _playerGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_bushGo);
        }

        private int Berries => _inv.Model.CountItem(ItemCatalog.BerryId);

        // === 86caf7a6q AC1: pressing E in range of a ripe bush LOOTS berries into the inventory ===
        [UnityTest]
        public IEnumerator PressE_InRangeOfRipeBush_LootsBerriesIntoInventory()
        {
            Assert.IsTrue(_bush.IsRipe, "precondition: bush ships ripe");
            Assert.AreEqual(0, Berries, "precondition: no berries held");

            // Stand in range of the bush, then press E (the programmatic loot edge). The loot fires on the
            // looter's next Update — it discovers the bush (IPickable), resolves it as nearest-in-range, loots.
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            _looter.RequestLoot();
            yield return null; // looter Update consumes the latch + loots

            Assert.IsFalse(_bush.IsRipe, "pressing E in range loots the ripe bush -> it goes BARE");
            Assert.AreEqual(_bush.berriesPerHarvest, Berries,
                "E-loot yields berriesPerHarvest into the inventory (the item-model AddItem seam, via TryLoot)");
        }

        // === 86caf7a6q AC4 (the load-bearing not-auto proof): standing in range WITHOUT pressing E loots
        // NOTHING. The silent-killer this guards: a regressed proximity-auto path would harvest on arrival.
        // We stand on the bush for many frames and assert it stays ripe + the pack stays empty until E. ===
        [UnityTest]
        public IEnumerator InRange_WithoutPressingE_LootsNothing_NotProximityAuto()
        {
            // Stand right on the bush (well within LootRange) and let many Updates run with NO loot request.
            _playerGo.transform.position = new Vector3(0.2f, 0f, 0.2f);
            for (int i = 0; i < 20; i++) yield return null;

            Assert.IsTrue(_bush.IsRipe,
                "standing in range with NO E press leaves the bush RIPE — it does NOT auto-harvest (AC4)");
            Assert.AreEqual(0, Berries,
                "proximity ALONE loots nothing — walking up is not enough, the player must press E (AC4)");

            // And the SAME standing position DOES loot once E is pressed (proves it was the input, not the
            // position, gating the loot).
            _looter.RequestLoot();
            yield return null;
            Assert.IsFalse(_bush.IsRipe, "pressing E from the same spot now loots — input is the gate, not range");
            Assert.AreEqual(_bush.berriesPerHarvest, Berries, "E from in range loots the berries");
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

        // === PERF (86cabnjv8 NIT 2): a RIPE bush has NO per-frame work — its Update must be DISABLED so the
        // player loop skips it (across the ~32 scatter bushes that is ~32 fewer Update dispatches/frame in the
        // common steady state). Regression guard for the self-disabling-tick optimization: a future change
        // that leaves Update always-enabled (the original per-frame poll) re-introduces the NIT and reds this.
        // The companion HarvestedBush_Regrows_BerriesAfterTimer proves the regrow STILL fires after enabling,
        // so this perf gate cannot be "passed" by simply never running Update. ===
        [UnityTest]
        public IEnumerator RipeBush_DisablesUpdate_ReEnablesOnlyWhileRegrowPending()
        {
            // A bush ships RIPE -> Awake disables its Update (no regrow pending = no per-frame work).
            yield return null; // let Awake run
            Assert.IsTrue(_bush.IsRipe, "precondition: bush ships ripe");
            Assert.IsFalse(_bush.enabled,
                "a RIPE bush DISABLES its Update — the player loop skips it (NIT 2: no per-frame poll while ripe)");

            // Harvest -> BARE -> a regrow is pending -> Update must be ENABLED so the timer can fire.
            _bush.Harvest();
            Assert.IsFalse(_bush.IsRipe, "harvest -> bare");
            Assert.IsTrue(_bush.enabled,
                "a BARE bush ENABLES its Update — the regrow timer needs the per-frame tick while pending");

            // Let the regrow fire through the player loop (Update -> Regrow), then it must DISABLE itself again.
            float start = Time.time;
            while (Time.time - start < 1f && !_bush.IsRipe) yield return null;
            Assert.IsTrue(_bush.IsRipe, "the berries regrew through the player-loop Update tick");
            Assert.IsFalse(_bush.enabled,
                "once ripe again the bush DISABLES its Update — back to zero per-frame cost (the tick self-disables)");
        }

        // === PERF (86cabnjv8 NIT 2): a PLAIN (non-berry) bush NEVER has regrow work — its Update stays
        // disabled from Awake on. Guards that the disable rule keys off (hasBerries && !ripe), so a plain
        // bush is skipped by the player loop too (and a no-op Harvest never enables the tick). ===
        [UnityTest]
        public IEnumerator PlainBush_KeepsUpdateDisabled()
        {
            // A fresh PLAIN bush: hasBerries must be set BEFORE the component's Awake runs so the Awake-time
            // tick sync sees a plain bush. Build the GameObject, set the field via a disabled-on-add trick is
            // not available, so build it inactive, add the component, configure, then activate (Awake fires on
            // activation with hasBerries already false).
            var plainGo = new GameObject("PlainBush");
            plainGo.SetActive(false);
            var plain = plainGo.AddComponent<BerryBush>();
            plain.inventory = _inv;
            plain.hasBerries = false;
            plainGo.SetActive(true); // Awake runs now, with hasBerries == false
            try
            {
                yield return null;
                Assert.IsFalse(plain.enabled,
                    "a plain bush has no berries to regrow — its Update stays DISABLED (no per-frame work, ever)");

                // A plain bush's Harvest is a no-op (returns 0) and must NOT enable the tick.
                Assert.AreEqual(0, plain.Harvest(), "plain bush Harvest is a no-op");
                yield return null;
                Assert.IsFalse(plain.enabled, "a no-op harvest on a plain bush never enables the Update tick");
            }
            finally { Object.Destroy(plainGo); }
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

        // === 86caf7a6q AC3: pressing E OUT of range loots nothing (the nearest-in-range resolve finds no
        // pickable in reach, so E is a harmless no-op) ===
        [UnityTest]
        public IEnumerator PressE_OutOfRange_LootsNothing()
        {
            // Player is far from the bush (20,_,20 from SetUp). Press E repeatedly — nothing in reach.
            for (int i = 0; i < 5; i++) { _looter.RequestLoot(); yield return null; }
            Assert.IsTrue(_bush.IsRipe, "E out of range -> the bush stays ripe (nothing in reach to loot)");
            Assert.AreEqual(0, Berries, "E out of range -> no berries (nearest-in-range resolve finds nothing)");
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

        // === AC3 leftover handling: harvesting into an inventory with only PARTIAL room returns ONLY the
        // amount that actually landed (the rest spills/leftover) — Harvest() reflects AddItem's leftover
        // contract, never over-credits the pack, and the bush still goes BARE + schedules regrow. ===
        [Test]
        public void Harvest_IntoNearlyFullInventory_AddsOnlyWhatFits_AndStillDepletes()
        {
            // Fill the WHOLE model so EXACTLY 1 berry of slot-room remains, then a 3-berry harvest can only
            // land 1 (2 spill as leftover). 86caf7g6f: berries are now a belt-eligible Consumable, so AddItem
            // spills past the full pack onto the belt too — capacity is (inventory + belt) slots × MaxStack,
            // NOT inventory-only. CountItem(berry) (the `Berries` helper) already sums both arrays.
            var berryDef = _inv.Catalog.ById(ItemCatalog.BerryId);
            Assert.IsNotNull(berryDef, "precondition: the catalog defines the berry item");
            int cap = berryDef.MaxStack *
                      (_inv.Model.InventorySlots.Count + _inv.Model.BeltSlots.Count); // 20×(20+5) = 500
            int leftoverFromFill = _inv.Model.AddItem(berryDef, cap - 1);  // pack now holds cap-1 (399)
            Assert.AreEqual(0, leftoverFromFill, "precondition: the fill itself fit (no spill)");
            Assert.AreEqual(cap - 1, Berries, "precondition: inventory loaded to cap-1");

            _bush.berriesPerHarvest = 3; // yield exceeds the 1 remaining slot of room
            int added = _bush.Harvest();

            Assert.AreEqual(1, added,
                "Harvest returns ONLY the count that fit (1 of 3) — it never over-credits a full pack (AddItem leftover contract)");
            Assert.AreEqual(cap, Berries, "the inventory tops out at cap; the overflow (2) was dropped, not negative-stored");
            Assert.IsFalse(_bush.IsRipe, "the bush still goes BARE on harvest even when the pack couldn't hold the full yield");
            Assert.Greater(_bush.RegrowAt, 0f, "a regrow is still scheduled (the bush persists + will re-ripen)");
        }

        // === AC3 fully-full case: harvesting into a COMPLETELY full inventory lands 0 — no negative store,
        // no over-credit — and the bush still depletes (a wasted harvest, but never corrupts the pack). ===
        [Test]
        public void Harvest_IntoFullInventory_AddsNothing_NoOverCredit()
        {
            var berryDef = _inv.Catalog.ById(ItemCatalog.BerryId);
            // 86caf7g6f: berries are belt-eligible now → full capacity is (inventory + belt) slots × MaxStack.
            int cap = berryDef.MaxStack *
                      (_inv.Model.InventorySlots.Count + _inv.Model.BeltSlots.Count); // 20×(20+5) = 500
            _inv.Model.AddItem(berryDef, cap); // model completely full (pack + belt)
            Assert.AreEqual(cap, Berries, "precondition: inventory full");

            _bush.berriesPerHarvest = 3;
            int added = _bush.Harvest();

            Assert.AreEqual(0, added, "a full pack accepts 0 berries — Harvest credits nothing");
            Assert.AreEqual(cap, Berries, "the count is unchanged (no over-credit, no negative store)");
            Assert.IsFalse(_bush.IsRipe, "the bush still depletes on the (wasted) harvest");
        }
    }
}
