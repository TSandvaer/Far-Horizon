using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage that the <see cref="Inventory"/> FAÇADE correctly maps the legacy ledger surface
    /// (HasAxe / WoodCount / CraftAxe / AddWood / SpendWood) onto the new <see cref="InventoryModel"/>
    /// (item-model contract §7 migration seam) AND exposes the new PoC surface (PickUpAxe /
    /// IsAxeSelectedInBelt). The original InventoryTests already pins the legacy BEHAVIOR (those stay
    /// green unchanged); this pins the new MAPPING so a future model change can't silently break a caller.
    /// </summary>
    public class InventoryFacadeTests
    {
        private GameObject _go;
        private Inventory _inv;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Inventory");
            _inv = _go.AddComponent<Inventory>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void HasAxe_MapsToModelOwnership()
        {
            Assert.IsFalse(_inv.HasAxe, "no axe at start");
            Assert.IsTrue(_inv.CraftAxe(), "CraftAxe places an axe (transition true)");
            Assert.IsTrue(_inv.HasAxe, "HasAxe reflects model ownership after the axe is placed");
            Assert.IsFalse(_inv.CraftAxe(), "crafting again is a no-op (already owned)");
        }

        [Test]
        public void CraftAxe_AutoPlacesIntoSelectedBelt_SoHeldAxeShows()
        {
            // The axe lands in belt slot 0, which is the default selected slot -> the held axe shows (AC4).
            _inv.CraftAxe();
            Assert.IsTrue(_inv.IsAxeSelectedInBelt,
                "after CraftAxe the axe is the SELECTED belt item (belt slot 1 auto-place + default selection)");
            Assert.AreEqual("axe", _inv.Model.BeltSlots[0].Def.Id, "the axe is in belt slot 1 (index 0)");
        }

        [Test]
        public void IsAxeSelectedInBelt_FollowsSelection_NotMereOwnership()
        {
            _inv.CraftAxe();                       // belt slot 0, selected
            Assert.IsTrue(_inv.IsAxeSelectedInBelt);

            _inv.Model.SelectBelt(1);              // select an empty slot
            Assert.IsFalse(_inv.IsAxeSelectedInBelt, "axe not in the selected slot -> not shown");
            Assert.IsTrue(_inv.HasAxe, "...but still owned (HasAxe is ownership, not selection)");
        }

        // REGRESSION GUARD (PR #224 chop-capture-gate red, run 28539711263): a SECOND belt-eligible weapon
        // (the Combat POC spear) acquired BEFORE the axe steals belt slot 0 — the DEFAULT-SELECTED slot — so
        // the later-crafted axe lands in slot 1 and IsAxeSelectedInBelt goes FALSE, silently disabling the
        // chop gate (ShouldChopOnClick needs axeSelected). The scene bug was a proximity-auto SpearPickup ON
        // the spawn radius firing frame-1 (fixed by relocating SpearPickupPosition clear of spawn). This pins
        // the underlying model INVARIANT so any future acquisition-ordering change that de-selects the axe
        // fails HERE (fast) instead of only in the shipped chop-capture gate (slow). See MovementCameraScene
        // .SpearPickupPosition + SpearPickupClearOfSpawn (ChopSceneTests) for the scene-geometry sibling guard.
        [Test]
        public void SpearAcquiredBeforeAxe_StealsSlot0_DeselectsAxe_TheChopRegression()
        {
            var spear = _inv.Catalog.ById(ItemCatalog.SpearId);
            Assert.IsNotNull(spear, "the spear is in the catalog (Combat POC AC4)");

            // Spear first -> lands in belt slot 0 (the default-selected slot). This is the state the scene
            // bug produced (proximity-auto pickup at spawn).
            Assert.IsNotNull(_inv.Model.AddToolToBelt(spear), "spear placed in belt slot 0");
            Assert.IsTrue(_inv.Model.IsSelectedBeltItem(ItemCatalog.SpearId), "spear is the selected item");

            // Then craft the axe -> slot 1, NOT the selected slot 0 -> the chop gate would see axe NOT selected.
            _inv.CraftAxe();
            Assert.IsTrue(_inv.HasAxe, "axe owned");
            Assert.IsFalse(_inv.IsAxeSelectedInBelt,
                "THE BUG: with the spear in slot 0, the crafted axe lands in slot 1 -> axe is NOT the selected " +
                "belt item -> the chop gate no-ops -> no wood (the exact chop-capture-gate regression)");
        }

        // The FIX-side invariant: with slot 0 free at craft time (the shipped ordering after the scene fix —
        // the player crafts the axe before ever reaching the relocated spear), the axe lands in slot 0 = the
        // selected slot, so chopping works even once a spear is ALSO acquired later.
        [Test]
        public void AxeCraftedBeforeSpear_StaysSelected_SoChopKeepsWorking()
        {
            _inv.CraftAxe();                       // slot 0 (selected)
            Assert.IsTrue(_inv.IsAxeSelectedInBelt, "axe is the selected belt item");

            var spear = _inv.Catalog.ById(ItemCatalog.SpearId);
            _inv.Model.AddToolToBelt(spear);       // spear -> slot 1 (does NOT change selection)
            Assert.IsTrue(_inv.IsAxeSelectedInBelt,
                "acquiring the spear AFTER the axe leaves the axe selected (slot 0) -> chop still works");
        }

        [Test]
        public void WoodCount_SumsAcrossStacks()
        {
            _inv.AddWood(20); // fills one stack to the cap
            _inv.AddWood(7);  // spills to a second stack
            Assert.AreEqual(27, _inv.WoodCount, "WoodCount sums Count across ALL wood stacks (the silent-killer guard)");
        }

        [Test]
        public void AddWood_ZeroOrNegative_IsNoOp()
        {
            Assert.AreEqual(0, _inv.AddWood(0));
            Assert.AreEqual(0, _inv.AddWood(-4));
            Assert.AreEqual(0, _inv.WoodCount);
        }

        [Test]
        public void SpendWood_AllOrNothing_AcrossStacks()
        {
            _inv.AddWood(25); // 20 + 5 across two stacks
            Assert.IsFalse(_inv.SpendWood(26), "can't afford -> false, debit nothing");
            Assert.AreEqual(25, _inv.WoodCount, "a failed spend debits nothing");
            Assert.IsTrue(_inv.SpendWood(22), "affordable spend succeeds");
            Assert.AreEqual(3, _inv.WoodCount, "exactly 22 spent");
        }

        [Test]
        public void PickUpAxe_PlacesInBeltSlot1()
        {
            Assert.IsTrue(_inv.PickUpAxe(), "first pickup places the axe (transition)");
            Assert.AreEqual("axe", _inv.Model.BeltSlots[0].Def.Id, "axe -> belt slot 1");
            Assert.IsFalse(_inv.PickUpAxe(), "a second pickup is a no-op (already owned)");
        }

        [Test]
        public void Changed_Forwarded_FromModel()
        {
            int fires = 0;
            _inv.Changed += () => fires++;
            _inv.AddWood(2);       // model Changed -> façade Changed
            _inv.CraftAxe();       // again
            Assert.AreEqual(2, fires, "the façade forwards the model's Changed event");
        }
    }
}
