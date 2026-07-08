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
    /// === Opened by the placement (explicit handoff), re-opened by proximity + key ===
    /// <see cref="CraftingTablePlacement"/> calls <see cref="Open"/> right after it reveals the table (so the
    /// open never races the confirm-frame key). Afterwards, while the built table is near + the menu is
    /// closed, the build/interact key re-opens it; Escape (or the ✕ button) closes. 🎚️ open-mechanic default —
    /// Sponsor-soak tunes.
    ///
    /// === Robust headless (logic ≠ layout) + serialization ===
    /// The Open/Close/CraftRecipe LOGIC does not depend on a laid-out panel (a missing/unresolved UIDocument
    /// only skips the repaint), so the PlayMode gate drives the real craft path without a rendered panel; the
    /// VISUAL is proven by the shipped-build capture. The UIDocument + PanelSettings + Inventory/table/player
    /// refs serialize into Boot.unity (MovementCameraScene) — NOT an Awake build. NO mutable statics.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CraftingMenuUI : MonoBehaviour
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
        [Tooltip("Planar (XZ) distance within which the built table's menu can be (re)opened with the build key.")]
        public float openRadius = 2.5f;
        [Tooltip("Key to re-open the menu when near the built table (mirrors the placement build key, C).")]
        public KeyCode reopenKey = KeyCode.C;
        [Tooltip("Key to close the menu.")]
        public KeyCode closeKey = KeyCode.Escape;

        /// <summary>Whether the recipe menu is currently open. Test/UI seam.</summary>
        public bool IsOpen { get; private set; }

        private readonly List<Recipe> _recipes = CraftingRecipeBook.BuildDefaults();
        // Row view caches, parallel to _recipes (index-aligned) so RefreshAll repaints in place.
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<Label> _rowState = new List<Label>();

        private VisualElement _scrim;
        private VisualElement _panel;
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
            if (IsOpen)
            {
                if (Input.GetKeyDown(closeKey)) Close();
                return;
            }
            // Re-open with the build key while the built table is near (the placement opens it the first time).
            if (table != null && table.IsBuilt && Input.GetKeyDown(reopenKey) && PlayerInRange())
                Open();
        }

        /// <summary>True iff the player is within <see cref="openRadius"/> (planar) of the built table.</summary>
        public bool PlayerInRange()
        {
            if (table == null || player == null) return false;
            Vector2 t = new Vector2(table.transform.position.x, table.transform.position.z);
            Vector2 p = new Vector2(player.position.x, player.position.z);
            return Vector2.Distance(t, p) <= openRadius;
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
            var close = new Button(Close) { text = "X" };
            close.style.color = Cream; close.style.backgroundColor = RowBg;
            close.style.width = 30; close.style.height = 26;
            titleRow.Add(close);
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
        }

        private void PaintRow(VisualElement row, Label right, Recipe recipe)
        {
            RecipeRowState state = RowStateOf(recipe);
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
