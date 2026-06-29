# Per-Weapon Attack Animation Pipeline — Unity 6 Generic Rig (Mixamo)

## Question

What is the cleanest way to wire per-weapon Mixamo attack clips onto the castaway's Animator
so ticket `86caffwv5` can land correctly — covering clip approach, left-click trigger + hit
sync, Mixamo import gotchas, and composition with in-hand weapon placement?

---

## Bottom line

**Use an Animator state per weapon family (not `AnimatorOverrideController`) driven by a
`SetTrigger`.** The castaway uses a **Generic rig** (confirmed in `character-pipeline.md` — the
Humanoid-explode incident forced a Generic pivot), so `AnimatorOverrideController`'s main
advantage (Avatar muscle retarget stays live across swaps) does not apply; the simpler Animator
state approach carries less hidden state and is easier to test. Import the Mixamo axe-attack clip
**Without Skin** (Generic type, same root node as the base character FBX), **Bake Into Pose** all
root motion axes so the clip is in-place, **Loop = OFF**. Wire the "hit" moment via an
**Animation Event** on the clip (not a timed coroutine) — it fires precisely on the frame the
strike peaks regardless of playback speed. The `CastawayArmPose` additive-offset chain must
be **disabled** (or zeroed) while the full-body clip is active, otherwise the arm-pose layer
double-applies rotation to the right arm.

---

## Evidence

### E1 — Generic rig: clips bind by transform path, not muscle retarget
**Source:** Unity Manual — "Importing a model with non-humanoid (generic) animations"
(docs.unity3d.com/Manual/GenericAnimations.html). **Strength: Strong (official docs).**

A Generic Avatar auto-binds animation clips by matching bone names/paths in the hierarchy.
"Without Skin" FBX clips from Mixamo bind cleanly to the character FBX as long as both
share the same Generic Avatar (same root node, same bone hierarchy). No avatar-copy step
is required as it is for Humanoid retarget. The project's existing `Idle.fbx`, `Walking.fbx`
etc. are all Generic and already prove this path works.

### E2 — AnimatorOverrideController state-machine behaviour
**Source:** Unity Scripting API — `AnimatorOverrideController`
(docs.unity3d.com/ScriptReference/AnimatorOverrideController.html). **Strength: Strong
(official docs).**

The docs state: "Swapping `Animator.runtimeAnimatorController` with an
`AnimatorOverrideController` based on the same `AnimatorController` at runtime doesn't reset
state machine's current state." The `AnimatorOverrideController` indexer "triggers a
reallocation of the animator's clip bindings" per call; `ApplyOverrides` reduces that to one
reallocation for a batch of changes. Both approaches are GC-safe IF the override list is
pre-allocated (`GetOverrides` is alloc-free with a pre-sized list). However for the small
weapon set here (axe→knife→sword→spear = 4 attack variants), separate Animator states per
weapon family are simpler: no pre-allocation dance, state transitions are self-documenting,
and the AOC main benefit (reusing one state for N clips) is marginal at 4 clips.

**Verdict for Far Horizon:** `AnimatorOverrideController` is appropriate when the weapon set
grows large enough (≥8–10 variants) that maintaining N duplicate Animator states is
burdensome. At 4 it is over-engineering. Use separate states, switch via `SetTrigger`.

### E3 — Mixamo "Without Skin" + Generic import settings for in-place clip
**Source:**
- Unity Manual — "GenericAnimations" (docs.unity3d.com/Manual/GenericAnimations.html).
  **Strength: Strong (official docs).**
- Unity Discussions — "Troubles with Root motion (Mixamo)"
  (discussions.unity.com/t/troubles-with-root-motion-mixamo/789798). **Strength: Moderate
  (community, corroborates docs).**
- Mixamo to Unity guide — Advanced Game Design Docs
  (advanced-game-design.readthedocs.io/en/latest/class_resources/mixamo/index.html).
  **Strength: Moderate (well-sourced tutorial, widely cited).**

The Mixamo axe-attack clip the Sponsor has is a full-body authored clip. Correct import:

| Setting | Value | Why |
|---|---|---|
| **Rig > Animation Type** | Generic | Matches the base character FBX |
| **Rig > Avatar Definition** | Copy From Other Avatar → `Castaway` avatar | Binds to the same bone hierarchy |
| **Animation > Bake Into Pose: Root Transform Rotation** | ON | Prevents in-clip body yaw drift |
| **Animation > Bake Into Pose: Root Transform Position (Y)** | ON | Prevents vertical hop |
| **Animation > Bake Into Pose: Root Transform Position (XZ)** | ON | Keeps clip in-place (no translation) |
| **Animation > Loop Time** | OFF | One-shot attack, not a cycle |
| **Animation > Loop Pose** | OFF | Same |
| **Apply Root Motion** (Animator component) | OFF (already the case for WASD) | Root motion is driven by the locomotion code, not clips |

**Critical gotcha:** if "Bake Into Pose" is left OFF for any axis, the attack clip will slide
the character in world space during the swing (common Mixamo attack import failure). Every
Mixamo attack clip has authored hip translation; baking it into pose collapses that
translation onto the root bone's T-pose offset and the character stays put.

**Scale gotcha:** Mixamo exports at 1 cm = 1 unit by default (scale factor 100). The
castaway base FBX was already imported with the project's scale convention — verify the
attack clip's FBX import scale factor matches the base character. If the base character was
imported at scale 1 (Unity auto-scale), set the attack clip to the same value. A mismatch
causes the skeleton to visibly snap when the attack state is entered.

### E4 — Animation Events vs timed coroutine for hit sync
**Source:**
- Unity Manual — "Animation Events" (docs.unity3d.com/Manual/AnimationEventsOnImportedClips.html).
  **Strength: Strong (official docs).**
- Unity Discussions — "Synchronize attack animation and hit detection in melee combat"
  (forum.unity.com/threads/need-feedback-on-how-to-synchronize-attack-animation-and-hit-detection-in-melee-combat.1007909/).
  **Strength: Moderate (well-reasoned community thread).**

Two options:

**Option A — Animation Event (recommended):** Place an Animation Event keyframe at the strike
frame in the clip import settings (`Animation` tab → `Events` list). At runtime the Animator
calls a named method on any `MonoBehaviour` on the same GameObject. Zero drift — fires on
the exact clip frame where the axe is at full speed regardless of playback speed, blend
weights, or frame rate. The existing `ChopTree.Chop()` (or a new `OnAttackHit()` method) is
the target. Name the event `OnWeaponHit` and implement it on the attack controller script.

**Option B — Timed coroutine (`WaitForSeconds(clipDuration * hitFraction)`):** Fires after
a fixed real-time delay. Drifts from visual if the Animator playback speed is ever scaled
(speed multiplier, slow-motion, ability modifiers). Harder to tune. NOT recommended.

**Verdict:** Animation Events win on precision and maintainability for melee. The one
caveat: Animation Events on **imported** clips (the `Without Skin` FBX) must be added in
Unity's FBX import settings (`Animation` tab → `Events`), not in an Animation Controller
transition, because the project doesn't own the clip source. These import-side events
survive FBX re-import cleanly.

### E5 — CastawayArmPose + HeldAxeRig composition during a full-body clip
**Source:** Far Horizon — `team/erik-consult/procedural-action-verb-animation.md` (this
project, 2026-06-25). **Strength: Strong (in-project verified ground truth).**

The existing execution order chain is:

```
Animator.Update()            [writes bone.localRotation from the CLIP]
CastawayArmPose.LateUpdate   [DefaultExecutionOrder 50 — right-multiplies offset Q onto rightUpperArm]
HeldAxeRig.LateUpdate        [DefaultExecutionOrder 100 — seats axe on hand]
```

When a full-body Mixamo attack clip is playing, the Animator ALREADY drives `rightUpperArm`
to the attack arc. `CastawayArmPose` will then right-multiply its carry-offset on top —
double-applying rotation. This produces a wild arm swing that bears no relation to either
pose.

**Fix:** the attack state's entry must **zero out** `CastawayArmPose`'s override eulers AND
set its `_rightOffsetQ` and `_leftOffsetQ` to identity for the duration of the attack clip.
Two implementation options:

- **Option A (recommended):** `CastawayArmPose` exposes a `bool suppressForClip` field;
  the attack controller sets it `true` on state entry and `false` on `OnWeaponHit` return
  or state exit. When `suppressForClip == true`, `LateUpdate` skips its entire right-arm
  compose block. `HeldAxeRig` still runs at order 100 and reads the hand transform the
  CLIP authored → axe tracks the attack arc correctly with no code change to `HeldAxeRig`.

- **Option B:** A full Animator Layer with an AvatarMask isolating the upper body. Ruled
  out — `procedural-action-verb-animation.md` E8 documents why AvatarMask on this Generic
  rig is fragile (mask enumerates transform paths; any future character swap silently
  breaks it).

`HeldAxeRig` requires NO changes: because it runs AFTER both systems (order 100), it
already reads the final composite hand transform whether `CastawayArmPose` is active or
suppressed.

### E6 — Animator state vs AnimatorOverrideController for a growing weapon set
**Source:** Unity Manual — "Animator Override Controller"
(docs.unity3d.com/6000.2/Documentation/Manual/AnimatorOverrideController.html). **Strength:
Strong (official docs).** Application verdict from this project's weapon set size.

The AOC pattern is best suited when: one state machine, many clip variants (enemies with
different idles but identical state logic). For Far Horizon's per-weapon case, each weapon
type (axe vs sword vs spear) plausibly wants different transition exit times, different
attack durations, and different combo counts in the future — meaning the state machines will
diverge anyway. Separate states or a sub-state-machine per weapon family is the
right foundation:

**Recommended structure (grows to 4 weapons cleanly):**

```
[Animator Controller]
  Base Layer:
    Idle
    Walk / Run (blend tree)
    Jump
    Attack_Axe       ← new state, axe attack clip
    Attack_Knife     ← future
    Attack_Sword     ← future
    Attack_Spear     ← future

  Transitions from any locomotion state:
    → Attack_Axe     on Trigger "AttackAxe"
    → (future Attack_Knife on Trigger "AttackKnife", etc.)
  Transition from Attack_* back to locomotion:
    → on clip exit (Has Exit Time = true, at normalized 1.0)
```

This avoids AOC GC/reallocation concerns entirely (no runtime swap), is fully serialized
in the `.controller` asset (version-control friendly), and is inspectable in the Animator
window without runtime tooling.

**If** the set grows to ≥8 variants, migrate to AOC: pre-allocate the override list with
`GetOverrides(preAllocList)` at `Start`, swap via `ApplyOverrides(list)` on weapon equip.
That migration is a clean refactor — the trigger + state structure stays; only the clip
provider changes.

---

## Application to Embergrave → Far Horizon

### The Sponsor's immediate need

The Sponsor has **one clip**: a Mixamo axe-attack FBX (not yet imported). He rejected the
procedural `ChopPoseDriver` swing and wants a proper Mixamo attack clip instead. Ticket
`86caffwv5` covers wiring it.

**What Devon/Drew need from the Sponsor before implementation can start:**

> **Provide the axe-attack FBX file.** The clip must be Mixamo "Without Skin" format
> (skeleton + animation only, no mesh), FBX for Unity, 30 fps, Keyframe Reduction: None.
> Drop it into `Assets/Art/Character/Castaway/` alongside the existing
> `Idle.fbx` / `Walking.fbx` etc. If the file already exists locally, confirm its filename
> so Devon can reference it in the import settings.

If the Sponsor downloaded it as "With Skin" (mesh + rig + animation), the mesh can be
discarded during import — but "Without Skin" is cleaner.

### Implementation checklist for `86caffwv5`

1. **Import the clip:**
   - Rig tab: Generic, `Copy From Other Avatar` → the existing castaway avatar asset.
   - Animation tab: `Bake Into Pose` all three root motion axes, `Loop Time = OFF`.
   - Animation tab → Events: add event at the strike frame, method name `OnWeaponHit`.
   - Verify scale factor matches the base character FBX.

2. **Add `Attack_Axe` state to the Animator Controller:**
   - Assign the imported clip.
   - Transition FROM any locomotion state on Trigger `"AttackAxe"`, `Has Exit Time = false`
     (immediate on left-click).
   - Transition BACK via `Has Exit Time = true` at normalized time 1.0 (clip finishes).

3. **Wire left-click input:**
   - Add `Attack` action (Button) to `FarHorizonInputActions.inputactions`
     (`<Mouse>/leftButton`). This is the same Input System asset — no new package.
   - In the attack controller: `if (_input.Player.Attack.WasPressedThisFrame())` →
     `_animator.SetTrigger("AttackAxe")`. Guard: block input if already in an attack state
     (`_animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack")`).

4. **Suppress `CastawayArmPose` during the clip:**
   - Add `public bool suppressForClip` to `CastawayArmPose`.
   - In the attack controller's `OnAnimatorStateEnter`-equivalent (or a coroutine started
     on `SetTrigger`): `armPose.suppressForClip = true`.
   - In `OnWeaponHit` (Animation Event handler): execute hit logic (existing `ChopTree.Chop()`
     path or equivalent) then schedule re-enable: `armPose.suppressForClip = false` after
     the clip's return arc (could also be on state-exit callback).

5. **`HeldAxeRig` — no changes needed.** It reads the final hand transform at order 100
   regardless of which system wrote it.

6. **Test (EditMode + PlayMode):**
   - EditMode: verify `Attack_Axe` state exists in the Animator Controller; verify the
     Animation Event `OnWeaponHit` is present on the clip.
   - PlayMode: trigger the attack → assert `suppressForClip == true` mid-clip → assert
     `ChopTree` chop logic fired → assert `suppressForClip == false` post-clip.

---

## Top 3 gotchas for Devon/Drew

| # | Gotcha | Fix |
|---|---|---|
| 1 | **Bake Into Pose not set → character slides** | Enable all three Bake Into Pose checkboxes on the attack clip import. Do this BEFORE judging the animation in the editor — a sliding clip looks broken even if perfectly authored. |
| 2 | **CastawayArmPose double-applies rotation** | Set `suppressForClip = true` on attack state entry; `HeldAxeRig` needs NO changes (it auto-tracks the clip-driven hand). |
| 3 | **Scale mismatch → skeleton snaps on state enter** | Confirm the attack FBX import scale factor matches the base castaway FBX. Open both in the Inspector → Rig tab → check scale value is identical. |

---

## Open questions

- **Clip filename:** the Sponsor has the clip but it is not yet in the project. Devon should
  confirm the filename before writing the Animator Controller state.
- **Hit-logic target:** `ChopTree.Chop()` is the existing chop path. Confirm whether
  `86caffwv5` should call `ChopTree.Chop()` from `OnWeaponHit`, or whether a new
  `WeaponAttackController.OnWeaponHit()` should be the seam and call chop as a subscriber.
  The Animation Event calls a method on a component on the same GameObject — the component
  structure determines where `OnWeaponHit` lives.
- **In-hand placement ticket `86caffwuz`:** that ticket covers baking the hand-local
  weapon transform. The attack clip drives the hand bone; `HeldAxeRig` reads it. The
  in-hand placement only needs to be correct relative to the hand bone — the clip's hand
  motion carries it automatically. No conflict, but `86caffwuz` should land before or
  concurrently with `86caffwv5` so the baked placement is evaluated under the attack clip's
  arc, not just the idle carry pose.
