# Three-Bar Need-Meter HUD — Implementable Spec (warmth + hunger + thirst)

**Ticket:** `86caamkxv` (need-meter HUD — three need bars). **Owner:** Uma (spec) → Devon (wiring) · **Reviewer:** Drew.
**Work-type:** spec (design-only; no code). **Status:** SPEC — this is the doc Devon implements VERBATIM to add the THIRST bar and ship all three.

> **What this doc is for (read first):** the warmth + hunger bars **already ship** in `Assets/Scripts/Runtime/SurvivalHud.cs` (warmth = ember-gold, hunger = leaf-green, both segmented IMGUI). This ticket adds the **third bar — THIRST — in the same idiom**, and generalizes the draw to N bars without touching warmth/hunger's look. So this spec is deliberately TIGHT: it pins the thirst bar (color, glyph, row, band ramp, critical treatment) against the **shipped code as ground truth**, and states the layout/order for the three-bar column. It does NOT redesign warmth or hunger — the shipped values are the lock.

**Supersedes for implementation:** the two earlier direction docs ([`need-meter-3bar-direction.md`](need-meter-3bar-direction.md), [`need-meter-ui-direction.md`](need-meter-ui-direction.md)) explored the trio in the abstract and **disagree on the hunger color** (3bar = leaf-green; ui-direction = berry-red). **The shipped build resolves it:** hunger ships GREEN (`FedGreen #8CB85C`). This spec aligns to the shipped code and is the single implementable source for `86caamkxv`; where it differs from the older docs, **this doc + the shipped code win** (the discrepancy is logged as a Decision draft in the final report).

---

## 0. Tonal anchor (read this first)

**Three quiet glows at the edge of vision — the castaway's body told as light, not a console stat-block.** Warmth is *a warm light the cold is eating into* (shipped). Hunger and thirst are its siblings: the same banked-resource-being-spent read, each in its own honest color so the player learns the three at a glance and never reads a word. **Thirst is the one cool note** — the only need the fire can't fix earns the one cool color (the world's own water-blue), which is exactly what makes it read instantly as "the water one."

The gate, unchanged from the shipped warmth bar: **if a beat makes the HUD louder, slicker, or more "AAA stat-bar," it's wrong even if it's clear.** Three small warm-toned glows in the calmest corner; the world keeps the frame. Every channel sub-1.0 (HDR/sRGB-clamp discipline — a tonal rule here, not just a clamp rule). No pure white, no `#FF0000`, no neon, no flashing.

**The load-bearing call:** the three bars read as ONE family (same form, plate, segment grammar, motion) yet are **instantly distinguishable by color + glyph alone** — gold fire / green food / blue water, three hues far apart on the wheel so they never blur even at peripheral glance.

---

## 1. The shipped baseline — LOCKED, do not redesign (warmth + hunger)

These are read straight from `SurvivalHud.cs` and are the **ground truth** the thirst bar matches. Devon preserves them unchanged (AC3 — no regression).

| | **Warmth (row 0, bottom)** | **Hunger (row 1, middle)** |
|---|---|---|
| Glyph (left of bar) | `▲` flame | `●` berry |
| Safe (≥60%) | ember-gold `#E8B25C` (0.91, 0.70, 0.36) | fed-green `#8CB85C` (0.55, 0.72, 0.36) |
| Warning (30–60%) | dusk-orange `#D98A4E` (0.85, 0.54, 0.31) | ripe-amber `#D99E4D` (0.85, 0.62, 0.30) |
| Critical (<30%) | coal-red `#B5563C` (0.71, 0.34, 0.24) | berry-red `#BD4D4D` (0.74, 0.30, 0.30) |
| Emptied segments | charcoal `#2E2A2B` (0.18, 0.165, 0.17) | charcoal `#2E2A2B` (shared) |
| Anchor (IMGUI) | `y = Screen.height - 44`, `x = 16`, box `260 × 28` | `y = Screen.height - 80`, `x = 16`, box `260 × 28` |

**Shared grammar (all three bars use it — already in the shipped warmth/hunger draw):**
- **Exactly 10 segments**, `filled = SurvivalHud.FilledSegments(Current01)` = **FLOOR** rule (`Mathf.FloorToInt(current01 * 10)`, clamped 0..10). A segment lights only when its full 1/10th is earned. The PlayMode boundary assert carries.
- **Empties right-to-left** as the need decays (the "burning/draining down" motion).
- **One low-alpha dark plate per bar** — `rgba(0,0,0,0.55)` (`SurvivalHud.PlateAlpha`, the BootHud stamp-plate family) with ~6 px padding. **Three discrete plates** (each bar reads as its own glow — not one tall plate, which reads "panel").
- **Glyph left of the bar**, drawn in the bar's own fill color; **dims to alpha 0.4 at empty** (`filled > 0 ? 1f : 0.4f`).
- **No number, no percent, no "+1" toast** — glanceable feeling, not a readout.
- **Optional ember-flicker stays warmth-ONLY** (`±6% alpha breathe ~1.5s` on warmth's rightmost-filled segment). Hunger and thirst do NOT flicker — flicker = fire; a flickering food/water bar reads wrong.

> **Hunger color note (reconciliation):** the shipped hunger is GREEN, not the berry-red the older `need-meter-ui-direction.md` proposed. **Green wins** (it ships, it's the universal "food/grow/eat" read, and it reuses the world's canopy-green family `style-guide-v2.md §6`). The thirst color below is chosen to triangulate cleanly against shipped gold + shipped green.

---

## 2. THIRST — the new bar (this ticket's core deliverable)

The thirst bar is the warmth/hunger bar **with one new palette + one new glyph + one new row** — nothing else changes. It mirrors the shipped `DrawHungerBar()` exactly.

### 2.1 Color anchor — water-blue (the call)

**Thirst = the world's own fresh-water blue.** The pond the player drinks from is `#3E8FC4` (`style-guide-v2.md §6` "Water ribbon" / the `21h16_52` lake — looked at it: a bright, warm-leaning fresh stream-blue, NOT a cold cyan). **The need's color IS the source's color** — drink-blue water, watch the blue bar refill. This is the maximum-distinguishability third hue against shipped gold + green: gold / green / blue sit far apart on the wheel, so the three bars never blur at peripheral glance.

| Band (`Current01`) | Thirst fill color | RGB (0–1) | Reads as |
|---|---|---|---|
| **Slaked (≥0.60)** | bright stream-blue `#3E8FC4` | 0.24, 0.56, 0.77 | watered, fresh (the world's literal water color) |
| **Dry-ish (0.30–0.60)** | pale teal `#5FA9B0` | 0.37, 0.66, 0.69 | starting to want water |
| **Parched (<0.30)** | dry grey-blue `#6E8A9C` | 0.43, 0.54, 0.61 | the dry-throat ache — drained toward dusty grey, **never** an alarm hue |
| **Emptied segments** | charcoal `#2E2A2B` | 0.18, 0.165, 0.17 | **shared** with warmth/hunger (the spent color is one charcoal across all three; only the FILLED run carries the need's identity — matches shipped) |

**The cool-note rule (load-bearing):** thirst is the ONLY non-warm color in the cluster. That is deliberate — it's what makes the trio instantly separable and keeps the corner from going monochrome-warm-mush. The blue is sub-1.0, world-derived, and pulled warm-of-pure-cyan, so it belongs to the palette — *a cool note within a warm chord, not a cold intruder*. Mitigations that keep the corner warm overall: (a) the blue is the world's warm-bright water blue; (b) thirst sits at the TOP of the stack (smallest, least-glanced when full); (c) emptied segments are the shared warm-charcoal, not a cold blue-grey. Even fully parched it drifts to dusty grey-blue — desaturation toward grey, never `#FF0000`. The no-screaming-red discipline holds across all three needs.

### 2.2 Glyph — a droplet

`◆` rotated-diamond / single teardrop, drawn in the thirst fill color, left of the bar (the shipped glyph slot, `x`, `y+3`, `18×22`), dimming to alpha 0.4 at empty exactly like the flame/berry. **A droplet reads "water" faster than any letter** — zero text. Implementation note: the shipped warmth/hunger use single Unicode glyphs (`▲`/`●`) in a bold `GUIStyle.Label`; thirst can use a single drop-like glyph the same way (e.g. `◆` or a small authored 12px droplet sprite when the icon set `ui-iconography-sourcing.md` bakes). **Fallback (shipped precedent):** if no clean Unicode drop renders crisply at 18px in-build, fall back to a warm-cream `~` or a tiny authored droplet — but a true droplet is strongly preferred. Sprite swap is later polish, never a blocker.

### 2.3 Row + anchor (extends the shipped stack)

Thirst is **row 2, the top of the need column**, one row-pitch above hunger (the shipped 36px pitch):

- **Thirst:** `y = Screen.height - 116`, `x = 16`, box `260 × 28` (identical geometry to warmth/hunger).
- The **inventory ledger** (shipped `DrawInventoryLedger()`, currently at `-116`) **moves UP to `y = Screen.height - 152`** to clear the new top row. Same cream, same plate, same absent-when-empty. (If the inventory/belt UI `86caa4bya` later supersedes the ledger, the ledger row simply isn't drawn; the need column does not depend on it.)
- **Safe-area:** the top of the stack (ledger at `-152`) stays ≥16px from the BootHud top plates (title top-left, BUILD stamp top-right — both unchanged, uncovered; the stamp is load-bearing for every soak). **Three needs + one ledger is the ceiling this milestone** — the column does not grow taller without a layout revisit.

---

## 3. The three-bar column — order, layout, generalization (AC1/AC3/AC4)

### 3.1 Stack order — warmth bottom · hunger middle · thirst top

```
+--------------------------------------------------------------+
| [Far Horizon]                              BUILD <tag|utc|sha>|   <- BootHud (unchanged)
|                                                              |
|                      ( the world — the star )                |
|                                                              |
|   axe 1   wood 3                                             |   <- inventory ledger  (moves up to -152)
|   ◆ thirst   ▰▰▰▰▰▰▰▱▱▱                                      |   <- thirst   (row 2, top)   -116
|   ● hunger   ▰▰▰▰▰▱▱▱▱▱                                      |   <- hunger   (row 1, middle) -80   (shipped)
|   ▲ warmth   ▰▰▰▰▰▰▰▱▱▱                                      |   <- warmth   (row 0, bottom) -44   (shipped)
+--------------------------------------------------------------+
```
*(`▰`=filled, `▱`=emptied; glyphs are the spec'd marks)*

**Warmth stays anchored at the bottom** (its shipped `-44` position — no regression, AC3). Hunger holds its shipped `-80`. Thirst stacks up to `-116`. The column reads bottom-up as **the order the player met the needs** (warmth first → food → water), so a returning player's muscle memory ("warmth is the bottom one") never moves. **This is a feel call, soak-confirmable** — if the Sponsor's eye wants thirst↔hunger swapped, it's a one-line row-index reorder (the bars are a uniform array). Flagged §6 Q1.

**Left-aligned column:** all three share `x=16` and a common glyph-left edge; the segment runs start at a common x so the three filled-runs read as a clean aligned column the eye compares top-to-bottom at a glance. Right 60% and top of the frame stay clean — world breathes.

### 3.2 Generalize, don't rebuild (AC3/AC4)

The shipped `DrawWarmthBar()` and `DrawHungerBar()` are **byte-for-byte the same routine** with three differences: the need source, the band-color function, and the glyph + row-y. **Devon's job is to lift that into ONE reusable routine called 3×** — e.g.:

```
DrawNeedBar(SurvivalNeed need, System.Func<float,Color> bandColor, string glyph, float baselineY)
```

backed by a small `NeedBar[]` / 3-row table (warmth, hunger, thirst). Then:
- **AC4 is satisfied for free:** a 4th need is one new array entry + one new band-color function — a one-line add. Build exactly this uniform widget; do **not** over-engineer past it (no data-driven scriptable-need-UI system this milestone).
- **AC3 is satisfied by construction:** warmth + hunger keep their shipped band functions (`BandColor`, `HungerBandColor`) and shipped anchors unchanged; the generalization wraps them, it does not alter them. Add `ThirstBandColor(float)` mirroring `HungerBandColor` exactly (same cutoffs `WarmBand=0.60` / `CoolBand=0.30`, new palette).
- **Subscribe-never-poll (AC1):** thirst binds to `ThirstNeed.Changed` + reads `Current01` exactly as warmth/hunger do (the shipped pattern — the IMGUI `OnGUI` draws from the cached value; `BootstrapProject` serializes the `ThirstNeed` ref editor-time, with the `Awake` `FindObjectOfType` fallback as a build-safety net, matching the shipped warmth/hunger wiring). **No per-frame polling.** May be null → the thirst bar is simply not drawn (matches shipped hunger's null-guard).

---

## 4. Critical-state treatment (`IsCritical`) — consistent across all three (AC2)

`IsCritical` is the shared boolean each `SurvivalNeed` exposes. When a need goes critical, the **same treatment applies to all three** (consistency is the AC):

- The bar is **already in its Critical-band color** (§1/§2.1) by the time `IsCritical` trips — the band cutoffs and the flag roughly coincide (confirm against the merged need models, §6 Q3).
- **The need's GLYPH gets a slow pulse** — a `~1.0s` ease-in-out alpha breathe between `~0.55` and `1.0` on the **glyph only** (NOT the bar, NOT the whole row). A slow breathe, **not a blink/flash** — flashing is console-game language and breaks the painterly calm. One pulse pattern, three glyphs — consistent. Cheap alpha tween, no allocation (extends the shipped warmth-flicker technique into the shared critical language).
- **Multi-need-critical:** if two or three go critical at once, all their glyphs breathe at the **SAME phase** (one shared phase clock) so the corner pulses as one calm body, not a panic of competing blinkers.
- **No red vignette, no screen-edge alarm, no death overlay** — out of scope (ticket OOS: "death/fail-state HUD"). The pulsing glyph + the critical-band bar color is the whole "this need is urgent" read.
- **Empty floor (`Current01 = 0`):** the bar is all charcoal, the glyph dimmed (shipped floor); if `IsCritical` is still true at zero, the dimmed glyph keeps its slow pulse so "you are out of X" still reads. No fail-state beyond that.

> **Note on shipped warmth:** the live `SurvivalHud` does NOT yet implement an `IsCritical` glyph-pulse (warmth uses the band-color shift only). This spec **adds the glyph-pulse as the shared critical treatment for all three** (AC2 requires consistent critical-state treatment). Applying it to warmth too is a deliberate, in-scope consistency add — not a warmth regression (the band-color behavior is untouched; the pulse is additive). If a Sponsor soak finds the warmth pulse unwelcome, gate it to hunger/thirst — one-line.

### 4.1 Satisfaction beat (drink/eat/warm) — optional polish, one shared language

The vision's feel is *small relief, repeated* ("small amount of thirst with each scoop"). When a need rises (`Changed` fires upward on drink/eat/warm), the newly-filled segment(s) may **fade IN over ~250ms ease-out** (a gentle "topping up"), optionally with a brief one-time soft bloom on the gained segment. One language, three needs; the drink-water flush tints momentarily toward the stream-blue, the eat-berry flush toward the fed-green, the warm flush toward gold. **This is polish, not a blocker** — the floor is the bar simply gaining segments (the FLOOR rule already does this). No floating "+1" toast (matches shipped quiet-ledger discipline).

| Motion | Duration | Easing | Scope |
|---|---|---|---|
| Segment fill/drain cross-fade | ~250 ms | ease-out | the one changed segment |
| Critical-glyph pulse | ~1.0 s cycle | ease-in-out | the critical need's glyph only (shared phase) |
| Warmth ember-flicker (shipped, optional) | ~1.5 s cycle, ±6% alpha | — | warmth rightmost-filled segment only |

All are cheap alpha/color tweens — no per-frame bar lerp, no allocation (the shipped build-safe discipline).

---

## 5. Primitive discipline + tonal fit

- **Flat color rects only.** `GUI.DrawTexture(rect, Texture2D.whiteTexture)` with `GUI.color` per segment (band color vs shared charcoal) — the shipped IMGUI technique. **No custom shader, no mesh, no Polygon primitive** — build-safe (pure IMGUI never strips to magenta). Glyphs are `GUI.Label` marks (or tiny flat sprites) left of each bar. *(The HDR-clamp + flat-fill rule that drove the shipped warmth/hunger bars carries to thirst: every channel sub-1.0, no gradient texture, no primitive that can shade dark or strip on the desktop URP build.)*
- **One family, three glows.** Same form, plate, segment grammar, motion, shared charcoal — three bars that obviously belong together, distinguished only by identity color + glyph. The HUD reads as one system, not three stat-bars bolted on.
- **The HUD's gold still ties to the gameplay UI.** `gameplay-ui-direction.md` reuses warmth's ember-gold for the belt's selected-slot rim ("gold = warm/active/yours"). Adding hunger-green + thirst-blue does NOT break that — green echoes the world's canopy-green, blue echoes the world's water-ribbon, so the whole color system stays of-the-world.
- **Recede-when-safe.** At full needs all three bars are calm and easy to ignore; the design pulls the eye only as a need drops (its run cools toward its critical tone; its glyph pulses only when critical). World-stays-the-star, enforced by layout.

---

## 6. Open questions (soak/tuning confirmations — NONE block implementation)

The direction above is complete and implementable as written. These are soak/tuning dials Devon resolves against the merged need models + a Sponsor soak.

- **Q1 — Stack order** (warmth-bottom / hunger-mid / thirst-top, §3.1). A feel call (order-introduced). One-line row-index reorder if the Sponsor's eye wants thirst↔hunger swapped. `needs-soak`.
- **Q2 — Thirst color anchor + droplet glyph** (§2). Made within my visual-direction authority (no color lock in DECISIONS.md — only that the three needs exist). Water-blue reuses the world's own water color; droplet glyph. **QA pin for Tess; Sponsor soak can retune** any of the three thirst band hexes (they're constants, cheap to dial). `needs-soak`.
- **Q3 — Band cutoffs vs `IsCritical`** (§4). The 0.60 / 0.30 cutoffs (shipped for warmth/hunger; mirrored for thirst) are feel-tied, not the need models' tuned decay/critical thresholds. Confirm `IsCritical` trips roughly at the bottom band so the critical-band color + glyph pulse coincide. Cheap to retune — constants.
- **Q4 — Warmth glyph-pulse on critical** (§4 note). AC2 requires consistent critical treatment, so this spec adds the glyph-pulse to all three (including warmth, which ships without it). If the Sponsor soak finds the warmth pulse unwelcome, gate it to hunger/thirst — one-line. `needs-soak`.

**`86caamkxv` is UNBLOCKED for implementation** by this spec: layout, thirst identity, generalization shape, motion, and critical treatment are all specified; the four questions are soak/tuning confirmations, not design gaps. (The ticket's OTHER dependency — the thirst `86caamkv7` + hunger `86caamkp8` need models existing — is a separate gate this doc does not affect.)

---

## 7. Decision drafts (for Priya's weekly DECISIONS.md batch — I do not edit DECISIONS.md directly)

- **Decision draft:** The shipped hunger bar color is **GREEN** (`FedGreen #8CB85C`), resolving the disagreement between the two earlier direction docs (`need-meter-3bar-direction.md` = green vs `need-meter-ui-direction.md` = berry-red). Green wins — it ships, it's the universal food/grow/eat read, and it reuses the world's canopy-green. `need-meter-ui-direction.md`'s berry-red hunger palette + per-need umber/basin emptied-segment colors are SUPERSEDED by the shipped code + this spec (shared charcoal emptied segments across all three). (`hud-three-bar-spec.md` §1.)
- **Decision draft:** **THIRST = water-blue** (`#3E8FC4` slaked → `#5FA9B0` dry-ish → `#6E8A9C` parched), the world's own water-ribbon color (`style-guide-v2.md §6`), the single COOL element in the bottom-left HUD cluster; warmth + hunger stay warm. The lone cool note is the instant "this is the water need" signal and prevents three warm bars blurring together. Glyph = a droplet. (`hud-three-bar-spec.md` §2.)
- **Decision draft:** The three bars share ONE reusable `DrawNeedBar(...)` widget (the generalized shipped warmth/hunger draw) over a uniform `NeedBar[]`, so a 4th need is a one-line add (AC4); warmth + hunger's shipped look/feel/anchors/tests are preserved unchanged (AC3); thirst stacks to row 2 (`-116`), ledger moves to `-152`. Stays IMGUI flat-rect (no shader/mesh/Polygon, no UI-Toolkit migration this ticket — that's a separate flagged follow-up in `need-meter-3bar-direction.md §7`). (`hud-three-bar-spec.md` §3/§5.)
- **Decision draft:** A shared `~1.0s` glyph-only alpha-breathe is the consistent `IsCritical` treatment across all three needs (AC2); applying it to warmth too (which ships without it) is an additive consistency change, not a warmth regression. No red vignette / death overlay (OOS). (`hud-three-bar-spec.md` §4.)

---

## 8. Out of scope (per ticket)

The hunger/thirst NEED models + decay + satisfaction (`86caamkp8` / `86caamkv7`); the inventory/belt UI (`86caa4bya`); a needs-balance/tuning pass (the need tickets own defaults); a death/fail-state HUD or red vignette; a settings toggle to hide bars (not spec'd — if the Sponsor later wants one it registers into the settings panel `86caa4bqp`); the IMGUI→UI-Toolkit HUD migration ITSELF (a separate flagged follow-up — `need-meter-3bar-direction.md §7`); low/critical-need audio cues (a separate `audio-direction.md` concern — flag if the Sponsor wants a soft low-need stinger).

---

## Cross-references

- Ticket **`86caamkxv`** (this spec implements into it) · `86caamkp8` (hunger need — read surface) · `86caamkv7` (thirst need + pond — read surface, this bar's water source) · `86caa5zz3` (bushes/berries — hunger source).
- `Assets/Scripts/Runtime/SurvivalHud.cs` — **the shipped warmth + hunger IMGUI bars (ground truth)** this generalizes to add thirst: `DrawWarmthBar`/`DrawHungerBar`, `FilledSegments` FLOOR rule, `BandColor`/`HungerBandColor`, `PlateAlpha 0.55`, `SegmentCount 10`, subscribe-`Changed`-read-`Current01`, right-to-left empty, shared charcoal `#2E2A2B`.
- `Assets/Scripts/Runtime/SurvivalNeed.cs` — the shared need base (`Current01`/`IsCritical`/`Changed`/`TickSeconds`) thirst extends (DECISIONS 2026-06-19, Pattern A); `WarmthNeed`/`HungerNeed`/`ThirstNeed` all expose the identical surface so the HUD binds all three uniformly.
- `Assets/Scripts/Runtime/BootHud.cs` — the top-left title + top-right BUILD-stamp plates (unchanged, uncovered; the stamp is load-bearing for soak); plate-alpha family the need plates match.
- [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) — the PARENT warmth glow-bar spec (segment form, ember palette, plate, flicker, IMGUI primitive); warmth look/feel preserved verbatim (AC3).
- [`need-meter-3bar-direction.md`](need-meter-3bar-direction.md) / [`need-meter-ui-direction.md`](need-meter-ui-direction.md) — the two earlier exploratory direction docs; **this spec is the implementable reconciliation** (hunger = shipped green; thirst = water-blue; shared charcoal). The UI-Toolkit migration flag in `need-meter-3bar-direction.md §7` carries (separate follow-up, NOT this ticket).
- [`gameplay-ui-direction.md`](gameplay-ui-direction.md) — carved-wood gameplay-UI palette (ember-gold belt rim; `accent-leaf` green the hunger color echoes); §9 item-model note (ledger reconcile).
- [`style-guide-v2.md`](style-guide-v2.md) §6 — world palette anchors: **water-blue `#3E8FC4` (the thirst color)**, canopy-green family (hunger), the warm/saturated/sub-1.0 discipline; the saturated-terrain HUD-plate-legibility soak watch-item (re-verify all three plates).
- `.claude/docs/art-direction.md` + `inspiration/` (`21h16_52` lake, `21h16_13` river — looked at them; the warm-bright water blue the thirst bar borrows) — chunky-cartoon board, sub-1.0 warm carry-overs.
- `.claude/docs/unity-conventions.md` / `.claude/docs/lowpoly-quality.md` — IMGUI build-safety; HDR-clamp/sub-1.0 flat-fill discipline; real-`Time.time`-window test rule (AC5).
- DECISIONS 2026-06-17 (M-U2 → three needs) · 2026-06-19 (`SurvivalNeed` base, Pattern A).
</content>
</invoke>
