# Game Juice Concepts — Far Horizon

## Question

Which "game juice" techniques will most elevate Far Horizon's chunky low-poly survival feel for
the near-term loop (chop wood, pick up items, need-meter feedback), without clashing with the
calm/hopeful tone or the Unity 6 / URP / Windows desktop performance budget?

## Bottom line

Five techniques deliver the highest impact at the lowest implementation cost for the M-U2 loop.
Tone-calibrated application is the key constraint: amplitude must stay in the "lively, not violent"
register that matches the art direction (toy-like, saturated, calm hopeful journey). Hit-stop and
easing curves are the highest-leverage starting point — zero GC pressure, no assets needed.
Camera impulse and pooled particle bursts are next. Squash/stretch on pickups and UI transitions
is the finishing layer. Screen-shake-only is NOT the lead technique here: overdone shake clashes
directly with the calm-journey north star.

---

## Evidence

### E1 — GameAnalytics "Squeezing More Juice Out of Your Game Design"
- **Source:** GameAnalytics Blog, gameanalytics.com/blog/squeezing-more-juice-out-of-your-game-design (2024, retrieved 2026-06-18)
- **Strength:** Moderate — well-sourced practitioner write-up; cites shipped indie titles (Jelly Jump, Super Time Force)
- **Key claims:**
  - "The more common an action, the simpler the juice" — calibrate intensity to action frequency.
  - Easing functions are described as the single technique closest to mandatory: "nothing in the real world moves in a linear way."
  - Hit-stop ("pausing the game for a split second") reinforces impacts; cited alongside particle collision bursts as a pair.
  - Audio layering (multiple sound layers per action) creates perceived quality disproportionate to visual cost.
  - Overriding principle: **juice must reinforce existing mechanics, not distract from them**.

### E2 — Gamedeveloper.com "3 Game Juice Techniques from Slime Road"
- **Source:** Game Developer (formerly Gamasutra), gamedeveloper.com/design/3-game-juice-techniques-from-slime-road (retrieved 2026-06-18)
- **Strength:** Moderate — practitioner postmortem of a shipped mobile game; specific implementation described
- **Key claims:**
  - All three techniques used in Slime Road — easing on every transform, animate-in (never instant appearance), and particle celebration on scoring — are lightweight and engine-agnostic.
  - "Progressive appearance" (grow from scale 0, staggered, on any UI/world event) costs ~0 draw-calls and drives outsized feel improvement.
  - Particle celebration is most powerful at **scoring/reward moments**, not ambient traversal.

### E3 — HackRead "The Juice Factor: Designing Game Feel"
- **Source:** hackread.com/the-juice-factor-designing-game-feel/ (2024, retrieved 2026-06-18)
- **Strength:** Moderate — well-structured practitioner article; references Vampire Survivors as shipped evidence
- **Key claims:**
  - Hit-stop at 3–5 frames registers impact and creates resistance; referenced as near-universal in action/survival games.
  - **Proportional application rule:** minor events → subtle jitter; major events → moderate shake. Never uniform intensity.
  - Screen shake is cited as effective but also the easiest to over-apply — explicit warning.
  - Input forgiveness (coyote time, input buffering) improves *perceived* responsiveness without any visual cost.

### E4 — The Design Lab Blog "Making Gameplay Irresistibly Satisfying Using Game Juice"
- **Source:** thedesignlab.blog/2025/01/06/making-gameplay-irresistibly-satisfying-using-game-juice/ (January 2025, retrieved 2026-06-18)
- **Strength:** Moderate — accessible practitioner article; cites Astro Bot as shipped example
- **Key claims:**
  - Immediate feedback is "the cornerstone of game juice" — every action needs at least one multi-sensory response.
  - Squash and stretch (anticipation, impact exaggeration, elastic transitions) is listed as a named technique across all major juice frameworks.
  - Positions juice as a design philosophy for emotional resonance, not just visual polish.

### E5 — Cinemachine 3.x Official Docs — Noise and Impulse
- **Source:** Unity Official Documentation, docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/setup-apply-noise.html (Unity 6, retrieved 2026-06-18)
- **Strength:** Strong — official Unity package documentation
- **Key claims:**
  - For sudden event-triggered shake (axe impact, item pickup), the docs **explicitly recommend Impulse over Noise**: "For sudden shakes (e.g. in response to events like explosions), we recommend the use of Impulse rather than Noise."
  - Cinemachine 3.x (shipped with Unity 6) uses `CinemachineImpulseSource.GenerateImpulse()` — one method call; no sustained processing cost when idle.
  - Continuous noise (hand-held feel) uses BasicMultiChannelPerlin with amplitude/frequency tuning — always-on cost, suitable for ambient feel, not event feedback.

### E6 — Unity 6 Official Docs — ObjectPool and Particle Pooling
- **Source:** Unity Official Documentation, docs.unity3d.com/6000.4/Documentation/Manual/performance-reusable-code.html + scripting API (retrieved 2026-06-18)
- **Strength:** Strong — official Unity 6 documentation
- **Key claims:**
  - `UnityEngine.Pool.ObjectPool<T>` is the built-in zero-alloc pool (matches unity6-mastery.md §5 rule: use the built-in pool for frequently spawned objects).
  - Particle systems pooled via this pattern avoid per-chop `Instantiate`/`Destroy` GC spikes; reset via `ParticleSystem.Stop()` + callback on `OnParticleSystemStopped`.
  - Confirmed compatible with Unity 6000.4 (the project's exact version).

### E7 — DOTween Performance Evidence
- **Source:** dredyson.com DOTween technical breakdown (2024, retrieved 2026-06-18); dotween.demigiant.com official site
- **Strength:** Moderate — practitioner benchmarks, not official Unity docs
- **Key claims:**
  - DOTween is described as 400%+ faster than its predecessor, type-safe, avoids GC allocations in hot paths.
  - `AnimationCurve` in the Inspector is the preferred authoring path when designers need to tune feel without recompiling — directly applicable to easing tuning on pickup feedback.
  - DOTween free tier covers all transform tweening needed for squash/stretch and UI feedback.

### E8 — MoreMountains Feel Asset
- **Source:** feel.moremountains.com (retrieved 2026-06-18)
- **Strength:** Moderate — product documentation / asset store listing
- **Key claims:**
  - Bundles 150+ feedback types (camera shake, freeze frame / time-stop, particle bursts, transform tweens, audio, post-processing) into one component-based system.
  - Includes a "Time" category with freeze frame and timescale modification — the standard hit-stop implementation.
  - URP post-processing support is explicit.
  - **Cost:** paid Unity Asset Store asset (price varies; discounted with Corgi/TopDown Engine ownership). Within the project's ~100–200 USD/mo tooling budget IF the team opts in — but all described techniques are also achievable without it via Unity 6 built-ins + DOTween.

---

## Technique Ranking for Far Horizon M-U2

The five techniques below are ordered by: (impact on the near-term loop) × (implementation cost) ×
(tone-fit with calm/hopeful chunky low-poly aesthetic).

### 1. Easing curves on all transforms (HIGHEST PRIORITY)
**Impact:** High. **Cost:** Near-zero. **Tone-fit:** Perfect.

Every moving object in the game should use a non-linear curve. DOTween or Unity's `AnimationCurve`
(Inspector-tunable, no recompile). Apply to: item arc-to-inventory, need-meter bar fill, UI
panel open/close, axe swing follow-through. Ease-out on arrivals (object settles naturally);
ease-in-out on oscillating UI. This is E1's "closest to mandatory" finding — it is the cheapest
technique with the broadest footprint. No GC, no performance cost, no tone risk.

**Implementation path (Unity 6):** `AnimationCurve` field on the relevant MonoBehaviour, sampled
via `curve.Evaluate(t)` inside a coroutine or DOTween `.SetEase(Ease.OutBack)` / custom curve.
The DOTween free tier covers this. Pairs with the existing SO-event-channel architecture
(unity6-mastery.md §6) — a pickup SO event fires the tween.

### 2. Hit-stop on axe impact (HIGH PRIORITY)
**Impact:** High for the chop loop. **Cost:** Very low. **Tone-fit:** Good (calibrated to 2–3 frames).

A 2–3 frame timescale-to-zero freeze on axe-strikes-tree registers weight and physicality without
violence. E3 cites 3–5 frames as the standard range; for FH's calm tone, the low end (2–3 frames)
is correct — enough to feel satisfying, not enough to feel jarring. E1 and E3 both identify
hit-stop as the technique that most contributes to a "heavy tool" sensation.

**Implementation path:** `Time.timeScale = 0` for 2–3 frames in a coroutine on the `ImpactEvent`
SO channel; restore via `Time.timeScale = 1`. Exempt the camera and any UI elements from the
freeze (use `Time.unscaledDeltaTime` for camera and UI update loops — unity6-mastery.md §5
principle applies: multiply by `Time.deltaTime` vs `Time.unscaledDeltaTime` consciously). No
additional assets needed.

**Tone calibration note:** strictly 2–3 frames. The Proportional Application rule (E3) means chop
gets a brief freeze; need-meter critical-low gets none. Never apply hit-stop to ambient traversal.

### 3. Pooled particle burst on chop + item land (HIGH PRIORITY)
**Impact:** High (the most visually immediate feedback). **Cost:** Low with pooling.

Wood chips on axe impact + a small sparkle/dust puff when an item lands. The Slime Road postmortem
(E2) shows particle celebration at the reward moment drives outsized feel improvement. The
key constraint is pooling: per E6, `UnityEngine.Pool.ObjectPool<T>` with
`OnParticleSystemStopped` callback keeps this zero-alloc. One pool per particle effect prefab
(wood-chip burst, item-land puff).

**Art direction fit:** particles should read chunky / faceted in the same language as the world
meshes — avoid thin/wispy smoke or hyperreal sparks. 8–12 large-ish flat polygonal chips with a
brief bounce (easing technique #1 composited here) match the toy-like, saturated direction
(`art-direction.md` — blob trees, faceted rocks, bold readable silhouettes).

**Performance note:** GPU Resident Drawer (unity6-mastery.md §2) has a disqualifier for
`MaterialPropertyBlocks` on MeshRenderer. Particle systems use their own renderer path — they are
NOT MeshRenderers in the sense that triggers the GPU Resident Drawer disqualifier. Pool them; keep
particle material count low (one shared Unlit/Particle shader variant).

### 4. Cinemachine Impulse on axe impact + need-critical event (MEDIUM-HIGH PRIORITY)
**Impact:** Medium-high. **Cost:** Low (Cinemachine 3.x already ships with Unity 6).

A short, low-amplitude CinemachineImpulse on axe impact ("thud") and a softer lateral sway when
a need meter hits critical. E5 (official docs) explicitly recommends Impulse over Noise for
event-triggered shake. The calm-hopeful tone constraint is strictly amplitude: axe impact ~0.05–0.1
world units, single-frame decay. For comparison, a combat game might use 0.3–0.5 with a 0.4s
decay. FH's should feel like "the axe hit something solid", not "explosion nearby".

**Implementation path:** `CinemachineImpulseSource` component on the axe GameObject (or the tree
root); call `GenerateImpulse(force)` from the `ImpactEvent` SO channel listener. The orbital
camera (LateUpdate, as per unity6-mastery.md §5) passes the impulse through cleanly.

**Tone calibration note:** a need-critical impulse should be SOFTER than the axe impulse and
lateral-only (a gentle "lurch" of discomfort, not a camera punch). Tune the `RawSignalAsset`
profile for each use case separately.

### 5. Squash/stretch + progressive appearance on item pickup and UI transitions (MEDIUM PRIORITY)
**Impact:** Medium. **Cost:** Low. **Tone-fit:** Perfect — the toy-like chunky art direction
actively invites elastic, expressive transforms.

Item-pickup arc: the item animates from world position to belt/inventory slot with a scale
0 → 1.2 → 1.0 ease (overshoot/spring feel). Stagger multiple pickups by 40–60ms for a cascade
read. UI panel open: scale from 0 with `OutBack` ease. The Slime Road "progressive appearance"
principle (E2) — nothing instantaneous, everything grows into place — is the core idea.

The chunky character and prop style (art-direction.md: oversized heads, simplified forms) sets a
visual precedent for exaggerated-but-readable transforms. A 20% overshoot on a sphere-item is
legible and charming; the same on a realistic model would read broken.

**Implementation path:** DOTween free tier `DOScale(1.0f, 0.25f).SetEase(Ease.OutBack)` on pickup.
`UsageHints.DynamicTransform` on animated UI Toolkit elements (unity6-mastery.md §9 rule: animate
transforms, not layout properties). No GC if tweens are managed via `DOTween.Kill()` on the
object's `OnDisable`.

---

## What to AVOID for Far Horizon's tone

| Technique | Risk | Guidance |
|---|---|---|
| Heavy screen shake (high amplitude, >0.3s decay) | Clashes with calm/hopeful north star; induces discomfort | Use only Cinemachine Impulse at low amplitude (technique #4 above) |
| Hit-stop > 4 frames | Reads as violence/trauma, not craft | Cap at 2–3 frames for FH |
| Chromatic aberration / vignette pulse on hits | URP post-processing Volume transitions cost a separate URP pass (unity6-mastery.md §1: custom Renderer Features must use Render Graph two-stage model) | Defer until the polish milestone; not free |
| Continuous camera noise (hand-held always-on) | Always-on CPU cost; undermines the "steady, hopeful observer" camera feel | Reserve for stress/storm events if they appear in M-U3+ |
| Audio stingers on every action | Fatigue risk on the most common actions (chopping is repetitive) | E1: "the more common an action, the simpler the juice" — use layered ambient over frequent stingers |

---

## Application to Far Horizon

- **Near-term loop (M-U2: chop wood, pick up items, need meters):** techniques 1–4 fully apply.
  Easing and hit-stop ship first (no new assets); particles and impulse follow in the same sprint.
  Squash/stretch on pickups is a one-day add once the inventory-to-belt arc is wired.
- **Art direction compatibility:** the chunky-cartoon, toy-like, saturated world (art-direction.md
  board v2) tolerates and invites elastic transforms and bold particle shapes. The palette (warm,
  saturated) maps to bright high-contrast particle colors — use warm oranges/yellows for wood chips,
  cool teals for collected items (echoing the character's teal backdrop reference).
- **Performance budget:** all five techniques are zero-GC or pool-managed, and none conflict with
  the GPU Resident Drawer rules (unity6-mastery.md §2). The only marginal cost is particle
  draw-calls, absorbed by keeping particle material variants low and pool sizes small (8–12 items
  per pool at most for M-U2 frequency).
- **Feel / More Mountains asset:** optional quality-of-life purchase (paid, within budget) that
  would accelerate authoring of all five techniques via a designer-friendly UI. Not required —
  all techniques are achievable with Unity 6 built-ins + DOTween free + Cinemachine 3.x. Decision
  for Priya/Devon to make based on sprint velocity.
- **Godot-era context:** this project has no HTML5 / WebGL target (CLAUDE.md confirmed); the
  desktop-only constraint removes the WebGL GC/performance constraints from the Embergrave era.
  All techniques above are evaluated for Windows desktop only.

---

## Sources

- E1: GameAnalytics Blog — https://www.gameanalytics.com/blog/squeezing-more-juice-out-of-your-game-design
- E2: Game Developer (Slime Road postmortem) — https://www.gamedeveloper.com/design/3-game-juice-techniques-from-slime-road
- E3: HackRead "The Juice Factor" — https://hackread.com/the-juice-factor-designing-game-feel/
- E4: The Design Lab Blog — https://thedesignlab.blog/2025/01/06/making-gameplay-irresistibly-satisfying-using-game-juice/
- E5: Cinemachine 3.x Noise docs (official, Unity 6) — https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/setup-apply-noise.html
- E6: Unity 6 ObjectPool docs (official) — https://docs.unity3d.com/6000.4/Documentation/Manual/performance-reusable-code.html
- E7: DOTween technical breakdown — https://dredyson.com/the-hidden-truth-about-dotween-hotween-v2-a-unity-tween-engine-what-every-developer-needs-to-know-about-performance-migration-and-the-complete-technical-breakdown-that-changes-everything/
- E8: MoreMountains Feel — https://feel.moremountains.com/
