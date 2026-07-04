using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarHorizon
{
    /// <summary>
    /// The UI Toolkit view for the inventory pack + belt hotbar (ticket 86caa4bya — AC1/AC2/AC4/AC6/AC7).
    /// THE THIN VIEW: all data + correctness lives in <see cref="InventoryModel"/> (pure C#, EditMode-
    /// tested); this MonoBehaviour renders slot wells, routes input, and drives drag/drop THROUGH the
    /// model's mutators (TryMove enforces the tool-vs-resource gate — the view never bypasses it).
    ///
    /// What it does:
    ///   • AC1 — Tab opens/closes the inventory pack (a 5×4 grid by default; count follows the model).
    ///   • AC2 — a belt hotbar (default 5 slots) at bottom-center; number keys 1–N (or a tap on the bottom
    ///           strip) select; the selected slot wears the ember-gold rim (.slot--selected) + a small upward
    ///           lift. (The mouse-scroll -> belt binding was REMOVED in 86cabh907 — the wheel is camera zoom.)
    ///   • AC4 — selection drives the held item via the model (HeldAxe reads Inventory.IsAxeSelectedInBelt);
    ///           this view only visualizes the selection — the in-world axe coherence is the model's.
    ///   • AC6 — drag/move among inventory slots, down into the belt, reassign belt slots — every move goes
    ///           through InventoryModel.TryMove, which REJECTS a resource dropped on the belt (deny-glow).
    ///   • AC7 — stack-count badge at count >= 2 (gold-tinted at the cap); tools show no badge.
    ///
    /// Input is legacy Input.* (the project is activeInputHandler=0 — unity-conventions.md §Input). UI
    /// Toolkit panels do NOT block the legacy Input.* polling other systems read; the belt scroll/number
    /// keys here are read directly. (When the settings panel PR #83 lands its shared UiInputGate, the
    /// reconciliation PR routes both modal panels through it so locomotion is swallowed while open —
    /// cross-lane follow-up; on main today WasdMovement reads no gate, so the inventory does not regress it.)
    /// The belt number-keys here are read directly; the wheel is NOT read here (it is camera zoom).
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): the UIDocument + UXML/USS + the Inventory
    /// reference are wired editor-time (MovementCameraScene) + serialized into Boot.unity. The Awake
    /// fallbacks are build-safety nets. NO mutable statics (the StaticStateResetTests audit stays green).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InventoryUI : MonoBehaviour
    {
        [Header("UI Toolkit assets (wired editor-time, serialized)")]
        [Tooltip("The UIDocument hosting the inventory UI. Auto-resolved from this GameObject if unset.")]
        public UIDocument document;
        [Tooltip("The inventory shell UXML (InventoryPanel.uxml). Wired editor-time so it serializes.")]
        public VisualTreeAsset panelUxml;
        [Tooltip("The shared carved-wood palette (InventoryPalette.uss).")]
        public StyleSheet paletteUss;
        [Tooltip("The inventory/belt styling (InventoryPanel.uss).")]
        public StyleSheet panelUss;

        [Header("Data")]
        [Tooltip("The inventory whose model this view renders + drives. Wired editor-time; scene-found fallback.")]
        public Inventory inventory;

        [Header("Toggle")]
        [Tooltip("Key that opens/closes the inventory pack. Tab per AC1 (free — no clash with the input map).")]
        public KeyCode toggleKey = KeyCode.Tab;

        /// <summary>Whether the inventory pack is currently open (Tab). Exposed for tests + future input-gate.</summary>
        public bool IsOpen { get; private set; }

        private InventoryModel _model;
        private VisualElement _root;
        private VisualElement _scrim;
        private VisualElement _panel;
        private VisualElement _invGrid;     // the pack grid container
        private VisualElement _beltDock;    // the belt row docked inside the open pack
        private VisualElement _beltStrip;   // the always-on bottom-center belt strip
        private VisualElement _dragGhost;

        // Slot-view caches so RefreshAll repaints existing elements rather than rebuilding the tree.
        private readonly List<VisualElement> _invSlots = new List<VisualElement>();
        private readonly List<VisualElement> _beltDockSlots = new List<VisualElement>();
        private readonly List<VisualElement> _beltStripSlots = new List<VisualElement>();

        private bool _built;

        // Active drag state (pointer-driven, AC6).
        private bool _dragging;
        private SlotRef _dragFrom;
        private ItemDef _dragDef;

        /// <summary>USS class that hides a slot's item content (icon/chip/badge) while it is the SOURCE of an
        /// active drag — so the item reads in ONE place (the #drag-ghost), not two (#90 dup-fix).</summary>
        public const string DraggingSourceClass = "slot--dragging-source";

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
        }

        void OnEnable()
        {
            if (inventory != null)
            {
                _model = inventory.Model;
                inventory.Changed += RefreshAll;
            }
            BuildView();
            SetOpen(false);     // start closed — Tab opens the pack
            RefreshAll();
        }

        void OnDisable()
        {
            if (inventory != null) inventory.Changed -= RefreshAll;
        }

        void Update()
        {
            // Tab toggles the pack (legacy Input — the project is activeInputHandler=0).
            if (Input.GetKeyDown(toggleKey)) SetOpen(!IsOpen);

            if (_model == null) return;

            // AC2 — number keys 1..N select the matching belt slot (range FOLLOWS belt-slot-count; key N
            // is a no-op when the belt has fewer than N slots — the reduced-belt boundary, S7).
            int beltCount = _model.BeltSlots.Count;
            for (int i = 0; i < beltCount && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    _model.SelectBelt(i);
                    break;
                }
            }

            // Belt selection is NUMBER-KEYS-ONLY (86cabh907 dial-tool round, Sponsor blocker #2). The
            // mouse-scroll -> belt-slot binding was REMOVED: the wheel is the camera ZOOM (OrbitCamera reads
            // Input.mouseScrollDelta on the same axis), so scrolling to zoom ALSO walked the belt selection —
            // a conflict the Sponsor flagged as "not necessary." The wheel now only zooms; belt slots select
            // via 1..N (above) and a tap on the bottom hotbar strip (BuildSlotRow selectsOnTap). InventoryModel
            // .CycleBelt stays (used by tests + future rebindable input), it is just no longer wired to scroll.

            // Track the drag ghost to the cursor while dragging.
            if (_dragging && _dragGhost != null) PositionGhostAtMouse();
        }

        // ============================================================================================
        // View construction.
        // ============================================================================================

        private void BuildView()
        {
            if (_built || document == null) return;

            var rootVisual = document.rootVisualElement;
            if (rootVisual == null) return;

            // Instantiate the UXML shell (or build a minimal tree if the asset wasn't wired — build-safety).
            if (panelUxml != null)
            {
                rootVisual.Clear();
                panelUxml.CloneTree(rootVisual);
            }
            if (paletteUss != null && !rootVisual.styleSheets.Contains(paletteUss)) rootVisual.styleSheets.Add(paletteUss);
            if (panelUss != null && !rootVisual.styleSheets.Contains(panelUss)) rootVisual.styleSheets.Add(panelUss);

            _root = rootVisual.Q<VisualElement>("root") ?? rootVisual;
            _scrim = rootVisual.Q<VisualElement>("inv-scrim");
            _panel = rootVisual.Q<VisualElement>("inv-panel");
            _invGrid = rootVisual.Q<VisualElement>("inv-grid");
            _beltDock = rootVisual.Q<VisualElement>("belt-dock");
            _beltStrip = rootVisual.Q<VisualElement>("belt-bar-strip");
            _dragGhost = rootVisual.Q<VisualElement>("drag-ghost");

            BuildSlots();
            _built = true;
        }

        private void BuildSlots()
        {
            if (_model == null) return;

            // Pack grid.
            BuildSlotRow(_invGrid, _invSlots, _model.InventorySlots.Count, SlotArea.Inventory, showHotkey: false);
            // Docked belt row (inside the open pack) + the always-on bottom belt strip — both render the
            // SAME belt slots (direction §4.1 "dock-the-same-state, mirror visually").
            //
            // BUG 2 (#90): clicking the DOCKED belt row (inside the pack) must NOT change the active belt
            // selection — the pack's belt row is an ORGANIZE target (a drag dest), NOT a selector. Active
            // selection changes ONLY via 1–N / scroll (AC2). The bottom STRIP is the hotbar, so a tap THERE
            // still selects. We pass selectsOnTap=false for the docked row, true for the strip.
            BuildSlotRow(_beltDock, _beltDockSlots, _model.BeltSlots.Count, SlotArea.Belt,
                         showHotkey: true, selectsOnTap: false);
            BuildSlotRow(_beltStrip, _beltStripSlots, _model.BeltSlots.Count, SlotArea.Belt,
                         showHotkey: true, selectsOnTap: true);
        }

        private void BuildSlotRow(VisualElement container, List<VisualElement> cache, int count,
                                  SlotArea area, bool showHotkey, bool selectsOnTap = false)
        {
            if (container == null) return;
            container.Clear();
            cache.Clear();
            for (int i = 0; i < count; i++)
            {
                var slot = MakeSlot(area, i, showHotkey, selectsOnTap);
                container.Add(slot);
                cache.Add(slot);
            }
        }

        private VisualElement MakeSlot(SlotArea area, int index, bool showHotkey, bool selectsOnTap)
        {
            var slot = new VisualElement();
            slot.AddToClassList("slot");
            slot.pickingMode = PickingMode.Position;

            var icon = new VisualElement { name = "icon" };
            icon.AddToClassList("slot__icon");
            icon.pickingMode = PickingMode.Ignore;
            slot.Add(icon);

            var chip = new Label { name = "chip" };
            chip.AddToClassList("slot__chip");
            chip.pickingMode = PickingMode.Ignore;
            slot.Add(chip);

            if (showHotkey)
            {
                var hk = new Label { name = "hotkey", text = (index + 1).ToString() };
                hk.AddToClassList("slot__hotkey");
                hk.pickingMode = PickingMode.Ignore;
                slot.Add(hk);
            }

            var badge = new Label { name = "badge" };
            badge.AddToClassList("slot__badge");
            badge.pickingMode = PickingMode.Ignore;
            slot.Add(badge);

            var slotRef = new SlotRef(area, index);

            // Drag starts a move (AC6); a tap on the bottom STRIP also selects (selectsOnTap) — the docked
            // pack belt row does NOT select (BUG 2, #90). PointerMove drives the deny/ok hover preview while
            // dragging (the captured-pointer redirect makes PointerEnter/Leave unreliable mid-drag — BUG 1).
            slot.RegisterCallback<PointerDownEvent>(e => OnSlotPointerDown(e, slotRef, slot, selectsOnTap));
            slot.RegisterCallback<PointerMoveEvent>(e => OnSlotPointerMove(e));
            slot.RegisterCallback<PointerUpEvent>(e => OnSlotPointerUp(e, slot));

            return slot;
        }

        // ============================================================================================
        // Open / close.
        // ============================================================================================

        /// <summary>Open/close the pack. Hidden via display:None (zero render cost — unity6-mastery §9),
        /// then play the "pops open" scale+fade on the laid-out panel.</summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (_scrim == null) return;
            _scrim.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open && _panel != null)
            {
                _panel.style.scale = new Scale(new Vector3(0.96f, 0.96f, 1f));
                _panel.style.opacity = 0f;
                _panel.schedule.Execute(() =>
                {
                    _panel.style.scale = new Scale(Vector3.one);
                    _panel.style.opacity = 1f;
                }).StartingIn(0);
            }
            RefreshAll();
        }

        // ============================================================================================
        // Repaint — render every slot from the model (called on Inventory.Changed + open/close).
        // ============================================================================================

        private void RefreshAll()
        {
            if (_model == null) return;
            PaintRow(_invSlots, _model.InventorySlots, SlotArea.Inventory);
            PaintRow(_beltDockSlots, _model.BeltSlots, SlotArea.Belt);
            PaintRow(_beltStripSlots, _model.BeltSlots, SlotArea.Belt);
        }

        private void PaintRow(List<VisualElement> slots, IReadOnlyList<ItemStack> stacks, SlotArea area)
        {
            for (int i = 0; i < slots.Count && i < stacks.Count; i++)
                PaintSlot(slots[i], stacks[i], area, i);
        }

        private void PaintSlot(VisualElement slot, ItemStack stack, SlotArea area, int index)
        {
            var icon = slot.Q<VisualElement>("icon");
            var chip = slot.Q<Label>("chip");
            var badge = slot.Q<Label>("badge");

            // Selected-belt highlight (AC2/AC4) — the gold rim + lift, only on the selected belt slot.
            bool selected = area == SlotArea.Belt && index == _model.SelectedBeltIndex;
            slot.EnableInClassList("slot--selected", selected);

            // Clear deny/ok hover-state classes when repainting (a stale drag class shouldn't persist).
            slot.RemoveFromClassList("slot--drop-ok");
            slot.RemoveFromClassList("slot--drop-deny");

            if (stack.IsEmpty)
            {
                if (icon != null) icon.style.backgroundImage = StyleKeyword.None;
                if (chip != null) chip.text = "";
                if (badge != null) badge.style.display = DisplayStyle.None;
                return;
            }

            // Icon (rendered prop sprite) OR a warm-cream letter-chip fallback (direction §6.3).
            if (icon != null && chip != null)
            {
                if (stack.Def.Icon != null)
                {
                    icon.style.backgroundImage = new StyleBackground(stack.Def.Icon);
                    chip.text = "";
                }
                else
                {
                    icon.style.backgroundImage = StyleKeyword.None;
                    chip.text = LetterChip(stack.Def);
                }
            }

            // Stack badge (AC7) — only at count >= 2; tools never show one (cap 1). Gold-tinted at cap.
            if (badge != null)
            {
                if (ItemDef.IsStackable(stack.Def) && stack.Count >= 2)
                {
                    badge.text = stack.Count.ToString();
                    badge.style.display = DisplayStyle.Flex;
                    badge.EnableInClassList("slot__badge--full", stack.Count >= stack.Def.MaxStack);
                }
                else
                {
                    badge.style.display = DisplayStyle.None;
                }
            }
        }

        // The first letter of the display name, upper-cased (A axe / W wood / S stone / B berries).
        private static string LetterChip(ItemDef def)
        {
            string n = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : def?.Id;
            return string.IsNullOrEmpty(n) ? "?" : n.Substring(0, 1).ToUpperInvariant();
        }

        // ============================================================================================
        // Pointer interaction — select (tap) + drag/drop (AC6).
        // ============================================================================================

        private void OnSlotPointerDown(PointerDownEvent e, SlotRef slotRef, VisualElement slot, bool selectsOnTap)
        {
            if (_model == null) return;
            ItemStack here = _model.At(slotRef);

            // BUG 2 (#90): a tap selects a belt slot ONLY on the bottom hotbar STRIP (selectsOnTap). The
            // docked belt row inside the pack is an ORGANIZE target, NOT a selector — clicking it must leave
            // the active selection unchanged (the selection moves only via 1–N / scroll). Selection is
            // harmless even on an empty slot.
            if (selectsOnTap && slotRef.Area == SlotArea.Belt)
                _model.SelectBelt(slotRef.Index);

            // Begin a drag only if the slot holds something.
            if (here.IsEmpty) return;
            BeginDrag(slotRef);
            slot.CapturePointer(e.pointerId);
        }

        /// <summary>
        /// Start a drag from <paramref name="slotRef"/>: arm the drag state, raise the #drag-ghost carrying
        /// the item, and DIM the source slot's content so the item reads in ONE place (the ghost), not two —
        /// the #90 "duplicate" the Sponsor saw holding the mouse on the berries. Public lifecycle seam so an
        /// EditMode/PlayMode test can drive a drag without synthesizing a captured-pointer device. No-op if
        /// the model is missing or the slot is empty.
        /// </summary>
        public void BeginDrag(SlotRef slotRef)
        {
            if (_model == null) return;
            ItemStack here = _model.At(slotRef);
            if (here.IsEmpty) return;
            _dragging = true;
            _dragFrom = slotRef;
            _dragDef = here.Def;
            ShowGhost(here.Def);
            // Dim EVERY view of the source ref (a belt slot is mirrored in the dock + the strip — dimming
            // just the clicked element would still leave the item drawn in the mirror).
            SetSourceDim(slotRef, true);
        }

        // BUG 1 (#90): while dragging, the SOURCE slot has pointer capture, so PointerMove fires on the
        // SOURCE (not the slot under the cursor). Resolve the hovered slot by hit-testing the cursor against
        // the slot caches each move + repaint its deny/ok preview — PointerEnter/Leave do NOT fire mid-drag
        // on the non-captured slots, so they cannot drive the preview (the old approach silently no- op'd).
        private void OnSlotPointerMove(PointerMoveEvent e)
        {
            if (!_dragging) return;
            ClearDropPreview();
            if (TryResolveSlotAt(e.position, out _, out VisualElement target, out SlotArea area))
            {
                bool deny = area == SlotArea.Belt && !ItemDef.IsBeltEligible(_dragDef);
                target.EnableInClassList("slot--drop-deny", deny);
                target.EnableInClassList("slot--drop-ok", !deny);
            }
        }

        private void OnSlotPointerUp(PointerUpEvent e, VisualElement slot)
        {
            if (!_dragging) return;
            slot.ReleasePointer(e.pointerId);
            EndDrag(e.position);
        }

        /// <summary>
        /// End the active drag with a drop at <paramref name="dropPosition"/> (panel space): hide the ghost,
        /// resolve + apply the move by POSITION (BUG 1 — the captured-pointer redirect makes the event target
        /// the SOURCE, so position is the authoritative drop target), then RESTORE the source slot's content
        /// — on a LANDED drop (RefreshAll repaints the now-empty source + the filled dest) AND on a CANCEL /
        /// drop-outside (the item never left, so the source must read full again). Clearing the dim before
        /// RefreshAll guarantees no permanent dim survives whether or not the move landed (no item lost, no
        /// stuck dim). Public lifecycle seam (pairs with <see cref="BeginDrag"/>). Returns true iff a move
        /// actually landed.
        /// </summary>
        public bool EndDrag(Vector2 dropPosition)
        {
            if (!_dragging) return false;
            HideGhost();
            ClearDropPreview();
            bool moved = ApplyDrop(_dragFrom, dropPosition);
            SetSourceDim(_dragFrom, false);
            _dragging = false;
            _dragDef = null;
            RefreshAll();
            return moved;
        }

        /// <summary>
        /// Resolve the drop target under <paramref name="panelPos"/> and route the move through the model
        /// (BUG 1 #90 — the captured-pointer redirect prevents using the PointerUp event target). The move
        /// goes THROUGH InventoryModel.TryMove, which enforces the tool-vs-resource gate (a resource onto
        /// the belt is rejected — the view never bypasses it). Returns true if a real move landed. PUBLIC +
        /// pure-ish (only reads layout + mutates the model) so an EditMode test can assert drop resolution
        /// without a live pointer device.
        /// </summary>
        public bool ApplyDrop(SlotRef from, Vector2 panelPos)
        {
            if (_model == null) return false;
            if (!TryResolveSlotAt(panelPos, out SlotRef to, out _, out _)) return false;
            return _model.TryMove(from, to);
        }

        // Hit-test a panel-space position against the painted slot caches, returning the SlotRef it lands on
        // (inventory grid first, then the docked belt row, then the bottom strip). worldBound is in panel
        // coordinates (same space as PointerEvent.position), so Contains is the authoritative hit test even
        // while the source slot holds pointer capture.
        private bool TryResolveSlotAt(Vector2 panelPos, out SlotRef slotRef, out VisualElement element, out SlotArea area)
        {
            if (TryHit(_invSlots, SlotArea.Inventory, panelPos, out slotRef, out element)) { area = SlotArea.Inventory; return true; }
            if (TryHit(_beltDockSlots, SlotArea.Belt, panelPos, out slotRef, out element)) { area = SlotArea.Belt; return true; }
            if (TryHit(_beltStripSlots, SlotArea.Belt, panelPos, out slotRef, out element)) { area = SlotArea.Belt; return true; }
            slotRef = default; element = null; area = SlotArea.Inventory;
            return false;
        }

        private static bool TryHit(List<VisualElement> slots, SlotArea area, Vector2 panelPos,
                                   out SlotRef slotRef, out VisualElement element)
        {
            int idx = HitIndex(slots, panelPos);
            if (idx >= 0)
            {
                slotRef = new SlotRef(area, idx);
                element = slots[idx];
                return true;
            }
            slotRef = default; element = null;
            return false;
        }

        /// <summary>
        /// Pure hit-test over slot elements (BUG 1 #90): the index of the first slot whose worldBound
        /// contains <paramref name="panelPos"/>, or -1. Delegates to the rect overload so the resolution
        /// logic is unit-testable WITHOUT a laid-out panel (feed synthetic rects in EditMode).
        /// </summary>
        public static int HitIndex(IReadOnlyList<VisualElement> slots, Vector2 panelPos)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] != null && slots[i].worldBound.Contains(panelPos))
                    return i;
            return -1;
        }

        /// <summary>
        /// Pure rect hit-test (BUG 1 #90 regression seam): the index of the first rect containing
        /// <paramref name="panelPos"/>, or -1. Static + side-effect-free + layout-free so an EditMode test
        /// asserts the position→index resolution the captured-pointer drop relies on with SYNTHETIC rects
        /// (no live panel needed). The element overload above feeds it each slot's worldBound.
        /// </summary>
        public static int HitIndex(IReadOnlyList<Rect> rects, Vector2 panelPos)
        {
            for (int i = 0; i < rects.Count; i++)
                if (rects[i].Contains(panelPos))
                    return i;
            return -1;
        }

        // ============================================================================================
        // Source-slot dim during drag (#90 dup-fix).
        // ============================================================================================

        /// <summary>
        /// Toggle the source-dim class on EVERY painted view of <paramref name="slotRef"/> (#90 dup-fix). A
        /// belt slot is mirrored in BOTH the docked row and the bottom strip — so a belt drag must dim both
        /// mirrors or the item still draws in the un-dimmed one. Inventory slots have a single view. Public +
        /// element-collection-driven so an EditMode test can assert the class lands on the source's view(s).
        /// </summary>
        private void SetSourceDim(SlotRef slotRef, bool on)
        {
            foreach (var el in SlotViews(slotRef))
                el?.EnableInClassList(DraggingSourceClass, on);
        }

        /// <summary>Every painted VisualElement that renders <paramref name="slotRef"/>: the single grid cell
        /// for an inventory ref, or BOTH the dock + strip cells for a belt ref (the two mirrors).</summary>
        private IEnumerable<VisualElement> SlotViews(SlotRef slotRef)
        {
            if (slotRef.Area == SlotArea.Inventory)
            {
                if (slotRef.Index >= 0 && slotRef.Index < _invSlots.Count) yield return _invSlots[slotRef.Index];
            }
            else
            {
                if (slotRef.Index >= 0 && slotRef.Index < _beltDockSlots.Count) yield return _beltDockSlots[slotRef.Index];
                if (slotRef.Index >= 0 && slotRef.Index < _beltStripSlots.Count) yield return _beltStripSlots[slotRef.Index];
            }
        }

        /// <summary>Test seam (#90 dup-fix): true iff ANY painted view of <paramref name="slotRef"/> currently
        /// carries the source-dim class. Lets an EditMode/PlayMode test assert dim-on-drag + restore-on-end
        /// without poking USS-computed styles.</summary>
        public bool IsSourceDimmed(SlotRef slotRef)
        {
            foreach (var el in SlotViews(slotRef))
                if (el != null && el.ClassListContains(DraggingSourceClass)) return true;
            return false;
        }

        private void ClearDropPreview()
        {
            ClearPreview(_invSlots);
            ClearPreview(_beltDockSlots);
            ClearPreview(_beltStripSlots);
        }

        private static void ClearPreview(List<VisualElement> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;
                slots[i].RemoveFromClassList("slot--drop-ok");
                slots[i].RemoveFromClassList("slot--drop-deny");
            }
        }

        // ============================================================================================
        // Drag ghost.
        // ============================================================================================

        private void ShowGhost(ItemDef def)
        {
            if (_dragGhost == null) return;
            if (def != null && def.Icon != null)
                _dragGhost.style.backgroundImage = new StyleBackground(def.Icon);
            else
                _dragGhost.style.backgroundImage = StyleKeyword.None;
            _dragGhost.style.display = DisplayStyle.Flex;
            PositionGhostAtMouse();
        }

        private void HideGhost()
        {
            if (_dragGhost != null) _dragGhost.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Verification seam: raise the #drag-ghost carrying <paramref name="def"/> WITHOUT arming a drag, so
        /// <see cref="Update"/>'s per-frame <c>PositionGhostAtMouse</c> tracking does NOT fire (that only runs
        /// while <c>_dragging</c>) and a subsequent synthetic <see cref="PositionGhostAtScreenPoint"/> is NOT
        /// overwritten by the live OS cursor. This lets the shipped-build capture drive a KNOWN, on-screen
        /// cursor DETERMINISTICALLY — the old gate rode the live off-window OS cursor (Input.mousePosition),
        /// which is unstable/lagged in an automated launch and produced a false divergence (86cajrtr1). No
        /// gameplay caller: real drags go through <see cref="BeginDrag"/>.
        /// </summary>
        public void ShowGhostForVerification(ItemDef def) => ShowGhost(def);

        /// <summary>Half the 56px ghost — the offset that centers the ghost on the cursor (panel units;
        /// ScreenToPanel output is already in panel space, so the -28 is a panel-unit nudge, not screen px).</summary>
        private const float GhostHalfSize = 28f;

        private void PositionGhostAtMouse()
        {
            PositionGhostAtScreenPoint(Input.mousePosition);
        }

        /// <summary>
        /// Position the drag-ghost so its CENTER sits at <paramref name="screenPos"/> (screen space:
        /// <c>Input.mousePosition</c> convention — origin bottom-left, Y up). FLIP-THEN-CONVERT, in THIS exact
        /// order (the recurring-bug fix, 86caffw9h — the LAST time this should recur):
        ///   (1) flip Y manually FIRST — <c>RuntimePanelUtils.ScreenToPanel</c> expects a TOP-LEFT-origin input
        ///       (the official Unity 6000.0 docs example flips Y before the call), and
        ///   (2) ScreenToPanel applies the panel SCALE (PanelScaleMode.ScaleWithScreenSize, refRes 1920x1080).
        /// The OLD code did the flip but NEVER the scale, so at a non-1080p window (panel scale != 1) the ghost
        /// rendered scale× off the cursor — the "ghost jumps out into the world / far right of screen" the
        /// Sponsor reported. INVISIBLE at exactly 1920x1080 (scale ~= 1), which is why prior fixes that tested
        /// only at 1080p missed it. Do NOT remove the manual flip, add a second flip, or divide by scale by
        /// hand — any of those re-breaks it. Public so the shipped-build verify capture can drive a KNOWN cursor
        /// (no real mouse in an automated launch) through the SAME production path it asserts.
        /// </summary>
        public void PositionGhostAtScreenPoint(Vector2 screenPos)
        {
            if (_dragGhost == null || _dragGhost.panel == null) return;
            Vector2 sp = screenPos;
            sp.y = Screen.height - sp.y;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_dragGhost.panel, sp);
            _dragGhost.style.left = panelPos.x - GhostHalfSize;
            _dragGhost.style.top = panelPos.y - GhostHalfSize;
        }

        /// <summary>
        /// The drag-ghost's CENTER in PANEL space (origin top-left, Y down — the laid-out <c>worldBound</c>
        /// convention), or <c>null</c> if the ghost/panel isn't laid out yet. The shipped-build verify capture
        /// reads this back and asserts it ≈ the EXPECTED panel point for the cursor it drove in
        /// (<see cref="ExpectedGhostPanelCenter"/>) — proving the ghost tracks the cursor at a non-1080p
        /// resolution (86caffw9h). Both sides go through <c>RuntimePanelUtils.ScreenToPanel</c>, so the assert
        /// validates the production conversion end-to-end (no PanelToScreen needed — that API doesn't exist).
        /// </summary>
        public Vector2? GhostCenterPanelPoint()
        {
            if (_dragGhost == null || _dragGhost.panel == null) return null;
            Rect wb = _dragGhost.worldBound;                      // panel space (Y down), already laid out
            return new Vector2(wb.x + wb.width * 0.5f, wb.y + wb.height * 0.5f);
        }

        /// <summary>
        /// The EXPECTED panel-space center for a ghost positioned at <paramref name="screenPos"/> — the SAME
        /// flip-then-ScreenToPanel the production <see cref="PositionGhostAtScreenPoint"/> uses, WITHOUT the
        /// -28 nudge (so it returns the cursor's panel point, i.e. where the ghost CENTER should land). The
        /// verify capture compares this to <see cref="GhostCenterPanelPoint"/> at a non-1080p resolution; the
        /// pre-fix (scale-less) code would diverge here by the panel scale. <c>null</c> if no panel yet.
        /// </summary>
        public Vector2? ExpectedGhostPanelCenter(Vector2 screenPos)
        {
            if (_dragGhost == null || _dragGhost.panel == null) return null;
            Vector2 sp = screenPos;
            sp.y = Screen.height - sp.y;
            return RuntimePanelUtils.ScreenToPanel(_dragGhost.panel, sp);
        }

        /// <summary>Diagnostic (86cajrtr1) — dump the ghost's laid-out geometry (layout vs worldBound vs
        /// style.left/top, resolved scale + transform-origin). Logged by the shipped -verifyInvDragGhostPos
        /// gate ONLY on a divergence, so a future re-break is triageable from the CI log without a rebuild.</summary>
        public string GhostGeomDiag()
        {
            if (_dragGhost == null || _dragGhost.panel == null) return "ghost/panel null";
            var g = _dragGhost;
            var rs = g.resolvedStyle;
            string parentWb = g.parent != null ? g.parent.worldBound.ToString() : "no-parent";
            return "layout=" + g.layout + " worldBound=" + g.worldBound +
                   " styleL/T=(" + g.style.left + "," + g.style.top + ")" +
                   " resolvedScale=" + rs.scale + " transformOrigin=" + rs.transformOrigin +
                   " rsW/H=(" + rs.width + "," + rs.height + ")" + " parentWB=" + parentWb;
        }

        // ============================================================================================
        // Pointer-over-UI test (86caa4c5c CHANGE 1 — the left-click-chop UI guard).
        // ============================================================================================

        /// <summary>
        /// True when the SCREEN-space cursor (origin bottom-left, Y up — <c>Input.mousePosition</c> space) is
        /// over an interactive element of THIS inventory UI — the always-on bottom belt STRIP, or any slot of
        /// the open pack. The left-click chop (ChopTree, 86caa4c5c CHANGE 1) consults this so a click that
        /// lands on the belt/inventory UI selects/drags a slot rather than swinging the axe at a tree behind
        /// the panel. UI Toolkit panels do NOT block the legacy <c>Input.*</c> world-click polling (research
        /// §E1, same reason <see cref="UiInputGate"/> exists), so the world consumer must ASK the panel whether
        /// the pointer is over a pickable element. Uses the panel's hit-test (<c>panel.Pick</c>) against the
        /// painted slot caches — picking-mode-aware: only the slot wells (PickingMode.Position) register; the
        /// transparent scrim/labels (PickingMode.Ignore) do not. Returns false if the view isn't built yet (no
        /// panel) so a world click is never wrongly swallowed before the UI exists. Public so an EditMode test
        /// can drive the pure rect-overlap core (<see cref="ScreenPointOverlapsAnyRect"/>) without a live panel.
        /// </summary>
        public bool IsPointerOverUI(Vector2 screenPos)
        {
            if (_root == null || _root.panel == null) return false;
            // Screen (Y-up) -> panel (Y-down, SCALE-applied) — the SAME flip-then-convert PositionGhostAtMouse
            // uses (86caffw9h sibling fix). The slot worldBounds we hit-test below are in panel space (already
            // scaled by UI Toolkit layout), so the cursor MUST also be scaled to panel space or the hit-test is
            // off by the panel scale at any non-1080p window — wrongly chopping/consuming the world behind the
            // panel near the belt/pack edge, or swallowing a legit world click. FLIP first, then ScreenToPanel.
            Vector2 sp = new Vector2(screenPos.x, Screen.height - screenPos.y);
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, sp);

            // The always-on belt STRIP (interactive whether or not the pack is open).
            if (HitIndex(_beltStripSlots, panelPos) >= 0) return true;

            // The open pack's grid + docked belt row are only on-screen (and only pick) while the pack is open.
            if (IsOpen)
            {
                if (HitIndex(_invSlots, panelPos) >= 0) return true;
                if (HitIndex(_beltDockSlots, panelPos) >= 0) return true;
                // Over the open panel body itself (a click on the pack chrome must not chop the world behind it).
                if (_panel != null && _panel.worldBound.Contains(panelPos)) return true;
            }
            return false;
        }

        /// <summary>
        /// PURE screen-point-over-UI core (the unit-testable seam for <see cref="IsPointerOverUI"/>): given a
        /// SCREEN-space point (Y-up), the screen HEIGHT, the PANEL SCALE, and the panel-space rects of the
        /// interactive UI elements (Y-down), return true iff the converted point lands in ANY rect. This
        /// replicates <c>RuntimePanelUtils.ScreenToPanel</c>'s math headlessly: flip Y first, THEN divide by the
        /// panel scale (PanelScaleMode.ScaleWithScreenSize). Static + layout-free so the EditMode guard asserts
        /// the screen→panel flip+SCALE with SYNTHETIC rects (no live UIDocument). The scale term is the
        /// recurring-bug fix (86caffw9h): a scale=1 (1080p) test alone CANNOT catch the missing-scale flaw.
        /// </summary>
        /// <param name="panelScale">The panel's effective scale (screen px per panel unit). 1.0 at the
        /// reference resolution (1920x1080); ~1.333 at 2560x1440. A value &lt;= 0 is treated as 1 (no scale)
        /// so a degenerate call never divides by zero.</param>
        public static bool ScreenPointOverlapsAnyRect(Vector2 screenPos, float screenHeight, float panelScale,
                                                      IReadOnlyList<Rect> uiRects)
        {
            return HitIndex(uiRects, ScreenToPanelPoint(screenPos, screenHeight, panelScale)) >= 0;
        }

        /// <summary>
        /// PURE model of <c>RuntimePanelUtils.ScreenToPanel</c> under PanelScaleMode.ScaleWithScreenSize: given
        /// a SCREEN-space point (Y-UP, <c>Input.mousePosition</c> convention), the screen HEIGHT, and the panel
        /// SCALE (screen px per panel unit — 1.0 at the 1920x1080 reference, ~1.333 at 2560x1440), return the
        /// point in PANEL space (Y-DOWN, worldBound convention): FLIP Y first, THEN divide BOTH axes by the
        /// scale. This is THE recurring-bug math (86caffw9h / 86cajrtr1): the drag-ghost center, the
        /// pointer-over-UI hit-test, and the shipped -verifyInvDragGhostPos gate all rest on this exact
        /// flip-then-scale. Layout-free + static so an EditMode test pins it at BOTH scale 1.0 AND 1.333 — a
        /// scale=1-only test cannot catch a dropped scale term (invisible at exactly 1080p). Production
        /// <see cref="PositionGhostAtScreenPoint"/> defers to the ENGINE's ScreenToPanel (authoritative); this
        /// static documents + guards the intended relation the engine must satisfy.
        /// </summary>
        /// <param name="panelScale">Screen px per panel unit. A value &lt;= 0 is treated as 1 (no scale) so a
        /// degenerate call never divides by zero.</param>
        public static Vector2 ScreenToPanelPoint(Vector2 screenPos, float screenHeight, float panelScale)
        {
            float s = panelScale > 0f ? panelScale : 1f;
            return new Vector2(screenPos.x / s, (screenHeight - screenPos.y) / s);
        }
    }
}
