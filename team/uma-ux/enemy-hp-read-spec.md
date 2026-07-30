# Enemy-HP Read — Transient Above-Head Pip-Row (implementable spec)

**Ticket:** `86caxhfg2` (feat(combat): enemy-HP read — transient above-head pip-row on the LootPrompt anchor).
**Owner (impl):** Drew · **Reviewer:** Devon · **Spec author:** Uma · **Lane:** Unity-build, `needs-soak`.
**Work-type:** spec (design-only; no code in this PR).

> **⛔ This doc does NOT un-defer the ticket.** `86caxhfg2` is sequenced behind **`86caxjwb3`** (enemy body-level
> hit feedback — `_HitFlash` + flinch + the pooled dust puff) by Sponsor decision 2026-07-27, and the
> *exists-at-all* question is settled at **that** ticket's soak (its AC6(c)), not here. This spec answers the
> **execution** questions so that IF the body soak returns *"still want it"*, the ticket is dispatchable the same
> day with no second spec round. If the body soak returns *"already answered"*, this doc closes with the ticket
> and §2's arithmetic still stands as a balance finding worth keeping.

**Supersedes for implementation detail:** [`hp-hud-polish-spec.md`](hp-hud-polish-spec.md) **§6** (the merged
parent — PR #339, `e13a51e`). §6 established WHAT and WHY; this doc establishes HOW, and **corrects two premises
in it** (§1.3 and §1.4 below). Where the two disagree, this doc wins and §6 should be read with the pointer here.

**Builds on (do NOT duplicate):** [`combat-cluster-design-brief.md`](combat-cluster-design-brief.md) §1.2 / §2.5
(the BODY read — owned by `86caxjwb3`, cited not absorbed) + §4 (primitive discipline) ·
[`status-effect-readability-spec.md`](status-effect-readability-spec.md) §3.2 (the head-anchor precedence rule
this spec extends to enemy heads) · [`style-guide-v2.md`](style-guide-v2.md) §6 (the bone anchor `#CFC6AD`,
sub-1.0 palette, the HUD-plate-over-saturated-green watch item) · [`hud-three-bar-spec.md`](hud-three-bar-spec.md)
(the segment/plate grammar) · `.claude/docs/game-juice.md` §0/§1/§2 (every amplitude cap here) ·
`team/quality-bars.md` **#2 / #7 / #9 / #10**.

**Board (looked at the images, not the prose):** `inspiration/2026-06-12_21h16_13.png` and
`inspiration/2026-06-12_21h13_31.png`. Both confirm the value story this spec rests on — the world is **high-key**:
bright saturated grass (near-white in the sun), mid-value canopies, bright blue sky. There is almost **no dark
value anywhere in frame**. That is why the dark plate is the load-bearing element: it is the rarest value in the
world, so a small dark chip reads instantly as *"this is UI, and it belongs to that body"* against grass, canopy
AND sky, and a pale bone pip on it holds at orbit distance where any mid-value hue would be eaten by the green.
It is also why the plate must stay **small** — a dark rectangle is the most alien thing this world can show.

---

## 0. Tonal anchor (read this first)

> **The pip-row is not a health bar. It is the animal's breathing getting shorter.** You hit a boar; for a moment
> you can see how much fight it has left in it, and then the world goes back to being a quiet green island with an
> animal in it. It is a glance, not a readout. It belongs to the *creature*, never to *you* — pale, small, dim,
> and gone before you have finished the next swing. If a player ever describes it as "the enemy health bar," the
> execution has failed even if every number is correct.

**The gate:** at any instant during a fight, the pip-row must be **quieter than the body it sits above** — dimmer,
smaller, and slower to change than the `_HitFlash` / flinch / dust puff that `86caxjwb3` puts on the creature
itself. The body answers *"did I connect?"*; the pip-row answers *"is it nearly down?"* — and only the second
question earns UI. **If a beat makes the pip-row the brightest or fastest thing in the frame on an impact, cut
the beat, not the body.**

**The load-bearing call of this spec:** the row reads on **FORM + POSITION + VALUE**, never on hue or motion —
five discrete blocks (form), locked to that creature's own head (position), pale-on-dark (value). Quality-bar #10
is satisfied three times over and the desaturation check passes by construction, because **there is no hue in the
element at all**.

---

## 1. Ground truth (read from `origin/main`, quoted not inferred)

Every value below was read during this spec's authoring. Four of them change what the ticket assumes.

### 1.1 The anchor path (verified)

| Fact | Source |
|---|---|
| `headAnchorHeight = 2.2f` | `LootPrompt.cs:62` |
| `Camera.WorldToScreenPoint(head)`, hidden when `sp.z <= 0f` | `LootPrompt.cs:174-176` |
| Screen clamp at `ScreenMargin = 8f`, `HeadGapPx = 6f`, `PillH = 30f` | `LootPrompt.cs:69-72`, applied `:191-192` |
| `PlateAlpha = 0.55f` black plate, `Cream` ink | `LootPrompt.cs:65-67`, `:194-197` |
| Pure priority seam `ResolveInteractionPrompt(...)` | `LootPrompt.cs:212-220` |

### 1.2 The combat surface (verified)

| Fact | Value | Source |
|---|---|---|
| `Health` read surface | `Current` / `Max` / `Current01` / `IsDead` / `event Action<float> Changed` / `event Action Died` | `Health.cs:80-97` |
| Damage math | `amount × resistance.Multiplier(type) × damageTakenMul` | `Health.cs:151` |
| Boar HP per tier | easy **32** / med **40** / hard **50** | `BoarEnemy.cs:40,42,44` |
| Boar resistance | pierce **×2.0**, slash **×0.75**, blunt ×1.0 | `BoarEnemy.cs:49,54` |
| Snake HP | **24, FLAT — no per-tier HP** (only bite damage is per-tier) | `SnakeEnemy.cs:32`, `ApplyDifficulty` at `:95-100` |
| Snake resistance | pierce **×1.6**, slash ×1.0 | `SnakeEnemy.cs:36` |
| Enemy `damageTakenMul` | stays **1.0** — `BoarEnemy`/`SnakeEnemy.ApplyDifficulty` write `Health.max` / gore / bite only, never `damageTakenMul` | `BoarEnemy.cs:117-131`, `SnakeEnemy.cs:95-100` |
| The player→enemy strike seam | `PerformAttack` → `float removed = target.ApplyDamage(...)`; `if (removed > 0f) HitsLanded++` | `MeleeAttack.cs:229-231` |
| Weapon damage set | 15 defs, 4 → 21 base damage | `WeaponCatalog.cs:62-129` |
| Emptied-segment colour | `Charcoal` `#2E2A2B` (0.18, 0.165, 0.17) | `SurvivalHud.cs:83` |
| Warm-cream | `Cream` `#EAD9B8` (0.92, 0.85, 0.72) | `SurvivalHud.cs:84` |
| Bone anchor | `#CFC6AD` (0.81, 0.78, 0.68) | `style-guide-v2.md` §6 |

### 1.3 ⚠ CORRECTION 1 — `LootPrompt` anchors above the **PLAYER's** head, not the target's

`_playerT = looter != null && looter.player != null ? looter.player : transform;` (`LootPrompt.cs:112`), projected
at `_playerT.position + Vector3.up * headAnchorHeight` (`:174`). **Every interaction prompt — "Chop", "Mine
stone", "Press E to pick up berries" — is drawn above the CASTAWAY's head, never above the thing being named.**

The ticket's AC2 says *"Three things can want a head: the interaction/loot pill, a status-effect world cue, and
this pip-row"*, and asks for one arbiter. That framing is **half right**: the interaction pill can never contend
for the *enemy's* head, because it is not drawn there. What the ticket calls "the shared anchor" is a shared
**projection idiom + code path**, not a shared screen position. The practical consequences are in §4 — they change
the arbitration design, not the reuse mandate. The mandate stands: reuse the path, do not fork a second projector.

### 1.4 ⚠ CORRECTION 2 — the player's HP bar is **10 segments today**, not 5

`SurvivalHud.cs:44` is `public const int SegmentCount = 10;` and there is **no `HpSegmentCount`** in the file. The
5-chunky-segment player bar is `86cah7z2q`'s AC1 — Sponsor-locked but **not yet shipped**. So AC3's rationale
(*"5 pips, matching the player's HP grammar … so the player reads it instantly with no new learning"*) is a
**forward dependency on `86cah7z2q` landing first**, not a description of the live build.

**Ruling: ship 5 pips either way, but the reason changes with the order.**
- **If `86cah7z2q` lands first** — 5-and-5 is a shared vocabulary, exactly as the ticket argues.
- **If the pip-row lands first** — 5-vs-10 is *correct hierarchy anyway*: yours is fine-grained and detailed,
  theirs is coarse and provisional. Read it as a feature, not a mismatch, and say so in the Self-Test Report.
- **The arithmetic forbids matching 10 regardless.** Ten pips inside the pinned 64 px pill with 2 px gaps gives
  `(64 − 6 padding − 18 gap) / 10 = 4.0 px` per pip. A 4 px chip is not a readable block at orbit distance. Five
  is the largest count that keeps a legible pip. **The 5 is geometry-driven; the grammar match is a bonus.**

### 1.5 The body read this is secondary to (still not shipped — the reason for the deferral)

`_HitFlash` appears only in Uma spec docs; no `ParticleSystem` / `ObjectPool` / `OnParticleSystemStopped` anywhere
in `Assets/Scripts`. That is **`86caxjwb3`'s** scope. **Cited, not absorbed** — nothing in this doc specifies a
flash, a flinch, or a puff, and §5's amplitude budget is written *relative* to a body read that will exist by the
time this ships.

---

## 2. Q1 — pips vs continuous bar: **PIPS SURVIVE, but NOT as "hits remaining"**

### 2.1 The question the ticket asks, answered from the shipped numbers

> *"Pips read as discrete 'hits remaining' … but they break down if enemy HP isn't cleanly divisible by strike
> damage across weapon tiers."*

**They are not cleanly divisible, and they never will be.** Effective damage = `base × resistance` (enemy
`damageTakenMul` is 1.0 — §1.2). Hits-to-kill on a **MEDIUM boar (40 HP, slash ×0.75, pierce ×2.0)** across the
15 shipped `WeaponCatalog` defs:

| Weapon | Base | Type | Effective | Hits to kill | Pips per hit (1 pip = 8 HP) |
|---|---|---|---|---|---|
| `dagger_wood` | 6 | Slash | 4.5 | **9** | 0.56 |
| `dagger_stone` / `pickaxe_stone` | 8 | Slash | 6.0 | 7 | 0.75 |
| `axe_wood` | 10 | Slash | 7.5 | 6 | 0.94 |
| `dagger_iron` | 10 | Slash | 7.5 | 6 | 0.94 |
| `sword_wood` | 12 | Slash | 9.0 | 5 | 1.13 |
| `pickaxe_iron` | 12 | Slash | 9.0 | 5 | 1.13 |
| `spear_wood` | 6 | Pierce | 12.0 | 4 | 1.50 |
| `axe` (stone) | 14 | Slash | 10.5 | 4 | 1.31 |
| `sword_stone` | 16 | Slash | 12.0 | 4 | 1.50 |
| `axe_iron` | 18 | Slash | 13.5 | 3 | 1.69 |
| `spear` (stone) | 9 | Pierce | 18.0 | 3 | 2.25 |
| `sword_iron` | 21 | Slash | 15.75 | 3 | 1.97 |
| `spear_iron` | 12 | Pierce | 24.0 | **2** | 3.00 |

**Hits-to-kill spans 2 … 9 on one enemy at one tier.** On a snake (24 HP flat, pierce ×1.6) it spans 2 … 4. A
fixed 5-pip row therefore **cannot** mean "hits remaining" — it would be wrong for 13 of 14 weapon/enemy pairings
and would actively lie (5 pips, 9 hits) exactly where the player is weakest and most needs honesty.

> *(Which of the 15 defs are reachable in combat today is a wiring question — `WeaponCatalog.cs:33-58` records
> wood- and new-tier combat wiring as follow-up. The spec must survive all of them because the def set IS the
> balance surface the pip-row reads, and a def that becomes reachable later must not need a UX revision.)*

### 2.2 So what the row means: **a 5-block quantized PROPORTION**

The pips are a **chunky bar**, not a counter. `filled = FloorToInt(Current01 × 5)`, clamped 0..5 — the same FLOOR
rule the shipped HUD uses (`SurvivalHud.cs:361`), which never over-reports. The read the player learns is
*"about a fifth gone / about half gone / nearly out"*, which is exactly the question AC1 names and is
weapon-agnostic by construction.

**This is a re-labelling, not a redesign** — the ticket's geometry, colour and count all stand. But it must be
written down, because "pips = hits" is the intuition a dev or a reviewer will bring, and it will produce wrong
follow-up decisions (e.g. "make it 4 pips so the axe divides evenly" — which would break for every other weapon).

### 2.3 The one place pips genuinely break — and the fix

A pip is 20 % of max HP. **Any hit landing under 20 % moves nothing.** From the table, on a medium boar:

| Weapon | Pips/hit | Share of hits that change NOTHING on the row |
|---|---|---|
| `dagger_wood` | 0.56 | **~44 %** |
| `dagger_stone`, `pickaxe_stone` | 0.75 | ~25 % |
| `axe_wood`, `dagger_iron` | 0.94 | ~6 % |

And it is **worst on hard** (50 HP → pip = 10 HP → `dagger_wood` = 0.45 pips/hit → **~55 % silent hits**), where
the hold is also shortest. A row that appears, shows an unchanged pip count, and fades reads as *"you did
nothing"* — the precise false-negative this ticket exists to prevent, delivered by the element meant to fix it.

**Fix — the DRAINING PIP (value, not width).** Show the fractional remainder as an **alpha** on the leading pip:

```
f      = Mathf.Clamp01(health.Current01)
whole  = Mathf.FloorToInt(f * 5)                 // 0..5 fully-lit bone pips
rem    = f * 5f - whole                          // 0..1 — the fraction of the NEXT pip still alive
drainA = Mathf.Lerp(DrainMinAlpha, 1f, rem)      // DrainMinAlpha = 0.35
```
- pips `0 .. whole-1` → bone `#CFC6AD` at α 1.0
- pip `whole` (when `whole < 5`) → bone `#CFC6AD` at α `drainA`
- pips `whole+1 .. 4` → `Charcoal` `#2E2A2B`
- **Living-floor rule:** while `Current > 0`, at least one pip must render at ≥ `DrainMinAlpha`. If float
  precision ever produces an all-charcoal row on a living enemy, force pip 0 to `DrainMinAlpha`. The mirror of the
  FLOOR rule's honesty guarantee: **never read dead while alive.**
- All of the above is then multiplied by the row's master fade alpha (§3).

**Why alpha and not a partial-width fill:** the pip is 10 × 6 px. A width fill has 10 steps of ~1 px each — under
a 0.55-alpha plate at orbit distance that is noise. A value step from bone-1.0 to bone-0.35 on charcoal is a large
perceptual change in a 60 px² block, survives desaturation (quality-bar #10), and reuses the HUD's existing alpha
grammar (`SurvivalHud` glyph α 1.0 / 0.4; the ±8 % ember breathe). **Every landed hit now moves something**, and
the amount it moves is proportional to the damage — which is also a free, wordless weapon-comparison read that
strengthens quality-bar #9 rather than substituting for it.

### 2.4 Why NOT a continuous bar (the alternative, and why it loses)

1. **It forks the HUD grammar.** The player's own vitality is (or is becoming) blocks. An enemy's being a smooth
   fill teaches two vocabularies for one concept, at the moment of highest cognitive load.
2. **A smooth 64 × 10 fill loses its leading edge at distance.** Segmentation is what gives a tiny element
   readable internal structure; a continuous fill at orbit distance is a dash whose length you cannot judge
   without a reference, and there is no reference next to a boar.
3. **Quality-bar #10.** A continuous bar's only channel is *length*. Blocks give a *countable* form channel plus
   the value channel — two independent, both hue-free.
4. **Tone (a taste claim, flagged as such).** A smooth bar over a creature is the single most recognisable
   action-game nameplate signature in the medium. Blocks read as tally-marks — closer to notches on a stick than
   to a HUD. This one is a judgement, and it is on the Sponsor-input list (§11 Q2).

**Verdict:** pips survive. They are a quantized proportion, not a hit counter, and they need the draining pip to
stay honest for the low-damage weapons. Neither change touches the ticket's ACs — they *sharpen* AC1 and AC3.

---

## 3. Q2 — transient timing: arm, hold, fade, re-arm, die

### 3.1 ARM comes from the STRIKE, not from `Health.Changed`

**This is the load-bearing behavioural call in the spec.**

The axe carries a bleed (`AxeBleedDps = 2f` / 3 s; `AxeIronBleedDps = 3f` — `WeaponCatalog.cs:65,119`), and bleed
ticks route through `Health.ApplyDamage`, which fires `Changed`. If the row **armed** on `Changed`, then:
- a boar you hit once and walked away from keeps re-summoning UI over itself for 3 s of bleed, hold re-armed from
  zero on every tick — a ~6.5 s row on easy for one axe hit; and
- each tick is 2 HP = 0.25 pips, so the row is on screen showing *nothing changing*. Sticky **and** uninformative
  — both failure modes at once. This is the enemy-side sibling of `hp-hud-polish-spec.md` §2.4's DoT strobe.

**Rule — separate ARM from UPDATE:**
- **ARM / re-arm** ← the player's landed strike only: the seam at `MeleeAttack.cs:229-231`, on `removed > 0f`
  (the same predicate that already increments `HitsLanded`). A whiff arms nothing. A strike on an already-dead
  target arms nothing (`ApplyDamage` returns 0). No proximity, no look-at, no camera-target, no
  enemy-attacks-you.
- **UPDATE** ← `Health.Changed` on the armed target, bound while the row lives, **never polled** (AC2 honoured).
  A DoT tick therefore *updates the pips of a row that is already showing* and **does not extend its hold**.

Consequence, stated plainly and deliberately: a bleed that kills an enemy after the row has faded shows nothing.
Correct — the topple and the dust are the death read (`combat-cluster-design-brief.md` §2.5), and re-summoning a
plate over an animal you are no longer fighting is exactly what "the world stays calm" forbids.

### 3.2 The state machine (defaults; Sponsor-soak tunes)

| Phase | Duration | Curve | Notes |
|---|---|---|---|
| **Fade-in** | **0.12 s** | ease-out, **alpha only** | No scale-pop, no slide-in. A pill that grows or slides pulls the eye off the body, which is where the primary read is. 0.12 s ≈ 7 frames — removes the pop without delaying the read. |
| **Hold** | **easy 3.5 / med 2.0 / hard 1.2 s** | — | Ticket AC1/AC4 defaults, unchanged. Measured from the last ARM. |
| **Fade-out** | **0.4 s** | ease-out | Ticket §6 default. |
| **Gone** | — | — | At α ≤ 0.01 the row is **removed from the draw set and unsubscribed** — not drawn at alpha 0. AC5 asserts this. |

Worst case on medium: `0.12 + 2.0 + 0.4 = 2.52 s`. **Total budget cap: no row may ever live longer than
`fadeIn + hold + fadeOut` from its last arm.** No "last target" memory, no re-show on proximity.

**Re-arm:** a new landed strike resets the hold timer to full. If the row is mid-fade-out, alpha eases back to 1.0
over the 0.12 s fade-in rather than snapping — **timers reset, alpha never jumps** (AC5's "re-arms from zero
rather than stacking timers").

**Pip transitions inside a live row** (the only motion the element has — see §5):
- the draining pip's alpha eases to its new value over **0.12 s** (never a step);
- a pip that just went fully out steps to warm-cream `#EAD9B8` at **α ≤ 0.85**, holds **0.06 s**, then eases to
  `Charcoal` over **0.18 s**. **Total ≤ 0.24 s.** This is the player HUD's lost-segment flash
  (`hp-hud-polish-spec.md` §2.3 layer A) at roughly **60 % amplitude and half the duration** — because this is the
  secondary read and it must stay quieter than the body.

### 3.3 Death

On `Health.Died`:
1. the row stops accepting re-arms immediately;
2. pips empty to 5 × `Charcoal` over **0.25 s** (eased) — the "it's out" beat;
3. fade-out over the standard **0.4 s**.

**Cap the whole death read at ≤ 0.7 s**, so the row is gone well before the body finishes settling
(`BoarBodyRig`'s topple). **No tombstone:** a dead enemy never displays a static 0/5 row, and a corpse never
re-arms one. No "killed" flourish, no burst, no XP, no counter (ticket AC3).

---

## 4. Q3 — the shared anchor: what actually collides, and the deterministic order

### 4.1 The anchor HEIGHT must be derived per body — 2.2 m is a castaway number

`headAnchorHeight = 2.2f` is tuned for a ~1.8 m castaway (`LootPrompt.cs:60-62`). A boar's back is well under
that and a snake is on the ground; copying 2.2 m puts the row in the sky above a snake, severing it from its
owner. Neither enemy exposes an authored height, and AC2 forbids per-enemy pip code.

**Rule:** on ARM, compute the anchor **once** from the target's own geometry and cache it on the row record —
encapsulate `GetComponentsInChildren<Renderer>()` bounds, take `bounds.max.y − root.position.y`, add
`HeadClearance = 0.25f`. Fallback `1.0f` if no renderer (a bare test rig). **Once per arm, never per frame** —
the row lives ≤ 3.9 s, so a cached value cannot drift meaningfully, and a toppling boar keeps the anchor it had
when you hit it (which is what you want: the row should not ride the corpse down).

A third enemy added later gets a correct anchor **for free**, satisfying AC2's shared-implementation constraint
without a per-enemy branch.

### 4.2 Clamp for SPILL, hide for OFF-SCREEN (a deliberate delta from `LootPrompt`)

`LootPrompt` clamps unconditionally (`:191-192`) because the player is always on screen and must always be able to
read their prompt. **An enemy is not.** A row clamped to a screen edge over an off-frame boar is an orphan plate
that names nothing — pure noise, and tonally the worst thing this element can do.

**Rule:**
- hide when `sp.z <= 0` (behind camera — AC2/AC5, verbatim from `LootPrompt.cs:176`);
- hide when the **anchor** projects outside `[0, Screen.width] × [0, Screen.height]`;
- otherwise **clamp the pill RECT** inside `ScreenMargin = 8f` so it never spills (AC5's clamp test).
- Cheap robustness guard: hide beyond `MaxDrawDistance = 40 m` — a fleeing bleeder should not leave a pill
  dancing on the horizon. A `const`, not a dial (§8).

Express as one pure static predicate so AC5 asserts it with no scene rig:
`bool TryResolveRowRect(Vector3 sp, float screenW, float screenH, float rowW, float rowH, float gapPx, float extraUpPx, out Rect rect)`.

### 4.3 What can actually contend for an ENEMY head

Per §1.3 the interaction pill is **not** a contender — it lives on the player. On an enemy head there are exactly
two, and one more case at range:

**(a) Status-effect world cue vs the pip-row (a real same-head contention).**
`status-effect-readability-spec.md` §3.2 already rules: **status wins the head anchor**, the other element shifts
up, deterministically, never overlapping. That rule is honoured verbatim here — **no second arbiter, no
re-litigation**:

> **The enemy head stack, from the body upward:** `[head] → status cue band (owns the anchor) → pip-row`.
> When no status cue is active on that body, the pip-row occupies the anchor itself.

Pure predicate: `float EnemyHeadRowOffsetPx(bool statusCueActive) => statusCueActive ? StatusBandH : 0f;` with
`StatusBandH = 26f` (a pinned const sized to the §5.1 stun-star ring / §4.2 poison-pip column). One line, pure,
asserted in EditMode — the same shape `LootPrompt.ResolveInteractionPrompt` (`:212-220`) is asserted in.

Why status wins and not the row, given that the row wants to be tight to its body: the status cues are **rising /
orbiting particles** whose whole read is motion through the space directly above the head; displacing them breaks
the effect. The pip-row is a static plate and reads fine one band higher — and a plate sitting *directly* on the
head anchor would occlude rising poison pips (IMGUI draws over world particles), which would be a silent
cross-spec regression. Precedence preserved, occlusion avoided, no change needed to the sibling spec.

**(b) Two or more enemies' rows colliding in screen space.** Rows arm only on YOUR strike and you can only strike
one target at a time, so realistic concurrency is 1, occasionally 2 during a hold window. The cap exists so a
future pack-spawn cannot paper the frame:

- **`MaxRows = 3`** — a code `const`, **NOT a registry id** (§8 keeps the ticket's two-id budget honest). Beyond
  3, the **oldest-armed** row is retired immediately into the standard 0.4 s fade-out.
- **Placement order = nearest first.** Sort candidates by ascending `sp.z`. The nearest enemy — the one you are
  fighting — keeps the true anchor; farther rows displace. Deterministic and correct by intent.
- **De-overlap:** for each row after the first, if its rect intersects an already-placed rect, push **up** by
  `rowH + 4 px`, max **2 pushes**; if it still collides, **drop the row this frame** (do not stack a third
  ambiguous plate). Pure function, pinned in the AC5 arbitration test.
- **A row never changes which body it belongs to.** Displacement is vertical only, so the horizontal centre still
  points at its owner — the single property that keeps attribution unambiguous with two rows up.

**(c) "Damaged AND lootable."** Today no enemy is an `IPickable` — the implementations are berry bush, pond, log
pile, stick, stone/ore piles (`Assets/Scripts/Runtime/*`), and `DeathHandler.cs:20-21` records that a full
pickable-drop prop is a later ticket. `combat-cluster-design-brief.md` §2.5 says the boar *"settles as a lootable
(meat/hide) or fades"* — so this is a **forward** case. The forward contract, so nobody has to re-derive it:

1. **The two never share a screen position anyway.** The loot pill renders above the **player's** head naming the
   corpse ("Press E to pick up hide"); the pip-row is above the **corpse**. Two bodies, two anchors.
2. **The pip-row is already gone by then.** Death retires the row inside ≤ 0.7 s (§3.3) and a corpse can never
   re-arm one, so an emptied row and a loot pill can co-exist for at most that window — and only if the player is
   already in loot range at the moment of death.
3. **Hard rule:** a lootable corpse **never** shows a pip-row. If a future enemy is lootable *while alive*
   (shearable, tameable), the pip-row still only appears on a landed strike — looting is not a strike.
4. **Screen proximity in melee is real and is handled by (b).** At spear reach (3.6 m) the player's head and the
   enemy's head can project within tens of pixels of each other. The loot pill (`PillH = 30f`) and a 10 px row are
   different sizes at different anchors, and the de-overlap in (b) is **pip-rows only** — the pip-row must **never
   displace the interaction pill**, which is a Sponsor-decided anchor with an action bound to it. If they visually
   crowd, the pip-row is the one that gets pushed up. Name it in the soak checklist (§10).

---

## 5. Q4 — calm-tone amplitude budget

`game-juice.md` §0 asks for one sentence of what the player should feel. Here it is:
**"I can see it's wearing down" — not "I hit it."** The second sentence belongs to the body (`86caxjwb3`).

### 5.1 FORBIDDEN on this element (each with its reason)

| Forbidden | Why |
|---|---|
| **Row nudge / translate of any kind** | The player's own HUD gets a ≤4 px wince nudge on incoming damage (`hp-hud-polish-spec.md` §2.3 B). Copying it here inverts the meaning — a lurching enemy row reads as *you* being hit. |
| **Scale pop / grow-in on the plate or pips** | Pulls the eye off the body at the exact frame the body's flash is the primary read. |
| **Whole-plate flash or plate-alpha spike** | The plate is the calmest thing in the element; flashing the frame around a readout is a slot-machine idiom. |
| **Any hue shift** (toward red / orange / warning colours) | Red is the player's vitality (`SurvivalHud` `VitalRed`/`WoundOrange`/`DarkBlood`). The element has no hue channel at all — that is what makes bar #10 automatic. |
| **Screen shake / Cinemachine Impulse attributable to this element** | `game-juice.md` §2. Impulse belongs to the strike, owned by `86caffwv5`/`86caxjwb3` — the pip-row adds none of its own. |
| **Hit-stop attributable to this element** | Same: hit-stop is the strike's punctuation, hard-capped at 3 frames, and this element must not add to that budget. |
| **Particles of any kind** | The pip-row is IMGUI. No pooled system, no puff — the puff is `86caxjwb3`'s. |
| **Numbers, text, names, level labels, a "killed" flourish** | Ticket OOS; quality-bar #9 (the matchup must stay emergent). |
| **Audio** | No audio bus exists (verified 2026-07-27). `<deferred — no audio bus>` and even then: this element gets none. The strike already has a cue budget; a second layer on the same frame is fatigue by construction (`game-juice.md` §1.3). |

### 5.2 ALLOWED, with the caps

| Beat | Spec | Cap (HARD) |
|---|---|---|
| Row fade-in | alpha 0 → 1, ease-out | **0.12 s**, alpha only |
| Row fade-out | alpha 1 → 0, ease-out | **0.4 s**, alpha only |
| Draining-pip alpha ease | to the new `drainA`, eased | **0.12 s** |
| Lost-pip extinguish | `#EAD9B8` at **α ≤ 0.85**, hold 0.06 s, ease to `Charcoal` 0.18 s | total **≤ 0.24 s** |
| Death empty | 5 × `Charcoal` over 0.25 s, then the standard fade | whole death read **≤ 0.7 s** |

**Every animation on this element is alpha or colour-value. Nothing moves, nothing scales, nothing shakes.** That
is the amplitude discipline in one sentence, and it is trivially auditable at review.

### 5.3 The loudness ordering (the soak's real test)

On any single impact frame, in descending prominence: **body `_HitFlash` > flinch > dust puff > pip-row change >
everything else.** If the soak capture shows the pip-row as the most eye-catching change in the frame, the
element is miscalibrated — and the correct fix is to **lower the pip-row** (drop the extinguish flash to
α ≤ 0.6, or remove it entirely and keep only the value change), **never to raise the body**.

---

## 6. Q5 — difficulty: the READ is identical; only the generosity of TIME changes

**The form, colour, count, position, trigger and state machine are byte-identical at all three tiers.** Only
`enemy_hp_pip_hold` moves: **easy 3.5 / medium 2.0 / hard 1.2 s**.

**Why not vary the form:** a kid on easy and an adult on hard must learn the same vocabulary — and the tier is
live-switchable from the settings panel, so a form that changed per tier would *relabel the UI mid-session*.
Quality-bar #7 asks every system to have three tiers; it does not ask them to look different.

**`enemy_hp_pips_enabled` is GLOBAL, not per-tier, and defaults ON at every tier.** Turning it off on hard was
considered and rejected: it makes hard a *different game* rather than a harder one, and it would make the
ticket's own soak unfalsifiable on the tier where the read is most needed. The flag's job is the one-line soak
revert path (ticket AC4), nothing else.

**⚠ A per-tier asymmetry already exists in the balance layer — name it so nobody "fixes" it.** Boar HP is per-tier
(32 / 40 / 50) while weapon damage is not, so **pip resolution is already tier-dependent**: one pip = 6.4 HP easy,
8.0 medium, 10.0 hard. Combined with the shorter hold, **hard is stingier by two independent mechanisms** — fewer
pips move per hit AND less time to see them. That gradient points the right way and is free. But it is also where
the element is most likely to fail:

> **Soak watch-item (hard tier):** a `dagger_wood`-class weapon on a hard boar moves no pip on **~55 %** of hits,
> inside a 1.2 s window. If hard reads as *"I can't tell if I'm winning"*, the dial to move is the **HOLD**
> (up to ~1.6 s) — **never** the pip count, never the colour, never a per-tier form change.

**A balance inconsistency to log, not to fix here:** the snake's HP is **flat 24 across all tiers**
(`SnakeEnemy.cs:32`; `ApplyDifficulty` sets bite damage only) while the boar's is per-tier. So the snake's pip
resolution is tier-invariant and the boar's is not. That is a **combat-balance** matter (`86caaz4vn` / the
balance lane), **out of scope here** — cited so the pip-row's tier behaviour is not blamed for it.

---

## 7. Geometry + colour (derived arithmetic — do not re-derive)

Pill and pip dimensions are the ticket's / §6's pinned values; the insets below are the arithmetic Drew would
otherwise have to compute, published so two implementations cannot disagree.

| Element | Value |
|---|---|
| Pill (plate) | `64 × 10` px, black at `PlateAlpha = 0.55f` × row alpha |
| Pip | `10 × 6` px, 2 px gaps |
| Run width | `5 × 10 + 4 × 2 = 58` px → **3 px padding each side** (`64 − 58 = 6`) |
| Pip x | `pillX + 3 + i * 12` (i = 0..4) |
| Pip y | `pillY + 2` (10 − 6 = 4 → 2 px above and below) |
| Pill x | `Mathf.Clamp(sp.x − 32f, ScreenMargin, Screen.width − ScreenMargin − 64f)` |
| Pill y (GUI, top-down) | `(Screen.height − sp.y) − HeadGapPx − 10f − extraUpPx`, then clamped inside `ScreenMargin` |
| Filled pip | bone `#CFC6AD` (0.81, 0.78, 0.68) — `style-guide-v2.md` §6 |
| Draining pip | bone `#CFC6AD` at α `Lerp(0.35, 1.0, rem)` |
| Spent pip | `Charcoal` `#2E2A2B` (0.18, 0.165, 0.17) — `SurvivalHud.cs:83` |
| Extinguish flash | `Cream` `#EAD9B8` (0.92, 0.85, 0.72) at α ≤ 0.85 — `SurvivalHud.cs:84` |

**Fixed pixel size at every distance** — no distance scaling. A distance-scaled row is unreadable far away and
oversized in close melee; a fixed 64 × 10 chip is a stable, learnable object.

**Primitive discipline (cite when implementing):** pure IMGUI `GUI.DrawTexture(rect, Texture2D.whiteTexture)`
with `GUI.color`, explicit `Rect`s only, `useGUILayout = false` — the `BootHud`/`SurvivalHud`/`LootPrompt` idiom.
**No** world-space Canvas, **no** UI-Toolkit panel, **no** billboard mesh, **no** second projection helper, **no**
post-process Volume, **no** `MaterialPropertyBlock` (`unity6-mastery.md` §2 — GPU Resident Drawer disqualifier),
and **no** `LowPolyVertexColor` material touch. Every channel sub-1.0. IMGUI also never strips to magenta in the
IL2CPP release, which is why the whole HUD is IMGUI.

**Architecture shape that satisfies AC2 (Devon reviews; not over-prescribed):** ONE scene-level component in the
HUD family owning a **fixed-capacity** array of `MaxRows` row records `{ Health, Transform, anchorY, armTime,
last01, drainA, extinguishAt }`. Armed from the `MeleeAttack` strike seam; subscribes `Health.Changed` + `Died`
per armed target and **unsubscribes on expiry / death / destroy** (AC5's no-leaked-handlers test). Fixed capacity
= no per-frame allocation. `OnGUI` draws ≤ 3 rows from cached state and re-projects the anchors — **no
`FindObjectsOfType`, no `Current01` polling, no allocation in `OnGUI`** (`unity6-mastery.md` §5/§6).

---

## 8. Tunables + registry ids — **exactly two, as the ticket pins them**

| Id | Drives | Default | Per-tier? |
|---|---|---|---|
| `enemy_hp_pip_hold` | §3.2 hold seconds before the fade | **3.5 / 2.0 / 1.2** | **Yes** |
| `enemy_hp_pips_enabled` | §6 master off switch (the soak revert path) | **on** | No — global |

Registered via this feature's **own** `Populate…` method on `SettingsCatalog` — never grow the base `Populate`
signature (`PopulateThirst`/`PopulateChop`/`PopulateCombat`/`PopulateBoar`/`PopulateIron` precedent). The per-tier
dial must write **both** the active field **and** the active tier's map entry, or `ApplyDifficulty` clobbers it
(the documented **dead-knob** class, `SettingsCatalog.cs` `PopulateBoar` note). Any new key binding must be
Danish-layout-agnostic (`[[sponsor-danish-keyboard-layout]]`).

**Do NOT mint further ids.** These are **code consts**, deliberately: `MaxRows = 3`, `DrainMinAlpha = 0.35f`,
`HeadClearance = 0.25f`, `StatusBandH = 26f`, `MaxDrawDistance = 40f`, `FadeInSeconds = 0.12f`,
`FadeOutSeconds = 0.4f`. Rationale: the ticket's two-id budget is part of its M-sizing, and every one of these is
a structural constant the Sponsor has no reason to dial — the two things he *will* want to dial (how long it
lingers, whether it exists) are exactly the two ids above.

---

## 9. Success-tests this spec ADDS to AC5 (the ticket's list stands; these sharpen it)

- **Proportion, not hits.** `filled = FloorToInt(Current01 × 5)` over a swept `Current01` — assert monotonic and
  never over-reporting; assert `Current01 = 0.999` → 4 filled + a draining pip, not 5.
- **The draining pip moves on a sub-pip hit.** Drive `dagger_wood`-magnitude damage (4.5 effective) on a 40 HP
  boar and assert `drainA` **strictly decreases** on every hit, including the ~44 % of hits where `filled` is
  unchanged. *(This is the test that proves the ticket's core promise for low-damage weapons.)*
- **Living floor.** For any `Current > 0`, at least one pip renders at α ≥ `DrainMinAlpha`.
- **Bleed does not extend the hold.** Arm the row, tick a bleed via `Health.ApplyDamage` for 3 s, assert the row
  retires at `arm + hold + fade` regardless of tick count, and that the pips DID update meanwhile.
- **A whiff arms nothing.** `PerformAttack` with a null / dead / out-of-range target → no row.
- **Anchor derivation.** A tall body and a low body produce different anchor heights from the same shared code
  path; a no-renderer rig falls back to 1.0 m. Assert the same component drives boar and snake with no branch.
- **Off-screen hides, edge clamps.** The pure `TryResolveRowRect` returns `false` for `z <= 0` and for an anchor
  outside the screen rect; returns a rect inside `ScreenMargin` for a near-edge anchor.
- **Enemy head stack.** `EnemyHeadRowOffsetPx(statusCueActive: true) == StatusBandH`, `false == 0` — pin the
  order in the assert.
- **Multi-row.** 4 eligible rows → 3 drawn, oldest retired; overlapping rects resolve to non-overlapping,
  deterministic placement with the nearest keeping the true anchor.
- **Never displaces the interaction pill.** With a loot prompt and a pip-row both eligible, the pill's rect is
  byte-identical to its no-pip-row rect.

**Shipped-build capture (windowed)** — ticket AC5's list, plus one: **(f)** two damaged enemies on screen at once
showing two non-overlapping rows, each visually attributable to its own body.

---

## 10. Predict-Before-Soak

**(a) Prediction (falsifiable).** *"Nothing hovers over any animal until I hit it. On a landed hit a small pale
five-block chip fades in above that animal — not above me — inside a couple of frames; it tells me roughly how
much fight it has left; every hit visibly changes it even when the block count doesn't move; it is dimmer and
slower than the flash on the creature's own body; it is completely gone about two and a half seconds after my
last hit on medium; a second animal I hit gets its own chip that never overlaps the first; a dead animal's chip
empties and vanishes with the topple and never lingers as a marker; and at no point is there a number, a name,
a red bar, or anything hovering over a creature I have not engaged."*

**(b) Bounded convergence claim.** Bars **tested**: **#7** (per-tier hold, three tiers), **#2** (eased fade,
never linear), **#9** (matchup legibility — the element must not become a substitute for the emergent reach /
weakness read; §2.3's proportional drain should *strengthen* it by making "the spear does more" visible without a
word), **#10** (≥2 hue-independent channels — form + position + value; desaturate the capture and the read must
survive intact). Bars **NOT tested**: **#1**, **#3**, **#4**, **#5**, **#6**, **#8** — no world, mesh, material,
weapon-sizing or nudge-tool surface is touched.

**(c) The exists-at-all question is NOT judged here.** It is settled at `86caxjwb3`'s soak (its AC6(c)). This
soak judges execution: hold timing, form, colour, arbitration, and whether the row stays quieter than the body.

**(d) After soak:** outcome vs prediction; a refutation is a finding — investigate the foundation before
re-fixing (`[[claim-removed-soak-shows-present-investigate-foundation]]`).

**Contribution to `86caxjwb3`'s judgement (cited, not absorbed):** §2.3's arithmetic says **up to ~55 % of hits
with low-damage weapons cannot move a pip at all**. Whatever answers *"did I connect?"* on those hits must be the
**body**. That is independent evidence that the body read has to land first — and if the body read turns out to
answer *"is it nearly down?"* as well (via flinch intensity and the death-settle), closing this ticket is a fully
defensible outcome.

---

## 11. Sponsor-input items (NONE block implementation)

- **Q1 — pips vs a continuous bar (§2).** The mechanical case is settled (hits-to-kill spans 2–9, so pips cannot
  mean "hits"), but *blocks-vs-smooth-fill* is partly taste. Blocks are recommended and spec'd. `needs-soak`.
- **Q2 — does a smooth bar read as "action game" to him the way it does to me (§2.4 point 4)?** The one
  explicitly-flagged taste claim in this spec.
- **Q3 — the draining pip (§2.3).** Does a half-lit leading block read as *"partly gone"*, or as a rendering
  glitch? This is the fix for silent sub-pip hits; if it reads as a glitch the fallback is to accept silent hits
  and lean entirely on the body read.
- **Q4 — hold timing (§3.2).** 3.5 / 2.0 / 1.2 s. Does medium's 2.0 s feel like a glance or like a lingering
  HUD? Hard's 1.2 s is the one most likely to be too short (§6's watch-item).
- **Q5 — the enemy head stack (§4.3a).** Status cue above the head, pip-row above the status band. Does the row
  feel detached from its animal when a status cue pushes it up 26 px?
- **Q6 — `MaxRows = 3` (§4.3b).** Is three simultaneous rows already too many for this world's calm, or is the
  cap fine because natural concurrency is 1–2?
- **Q7 — 5 pips while the player's own bar is still 10 (§1.4).** Only relevant if this ships before
  `86cah7z2q`. Reads as hierarchy (recommended interpretation) or as an inconsistency?

---

## 12. Out of scope

The enemy **body** read — `_HitFlash`, flinch / hit-react, dust puff, death topple (**`86caxjwb3`** — cited
throughout, absorbed nowhere). Implementing any of this (spec-only PR). The player's own HP HUD (`86cah7z2q`).
Status-effect **definitions** and their cue visuals (`86cah7yuh` / `status-effect-readability-spec.md`) beyond
the head-stack arbitration in §4.3a. Persistent enemy HP bars, nameplates, level labels, target-locked panels,
floating damage numbers, a combat log, any text hint (**forbidden, not deferred** — quality-bar #9). Re-balancing
enemy HP or weapon damage — including the snake's flat-HP inconsistency in §6, which is **logged, not fixed**
(balance lane). Enemy loot drops / corpse pickables (`DeathHandler.cs:20-21` names it a later ticket) beyond the
forward contract in §4.3c. Any post-process Volume, full-screen overlay or chromatic aberration. **Any audio** —
no bus exists (verified 2026-07-27: zero `.ogg`/`.wav`/`.mp3` under `Assets`, zero `AudioSource`/`AudioClip`/
`PlayOneShot` in `Assets/Scripts`). An IMGUI → UI-Toolkit migration.

---

## 13. Decision drafts (for Priya's DECISIONS.md batch — I do not edit that file)

- **Decision draft:** The enemy pip-row is a **5-block quantized PROPORTION read, explicitly NOT "hits
  remaining"** — hits-to-kill spans **2 … 9** on a medium boar across the 15 shipped `WeaponCatalog` defs
  (`spear_iron` 2 → `dagger_wood` 9), so a fixed pip count cannot mean hits for any weapon but one. Five is
  additionally **geometry-forced**: ten pips in the pinned 64 px pill would be 4.0 px each. A continuous bar was
  considered and rejected (forks the HUD grammar, loses its leading edge at orbit distance, single-channel under
  quality-bar #10). (`enemy-hp-read-spec.md` §2.)
- **Decision draft:** Sub-pip hits are covered by a **DRAINING PIP** — the leading pip renders at
  `α = Lerp(0.35, 1.0, remainder)` — because low-damage weapons move no pip on up to **~44 %** (medium boar) /
  **~55 %** (hard boar) of landed hits, which would deliver the exact "I did nothing" false-negative the ticket
  exists to prevent. Value, not width: a 10 × 6 px pip has no readable width granularity, and a value step
  survives desaturation (quality-bar #10). A **living-floor** rule guarantees at least one pip at ≥ 0.35 α while
  `Current > 0` — never read dead while alive. (`enemy-hp-read-spec.md` §2.3.)
- **Decision draft:** The row **ARMS on the player's landed strike** (the `MeleeAttack` seam, `removed > 0f`),
  **not** on `Health.Changed`; `Health.Changed` drives the row's VALUE while it is already showing and never
  extends the hold. Reason: the axe's bleed ticks through `Health.ApplyDamage`, so arming on `Changed` would
  re-summon a plate over a disengaged animal for 3 s of DoT while showing nothing change — the enemy-side sibling
  of the HUD's §2.4 DoT strobe. (`enemy-hp-read-spec.md` §3.1.)
- **Decision draft:** `LootPrompt` anchors above the **PLAYER's** head (`LootPrompt.cs:112,174`), not the
  target's — so "the shared above-head anchor" is a shared **projection idiom and code path**, not a shared screen
  position, and the interaction pill can never contend for an enemy's head. The **enemy** head stack is therefore
  its own deterministic order — `[head] → status cue band → pip-row` — which **honours**
  `status-effect-readability-spec.md` §3.2's "status wins the head anchor" verbatim rather than re-litigating it,
  and additionally prevents an IMGUI plate from occluding rising poison pips. The enemy anchor **height** is
  derived once per arm from the target's renderer bounds (+0.25 m clearance), never the castaway's 2.2 m.
  (`enemy-hp-read-spec.md` §1.3 / §4.)
- **Decision draft:** The enemy row **clamps for spill but HIDES when its anchor is off-screen** — a deliberate
  delta from `LootPrompt`, which clamps unconditionally because the player is always on frame. A clamped row over
  an off-frame enemy is an orphan plate naming nothing. Concurrency is capped at **`MaxRows = 3`** (a code const,
  **not** a third registry id), placed nearest-first with vertical-only de-overlap so a row's horizontal centre
  always still points at its owner; the pip-row **never** displaces the interaction pill.
  (`enemy-hp-read-spec.md` §4.2 / §4.3.)
- **Decision draft:** The element's **entire animation budget is alpha and colour-value** — nothing moves, scales
  or shakes. No row-nudge (that idiom means *you* were hit), no scale-pop, no plate flash, no hue shift, no
  particles, no Impulse or hit-stop of its own. The one permitted accent is a lost-pip extinguish at
  **~60 % of the player HUD's amplitude and half its duration** (`#EAD9B8` α ≤ 0.85, ≤ 0.24 s total). The gate is
  the **loudness ordering**: body flash > flinch > dust > pip-row; if the soak shows the row as the loudest thing
  on an impact, lower the row, never raise the body. (`enemy-hp-read-spec.md` §5.)
- **Decision draft:** **Difficulty changes only the generosity of TIME, never the read.** Form, colour, count,
  position, trigger and state machine are identical at all three tiers; only `enemy_hp_pip_hold`
  (3.5 / 2.0 / 1.2 s) moves, and `enemy_hp_pips_enabled` stays **global and ON at every tier** (turning it off on
  hard makes hard a different game and makes the soak unfalsifiable where the read matters most). A per-tier
  asymmetry already exists for free — boar HP is per-tier (32/40/50) while weapon damage is not, so pip
  resolution is coarser on easy and finer on hard; if hard reads as illegible the dial to move is the **HOLD**,
  never the pip count. **Logged, not fixed:** the snake's HP is flat 24 across all tiers while the boar's is
  per-tier — a balance-lane inconsistency (`86caaz4vn`), out of scope here. (`enemy-hp-read-spec.md` §6.)
- **Decision draft (ordering):** The player's HP bar on `main` is still **10 segments**
  (`SurvivalHud.cs:44 SegmentCount = 10`; no `HpSegmentCount`) — `86cah7z2q`'s 5-segment bar is Sponsor-locked but
  unshipped. The enemy row ships at **5 either way**: if the player bar lands first it is a shared vocabulary; if
  not, 5-vs-10 reads as correct hierarchy (yours detailed, theirs coarse). The count is geometry-forced
  regardless. (`enemy-hp-read-spec.md` §1.4.)

---

## Cross-references

- **Tickets:** `86caxhfg2` (this spec) · **`86caxjwb3`** (⛔ blocking predecessor — enemy body-level hit feedback;
  its soak decides whether this ticket lives) · `86cah7z2q` (parent — HP HUD polish; this spec's §6 origin) ·
  `86cah7yuh` (status effects — shares the enemy head; §4.3a arbitrates) · `86cah7xxp` (POC — `Health`) ·
  `86cah7ydt` (boar) · `86caaz4vn` (snake — the flat-HP balance item in §6) · `86cabcdpn` (combat design lock) ·
  `86caffwv5` (per-class swings — owns hit-stop / Impulse).
- **Code (ground truth, read during authoring):** `Assets/Scripts/Runtime/LootPrompt.cs`
  (`:62` anchor height, `:65-72` plate/margin consts, `:112` **player** transform, `:174-176` projection + `z<=0`,
  `:191-192` clamp, `:212-220` the pure priority seam) · `Combat/Health.cs` (`:80-97` read surface, `:146-161`
  `ApplyDamage`, `:151` the damage formula) · `Combat/MeleeAttack.cs` (`:88-90` `LastDamageDealt`, `:229-231` the
  strike seam this arms from) · `Combat/BoarEnemy.cs` (`:40,42,44` per-tier HP, `:49` pierce ×2.0, `:54` slash
  ×0.75, `:117-131` `ApplyDifficulty`) · `Combat/SnakeEnemy.cs` (`:32` flat 24 HP, `:36` pierce ×1.6, `:95-100`
  `ApplyDifficulty`) · `Combat/ResistanceProfile.cs` (`:41-53` `Multiplier`) · `Combat/WeaponCatalog.cs`
  (`:62-129` the 15 defs' damage consts) · `SurvivalHud.cs` (`:44` `SegmentCount = 10`, `:47` `PlateAlpha`,
  `:83` `Charcoal`, `:84` `Cream`, `:361` the FLOOR rule) · `Combat/DeathHandler.cs` (`:20-21` corpse-pickable is
  a later ticket) · `Settings/SettingsCatalog.cs` (id convention + dead-knob precedent).
- **Docs:** `.claude/docs/game-juice.md` §0 (amplitude is the whole game) / §1 (easing, hit-stop cap, audio
  variation, pooling) / §2 (hard don'ts — every cap in §5) · `.claude/docs/art-direction.md` +
  `inspiration/2026-06-12_21h16_13.png`, `21h13_31.png` (looked at them — the high-key world that makes the dark
  plate the load-bearing value) · `.claude/docs/vision-far-horizon-game-concept.md` (kid → adult difficulty) ·
  `.claude/docs/unity6-mastery.md` §2 (GRD / no MPB) / §5-§6 (no alloc or Find in hot paths).
- **Uma specs:** `hp-hud-polish-spec.md` §6 (the merged parent this implements + corrects) · `§2.3`/`§2.4` (the
  player-side wince + DoT debounce the enemy side deliberately does NOT copy) ·
  `status-effect-readability-spec.md` §3.2 (head-anchor precedence, honoured verbatim) ·
  `combat-cluster-design-brief.md` §1.2 / §2.5 (the body read — `86caxjwb3`) / §4 (primitive discipline) ·
  `style-guide-v2.md` §6 (bone `#CFC6AD`, sub-1.0, the plate-over-saturated-green watch item) ·
  `hud-three-bar-spec.md` (the segment/plate grammar).
- **Bars / memories:** `quality-bars.md` **#2**, **#7**, **#9**, **#10** · `[[difficulty-settings-easy-medium-hard]]`
  · `[[sponsor-danish-keyboard-layout]]` · `[[active-input-not-proximity-auto-for-actions]]` ·
  `[[served-unverified-soaks-need-played-verification]]` · `[[verify-grounding-soaks-by-gameplay-cam-visual]]` ·
  `[[claim-removed-soak-shows-present-investigate-foundation]]` · DECISIONS 2026-07-21 (above-head anchor),
  2026-07-22 (boar soak PASS / bar #9), 2026-07-27 (the sequencing decision that defers this ticket).
