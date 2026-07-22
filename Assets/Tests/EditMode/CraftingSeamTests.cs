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
        public void Facade_TryCraft_SyntheticPlaceholder_IsRefused()
        {
            // ③ flips IRON live, so no BUILT-IN placeholder rows remain — but the craft seam must STILL refuse a
            // Placeholder recipe (the guard that a future tier's Locked row can never mint a tool). Construct one.
            var go = new GameObject("Inv");
            var inv = go.AddComponent<Inventory>();
            inv.AddWood(99);
            var placeholder = new Recipe(CraftTier.Iron, CraftTool.Axe, ItemCatalog.AxeWoodId, "Locked",
                new[] { new RecipeCost(ItemCatalog.WoodId, 1) }, placeholder: true);
            Assert.IsFalse(inv.CanAfford(placeholder), "a placeholder is never affordable-to-craft");
            Assert.IsFalse(inv.TryCraft(placeholder), "the craft seam refuses a Placeholder recipe");
            Assert.IsFalse(inv.Model.OwnsItem(ItemCatalog.AxeWoodId), "no output granted on a refused placeholder");
            Object.DestroyImmediate(go);
        }

        // === IRON tier LIVE (③ — ticket 86camz9vh): the strip-test — craft an iron axe/sword from ingots ===

        [Test]
        public void Facade_TryCraft_IronAxe_LiveWithWoodAndIngots_DebitsAndGrants()
        {
            var go = new GameObject("Inv");
            var inv = go.AddComponent<Inventory>();
            inv.AddWood(10);
            inv.Model.AddItem(inv.Catalog.ById(ItemCatalog.IronIngotId), 10); // smelted at the forge (#292); seed directly
            var recipe = FindRecipe(CraftTier.Iron, CraftTool.Axe);
            Assert.IsFalse(recipe.Placeholder, "③ flips the IRON axe row LIVE (Placeholder=false)");
            Assert.AreEqual(ItemCatalog.AxeIronId, recipe.OutputItemId, "the iron axe mints the NEW axe_iron id");

            Assert.IsTrue(inv.CanAfford(recipe), "10 wood + 10 ingots affords the iron axe");
            Assert.IsTrue(inv.TryCraft(recipe), "the façade resolves the iron-axe output id + crafts it");
            Assert.AreEqual(10 - CraftingRecipeBook.IronAxeWood, inv.WoodCount, "wood debited by the recipe cost");
            Assert.AreEqual(10 - CraftingRecipeBook.IronAxeIngot, inv.Model.CountItem(ItemCatalog.IronIngotId),
                "iron ingots debited by the recipe cost (all-or-nothing)");
            Assert.IsTrue(inv.Model.OwnsItem(ItemCatalog.AxeIronId), "the iron axe (axe_iron) is granted to the belt");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Facade_TryCraft_IronDaggerSpearSword_NewIds_DebitAndGrant()
        {
            var go = new GameObject("Inv");
            var inv = go.AddComponent<Inventory>();
            inv.AddWood(20); inv.Model.AddItem(inv.Catalog.ById(ItemCatalog.IronIngotId), 20);

            var dagger = FindRecipe(CraftTier.Iron, CraftTool.Dagger);
            Assert.AreEqual(ItemCatalog.DaggerIronId, dagger.OutputItemId, "the iron dagger mints the NEW dagger_iron id");
            Assert.IsTrue(inv.TryCraft(dagger), "the iron dagger crafts"); Assert.IsTrue(inv.Model.OwnsItem(ItemCatalog.DaggerIronId));

            var spear = FindRecipe(CraftTier.Iron, CraftTool.Spear);
            Assert.AreEqual(ItemCatalog.SpearIronId, spear.OutputItemId, "the iron spear mints the NEW spear_iron id");
            Assert.IsTrue(inv.TryCraft(spear), "the iron spear crafts"); Assert.IsTrue(inv.Model.OwnsItem(ItemCatalog.SpearIronId));

            var sword = FindRecipe(CraftTier.Iron, CraftTool.Sword);
            Assert.AreEqual(ItemCatalog.SwordIronId, sword.OutputItemId, "the iron sword mints the NEW sword_iron id");
            Assert.IsTrue(inv.TryCraft(sword), "the iron sword crafts"); Assert.IsTrue(inv.Model.OwnsItem(ItemCatalog.SwordIronId));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void IronTierNewIds_ResolveInBothCatalogs_AsTools_StrongerThanStone()
        {
            var weap = ScriptableObject.CreateInstance<WeaponCatalog>();
            weap.BuildDefaults();
            // The 4 NEW iron cells (pickaxe_iron already shipped I-0) resolve in BOTH catalogs as belt Tools.
            foreach (var id in new[] { ItemCatalog.AxeIronId, ItemCatalog.SpearIronId, ItemCatalog.DaggerIronId, ItemCatalog.SwordIronId })
            {
                var item = _cat.ById(id);
                Assert.IsNotNull(item, $"'{id}' must resolve in ItemCatalog");
                Assert.AreEqual(ItemKind.Tool, item.Kind, $"'{id}' is a belt-eligible Tool");
                Assert.IsNotNull(weap.ById(id), $"'{id}' must resolve in WeaponCatalog (both lanes share the id)");
            }
            // Iron is the forged UPGRADE — monotonically stronger than the stone tier (§7-B, a soakable read).
            Assert.Greater(weap.ById(ItemCatalog.AxeIronId).Damage, weap.ById(WeaponCatalog.AxeId).Damage,
                "iron axe out-damages the stone axe (the forged upgrade)");
            Assert.Greater(weap.ById(ItemCatalog.SwordIronId).Damage, weap.ById(WeaponCatalog.SwordStoneId).Damage,
                "iron sword out-damages the stone sword");
            Object.DestroyImmediate(weap);
        }

        // === STONE tier LIVE (② — ticket 86camz9v7): the strip-test — craft a stone axe at the table ===

        [Test]
        public void Facade_TryCraft_StoneAxeRecipe_DebitsAndGrants()
        {
            var go = new GameObject("Inv");
            var inv = go.AddComponent<Inventory>();
            inv.AddWood(10);
            inv.Model.AddItem(inv.Catalog.ById(ItemCatalog.StoneId), 10); // stone is mined from boulders (②); seed it directly
            var recipe = FindRecipe(CraftTier.Stone, CraftTool.Axe);
            Assert.IsFalse(recipe.Placeholder, "② flips the STONE axe row LIVE (Placeholder=false)");

            Assert.IsTrue(inv.CanAfford(recipe), "10 wood + 10 stone affords the stone axe");
            Assert.IsTrue(inv.TryCraft(recipe), "the façade resolves the stone-axe output id + crafts it");
            Assert.AreEqual(10 - CraftingRecipeBook.StoneAxeWood, inv.WoodCount, "wood debited by the recipe cost");
            Assert.AreEqual(10 - CraftingRecipeBook.StoneAxeStone, inv.StoneCount, "stone debited by the recipe cost");
            Assert.IsTrue(inv.Model.OwnsItem(ItemCatalog.AxeId), "the stone 'axe' (shipped id, §6b) is granted to the belt");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Facade_TryCraft_StoneDaggerAndSword_NewIds_DebitAndGrant()
        {
            var go = new GameObject("Inv");
            var inv = go.AddComponent<Inventory>();
            inv.AddWood(20); inv.Model.AddItem(inv.Catalog.ById(ItemCatalog.StoneId), 20);

            var dagger = FindRecipe(CraftTier.Stone, CraftTool.Dagger);
            Assert.IsFalse(dagger.Placeholder, "② flips the stone dagger LIVE");
            Assert.AreEqual(ItemCatalog.DaggerStoneId, dagger.OutputItemId, "the stone dagger mints the NEW dagger_stone id");
            Assert.IsTrue(inv.TryCraft(dagger), "the stone dagger crafts");
            Assert.IsTrue(inv.Model.OwnsItem(ItemCatalog.DaggerStoneId), "dagger_stone granted");

            var sword = FindRecipe(CraftTier.Stone, CraftTool.Sword);
            Assert.AreEqual(ItemCatalog.SwordStoneId, sword.OutputItemId, "the stone sword mints the NEW sword_stone id");
            Assert.IsTrue(inv.TryCraft(sword), "the stone sword crafts");
            Assert.IsTrue(inv.Model.OwnsItem(ItemCatalog.SwordStoneId), "sword_stone granted");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void StoneTierNewIds_ResolveInBothCatalogs_AsTools()
        {
            var weap = ScriptableObject.CreateInstance<WeaponCatalog>();
            weap.BuildDefaults();
            foreach (var id in new[] { ItemCatalog.DaggerStoneId, ItemCatalog.SwordStoneId })
            {
                var item = _cat.ById(id);
                Assert.IsNotNull(item, $"'{id}' must resolve in ItemCatalog");
                Assert.AreEqual(ItemKind.Tool, item.Kind, $"'{id}' is a belt-eligible Tool");
                Assert.IsNotNull(weap.ById(id), $"'{id}' must resolve in WeaponCatalog (both lanes share the id)");
            }
        }

        // === NIT #1 (folded — Drew's PR #294 review, comment 4919859554): the grant-first LOSS-FREE abort ===
        // TryCraft grants the output FIRST; if the inventory is COMPLETELY full so the grant cannot land, the craft
        // aborts with NO debit (materials are never spent for a tool that had nowhere to go).
        [Test]
        public void Craft_FullInventory_NoRoom_AbortsWithNoDebit_NoGrant()
        {
            // 1 belt slot + 2 inventory slots — tiny grid so we can fill it exactly.
            var model = new InventoryModel(inventorySlots: 2, beltSlots: 1);
            // Fill the single belt slot with a tool (so a granted tool can't land there).
            var beltTool = model.AddToolToBelt(_cat.ById(ItemCatalog.AxeId));
            Assert.IsTrue(beltTool.HasValue, "precondition: the belt's one slot is filled with a tool");
            // Fill BOTH inventory slots with the recipe's inputs (wood + stone) so the pack is completely full AND
            // the craft is affordable — the ONLY thing that can fail is the grant having nowhere to land.
            Assert.AreEqual(0, model.AddItem(_cat.ById(ItemCatalog.WoodId), 3), "wood fills inventory slot 0");
            Assert.AreEqual(0, model.AddItem(_cat.ById(ItemCatalog.StoneId), 3), "stone fills inventory slot 1");

            var costs = new[] { new RecipeCost(ItemCatalog.WoodId, 3), new RecipeCost(ItemCatalog.StoneId, 3) };
            var output = _cat.ById(ItemCatalog.PickaxeStoneId); // a Tool → needs a free belt/inventory slot to land

            Assert.IsTrue(model.CanAfford(costs), "precondition: the recipe IS affordable (3 wood + 3 stone held)");
            bool ok = model.TryCraft(costs, output);

            Assert.IsFalse(ok, "a full inventory (no room for the granted tool) → the craft ABORTS");
            Assert.AreEqual(3, model.CountItem(ItemCatalog.WoodId), "LOSS-FREE: no wood debited on the aborted craft");
            Assert.AreEqual(3, model.CountItem(ItemCatalog.StoneId), "LOSS-FREE: no stone debited on the aborted craft");
            Assert.IsFalse(model.OwnsItem(ItemCatalog.PickaxeStoneId), "no output granted on the aborted craft");
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

        // === F6 row-visibility truth-table (unlocked-only by default + show-locked toggle) ===

        [Test]
        public void IsRowVisible_UnlockedOnly_ByDefault_ShowLockedReveals()
        {
            // Default (toggle OFF): Craftable + Unaffordable (both UNLOCKED) show; Locked hides.
            Assert.IsTrue(CraftingMenuUI.IsRowVisible(RecipeRowState.Craftable, showLocked: false),
                "a craftable row is always visible");
            Assert.IsTrue(CraftingMenuUI.IsRowVisible(RecipeRowState.Unaffordable, showLocked: false),
                "an unlocked-but-short row still shows (it's unlocked)");
            Assert.IsFalse(CraftingMenuUI.IsRowVisible(RecipeRowState.Locked, showLocked: false),
                "F6: Locked placeholder rows are HIDDEN by default (not a wall of greyed rows)");

            // Toggle ON: everything shows (the ladder is discoverable on demand).
            Assert.IsTrue(CraftingMenuUI.IsRowVisible(RecipeRowState.Locked, showLocked: true),
                "'Show locked' reveals the Locked ladder");
            Assert.IsTrue(CraftingMenuUI.IsRowVisible(RecipeRowState.Craftable, showLocked: true));
            Assert.IsTrue(CraftingMenuUI.IsRowVisible(RecipeRowState.Unaffordable, showLocked: true));
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
        public void RecipeBook_Has15Rows_AllLive_OutputsResolveInBothCatalogs()
        {
            // ① shipped WOOD live; ② flipped STONE live; ③ (ticket 86camz9vh) flips IRON live → ALL 15 live, 0 placeholders.
            var weap = ScriptableObject.CreateInstance<WeaponCatalog>();
            weap.BuildDefaults();
            var book = CraftingRecipeBook.BuildDefaults();
            Assert.AreEqual(15, book.Count, "3 tiers × 5 tools");
            int live = 0;
            foreach (var r in book)
            {
                Assert.IsFalse(r.Placeholder, $"③ leaves NO placeholder rows — {r.Tier} {r.Tool} must be LIVE");
                live++;
                Assert.IsNotNull(_cat.ById(r.OutputItemId),
                    $"a live {r.Tier} recipe output '{r.OutputItemId}' must resolve in the ItemCatalog");
                Assert.IsNotNull(weap.ById(r.OutputItemId),
                    $"a live {r.Tier} recipe output '{r.OutputItemId}' must resolve in the WeaponCatalog (both lanes)");
                Assert.Greater(r.Costs.Length, 0, "a live recipe has a cost");
            }
            Assert.AreEqual(15, live, "all 15 rows LIVE (5 WOOD + 5 STONE + 5 IRON)");
            Object.DestroyImmediate(weap);
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
