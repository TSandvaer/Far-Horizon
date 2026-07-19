using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarHorizon
{
    /// <summary>The paint state of a build-menu row (the menu paints from this; the EditMode guard pins
    /// the truth-table). A row is BUILDABLE only when affordable AND not already built.</summary>
    public enum BuildRowState { Buildable, Unaffordable, Built }

    /// <summary>
    /// The BUILD MENU — the SINGLE entry point for placing structures (ticket 86catpvpa). Pressing C opens
    /// a UI-Toolkit modal listing every placeable structure (crafting table / forge / … future placeables)
    /// with its material cost; selecting an AFFORDABLE row closes the menu and enters that structure's ①
    /// free-cursor ghost-placement flow (free-cursor ghost / left-click place / Escape cancel / scroll
    /// rotate — the ① mechanics VERBATIM, no parallel flow). Replaces the interim direct build keys — the
    /// table's C-direct-placement (① 86camz9uz) AND the forge's V-direct-placement (③ #305) — both of which
    /// are RETIRED here so the menu is the ONLY build entry point (the Sponsor's confusion fix, mid-soak
    /// 2026-07-19: "'C' should open a crafting menu allowing me to build the forge also").
    ///
    /// === The shared seam this ticket AUTHORS (Pattern-A vocabulary contract) ===
    /// Rows are <see cref="IBuildPlaceable"/>s registered via <see cref="RegisterPlaceable"/>. ③ forge +
    /// ⑤ campfire (once converted to the ghost flow — that conversion is ⑤'s scope) + any future placeable
    /// ADD a row into THIS menu; they do NOT fork a parallel menu. The editor-authored wiring lives in the
    /// serialized <see cref="placeableSources"/> array (each element a MonoBehaviour implementing
    /// <see cref="IBuildPlaceable"/>); ⑤ appends its converted CampfirePlacement to that array in
    /// MovementCameraScene.BuildCampfire — one line, no menu change.
    ///
    /// === Modal — UiInputGate guards click-leak (constraint 4) ===
    /// <see cref="Open"/> PUSHES <see cref="UiInputGate"/> so while the menu is up every world verb +
    /// locomotion + camera scroll is swallowed — a menu click can NEVER leak to a world verb (the ①
    /// CraftingMenuUI/placement idiom). <see cref="Close"/> POPs it. C opens the menu ONLY when no other
    /// modal owns input (<see cref="UiInputGate.CaptureWorldInput"/> false), so pressing C while a ghost
    /// placement is active (which itself holds the gate) does NOT re-open the menu.
    ///
    /// === Rows greyed when unaffordable (constraint 5) / non-interactive (the AC success-test) ===
    /// A row is greyed + its selection REFUSED when the pack can't afford it (<see cref="IBuildPlaceable.
    /// CanAffordBuild"/> false) or the structure is already built (<see cref="IBuildPlaceable.
    /// IsBuildComplete"/> true) — the cost is always shown. Only a BUILDABLE row's selection enters the
    /// ghost flow (the regression guard: <see cref="SelectPlaceable"/> must call
    /// <see cref="IBuildPlaceable.BeginBuildPlacement"/>).
    ///
    /// === Robust headless (logic ≠ layout) + serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The registration / Open / Close / SelectPlaceable LOGIC does not depend on a laid-out panel (a
    /// missing/unresolved UIDocument only skips the repaint), so an EditMode test drives the real select
    /// path without a rendered panel; the VISUAL is proven by the shipped-build capture (-verifyBuildMenu).
    /// The UIDocument + PanelSettings + placeableSources refs serialize into Boot.unity (MovementCameraScene).
    /// NO mutable statics (only instance state + readonly colour constants).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BuildMenuUI : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The UIDocument hosting the menu. Auto-resolved from this GameObject if unset.")]
        public UIDocument document;

        [Tooltip("The placement drivers this menu fronts — each MUST implement IBuildPlaceable (crafting " +
                 "table + forge today; ⑤ appends the converted campfire here). Registered as rows at OnEnable.")]
        public MonoBehaviour[] placeableSources;

        [Header("Input")]
        [Tooltip("Key to OPEN/CLOSE the build menu (default C — Danish-layout-safe, free of the gameplay " +
                 "keys). This is the SINGLE build entry point; the interim table-C / forge-V direct keys are retired.")]
        public KeyCode openKey = KeyCode.C;
        [Tooltip("Key to CLOSE the menu (Escape).")]
        public KeyCode closeKey = KeyCode.Escape;

        /// <summary>Whether the build menu is currently open. Test/UI seam.</summary>
        public bool IsOpen { get; private set; }

        // The registered placeables (rows), in registration order. Editor-authored via placeableSources +
        // a runtime RegisterPlaceable seam (③/⑤/tests).
        private readonly List<IBuildPlaceable> _placeables = new List<IBuildPlaceable>(4);
        // Row-view caches, index-aligned to _placeables so RefreshAll repaints in place.
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<Label> _rowState = new List<Label>();

        private VisualElement _scrim;
        private VisualElement _panel;
        private bool _built;        // the view tree is built (rebuilt when placeables change)
        private bool _gateTracked;  // tracks our UiInputGate push so it can never stick open
        private Inventory _inventory; // subscribed to for live row refresh (found lazily)

        // Warm carved-wood palette (matches the crafting menu / HUD family read).
        private static readonly Color Cream = new Color(0.92f, 0.85f, 0.72f);
        private static readonly Color PanelBg = new Color(0.16f, 0.12f, 0.09f, 0.97f);
        private static readonly Color RowBg = new Color(0.24f, 0.19f, 0.14f, 1f);
        private static readonly Color Ember = new Color(1f, 0.72f, 0.28f);

        /// <summary>The registered placeables (read-only; test seam — asserts the menu lists exactly the set).</summary>
        public IReadOnlyList<IBuildPlaceable> Placeables => _placeables;

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (_inventory == null) _inventory = FindObjectOfType<Inventory>();
        }

        void OnEnable()
        {
            CollectPlaceableSources();
            if (_inventory == null) _inventory = FindObjectOfType<Inventory>();
            if (_inventory != null) _inventory.Changed += RefreshAll;
            BuildView();
            SetShown(false); // ships closed
            RefreshAll();
        }

        void OnDisable()
        {
            if (_inventory != null) _inventory.Changed -= RefreshAll;
            // Never leave the world-input gate stuck open if we're torn down while open.
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
        }

        void Update()
        {
            if (IsOpen)
            {
                // Toggle closed on C, or close on Escape.
                if (Input.GetKeyDown(openKey) || Input.GetKeyDown(closeKey)) Close();
                return;
            }
            // Open on C ONLY when no other modal owns input — so C during an active ghost placement (which
            // holds the gate) does NOT re-open the menu, and the recipe menu / settings panel don't clash.
            if (Input.GetKeyDown(openKey) && !UiInputGate.CaptureWorldInput) Open();
        }

        // ============================================================================================
        // Registration seam (Pattern-A — ③/⑤ consume this).
        // ============================================================================================

        /// <summary>Register a placeable as a menu row (idempotent — a placeable already registered is
        /// ignored). Rebuilds the view so the new row appears. The editor-authored path routes every
        /// <see cref="placeableSources"/> element through here at OnEnable; ③/⑤/tests can also call it
        /// directly. Null / already-present placeables are no-ops.</summary>
        public void RegisterPlaceable(IBuildPlaceable placeable)
        {
            if (placeable == null || _placeables.Contains(placeable)) return;
            _placeables.Add(placeable);
            _built = false;   // force a view rebuild so the new row is added
            BuildView();
            RefreshAll();
        }

        private void CollectPlaceableSources()
        {
            if (placeableSources == null) return;
            foreach (var src in placeableSources)
            {
                if (src is IBuildPlaceable p && !_placeables.Contains(p)) _placeables.Add(p);
            }
        }

        // ============================================================================================
        // Open / Close (modal — pushes/pops the world-input gate).
        // ============================================================================================

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

        // ============================================================================================
        // Selection — the ① ghost-flow entry (the regression guard).
        // ============================================================================================

        /// <summary>
        /// Select <paramref name="placeable"/>'s row: if it is BUILDABLE (affordable AND not already built),
        /// CLOSE the menu and enter its ① free-cursor ghost-placement flow via
        /// <see cref="IBuildPlaceable.BeginBuildPlacement"/>; return true. An unaffordable / already-built /
        /// null row is NON-interactive — no menu close, no placement, returns false. The row-click handler
        /// AND the EditMode regression-guard test both drive this seam (the guard reds if a buildable
        /// selection does not enter the ghost flow). Closing the menu FIRST releases the menu's gate; the
        /// placement immediately re-pushes its own, so world input stays modal across the transition.
        /// </summary>
        public bool SelectPlaceable(IBuildPlaceable placeable)
        {
            if (placeable == null) return false;
            if (ResolveRowState(placeable.CanAffordBuild, placeable.IsBuildComplete) != BuildRowState.Buildable)
                return false; // greyed row (unaffordable / already built) — non-interactive
            Close();
            placeable.BeginBuildPlacement(); // enter the ① ghost flow (VERBATIM — no parallel flow)
            return true;
        }

        /// <summary>PURE row-state truth-table (unit-testable without a scene): a row is BUILDABLE only when
        /// the structure is NOT already built AND the pack can afford it; a built structure reads Built; an
        /// unbuilt unaffordable structure reads Unaffordable. The menu paints greyed + non-interactive for
        /// Built/Unaffordable. Static + dependency-free so the EditMode guard pins every cell.</summary>
        public static BuildRowState ResolveRowState(bool canAfford, bool isBuilt)
        {
            if (isBuilt) return BuildRowState.Built;
            return canAfford ? BuildRowState.Buildable : BuildRowState.Unaffordable;
        }

        /// <summary>The current row state of <paramref name="placeable"/> (test/paint seam).</summary>
        public BuildRowState RowStateOf(IBuildPlaceable placeable)
            => placeable == null ? BuildRowState.Unaffordable
                                 : ResolveRowState(placeable.CanAffordBuild, placeable.IsBuildComplete);

        // ============================================================================================
        // View construction (code-built tree — inline styles, no UXML/USS asset dependency; small tree).
        // ============================================================================================

        private void BuildView()
        {
            if (_built || document == null) return;
            var rootVisual = document.rootVisualElement;
            if (rootVisual == null) return; // headless / not laid out — logic still works, just no repaint
            rootVisual.Clear();
            _rows.Clear();
            _rowState.Clear();

            _scrim = new VisualElement { name = "build-scrim" };
            _scrim.style.position = Position.Absolute;
            _scrim.style.left = 0; _scrim.style.top = 0; _scrim.style.right = 0; _scrim.style.bottom = 0;
            _scrim.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            _scrim.style.alignItems = Align.Center;
            _scrim.style.justifyContent = Justify.Center;
            rootVisual.Add(_scrim);

            _panel = new VisualElement { name = "build-panel" };
            _panel.style.minWidth = 420;
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
            var title = new Label("BUILD");
            title.style.color = Ember; title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(title);

            var close = new Button(Close) { text = "X" };
            close.style.color = Cream; close.style.backgroundColor = RowBg;
            close.style.width = 30; close.style.height = 26;
            titleRow.Add(close);
            _panel.Add(titleRow);

            for (int i = 0; i < _placeables.Count; i++)
                _panel.Add(MakeRow(_placeables[i]));

            _built = true;
        }

        private VisualElement MakeRow(IBuildPlaceable placeable)
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

            var name = new Label(placeable.BuildDisplayName);
            name.style.color = Cream; name.style.fontSize = 14;
            name.pickingMode = PickingMode.Ignore;
            row.Add(name);

            var right = new Label(CostText(placeable));
            right.style.color = Cream; right.style.fontSize = 12;
            right.style.unityTextAlign = TextAnchor.MiddleRight;
            right.pickingMode = PickingMode.Ignore;
            row.Add(right);

            // SelectPlaceable is the authority — it refuses a greyed row, so a stray click on an
            // unaffordable/built row can never enter placement (the AC "non-interactive" invariant).
            row.RegisterCallback<PointerDownEvent>(_ => SelectPlaceable(placeable));

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
            for (int r = 0; r < _rows.Count && r < _placeables.Count; r++)
                PaintRow(_rows[r], _rowState[r], _placeables[r]);
        }

        private void PaintRow(VisualElement row, Label right, IBuildPlaceable placeable)
        {
            BuildRowState state = RowStateOf(placeable);
            switch (state)
            {
                case BuildRowState.Buildable:
                    row.style.opacity = 1f;
                    row.SetEnabled(true);
                    right.text = CostText(placeable);
                    right.style.color = Ember;
                    break;
                case BuildRowState.Unaffordable:
                    row.style.opacity = 0.5f;             // greyed (constraint 5)
                    row.SetEnabled(true);                 // still shows the cost; SelectPlaceable refuses the click
                    right.text = CostText(placeable) + "  (need more)";
                    right.style.color = Cream;
                    break;
                default: // Built — one-per-world; hide the row so it can't be placed twice.
                    row.style.display = DisplayStyle.None;
                    break;
            }
            if (state != BuildRowState.Built) row.style.display = DisplayStyle.Flex;
        }

        private static string CostText(IBuildPlaceable placeable)
        {
            int wood = placeable.BuildWoodCost, stone = placeable.BuildStoneCost;
            if (wood > 0 && stone > 0) return wood + " wood + " + stone + " stone";
            if (wood > 0) return wood + " wood";
            if (stone > 0) return stone + " stone";
            return "free";
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
