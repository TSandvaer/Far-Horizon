# Game Juice — Feel / Polish / Feedback Guardrails

**MANDATORY pre-work read for any feel / polish / feedback / "make it satisfying" dispatch**
(chop-impact feedback, pickup feel, need-bar transitions, campfire/world liveness, jump/landing feel).
This is the concise "adopt these in OUR code" checklist distilled from Erik's R&D note.
Full evidence, citations, per-technique tone/perf ratings, and the system-by-system surface mapping:
`team/erik-consult/game-juice-research.md`.
Cross-refs: `unity6-mastery.md` §2 (GPU Resident Drawer / MPB disqualifier) / §3 (no shadowed point light) / §5 (object pool + `unscaledDeltaTime`) / §9 (animate transforms, not layout); `procedural-animation-verbs.md` (the `CastawayArmPose`→`HeldAxeRig` chain — never scale the rig); `lowpoly-quality.md` (faceted/chunky particle shapes, seeded phase stagger); `quality-bars.md` (#2 motion-defaults-lively, #7 kid-friendly tone).

The north-star governs the whole doc: Far Horizon is a **calm, hopeful journey**, NOT a twitch game. Juice must read as **"alive and satisfying," never "violent or chaotic."** Amplitude is the entire tuning variable — under-juicing reads cheap, over-juicing breaks the tone. When in doubt, smaller.

---

## 0. Anchor in the tone before tuning a value — amplitude is the whole game

Every juice value is a **calm-tone amplitude cap**, tunable downward, never a license to crank up. Before shipping any feedback effect, state in one sentence what the player should FEEL ("the axe hit something solid," not "an explosion went off nearby"). If the effect reads louder than that sentence, it's miscalibrated — turn it down. All numeric values below are **defaults (Sponsor-soak tunes)**, not mandates; a feel/polish ticket pairs them with a Predict-Before-Soak line.

---

## 1. The five must-haves (highest bang-per-effort, ship-first order)

Ranked by (near-term-loop impact) × (low cost) × (calm-tone fit). The top of Erik's 10-technique ranked table:

1. **Easing on EVERYTHING that moves (T1).** Effort low, impact very-high, tone perfect, zero perf risk. No transform lerps linear — need-bar fill, pickup arc, campfire ignite ramp, UI panel open, axe follow-through all use an ease (`OutBack`/`ease-out`/spring). DOTween free tier or `AnimationCurve.Evaluate(t)`; PrimeTween if tween GC ever shows in the profiler. Under-applying easing (leaving linear) is the single most common "feels cheap" defect — easing is the foundation, not an effect.
2. **Hit-stop on the axe strike — 2–3 frames, capped (T2).** `Time.timeScale = 0` for **2 frames** mid-chop, **3 frames** on the tree-fell blow; restore to 1. Fires on the `ImpactEvent` SO channel at the Mixamo axe-attack clip's impact keyframe. Camera + UI must run on `Time.unscaledDeltaTime` so they don't freeze. **Hard cap 3 frames** — 4–5 reads as "stunned/painful," wrong for the tone.
3. **Audio variation + layering on repeated verbs (T6).** 4–6 wood-impact clips for chop + ±10% `Random.Range` pitch via `PlayOneShot`; 3–4 softer variants each for berry-pick and drink. 1 clip = "broken record" fatigue (worst on chopping, the highest-frequency verb); 3–5 acceptable, 6–8 excellent. Warm/woody/organic, never metallic. Near-zero perf, no pooling needed.
4. **Pooled, faceted particle bursts at reward moments (T3).** Wood chips on chop impact, berry-pop on harvest, teal water droplets on drink, dust puff on item-land. **Pool every system** via `UnityEngine.Pool.ObjectPool<T>` + `OnParticleSystemStopped` return — per-event `Instantiate`/`Destroy` spikes GC. Chunky/faceted/polygonal shapes (warm palette), NOT thin wispy smoke. ≤12 particles per burst. Bursts only — never ambient traversal.
5. **Ambient micro-animation for world liveness (T7).** Campfire light-intensity flicker (0.8–1.2× base, ~2Hz), collectible float-bob (±0.05u, 0.8Hz), water waves (already shipped — don't regress). **Seed a per-instance phase offset** so they don't pulse in sync (extends the seeded-scatter pattern); gate on activation radius. Respects `quality-bars.md` §Grass — bushes/grass stay still; only the trees-in-air move.

Next tier (apply where the surface exists): T4 Cinemachine **Impulse** (NOT Noise) at micro amplitude (~0.05–0.10u, single-frame decay) on axe impact; T5 squash/stretch + progressive-appearance on props/UI; T8 coyote-time + input-buffer on jump; T9 grow-from-scale-0 on spawns; T10 +5° sprint FOV. See the note for the full ranked table + surface mapping.

---

## 2. Hard don'ts — tone & perf contraindications

Each clashes with the calm/hopeful tone OR breaks a Unity 6/URP invariant. Do NOT ship these:

- **No sustained / high-amplitude screen shake.** Continuous camera Noise clashes with the calm north-star and risks motion-sickness for kids. Use ONLY Cinemachine **Impulse** at micro amplitude for discrete events — never always-on `BasicMultiChannelPerlin` (it's also an always-on CPU cost).
- **No hit-stop > 3 frames.** 4–5 frames reads as violence/trauma. Cap strictly at 2 (mid-chop) / 3 (tree-fell).
- **No squash/stretch on the character body / rig.** The castaway is a rigged Humanoid; non-uniform scale breaks skinning and desyncs the `HeldAxeRig` bone positions (the chain is additive bone-rotation offsets, NOT scale). Squash/stretch on **props and UI only**.
- **No `MaterialPropertyBlock` on juice VFX MeshRenderers.** It disqualifies the renderer from the GPU Resident Drawer instanced path (`unity6-mastery.md` §2). Use particle systems (their own renderer path) or separate material instances. (Particles are exempt — they're not the MPB-disqualified MeshRenderer path.)
- **No real-time shadowed point light for the campfire.** A shadowed point = 6 shadow-map passes/frame (`unity6-mastery.md` §3). Use an UNSHADOWED point light + baked emissive; animate the unshadowed intensity for the flicker.
- **No chromatic-aberration / lens-distortion pulse on hits.** Tonally wrong (reads as damage) AND a URP post-process Volume change needs a separate Render-Graph pass — not free. Defer to a danger/storm event if one ever appears in M-U3+.
- **No audio stinger on every common action.** Fatigue on high-frequency verbs — varied short clips + pitch range beat a single prominent stinger; ambient layering > stingers for chopping.
- **No per-action juice on ambient traversal.** No hit-stop, camera impulse, or particle burst on walking/running — reserve all of them for discrete reward/impact moments.

---

## 3. Perf / shared-palette compliance one-liner

All five must-haves are zero-GC or pool-managed and DO NOT touch the world's shared-palette ~1-draw-call model: particles use a separate `Unlit/Particle` material (not `LowPolyVertexColor`), juice adds no new world shaders, and no juice technique adds an MPB to a world MeshRenderer. DOTween free tier is IL2CPP-safe. Full compliance table in the note.

---

> **A note on the older draft.** `team/erik-consult/game-juice-concepts.md` (2026-06-23) was the first pass: 5 techniques, 8 sources. `game-juice-research.md` (2026-06-30) supersedes it as a strict superset — same 5 + T6–T10, a ranked table, a fuller AVOID table, the stronger proportionality source, and the system-by-system mapping. This checklist distills the **research** note; treat `concepts` as historical.
