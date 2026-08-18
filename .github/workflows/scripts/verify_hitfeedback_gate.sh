#!/usr/bin/env bash
# verify_hitfeedback_gate.sh — shipped-build ENEMY HIT-FEEDBACK capture gate (ticket 86caxjwb3 AC7).
# Direct sibling of verify_boar_gate.sh (same WINDOWED launch shape, same self-asserting-component +
# frame-backstop shape).
#
# WHY THIS GATE EXISTS, AND WHY IT CANNOT BE A TEST. The defect class this ticket's [DFC-1] names is a
# PERMANENTLY-WHITE ENEMY: if the flash decay is driven from a C#-written `Time.time` stamp differenced
# against the shader's `_Time.y` — which is `Time.timeSinceLevelLoad`, NOT `Time.time` — the numerator is a
# constant negative and the term saturates to 1.0 forever, from the first hit until the process exits. The
# IMPACT FRAME of that build looks correct. An EditMode test asserting "SetFloat was called with a rising
# timestamp" is GREEN on it. It is invisible to EditMode BY CONSTRUCTION, because the two clocks only differ
# by the time the level took to load — which is zero in the editor and never zero in a built player.
# So the only place it can be caught is a real frame, a beat AFTER the hit, in a shipped exe. That is this
# gate: HitFeedbackVerifyCapture shoots an UN-HIT control, the impact, the flinch, and then the SAME creature
# at the SAME framing ~0.5s later, and self-asserts the material value has returned to exactly 0.
#
# It also self-asserts four sibling classes that each leave every other assertion green:
#   * ALL of a creature's part-materials flash TOGETHER (7 boar / 13 snake). A naive singular
#     GetComponentInChildren flashes 1 of 7 — a flash on the body but not the head reads as a bug, not juice.
#   * the SHARED driver lights a SECOND creature with no per-enemy branch (the snake half).
#   * the pooled dust puff actually RECYCLES ([DFC-4c]: without `main.stopAction = Callback`,
#     OnParticleSystemStopped is never delivered, the pool never gets its instances back, and the "pooled"
#     claim is silently false while emitting keeps working).
#   * the puff material's shader RESOLVES in the shipped player and is not Unity's error shader ([DFC-4b] —
#     this gate IS the "verify in the BUILT exe, do NOT assume" half of that constraint; the puff material
#     ships via its serialized reference in Boot.unity rather than an AlwaysIncludedShaders pin, which is the
#     mechanism the R5 unpin 86cahne3d measured and adopted for URP package shaders).
#
# WINDOWED (-screen-fullscreen 0), NOT -batchmode: HitFeedbackVerifyCapture uses
# ScreenCapture.CaptureScreenshot + WaitForEndOfFrame, both DEAD under -batchmode (no swapchain to read back
# + no end-of-frame render pass to resume the coroutine — unity-conventions.md §Headless). A "helpful"
# headless conversion would silently produce BLACK frames while the logic half still exited 0 (the #287
# false-empty class); the launch-mode registration in tests/scripts/test_gate_scripts.sh reds that.
#
# WEDGE HARDENING (mirrors capture_gate.sh / verify_boar_gate.sh): `timeout -k 15` SIGKILLs a SIGTERM-ignoring
# hung player, and a single rc==124-only retry re-launches ONCE on a first-frame present-loop wedge before
# declaring failure (a real non-zero gate failure is NEVER retried — that would mask a genuine regression).
# LAUNCH_TIMEOUT is 420 (above the boar gate's 360): this coroutine walks the player to TWO creatures in
# sequence (two 22s walk deadlines), holds a 0.5s decay window per creature, spends up to 14 axe hits at 0.25s
# on the kill, waits up to 6s for the pool to drain, and writes 7 captures.
#
# Usage: verify_hitfeedback_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/hitfeedback-caps   logFile default: ci-out/verify-hitfeedback.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/hitfeedback-caps}"
LOG_FILE="${3:-ci-out/verify-hitfeedback.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_hitfeedback_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_hitfeedback] FAILED — exe not found: $EXE" >&2
  echo "[verify_hitfeedback]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

LAUNCH_TIMEOUT=420

# launch_once — clear stale artifacts, launch the windowed exe under timeout, set exe_rc. Re-clears EVERY
# attempt so a partial first-attempt capture/log can't mask the retry.
launch_once() {
  rm -f "$ABS_CAP"/hit_*.png
  rm -f "$LOG_FILE"
  echo "[verify_hitfeedback] launching shipped exe windowed (-verifyHitFeedback): $EXE"
  echo "[verify_hitfeedback]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -verifyHitFeedback -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero
# self-assert failure is NOT a wedge — never retry it.
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_hitfeedback] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_hitfeedback] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) — including the per-creature measurement lines and the
# final `GATE PASS/FAIL: … boarDecayedToZero=… snakeDecayedToZero=… poolRecycled=…` line, so a red names WHICH
# half broke without downloading the artifact.
if [ -f "$LOG_FILE" ]; then
  grep -F "[HitFeedbackVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_hitfeedback]   /' || true
fi

# Check 1 — the exit code IS the gate. The component self-asserts the whole read (rested-before → all parts
# flash together → flinch → DECAYED BACK TO ZERO → puffed, on BOTH creatures, plus the death puff and the pool
# recycling), else Quit(1).
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_hitfeedback] FAILED — -verifyHitFeedback self-assert reported the flash/flinch/puff read did NOT hold (exe_rc=$exe_rc)" >&2
  echo "[verify_hitfeedback]   a 'decayedToZero=False' in the lines above is the LATCHED-FLASH class ([DFC-1]) — read the ticket's AC2 before 'fixing' the amplitude" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the frames must be real swapchain content (not black/uniform/magenta — the last
# would be a shader-strip regression on the particle material, which is exactly the [DFC-4b] question this
# gate answers empirically). A PASSING run writes all 7 frames; require >= 5 so a near-total capture failure
# reds here without making a single conditional frame load-bearing twice (the exit code already gates those).
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 5
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_hitfeedback] HIT-FEEDBACK CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_hitfeedback] HIT-FEEDBACK CAPTURE GATE PASSED — flash (all parts together) + flinch + pooled dust puff fire on BOTH creatures in the shipped build, and the flash DECAYS BACK TO BASE COLOUR"
exit 0
