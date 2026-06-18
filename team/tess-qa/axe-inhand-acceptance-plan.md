# QA Acceptance Plan — held axe during vigorous locomotion (`86caa83wn`)

**Ticket:** `fix(locomotion): held axe swings into head during run/jump — tame axe-follow during vigorous locomotion`
**PR:** _(pending — Devon impl complete + committed locally `db429c9`, awaiting the single Unity build slot to verify + push)_
**Authored:** pre-staged 2026-06-17 against the ticket ACs (Devon's branch NOT yet pushed — plan is AC-driven, not diff-driven; re-confirm against the real diff when the PR opens).

## What this fix is (one line)

The raw-hand-follow held axe (`HeldAxeRig.LateUpdate`: `transform.position = hand.position + hand.rotation * worldOffsetFromHand`) was tuned against the IDLE/WALK low-arm pose. The Mixamo RUN clip (and the running-jump clip) pumps the right arm UP near the head, so the rigidly-following axe rides INTO the head and reads "unseated" through run/jump. Fix tames the follow **per-locomotion-state** (clamp/damp/mask) so the axe can't swing above shoulder height during RUN+JUMP, **while leaving the Sponsor-approved walk/idle seat + the locked follow-the-arm-at-walk choice untouched.** This is a FEEL call with an F9 runtime knob for the Sponsor (per `sponsor-prefers-direct-tweak-tools-for-fiddly-placement`).

## Process-gate preflight (HARD gates — bounce on any miss)

1. **Diagnose-Before-Fix.** `fix(...)` PR body MUST carry (a) the diagnosed root cause in one sentence + (b) ONE cited isolation result, BEFORE the fix description (TESTING_BAR §Diagnose-Before-Fix). The ticket already supplies a textbook diagnosis (rigid raw-hand follow + RUN clip arm-up + the byte-identical-offset isolation proving it's the clip pose, NOT a lost fix) — the PR body must restate it with a cited isolation line (e.g. an axe-vs-head trace / clamp-off→on guard red→green / a shipped run-cycle capture frame). Missing → REQUEST_CHANGES, one-line.
2. **Self-Test Report comment** present before I review (UX-visible: combat/locomotion/visual). What was run, **on which build stamp** (`BUILD <tag> | <UTC> | <sha>` from the HUD), what was observed — concrete values only. Missing → REQUEST_CHANGES (hard gate).
3. **Regression guard line in the Done clause** (PR #216 process gate) + **cross-lane integration check** in the Self-Test Report.
4. **No diff touch on `team/DECISIONS.md`** (non-Priya). If touched → bounce.
5. **CI green** cited by the actual run ID on the PR head SHA (not local results; local EditMode/PlayMode/serve_soak ≠ CI). Empty run (`total == 0`) is a failure.

## Mechanical sign-off — what QA can verify headlessly / from the shipped capture

### A. EditMode + PlayMode suites (paired tests, all green from the `-testResults` XML `<test-run result="Passed">` line, not exit codes)

> **Headless-time discipline:** anything that must observe the RUN/JUMP *clip pose* must be EditMode + `AnimationClip.SampleAnimation` — NOT PlayMode. In headless PlayMode `Time.deltaTime≈0`, the Animator never advances the clip, the run/jump pose never manifests, and a PlayMode "run" assertion green-passes trivially (the documented false-green class; see `CastawayRunClipGroundedTests` header). Per-frame *follow/clamp math* on a synthetic hand can stay PlayMode (it doesn't need the Animator to tick).

| # | Expectation | How | Notes / tolerance |
|---|---|---|---|
| AC1 | **Axe stays seated (no into-head) through the RUN cycle.** | EditMode: pose the `RunClip` ("CastawayRun", `RunFbxPath`) at N≥8 phases via `SampleAnimation`; for each, run the production seating math and assert the axe pivot is **below the head bone / below shoulder height** and within a seated tolerance of the grip. | The into-head swing is the bug — assert the axe Y stays under a head/shoulder ceiling across the WHOLE cycle, sampled scale-immune (unit-scale TRS, never `localToWorldMatrix` — the 100× FBX trap). N≥8 phases, not 3 (sample-size discipline). |
| AC1b | **Same, through the JUMP clip.** | EditMode: pose `JumpRunningClip` ("CastawayJumpRunning", `JumpRunningFbxPath`) — and `JumpIdleClip` if the fix scopes it — at N≥8 phases; same below-ceiling + seated-tolerance assertion. | Jump's running-arm motion is the sibling of run (ticket: "Jump.fbx will exhibit the same"). Confirm both jump clips covered or the PR states why idle-jump is out of scope. |
| **NIT-AC2** | **Finger-curl covers the RUN clip** (absorbed PR #68 NIT). | EditMode/PlayMode finger-curl guard extended so fingers curl on the haft (no mangled/open splay) **through the RUN clip pose**, gated on `HasAxe` (`CastawayFingerCurl.IsGripping`). | The existing `CastawayFingerCurlPlayModeTests` asserts empty-handed-open vs held-curl on a SYNTHETIC hand; the #68 absorption needs the curl verified against the REAL run-clip hand pose (EditMode+SampleAnimation) or a shipped-capture finger close-up. Empty-handed must STILL stay open. |
| **NIT-AC4** | **Axe seated through the RUN cycle** (absorbed PR #68 NIT) — within seated tolerance across the whole run cycle, not just spawn. | EditMode run-clip sample sweep (same rig as AC1). | This is the per-frame run-cycle envelope guard; distinct from AC1's into-head ceiling. Sample EVERY phase, N≥8. |
| AC3 | **Walk/idle seat UNCHANGED.** | EditMode: pose `WalkClip`/`IdleClip`, assert the axe seat is byte-equal (within float noise) to the **pre-fix** seat from those clips. | The fix must be per-state — a regression that re-tunes the single offset breaks walk/idle. Pin the walk/idle seat so the per-state clamp can't bleed into it. |
| AC4 | **Across-facings seat invariant preserved.** | `HeldAxeStaysSeatedAcrossFacingsPlayModeTests` (existing) stays green — the hand-local-offset-rotated-by-hand contract is untouched. | The facing fix (`86ca9qwvd`/`86ca9xz00`) is KEPT; clamp must not re-introduce a world-fixed offset. |
| AC5 | **F9 runtime clamp knob** wired + INERT in normal play. | EditMode/PlayMode: the AxeNudgeTool (or a new per-state clamp field) is serialized, toggles on F9, does nothing until toggled (no HUD, no input read in normal play — `AxeNudgeToolPlayModeTests` pattern). | If the fix adds a 5th nudge target or a clamp scalar, it inherits the inert-until-toggled contract. The Sponsor dials it; verify the log prints a copy-pasteable bake value. |
| AC6 | **No-cumulative-drift / bounded follow preserved.** | If the fix adds damping per-state, the `FollowPos`/output-vertical no-ratchet guard (the `86ca9ykp0` lesson) stays green — a damp must ease toward the LIVE hand, never integrate a baseline. | Guard the OUTPUT vertical over N walk/run steps (assert NO cumulative drift), not merely that an anchor "settles." |

### B. Regression guards (must stay green — explicitly re-run, cite the result)

- **`modelSoleGround` / `ComputeModelGroundLocalY` UNTOUCHED.** `CastawayRunClipGroundedTests` (`RunClip_LiftsTheSoleOffTheRoot_TheRealBug` + `ModelGroundingMath_CancelsTheRunLift_AcrossTheCycle`) stays green. The axe fix must NOT touch the float/grounding system (ticket OOS). Confirm the diff does not edit `CastawayCharacter.modelSoleGround`, `ComputeModelGroundLocalY`, or `ApplyGroundSnap`.
- **Axe-follows-arm-at-walk preserved** (locked Sponsor choice `86ca9zcjn`). The walk/idle follow must still swing with the arm — the clamp is RUN/JUMP-only. `HeldAxeWalkBouncePlayModeTests` + the seated-across-facings suite stay green.
- **Jump→Locomotion no-exit-time return** (`86ca9yq3q` / PR #69) stays green — the controller transition guard. The axe fix touches the held prop, not the jump state machine; confirm no controller regression.
- **Static-state reset guard** (`StaticStateResetTests`) — if the fix adds any mutable runtime static (a per-state clamp cache), it MUST carry a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset or the structural guard reds.

### C. Shipped-build gameplay-cam capture checks (HARD — editor evidence is necessary, never sufficient)

> Per `verify-grounding-soaks-by-gameplay-cam-visual` + the false-green family: judgment-grade visual captures use the **DEFAULT GAMEPLAY orbit cam** at its real pitch/zoom + actual scene lighting/post — NOT an isolated hero shot or a subject-fit zoom (those lied 4× in this project). The headless EditMode suite CANNOT validate the WebGL/WebGL2-equivalent runtime visual surface — the BUILT exe is the authority.

QA independently RE-RUNS the verify gate (`serve_soak.sh` from a CLEAN checkout, or the fix's `-verify*` capture mode) and judges its OWN artifact (image attachment from agents is structurally impossible — PR #13 precedent). Checklist:

1. **HUD stamp == HEAD** before judging anything. Read the BAKED stamp out of `FarHorizon_Data/resources.assets` (or trust `serve_soak`'s stamp-vs-HEAD guard) — NOT the `FarHorizon.exe` stub mtime (it never changes per build; the freshness trap). Never reuse an existing `Build/Windows` exe.
2. **Axe below the head through the RUN cycle** — capture the gameplay-orbit frame while RUNNING (held axe equipped), MULTIPLE phases of the run cycle. Axe must sit at the side/grip, NOT intersecting the head. Shoot from the default over-shoulder orbit the player actually uses (a front-only or top-only angle masked the hair defect — same family).
3. **Axe below the head DURING the JUMP** — capture the running-jump apex frame; axe stays seated, no into-head.
4. **Walk/idle seat visually unchanged** vs the Sponsor's approved seat — A/B the walk-cycle + idle frames against the pre-fix build's seat. Must read identical.
5. **Fingers curl on the haft** (not splayed/mangled) in the run + jump capture (the #68 NIT, visual confirmation).
6. **CaptureGate frames real** (not black/empty/uniform/all-magenta) — `frame_check.py` PASS line quoted.

## Sponsor feel-soak — what QA CANNOT sign off; needs the Sponsor's eye

This is a FEEL/taste call (`sponsor-taste-overrides-art-direction-board`; `sponsor-prefers-natural-lively-motion`). Mechanical gates can prove the axe stays below a geometric ceiling and the walk seat is unchanged — they CANNOT judge whether the tamed run/jump follow READS natural vs over-damped/locked, or whether the clamp height the Sponsor wants is exactly where the fix put it.

- **Does the tamed run/jump follow read natural?** The ticket warns: damp smooths jitter but may NOT stop the macro into-head swing; over-clamping reads as a detached/locked axe (the exact thing the Sponsor REVERSED in `86ca9zcjn`). "Lively, lightly damped, not static" is the Sponsor's standing preference — only he can confirm the balance.
- **Is the run/jump clamp height right?** The F9 knob exists precisely so the Sponsor dials it himself rather than the team grinding blind iterations. If the soak rejects, the fast path is: serve the F9 build, let the Sponsor dial the clamp + read the bake value, bake it — do NOT iterate blind (per the direct-tweak-tool memory; max ~2 soak-rejects before instrumenting).
- **Soak hand-off format** (`soak-handoff-path-and-explicit-test-checklist`): the soak ask MUST give the exact exe path + the expected HUD stamp AND an explicit "run, jump, watch the axe stays below the head; walk + idle look unchanged; F9 to dial if off" checklist. Never make the Sponsor guess what to launch or judge.

## Verdict routing

- Devon's PR → I QA it (game-side dev PR). Verdict as a PR comment: APPROVE / APPROVE_WITH_NITS / REQUEST_CHANGES. If shared-auth blocks `--approve`, submit as `COMMENTED` with the verdict up front (#211 precedent).
- **Drain-mode posture:** approve non-critical nits in the review body so closure lands; reserve REQUEST_CHANGES for failed AC, missing Self-Test Report, regression, missing N≥8 evidence, missing Diagnose-Before-Fix.
- **Branch-checkout note:** if devon-wt still holds the branch, check out the SHA DETACHED or review from `origin/<branch>` — never `git checkout <branch>` (the worktree-lock trap).

## Open items to confirm when the PR opens (plan was AC-driven, not diff-driven)

- Which approach landed — (a) per-state clamp, (b) per-state damp, (c) avatar-mask the run/jump right arm? The QA emphasis shifts: (a)/(b) → assert the per-state envelope + the F9 knob; (c) → assert the mask doesn't bleed into walk/idle and the masked arm still reads natural.
- Whether BOTH jump clips (`JumpRunning` + `JumpIdle`) are covered or idle-jump is explicitly OOS.
- Exact test file names + new clamp field/API on `HeldAxeRig` (this plan assumes the field set on `main`: `worldOffsetFromHand`, `relEuler`, `followDamp`, `FollowPos`).
