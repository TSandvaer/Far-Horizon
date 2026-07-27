# Heavy-Attack INPUT MODEL — Timing, Commitment, Read, Tiers, Generalization — `86caxh64q`

**Author:** Uma (UX / Visual / Audio Direction). **Reviewer:** Priya. **Status:** doc-only spec — no code, no
build. Feeds the implementation ticket `86cau6prr`, which may cite these sections directly.

> ### Which doc answers what — read this before citing either
> There are now **two** heavy-attack specs under `team/uma-ux/`. They are siblings, not versions:
>
> | Doc | Answers | Status |
> |---|---|---|
> | `heavy-attack-input-spec.md` (merged, PR #326) | **WHICH input** — the candidate/collision analysis (RMB / modifier+LMB / hold-LMB / dedicated key), the live binding table, the recommendation (`F`), and Q2/Q3/Q4 popup framing | on `main` |
> | **this doc** | **The MODEL behind that input** — the input's own state contract, the wind-up/cancel/cooldown commitment model, the heavy-vs-light READ, the per-tier dials, and the generalization path to the other four swing classes | new |
>
> §1 below restates #326's conclusion in one paragraph and **corrects two of its code citations against live
> `main`** — do not re-litigate the input choice here; do carry the corrections forward.

> ### 🔒 VOCABULARY AUTHORITY
> **`86cau6prr`'s pinned contract is authoritative over this spec.** Where the ticket names an identifier,
> field, test file, or parameter, the ticket's name wins and this doc is what gets corrected — not the other
> way round. Vocabulary settled in that contract and used verbatim here: `TriggerAttack(class, speed)` ·
> `Chop` / `WeaponClass` / `ChopSpeed` · `AnyState→AttackX` · `AnimId*` · `WeaponClassForAnimationId` ·
> `ActionsBlocked` · **`heavyWindupSeconds`** (the NEW field on `MeleeAttack` — *not* the resource verbs'
> `swingImpactDelaySeconds`) · **`HeavyWindupNormT`** · and the no-orphan-anim-id invariant in
> **`AttackSwingControllerTests` assertion 6** (*not* `WeaponSetTests`). The last two were corrected in the
> ticket body on 2026-07-27 during this spec's review round; this doc matches the corrected ticket.
> Names this spec *introduces* (`ShouldHeavyOnPress`, `RequestHeavyPress`, `WeaponDef.HeavyAnimationId`,
> `AnimIdSwordHeavyOverhead`, `WeaponClassSwordHeavy`, `heavy_*` ids, `PopulateHeavyAttack`) are **proposals** —
> if the impl ticket pins different ones, the ticket wins.

---

## 0. Tonal anchor — what the heavy should FEEL like

> **The heavy is the castaway deciding to *mean it*. He plants, takes the weapon up and back, and brings it
> down with his whole weight behind it — and for the length of that beat he has given up his footwork to do
> it. It reads "I put my back into that one," never violence. The light slash is conversation; the heavy is
> a sentence you can't take back. The trade is legible before the blow lands, not discovered afterward.**

Two consequences the whole spec hangs on:

1. **The commitment must be READABLE IN ADVANCE.** If the player only learns the heavy was slow *after* being
   gored, the mechanic reads as unfair rather than weighty. The wind-up is the promise; the recovery is the
   price. §5 is about making the promise visible.
2. **The cost is TIME, never a resource and never a gauge.** No stamina bar, no charge meter, no new HUD
   element. A calm HUD is a shipped commitment (`hp-hud-polish-spec.md` §3, pending PR #339); the heavy must
   not be the thing that adds a fourth gauge to it.

A child should feel *decisive and a little powerful* landing one — and should understand, without being told,
why they got clipped when they threw one at the wrong moment.

---

## 1. What #326 settled (do not re-litigate) + two ground-truth corrections

**Settled:** the second attack input is a **dedicated layout-agnostic letter key — `F`**, with `R` as the
soak alternate. RMB is rejected (it *is* the camera orbit-drag, and RMB-held is the light path's rejection
guard); modifier+LMB is rejected (Shift = run, Ctrl = crouch — both conventional modifiers are locomotion);
hold-LMB-then-release is rejected (it overloads the locked one-click-one-swing left path and collides with the
shipped hold-to-repeat chop/mine). Full evidence + the live binding table: `heavy-attack-input-spec.md` §2–§5.
**That analysis stands and this spec builds on it.** §3.1 re-tests the two candidates the ticket names
explicitly (RMB, hold-LMB) against ground truth found *after* #326 merged — both come out rejected again, on
one new argument each.

**Correction 1 — the guard's real name.** #326 cites `MeleeAttack.ShouldAttackOnClick`. The shipped pure guard
is **`MeleeAttack.ShouldSwingOnClick(weaponSelected, verbClaimedClick, uiPanelOpen, pointerOverUI, rmbHeld)`**
(`Assets/Scripts/Runtime/Combat/MeleeAttack.cs:154`) — five terms, and the second is `verbClaimedClick` (the
verb-wins-over-whiff arbitration), which #326 does not mention at all. §3.2 uses the live signature.

**Correction 2 — and this one is load-bearing: THE COMBAT PATH HAS NO IMPACT DELAY TODAY.**
`MeleeAttack.PerformAttack` fires the swing and calls `target.ApplyDamage(...)` **synchronously, in the same
frame as the click** (`PerformAttack` spans `MeleeAttack.cs:204-239`; the `ApplyDamage` call is at **:229**).
`swingImpactDelaySeconds` exists **only on the resource verbs**
— `ChopTree.cs:256`, `MineBoulder.cs:127`, `MineOre.cs:122` — never on `MeleeAttack`. So the ticket's default
*"impact lands at ~0.40 s"* and this spec's entire wind-up model **require a delayed-impact seam that does not
yet exist in combat**. That is the single largest implementation implication of the commitment model, and §4.4
specs it. *(`86cau6prr`'s 🎚️ block now carries this correction — the wind-up default is a new
**`heavyWindupSeconds`** field on `MeleeAttack`, updated 2026-07-27.)* It also carries a regression risk:
**the LIGHT path must keep its synchronous, zero-delay damage** — it shipped that way and the Sponsor has
soaked it.

---

## 2. Ground truth — the shipped attack path (quoted, not inferred)

| Fact | Value | Source |
|---|---|---|
| Light input | `Input.GetMouseButtonDown(0)` (+ `RequestAttackClick()` latch for headless/capture) | `MeleeAttack.cs:161,165` |
| Light guard (pure, static, testable) | `ShouldSwingOnClick(weaponSelected, verbClaimedClick, uiPanelOpen, pointerOverUI, rmbHeld)` | `MeleeAttack.cs:154` |
| Single-flight | `_lastAttackAt` + `baseAttackCooldown = 0.35 s`, divided by `weapon.AttackSpeed` | `MeleeAttack.cs:77,186-188` |
| Damage timing | **immediate, same frame as the click** | `PerformAttack` `MeleeAttack.cs:204-239`; `ApplyDamage` at `:229` |
| Whiff policy | one click = one swing **target or not**; damage alone is target-gated (soak-2 fix) | `MeleeAttack.cs:139-141` |
| Verb precedence | chop / boulder-mine / ore-mine claim the click when their tool is selected + target in range → the attack suppresses its whiff | `MeleeAttack.cs:251-268` |
| Swing routing | `TriggerAttack(weaponClass, speed)` → `WeaponClass` int + `ChopSpeed` float + the shared `Chop` trigger → `AnyState→AttackX` | `CastawayCharacter.cs:586` |
| Swing classes | axe 0 · pickaxe 1 · dagger 2 · spear 3 · **sword 4** | `CastawayCharacter.cs:265-269` |
| Per-class playback speed | axe 1.0 · pickaxe 1.5 · dagger 1.0 · spear 1.2 · **sword 1.5** | `CastawayCharacter.cs:281-290` |
| Sword light stats (iron) | damage 21 · reach 2.4 · attackSpeed 1.25 → light lockout ≈ **0.28 s** | `WeaponCatalog.cs:127-129` |
| Tier surface | `SurvivalNeed.DifficultyTier` (Easy/Medium/Hard); live read = `DeathHandler.tier`; enemies + `Health` already scale per tier | `Health.cs:122`, `BoarEnemy.cs:40-78` |
| Wind-up precedent (reuse the idiom) | `BoarAI` `Wander→Chase→Windup→Charge→Cooldown`, `windupSeconds = 0.7`, direction commits at the tell's END, `WindupNormT` normalized accessor for headless determinism | `BoarAI.cs:49,90-101,138-140` |

**✅ The heavy has its OWN clip, and its Animator state already exists — dormant.** *(Corrected in review: an
earlier draft of this spec claimed the reserved heavy and the axe light swing were the same motion. **That was
wrong** — it inferred the FBX from the runtime const `AxeSwingClipName = "CastawayAxeSwing"`
(`CastawayCharacter.cs:320`), which does not name its source file, and it predated `86caffwv5` landing five new
per-class swing FBXs. The editor file that *does* name sources refutes it.)*

| | Source FBX | Clip | Where it plays |
|---|---|---|---|
| **Axe LIGHT** | `Attack_Axe.fbx` (`CharacterAssetGen.cs:83`) | `CastawayAxeSwing` (`:253` — *"Attack_Axe.fbx (one-shot power chop)"*) | `AttackAxe` state, wired from `axeSwing = FindClip(AttackAxeFbxPath, AxeSwingClip)` (`:1213`), imported `:405` |
| **Reserved HEAVY** | `Melee_Attack.fbx` (`:77`) | **`CastawayMelee`** (`:248`), renamed on import by `ConfigureMeleeFbx` (`:1097-1116`) | the **dormant `Attack` state** (`:1381-1385`) |

Two distinct Mixamo takes, two distinct files. The reserved state's own comment (`CharacterAssetGen.cs:1373-1380`)
is explicit: its incoming `AnyState→Attack` transition was **removed** in `86caffwv5`, but *"the STATE + its clip
+ its return transitions are KEPT — it is RESERVED for the future sword HEAVY attack … dormant until the
heavy-attack ticket wires it. Do NOT delete, remap, or repurpose it."*

**Two consequences, both good for the impl:**
1. **The heavy's Animator state does not need to be created — it needs to be RE-WIRED** (§7.2 step 3). That is
   an even cleaner fit for the ticket's *extends-never-re-architects* constraint than adding a sixth state.
2. **The heavy is a genuinely different motion from every shipped light**, so the read (§5) rests on real
   animation difference, not on cadence and juice alone.

The overhead becomes the axe's motion on exactly one path: `WireAttackClass` passes `melee` as a **defensive
fallback used only if a per-class clip is missing** (`:1560-1570`, `state.motion = clip != null ? clip :
fallback;` + a `LogWarning`). `Attack_Axe.fbx` is present, so that is a degraded-ship guard, not the shipped
state.

**⚠ Do not slow the heavy down to sell weight.** `SwingSpeedSword = 1.5` carries the comment *"soak-5: sword
slash 'way too slow' at 1.0 → 1.5×"* — the Sponsor has already rejected a slow sword once. The heavy plays the
overhead at its **natural 1.0**, which is *already* a 1.5:1 cadence contrast against the light. Going below 1.0
re-opens a settled reject.

---

## 3. (a) The second attack input — the state contract

### 3.1 The two candidates the ticket names, re-tested against ground truth

| Candidate | New argument found after #326 | Verdict |
|---|---|---|
| **RMB (button 1)** | On top of #326's camera-orbit collision: `rmbHeld` is a **term inside the shipped light guard** (`MeleeAttack.cs:156`). Binding the heavy to RMB means the same physical button simultaneously *suppresses* the light and *fires* the heavy — a single button with two opposed roles in one truth-table. Unreviewable, and every orbit-drag becomes a heavy. | **REJECT** |
| **Hold-LMB → release** | On top of #326's overload objection: the light applies damage **synchronously on the press** (§1 Correction 2). A hold-to-disambiguate model must therefore *defer the light's damage to release* — i.e. re-time the one shipped, Sponsor-soaked timing in the game — or fire light-on-press and heavy-on-threshold, which is two strikes from one gesture. Both are regressions on locked behaviour. | **REJECT** |
| **Dedicated key `F`** (#326's pick) | New argument **for**: `F` is **orthogonal to the entire verb-arbitration table**. Because it is not the left button, it never interacts with `verbClaimedClick` — so an axe-holding player standing at a tree can chop with LMB and heavy-strike with `F`, and no precedence rule has to be invented. Every LMB-based candidate must extend that table; `F` doesn't touch it. | **KEEP** |

**Sponsor judges at soak:** `F` vs `R` (hand position — `F` sits under the WASD index finger, `R` above it).
Both bindable; ship `F` as default with `R` offered in the same soak.

### 3.2 The heavy's guard — a sibling of the light's, not a fork of it

Pure, static, dependency-free, EditMode-testable with no scene (mirrors the shipped idiom):

`ShouldHeavyOnPress(heavyCapableWeaponSelected, uiPanelOpen, pointerOverUI, actionsBlocked)`

- **`heavyCapableWeaponSelected`** — the selected belt item resolves to a `WeaponDef` whose heavy is *defined*
  (§7.2 data seam). Sword-first means exactly one weapon returns true at ship.
- **`uiPanelOpen` / `pointerOverUI`** — identical to the light (`UiInputGate.CaptureWorldInput`,
  `InventoryUI.IsPointerOverUI`). A keypress while a modal panel owns the screen must not swing.
- **`actionsBlocked`** — the stun term, *if* `86cah7yuh` has landed. One stun rule for both paths (the ticket's
  constraint); a blocked heavy is **dropped, not queued**, and flashes the stun chip once
  (`status-effect-readability-spec.md` §5.2, pending PR #339).
- **NOT gated on `rmbHeld`** — deliberate. A keyboard press is not a camera drag, so orbit-look + heavy-strike
  compose. This is the ergonomic win #326 identified and it survives review.
- **NOT gated on `verbClaimedClick`** — the heavy is a **combat verb only**. It never chops a tree, mines a
  boulder, or claims a resource target, at any distance (§7.3 names why this matters for the axe).
- **No target requirement.** A heavy at empty air **whiffs**, exactly as the light does. Auto-aborting a
  targetless heavy would be proximity logic wearing the active-input contract's clothes — forbidden
  (`[[active-input-not-proximity-auto-for-actions]]`).

### 3.3 One press = one heavy — the exclusivity + drop rules

1. **Edge-triggered only** (`Input.GetKeyDown`), plus a `RequestHeavyPress()`-style latch so headless PlayMode
   and the shipped capture drive the same path (the `RequestAttackClick` precedent, `MeleeAttack.cs:161`).
2. **Same-frame arbiter, heavy wins:** `if (heavyEdge) PerformHeavy(); else if (lightEdge) PerformAttack();`
   — mutually exclusive by construction, over **one shared `_lastAttackAt`** gate. A light and a heavy can
   never both fire in a frame, in either order.
3. **Never buffered.** A heavy press during any lockout (own recovery, or the light's cooldown) is **dropped
   silently** — not queued, not remembered. Buffering breaks "one press = one strike" and makes the
   commitment window feel like it lies. *(Contrast: `game-juice.md` T8 recommends input buffering — that is for
   **jump**, where a dropped input reads as unresponsive. For a committed attack, a queued input reads as the
   character acting on its own. Do not generalize T8 to this surface.)*
4. **Held key does not repeat.** Unlike LMB on chop/mine (`GetMouseButton` hold-to-repeat), holding `F` fires
   **once**. Release and press again for a second heavy.

---

## 4. (b) The commitment model — four phases, one lockout

### 4.1 The phases

| Phase | Default (medium) | What the player can do | What is true |
|---|---|---|---|
| **WIND-UP** | press → **0.40 s** | turn freely; translate at **0.4×** speed | Damage has NOT landed. The tell is on screen (§5.2). |
| **IMPACT** | 1 frame at wind-up end | — | Damage applies once; all impact juice fires here (§5.3). |
| **RECOVERY** | impact → **+0.55 s** | translate at **0.4×**, ramping to 1.0 across the phase | No attack of either kind can fire. This is the punish window. |
| **READY** | ≈ **0.95 s** after the press | everything | Next light or heavy accepted. |

**The contrast that makes the mechanic:** light-sword lockout ≈ **0.28 s** from click with damage on frame 0;
heavy lockout ≈ **0.95 s** from press with damage at 0.40 s. **~3.4× the commitment, and the first 0.40 s of it
buys nothing yet.** That ratio is the design; the absolute numbers are dials.

**Movement damping, not rooting.** Full rooting reads dead and fights the standing bar (`quality-bars.md` #2 —
motion defaults lively; `[[sponsor-prefers-natural-lively-motion]]`). Free turning + damped translation reads
"planted and committed" while staying alive. **Sponsor judges at soak:** 0.4× may read floaty (wants 0.25×) or
sticky (wants 0.6×). Per-tier row in §6.

### 4.2 Cancel policy — the heavy is NOT player-cancellable

**No cancel input, at any tier.** A cancellable heavy is a free option: you would throw it every time and bail
on read, which deletes the risk half of risk/reward and makes the tell meaningless. Commitment *is* the cost
(§0.2). This also keeps the input surface at one key — no cancel binding to find on a Danish layout.

**Three external interrupts, and only these:**

| Interrupt | Behaviour |
|---|---|
| **Stun** (`86cah7yuh`, if landed) | In-flight heavy **aborts**: no damage, no queue, one stun-chip flash. Same `ActionsBlocked` term the light reads — one stun rule, not two. |
| **Player death** | Aborts immediately; the death sequence owns the character. |
| **Target dies / leaves reach mid-wind-up** | The swing **completes and whiffs**. Do NOT re-target mid-flight and do NOT cancel — re-targeting is auto-aim, cancelling is the free option. Target resolution happens **once, at IMPACT** (§4.4). |

**Explicitly NOT interrupts:** **taking damage** — a flinch that eats your committed swing is the most
frustrating beat in melee games and it punishes the kid tier hardest. **This is a soak dial, not an invariant —
registered as §8.4b**, defaulted NO; a Hard-only `heavy_flinch_cancel` is a defensible tier dial if the Sponsor
wants it. Also not interrupts: jumping or moving (movement is damped, not forbidden); switching belt slots (the
swing already fired — let it land; the *next* press reads the new item).

### 4.3 Cooldown, not stamina

Recovery time **is** the cost. No stamina resource, no meter (§0.2; `86cau6prr` Q4-A). Implementation is the
shipped one — extend `_lastAttackAt` with a heavy-length lockout — **not** a second timing system.

### 4.4 The delayed-impact seam (the part that does not exist yet)

Per §1 Correction 2, combat damage is synchronous today. The heavy needs:

- **A phase timer on `MeleeAttack`** (`heavyWindupSeconds`, default 0.40, divided by the heavy's playback speed
  exactly as `ChopTree.cs:832` divides — reuse that arithmetic rather than inventing a second convention).
- **Target resolution at IMPACT, not at press.** `ResolveNearestTarget(weapon.Reach)` runs when the blow lands,
  so walking away from a wind-up genuinely misses. This is the honest reading of "the blow lands where the
  blade is," and it makes the tell mean something.
- **A normalized-progress accessor** — `HeavyWindupNormT` (0→1), anchored on `Time.time`, mirroring
  `BoarAI.WindupNormT` (`BoarAI.cs:138-140`). This is the *only* reliable way to drive and assert the phase
  headless, where `Time.deltaTime ≈ 0` (the documented trap in `procedural-animation-verbs.md`).
- **The light path untouched.** Light damage stays synchronous-on-click. Any refactor that routes both through
  one delayed path is a regression on soaked behaviour — name it in the PR body if attempted.

---

## 5. (c) How heavy-vs-light READS — three channels, all inside the calm caps

### 5.1 The channel split

Light and heavy must be distinguishable **before**, **at**, and **after** the blow. Most games only do "at."
Here, *before* is the important one — it is what makes the commitment fair.

| Channel | LIGHT (sword slash) | HEAVY (overhead) |
|---|---|---|
| **Before** (anticipation) | none — instant, damage on frame 0 | **0.40 s visible wind-up**: weapon up and back, whole-body cock; playback **1.0** vs the light's 1.5; one warm wind-up breath/whoosh |
| **At** (impact) | hit-stop **2 frames**, Impulse ~**0.06 u**, ≤**8** particles, a clean sweeping *cut* | hit-stop **3 frames** (the hard cap), Impulse ~**0.10 u**, ≤**12** particles, a deeper *thud* under the cut |
| **After** (aftermath) | recovery invisible (0.28 s, reads continuous) | **0.55 s settle** you can see: follow-through past the low point, then rise; movement ramps back |

**The strongest differentiator is one this table understates: the two motions travel on different AXES.** The
sword light is a sideways slash (`CastawaySwordSlash` ← `Attack_Sword.fbx`); the heavy is a downward overhead
(`CastawayMelee` ← `Melee_Attack.fbx`, Mixamo *"Standing Melee Attack Downward"*) — two distinct clips (§2).
That is the Sponsor's own framing of the feature, verbatim: *"sword should have a real slash (sideways swing)
it should also have heavy attack (swing from above)"* (`86cau6prr` Source). Horizontal-vs-vertical is legible
at gameplay framing in a single frame, before cadence or juice contributes anything.

Every value above is the **top of the shipped band, not a new band** — `game-juice.md` §1.2 caps hit-stop at 3
and `combat-cluster-design-brief.md` §1.2 already assigns axe/pickaxe 3 frames and sword/spear 2. **The heavy
takes the 3-frame row on its own merits** — it is the weightiest strike in the game, a full-body overhead
commit, and `86cau6prr` already defaults it to 3. *(An earlier draft justified the 3 by claiming the heavy
"is the axe motion" — that premise was refuted in review (§2); the value stands, the reasoning is replaced.)*
**Nothing here exceeds a cap; the heavy simply sits at the ceiling the light sits below.** That is the whole
differentiation budget, and it is enough.

### 5.2 The anticipation cue — motion first, one audio layer, nothing else

- **Primary: the motion.** The overhead's raise is a full-body silhouette change; at gameplay framing it reads
  without help. Trust it.
- **Secondary: one wind-up audio cue** at wind-up start — a warm *breath + cloth/haft whoosh*, 3–4 variants,
  ±10 % pitch jitter, SFX bus, quiet (~−18 dB, clearly under the impact). Material-honest per
  `quality-bars.md` #3: wood haft = dry creak-whoosh; iron = the same whoosh with a faint honed edge, never a
  metallic ring. `<deferred — no audio bus>` (matching `status-effect-readability-spec.md`, pending PR #339):
  spec the cue now, wire it when an SFX bus exists; the visual read must stand alone until then.
- **Explicitly NOT:** no charge meter, no HUD gauge, no reticle change, no weapon glow/trail shader, no
  time-dilation, no camera push-in, no vignette. Each would either add a gauge (§0.2), add a shader
  (out of budget), or break a `game-juice.md` §2 hard-don't.

### 5.3 Impact juice — top-of-band, and not one step past it

Fires on the **IMPACT frame** (§4.4), i.e. the delayed frame, not the press frame:

- **Hit-stop 3 frames**, `Time.timeScale = 0` then restore; camera + UI on `Time.unscaledDeltaTime`
  (`game-juice.md` §1.2). **Sponsor judges at soak:** does 3 read "solid" or "stunned"? He has an open
  question on exactly this in `86cau6prr`. If 3 reads stunned, drop to 2 — **never up to 4**.
- **Cinemachine Impulse ~0.10 u, single-frame decay.** Never `BasicMultiChannelPerlin`, never sustained
  (`game-juice.md` §2).
- **One pooled faceted puff, ≤12 particles**, `Unlit/Particle`, warm dust-brown, **every channel sub-1.0**
  (HDR-clamp, `style-guide-v2.md` §5). **Never red, never a spray, never gore** — at any tier. Pool via
  `ObjectPool<T>` + `OnParticleSystemStopped`.
- **Hit-flash on the struck enemy** via a `_HitFlash` float in `CBUFFER_START(UnityPerMaterial)` on a
  **per-enemy material instance** — **not** a `MaterialPropertyBlock` (MPB disqualifies the GPU Resident
  Drawer instanced path, `unity6-mastery.md` §2) and **not** a full-screen post-process Volume pulse
  (Render-Graph cost + tonally wrong, `game-juice.md` §2). Slightly longer/warmer than the light's, still
  sub-1.0, ~0.10 s eased out.
- **Impact audio:** the light's sword bank with a **deeper thud layered under**, ±10 % pitch. Reuse the bank —
  4–6 clips per material is the shipped floor (`combat-cluster-design-brief.md` §1.2). `<deferred — no audio bus>`.

### 5.4 The residual read question — checkable in-editor, before the soak, with no new art

The heavy has its own clip and its own axis (§2, §5.1), so the light-vs-heavy read is **not** at risk. One
honest residual remains: `CastawayMelee` (the heavy) and `CastawayAxeSwing` (the axe light) are **both downward
chop-family takes** — `Attack_Axe.fbx` is commented *"one-shot power chop"* (`CharacterAssetGen.cs:253`) and
`Melee_Attack.fbx` is Mixamo *"Standing Melee Attack Downward"*. A player who has been chopping trees and then
throws a sword heavy sees two downward swings. **Do they read as distinctly different motions, or as the same
swing twice?**

**This needs no new art and no pre-implementation decision.** Both clips are in the tree today. The check is an
**in-editor A/B: play `CastawayMelee` and `CastawayAxeSwing` back-to-back on the v4 rig and look.** Do it during
implementation, before the build goes out for soak, and put the answer in the Self-Test Report.

- **If they read distinctly** (the expected outcome — different Mixamo takes, different weapon in hand, 1.0 vs
  1.0 playback but different arcs): ship as-is, nothing further.
- **If they read as the same swing:** the *first* lever is the ones already spec'd — the prop, the wind-up cue
  (§5.2), the top-of-band impact (§5.3), the visible 0.55 s settle (§4.1). **Sourcing a dedicated cleave clip is
  the post-soak escape route, not a pre-impl step** — and if it is ever reached, the fix is the *motion*, never
  more amplitude. Cranking juice to paper over a motion problem is how a calm game turns loud.

---

## 6. (d) Per-tier dials — easy / medium / hard

**The heavy exists on all three tiers, with identical cues, colours, motion and audio.** Only generosity
changes. Nothing gets scarier-looking on hard (the standing rule across the combat cluster —
`combat-cluster-design-brief.md` §2.3, `status-effect-readability-spec.md` §7, pending PR #339). The tier
north-star is the Sponsor's own framing: *"difficulty level / scariness can be adjusted"* for
**children as well as adults** (`vision-far-horizon-game-concept.md`, line 1).

| Dial | Easy (kid) | Medium (baseline) | Hard (adult) | Why it is the tier axis |
|---|---|---|---|---|
| **Wind-up** | 0.40 s | 0.40 s | 0.40 s | **Flat.** The tell is the teaching; shortening it on easy would teach a timing that hard then breaks. |
| **Recovery / punish window** | **0.40 s** | 0.55 s | **0.75 s** | The real difficulty knob. Easy: a mistimed heavy costs a beat. Hard: it costs you the exchange. |
| **Move damping during wind-up + recovery** | **0.6×** | 0.4× | **0.25×** | How trapped the commitment feels. Easy stays mobile enough to walk out of a boar charge. |
| **Damage multiplier vs light** | 2.0× | 2.0× | 2.0× | **Deliberately flat by default — see the note below.** |
| **Hit-stop frames** | 3 | 3 | 3 | Flat. It is a tone value, not a difficulty value, and it is already at the cap. |

**⚠ Balance hygiene — do not double-dip the damage axis.** Enemy HP *already* scales per tier
(`BoarEnemy.BoarEasyMaxHp = 32` vs `BoarHardMaxHp = 50`) and `Health.damageTakenMul` scales per tier on the
target side (`Health.cs:122-133`). A per-tier heavy multiplier on top means three multiplicative tier axes on
one number, and the easy tier drifts to one-shotting everything. So the row is **exposed but defaulted equal**
across tiers; tune enemy HP first. **Sponsor judges at soak** if easy wants a real power bump anyway.

**Dead-knob rule (mandatory, shipped precedent).** Every per-tier slider must write **both** the active field
**and** the active tier's map entry — otherwise the next `ApplyDifficulty(...)` clobbers the live dial. This is
the documented `boar_*` pattern (`SettingsCatalog.cs`, boar tweakables comment) and the reason the dialed value
bakes into the preset (`[[verify-soak-builds-or-bake-and-judge]]`).

**Registry ids** — stable `snake_case`, registered by a **new dedicated `PopulateHeavyAttack`** method (the
shipped convention: each feature adds its own `Populate`, never grows the base signature):

| Id | Drives |
|---|---|
| `heavy_windup` | §4.1 wind-up seconds (per-tier row exists; flat by default) |
| `heavy_recovery` | §4.1 recovery seconds (**the primary tier dial**) |
| `heavy_move_damping` | §4.1 translation multiplier during wind-up + recovery |
| `heavy_damage_mul` | §6 multiplier vs the light (flat by default) |
| `heavy_hitstop_frames` | §5.3 — hard ceiling **3**, never settable to 4 |
| `heavy_windup_audio_enabled` | §5.2 — the cue's revert path once a bus exists |
| `heavy_enabled` | master off switch — the soak's one-click revert if the whole mechanic reads wrong |

---

## 7. (e) Sword-first, and how the model generalizes to the other four classes

### 7.1 Per-class heavy verdict

| Class | `WeaponClass` | Light today | Heavy verdict | Motion that would fit | What it needs |
|---|---|---|---|---|---|
| **Sword** | 4 | `CastawaySwordSlash` @ 1.5 (sideways) | **SHIP FIRST** | the reserved overhead `CastawayMelee` @ 1.0 (downward) | nothing new — clip **and** its dormant state are already in-repo |
| **Spear** | 3 | `CastawaySpearThrust` @ 1.2 | **best second candidate** | a committed two-hand lunge — reach + commitment is already the spear's identity, and it is the boar-matchup weapon | one new Mixamo clip |
| **Axe** | 0 | `CastawayAxeSwing` @ 1.0 (downward power chop) | **deferred — blocked on motion, not on model** | a visibly bigger commit than its light: a two-hand wind or a step-through cleave. Note the axe light is *already* a downward power chop, so an axe heavy has the least motion headroom of the five | one new clip; reusing `CastawayMelee` here would collide with the sword heavy |
| **Pickaxe** | 1 | `CastawayPickaxeSwing` @ 1.5 | **low priority** | — | it is a tool that can fight, not a fighting tool |
| **Dagger** | 2 | `CastawayDaggerStab` @ 1.0 | **NO heavy — by design** | — | the dagger's identity is tempo; a heavy dagger is a contradiction, and a "flurry" heavy would breach one-press-one-strike |

### 7.2 The data seam — roster expansion must be DATA, not a new input path

Extend the shipped routing; do not re-architect it (`86caxh64q` constraint):

1. **`WeaponDef` gains an optional `HeavyAnimationId`** (null = no heavy). `heavyCapableWeaponSelected`
   (§3.2) is exactly `HeavyAnimationId != null`. Sword-first = one non-null row.
2. **`WeaponCatalog` gains an `AnimId*` const per heavy** (e.g. `AnimIdSwordHeavyOverhead = "sword_heavy_overhead"`)
   and a matching row in `WeaponClassForAnimationId` → a **new `WeaponClass` int (5, then 6…)**. No orphan id
   may exist — that invariant lives in **`Assets/Tests/EditMode/AttackSwingControllerTests.cs` assertion 6**
   (lines 28 / 229 / 248-255), **not** in `WeaponSetTests.cs`, which carries no `AnimationId` assertion.
   *(Both this spec and `86cau6prr` said `WeaponSetTests` until 2026-07-27; the ticket is corrected and this
   matches it.)*
3. **`CastawayCharacter` gains one `WeaponClassSwordHeavy = 5` const and one `SwingSpeedForClass` case** (1.0).
   **The Animator state already exists — RE-WIRE it, do not add a sixth.** The dormant `Attack` state
   (`CharacterAssetGen.cs:1381-1385`) already holds `CastawayMelee` and its return transitions, and is
   `ChopSpeedParam`-driven; it is reserved for exactly this and its comment forbids deleting or repurposing it
   (`:1373-1380`). The wiring is **one incoming `AnyState→Attack` transition on (`Chop` && `WeaponClass == 5`)**
   — the same `WireAttackClass` idiom the five shipped classes use. *(If the impl prefers renaming that state to
   `AttackSwordHeavy` for symmetry with `AttackAxe`/`AttackSword`, that is a **rename of the existing state**,
   never a second state — the ticket's vocabulary governs the final name.)* **No new Animator layer, no
   AvatarMask, no second trigger system, no procedural swing**
   (`[[chop-swing-mixamo-clip-not-procedural]]`, `procedural-animation-verbs.md`).
4. **Adding the spear heavy later = one clip + one const + one row + one state.** Zero input changes, zero
   guard changes, zero timing-model changes. That is the test of whether this spec generalized correctly.

### 7.3 The trap this seam must avoid — a heavy is never a resource verb

Because `F` bypasses `verbClaimedClick` (§3.2), an axe-holding player next to a tree could press `F` and — if
the heavy were routed through the verb layer — chop the tree with a combat swing, double-yielding wood or
double-damaging a boar depending on precedence. **The heavy is combat-only: it resolves targets exclusively
through `ResolveNearestTarget(weapon.Reach)` over `Health` components and never touches
`ChopTree`/`MineBoulder`/`MineOre`.** State this as an explicit non-goal in the impl PR body; it is the kind of
thing that ships correct and then regresses when the axe heavy lands.

**Rig fact — GENERIC, not Humanoid.** The live v4 rig **and** `Melee_Attack.fbx` are both `animationType: 2` =
**GENERIC** (`v4/castaway_v4_rigged.fbx.meta:101`, `Melee_Attack.fbx.meta:130`). Do not set the clip to
Humanoid — that is the explode-to-a-cone trap on this scaled hierarchy (`86ca8rdkp`). `ConfigureMeleeFbx`
sets Generic + `CreateFromThisModel` deliberately (`CharacterAssetGen.cs:1097-1116`), so binding is by
transform-path bone name, and the clip is **already imported and bound against the v4 rig today** — what is
dormant is its Animator state's incoming transition (§2), not the binding. *(`86cau6prr`'s rig constraint said
"confirm the reserved clip's **Humanoid** avatar" until 2026-07-27; that was backwards and is now corrected in
the ticket to GENERIC with a grep-it-yourself instruction. Follow the corrected ticket.)*

---

## 8. Sponsor-judges-at-soak register (every feel call in this doc, in one place)

| # | Call | Spec'd default | Section |
|---|---|---|---|
| 1 | `F` vs `R` as the heavy key | `F`, `R` offered in the same soak | §3.1 |
| 2 | Commitment weight — total lockout ≈0.95 s vs the light's ≈0.28 s | 0.40 wind-up + 0.55 recovery | §4.1 |
| 3 | Movement damping during commitment | 0.4× (not rooted) | §4.1 |
| 4 | Player-cancel | **none** — commitment is the cost | §4.2 |
| 4b | **Flinch-cancel** — should taking damage mid-wind-up abort the heavy? | **NO** — a committed swing survives being hit | §4.2 |
| 5 | Does hit-stop 3 read "solid" or "stunned"? | 3 (the cap); drop to 2 if stunned, never 4 | §5.3 |
| 6 | Do the two downward takes (`CastawayMelee` heavy vs `CastawayAxeSwing` axe light) read as distinct motions? | ship on the reserved clip; **answer the in-editor A/B during impl, before the soak** — a dedicated cleave is the post-soak escape route only | §5.4 |
| 7 | Wind-up audio cue character (breath+whoosh, −18 dB) | spec'd, `<deferred — no audio bus>` | §5.2 |
| 8 | Should easy get a real damage bump, or is enemy HP the right axis? | flat 2.0×; tune enemy HP first | §6 |
| 9 | Spear second, or roster-wide sooner? | spear second | §7.1 |

**On 4b (flagged in review as decided-by-omission):** flinch-cancel is a standard design axis and the kid tier
is exactly where it gets argued, so it belongs in this register rather than buried in §4.2's prose. It is
spec'd **NO** on tonal grounds — losing a committed swing to a hit you could not react to is the most
frustrating beat in melee, and it punishes the easy tier hardest. Treated as a **soak dial, not an invariant**:
if the Sponsor wants hard-tier flinch-cancel, that is a defensible tier dial (a `heavy_flinch_cancel` bool,
Hard-only) and not a redesign.

---

## 9. Success tests the impl ticket should name (all EditMode-able except where noted)

1. **`ShouldHeavyOnPress` truth-table** — all 16 combinations of the four terms, pure/static, no scene.
2. **Same-frame exclusivity** — heavy edge + light edge in one frame ⇒ exactly **one** swing fires (heavy),
   over the shared `_lastAttackAt`.
3. **Drop-not-queue** — N heavy presses during recovery ⇒ **0** extra swings, and none fire late.
4. **Delayed impact** — damage lands at wind-up end, **not** at the press; asserted by stepping
   `HeavyWindupNormT` (headless-safe; `Time.deltaTime ≈ 0` makes a wall-clock assert flaky).
5. **Walk-out miss** — target leaves reach during wind-up ⇒ swing completes, **0** damage, no re-target.
6. **Light path unregressed** — light damage still applies on the click frame (guards the §4.4 refactor risk).
7. **Verb isolation** — with an axe selected and a tree in range, a heavy press deals **no** tree damage and
   yields **no** wood.
8. **Stun drop** (if `86cah7yuh` landed) — heavy press while blocked ⇒ 0 swings, 1 chip-flash event.
9. **No orphan anim id** — the new heavy `AnimId` maps to a `WeaponClass`; extend
   **`AttackSwingControllerTests` assertion 6** (`Assets/Tests/EditMode/AttackSwingControllerTests.cs`,
   lines 28 / 229 / 248-255), **not** `WeaponSetTests`.
10. **Tier dial writes both** active field and active-tier map entry (dead-knob guard).
11. **Hit-stop ceiling** — `heavy_hitstop_frames` cannot be set above 3.
12. **Shipped-build capture** — the heavy swing + its visible settle captured from the **built exe**, windowed
    (the editor-vs-runtime divergence class). The soak is the real interaction gate.

---

## 10. Predict-Before-Soak (the impl author's Self-Test Report carries this)

> "Pressing **`F`** with the sword equipped plays the overhead **once** at 1.0 speed and returns to idle. No
> damage lands for the first **~0.40 s**; the blow lands when the blade reaches the low point, dealing **~2×**
> the light slash with hit-stop **3 frames**, a ≤12-particle warm puff, and a ~0.10 u Impulse. For **~0.95 s**
> total, neither a light left-click nor a second `F` does anything (dropped, not queued), and translation is
> damped to 0.4× while turning stays free. Walking out of reach during the wind-up produces a whiff with **0**
> damage. The heavy reads as a committed power strike — calm-tone, no red, no shake."
>
> **Bounded convergence:** bars tested — **#2** (lively motion), **#7** (3 tiers). Bars NOT tested — **#3**
> (material honesty: the wind-up + impact audio are `<deferred — no audio bus>`, so the material read is
> unverified), **#5** (no mesh/world surface touched). A refuted prediction is a finding, not a re-fix prompt.

---

## 11. Out of scope

Implementation (`86cau6prr`). Damage **numbers** balancing beyond the multiplier's default (dev-ticket dials).
New weapon types (`86cah7ym9`). Sourcing new heavy clips for spear/axe (§7.1 — follow-ups). The audio bus
itself. Enemy heavies / enemy wind-up changes (the boar's telegraph is shipped and untouched). Any charge
meter or stamina system (Q3-A / Q4-A, already answered `A` in `heavy-attack-input-spec.md` §6).

---

## 12. Decision drafts (for Priya's `DECISIONS.md` batch — I do not edit that file)

- **Decision draft:** "Heavy-attack commitment model (`86caxh64q`): the heavy is **not player-cancellable** —
  commitment is the entire cost. Four phases (wind-up 0.40 s → impact → recovery 0.55 s → ready ≈0.95 s), vs the
  light sword's ≈0.28 s lockout. Cost is TIME, never stamina and never a gauge. Interrupts limited to stun and
  death; taking damage does **not** cancel a committed swing (kid-tier frustration). **Default pending the
  Sponsor soak — no-player-cancel is §8.4, no-flinch-cancel is §8.4b, commitment weight is §8.2.**"
- **Decision draft:** "Heavy-attack requires a **delayed-impact seam on `MeleeAttack`** that does not exist
  today — combat damage is applied synchronously in the click frame (`PerformAttack` `MeleeAttack.cs:204-239`,
  `ApplyDamage` at `:229`) and `swingImpactDelaySeconds` lives only on the resource verbs (`ChopTree.cs:256`,
  `MineBoulder.cs:127`, `MineOre.cs:122`). The new field is **`heavyWindupSeconds`** on `MeleeAttack`; target
  resolution moves to the impact frame **for the heavy only**; the light path keeps synchronous damage (soaked
  behaviour). Verified code fact — **no soak qualifier needed.**"
- **Decision draft:** "Heavy per-tier axis is **recovery length + movement damping**, not damage. The damage
  multiplier row is exposed but defaulted flat at 2.0× across tiers, because enemy HP (`BoarEnemy` 32/50) and
  `Health.damageTakenMul` already scale per tier — three multiplicative axes on one number would drift easy to
  one-shotting. **Default pending the Sponsor soak (§8.8).**"
- **Decision draft:** "Heavy roster path: **sword first, spear second**; dagger gets **no heavy** by design
  (tempo is its identity); axe/pickaxe heavies are blocked on a new clip, not on the model. Expansion is a
  data row + one Animator state (`WeaponDef.HeavyAnimationId` → new `AnimId` → new `WeaponClass` 5+), never a
  new input path. **Default pending the Sponsor soak (§8.9).**"
- **Decision draft:** "Heavy-attack clip provenance (corrects a refuted claim in this spec's first draft): the
  reserved heavy is **`CastawayMelee` ← `Melee_Attack.fbx`**, DISTINCT from the axe light
  **`CastawayAxeSwing` ← `Attack_Axe.fbx`** (`CharacterAssetGen.cs:77,83,248,253,405,1213`). Its Animator state
  **already exists and is dormant** (`:1373-1385`) — the impl **re-wires** that reserved state with one
  `AnyState→Attack` transition on (`Chop` && `WeaponClass == 5`) rather than adding a sixth state. The overhead
  is the axe's motion only via `WireAttackClass`'s missing-clip fallback (`:1560-1570`), which is a
  degraded-ship guard, not the shipped state. Verified code fact — **no soak qualifier needed.**"

---

## Cross-references

- **Tickets:** `86caxh64q` (this spec) · `86cau6prr` (consumer — the mechanic; cites these sections) ·
  `86cah7ym9` (roster) · `86cah7yuh` (stun / `ActionsBlocked` term) · `86caffwv5` (light swings — shipped, owns
  the chain) · `86cavj8pf` (standing PlayMode reds on main — diff before blaming a PR).
- **Sibling specs:** `team/uma-ux/heavy-attack-input-spec.md` (the input CHOICE — §1 corrects two of its code
  citations) · `team/uma-ux/combat-cluster-design-brief.md` §0 / §1.1 / §1.2 / §2.3 ·
  `team/uma-ux/hp-hud-polish-spec.md` §3 and `team/uma-ux/status-effect-readability-spec.md` §5.2 / §7
  (**both pending PR #339, unmerged**) · `team/uma-ux/style-guide-v2.md` §5 (HDR clamp) ·
  `team/drew-dev/weapon-swings-clip-plan.md` §1.1 (GENERIC-not-Humanoid).
- **Docs:** `.claude/docs/game-juice.md` §0 / §1.2 / §2 · `.claude/docs/procedural-animation-verbs.md` ·
  `.claude/docs/unity6-mastery.md` §2 (GRD / MPB) · `.claude/docs/vision-far-horizon-game-concept.md` ·
  `team/quality-bars.md` #2 / #3 / #7 · `team/TESTING_BAR.md` (Predict-Before-Soak + bounded convergence).
- **Code (read-only, cited):** `Assets/Scripts/Editor/CharacterAssetGen.cs` (clip provenance §2 + the dormant
  reserved state + `WireAttackClass`) · `Assets/Tests/EditMode/AttackSwingControllerTests.cs` (assertion 6, the
  no-orphan-anim-id invariant) · `Assets/Scripts/Runtime/Combat/MeleeAttack.cs` ·
  `Assets/Scripts/Runtime/Combat/WeaponCatalog.cs` · `Assets/Scripts/Runtime/CastawayCharacter.cs` ·
  `Assets/Scripts/Runtime/Combat/Health.cs` · `Assets/Scripts/Runtime/Combat/BoarAI.cs` (wind-up idiom) ·
  `Assets/Scripts/Runtime/Combat/BoarEnemy.cs` · `Assets/Scripts/Runtime/ChopTree.cs` (impact-delay arithmetic) ·
  `Assets/Scripts/Runtime/Settings/SettingsCatalog.cs` (registry-id convention).
- **Memories / conventions:** `[[active-input-not-proximity-auto-for-actions]]` ·
  `[[sponsor-danish-keyboard-layout]]` · `[[chop-swing-mixamo-clip-not-procedural]]` ·
  `[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]` · `[[difficulty-settings-easy-medium-hard]]` ·
  `[[sponsor-prefers-natural-lively-motion]]` · `[[verify-soak-builds-or-bake-and-judge]]` ·
  `[[castaway-v4-blocky-handmodel-passed-lookdev]]` (never Blender-re-export the rigged FBX).
- **Provenance note:** `86cau6prr`'s body cites a `DECISIONS.md` 2026-07-27 next-wave entry. As of this
  branch's base (`99035d1`) **no 2026-07-27 entry exists on `main`** — Priya batches decisions, so it is
  presumably pending. Cited here as *the ticket's reference*, not as a doc I read.

---

## Sponsor-input items (for the popup — decided by the Sponsor, not here)

All ten live in §8 with their spec'd defaults. **None of them block implementation** — every one has a
defensible default the dev can build against and the soak can then move.

**Exactly ONE is a genuine pre-implementation call:**

1. **§8.2 — commitment weight.** ≈0.95 s total lockout (0.40 wind-up + 0.55 recovery) against the light's
   ≈0.28 s is a real beat of vulnerability, and it is a **build-shaping number, not a tuning number** — the
   phase structure in §4.1 is what gets implemented. If ≈0.95 s reads as too punishing for the kid tier even at
   easy's 0.40 s recovery, the defaults should move *before* the impl dispatch.

**Everything else is a soak dial** — including the read question at §8.6, which an earlier draft wrongly
elevated to a pre-impl art-sourcing decision on a refuted premise. The heavy has its own clip and its own
Animator state already in-repo (§2); the residual *"do the two downward takes read distinctly?"* question is
answered by an **in-editor A/B of two existing clips during implementation** (§5.4), needs no new art, and
gates nothing. Do not put an asset-sourcing question in front of the Sponsor for it.
