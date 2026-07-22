using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode truth-table guard for the LEFT-CLICK BOULDER-MINE gate (ticket 86camz9v7 / ② — boulder mining is
    /// an active left-click WITH A PICKAXE SELECTED, NOT proximity-auto). The boulder verb REUSES the pure static
    /// <see cref="MineOre.ShouldMineOnClick"/> (the guard is generic — inRange + pickaxeSelected + the three
    /// world-click guards), so the whole guard table is already covered by MineClickGateTests; this file pins the
    /// BOULDER-SPECIFIC piece: the WIDENED tier gate <see cref="MineBoulder.IsBoulderPickaxeSelected"/> (a WOOD,
    /// stone, OR iron pickaxe SELECTED — the ② entry gate is the wood pickaxe). Sibling of MineClickGateTests.
    /// </summary>
    public class MineBoulderClickGateTests
    {
        // === The boulder verb reuses the SAME pure guard — proven with the boulder's pickaxe-selection semantics ===

        [Test]
        public void Mines_OnlyWhenAllPreconditionsHold()
        {
            Assert.IsTrue(
                MineOre.ShouldMineOnClick(inRange: true, pickaxeSelected: true,
                                          uiPanelOpen: false, pointerOverUI: false, rmbHeld: false),
                "a left-click mines a boulder ONLY when in range + a pickaxe selected + no panel + not over UI + RMB up");
        }

        [Test]
        public void NoMine_WhenOutOfRange_OrNoPickaxe_OrPanel_OrOverUI_OrRmb()
        {
            Assert.IsFalse(MineOre.ShouldMineOnClick(false, true, false, false, false), "out of range → no mine");
            Assert.IsFalse(MineOre.ShouldMineOnClick(true, false, false, false, false), "no pickaxe selected → no mine");
            Assert.IsFalse(MineOre.ShouldMineOnClick(true, true, true, false, false), "modal panel open → no mine");
            Assert.IsFalse(MineOre.ShouldMineOnClick(true, true, false, true, false), "pointer over UI → no mine");
            Assert.IsFalse(MineOre.ShouldMineOnClick(true, true, false, false, true), "RMB orbit drag → no mine");
        }

        // === THE WIDENED TIER GATE — a WOOD pickaxe (the ② entry tool) mines boulders; stone/iron also do; axe/none don't ===

        [Test]
        public void IsBoulderPickaxeSelected_TrueForWoodStoneIron_WhenSelected_FalseOtherwise()
        {
            var invGo = new GameObject("Inventory");
            try
            {
                var inv = invGo.AddComponent<Inventory>();
                Assert.IsFalse(MineBoulder.IsBoulderPickaxeSelected(null), "null inventory → not selected");
                Assert.IsFalse(MineBoulder.IsBoulderPickaxeSelected(inv), "empty belt → not selected");

                // Each of the three pickaxe tiers, when SELECTED, satisfies the boulder gate.
                foreach (var id in new[] { ItemCatalog.PickaxeWoodId, ItemCatalog.PickaxeStoneId, ItemCatalog.PickaxeIronId })
                {
                    var freshGo = new GameObject("InvFresh");
                    var fresh = freshGo.AddComponent<Inventory>();
                    var slot = fresh.Model.AddToolToBelt(fresh.Catalog.ById(id));
                    Assert.IsTrue(slot.HasValue, $"'{id}' is a belt-eligible Tool and lands on the belt");
                    fresh.Model.SelectBelt(slot.Value.Index);
                    Assert.IsTrue(MineBoulder.IsBoulderPickaxeSelected(fresh),
                        $"a SELECTED '{id}' satisfies the boulder gate (wood is the entry tier; stone/iron are widened up)");
                    Object.DestroyImmediate(freshGo);
                }
            }
            finally
            {
                Object.DestroyImmediate(invGo);
            }
        }

        [Test]
        public void IsBoulderPickaxeSelected_FalseForNonPickaxe_AndForOwnedButNotSelected()
        {
            var invGo = new GameObject("Inventory");
            try
            {
                var inv = invGo.AddComponent<Inventory>();

                // An AXE selected is NOT a pickaxe → no boulder mine.
                var axeSlot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.AxeId));
                Assert.IsTrue(axeSlot.HasValue, "the axe lands on the belt");
                inv.Model.SelectBelt(axeSlot.Value.Index);
                Assert.IsFalse(MineBoulder.IsBoulderPickaxeSelected(inv), "an AXE selected is not a pickaxe → no boulder mine");

                // A wood pickaxe OWNED but NOT the selected belt item → the selection gate refuses it.
                var pickSlot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.PickaxeWoodId));
                Assert.IsTrue(pickSlot.HasValue, "the wood pickaxe also lands on the belt");
                inv.Model.SelectBelt(axeSlot.Value.Index); // keep the AXE selected
                Assert.IsFalse(MineBoulder.IsBoulderPickaxeSelected(inv),
                    "wood pickaxe OWNED but the AXE is selected → not selected (the selection gate, not ownership)");
            }
            finally
            {
                Object.DestroyImmediate(invGo);
            }
        }

        // === A bare MineBoulder with NO input has NO active chain (the chain is input-driven, never auto-started) ===
        [Test]
        public void BareMineBoulder_NoInput_HasNoActiveChain()
        {
            var go = new GameObject("MineBoulderChainProbe");
            try
            {
                var mine = go.AddComponent<MineBoulder>();
                Assert.IsFalse(mine.IsMineChainActive, "a freshly-added MineBoulder with no held input has NO chain");
                mine.SetMineHeld(true);
                Assert.IsFalse(mine.IsMineChainActive, "SetMineHeld alone (no gated Update) does not start a chain");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
