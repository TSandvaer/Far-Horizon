using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The inventory component — now a thin FAÇADE over the slot/stack <see cref="InventoryModel"/>
    /// (ticket 86caa4bya; supersedes the U2-2 thin ledger 86ca8bdaq). The model is the real owner of the
    /// inventory grid + belt hotbar; this MonoBehaviour is the SCENE-SERIALIZED host that wires it into
    /// Boot.unity, exposes the model to the UI, AND preserves the FULL legacy ledger surface so every
    /// existing caller stays green (item-model contract §7 "migration seam — no stranded callers").
    ///
    /// === Legacy surface preserved VERBATIM (contract §7) ===
    /// The whole U2 ledger API maps onto the model with NO behavior change for the callers:
    ///   bool  HasAxe        -> the model OWNS an axe in any slot (CountItem("axe") > 0).
    ///   int   WoodCount     -> summed Count of all "wood" stacks across inventory + belt.
    ///   event Action Changed-> forwarded from the model's Changed (HUD subscribes, never polls).
    ///   bool  CraftAxe()    -> mint + auto-place an axe TOOL on the belt (idempotent; one-shot true).
    ///   int   AddWood(int)  -> AddItem(woodDef, n) (the chop shim until 86caa4c5c switches to AddItem).
    ///   bool  SpendWood(int)-> all-or-nothing debit of "wood" across stacks (the campfire build gate).
    /// Callers unchanged: SurvivalHud / ChopTree / StumpAxe / HeldAxe / CastawayFingerCurl /
    /// CampfirePlacement / CraftSpot + the *VerifyCapture probes + InventoryTests.
    ///
    /// === New slot-model surface (AC1-AC7) ===
    ///   <see cref="Model"/>             — the InventoryModel (slots/belt/selection); the UI binds to it.
    ///   <see cref="Catalog"/>           — the ItemDef lookup (axe/wood/stone/berry).
    ///   <see cref="PickUpAxe"/>         — AC3 axe pickup -> auto-placed in belt slot 1.
    ///   <see cref="IsAxeSelectedInBelt"/> — AC4 held-axe show/hide query (axe IS the selected belt item).
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// Serialized into Boot.unity editor-time (BootstrapProject), NOT added at Awake. The model + catalog
    /// are pure C# built in <see cref="EnsureModel"/> (called from Awake AND lazily by every accessor so
    /// the legacy EditMode tests, which AddComponent then call immediately, see a ready model). The
    /// canonical defs are built in code (catalog.BuildDefaults) so no .asset wiring is required for the
    /// PoC; an authored ItemCatalog.asset can be assigned later without changing this surface.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Header("Slot counts (AC1/AC2 — adjustable later via the settings panel registry, follow-up)")]
        [SerializeField, Tooltip("Inventory grid slot count (AC1 default 20).")]
        private int _inventorySlots = InventoryModel.DefaultInventorySlots;
        [SerializeField, Tooltip("Belt hotbar slot count (AC2 default 5).")]
        private int _beltSlots = InventoryModel.DefaultBeltSlots;

        [SerializeField,
         Tooltip("Optional authored ItemCatalog asset. If unset, the catalog is built in code at Awake " +
                 "with the canonical defs (axe/wood/stone/berry/water) — no .asset wiring required.")]
        private ItemCatalog _catalogAsset;

        private InventoryModel _model;
        private ItemCatalog _catalog;

        /// <summary>The slot/stack model (the UI binds to this). Built lazily so EditMode tests that
        /// AddComponent then call immediately see a ready model.</summary>
        public InventoryModel Model { get { EnsureModel(); return _model; } }

        /// <summary>The item catalog (axe/wood/stone/berry lookup) — the resource-pickup seam.</summary>
        public ItemCatalog Catalog { get { EnsureModel(); return _catalog; } }

        /// <summary>Fires whenever the model changes (craft / add / move / select / spend). The HUD + UI
        /// subscribe and never poll — same contract as the legacy ledger's Changed.</summary>
        public event Action Changed;

        void Awake() => EnsureModel();

        private void EnsureModel()
        {
            if (_model != null) return;

            // Catalog: prefer an authored asset; otherwise build the canonical defs in code.
            if (_catalogAsset != null && _catalogAsset.All != null && _catalogAsset.All.Count > 0)
            {
                _catalog = _catalogAsset;
            }
            else
            {
                _catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                _catalog.BuildDefaults();
            }

            _model = new InventoryModel(_inventorySlots, _beltSlots);
            _model.Changed += OnModelChanged;
        }

        private void OnModelChanged() => Changed?.Invoke();

        // ============================================================================================
        // SLOT-COUNT authoring surface (ticket 86cabfa4e — the #90 AC1/AC2 settings-registration follow-up).
        // The slot counts are CONSTRUCTION-TIME inputs to InventoryModel (its grid + belt arrays are readonly,
        // sized once in the ctor) — NOT live fields. So a dev-console "set the count" must REBUILD the model for
        // the change to take effect; a bare field write would be a dead knob. The dev-console (settings panel,
        // 86caa4bqp) is the only caller; this is a dev-tool re-size, NOT a player-facing live resize that
        // preserves contents. No gameplay-loop change: add/stack/move/select all stay byte-identical.
        // ============================================================================================

        /// <summary>The authoring inventory-grid slot count (the count the model is built from; default 20). The
        /// dev-console `inventory slots` setting reads this.</summary>
        public int InventorySlotCount => _inventorySlots;

        /// <summary>The authoring belt-hotbar slot count (the count the model is built from; default 5). The
        /// dev-console `belt slots` setting reads this.</summary>
        public int BeltSlotCount => _beltSlots;

        /// <summary>
        /// Set the inventory-grid slot count and REBUILD the model so the change takes effect (the model's grid
        /// array is readonly, sized at construction). Dev-console only (`inventory slots` setting, 86cabfa4e AC1):
        /// a re-size that does NOT preserve contents — acceptable for a dev tool. A no-op (no rebuild, no Changed)
        /// when the count is unchanged. Clamps to ≥1 (the model also clamps, but guard here so the authoring field
        /// is honest).
        /// </summary>
        public void SetInventorySlotCount(int count)
        {
            count = Mathf.Max(1, count);
            if (count == _inventorySlots) return;
            _inventorySlots = count;
            RebuildModel();
        }

        /// <summary>
        /// Set the belt-hotbar slot count and REBUILD the model so the change takes effect (the belt array is
        /// readonly, sized at construction). Dev-console only (`belt slots` setting, 86cabfa4e AC2). A no-op when
        /// unchanged. Clamps to ≥1.
        /// </summary>
        public void SetBeltSlotCount(int count)
        {
            count = Mathf.Max(1, count);
            if (count == _beltSlots) return;
            _beltSlots = count;
            RebuildModel();
        }

        /// <summary>
        /// Re-construct the InventoryModel from the current authoring counts (after a dev-console slot-count
        /// change). Detaches the old model's Changed handler, builds a fresh empty model on the SAME catalog, and
        /// fires Changed so the UI/HUD rebinds to the new slot grid. Empties the inventory (a dev re-size, not a
        /// content-preserving live resize — see the section note).
        /// </summary>
        private void RebuildModel()
        {
            if (_model != null) _model.Changed -= OnModelChanged;
            _model = new InventoryModel(_inventorySlots, _beltSlots);
            _model.Changed += OnModelChanged;
            Changed?.Invoke();
        }

        // ============================================================================================
        // NEW slot-model surface (AC1-AC7).
        // ============================================================================================

        /// <summary>
        /// AC3 — pick up the axe: add the axe TOOL and auto-place it in the first free belt slot
        /// (belt slot 1). Returns true on the transition (the axe was actually placed), false if an axe
        /// is already held (idempotent, mirrors the old CraftAxe one-shot). The world axe pickup calls
        /// this; later the craft step calls it too.
        /// </summary>
        public bool PickUpAxe()
        {
            EnsureModel();
            if (_model.OwnsItem(ItemCatalog.AxeId)) return false; // already holds an axe — one-shot
            var axe = _catalog.ById(ItemCatalog.AxeId);
            if (axe == null) return false;
            return _model.AddToolToBelt(axe).HasValue;
        }

        /// <summary>AC4 — the held-axe show/hide query: true when the axe IS the SELECTED belt item (not
        /// merely owned). HeldAxe gates its renderer on this; CastawayFingerCurl gates its grip on it.</summary>
        public bool IsAxeSelectedInBelt => Model.IsSelectedBeltItem(ItemCatalog.AxeId);

        /// <summary>
        /// Combat POC 86cah7xxp AC4 — acquire the SPEAR (the second contrasting craftable weapon). Adds the
        /// spear TOOL to the first free belt slot (mirrors <see cref="PickUpAxe"/>). Returns true on the
        /// transition (the spear was actually placed), false if a spear is already held (idempotent). The
        /// spear pickup/craft calls this; the melee attack then resolves the spear WeaponDef when it is the
        /// SELECTED belt item.
        /// </summary>
        public bool PickUpSpear()
        {
            EnsureModel();
            if (_model.OwnsItem(ItemCatalog.SpearId)) return false; // already holds a spear — one-shot
            var spear = _catalog.ById(ItemCatalog.SpearId);
            if (spear == null) return false;
            return _model.AddToolToBelt(spear).HasValue;
        }

        /// <summary>Combat POC 86cah7xxp AC4 — true when the SPEAR is the SELECTED belt item (the melee attack
        /// reads this to resolve the spear as the active weapon, the axe-sibling query).</summary>
        public bool IsSpearSelectedInBelt => Model.IsSelectedBeltItem(ItemCatalog.SpearId);

        /// <summary>Combat POC 86cah7xxp — true once the castaway OWNS a spear (in any slot).</summary>
        public bool HasSpear => Model.OwnsItem(ItemCatalog.SpearId);

        /// <summary>I-2 (86cakkmr0) — true when the STONE pickaxe is the SELECTED belt item. The held-visual gate
        /// (HeldAxe.ShouldShow) + the mesh sync (HeldWeaponCycleDebug.SelectionIndexFor) read this so the held
        /// pickaxe SHOWS in-hand when selected — the axe/spear-sibling query. Fixes the soak-fail where selecting
        /// the pickaxe belt slot showed EMPTY hands (the belt→held coupling omitted the 5th tool type).</summary>
        public bool IsPickaxeStoneSelectedInBelt => Model.IsSelectedBeltItem(ItemCatalog.PickaxeStoneId);

        /// <summary>I-2 (86cakkmr0) — true when the IRON pickaxe is the SELECTED belt item (the iron-tier sibling
        /// of <see cref="IsPickaxeStoneSelectedInBelt"/>).</summary>
        public bool IsPickaxeIronSelectedInBelt => Model.IsSelectedBeltItem(ItemCatalog.PickaxeIronId);

        // ============================================================================================
        // LEGACY ledger surface — preserved VERBATIM (contract §7). Every caller stays green.
        // ============================================================================================

        /// <summary>True once the castaway OWNS an axe (in any slot). The chop gate / stump-axe / craft
        /// idempotency read this (ownership, NOT selection — selection is IsAxeSelectedInBelt).</summary>
        public bool HasAxe => Model.OwnsItem(ItemCatalog.AxeId);

        /// <summary>Live wood tally — summed Count of all "wood" stacks. The HUD reads it.</summary>
        public int WoodCount => Model.CountItem(ItemCatalog.WoodId);

        /// <summary>Live stone tally — summed Count of all "stone" stacks. The forge build gate reads it
        /// (86cakkmvc / I-3). Sibling of <see cref="WoodCount"/>.</summary>
        public int StoneCount => Model.CountItem(ItemCatalog.StoneId);

        /// <summary>
        /// Craft (== acquire) the axe — the legacy entry to the loop, now placing the axe tool on the
        /// belt. Idempotent: crafting an axe you already hold is a no-op (no Changed). Returns true only
        /// on the false->true transition so the craft spot fires its one-shot feedback exactly once.
        /// </summary>
        public bool CraftAxe() => PickUpAxe();

        /// <summary>
        /// Add wood to the inventory (the chop shim — contract §7). Forwards to AddItem(woodDef, n) so the
        /// chop call-site compiles unchanged until 86caa4c5c switches to AddItem directly. Returns the new
        /// total wood count. A zero/negative amount is a no-op.
        /// </summary>
        public int AddWood(int amount)
        {
            EnsureModel();
            if (amount <= 0) return WoodCount;
            var wood = _catalog.ById(ItemCatalog.WoodId);
            if (wood != null) _model.AddItem(wood, amount);
            return WoodCount;
        }

        /// <summary>
        /// Spend wood — the campfire BUILD seam (contract §7). ALL-OR-NOTHING: if fewer than
        /// <paramref name="amount"/> wood is held, NOTHING is spent and false is returned (no partial
        /// debit, no Changed) — the load-bearing "no wood -> no campfire" gate. On success debits the
        /// wood across stacks, fires Changed, returns true. A zero/negative amount is a no-op success.
        /// </summary>
        public bool SpendWood(int amount)
        {
            EnsureModel();
            if (amount <= 0) return true;                 // spending nothing trivially succeeds
            // RemoveItem is itself all-or-nothing: it returns false + debits nothing if fewer than
            // 'amount' are held — the load-bearing "no wood -> no campfire" gate (contract §7).
            return _model.RemoveItem(ItemCatalog.WoodId, amount);
        }

        /// <summary>
        /// Spend stone — the forge BUILD seam (86cakkmvc / I-3), the STONE sibling of <see cref="SpendWood"/>.
        /// ALL-OR-NOTHING: if fewer than <paramref name="amount"/> stone is held, NOTHING is spent and false is
        /// returned (no partial debit, no Changed) — the load-bearing "not enough mats -> no forge" gate. On
        /// success debits the stone across stacks, fires Changed, returns true. A zero/negative amount is a no-op
        /// success (matches SpendWood).
        /// </summary>
        public bool SpendStone(int amount)
        {
            EnsureModel();
            if (amount <= 0) return true;
            return _model.RemoveItem(ItemCatalog.StoneId, amount);
        }

        // ============================================================================================
        // MATERIAL-COST CRAFT façade (ticket 86camz9uz / crafting-redesign ① — the recipe seam). Resolves a
        // Recipe's explicit outputItemId against the catalog + delegates to InventoryModel.TryCraft (the pure
        // all-or-nothing debit → AddToolToBelt grant). This is the NEW crafting entry — distinct from the
        // legacy free-mint CraftAxe (which is retained VERBATIM for the legacy callers/tests but is NOT
        // extended by the new material-cost path, per the ① constraint).
        // ============================================================================================

        /// <summary>
        /// Craft <paramref name="recipe"/> — the material-cost table craft. Refuses a null/Placeholder recipe
        /// (STONE/IRON rows are Locked in ①) and a recipe whose <see cref="Recipe.OutputItemId"/> does not
        /// resolve in the catalog (both return false, debit nothing). Otherwise resolves the output ItemDef +
        /// calls <see cref="InventoryModel.TryCraft"/> (all-or-nothing debit across the cost lines → grant the
        /// tool onto the belt). Returns true iff the craft happened. The menu clicks this; a follow gate check
        /// (tier unlocked) lives in the menu UI, but the model still enforces affordability + the placeholder
        /// refusal here so a stray call can never mint a Locked tool for free.
        /// </summary>
        public bool TryCraft(Recipe recipe)
        {
            EnsureModel();
            if (recipe == null || recipe.Placeholder) return false; // Locked placeholder → never crafts in ①
            var output = _catalog.ById(recipe.OutputItemId);
            if (output == null) return false;                       // unresolved output id → no craft
            return _model.TryCraft(recipe.Costs, output);
        }

        /// <summary>
        /// True iff <paramref name="recipe"/>'s inputs are affordable right now (the menu paints
        /// Craftable-vs-Unaffordable from this). Forwards to <see cref="InventoryModel.CanAfford"/>; a
        /// Placeholder recipe is never affordable-to-craft (it is Locked), so returns false. Read-only.
        /// </summary>
        public bool CanAfford(Recipe recipe)
        {
            EnsureModel();
            if (recipe == null || recipe.Placeholder) return false;
            return _model.CanAfford(recipe.Costs);
        }
    }
}
