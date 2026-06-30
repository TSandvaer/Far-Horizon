# Procedural Action-Verb Animation — Castaway Generic Rig

**MANDATORY pre-work read for ALL action-verb animation work** (chop, pick-up, drink, throw — or ANY change to `CastawayArmPose` / `HeldAxeRig` / a held-prop seating or arm-pose driver).
Full evidence, per-verb mapping, and source citations: `team/erik-consult/procedural-action-verb-animation.md` (ticket `86cae5tb3`).
Cross-refs: `unity-conventions.md` §Editor-vs-runtime (held-prop world-space posing, the walk-float saga) + §Headless / CLI rituals (the `WaitForEndOfFrame` / `Time.deltaTime≈0` traps).

The codebase has exactly ONE arm-modification idiom: a `LateUpdate` ADDITIVE bone-rotation offset right-multiplied onto the Animator's clip pose (`CastawayArmPose`). Author every action verb as an additive offset curve on that idiom — NOT a new Animator clip, state, layer, or AvatarMask.

---

## The non-negotiable chain

```
Animator (writes clip pose)
  → CastawayArmPose.LateUpdate [DefaultExecutionOrder 50]  (additive offset + run-lower)
      → HeldAxeRig.LateUpdate  [DefaultExecutionOrder 100] (seats axe on hand)
```

Any action-verb driver MUST run at `DefaultExecutionOrder` < 50 when it writes from `LateUpdate` (so it feeds `CastawayArmPose` BEFORE order 50 applies it). `HeldAxeRig` (order 100) reads the FINAL posed hand and follows automatically — never move the axe directly from the verb driver.

---

## Authoring checklist (one box per verb)

- [ ] **Measure bone axes FIRST.** Cheat sheet (verified `−armTrace`, `mixamorig:RightArm` LOCAL frame): `+local-X` = outward spread; `+local-Z` = raise/forward-reach; `+local-Y` = near-useless twist. Do NOT assume Y = up — the Mixamo Generic rig's local bone frames are arbitrary; a guessed axis swings the wrong way.
- [ ] **Expose `SwingNormT` (0→1→0) as a public property** for headless test access (drive the swing by `Time.time` anchoring, never `Time.deltaTime` accumulation).
- [ ] **Add a `swingOverrideEuler` field to `CastawayArmPose`** — reset to `Vector3.zero` when the driver's `SwingNormT >= 1`; compose it as a right-multiply AFTER `_rightOffsetQ`. Zero at rest → identity quaternion → the locked carry/idle/walk/run pose is byte-unchanged (zero cost).
- [ ] **Keep `followDamp = 0` during a fast chop-class swing** — with `followDamp > 0` the axe lags the strike peak by ≤1 frame.
- [ ] **PlayMode test: NEVER use `WaitForEndOfFrame`** (it does NOT fire in `-batchmode` — the swing never resumes). Use `WaitForSeconds` / `WaitForSecondsRealtime` (both headless-safe). Assert `SwingNormT > 0` mid-swing and `swingOverrideEuler ≈ 0` at rest. Do NOT assert on `Time.deltaTime` values.
- [ ] **Driver runs at `DefaultExecutionOrder` < 50 when writing from `LateUpdate`.** (An `Update`-writing driver is consumed at any order, but all current verb drivers use `LateUpdate` — keep order < 50.)
- [ ] **TWO idioms now coexist — pick by whether the verb needs a full-body pose or an arm-only offset.**
  - **Full-body clip verbs → base-layer Animator OVERLAY states (the Attack/Jump idiom).** As of `86cackb3j` the crouch + hit-react + stunned + pick-up clips ARE wired as clip-driven base-layer states reached by `AnyState→state` triggers/bools (see "Per-verb status" + the wired-state list below). They one-shot and return to `Locomotion`/`Idle` (or loop, for Stunned), exactly like `Attack` and `Jump` — the upright Walk↔Run blend tree stays `{Idle, Walk, Run}` untouched (the OOS-protection idiom). These overlay states still compose with `CastawayArmPose`→`HeldAxeRig`: the Animator writes the clip pose, `CastawayArmPose` adds its arm offset on top (order 50), `HeldAxeRig` seats the axe on the final hand (order 100). **`Picking Up.fbx` IS NOW WIRED as the `PickingUp` state — do NOT revert it to "reference-only".**
  - **Arm-only offset verbs (chop / drink / throw) → the additive `CastawayArmPose` offset idiom** (the rest of this doc). Use this when there is no source clip OR the verb is a quick arm gesture that must layer over live locomotion without swapping the whole-body pose.
  - **AvatarMask:** still not used — both idioms above avoid it. Propose a mask layer ONLY if a verb must play arms-only while the legs keep a *different* base clip simultaneously (neither current idiom does); scope it as its own migration ticket (it re-tests every locomotion state and breaks silently on a bone rename).

---

## Per-verb status (updated 2026-06-29, PR #186 / `86cackb3j`)

| Verb | Clip exists? | Wired? | Idiom + approach |
|---|---|---|---|
| Chop | NO (`86caa4c5c` AC1 "reuse/extend" is MISLEADING — no chop clip exists; the driver IS the implementation) | n/a | **Arm-offset driver**; `TriggerSwing()` from `ChopTree.Chop()`; downward arc (−local-Z windup→strike→return). NOTE: the Sponsor has since asked for a proper Mixamo axe-attack CLIP over the procedural swing ([[chop-swing-mixamo-clip-not-procedural]]) — when that clip lands, Chop migrates to a base-layer overlay state like the verbs below. |
| Pick-up | `Picking Up.fbx` | **YES — `PickingUp` state (`86cackb3j`)** | **Base-layer overlay state**, `AnyState→PickingUp` on the `PickUp` trigger; one-shot, returns to `Locomotion`/`Idle`. (Was "unwired / reference-only" pre-#186 — that framing is obsolete; do NOT revert.) |
| Crouch idle / crouch walk | `Crouching Idle.fbx` / `Sneak Walk.fbx` | **YES — `CrouchIdle` + `CrouchWalk` (`86cackb3j`)** | **Base-layer overlay states** on the `Crouch` bool: `Idle→CrouchIdle` (Crouch && !Moving), `Locomotion→CrouchWalk` (Crouch && Moving). A SECOND locomotion lane — the upright `{Idle, Walk, Run}` blend tree is untouched. |
| Hit-react (Body / Head / BigStomach / Stomach / Rib) | `Hit To Body` / `Head Hit` / `Big Stomach Hit` / `Stomach Hit` / `Rib Hit` `.fbx` | **YES — 5 hit-react states (`86cackb3j`)** | **Base-layer overlay states**, `AnyState→<region>` on the `Hit` trigger, clip selected by the `HitRegion` int (0=Body,1=Head,2=BigStomach,3=Stomach,4=Rib); one-shot, returns to `Locomotion(Moving)`/`Idle`. The `-verifyHitReact` shipped-build capture is AC3's gate. |
| Stunned / get-up | `Stunned.fbx` / `Getting Up.fbx` | **YES — `Stunned` + `GettingUp` (`86cackb3j`)** | **Base-layer overlay states** on the `Stunned` bool: `AnyState→Stunned` while true (looping knocked-down hold), `Stunned→GettingUp` when it flips false (one-shot recovery), then `GettingUp→Locomotion`/`Idle` on exit. |
| Drink | No clip | No | **Arm-offset driver**, BOTH arms raised toward the face (+local-Z), slow sustained hold ~1.0–1.5s. |
| Throw | No clip | N/A (future) | **Arm-offset driver** (wind-back → forward arc) + a `HeldAxeRig.enabled = false` detach/projectile-spawn event at a `SwingNormT` release threshold. |

**Wired base-layer states (the `86cackb3j` set, source: `CastawayAnimator.controller` + the `*Param` constants in `CastawayCharacter.cs`):** `PickingUp`, `CrouchIdle`, `CrouchWalk`, `HitToBody`, `HeadHit`, `BigStomachHit`, `StomachHit`, `RibHit`, `Stunned`, `GettingUp` — 10 clip-driven overlay states, plus the pre-existing `Attack`/`JumpIdle`/`JumpRunning`. Driven by the `Crouch` (bool), `Hit` (trigger) + `HitRegion` (int), `Stunned` (bool), `PickUp` (trigger) parameters. The actual TRIGGERING from gameplay/damage systems is not yet wired (the params exist + are controller-test-covered; no system sets them yet) — but the Animator states themselves are SHIPPED, NOT reference-only.

---

## Mixamo looped clips MUST set loop-pose blend, or the pose snaps at the seam (86caa3kur / #197)

Any Mixamo in-place looped clip (locomotion + idle + crouch-walk + the Stunned hold) needs **loop-pose blending ON** or the pose discontinuously SNAPS at the frame-N→frame-0 wrap once per clip cycle. For `Sneak Walk` (28-frame cycle = one L+R gait cycle) that snap WAS the Sponsor's "left, right, JERK" every 2 steps. The fix is in `CharacterAssetGen.LoopAndRename`: set **`cc.loopPose = true`** on the `ModelImporterClipAnimation` — the C# property `loopPose` serializes to the `.meta` field **`loopBlend: 1`** (Unity API↔YAML names differ; do not look for `loopBlend` on the importer — it is `loopPose`). Then regen + COMMIT the `.fbx.meta` (the build ships the committed snapshot).

**Why it hid from 3 prior instruments:** a `normalizedTime` trace stays monotonic with clean TIME-wraps and velocity stays smooth — but the POSE seam still snaps. **A clean TIME-wrap ≠ a clean POSE-wrap.** A loop-seam jerk is invisible to a clock trace; verify the actual pose-blend flag (`loopBlend` in the `.meta`), not the clip's normalizedTime.
