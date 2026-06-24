#!/usr/bin/env bash
# verify_pond_gate.sh — shipped-build FRESHWATER-POND capture gate (ticket 86caamkv7 follow-up,
# wired in 86caamkxv). Sibling of verify_settings_gate.sh / capture_gate.sh, purpose-built for the
# pond's success-test ("the freshwater pond renders FRESH-BLUE + visible in the SHIPPED build" — the
# thirst drink SOURCE; #124 verified the need model + drink-action headlessly but the pond's shipped
# render was never gated, so the generic -captureGate (spawn-frame sanity only) never framed it).
#
# This launches the BUILT exe WINDOWED with -verifyPond, which drives FreshwaterPondVerifyCapture: it
# frames the GAMEPLAY-pitch orbit camera onto the pond at three yaws, writes pond_a/b/c.png, and
# SELF-ASSERTS the perceptual FRESH-BLUE read (centre B > G by a clear margin — the freshwater tell;
# the sea's teal never passes) + VISIBLE (the disc differs from the sky/grass surround). The component
# calls Application.Quit(1) if the pond is NOT fresh-blue/visible (or is missing from Boot.unity), so
# the exe's exit code IS the gate verdict — this wrapper just launches it windowed and propagates that,
# with a frame_check.py backstop on the PNGs (a real swapchain frame, not black/uniform/magenta).
#
# Windowed (NOT -batchmode — ScreenCapture needs a real swapchain, spike iter-4 / unity-conventions.md).
# A wall-clock timeout fails a hung launch instead of blocking CI forever (mirrors capture_gate.sh).
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

# Clear any stale captures so frame_check only sees THIS run's pond frames.
rm -f "$ABS_CAP"/pond_*.png
rm -f "$LOG_FILE"

echo "[verify_pond] launching shipped exe windowed (-verifyPond): $EXE"
echo "[verify_pond]   captureDir=$ABS_CAP logFile=$LOG_FILE"

# Windowed + small so it never grabs the desktop; -verifyPond drives FreshwaterPondVerifyCapture;
# -logFile redirects the standalone player's Player.log so the FRESH-BLUE verdict line is grep-able
# here. The component calls Application.Quit(0/1) when done; cap wall-clock so a hung launch fails.
LAUNCH_TIMEOUT=120
set +e
timeout "${LAUNCH_TIMEOUT}" "$EXE" \
  -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
  -verifyPond -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
exe_rc=$?
set -e
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_pond] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[FreshwaterPondVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_pond]   /' || true
fi

# Check 1 — the exit code IS the gate (the component self-asserts FRESH-BLUE + visible, else Quit(1)).
# A non-zero exe_rc means the pond did NOT read fresh-blue/visible in the shipped frame, or was missing.
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_pond] FAILED — -verifyPond self-assert reported the pond is NOT fresh-blue/visible (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the pond frames must be real swapchain content (not black/uniform/magenta).
# Three frames expected (pond_a/b/c); require >= 1 so a partial capture still gives signal.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 1
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_pond] POND CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_pond] POND CAPTURE GATE PASSED — the freshwater pond reads fresh-blue + visible in the shipped build"
exit 0
