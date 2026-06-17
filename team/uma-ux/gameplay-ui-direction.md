# Gameplay UI — Visual Direction (settings panel + inventory + belt + item icons)

**Tickets:** `86caa4bqp` (tweakable settings panel) · `86caa4bya` (inventory/belt + axe & wood PoC items)
**Owner:** Uma (direction) → Devon (UI Toolkit implementation) · Reviewer: Drew
**Status:** DIRECTION — docs only, no implementation here. Devon builds the UI Toolkit surfaces from this.
**Source of truth:** the board PNGs in [`inspiration/`](../../inspiration/) (look at them) + [`style-guide-v2.md`](style-guide-v2.md) (the whole-game palette + tool language) + [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) (the existing HUD palette this must sibling).

---

## 0. Tonal anchor (read this first)

**These panels are warm wooden drawers in a sunlit toy world — not a game-console OS bolted over the painting.** The board is a cheerful, faceted, saturated diorama (`21h13_31`, `21h16_13`, `21h22_33`): warm soil-browns, sunny grass-greens, soft daylight, hand-made asymmetry. The gameplay UI must read like it was *carved from the same wood as the axe haft* — warm cream ink on soft dark-walnut plates, chunky rounded corners, a little hand-made softness. When the castaway opens his pack, it should feel like opening a small wooden tackle box on a beach, not summoning a settings overlay.

Two surfaces, two jobs, one material:
- **The settings panel is a workbench** — a quiet utility drawer the Sponsor pulls open to dial knobs. It is on-tone but *unfussy*; it earns its keep by being legible and instantly-tweakable, not by being pretty. (It is fundamentally a soak-tuning instrument — give-him-the-knob; cf. the F9 axe-nudge pattern, ticket `86caa4bqp`.)
- **The inventory + belt are the castaway's pack** — these are *seen constantly* during play, so they carry more of the toy-warm charm: warm wood slots, soft item icons, a satisfying chunky open/close.

The world keeps the frame. Even open, the inventory leaves the world breathing around its edges (it is a centered panel over a dimmed-but-visible world, never a full opaque takeover). The belt is a thin strip hugging the bottom edge — glanceable, never in the way.

**The gate (same as every surface):** if a styling beat makes the UI colder, slicker, or more "AAA-launcher," it's wrong even if it's clean. Warm wooden toybox is the target. Every channel sub-1.0 (HDR/sRGB-clamp discipline carries from the HUD spec + Zone-D — saturated, never blown-out).

---

## 1. Shared UI material — the "carved from the same wood" palette

Every surface below inherits this. It is deliberately a **sibling of the existing BootHud / SurvivalHud chrome** (low-alpha dark plates, warm-cream ink) so the gameplay UI reads as ONE family with what already ships — not a second visual system. The HUD's ember-band palette (`u2-5-survival-hud-spec.md` §3) and the world anchors (`style-guide-v2.md` §6) are the parents; these are their UI children.

| Token | Role | Color | RGB (0–1) | Notes |
|---|---|---|---|---|
| `panel-walnut` | Panel/drawer background plate | `#2A2320` @ α0.88 | 0.165, 0.137, 0.125 | Warm dark walnut, NOT neutral black — the plate has a brown bias so it reads "wood," not "OS." Higher alpha than the HUD's 0.55 because these are *deliberate* panels (modal-ish), not peripheral glows. |
| `panel-edge` | Panel rim / bevel highlight | `#5A4632` @ α1.0 | 0.353, 0.275, 0.196 | A 2px warm-brown rim around panels + a 1px lighter top-inner line = the "hand-made bevel catching light" read (the board's signature edge-bevel, in 2D). |
| `slot-empty` | Empty inventory/belt slot fill | `#3A302A` @ α0.92 | 0.227, 0.188, 0.165 | Slightly lighter than the panel so empty slots read as recessed wells in the drawer. |
| `slot-hover` | Slot under cursor | `#4A3D33` @ α0.95 | 0.290, 0.239, 0.200 | A warm lift on hover — the only "interactive shimmer," kept subtle. |
| `slot-selected` | SELECTED belt slot (and drag target) | warm gold rim `#E8B25C` | 0.91, 0.70, 0.36 | **Reuses the HUD's ember-gold** verbatim — the selected belt slot glows with the same warmth as the warmth-bar. Ties belt-selection to the game's warm identity. (See §3.) |
| `ink-cream` | Primary text (labels, counts, headers) | warm cream `#EAD9B8` | 0.92, 0.85, 0.72 | **Reuses the HUD ledger cream** (`u2-5` §4) — same hand-lettered warm ink. The paver-cream family from the world palette. |
| `ink-dim` | Secondary text (units, hints, disabled) | dim cream `#9C907A` | 0.61, 0.565, 0.478 | For helper text + disabled/non-belt-able items. |
| `accent-leaf` | Positive affordance (slider fill, valid drop) | world leaf-green `#4C9E3A` | 0.30, 0.62, 0.23 | **Reuses the world canopy green** (`style-guide-v2` §6) — slider tracks fill leaf-green, valid drop targets flash leaf-green. The world's own color doing UI work. |
| `accent-deny` | Invalid drop / clamp-hit | muted coal red `#B5563C` | 0.71, 0.34, 0.24 | **Reuses the HUD coal-red** — a dying-ember "no," never a screaming `#FF0000` error red. Used when wood is dragged at the belt (resource-can't-go-on-belt) or a range slider hits its clamp. |
| `badge-bg` | Stack-count badge background | `#2A2320` @ α0.85 | 0.165, 0.137, 0.125 | Same walnut, slightly lower alpha — the count badge is a tiny walnut chip in the slot corner. |

**Discipline notes (carry from the HUD + Zone-D):**
- **No pure white, no pure-saturated red, no neon.** Cream not white; coal-red not error-red; gold not amber-LED. Every value sub-1.0.
- **Warm bias on the "neutral" too** — the plate is walnut-brown, not grey-black. This single choice is what stops the UI reading as a generic engine overlay.
- **One bevel per panel edge** — the 2D echo of the board's signature edge-highlight (`style-guide-v2` §1.3). A warm rim + a lighter top-inner line; nothing heavier.

---

## 2. SETTINGS PANEL (`86caa4bqp`) — the workbench drawer

**Tonal read:** a quiet wooden workbench the Sponsor pulls open mid-soak to turn knobs, then closes. On-tone but utility-first — legibility and instant-tweak beat decoration here. It is the soak-tuning instrument; its job is to *get out of the way of dialing*.

### 2.1 Framing & open/close
- **Toggle key:** `Esc` (the ticket's suggested settings/menu key). Confirmed non-clashing with the locked input map — WASD (move), Shift (run), Ctrl (crouch), Space (jump), Tab (inventory), 1–5 + scroll (belt). `Esc` is free and is the universal "open the menu" expectation.
- **Placement:** a **centered vertical panel**, ~420px wide, max ~70% screen height, the world dimmed to ~60% behind it (a `panel-walnut`-tinted scrim, NOT black — the world stays warm behind the drawer). The world is still visible so the Sponsor sees his live tweak take effect *behind* the panel.
- **Open/close feel:** a quick **120ms ease-out slide-up + fade-in** (panel rises ~16px into place as it fades). Close is the reverse, ~90ms. Snappy, not floaty — this panel is opened and closed dozens of times per soak; sluggish animation would annoy. (USS `transition` on `translate` + `opacity`; no per-frame script.)
- **Header:** a single `ink-cream` title row — `Settings` — with a thin `panel-edge` underline. Optional small flame/gear glyph left of it (language-free), but text is fine.

### 2.2 The setting-row vocabulary (this is the extensible pattern's VISUAL contract)
The ticket's AC2 demands an **extensible registry** — each setting is a typed entry, and adding a future setting is a few lines. The *visual* side of that contract: **three row archetypes**, each a reusable USS-classed template. Registering a new setting = pick the archetype + give it a label; no new styling.

**Archetype A — `setting-row--slider` (single float):** label left (`ink-cream`), a horizontal slider center, a live numeric readout right (`ink-cream`, monospace-ish, fixed-width so it doesn't jitter as the value changes). Slider track is `slot-empty`; the filled portion + the thumb are `accent-leaf`. Thumb is a chunky rounded square (~18px), not a thin handle — chunky-toy, easy to grab. → walk speed, run speed, jump height, tool-use speed.

**Archetype B — `setting-row--range` (min–max pair):** label left, then a **dual-thumb range** (one track, two `accent-leaf` thumbs) center, with TWO readouts right (`min` / `max`, separated by a thin dash). Both ends are independently draggable and the live system clamps to them (AC4). When a thumb is dragged to touch the other (min==max) or hits the registered hard-limit, the thumb briefly flashes `accent-deny` (the clamp-hit feedback). → zoom range (orbit min/max), view-angle range (orbit pitch min/max).

**Archetype C — `setting-row--stepper` (int):** label left, a `[ − ] value [ + ]` chunky stepper center-right, value in `ink-cream`. Used by the DOWNSTREAM tickets that register int settings into this panel (belt slots, inventory slots, stack size — those are OOS for this ticket but the archetype must EXIST so they slot in cleanly). → registered later by `86caa4bya`.

**Row rhythm:** rows are ~44px tall, separated by a 1px `panel-edge` @ α0.4 hairline, ~12px horizontal padding. Generous vertical breathing so a soaking Sponsor can hit the right thumb without fishing. Disabled/not-yet-built settings (run/jump/chop extension hooks, AC3) render the row in `ink-dim` with a small `(soon)` tag — present-but-greyed, so the Sponsor SEES the extension points without them being mistaken for live knobs.

### 2.3 Footer
- A `Reset to defaults` text button (`ink-dim`, lifts to `ink-cream` on hover) — AC5's nice-to-have. Bottom-left of the footer.
- Persistence is silent (PlayerPrefs/settings asset, AC5) — no "saved" toast; the values simply survive a relaunch. Quiet.

### 2.4 What the settings panel does NOT do
No audio/graphics/key-rebind sections (OOS — this is a soak-tuning panel, not a full options menu). No tabs this milestone (one scrollable column). The registry can grow into a fuller menu later; the three archetypes are the seed.

---

## 3. BELT hotbar (`86caa4bya` AC2) — the bottom strip

**Tonal read:** a thin row of wooden slots hugging the bottom edge — the castaway's quick-access tools, always glanceable. This is on-screen *constantly*, so it earns full toy-warm charm but stays SLIM (it must not crowd the world or fight the bottom-left SurvivalHud cluster).

### 3.1 Layout & placement
- **Bottom-CENTER**, horizontally centered, ~16px above the bottom safe-edge. **Deliberately center, not bottom-left** — the SurvivalHud (warmth glow-bar + ledger) owns bottom-LEFT (`u2-5` §2); the belt takes center so the two never collide. The world breathes on both flanks.
- **Default 5 slots** (count follows the `belt slots` setting registered into the settings panel). Each slot ~56×56px, ~6px gap, on one `panel-walnut` strip-plate with the `panel-edge` rim.
- Slots are `slot-empty` wells; a slot holding an item shows the item ICON centered + a small number label (`1`–`5`) in the **top-left corner** of each slot in `ink-dim` (the hotkey hint — language-free, just the digit).

### 3.2 The selected-slot highlight (the load-bearing read)
- The SELECTED slot gets a **`slot-selected` gold rim** (~2.5px, ember-gold `#E8B25C`) + a subtle inner warm glow + a tiny **upward lift (~3px translate-up)** so it reads as "popped forward / in hand." This is the clearest possible "this is what I'm holding" signal, and it reuses the HUD's warmth-gold so belt-selection feels part of the same warm identity.
- **Selection driver (AC2):** number keys `1–5` (range follows belt-slot-count) OR mouse-scroll to cycle. On scroll, the gold rim slides slot-to-slot with a quick ~80ms ease — a satisfying snap, so cycling feels tactile.
- **The selected belt slot drives the held item (AC4):** the gold-rimmed slot's tool is the one shown in-hand via `HeldAxeRig` (axe shows in-hand ONLY when it's in the selected slot). The UI's gold rim and the in-world held axe are the SAME state, visualized twice — that coherence (rim ⇄ hand) is the whole point of the belt read.

---

## 4. INVENTORY (`86caa4bya` AC1) — the pack (Tab)

**Tonal read:** the castaway's wooden tackle box — a centered grid of warm wells the player opens to sort their haul. Seen often, so it carries the charm; the world dims-but-stays behind it.

### 4.1 Framing & open/close
- **Toggle key:** `Tab` (per AC1). Non-clashing with the input map.
- **20-slot grid** (count follows the `inventory slots` setting). A clean **5×4 grid** reads best at the default 20 (matches the belt's 5-wide rhythm so belt + inventory feel like one column system). Each slot ~64×64px, ~8px gap, on a `panel-walnut` panel with the `panel-edge` rim. Centered; world dimmed to ~60% behind (same warm scrim as the settings panel — consistency).
- **Belt shown docked at the bottom of the inventory panel while open** — when Tab is open, render the 5 belt slots as a labeled row *inside/beneath* the inventory grid (a thin `panel-edge` divider + a small `ink-dim` `Belt` label between them). This makes drag-between-inventory-and-belt (AC6) a direct, obvious gesture — you drag DOWN from the grid into the belt row. (The belt ALSO stays visible in its normal bottom-center spot during closed-inventory play; opening Tab just adds the docked copy as the drag target. Devon's call whether to dock-the-same-widget or mirror — visually they must look identical.)
- **Open/close feel:** a slightly softer **150ms ease-out scale-up (0.96→1.0) + fade** — the box "pops open." Close ~110ms reverse. A hair more playful than the settings panel's slide (the pack is the charming surface; the workbench is the utility one).

### 4.2 Slot & item look
- Empty slot = `slot-empty` recessed well. Hover = `slot-hover` warm lift. A slot holding an item shows the **item icon** centered (see §6) + a **stack-count badge** bottom-right (see §5).
- **Drag feedback (AC6):** on pick-up, the icon lifts to follow the cursor at ~1.1× scale with a soft drop-shadow (the item "lifts off the felt"); the source slot dims to `slot-empty` while held. Valid drop targets get a soft `accent-leaf` inner glow as the cursor passes over them; INVALID targets (e.g. a resource dragged at the belt — AC6 tool-vs-resource rule) get an `accent-deny` coal-red inner glow + the dragged icon won't seat (it springs back to source with a quick ~120ms ease). The deny-glow is the language-free "resources don't go on the belt" teach.
- **Tool-vs-resource rule (AC6), visualized:** TOOLS (axe) are belt-allowed — they show a tiny `ink-cream` corner notch/dot meaning "equippable." RESOURCES (wood, stone) are inventory-only — no notch, and they coal-red-deny if dragged at the belt. State the rule in one place so the player learns it once: *tools wear a small equip-notch and may sit on the belt; resources have none and stay in the pack.*

### 4.3 Stacking (AC7)
- Stackable resources (wood, stone) stack to the cap (default 20, follows the `inventory stack size` setting). Tools (axe) never stack — no badge on the axe.
- A full stack (at cap) shows its count badge in `ink-dim`-bordered gold-ish to read "full"; below cap it's plain `ink-cream`. Subtle — just enough to glance "that stack's full."

---

## 5. STACK-COUNT BADGE — spec

- A small chip in the **bottom-right** of any slot holding count ≥ 2 (count 1 shows NO badge — silence, per the HUD's quiet-ledger discipline; a lone item doesn't need "×1" clutter).
- Form: a ~18px rounded `badge-bg` walnut chip, `ink-cream` number, no "×" glyph (just the digit — `12`, not `×12`; cleaner and language-free). Bottom-right so it never covers the icon's readable silhouette (which lives center-and-up).
- Tools (non-stacking) never show a badge.

---

## 6. ITEM ICONS — the concrete recommendation (the ticket's explicit open question)

> Ticket `86caa4bya` AC5 + the dispatch flag explicitly call this out: *"we need to find out the graphics for the inventory, cute/warm/low-poly."* Here is the concrete call.

### 6.1 RECOMMENDATION: render the existing 3D props to flat sprites (NOT hand-author flat 2D sprites)

**The icons are small orthographic RENDERS of the actual in-game low-poly props, baked to PNG sprites — not separately-authored flat 2D art.** This is the right call for four reasons:

1. **Coherence is the whole game's gate.** The board's core rule is "character, tools, world all carved from the same faceted material" (`style-guide-v2` §0). An inventory icon that's a *different* drawing of the axe than the one in the castaway's hand breaks that — the player sees two axes. Rendering the SAME prop mesh guarantees the icon and the held item are literally the same object. This is the strongest possible coherence.
2. **The props already exist or are trivially-built in-style.** The axe is a shipped FBX (`Assets/Art/Props/CastawayAxe/`). Wood and stone are simple faceted props on the board's exact vocabulary (`21h12_49` log + `21h10_44` rocks) — Blender-MCP scripts them in minutes (the team's first-choice asset route, `unity-conventions.md` §Asset creation), and the SAME mesh doubles as the world pickup AND the icon source. One asset, two uses.
3. **Faceted low-poly renders BEAUTIFULLY to a small icon** — the chunky silhouettes and few-big-facets read crisply at 64px in a way fussy 2D art wouldn't, and the soft studio key-light + AO + contact shadow give each icon that polished-diorama pop for almost no effort.
4. **Zero style-drift risk over time.** As the props evolve (recolors, re-styles), re-baking the icon is a one-command render — the icon never falls out of sync with the prop. Hand-authored sprites would need manual re-drawing every time.

### 6.2 HOW to source/author each icon (in-style, concrete)

**The bake recipe (one reusable editor pass — recommend a small `IconBaker` editor utility, Devon's call):**
- Load the prop FBX into a tiny offscreen scene with the world's **soft even key light + gentle fill + a subtle contact shadow** (the board's studio-light setup, `style-guide-v2` §1.5).
- **Orthographic camera, ~3/4 hero angle** (slightly above + slightly to one side — the same flattering angle the board shots use; e.g. `21h08_08` views the axe at a gentle 3/4). NOT a flat side-on or top-down — 3/4 reads the chunky volume and the edge-bevel.
- Render to a **square transparent PNG** (256×256 source, displayed at 64px — render big, downscale crisp). A faint soft drop-shadow baked under the prop helps it sit on the slot felt.
- Import as a `Sprite (2D and UI)`, point-or-bilinear filtered, into `Assets/Art/Icons/`.

**Per-icon framing + palette (sub-1.0, the world's own colors):**

| Icon | Source prop | Hero angle / framing | Palette anchor |
|---|---|---|---|
| **AXE** | the SHIPPED `CastawayAxe.fbx` | 3/4, head up-left, haft trailing down-right (reads as an axe in silhouette instantly — matches `21h08_08`'s read) | the SHIPPED atlas — **slate/steel head, warm-brown haft, dark leather-wrapped grip**. **Do NOT recolor the icon to barn-red** — the shipped held axe is the accepted slate/steel rustic hatchet (DECISIONS 2026-06-14: "axe-head ACCEPTED… genuinely looks like an axe"; barn-red recolor DROPPED). The icon must match the in-hand axe the player sees, not the abstract style-guide barn-red ideal. Icon ⇄ held-prop consistency wins. |
| **CHOPPED WOOD** | a small faceted **log bundle** (Blender-MCP, vocab from `21h12_49`'s cut log + `21h22_33` stumps) | 3/4, a short stacked bundle of 2–3 cut log segments (cut-end rings facing camera reads "chopped" unmistakably) | warm wood-brown body `#7A5230` (0.48,0.32,0.19 — the haft/trunk anchor), a lighter top-facet `#9A6B40`, pale cut-end rings `#C8A878`. Warm, sunny. |
| **PICKED-UP STONE** | a small faceted **rock cluster** (Blender-MCP, vocab from `21h10_44` rocks) | 3/4, a chunky 2–3-rock cluster, a few big planes, lighter top-facets catching the key | warm grey `#8E8A82` (0.56,0.54,0.51 — the world rock anchor), lighter top-facet `#A6A29A`, darker side `#6E6A63`. **Warm-grey, not blue-grey** (`style-guide-v2` §4 rocks rule). |

**Stack-count badge** is UI chrome over the icon (§5) — not part of the rendered sprite. The sprite is just the prop; the badge is composited live so the same icon serves any count.

### 6.3 Fallback (if icon renders aren't ready when Devon wires)
Per the HUD-spec precedent (`u2-5` §4 — "fall back to short warm-cream text labels… sprite swap is a later polish, not a blocker"): if the rendered sprites aren't baked when the inventory wires, **fall back to a chunky warm-cream letter chip in the slot** (`A` axe / `W` wood / `S` stone on a `slot-empty` well) — legible, on-tone, swappable. The icons are the target; the letter-chip keeps the system shippable if the bake lags. **Strongly prefer the renders** — they're cheap and they're the coherence win — but don't let an un-baked icon block the inventory landing.

---

## 7. Cross-surface coherence — the three surfaces as one family

- **Same plate idiom** (walnut plate + warm-brown bevel rim) across settings, inventory, belt → one wooden-toybox material, sibling to the existing BootHud/SurvivalHud chrome (NOT a second UI system).
- **The ember-gold does triple duty** — HUD warmth fill, belt selected-slot rim, "full stack" tint — so gold consistently means "warm / active / yours" across HUD and gameplay UI.
- **Leaf-green = positive affordance, coal-red = denied** everywhere (slider fill / valid drop = leaf; clamp-hit / invalid drop = coal-red). One affordance language the player learns once.
- **Cream is the only text color** (plus dim-cream for secondary). No second font color introduces a new "voice."
- **The belt's selected-rim ⇄ the in-world held axe are the same state** — the most important coherence: UI and world never disagree about what's in hand.
- **Sub-1.0 everywhere** — the HDR/sRGB-clamp discipline that governs the world and the HUD governs the UI too. Saturated, warm, never blown-out.

---

## 8. Input-map audit (confirms no key clashes)

| Key | Use | Source |
|---|---|---|
| WASD | move | `86ca9yq2x` (merged) |
| Shift | run | `86ca9yq34` (in flight) |
| Ctrl | crouch | input map |
| Space | jump | `86ca9yq3q` (queued) |
| **Tab** | **inventory** | `86caa4bya` AC1 — FREE |
| **1–5 / scroll** | **belt select** | `86caa4bya` AC2 — FREE |
| **Esc** | **settings panel** | `86caa4bqp` AC1 — FREE |

No collisions. (If a future feature wants `Esc` for pause, the settings panel moves to a dedicated key — flag at that time; for now `Esc` is the natural menu key and is unclaimed.)

---

## 9. Implementation notes for Devon (primitive discipline)

- **UI Toolkit (UXML + USS)**, per both tickets + `unity6-mastery.md` §UI Toolkit. This is the FIRST UI Toolkit surface in the project (no `.uxml`/`.uss` exist yet — greenfield). The existing HUD (`BootHud`/`SurvivalHud`) stays IMGUI; the gameplay UI is the new UI Toolkit system. They coexist (IMGUI draws over the UIDocument or vice-versa — Devon sets the layer/sort).
- **All styling lives in USS** (the §1 palette → USS custom properties / `:root` vars; the three setting-row archetypes → reusable USS classes; the slot states → `:hover` / `.slot--selected` / `.slot--deny` classes). The extensible-registry contract (AC2) is HALF code (the typed registry) and HALF these reusable USS-classed templates — a new setting reuses an archetype class, never new CSS.
- **No custom shader, no Polygon mesh** for any of this — UI Toolkit's quad/border/rounded-corner rendering covers every surface here (plates, slots, rims, sliders, badges). The chunky-rounded-toy look is `border-radius` + `border-width` + the warm palette, not geometry. (This sidesteps the whole faceted-mesh primitive class — UI is 2D quads.)
- **Animations are USS `transition`s** (translate + opacity + scale on open/close, rim-slide on belt select) — no per-frame Update allocations. Keep them snappy (settings 120ms, inventory 150ms, belt-rim 80ms) per §2.1/§3.2/§4.1.
- **Icons:** the `IconBaker` (§6.2) is a small editor utility (offscreen scene + ortho cam + render-to-PNG). Reuses the prop FBXs — no separate art pipeline. Recommend baking AXE first (asset exists), then wood + stone after their Blender-MCP props are scripted.
- **Item-model note:** the new ticket's slot/stack inventory MODEL supersedes the thin U2-2 `Inventory.cs` (`HasAxe`/`WoodCount` flags) — it's a model evolution, not a binding to the old two-flag surface. The SurvivalHud's existing ledger read may need re-pointing at the new model; flag for Devon to reconcile (out of THIS direction doc's scope, but note it so it isn't a surprise).

---

## 10. Out of scope (per tickets)

Implementation; audio (no cue/bus direction here — separate concern); the actual chop/stone GATHERING mechanics (their own tickets — this only styles the inventory ITEMS); crafting the axe (pickup-for-test here); equipment/armor systems; item tooltips/stats beyond name + icon + count; a full audio/graphics/key-rebind options menu (the settings panel is a soak-tuning instrument that the registry can grow into later, not a full options screen this milestone); settings for not-yet-built features (run/jump/chop — extension hooks only, rendered greyed per §2.2).

---

## Cross-references

- Tickets **`86caa4bqp`** (settings panel) · **`86caa4bya`** (inventory/belt + axe/wood items) — the ACs this direction serves.
- [`inspiration/`](../../inspiration/) — board v2 PNGs (ground truth): `21h08_08` (axe — AXE icon ref), `21h12_49` (log/stump — WOOD icon ref), `21h10_44` (rocks — STONE icon ref), `21h13_31`/`21h16_13`/`21h22_33` (world palette/light).
- [`style-guide-v2.md`](style-guide-v2.md) — §1 shared grammar (edge-bevel), §3 tool language, §6 palette anchors (axe/wood/rock/green colors reused here).
- [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) — §3/§4 HUD ember-band + ledger-cream palette (the parent of this UI's cream/gold/coal-red); §2 bottom-left corner ownership (why the belt takes bottom-CENTER).
- `Assets/Art/Props/CastawayAxe/` — the shipped slate/steel hatchet FBX (the AXE icon's render source; slate head NOT barn-red per DECISIONS 2026-06-14).
- `Assets/Scripts/Runtime/HeldAxeRig.cs` — the held-axe rig AC4 wires the belt selected-slot to (rim ⇄ in-hand coherence).
- `Assets/Scripts/Runtime/Inventory.cs` — the thin U2-2 ledger the new slot/stack model evolves (§9 note).
- `.claude/docs/art-direction.md` — board-v2 catalog + warm/sub-1.0 carry-overs.
- `.claude/docs/unity-conventions.md` §Asset creation — Blender-MCP route for the wood/stone props.
- `.claude/docs/unity6-mastery.md` §UI Toolkit — the implementation primitive.
- DECISIONS 2026-06-14 — axe-head slate/steel ACCEPTED, barn-red recolor DROPPED (binds the AXE icon palette).
