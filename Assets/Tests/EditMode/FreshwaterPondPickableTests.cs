using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the FRESHWATER POND as an <see cref="IPickable"/> — the GET side that closes the
    /// thirst loop (ticket 86cafc6vx AC1/AC5/AC6/AC7). The pond yields ONE water per E press into the belt
    /// (the SAME E-loot verb as berries/sticks/stones/wood); it is an INFINITE source (never depletes). These
    /// tests pin the loot CONTRACT headlessly, mirroring StickProp/LogPile/BerryBush test shape:
    ///   • CanLoot is true while an inventory is wired (AC5 — infinite, repeatable) + false with none (no null-ref);
    ///   • TryLoot adds exactly ONE WaterId + returns true (AC1) — the SAME id the left-click drink consumes (#156);
    ///   • repeated TryLoot each yield one more water (the pond never runs dry — AC5);
    ///   • a FULL pack → TryLoot lands 0 → returns false, a clean no-op, no over-add (mirror Stick/LogPile, AC1);
    ///   • DisplayName == "water" + GatherVerb == "collect" → "Press E to collect water" (AC7);
    ///   • LootRange sources the VISIBLE waterline (EffectiveDrinkRadius), not the buried disc rim (#130, AC5).
    /// </summary>
    public class FreshwaterPondPickableTests
    {
        private GameObject _invGo;
        private GameObject _pondGo;
        private Inventory _inv;
        private FreshwaterPond _pond;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true; // bare rig — ignore any incidental component logs
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _pondGo = new GameObject("FreshwaterPond");
            _pondGo.transform.position = new Vector3(7f, 0f, -3f);
            _pond = _pondGo.AddComponent<FreshwaterPond>();
            _pond.inventory = _inv;
            _pond.pondSurfaceRadius = 2.6f;
            _pond.drinkRadius = 2.0f; // effective/loot range ~4.6u
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_invGo);
            Object.DestroyImmediate(_pondGo);
            LogAssert.ignoreFailingMessages = false;
        }

        // === AC5: the pond is loot-able while an inventory is wired (INFINITE source); false with none ===
        [Test]
        public void CanLoot_TrueWithInventory_FalseWithout()
        {
            Assert.IsTrue(_pond.CanLoot, "a pond with an inventory wired is loot-able (an infinite water source)");

            _pond.inventory = null;
            Assert.IsFalse(_pond.CanLoot,
                "a pond with NO inventory wired is NOT loot-able (E is then a clean no-op, never a null-ref)");
        }

        // === AC1: TryLoot adds exactly ONE WaterId + returns true (the canonical id the drink consumes) ===
        [Test]
        public void TryLoot_AddsExactlyOneWater_ReturnsTrue()
        {
            Assert.AreEqual(0, _inv.Model.CountItem(ItemCatalog.WaterId), "precondition: no water");

            bool looted = _pond.TryLoot(_inv);

            Assert.IsTrue(looted, "looting one water into an empty pack succeeds (AC1)");
            Assert.AreEqual(1, _inv.Model.CountItem(ItemCatalog.WaterId),
                "exactly ONE WaterId landed (one-per-press — mirrors stick/stone, NOT a whole-pile grab; AC1/AC5)");
        }

        // === AC5: the pond is INFINITE — repeated presses each yield one more water (it never runs dry) ===
        [Test]
        public void TryLoot_Repeated_EachYieldsOneWater_PondNeverDepletes()
        {
            _pond.TryLoot(_inv);
            _pond.TryLoot(_inv);
            _pond.TryLoot(_inv);

            Assert.AreEqual(3, _inv.Model.CountItem(ItemCatalog.WaterId),
                "three E presses each yield one water -> 3 water (the pond is an INFINITE standing source, AC5)");
            Assert.IsTrue(_pond.CanLoot, "the pond stays loot-able after any number of presses (never consumed, AC5)");
        }

        // === AC1: a FULL pack -> TryLoot lands 0 -> false, a clean no-op, no over-add (mirror Stick/LogPile) ===
        [Test]
        public void TryLoot_FullPack_IsACleanNoOp_NoOverAdd()
        {
            // Fill every slot the water item can occupy, then loot: 0 fits -> false, count unchanged, pond intact.
            var water = _inv.Catalog.ById(ItemCatalog.WaterId);
            int capacity = CountFreeWaterCapacity();
            Assert.Greater(capacity, 0, "precondition: an empty pack can hold some water");
            _inv.Model.AddItem(water, capacity); // pack now full of water
            Assert.AreEqual(0, CountFreeWaterCapacity(), "precondition: zero water room left");
            int waterFull = _inv.Model.CountItem(ItemCatalog.WaterId);

            bool looted = _pond.TryLoot(_inv);

            Assert.IsFalse(looted, "a full pack lands 0 -> TryLoot returns false (clean no-op, AC1)");
            Assert.AreEqual(waterFull, _inv.Model.CountItem(ItemCatalog.WaterId),
                "no water over-added on a full pack (the looter moves past)");
            Assert.IsTrue(_pond.CanLoot, "the pond is INFINITE — still loot-able after a declined (full-pack) loot");
        }

        // === AC1: TryLoot is graceful with NO inventory (the interface-fallback path) — no null-ref ===
        [Test]
        public void TryLoot_NoInventory_IsGracefulNoOp()
        {
            _pond.inventory = null;
            bool looted = true;
            Assert.DoesNotThrow(() => looted = _pond.TryLoot(null),
                "looting with no inventory must NOT null-ref — a clean no-op");
            Assert.IsFalse(looted, "no inventory -> nothing looted (false)");
        }

        // === AC7: the prompt copy — DisplayName "water" + GatherVerb "collect" => "Press E to collect water" ===
        [Test]
        public void Prompt_NamesWater_AndCollectVerb()
        {
            Assert.AreEqual("water", _pond.DisplayName,
                "the pond's DisplayName is the lower-case mass noun 'water' (the inventory resource word, AC7)");
            Assert.AreEqual("collect", ((IPickable)_pond).GatherVerb,
                "the pond's gather VERB is 'collect' (you collect water from a well, not 'pick up' — Uma §2a, AC7)");

            // The full prompt label flows through the GENERIC LootPrompt.BuildLabel with the pond's verb override.
            Assert.AreEqual("Press E to collect water", LootPrompt.BuildLabel(_pond, KeyCode.E),
                "the pond prompt reads 'Press E to collect water' (the verb override fits the gather action, AC7)");
        }

        // === AC5: LootRange sources the VISIBLE waterline (EffectiveDrinkRadius), not the buried disc rim ===
        [Test]
        public void LootRange_SourcesTheWaterline_EffectiveDrinkRadius()
        {
            Assert.AreEqual(_pond.EffectiveDrinkRadius, _pond.LootRange, 1e-4f,
                "the pond's E-loot reach IS its EffectiveDrinkRadius (pondSurfaceRadius keyed to the visible " +
                "waterline + the reach margin) — so the prompt + loot fire where the player SEES water, not at " +
                "the buried disc rim (#130 ROUND 8 'follow the visible waterline' lesson, AC5)");
            Assert.Greater(_pond.LootRange, 0f, "the pond must have a positive loot reach");

            // A larger surface radius (a wider pond) extends the reach — the per-item radius tracks the waterline.
            _pond.pondSurfaceRadius = 5f;
            Assert.AreEqual(5f + 2.0f, _pond.LootRange, 1e-4f,
                "a wider pond's loot reach grows with its surface radius (the waterline source, not a fixed radius)");
        }

        // The empty pack's free water capacity = how many water AddItem accepts before returning leftover. Probe
        // on a throwaway inventory so the real one stays clean (mirrors LogPileTests.CountFreeWoodCapacity).
        private int CountFreeWaterCapacity()
        {
            var probeGo = new GameObject("ProbeInv");
            var probe = probeGo.AddComponent<Inventory>();
            var water = probe.Catalog.ById(ItemCatalog.WaterId);
            int huge = 100000;
            int leftover = probe.Model.AddItem(water, huge);
            int cap = huge - leftover;
            Object.DestroyImmediate(probeGo);
            return cap - _inv.Model.CountItem(ItemCatalog.WaterId);
        }
    }
}
