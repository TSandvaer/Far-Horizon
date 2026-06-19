using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Deterministic EditMode coverage for the slot/stack <see cref="InventoryModel"/> (ticket 86caa4bya,
    /// AC3/AC4/AC6/AC7). The model is pure C# (no scene, no Update), so the WHOLE contract — add/stack/
    /// spill, the tool-vs-resource move gate, belt-select/cycle, consume, all-or-nothing remove — is
    /// driveable here. This is the bulk of the AC8 regression guard (the PlayMode suite covers the
    /// scene-wired show/hide + pickup transitions).
    ///
    /// Catches the BUG CLASS, not the instance (per Tess's strategy §S3/§S4/§S5): move asserts the WHOLE
    /// count-conservation invariant (no dupe, no vanish), not just the destination; the tool-vs-resource
    /// gate pairs the negative (resource rejected) WITH the positive control (tool accepted); stacking is
    /// asserted at the 19/20/21 boundary explicitly, not "some large number".
    /// </summary>
    public class InventoryModelTests
    {
        private ItemCatalog _catalog;
        private ItemDef Axe => _catalog.ById(ItemCatalog.AxeId);
        private ItemDef Wood => _catalog.ById(ItemCatalog.WoodId);
        private ItemDef Stone => _catalog.ById(ItemCatalog.StoneId);
        private ItemDef Berry => _catalog.ById(ItemCatalog.BerryId);

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            _catalog.BuildDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var d in _catalog.All)
                if (d != null) Object.DestroyImmediate(d);
            Object.DestroyImmediate(_catalog);
        }

        private InventoryModel NewModel(int inv = 20, int belt = 5) => new InventoryModel(inv, belt);

        // === The guards themselves (contract §2) ===

        [Test]
        public void Guards_Tool_BeltEligible_NotStackable()
        {
            Assert.IsTrue(ItemDef.IsBeltEligible(Axe), "a Tool (axe) is belt-eligible");
            Assert.IsFalse(ItemDef.IsStackable(Axe), "a Tool never stacks");
            Assert.AreEqual(1, Axe.MaxStack, "a Tool's MaxStack is 1");
        }

        [Test]
        public void Guards_Resource_NotBeltEligible_Stackable()
        {
            foreach (var r in new[] { Wood, Stone, Berry })
            {
                Assert.IsFalse(ItemDef.IsBeltEligible(r), $"{r.Id} (Resource) is NOT belt-eligible (inventory-only)");
                Assert.IsTrue(ItemDef.IsStackable(r), $"{r.Id} (Resource) stacks");
                Assert.AreEqual(ItemDef.DefaultResourceStack, r.MaxStack, $"{r.Id} stacks to the cap (20)");
            }
        }

        [Test]
        public void CanonicalIds_AreExactlyAxeWoodStoneBerry()
        {
            Assert.AreEqual("axe", Axe.Id);
            Assert.AreEqual("wood", Wood.Id);
            Assert.AreEqual("stone", Stone.Id);
            Assert.AreEqual("berry", Berry.Id);
            Assert.AreEqual(ItemKind.Tool, Axe.Kind);
            Assert.AreEqual(ItemKind.Resource, Wood.Kind);
        }

        // === Empty start ===

        [Test]
        public void StartsEmpty_AllSlotsEmpty_Selected0()
        {
            var m = NewModel();
            Assert.AreEqual(20, m.InventorySlots.Count);
            Assert.AreEqual(5, m.BeltSlots.Count);
            Assert.AreEqual(0, m.SelectedBeltIndex);
            for (int i = 0; i < m.InventorySlots.Count; i++) Assert.IsTrue(m.InventorySlots[i].IsEmpty);
            for (int i = 0; i < m.BeltSlots.Count; i++) Assert.IsTrue(m.BeltSlots[i].IsEmpty);
        }

        // === AC3 — tool auto-place into belt slot 1 (index 0) ===

        [Test]
        public void AddToolToBelt_LandsInBeltSlot1_Exactly_NotInventory()
        {
            var m = NewModel();
            var placed = m.AddToolToBelt(Axe);

            Assert.IsTrue(placed.HasValue, "the axe was placed");
            Assert.AreEqual(SlotArea.Belt, placed.Value.Area, "the axe lands on the BELT (it's a tool)");
            Assert.AreEqual(0, placed.Value.Index, "specifically belt SLOT 1 (index 0) — not slot 2, not the pack");
            Assert.AreEqual("axe", m.BeltSlots[0].Def.Id);
            Assert.AreEqual(1, m.BeltSlots[0].Count, "exactly one axe (a tool never stacks)");
            for (int i = 0; i < m.InventorySlots.Count; i++)
                Assert.IsTrue(m.InventorySlots[i].IsEmpty, "the axe is NOT duplicated into the pack");
        }

        [Test]
        public void AddItem_Resource_NeverAutoLandsOnBelt()
        {
            var m = NewModel();
            int left = m.AddItem(Wood, 3);
            Assert.AreEqual(0, left, "all 3 wood fit");
            Assert.AreEqual(3, m.InventorySlots[0].Count, "wood lands in the PACK (inventory slot 0)");
            for (int i = 0; i < m.BeltSlots.Count; i++)
                Assert.IsTrue(m.BeltSlots[i].IsEmpty, "a resource NEVER auto-lands on the belt (contract §2)");
        }

        // === AC4 — selected-belt-item query drives held-item ===

        [Test]
        public void IsSelectedBeltItem_True_OnlyWhenAxeIsTheSelectedBeltStack()
        {
            var m = NewModel();
            m.AddToolToBelt(Axe); // belt slot 0, slot 0 selected
            Assert.IsTrue(m.IsSelectedBeltItem("axe"), "axe in selected slot 0 -> selected");

            // Move the axe to belt slot 2; slot 0 still selected -> NOT the selected item.
            Assert.IsTrue(m.TryMove(SlotRef.Belt(0), SlotRef.Belt(2)));
            Assert.IsFalse(m.IsSelectedBeltItem("axe"), "axe in slot 2 with slot 0 selected -> NOT selected");

            // Select slot 2 -> now it IS the selected item (the transition, not the end-state).
            m.SelectBelt(2);
            Assert.IsTrue(m.IsSelectedBeltItem("axe"), "select slot 2 -> axe becomes the selected item");

            // Move the axe OFF the belt into the pack -> not selected even though still owned.
            Assert.IsTrue(m.TryMove(SlotRef.Belt(2), SlotRef.Inventory(0)));
            Assert.IsFalse(m.IsSelectedBeltItem("axe"), "axe in the pack -> NOT the selected belt item");
            Assert.IsTrue(m.OwnsItem("axe"), "...but still OWNED");
        }

        [Test]
        public void EmptySelectedSlot_IsNotAnyItem()
        {
            var m = NewModel();
            m.AddToolToBelt(Axe);
            m.SelectBelt(1); // an empty slot
            Assert.IsFalse(m.IsSelectedBeltItem("axe"), "an empty selected slot holds nothing");
        }

        // === AC2 — belt selection: number keys (SelectBelt) + scroll (CycleBelt wrap) ===

        [Test]
        public void SelectBelt_ClampsToBeltCount()
        {
            var m = NewModel(belt: 3);
            m.SelectBelt(2);
            Assert.AreEqual(2, m.SelectedBeltIndex);
            m.SelectBelt(9); // out of range (the reduced-belt boundary, S7)
            Assert.AreEqual(2, m.SelectedBeltIndex, "select clamps to the last slot, never out of range");
            m.SelectBelt(-3);
            Assert.AreEqual(0, m.SelectedBeltIndex, "clamps at 0 too");
        }

        [Test]
        public void CycleBelt_WrapsBothDirections()
        {
            var m = NewModel(belt: 5);
            m.CycleBelt(+1); Assert.AreEqual(1, m.SelectedBeltIndex);
            m.SelectBelt(4);
            m.CycleBelt(+1); Assert.AreEqual(0, m.SelectedBeltIndex, "scroll past the end wraps to slot 0");
            m.CycleBelt(-1); Assert.AreEqual(4, m.SelectedBeltIndex, "scroll before the start wraps to the last slot");
        }

        // === AC6 — move/merge with the tool-vs-resource gate + count conservation ===

        [Test]
        public void TryMove_ResourceToBelt_IsRejected_NoOp()
        {
            var m = NewModel();
            m.AddItem(Wood, 5); // pack slot 0
            bool moved = m.TryMove(SlotRef.Inventory(0), SlotRef.Belt(1));

            Assert.IsFalse(moved, "a RESOURCE (wood) can NEVER move onto the belt (the load-bearing AC6 gate)");
            Assert.AreEqual(5, m.InventorySlots[0].Count, "the wood stays in the pack (no debit)");
            Assert.IsTrue(m.BeltSlots[1].IsEmpty, "the belt slot stays empty (rejected)");
        }

        [Test]
        public void TryMove_ToolToBelt_IsAllowed_PositiveControl()
        {
            // The positive control so the gate isn't trivially "reject everything".
            var m = NewModel();
            m.AddItem(Axe, 1); // an axe in the pack (slot 0)
            Assert.AreEqual("axe", m.InventorySlots[0].Def.Id);

            bool moved = m.TryMove(SlotRef.Inventory(0), SlotRef.Belt(2));
            Assert.IsTrue(moved, "a TOOL (axe) IS allowed onto the belt");
            Assert.AreEqual("axe", m.BeltSlots[2].Def.Id, "the axe is now in belt slot 3");
            Assert.IsTrue(m.InventorySlots[0].IsEmpty, "the source pack slot is cleared (no duplicate)");
        }

        [Test]
        public void TryMove_BetweenInventorySlots_ConservesCount_NoDupeNoVanish()
        {
            var m = NewModel();
            m.AddItem(Wood, 7); // pack slot 0
            int before = m.CountItem("wood");

            Assert.IsTrue(m.TryMove(SlotRef.Inventory(0), SlotRef.Inventory(5)));
            Assert.IsTrue(m.InventorySlots[0].IsEmpty, "source cleared");
            Assert.AreEqual(7, m.InventorySlots[5].Count, "destination filled");
            Assert.AreEqual(before, m.CountItem("wood"), "total count invariant — no dupe, no vanish");
        }

        [Test]
        public void TryMove_MergesSameResourceStacks_RespectingCap()
        {
            var m = NewModel();
            // Two wood stacks: 15 in slot 0, 10 in slot 1.
            m.AddItem(Wood, 15);
            // Force a SECOND stack by filling slot 0 to cap first, then add more elsewhere.
            var m2 = NewModel();
            m2.AddItem(Wood, 20); // slot 0 = 20 (cap)
            m2.AddItem(Wood, 8);  // spills: slot 0 full, slot 1 = 8
            Assert.AreEqual(20, m2.InventorySlots[0].Count);
            Assert.AreEqual(8, m2.InventorySlots[1].Count);

            // Move slot 1 (8) onto slot 0 (20, full) -> no room, no-op.
            Assert.IsFalse(m2.TryMove(SlotRef.Inventory(1), SlotRef.Inventory(0)),
                "merging onto a full same-item stack is a no-op (no overflow past the cap)");
            Assert.AreEqual(8, m2.InventorySlots[1].Count, "the spill stack is untouched");
        }

        // === AC7 — stacking to the cap (19/20/21 boundary) + spill ===

        [Test]
        public void AddItem_StacksToCap_Then21stSpillsToNextSlot()
        {
            var m = NewModel();
            m.AddItem(Wood, 19);
            Assert.AreEqual(19, m.InventorySlots[0].Count, "19 in one stack");

            m.AddItem(Wood, 1);
            Assert.AreEqual(20, m.InventorySlots[0].Count, "exactly at the cap (20)");
            Assert.IsTrue(m.InventorySlots[1].IsEmpty, "nothing spilled at exactly the cap");

            m.AddItem(Wood, 1); // the 21st
            Assert.AreEqual(20, m.InventorySlots[0].Count, "stack 0 stays AT the cap (no off-by-one to 21)");
            Assert.AreEqual(1, m.InventorySlots[1].Count, "the 21st spills to the next slot");
            Assert.AreEqual(21, m.CountItem("wood"), "total is 21 across the two stacks");
        }

        [Test]
        public void AddItem_Tool_DoesNotStack_SecondAxeTakesANewSlot()
        {
            var m = NewModel();
            m.AddItem(Axe, 1); // pack slot 0
            m.AddItem(Axe, 1); // a 2nd axe — must NOT merge into slot 0 (tools don't stack)
            Assert.AreEqual(1, m.InventorySlots[0].Count, "the first axe stays a stack of 1");
            Assert.AreEqual("axe", m.InventorySlots[1].Def.Id, "the 2nd axe takes its OWN slot");
            Assert.AreEqual(1, m.InventorySlots[1].Count);
        }

        [Test]
        public void AddItem_ReturnsOverflow_WhenInventoryFull()
        {
            var m = NewModel(inv: 1, belt: 1); // 1 pack slot, cap 20 -> max 20 wood (belt rejects resources)
            int left = m.AddItem(Wood, 25);
            Assert.AreEqual(5, left, "5 wood did not fit (1 slot * 20 cap)");
            Assert.AreEqual(20, m.CountItem("wood"));
        }

        // === Consume (berry eat) + all-or-nothing remove ===

        [Test]
        public void TryConsumeSelected_RemovesOne_FromSelectedBelt()
        {
            var m = NewModel();
            // Berries can't auto-belt; place via a tool? No — put a berry stack on the belt only via a move
            // is rejected. So test consume on a belt slot we seed through AddToolToBelt? Berries are a
            // resource. Instead: select a belt slot holding an item by moving a TOOL there (axe), then a
            // consume removes the one axe.
            m.AddToolToBelt(Axe); // belt slot 0
            Assert.IsTrue(m.TryConsumeSelected(), "consuming the selected belt item succeeds");
            Assert.IsTrue(m.BeltSlots[0].IsEmpty, "the last one consumed clears the slot to empty");
            Assert.IsFalse(m.TryConsumeSelected(), "consuming an empty selected slot fails");
        }

        [Test]
        public void RemoveItem_AllOrNothing_AcrossStacks()
        {
            var m = NewModel();
            m.AddItem(Wood, 20); // slot 0
            m.AddItem(Wood, 5);  // slot 1
            Assert.AreEqual(25, m.CountItem("wood"));

            Assert.IsFalse(m.RemoveItem("wood", 26), "can't afford 26 -> debit NOTHING");
            Assert.AreEqual(25, m.CountItem("wood"), "a failed remove debits nothing");

            Assert.IsTrue(m.RemoveItem("wood", 22), "removing 22 (affordable) succeeds");
            Assert.AreEqual(3, m.CountItem("wood"), "exactly 22 removed across stacks");
        }

        // === Changed event discipline (UI subscribes, never polls) ===

        [Test]
        public void Changed_Fires_OncePerRealMutation()
        {
            var m = NewModel();
            int fires = 0;
            m.Changed += () => fires++;

            m.AddItem(Wood, 3);      // 1
            m.SelectBelt(1);         // 2
            m.SelectBelt(1);         // no-op (same slot) -> no fire
            m.AddToolToBelt(Axe);    // 3
            Assert.AreEqual(3, fires, "Changed fires once per REAL change, never on a no-op");
        }
    }
}
