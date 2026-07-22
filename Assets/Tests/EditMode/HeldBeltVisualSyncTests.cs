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
                               HeldWeaponCycleDebug.SelectionIndexFor(true, false, false, false));

        [Test]
        public void SelectionIndexFor_SpearSelected_IsSpearIndex()
            => Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex,
                               HeldWeaponCycleDebug.SelectionIndexFor(false, true, false, false));

        // I-2 (86cakkmr0) — the pickaxe tiers now map (the soak-fail was the belt→held sync omitting them).
        [Test]
        public void SelectionIndexFor_PickaxeStoneSelected_IsPickaxeStoneIndex()
            => Assert.AreEqual(HeldWeaponCycleDebug.PickaxeStoneFamilyIndex,
                               HeldWeaponCycleDebug.SelectionIndexFor(false, false, true, false),
                "stone pickaxe selected -> the STONE pickaxe mesh (the belt→held sync now maps the 5th tool type)");

        [Test]
        public void SelectionIndexFor_PickaxeIronSelected_IsPickaxeIronIndex()
            => Assert.AreEqual(HeldWeaponCycleDebug.PickaxeIronFamilyIndex,
                               HeldWeaponCycleDebug.SelectionIndexFor(false, false, false, true),
                "iron pickaxe selected -> the IRON pickaxe mesh");

        [Test]
        public void SelectionIndexFor_NothingWeaponSelected_IsMinusOne()
            => Assert.AreEqual(-1, HeldWeaponCycleDebug.SelectionIndexFor(false, false, false, false),
                "empty / berry / water / weapon-in-pack selection drives NO held-weapon mesh (the gate hides the seat)");

        [Test]
        public void SelectionIndexFor_Priority_AxeWinsThenSpearThenPickaxe()
        {
            // Only one belt slot is selected in play, so at most one flag is ever true; pin the deterministic
            // tie-break anyway (axe > spear > pickaxe-stone > pickaxe-iron).
            Assert.AreEqual(HeldWeaponCycleDebug.AxeFamilyIndex,
                            HeldWeaponCycleDebug.SelectionIndexFor(true, true, true, true), "axe wins");
            Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex,
                            HeldWeaponCycleDebug.SelectionIndexFor(false, true, true, true), "spear next");
            Assert.AreEqual(HeldWeaponCycleDebug.PickaxeStoneFamilyIndex,
                            HeldWeaponCycleDebug.SelectionIndexFor(false, false, true, true), "pickaxe-stone next");
        }

        // --- The family-contract constants the sync depends on (a reorder would silently re-cross). ---

        [Test]
        public void FamilyContract_SpearIndexNamesTheSpearNode()
        {
            Assert.AreEqual(0, HeldWeaponCycleDebug.AxeFamilyIndex, "the axe is the locked default index 0");
            Assert.AreEqual("wpn_axe_stone_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.AxeFamilyIndex]);
            Assert.AreEqual("wpn_spear_stone_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.SpearFamilyIndex],
                "SpearFamilyIndex MUST name the STONE spear node — a WeaponNodeNames reorder without re-pinning " +
                "this index would render the WRONG weapon for the selected spear (the crossed-visual class; 86cajkk7h)");
            Assert.AreEqual("SPEAR",
                HeldWeaponCycleDebug.WeaponLabels[HeldWeaponCycleDebug.SpearFamilyIndex]);
            // I-2 (86cakkmr0) — the pickaxe indices must name the pickaxe nodes (a reorder would cross the held
            // visual for a selected pickaxe, the same class the spear pin guards).
            Assert.AreEqual("wpn_pickaxe_stone_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.PickaxeStoneFamilyIndex],
                "PickaxeStoneFamilyIndex MUST name the STONE pickaxe node");
            Assert.AreEqual("wpn_pickaxe_iron_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.PickaxeIronFamilyIndex],
                "PickaxeIronFamilyIndex MUST name the IRON pickaxe node");
        }

        // --- AC2: BOTH pickup orders, per-selection predicates -> the index the sync will apply. ---
        // (The InventoryFacadeTests pin slot placement; this pins the VISUAL-layer decision derived from it.)

        private static Inventory NewInventory(out GameObject go)
        {
            go = new GameObject("Inventory");
            return go.AddComponent<Inventory>();
        }

        private static int DesiredIndex(Inventory inv)
            => HeldWeaponCycleDebug.SelectionIndexFor(inv.IsAxeSelectedInBelt, inv.IsSpearSelectedInBelt,
                                                      inv.IsPickaxeStoneSelectedInBelt, inv.IsPickaxeIronSelectedInBelt);

        // I-2 (86cakkmr0) — the SOAK-FAIL regression: acquire a stone pickaxe, SELECT its belt slot, and the
        // belt→held sync must map it to the STONE pickaxe mesh index (the defect returned -1 -> empty hands).
        [Test]
        public void PickaxeSelected_SelectionTable_MapsToThePickaxeMesh()
        {
            var inv = NewInventory(out var go);
            try
            {
                var slot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.PickaxeStoneId));
                Assert.IsTrue(slot.HasValue, "stone pickaxe acquired (a belt-eligible Tool)");
                inv.Model.SelectBelt(slot.Value.Index);
                Assert.IsTrue(inv.IsPickaxeStoneSelectedInBelt, "precondition: the stone pickaxe is selected");
                Assert.AreEqual(HeldWeaponCycleDebug.PickaxeStoneFamilyIndex, DesiredIndex(inv),
                    "stone pickaxe selected -> the STONE pickaxe mesh (soak-fail: this used to return -1 = empty hands)");

                inv.Model.SelectBelt((slot.Value.Index + 1) % inv.BeltSlotCount); // deselect
                Assert.AreEqual(-1, DesiredIndex(inv), "pickaxe owned but NOT selected -> no held mesh");
            }
            finally { Object.DestroyImmediate(go); }
        }

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

        // ============================================================================================
        // 86caffwv5 soak-3 — WOOD tier: a crafted WOOD weapon selected in the belt showed NOTHING in the hand.
        // Root cause (Drew's trace): the wood ids (axe_wood/…) mapped through NEITHER SelectionIndexFor (stone/iron
        // only, -1) NOR HeldAxe.ShouldShow -> the seat stayed hidden. These pin the ADDITIVE wood decision table +
        // the belt→held desired index the sync now composes (stone/iron first, then the wood fallback).
        // ============================================================================================

        // The pure wood-tier selection -> family-index map (the additive wood sibling of SelectionIndexFor).
        [Test]
        public void WoodSelectionIndexFor_MapsEachWoodClass_ToItsWoodIndex()
        {
            Assert.AreEqual(HeldWeaponCycleDebug.AxeWoodFamilyIndex,
                HeldWeaponCycleDebug.WoodSelectionIndexFor(true, false, false, false, false), "wood axe");
            Assert.AreEqual(HeldWeaponCycleDebug.DaggerWoodFamilyIndex,
                HeldWeaponCycleDebug.WoodSelectionIndexFor(false, true, false, false, false), "wood dagger");
            Assert.AreEqual(HeldWeaponCycleDebug.SwordWoodFamilyIndex,
                HeldWeaponCycleDebug.WoodSelectionIndexFor(false, false, true, false, false), "wood sword");
            Assert.AreEqual(HeldWeaponCycleDebug.SpearWoodFamilyIndex,
                HeldWeaponCycleDebug.WoodSelectionIndexFor(false, false, false, true, false), "wood spear");
            Assert.AreEqual(HeldWeaponCycleDebug.PickaxeWoodFamilyIndex,
                HeldWeaponCycleDebug.WoodSelectionIndexFor(false, false, false, false, true), "wood pickaxe");
            Assert.AreEqual(-1, HeldWeaponCycleDebug.WoodSelectionIndexFor(false, false, false, false, false),
                "no wood weapon selected -> -1 (the stone/iron path or the gate hides the seat)");
        }

        // Each wood family index names its wood node (a reorder would render the WRONG wood weapon for the selection —
        // the crossed-visual class the spear/pickaxe pins guard, extended to the wood tier).
        [Test]
        public void FamilyContract_WoodIndicesNameTheWoodNodes()
        {
            Assert.AreEqual("wpn_axe_wood_01",     HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.AxeWoodFamilyIndex]);
            Assert.AreEqual("wpn_knife_wood_01",   HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.DaggerWoodFamilyIndex]);
            Assert.AreEqual("wpn_sword_wood_01",   HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.SwordWoodFamilyIndex]);
            Assert.AreEqual("wpn_spear_wood_01",   HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.SpearWoodFamilyIndex]);
            Assert.AreEqual("wpn_pickaxe_wood_01", HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.PickaxeWoodFamilyIndex]);
        }

        // The DESIRED index the sync composes: stone/iron table first, then the wood FALLBACK (production order).
        private static int DesiredIndexWithWood(Inventory inv)
        {
            int d = HeldWeaponCycleDebug.SelectionIndexFor(inv.IsAxeSelectedInBelt, inv.IsSpearSelectedInBelt,
                                                           inv.IsPickaxeStoneSelectedInBelt, inv.IsPickaxeIronSelectedInBelt);
            return d >= 0 ? d : HeldWeaponCycleDebug.WoodSelectionIndexFor(inv);
        }

        // THE soak-3 REGRESSION: acquire a WOOD axe, SELECT its belt slot — the sync must map it to the WOOD axe mesh
        // (the defect returned -1 -> EMPTY hands, so the Sponsor "couldn't test dagger/sword"). All 5 wood classes.
        [Test]
        public void WoodTierSelected_SelectionTable_MapsToTheWoodMesh_NotEmptyHands()
        {
            var cases = new (string id, int index, string label)[]
            {
                (ItemCatalog.AxeWoodId,     HeldWeaponCycleDebug.AxeWoodFamilyIndex,     "wood axe"),
                (ItemCatalog.DaggerWoodId,  HeldWeaponCycleDebug.DaggerWoodFamilyIndex,  "wood dagger"),
                (ItemCatalog.SwordWoodId,   HeldWeaponCycleDebug.SwordWoodFamilyIndex,   "wood sword"),
                (ItemCatalog.SpearWoodId,   HeldWeaponCycleDebug.SpearWoodFamilyIndex,   "wood spear"),
                (ItemCatalog.PickaxeWoodId, HeldWeaponCycleDebug.PickaxeWoodFamilyIndex, "wood pickaxe"),
            };
            foreach (var (id, index, label) in cases)
            {
                var inv = NewInventory(out var go);
                try
                {
                    var slot = inv.Model.AddToolToBelt(inv.Catalog.ById(id));
                    Assert.IsTrue(slot.HasValue, label + " acquired onto the belt (a belt-eligible Tool)");
                    inv.Model.SelectBelt(slot.Value.Index);

                    // The stone/iron table alone still returns -1 (proving the WOOD FALLBACK is what maps it — the
                    // soak-3 defect was exactly the missing fallback).
                    Assert.AreEqual(-1, DesiredIndex(inv),
                        label + ": the stone/iron SelectionIndexFor alone returns -1 (the pre-fix EMPTY-hands path)");
                    Assert.AreEqual(index, DesiredIndexWithWood(inv),
                        label + " selected -> its WOOD mesh index (soak-3: used to be -1 = nothing in hand)");

                    inv.Model.SelectBelt((slot.Value.Index + 1) % inv.BeltSlotCount); // deselect
                    Assert.AreEqual(-1, DesiredIndexWithWood(inv), label + " owned but NOT selected -> no held mesh");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
    }
}
