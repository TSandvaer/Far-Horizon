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
    ///   • AC2 — a belt hotbar (default 5 slots) at bottom-center; number keys 1–N and mouse-scroll select;
    ///           the selected slot wears the ember-gold rim (.slot--selected) + a small upward lift.
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

            // AC2 — mouse-scroll cycles the selected belt slot (wraps at the ends).
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0.01f) _model.CycleBelt(-1);   // scroll up -> previous slot
            else if (scroll < -0.01f) _model.CycleBelt(+1); // scroll down -> next slot

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
            BuildSlotRow(_beltDock, _beltDockSlots, _model.BeltSlots.Count, SlotArea.Belt, showHotkey: true);
            BuildSlotRow(_beltStrip, _beltStripSlots, _model.BeltSlots.Count, SlotArea.Belt, showHotkey: true);
        }

        private void BuildSlotRow(VisualElement container, List<VisualElement> cache, int count,
                                  SlotArea area, bool showHotkey)
        {
            if (container == null) return;
            container.Clear();
            cache.Clear();
            for (int i = 0; i < count; i++)
            {
                var slot = MakeSlot(area, i, showHotkey);
                container.Add(slot);
                cache.Add(slot);
            }
        }

        private VisualElement MakeSlot(SlotArea area, int index, bool showHotkey)
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

            // Click selects a belt slot (a tap with no drag); drag starts a move (AC6).
            slot.RegisterCallback<PointerDownEvent>(e => OnSlotPointerDown(e, slotRef, slot));
            slot.RegisterCallback<PointerEnterEvent>(e => OnSlotPointerEnter(slotRef, slot));
            slot.RegisterCallback<PointerLeaveEvent>(e => OnSlotPointerLeave(slot));
            slot.RegisterCallback<PointerUpEvent>(e => OnSlotPointerUp(e, slotRef, slot));

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

        private void OnSlotPointerDown(PointerDownEvent e, SlotRef slotRef, VisualElement slot)
        {
            if (_model == null) return;
            ItemStack here = _model.At(slotRef);

            // A tap on a belt slot selects it (AC2). Selection is harmless even on an empty slot.
            if (slotRef.Area == SlotArea.Belt)
                _model.SelectBelt(slotRef.Index);

            // Begin a drag only if the slot holds something.
            if (here.IsEmpty) return;
            _dragging = true;
            _dragFrom = slotRef;
            _dragDef = here.Def;
            ShowGhost(here.Def);
            slot.CapturePointer(e.pointerId);
        }

        private void OnSlotPointerEnter(SlotRef slotRef, VisualElement slot)
        {
            if (!_dragging) return;
            // Preview the drop validity: a resource over a belt slot denies (AC6 tool-vs-resource).
            bool deny = slotRef.Area == SlotArea.Belt && !ItemDef.IsBeltEligible(_dragDef);
            slot.EnableInClassList("slot--drop-deny", deny);
            slot.EnableInClassList("slot--drop-ok", !deny);
        }

        private void OnSlotPointerLeave(VisualElement slot)
        {
            slot.RemoveFromClassList("slot--drop-ok");
            slot.RemoveFromClassList("slot--drop-deny");
        }

        private void OnSlotPointerUp(PointerUpEvent e, SlotRef slotRef, VisualElement slot)
        {
            if (!_dragging) return;
            slot.ReleasePointer(e.pointerId);
            HideGhost();

            // The move goes THROUGH the model (TryMove enforces the tool-vs-resource gate — the view never
            // bypasses it). A rejected move (resource→belt) is a no-op; the model state is the truth.
            _model.TryMove(_dragFrom, slotRef);

            _dragging = false;
            _dragDef = null;
            RefreshAll();
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

        private void PositionGhostAtMouse()
        {
            if (_dragGhost == null || _root == null) return;
            // Convert screen-space mouse (origin bottom-left, Y up) to panel-space (origin top-left, Y down).
            Vector2 m = Input.mousePosition;
            float panelY = Screen.height - m.y;
            _dragGhost.style.left = m.x - 28f;
            _dragGhost.style.top = panelY - 28f;
        }
    }
}
