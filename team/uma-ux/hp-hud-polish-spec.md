# HP HUD Polish + Heal-Source Surfacing — Implementable Spec

**Ticket:** `86cah7z2q` (HP HUD polish + heal sources beyond needs-gated regen). **Owner:** Uma (HUD visual) → Drew (heal-source wiring) · **Reviewer:** Devon (per ticket) · **Spec reviewer:** Priya.
**Work-type:** spec (design-only; no code in this PR). **Status:** SPEC — the doc `86cah7z2q` implements. Sections are citable from the ticket's ACs.
**Depends on:** the combat POC `86cah7xxp` (COMPLETE — `Health` / `HealthRegen` / `DeathHandler` shipped; the HP bar already draws). This ticket POLISHES a shipped surface; it does not introduce HP.

> **What this doc is for (read first):** an HP bar **already ships** in `Assets/Scripts/Runtime/SurvivalHud.cs` — the POC deliberately reused the need-bar widget for a "quick-and-legible" readout (`86cah7xxp` AC9) and explicitly deferred polish to here. So this spec does two things: (1) it gives HP a **hierarchy and a feedback language of its own** so the player never has to parse four identical bars, and (2) it specs **how a heal announces its SOURCE** (regen / heal item / rest-at-campfire) plus the **enemy-HP read** and the **per-tier** behavior. It is tight against the shipped code as ground truth — every geometry number below is either quoted from the shipped draw or an explicit, arithmetic-checked delta from it.

**Builds on (do NOT duplicate):** [`combat-cluster-design-brief.md`](combat-cluster-design-brief.md) §3.3 (the lighter first pass this deepens) + §1.2 (impact-juice caps) + §4 (primitive discipline) · [`hud-three-bar-spec.md`](hud-three-bar-spec.md) (the bar grammar, plate, FLOOR rule, critical glyph-pulse, satisfaction beat) · [`u2-5-survival-hud-spec.md`](u2-5-survival-hud-spec.md) (the parent ember-band glow-bar spec) · [`style-guide-v2.md`](style-guide-v2.md) §6 (sub-1.0 palette anchors + the HUD-plate-over-saturated-green watch item) · [`gameplay-ui-direction.md`](gameplay-ui-direction.md) (gold = active/yours; cream = the only text voice) · [`difficulty-snake-poc-ux.md`](difficulty-snake-poc-ux.md) §A6 (the tier-preset contract) + §B3 (the warm-coal contact-feedback precedent).
**Sibling spec (same wave):** [`status-effect-readability-spec.md`](status-effect-readability-spec.md) — status chips dock onto this HUD (§5 there = §3.4 here; the DoT-flash debounce in §2.4 below is a shared requirement).
**Board:** looked at `inspiration/2026-06-12_21h00_32.png` (chunky castaway — generous head-space above the head, which is what makes the above-head enemy read in §6 viable) and `21h16_13` (the saturated-green world the HUD plates must hold against — the §2.4/§6 value-contrast rule comes from it).

---

## 0. Tonal anchor (read this first)

> **HP is the castaway's BODY, and it is a different kind of thing than a need. Warmth, hunger and thirst are a fire burning down — slow, floored, forgiving. HP is a flinch: it drops in one bite and it can end the run. So the HP read must be the one element in the corner that says "this one is about you staying alive" — WITHOUT ever raising its voice. When you get hurt, the HUD should feel like a wince; when something mends you, it should feel like relief. Never an alarm, never a power-up.**

The gate, unchanged from the shipped bars: **if a beat makes the HUD louder, slicker, or more "AAA stat-bar", it's wrong even if it's clear.** Three quiet need-glows plus one vital read, in the calmest corner; the world keeps the frame. Every channel sub-1.0. No pure white, no `#FF0000`, no neon, no flashing, no numbers.

**The load-bearing call of this spec:** HP is distinguished from the needs by **FORM, not by shouting** — a taller bar with FIVE chunky segments against the needs' TEN fine ones, separated by a breath of space. Shape reads faster than color at peripheral glance, and it survives a colour-blind player and a saturated-green background. Everything else here (damage wince, heal relief, low-HP quickening) is amplitude tuning inside the `game-juice.md` caps.

---

## 1. Ground truth — the shipped HP bar (quoted, not guessed)

Read straight from `Assets/Scripts/Runtime/SurvivalHud.cs`. **These are the values the polish deltas below are measured against.**

| Shipped fact | Value |
|---|---|
| Draw path | IMGUI `OnGUI`, explicit `Rect`s only (no `GUILayout.*`), `GUI.DrawTexture(rect, Texture2D.whiteTexture)` per segment |
| HP row anchor | `y = Screen.height - 152f`, `x = 16f`, box `260 × 28` (the need-bar geometry, reused verbatim) |
| Need rows | warmth `-44`, hunger `-80`, thirst `-116` (36 px pitch); inventory ledger `-188` |
| Segments | `SurvivalHud.SegmentCount = 10`; `glyphW = 22f`, `gap = 3f`, `segY = y + 4f`, `segH = h - 8f` |
| Plate | `DrawPlate(x-6, y-6, w+12, h+12)`, `PlateAlpha = 0.55f` |
| Glyph | `♥` in `_heartStyle` (`fontSize 18`, Bold), drawn in the band colour, `Rect(x, y+3, 18, 22)`; alpha `filled > 0 ? 1f : 0.4f` |
| HP band colours | `VitalRed #CC474D` (0.80, 0.28, 0.30) ≥0.60 · `WoundOrange #C76B52` (0.78, 0.42, 0.32) 0.30–0.60 · `DarkBlood #8C3338` (0.55, 0.20, 0.22) <0.30 · emptied = shared `Charcoal #2E2A2B` |
| Critical | `HpCriticalThreshold01 = 0.25f` — HUD-derived (`Health` has no `IsCritical`); glyph slow-breathe, `CriticalPulsePeriod = 1.0f`, `CriticalPulseMinAlpha = 0.55f`, shared phase clock |
| Bind | `health.Changed += OnHealthChanged` in `Awake` after the `FindObjectOfType` fallback — **never poll** |
| `Health` read surface | `Current` / `Max` / `Current01` / `IsDead` / `event Action<float> Changed` / `event Action Died`; mutations `ApplyDamage(amount, DamageType)` (returns HP actually removed), `Heal(amount)`, `RestoreFull()` |
| Per-tier HP | `easyMax 120` / `medMax 100` / `hardMax 80`; `easyDamageTakenMul 0.6` / `med 1.0` / `hard 1.35` (`Health.ApplyDifficulty`) |
| Regen | `HealthRegen.regenPerSecond = 2f`, `needThreshold01 = 0.4f`, `criticalSlowDrains = false` (STALL is the default policy) |
| Death | `DeathHandler` (tier-selected): easy faint-in-place, medium campfire-respawn + inventory kept, hard campfire-respawn + inventory dropped; `NavMeshAgent.Warp`; read surface `DeathCount` / `LastFaintedInPlace` / `LastRespawnPosition` / `LastDroppedInventory` |

**⚠ Verified constraint — THERE IS NO AUDIO SYSTEM.** `Assets/` contains no `.ogg` / `.wav` / `.mp3`, and no script references `AudioSource` or `PlayOneShot` (checked 2026-07-27). Every audio line in this spec is **direction for when an audio bus lands — NOT authorable in this ticket.** Marked `<deferred — no audio bus>` inline. This supersedes the "soft ascending chime" phrasing in `combat-cluster-design-brief.md` §3.3, which read as shippable; it is not.

---

## 2. HP hierarchy + the damage wince (the HUD half of the ticket)

### 2.1 Placement — stay in the left column, earn hierarchy through FORM

HP **keeps** the shipped left-anchored column and stays at the **top of the stack**. Do not move warmth: the "warmth is the bottom one" muscle memory is protected by `hud-three-bar-spec.md` §3.1 and a reorder would cost more than it buys. Instead HP separates itself three ways:

1. **A breath of space.** HP's baseline moves from `-152` to **`y = Screen.height - 162f`** (a 46 px gap above thirst's `-116` vs the needs' uniform 36 px pitch). Ten pixels of nothing is enough for the eye to group "the three" and read "the one" as separate — the same trick the settings panel uses with its heavier divider above the meta-row (`difficulty-snake-poc-ux.md` §A1).
2. **A taller bar.** Box **`260 × 34`** (needs stay `260 × 28`); `segY = y + 5f`, `segH = h - 10f` (= 24). Same `x = 16`, same `w = 260` — the left edge and the segment-run start x stay common with the needs, so the column still reads as one aligned family.
3. **FIVE chunky segments, not ten.** `HpSegmentCount = 5` (each segment = 20% HP). This is the load-bearing distinction: at peripheral glance a 5-block bar and a 10-block bar are *instantly* different objects, and the chunkier blocks read as "hearts" — the universal kid-legible vitality grammar. Keep the shipped **FLOOR** rule (`Mathf.FloorToInt(current01 * 5)`, clamped 0..5) so a 3.4/5 HP still reads 3/5 and never over-reports. **HP does NOT take the needs' `TopSegmentThreshold = 0.95f` near-full exception** — that exception exists because needs decay continuously (`SurvivalHud` §TopSegmentThreshold comment); HP does not decay, so full HP is exactly `1.0` and lights 5/5 honestly.

**Layout arithmetic (checked, so nothing overlaps):** HP occupies 162→128 px above the bottom; its plate 168→122. The inventory ledger's plate currently tops out at 191 and bottoms at 173 (`y = Screen.height - 188`, `DrawPlate(x-6, y-3, w+12, h+6)`), which would collide once HP grows. **The ledger moves UP to `y = Screen.height - 204f`** (plate 207→189) — 21 px clear above HP's plate. Same cream, same plate, same absent-when-empty. (If the belt/inventory UI has superseded the ledger by implementation time, the row simply isn't drawn; the HP row does not depend on it. Verify at implementation — do not assume.)
**Safe area:** the top of the stack (ledger at `-204`) must stay ≥16 px clear of the `BootHud` plates. The BUILD stamp is load-bearing for every soak — **an implementation that covers the stamp is a hard fail**, not a NIT.

```
+--------------------------------------------------------------+
| [Far Horizon]                              BUILD <tag|utc|sha>|   <- BootHud (untouched, uncovered)
|                                                              |
|                     ( the world — the star )                 |
|                                                              |
|   axe 1   wood 3                                             |   <- ledger        -204 (moves up)
|   ♥  ▰▰▰▰▰  ▰▰▰▰▰  ▰▰▰▰▰  ▱▱▱▱▱  ▱▱▱▱▱   [chips]              |   <- HP  (5 seg, h34) -162  ← the vital
|                                                              |   <- 10px breath
|   ◆ thirst   ▰▰▰▰▰▰▰▱▱▱                                      |   <- thirst        -116
|   ● hunger   ▰▰▰▰▰▱▱▱▱▱                                      |   <- hunger         -80
|   ▲ warmth   ▰▰▰▰▰▰▰▱▱▱                                      |   <- warmth         -44
+--------------------------------------------------------------+
```

**⚠ Sponsor-judges-at-soak:** the 5-vs-10 segment split and the taller HP bar CHANGE shipped geometry. This is the one call in this spec that a soak could send back to "keep it identical to the needs". Both are constants — a revert is a one-line flip of `HpSegmentCount` back to `SegmentCount` and `h` back to 28. Predicted upside: "which bar is my health" becomes a zero-thought read. See §8 Q1.

### 2.2 Colour — keep the shipped band ramp exactly

`VitalRed → WoundOrange → DarkBlood` ships and is right: a warm heart-red that *darkens and cools toward dried blood* as HP drops, rather than brightening toward alarm. **Do not retune the three hexes** in this ticket. They are the one red in the HUD cluster (the needs are gold / green / blue, and the coal-red / berry-red critical bands are deliberately muted dying-ember tones — `style-guide-v2.md` §6). HP's red is the *only* saturated red allowed in the corner, and that exclusivity is what makes it read as "vital" without a label.

Reconciliation note: `weapon-tool-style-spec.md`'s red lashing was REMOVED at the Sponsor's direction (`combat-cluster-design-brief.md` §3.1). That was **prop** colour ("no arbitrary colours on a material", quality-bar #3). HUD semantics are a different domain — a red vitality read is a universal convention, not an arbitrary material tint. No conflict; called out here so nobody "fixes" it.

### 2.3 Damage wince — three layers, all inside the calm caps

Fired on `Health.Changed` when `Current01` **decreased** (the HUD holds the previous value and diffs; `Changed` already carries the new `Current01`). `dmgFrac` = the fraction of `Max` lost by this event — every amplitude below scales with it, so a 1 HP nick is nearly invisible and a boar gore (18 HP on medium of 100 = 0.18) is a clear wince.

| Layer | Spec | Cap (HARD) |
|---|---|---|
| **A — Lost-segment flash** | The segments that just went out flash warm-cream `#EAD9B8` (0.92, 0.85, 0.72 — the shipped `Cream`, already in the file) at α `0.55 + 0.45·dmgFrac`, held ~0.10 s, then eased to `Charcoal` over ~0.25 s (ease-out). This is the "which blocks did I just lose" read — far more legible than a bar-wide wash. | flash α ≤ 1.0 on a sub-1.0 colour; total ≤ 0.35 s |
| **B — Row nudge** | The HP row (plate + glyph + segments) translates **left** by `2f + 2f·dmgFrac` px and eases back over ~0.15 s (ease-out, `Time.unscaledDeltaTime`). A wince, not a shake. | ≤ 4 px, ≤ 0.2 s, translate ONLY — no rotation, no scale, no oscillation (one out-and-back, never a wobble) |
| **C — Screen-edge coal pulse** | A soft IMGUI edge vignette in the HUD's dying-ember coal-red `#B5563C` — the precedent already set for non-gory contact feedback (`difficulty-snake-poc-ux.md` §B3). Peak α = `0.10 + 0.20·dmgFrac`, ease-out over ~0.35 s, then **fully gone**. | peak α ≤ 0.30; duration ≤ 0.35 s; **TRANSIENT ONLY — a sustained low-HP vignette is forbidden** (§2.5) |

**Primitive discipline for layer C (cite when implementing):** the vignette is **IMGUI-drawn** — four `GUI.DrawTexture` edge strips (or one 9-slice-ish frame) on `Texture2D.whiteTexture` with `GUI.color` alpha, at the very top of `OnGUI` so it sits under the bars. **NOT a post-process Volume pulse** and **NOT chromatic aberration / lens distortion** — both are ruled out by `game-juice.md` §2 (tone + a Render-Graph pass cost), and `combat-cluster-design-brief.md` §4 already pins full-screen overlays to the UI layer. Pure IMGUI also never strips to magenta in the built exe, which is why the whole HUD is IMGUI.

**No hit-stop on INCOMING damage. Default: none, any tier.** Hit-stop (`game-juice.md` §1.2, 2–3 frames) belongs to the player's *own* strike landing — it is a reward punctuation. Freezing time when the player gets *hurt* reads as trauma and inverts the tone. If a soak asks for one, the absolute ceiling is **2 frames on the HARD tier only**; easy and medium stay at zero. **No camera Impulse on incoming damage either** (Impulse is reserved for the strike, `combat-cluster-design-brief.md` §1.2).

**No numbers.** No floating damage numerals, no `-18`, no percent. Numbers are for the dev console, not the calm HUD (the same discipline the need bars ship with).

### 2.4 DoT debounce — the cross-spec requirement (do not skip)

Bleed and poison tick **through `Health.ApplyDamage`** (`StatusEffectController.TickSeconds`), which fires `Changed` on every tick. A per-`Changed` wince would therefore **strobe** the vignette and the row-nudge several times a second while a DoT runs — the single worst tonal failure available on this surface.

**Requirement:** the damage wince is **debounced in the HUD** — minimum **0.35 s** between wince triggers, and amplitudes already scale with `dmgFrac` so a sub-1 %-of-max tick produces a visually null wince. No framework change is needed for this (a source tag on `ApplyDamage` would be a bigger, riskier change than a HUD debounce — deliberately NOT proposed). The *segment* flash (layer A) is exempt from the debounce **only** when a segment boundary is actually crossed, which a slow DoT does rarely.

**Success-test (name it in the ticket):** drive a bleed/poison DoT for 3 s in EditMode; assert the wince trigger count ≤ `ceil(3 / 0.35)` and that a single 18 HP gore produces exactly one trigger.

### 2.5 Low-HP warning — a quickening pulse, in the HUD only

At `Current01 <= HpCriticalThreshold01` (shipped `0.25f`):
- **Shipped, keep:** the `♥` glyph slow-breathes (α 0.55↔1.0, `CriticalPulsePeriod = 1.0f`, shared phase clock with any critical need so the corner pulses as one calm body).
- **Polish adds — the quickening.** The HP glyph's pulse period shortens as HP falls through the critical band: `period = Mathf.Lerp(0.75f, 1.0f, Current01 / 0.25f)`. A faltering heartbeat that speeds up slightly is diegetic, wordless and kid-legible. **HARD FLOOR: never below 0.70 s** — faster than that stops reading as a heartbeat and starts reading as a blinking alarm light. HP's glyph is allowed to leave the shared phase clock while quickening (it is no longer at the shared period); the three needs keep theirs.
- **Polish adds — a faltering ember.** The rightmost FILLED HP segment breathes ±8 % alpha on the glyph's phase (extends the shipped warmth ember-flicker technique, `emberFlicker` arg, to HP). One segment only.
- **FORBIDDEN:** a sustained red screen vignette, a screen-edge alarm, a heartbeat SFX loop (`<deferred — no audio bus>`, and even then: no), a desaturation post-process, a "LOW HEALTH" text card. `hud-three-bar-spec.md` §4's no-red-vignette rule was written for need criticality and deferred the fail-state surface to a later ticket — **this is that ticket, and the ruling is: the warning stays inside the HUD.** Only the transient damage pulse (§2.3 C) ever touches the screen edge.

### 2.6 Death + revival read — wordless, ≤1.5 s, no death card

`DeathHandler` already does the mechanics. The HUD's job is the beat.

- **All tiers:** HP empties to 5 charcoal segments; the `♥` dims to α 0.4 and **stops** pulsing (a stopped pulse is the death read — no new element). The plates deepen from α 0.55 → 0.70 over ~0.4 s and hold ~0.6 s: the HUD itself "goes quiet". Then the revival.
- **Easy (faint in place):** HP refills 0→5 over ~1.2 s, eased (the §4 heal grammar at its largest) while the plates return to 0.55. Reads "you came round". `LastFaintedInPlace` is the flag to key it off.
- **Medium / hard (campfire respawn):** the same quiet-then-refill beat; the camera arriving somewhere else does the "where am I" work. **Hard additionally:** the inventory is dropped — the read is the **ledger/belt simply emptying** (no new UI), and the drop site is recoverable through the existing `LootPrompt` pickup path when the player walks back. Reuse, no invention.
- **FORBIDDEN at every tier:** a "YOU DIED" card, a letterbox, a red flash-out, a slow-motion death, a score/deaths counter on screen. `DeathCount` stays a dev-console/test read.

**⚠ Sponsor-judges-at-soak:** whether the wordless beat is enough, or whether he wants **one** cream line in the `LootPrompt` pill idiom (e.g. "You wake by the fire", ≤2 s, cream ink, above-head anchor — the existing code path, no new UI system). Spec'd as OFF by default; a one-flag flip. See §8 Q3.

---

## 3. What HP does NOT borrow from the needs

Stated explicitly because "reuse the need widget" is the shipped starting point and over-reuse is the failure mode:

| Need behaviour | HP? | Why |
|---|---|---|
| 10 segments | **No** — 5 | Form is the hierarchy (§2.1) |
| `TopSegmentThreshold = 0.95f` near-full exception | **No** | HP doesn't decay; 1.0 is reachable and honest |
| Continuous decay | **No** | `Health` has no `Update`; regen is driven by `HealthRegen` |
| Ember flicker on the rightmost segment | **Only when critical** | Warmth flickers because it IS fire; HP flickers only as a faltering pulse |
| `IsCritical` from the model | **No** — HUD-derived at `0.25` | `Health` is not a `SurvivalNeed` (locked decision 1) |
| Shared charcoal for emptied segments | **Yes** | The spent colour is one charcoal across the whole cluster — keep it |
| Low-alpha dark plate at α 0.55 | **Yes** | One plate family (`BootHud` lineage) |
| No numbers / no toast | **Yes** | Non-negotiable |

---

## 4. Heal sources — the source identifies itself by AMPLITUDE and by TINT

The ticket's real design question is *"what tells the player that berries / the campfire / regen healed them?"* The answer is **one shared gain-grammar at three amplitudes, with the tint borrowed from the source's own colour** — the same logic that made thirst water-blue ("the need's colour IS the source's colour", `hud-three-bar-spec.md` §2.1). No text, no icons, no toast.

**Shared gain-grammar (the floor, all three sources):** newly-filled segments **fade in over ~250 ms, ease-out** (`hud-three-bar-spec.md` §4.1's satisfaction beat, already the house language). Never an instant pop.

| Source | Amplitude | Tint borrowed from | Extra layer | Cadence sanity |
|---|---|---|---|---|
| **Needs-gated regen** (shipped `HealthRegen`, 2 HP/s) | **Quietest** | none — stays `VitalRed` | none. No sweep, no particle, no sound. | At 2 HP/s on `medMax 100`, a 20 %-segment boundary is crossed every ~10 s. Announcing ambient recovery more often than that turns healing into noise. |
| **Heal item** (consume seam, berry-like — Drew's wiring) | **Mid** | a warm-cream `#EAD9B8` sweep left→right along the bar, ~0.3 s, α ≤ 0.25 | a pooled faceted puff at the player, **≤ 8 particles**, warm-cream / soft-green, `Unlit/Particle` material, `ObjectPool<T>` + `OnParticleSystemStopped` (the berry-pop precedent) | One discrete beat per consume — it's an active choice, it may announce itself. |
| **Rest at campfire** (campfire interaction reuse) | **Warmest** | the HP filled-run lerps **one half-step toward the campfire's ember-gold `#E8B25C`** while rest-heal is active, then eases back to `VitalRed` over ~0.5 s when it stops | optional: the `♥` glyph borrows the same warm shift. No particle (the fire already has its own). | Continuous while resting — so it must be a **held tint**, not a repeating flash. A flashing rest-heal is the failure mode here. |

**Why the tint carries the source:** the player learns in one session that gold = the fire (the belt's selected-slot rim, the warmth bar, the campfire light — gold is already "warm/active/yours" across the whole UI, `gameplay-ui-direction.md` §7). So a gold-shifted HP bar means *the fire is mending me* with zero words. Cream = "an item did something" is the ledger/text voice, already the only cream in the corner.

**Audio direction `<deferred — no audio bus>`:** when a bus exists — heal item = a soft warm ascending two-note (SFX bus, ~-14 dB under ambient, 3–4 pitch-varied clips per `game-juice.md` §1.3 to avoid broken-record fatigue on a repeated verb); rest-at-campfire = **no** discrete cue (the campfire's own ambient loop is the cue — a second layered cue on a continuous heal is fatigue by construction); regen = silent, always. Do not source clips in this ticket.

**Hard don'ts (heals):** no full-bar white flash; no upward-floating "+N"; no power-up chime; no bloom; no screen-centre glow; no slow-motion. Recovery reads as **relief**, never as a buff pickup.

**Per-tier heal potency** (quality-bar #7, `86cah7xxp` AC8b pattern): easy heals more / rests faster, hard heals least. **The bar's LENGTH must not change per tier** — `Current01` is normalised and `easyMax 120` / `hardMax 80` must never become a physically longer or shorter bar (that would read as "easy gets a bigger health bar" and would break the fixed layout). Only what a segment *means* changes. Name this as a success-test.

---

## 5. Status-effect chips — the dock (owned by the sibling spec)

Chips live on the **HP row**, immediately right of the segment run: `x = 16 + 260 + 8 = 284`, `22 × 22` per chip, up to **4** chips, fixed left-to-right KIND order so *position itself* is a cue. Full per-effect visual/motion/duration/stacking spec lives in [`status-effect-readability-spec.md`](status-effect-readability-spec.md) §3–§5 — **do not re-spec it here.** This section only reserves the space and pins the anchor so the two tickets cannot collide: `284 + 4·22 + 3·4 (gaps) = 384 px` right edge, comfortably inside a 1280-wide frame and clear of the right-hand safe area.

---

## 6. Enemy-HP read — transient, above-head, never a nameplate

The player needs to answer *"is it nearly down?"* — and the calm tone forbids a persistent bar over every animal. So:

- **Primary read stays the BODY:** the `_HitFlash` material-instance pulse + the flinch/hit-react + the dust puff (`combat-cluster-design-brief.md` §1.2 / §2.5). Most of the "am I hurting it" answer must come from the creature itself. **No enemy HP element is drawn until the enemy has been hit at least once.**
- **Secondary read — a transient pip-row above the head.** On a landed hit, show a small pip-row anchored above that enemy for a short hold, then fade over ~0.4 s. **Reuse the `LootPrompt` above-head path** (Sponsor-decided anchor, DECISIONS 2026-07-21): `Camera.WorldToScreenPoint(root + Vector3.up * headAnchorHeight)`, screen-clamped with `ScreenMargin = 8f`, **hidden when the projection is behind the camera (`z <= 0`)**, IMGUI plate at `PlateAlpha = 0.55f`. One code path, one place to look.
  - **Form:** 5 pips (same 5-block grammar as the player's HP, so the player reads it instantly), pill ~`64 × 10`, pips `10 × 6` with 2 px gaps, on the standard dark plate.
  - **Colour:** the enemy's HP is **not** your vital — it must sit BELOW the player HUD in visual weight. Use a **desaturated bone/off-white `#CFC6AD`** (0.81, 0.78, 0.68 — the existing bone anchor, `style-guide-v2.md` §6) for filled pips and the shared `Charcoal` for spent. **Not red** — red is reserved for the player's own vitality, and a red bar over a boar reads as an action-game health bar. Value contrast, not hue: the world is saturated mid-green (`21h16_13`), so a **pale** pip on a dark plate holds at orbit distance where a mid-value colour would not.
  - **Boar / snake both** get it from the shared `Health` component — no per-enemy code (`BoarEnemy` and `SnakeEnemy` both carry `Health`; the pip-row binds `Health.Changed` on the hit target, never polls).
  - **Death:** the pip-row empties and fades with the topple; no "killed" flourish, no XP, no counter.
- **FORBIDDEN:** persistent enemy HP bars, floating damage numbers, nameplates, level labels, a target-locked HUD panel, a "weak to pierce!" popup. Quality-bar #9 (confirmed at the boar soak, 2026-07-22) says the matchup must read **emergently** from reach + weakness feedback — a text hint would actively break a bar the Sponsor has already passed.
- **Per-tier hold (a difficulty dial that isn't damage numbers):** easy ~3.5 s (a kid gets more time to see progress), medium ~2.0 s, hard ~1.2 s. Same element, different generosity — exactly the scariness-by-timing model the snake/boar specs use.

**⚠ Sponsor-judges-at-soak:** whether an enemy HP read should exist at all, versus body-read-only. Both are defensible in a calm game; the pip-row is the conservative middle (invisible until you engage, gone in a couple of seconds). One flag disables it entirely. See §8 Q2.

---

## 7. Tunables + registry ids (the dev-console surface)

Follow the shipped convention exactly: stable `snake_case` ids in `SettingsCatalog`, registered by a **new dedicated `PopulateHpHud` method** — *"each feature adds its OWN Populate method; never grows the base Populate signature"* (`SettingsCatalog.cs`, PopulateCombat/PopulateBoar comment). Per-tier sliders must write **both** the active field **and** the active tier's per-tier entry, or the knob is dead the moment `ApplyDifficulty` runs (the documented dead-knob class, `boar_*` precedent).

| Proposed id | Drives | Default |
|---|---|---|
| `hp_damage_flash_amp` | §2.3 layers A+B+C master amplitude scalar | 1.0 |
| `hp_damage_vignette_peak` | §2.3 C peak α | 0.18 |
| `hp_wince_debounce` | §2.4 minimum seconds between winces | 0.35 |
| `hp_low_warning_threshold` | §2.5 critical fraction (shipped `HpCriticalThreshold01`) | 0.25 |
| `hp_low_pulse_min_period` | §2.5 quickening floor (hard floor 0.70) | 0.75 |
| `heal_item_amount` | §4 heal-item HP restored (per-tier) | tier table |
| `campfire_rest_heal_rate` | §4 HP/s while resting (per-tier) | tier table |
| `enemy_hp_pip_hold` | §6 hold seconds before fade (per-tier) | 3.5 / 2.0 / 1.2 |
| `enemy_hp_pips_enabled` | §6 master off switch (the soak's revert path) | on |

Already shipped, reuse — do not re-mint: `hp_max`, `damage_taken_mul`, `hp_regen_rate`, `death_behavior_tier` (`PopulateCombat`), `boar_hp_max`, `boar_gore_damage`, `boar_charge_speed` (`PopulateBoar`).

---

## 8. Sponsor-input items + open questions (NONE block implementation)

- **Q1 — the 5-segment / taller HP bar (§2.1).** The one call that changes shipped geometry. Predicted win: instant "that's my health" separation. Revert = two constants. `needs-soak`.
- **Q2 — enemy HP pip-row (§6): exists at all?** Transient-on-hit vs body-read-only. `enemy_hp_pips_enabled` is the switch. `needs-soak`.
- **Q3 — death beat (§2.6): wordless, or one cream line** in the `LootPrompt` pill ("You wake by the fire")? Default OFF. `needs-soak`.
- **Q4 — wince amplitudes (§2.3).** Row-nudge 4 px cap, vignette peak α 0.18–0.30, lost-segment flash hold. Does the wince read as a wince or as a twitch?
- **Q5 — the quickening (§2.5).** Does a heartbeat that speeds up read as tension (intended) or as an alarm (wrong)? Floor is 0.70 s; flat 1.0 s is the fallback.
- **Q6 — heal tint legibility (§4).** Does the ember-gold shift during rest-at-campfire read as "the fire is mending me", or just as a colour glitch? The cream item-sweep vs the gold rest-tint must be distinguishable at a glance.
- **Q7 — hard-tier incoming hit-stop.** Default zero at every tier. Does hard want 2 frames? (Ceiling 2; never 3+ on incoming damage.)

## 9. Predict-Before-Soak (the Self-Test Report must carry these)

- *"The HP bar reads as a separate, more important element than the three need bars within one glance and without reading a label; a boar gore produces exactly ONE wince (segment flash + ≤4 px row nudge + a coal edge pulse that is fully gone inside 0.35 s); a 3-second bleed produces at most 9 winces and NO strobing; resting at a campfire tints the HP run toward ember-gold and eases back when I stand up; at ≤25 % HP the heart quickens but never blinks; nothing at any point shows a number, a damage numeral, or a 'YOU DIED' card; the BUILD stamp is never covered."*
- **Bounded convergence claim:** tested bars — #7 (3 tiers: per-tier heal potency + pip hold + death behaviour), #4 (real-world anchor: a wince reads as a wince), #2 (motion lively-but-damped: eased, never linear). **NOT tested:** #3 (material-honest — no meshes in this ticket), #5 (in-hand sizing), #9 (matchup legibility — untouched, but §6's no-text rule protects it).
- **Refuted prediction = a finding.** Stop and investigate the foundation before re-fixing (`[[claim-removed-soak-shows-present-investigate-foundation]]`).

## 10. Out of scope

The status-effect *definitions* (poison/stun/slow — the sibling spec + `86cah7yuh`); the `Health`/`HealthRegen`/`DeathHandler` mechanics (shipped in the POC); an IMGUI→UI-Toolkit HUD migration (the standing separate follow-up, `need-meter-3bar-direction.md` §7); **any audio file** (no bus exists — §1); enemy hit-flash / flinch / dust (swings + boar tickets, already shipped or spec'd in `combat-cluster-design-brief.md` §1.2/§2.5); damage numbers or a combat log (forbidden, not deferred); a heal-item *recipe* or its icon (crafting/icon tickets); a settings toggle to hide the HUD.

## 11. Decision drafts (for Priya's DECISIONS.md batch — I do not edit that file)

- **Decision draft:** HP earns HUD hierarchy through **FORM, not position or volume** — 5 chunky segments + a taller `260×34` bar + a 10 px breath gap above thirst (`y = -162`), with the shipped `VitalRed/WoundOrange/DarkBlood` ramp and the left-column anchor unchanged; the ledger moves to `-204`. The needs' `TopSegmentThreshold` near-full exception does NOT apply to HP. (`hp-hud-polish-spec.md` §2.1/§3.)
- **Decision draft:** A heal identifies its SOURCE by **amplitude + a tint borrowed from the source** — regen silent/untinted, heal-item a cream sweep + ≤8-particle puff, rest-at-campfire a held ember-gold shift on the filled run. Extends the "the need's colour IS the source's colour" rule from the thirst bar. No numbers, no toast, no chime (no audio bus exists). (`hp-hud-polish-spec.md` §4.)
- **Decision draft:** The low-HP warning stays **inside the HUD** (quickening heart-glyph pulse, floor 0.70 s + one faltering segment). A screen-edge coal vignette is permitted ONLY as a transient damage pulse (≤0.35 s, peak α ≤0.30); a **sustained** low-HP vignette, a post-process pulse, and any hit-stop on INCOMING damage are forbidden. This resolves the fail-state surface `hud-three-bar-spec.md` §4 deferred. (`hp-hud-polish-spec.md` §2.3/§2.5.)
- **Decision draft:** Enemy HP is a **transient above-head pip-row** (5 bone-white pips, on-hit only, per-tier hold 3.5/2.0/1.2 s) reusing the `LootPrompt` above-head anchor — never a persistent bar, nameplate, or damage numeral; the body (hit-flash + flinch) stays the primary read, protecting the emergent-matchup bar #9. (`hp-hud-polish-spec.md` §6.)
- **Decision draft:** DoT-sourced damage must be **debounced in the HUD** (≥0.35 s between winces, amplitude scaled by damage fraction) because bleed/poison route through `Health.ApplyDamage` and would otherwise strobe the wince several times a second. A source tag on `ApplyDamage` was considered and rejected as the more invasive fix. (`hp-hud-polish-spec.md` §2.4.)

## Cross-references

- **Tickets:** `86cah7z2q` (this spec) · `86cah7xxp` (POC — `Health`/`HealthRegen`/`DeathHandler`/the shipped HP bar; COMPLETE) · `86cah7yuh` (status effects — the sibling spec) · `86cah7ydt` (boar — the enemy this reads) · `86caaz4vn` (snake) · `86cabcdpn` (combat design lock) · `86caamkxv` (three-bar HUD lineage).
- **Code (ground truth):** `Assets/Scripts/Runtime/SurvivalHud.cs` (rows, `SegmentCount`, `PlateAlpha`, `FilledSegments`, `HpBandColor`, `HpCriticalThreshold01`, `CriticalPulsePeriod`, `GlyphPulseAlpha`, `DrawNeedBar`, `DrawInventoryLedger`) · `Assets/Scripts/Runtime/Combat/Health.cs` (`Current01`/`Changed`/`Died`/`ApplyDamage`/`Heal`/`RestoreFull`/`ApplyDifficulty`) · `HealthRegen.cs` · `DeathHandler.cs` · `StatusEffectController.cs` (the DoT tick that forces §2.4) · `BoarEnemy.cs` / `SnakeEnemy.cs` (shared `Health`) · `LootPrompt.cs` (the above-head world anchor + literal-letter/Danish-layout rule) · `Settings/SettingsCatalog.cs` (id convention + `PopulateCombat`/`PopulateBoar`) · `BootHud.cs` (the BUILD stamp that must stay uncovered).
- **Docs:** `.claude/docs/game-juice.md` (§1 the five must-haves, §2 hard don'ts — every cap here) · `.claude/docs/art-direction.md` + `inspiration/21h00_32`, `21h16_13` (looked at them) · `.claude/docs/unity6-mastery.md` §2 (GRD / no MPB) · `.claude/docs/vision-far-horizon-game-concept.md` (kid→adult).
- **Uma specs:** `combat-cluster-design-brief.md` §3.3 (the lighter first pass this supersedes for implementation detail — incl. the chime, now `<deferred>`) · `hud-three-bar-spec.md` · `u2-5-survival-hud-spec.md` · `style-guide-v2.md` §6 · `gameplay-ui-direction.md` · `difficulty-snake-poc-ux.md` §A6/§B3 · `status-effect-readability-spec.md` (sibling).
- **Bars / memories:** `quality-bars.md` #2, #4, #7, #9 · `[[difficulty-settings-easy-medium-hard]]` · `[[verify-soak-builds-or-bake-and-judge]]` · `[[served-unverified-soaks-need-played-verification]]` · `[[sponsor-danish-keyboard-layout]]` · DECISIONS 2026-07-21 (above-head prompt anchor), 2026-07-22 (boar soak PASS / bar #9).
