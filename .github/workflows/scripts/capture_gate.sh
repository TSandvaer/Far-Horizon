#!/usr/bin/env bash
# capture_gate.sh — shipped-build capture gate runner (ticket 86ca86g7k).
#
# Launches the BUILT Windows exe WINDOWED (not -batchmode — ScreenCapture needs a
# real swapchain, spike iter-4 / unity-conventions.md), drives the standard
# CaptureGate component to render N frames with the HUD build-stamp visible, then
# hands the captured PNGs to frame_check.py which FAILS on black/empty/uniform/
# magenta frames. This is the editor-vs-runtime backstop the testing bar requires:
# editor evidence is necessary, never sufficient — only a real frame from the
# shipped exe proves it renders.
#
# Use it two ways:
#   * Local (authors, for the Self-Test Report shipped-build evidence):
#       .github/workflows/scripts/capture_gate.sh Build/Windows/FarHorizon.exe
#   * CI (self-hosted Windows runner step in ci.yml), after the build step.
#
# Usage: capture_gate.sh <FarHorizon.exe> [<captureDir>] [<frames>]
#   captureDir default: ci-out/caps   frames default: 4
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/caps}"
FRAMES="${3:-4}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: capture_gate.sh <FarHorizon.exe> [captureDir] [frames]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[capture_gate] FAILED — exe not found: $EXE" >&2
  echo "[capture_gate]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"

# Clear any stale captures so frame_check only sees THIS run's frames (a leftover
# good frame must never mask a now-black one).
rm -f "$ABS_CAP"/capture_*.png

echo "[capture_gate] launching shipped exe windowed: $EXE"
echo "[capture_gate]   frames=$FRAMES captureDir=$ABS_CAP"

# Windowed + small so it never grabs the desktop; -captureGate drives CaptureGate;
# the component calls Application.Quit() when done. Cap wall-clock so a hung launch
# (the exe never renders / never quits) fails instead of blocking CI forever.
LAUNCH_TIMEOUT=120
set +e
timeout "${LAUNCH_TIMEOUT}" "$EXE" \
  -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
  -captureGate -captureFrames "$FRAMES" -captureDir "$ABS_CAP"
rc=$?
set -e
if [ "$rc" -eq 124 ]; then
  echo "[capture_gate] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s; inspecting whatever it captured" >&2
fi

# Authoritative pass/fail: the OUT-OF-ENGINE frame inspection. Require at least 1
# frame (zero frames = the exe never rendered = a failure, same silent-killer
# shape as the test-result total>0 rule).
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 1
