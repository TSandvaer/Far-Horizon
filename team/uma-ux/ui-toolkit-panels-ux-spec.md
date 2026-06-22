# UI Toolkit Panels — UX Spec (settings panel + inventory + belt)

**Tickets:** `86caa4bqp` (tweakable settings panel) · `86caa4bya` (inventory/belt + axe & wood PoC items)
**Owner:** Uma (direction) → Devon (UI Toolkit implementation) · Reviewer: Drew
**Status:** DIRECTION — docs only, NO implementation here. This is PREP, sequenced behind locomotion (`86ca9yq34` run / `86ca9yq3q` jump). Devon builds these UI Toolkit surfaces from this spec when the milestone reaches them.
**Builds on:** Erik's tech call — [`team/erik-consult/ui-toolkit-vs-ugui-fh.md`](../erik-consult/ui-toolkit-vs-ugui-fh.md) (UI Toolkit for ALL three surfaces) + [`team/erik-consult/ui-toolkit-inventory-settings-research.md`](../erik-consult/ui-toolkit-inventory-settings-research.md) (the runtime gotchas).
**Visual parent:** [`gameplay-ui-direction.md`](gameplay-ui-direction.md) — the tonal anchor, the 9-token carved-wood palette, the layout, the row archetypes. THIS doc does NOT re-decide any of that; it translates it into concrete UI Toolkit USS/UXML structure (selectors, BEM classes, transition declarations, document/panel topology) Devon can author from directly.
**HUD sibling:** [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) — the existing IMGUI warmth/ledger HUD these panels must read as family with (see §7).

---

## 0. Tonal anchor (carried, not re-decided)

**These panels are warm wooden drawers in a sunlit toy world — carved from the same wood as the axe haft.** That anchor is set in `gameplay-ui-direction.md §0` and is NOT relitigated here. The job of THIS doc: make sure the UI Toolkit *structure* (the visual tree, the USS cascade, the transitions) serves that anchor — chunky rounded walnut plates, warm-cream ink, hand-made bevel, snappy-not-floaty motion — instead of drifting toward a generic engine overlay because the markup was authored without the feel in mind.

**The gate stays the same:** if a USS choice makes the UI colder, slicker, or more "AAA-launcher" — a hard 0px corner, a pure-white label, a thin slick handle, a blown-out glow — it is wrong even if it compiles clean and batches well. **Every channel sub-1.0** (HDR/sRGB-clamp discipline from the HUD + Zone-D). The carved-wood toybox is the target.

---

## 1. Document & panel topology (how the surfaces map to UIDocuments)

Per `unity6-mastery.md §9` + Erik's research §E2/§E3, the three surfaces are NOT one UIDocument. They have different visibility lifetimes, sort orders, and scaling needs:

| UIDocument | Contents | Visibility | Panel Settings | Sort order |
|---|---|---|---|---|
| **`BeltDocument`** | the 5-slot belt strip | always visible during play | `Scale With Screen Size`, ref 1920×1080, match 0.5 | base (0) |
| **`InventoryDocument`** | the 5×4 grid + docked belt-row copy + scrim | toggled (`Tab`) | same scaling as belt | +1 (above belt) |
| **`SettingsDocument`** | the workbench column + scrim | toggled (`Esc`) | same scaling | +2 (above inventory) |
| **`DragLayerDocument`** | the drag-ghost element only | only while dragging | same scaling | +3 (top — see §4.4) |

**Why separate documents, not one tree with hidden subtrees:**
- Different sort orders need different Panel Settings assets (Erik §E2 belt-vs-inventory note; research §E2 drag-layer note).
- The drag-ghost on its own top document keeps the inventory grid's visual tree STABLE during a drag — `BringToFront()` on the real slot would re-batch the whole grid mid-drag (research §E2 "BringToFront + atlas batching" gotcha). The ghost lives on `DragLayerDocument` instead.
- `BootHud`/`SurvivalHud` stay IMGUI and coexist (Erik §E6); Devon sets the IMGUI-vs-UIDocument layer order so the build-stamp plate (load-bearing for soak) is never covered.

**Toggle = `style.display`, never `opacity`** (`unity6-mastery.md §9` rule + research §E4): a hidden panel is `DisplayStyle.None` (zero layout, zero render cost), shown is `DisplayStyle.Flex` — then the open transition (§3.1/§4.1) plays on the now-laid-out element. `opacity = 0` would keep paying render cost for an invisible panel.

---

## 2. The palette as USS custom properties (`:root` variable block)

The `gameplay-ui-direction.md §1` 9-token palette becomes a `:root` custom-property block in a SHARED stylesheet (`Palette.uss`) that all four documents reference. This is the single source of the carved-wood material — one edit recolors every surface, and it is the USS echo of "all carved from the same wood."

**`Palette.uss` — the `:root` token block (verbatim values from `gameplay-ui-direction.md §1`, all sub-1.0):**

```css
:root {
    /* plates & wells */
    --panel-walnut:   rgba(42, 35, 32, 0.88);   /* #2A2320 — warm dark walnut, brown bias */
    --panel-edge:     rgb(90, 70, 50);           /* #5A4632 — warm-brown bevel rim */
    --slot-empty:     rgba(58, 48, 42, 0.92);    /* #3A302A — recessed well */
    --slot-hover:     rgba(74, 61, 51, 0.95);    /* #4A3D33 — warm lift on hover */
    --badge-bg:       rgba(42, 35, 32, 0.85);    /* #2A2320 — count-chip walnut */
    /* ink */
    --ink-cream:      rgb(234, 217, 184);        /* #EAD9B8 — primary text (HUD ledger cream) */
    --ink-dim:        rgb(156, 144, 122);        /* #9C907A — secondary/disabled */
    /* affordance — one language the player learns once */
    --slot-selected:  rgb(232, 178, 92);         /* #E8B25C — ember-gold, REUSES HUD warmth fill */
    --accent-leaf:    rgb(76, 158, 58);          /* #4C9E3A — positive: slider fill / valid drop */
    --accent-deny:    rgb(181, 86, 60);          /* #B5563C — denied: clamp-hit / invalid drop (coal-red, NOT error-red) */
}
```

**Discipline encoded by this block (carry from `gameplay-ui-direction.md §1` + HUD §5):**
- The "neutral" plate is `--panel-walnut` (brown bias), NOT a grey-black. This single token is what stops the UI reading as a generic overlay — do not let it drift toward neutral.
- `--ink-cream` is the ONLY text color (plus `--ink-dim` for secondary). No third color introduces a new "voice."
- `--slot-selected` / `--accent-leaf` / `--accent-deny` are the SAME three affordance colors everywhere (HUD + every panel) → gold = "warm/active/yours", leaf = "yes", coal-red = "no".
- All values authored as `rgb()`/`rgba()` 0–255 for authoring legibility; every channel < 255 (sub-1.0). NO `#FFFFFF`, NO `#FF0000`, NO neon.

**The bevel idiom** (`gameplay-ui-direction.md §1` "one bevel per panel edge") in USS: a 2px `border-color: var(--panel-edge)` rim + a 1px lighter top-inner line via a pseudo top-border or an inset highlight element. One bevel per plate, nothing heavier.

---

## 3. SETTINGS PANEL (`86caa4bqp`) — USS/UXML structure

Visual direction lives in `gameplay-ui-direction.md §2`. Structure below.

### 3.1 Document tree (UXML) + open/close transition
```
SettingsDocument
└── .settings-scrim                 (full-screen, walnut-tinted 60% dim — NOT black)
    └── .settings-panel             (centered column, ~420px, max 70% height)
        ├── .settings-panel__header (Label "Settings" + 1px panel-edge underline)
        ├── .settings-panel__rows   (scroll container; one column)
        │   └── (one .setting-row--* per registered setting)
        └── .settings-panel__footer (.settings-reset text button, bottom-left)
```
- **Toggle:** `Esc` (free per `gameplay-ui-direction.md §8` input-map audit). `style.display` flip on `.settings-panel` parent.
- **Open transition** (`gameplay-ui-direction.md §2.1` — 120ms ease-out slide-up + fade): on `.settings-panel`,
  `transition: translate 120ms ease-out, opacity 120ms ease-out;` — open animates `translate: 0 0` from `translate: 0 16px` and `opacity: 0→1`. Close is the reverse at ~90ms. **`UsageHints.DynamicTransform`** on `.settings-panel` (animate transform, NOT width/height — `unity6-mastery.md §9`). Snappy, not floaty — opened/closed dozens of times per soak.
- **Scrim** is `--panel-walnut` at a lower alpha covering the screen, so the live tweak is visible taking effect behind the panel (the workbench's whole point).

### 3.2 The three setting-row archetypes as BEM USS classes
`gameplay-ui-direction.md §2.2` defines the visual contract; here are the BEM class names + the structure each archetype carries. **Registering a new setting = pick an archetype class + give it a label. No new USS.** (This IS the extensible-registry AC2 visual half — Erik §E5 / research §E4.)

| Archetype | BEM block/modifier | Structure | Used by |
|---|---|---|---|
| **A — single float** | `.setting-row .setting-row--slider` | `.setting-row__label` (left, cream) · `.setting-row__slider` (`accent-leaf` fill + chunky ~18px rounded-square thumb) · `.setting-row__readout` (right, fixed-width cream — no jitter) | walk/run/jump/tool-use speed |
| **B — min–max range** | `.setting-row .setting-row--range` | label · `.setting-row__range` (one track, two `accent-leaf` thumbs) · two readouts (`__readout--min` / `__readout--max`, thin dash between) | zoom range, pitch range |
| **C — int stepper** | `.setting-row .setting-row--stepper` | label · `.setting-row__stepper` (`[ − ]` `.stepper__btn--dec` · value · `[ + ]` `.stepper__btn--inc`) | belt slots / inv slots / stack size — registered LATER by `86caa4bya`; the class must EXIST now so they slot in |

**State modifiers (shared across archetypes):**
- `.setting-row--disabled` — extension-hook settings not yet built (AC3): row text → `--ink-dim` + a small `(soon)` tag (`.setting-row__soon`). Present-but-greyed so the Sponsor SEES the extension points without mistaking them for live knobs.
- `.setting-row__thumb--clamp` — transient class added for ~150ms when a range thumb hits min==max or a hard-limit: thumb flashes `--accent-deny` then removes the class (the clamp-hit feedback, AC4). USS `transition` on `background-color`.

**Row rhythm USS:** `.setting-row { height: 44px; padding: 0 12px; }` + a 1px `--panel-edge` @ α0.4 bottom hairline between rows. Generous so a soaking Sponsor hits the right thumb without fishing.

### 3.3 Data binding (the AC2 + AC5 code-half — from Erik §E2 / research §E4)
- `SettingsSO : ScriptableObject` exposes each setting as a `[CreateProperty]` property; the setter applies the live effect AND writes `PlayerPrefs` in ONE call (single authority — research §E4 "do NOT split TwoWay-drive and PlayerPrefs-save").
- Each slider/stepper binds **`TwoWay`** to its SO property → dragging propagates live + persists in one step.
- `[CreateProperty]` on the property + `[SerializeField, DontCreateProperty]` on the backing field — BOTH required or it silently falls back to reflection (research §E4 red flag #4).
- Leave UXML `data-source` UNRESOLVED; assign `element.dataSource = settingsSO` in C# at Start (research §E4 — keeps the SO swappable/testable for AC6).
- Persistence is SILENT — no "saved" toast (`gameplay-ui-direction.md §2.3`); values simply survive relaunch.

---

## 4. BELT + INVENTORY (`86caa4bya`) — USS/UXML structure

Visual direction: `gameplay-ui-direction.md §3` (belt) + §4 (inventory) + §5 (badge) + §6 (icons). Structure below.

### 4.1 Belt document tree + selection
```
BeltDocument
└── .belt                       (bottom-CENTER strip-plate, walnut + panel-edge rim)
    └── .belt__slot             (×5; ~56px; .slot base class) — see slot states §4.3
        ├── .slot__icon         (Image; item sprite or letter-chip fallback)
        ├── .slot__hotkey       (top-left digit 1–5, --ink-dim — language-free hint)
        └── .slot__badge        (bottom-right count chip; §4.5)
```
- **Bottom-CENTER** (`gameplay-ui-direction.md §3.1`) — the SurvivalHud owns bottom-LEFT; belt takes center so they never collide.
- **Selected slot** = `.slot--selected`: ~2.5px ember-gold (`--slot-selected`) rim + subtle inner warm glow + **~3px translate-up lift** (reads "popped forward / in hand"). On scroll/number-key change, the gold rim slides slot-to-slot via `transition: translate 80ms ease-out` on the lifted slot (`gameplay-ui-direction.md §3.2`) — a tactile snap. `UsageHints.DynamicTransform` on belt slots.
- **Selection driver (AC2):** number keys `1–5` OR mouse-scroll — legacy `Input` polling. **GATE these when inventory/settings open** (research §E1/§E5 — `Input.*` polling bleeds through open UI; explicit `_inventoryOpen`/`_settingsOpen` flags mandatory).
- **`.slot--selected` ⇄ `HeldAxeRig`** are the SAME state (AC4): the gold-rimmed slot's tool is the in-hand one. UI rim and in-world held axe never disagree — the load-bearing coherence.

### 4.2 Inventory document tree
```
InventoryDocument
└── .inv-scrim                  (walnut-tinted 60% dim — same warm scrim as settings)
    └── .inv-panel              (centered)
        ├── .inv-panel__grid    (flex-wrap: wrap; 5×4; ~64px slots, 8px gap)
        │   └── .inv-panel__slot (.slot base class)
        ├── .inv-panel__divider (1px panel-edge + "Belt" --ink-dim label)
        └── .inv-panel__belt-row(docked copy of the 5 belt slots — drag target, AC6)
```
- **Toggle `Tab`** (`gameplay-ui-direction.md §4.1`). `style.display` flip.
- **Open transition** (`gameplay-ui-direction.md §4.1` — 150ms ease-out scale-up + fade, the box "pops open"): `transition: scale 150ms ease-out, opacity 150ms ease-out;` open from `scale: 0.96` `opacity: 0` → `scale: 1.0` `opacity: 1`. Close ~110ms reverse. A hair more playful than the settings slide (the pack is the charming surface). `UsageHints.DynamicTransform`.
- **Docked belt-row** inside the inventory makes drag-down-to-equip an obvious gesture (AC6). Visually identical to the bottom-center belt — Devon's call whether to dock-the-same-widget or mirror.
- **`focusable = false` on all `.slot` elements** (research §E5) — else `Tab` navigates focus within the grid instead of closing the inventory. NO `TextField` in either panel.

### 4.3 Slot states — the BEM class set (this is the spec's core slot vocabulary)
One `.slot` base class; states are modifiers. Drives belt AND inventory slots identically:

| Class | Visual | Trigger |
|---|---|---|
| `.slot` | base well: `--slot-empty` fill, rounded corners, recessed read | always |
| `.slot--hover` | `--slot-hover` warm lift | `:hover` (use the pseudo-class; modifier only if Devon needs scripted hover) |
| `.slot--selected` | ember-gold rim + inner glow + 3px lift | belt selection only (§4.1) |
| `.slot--filled` | holds `.slot__icon` (vs empty well) | item present |
| `.slot--drag-source` | source slot dims to `--slot-empty` while its item is held | during drag (§4.4) |
| `.slot--drop-valid` | soft `--accent-leaf` inner glow as cursor passes over a legal target | drag-over, tool→belt or any→inv |
| `.slot--drop-deny` | `--accent-deny` coal-red inner glow; dragged icon won't seat | drag-over, resource→belt (AC6 tool-vs-resource) |
| `.slot--equippable` | tiny `--ink-cream` corner notch/dot = "may sit on the belt" | tools (axe) only — NOT resources |

**The tool-vs-resource teach (AC6), visualized in ONE place:** *tools wear a small equip-notch (`.slot--equippable`) and may sit on the belt; resources have none and coal-red-deny (`.slot--drop-deny`) if dragged at the belt.* The deny-glow is the language-free "resources don't go on the belt" lesson.

### 4.4 Drag-and-drop — the runtime pattern (research §E2, the #1 red flag)
- **`PointerManipulator` subclass on the slot's icon element** — NEVER `UnityEditor.DragAndDrop` (does not compile in the player build — research §E2 / red flag #1).
- `PointerDownEvent` → capture pointer, clone the icon as a **ghost on `DragLayerDocument`** (§1) at `Position.Absolute`, dim source via `.slot--drag-source`.
- `PointerMoveEvent` → translate the ghost (`UsageHints.DynamicTransform`).
- `PointerCaptureOutEvent` → **drop handler**: pull-based `worldBound.Overlaps` across registered slots; snap to closest overlapping legal slot, else spring back to source (`transition: translate 120ms ease-out`). Do NOT use per-slot `PointerEnterEvent` (fires for every slot crossed — expensive; research §E2).
- **Drag feedback** (`gameplay-ui-direction.md §4.2`): ghost at ~1.1× scale + soft drop-shadow ("lifts off the felt"); valid targets get `.slot--drop-valid`, invalid get `.slot--drop-deny` + spring-back.

### 4.5 Stack-count badge (`gameplay-ui-direction.md §5`)
- `.slot__badge` — ~18px rounded `--badge-bg` chip, bottom-right (never covers the icon's center-up silhouette), `--ink-cream` digit, NO "×" glyph (just `12`).
- Shows only at count ≥ 2 (count 1 = silence, per the HUD's quiet-ledger discipline). Tools (non-stacking) never show a badge.
- **Composited live** over the icon — the sprite is just the prop; the badge is UI chrome, so one icon serves any count.

### 4.6 Item icons (`gameplay-ui-direction.md §6` + `ui-iconography-sourcing.md`)
NOT re-decided here — `.slot__icon` is an `Image`/`VisualElement` whose `backgroundImage` is the `IconBaker`-rendered sprite of the actual prop (axe = shipped slate/steel FBX; wood/stone = Blender-MCP props). Atlas all icons into one **Sprite Atlas** (`Assets/Art/UI/ItemIcons.spriteatlasv2`) before the 4th distinct icon (Erik §E3 / research §E3 — keeps icon-bearing slots in one draw batch under the 8-texture cap). Fallback: a chunky warm-cream letter-chip (`A`/`W`/`S`) if a bake lags.

---

## 5. Transition timing table (one place — Devon authors USS `transition` from this)

| Surface | Property | Open | Close | Easing | Hint |
|---|---|---|---|---|---|
| Settings panel | `translate` + `opacity` | 120ms (16px slide-up + fade) | ~90ms reverse | ease-out | `DynamicTransform` |
| Inventory | `scale` (0.96→1.0) + `opacity` | 150ms (pop) | ~110ms reverse | ease-out | `DynamicTransform` |
| Belt selection rim | `translate` (slot-to-slot lift) | 80ms | — | ease-out | `DynamicTransform` |
| Drag spring-back | `translate` (ghost → source) | 120ms | — | ease-out | `DynamicTransform` |
| Clamp-hit thumb | `background-color` | ~150ms flash | (class removed) | ease | — |

All snappy, not floaty — these are touched constantly during a soak. **Animate transforms, never width/height** (`unity6-mastery.md §9`). No per-frame `Update` lerps — pure USS `transition`.

---

## 6. USS authoring discipline (primitive rules — `unity6-mastery.md §9`)

- **No custom shader, no mesh, no Polygon primitive** for any of this. The chunky-rounded-toy look is `border-radius` + `border-width` + the §2 palette — geometry-free. UI Toolkit is 2D quads/borders/rounded-corners; that covers every surface (plates, slots, rims, sliders, steppers, badges). (Sidesteps the whole faceted-mesh primitive class — and there is no HDR-clamp/WebGL2 visibility risk here because there is no WebGL target; Windows-desktop only per CLAUDE.md. The sub-1.0 discipline carries anyway for tonal reasons — saturated, never blown-out.)
- **All styling in USS BEM selectors** (`block__element--modifier`); no inline styles. Keep selectors shallow/specific — avoid `*` and broad `.unity-*` (expensive on large trees, `unity6-mastery.md §9`).
- **`:root` custom properties** (§2) are the single material source — every plate/slot/text references a token, never a literal hex. One palette edit recolors the whole UI family.
- **Hide via `display: none`**, never `opacity: 0` (§1). Toggle = `DisplayStyle.None`/`.Flex`.

---

## 7. Composition with the existing need-meter HUD

The HUD (`u2-5-survival-hud-spec.md`) ships IMGUI today (`BootHud`/`SurvivalHud`); these panels are the new UI Toolkit system. They coexist (Erik §E6). The composition rules:

- **One material family, two render systems.** The §2 `:root` tokens are DELIBERATELY the HUD's own colors: `--slot-selected` IS the HUD warmth ember-gold (`#E8B25C`); `--ink-cream` IS the HUD ledger cream (`#EAD9B8`); `--accent-deny` IS the HUD cold-state coal-red (`#B5563C`). So the new UI Toolkit panels read as SIBLINGS of the IMGUI HUD, not a second visual system — even though the render path differs.
- **The ember-gold does triple duty** — HUD warmth fill ⇄ belt `.slot--selected` rim ⇄ "full stack" badge tint. Gold consistently means "warm / active / yours" across both systems.
- **Layer/sort order** — Devon sets the UIDocument sort so the BootHud build-stamp plate (top-right, load-bearing for every soak) is NEVER covered by a panel scrim. The scrim dims the WORLD, not the HUD chrome — keep the warmth bar + build-stamp readable through/above the scrim, or the soak loop breaks.
- **Bottom-left stays the HUD's; bottom-center is the belt's** — the layout split (`gameplay-ui-direction.md §3.1`) holds across both systems so warmth-glow-bar and belt never collide.
- **Migration note (deferred):** Erik §E1/Application notes that WHEN the 2nd + 3rd need-meters (hunger/thirst) arrive, the IMGUI warmth bar should migrate INTO a UI Toolkit HUD document so all three needs share one tree. That migration is its OWN ticket — NOT this spec's scope. When it lands, this §2 palette + the slot/affordance vocabulary carry over unchanged (the need-meter bands already share the gold/orange/coal-red ramp). Flag for the milestone that adds need #2.

---

## 8. Out of scope (per tickets)

Implementation; audio cue/bus direction (separate concern — no audio in these UI tickets); the chop/gather/craft MECHANICS (their own tickets — this only styles inventory ITEMS); the IMGUI→UI-Toolkit HUD migration (its own ticket, §7); a full audio/graphics/key-rebind options menu (the settings panel is a soak-tuning instrument the registry grows into later); item tooltips/stats beyond name+icon+count; equipment/armor systems; settings for not-yet-built features (run/jump/chop — extension hooks only, rendered greyed via `.setting-row--disabled` §3.2).

---

## 9. Cross-references

- Tickets **`86caa4bqp`** (settings) · **`86caa4bya`** (inventory/belt + axe/wood) — the ACs this structure serves.
- [`gameplay-ui-direction.md`](gameplay-ui-direction.md) — the visual-direction PARENT: §0 tonal anchor, §1 the 9-token palette (→ this doc's §2 `:root` block), §2 settings archetypes (→ §3), §3 belt (→ §4.1), §4 inventory (→ §4.2), §5 badge (→ §4.5), §6 icons (→ §4.6), §8 input-map, §9 primitive discipline.
- [`team/erik-consult/ui-toolkit-vs-ugui-fh.md`](../erik-consult/ui-toolkit-vs-ugui-fh.md) — the tech decision (UI Toolkit for all three) this spec implements; §E2 binding, §E3 draw-call/atlas, §E5 USS-styling-for-carved-wood.
- [`team/erik-consult/ui-toolkit-inventory-settings-research.md`](../erik-consult/ui-toolkit-inventory-settings-research.md) — runtime gotchas: §E1/§E5 input-bleed gating, §E2 PointerManipulator drag (NOT editor API), §E3 dynamic-atlas/Sprite-Atlas, §E4 `[CreateProperty]`/PlayerPrefs persistence, §E5 Tab focusable-false.
- [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) — the IMGUI HUD these panels sibling (§7 composition); the ember-gold/cream/coal-red parents of the §2 tokens.
- [`ui-iconography-sourcing.md`](ui-iconography-sourcing.md) — the icon/glyph sourcing map (IconBaker / USS shapes / recolored vector set) behind §4.6.
- `.claude/docs/unity6-mastery.md §9` — UI Toolkit authoring rules (BEM USS, `display:none` toggle, transform-not-layout animation, `[CreateProperty]`, ≤8-texture batch) the §6 discipline cites.
- `Assets/Scripts/Runtime/HeldAxeRig.cs` — the held-axe rig AC4 wires `.slot--selected` to (rim ⇄ in-hand coherence, §4.1).
- DECISIONS 2026-06-14 — axe-head slate/steel ACCEPTED, barn-red DROPPED (binds the AXE icon palette, §4.6).
</content>
</invoke>
