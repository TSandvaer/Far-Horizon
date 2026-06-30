#!/usr/bin/env bash
# verify_sneak_gate.sh — shipped-build SNEAK-WALK LOOP-HITCH instrument run (ticket 86caa3kur RE-SOAK, attempt 2).
# Sibling of verify_water_gate.sh / verify_chop_gate.sh / capture_gate.sh, purpose-built to DUMP the live
# per-frame Animator ground truth during the crouch sneak-walk so the LOOP HITCH the Sponsor re-soaked ("the
# crouch sneak-walk lags between each walk animation — two steps repeated, lags between each") is DISCRIMINATED
# among the three candidate causes (clip loop-seam / foot-sync stall / state re-entry).
#
# WHY A DIAGNOSTIC GATE (not a pass/fail one): the position/velocity layer was already fixed (Devon's
# smooth-direct-drive, PR #197 first push) and the Sponsor's re-soak STILL hitched — at the ANIMATION layer.
# EditMode/PlayMode can't observe an Animator loop hitch (headless Time.deltaTime≈0 stalls the Animator —
# unity-conventions §Headless), so this launches the BUILT exe windowed with -verifySneak, driving
# SneakVerifyCapture: it holds W (walk baseline) → W+Ctrl (the sneak) and SAMPLES the live layer-0 Animator
# state hash + clip name + normalizedTime + EFFECTIVE playback speed + in-transition + the #186 LocoSpeedMul,
# emitting ~10Hz [SneakTrace] lines and an [SneakVerifyCapture] ANIM-LOOP verdict line that NAMES the candidate.
#
# This gate is a DIAGNOSTIC: it PASSES as long as the exe ran + the trace + frames landed (so the trace artifact
# is always uploaded for the orchestrator/Sponsor to read). It does NOT fail the build on a detected hitch — the
# whole point is to READ the trace, name the confirmed cause, then fix it. The named verdict is echoed to the CI
# log; the [SneakTrace] lines + the verdict + the frames are uploaded as the artifact.
#
# Windowed (NOT -batchmode — ScreenCapture + a live Animator need a real swapchain; spike iter-4 /
# unity-conventions.md). A wall-clock timeout fails a hung launch instead of blocking CI forever.
#
# Usage: verify_sneak_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>] [<extraArg>]
#   captureDir default: ci-out/sneak-caps   logFile default: ci-out/verify-sneak.log
#   extraArg (optional): e.g. -sneakNoFootSync (the candidate-#2 disconfirming control run)
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/sneak-caps}"
LOG_FILE="${3:-ci-out/verify-sneak.log}"
EXTRA_ARG="${4:-}"

if [ -z "$EXE" ]; then
  echo "usage: verify_sneak_gate.sh <FarHorizon.exe> [captureDir] [logFile] [extraArg]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_sneak] FAILED — exe not found: $EXE" >&2
  echo "[verify_sneak]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Clear any stale captures/log so we only read THIS run.
rm -f "$ABS_CAP"/sneak_*.png
rm -f "$LOG_FILE"

echo "[verify_sneak] launching shipped exe windowed (-verifySneak ${EXTRA_ARG}): $EXE"
echo "[verify_sneak]   captureDir=$ABS_CAP logFile=$LOG_FILE"

LAUNCH_TIMEOUT=120
set +e
timeout "${LAUNCH_TIMEOUT}" "$EXE" \
  -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
  -verifySneak ${EXTRA_ARG} -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
exe_rc=$?
set -e
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_sneak] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch)" >&2
  exit 1
fi

# Echo the ground-truth verdict + a sample of the per-frame trace for the CI log (the discriminator output).
if [ -f "$LOG_FILE" ]; then
  echo "[verify_sneak] ---- SneakVerifyCapture summary + ANIM-LOOP verdict ----"
  grep -F "[SneakVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_sneak]   /' || true
  echo "[verify_sneak] ---- [SneakTrace] per-frame Animator dump (first 40 lines) ----"
  grep -F "[SneakTrace]" "$LOG_FILE" | head -n 40 | sed 's/^/[verify_sneak]   /' || true
  trace_lines=$(grep -cF "[SneakTrace]" "$LOG_FILE" || true)
  echo "[verify_sneak] total [SneakTrace] lines captured: ${trace_lines}"
else
  echo "[verify_sneak] WARNING — no log file produced at $LOG_FILE (no trace to read)" >&2
fi

# DIAGNOSTIC gate: pass if the exe ran clean (self-quit 0) AND the trace produced lines. We do NOT block on a
# detected hitch — the verdict is read by a human/orchestrator, then the cause is fixed. A hung/crashed launch
# (no trace) IS a failure (the instrument didn't run).
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_sneak] FAILED — exe exited non-zero (exe_rc=$exe_rc); the instrument did not complete cleanly" >&2
  exit 1
fi
if [ -f "$LOG_FILE" ] && grep -qF "[SneakTrace]" "$LOG_FILE"; then
  echo "[verify_sneak] SNEAK LOOP-HITCH INSTRUMENT RAN — trace + verdict captured (read the ANIM-LOOP verdict above)"
  exit 0
fi
echo "[verify_sneak] FAILED — the instrument produced NO [SneakTrace] lines (the avatar/Animator wire may be missing from Boot.unity)" >&2
exit 1
