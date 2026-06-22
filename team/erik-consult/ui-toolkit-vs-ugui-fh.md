# UI Tech Decision — UI Toolkit vs uGUI for Far Horizon (Unity 6)

## Question

Which Unity UI system should Far Horizon use for the three near-term surfaces:
(a) the HUD body-column (warmth + hunger + thirst need-meters),
(b) the inventory grid + belt hotbar, and
(c) the tweakable settings panel?

## Bottom Line

**Use UI Toolkit for ALL three surfaces.** It is Unity's forward-directed choice for
screen-overlay game UI in Unity 6, ships with no extra package, and has decisive
advantages in data binding, draw-call efficiency, and iteration speed. The existing
warmth HUD (IMGUI, `BootHud.cs`) is grandfathered but the need-meter column and every
new gameplay-UI surface from this milestone onward is UI Toolkit. uGUI has no
meaningful advantage for any of Far Horizon's near-term UI surfaces and introduces
real drawbacks: no first-class data binding, higher draw-call overhead, and no
USS-driven styling that would support the "carved from the same wood" visual language.

---

## Evidence

### E1 — Official Unity 6 recommendation for screen-overlay UI
- **Source:** Unity 6.0 Manual, "Comparison of UI systems in Unity,"
  `docs.unity3d.com/6000.0/Documentation/Manual/UI-system-compare.html`
  (Strong — official docs, fetched 2026-06-18.)
- The docs state: "UI Toolkit is an alternative to uGUI (Unity UI) if you create a
  screen overlay UI that runs on a wide variety of screen resolutions." The explicit
  recommended cases for uGUI — in-world 3D UI, VFX with custom shaders, easy
  `MonoBehaviour` referencing — do not apply to Far Horizon's need-meters, inventory,
  or settings panel.
- Features exclusive to UI Toolkit (vs uGUI): data binding system, USS global style
  management, UI transition animations, SVG support. Far Horizon uses three of these
  four immediately (binding for settings/HUD, USS for the "carved wood" palette,
  transitions for panel open/close animations).

### E2 — Unity 6 data binding (runtime, `[CreateProperty]`)
- **Source:** Unity 6 Manual, "Data binding best practices,"
  `docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides/...`
  (Strong — official docs, cited in `team/erik-consult/ui-toolkit-inventory-settings-research.md`.)
- Unity 6 ships compile-time data binding via `[CreateProperty]` with zero reflection
  cost per frame. `TwoWay` binding on the settings panel drives the `SettingsSO`
  property setter, which both applies the live effect and writes `PlayerPrefs` in one
  call — exactly what AC2 + AC5 of ticket `86caa4bqp` require. uGUI has no equivalent;
  every control update is a manual `UnityEvent` wire.
- `unity6-mastery.md §9` records the binding setup, `ToTarget` vs `TwoWay` semantics,
  and the `[DontCreateProperty]` backing-field annotation requirement.

### E3 — Draw-call and frame-time overhead
- **Source:** Angry Shark Studio, "Unity UI Toolkit vs UGUI: 2025 Developer Guide,"
  `angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/`
  (Moderate — well-sourced third-party benchmark on Unity 2022.3.10f1; methodology
  not independently reproduced; direction is consistent with E1 architectural
  differences, so weight as corroborating, not primary.)
- Benchmark with 1,000 interactive elements: UI Toolkit = 5 draw calls vs uGUI = 45
  draw calls (9× fewer); 4.2 ms frame time vs 12.5 ms (3×); 48 MB memory vs 125 MB.
  These magnitudes are expected: uGUI builds UI over a Canvas/GameObject hierarchy
  whose transform updates and transparency-rebatch cost scales with element count;
  UI Toolkit's visual tree does not.
- For Far Horizon's surfaces (HUD ~15 elements, inventory ~25 slots, settings ~10
  rows), the gap is smaller in absolute terms than the benchmark's 1,000-element test.
  But the structural advantage — UI Toolkit's uber shader batching ≤8 textures per
  draw call, the dynamic atlas packing item icons, no Canvas rebuild on slot content
  change — is real at any scale and will only matter MORE as the survival loop grows.
- **Caveat:** benchmark was run on Unity 2022.3, not Unity 6. Unity 6's UI Toolkit
  has additional optimizations (improved panel layouting, `[CreateProperty]` compile
  time, `BatchRendererGroup` decoupling from UI). The direction is conservative, not
  over-stated.

### E4 — Unity 6 UI Toolkit feature additions (2025 status)
- **Source:** Unity Discussions, "UI Toolkit development status and next milestones —
  February 2025," `discussions.unity.com/t/...1607740`
  (Strong — Unity product manager post, official roadmap.)
- February 2025 update confirms UI Toolkit is Unity's active-investment UI system for
  runtime use. New in or since Unity 6: `TabView`, `ToggleButtonGroup`, `Slider` /
  `MultiColumnListView` improvements, runtime data binding, Shader Graph UI target
  (Unity 6.3 LTS). World-space UI is on the roadmap.
- uGUI receives maintenance fixes only; UI Toolkit is where Unity is investing.

### E5 — USS styling and the "carved wood" visual language
- **Source:** `unity6-mastery.md §9` (Strong — internal, evidence-graded in
  `team/erik-consult/ui-toolkit-inventory-settings-research.md`.)
- The gameplay-UI-direction spec (`team/uma-ux/gameplay-ui-direction.md §1`) defines a
  precise 9-token palette (`panel-walnut`, `slot-empty`, `slot-selected`, `ink-cream`,
  etc.) and a BEM class vocabulary (`.slot--selected`, `.slot--deny`, `.setting-row--slider`).
  USS custom properties and `:root` vars can encode this palette directly; `:hover` and
  state-class swaps drive all interactive state. uGUI equivalent: per-element `Image`
  color assignments in C#, no cascade, no reuse — every state is a manual code call.
- Open/close animations (settings 120ms ease-out slide, inventory 150ms scale-pop per
  `gameplay-ui-direction.md §2.1 / §4.1`) map trivially to USS `transition` on
  `translate`, `scale`, and `opacity` with `UsageHints.DynamicTransform`. uGUI would
  require an Animation Clip or a per-frame `LerpUnclamped` in Update — both add
  maintenance overhead.

### E6 — uGUI: the one remaining advantage (and why it doesn't apply here)
- **Source:** Unity 6 Manual comparison table (E1 source, Strong).
- uGUI retains legitimate advantages for: UI elements lit and positioned in 3D world
  space (e.g. floating damage numbers over an enemy head), Animation Clip / Timeline
  integration, and scenarios where the team's existing uGUI expertise sharply
  outweighs the learning cost. None of these apply to Far Horizon's near-term surfaces.
  The existing `BootHud.cs` (IMGUI) can stay IMGUI indefinitely — it is the build-stamp
  plate and the zone-D warmth-bar, both simple enough that a migration isn't worth the
  risk. The NEW surfaces (need-meter column, inventory, belt, settings) are greenfield
  and belong in UI Toolkit.

---

## Application to Far Horizon

### Near-term surfaces and their UI Toolkit pattern

**HUD body-column (warmth + hunger + thirst need-meters):**
Each need-meter is a horizontal segmented bar (10 segments per the warmth spec,
`u2-5-survival-hud-spec.md §3`). With UI Toolkit: a single `UIDocument` with three
`VisualElement` containers (one per need), each holding 10 child elements. USS fills
match the ember-gold / dusk-orange / coal-red band palette. Binding: `ToTarget` on
`Current01` via `[CreateProperty]` on each need's ScriptableObject, or event-driven
push from the SO event channel (`unity6-mastery.md §6`). `style.display = None` hides
segments past the fill level — cheapest show/hide (E1, §9 rule).

The existing IMGUI warmth bar in `BootHud.cs` should be MIGRATED into the new
`UIDocument` when the hunger + thirst meters arrive — running two UI systems
simultaneously (IMGUI + UI Toolkit) is supported but adds layer-order friction. The
migration cost is low (the spec is already written); the right time is the milestone
that adds the second need. Until then, the IMGUI warmth bar ships as-is.

**Inventory grid + belt hotbar (ticket `86caa4bya`):**
Belt: a second `UIDocument` (separate `Panel Settings` asset for different sort order
and scaling from the inventory panel). 5 `VisualElement` slots, USS `.slot--selected`
drives the ember-gold rim (`#E8B25C`) + 3px translate-up lift via USS `transition`.
Inventory: a centered panel, 5×4 `VisualElement` grid, `flex-wrap: wrap`, 64×64px
slots. Drag-and-drop via `PointerManipulator` subclass (pointer-event path only — NOT
`UnityEditor.DragAndDrop`, which will not compile in the player build; this is the
#1 gotcha from `ui-toolkit-inventory-settings-research.md §E2`). Item icons: rendered
3D-prop sprites in a single Sprite Atlas (one texture batch for all icons regardless
of count, per `ui-toolkit-inventory-settings-research.md §E3`).

**Settings panel (ticket `86caa4bqp`):**
Centered vertical panel, `SettingsSO : ScriptableObject` with `[CreateProperty]`
properties. Each property setter: apply live effect + `PlayerPrefs.SetFloat`. Three
USS-classed row archetypes from `gameplay-ui-direction.md §2.2` map directly to the
extensible registry's visual contract. `TwoWay` binding per control. Open/close via
Esc key (confirmed non-clashing per `gameplay-ui-direction.md §8`); `style.display =
None` to show/hide the panel (zero render cost when hidden).

### Draw-call budget expectation
At the near-term scale (HUD ~12 elements, belt 5 slots, inventory 25 slots when open,
settings ~10 rows when open), UI Toolkit's dynamic atlas + uber-shader batching should
hold the entire open-inventory frame to 2–4 UI draw calls assuming all item icons are
packed in one Sprite Atlas (`Assets/Art/UI/ItemIcons.spriteatlasv2`). No Atlas =
potential 1 draw call per distinct icon past the 8-texture batch cap. Pack the atlas
first before adding the 4th distinct item icon.

### What changes from the existing HUD approach
The `BootHud.cs` IMGUI warmth bar and ledger (warmth segment bar + "axe x1 wood x3"
line) are the project's only shipped UI today. They remain IMGUI until the multi-need
HUD migration. Every gameplay UI surface from `86caa4bya` and `86caa4bqp` onward is
UI Toolkit. The two systems coexist without conflict (IMGUI draws via `OnGUI`; the
`UIDocument` is a separate Camera-agnostic overlay; Devon sets the Panel Settings sort
order so they layer correctly).

### Red flags for implementers (inherited from `ui-toolkit-inventory-settings-research.md`)
1. **Editor drag API in runtime build** — `UnityEditor.DragAndDrop` does not compile
   in the player. Runtime drag-and-drop is `PointerManipulator` / pointer events only.
2. **Locomotion input bleeds through open UI** — `Input.GetKey` polling is NOT blocked
   by panel focus. Explicit `_inventoryOpen` / `_settingsOpen` gate flags in the
   locomotion and hotbar scripts are mandatory.
3. **Tab key UI focus navigation** — set `focusable = false` on all slot
   `VisualElement`s or Tab navigates within the panel instead of closing inventory.
4. **`[CreateProperty]` without `[DontCreateProperty]` on the backing field** silently
   falls back to reflection. Both annotations required.
5. **SO changes are in-memory only at runtime** — `PlayerPrefs` write-through in each
   property setter is the only persistence path; `AssetDatabase.SaveAssets` is
   editor-only and will compile-error in the player build.

---

## Verdict table

| Criterion | UI Toolkit | uGUI | Winner |
|---|---|---|---|
| Unity 6 official recommendation for screen-overlay | Yes (explicit) | Legacy / maintenance | UI Toolkit |
| Data binding (compile-time, no reflection) | `[CreateProperty]` | None (manual events) | UI Toolkit |
| Draw calls at Far Horizon scale | 2–4 (atlas + batch) | ~15–45 (canvas rebuilds) | UI Toolkit |
| USS styling → "carved wood" palette + BEM states | Native | Manual C# per element | UI Toolkit |
| CSS `transition` animations (open/close panels) | Native | Anim Clip or Update lerp | UI Toolkit |
| Runtime drag-and-drop | PointerManipulator | uGUI EventSystem / drag | Tie (both need custom code) |
| World-space / in-world UI | Not yet (roadmap) | Yes | uGUI (N/A to these surfaces) |
| Team gotcha count | 5 (see above) | Fewer familiar traps | Slight uGUI (but gap is known) |
| Evidence strength | Strong (official) + moderate (benchmarks) | — | — |

---

## Sources

- Unity 6 Manual — Comparison of UI systems: `docs.unity3d.com/6000.0/Documentation/Manual/UI-system-compare.html`
- Unity 6 Manual — Migrate from uGUI to UI Toolkit: `docs.unity3d.com/6000.3/Documentation/Manual/UIE-Transitioning-From-UGUI.html`
- Unity Discussions — UI Toolkit development status Feb 2025: `discussions.unity.com/t/ui-toolkit-development-status-and-next-milestones-february-2025/1607740`
- Angry Shark Studio — UI Toolkit vs UGUI 2025 guide (benchmark data): `angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/`
- `team/erik-consult/ui-toolkit-inventory-settings-research.md` — inventory/settings deep-dive (all prior §E references), cited throughout
- `team/uma-ux/gameplay-ui-direction.md` — the visual direction this tech must serve
- `team/uma-ux/u2-5-survival-hud-spec.md` — the existing HUD spec and IMGUI context
- `.claude/docs/unity6-mastery.md §9` — UI Toolkit authoring rules and performance numbers
