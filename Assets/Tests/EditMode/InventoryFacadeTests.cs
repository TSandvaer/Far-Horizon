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
