using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode truth-table guard for the LEFT-CLICK CONSUME gate (ticket 86caf7a30 — left-click consumes the
    /// SELECTED belt item when it is a CONSUMABLE; a world-click must NOT consume when over the inventory/belt
    /// UI, while a modal panel is open, or during a camera-orbit RMB drag). The decision is the pure static
    /// <see cref="LeftClickConsume.ShouldConsumeOnClick"/>, so the WHOLE guard table is asserted headlessly here
    /// — the live select→click→consume loop is covered in PlayMode (LeftClickConsumePlayModeTests).
    ///
    /// This MIRRORS ChopClickGateTests VERBATIM in intent: the consume gate is the disjoint sibling of the chop
    /// gate (chop fires on the AXE selected; consume fires on a CONSUMABLE selected) — proving the two share the
    /// same world-click guard family (panel/over-UI/RMB) and never double-fire on the shared left-click edge.
    ///
    /// ShouldConsumeOnClick(selected, uiPanelOpen, pointerOverUI, rmbHeld) must be:
    ///   true  ONLY when selected is a non-empty Consumable && !uiPanelOpen && !pointerOverUI && !rmbHeld;
    ///   false if ANY single precondition is violated (the regression guard for each guard's BUG CLASS).
    /// </summary>
    public class LeftClickConsumeGateTests
    {
        // Build the canonical defs in code (no .asset round-trip) so the gate's selected-stack cases are real.
        private static ItemDef Def(string id, ItemKind kind)
        {
            var d = ScriptableObject.CreateInstance<ItemDef>();
            d.Init(id, id, kind);
            return d;
        }

        private static ItemStack Consumable() => new ItemStack(Def(ItemCatalog.BerryId, ItemKind.Consumable), 1);
        private static ItemStack Water() => new ItemStack(Def(ItemCatalog.WaterId, ItemKind.Consumable), 1);
        private static ItemStack Axe() => new ItemStack(Def(ItemCatalog.AxeId, ItemKind.Tool), 1);
        private static ItemStack Wood() => new ItemStack(Def(ItemCatalog.WoodId, ItemKind.Resource), 1);

        [Test]
        public void Consumes_OnlyWhenAllPreconditionsHold_Berry()
        {
            Assert.IsTrue(
                LeftClickConsume.ShouldConsumeOnClick(Consumable(),
                    uiPanelOpen: false, pointerOverUI: false, rmbHeld: false),
                "a left-click consumes ONLY when a consumable is selected + no panel open + not over UI + RMB up");
        }

        [Test]
        public void Consumes_WhenWaterSelected()
        {
            Assert.IsTrue(
                LeftClickConsume.ShouldConsumeOnClick(Water(), false, false, false),
                "water (a Consumable) selected -> a left-click consumes it (the drink branch fires)");
        }

        [Test]
        public void NoConsume_WhenAxeSelected_TheChopBranchOwnsIt()
        {
            // The axe is a TOOL, not a Consumable — consume must NOT fire on it (ChopTree's DISJOINT branch
            // handles the axe→chop). This is the load-bearing "left-click with the axe still chops, no consume
            // regression" guard at the gate level: the consume gate refuses the axe, so the two never collide.
            Assert.IsFalse(
                LeftClickConsume.ShouldConsumeOnClick(Axe(), false, false, false),
                "the axe is a Tool, not a Consumable -> a left-click never CONSUMES it (chop owns the axe)");
        }

        [Test]
        public void NoConsume_WhenAResourceSelected()
        {
            // A pure Resource (wood) is not belt-eligible/consumable — even if it somehow sat selected, no consume.
            Assert.IsFalse(
                LeftClickConsume.ShouldConsumeOnClick(Wood(), false, false, false),
                "a Resource is not a Consumable -> a left-click never consumes it");
        }

        [Test]
        public void NoConsume_WhenSelectionEmpty()
        {
            Assert.IsFalse(
                LeftClickConsume.ShouldConsumeOnClick(ItemStack.Empty, false, false, false),
                "an EMPTY selected belt slot -> a left-click does nothing (no consume, no error)");
        }

        [Test]
        public void NoConsume_WhenAModalPanelIsOpen()
        {
            Assert.IsFalse(
                LeftClickConsume.ShouldConsumeOnClick(Consumable(),
                    uiPanelOpen: true, pointerOverUI: false, rmbHeld: false),
                "a click while a modal panel (inventory pack / settings) is open must NOT consume");
        }

        [Test]
        public void NoConsume_WhenPointerIsOverTheInventoryBeltUI()
        {
            // Mirrors the chop guard: a click over the always-on belt strip / open pack selects/drags a slot,
            // it does NOT consume the item behind it.
            Assert.IsFalse(
                LeftClickConsume.ShouldConsumeOnClick(Consumable(),
                    uiPanelOpen: false, pointerOverUI: true, rmbHeld: false),
                "a click OVER the inventory/belt UI must NOT consume the selected item (shared world-click guard)");
        }

        [Test]
        public void NoConsume_WhenRmbHeld_CameraOrbitDrag()
        {
            Assert.IsFalse(
                LeftClickConsume.ShouldConsumeOnClick(Consumable(),
                    uiPanelOpen: false, pointerOverUI: false, rmbHeld: true),
                "a click while the RIGHT mouse button is held (a camera-orbit drag) must NOT consume");
        }
    }
}
