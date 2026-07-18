using System;
using System.Collections.Generic;

namespace FarHorizon
{
    /// <summary>The three crafting tiers at the table (ticket 86camz9uz / crafting-redesign — spec §3/§5).
    /// WOOD is the crudest pre-stone rung (all-wood tools); STONE + IRON are the upgrades. The menu groups
    /// rows by this tier; ① ships WOOD live + STONE/IRON as Locked placeholders (②/③ flip them live).</summary>
    public enum CraftTier
    {
        Wood,
        Stone,
        Iron,
    }

    /// <summary>The five tool TYPES every tier offers (spec §3 — axe / pickaxe / spear / dagger / sword). The
    /// menu orders rows by this within each tier so the ladder reads consistently across tiers.</summary>
    public enum CraftTool
    {
        Axe,
        Pickaxe,
        Spear,
        Dagger,
        Sword,
    }

    /// <summary>The display state of a recipe ROW in the menu (spec §3 "State per row"). A pure enum so the
    /// row-state truth-table is unit-testable (<see cref="Recipe.ResolveRowState"/>).</summary>
    public enum RecipeRowState
    {
        /// <summary>Tier unlocked AND affordable — the row is active; click crafts it.</summary>
        Craftable,
        /// <summary>Tier unlocked but materials short — greyed, shows the cost + what's missing.</summary>
        Unaffordable,
        /// <summary>Tier not yet unlocked (or a ①-Locked placeholder) — greyed, shows the unlock hint.</summary>
        Locked,
    }

    /// <summary>ONE input line of a recipe: an item id + the amount the craft debits (all-or-nothing). A
    /// plain readonly struct (no per-row alloc) — the recipe carries an array of these.</summary>
    [Serializable]
    public struct RecipeCost
    {
        public readonly string ItemId;
        public readonly int Amount;
        public RecipeCost(string itemId, int amount) { ItemId = itemId; Amount = amount; }
    }

    /// <summary>
    /// A single crafting recipe — a DATA ROW carrying an explicit <see cref="OutputItemId"/> (spec §6b, the
    /// lowest-regression scheme: the menu maps a (tier, tool) cell → whatever the live id is; shipped ids stay
    /// stable). The craft seam (<see cref="InventoryModel.TryCraft"/>) debits <see cref="Costs"/> all-or-
    /// nothing then grants the resolved output tool via <see cref="InventoryModel.AddToolToBelt"/> — it never
    /// extends the free-mint <c>CraftAxe</c> path.
    ///
    /// <see cref="Placeholder"/> marks the STONE/IRON rows ① ships as Locked (their material sources —
    /// boulder-stone, iron ingots — don't exist yet); ②/③ clear it as those rows go live. A placeholder row
    /// carries its intended future id + costs for DISPLAY only; the craft seam refuses it.
    /// </summary>
    [Serializable]
    public sealed class Recipe
    {
        public readonly CraftTier Tier;
        public readonly CraftTool Tool;
        /// <summary>The id of the tool/weapon this recipe grants (explicit — spec §6b). Empty is never crafted.</summary>
        public readonly string OutputItemId;
        /// <summary>Player-facing row label (e.g. "Wood Axe", "Dagger"). Uses the "dagger" term (§6a).</summary>
        public readonly string DisplayName;
        public readonly RecipeCost[] Costs;
        /// <summary>True for a row that ① ships as Locked regardless of unlock/affordability (STONE/IRON). ②/③
        /// mint the live recipe (Placeholder=false) when the tier's material sources exist.</summary>
        public readonly bool Placeholder;

        public Recipe(CraftTier tier, CraftTool tool, string outputItemId, string displayName,
                      RecipeCost[] costs, bool placeholder)
        {
            Tier = tier;
            Tool = tool;
            OutputItemId = outputItemId;
            DisplayName = displayName;
            Costs = costs ?? Array.Empty<RecipeCost>();
            Placeholder = placeholder;
        }

        /// <summary>
        /// PURE row-state truth-table (spec §3 — the unit-testable core the menu paints from): a Placeholder or
        /// tier-locked row is <see cref="RecipeRowState.Locked"/>; an unlocked row is
        /// <see cref="RecipeRowState.Craftable"/> when affordable, else <see cref="RecipeRowState.Unaffordable"/>.
        /// Static + dependency-free so the EditMode guard pins every cell without a scene or a live model.
        /// </summary>
        public static RecipeRowState ResolveRowState(bool placeholder, bool tierUnlocked, bool affordable)
        {
            if (placeholder || !tierUnlocked) return RecipeRowState.Locked;
            return affordable ? RecipeRowState.Craftable : RecipeRowState.Unaffordable;
        }
    }

    /// <summary>The inputs the tier-unlock model reads (spec §7-C). A plain value carrier so the unlock
    /// truth-table (<see cref="CraftingRecipeBook.IsTierUnlocked"/>) is testable without game state.</summary>
    public struct CraftingUnlockState
    {
        /// <summary>Has the player placed a crafting table? (WOOD unlocks on this.)</summary>
        public bool TablePlaced;
        /// <summary>Has the player ever obtained a WOOD pickaxe? (STONE unlocks on this — §7-C.)</summary>
        public bool OwnsWoodPickaxe;
        /// <summary>Has the player ever obtained an iron ingot? (IRON unlocks on this — §7-C.)</summary>
        public bool OwnsIronIngot;

        public CraftingUnlockState(bool tablePlaced, bool ownsWoodPickaxe, bool ownsIronIngot)
        {
            TablePlaced = tablePlaced;
            OwnsWoodPickaxe = ownsWoodPickaxe;
            OwnsIronIngot = ownsIronIngot;
        }
    }

    /// <summary>
    /// The canonical recipe SET (ticket 86camz9uz ① — spec §5). Built in code (no .asset round-trip — the
    /// ItemCatalog/WeaponCatalog precedent) so the menu + tests share ONE source. 15 rows = 3 tiers × 5 tools;
    /// ① mints WOOD live + STONE/IRON as Locked placeholders. Costs are the §5 predictions — every one is a
    /// 🎚️ <c>default X — Sponsor-soak tunes</c> (SettingsCatalog registration of the costs is a mechanical
    /// follow-up, matching how ForgePlacement.woodCost/stoneCost ship as plain fields).
    /// </summary>
    public static class CraftingRecipeBook
    {
        // === Structure costs (§5) — the crafting TABLE (this ticket's place-to-build). 🎚️ default — Sponsor-soak tunes. ===
        public const int TableWoodCost = 5;
        public const int TableStoneCost = 3;

        // === WOOD-tier tool costs (§5) — all wood, the cheap bootstrap. 🎚️ default — Sponsor-soak tunes. ===
        public const int WoodAxeWood = 3;
        public const int WoodPickaxeWood = 3;
        public const int WoodSpearWood = 2;
        public const int WoodDaggerWood = 2;
        public const int WoodSwordWood = 4;

        // === STONE-tier tool costs (§5) — LIVE in ② (ticket 86camz9v7). 🎚️ default — Sponsor-soak tunes. ===
        public const int StoneAxeWood = 3, StoneAxeStone = 3;
        public const int StonePickaxeWood = 3, StonePickaxeStone = 3;
        public const int StoneSpearWood = 2, StoneSpearStone = 2;
        public const int StoneDaggerWood = 2, StoneDaggerStone = 2;
        public const int StoneSwordWood = 4, StoneSwordStone = 4;

        // === IRON-tier tool costs (§5) — DISPLAY only in ① (Locked placeholders); ③ wires them live. ===
        public const int IronAxeWood = 2, IronAxeIngot = 3;
        public const int IronPickaxeWood = 2, IronPickaxeIngot = 3;
        public const int IronSpearWood = 2, IronSpearIngot = 2;
        public const int IronDaggerWood = 1, IronDaggerIngot = 2;
        public const int IronSwordWood = 2, IronSwordIngot = 4;

        private static RecipeCost[] Wood(int n) => new[] { new RecipeCost(ItemCatalog.WoodId, n) };
        private static RecipeCost[] WoodStone(int w, int s) =>
            new[] { new RecipeCost(ItemCatalog.WoodId, w), new RecipeCost(ItemCatalog.StoneId, s) };
        private static RecipeCost[] WoodIngot(int w, int i) =>
            new[] { new RecipeCost(ItemCatalog.WoodId, w), new RecipeCost(ItemCatalog.IronIngotId, i) };

        /// <summary>
        /// The 15 recipes, tier-grouped (WOOD → STONE → IRON), tool-ordered (axe/pickaxe/spear/dagger/sword).
        /// WOOD + STONE rows are LIVE (Placeholder=false — ① shipped WOOD, ② ticket 86camz9v7 flips STONE); IRON
        /// rows are Placeholder=true (③ wires them live once iron ingots exist). "Dagger"/"Sword" display per §6a.
        /// </summary>
        public static List<Recipe> BuildDefaults()
        {
            return new List<Recipe>
            {
                // WOOD tier — LIVE (①).
                new Recipe(CraftTier.Wood, CraftTool.Axe,     ItemCatalog.AxeWoodId,     "Wood Axe",     Wood(WoodAxeWood),     false),
                new Recipe(CraftTier.Wood, CraftTool.Pickaxe, ItemCatalog.PickaxeWoodId, "Wood Pickaxe", Wood(WoodPickaxeWood), false),
                new Recipe(CraftTier.Wood, CraftTool.Spear,   ItemCatalog.SpearWoodId,   "Wood Spear",   Wood(WoodSpearWood),   false),
                new Recipe(CraftTier.Wood, CraftTool.Dagger,  ItemCatalog.DaggerWoodId,  "Wood Dagger",  Wood(WoodDaggerWood),  false),
                new Recipe(CraftTier.Wood, CraftTool.Sword,   ItemCatalog.SwordWoodId,   "Wood Sword",   Wood(WoodSwordWood),   false),

                // STONE tier — LIVE (② — ticket 86camz9v7). Placeholder=false: boulder-stone mining (② new) is
                // the volume stone source, so these are now craftable. Shipped "axe"/"spear"/"pickaxe_stone" ids
                // stay STABLE (§6b — do NOT migrate); dagger_stone/sword_stone are the 2 NEW cells minted by ②.
                // The tier still gates on §7-C (STONE unlocks on first-wood-pickaxe-owned) via IsTierUnlocked —
                // Placeholder=false makes the row LIVE once the tier unlocks + is affordable (the menu paints it).
                new Recipe(CraftTier.Stone, CraftTool.Axe,     ItemCatalog.AxeId,          "Stone Axe",     WoodStone(StoneAxeWood, StoneAxeStone),         false),
                new Recipe(CraftTier.Stone, CraftTool.Pickaxe, ItemCatalog.PickaxeStoneId, "Stone Pickaxe", WoodStone(StonePickaxeWood, StonePickaxeStone), false),
                new Recipe(CraftTier.Stone, CraftTool.Spear,   ItemCatalog.SpearId,        "Stone Spear",   WoodStone(StoneSpearWood, StoneSpearStone),     false),
                new Recipe(CraftTier.Stone, CraftTool.Dagger,  ItemCatalog.DaggerStoneId,  "Stone Dagger",  WoodStone(StoneDaggerWood, StoneDaggerStone),   false),
                new Recipe(CraftTier.Stone, CraftTool.Sword,   ItemCatalog.SwordStoneId,   "Stone Sword",   WoodStone(StoneSwordWood, StoneSwordStone),     false),

                // IRON tier — Locked placeholders (③). Future ids per §6b: pickaxe_iron exists; axe_iron/spear_iron/
                // dagger_iron/sword_iron are minted by ③.
                new Recipe(CraftTier.Iron, CraftTool.Axe,     "axe_iron",                "Iron Axe",     WoodIngot(IronAxeWood, IronAxeIngot),         true),
                new Recipe(CraftTier.Iron, CraftTool.Pickaxe, ItemCatalog.PickaxeIronId, "Iron Pickaxe", WoodIngot(IronPickaxeWood, IronPickaxeIngot), true),
                new Recipe(CraftTier.Iron, CraftTool.Spear,   "spear_iron",              "Iron Spear",   WoodIngot(IronSpearWood, IronSpearIngot),     true),
                new Recipe(CraftTier.Iron, CraftTool.Dagger,  "dagger_iron",             "Iron Dagger",  WoodIngot(IronDaggerWood, IronDaggerIngot),   true),
                new Recipe(CraftTier.Iron, CraftTool.Sword,   "sword_iron",              "Iron Sword",   WoodIngot(IronSwordWood, IronSwordIngot),     true),
            };
        }

        /// <summary>
        /// PURE tier-unlock truth-table (spec §7-C — the accepted default): WOOD unlocks once the table is
        /// placed; STONE once the player has ever owned a WOOD pickaxe; IRON once they have ever owned an iron
        /// ingot. Static + dependency-free so the EditMode guard pins every cell. (In ① the STONE/IRON rows are
        /// additionally Placeholder=true, so they read Locked in the menu even if this returns true — ②/③ clear
        /// the placeholder as those source paths land; this predicate is the forward-looking model they use.)
        /// </summary>
        public static bool IsTierUnlocked(CraftTier tier, CraftingUnlockState state)
        {
            switch (tier)
            {
                case CraftTier.Wood: return state.TablePlaced;
                case CraftTier.Stone: return state.OwnsWoodPickaxe;
                case CraftTier.Iron: return state.OwnsIronIngot;
                default: return false;
            }
        }
    }
}
