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
  - ⚠ **The cheat sheet above is NOT universal — a measured counter-example exists (pending PR #354 merge, `86cay4282`).** On the pickaxe MINE clip, applying the documented "`+X` = outward spread" to the **LEFT** arm **CLOSED** the hands (separation 1.08 → 0.86 SW) — the opposite of the table. **Cause not settled:** it is equally consistent with (a) a left/right MIRROR of the local frame — note the table was verified on `mixamorig:**Right**Arm` only — and (b) the clip reaching across the body. Do not adopt either explanation as fact. **The operative rule is unchanged and is why the checklist item exists: MEASURE on the target bone AND the target clip before choosing a value. Treat the table as a starting hypothesis to confirm, never as a lookup.**
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

## Diagnose a clip-pose defect by GEOMETRY in the character's own frame, never by per-bone quaternion deviation (`86cav8xg9`, PR #337 @ `fee2604`)

A contorted-looking clip invites a per-bone quaternion diff against a reference pose. **That is the wrong instrument** — it reports large deviations on bones that look fine and small ones on the bone actually causing the read, because a quaternion delta says nothing about where the limb ends up in the body's own frame. #337's ticket premise (curve corruption, found by exactly that method) was **REFUTED**; the real cause was the **Hips hinge owning 46° of a 66° torso fold**, which only surfaces once you measure limb/torso geometry — angles and clearances relative to `ModelTransform` — rather than bone rotations.

**Rule: diagnose clip-pose defects with limb/torso geometry in the character's own frame.** Measure the fold angle, the torso clearance, the elbow angle. Per-bone quaternion deviation is for detecting *curve corruption* (a bone whose curve is genuinely broken), not for locating *a pose that reads wrong*.

**Corollary that fell out of the same PR:** because a pelvis-space fix rotates the whole body, anything expressed as a **relative** relationship is invariant under it — hand-to-hand distance, and a prop seated as `hand.rotation * Euler(relEuler)`. So a pelvis fix **cannot** cause (or cure) a two-handed-looking grip or a prop that pivots relative to the hand. That invariance is what let #337's triage separate its own bar from two unrelated defects (`86cay4282`) with a one-file diff as evidence.

## Sizing an additive arm offset: the `|Q|` blast-radius rule (`86caxgwbz`, PR #343)

A fixed additive arm offset's **worst-case arc is capped by its own rotation magnitude `|Q|`**, and the per-clip variation is the fraction of that cap the clip's elbow fold realises. Measured across 22 live clips: `Euler(-5,-22,0)` is ≈22.6°, and observed arcs ran 9.3°→19.9° — **41%→88% of the ceiling**. Idle sits at the low end (elbow ≈149°, near-straight); attack and hit-react clips sit at the high end (elbow 44°–86°).

**The sizing heuristic: dials under ~25° are clip-safe by construction; dials over ~40° need a state gate.** Magnitude alone does NOT tell you about self-intersection — DIRECTION depends on the full pose, so torso/head clearance must be measured separately and is not derivable from `|Q|`.

⚠ **These bands are CLIP-CONDITIONAL, not rig-constants — and reading them as constants has already misdirected one investigation (pending PR #354 merge, `86cay4282`).** A `|Q|` above the 40° band is a flag to go measure, **not** a diagnosis. Concrete case: `CastawayArmPose.cs:100`'s `rightArmEuler(-4,-50,-3)` (|Q| ≈ 50.3°, over the band) was written into a ticket AND an orchestrator dispatch brief as the prime suspect for a held-tool defect. It was neither the cause nor even live — it is the **v3/rollback default**, while `MovementCameraScene.cs:1450` bakes `CastawayV4RightArmEuler(-5,-22,0)` (|Q| = 22.6°, inside the safe band) on the shipped v4 hero. The real cause was elsewhere entirely (the clip's authored two-handed motion). **Before naming any over-band dial as a suspect, confirm it is the value that actually SHIPS** — a latent rollback default can sit in source looking exactly like a live dial (see also `86caxjwhh` AC5: there is no `FARHORIZON_CASTAWAY_V4=0` path, so v4 is unconditionally on and the v3 values are unreachable without a const flip).

## An additive offset gated on a GAMEPLAY signal instead of ANIMATION STATE is the trap shape (`86caxj30g`, PR #346 @ `884c611`)

The always-on carry eulers survive precisely *because* they are unconditional and small (22/22 clips fit). The dangerous one is the **conditional** offset — and specifically one whose condition is a gameplay signal (velocity, held item) rather than the animation state. It then leaks into every clip it was never dialed against. Shipped instance: `runLowerEuler(-10,12,-42)` gated on `CastawayCharacter.IsRunning` — a velocity read — reached all 5 attack swings, because the swings carry no locomotion gate. Measured contamination: 45.8–47.6° arc / up to 0.896 SW distortion on every attack clip.

**Rule: a `CastawayArmPose`-family additive offset gated on a gameplay signal must ALSO gate on Animator layer-0 state.**

**And the layer-0 read must be transition-paired.** A gate that reads only `GetCurrentAnimatorStateInfo(0)` is wrong during a crossfade: the shipped controller reports `current=Locomotion, next=AttackAxe` for the *entire* `AnyState→Attack` blend (`CharacterAssetGen.WireAttackClass` sets `any.duration = 0.06f`), so a current-only gate stays "in lane" straight through it. **Pair it with `IsInTransition(0)` + `GetNextAnimatorStateInfo(0)`** — see `CastawayCharacter.LocomotionLaneOwnsPoseFor(currentHash, inTransition, nextHash)` (`CastawayCharacter.cs:788`, live read at `:805`).

**Release must be asymmetric.** Handing the pose back to the clip has to out-run the swing: at the carry blend rate the overlay is still at weight 1.000 when the crossfade ends and only reaches 0.05 at ~0.62 s, against a ~1.05 s fast swing. The shipped fast-out (`CastawayArmPose.runLowerOverlayReleaseRate = 30f`, `:154`; policy in the pure `NextRunWeight(...)`, `:217`) reaches 0.011 by 0.150 s. **Corollary for tests: a regression guard asserting a low arc "while an attack state is active" is unachievable at swing ENTRY** — time-qualify the assert past the ease window.

⚠ **Known residual on the exit side:** there is a ~0.3–0.5 s window after a swing ends where the overlay sits at ~0 while the player is still sprinting (re-exposes `86caa83wn`). A consequence of the fast-out, flagged for soak observation — check the exit direction, not just entry.

## Held-weapon seat dials are per weapon CLASS, not per material TIER (`86caffwv5`, PR #327 @ `250e4e6`)

The in-hand seat (scale + local offset of the mesh-holder) is a property of the grip/haft GEOMETRY — the weapon CLASS — not the material tier: all three tiers of a class (stone/iron/wood) share the same family haft shape (the `blender-asset-pipeline.md` shared-style contract), so they seat identically. `HeldWeaponCycleDebug.WeaponMeshScale` (`HeldWeaponCycleDebug.cs:260`) and `WeaponMeshLocalOffset` (`HeldWeaponCycleDebug.cs:281`) apply ONE dial per class across all its tiers via shared per-index values: axe (indices 0/6/10) scale 1.0; dagger/knife (1/7/11) 0.771; sword (2/8/12) 0.950; spear (3/9/13) 0.900; pickaxe (4/5/14) 1.0 — harvested from the Sponsor's soak-6 final-dial log (`Build/soak-swings-6/sponsor-final-dial-Player.log`).

The round-7 "same dial for rock and metal" bake (Sponsor-directed, verbatim) RETIRED the previous per-tier seat outright, INCLUDING the original approved stone-axe value — the axe class was no longer a zero-locked seat once the Sponsor dialed a real in-hand seat for the whole class. **Rule: when a per-material dial turns out to be geometry-driven (class-level, not tier-level), collapse it to one dial per class and retire the old per-instance values — even previously-approved ones — rather than preserving them as a fallback.** Re-verify against the current per-class table before assuming a new tier needs its own dial; a new tier of an existing class reuses the class dial, since per-tier duplication drifts silently the next time the class dial is retuned.

This is a SEATING concern (mesh-holder transform), distinct from the `CastawayArmPose`→`HeldAxeRig` arm-pose chain documented elsewhere in this file — the class dial composes UNDER whatever pose the arm chain produces; it does not participate in it.

## Two-handed imported clips: reseat the PROP, don't de-grip the hand (`86cay4282`, PR #354 — pending merge)

⚠ **Unmerged at time of writing** (branch `drew/86cay4282-swing-defects`) — treat file/line cites against this ticket as unverifiable until merge; the RULE below is what carries.

Two pickaxe-MINE soak defects — *"swinging like he is handling the axe with both hands"* and *"the axe is still pivoting"* — resolved to ONE root cause: the Mixamo pickaxe-MINE clip is authored **two-handed**. (Confirmed by `AttackClipPoseDiag`'s prop-seat pass: the tool's seat is perfectly rigid — `axisSpreadInHand` = 0.000° on every clip — while the clip's hands lock 1.09–1.29 SW apart with the tool 63.8–89.7° off the hand line.) The first fix built a state-gated LEFT-ARM de-grip via the additive `CastawayArmPose` idiom, to pull the left hand off the phantom haft.

**On soak the Sponsor reversed the direction, verbatim: "we need to position the axe for a two hand grip"** — keep the clip's authored two-handed pose and move the PROP so it seats into both hands, instead of pulling a hand away from the prop.

**Why the reversal is the better default:** an imported two-handed clip already places both hands roughly COLLINEAR along a phantom haft — that collinearity *is* what "looks two-handed" means, i.e. the animator's own intent. An arm-pose override fights that intent and can only ever pull one hand away; a state-gated **PROP-SEAT offset** accepts the authored hand pose as ground truth and moves one cheap prop transform to meet it — no new bone-rotation math, and none of the de-grip's `|Q|` blast-radius / self-intersection exposure (see the sizing-heuristic section above).

**Rule: when a held-prop defect on an imported clip can be framed either as "fix the arm" or "fix the prop seat", default to reseating the PROP.** Reach for the arm-pose idiom only when the clip's authored grip is genuinely WRONG for the prop geometry — not merely because it "looks two-handed".

**Scope discipline:** locked to the pickaxe MINE state only. The chop seat (`HeldAxeRelEuler`) is locked across soak rounds 1-5 and stays OUT of scope. **The abandoned de-grip mechanism is KEPT at weight 0, not deleted** — it stays as an A/B dial. Don't treat an abandoned-direction mechanism as dead code to delete on sight; a Sponsor reversal can still want the old dial for comparison, and it costs nothing parked at zero.

**A direction reversal is not a defect report.** The de-grip was correctly built, tested, and green; what changed was the Sponsor's chosen answer to the same defect. Grade the round on that basis rather than as a failed implementation.
