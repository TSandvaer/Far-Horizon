using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the 86cahngdg soak-224 crossed-visual fix — the BELT SELECTION owns the held
    /// visual. The defect: the seat's visibility gate fired on IsAxeSelectedInBelt while the DISPLAYED mesh
    /// stayed whatever the [B] debug cycle last set — so with the spear mesh displayed, selecting the AXE
    /// slot rendered the SPEAR in hand, and selecting the SPEAR slot rendered EMPTY hands (no spear
    /// predicate, no selection->mesh sync). These tests pin the PURE selection->family-index mapping the
    /// sync applies, the family-contract constants it depends on, and the Inventory selection predicates
    /// across BOTH pickup orders (AC2) — all in the BLOCKING EditMode lane (PlayMode is advisory; the
    /// component-level renderer/mesh table lives in HeldBeltWeaponVisualPlayModeTests).
    /// </summary>
    public class HeldBeltVisualSyncTests
    {
        // --- The pure selection -> family-index mapping (the sync's decision table). ---

        [Test]
        public void SelectionIndexFor_AxeSelected_IsAxeIndex()
            => Assert.AreEqual(HeldWeaponCycleDebug.AxeFamilyIndex,
                               HeldWeaponCycleDebug.SelectionIndexFor(true, false));

        [Test]
        public void SelectionIndexFor_SpearSelected_IsSpearIndex()
            => Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex,
                               HeldWeaponCycleDebug.SelectionIndexFor(false, true));

        [Test]
        public void SelectionIndexFor_NothingWeaponSelected_IsMinusOne()
            => Assert.AreEqual(-1, HeldWeaponCycleDebug.SelectionIndexFor(false, false),
                "empty / berry / water / weapon-in-pack selection drives NO held-weapon mesh (the gate hides the seat)");

        [Test]
        public void SelectionIndexFor_AxeWins_WhenBothFlagsSet()
            // Both flags true cannot happen from one selected slot; pin the deterministic tie-break anyway.
            => Assert.AreEqual(HeldWeaponCycleDebug.AxeFamilyIndex,
                               HeldWeaponCycleDebug.SelectionIndexFor(true, true));

        // --- The family-contract constants the sync depends on (a reorder would silently re-cross). ---

        [Test]
        public void FamilyContract_SpearIndexNamesTheSpearNode()
        {
            Assert.AreEqual(0, HeldWeaponCycleDebug.AxeFamilyIndex, "the axe is the locked default index 0");
            Assert.AreEqual("wpn_axe_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.AxeFamilyIndex]);
            Assert.AreEqual("wpn_spear_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.SpearFamilyIndex],
                "SpearFamilyIndex MUST name the spear node — a WeaponNodeNames reorder without re-pinning " +
                "this index would render the WRONG weapon for the selected spear (the crossed-visual class)");
            Assert.AreEqual("SPEAR",
                HeldWeaponCycleDebug.WeaponLabels[HeldWeaponCycleDebug.SpearFamilyIndex]);
        }

        // --- AC2: BOTH pickup orders, per-selection predicates -> the index the sync will apply. ---
        // (The InventoryFacadeTests pin slot placement; this pins the VISUAL-layer decision derived from it.)

        private static Inventory NewInventory(out GameObject go)
        {
            go = new GameObject("Inventory");
            return go.AddComponent<Inventory>();
        }

        private static int DesiredIndex(Inventory inv)
            => HeldWeaponCycleDebug.SelectionIndexFor(inv.IsAxeSelectedInBelt, inv.IsSpearSelectedInBelt);

        [Test]
        public void AxeThenSpear_SelectionTable_MapsToTheRightHeldMesh()
        {
            var inv = NewInventory(out var go);
            try
            {
                Assert.IsTrue(inv.PickUpAxe(), "axe acquired (slot 0, selected)");
                Assert.AreEqual(HeldWeaponCycleDebug.AxeFamilyIndex, DesiredIndex(inv),
                    "axe selected -> the AXE mesh");

                Assert.IsTrue(inv.PickUpSpear(), "spear acquired (slot 1, NOT selected)");
                Assert.AreEqual(HeldWeaponCycleDebug.AxeFamilyIndex, DesiredIndex(inv),
                    "acquiring the spear does NOT change the held visual (axe still selected)");

                inv.Model.SelectBelt(1); // the spear's slot
                Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex, DesiredIndex(inv),
                    "spear selected -> the SPEAR mesh (soak-224: this used to render EMPTY hands)");

                inv.Model.SelectBelt(2); // an empty slot
                Assert.AreEqual(-1, DesiredIndex(inv), "empty selected -> no held weapon");

                inv.Model.SelectBelt(0); // back to the axe
                Assert.AreEqual(HeldWeaponCycleDebug.AxeFamilyIndex, DesiredIndex(inv),
                    "re-selecting the axe -> the AXE mesh returns (soak-224: this used to render the SPEAR)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SpearThenAxe_SelectionTable_MapsToTheRightHeldMesh()
        {
            var inv = NewInventory(out var go);
            try
            {
                Assert.IsTrue(inv.PickUpSpear(), "spear acquired FIRST (slot 0, selected)");
                Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex, DesiredIndex(inv),
                    "spear-first pickup lands selected -> the SPEAR mesh immediately");

                Assert.IsTrue(inv.PickUpAxe(), "axe acquired second (slot 1, NOT selected)");
                Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex, DesiredIndex(inv),
                    "acquiring the axe does NOT steal the held visual (spear still selected)");

                inv.Model.SelectBelt(1); // the axe's slot
                Assert.AreEqual(HeldWeaponCycleDebug.AxeFamilyIndex, DesiredIndex(inv),
                    "axe selected -> the AXE mesh (order-independent — AC2)");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
