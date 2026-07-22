using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;

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

        // === ROUND-4 REGRESSION GUARD (86caffwv5 soak-4 "I cannot chop a tree" — the WOOD axe) ===
        // The chop verb gates on IsAnyAxeSelectedInBelt (round-4), NOT the stone-only IsAxeSelectedInBelt. Round-3
        // added the wood axe to the belt + held-visual but the chop gate still read the stone-only predicate, so
        // selecting a WOOD axe failed the chop gate → no chop (while MeleeAttack's whiff still played). These pin
        // that EVERY axe tier (wood/stone/iron) satisfies the chop's select gate, and that the stone-only predicate
        // is UNCHANGED (the held-visual callers depend on its stone-only semantics).
        [Test]
        public void IsAnyAxeSelectedInBelt_TrueForEveryAxeTier_WoodStoneIron()
        {
            // WOOD axe selected — THE Sponsor's soak-4 repro (he tested the wood tier first).
            SelectFreshTool(ItemCatalog.AxeWoodId);
            Assert.IsTrue(_inv.IsAnyAxeSelectedInBelt, "a selected WOOD axe satisfies the chop gate (the soak-4 fix)");
            Assert.IsFalse(_inv.IsAxeSelectedInBelt, "...but the STONE-only predicate stays false for the wood axe (unchanged)");

            // STONE axe (the shipped "axe" id).
            SelectFreshTool(ItemCatalog.AxeId);
            Assert.IsTrue(_inv.IsAnyAxeSelectedInBelt, "a selected STONE axe satisfies the chop gate");
            Assert.IsTrue(_inv.IsAxeSelectedInBelt, "the stone axe still satisfies the stone-only predicate too");

            // IRON axe.
            SelectFreshTool(ItemCatalog.AxeIronId);
            Assert.IsTrue(_inv.IsAnyAxeSelectedInBelt, "a selected IRON axe satisfies the chop gate");
            Assert.IsFalse(_inv.IsAxeSelectedInBelt, "the stone-only predicate stays false for the iron axe");
        }

        [Test]
        public void IsAnyAxeSelectedInBelt_FalseForNonAxeSelections()
        {
            SelectFreshTool(ItemCatalog.SpearId);
            Assert.IsFalse(_inv.IsAnyAxeSelectedInBelt, "a selected spear is not an axe → the chop gate stays closed");
            SelectFreshTool(ItemCatalog.PickaxeWoodId);
            Assert.IsFalse(_inv.IsAnyAxeSelectedInBelt, "a selected pickaxe is not an axe → the chop gate stays closed");
        }

        // === 86cav8xu8 — the fall-through-by-CONSTRUCTION guard (kills the hand-enumerated tier list) ===
        // IsAnyAxeSelectedInBelt is now WeaponClass-DERIVED (WeaponCatalog.WeaponClassForItemId == WeaponClassAxe),
        // NOT a hardcoded {axe, axe_wood, axe_iron} list. This asserts the EQUIVALENCE across the WHOLE weapon
        // catalog: for EVERY belt-selectable weapon, the gate is true IFF that item's derived WeaponClass is the axe
        // class. A future 4th axe tier added to BuildDefaults is covered HERE automatically (this iterates the
        // catalog) AND by the production gate (same derivation) — closing the soak-4 fall-through by construction.
        // A regression to hand-enumeration that omits a tier (or a derivation drift) breaks this equivalence.
        [Test]
        public void IsAnyAxeSelectedInBelt_IsWeaponClassDerived_AcrossTheWholeCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
            catalog.BuildDefaults();
            try
            {
                int axeTiersSeen = 0, nonAxeSeen = 0;
                foreach (var def in catalog.All)
                {
                    if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                    bool gate = SelectFreshBeltWeaponMatchesAxeGate(def.Id, out bool selectable);
                    if (!selectable) continue; // weapon id with no inventory ItemDef — not belt-selectable, skip
                    bool expectAxe = WeaponCatalog.WeaponClassForItemId(def.Id) == CastawayCharacter.WeaponClassAxe;
                    Assert.AreEqual(expectAxe, gate,
                        "IsAnyAxeSelectedInBelt for '" + def.Id + "' must equal (derived WeaponClass == axe class)");
                    if (expectAxe) axeTiersSeen++; else nonAxeSeen++;
                }
                Assert.GreaterOrEqual(axeTiersSeen, 3, "every current axe tier (wood/stone/iron) must be exercised");
                Assert.Greater(nonAxeSeen, 0, "and at least one non-axe weapon must be exercised (the false case)");
            }
            finally
            {
                foreach (var d in catalog.All) if (d != null) Object.DestroyImmediate(d);
                Object.DestroyImmediate(catalog);
            }
        }

        // Rig a FRESH inventory (so belt slot 0 is free — the 5-slot belt can't hold the whole 15-weapon catalog),
        // place + select the weapon id, and return IsAnyAxeSelectedInBelt. `selectable` is false when the weapon id
        // has no inventory ItemDef (not belt-eligible → the derivation guard skips it).
        private bool SelectFreshBeltWeaponMatchesAxeGate(string weaponId, out bool selectable)
        {
            var go = new GameObject("InvDerive");
            try
            {
                var inv = go.AddComponent<Inventory>();
                var item = inv.Catalog.ById(weaponId);
                if (item == null) { selectable = false; return false; }
                var placed = inv.Model.AddToolToBelt(item);
                if (!placed.HasValue) { selectable = false; return false; } // not a belt-eligible Tool
                selectable = true;
                inv.Model.SelectBelt(placed.Value.Index);
                return inv.IsAnyAxeSelectedInBelt;
            }
            finally { Object.DestroyImmediate(go); }
        }

        // Place a tool on the belt AND select its slot, so IsAnyAxeSelectedInBelt reads the intended selection
        // (the belt auto-places into the first free slot; SelectBelt(index) picks it). A fresh Inventory per
        // [SetUp] means slot 0 is the first free slot for the FIRST call; later calls place into the next slot.
        private void SelectFreshTool(string id)
        {
            var def = _inv.Catalog.ById(id);
            Assert.IsNotNull(def, "tool id '" + id + "' is in the catalog");
            var placed = _inv.Model.AddToolToBelt(def);
            Assert.IsTrue(placed.HasValue, "tool '" + id + "' placed on the belt");
            _inv.Model.SelectBelt(placed.Value.Index);
            Assert.IsTrue(_inv.Model.IsSelectedBeltItem(id), "tool '" + id + "' is now the selected belt item");
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
