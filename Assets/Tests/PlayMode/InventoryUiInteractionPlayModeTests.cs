using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode regression guard for the THREE #90 soak-caught UI-INTERACTION bugs (ticket 86caa4bya /
    /// follow-up 86cabfa21). These ride a LIVE UI Toolkit panel (UIDocument + PanelSettings) laid out over
    /// real frames — the layer the existing InventoryModel/Held-axe suites never touched, which is exactly
    /// why CI (had the suite run) wouldn't have caught the bugs: the model was always correct; the VIEW
    /// interaction was broken. Each test asserts the END-TO-END effect through the real InventoryUI, not a
    /// model call.
    ///
    ///   • BUG 1 — drag/move between slots: a drop RESOLVED BY CURSOR POSITION (not the captured-pointer
    ///     event target) actually moves the item. The old code read the PointerUp event's target, which the
    ///     pointer-capture redirect pins to the SOURCE slot → TryMove(from,from) = same-slot no-op.
    ///   • BUG 2 — clicking the DOCKED belt row (inside the pack) must NOT change the active selection;
    ///     clicking the bottom STRIP (the hotbar) still selects.
    ///   • BUG 3 — obtained wood shows in the grid as a recognizable ICON (not a bare "W" letter-chip).
    ///
    /// HARDENING (ticket 86cabugc3 — the #102 drag-source-dim NITs follow-up) adds the live-panel half the
    /// original suite left to a class-name string assert:
    ///   • DraggingASlot_ResolvesSourceContentToHidden_NotJustTheClass (NIT 1) — resolvedStyle.visibility
    ///     == Hidden on the dragged source icon/chip/badge (the USS rule's actual rendered effect).
    ///   • DraggingASlot_PreservesSourceWorldBound_LayoutNotCollapsed (NIT 2) — the dimmed source keeps its
    ///     worldBound (visibility:hidden, NOT display:none — the BUG 1 drop hit-tests that rect).
    ///   • BeginDragOnEmptySlot_DoesNothing_HitsIsEmptyGuard (NIT 3) — the regression-critical IsEmpty seam.
    ///   • RefreshAllMidDrag_PreservesTheSourceDimClass (NIT 4) — a mid-drag Inventory.Changed repaint must
    ///     not clear slot--dragging-source.
    /// </summary>
    public class InventoryUiInteractionPlayModeTests
    {
        private GameObject _invGo;
        private GameObject _uiGo;
        private Inventory _inv;
        private InventoryUI _ui;
        private PanelSettings _panel;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _uiGo = new GameObject("InventoryUI");
            var doc = _uiGo.AddComponent<UIDocument>();
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            // Constant-pixel scale at a known size so the slots lay out at their authored 64px and worldBound
            // is non-degenerate for the position-resolved drop (BUG 1) test.
            _panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panel.referenceResolution = new Vector2Int(1280, 720);
            doc.panelSettings = _panel;

            _ui = _uiGo.AddComponent<InventoryUI>();
            _ui.document = doc;
            _ui.inventory = _inv;
            // Wire the REAL shipped shell UXML + USS (the named grid/belt containers + the 64px slot sizing)
            // so this test exercises the exact tree the build ships — not a hand-rolled stand-in.
            _ui.panelUxml = LoadShellUxml();
            _ui.paletteUss = LoadUss("Assets/UI/InventoryPalette.uss");
            _ui.panelUss = LoadUss("Assets/UI/InventoryPanel.uss");
            Assert.IsNotNull(_ui.panelUxml, "the shipped InventoryPanel.uxml must load for the UI test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uiGo);
            Object.Destroy(_invGo);
            if (_panel != null)
            {
                if (_panel.themeStyleSheet != null) Object.Destroy(_panel.themeStyleSheet);
                Object.Destroy(_panel);
            }
        }

        // BUG 3 — wood obtained → a grid cell shows the wood ICON (not a "W" chip; not empty).
        [UnityTest]
        public IEnumerator ObtainedWood_ShowsAsIconInTheGrid_NotALetterChip()
        {
            yield return WaitFrames(4);
            _inv.AddWood(3);
            _ui.SetOpen(true);
            yield return WaitFrames(6);

            // Model truth.
            Assert.AreEqual(3, _inv.WoodCount, "the model holds the chopped wood");
            Assert.AreEqual("wood", _inv.Model.InventorySlots[0].Def.Id, "wood is in inventory slot 0");

            // The wood ItemDef carries a baked icon (BUG 3 fix — null icon was the cause).
            var woodDef = _inv.Catalog.ById(ItemCatalog.WoodId);
            Assert.IsNotNull(woodDef.Icon,
                "the wood ItemDef must carry a recognizable icon (AC5) — a null icon renders only a bare " +
                "'W' letter-chip, which is the #90 BUG 3 the Sponsor reported ('wood does not appear')");

            // Painted truth: grid cell 0 shows the icon sprite, NOT a chip letter.
            var grid = Grid();
            Assert.IsNotNull(grid, "the inv-grid container must exist");
            var cell0 = First(grid);
            var icon = cell0.Q<VisualElement>("icon");
            var chip = cell0.Q<Label>("chip");
            Assert.IsNotNull(icon.style.backgroundImage.value.sprite,
                "grid cell 0 must paint the wood icon sprite (BUG 3 — wood now reads as a wood item)");
            Assert.IsTrue(string.IsNullOrEmpty(chip.text),
                "the letter-chip must be cleared once an icon is shown");
        }

        // BUG 1 — drag wood from inventory slot 0 → slot 5 actually moves it (cursor-resolved drop).
        [UnityTest]
        public IEnumerator DragWoodBetweenInventorySlots_MovesIt()
        {
            yield return WaitFrames(4);
            _inv.AddWood(3);
            _ui.SetOpen(true);
            yield return WaitFrames(8); // let the grid lay out so worldBound is real

            var grid = Grid();
            var cells = Children(grid);
            Assert.GreaterOrEqual(cells.Count, 6, "grid must have at least 6 cells");

            // Target cell 5's center in panel space.
            Vector2 targetCenter = cells[5].worldBound.center;
            Assert.That(targetCenter.x, Is.Not.EqualTo(0f).Within(0.0001f).Or.Not.EqualTo(0f),
                "the target cell must be laid out (non-degenerate worldBound) for a position-resolved drop");

            // Drive the real drop seam by POSITION (BUG 1 — not the source event target).
            bool moved = _ui.ApplyDrop(SlotRef.Inventory(0), targetCenter);
            yield return null;

            Assert.IsTrue(moved, "a position-resolved drop onto a different inventory slot moves the item");
            Assert.IsTrue(_inv.Model.InventorySlots[0].IsEmpty, "source slot is now empty");
            Assert.AreEqual("wood", _inv.Model.InventorySlots[5].Def.Id, "wood landed in the dropped-on slot");
            Assert.AreEqual(3, _inv.Model.InventorySlots[5].Count, "the whole stack moved (count conserved)");
        }

        // BUG 1 (axe) — drag the axe from belt slot 1 → belt slot 3 actually moves it.
        [UnityTest]
        public IEnumerator DragAxeBetweenBeltSlots_MovesIt()
        {
            yield return WaitFrames(4);
            _inv.PickUpAxe(); // belt slot 0
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            var dock = DockBelt();
            var cells = Children(dock);
            Assert.GreaterOrEqual(cells.Count, 4, "docked belt must have its slots");
            Vector2 target = cells[3].worldBound.center;

            bool moved = _ui.ApplyDrop(SlotRef.Belt(0), target);
            yield return null;

            Assert.IsTrue(moved, "the axe moves to a different belt slot via a position-resolved drop");
            Assert.IsTrue(_inv.Model.BeltSlots[0].IsEmpty, "belt slot 1 is now empty");
            Assert.AreEqual("axe", _inv.Model.BeltSlots[3].Def.Id, "the axe landed in belt slot 4");
        }

        // BUG 2 — clicking the DOCKED belt row inside the pack does NOT change the active selection.
        [UnityTest]
        public IEnumerator ClickingDockedBeltRow_DoesNotChangeSelection()
        {
            yield return WaitFrames(4);
            _inv.PickUpAxe();
            _inv.Model.SelectBelt(0);
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            int before = _inv.Model.SelectedBeltIndex;
            Assert.AreEqual(0, before, "selection starts at slot 1");

            // Simulate a click on the DOCKED belt row's slot index 2 (the organize row, not the hotbar).
            var dock = DockBelt();
            var cells = Children(dock);
            SendClick(cells[2]);
            yield return null;

            Assert.AreEqual(before, _inv.Model.SelectedBeltIndex,
                "clicking the docked belt row (inside the pack) must NOT change the active selection " +
                "(#90 BUG 2 — the pack belt row is an organize target, not a selector; selection moves only " +
                "via 1–N / scroll)");
        }

        // BUG 2 positive control — clicking the bottom STRIP (the hotbar) DOES select.
        [UnityTest]
        public IEnumerator ClickingBottomBeltStrip_DoesSelect()
        {
            yield return WaitFrames(4);
            _inv.PickUpAxe();
            _inv.Model.SelectBelt(0);
            yield return WaitFrames(8);

            var strip = StripBelt();
            var cells = Children(strip);
            Assert.GreaterOrEqual(cells.Count, 3, "the bottom strip mirrors the belt slots");
            SendClick(cells[2]);
            yield return null;

            Assert.AreEqual(2, _inv.Model.SelectedBeltIndex,
                "clicking the bottom hotbar strip DOES select that slot (the strip is the selector)");
        }

        // #90 DUP-FIX — while a drag is active, the SOURCE slot's item content is hidden (dimmed) so the
        // item reads in ONE place (the #drag-ghost), not two (source + ghost = the "duplicate" the Sponsor
        // saw holding the mouse down on the berries). Restored on a LANDED drop.
        [UnityTest]
        public IEnumerator DraggingASlot_HidesTheSourceContent_RestoredOnDrop()
        {
            yield return WaitFrames(4);
            _inv.AddWood(3);          // wood in inventory slot 0
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            var src = SlotRef.Inventory(0);
            Assert.IsFalse(_ui.IsSourceDimmed(src), "source is not dimmed before the drag starts");

            // Begin the drag (real lifecycle seam — dims the source + raises the ghost).
            var grid = Grid();
            var cells = Children(grid);
            _ui.BeginDrag(src);
            yield return null;

            Assert.IsTrue(_ui.IsSourceDimmed(src),
                "while dragging, the source slot's content is hidden so the item shows only on the ghost " +
                "(#90 dup-fix — the source+ghost double-render was the reported 'duplicate')");

            // Drop onto a DIFFERENT slot (a landed move) — resolved by cursor position; the source restores.
            bool moved = _ui.EndDrag(cells[5].worldBound.center);
            yield return null;

            Assert.IsTrue(moved, "a position-resolved drop onto a different slot lands the move");
            Assert.IsFalse(_ui.IsSourceDimmed(src), "the source-dim is cleared once the drag ends (landed drop)");
            Assert.IsTrue(_inv.Model.InventorySlots[0].IsEmpty, "the item moved out of the source");
            Assert.AreEqual("wood", _inv.Model.InventorySlots[5].Def.Id, "the item landed in the drop target");
        }

        // #90 DUP-FIX — a CANCELLED drag (drop OUTSIDE any slot) must fully restore the source: no dim left
        // behind AND no item lost (the item never left the source).
        [UnityTest]
        public IEnumerator CancellingADrag_RestoresTheSource_NoDimNoLoss()
        {
            yield return WaitFrames(4);
            _inv.AddWood(3);
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            var src = SlotRef.Inventory(0);

            _ui.BeginDrag(src);
            yield return null;
            Assert.IsTrue(_ui.IsSourceDimmed(src), "the source dims at drag start");

            // Drop far outside every slot → no move; the item stays put and the dim must clear.
            bool moved = _ui.EndDrag(new Vector2(10000, 10000));
            yield return null;

            Assert.IsFalse(moved, "a drop outside every slot does not move anything");
            Assert.IsFalse(_ui.IsSourceDimmed(src),
                "a cancelled drag (drop outside any slot) clears the source-dim — no permanent dim (#90)");
            Assert.AreEqual("wood", _inv.Model.InventorySlots[0].Def.Id, "the item is still in the source");
            Assert.AreEqual(3, _inv.Model.InventorySlots[0].Count, "no item lost on a cancelled drag");
        }

        // #90 DUP-FIX (belt mirror) — a belt slot is drawn in BOTH the docked row and the bottom strip;
        // dragging it must dim BOTH mirrors, else the item still draws in the un-dimmed one.
        [UnityTest]
        public IEnumerator DraggingABeltSlot_DimsBothMirrors()
        {
            yield return WaitFrames(4);
            _inv.PickUpAxe();         // axe in belt slot 0
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            var dock = DockBelt();
            var strip = StripBelt();
            var dockCells = Children(dock);

            _ui.BeginDrag(SlotRef.Belt(0));
            yield return null;

            Assert.IsTrue(_ui.IsSourceDimmed(SlotRef.Belt(0)),
                "dragging a belt slot dims its source ref (both the dock + strip mirrors carry the class)");
            Assert.IsTrue(Children(dock)[0].ClassListContains(InventoryUI.DraggingSourceClass),
                "the docked-row mirror of the dragged belt slot is dimmed");
            Assert.IsTrue(Children(strip)[0].ClassListContains(InventoryUI.DraggingSourceClass),
                "the bottom-strip mirror of the same belt slot is ALSO dimmed (no un-dimmed duplicate)");

            // End the drag back on the same slot (a no-op drop) — both mirrors restore.
            _ui.EndDrag(dockCells[0].worldBound.center);
            yield return null;
            Assert.IsFalse(_ui.IsSourceDimmed(SlotRef.Belt(0)), "both mirrors restore on drag end");
        }

        // NIT 1 (86cabugc3) — assert the USS EFFECT live, not just the class-name string. While a drag is
        // active, the source slot's icon/chip/badge resolvedStyle.visibility must be Hidden (the
        // .slot--dragging-source rule actually hides the content). The EditMode companion
        // (UssRule_HidesSlotContentViaVisibility_NotDisplay) parses the USS file; this rides the live
        // laid-out panel so the resolved style — the real rendered effect — is what's asserted.
        [UnityTest]
        public IEnumerator DraggingASlot_ResolvesSourceContentToHidden_NotJustTheClass()
        {
            yield return WaitFrames(4);
            _inv.AddWood(3);              // wood in inventory slot 0
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            var src = SlotRef.Inventory(0);
            var cell0 = First(Grid());
            var icon = cell0.Q<VisualElement>("icon");
            var chip = cell0.Q<Label>("chip");
            var badge = cell0.Q<Label>("badge");
            Assert.AreEqual(Visibility.Visible, icon.resolvedStyle.visibility,
                "the source icon is visible BEFORE the drag begins");

            _ui.BeginDrag(src);
            yield return WaitFrames(4);   // let the USS class apply + styles re-resolve

            Assert.IsTrue(_ui.IsSourceDimmed(src), "the class lands on the source (sanity)");
            Assert.AreEqual(Visibility.Hidden, icon.resolvedStyle.visibility,
                "while dragging, the .slot--dragging-source USS rule must RESOLVE the icon to " +
                "visibility:Hidden — the actual rendered effect, not just the class string (NIT 1). A " +
                "deleted/renamed rule would leave it Visible and the item would double-render (source + ghost).");
            Assert.AreEqual(Visibility.Hidden, chip.resolvedStyle.visibility,
                "the letter-chip is hidden too (the chip is the no-icon fallback render of the item)");
            Assert.AreEqual(Visibility.Hidden, badge.resolvedStyle.visibility,
                "the stack badge is hidden too (else a count badge floats over the empty-looking source)");

            _ui.EndDrag(new Vector2(10000, 10000));   // cancel — restore the source
            yield return WaitFrames(2);
            Assert.AreEqual(Visibility.Visible, icon.resolvedStyle.visibility,
                "the source icon resolves back to Visible once the drag ends (the dim is not permanent)");
        }

        // NIT 2 (86cabugc3) — the dim must PRESERVE the source slot's layout box. visibility:hidden keeps the
        // cell laid out (worldBound stays valid); display:none would collapse it. The BUG 1 cursor-resolved
        // drop hit-tests the source slot's worldBound, so a collapsed box would silently break drop resolution
        // ON the source (e.g. a drag that ends back on the source, or a hover preview over it). Assert the
        // dragged source's worldBound is unchanged across BeginDrag (same rect → layout preserved).
        [UnityTest]
        public IEnumerator DraggingASlot_PreservesSourceWorldBound_LayoutNotCollapsed()
        {
            yield return WaitFrames(4);
            _inv.AddWood(3);              // wood in inventory slot 0
            _ui.SetOpen(true);
            yield return WaitFrames(8);   // grid laid out → worldBound is real

            var src = SlotRef.Inventory(0);
            var cell0 = First(Grid());
            Rect before = cell0.worldBound;
            Assert.Greater(before.width, 0f, "the source slot is laid out (non-degenerate) before the drag");
            Assert.Greater(before.height, 0f, "the source slot has a real height before the drag");

            _ui.BeginDrag(src);
            yield return WaitFrames(4);   // styles re-resolve; layout would collapse here if display:none

            Rect during = cell0.worldBound;
            Assert.IsTrue(_ui.IsSourceDimmed(src), "the source is dimmed (the rule is active — sanity)");
            Assert.AreEqual(before.width, during.width, 0.5f,
                "the dimmed source slot KEEPS its width — visibility:hidden preserves the layout box (NIT 2). " +
                "display:none would collapse it to 0, invalidating the worldBound the BUG 1 cursor-resolved " +
                "drop hit-tests against.");
            Assert.AreEqual(before.height, during.height, 0.5f,
                "the dimmed source slot KEEPS its height (layout preserved, not collapsed)");
            Assert.AreEqual(before.position, during.position,
                "the dimmed source slot stays in the SAME position (no reflow from a collapsed box)");

            // The worldBound is still a valid drop target: a drop AT the source's own center resolves back to
            // the source (and is a same-slot no-op move). If the box had collapsed, this point would resolve
            // to a different slot or to nothing.
            bool moved = _ui.EndDrag(during.center);
            yield return null;
            Assert.IsFalse(moved, "a drop back on the (still-laid-out) source is a same-slot no-op, not a move");
            Assert.AreEqual("wood", _inv.Model.InventorySlots[0].Def.Id, "the item is still in the source");
        }

        // NIT 3 (86cabugc3) — BeginDrag on an EMPTY-but-existing slot must hit the IsEmpty guard: no drag
        // arms, no source dims, no ghost. This is the regression-critical AC seam — without the guard, a drag
        // from an empty slot would raise an empty ghost + dim a slot that has nothing to dim, and a drop would
        // move "nothing" (or worse, smear state). Drives the real BeginDrag/EndDrag seams over the live panel.
        [UnityTest]
        public IEnumerator BeginDragOnEmptySlot_DoesNothing_HitsIsEmptyGuard()
        {
            yield return WaitFrames(4);
            _inv.AddWood(3);              // wood ONLY in inventory slot 0
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            // Slot 1 exists (laid out) but holds nothing.
            var empty = SlotRef.Inventory(1);
            Assert.IsTrue(_inv.Model.InventorySlots[1].IsEmpty, "slot 1 is empty-but-existing (the guard's case)");
            var cells = Children(Grid());
            Assert.GreaterOrEqual(cells.Count, 6, "the grid is laid out with its slots");
            Assert.Greater(cells[1].worldBound.width, 0f, "the empty slot is laid out (it EXISTS, just empty)");

            _ui.BeginDrag(empty);
            yield return WaitFrames(2);

            Assert.IsFalse(_ui.IsSourceDimmed(empty),
                "BeginDrag on an empty slot must NOT dim it — the IsEmpty guard early-returns before arming " +
                "the drag (NIT 3 — dimming/ghosting an empty slot is the regression this seam guards)");

            // The drag never armed, so an EndDrag onto the FILLED slot 0 is a no-drag no-op — it must NOT
            // move slot 0's wood (no phantom move from a never-started drag).
            bool moved = _ui.EndDrag(cells[0].worldBound.center);
            yield return null;
            Assert.IsFalse(moved, "EndDrag with no armed drag returns false — no phantom move");
            Assert.AreEqual("wood", _inv.Model.InventorySlots[0].Def.Id, "the real item is untouched");
            Assert.AreEqual(3, _inv.Model.InventorySlots[0].Count, "no count change from the empty-slot drag attempt");
        }

        // NIT 4 (86cabugc3) — a RefreshAll fired MID-DRAG (any Inventory.Changed: a pickup elsewhere, a belt
        // re-select, a need-tick consuming a belt item) must PRESERVE the source-dim class. PaintSlot clears
        // the transient drop-ok/drop-deny preview classes + re-toggles slot--selected, but must leave
        // slot--dragging-source alone — else a Changed event mid-drag would un-dim the source and the item
        // would flash back as the double-render. We drive a belt-slot drag, fire Changed via a DIFFERENT belt
        // slot's select (doesn't touch the dragged slot), and assert the dim survives the repaint.
        [UnityTest]
        public IEnumerator RefreshAllMidDrag_PreservesTheSourceDimClass()
        {
            yield return WaitFrames(4);
            _inv.PickUpAxe();            // axe in belt slot 0
            _inv.Model.SelectBelt(0);
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            var src = SlotRef.Belt(0);
            _ui.BeginDrag(src);
            yield return WaitFrames(2);
            Assert.IsTrue(_ui.IsSourceDimmed(src), "the dragged belt slot dims at drag start (sanity)");

            // Fire Inventory.Changed -> RefreshAll mid-drag WITHOUT touching the dragged slot: select a
            // DIFFERENT belt slot. (SelectBelt fires Changed; slot 0 stays the drag source.) This is the
            // real path a mid-drag pickup/need-tick would take.
            _inv.Model.SelectBelt(2);
            yield return WaitFrames(2);   // let RefreshAll repaint every slot

            Assert.IsTrue(_ui.IsSourceDimmed(src),
                "a RefreshAll fired mid-drag (Inventory.Changed) must PRESERVE the source-dim class — " +
                "PaintSlot re-toggles slot--selected + clears drop-preview classes, but must NOT clear " +
                "slot--dragging-source (NIT 4). If it did, a Changed event mid-drag would un-dim the source " +
                "and the item would flash back as the #90 double-render.");
            // Both belt mirrors must still carry the class after the repaint.
            Assert.IsTrue(Children(DockBelt())[0].ClassListContains(InventoryUI.DraggingSourceClass),
                "the docked-row mirror keeps the dim across the mid-drag repaint");
            Assert.IsTrue(Children(StripBelt())[0].ClassListContains(InventoryUI.DraggingSourceClass),
                "the bottom-strip mirror keeps the dim across the mid-drag repaint");

            _ui.EndDrag(new Vector2(10000, 10000));   // cancel — restore
            yield return null;
            Assert.IsFalse(_ui.IsSourceDimmed(src), "the dim clears once the drag ends");
        }

        // 86caffw9h DRAG-GHOST MISPOSITION — the drag-ghost's CENTER must land on the cursor it was driven to,
        // through the REAL production positioning path (PositionGhostAtScreenPoint: flip-then-ScreenToPanel).
        // This rides the live laid-out panel (the layer the EditMode pure-math test can't), asserting the
        // production seam puts the ghost on the cursor end-to-end. The non-1080p panel-SCALE divergence is
        // pinned by the EditMode unit test + the shipped-build -verifyInvDragGhostPos capture (which forces a
        // 2560x1440 window); here scale may resolve to 1 headlessly, but actual≈expected (both via the same
        // ScreenToPanel) is the invariant that proves the ghost tracks the cursor.
        [UnityTest]
        public IEnumerator DraggingASlot_GhostCenterTracksTheCursor()
        {
            yield return WaitFrames(4);
            _inv.AddWood(3);              // wood in inventory slot 0
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            _ui.BeginDrag(SlotRef.Inventory(0));
            yield return WaitFrames(2);

            // Drive a KNOWN cursor (screen px, Y-up) through the production positioning, then let layout settle.
            var cursor = new Vector2(Screen.width * 0.35f, Screen.height * 0.55f);
            _ui.PositionGhostAtScreenPoint(cursor);
            yield return WaitFrames(2);

            Vector2? actual = _ui.GhostCenterPanelPoint();
            Vector2? expected = _ui.ExpectedGhostPanelCenter(cursor);
            Assert.IsTrue(actual.HasValue && expected.HasValue,
                "the ghost + panel must be laid out so the center read-back is valid");
            float err = Vector2.Distance(actual.Value, expected.Value);
            Assert.LessOrEqual(err, 2f,
                "the drag-ghost CENTER must land on the cursor's panel point (flip-then-ScreenToPanel) — " +
                "86caffw9h: a scale-less convert would diverge by the panel scale (the recurring misposition)");

            _ui.EndDrag(new Vector2(10000, 10000));   // cancel — no move, just tear down the drag
            yield return null;
        }

        // ---- helpers ----

        private VisualElement Root() => _uiGo.GetComponent<UIDocument>().rootVisualElement;
        private VisualElement Grid() => Root()?.Q<VisualElement>("inv-grid");
        private VisualElement DockBelt() => Root()?.Q<VisualElement>("belt-dock");
        private VisualElement StripBelt() => Root()?.Q<VisualElement>("belt-bar-strip");

        private static VisualElement First(VisualElement parent)
        {
            foreach (var c in parent.Children()) return c;
            return null;
        }

        private static List<VisualElement> Children(VisualElement parent)
        {
            var list = new List<VisualElement>();
            foreach (var c in parent.Children()) list.Add(c);
            return list;
        }

        private static void SendClick(VisualElement target)
        {
            // A pointer-down on the slot is what InventoryUI's selection handler binds to. Use the slot
            // center so it resolves to that element.
            Vector2 p = target.worldBound.center;
            using (var down = PointerDownEvent.GetPooled())
            {
                down.target = target;
                target.SendEvent(down);
            }
        }

        private static IEnumerator WaitFrames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        // Load the REAL shipped shell UXML (the named grid/belt containers). Editor-only path — CI runs the
        // PlayMode suite in the editor, where AssetDatabase resolves the project asset.
        private static VisualTreeAsset LoadShellUxml()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/InventoryPanel.uxml");
#else
            return null;
#endif
        }

        private static StyleSheet LoadUss(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
#else
            return null;
#endif
        }
    }
}
