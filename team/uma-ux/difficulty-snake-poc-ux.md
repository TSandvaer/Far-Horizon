# UX Spec — Difficulty selector + Snake POC feel/menace

**Tickets:** difficulty directive (memory `difficulty-settings-easy-medium-hard`, Sponsor 2026-06-19) · settings panel `86caa4bqp` (PR #83, the registry this rides) · snake POC `86caaz4vn` (design-locked, build HELD)
**Owner:** Uma (direction) → Devon (settings-panel archetype + audio bus) / Drew (snake feel + telegraph) · Reviewer: orchestrator
**Status:** DIRECTION — docs only, no implementation here. Engine-agnostic; UI-Toolkit + Unity specifics noted where useful.
**Builds on (do NOT duplicate):** [`gameplay-ui-direction.md`](gameplay-ui-direction.md) §1 (the "carved from the same wood" UI material) + §2 (the settings-panel workbench + row archetypes A/B/C this EXTENDS) · [`style-guide-v2.md`](style-guide-v2.md) §1 (shared grammar), §2 (character proportions — the snake siblings the castaway), §6 (palette anchors) · [`need-meter-ui-direction.md`](need-meter-ui-direction.md) (the warm-toy-over-HUD gate) · [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) (ember-band palette).
**Source of truth:** the board PNGs in [`inspiration/`](../../inspiration/) — looked at them: `21h00_32` (chunky castaway — the snake must read as a sibling of THIS toy), `21h10_44`/`21h11_03` (nature blob vocabulary the snake lives in), `21h13_31`/`21h22_33` (the warm sunlit world the snake must not turn into a horror frame).

---

## PART A — DIFFICULTY SELECTOR UX

### A0. Tonal anchor (read this first)

**Difficulty is a kindness, not a gate — "how gentle should the world be?", not "are you good enough?".** The Sponsor's frame is one game for *both* a kid and an adult (memory `difficulty-settings-easy-medium-hard`): easy is a soft, low-pressure ramble where the world mostly gives; hard is a genuine adult survival squeeze. The selector must read warm and inviting, never as a competence test or a wall of "casual / normal / nightmare" bravado. It is the workbench's first *world-shaping* knob — and unlike the speed/zoom sliders (which tune ONE param), this one knob quietly re-tunes many systems at once (need-decay, snake aggro/speed, future hazards). The UI hides that machinery: the player picks a feeling, the world obliges.

**The gate:** if the control reads as a cold dropdown or a punitive label set, it's wrong even if it's functional. Three warm carved choices in the same wooden drawer as everything else.

### A1. Where it lives — a new row in the settings panel

The difficulty selector is a **registered setting in the existing settings panel** (`86caa4bqp` / PR #83 registry — `gameplay-ui-direction.md` §2.2), NOT a separate screen. This is the right home for four reasons:
1. The settings panel is *already* the project's one place for tunable knobs, and the difficulty memory explicitly says "surface difficulty as a settings-panel property (ties into the settings registry, ticket 86caa4bqp)."
2. It inherits the whole warm-wooden-drawer material (`gameplay-ui-direction.md` §1) for free — one family, no new visual system.
3. It is live-tweakable mid-soak (the registry's whole point) — the Sponsor flips easy↔hard and watches need-decay + snake aggro re-tune behind the dimmed panel, exactly the F9-nudge live-tune workflow the panel exists for.
4. It is the registry's first *enum* setting — so it earns a fourth reusable archetype that all future enum settings (future graphics presets, control schemes) slot into cleanly.

**Placement within the panel:** difficulty is the **first row, at the very top**, above the speed/zoom sliders, separated from them by a slightly heavier `panel-edge` divider — because it is the *meta* knob (it governs the others' feel), it reads first. A small `ink-dim` sub-label under the header — `World` — groups it as the "what kind of world" control, distinct from the `Tuning` sliders below. (One word, language-light; optional, Devon's call.)

### A2. Control type — `setting-row--choice` (the new fourth archetype)

The existing panel has three row archetypes (`gameplay-ui-direction.md` §2.2): A slider (float), B range (min/max), C stepper (int). Difficulty is an **enum-of-3** — none of those fit. **Add Archetype D — `setting-row--choice`: a horizontal segmented 3-way toggle** (a "segmented control" — three abutting chunky pill-buttons in one wooden track, exactly one selected).

**Why a segmented 3-toggle, NOT a dropdown:**
- All three options are **always visible** — the Sponsor (and a kid) sees the full range at a glance, no click-to-discover. For exactly-three discrete choices, a dropdown is strictly worse UX (an extra click + hides two-thirds of the information).
- It reads as **a slider with three notches** — left-to-right = gentle-to-hard, a spatial "more pressure as you go right" metaphor that needs no reading. This mirrors the panel's other left-to-right sliders, so it feels native to the drawer.
- Chunky segmented pills ARE the toy-warm idiom — three rounded wooden tabs, the selected one lifted and gold-rimmed, is the same vocabulary as the belt's selected-slot (`gameplay-ui-direction.md` §3.2). One selection-language across the whole UI.

**Visual spec (inherits the §1 palette verbatim):**
- One `panel-walnut` track-plate with the `panel-edge` rim, ~44px tall (matches the panel's row rhythm), holding three equal-width abutting segments.
- **Unselected segment:** `slot-empty` fill, `ink-dim` label — recessed, quiet.
- **Selected segment:** `slot-selected` ember-gold rim (`#E8B25C`, ~2.5px) + a subtle warm inner glow + a tiny upward lift (~2px translate-up) + `ink-cream` label — the SAME "popped forward / this is the one" read as the selected belt slot. Reusing the warmth-gold ties difficulty-selection to the game's warm identity (gold = "active / yours" everywhere — `gameplay-ui-direction.md` §7).
- **Hairline dividers** (`panel-edge` @ α0.4) between the three segments so they read as three tabs in one track, not one blurred bar.
- **Switch feel:** clicking a segment slides the gold rim segment-to-segment with a quick ~80ms ease (the SAME rim-slide as belt-scroll — `gameplay-ui-direction.md` §3.2). A satisfying snap; tactile, not instant-pop. (USS `transition` on the rim's `translate` — no per-frame script.)

### A3. Labels — warm words, not bravado

**Use `Easy · Medium · Hard`** — plain, warm, universally understood (a kid reads them; the Sponsor's own directive names them). Reject the "casual/normal/nightmare/insane" ladder — it's the cold-bravado register the tonal anchor forbids, and it reads as a competence test.

- Text labels in `ink-cream` (selected) / `ink-dim` (unselected). Cream is the only text voice (`gameplay-ui-direction.md` §7) — no new font color.
- **Optional warm glyph left of each label** (language-light reinforcement, kid-readable, Devon's call): Easy = a small leaf/sprout `🌱`-read (the world gives); Medium = a small flame (the warmth need, the baseline survival loop); Hard = a small mountain/peak (the far-horizon climb). Glyphs are *geometry-simple* faceted icons in the world's own colors (leaf-green / ember-gold / rock-grey) — NOT emoji, NOT skulls. If glyphs add fuss, text alone is fine — labels carry it.
- **No numbers, no percentages, no stars.** The control says a *feeling*, not a stat. (Same discipline as the need-meters: no number shouting — `u2-5` §3.)

### A4. Default tier — **Medium**

Default to **Medium**, and pre-select it on first run. Rationale:
- Medium is the **designed-baseline** — the need-decay rates and snake presets that the team tunes against during normal soaks ARE the medium values (the F-key-tuned snake values become the medium preset; easy/hard scale off it — see Part B). Shipping medium-default means the Sponsor's soak sees the intended baseline feel unless he opts into the extremes.
- It's the honest middle for a game serving both audiences — neither pre-judges the player as a kid nor as a veteran.
- The difficulty memory frames the tiers as scaling *around* a baseline; medium IS that baseline. (If the Sponsor later wants easy-default to be welcoming for the kid-first framing, that's a one-line default flip baked from soak — flag it as a soak question, don't pre-decide.)

### A5. How switching a tier reads to the player

**The switch must feel like the world breathing in or out — calm, immediate, never a jarring reset.** This is the load-bearing feel call: the difficulty knob silently re-tunes many systems, and the player should *sense* that without a modal "Difficulty changed!" announcement.

- **Immediate + live (no restart).** Per the registry's AC2, flipping the segment re-applies the tier's preset values to the live systems on the spot (need-decay multipliers, snake aggro/speed/cooldown/lunge presets — Part B). The Sponsor soak-flips easy↔hard and watches the snake get bolder / the needs drain faster *behind the dimmed panel*, in real time. No reload, no confirm dialog.
- **No punitive confirmation.** Do NOT pop "Are you sure? Hard is challenging!" — that's the competence-test register the anchor forbids. The choice is reversible at any time from the same drawer; trust the player.
- **Quiet acknowledgement, in the world's voice (optional, kid-friendly nicety):** on switch, a *single* brief warm pulse on the relevant HUD elements as they re-tune — e.g. the need-meters give one soft ember-gold shimmer as their decay re-rates (the same shimmer language as a need ticking — `need-meter-ui-direction.md`). This is the diegetic "the world just changed gear" beat: felt, not announced. If it reads as noise in soak, cut it — text-free silence is the safe default.
- **Persistence:** the chosen tier persists across runs (PlayerPrefs / settings asset, registry AC5) — the Sponsor's kid picks easy once and it stays. Silent persistence, no "saved" toast (`gameplay-ui-direction.md` §2.3).

### A6. The tier-preset contract (what the selector drives — the cross-system shape)

The selector itself is *one enum*. The work is that **every tunable system reads its values from a `DifficultyTier`-keyed preset table**, not from hard-coded constants. This is the memory's "design every system with tiers in mind from the start" made concrete. The selector flips one enum; each system's preset block supplies the three columns:

| System | Easy (gentle / kid) | Medium (baseline) | Hard (adult squeeze) | Source |
|---|---|---|---|---|
| Need-decay rate (warmth/hunger/thirst) | slow drain — long forgiving ramps | baseline drain | fast drain — real pressure | `need-meter-ui-direction.md` (the decay each meter encodes) |
| Snake aggro radius | small (easy to avoid) | baseline | large (territorial, hard to skirt) | Part B / F-key presets |
| Snake move + lunge speed | slow, very readable telegraph | baseline | fast, shorter telegraph | Part B |
| Snake lunge cooldown | long (rare strikes) | baseline | short (relentless) | Part B |
| (future) combat/HP, hazard intensity | gentle | baseline | punishing | difficulty memory |

**Devon's implementation note:** the cleanest shape is a `DifficultyTier { Easy, Medium, Hard }` enum + a small `ScriptableObject` preset asset per tunable system (or one combined `DifficultyPresets` SO with a row per system), so the F-key-tuned snake values (and the Sponsor's soak-baked need-decay values) drop straight into the medium column and the easy/hard columns scale off them. The selector sets a single global `CurrentDifficulty`; systems subscribe and re-read on change. This keeps tiers first-class (the memory's whole point) instead of a retrofit. (Architecture is Devon's call; this names the SHAPE the UX assumes.)

---

## PART B — SNAKE POC feel / menace UX

*Direction Devon/Drew implement when the snake build is un-held (`86caaz4vn` design-locked: territorial ambusher; proximity-radius + lunge; mid-size, readable, lightly-menacing chunky low-poly; F-key-tuned aggro/speed/cooldown/lunge; 3 difficulty presets; avoid-only; build HELD).*

### B0. Tonal anchor (read this first)

**The snake is the world's first "careful, now" — a wary territorial animal, not a monster.** The Sponsor's locked frame is *lightly-menacing, not horror*, and *avoid-only* (the player skirts its patch, it doesn't hunt across the island). So the feeling target is **the moment a real snake on a sunlit trail freezes, coils, and watches you** — your pulse ticks up, you give it space, you move on. Tense for a second, not terrifying. It must read as a **sibling of the chunky toy castaway** (`21h00_32`) living in the same warm blob-nature world (`21h10_44`) — a *carved wooden snake*, big readable facets, a little bit cross about you being here. If a beat tips it toward horror — sudden screech, blood, a gaping fanged maw, a jump-scare from off-screen — it's wrong even if it's "scarier," and especially wrong for the kid audience on easy. **Lightly-menacing toy-world over horror.** Warm sunlit frame keeps the frame; the snake is a small sharp note inside it.

### B1. Does the snake telegraph its lunge? — **YES, always. The telegraph is the whole point.**

**This is the single most important feel call in the POC.** An avoid-only enemy that strikes WITHOUT warning is just an unfair damage-on-contact trap — frustrating, not tense, and impossible for a kid to learn. An avoid-only enemy that *clearly signals* before it strikes is a fair, readable, teachable challenge: **the player is always given the information to avoid the hit.** Menace comes from the *building threat you can see*, not from the surprise. So the snake telegraphs in three escalating, always-readable stages:

**Stage 1 — IDLE / patrol (player outside aggro radius):** the snake is a calm carved animal — a slow, gentle idle sway of the head, slow ambient body breathing, maybe an occasional tongue-flick. Reads as "an animal minding its own patch." Non-threatening; a kid can walk the world and just *notice* snakes exist. (This is most of the snake's screen-time — it should look like wildlife, not a trap, 90% of the time.)

**Stage 2 — ALERTED / coiling (player enters aggro radius):** the readable "careful" beat. The snake **turns to face the player, rears its head up, and draws into a tighter coil** — a clear posture shift from "lounging" to "watching you." This is a *held, sustained* pose (not a flash) so the player has time to read it and back out. Paired with the alert audio cue (B3). **This stage is the avoid-window** — if the player leaves the radius here, the snake never strikes and relaxes back to idle. The whole avoid-only loop lives in this stage being long and legible enough to obey.

**Stage 3 — WIND-UP → LUNGE (player too close / lingered too long):** a distinct **pull-back wind-up** — the head draws back and the coil compresses for a clear beat (the "drawing the bow" moment — the universal animal-strike tell), THEN the lunge: a fast forward snap toward the player, then an immediate **recoil back to its coil** and the lunge cooldown. The wind-up beat is the final fair-warning: even in stage 3 the player gets a readable fraction-of-a-second to dodge back. Contact during the lunge is the hit (avoid-only = no HP system yet; "attack on contact" = the lunge connects — per the locked design). After the lunge the snake recoils and re-enters its cooldown; it does NOT chase (territorial, avoid-only).

**The telegraph scales with difficulty (B2) — but it NEVER disappears.** Even on hard the wind-up exists; it's just faster. Removing the telegraph entirely would convert a fair challenge into an unfair trap, which breaks the avoid-only contract on every tier.

### B2. How menace scales kid → adult across the 3 tiers

Menace scales by **how much avoid-window the snake gives you** — the telegraph timing and the aggro reach — NOT by making it gorier or louder. Same carved snake, same warm world, same telegraph stages on every tier; what changes is the *reaction time the player gets* and *how much territory the snake claims*. This maps directly onto the F-key-tuned params (aggro / speed / cooldown / lunge) → the three difficulty presets (Part A6):

| Param (F-key-tuned → preset) | **Easy** (kid / gentle) | **Medium** (baseline) | **Hard** (adult squeeze) | Feel |
|---|---|---|---|---|
| **Aggro radius** | small — easy to see and skirt the snake's patch | baseline | large — the snake claims more ground; you must route around | how much territory you must respect |
| **Move / turn speed** | slow — it tracks you lazily | baseline | fast — it faces you quickly, harder to slip behind | how nimbly it covers you |
| **Lunge wind-up (the telegraph length)** | LONG, exaggerated — a kid has ample time to back out | baseline | SHORT but PRESENT — an adult must react sharply | the fair-warning window (never zero) |
| **Lunge speed / reach** | slow, short reach — easy to dodge | baseline | fast, longer reach — commit to your dodge | how hard the strike is to evade |
| **Lunge cooldown** | long — rare strikes, lots of recovery | baseline | short — relentless, re-threatens fast | how often you're under threat |

**Easy = "a snake you watch and walk around"** (a kid notices it, gives it space, never gets surprised — the telegraph is huge). **Hard = "a snake that genuinely makes you commit your dodge"** (an adult must read the short wind-up and react, and the bigger radius forces real route-planning). **The horror line is never crossed on any tier** — hard is *demanding*, not *frightening*; no extra gore/screech is unlocked at hard. Scaling lives entirely in timing + reach + cadence, which is exactly what the F-key tuner exposes — so the Sponsor live-tunes the three columns by feel, then bakes them as the presets (the give-him-the-knob workflow, memory `sponsor-prefers-direct-tweak-tools-for-fiddly-placement`).

### B3. Audio-cue direction — hiss / strike, warm-tense not horror

> **Caveat:** this is direction for *when audio is in scope*. Far Horizon has **no audio bus / SFX system shipped yet** (the gameplay UI + HUD are silent so far). This section specs the cues + the menace tone so Devon can wire them whenever the audio system lands; it is NOT a request to source clips into this docs PR. No audio files are authored here.

Audio is HALF the menace and carries the telegraph when the snake is off-center in the player's view (the player may hear the alert before they fully see the coil). Three cues, mapped to the three telegraph stages, all in the **warm-tense, not horror** register:

| Cue | Trigger (telegraph stage) | Tone direction | Menace / horror discipline |
|---|---|---|---|
| **Hiss — alert** | Stage 2 (player enters aggro radius) | a soft, dry, *rising* hiss — the "I see you, careful" warning. Spatialized (3D, from the snake) so the player can *locate* the threat by ear. Short, then it can sustain a low sibilance while alerted. | a real-animal hiss, NOT a monster snarl or a synth screech. Dry and breathy, low-mid, no sub-bass dread layer. This is the friendliest-possible "back off" — a kid should find it *exciting*, not nightmare-fuel. |
| **Wind-up tick** | Stage 3 (lunge wind-up begins) | a tiny tightening sound — a quick rising pitch or a soft "coil-creak" — the audio twin of the pull-back pose. ~the wind-up's length. | this is the *fair-warning by ear* — it must be audible and distinct so a player not looking directly at the snake still gets the strike warning. Subtle, sharp, brief; not a stinger. |
| **Strike — lunge** | Stage 3 (the lunge snap) | a quick dry *snap / whip* — the lunge committing. Crisp, percussive, over fast. | NO meaty/gory impact, NO scream. A clean whip-snap reads "fast animal," not "gore." On contact: a soft non-violent "ow" feedback beat (see below) — kid-safe. |

**Contact feedback (no HP system yet, avoid-only):** when the lunge connects, the *player* gets a gentle, non-gory feedback beat — direction: a soft warm "oof" / a brief screen-edge warm-coal pulse (reuse the HUD coal-red `#B5563C` as a soft vignette flash, NOT a red blood overlay — `u2-5` discipline: dying-ember red, never alarm red). It says "that got you, mind the snake" in the world's warm voice. (What contact *costs* mechanically is the design team's call when an HP/consequence system lands — OOS here; this specs only the FEEL of being caught.)

**Audio menace scaling across tiers:** keep the *same* clips on all tiers — do NOT make the hiss scarier on hard. Scaling is the *cadence* the telegraph timing already drives (on hard the wind-up tick is shorter/sharper because the wind-up is shorter; on easy the hiss has a longer lead because the avoid-window is longer). Same warm sound-set, different rhythm. This keeps easy kid-safe and hard demanding with one cue library.

**Bus / mix direction (for when the audio system exists):** the snake cues sit on an **SFX/world-creature bus**, ducked under nothing critical but clearly audible over ambient; spatialized 3D (distance-attenuated, so a far snake is a faint hiss = "something's over there," a near snake is a clear warning). No music sting on alert — the *world's* tension does the work, not a score cue. Keep the snake's whole sound-world *dry and natural* (it's a real animal in a warm world), reserving any processed/dread audio for genuinely-scary future enemies if the game ever wants them (OOS).

### B4. Visual style of the snake — sibling of the toy world (chunky low-poly)

Per the locked design (*mid-size, readable, lightly-menacing chunky low-poly*) and the shared grammar (`style-guide-v2.md` §1):

- **Form:** a **mid-size carved snake** — chunky faceted body segments (a few big planes per segment, legible facets), bold readable silhouette at orbit distance, a clearly-readable head distinct from the body. Reads as the same carved-toy material as the castaway (`21h00_32`) and the blob-nature props (`21h10_44`). NOT a thin spindly snake (thin geometry reads poorly at orbit distance AND triggers the thin-foliage normal bug — `style-guide-v2.md` §1.2). Chunky over delicate.
- **Palette (sub-1.0, HDR-safe, warm-world-native):** a **warm earthy snake** that belongs on the sunlit trail, not a cold or toxic-neon serpent. Direction anchors (eye-dropper baseline for Tess, tune in-build):
  - Body base — a warm olive/ochre-green `~#6E7A3A` (≈0.43, 0.48, 0.23) or a warm sandy-brown `~#9A6B40` (≈0.60, 0.42, 0.25) — sits naturally against the world's leaf-greens and warm soil. (Two viable directions; Sponsor soak picks — both are warm and on-world.)
  - Top-lit facets a half-step brighter; shadow facets a half-step deeper (the faceted read).
  - **A single warm-but-sharper accent** for menace — a muted dark-coal pattern (banding/diamonds, `~#3A2E26`) along the back, and a slightly-brighter belly. The pattern is the "this animal is a little dangerous" read — chunky painted facets, not fine scales.
  - **Eyes:** small, dark, alert (the same big-dark-eye language as the castaway, scaled down and narrowed — an alert animal eye, not a cute round one and NOT a glowing-red horror eye). One small sharp highlight = "watching you."
  - **NO neon, NO toxic-green glow, NO red glowing eyes, NO blood/fangs-bared maw.** Every channel sub-1.0; menace from posture + pattern + the sharp eye, never from horror-coloring. (HDR/sRGB-clamp discipline — `style-guide-v2.md` §6.)
- **The edge-bevel grammar** (the board's signature, `style-guide-v2.md` §1.3) is OPTIONAL on the snake — it's an animal, not a tool/blade. A subtle top-facet highlight along the spine is enough; do NOT force a near-white tool-bevel onto it (that would read as "metal snake"). The snake's identity detail is the *back-pattern + alert eye*, not a blade-bevel.

### B5. What the snake POC does NOT do (carry the locked OOS)

Avoid-only — the snake does NOT chase across the island, does NOT path-find to hunt the player (it defends a patch). No HP/death system, no combat-back (the player can't fight it yet — avoid-only). No enemy roster / other animals (snake-only POC — the ticket OOS). No horror beats (gore, jump-scares, dread-score) on any tier. No audio files sourced in this docs PR (B3 is direction for when the audio system lands). Mechanical consequence-of-contact (what a hit costs) is the design team's call when an HP/consequence system is scoped — this spec covers FEEL only.

---

## Cross-references

- **Difficulty:** memory `difficulty-settings-easy-medium-hard` (Sponsor 2026-06-19 — easy/med/hard, design every system with tiers) · settings panel `86caa4bqp` / PR #83 (the registry the selector rides) · `gameplay-ui-direction.md` §2.2 (row archetypes A/B/C — Part A2 adds D) + §1 (the warm UI palette) + §7 (gold = active/yours).
- **Snake:** `86caaz4vn` (design-locked POC — territorial ambusher / proximity-radius + lunge / F-key-tuned / 3 presets / avoid-only / build HELD).
- **Tone gates:** `need-meter-ui-direction.md` §0 (warm-toy-over-HUD) · `style-guide-v2.md` §0–§1 (chunky carved material — the snake siblings the castaway) + §6 (sub-1.0 palette anchors) · `u2-5-survival-hud-spec.md` §3 (coal-red `#B5563C` = dying-ember, never alarm — reused for the contact vignette).
- **Workflow:** memory `sponsor-prefers-direct-tweak-tools-for-fiddly-placement` (the F-key live-tune → bake-as-preset workflow that fills the medium column).
- **Inspiration (ground truth, looked at them):** `21h00_32` (castaway — the snake's sibling material), `21h10_44`/`21h11_03` (blob-nature world the snake lives in), `21h13_31`/`21h22_33` (warm sunlit frame the menace must not turn to horror).
```
