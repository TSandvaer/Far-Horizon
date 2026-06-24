# Need-Meter UI — Visual + UX Direction (warmth · hunger · thirst)

> **⚠ SUPERSEDED (for implementation) by [`hud-three-bar-spec.md`](hud-three-bar-spec.md)** — the reconciled implement-to spec for `86caamkxv`. This doc proposed berry-red hunger; the SHIPPED build resolves hunger to GREEN (`#8CB85C`) and thirst to water-blue (`#3E8FC4`+droplet) — the reconciled spec wins where they differ. Kept for the design rationale / tonal exploration; do not implement from it.

**Scope:** M-U2 EXPANDED to three survival needs (`mu2-scope-expanded-hunger-thirst` — Sponsor 2026-06-17: WARMTH-only loop now ALSO carries hunger (berries) + thirst (pond)). The gameplay wave + bushes ticket `86caa5zz3` carry it.
**Owner:** Uma (direction) → Drew/Devon (implement later) · Reviewer: orchestrator.
**Status:** DIRECTION — docs only, no implementation here. Engine-agnostic; UI-Toolkit specifics noted where useful.
**Builds on (do NOT duplicate):** [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) (the SHIPPED warmth ember glow-bar — this extends its reserved column) + [`gameplay-ui-direction.md`](gameplay-ui-direction.md) (PR #66 — belt/inventory/settings; the "carved from the same wood" UI material this must sibling) + [`style-guide-v2.md`](style-guide-v2.md) §6 (world palette anchors).
**Source of truth:** the board PNGs in [`inspiration/`](../../inspiration/) (looked at them). Vision arc: [`.claude/docs/vision-far-horizon-game-concept.md`](../../.claude/docs/vision-far-horizon-game-concept.md) (bonfire WARMTH → berries HUNGER → pond THIRST).

---

## 0. Tonal anchor (read this first)

**Three needs read as three living things the world gives and the wild takes back — not three stat-bars stacked in a corner.** The vision arc is gentle and bodily: the castaway *dries at the fire*, then *gets hungry*, finds berries for "small satisfaction," then *gets thirsty* and scoops pond water "satisfying a small amount with each scoop." That cadence — slow want, small relief — is the feel. Each meter must encode its OWN element so a glance reads *which* need without a label:

- **WARMTH is fire** — the banked ember being eaten by cold (already shipped: `u2-5` §3, ember-gold→coal-red glow-bar, empties right-to-left like a fire burning down). This is the parent; it set the language.
- **HUNGER is the harvest** — a warm ripe-berry/bread read that *drains toward an empty husk*. Earthy-warm, not fire-warm — distinct from warmth's gold so the two never read as the same need.
- **THIRST is fresh water** — the only COOL note in the cluster: a bright stream-blue (`#3E8FC4`, the `21h16_13` river / `21h16_52` lake) that *drains toward a dry/grey basin*. The single cool element is deliberate — it's the one need the warm fire can't fix, so it earns the one cool color and reads instantly as "the water one."

**The gate (same as every surface):** these live at the edge of vision. At full, all three RECEDE — calm, glanceable, the world keeps the frame. They earn the eye only as they drain. If a meter ever reads as a hostile red console gauge instead of a warm diorama element quietly slipping, it's wrong even if it's legible. Warm toy-world over game-HUD. **Every channel sub-1.0** (HDR/sRGB-clamp discipline carries from `u2-5` + Zone-D — saturated, never blown-out). No pure white, no `#FF0000`, no neon, no flashing.

**Why three-as-a-family matters:** warmth shipped alone. Adding two more is the moment the corner could tip from "diegetic glow" into "stat panel." The whole job of this doc is to make the trio read as ONE small honest body-readout in the world's own colors — three siblings of the fire, not a HUD bolted on.

---

## 1. Layout — the bottom-left "body column"

The shipped warmth bar lives bottom-left (`u2-5` §2) and **explicitly reserved the stack-up slot**: *"M-U3's second need stacks UP from the warmth bar in the same column; reserved, not built now."* This is that build-out. The three needs form a tidy vertical **body column** in the calmest corner, farthest from both BootHud plates (title top-left, BUILD stamp top-right — do NOT cover the stamp; load-bearing for every soak).

```
+--------------------------------------------------------------+
| [Far Horizon]                              BUILD <tag|utc|sha>|
|                                                              |
|                      ( the world — the star )                |
|                                                              |
|   ( ~ thirst   ▰▰▰▰▰▱▱▱▱▱ )                                  |  <- thirst   (top of column)
|   ( ~ hunger   ▰▰▰▰▰▰▰▱▱▱ )                                  |  <- hunger   (middle)
|   ( ▲ warmth   ▰▰▰▰▰▰▱▱▱▱ )                                  |  <- warmth   (bottom — UNCHANGED, shipped)
|                          [ belt: 1 2 3 4 5 ]                 |  <- belt (bottom-CENTER, #66 — never collides)
+--------------------------------------------------------------+
```

**Stack order (bottom-up = body priority):** WARMTH lowest (urgent + shipped; do NOT move it — it stays at `y = Screen.height - 44`, `x = 16`, per `u2-5`). HUNGER stacks directly above it; THIRST above hunger. Reading bottom-up matches "ground truth about my body," and warmth — the original/most-urgent need — anchors the column.

**Column math (extends `u2-5`'s left-anchored idiom):**
- All three left-anchored at `x = 16` — one clean left margin, world breathes on the right 60% + top.
- Row pitch ~36px (matches `u2-5`'s ledger-above-warmth spacing): warmth baseline `Screen.height - 44`; hunger `Screen.height - 80`; thirst `Screen.height - 116`.
- The **inventory ledger** (`u2-5` §4, the `axe ×1 wood ×3` row) moves UP to sit just above thirst (`~Screen.height - 152`) — the body column (needs) and the pack ledger (items) stay one tidy left stack. Cluster ceiling rises to `~Screen.height - 168`; still ≥16px from top, world still breathes.

> **No collision with #66.** The belt is bottom-CENTER (`gameplay-ui-direction.md` §3.1 deliberately ceded center so it never fights this corner); inventory + settings are centered modals over a dimmed world. The body column owns bottom-LEFT exclusively. Confirmed clean.

**Glyph label per need (language-free, diegetic — the only label):** a small ~12px icon left of each bar, no word.
- WARMTH = the flame `▲` (shipped, `u2-5` §3).
- HUNGER = a small **berry-cluster / sprig** glyph (three dots on a stem — reads "forage," ties to the bush).
- THIRST = a **water-drop** glyph (a single droplet — reads "water" instantly; the cool note even in the glyph).

---

## 2. The shared meter form — segmented ember-sibling bars

All three reuse the SHIPPED warmth bar's **form** verbatim so they read as one family: **10 flat segments** (filled count = `floor(Current01 × 10)`, clamped 0..10 — the deterministic rule pinned in `u2-5` §3 / Tess PR #9), filled-left, draining-right, on the same single low-alpha dark plate (`rgba(0,0,0,0.55)` — the BootHud plate-alpha family, `u2-5` §3). Segmented (not smooth) is diegetic ("portions remaining"), HDR-clamp-forgiving (flat fills, no gradient texture), and already proven in the build. What CHANGES per need is only the **fill palette** (the element) and the **drain metaphor** — never the form. That sameness-of-form + difference-of-color is exactly what makes three bars read as one body-readout rather than three competing widgets.

**Why not vary the form per need (e.g. a radial for thirst):** consistency of form is the recede-into-calm lever. Three different shapes would shout "three systems." Same shape, three colors = one quiet instrument with three honest channels. (Decision draft below.)

---

## 3. WARMTH — UNCHANGED (shipped; here for column context only)

Per `u2-5` §3, verbatim — **do not re-spec, do not touch.** Ember-gold `#E8B25C` (warm 100–60%) → dusk-orange `#D98A4E` (cooling 60–30%) → coal-red `#B5563C` (cold 30–0%); emptied segments cold charcoal `#2E2A2B`; band transition is a whole-run color shift, no flashing; optional ±6% alpha ember-flicker on the rightmost filled segment. It is the PARENT — hunger + thirst are designed as its siblings, borrowing its form and its sub-1.0 warm discipline. Listed here only so the column reads as a set.

---

## 4. HUNGER — the harvest meter (berries)

**Tonal read:** ripe-when-full, husk-when-empty. Warmth is fire; hunger is the *fed body* — a warm, earthy, sustenance read (ripe berry / warm bread), drifting toward a hollow dry husk as it empties. Earthy-warm, kept clearly distinct from warmth's gold so a glance separates "I'm cold" from "I'm hungry" with zero label-reading.

**Fill palette (sub-1.0, world-derived — the forage-ground colors of `21h22_33`'s berries/wildflowers):**

| State band | Fill color (filled segs) | RGB (0–1) | Reads as |
|---|---|---|---|
| Fed (≈100–60%) | ripe berry-red `#C24A4A` | 0.76, 0.29, 0.29 | nourished, satisfied |
| Peckish (≈60–30%) | warm clay `#B06A3C` | 0.69, 0.42, 0.24 | starting to want |
| Hungry (≈30–0%) | dull ochre-brown `#8A6A3A` | 0.54, 0.42, 0.23 | the empty-stomach ache |
| Emptied segments | dry-husk umber `#33291F` | 0.20, 0.16, 0.12 | the gone harvest (warm-brown, NOT warmth's cold charcoal — keeps hunger in the EARTH family) |

**Distinction discipline vs warmth:** hunger's berry-red (`#C24A4A`) is intentionally separated from BOTH warmth's ember-gold AND the axe's barn-red (`#A33B30`, `style-guide-v2` §6) — it's a softer, pinker, *fruit* red, not a fire or a tool. And critically: hunger's emptied segments are warm-UMBER (`#33291F`), where warmth's emptied segments are cold-CHARCOAL (`#2E2A2B`). The drain colors diverge so even a half-drained hunger bar can't be misread as a half-drained warmth bar. (Fire goes to ash/cold; food goes to dry/empty — two different "gone.")

**Drain metaphor:** drains right-to-left like warmth (column consistency), but the band-shift goes ripe→clay→ochre — a *ripening-in-reverse / fruit-drying* read, not a fire-cooling read.

---

## 5. THIRST — the fresh-water meter (pond)

**Tonal read:** the one COOL note in the warm corner. Thirst is the only need the fire can't fix, so it earns the one cool color — bright fresh stream-blue draining toward a dry grey basin. This single cool element is the strongest "this is the WATER one" signal possible; it also visually answers the vision arc beat ("gets thirsty after the berries → finds a pond").

**Fill palette (sub-1.0 — the world's own water blue, `style-guide-v2` §6 / the `21h16_13` river + `21h16_52` lake):**

| State band | Fill color (filled segs) | RGB (0–1) | Reads as |
|---|---|---|---|
| Slaked (≈100–60%) | bright stream-blue `#3E8FC4` | 0.24, 0.56, 0.77 | watered, fresh (the world's literal water color) |
| Dry-ish (≈60–30%) | pale teal `#5FA6B0` | 0.37, 0.65, 0.69 | starting to want water |
| Parched (≈30–0%) | desat dust-blue `#6E8A92` | 0.43, 0.54, 0.57 | the dry-throat ache (drained toward grey, not toward an alarm color) |
| Emptied segments | dry basin grey `#2A2E30` | 0.165, 0.18, 0.19 | the empty pond (a cool-grey basin — distinct again from warmth's charcoal + hunger's umber) |

**The cool-note rule (the load-bearing tonal call):** thirst is the ONLY non-warm color in the whole HUD cluster. That is intentional and must hold — it is what makes the trio instantly separable AND keeps the corner from going monochrome-warm-mush (three warm bars would blur together). The blue is sub-1.0, world-derived, and pulled slightly warm-of-pure-cyan so it still belongs to the cohesive palette — a *cool note within a warm chord*, not a cold intruder. Even fully parched it drifts to dust-grey, never to a screaming red — thirst's urgency is "everything's drying out," conveyed by desaturation toward grey, not by an alarm hue. (This keeps the no-screaming-red discipline of `u2-5` intact across all three needs.)

**Drain metaphor:** right-to-left; band-shift bright-blue→teal→dust — water evaporating / a pond shrinking in the sun.

---

## 6. Low-need warning states — quiet urgency, never alarm

The needs must *pull the eye exactly when the body needs help* and stay out of the way otherwise (the recede-when-safe rule, `u2-5` §5). One unified low-need language across all three — the player learns it once:

1. **Color is the primary alarm** (already in the bands above): each need cools/dulls toward its "ache" color as it drains. That whole-run color drift IS the warning — no separate alarm element (matches `u2-5`'s "band transition, not a separate alarm").
2. **Critical pulse (≤ ~15%, the "act NOW" floor):** the need's **glyph** (flame / berry-sprig / drop) does a slow **±8% alpha breathe at ~1.2s cycle** — a soft heartbeat, NOT a blink. Pure alpha modulation on the one glyph; no per-frame allocation (USS opacity transition or a single eased lerp). Slow breathe = "urgent but calm," reads as the body laboring, not a console alarm. This reuses + generalizes the warmth bar's optional ember-flicker (`u2-5` §3) into the shared critical-state language. Flashing/blinking is forbidden (console-game language; shatters the painterly calm).
3. **No death/fail overlay, no red vignette, no audio sting in THIS spec** (out of scope — needs cap at a simple floor this milestone, per `u2-5` §3 empty-state floor; fail-state + any cue is a separate concern). The dimmed glyph + ache-color is the whole "you are in trouble" read.
4. **At-zero floor:** the bar is all emptied-segments (its own husk/basin/ash color), glyph dimmed + breathing. Honest, quiet, no escalation.

**Multi-need-low case:** if two or three go critical at once, all their glyphs breathe — but at the SAME ~1.2s cycle (synchronized, not staggered) so the corner pulses as one calm body, not a panic of competing blinkers. (Devon: drive all glyph-breathes off one shared phase clock.)

---

## 7. Satisfaction feedback — the eat-berry / drink-water beat

The vision's signature feel is **small relief, repeated**: "small satisfaction to his hunger" per harvest; "small amount of thirst with each scoop." The feedback must read as *gentle, incremental, earned* — a sip and a nibble, not a power-up. This is the one place the needs get a little active juice (everything else recedes).

**The shared satisfaction beat (one language, two needs):**
1. **Segment fill-up tween:** on eat/drink, the gained segment(s) fill in with a quick **~180ms ease-out grow + a one-time soft warm-bloom pulse** on the newly-filled segment(s) — the bar visibly *gains ground* with a small satisfied glow, then settles. Incremental: a single berry / single scoop fills a small amount (often <1 segment — accumulate fractional, light the segment when its 1/10th is earned, per the floor rule), so the player sees the bar creep up bite-by-bite, matching "small satisfaction each time." Big enough to feel good, small enough to keep the loop going (you go back for more).
2. **Glyph acknowledgment:** the need's glyph does a single **gentle ~150ms scale-pop (1.0→1.12→1.0)** + a brief brighten — a little "ahh, that's better" nod. One-shot, not looping.
3. **Per-need flavor (the only difference):**
   - **EAT BERRY (hunger):** the fill-pulse tints momentarily toward the ripe berry-red — a warm "fed" flush. Pairs with the harvest gesture on the bush (`86caa5zz3`).
   - **DRINK WATER (thirst):** the fill-pulse tints momentarily toward the bright stream-blue — a cool "refreshed" flush. Matches the hand-scoop-from-pond gesture; each scoop = one small beat (the vision's "with each scoop"). When the player later crafts a cup (vision: "hold more water"), the SAME beat just fills more segments per use — the feedback scales, the language doesn't change.
4. **No floating "+1" toast, no acquisition feed** (matches `u2-5` §4's quiet-ledger discipline — the bar simply gains; the satisfaction is the fill-tween + glyph-pop, not a number popping off). Keep it diegetic and calm.

**Warmth's satisfaction beat (for consistency — drying at the fire):** the SAME beat applies when warmth refills near the bonfire — segments grow in with the warm-bloom pulse + flame-glyph pop. This was implicit in `u2-5` (warmth rises near fire); naming it here makes the three needs share ONE satisfaction language. (Minor extension of `u2-5`, not a contradiction.)

---

## 8. Composition with the #66 gameplay UI (belt / inventory / settings)

The need column and the #66 surfaces are designed to **never fight and to share one material**:

- **Spatial separation (no overlap):** needs own bottom-LEFT (this doc); belt owns bottom-CENTER (`#66` §3); inventory + settings are centered modals (`#66` §2/§4). Three disjoint regions. The world breathes between them.
- **When inventory (Tab) or settings (Esc) opens:** the world dims to ~60% behind the centered panel (`#66` §2.1/§4.1). **The need column stays fully visible + un-dimmed** — it's a persistent body-readout, not part of the dimmable world. The player must still see they're freezing/starving while sorting their pack. (Devon: the need-HUD draws ABOVE the dim scrim; it is not behind it.) This is a deliberate divergence from the world-dims rule: needs are body-truth, always legible.
- **Shared material, two rendering systems — reconcile:** the need bars are SHIPPED in IMGUI (`u2-5` §6, BootHud family). The #66 belt/inventory/settings are NEW UI Toolkit (UXML/USS). They coexist (Erik's UI-Toolkit research: UI Toolkit + legacy IMGUI/Input run cleanly side-by-side; `ui-toolkit-inventory-settings-research.md` E1). **The palette is the bridge:** both systems draw from the SAME sub-1.0 warm tokens —
  - need-bar ember-gold = `#66`'s belt selected-rim gold = `slot-selected` `#E8B25C` (one gold means "warm/active/yours");
  - need emptied-charcoal/umber/basin family ≈ `#66`'s `panel-walnut`/`slot-empty` dark warm plates (one dark-warm family);
  - cream labels, where any need ever shows text, = `#66`'s `ink-cream` `#EAD9B8`.
  So even across IMGUI↔UI-Toolkit the player sees ONE warm wooden-toybox visual family, not two UI systems. (This is the same "sibling of BootHud chrome" gate `#66` §0 set.)
- **Belt-bottom-center clearance:** the need column's lowest row (warmth, `Screen.height-44`) and the belt (bottom-center) are horizontally disjoint, but verify in soak that a 5-slot belt at small window widths doesn't crowd the `x=16` column — if it ever does, the belt yields (it's center-anchored and can shrink its plate), the needs hold the corner. (Soak watch-item, not a spec change.)

---

## 9. Implementation notes (engine-agnostic + UI-Toolkit specifics)

- **System choice — STAY IMGUI for the need bars** (extend `SurvivalHud`, the shipped warmth bar). Reasons: (a) it's the lowest-risk path — warmth already ships there and `u2-5` §6 proves IMGUI flat-rect bars are build-safe (never strip to magenta); (b) hunger + thirst are *the same bar* with a different fill color — literally three calls to the existing segment-draw with three palettes; (c) no UI-Toolkit Canvas/UIDocument setup needed for a peripheral readout. Draw each segment as `GUI.DrawTexture(rect, Texture2D.whiteTexture)` with `GUI.color` per segment (band color vs the need's emptied color) — the technique already in `SurvivalHud`. **No custom shader, no mesh, no Polygon primitive** — flat color rects only (carries the `u2-5` §6 primitive discipline).
- **If the team later moves the HUD to UI Toolkit** (e.g. to unify with #66's UIDocument): this layout + the three palettes + the segment/drain/critical-breathe/satisfaction rules carry over UNCHANGED — they're system-agnostic. In UI Toolkit each bar is a `VisualElement` row (flame/berry/drop `Image` glyph + 10 child segment `VisualElement`s); band color via USS class swap (`.seg--fed`/`.seg--peckish`/…); critical-breathe + satisfaction-pulse via USS `transition` on `opacity`/`scale` (no per-frame Update alloc, per `unity6-mastery.md` §UI Toolkit + #66 §9). Either way the spec holds.
- **Animations are cheap + alloc-free:** critical-breathe = one shared eased alpha lerp (synchronized phase clock, §6); satisfaction = one-shot ~180ms segment-grow + ~150ms glyph-pop tweens (`u2-5`-style, no per-frame garbage). Keep them snappy and calm.
- **Data contract (parallels `u2-5` §7 — pin against the merged need models when wiring):** each need exposes a normalized `Current01` (0..1 clamped) + a `Changed` event + an `IsCritical` flag (warmth already has these — `WarmthNeed.Current01` / `IsCritical` / `Changed`, `u2-5` §7). Hunger + thirst models (gameplay wave / `86caa5zz3`) should mirror that EXACT surface — `HungerNeed.Current01` / `ThirstNeed.Current01`, same `Changed`/`IsCritical` shape — so the HUD binds all three identically with no special-casing. The satisfaction beat fires off the `Changed` event when the value rises (eat/drink/warm); the critical-breathe off `IsCritical`. (Decision draft below pins the naming.)
- **Glyphs:** flame (shipped), berry-sprig, water-drop — tiny ~12px. Render as flat sprites (same `IconBaker`-style render route as #66 §6, or simple authored glyphs — they're 12px chrome, either works). Sub-1.0, warm/cool per their need.

---

## 10. Out of scope (this milestone)

The harvest/scoop GATHERING mechanics themselves (berry-bush + pond interactions — `86caa5zz3` / the gameplay wave; this only presents the NEED state they feed); decay tuning + band-cutoff numbers (proposed by feel, Drew tunes the rates — cheap constants, retune in soak per `u2-5` §7 Q5); death/fail overlays + low-need audio cues (separate concern — no cue/bus direction here); the craftable cup (vision: "hold more water" — the satisfaction beat scales to it for free, but the cup item/craft is its own ticket); difficulty/scariness scaling of decay rates (vision: kid/adult adjustable — surfaces into the #66 settings panel registry later as `setting-row--slider` entries; not built here); a fourth+ need (none planned).

---

## Decisions (drafts — for Priya's weekly DECISIONS.md batch; I do not edit DECISIONS.md directly)

- **Decision draft:** Three needs share ONE meter FORM (10-segment flat bar, draining right-to-left, on the `u2-5` dark plate) and differ ONLY by fill palette + drain metaphor — no per-need shape variation (e.g. no radial for thirst). Rationale: form-sameness is the recede-into-one-calm-instrument lever; three shapes would read as three systems. (`need-meter-ui-direction.md` §2.)
- **Decision draft:** THIRST is the single COOL element in the bottom-left HUD cluster (stream-blue, the world water color); warmth + hunger stay warm. The lone cool note is the instant "this is the water need" signal and prevents three warm bars blurring together. (`need-meter-ui-direction.md` §0/§5.)
- **Decision draft:** Need bars stay IMGUI (extend `SurvivalHud`), NOT migrated to #66's UI Toolkit system this milestone — lowest-risk, build-proven, hunger/thirst are the shipped warmth bar with a new palette. Palette tokens bridge the IMGUI↔UI-Toolkit systems into one warm visual family. (`need-meter-ui-direction.md` §8/§9.)
- **Decision draft:** Hunger + thirst need models expose the SAME `Current01` / `Changed` / `IsCritical` surface as the shipped `WarmthNeed`, so the HUD binds all three identically. (`need-meter-ui-direction.md` §9.) — for whoever lands the gameplay-wave need models (`86caa5zz3`).

---

## Cross-references

- [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) — the SHIPPED warmth ember glow-bar this extends (§2 reserved-column, §3 segment form + ember palette + flicker, §5 recede-when-safe, §6 IMGUI primitive discipline, §7 `Current01`/`Changed`/`IsCritical` data contract).
- [`gameplay-ui-direction.md`](gameplay-ui-direction.md) — PR #66: belt (bottom-center) / inventory (Tab) / settings (Esc); the "carved from the same wood" UI material + `#E8B25C` gold / `#EAD9B8` cream / `panel-walnut` tokens this composes with (§8).
- [`style-guide-v2.md`](style-guide-v2.md) §6 — world palette anchors: water blue `#3E8FC4` (thirst), the warm/saturated/sub-1.0 discipline; axe barn-red `#A33B30` (kept distinct from hunger's berry-red).
- [`team/erik-consult/ui-toolkit-inventory-settings-research.md`](../erik-consult/ui-toolkit-inventory-settings-research.md) — E1: UI Toolkit (#66 surfaces) + legacy IMGUI/Input (need bars) coexist cleanly (the §8 system-coexistence basis).
- [`inspiration/`](../../inspiration/) — board ground truth: `21h16_13` (campfire-at-feet / river-blue / faceted terrain — the whole-game frame), `21h16_52` (bright lake — pond/thirst water color), `21h22_33` (forest meadow berries + wildflowers + mushrooms — the forage/hunger ground), `21h10_44` (nature set — bush/rock vocab), `21h13_31` (grassland feel — small-player/big-world recede gate).
- [`.claude/docs/vision-far-horizon-game-concept.md`](../../.claude/docs/vision-far-horizon-game-concept.md) — the bonfire→berries→pond arc + "small satisfaction each time" cadence (§7 satisfaction beat).
- [`.claude/docs/art-direction.md`](../../.claude/docs/art-direction.md) — chunky-cartoon board catalog + sub-1.0 warm carry-overs.
- [`.claude/docs/unity6-mastery.md`](../../.claude/docs/unity6-mastery.md) §UI Toolkit — the alternate implementation primitive (if/when the HUD migrates).
- Tickets: `86caa5zz3` (bushes / gameplay wave — feeds the hunger+thirst need state) · the gameplay-wave decomposition (Priya). Memory: `mu2-scope-expanded-hunger-thirst`.
