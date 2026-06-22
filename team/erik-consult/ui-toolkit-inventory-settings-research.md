# UI Toolkit — Inventory/Hotbar + Settings Panel (runtime, Unity 6)

## Question

What is the idiomatic Unity 6 UI Toolkit approach for (a) a drag-and-drop inventory grid + belt hotbar,
(b) a live-tunable settings panel with persistence, and (c) coexistence with the project's legacy Input
system — specifically number-key / mouse-scroll hotbar selection and a Tab-toggle inventory? What are
the gotchas that could bite Devon/Drew mid-implementation?

## Bottom Line

UI Toolkit is the right choice for both tickets (86caa4bya, 86caa4bqp) and runs cleanly alongside legacy
`Input` polling. The idiomatic runtime drag-and-drop pattern uses `PointerManipulator` / pointer events
with pull-based slot-overlap detection — **not** the editor drag-and-drop API, which is a different code
path. For the settings panel, bind via `[CreateProperty]` + `TwoWay` DataBinding to a ScriptableObject
that also writes through to PlayerPrefs; this gives immediate live effect AND cross-run persistence in one
pass. The single biggest implementation RED FLAG: reading UI Toolkit drag events through the editor API
(`DragAndDropManipulator` + `UnityEditor.DragAndDrop`) will NOT compile in a player build — runtime
drag-and-drop needs the pointer-event approach from scratch.

---

## Evidence

### E1 — Runtime event system + input coexistence
- **Source:** Unity 6 Manual, "Runtime UI event system and input handling,"
  [docs.unity3d.com/Manual/UIE-Runtime-Event-System.html](https://docs.unity3d.com/Manual/UIE-Runtime-Event-System.html) — Strong (official docs).
- UI Toolkit auto-detects whichever `Active Input Handling` is set in Player Settings and adapts; the
  legacy `Input Manager (Old)` path is fully supported. No `activeInputHandler` flip is required — the
  project's current legacy `Input` stance is fine as-is.
- Keyboard events in UI Toolkit are **dispatched via the event system** (KeyDownEvent, etc.) to the
  focused element, but `Input.GetKey` / `Input.GetAxis` are **polling** calls that bypass the event
  dispatcher entirely. They fire unconditionally every `Update` regardless of which UI element has
  focus. This means number keys 1–5, Shift (sprint), and mouse-scroll queries in `Update` will NOT
  be blocked or consumed by an open inventory or settings panel — they continue to fire even when UI
  is on screen.
- Confirmed by: [docs.unity3d.com/Manual/UIE-faq-event-and-input-system.html](https://docs.unity3d.com/Manual/UIE-faq-event-and-input-system.html)
  — the FAQ notes UI Toolkit uses the same `EventSystem` as uGUI; it does not intercept raw
  `Input.*` polling calls.

**IMPLICATION FOR 86caa4bya (inventory):** The legacy `Input.GetKeyDown(KeyCode.Tab)` toggle, number
keys 1–5, and `Input.GetAxis("Mouse ScrollWheel")` will all keep firing while the inventory is open.
Devon must gate them explicitly: ignore hotbar number-key selection when the inventory panel is open,
and suppress character locomotion input while inventory is open. A simple `bool _inventoryOpen` flag
(set in the panel's show/hide logic) gates the relevant `Input.*` reads in `Update`. Do NOT rely on UI
Toolkit panel focus to suppress them — it won't.

---

### E2 — Runtime drag-and-drop: PointerManipulator approach
- **Source:** Unity 6 Manual, "Create a drag-and-drop UI inside a custom Editor window,"
  [docs.unity3d.com/6000.6/Documentation/Manual/UIE-create-drag-and-drop-ui.html](https://docs.unity3d.com/6000.6/Documentation/Manual/UIE-create-drag-and-drop-ui.html)
  — Strong (official docs); **warning: the guide is editor-window scoped.**
- **Source:** GitHub, gamedev-resources, "create-a-runtime-inventory-with-UI-Toolkit,"
  [github.com/gamedev-resources/create-a-runtime-inventory-with-UI-Toolkit](https://github.com/gamedev-resources/create-a-runtime-inventory-with-UI-Toolkit)
  — Moderate (community reference implementation, widely cited).

The idiomatic runtime pattern:

1. **Extend `PointerManipulator`** (not the editor-only `DragAndDropManipulator`).
2. Register on the **draggable item element** (not the slot container):
   - `PointerDownEvent` — capture the pointer (`target.CapturePointer(evt.pointerId)`), record
     drag start position, lift the element visually (set `style.position = Position.Absolute`,
     move above other slots in paint order via `BringToFront()`).
   - `PointerMoveEvent` — translate the element (`style.left`, `style.top` or `transform.position`).
     Use `UsageHints.DynamicTransform` on the dragged element (unity6-mastery §9).
   - `PointerUpEvent` — release capture (`target.ReleasePointer`).
   - `PointerCaptureOutEvent` — **drop handler**: call `worldBound.Overlaps(slot.worldBound)` across
     all registered slot `VisualElement`s; snap to the closest overlapping slot, or return to origin
     if none. This is the pull-based approach from the official example — do NOT use
     `PointerEnterEvent` on each slot (fires during move = expensive; also triggers for every slot
     the cursor crosses).

3. **Ghost/drag-image:** clone the item's `VisualElement` at `PointerDown` and add it to the panel
   root at `Position.Absolute`; remove it at `PointerCaptureOut`. This avoids layout thrash on the
   actual slot.

4. **Stack-count label + icon:** each slot is a `VisualElement` with a child `Image` (`.sprite`)
   and a `Label` for stack count. Update the label text in C# when inventory data changes; no
   runtime binding needed here — event-driven push from inventory model is cheaper.

**GOTCHA — editor vs runtime drag API:** `UnityEditor.DragAndDrop` (used in the official drag guide's
editor example) **does not exist in runtime builds**. Any code path referencing that namespace will
fail to compile as a player. The pointer-event path above is the only valid runtime approach.

**GOTCHA — `BringToFront()` + atlas batching:** calling `BringToFront()` on the dragged element
reorganizes the visual tree and can break UI Toolkit's mesh batching temporarily. For a soak-phase PoC
this is fine; for a release build, consider a dedicated drag-layer overlay panel (a second `UIDocument`
at higher sort order) to hold the ghost element, keeping the slot panel's tree stable.

---

### E3 — Icon texture management: dynamic atlas
- **Source:** Unity 6 Manual, "Control textures of the dynamic atlas,"
  [docs.unity3d.com/6000.2/Documentation/Manual/UIE-control-textures-of-the-dynamic-atlas.html](https://docs.unity3d.com/6000.2/Documentation/Manual/UIE-control-textures-of-the-dynamic-atlas.html)
  — Strong (official docs).
- **Source:** Unity 6 Manual, "Optimizing performance,"
  [docs.unity3d.com/6000.4/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/optimizing-performance.html](https://docs.unity3d.com/6000.4/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/optimizing-performance.html)
  — Strong (official docs).

UI Toolkit's **dynamic atlas** auto-packs referenced textures at runtime — no manual Sprite Atlas
required for inventory icons at this scale (20 inventory slots + 5 belt slots = 25 possible icons;
the PoC uses only 2: axe + wood). The uber shader batches up to **8 textures per draw call**; with 2
item icons the PoC will batch comfortably in one draw call.

For production: if the item-icon count grows past 8 distinct sprites visible simultaneously in one
UIDocument, they split into multiple draw calls. Mitigate by packing all item icons into a **single
Sprite Atlas** (`Assets/Art/UI/ItemIcons.spriteatlasv2`) — Unity's dynamic atlas treats a packed sprite
atlas as one texture entry regardless of how many sprites it contains, keeping all icon-bearing slots
in one batch. The `unity6-mastery.md §9` rule (Sprite Atlas for static content + dynamic atlas for
runtime-generated) maps exactly here: item icons are static assets → Sprite Atlas.

Call `RuntimePanelUtils.ResetDynamicAtlas()` if the inventory undergoes heavy item churn (many icons
added/removed in a session) to prevent fragmentation.

---

### E4 — Settings panel: data binding + persistence
- **Source:** Unity 6 Manual, "Data binding best practices,"
  [docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/data-binding.html](https://docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/data-binding.html)
  — Strong (official docs).
- **Source:** Unity Learn, "UI Toolkit in Unity 6: Crafting Custom Controls with Data Bindings,"
  [learn.unity.com/tutorial/ui-toolkit-in-unity-6-crafting-custom-controls-with-data-bindings](https://learn.unity.com/tutorial/ui-toolkit-in-unity-6-crafting-custom-controls-with-data-bindings)
  — Strong (Unity-authored tutorial).

**Recommended persistence approach: ScriptableObject + PlayerPrefs write-through.**

Rationale: the ticket (86caa4bqp AC5) requires (a) live effect on the game param, (b) survive a
relaunch. A plain ScriptableObject asset solves (a) — it's a live C# object all systems can reference;
its runtime field changes are immediate. But SO changes are NOT persisted to disk at runtime (unless
`AssetDatabase.SaveAssets` is called, which is editor-only). So (b) requires an additional write to
PlayerPrefs on each setting change. The SO holds the live authoritative values; PlayerPrefs holds the
persisted copy; at startup the SO is initialized from PlayerPrefs (or falls back to defaults baked
into the asset).

**Do NOT use `TwoWay` DataBinding to drive the SO property AND separately save to PlayerPrefs in two
places** — it creates a split authority problem. Instead:

- The SO exposes each setting as a `[CreateProperty]` property whose setter both applies the live
  effect AND writes to PlayerPrefs in one call.
- The slider/field is bound `TwoWay` to the SO property — so dragging the slider calls the property
  setter, which propagates live + persists in one step.

This keeps the AC2 "live update" and AC5 "persist across runs" in the same code path with no extra
coordination layer.

**UXML authoring note:** leave `data-source` unresolved in UXML (do not bake the SO reference into
the UXML attribute). Assign `element.dataSource = settingsSO` in C# at `Awake`/`Start`. This keeps
the SO swappable and testable (AC6 can inject a test SO instance).

**`[CreateProperty]` compile-time check:** the attribute is available in `Unity.Properties` (included
in Unity 6 with no additional package). It generates binding code at compile time — no reflection cost
per frame. Marking a property `[CreateProperty]` without `[SerializeField, DontCreateProperty]` on the
backing field is the common omission that silently falls back to reflection; check both annotations.

**Binding update cadence:** bindings update every frame by default. For a settings panel (infrequently
visible; only a handful of values), this is fine and requires no optimization.

---

### E5 — Input key assignment: Tab conflict
- **Source:** Unity 6 Manual, FAQ for input and event systems,
  [docs.unity3d.com/Manual/UIE-faq-event-and-input-system.html](https://docs.unity3d.com/Manual/UIE-faq-event-and-input-system.html)
  — Strong (official docs).

The UI Toolkit event system intercepts **Tab** for focus navigation between focusable elements in the
panel. If the inventory panel has any focusable element (a text field, a focusable slot), pressing Tab
to close the panel will ALSO fire UI Toolkit's Tab-navigation event, potentially shifting focus within
the panel before the legacy `Input.GetKeyDown(KeyCode.Tab)` check runs in `Update`.

Recommended: set `focusable = false` on all non-interactive slot elements in the inventory panel (the
default for `VisualElement` is `focusable = false`; only `TextField` etc. default to `true`). Avoid
placing a `TextField` in the inventory or settings panel. With no focusable elements, Tab navigation
in UI Toolkit is a no-op and `Input.GetKeyDown(KeyCode.Tab)` captures the key cleanly via legacy
polling.

For the settings panel (86caa4bqp), sliders and int fields authored via UI Toolkit are focusable by
default. Tab navigates between them when the panel is open — this is acceptable UX (Esc or the chosen
toggle key to close), but means pressing Tab in the settings panel will advance slider focus rather
than close it. Document this behavior or use a non-Tab key to close (the ticket suggests Esc-menu
behavior).

---

## Application to Far Horizon

### 86caa4bya — Inventory / Belt hotbar

- **Slot grid:** `VisualElement` container with USS `flex-wrap: wrap` + fixed slot size. 20 inventory
  slots = 4×5 or 5×4 grid. USS class `.slot`, `.slot--selected`, `.slot--empty` (BEM per
  unity6-mastery §9).
- **Drag-and-drop:** `PointerManipulator` subclass on each occupied slot's icon element. Pull-based
  overlap detection on `PointerCaptureOutEvent`. Ghost element on a dedicated overlay panel (second
  `UIDocument` at sort order +1) to avoid tree disruption.
- **Item icons (axe, wood):** Sprite Atlas with both icons → single texture batch. Assign sprite via
  `element.style.backgroundImage = new StyleBackground(sprite)`.
- **Stack-count label:** `Label` child, updated on inventory change event (SO event channel per
  unity6-mastery §6 — `InventoryChangedEvent` SO).
- **Held-axe show/hide (AC4):** the inventory closes → legacy Update reads selected slot → sets
  `HeldAxeRig` active/inactive. This is a pure C# data-model decision; no UI binding needed.
- **Input gating:** `bool _inventoryOpen` flag gates locomotion input and hotbar number-key selection
  when inventory panel is displayed. Without this, WASD and number keys 1–5 continue to fire through
  the open panel.
- **Belt hotbar:** a separate `UIDocument` at the bottom (always visible). Separate Panel Settings
  asset from the inventory panel — different scaling / layer order.

### 86caa4bqp — Settings panel

- **Architecture:** `SettingsSO : ScriptableObject` with `[CreateProperty]` properties for zoom-min,
  zoom-max, pitch-min, pitch-max, walk-speed (and named extension stubs for run-speed, jump-height,
  tool-use-speed). Each setter: apply live to the target system + `PlayerPrefs.SetFloat(key, value)`.
  On `Awake`, initialize each property from PlayerPrefs (or SO inspector default as fallback).
- **Panel structure:** one slider per scalar param; two sliders per range param (zoom range, pitch
  range) — label each end clearly ("Min / Max"). `TwoWay` binding per slider to the SO property.
- **Toggle key:** the ticket defers key choice to Devon; recommend `F1` or `P` (neither conflicts with
  WASD / Shift / Ctrl / Tab / Space / 1-5). The ticket explicitly lists Esc-menu behavior; if Esc is
  chosen, guard against closing the panel AND triggering application quit simultaneously.
- **Extensibility (AC2):** a `List<SettingEntry>` registry where each entry is typed
  (float/int/range); the panel builds its own UI from the list at Start. Adding a future setting =
  append one `SettingEntry` to the list + write the property on `SettingsSO`. No UXML rebuild.
- **AC6 test:** the SO-binding approach makes this testable in PlayMode — instantiate a test SO, bind,
  assert the slider value changes the SO property and the target system's live param. No UI hierarchy
  queries needed if the SO is the single source of truth.

### RED FLAGS for implementers

1. **Editor drag API in runtime build** — any reference to `UnityEditor.DragAndDrop` will fail
   compilation for `FarHorizon.exe`. Runtime drag-and-drop is pointer-event only.
2. **Tab key conflict** — UI Toolkit intercepts Tab for focus navigation when focusable elements exist
   in the panel. Set `focusable = false` on all slot `VisualElement`s. Inventory toggle via Tab
   requires the panel to have no focusable children, OR route the toggle through a key that UI Toolkit
   does not intercept (Tab is the canonical focus-advance key in the event system).
3. **Locomotion input bleeds through open UI** — `Input.GetKey` polling is NOT blocked by panel focus.
   Explicit `_inventoryOpen` / `_settingsOpen` gates in the locomotion and hotbar scripts are
   mandatory; without them the player walks while clicking inventory slots.
4. **`[CreateProperty]` without the backing-field annotation** — silently falls back to reflection.
   Both `[SerializeField, DontCreateProperty]` on the backing field AND `[CreateProperty]` on the
   property are required for compile-time binding.
5. **SO property changes not persisted at runtime** — ScriptableObject field changes are in-memory
   only at runtime; `PlayerPrefs` write-through in the setter is the required persistence path (not
   `AssetDatabase.SaveAssets`, which is editor-only and will compile-error in a player build).

---

## Sources (cited above)

- [Unity 6 Manual — Runtime UI event system and input handling](https://docs.unity3d.com/Manual/UIE-Runtime-Event-System.html)
- [Unity 6 Manual — FAQ for input and event systems with UI Toolkit](https://docs.unity3d.com/Manual/UIE-faq-event-and-input-system.html)
- [Unity 6 Manual — Create a drag-and-drop UI inside a custom Editor window](https://docs.unity3d.com/6000.6/Documentation/Manual/UIE-create-drag-and-drop-ui.html)
- [Unity 6 Manual — Control textures of the dynamic atlas](https://docs.unity3d.com/6000.2/Documentation/Manual/UIE-control-textures-of-the-dynamic-atlas.html)
- [Unity 6 Manual — Optimizing performance (UI Toolkit for advanced developers)](https://docs.unity3d.com/6000.4/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/optimizing-performance.html)
- [Unity 6 Manual — Data binding best practices](https://docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/data-binding.html)
- [Unity 6 Manual — Focus system in UI Toolkit](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-focus-order.html)
- [Unity Learn — UI Toolkit in Unity 6: Crafting Custom Controls with Data Bindings](https://learn.unity.com/tutorial/ui-toolkit-in-unity-6-crafting-custom-controls-with-data-bindings)
- [GitHub — gamedev-resources/create-a-runtime-inventory-with-UI-Toolkit](https://github.com/gamedev-resources/create-a-runtime-inventory-with-UI-Toolkit)
