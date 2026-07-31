# Rock affordance direction — decorative scatter vs minable node

**Ticket:** `86cav8ybj` — *polish(art): disambiguate decorative scatter rocks from minable boulder/ore nodes*
**Scope of THIS doc:** DIRECTION only. No `Assets/**` change, no build, no capture, no test. Implementation
is the Unity-build half of the ticket and stays open behind the single build slot.
**Owner:** Uma (direction) → Drew/Devon (impl once Sponsor confirms the direction). **Reviewer:** Devon.
**Status:** direction proposal awaiting a Sponsor direction-pick (ticket AC2 forbids build-then-soak of a
guessed mechanism). Tagged `needs-soak`; unvalidated until a soak exercises it.
**Revision:** **round 3** — re-audit against the **2026-07-31 amendment to Bar 10** (the "a channel must VARY
between cued and non-cued instances" clause + the `86caz5na6` KNOWN-INCOMPLETE finding that variance is
tested as a *boolean, never a magnitude*), plus a full re-verification of every line cite at `origin/main`
**`750f190`**. Changed this round: **§0 NEW** (the re-audit and its verdicts) · **§8.0 NEW** (the default-
gameplay-framing arithmetic every channel is now judged at) · **§2.4 NEW** (a third shipped inversion —
the bare grass collar is on the wrong class; and the evidence that rules the collar OUT as a channel) ·
**§4 demoted** (the radial split is withdrawn as a *counted channel*, kept as an authoring invariant) ·
**§5.4 NEW** (D4 — the replacement second channel) · **§8 rewritten** (per-channel axis + magnitude +
failure-domain declaration; developer-verifiable ACs) · **§9** bar reworded to carry a magnitude clause ·
**§10** now carries a second-channel pick.
⚠ **§2/§2.1/§2.3/§4/§5/§6 line cites re-anchored — EVERY `MovementCameraScene.cs` cite in round 2 had
drifted** (that file grew ~140–280 lines since `840a1c6`; e.g. the ore-node mesh moved `:3005`→`:3284`,
the agent height `:4182`→`:4452`). Three `LowPolyZoneGen.cs` cites were off by one. Values are unchanged —
only the anchors moved. **Round 2's numeric findings all re-verified true at `750f190`.**
Unchanged and confirmed by round-2 review: §1, §2.2, §3, §7.

**Revision 3.1 (`86cazhvdc`) — Priya's four review NITs on PR #391, plus one defect her N1 exposed
upstream of itself.** Nothing in the channel audit moves: the two counted channels, the SCALE exclusion,
the §4 withdrawal and every magnitude claim stand exactly as reviewed. What changed:

- **§0.1 NEW — the measurement conventions are now DECLARED, once** (N1 + N2). The spec was carrying two
  width conventions without saying so: the ore and decorative figures used **full** planar extent, while the
  boulder figure was a **half**-width read (and on an unstated rate, so it reproduced exactly nowhere —
  §8.0 shows the two candidate half-width values it sits between). Full width is now the single declared
  convention; `b/a` is renamed for what
  it always was (a **stand ratio** against footprint radius, not a width ratio) and the `P` nominal each row
  uses is pinned. **Boulder width `76 px` → `~107–137 px`** (§8.0); §5.1's `0.69u × 0.41u` restated.
- **§8.0's px-per-unit rate CORRECTED, and this one is mine, not a NIT** — found while re-verifying against
  current `origin/main`. Round 3 computed screen scale as `720 px ÷ 45° = 16 px/°`, which assumes pixels are
  linear in *angle*; a pinhole camera is linear in *tangent*. The correct rate is
  `720 ÷ (2 × 14 × tan 22.5°) = 720 ÷ 11.598 = **62.1 px/u**`, not 65.5 — and it is exactly the figure the
  amended `team/quality-bars.md` Bar 10 now states as canonical (*"720 / (2 × 14 × tan 22.5°) ≈ 62 px per
  world metre"*), so round 3 as merged would have contradicted the bar it re-audits against. **Every absolute
  px figure in the spec drops by ~5%; every RATIO, every separation multiple and every conclusion is
  unchanged**, because the correction is a single scalar applied to a common base. The 1.6×-known-failing vs
  3.0×-proposed calibration, the ×1.43–1.74 amplification and the 5.82 : 1 / 1.92 : 1 pair are all
  arithmetically untouched — verified, not assumed.
- **AC1 now gates BOTH sides of the inversion** (N3), on one explicitly-stated common metric, with the
  minable side's tail honestly reported rather than over-gated. §8.2.
- **§4.3's no-radial-readout claim carries its grep** (N4). §10.4 states the §6 NavMesh-carve hazard so a
  Sponsor "yes" is actionable (Priya's fifth item). **No §10 flag is settled here — all three remain his.**

**Revision 3.2 (`86cazhvdc`) — Priya's `REQUEST_CHANGES` on PR #391, scoped to one item, plus her three
round-4 NITs.** Her review verified the 3.1 ruler correction and re-derived every rescaled px figure
independently; **nothing in that rescale, and nothing in N1/N2/N4, is re-opened here.** The block was that
**§10.3 recommended the one option that fails §8.2's own gate**, on margins computed against a metric the
gate does not use. What changed:

- **⛔ §10.3 gains the gate column, and 3.1's recommendation of option B is RETRACTED.** B (`q = 0.72`)
  drives the decorative stand-ratio ceiling to **`S = 0.813`** against a minable floor of **0.787** — the
  populations overlap and **AC1c is red**. Every other lever is enumerated and none rescues it in scope: the
  scale cap moves `S` by *exactly zero* (`s` cancels), zero tilt still leaves B at 0.800, and the only
  arithmetic repair (re-pinning the sink 0.08 → 0.120) hands back ~75 % of the floor-visibility advantage B
  exists to buy. **B is NOT deleted** — the worry it answers is real, and the pick stays the Sponsor's; it
  is now costed instead of recommended blind. New finding: that worry has an **AC1c-neutral** lever already
  named in §5.1 — raise the scale **floor** (`s` cancels, so it is free on the gate; +0.10 of floor = +0.55 px).
- **⛔ §8.2's AC1c margins RESTATED on AC1c's own metric, and the gate turns out to be SOUND.** 3.1 reported
  a decorative ceiling of `0.79` meeting a boulder floor of `0.79` — zero margin, which is not a testable
  gate. **Those two were on different metrics:** `V·q/P` is sink-free *and* tilt-free; the gate's
  `S = bounds.max.y ÷ max(extents.x, extents.z)` carries both. Correctly measured, option A's ceiling is
  **0.701**, giving a **real +0.086 margin** (+0.048 on the tilted-AABB bound). **The alarm was over-stated
  and the gate needs no rescue** — it needed to be measured on the right quantity.
- **§8.2's named lever gains a NUMERIC SELF-TERMINATING TRIGGER** (Priya's PL call): an explicit
  gap-threshold ladder with one lever pull (`q → 0.55`), a PASS-with-defect branch, and a hard STOP that
  forbids touching a minable value. The `0.05` threshold is *derived* from the tilted-AABB modelling band,
  not picked. ⚠ **`q` itself is NOT pulled here — it is a §10.3 dial and it stays the Sponsor's.**
- **§0.1's `P` pin now states its DIRECTION** — worst case is `P = 1.20` on an *apex* row but `P = 0.96` on a
  *stand-ratio* row, because `P` is the denominator there. Both were already used correctly; neither was
  declared, which is the same unstated-convention class as N1.
- **Round-4 NITs closed:** §5.1's `1.67× → "twice that"` chain (landed at 3.33, stated 4.07) replaced with
  the recomputable `2P/(V·q) = 3.55` → tilt+sink → `4.07` chain · §3's categorical-vs-magnitude sentences
  now define the distinction in the same breath · §8.0's "20u × 11u ground window" corrected to
  **~20.6u × ~14u** (11.6u was a *frame*-plane height, not a ground depth — the same plane-mixing class the
  ruler fix closed, and conservative, so nothing built on it moves).
- **Cite drift repaired:** **PR #395 renamed the `quality-bars.md` heading revision 3.1 cited.** Repointed to
  the sub-§ that actually carries the figure (§11), and 3.1's "#386 invalidated no § anchor" note qualified.
- **Not settled, deliberately:** §10.2, §10.3 and §10.4 all remain open Sponsor picks. Making B's arithmetic
  honest is not the same as choosing among the options.

⚠ **KNOWN INCOMPLETE, declared rather than discovered later.** `team/quality-bars.md` gained a
**Bar 10 — the four checks** section (`86caz5na6`, merged in PR #386) *after* this spec's round-3 audit was
written. C1 (amplitude stated as the **delta** between cued and non-cued, in px at a stated framing), C2
(failure independence), C3 (the comparison set) and C4 (the two-sided artifact) are **not** re-audited here —
revision 3.1 is scoped to the four NITs. Two are already substantially satisfied by construction (§8.1's
failure-domain row ≈ C2; §8.2's populous-both-classes note ≈ C3), and this revision's AC1 change moves the
spec *toward* C4's two-sided posture. **C1 is the open one**: this spec states magnitude as a *ratio
separation* (5.82 : 1 vs 1.92 : 1) where C1 asks for a pixel **delta** plus a fraction of the object's own
on-screen extent. **A round-4 C1 re-audit is OWED before this direction is called Bar-10-clean.**

---

## 0.1 Measurement conventions — declared once, and every figure in this spec is on them

Round 3 carried two width conventions without naming either, which is how a boulder came to be quoted at
~60% of its width (N1) and how a table of correct figures came to be glossed with a sentence they
contradict (N2). Both are convention defects, not arithmetic defects — each figure was individually
reproducible from *some* convention, which is exactly why the pair survived a careful review. The
conventions below are now fixed;
**any figure in this document that cannot be recomputed from them is a defect, not a variant reading.**

| Term | Convention | Why this one |
|---|---|---|
| **Width** | **FULL planar extent** — tip to tip across the ground, i.e. `2 × planar half-extent`. Every px width, every world-unit width, and every plain-language *"wide" / "wider"* in this spec. | It is what AC5 measures: a capture shows you the whole silhouette, not half of it. |
| **Height** | **Apex above ground**, NOT full vertical extent. | `LP_Rock` meshes are centred on the ground point (§5.1), so half the mesh is buried and never renders; minable meshes are *lifted*, so more than half does. Full vertical extent would count buried geometry as a cue. |
| **Radius** | A half-extent by definition, always labelled *radius*, and never quoted as a width. | §2's table and every `FacetedRock(r, …)` argument. |
| **`b/a` — the STAND RATIO** | `b` = apex above ground; `a` = planar **HALF**-extent (the footprint radius). Threshold **1.0**. **It is NOT a width ratio.** | It answers *"does this stone's top clear its own footprint radius?"*, and it is the **gate** metric because `Renderer.bounds.extents` is natively a half-extent (AC1). |
| ⛔ **`S` (MEASURED) vs `V·q/P` (DERIVED) — revision 3.2, and this is the root of the defect Priya's `REQUEST_CHANGES` found** | **`S` = `bounds.max.y ÷ max(extents.x, extents.z)`** — the stand ratio as AC1 actually reads it, carrying tilt **and** sink. **`V·q/P`** is the ratio of the *nominal semi-axes*, carrying **neither**. **They are the same number ONLY for a class with no tilt and no sink.** Quote `S` for anything gate-related; quote `V·q/P` only for the categorical `< 1.0` argument. | The minable classes take no tilt and no sink, so for them `S = (lift + V)/P` exactly and the two forms coincide. **The decorative class takes both**, so they diverge — at option A's ceiling, `V·q/P` = 0.7925 but `S` = **0.701**. Revision 3.1 compared a decorative `V·q/P` against a minable `S` and reported a zero margin that does not exist; the real margin is **+0.086** (§8.2). A margin is only a margin when both sides are the same quantity. |
| **Conversion** | Full-width flatness = `2 ÷ (b/a)`, before sink and tilt. | So the two never have to be guessed at from each other. |
| **`P` (planar half-extent factor, §2.3)** | **Nominal and floor rows: `P = 1.00`. Worst case: `P = 1.20` on an APEX row, `P = 0.96` on a STAND-RATIO row.** Stated at every use. | All three sit inside §2.3's measured `P` range (0.96–1.38). ⚠ **The two worst cases pull in opposite directions and revision 3.1 did not say so** (revision 3.2): a *tallest-slab* apex worst case takes `P` HIGH, because tilting a wide shape raises its apex through the `a·sin θ` term; a *stand-ratio* worst case takes `P` LOW, because `P` is the **denominator** of `b/a`. Verified rather than asserted — at the option-A ceiling draw, `S` is **0.70** at `P = 0.96` and **0.63** at `P = 1.20`, so 0.96 is the worst case for the gate. Every apex figure in §5.1/§10.3 reproduces on the 1.20 pin; every stand-ratio ceiling in §2.3/§8.2/§10.3 reproduces on the 0.96 pin. |

⚠ **The rule this replaces N2's gloss with, and it must not be re-inverted.** On the declared width
convention, **every stone class in this world is wider than tall** — an ore node is 59 px wide × 31 px tall
(§8.0) and says so itself. **No "taller than wide" claim may be derived from a `b/a` figure.** What inverts
between the classes is the **stand ratio** — whether the top clears the footprint radius — and what the
*player* sees is not an inversion at all but a **magnitude**: a 3.0× separation in apparent flatness against
a shipped 1.6× that already demonstrably fails (§8.0). The channel is unchanged; only the words are.

---

## 0. Round 3 — the re-audit the amended Bar 10 forces

`team/quality-bars.md` Bar 10 was amended on 2026-07-31, while this spec sat open. Two clauses land on it:

- **"A channel is a property that DIFFERS between an instance in the cued state and one that is not."**
  A property present on every instance is *style, not a cue*.
- **KNOWN INCOMPLETE (`86caz5na6`).** Tess constructed three cues that pass every clause of the amendment
  and are useless. The one that bites this spec: **variance is tested as a boolean, never a magnitude** —
  "a 2 cm marker + a 3 mm bob passes and is invisible at gameplay distance."

Round 2 declared two channels. Re-audited against the amendment **and against the actual shipped camera**
(§8.0 — pitch 55°, distance 14u, FOV 45°, all three verified at `750f190`), only one survives:

| Round-2 claim | Axis | Varies cued ↔ non-cued? | Magnitude at DEFAULT framing | Verdict |
|---|---|---|---|---|
| **Stand-ratio inversion** (top clears its own footprint radius vs does not — §0.1; round 3 called this "wider-than-tall vs taller-than-wide", which is wrong on the declared width convention) | **FORM** | Yes — categorical at every draw (§2.3) | **AMPLIFIED ×1.43** by the 55° down-pitch (§8.0). Apparent flatness 5.8 : 1 vs 1.9 : 1 | **HOLDS — the load-bearing channel** |
| **Apex-height ratio** (≥2×, later "nominal 3.5×") | **SCALE** — and SCALE is *silhouette geometry*, the same axis family as the stand-ratio inversion | Yes, but nominal only | **SUPPRESSED ×0.57** by the same pitch. Worst case: 18 px vs 24 px | **NOT a second channel** — round 2 already withdrew the guarantee; round 3 additionally forbids **counting** it, because shape + height is this spec's own bob-and-sway |
| **Radial domain split** (decorative r ≥ 17u, minable r ∈ [9,17]) | **POSITION** | Yes — real, shipped, per-instance | **ZERO.** There is no radial readout, no minimap, and the island is 292u across; a player at ground level cannot see the island centre. The variance is true and produces **no pixels** | **WITHDRAWN as a counted channel** — kept as an authoring invariant + guard test (§4.2, D2a) |

**So round 2, honestly re-scored, ships ONE perceptible channel.** That is precisely the failure the ticket
exists to avoid, arriving one level up: round 2 counted channel *types* correctly and channel *magnitudes*
not at all — the same error, in a different currency, as the `86caz5na6` sword cue that rode bob + sway.

⚠ **This is a self-correction, not a reviewer's finding.** Devon's round-2 review (`a4db32f`) confirmed §4,
and it was correct against the bar *as written at the time*. The bar moved.

**Round 3's job is therefore exactly one thing: supply a second channel that has measurable magnitude at
default gameplay framing, on an axis that is not FORM.** §8.0 is the ruler; §5.4 is the proposal; §10.2 is
the Sponsor pick, because which one it should be is a taste call this spec is not entitled to settle.

---

## 1. The tonal anchor

> **Far Horizon's shoreline is a place where stone is either LYING THERE or STANDING THERE.**
> Loose stone lies down — it is litter the sea and the weather left flat in the grass, and you walk over it
> without thinking. Stone worth a pickaxe stands UP out of the ground, alone, rooted, taller than you can
> step over. The player should learn that in the first two minutes without being told, and never unlearn it.

The failure this ticket names is not "the player clicked the wrong rock." It is that the world currently
makes a **promise it does not keep** — every grey lump in the grass carries the same posture, the same
proportion, the same grey, and one of the four classes silently has no verb behind it. The fix is to give
the non-interactive class a **posture of its own**, so a swing is never offered to something that cannot
take one.

Everything below serves that anchor. Any beat that does not reinforce *lying-down vs standing-up* is cut.

---

## 2. The measured problem — there is currently NO discriminating channel

Four distinct stone classes ship today. Only one of them is non-interactive, and it sits in the **middle**
of the size distribution. **All values re-fetched from source at `origin/main` `750f190` (round 3); every
`MovementCameraScene.cs` anchor below is NEW — the round-2 anchors, taken at `840a1c6`, have all drifted.**
Derived numbers show their arithmetic.

⚠ **Every "derived" figure in this table is a NOMINAL figure** — it assumes a `FacetedRock` half-extent
equal to its `radius` argument. That assumption is **false**; the real factor spans ~0.63–1.29. **§2.3
corrects it, and the corrected numbers — not these — are what §5.1 / §8 / §9 / §10 now quote.** The table is
kept in nominal form because the *relative* diagnosis (one silhouette language, colour arguing backwards,
the ore node as the median decorative rock) is unaffected by a factor that applies to all four classes alike.

| Class | Object | Mesh (source) | World planar radius (nominal) | Apex above ground (NOMINAL — see §2.3) | Verb |
|---|---|---|---|---|---|
| Pickup pebble | `LP_Stone` | `FacetedRock(0.22, jitter 0.34)` (`LowPolyZoneGen.cs:1573`) | 0.22 × [0.35, 0.80] = **0.077–0.176u** (`:1164`) | ≈ same | `E` → 1 stone |
| **Decorative scatter** | `LP_Rock` | `FacetedRock(0.55, jitter 0.38)` (`:1324`) | 0.55 × [0.55, 1.55] = **0.303–0.853u** (`:1031`) | ≈ radius = **0.30–0.85u** (centred at the ground point) | **NONE** |
| Minable ore node | `OreNode` | `FacetedRock(rockRadius, 0.42f, seed)` (`MovementCameraScene.cs:3284`, radius const `:3275` = `0.58f`) | **0.58u** (fixed) | lift `radius×0.55f` (`:3277`) + mesh half-extent → **0.864u nominal / 0.682u worst case** (§2.3) | pickaxe → mine |
| Minable boulder | `Boulder` | `FacetedRock(radius, 0.40f, seed)` (`:3457`, radius draw `:3448` = `1.05f + 0.30f×rnd`) | **1.05–1.35u** | lift `radius×0.45f` (`:3449`) + half-extent = `radius×(V+0.45)` → **1.46–1.88u nominal** | pickaxe → mine |

Player `NavMeshAgent.height` is **1.8u** (`MovementCameraScene.cs:4452`; the avatar root is scaled to it,
`:4476`) — so "0.85u" is roughly **mid-thigh** on the castaway and "0.90u" is **hip**. Those two are the
same read at gameplay orbit framing.

⚠ **One round-2 number corrected here, not just re-anchored.** Round 2 gave the boulder apex as
`radius × 1.45 = 1.52–1.96u`, which used a vertical half-extent factor of `V = 1.0`. §2.3 establishes
`V` nominal = **0.94**, so the honest nominal is `radius × 1.39` = **1.46–1.88u**. The conclusion
(the boulder is by far the tallest stone) is unaffected.

**Channel-by-channel audit — every one is empty:**

- **FORM (silhouette).** All four classes are the *same function*, `LowPolyMeshes.FacetedRock`, at
  jitter 0.34 / 0.38 / 0.40 / 0.42 — a spread of 0.08 on a single parameter. There is no silhouette
  language separating them; there is one language at four sizes.
- **FORM (size).** The ore node's 0.58u radius lands at the **50.5th percentile** of the decorative band
  ((0.58 − 0.3025) / (0.8525 − 0.3025)). The ore node is, dimensionally, *the median decorative rock*.
  The boulder's floor is only **1.05 / 0.853 = 1.23×** the decorative ceiling — and decorative rocks are
  built in **clusters of 2–4** (`:1025`) spread over ±1.8u (`:1028-1029`), so a cluster presents **more**
  visual mass than one boulder. Size, as shipped, argues the wrong way.
- **FORM (attitude/seating).** Decorative rocks take a yaw + a **0–10° tilt** (`:1317-1318`) and a
  **uniform** scale (`:1319`), centred at the ground point. Minable nodes take yaw only and are lifted
  (`radius×0.45` boulder `:3449` / `radius×0.55` ore `:3277`). Both therefore read as *the same
  half-embedded chunk*, upright, ungraded.
- **POSITION — global (radial).** ⚠ **CORRECTED in round 2, then DEMOTED in round 3 (§0, §4.3).** Decorative
  outcrop *centres* are hard-rejected below `spawnClearR + 4f` (`:1024` — round 2 cited `:1023`, off by
  one), and `spawnClearR = 13f` (`:981`) — so **no decorative outcrop centre exists below r = 17u.** Both
  minable pools sit in the **9–17u** walkable loop annulus (ore `:3201`, boulders `:3382` — the same
  `9.0 + rng.NextDouble() × 8.0`). The only bleed is the per-rock ±1.8u offset (`:1028-1029`), which lets an
  *individual* slab reach r ≈ 17 − √(1.8²+1.8²) = **14.45u**. The split is real, shipped, and unguarded —
  **but it produces no pixels at gameplay framing, so round 3 stops counting it as a channel.** §4.3.
- **POSITION — local (company/spacing).** *New in round 3; this is the axis §5.4 builds on.* Decorative
  rocks are authored in **outcrops of 2–4** (`n = 2 + rnd.Next(0, 3)`, `:1025`) with each member offset
  `±1.8u` in x and z (`:1028-1029`) — so a decorative rock almost always has a **peer within ~1.2–1.9u**.
  Minable nodes are the opposite by construction: ore rejects any position within **3.0u** of another ore
  node and **3.5u** of a landmark (`:3210`, `:3207`); boulders reject within **4.5u** of another boulder and
  **4.0u** of an ore node (`:3391`, `:3388`). **Every minable node stands with at least 3.0u of clear ground
  around it; every decorative rock is one of a huddle.** That contrast is live today — and unlike the radial
  split it is *visible in a single frame*. It is currently **not categorical** (§5.4 fixes that) and is
  guarded by nothing.
- **COLOUR.** `RockCol = (0.62, 0.60, 0.555)` (`LowPolyZoneGen.cs:92`) and
  `BoulderStoneGrey = (0.62, 0.60, 0.555)` (`MovementCameraScene.cs:3325`) are **bit-identical**.
  `OreRockGrey = (0.50, 0.48, 0.45)` (`:3146`) is *darker* than the decorative rock — so the minable ore
  node is the dullest stone in the world and the decorative prop is the brightest.

### 2.1 The one live attract channel is currently painted on the WRONG object

`RockVertexColorMat` — the **decorative** scatter-rock material — opts in to:

- `_RimIntensity = RockRimIntensity = 0.12f` (`LowPolyZoneGen.cs:96`, set at `:1937`) — the RCK-1
  caught-sun silhouette highlight (ticket `86cahhfkc`), and
- `_AOStrength = 0.5f` (same method) — crevice contact-darkening.

The **minable** materials set `_Tint` and nothing else: `boulderMat` (`MovementCameraScene.cs:3345`),
`rockMat` (`:3164`) / `veinMat` (`:3166`) for the ore pool.

So the only silhouette-attract term the project has built is **live on the prop you cannot mine and dead
on the two you can.** This is Bar 10's second motivating instance (Devon, PR #349 — "a find-in-world
attract cue lost its Fresnel rim") in mirror image: the cue did not merely collapse, it **inverted**.
Correcting the inversion is necessary hygiene. It is *not* the cue — see §5.3 for why.

### 2.2 A behavioural discriminator already exists, and is invisible

Minable nodes carve the NavMesh at runtime so the player is **blocked at the stone's surface**
(`MineOre.MovementCarveWorldRadius` `:711` = `Mathf.Max(CarveFloorRadius, footprintRadius + CarveClearance −
CarveAgentStandoff)`, with `CarveClearance = 0.15f` `:702`, `CarveAgentStandoff = 0.40f` `:703`,
`CarveFloorRadius = 0.20f` `:704` — **all four re-verified at `750f190`**). Decorative scatter rocks carve
nothing and hold no collider — **the player walks straight through them.**

The world therefore *already teaches* the distinction — by bumping into it. The affordance exists in the
simulation and is simply not readable by eye. **That fixes the direction of the answer:** the visual cue
must agree with the physics that already ships. Minable = solid, seated, has mass, you go around it.
Decorative = loose, low, you walk over it. Any cue that contradicts that (a glow, a marker, a hue) adds a
second vocabulary on top of a truth the world is already telling.

### 2.3 CORRECTION (round 2, re-verified round 3) — `FacetedRock` apex ≠ radius, and the ≥2× floor was never a floor

Round-1 arithmetic treated a `FacetedRock(r, jitter)` as having a vertical half-extent of `r`. It does not.
`LowPolyMeshes.FacetedRock` (declared `LowPolyMeshes.cs:335`; the displacement block `:373-388` — **cites
re-verified unchanged at `750f190`**) applies, per instance and per vertex:

- `sy = 0.85f + rnd × 0.18f` → **[0.85, 1.03]**, one draw per instance (`:375`);
- `rj = 1f + (rnd − 0.5f) × jitter` → **[1 − j/2, 1 + j/2]**, one draw **per vertex** (`:382`);
- an absolute isotropic wobble of `radius × jitter × 0.22f`, i.e. **±`radius × j × 0.11`** per component
  (`:386-388`).

So the vertical half-extent factor is **`V = sy × rj(pole) ± j × 0.11`**, and the planar one is
**`P = sx × max(rj over the equatorial verts) ± j × 0.11`** with `sx = 0.92 + rnd × 0.20` (`:373`).

| Class | `jitter` | `V` floor | `V` nominal | `V` ceiling |
|---|---|---|---|---|
| Pickup pebble | 0.34 | 0.85×0.83 − 0.037 = **0.668** | 0.94 | 1.03×1.17 + 0.037 = **1.242** |
| Decorative scatter | 0.38 | 0.85×0.81 − 0.042 = **0.647** | 0.94 | 1.03×1.19 + 0.042 = **1.268** |
| Boulder | 0.40 | 0.85×0.80 − 0.044 = **0.636** | 0.94 | 1.03×1.20 + 0.044 = **1.280** |
| Ore node | 0.42 | 0.85×0.79 − 0.046 = **0.626** | 0.94 | 1.03×1.21 + 0.046 = **1.293** |

**The asymmetry that matters, and it is the whole answer to "name the spread."** The mesh is a subdiv-1
octahedron — **"8 faces -> 32 faces, 6 -> 18 verts"** in the generator's own comment (`LowPolyMeshes.cs:348`;
the consuming comment is `LowPolyZoneGen.cs:1320`): one vert at `n.y = 1`, four at `0.7071`, eight at `0`,
then mirrored.
So `V` is set by **essentially ONE vertex** (the pole; the next ring only overtakes it when
`0.7071 × rj > rj(pole)`, i.e. `rj(pole) < 0.841` — reachable but uncommon), which gives `V` a genuinely
low, broadly-spread tail. `P` is the **max over twelve** planar-bearing verts, so it concentrates near the
top of its range (≈ 0.96–1.38, nominal ≈ 1.00–1.05). **Vertical extent is one draw; planar extent is a
maximum of twelve.** That single fact decides which of this spec's two FORM guarantees survives:

- **The stand-ratio inversion SURVIVES every tail on the DECORATIVE side.** It fails only if `V × q ≥ P`
  (`q` = the D1 squash). At
  `q = 0.60` that needs `V ≥ P / 0.60`, i.e. `V ≥ 1.60` at realistic `P` — and `V` cannot exceed **1.268**.
  Unreachable: a decorative slab's `b/a` caps at `1.268 × 0.60 / 0.96` = **0.79**, always under 1.0. That
  side is **categorical at every draw**, which is exactly why §3 leads with it. ⚠ **The MINABLE side is
  not symmetric, and revision 3.1 stops implying it is (N3):** `b/a` there is `(lift + V)/P`, whose own tail
  reaches `(0.45 + 0.636)/1.38` = **0.79** for a boulder — so the *inversion* is a strong nominal that must
  be **measured**, not derived. §8.2's AC1c is that measurement, with a named lever if it comes back tight.
  ⛔ **DO NOT read those two `0.79`s as a margin — revision 3.2.** They are on **different metrics**: the
  decorative one is `V·q/P`, which is sink-free *and* tilt-free, while the boulder one is `(lift + V)/P`,
  which is what `bounds.max.y` actually returns for a class that takes neither. Restated on AC1c's own
  metric the decorative ceiling is **0.701**, not 0.79, and the real margin is **+0.086** — see §8.2. The
  figures above are correct for the *categorical* `< 1.0` claim they are making here; they are **not** the
  gate's numbers, and §8.2 is the only place the gate's margin may be quoted from.
- **The ≥2× apex-height separation DOES NOT survive.** Corrected numbers in §5.1. It holds ~3× nominal and
  degrades to **~1.3× worst-case**. It was quoted in round 1 as a floor; it is a *nominal*, and the
  round-1 figures (2.7× / 2.1×) were neither nominal-nominal nor worst-case but an inconsistent middle.

**Consequences, applied throughout this round-2 revision:** the height ratio is demoted from a *derived
guarantee* to a **measured report with a nominal target**; the impl must encapsulate the shipped
`Renderer.bounds` (the idiom `MineOre.TryPlanarFootprint`, `MineOre.cs:812`, already uses) and print the
achieved **minimum** ratio and the achieved **maximum** `b/a` **stand ratio** (§0.1 — apex above ground ÷
planar half-extent, *not* a width ratio) across every instance — failing the
gate on any instance whose stand-ratio inversion is violated, and *reporting* (not failing) the height ratio.
§9's candidate bar and the `team/quality-bars.md` row are reworded to match.

**One correction to the reviewer's own figure, for the record:** the round-2 review estimated the worst
realized pair at "~1.7×" against "the shortest of the ~7 deterministic ore nodes." Seven is the **boulder**
pool (`BoulderPoolSize = 7`, `MovementCameraScene.cs:3319`). The **ore** pool is
`OreNodePoolSize => IronDifficultyPresets.Easy.OreNodeCount` (`:3141`), and
`Easy = new IronDifficulty(oreNodeCount: 24, …)` (`Assets/Scripts/Runtime/Items/IronDifficulty.cs:54`)
= **24 placed** (14 active at the Medium default — `mine.activeNodeCount = IronDifficultyPresets.Medium
.OreNodeCount` `:3237`, `Medium` = 14 at `IronDifficulty.cs:55`), each with its own mesh seed
`86300 + i × 17` (`:3216`) — so the low `V` tail is sampled across **24** independent draws, not 7, and the
honest worst case is **lower** than 1.7×, not higher. Figures in §5.1.

### 2.4 NEW (round 3) — a THIRD shipped inversion: the bare grass collar is on the wrong class too

§2.1 found the rim highlight live on the prop you cannot mine. The same inversion exists in the ground
cover, and nobody has named it:

- Every placed `LP_Rock` records `(x, z, RockFootprintRadius × scale)` into `rockFootprints`
  (`LowPolyZoneGen.cs:1033`, radius const `:1388` = `0.55f`), and the grass loop rejects any tuft inside
  that footprint **+ `GrassRockPad = 0.35f`** (`:1391`, test `OverlapsAnyRock` `:1399-1405`, called at
  `:1053`). So a decorative rock at nominal `s = 0.90` sits in a **bare, grass-free collar of radius
  `0.55 × 0.90 + 0.35 = 0.845u`.**
- `rockFootprints` is populated **only** from the `LP_Rock` loop. Ore nodes and boulders are authored in a
  different file at a different bootstrap step (§4.1) and contribute nothing to it — so **minable stone gets
  no collar at all.** The class that is *scenery* owns the clearing; the class you walk up to and swing at
  does not.

**Two consequences, and they pull in opposite directions — both matter.**

1. **This is NOT a usable channel, and the evidence rules it out rather than taste.** Grass ships at
   `clumpTarget = 360` (`:1044`) spread over `plantOuterR = IslandShoreR + CoastIrregAmp = 120 + 26 = 146u`
   (`:979`, `:237`, `:271`) — roughly **0.005 tufts per u²**. The expected number of tufts that would land
   inside a 0.58u-radius ore node is about **0.006**. The "collar" the minable class lacks is a distinction
   with, on average, no tuft in it to notice. Inverting the rule would buy ~zero pixels and would cost a
   grass-loop RNG shift plus a capture re-baseline. **Ruled out — see §7.** It would also graze the #130
   soak defect the `GrassRockPad` rule exists to fix ("grass sprouting through a stone"), which is a
   Sponsor-rejected state, not a neutral one.
2. **But the collar is what PROTECTS D1, and that makes it load-bearing.** Grass blades stand
   `GrassClump(0.55f, …)` (`:1344`) × `localScale ∈ [0.5, 1.0]` (`:1054-1055`) × the per-blade
   `0.7–1.3` factor (`LowPolyMeshes.cs:1204`, inside `GrassClump` `:1192`) = **0.19–0.72u tall**, nominal
   ≈ 0.30u. A D1 slab's nominal
   apex is **0.243u** (§5.1) — *shorter than the grass around it*. The only reason a flattened slab does not
   vanish into the meadow is that it stands in its own 0.845u bare collar, and that collar scales with the
   rock (`RockFootprintRadius × scale`), so it survives the §5.1 scale cap automatically.
   **Therefore: `RockFootprintRadius`, `GrassRockPad` and the `OverlapsAnyRock` grass reject are now
   CHANNEL-CRITICAL. Do not shrink, remove or "optimise" any of them in the D1 PR.** Round 2 already said
   "do not fix this bookkeeping"; round 3 upgrades the reason from *it stays correct on its own* to *the
   cue depends on it*.
   Supporting detail, same direction: grass tufts are authored `castShadows: false` (`:1345`, rationale
   comment `:1341-1342`) while
   rocks keep shadow casting ON — so at gameplay framing a slab's contact shadow is the only shadow at that
   scale, and it reads as ground contact against a shadowless meadow. §5.1's "keep shadows ON" is not a
   nicety.

---

## 3. PRIMARY CHANNEL — **FORM: attitude + aspect ratio** ("lying down vs standing up")

> **One sentence:** *Decorative stone lies DOWN — a flat lozenge whose top never clears the ground it
> covers, tilted, half-buried, in a huddle of peers. Minable stone stands UP — its top clears its own
> footprint, level, alone.*

This is **FORM** in Bar 10's first rank, delivered as a **stand-ratio inversion**: the decorative class
crosses from *apex above its footprint radius* to *apex below it* — `b/a` from ≈ **1.49** (ore) and
≈ **1.39** (boulder) to ≈ **0.56**, on opposite sides of 1.0 (§0.1, §8.1). That is a *categorical* read, not
a magnitude read — the eye does not have to compare two objects to judge it, which is what makes it work at
a glance on a lone rock.

⚠ **The distinction that keeps this paragraph and the next from contradicting each other** (revision 3.2,
Priya's round-4 NIT 2). *Categorical* and *magnitude* here answer two different questions and the spec needs
both, so the words must not be used interchangeably:

- **Categorical** describes the **judgement the player makes**: it is a *single-object* verdict — "does this
  one's top clear its own footprint?" — answerable with no second rock in frame. That is the property that
  makes the cue work on a lone stone, and it is what the `b/a` crossing of 1.0 encodes.
- **Magnitude** describes the **pixels that carry that judgement**: how far from the threshold the object
  sits, and therefore whether the eye can resolve which side it is on at all. Bar 10's `86caz5na6` gap is
  that a crossing can be real and invisible; a categorical property with no magnitude is exactly the 3 mm
  bob.

**So: categorical is why one rock is enough; magnitude is why the answer is legible.** The paragraph below
states the magnitude, and it is not a retreat from the categorical claim — it is the half that makes it
survive Bar 10.

⚠ **Say it in the right words, because round 3 said it in the wrong ones (N2).** On the declared width
convention **both** classes are wider than tall — an ore node is 59 px wide × 31 px tall (§8.0). The
inversion is in the **stand ratio**, and what the *player* reads is the **magnitude** it produces: a
decorative slab at ≈ 5.82 : 1 apparent flatness against an ore node's ≈ 1.92 : 1, a **3.0× separation**
against the **1.6×** the world already ships and that demonstrably fails (§8.0). Never restate this cue as
"taller than wide"; state it as *stands proud of its footprint* vs *lies flat across it*.

⚠ **ROUND-3 CORRECTION to this section's own framing.** Round 2 opened by saying the cue rides "two of
[FORM's] three named sub-axes at once (silhouette **and** size)." **Struck.** Silhouette-proportion and
size are not two channels; they are two descriptions of the same axis, and Bar 10's `86caz5na6` amendment
is explicitly about that arithmetic (bob + sway counted as two). **This section supplies exactly ONE
channel.** The second lives in §5.4, on POSITION. See §8.1.

**What it reads as for someone who cannot separate the hues** (the Bar 10 obligation): the cue is **100%
geometric**. Grey-scale the frame and it survives untouched, because it is carried by silhouette
proportion and ground contact, not by any tint, rim, or emission. A fully colour-blind player, a
desaturated capture, and a player at 4 m from a 24" monitor all get the same read: *the flat ones are
scenery, the ones that stick up are stone you can work.* Stated as the falsifiable test — **desaturate the
shipped-build capture; the cue must be entirely intact.** (Bar 10's own check.)

**Why this and not a re-authored mesh.** Of the four stone classes, exactly one — `LP_Rock` — has **no
gameplay contract attached**: no verb, no yield, no navmesh carve, no respawn timer, no capture harness, no
difficulty dial. It is also the class that is *dimensionally wrong* (§2, the median-decorative-rock
finding). So the whole cue can be bought by re-posturing the one class nobody depends on, using
**transform values only** — no new mesh, no chamfer pass, no material authoring, and therefore no
collision with `86cacewju`'s hero-prop bevel lineage.

---

## 4. ~~SECONDARY CHANNEL~~ → **AUTHORING INVARIANT — POSITION (global): the loop vs the horizon**

> ⚠ **ROUND 3 STATUS CHANGE. This section is no longer a channel.** It was round 2's second channel; §0
> withdraws that claim because the split produces **zero pixels at gameplay framing** (§4.3). Everything
> below is still true, still worth guarding, and D2a still ships — but it counts **zero** toward Bar 10's
> ≥2. The replacement second channel is §5.4.

> **One sentence:** *Stone you can work lives in the loop where you live. Stone that is only scenery lies
> out toward the horizon.*

### 4.1 What round 1 got wrong — two errors, and the second is the bigger one

**Error 1 — the ordering premise was inverted.** Round 1 asserted that the scatter runs *before* the
minable pools, and concluded the exclusion had to run minable-side. **The real order is the exact
opposite,** and I verified it myself at `main` (`b9abf7b`) rather than taking the correction on trust:

| # | Site | What it authors |
|---|---|---|
| 1 | `BootstrapProject.Run` **`:96`** `BuildBootScene()` (declared `:316`) → **`:559`** `MovementCameraScene.Author(camGo)` → `BuildOreNodes` **`MovementCameraScene.cs:583`** / `BuildBoulders` **`:595`** | **both minable pools, complete** |
| 2 | `BootstrapProject.Run` **`:105`** `WorldBootstrap.BuildEnvironment()` → **`WorldBootstrap.cs:158`** `LowPolyZoneGen.BuildZone` → **`LowPolyZoneGen.cs:645`** `ScatterIslandProps` → the rock loop **`:1019-1036`** → `BuildRock` **`:1304`** | **every `LP_Rock`** |

*(Round-3 re-anchor: round 2 cited the two `Author` call sites as `:410`/`:422` and `BuildChopTree` as
`:374`; all three drifted. The **ordering conclusion is unchanged** — see below.)*

The bootstrap states the order in its own comments — `BootstrapProject.cs:107-112` ("*BuildEnvironment has
authored the LowPolyScatter root … it did NOT exist at BuildChopTree time — the boot scene's
player/craft/chop are authored at line 96, the environment scatter only lands here*") and `:119-122`
("*BuildWiredStone (pre-scatter, BuildBootScene) … ScatterIslandProps (here, inside BuildEnvironment)*").
`BuildChopTree` is called at `MovementCameraScene.cs:547`, *earlier* in `Author` than `BuildOreNodes`
`:583` — so if the scatter root does not exist at `:547` it certainly does not exist at `:583`/`:595`.

**Consequence, stated as the reviewer stated it, because it is correct:** a minable-side reject list would
collect **zero** `LP_Rock` positions, the ring would never reject a candidate, no position would move, no
baseline would shift — **and every test would pass.** The starvation guard would have logged a
healthy-looking `2.6u` from a pass that examined nothing. That is precisely the silent-no-op failure mode
§8 claims immunity from, arriving through the placement path instead of the material path. Round 1's §8
declared two channels; it would have shipped one.

**Error 2 — and this one survives the re-siting: the channel has almost nothing to exclude.** The
reviewer's proposed remedy (a post-scatter cull, correctly sited) fixes the *mechanism* but not the
*premise*. §2's corrected POSITION bullet is why:

- decorative outcrop **centres** are hard-rejected below `spawnClearR + 4f` (`:1024`) and
  `spawnClearR = 13f` (`:981`) → **no outcrop centre below r = 17u**;
- both minable pools draw `rad = 9.0 + rng.NextDouble() × 8.0` → **r ∈ [9, 17]** (ore `:3201`, boulders
  `:3382`; both then place at `y = 0` — ore `:3204`, boulder `:3385`);
- the per-rock offset is `(rnd − 0.5) × 3.6f` on each of x and z (`:1028-1029`) → an individual slab
  reaches inward to at most **r = 17 − √(1.8² + 1.8²) = 14.45u**.

So a rock can only ever come near a node inside the thin `[14.45, 17]` band, and only from an outcrop whose
centre sits in `[17, 19.55]`. Outcrop centres are **areally** uniform (`rr = plantOuterR × √rnd`, `:1022`)
over `plantOuterR = IslandShoreR + CoastIrregAmp = 120 + 26 = 146u` (`:979`, `:237`, `:271`), so the
fraction of outcrops eligible to contribute at all is `(19.55² − 17²) / (146² − 17²) = 91 / 21027 ≈ **0.43%**`.
With 60 rocks (`rockTarget = 60`, `:1018`) in clusters of 2–4 (`:1025`) ≈ 20 outcrops, that is **≈ 0.09
eligible outcrops** across the whole island. **The expected number of decorative rocks within 2.6u of any
minable node is far below one.**

**Therefore a cull pass — however correctly sited — would cull nothing, and would be the same silent no-op
one layer further along.** I am not taking the suggested re-siting as the fix. The re-siting is *necessary*
(it is the only order in which either class can see the other, and §5.2 uses it), but it is not
*sufficient*, and shipping ~15 lines of culling machinery whose expected yield is zero would be exactly the
kind of beat this spec's §7 exists to refuse.

### 4.2 What POSITION actually is here — an invariant that already ships, unnamed and unguarded

Read the shipped radial rules as one table and the channel is already there:

| Stone class | Radial domain | Verb |
|---|---|---|
| Minable ore node | r ∈ [9, 17] (`MovementCameraScene.cs:3201`) | pickaxe → mine |
| Minable boulder | r ∈ [9, 17] (`:3382`) | pickaxe → mine |
| Pickup pebble | r ≥ 13 (`LowPolyZoneGen.cs:1155`, `spawnClearR`) | `E` → 1 stone |
| **Decorative scatter** | **r ≥ 17** centres (`:1024`, `spawnClearR + 4f`) | **NONE** |

**Every stone class that carries a verb reaches into the survival loop. The one class with no verb is the
one class excluded from it.** It is live in the shipped build today, was never named, never documented, and
is protected by **nothing**: a future scatter re-tune that lowers `spawnClearR`, widens the annulus, or
raises the ±1.8u cluster spread would silently dissolve it, and no test would notice.

⚠ **Round-3 correction to round 2's reading of the bar.** Round 2 called this "Bar 10's POSITION rank in its
literal form — *a fixed slot per kind*." That is the wrong reading. Bar 10's POSITION example is a **HUD
slot** — a fixed screen region the player looks at, where *"the third slot is lit"* is itself the read. Its
force comes from the slot being **in the player's field of view**. A radial band measured from an origin the
player cannot see, on a 292u island, has the *form* of a slot and none of its *function*. §4.3.

So the honest job for this channel is **not to build it. It is to name it, measure it, and lock it** — §5.2.

**Why this is a better outcome than round 1's design, not a retreat.** Round 1's minable-side reject would
have moved boulder and ore positions (different draws consumed), forcing a `BoulderVerifyCapture` +
`MineVerifyCapture` re-baseline in the same PR. The corrected reading needs **zero** placement change, so
the minable pools never move and **no capture baseline shifts.** The seed-42 stream is untouched either way
— but now trivially so, because nothing is added to or removed from any RNG.

**And the reviewer's seed-lock point is granted:** my round-1 prohibition ("*do not do it scatter-side*")
was over-broad. It is correct only about **rejecting inside the scatter loop** — that consumes different
draws and moves the island's trees. It is **not** correct about a **post-scatter pass**, which runs after
the seed-42 stream is fully consumed and therefore cannot perturb it. The project already has that idiom
four times over (`WireChopScatterRoot` `BootstrapProject.cs:113`, `WireStoneScatterRoot` `:123`,
`WireBerryBushes` `:134`, `WireWorldLookConsole` `:141`) — all of which exist *specifically because* the
scatter does not exist at `BuildBootScene` time. §5.2's measurement pass is sited there, as a fifth sibling.

### 4.3 NEW (round 3) — why this is an invariant and not a channel

Bar 10's amended definition asks two questions of a candidate channel. The radial split answers the first
and fails the second:

| Bar 10 question | Radial split |
|---|---|
| Does the property **differ** between a cued and a non-cued instance? | **Yes.** `r ≥ 17` vs `r ∈ [9,17]`, per instance, machine-checkable, zero overlap between outcrop *centres* and the minable annulus. |
| Is that difference **perceptible at the framing the player actually plays at**? (`86caz5na6`: variance is a magnitude, not a boolean) | **No — and not marginally.** It renders **0 px**. Radius-from-origin is not a visual property. The build has no minimap, no compass, no coordinate readout; the shore is 120u out and the island is 292u across, so from inside the loop the centre is not locatable by eye. A player standing next to a rock has no way to know whether they are at r = 15 or r = 19. |

**EVIDENCE for the no-radial-readout claim (N4) — it is a negative, so it is grepped, not asserted.** Run at
`origin/main` **`0f14b4f`**:

```
$ git grep -ilE "minimap|mini-map|compass" 0f14b4f -- Assets/Scripts | wc -l
0
```

Zero files across the entire runtime + editor script tree. Widening the same pattern to the **whole**
`Assets` tree returns exactly **two** hits, and neither is a UI surface — both are prose inside code
comments: `Assets/Shaders/GradientSkybox.shader:168` (*"the warmth follows the sun's compass bearing"*) and
`Assets/Tests/EditMode/SeededScatterVariationTests.cs:87` (*"per-instance seeds spread across the
compass"*). A grep for `coordinate|coords` over `Assets/Scripts` returns six files, all of them capture
rigs, editor asset-gen, or **screen**-space UI maths (e.g. `InventoryUI.cs:458`, *"coordinates (same space
as `PointerEvent.position`)"*) — **no world-position or radius readout is presented to the player
anywhere.** The claim this section rests a *withdrawal* on is therefore demonstrated, not inferred.

**The trap this closes, stated so the next spec does not re-open it:** a placement rule can be *perfectly
enforced, fully tested, and completely invisible*. Enforcement is not perception. Round 2 verified the rule
existed (correctly) and inferred that it therefore taught something (incorrectly). Round 2's own §8 caveat
had already half-seen this — *"POSITION alone provably does not teach the distinction… its role is
reinforcing confirmation"* — but a channel that teaches nothing on its own and shows nothing on screen is
not reinforcing anything; it is a **coincidence between two placement rules**. Worth locking, because a
world where scenery drifts into the mining loop is worse. Worth **counting**, no.

**What survives:** D2a (§5.2) — measure it, assert it, fail loudly if either set is empty. Unchanged and
still recommended. It is now filed under *world-layout hygiene*, not under the affordance cue.

---

## 5. Concrete values — Tier 1 (the whole recommendation)

**FOUR edits in round 3** (D1, D2, D3, and the new D4 — §5.4). All are **value/transform changes at existing
call sites**; none authors geometry or
materials.

### 5.1 D1 — Decorative scatter rock lies down (`LowPolyZoneGen.BuildRock`, `:1304-1326` + the scale line `:1031`)

| Knob | Today | Proposed | Why |
|---|---|---|---|
| `localScale` | `Vector3.one * scale` (uniform, `:1319`) | `new Vector3(s, 0.60f * s, s)` | The stand-ratio inversion. **RESTATED on the declared width convention (N1)** — round 3 gave this as "0.69u wide × 0.41u tall", which were both *half*-extents at the `s = 1.25` cap. **Chain CORRECTED in revision 3.2 (Priya round-4 NIT 1)** — 3.1's "`1/0.60` = 1.67×, and the visible aspect is *twice that*" lands at 3.33, not the 4.07 stated beside it. The recomputable chain, all three factors named: **`2P/(V·q)` = `2 × 1.00 / (0.94 × 0.60)` = 3.55 : 1** untilted and unsunk at the nominal draw (this *is* §0.1's `2 ÷ (b/a)` conversion, and `s` cancels out of it); then tilt raises the apex by ×1.129 and the proportional sink takes back `0.08 × 0.90` = 0.072u, netting **×1.15** on the flatness — so at nominal `s = 0.90` it is **0.99u full width ÷ 0.243u apex = 4.07 : 1 flat** (§8.0), reading **≈ 5.82 : 1 on screen**. The 1.67 figure is the squash reciprocal alone; it reaches 3.55 only after `1/0.94` for `V`, and 4.07 only after tilt and sink. |
| `scale` band | `0.55 + rnd × 1.00` → 0.55–1.55 (`:1031`) | `0.55 + rnd × 0.70` → **0.55–1.25** | Caps the apex; keeps the planar footprint generous so the world does not read emptier. |
| Tilt (Euler X/Z) | `rnd × 10f` each (`:1317-1318`) | `8f + rnd × 14f` → **8–22°** each | A tilted slab shows a **plane and an edge**; this is what kills the mound read (see the risk note). |
| Sink | none (centred at `GroundPoint`) | **`−0.08 × s` in Y — PROPORTIONAL, pinned (N3)** | Buries the low edge so it reads *settled into the grass*, not *placed on it*. **Proportional, not fixed** — see the pin below. |
| Shadow casting | ON (`MakeMeshObject` default) | **keep ON** | The contact shadow of a low slab is the ground-contact evidence. Do not "optimise" it off. |

`MakeMeshObject` parents the mesh at identity local transform (`:1424`, `SetParent(parent, false)` with no
local offset), so the mesh is centred on the `LP_Rock` origin and **apex above ground = mesh vertical
half-extent × `localScale.y` − sink.** Confirmed by read, because the whole §5.1 derivation rests on it.

**PIN (N3) — the sink is `−0.08 × s`, PROPORTIONAL, and this reverses the reviewer's suggested default.**
The review asked me to state FIXED explicitly so an implementer would not "improve" it to `−0.08 × s`. The
corrected §2.3 arithmetic says the opposite, and the number decides it: at the low tail
(`s = 0.55`, `V = 0.647`) the pre-sink apex is `0.55 × 0.647 × 0.60 × 0.55 = 0.117u`, so a **fixed** 0.08
sink consumes **68%** of the smallest slab and leaves a ~0.04u nub — buried, not settled. Proportional
leaves `0.117 − 0.044 = 0.073u` (≈0.085u once the tilt term is included), which is a stone in grass.
A larger stone settling deeper into turf is also the physically honest read. **Safety clamp:** the sink must
never exceed **40% of that instance's measured pre-sink apex** — free to enforce, because §2.3 already
requires the impl to encapsulate `Renderer.bounds` per instance.

**Resulting bands, CORRECTED per §2.3** (round 1 quoted `0.10–0.33u` on the false `apex ≈ radius`
assumption). Apex = `√(b²cos²θ + a²sin²θ) − 0.08 s`, where `b = 0.55 · V · q · s`, `a = 0.55 · P · s`, and
`θ` is the combined tilt of the local +Y axis (Euler X and Z each 8–22° → θ ≈ 11.3–31.1°). **Tilting a
flat-lying shape RAISES its silhouette** — an effect round 1 omitted entirely, and it costs ~+18% at
the ceiling. `P` per §0.1: **1.00** on the floor and nominal rows, **1.20** on the ceiling row (a tallest-slab
worst case correctly takes `P`'s high end, because the raise arrives through the `a · sin θ` term):

| | `s` | `V` | `θ` | `P` | Apex | **On screen at default framing** (§8.0, 35.6 px/u vertical) |
|---|---|---|---|---|---|---|
| Floor | 0.55 | 0.647 | 11.3° | 1.00 | **0.085u** | **~3 px** |
| Nominal | 0.90 | 0.94 | 21° | 1.00 | **0.243u** | **~9 px** |
| Ceiling (worst case) | 1.25 | 1.268 | 31.1° | 1.20 | **0.518u** | **~18 px** |

⚠ **New risk surfaced by the round-3 framing arithmetic — the FLOOR of the band, not the ceiling.** At
`s = 0.55` the slab renders **~3 px tall** and **~31 px wide** (full width `2 × 0.55 × 0.55 = 0.605u`
× 50.9 px/u, §0.1 + §8.0) in a 1280×720 frame. Combined with §2.4's
finding that the surrounding grass blades are 0.19–0.72u (nominal ~0.30u, i.e. **~11 px**), the smallest
slabs are *shorter than the grass beside them* by a factor of ~3. Round 2's named risk was "did they become
puddles"; round 3 adds a sharper one: **the smallest slabs may read as ABSENT rather than as scenery.**
This is a second, independent reason option B exists (§10.2) — and it argues that if the Sponsor's soak
verdict is "the shoreline got emptier," the correct lever is to **raise the scale FLOOR**, not to relax the
squash. Their bare grass collars (§2.4) are what keep them visible at all; do not touch that rule.

Against the ore node — the minable floor — at **0.864u nominal** and **0.682u worst-case**
(`0.58 × (V + 0.55)`; `rockRadius` const `:3275`, lift `radius × 0.55f` `:3277`), the honest apex-height
separation is:

| | Ratio | vs the round-1 claim |
|---|---|---|
| **Nominal** (both classes at mid draws) | **3.55×** | round 1 said 2.7× — it was neither nominal nor worst-case |
| **Worst case** (tallest slab vs shortest of 24 ore nodes) | **1.32×** | **below the ≥2× §8 and the bar asserted** |

**So the ≥2× height floor is withdrawn as a guarantee.** It is now stated as *nominal ≈ 3.5×, worst case
≈ 1.3×, measured and reported per build*. What carries the channel instead is the **stand-ratio inversion**,
which §2.3 shows is categorical at every draw: `b/a = V · q / P` = `0.94 × 0.60 / 1.00` = **0.56** nominal
(§0.1's `P` pin) — i.e. the slab's top reaches barely half its own footprint radius — which is **4.07 : 1
flat** on the declared full-width convention. It is mathematically unable to invert while `q = 0.60`: that
would need `V ≥ 1.60`, and `V` caps at 1.268.

**ROUND 3 — and the framing arithmetic settles the argument the other way round from what you'd expect.**
The default camera pitches **55° DOWN** (§8.0), which foreshortens vertical extent by `cos 55° = 0.574` and
leaves planar extent essentially intact. So:

- the **height** ratio is the channel the camera *fights*. Worst case 0.518u vs 0.682u is **18 px vs 24 px**
  — a 6-pixel difference in a 720p frame, which is not a read at any glance. Round 2 withdrew the ≥2×
  *guarantee*; round 3 additionally forbids **counting** height as a channel at all, because it is the same
  axis (silhouette geometry) as the stand-ratio inversion — counting both would be this spec's own version of
  the `86caz5na6` bob-plus-sway failure.
- the **aspect** inversion is the channel the camera *helps*. The same foreshortening multiplies apparent
  flatness by **1.43–1.74×** (§8.0). A world-space 4.07 : 1 slab reads as **≈ 5.8 : 1 on screen**, against
  an ore node's ≈ 1.9 : 1. **That is the read, and the default framing is its best case, not its worst.**

**The three-tier ladder (N5 — reworded; round 1 read as a height ordering and the middle rung is the
shortest).** Ordered by *what your body does*, not by height:

> **you walk over it** (decorative slab — lies flat, tilted, 0.09–0.52u, no verb) → **you crouch and take it
> in your hand** (pickup pebble — round and proud, 0.05–0.22u, `E`) → **you plant your feet and swing at it**
> (ore node / boulder — stands up, 0.68u and above, pickaxe).

**PEBBLE PAIR (N4) — the honest answer: posture separates them, height does NOT.** Corrected per §2.3, the
pebble apex is `0.22 × V × [0.35, 0.80]` = **0.051–0.219u** (`:1164`, jitter 0.34), which **overlaps** the
decorative slab's 0.085–0.518u across most of the pebble's range — a worse overlap than the review's
planar-only 1.7× estimate suggested. The separation is therefore carried **entirely by posture**, and it is
robust: the pebble is **uniformly** scaled (stand ratio `b/a = V/P` = `0.94 / 1.00` = **0.94** — near
equidimensional) and untilted beyond its yaw, while the slab is 0.60-squashed **and** tilted 8–22°
(`b/a` = **0.56**). ⚠ **Restated honestly on the declared convention (N2), and it is weaker than round 3
wrote it:** this pair is a **magnitude** separation, not the categorical inversion the minable pair gets —
the pebble's own top does not clear its footprint radius either (0.94 < 1.0). On screen the slab reads
**5.82 : 1** flat against the pebble's **3.04 : 1** (§8.0's rates), a **1.9× separation** — above the 1.6×
this spec demonstrates already fails, but well below the 3.0× the primary channel buys. It is the same
channel the primary cue rides, so it cannot regress independently. Both classes also lie **below** the
minable floor, so neither is confusable with a swing target. **Residual risk ACCEPTED and named:** a player may occasionally press `E` at a decorative slab
expecting a pebble. The cost is one wasted keypress with no animation commitment — categorically cheaper
than a wasted pickaxe swing, which is the failure this ticket exists to remove. It is not worth a beat.

Real-world anchor sentence, per Bar 4 / `lowpoly-quality.md` §0: **"A decorative shore rock is a flat slab
half-sunk in the grass that you could step onto without lifting your knee."** The side-profile capture must
satisfy that sentence, not a number.

⚠ **Named risk — the "no thin Y-squash" precedent.** `BuildRock`'s own comment (`:1315`, soakfix2
`86ca8m5zu`) records that a Y-squash was rejected because it "flattened it toward a mound." **That reject
was against the *welded* subdiv-2 `FacetedSphere`**, whose weld averages facets into a continuous
gradient — the mound read came from the *smooth normals*, not from the squash. Today's mesh is the
flat-shaded `FacetedRock` (unwelded, explicit per-face normals, per-facet value baked to vertex colour),
whose facets survive a 0.60 squash intact. The tilt raise (8–22°) is the belt-and-braces: a mound has no
visible edge plane, a tilted slab does. **Still, this is the one item that can fail the soak.**
Lower-risk fallback if the Sponsor's verdict is "mound": squash **0.72**, tilt **8–22°**, scale cap
**1.15** (§10.3 option B) — at the *nominal* draw a weaker but still categorical stand-ratio inversion:
`b/a = V · q / P` = `0.94 × 0.72 / 1.00` = **0.68**, giving **3.44 : 1** flat on the declared full-width
convention at nominal `s = 0.90` (0.99u ÷ 0.288u) and **4.91 : 1** on screen, against option A's
4.07 : 1 / 5.82 : 1.
Do not go below squash 0.55; that crosses into
disc/pancake territory and stops reading as stone.

⛔ **RE-CHECKED AGAINST THE ACTUAL GATE — revision 3.2, and the fallback does NOT clear it.** Revision 3.1
validated this fallback with *"still below 1.0 with margin"*. **1.0 is not the gate.** §8.2's AC1c gates
**measured disjointness** of the two populations, and the number the fallback has to clear is the **minable
floor `S = 0.787`** (worst-case boulder), not 1.0. `0.68` is also the *nominal* draw; the gate is decided at
the decorative **ceiling**. On AC1c's own metric (`bounds.max.y`, tilt **and** sink included, `V = 1.268`,
`P = 0.96`, `θ = 31.1°`) the `q = 0.72` ceiling is **`S = 0.813`** — **above** the 0.787 floor, so the two
populations **overlap and AC1c fails**. Full derivation, all three options, and the enumeration of every
lever that could and could not rescue it: **§10.3**. The `q = 0.60` of the main spec clears it at
`S = 0.701` (margin **+0.086**). **Do not adopt this fallback on a "mound" soak verdict without reading
§10.3 first** — it trades a soak risk for a red CI gate.

**Bookkeeping that needs NO change (do not "fix" it):** `RockFootprintRadius = 0.55f` (`:1388`) is
consumed by `OverlapsAnyRock` as `RockFootprintRadius * scale`, so the grass- and pebble-exclusion
footprints shrink proportionally with the new scale cap and stay correct automatically.

### 5.2 D2 — REWRITTEN (round 2): **measure the shipped invariant and lock it. Do not build a ring.**

Round 1 specified a minable-side reject list. §4.1 shows that is impossible (the pools author first) *and*
pointless (the radial separation leaves it ~nothing to reject). D2 is therefore re-scoped from *place
differently* to **assert what already holds** — and the assertion is the deliverable, because an invariant
nobody measures is an invariant that dissolves at the next re-tune.

**D2a — the invariant test (REQUIRED, and it is the whole of D2's normal path).**

A test over the **saved `Boot.unity`** (not a live bootstrap — the shipped scene is the artifact that
matters, per the editor-vs-runtime discipline) that:

1. Collects every `LP_Rock` (`LowPolyZoneGen.cs:1306`) under the `LowPolyScatter` root (`:643`), and every
   `OreNode` (`MineOre.OreNodeName`, `MineOre.cs:66`) under `"OreNodes"` (`MovementCameraScene.cs:3170`)
   plus every `Boulder` (`MineBoulder.BoulderNodeName`, `MineBoulder.cs:64`) under `"Boulders"` (`:3349`).
   Discovery by root name has precedent in-tree: `BuildBoulders` already does `GameObject.Find("OreNodes")`
   at `:3368`.
2. **FAILS LOUDLY if either set is empty.** This is the R1 regression guard, stated as the review asked:
   *if the `LP_Rock` set comes back empty, `Debug.LogError` and fail — never a silent zero.* A pool-count
   assertion would not have caught round 1's bug; a **non-empty-both-sets** assertion is the one that would.
3. Computes and **prints** `min` planar XZ distance across the full cross-product, plus the count of pairs
   below 2.6u. **Planar, deliberately:** the minable pools are placed at `y = 0` (`:3204`, `:3385`) before
   the terrain exists at all (`BootstrapProject.cs:105`), so any Y-inclusive metric would be measuring
   bootstrap order, not world layout. Planar distance is terrain-height-agnostic and is the same metric the
   shipped reject lists already use (`PlanarDistXZ`, declared `:3309`).
4. Asserts `min ≥ 2.6u`. **The test cannot pass vacuously** — it emits a measured number every run, so a
   green result carries evidence rather than silence.

**The 2.6u threshold, re-derived (and correcting round 1's single citation).** There are **two** shipped
reaches, not one: ore `mineRadius = 2.2f` (`:3236`) and boulder `mineRadius = 2.4f` (`:3417`). 2.6u clears
the larger with margin, so **nothing decorative sits inside the reach the pickaxe actually has** — the
property that makes this a readable rule rather than a cosmetic one. Round 1 cited only the boulder's 2.4;
the number survives the correction, its justification is now complete.

**D2b — the conditional remedy (only if D2a measures a violation).** If and only if `min < 2.6u`, cull the
offending `LP_Rock` instances in a **post-scatter pass** in `BootstrapProject.Run`, after
`WorldBootstrap.BuildEnvironment()` (line 105) and alongside the four existing `Wire*` siblings (`:113`,
`:123`, `:134`, `:141`). Properties, all of which are why this siting is right:

- **Cull, never reject.** Destroying an already-placed object consumes **zero** RNG draws, so the seed-42
  stream is byte-identical and every tree, grass tuft, stick, bush and surviving rock stays exactly where it
  is today. Rejecting inside the scatter loop would move all of them — that part of round 1's warning was
  correct and stands.
- **The minable pools never move**, so — unlike round 1's design — **`BoulderVerifyCapture` and
  `MineVerifyCapture` need no re-baseline.** Only `RockVerifyCapture` (which centroids the `LP_Rock` set,
  `RockVerifyCapture.cs:57-64`) shifts, and only if a rock was actually culled.
- **Company preserved — PROMOTED in round 3 from a nicety to a hard requirement.** A per-rock cull can
  strand a cluster member alone. Round 2 made this a conditional courtesy; §5.4 makes **company itself the
  second channel**, so a stranded singleton is no longer cosmetic — it is an instance with **one** channel.
  The pass is therefore **unconditional**, not "if any cull occurs": after any cull, remove every survivor
  with no `LP_Rock` peer within the D4 company radius (§5.4). Single non-cascading evaluation against the
  post-cull set — a mutual pair is company and survives. Deterministic and bounded; no iteration to fixpoint.
- **Known harmless side effect:** pebbles reject against `rockFootprints` (`:1161`, `OverlapsAnyRock`
  declared `:1399`) during the scatter, so a pebble suppressed by a rock that is later culled simply does
  not exist. A handful of missing pebbles out of 70 (`:1149`); not worth compensating.

**Predict-Before-Soak for D2 specifically (falsifiable, graded by the impl's own log):**

> **D2a will measure `min > 2.6u` and cull count `0`.** Arithmetic in §4.1: only ~0.43% of outcrop centres
> are radially eligible to contribute, ≈0.09 outcrops island-wide. **If that holds, D2 ships as a guard only
> — one test, zero scene change, zero capture re-baseline, and D2b is never written.** If it fails, D2b is
> already specified and the measurement tells the implementer exactly how many instances it must handle.

**N6 is moot by construction.** Round 1 was asked to also cite the ore loop's bounds (`guard < 8000`
`:3194`, landmark 3.5u `:3207`, self-spacing 3.0u `:3210` — **re-anchored round 3**) because §5.2 cited only
the boulder side. The
re-scope touches **neither** placement loop, so there is no reject list to extend and no starvation to guard
— the round-1 starvation paragraph is withdrawn along with the mechanism it protected.

### 5.3 D3 — De-invert the rim, as hygiene, NOT as the cue

**PIN (N2a) — the number is exactly `0.12`, not "at least 0.12".** Round 1's "at least" contradicted this
section's own "let it be equal across the stone family," and an implementer had to guess. Set
`_RimIntensity = 0.12f` — the same literal as `RockRimIntensity` (`LowPolyZoneGen.cs:96`) — with
`_RimPower 3` to match, on **all three** minable materials, `HasProperty`-guarded exactly as the rock
material does it (`:1937`); an unguarded set on a material whose shader lacks the property is the silent
no-op Bar 10 explicitly warns about. Mirror `_AOStrength 0.5f` onto them too, so the minable stone is not
the only stone in the world with flat crevices. **Ceiling, and it is the point of §5.3:** do **not** raise
any minable material above 0.12 to "make the ore pop" — that is the rim-differential-as-cue route this
section rejects.

**PIN (N2b) — `veinMat` (`MovementCameraScene.cs:3165`, tinted `:3166`) is INCLUDED. Yes to both rim and AO.** Three
reasons, in priority order: (1) **uniformity is the entire content of D3** — making the vein the single
exception would be arbitrary and would reintroduce a differential; (2) the `_AOStrength 0.5` crevice
darkening lands precisely where each vein lump meets the body, which is the contact shadow that makes the
lump read as a **separate mass** rather than a colour patch — so it strengthens the ore node's one genuine
form discriminator (§6, `:3288-3303`, Bar 3 pattern-via-geometry); (3) the rim at 0.12 is a whisper on a
`0.15f + 0.05f × rnd` = **0.15–0.20u** lump (`:3303`), well below anything that reads as glow. **Watch item for the soak:** the vein is
the one *rust*-coloured surface in the stone family, so if the Sponsor reports the ore nodes reading
"highlighted" or "gamey," `veinMat`'s rim is the first thing to drop back to 0 — not the grey materials'.

**And do NOT sell a rim differential as the discriminator.** A "minable rocks are rimmed at 0.20,
decorative at 0.12" cue is a *fine luminance comparison between two objects*, which (a) requires both in
frame, (b) dies on a bright-sky frame where warm-grey stone is already near the top of the value range,
and (c) is a single-channel cue by Bar 10's definition once you grant that it is neither form nor position.
It reads as "one rock is lit slightly differently," which is weather, not affordance. **Keep the rim as a
world-look term (RCK-1's original purpose — a whisper of caught sun, board `21h10_44`) and let it be equal
across the stone family.** Do not remove it from the decorative rock: that would regress a landed,
deliberate Tier-1 look item.

### 5.4 D4 — NEW (round 3): **POSITION (local) — company vs solitude, made categorical**

> **One sentence:** *Scenery comes in litters. Stone you can work stands by itself.*

This is the replacement for the withdrawn §4 channel. It is **POSITION**, it is **not FORM**, and unlike the
radial split it is a *single-frame* read: you do not need to know where you are to see that one thing stands
alone in cleared ground while another is one of four lying together.

**It is board language, not invention.** `inspiration/2026-06-12_21h12_49.png` shows decorative stone as an
arc of separate low rounds settled in the grass beside a stump; `21h22_52` shows it as loose litter strewn
along a path — in both, scenery stone is *plural and spread*. `21h21_30` shows working stone as one
contiguous standing mass with clear ground around it, and `21h10_44` puts the two side by side in the
Sponsor's own reference sheet: low separate rounds bottom-left and bottom-right, one standing shard mass in
the middle.

**Most of it already ships (§2's POSITION-local bullet).** What does not ship is the *guarantee*:

| | Nearest same-class neighbour | Source |
|---|---|---|
| Decorative `LP_Rock` | typically **1.2–1.9u** (2–4 members inside a 3.6u-wide box) — but worst case **√(3.6² + 3.6²) = 5.09u** | `:1025`, `:1028-1029` |
| Minable ore node | **≥ 3.0u** from another ore node, ≥ 3.5u from a landmark | `:3210`, `:3207` |
| Minable boulder | **≥ 4.5u** from another boulder, ≥ 4.0u from an ore node | `:3391`, `:3388` |

**The distributions overlap, so today this is a tendency, not a channel.** A decorative pair drawn to
opposite corners of its box sits 5.09u apart — *more* isolated than the 3.0u floor separating two ore nodes.
Bar 10 does not accept a tendency.

**D4 — one constant, and it becomes categorical.** Change the per-rock cluster offset at `:1028-1029` from
`3.6f` to **`2.0f`**:

```
float x = cxp + (float)(rnd.NextDouble() - 0.5) * 2.0f;   // was 3.6f
float z = czp + (float)(rnd.NextDouble() - 0.5) * 2.0f;   // was 3.6f
```

- Worst-case intra-cluster separation becomes `√(2.0² + 2.0²)` = **2.83u**, which is **below the 3.0u floor
  on minable-to-minable spacing.** The populations then no longer overlap on this axis at any draw:
  **every decorative rock has a peer nearer than any minable node has one.** Categorical, per instance,
  machine-checkable — the same property class §3 chose the stand-ratio inversion for.
- Typical separation tightens to ~0.7–1.1u, which is the board's *litter* read (`21h22_52`) rather than the
  current thin sprinkle.
- **On screen at default framing** (§8.0, 50.9–62.1 px/u planar): peers land **~36–68 px apart** in a
  1280×720 frame (0.7u × 50.9 = 36 px along the view heading; 1.1u × 62.1 = 68 px across it) while a minable
  node's nearest neighbour of any stone class is **≥ 132 px** away (2.6u × 50.9,
  D2a's floor). Both are comfortably inside one frame — the default camera's ground window is roughly
  **20.6u wide × ~14u deep** (§8.0; corrected in revision 3.2 from "20u × 11u", which mixed a ground width
  with a frame-plane height — the correction *widens* the window, so this claim only gets safer), so a 2.0u
  huddle and a 3.0u clearing are seen *together*, which is the condition a relative read needs.

⚠ **Two honest caveats, both must be in the impl PR.**

1. **`2.0f` leaves only a 0.17u margin, and the margin is the fragile part.** If Devon prefers headroom,
   `1.8f` → 2.55u worst case (0.45u margin) at a slightly tighter litter. **Do not go above `2.1f`**
   (2.97u — the margin vanishes). The impl must **measure and print** the achieved maximum
   `LP_Rock`→nearest-`LP_Rock` distance and **fail** the gate if it is ≥ 3.0u, rather than trusting this
   arithmetic. (§2.3's lesson: a derived constant is a nominal; a measured worst case is a gate.)
2. **This CAN perturb the seed-42 stream, unlike D1.** Changing the constant consumes the *same* draws, but
   it moves candidate positions, and `if (!OnLandmass(x, z)) continue;` (`:1030`) sits between the offset
   draws and the `scale` draw (`:1031`) — so a candidate that flips across the coastline changes how many
   draws that iteration consumes. Tightening pulls members *toward* an already-accepted centre, so flips
   should be rare and one-directional (reject→accept), but **`RockVerifyCapture` may need a re-baseline**
   and the PR must state whether it did. The minable pools are authored earlier, in a different file (§4.1),
   and **cannot** move — so `MineVerifyCapture` / `BoulderVerifyCapture` are untouched either way.

**Singletons are now a defect, not an aesthetic wrinkle.** The inner loop can leave a cluster with one
member (an `OnLandmass` reject at `:1030`, or `rocksPlaced` reaching `rockTarget = 60` mid-cluster,
`:1018`/`:1026`). A lone decorative slab has FORM and no POSITION — one channel. So §5.2's company pass is
**unconditional** in round 3, and its radius is the D4 box diagonal (2.83u at `2.0f`), not the old 3.6u.

**What D4 does NOT do.** It does not make the minable node *look* different — the whole channel is carried
by the decorative class changing, exactly as §3 argued (the class with no gameplay contract is the one that
moves). No minable prefab, material, mesh, position or carve is touched by D4.

---

## 6. Tier 2 — OPTIONAL, only if the Tier-1 soak says the boulder still reads ambiguous

The ore node already carries a genuine form discriminator: **3 rusty vein lumps clustered on its upper
surface** (`veinCount = 3` at `:3292`, placed `:3295-3301`, meshed `:3303`), which is Bar 3's "pattern via
geometry" and reads as *iron in rock*. The
**boulder has literally nothing** — it is `FacetedRock` in the decorative tint at 1.23× the decorative
size ceiling. If Tier 1 lands and the boulder is still the weak one, the cheapest next form beat is:

**A subordinate chip skirt at the boulder's foot** — 3–4 `FacetedRock(0.10–0.18)` children at planar
`radius × 0.55–0.75`, sitting at the ground line, on the *same* `boulderMat`. It reuses the ore-vein loop
idiom verbatim, authors no new mesh, and lands the board's own language (`21h21_30`: the standing outcrop
has loose rounds at its foot; `21h10_44`: the shard cluster has chips + grass at its base). The read it
buys is **silhouette hierarchy** — *one dominant mass with subordinate crumbs* vs *a peer group of
equals* — which is a different gestalt from the decorative huddle and cannot be confused with it once the
decorative class is lying flat.

⚠ **Hard implementation hazard, must be resolved before this is attempted.**
`MineableNodeState.TryPlanarFootprint` (`MineOre.cs:812`) encapsulates **every** renderer's bounds in the
node (the loop `:817-822`, `b.Encapsulate(_renderers[i].bounds)` at `:821`) and feeds the min XZ half-extent
into `MovementCarveWorldRadius` (`:711`). Adding chip children
inside the node **widens the measured footprint and therefore the NavMesh carve** — which re-opens exactly
the round-8 defect the Sponsor already rejected once — his soak-7 verdict, recorded verbatim at
`MineOre.cs:686-687`: *"im blocked but already at this distance in the screenshot, not at the edge of the
boulder"* (the invisible-wall read; quote re-verified at `0f14b4f`). Required
resolution: either keep the chips strictly inside the body's planar silhouette so `_carveFootprint` is
provably unchanged, **or** restrict `TryPlanarFootprint` to the body renderer (`BoulderMesh` / `OreRock`)
and re-verify. Either way, re-run `MineBoulderPlacementObstacleTests` and `BoulderSceneTests` and quote
`_carveFootprint` before/after in the PR. **Chips must live inside the node** (not as pool-root siblings)
so they sink/fade/regrow with the break tween — a broken boulder must not leave orphan rubble on a spot
that regrows.

This tier is deliberately **not** in the recommendation. It is a labelled escape hatch so a soak-reject has
a next move that does not require re-opening mesh authoring.

---

## 7. What I am deliberately NOT proposing, and why

| Not proposing | Why not |
|---|---|
| **Re-authoring the boulder or ore-node mesh** (new silhouette, bevel/chamfer, split-rock variant) | `86cacewju` owns the hero-prop bevel lineage and is separately deferred. And it is unnecessary: the whole cue is buyable from the one class with no gameplay contract (§3). Re-authoring a hero mesh to fix a prop's readability is paying at the wrong end. |
| **A hue/tint separation** ("make the minable ones warmer") | Fails Bar 10 outright (colour ranks last, never first) and fails Bar 3 (material-honest — warm-grey stone is warm-grey stone; the red axe-head reject is the precedent). It would also be *invisible* against a saturated mid-green world that eats hue cues, which is Bar 10's stated WHY. |
| **A rim/glow differential as the cue** | §5.3. Fine luminance comparison between two objects, needs both in frame, dies on a bright frame, and is single-channel by Bar 10's definition. Kept as equalised world-look only. |
| **Micro-animation / pulse / bob on minable nodes** | `game-juice.md` §0 caps amplitude to the calm tone ("alive and satisfying, never violent or chaotic") and §"hard don'ts" bars sustained motion. More decisively, the #172 soak verdict is already on record — *"moving grass/bushes looks weird; only the trees up in the air should move"* — which is why meadow grass ships with `_WaveAmp = 0` (`LowPolyZoneGen.GrassWaveMat`). A shoreline of throbbing rocks is the theme-park failure. **Amplitude I would accept: zero.** Bar 10 ranks motion third anyway; we have two better channels. |
| **Screen-space outline on interactives** (Sobel depth/normal Renderer Feature) | Explicitly ruled out in `lowpoly-quality.md` §3 — expensive at desktop res on a large island, and the board's edge language is a per-face chamfer, not a full-silhouette outline. |
| **Hover highlight / cursor change on the minable node** | Wrong for the control scheme: since the WASD pivot the mouse **orbits the camera**, it is not a world pointer, so there is no reliable hover state to read. It also fails the AC as written — "the affordance reads WITHOUT a click" means readable while *scanning*, and a hover cue requires you to already be pointing at the answer. And it is input-handling, which the ticket puts out of scope. |
| **HUD marker / minimap pip / floating icon or glyph** | Bar 10 makes text/iconography the last-resort fallback. It also violates the tonal anchor: the world should teach this, not a UI layer narrating it. Deferring it costs nothing; a marker layer is very hard to remove later. |
| **Re-siting the minable pools out of the scatter annulus** | Would move findability, which the pools' own placement comments deliberately tuned ("findable without heavy exploration and on the proven-walkable NavMesh loop"). **And round 2 makes it doubly unnecessary:** §4.2 shows the radial separation the re-siting would buy **already ships** (`r ≥ 17u` decorative vs `[9,17]` minable), so the only gap was that nothing guarded it — which D2a closes with a test, not a placement change. |
| **Touching the mine gate / `ClickGateDiagnostic` / arbitration** | Confirmed correct by the 2026-07-21 ClickGateDiag session; ticket AC3 forbids it. Nothing in this direction changes a single gate predicate. |
| **A second decorative rock MESH variant** (a distinct "scenery-only" shape) | Tempting and it would be the strongest possible form cue — but it is mesh authoring on 60 instances, it triples the world-scatter pass, and the transform-only route already yields a categorical stand-ratio inversion. Held in reserve behind Tier 2. |
| **Inverting the bare grass collar** (grass grows over decorative slabs; minable nodes get a cleared ring) — *added round 3* | Ruled out on **measurement**, not taste (§2.4): grass ships at ~0.005 tufts/u², so the expected number of tufts inside a 0.58u ore node is **≈0.006** — the collar the minable class lacks has, on average, no tuft in it to notice. It would cost a grass-loop RNG shift plus a capture re-baseline for ~zero pixels, and it grazes the Sponsor-rejected #130 defect the `GrassRockPad` rule exists to fix. **The collar stays exactly as it ships — §2.4 shows it is what keeps a flattened slab visible at all.** |
| **A value / luminance separation** ("minable stone is the lighter grey") — *added round 3* | Nearest miss on the list, and the current state is genuinely **inverted** (§2 COLOUR: the minable ore node is the *dullest* stone in the world). But it is the COLOUR axis, which Bar 10 ranks **last**; a ~0.12-linear step between two warm greys is exactly what distance fog, bloom and the grading pass eat first; and it is a *relative* read needing both objects in frame. **Recommended as HYGIENE, offered to the Sponsor as §10.2 option P3, and explicitly NOT counted toward the ≥2.** |

**Scope total for the recommendation: four call sites, ~45 lines, one capture re-baseline** (round 3 adds
D4's single constant + the promoted company pass). That is the point — the Sponsor has already accepted the
current state, so a modest direction that lands is worth more than an ambitious one that queues behind the
build slot forever.

---

## 8. Verification, and the falsifiable pre-soak claim

### 8.0 NEW (round 3) — DEFAULT GAMEPLAY FRAMING: the ruler every channel is now judged against

Bar 10's `86caz5na6` gap is that variance is tested as a **boolean, never a magnitude** — a cue can vary
correctly and be invisible. So before any channel is declared, here is the framing it has to survive, with
every parameter fetched from source at `750f190`:

| Parameter | Value | Source |
|---|---|---|
| Orbit distance (default) | **14u** | `MovementCameraScene.cs:4754` (`orbit.distance = 14f`); field default `OrbitCamera.cs:35` |
| Zoom range | 6u – 26u | `OrbitCamera.cs:49-50` |
| Pitch (default) | **55° down** — *"Sponsor-preferred top-down-ish framing — LOCKED"* | `MovementCameraScene.cs:4748` |
| Pitch range | 8° – 70° | `:4752-4753` |
| Vertical FOV | **45°** | `:4715` |
| Reference frame | 1280 × 720 | `OrbitCamera.cs:158` |
| Measured anchor | the 1.8u castaway renders **~55 × 95 px** at this framing (Tess-confirmed on the round-1 capture) | `OrbitCamera.cs:158-159`; agent height `MovementCameraScene.cs:4452` |

**The arithmetic, and it is the single most useful thing in this revision.** ⚠ **CORRECTED in revision 3.1 —
round 3 had this ~5% high.** Round 3 computed screen scale as `720 px ÷ 45° = 16 px/°` and multiplied by the
angle a 1u span subtends (`2 · atan(0.5/14)` = 4.09°) to get 65.5 px/u. **That treats pixels as linear in
angle; a pinhole camera is linear in tangent**, and `720/45` is the *average* rate across the whole 45°,
which overstates the rate at frame centre. The correct base rate is the frame-plane one — and it is exactly
the figure `team/quality-bars.md` Bar 10 now states as canonical:

```
frame-plane height at 14u = 2 × 14 × tan(45°/2) = 2 × 14 × 0.41421 = 11.598 u
base scale                = 720 px ÷ 11.598 u   = 62.1 px/u
```

- **planar (ground) extent → 50.9–62.1 px/u** (`62.1 × sin 55° = 50.9` along the view heading;
  unforeshortened at 62.1 across it);
- **vertical extent → 35.6 px/u** (`62.1 × cos 55°`).

**Every RATIO in this spec is unaffected** — the correction is one scalar on a common base, so the ×1.43–1.74
amplification (`tan 55°` and `1/cos 55°`), the 5.82 : 1 / 1.92 : 1 pair and the 1.6×-vs-3.0× calibration are
arithmetically identical before and after. Only the absolute px figures move, each by ~5%.

> **The default camera looks DOWN at 55°. It therefore SUPPRESSES height by ×0.57 and leaves footprint
> intact — so it is the worst possible camera for a height cue and the best possible camera for a shape-of-
> footprint cue. The apparent flatness of any object is its true flatness multiplied by 1.43–1.74×.**

That one fact decides the whole channel audit, and it decides it *against* the intuitive answer ("make the
scenery obviously shorter") and *for* the stand-ratio inversion.

⚠ **Two limits on this arithmetic, stated so nobody treats it as a gate on its own.** (1) The geometric
figure (35.6 px/u vertical) and the measured anchor (95 px / 1.8u = 52.8 px/u) disagree — and the corrected
rate **widens** that gap, which makes the conservative posture below more honest, not less. The camera
also applies terrain clearance, follow-lag and an occlusion pull-in (`OrbitCamera.cs:342`), and the measured
95 px is a whole silhouette rather than a pure vertical axis. **This spec uses the CONSERVATIVE 35.6 px/u
throughout** — every height figure below is a lower bound. (2) These are *sizing estimates for choosing a
direction*. **The impl must MEASURE pixel extents on the shipped-build capture and quote them** (AC5); no
number in this section is a substitute for that.

**What it actually looks like at normal play distance — in plain words, which is the point of §8.0.**
Standing in the survival loop at default framing, the ground window is roughly **20.6u wide × ~14u deep**.

> ⚠ **CORRECTED in revision 3.2 (Priya round-4 NIT 3) — 3.1 said "20u × 11u", which mixes planes.** 20.6u is
> a **ground**-plane width (`11.598 × 1280/720`, unforeshortened across the heading); 11.6u was the
> **frame**-plane height and is not a ground depth. The ground depth at 55° is `11.598 ÷ sin 55°` =
> `11.598 ÷ 0.8192` ≈ **14.2u** at the frame-centre depth. The true ground footprint is a **trapezoid**, not a
> rectangle — narrower at the near edge, wider at the far — so 14u is a centre-depth figure, not a bound.
> **Nothing built on this moves:** the old figure was *conservative* (it understated the depth), so every
> "both inside one ground window" claim in §8.1 and below gets more room, not less. It is the same
> plane-mixing class the px/u ruler fix closed one level down, which is why it is corrected rather than
> waved through.

**All widths below are FULL planar extent and all heights are apex above ground, per §0.1.** Arithmetic is
shown inline because N1 was a figure that could not be recomputed.

- **A minable ore node** is a compact lump about **59 px wide × 31 px tall** — width `2 × 0.58u` (`:3275`)
  × 50.9 = 59 px; height `0.58 × (0.55 + 0.94)` = 0.864u × 35.6 = 31 px. Roughly one third of the
  castaway's height, with a distinct lit top plane, a visible side face, its own cast shadow, and three
  rust-coloured bumps on its crown. It reads as *an object standing on the ground*.
- **A minable boulder** is a **~107–137 px wide × ~59 px tall** mass — ⚠ **N1 CORRECTED.** Round 3 quoted
  "~76 × 63 px"; the height was right in intent but the width was a *half*-width read on an unstated rate,
  and it does not reproduce on either planar rate (half-width at the mid draw is `1.20u × 50.9` = 61 px along
  the heading, `× 62.1` = 75 px across it — 76 px reproduces exactly nowhere). On the declared convention the
  boulder's planar radius is **1.05–1.35u** (`:3448`), so full width is **2.10–2.70u** → `× 50.9` =
  **107–137 px**; height at the mid draw is `1.20 × 1.39` = 1.668u × 35.6 = **59 px**. It is about
  two-thirds of the castaway's height and roughly **twice** an ore node's width. Unmistakable — and round 3
  *understated* it by ~40%, so no claim in this spec was inflated by the error.
- **A decorative slab, after D1** is a **~50 px wide × 9 px tall** lozenge — width `2 × 0.55 × 0.90` = 0.99u
  × 50.9 = 50 px; height 0.243u × 35.6 = 9 px. No side face, no top-vs-side
  distinction, a thin smear of contact shadow directly beneath it, sitting at roughly the height of the
  grass blades around it (§2.4). It reads as *a mark on the ground*, not an object on it.
- **And they come in different numbers:** the slab has two or three identical companions within ~36–68 px
  (D4); the node has nothing else within 132 px.

**The one-sentence version, which is the acceptance test:** *at normal play distance you are choosing
between lumps that stand alone and lozenges that lie in litters, and you can do it without moving the
camera.*

**Calibration, so "it's a big difference" is not taken on faith.** The world already ships an apparent-
flatness difference and it **is not enough**: a decorative rock today is uniform-scaled and centred on the
ground point, so its full-width flatness is `2P/V` = `2.00/0.94` = 2.13 world → `× tan 55°` = **3.04 : 1**
apparent, against an ore node's `2P/(0.55+V)` = `2.00/1.49` = 1.34 → **1.92 : 1** — a **1.6× separation
(3.04 ÷ 1.92 = 1.58) that demonstrably fails**, since it is the state the
Sponsor is currently dead-clicking through. D1 option A takes decorative to `4.07 × tan 55°` ≈ **5.82 : 1**,
a **3.0× separation** (5.82 ÷ 1.92 = 3.03).
**The target is therefore "roughly double the separation that is already known to fail," not "some
difference exists."** That is the magnitude claim, and the soak either confirms it or corrects it.

### 8.1 Bar 10 channel declaration — REWRITTEN (round 3)

Two channels, on two different axes, each with a stated magnitude at default framing and a stated failure
domain. **The height ratio is deliberately absent — see the note after the table.**

| | Channel 1 | Channel 2 |
|---|---|---|
| **Name** | Stand-ratio inversion (§3, D1) | Company vs solitude (§5.4, D4) |
| **Axis** | **FORM** | **POSITION (local)** |
| **What it is on a CUED instance** (minable) | apex **clears** its own footprint radius — stand ratio `b/a` (§0.1: apex ÷ planar HALF-extent, `P = 1.00`) = **1.39** boulder `(0.45+0.94)/1.00`, **1.49** ore `(0.55+0.94)/1.00` | nearest stone of any class **≥ 2.6u** (D2a); nearest same-class **≥ 3.0u** |
| **What it is on a NON-CUED instance** (decorative) | apex reaches barely **half** its footprint radius — `b/a` = `0.94 × 0.60 / 1.00` = **0.56** | a peer within **≤ 2.83u**, typically 0.7–1.1u |
| **Bar 10 invariance check** ("what does this look like on a non-cued instance?") | *Inverted, not merely reduced* — the two classes fall on opposite sides of `b/a = 1`. **Not the same. Passes.** ⚠ **`b/a` is a stand ratio, NOT a width ratio (N2, §0.1)** — on the declared full-width convention **both** classes are wider than tall, and the row below is where the player-visible claim lives. | *Disjoint ranges* — max decorative NN (2.83u) < min minable NN (3.0u). **Not the same. Passes.** |
| **Magnitude at default framing** (§8.0) | **Amplified ×1.43.** 5.82 : 1 vs 1.92 : 1 apparent flatness — a 3.0× separation, against the 1.6× that is known to fail | **~36–68 px** peer spacing vs **≥132 px** isolation, both inside one **~20.6u × ~14u** ground window (§8.0, corrected in 3.2 — the old "11u" was a frame-plane height, and the correction *widens* the window) |
| **Failure domain** | `BuildRock`'s `localScale` / tilt / scale-band — **transform values only.** No shader property, so it cannot silently no-op the way a `HasProperty`-guarded material set can (Bar 10's Devon-verified `_RimIntensity` mechanism) | The scatter loop's cluster-offset constant + the unconditional company pass — **inter-object placement.** A different function, a different mechanism, a different kind of edit |
| **Survives desaturation?** | Yes — 100% geometric | Yes — 100% geometric |

**Why the apex-height ratio is NOT listed as a third channel, and must not be re-added.** It varies
(nominal 3.55×) and it is machine-checkable, so it *passes* a naive reading of the bar. But it is
**silhouette geometry — the same axis as the stand-ratio inversion** — so counting it would be exactly the
`86caz5na6` failure (bob + sway counted as two). And §8.0 shows the camera suppresses it: worst case is
**18 px vs 24 px** (0.518u and 0.682u × 35.6), a 6-pixel difference. **Report it; never count it.**

⚠ **Shared failure domain — named, because Bar 10's KNOWN-INCOMPLETE clause requires it.** The two channels
have independent *mechanisms*, but they share **one** domain: both are baked into `Boot.unity` at bootstrap,
and the shipped build renders the **committed** scene, not a freshly generated one
(`[[unity-procedural-committed-assets-go-stale]]`). If the scene asset is not regenerated and committed, both
channels are absent together, with no error anywhere. **D2a is the guard**: it runs over the *saved*
`Boot.unity` and fails loudly on an empty set, so a stale scene cannot present as a pass. **This is why D2a
survives §4's demotion** — it stopped being a channel and became the thing that stops both channels failing
silently.

**Not vulnerable to `86caz5na6`'s third evasion** (an invariance check returns no verdict on a *unique*
instance): both classes are populous — 60 `LP_Rock` (`:1018`), 24 ore nodes (`IronDifficulty.cs:54`), 7
boulders (`:3319`) — and every check below runs across the full cross-product, so there is always a non-cued
instance to compare against.

### 8.2 Developer-verifiable acceptance criteria

Written so Drew/Devon can pass or fail each one from a log line or a capture, with no taste judgement.
**AC1–AC6 are machine-checkable. AC7 is the Sponsor's, and only the Sponsor's.**

⚠ **AC1 SPLIT IN REVISION 3.1 (N3) — an inversion claim must gate BOTH sides.** Round 3's AC1 gated only
the decorative class, so an implementation could satisfy it in full while leaving every minable node
untouched: the cue would exist on paper and not on screen. Three things had to be fixed together, and the
first is why the split was not simply "run AC1 on the ore nodes too":

1. **`extents.y / max(extents.x, extents.z)` is the WRONG metric for the minable side, and it fails
   silently.** `Renderer.bounds.extents` is measured about the bounds **centre**, so it excludes the ground
   lift (`radius × 0.55f` ore `:3277`, `× 0.45f` boulder `:3449`). Applied literally to an ore node it
   returns `V/P` ≈ `0.94/1.00` = **0.94 — below 1.0** — and a developer running round 3's AC1 symmetrically
   would have concluded *there is no inversion*. The common metric is therefore **`S = bounds.max.y ÷
   max(bounds.extents.x, bounds.extents.z)`** with the world ground plane at `y = 0` — which is §0.1's
   stand ratio **in its MEASURED form**, and which both pools can be measured on because both are placed at
   `y = 0` (`:3204`, `:3385`, §5.2). ⚠ **Revision 3.2: "exactly §0.1's `b/a`" was too loose and it is what
   let the metric error through.** `S` is the stand ratio *as read off `Renderer.bounds`* — tilt and sink
   included. The **derived** form `V·q/P` used in §2.3/§5.1 carries neither, and for the decorative class
   the two differ materially (0.7925 vs 0.701 at option A's ceiling). See §0.1's `S`-vs-`V·q/P` row.
2. **The minable side is REPORTED, not thresholded, and the arithmetic is why.** `S > 1.0` is a *nominal*
   on that side, not a floor — §2.3's tails give a worst-case ore node `(0.55 + 0.626)/1.38` = **0.8522** and
   a worst-case boulder `(0.45 + 0.636)/1.38` = **0.7870**, both under 1.0. (Both are already on the `S`
   metric — the minable classes take no tilt and no sink — so **0.7870 is `min(S over minable)` and is the
   figure AC1c gates against.** Quoted to 4 dp because the margins below are third-decimal.) A hard `S > 1.0` gate on the cued
   class could therefore fail on geometry this spec **does not change** (§8.2 OOS: D1/D2/D4 touch no minable
   mesh, lift or placement), leaving the implementer a red gate with no in-scope fix. This is §2.3's own
   lesson applied to the class round 3 exempted from it: *a derived constant is a nominal; a measured worst
   case is a gate.*
3. **AC1c is the gate that actually encodes "inversion"** — measured disjointness of the two populations,
   not a fixed threshold on either. Both pools are deterministic (60 `LP_Rock`; 24 ore seeds
   `86300 + i × 17` `:3216`; 7 boulders `:3319`), so the full cross-product is cheap and the answer is the
   same every build. ⚠ **And it is a genuine gate, not a formality: §2.3's tails permit an overlap.**

⛔ **REVISION 3.2 — THE MARGINS WERE STATED ON A METRIC THIS GATE DOES NOT MEASURE. Corrected here, and
the correction is the reason the gate turns out to be SOUND.** Revision 3.1 reported the decorative ceiling
as `(1.268 × 0.60)/0.96` = **0.79** meeting a boulder floor of **0.79** — a zero margin, which Priya
correctly called untestable. **Those two numbers are not on the same metric.** `V·q/P` is a *sink-free,
tilt-free* ratio of nominal semi-axes. AC1c measures `S = bounds.max.y ÷ max(extents.x, extents.z)`, and
`bounds.max.y` carries **both** of D1's vertical edits — the tilt raise (`a · sin θ`, which *adds*) and the
`−0.08 × s` sink (which *subtracts*). Omitting them is not conservative in either direction; each is
individually larger than the margin, and in revision 3.1 they cancelled by luck.

**Which side was wrong: only the decorative one.** The minable classes take **no tilt and no sink**, so
their `S` is exactly `(lift + V)/P` — already the gate's metric. The boulder floor `(0.45 + 0.636)/1.38` =
**0.7870** and the ore floor `(0.55 + 0.626)/1.38` = **0.8522** stand unchanged; `min(S over minable)` =
**0.7870**. Everything below re-states the *decorative* side onto that same metric.

**The decorative ceiling on AC1c's own metric** (option A: `V = 1.268`, `q = 0.60`, `P = 0.96` per §0.1's
stand-ratio pin, `θ = 31.1°`, sink `0.08 × s`):

```
b    = 0.55 · V · q · s = 0.41844 s        a = 0.55 · P · s = 0.528 s
apex = sqrt(b²cos²θ + a²sin²θ) − 0.08 s = (0.450286 − 0.08) s = 0.370286 s
S    = 0.370286 s / 0.528 s = 0.7013            ← the scale `s` CANCELS (see below)
       (tilt alone → 0.85 · sink alone → 0.64 · neither → 0.7925, which is 3.1's figure)
margin against the 0.7870 minable floor = +0.086
```

⚠ **`S` is independent of `s`, and that is load-bearing.** `b`, `a` and the sink are all proportional to
`s`, so `s` divides out of `S` entirely. **The scale cap therefore cannot move AC1c at all** — which is why
§10.3's options A and C share a verdict, and why the only knobs this gate responds to are `q`, the tilt band
and the sink coefficient. Every AC1c figure in this spec is quoted per-unit-`s` for that reason.

**So the realized option-A ceiling is ≈0.70 against a floor of 0.787 — a real +0.09 margin, not a
touching boundary. The gate is sound and needs no rescue; it needed to be measured correctly.** Revision
3.1's alarm was over-stated, and it is corrected precisely *because* it happens to land the safe way: a
reader who inherits a number that is not on the gate's metric will trust it the next time it lands the
unsafe way. (On the sink-free figures as 3.1 wrote them the two do not even meet — they **cross**, 0.7925
vs 0.7870, i.e. AC1c fails by 0.0055. That artifact disappears entirely on the correct metric.)

⚠ **One honest bound on the derived margin, and it is why AC1c is MEASURED.** The derivation above uses the
untilted planar half-extent `a` as the denominator. `Renderer.bounds` is a world **AABB**, and a slab tilted
by `θ` has its *minimum* horizontal AABB half-extent at `sqrt(a²cos²θ + b²sin²θ)` = `0.5011 s` — 5.1% below
`a`. If the shipped mesh's widest planar axis lands near that minimum, option A's measured `S` rises to
**0.739** and the margin narrows to **+0.048**. Both readings pass; the band is **`S` 0.70–0.74, margin
+0.086 → +0.048**. This modelling uncertainty is the reason the trigger threshold below is **0.05** rather
than a picked number — it is set to swallow exactly this band. The same bound applied to `q = 0.72` moves
it from −0.026 to **−0.036**: it fails on both readings, so no verdict in §10.3 turns on this.

**AC1c is calibrated for §10.3 option A (`q = 0.60`), and equally for option C (`q = 0.60`, `s` cancels).
It does NOT hold for option B (`q = 0.72`), which measures `S = 0.813` against the 0.787 floor and
therefore fails — see §10.3 for the full option table and the enumeration of every lever that could and
could not rescue it.** If the Sponsor picks B, AC1c's threshold is part of that pick and must be
re-derived with it; do not ship B against this AC unchanged.

**NAMED LEVER, with a numeric self-terminating trigger (revision 3.2 — Priya's PL call).** An AC that can
come back tight must resolve itself in-scope or stop cleanly; it must never hand the implementer a red gate
and a question. Let `gap = min(S over 24 OreNode + 7 Boulder) − max(S over 60 LP_Rock)`, all on the AC1c
metric, measured on the saved scene:

| Measured `gap` | Action — no judgement call anywhere in this ladder |
|---|---|
| **`> 0.05`** | **PASS.** Record `gap` in the PR and stop. (Derived expectation under option A: **+0.086**, or **+0.048** on the tilted-AABB bound.) |
| **`≤ 0.05`** (including negative) | Apply the lever **ONCE**: `q` **0.60 → 0.55** — §5.1's hard floor, *do not go below it* — regenerate the scene, re-measure. Derived effect: ceiling `S` 0.701 → **0.657**, i.e. the lever buys **≈ +0.044** of gap (**+0.036** on the tilted-AABB bound). Touch **no** minable value. |
| ↳ re-measured **`> 0.05`** | **PASS.** Quote **both** measurements in the PR and note the lever fired. |
| ↳ re-measured **`0 < gap ≤ 0.05`** | **PASS WITH DEFECT.** Quote both; file the residual as a follow-up. Disjoint is disjoint, but a sub-0.05 gap is inside the modelling band above and should not be claimed as comfortable. |
| ↳ re-measured **`≤ 0`** | ⛔ **STOP AND REPORT. Do not proceed and do not improvise.** The direction's disjointness premise is falsified on shipped geometry; this returns to **direction**, not to tuning. **Do not touch a minable value to close it** — ticket AC3 and §8.2's OOS list forbid it, and doing so would trade a red gate for an unmeasured change to the class the whole cue is defined against. |

**Why the ladder terminates.** `q = 0.55` is the floor (§5.1: below it the slab reads as a disc, not stone),
so there is exactly one lever pull available and the ladder cannot loop. Its headroom is ≈ 0.044, so a
measured gap below about **−0.045** is not rescuable in-scope — that branch is reachable, which is why it
is written rather than assumed away. **The scale cap is not a second lever** (`s` cancels). **The tilt band
is arithmetically a lever but is not an available one:** dropping tilt to 0° moves option A's ceiling
0.701 → **0.641** (gain 0.060), but tilt is the single feature §5.1 names as what kills the "mound" read,
so spending it to buy AC1c margin trades the gate against the one risk most likely to fail the soak.
**`q` is the only lever this AC may pull, and it may pull it once.**

| # | Acceptance criterion | How it is verified | Fails when |
|---|---|---|---|
| **AC1a** *(non-cued side)* | Every `LP_Rock` in the saved `Boot.unity` has stand ratio **`S < 1.0`**, where **`S = bounds.max.y ÷ max(bounds.extents.x, bounds.extents.z)`** on the world ground plane `y = 0` (§0.1) | EditMode test over the saved scene; prints the achieved **maximum** `S` across all 60 instances | any single instance ≥ 1.0 |
| **AC1b** *(cued side — NEW, N3)* | Every `OreNode` and every `Boulder` is measured on the **same** `S`, and the achieved **minimum** across all 24 + 7 is printed alongside AC1a's maximum | same test, same metric, same scene | not printed — see the note below for why this side is **reported**, not thresholded |
| **AC1c** *(the inversion itself — N3; trigger added in 3.2)* | The two populations are **disjoint**: `gap = min(S over minable) − max(S over decorative) > 0`, and the achieved gap is printed. **`gap ≤ 0.05` fires the self-terminating lever ladder above — apply `q → 0.55` ONCE, regenerate, re-measure, quote both.** Derived expectation under option A: **+0.086** (**+0.048** on the tilted-AABB bound); minable floor **`S = 0.7870`** | same test; prints both extremes and their difference, and — if the lever fired — the pre- and post-lever gap | the **re-measured** gap is still `≤ 0` after the single `q → 0.55` pull. Then **STOP and report**; do NOT touch a minable value (ticket AC3, OOS below). ⚠ Calibrated for §10.3 **option A / C** (`q = 0.60`); **option B (`q = 0.72`) fails this AC as specified — `S = 0.813` vs the 0.7870 floor** (§10.3) |
| **AC2** | Every `LP_Rock` has another `LP_Rock` within **2.83u** planar (the D4 box diagonal); and the achieved **maximum** nearest-neighbour distance is **< 3.0u** | same test; prints max NN + the count of singletons | max NN ≥ 3.0u, or any singleton survives the company pass |
| **AC3** | Minimum planar distance `LP_Rock` → any minable node is **≥ 2.6u**; the `LP_Rock`, `OreNode` and `Boulder` sets are each **non-empty** | D2a (§5.2); prints min distance, sub-2.6u pair count, and all three set sizes | min < 2.6u, or **any set is empty** (the round-1 silent-no-op guard) |
| **AC4** | The achieved **minimum apex-height ratio** (shortest minable ÷ tallest decorative) is printed | same test — **reported, not gated** (§2.3: it is a nominal, not a floor) | not printed at all |
| **AC5** | On a **shipped-exe** gameplay-orbit capture (pitch 55, distance 14, 1280×720) containing ≥1 decorative cluster and ≥1 minable node: the measured **pixel** width and height of one instance of each class are quoted in the PR | read off the capture; compare against §8.0's estimates | not measured, or the decorative instance exceeds **~2× §8.0's predicted 9 px** height (which would mean the squash did not land) |
| **AC6** | The **same capture, desaturated**, still shows both channels | Bar 10's own check | either channel disappears |
| **AC7** | *(Sponsor, soak)* On one pass of the 9–17u loop, minable stone is identified **without clicking** | soak | Sponsor says otherwise — and then the DIRECTION is wrong, not the tuning |

**Explicitly OUT OF SCOPE for the implementation this spec briefs:**

- The mine gate, `ClickGateDiagnostic`, click arbitration, and every input-handling path (ticket AC3 —
  confirmed correct by the 2026-07-21 ClickGateDiag session). **Not one gate predicate changes.**
- Minable-node **data**: pool sizes, difficulty presets, `mineRadius`, yields, respawn timers, the NavMesh
  carve and `TryPlanarFootprint`. D1/D2/D4 touch **no** minable placement or geometry.
- Any new shader, shader property, Renderer Feature, material, or mesh. D3 sets two **existing**,
  `HasProperty`-guarded properties; nothing else is authored.
- `RockFootprintRadius`, `GrassRockPad`, `OverlapsAnyRock` and the grass/pebble reject rules — **do not
  touch** (§2.4: the cue now depends on them).
- Tier 2's chip skirt (§6) — held behind a separate Sponsor decision (§10.4).
- The `86cacewju` hero-prop bevel/chamfer lineage.
- Hue changes of any kind; HUD/marker/minimap surfaces; motion of any amplitude on any stone.

**Capture protocol (impl PR, from the SHIPPED exe — editor framing is not evidence):**

1. Gameplay-orbit frame at **default** parameters (pitch 55, distance 14, FOV 45) containing at least one
   decorative cluster **and** one minable node in shot. **Do not compose a favourable frame** — the whole
   claim is that this survives the framing the Sponsor actually plays at.
2. The **same frame desaturated** — Bar 10's check (AC6). If either channel goes, the direction failed, not
   the tuning.
3. A **side-profile** shot of a decorative cluster against the anchor sentence in §5.1 (Bar 4 /
   `lowpoly-quality.md` §0 — up-vs-down is invisible from player-eye and obvious side-on). **Diagnostic
   only** — it is a *favourable* camera by construction, so it may not be used as evidence for AC5/AC6.
4. **Quote SEVEN measured numbers** (revision 3.1 — adds the cued side of the inversion, N3): (a) D2a's
   measured **minimum planar distance** `LP_Rock` → minable node + the sub-2.6u pair count; (b) the three set
   **sizes**, proving none was empty; (c) the achieved **minimum apex-height ratio** (reported); (d) the
   achieved **maximum stand ratio `S`** across all `LP_Rock` — the figure that must hold below 1.0 (AC1a);
   (e) **NEW —** the achieved **minimum stand ratio `S`** across all `OreNode` + `Boulder`, on the same
   metric, and the **gap** between it and (d) — the figure that must be positive (AC1b/AC1c). **If the
   `gap ≤ 0.05` lever fired, quote BOTH the pre- and post-lever gap and say so explicitly** (revision 3.2);
   a single post-lever number hides that the gate came back tight, which is the thing the next round needs
   to know. (f) the
   achieved **maximum `LP_Rock` nearest-neighbour distance** — the figure that must hold below 3.0u; (g) the
   **pixel** width (full planar extent) × height (apex above ground) of one decorative and one minable
   instance on the default-framing capture, per §0.1's convention.

**Soak probe targets for the Sponsor** (one-line asks, not a checklist to interpret):

- *"Walk the 9–17u loop. Without clicking anything, point at every stone you think you can mine."*
- *"Do the flat rocks still read as stone, or did they become puddles/pancakes?"* — the D1 risk.
- *"Does the shoreline still feel decorated, or did it get emptier — or did the small ones vanish
  altogether?"* — the scale-cap risk **and** round 3's new floor-visibility risk (§5.1: the smallest slabs
  render ~3 px tall).
- *"Do the flat rocks look like they belong together in little groups, or just randomly sprinkled?"* — D4.

**Predict-Before-Soak (falsifiable, graded against the soak):**

> With D1 + D2 + D4 shipped, the Sponsor will identify minable stones with **zero dead-click attempts** on a
> single pass of the 9–17u loop, and will NOT report the decorative rocks reading as "puddles," "pancakes,"
> or "mounds." **The prediction I expect to be wrong, if any:** that the *boulder* is now unambiguous —
> the boulder gains no positive cue in Tier 1, only the contrast of everything around it lying down, so
> "the big one is fine but I'm still not sure about it" is the most likely partial verdict, and Tier 2 (§6)
> is pre-staged for exactly that.
>
> **Round 3 adds a second, independently gradeable prediction — this one about the framing claim itself,
> and revision 3.1 tightens its bounds onto the corrected rate (§8.0):** on the default-framing shipped
> capture, the decorative instance will measure **≤ 18 px tall** (§5.1's worst-case ceiling apex 0.518u ×
> 35.6 px/u) and the minable instance **≥ 24 px tall** (§8.0's worst-case ore apex 0.682u × 35.6 px/u).
> Both bounds are now *derived* rather than picked, so a miss is diagnostic rather than arguable.
> **If that fails, §8.0's arithmetic is wrong** and every magnitude
> claim in this revision has to be re-derived from the measured capture instead — which is a *better*
> outcome than discovering it at the soak, and is why AC5 quotes pixels rather than world units.
>
> ⚠ **One honest note on grading this prediction.** Round 3's version of it (`≤ 20 px` / `≥ 28 px`) was
> stated against a px/u rate that revision 3.1 has since found to be ~5% high. A prediction graded against
> a ruler that later moved is not a clean falsification either way — **grade only the bounds above.**

**Bounded convergence claim.** This document is **spec-only**: no build, no capture, no test, no shipped
evidence. Bars tested: **none.** The direction is unvalidated until a soak exercises it. What IS verified
here is the *diagnosis*: every value in §2 and §8.0 was re-read from source at `origin/main` **`750f190`**
in round 3 with the cited line numbers, including the rim inversion (§2.1) and the collar inversion (§2.4),
both of which are present-tense defects independent of whether this direction is picked.
**Revision 3.1 re-verified that boundary against `origin/main` `0f14b4f`:** `git diff --name-only
750f190..origin/main` returns **no `Assets/**` path at all** (only `.claude/docs/`, `team/` and
`tools/debug/` markdown), so every line anchor in this spec is current as of `0f14b4f`, not merely as of
`750f190`. What DID move on `main` is `team/quality-bars.md` — see the KNOWN-INCOMPLETE note in the
revision header.
**Explicitly NOT verified — the honest boundary of this round:** the pixel figures in §8.0 are *arithmetic
from verified camera constants*, not measurements from a capture (AC5 is where they become evidence); the
1.6×-already-fails calibration is *derived from the shipped values plus the Sponsor's dead-click report*,
not from an instrumented A/B; **the C1–C4 checks added to Bar 10 in PR #386 have NOT been run against this
direction (revision-header note; C1 is the open one)**; and no claim here has been through a build, a test
run, or a soak.

---

## 9. Candidate bar wording (ticket AC1 asks for the bar to be named)

Offered as a **candidate**, not an entry — `team/quality-bars.md` is Sponsor-confirmed and maintained via
`/name-the-bar`, so this belongs in its "Open / unconfirmed" queue, not in the Bars table, until the soak
confirms or corrects it.

> **Candidate Bar — interactive-vs-scenery must be readable by POSTURE, and the posture cue must be
> measured in PIXELS at the default camera.** Two world objects that share a material family must not share
> a *posture*. If one carries a verb and the other does not, the non-interactive one changes its **stand
> ratio** — its apex stops clearing its own footprint radius, and it stays that way on **every instance**;
> the interactive one stands up. **The class that changes is always the one with no gameplay
> contract attached** (no verb, no yield, no carve, no timer, no capture harness) — never the hero prop.
> **State the cue as a categorical inversion, never as a size ratio:** on a procedurally-jittered mesh a
> height ratio is a *nominal* that collapses at the tail (`86cav8ybj` §2.3 — a claimed ≥2× floor measured
> 1.3× worst-case), whereas a stand-ratio inversion holds at every draw and is cheap to assert per instance.
>
> **And DECLARE THE MEASUREMENT CONVENTION IN THE SAME BREATH, because the inversion is invisible without
> it** (`86cazhvdc` N1/N2). *Width* means full planar extent; *height* means apex above ground; the stand
> ratio `b/a` is apex ÷ planar **half**-extent and is NOT a width ratio. Say which, once, at first use. A
> spec that mixes half- and full-width silently will state a figure that misses by 40% and a plain-language
> gloss its own table contradicts — and both will survive review, because each is individually reproducible
> from an unstated convention. **A crossing of `b/a = 1` is NOT "taller than wide": under a down-pitched
> camera almost every ground prop is wider than tall, and the thing the player actually reads is the
> MAGNITUDE the crossing produces, not the crossing.**
>
> **And the magnitude clause, which is the half round 2 was missing (`86caz5na6`): the cue's separation is
> stated in PIXELS at the project's default camera, and it is compared against a separation already known
> to FAIL — never against zero.** Far Horizon's default orbit pitches **55° down**, which foreshortens
> vertical extent by ×0.57 and leaves footprint intact — so it *suppresses* every height cue by nearly half
> and *amplifies* every footprint-shape cue by ~1.5×. A world-space number is not a cue budget; the
> on-screen number is. In this case the shipped world already separates the two classes by **1.6×** apparent
> flatness and that demonstrably does not read — so the bar is *roughly double the known-failing
> separation*, not *some difference exists*.
>
> **Check (three, all required): (1)** desaturate the shipped-build capture and ask "point at the ones you
> can use"; **(2)** gate CI on the **measured worst-case** aspect across every instance, never on a derived
> constant; **(3)** quote the **pixel** extents of one cued and one non-cued instance from a capture taken at
> the DEFAULT camera parameters — a composed or side-profile frame is diagnostic, never evidence. WHY: the
> mine gate can be perfectly correct and the world still invite dead-clicks; a shared-palette style
> deliberately removes hue as a discriminator, so posture is the only channel left that scales across a
> whole prop family; and a posture difference that is real in world units can still be invisible at the
> angle the game is actually played from.

Falsifiable, and it fails loudly in two distinct ways: if a soak shows players still dead-clicking a
lying-down slab, the bar is wrong and the discriminator has to move up to mesh authoring; if AC5's measured
pixels contradict §8.0's arithmetic, the *magnitude clause's* numbers are wrong and have to be re-derived
from the capture. **Both are worth knowing; only the first invalidates the posture idea.**

---

## 10. Open items for the Sponsor (direction-pick, per ticket AC2)

### 10.1 Pick the direction

Recommended: **Tier 1 = posture (D1) + company (D4)** (§3, §5.1, §5.4). Alternatives on the table and their
costs are enumerated in §7 so the pick is informed rather than a menu. Unchanged from round 2 except that
"solitude" is now delivered by D4 (local, visible) instead of §4's radial split (global, invisible).

### 10.2 NEW (round 3) — **the second channel is a Sponsor pick, and the ticket needs one**

This is the item round 3 exists to surface. §0 establishes that the round-2 direction ships **one**
perceptible channel; Bar 10 requires **two on different axes**. Three candidates, all costed:

| | Option | Axis | Magnitude at default framing | Cost | Recommendation |
|---|---|---|---|---|---|
| **P1** | **D4 — company vs solitude** (§5.4): tighten the decorative cluster constant so every scenery rock provably has a peer closer than any minable node has one | **POSITION** | peers ~36–68 px apart vs ≥132 px isolation, both inside one ground window | **one constant + the promoted company pass.** Possible `RockVerifyCapture` re-baseline; minable pools cannot move | ✅ **RECOMMENDED** — cheapest, board-supported (`21h12_49`, `21h22_52`), categorical by construction, and mostly already shipped |
| **P2** | Grass-collar inversion (§2.4) | POSITION | **~0 px** — grass is ~0.005 tufts/u², so the collar the minable class lacks has on average no tuft in it | grass-loop RNG shift + capture re-baseline; grazes the Sponsor-rejected #130 "grass through stone" defect | ❌ **Ruled out on measurement** (§7) — listed so the option is visibly considered, not silently dropped |
| **P3** | Value/luminance inversion — make minable stone the *lighter* grey (it is currently the **darkest**, §2 COLOUR) | **COLOUR** | unquantified; a ~0.12-linear step between warm greys is what fog/bloom/grading eat first | two colour constants | ⚠️ **Do it as HYGIENE regardless** (the current inversion is a defect), but **do not count it** as the second channel — Bar 10 ranks colour last and the mid-green world eats hue |

> **The decision the Sponsor is actually making:** *"Is the flat-vs-standing shape difference enough on its
> own, or do I also want the scenery to visibly clump?"* If the answer is "shape is enough," that is a
> legitimate call — but it means **shipping a known single-channel cue**, and this spec would want that
> recorded as a deliberate accepted risk rather than an oversight. **I am not entitled to settle it:** it is
> subjective-feel, the ticket is `needs-soak`, and P1 changes how the whole shoreline is composed.

### 10.3 The taste call inside D1 — three options, with HONEST numbers

Round 1 quoted these at **2.7×** and **2.1×**; §2.3 shows both figures were an inconsistent middle — neither
nominal nor worst-case. Corrected, with worst-case shown **alongside** nominal, because a number you pick
between must be the number you actually get:

**Revision 3.1 restates the aspect column on the declared width convention (§0.1, N2).** Round 3 gave it as
`a/b` — a planar **half**-extent reciprocal — at two different `P` nominals (1.05 for A/C, 1.00 for B), which
is the same unstated-convention defect as N1. It is now **full planar extent ÷ apex above ground at the
nominal draw `s = 0.90`** (the same instance across all three rows, which is what makes them comparable),
with the on-screen figure beside it:

**Revision 3.2 adds the column the table was missing, and it changes the shape of the choice (Priya's
`REQUEST_CHANGES` on PR #391).** Every column above was a *look* number; none of them told the Sponsor
whether the option **passes the gate this same document declares**. §8.2's AC1c requires the decorative and
minable `S` populations to be **disjoint**, against a minable floor of **`S = 0.787`**. That is a hard CI
gate, not a preference — so it belongs in the table beside the taste columns:

| Option | Squash `q` | Scale cap | Apex band | Flatness, world → **on screen** | Height ratio **nominal** | Height ratio **worst case** | **Nominal apex on screen** (§8.0) | ⛔ **Decorative `S` ceiling on AC1c's metric → margin vs the 0.787 minable floor** |
|---|---|---|---|---|---|---|---|---|
| **A — recommended** | 0.60 | 1.25 | 0.09–0.52u | 0.99 ÷ 0.243 = **4.07 : 1** → **5.82 : 1** | **3.55×** | **1.32×** | **~9 px** (floor ~3 px) | **`S` = 0.701** (0.739 on the tilted-AABB bound) → **+0.086 … +0.048** — ✅ **PASSES on both readings** |
| **B — conservative** ("mound" risk-averse, and round 3's answer to the vanishing-floor risk) | 0.72 | 1.15 | 0.10–0.54u | 0.99 ÷ 0.288 = **3.44 : 1** → **4.91 : 1** | **3.18×** | **1.27×** | **~10 px** (floor ~4 px) | **`S` = 0.813** (0.823 on the tilted-AABB bound) → **−0.026 … −0.036** — ⛔ **FAILS on both readings** |
| **C — keep the mass** (cap unchanged) | 0.60 | 1.55 | 0.09–0.64u | 0.99 ÷ 0.243 = **4.07 : 1** → **5.82 : 1** | **3.04×** | **1.06×** | **~9 px** (ceiling ~23 px) | **`S` = 0.701** — identical to A → **+0.086 … +0.048** — ✅ **PASSES** (the scale cap cancels out of `S`; §8.2) |

*(C's flatness equals A's at the nominal draw because they share `q`; C differs only in how far the band's
ceiling reaches, which is what its worst-case column exists to show.)*

**Read the worst-case column, because it is the whole reason this table was re-derived.** All three clear 2×
comfortably at nominal. **None** clears 2× at the tail. And **option C's worst case is 1.06× — the tallest
decorative slab essentially REACHES the apex of the shortest of the 24 ore nodes.** That is the concrete
cost of keeping the 1.55 cap, and round 1's "ratio still 2.1×" concealed it entirely. Option A has the best
worst case and the stronger read; C is the one I would now argue against on evidence rather than taste.

**Round 3's addition — the height columns are now a REPORT, not the decision.** §8.0 shows the camera
suppresses height by ×0.57, so the difference between A's 1.32× and C's 1.06× worst case is a handful of
pixels either way. **In all three the stand-ratio inversion holds at every draw on the decorative side** (§2.3) and it is the channel
the framing amplifies. So what the Sponsor is really choosing is **how much "decoratedness" the shoreline
keeps** — and, new in round 3, **whether the smallest slabs stay visible at all** (§5.1: at option A's floor
they render ~3 px tall against ~11 px grass blades).

---

⛔ **REVISION 3.2 — READ THIS BEFORE PICKING. Revision 3.1 closed this section by recommending B, and B as
specified cannot pass AC1c.** That recommendation was made on the look columns alone, before the gate
column existed; it was a decision trap, and retracting it is the whole point of this revision. **B is NOT
deleted** — the worry it answers is real and the Sponsor may still want it. What follows is what it costs,
stated so the choice is informed rather than blocked.

**Why B fails.** `q` is the one knob AC1c is sensitive to, and B *is* a `q` choice. At `q = 0.72` the
decorative ceiling reaches `S = 0.813` against a minable floor of `0.787` — the populations **overlap**, so
`min(S over minable) > max(S over decorative)` is false and AC1c is red. Break-even sits at **`q ≈ 0.693`**;
a testable 0.05 gap needs **`q ≈ 0.639`**. Both are *below* B and *toward* A, so "lower `q` a little to fix
it" is not a repair — it is a partial revert to A.

**Every other lever, enumerated, because "no lever exists" is a claim that has to be tested rather than
asserted:**

| Lever | Effect on B | Usable? |
|---|---|---|
| **Scale cap** (B's other knob, 1.15) | **Exactly zero.** `b`, `a` and the sink are all ∝ `s`, so `s` divides out of `S` entirely (§8.2). | ❌ Not a lever at all |
| **Tilt band** (8–22°) | Dropping tilt to **0°** moves B only 0.813 → **0.800** — *still above 0.787*. B fails even with the tilt removed entirely, and removing it destroys the mound protection B exists to buy. | ❌ Cannot rescue it, and self-defeating |
| **Sink coefficient** (§5.1's `−0.08 × s` pin) | Re-pinning to **`−0.120 × s`** does deliver a 0.05 gap at the ceiling. But at the *floor* draw it demands 0.066u of sink against §5.1's 40 %-of-pre-sink-apex safety clamp, which allows only **0.060u** — so the clamp binds and the smallest slab lands at **0.090u ≈ 3.2 px**, against B-as-specified's 3.8 px and option A's 3.0 px. | ⚠ **Arithmetically real, but it consumes ~75 % of B's own benefit** |
| **Minable-side change** | Would close the gap trivially. | ⛔ **Forbidden** — ticket AC3 + §8.2 OOS |

**So the honest verdict: B as written in this table cannot ship against AC1c.** The only repair that keeps
`q = 0.72` is the deeper sink, and it hands back three quarters of the floor-visibility advantage that is
the entire reason B is on the table — B would end up 0.2 px better than A at the floor instead of 0.8 px,
having spent the gate's whole margin to get there. **I have deliberately not folded that repair into B as a
"B-prime".** It re-pins a §5.1 value on the Sponsor's behalf to make his own option survivable, which is
settling the pick by the back door; if he wants B, that is the shape it would have to take and it should be
chosen with the cost visible.

**And the worry B exists to answer has a lever that costs the gate NOTHING.** §5.1 already names it: if the
smallest slabs read as absent, **raise the scale FLOOR** (currently 0.55), do not relax the squash. Because
`s` cancels out of `S`, **that lever is AC1c-neutral by construction** — every `+0.10` of scale floor buys
**+0.55 px** at the floor draw at exactly zero cost to the gate, where the same worry paid for through `q`
costs the gate everything. That is a new finding from this revision's arithmetic, and it is the strongest
argument that the vanishing-floor risk and the squash dial should never have been coupled.

**What remains genuinely his, and I am not settling any of it:** A vs C (how much "decoratedness" the
shoreline keeps — a real taste call, and both pass the gate); whether to take B anyway with the deeper-sink
repair and its cost; whether to treat the vanishing-floor risk pre-emptively via the scale floor or wait for
the soak to report it. **C remains the one I would argue against on evidence** — its 1.06× worst-case height
ratio means the tallest decorative slab essentially reaches the shortest ore node's apex — but that is an
argument, not a gate, and C passes AC1c exactly as A does.

### 10.4 Tier 2 pre-authorisation

If the boulder reads ambiguous at soak, may the chip skirt (§6) go straight into a follow-up, or should it
come back for a second direction-pick?

**The hazard, stated inline so a "yes" is actionable — revision 3.1, and it is the reason round 3's version
of this question could not honestly be answered.** `MineableNodeState.TryPlanarFootprint`
(`MineOre.cs:812`) encapsulates **every** renderer inside a minable node (`b.Encapsulate(_renderers[i]
.bounds)`, `:821`) and feeds the resulting planar half-extent into `MovementCarveWorldRadius` (`:711`), so
**any chip child that pokes outside the boulder's own planar silhouette widens the NavMesh carve** — which
re-opens the round-8 defect the Sponsor already rejected once, verbatim in the source: *"im blocked but
already at this distance in the screenshot, not at the edge of the boulder"* (`MineOre.cs:686-687`), the
invisible-wall verdict.

**So what a "yes" here actually pre-authorises, and what it does not:**

- ✅ **Yes authorises the CARVE-NEUTRAL form only** — chips kept strictly inside the body's planar
  silhouette, with `_carveFootprint` quoted before/after in the PR and `MineBoulderPlacementObstacleTests` +
  `BoulderSceneTests` green. That variant cannot move a carve by construction, so it is a pure look change
  and needs no second pick.
- ❌ **Yes does NOT authorise the other resolution §6 names** — restricting `TryPlanarFootprint` to the body
  renderer. That changes shipped blocking behaviour on **every** minable node, ore included, on a code path
  the Sponsor has already soak-rejected once. **It comes back for its own decision** regardless of the
  answer here.

**Cost of a "no":** one extra direction-pick round after the soak, on a beat that is already fully specified
— i.e. latency, not rework.

---

## 11. Cross-references

- **Ticket** `86cav8ybj` (this direction) · `86cacewju` (hero-prop bevel/chamfer lineage — deferred, NOT
  touched) · `86cahhfkc` (RCK-1 rock rim) · `86caamnnj` (the shader rim term) · `86ca8m5zu` (rock soakfix2,
  the Y-squash precedent) · `86caffwv5` round-7/8 (the minable-node navmesh carve + invisible-wall verdict)
  · `86cadj4g7` (grass-in-stone footprint rule).
- **Bars:** `team/quality-bars.md` Bar 10 (single-channel collapse — **and specifically its 2026-07-31
  "a channel must VARY between cued and non-cued instances" amendment plus the `86caz5na6` KNOWN-INCOMPLETE
  note, which are what round 3 re-audits against**). ⚠ **CITE REPOINTED in revision 3.2 — the anchor moved
  under this spec while PR #391 was open.** Revision 3.1 cited **§ "Bar 10 — the four checks"**; **PR #395
  renamed that heading to `## Bar 10 — the standard in full, and the four checks`** (verified on `main`
  `2d0658b`), so the 3.1 anchor is stale. The durable anchor is the **sub-§ that actually carries the
  figure** — **`§ "The default gameplay framing — the one framing a magnitude claim may be stated against"`**
  — not the parent Bar-10 heading, which has now been reworded twice. Revision 3.1 takes **one** thing from
  it: its canonical figure `720 / (2 × 14 × tan 22.5°) = 720 / 11.60 ≈ 62 px per world metre` (quoted
  verbatim from that sub-§ on `2d0658b`), which is what exposed §8.0's px-per-unit error — and **defers the
  C1–C4 re-audit to round 4** (revision-header KNOWN-INCOMPLETE note). Also Bar 3 (material-honest, pattern via geometry), Bar 4
  (real-world anchor + side profile), Bar 6 (the board is a guide, not a contract). Bar 1 (organic, never
  geometric) also constrains D4 — a tightened cluster must still read as litter, not as a formation.
- **Docs:** `.claude/docs/art-direction.md` (board; the rock language in `21h10_44`, `21h12_49`,
  `21h21_30`, `21h22_52`) · `.claude/docs/lowpoly-quality.md` §0 (anchor + silhouette), §1 (do-not-regress
  flat-shading/normals), §2 Rec 4 (rim), §2 Rec 7 (seeded rotation/lean on scatter — D1's tilt widening is
  that pattern), §3 (screen-space outlines ruled out) · `.claude/docs/game-juice.md` §0 + hard-don'ts
  (amplitude ceiling — why MOTION is not on the axis menu) · `.claude/docs/unity6-mastery.md` §2
  (shared-material batching — D1/D4 add **zero** new materials and zero draw calls; D2a adds no runtime work
  at all — it is an editor/test-time measurement, and its normal outcome is zero scene change).
  **NOTE on citation style (round 3, reaffirmed in revision 3.1, QUALIFIED in revision 3.2):** `.claude/docs`
  and `team/quality-bars.md` are cited by **§ anchor, never by line number** — Bar 10's own third-instance
  note records three cites shifting inside one PR, and PR #386 added ~366 lines to that same file without
  invalidating a single § anchor here. ⚠ **PR #395 then DID invalidate one** — it renamed
  `## Bar 10 — the four checks` → `## Bar 10 — the standard in full, and the four checks`, which is the
  anchor revision 3.1 cited. **So § anchors are more durable than line numbers, not immune**, and the lesson
  is sharper than "prefer anchors": **cite the deepest sub-§ that actually carries the claim you are using.**
  Parent headings get reworded as a doc grows (this one twice in two PRs); the sub-§ carrying a specific
  figure is the stable surface. Applied above. The
  `Assets/**` cites in this doc are line-anchored because the claims are arithmetic on specific literals; all
  of them carry the ref they were verified at, and **round 3 found that every
  `MovementCameraScene.cs` cite from round 2's `840a1c6` had drifted** — re-verify before quoting them
  onward. **Revision 3.1 re-verified them at `0f14b4f`:** `git diff --name-only 750f190..origin/main`
  returns no `Assets/**` path, so every `Assets/**` anchor in this spec is current at `0f14b4f` — the ref to
  quote onward is now **`0f14b4f`**, not `750f190`.
- **Memory:** `[[unity-procedural-committed-assets-go-stale]]` (the shared failure domain both channels sit
  in — §8.1) · `[[physical-features-anchor-realworld-not-metric]]` (Bar 4's mechanism; §5.1's anchor
  sentence) · `[[verify-grounding-soaks-by-gameplay-cam-visual]]` (why §8.0 exists and why the side-profile
  shot is diagnostic-only).
- **Board references looked at for this spec:** `inspiration/2026-06-12_21h10_44.png` (low half-buried
  rounds vs the standing shard cluster — the two postures, side by side, in the Sponsor's own reference),
  `21h12_49` (flat grey rounds settled in the grass by the stump), `21h21_30` (standing columnar outcrop
  with loose rounds at its foot), `21h22_52` (decorative stone as low half-buried litter along a path).
- **Sibling specs:** `team/uma-ux/world-look-polish-direction.md`, `team/uma-ux/pre-soak-visual-audit.md`,
  `team/uma-ux/status-effect-readability-spec.md` (the same channel-discipline reasoning applied to HUD).
