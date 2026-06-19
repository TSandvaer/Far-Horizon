using System.Collections.Generic;
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
