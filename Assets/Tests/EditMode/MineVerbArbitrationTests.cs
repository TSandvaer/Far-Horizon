using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the mine-verb ARBITRATION query <c>WouldClaimClick()</c> (86caffwv5 round-4/round-5).
    /// Tess flagged this path UNTESTED and soak-5 proved the gap real; this pins the arbitration contract
    /// MeleeAttack relies on to suppress its whiff swing when a mine verb owns the click.
    ///
    /// NOTE (round-5 diagnosis, diagnose-via-trace): the boulder MINE GATE already ACCEPTS the wood pickaxe
    /// (proven green by <see cref="MineBoulderClickGateTests"/>) and the mine verbs already FACE the target
    /// (BeginMineSwing calls character.FaceWorldTarget before TriggerMine — byte-identical to the tree-chop
    /// path). So the ticket's "boulder gate rejects wood / no turn" framing is refuted by the code + these
    /// tests; the soak-5 live failure is in the walk / real-mouse / belt-selection layer the teleport-based
    /// -verifyBoulder/-verifyMine gates cannot exercise. This file locks the arbitration half.
    /// </summary>
    public class MineVerbArbitrationTests
    {
        // --- MineBoulder.WouldClaimClick: a WOOD pickaxe selected + a boulder in range → the boulder verb claims. ---

        [Test]
        public void MineBoulder_WouldClaimClick_TrueWhenWoodPickaxeSelected_AndBoulderInRange()
        {
            var invGo = new GameObject("Inventory");
            var playerGo = new GameObject("Player");
            var rootGo = new GameObject("Boulders");
            var mineGo = new GameObject("MineBoulder");
            try
            {
                var inv = invGo.AddComponent<Inventory>();
                var slot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.PickaxeWoodId));
                Assert.IsTrue(slot.HasValue, "the wood pickaxe lands on the belt");
                inv.Model.SelectBelt(slot.Value.Index);

                playerGo.transform.position = Vector3.zero;

                // A single mineable boulder 1u from the player (well inside the 2.4u boulder mineRadius).
                var boulder = new GameObject(MineBoulder.BoulderNodeName);
                boulder.transform.SetParent(rootGo.transform, false);
                boulder.transform.position = new Vector3(1f, 0f, 0f);

                var mine = mineGo.AddComponent<MineBoulder>();
                mine.inventory = inv;
                mine.player = playerGo.transform;
                mine.boulderRoot = rootGo.transform;
                mine.mineRadius = 2.4f;
                mine.InitializePoolForTest(); // Start does not auto-fire in EditMode
                Assert.AreEqual(1, mine.NodeCount, "the one authored boulder is discovered");

                Assert.IsTrue(mine.WouldClaimClick(),
                    "wood pickaxe selected + a boulder in range → the boulder-mine verb OWNS the click (MeleeAttack suppresses its whiff)");

                // Walk the player OUT of range → no claim (the whiff would then be allowed).
                playerGo.transform.position = new Vector3(50f, 0f, 0f);
                Assert.IsFalse(mine.WouldClaimClick(), "boulder out of range → the verb does not claim the click");
            }
            finally
            {
                Object.DestroyImmediate(mineGo);
                Object.DestroyImmediate(rootGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(invGo);
            }
        }

        [Test]
        public void MineBoulder_WouldClaimClick_FalseWhenNoPickaxeSelected_OrRefsNull()
        {
            var invGo = new GameObject("Inventory");
            var playerGo = new GameObject("Player");
            var rootGo = new GameObject("Boulders");
            var mineGo = new GameObject("MineBoulder");
            try
            {
                var inv = invGo.AddComponent<Inventory>();
                // AXE selected (not a pickaxe) — the boulder verb must NOT claim.
                var axeSlot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.AxeId));
                inv.Model.SelectBelt(axeSlot.Value.Index);

                var boulder = new GameObject(MineBoulder.BoulderNodeName);
                boulder.transform.SetParent(rootGo.transform, false);
                boulder.transform.position = new Vector3(1f, 0f, 0f);

                var mine = mineGo.AddComponent<MineBoulder>();
                mine.inventory = inv;
                mine.player = playerGo.transform;
                mine.boulderRoot = rootGo.transform;
                mine.mineRadius = 2.4f;
                mine.InitializePoolForTest();

                Assert.IsFalse(mine.WouldClaimClick(), "an AXE selected (not a pickaxe) → the boulder verb does not claim");

                mine.inventory = null;
                Assert.IsFalse(mine.WouldClaimClick(), "null inventory → never claims (a bare rig)");
            }
            finally
            {
                Object.DestroyImmediate(mineGo);
                Object.DestroyImmediate(rootGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(invGo);
            }
        }

        // --- MineOre.WouldClaimClick: a WOOD pickaxe does NOT claim ORE (spec §5 — wood mines boulders, not iron
        //     ore). This is the arbitration side of the ADVISEMENT (the Sponsor expected wood to mine ore too). ---

        [Test]
        public void MineOre_WouldClaimClick_FalseForWoodPickaxe_TrueGateForStoneIron()
        {
            var invGo = new GameObject("Inventory");
            var playerGo = new GameObject("Player");
            var mineGo = new GameObject("MineOre");
            try
            {
                var inv = invGo.AddComponent<Inventory>();
                var mine = mineGo.AddComponent<MineOre>();
                mine.inventory = inv;
                mine.player = playerGo.transform;

                // WOOD pickaxe selected — MineOre.IsPickaxeSelected excludes wood (spec §5), so WouldClaimClick
                // short-circuits false BEFORE any node lookup: a wood pickaxe never claims an ore click.
                var woodSlot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.PickaxeWoodId));
                inv.Model.SelectBelt(woodSlot.Value.Index);
                Assert.IsFalse(MineOre.IsPickaxeSelected(inv), "wood pickaxe is NOT an ore-mining tool (spec §5 — ADVISEMENT)");
                Assert.IsFalse(mine.WouldClaimClick(), "wood pickaxe → MineOre never claims the click (ore needs stone/iron)");

                // STONE pickaxe selected — the ORE gate accepts it (the claim then also needs a node in range,
                // which a bare rig has none of, so WouldClaimClick stays false — but the SELECT gate flips true).
                var stoneSlot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.PickaxeStoneId));
                inv.Model.SelectBelt(stoneSlot.Value.Index);
                Assert.IsTrue(MineOre.IsPickaxeSelected(inv), "stone pickaxe IS an ore-mining tool (spec §5)");
            }
            finally
            {
                Object.DestroyImmediate(mineGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(invGo);
            }
        }
    }
}
