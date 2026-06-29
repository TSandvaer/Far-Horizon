#!/usr/bin/env bash
# verify_water_gate.sh — shipped-build WATER-ACQUISITION capture gate (ticket 86cafc6vx AC6 — the GET side
# that closes the thirst loop). Sibling of verify_loot_gate.sh / verify_pond_gate.sh / capture_gate.sh,
# purpose-built for the water-acquisition success-test ("standing at the pond shows 'Press E to collect water',
# E loots ONE water into the inventory, and a left-click on the selected water raises the thirst bar — IN THE
# SHIPPED BUILD"). The generic CI -captureGate only shoots the DEFAULT SPAWN frame — the player stands mid-
# field, OUTSIDE the pond's loot range, so the prompt is HIDDEN and NO loot/drink beat is exercised → the one
# surface this ticket delivers had ZERO built-frame evidence.
#
# This launches the BUILT exe WINDOWED with -verifyWater, which drives WaterAcquisitionVerifyCapture: it
# teleports the player IN the pond's loot range (agent.Warp — NOT MoveTo, DEAD under WASD), so LootPrompt
# resolves the pond + paints "Press E to collect water"; then drives RequestLoot (one water into the inventory)
# + select-water + RequestUseClick (the drink); captures water_prompt.png + water_drink.png; and SELF-ASSERTS
# all four beats: (1) the prompt SHOW state (NearestInRange resolves the POND + the label is "collect water"),
# (2) the prompt RENDERED (the IMGUI plate+label painted into the frame), (3) the E-LOOT (exactly one WaterId
# entered the inventory), (4) the DRINK (thirst rose). The component calls Application.Quit(1) on ANY failure
# (or missing wiring from Boot.unity), so the exe's exit code IS the gate verdict — this wrapper launches it
# windowed and propagates that, with a frame_check.py backstop on the PNGs (a real swapchain frame).
#
# Windowed (NOT -batchmode — ScreenCapture needs a real swapchain, spike iter-4 / unity-conventions.md).
# A wall-clock timeout fails a hung launch instead of blocking CI forever (mirrors verify_loot_gate.sh).
#
# Usage: verify_water_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/water-caps   logFile default: ci-out/verify-water.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/water-caps}"
LOG_FILE="${3:-ci-out/verify-water.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_water_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_water] FAILED — exe not found: $EXE" >&2
  echo "[verify_water]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Clear any stale captures so frame_check only sees THIS run's water-acquisition frames.
rm -f "$ABS_CAP"/water_*.png
rm -f "$LOG_FILE"

echo "[verify_water] launching shipped exe windowed (-verifyWater): $EXE"
echo "[verify_water]   captureDir=$ABS_CAP logFile=$LOG_FILE"

# Windowed + small so it never grabs the desktop; -verifyWater drives WaterAcquisitionVerifyCapture;
# -logFile redirects the standalone player's Player.log so the verdict lines are grep-able here. The
# component calls Application.Quit(0/1) when done; cap wall-clock so a hung launch fails.
LAUNCH_TIMEOUT=120
set +e
timeout "${LAUNCH_TIMEOUT}" "$EXE" \
  -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
  -verifyWater -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
exe_rc=$?
set -e
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_water] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[WaterAcquisitionVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_water]   /' || true
fi

# Check 1 — the exit code IS the gate. The component self-asserts the four beats (prompt show + render, the
# E-loot of one water, the left-click drink raising thirst), else Quit(1). A non-zero exe_rc means a beat
# failed (or the wiring was missing from Boot.unity).
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_water] FAILED — -verifyWater self-assert reported the water-acquisition loop did NOT complete (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the water-acquisition frames must be real swapchain content (not black/uniform/
# magenta). Two frames expected (water_prompt + water_drink); require >= 1 so a partial capture still signals.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 1
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_water] WATER-ACQUISITION CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_water] WATER-ACQUISITION CAPTURE GATE PASSED — 'Press E to collect water' shows + E loots water + left-click drink raises thirst in the shipped build"
exit 0
