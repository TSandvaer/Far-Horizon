#!/usr/bin/env bash
# verify_swings_gate.sh -- shipped-build SWING capture gate: the 5 per-class swings, the pickaxe
# deep-fold ceiling, the MINE TWO-HAND GRIP palm assert, and the round-5 ARM-RELEASE term
# (ticket 86caynve9 -- the CI-wiring follow-up from Devon's PR #354 round-5 review, NIT 6:
# "`git grep verifySwings -- .github/` = 0 today; the capture job's log on run 30540738473 never
# emits [swing-twohand]/[swing-release], so #354's own headline regression guard never runs").
#
# Direct sibling of verify_boar_gate.sh (the WINDOWED launch shape + wedge-retry hardening) and of
# verify_mine_gate.sh / verify_boulder_gate.sh (the self-asserting-component + frame-backstop shape).
#
# WHY THIS GATE EXISTS: PR #354 names its Regression guard (Done clause) as "the shipped
# -verifySwings palm assert (pinEngaged + palmMeasured + leftPalmOnHaft + rightWristOnHaft, exit
# code = verdict)". That assert is the only thing standing between the codebase and a silent return
# of the round-3 defect the Sponsor rejected by eye (left hand 28.2 cm off the shaft, while the
# round-3 gate was GREEN at its then-cap of 0.80 SW). Nothing in CI ran it -- so from #354's merge
# onward its headline guard was a manual ritual that protects the change only for as long as
# somebody remembers to type the flag. -verifySwings is also the most expensive gate on the board to
# re-derive by hand: it needs the pickaxe selected, the mine swing running, a 156-frame scored
# sweep, an F9 panel pass and a release sweep -- exactly the setup a human skips.
#
# This launches the BUILT exe WINDOWED with -verifySwings, which drives SwingVerifyCapture:
# fire all 5 per-class swings through the production TriggerAttack seam (asserting the routing and
# that the skinned mesh stays at the player -- the Generic-rig cone-explosion guard, 86ca8rdkp);
# measure the LIVE composed pickaxe torso fold against its 50deg ceiling; run the two-hand grip pass
# and score the LEFT PALM (not the wrist 5.6 cm behind it) against the mesh-derived touch bound;
# prove the F9 mine-seat panel draws + its rows carry real numbers; then fire one more mine swing
# with the panel CLOSED and watch the left-arm pin RELEASE frame by frame. It calls
# Application.Quit(pass ? 0 : 1) (SwingVerifyCapture.cs:588), so the exe's exit code IS the gate
# verdict -- this wrapper propagates it, and adds the two checks below.
#
# CHECK 2 IS NOT DECORATION -- THE EXIT CODE ALONE CANNOT SEE A SKIPPED PASS. SwingVerifyCapture
# deliberately makes a missing precondition LOUD IN THE LOG rather than RED, and says so in its own
# source: _releaseOk "defaults TRUE only so a SKIPPED pass cannot red the whole gate; a skip is LOUD
# in the log instead" (SwingVerifyCapture.cs:593). The same shape holds for the fold pass (:224),
# the two-hand grip pass (:317), the left-arm pin (:276), the held-weapon force (:309) and the F9
# panel (:749) -- each warns verbatim "do NOT read a PASS here as proof ...". Concretely: if
# mixamorig:Hips/Head do not resolve on the live rig, the ENTIRE fold + grip + panel + release block
# is skipped, foldOk/gripOk/_releaseOk all stay at their `true` initialisers, and
# `pass = allRouted && meshStayed && foldOk && gripOk && _releaseOk` (:564) is TRUE with the palm
# never measured once. Exit 0. Green CI. That is the very false-green class this ticket exists to
# close, so CI-wiring the exit code WITHOUT converting those warnings into a red would ship a gate
# that can rubber-stamp the defect it was built for. Check 2 does that conversion.
#
# WINDOWED (-screen-fullscreen 0), NOT -batchmode: SwingVerifyCapture captures via
# ScreenCapture.CaptureScreenshot (SwingVerifyCapture.cs:962), which reads the BACKBUFFER -- dead
# under -batchmode (no swapchain). That is the boundary sentence in unity-conventions.md Headless
# verbatim: a gate MUST stay WINDOWED "iff any judged pixel comes from the BACKBUFFER -- i.e.
# ScreenCapture.CaptureScreenshot / WaitForEndOfFrame, or a screen-space IMGUI / UI-Toolkit
# OVERLAY". Both halves apply here: the capture call itself, AND the F9 mine-seat panel pass which
# photographs an IMGUI overlay (swing_pickaxe_panel.png) that never composites into a camera
# RenderTexture. A "helpful" headless conversion yields BLACK frames while the logic half still
# exits 0 -- the #287 false-empty class. Registered in WINDOWED_GATES in
# tests/scripts/test_gate_scripts.sh, which reds exactly that conversion.
#
# WEDGE HARDENING (86cafzaeb; mirrors capture_gate.sh / verify_boar_gate.sh / verify_boulder_gate.sh):
# `timeout -k 15` SIGKILLs a SIGTERM-ignoring hung player, and a single rc==124-only retry
# re-launches ONCE on a first-frame present-loop wedge before declaring failure (a real non-zero
# gate failure is NEVER retried -- that would mask a genuine swing/grip/release regression).
# LAUNCH_TIMEOUT is the family default 300: the measured coroutine is ~30s of gameplay (5 swings at
# ~1.2s, two 2.6s measure windows plus their shoot passes, the panel pass, and a release sweep
# bounded at FoldWindowSec*2.5 = 6.5s), so 300 leaves wide headroom over a healthy-but-slow run
# while still distinguishing a wedge from normal completion.
#
# Usage: verify_swings_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/swings-caps   logFile default: ci-out/verify-swings.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/swings-caps}"
LOG_FILE="${3:-ci-out/verify-swings.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_swings_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_swings] FAILED -- exe not found: $EXE" >&2
  echo "[verify_swings]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

LAUNCH_TIMEOUT=300

# launch_once -- clear stale artifacts, launch the windowed exe under timeout, set exe_rc. Re-clears
# EVERY attempt so a partial first-attempt capture/log can't mask the retry (and so Check 2 can
# never read a PREVIOUS run's verdict -- the #130 stale-log false-green class).
launch_once() {
  rm -f "$ABS_CAP"/swing_*.png
  rm -f "$LOG_FILE"
  echo "[verify_swings] launching shipped exe windowed (-verifySwings): $EXE"
  echo "[verify_swings]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  # Windowed + small so it never grabs the desktop. -logFile redirects the standalone player's
  # Player.log so the verdict lines are grep-able here. The component calls Application.Quit(0/1).
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -verifySwings -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero
# self-assert failure is NOT a wedge -- never retry it (it would mask a genuine regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_swings] WARN -- exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) -- retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_swings] FAILED -- exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the DECISIVE verdict lines for the CI log, so a red names WHICH half broke without anyone
# downloading the artifact. Curated rather than "every marker line": a healthy run emits ~68 marker
# lines (the release pass alone traces 30 frames), and the full log ships as an artifact anyway.
# The needle set is the four criterion-bearing lines PLUS every skip/FAIL warning Check 2 reds on.
DECISIVE_RE='\[SwingVerifyCapture\] verification complete|\[swing-twohand\] engaged=|\[swing-twohand\] LEFT-ARM PIN|\[swing-release\] crossfade OUT|\[swing-release\] FAIL|SKIPPED|no CastawayLeftArmHaftIk|no HeldWeaponCycleDebug|no AxeNudgeTool'
if [ -f "$LOG_FILE" ]; then
  grep -E "$DECISIVE_RE" "$LOG_FILE" | sed 's/^/[verify_swings]   /' || true
fi

# ---------------------------------------------------------------------------
# Check 1 -- the exit code IS the gate. SwingVerifyCapture self-asserts
# `allRouted && meshStayed && foldOk && gripOk && _releaseOk` (SwingVerifyCapture.cs:564) where
# `gripOk = engaged && pinEngaged && leftOn && rightOn` (:489), else Quit(1). A non-zero exe_rc
# means a swing failed to route, the mesh cone-exploded, the pickaxe fold went past its ceiling, the
# left PALM left the haft, the seat pulled the haft out of the right hand, or the left arm never let
# go -- exactly the regressions this gate exists to catch.
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_swings] FAILED -- -verifySwings self-assert reported a swing/fold/two-hand-grip/release regression (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# ---------------------------------------------------------------------------
# Check 2 -- EVIDENCE PRESENCE. See the header: a skipped pass is LOUD IN THE LOG, not red, so the
# exit code alone can report PASS on a run that never measured the palm at all. This check requires
# the criterion terms to be PRESENT AND TRUE, and requires every documented skip warning to be
# ABSENT. Both halves are needed: presence alone would miss a skip that also happens to print an
# unrelated True, and absence alone would miss a truncated run that never reached the verdict.
#
# Needles are deliberately BOOLEAN TOKENS ONLY -- never numeric. The shipped player writes floats
# with the RUNNER'S locale: a real passing run on a Danish-locale Windows box logs
# `worstLeftPALM=0,239SW=10,6cm`, with a COMMA. A needle like `0.239` would false-red there while
# passing on an en-US runner, which is a gate that depends on the machine rather than the build.
# Booleans (`True`/`False`) are locale-invariant.
#
# Every needle below was validated against a REAL passing -verifySwings Player.log from the round-5
# build (Drew's soak4-swings run, 12/12 frames, `releaseOk=True => PASS=True`): each REQUIRED needle
# matched >= 1 line, each ABSENT needle matched 0.
# ⚠ DO NOT PRUNE THIS LIST AS "REDUNDANT WITH THE SUMMARY LINE" -- the four IN-BLOCK needles are the
# only thing standing between this gate and a silent false-green. Verbatim from Drew's #369 review
# (comment 5136309565): SwingVerifyCapture.cs's `if (castaway != null && animator != null)` guard wraps
# the WHOLE measurement block, and a run with `castaway != null && animator == null` skips every check
# while `allRouted` still computes TRUE (:164) and foldOk/gripOk/_releaseOk keep their `true`
# INITIALISERS -- so the verdict line prints `foldOk=True gripOk=True releaseOk=True => PASS=True` and
# the exe exits 0 having measured NOTHING. Consequence for the needle set below:
#   * `verification complete` and `releaseOk=True` are printed by that SUMMARY line and are therefore
#     TAUTOLOGIES on the skipped path -- they prove the coroutine finished, never that anything was
#     measured. They stay because they catch the TRUNCATED run (S7), not because they carry the skip.
#   * `pinEngaged=True`, `palmMeasured=True`, `leftPalmOnHaft=True`, `rightWristOnHaft=True` are emitted
#     ONLY from inside the guard. They are the load-bearing four: delete any one and the skipped-rig run
#     loses a detector; delete all four and it greens outright.
# The 86caynve9 round also added an `else` on that guard emitting the existing "fold pass SKIPPED"
# ABSENT needle, so the path now reddens on BOTH halves of Check 2 -- but the presence half must remain
# sufficient on its own, because a future refactor can drop a warning far more easily than a criterion
# log line. Regression-guarded by `verify_swings S4b` in tests/scripts/test_gate_scripts.sh, which
# models the WORST shape (silent guard, no skip warning at all) and asserts RED.
REQUIRED_NEEDLES=(
  "[SwingVerifyCapture] verification complete"  # the coroutine reached its verdict at all (TAUTOLOGY on
                                                #   the skipped-rig path -- see the header; keeps S7 honest)
  "pinEngaged=True"                             # LOAD-BEARING (in-block): left-arm haft IK existed AND engaged
  "palmMeasured=True"                           # LOAD-BEARING (in-block): palm anchor resolved (fails closed)
  "leftPalmOnHaft=True"                         # LOAD-BEARING (in-block): THE round-4 palm-touch criterion
  "rightWristOnHaft=True"                       # LOAD-BEARING (in-block): seat kept the haft in the right hand
  "releaseOk=True"                              # THE round-5 term: the left arm let go on time (TAUTOLOGY on
                                                #   the skipped-rig path -- see the header)
)
# Each of these is a verbatim fragment of a Debug.LogWarning whose own text says some variant of
# "do NOT read a PASS here as proof ...". Any hit means the evidence for a criterion is MISSING from
# this run even though the exit code may be 0.
ABSENT_NEEDLES=(
  "fold pass SKIPPED"            # SwingVerifyCapture.cs:224 -- Hips/Head unresolved; the WHOLE
                                 #   fold+grip+panel+release block never ran
  "two-hand grip pass SKIPPED"   # :317 -- arm/hand bones or the HeldToolRig unresolved
  "[swing-release] SKIPPED"      # :628 -- release pass skipped; _releaseOk keeps its `true` default
  "no CastawayLeftArmHaftIk"     # :276 / :628 -- the left-hand PIN is absent from this build
  "no HeldWeaponCycleDebug"      # :309 -- the held weapon was never forced to the pickaxe, so any
                                 #   grip figure describes whatever tool happened to be in hand
  "no AxeNudgeTool"              # :749 -- the F9 mine-seat instrument is absent from this build
)

evidence_rc=0
if [ ! -f "$LOG_FILE" ]; then
  echo "[verify_swings] FAILED -- no player log at $LOG_FILE; the shipped exe produced NO verdict evidence" >&2
  evidence_rc=1
else
  for n in "${REQUIRED_NEEDLES[@]}"; do
    if grep -qF -- "$n" "$LOG_FILE"; then
      echo "[verify_swings]   evidence OK      : '$n'"
    else
      echo "[verify_swings]   evidence MISSING : '$n' -- the shipped run never reported this criterion as met" >&2
      evidence_rc=1
    fi
  done
  for n in "${ABSENT_NEEDLES[@]}"; do
    if grep -qF -- "$n" "$LOG_FILE"; then
      echo "[verify_swings]   evidence SKIPPED : '$n' -- a precondition was missing, so this criterion was NOT measured (the component warns LOUDLY instead of failing; this gate reds it)" >&2
      evidence_rc=1
    fi
  done
fi
if [ "$evidence_rc" -ne 0 ]; then
  echo "[verify_swings] FAILED -- the two-hand-grip palm assert and/or the arm-release assert did not RUN-and-PASS in the shipped build. A green exit code with missing evidence is the false-green this gate exists to close (SwingVerifyCapture makes a skipped pass loud in the log, not red)." >&2
fi

# ---------------------------------------------------------------------------
# Check 3 -- frame backstop: the swing frames must be real swapchain content (not black/uniform/
# magenta -- the last would be a shader-strip regression). A PASSING run writes 12 frames, but only
# the FIVE per-class swing frames (swing_axe/pickaxe/dagger/spear/sword.png) are UNCONDITIONAL; the
# fold / two-hand / panel / release frames are state-conditional, so require >= 5 -- enough that a
# near-total capture failure fails here, without making a state-conditional frame load-bearing twice
# (Check 1 + Check 2 already gate those states). Measured margins on a real 12-frame run: worst
# variance 511.3 (floor 8.0), mean_luma 60.0..89.5 (band 6..250), magenta 0.00 on every frame.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 5
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$evidence_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_swings] SWINGS CAPTURE GATE FAILED (exe_rc=$exe_rc evidence_rc=$evidence_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_swings] SWINGS CAPTURE GATE PASSED -- 5 per-class swings routed, the pickaxe fold stayed under its ceiling, the LEFT PALM sat on the haft and the RIGHT wrist kept it, and the left arm released on time, all proven end-to-end in the shipped build"
exit 0
