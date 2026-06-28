using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the LEFT-CLICK CONSUME loop (ticket 86caf7a30 — left-click USES the selected belt
    /// item; the item TYPE determines the effect). Drives <see cref="LeftClickConsume.TryConsumeSelected"/>
    /// directly (isolating the consume LOGIC from the mouse device — the click path itself is one
    /// Input.GetMouseButtonDown line + a shipped-build capture) and proves the dispatch END TO END:
    ///   - select BERRY + left-click -> EXACTLY one berry consumed AND hunger restored (the SHIPPED atomic
    ///     EatBerryAction.TryEatOneBerry / AddFood seam — consume + restore inseparable);
    ///   - select WATER + left-click -> EXACTLY one water consumed AND thirst restored (RemoveItem + the SHIPPED
    ///     ThirstNeed.AddWater — atomic);
    ///   - select AXE + left-click -> NO consume (the axe is ChopTree's disjoint branch — the chop-still-fires
    ///     regression guard at the consume layer: consume must refuse the axe);
    ///   - select NOTHING / a non-consumable -> clean no-op (no consume, no error);
    ///   - one click = ONE consume (no double-consume on a single click).
    ///
    /// Sibling of EatBerryActionPlayModeTests / DrinkActionPlayModeTests — the unified-trigger counterpart that
    /// re-points the eat/drink to the left-click-while-selected model.
    /// </summary>
    public class LeftClickConsumePlayModeTests
    {
        private GameObject _go;
        private Inventory _inv;
        private HungerNeed _hunger;
        private ThirstNeed _thirst;
        private EatBerryAction _eat;
        private LeftClickConsume _consume;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("LeftClickConsumeTest");
            _inv = _go.AddComponent<Inventory>();

            _hunger = _go.AddComponent<HungerNeed>();
            _hunger.max = 100f;
            _hunger.startFull = false;            // start hungry so the restore is observable
            _hunger.berryRestoreAmount = 18f;

            _thirst = _go.AddComponent<ThirstNeed>();
            _thirst.max = 100f;
            _thirst.startFull = false;            // start thirsty so the restore is observable
            _thirst.waterScoopAmount = 14f;

            // The SHIPPED atomic berry eat-seam the consume reuses (no re-implemented restore).
            _eat = _go.AddComponent<EatBerryAction>();
            _eat.inventory = _inv;
            _eat.hunger = _hunger;

            _consume = _go.AddComponent<LeftClickConsume>();
            _consume.inventory = _inv;
            _consume.hunger = _hunger;
            _consume.thirst = _thirst;
            _consume.eatSeam = _eat;
            _consume.inventoryUI = null;          // no UI rig -> the over-UI guard is skipped (bare test rig)
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_go);

        private int Berries => _inv.Model.CountItem(ItemCatalog.BerryId);
        private int Water => _inv.Model.CountItem(ItemCatalog.WaterId);

        // Give n of an item, then SELECT the belt slot it landed in (consumables auto-land on the belt — found,
        // not assumed). Returns true once the id is the selected belt item.
        private bool GiveAndSelect(string id, int n)
        {
            var def = _inv.Catalog.ById(id);
            Assert.IsNotNull(def, "the catalog must define " + id);
            _inv.Model.AddItem(def, n);
            var belt = _inv.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
            {
                var s = _inv.Model.At(SlotRef.Belt(i));
                if (!s.IsEmpty && s.Def.Id == id) { _inv.Model.SelectBelt(i); break; }
            }
            return _inv.Model.IsSelectedBeltItem(id);
        }

        // === SELECT BERRY + LEFT-CLICK -> one berry consumed AND hunger restored (atomic, end-to-end) =====
        [Test]
        public void LeftClick_BerrySelected_EatsOneBerry_AndRestoresHunger()
        {
            Assert.IsTrue(GiveAndSelect(ItemCatalog.BerryId, 3), "precondition: 3 berries held + berry selected");
            float hungerBefore = _hunger.Current; // 0 at start (startFull=false), no tick yet

            bool consumed = _consume.TryConsumeSelected();

            Assert.IsTrue(consumed, "left-click with a berry selected consumes it (the dispatch fires the eat seam)");
            Assert.AreEqual(2, Berries, "EXACTLY one berry consumed (the stack decrements by one)");
            Assert.AreEqual(hungerBefore + _hunger.berryRestoreAmount, _hunger.Current, 0.01f,
                "the left-click eat restores exactly berryRestoreAmount hunger (consume + restore atomic, " +
                "through the SHIPPED HungerNeed.AddFood seam — not re-implemented)");
        }

        // === SELECT WATER + LEFT-CLICK -> one water consumed AND thirst restored (atomic) =================
        [Test]
        public void LeftClick_WaterSelected_DrinksOneWater_AndRestoresThirst()
        {
            Assert.IsTrue(GiveAndSelect(ItemCatalog.WaterId, 2), "precondition: 2 water held + water selected");
            float thirstBefore = _thirst.Current;

            bool consumed = _consume.TryConsumeSelected();

            Assert.IsTrue(consumed, "left-click with water selected consumes it (the dispatch fires the drink path)");
            Assert.AreEqual(1, Water, "EXACTLY one water unit consumed (RemoveItem(WaterId,1) — atomic)");
            Assert.AreEqual(thirstBefore + _thirst.waterScoopAmount, _thirst.Current, 0.01f,
                "the left-click drink restores exactly waterScoopAmount thirst (through the SHIPPED " +
                "ThirstNeed.AddWater seam — not re-implemented)");
        }

        // === SELECT AXE + LEFT-CLICK -> NO consume (chop owns the axe; consume must refuse it) ============
        [Test]
        public void LeftClick_AxeSelected_DoesNotConsume_ChopRegressionGuard()
        {
            // Put the axe on the belt + select it. A left-click with the axe selected must NOT consume anything
            // (the axe is a Tool, not a Consumable) — ChopTree's DISJOINT branch handles axe→chop. This is the
            // consume-layer half of the "left-click with the axe still chops, no consume regression" guard.
            Assert.IsTrue(_inv.PickUpAxe(), "precondition: axe picked up onto the belt");
            // Select the belt slot holding the axe.
            var belt = _inv.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
            {
                var s = _inv.Model.At(SlotRef.Belt(i));
                if (!s.IsEmpty && s.Def.Id == ItemCatalog.AxeId) { _inv.Model.SelectBelt(i); break; }
            }
            Assert.IsTrue(_inv.IsAxeSelectedInBelt, "precondition: the axe is the selected belt item");
            // Also hold a berry in the pack so a (wrong) consume would have something to eat — proving the
            // no-consume is because the AXE is selected, not because nothing is consumable.
            _inv.Model.AddItem(_inv.Catalog.ById(ItemCatalog.BerryId), 1);
            int berriesBefore = Berries;
            float hungerBefore = _hunger.Current;

            bool consumed = _consume.TryConsumeSelected();

            Assert.IsFalse(consumed, "a left-click with the AXE selected must NOT consume (chop owns the axe)");
            Assert.AreEqual(berriesBefore, Berries,
                "no berry consumed when the axe is selected (consume refuses the tool — the disjoint branch)");
            Assert.AreEqual(hungerBefore, _hunger.Current, 0.0001f, "no restore when the axe is selected");
        }

        // === SELECT NOTHING -> clean no-op (no consume, no error) =========================================
        [Test]
        public void LeftClick_EmptySelection_IsNoOp()
        {
            // Nothing on the belt (fresh inventory) -> the selected belt slot is empty.
            Assert.IsTrue(_inv.Model.SelectedBeltStack.IsEmpty, "precondition: nothing selected on the belt");
            float hungerBefore = _hunger.Current, thirstBefore = _thirst.Current;

            bool consumed = false;
            Assert.DoesNotThrow(() => consumed = _consume.TryConsumeSelected(),
                "a left-click with an empty selection must be a clean no-op, never an error");
            Assert.IsFalse(consumed, "empty selection -> nothing consumed");
            Assert.AreEqual(hungerBefore, _hunger.Current, 0.0001f, "no hunger change");
            Assert.AreEqual(thirstBefore, _thirst.Current, 0.0001f, "no thirst change");
        }

        // === ONE click = ONE consume (no double-consume on a single click) ================================
        [Test]
        public void LeftClick_SingleClick_ConsumesExactlyOne()
        {
            Assert.IsTrue(GiveAndSelect(ItemCatalog.BerryId, 5), "precondition: 5 berries held + selected");
            int before = Berries;

            _consume.TryConsumeSelected(); // ONE click

            Assert.AreEqual(before - 1, Berries,
                "a SINGLE left-click consumes EXACTLY one berry (no double-consume on one click)");
        }

        // === Empty-handed water click against no water -> no-op (all-or-nothing) ==========================
        [Test]
        public void LeftClick_WaterSelected_NoWaterLeft_IsAtomicNoOp()
        {
            Assert.IsTrue(GiveAndSelect(ItemCatalog.WaterId, 1), "precondition: 1 water held + selected");
            Assert.IsTrue(_consume.TryConsumeSelected(), "the only water is drunk");
            Assert.AreEqual(0, Water, "no water left");
            float thirstAfterFirst = _thirst.Current;

            // The slot is now empty -> a second click finds nothing selected -> no-op (no negative inventory).
            bool again = _consume.TryConsumeSelected();
            Assert.IsFalse(again, "a second click with no water left is a no-op");
            Assert.AreEqual(0, Water, "no negative inventory after draining the water");
            Assert.AreEqual(thirstAfterFirst, _thirst.Current, 0.0001f, "no extra restore on the empty click");
        }

        // === Graceful with no ThirstNeed wired -> consumes water, no restore, no null-ref =================
        [Test]
        public void LeftClick_WaterSelected_NoThirstNeed_ConsumesGracefully()
        {
            _consume.thirst = null;
            Assert.IsTrue(GiveAndSelect(ItemCatalog.WaterId, 2), "precondition: 2 water held + selected");

            bool consumed = false;
            Assert.DoesNotThrow(() => consumed = _consume.TryConsumeSelected(),
                "drinking with no ThirstNeed wired must NOT null-ref (graceful)");
            Assert.IsTrue(consumed, "the water is still consumed when no thirst need is present");
            Assert.AreEqual(1, Water, "graceful no-ThirstNeed: exactly one water consumed, restore no-op'd");
        }

        // === LEFT-CLICK eat VISIBLY refills the hunger HUD BAR (the #101 percept, via the new trigger) ====
        [UnityTest]
        public IEnumerator LeftClick_BerryEat_RefillsTheHungerHudBar()
        {
            var sceneGo = new GameObject("ConsumeHudRefillScene");
            var inv = sceneGo.AddComponent<Inventory>();
            var hunger = sceneGo.AddComponent<HungerNeed>();
            hunger.max = 100f;
            hunger.startFull = false;
            hunger.startFraction01 = HungerNeed.HungerStartFraction01; // 0.55 -> ~5 of 10 segments at spawn
            hunger.berryRestoreAmount = 18f;

            var hud = sceneGo.AddComponent<SurvivalHud>();
            hud.hunger = hunger;

            var eat = sceneGo.AddComponent<EatBerryAction>();
            eat.inventory = inv;
            eat.hunger = hunger;

            var consume = sceneGo.AddComponent<LeftClickConsume>();
            consume.inventory = inv;
            consume.hunger = hunger;
            consume.eatSeam = eat;
            consume.inventoryUI = null;

            var def = inv.Catalog.ById(ItemCatalog.BerryId);
            inv.Model.AddItem(def, 1);
            // Select the berry belt slot.
            var belt = inv.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
            {
                var s = inv.Model.At(SlotRef.Belt(i));
                if (!s.IsEmpty && s.Def.Id == ItemCatalog.BerryId) { inv.Model.SelectBelt(i); break; }
            }

            yield return null; // Start() seeds _current + fires the initial Changed

            int litBefore = SurvivalHud.FilledSegments(hud.hunger.Current01);
            Assert.Less(litBefore, SurvivalHud.SegmentCount,
                "the SHIPPED hunger config must NOT start the bar full (else an eat shows no refill)");

            bool ate = consume.TryConsumeSelected();
            Assert.IsTrue(ate, "the LEFT-CLICK eat fires");
            Assert.AreEqual(0, inv.Model.CountItem(ItemCatalog.BerryId), "exactly one (the only) berry consumed");

            int litAfter = SurvivalHud.FilledSegments(hud.hunger.Current01);
            Assert.Greater(litAfter, litBefore,
                "after a LEFT-CLICK eat, the HUD lights MORE hunger segments — the bar VISIBLY refills " +
                $"(before={litBefore} -> after={litAfter}, Current01={hud.hunger.Current01:0.00})");

            Object.Destroy(sceneGo);
        }

        // === No inventory wired -> safe no-op (build-safety: never null-ref) ==============================
        [Test]
        public void LeftClick_WithNoInventory_IsSafeNoOp()
        {
            _consume.inventory = null;
            bool consumed = false;
            Assert.DoesNotThrow(() => consumed = _consume.TryConsumeSelected(),
                "a left-click with no inventory wired must be a safe no-op, never a null-ref");
            Assert.IsFalse(consumed, "no inventory -> nothing consumed");
        }
    }
}
