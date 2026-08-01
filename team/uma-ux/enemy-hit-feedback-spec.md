# Enemy Body-Level Hit Feedback — design spec (`_HitFlash` + procedural flinch + dust puff)

**Ticket:** `86caxjwb3` (feat(combat): enemy body-level hit feedback).
**Owner (impl):** Drew · **Reviewer:** Devon · **Spec author:** Uma · **Lane:** Unity-build, soak-gated.
**Work-type:** design spec (design-only; no code in this PR — docs-only, so `ci.yml` does not start and there is
**no `structure` / `build` / `capture` / `playmode` result**; the separate `docs-markup` workflow **does** run and
gives one real hosted green. See `team/TESTING_BAR.md` § *What CI actually covers, by PR lane*.
*Corrected 2026-07-31 — this line previously read "zero CI checks fire", which is false;
`[[ci-paths-ignore-skips-the-whole-run]]` is `ci.yml`-scoped only.*)

**Builds on, does NOT re-run:** `team/erik-consult/enemy-hit-feedback-hitflash-particle-flinch.md` — the
TECHNIQUE research. Read its **CORRECTIONS block first**; where the block and the note's body disagree, the
block wins, and where the block and this spec are both silent, the ticket's `[DFC-1…5]`/`[DFC-B]` ACs win.
**This spec adds no technique claim to Erik's note and re-derives none of it.** It answers the five DESIGN
questions the ticket leaves open, and it reports two findings that change what "fire on a damage delta" means.

**Sibling spec — read as ONE system:** [`enemy-hp-read-spec.md`](enemy-hp-read-spec.md) (`86caxhfg2`, PR #371 —
the above-head pip row, deferred behind this ticket). §1 below is the divide-the-labour contract between them,
and §1.3 **amends** that spec's §3.2 in one place. Everything else there is cited, not restated.

> **📌 REVISION 2026-08-01 — §16 added: the body read audited as a bar-#10 CHANNEL.**
> The pip-row re-audit (PR #406, `enemy-hp-read-spec.md` §15) established that **the pip row alone is a
> ONE-channel cue** and that the enemy-damage read meets bar #10's ≥2 **only as pip-row + body read**. That
> makes this spec's subject the channel the composed cue rests on, so **§16** states what the ticket never
> asked for: **which axis the body contributes, its cued-vs-non-cued DELTA in pixels at the canonical
> framing, and whether it survives C2 against the pip row.** Three of its findings are unwelcome and are
> stated as findings rather than smoothed over — **§16.5** (the body is **ONE** failure domain, not the
> three `enemy-hp-read-spec.md` §15.4 credits it with — that count is **withdrawn** here), **§16.4** (the
> boar's flinch is a **1.7–3.2 px** channel at the framing the player actually plays at, and unlike the pip
> row's, its verdict **turns on** which C1 floor is eventually chosen), and **§16.6** (§5's weapon-weight
> differentiation is **sub-pixel-to-1.33 px** on the boar's flinch). **Every px figure below is recomputed
> from source and shows its arithmetic; every source claim is pinned to `fb2ac24`, never to `origin/main`.**
> ⚠ **The 2.4–3.2 px figure this banner carried in draft was a pre-arithmetic estimate and is retired** —
> §16.4a computes 1.7085–3.1776 px. It is corrected here rather than quietly deleted, because a banner
> number written before the arithmetic is exactly the shape §16 exists to catch.
>
> 🔴 **REVISION 2 — 2026-08-01, from the PR #413 peer review. §16.10a is the ledger; read it before quoting any
> figure from a §16 draft.** Seven corrections, one of which moves a conclusion: **§16.4c's puff table mixed two
> measurement planes** (frame-plane chunk extent ÷ foreshortened creature height, every %-of-height figure
> inflated by `sec 55° = 1.7434×`), and with that fixed **§13 Q9's *"B is the only option"* is WITHDRAWN — no
> option is eliminated.** That table is Sponsor-facing decision input, which is why it was fixed before merge
> rather than deferred as a NIT. The other six move no verdict: §16.5's C2 framing is restated at its real
> strength (the divergence is a granularity inconsistency in the sibling spec, **not** a silence in bar #10),
> §16.7's snake-backstop hypothesis is **discharged as measured** (snake Cue C = 2), and four arithmetic /
> counting corrections are logged in §16.10a. **A mirror onto PR #406 is OWED and filed there** — §16.10a's
> closing note.
> §16 changes no design value in §§0–15 — it measures them, and where a measurement contradicts a claim
> §§0–15 made, the CLAIM is withdrawn rather than the number softened. Where it supplies a value that did
> not exist (the puff's geometry, §16.4c), it says so and offers it as Sponsor options rather than settling
> it. **The one edit §16 makes above itself is a disambiguation, not a value:** §4.3's snake head yaw
> `±12°` is pinned as a PEAK excursion (§16.4b).

**Also builds on:** [`combat-cluster-design-brief.md`](combat-cluster-design-brief.md) §1.2 / §2.4 / §2.5 / §4
(mine; §3 and §5 below deliberately REFINE two lines of it — flagged where) · [`style-guide-v2.md`](style-guide-v2.md)
§5 (sub-1.0 HDR discipline) / §6 (warm-bias-plus-saturation rule) · `.claude/docs/game-juice.md` §0/§1/§2 ·
`.claude/docs/procedural-animation-verbs.md` (the additive-offset idiom + its authoring checklist) ·
`team/quality-bars.md` **#2 / #7 / #9 / #10**.

**Board (looked at the images, not the prose):** `inspiration/2026-06-12_21h10_44.png`,
`2026-06-12_21h16_13.png`. Two things in them drive this spec. First, **the world is high-key and saturated** —
bright greens, no dark values anywhere in frame — so a "bright flash" has far less contrast headroom here than in
the dark-game references the technique is usually drawn from; brightness alone is a weak lever. Second, **the
style's whole identity is BROAD FLAT FACETS with visible shading steps**, and the creatures read as chunky
silhouettes with a few strong tone regions (dark eye, ivory tusk, body brown). Anything that flattens those
regions into one tone destroys the read it was meant to serve. §3 is built on that observation.

---

## 0. Tonal anchor (read this first)

> **The creature is an ANIMAL that just got hit, not a hitbox that registered a value.** The read you want is a
> body flinching because something landed on it — a hitch, a head thrown up, a puff of dry earth kicked off the
> ground — and then the animal carrying on. It is one beat, over in a quarter of a second, and then the island
> is quiet again. If a player would describe it as "the enemy flashes when you hit it," the execution has drifted
> toward an action-game hit-marker; if they'd describe it as "it flinched," it landed.

The single sentence `game-juice.md` §0 asks for: **"my axe landed on something solid and alive."** Not "a hit
registered." Not "critical damage." If any beat below reads louder than that sentence, it is miscalibrated —
turn it down.

**The three channels are not three effects; they are one event seen three ways.** The flash says *it happened*
(instant, whole-silhouette, binary). The flinch says *how hard* (a fifth of a second, physical, proportional).
The dust says *where, and in this world* (earthy, brief, grounds the impact in the island's materials). Cut any
one and the event loses a dimension — which is why AC1's all-three constraint is right and must not be descoped.

---

## 1. The composed system — what the BODY carries that the PIP ROW cannot

### 1.1 The division of labour, in one sentence

> **The body says "that landed, and this hard"; the pip row says "and it's this close to down."**

| | Body read (`86caxjwb3`) | Pip row (`86caxhfg2`) |
|---|---|---|
| Question answered | *Did I connect?* | *Is it nearly down?* |
| Resolution | **ANALOG** — continuous in the hit's weight | **QUANTIZED** — 5 blocks + a drain alpha |
| Time-shape | **INSTANTANEOUS** — one beat, ≤0.25 s | **CUMULATIVE** — a state that persists ~2.5 s |
| Attachment | ON the creature (its own material + its own pose) | ABOVE the creature (a UI plate) |
| Channels | value-step + form-displacement + added silhouette (dust) | form (block count) + position + value |
| Fails when | absent — the player can't tell a hit from a whiff | absent — the player can't tell if they're winning |

### 1.2 Why the body is load-bearing on exactly the hits the pip row cannot serve

`enemy-hp-read-spec.md` §2.3 establishes from the shipped numbers that a pip is 20 % of max HP, so **a
low-damage weapon moves no pip on ~44 % of hits on a medium boar and ~55 % on hard**. On those hits the pip row
shows an unchanged block count. **The body read is the entire answer to "did I connect?" on more than half the
hits of the game's weakest weapon** — that is not a nice-to-have layer over the HP read, it IS the feedback on
those frames. It is also why the body must be **analog**: it is the only element that can distinguish a 4.5 HP
dagger tap from a 24 HP spear thrust, because the pip row quantizes both into "nothing moved" / "one block".

### 1.3 The anti-double-signal rule — and a one-line AMENDMENT to `enemy-hp-read-spec.md` §3.2

On a hit where a pip DOES go fully out, four things fire on one frame: body flash, flinch, dust puff, and (per
that spec §3.2) the pip's **lost-pip extinguish flash** (`#EAD9B8` at α ≤ 0.85, ≤ 0.24 s). Three of those four
carry information. The extinguish flash does not — it duplicates the flash's *"something happened"* message,
while the pip's INFORMATION (the count went down) is fully legible from the count alone.

> **AMENDMENT to `enemy-hp-read-spec.md` §3.2 — the lost-pip extinguish flash fires ONLY when no body flash
> fired within the last `enemy_hit_flash_seconds`.** In practice that makes it a **bleed/DoT-only accent**: a
> strike always brings a body flash, so the extinguish flash is suppressed there; a bleed tick that empties a
> pip inside a live row brings no body flash (§2.1), and there the extinguish flash is the only thing that would
> mark the change — so it keeps a real job.

This is not a new arbitration; it is that spec's own §5.3 rule (*"if the row is the loudest thing on an impact,
lower the row, never raise the body"*) applied by construction instead of by tuning. Precedence is
**one-directional**: the body is never suppressed because the pip carries something. Nothing in this spec asks
the pip row to grow a channel, and nothing here waits on `86caxhfg2` — if that ticket closes at this soak, the
amendment simply never applies.

**Loudness ordering (refines that spec's §5.3, does not contradict it):** on the **impact frame** the ordering
stands — flash > flinch > dust > pip. Across the **whole event** the flinch is the longest-lived and is what
makes the hit read as weight; the flash is a spark on one frame. Both statements are true; grade the capture on
the impact frame, grade the feel on the event.

---

## 2. Ground truth read from `origin/main` — two findings that change the ticket's premise

Every value below was read during this spec's authoring at `c8ce948` and is quoted, not inferred.

### 2.1 🔴 FINDING A (BLOCKING) — bleed calls `Health.ApplyDamage` **EVERY FRAME**, so "fire on a `Health.Changed` damage delta" strobes all three channels for 3 s

`StatusEffectController.Update()` computes `dt = now - _lastTickTime` and calls `TickSeconds(dt)`
(`StatusEffectController.cs:55-61`); `TickSeconds` calls `health.ApplyDamage(a.Spec.damagePerSecond * slice,
a.Spec.damageType)` per active effect (`:99`). `Health.SetCurrent` fires `Changed` on any non-approximate change
(`Health.cs:183-188`). The stone axe applies `MakeBleed(AxeBleedDps = 2f, AxeBleedDuration = 3f)`
(`WeaponCatalog.cs:65-66`, applied `:260`); the iron axe `3f / 3f` (`:119-120`, applied `:312`).

**So every axe hit is followed by ~180 `Health.Changed` events at 60 fps over 3 s.** A literal reading of AC1's
*"fire from `Health.Changed` on a damage delta"* + AC1's mandatory previous-value guard (which only excludes
heals and init — a bleed tick IS a damage delta) produces, for three seconds after every axe hit:

- **the flash timestamp rewritten every frame → a continuously-lit enemy**, visually indistinguishable from the
  `[DFC-1]` latch bug;
- **the flinch re-triggered every frame → a vibrating boar** — a sustained wobble, a `game-juice.md` §2
  hard-don't, and the exact opposite of "the recoil resolves";
- **a dust burst every frame → ~60 bursts/s × up to 12 particles**, a smoke column instead of a puff, with the
  brand-new pool thrashed at 60 releases/s.

**It is invisible to every AC7 test as written** (none of them tick a bleed alongside a strike), and it poisons
the `[DFC-1]` verification: **AC6(d)'s "hit, then WAIT, confirm base colour" check FAILS on a bleeding boar for
a completely different reason than the latch** — a misdiagnosis trap that would send a dev to rewrite a
correct clock. *(Every fact in this paragraph is quoted above; the failure itself is a `Hypothesis:` — a
predicted consequence of the shipped code, not an observed soak.)*

**The fix — a two-part gate, both attacker-free so AC1's shared-path constraint is honoured verbatim:**

1. **MAGNITUDE GATE (the semantic rule).** The body read fires only when a single `Changed` damage delta is
   **≥ 2.0 % of `Health.Max`**. A hit is a hit because it is BIG, not because of who sent it — so no attacker
   coupling, no new `DamageType`, no per-enemy branch, and a future hazard/fire source is covered for free.
   The separation is wide and verified from the shipped constants:

   | Source | Per-call HP | As a fraction of `Max` |
   |---|---|---|
   | Iron-axe bleed tick @60 fps (worst bleed) | `3 × 1/60` = **0.050** | boar-50 **0.10 %** · snake-24 **0.21 %** |
   | Stone-axe bleed tick @60 fps | `2 × 1/60` = **0.033** | boar-40 **0.083 %** |
   | ⟵ *gate at 2.0 %* | boar-40 **0.80** · snake-24 **0.48** | **2.00 %** |
   | Weakest shipped strike (`dagger_wood` 6 slash × 0.75) on a HARD boar | **4.5** | **9.0 %** |
   | `axe` (stone, 14 slash × 0.75) on a medium boar | **10.5** | **26.3 %** |
   | `spear_iron` (12 pierce × 2.0) on a medium boar | **24.0** | **60.0 %** |

   The gate sits ~**9.5×** above the worst bleed tick and ~**4.5×** below the weakest real strike.
   *(Sources: `WeaponCatalog.cs:62/86/95/121`, `BoarEnemy.cs:40/42/44/49/54`, `SnakeEnemy.cs:32/36`.)*

2. **REFRACTORY WINDOW (the frame-rate-independent guard).** After a fire, suppress re-fire for
   **0.12 s**. Needed because a bleed tick's amount is `dps × dt`, so the gate is frame-rate-sensitive: on a
   snake (24 HP) an iron-axe tick clears 2 % once `dt ≥ 0.16 s` (~6 fps). One stray flash on a severe hitch is
   acceptable; a strobe is not, and the window makes the strobe impossible regardless of frame time.

**Do NOT solve this by excluding bleed at the source** (a `DamageType`/source tag threaded through
`ApplyDamage`) — that adds an attacker/source concept to the one shared damage seam for a cosmetic system, and
it silently fails for the next non-strike damage source nobody remembers to tag.

### 2.2 🔴 FINDING B (BLOCKING for any damage-proportional design) — `removed` is CLAMPED, so a weight driven off it makes **the killing blow the quietest hit of the fight**

`Health.ApplyDamage` computes `effective = amount × resistance.Multiplier(type) × max(0, damageTakenMul)`
(`Health.cs:151`), then `SetCurrent(_current - effective)` where `SetCurrent` **clamps to `[0, max]`**
(`:185`), and returns `removed = before - _current` (`:157`). So on a boar with 1 HP left, a `spear_iron`
thrust whose *intended* effective damage is 24.0 returns `removed = 1.0` — **2.5 % of the intent**.

Any amplitude curve driven off `removed` (or off the `Current01` delta the `Changed` event carries) therefore
renders **the kill as the feeblest hit in the fight** — and it would also drop below the §2.1 magnitude gate
(1.0 HP on a 40 HP boar = 2.5 %, and on a hard 50 HP boar 1.0 HP = 2.0 %, i.e. **right on the gate**), so on
hard the killing blow can produce *no body feedback at all*. That is the worst possible frame to go silent.

**The fix — drive the weight off the hit's INTENT, not off the HP it managed to remove.** Requirement, with
my pick named so nobody has to guess:

> **Pick — `Health` records the pre-clamp `effective` of the most recent `ApplyDamage` in a public read-only
> property (an `enemy-hit-feedback`-facing `LastEffectiveDamage`-class value), assigned at `Health.cs:151-152`
> before `SetCurrent`.** Reason: the feedback driver keeps reading ONLY `Health` — no attacker coupling (AC1),
> no per-enemy branch, and every non-weapon damage source is covered because they all route through this one
> documented seam. Two lines, in the file that already owns `Changed` and `Died`.

The rejected alternative — recompute `weapon.Damage × resistance` on the `MeleeAttack` side and pass it in — is
attacker coupling (AC1 forbids it), and it is blind to any damage that does not come from a weapon.
*(Route change requires a STOP-and-report to the orchestrator with Devon agreeing before build, the same
discipline the ticket applies to `[DFC-5]`'s route (i)/(ii).)*

**Consequence for the magnitude gate:** the gate must ALSO read the intent, not `removed` — otherwise a killing
blow on a nearly-dead enemy fails the gate as shown above. One scalar, used by both. Pin it in AC7.

### 2.3 The creature palette — and why "lerp toward warm white" would erase the eyes and the tusks

| Vertex tone | RGB (0–1) | Source |
|---|---|---|
| `BoarBrown` (body / head / tail) | 0.42, 0.32, 0.22 | `MovementCameraScene.cs:2562` |
| `BoarSnout` | 0.52, 0.42, 0.34 | `:2563` |
| `BoarTusk` (ivory) | **0.90, 0.88, 0.78** | `:2564` |
| `BoarEye` (near-black) | **0.06, 0.05, 0.04** | `:2565` |
| `BoarLegCol` / `BoarHoof` | 0.36, 0.27, 0.19 / 0.14, 0.11, 0.09 | `:2566-2567` |
| `SnakeRust` / `SnakeDark` | 0.78, 0.38, 0.16 / 0.34, 0.18, 0.10 | `:2539-2540` |
| `SnakeHeadCol` / `SnakeEye` | 0.85, 0.45, 0.18 / 0.06, 0.05, 0.04 | `:2541-2542` |

**The load-bearing fact: these are per-VERTEX colours inside a SINGLE mesh part, sharing ONE material.**
`LowPolyMeshes.BoarHead(..., BoarBrown, BoarSnout, BoarTusk, BoarEye, 74111)` (`:2814`) bakes body-brown, snout,
ivory tusk and near-black eye into one head mesh; the shader's albedo is `IN.color.rgb * _Tint.rgb`
(`LowPolyVertexColor.shader:240`). A per-material flash therefore hits all four tones with the same operation.

A lerp toward a flat warm cream at a readable amplitude (~0.55) takes the **eye** from 0.06 → ~0.53 and the
**tusk** from 0.90 → ~0.91. For the flash's duration the boar has **no eyes and no tusk contrast** — it becomes
a washed-out ghost of itself. Those are the two features the art board makes identity-critical ("big expressive
dark eyes"; the tusks are also the *matchup* read, quality-bar #9). §3 fixes this.

### 2.4 Bloom headroom — the sub-1.0 rule pays off concretely

`Assets/Settings/ZoneD_PostProfile.asset:59-64` — Bloom `threshold: 1.02`, `intensity: 0.25`. A flashed albedo
capped at **0.92** cannot bloom on its own; it could only cross the threshold if direct lighting multiplied the
brightest channel by > 1.11×. So the amplitude cap is a real cap, not one that bloom quietly re-opens.
**Verify in the shipped capture, not in the editor** (`unity-conventions.md` §Editor-vs-runtime).

### 2.5 Everything else this spec relies on (already in the ticket — cited, not restated)

No Animator on either enemy; `BoarBodyRig` poses 7 baked parts in `LateUpdate` with additive
`Quaternion.Euler` terms right-multiplied onto a captured HOME pose (`BoarBodyRig.cs:143-178`);
`SnakeBodyChain` poses 13 segments along a trail with a lateral slither + a front-links lift
(`SnakeBodyChain.cs:254-298`); 20 unique inline material instances already exist; `Health.Changed` fires on
heal/init too. Amplitudes I calibrate against are the SHIPPED, Sponsor-PASSED ones on the same rigs:
`headLowerDeg = 34f`, `chargeLeanDeg = 12f`, `legSwingDeg = 18f`, `breatheAmplitude = 0.015f`, tail wag `±8°`
(`BoarBodyRig.cs:67-75`, `:170`); `slitherAmplitude = 0.055f`, `telegraphLift = 0.32f`, `telegraphLinks = 3`,
head tell `-40° × rear` (`SnakeBodyChain.cs:54/65/67`, `:294`).

---

## 3. Q1 — the flash: amplitude, duration, and whether it scales

### 3.1 ⚠ REFINEMENT of `combat-cluster-design-brief.md` §1.2: a warm **EXPOSURE LIFT with a sub-1.0 ceiling**, not a lerp toward flat warm-white

§1.2 (mine) says *"a brief sub-1.0 warm-white tint pulse"*. Per §2.3 that would erase the eye and tusk tones
that share the material. **The corrected look requirement:**

> **The flash brightens each vertex tone along its OWN colour — a warm-biased multiplicative lift, clamped to a
> sub-1.0 ceiling — so every tone gets brighter while their RELATIVE order is preserved. The boar's eye stays
> the darkest thing on it and the tusk stays the brightest, at every point in the pulse.**

Behaviour (the numbers are the design; the one-liner is illustrative, not a prescription — the property shape is
the implementer's call per AC2's *"whichever property shape ships"*):

```
gain   = (1.90, 1.72, 1.50)      // warm-biased: strongest in R, weakest in B (style-guide §6 warm bias)
ceil   = 0.92                    // sub-1.0 (style-guide §5); below Bloom's 1.02 (§2.4)
lifted = min(albedo * gain, max(albedo, ceil))     // the max() guards a vertex already above the ceiling
albedo = lerp(albedo, lifted, flashFactor)         // flashFactor = amp * easedDecay, 0 at rest
```

What that does to the shipped tones at full flash:

| Tone | Base → flashed | Relative luma step |
|---|---|---|
| `BoarBrown` | (0.42,0.32,0.22) → (0.80,0.55,0.33) | **1.76×** |
| `BoarTusk` | (0.90,0.88,0.78) → (0.92,0.92,0.92) | stays the brightest ✓ |
| `BoarEye` | (0.06,0.05,0.04) → (0.11,0.09,0.06) | stays near-black ✓ |
| `SnakeRust` | (0.78,0.38,0.16) → (0.92,0.65,0.24) | **1.52×** |

Three properties this buys that a flat-cream lerp does not: the **facet identity survives** (ratios preserved →
the shading steps stay visible, the body doesn't become a silhouette blob); **quality-bar #10 is satisfied on a
hue-independent channel** (the step is ~1.5–1.8× in *luma*, so it survives the desaturated-capture check on both
enemies); and **the no-op-at-default proof is unchanged** — at `flashFactor = 0` the expression is exactly
`albedo`, bit-identical, for every other consumer of the shared shader (AC2's 🔒 / AC7's shader no-op test).

**Apply it to ALBEDO, BEFORE lighting.** There is a shipped precedent one line away: `_MeadowPatchAmp` does
`albedo = lerp(albedo, patchTone, ...)` at `LowPolyVertexColor.shader:252` under the comment *"Applied to albedo
BEFORE lighting so the extra tone lights like the rest of the ground"* (`:247`). Same reason here: a
post-lighting add would flatten the facets, which is the one thing this art direction cannot afford.
**Known trade-off:** a fully shadowed facet gets ambient only (`:275-278`), so the shadow side flashes less.
That is the correct trade (form over uniformity). **Dial direction if the soak says the shadow side doesn't
read: add a small post-lighting lift, ≤ 0.15 weight — never raise the albedo gain past the point where the body
out-brightens the tusks.**

### 3.2 The curve: instant attack, eased release (this is what "eased out" means)

`[DFC-2]` requires a non-linear decay and leaves the route open. The design requirement:

- **Attack = ZERO frames.** Peak on the impact frame. Any ramp-in delays the causal link between the click and
  the reaction, and inside a ~5-frame effect a ramp eats the read entirely. "Eased out" in §1.2 means the
  RELEASE is eased — it does not mean ease-in-out. Say which you shipped in the PR body.
- **Release = eased-out, monotonic, no overshoot.** Devon's `t *= t` satisfies this; so does any
  smoothstep-out. Light does not bounce — the flash never comes back up.
- **Duration = 0.08 s default** (the ticket's 🎚️, unchanged), i.e. ~5 frames at 60 fps.

### 3.3 Distance: the amplitude does NOT scale, and the duration is the lever

> **The flash amplitude is distance-invariant. If it doesn't read at orbit distance, raise the DURATION (up to
> ~0.14 s), never the amplitude.**

Three reasons. (a) **Amplitude is already at its ceiling for a different reason** — §3.1's tusk/eye contrast
requirement and the 0.92 sub-1.0 cap bound it; raising amplitude to buy distance-readability spends the
intra-body contrast that makes the creature legible at all. (b) **The failure mode at distance is small screen
AREA, and duration is the right cure for that, not intensity** — a brief change on a small target is missed
because it wasn't on screen long enough to be noticed, and the flash already covers 100 % of the creature's
pixels, which is the most area-efficient a cue can be. (c) **A distance-varying amplitude makes the same hit
look different depending on the camera**, so the player can never calibrate it — and it makes soak captures
ungradeable against each other. Consistency beats local optimality here.

*(Mechanically it also matters: a distance-scaled amplitude means a per-frame per-enemy camera-distance write
into 20 material instances — a hot-path cost AC1's no-per-frame-allocation constraint has no appetite for.)*

### 3.4 The flash does NOT scale with damage — it is the binary "connected" channel

See §5. The flash answers *did it land*; the weight lives in the flinch and the dust.

---

## 4. Q2 — the flinch: which parts, how far, how it decays

**Which rig:** the **ENEMY** rigs — `BoarBodyRig` (7 baked parts) and `SnakeBodyChain` (13 segments). **Not the
castaway.** `procedural-animation-verbs.md`'s hard chain (`CastawayArmPose` order 50 → `CastawayHandPose` 65 →
`HeldAxeRig` 100 → `CastawayLeftArmHaftIk` 110) **does not apply** — there is no Animator, no skinned mesh and
no held prop on either enemy, so there is no clip pose to compose onto and no seat to feed. **What DOES carry
over, and is mandatory:** the additive-offset-onto-a-captured-HOME idiom, the `Time.time`-anchored public
`NormT` property for headless test access, the **zero-at-rest → identity** requirement (so the shipped
silhouette is byte-unchanged when not flinching), and the PlayMode trap list (**never `WaitForEndOfFrame`**;
never assert on `Time.deltaTime`). The `game-juice.md` §2 **no-squash/stretch** rule also carries — its stated
reason (skinning + `HeldAxeRig` desync) doesn't apply here, but its tonal reason does: non-uniform scale on a
creature reads as cartoon slapstick, which is the wrong register for an animal being hurt. **Rotation and small
per-part offsets only. No scale terms, on either enemy, ever.**

### 4.1 The rule that generalizes to enemy #3: **the flinch occupies an axis ORTHOGONAL to that creature's telegraph**

Both enemies have a Sponsor-PASSED telegraph, and both telegraphs are *anticipation* poses. A flinch that moves
along the same axis would fake a telegraph — corrupting the one read the boar soak already passed
(quality-bar #9; DECISIONS 2026-07-22). So:

| Creature | Its telegraph (shipped) | Its flinch (this spec) |
|---|---|---|
| Boar | head **DOWN** `+34° × windup` pitch, body **forward** lean `+12° × charge` (`BoarBodyRig.cs:140-141,157,161`) | head **UP** (negative pitch), body **BACK** (negative pitch) |
| Snake | **VERTICAL** rear — `telegraphLift 0.32u` on the front 3 links + `-40°` head-up pitch (`SnakeBodyChain.cs:275-280,294`) | **LATERAL** whip — a decaying sideways pulse travelling back down the chain |

### 4.2 Boar — head-toss + body recoil + tail flick; legs untouched

Peak amplitudes are stated as **fractions of an already-Sponsor-approved amplitude on the same rig** — that is
the calibration method, so the numbers are defensible rather than invented:

| Part | Term | Peak default | Calibration |
|---|---|---|---|
| Head (`HeadIndex`) | pitch **−14°** (up — opposite the gore tell) | 14° | **41 % of `headLowerDeg` 34°** — the flinch must never out-shout the telegraph |
| Body (`BodyIndex`) | pitch **−5°** (recoil back) | 5° | **42 % of `chargeLeanDeg` 12°** |
| Tail (`TailIndex`) | yaw **+10°** kick | 10° | **1.25× the ±8° idle wag** — just enough to read over the continuous wag |
| Legs (2..5) | **nothing** | — | see below |

- **The legs are deliberately untouched.** Injecting a term into a phase-driven gait sine stutters the walk, and
  four legs moving together reads as a *collapse* — i.e. as death. The legs are the *"it's still coming"* read
  that hard tier requires (§4.5); leaving them alone is what keeps a flinch from looking like a stumble.
- **The tail flick earns its place on camera grounds:** at orbit distance from behind, the head is often
  occluded by the body. The tail is the one part that reads from every angle, and a flick is the cheapest,
  most animal-legible "it reacted" there is.
- **Rotation-only — no positional term.** The parts all hang off a shared `bodyOrigin` computed from the
  agent-owned root (`BoarBodyRig.cs:120-126`); translating the body part alone slides it off its legs, and
  translating all parts is a visual root shift that desyncs the body from its collider/agent. **Hard don't: the
  flinch never writes the root and never moves `bodyOrigin`.**
- **Composition, in the rig's own idiom:** one more additive `Quaternion.Euler(...)` right-multiplied onto
  `_homeRot[i]` in the existing per-part branch, gated by the SAME `dead ?` ternary family the rig already uses
  (`dead ? 0f : flinchTerm`) — see §6.

### 4.3 Snake — a lateral whip that travels back down the chain

- **Head + the front `flinchLinks = 4` segments** get an added **lateral** offset (the same
  `Vector3.Cross(Vector3.up, tangent)` basis the slither already uses, `SnakeBodyChain.cs:267`), peaking at
  **0.09 u** on segment 0 — **1.6× `slitherAmplitude` (0.055 u)**, so it reads *over* the ongoing wave rather
  than blending into it.
- **Taper — copy the shipped telegraph taper verbatim:** `k = 1 - i/(flinchLinks+1); k*k`
  (`SnakeBodyChain.cs:278-279`). Reuse, don't reinvent: it is already Sponsor-passed on this body.
- **Phase lag per segment** so the recoil *travels* backward (~0.02 s per link). A snake's whole body is its
  expression; a head-only recoil on a 13-segment chain reads as the head detaching from the body.
- **Head yaw ±12°** added onto the per-frame `LookRotation` (`:292-295`) — lateral, never pitch (pitch is the
  tell). *(**±12° is a PEAK excursion**, the same convention as the boar's head `−14°` in §4.2 — not a 24°
  full swing. Pinned 2026-08-01 because §16.4b's magnitude doubles under the other reading and the wording
  did not exclude it. No value changed; the ambiguity did.)*
- **No vertical term at all.** Vertical is the rear/strike tell (§4.1).

### 4.4 Decay — and the one place bar #2 and the "no wobble" don't must be reconciled

- **Attack ≈ 0.04 s** (eased in, ~2–3 frames). Unlike the flash, a body has inertia; an instant pose snap reads
  as a teleport, not a hit.
- **Release ≈ 0.18 s**, eased out. **Total envelope ≤ 0.22 s** — deliberately ~2.75× the flash, because the
  flinch is the channel that carries *weight*, and weight is a function of duration.
- **Exactly ONE small counter-overshoot, ≤ 15 % of peak, absorbed inside the same 0.22 s.** This is the
  reconciliation: `game-juice.md` §2 forbids *sustained* wobble and the ticket's AC3 says "the recoil resolves",
  while quality-bar #2 forbids motion that is dead/over-damped. A single ≤15 % counter-move is the settle of a
  real body; a second oscillation is a spring toy. **Two visible oscillations = a defect.**
- **Driver:** a public `Time.time`-anchored `HitReactNormT` (0→1), the `WindupNormT`/`ChargeNormT` shape
  (`BoarAI.cs:138-144`), **inactive → the additive term is exactly identity**. A re-hit inside the envelope
  **restarts** the flinch from its current pose (re-anchor the timestamp; do not add a second concurrent
  offset — stacked offsets are how a 14° head-toss becomes a 40° one on a fast weapon).

### 4.5 The AI contract — the stagger suppresses MOVEMENT, never STATE

AC3's 🔒 (a flinch must not cancel a committed charge) is satisfied **by construction**, not by care:

> **The flinch POSE plays in every state, including `Windup`, `Charge` and `Cooldown` — it is purely additive
> and never touches `BoarAI.State`. The STAGGER is a separate, tier-gated suppression of the agent's ADVANCE
> that also never touches `State`.**

- **Which states the stagger may affect: `Chase` only.** Never `Charge` (the commit is Sponsor-PASSED and
  dodgeable-because-committed; interrupting it makes the telegraph a lie), never `Windup`, never `Dead`, and
  meaninglessly in `Wander`/`Cooldown` (nothing to interrupt).
- **`Windup` is non-interruptible at EVERY tier, including easy** — and this is the deliberate call. A
  cancellable telegraph lets a button-mashing kid prevent the boar from ever charging, so the creature never
  demonstrates its signature and the player never learns the dodge. Quality-bar #9's emergent matchup needs the
  charge to *happen*. The kid-friendly gradient is delivered instead by the Chase-stagger: on easy the boar
  visibly loses ground when you connect, which is the "I'm holding it off" feeling, without disarming it.
- **Per-tier (quality-bar #7):** `enemy_hit_stagger_seconds` — **easy 0.35 / medium 0.15 / hard 0.0**. At hard
  the flinch is pose-only: it keeps coming, exactly as brief §2.5 specifies.
- **Not knockback.** The recoil is **non-directional** — each creature's own local frame (head up / body back /
  tail flick / lateral whip), never a displacement away from the attacker. A directional recoil needs the
  attacker's position (AC1 forbids the coupling), and displacing a `NavMeshAgent`-driven body is *knockback* —
  a different mechanic, with its own AI-contract and charge-commit consequences. **If a directional recoil is
  ever wanted it is a knockback ticket, not an extension of this flinch.**

---

## 5. Q3 — weapon differentiation: YES, but on ONE scalar, and not in the flash

**Answer: an iron spear must read visibly heavier than a wood dagger — and the differentiation must be
EMERGENT from the damage the hit actually did, never from a weapon-tier or damage-type lookup.** That is
quality-bar #9's whole thesis (no hardcoded matchup table), and it makes brief §2.4's promise — *"a pierce hit
on the boar lands with a … bigger flinch than a slash hit"* — fall out for free, because pierce×2.0 on a boar
IS a bigger number.

**One weight scalar, derived from the §2.2 intent value, shared by every channel:**

```
w = sqrt( clamp01( intentEffectiveDamage / (Health.Max * 0.5) ) )     // 0..1
```

Square-root compression is the design choice: it lifts the weak hits up (the anti-false-negative direction —
`dagger_wood` on a medium boar lands at `w ≈ 0.47` rather than 0.23), and it flattens the top so a `spear_iron`
can't run away with the amplitude budget.

| Weapon (on a MEDIUM boar, `Max*0.5 = 20`) | Intent | `w` | flinch × | puff |
|---|---|---|---|---|
| `dagger_wood` 6 slash ×0.75 | 4.5 | 0.4743 | **0.88** | 6 |
| `axe` (stone) 14 slash ×0.75 | 10.5 | 0.72 | **1.08** | 8 |
| `spear_iron` 12 pierce ×2.0 | 24.0 | 1.00 | **1.30** | 9 |

*⚠ **Corrected 2026-08-01** (PR #413 review NIT 3): the `dagger_wood` flinch multiplier read **0.90** in a
draft, which its own formula does not produce — `w = sqrt(4.5/20) = 0.474342` ⇒
`Lerp(0.50, 1.30, 0.474342) = 0.879473` ⇒ **0.88**. **A derived display cell only.** The design value is the
formula, which is unchanged; the ~1.5× spread claim is unaffected (`1.30 / 0.879473 = 1.478×`); the puff count
is unchanged (`round(Lerp(4, 9, 0.474342)) = round(6.37) = 6`). §16.6's on-screen figures are recomputed from
`0.879473`, not from `0.90`.*

- **Flinch multiplier = `Lerp(0.50, 1.30, w)`** → a ~**1.5×** spread across the shipped weapon set.
- **Puff count = `round(Lerp(4, 9, w))`**, hard cap **12** (brief §1.2) → a ~1.5× spread.
- **Flash: NO scaling.** ⚠ **REFINEMENT of brief §2.4's "slightly meatier `_HitFlash`"** — a ≤1.2×
  amplitude delta inside a 5-frame tint pulse on a moving creature at orbit distance is below what a player can
  actually discriminate, so it buys nothing and costs the flash its one virtue: being a **reliable binary**
  the player learns as *"that connected"*. Put the meat in the flinch and the dust, where 1.5× is plainly
  visible. §2.4's *conclusion* (a pierce hit feels like it worked better, wordlessly) is fully delivered.
- **The floor is the point.** At `w = 0` the flinch is still 0.50× and the puff is still 4 particles — the
  weakest hit that clears the §2.1 gate is still unmistakable. This is the body-side mirror of
  `enemy-hp-read-spec.md` §2.3's **living-floor** rule: *never read as nothing while something happened.*
- **Is this over-signalling?** No, because the range is narrow, the mapping is continuous (no thresholds, no
  tiers, nothing that reads as a "crit"), and there is no readout — the player gets a *sense of weight*, not a
  damage meter. **The line it must not cross: if the differentiation ever becomes step-shaped or gains its own
  extra beat (a bigger flash, a second puff, a different colour on a strong hit), it has become a crit system
  and is out of tone.** Forbidden in §9.

---

## 6. Q4 — the kill hit: it gets its own treatment, and the treatment is ABSENCE

**The death animation is not "enough" — but the answer is not escalation.** Today `BoarAI.BoarState.Dead` is
*"motion stops, the body settles"* (`BoarAI.cs:25`, entered `:214`); the rig flattens (`dead ?` branches at
`BoarBodyRig.cs:136,140,154,165,170`) and the snake lies flat (`SnakeBodyChain.cs:249,281`). A topple animation
is OOS. So the settle alone is a *cessation* with no punctuation — the moment the fight ends is the moment with
the least feedback in it, which is backwards.

**The kill's distinctness comes from a change of SHAPE, not of volume:**

| Beat | On a normal hit | On the killing blow |
|---|---|---|
| Flash | fires, decays over 0.08 s | **fires and decays IDENTICALLY** — do NOT zero it on death (a snap back to base colour on the kill frame is a visible pop) |
| Flinch | plays, 0.22 s envelope | **CANCELLED immediately on `Died`** — `HitReactNormT` forced inactive, so the additive term is identity on the next `LateUpdate` |
| Impact puff | fires at the body | **SUPPRESSED** |
| Death puff | — | fires at **`Died` + 0.20 s**, at the **GROUND line**, wider / slower / softer |

- **Why cancel the flinch:** a half-played recoil offset held on a settling body is a twitch on a corpse — the
  single most "broken" thing this feature can render. It also composes cleanly with the rig: gate the flinch
  with the same `dead ?` ternary the rig already applies to breathe/gait/wag.
- **Why the death puff is DELAYED and the impact puff suppressed:** firing both on the same frame is a double
  burst — a *kill burst*, which the ticket forbids outright (tone). Two separated beats read as *"hit … it goes
  down"*; one stacked beat reads as *"it popped"*. Brief §2.5 puts the dust on the **tipping**, not on the hit:
  *"The boar tips over, a soft dust puff."* With no topple in scope, the settle is what the dust must sell — so
  the puff belongs at the **ground contact**, low and spreading outward, which makes the settle read as
  *landing* rather than as the animation simply stopping.
- **Death puff shape:** ≤ **10** particles (`round(Lerp(6, 10, w))`, cap 12), spawned at the creature's
  ground line, **flatter outward** spread (not upward), lifetime ~**0.9 s** vs the impact puff's ~0.4 s, fading
  to a lower peak alpha. Softer, wider, slower — the visual grammar of dust settling, not of an impact.
- **Forbidden on the kill:** no extra/brighter flash, no bigger flinch, no second burst, no corpse recolour
  (no grey-out, no fade-to-dark), no slow-motion, no camera move, no sound (no bus exists). *"It's out," never
  "it's slaughtered"* (brief §2.5, kid-safe).
- **Whole death read ≤ 1.1 s** from the killing blow, and it must be finished before
  `enemy-hp-read-spec.md` §3.3's row (≤ 0.7 s) would have anything to say — the two never fight because the
  pip row is retired first and the body's beat is the last thing on screen. That ordering is intentional: the
  **creature** owns the end of the fight, not a UI plate.

---

## 7. Q5 — zero / blocked damage: no such case exists today, and the forward rule is that it needs its OWN cue

**Verified: there is no blocked, absorbed, or immune outcome on a LIVE enemy in the shipped build.**

- `ResistanceProfile.Multiplier` treats a **zero or negative** authored multiplier as **NEUTRAL (1.0)** —
  *"treat 'unauthored' (≤ 0) as neutral so a missing profile never makes a target immortal"*
  (`ResistanceProfile.cs`, the `Multiplier` body). **A resistance of 0 is unrepresentable**, so damage
  immunity cannot be authored.
- `damageTakenMul` stays **1.0** on both enemies — `BoarEnemy.ApplyDifficulty` / `SnakeEnemy.ApplyDifficulty`
  write `Health.max`, gore and bite only.
- Every one of the 15 `WeaponCatalog` defs has damage > 0 (6 … 21 base).
- There is no block, parry, guard, armour or shield system anywhere in `Runtime/Combat/`.

**So `removed == 0` happens in exactly two situations, and NEITHER may produce body feedback:**

| Case | Path | Correct feedback |
|---|---|---|
| **Whiff / no target** | `PerformAttack` swings, then `if (target == null \|\| target.IsDead) return;` (`MeleeAttack.cs:223`) — no `ApplyDamage`, no `Changed` | **none** — the swing IS the feedback. A whiff that flashes anything makes the flash a lie, and the flash's whole job is to be the hit/whiff discriminator (§3.4). |
| **Strike on a corpse** | same early-return; `ApplyDamage` also returns 0 for `_current <= 0` (`Health.cs:149`) | **none.** No flash, no flinch, no puff. Hitting a dead animal must feel inert — that is the calm returning. |

Both fall out for free: no `Changed` fires, so the shared `Health.Changed` path can't fire. **Nothing extra to
build — but pin both in AC7** (a whiff and a corpse-strike each assert zero fires), because they are exactly the
cases a future refactor breaks silently.

**Forward rule, so nobody reuses this system for the wrong message:**

> **If a block / armour / immune-phase mechanic ever lands, it needs its OWN cue and MUST NOT be rendered as a
> quieter version of the hit.** A dimmed flash + a small flinch is indistinguishable from a weak hit that DID
> land — a false positive by construction, and the mirror of the false negative this whole ticket exists to
> kill. **Zero damage is a different MESSAGE, not a lower AMPLITUDE.** The right shape (out of scope here) is a
> distinct grammar: a hard non-warm tick with no flash, no dust, and a *stopped* pose rather than a recoiling
> one — plus its own `game-juice`-capped beat. File it with the mechanic.

---

## 8. The dust puff — the design side (technique is the ticket's `[DFC-4]`/`[DFC-5]`)

- **Tonal anchor:** *dry earth kicked off the ground by something heavy*, not an impact spark and never a
  wound. In this world dust is the material vocabulary of weight (the board's stumps, logs, paths and rocks are
  all warm earth); a spark or a wisp belongs to a different game.
- **Where it spawns — the creature's own body, NOT a contact point.** AC4 asks for "the contact point", but
  AC1 forbids attacker coupling and the shared `Health.Changed` seam carries no hit position. **Design call:
  spawn at the creature's renderer-bounds centre, biased to ~60 % of its height, with a small deterministic
  jitter** — the same bounds-derived trick `enemy-hp-read-spec.md` §4.1 uses for the head anchor, so enemy #3
  is correct for free. **Why this is not a compromise:** a ≤12-particle chunky burst living ~0.4 s is not
  legible enough for its *origin* to be readable — the player reads *"dust, at the animal"*, never *"dust, 12 cm
  left of centre"*. **Falsifiable:** if the soak says the puff looks detached from the strike, the fix is its
  size and its upward bias — **not** plumbing an attacker vector through the damage seam.
- **Shape:** a short **upward-and-outward cone**, gravity-affected so the chunks arc and fall back. **Never
  radially symmetric** — a symmetric ring reads as an explosion. Chunky faceted quads/chunks, not soft wisps
  (`game-juice.md` §1.4, `lowpoly-quality.md`).
- **Colour — a PROPOSAL for the QA pin, not an existing anchor:** pale warm earth **`#B39472` (0.70, 0.58,
  0.45)**, derived by lightening the world's warm-bark hue family (`0.42, 0.30, 0.19` at
  `MovementCameraScene.cs:2164`) two steps so it reads as *airborne dust catching light* rather than as a chunk
  of the animal. Every channel sub-1.0. **Never red, never pink, never a `BerryRed`-adjacent tone** — red on a
  creature is gore and breaks the kid-safe tone (brief §2.5, `game-juice.md` §0). It must also stay lighter
  than every creature tone in §2.3 so it never reads as a piece coming off the body. **Sponsor-input item.**
- **Counts + timing:** impact `round(Lerp(4, 9, w))`, lifetime ~0.35–0.45 s; death `round(Lerp(6, 10, w))` at
  `Died + 0.20 s`, lifetime ~0.9 s, flatter and lower (§6). Hard cap **12** either way.
- **Primitive discipline (Unity translation, brief §4):** a **separate particle material** — never the world
  palette material, never an MPB on a world `MeshRenderer`. Particles are explicitly exempt from the MPB
  disqualifier (`game-juice.md` §2) because `ParticleSystemRenderer` is not that path. All build-side details
  (`Universal Render Pipeline/Particles/Unlit`, `AlwaysIncludedShaders`, `stopAction = Callback`, the
  Editor-asmdef mesh route) are the ticket's `[DFC-4]`/`[DFC-5]` — **not re-specified here.**

---

## 9. Amplitude budget — allowed, with caps; and forbidden, with reasons

| Beat | Spec | Cap (HARD) |
|---|---|---|
| Flash | warm multiplicative lift, ceiling 0.92, instant attack, eased-out release | **0.08 s** (dial ≤ 0.14 s) |
| Flinch — boar | head −14° / body −5° / tail +10°, legs untouched | envelope **≤ 0.22 s**, ≤1 overshoot ≤15 % |
| Flinch — snake | lateral 0.09 u seg-0, `k²` taper over 4 links, head yaw ±12° | same envelope |
| Stagger (easy/med only) | agent advance suppressed in `Chase` | **0.35 / 0.15 / 0.0 s** |
| Impact puff | 4–9 particles, upward cone, gravity | **≤12**, ~0.45 s |
| Death puff | 6–10 particles at the ground line, `Died + 0.20 s` | **≤12**, ~0.9 s |
| Whole normal-hit event | flash + flinch + puff | **≤ 0.25 s** |
| Whole death read | flash decay + settle + death puff | **≤ 1.1 s** |

**Forbidden — each with its reason:**

| Forbidden | Why |
|---|---|
| **Red / pink / crimson anything** on flash or dust | gore; breaks kid-safe tone (`game-juice.md` §0, brief §2.5) |
| **Any channel ≥ 1.0** | HDR clamp + bloom blow-out (`style-guide-v2.md` §5; Bloom threshold 1.02, §2.4) |
| **A flat-cream lerp that flattens the vertex tones** | erases the boar's eyes and tusks (§2.3) |
| **Post-lighting flash as the primary term** | flattens the facets — the low-poly identity's opposite (§3.1) |
| **Squash / stretch / any scale term on either enemy** | `game-juice.md` §2; reads as cartoon slapstick on a hurt animal (§4) |
| **Two or more visible flinch oscillations** | sustained wobble (`game-juice.md` §2); a spring toy, not a body (§4.4) |
| **A flinch along the creature's telegraph axis** | fakes a charge/strike tell; regresses quality-bar #9 (§4.1) |
| **Directional recoil / displacement** | that is knockback — a different mechanic, and it needs the attacker (§4.5) |
| **Any change to `BoarAI.State` / `SnakeAI.State`** | AC3's 🔒; the charge commit is Sponsor-PASSED (§4.5) |
| **Interrupting `Windup` at ANY tier** | a cancellable telegraph teaches nothing and can be mashed away (§4.5) |
| **Leg terms during the flinch** | four legs together = a collapse = death read (§4.2) |
| **Writing the root / `bodyOrigin`** | desyncs the visual from the agent (`BoarBodyRig.cs:19-26`) (§4.2) |
| **Hit-stop, `Time.timeScale`, Cinemachine Impulse, camera shake** | ticket OOS; `game-juice.md` §2; three runtime files record *"the game never scales `Time.timeScale`"* |
| **Step-shaped / crit-flavoured damage differentiation** | becomes a crit system; out of tone (§5) |
| **A kill flourish** — brighter flash, second burst, corpse recolour, slow-mo | "it's out," never "it's slaughtered" (§6) |
| **Feedback on a whiff or on a corpse** | destroys the flash's hit/whiff discrimination (§7) |
| **Zero-damage rendered as a dim hit** | false positive by construction (§7 forward rule) |
| **Numbers, damage popups, kill counters, XP, a combat log** | forbidden not deferred (ticket; quality-bar #9) |
| **Any audio** | no bus exists — `<deferred — no audio bus>`; do not attempt the "thunk" |

---

## 10. Dials + per-tier registration (AC5) — six ids, exactly the ticket's list

| Id | Drives | Default | Per-tier? |
|---|---|---|---|
| `enemy_hit_feedback_enabled` | master off switch — the one-flag revert path | **on** | No — global |
| `enemy_hit_flash_amp` | §3.1 lift amplitude (shader default **0**; the dial supplies the live value) | 1.0 | No |
| `enemy_hit_flash_seconds` | §3.2 duration | **0.08** | No |
| `enemy_hit_flinch_amp` | §4.2/§4.3 scale on the per-part peaks | 1.0 | No |
| `enemy_hit_stagger_seconds` | §4.5 Chase-advance suppression | **0.35 / 0.15 / 0.0** | **YES** |
| `enemy_hit_puff_count` | §8 base burst count (the §5 weight curve scales it; cap 12) | 6 | No |

**Only the stagger is per-tier** — the same principle as `enemy-hp-read-spec.md` §6: *difficulty changes the
generosity of TIME, never the read.* A kid on easy and an adult on hard must learn the same vocabulary, and the
tier is live-switchable, so a per-tier *look* would relabel the game mid-session. Quality-bar #7 asks every
system for three tiers; it does not ask them to look different.

**Registration:** this feature's **own** `Populate…` on `SettingsCatalog` — never grow the base `Populate`
signature (`PopulateThirst`/`PopulateChop`/`PopulateCombat`/`PopulateBoar`/`PopulateIron` precedent). The
per-tier dial must write **both** the active field **and** the active tier's map entry or `ApplyDifficulty`
clobbers it (the documented **dead-knob** class). **DEV-console rows (F3 class), never player Settings rows**
(DECISIONS 2026-07-01 split). Any new binding Danish-layout-agnostic (`[[sponsor-danish-keyboard-layout]]`).

**Deliberately CONSTS, not ids** (structural; the Sponsor has no reason to dial them):
magnitude gate `0.02`, refractory `0.12 s`, weight reference `0.5 × Max`, the sqrt compression, flash ceiling
`0.92`, the gain vector, flinch attack `0.04 s` / release `0.18 s` / overshoot `0.15`, snake `flinchLinks = 4`
and its per-link lag, death-puff delay `0.20 s`, both puff lifetimes.

---

## 11. Predict-Before-Soak — what this spec ADDS to AC6

The ticket's AC6(a) template stands. **Add two clauses, because each is the falsifier for one of §2's
findings** — without them the soak can pass while the defect is live:

> **(i) Bleed clause.** *"After I hit the boar with the axe and STOP attacking, the reaction is ONE beat. The
> boar does not keep flashing, twitching, or puffing dust for the next three seconds while the bleed ticks."*
> **(ii) Kill-weight clause.** *"The killing blow reads as heavy as the weapon deserves — a spear thrust that
> finishes a nearly-dead boar looks like a spear thrust, not like the weakest tap of the fight."*

> ⚠ **Misdiagnosis guard for `[DFC-1]`.** AC6(d)'s *hit-then-wait* check has **two** possible causes of a
> still-tinted enemy: the `Time.time`/`_Time.y` latch, and a bleed re-firing the flash every frame (§2.1).
> **Before concluding the clock is wrong, check whether an axe bleed is running** (hit with a `spear`, which
> carries no bleed — `WeaponCatalog` applies bleed only to the axe class — and re-run the wait). Two very
> different fixes; one symptom.

**Bounded convergence claim** — bars **tested**: **#2** (eased, lively, never linear and never dead — §3.2,
§4.4), **#7** (three tiers via the stagger — §4.5), **#9** (the telegraph read must NOT regress — §4.1's
orthogonal-axis rule and §4.5's non-interruptible `Windup` exist for this), **#10** — *sharpened by §16, and
the sharpening narrows the claim rather than widening it*: **desaturate** and the **hue-independent-channel**
clause are satisfied by construction (§3.1's multiply, §16.3a); **C1 magnitudes are stated and two of them do
not clear two of the bar's four candidate floors** (§16.4a boar flinch 1.7085–3.1776 px, §16.6 weight
0.7615–1.3276 px); **C2 returns ONE failure domain for the body alone** (§16.5); **C3 is a naming obligation
with no consumer** and **C4 is unbuilt project-wide**, so neither is coverage. Bars **NOT tested**: **#1**,
**#3**, **#4**, **#5**, **#6**, **#8** — no world, weapon-material, real-world-feature, in-hand-sizing or
nudge-tool surface is touched.

> ⚠ **What the soak CANNOT converge, stated so the PASS is not over-read.** A Sponsor PASS on this soak
> settles *feel* — it does **not** settle §16.5's C2 verdict (an architectural property no amount of looking
> reveals), nor the C1 floor (unset project-wide), nor C4 (unbuilt). **A soak PASS plus a one-domain C2 count
> are not in conflict and neither overrides the other**; conflating them is how "it looked great" becomes a
> claimed bar clearance.

**AC6(c) — the `86caxhfg2` judgement item.** I am **not** pre-answering it (the ticket forbids that, and it is
genuinely open). What I can do is hand the Sponsor the question cleanly separated from the pass/fail:

> **Ask it as its own question, after he has fought both creatures:** *"With the creature's own flash, flinch,
> dust and death-settle in front of you — is **'is it nearly down?'** already answered, or do you still want a
> small five-block chip above its head?"*
> **Two follow-ups that make either answer actionable:** *"Could you tell a spear hit from a dagger hit without
> looking at anything above its head?"* (grades §5's weight channel) and *"Did you ever feel a hit did nothing?"*
> (grades §2.1's gate + §5's floor).
> **Evidence either way is already on the table:** `enemy-hp-read-spec.md` §2.3 shows a low-damage weapon moves
> no pip on ~44 %/~55 % of hits, so if the body answers *both* questions the pip row is genuinely redundant —
> and if it does not, that arithmetic is why the row exists. **Both outcomes are clean.**

---

## 12. Success-tests this spec ADDS to AC7 (the ticket's list stands; these are new)

- 🔴 **Bleed does not strobe.** Land one strike, then drive `StatusEffectController.TickSeconds(1/60f)` 180×
  (3 s of bleed) and assert the feedback fired **exactly once** — from the strike, never from a tick. *(The
  single most valuable new test in this spec; §2.1.)*
- **Refractory.** Two qualifying deltas 0.05 s apart → **one** fire.
- **Gate boundary.** A delta at **1.9 %** of `Max` → no fire; at **2.1 %** → fire.
- 🔴 **Kill weight uses INTENT, not `removed`.** A `spear_iron`-intent hit on a **1 HP** boar produces the same
  weight scalar as on a full-HP boar, and **clears the magnitude gate** (§2.2 — on a hard boar `removed = 1.0`
  is exactly 2.0 % of `Max`, i.e. on the boundary; the intent-driven gate must pass it unambiguously).
- **Intra-material contrast preserved (§2.3/§3.1).** At full flash, on the boar HEAD material: the tusk-vertex
  result is strictly brighter than the body-vertex result, and the eye-vertex result strictly darker than both.
- **Ceiling guard.** A vertex whose channel already exceeds the 0.92 ceiling is **not darkened** at full flash.
- **Flinch is identity at rest.** With `HitReactNormT` inactive, every boar part transform and every snake
  segment transform is bit-identical to the no-feature build.
- **Flinch never changes State.** At **every** tier, during `Windup` and during `Charge`, on both enemies.
- **Stagger suppresses movement, not state.** Easy + `Chase`: advance suppressed for `staggerSeconds`,
  `State == Chase` throughout. `Windup` is unaffected at every tier.
- **Flinch does not stack.** Two hits inside the 0.22 s envelope produce a peak offset ≤ the single-hit peak.
- **Death cancels the flinch but NOT the flash.** On `Died`: the flinch term is identity on the next
  `LateUpdate`, while the flash factor is still > 0 and continues to decay.
- **One puff on a kill, not two.** The killing blow fires the death puff (delayed) and **no** impact puff.
- **A whiff and a corpse-strike each fire nothing** (§7) — three channels, zero fires.
- **Cross-spec (only if `86caxhfg2` ships):** with a body flash active, `enemy-hp-read-spec.md` §3.2's
  lost-pip extinguish flash is **suppressed**; with a bleed-driven pip loss and no body flash, it **fires**
  (§1.3).

**Added by §16 (the bar-#10 audit):**

- 🔴 **C2 injection — the resource enumeration, run as a test, not asserted.** Null the material array ⇒ only
  the flash stops; null `BoarBodyRig.parts` ⇒ only the flinch stops; null the pooled `ParticleSystem` ⇒ only
  the dust stops. **All three must pass** — they are the evidence for the "3 at leaf granularity" half of
  §16.5, and §15.3(iii) predicted them. **Separately assert the tie-breaker half:** suppress the gated
  `Health.Changed` dispatch ⇒ **all three** stop together. *Both results are real; §16.5 explains why they
  disagree and which one this spec adopts. The test records both rather than only the flattering one.*
- **Flinch delta ≠ flinch extent (the C1 trap, §16.3b).** With the tail term active, assert the **difference**
  between a flinched and an unflinched boar's tail yaw at the same `Time.time` is the flick's own 10°, and
  **not** the 26° the same-instance extremes would show. Pins the one live absolute-≠-delta instance so a
  future capture-based measurement cannot quietly claim 6.14 px.
- 🔴 **Both creatures KEEP their death-path poll backstop (§16.7).** With the `Health.Died` subscription
  removed, assert `BoarAI` **and** `SnakeAI` each still enter their dead state from the per-frame poll
  (`SyncDeathState()`). **Both have it today — measured, not assumed:** `BoarAI.cs:204`/`:235` and
  `SnakeAI.cs:206`/`:237` at `fb2ac24`. **Those lines ARE the death cue's second failure domain**: delete
  either one as "dead code" and that creature's Cue C drops from 2 domains to 1 with nothing else changing and
  nothing reporting it. *(A 2026-08-01 draft of this test read "discover whether the snake has a backstop" and
  was labelled `Hypothesis, unverified`. It has one — §16.7 — so the test's job is now to KEEP it, which is
  the more valuable job of the two.)*
- **Puff chunk size is a named constant with a stated value, not an inline literal.** Whichever of §16.4c's
  options ships, assert the value is reachable from one symbol — the audit above is recomputable only if the
  number has a name.

**Shipped-build capture** — the ticket's (a)…(f) list stands. **Add (g):** the boar **~1.5 s after a single
axe hit, with the bleed still ticking**, showing base colour and a still body — the §2.1 discriminator, and the
only capture that can fail on the strobe. **Add (h) [§16.9]:** capture **(a)** re-framed so the **snake is in
shot alongside the struck boar** — that frame IS C4's `cue_pair.png` for this cue, and (a) as the ticket words
it does not require the second creature to be visible.

---

## 13. Sponsor-input items (NONE block implementation)

- **Q1 — the flash as a warm LIFT rather than a warm-white TINT (§3.1).** The mechanical case is settled (a
  flat-cream lerp erases the eyes and tusks), but *how bright is right* is taste. `needs-soak`.
- **Q2 — the boar's head-UP toss (§4.2).** Does it read as *"it felt that"* and never as a charge tell?
- **Q3 — weight in the flinch + dust only, flash uniform (§5).** Does a spear read heavier than a dagger
  without the flash helping? If not, the dial is the flinch range, not the flash.
- **Q4 — the kill as ABSENCE (§6).** Does "flash, then the flinch stops and dust settles at the ground" land
  as *"it's out"*, or does it feel like the game forgot to react?
- **Q5 — the snake's lateral whip (§4.3).** A 13-segment ground-level body at orbit distance: does the recoil
  read at all, or does the snake need a bigger share of the amplitude budget than the boar?
- **Q6 — the dust colour pin `#B39472` (§8).** A proposal, not an existing anchor — QA pin needed.
- **Q7 — easy-tier stagger 0.35 s (§4.5).** *"I'm holding it off"* or *"the boar is broken"*?
- **Q8 — `Windup` non-interruptible even on easy (§4.5).** Deliberate, and the one place I chose legibility
  over kid-forgiveness. Confirm or correct.
- **Q9 — the dust chunk SIZE (§16.4c).** A value that never existed in §8, now priced as three options at the
  default framing (55° / 14 u / 45° FOV / 1280×720). Each chunk's on-screen extent is quoted as the **pair**
  `world-vertical – frame-plane`, because a 3-D chunk's own extent lies between them:
  - **A — 0.05 u** (the house scale): **1.78 – 3.10 px**; **5.6 %** of the boar's on-screen height, **21.7 %**
    of the snake's. Under 4 px on *both* readings ⇒ reads as speckle at 14 u if the C1 floor lands at 4 px.
  - **B — 0.08 u**: **2.85 – 4.97 px**; **8.9 % / 34.8 %**. Clears 4 px in the frame plane, **fails it** on the
    conservative vertical reading.
  - **C — 0.12 u**: **4.27 – 7.45 px**; **13.3 % / 52.2 %**. The only option clearing 4 px on **both** readings;
    clears 6.2080 px only in the frame plane.

  **No option is eliminated and no recommendation is made** — it is a look call on an element nobody has seen
  rendered. ⚠ **A 2026-08-01 draft of this item called B *"the only option"*. That is WITHDRAWN** — it rested
  on a table that mixed measurement planes (§16.4c's correction notice; every %-of-height figure was inflated
  by 1.7434×). Under a consistent reading the *"under a quarter of the boar"* clause eliminates **nothing**
  (largest option = 13.3 %), and the 4 px clause favours **C**, not B. **The one thing no option fixes:** a
  chunk is **3.91×** as large a fraction of the snake as of the boar *whatever* size ships (`0.90 / 0.23`,
  invariant in `s`) — so if that disproportion matters, the answer is a bounds-derived scale (§16.4c), not a
  smaller number here. `needs-soak`, and cheap to bake as a discrete PICKER rather than a slider
  (`[[verify-soak-builds-or-bake-and-judge]]`).
- **Q10 — the price of the AC6(c) "yes, close the pip row" branch (§16.8).** Not a taste question and **not a
  re-ask of AC6(c)** — AC6(c) stays exactly as the ticket words it and stays his to answer at the soak. This
  is the fact he should have in hand when he answers: `enemy-hp-read-spec.md` §15.4 told him **both** answers
  were bar-#10-legal, and §16.5 shows that was wrong. *"Still want the row"* is legal as-is; *"close it"*
  leaves the game's enemy-damage cue single-failure-domain and **costs one follow-up ticket** (a second
  independent trigger path for one body channel). **Both outcomes remain clean; one is no longer free.**

---

## 14. Out of scope

Implementing any of this (spec-only PR). **The pip row itself** (`86caxhfg2` /
[`enemy-hp-read-spec.md`](enemy-hp-read-spec.md) — cited throughout, absorbed nowhere; §1.3's amendment is one
line and applies only if that ticket ships). **Re-deriving Erik's technique research** — the HLSL/particle/pool
mechanics are his note + the ticket's `[DFC-1…5]`/`[DFC-B]`, not restated here. Hit-stop / `Time.timeScale` /
Cinemachine Impulse (ticket OOS). **All audio** — no bus exists. A death topple animation or any death
re-posing beyond the existing settle (the death **dust** is in scope; the **choreography** is not). Knockback /
directional displacement (§4.5 — its own ticket if ever wanted). Blocked / armour / immune feedback (§7 —
forward rule only). Lootable-corpse drops. Re-balancing enemy HP, damage or resistances — including the snake's
flat 24 HP across all tiers (`SnakeEnemy.cs:32`), **logged not fixed**, balance lane. Player-side damage
feedback (`86cah7z2q`) and status-effect world cues (`86cah7yuh`) — but **do not fork a second pool**; whichever
lands second extends this one. Editing Erik's note or `team/DECISIONS.md` (§15 drafts are for Priya's batch).

---

## 15. Decision drafts (for Priya's DECISIONS.md batch — I do not edit that file)

- **Decision draft:** The enemy body read and the above-head pip row divide the labour as **body = "that
  landed, and this hard" (analog, instantaneous, ON the creature) / pip row = "it's this close to down"
  (quantized, cumulative, ABOVE it)**. The body is load-bearing precisely where the pip row is silent — a
  low-damage weapon moves no pip on ~44 % (medium boar) / ~55 % (hard) of landed hits. Precedence is
  one-directional: the pip row always yields to the body, never the reverse.
  (`enemy-hit-feedback-spec.md` §1.)
- **Decision draft (amends a pending spec):** `enemy-hp-read-spec.md` §3.2's **lost-pip extinguish flash fires
  only when no body flash fired within the flash duration**, making it a **bleed/DoT-only accent**. A strike
  always brings a body flash, and the pip's information is the count change — the extinguish flash there would
  duplicate the body's "something happened" with no added information. (`enemy-hit-feedback-spec.md` §1.3.)
- **Decision draft (🔴 corrects the ticket's premise):** **Bleed calls `Health.ApplyDamage` EVERY FRAME**
  (`StatusEffectController.cs:55-61`, `:99`), so a literal *"fire on a `Health.Changed` damage delta"* strobes
  the flash, flinch and dust puff for the full 3 s bleed (axe 2 dps / iron axe 3 dps,
  `WeaponCatalog.cs:65-66/119-120`) — a permanently-lit enemy indistinguishable from the `[DFC-1]` latch bug, a
  vibrating body, and ~60 particle bursts/second through the project's brand-new pool. Gated by **(1) a
  magnitude gate at 2.0 % of `Health.Max`** — ~9.5× above the worst 60 fps bleed tick (0.05 HP) and ~4.5× below
  the weakest shipped strike (`dagger_wood` 4.5 HP = 9.0 % on a hard boar) — **and (2) a 0.12 s refractory
  window**, because a bleed tick's amount is `dps × dt` and so the gate alone is frame-rate-sensitive. Both are
  attacker-free, so AC1's shared-`Health.Changed` path is honoured verbatim.
  (`enemy-hit-feedback-spec.md` §2.1.)
- **Decision draft (🔴 corrects any damage-proportional design):** `Health.ApplyDamage` returns
  **clamped** `removed` (`Health.cs:157`, `SetCurrent` clamps at `:185`), so a weight driven off it makes the
  **killing blow the quietest hit of the fight** — and on a hard boar `removed = 1.0` is exactly 2.0 % of `Max`,
  i.e. on the magnitude gate, so the kill could produce no feedback at all. The weight and the gate must both
  read the **pre-clamp intent** (`effective`, `Health.cs:151`), recorded on `Health` as a public read-only
  value; recomputing it attacker-side is rejected (AC1 coupling + blind to non-weapon damage).
  (`enemy-hit-feedback-spec.md` §2.2.)
- **Decision draft (refines `combat-cluster-design-brief.md` §1.2):** the flash is a **warm-biased
  multiplicative EXPOSURE LIFT with a sub-1.0 ceiling (0.92)**, applied to **albedo BEFORE lighting** (the
  shipped `_MeadowPatchAmp` idiom, `LowPolyVertexColor.shader:247-252`) — **not** a lerp toward flat warm-white.
  Reason: the boar's ivory tusk (0.90,0.88,0.78) and near-black eye (0.06,0.05,0.04) are per-VERTEX colours
  inside the SAME head mesh and material (`MovementCameraScene.cs:2564-2565`, `:2814`), so a flat-cream lerp at
  a readable amplitude erases both — the identity features AND the quality-bar-#9 tusk read. A multiply
  preserves the tone order; the luma step stays ~1.76× (boar) / ~1.52× (snake), i.e. hue-independent and
  desaturation-proof (bar #10). At amplitude 0 the expression is bit-identical to today, so the shared shader's
  no-op-at-default proof is unchanged. (`enemy-hit-feedback-spec.md` §2.3 / §3.1.)
- **Decision draft:** **Flash amplitude is distance-invariant and does NOT scale with damage.** It is the
  binary *"connected"* channel. If it fails to read at orbit distance the lever is **DURATION** (0.08 → ≤0.14 s),
  never amplitude — amplitude is already bounded by the tusk-contrast requirement and the sub-1.0 ceiling, and a
  distance- or damage-varying flash destroys the one thing that makes it learnable. Attack is **zero frames**
  (peak on the impact frame); *"eased out"* means the RELEASE is eased, never ease-in-out.
  (`enemy-hit-feedback-spec.md` §3.)
- **Decision draft:** **The flinch occupies an axis ORTHOGONAL to that creature's telegraph** — boar: head
  **UP** / body **BACK** against the shipped head-down `+34°` gore tell and `+12°` charge lean; snake:
  **LATERAL** whip against the shipped **VERTICAL** `0.32u` rear. A same-axis flinch would fake a telegraph and
  regress the Sponsor-PASSED charge read (bar #9). Peaks are calibrated as fractions of already-approved
  amplitudes on the same rig (head 41 % of `headLowerDeg`, body 42 % of `chargeLeanDeg`, tail 1.25× the idle
  wag, snake lateral 1.6× `slitherAmplitude`). Rotation + small per-part offsets only — no scale, no root
  write, no leg terms (four legs together reads as a collapse). Envelope ≤ 0.22 s with **exactly one** ≤15 %
  counter-overshoot — the reconciliation of bar #2 (never dead) with `game-juice.md` §2 (never a sustained
  wobble). (`enemy-hit-feedback-spec.md` §4.)
- **Decision draft:** **The tier stagger suppresses the agent's ADVANCE, never `State`** — so AC3's
  "must not cancel a committed charge" holds by construction. It applies in **`Chase` only** (easy 0.35 /
  medium 0.15 / hard 0.0 s); **`Windup` is non-interruptible at EVERY tier including easy**, because a
  cancellable telegraph can be mashed away and then the boar never demonstrates the charge the player is
  supposed to learn to dodge (bar #9). The recoil is **non-directional** — a hitch, not knockback; a
  directional recoil needs the attacker (AC1) and displacing a `NavMeshAgent` body is a separate mechanic.
  (`enemy-hit-feedback-spec.md` §4.5.)
- **Decision draft:** **Weapon weight is EMERGENT from one scalar, and it is not in the flash.**
  `w = sqrt(clamp01(intent / (Max × 0.5)))` drives the **flinch** (`Lerp(0.50, 1.30, w)`) and the **puff count**
  (`Lerp(4, 9, w)`) — a ~1.5× spread across the shipped weapon set — with **no weapon-tier or damage-type
  lookup** (bar #9), which delivers brief §2.4's "a pierce hit lands meatier" for free because pierce×2.0 IS a
  bigger number. **The flash gets no scaling** (refines brief §2.4): a ≤1.2× tint delta inside 5 frames on a
  moving creature is below discrimination and costs the flash its role as a reliable binary. A **floor**
  (0.50× flinch / 4 particles) mirrors the pip row's living-floor rule. Step-shaped or extra-beat
  differentiation is forbidden — that is a crit system. (`enemy-hit-feedback-spec.md` §5.)
- **Decision draft:** **The kill hit's treatment is a change of SHAPE, not of volume — it is ABSENCE.** The
  flash plays and decays identically (never zeroed on death — a snap to base colour is a pop); the **flinch is
  cancelled** on `Died` (a half-played recoil held on a settling body is a twitch on a corpse); the impact puff
  is **suppressed** and replaced by a **softer, wider, slower death puff at the GROUND line, delayed 0.20 s**,
  so the beats read *"hit … it goes down"* rather than one kill-burst (forbidden). Brief §2.5 puts the dust on
  the tipping, and with a topple out of scope the ground-line puff is what makes the settle read as landing.
  No brighter flash, no corpse recolour, no slow-mo, whole death read ≤ 1.1 s.
  (`enemy-hit-feedback-spec.md` §6.)
- **Decision draft:** **No blocked / absorbed / immune outcome exists on a live enemy** —
  `ResistanceProfile.Multiplier` maps a ≤0 multiplier to NEUTRAL 1.0 (so immunity is unrepresentable), enemy
  `damageTakenMul` stays 1.0, all 15 weapon defs deal > 0, and there is no block/parry/armour system. The only
  zero-damage cases are a **whiff** and a **strike on a corpse**, and both must produce **zero** body feedback
  (the swing is the whiff's feedback; hitting a dead animal must feel inert). Forward rule: if a block/armour
  mechanic ever lands it needs its **own** cue — **zero damage is a different MESSAGE, not a lower
  AMPLITUDE**; a dimmed flash is indistinguishable from a weak hit that did land.
  (`enemy-hit-feedback-spec.md` §7.)
- **Decision draft:** **The dust puff spawns at the creature's renderer-bounds centre (~60 % height), not at a
  contact point** — the shared `Health.Changed` seam carries no hit position and AC1 forbids attacker coupling,
  and a ≤12-particle 0.4 s burst is not legible enough for its origin to be readable anyway (the player reads
  "dust, at the animal"). Enemy #3 is correct for free. Shape is a short upward-and-outward gravity-affected
  cone, **never radially symmetric** (a ring reads as an explosion). Proposed colour pin: pale warm earth
  **`#B39472`**, lighter than every creature tone so it never reads as a piece coming off the body; never red.
  (`enemy-hit-feedback-spec.md` §8.)

**Decision drafts ADDED by the 2026-08-01 revision (§16 — the bar-#10 audit):**

- **Decision draft (🔴 withdraws a claim in a pending sibling spec):** **The enemy BODY read is a ONE
  failure-domain cue, not the three `enemy-hp-read-spec.md` §15.4 credits it with.** All three channels
  (flash / flinch / dust) fire from the single gated `Health.Changed` dispatch that **AC1 mandates** (*"one
  shared path for every enemy"*) plus §2.1's magnitude gate and refractory — literally C2's own enumerated
  *"one early return in one `Update` guarding both"* shared-domain form. §15.4's table named the material,
  the `Transform[]` and the `ParticleSystem`: **leaf properties, the granularity C2's tie-breaker exists to
  forbid.** §15.4's *verdict* (the composed pip-row + body cue meets ≥2) **survives** — but on the two
  elements' **different triggers** (pip row ARMS from the strike seam, body fires from `Health.Changed`), at
  **exactly 2**, not "with margin". §15.4's further claim that **"body-read-only-forever is a bar-#10-legal
  outcome" is WITHDRAWN.** (`enemy-hit-feedback-spec.md` §16.5 / §16.8.)
- **Decision draft (a live constraint on `86caxhfg2`, not an observation):** **the pip row must keep its
  STRIKE-armed trigger** (`enemy-hp-read-spec.md` §3.1). It is the only thing separating its failure domain
  from the body's, so re-plumbing ARM onto `Health.Changed` "for simplicity" would collapse the composed
  enemy-damage cue from 2 domains to 1 — a bar-#10 failure that would look like a harmless refactor in review.
  (`enemy-hit-feedback-spec.md` §16.8.)
- **Decision draft (🔴 corrects a granularity inconsistency in a pending sibling spec — REPLACES a draft that
  called this a bar gap):** **`enemy-hp-read-spec.md` §15.4 applied TWO granularities in ONE table**, and that,
  not any silence in bar #10, is why C2 appeared to return two answers on the body. At `ad8a8bd` its
  composed-cue table enters the pip row at **dependency** granularity (*"the row record / resolve predicate /
  `enemy_hp_pips_enabled`"* ⇒ **1**, per its own §15.3) and the body's three channels at **leaf** granularity
  (*"the material instance + the shader property"* / *"the part `Transform[]`"* / *"the pooled
  `ParticleSystem`"* ⇒ **3**). Applied evenly, the table returns **1** for the body. **Bar #10 is not silent
  here:** C2's enumerated shared-domain forms already include *"one early return in one `Update` guarding
  both"* — the body's exact shape — and C2's injection clause is a **confirmation step for the tie-breaker**
  (*"null the **named dependency**, assert **both** channels stop"*), not a rival resource-enumeration
  procedure. **The enumeration is `enemy-hp-read-spec.md` §15.3(ii)'s own construction, attributed to C2 but
  not defined by it.** ⚠ **A 2026-08-01 draft of this decision framed the divergence as *"a gap in bar #10 the
  bar is silent on"*. WITHDRAWN as under-stated** (PR #413 review §2) — it made the finding weaker than the
  evidence supports and invited an escalation the bar does not need. **What remains open is a smaller, honest
  question, and it does not gate this verdict:** does bar #10 *want* a resource-enumeration procedure added?
  That would be an **amendment** to C2, not a clarification. `/name-the-bar` candidate on that narrow question
  only. (`enemy-hit-feedback-spec.md` §16.5.)
- **Decision draft (proposes a bar-#10 addition):** **bar #10's framing table needs a third scale row for
  world-HORIZONTAL displacement — `62.0798 × sin 55° = 50.8528 px/m`.** The table carries frame-plane
  (62.0798) and world-vertical (35.6075) only, which is sufficient for a screen-space HUD surface and
  insufficient for any world-space cue. The consequence inverts the intuition: **at pitch 55 a VERTICAL
  displacement is the most foreshortened thing on screen**, and a horizontal one never falls below 1.43× it —
  so a head-toss is the most expensive gesture per degree this camera can be shown and a tail flick the
  cheapest. Also proposed: **C4 needs a TEMPORAL pair for the impact-cue class** (cued frame vs the same
  instance pre-cue), because C3/C4 are built around a spatial one-frame pair and an impact cue's real
  comparison is temporal. (`enemy-hit-feedback-spec.md` §16.2 / §16.9.)
- **Decision draft (🔴 withdraws a visibility claim in THIS spec):** **§5's *"1.5× is plainly visible"* is
  withdrawn for the boar's flinch.** The amplitude spread across the shipped weapon set is real, but the
  on-screen delta between `dagger_wood` and `spear_iron` is **0.7615 px** (vertical reading) / **1.3276 px**
  (frame-plane) on the head and **0.8173–0.9978 px** on the tail — under three of the bar's four candidate
  floors. §5's DESIGN (emergent-from-damage weight, sqrt compression, the floor, no scaling on the flash) is
  unchanged and rests on bar #9, not on px. The dial that answers it if the soak agrees is
  **`enemy_hit_flinch_amp` or the puff count — never the flash.** (`enemy-hit-feedback-spec.md` §16.6.)
- **Decision draft:** **§6's *"the kill's treatment is ABSENCE"* is ratified by bar #10, not merely by tone.**
  Absence-of-motion (gait / wag / slither ceasing on `dead`) is a legitimate varying MOTION-axis channel, and
  it is what gives the death cue its second axis over the FORM that settle + death-puff share. A kill
  *flourish* — forbidden in §9 on tone grounds — would have added FORM on top of FORM and bought no channel.
  (`enemy-hit-feedback-spec.md` §16.7.)

---

## 16. Bar #10 audit — the body read measured as a CHANNEL (added 2026-08-01)

**What this audits against.** `team/quality-bars.md` § *"Bar 10 — the standard in full, and the four checks
(`86caz5na6` + `86cazhjw4`, 2026-07-31)"*. **All source below was read at
`fb2ac245fc419d442a474c5d2f970535fa884743`.** That SHA is one commit ahead of this branch's base (`39ee4e6`);
the intervening commit (PR #387, a build-concurrency spike) touches
`.claude/docs/unity-conventions.md`, `team/spikes/`, `tools/debug/` and **no file under `Assets/`**, verified by
`git diff --name-only 39ee4e6 fb2ac24`. So every constant quoted here is identical on both. **Pinning a SHA
rather than `origin/main` is not ceremony** — `origin/main` moved *during* this audit, and a figure attributed
to a moving ref cannot be recomputed by a reviewer.

**Why this section exists at all.** The ticket never asked for it. `enemy-hp-read-spec.md` §15 (PR #406) found
the pip row is a **one-channel** cue and credited the body with **three independent failure domains**, on which
its verdict *"the composed cue passes ≥2 with margin"* rests. That credit was written about **unimplemented
code**, from this spec, and §15.3(iii) labelled it **"⚠ OWED, NOT DELIVERED"**. §16 pays it — and finds the
credit was wrong. Auditing my own sibling spec's load-bearing claim and reporting that it fails is the whole
point; a §16 that ratified §15 would have been worth nothing.

### 16.1 Name the CUES first — the body answers THREE questions, and they do not share a channel set

§15.1's error was counting channels without naming the cue. Same discipline here, and the body is worse: it
carries **three** questions where the pip row carried two.

| Cue | The player's question | The non-cued comparison (C3) | Where it is judged |
|---|---|---|---|
| **A — CONNECTION** | *"Did that land?"* | a creature **not** being hit, same frame | AC6(a); capture (a) |
| **B — WEIGHT** | *"How hard did that land?"* | **a lighter hit** on the same creature | AC6 §11(ii); §13 Q3 |
| **C — DEATH** | *"Is it down?"* | a **live** creature | AC6(a); capture (d) |

**C3 step 1 is satisfiable for all three, and for A it is satisfiable IN THE SHIPPED SCENE** — `BuildCombat`
authors one snake **and** one boar (`MovementCameraScene.cs`, `BuildSnake(player, groundLayer)` then
`var boar = BuildBoar(player, groundLayer)`), so a struck boar and an unstruck snake occupy the same frame by
construction. They share the material family (both `Shader.Find("FarHorizon/LowPolyVertexColor")`) and the
chunky-faceted silhouette family, which is C3 **step 2**'s test — so the pair qualifies at step 1 *and* would
survive step 2 if anyone contested "same kind". **No purpose-built rig is needed for this surface**, the same
happy property §15.5 found for the pip row and for the same underlying reason: the world already contains the
comparison.

### 16.2 The framing — cited, plus ONE derived row the bar's table does not carry

Framing values quoted verbatim from the bar's § *"The default gameplay framing"*: pitch **55°**, distance
**14 u**, FOV **45°**, capture **1280 × 720**, frame-plane **62.0798 px/m**, world-vertical (foreshortened)
**35.6075 px/m** = `62.0798 × cos 55°`. **The ruler is not this doc's to re-derive** (§14.1's rule, carried).

**The bar's two rows are not sufficient here, and the gap is not pedantry.** The pip row is screen-space IMGUI,
so §15.2 could say *"the pill's px **are** px"*. Every channel in §16 is a **world-space** displacement, and two
of them (the boar's tail flick, the snake's whip) are world-**HORIZONTAL** — a direction the bar's table does
not price. Derived from the same framing, for a unit world direction `d` the on-screen scale is
`62.0798 × sqrt(1 − (d·f)²)` where `f` is the camera forward:

| Displacement direction | Scale | Arithmetic |
|---|---|---|
| In the frame plane (upper bound, any extent) | **62.0798 px/m** | the bar's own figure |
| World **HORIZONTAL**, worst bearing (along the camera's forward-horizontal) | **50.8528 px/m** | `62.0798 × sin 55° = 62.0798 × 0.819152` |
| World **VERTICAL** (any bearing) | **35.6075 px/m** | `62.0798 × cos 55° = 62.0798 × 0.573576` |

> **Read the consequence, because it inverts the intuition.** At pitch 55 a **vertical** displacement is the
> *most* foreshortened thing on screen — worse than any horizontal one. A world-horizontal displacement is
> bearing-dependent but never falls below **50.8528 px/m**, i.e. **1.43×** the vertical scale. **So a head-toss
> is the most expensive gesture per degree that this camera can be shown, and a tail flick is the cheapest** —
> which is the opposite of what §4.2 assumed when it ranked the head as the primary term and the tail as the
> "cheapest" afterthought. §4.2's *conclusion* (keep the tail flick) survives; its *reason* (occlusion from
> behind) is now the second reason, not the first.
>
> **Proposed as a bar addition, not asserted as one.** This row is arithmetic over the bar's own framing table,
> but it is not IN that table, and world-space cues are a whole class (`86cah7y5b`, the rock/ore/driftwood
> posture candidate) that will need it. Decision draft in §15; `/name-the-bar` candidate.

**Rotation convention, quoted:** *"a point at horizontal radius `r` from that axis travels a chord
`2 · r · sin(Δ/2)`"*, `r` = *"the measured horizontal distance from the rotation axis to the vertex the author
claims the read from, **never the object's bounding radius**"*, reading = **peak-to-peak**. Every `r` below
names a real vertex from the mesh builder and says which vertex was **not** claimed.

**Derived vs measured — the same honesty line the bar draws.** Everything in §16 is arithmetic over serialized
constants and mesh-builder source. **No build was run** (this is the non-build lane; Drew holds the Unity
slot). Geometry cannot see occlusion, fog, contrast or AA — a geometry-green channel can still be invisible,
and a geometry-red one is dead regardless. Both directions matter below.

### 16.3 Cue A — CONNECTION. The three channels, their axes, and their deltas

**All three vary between cued and non-cued, and the variance is pinned by tests this spec already wrote** —
§12's *"Flinch is identity at rest"* (every transform bit-identical to the no-feature build), the shader's
inert default (AC2 `[DFC-1]`/Claim 2), and the puff's absence at rest. So the **free invariance pre-filter**
passes all three with no build, which is what that pre-filter is for.

#### 16.3a Flash — the **VALUE** channel. Area and luminance stated separately, never traded

C1 forbids quoting an area claim as though it were a displacement figure, so the flash owes **two** numbers in
**two** units.

**AREA (px²).** The flash is a per-material albedo write reaching every part (AC2's 🔒 — all 7 boar / all 13
snake materials), so the repainted area is **100 % of the creature's rendered silhouette by construction**.
Upper-bounded by the §14.2 bounding boxes: boar `93.74 × 32.05 = 3004.4 px²`, snake
`120.43 × 8.19 = 986.3 px²`. **The silhouette fill fraction is NOT derivable from source** — it needs the
rendered backstop, and inventing one here would be the pond-in-a-mound move. What survives the unknown: at any
fill ≥ 30 % the boar's repainted area is **≥ 901 px²**, i.e. **≥ 5.0×** the pip row's strongest channel
(CH1, 180 px²), and the ordering does not invert at any plausible fill.

**LUMINANCE (ΔL, Rec.709 `0.2126R + 0.7152G + 0.0722B`), on §3.1's own shipped tone table.** This is a
different claim in different units and is quoted as such:

| Tone | L base | L flashed | **ΔL** | ratio |
|---|---|---|---|---|
| `BoarBrown` (the dominant area) | `0.2126×0.42 + 0.7152×0.32 + 0.0722×0.22 = 0.334040` | `0.2126×0.798 + 0.7152×0.5504 + 0.0722×0.33 = 0.587127` | **0.2531** | 1.758× |
| `SnakeRust` | `0.165828+0.271776+0.011552 = 0.449156` | `0.195592+0.467455+0.017328 = 0.680375` | **0.2312** | 1.515× |
| `BoarEye` | `0.012756+0.035760+0.002888 = 0.051404` | `0.024236+0.061507+0.004332 = 0.090076` | **0.0387** | 1.752× |
| `BoarTusk` | `0.191340+0.629376+0.056316 = 0.877032` | `0.92 × (0.2126+0.7152+0.0722) = 0.920000` | **0.0430** | 1.049× |

> *⚠ **Computed from §3.1's `gain = (1.90, 1.72, 1.50)` / `ceil = 0.92` EXPRESSION, not from its display-rounded
> flashed column** (corrected 2026-08-01, PR #413 review NIT 3). A draft took the flashed tones from §3.1's
> table as printed — `(0.80, 0.55, 0.33)` / `(0.92, 0.65, 0.24)` / `(0.11, 0.09, 0.06)` — and read
> **0.2532 / 0.2286 / 0.0407**. The exact lifts are `(0.798, 0.5504, 0.33)`, `(0.92, 0.6536, 0.24)` and
> `(0.114, 0.086, 0.060)`. **`BoarEye` was the one that mattered** (0.0407 → **0.0387**, ~5 % generous), and
> the correction makes its own conclusion — the eye contributes almost nothing — hold **a fortiori**.
> `BoarBrown` moved 1e-4, which is noise. **`BoarTusk` was already exact**: all three ceiling clamps fire, so
> the flashed tone is exactly `(0.92, 0.92, 0.92)`. **No verdict and no downstream ratio in §16 moves** — the
> §16.8 comparisons against the pip row still read 0.64× and 1.42× at the precision they are quoted to.
> Recorded because the next doc that quotes these numbers should know which decimal is load-bearing.*

> **🔴 Claim the flash's read from the BODY-BROWN mass, never from the tusk.** The tusk moves **ΔL 0.0430** —
> a 1.049× step. That is real in the data and renders as near-nothing, and §3.1 *chose* it: the tusk sits
> near the 0.92 ceiling precisely so the ivory stays the brightest thing on the animal. **The cost is
> explicit now: the boar's two identity features (tusk 0.0430, eye 0.0387) contribute almost nothing to the
> flash's magnitude.** The channel is carried by the brown body mass at ΔL 0.2531 and by nothing else.
> `1.758× / 1.515×` are luma ratios, so **desaturate is satisfied by construction** (§3.1's multiply is
> hue-preserving) — that clause was already right and is the one bar-#10 clause this element passes trivially.
>
> **Against the pip row:** the flash's ΔL 0.2531 is **0.64×** the pip row's best-case CH2 depth (0.3929) and
> **1.42×** its worst per-hit depth (0.1786) — but delivered on **≥5×** the area. Neither element dominates
> the other on value; they trade depth for area. **Which is exactly why they collide — see §16.8.**

#### 16.3b Flinch — the **MOTION** channel. Three terms, each measured against the vertex it claims

`BoarBodyRig.LateUpdate` writes each part's `position` from its captured `_homePos` and its `rotation` from
`_homeRot × Quaternion.Euler(...)` — so **each part rotates about its own origin and no part's origin moves.**
Every `r` below is therefore measured inside one mesh, from that mesh's local origin.

| Term (§4.2) | Δ (peak) | `r` — vertex claimed | Source of `r` | chord `2·r·sin(Δ/2)` |
|---|---|---|---|---|
| Head **pitch −14°** | 14° | **0.2100 u** — the snout front-cap centre | `BoarHead` emits `new Vector3(0, 0, halfL)`, `halfL = BoarHeadLength/2 = 0.42/2` | `2×0.2100×sin 7° = 2×0.2100×0.1218693 = ` **0.0511851 u** |
| Body **pitch −5°** | 5° | **0.5500 u** — the rump cap centre | `BoarBody` emits `new Vector3(0, radius×rings[0][3], −halfL)`, `rings[0][3] = 0.00`, `halfL = BoarBodyLength/2 = 1.1/2` | `2×0.5500×sin 2.5° = 2×0.5500×0.0436194 = ` **0.0479813 u** |
| Tail **yaw +10°** | 10° | **0.2200 u** — the tail tip | `BoarTail` emits `tip = new Vector3(0, −radius×0.6, −length)`, `length = 0.22`; yaw radius = `sqrt(x²+z²) = 0.22` | `2×0.2200×sin 5° = 2×0.2200×0.0871557 = ` **0.0383485 u** |

> **⚠ PIN THE PIVOT — a plausible implementation choice DOUBLES every head figure above** (added 2026-08-01,
> PR #413 review §6b). `BoarHead`'s rings run `zFrac −1.00 → +1.00` scaled by `halfL = 0.21` about **the part's
> own origin** (`Assets/Scripts/Editor/LowPolyMeshes.cs:1550-1596` at `fb2ac24`: the neck back cap is
> `new Vector3(0, 0, −halfL)`, the snout front cap `new Vector3(0, 0, +halfL)`), and `BoarBodyRig`
> right-multiplies `Quaternion.Euler` onto `_homeRot[i]` — so the head pivots at its **middle**, and `r = 0.21`
> is the snout's true distance from the axis. **An implementer who builds the toss at the anatomically natural
> NECK JOIN (local `z = −0.21`) puts the snout at `r = 0.42` and doubles every head figure in this audit** —
> 1.8226 → **3.6452** px vertical, 3.1776 → **6.3552** px frame-plane — which flips §16.4a's 1.7804 px row for
> the head from marginal (×1.02) to comfortable and **changes its 4 px verdict**. **`r = 0.21` is correct as
> shipped, and pinning the pivot is a CONSTRAINT on AC3's implementation, not merely a measurement note.** Not
> a defect in the arithmetic — a trap the arithmetic makes visible.

**Vertices deliberately NOT claimed, and why** — this is where a bounding-radius cheat would live:
- The head's **ear apex** sits at local `(±0.1364, 0.26048, −0.1861)` (`EmitBoarEar`: `baseCentre +
  (0, r×1.4, −r×0.5)`, `r = neckR×0.26 = 0.0572`, base `(±neckR×0.62, neckR×0.82, −halfL×0.75)`), giving
  `sqrt(0.26048² + 0.1861²) = sqrt(0.067850+0.034633) = ` **0.32013 u** — **52 % larger than the claimed
  0.2100**. It is a 3-triangle sliver whose own on-screen area is a fraction of a pixel. Claiming the head's
  read from it would inflate the figure by half and is precisely what C1's *"never the object's bounding
  radius"* forbids.
- The head's **snout-ring top vertex** (`z = 0.21`, `y ≤ neckR×0.42 = 0.0924`) gives `0.22943 u`. Also larger,
  also not claimed — the front-cap centre is the honest representative of the face that moves.
- The body's **rump-top vertex** gives `sqrt(0.55² + 0.154²) = 0.57115 u`. Not claimed; the cap centre is.

**Converted at §16.2's scales.** The head and body terms are **pitches**, so their displacement at the claimed
vertex is ~vertical (at `(0,0,z)` under a pitch, the tangent is `±Y`; the chord's bearing tilts off vertical by
only `Δ/2` = 7° / 2.5°). The tail is a **yaw**, so its displacement is world-horizontal:

| Term | Operative reading | **px** | Frame-plane upper bound |
|---|---|---|---|
| Head pitch | vertical, 35.6075 | `0.0511851 × 35.6075 = ` **1.8226** | `0.0511851 × 62.0798 = ` **3.1776** |
| Body pitch | vertical, 35.6075 | `0.0479813 × 35.6075 = ` **1.7085** | `0.0479813 × 62.0798 = ` **2.9787** |
| Tail yaw | horizontal, 50.8528 → 62.0798 | `0.0383485 × 50.8528 = ` **1.9501** | `0.0383485 × 62.0798 = ` **2.3807** |

**⚠ The tail is the one term where ABSOLUTE ≠ DELTA, and it is a LIVE instance of the bar's example 2.** The
non-cued boar's tail is **already moving**: `wag = Mathf.Sin(Time.time × 3.1f) × 8f` (`BoarBodyRig.LateUpdate`,
tail branch). So the same-instance extremes pair — the rendered backstop — would read `(8+10) − (−8) = 26°`
⇒ `2×0.22×sin 13° = 2×0.22×0.2249511 = 0.0989785 u` ⇒ **6.1446 px** frame-plane. The **difference signal** is
the flick's own 10°, i.e. **2.3807 px**. That is a **2.58× overstatement**, on shipped values, from the
instrument the bar names as C1's rendered backstop. The bar asked for an example where the two differ; the
project has one, in code, today. **Consequence for AC7: the flinch's `cue_ext_a`/`cue_ext_b` pair is a valid
delta stand-in for the HEAD and BODY terms (their non-cued value is exactly zero, pinned by §12's
identity-at-rest test) and is NOT valid for the TAIL.** Grade the tail from geometry only.

**With the ≤15 % counter-overshoot at its cap** (§4.4) the head's p2p difference signal is `14 + 2.1 = 16.1°`
⇒ `2×0.2100×sin 8.05° = 2×0.2100×0.1400372 = 0.0588156 u` ⇒ **2.0943 px** vertical / **3.6513 px**
frame-plane. Quoted at the **guaranteed** 14° above, because the overshoot is a cap and not a promise.

#### 16.3c Dust — the **FORM** channel, whose magnitude **does not exist yet**

§8 specifies the puff's count (`4–9`, cap 12), lifetime (~0.35–0.45 s), colour (`#B39472`), shape (upward-
and-outward gravity cone, never radially symmetric) and material discipline. **It specifies no particle SIZE,
no cone half-angle, no initial speed and no spawn radius.** Without a size there is no on-screen extent, and
without an extent there is **no C1 magnitude** — the channel cannot be counted, only named. That is a gap in
**my** spec, found by auditing it, and it is filled in §16.4c as Sponsor options rather than settled here.

#### 16.3d ❌ Candidates the free invariance pre-filter kills — named so nobody counts them later

- **The creature's base colour and silhouette.** Present on every boar, cued or not ⇒ *style, not a cue*. It
  answers *"what kind of thing is this"*, never *"which one was just hit"*.
- **The idle breathe bob.** `breatheAmplitude = 0.015f`, a body-part positional term present on every live
  boar. Killed **twice**: invariant, **and** `0.015 × 35.6075 = 0.5341 px` — sub-pixel under every candidate
  floor. Two independent reasons, either sufficient.
- **The leg gait and the tail wag.** Same shape as the breathe — present on both instances. The tail's
  *flick* survives as a delta (16.3b); the *wag* it rides on does not.
- **`_HitFlashTime` as a channel distinct from the flash amplitude.** They are one write on one axis; counting
  the stamp and the amplitude separately would be leaf-splitting.

**Cue A axis count: 2 COUNTED axes — VALUE (flash) + MOTION (flinch)** — both hue-independent. That clears the
row's ≥2 **on axes**. **FORM (dust) is NAMED but NOT COUNTED:** §16.3c refuses it for want of a magnitude, and
crediting it here would be crediting exactly what that section just declined. *(A 2026-08-01 draft read
*"= 3 distinct axes"*; **withdrawn** — PR #413 review NIT 1. No verdict moves: 2 still clears ≥2, and §16.8's
composed count is per-element-**domain**, not per-axis. Corrected because it is the one line in §16 that credits
what the section above it scrupulously refuses to, and it is quotable out of context.)* FORM becomes a third
counted axis the moment §13 Q9 sets a size — §16.4c prices what it would then be worth. **C2 is the binding
constraint and it does not clear — §16.5.**

### 16.4 🔴 The magnitudes that do not clear, and the one that does not exist

#### 16.4a The boar's flinch is a **1.7085 – 3.1776 px** channel, and the verdict TURNS on the unset floor

§15.2 was able to close with *"the verdict does not depend on which floor is eventually chosen"*. **This
section cannot say that, and saying it anyway would be the single easiest way to make §16 worthless.**
Against the four candidate floors the bar itself discusses:

| Candidate floor | Head 1.8226 / 3.1776 | Body 1.7085 / 2.9787 | Tail 1.9501 / 2.3807 | Verdict |
|---|---|---|---|---|
| **1 px** | pass / pass | pass / pass | pass / pass | **channel survives** |
| **1.7804 px** (`game-juice.md` §1's ±0.05 u, peak-foreshortened) | pass (×1.02) / pass | **FAIL** (×0.96) / pass | pass / pass | **marginal — one term reds** |
| **4 px** | **FAIL / FAIL** | **FAIL / FAIL** | **FAIL / FAIL** | **🔴 channel is GONE** |
| **6.2080 px** | **FAIL / FAIL** | **FAIL / FAIL** | **FAIL / FAIL** | **🔴 channel is GONE** |

> **State the consequence plainly.** If the C1 floor lands at **4 px**, the boar's flinch is not a channel,
> Cue A on the boar collapses to VALUE (flash) + FORM (dust), and **§0's tonal anchor — *"if they'd describe
> it as 'it flinched', it landed"* — is describing something the arithmetic says they cannot see at 14 u.**
> §16 does **not** set the floor (the bar does not, and picking one to make my own spec pass is bar-gaming).
> It states the magnitudes so a reviewer recomputes them against whatever floor is set, and it states that
> **two of the four candidates red this channel** — which §15 never had to say about the pip row.
>
> **What §16 explicitly does NOT do about it: raise the amplitudes.** §4.2's peaks are calibrated as fractions
> of Sponsor-PASSED amplitudes on the same rig (head 41 % of `headLowerDeg`, body 42 % of `chargeLeanDeg`),
> and §4.1's orthogonal-axis rule plus bar #9's telegraph read are what bound them. Cranking degrees to clear
> a floor that has not been set would trade a *confirmed* bar (#9, the boar soak PASS) for an *unset* one.
> **The lever, if the soak agrees the flinch is invisible, is `enemy_hit_flinch_amp` (§10) — a dial that
> already exists and that the Sponsor rides live.** That is the honest response: measure, report, hand him
> the knob.
>
> **What the geometry cannot settle, in the charitable direction.** The bar is explicit that geometry sees
> neither contrast nor motion salience. A 2 px displacement of a **high-contrast silhouette edge against a
> saturated green field**, sustained over ~0.22 s at 60 fps, is a temporal signal the px figure does not
> price. §16 does not claim the flinch is invisible — it claims the **magnitude is 1.7–3.2 px** and that this
> is the band in which the bar's own floor question is unresolved. **AC6's soak is the arbiter, and §13 Q2/Q5
> are already the right questions to ask him.**

#### 16.4b The snake's flinch is **2.35× the boar's**, which reverses §13 Q5's worry

`SnakeBodyChain` applies its lateral term as a **positional** write (`p += lateral × ...`; `lateral =
Vector3.Cross(Vector3.up, tangent).normalized`), so §4.3's `0.09 u` peak on segment 0 is a displacement
directly, world-**horizontal**:

| Term | Delta (u) | px @ 50.8528 | px @ 62.0798 |
|---|---|---|---|
| Lateral whip, segment 0 (§4.3) | **0.09** | `0.09 × 50.8528 = ` **4.5768** | `0.09 × 62.0798 = ` **5.5872** |
| Head yaw, peak 12° (§4.3, pinned) | `2×0.13×sin 6° = 2×0.13×0.1045285 = 0.0271774` | **1.3820** | **1.6872** |

`r = 0.13 u` = `SnakeHeadLength/2 = 0.26/2`. *(Under the discarded 24°-full-swing reading the head yaw would be
`2×0.13×sin 12° = 0.0540570 u` ⇒ 2.7490 / 3.3559 px — which is why §4.3's wording was pinned rather than left.)*

**⚠ Same absolute-≠-delta trap as the tail, and worse.** The non-cued snake is already swinging laterally:
`slitherAmplitude = 0.055f` at full crawl, `idleSwayAmplitude = 0.012f` stationary. The same-instance extremes
pair reads `(0.055 + 0.09) − (−0.055) = 0.20 u` ⇒ up to **12.42 px** frame-plane — a **2.22×** overstatement
of the 0.09 u difference signal. **The snake's rendered backstop is invalid for the same reason the tail's is.**

> **The finding: the snake's whip clears every candidate floor except 6.2080 px, at 4.5768–5.5872 px — while
> the boar's best term clears only 1 px unambiguously.** §13 **Q5** asks whether the snake needs a *bigger*
> share of the amplitude budget than the boar. On C1 magnitude the answer is **no — it already has 2.35× the
> boar's** (`4.5768 / 1.9501`). Q5's real worry is **legibility at ground level under occlusion**, which is a
> different question that geometry cannot answer (the snake's on-screen *height* is 8.19 px, §14.2 — a body
> that thin can be displaced 5 px and still be hard to see against grass). **Q5 stays on the Sponsor list,
> re-worded: not "is the amplitude enough" — the numbers say it is — but "does a 5 px lateral snap read on a
> body 8 px tall lying in green".**

#### 16.4c The puff's geometry — three options, priced; **no pick made here**

> ### 🔴 **CORRECTED 2026-08-01 — the table below replaces one that MIXED TWO MEASUREMENT PLANES**
>
> (PR #413 review, NIT 2 — graded non-blocking there; **treated as blocking here, because this table is
> Sponsor-facing decision input for §13 Q9 and it eliminated an option that is not actually eliminated.**)
>
> The draft quoted each chunk's extent at the **frame-plane** `62.0798 px/m` and then divided it by creature
> heights derived at the **world-vertical** `35.6075 px/m`. Every %-of-height figure was therefore inflated by
> `62.0798 / 35.6075 = ` **1.7434×** — which is exactly `sec 55°`, i.e. the *only* factor separating the bar's
> two scale rows (`35.6075 = 62.0798 × cos 55°`, §16.2). **A single hidden `sec 55°` is why the mix was
> invisible: both numbers looked like "px".**
>
> **Two stated conclusions do not survive it, and one of them told the Sponsor which option to pick.** They
> are restated at the foot of this section rather than quietly patched.
>
> ⚠ **§16.3a's `93.74 × 32.05 = 3004.4 px²` box is NOT the same error and is left alone.** An on-screen
> bounding box is width × height, and those are **two different world directions**: a broadside length reads
> in the frame plane, a vertical extent foreshortens. *Composing* them into a box is correct; *dividing* one
> by the other is not. Only the height column below was wrong.

**The population for every px figure in this section:** pitch **55°**, distance **14 u**, FOV **45°**, capture
**1280 × 720** — the bar's default gameplay framing, quoted in §16.2 and not re-derived here. A world extent `s`
reads `s × 35.6075 px` along **world-vertical** and `s × 62.0798 px` in the **frame plane** (the upper bound over
all directions). A dust chunk is a 3-D faceted solid, so its own on-screen extent lies **between** the two
depending on how it happens to be rotated — **the pair is the honest quote, which is what §16.3b, §16.4a and
§16.4b already do for every other magnitude in this audit. This table was the one place that did not.**

**The %-of-height column needs no px conversion at all — and that is the tell.** Chunk-vertical over
creature-vertical is `(s × k) / (H × k) = s / H`: the scale **cancels**, so the fraction is identical in either
plane and is a pure world-space ratio. Getting a *different* answer out of it is only possible by using two
different `k`s. Creature heights from `enemy-hp-read-spec.md` §14.2, **which carries both readings** — boar
**0.90 m** (32.05 px vertical / 55.87 px frame-plane), snake **0.23 m** (8.19 / 14.28). Burst `n = 6` = §10's
`enemy_hit_puff_count` default.

| Option | `s` | vertical extent | frame-plane extent | ≤ px²/chunk | ≤ px², n=6 | **% of boar height** | **% of snake height** |
|---|---|---|---|---|---|---|---|
| **A** | 0.05 u (the `game-juice.md` §1 house scale) | **1.7804** | **3.1040** | 9.63 | 57.8 | **5.6 %** | **21.7 %** |
| **B** | 0.08 u | **2.8486** | **4.9664** | 24.67 | 148.0 | **8.9 %** | **34.8 %** |
| **C** | 0.12 u | **4.2729** | **7.4496** | 55.50 | 333.0 | **13.3 %** | **52.2 %** |

*Arithmetic, shown: `0.05 × 35.6075 = 1.78038`, `0.05 × 62.0798 = 3.10399`; `0.05 / 0.90 = 5.56 %`,
`0.05 / 0.23 = 21.74 %`; likewise at 0.08 and 0.12. **Cross-check that the ratio really is scale-free:** at
frame-plane on both terms, `3.1040 / 55.87 = 5.56 %` and `3.1040 / 14.28 = 21.7 %` — same answers. The px²
column is the chunk's **frame-plane bounding box**, an upper bound twice over (a rotated faceted tri-chunk
covers roughly half to two-thirds of its box, and a chunk presenting one axis vertically covers `cos 55° =
0.574` of the box's area). It is compared against no foreshortened quantity here.*

**Against the same four candidate floors §16.4a uses**, read `vertical / frame-plane`:

| Option | 1 px | 1.7804 px | 4 px | 6.2080 px |
|---|---|---|---|---|
| **A** (1.7804 / 3.1040) | pass / pass | **ties exactly** / pass | **FAIL / FAIL** | **FAIL / FAIL** |
| **B** (2.8486 / 4.9664) | pass / pass | pass / pass | **FAIL** / pass | **FAIL / FAIL** |
| **C** (4.2729 / 7.4496) | pass / pass | pass / pass | pass / pass | **FAIL** / pass |

*Option A ties the 1.7804 px floor **exactly**, and not by luck: that floor **is** `game-juice.md` §1's ±0.05 u
read at the vertical scale, and option A's `s` is that same 0.05 u.*

**What the corrected numbers say, without choosing:**

- **🔴 *"B is the only option"* does NOT survive — and it was this table's load-bearing claim.** The draft's
  §13 Q9 called B *"the only option where each chunk clears a 4 px floor **and** stays under a quarter of the
  boar's on-screen height."* **Both halves fail:**
  - **The quarter-of-the-boar clause eliminates NOTHING.** At a consistent reading the *largest* option, C, is
    **13.3 %** of the boar's height — every option is already under a quarter, by roughly a factor of two. That
    clause discriminated only because of the 1.7434× inflation.
  - **Clearing 4 px is reading-dependent, and B is on the WRONG side of it under the conservative reading.**
    Frame-plane: **B and C** clear, A does not — *two* survivors. World-vertical: **only C** clears (4.2729) —
    a *single* survivor, and it is **not B**. ⇒ **B is eliminated under the conservative reading and merely
    tied-with-C under the generous one. It is never uniquely correct.**
- **No option clears 6.2080 px on the vertical reading.** The draft's *"C is the only option clearing every
  candidate floor including 6.2080 px"* holds **only** as a frame-plane statement (7.4496 vs 4.2729) and is
  re-labelled as one.
- **A is the only option under 4 px on BOTH readings** — so the *"reads as speckle at 14 u if the floor lands
  at 4 px"* worry about A is the one conclusion that survives unchanged, and it now survives **without**
  depending on which plane you read it in.
- **🔴 The snake disproportion is REAL, and it is a CONSTANT — which is a more useful finding than the 91 %
  was.** The ratio between the two creatures' shares is `0.90 / 0.23 = ` **3.91× for every option**, because
  `s` cancels there too: whatever chunk size ships, a chunk is 3.91× as large a fraction of the snake as of the
  boar. **So Q9's answer cannot fix the disproportion — no size pick reaches it.** (The draft's *"even option A
  is 37.9 % of the snake"* overstated the size; at **21.7 %** the inversion is a fifth of the body's height
  rather than two-fifths. It is still the sharpest single number in this table, and it is §14.3's inversion
  again on a different element.)
  *(The height-only qualifier is carried deliberately, exactly as `enemy-hp-read-spec.md` §14.3 carries it
  after Devon's N3: the snake's on-screen presence is dominated by its **120.43 px length**, against which even
  option C's chunk is **6.2 %** — `7.4496 / 120.43`. **That figure was already consistent** (both terms
  frame-plane; `0.12 / 1.94 = 6.19 %` scale-free) and is unchanged by this correction — which is itself
  confirmation of the diagnosis: only the height column mixed planes. The inversion is real and
  one-dimensional; "the puff swamps the snake" full stop would be the same overstatement.)*
- **The forward rule that falls out is unchanged, and is now better supported:** a per-creature puff scale is
  the obvious fix and is **rejected** — it re-introduces the per-enemy fork AC1 forbids and makes enemy #3
  wrong by default. If the disproportion matters at the soak, the lever is a **bounds-derived** scale (the §8
  spawn point is already renderer-bounds-derived, so enemy #3 stays correct for free), not a `SnakeChunkSize`
  constant. **The 3.91× constant is the reason it has to be bounds-derived: it is invariant under every value
  Q9 could return.**
- **The size bound §8 is missing is still missing — and the tone argument for it is now WEAKER, not stronger.**
  C at **13.3 %** of the boar's height is not *"approaching a piece came off the animal"* on the arithmetic;
  that phrasing was the inflated reading talking, and it is withdrawn. §8 still guards the tone with COLOUR
  only (*"lighter than every creature tone"*) and never with a size bound, and **that gap is what this row
  exposes** — independent of which option wins, and no longer propped up by a number that was wrong.

**Sponsor-input item §13 Q9.** No recommendation is made — this is a look call on an element that has never
been rendered, and the honest state is *"three priced options, **none eliminated**, and a missing size bound"*.
**The draft's state was "B, by elimination". That elimination was an artifact of the mixed scale and is
withdrawn.**

### 16.5 🔴 C2 — the body is **ONE** failure domain. `enemy-hp-read-spec.md` §15.4's three-domain count is WITHDRAWN

§15.4's table named the flash's domain as *"the material instance + the shader property"*, the flinch's as
*"the part `Transform[]`"*, the dust's as *"the pooled `ParticleSystem`"*, concluded *"null any one and the
other three survive"*, and rested its verdict on that. **Those three names are LEAF properties — exactly the
granularity C2's tie-breaker was added to forbid.**

C2's tie-breaker, verbatim: *"Name the **nearest common dependency on the code path both channels actually
traverse** — never a leaf property."* #351 is the bar's own demonstration of the difference: at leaf
granularity its channels name `visual.localPosition` and `visual.localRotation` ⇒ 2, while one
`if (visual == null) return;` kills both ⇒ 1.

**Applied honestly to the body:**

| Channel | Leaf name (§15.4's answer — the tempting, WRONG one) | Nearest common dependency on the shared path |
|---|---|---|
| Flash (VALUE) | the material instance + `_HitFlash*` property | **the gated `Health.Changed` dispatch** |
| Flinch (MOTION) | `BoarBodyRig.parts` / `SnakeBodyChain` segments | **the gated `Health.Changed` dispatch** |
| Dust (FORM) | the pooled `ParticleSystem` | **the gated `Health.Changed` dispatch** |

All three fire from **one** handler, and the ticket **mandates** that they do: AC1's 🔒 *"fire from
`Health.Changed` on a damage delta, never from the attacker. **One shared path for every enemy**… No
`BoarEnemy` / `SnakeEnemy` branches."* On top of that path this spec adds §2.1's **magnitude gate** (2.0 % of
`Health.Max`) and **0.12 s refractory**, and §2.2's **pre-clamp intent** read. That is not an abstraction I
imposed to reach a verdict — **it is literally one of the four shared-domain forms C2 enumerates: *"one early
return in one `Update` guarding both."*** ⇒ **count = 1. The body read alone does NOT meet bar #10's ≥2.**

**Two procedures return different answers here — and the difference is NOT a hole in bar #10.** §15.3(ii)
settled the pip row by **resource enumeration**: enumerate every resource each channel reads and null each in
turn. Run that same procedure on the body and it returns **three** resources, each killing exactly one channel —
which is precisely why §15.3(iii) offered the body as *the control that proves the procedure can return ≥2*.
**That prediction is CONFIRMED: the resource enumeration returns 3.** The tie-breaker returns 1.

> ### ⚠ **CORRECTED 2026-08-01 — the "the bar is silent on which governs" framing is WITHDRAWN as UNDER-stated**
>
> (PR #413 review, §2 — the reviewer's disagreement makes this finding **stronger**, and a draft of this
> section had it weaker than the evidence supports. Stating it at full strength.)
>
> **The bar is not silent on this case. Two things in its own text reach it directly:**
>
> 1. **C2's enumerated shared-domain forms explicitly include a HANDLER, not just a rendering resource** —
>    *"one early return in one `Update` guarding both."* That is exactly the body's shape, and it is cited two
>    paragraphs above. This case is inside the bar's worked list, not outside it.
> 2. **C2's injection clause is a CONFIRMATION STEP for the tie-breaker, not a rival procedure.** Read its
>    words: *"null or disable **the named dependency** and assert **both channels stop**."* It verifies the
>    collapse the naming rule has already named. **It nowhere instructs anyone to enumerate every resource and
>    count the survivors.** That procedure comes from `enemy-hp-read-spec.md` §15.3(ii)'s own construction,
>    which attributes itself to C2 — **the bar never defines it.** ⇒ **The two halves do not disagree. One of
>    them is not C2.**
>
> **What the divergence actually is — a granularity inconsistency inside the sibling spec, verifiable at a
> pinned sha.** At **`ad8a8bd`**, `enemy-hp-read-spec.md` §15.4's composed-cue table enters the pip row at
> **dependency** granularity — *"the row record / resolve predicate / `enemy_hp_pips_enabled`"*, counted as
> **one** per its own §15.3 — and then enters the body's three channels at **leaf** granularity — *"the
> material instance + the shader property"*, *"the part `Transform[]`"*, *"the pooled `ParticleSystem`"*,
> counted as **three**. **Two granularities, four rows, one table.** Apply the pip row's own granularity to
> the body's rows and the table returns 1.
>
> **That inconsistency IS the finding. No bar gap is needed to reach it** — and claiming one weakened a
> conclusion that already stands on the sibling doc's own reasoning, applied evenly.

> **This section adopts ONE, and states why, and states what would overturn it.**
> **Adopted: 1.** (a) The tie-breaker's *"never a leaf property"* is unambiguous, and material / `Transform[]`
> / `ParticleSystem` are leaves by the same standard that makes `visual.localPosition` one. (b) The shared
> dispatch is not an incidental convenience — AC1 **requires** it, so the coupling is architectural and
> permanent, not an implementation choice a reviewer could ask to be undone. (c) The alternative reading is not
> a second clause of the bar competing with the first — it is a procedure a sibling spec constructed and
> attributed to C2. **Adopting it would be taking the count that makes my own spec pass on the strength of my
> own sibling doc's construction: the bar-gaming its history section warns about, one step removed.**
> **What would overturn it:** the bar **ADOPTING** the resource enumeration as a defined C2 procedure. That
> would be an **amendment**, not a clarification, because C2's text does not contain it today — and §16 does
> not propose it. **On bar #10 as written, the count is 1**, and the live question is not "which half governs"
> but "does the bar want an enumeration procedure at all". `/name-the-bar` candidate either way, and the
> verdict does not wait on it.
>
> **⚠ Not a defect in the design, and not a reason to add a channel.** All three channels are correct, in
> tone, and doing distinct jobs (§0: *"one event seen three ways"*). C2 measures how they FAIL, not how they
> read. Adding a fourth channel on a fourth trigger to reach ≥2 would be bar-gaming; re-plumbing one channel
> off `Health.Changed` would break AC1. **The right response to a one-domain verdict is to state it.**

### 16.6 🔴 Cue B — WEIGHT. The differentiation is sub-pixel-to-1.33 px on the boar's flinch

§5 promises a *"~1.5× spread"* across the shipped weapon set and asserts *"1.5× is plainly visible"* in the
flinch and the dust. The **amplitude** spread is real; the **on-screen** spread is not what that sentence
implies. Cue B's delta is `dagger_wood` vs `spear_iron` — **computed from §5's formula, not from its display
table**: `w = sqrt(4.5/20) = 0.474342` ⇒ flinch `×0.879473`, against `w = 1.00` ⇒ `×1.30`.

| Term | at ×0.8795 | at ×1.30 | Δ chord (u) | **Δ px (operative)** | Δ px (frame-plane) |
|---|---|---|---|---|---|
| Boar head | `2×0.21×sin 6.1563° = 0.0450411` | `2×0.21×sin 9.1° = 0.0664264` | 0.0213853 | **0.7615** | 1.3276 |
| Boar tail | `2×0.22×sin 4.3974° = 0.0337369` | `2×0.22×sin 6.5° = 0.0498093` | 0.0160724 | **0.8173** | 0.9978 |
| Snake lateral | `0.09×0.879473 = 0.0791526` | `0.09×1.30 = 0.117` | 0.0378474 | **1.9246** | 2.3496 |
| Puff count | 6 chunks | 9 chunks | — (FORM/area) | +3 chunks; ≤74 px² at option B (frame-plane bbox) | — |

*⚠ **Recomputed 2026-08-01** (PR #413 review NIT 3). A draft rode §5's display cell `×0.90`, which that
section's own formula does not produce (see §5's correction note). At the exact `×0.879473` every figure rises
~5 %: head 0.7242 → **0.7615** / 1.2626 → **1.3276**, tail 0.7774 → **0.8173** / 0.9490 → **0.9978**, snake
1.8307 → **1.9246** / 2.2349 → **2.3496**. **The draft UNDERSTATED the deltas and every floor verdict below is
unchanged** — which is the direction that matters: the finding was not an artifact of the error.*

> **The finding: on the boar, the weapon-weight difference between the weakest and the strongest shipped
> weapon is 0.76–1.33 px of displacement.** That fails 1.7804, 4 and 6.2080 px and clears 1 px only under the
> frame-plane upper bound **and only on the head term** (the tail's frame-plane delta, 0.9978, is still under
> 1 px). **§5's "1.5× is plainly visible in the flinch" is not supported by C1 arithmetic on the boar.** It may
> well hold on the **puff count** (a FORM/area channel, +3 chunks = a 50 % more populous burst, which is a
> countable change rather than a measured displacement) and it does hold better on the **snake**
> (1.92–2.35 px).
>
> **§5's DESIGN is not withdrawn — its visibility CLAIM is.** The emergent-from-damage architecture, the sqrt
> compression, the floor, and the no-scaling-on-the-flash call are all still right and rest on bar #9, not on
> px. What is withdrawn is the sentence *"where 1.5× is plainly visible"* as applied to the boar's flinch.
> **§13 Q3 already asks the Sponsor the exact right question** — *"does a spear read heavier than a dagger
> without the flash helping?"* — and §16 now tells him what to expect and which dial answers it: **the flinch
> RANGE (`Lerp(0.50, 1.30, w)`) or the puff count, never the flash** (§3.4/§5 hold that line for reasons
> unrelated to this finding).
>
> **Cue B's axis count is 2 (MOTION + FORM) and its C2 count is 1** — both channels are driven by the single
> weight scalar `w` computed inside the same gated dispatch as §16.5. Same verdict, same reason.

### 16.7 Cue C — DEATH. The only body cue that passes ≥2 — **on BOTH creatures** — and **ABSENCE is why**

| Channel | Axis | Varies vs a live creature? | Nearest dependency |
|---|---|---|---|
| The settle pose + the death puff | **FORM** | yes — head drops (`headDrop = 0.6` ⇒ 20.4°), body `breathe = −0.04`, and a burst appears | one axis ⇒ **counts as ONE** |
| Idle motion **CEASES** — gait, tail wag, slither all stop | **MOTION** | yes — `dead ? 0f : …` on the leg, tail and slither branches | `BoarAI.State` / `SnakeAI.State` / the `dead` flag |
| Death puff (as a failure domain) | (FORM, above) | — | the pooled `ParticleSystem` + `Health.Died` |

**Two things make Cue C the strong one, and neither was designed for bar #10:**

1. **Absence-of-motion is a legitimate MOTION-axis channel.** The non-cued (live) creature carries continuous
   idle motion; the cued (dead) one carries none. That varies, it is hue-independent, and it is already
   shipped in the `dead ?` branches. **So §6's *"the kill's treatment is ABSENCE"* is not merely tonally right
   — it is what gives Cue C its second axis.** A kill *flourish* (forbidden in §9) would have added FORM on
   top of FORM and bought nothing.
2. **The failure domains genuinely differ.** The settle rides `BoarAI`'s `Dead` state, which has **two**
   independent entries: the `Health.Died` subscription (`_health.Died += OnDied`) **and** a per-frame poll
   backstop (`if (State != BoarState.Dead && _health != null && _health.IsDead) OnDied();`). The death puff,
   as §6/AC4 specify it, hangs on `Health.Died` alone. **An injection exists that kills exactly one:**
   unsubscribe `Died` ⇒ the puff never fires, the settle still happens via the poll. ⇒ **count = 2.**

> **⚠ Do not read (2) as a design win — it is an accident of `BoarAI`'s defensiveness.** Recorded here so that
> whoever implements AC4's death puff knows the poll backstop is what the count rests on, and does not "tidy"
> the puff onto the same single subscription.
>
> ### ✅ **DISCHARGED 2026-08-01 — the snake has the same pair. Measured, not assumed.**
>
> A draft of this section carried `Hypothesis, unverified:` — *"the `SnakeAI` equivalent was not read for this
> audit; if it has no poll backstop the snake's Cue C is 1, not 2."* **It has been read**, at this audit's pin
> `fb2ac245fc419d442a474c5d2f970535fa884743`:
>
> - `SnakeAI.cs:206` — `if (_health != null) _health.Died += OnDied;`
> - `SnakeAI.cs:237` — `if (State != SnakeState.Dead && _health != null && _health.IsDead) OnDied();`,
>   inside the **public** `SyncDeathState()` (`:234-240`), which `SnakeAI.Update()` calls **every frame**
>   (`:242`).
>
> Structurally identical to `BoarAI.cs:204` / `:235`. ⇒ **The snake's Cue C is 2, on the same injection:
> unsubscribe `Died` and the death puff never fires while the settle still happens via the poll.** The
> hypothesis is promoted to **measured** and the caveat is struck. *(Independently reproduced at the same pin
> by PR #413's reviewer — same two lines.)*
>
> **This makes the §12 success-test MORE valuable, not redundant.** It is re-scoped from *"discover whether the
> snake has a backstop"* to ***"pin that BOTH creatures keep one"***: the death cue's second failure domain is
> now known to be exactly those two lines per creature, and either could be deleted as dead code by someone who
> does not know they are load-bearing for bar #10. **A count that rests on a defensive line nobody has been
> told is load-bearing is one refactor from being wrong.**

### 16.8 What this means for the COMPOSED cue, and for AC6(c) — information, not a decision

**The composed cue (pip row + body) still passes ≥2 — but on a different, and much tighter, basis than
§15.4 gave.** §15.4's margin came from three body domains. There is one. The composed count is:

| Element | Its one failure domain | Injection that kills exactly it |
|---|---|---|
| **Pip row** | the row record, ARMED from the **strike seam** (`MeleeAttack.cs:229-231`, `enemy-hp-read-spec.md` §3.1 — *"ARM comes from the STRIKE, not from `Health.Changed`"*) | suppress the strike-arm ⇒ no row; the body still flashes, flinches and puffs |
| **Body** | the **gated `Health.Changed` dispatch** (§16.5) | suppress the gate ⇒ no body feedback; the row still arms on the strike and updates from `Changed` |

⇒ **exactly 2, not "≥2 with margin".** `Health` itself is the pair's common ancestor, but naming it is the
over-proving existence-gate move §15.3 already rejected — nulling `Health` removes the enemy's ability to take
damage at all.

> **🔴 Three consequences, and the third is the one that matters.**
>
> 1. **§15.4's verdict survives; its reasoning is replaced.** *"The enemy-damage read meets ≥2 only as
>    pip-row + body"* is still true. It is true because the two elements have **different triggers on
>    different code paths**, not because the body has three domains.
> 2. **It is now a LIVE CONSTRAINT on `86caxhfg2`'s implementation, not just an observation.** The composed
>    ≥2 depends entirely on the pip row keeping its **strike-armed** trigger. If that spec's §3.1 were ever
>    re-plumbed onto `Health.Changed` "for simplicity", the composed cue would collapse to **1** — and the
>    change would look like a harmless refactor. **§15 decision draft filed accordingly.**
> 3. **`enemy-hp-read-spec.md` §15.4's claim that *"body-read-only-forever is a bar-#10-legal outcome"* is
>    WITHDRAWN.** It rested on three body domains. With one, closing `86caxhfg2` at this soak would leave the
>    game's entire enemy-damage cue **single-failure-domain** — the thing bar #10 forbids outright.
>
> **This does NOT pre-answer AC6(c), and the ticket is right to forbid that.** *"Is 'is it nearly down?'
> already answered by the body?"* stays genuinely open and stays the Sponsor's. What changed is the shape of
> the information he gets: §15.4 told him *both* answers were bar-legal. **They are not.** *"No, still want
> the row"* is bar-legal as-is. *"Yes, close it"* is bar-legal **only if something gives the body a second
> failure domain** — and the honest statement is that **no such remedy exists inside this ticket's scope**:
> a second trigger is an architecture change, and inventing a fourth channel to reach a count is bar-gaming.
> **So the correct hand-off is: either answer is still a clean outcome, and the "yes, close it" branch
> carries one follow-up ticket (a second independent trigger path for one body channel) rather than being
> free.** That is information for the decision, priced — not the decision. **§13 Q10.**

### 16.9 C3 and C4 for an IMPACT cue — the artifact the ticket already specifies, and the one clause that does not fit

- **C3 is satisfied at step 1 by the shipped scene** (§16.1) — a struck boar and an unstruck snake in one
  frame. **Named comparison members for the Self-Test Report:** the snake (same shader, same faceted family),
  and at step 2 the warm-brown scatter rocks and chop-tree trunks that share `BoarBrown`'s hue family. C3
  **collects**; it returns no discrimination verdict (the bar is explicit), so this is a naming obligation, not
  a pass to cite.
- **C4's `cue_pair.png` is already in the ticket's capture list** — capture **(a)** *"a landed axe hit on a
  boar at gameplay framing showing flash + puff"* **is** the pair frame, provided the snake is in shot.
  **One addition needed, and it is cheap:** the ticket's (a) does not require the second creature to be
  visible. **Say so in the Self-Test Report and frame it deliberately**, or the capture satisfies four
  obligations and silently misses the fifth (`enemy-hp-read-spec.md` §15.5's N6 lesson).
- **The human half, unwaivable, with this element's question:** show `cue_pair.png` to someone who has not
  read the PR and ask, **before any number** — ***"point at the animal that was just hit."*** Right first
  try, no second look = pass. Record who and what they said.
- **`cue_ext_a` / `cue_ext_b` are LIVE and valid here — for the head and body terms only.** The flinch is a
  motion channel whose non-cued value is exactly **zero** (§12's identity-at-rest test pins it), which is the
  precise condition the bar sets for the same-instance extremes pair to stand in for the delta. **Invalid for
  the tail (idle wag) and for the snake's lateral (slither/idle-sway)** — §16.3b/§16.4b.
- **Desaturate: passes by construction on all three channels** — the flash is a hue-preserving multiply
  (§3.1), the flinch is geometry, the dust is geometry. This element satisfies that clause trivially and it
  is worth saying it is the only one it satisfies trivially.
- **⚠ The clause that does not fit, stated as a bar gap rather than a waiver.** C3/C4 are built around a
  **spatial** pair in one frame. **An impact cue's real comparison is TEMPORAL** — the animal a moment ago vs
  the animal now. The spatial pair is available here and is not being waived, but it answers *"which animal
  is cued"* rather than *"did that hit land"*, which is Cue A's actual question. **The temporal pair the cue
  is really judged on is also already in the ticket:** capture **(a)** at impact and capture **(f)** at
  ~0.5 s. Proposed as a C4 extension for the impact-cue class (`/name-the-bar` candidate, §15 decision draft),
  **not** as a reason to skip the spatial pair.
- **C4 remains UNBUILT project-wide.** Nothing above is coverage; it is specification. Labelled per the bar's
  own rule.

### 16.10 What §16 changes elsewhere in this spec

| Where | Change |
|---|---|
| **§4.3** | `±12°` pinned as a PEAK excursion (disambiguation, no value moved) |
| **§5** | the sentence *"where 1.5× is plainly visible"* is withdrawn **for the boar's flinch** (§16.6); the design stands. **Plus one DERIVED display cell corrected** — `dagger_wood`'s flinch multiplier `0.90 → 0.88`, the value §5's own formula produces (`Lerp(0.50, 1.30, sqrt(4.5/20)) = 0.879473`) |
| **§8** | a **size bound** is missing from the puff spec and is now a Sponsor-input item (§13 Q9), not a silent default |
| **§11** | bar-#10 line in the bounded-convergence claim sharpened — see below |
| **§12** | four success-tests added — see below |
| **§13** | **Q9** (puff chunk size) and **Q10** (the AC6(c) branch cost) added |
| **§15** | five decision drafts added |
| **`enemy-hp-read-spec.md` §15.4** | its three-domain table and its *"body-read-only-forever is bar-#10-legal"* claim are **withdrawn** by §16.5/§16.8. That spec is PR #406 and unmerged; **this is a finding against it, not an edit to it** — I do not edit a sibling doc from this branch. Whoever lands second reconciles, and §15's decision draft says which way. |

**Nothing in §§0–10 changes a DESIGN value.** No amplitude, colour, duration, cap or dial moved. **One derived
display cell in §5 is corrected** (`0.90 → 0.88`) — arithmetic over an unchanged formula, not a design change,
and called out here rather than folded in silently because §§0–10 are otherwise pinned.

#### 16.10a Corrections applied 2026-08-01 from the PR #413 peer review

Recorded as a table because each one is a claim this doc made and had to take back. **No verdict in §16 moves.**

| § | What was wrong | Corrected to | Does a verdict move? |
|---|---|---|---|
| **§16.4c / §13 Q9** | 🔴 **the %-of-height column mixed measurement planes** — frame-plane chunk extent ÷ foreshortened creature height, inflating every figure by `sec 55° = ` **1.7434×** | consistent (scale-free) ratios: A **5.6 / 21.7 %**, B **8.9 / 34.8 %**, C **13.3 / 52.2 %**, with each option's extent quoted as the vertical–frame-plane **pair** | **YES — the only one that does.** *"B is the only option"* is **WITHDRAWN**; no option is eliminated. Sponsor-facing, which is why this was treated as blocking |
| **§16.5** | the C2 divergence framed as *"a gap the bar is silent on"* — **UNDER-stated** | the divergence is `enemy-hp-read-spec.md` §15.4's **two granularities in one table**; the bar reaches this case in its own text | no — count stays **1**, on firmer ground |
| **§16.7** | the snake's death backstop was `Hypothesis, unverified` | **measured** at `fb2ac24`: `SnakeAI.cs:206`/`:237` mirror `BoarAI.cs:204`/`:235` ⇒ snake Cue C = **2** | no — it confirms the stated count for both creatures |
| **§16.3d** | credited FORM (dust) toward *"3 distinct axes"* after §16.3c refused to count it | **2 counted axes**, FORM named-but-unmeasured pending Q9 | no — 2 still clears ≥2 |
| **§16.3a** | ΔL table computed from §3.1's **display-rounded** flashed column | computed from §3.1's gain/ceiling **expression**; `BoarEye` **0.0407 → 0.0387** | no — the eye's *"contributes almost nothing"* holds a fortiori |
| **§5 / §16.6** | rode a display cell (`×0.90`) that §5's own formula does not produce | `×0.879473`; weight deltas **0.7615–1.3276 px** on the boar | no — the draft **understated** the deltas; every floor verdict is unchanged |
| **§16.3b** | the head's pivot was measured but never **pinned** | pivot pinned at the part's own origin (`r = 0.21`); a neck-join build doubles every head figure | no — it forecloses a silent doubling at implementation time |

> **🔴 MIRROR OWED onto `enemy-hp-read-spec.md` (PR #406) — filed, not performed.** My own accepted working rule
> is that **a withdrawal in one sibling spec must be mirrored into the other before either merges — a finding
> filed against a doc is not a fix to it.** That rule cuts **both** ways, and it now points at PR #406: its
> mirror of §16.5 (added at `dc1fea5`) quotes the framing this section has just withdrawn, in two places —
> §15.3(iii)'s mirror blockquote (*"the bar does not say which governs"*) and §15.4's struck-table notice
> (*"when a bar is silent"*). **Neither is wrong about the count (1); both are wrong about WHY.** The correction
> is owed **on that branch**, by the same rule that says I do not edit a sibling doc from this one — filed here
> so merge order cannot lose it, and it is small: replace the "bar is silent" clause with the two-granularities
> reading above. **Whichever PR merges second must carry it.**

---

## Cross-references

- **Tickets:** `86caxjwb3` (this spec) · **`86caxhfg2`** (deferred dependent — the pip row; settled at this
  ticket's soak per AC6(c)) · `86cah7ydt` (boar — `BoarAI`/`BoarBodyRig`; the charge feel §4 must not regress) ·
  `86caaz4vn` (snake — `SnakeBodyChain`; the flat-24-HP balance note) · `86caffwv5` (light swings — the impact
  frame this hangs off; owns hit-stop/Impulse) · `86cah7xxp` (combat POC — `Health`) · `86cah7z2q` (player HP
  HUD) · `86cah7yuh` (status effects) · `86cabcdpn` (combat design lock) · **PR #348 comment `5109223633`**
  (Devon's dev factual-check — the `[DFC-*]` source) · **PR #371** (`enemy-hp-read-spec.md`).
- **Code (ground truth, read at `c8ce948` during authoring):**
  `Assets/Scripts/Runtime/Combat/Health.cs` (`:80/84` events, `:93` `Current01`, `:146-161` `ApplyDamage`,
  `:151` the damage formula + where intent lives, `:157` clamped `removed`, `:183-188` `SetCurrent`) ·
  `StatusEffectController.cs` (`:55-61` per-frame `Update`→`TickSeconds`, `:85-99` the per-frame
  `ApplyDamage`) · `WeaponCatalog.cs` (`:62/68/86/89/92/95/105/108/116/121/124/127` damage consts,
  `:65-66`/`:119-120` bleed dps + duration, `:260`/`:312` bleed application) · `MeleeAttack.cs` (`:204-240`
  `PerformAttack`, `:223` the whiff/corpse early-return, `:229-231` the strike seam) ·
  `ResistanceProfile.cs` (`Multiplier` — a ≤0 multiplier is NEUTRAL, so immunity is unrepresentable) ·
  `BoarEnemy.cs` (`:40/42/44` HP 32/40/50, `:49` pierce ×2.0, `:54` slash ×0.75, `:123-127` `ApplyDifficulty`) ·
  `SnakeEnemy.cs` (`:32` flat 24 HP, `:36` pierce ×1.6, `:112-113` profile) · `BoarAI.cs` (`:25` the Dead
  contract, `:49` the state enum, `:138-144` the `NormT` idiom, `:214` death entry) ·
  `BoarBodyRig.cs` (`:19-26` the never-write-the-root contract, `:39-46` part indices + `PartCount = 7`,
  `:67-75` the shipped amplitudes, `:114-178` `LateUpdate`, `:136-141` the `dead ?` branches) ·
  `SnakeBodyChain.cs` (`:54/65/67` slither + telegraph amplitudes, `:254-298` the pose loop, `:267` the lateral
  basis, `:275-280` the taper, `:292-295` head orientation) ·
  `Assets/Shaders/LowPolyVertexColor.shader` (`:150-167` the CBUFFER + the three default-0 float precedents,
  `:240` albedo assembly, `:247-252` the pre-lighting albedo-lerp precedent, `:275-278` lit + ambient) ·
  `Assets/Scripts/Editor/MovementCameraScene.cs` (`:2164` warm bark, `:2539-2542` snake tones, `:2562-2567`
  boar tones, `:2814` the four-tone head mesh) · `Assets/Settings/ZoneD_PostProfile.asset` (`:59-64` Bloom
  threshold 1.02 / intensity 0.25).
- **Docs:** **`team/erik-consult/enemy-hit-feedback-hitflash-particle-flinch.md` — CORRECTIONS block C1…C5**
  (the technique base; corrections authoritative over its body) · `.claude/docs/game-juice.md` §0 (amplitude is
  the whole game) / §1.1 (easing) / §1.4 (pooled faceted bursts, ≤12) / §2 (every hard-don't in §9) ·
  `.claude/docs/procedural-animation-verbs.md` (the additive-offset idiom, the `NormT` + zero-at-rest
  requirements, the PlayMode traps — and why the castaway chain does NOT apply to an enemy rig) ·
  `.claude/docs/art-direction.md` + `inspiration/2026-06-12_21h10_44.png`, `21h16_13.png` (looked at them — the
  high-key saturated world with broad flat facets and strong per-region tones that §3.1 protects) ·
  `.claude/docs/unity6-mastery.md` §2 / §GC · `.claude/docs/unity-conventions.md:211-213` (SRP-Batcher gate) ·
  `.claude/docs/lowpoly-quality.md` (chunky faceted particle shapes).
- **Uma specs:** `enemy-hp-read-spec.md` (§1 divides the labour; §1.3 amends its §3.2) ·
  `combat-cluster-design-brief.md` §1.2 (refined by §3.1) / §2.4 (refined by §5) / §2.5 / §2.6 / §4 ·
  `style-guide-v2.md` §5 (sub-1.0) / §6 (warm bias + saturation) · `hp-hud-polish-spec.md` §2.3-§2.4 (the
  player-side wince + DoT debounce this deliberately does not copy).
- **Code read for §16 only, at `fb2ac245fc419d442a474c5d2f970535fa884743`** (one commit ahead of this branch's
  base `39ee4e6`; `git diff --name-only 39ee4e6 fb2ac24` touches no file under `Assets/`, so every constant is
  identical on both): `Assets/Scripts/Editor/MovementCameraScene.cs` — the `BuildBoar` const block
  (`BoarBodyLength 1.1`, `BoarBodyRadius 0.28`, `BoarHeadLength 0.42`, `BoarHeadNeckR 0.22`,
  `BoarGroundClearance 0.62`), the part offsets (head `(0, 0.02, 0.72)`, tail `(0, 0.14, −0.58)`), the snake
  consts (`SnakeBodyLinks 12`, `SnakeLinkSpacing 0.14`, `SnakeNeckRadius 0.115`, `SnakeHeadLength 0.26`), and
  `BuildCombat`'s `BuildSnake` → `BuildBoar` ordering (the §16.1 C3 pair) ·
  `Assets/Scripts/Editor/LowPolyMeshes.cs` — `BoarBody` / `BoarHead` ring tables, `BoarTail` (`tip =
  (0, −radius×0.6, −length)`), `EmitBoarTusk` (`apex = base + (0, height, height×0.35)`), `EmitBoarEar`
  (`apex = base + (0, r×1.4, −r×0.5)`) · `BoarBodyRig.cs` — the `LateUpdate` pose loop (per-part
  `localRot × Quaternion.Euler`, `part.position` from `_homePos`), the tail `wag = Sin(Time.time×3.1)×8f`,
  `breatheAmplitude 0.015` · `BoarAI.cs` — `_health.Died += OnDied` **and** the per-frame poll backstop
  (`if (State != Dead && _health.IsDead) OnDied()`), the §16.7 second domain · `SnakeBodyChain.cs` —
  `slitherAmplitude 0.055`, `idleSwayAmplitude 0.012`, the `p += lateral × …` positional write, the
  `k = 1 − i/(telegraphLinks+1); k²` taper.
- **Bars / memories:** `team/quality-bars.md` **#2 / #7 / #9 / #10 (§ Bar 10 — the standard in full, and the
  four checks; C1 amplitude / C2 failure-independence / C3 comparison set / C4 two-sided artifact + the
  default-gameplay-framing table)** · `enemy-hp-read-spec.md` **§14 / §15** (PR #406 — §16 audits and partly
  withdraws §15.4) · `[[difficulty-settings-easy-medium-hard]]`
  · `[[sponsor-prefers-natural-lively-motion]]` · `[[active-input-not-proximity-auto-for-actions]]` ·
  `[[served-unverified-soaks-need-played-verification]]` · `[[verify-grounding-soaks-by-gameplay-cam-visual]]` ·
  `[[sponsor-rejects-unsoakable-placeholders]]` · `[[claim-removed-soak-shows-present-investigate-foundation]]`
  · `[[sponsor-danish-keyboard-layout]]` · `[[ci-paths-ignore-skips-the-whole-run]]` · DECISIONS 2026-07-22
  (boar soak PASS / bar #9), 2026-07-27 (the sequencing decision + "Prescribed-not-shipped"), 2026-07-01
  (settings-panel split).
