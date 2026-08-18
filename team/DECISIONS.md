# Decision Log — Far Horizon

> **Append protocol (carried from RandomGame, adopted there 2026-05-15):** this file is centralized. Agents NEVER edit it directly — record `Decision draft:` lines in final reports; Priya batches them into a single PR. The orchestrator logs Sponsor-made and cross-role decisions directly.

Append-only. Format:

```
## YYYY-MM-DD — <short title>
- Decided by: <Sponsor | Priya | orchestrator>
- Decision: <one sentence>
- Why: <the load-bearing reason>
- Reversibility: <reversible | one-way>
- Affects: <roles or systems>
```

> **Correction protocol (established 2026-07-30, `86caz4td5`):** a **merged** entry is never rewritten — not even annotated with a forward-pointer, because an insertion into a historical entry still passes the 0-deletions invariant check and so erodes the record without tripping the guard. Append a new entry whose title begins `CORRECTION:` and which carries an extra **`- Amends:`** field quoting the amended entry's title verbatim + the SHA it merged in (title + SHA only — a positional word like "below" goes stale on the next append). When the correction is **partial**, name the clause **withdrawn**, the clause that **replaces** it, and — explicitly — what still **stands**; if a surviving clause's *reasoning* was replaced, say so, because "unchanged" claims the grounds held too and not just the conclusion. A **full reversal** needs only `Amends:` + `Withdrawn: the entire entry` — running the three-way split on a total reversal yields two empty fields, and ceremony gets skipped. A **widening** that withdraws nothing is a new entry citing the old, never a correction. The correction entry is the **only** place the retired wording is quoted verbatim: the doc being fixed must not reproduce it — not even as a negation — or a staleness grep for the old phrase can never come clean. That quote does double duty, and it is the reason no back-pointer is needed: a term-grep that lands inside the amended entry also returns the correction's `Withdrawn:` line, because both contain the retired wording. Corrections are Priya's, same as entries. (An entry still sitting on an **unmerged** branch may be edited in place — that is a draft revision, not a correction, and needs none of this.)

> **To find whether an entry has been amended, do NOT grep `CORRECTION:`.** That is the going-forward shape only. **11 earlier entries** (as of `c8ce948`) instead mark the amendment in their **TITLE** — `(supersedes …)` / `(reverses …)` / `(… WITHDRAWN)` — and some perform the same three-way split *inline* in a normally-titled entry (the 2026-06-24 need-HUD entry does all four fields in prose). Those are not being rewritten. The one scan that covers every shape, and that cannot go stale because it is derived from the file rather than maintained beside it:
> ```
> grep -nE '^## .*(CORRECTION:|supersed|reverses|WITHDRAWN)' team/DECISIONS.md
> ```
> ~12 hits, date-ordered — read the titles, then the `Amends:` field or the title marker for the target. **A hand-maintained amended-entries index is deliberately NOT kept:** it would be the one mutable surface in an append-only file, a missing row is an absent *insertion* so the 0-deletions check cannot detect it, and a reader who trusts it and finds nothing is back to "not amended or not indexed?" — the same false negative one level up. It is also not keyable: at least 4 of the 11 amend something that is **not an entry in this file** (the 2026-06-16 WASD entry reverses a `CLAUDE.md` line; the 2026-06-15 vista entry supersedes an Erik research recommendation; the 2026-07-27 low-HP entry withdraws a registry id that was never minted; the 2026-07-08 no-git-handoffs entry supersedes a working practice), and line numbers cannot serve as keys because every append shifts them.

Godot-era decisions (2026-05-02 → 2026-06-12) live in the archived RandomGame repo: `c:/Trunk/PRIVATE/RandomGame/team/DECISIONS.md`.

---

<!-- BATCH 2026-07-31 — the enemy-feedback spec pair (#376 `7d6d96f` + #371 `59a6e53`). 16 entries from 20 parked
     drafts; the 4 composes and the reason for each are recorded in this batch's PR body, not here. -->

## 2026-07-31 — Enemy body and pip row divide the labour: body = "that landed, and this hard"; row = "it's this close to down"

- Decided by: Uma (spec author, `86caxjwb3` / PR **#376** `7d6d96f` §1; the pip-row half stated identically in `86caxhfg2` / PR **#371** `59a6e53` §1) — Priya-batched.
- Decision: The enemy's two feedback surfaces carry **different questions and never the same one**. The **body** read (flash / flinch / dust, ON the creature) is **analog + instantaneous** and answers *"that landed, and this hard"*. The **above-head pip row** is **quantized + cumulative** and answers *"it's this close to down"*. Precedence is **one-directional: the pip row yields to the body, never the reverse** — the body is never suppressed, shortened or dimmed because the pip carries something.
- Why: the body is load-bearing exactly where the pip row is **silent**. A low-damage weapon moves **no pip at all** on ~44 % (medium boar) / ~55 % (hard boar) of landed hits, so a design that lets the row speak for the hit delivers the "I did nothing" false-negative both tickets exist to prevent. The asymmetry in the precedence rule follows from the asymmetry in the information: a missing body flash loses the only evidence the strike connected, while a missing pip step is recoverable on the next hit.
- Reversibility: reversible (a division-of-labour rule; a future design that made the row continuous and instantaneous would re-open it)
- Affects: Uma (both specs' §1) · Drew / Devon (`86caxjwb3` impl, and `86caxhfg2` behind it) · `team/quality-bars.md` #10 (the two surfaces are separate channels only because they carry separate information — the same variance argument bar #10 now encodes).

## 2026-07-31 — The shared `Health.Changed` seam is NOT a hit seam: it lies about cadence AND about magnitude, and both must be corrected before feedback hangs off it

- Decided by: Uma (spec author, PR **#376** `7d6d96f` §2.1 + §2.2), correcting `86caxjwb3`'s own premise — Priya-batched. Both corrections re-verified against `origin/main` @ `90d024b` while writing this entry.
- Decision: Feedback driven off `Health.Changed` must gate on **two** corrections, not one. **(a) Cadence** — bleed calls `Health.ApplyDamage` **every frame** (`StatusEffectController.cs:55-61` `Update`→`TickSeconds`, `:99` the per-frame `ApplyDamage`), so a literal *"fire on a damage delta"* strobes flash + flinch + dust for the full 3 s bleed. Gate it on a **magnitude threshold at 2.0 % of `Health.Max`** *and* a **0.12 s refractory window** — the threshold alone is frame-rate-sensitive because a bleed tick's amount is `dps × dt`. **(b) Magnitude** — `ApplyDamage` returns **clamped** `removed` (`Health.cs:157`; `SetCurrent` clamps at `:185`), so both the gate and any damage-proportional weight must read the **pre-clamp intent** (`effective`, `Health.cs:151`), exposed as a public read-only value on `Health`. Recomputing it attacker-side is **rejected** (breaks AC1's attacker-free contract and goes blind to non-weapon damage).
- Why: the two corrections are not independent, and that is the whole reason they are one entry. Applying (a) without (b) makes the **killing blow the quietest hit of the fight** — worse, on a hard boar (50 HP, `BoarEnemy.cs:44`) a clamped `removed = 1.0` is **exactly 2.0 % of `Max`**, i.e. sitting on the gate, so the kill can produce **no feedback at all**. The gate value is only safe because the pre-clamp read exists. Scale of the cadence problem: axe 2 dps / iron axe 3 dps over 3 s (`WeaponCatalog.cs:65-66` / `:119-120`), so the worst 60 fps tick is 0.05 HP — the 2.0 % gate sits ~9.5× above it and ~4.5× below the weakest shipped strike (`dagger_wood`, 6 base × 0.75 Slash = 4.5 HP = 9.0 % on a hard boar). A permanently-lit, vibrating enemy emitting ~60 particle bursts/second is also indistinguishable from the `[DFC-1]` latch bug — a wrong fix that looks like a known bug is the expensive kind.
- Reversibility: reversible (two gate values + one exposed field; the values are 🎚️ soak-tunable, the *shape* is not)
- Affects: Devon / Drew (`86caxjwb3` impl; `Health.cs` gains one read-only member) · `86cah7yuh` (status effects — the per-frame DoT path is theirs) · `86caxhfg2` (which solves the SAME root cause a different way — see the arming entry below) · any future cue driven off `Health.Changed`. Source: PR #376 §2.1/§2.2 + Devon's dev factual-check on PR #348 (`5109223633`, the `[DFC-*]` set).

## 2026-07-31 — The hit flash is a warm-biased multiplicative EXPOSURE LIFT on albedo BEFORE lighting, sub-1.0 ceiling — never a lerp toward flat warm-white

- Decided by: Uma (spec author, PR **#376** `7d6d96f` §2.3 / §3.1), refining `combat-cluster-design-brief.md` §1.2 — Priya-batched.
- Decision: The enemy hit flash is a **multiplicative exposure lift with a warm bias and a sub-1.0 ceiling (0.92)**, applied to **albedo before lighting** via the shipped `_MeadowPatchAmp` idiom (`Assets/Shaders/LowPolyVertexColor.shader:247-252` — a `> 0.0` guarded albedo write ahead of the lit/ambient assembly). It is explicitly **not** a lerp toward a flat warm-white.
- Why: the boar's ivory tusk `(0.90, 0.88, 0.78)` and near-black eye `(0.06, 0.05, 0.04)` are **per-vertex colours inside the same head mesh and the same material** (`MovementCameraScene.cs:2564-2565`; the four-tone head mesh at `:2814`). A flat-cream **lerp** at any readable amplitude therefore erases **both at once** — the identity features *and* the quality-bar-#9 tusk read that the boar soak passed on. A **multiply** preserves the tone ordering while still delivering a luma step of ~1.76× (boar) / ~1.52× (snake): hue-independent, and so desaturation-proof under bar #10. The sub-1.0 ceiling keeps it inside the HDR-clamp discipline (`style-guide-v2.md` §5) and below the Bloom threshold (1.02, `ZoneD_PostProfile.asset:59-64`) so a hit never blooms. And at amplitude 0 the expression is **bit-identical to today**, so the shared shader's no-op-at-default proof carries over unchanged — which is what lets a new float land on a shader every material in the world uses.
- Reversibility: reversible (a shader expression + one CBUFFER float defaulting to 0)
- Affects: Devon / Drew (`86caxjwb3`; the new float must go in `CBUFFER_START` per `LowPolyVertexColor.shader:93`) · every material on that shader (protected by the default-0 no-op) · `team/quality-bars.md` #9 (tusk read) + #10 (hue-independence) · `combat-cluster-design-brief.md` §1.2 (refined, not reversed).

## 2026-07-31 — The flash is the binary "connected" channel; weapon weight is EMERGENT from one scalar and the flash is excluded from it

- Decided by: Uma (spec author, PR **#376** `7d6d96f` §3 + §5), refining `combat-cluster-design-brief.md` §2.4 — Priya-batched.
- Decision: **The flash carries one bit — "connected" — and nothing else.** It is **distance-invariant** and does **not scale with damage**; its attack is **zero frames** (peak on the impact frame) and *"eased out"* means the **release** is eased, never ease-in-out. If it fails to read at orbit distance the lever is **DURATION** (0.08 → ≤0.14 s), never amplitude. Weapon weight instead rides **one scalar** — `w = sqrt(clamp01(intent / (Max × 0.5)))` — driving the **flinch** (`Lerp(0.50, 1.30, w)`) and the **puff count** (`Lerp(4, 9, w)`), with a **floor** (0.50× flinch / 4 particles) mirroring the pip row's living-floor rule. **No weapon-tier and no damage-type lookup.** Step-shaped or extra-beat differentiation is forbidden — that is a crit system.
- Why: these are one decision because the flash's exclusion is *what makes the scalar safe*. Amplitude is already pinned from two sides (the tusk-contrast requirement and the sub-1.0 ceiling), so there is no room to scale it; and a ≤1.2× tint delta inside 5 frames on a moving creature is **below discrimination** — spending the flash on differentiation costs it its role as a reliable binary and buys nothing. Distance- or damage-varying the flash destroys the one property that makes it **learnable**. The single scalar delivers brief §2.4's *"a pierce hit lands meatier"* **for free**, because pierce ×2.0 (`BoarEnemy.cs:49`) simply IS a bigger number — a ~1.5× spread across the shipped weapon set with no table to maintain, which is the same no-hardcoded-matchup discipline bar #9 confirmed at the boar soak. Note the scalar reads `intent`, not `removed`, per the seam entry above.
- Reversibility: reversible (one formula + two `Lerp` bands; the numbers are 🎚️ soak-tunable, the flash's exclusion is not)
- Affects: Devon / Drew (`86caxjwb3`) · `86caffwv5` (light swings — owns the impact frame this hangs off, and hit-stop/Impulse) · `team/quality-bars.md` #9 · `combat-cluster-design-brief.md` §2.4 (refined).

## 2026-07-31 — The flinch occupies an axis ORTHOGONAL to that creature's telegraph

- Decided by: Uma (spec author, PR **#376** `7d6d96f` §4) — Priya-batched. Rig amplitudes re-verified on `origin/main` @ `90d024b`.
- Decision: Each creature's hit flinch moves on an axis **orthogonal to its own telegraph**. Boar: head **UP** / body **BACK**, against the shipped head-down `headLowerDeg = 34f` gore tell and the `chargeLeanDeg = 12f` charge lean (`Combat/BoarBodyRig.cs:73`, `:75`). Snake: **LATERAL** whip, against its shipped **vertical** rear. Peaks are specified as fractions of already-approved amplitudes on the same rig (head 41 % of `headLowerDeg`, body 42 % of `chargeLeanDeg`, tail 1.25× the idle wag, snake lateral 1.6× `slitherAmplitude = 0.055f` at `Combat/SnakeBodyChain.cs:54`). **Rotation + small per-part offsets only** — no scale, no root write, no leg terms. Envelope ≤ 0.22 s with **exactly one** ≤15 % counter-overshoot.
- Why: a same-axis flinch **fakes a telegraph**. The player has been taught to read head-down as *the boar is about to gore*; a hit that also pushes the head down injects a false tell and regresses the charge read the Sponsor PASSED at the boar soak (bar #9) — a feedback addition that damages a working read is a net loss however good it looks in isolation. Calibrating as fractions of approved amplitudes rather than fresh numbers means the flinch cannot out-shout the telegraph it must stay legible against. Leg terms are excluded specifically because **four legs moving together reads as a collapse**, not a flinch — wrong message, not wrong amount. The single bounded overshoot is the reconciliation of bar #2 (motion never reads dead) with `game-juice.md` §2 (never a sustained wobble): one counter-beat is alive, two is a vibration.
- Reversibility: reversible (additive `LateUpdate` offsets; zero at rest by construction)
- Affects: Drew / Devon (`86caxjwb3`) · `86cah7ydt` (boar — the charge feel must not regress) · `86caaz4vn` (snake) · `.claude/docs/procedural-animation-verbs.md` (the additive-offset + zero-at-rest idiom; note the castaway `CastawayArmPose`→`HeldAxeRig` chain does NOT apply to an enemy rig) · `team/quality-bars.md` #2 + #9.

## 2026-07-31 — The tier stagger suppresses the agent's ADVANCE, never its `State`; `Windup` is non-interruptible at EVERY tier including easy

- Decided by: Uma (spec author, PR **#376** `7d6d96f` §4.5) — Priya-batched.
- Decision: The hit stagger is implemented as a suppression of the agent's **advance**, never a write to `State`, and it applies in **`Chase` only** (easy 0.35 / medium 0.15 / hard 0.0 s). **`Windup` is non-interruptible at every tier, easy included.** The recoil is **non-directional** — a hitch, not knockback.
- Why: suppressing advance rather than state makes `86caxjwb3` AC3's *"must not cancel a committed charge"* true **by construction** instead of by careful coding — the class of guarantee that survives a refactor. Keeping `Windup` non-interruptible even on easy is the counter-intuitive half and the load-bearing one: a cancellable telegraph can be **mashed away**, and then the boar never actually demonstrates the charge the player is supposed to learn to dodge. Easy must be *more forgiving*, not *differently taught* — a tier that removes the lesson is a different game, not an easier one (bar #7). Non-directional is a scope call with a hard reason: a directional recoil needs the attacker's position, which AC1's attacker-free seam does not carry, and displacing a `NavMeshAgent` body is a separate mechanic with its own failure modes.
- Reversibility: reversible (three per-tier durations, 🎚️ soak-tunable; the `Chase`-only scoping and the `Windup` exclusion are not)
- Affects: Drew / Devon (`86caxjwb3`) · `86cah7ydt` (`BoarAI` — the state enum + the Dead contract) · `team/quality-bars.md` #7 (per-tier) + #9 (the learnable charge) · `[[difficulty-settings-easy-medium-hard]]`.

## 2026-07-31 — The kill hit's treatment is a change of SHAPE, not of volume — it is ABSENCE

- Decided by: Uma (spec author, PR **#376** `7d6d96f` §6) — Priya-batched.
- Decision: The killing blow gets **less**, not more. The flash **plays and decays identically** and is **never zeroed on death**; the **flinch is cancelled** on `Died`; the impact puff is **suppressed** and replaced by a **softer, wider, slower death puff at the GROUND line, delayed 0.20 s**. No brighter flash, no corpse recolour, no slow-mo; whole death read ≤ 1.1 s.
- Why: the beats must read *"hit … it goes down"* — two events — rather than one undifferentiated kill-burst, which `game-juice.md` §2 forbids outright at this tone. Each clause has its own reason and they pull in different directions, which is why the entry is about shape rather than a single amplitude: **not zeroing the flash** because a snap to base colour on death is a **pop** (the most common cheap-looking death artifact); **cancelling the flinch** because a half-played recoil held on a settling body is a **twitch on a corpse**; **delaying the puff to the ground line** because with a topple out of scope, the ground-line dust is the only thing that makes the settle read as *landing* rather than as the model switching off. Brief §2.5 puts the dust on the tipping; absent a topple, this is how that intent survives.
- Reversibility: reversible (one delay + three amplitude/shape values, 🎚️ soak-tunable)
- Affects: Drew / Devon (`86caxjwb3`) · `86cah7ydt` (`BoarAI` death entry + the `dead ?` branches in `BoarBodyRig.LateUpdate`) · `.claude/docs/game-juice.md` §2 (the kill-burst hard-don't) · `combat-cluster-design-brief.md` §2.5.

## 2026-07-31 — No blocked / absorbed / immune outcome exists on a live enemy; and the forward rule is that zero damage is a different MESSAGE, not a lower AMPLITUDE

- Decided by: Uma (spec author, PR **#376** `7d6d96f` §7; §7's base-damage range corrected 4→6 by PR **#377** `92de044`) — Priya-batched. Re-verified on `origin/main` @ `90d024b`.
- Decision: There is **no zero-damage-on-a-live-enemy case to design for today**, and the two zero cases that *do* exist must produce **zero body feedback**: a **whiff** (the swing is its own feedback) and a **strike on a corpse** (hitting a dead animal must feel inert). **Forward rule:** if a block / parry / armour mechanic ever lands, it needs its **own** cue — **zero damage is a different MESSAGE, not a lower amplitude**.
- Why: immunity is **unrepresentable** in the shipped model, not merely unused — `ResistanceProfile.Multiplier` maps any `m <= 0f` to **NEUTRAL `1f`** (`Combat/ResistanceProfile.cs`, whose own comment states the intent: *"a missing profile never makes a target immortal"*). Both enemies keep `damageTakenMul` at 1.0, all 15 `WeaponCatalog` defs deal **> 0** (base 6 … 21 — the min is 6, not 4; that arithmetic was wrong on §7's low end and is corrected in `92de044`), and there is no block/parry/armour/shield system anywhere in `Runtime/Combat/`. The forward rule matters more than the present-tense finding: a **dimmed** flash is indistinguishable from a **weak hit that did land**, so the intuitive "less feedback for less damage" answer silently converts a *your attack was negated* event into a *you did a little damage* event — the single most misleading substitution available in a hit-feedback system.
- Reversibility: reversible as a scope statement; the forward rule is a design constraint on a mechanic that does not exist yet
- Affects: Drew / Devon (`86caxjwb3` — the whiff/corpse early-return at `Combat/MeleeAttack.cs`) · whoever ships a block/armour mechanic · `86cah7ym9` (weapon roster — the "all 15 deal > 0" premise) · `combat-cluster-design-brief.md`.

## 2026-07-31 — The dust puff spawns at the creature's renderer-bounds centre, never at a contact point, and its shape is never radially symmetric

- Decided by: Uma (spec author, PR **#376** `7d6d96f` §8) — Priya-batched.
- Decision: The impact dust spawns at the target's **renderer-bounds centre (~60 % height)**, **not** at a contact point. Shape is a short **upward-and-outward gravity-affected cone** — **never radially symmetric**. Proposed colour pin: pale warm earth **`#B39472`**, lighter than every creature tone; **never red**.
- Why: the shared `Health.Changed` seam **carries no hit position**, and AC1 forbids reaching back to the attacker to get one — so a contact-point origin is unavailable without breaking the seam that makes the whole system attacker-free. The design answer is that it does not matter: a ≤12-particle, 0.4 s burst is **not legible enough for its origin to be readable**, so the player reads *"dust, at the animal"* and the bounds centre satisfies that completely. This is the good case of a constraint and a design preference agreeing — and it makes enemy #3 correct for free, with no per-creature authoring. The two shape prohibitions carry the tone: a **ring** reads as an **explosion**, and **red** dust reads as **blood**, both of which break the calm register the whole spec is calibrated to. Lighter-than-every-creature-tone is what stops the puff reading as a **piece coming off the body**.
- Reversibility: reversible (a spawn point, a cone shape, one colour — colour is 🎚️ soak-tunable)
- Affects: Drew / Devon (`86caxjwb3` — the project's FIRST pooled `ParticleSystem`) · `.claude/docs/game-juice.md` §1.4 (pooled faceted bursts, ≤12) · `.claude/docs/lowpoly-quality.md` (chunky faceted particle shapes) · `.claude/docs/unity6-mastery.md` §2 (no `MaterialPropertyBlock` on juice VFX).

## 2026-07-31 — The lost-pip extinguish flash is CONDITIONAL on no body flash — in practice a bleed/DoT-only accent, met by construction rather than by tuning

- Decided by: Uma (spec author). **One entry from two spec sections that state the same rule** — `86caxhfg2` / PR **#371** `59a6e53` §5 + §7 (and its own §13 draft), **as amended by** `86caxjwb3` / PR **#376** `7d6d96f` §1.3, which is the authority here. Priya-batched. The hp-read draft says so itself: *"this draft and that spec's §13 extinguish draft state the SAME rule; batch them as one entry, not two."*
- Decision: The pip row's one permitted accent — a lost-pip extinguish flash at **~60 % of the player HUD's amplitude and half its duration** (`#EAD9B8`, α ≤ 0.85, ≤ 0.24 s total) — **fires only when no body flash fired on that target within `enemy_hit_flash_seconds`**. Since a **strike always brings a body flash**, that makes it a **bleed/DoT-only** accent, and **an extinguish flash on a strike frame is a bug, not a miscalibration.** `enemy_hit_flash_seconds` is owned by `enemy-hit-feedback-spec.md` §10.
- Why: the pip's information **is the count change**; on a strike frame an extinguish flash duplicates the body's *"something happened"* while adding nothing, and it does so at the exact moment the loudness ordering (body flash > flinch > dust > pip row) is most crowded. The valuable half is the **mechanism**: gating on *"did a body flash fire?"* rather than on a tuned amplitude means the ordering is met **by construction**, so it cannot drift when either surface is re-dialled — and it converts a calibration question into a testable predicate. Suppression stays **one-directional** (the body is never suppressed for the pip) per the division-of-labour entry above. The reason this is one entry and not two: the extinguish rule was parked in **both** specs' draft lists; splitting it would put the same rule on the record twice with two different sources, and a later reader amending one copy would leave the other standing.
- Reversibility: reversible (one predicate + three values, 🎚️ soak-tunable)
- Affects: Drew / Devon (`86caxhfg2`, deferred behind `86caxjwb3`) · `enemy-hp-read-spec.md` §3.2 (the rule it amends) + §5/§7 · `enemy-hit-feedback-spec.md` §1.3 (the amending authority) + §10 (owns the dial) · `hp-hud-polish-spec.md` §2.3-§2.4 (the player-side wince the enemy side deliberately does not copy).

## 2026-07-31 — The enemy pip row is a 5-block quantized PROPORTION read — explicitly NOT "hits remaining" — with a draining leading pip and a living floor

- Decided by: Uma (spec author, `86caxhfg2` / PR **#371** `59a6e53` §2 + §2.3) — Priya-batched. Re-verified on `origin/main` @ `90d024b`.
- Decision: The row is **five pips reading a PROPORTION of max HP**, explicitly not *"hits remaining"*. Sub-pip hits are carried by a **DRAINING leading pip** — rendered at `α = Lerp(0.35, 1.0, remainder)` — and a **living-floor** rule guarantees at least one pip at ≥ 0.35 α while `Current > 0`, so the row never reads dead while the animal is alive.
- Why: hits-to-kill spans **2 … 9** on a medium boar across the 15 shipped `WeaponCatalog` defs (`spear_iron` 2 → `dagger_wood` / `pickaxe_wood` 9), so a fixed 5-pip row interpreted as hits would be **wrong for 13 of the 15** — only `sword_wood` and `pickaxe_iron` land on 5. Five is additionally **geometry-forced**: ten pips inside the pinned 64 px pill would be 4.0 px each. The draining pip is not a separate nicety but the **completion of the same decision** — quantizing to five *creates* a sub-pip blind spot, and shipping the quantization without the drain delivers the "I did nothing" false-negative on up to ~44 % (medium) / ~55 % (hard) of landed hits. It is **value, not width**: a 10 × 6 px pip has no readable width granularity, and a value step survives desaturation (bar #10). A continuous bar was considered and rejected — it forks the HUD grammar, loses its leading edge at orbit distance, and is single-channel under bar #10.
- Reversibility: reversible (a count + an alpha band; the count is geometry-forced, so re-opening it means re-opening the pill geometry)
- Affects: Drew / Devon (`86caxhfg2`) · `SurvivalHud.cs` grammar (`:44` `SegmentCount`, `:47` `PlateAlpha`, `:361` the FLOOR rule this mirrors — **and `:343`/`:365`'s `TopSegmentThreshold = 0.95f` promotion, which makes `FilledSegments` unsafe to reuse here**) · `hud-three-bar-spec.md` · `team/quality-bars.md` #10.

## 2026-07-31 — The enemy pip row ARMS on the player's landed strike, not on `Health.Changed` — the same root cause as the body gate, solved differently because the constraints differ

- Decided by: Uma (spec author, `86caxhfg2` / PR **#371** `59a6e53` §3.1) — Priya-batched.
- Decision: The row **arms on the `MeleeAttack` strike seam** (`removed > 0f`), **not** on `Health.Changed`. `Health.Changed` drives the row's **VALUE while it is already showing** and **never extends the hold**.
- Why: same root cause as the body-feedback gate — the axe's bleed ticks through `Health.ApplyDamage` — but the **remedy is deliberately different, and the difference is instructive**. Arming on `Changed` would re-summon a plate over a **disengaged** animal for 3 s of DoT while showing nothing change: the enemy-side sibling of the HUD's own §2.4 DoT strobe. The body side could not take this route because AC1 forbids attacker coupling, so it had to correct `Health.Changed` in place with a magnitude gate plus a refractory window; the row has **no such constraint**, so it can simply arm from a seam that already means *"the player hit something"* (`Combat/MeleeAttack.cs` exposes `LastDamageDealt` and computes `removed` at the strike). The generalisable point: **one root cause does not imply one remedy** — the cheapest correct fix depends on which seams the surface is allowed to touch, and recording only the body's gate would have left a future author assuming the gate is the house pattern.
- Reversibility: reversible (a trigger source)
- Affects: Drew / Devon (`86caxhfg2`) · `86cah7yuh` (the DoT path) · `hp-hud-polish-spec.md` §2.4 (the player-side DoT debounce this is the sibling of) · the seam entry above (`86caxjwb3`'s gate — the same root cause).

## 2026-07-31 — The enemy row reuses `LootPrompt`'s projection IDIOM but diverges from it on anchor, stack order, off-screen policy and concurrency

- Decided by: Uma (spec author, `86caxhfg2` / PR **#371** `59a6e53` §1.3 / §4 / §4.2 / §4.3) — Priya-batched. `LootPrompt`'s anchor re-verified on `origin/main` @ `90d024b`.
- Decision: *"The shared above-head anchor"* means a shared **projection idiom and code path**, **not** a shared screen position: `LootPrompt` anchors above the **PLAYER's** head (`Runtime/LootPrompt.cs` resolves `_playerT` from the looter's `player`, else its own transform — it is authored ON the player), so the interaction pill can never contend for an enemy's head. The **enemy** head stack is therefore its own deterministic order — **`[head] → status cue band → pip row`** — and the enemy anchor **height** is derived once per arm from the target's renderer bounds (+0.25 m clearance), never the castaway's 2.2 m. Four deliberate deltas from `LootPrompt`: it **clamps for spill but HIDES when its anchor is off-screen** (`LootPrompt` clamps unconditionally because the player is always on frame); concurrency is capped at **`MaxRows = 3`**, a **code const, not a third registry id**; placement is **nearest-first with vertical-only de-overlap** so a row's horizontal centre always still points at its owner; and the pip row **never** displaces the interaction pill.
- Why: these are one entry because the failure mode is one action — **copying `LootPrompt` wholesale** — and it gets all four wrong at once. A clamped row over an off-frame enemy is an **orphan plate naming nothing**, which is strictly worse than no row. Horizontal de-overlap would break the row's only binding to its owner. And the stack order **honours `status-effect-readability-spec.md` §3.2's "status wins the head anchor" verbatim rather than re-litigating it** — which additionally prevents an IMGUI plate from occluding rising poison pips, a defect neither spec would have owned.
- Reversibility: reversible (a placement policy + one const)
- Affects: Drew / Devon (`86caxhfg2`) · `86cah7yuh` (shares the enemy head; §3.2 arbitrates) · `Runtime/LootPrompt.cs` (idiom reused, policy deliberately not) · `Settings/SettingsCatalog.cs` (`MaxRows` deliberately NOT a registry id).

## 2026-07-31 — The enemy pip row's entire animation budget is alpha and colour-value: nothing moves, scales or shakes

- Decided by: Uma (spec author, `86caxhfg2` / PR **#371** `59a6e53` §5 + §7) — Priya-batched.
- Decision: The element animates **only** in alpha and colour-value. **No row-nudge, no scale-pop, no plate flash, no hue shift, no particles, and no Impulse or hit-stop of its own.** The single permitted accent is the conditional lost-pip extinguish (its own entry above).
- Why: a row-nudge is not merely excess amplitude — in this game's established vocabulary it **means something else**: nudging the HUD is how *you were hit* reads, so borrowing it for an enemy's HP row asserts damage to the player. Wrong message, not wrong volume. Scale-pop and plate flash are excluded on the loudness ordering (the row is last behind body flash > flinch > dust); hue shift is excluded because a value-only read survives desaturation and a hue read does not (bar #10); Impulse/hit-stop is excluded because `86caffwv5` owns those channels and a second owner means two systems fighting for the same frames. Confining the budget to two properties also makes the whole element cheap to reason about at review time — every proposed addition is answerable by "is it alpha or value?".
- Reversibility: reversible (a prohibition list)
- Affects: Drew / Devon (`86caxhfg2`) · `86caffwv5` (owns hit-stop / Impulse) · `.claude/docs/game-juice.md` §2 (the hard-don'ts) · `team/quality-bars.md` #10.

## 2026-07-31 — Difficulty changes only the generosity of TIME on the enemy pip row, never the read

- Decided by: Uma (spec author, `86caxhfg2` / PR **#371** `59a6e53` §6) — Priya-batched. Per-tier HP re-verified on `origin/main` @ `90d024b`.
- Decision: Form, colour, count, position, trigger and state machine are **identical at all three tiers**. Only `enemy_hp_pip_hold` moves (**3.5 / 2.0 / 1.2 s**), and `enemy_hp_pips_enabled` stays **global and ON at every tier**. If hard reads as illegible, the dial to move is the **HOLD**, never the pip count.
- Why: turning the row off on hard makes hard **a different game**, and — the sharper objection — it makes the soak **unfalsifiable exactly where the read matters most**, because the tier under the heaviest pressure is the one with no readout to judge. A per-tier asymmetry already exists **for free** and is the reason the count must not become a tier dial: boar HP is per-tier (32 / 40 / 50, `Combat/BoarEnemy.cs:40/42/44`) while weapon damage is not, so pip resolution is already coarser on easy and finer on hard. Adding a second tier-varying term on top of that would make two tiers differ in ways nobody can attribute.
- Reversibility: reversible (three hold durations, 🎚️ soak-tunable)
- Affects: Drew / Devon (`86caxhfg2`) · `Settings/SettingsCatalog.cs` (a live dial must write BOTH the active field and the active tier's map entry — the dead-knob class) · `team/quality-bars.md` #7 · `[[difficulty-settings-easy-medium-hard]]`. **Logged, not fixed:** the snake's HP is flat **24** across all tiers (`Combat/SnakeEnemy.cs`, `SnakeMaxHp = 24f`) while the boar's is per-tier — a balance-lane inconsistency owned by `86caaz4vn`, out of scope for both specs.

## 2026-07-31 — The enemy pip row ships at FIVE regardless of whether the player's 5-segment HP bar lands first

- Decided by: Uma (spec author, `86caxhfg2` / PR **#371** `59a6e53` §1.4) — Priya-batched. Re-verified on `origin/main` @ `90d024b`.
- Decision: The enemy row ships at **5 pips either way**, so `86caxhfg2` is **not sequenced behind** `86cah7z2q`. If the player's 5-segment bar lands first, the two read as a **shared vocabulary**; if it does not, **5-vs-10 reads as correct hierarchy** — yours detailed, theirs coarse.
- Why: this is an **ordering** decision, and it exists to stop a false dependency from forming. The player's HP bar on `main` is still **10 segments** (`SurvivalHud.cs:44 SegmentCount = 10`; there is no `HpSegmentCount`) — `86cah7z2q`'s 5-segment bar is **Sponsor-locked but unshipped**, which is exactly the state in which a planner invents a blocking dependency out of a vocabulary-consistency worry. The count is **geometry-forced** regardless (see the pip-row entry above), so there is no version of this where waiting changes the answer. Recording it means a future board pass cannot re-gate `86caxhfg2` on `86cah7z2q` without contradicting a written decision. Note the row's own actual blocker is different and real: it is deferred behind `86caxjwb3`, whose soak decides whether it lives at all.
- Reversibility: reversible (a sequencing call)
- Affects: Priya (board sequencing — do NOT gate `86caxhfg2` on `86cah7z2q`) · Drew / Devon · `86cah7z2q` (the player-side bar) · `hud-three-bar-spec.md` (the segment/plate grammar both share).

## 2026-07-30 — CORRECTION: committed Blender export/build SCRIPTS live in `tools/debug/`, not `art-src/`

- Amends: the entry **"An asset/hero PROVENANCE commit must include the export SCRIPT — `.blend` + FBX alone is not a reproducible export"** (dated 2026-07-30; merged in `51f4623`, PR #367 — title + SHA are the keys, deliberately no positional word). Correction by Priya — the amended entry is my own; `86caz4td5`.
- **Withdrawn:** that entry's LOCATION clause only — *"A committed `bpy` export script beside the source `.blend` in `art-src/` is the preferred form"* and *"it lives in `art-src/`, not next to the FBX"*. `art-src/` is the wrong directory.
- **Replaced by:** an export / provenance / asset-build script is committed to **`tools/debug/`**, with a one-line row in `tools/debug/REGISTRY.md`. `art-src/` holds **non-executable source artifacts only** — `.blend` sources, palette PNGs, concept art, hero/family renders, provenance `README`s. **The `REGISTRY.md` row is not tidiness — it is the replacement for the discoverability the withdrawn clause was buying.** *"Beside the source `.blend`"* was **provenance-by-adjacency**: find either artifact and you have found the other, with no index to consult and nothing to keep in sync. `tools/debug/` breaks that adjacency in **both** directions — the `.blend` no longer carries its recipe, and the recipe no longer carries its source — so a script with no row is not merely un-indexed, it is unfindable, and provenance that cannot be located is not provenance. That is why the row is part of the convention rather than a nice-to-have, and it is why this correction replaces a **mechanism** and not only a path. One precision the amended entry is owed: adjacency was never actually in force — zero `.py` had ever been committed under `art-src/` — so what is replaced is the mechanism that entry *chose*, not a property the repo ever had.
- **Still stands, unchanged:** that the export RECIPE is *provenance* and binding on every asset/hero provenance commit; that it must record (1) the mandatory `Join` step, (2) the resulting joined object's NAME, (3) the axis/scale export settings; the load-bearing reason (Unity's **Generic** rig binds by transform path, so a differently-named `Join` imports cleanly and silently never binds); and that a README statement of what was actually done is the acceptable minimum.
- **Still stands, but on REPLACED grounds:** that `blender-asset-pipeline.md:279`'s old location was wrong. Same verdict, different warrant — it now rests on the re-measured evidence below (`Assets/Art/` holds zero `.py`), **not** on the original's inheritance from `character-pipeline.md:62`, which this correction withdraws as a method. That inheritance was the original's *only* argument for the conclusion, so filing this under "unchanged" would claim support this entry itself demolishes. Listed separately for that reason: a conclusion that survives on new grounds is not an untouched clause, and the one item in a still-stands list whose warrant the correction destroyed is exactly the item a later reader must not be allowed to skim past.

  Taken together: the rule is not reversed and the recipe is still binding provenance — but a path **and** a discoverability mechanism changed, and one surviving conclusion was re-grounded. Narrow fix, not a reversal; more than a directory swap.
- Why the original was wrong: I checked **two** candidate directories (`art-src/`, `Assets/Art/`), found zero `.py` in both, and then picked `art-src/` by inheriting the *source-artifact* convention (`character-pipeline.md:62`) rather than by finding where scripts actually are. I never searched `tools/debug/`. Ground truth re-measured for this correction on `origin/main` @ `c8ce948`: `git ls-tree -r --name-only origin/main | grep '^tools/debug/.*\.py$'` returns **28** files — 27 of which use the `bpy` API; the 28th (`blender_mcp_send.py`) is the TCP transport to the Blender-MCP addon, so Blender tooling either way — while the same query under `art-src/` and under `Assets/Art/` returns **zero**. The count is also 28 at `51f4623`, so `86caz4td5`'s "26" was a miscount against its own enumeration (25 `bl_*` + 3), not staleness. **The generalisable lesson: "zero in the two places I looked" is not evidence for either of them.** A location claim must be settled by searching the repo for the ARTIFACT KIND (`*.py`) and reading off the directory that has them — never by inheriting a sibling artifact's convention. The amended entry even recorded its own "zero under either" measurement and still concluded from it; a null result pointed at the answer being somewhere unlooked, and was read as a tiebreak instead.
- Reversibility: reversible (a location convention — re-homing the scripts would legitimately re-open it)
- Affects: Devon / Drew / Uma (every asset-provenance and Blender-script commit) · `.claude/docs/blender-asset-pipeline.md:279` — **corrected in the same PR as this entry**, and now the single authority for the location; this entry no longer overrides it · `tools/debug/REGISTRY.md` (the row is part of the convention) · `86caywfjq` (v4's unrecorded `Join` step — its export script goes to `tools/debug/`, not `art-src/`). Known adjacent gap, reported not fixed: 5 of the 28 scripts (`bl_01b_palette_flint.py`, `bl_02b_axe_flint.py`, `bl_02c_flint_head.py`, `bl_02d_flint_head_v2.py`, `bl_03b_uv_flint.py`) have no `REGISTRY.md` row, so `86caz4td5`'s claim that the registry describes "each" was also wrong — coverage is 23/28.

## 2026-07-30 — A posture/affordance cue on a procedurally-jittered mesh is specified as a CATEGORICAL aspect inversion, never a size ratio

- Decided by: Uma (spec author, `86cav8ybj` / PR #362 round 2 at head `c3ec4d7`), after Devon's round-1 `REQUEST_CHANGES` NIT N1 (comment `5131041457`) — Priya-batched. ⚠ **PR #362 was OPEN when this entry was written** (spec-only, zero `Assets/**`, direction half only). The rule below is a spec-authoring methodology call and does **not** depend on which direction option the Sponsor picks; it survives even if the rock direction is rejected outright. ⚠ **#362 owes TWO drafts and only this one is recorded.** Its round-1 sibling — *"Interactive-vs-scenery disambiguation is bought by POSTURE on the non-interactive class, never by hue and never by re-authoring the hero prop"* (PR #362 body §"Decision draft (for Priya's weekly DECISIONS.md batch)") — is **deliberately HELD for a later batch, not missed**: unlike this one it IS direction-dependent, so it must not land ahead of the Sponsor's A/B/C pick.
- Decision: When a readability cue must separate two object classes built from the SAME procedurally-jittered mesh generator, specify the cue as a **categorical aspect inversion** ("the decorative class crosses from taller-than-wide to **wider**-than-tall"), never as a size/height **ratio**. Where a ratio is still informative, quote **worst-case alongside nominal** — never a single number that is neither. Any ratio that survives into an acceptance criterion must be a **measured per-instance check** (encapsulate the shipped renderer bounds, report the achieved *minimum*), never a constant derived from nominal half-extents.
- Why: `LowPolyMeshes.FacetedRock` applies a per-instance `sy ∈ [0.85, 1.03]` (`LowPolyMeshes.cs:375`), a **per-vertex** `rj = 1 ± jitter/2` (`:382`) and an absolute wobble of `± radius × jitter × 0.11` (`:386-388`), putting the vertical factor at roughly `V ∈ [0.63, 1.29]`. The asymmetry that decides which guarantee survives: the mesh is a subdiv-1 octahedron, so `V` is set by essentially **one** pole vertex and has a real low tail, while the planar extent is the **max of twelve** and concentrates near its top. A ratio derived from a nominal half-extent therefore collapses at the tail — `86cav8ybj`'s claimed **≥2× apex floor** re-derived to **1.32×** worst case on the recommended option and **1.06×** on the "keep the mass" option, i.e. the tallest decorative slab essentially reaching the shortest ore node's apex, which the round-1 phrasing "still 2.1×" concealed. Round 1's quoted 2.7× / 2.1× were neither nominal nor worst-case — an inconsistent middle. The aspect **inversion** by contrast survives every draw — but its threshold is **squash-scoped, not universal**: flipping it needs `V × q ≥ P`, so at the specified squash **`q = 0.60`** (options A and C) it needs `V ≥ 1.60`, against a 1.268 cap (spec §2.3, `rock-affordance-direction.md:142-143`, which scopes it the same way at `:333` and `:536`). Option B's `q = 0.72` re-derives the same floor to **≈1.33** (`0.96 / 0.72` off the identical realistic-`P` floor the 1.60 comes from — **Uma's derivation in the PR #367 round-2 review**, comment `5135576618`, not a #362 spec figure), still clear of 1.268. So the inversion holds on **all three** options, at ~26% headroom on A/C and ~5% on B; quoting only the roomier figure would be this entry's own rule failing one level up. Two corollaries worth carrying: (a) the reviewer's own worst-case estimate (~1.7×) was itself too generous because it sized the low tail off `BoulderPoolSize = 7` (`MovementCameraScene.cs:3049`) when the ore pool is `IronDifficultyPresets.Easy.OreNodeCount` (`:2871`) — more draws, deeper tail; (b) a number a human picks an option **by** must be the number he actually **gets**.
- Reversibility: reversible (a spec-authoring rule; a future generator with a tight vertical distribution could re-earn a ratio floor — but only by measuring it)
- Affects: Uma (spec authoring) · Devon / Drew (the `86cav8ybj` implementation half, and any AC quoting a size ratio as a floor) · `team/quality-bars.md` Bar 10 (FORM-first channel ranking) and its "Open / unconfirmed" candidate row · `team/TESTING_BAR.md` bounded-convergence claims. Source: PR #362 body §"N1 — honest worst-case, quoted alongside nominal" + round-2 Self-Test Report (comment `5131293335`); Devon's peer review (comment `5131041457`, "Decision draft in the PR body: read, not acted on. It's Priya's to batch"). Every `LowPolyMeshes.cs` / `MovementCameraScene.cs` anchor above re-verified on `origin/main` @ `b9abf7b` while writing this entry.

## 2026-07-30 — An asset/hero PROVENANCE commit must include the export SCRIPT — `.blend` + FBX alone is not a reproducible export

- Decided by: Tess (QA verdict `PASS_WITH_NITS` on PR #357, comment `5129792893`, §"What a future re-rigger would still be missing" + NIT 1) with Drew's independent peer-review NIT 2 (comment `5128605270`) — merged `840a1c6`; Priya-batched.
- Decision: A provenance commit for any hero/asset version must commit the **export recipe**, not just the source `.blend` + the FBX set. The recipe must state, as things that WERE DONE rather than things to consider: (1) the **`Join` step** — that it is mandatory, (2) the **resulting joined object's NAME**, and (3) the axis/scale export settings. A committed `bpy` export script **beside the source `.blend` in `art-src/`** is the preferred form; a README statement of what was actually done is the acceptable minimum. `blender-asset-pipeline.md` §10 already recommends *scripting* the export (table row "FBX export with exact settings | YES") **and** already says to commit it (`:279`, "Commit the script next to the FBX so future passes re-run deterministically") — what this decision adds is that the script is **provenance, binding on every asset/hero provenance commit**, and that it lives in **`art-src/`, not next to the FBX**: `:279`'s location contradicts `character-pipeline.md:62`'s standing source convention ("new character generations land under **`art-src/`**") and the shipped layout, and `art-src/` wins. "Consider doing X" phrasing does not discharge the requirement.
- Why: on `86cayp1vb` (castaway v4) the committed `.blend` holds **40 separate mesh objects** while every committed and shipped FBX contains **exactly one** mesh node (`CastawayV4`, 960 verts / 1,760 tris — geometry-identical across the `.blend`, `castaway_v4_apose_rawfix.fbx` and the shipped `Assets/Art/Character/Castaway/v4/castaway_v4_rigged.fbx`, sorted-world-vertex digest `02d47c052037d21bf71f20d0` on all three). So a `Join` demonstrably happened at export — and nothing committed records it as done: the README's §Handoff step 1 still reads "**Consider** `Join`-ing the 40 parts into one mesh first for Mixamo upload", phrased as an open question, and no export `.py` is committed. The object **name** is the sharpest edge, and the reason this is not merely tidiness: Unity's **Generic** rig binds by transform path, and the shipped rig's mesh node is `CastawayV4` — so a differently-named `Join` produces a file that imports cleanly and simply never binds, a silent break with no error to grep for. Item (3) is already documented (`character-pipeline.md` §Step 3); (1) and (2) were not, and they are the two a re-exporter must otherwise re-derive from a raw FBX parse.
- Reversibility: reversible (a provenance-checklist rule; it adds one file to a commit that already exists)
- Affects: Devon / Drew / Uma (every future asset-provenance commit) · `.claude/docs/blender-asset-pipeline.md` §10 (the rule it makes binding — **and `:279`, whose "next to the FBX" location this decision overrides; a doc fix is owed there, unticketed as of this entry**) · `.claude/docs/character-pipeline.md` §Step 3 + `:62` (the `art-src/` convention that wins) · open tickets `86caywfjq` (v4's Join step unrecorded — the ticket that discharges this for v4) and `86caywf84` (the sibling code-comment staleness filed off the same review). Scope note: this decision does **not** widen to the Mixamo auto-rig settings, which `86cayp1vb`'s own review recorded as a separate, non-blocking gap. Source: PR #357 (`840a1c6`) comments `5129792893` + `5128605270`; the `art-src/`-vs-FBX location split re-verified on `origin/main` @ `3992e96` (sources `art-src/castaway_v4.blend` + `art-src/castaway-v4-README.md`; FBX ships to `Assets/Art/Character/Castaway/v4/castaway_v4_rigged.fbx`; **zero** committed `.py` under either `art-src/` or `Assets/Art/`).

## 2026-07-27 — Status-effect framework: FIVE bounded extensions, contract-pinned vocabulary (not "just data")

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #339 (`e13a51e`)
- Decision: Adding poison / stun / slow to the shipped bleed-only framework requires exactly five bounded extensions, named per `86cah7yuh`'s pinned VOCABULARY CONTRACT as amended 2026-07-27 (ticket comment `90150245438801`): (1) kind-aware `StatusEffectSpec.IsActive` — DoT kinds keep the DPS+duration rule, control kinds require duration only; (2) a `magnitude01` field appended to the existing struct, with factories `MakePoison(dps, duration)` / `MakeStun(duration)` / `MakeSlow(magnitude01, duration)`; (3) the query trio `IsActive(kind)` / `ActionsBlocked` / `MoveSpeedMultiplier` on `StatusEffectController`; (4) the two zero-alloc chip queries `RemainingSeconds(kind)` / `Stacks(kind)` — an `IReadOnlyList<ActiveEffectView>` is REJECTED because `foreach` over the interface boxes an enumerator every `OnGUI` frame; (5) append-only enum ordering `Poison, Stun, Slow` with `Bleed` pinned at ordinal 0. Nothing beyond that — no ScriptableObject effect assets, no authoring window, no effect registry, no cleanse/resistance mechanic. `ActionsBlocked` (not `IsStunned`) is deliberate: stun blocks the action verbs, never movement.
- Why: the ticket's "just add effect data" framing is true for poison and false for stun and slow — the shipped `StatusEffectSpec.IsActive => damagePerSecond > 0f && durationSeconds > 0f` (verified at `Assets/Scripts/Runtime/Combat/StatusEffect.cs:44`) makes a correctly-authored zero-DPS stun a **silent no-op**, the worst class of bug. Naming the five gaps up front makes Devon extend the shipped shape instead of forking a parallel control-effect type, and the contract pin prevents the parallel-dispatch vocabulary divergence that makes sibling PRs non-mergeable.
- Reversibility: reversible (a struct-field append is serialization-safe; the enum is append-only so already-serialized specs — `BoarEnemy.goreBleed`, `SnakeEnemy.biteBleed`, the axe `OnHitStatus` — do not shift)
- Affects: Devon (`86cah7yuh` impl) · Drew (reviewer) · `StatusEffect.cs` / `StatusEffectController.cs` / `WasdMovement` / the `MeleeAttack.ShouldSwingOnClick` truth-table family. Source: `team/uma-ux/status-effect-readability-spec.md` §2 (G1–G5), PR #339 `e13a51e`.

## 2026-07-27 — Status composition: STRONGEST-WINS slow, sub-linear DoT stacking capped at 3, stun never stacks

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #339 (`e13a51e`)
- Decision: `StatusEffectController.Apply`'s deferred stack/refresh policy is now settled per kind. **Slow: STRONGEST WINS** — `MoveSpeedMultiplier` is the single *smallest* active `magnitude01`, then clamped to the tier floor; it is **never a product** (0.7 × 0.8 = 0.56 would fall straight through a 0.6 floor). **Bleed / poison: refresh-and-intensify, capped at 3 stacks** — duration = `max(remaining, new)`, magnitude scales sub-linearly at 1.0× / 1.6× / 2.0× of base DPS. **Stun: never stacks and never extends** (see the caps entry below). **Cross-kind: independent** — up to all four active at once. The floor and per-tier magnitude *values* are soak-tunable dials; the composition RULES above are not.
- Why: the shipped `Apply` adds an unbounded fresh instance per call and its own doc comment defers the policy to "a later ticket" — this is that ticket. A linear 3× DoT on hard's `damageTakenMul 1.35` is a stealth instant-death, and a multiplied slow drops below the floor that keeps the boar charge dodgeable — which would convert a fair telegraph into a trap and break the snake/boar fairness contract.
- Reversibility: reversible (policy is code + per-kind counters; success-tests pin it — 5× bleed ⇒ `Stacks(Bleed) == 3` at 2.0× base, two slows at 0.7/0.8 ⇒ `MoveSpeedMultiplier == 0.7` not 0.56)
- Affects: Devon (`86cah7yuh`) · `StatusEffectController` · boar/snake fairness contract. Source: `status-effect-readability-spec.md` §6.2/§6.3, PR #339 `e13a51e`.

## 2026-07-27 — Stun caps are hard invariants, not tunables (≤2.0 s, chain-immunity, no break-out input)

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #339 (`e13a51e`)
- Decision: Stun carries hard ceilings that no dial may exceed: **≤2.0 s at any tier** (easy ≤0.6 s, medium ≤1.2 s), a **chain-immunity window ≥ the stun's own duration** on every tier (easy ≥3.0 s) during which a re-applied stun is **dropped entirely** — not queued, not refreshed — and **no input may be required to break out** (no mash-free, no shake-off, no key prompt). Movement is never blocked; `ActionsBlocked` gates the action verbs only. A swallowed left-click is **DROPPED, never buffered**, and flashes the stun chip once so a dead click is never mistaken for a bug. Registry ids may tune durations *under* these ceilings. (Whether stun exists at all on the EASY tier remains an open Sponsor-soak question — `status_stun_enabled_easy`, §8 Q1 — and is NOT settled here.)
- Why: losing control is the scariest thing the game can do to a kid, and a chained stun is the single most rage-inducing mechanic in games — the shipped list-add `Apply` would happily stack stuns forever. The no-break-out-input rule is also Danish-layout-safe: `LootPrompt.BuildLabel` pins the literal-letter convention, so a stun is a *wait*, which is simultaneously the kid-friendly and the layout-safe choice.
- Reversibility: reversible for the values under the ceilings; the ceilings themselves are treated as invariants (raising one is a new Sponsor decision, not a dial)
- Affects: Devon (`86cah7yuh`) · `StatusEffectController` · the `ShouldSwingOnClick` / `ShouldChopOnClick` / `ShouldLootOnKey` / `MineOre` / `LeftClickConsume` truth-tables · `[[sponsor-danish-keyboard-layout]]`. Source: `status-effect-readability-spec.md` §5.2/§5.3, PR #339 `e13a51e`.

## 2026-07-27 — Slow needs no new locomotion code — `MoveSpeedMultiplier` only

- Decided by: Priya (PR #339 review verification, re-checked against source 2026-07-27)
- Decision: The slow effect ships with **no second speed source and no new Animator state** — `WasdMovement` multiplies its commanded speed by `MoveSpeedMultiplier` and nothing else is built. A slow reads as heavy legs for free.
- Why: verified in `Assets/Scripts/Runtime/CastawayCharacter.cs` — the Animator blend is already speed-driven (`SpeedParam = "Speed"` fed from the same `agent.velocity` magnitude `WasdMovement` commands, `:213`) and foot-sync scales clip playback by `actualSpeed / strideRef` (`:109-111`) inside a clamp band of `footSyncMulMin = 0.5f` … `footSyncMulMax = 2.5f` (`:87-88`). A 0.6× multiplier therefore flows through to both a slower Walk blend and a 0.6× stride cadence, comfortably inside the band — the legs neither freeze nor skate. An earlier spec draft carried this as a "verify at implementation" open item; it is retired.
- Reversibility: reversible (the finding is a code fact; if the band ever narrows, the item re-opens)
- Affects: Devon (`86cah7yuh` AC3 🔒 — do not add a second speed source) · `CastawayCharacter` / `WasdMovement`. Source: `status-effect-readability-spec.md` §6.1, PR #339 `e13a51e`.

## 2026-07-27 — Status chips are IMGUI on the shipped `SurvivalHud` path — not UI Toolkit

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #339 (`e13a51e`)
- Decision: Status-effect chips render as flat IMGUI (`GUI.DrawTexture` on `Texture2D.whiteTexture` + `GUI.Label` glyphs, explicit `Rect`s, no `GUILayout.*`) inside the existing `SurvivalHud.OnGUI` — 22 px chips, 4 fixed slots, a 2 px duration underline, 1–3 stack pips, no numbers and no countdown digits. This **corrects `combat-cluster-design-brief.md` §3.2's "UI Toolkit panel / UI Image" phrasing**. Chips bind the three zero-alloc scalar queries only — no `foreach`, no list, no enumerable in `OnGUI`. Whichever of `86cah7z2q` / `86cah7yuh` lands FIRST authors the minimal pip row and the other EXTENDS it; neither forks a second HUD renderer.
- Why: the live HUD is IMGUI, one HUD code path is the standing rule (`86caamkxv`), and standing up a second UI stack for four 22 px chips is unjustifiable. Pure IMGUI also never strips to magenta in the built exe — the reason the whole HUD is IMGUI in the first place.
- Reversibility: reversible (a future IMGUI→UI-Toolkit HUD migration remains a standing separate follow-up and would carry the chips with it)
- Affects: Devon + Uma + Drew (`86cah7yuh` / `86cah7z2q` — whichever lands first) · `SurvivalHud.cs`. Source: `status-effect-readability-spec.md` §3.1 + `hp-hud-polish-spec.md` §5, PR #339 `e13a51e`.

## 2026-07-27 — Status effects read on THREE colour-independent channels (silhouette / motion-speed / fixed slot)

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #339 (`e13a51e`)
- Decision: Every status effect must be identifiable on each of three channels **independently of hue**: chip **silhouette** (shape), **motion speed** (stun fastest, slow slowest, poison medium, bleed tick-synced), and a **fixed HUD slot per kind** — an inactive kind leaves its slot EMPTY, never packed, so "the third slot is lit" is itself the stun read. Colour is the third cue, never the first; text is a last-resort fallback in the `LootPrompt` cream-pill idiom ("Bleeding" / "Poisoned" / "Stunned" / "Slowed", nothing else). A cue that only works because of its colour is a failed cue.
- Why: the world is saturated mid-green and will happily eat a green cue; a colour-blind player must still read the corner; and 22 px chips at peripheral glance in a 1080p frame do not carry hue reliably. Positional constancy is the cheapest and most robust of the three channels and costs nothing to implement.
- Reversibility: reversible (shapes/motions are per-chip constants; the slot ORDER is the part that should not churn once players learn it)
- Affects: Devon + Uma (`86cah7yuh` / `86cah7z2q`) · `SurvivalHud` chip dock. Source: `status-effect-readability-spec.md` §3, PR #339 `e13a51e`.

## 2026-07-27 — DoT winces are debounced HUD-side; no source tag on `Health.ApplyDamage`

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #339 (`e13a51e`)
- Decision: The HP damage-wince is **debounced inside the HUD** — a minimum ~0.35 s between wince triggers, with every amplitude scaled by the fraction of `Max` lost — and the lost-segment flash is exempt only when a segment boundary is actually crossed. Adding a damage-source tag to `Health.ApplyDamage` was considered and **rejected** as the more invasive fix. This is a hard requirement of BOTH combat tickets: if the HP-HUD ticket has not landed, a poison implementation must not ship a per-tick screen pulse of its own.
- Why: bleed and poison tick through `Health.ApplyDamage` → `Health.Changed` (`StatusEffectController.TickSeconds`), so a per-`Changed` wince would strobe the vignette and row-nudge several times a second while a DoT runs — the single worst tonal failure available on this surface. A HUD debounce is contained; a framework source-tag touches every damage call site for the same outcome.
- Reversibility: reversible (one debounce constant, exposed as `hp_wince_debounce`)
- Affects: Uma + Drew (`86cah7z2q`) · Devon (`86cah7yuh` — inherits the requirement) · `SurvivalHud` / `Health` / `StatusEffectController`. Source: `hp-hud-polish-spec.md` §2.4 + `status-effect-readability-spec.md` §4.3, PR #339 `e13a51e`.

## 2026-07-27 — ONE low-HP threshold: the shipped `HpCriticalThreshold01 = 0.25f` (second registry id WITHDRAWN)

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #339 (`e13a51e`)
- Decision: The low-HP fraction is the shipped `SurvivalHud.HpCriticalThreshold01 = 0.25f` const (verified at `Assets/Scripts/Runtime/SurvivalHud.cs:152`). The proposed registry id `hp_low_warning_threshold` is **explicitly withdrawn — do not mint it**. One threshold, one home.
- Why: `86cah7z2q` AC2 🎚️ already pins reuse of the existing threshold rather than minting a second one, and an earlier spec revision proposed the second id in §7. Two thresholds for one concept is the dead-knob class in a new costume — a dial that appears live and silently disagrees with the const the draw path actually reads.
- Reversibility: reversible (one const; if it ever needs to be tunable, the const becomes the dial — never a parallel id)
- Affects: Uma + Drew (`86cah7z2q`) · `SettingsCatalog.PopulateHpHud` · `SurvivalHud`. Source: `hp-hud-polish-spec.md` §2.5/§7, PR #339 `e13a51e`.

## 2026-07-27 — The low-HP fail-state surface stays INSIDE the HUD (no sustained vignette, no post-process)

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #339 (`e13a51e`)
- Decision: The low-HP warning lives in the HUD, not on the screen. **Forbidden at every tier:** a sustained red/low-HP screen vignette, a screen-edge alarm, a desaturation or any post-process/lens pulse, a "LOW HEALTH" text card, a heartbeat SFX loop, a "YOU DIED" card, a letterbox, a slow-motion death, an on-screen deaths counter. **Permitted:** a screen-edge coal-red pulse ONLY as a *transient* damage beat (fully gone inside ~0.35 s, IMGUI edge strips, never a post-process Volume). **No hit-stop and no camera Impulse on INCOMING damage** — those are the reward punctuation for the player's own strike landing. This resolves the fail-state surface that `hud-three-bar-spec.md` §4 deferred. (The warning's pulse SHAPE and the specific amplitudes remain open Sponsor-soak dials — §8 Q4/Q5/Q7 — and are NOT settled here.)
- Why: freezing time or washing the screen when the player gets *hurt* reads as trauma and inverts the calm tone; a sustained vignette turns a wince into an alarm the player cannot dismiss. Full-screen post-process also costs a Render-Graph pass and is already ruled out by `game-juice.md` §2, while pure IMGUI never strips to magenta in the built exe.
- Reversibility: reversible for the permitted transient (amplitude constants + `hp_damage_vignette_peak`); the forbidden list is treated as a tone invariant
- Affects: Uma + Drew (`86cah7z2q`) · Devon (`86cah7yuh` — the same rule binds DoT ticks) · `SurvivalHud` / `game-juice.md` caps. Source: `hp-hud-polish-spec.md` §2.3/§2.5/§2.6, PR #339 `e13a51e`.

## 2026-07-27 — Prescribed-not-shipped: no particle system, no `_HitFlash`, no audio bus — the combat cluster builds the FIRST

- Decided by: Priya (spec-review verification, independently re-verified on `origin/main` 2026-07-27)
- Decision: Three "existing precedents" cited in earlier spec drafts **do not exist**, and the combat-cluster tickets must size for that. Verified counts on `main`: `ParticleSystem` / `ObjectPool` / `OnParticleSystemStopped` in `Assets/Scripts` = **0**; `_HitFlash` anywhere under `Assets` = **0**; `AudioSource` / `PlayOneShot` in `Assets/Scripts` = **0**; `.ogg`/`.wav`/`.mp3` under `Assets` = **0**. Consequences: (1) `game-juice.md` §1.4's pooling guidance is a **prescription, not a record of shipped code**, and the "berry-pop precedent" cited in earlier drafts does not exist — whichever of `86cah7z2q` / `86cah7yuh` lands first builds the project's FIRST pooled particle system (pool, material, prefab and all), which is materially more than "reuse the existing pattern"; (2) enemy hit-flash / flinch / dust are spec'd only and owned by the swings lane — if a HUD ticket lands before any body-level hit feedback, its enemy read is the ONLY enemy-damage feedback and must not ship disabled; (3) **every audio line in both specs is `<deferred — no audio bus>` and not authorable**, which supersedes `combat-cluster-design-brief.md` §3.3's "soft ascending chime" phrasing that read as shippable.
- Why: sizing an M-ticket against a precedent that does not exist is how a wave slips silently. Both specs' bar-side and IMGUI fallbacks were written precisely so readability is not hostage to that lift — the smaller slice ships the bar/chip language and defers the particles.
- Reversibility: n/a (a verified state-of-repo fact; the decision it drives — size for the first particle system, defer all audio — is reversible per ticket)
- Affects: Uma + Devon + Drew (`86cah7z2q` / `86cah7yuh` sizing) · orchestrator (wave sequencing) · `game-juice.md` readers. Source: `hp-hud-polish-spec.md` §1/§4/§6 + `status-effect-readability-spec.md` §1/§4.2, PR #339 `e13a51e`.

## 2026-07-27 — Policy growth beyond a pinned VOCABULARY CONTRACT lands as a ticket amendment, never a silent spec redefinition

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #339 (`e13a51e`)
- Decision: When a spec needs framework behaviour beyond what a ticket's pinned VOCABULARY CONTRACT already authorizes, it **requests a contract amendment on the ticket** rather than redefining the surface in prose. The ticket contract wins over the spec wherever the two diverge, and a divergence caught at review is **REQUEST_CHANGES, not a NIT**. The two amendments this wave needed are **already APPLIED**, not pending: `86cah7yuh`'s 2026-07-27 amendment block carries **A1** (the stun chain-immunity window, one per-kind last-expiry timestamp) and **A2** (the per-kind stacking policy plus `Stacks(kind)`) — verbatim, *"Requested by `status-effect-readability-spec.md` §2.1 and **granted here**, so the extension surface is not silently larger than the contract"* — and the ticket's success-tests name both. Nothing from this wave is left outstanding.
- Why: parallel dispatches against a shared concept only stay mergeable while one authority names the identifiers; a spec that quietly grows the surface reintroduces exactly the vocabulary divergence the contract exists to prevent. Both amendments here are bounded (one timestamp + one count per kind) and neither adds a type — cheap to authorize, expensive to discover mid-implementation. The round that granted them also reconciled AC3's medium slow default 0.6 → 0.7 (0.6 stays the hard floor) so one number ships, which is the same "ship ONE value, never two" discipline applied at the ticket layer.
- Reversibility: reversible (process rule; amendments are ticket edits)
- Affects: Devon + Drew (`86cah7yuh` implements against the amended contract; review posture) · Priya (contract-amendment authoring) · every parallel-dispatch brief. Source: `status-effect-readability-spec.md` §2 (🔒 VOCABULARY AUTHORITY) + §2.1, PR #339 `e13a51e`; amendments read from `86cah7yuh`'s body 2026-07-27.

## 2026-07-27 — Heavy attack needs a delayed-impact seam (`heavyWindupSeconds`); the light path stays synchronous

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #340 (`aa10fa5`)
- Decision: The heavy attack requires a **delayed-impact seam on `MeleeAttack` that does not exist today**. The new field is **`heavyWindupSeconds`** on `MeleeAttack` — deliberately NOT the resource verbs' `swingImpactDelaySeconds` — and target resolution moves to the impact frame **for the heavy only**; the light path keeps its synchronous damage (soaked, shipped behaviour). Companion normalized read: `HeavyWindupNormT`.
- Why: verified code fact — combat damage is applied synchronously in the click frame (`Assets/Scripts/Runtime/Combat/MeleeAttack.cs:229`, `target.ApplyDamage(...)` inside `PerformAttack`), while `swingImpactDelaySeconds` is **declared only on the three resource verbs** (`ChopTree.cs:256`, `MineBoulder.cs:127`, `MineOre.cs:122`) and **declared nowhere on the combat path** — `MeleeAttack.cs` carries zero references. The count split, stated so it is not mis-cited later: **3 declaration sites**; **10 files under `Assets/`** mention the identifier (the 3 declarations + 5 test files + 2 serialized scenes); **16 tracked files repo-wide** once the team specs and this log are counted. Reusing the resource-verb field name across the combat path would blur two different timing models on one identifier. **No soak qualifier needed** — this is a build-shaping code fact, not a feel call.
- Reversibility: reversible (one field + an impact-frame branch on the heavy path only)
- Affects: `86cau6prr` (impl) · `MeleeAttack.cs` · Drew/Devon. Source: `team/uma-ux/heavy-attack-input-model-spec.md` §4.4/§12, PR #340 `aa10fa5`.

## 2026-07-27 — Heavy attack RE-WIRES the dormant reserved `Attack` state (`CastawayMelee`) — never a sixth state

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #340 (`aa10fa5`)
- Decision: The reserved heavy clip is **`CastawayMelee` ← `Melee_Attack.fbx`**, DISTINCT from the axe light **`CastawayAxeSwing` ← `Attack_Axe.fbx`**. Its Animator state **already exists and is dormant**, so the implementation **re-wires that reserved state** with one `AnyState→Attack` transition on (`Chop` && `WeaponClass == 5`) rather than adding a sixth state. **Do NOT call `WireAttackClass` as-is** — its first line adds a state, which would create a *new* state and strand the reserved one; either add the `AnyStateTransition` directly or extend the helper to accept an existing `AnimatorState`. Renaming the reserved state for symmetry is acceptable; a second state is not. The clip is **GENERIC, not Humanoid** (`Melee_Attack.fbx.meta:130` and `castaway_v4_rigged.fbx.meta:101` both `animationType: 2`) and is already imported and bound against the v4 rig — what is dormant is the incoming transition, not the binding.
- Why: verified against `Assets/Scripts/Editor/CharacterAssetGen.cs` (`:77`/`:83` fbx paths, `:248`/`:253` clip consts, the reserved-state comment at `:1374`). Setting the clip to Humanoid is the explode-to-a-cone trap on this scaled hierarchy (`86ca8rdkp`), and `86cau6prr`'s body said "Humanoid" until 2026-07-27 — corrected in the ticket during this spec's review round. **No soak qualifier needed.** Also corrected in the same round: the no-orphan-anim-id invariant lives in `Assets/Tests/EditMode/AttackSwingControllerTests.cs` assertion 6, **not** in `WeaponSetTests.cs` (verified: `WeaponSetTests` contains zero `AnimationId` references).
- Reversibility: reversible (Animator wiring is generated by `CharacterAssetGen`; a dedicated cleave clip stays the post-soak escape route)
- Affects: `86cau6prr` (impl) · `CharacterAssetGen.cs` / `CastawayCharacter.cs` / `AttackSwingControllerTests.cs` · `[[chop-swing-mixamo-clip-not-procedural]]` · `[[castaway-v4-blocky-handmodel-passed-lookdev]]`. Source: `heavy-attack-input-model-spec.md` §2/§7.2/§12, PR #340 `aa10fa5`.

## 2026-07-27 — Heavy-attack roster expansion is a DATA seam, and the heavy is combat-only

- Decided by: Uma (spec author) — ratified via Priya's spec review + merge of PR #340 (`aa10fa5`)
- Decision: Giving another weapon a heavy is **data plus one Animator state — never a new input path**: an optional `WeaponDef.HeavyAnimationId` (null = no heavy, and `heavyCapableWeaponSelected` is exactly `HeavyAnimationId != null`), one `AnimId*` const, one row in `WeaponClassForAnimationId` → a new `WeaponClass` int, one `SwingSpeedForClass` case, one Animator state. Zero input changes, zero guard changes, zero timing-model changes — that is the test of whether the model generalized. No new Animator layer, no AvatarMask, no second trigger system, no procedural swing. **And the heavy is combat-only:** it resolves targets exclusively through `ResolveNearestTarget(weapon.Reach)` over `Health` components and never touches `ChopTree` / `MineBoulder` / `MineOre` — state that as an explicit non-goal in the impl PR body. (Which weapons get a heavy and in what order is an open Sponsor-soak question — §8.9 — and is NOT settled here.)
- Why: the dedicated heavy key bypasses `verbClaimedClick`, so an axe-holding player next to a tree could press it and — if the heavy were routed through the verb layer — chop the tree with a combat swing, double-yielding wood or double-damaging a boar depending on precedence. It ships correct and regresses the moment an axe heavy lands, so the non-goal has to be written down. Keeping expansion in data is what stops each new weapon from re-opening the input analysis `heavy-attack-input-spec.md` already settled.
- Reversibility: reversible (data rows + consts)
- Affects: `86cau6prr` (impl) · `86cah7ym9` (roster) · `WeaponDef` / `WeaponCatalog` / `CastawayCharacter` / `MeleeAttack`. Source: `heavy-attack-input-model-spec.md` §7.2/§7.3/§12, PR #340 `aa10fa5`.

## 2026-07-22 — Weapon held-seat: ONE dial per weapon class across all tiers (stone-axe zero-lock RETIRED)

- Decided by: Sponsor (verbatim "use the same dial for rock and metal", soak-swings-6 final F9 dial; baked by Drew in PR #327 round 7)
- Decision: Each weapon CLASS (axe / pickaxe / spear / sword / dagger) uses ONE dialed held-seat (offset/euler/scale) applied identically to its wood/stone/iron tiers — per-tier seat divergence is retired. The original stone axe's zero-locked index-0 seat (ApplyCurrent restored the captured baseline byte-unchanged) is RETIRED: `HeldWeaponCycleDebug.ApplyCurrent` now composes index-0's array seat like every other weapon (backward-compatible with the old zero array), and `Awake` seats the equipped chop axe at spawn. The axe SCALE stays 1.0 (the held-scale dial still refuses the axe); `HeldAxeV3/V4` rig constants are UNTOUCHED (the class euler composes on the approved rig baseline).
- Why: the Sponsor wanted all material tiers of a weapon to read IDENTICALLY in-hand; one per-class seat is simpler + drift-guarded, and the equipped stone chop-axe now matches the approved wood-axe look.
- Reversibility: reversible (seat constants + the ApplyCurrent composition are code; drift-guarded by `HeroAxeSceneTests._SameDialAcrossTiers_86caffwv5` + per-tier equality pins + the axe scale-1.0 guard)
- Affects: `HeldWeaponCycleDebug` / `HeroAxeSceneTests`, held-weapon visual seating; Drew + Devon + Tess. Source: PR #327 (86caffwv5) r7, comment 5034253841.

## 2026-07-22 — Boar soak PASS → PR #332 merged; 2nd-enemy mesh route ratified (snake-style C#-baked)

- Decided by: Sponsor (soak popup, 2026-07-22 afternoon)
- Decision: Boar soak on `soak-boar-1` (stamp `9f76ec7`) PASSED in full — charge feel, emergent spear-beats-boar matchup legibility, AND the look; #332 merged as `0dc4844`, ticket 86cah7ydt complete. The AC5 route deviation is RATIFIED: 2nd-enemy meshes follow the snake's C#-editor-baked + procedurally-posed route (no rig — sidesteps the FBX-helicopter class); Blender-authored silhouettes remain optional swap-tickets (the swap-hatch is Devon-verified drop-in).
- Why: the systemic matchup proof (spear 18.0/hit vs axe 10.5/hit purely from reach + pierce-tag composition, zero table — guard test deletes the tag and the bonus vanishes) is the locked-decision-5 payoff; the route deviation is precedent-consistent (snake) and avoids the just-proven Blender round-trip trap. Sponsor's eye confirmed what the metrics could not (matchup LEGIBILITY).
- Reversibility: reversible (mesh swap-hatch; dials all per-tier tunable)
- Affects: combat cluster (3rd-enemy tickets inherit the route), asset-routing doc (creature-route footnote → Priya batch), quality-bars.md (matchup-legibility bar → Priya appends)

## 2026-07-22 — v4 right-hand fix: defer again + Option-C feasibility spike; never Blender-re-export the rigged character

- Decided by: Sponsor (popup, 2026-07-22 midday)
- Decision: (1) PR #330 merges as a safety PR (byte-revert + FBX v7700 canary + raw-parse instrument) on Devon's byte-identity verification — no re-soak needed at net-zero visual diff. (2) The right-hand defect stays DEFERRED (second deferral); a research-lane spike proves/kills Option C (raw-FBX binary weight edit, no hierarchy re-export) before any fix route is chosen; Option B (Mixamo re-rig) explicitly not chosen now. (3) Standing engineering rule from the incident: NEVER re-export an already-rigged Mixamo character through Blender's FBX exporter — it rebakes rest orientations on most bones (33/42 measured) and helicopters all zero-rest Generic clips; clip-layer bpy edits remain OK; enforcement = the v7700 canary test.
- Why: Option A proved structurally impossible (PR #330 comment 5044931437); Option B discards the accepted left-hand dial and re-rolls the defect-producing auto-rig; the defect is cosmetic and already Sponsor-accepted once ("ill fix the hands later", 2026-07-20). Evidence-first spike beats gambling the build lane.
- Reversibility: reversible (spike is throwaway; routes stay open)
- Affects: Drew/Devon (character pipeline), 86cau4za2 (back to `to do` post-merge), boar sequencing (unblocked — build lane frees at #330 merge)

## 2026-07-22 — Swings cluster ships: mini-soak-8 PASS → PR #327 merged (re-appended after the 08:09 revert)

- Decided by: Sponsor
- Decision: Mini-soak of `Build/soak-swings-8` (stamp `58ae23d`) PASSED — walk-into-boulder blocks at touching distance and click-mine fires from the blocked spot; #327 merged as `250e4e6`; tickets 86caffwv5 + 86caffwuz complete.
- Why: The r8 carve-tighten resolved the only soak-7 reject (blocked a body-length out); all machine gates were already green (CI green after capture-job rerun — the 2026-07-21 failure was a runner shutdown signal mid-job, not code; Devon APPROVE_WITH_NITS r7; Tess QA PASS + SERVE GO incl. the r8 played-check, comment 5042990009).
- Reversibility: reversible (revert PR #327)
- Affects: Drew/Devon/Tess (swings surface); board (cluster #2 boar unblocked; round-9 right-hand investigation done — fix pending Sponsor sequencing)

## 2026-07-21 — Erik Rigify verdict accepted: STAY Mixamo (reconstructed 2026-07-22)

- Decided by: Sponsor (implicit accept — no pushback on the served verdict)
- Decision: stay on the Mixamo pipeline; bad clips get clip-layer bpy repair (à la SneakGaitCurveFix); Rigify re-enters only if a human animator ever joins (open question to Sponsor, unanswered).
- Why: Rigify is a control rig with zero clips; game-export friction + re-rig blast radius (Generic bindings, Animator, all 15 seats) = Very High for zero gain. Research note: team/erik-consult/rigify-vs-mixamo-research.md (merged #328).
- Reversibility: reversible (future re-evaluation)
- Affects: animation asset routing
- Provenance: re-appended 2026-07-22 after the 08:09:34Z working-tree revert wiped the original uncommitted entry; sources = 2026-07-21 session save + PR #327 trail.

## 2026-07-21 — Loot prompt anchor: above the character's head (reconstructed 2026-07-22)

- Decided by: Sponsor (confirmed the orchestrator's delegated recommendation)
- Decision: interaction/loot prompts render above the castaway's head (tier-aware ore text; the belt-overlapping prompt relocated).
- Why: readability at gameplay framing; the old anchor collided with the belt UI.
- Reversibility: reversible
- Affects: LootPrompt / HUD
- Provenance: re-appended 2026-07-22 after the 08:09:34Z working-tree revert; sources = 2026-07-21 session save + PR #327 trail.

## 2026-07-21 — Ore spec KEPT: wood pickaxe mines boulders only (reconstructed 2026-07-22)

- Decided by: Sponsor ("Keep spec" popup, 2026-07-21)
- Decision: wood pickaxe mines BOULDERS only; iron ore requires a stone/iron pickaxe; the tier-aware tooltip "Needs stone pickaxe" is the UX cue.
- Why: progression legibility — the tooltip carries the teaching; the shipped spec matched his intent.
- Reversibility: reversible (spec/dial change)
- Affects: mining progression, LootPrompt copy
- Provenance: re-appended 2026-07-22 after the 08:09:34Z working-tree revert wiped the original uncommitted entry; sources = 2026-07-21 session save + PR #327 trail.

## 2026-07-19 — Combat cluster: SPEC PREP starts now; implementation gated on #317 (v4 activation) merge

- Decided by: Sponsor (orchestrator popup, 2026-07-19)
- Decision: Combat-cluster **spec prep begins immediately** (AC-flesh, design brief, sequencing) while `#317` (castaway v4 activation) finishes; **implementation of every combat ticket still waits for `#317`'s merge**. The six cluster tickets — swings `86caffwv5`, boar `86cah7ydt`, find-in-world weapons `86cah7y5b`, weapon-roster expansion `86cah7ym9`, additional status effects `86cah7yuh`, HP-HUD polish + heal sources `86cah7z2q` — stay `to do` (prep ≠ implementation) and each carries a `#317-merge` implementation gate.
- Why: v4 is the live hero the combat verbs animate on (swings play on the castaway animator); prepping specs in parallel keeps the non-build lane full without dispatching impl against a hero that is mid-activation. Splitting prep from impl lets the design settle before code starts.
- Reversibility: reversible (prep is docs/ACs only; no code committed until #317 merges)
- Affects: combat cluster (6 tickets), Devon + Drew + Uma + Tess; sequencing per the sibling decision below

## 2026-07-19 — Combat cluster order: SWINGS first, boar second

- Decided by: Sponsor (orchestrator popup, 2026-07-19)
- Decision: The combat cluster is sequenced **swings first** (`86caffwv5` — attack animation per weapon: a Mixamo clip per weapon class, one-click-one-strike active input) **then the wild boar** (`86cah7ydt` — 2nd enemy + weapon-vs-mob matchup proof). Recorded on both tickets.
- Why: a weapon that reads as a real attack is the foundation the enemy matchup builds on — the boar's "spear beats boar via reach + weak-to-pierce" proof only lands once the swings feel like real attacks. Sponsor-picked order.
- Reversibility: reversible (sequencing only)
- Affects: combat cluster dispatch order, Drew + Devon

## 2026-07-08 — Crafting system redesigned: placed recipe-menu table + 3 tiers + unified place-to-build

- Decided by: Sponsor + orchestrator (grill-resolved, ticket `86camz6n0`, 2026-07-08; grounded on the forge-soak feedback in `86camyvzw`/`86camyvwn`)
- Decision: The crafting surface is redesigned. (1) **Unified place-to-build**: the crafting table (wood+stone), forge (much more stone), and campfire are all placed by the player and are **INVISIBLE until placed** (retires the pre-visible fixed spots). (2) **Crafting table = a recipe MENU** — recipes grouped by tier, greyed until the tier is unlocked AND affordable, click-to-craft; RETIRES the `CraftSpot` auto-craft stump + the free-mint `CraftAxe` path. (3) **Three tiers WOOD→STONE→IRON**, each with axe/pickaxe/spear/dagger/sword (~15 recipes) crafted via a **material-cost seam** (`InventoryModel.RemoveItem` all-or-nothing → `AddToolToBelt`). (4) **Tier-gated loop**: hand-gather sticks+pebbles → table → wood tools → wood-pick mines STONE from boulders (NEW) → stone tools → stone-pick mines IRON-ORE (shipped #287) → forge smelts → BARS (shipped #292) → iron tools. Re-scoped into 4 build-lane tickets (① table foundation + wood tier, ② boulder-mining + stone tier, ③ forge place-to-build rework + iron tier, ④ full-chain soak). Absorbs I-4 `86cakkmy2` + forge-vis `86camyvzw` + NIT `86camw8rm` (→③) and I-5 `86cakkn15` (→④); icons `86camyvwn` stays a separate fable session. Full spec: `team/priya-pl/crafting-system-spec.md`.
- Why: The Sponsor's forge-soak (2026-07-08, build `4cb464b`) rejected pre-visible/auto-built structures ("must NOT be visible before it is built — the player builds it by gathering the ingredients and PLACING it"); the grill generalised that to every structure + turned the thin one-recipe stump into a real tiered recipe menu, giving the survival arc its full gather→craft→upgrade spine. Model-A's shipped mine/smelt mechanics are reused; its "extend the thin CraftSpot bench" assumption is superseded.
- Reversibility: reversible per ticket (each of ①–④ is a discrete PR); the design direction is Sponsor-locked
- Affects: crafting/inventory/structures/UI, Devon + Drew + Tess + Uma; supersedes `iron-model-a-spec.md` on the crafting-table question

---

## 2026-06-12 — Project founded: Far Horizon (Sponsor-directed)

- Decided by: Sponsor (sequence of popup decisions, recorded verbatim on RandomGame ClickUp ticket 86ca85ttd)
- Decision: The Unity production project is **Far Horizon** — a FRESH Unity 6/URP project (the eval spike stays a read-only reference, not graduated), new private GitHub repo `TSandvaer/Far-Horizon`, new ClickUp list `901523878268`; milestones split M-U1 (bootstrap + deliberate ports) / M-U2 (thin survival loop: ONE need → craft axe → chop → campfire); PixelLab subscription kept idle; the Godot repo/list archive read-only.
- Why: Engine decision 2026-06-12 (migrate to Unity — RandomGame DECISIONS.md; evidence: spike verdict YES on all capabilities + all style gates passed: character "appealing", "i love zone D + quality", "zone c approved"). Fresh-over-graduate and the name were the Sponsor's explicit picks.
- Reversibility: one-way in practice
- Affects: everything — repo, tracker, roadmap, all roles

## 2026-06-12 — Bootstrap exception: U1/U2 land direct on main

- Decided by: orchestrator
- Decision: The U1 Unity skeleton (root commit 3a6ef5c) and the U2 orchestration scaffold commit straight to `main`; PR-flow + protected-main discipline is binding from U3 onward.
- Why: An empty repo has no main to branch from and no reviewers' worktrees yet; both commits are recorded on their tickets (86ca86fb7 / 86ca86fgy) with full evidence.
- Reversibility: reversible (convention forward-looking)
- Affects: git protocol, all roles

## 2026-06-12 — WARMTH is the single M-U2 survival need

- Decided by: Sponsor (orchestrator popup, recorded on ticket 86ca8bd9m / U2-1)
- Decision: The one decaying need that drives the thin M-U2 loop is **WARMTH** — cold creeps in; the campfire (U2-4) is what answers it. No second need, no hunger/energy, no shelter (those are M-U3+ proposals).
- Why: Fits the castaway-washed-ashore fiction (wet, cold, one pressing need) and keeps M-U2 thin per the Sponsor's locked one-need-→-craft-axe-→-chop-→-campfire loop. Shipped: WarmthNeed model (PR #11), campfire satisfaction (PR #15), full-cycle PlayMode coverage (PR #16).
- Reversibility: reversible (the single-need model generalizes to two needs in M-U3)
- Affects: survival loop, HUD, all M-U2 content tickets

## 2026-06-12 — Art-direction board rebased to chunky cartoon low-poly (whole game)

- Decided by: Sponsor (in-chat, evening — "throwing a lot of stuff in the inspiration folder, deleted the old genre"; captured in art-direction.md board v2 + Uma's style-guide-v2 PR #17)
- Decision: The entire art-direction board is REBASED to **chunky stylized cartoon low-poly** across all three surfaces — character, tools/props, world/nature. The 2026-06-08 lush-garden/courtyard references are deleted. On the castaway specifically the change is **STYLE ONLY**: the chunky/cartoon stylization transfers to the LOCKED young/hopeful identity (the reference's bearded rugged adult is NOT adopted; `_castaway_judge/` sheets remain the identity ground truth).
- Why: Sponsor replaced the whole inspiration board with a coherent toy-like, saturated, faceted-flat-shaded direction; the warm/lush FEELING and small-player/big-alive-world north-star carry, only the rendering style shifts. Drove the style wave (axe PR #21, blob trees PR #22) and the castaway stylization ticket 86ca8ca1m.
- Reversibility: one-way in practice (board content replaced; downstream assets re-skinned)
- Affects: all visual/level/prop/palette work, Uma + Drew + Devon

## 2026-06-12 — Vertex-color inline-materials pattern for multi-colored low-poly props

- Decided by: Drew (blob-canopy tree implementation, ticket 86ca8ce7j / PR #22)
- Decision: Multi-colored low-poly props bake their per-region colors (e.g. the blob canopy's CanopyShadow/Body/Top) into **vertex color** and render through ONE shared custom `FarHorizon/LowPolyVertexColor` material rather than multiple per-color `.mat` assets; materials are assigned to `sharedMaterial` and serialized into the scene, NOT persisted as standalone asset files.
- Why: URP/Lit ignores vertex color, and per-instance color-jittered `.mat` assets cause asset churn (unity-conventions.md low-poly section). One vertex-color material keeps the faceted multi-value look while avoiding the churn; falls back to flat green if the shader is unresolved. Proven in LowPolyZoneGen.cs (canopy) and the terrain beach→field ramp.
- Reversibility: reversible (rendering convention; swappable per-prop)
- Affects: world/prop content systems, Drew

## 2026-06-13 — Castaway base SWAPPED to a sourced chunky-cartoon rig (Mini Chibi Kid)

- Decided by: Sponsor (2026-06-13, after the cartoon-ify attempts failed; recorded on ticket 86ca8ca1m + STATE.md + decisions-while-away.md)
- Decision: Stop editing the realistic Quaternius mesh and **SOURCE a pre-rigged chunky-cartoon base**. Chosen: Sketchfab **"Mini Chibi Kid" by joaobaltieri** (UID `6feb5bd7ade54b5fac25a0e1e5fbe729`, **CC-BY**), integrated as PR #26 (branch `devon/chibi-castaway-integration`). It ships its own Idle/Walk/Run animations on a Mixamo-style humanoid rig, the cartoon face the Sponsor asked for (white-sclera/black-pupil eyes + bushy brows), young/hopeful, ~1442 faces. PR #25 (Quaternius bone-scale path) is kept open as fallback until the chibi is proven.
- Why: A whole evening of cartoon-ifying the realistic head failed — vertex sculpt mangled arms/hands; bone-scale gave a clean chunky body but the cartoon face could not be sculpted onto a socket-set realistic skull. Lesson (now in unity-conventions.md): **when a base mesh fights the target style after 2+ edit attempts, stop editing and source a purpose-built base** — vet for license (prefer CC-BY, avoid CC-NonCommercial for a potentially-commercial game), low face count, and critically whether it ships its own animation set.
- Reversibility: reversible (revert the squash merge / iterate via recolor or base-swap; PR #25 fallback retained)
- Affects: player character, Devon + Uma, animation/rig pipeline

## 2026-06-13 — PR-merge to protected `main` is NOT orchestrator-auto-decidable (always Sponsor-gated)

- Decided by: auto-mode classifier boundary (recorded in decisions-while-away.md, 2026-06-13 0620 UTC)
- Decision: Merging a PR to the protected `main` branch is **always Sponsor-gated** on this project, regardless of auto-mode / orchestrator-autonomy state. The promoted "routine-PR-merge when CI green + peer reviewer attached" auto-decide class does NOT apply here — the classifier denies it as an externally-visible action the Sponsor never explicitly approved.
- Why: The orchestrator tried to auto-merge PR #26 (CI green, Tess APPROVE with independent shipped-exe reproduction) under the routine-merge class; the auto-mode classifier blocked it. Correct boundary — not retried. The look-verdict (post-merge soak) is the Sponsor's gate, so the merge itself must be Sponsor-approved.
- Reversibility: reversible (governance convention; reaffirmable per the never-auto-decide externally-visible-action rule)
- Affects: orchestrator, git protocol, all merge flows

## 2026-06-13 — Axe sourced as a CC-BY hatchet (procedural axe didn't read as an axe)

- Decided by: Sponsor (2026-06-13 — "the axe does not look like an axe"; recorded in STATE.md)
- Decision: The procedural hero axe (PR #21) didn't read as an axe, so the Sponsor chose to **source one**. Sourced + committed: Sketchfab **"One-handed stylized axe" by Viktor.G** (UID `d2e3f8682d71425ba2bf72f3e3d78f7c`, **CC-BY**) — a rustic leather-wrapped hatchet that reads unmistakably as an axe (branch `orch/castaway-axe-asset` @ `79b903b`, `Assets/Art/Props/CastawayAxe/`). Integration (replace the procedural axe + attach to the chibi's hand bone) is a separate PR **sequenced AFTER the chibi (PR #26) lands**.
- Why: The procedural prop read ambiguously; a sourced chunky-cartoon hatchet matches the style-guide tool language (ref 21h08_08) and reads correctly. Sequenced after the chibi because the axe attaches to the chibi's hand bone.
- Reversibility: reversible (asset swap; integration not yet merged)
- Affects: survival loop hero prop, Devon, asset pipeline

## 2026-06-13 — Castaway identity recolor (sandy hair / khaki) is a tunable soak follow-up

- Decided by: Sponsor (recorded on ticket 86ca8ca1m + STATE.md)
- Decision: The chibi ships with its default look now; the young/hopeful identity recolor (sandy hair, khaki) is a **deliberate tunable follow-up judged from the soak**, not a blocker on the base swap landing.
- Why: Decouples the structural base-swap decision (proportions + rig + animations) from the subjective identity-recolor tuning, which is best judged against the shipped-build soak rather than pre-specified.
- Reversibility: reversible (recolor is a tuning pass)
- Affects: player character, Uma + Devon

## 2026-06-13 — Asset-sourcing/creation route: Sketchfab + Blender-MCP; AI generators need Sponsor keys

- Decided by: Sponsor (Blender-MCP capability flagged 2026-06-12; AI-gen "Both" 2026-06-13; recorded in CLAUDE.md, unity-conventions.md, STATE.md)
- Decision: The asset-sourcing/creation route for Far Horizon is **Blender + Blender MCP** — Sketchfab search/import (sourcing existing assets) and procedural Blender modeling. The AI text/image-to-3D generators (Hyper3D Rodin, Hunyuan3D) are available and enabled in Blender but **require the Sponsor to supply API keys** (Rodin MAIN_SITE mode needs a key; Hunyuan3D needs a Tencent secret pair) — keys PENDING. Sketchfab works with just an account API key (already set).
- Why: Sketchfab + procedural Blender cover the immediate need (chibi base, hatchet, world props); the AI generators are a future lever gated on Sponsor-supplied keys. PixelLab is explicitly OFF Far Horizon's books — the Sponsor uses that subscription for other projects ("im using pixellab for other projects, dont worry about it", 2026-06-12); pixel-art-native and ruled out for this game's 3D characters/world.
- Reversibility: reversible (tooling route; generators enable once keys arrive)
- Affects: all asset creation, orchestrator R&D lane, Devon + Drew + Uma

## 2026-06-13 — Castaway BASE SWAP completed + recolored to identity (chibi shipped)

- Decided by: Sponsor (base choice) + Devon/Uma (integration + recolor execution)
- Decision: The castaway base swap is COMPLETE. The cartoon-face-on-realistic-Quaternius-head route FAILED (a whole evening of head-sculpt/bone-scale attempts couldn't put a cartoon face on a socket-set realistic skull). Sponsor sourced a pre-rigged chunky-cartoon base — Sketchfab **"Mini Chibi Kid"** (CC-BY) — integrated via PR #26 (squash `9dd317f`), then recolored to our young/hopeful identity (sandy hair, warm khaki) via PR #32 (squash `46f2a9d`, combined scene-integration PR). The recolor is a **luma-preserving UV-cell atlas PNG repaint** (the bound `_BaseMap` PNG bytes change; materials/import config unchanged) — explicitly NOT a material tint.
- Why: Lesson (now in unity-conventions.md): when a base mesh fights the target style after 2+ edit attempts, stop editing and source a purpose-built base — vet for license (prefer CC-BY), low face count, and critically whether it ships its own animations. Mini Chibi Kid ships its own Idle/Walk/Run on a Mixamo-style humanoid rig, which is why it won over re-sculpting. Atlas-repaint over material-tint preserves the toon's per-cell luma shading.
- Reversibility: reversible (revert the squash merges / re-repaint the atlas in ≤1 PR; PR #25 Quaternius fallback was closed superseded)
- Affects: player character, Devon + Uma, animation/rig + recolor pipeline

## 2026-06-13 — Axe re-done as a sourced rustic hatchet (procedural axe didn't read)

- Decided by: Sponsor (base choice) + Devon (integration)
- Decision: The procedural hero axe (PR #21) didn't read as an axe, so it was replaced with the sourced Sketchfab **"One-handed stylized axe" by Viktor.G** (CC-BY) — a rustic leather-wrapped hatchet — integrated and attached to the chibi's right-hand bone (`RightHand_010`) via PR #29 (squash `3f3a3b6`). The procedural `HeroAxeMesh` path was retired (deleted with its tests).
- Why: The procedural prop read ambiguously; a sourced chunky-cartoon hatchet matches the style-guide tool language and reads correctly. Sequenced after the chibi (PR #26) because the axe attaches to the chibi's hand bone. Scale-trap (a 267× lossy-scale giant-axe) was caught and fixed to ~0.43u on the ~0.95u kid before merge.
- Reversibility: reversible (asset swap in ≤1 PR)
- Affects: survival-loop hero prop, Devon, asset pipeline

## 2026-06-13 — M-U3 REDIRECTED: survival-mechanic → SCENE COMPLETION ("finish the scene, water at the beach")

- Decided by: Sponsor (verbatim 2026-06-13: "finish the scene, i want water at the beach")
- Decision: M-U3 is redirected away from the next survival mechanic (second need / food / day-night, per survival-roadmap §3) toward **SCENE COMPLETION** — finishing the shore scene so it reads as a beach, not a clearing. First beat shipped: a stylized low-poly beach ocean brought into the SHIPPED soak scene (`MovementCameraScene`/Boot.unity) — Uma's direction PR #28 (`b78da67`), implemented + integrated via PR #32 (`46f2a9d`, regenerated Boot.unity). PRs #30 (beach ocean) and #31 (castaway recolor) were CLOSED superseded — their work landed combined in #32.
- Why: The castaway washed ashore but the scene had no coast (flat warm ground, blob trees, no water) — it read as "a clearing," not "a beach." Adding water completes the washed-ashore premise and makes the small-player/big-alive-world north-star visible in one frame. The Sponsor's redirect takes priority over the roadmap's next-mechanic default.
- Reversibility: reversible (milestone scoping; the survival-mechanic roadmap items remain queued behind scene completion)
- Affects: M-U3 milestone scope, the shipped scene, Uma + Drew + Devon, the board

## 2026-06-13 — `main` merges are Sponsor-gated (governance CONFIRMED)

- Decided by: auto-mode classifier boundary, then Sponsor (explicit batch approval)
- Decision: Merging any PR to protected `main` is **always Sponsor-gated** on this project, regardless of auto-mode / orchestrator-autonomy state — the promoted "routine-PR-merge when CI green + peer reviewer attached" auto-decide class does NOT apply here. CONFIRMED in practice: the orchestrator's attempt to auto-merge PR #26 was correctly denied by the auto-mode classifier (not retried); the Sponsor then explicitly approved the castaway/scene-completion batch (#26/#28/#29/#32), which merged with `--admin --squash`.
- Why: A `main` merge is an externally-visible action the Sponsor never blanket-approved, and the look-verdict (post-merge soak) is the Sponsor's gate — so the merge itself must be Sponsor-approved. (Supersedes/reaffirms the 2026-06-13 governance note above; recorded with the batch-approval outcome.)
- Reversibility: reversible (governance convention; reaffirmable per the never-auto-decide externally-visible-action rule)
- Affects: orchestrator, git protocol, all merge flows

## 2026-06-13 — AI-gen held in reserve; Sketchfab is the default free asset route

- Decided by: Sponsor (asset-route confirmation through the castaway/axe wave)
- Decision: The default asset-sourcing route is **Sketchfab search/import** (free, account-key only) — proven through the chibi base + hatchet this wave. The AI image/text-to-3D generator **Hyper3D (Rodin)** is held IN RESERVE behind a **$96/mo Business-API gate** (MAIN_SITE mode needs a paid key the Sponsor hasn't supplied); do not assume it as a route until the Sponsor opts into that cost.
- Why: Sketchfab covers the immediate need at zero marginal cost; the paid AI generator is a future lever, not a baseline. Keeps the asset pipeline free-by-default and the cost decision explicitly the Sponsor's. (Refines the 2026-06-13 asset-route decision with the cost-gate specifics surfaced this wave.)
- Reversibility: reversible (route preference; Hyper3D enables once the Sponsor supplies the Business-API key)
- Affects: all asset creation, orchestrator R&D lane, Devon + Drew + Uma

## 2026-06-13 — M-U2 loop-feel verdict = FUN → M-U3 unblocked

- Decided by: Sponsor (loop-soak verdict)
- Decision: The M-U2 thin survival loop (one need → craft axe → chop → campfire) soaked as **FUN** per the Sponsor. That verdict was THE gate on starting M-U3; with it given, M-U3 (redirected to scene completion — see above) is unblocked.
- Why: The thin-first loop was deliberately gated on a real-feel verdict before expanding scope; "fun" confirms the foundation is worth building on and releases the next milestone.
- Reversibility: n/a (a verdict, not a reversible config)
- Affects: roadmap sequencing, all M-U3 work

## 2026-06-13 — Held props on the chibi rig are posed in WORLD space, not bone-local (267× lossy-scale trap)

- Decided by: Devon (held-axe attach, ticket 86ca8ce6y / PR #39 trace; recorded in STATE.md + unity-conventions.md §FBX)
- Decision: A prop attached to an imported-rig bone on the height-normalized chibi FBX is **parented, then posed in WORLD space** (set world position+rotation after `SetParent`, size by `worldTarget ÷ bone.lossyScale`) — NOT by nudging bone-local offsets. The attach bone is resolved from the `SkinnedMeshRenderer.bones` array BY NAME (`RightHand_010`), never a `transform.Find`/hierarchy name-scan (the rig carries trap nodes — a mesh-group `head` at the origin, a `RightHand.Dummy_011` sibling — that a scan matches first).
- Why: `RightHand_010` carries a ~267× `lossyScale` and arbitrarily-rotated local axes (local +Y maps to world ≈`(0.48,−0.84,0.23)`, mostly DOWN). A naive local scale shipped a 30–50u GIANT axe once; later a local-offset "lift" shoved the axe sideways to a 0.43u sliver at the hip — the literal "no axe" soak bug. World-space posing after parenting is deterministic on these rigs where bone-local is not.
- Reversibility: reversible (attach convention; re-pose in ≤1 PR)
- Affects: held-prop pipeline, Devon, any future bone-attached prop

## 2026-06-13 — NO-AXE root cause: invisible hip-sliver hidden by a false-green zoom-to-fit verify capture

- Decided by: Devon (root-cause trace, ticket 86ca8ce6y/86ca8ca1m / PR #39; recorded in STATE.md + unity-conventions.md)
- Decision: The recurring "I see no axe" soak complaint (flagged 3×) was traced — NOT to a broken craft/equip path — to the held axe being a **0.43u blade-down sliver at the hip (~3.7% of the real 14u/55° orbit frame = invisible)**, while `-verifyAxe`'s zoom-to-fit close-up went **FALSE-GREEN** (a subject-fit capture renders the prop at a fixed apparent size regardless of its real gameplay scale). Fix: world-space pose to ~1.0u seated at the chest (blade flat, ~8.6% frame) + a new `StumpAxe` (inverse-`HasAxe` gate) planting the hatchet upright in the chopping block VISIBLE FROM SPAWN; and the standing rule that any "is X visible to the player" gate captures from a **FIXED-ORBIT** frame matching real gameplay distance/FOV, never a zoom-to-fit close-up.
- Why: A capture that auto-zooms to its subject cannot validate gameplay-SCALE visibility — it is the third instance of the false-green-capture class (after the no-post verify cam and the stale-SMR-bounds framing). The fix had to address both the geometry (world-space pose) and the gate (fixed-orbit capture) or the bug would recur green.
- Reversibility: reversible (pose + capture-rig convention)
- Affects: held-axe + stump-axe, Devon, all visibility verify gates, Tess

## 2026-06-13 — SEA root cause: water was BACKFACE-CULLED (inverted winding), not occluded — winding flipped

- Decided by: Drew (root-cause trace, tickets 86ca8fet0 / PR #38; recorded in STATE.md + unity-conventions.md)
- Decision: The "I see no ocean / grey pond / too sky-cyan" soak complaints were traced to the water mesh rendering **ZERO pixels because it was backface-culled**, NOT occluded by foreground terrain. The sea grid lays its rows near→far in DECREASING world Z but reused the +Z terrain grid's triangle index order → faces wound the opposite way → −Y normals → default URP `Cull Back` culled them from the above-looking gameplay cam. Fix = **reverse the water triangle winding** in `LowPolyZoneGen.BuildWaterEdge`; the earlier geometry chases (slope/deepen/overlap) and the camera-pitch/occlusion hypotheses were REVERTED as wrong-cause. Proven: magenta cross-build diff `0 → 55,103 px` (5.98% frame, N=8 deterministic); a `-seaWaterOnly` probe (hide every other mesh) still showed 0 sea px BEFORE the flip, disproving occlusion. Guard = `WaterFacesUpTests` (every `Water_Play` normal·+Y > 0).
- Why: A color/material/camera tweak can never fix a not-rendering mesh; the magenta-diff proved invisibility and the isolate-probe pinned the cause to winding (the same family as the foliage opposite-winding bug). This closes weeks of "sea looks wrong" tweaks that were all chasing a symptom (fog/sky masquerading as water).
- Reversibility: reversible (winding flip; one mesh-gen method)
- Affects: ocean rendering, Drew, `LowPolyZoneGen`, all reused-grid mesh gen

## 2026-06-13 — Gray beach slab = the TestGround collision proxy; renderer disabled (kept as collider)

- Decided by: Drew (root-cause + fix, ticket 86ca8feuf-adjacent / PR #38 `f455853`; recorded in STATE.md + unity-conventions.md)
- Decision: The grey slab the Sponsor saw on the beach is the flat-Y0 `TestGround` slab (moss-grey `(0.42,0.46,0.40)`) built by `MovementCameraScene.BuildFlatGround` as the **NavMesh / click-move COLLISION PROXY** — it pokes ABOVE the Zone-D sand only on the seaward foreshore where the visual terrain DIPS below Y0 (inland the sand rises above Y0 and hides it). Fix: **disable its `MeshRenderer`** (kept-but-disabled so `.bounds` still resolves for the water-occlusion test) and KEEP the collider → NavMesh + click-move stay bit-identical, zero path regression. Guard `TestGround_IsCollisionProxyOnly_RendererDisabled_NoGreySlab`. When U5 replaces the env surface, fold the collider into the real terrain + delete the placeholder.
- Why: The slab is collision-only; deleting the GameObject would break the occlusion test that reads its bounds, and removing the collider would regress NavMesh/click-move. Disabling just the renderer removes the visual artifact with zero gameplay change.
- Reversibility: reversible (one renderer flag; folds into real terrain at U5)
- Affects: beach scene, Drew, NavMesh/click-move, U5 terrain work

## 2026-06-13 — Binary-scene integration playbook validated (regenerate-on-rebase + merge-tree pre-flight)

- Decided by: orchestrator (integration of #38 + #39 → #40; playbook at `team/orchestrator/integration-39-38-playbook.md`, validated by workflow run `wf_d63e952a-804`)
- Decision: The regenerate-on-rebase pattern for multiple scene-baking PRs (proven on #32/#36) is now a **standing playbook**: base the integration branch on the larger changeset, `git merge --no-ff` the other, take `--theirs/--ours` PROVISIONALLY on the binaries (`Boot.unity` + `BuildStamp.txt`), then **MANDATORY re-bake** via `serve_soak.sh` (`BootstrapProject.Run`) so both features land in the regenerated scene, and gate with BOTH features' scene-presence EditMode tests GREEN TOGETHER (the half-baked-scene gate). A `git merge-tree --write-tree` PRE-FLIGHT predicts the conflict surface (for #38+#39: only 3 files overlap; `MovementCameraScene.cs` auto-merges clean; the two binaries regenerate) so the integration is dispatched with a known-clean expectation.
- Why: Binary, bootstrap-generated `Boot.unity` cannot be hand-merged; the silent-drop failure (ship only one branch's bake) is real and the dual-feature test gate is what catches it. Pre-flighting via merge-tree turns a risky integration into a mechanical one with a written conflict map.
- Reversibility: reversible (process convention; playbook lives in orchestrator docs)
- Affects: orchestrator integration flow, all scene-baking PRs, Devon + Drew + Tess

## 2026-06-14 — Sponsor soak decisions: axe-tweak = in-game NUDGE TOOL; sea-color + axe-head ACCEPTED; auto-status OFF

- Decided by: Sponsor (2026-06-14 soak of PR #40 / `31ce95c` + /sponsor-questions-walkthrough; recorded verbatim in STATE.md resume header)
- Decision: Four Sponsor calls on his 2026-06-14 return. (1) **Axe-tweak mechanism = an in-game, build-gated NUDGE TOOL** — rather than the team iterating exact held/stump-axe transforms, Devon ships a sane DEFAULT plus a debug tool the Sponsor drives himself (select prop → nudge pos+rot → read live values on the HUD → report → bake). (2) **Sea color ACCEPTED** — the now-visible teal sea is liked; saturation polish is DEFERRED, not a blocker. (3) **Axe-head ACCEPTED** — the slate/steel sourced-hatchet head "genuinely looks like an axe"; the earlier barn-red recolor idea is DROPPED. (4) **auto-status OFF** — cron `03029456` cancelled, state file `enabled=false`; re-arm only on an explicit Sponsor ask.
- Why: (1) The held/stump-axe placement is a subjective-feel call best dialed by the Sponsor against the real gameplay view — a nudge tool ends the over-iterate loop and lets him set it once. (2)/(3) Locking the two "good enough / liked" visual calls stops further color-chase churn. (4) The Sponsor is back at the keyboard, so the away-orchestration pulse is unneeded.
- Reversibility: reversible (saturation polish remains a future tweak; auto-status re-armable; nudge tool is build-gated debug, not shipped UX)
- Affects: held/stump-axe placement (Devon), sea + axe-head polish backlog, orchestrator cadence

## 2026-06-15 — Castaway re-generated (Hyper3D→Mixamo) + adopted on the GENERIC rig

- Decided by: Sponsor (concept pick + ADOPT call) + Devon (in-engine rig finding; ticket `86ca8r72j` spike → `86ca8rdkp` adoption)
- Decision: The Sketchfab chibi is REPLACED by a freshly-generated chunky-low-poly castaway (concept art → Rodin Gen-2.5 Image-to-3D, Quad 8k/symmetric/de-lit → Mixamo Standard-Skeleton auto-rig, Idle+Walk). The viability spike (`86ca8r72j`) proved it imports + animates + reads on-style in a shipped URP exe; the Sponsor chose ADOPT, integrated under `86ca8rdkp`. **Load-bearing rig call: ship on the GENERIC (transform-path-bind) rig, NOT Mixamo Humanoid** — the Humanoid muscle-retarget EXPLODES the skinned mesh at runtime (cone displacement) under the scaled scene hierarchy; the spike's bounds-following camera HID it. Generic renders clean. New right-hand bone `mixamorig:RightHand` (lossyScale 1 — no 267× trap). Recolor = luma-preserving HSV remap (toon gradient kept).
- Why: A purpose-generated base beats re-skinning a fighting mesh (the chibi/Quaternius lesson). The Humanoid-explosion is invisible to a spike capture whose camera follows the mesh bounds — the shipped-build capture gate at gameplay framing is what surfaced it. Generic-rig transform-path binding sidesteps the muscle-retarget that detonates under non-uniform scene scale.
- Reversibility: reversible (revert the adoption PR; the rig choice is an import-setting + wiring change in ≤1 PR)
- Affects: player character, rig/animation pipeline, Devon + Uma + Tess, `character-pipeline.md` §Step 4 + `unity-conventions.md` §FBX

## 2026-06-15 — During-walk float = exponential-smoothing LAG (not snap-pick); ship a dial WITH its gauge

- Decided by: Devon (4th-attempt root-cause) + Sponsor (escalation: "you have to add logging or nudging"; ticket `86ca8rdkp`)
- Decision: The recurring "grounded standing, elevated walking" complaint is the EXPONENTIAL-SMOOTHING-LAG class, NOT a snap-pick error. At rest the snap is exact (gap 0.000); a constant-rate (k=18) filter lagged the descending foreshore ~1.2cm at 5.5 u/s while moving, and the blob shadow compounded it (driven off the RAW target while feet rode the SMOOTHED Y). Fix: speed-adaptive snap rate (`snapRateMove` 60 ≫ `snapRateRest` 18) + shadow off the avatar's ACTUAL world-Y + a Sponsor-dialable `groundYOffset`. **And: don't ship a dial without its gauge** — the float was chased for many iterations until a LIVE on-screen measurement (the F8/F9 FloatDiagnostic GAP readout, ~1Hz `[FloatTrace]` log) PROVED feet track the foreshore within ≤2.6mm the whole walk. "Is it fixed" is now answered by a number, not argument.
- Why: After 2+ rejects on a subjective-feel target the unstick/instrument rule fires — a gauge ends the argue-loop and makes the next dial precise (memory [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]]). The lag-vs-pick distinction matters because color/value tweaks can never fix a timing-lag bug.
- Reversibility: reversible (snap-rate + shadow-source are tunable; the diagnostic is build-gated debug)
- Affects: avatar grounding (Devon), the F8/F9 diagnostic, all snapped-avatar locomotion percepts, Tess

## 2026-06-15 — Held-prop stabilization in the BODY-ROOT local frame, not world space

- Decided by: Devon (held-axe walk-shift root-cause; ticket `86ca8rdkp`)
- Decision: A held prop that shifts/clips as the holding arm animates through the walk cycle is stabilized by anchoring its grip in the **BODY-ROOT local frame** (a grip-anchor in `HeldAxeRig`), NOT in world space. Measured: world-space posing made the grip drift WORSE (0.93→1.5u) because world lags locomotion; the body-root grip-anchor cut it to →0.18u (81% better). (Note: this REFINES the 2026-06-13 "held props posed in WORLD space" decision for the OLD chibi `RightHand_010` rig — that rig carried a 267× lossy-scale + arbitrary local axes that made bone-local non-deterministic; the new Mixamo `mixamorig:RightHand` has lossyScale 1, so body-root-local stabilization is now both possible and superior for swing-stability.)
- Why: World-space pose is recomputed each frame from a lagging locomotion transform, so a clip-driven arm swing drags the prop; a body-root-relative anchor moves WITH the body and only the local swing remains, which is what stabilizing damps.
- Reversibility: reversible (grip-anchor frame is a one-method change)
- Affects: held-prop pipeline, Devon, any bone-attached prop on the new rig

## 2026-06-15 — Vista = discrete grounded land/island clusters + fog recession (supersedes Erik's far-ring)

- Decided by: Drew (root-cause + production call; ticket `86ca8t9pq`)
- Decision: The far-horizon vista is built from **discrete GROUNDED land/island clusters** (faceted `FacetedLandmass` shelves, e.g. ~18 peaks in ~6 island clusters on landmass bases +2-6u, ≥5/12 azimuth sectors left open sky/sea) with **fog-only atmospheric recession** — NOT Erik's far-encircling mountain-ring + deep per-cluster fade-tint. Erik's ring+deep-fade caused "floating translucent shards" (the mountain ROOT CAUSE was a DOUBLE-FADE: per-cluster tint 0.45-0.82 × Exp² fog both washing far clusters 70-95% to horizon). Fix capped tint (`MtnFadeCap` 0.25), pulled clusters in (`MtnDistanceScale` 0.55), dropped the 950u ghost range, grounded each on an island shelf. Open sky now dominates.
- Why: A grounded, opaque, discrete-cluster vista reads as solid distant land; an encircling translucent ring with stacked fade fights the fog and produces shards. Erik's winding/surface hypothesis was REFUTED by trace (shader was opaque alpha=1, winding green) — the failure was compositional (double-fade), not a render bug.
- Reversibility: reversible (vista-gen parameters + landmass mesh; tunable in ≤1 PR)
- Affects: world-look vista (Drew), `LowPolyZoneGen`, the art board's far-horizon read, Erik-consult routing

## 2026-06-15 — Diagnose-via-trace BEFORE fixing — geometry/threshold subjective tuning is trace-swept first

- Decided by: orchestrator + Drew + Devon (pattern hardened across the whole 2026-06-15 saga; tickets `86ca8t9pq` / `86ca8rdkp`)
- Decision: Geometry- and threshold-bound subjective tuning is **trace-swept against a headless geom model + scene-verified BEFORE serving a soak** — never fixed on a naive hypothesis. Naive framings were overturned REPEATEDLY this saga: walk-elevated (4 attempts; was smoothing-lag, then shadow-stranded-above-feet, then renderer-disabled-slab-pick); "water elevated" (was vista-islands DRAPING over the play space, not water-Y, not sea-extent, not occlusion — all trace-refuted); finger-mangle (was an OPEN clip-hand around the haft, skinning CLEAN — not a re-weight); shoreline foam (was a steep ramp + coarse water grid stranding foam over deep water, not "kept-only-shifted"); sky greyish (was the over-shoulder orbit framing the MID/horizon band — saturate THAT band, not the zenith — plus a STALE committed `GradientSky.mat` masking the source palette). Each was caught only by trace (`-hideVista`, magenta cross-build diff, `-groundTrace`, isolation probes), never by the first plausible fix.
- Why: The intuitive fix-shape was wrong more often than right on geometry/threshold-bound percepts this saga; trace is the cheap instrument that prevents the expensive soak-overturn loop (Erik's #1 accuracy pattern — Diagnose-Before-Fix kills 2-4 overturns/defect).
- Reversibility: n/a (a process convention; lives in TESTING_BAR / dispatch discipline)
- Affects: all subjective-visual/geometry tuning, Devon + Drew + Tess + orchestrator, Erik's accuracy patterns

## 2026-06-15 — Custom URP skybox shader must use standard skybox-pass render state

- Decided by: Drew (root-cause via `-flatSky` probe; ticket `86ca8t9pq`)
- Decision: A custom URP skybox shader assigned to `RenderSettings.skybox` MUST use **standard skybox-pass render state** (`Cull Off` / `ZWrite Off`, object-space direction, normal clip). The `GradientSkybox.shader` was forcing depth (`positionCS.xyww` on a Background-queue SubShader) → it drew OVER scene geometry → whole-frame wash. Fixed to the standard skybox-pass state.
- Why: A Background-queue shader that writes/forces depth paints across the frame instead of behind geometry — sibling of the magenta/cull-back false-symptom family. The skybox pass has a prescribed render state; deviating from it makes the sky occlude the world.
- Reversibility: reversible (shader render-state block; one-shader change)
- Affects: sky rendering (Drew), `GradientSkybox.shader`, `unity-conventions.md` §Editor-vs-runtime

## 2026-06-15 — In-house asset routes confirmed over paid AI-3D tools (3D-Agent declined)

- Decided by: Sponsor (asked Erik to evaluate; Erik recommended AGAINST; ticket `86ca92vrk` + the world-look-quality consult)
- Decision: The asset route stays **in-house — procedural + URP Shader Graph (world/props) + Hyper3D Rodin → Mixamo (characters)**. The paid AI-3D generator **3D-Agent.com is NOT adopted** (Erik's eval: photoreal output, no low-poly control; doesn't fit the chunky-cartoon direction; existing routes already cover world-look assets, characters/props, and the asset pipeline). Meshy.ai free tier noted as a fallback only. (Reaffirms + extends the 2026-06-13 asset-route decisions with the explicit 3D-Agent decline.)
- Why: The existing routes proved out the castaway + hatchet + full world-look this saga at zero/low marginal cost and WITH the stylization control a photoreal generator lacks; a paid tool that fights the art direction is not worth the spend (memory [[in-house-asset-routes-over-paid-tools]]).
- Reversibility: reversible (route preference; re-evaluable if a low-poly-capable tool appears)
- Affects: all asset creation, Devon + Drew + Uma, orchestrator R&D lane

## 2026-06-15 — Stacked-PR integration (#48 on #47) + re-reconcile each soak round

- Decided by: orchestrator (integration topology; validated against `team/orchestrator/integration-39-38-playbook.md`)
- Decision: The multi-round character (#47) + world-look (#48) work is integrated as a **linear stacked PR** — #48 is based on #47's branch (not main), so the combined build carries both feature sets in one regenerated `Boot.unity`. Each soak round that churns either side **RE-RECONCILES #48 onto the updated #47** and regenerates Boot.unity per the integration playbook; only `Boot.unity` + `BuildStamp.txt` ever conflict (code auto-merges clean). Consequence: CI does NOT auto-run on #48 (it fires only on PRs→main) — the local full suite + serve_soak stamp==HEAD are the authoritative soak evidence; at the big merge, land #47 first OR retarget #48→main and re-run CI (EPERM-aware).
- Why: A linear stack avoids a three-way Boot.unity reconcile and keeps one combined soak artifact; the regenerate-on-rebase playbook (proven #32/#36/#40) makes the per-round reconcile mechanical with a known conflict surface.
- Reversibility: reversible (branch topology; retarget-able to main at merge)
- Affects: orchestrator integration flow, all scene-baking stacked PRs, Devon + Drew + Tess

## 2026-06-15 — World-look LOOK-verdict still Sponsor-PENDING (technical/root-cause decisions above ARE settled)

- Decided by: orchestrator (status note, not a settled look-call; tickets `86ca8t9pq` / `86ca8rdkp`)
- Decision: The above 2026-06-15 entries capture the SETTLED technical + root-cause decisions of the saga. The final **world-look LOOK-approval remains Sponsor-pending** — across the saga the Sponsor APPROVED-IN-PART repeatedly (C5 walk-grounding accepted, shoreline position fixed, character identity/recolor good) while flagging fresh world-look issues each round (shoreline foam, sky/clouds, mountain detail). The combined soak (#48 stacked on #47) is being re-served; the look-verdict + THE BIG MERGE stay Sponsor-gated on the protected branch.
- Reversibility: n/a (a status flag; the look-verdict is the Sponsor's to give)
- Affects: roadmap sequencing, the big merge, orchestrator cadence, Devon + Drew

## 2026-06-16 — Organic seed-42 island is the world basis (supersedes the round disc + the strip)

- Decided by: Sponsor (soak picks 2026-06-16; "I love this island, commit this" → SEED 42 LOCKED)
- Decision: The world is a big ORGANIC/IRREGULAR procedural island — varied coast (beaches + cliffs), beach level with the grass, foam on all edges, water on all sides, mountains on separate islands — generated at `LowPolyZoneGen.IslandSeed = 42` (LOCKED; do NOT re-roll). Supersedes the earlier round disc and the beach-to-meadow strip.
- Why: The disc read artificial (square seabed edge + a "line"); the Sponsor wanted a real-island silhouette and picked seed 42 from 4 variant captures as the most "real island" (peninsula + bays). Shipped in the big merge (#50 → main `6aada8f`).
- Reversibility: reversible in principle (re-roll the seed) but Sponsor-locked — treat as one-way unless he reopens it.
- Affects: world gen (LowPolyZoneGen), NavMesh, camera, all future world content.

## 2026-06-16 — Sea renders Opaque + top-as-front-face (URP cull is by WINDING, not the normal)

- Decided by: orchestrator + Drew (root-cause, PR #50 `d944f6c`)
- Decision: The "sea reads identical to sky" defect was BACKFACE-CULLING, not fog — URP `Cull Back` culls by triangle WINDING, so a +Y-normal guard is a proxy a culled mesh satisfies. Fix = reverse the sea triangle winding so the TOP is the FRONT face; GUARD the winding direction (not the normal). Water stays Opaque-queue (avoids transparent overdraw on the large ocean) with a water-only fog cap → distinct teal + moving waves.
- Why: The gameplay cam saw the skybox THROUGH the culled sea (water==sky); the normal-guard false-greened. Same perceptual-vs-proxy cull family as the magenta / −Z-grid findings (unity-conventions.md).
- Reversibility: reversible (winding flip) but it is the correct render setup — do not revert.
- Affects: water rendering, the visual-pass SRP gate (`86ca9a3b3`), unity-conventions.

## 2026-06-16 — Held prop FOLLOWS the arm's natural swing (reverses the stabilizer)

- Decided by: Sponsor (soak 2026-06-16, "it works perfectly"; final F9 seat dialed)
- Decision: The held axe rides the RAW hand bone's natural swing during locomotion — `HeldAxeRig` removed the swing-stabilizer/grip-anchor AND the bounce-fix vertical-decouple, keeping only the facing fix (hand-local offset rotated by `hand.rotation`, never `hand.TransformPoint`) + a light damp. Final seat `HeldAxeWorldOffsetFromHand=(-0.1502,-0.1602,-0.0528)`, euler `(16,2,-82)`. This REVERSES the earlier stabilize-steady decisions (`86ca8rdkp` / `86ca9ykp0`).
- Why: "Steady-held" vs "natural swing" is a taste call; the Sponsor chose natural follow. Follow-the-arm is simpler and has no cumulative ratchet by construction. The choice carries to run/jump.
- Reversibility: reversible (re-add stabilization) but Sponsor-chosen — the stabilizer traps are the path not taken.
- Affects: HeldAxeRig, CastawayCharacter, the locomotion backlog (run/jump axe behavior).

## 2026-06-16 — Locomotion pivots to WASD + run + jump (supersedes click-to-move core feel)

- Decided by: Sponsor (2026-06-16; CLAUDE.md core-feel line updated 2026-06-17 per his "WASD is the core feel" pick)
- Decision: The movement model pivots from PoE-style click-to-move to WASD + run (Shift) + jump (Space). This REVERSES the "Sponsor-locked PoE-style click-to-move core feel" in CLAUDE.md Context. Backlog sequenced WASD `86ca9yq2x` → run `86ca9yq34` → jump `86ca9yq3q`; the live build keeps click-to-move until they land.
- Why: Sponsor preference — direct WASD control fits the survival-exploration feel better than click-to-move.
- Reversibility: reversible (the input layer is swappable), Sponsor-directed.
- Affects: input/locomotion (CastawayCharacter, MovementCameraScene), held-axe behavior, and jump touches the float system (`modelSoleGround` must suspend ground-snap airborne).

## 2026-06-16 — Unity-6/URP mastery is an always-on mandatory-read (Sponsor HIGH-PRIORITY)

- Decided by: Sponsor (2026-06-16, "cannot stress enough how important")
- Decision: `.claude/docs/unity6-mastery.md` (distilled Unity 6/URP always-on guardrails) is auto-loaded at SessionStart and a MANDATORY pre-read for Drew/Devon before ANY Unity code — wired into CLAUDE.md, the dispatch-template, and the persona files; full cited reference at `team/erik-consult/unity6-mastery-research.md`.
- Why: Repeated Unity/URP traps (serialization, culling, GC, lighting budget) cost soak rounds; a distilled always-on reference reduces them. Sponsor flagged it as high-priority. Shipped in orch-docs PR #56.
- Reversibility: reversible (docs) but a standing process gate.
- Affects: every Drew/Devon Unity dispatch, dispatch-template, SessionStart hook.

## 2026-06-17 — Locomotion sequence locked: WASD (merged) → run → jump → crouch

- Decided by: Sponsor (order is Sponsor-set; crouch added 2026-06-17)
- Decision: The locomotion family ships in a Sponsor-set order: **WASD MERGED** (`86ca9yq2x`, PR #63 squash `f34a829`, feel-approved) → **run-on-Shift** in flight (`86ca9yq34`) → **jump-on-Space** queued (`86ca9yq3q`) → **crouch-on-Ctrl** new (`86caa3kur`, queued). Run/jump/crouch each build on the merged WASD base; crouch is best sequenced AFTER run + jump land so its stance composes onto the finished Walk/Run/Jump Animator without blend-tree churn (but is independent enough to dispatch whenever the locomotion lane is free). Jump is the ONE ticket that touches the float system — it must SUSPEND `modelSoleGround` ground-snap airborne while leaving the grounded-state 8-attempt float fix unchanged.
- Why: WASD is the new core feel (supersedes click-to-move; see 2026-06-16 pivot). The Sponsor set the per-feature order; each feature is feel-soaked before merge. Serializing run→jump→crouch keeps the shared Animator + the held-axe/finger-curl/grounding wiring from churning under parallel edits.
- Reversibility: reversible (each feature is an additive input + Animator state; per-ticket revert in ≤1 PR).
- Affects: input/locomotion (CastawayCharacter, MovementCameraScene), the Animator, held-axe + finger-curl drivers, `modelSoleGround` (jump only), Devon + Drew + Tess.

## 2026-06-17 — Locomotion animation route: Sponsor-sourced Mixamo Without-Skin / In-Place clips

- Decided by: Sponsor (clip sourcing) + Devon (retarget execution)
- Decision: The locomotion clips (Running / Jump / Crouching-Idle / Sneak-Walk) are **sourced by the Sponsor from Mixamo** as **FBX-for-Unity / Without Skin / In Place / 30fps** and dropped into `Assets/Art/Character/Castaway/`; the implementing agent imports + retargets them to the castaway Humanoid like the existing Idle/Walk (Rig → Humanoid, Copy-From-Other-Avatar = the Idle avatar). **In-Place** because movement is driven by the locomotion system (NavMeshAgent-driven), not by root-motion in the clip. The Sponsor-downloaded crouch clips (`Crouching Idle.fbx`, `Sneak Walk.fbx`) are UNTRACKED in the main worktree — the agent copies them from `c:/Trunk/PRIVATE/Far-Horizon/Assets/Art/Character/Castaway/` into its own worktree AFTER its Step-0 `git clean` (else the clean wipes them). All clips carry the Mixamo MANGLED-FINGER note: the open-hand pose reads mangled holding the axe → the HasAxe-gated `CastawayFingerCurl` driver (curl axis MEASURED, not guessed) must cover every new clip.
- Why: Mixamo + the existing Hyper3D→Mixamo pipeline already produced Idle/Walk; reusing the route keeps the rig/retarget mechanics identical. Without-Skin clips retarget onto the existing castaway mesh; In-Place avoids fighting the code-driven locomotion with baked root motion.
- Reversibility: reversible (clip swap / re-import per feature in ≤1 PR).
- Affects: run/jump/crouch animation (Devon), `character-pipeline.md`, the finger-curl driver, the Animator.

## 2026-06-17 — Hit-reaction clips PARKED for a future damage/combat-feedback feature

- Decided by: Sponsor (2026-06-17)
- Decision: The hit-reaction clips (Head Hit / Rib Hit / Stomach Hit / Big Stomach Hit / Getting Up / Stunned) are **PARKED** — not wired now — for a FUTURE damage/combat-feedback feature. There is no damage source designed yet, so there is nothing for these reactions to respond to.
- Why: Wiring reaction animations with no damage system to trigger them would be dead content; the clips are deferred until a damage/combat-feedback feature gives them a trigger. Keeps the locomotion + gameplay waves thin and free of speculative animation state.
- Reversibility: reversible (the clips are parked, not deleted; pick them up when the damage feature is designed).
- Affects: animation backlog, a future damage/combat-feedback milestone, Devon + Priya (scope).

## 2026-06-17 — Gameplay wave: settings panel → inventory/belt → chop → stone (settings = extensible registry)

- Decided by: Sponsor (ticket-prompts 2026-06-17; sequence Sponsor-set)
- Decision: A new gameplay wave of four tickets ships in this strict order: **settings panel** (`86caa4bqp`) → **inventory + belt** (`86caa4bya`) → **chop trees for wood** (`86caa4c5c`) → **pick up small stones** (`86caa4c96`). The **settings panel is FIRST and FOUNDATIONAL** — it is an EXTENSIBLE registry (each setting a named, typed entry — float slider / int / min-max range — bound to a LIVE gameplay param, no restart) that the later three tickets REGISTER into (inventory registers belt-slot / inventory-slot / stack-size; chop registers tree-regrowth + tool-use-speed; stone registers stone-respawn). The inventory ticket defines the shared ITEM model — the **tool-vs-resource rule** (tools → belt-allowed + don't stack; resources → inventory-only + stack to a cap) — that chop (`chopped wood`) and stone (`picked up stones`) plug their resource items into. Inventory on Tab (20 slots), belt hotbar at the bottom (5 slots, select via 1–5 / scroll), axe = PoC tool auto-placed in belt slot 1 + shown in-hand only when selected.
- Why: The settings panel is the soak-tuning instrument (give-him-the-knob: the Sponsor dials values live, we bake the chosen defaults — the F9 axe-nudge pattern generalized to a registry). Building it first means each downstream feature registers its tweakables instead of hard-coding them; building the inventory item model second means chop + stone are thin add-ons onto a settled item/slot system rather than re-deriving it. The strict order is a hard blocked-by chain — the downstream tickets consume the upstream tickets' shared surfaces.
- Reversibility: reversible (additive feature systems; per-ticket revert in ≤1 PR) — but the SHARED contracts (settings-registry API + inventory item model) should be pinned before any PARALLEL dispatch (see `team/priya-pm/gameplay-wave-plan.md`).
- Affects: settings/inventory/chop/stone systems, UI Toolkit, world-gen scatter (chop/stone), `HeldAxeRig` (selected-slot show/hide), Devon + Drew + Tess.

## 2026-06-17 — M-U2 survival loop EXPANDED from one need (WARMTH) to three (warmth + hunger + thirst)

- Decided by: Sponsor (2026-06-17; vision doc `.claude/docs/vision-far-horizon-game-concept.md` + ticket-prompts)
- Decision: M-U2 — which shipped THIN with a single Sponsor-locked WARMTH need (DECISIONS 2026-06-12) — is **expanded to THREE needs**: **warmth** (existing, campfire), **hunger** (harvest berries from bushes → "small satisfaction to his hunger"), and **thirst** (drink-from-hand at a freshwater pond → "small amount of thirst with each scoop"). Three new tickets carry it: `86caamkp8` (HUNGER need — generalizes the `WarmthNeed` model, satisfied by the berry eat-action from bushes `86caa5zz3`), `86caamkv7` (THIRST need + a freshwater pond placed in the seed-42 world + a no-tool drink-scoop interaction), `86caamkxv` (need-meter HUD — generalizes the single-warmth `SurvivalHud` to three bars, to Uma's forthcoming direction). All three GENERALIZE the existing `WarmthNeed` surface (`Current01`/`Max`/`IsCritical`/`Changed`, Time.time-window decay, `TickSeconds` for EditMode) — no rebuild. Death/fail/starvation/dehydration states stay OUT of scope (a floor, not a fail). A cup/container to hold more water is explicitly deferred ("later").
- Why: The Sponsor's full survival-arc vision always included berries/hunger + fresh-water/thirst (the game-concept doc); the WARMTH-only loop was the deliberate thin START, and the single-need model was designed to generalize to N needs. Expanding now — after locomotion + the gameplay wave's inventory — lets berries be eaten from inventory and the pond plug into the settled item/interaction patterns. This reconciles the scope-mismatch flagged in the game-concept doc's index line.
- Reversibility: reversible (additive need systems on the proven `WarmthNeed` pattern; per-ticket revert in ≤1 PR). The three needs land close together — a shared abstract need base, if extracted, must agree its name + surface BEFORE both land (shared-concept naming discipline; coordination noted on `86caamkv7`).
- Affects: survival loop, HUD, world-gen (pond + bushes), settings registry (need tweakables), CLAUDE.md M-U2 scope (updated this PR), Devon + Drew + Uma + Tess.

## 2026-06-17 — Adopt Erik's procedural-mesh + URP-shader quality findings as standing dev guidance

- Decided by: Sponsor (2026-06-17, "apply Erik R&D findings to all developers")
- Decision: Erik's R&D note `team/erik-consult/procedural-shadergraph-quality-research.md` (ticket `86ca8x038`) is **distilled into a standing dev-guardrails doc `.claude/docs/lowpoly-quality.md`** — the `unity6-mastery.md` precedent — and made a MANDATORY pre-work read for all visual/mesh/shader work (auto-loads via the SessionStart hook + the existing "sub-agents Read every `.claude/docs/*.md` before work" rule; a CLAUDE.md Detailed-Documentation index line added). The seven adoptable patterns are filed as Unity tickets, sequenced for the single build slot: `86caamnhf` (apply the confirmed-bug `QuantizeFine` fix), `86caamnjb` (`_FlatShading` ddx/ddy toggle), `86caamnmb` (transparent depth-fade `LowPolyWater.shader` — fog-cap migration risk noted), `86caamnnj` (Fresnel/rim term), and `86caamnra` (a polish backlog rolling up chamfer highlight + vertex-AO bake + seeded scatter rotation). Toon hard-band ramp + screen-space outlines + flat-shading the welded terrain + transparent-water-without-fog-cap are explicitly RULED OUT (they fight the approved faceted-smooth look).
- Why: The Sponsor wants the in-house procedural-mesh + URP-shader route (paid AI-3D tools declined) levelled up; standing guidance + filed tickets operationalize the research into code rather than leaving it as a one-off note. The doc also pins the already-correct patterns NOT to regress (outward winding, per-face normals, up-biased foliage normals, SRP-Batcher compliance, the `_FogCap` floor) so a future change doesn't reopen a closed bug.
- Reversibility: reversible (doc + tickets; no code shipped in this PR — each code ticket reverts in ≤1 PR).
- Affects: all visual/mesh/shader work, Drew + Devon (+ Sponsor-Blender for chamfer geometry), Tess (the visual-UX gate already requires an SRP-Batcher check per `86ca9a3b3`).

## 2026-06-18 — Locomotion-first gate RELEASED; gameplay wave un-gated; crouch deferred

- Decided by: Sponsor (2026-06-18)
- Decision: The locomotion-first sequence gate is **released** — the gameplay/survival wave (settings panel → inventory/belt → chop → stone → bushes/berries → hunger → thirst → three-bar HUD) is now **un-gated** and dispatchable without waiting for run/jump to fully land. **Crouch (`86caa3kur`) is DEPRIORITIZED** ("it can wait") — set to low priority, to be picked up after the gameplay wave. The settings panel (`86caa4bqp`) remains the foundational first dispatch (extensible registry the downstream tickets register into); the inventory item model (`86caa4bya`) remains the second, gating the world-resource family (chop/stone/bushes).
- Why: the Sponsor judged the locomotion lane far enough along that the gameplay wave should not idle behind it; crouch is the lowest-value locomotion remainder and composes onto the finished Animator just as well later. Capacity discipline: the wave is constrained by the single-Unity-build slot (one Unity-build ticket in flight at a time per `single-unity-build-slot-serializes-orchestration`), so the wave serializes on the build-bearing tickets even though the dependency graph would allow more parallelism.
- Reversibility: reversible (priority + sequence calls; re-gate or re-prioritize crouch in a single board edit).
- Affects: dispatch order, the gameplay wave, crouch ticket priority, orchestrator + Devon + Drew + Tess.

## 2026-06-19 — Route A: unified hand-tool/weapon visual style via ONE in-house Blender pipeline

- Decided by: Sponsor (2026-06-19, "Route A")
- Decision: The hand-tool/weapon family (axe, knife, sword, spear, …) gets a **unified visual style** through **ONE in-house Blender (MCP) pipeline** sharing **ONE style spec** (`team/uma-ux/weapon-tool-style-spec.md`, Uma finalizes) + **ONE shared low-poly palette material** — NOT per-asset sourcing. Family cohesion is treated as a STYLE-SYSTEM decision (shared spec + shading model + palette + one pipeline + one shared grip pivot so a single `HeldTool` rig generalizes), not item-by-item asset acquisition. The currently-shipped CC-BY axe (`Assets/Art/Props/CastawayAxe/` — Viktor.G "One-handed stylized axe", Sketchfab CC-BY, baked photographic atlas) is a **PLACEHOLDER to be re-made in the family style — NOT the style anchor** (it is the outlier vs the flat-shaded Zone-D world). Two style parameters stay OPEN for Uma to lock against the LIVE build (not in the abstract): (a) shading model flat-vs-smooth — verify against how the world props are actually shaded in-engine; (b) palette hexes — EXTRACTED from the live world palette, never invented. Re-making the axe in-house RETIRES the CC-BY attribution obligation: once the in-house axe ships, remove `Assets/Art/Props/CastawayAxe/CastawayAxe_License_CC-Attribution.txt` + the in-game/about credit.
- Why: Sourcing each weapon separately gives a mismatched family (the current axe's baked atlas is exactly that outlier); one shared spec + palette + pipeline is the only route that makes every item read as "made by the same castaway" and lets one `HeldTool` rig seat any item. In-house also keeps the family CC-obligation-free. Attribution history: a procedural C# `HeroAxeMesh` was tried first (PR #21, abandoned — "didn't read as an axe"), replaced by the Viktor.G CC-BY asset (PR #29, ticket 86ca8ce6y) which carries an in-game-credit obligation until replaced. Tickets: Uma finalizes the spec (lock the 2 open params); Devon produces the matched SET + re-makes the hero axe (HARD-GATED on the spec; uses the single Unity-build slot; generalizes `HeldAxe.cs`/`HeldAxeRig.cs` → a shared `HeldTool` rig; shipped-build capture gate before merge). Rationale memory: `weapon-tool-unified-style-inhouse-blender-set`.
- Reversibility: reversible (style-system + pipeline convention; per-asset revert in ≤1 PR) — but re-making the axe + retiring the CC-BY file is effectively one-way once it lands and the license file is removed.
- Affects: all hand-tool/weapon assets, the shared palette material, `HeldTool` rig (generalized from `HeldAxeRig`), the CC-BY attribution obligation + about-screen credit, Uma (spec) + Devon (SET) + Tess (capture gate).

## 2026-06-19 — Shared survival-need base = Pattern A: hunger OWNS `SurvivalNeed`, thirst EXTENDS it

- Decided by: Priya (AC pin resolving a Tess wave-prep mergeability risk, PR #86; consistent with the parallel-shared-concept naming discipline + DECISIONS 2026-06-17 three-needs expansion)
- Decision: For the hunger (`86caamkp8`) / thirst (`86caamkv7`) pair that both generalize the `WarmthNeed` surface and both feed the three-bar need HUD (`86caamkxv`), the shared abstract need base is owned by **Pattern A** — the FIRST-to-land need (HUNGER) OWNS the shared base type `SurvivalNeed` (defined in `Assets/Scripts/Runtime/SurvivalNeed.cs`, surface byte-identical to WarmthNeed: `Current01`/`Current`/`Max`/`IsCritical`, `event Action<float> Changed`, `TickSeconds`, `decayPerSecond`/`floor01`/`criticalThreshold01`, a protected satisfaction primitive). THIRST EXTENDS the merged `SurvivalNeed` from main (adds `AddWater`) and does NOT re-declare a base; HUNGER extends it adding `AddFood`. WarmthNeed is NOT required to be refactored onto the base by these tickets (the base generalizes WarmthNeed's *shape*, not its file). Divergence between the base and either need's usage at review is **REQUEST_CHANGES (mergeability-blocking), NOT a NIT**. Pinned in both tickets' ACs (hunger AC1a / thirst AC1a). Recommendation accompanying this decision: an AC-level pin SUFFICES — a full need-base vocabulary-contract doc (à la Drew's item-model contract) is NOT warranted, because the shared surface is fully specified by the existing `WarmthNeed.cs` (a single concrete reference both tickets already mirror) and only ONE new type name (`SurvivalNeed`) + its export site need pinning; sequencing hunger before thirst (Pattern A) collapses the remaining ambiguity by construction.
- Why: hunger + thirst land close together and both bind the HUD via the identical read surface; per the parallel-shared-concept naming discipline, an unowned shared base produces divergent vocabulary (`SurvivalNeed` vs `NeedBase`, differing `Current01` shapes) and non-mergeable parallel PRs. Pattern A (first-to-land owns; sequence the dispatches) removes the divergence by construction at the cost of one merge cycle, which the wave's single-Unity-build-slot serialization already imposes.
- Reversibility: reversible (the base type + its extension are a refactor in ≤1 PR; the ownership rule is an AC pin, re-editable on the board).
- Affects: hunger (`86caamkp8`) + thirst (`86caamkv7`) + the need HUD (`86caamkxv`), the survival-need model, Devon (owner of both needs) + Drew (reviewer) + Tess (the vocabulary-grep review gate).

## 2026-06-24 — The 3 survival needs do NOT share a common base TYPE; the HUD binds by read-state value, not by `SurvivalNeed` param

- Decided by: Devon (implementation finding on the three-bar HUD, ticket `86caamkxv` / PR #129)
- Decision: The three survival needs do **NOT** all share a common base type. **`WarmthNeed` is a standalone `MonoBehaviour`** that PREDATES `SurvivalNeed` and does NOT extend it; only **Hunger + Thirst extend the `SurvivalNeed` base**. Consequently the HUD's generalized `DrawNeedBar` takes the need read-state **BY VALUE** (the `current01` fill fraction + the `isCritical` flag, duck-typed and null-guarded) rather than a `SurvivalNeed`-typed widget parameter. This **supersedes** the assumption in the #125 HUD spec / #127 QA-plan that a `SurvivalNeed`-typed widget param would be the bindable surface. The earlier Pattern-A decision (2026-06-19) still holds for Hunger↔Thirst (Thirst extends the base Hunger owns); this entry only corrects the cross-need HUD-binding assumption — WarmthNeed is NOT on the base, so a base-typed HUD param could not bind all three.
- Why: `WarmthNeed` shipped first (M-U2 warmth-only loop, PR #11) before `SurvivalNeed` existed, and was never refactored onto the base (the Pattern-A decision explicitly did not require it). A `SurvivalNeed`-typed widget param therefore cannot accept WarmthNeed; passing the read-state by value (fill fraction + critical flag) is the only surface all three needs share, and it keeps `DrawNeedBar` decoupled from the need class hierarchy.
- Reversibility: reversible (refactoring WarmthNeed onto `SurvivalNeed` and re-typing the HUD param is a ≤1 PR change if a base-typed binding is later wanted).
- Affects: the three-bar need HUD (`86caamkxv`), the survival-need model + WarmthNeed, the #125 HUD spec + #127 QA-plan assumptions, Devon + Uma (HUD spec) + Tess (QA plan).

## 2026-06-24 — Real-world anchor + silhouette gate for physical-world features (four standing rules)

- Decided by: Sponsor (2026-06-24, popup "bake all four in")
- Decision: Four standing rules now govern every physical-world feature task (pond / fire / hill / dune / terrain carve / water body / shaped prop whose up-vs-down read matters), so such features look RIGHT on the FIRST try: **(1) Real-world anchor** — every such task OPENS with one plain sentence naming what the thing IS in real life ("a pond is a HOLE in the ground the player steps DOWN into; water collects in it"); the build must satisfy that sentence, not just a numeric/color/byte/seed metric. **(2) Mandatory side-profile (silhouette) capture** — before any 3D-shape feature ships to QA/Sponsor, the AUTHOR captures and eyeballs a side-on shot themselves (up-vs-down is invisible from player-eye/top-down, obvious side-on). **(3) Fix the cause, not the symptom** — a fix that contradicts the real-world anchor (e.g. raising water above ground to dodge an occlusion bug) is a band-aid → rethink/escalate; carve the pond INTO the terrain instead. **(4) Reviewer + QA human-eye line** — "would a person call this a <pond/fire/hill>?" sits beside the seed-42/byte/metric checks. Woven into: dispatch-template (new "Real-world anchor + silhouette gate" situational block + pre-dispatch checklist item), `lowpoly-quality.md` §0, devon.md + drew.md (self-test one-liner), tess.md (QA human-eye gate).
- Why: the freshwater pond shipped as a raised MOUND **twice** (cautionary example: PR #130) because the team chased the `-verifyPond` color metric (green on a mound — a metric can't tell a hole from a hill) and "fixed" a water-hidden-under-terrain occlusion bug by LIFTING water above ground = a pond on a hill (nonsense; a pond is a hole). Metrics can't see nonsense; anchoring in the real-world thing + a one-shot side-profile catches up-vs-down errors on the first pass, cheaper than repeated soak-reject rounds.
- Reversibility: reversible (process convention woven into docs/templates/persona files; removable in ≤1 PR).
- Affects: every physical-world-feature dispatch, the dispatch-template, `lowpoly-quality.md`, Devon + Drew (authors) + Tess (reviewer/QA gate) + orchestrator (dispatch-side block selection).

## 2026-06-25 — Freshwater pond collar = FLAT terrain-painted vertex color, NOT a raised mesh

- Decided by: Priya (lesson captured from Devon's round-5 pond diagnostic, ticket `86cadj4g7` / PR #130 — **in-flight, not merged**: the terrain-paint approach is proven correct, only the verify-gate is in round-6 rework, so the decision itself stands ahead of the merge)
- Decision: Flush ground-level water/terrain features (the freshwater pond, and any future puddle/shore/inlet collar) get their shoreline ring as **flat terrain-painted vertex color on the ground mesh, NOT a raised collar/bank-ring mesh**. The persistent white-shoreline-ring artifact in the round-5 pond was isolated by Devon's `-verifyPondDiag` prover. Foam was NOT one of the prover's toggles — foam was already OFF as a round-5 precondition (foam had been a separately-proven white-ring cause in an EARLIER round: the stale committed `PondWaterMat.mat`, final fix `c2af204`, guarded by `CommittedPondMaterialAsset_ShipsFoamOff_NotStale`). The prover's four conditions were **baseline / bloom-off / collar-REMOVED / sea-plane-off** (`Assets/Scripts/Runtime/FreshwaterPondVerifyCapture.cs:~85-89`). With foam ALREADY off, the round-5 residual white shoreline ring was PROVEN by the collar-removed toggle to be the **raised `PondBank` collar mesh** — removing the collar made the ring vanish, while bloom-off and sea-plane-off each left it present (so the round-5 residual ring was the collar, NOT bloom and NOT the sea plane). Root cause: a raised collar/bank-ring mesh draping a recessed-bowl wall catches the warm Zone-D key light edge-on and reads pale/washed-white — a structural white-ring source independent of any post/shader effect. Standing guidance: prefer a terrain-painted vertex-color ring (flush with the ground) over a raised bank-ring mesh for any flush ground feature; if a raised lip is genuinely wanted, treat the pale-edge read as the expected failure mode and verify it in the shipped-build capture, not just the editor.
- Why: the round-5 residual ring resisted effect-side fixes (bloom/sea-plane) because the cause was geometric, not shader/post; the prover isolated it definitively once foam was already ruled out (and fixed) in the prior round. Encoding "flush feature → flat painted ring, not a raised mesh" as a decision prevents the next ground-water feature from re-introducing the same raised-collar white-ring class. Coheres with the existing low-poly vertex-color inline-materials pattern (DECISIONS 2026-06-12) and the "physical features: anchor real-world + side-profile capture, fix the cause not the metric" discipline.
- Reversibility: reversible (a per-feature mesh-vs-painted-ring choice; revert in ≤1 PR) — but re-introducing a raised collar reopens the proven white-ring class. Note #130 is still in review; if its round-6 rework changes the approach materially, revisit this entry.
- Affects: the freshwater pond + any future flush ground-water feature, world-gen ground/terrain vertex-color painting, Devon + Drew (visual/mesh work) + Tess (shipped-build capture gate on the shoreline read).

## 2026-07-01 — Combat / HP / Death system LOCKED (grill-first, 9 Sponsor decisions)

- Decided by: Sponsor (via /grill-me, 9 branches all Sponsor-picked; design ticket `86cabcdpn`)
- Decision: The combat/HP/death model is locked across 9 decisions: **(1)** HP is a **dedicated `Health` component, SEPARATE from `SurvivalNeed`** (needs rest at a floor and never hit zero; HP takes ACUTE damage and 0 HP = death — a different shape, do NOT fold into SurvivalNeed). **(2)** Death consequence is **tiered by difficulty — the 3 death behaviors ARE the 3 tiers:** Easy = faint & recover in place (no setback, enemy disengages, low damage, fast regen); Medium = respawn at last campfire (start beach if none), inventory KEPT, moderate; Hard = respawn at camp + inventory DROPS at the death spot (reclaimable), high damage, slow regen. **(3)** HP regen is **NEEDS-GATED** — regenerates only while warmth/hunger/thirst are above threshold; a critical need STALLS regen (or slow-drains HP), reading the SurvivalNeed surface (`Current01`/`IsCritical`) without adding a new need. **(4)** Fighting back is a **WEAPON SYSTEM** (not axe-only): a weapon carries damage/reach/attack-speed/own-animation/damage-type (pierce/slash/blunt)/optional on-hit-status; identity = type × material tier; reuses the left-click swing; acquisition = BOTH craft-at-station (wood→stone→bone/metal) AND find-in-world. **(5)** Weapon-vs-mob effectiveness is **HYBRID** — weapon attributes + a damage-TYPE tag vs mob size/behavior + resistances/weaknesses ("spear beats boar" emerges from long reach + boar weak-to-pierce); systemic + designer-tunable via type↔resistance tags, NO full O(weapon×mob) table. **(6)** Status effects are a **GENERAL data-driven framework (DoT/stun/slow-capable), shipping BLEED first**; works both ways (mobs→player and player→mobs). **(7)** Enemies have HP + resistances + behavior and die at 0 HP (mirror of the player HP model). **(8)** Difficulty exposure — HP-max/damage-taken/regen-rate/death-behavior are per-tier, dialed in the dev-tweak console (same pattern as per-need decay `86cabeqwf`) and baked into the difficulty presets. **(9)** All of the above compose into ONE integrated system, which the POC proves. **Broken into impl tickets:** POC `86cah7xxp` (lean vertical slice — player HP + tiered death + needs-gated regen + snake-as-damageable + axe-vs-spear + bleed + damage-type↔resistance + HP readout) and phased follow-ups `86cah7y5b` (find-in-world acquisition), `86cah7ydt` (wild boar 2nd enemy + matchup proof), `86cah7ym9` (weapon-roster expansion), `86cah7yuh` (poison/stun/slow), `86cah7z2q` (HP HUD polish + heal sources). Design ticket `86cabcdpn` is design-complete (records the design + spawns the impl tickets).
- Why: the game had no health/damage/death (SurvivalNeed has only a cosmetic critical flag + a no-fail floor; WarmthNeed explicitly scoped death OUT) and the snake POC (`86caaz4vn`) shipped a bite with no effect. The Sponsor directed (2026-06-19) "introduce a real HP/death system but grill me about it first"; the grill resolved every branch. Keeping HP separate from needs, gating regen ON needs, and making enemies damageable ties combat into the existing survival loop as one system rather than a bolt-on. The tiered-death-as-difficulty-tiers choice makes the game kid-friendly on easy and consequential on hard per the standing difficulty-settings directive (quality-bar #7).
- Reversibility: reversible in principle (each system is new code addable/removable in bounded PRs) — but the model is Sponsor-locked and unblocks the whole combat roadmap, so treat as directionally committed. The enemy-HP surface is SHARED with the snake POC `86caaz4vn` (whichever lands the enemy `Health` first OWNS it; the other extends it — per the parallel-shared-concept naming discipline).
- Affects: a new `Health` component + weapon system + status-effect framework + enemy-HP surface; the SurvivalNeed framework (regen reads its surface), the dev-tweak console + difficulty presets, the unified-weapon Blender set + procedural-animation-verbs (per-weapon swings), the snake POC `86caaz4vn`; Drew (POC owner) + Devon (reviewer / systems follow-ups) + Uma (HP HUD) + Tess (QA + shipped-build capture + soak).

## 2026-06-30 — Next-island/boat POC: DESTINATION-FIRST — prove the big-island terrain-gen now; boat/journey deferred

- Decided by: Sponsor (2026-06-30 `/grill-me` on the next-island/boat prompt; ticket `86caa9zpp`)
- Decision: The next-island/boat prompt is **split into two halves and sequenced destination-first**. **Half 1 — DESTINATION (ticket `86caa9zpp`):** the POC proves the **big-island terrain-gen** and is **build-ready now** (grill done, design locked). The locked design: (Q1 scope) destination-first — the POC proves the terrain-gen; the boat/journey is a separate follow-up half. (Q2 size+perf) a *feels-big* island, **~2-3 min to cross**, holding **60fps** on the EXISTING low-poly + GPU-Resident-Drawer approach **scaled up** — scale toward the eventual ~10-min target ONLY if perf holds; the **#1 POC finding is the PERF VERDICT** (single scaled mesh + LOD vs needing chunked/streamed terrain). (Q3 mountain) ONE dominant **snow-cap peak** = the island's hero landmark + future sea-beacon, snow rendered as a **height-threshold white material** on the faceted low-poly mesh (no snow texture). Shape: organic / non-round, like the seed-42 start island. (Q4 success bar) walkable + feels-big + organic + one snow-cap peak + 60fps, judged in the **shipped build** via a **Sponsor walk-soak**. The Sponsor **declined a separate Erik perf-benchmark — the build itself is the perf test.** **OOS of this POC:** the boat/sail + journey/reveal; survival systems on the new island; props/decoration/content; wiring to the seed-42 start island (the POC island loads **STAND-ALONE** for the soak). **Half 2 — JOURNEY (ticket `86caa9zju`, the boat):** **DEFERRED** — it gets its OWN `/grill-me` only AFTER the destination POC lands + soaks; kept `to do` + a `sponsor-gate`/grill-first note; the boat design is unsettled.
- Why: the "sail to a much-bigger island, walk ~10 min across it" vision only works if the terrain-gen scales without breaking framerate, so the Sponsor chose to answer the perf go/no-go on the big island BEFORE any boat work — the journey only matters if the destination is feasible. Trying the existing gen scaled (rather than a new gen or a separate benchmark) keeps the POC's question precise: does the *existing* approach scale? Sequencing destination-first also keeps the boat grill honest — it gets designed against a known-feasible island, not a hypothetical one.
- Reversibility: reversible (POC on an independent branch; the start island is untouched; the split + sequence are board state, re-editable in ≤1 PR). The perf VERDICT it produces is a finding, not a config.
- Affects: world-gen (a new stand-alone big-island POC scene + scaled terrain + height-threshold snow material), the next-island POC `86caa9zpp` (build-ready) + the deferred boat/journey `86caa9zju`, Devon/Drew (owner+reviewer) + Tess (shipped-build capture gate + side-profile silhouette) + the Sponsor (walk-soak), quality-bars Bar 1 (organic) + Bar 4 (real-world feature + side-profile), the single Unity-build slot.

## 2026-06-30 — Open-horizon = full open ocean (Option A); next-island reveal via natural fog-haze

- Decided by: Sponsor (2026-06-30 walkthrough on the open-horizon spec `86cafffe8` / PR #199)
- Decision: The horizon look is **Option A — full open ocean**: REMOVE the distant horizon mountains so the start island reads as "a little island lost in a huge ocean", open blue water dissolving into warm sky a full 360° around the player. **Option B (a faint rim hint on the horizon) is the pre-planned soak fallback** — adopt it only if an empty horizon reads cheap/flat in the soak. The future next-island reveal (the journey POC `86caa9zpp` follow-up) uses a **natural fog-haze reveal (Approach 1)** — the next island fades up out of the haze as the player nears it — NOT a scripted/authored cinematic reveal moment. Impl ticket: **`86cagfn8h`** (open-horizon look); soak-gated.
- Why: Sponsor vision call (subjective-feel, his domain). A full open ocean strengthens the big-endless-world / small-player north-star — the lone little island in a vast sea reads as the start of a real journey, and a natural haze-reveal keeps the eventual next-island moment diegetic rather than staged.
- Reversibility: reversible (the mountains are removable/re-addable world-gen state; Option B is the staged fallback if the soak rejects the empty horizon; revert in ≤1 PR).
- Affects: world-gen / skybox / horizon look (`86cagfn8h`), the next-island reveal approach for the journey POC follow-up, quality-bars (big-endless-world north-star), Devon/Drew (author) + Tess (shipped-build capture gate) + the Sponsor (soak).

## 2026-07-01 — Settings-panel SPLIT: player-facing Settings (F1) vs dev debug console (new key) — supersedes the unified-panel direction

- Decided by: Sponsor (2026-07-01 soak of the #218 F-key migration — "that was not the intention")
- Decision: The single unified settings/dev-tweak panel is **SPLIT into two panels**: **(a) a PLAYER-FACING Settings panel on F1** (the existing open key) carrying only the rows a player should touch — belt slots, inventory stack size, difficulty tier (if/when a row exists), and the three survival-need on/off toggles + decay-rate sliders (warmth/hunger/thirst) — WITH a conditional-visibility rule that shows a need's decay-rate slider ONLY when that need's decay toggle is ON; and **(b) a SEPARATE DEV DEBUG CONSOLE on a NEW key (proposed F3 — layout-agnostic function key, verified unused; F2 = legacy IMGUI overlays, F5/F6 = SneakIsolationTool)** carrying every dev-only visual/positional tuning row migrated in #218 (world-look sun/fog/clouds/mountains, arm-pose R/L, run-lower, cam-follow lerp/vertical/airborne/lead, held-weapon placement, ground-Y, air-control accel, tree/stone/berry/log timers + yields, plus the panel-chrome Console UI scale / UI text scale). This **REVERSES** the earlier "one unified panel that absorbs all F-key handles" direction (memory `[[sponsor-wants-unified-dev-tweak-console]]`, DECISIONS/dev-tweak-console-spec lineage) — the unification is retained ONLY within the dev console; the player must never see the dev nudges. Impl ticket: `86cah8ukr` (`feat/refactor`, Unity-build lane, Devon owner / Drew reviewer, `needs-soak`). #220 (`86cabeqwf`, the per-need on/off + decay-rate work) is **SUPERSEDED / folded into** the split ticket — its need rows land in the PLAYER panel with the new conditional-visibility fix rather than as unconditional rows on the unified panel.
- Why: The #218 migration correctly consolidated the standalone F7/F9/F10 dev nudges into the console, but it put them alongside genuine player settings, and the Sponsor's soak surfaced that a player opening Settings must NOT see arm-pose eulers / fog channels / follow-gains. The split keeps the dev-tuning unification (one console, all nudges) while giving the shipping player a clean, minimal Settings panel. The conditional-visibility fix (#220) removes the meaningless decay slider for a disabled need.
- Reversibility: reversible (a UI routing/key split + a per-need slider visibility rule; the underlying registry + bindings are unchanged, so revert is ≤1 PR — re-point both panels at one registry view + one key).
- Affects: `SettingsPanel` / `SettingsCatalog` / the panel scene wiring (`MovementCameraScene`/`Boot.unity`), the two open keys (F1 player + proposed F3 dev), the superseded #220 per-need work (`86cabeqwf`), memory `[[sponsor-wants-unified-dev-tweak-console]]` (now scoped to the dev console only), Devon (author) + Drew (reviewer) + Tess (shipped-build capture gate) + the Sponsor (soak).

## 2026-07-05 — Castaway v2 identity: bearded, rugged, friendly-neutral adult survivor
- Decided by: Sponsor
- Decision: The hero castaway's identity becomes a **bearded, rugged, friendly-neutral adult survivor**, deliberately REVERSING the earlier "young + happy" lock. (Sponsor chose the "Full reference look" against `inspiration/2026-06-12_21h00_32.png`, then iterated the concept to a friendly-neutral expression.)
- Why: Sponsor's direct in-session art call on the Castaway v2 concept — the full-reference rugged look reads as the intended hero over the earlier young+happy design sheet.
- Reversibility: reversible (asset/identity choice; the old castaway stays live until v2 passes the soak)
- Affects: character art, CLAUDE.md character identity, the Castaway v2 integration, downstream gear/customization

## 2026-07-05 — Hero-character route ratified: concept → Rodin → Mixamo
- Decided by: Sponsor
- Decision: The hero-character production route is ratified as **AI concept image (openai-image gpt-image-1, image-to-image from the inspiration board) → Hyper3D Rodin (web UI, Gen-2.5/High, de-light ON, Quad ~8000) → Mixamo auto-rig → Unity Humanoid**, proven end-to-end by shipping Castaway v2 through it (harvest PR #260).
- Why: proven in-session end-to-end (concept → Rodin → Mixamo → Blender-verified 41-bone rig); extends the existing character-pipeline.md route; the Rodin base is welded, so customization = gear modules + texture recolors (deeper hair/beard modularity = phase 2).
- Reversibility: reversible in principle (route choice); one-way in practice for v2
- Affects: character pipeline, character-pipeline.md, all future hero-character work, gear-module strategy

## 2026-07-06 — Sponsor walkthrough: #261 fold+merge, axe redo iter-1 approved, v2 owns grip, combined re-seat
- **#261 docs:** fold the 2 stale CLAUDE.md lines (line-3 identity, doc-index Humanoid->Generic; + the hero-route/v2-LIVE line, same error class) then auto-merge label. Folded as `d697960`.
- **Stone-axe redo (86cajkk7h):** Sponsor approved iteration 1 on the A/B — haft radial x0.70 (0.051–0.064 m), head uniform x0.88 about the mount point (X-width 0.312->0.2746 m). Baked to FBX + blend saved. Original rejected FBX preserved in session scratchpad only.
- **PR #239 / 86cahnmjv:** closed superseded-by-v2-rig; the combined v2 re-seat pass owns thumb/grip on the new hand; fresh fix only if v2 reproduces the defect.
- **Re-seat sequencing:** ONE combined Devon pass (new axe integration + all-4 stone weapons on the v2 hand) instead of two passes/two soaks.
- **Auto-status:** stays OFF during the interactive session; re-arm when dispatchable work exists.

## 2026-07-06 — Hero-character low-poly direction re-opened (Sponsor, feedback-driven)
Sponsor received feedback that the Hyper3D/Rodin castaway is "not low poly like the rest of the game" and wants to revisit the in-house low-poly path. Route decision deferred to evidence: Erik consult dispatched FIRST (ticket 86cak3r3k, note → team/erik-consult/lowpoly-hero-conversion-research.md) evaluating (1) low-polyfy the existing v2 mesh, (2) fresh hand-model burst (context: 7 bpy hero fails 2026-07-05), (3) Rodin re-gen from low-poly concept, (4) shader-only faceting. Castaway v2 STAYS the live default until a replacement passes the full soak gate — this decision re-opens direction, it does not revert #262.

## 2026-07-06 — Castaway v3 identity: hopeful-but-SCARED young adventurer (reverses the v2 bearded-rugged lock)
Sponsor (verbatim, route-pick popup): "I dont actually like the current v2. he is too strong and too happy. I would like another go at a hopeful but scared young man prepared for adventure." So the 2026-07-05 bearded-rugged-friendly identity is REJECTED for the next hero, partially returning to the original young+hopeful direction with a scared/brave nuance. Erik's Route-1 conversion mechanics (retopo ~1,500–3,000 tris + flat-shade + palette re-texture + Mixamo Generic; 86cak3r3k) are ADOPTED but applied to a NEW base mesh: concept → WEB-Rodin → retopo/low-polyfy → Mixamo → Unity Generic, staged v3 toggle. v2 stays the LIVE default until v3 passes the full soak gate. CLAUDE.md's character line stays describing the LIVE v2 (#261 unchanged); it flips when v3 activates.

## 2026-07-06 — Castaway v3 concept LOCKED (variant E)
Sponsor picked concept E after 2 rounds (5 candidates): man in his mid-30s, light stubble (adult, NOT v2-rugged), lean ordinary-guy build, scared-but-going-anyway expression (wide worried eyes, set mouth), torn-sleeve teal shirt, rope belt, rolled grey-brown trousers, barefoot, A-pose. Locked file: art-src/castaway_v3_concept_apose.png. Round-1 direction note: "torn shirt, but not a boy, a man in his 30s" (rejected the young-boy reads A/B/C). Next: Sponsor runs WEB-Rodin image-to-3D on the locked concept (ticket 86cak41d4).

## 2026-07-06 — Castaway v3 concept RE-LOCKED: variant G supersedes E (expression dialed back)
Sponsor re-judged after locking E: "he looks too scared" → "should be more neutral" → picked G from the F/G/H expression round: mid-30s, light stubble, lean, head-on gentle friendly smile (not scared, not neutral-blank), torn teal shirt, rope belt, rolled trousers, barefoot, A-pose. art-src/castaway_v3_concept_apose.png now = G (E superseded, kept in session scratchpad only). Identity nuance final: the "scared" part of the original brief is carried by ANIMATION/posture, not the sculpt — the face bakes a mild friendly-neutral read.

## 2026-07-06 — Castaway v3 facet density LOCKED: 3.0k tris (QuadriFlow 1509 quads)
Sponsor picked the 3.0k-tri conversion from the facet A/B (raw-Rodin vs 3.0k vs 2.1k): clearly faceted, keeps hands/belt/silhouette, better rig deformation headroom; top of Erik's 1,500-3,000 band. Source of truth: art-src/castaway_v3_lowpoly.blend (raw 8,370-quad Rodin import + the chosen v3_lp_2800tri mesh). Remaining before Mixamo: region-color cleanup (border noise), painted/geometry face features, final palette texture + UV placement.

## 2026-07-06 — Castaway v3 Route-1 conversion GESTALT-REJECTED ("no its terrible" — the whole converted character)
The retopo conversion (QuadriFlow 3.0k + flat semantic regions + geometry face decals) cleared every itemized defect (density Sponsor-picked from A/B, regions cleaned, face positions verified against the sculpt's measured sockets) and still failed the Sponsor's bar as a WHOLE. Per character-pipeline.md's ratified gestalt diagnostic: route switched, not iterated. Work preserved in art-src/castaway_v3_lowpoly.blend for reference. v2 stays LIVE. Next: Sponsor picks among remaining routes (Rodin Smart-Low-poly re-gen probe / Quaternius-class low-poly base customization toward concept G / exaggerated-chunky concept re-draw / pause).

## 2026-07-06 — v3 route after gestalt-fail: Rodin Smart-Low-poly(BETA) re-gen probe
Sponsor picked the probe: re-run locked concept G through Rodin choosing Smart-Low-poly topology at Confirm (instead of Quad-8000). Framed explicitly as a PROBE (Erik grade: unproven, vendor-claimed) — fallbacks remain low-poly-base customization (Quaternius-class, v1 precedent) and exaggerated-chunky concept re-draw. Export lands in a SEPARATE folder (art-src/castaway-v3-rodin-export-lowpoly/); the first Quad-8000 export stays untouched.

## 2026-07-06 — v3 hero route LOCKED: Rodin Smart-Low-poly + HSV-posterized diffuse
The Smart-Low-poly(BETA) probe SUCCEEDED where the retopo conversion gestalt-failed: Rodin re-gen of locked concept G with Smart-Low-poly topology (Quad type, Low density) produced REAL facet geometry at 3,605 quads / 7.4k tri-equiv (~half of Quad-8000's 16.7k), keeping the concept's painted cartoon face. Sponsor picked the HSV-POSTERIZED diffuse treatment (V 5-steps / S 4-steps, hue untouched — flattens cloth/skin shading to steps, keeps the face) over painted-as-is. Erik's 1.5–3k band is superseded by Sponsor verdict at 7.4k. Export: art-src/castaway-v3-rodin-export-lowpoly/ + texture_diffuse_posterized.png. Next: Mixamo auto-rig (A-pose ok) → Unity GENERIC → staged v3 toggle + capture-gate reconcile + held-weapon re-seat.

## 2026-07-06 — Double soak-PASS: v3 identity + #254 weapons; activation ordered
Sponsor (verbatim): "soak v3 identity approved. soak 254 approved lets move on with v3 char and dial in the weapons with that char."
- v3 identity soak (Build\soak-v3\, stamp bb715b1) PASSED -> activation ticket 86cak9kau un-gates (default flip + capture-gate reconcile + ALL-4-weapon re-seat on the v3 hand + finger-curl + Drew's #263 NIT).
- #254 weapons soak (Build\soak-254-v2\, stamp 10b8195) PASSED -> auto-merge labeled (MERGEABLE, sequenced after #263's 11:46Z merge). 86cajkk7h complete on merge; 86cahnmjv closed (thumb defect not reproduced; v3 grip owned by 86cak9kau item 4).
- Weapon "dial-in" on the v3 char = 86cak9kau scope items 3-4, dispatching once #254 lands on main (re-seat then covers all 4 stone weapons).

## 2026-07-06 — v3-live soak PASS (with follow-up): v3 activation ships
Sponsor (verbatim, walkthrough): "pass but weapons etc. need to be dialed in for the new v3 character." #264 auto-merge-labeled; 86cak9kau completes on merge; v3 = the shipped default hero. Follow-up: a weapon DIAL-IN pass on v3 (seat/scale/pose fine-tuning, Sponsor-driven) — per [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]], route through the F9 nudge handle (fix its broken head-resize keys 86cajuuz0 where still relevant) -> Sponsor dials -> bake. Ticket to be filed this walkthrough round.

## 2026-07-06 — Walkthrough Q2+Q3: #265 Sponsor-browser-merging now; island lane GREEN-LIT
- #265 (weaponset CI gate): Sponsor merging in browser now; orch flips 86caju052 complete on observed merge.
- Island lane: GO — dispatch C2 (86cakk4w8) when the build slot frees post-#264-merge; C2->C3->C4 serial; the v3 weapon dial-in follow-up runs in the non-build lane in parallel.

## 2026-07-06 — Iron progression Q1: MODEL A locked (mine ore + smelter)
Sponsor picked Model A over Erik's D recommendation: full mining chain — ore nodes, pickaxe (new 5th tool type per tier), smelter. Richest survival build, scope L. Consequences: pickaxe extends the Sponsor-locked weapon set (Blender burst needed for wpn_pickaxe_{stone,iron}_01); 86cah7y5b (find-in-world) stays standalone (not absorbed). Q2-Q5 refine the model below.

## 2026-07-06 — Iron progression Q2+Q3: WORK-LED earn feel; NEW forge/furnace structure
Q2: work-led — the crafting/smelting grind is the point (ore findable without heavy exploration; effort = mining volume + fuel + smelt time). Q3: a NEW buildable forge/furnace structure (distinct from the bonfire; extends the survival arc shipwreck->...->furnace->iron; crafting-table-class buildable).

## 2026-07-06 — Iron progression Q4+Q5: PEACEFUL gather/craft; BOTH difficulty dials
Q4: NO combat guard — mining is peaceful; the iron chain ships fully independent of the combat cluster (no hard dep). Q5: BOTH dials exposed — ore rarity + smelt cost (fuel/time/material), per-tier easy/med/hard presets, registered in SettingsCatalog per the existing tweakable pattern.

## 2026-07-06 — Iron progression BONUS: PICKAXE approved as the 5th tool type (both tiers)
wpn_pickaxe_stone_01 + wpn_pickaxe_iron_01 extend the locked weapon family (knapped stone / forged iron recipes per [[weapon-two-tier-style-stone-iron]]); authored via an interactive Sponsor-judged Blender burst (schedulable). IRON DESIGN NOW FULLY LOCKED: Model A (mine ore + smelter) · work-led · new forge/furnace buildable · peaceful (no combat dep) · both difficulty dials (ore rarity + smelt cost) · pickaxe 5th type. Erik's note must be committed to main before/with the citing implementation spec.

## 2026-07-07 — Fable = advisor-only; opus implements everything (supersedes the design-lane fable exception)
- Decided by: Sponsor
- Decision: Fable (the orchestrator session) only analyzes tasks, authors the plan/brief, and gives advisement; ALL implementation — including the creative/Blender/design-build lane — runs on opus agents; an agent that is unsure or hits a plan gap STOPS (commit+push WIP) and asks fable for advisement (`ADVISEMENT NEEDED:` report → SendMessage answer) instead of improvising.
- Why: fable token conservation (Sponsor verbatim 2026-07-07: "stop burning fable tokens and begin using fable only as an advisor…"). Supersedes the 2026-07-03 per-dispatch `model:"fable"` design-lane upgrade.
- Reversibility: reversible (re-enable per-dispatch upgrades on the Sponsor's word)
- Affects: orchestrator dispatch policy, all personas, R&D/creative lane, dispatch-template (new Advisor-escalation block)

## 2026-07-08 — STANDING AUTO-MERGE authorization (supersedes per-PR merge approval for the non-soak class)
- Decided by: Sponsor (popup, 2026-07-08 morning)
- Decision: the orchestrator auto-labels ANY PR for merge — present or away, no per-PR ask — once ALL machine gates are green (required CI SUCCESS + peer APPROVE verdict + Tess QA where the class requires it + Self-Test Report where UX-visible) AND the PR has no soak surface. Soak-surface (feel/visual) PRs still gate on the Sponsor's soak verdict. Workflow-file (.github) PRs remain manual until the scoped-PAT fix (86cafhehe) lands.
- Why: Sponsor verbatim: "look into why i have to do any manual merges, I want to avoid this." Investigation: the label path was already mechanical; only the per-PR-approval policy forced manual steps. The one-click staging class is retired.
- Reversibility: reversible — Sponsor revokes with a word; falls back to one-click staging.
- Affects: orchestrator merge flow, away-queue format (one-click class retired), all personas' merge expectations

## 2026-07-08 — Sponsor NEVER performs git/CLI operations (hard rule; supersedes all "you run this" handoffs)
- Decided by: Sponsor (verbatim, /drain-and-save popup: "I NEVER WANT TO COMMIT, PUSH OR ANYTHING YOU SHOULD DO IT")
- Decision: the Sponsor is never handed git/gh commands to run — no merges, commits, pushes, worktree cleanups, or label commands. The orchestrator/team performs ALL mechanical operations; where the classifier gates an action, the orchestrator obtains the Sponsor's in-context approval via popup and then executes it ITSELF. The Sponsor's role is verdicts and approvals only (soaks, dials, priorities, popup clicks).
- Why: repeated friction handing the Sponsor one-click commands (away-queue one-clicks, fh-261-fold cleanup, the #287 merge suggestion). Pairs with the 2026-07-08 STANDING AUTO-MERGE grant.
- Reversibility: reversible on the Sponsor's word
- Affects: orchestrator merge/cleanup flows, away-queue format (no command handoffs — approval-only items), memory [[explain-why-before-handing-sponsor-commands]]

## 2026-07-08 — Rule clarified: GitHub UI clicks count as commands too (Sponsor scope-check)
- Decided by: Sponsor (verbatim: "clicking in gh should count as command also then")
- Decision: the never-runs-commands rule includes GitHub's web UI — no browser merges, no UI operations. The Sponsor's surface is IN-CHAT ONLY (popups, soak verdicts, priorities) plus physical machine actions the orchestrator's sandbox genuinely cannot perform (e.g. launching the interactive runner window — attempted twice, OS-denied). Consequence: the browser-merge class is RETIRED — even .github workflow-file PRs are merged by the orchestrator via direct `gh pr merge --admin` after in-chat approval (proven live on PR #287, 2026-07-08 16:03Z; the workflow-token wall only constrains the Action's token, not the orchestrator's gh auth). The scoped-PAT ticket 86cafhehe is downgraded to optional (label-path completeness, no longer required for any merge).
- Reversibility: reversible on the Sponsor's word
- Affects: merge flows for .github PRs, away-queue item format, ticket 86cafhehe priority

## 2026-07-18 — Wood-tier weapon set PASSED (Sponsor walkthrough verdict)

- **Decision:** Sponsor PASSED all 5 wood-tier pieces as-is (axe/pickaxe/spear/knife/sword; whittled-wood, existing palette tones, 28-41 tris) from the 13 staged renders in art-src/wood-burst-renders/ — "PASS — export FBXs, integrate" via /sponsor-questions-walkthrough popup.
- **Consequence:** FBXs export from art-src/weapons_reauthor.blend (wood row y=-0.6) to Assets/Art/Props/WeaponPack/ and integration proceeds (ids *_wood already live in #294 catalogs; in-hand seating + verbs remain ②/art-burst scope per the #294 deferred flags).
- **Source:** away-queue item 0b (staged 2026-07-08) → resolved 2026-07-18.

## 2026-07-19 — Crafting redesign wave ①-④ CLOSED on sponsor soak PASS; C build menu is the single build entry point

- **④ chain soak = SPONSOR PASS** (walkthrough popup, "chain works, forge reads right"; soak-crafting-4 @ 75a9725): `86camz9uz` ① (shipped 07-18) · `86camz9v7` ② · `86camz9vh` ③ · `86camz9vq` ④ · ghost-obstruction fix `86catqxm0` — all complete. The Sponsor-locked wood→stone→iron progression from the 2026-07-08 grill is live end-to-end.
- **Sponsor design confirmation (mid-soak verbatim, ticket 86catpvpa comment 90150243183538):** C = build MENU for all placeable structures; the placed crafting TABLE's menu is ITEMS-only (tools/weapons); the interim forge key V retires. Shipped same-day as PR #311 (`IBuildPlaceable`/`BuildMenuUI.RegisterPlaceable` seam — ⑤ campfire and future placeables register rows, never fork a menu).
- **Merge-path policy shift (sponsor verbatim in-walkthrough: "Why do i have to merge anything? you can do it. yes merge now"):** fully-gated workflow-file PRs are orch-DIRECT-merged via `gh pr merge --admin` when the sponsor is present/delegating — the browser-click ritual was classifier-convention only, never token-required for the CLI (#299 `d757c2e`, #308 `fdb81df`, #309 `9a8687b`). Away-mode staging unchanged.

## 2026-08-02 — Orchestration doctrine rewritten: 12 rulings to stop the team generating its own work

- Decided by: Sponsor (grilled through 12 discrete decisions in one session; every ruling his)
- Decision: (1) hard ceiling of one developer + one reviewer + at most one justified support;
  (2) `maintain-docs` Stop hook REMOVED, skill is manual-only and gated on naming an incident
  plus what it cost; (3) `APPROVE_WITH_NITS` DELETED — two verdicts, one round, and a review may
  NEVER create a ticket; docs/test-only PRs get no reviewer; (4) agents may create tickets only
  for bugs reproduced in a built exe, everything else is Sponsor-gated; (5) blanket
  read-all-12-docs pre-read replaced by a per-task-class routing table; (6) testing bar KEPT
  intact but testing the test infrastructure is banned, and verify-captures ship CI-wired or not
  at all; (7) away/unattended mode OFF until three feats ship; (8) STATE.md slimmed to a resume
  header, away-queue + decisions-while-away archived to `team/log/`; (9) kill switch armed — any
  calendar week with zero `feat` merges retires the standing team; (10) next destination is
  closing out the weapon/combat line (PR #351).
- Why: measured on `origin/main` 2026-08-02 — last `feat` was 2026-07-22 (`0dc4844`), and the 79
  commits since were 47 docs, 12 chore, 10 fix, 8 test, 1 spike, 1 ci and ZERO feat. Nine of ten
  open PRs were non-gameplay. An unattended loop burned four rate-limit windows and then the
  weekly account cap producing documentation. Removing the anti-idle hook killed the DEMAND for
  work; these rulings kill the SUPPLY engines that manufactured it — an auto-firing docs skill
  whose three proposers were asked "what should we document" every tick, a review verdict that
  auto-filed a ticket (verified chain #383 to #394 to #401), unbounded ticket authoring, and
  docs run through the full code-review pipeline.
- Sponsor's framing, verbatim: "I want a well oiled team that does productive work, not work for
  the sake of work" and "if this is not possible a single session with a single agent works
  better than orchestration."
- Reversibility: reversible (all doctrine/prose + two settings edits) — but the kill switch is
  deliberately automatic so reversal-by-drift is detectable within a week.
- Affects: all roles, `CLAUDE.md`, `.claude/settings.json`, the `maintain-docs` skill, the
  dispatch template, `team/TESTING_BAR.md`, `team/STATE.md`, all six persona files, `TEAM.md`.

## 2026-08-18 — Enemy-HP pip-row NOT NEEDED: the body read answers AC6(c)
- Decided by: Sponsor
- Decision: The above-head enemy-HP pip-row (`86caxhfg2`) is closed as not-needed — with the #436
  body-level package (flash / flinch / dust / topple-death) in front of him, "is it nearly down?"
  is already answered. Given via popup immediately after PASSING the #436 r3 soak (build
  `zoned | 2026-08-15T07:12:23Z | 2fa9789`); #436 merged the same day (2026-08-18) on that soak,
  the second gameplay `feat` under the 2026-08-02 doctrine.
- Why: this is the deferred AC6(c) judgement item from the 2026-07-27 "Enemy-HP read SEQUENCED"
  decision — body feedback shipped first precisely so this question could be answered by feel
  rather than in the abstract. The body read carries the primary signal; the screen stays free of
  floating UI, which fits the calm low-poly tone.
- Reversibility: reversible — the ticket's spec (`team/uma-ux/hp-hud-polish-spec.md` §6) and its
  full AC survive on the closed ticket; reopening needs only a new Sponsor yes.
- Affects: `86caxhfg2` (complete/not-needed), combat UX surface, `86cah7yuh` (status-effect head
  cues no longer arbitrate against a pip-row).

## 2026-08-18 — Post-#436 priorities: swing direction FIRST, boar charge-snap ticketed behind it
- Decided by: Sponsor
- Decision: (1) The single most important issue is that held weapons/tools do not POINT in the
  right direction while swinging — filed as `86cb6v03j` (high), Drew dispatched same day.
  (2) The pre-existing boar charge-snap (no NavMesh at runtime — 3× `Failed to create agent` in
  the soak Player.log — so `BoarAI.MoveTowards`'s transform fallback snaps the boar onto the
  player's XZ, observed as "the player and boar are repositioned" in the 2026-08-14 failed soak)
  is ticketed at normal priority as `86cb6vjf8`, explicitly queued BEHIND the swing work, no
  dispatch until its turn.
- Why: (1) is the Sponsor's verbatim ranking the moment #436 closed; (2) preserves a triaged
  observation (Tess, 2026-08-14) without letting it jump the queue — it sits on charge feel the
  Sponsor previously soak-PASSED, so only the snap is the defect.
- Reversibility: reversible (priority ordering).
- Affects: Drew (dispatched on `86cb6v03j`), the swing/held-prop surface, `BoarAI`, the board.
