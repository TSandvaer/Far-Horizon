using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// LEFT-CLICK CONSUME the SELECTED belt item (ticket 86caf7a30 — the unified input model, DECISIONS
    /// 2026-06-27). With a CONSUMABLE selected in the belt, a LEFT-CLICK consumes ONE unit and applies its
    /// type-specific restore: a BERRY → eat (HungerNeed.AddFood), WATER → drink (ThirstNeed.AddWater). This
    /// is the consumable sibling of <see cref="ChopTree"/>'s axe→chop: left-click is ONE "use the selected
    /// belt item" verb whose EFFECT is determined by the item's TYPE. The old E-eat-berry + proximity-drink-
    /// at-the-pond triggers are REMOVED (AC4) — left-click is the single consume input now.
    ///
    /// === Why this is NOT a parallel input path (constraint: reuse the #140 left-click-with-selected-item seam) ===
    /// The "seam" #140 established is: read the SELECTED belt slot's item kind, and route left-click by it. The
    /// chop handler (<see cref="ChopTree"/>) fires ONLY when the AXE is selected (Inventory.IsAxeSelectedInBelt);
    /// THIS handler fires ONLY when a CONSUMABLE is selected. The two branches are DISJOINT by the selected-item
    /// discriminant — exactly ONE can match a given selection — so they never double-fire on the shared left-click
    /// edge (when a berry is selected the axe-gate is false → ChopTree no-ops; when the axe is selected
    /// <see cref="ShouldConsumeOnClick"/>'s consumable check is false → this no-ops). Same dispatch contract,
    /// disjoint handlers — NOT two parallel paths racing the same item.
    ///
    /// === The guards mirror chop VERBATIM (constraint: guard against UI-click / camera-drag exactly as chop does) ===
    /// <see cref="ShouldConsumeOnClick"/> is the pure static decision (the unit-testable truth-table, the analog of
    /// <see cref="ChopTree.ShouldChopOnClick"/>): a consumable must be selected AND no modal panel open
    /// (<see cref="UiInputGate.CaptureWorldInput"/>) AND the pointer NOT over the inventory/belt UI
    /// (<see cref="InventoryUI.IsPointerOverUI"/>) AND the RIGHT mouse button NOT held (the camera orbit drag,
    /// <c>Input.GetMouseButton(1)</c>). Unlike chop, consume has NO range gate — you drink/eat what you hold,
    /// anywhere.
    ///
    /// === One click = one consume = one restore (AC1) ===
    /// Edge-triggered on <c>Input.GetMouseButtonDown(0)</c> (NOT the held level — eating/drinking is one-shot per
    /// click, no hold-to-repeat); plus a one-shot <see cref="RequestUseClick"/> latch (the input-independent seam,
    /// the analog of <see cref="ChopTree.RequestChopClick"/>) so headless PlayMode + the shipped-build capture
    /// drive the SAME consume path without a real mouse. The consume is ATOMIC + all-or-nothing: it reuses the
    /// SHIPPED restore seams (<see cref="HungerNeed.TryEatBerry"/> / a water analog) so a unit is never removed
    /// without its restore, nor restored without the removal — and an empty/zero-count selection is a clean no-op.
    ///
    /// === Reuses the SHIPPED restores — does NOT re-implement (constraint: AddFood/AddWater are the hooks) ===
    /// Berry → <see cref="EatBerryAction.TryEatOneBerry"/> (the shipped atomic consume+AddFood seam, selection-
    /// agnostic — it draws a berry from any slot). Water → <see cref="TryDrinkOneWater"/> here, which removes one
    /// <see cref="ItemCatalog.WaterId"/> from inventory and restores via the SHIPPED <see cref="ThirstNeed.AddWater"/>
    /// (atomic: restore ONLY if the consume succeeded). Re-implementing a restore would desync the HUD bars + the
    /// settings (the need decay/floors/amounts/HUD are UNCHANGED — only the TRIGGER moves here).
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The component + its Inventory/HungerNeed/ThirstNeed/InventoryUI refs are wired editor-time by
    /// BootstrapProject (serialized into Boot.unity), NOT added at Awake. The Awake FindObjectOfType calls are a
    /// build-safety net only. LeftClickConsumeSceneTests guards the serialized presence + wiring.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[consume-trace]` lines on the first eat / first drink / first no-op so the consume input's runtime
    /// behaviour is readable from the build log; [Conditional("UNITY_EDITOR")] strips them from the shipped exe.
    ///
    /// NO MUTABLE STATICS (instance state only) — the StaticStateResetTests audit needs no
    /// [RuntimeInitializeOnLoadMethod] reset for this class.
    /// </summary>
    public class LeftClickConsume : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory whose SELECTED belt item left-click consumes. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The hunger need a berry restores (AddFood). Wired at bootstrap; scene-found fallback. May be " +
                 "null — eating then consumes the berry gracefully with no restore (no null-ref).")]
        public HungerNeed hunger;

        [Tooltip("The thirst need a water unit restores (AddWater). Wired at bootstrap; scene-found fallback. May " +
                 "be null — drinking then consumes the water gracefully with no restore (no null-ref).")]
        public ThirstNeed thirst;

        [Tooltip("The SHIPPED atomic berry eat-seam (consume one berry + AddFood). Reused VERBATIM so a left-click " +
                 "eat is the SAME atomic consume+restore as the old E-eat — no re-implemented restore. Wired at " +
                 "bootstrap; scene-found fallback. Null is tolerated — the eat branch then no-ops (no null-ref).")]
        public EatBerryAction eatSeam;

        [Tooltip("The inventory/belt UI for the over-UI left-click guard (CHANGE: a click OVER the belt/pack must " +
                 "NOT consume the item behind it; the consume asks IsPointerOverUI — mirrors chop). Wired at " +
                 "bootstrap; scene-found fallback. Null is tolerated — the over-UI guard is then skipped (a bare " +
                 "test rig with no UI), but the modal-panel + RMB-orbit guards still apply.")]
        public InventoryUI inventoryUI;

        // The one-shot programmatic LEFT-CLICK latch (the input-independent seam, the analog of
        // ChopTree.RequestChopClick). A headless PlayMode run / the shipped-build capture can't inject a real
        // mouse button, so a consume-click is REQUESTED via this latch (consumed once on the next Update).
        private bool _useClickRequested;

        private bool _tracedFirstEat;
        private bool _tracedFirstDrink;
        private bool _tracedFirstNoop;

        void Awake()
        {
            // Serialized refs are the source of truth (BootstrapProject wires them editor-time). These are a
            // build-safety net only — never the path the shipped build relies on.
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (hunger == null) hunger = FindObjectOfType<HungerNeed>();
            if (thirst == null) thirst = FindObjectOfType<ThirstNeed>();
            if (eatSeam == null) eatSeam = FindObjectOfType<EatBerryAction>();
            if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();
        }

        void Update()
        {
            // Edge-triggered on a left-click (one consume per click — eating/drinking is one-shot, NOT hold-to-
            // repeat like chop). The one-shot RequestUseClick latch also counts as a single click (headless +
            // capture seam). Legacy Input.* (the project is activeInputHandler=0 — unity-conventions.md §Input).
            bool clickLatch = _useClickRequested;
            _useClickRequested = false;
            if (!Input.GetMouseButtonDown(0) && !clickLatch) return;

            TryConsumeSelected();
        }

        /// <summary>
        /// Attempt to CONSUME the selected belt item on a left-click (AC1 — the use-dispatch). Reads the SELECTED
        /// belt slot's item, checks the guard truth-table, and routes by item TYPE: berry → eat (AddFood), water →
        /// drink (AddWater). A non-consumable selection (axe / empty / resource) is a clean no-op (the axe is
        /// handled by ChopTree's disjoint branch; this never touches it). Returns true iff a unit was actually
        /// consumed. Public + scene-free so the PlayMode test drives it without a real mouse.
        /// </summary>
        public bool TryConsumeSelected()
        {
            if (inventory == null) return false;

            ItemStack selected = inventory.Model.SelectedBeltStack;
            bool overUI = inventoryUI != null && inventoryUI.IsPointerOverUI(Input.mousePosition);
            bool rmbHeld = Input.GetMouseButton(1);

            if (!ShouldConsumeOnClick(selected, UiInputGate.CaptureWorldInput, overUI, rmbHeld))
            {
                // A left-click with nothing / a non-consumable (axe — ChopTree's branch / a resource) selected,
                // or while a guard holds (panel open / over UI / RMB orbit): clean no-op, no error (AC1).
                if (!_tracedFirstNoop)
                {
                    _tracedFirstNoop = true;
                    ConsumeTrace("left-click with no consumable selected (or guard held) -> no-op");
                }
                return false;
            }

            string id = selected.Def.Id;

            if (id == ItemCatalog.BerryId)
            {
                // Reuse the SHIPPED atomic berry eat-seam VERBATIM (consume one berry + AddFood). It is
                // selection-agnostic (draws a berry from any slot), which is fine: a berry IS the selected belt
                // item here (the dispatch guaranteed it), and the all-or-nothing guard is preserved by the seam.
                bool eaten = eatSeam != null
                    ? eatSeam.TryEatOneBerry()
                    : TryEatOneBerryDirect();   // graceful fallback if the shipped seam isn't wired (a bare rig)
                if (eaten && !_tracedFirstEat)
                {
                    _tracedFirstEat = true;
                    ConsumeTrace("ate 1 berry via left-click -> berries=" +
                                 inventory.Model.CountItem(ItemCatalog.BerryId) +
                                 (hunger != null ? ", hunger=" + hunger.Current01.ToString("F2") : ""));
                }
                return eaten;
            }

            if (id == ItemCatalog.WaterId)
            {
                bool drank = TryDrinkOneWater();
                if (drank && !_tracedFirstDrink)
                {
                    _tracedFirstDrink = true;
                    ConsumeTrace("drank 1 water via left-click -> water=" +
                                 inventory.Model.CountItem(ItemCatalog.WaterId) +
                                 (thirst != null ? ", thirst=" + thirst.Current01.ToString("F2") : ""));
                }
                return drank;
            }

            // A belt-eligible kind we don't have an effect for (defensive — only berry+water are consumables
            // today). No-op rather than error (AC1: non-consumable selected → nothing).
            return false;
        }

        /// <summary>
        /// PURE consume-on-a-left-click decision (the unit-testable guard truth-table, the analog of
        /// <see cref="ChopTree.ShouldChopOnClick"/>). Given a left-click edge, decide whether a CONSUME should
        /// fire on the <paramref name="selected"/> belt stack:
        ///   • the selected stack is non-empty AND a <see cref="ItemKind.Consumable"/> (the dispatch discriminant —
        ///     so the axe / a resource / an empty slot never consumes; the axe is ChopTree's disjoint branch);
        ///   • NOT <paramref name="uiPanelOpen"/> — no modal panel owns the screen;
        ///   • NOT <paramref name="pointerOverUI"/> — the click is NOT over the inventory/belt UI;
        ///   • NOT <paramref name="rmbHeld"/> — the right mouse button is NOT held (no camera-orbit drag).
        /// All must hold. Static + dependency-free so the EditMode guard asserts the whole table with no
        /// scene/Input/UI rig. NOTE: the selected item's COUNT is NOT a gate here — a zero/empty stack is already
        /// excluded by IsEmpty, and the per-effect consume seams are all-or-nothing (a 0-count consume no-ops),
        /// so a double-consume on one click is impossible (the consume removes exactly one unit atomically).
        /// </summary>
        public static bool ShouldConsumeOnClick(ItemStack selected, bool uiPanelOpen,
                                                bool pointerOverUI, bool rmbHeld)
            => !selected.IsEmpty
               && selected.Def.Kind == ItemKind.Consumable
               && !uiPanelOpen && !pointerOverUI && !rmbHeld;

        /// <summary>
        /// Request ONE consume strike programmatically — the input-independent analog of a left-click (the analog
        /// of <see cref="ChopTree.RequestChopClick"/>). Latched + consumed on the next Update (one consume per
        /// call), so a headless PlayMode test + the shipped-build capture trigger a consume where a real mouse
        /// button can't be injected. The selected-consumable + over-UI/RMB/panel guards still apply.
        /// </summary>
        public void RequestUseClick() => _useClickRequested = true;

        /// <summary>
        /// Drink ONE water unit: consume exactly one <see cref="ItemCatalog.WaterId"/> from the inventory AND
        /// restore thirst, atomically, via the SHIPPED <see cref="ThirstNeed.AddWater"/> (NOT re-implemented).
        /// All-or-nothing: RemoveItem debits nothing + returns false when no water is held, and the restore only
        /// runs on a successful consume — a unit is never removed without its restore, nor restored without the
        /// removal. Graceful when no <see cref="ThirstNeed"/> is wired (consume only, no restore, no null-ref).
        /// Public + scene-free so the PlayMode test drives it. (The pond's proximity-drink — 86caamkv7 — is
        /// REMOVED in this ticket; drinking is now belt-item-driven.)
        /// </summary>
        public bool TryDrinkOneWater()
        {
            if (inventory == null) return false;
            // All-or-nothing consume: RemoveItem returns false + debits nothing when no water is held.
            if (!inventory.Model.RemoveItem(ItemCatalog.WaterId, 1)) return false;
            if (thirst != null) thirst.AddWater(); // SHIPPED per-scoop restore (waterScoopAmount) — not re-implemented
            return true;
        }

        // Graceful direct berry-eat fallback used ONLY when the shipped EatBerryAction seam isn't wired (a bare
        // test rig). Mirrors EatBerryAction.TryEatOneBerry's atomic path so behaviour is identical: consume one
        // berry, restore via the SHIPPED HungerNeed.TryEatBerry (all-or-nothing). The shipped seam is preferred in
        // the build (wired by BootstrapProject) so this is a safety net, never the primary path.
        private bool TryEatOneBerryDirect()
        {
            if (inventory == null) return false;
            System.Func<bool> consumeOneBerry = () => inventory.Model.RemoveItem(ItemCatalog.BerryId, 1);
            return hunger != null ? hunger.TryEatBerry(consumeOneBerry) : consumeOneBerry();
        }

        // [consume-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call +
        // its argument evaluation (the string concat) from the shipped IL2CPP release exe so the trace costs the
        // player nothing (unity6-mastery §5/§10). The first-time guards keep it one-shot.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ConsumeTrace(string msg) => Debug.Log("[consume-trace] " + msg);
    }
}
