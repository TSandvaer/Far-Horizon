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

        // ============================================================================================
        // 86caxjx26 — STONE BLADES: `dagger_stone` + `sword_stone` selected in the belt showed NOTHING in the
        // hand. The LAST two ids in the roster with no held-visual map row, and the fourth occurrence of the
        // class (pickaxe I-2 -> wood soak-3 -> iron blades #351 -> these). They were last because indices 1
        // and 2 were the only slots in the 0-14 range with no NAMED family constant to map them to.
        // ============================================================================================

        // The pure stone-blade selection -> family-index map (the additive sibling of Wood/IronSelectionIndexFor).
        [Test]
        public void StoneBladeSelectionIndexFor_MapsEachStoneBlade_ToItsStoneIndex()
        {
            Assert.AreEqual(HeldWeaponCycleDebug.DaggerStoneFamilyIndex,
                HeldWeaponCycleDebug.StoneBladeSelectionIndexFor(true, false), "stone dagger");
            Assert.AreEqual(HeldWeaponCycleDebug.SwordStoneFamilyIndex,
                HeldWeaponCycleDebug.StoneBladeSelectionIndexFor(false, true), "stone sword");
            Assert.AreEqual(-1, HeldWeaponCycleDebug.StoneBladeSelectionIndexFor(false, false),
                "no stone blade selected -> -1 (the other tiers or the gate hide the seat)");
            // Only one belt slot is selected in play; pin the deterministic tie-break anyway.
            Assert.AreEqual(HeldWeaponCycleDebug.DaggerStoneFamilyIndex,
                HeldWeaponCycleDebug.StoneBladeSelectionIndexFor(true, true), "stone dagger wins the tie-break");
        }

        // Each stone-blade family index names its stone node (a reorder would render the WRONG weapon for the
        // selection — the crossed-visual class the spear/pickaxe/wood/iron pins already guard).
        [Test]
        public void FamilyContract_StoneBladeIndicesNameTheStoneNodes()
        {
            Assert.AreEqual("wpn_knife_stone_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.DaggerStoneFamilyIndex],
                "DaggerStoneFamilyIndex MUST name the STONE knife node (index 1)");
            Assert.AreEqual("wpn_sword_stone_01",
                HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.SwordStoneFamilyIndex],
                "SwordStoneFamilyIndex MUST name the STONE sword node (index 2)");
        }

        // THE DEFECT, restated as an executable proof (the wood/iron tests' shape). For each stone BLADE the
        // three PRE-EXISTING tables all return -1 — that triple IS the pre-fix empty-hands path — and only the
        // new stone-blade fallback maps it, reached through the SHARED predicate the gate + finger-curl read.
        [Test]
        public void StoneBladeSelected_UsedToBeEmptyHands_NowMapsToItsStoneMesh()
        {
            var cases = new (string id, int index, string label)[]
            {
                (ItemCatalog.DaggerStoneId, HeldWeaponCycleDebug.DaggerStoneFamilyIndex, "stone dagger"),
                (ItemCatalog.SwordStoneId,  HeldWeaponCycleDebug.SwordStoneFamilyIndex,  "stone sword"),
            };
            foreach (var (id, index, label) in cases)
            {
                var inv = NewInventory(out var go);
                try
                {
                    var slot = inv.Model.AddToolToBelt(inv.Catalog.ById(id));
                    Assert.IsTrue(slot.HasValue, label + " acquired onto the belt (a belt-eligible Tool)");
                    inv.Model.SelectBelt(slot.Value.Index);

                    Assert.AreEqual(-1, DesiredIndex(inv),
                        label + ": the stone-axe/spear/pickaxe SelectionIndexFor returns -1 (1/3 of the pre-fix path)");
                    Assert.AreEqual(-1, HeldWeaponCycleDebug.WoodSelectionIndexFor(inv),
                        label + ": the WOOD table returns -1 (2/3 of the pre-fix path)");
                    Assert.AreEqual(-1, HeldWeaponCycleDebug.IronSelectionIndexFor(inv),
                        label + ": the IRON table returns -1 — these three together ARE the EMPTY-HANDS defect");

                    Assert.AreEqual(index, HeldWeaponCycleDebug.StoneBladeSelectionIndexFor(inv),
                        label + " selected -> its STONE mesh index (this used to be nothing in hand)");
                    Assert.AreEqual(index, HeldWeaponCycleDebug.HeldVisualIndexFor(inv),
                        label + ": the COMPOSED index the mesh sync applies resolves to the same stone mesh");
                    Assert.IsTrue(HeldWeaponCycleDebug.IsHeldVisualWeaponSelected(inv),
                        label + ": the SHARED held-visual predicate is now TRUE, so HeldAxe.ShouldShow shows the " +
                        "seat and CastawayFingerCurl closes the grip (they read the same predicate)");

                    inv.Model.SelectBelt((slot.Value.Index + 1) % inv.BeltSlotCount); // deselect
                    Assert.IsFalse(HeldWeaponCycleDebug.IsHeldVisualWeaponSelected(inv),
                        label + " owned but NOT selected -> the seat stays hidden (ownership is not selection)");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        // The stone-blade map is ADDITIVE: it must not change what the three already-soaked tiers resolve to.
        // It is composed LAST for exactly this reason, so it can only fill cases that used to be -1.
        [Test]
        public void StoneBladeMap_DoesNotDisturbTheSoakedStoneWoodIronDecisions()
        {
            Assert.AreEqual(HeldWeaponCycleDebug.AxeFamilyIndex,
                HeldWeaponCycleDebug.SelectionIndexFor(true, false, false, false), "stone axe unchanged");
            Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex,
                HeldWeaponCycleDebug.SelectionIndexFor(false, true, false, false), "stone spear unchanged");
            Assert.AreEqual(HeldWeaponCycleDebug.PickaxeStoneFamilyIndex,
                HeldWeaponCycleDebug.SelectionIndexFor(false, false, true, false), "stone pickaxe unchanged");
            Assert.AreEqual(HeldWeaponCycleDebug.PickaxeIronFamilyIndex,
                HeldWeaponCycleDebug.SelectionIndexFor(false, false, false, true), "iron pickaxe unchanged");
            Assert.AreEqual(HeldWeaponCycleDebug.SwordWoodFamilyIndex,
                HeldWeaponCycleDebug.WoodSelectionIndexFor(false, false, true, false, false), "wood sword unchanged");
            Assert.AreEqual(HeldWeaponCycleDebug.SwordIronFamilyIndex,
                HeldWeaponCycleDebug.IronSelectionIndexFor(false, false, true, false), "iron sword unchanged");
        }

        // The extraction of the fallback chain into HeldVisualIndexFor must be BEHAVIOUR-PRESERVING: the predicate
        // and the mesh sync used to compose the chain independently, by hand. Pin that the composed index agrees
        // with the predicate for EVERY tier — the drift between those two copies is what the extraction removes.
        [Test]
        public void HeldVisualIndexFor_AgreesWithThePredicate_AcrossEveryTier()
        {
            var ids = new[]
            {
                ItemCatalog.AxeId, ItemCatalog.SpearId, ItemCatalog.PickaxeStoneId, ItemCatalog.PickaxeIronId,
                ItemCatalog.AxeWoodId, ItemCatalog.DaggerWoodId, ItemCatalog.SwordWoodId, ItemCatalog.SpearWoodId,
                ItemCatalog.PickaxeWoodId, ItemCatalog.AxeIronId, ItemCatalog.DaggerIronId, ItemCatalog.SwordIronId,
                ItemCatalog.SpearIronId, ItemCatalog.DaggerStoneId, ItemCatalog.SwordStoneId,
            };
            foreach (string id in ids)
            {
                var inv = NewInventory(out var go);
                try
                {
                    var slot = inv.Model.AddToolToBelt(inv.Catalog.ById(id));
                    Assert.IsTrue(slot.HasValue, id + " acquired onto the belt");
                    inv.Model.SelectBelt(slot.Value.Index);
                    Assert.GreaterOrEqual(HeldWeaponCycleDebug.HeldVisualIndexFor(inv), 0,
                        id + ": the composed index resolves a mesh");
                    Assert.IsTrue(HeldWeaponCycleDebug.IsHeldVisualWeaponSelected(inv),
                        id + ": the predicate agrees with the composed index (they are now ONE composition — a " +
                        "disagreement means a tier visible to the mesh sync but not to the finger-curl, or vice versa)");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
    }
}
