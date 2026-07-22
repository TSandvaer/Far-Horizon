# Procedural Action-Verb Animation — Castaway Generic Rig

**MANDATORY pre-work read for ALL action-verb animation work** (chop, pick-up, drink, throw — or ANY change to `CastawayArmPose` / `HeldAxeRig` / a held-prop seating or arm-pose driver).
Full evidence, per-verb mapping, and source citations: `team/erik-consult/procedural-action-verb-animation.md` (ticket `86cae5tb3`).
Cross-refs: `unity-conventions.md` §Editor-vs-runtime (held-prop world-space posing, the walk-float saga) + §Headless / CLI rituals (the `WaitForEndOfFrame` / `Time.deltaTime≈0` traps) + §FBX / rigs / characters (body is Y-yaw-only, no tilt/lean exists — a lean/tilt ask is new work, NOT an extension of this arm-pose idiom).

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

## Looped-clip `loopPose`≠`loopBlend` importer API (86caa3kur / #197)

Set loop-pose blending on any Mixamo in-place looped clip (locomotion + idle + crouch-walk + the Stunned hold) in `CharacterAssetGen.LoopAndRename` via **`cc.loopPose = true`** on the `ModelImporterClipAnimation`. ⚠ The C# property is **`loopPose`**; it SERIALIZES to the `.meta` field **`loopBlend: 1`** (Unity API↔YAML names differ; there is NO `cc.loopBlend` importer property — writing it does not compile). Then regen + COMMIT the `.fbx.meta` (the build ships the committed snapshot; see [[unity-procedural-committed-assets-go-stale]]).

> ⚠ **`loopBlend` did NOT cause/fix the #197 sneak jerk — do not reach for it on a per-gait pose jerk.** The prior framing of this section (loop-seam = the "left, right, JERK" cause; `loopBlend:1` = the fix) is **REFUTED**: the shipped `loopBlend:1` soak `770bffd` was "FAILED, NO CHANGE" and the live-Animator probe measured loopBlend's runtime effect at **0.000°**. The real cause + the diagnostic that found it are below. The `loopPose`≠`loopBlend` API note above is still valid — keep loop-pose blending on looped clips as hygiene — it just was NOT this jerk's cause.

## When a per-gait pose jerk survives multiple fixes, measure the LIVE skeleton (86caa3kur / #197)

**The failure class.** A per-gait-cycle pose jerk ("left, right, JERK, repeat" — once per stride) that SURVIVES multiple fixes because every INDIRECT instrument is BLIND to it: a `normalizedTime` trace is a CLOCK (blind to pose — stays monotonic with clean wraps while the pose still snaps); an `agent.transform`/root-position CoV reads the ROOT (blind to the skeleton); Unity `SampleAnimation` A/B reads RAW curves (blind to `loopBlend`'s RUNTIME blend — which measured 0.000° live). A clean clock, a smooth root, and clean raw curves can ALL be true while the rendered pose jerks.

**The right instrument.** A PlayMode **Animator-tick probe**: `Animator.Update(dt)` on the REAL rig, then sample the LIVE model-bone `localRotation`s frame-by-frame across the gait cycle. That is the ONLY layer that sees the RENDERED skeleton pose — what the player actually sees. Reusable probe: `Assets/Tests/PlayMode/SneakGaitRuntimePoseProbe.cs` (on main via #197).

**The #197 cause + fix pattern.** The jerk was a MID-CYCLE clip keyframe DISCONTINUITY — `lefttoebase` snapped **80.5° in ONE frame at normalizedTime ≈ 0.907** (whole-body 106.9°) — NOT the loop wrap, NOT `loopBlend`. Fixed by `Assets/Scripts/Editor/SneakGaitCurveFix.cs` (slerp-resample ONLY the corrupted bone-curve run → committed smoothed `.anim`), guarded by `Assets/Tests/EditMode/SneakGaitCurveSmoothTests.cs`.

**THE RULE.** When a pose jerk survives ≥2 fixes, measure the LIVE rendered skeleton (tick the real Animator + sample model-bone localRotations) BEFORE guessing again. Cost of measuring the wrong layer here: **8 soaks + 3 blind instruments** before the live-skeleton probe pinned it in one pass. Relates to [[soak-fail-test-pass-instrument-runtime]] (the indirect instrument IS the blind spot).

## Debug-instrument caveat: run-lower's engagement is state-gated, not always-on (soak-239-v2)

`CastawayArmPose` exposes a **run-lower** additive offset — this doc is the MANDATORY pre-read for any `CastawayArmPose` change, yet until now named run-lower with zero further detail. **Run-lower's effect is ENGAGEMENT-WEIGHTED, not always-on: its blend weight is 0 while the character is idle/walking and only rises while the character is actually in the RUN locomotion state.** A debug nudge-tool dial that writes run-lower's target value directly, bypassing the engagement weight, changes the underlying number with NO visible effect at idle — the arm doesn't move — which reads exactly like a broken/unresponsive tool (F9 weapon-nudge panel, soak-239-v2; the Sponsor was burned by this twice before the cause was found).

**Rule for this idiom specifically: any debug/nudge instrument that targets an engagement-weighted `CastawayArmPose` field must either drive/force the gating state (e.g. force the RUN engagement to 1 while the dial is in use) or surface the current engagement weight on-screen** — a raw value dial with no engagement readout can't be told apart from a broken handler.

**Siblings (same "wired but conditionally inert" family; the general debug-tool design rule lives in `unity-conventions.md` §Input System):** the axe-head PgUp/PgDn precondition trap (F9 nudge tool — axe-head resize silently no-ops unless the axe is the currently-held weapon) and the weapon-mesh-holder stomp (`unity-conventions.md` §FBX / rigs / characters — a rig-driven transform silently overwrites a debug nudge's per-frame write, so only the `localScale` dial visibly worked). All three are instances of a debug dial whose write SUCCEEDS at the data layer while a downstream gate — animation engagement weight, held-item precondition, or rig `LateUpdate` overwrite — silently discards its visible effect.

## Held-weapon seat dials are per weapon CLASS, not per material TIER (`86caffwv5`, PR #327 @ `250e4e6`)

The in-hand seat (scale + local offset of the mesh-holder) is a property of the grip/haft GEOMETRY — the weapon CLASS — not the material tier: all three tiers of a class (stone/iron/wood) share the same family haft shape (the `blender-asset-pipeline.md` shared-style contract), so they seat identically. `HeldWeaponCycleDebug.WeaponMeshScale` (`HeldWeaponCycleDebug.cs:260`) and `WeaponMeshLocalOffset` (`HeldWeaponCycleDebug.cs:281`) apply ONE dial per class across all its tiers via shared per-index values: axe (indices 0/6/10) scale 1.0; dagger/knife (1/7/11) 0.771; sword (2/8/12) 0.950; spear (3/9/13) 0.900; pickaxe (4/5/14) 1.0 — harvested from the Sponsor's soak-6 final-dial log (`Build/soak-swings-6/sponsor-final-dial-Player.log`).

The round-7 "same dial for rock and metal" bake (Sponsor-directed, verbatim) RETIRED the previous per-tier seat outright, INCLUDING the original approved stone-axe value — the axe class was no longer a zero-locked seat once the Sponsor dialed a real in-hand seat for the whole class. **Rule: when a per-material dial turns out to be geometry-driven (class-level, not tier-level), collapse it to one dial per class and retire the old per-instance values — even previously-approved ones — rather than preserving them as a fallback.** Re-verify against the current per-class table before assuming a new tier needs its own dial; a new tier of an existing class reuses the class dial, since per-tier duplication drifts silently the next time the class dial is retuned.

This is a SEATING concern (mesh-holder transform), distinct from the `CastawayArmPose`→`HeldAxeRig` arm-pose chain documented elsewhere in this file — the class dial composes UNDER whatever pose the arm chain produces; it does not participate in it.
