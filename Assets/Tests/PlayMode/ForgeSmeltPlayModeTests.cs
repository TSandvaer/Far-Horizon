using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the I-3 FORGE build + SMELT loop (ticket 86cakkmvc), driving the Inventory/Forge/
    /// ForgePlacement seams directly (no scene, no input injection) to isolate the mechanic:
    ///
    ///   • BUILD gate (ForgePlacement.TryBuild) — enough wood+stone → the forge builds + debits BOTH; too few of
    ///     EITHER → no build, NO debit (the load-bearing "not enough mats → no furnace" negative case).
    ///   • SMELT over a TIMER — a built forge with the player in range + ore + fuel begins a smelt, and after the
    ///     smelt SECONDS elapse yields ONE iron-ingot (ore + fuel debited up front). The timer is proven against
    ///     SIMULATED time — the assertion that it does NOT complete before the seconds elapse is the timer's teeth.
    ///   • insufficient FUEL → no smelt (no ingot, no ore debit).
    ///   • Inventory.SpendStone all-or-nothing (the forge-build debit seam).
    ///
    /// FOLDED REVIEW NIT (Tess): the smelt-timer test advances SIMULATED time via the #288 deterministic TestClock
    /// seam (Forge.TestClock — the SAME pattern ChopTree uses), NOT Time.captureDeltaTime (proven INEFFECTIVE in CI
    /// batchmode — headless deltaTime≈0 / captureDeltaTime unhonored, unity-conventions.md §headless time). The TEST
    /// OWNS the clock the Forge reads + advances it a fixed StableStep per frame, so "the smelt seconds elapsed" is
    /// deterministic and clock-independent while the shipped gate logic (a plain Time.time read) is byte-unchanged.
    /// </summary>
    public class ForgeSmeltPlayModeTests
    {
        private GameObject _invGo, _playerGo, _forgeGo;
        private Inventory _inv;
        private Forge _forge;

        // OWNED DETERMINISTIC CLOCK (#288 pattern) — the smelt timer reads Forge.Now via this; Step() advances it a
        // fixed StableStep per frame so the seconds-gate spans a deterministic frame count regardless of the
        // (un-honored) headless engine clock. Step 0.01s (100Hz): a 0.30s smelt spans ~30 frames, well inside budget.
        private const float StableStep = 0.01f;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
            LogAssert.ignoreFailingMessages = true; // bare rig: Forge/Inventory Awake logs are not under test

            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, 0f, 0f);

            _forgeGo = new GameObject("Forge");
            _forgeGo.transform.position = new Vector3(0f, 0f, 0f); // co-located → player is trivially "in range"
            _forge = _forgeGo.AddComponent<Forge>();
            _forge.inventory = _inv;
            _forge.player = _playerGo.transform;
            _forge.smeltRadius = 3f;
            // Cheap fast batch so the headless test advances quickly (the CONVERSION + the timer gate are under
            // test, not the wall-clock duration). 1 ore + 1 fuel per ingot; a SHORT smelt time.
            _forge.orePerIngot = 1;
            _forge.fuelPerSmelt = 1;
            _forge.smeltSeconds = 0.30f;
            _forge.TestClock = () => _now; // inject the owned deterministic clock (#288)
        }

        [TearDown]
        public void TearDown()
        {
            if (_invGo != null) Object.Destroy(_invGo);
            if (_playerGo != null) Object.Destroy(_playerGo);
            if (_forgeGo != null) Object.Destroy(_forgeGo);
            LogAssert.ignoreFailingMessages = false;
        }

        // Advance the OWNED clock one fixed StableStep + tick one frame (the deterministic analog of a rendered
        // frame). Waits that expect the smelt timer to progress step via these; a bare `yield return null` is used
        // ONLY where the assertion is that NOTHING happens.
        private IEnumerator Step() { _now += StableStep; yield return null; }
        private IEnumerator StepFrames(int n) { for (int i = 0; i < n; i++) yield return Step(); }
        private IEnumerator StepUntil(System.Func<bool> done, int maxFrames = 4000)
        {
            int f = 0;
            while (!done() && f++ < maxFrames) yield return Step();
        }

        private void GrantResource(string id, int amount)
        {
            var def = _inv.Catalog.ById(id);
            Assert.IsNotNull(def, "catalog must resolve " + id);
            _inv.Model.AddItem(def, amount);
        }

        private int Count(string id) => _inv.Model.CountItem(id);

        // === SMELT over a TIMER (the folded-NIT test — SIMULATED time) ===
        [UnityTest]
        public IEnumerator BuiltForge_WithOreAndFuel_SmeltsOneIngot_ONLY_AfterTheTimerElapses()
        {
            _forge.Build();
            GrantResource(ItemCatalog.IronOreId, 1); // exactly one batch's ore
            GrantResource(ItemCatalog.WoodId, 1);    // exactly one batch's fuel
            Assert.AreEqual(0, Count(ItemCatalog.IronIngotId), "precondition: no ingot yet");

            // Frame 1 begins the smelt (in range + built + mats). The ore + fuel are debited UP FRONT.
            yield return Step();
            Assert.IsTrue(_forge.IsSmelting, "the smelt must START (built + in range + ore + fuel)");
            Assert.AreEqual(0, Count(ItemCatalog.IronOreId), "ore is debited up front at smelt start");
            Assert.AreEqual(0, Count(ItemCatalog.WoodId), "fuel (wood) is debited up front at smelt start");

            // BEFORE the seconds elapse (advance to ~half the smelt time): NO ingot yet — the timer has teeth.
            yield return StepFrames(12); // ~0.12s of the 0.30s smelt (well short)
            Assert.AreEqual(0, Count(ItemCatalog.IronIngotId),
                "the ingot must NOT appear before the smelt seconds elapse (SIMULATED-time gate) — this is the " +
                "timer's teeth; if this reds, the smelt completes instantly (no work-led earn)");
            Assert.IsTrue(_forge.IsSmelting, "still smelting mid-timer");

            // Advance PAST the smelt seconds → exactly ONE ingot lands, and the forge goes idle (mats exhausted).
            yield return StepUntil(() => _forge.CompletedSmelts >= 1);
            Assert.AreEqual(1, _forge.CompletedSmelts, "exactly one batch completed");
            Assert.AreEqual(1, Count(ItemCatalog.IronIngotId), "one smelt yields exactly ONE iron ingot");
            Assert.IsFalse(_forge.IsSmelting, "idle after the batch (no mats left for a second)");
        }

        // === insufficient FUEL → no smelt ===
        [UnityTest]
        public IEnumerator BuiltForge_WithOreButNoFuel_DoesNotSmelt()
        {
            _forge.Build();
            GrantResource(ItemCatalog.IronOreId, 3); // ore aplenty
            // NO wood granted → fuel short (fuelPerSmelt=1).
            Assert.AreEqual(0, Count(ItemCatalog.WoodId), "precondition: no fuel");

            yield return StepFrames(60); // plenty of time for a smelt to have started/finished if the gate were open

            Assert.IsFalse(_forge.IsSmelting, "no fuel → no smelt starts");
            Assert.AreEqual(0, _forge.CompletedSmelts, "no fuel → no batch completes");
            Assert.AreEqual(0, Count(ItemCatalog.IronIngotId), "no fuel → no ingot");
            Assert.AreEqual(3, Count(ItemCatalog.IronOreId), "no fuel → ore is NOT debited (no half-paid smelt)");
        }

        // === NOT built → no smelt even with mats + in range ===
        [UnityTest]
        public IEnumerator UnbuiltForge_WithMats_DoesNotSmelt()
        {
            // Do NOT Build().
            GrantResource(ItemCatalog.IronOreId, 2);
            GrantResource(ItemCatalog.WoodId, 2);

            yield return StepFrames(60);

            Assert.IsFalse(_forge.IsSmelting, "an unbuilt forge never smelts");
            Assert.AreEqual(0, _forge.CompletedSmelts, "unbuilt → no batches");
            Assert.AreEqual(2, Count(ItemCatalog.IronOreId), "unbuilt → ore untouched");
        }

        // === the BUILD gate (ForgePlacement.TryBuild) — all-or-nothing across wood+stone ===
        [UnityTest]
        public IEnumerator ForgePlacement_BuildGate_IsAllOrNothing_NoMatsNoBuildNoDebit()
        {
            var placeGo = new GameObject("ForgePlacement");
            var place = placeGo.AddComponent<ForgePlacement>();
            place.inventory = _inv;
            place.forge = _forge;
            place.player = _playerGo.transform;
            place.woodCost = 4;
            place.stoneCost = 5;
            yield return null;

            // Too little STONE (enough wood) → NO build, NO debit.
            GrantResource(ItemCatalog.WoodId, 4);
            GrantResource(ItemCatalog.StoneId, 3); // one short
            Assert.IsFalse(place.TryBuild(), "1 stone short → build must fail");
            Assert.IsFalse(place.HasBuilt, "failed build → forge NOT built");
            Assert.IsFalse(_forge.IsBuilt, "failed build → Forge stays unbuilt");
            Assert.AreEqual(4, Count(ItemCatalog.WoodId), "failed build → NO wood debited (all-or-nothing)");
            Assert.AreEqual(3, Count(ItemCatalog.StoneId), "failed build → NO stone debited");

            // Top up the stone → now affordable → builds + debits BOTH.
            GrantResource(ItemCatalog.StoneId, 2); // now 5
            Assert.IsTrue(place.TryBuild(), "enough of both → build succeeds");
            Assert.IsTrue(place.HasBuilt, "built");
            Assert.IsTrue(_forge.IsBuilt, "the Forge is now built");
            Assert.AreEqual(0, Count(ItemCatalog.WoodId), "wood debited on build");
            Assert.AreEqual(0, Count(ItemCatalog.StoneId), "stone debited on build");

            Object.Destroy(placeGo);
        }

        // === Inventory.SpendStone — the forge-build debit seam (all-or-nothing, the SpendWood sibling) ===
        [UnityTest]
        public IEnumerator SpendStone_IsAllOrNothing()
        {
            GrantResource(ItemCatalog.StoneId, 3);

            Assert.IsFalse(_inv.SpendStone(5), "spending more stone than held fails");
            Assert.AreEqual(3, Count(ItemCatalog.StoneId), "a failed spend debits NOTHING");

            Assert.IsTrue(_inv.SpendStone(2), "spending within the held amount succeeds");
            Assert.AreEqual(1, Count(ItemCatalog.StoneId), "the spent stone is debited");

            Assert.IsTrue(_inv.SpendStone(0), "spending zero trivially succeeds (matches SpendWood)");
            yield return null;
        }
    }
}
