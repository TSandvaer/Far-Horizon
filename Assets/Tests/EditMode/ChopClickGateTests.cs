using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode truth-table guard for the LEFT-CLICK chop gate (ticket 86caa4c5c CHANGE 1 — the chop is now
    /// triggered by an active left-click, NOT proximity-auto; and a world-click must NOT chop when it lands on
    /// the inventory/belt UI, while a modal panel is open, or during a camera-orbit RMB drag). The decision is
    /// the pure static <see cref="ChopTree.ShouldChopOnClick"/>, so the WHOLE guard table is asserted headlessly
    /// here — the live click→chop loop is covered in PlayMode (ChopTreePlayModeTests).
    ///
    /// ShouldChopOnClick(inRange, axeSelected, uiPanelOpen, pointerOverUI, rmbHeld) must be:
    ///   true  ONLY when inRange && axeSelected && !uiPanelOpen && !pointerOverUI && !rmbHeld;
    ///   false if ANY single precondition is violated (the regression guard for each guard's BUG CLASS —
    ///   e.g. "a click on the belt UI chops the tree behind it" = the pointerOverUI=false branch breaking).
    /// </summary>
    public class ChopClickGateTests
    {
        [Test]
        public void Chops_OnlyWhenAllPreconditionsHold()
        {
            Assert.IsTrue(
                ChopTree.ShouldChopOnClick(inRange: true, axeSelected: true,
                                           uiPanelOpen: false, pointerOverUI: false, rmbHeld: false),
                "a left-click chops ONLY when in range + axe selected + no panel open + not over UI + RMB up");
        }

        [Test]
        public void NoChop_WhenOutOfRange()
        {
            Assert.IsFalse(
                ChopTree.ShouldChopOnClick(false, true, false, false, false),
                "out of range -> a click never chops (proximity is still required, only the trigger changed)");
        }

        [Test]
        public void NoChop_WhenAxeNotSelected()
        {
            Assert.IsFalse(
                ChopTree.ShouldChopOnClick(true, false, false, false, false),
                "axe not the SELECTED belt item -> a click never chops (the load-bearing gate, unchanged)");
        }

        [Test]
        public void NoChop_WhenAModalPanelIsOpen()
        {
            Assert.IsFalse(
                ChopTree.ShouldChopOnClick(true, true, uiPanelOpen: true, pointerOverUI: false, rmbHeld: false),
                "a click while a modal panel (inventory pack / settings) is open must NOT chop the world behind it");
        }

        [Test]
        public void NoChop_WhenPointerIsOverTheInventoryBeltUI()
        {
            // THE UI-CLICK-DOESN'T-CHOP guard (Sponsor: 'left-click must only chop in the game world'): a click
            // over the always-on belt strip / open pack selects/drags a slot, it does NOT swing the axe.
            Assert.IsFalse(
                ChopTree.ShouldChopOnClick(true, true, uiPanelOpen: false, pointerOverUI: true, rmbHeld: false),
                "a click OVER the inventory/belt UI must NOT chop the tree behind it (CHANGE 1 guard)");
        }

        [Test]
        public void NoChop_WhenRmbHeld_CameraOrbitDrag()
        {
            Assert.IsFalse(
                ChopTree.ShouldChopOnClick(true, true, uiPanelOpen: false, pointerOverUI: false, rmbHeld: true),
                "a click while the RIGHT mouse button is held (a camera-orbit drag) must NOT chop");
        }

        [Test]
        public void InventoryUI_ScreenPointOverlap_FlipsScreenToPanelAndHitTests()
        {
            // The pure core of InventoryUI.IsPointerOverUI: a SCREEN point (Y-up) is flipped to panel space
            // (Y-down by screenHeight) before hit-testing the UI rects. A belt-strip rect near the BOTTOM of
            // the screen sits near the BOTTOM in panel space (large panel-Y); the flip is what makes the hit
            // land. This guards the screen→panel convention the over-UI guard relies on.
            const float screenH = 720f;
            // A UI rect occupying the bottom strip in PANEL space (Y-down): panel-Y 660..710 ≈ screen-Y 10..60.
            var rects = new[] { new Rect(500f, 660f, 240f, 50f) };

            // A click at the bottom of the screen (screen-Y 30 -> panel-Y 690) lands inside the strip rect.
            Assert.IsTrue(
                InventoryUI.ScreenPointOverlapsAnyRect(new Vector2(600f, 30f), screenH, rects),
                "a bottom-of-screen click over the belt-strip rect is over UI (screen->panel flip + hit)");

            // A click in the MIDDLE of the screen (screen-Y 360 -> panel-Y 360) is the open world, not the strip.
            Assert.IsFalse(
                InventoryUI.ScreenPointOverlapsAnyRect(new Vector2(600f, 360f), screenH, rects),
                "a mid-screen world click is NOT over the bottom belt strip");
        }
    }
}
