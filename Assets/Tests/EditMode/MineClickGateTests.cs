using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode truth-table guard for the LEFT-CLICK MINE gate (ticket 86cakkmr0 / I-2 — mining is an active
    /// left-click WITH A PICKAXE SELECTED, NOT proximity-auto; and a world-click must NOT mine when it lands on the
    /// inventory/belt UI, while a modal panel is open, or during a camera-orbit RMB drag). The decision is the pure
    /// static <see cref="MineOre.ShouldMineOnClick"/>, so the WHOLE guard table is asserted headlessly here — the
    /// live click→mine loop is covered in PlayMode (MineOrePlayModeTests). Sibling of ChopClickGateTests.
    ///
    /// ShouldMineOnClick(inRange, pickaxeSelected, uiPanelOpen, pointerOverUI, rmbHeld) must be:
    ///   true  ONLY when inRange && pickaxeSelected && !uiPanelOpen && !pointerOverUI && !rmbHeld;
    ///   false if ANY single precondition is violated — the regression guard for each guard's BUG CLASS.
    /// </summary>
    public class MineClickGateTests
    {
        [Test]
        public void Mines_OnlyWhenAllPreconditionsHold()
        {
            Assert.IsTrue(
                MineOre.ShouldMineOnClick(inRange: true, pickaxeSelected: true,
                                          uiPanelOpen: false, pointerOverUI: false, rmbHeld: false),
                "a left-click mines ONLY when in range + a pickaxe selected + no panel open + not over UI + RMB up");
        }

        [Test]
        public void NoMine_WhenOutOfRange()
        {
            Assert.IsFalse(
                MineOre.ShouldMineOnClick(false, true, false, false, false),
                "out of range -> a click never mines (proximity is still required)");
        }

        [Test]
        public void NoMine_WhenPickaxeNotSelected()
        {
            // THE LOAD-BEARING SELECTION GATE — a pickaxe not the SELECTED belt item (even if owned) never mines.
            Assert.IsFalse(
                MineOre.ShouldMineOnClick(true, false, false, false, false),
                "pickaxe not the SELECTED belt item -> a click never mines (the load-bearing gate)");
        }

        [Test]
        public void NoMine_WhenAModalPanelIsOpen()
        {
            Assert.IsFalse(
                MineOre.ShouldMineOnClick(true, true, uiPanelOpen: true, pointerOverUI: false, rmbHeld: false),
                "a click while a modal panel (inventory pack / settings) is open must NOT mine the world behind it");
        }

        [Test]
        public void NoMine_WhenPointerIsOverTheInventoryBeltUI()
        {
            // THE UI-CLICK-DOESN'T-MINE guard: a click over the always-on belt strip / open pack selects/drags a
            // slot, it does NOT swing the pickaxe (the Sponsor's "left-click must only act in the game world").
            Assert.IsFalse(
                MineOre.ShouldMineOnClick(true, true, uiPanelOpen: false, pointerOverUI: true, rmbHeld: false),
                "a click OVER the inventory/belt UI must NOT mine the node behind it");
        }

        [Test]
        public void NoMine_WhenRmbHeld_CameraOrbitDrag()
        {
            Assert.IsFalse(
                MineOre.ShouldMineOnClick(true, true, uiPanelOpen: false, pointerOverUI: false, rmbHeld: true),
                "a click while the RIGHT mouse button is held (a camera-orbit drag) must NOT mine");
        }

        // === HOLD-TO-MINE rides the SAME gate — a HELD button is gated identically to a click (no parallel path) ===
        [Test]
        public void HoldChain_RidesTheSameGate_NotAParallelUngatedPath()
        {
            Assert.IsTrue(
                MineOre.ShouldMineOnClick(true, true, false, false, false),
                "a held swing mines under the same all-preconditions-hold condition as a click");
            Assert.IsFalse(
                MineOre.ShouldMineOnClick(true, true, false, false, rmbHeld: true),
                "a HELD RMB camera-orbit drag must NOT mine, even with LMB held (mis-routed drag = the regression)");
            Assert.IsFalse(
                MineOre.ShouldMineOnClick(true, true, false, pointerOverUI: true, rmbHeld: false),
                "holding over the inventory/belt UI must NOT mine the node behind it (per-swing gate)");
        }

        // === A bare MineOre with NO input has NO active chain (the chain is input-driven, never auto-started) ===
        [Test]
        public void BareMineOre_NoInput_HasNoActiveChain()
        {
            var go = new GameObject("MineOreChainProbe");
            try
            {
                var mine = go.AddComponent<MineOre>();
                Assert.IsFalse(mine.IsMineChainActive,
                    "a freshly-added MineOre with no held input must have NO active swing chain");
                mine.SetMineHeld(true);
                Assert.IsFalse(mine.IsMineChainActive,
                    "SetMineHeld alone (no gated Update) does not start a chain — the chain is gated, not a latch");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // === IsPickaxeSelected — the pickaxe-selected gate resolves BOTH tiers against the selected belt slot ===
        [Test]
        public void IsPickaxeSelected_TrueForEitherTier_WhenSelected_FalseOtherwise()
        {
            var invGo = new GameObject("Inventory");
            try
            {
                var inv = invGo.AddComponent<Inventory>();
                // A null inventory / empty belt is not "pickaxe selected".
                Assert.IsFalse(MineOre.IsPickaxeSelected(null), "null inventory -> no pickaxe selected");
                Assert.IsFalse(MineOre.IsPickaxeSelected(inv), "empty belt -> no pickaxe selected");

                // Add the STONE pickaxe to the belt + select its slot -> selected.
                var cat = inv.Catalog;
                var stone = cat.ById(ItemCatalog.PickaxeStoneId);
                var slot = inv.Model.AddToolToBelt(stone);
                Assert.IsTrue(slot.HasValue, "the stone pickaxe is belt-eligible (a Tool) and lands on the belt");
                inv.Model.SelectBelt(slot.Value.Index);
                Assert.IsTrue(MineOre.IsPickaxeSelected(inv),
                    "with the STONE pickaxe the selected belt item, IsPickaxeSelected is true");

                // Select a different (empty) slot -> not selected.
                inv.Model.SelectBelt((slot.Value.Index + 1) % inv.BeltSlotCount);
                Assert.IsFalse(MineOre.IsPickaxeSelected(inv),
                    "owned but NOT the selected belt item -> IsPickaxeSelected is false (selection gate, not ownership)");
            }
            finally
            {
                Object.DestroyImmediate(invGo);
            }
        }
    }
}
