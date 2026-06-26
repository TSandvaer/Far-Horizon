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
- [ ] **No Animator state changes, no AvatarMask.** The additive-offset pattern is the project idiom. A `Picking Up.fbx` exists on disk but is UNWIRED — use it only as a curve-shape REFERENCE, do NOT wire it into the Animator. Propose a layer/mask ONLY if full-body root motion is genuinely required, and scope it as its own migration ticket (it re-tests every locomotion state and breaks silently on a bone rename).

---

## Per-verb status (2026-06-25)

| Verb | Clip exists? | Arm-anim shipped? | Recommended approach |
|---|---|---|---|
| Chop | NO (`86caa4c5c` AC1 "reuse/extend" is MISLEADING — no chop clip exists; the driver IS the implementation) | No | Additive driver; `TriggerSwing()` from `ChopTree.Chop()`; downward arc (−local-Z windup→strike→return) |
| Pick-up | `Picking Up.fbx` (unwired) | No | Additive driver (clip as curve reference only); both arms bend toward the ground, ~0.6s |
| Drink | No clip | No | Additive driver, BOTH arms raised toward the face (+local-Z), slow sustained hold ~1.0–1.5s |
| Throw | No clip | N/A (future) | Additive driver (wind-back → forward arc) + a `HeldAxeRig.enabled = false` detach/projectile-spawn event at a `SwingNormT` release threshold |
