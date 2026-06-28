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

        // === HOLD-TO-CHOP (86caf7a0p) — the hold-repeat chain rides the SAME ShouldChopOnClick gate, so a HELD
        //     button is gated identically to a click: any single precondition failing means NO swing (no chop).
        //     This pins that the hold path did NOT introduce a parallel ungated swing trigger (the constraint:
        //     "do NOT add a parallel swing path"). The held-vs-click distinction is purely INPUT cadence — the
        //     decision of WHETHER a given swing chops is the one shared static gate, asserted above + here. ===
        [Test]
        public void HoldChain_RidesTheSameGate_NotAParallelUngatedPath()
        {
            // A held swing chops ONLY under the same all-true condition as a click (no separate hold rule).
            Assert.IsTrue(
                ChopTree.ShouldChopOnClick(inRange: true, axeSelected: true,
                                           uiPanelOpen: false, pointerOverUI: false, rmbHeld: false),
                "a held swing chops under the same all-preconditions-hold condition as a click");

            // A HELD camera-orbit drag (RMB held) must NOT be read as hold-to-chop — the obvious regression the
            // ticket calls out ("a HELD drag for camera-orbit must NOT be read as hold-to-chop").
            Assert.IsFalse(
                ChopTree.ShouldChopOnClick(true, true, uiPanelOpen: false, pointerOverUI: false, rmbHeld: true),
                "a HELD RMB camera-orbit drag must NOT chop, even with LMB held (mis-routed drag = the regression)");

            // Held over the belt UI / with a modal panel open — no chop (the chain re-evaluates the gate per swing).
            Assert.IsFalse(
                ChopTree.ShouldChopOnClick(true, true, uiPanelOpen: false, pointerOverUI: true, rmbHeld: false),
                "holding over the inventory/belt UI must NOT chop the tree behind it (per-swing gate)");
            Assert.IsFalse(
                ChopTree.ShouldChopOnClick(true, true, uiPanelOpen: true, pointerOverUI: false, rmbHeld: false),
                "holding while a modal panel is open must NOT chop (per-swing gate)");
        }

        // === HOLD-TO-CHOP — a bare ChopTree with NO input has NO active chain (the chain is input-driven, never
        //     auto-started; the seam defaults are off). Guards against a chain leaking on with no held button. ===
        [Test]
        public void BareChopTree_NoInput_HasNoActiveChain()
        {
            var go = new GameObject("ChopTreeChainProbe");
            try
            {
                var tree = go.AddComponent<ChopTree>();
                Assert.IsFalse(tree.IsChopChainActive,
                    "a freshly-added ChopTree with no held input must have NO active swing chain");
                // Setting held WITHOUT an Update tick still does not start a chain (the chain begins in Update,
                // gated on inventory/player/range — none wired here). The seam is inert until a gated Update.
                tree.SetChopHeld(true);
                Assert.IsFalse(tree.IsChopChainActive,
                    "SetChopHeld alone (no gated Update) does not start a chain — the chain is gated, not a latch");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void InventoryUI_ScreenPointOverlap_FlipsScreenToPanelAndHitTests()
        {
            // The pure core of InventoryUI.IsPointerOverUI: a SCREEN point (Y-up) is flipped to panel space
            // (Y-down by screenHeight) before hit-testing the UI rects. At the reference resolution the panel
            // scale is 1, so this asserts the flip in isolation. A belt-strip rect near the BOTTOM of the
            // screen sits near the BOTTOM in panel space (large panel-Y); the flip is what makes the hit land.
            const float screenH = 720f;
            const float scale1 = 1f;
            // A UI rect occupying the bottom strip in PANEL space (Y-down): panel-Y 660..710 ≈ screen-Y 10..60.
            var rects = new[] { new Rect(500f, 660f, 240f, 50f) };

            // A click at the bottom of the screen (screen-Y 30 -> panel-Y 690) lands inside the strip rect.
            Assert.IsTrue(
                InventoryUI.ScreenPointOverlapsAnyRect(new Vector2(600f, 30f), screenH, scale1, rects),
                "a bottom-of-screen click over the belt-strip rect is over UI (screen->panel flip + hit)");

            // A click in the MIDDLE of the screen (screen-Y 360 -> panel-Y 360) is the open world, not the strip.
            Assert.IsFalse(
                InventoryUI.ScreenPointOverlapsAnyRect(new Vector2(600f, 360f), screenH, scale1, rects),
                "a mid-screen world click is NOT over the bottom belt strip");
        }

        [Test]
        public void InventoryUI_ScreenPointOverlap_AppliesPanelScale_NotRawScreenPx()
        {
            // THE RECURRING-BUG REGRESSION GUARD (86caffw9h): the screen->panel conversion must divide by the
            // panel SCALE (PanelScaleMode.ScaleWithScreenSize), not just flip Y. At a non-1080p window the panel
            // scale != 1; a point's PANEL coordinate is screenPx/scale, NOT raw screenPx. The OLD core flipped Y
            // but never applied scale, so it hit-tested raw screen px against scaled panel rects -> the wrong
            // result at any non-reference resolution. This test FAILS against the pre-fix (scale-less) core and
            // is the gap that let the bug recur (the existing test above only exercised scale == 1, i.e. 1080p).
            const float screenH = 1440f;            // 2560x1440 window
            const float scale = 1440f / 1080f;      // ~1.333 — ScaleWithScreenSize at refRes-height 1080
            // A slot well in PANEL space (1080-tall panel coords): a pack cell at panel (300,200) size 80x80.
            var rects = new[] { new Rect(300f, 200f, 80f, 80f) };

            // The cell's panel center (340,240) maps to SCREEN px: x = 340*scale = ~453.3;
            // panelY 240 = (screenH - screenY)/scale  ->  screenY = screenH - 240*scale = 1440 - 320 = 1120.
            var screenOverCell = new Vector2(340f * scale, 1120f);
            Assert.IsTrue(
                InventoryUI.ScreenPointOverlapsAnyRect(screenOverCell, screenH, scale, rects),
                "a click whose PANEL-converted point is the cell center must hit (scale applied = screenPx/scale)");

            // Feeding the SAME screen point but pretending scale == 1 (the OLD behavior) must MISS the cell —
            // proof the conversion actually depends on the scale term (the missing-scale flaw the bug was).
            Assert.IsFalse(
                InventoryUI.ScreenPointOverlapsAnyRect(screenOverCell, screenH, 1f, rects),
                "with scale forced to 1 (the pre-fix path) the same screen point lands OUTSIDE the panel cell");

            // Symmetrically: the raw screen px treated as panel px (old bug) is far outside the small cell.
            Assert.IsFalse(
                InventoryUI.ScreenPointOverlapsAnyRect(new Vector2(340f, 240f), screenH, scale, rects),
                "panel-space coords fed as screen px do NOT hit once the scale conversion is in place");
        }
    }
}
