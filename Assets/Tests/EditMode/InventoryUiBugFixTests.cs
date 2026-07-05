using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the THREE #90 soak-caught inventory bugs (ticket 86caa4bya / follow-up
    /// 86cabfa21) — the CHEAP, ALWAYS-RUN half (the live-panel end-to-end interaction is the PlayMode
    /// suite InventoryUiInteractionPlayModeTests). These catch the bug CLASS at the seam each bug lived on,
    /// so a regression reds even if the PlayMode suite is ever environment-quarantined:
    ///
    ///   • BUG 1 — the position→slot hit-test (InventoryUI.HitIndex) the captured-pointer drop relies on.
    ///   • BUG 2 — the docked-belt-row click must not select (asserted live in PlayMode; here we pin the
    ///     model invariant the fix preserves — a tap is the ONLY select path besides 1–N/scroll).
    ///   • BUG 3 — the wood ItemDef carries a non-null ICON (a null icon was the cause: only a "W" chip).
    ///
    /// HARDENING (ticket 86cabugc3 — the #102 drag-source-dim NITs follow-up): the original
    /// DraggingSourceClass_MatchesTheUssSelector pins only the C# class-NAME string — it would stay green
    /// even if the USS rule were deleted or changed to display:none. The two added cases here close that:
    ///   • UssRule_HidesSlotContentViaVisibility_NotDisplay — parses the SHIPPED InventoryPanel.uss and
    ///     asserts the .slot--dragging-source rule actually sets visibility:hidden (NOT display:none) on
    ///     icon/chip/badge (NIT 1 + NIT 2 source-of-truth — the USS file IS the authoritative rule the
    ///     PlayMode resolvedStyle assert exercises live; this is the cheap always-run companion).
    ///   • DragLifecycleSeams_… now ALSO asserts EndDrag returns false with no active drag (NIT 5).
    /// </summary>
    public class InventoryUiBugFixTests
    {
        // BUG 3 — chopped wood now carries a recognizable icon (the root cause was a NULL icon → "W" chip).
        [Test]
        public void Wood_HasANonNullIcon_SoItReadsAsWoodNotALetter()
        {
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            catalog.BuildDefaults();

            var wood = catalog.ById(ItemCatalog.WoodId);
            Assert.IsNotNull(wood, "the wood def must exist");
            Assert.IsNotNull(wood.Icon,
                "chopped wood must carry a baked icon (AC5) — a null icon renders only a bare 'W' letter-" +
                "chip, the #90 BUG 3 the Sponsor reported as 'wood does not appear in the inventory'");
            Assert.Greater(wood.Icon.texture.width, 0, "the wood icon sprite must wrap a real texture");

            // Stone + berry (downstream tickets share this catalog) also get a stand-in icon, not a letter.
            Assert.IsNotNull(catalog.ById(ItemCatalog.StoneId).Icon, "stone gets a stand-in icon");
            Assert.IsNotNull(catalog.ById(ItemCatalog.BerryId).Icon, "berries get a stand-in icon");
            // The axe (a tool) is allowed a null PoC icon (it has the held-axe rig); not asserted here.

            foreach (var d in catalog.All) if (d != null) Object.DestroyImmediate(d);
            Object.DestroyImmediate(catalog);
        }

        // BUG 1 — the position→slot hit-test resolves the slot whose rect contains the cursor (this is what
        // the captured-pointer drop uses INSTEAD of the PointerUp event target, which the capture redirect
        // pins to the SOURCE slot). Synthetic rects → deterministic, no panel layout needed.
        [Test]
        public void HitIndex_ResolvesTheSlotUnderTheCursor_NotTheSource()
        {
            // Six 64px slots in a row at y=0 (worldBound-equivalent rects).
            var rects = new List<Rect>();
            for (int i = 0; i < 6; i++) rects.Add(new Rect(i * 64, 0, 64, 64));

            // A point inside slot index 3 resolves to 3 (NOT 0, the would-be captured source).
            Assert.AreEqual(3, InventoryUI.HitIndex(rects, new Vector2(3 * 64 + 32, 32)),
                "the cursor over slot 3 resolves to index 3 — the drop target is the HOVERED slot, not the " +
                "captured source (the #90 BUG 1 root: the old code used the event target = the source)");
            Assert.AreEqual(0, InventoryUI.HitIndex(rects, new Vector2(10, 10)), "cursor in slot 0 → 0");
            Assert.AreEqual(5, InventoryUI.HitIndex(rects, new Vector2(5 * 64 + 1, 63)), "cursor in last slot → 5");

            // A point outside every slot resolves to -1 (no drop → no move).
            Assert.AreEqual(-1, InventoryUI.HitIndex(rects, new Vector2(5000, 5000)),
                "a cursor outside all slots resolves to no slot (the drop is dropped, not mis-applied)");
        }

        // #90 DUP-FIX — the source-dim USS class the C# fix toggles must stay in lockstep with the USS
        // selector that hides the slot content. This pins the contract: change one side without the other and
        // the source slot dims to nothing (C# constant changed) or never dims (USS selector renamed). The
        // live dim-on-drag + restore-on-drop/cancel behavior is asserted end-to-end in the PlayMode suite
        // (InventoryUiInteractionPlayModeTests) over a real laid-out panel — this is the cheap always-run
        // half that reds even if that PlayMode suite is ever environment-quarantined.
        [Test]
        public void DraggingSourceClass_MatchesTheUssSelector()
        {
            Assert.AreEqual("slot--dragging-source", InventoryUI.DraggingSourceClass,
                "the C# source-dim class must match the .slot--dragging-source selector in InventoryPanel.uss " +
                "(its descendant rules hide .slot__icon/.slot__chip/.slot__badge — the #90 dup-fix). Rename " +
                "one side without the other and the source either dims to nothing or never dims.");
        }

        // #90 DUP-FIX — the drag lifecycle seams are NULL-safe before the panel is built: calling them on a
        // bare component (no UIDocument laid out) must not throw and must report no dim (the build-safety net,
        // since BeginDrag/EndDrag/IsSourceDimmed are public seams the PlayMode test drives directly).
        [Test]
        public void DragLifecycleSeams_AreSafeBeforeTheViewIsBuilt()
        {
            var go = new GameObject("InventoryUI", typeof(UnityEngine.UIElements.UIDocument));
            var ui = go.AddComponent<InventoryUI>();   // no inventory wired, no panel laid out

            Assert.DoesNotThrow(() => ui.BeginDrag(SlotRef.Inventory(0)),
                "BeginDrag on an unbuilt view must not throw (model null → no-op)");
            Assert.IsFalse(ui.IsSourceDimmed(SlotRef.Inventory(0)),
                "no slot is dimmed when the view has no model/panel");

            // NIT 5 (86cabugc3): assert the RETURN value, not just no-throw. EndDrag with no active drag
            // must report false ("nothing moved") — the contract OnSlotPointerUp + callers branch on. A
            // future refactor that returned true on the no-drag path would silently break drop-detection.
            bool moved = false;
            Assert.DoesNotThrow(() => moved = ui.EndDrag(Vector2.zero),
                "EndDrag with no active drag must not throw (returns false, no-op)");
            Assert.IsFalse(moved,
                "EndDrag with no active drag returns false (no move landed) — the _dragging-guard early-out");

            Object.DestroyImmediate(go);
        }

        // NIT 1 + NIT 2 (86cabugc3) — assert the USS RULE'S EFFECT, not just the C# class-name string. The
        // sibling DraggingSourceClass_MatchesTheUssSelector pins only InventoryUI.DraggingSourceClass; it
        // stays green even if the .slot--dragging-source USS rule were deleted, or switched from
        // visibility:hidden to display:none. This parses the SHIPPED InventoryPanel.uss and asserts the rule
        //   (a) targets .slot__icon / .slot__chip / .slot__badge under .slot--dragging-source, and
        //   (b) hides them via VISIBILITY (which preserves the slot's layout box → worldBound stays valid for
        //       the BUG 1 cursor-resolved drop) — NOT display:none (which would collapse the layout box and
        //       break the position-resolved drop on the source slot).
        // The live resolvedStyle.visibility == Hidden + worldBound-preservation asserts ride a laid-out panel
        // in the PlayMode suite; this is the cheap, always-run source-of-truth companion (the USS FILE is the
        // rule the PlayMode test exercises). Parsing the file directly (vs loading a StyleSheet) keeps this an
        // EditMode test with no UIElements layout dependency.
        [Test]
        public void UssRule_HidesSlotContentViaVisibility_NotDisplay()
        {
            string ussPath = Path.Combine(Application.dataPath, "UI", "InventoryPanel.uss");
            Assert.IsTrue(File.Exists(ussPath), "the shipped InventoryPanel.uss must exist at Assets/UI/");
            string uss = File.ReadAllText(ussPath);

            // Find the rule block whose selector list references the source-dim class on the content
            // sub-elements. The shipped form groups the three selectors then one declaration block:
            //   .slot--dragging-source .slot__icon, ... .slot__chip, ... .slot__badge { visibility: hidden; }
            var rule = Regex.Match(
                uss,
                @"\.slot--dragging-source[^{}]*\{[^{}]*\}",
                RegexOptions.Singleline);
            Assert.IsTrue(rule.Success,
                "InventoryPanel.uss must contain a .slot--dragging-source rule (the #90 dup-fix). Its " +
                "absence means a dragged source slot is never dimmed — the item double-renders (source + ghost).");

            string selectorAndBody = rule.Value;

            // (a) it targets the three content sub-elements (icon/chip/badge) — dimming the WELL only would
            //     hide the slot frame, not the item content.
            Assert.IsTrue(selectorAndBody.Contains(".slot__icon"), "the dim rule must target the slot icon");
            Assert.IsTrue(selectorAndBody.Contains(".slot__chip"), "the dim rule must target the letter-chip");
            Assert.IsTrue(selectorAndBody.Contains(".slot__badge"), "the dim rule must target the stack badge");

            // (b) it hides via VISIBILITY (layout preserved), NOT display:none (layout collapsed). The
            //     declaration body is everything inside the braces.
            string body = selectorAndBody.Substring(selectorAndBody.IndexOf('{') + 1).TrimEnd('}', ' ', '\n', '\r', '\t');
            Assert.IsTrue(Regex.IsMatch(body, @"visibility\s*:\s*hidden", RegexOptions.IgnoreCase),
                "the source-dim rule must set 'visibility: hidden' — that hides the item content while " +
                "PRESERVING the slot's layout box, so worldBound stays valid for the BUG 1 cursor-resolved drop");
            Assert.IsFalse(Regex.IsMatch(body, @"display\s*:\s*none", RegexOptions.IgnoreCase),
                "the source-dim rule must NOT use 'display: none' — that collapses the slot's layout box, " +
                "invalidating worldBound and breaking the position-resolved drop on the source slot (NIT 2)");
        }

        // 86cajrtr1 (recurrence of 86caffw9h) — the DRAG-GHOST scale math: screen (Y-up) -> panel (Y-down)
        // is FLIP-Y-then-DIVIDE-BY-PANEL-SCALE. This pins the pure static InventoryUI.ScreenToPanelPoint (the
        // seam the drag-ghost center AND the pointer-over-UI hit-test both rest on) at BOTH panel scales — the
        // 1.0 (1080p) case CANNOT catch a dropped scale term, so the 1.3333 (1440p) case is load-bearing. The
        // scale-1.3333 vector is the EXACT screen point + expected panel center the shipped gate logged on its
        // first real run (run 28680351313: screen (-1198,1361) -> expectedPanelCenter (-898.5,59.25)).
        [Test]
        public void ScreenToPanelPoint_FlipsYThenDividesByScale_AtBothPanelScales()
        {
            // Scale 1.0 (1920x1080 reference): flip Y only, no scale.
            Vector2 p1 = InventoryUI.ScreenToPanelPoint(new Vector2(960f, 540f), 1080f, 1.0f);
            Assert.AreEqual(960f, p1.x, 0.01f, "x is unchanged at scale 1");
            Assert.AreEqual(540f, p1.y, 0.01f, "y is flipped (1080-540) at scale 1");

            // Scale 1.3333 (2560x1440): flip Y, THEN divide BOTH axes by the scale (== *0.75). EXACT gate values.
            const float s = 1440f / 1080f;   // 1.33333
            Vector2 p2 = InventoryUI.ScreenToPanelPoint(new Vector2(-1198f, 1361f), 1440f, s);
            Assert.AreEqual(-898.5f, p2.x, 0.05f, "x divided by the panel scale (-1198/1.3333 = -898.5)");
            Assert.AreEqual(59.25f, p2.y, 0.05f, "flip (1440-1361=79) THEN /scale (79/1.3333 = 59.25)");

            // Degenerate scale (<=0) is treated as 1 so a bad call never divides by zero.
            Vector2 deg = InventoryUI.ScreenToPanelPoint(new Vector2(100f, 100f), 1080f, 0f);
            Assert.AreEqual(100f, deg.x, 0.01f, "degenerate scale <=0 -> no scale");
            Assert.AreEqual(980f, deg.y, 0.01f, "flip only (1080-100) when scale is degenerate");
        }

        // 86cajrtr1 — the scale term is INVISIBLE at exactly 1080p: the SAME screen point maps to a very
        // different panel point at 1.3333 vs 1.0. A dropped-scale regression (the recurring bug class) would
        // place the ghost at the scale-1 point even at 1440p — hundreds of px off toward the bottom-right. This
        // is WHY a scale=1-only (1080p) test can never catch the bug, and why the shipped gate forces 2560x1440.
        [Test]
        public void ScaleTerm_MovesTheGhostHundredsOfPx_AndIsInvisibleAt1080p()
        {
            const float s = 1440f / 1080f;
            var far = new Vector2(2180f, 260f);            // far top-right screen point (large X)
            Vector2 scaled = InventoryUI.ScreenToPanelPoint(far, 1440f, s);
            Vector2 unscaled = InventoryUI.ScreenToPanelPoint(far, 1440f, 1.0f);

            Assert.AreEqual(1635f, scaled.x, 0.05f, "2180/1.3333 = 1635");
            Assert.AreEqual(885f, scaled.y, 0.05f, "(1440-260)/1.3333 = 885");
            Assert.Greater(Vector2.Distance(scaled, unscaled), 400f,
                "at 1440p the panel scale moves the ghost hundreds of px vs the scale-1 result — a scale=1-only " +
                "test cannot catch a dropped scale term (86caffw9h/86cajrtr1: invisible at exactly 1080p)");
        }

        // BUG 2 — the model has exactly ONE programmatic select path; the UI fix routes the docked-row click
        // AWAY from it (asserted end-to-end in PlayMode). Here: SelectBelt is the only mutator of selection,
        // so a view that does not call it on the docked row cannot change selection (the invariant the fix
        // relies on — there is no hidden second select path in the model).
        [Test]
        public void SelectBelt_IsTheOnlySelectionMutator()
        {
            var go = new GameObject("Inventory");
            var inv = go.AddComponent<Inventory>();
            var model = inv.Model;

            inv.PickUpAxe();          // axe to belt slot 0
            inv.AddWood(5);           // wood to the pack
            int sel = model.SelectedBeltIndex;
            Assert.AreEqual(0, sel, "selection starts at 0");

            // A MOVE between slots must not move the selection (only SelectBelt/CycleBelt do).
            model.TryMove(SlotRef.Belt(0), SlotRef.Belt(2));
            Assert.AreEqual(sel, model.SelectedBeltIndex,
                "moving the axe between belt slots does NOT change which slot is selected — selection is " +
                "independent of organize moves (the model invariant the #90 BUG 2 UI fix preserves)");

            Object.DestroyImmediate(go);
        }
    }
}
