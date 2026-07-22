#!/usr/bin/env bash
# verify_pond_gate.sh — shipped-build FRESHWATER-POND capture gate (ticket 86caamkv7 follow-up,
# wired in 86caamkxv). Sibling of verify_settings_gate.sh / capture_gate.sh, purpose-built for the
# pond's success-test ("the freshwater pond renders FRESH-BLUE + visible in the SHIPPED build" — the
# thirst drink SOURCE; #124 verified the need model + drink-action headlessly but the pond's shipped
# render was never gated, so the generic -captureGate (spawn-frame sanity only) never framed it).
#
# This launches the BUILT exe WINDOWED with -verifyPond, which drives FreshwaterPondVerifyCapture: it
# frames the GAMEPLAY-pitch orbit camera onto the pond at three yaws (pond_a/b/c.png), THEN shoots a 4th
# EYE-LEVEL SIDE-PROFILE frame looking horizontally across the pond (pond_side.png — ticket 86cadj4g7
# #130 / lowpoly-quality.md §0: up-vs-down is invisible from the down-angle frames, obvious side-on). It
# SELF-ASSERTS two PERCEPTS: (1) FRESH-BLUE — centre B > G by a clear margin (the freshwater tell; the
# sea's teal never passes) + VISIBLE (the disc differs from the sky/grass surround), and (2) SIDE-PROFILE
# SUNK — the water band sits BELOW the surrounding-grass line (a mound bulges it ABOVE; the #130 defect).
# The component calls Application.Quit(1) if the pond is NOT fresh-blue/visible OR reads as a MOUND from
# the side profile (or is missing from Boot.unity), so the exe's exit code IS the gate verdict — this
# wrapper just launches it windowed and propagates that, with a frame_check.py backstop on the PNGs (a
# real swapchain frame, not black/uniform/magenta).
#
# Windowed (NOT -batchmode — ScreenCapture needs a real swapchain, spike iter-4 / unity-conventions.md).
# A wall-clock timeout fails a hung launch instead of blocking CI forever (mirrors capture_gate.sh).
# WEDGE HARDENING (capture-flake investigation wf_b92193a7-ba9; mirrors capture_gate.sh):
# LAUNCH_TIMEOUT is 300s (was 120 — 120 had no margin), `timeout -k 15` SIGKILLs a SIGTERM-ignoring
# hung player, and a single rc==124-only retry re-launches ONCE on a first-frame present-loop wedge
# before declaring failure (a real non-zero gate failure is NEVER retried).
#
# Usage: verify_pond_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/pond-caps   logFile default: ci-out/verify-pond.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/pond-caps}"
LOG_FILE="${3:-ci-out/verify-pond.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_pond_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_pond] FAILED — exe not found: $EXE" >&2
  echo "[verify_pond]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Wall-clock cap so a hung launch fails instead of blocking CI forever. 300 gives real margin over the
# longest healthy launch. `-k 15` hard-KILLs (SIGKILL) a player that ignores the soft SIGTERM 15s later,
# so a wedged D3D12 present-loop process can't linger and hold a swapchain into the retry / the next gate.
LAUNCH_TIMEOUT=300

# launch_once — clear stale artifacts, launch the windowed exe under timeout, set exe_rc. Re-clears EVERY
# attempt so a partial first-attempt capture/log can't mask the retry.
launch_once() {
  rm -f "$ABS_CAP"/pond_*.png
  rm -f "$LOG_FILE"
  echo "[verify_pond] launching shipped exe windowed (-verifyPond): $EXE"
  echo "[verify_pond]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  # Windowed + small so it never grabs the desktop; -verifyPond drives FreshwaterPondVerifyCapture;
  # -logFile redirects the standalone player's Player.log so the FRESH-BLUE verdict line is grep-able.
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -verifyPond -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge wf_b92193a7-ba9). A real
# non-zero self-assert failure is NOT a wedge — never retry it (it would mask a genuine pond-render failure).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_pond] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_pond] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[FreshwaterPondVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_pond]   /' || true
fi

# Check 1 — the exit code IS the gate. The component self-asserts FRESH-BLUE + visible AND the eye-level
# SIDE-PROFILE SUNK read (water below the grass line), else Quit(1). A non-zero exe_rc means the pond did
# NOT read fresh-blue/visible OR reads as a MOUND from the side profile (the #130 defect), or was missing.
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_pond] FAILED — -verifyPond self-assert reported the pond is NOT fresh-blue/visible OR reads as a MOUND from the side profile (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the pond frames must be real swapchain content (not black/uniform/magenta).
# Four frames expected now (pond_a/b/c + pond_side); require >= 1 so a partial capture still gives signal.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 1
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_pond] POND CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_pond] POND CAPTURE GATE PASSED — the freshwater pond reads fresh-blue + visible AND sits SUNK below the grass line (eye-level side profile) in the shipped build"
exit 0
