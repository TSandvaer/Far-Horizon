# Procedural Action-Verb Animation Playbook — Castaway Generic Rig

## Question

How do Devon/Drew add a windup→strike→return player SWING animation for the chop action (ticket
`86caa4c5c`) — and the coming pick-up / drink / throw verbs — on the Mixamo Generic rig without
breaking the existing `CastawayArmPose` → `HeldAxeRig` execution chain or the Sponsor's locked
idle/walk/run arm-pose?

> **Correction on record:** chop ticket `86caa4c5c` AC1 states "reuse/**extend** the existing
> chop animation." That is **FALSE** in the "reuse" sense. No chop animation clip exists in the project.
> `Assets/Art/Character/Castaway/` contains: `Idle.fbx`, `Walking.fbx`, `Running.fbx`,
> `Jump.fbx`, `Jump_idle.fbx`, `Jump_running.fbx`, `Picking Up.fbx`, and several hit-react
> clips — no chop, no strike, no swing. The **missing piece is the player swing animation**;
> AC1 must be rewritten to specify authoring a new additive bone-offset curve (this playbook)
> rather than reusing a nonexistent clip.

---

## Bottom line

Author chop (and every other action verb) as a **LateUpdate additive bone-rotation offset curve
on `CastawayArmPose`** — not as a new Animator clip or layer. The codebase already has exactly
one arm-modification idiom and it is that pattern (`CastawayArmPose.LateUpdate` order 50, right-
multiplied onto the Animator's clip pose); `HeldAxeRig` (order 100) then reads the final hand
transform so the axe follows automatically. A new Animator layer with AvatarMask is technically
possible but contradicts the deliberate design decision against it and adds risk without benefit
on this Generic rig. The bone-axis-measurement ritual (`-armTrace`) and the execution-order
contract (Animator → ArmPose 50 → HeldAxeRig 100) are the two non-negotiable pre-conditions; get
both wrong and the swing either applies against stale bone data or double-applies the offset.

---

## Evidence

### E1 — Unity Generic rig: no muscle retarget, transform-path binding
**Source:** Unity Manual — "FBX Importer — Rig tab" (Unity 6 docs,
docs.unity3d.com/6000.0/Documentation/Manual/FBXImporter-Rig.html) — the transform-path
binding behaviour for Generic rigs is documented here, not in the generic AnimationsImport
overview page. **Strength: source-verified (official docs; citation corrected from
AnimationsImport.html which does not cover this detail).**

A Generic rig binds animation clips by transform path, not via Avatar muscle retarget. This
means:
- Bone local rotations written by the Animator are the raw Mixamo joint angles.
- There is no muscle-space normalisation — the `localRotation` you read in `LateUpdate` is
  exactly what the clip authored.
- Additive offsets via `bone.localRotation = clipPose * Quaternion.Euler(offset)` compose
  predictably in the bone's local frame, which is what `CastawayArmPose` exploits.
- AvatarMask works on a Generic rig via per-bone transform-path selection (not muscle groups) —
  it does NOT require a Humanoid rig. However, the mask asset must enumerate exact Mixamo bone
  names, so it breaks **silently** on any bone rename or character swap. It also carries higher
  authoring overhead than the additive-offset idiom. Both reasons are why the project avoids it.
  **Source (C6 re-verify):** Unity Manual — "Avatar Mask window"
  (docs.unity3d.com/6000.0/Documentation/Manual/class-AvatarMask.html); Unity Manual —
  "Animation Layers" (docs.unity3d.com/6000.0/Documentation/Manual/AnimationLayers.html).
  **Strength: Strong (official docs).**

### E2 — LateUpdate execution order: the only safe window for post-Animator bone modification
**Source:** Unity Manual — "Order of execution for event functions"
(docs.unity3d.com/6000.0/Documentation/Manual/ExecutionOrder.html). **Strength: Strong
(official docs).**

The Animator writes bone transforms in `Animator.Update()`, which runs as part of the `Update`
phase (or during `LateUpdate` if `updateMode = AnimatePhysics` — not the case here). Any
MonoBehaviour `LateUpdate` that runs AFTER the Animator has already written this frame's pose can
safely read `bone.localRotation` as the clip value and right-multiply an offset onto it. This is
exactly the pattern in `CastawayArmPose.LateUpdate`:

```csharp
// From CastawayArmPose.cs — the pattern, verbatim:
rightUpperArm.localRotation = rightUpperArm.localRotation * _rightOffsetQ * runLowerQ;
```

Reading and immediately modifying `bone.localRotation` in `LateUpdate` is the standard Unity
idiom for IK overlays, procedural aim, and post-animation corrections. It is NOT safe in
`Update` (Animator may not have run yet for this frame on some execution orderings).

### E3 — DefaultExecutionOrder controls the within-LateUpdate ordering
**Source:** Unity Scripting API — `DefaultExecutionOrder` attribute
(docs.unity3d.com/6000.0/Documentation/ScriptReference/DefaultExecutionOrder.html) for the
attribute semantics. The specific order-50 / order-100 values are from the project codebase
(`CastawayArmPose.cs` / `HeldAxeRig.cs` headers, verified 2026-06-25). **Strength:
source-verified (attribute docs Strong; specific order values are a repo code quote, not
official doc constants).**

The project already uses this correctly:
- `CastawayArmPose` → `[DefaultExecutionOrder(50)]` — runs after default-order scripts.
- `HeldAxeRig` → `[DefaultExecutionOrder(100)]` — runs after `CastawayArmPose`.

This guarantees: Animator writes pose → ArmPose applies offset → HeldAxeRig reads the FINAL
hand transform → axe seats correctly. Any new action-verb script that drives `CastawayArmPose` from **`LateUpdate`**
(a `ChopPoseDriver` or equivalent) MUST use an order less than 50 (e.g. order 0 or 10) so it
writes its curve value INTO `CastawayArmPose` BEFORE ArmPose's `LateUpdate` applies it. (A driver
that writes only in `Update` would be consumed the same frame regardless of order, but
`LateUpdate`-writing drivers carry a hard order-less-than-50 requirement.)

### E4 — AnimationCurve for timed one-shot offset curves
**Source:** Unity Scripting API — `AnimationCurve`
(docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimationCurve.html). **Strength:
Weak-Moderate (official API ref; GC claim is empirical convention, not doc-guaranteed).**

`AnimationCurve.Evaluate(t)` is a C# runtime evaluation of a Bezier curve sampled at normalised
time `t ∈ [0,1]`. `AnimationCurve` is a **class** (reference type), not a struct — the curve
object itself allocates on the heap when constructed. However, `Evaluate()` on a pre-existing
serialised `[SerializeField]` curve does not allocate per steady-state call (no boxing, no new
object per frame). The safe pattern is: declare as a `[SerializeField]` field (allocated once at
load), call `Evaluate(t)` each frame — zero per-frame GC. Do not construct `new AnimationCurve()`
at runtime in a hot path. Used here to author the windup→peak→return shape for each axis of bone
rotation. Each keyframe's in/out tangent controls feel (hard strike vs soft pick-up).
Inspector-editable so Devon/Drew can tune without recompile.

### E5 — Mixamo "Picking Up.fbx" exists but is unwired
**Source:** Direct file enumeration of
`Assets/Art/Character/Castaway/` in this repo (verified 2026-06-25). **Strength: Strong
(ground truth — file present on disk).**

`Picking Up.fbx` is on disk. It is NOT imported into the Animator Controller and NOT referenced
by any runtime script. For pick-up this creates a fork: (a) use the clip as a full-body override
(requires Animator state + AvatarMask or a separate Animator.Play), or (b) treat the clip as a
reference/inspiration and drive pick-up via the additive offset pattern. Verdict: option (b) is
the safer choice for **the arm-only phase of pick-up** (the additive offset bends the arms toward
the ground without clobbering the locomotion clip); option (a) is needed ONLY if full-body root
motion or a complete body freeze is required during pick-up. See per-verb mapping below.

### E6 — "Headless deltaTime ≈ 0" trap for one-shot pose assertion
**Source:** Far Horizon unity-conventions.md §"Headless / CLI rituals" — "Headless PlayMode time
trap: `Time.deltaTime ≈ 0` per frame in headless runs — never assert on per-frame deltas; sample
over a real `Time.time` window instead." **Strength: Strong (empirically observed, in-project
doc).**

An action-verb swing is timed by accumulated `Time.time` over a duration (e.g. 0.4s). In a
headless PlayMode test each frame advances ~0 seconds of game time, so the swing does not
progress from the test's perspective if the driver uses `deltaTime` accumulation. **Pattern used
in this project:** drive the swing by `Time.time` directly (not `Time.deltaTime` accumulation),
and expose `SwingNormT` (current 0→1→0 position in the swing) as a readable property so tests
can assert `SwingNormT == 0` at rest and `SwingNormT > 0` mid-swing without caring about exact
deltaTime values. The project's headless capture and locomotion test coroutines use plain
`WaitForSeconds`/`Time.time` windows and exit green — both `WaitForSeconds` and
`WaitForSecondsRealtime` are valid in PlayMode tests (see unity-conventions.md); the real
headless trap is `WaitForEndOfFrame` (E7 below), not `WaitForSeconds`.

### E7 — WaitForEndOfFrame unreliable in headless PlayMode
**Source:** Far Horizon unity-conventions.md §"Headless / CLI rituals" — "`WaitForEndOfFrame` is
NOT evoked in `-batchmode` (Devon, PR #60 locomotion-harness work)." **Strength: Strong
(empirically observed, in-project doc).**

Do NOT gate any post-`LateUpdate` pose assertion on `WaitForEndOfFrame` in a PlayMode test — it
never fires in headless. The project workaround is to record the post-LateUpdate value into a
cached field in a high-`DefaultExecutionOrder` LateUpdate component and read the cached value
on the next `yield return null`.

### E8 — Animator Layer + AvatarMask on Generic rig: the one case it WOULD be appropriate
**Source:** Unity Manual — "Avatar Mask window"
(docs.unity3d.com/6000.0/Documentation/Manual/class-AvatarMask.html); Unity Manual — "Animation
Layers" (docs.unity3d.com/6000.0/Documentation/Manual/AnimationLayers.html). **Strength: Strong
(official docs). Application verdict: NOT recommended for Far Horizon's current rig.**

AvatarMask + Animator Layer is the standard Unity facility for blending a per-body-region
animation on top of a locomotion layer. On a Generic rig it isolates specific transform paths
rather than muscle groups. It IS an appropriate choice when:

1. A full-body authored clip (with root motion) needs to override only the upper body, AND
2. The team has tooling to author and maintain the mask asset as the skeleton evolves, AND
3. The clip controls IK targets or multiple bones simultaneously (spine curve, both arms,
   shoulder rotation) in ways a simple euler offset can't express.

For Far Horizon TODAY it is NOT recommended because:
- The existing `CastawayArmPose` additive-offset idiom already handles all approved arm poses
  (idle relax, carry, run-lower) without any Animator layer or mask asset.
- Adding a layer + mask is a schema change to the Animator Controller, which affects ALL clips
  and requires re-testing every motion state.
- The Generic rig's mask granularity is by transform path — a mask .asset must enumerate the
  exact bone names, which are Mixamo-specific and will break silently if a future character swap
  renames bones.
- For the action-verb scope (chop, drink, pick-up, throw) the motion is ARM-dominated and
  duration-bounded — a LateUpdate curve is a simpler, more maintainable solution.

**The ONE case where a layer would be worth it:** a full-body attack animation (e.g. a two-handed
overhead smash requiring spine extension and foot-planting root motion) that cannot be expressed
as an additive arm-offset. That is out of scope for M-U2 and the current thin chop.

---

## Application to Far Horizon

### The authoritative execution order chain (verified from source)

```
Frame N:
  Animator.Update()          [built-in, after Update]
      → writes bone.localRotation for this frame's clip pose
  CastawayArmPose.LateUpdate [DefaultExecutionOrder 50]
      → right-multiplies _rightOffsetQ + _runWeight*runLowerQ onto rightUpperArm
      → right-multiplies _leftOffsetQ onto leftUpperArm
  HeldAxeRig.LateUpdate      [DefaultExecutionOrder 100]
      → reads hand.position + hand.rotation → seats axe in world space
```

A `ChopPoseDriver` (or `DrinkPoseDriver`, etc.) runs at order 0–10, updates `CastawayArmPose`'s
override euler each frame, and `CastawayArmPose.LateUpdate` picks it up.

### Pattern: how to add a new action verb

**Step 1 — Bone-axis measurement ritual.**
Before coding, measure which LOCAL axis on `mixamorig:RightArm` (the upper-arm bone) produces
the swing direction you want. The project already has empirical measurements:
- `+local-X` → spreads the hand AWAY from the torso (outward for both arms, same sign — verified
  by `RuntimeRigTrace -armTrace`, 2026-06-15, cited in `CastawayArmPose.cs` header).
- `+local-Z` → lifts/reaches the hand (+Y world direction) — the "carry raise" axis.
- `+local-Y` → near-useless twist (same source).

**For a chop swing:** the swing is a downward arc — primarily `−local-Z` (lower the arm) with
a possible `+local-X` windup spread. Verify against the live rig by temporarily cranking the Z
and X euler fields on `CastawayArmPose` via the F9 AxeNudgeTool and judging the arc visually
before coding a driver.

Do NOT assume Y-axis is the raise axis. The Mixamo Generic rig's LOCAL bone frames are
arbitrary — the trace beat the first guess (`−Z lowers`, not `−Y`). Always measure.

**Step 2 — Author the `AnimationCurve` shape.**
Three serialised curves on the driver (one per euler axis): `swingX`, `swingY`, `swingZ`.
Time normalised to `[0, 1]` over the swing duration (e.g. 0.4s for a chop).

Typical windup→strike→return shape (X or Z axis, degrees):
- `t=0.0`: 0° (rest)
- `t=0.25`: +15° (windup back — slower tangent)
- `t=0.60`: −40° (strike peak — fast/steep tangent before and after)
- `t=1.0`: 0° (return — ease-out tangent)

Inspector-tweakable. Expose `SwingNormT` as a readable public property (see E6 above for test
access).

**Step 3 — Driver script pattern.**
```csharp
// (Order 0 or 10 — runs before CastawayArmPose order 50)
[DefaultExecutionOrder(10)]
public class ChopPoseDriver : MonoBehaviour
{
    public CastawayArmPose armPose;
    public AnimationCurve swingX, swingY, swingZ;
    public float swingDuration = 0.4f;

    private float _swingStart = float.NegativeInfinity;
    public float SwingNormT => Mathf.Clamp01(
        (Time.time - _swingStart) / Mathf.Max(0.001f, swingDuration));

    public void TriggerSwing() => _swingStart = Time.time;

    void LateUpdate()
    {
        if (armPose == null) return;
        float t = SwingNormT;
        if (t >= 1f) return; // at rest — don't override

        // Compose onto the existing rightArmEuler via a SEPARATE override field.
        // CastawayArmPose.LateUpdate will add swingOverrideEuler on top of its carry offset.
        armPose.swingOverrideEuler = new Vector3(
            swingX.Evaluate(t),
            swingY.Evaluate(t),
            swingZ.Evaluate(t));
    }
}
```

`CastawayArmPose.LateUpdate` gains one additional right-multiply for `Quaternion.Euler(swingOverrideEuler)`
in the right-arm composition line. When no swing is active `swingOverrideEuler` is `Vector3.zero`
→ identity quaternion → zero cost, the locked carry pose is byte-unchanged.

**Step 4 — HeldAxeRig seating.**
No change needed. Because `ChopPoseDriver` and `CastawayArmPose` both run BEFORE `HeldAxeRig`
(order 100), the axe automatically follows the swung hand to the target and returns on the
return arc. The fast-swing seating gotcha: if `swingDuration` is too short (< ~0.15s) the axe
can appear to lead the hand by one frame because `HeldAxeRig` runs at the end of the same
`LateUpdate` batch but reads the PREVIOUS-frame `followDamp`-eased position when `followDamp > 0`.
With the default `followDamp = 0` (raw follow) this lag is zero; with `followDamp > 0` and a
very fast swing, the axe lags the hand's peak by ≤ 1 frame. The fix for a fast-feeling chop
is to keep `followDamp = 0` during the swing (or set it from the driver) and allow the raw
follow to track the full arc.

**Step 5 — PlayMode test pattern (avoid headless traps E6 + E7).**

```csharp
[UnityTest]
public IEnumerator ChopSwing_PeakDisplacesRightArm_ReturnRestoresCarryPose()
{
    // 1. Load the scene (Boot.unity must be bootstrapped — see unity-conventions.md).
    // 2. Get ChopPoseDriver and CastawayArmPose.
    // 3. Record the rest armPose.rightArmEuler.
    float swingDuration = driver.swingDuration;
    driver.TriggerSwing();

    // 4. Yield frames until past the peak (NOT WaitForEndOfFrame — E7; WaitForSeconds also fine).
    yield return new WaitForSeconds(swingDuration * 0.6f);
    // 5. Assert SwingNormT is between 0 and 1 (active mid-swing).
    Assert.Greater(driver.SwingNormT, 0f);
    Assert.Less(driver.SwingNormT, 1f);

    // 6. Wait for the full return.
    yield return new WaitForSeconds(swingDuration * 0.5f + 0.05f);
    // 7. Assert swingOverrideEuler is approximately zero (carry pose restored).
    Assert.AreApproximatelyEqual(0f, armPose.swingOverrideEuler.magnitude, 0.1f);
}
```

Do NOT assert on `Time.deltaTime` values. Do NOT gate on `WaitForEndOfFrame` (E7 — never fires
headless). Use `WaitForSeconds` or `WaitForSecondsRealtime` — both are headless-safe. Expose
`SwingNormT` as a public property on the driver for test access.

---

### Per-verb mapping

#### CHOP (`86caa4c5c`) — implement NOW

- **Approach:** `ChopPoseDriver` as described above, triggered by `ChopTree` when a chop lands.
- **Curve shape:** downward arc (−local-Z windup back, then through to the strike, return). Windup
  should read 0.1–0.15s (fast, purposeful), strike at ~0.35s, full return at 0.5–0.6s.
- **Axe follows automatically** via `HeldAxeRig` order 100 with `followDamp = 0`.
- **`ChopTree.Chop()`** calls `chopPoseDriver.TriggerSwing()` — a clean seam; `ChopTree` already
  owns the "a chop just landed" event.
- **AC1 correction:** no existing chop clip exists; the "reuse/extend" wording in AC1 is
  misleading — this driver IS the implementation; the swing is authored from scratch.
- **Test:** `ChopSwing_PeakDisplacesRightArm_ReturnRestoresCarryPose` (pattern above).

#### PICK-UP (`Picking Up.fbx` exists, unwired)

- **Approach:** additive arm-offset driver (same pattern). `Picking Up.fbx` can be used as a
  visual reference for the curve shape — watch the clip in the Unity Inspector to observe which
  bone arcs and how far — but do NOT wire it into the Animator. The Animator state machine change
  (add a "PickingUp" state, set trigger, wait for exit) is a larger scope than the additive-offset
  approach and risks regression across all locomotion states.
- **Curve shape:** both arms bend toward the ground / forward (positive local-X forward-reach for
  the right arm, curve dips then returns). Duration ~0.6s (slower, deliberate).
- **Axe follows.** If the pick-up is the AXE pick-up (`AxePickup`), the axe visibility gate
  (`HeldAxe.SetActive(true)`) fires after the pick-up animation returns — the swing can drive the
  arm down and the visibility gate syncs the axe appearance to the return.
- **Extends cleanly** — no new Animator states, no AvatarMask.

#### DRINK-FROM-HAND (thirst shipped, no arm anim — `86caamkv7`)

- **Approach:** additive arm-offset driver, BOTH arms (cupped-hands gesture: raise both arms
  forward toward the mouth). The raise axis is positive local-Z on both upper arms.
- **Curve shape:** slow, sustained hold (~1.0–1.5s at the peak), then lower. No fast strike.
- **Extends cleanly** — same driver pattern, different curves, left arm also driven.
- **Note:** drink is currently triggered at the pond proximity. The driver receives a `TriggerDrink()`
  call from the drink interaction script, same seam as chop.

#### THROW (future)

- **Approach:** additive arm-offset driver (wind-back then forward arc), same pattern.
- **Release point:** the throw mechanic needs the axe to DETACH from the hand at the peak. This
  is NOT a bone-rotation concern — it is a `HeldAxeRig.enabled = false` + projectile-spawn event
  at the correct point in the swing timeline (when `SwingNormT` crosses the release threshold).
- **Extends cleanly** — additive offset drives the arm, the release event handles the projectile.

---

### Known gotchas summary

| Gotcha | Details | Source |
|---|---|---|
| Bone-axis measurement is mandatory | LOCAL-X and LOCAL-Z on `mixamorig:RightArm` are NOT the obvious world axes. Guess wrong → swing goes the wrong direction. Measure via F9 nudge or `-armTrace` BEFORE coding. | E1 + CastawayArmPose.cs header |
| Axe one-frame lag with followDamp > 0 and a fast swing | If followDamp > 0 the axe lags the hand's peak by ≤1 frame. Keep followDamp = 0 for the chop arc. | HeldAxeRig.cs; E3 |
| WaitForEndOfFrame unreliable headless | Never use `WaitForEndOfFrame` in PlayMode tests — it does not fire in `-batchmode`. Use `WaitForSeconds` or `WaitForSecondsRealtime`; both work. | E7; unity-conventions.md |
| Time.deltaTime ≈ 0 headless | Never accumulate deltaTime across frames in a test; use Time.time anchoring and expose SwingNormT. | E6; unity-conventions.md |
| swingOverrideEuler must be zero at rest | If the driver does not reset swingOverrideEuler to Vector3.zero after the swing completes, the carry pose drifts. | Pattern Step 3 |
| DefaultExecutionOrder < 50 is mandatory for a LateUpdate-writing driver | A `LateUpdate`-writing driver at order 0 runs BEFORE ArmPose (50) — correct. A driver writing in `LateUpdate` at order 100+ runs AFTER and its override is never read this frame. An `Update`-writing driver can be consumed at any order, but all current verb drivers use `LateUpdate` so the < 50 rule applies. | E3 |
| AvatarMask breaks silently on Generic rig bone rename | Do not introduce AvatarMask assets that enumerate Mixamo bone paths; they are fragile to character swaps. | E8 |

---

## Proposed `.claude/docs/` distilled checklist content

The following is the mandatory-checklist content for a new `.claude/docs` entry (suggested
filename: `procedural-animation-verbs.md`). The orchestrator/Priya wires the index line + MANDATORY
marker.

---

**MANDATORY pre-work read for ALL action-verb animation work (chop, pick-up, drink, throw).**
Full evidence at `team/erik-consult/procedural-action-verb-animation.md`.

### The non-negotiable chain

```
Animator (writes clip pose)
  → CastawayArmPose.LateUpdate [order 50]  (additive offset + run-lower)
      → HeldAxeRig.LateUpdate  [order 100] (seats axe on hand)
```

Any action-verb driver MUST run at `DefaultExecutionOrder` < 50. `HeldAxeRig` automatically
follows — never move the axe directly from the verb driver.

### Authoring checklist (one box per verb)

- [ ] **Measure bone axes first.** Cheat sheet (verified `−armTrace`): `+local-X` = outward
  spread; `+local-Z` = raise/forward-reach; `+local-Y` = twist (useless). Do NOT assume Y = up.
- [ ] **Expose `SwingNormT` (0→1→0) as a public property** for test access.
- [ ] **Add `swingOverrideEuler` field to `CastawayArmPose`** (reset to `Vector3.zero` when
  driver `SwingNormT >= 1`; compose as a right-multiply AFTER `_rightOffsetQ`).
- [ ] **`followDamp = 0` during a fast chop-class swing** (otherwise axe lags the strike peak
  by one frame).
- [ ] **PlayMode test: NEVER use `WaitForEndOfFrame` (does not fire in `-batchmode`).** Use
  `WaitForSeconds` or `WaitForSecondsRealtime` — both are headless-safe. Assert `SwingNormT > 0`
  at mid-swing; assert `swingOverrideEuler ≈ 0` at rest.
- [ ] **Driver runs at `DefaultExecutionOrder` < 50 when writing from `LateUpdate`.** An
  `Update`-writing driver can be consumed at any order, but all current verb drivers use
  `LateUpdate` — keep order < 50.
- [ ] **No Animator state changes, no AvatarMask.** The additive-offset pattern is the
  project idiom. Propose a layer only if full-body root motion is required AND scope it as its
  own migration ticket.

### Per-verb status (2026-06-25)

| Verb | Clip exists? | Arm-anim shipped? | Recommended approach |
|---|---|---|---|
| Chop | NO (AC1 of `86caa4c5c` "reuse/extend" is misleading — no clip exists) | No | Additive driver, TriggerSwing() from ChopTree.Chop() |
| Pick-up | Picking Up.fbx (unwired) | No | Additive driver (use clip as curve reference only) |
| Drink | No clip | No | Additive driver, both arms raised toward face |
| Throw | No clip | N/A (future) | Additive driver + detach event at SwingNormT threshold |
