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
  tell).
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
| `dagger_wood` 6 slash ×0.75 | 4.5 | 0.47 | **0.90** | 6 |
| `axe` (stone) 14 slash ×0.75 | 10.5 | 0.72 | **1.08** | 8 |
| `spear_iron` 12 pierce ×2.0 | 24.0 | 1.00 | **1.30** | 9 |

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
orthogonal-axis rule and §4.5's non-interruptible `Windup` exist for this), **#10** (the flash's ~1.5–1.8× luma
step is hue-independent, so the desaturated capture must still show it — §3.1). Bars **NOT tested**: **#1**,
**#3**, **#4**, **#5**, **#6**, **#8** — no world, weapon-material, real-world-feature, in-hand-sizing or
nudge-tool surface is touched.

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

**Shipped-build capture** — the ticket's (a)…(f) list stands. **Add (g):** the boar **~1.5 s after a single
axe hit, with the bleed still ticking**, showing base colour and a still body — the §2.1 discriminator, and the
only capture that can fail on the strobe.

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
- **Bars / memories:** `team/quality-bars.md` **#2 / #7 / #9 / #10** · `[[difficulty-settings-easy-medium-hard]]`
  · `[[sponsor-prefers-natural-lively-motion]]` · `[[active-input-not-proximity-auto-for-actions]]` ·
  `[[served-unverified-soaks-need-played-verification]]` · `[[verify-grounding-soaks-by-gameplay-cam-visual]]` ·
  `[[sponsor-rejects-unsoakable-placeholders]]` · `[[claim-removed-soak-shows-present-investigate-foundation]]`
  · `[[sponsor-danish-keyboard-layout]]` · `[[ci-paths-ignore-skips-the-whole-run]]` · DECISIONS 2026-07-22
  (boar soak PASS / bar #9), 2026-07-27 (the sequencing decision + "Prescribed-not-shipped"), 2026-07-01
  (settings-panel split).
