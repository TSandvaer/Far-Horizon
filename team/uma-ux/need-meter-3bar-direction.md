# Need-Meter HUD — Three-Bar Direction (warmth + hunger + thirst)

> **⚠ SUPERSEDED (for implementation) by [`hud-three-bar-spec.md`](hud-three-bar-spec.md)** — the reconciled implement-to spec for `86caamkxv`. That doc aligns to the SHIPPED build (hunger `#8CB85C`, thirst `#3E8FC4`+droplet) and wins where they differ (this doc's hunger value diverges). Kept for the design rationale / tonal exploration; do not implement from it.

**Ticket:** `86caamkxv` (3-bar need HUD) — this doc is the DESIGN INPUT the ticket implements. **Owner:** Uma (direction) → Devon (HUD wiring) · **Reviewer:** Drew
**Depends on (runtime read surfaces):** `86caamkp8` (HUNGER need) · `86caamkv7` (THIRST need) — both expose the SAME surface as `WarmthNeed` (`Current01`/`Max`/`IsCritical`/`Changed`).
**Status:** DIRECTION — docs only, no implementation here. Devon generalizes the existing `SurvivalHud.cs` to N bars per this spec; he does NOT rebuild from scratch (ticket instruction).

**Extends, does NOT rewrite:**
- [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) — the existing single-warmth glow-bar (10-segment ember band, sub-1.0 palette, right-to-left empty, bands, flame glyph, plate, optional flicker). **That spec is the PARENT.** Warmth's look/feel is unchanged; this doc adds two SIBLING bars in the same idiom + the shared layout/column rules.
- [`gameplay-ui-direction.md`](gameplay-ui-direction.md) — the carved-wood gameplay-UI palette (ember-gold / cream / coal-red the panels already reuse).
- [`ui-toolkit-panels-ux-spec.md`](ui-toolkit-panels-ux-spec.md) §7 — already flagged the IMGUI→UI-Toolkit HUD migration as "its own ticket, when need #2 arrives." Need #2 has arrived; see §7 below.
- Erik [`ui-toolkit-vs-ugui-fh.md`](../erik-consult/ui-toolkit-vs-ugui-fh.md) — UI Toolkit is the forward HUD system; the existing IMGUI warmth bar is grandfathered and migrates "when the hunger + thirst meters arrive."

---

## 0. Tonal anchor (read this first)

**Three quiet glows at the edge of vision — the castaway's body, told as light, not as a console stat-block.** The U2-5 anchor holds verbatim and now governs all three: *the world is the star; the HUD is a diegetic glow the player glances at and feels, then looks back at the world.* Warmth was "a warm light the cold is eating into." Hunger and thirst join it as the SAME kind of read — a banked resource being slowly spent — each in its own honest, language-free color so the player learns the three at a glance and never has to read a word.

The gate, unchanged from U2-5 and the gameplay-UI direction: **if a beat makes the HUD louder, slicker, or more "AAA stat-bar," it's wrong even if it's clear.** Three small warm glows in the calmest corner. Every channel sub-1.0 (HDR/sRGB-clamp discipline carries — it's a tonal rule here, not a WebGL one; Windows-desktop only). The world keeps the frame; even with three bars the right 60% and top stay clean.

**The single most important call:** the three bars must read as ONE family (same form, same plate, same segment grammar, same motion) and yet be **instantly distinguishable by color + glyph alone** — so a glance tells warmth-vs-hunger-vs-thirst without reading, and a low bar tells *which* need is dropping by its color cooling toward its own critical tone.

---

## 1. Layout — the bottom-left body-column (extends U2-5 §2)

U2-5 reserved this exact growth: *"M-U3's second need stacks UP from the warmth bar in the same column; reserved, not built now."* This is that stack, now built — three needs, not two, in the bottom-left body-column. BootHud's two plates (top-left title, top-right BUILD stamp) are unchanged and uncovered — the build stamp stays load-bearing for every soak.

```
+--------------------------------------------------------------+
| [Far Horizon]                              BUILD <tag|utc|sha>|   <- BootHud (unchanged)
|                                                              |
|                                                              |
|                      ( the world — the star )                |
|                                                              |
|   ~ thirst   ▰▰▰▰▰▰▰▱▱▱                                      |   <- thirst  (top of stack)
|   * hunger   ▰▰▰▰▰▱▱▱▱▱                                      |   <- hunger  (middle)
|   ^ warmth   ▰▰▰▰▰▰▰▱▱▱                                      |   <- warmth  (bottom — unchanged)
+--------------------------------------------------------------+
```
*(glyphs above are placeholders for the spec'd icons in §3; `▰`=filled, `▱`=emptied)*

### 1.1 Order — warmth bottom, hunger middle, thirst top

**Warmth stays anchored at the bottom** (its existing U2-5 position — no regression, AC3). The two new needs **stack UP from it**, in the order the loop teaches them and in rough order of moment-to-moment urgency in the cold-island fiction:

1. **Warmth** (bottom) — the founding need, the one U2-5 shipped; cold is the island's first threat. Lowest = most-glanced = the urgent one, per U2-5's "warmth sits lowest and largest" reasoning.
2. **Hunger** (middle) — the second-introduced need (berries from bushes, `86caa5zz3`).
3. **Thirst** (top) — the third-introduced need (pond, `86caamkv7`).

> **Why this order, not alphabetical or "vital-signs":** the column reads bottom-up as the order the *player* met the needs (warmth first, then food, then water) — the HUD grows in the same sequence the milestone taught the loop, so a returning player's muscle memory ("warmth is the bottom one") never moves. **This is a feel call, soak-confirmable** — if the Sponsor's eye wants thirst-above-hunger swapped, it's a one-line reorder (the bars are a uniform array). Flagged in §8 Q1.

### 1.2 Anchor math (extends U2-5 §2, same left-anchored x=16 idiom)

Three rows of the U2-5 element box (~`260 × 28`), stacked with the U2-5 inter-row pitch (~36 px, matching its warmth→ledger gap):

- **Warmth (row 0, bottom):** baseline `y = Screen.height - 44`, `x = 16` — **unchanged from U2-5 §2** (no regression).
- **Hunger (row 1):** `y = Screen.height - 80`, `x = 16`.
- **Thirst (row 2, top):** `y = Screen.height - 116`, `x = 16`.
- **Inventory ledger** (U2-5 §4, the `axe x1 wood x3` line) moves UP to clear the taller stack: `y = Screen.height - 152`, `x = 16`. *(It rides above the need column; same cream, same plate. If the inventory/belt UI `86caa4bya` supersedes the ledger, the ledger row simply isn't drawn — Devon reconciles per `gameplay-ui-direction.md §9` item-model note. The need column does not depend on the ledger.)*
- **Safe-area:** the top of the stack (thirst, ~`Screen.height - 116`) stays well clear of the BootHud top plates; everything ≥16 px from edges. Three needs + one ledger row is the ceiling this milestone — the column does not grow taller without a layout revisit.

- **Left-aligned column:** all three bars share x=16 and a common left edge for their glyph; the segment runs start at a common x so the three filled-runs read as a clean aligned column (the eye compares fill heights down the stack at a glance). The right 60% and top of the frame stay clean — world breathes.

---

## 2. The shared bar widget — one form, reused 3× (supports AC4)

**AC4 asks: do the three bars share a consistent component/template so a 4th need is a one-line add? Yes — this direction explicitly supports and requires it.** Every bar is the SAME widget (the U2-5 warmth glow-bar, generalized), parameterized by exactly four things:

| Parameter | Source | Example (warmth) |
|---|---|---|
| **Need source** | the need instance (`WarmthNeed`/`HungerNeed`/`ThirstNeed`) — subscribe to `Changed`, read `Current01`, read `IsCritical` | `WarmthNeed` |
| **Fill-color ramp** | a 3-band warm→critical ramp, §4 | ember-gold → dusk-orange → coal-red |
| **Glyph** | a small language-free icon, §3 | flame `^` |
| **Stack row index** | 0 (bottom) … N (top), drives the y-anchor in §1.2 | 0 |

Everything else is shared and identical across all three (so the family reads as one system):
- **10 segments**, `filledCount = floor(Current01 × 10)` clamped 0..10 — **the exact U2-5 §3 rule, verbatim** (FLOOR, deterministic, the PlayMode boundary assert carries). A segment lights only when its full 1/10th is earned.
- **Empties right-to-left** as the need decays (U2-5's "fire burning down" motion) — same for all three (food/water "draining" right-to-left reads the same as warmth burning down).
- **Emptied segments dim to the SAME cold charcoal** `#2E2A2B` (0.18, 0.165, 0.17) across all three — the "spent" color is shared so only the FILLED run carries the need's identity color. (One charcoal, three fill-ramps.)
- **Same low-alpha dark plate** behind each bar (`rgba(0,0,0,0.55)`, U2-5 §3 plate, matched to BootHud's stamp-plate alpha family) with ~6 px padding — legible over bright saturated-green terrain and dark foliage alike. Three plates, or one taller plate behind the whole column — Devon's call; **prefer three discrete plates** (each bar reads as its own glow; a single tall plate reads more "panel," less "diegetic edge-glow"). Re-verify plate legibility over the new saturated-green terrain in soak (the `style-guide-v2.md §6` watch-item — applies to all three plates now).
- **No number, no percent text, no "+1" toast** — glanceable feeling, not a stat readout (U2-5 discipline, all three).

> **Implementation framing (this milestone = IMGUI, generalized):** the ticket says *generalize the existing `SurvivalHud.cs`, do not rebuild.* So this widget is the existing IMGUI flat-color-rect bar (U2-5 §6 — `GUI.DrawTexture(rect, Texture2D.whiteTexture)` with `GUI.color` per segment) turned into a small reusable draw routine called 3× with the four parameters above. **No custom shader, no mesh, no Polygon primitive** — flat color rects only, the same build-safe IMGUI technique already shipping. The UI-Toolkit migration is a SEPARATE follow-up (§7) — it does not block this ticket and the layout/palette/glyph rules here carry over to it unchanged.

---

## 3. Per-need identity — color anchor + glyph (the distinguishability call, AC2)

Each need owns a **distinct warm color family** + a **language-free glyph** so the three are told apart at a glance with zero text. All glyphs sit LEFT of their bar (the U2-5 flame-glyph position), drawn in the bar's own full-saturation fill color so the glyph + fill read as one need-identity.

| Need | Glyph (language-free) | Identity reads as | Full color (sub-1.0) |
|---|---|---|---|
| **Warmth** | **flame** ▲ (the U2-5 glyph — unchanged) | a fire | ember-gold `#E8B25C` (0.91, 0.70, 0.36) |
| **Hunger** | **leaf/berry sprig** (a small berry-on-stem, or a simple wheat/leaf mark) | food | leaf-green `#5FA83C` (0.37, 0.66, 0.24) |
| **Thirst** | **droplet** (a single teardrop) | water | water-blue `#3E8FC4` (0.24, 0.56, 0.77) |

**Why these three colors — the distinguishability + tonal logic:**
- **Warmth = warm gold** (fire) — unchanged, the founding identity.
- **Hunger = leaf-green** — reuses the WORLD's own canopy green family (`style-guide-v2.md §6` `#4C9E3A` / `gameplay-ui-direction.md §1` `accent-leaf #4C9E3A`), nudged a hair warmer/brighter (`#5FA83C`) so it reads as *living food* (berries, leaves) and stays legible over the dark plate. Green = "grow / eat" is the universal read; it's the world's own color doing HUD work, so the HUD stays of-the-world.
- **Thirst = water-blue** — reuses the world's water-ribbon blue (`style-guide-v2.md §6` `#3E8FC4`). The pond the player drinks from is this blue; the bar is the same blue. Coherence: the need's color IS the source's color.
- **Three different hues (gold / green / blue) are the maximum-distinguishability triad** — they sit far apart on the wheel so the bars never blur into each other even at peripheral glance, yet all three are pulled warm/saturated-but-sub-1.0 so they belong to the same world palette (the blue is a warm-leaning bright stream blue, not a cold cyan; the green is warm leaf, not neon). **This is the load-bearing distinguishability decision (AC2).**

> **One tonal watch:** the U2-5 anchor was "warm light the cold eats" — adding a blue need risks a cold note. Mitigated by (a) the blue is the world's own warm-bright water blue, not a cold UI cyan; (b) it sits at the TOP of the stack (smallest, least-glanced when full); (c) emptied segments are the shared warm-charcoal, not a cold blue-grey. The corner stays warm overall; the blue is a single honest accent, not a temperature shift.

> **Glyph sourcing:** prefer tiny flat glyphs/sprites (flame already exists as the U2-5 ▲; berry-sprig + droplet are trivial 12px marks). **Fallback (U2-5 §4 precedent):** if glyph sprites aren't ready when Devon wires, fall back to a single warm-cream initial in the bar's color — but glyphs strongly preferred (a droplet reads "water" faster than a `T`). When the icon set `ui-iconography-sourcing.md` bakes, the need glyphs join it. Sprite swap is later polish, never a blocker.

---

## 4. Fill / drain transitions + per-need band ramps (timing + critical-state, AC2)

### 4.1 Fill/drain motion — quiet, matched to the decay, no per-frame lerp dazzle

The bar is **driven by the need's `Changed` event** (subscribe-never-poll, AC1) — it reflects `Current01` when the need fires. The needs decay on a `Time.time`-window tick (`TickSeconds`, per the shared model), so visible change is naturally slow and stepwise (a segment drops when its 1/10th is crossed). **Direction:**

- **Segment count change is a soft fade, not a hard pop:** when `filledCount` drops by one (decay) or rises (satisfaction), the affected segment **cross-fades** between its fill color and charcoal over **~250 ms ease-out** — quiet, so the eye catches the change in the corner without it flashing. Satisfaction (drinking/eating/warming) fades the segment IN over the same ~250 ms (a gentle "topping up"). This is a single segment's alpha/color tween, not a per-frame bar lerp — cheap, no allocation.
- **No bar-wide sweep animation, no juice-y bounce** — the U2-5 calm discipline holds. The change is a quiet segment fade; the world stays the star.
- **Optional ember-flicker (U2-5 §3 stretch) stays warmth-only** — the rightmost filled segment of the WARMTH bar may keep its ±6% alpha breathe at ~1.5 s; hunger/thirst do NOT flicker (flicker = fire; a flickering water bar reads wrong). One need's signature life-detail, not a HUD-wide effect. Cut if it judders in-build (U2-5 floor).

### 4.2 Per-need 3-band fill ramp (the filled run cools toward the need's critical tone)

Each need's filled run **shifts color as it empties** — the same U2-5 mechanism (whole filled run color-shifts across bands, no separate alarm element), but each need cools toward ITS OWN critical tone, so a low bar tells you *which* need is dropping by color alone:

| Band (Current01) | Warmth | Hunger | Thirst |
|---|---|---|---|
| **Safe (≈1.0–0.6)** | ember-gold `#E8B25C` (0.91,0.70,0.36) | leaf-green `#5FA83C` (0.37,0.66,0.24) | water-blue `#3E8FC4` (0.24,0.56,0.77) |
| **Warning (≈0.6–0.3)** | dusk-orange `#D98A4E` (0.85,0.54,0.31) | dry-olive `#A89236` (0.66,0.57,0.21) | pale-teal `#5FA9B0` (0.37,0.66,0.69) |
| **Critical (≈0.3–0.0)** | coal-red `#B5563C` (0.71,0.34,0.24) | parched-amber `#B5803C` (0.71,0.50,0.24) | dry-grey-blue `#6E8A9C` (0.43,0.54,0.61) |

**Logic of each ramp (the feel, not arbitrary):**
- **Warmth** — gold → orange → coal-red: fire cooling to embers, **unchanged from U2-5 §3.**
- **Hunger** — green → olive → parched-amber: *fresh food drying to none* — the green desaturates and warms toward a hungry amber-brown (a wilting / empty-larder read). Never a screaming red — a tired amber.
- **Thirst** — blue → teal → grey-blue: *water draining to dry* — the blue desaturates and greys toward a dusty dry-grey-blue (a "the well is going dry" read). Never cyan-alarm — a muted dusty blue.

**All three critical tones are muted, warm-leaning, sub-1.0 — never `#FF0000` alarm-red.** The U2-5 single-most-important call ("a bright alarm red would shatter the warm-lush calm") holds for all three: each need's "danger" is its OWN color going *tired/muted*, not a shared red panic. The player learns "amber = starving, dusty-blue = parched, coal = freezing" — three distinct critical reads, no text.

### 4.3 Critical-state treatment (`IsCritical`) — consistent across all three (AC2)

`IsCritical` is the shared boolean each need exposes. When a need goes critical, **the SAME treatment applies to all three** (consistency is the AC):

- The bar is **already in its Critical-band color** (§4.2) by the time `IsCritical` trips (the bands and the flag should roughly coincide — confirm with the need-model tuning, §8 Q3).
- **The need's GLYPH gets a slow pulse** — a ~1.0 s ease-in-out alpha breathe between ~0.55 and 1.0 on the glyph only (NOT the bar, NOT the whole row). A slow breathe, **not a blink/flash** — flashing is console-game language and breaks the painterly calm (U2-5 rule). The pulse draws the eye to *which* glyph (flame/berry/droplet) is in trouble without a loud alarm. One pulse pattern, three glyphs — consistent.
- **No red vignette, no screen-edge alarm, no death overlay** — out of scope (U2-5 §3 empty-state floor; ticket OOS: "death/fail-state HUD"). The pulsing glyph + the critical-band bar color is the whole "this need is urgent" read.
- **Empty floor (Current01 = 0):** the bar is all charcoal, the glyph dimmed (U2-5 §3 floor) — but if `IsCritical` is still true at zero, the dimmed glyph keeps its slow pulse so "you are out of X" still reads. No fail-state beyond that this milestone.

> **Timing summary (one table for Devon):**
> | Motion | Duration | Easing | Scope |
> |---|---|---|---|
> | Segment fill/drain cross-fade | ~250 ms | ease-out | the one changed segment |
> | Critical-glyph pulse | ~1.0 s cycle | ease-in-out | the critical need's glyph only |
> | Warmth ember-flicker (optional) | ~1.5 s cycle, ±6% alpha | — | warmth rightmost-filled segment only |
> All are cheap alpha/color tweens — no per-frame bar lerp, no allocation (the U2-5 build-safe discipline).

---

## 5. Tonal fit + cross-surface coherence (extends U2-5 §5)

- **One family, three glows.** Same form, plate, segment grammar, motion, shared charcoal — three bars that obviously belong together, distinguished only by their identity color + glyph. The HUD reads as one system, not three stat-bars bolted on.
- **The HUD's gold still ties to the gameplay UI.** `gameplay-ui-direction.md` reuses the warmth ember-gold for the belt's selected-slot rim and "full stack" tint — "gold = warm/active/yours." Adding hunger-green + thirst-blue does NOT break that: gold remains warmth's identity in the HUD; green/blue are *new need identities*, and they happen to echo the world's leaf-green (`accent-leaf`) and water-blue, so the whole color system stays of-the-world.
- **Every channel sub-1.0, drawn from the world's own colors** (ember-gold, leaf-green, water-blue, the desaturated critical tones, shared charcoal) — the HUD uses the world's palette, which is what makes it recede into the painting (U2-5 §5). No pure white, no neon, no alarm-red.
- **Recede-when-safe** — at full needs all three bars are calm and easy to ignore; the design only pulls the eye as a need drops (its run cools toward its critical tone; its glyph pulses only when critical). The HUD earns attention exactly when a need is urgent and stays out of the way otherwise — three needs, same discipline.
- **World-stays-the-star, enforced by layout** — three left-anchored bars + one ledger row in the quietest corner; right 60% and top of frame clean.

---

## 6. Implementation notes for Devon (this milestone — IMGUI N-bar generalization)

- **Generalize, don't rebuild (ticket AC3/AC4).** Turn `SurvivalHud.cs`'s single warmth draw into a reusable `DrawNeedBar(need, ramp, glyph, rowIndex)` routine called 3× from a `NeedBar[]`/list. Warmth's existing look/feel, subscription, right-to-left empty, charcoal, and its tests are **preserved unchanged** — the generalization wraps it, it does not alter it. A 4th need = add one array entry (AC4 satisfied by the uniform widget §2).
- **Subscribe, never poll (AC1).** Each bar subscribes to its need's `Changed` and caches `Current01`/`IsCritical`; the IMGUI `OnGUI` draws from the cached values — same pattern `SurvivalHud` already uses for warmth. Unsubscribe on disable (the existing warmth pattern carries).
- **Primitive discipline (unchanged):** flat color rects via `GUI.DrawTexture(rect, Texture2D.whiteTexture)` + `GUI.color` per segment (filled-band color vs shared charcoal). **No custom shader, no mesh, no Polygon primitive** — build-safe IMGUI, the U2-5 §6 technique. Glyphs are small textures or `GUI.Label` marks left of each bar.
- **Tests (AC5 — extend, don't replace `SurvivalHudTests`/`SurvivalHudPlayModeTests`):** assert all three bars present + each bound to its need; drive each need via `TickSeconds`/its satisfaction hook and assert that bar's `filledCount` tracks across the band thresholds (the U2-5 FLOOR boundary asserts, ×3); assert the warmth bar's existing asserts still pass (no regression). Sample decay over a real `Time.time` window, never per-frame deltas (`unity-conventions.md`).
- **Shipped-build capture gate:** all three bars visible + each one moving as its need decays/satisfies, captured from the BUILT exe (not just the editor) — per the ticket's UX-visible gate + the editor-vs-runtime divergence rule.

---

## 7. The IMGUI → UI-Toolkit HUD migration (its own follow-up ticket — FLAGGED, not this one)

`ui-toolkit-panels-ux-spec.md §7` and Erik `ui-toolkit-vs-ugui-fh.md` (Application notes) both pre-flagged: **when the 2nd + 3rd need-meters arrive, the IMGUI warmth bar should migrate INTO a UI Toolkit HUD document** so all three needs share one UI Toolkit tree (Erik: "the right time is the milestone that adds the second need"). That milestone is now.

**Direction call:** the migration is **NOT** part of `86caamkxv`. The ticket explicitly says *"generalize the existing warmth HUD, do not rebuild it from scratch"* and *"extend `SurvivalHudTests`"* — i.e. it wants the THREE bars shipped fast in the already-proven IMGUI path, not gated behind a render-system rewrite. Forcing a UI-Toolkit rebuild into this ticket would (a) contradict its "don't rebuild" instruction, (b) risk warmth-regression (AC3) during a render-system swap, and (c) bloat an M-sized ticket into an L. **So: ship the three bars in generalized IMGUI now (this ticket); migrate the whole HUD to UI Toolkit as a SEPARATE follow-up.**

**Proposed follow-up ticket (for the orchestrator/Priya to file — I do not file tickets):**
- **Shape:** `feat(ui): migrate need-meter HUD from IMGUI to UI Toolkit (one HUD UIDocument)` — move the three need bars (and optionally the ledger) from `SurvivalHud.cs` IMGUI into a `HudDocument` UIDocument, per Erik's HUD-body-column pattern (3 `VisualElement` containers, 10 segment children each, `display:none` to hide segments past the fill level, `ToTarget`/event-push binding on each need's `Current01`).
- **Why a follow-up, not now:** lower risk (the IMGUI three-bar version proves the layout/palette/motion FIRST; the migration then targets a known-good visual target), and it keeps `86caamkxv` at M. The BootHud build-stamp plate (top-right, load-bearing for soak) and the warmth bar must stay readable through the migration — never covered by the new UIDocument (Erik §E6 layer-order note).
- **This direction carries over unchanged.** The layout (§1), per-need colors + glyphs (§3), band ramps + timing + critical treatment (§4), and the one-widget-reused-3× shape (§2) are **render-system-agnostic** — they map directly onto the UI Toolkit `:root` palette (the `--slot-selected` ember-gold IS warmth's gold; add `--need-hunger`/`--need-thirst` tokens for the two new ramps) and USS `transition`s (the ~250 ms segment fade + ~1.0 s critical pulse become USS `transition`s, no per-frame Update). The migration is a render-path port, not a redesign.

> **Sequencing:** this is a recommendation, not a decision — the orchestrator/Sponsor own ticket priority. The migration is reversible and low-stakes (the IMGUI version keeps shipping until it lands). Logged as a Decision draft in my final report for Priya's weekly DECISIONS batch.

---

## 8. Open questions (calls within my authority made; these are soak/tuning confirmations, not blockers)

**None of these block `86caamkxv` — they're soak/tuning confirmations Devon resolves against the merged need models + a Sponsor soak.** The direction above is complete and implementable as written.

- **Q1 — Stack order (warmth-bottom / hunger-mid / thirst-top).** A feel call (§1.1) — order-introduced + warmth-stays-bottom. **One-line reorder if the Sponsor's eye wants it different** (the bars are a uniform array). `needs-soak`.
- **Q2 — Hunger/thirst color anchors + glyphs (§3).** Made within my visual-direction authority (no prior lock in DECISIONS.md — only that the three needs exist). Leaf-green + water-blue reuse the world's own colors; berry-sprig + droplet glyphs. **QA pin for Tess; Sponsor soak can retune** any of the six band hexes (they're constants, cheap to dial). `needs-soak`.
- **Q3 — Band cutoffs vs `IsCritical` (§4.2/§4.3).** The safe/warning/critical band cutoffs (0.6 / 0.3) are a starting proposal tied to the *feel*, not the need models' tuned decay/critical thresholds. **Ask the need-model owners (`86caamkp8`/`86caamkv7`):** confirm `IsCritical` trips at roughly the bottom band so the critical-band color + the glyph pulse coincide. Cheap to retune — they're constants.
- **Q4 — Plate strategy (three discrete vs one tall).** Recommended three discrete plates (§2); Devon's implementation call. Re-verify plate legibility over the new saturated-green terrain in soak (`style-guide-v2.md §6` watch-item). `needs-soak`.

**86caamkxv is UNBLOCKED for implementation** by this direction: layout, per-need identity, motion, and critical treatment are all specified; the four open questions are soak/tuning confirmations, not design gaps. (The ticket's OTHER dependency — the hunger `86caamkp8` + thirst `86caamkv7` need models existing — is a separate gate this doc does not affect.)

---

## 9. Out of scope (per ticket)

The hunger/thirst NEED models + decay + satisfaction (`86caamkp8`/`86caamkv7`); a needs-balance/tuning pass (the need tickets own defaults); the inventory/belt UI (`86caa4bya`); a death/fail-state HUD or red vignette; a settings toggle to hide bars (not spec'd here — if the Sponsor later wants one, it registers into the settings panel `86caa4bqp` as a future add); the IMGUI→UI-Toolkit migration ITSELF (§7 — its own follow-up ticket); audio cues for low/critical needs (a separate audio-direction concern — not specced here; flag if the Sponsor wants a soft low-need stinger, it'd be a `audio-direction.md` follow-up).

---

## Cross-references

- Ticket **`86caamkxv`** (this direction implements into it) · `86caamkp8` (hunger need — read surface) · `86caamkv7` (thirst need + pond — read surface) · `86caa5zz3` (bushes/berries — hunger source).
- [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) — the PARENT warmth glow-bar spec (§2 layout / §3 ember band + segments + plate + flicker / §4 ledger / §5 tonal fit / §6 IMGUI primitive) this extends; warmth look/feel preserved verbatim (AC3).
- [`gameplay-ui-direction.md`](gameplay-ui-direction.md) — the carved-wood gameplay-UI palette (ember-gold / cream / coal-red / `accent-leaf` green the §3 hunger color echoes); §9 item-model note (ledger reconcile).
- [`ui-toolkit-panels-ux-spec.md`](ui-toolkit-panels-ux-spec.md) §7 — the migration pre-flag (need #2 arrives) §7 here answers; the `:root` palette the migration extends.
- [`style-guide-v2.md`](style-guide-v2.md) §6 — world palette anchors (canopy green `#4C9E3A`, water blue `#3E8FC4`) the §3 need colors reuse; the saturated-terrain HUD-plate-legibility watch-item.
- [`team/erik-consult/ui-toolkit-vs-ugui-fh.md`](../erik-consult/ui-toolkit-vs-ugui-fh.md) — UI Toolkit is the forward HUD system; the HUD-body-column UI Toolkit pattern + the "migrate when need #2 arrives" note §7 acts on.
- `Assets/Scripts/Runtime/SurvivalHud.cs` — the existing IMGUI warmth HUD this ticket generalizes to N bars (subscribe-`Changed`-read-`Current01`, right-to-left, charcoal).
- `Assets/Scripts/Runtime/BootHud.cs` — the top-left title + top-right BUILD-stamp plates (unchanged, uncovered); plate-alpha family the need plates match.
- `.claude/docs/unity-conventions.md` — IMGUI build-safety; real-`Time.time`-window test rule.
- `.claude/docs/art-direction.md` + `inspiration/` — warm/lush, small-player/big-world, sub-1.0 cohesive palette (the HUD borrows the world's own colors).
- DECISIONS 2026-06-17 — M-U2 expanded to three needs (warmth + hunger + thirst); the three share the `WarmthNeed` model surface (`Current01`/`Max`/`IsCritical`/`Changed`).
