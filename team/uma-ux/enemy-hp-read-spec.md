# Enemy-HP Read — Transient Above-Head Pip-Row (implementable spec)

**Ticket:** `86caxhfg2` (feat(combat): enemy-HP read — transient above-head pip-row on the LootPrompt anchor).
**Owner (impl):** Drew · **Reviewer:** Devon · **Spec author:** Uma · **Lane:** Unity-build, `needs-soak`.
**Work-type:** spec (design-only; no code in this PR).

**📌 Every source claim, diff range and px figure in this doc is pinned to `bf33b655e4953478549f4f74c5a692c39ee3c8f9`**
(the tip of `origin/main` when this revision was authored, 2026-08-01) — **never to the moving ref `origin/main`**.
A magnitude claim written against a moving ref self-falsifies the moment the ref advances, which is the failure this
pin exists to prevent. Re-resolve before relying on any figure after that SHA. Where this doc cites a `file.cs:NN`
line number it was **re-verified at that SHA**; for anything ADDED in this revision the citation is the **symbol**,
per `quality-bars.md` § Bar 10's *"Cite the SYMBOL, never the line number"* rule.

> **🔄 REVISION 2026-08-01 — what changed and why.** Bar #10 moved under this spec after it merged (PR #371,
> `59a6e53`): **PR #380** `90d024b` added the variance clause, **PR #386** `0f14b4f` added checks **C1–C4**, and
> **PR #395** `aeeafa0` moved the standard out of the Bars-table row into its own section (the cell went
> **2,523 → 423 characters**). This revision re-audits the spec against the amended standard and adds two sections:
> **§14** (readability at the canonical gameplay framing — the px figures the spec previously asserted only as
> *"reads at orbit distance"*) and **§15** (the bar #10 C1–C4 audit). It **withdraws** §0's *"satisfied three times
> over"* claim and corrects §10(b). **No design decision in §1–§13 is reversed by this revision** — §14 pins the
> reference frame of one const (`MaxDrawDistance`, which stays `40f`) and surfaces one new Sponsor question (the
> snake's size relative to the pill); §15 reframes what the element can claim on its own. The §2 balance
> arithmetic, the §3 state machine, the §4 arbitration and the §5 amplitude budget all stand unchanged.
>
> **🔁 Second pass, 2026-08-01 — three self-corrections from PR #406's REQUEST_CHANGES review (Devon).** The
> review could not break §15.4's conclusion and the one-channel verdict stands; all three blockers were in the
> *consequences* this revision drew from it, and all three are fixed here rather than argued with:
> **(M1)** the `MaxDrawDistance` `40f → 12f` correction is **WITHDRAWN** — it mixed camera-frame and player-frame
> units, and in the camera frame (the only one the predicate can read) `12f` deletes the row through ordinary
> melee. `40f` stands with a stated frame and a real derivation (§14.3, §4.2, §8, §9).
> **(M2)** §11 Q8's option **(C)** is **WITHDRAWN as arithmetically eliminated** by this spec's own readability
> floor and replaced by a clamped, fully-costed **(C′)** — a Sponsor may not be handed an option the numbers have
> already killed (§11).
> **(M3)** §15.3's C2 injection was **tautological** — nulling the existence gate collapses every cue in the game,
> including the bar's own passing pairing — and is replaced by a three-part test whose middle step (per-resource
> null over an enumerated resource set) can return either answer (§15.3, §9).
> Non-blocking nits N1–N4 and N6 are fixed inline and marked at each site.

> **⛔ This doc does NOT un-defer the ticket.** `86caxhfg2` is sequenced behind **`86caxjwb3`** (enemy body-level
> hit feedback — `_HitFlash` + flinch + the pooled dust puff) by Sponsor decision 2026-07-27, and the
> *exists-at-all* question is settled at **that** ticket's soak (its AC6(c)), not here. This spec answers the
> **execution** questions so that IF the body soak returns *"still want it"*, the ticket is dispatchable the same
> day with no second spec round. If the body soak returns *"already answered"*, this doc closes with the ticket
> and §2's arithmetic still stands as a balance finding worth keeping.

**Supersedes for implementation detail:** [`hp-hud-polish-spec.md`](hp-hud-polish-spec.md) **§6** (the merged
parent — PR #339, `e13a51e`). §6 established WHAT and WHY; this doc establishes HOW, and **corrects two premises
in it** (§1.3 and §1.4 below). Where the two disagree, this doc wins and §6 should be read with the pointer here.

**⚠ Sibling spec that AMENDS this one — read it before implementing §3.2:**
[`enemy-hit-feedback-spec.md`](enemy-hit-feedback-spec.md), the body-level read owned by **`86caxjwb3`**. Its
**§1.3 amends this spec's §3.2**: *the lost-pip extinguish flash fires ONLY when no body flash fired within
`enemy_hit_flash_seconds`* — which in practice makes it a **bleed/DoT-only accent**, because a strike always brings
a body flash. That amendment is marked inline at **every** place this doc states the extinguish (**§3.2, §5.2,
§5.3, §7**) and is carried in **§13**'s draft; the suppression **input** it requires is specified in **§7**'s row
record, **§8**'s const accounting and **§9**'s test list. Precedence is **one-directional**: the body is never
suppressed because the pip carries something. This is not a new arbitration — it is §5.3's own *"lower the row,
never raise the body"* applied **by construction instead of by tuning**. Where the two specs disagree on the
extinguish, **the sibling wins**; everywhere else this doc is the implementation source.

**Builds on (do NOT duplicate):** [`enemy-hit-feedback-spec.md`](enemy-hit-feedback-spec.md) (the BODY read —
`_HitFlash` + flinch + dust puff, owned by `86caxjwb3`; cited not absorbed, and the source of the §3.2 amendment
above) · [`combat-cluster-design-brief.md`](combat-cluster-design-brief.md) §1.2 / §2.5
(the BODY read — owned by `86caxjwb3`, cited not absorbed) + §4 (primitive discipline) ·
[`status-effect-readability-spec.md`](status-effect-readability-spec.md) §3.2 (the head-anchor precedence rule
this spec extends to enemy heads) · [`style-guide-v2.md`](style-guide-v2.md) §6 (the bone anchor `#CFC6AD`,
sub-1.0 palette, the HUD-plate-over-saturated-green watch item) · [`hud-three-bar-spec.md`](hud-three-bar-spec.md)
(the segment/plate grammar) · `.claude/docs/game-juice.md` §0/§1/§2 (every amplitude cap here) ·
`team/quality-bars.md` **#2 / #7 / #9 / #10** — specifically § ***"Bar 10 — the standard in full, and the four
checks (`86caz5na6` + `86cazhjw4`, 2026-07-31)"*** and its sub-§ ***"The default gameplay framing — the one framing
a magnitude claim may be stated against"***, which supplies **every** px-per-metre figure in §14 (**cited, never
re-derived** — see §14.1).

**Board (looked at the images, not the prose):** `inspiration/2026-06-12_21h16_13.png` and
`inspiration/2026-06-12_21h13_31.png`. Both confirm the value story this spec rests on — the world is **high-key**:
bright saturated grass (near-white in the sun), mid-value canopies, bright blue sky. **Nothing in either frame
approaches near-black** *(⚠ this sentence previously read "there is almost **no dark value anywhere in frame**" —
corrected 2026-08-01 on a re-look: the frames carry a LOT of mid-dark tree/mountain shadow, and shadow is the
plate's real competitor. See **§14.5**, which keeps the conclusion and fixes the reasoning.)* That is why the dark
plate is the load-bearing element: it is the rarest value in the
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

**The load-bearing call of this spec:** the row reads on **FORM + VALUE**, never on hue or motion — five discrete
blocks (form) that are pale-on-dark (value). The desaturation check passes by construction, because **there is no
hue in the element at all**.

> **🔴 CORRECTED 2026-08-01 (`86caxhfg2` revision) — this paragraph previously read "FORM + POSITION + VALUE …
> quality-bar #10 is satisfied three times over," and that claim is WITHDRAWN.** It was written against bar #10 as
> it stood at PR #339 / `e13a51e`; the bar has since been amended twice (**PR #380 `90d024b`** added the variance
> clause, **PR #386 `0f14b4f`** added checks C1–C4, **PR #395 `aeeafa0`** dieted the row into
> `team/quality-bars.md` § *"Bar 10 — the standard in full, and the four checks (`86caz5na6` + `86cazhjw4`,
> 2026-07-31)"*). Re-audited against the amended standard in **§15**, the count is **not three**:
> **POSITION does not vary with the read this element delivers** (a nearly-dead enemy's row sits in exactly the
> same place as a barely-scratched one's — Δ = **0.00 px**), so it is struck by the bar's own variance clause; and
> **FORM and VALUE share one failure domain**, so C2 collapses them to **one counted channel**. The honest verdict
> is in **§15.4**: *the pip-row alone does not meet ≥2 — the enemy-damage cue meets it only as pip-row + body
> read.* That is a structural ratification of the Sponsor's sequencing decision, not an argument against the
> element. **Read §15 before quoting any channel count from this doc.**

---

## 1. Ground truth (read at `bf33b65`, quoted not inferred)

Every value below was read during this spec's authoring and **re-verified at
`bf33b655e4953478549f4f74c5a692c39ee3c8f9`** on 2026-08-01. Four of them change what the ticket assumes.

> **The heading previously read *"read from `origin/main`"* — corrected 2026-08-01.** `origin/main` is a **moving
> ref**: a claim written against it is true only until the next merge and cannot be re-checked afterwards, because
> the reader has no way to know which tree the author saw. Every `file.cs:NN` in §1.1/§1.2 still resolves at the
> pinned SHA (spot-checked: `LootPrompt` `:62` `:65` `:69` `:70` `:72` `:174` `:191-192`; `SurvivalHud` `:44` `:83`
> `:84` `:343` `:361`), but **line numbers drift** — bar #10's own history records four citations shifting when
> PR #379 merged. For anything added after this date, **cite the symbol**.

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
| Weapon damage set | 15 defs, **6 → 21** base damage — min **6** (`PickaxeWoodDamage:89`, `SpearWoodDamage:92`, `DaggerWoodDamage:95`), max **21** (`SwordIronDamage:127`). **No def deals 4.** | `WeaponCatalog.cs:62-129` |
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
in `Assets/Scripts`. That is **`86caxjwb3`'s** scope, specified in
[`enemy-hit-feedback-spec.md`](enemy-hit-feedback-spec.md). **Cited, not absorbed** — nothing in this doc specifies
a flash, a flinch, or a puff, and §5's amplitude budget is written *relative* to a body read that will exist by the
time this ships.

**One exception to "not absorbed," and it runs the other way:** that spec's **§1.3 amends this spec's §3.2**
(extinguish suppression) and its **§10** owns the `enemy_hit_flash_seconds` this spec reads. So the dependency is
**not purely citational** — see the ⚠ sibling-spec block in the header. Do not read "cited, not absorbed" as
licence to implement §3.2's extinguish unconditionally.

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
| `dagger_wood` / `pickaxe_wood` | 6 | Slash | 4.5 | **9** | 0.56 |
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
fixed 5-pip row therefore **cannot** mean "hits remaining" — it would be wrong for **13 of the 15 defs** and would
actively lie (5 pips, 9 hits) exactly where the player is weakest and most needs honesty.

*(Exactly **two** defs land on 5 hits vs a medium boar — `sword_wood` and `pickaxe_iron`, both 12 Slash → 9.0
effective → `ceil(40 / 9.0) = 5`. Every other def misses. The denominator is **defs**, not weapon×enemy pairings —
that product would be 30, and the 13 above is a count of defs at one enemy/tier.)*

> *(Which of the 15 defs are reachable in combat today is a wiring question — `WeaponCatalog.cs:33-58` records
> wood- and new-tier combat wiring as follow-up. The spec must survive all of them because the def set IS the
> balance surface the pip-row reads, and a def that becomes reachable later must not need a UX revision.)*

### 2.2 So what the row means: **a 5-block quantized PROPORTION**

The pips are a **chunky bar**, not a counter. `filled = FloorToInt(Current01 × 5)`, clamped 0..5 — **pure FLOOR,
which never over-reports.** The read the player learns is *"about a fifth gone / about half gone / nearly out"*,
which is exactly the question AC1 names and is weapon-agnostic by construction.

> **⚠ FLOOR ONLY — do NOT reuse `SurvivalHud.FilledSegments`.** The shipped player-HUD helper is *not* a pure
> floor: after `FloorToInt(c * SegmentCount)` (`SurvivalHud.cs:361`) it **promotes `N-1 → N` when
> `c >= TopSegmentThreshold = 0.95f`** (`:343`, `:365`) — a deliberate top-segment policy so a continuously-decaying
> full need shows and holds 10/10 instead of capping at 9 (ticket `86cafc6ty`). That promotion **over-reports by
> design**, and inheriting it here would fail this spec's own §9 assert (`Current01 = 0.999` must read **4 filled +
> a draining pip**, never 5) — an enemy at 99.9 % must never read as untouched. The enemy row wants the *lower*-
> segment behaviour only. Write the three-line floor locally; cite `:361` as the **shape** of the rule, not as a
> call site to reuse. (The player HUD is right to promote — a need you just filled should read full; an enemy you
> just scratched must not read full. Same arithmetic, opposite honesty requirement.)

**This is a re-labelling, not a redesign** — the ticket's geometry, colour and count all stand. But it must be
written down, because "pips = hits" is the intuition a dev or a reviewer will bring, and it will produce wrong
follow-up decisions (e.g. "make it 4 pips so the axe divides evenly" — which would break for every other weapon).

### 2.3 The one place pips genuinely break — and the fix

A pip is 20 % of max HP. **Any hit landing under 20 % moves nothing.** From the table, on a medium boar:

| Weapon | Pips/hit | Share of hits that change NOTHING on the row |
|---|---|---|
| `dagger_wood`, `pickaxe_wood` | 0.56 | **~44 %** |
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
  > **⚠ AMENDED by [`enemy-hit-feedback-spec.md`](enemy-hit-feedback-spec.md) §1.3 — fires ONLY when no body flash
  > fired within `enemy_hit_flash_seconds`; in practice a bleed/DoT-only accent.** A strike always brings a body
  > flash, so the extinguish is **suppressed on strikes**; a bleed tick that empties a pip inside an already-live
  > row brings no body flash, and there the extinguish is the only thing marking the change — so it keeps a real
  > job. **Do not implement this bullet unconditionally.** The suppression input is in §7's row record; the const
  > is in §8; the assert is in §9. Precedence is one-directional — never suppress the body for the pip.

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
- Cheap robustness guard: hide beyond **`MaxDrawDistance = 40f`, and that number is a CAMERA-frame distance** —
  the guard exists so a row can never outlive its fight and become an orphan plate at the back of the frame.
  A `const`, not a dial (§8).
  > **⚠ The reference frame is load-bearing and this bullet previously did not state it.** The pure predicate
  > below takes `sp` and nothing else, and `sp.z` is *"distance in front of the camera"*
  > (`LootPrompt.cs:174-176`, quoted verbatim in §1.1) — there is no player transform in the signature, so the
  > only distance this guard can read is **camera-to-target**, never player-to-target. Any future re-tune must
  > state the frame in the same sentence as the number.
  > **🔴 A 2026-08-01 draft of this section corrected `40f → 12f`. That correction is WITHDRAWN before it reached
  > code** (Devon, PR #406 review M1): in the camera frame a struck enemy sits at **11.81–16.49 m** at the default
  > framing, so `12f` would have deleted the row through most of ordinary melee — the exact case the element
  > exists for. **`40f` stands, now with the derivation it never had.** Full arithmetic, the band table and the
  > two crossover rules: **§14.3**. Sponsor FYI: **§11 Q9**.

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
| Lost-pip extinguish **— ⚠ CONDITIONAL, see below** | `#EAD9B8` at **α ≤ 0.85**, hold 0.06 s, ease to `Charcoal` 0.18 s | total **≤ 0.24 s** |
| Death empty | 5 × `Charcoal` over 0.25 s, then the standard fade | whole death read **≤ 0.7 s** |

**Every animation on this element is alpha or colour-value. Nothing moves, nothing scales, nothing shakes.** That
is the amplitude discipline in one sentence, and it is trivially auditable at review.

> **⚠ AMENDED by [`enemy-hit-feedback-spec.md`](enemy-hit-feedback-spec.md) §1.3** — the lost-pip extinguish row
> above is **conditional**: it fires only when no body flash fired within `enemy_hit_flash_seconds`, making it a
> bleed/DoT-only accent. On a normal strike the ALLOWED set for this element reduces to the fade and the
> draining-pip ease. Every other row in this table is unconditional.

### 5.3 The loudness ordering (the soak's real test)

On any single impact frame, in descending prominence: **body `_HitFlash` > flinch > dust puff > pip-row change >
everything else.** If the soak capture shows the pip-row as the most eye-catching change in the frame, the
element is miscalibrated — and the correct fix is to **lower the pip-row** (drop the extinguish flash on the DoT
path to α ≤ 0.6, or soften the draining-pip ease), **never to raise the body**.

> **⚠ AMENDED by [`enemy-hit-feedback-spec.md`](enemy-hit-feedback-spec.md) §1.3 — on the strike path this rule is
> now satisfied by CONSTRUCTION, not by tuning.** Because the extinguish flash is suppressed whenever a body flash
> fired within `enemy_hit_flash_seconds`, the loudest lever this section contemplated is **already gone on impact
> frames**; what remains on a strike is the draining-pip alpha ease. **Grade the soak accordingly:** an extinguish
> flash visible on a *strike* frame is a **BUG** (the amendment did not land) — not a miscalibration to tune down.
> §1.3 also refines the ordering across the whole event: on the impact frame the ordering above stands, but the
> **flinch is the longest-lived** beat and is what makes the hit read as weight. **Grade the capture on the impact
> frame; grade the feel on the event.**

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
| Extinguish flash **— ⚠ CONDITIONAL** | `Cream` `#EAD9B8` (0.92, 0.85, 0.72) at α ≤ 0.85 — `SurvivalHud.cs:84`. **AMENDED by [`enemy-hit-feedback-spec.md`](enemy-hit-feedback-spec.md) §1.3: fires only when no body flash fired within `enemy_hit_flash_seconds` — a bleed/DoT-only accent.** |

**Fixed pixel size at every distance** — no distance scaling. A distance-scaled row is unreadable far away and
oversized in close melee; a fixed 64 × 10 chip is a stable, learnable object.

> **⚠ This rule has a measured cost, quantified 2026-08-01 in §14.3: the pill does not shrink with its subject, so
> over a SNAKE it is 1.22× the animal's on-screen height at the default framing.** The rule is NOT changed here.
> **`MaxDrawDistance` (§4.2) is NOT the mitigation** — the snake fails at 14 u, in melee, at any cap value, and a
> 2026-08-01 draft that treated the cap as the fix produced a `12f` that would have deleted the row in melee
> (§14.3, withdrawn). The mitigation on the table is option **(B)** or **(C′)** in **§11 Q8**, and **(C′)** would
> overturn this rule outright — the Sponsor's call to make, not this doc's. If he takes (C′), this paragraph is
> what gets rewritten.

**Primitive discipline (cite when implementing):** pure IMGUI `GUI.DrawTexture(rect, Texture2D.whiteTexture)`
with `GUI.color`, explicit `Rect`s only, `useGUILayout = false` — the `BootHud`/`SurvivalHud`/`LootPrompt` idiom.
**No** world-space Canvas, **no** UI-Toolkit panel, **no** billboard mesh, **no** second projection helper, **no**
post-process Volume, **no** `MaterialPropertyBlock` (`unity6-mastery.md` §2 — GPU Resident Drawer disqualifier),
and **no** `LowPolyVertexColor` material touch. Every channel sub-1.0. IMGUI also never strips to magenta in the
IL2CPP release, which is why the whole HUD is IMGUI.

**Architecture shape that satisfies AC2 (Devon reviews; not over-prescribed):** ONE scene-level component in the
HUD family owning a **fixed-capacity** array of `MaxRows` row records `{ Health, Transform, anchorY, armTime,
last01, drainA, extinguishAt, lastBodyFlashTime }`. Armed from the `MeleeAttack` strike seam; subscribes `Health.Changed` + `Died`
per armed target and **unsubscribes on expiry / death / destroy** (AC5's no-leaked-handlers test). Fixed capacity
= no per-frame allocation. `OnGUI` draws ≤ 3 rows from cached state and re-projects the anchors — **no
`FindObjectsOfType`, no `Current01` polling, no allocation in `OnGUI`** (`unity6-mastery.md` §5/§6).

**The suppression INPUT the §3.2 amendment requires (the constraint; the mechanism is Drew's to pick).** The row
must be able to answer one question before it lights an extinguish flash: **"did a body flash fire on THIS target
within `enemy_hit_flash_seconds`?"** As originally written the row record had no input that could answer it, so the
amendment was un-implementable from this doc even by a dev who knew about it. Hence `lastBodyFlashTime` in the
record above — **a per-row timestamp, not a new registry id**:

- **What is pinned:** the input **exists**, it is **per-target** (a global "someone flashed recently" would suppress
  the flash on enemy B because enemy A was hit), and the predicate is `Time.time - lastBodyFlashTime >=
  enemy_hit_flash_seconds` → extinguish permitted, else suppressed. **Suppression is one-directional** — this
  predicate may only ever *withhold* a pip beat; nothing here may gate, delay or dim the body.
- **What is NOT pinned (Devon reviews the choice):** how the value gets there. The natural seam is the same
  `MeleeAttack` strike callback that already arms the row — the strike that triggers the body flash is the strike
  that arms, so stamping `lastBodyFlashTime = Time.time` at arm-time costs nothing and needs no cross-component
  reference. A direct query into the body-feedback component is also acceptable but couples two HUD-adjacent
  systems; prefer the stamp.
- **Why this does not break §8's two-id budget:** `enemy_hit_flash_seconds` is **owned and registered by
  [`enemy-hit-feedback-spec.md`](enemy-hit-feedback-spec.md) §10** (default `0.08`) — this spec **reads** it and
  mints nothing. `lastBodyFlashTime` is a private field, not a tunable. **This spec still adds exactly two ids.**
- **If `86caxjwb3` ships without that id** (soak revert, rename, or the body read closes this ticket), fall back to
  the code const `BodyFlashSuppressSeconds = 0.08f` and record the divergence — do **not** mint a registry id, and
  do **not** silently restore the unconditional flash.

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

**Read but NOT owned:** `enemy_hit_flash_seconds` (default **0.08**) — registered by
[`enemy-hit-feedback-spec.md`](enemy-hit-feedback-spec.md) **§10**, consumed here **read-only** as the §3.2
extinguish-suppression window (§7). This spec **mints nothing for it**, so the two-id budget above is unchanged. If
that id does not ship, use the code const `BodyFlashSuppressSeconds = 0.08f` (§7) — never a third registry id.

**Do NOT mint further ids.** These are **code consts**, deliberately: `MaxRows = 3`, `DrainMinAlpha = 0.35f`,
`HeadClearance = 0.25f`, `StatusBandH = 26f`, **`MaxDrawDistance = 40f`** (⚠ a **CAMERA-frame** distance — a
2026-08-01 draft proposed `12f` and it is **withdrawn**, §14.3; comment the const with the frame **and** the
armed-row derivation), `FadeInSeconds = 0.12f`,
`FadeOutSeconds = 0.4f`, and the §7 fallback `BodyFlashSuppressSeconds = 0.08f` (used only if
`enemy_hit_flash_seconds` is absent). Rationale: the ticket's two-id budget is part of its M-sizing, and every one of these is
a structural constant the Sponsor has no reason to dial — the two things he *will* want to dial (how long it
lingers, whether it exists) are exactly the two ids above.

---

## 9. Success-tests this spec ADDS to AC5 (the ticket's list stands; these sharpen it)

- **Proportion, not hits.** `filled = FloorToInt(Current01 × 5)` over a swept `Current01` — assert monotonic and
  never over-reporting; assert `Current01 = 0.999` → 4 filled + a draining pip, not 5. **Assert this against the
  row's OWN floor helper, and additionally assert it is NOT `SurvivalHud.FilledSegments`** — that helper promotes
  `N-1 → N` at 0.95 (`SurvivalHud.cs:365`) and would return 5 here, failing this assert (§2.2).
- **Extinguish suppression (the §3.2 amendment).** With a body flash stamped at `t`, a pip emptied at
  `t + 0.5 × enemy_hit_flash_seconds` must produce **NO** extinguish flash; the same pip emptied at
  `t + 2 × enemy_hit_flash_seconds` (the bleed/DoT path) **must** produce one. Assert **per-target**: a body flash
  on enemy A must not suppress the extinguish on enemy B. Assert the predicate is one-directional — no test may
  show the body flash withheld, shortened or dimmed because a pip changed.
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

**Added by the 2026-08-01 revision (§14 + §15):**

- **C2 injection — the one-channel verdict must be DEMONSTRATED by a test that could return the OTHER answer
  (§15.3).** Three asserts, in this order; the middle one carries the verdict.
  - **(i) Leaf-distinctness.** Pin `drainA` to a constant ⇒ `filled` still tracks HP. Pin `filled` ⇒ `drainA` still
    moves. Both must PASS — this is what makes C2's nearest-common-dependency tie-breaker operative rather than a
    formality (at leaf granularity the count is genuinely 2).
  - **(ii) Per-resource null — the evidence.** Enumerate the resources either channel reads on the draw path and
    null each in turn, asserting which channels survive. For this element the enumeration has **exactly one
    member — the row record** (no material, shader property, prefab ref, `Transform` or `ParticleSystem` is on the
    pip-row's `OnGUI` path). Assert both channels stop, **and assert the enumeration itself** — a test that fails
    loudly if a future refactor introduces a second resource, because that would change the count to 2 and this
    audit's verdict with it. The check returns one verdict per resource, so it is **not** single-output.
  - **(iii) Control — OWED, NOT DELIVERED.** The same procedure on the composed cue (§15.4) must return **≥2**
    (null the flash material / the rig `Transform[]` / the pooled `ParticleSystem`; each kills exactly one). Those
    channels are `86caxjwb3`'s and unimplemented at `bf33b65`, so this is a **stated prediction**, not coverage.
    Whichever ticket lands second owns it.
  > **⚠ The `enemy_hp_pips_enabled = false` assert is a SMOKE TEST, not C2 evidence** — it nulls the element's
  > existence gate, so it returns "collapse" for every cue in the game including bar #10's own passing pairing.
  > A 2026-08-01 draft of this spec offered it as the evidence for `count = 1`; that is withdrawn (Devon, PR #406
  > review M3). Keep the assert, label it, and never cite it in a channel count.
- **CH1's degenerate case and CH2's coverage of it (§15.2), as a pure-function assert.** At `Current01` 0.399 and
  0.201 the lit-pip count is **identical** — `FloorToInt(0.399 × 5) = FloorToInt(1.995) = 1` and
  `FloorToInt(0.201 × 5) = FloorToInt(1.005) = 1`, so FORM Δ = 0 — while `rem` runs **0.995 → 0.005**, so `drainA`
  differs by **0.65 × 0.99 = 0.6435**. *(Note the ×5: `rem = f × 5 − whole`, so a 0.198 swing in `Current01` is a
  **0.99** swing in `rem`, not 0.198. The pip bucket is 20 % of max HP wide, which is the whole reason CH2 has room
  to work in.)* Assert both halves in one test: `filled` equal, `drainA` strictly different.
  *(This is the test that pins WHY the draining pip is not optional — delete it and §2.3's whole argument becomes
  unenforced.)*
- **Pill/pip geometry is arithmetic, so pin it as arithmetic.** `5 × PipW + 4 × Gap == 58` and
  `(PillW − 58) / 2 == 3` — a pure assert that fails loudly if anyone later re-tunes `PillW` without re-deriving
  the padding (§15.2 / §7).
- **`MaxDrawDistance` (§14.3) — assert the FRAME, and assert BOTH sides.** Assert the const is **`40f`** and that
  its comment names the reference frame (**camera-frame `sp.z`** — `TryResolveRowRect`'s only spatial input is
  `sp`, whose `z` is *"distance in front of the camera"*) plus the armed-row derivation (worst `sp.z` at arm
  **28.49 m** + bounded recession **4.32 m** ⇒ **32.8 m**), so a future tuner sees the frame and the WHY before
  moving it — the bar's *"express the floor as an expression over the framing table, not a bare literal"*
  discipline applied to a const.
  - **It suppresses far:** a target at `sp.z` beyond the const resolves to **no row**.
  - **It NEVER suppresses a row the player armed:** sweep `OrbitCamera.distance` across its reachable band
    (`minDistance` 6 → `maxDistance` 26) and at each sample place a struck enemy at the worst-case melee position
    (weapon reach directly away from the camera, snake anchor 0.48 m); `TryResolveRowRect` must return **true**
    every time. **This is the assert that reds a `12f` — and a `12f` is exactly what the one-sided version of this
    test would have shipped** (§14.3; Devon, PR #406 review M1).

**Shipped-build capture (windowed)** — ticket AC5's list, plus **two**: **(f1)** and **(f2)** below.

> **SPLIT into two frames, deliberately (Devon, PR #406 review N6).** A single frame was carrying five separate
> obligations plus two framing constraints, and two of them **conflict**: "two non-overlapping rows at visibly
> different HP levels" and "a snake row in tree shadow" are not reliably co-satisfiable in one composition, so one
> frame could silently satisfy four and miss the fifth with nothing reporting it. Two frames, each with its own
> pass condition:
>
> - **(f1) — the PAIR frame.** Two damaged enemies on screen at once showing two non-overlapping rows, each
>   visually attributable to its own body, **at visibly different HP levels** (a pair at the same HP evidences
>   attribution but not the read). This one frame is doing triple duty and the Self-Test Report must say so
>   (§15.1 / §15.5): it is the **AC5 multi-row evidence**, bar #10's **C3 step-1 comparison pair**, and **C4's
>   `cue_pair.png`**.
> - **(f2) — the HARD-CASE frame(s).** At least one **SNAKE** row (§14.3's finding is snake-specific and a
>   boar-only capture cannot show it) **and** at least one row over an animal standing in **TREE SHADOW** (§14.5 —
>   the plate's competitor is shadow, not sunlit grass; a sunlit-only capture evidences the easy case only). These
>   may be one frame if a snake happens to stand in shadow, but **two frames is the default**; do not compress
>   them to chase a count.
>
> **Both are at the stated default gameplay framing** — 55° / 14 u / FOV 45 / 1280 × 720 — with every judged body
> inside the frustum. C4's production half is mechanically gateable even though its verdict is human; a capture at
> a favourable framing is the `-verifyPond`-green-on-a-mound failure applied to the instrument.

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
word), **#10** — **see the corrected claim below**. Bars **NOT tested**: **#1**, **#3**, **#4**, **#5**, **#6**,
**#8** — no world, mesh, material, weapon-sizing or nudge-tool surface is touched.

> **🔴 CORRECTED 2026-08-01 — the bar-#10 line above previously read "≥2 hue-independent channels — form + position
> + value; desaturate the capture and the read must survive intact," and that is now bounded three ways by §15.**
> **(i)** POSITION is **struck** for the HP read (Δ = 0.00 px — it does not vary with HP). **(ii)** FORM and VALUE
> share one failure domain, so C2 counts them as **one** — **the pip-row alone does NOT meet ≥2**; the enemy-damage
> cue meets it only as **pip-row + body read** (§15.4). **(iii)** Of the bar's four checks, this soak exercises
> **C1** (magnitudes stated, §15.2), **C2** (by the per-resource-null assert in §9) and **C3** (step-1 pair,
> capture **(f1)**);
> **C4 is unbuilt project-wide** and its human half (*"point at the animal that's closest to going down"*) is owed,
> not delivered. **Desaturate** stays required and passes by construction (no hue in the element).
> **⚠ On C2's evidence specifically (corrected again after PR #406 review M3):** what converges C2 is §9's
> **per-resource null** assert (the resource enumeration has exactly one member), **not** the
> `enemy_hp_pips_enabled` gate-null, which returns "collapse" for every cue in the game and is a smoke test only.
> The **control** half — the same procedure returning ≥2 on the composed cue — is **owed, not delivered**, because
> `86caxjwb3`'s three channels are unimplemented at `bf33b65`. So C2 converges the pip-row's own count and nothing
> about the composed cue's.
> **The bounded claim is therefore: this soak converges #2, #7, #9, and #10's C1/C2/C3 + desaturate for the
> composed pip-row-plus-body cue — and converges NOTHING for the pip-row in isolation, which is a known
> one-channel element by design.** Do not quote a #10 PASS from this soak without that qualifier.

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

**Added by the 2026-08-01 revision:**

- **🔴 Q8 — THE SNAKE. The pill is TALLER than the animal, at the framing you fight it (§14.3).** Measured: the
  snake's whole on-screen body is **8.19 px** tall at the default framing; the pill is **10 px** tall and **64 px**
  wide — **1.22× the snake's height and 7.81× it in width** (though only **0.53×** its 120.43 px length — the
  inversion is real but one-dimensional, §14.3). The boar is fine (0.31× / 2.00×). **This is a look call and I am
  deliberately not making it.** **Three live options — (A), (B), (C′) — plus one withdrawn.** Each live option
  carries **its own worst-case pip figure**, so all three are comparably quantified and none can be picked without
  seeing what it costs (the gap PR #406's review M2 caught: (B) carried its cost, the old (C) did not). Trade-offs
  stated, **no recommendation**:
  - **(A) Ship it as spec'd, one size for every enemy.** *For:* one learnable object, zero new code, AC2's
    "no per-enemy branch" untouched, and the row is transient so the oversize moment is ≤2.5 s. *Against:* over a
    snake the UI is visibly the larger object — the exact inversion of §0's *"smaller than the body it sits above"*
    gate, on the enemy the player meets first.
  - **(B) Shrink the pill for everyone** — e.g. `48 × 8` with `7 × 5` pips (run `5×7 + 4×2 = 43`, 2.5 px padding).
    *For:* fixes the snake by making the element quieter everywhere, which is tonally the direction §0 already
    points. *Against:* the pip drops 10 → 7 px wide and 6 → 5 px tall; §15.2's smallest FORM delta falls from
    **10 px to 7 px**, still above every candidate floor but with less margin, and §14.4's "a 4 px pip is not a
    block" argument gets closer to biting.
  - **(C) — WITHDRAWN as posed. Replaced by (C′).** (C) originally read *"scale the pill to the body's on-screen
    size (one multiplier from the cached §4.1 bounds)"* and it is **arithmetically eliminated by this spec's own
    readability floor** (Devon, PR #406 review M2). §4.1's cached quantity is a **height**
    (`bounds.max.y − root.position.y`). Normalising so the boar keeps today's `64 × 10`, the snake's multiplier is
    `8.19 / 32.05 = 0.2556` ⇒ a **16.4 × 2.6 px** pill with **2.6 × 1.5 px** pips — the pip lands **2.6× BELOW**
    the 4.0 px chip §1.4 already rejected as unreadable, on the exact enemy the question exists for. The other
    plausible bound flips the sign: length-scaled at the boar's `64 / 93.74 = 0.683` ratio gives
    `0.683 × 120.43 = 82.2 px` — a pill **larger** than today's, over a snake. So "scale to the body's on-screen
    size" was **under-determined**, and the one determination the option actually named was already dead.
    **A Sponsor must never be handed an option arithmetic has eliminated**, so it is withdrawn rather than left on
    the list to be picked.
  - **(C′) Scale the pill to the body's on-screen HEIGHT, CLAMPED, with a stated floor.**
    `m = clamp(bodyHeightPx / 32.05, 0.75, 1.0)` — one multiplier computed once per arm from the §4.1 bounds, no
    per-enemy branch, so AC2 survives. **The bound is HEIGHT, stated**, because that is what §4.1 caches and it is
    the dimension the snake fails on. Worst case is the floor, and it is carried here so this option is costed the
    way (B) is: `m = 0.75` ⇒ pill **48 × 7.5 px**, pips **7.5 × 4.5 px**, run `5 × 7.5 + 4 × 1.5 = 43.5`, padding
    2.25 px each side. *For:* over a snake the pill is **7.5 px against the body's 8.19 px = 0.92×** — back under
    §0's *"smaller than the body it sits above"* gate, which is the entire point of the question; the boar is
    untouched at `m = 1.0`. *Against, three costs:* **(a)** it still breaks §7's **"fixed pixel size at every
    distance"** — a snake's row is visibly smaller than a boar's *and* the same snake's row grows as you close on
    it, so the element stops being one stable learnable object; **(b)** the floor means subordination is **not**
    "by construction at every distance" as (C) claimed — past the crossover the row stops shrinking and (C′)
    degrades to (A)'s behaviour, so `MaxDrawDistance` is still needed and is not retired; **(c)** at the floor the
    smallest FORM delta falls **10 px → 7.5 px** (§15.2) and the pip *height* reaches **4.5 px**, within 0.5 px of
    the figure §1.4 rejected — the same cost (B) carries, arriving by a different route. **This still contradicts a
    rule this spec already made, so it needs his explicit call, not mine.**
- **Q9 — `MaxDrawDistance`: the question is WITHDRAWN, not answered (§14.3).** A 2026-08-01 draft asked *"is there
  any moment he wants an HP row on an animal 12–40 m away?"* **That question was not well-posed**, because it did
  not say which reference frame "12 m" was in (Devon, PR #406 review M1/Q9). In the camera frame — the only frame
  the predicate can read — a struck enemy sits at **11.81–16.49 m** at the default framing, so a "no, 12 is fine"
  would have ratified a const that hides the row through most of ordinary melee. **The const stays `40f`**, now
  derived as an armed-row floor (worst `sp.z` at arm **28.49 m** + bounded recession **4.32 m** ⇒ **32.8 m**).
  **No decision is requested.** It is surfaced only so he knows a question he may have seen was withdrawn rather
  than answered — and so the one real forward case is on record: **if he ever wants the row to survive past ~33 m**
  (a wounded animal retreating under some future flee behaviour that neither `BoarAI` nor `SnakeAI` has today), the
  const moves **up**, not down. Either way §14.3's snake finding is untouched — the cap never fixed it, which is
  exactly the confusion that produced the withdrawn 12 m.
- **Q10 — the one-channel verdict (§15.4).** Not a request for a decision, an FYI he may want to weigh at
  `86caxjwb3`'s soak: **"body-read-only-forever" is a bar-#10-legal outcome; "pip-row-only" never was.** If the body
  read alone answers *"is it nearly down?"*, closing this ticket is now the better-supported outcome of the two,
  not merely an acceptable one.

---

## 12. Out of scope

The enemy **body** read — `_HitFlash`, flinch / hit-react, dust puff, death topple (**`86caxjwb3`**, specified in
[`enemy-hit-feedback-spec.md`](enemy-hit-feedback-spec.md) — cited throughout, absorbed nowhere; **the single
exception is that spec's §1.3 amendment to this spec's §3.2**, which this doc honours inline rather than restating).
Implementing any of this (spec-only PR). The player's own HP HUD (`86cah7z2q`).
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
  (`spear_iron` 2 → `dagger_wood` / `pickaxe_wood` 9), so a fixed 5-pip row would be wrong for **13 of the 15
  defs** — only `sword_wood` and `pickaxe_iron` (both 12 Slash → 9.0 effective) land on 5. Five is
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
  **~60 % of the player HUD's amplitude and half its duration** (`#EAD9B8` α ≤ 0.85, ≤ 0.24 s total), and that
  accent is **CONDITIONAL: it fires only when no body flash fired on that target within `enemy_hit_flash_seconds`**
  — in practice a **bleed/DoT-only** accent, since a strike always brings a body flash. The gate is the **loudness
  ordering**: body flash > flinch > dust > pip-row — and on the strike path that gate is now met **by construction
  rather than by tuning**, so an extinguish flash on a strike frame is a bug, not a miscalibration. Suppression is
  **one-directional**: the body is never suppressed, shortened or dimmed because the pip carries something.
  (`enemy-hp-read-spec.md` §5 + §7, **amended by** `enemy-hit-feedback-spec.md` §1.3 — this draft and that spec's
  §13 extinguish draft state the SAME rule; batch them as one entry, not two.)
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

### 13.1 Decision drafts ADDED by the 2026-08-01 revision (§14 + §15)

- **Decision draft:** **A distance const on a screen-space UI element must state its REFERENCE FRAME in the same
  sentence as its number.** The pip-row's `MaxDrawDistance` **stays `40f`** and is a **camera-frame** distance,
  because `TryResolveRowRect`'s only spatial input is `sp` and `sp.z` is *"distance in front of the camera"*
  (`LootPrompt.cs:174-176`). A 2026-08-01 draft of this spec proposed `40f → 12f` by mixing frames — deriving in
  camera units (`8.19 px × 14 / 10 px = 11.47 m`) and justifying in player units (*"melee reach is 3.6 m"*).
  **In the camera frame a struck enemy reads 11.81–16.49 m at the default framing**, so `12f` would have deleted
  the row through most of ordinary melee. The correction is **withdrawn before implementation**, and `40f` gains
  the derivation it never had: an **armed-row floor** — worst `sp.z` at arm **28.49 m** (snake, 26 u zoom, weapon
  reach directly away) + bounded recession **4.32 m** (no flee state exists in `BoarAI`/`SnakeAI`; the fastest
  recession is the boar's `chargeDistance = 3.8f` overshoot plus `wanderSpeed = 1.1f`) ⇒ **32.8 m**. The old
  *"a fleeing bleeder should not leave a pill dancing on the horizon"* rationale is **retired**: it is a
  subordination claim the cap cannot deliver at any value, and there is no flee state for it to describe.
  (`enemy-hp-read-spec.md` §14.3 / §4.2 / §8; Devon, PR #406 review M1.)
- **Decision draft (process, generalisable beyond this element):** **A guard const gets a two-sided test — assert
  it suppresses far AND assert it never suppresses the case the feature exists for.** The `12f` above was
  one-sided by construction: §9's original assert only checked that a target beyond the cap resolved to no row, so
  a value that killed the feature in melee would have shipped green. The replacement sweeps `OrbitCamera.distance`
  across its reachable band and asserts a struck enemy at worst-case melee position **always** resolves. This is
  bar #10's *"state what the check returns on an instance that should FAIL"* discipline applied to a plain const.
  (`enemy-hp-read-spec.md` §9; Devon, PR #406 review M1.)
- **Decision draft:** **The pip-row is a SINGLE counted channel under bar #10's C2**, not three and not two. FORM
  (lit-pip count) and VALUE (draining-pip alpha) are different axes and genuinely complementary — VALUE carries
  exactly the sub-pip hits where FORM's delta is **0.00 px** — but they are written by the same `OnGUI` draw loop
  over the same row record behind the same resolve predicate and the same `enemy_hp_pips_enabled` gate, so C2's
  nearest-common-dependency rule counts them as **one**. POSITION is struck separately, by the variance clause:
  it does not differ between a nearly-dead and a barely-scratched enemy. **The enemy-damage cue meets ≥2 only as
  pip-row + body read** (`86caxjwb3`), whose `_HitFlash` (material property), flinch (part transforms) and dust
  (particle system) are three further independent failure domains. **This makes the Sponsor's 2026-07-27 sequencing
  decision a bar-#10 requirement, not only a tonal preference:** shipping the pip-row first would have made the
  game's entire enemy-damage read a single-failure-domain cue, which bar #10 forbids.
  > **⚠ Carry this caveat with the decision, and do NOT lean on *"#351's shape exactly"*.** #351 is C2's only
  > *live* worked instance and it was **over-determined** — both its channels are MOTION, so the amended row
  > already counts them as one by perception, and the bar states verbatim *"C2 does not change #351's count."*
  > **This is C2's first OUTCOME-DETERMINING use.** There is no precedent to check the application against; the
  > verdict rests on its own injection evidence (§15.3, and note that the evidence itself was corrected once —
  > the original gate-null assert was tautological, PR #406 review M3). Anyone extending C2 should treat this
  > entry as the precedent-setter it is, not as a second data point behind #351.
  (`enemy-hp-read-spec.md` §15; supersedes the *"satisfied three times over"* claim in the merged §0 at `59a6e53`.)
- **Decision draft (cross-doc correction, for whoever owns `86caxjwb3`):**
  `enemy-hit-feedback-spec.md` §1.1's division-of-labour table lists the pip row's channels as
  *"form (block count) + position + value"*. **`position` should be struck from that cell** for the same reason it
  is struck here — it is invariant with respect to the HP read. **Not edited by this PR** (that doc belongs to
  `86caxjwb3`); filed so the correction lands with that ticket rather than being silently inconsistent.

---

## 14. Readability at the DEFAULT GAMEPLAY FRAMING (added 2026-08-01)

Everywhere above, this spec justifies a size with the phrase *"at orbit distance"* — **"a 4 px chip is not a
readable block at orbit distance"**, **"a pale bone pip … holds at orbit distance"**, **"a continuous fill at orbit
distance is a dash"**. Those are the right instincts and **none of them carried a number**. This section supplies
the numbers, and one of them contradicts §0's own gate.

### 14.1 The framing figure is CITED, not re-derived

The canonical framing and its scale live in **`team/quality-bars.md` § *"Bar 10 — the standard in full, and the
four checks (`86caz5na6` + `86cazhjw4`, 2026-07-31)"* → *"The default gameplay framing — the one framing a
magnitude claim may be stated against"*** (at `bf33b655e4953478549f4f74c5a692c39ee3c8f9`). Quoting it rather than
rebuilding it is deliberate: **the ruler is not this doc's to re-derive**, and re-deriving it is how a spec ends up
contradicting the bar it is being audited against.

| Quantity | Value | Symbol |
|---|---|---|
| Orbit pitch | **55°** | `OrbitCamera.defaultPitch` (LOCKED; band `minPitch` 8° → `maxPitch` 70°) |
| Orbit distance | **14 u** | `OrbitCamera.distance` (band `minDistance` **6 u** → `maxDistance` **26 u**) |
| Vertical FOV | **45°** | the gameplay `cam.fieldOfView` set in `MovementCameraScene` |
| Capture size | **1280 × 720** | `CaptureGate.captureWidth` / `.captureHeight` |
| **Frame-plane scale** | **62.0798 px per world metre** | `720 / (2 × 14 × tan 22.5°)` — the bar's arithmetic |
| **Foreshortened scale** (a world-VERTICAL extent, at pitch 55) | **35.6075 px per world metre** | `62.0798 × cos 55°` |

**Quote the PAIR, never one number** — the bar's own rule. A body's *height* is a world-vertical extent and reads
at **35.6075 px/m**; a body's *broadside length* is in the frame plane and reads at **62.0798 px/m** as an upper
bound. Both appear below, labelled.

> **One thing this section does NOT do: set C1's pixel floor.** The bar states plainly that the floor is unset and
> lists three inputs that must be reconciled first (`frames_differ.py`'s `DEFAULT_MIN_FRAC` is an
> area-fraction-of-subsampled-pixels, **≥ 26 samples of 51,360**, not a linear px floor; an area fraction *inverts*
> C1's own scale rule; and any floor above **1.7804 px** reds `game-juice.md` §1's prescribed ±0.05 u float-bob
> under at least one reading). **This spec does not pick a number.** It states its own magnitudes so a reviewer can
> recompute them against whatever floor is eventually set — and notes in §15.2 that every channel here clears
> **every** candidate floor discussed in the bar (1, 1.7804, 4 and 6.2080 px) by a wide margin, so the verdict does
> not turn on the choice.

### 14.2 The bodies, measured from source at `bf33b65`

**Not inferred — read from the build constants.** Boar (`MovementCameraScene`, the `BuildBoar` const block):
`BoarGroundClearance = 0.62f` (root pivot above ground) + `BoarBodyRadius = 0.28f` (body half-extent, body part
authored at local zero) ⇒ **top of back 0.90 m**. Nose at `BoarHeadLength/2` beyond the head part's local
`z = 0.72`, tail part at `z = −0.58` ⇒ **≈ 1.51 m** nose-to-tail — **a LOWER bound, labelled as one**: that figure
takes the tail part's *origin* as the rear extent, and `LowPolyMeshes.BoarTail(0.22f, 0.03f, …)` extends up to
0.22 m past it (Devon, PR #406 review N4). The direction of error is favourable — a longer boar makes every boar
ratio below *better* — so nothing downstream moves; it is labelled so no one later quotes 1.51 m as measured.
Snake: `SnakeNeckRadius = 0.115f` half-extent
⇒ **0.23 m** tall lying on the ground; `SnakeHeadLength = 0.26f + SnakeLinkSpacing 0.14f × SnakeBodyLinks 12`
⇒ **1.94 m** long *(that expression is the source's own log line, not this doc's arithmetic)*.

| Body | Height on screen (foreshortened) | Height (frame-plane, upper bound) | Broadside length (frame-plane) |
|---|---|---|---|
| **Boar** (0.90 m tall, 1.51 m long) | **32.05 px** | 55.87 px | **93.74 px** |
| **Snake** (0.23 m tall, 1.94 m long) | **8.19 px** | 14.28 px | **120.43 px** |

*Arithmetic, shown: boar height `0.90 × 35.6075 = 32.047`; boar length `1.51 × 62.0798 = 93.74`; snake height
`0.23 × 35.6075 = 8.190`; snake length `1.94 × 62.0798 = 120.43`.*

> **Sanity anchor, with the bar's own caveat attached.** `OrbitCamera.cs:158` comments that this framing renders
> the castaway at *"roughly 55x95 px in a 1280x720 frame."* The bar explicitly **withdraws** that comment as a
> validation of the px/m model (95 px implies 1.5303 m frame-plane and 2.6680 m foreshortened; the second is
> implausible, so the comment is evidence of how rough a source comment is, not a cross-check). It is quoted here
> for the same limited purpose the bar allows: **a 64 px pill is wider than the player character's own ~55 px
> on-screen width.** That is a rough comparison and is labelled as one — it is not load-bearing for anything below.

### 14.3 🔴 The finding: the pill is NOT subordinate to the body at the distance the player fights

§0's gate says the row must be *"dimmer, **smaller**, and slower to change than"* the body. Against the measured
bodies, "smaller" does not hold:

| | Pill W (64 px) ÷ body length | Pill H (10 px) ÷ body height | Pill W ÷ body height |
|---|---|---|---|
| **Boar** | 0.68× | 0.31× | **2.00×** |
| **Snake** | 0.53× | **1.22×** | **7.81×** |

**The snake is the hard case, and it fails at the framing the player actually plays at.** The 10 px pill is
**1.22× taller than the snake's entire on-screen body** (10 ÷ 8.19). This is not a draw-distance problem that a cap
can fix — it is true at 14 u, in melee, on the default camera. **A UI plate TALLER than the animal it labels —
1.22× its height, even though only 0.53× its length — cannot read as belonging to that animal**; it reads as the
animal belonging to it. *(The height-only wording matters and the qualifier is carried deliberately: the snake's
on-screen presence is dominated by its **120.43 px length**, so the inversion is real but one-dimensional. Stating
it as "bigger than the animal" full stop would be the same overstatement §14.5 corrects the board header for —
Devon, PR #406 review N3.)* Flagged to the Sponsor as **§11 Q8** with three options and **no recommendation made
here** — this is a look call.

**The boar is fine on height (0.31×) and fine on length (0.68×), but the pill is 2.00× the boar's on-screen
height.** That is acceptable for a horizontal readout above a low quadruped and is *not* flagged as a defect — it
is stated so nobody later "discovers" it and treats it as one.

**Where the pill stops being subordinate at all** — scale falls as `14/d`, so the crossover distances are:

| Body | Pill W = body on-screen length | Pill H = body on-screen height | **Binding** |
|---|---|---|---|
| **Boar** | 20.51 m | 44.87 m | **20.51 m** |
| **Snake** | 26.35 m | **11.47 m** | **11.47 m** |

*Arithmetic, shown (boar width rule): `93.74 px × 14 / 64 px = 20.51 m`. (Snake height rule):
`8.19 px × 14 / 10 px = 11.47 m`.*

**⇒ and here is where the first draft of this section went wrong. `MaxDrawDistance` is a CAMERA-frame distance,
and the `12f` this section proposed would have deleted the row in melee.** The mistake is recorded rather than
quietly swapped, because it is the instructive kind: **the derivation and the justification were written in two
different reference frames and neither was named** (Devon, PR #406 review M1).

**The frame, established from the predicate's only spatial input.** §4.2's guard lives inside
`TryResolveRowRect(Vector3 sp, …)`, and `sp` comes from `Camera.WorldToScreenPoint`, whose `z` is
*"distance in front of the camera"* (`LootPrompt.cs:174-176`, verbatim). No player transform enters that
signature. **So the guard can only ever compare CAMERA-to-target** — and the `14` in `8.19 px × 14 / 10 px =
11.47 m` is `OrbitCamera.distance`, i.e. also camera-to-target. The crossover arithmetic above is in the right
frame; the sentence that justified `12f` — *"the camera orbits the player at 14 u and the longest melee reach is
the spear's 3.6 m, so a struck enemy is never near the cap"* — is **player**-frame reasoning, and it is the half
that does not survive.

**What a struck enemy's `sp.z` actually is.** `OrbitCamera.Apply` places the camera at
`_followPos − rot · Vector3.forward × distance`, with `_followPos = target.position + targetOffset` and
**`targetOffset = (0, 1.0, 0)`** — so at pitch 55° / distance 14 the camera sits **11.468 m above and 8.030 m
behind the pivot**, i.e. **12.468 m above the ground the enemy stands on**. With
`f = (0, −sin 55°, cos 55°) = (0, −0.819152, 0.573576)`:

| Enemy position (player at origin, ground `y = 0`) | boar anchor 1.15 m | snake anchor 0.48 m |
|---|---|---|
| 3.6 m toward the camera | `sp.z` = **11.81 m** | **12.36 m** |
| **standing at the player's own position** | `sp.z` = **13.88 m** | **14.43 m** |
| 3.6 m away from the camera | `sp.z` = **15.94 m** | **16.49 m** |

*Worked, boar-at-player:* camera `(0, 12.468, −8.030)`; `v = (0, 1.15 − 12.468, 8.030) = (0, −11.318, 8.030)`;
`sp.z = (−11.318)(−0.819152) + (8.030)(0.573576) = 9.2712 + 4.6060 = 13.877`. Lateral offset does **not** change
`sp.z` (`f` has no x component at yaw 0), so **the whole default-framing melee band is 11.81 → 16.49 m.**

> **Credit, and one correction that runs in the finding's favour.** The review computed **13.06 m** for the
> boar-at-player case. The difference is `OrbitCamera.targetOffset = (0, 1.0, 0)` — the rig orbits a point 1 m
> above the player root, not the root itself — so the corrected figure is **13.88 m**, i.e. **further past a 12 m
> cap**, not nearer it. The blocker is strengthened by its own correction.

**Across the reachable zoom band the number gets much larger.** `distance` is player-driven inside `minDistance`
6 u → `maxDistance` 26 u and `sp.z` tracks it almost one-for-one. Boar anchor, enemy at the player: **5.88 m** at
6 u, **13.88 m** at 14 u, **25.88 m** at 26 u. Add the 3.6 m reach directly away from the camera and the ceiling
at arm-time is **27.94 m** (boar) / **28.49 m** (snake).

**So the cap's real derivation is not a subordination crossover at all — it is "never suppress a row the player
armed."** The row arms only on YOUR landed strike (§3.1) and lives at most `hold 3.5 s + fade 0.4 s = 3.9 s` on
easy (§3.2), so the const must exceed the largest `sp.z` a struck enemy can reach inside that window:

> `max sp.z at arm` **+** `recession during the row's life`.

**Recession is bounded by the shipped AI, and it is small: neither enemy has a flee state.**
`BoarState { Wander, Chase, Windup, Charge, Cooldown, Dead }` and
`SnakeState { Wander, Chase, Telegraph, Lunge, Cooldown, Dead }` (read at `bf33b65`) — a struck animal closes on
the player or ambles; nothing runs away. The fastest recession available is the boar's **`chargeDistance = 3.8f`**
overshoot followed by **`wanderSpeed = 1.1f`** for the rest of the window: `3.8 + 1.1 × 3.4 = 7.54 m` of world
travel, adding `7.54 × cos 55° = 4.32 m` to `sp.z`. *(Upper bound — it ignores `cooldownSeconds`, during which the
boar does not travel.)* **Worst case ⇒ `28.49 + 4.32 = 32.8 m`.**

**⇒ `MaxDrawDistance` stays `40f`** — it clears the worst case with **~7.2 m** of margin and is the only value on
the table that does. **The `12f` proposal is WITHDRAWN**: it sits *inside* the default-framing melee band and
would have hidden the row through most of ordinary combat, converting a latent ambiguity into a live defect — and
§9's assert would have baked the mismatched frame into a shipped code comment. What the const gains here is the
derivation it never had. The old justification (*"a fleeing bleeder should not leave a pill dancing on the
horizon"*) is **retired on two counts**: it is a subordination claim, and the cap cannot deliver subordination at
**any** value (the snake fails at 14 u, in melee — the table above); and there is no flee state for it to describe.
**`40f` is now justified as an ARMED-ROW FLOOR expressed over the framing table**, per the bar's own *"express the
floor as an expression over the framing table, not a bare literal"* discipline. **Still a code const, not a
registry id** — §8's two-id budget is unchanged.

*(For the record, the 40 m ratios that motivated the withdrawn correction are real and unchanged: the pill is
**5.7×** the boar's on-screen height and **22.3×** the snake's at 40 m — boar `0.90 × 35.6075 × 14/40 = 11.22 px`
⇒ `64 / 11.22 = 5.70`; snake `0.23 × 35.6075 × 14/40 = 2.87 px` ⇒ `64 / 2.87 = 22.3`. They are a **§11 Q8**
subordination finding, not a cap finding, which is precisely the confusion that produced `12f`.)*

### 14.4 What the numbers CONFIRM (so the revision is not read as only bad news)

- **The 5-pip count is vindicated with a real figure.** §1.4 rejected 10 pips because they would be `4.0 px` each.
  This spec's pip is **10 px** wide — **2.5× the rejected figure**, and that ratio is exactly `10 / 4`: **this
  spec's own pip against the one it rejected.**
  > **🔴 CORRECTED 2026-08-01 (Devon, PR #406 review N2).** This bullet previously read *"a 4 px pip is 2.5×
  > smaller than the smallest candidate floor the bar discusses that would still red the house float-bob value."*
  > **That ratio resolves against nothing** — against the bar's four candidate floors it is `4 / 1.7804 = 2.25`,
  > `4 / 4 = 1`, `4 / 6.2080 = 0.64`, `4 / 1 = 4`. It was also a **category slip**: C1's floor governs a channel's
  > cued-vs-non-cued **delta**, not an element's absolute size, so a pip width may not be compared to it at all.
  > The claim above is the true one and needs no floor.
  **Five is right, and now it is right with arithmetic.**
- **Fixed-px sizing is vindicated.** §7's *"no distance scaling"* is what keeps the pip at a readable 10 × 6 px at
  every distance; a world-scaled row would be `10 × 14/26 = 5.4 px` wide at `OrbitCamera.maxDistance`. The cost of
  fixed-px is exactly the §14.3 finding — the pill does not shrink with its subject. **The cap is NOT the
  mitigation** (§14.3: the snake fails at 14 u, in melee, at any cap value); the mitigation on the table is
  **§11 Q8**, and that is the Sponsor's call.
- **The dark plate call is vindicated by the board** — with one correction to the header's wording, in §14.5.

### 14.5 Board re-look — the plate's contrast is real, but the header overstates it (corrected 2026-08-01)

**Looked at `inspiration/2026-06-12_21h13_31.png` and `2026-06-12_21h16_13.png` again for this revision**, because
§14 makes a size-and-contrast call and the images are the ground truth. The header's claim that there is
*"almost **no dark value anywhere** in frame"* is **too strong, and the overstatement matters at this element's
size.** What the images actually show:

- **Nothing approaches near-black.** The darkest values in both frames are the shaded faces of the mountains
  (`21h16_13`) and the tree trunks — mid-dark warm greys and browns. Charcoal `#2E2A2B` (L = 0.1686) and a 0.55-α
  black plate are still darker than anything the world contains. **The core call stands: the plate is the rarest
  value in this world, and that is what makes a 640 px² chip legible.**
- **But the frames are FULL of mid-dark shadow, and it is the largest dark shape in either image.** `21h13_31`'s
  ground is roughly half in soft tree-shadow; `21h16_13` has broad shadow bands under every pine and across the
  left mountain. The plate does not compete with *the sky or the sunlit grass* — those are the easy cases. **It
  competes with SHADOW**, and shadow is where animals stand.

**Consequence, and it is a soak watch-item rather than a spec change:** at 0.55 alpha the plate is
semi-transparent, so **a pip-row over an animal standing in tree shadow composites against an already-dark
background and loses plate/world separation** — exactly where the *pip* contrast (bone L 0.7792 vs charcoal
L 0.1686, §15.2) has to carry the whole read on its own. The pips are strong enough for that (the bone-charcoal
span is 0.6106, the largest value step available in the palette), so **no geometry or alpha change is proposed
here**. What is proposed: **the shipped-build capture set must include one row over an animal in tree shadow**,
not only over sunlit grass. A sunlit-only capture would evidence the easy case and miss the one the board says is
common — the presence-not-discrimination failure, applied to lighting.

---

## 15. Bar #10 audit under the AMENDED standard (added 2026-08-01)

**What this section audits against.** `team/quality-bars.md` § *"Bar 10 — the standard in full, and the four checks
(`86caz5na6` + `86cazhjw4`, 2026-07-31)"*, at `bf33b655e4953478549f4f74c5a692c39ee3c8f9`. The merged version of
this spec (`59a6e53`) was audited against the **pre-amendment** bar and claimed three channels; that claim is
withdrawn in §0 and re-adjudicated here.

### 15.1 First: name the CUE, because the count depends on which question is being asked

The merged spec counted three channels without stating which cue they serve, and that is the whole error — **the
element answers two different questions and they do not have the same channel set.**

| Cue | The player's question | The comparison (C3) |
|---|---|---|
| **Cue A — the READ** | *"Is **this** animal nearly down?"* | a second damaged enemy at a **different HP level**, same frame |
| **Cue B — ATTRIBUTION** | *"**Whose** row is that?"* | a second row over a **different body**, same frame |

**C3 is satisfiable at step 1 for both, which is the strong case** — a non-cued instance of the same kind is
visible in the same frame. It is available by construction, not by luck: §4.3b caps concurrency at `MaxRows = 3`
and §9's shipped-build capture **(f1)** already requires *"two damaged enemies on screen at once showing two
non-overlapping rows"* **at visibly different HP levels**. **That capture IS C3's step-1 pair and C4's
`cue_pair.png`** — the spec had already specified the artifact the bar needs, before the bar asked for it. No step-2 neighbour-naming is required, and the
world-object empty-set problem the bar spends C3 on does not arise for a HUD surface.

### 15.2 Cue A — the READ. Channels, axes, and the cued-vs-non-cued DELTA

**The magnitudes below are the DELTA between the cued and non-cued state, never one instance's own extent.** This
element is screen-space IMGUI at fixed px (§7, *"no distance scaling"*), so at the canonical 1280 × 720 capture the
pill's px **are** px — no world-to-screen conversion enters Cue A's magnitudes at all. Geometry check:
`5 × 10 + 4 × 2 = 58` px run in a 64 px pill ⇒ 3 px padding each side (matches §7). Pip pitch 12 px; pip area
60 px²; pill area 640 px².

| Channel | Axis | Non-cued state | Cued state | **Δ (C1 magnitude)** | Δ as fraction of the pill |
|---|---|---|---|---|---|
| **CH1 — lit-pip COUNT** | **FORM** | 4/5 lit | 1/5 lit | **3 pips repainted = 180 px²**; linear span **34 px** | 28.1 % of area, **53.1 % of width** |
| **CH1 worst case** (adjacent buckets) | FORM | 4/5 | 3/5 | **1 pip = 60 px²**; linear **10 px** | 9.4 % of area, 15.6 % of width |
| **CH1 degenerate case** (same bucket) | FORM | `Current01` 0.399 | `Current01` 0.201 | **0 px² / 0.00 px** | **0 % — FORM is blind here** |
| **CH2 — draining-pip ALPHA** | **colour → VALUE** (hue-independent) | `rem` 0.005, α 0.3532 | `rem` 0.995, α 0.9968 | **1 pip repainted = 60 px²**; linear **10 px** | 9.4 % of area |

**CH2's value depth, stated in its own units and NOT traded against the px figure** (the bar forbids quoting an
area/luminance claim as though it were the displacement figure). Rec.709 luminance on the authored sub-1.0 colours:
bone `#CFC6AD` **L = 0.7792**, charcoal `#2E2A2B` **L = 0.1686** ⇒ the bone-vs-charcoal span is **0.6106**. The
draining pip composited over charcoal moves **L 0.3842 → 0.7772** across the same-bucket extremes, i.e.
**ΔL = 0.3929** on that 60 px² block. The living-floor pip (α 0.35) sits at **L 0.3823**, still **0.2137** above
charcoal — so *"alive but nearly out"* is visibly distinct from *"spent"*, which is what §2.3's living-floor rule
promises.

**CH2's own worst case — the delta on a single sub-pip hit, which is the case this element exists for.** On a
non-boundary-crossing hit, `Δα = 0.65 × Δrem` and `ΔL = Δα × 0.6106`:

| Weapon (effective) | Enemy | Δrem per hit | Δα | **ΔL per hit** |
|---|---|---|---|---|
| `dagger_wood` (4.5) | boar **medium** (40) | 0.5625 | 0.3656 | **0.2233** |
| `dagger_wood` (4.5) | boar **hard** (50) | 0.4500 | 0.2925 | **0.1786** |
| `dagger_stone` (6.0) | boar hard (50) | 0.6000 | 0.3900 | 0.2381 |
| `axe_wood` (7.5) | boar medium (40) | 0.9375 | 0.6094 | 0.3721 |

**⇒ the weakest weapon on the hardest tier still moves the leading pip by ΔL 0.1786 on a 60 px² block** — on the
~55 % of hits where §2.3 shows FORM cannot move at all. That is the quantitative form of §2.3's promise, and it is
the single most important number in this audit: **CH2 is not a refinement of CH1, it is the only channel with a
non-zero delta on more than half of a `dagger_wood` fight.**

**Against C1's floor:** the smallest **non-zero** linear delta any channel here produces is **10 px** (one pip).
*(The "non-zero" is not hedging — CH1's degenerate row three rows above is **0.00 px**, and the unqualified
sentence this replaces was contradicted by this spec's own table; Devon, PR #406 review N1. The operative C1
magnitude for a quantised channel is the worst **adjacent** case, which is that same 10 px.)* That clears
**every** candidate floor the bar discusses — 1 px, the 1.7804 px game-juice collision figure, a 4 px reading, and
the 6.2080 px p2p frame-plane reading — by ≥1.6×. **The verdict below therefore does not depend on which floor is
eventually chosen**, which is the only honest way to state a magnitude against an unset floor.

**❌ CH3 candidate — POSITION: STRUCK, delta = 0.00 px.** The row's screen position is derived from the target's own
head anchor (§4.1). Between a nearly-dead enemy and a barely-scratched one, **that position is identical** — the
row does not move as HP falls. Per the bar's variance clause (*"a property present on every instance is style, not
a cue … it answers 'what KIND of thing is this', never 'WHICH one is cued'"*), position answers *whose* HP this is,
never *how much* — a different cue, counted separately as Cue B below. **The free invariance pre-filter kills it at
dispatch time with no build**, which is exactly what that pre-filter is for.

**❌ MOTION — deliberately empty, and that costs a channel.** §5.1 forbids every translation, scale-pop and shake on
this element, and §5.2's whole allowed set is *"alpha or colour-value."* That is the correct tone call
(`game-juice.md` §0/§2) and this revision does not disturb it — but it must be recorded as a **trade**, not a free
win: the element gives up the bar's third-ranked channel on purpose. Cue A's count is FORM + VALUE **only** because
MOTION was spent on tone.

### 15.3 C2 — FAILURE INDEPENDENCE: the two surviving channels collapse to ONE

**Name the thing whose absence kills each channel, at the nearest common dependency on the code path both actually
traverse — never a leaf property** (C2's tie-breaker, added precisely because author-naming has unbounded
granularity).

| Channel | Leaf-granularity name (the tempting, WRONG answer) | Nearest common dependency on the shared path |
|---|---|---|
| CH1 FORM | the per-pip `GUI.color` write in the lit branch | **the row record** (`filled` is read off it) |
| CH2 VALUE | the `drainA` field on the row record | **the row record** (`drainA` is a field on it) |

Both are written by **one `OnGUI` draw loop over one row record** (§7's architecture: *"`OnGUI` draws ≤ 3 rows from
cached state"*). **The named dependency is the row RECORD** — deliberately *not* `enemy_hp_pips_enabled` or the
resolve predicate, which sit further up as the element's existence gate and would prove far too much (see the
injection below). Retire the record and **both channels stop together**. ⇒ **count = 1. The pip-row alone does NOT
meet bar #10's ≥2.**

**This is #351's shape** — `WorldWeaponFind`'s float-bob and sway were called *"TWO independent transform-only
channels"* and were **independent in kind, identical in failure domain** (both die on one
`if (visual == null) return;`). CH1 and CH2 here are independent in *axis* (form vs value) and identical in failure
domain. **Different kind of independence, same failure.**

> **⚠ But the resemblance is weaker evidence than "exactly" implies, and this doc previously wrote "exactly"
> (Devon, PR #406 review).** #351 is C2's only *live* worked instance and it was **over-determined**: both its
> channels are MOTION, so the amended row already counts them as one by perception, and the bar says verbatim
> *"C2 does not change #351's count."* **This is therefore C2's first OUTCOME-DETERMINING use.** The verdict below
> stands on its own injection evidence, not on precedent — there is no prior application to check it against, and
> §13.1's decision draft says so rather than leaning on the resemblance.

**Settle it by INJECTION — and the injection must be able to return the OTHER answer.** A 2026-08-01 draft of this
section proposed one assert: null `enemy_hp_pips_enabled` (or force the resolve predicate false) and watch both
channels stop. **That assert is WITHDRAWN as evidence** (Devon, PR #406 review M3). It nulls the element's
*existence gate*, so it returns "collapse" for **every** cue in the game — including bar #10's own **PASSING**
worked pairing, whose mesh-presence FORM and shader-driven colour both vanish if the spawn gate is off. **A check
with exactly one possible output is not a check** — which is the opening premise of the very section this audit is
written against. The replacement is three parts, and only the middle one carries the verdict:

**(i) Leaf-distinctness — the count at leaf granularity really IS 2, which is what makes C2's tie-breaker
operative rather than a formality.** Pin `drainA` to a constant and assert `filled` still tracks HP; pin `filled`
and assert `drainA` still moves. Both pass. Had they not, C2 would never have been reached — the channels would be
one by simple aliasing, and the nearest-common-dependency rule would have had nothing to adjudicate.

**(ii) Resource enumeration + per-resource null — THIS is the evidence, and its output space is not a
singleton.** Enumerate every **resource** either channel reads on its draw path — the discriminator the bar's own
worked examples use (its constructed-FAIL is *two axes, one prefab reference*; its passing pairing is *prefab ref
+ material*, **two different resources**) — and null each in turn, recording which channels survive. For the
pip-row **that enumeration has exactly one member: the row record.** No material, no shader property, no prefab
reference, no `Transform`, no `ParticleSystem` enters the pip-row's `OnGUI` path at all — it is `GUI.DrawTexture` +
`GUI.color` over cached floats (§7's primitive discipline). Null it and both channels stop; **there exists no
injection that kills exactly one.** The check returns one verdict *per resource*, so a cue with two resources
returns *"resource R kills only channel A"* and counts **2**. ⇒ **count = 1, demonstrated rather than named.**

**(iii) The control that proves (ii) can return ≥2 — the identical procedure run on the composed cue (§15.4).**
Null the `_HitFlash` material ⇒ the flash dies, flinch and dust survive. Null `BoarBodyRig`'s part `Transform[]` ⇒
flinch dies, flash and dust survive. Null the pooled `ParticleSystem` ⇒ dust dies, the other two survive. **Same
test, three resources, ≥2.** ⚠ **OWED, NOT DELIVERED** — those three channels are `86caxjwb3`'s and are not
implemented at `bf33b65` (§1.5), so this half is a stated **prediction** in the bar's own C4 sense and is **not
counted as coverage**. It is written down so whichever ticket lands second inherits the control instead of
re-deriving it.

The `enemy_hp_pips_enabled` null survives as a **smoke test** — worth having, and explicitly **not** evidence for a
channel count. §9 labels it as such.

### 15.4 🔴 The verdict, and why it ratifies the sequencing decision rather than arguing with it

> **The pip-row is a ONE-channel cue. The enemy-damage read meets bar #10's ≥2 only as pip-row + body read.**

The body read (`86caxjwb3`, `enemy-hit-feedback-spec.md`) contributes channels in **three further, genuinely
independent failure domains** — the exact shapes C2's own list enumerates:

| Element | Channel | The thing whose absence kills it | Domain class (C2's list) |
|---|---|---|---|
| Pip-row | FORM + VALUE (one, per §15.3) | the row record / resolve predicate / `enemy_hp_pips_enabled` | a guarded code path |
| `_HitFlash` | value-step on the creature itself | the **material instance + the shader property** | *"a material or shader property"* — PR #349's documented silent-no-op class |
| Flinch | form-displacement of the creature's own parts | the **part `Transform[]`** on `BoarBodyRig` / `SnakeBodyChain` | *"a transform reference"* |
| Dust puff | added silhouette | the **pooled `ParticleSystem`** | *"a GameObject/prefab reference"* |

Null any one and the other three survive. **So the composed cue passes ≥2 with margin, and the pip-row alone does
not.** Three consequences worth writing down:

1. **The Sponsor's 2026-07-27 sequencing decision was structurally required, not merely tasteful.** His stated
   reason was tonal — *"shipping it first would make it the ONLY enemy-damage feedback in the game and the soak
   would judge it in exactly the distorted state it was not designed for."* Under the amended bar there is a second,
   independent reason: **pip-row-first would have made the game's whole enemy-damage cue single-failure-domain**,
   which bar #10 forbids outright. The decision needs no revisiting; it now has two justifications instead of one.
2. **It sharpens `86caxjwb3`'s AC6(c) question rather than pre-empting it.** *"Is 'is it nearly down?' already
   answered by the body?"* stays genuinely open and stays the Sponsor's to answer at that soak. What §15 adds is
   that **"body-read-only-forever" is a bar-#10-legal outcome** (three independent domains on the body alone) while
   **"pip-row-only" never was**. That is information for the decision, not the decision.
3. **Nothing here is a reason to change the element's design.** No channel is added to chase a count — adding a
   motion or hue channel to reach ≥2 in isolation would break §5's amplitude budget and §0's tonal anchor, and
   would be the bar-gaming the bar's own history section warns about. **The right response to a one-channel verdict
   is to state it, not to paper over it.**

### 15.5 C4 — the two-sided artifact: what this element owes, and what it already has

**C4 is SPECIFIED, not built** (build-lane, sequenced against the single Unity build slot; feasibility in
`team/erik-consult/two-sided-capture-feasibility.md`). Its verdict is human, not mechanical. Two notes specific to
this element:

- **The artifact is nearly free here, unlike for a world prop.** The bar observes that #351 needs a purpose-built
  two-instance scene because *"no cued/non-cued pair occurs anywhere in the live world at any dial setting."*
  **This element has the opposite property:** a cued/non-cued pair occurs in **ordinary gameplay** the moment the
  player hits two enemies — and §9's capture **(f1)** already requires it. **Do not build a purpose-built rig for
  this surface**; capture **(f1)**, taken at the stated framing, is `cue_pair.png`. *(§9 splits the old single
  capture (f) into **(f1)** the pair frame and **(f2)** the snake / tree-shadow hard cases, because five
  obligations on one frame let a capture silently satisfy four and miss one — PR #406 review N6.)*
- **The human half still applies and is not waivable.** `cue_pair.png` goes to someone who has not read the PR,
  with one question asked **before** they see any number — for this element the question is
  ***"point at the animal that's closest to going down."*** Right **first try, no second look** is the pass;
  hesitation or a wrong point is a FAIL. Record who was asked and the answer in the Self-Test Report.
- **The motion-extremes pair (`cue_ext_a` / `cue_ext_b`) is N/A** — this element has no motion channel (§15.2), so
  C1's rendered backstop has no live frame pair to measure. Geometry is the whole mechanical story here, which is
  exactly the case the bar describes for FORM/POSITION/colour channels.
- **Desaturate stays REQUIRED and passes by construction.** The element has no hue at all (bone / charcoal / cream
  are all near-neutral warm greys), so hue-independence is structural rather than tested-and-hoped. This is the one
  bar-#10 clause this element satisfies trivially, and it is worth saying that it is the *only* one it satisfies
  trivially.

### 15.6 Cue B — ATTRIBUTION: also one channel, and that is acceptable; state it rather than assume it

*"Whose row is that?"* rests on **POSITION** — the row's horizontal centre over its owner's head, which §4.3b
protects deliberately (*"displacement is vertical only, so the horizontal centre still points at its owner"*).
Delta: two enemies 3 m apart laterally put their rows **186.24 px** apart at the canonical framing
(`3 × 62.0798`) — enormous relative to any floor.

**But it is one channel.** Two enemies at the *same* HP produce pixel-identical rows differing only in position; the
per-body anchor height from §4.1 (boar `0.90 + 0.25 = 1.15 m`, snake `0.23 + 0.25 = 0.48 m`) is a real difference
between *kinds* but is still the POSITION axis and still the same failure domain. **Verdict: Cue B is
single-channel, and this spec accepts it** — POSITION is the bar's second-ranked channel, the delta is two orders
of magnitude above any candidate floor, and the alternative (making rows differ by form or hue per body) would mint
a per-enemy vocabulary that AC2 forbids. **Recorded as an accepted single-channel case with its reasoning, which is
what the bar asks for; not hidden inside a count of three.**

---

## Cross-references

- **Tickets:** `86caxhfg2` (this spec) · **`86caxjwb3`** (⛔ blocking predecessor — enemy body-level hit feedback;
  its soak decides whether this ticket lives) · `86cah7z2q` (parent — HP HUD polish; this spec's §6 origin) ·
  `86cah7yuh` (status effects — shares the enemy head; §4.3a arbitrates) · `86cah7xxp` (POC — `Health`) ·
  `86cah7ydt` (boar) · `86caaz4vn` (snake — the flat-HP balance item in §6) · `86cabcdpn` (combat design lock) ·
  `86caffwv5` (per-class swings — owns hit-stop / Impulse).
- **Code added by the 2026-08-01 revision (§14 — cited by SYMBOL, per bar #10's rule):**
  `Assets/Scripts/Editor/MovementCameraScene.cs` — the `BuildBoar` const block (`BoarGroundClearance`,
  `BoarBodyRadius`, `BoarBodyLength`, `BoarHeadLength`) and the `BuildSnake` const block (`SnakeNeckRadius`,
  `SnakeHeadLength`, `SnakeLinkSpacing`, `SnakeBodyLinks`) — the measured body sizes behind §14.2 ·
  `Assets/Scripts/Runtime/OrbitCamera.cs` — `defaultPitch`, `distance`, `minDistance`, `maxDistance`, and the
  `55x95 px` framing comment quoted **with the bar's own withdrawal caveat** (§14.2) ·
  `Assets/Scripts/Runtime/Combat/BoarBodyRig.cs` / `SnakeBodyChain.cs` — the `Transform[]` part arrays that are the
  flinch channel's failure domain in §15.4 · `Assets/Scripts/Runtime/VerifyCaptureFraming.cs`
  (`public static class VerifyCaptureFraming`) — where bar #10 places the world→px geometry assert.
- **Code (ground truth, read during authoring):** `Assets/Scripts/Runtime/LootPrompt.cs`
  (`:62` anchor height, `:65-72` plate/margin consts, `:112` **player** transform, `:174-176` projection + `z<=0`,
  `:191-192` clamp, `:212-220` the pure priority seam) · `Combat/Health.cs` (`:80-97` read surface, `:146-161`
  `ApplyDamage`, `:151` the damage formula) · `Combat/MeleeAttack.cs` (`:88-90` `LastDamageDealt`, `:229-231` the
  strike seam this arms from) · `Combat/BoarEnemy.cs` (`:40,42,44` per-tier HP, `:49` pierce ×2.0, `:54` slash
  ×0.75, `:117-131` `ApplyDifficulty`) · `Combat/SnakeEnemy.cs` (`:32` flat 24 HP, `:36` pierce ×1.6, `:95-100`
  `ApplyDifficulty`) · `Combat/ResistanceProfile.cs` (`:41-53` `Multiplier`) · `Combat/WeaponCatalog.cs`
  (`:62-129` the 15 defs' damage consts) · `SurvivalHud.cs` (`:44` `SegmentCount = 10`, `:47` `PlateAlpha`,
  `:83` `Charcoal`, `:84` `Cream`, `:361` the FLOOR rule — **and `:343`/`:365` the `TopSegmentThreshold = 0.95f`
  `N-1 → N` promotion that makes `FilledSegments` unsafe to reuse here, §2.2**) · `Combat/DeathHandler.cs` (`:20-21` corpse-pickable is
  a later ticket) · `Settings/SettingsCatalog.cs` (id convention + dead-knob precedent).
- **Docs:** `.claude/docs/game-juice.md` §0 (amplitude is the whole game) / §1 (easing, hit-stop cap, audio
  variation, pooling) / §2 (hard don'ts — every cap in §5) · `.claude/docs/art-direction.md` +
  `inspiration/2026-06-12_21h16_13.png`, `21h13_31.png` (looked at them — the high-key world that makes the dark
  plate the load-bearing value) · `.claude/docs/vision-far-horizon-game-concept.md` (kid → adult difficulty) ·
  `.claude/docs/unity6-mastery.md` §2 (GRD / no MPB) / §5-§6 (no alloc or Find in hot paths).
- **Uma specs:** **`enemy-hit-feedback-spec.md`** (the sibling body read — `86caxjwb3`; its **§1.3 amends this
  spec's §3.2** extinguish rule, its **§10** owns `enemy_hit_flash_seconds`, and its §13 extinguish draft is the
  same entry as this doc's) · `hp-hud-polish-spec.md` §6 (the merged parent this implements + corrects) ·
  `§2.3`/`§2.4` (the player-side wince + DoT debounce the enemy side deliberately does NOT copy) ·
  `status-effect-readability-spec.md` §3.2 (head-anchor precedence, honoured verbatim) ·
  `combat-cluster-design-brief.md` §1.2 / §2.5 (the body read — `86caxjwb3`) / §4 (primitive discipline) ·
  `style-guide-v2.md` §6 (bone `#CFC6AD`, sub-1.0, the plate-over-saturated-green watch item) ·
  `hud-three-bar-spec.md` (the segment/plate grammar).
- **Bars / memories:** `quality-bars.md` **#2**, **#7**, **#9**, **#10** · `[[difficulty-settings-easy-medium-hard]]`
  · `[[sponsor-danish-keyboard-layout]]` · `[[active-input-not-proximity-auto-for-actions]]` ·
  `[[served-unverified-soaks-need-played-verification]]` · `[[verify-grounding-soaks-by-gameplay-cam-visual]]` ·
  `[[claim-removed-soak-shows-present-investigate-foundation]]` · DECISIONS 2026-07-21 (above-head anchor),
  2026-07-22 (boar soak PASS / bar #9), 2026-07-27 (the sequencing decision that defers this ticket).
