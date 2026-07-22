# Game Juice — Far Horizon Deep-Dive Research

## Question

Which "game juice" techniques will most elevate Far Horizon's chunky low-poly survival feel — across the
near-term loop (chop wood, light campfire, harvest berries, drink from hand, need-meter feedback) and the
movement layer (footstep, jump, landing) — without clashing with the calm/hopeful tone or the Unity 6 /
URP / Windows desktop performance budget?

---

## Bottom line

Easing curves on every transform and hit-stop on the axe strike are the highest-leverage starting point —
zero GC, no new assets, immediate perceptible quality gain. Cinemachine Impulse (low amplitude) and pooled
particle bursts follow. Squash/stretch on item pickups and need-bar transitions is the finishing layer. The
calm/hopeful "journey" north-star dictates amplitude throughout: Far Horizon is NOT a twitch game — juice
must read as "alive and satisfying," never as "violent or chaotic." Screen shake in its continuous or
high-amplitude form is explicitly contraindicated for this tone.

---

## Evidence

### E1 — GameAnalytics "Squeezing More Juice Out of Your Game Design"
- **Source:** GameAnalytics Blog, [https://www.gameanalytics.com/blog/squeezing-more-juice-out-of-your-game-design](https://www.gameanalytics.com/blog/squeezing-more-juice-out-of-your-game-design) (2024, retrieved 2026-06-30)
- **Strength:** Moderate — well-sourced practitioner write-up covering shipped indie titles; not a peer-reviewed study
- **Key claims:**
  - "The more common an action, the simpler the juice" — calibrate intensity to action frequency. Chopping is highly repetitive; its juice must be subtle enough not to fatigue.
  - Easing functions are described as the single technique closest to mandatory: "nothing in the real world moves in a linear way."
  - Hit-stop ("pausing the game for a split second") reinforces physical impacts; cited alongside particle bursts as a standard pairing.
  - Audio layering (multiple sound layers per action) creates perceived quality disproportionate to visual cost.
  - "Animate everything that matters" — continuous micro-animations (fire flicker, floating items, bobbing UI) establish world liveness at zero interaction cost.
  - Overriding principle: juice must reinforce existing mechanics, not distract from them.

### E2 — Gamedeveloper.com "3 Game Juice Techniques from Slime Road"
- **Source:** Game Developer (formerly Gamasutra), [https://www.gamedeveloper.com/design/3-game-juice-techniques-from-slime-road](https://www.gamedeveloper.com/design/3-game-juice-techniques-from-slime-road) (retrieved 2026-06-30)
- **Strength:** Moderate — practitioner postmortem of a shipped mobile game; specific named techniques with implementation described
- **Key claims:**
  - Three core techniques: (1) easing on every transform, (2) animate-in (never instant appearance — objects grow from scale 0, staggered), (3) particle celebration at reward moments.
  - "Progressive appearance" — any UI or world object growing from scale 0 with a stagger — costs ~0 draw-calls and drives outsized feel improvement.
  - Particle celebration is most powerful at **scoring/reward moments** (collecting an item, completing a need-fill), not at ambient traversal.
  - Slow-motion emphasis on key moments (jumps, impacts) was used for dramatic weight.
  - Confetti / burst cascades at significant completion milestones.

### E3 — Gamedeveloper.com "The Juice Factor: Designing Game Feel" (HackRead mirror)
- **Source:** [https://hackread.com/the-juice-factor-designing-game-feel/](https://hackread.com/the-juice-factor-designing-game-feel/) (2024, retrieved 2026-06-30)
- **Strength:** Moderate — well-structured practitioner article; references Vampire Survivors and Celeste as shipped evidence
- **Key claims:**
  - Hit-stop at 3–5 frames registers impact and creates a physical resistance sensation; cited as near-universal in action/survival games.
  - **Proportional application rule:** minor events → subtle jitter; major events → moderate shake. Never uniform intensity.
  - Screen shake is cited as effective but the easiest technique to over-apply; explicit warning given.
  - Squash and stretch: characters stretch vertically on jump, squash horizontally on landing. Also applies to projectiles, UI elements.
  - Dynamic FOV increase during sprint creates speed sensation.
  - Input forgiveness (coyote time, input buffering) improves *perceived* responsiveness with zero visual cost.
  - Audio multi-layering: footsteps require three distinct sounds (heel strike, surface material, clothing rustle).

### E4 — GameDevAcademy "How To Improve Game Feel In Three Easy Ways"
- **Source:** GameDev Academy, [https://gamedevacademy.org/game-feel-tutorial/](https://gamedevacademy.org/game-feel-tutorial/) (retrieved 2026-06-30)
- **Strength:** Moderate — Unity-specific tutorial with code examples; covers screen shake, particle effects, audio randomization
- **Key claims:**
  - Screen shake: animate camera position + Z-rotation for 0.1–0.3 seconds, randomize direction, use easing to taper off.
  - Animation juiciness via scale changes: spike to 1.2 over 1 frame, reset by frame 4 — creates visual impact without lengthy animation sequences.
  - Particle settings for celebratory bursts: 0.10s duration, disabled loop, random lifetime 0.5–1.5s, burst count 5–15.
  - Audio randomization: store clips in arrays, select via `Random.Range()` — prevents the "broken record" fatigue on repeated actions. Critical for chopping.
  - Set Animator transition duration to 0 for immediate responsiveness.

### E5 — Cinemachine 3.x Official Docs — Noise and Impulse
- **Source:** Unity Official Documentation, [https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/setup-apply-noise.html](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/setup-apply-noise.html) (Unity 6, retrieved 2026-06-30)
- **Strength:** Strong — official Unity package documentation
- **Key claims:**
  - For sudden event-triggered shake, the docs **explicitly recommend Impulse over Noise**: "For sudden shakes (e.g. in response to events like explosions), we recommend the use of Impulse rather than Noise."
  - `CinemachineImpulseSource.GenerateImpulse()` is one method call; zero sustained processing cost when idle.
  - Continuous Noise (BasicMultiChannelPerlin) is always-on CPU cost — suitable only for ambient feel, not event feedback.
  - Cinemachine 3.x ships bundled with Unity 6; no additional purchase needed.

### E6 — Unity 6 Official Docs — ObjectPool and Particle Pooling
- **Source:** Unity Official Documentation, [https://docs.unity3d.com/6000.4/Documentation/Manual/performance-reusable-code.html](https://docs.unity3d.com/6000.4/Documentation/Manual/performance-reusable-code.html) (retrieved 2026-06-30)
- **Strength:** Strong — official Unity 6 documentation
- **Key claims:**
  - `UnityEngine.Pool.ObjectPool<T>` is the built-in zero-alloc pool (matches unity6-mastery.md §5: use the built-in pool for frequently spawned objects).
  - Particle systems pooled via `OnParticleSystemStopped` callback — `ParticleSystem.Stop()` + return-to-pool — keep per-chop cost at near-zero alloc.
  - Confirmed compatible with Unity 6000.4 (the project's exact version).

### E7 — DOTween / PrimeTween Performance Analysis
- **Source:** Omitram "DOTween vs LeanTween vs PrimeTween" (2026), [https://omitram.com/unity-tweening-guide-dotween-leantween-primetween/](https://omitram.com/unity-tweening-guide-dotween-leantween-primetween/) (retrieved 2026-06-30)
- **Strength:** Moderate — independent benchmark comparison; not official Unity docs
- **Key claims:**
  - DOTween's Sequencing API chains movements, rotations, and color changes into choreographed animations — directly applicable to item pickup arc.
  - LeanTween uses internal object pooling to prevent GC stutters — useful if GC is a live concern.
  - PrimeTween (newer) avoids all GC allocations entirely via struct-based design — strong candidate for future adoption if tween GC shows in profiler.
  - DOTween free tier covers all transform tweening needed for the M-U2 loop. No paid license required.
  - `AnimationCurve` in the Inspector is the designer-tuning path — tune feel without recompiling.

### E8 — MoreMountains Feel Asset
- **Source:** feel.moremountains.com, [https://feel.moremountains.com/](https://feel.moremountains.com/) (retrieved 2026-06-30)
- **Strength:** Moderate — product documentation / asset store listing
- **Key claims:**
  - 150+ feedback types including freeze frame, timescale modification, Cinemachine shake, particle bursts, transform tweens, audio, and URP post-processing Volume effects.
  - The "Time" category provides a single-component freeze-frame implementation — the standard hit-stop solution.
  - Explicit URP support; compatible with Unity 6.
  - Paid Unity Asset Store asset (price varies); all described techniques are also achievable with Unity 6 built-ins + DOTween free + Cinemachine 3.x without this asset.

### E9 — Wayline "The Juice Problem: How Exaggerated Feedback is Harming Game Design"
- **Source:** Wayline.io, [https://www.wayline.io/blog/the-juice-problem-how-exaggerated-feedback-is-harming-game-design](https://www.wayline.io/blog/the-juice-problem-how-exaggerated-feedback-is-harming-game-design) (retrieved 2026-06-30)
- **Strength:** Moderate — opinion/design critique; useful as the counter-argument for proportionality
- **Key claims:**
  - Excessive juice "masks fundamental flaws in gameplay and substitutes superficial excitement for genuine depth."
  - Puzzle, strategy, and narrative/journey games benefit most from restraint; action games need purposeful feedback rather than maximalism.
  - The solution is "strategic" juice that echoes the core feel — not juice as a layer applied after the fact.
  - "Seizure-inducing light shows for level-ups" and "overwhelming visual noise in cutscenes" are explicit examples of juice gone wrong.
  - Tone-resonant juice examples: Sekiro's parry sound (strategic audio), fluid animations showing weight (animation-first), environmental interactions.

---

## Full Technique Catalog

Each technique is rated for **tone-fit** (calm/hopeful survival journey, kid-friendly), **perf impact** on the
Unity 6/URP/Windows desktop budget, and assigned a **Far Horizon surface** from the existing system vocabulary.

---

### T1 — Easing curves on all transforms
**What it is:** Non-linear interpolation functions (ease-out, ease-in-out, overshoot/spring) applied to any
moving value — position, scale, opacity, color. Nothing in the real world moves at constant velocity.

**When it helps:** Always, on everything that moves. Zero baseline cost.

**When it's noise:** Never — easing is the foundation, not an effect. Under-applying it (leaving linear lerps)
reads as "cheap" or "broken."

**Tone-fit:** Perfect. Easing conveys natural weight and life without any implied violence.

**Perf impact:** Zero GC if using DOTween with `SetEase()` or `AnimationCurve.Evaluate(t)` in a coroutine.
PrimeTween eliminates all GC allocations if this becomes a profiler concern.

**Far Horizon surfaces:**
- Need-bar fill (warmth / hunger / thirst): `ease-out` on fill, overshoot spring on bar-critical flash.
  `Assets/Scripts/UI/NeedBarUI.cs` or equivalent.
- Item pickup arc to inventory: scale 0 → 1.2 → 1.0 with `OutBack` ease. Fires on the SO pickup event.
- Campfire ignite: light intensity ramp from 0 → target using `ease-out`; fire size scale from 0 with spring.
  `Assets/Scripts/World/CampfireController.cs` or equivalent.
- Axe swing follow-through: the existing `HeldAxeRig` (order 100) already follows `CastawayArmPose` (order 50)
  — add an eased damping coefficient so the axe's world-space lag on windup and overshoot on strike reads as
  weighted momentum (`procedural-animation-verbs.md` §T1).
- UI panel open/close: scale from 0 + `OutBack` ease (unity6-mastery.md §9: animate transforms with
  `UsageHints.DynamicTransform`, NOT layout `width`/`height`).

---

### T2 — Hit-stop / freeze-frame on axe impact
**What it is:** Timescale-to-zero for 2–5 frames at the moment of a strike connecting. The player's brain
registers the impact; the brief pause creates a physical resistance sensation (E1, E3).

**When it helps:** Any action where a tool or body hits a solid surface. Most powerful on the axe chop because
chopping is the primary verb of the near-term loop.

**When it's noise:** Ambient traversal (walking, running). Never apply to footsteps or passive movement.

**Tone-fit:** Good if calibrated to 2–3 frames. At 4–5 frames it reads as "stunned / painful"; at 2–3 it
reads as "solid / satisfying craft." E3's proportional rule applies: a full tree-fell event can get 3 frames;
a mid-chop (not the kill-blow) gets 2.

**Perf impact:** Zero — `Time.timeScale = 0` for 2–3 frames in a coroutine. Camera and UI must use
`Time.unscaledDeltaTime` (already mandated in unity6-mastery.md §5).

**Far Horizon surfaces:**
- **Chop strike:** `ChopVerb` / `ChopTree.Chop()` fires the `ImpactEvent` SO channel. A listener MonoBehaviour
  does `StartCoroutine(HitStop(frames: 2))`. The Mixamo axe-attack clip's impact frame is the trigger point
  (chop-swing = Mixamo clip per the memory `[[chop-swing-mixamo-clip-not-procedural]]`).
- **Tree fell (final blow):** 3 frames + a slight larger particle burst (T3 below). A more significant
  moment deserves marginally more weight.

---

### T3 — Pooled particle burst on impact and item land
**What it is:** Short-lived particle system instances that celebrate an action — wood chips on axe impact,
sparkle/dust puff when an item lands on the ground, berry pop on harvest, water splash/droplets on drink.

**When it helps:** At discrete reward moments: impact of a tool hit, item landing, resource gained, need
partially filled.

**When it's noise:** Ambient traversal. Continuous ambient particles (persistent dust trail while running)
risk visual fatigue and overdraw. Reserve bursts for moments.

**Tone-fit:** Good if art-direction-consistent. The chunky-cartoon language (art-direction.md) calls for
**bold, readable, polygon-flavored particles** — think fat angular chips, not thin smoke wisps. Warm
orange/yellow for wood chips; warm teal highlight for water droplets (matching the World palette); bright
greens for berry harvest.

**Perf impact:** The critical constraint is pooling. Per E6 (official Unity 6 docs) and unity6-mastery.md §6:
use `UnityEngine.Pool.ObjectPool<T>` with `OnParticleSystemStopped` return-to-pool. Per-event `Instantiate`
/ `Destroy` causes GC spikes audible in the profiler. Particle systems use their own renderer — they are
NOT subject to the GPU Resident Drawer `MaterialPropertyBlock` disqualifier (unity6-mastery.md §2). Keep
material variants low: one shared `Unlit/Particle` shader variant covers all burst types.

**Far Horizon surfaces:**
- **Chop impact:** wood-chip burst (8–12 faceted chip meshes or particle quads; warm orange-brown; brief
  bounce arc). Triggered at the `ImpactEvent` moment (same SO channel as hit-stop).
- **Tree fell:** larger chip burst + a brief sawdust puff (wider spread, slower fade).
- **Berry harvest:** berry-pop burst (3–5 small green/red spheres bouncing outward). Triggered from the
  `HarvestVerb` system.
- **Drink from hand:** water droplet burst (4–6 teal teardrop quads, falling straight down from the
  hand position). Triggered from the `DrinkVerb` at each scoop. Low amplitude — this is a calm, quiet
  action.
- **Item land (world→ground):** small dust puff (3–4 particles; very brief; white/warm). Triggered when
  the physics object settles.
- **Need-bar fill tick:** a tiny sparkle or "+icon" float (single particle; optional, see T5).

---

### T4 — Cinemachine Impulse on axe impact and need-critical event
**What it is:** A short, low-amplitude camera impulse triggered by a discrete world event — a brief
directional jolt (not sustained shaking) conveying physical force. Cinemachine 3.x (ships with Unity 6)
provides `CinemachineImpulseSource` / `CinemachineImpulseListener` for exactly this (E5, official docs).

**When it helps:** The axe connecting with a tree (physical feedback). Optionally: the need meter hitting a
"critical low" threshold (a soft lateral lurch of discomfort, not a punch).

**When it's noise:** Ambient traversal, UI interactions, berry harvest, drink. These actions do not involve
physical force and do not warrant a camera response.

**Tone-fit:** Good at very low amplitude. "This axe hit something solid" reads differently from "explosion
nearby." Amplitude is the entire tuning variable.

  - Axe impact: ~0.05–0.10 world units amplitude, single-frame decay profile. Essentially invisible as
    shake; felt as grounded physical confirmation.
  - Need-critical event: softer, lateral-only profile (~0.03 units), slightly longer decay (~0.15s). A
    gentle lurch of discomfort, not a camera punch. Only at the critical threshold, not every tick.

**Perf impact:** Zero sustained cost. `CinemachineImpulseSource.GenerateImpulse()` fires and decays in
< 0.2s; idle state = zero processing (E5).

**Far Horizon surfaces:**
- `CinemachineImpulseSource` component on the axe GameObject (or attached to the tree root on the
  `ImpactEvent` SO channel listener).
- The orbital camera (`CinemachineOrbitalFollow` or equivalent) passes the impulse through automatically
  via `CinemachineImpulseListener` on the camera. The `LateUpdate` orbit-follow (unity6-mastery.md §5)
  is compatible with Cinemachine — they use the same pipeline.
- Separate `RawSignalAsset` profiles for the axe-impact and need-critical impulse shapes.

---

### T5 — Squash/stretch + progressive appearance on pickups and UI
**What it is:** Items animate from world position to inventory with a scale-spring (0 → 1.2 → 1.0,
`OutBack` ease); UI panels grow into view rather than snapping; need-bar transitions overshoot slightly
before settling. "Progressive appearance" means nothing instantaneous — everything grows into place (E2).

**When it helps:** Item pickup arc, need-bar fill when a significant threshold is crossed, panel opens,
the campfire sprite/flame growing when ignited.

**When it's noise:** Frequent small actions where animation time would feel laggy. A sub-100ms spring
is imperceptible as delay but registers as "alive."

**Tone-fit:** Perfect for the chunky-cartoon world. The art-direction board (oversized heads, simplified
blocky forms, toy-like) actively invites elastic, exaggerated-but-readable transforms. A 20% overshoot
on a low-poly item sphere is charming; restraint should come from DURATION, not elimination.

**Perf impact:** Zero GC if DOTween tweens are `Kill()`'d on `OnDisable`. UI Toolkit elements need
`UsageHints.DynamicTransform` on animated elements (unity6-mastery.md §9: animate `translate`/`scale`,
NOT `width`/`height`).

**Far Horizon surfaces:**
- Berry pickup: berry GameObject scales 0 → 1.2 → 1.0 over 0.2s (`OutBack`) as it flies to the HUD belt.
- Need-bar fill: each increment ease-out; bar-full moment gets a brief spring-overshoot (scale 1.0 → 1.1
  → 1.0 on the bar itself, ~0.15s). Driven by the SO event channel.
- HUD need-bar critical-low flash: pulsing scale (1.0 → 0.95 → 1.0, repeating) in addition to color change.
  Use `UsageHints.DynamicTransform` on the bar element.
- Campfire ignition: the campfire prefab scales from 0.7 → 1.05 → 1.0 over 0.35s when ignited (grows to
  life). The point light intensity eases simultaneously (T1).
- Panel open: any dialog/menu UI scales from 0 with `OutBack`; close scales to 0 with `InBack`.

---

### T6 — Audio variation and layering
**What it is:** Multiple sound clips per action played via `Random.Range()` selection, with slight pitch
variance per play, and layered sounds (impact layer + tail layer) for richer perceived physicality (E1, E3,
E4). A different clip each time prevents the "broken record" fatigue on repeated actions.

**When it helps:** All repeated actions. Especially critical for chopping (the highest-frequency action in
M-U2).

**When it's noise:** This is never a "noise" risk — varied audio is always a quality improvement.
The risk is insufficient variation: 1 clip = fatigue; 3–5 clips = acceptable; 6–8 clips = excellent.

**Tone-fit:** Perfect. Audio variety is invisible to the player but builds subconscious quality. The
calm journey tone is served by warm, woody, organic sounds — not metallic or violent.

**Perf impact:** Multiple `AudioClip` assets (small) + `AudioSource.PlayOneShot()` — near-zero. No pooling
needed for audio. GC concern is zero for AudioSource.

**Far Horizon surfaces:**
- **Chop strike:** 4–6 wood-impact clips at varying pitches; 1–2 axe-whoosh clips on windup. `PlayOneShot`
  with `Random.Range(0.9f, 1.1f)` pitch variation.
- **Tree fell:** a heavier woody impact clip (distinct from chop-strike); 1 only, not varied.
- **Berry harvest:** a soft, quiet organic "pick" sound (3–4 variants). Quieter than chopping — proportional
  to action weight (E1).
- **Drink from hand:** a gentle water sound (3–4 variants — trickle/slurp/splash at varying pitches). Each
  scoop triggers one.
- **Campfire ignite:** a satisfying flame-catch sound (a single clip is fine — this is a single rare event).
- **Footstep:** 4–6 surface-appropriate clips (grass, sand, stone), layered with a cloth rustle (E3: "heel
  strike, surface material, clothing rustle"). This is M-U3+ scope but the pattern applies now.
- **Need meter tick (low):** a soft ambient warning tone or a heartbeat-like pulse. Single clip, looping or
  triggered per tick. Should feel urgent but not frightening for a kid-friendly experience (quality-bars.md #7).

---

### T7 — Ambient micro-animation (world liveness)
**What it is:** Continuous subtle animations on idle world objects — campfire flicker (scale/intensity
oscillation), berry-bush sway, float-bobbing on collectible items, cloud movement. This is E1's "animate
everything that matters" applied to the static world.

**When it helps:** All idle/ambient objects. This is the technique that makes the world feel ALIVE (the
Sponsor's locked north-star: "world feels BIG and ALIVE"). A perfectly static world reads as a test scene.

**When it's noise:** Off-screen objects. Apply liveness animations only within the relevant activation
radius; disable on far/occluded instances.

**Tone-fit:** Perfect — "alive" is explicitly the target. Campfire flicker, bush sway, item float are all
calm, natural, warm signals consistent with the hopeful journey tone.

**Perf impact:** One concern: continuous per-object sin/cos scale oscillation in `Update()` can accumulate
if hundreds of objects run it simultaneously. Mitigation: (a) stagger phase per instance (seeded offset so
they don't all pulse in sync — extends the existing seeded-scatter pattern from lowpoly-quality.md §Rec 7);
(b) use coroutines that yield until within activation radius; (c) Unity's Animation system (a simple 2-frame
looping clip) is more performant than a scripted `Mathf.Sin` update for simple oscillations on static props.

**Far Horizon surfaces:**
- **Campfire:** point light intensity oscillates via sin curve (0.8–1.2× base intensity, 2Hz, seeded offset
  per campfire instance). Flame particle system scale pulses similarly. This is quality-bars.md #2 ("motion
  defaults lively — foam PULSES").
- **Collectible items on ground:** a gentle float-bob (±0.05u Y-axis, 0.8Hz) using a coroutine.
  `SurvivalItem.cs` or equivalent world-drop component.
- **Berry bushes:** a very subtle ±2° lean oscillation on the bush mesh. Stationary per quality-bars.md §Grass
  (2026-06-29: "only the trees up in the air move; bushes + grass stay still") — interpretation: no
  continuous sway/wave; but a TRIGGERED gentle lean on interact is within the spirit of the bar.
- **Water waves:** already ship as a moving animation (quality-bars.md #2: "water has MOVING waves") — do not
  regress; this is juice that already exists.

---

### T8 — Input responsiveness: coyote time + input buffering
**What it is:** Coyote time = a 5–10 frame grace window after the player walks off a ledge during which
the jump action still triggers. Input buffering = storing a jump/action input pressed slightly before
landing and executing it when the character touches down (E3). Neither is visible — both improve
*perceived* feel and eliminate frustration.

**When it helps:** Jump-based traversal. With WASD + jump (`Space`) now the locomotion system, players
who "miss" the jump by 2–3 frames will perceive the character as unresponsive without this.

**When it's noise:** Not applicable to non-movement actions (chopping, harvesting, drinking).

**Tone-fit:** Perfect — invisibly better. No visual change; pure feel improvement.

**Perf impact:** Zero. A frame counter + a bool flag.

**Far Horizon surfaces:**
- `PlayerController.cs` (or WASD locomotion script in the backlog tickets `86ca9yq2x`/`yq34`/`yq3q`):
  add `_coyoteTimeCounter` (decrements per frame after leaving ground; enables jump while > 0) and
  `_jumpBufferCounter` (set on jump-input press; consumed when grounded within N frames).
- This is a code-only change on the locomotion ticket — no new assets.

---

### T9 — Progressive appearance on UI and world-spawns
**What it is:** Any object entering the scene grows from scale 0 or fades in (staggered if multiple),
rather than popping into existence. Any object leaving scales to 0 or fades out. Slime Road postmortem
calls this "animate-in" and cites it as their single highest-impact juice change (E2).

**When it helps:** HUD elements appearing/disappearing, inventory items added, need bars first appearing,
loot spawning, the campfire appearing when built.

**When it's noise:** Very frequent small events where the 0.1–0.2s animation would feel laggy. A 0.15s
staggered cascade on a 5-item pickup feels good; a 0.15s fade-in on EVERY footstep particle would be
invisible anyway (sub-perception).

**Tone-fit:** Perfect — reinforces "alive world" without any violence.

**Far Horizon surfaces:**
- Campfire prefab instantiation: grows from scale 0.1 → 1.0 over 0.4s (`OutBack`). Fires on the
  `CampfireBuilt` SO event.
- HUD need-bars: fade + scale in from 0 on first appearance.
- Loot/world items that spawn (dropped wood, berries): scale from 0 → 1.0 on instantiation.

---

### T10 — Dynamic FOV / camera lean on sprint
**What it is:** A slight FOV increase (5–8 degrees) during sprinting and a brief camera lean into the
turn on sharp direction changes (E3). Creates speed sensation without any animation.

**When it helps:** Sprint feels fast and alive rather than just "running faster."

**When it's noise:** Standing idle, slow walk. FOV should return to base the moment sprint input ends.

**Tone-fit:** Good. Mild FOV variance is common in survival games (Subnautica, Valheim). Keeps the
"big alive world" read intact by making the player feel small and fast within it.

**Perf impact:** Zero — `Camera.fieldOfView` or Cinemachine `Lens.FieldOfView` tween per frame.

**Far Horizon surfaces:**
- Cinemachine camera controller (orbital follow) — add a sprint-triggered FOV tween via DOTween or
  a Cinemachine custom extension. Fires on `Sprint.started` / `Sprint.canceled` input events.
- This lives on the WASD locomotion backlog tickets; should be part of that implementation.

---

## Ranked Candidate Table (bang-per-effort for Far Horizon)

| Rank | Technique | Effort | Impact | Tone-fit | Perf risk | Primary surface |
|------|-----------|--------|--------|----------|-----------|-----------------|
| 1 | T1 — Easing curves everywhere | Low | Very High | Perfect | None | All transforms, need bars, campfire, pickup arc |
| 2 | T2 — Hit-stop on axe impact | Very Low | High | Good (2–3f cap) | None | `ChopTree.Chop()` ImpactEvent |
| 3 | T6 — Audio variation + layering | Low | High | Perfect | None | All repeated verbs; chopping especially |
| 4 | T3 — Pooled particle bursts | Medium | High | Good (style match needed) | Low (pool it) | Chop impact, berry harvest, drink, item land |
| 5 | T7 — Ambient micro-animation | Low | High | Perfect | Low (phase-stagger) | Campfire, collectibles, water |
| 6 | T4 — Cinemachine Impulse | Low | Medium-High | Good (amplitude guard) | None | Axe strike, need-critical |
| 7 | T9 — Progressive appearance | Low | Medium | Perfect | None | Campfire build, HUD, loot spawn |
| 8 — | T5 — Squash/stretch + overshoot | Low | Medium | Perfect | None | Item pickups, need-bar fill, panel open |
| 9 | T8 — Coyote time + input buffer | Very Low | Medium | Perfect | None | Jump / locomotion |
| 10 | T10 — Sprint FOV shift | Very Low | Low-Medium | Good | None | Cinemachine; locomotion tickets |

---

## What to AVOID — Tone and Perf contraindications

| Technique | Why avoid for Far Horizon | Guidance |
|-----------|---------------------------|----------|
| Continuous / high-amplitude screen shake | Clashes with the calm/hopeful north-star; induces discomfort and motion sickness in kids (E9 warns: puzzle/narrative games need restraint). NOT the same as Cinemachine Impulse. | Use only T4 (Cinemachine Impulse at micro amplitude). Never sustained camera noise. |
| Hit-stop > 3 frames | Reads as violence / trauma / damage, not "satisfying craft." E3 gives 3–5 as the action-game range; FH's calm tone sits at 2–3. | Cap strictly at 2–3 frames for chop; 3 only for tree-fell. |
| Chromatic aberration / lens distortion pulse | URP post-processing Volume changes require a separate Renderer Feature pass using the Render Graph two-stage model (unity6-mastery.md §1). Not free — adds a render pass per effect. Also tonally wrong: chromatic aberration reads as damage/trauma. | Defer until a dedicated "storm or danger" event if it ever appears in M-U3+. |
| Continuous camera noise (hand-held, always-on) | Always-on CPU cost from BasicMultiChannelPerlin; undermines the "steady, hopeful observer" camera feel (E5: Noise is the wrong tool for event-driven feedback). | Reserve for extreme weather event if it appears, never as ambient. |
| Heavy / thin-wispy particle effects | Overdraw on the ~600u ocean extent is already a concern (lowpoly-quality.md §Rec 3 note on overdraw). Thin wispy smoke also fights the chunky-cartoon palette. | Faceted / chunky particle shapes only; pool all systems; keep count ≤12 per burst. |
| Audio stingers on every common action | Fatigue risk on highest-frequency actions. E1: "the more common an action, the simpler the juice." | Varied short clips + pitch range instead of a single prominent stinger. Ambient layering > stingers for chopping. |
| Squash/stretch on the character body mesh | The character is a rigged Humanoid (Mixamo rig); non-uniform scale on a rigged mesh breaks skinning and desynchronizes `HeldAxeRig` bone positions (procedural-animation-verbs.md: the chain is additive bone-rotation offsets, not scale changes). | Squash/stretch on PROPS and UI only — never on the character mesh or rig. |
| MaterialPropertyBlocks on juice VFX MeshRenderers | Disqualifies those renderers from the GPU Resident Drawer instanced path (unity6-mastery.md §2). | Use particle systems (their own renderer path) or separate material instances for VFX. No MPBs on world-prop juice renderers. |
| Real-time shadowed point light for campfire intensity | 6 shadow-map render passes per light per frame (unity6-mastery.md §3). | Use unshadowed point light + baked emissive; animate the unshadowed light intensity for T7. |

---

## Application to Far Horizon — system-by-system mapping

### Chop verb (`ChopTree.cs`, `CastawayArmPose`, `HeldAxeRig`, Mixamo axe-attack clip)
The primary verb of M-U2. Maximum juice ROI here.
- T2 (hit-stop 2–3 frames) fires at the Mixamo clip's impact keyframe → `ImpactEvent` SO channel.
- T3 (wood-chip burst, 8–12 particles) fires on the same `ImpactEvent`.
- T4 (Cinemachine Impulse ~0.05–0.10 units) fires on the same `ImpactEvent`.
- T1 (axe follow-through ease) via `followDamp` tuning in `HeldAxeRig` — already flagged in
  procedural-animation-verbs.md §checklist ("keep `followDamp = 0` during fast chop-class swing");
  post-strike the damp can ease the axe back to carry position naturally.
- T6 (4–6 wood-impact clips + 1–2 whoosh clips, ±10% pitch) via AudioSource on the axe GameObject.

### Campfire (`CampfireController.cs`, campfire prefab)
A reward / safety moment — deserves its own juice signature.
- T9 (campfire builds with scale grow 0 → 1.05 → 1.0) on instantiation.
- T7 (flame flicker: unshadowed point light intensity oscillation, 1.5–2Hz, seeded phase) continuously.
- T1 (light intensity ease-out on ignite, 0 → target over 0.5s) on the ignite event.
- T6 (flame-catch audio, one distinct clip) on ignite.

### Berry harvest (`HarvestVerb`, berry bush system)
A quieter, calm action — lighter juice signature than chopping.
- T3 (berry-pop burst, 3–5 small spheres) on harvest.
- T6 (3–4 soft "pick" sound variants, ±8% pitch) on harvest.
- T5 (berry item scales into HUD belt: 0 → 1.1 → 1.0 over 0.18s) on pickup.
- NO hit-stop, NO camera impulse — this is a gentle action.

### Drink from hand (`DrinkVerb`, pond interaction)
The most delicate action — water, calm, small satisfaction. Minimal but lively.
- T3 (water droplet burst, 4–6 teardrop particles, teal, falling straight down from hand) on each scoop.
- T6 (3–4 water sound variants — trickle / slurp / splash at ±10% pitch) on each scoop.
- T5 (thirst bar fill tick ease-out; at bar-full: spring overshoot on bar scale).
- NO hit-stop, NO camera impulse, NO particles for ambient traversal at the pond edge.

### Jump / landing (`PlayerController.cs`, WASD locomotion backlog)
- T1 (ease the jump arc — acceleration on ascent, deceleration on descent via `CharacterController` or
  Rigidbody velocity curve).
- T8 (coyote time 5–8 frames, input buffer 5 frames).
- T10 (FOV +5° on sprint, eases back on sprint release).
- T3 (small dust puff on landing, 3–4 particles) — a very subtle landing confirmation.
- Future: footstep audio variation (T6, 4–6 variants per surface type) when surface-detection is wired.

### Need-meter HUD (`NeedBarUI.cs`, three bars: warmth / hunger / thirst)
- T1 (ease-out on every bar fill tick; spring on bar-critical → full recovery).
- T5 (bar-critical pulse: scale 1.0 → 0.95 → 1.0, repeating, plus warm color flash).
- T4 (need-critical camera impulse: soft lateral-only, ~0.03 units, for the "panic" moment).
- T9 (bars fade + scale in from 0 on first HUD appearance).
- T6 (soft warning tone on critical threshold — single calm clip, not a stinger, kid-friendly).

### Item pickup / world drops (generic)
- T9 (world-spawned items scale from 0 on instantiation).
- T5 (pickup arc: item flies to HUD with scale spring).
- T7 (float-bob on idle ground items: ±0.05u Y, 0.8Hz, staggered phase per instance).
- T3 (small dust puff on landing when item drops to ground).

---

## Unity 6/URP and shared-palette compliance summary

| Concern | Status |
|---------|--------|
| GPU Resident Drawer | Particle systems exempt from MPB disqualifier. No juice technique adds MPBs to world MeshRenderers. |
| SRP Batcher | Juice techniques do not add new shaders. Use one shared Unlit/Particle shader variant for all burst particles. |
| GC allocations | T1 (DOTween), T3 (pooled), T4 (Cinemachine), T5 (DOTween), all zero-GC or pool-managed. |
| Draw-call budget | Pooled particles (max ~12 per burst, brief duration) add marginal overdraw, absorbed by keeping pool sizes small. |
| Shared-palette ~1-draw-call | Juice particles use a separate Unlit/Particle material (not the world `LowPolyVertexColor` shader) — does NOT break the shared palette model for the main world geometry. |
| Character mesh scale | T5 squash/stretch applies ONLY to props and UI — never to the rigged Humanoid mesh. |
| Real-time shadowed point light | T7 campfire flicker uses UNSHADOWED point light (unity6-mastery.md §3: NEVER a shadowed point for campfire). |
| IL2CPP build stripping | None of the juice techniques use reflection or dynamic codegen. DOTween's free tier is IL2CPP-safe. |

---

## Sources

- E1: GameAnalytics Blog — https://www.gameanalytics.com/blog/squeezing-more-juice-out-of-your-game-design
- E2: Game Developer (Slime Road postmortem) — https://www.gamedeveloper.com/design/3-game-juice-techniques-from-slime-road
- E3: HackRead "The Juice Factor" — https://hackread.com/the-juice-factor-designing-game-feel/
- E4: GameDev Academy "How To Improve Game Feel" — https://gamedevacademy.org/game-feel-tutorial/
- E5: Cinemachine 3.x Official Docs (Unity 6) — https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/setup-apply-noise.html
- E6: Unity 6 ObjectPool Docs — https://docs.unity3d.com/6000.4/Documentation/Manual/performance-reusable-code.html
- E7: Omitram DOTween/PrimeTween comparison (2026) — https://omitram.com/unity-tweening-guide-dotween-leantween-primetween/
- E8: MoreMountains Feel — https://feel.moremountains.com/
- E9: Wayline "The Juice Problem" — https://www.wayline.io/blog/the-juice-problem-how-exaggerated-feedback-is-harming-game-design
