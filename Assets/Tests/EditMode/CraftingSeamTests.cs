using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the material-cost CRAFT seam + recipe data (ticket 86camz9uz / crafting-redesign
    /// ①). Pure-logic (no scene) coverage of: the all-or-nothing debit → grant contract (InventoryModel /
    /// Inventory façade), the recipe row-state + tier-unlock truth-tables, the wood-tier ids resolving in
    /// BOTH catalogs, and the CraftAxe-retirement regression guard (the free-mint façade must NOT be stranded
    /// by the CraftSpot retirement, so chop/held-axe stay green).
    /// </summary>
    public class CraftingSeamTests
    {
        private ItemCatalog _cat;

        [SetUp]
        public void SetUp()
        {
            _cat = ScriptableObject.CreateInstance<ItemCatalog>();
            _cat.BuildDefaults();
        }

        private static RecipeCost[] Wood(int n) => new[] { new RecipeCost(ItemCatalog.WoodId, n) };

        // === All-or-nothing debit → grant (the load-bearing craft contract) ===

        [Test]
        public void Craft_ShortMats_NoCraft_NoDebit()
        {
            var model = new InventoryModel();
            model.AddItem(_cat.ById(ItemCatalog.WoodId), 2); // only 2 wood
            var axeWood = _cat.ById(ItemCatalog.AxeWoodId);

            bool ok = model.TryCraft(Wood(3), axeWood); // wood axe costs 3
            Assert.IsFalse(ok, "can't afford (2 < 3) → no craft");
            Assert.AreEqual(2, model.CountItem(ItemCatalog.WoodId), "short craft debits NOTHING (all-or-nothing)");
            Assert.IsFalse(model.OwnsItem(ItemCatalog.AxeWoodId), "no output granted on a failed craft");
        }

        [Test]
        public void Craft_Affordable_DebitsInputs_GrantsOutputToBelt()
        {
            var model = new InventoryModel();
            model.AddItem(_cat.ById(ItemCatalog.WoodId), 5);
            var axeWood = _cat.ById(ItemCatalog.AxeWoodId);

            bool ok = model.TryCraft(Wood(3), axeWood);
            Assert.IsTrue(ok, "5 wood affords the 3-wood axe → craft");
            Assert.AreEqual(2, model.CountItem(ItemCatalog.WoodId), "exactly 3 wood debited");
            Assert.IsTrue(model.OwnsItem(ItemCatalog.AxeWoodId), "the wood axe is granted");
            Assert.IsTrue(model.IsSelectedBeltItem(ItemCatalog.AxeWoodId),
                "the granted tool lands on the belt (auto-placed slot 0, the default selection)");
        }

        [Test]
        public void Craft_MultiCostLine_AllOrNothing_AcrossBothInputs()
        {
            // A synthetic 2-line recipe (wood + stone) — the stone-tier shape — proves the pre-check spans ALL
            // lines: short on line 2 debits NOTHING from line 1.
            var costs = new[] { new RecipeCost(ItemCatalog.WoodId, 2), new RecipeCost(ItemCatalog.StoneId, 3) };
            var output = _cat.ById(ItemCatalog.PickaxeWoodId);

            var model = new InventoryModel();
            model.AddItem(_cat.ById(ItemCatalog.WoodId), 5);
            model.AddItem(_cat.ById(ItemCatalog.StoneId), 1); // short on stone

            Assert.IsFalse(model.CanAfford(costs), "short on stone → not affordable across all lines");
            Assert.IsFalse(model.TryCraft(costs, output), "→ no craft");
            Assert.AreEqual(5, model.CountItem(ItemCatalog.WoodId), "line-1 wood must NOT be debited when line-2 is short");
            Assert.AreEqual(1, model.CountItem(ItemCatalog.StoneId), "stone untouched too");

            model.AddItem(_cat.ById(ItemCatalog.StoneId), 2); // now 3 stone
            Assert.IsTrue(model.TryCraft(costs, output), "now affordable → craft");
            Assert.AreEqual(3, model.CountItem(ItemCatalog.WoodId), "2 wood debited");
            Assert.AreEqual(0, model.CountItem(ItemCatalog.StoneId), "3 stone debited");
            Assert.IsTrue(model.OwnsItem(ItemCatalog.PickaxeWoodId), "output granted");
        }

        [Test]
        public void Craft_NullOutput_IsRefused()
        {
            var model = new InventoryModel();
            model.AddItem(_cat.ById(ItemCatalog.WoodId), 10);
            Assert.IsFalse(model.TryCraft(Wood(1), null), "a null output def never crafts (no debit)");
            Assert.AreEqual(10, model.CountItem(ItemCatalog.WoodId), "no debit on a null-output refusal");
        }

        // === Façade (Inventory.TryCraft resolves the recipe's ids via the catalog) ===

        [Test]
        public void Facade_TryCraft_WoodAxeRecipe_DebitsAndGrants()
        {
            var go = new GameObject("Inv");
            var inv = go.AddComponent<Inventory>();
            inv.AddWood(5);
            var recipe = FindRecipe(CraftTier.Wood, CraftTool.Axe);

            Assert.IsTrue(inv.CanAfford(recipe), "5 wood affords the wood axe");
            Assert.IsTrue(inv.TryCraft(recipe), "façade resolves the output id + crafts");
            Assert.AreEqual(5 - CraftingRecipeBook.WoodAxeWood, inv.WoodCount, "wood debited by the recipe cost");
            Assert.IsTrue(inv.Model.OwnsItem(ItemCatalog.AxeWoodId), "the wood axe is on the belt");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Facade_TryCraft_PlaceholderRecipe_IsRefused()
        {
            var go = new GameObject("Inv");
            var inv = go.AddComponent<Inventory>();
            inv.AddWood(99);
            var stoneAxe = FindRecipe(CraftTier.Stone, CraftTool.Axe); // ① Locked placeholder
            Assert.IsTrue(stoneAxe.Placeholder, "the STONE axe row is a ① placeholder");
            Assert.IsFalse(inv.CanAfford(stoneAxe), "a placeholder is never affordable-to-craft");
            Assert.IsFalse(inv.TryCraft(stoneAxe), "a placeholder recipe never crafts in ① (② wires it live)");
            Object.DestroyImmediate(go);
        }

        // === Row-state truth-table (spec §3) ===

        [Test]
        public void ResolveRowState_TruthTable()
        {
            Assert.AreEqual(RecipeRowState.Locked, Recipe.ResolveRowState(placeholder: true, tierUnlocked: true, affordable: true),
                "a placeholder is Locked regardless");
            Assert.AreEqual(RecipeRowState.Locked, Recipe.ResolveRowState(false, tierUnlocked: false, affordable: true),
                "a locked tier is Locked even if affordable");
            Assert.AreEqual(RecipeRowState.Craftable, Recipe.ResolveRowState(false, true, affordable: true),
                "unlocked + affordable = Craftable");
            Assert.AreEqual(RecipeRowState.Unaffordable, Recipe.ResolveRowState(false, true, affordable: false),
                "unlocked + short = Unaffordable");
        }

        // === Tier-unlock truth-table (spec §7-C) ===

        [Test]
        public void IsTierUnlocked_TruthTable()
        {
            var none = new CraftingUnlockState(false, false, false);
            var placed = new CraftingUnlockState(true, false, false);
            var woodPick = new CraftingUnlockState(true, true, false);
            var ingot = new CraftingUnlockState(true, true, true);

            Assert.IsFalse(CraftingRecipeBook.IsTierUnlocked(CraftTier.Wood, none), "WOOD locked until a table is placed");
            Assert.IsTrue(CraftingRecipeBook.IsTierUnlocked(CraftTier.Wood, placed), "WOOD unlocks on table placed");
            Assert.IsFalse(CraftingRecipeBook.IsTierUnlocked(CraftTier.Stone, placed), "STONE needs a wood pickaxe");
            Assert.IsTrue(CraftingRecipeBook.IsTierUnlocked(CraftTier.Stone, woodPick), "STONE unlocks on wood-pickaxe owned");
            Assert.IsFalse(CraftingRecipeBook.IsTierUnlocked(CraftTier.Iron, woodPick), "IRON needs an ingot");
            Assert.IsTrue(CraftingRecipeBook.IsTierUnlocked(CraftTier.Iron, ingot), "IRON unlocks on ingot owned");
        }

        // === Wood ids resolve in BOTH catalogs; shipped ids stay stable (§6b) ===

        [Test]
        public void WoodTierIds_ResolveInBothCatalogs_AsTools()
        {
            var weap = ScriptableObject.CreateInstance<WeaponCatalog>();
            weap.BuildDefaults();
            string[] woodIds =
            {
                ItemCatalog.AxeWoodId, ItemCatalog.PickaxeWoodId, ItemCatalog.SpearWoodId,
                ItemCatalog.DaggerWoodId, ItemCatalog.SwordWoodId,
            };
            foreach (var id in woodIds)
            {
                var item = _cat.ById(id);
                Assert.IsNotNull(item, $"'{id}' must resolve in ItemCatalog");
                Assert.AreEqual(ItemKind.Tool, item.Kind, $"'{id}' is a belt-eligible Tool");
                Assert.IsNotNull(weap.ById(id), $"'{id}' must resolve in WeaponCatalog (both lanes share the id)");
            }
        }

        [Test]
        public void ShippedIds_StayStable_NotMigrated()
        {
            // §6b — migrating the shipped stone ids would break the chop gate / held-axe / pickups / combat POC.
            Assert.IsNotNull(_cat.ById(ItemCatalog.AxeId), "shipped 'axe' id stays stable");
            Assert.IsNotNull(_cat.ById(ItemCatalog.SpearId), "shipped 'spear' id stays stable");
            Assert.IsNotNull(_cat.ById(ItemCatalog.PickaxeStoneId), "shipped 'pickaxe_stone' id stays stable");
            Assert.IsNotNull(_cat.ById(ItemCatalog.PickaxeIronId), "shipped 'pickaxe_iron' id stays stable");
        }

        // === Recipe book shape ===

        [Test]
        public void RecipeBook_Has15Rows_5WoodLive_10Placeholder_WoodOutputsResolve()
        {
            var book = CraftingRecipeBook.BuildDefaults();
            Assert.AreEqual(15, book.Count, "3 tiers × 5 tools");
            int wood = 0, placeholders = 0;
            foreach (var r in book)
            {
                if (r.Tier == CraftTier.Wood)
                {
                    wood++;
                    Assert.IsFalse(r.Placeholder, "WOOD rows are LIVE in ①");
                    Assert.IsNotNull(_cat.ById(r.OutputItemId), $"WOOD recipe output '{r.OutputItemId}' must resolve");
                    Assert.Greater(r.Costs.Length, 0, "a WOOD recipe has a cost");
                }
                else
                {
                    Assert.IsTrue(r.Placeholder, "STONE/IRON rows are Locked placeholders in ①");
                    placeholders++;
                }
            }
            Assert.AreEqual(5, wood, "5 WOOD rows");
            Assert.AreEqual(10, placeholders, "10 STONE/IRON placeholder rows");
        }

        // === REGRESSION GUARD: CraftAxe retirement must NOT strand a caller (chop/held-axe stay green) ===

        [Test]
        public void CraftAxe_Facade_StillPlacesTheStoneAxe_ChopGatePathIntact()
        {
            // 86camz9uz ① retires the free-mint CraftAxe's PRODUCTION caller (CraftSpot), NOT the façade
            // method — it stays for the legacy callers/tests. This guard reds if CraftAxe stops placing the
            // stone "axe" as the selected belt item (the exact predicate ChopTree.ShouldChopOnClick +
            // HeldAxe gate on: Inventory.IsAxeSelectedInBelt). If this breaks, chop + held-axe are stranded.
            var go = new GameObject("Inv");
            var inv = go.AddComponent<Inventory>();
            Assert.IsFalse(inv.HasAxe, "starts with no axe");
            Assert.IsTrue(inv.CraftAxe(), "CraftAxe still places the stone axe (one-shot true)");
            Assert.IsTrue(inv.HasAxe, "the stone axe is owned");
            Assert.IsTrue(inv.IsAxeSelectedInBelt,
                "the stone axe is the SELECTED belt item — the chop gate + held-axe path is NOT stranded");
            Assert.IsFalse(inv.CraftAxe(), "crafting again is a no-op (already owned) — the free-mint one-shot is intact");
            Object.DestroyImmediate(go);
        }

        private static Recipe FindRecipe(CraftTier tier, CraftTool tool)
        {
            foreach (var r in CraftingRecipeBook.BuildDefaults())
                if (r.Tier == tier && r.Tool == tool) return r;
            return null;
        }
    }
}
