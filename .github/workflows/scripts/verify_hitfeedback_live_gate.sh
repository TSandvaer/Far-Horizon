#!/usr/bin/env bash
# verify_hitfeedback_live_gate.sh — shipped-build ENEMY HIT-FEEDBACK gate, PLAYED rather than isolated
# (ticket 86caxjwb3, added by the 2026-08-14 soak). Sibling of verify_hitfeedback_gate.sh; the two are
# COMPLEMENTARY and neither replaces the other.
#
# ================= WHY A SECOND GATE EXISTS, AND WHAT IT COST TO LEARN =================
# On 2026-08-14 verify_hitfeedback_gate.sh PASSED against build `zoned | 2026-08-14T09:41:56Z | df5edf7`:
# snake materials 13/13 lit together at 0.6200, boar decayed to zero, deathPuff=True, pool recycled. ~30
# minutes later the Sponsor played THE SAME EXE and reported, verbatim:
#
#   "snake does not flash, boar flashes once but on next hit it doesnt flash and the player and boar are
#    repositioned. when both snake and boar dies they just disappear."
#
# Two instruments disagreeing IS the finding. The isolating gate was not lying — it was answering a narrower
# question than anyone read it as answering. Three specific gaps let it be green on all three reports:
#
#  1. IT READ BACK ITS OWN WRITE. `MinMaterialFlash()` asks the material for the float the driver just
#     SetFloat'd into it, so it is green whether or not one PIXEL changed. This gate reads the creature's
#     screen box out of the actual framebuffer and reports the lit-pixel fraction beside a same-box motion
#     NOISE FLOOR sampled one frame earlier — a number with no noise floor beside it is not evidence.
#  2. IT ASSERTED AMPLITUDE, NEVER EYE-TIME. 0.6200 says nothing about how long the pulse was on screen. It
#     was FIVE FRAMES (`FLASH done peak=0,620 frames=5 over 0,080s`, every hit, N=13), on a snake that dies in
#     two axe hits. This gate asserts a frames-on-screen FLOOR.
#  3. IT ASSERTED A COUNTER FOR THE DEATH. `DeathPuffCount > 0` cannot see a body that stands frozen and
#     upright for four seconds and then pops out of existence — measured `uprightness 0,991 -> 0,991,
#     meanY 0,944 -> 0,944` across the whole window. This gate samples the body's POSE through the settle.
#
# It also fights the creatures the way a PLAYER does — both AIs LIVE (never parked), damage driven through
# `MeleeAttack.RequestAttackClick()` so the click gate, verb arbitration, cooldown and ResolveNearestTarget
# all run, and the fight continues to the kill. That is how it discovered that a fresh launch starts with an
# EMPTY BELT (`sel=- | melee wpn=0(-)`, 24/24 clicks swallowed), which the isolating gate cannot meet because
# it calls PerformAttack with a locally-built WeaponCatalog axe.
#
# ⚠ THE DEVIATIONS ARE LOGGED BY THE COMPONENT, EVERY RUN, and they are part of the verdict's meaning: the
# player's HP is raised (so a 30 s stay in boar charge-range cannot end the run), the axe is granted, the verb
# consumers are disabled for the fight (verb arbitration is not this ticket's surface), and each creature's HP
# is raised so the read gets N >= 8 landed hits. They change how MANY reads are taken and whether the fight can
# be driven at all — never what a read SAYS. Read the `DEVIATION` lines in the log with the verdict.
#
# WINDOWED (-screen-fullscreen 0), NOT -batchmode: the component uses ScreenCapture.CaptureScreenshotAsTexture
# + WaitForEndOfFrame, both dead under -batchmode (no swapchain to read back, no end-of-frame pass to resume
# the coroutine — unity-conventions.md §Headless). A "helpful" headless conversion would silently measure BLACK
# frames while the logic half still exited 0.
#
# Usage: verify_hitfeedback_live_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/hitfeedback-live-caps   logFile default: ci-out/verify-hitfeedback-live.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/hitfeedback-live-caps}"
LOG_FILE="${3:-ci-out/verify-hitfeedback-live.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_hitfeedback_live_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_hitfeedback_live] FAILED — exe not found: $EXE" >&2
  echo "[verify_hitfeedback_live]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# 600, above the isolating gate's 420: this run does not park the AI, so it has to CHASE two creatures that
# move, and it spends N >= 8 landed hits plus a full despawn window on each.
LAUNCH_TIMEOUT=600

launch_once() {
  rm -f "$ABS_CAP"/live_*.png
  rm -f "$LOG_FILE"
  echo "[verify_hitfeedback_live] launching shipped exe windowed (-verifyHitFeedbackLive): $EXE"
  echo "[verify_hitfeedback_live]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -verifyHitFeedbackLive -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero self-assert
# failure is NOT a wedge — never retry it, that would mask a genuine regression.
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_hitfeedback_live] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_hitfeedback_live] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground truth — the DEVIATION lines, the per-hit reads, the per-creature verdicts — plus
# the driver's own [HitFeedback] strike/eye-time lines, so a red names WHICH half broke without downloading the
# artifact.
if [ -f "$LOG_FILE" ]; then
  grep -E "\[HitFeedbackLive\]|\[HitFeedback\]" "$LOG_FILE" | sed 's/^/[verify_hitfeedback_live]   /' || true
fi

exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_hitfeedback_live] FAILED — the PLAYED read did not hold (exe_rc=$exe_rc)" >&2
  echo "[verify_hitfeedback_live]   minFlashFrames below its floor = the 2026-08-14 EYE-TIME class: the flash" >&2
  echo "[verify_hitfeedback_live]   fires at full amplitude and is still reported as absent. Do NOT 'fix' it by" >&2
  echo "[verify_hitfeedback_live]   raising intensity — read EnemyHitFeedback.flashSeconds' note first." >&2
  echo "[verify_hitfeedback_live]   settleVisible=False = the death is a frozen body then a one-frame pop." >&2
  echo "[verify_hitfeedback_live]   landed=0 with swallowedClicks>0 = the CLICK never became a swing; the" >&2
  echo "[verify_hitfeedback_live]   [ClickGateDiag] line above names which gate ate it (empty belt / a verb)." >&2
  exe_gate_rc=1
fi

# Frame backstop: the frames must be real swapchain content (not black/uniform/magenta). A passing run writes
# up to 3 impact frames + 3 death frames per creature; require >= 5 so a near-total capture failure reds here
# without making any single conditional frame load-bearing twice (the exit code already gates the reads).
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 5
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_hitfeedback_live] HIT-FEEDBACK LIVE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_hitfeedback_live] HIT-FEEDBACK LIVE GATE PASSED — every landed hit in a PLAYED fight renders a flash for at least the eye-time floor, and both deaths visibly topple + sink under a covering puff instead of popping"
exit 0
