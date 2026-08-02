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
5. **Ambient micro-animation for world liveness (T7).** Campfire light-intensity flicker (0.8–1.2× base, ~2Hz), collectible float-bob (±0.05u, 0.8Hz) **— but see §1.5a: only on a LOOSE collectible**, water waves (already shipped — don't regress). **Seed a per-instance phase offset** so they don't pulse in sync (extends the seeded-scatter pattern); gate on activation radius. Respects `quality-bars.md` §Grass — bushes/grass stay still; only the trees-in-air move.

### 1.5a Motion is a property of PLACEMENT — gate the bob before you tune it

**Sponsor's rule, 2026-08-02 (soak of PR #351):** *an item **driven into** or **resting on** something is **STILL**; an item **lying loose** may bob.* Decide this BEFORE reaching for an amplitude — it is a gate, not a dial.

Why it is not a tuning question: a rigid object embedded in (or supported by) a rigid host **cannot** move relative to that host. When it does, the only reading the eye can construct is that the two aren't connected — so the piece reads as **hovering inside** its host rather than driven into it. **No amplitude is small enough to fix that**, because the defect is the *existence* of relative motion, not its size.

The incident, because the near-miss is the instructive part: `86cah7y5b` shipped an iron sword driven point-down into a stump with the standard ±0.05u / 0.8Hz bob + a ±4° sway. Every check was green and every number was in band — the shipped gate measured `bladeTipY=-0.072` vs `stumpTopY=0.475` at `peakBob=+/-0.050`, i.e. the tip stayed **0.497u inside the wood** at the top of every bob, and the capture gate additionally asserted both cue channels were *live*, which was exactly the wrong bar. The Sponsor's verdict was `"the sword is floating, moving in the stump"`. The pre-soak prediction had considered "too quiet" and "beacon-like" and could not see this, because it reasoned about intensity.

**Apply it as:** model placement explicitly (an enum on the component, with the STILL kind as the zero value so a forgotten placement is quiet rather than broken), gate both channels through it, and **keep the authored amplitudes non-zero and inert** rather than zeroing them in the scene — zeroing moves the rule out of the code and into serialized data, where the next author retypes a value and the defect returns silently. Verify it by **sampling the transform in the shipped build**, not by reading the placement field: measuring the frame also catches a bypassed gate or a second component writing the same transform. Live implementation: `WorldWeaponFind.FindPlacement` / `MotionAllowedFor`.

**Corollary for the cue that's left:** a still item has lost a channel, so don't assume its findability survived. `86cah7y5b`'s soak PASSED "reads as special at default framing" — on the *moving* sword. That pass does **not** transfer to the still one; it has to be re-asked.

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

## 2b. Every px figure carries its plane — the hidden `sec(pitch)` that makes a wrong ratio look right

Feel/feedback specs are argued in **screen pixels** ("the flash is 4 px", "the row is 32 px tall"), and this is where they go wrong. The camera sits at a pitch, so **a vertical world extent is foreshortened on screen while a horizontal one is not**. Divide one by the other and the units cancel — the ratio *looks* dimensionally valid — but you have silently dropped a `sec(pitch)` factor.

At the project's default framing (**pitch 55° / distance 14 u / FOV 45°**) that factor is:

```
sec 55° = 1 / cos 55° = 1 / 0.5735764 = 1.7434
frame-plane scale = 720 / (2 × 14 × tan 22.5°) = 62.080 px/m
```

so a `%-of-creature-height` figure computed by mixing planes is inflated **1.7434×**. Measured 2026-08-01 on `86caxjwb3`'s Q9 table, reproduced three independent ways (`sec 55°`; the ratio `62.0798/35.6075 = 1.743453`; and re-deriving the frame-plane scale from the framing rather than lifting it from the bar). Consequence there: *"C is 91 % of the snake"* was really **52 %**, and an option the table had eliminated turned out **not to be eliminated at all** — the corrected reading favoured a *different* option than the one the Sponsor would have been pointed at.

**Rules:**
- **State the plane with every px figure**, alongside pitch / distance / FOV / zoom. A px figure without its framing is undefined, not merely imprecise.
- **Never divide a frame-plane px extent by a foreshortened one.** If both sides are the same world axis the factor cancels honestly; if they are not, it does not.
- **Prefer scale-free ratios where they exist** — `%-of-height` is `(s·k)/(H·k) = s/H`, independent of the px scale entirely, which is why the corrected figures needed no scale at all.
- **Two channels that differ only by a projection factor are the same channel** (bar #10): a constant ratio across instances is an invariant, not a varying cue.

### Diagnosing a figure that "does not reproduce" — the error's *shape* names the cause

Plane-mixing is one cause, not the cause. Two distinct defects produce non-reproducing geometry figures, and **the ratio between the stated and the correct value tells you which**:

| Error ratio across the range | Cause | Fix |
|---|---|---|
| **Constant** (e.g. exactly `1.7434×` everywhere) | a missing frame conversion — **plane mix** | apply the projection factor; prefer a scale-free ratio |
| **Drifts with the input** (e.g. `1.0018×` at 8°, `1.0127×` at 22°) | a **linearisation used outside its domain** — a small-angle or first-order shortcut extrapolated past where it holds | derive the exact closed form; declare the valid band |

Measured 2026-08-01: `86caxjwb3`'s Q9 table was the constant case; `86cav8ybj`'s `θ` was the drifting case — someone had written `θ = √2 × 22° = 31.1°`, but **Unity composes `Euler(x,y,z)` as Z→X→Y**, so the true combined tilt is `θ = acos(cos x · cos z) = 30.72°`. No constant factor exists there, so hunting for one wastes the pass.

**So: compute the error at two or three points across the range before theorising.** Constant ⇒ look for a projection factor. Drifting ⇒ look for an approximation that has left its domain. And when a figure derives from stacked Euler rotations, **check the composition order** rather than assuming components add in quadrature.

## 3. Perf / shared-palette compliance one-liner

All five must-haves are zero-GC or pool-managed and DO NOT touch the world's shared-palette ~1-draw-call model: particles use a separate `Unlit/Particle` material (not `LowPolyVertexColor`), juice adds no new world shaders, and no juice technique adds an MPB to a world MeshRenderer. DOTween free tier is IL2CPP-safe. Full compliance table in the note.

---

> **A note on the older draft.** An earlier "game-juice concepts" note (2026-06-23) was the first pass: 5 techniques, 8 sources. **It is NOT in the repo — it was deleted in PR #201 (`187e486`) and is not restorable by path**; it is named here only so a reader who meets the name in an old ticket or PR knows what it was. `team/erik-consult/game-juice-research.md` (2026-06-30) supersedes it as a strict superset — same 5 + T6–T10, a ranked table, a fuller AVOID table, the stronger proportionality source, and the system-by-system mapping. This checklist distills the **research** note, which is the only one you can open.
