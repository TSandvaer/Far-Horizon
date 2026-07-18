using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarHorizon
{
    /// <summary>
    /// The recipe MENU for the placed crafting table (ticket 86camz9uz / crafting-redesign ① — spec §3). A
    /// UI-Toolkit MODAL panel in the InventoryUI family: recipes grouped by tier (WOOD / STONE / IRON), each
    /// with its 5 tool rows (axe / pickaxe / spear / dagger / sword). Per-row state (spec §3):
    ///   • Craftable   — tier unlocked AND affordable → active; click debits inputs + grants the tool;
    ///   • Unaffordable— tier unlocked but materials short → greyed, shows cost + what's missing;
    ///   • Locked      — tier not unlocked / a ①-placeholder (STONE/IRON) → greyed, shows the unlock hint.
    ///
    /// === Modal — UiInputGate guards click-leak (the ① constraint) ===
    /// Open() PUSHES <see cref="UiInputGate"/> so while the menu is up, every world verb (chop/mine/consume/
    /// attack/loot) + locomotion + camera is swallowed — a menu click can NEVER leak to a world verb. Close()
    /// POPs it. This is the strongest form of the spec §3 "the click-guard invariant" and mirrors how the
    /// settings panel gates focused input.
    ///
    /// === Opened by the placement (explicit handoff), re-opened via the E-interact arbiter (soak fix F5) ===
    /// <see cref="CraftingTablePlacement"/> calls <see cref="Open"/> right after it reveals the table (so the
    /// open never races the confirm-frame key). Afterwards the built table is USED with the universal E key:
    /// this menu is an <see cref="IPickable"/> ("use" verb, DisplayName "crafting table") so the player-side
    /// <see cref="PickableLooter"/> resolves the NEAREST in-range interactable and opens the menu on E — which
    /// gives, for FREE, the nearest-interactable PRECEDENCE the Sponsor asked for (E near a loose stone AND
    /// the table loots the nearer one; the shipped C-reopen is RETIRED — the general "C = build menu" is a
    /// follow-up, 86catpvpa). The <see cref="LootPrompt"/> then reads "Press E to use crafting table" with no
    /// per-item branch. Escape (or the ✕ button) closes. IPickable is the E-INTERACT surface here (the pond
    /// already stretched it past "pick up" with a custom verb); "using" the table adds nothing to the pack, so
    /// <see cref="TryLoot"/> opens the menu and returns true to consume the E press.
    ///
    /// === F6 — unlocked-only by default + a "show locked" toggle ===
    /// The menu shows only the tiers/rows the player can actually work with (WOOD live in ①); the STONE/IRON
    /// Locked placeholders are HIDDEN by default and revealed by a "Show locked" button, so the menu isn't a
    /// wall of greyed rows on the first table (the Sponsor's soak). The ladder is still discoverable on demand.
    ///
    /// === Robust headless (logic ≠ layout) + serialization ===
    /// The Open/Close/CraftRecipe LOGIC does not depend on a laid-out panel (a missing/unresolved UIDocument
    /// only skips the repaint), so the PlayMode gate drives the real craft path without a rendered panel; the
    /// VISUAL is proven by the shipped-build capture. The UIDocument + PanelSettings + Inventory/table/player
    /// refs serialize into Boot.unity (MovementCameraScene) — NOT an Awake build. NO mutable statics.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CraftingMenuUI : MonoBehaviour, IPickable
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The UIDocument hosting the menu. Auto-resolved from this GameObject if unset.")]
        public UIDocument document;
        [Tooltip("The inventory the recipes debit/grant against. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;
        [Tooltip("The placed table the menu belongs to (opens only when it IsBuilt + near). Scene-found fallback.")]
        public CraftingTable table;
        [Tooltip("The player transform for the proximity re-open check. Wired at bootstrap; ClickToMove fallback.")]
        public Transform player;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the built table is USABLE — the E-interact reach (this is " +
                 "the menu's IPickable.LootRange the PickableLooter resolves against). C-reopen is RETIRED (F5).")]
        public float openRadius = 2.5f;
        [Tooltip("Key to close the menu.")]
        public KeyCode closeKey = KeyCode.Escape;

        /// <summary>Whether the recipe menu is currently open. Test/UI seam.</summary>
        public bool IsOpen { get; private set; }

        private readonly List<Recipe> _recipes = CraftingRecipeBook.BuildDefaults();
        // Row view caches, parallel to _recipes (index-aligned) so RefreshAll repaints in place.
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<Label> _rowState = new List<Label>();
        // Per-tier section headers (F6 — hidden when the tier has no visible rows).
        private readonly List<CraftTier> _headerTiers = new List<CraftTier>();
        private readonly List<Label> _headers = new List<Label>();

        private VisualElement _scrim;
        private VisualElement _panel;
        private Button _showLockedButton;
        private bool _showLocked;   // F6 — Locked placeholder rows hidden by default; toggled by the button
        private bool _built;
        private bool _gateTracked;

        // Warm carved-wood-ish palette (matches the HUD/inventory family read).
        private static readonly Color Cream = new Color(0.92f, 0.85f, 0.72f);
        private static readonly Color PanelBg = new Color(0.16f, 0.12f, 0.09f, 0.97f);
        private static readonly Color RowBg = new Color(0.24f, 0.19f, 0.14f, 1f);
        private static readonly Color Ember = new Color(1f, 0.72f, 0.28f);

        /// <summary>The recipe set the menu shows (read-only; test seam).</summary>
        public IReadOnlyList<Recipe> Recipes => _recipes;

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (table == null) table = FindObjectOfType<CraftingTable>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
        }

        void OnEnable()
        {
            if (inventory != null) inventory.Changed += RefreshAll;
            BuildView();
            SetShown(false); // ships closed
            RefreshAll();
        }

        void OnDisable()
        {
            if (inventory != null) inventory.Changed -= RefreshAll;
            // Never leave the world-input gate stuck open if we're torn down while open.
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
        }

        void Update()
        {
            if (IsOpen && Input.GetKeyDown(closeKey)) Close();
            // Re-open is handled by the E-interact arbiter (this menu is an IPickable; the PickableLooter opens
            // it on E, nearest-interactable wins — F5). No per-frame key poll for opening here anymore.
        }

        /// <summary>True iff the player is within <see cref="openRadius"/> (planar) of the built table.</summary>
        public bool PlayerInRange()
        {
            if (table == null || player == null) return false;
            Vector2 t = new Vector2(table.transform.position.x, table.transform.position.z);
            Vector2 p = new Vector2(player.position.x, player.position.z);
            return Vector2.Distance(t, p) <= openRadius;
        }

        // ============================================================================================
        // IPickable — the E-INTERACT surface (soak fix F5). The built table is "used" (menu opens) with the
        // universal E key via the player-side PickableLooter's nearest-in-range resolve — so E near a loose
        // stone AND the table acts on the NEARER one (the precedence the Sponsor asked for), and the LootPrompt
        // shows "Press E to use crafting table". "Using" the table adds nothing to the pack; TryLoot opens the
        // menu and returns true to consume the E press. (IPickable is already the generic E-interact surface —
        // the pond stretched it past "pick up" with its own verb; this adds a "use" verb, no interface change.)
        // ============================================================================================

        /// <summary>IPickable: the table is USABLE while it is BUILT, the menu is CLOSED, and an inventory is
        /// wired. Closed-menu-only so E doesn't re-trigger while the menu owns the screen (the modal gate also
        /// blocks E, this is belt-and-braces). Unbuilt (invisible-until-placed) → not usable.</summary>
        public bool CanLoot => table != null && table.IsBuilt && !IsOpen && inventory != null;

        /// <summary>IPickable: the table's world position — the looter measures planar XZ distance to this for
        /// the nearest-interactable resolve (same idiom as every pickable).</summary>
        public Vector3 LootPosition => table != null ? table.transform.position : transform.position;

        /// <summary>IPickable: the table's interact reach — its <see cref="openRadius"/>. The looter uses THIS
        /// per-item radius in the nearest-in-range resolve, so a nearer stone/bush still wins over the table.</summary>
        public float LootRange => openRadius;

        /// <summary>IPickable: the generic prompt name — the table reads "Press E to use crafting table".</summary>
        public string DisplayName => "crafting table";

        /// <summary>IPickable: the gather verb — "use" (not "pick up") so the prompt fits the action (F5).</summary>
        public string GatherVerb => "use";

        /// <summary>IPickable.TryLoot — the E-interact for the built table: OPEN the recipe menu and return true
        /// to consume the E press (nothing is added to <paramref name="inv"/>). Returns false when not usable
        /// (unbuilt / already open) so the looter moves past to any other in-range pickable.</summary>
        public bool TryLoot(Inventory inv)
        {
            if (!CanLoot) return false;
            Open();
            return true;
        }

        /// <summary>Open the menu: push the world-input gate (modal), show, repaint. Idempotent.</summary>
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            UiInputGate.SetPanelOpen(true, ref _gateTracked); // MODAL — swallow world verbs while open
            SetShown(true);
            RefreshAll();
        }

        /// <summary>Close the menu: pop the world-input gate, hide. Idempotent.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            SetShown(false);
        }

        /// <summary>
        /// Craft <paramref name="recipe"/> if its row is currently CRAFTABLE (tier unlocked + affordable +
        /// not a placeholder). Returns true iff the craft happened. The row-click handler AND the PlayMode
        /// test seam (drives the real craft without synthesizing a UI pointer event, the InventoryUI
        /// BeginDrag/EndDrag precedent). The model still enforces affordability + placeholder-refusal, so a
        /// stray call can never mint a Locked/unaffordable tool.
        /// </summary>
        public bool CraftRecipe(Recipe recipe)
        {
            if (recipe == null || inventory == null) return false;
            if (RowStateOf(recipe) != RecipeRowState.Craftable) return false;
            bool ok = inventory.TryCraft(recipe);
            RefreshAll();
            return ok;
        }

        /// <summary>The current display state of <paramref name="recipe"/>'s row (spec §3) — the menu paints
        /// from this; a test asserts the truth-table end to end.</summary>
        public RecipeRowState RowStateOf(Recipe recipe)
        {
            if (recipe == null) return RecipeRowState.Locked;
            var unlock = new CraftingUnlockState(
                tablePlaced: table != null && table.IsBuilt,
                ownsWoodPickaxe: inventory != null && inventory.Model.OwnsItem(ItemCatalog.PickaxeWoodId),
                ownsIronIngot: inventory != null && inventory.Model.OwnsItem(ItemCatalog.IronIngotId));
            bool unlocked = CraftingRecipeBook.IsTierUnlocked(recipe.Tier, unlock);
            bool affordable = inventory != null && inventory.CanAfford(recipe);
            return Recipe.ResolveRowState(recipe.Placeholder, unlocked, affordable);
        }

        // ============================================================================================
        // View construction (code-built tree — inline styles, no UXML/USS asset dependency; small tree).
        // ============================================================================================

        private void BuildView()
        {
            if (_built || document == null) return;
            var rootVisual = document.rootVisualElement;
            if (rootVisual == null) return;
            rootVisual.Clear();
            _rows.Clear();
            _rowState.Clear();
            _headerTiers.Clear();
            _headers.Clear();

            _scrim = new VisualElement { name = "craft-scrim" };
            _scrim.style.position = Position.Absolute;
            _scrim.style.left = 0; _scrim.style.top = 0; _scrim.style.right = 0; _scrim.style.bottom = 0;
            _scrim.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            _scrim.style.alignItems = Align.Center;
            _scrim.style.justifyContent = Justify.Center;
            rootVisual.Add(_scrim);

            _panel = new VisualElement { name = "craft-panel" };
            _panel.style.minWidth = 460;
            _panel.style.paddingLeft = 18; _panel.style.paddingRight = 18;
            _panel.style.paddingTop = 14; _panel.style.paddingBottom = 16;
            _panel.style.backgroundColor = PanelBg;
            SetBorder(_panel, Ember, 2, 10);
            _scrim.Add(_panel);

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 8;
            var title = new Label("CRAFTING TABLE");
            title.style.color = Ember; title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(title);

            var titleButtons = new VisualElement();
            titleButtons.style.flexDirection = FlexDirection.Row;
            titleButtons.style.alignItems = Align.Center;

            // F6 — the "Show locked" toggle. Locked placeholder rows (STONE/IRON in ①) are hidden by default;
            // this reveals the ladder on demand so the first table isn't a wall of greyed rows.
            _showLockedButton = new Button(ToggleShowLocked) { text = ShowLockedLabel() };
            _showLockedButton.style.color = Cream; _showLockedButton.style.backgroundColor = RowBg;
            _showLockedButton.style.height = 26; _showLockedButton.style.marginRight = 8;
            titleButtons.Add(_showLockedButton);

            var close = new Button(Close) { text = "X" };
            close.style.color = Cream; close.style.backgroundColor = RowBg;
            close.style.width = 30; close.style.height = 26;
            titleButtons.Add(close);
            titleRow.Add(titleButtons);
            _panel.Add(titleRow);

            // Group rows by tier, in tier order, so the ladder reads WOOD → STONE → IRON.
            foreach (CraftTier tier in new[] { CraftTier.Wood, CraftTier.Stone, CraftTier.Iron })
            {
                var header = new Label(tier.ToString().ToUpperInvariant() + " TIER");
                header.style.color = Cream; header.style.fontSize = 13;
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.style.marginTop = 8; header.style.marginBottom = 3;
                header.style.opacity = 0.85f;
                _panel.Add(header);
                _headerTiers.Add(tier);
                _headers.Add(header);

                for (int i = 0; i < _recipes.Count; i++)
                {
                    if (_recipes[i].Tier != tier) continue;
                    _panel.Add(MakeRow(_recipes[i]));
                }
            }
            _built = true;
        }

        private VisualElement MakeRow(Recipe recipe)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 10; row.style.paddingRight = 10;
            row.style.paddingTop = 6; row.style.paddingBottom = 6;
            row.style.marginBottom = 3;
            row.style.backgroundColor = RowBg;
            SetBorder(row, new Color(0f, 0f, 0f, 0.35f), 1, 6);
            row.pickingMode = PickingMode.Position;

            var name = new Label(recipe.DisplayName);
            name.style.color = Cream; name.style.fontSize = 14;
            name.pickingMode = PickingMode.Ignore;
            row.Add(name);

            var right = new Label(CostText(recipe));
            right.style.color = Cream; right.style.fontSize = 12;
            right.style.unityTextAlign = TextAnchor.MiddleRight;
            right.pickingMode = PickingMode.Ignore;
            row.Add(right);

            row.RegisterCallback<PointerDownEvent>(_ => CraftRecipe(recipe));

            _rows.Add(row);
            _rowState.Add(right);
            return row;
        }

        // ============================================================================================
        // Repaint.
        // ============================================================================================

        private void RefreshAll()
        {
            if (!_built) return;
            // _rows / _rowState are index-aligned to the ORDER rows were added (tier-grouped), NOT to
            // _recipes' declaration order — so recover each row's recipe by matching the display name once.
            // Cheap (15 rows) + no per-frame alloc beyond the state string.
            for (int r = 0; r < _rows.Count; r++)
            {
                Recipe recipe = RecipeForRowIndex(r);
                if (recipe == null) continue;
                PaintRow(_rows[r], _rowState[r], recipe);
            }
            // F6 — hide a tier header when the toggle leaves it with no visible rows (an all-Locked tier
            // collapses entirely by default, so the first table shows only the live WOOD tier).
            for (int h = 0; h < _headers.Count; h++)
                _headers[h].style.display = TierHasVisibleRow(_headerTiers[h]) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>F6 — is a row VISIBLE given the show-locked toggle? Locked rows show only when the toggle is
        /// on; unlocked (Craftable/Unaffordable) rows always show. Pure so the EditMode guard pins the truth-table.</summary>
        public static bool IsRowVisible(RecipeRowState state, bool showLocked)
            => state != RecipeRowState.Locked || showLocked;

        private bool TierHasVisibleRow(CraftTier tier)
        {
            for (int i = 0; i < _recipes.Count; i++)
            {
                if (_recipes[i].Tier != tier) continue;
                if (IsRowVisible(RowStateOf(_recipes[i]), _showLocked)) return true;
            }
            return false;
        }

        private void ToggleShowLocked()
        {
            _showLocked = !_showLocked;
            if (_showLockedButton != null) _showLockedButton.text = ShowLockedLabel();
            RefreshAll();
        }

        private string ShowLockedLabel() => _showLocked ? "Hide locked" : "Show locked";

        private void PaintRow(VisualElement row, Label right, Recipe recipe)
        {
            RecipeRowState state = RowStateOf(recipe);

            // F6 — Locked placeholder rows are hidden unless the "Show locked" toggle is on.
            row.style.display = IsRowVisible(state, _showLocked) ? DisplayStyle.Flex : DisplayStyle.None;

            switch (state)
            {
                case RecipeRowState.Craftable:
                    row.style.opacity = 1f;
                    row.SetEnabled(true);
                    right.text = CostText(recipe);
                    right.style.color = Ember;
                    break;
                case RecipeRowState.Unaffordable:
                    row.style.opacity = 0.55f;
                    row.SetEnabled(true); // still shows the cost; the click is refused by CraftRecipe's guard
                    right.text = CostText(recipe) + "  (need more)";
                    right.style.color = Cream;
                    break;
                default: // Locked
                    row.style.opacity = 0.4f;
                    row.SetEnabled(true);
                    right.text = recipe.Placeholder ? "Locked — coming soon" : "Locked";
                    right.style.color = Cream;
                    break;
            }
        }

        // Map a row-cache index back to its recipe (rows were added tier-grouped). One pass over the same
        // tier-grouped order used to build them.
        private Recipe RecipeForRowIndex(int rowIndex)
        {
            int r = 0;
            foreach (CraftTier tier in new[] { CraftTier.Wood, CraftTier.Stone, CraftTier.Iron })
                for (int i = 0; i < _recipes.Count; i++)
                {
                    if (_recipes[i].Tier != tier) continue;
                    if (r == rowIndex) return _recipes[i];
                    r++;
                }
            return null;
        }

        private static string CostText(Recipe recipe)
        {
            if (recipe.Costs == null || recipe.Costs.Length == 0) return "free";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < recipe.Costs.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(recipe.Costs[i].Amount).Append(' ').Append(ShortName(recipe.Costs[i].ItemId));
            }
            return sb.ToString();
        }

        private static string ShortName(string id)
        {
            switch (id)
            {
                case ItemCatalog.WoodId: return "wood";
                case ItemCatalog.StoneId: return "stone";
                case ItemCatalog.IronIngotId: return "iron";
                default: return id;
            }
        }

        private void SetShown(bool on)
        {
            if (_scrim != null) _scrim.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void SetBorder(VisualElement e, Color c, float width, float radius)
        {
            e.style.borderLeftWidth = width; e.style.borderRightWidth = width;
            e.style.borderTopWidth = width; e.style.borderBottomWidth = width;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderTopLeftRadius = radius; e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius; e.style.borderBottomRightRadius = radius;
        }
    }
}
