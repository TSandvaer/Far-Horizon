#!/usr/bin/env bash
# verify_placement_gate.sh — shipped-build PLACEMENT-OBSTACLE capture gate (ticket 86catr49m).
# Sibling of verify_chop_gate.sh, purpose-built for the crafting-table placement OBJECT-OVERLAP rule: prove in
# the SHIPPED exe that the placement ghost reads GREEN on clear ground, RED over a real seed-42 scatter TREE
# (the navmesh-carve branch), and RED over a registered BOULDER (the PlacementObstacleRegistry branch — boulders
# are collider-free + do NOT carve the navmesh, so ONLY the registry makes them read RED; 86catr49m is the fix
# that wires it).
#
# WHY THIS GATE EXISTS (Tess PASS_WITH_NOTES on PR #302): PlacementVerifyCapture shipped INERT in #302 — never
# authored into Boot.unity + never CI-wired — so (1) the RED-over-tree AC had NO automated evidence path and
# (2) an UNWIRED -verifyPlacement verb NO-OPs AND HANGS the exe (nothing calls Application.Quit). This PR authors
# the harness into Boot.unity (MovementCameraScene.WirePlacementVerifyCapture + a Boot re-bake) AND adds this
# gate, so the placement-obstacle read has a recurring shipped-build proof and a regression turns CI RED.
#
# This launches the BUILT exe -batchmode with -verifyPlacement, driving PlacementVerifyCapture: it EnterPlacement,
# finds a clear spot (GREEN), a reachable scatter tree (RED), and a reachable registered boulder (RED), writes
# placement_clear_green.png + placement_tree_red.png + placement_boulder_red.png and SELF-ASSERTS
# clearOk && treeObstructed && boulderObstructed. It calls Application.Quit(pass ? 0 : 1), so the exe's exit code
# IS the gate verdict — this wrapper launches it + propagates that, with a frame_check.py backstop on the PNGs.
#
# HEADLESS (-batchmode, NO -nographics): PlacementVerifyCapture renders Camera.main into an offscreen RT
# (RenderTextureCapture.CaptureCameraToTexture -> RenderPipeline.SubmitRenderRequest, full URP post) + reads the
# pixels back — a real D3D12 device inits under -batchmode (proven on the shipped player), so NO window is needed
# (mirrors verify_chop_gate.sh's headless launch). A wall-clock timeout fails a hung launch instead of blocking
# CI forever.
#
# Usage: verify_placement_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/placement-caps   logFile default: ci-out/verify-placement.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/placement-caps}"
LOG_FILE="${3:-ci-out/verify-placement.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_placement_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_placement] FAILED — exe not found: $EXE" >&2
  echo "[verify_placement]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Wall-clock cap so a hung launch fails instead of blocking CI forever. The placement sequence teleports the
# player twice (tree + boulder) with short settle waits — no multi-second NavMesh moves — so it is well under
# the chop gate's 300s; 180 gives real margin. `-k 15` hard-KILLs a player that ignores the soft SIGTERM.
LAUNCH_TIMEOUT=180

# launch_once — clear stale artifacts, launch the headless exe under timeout, set exe_rc. Re-clears EVERY
# attempt so a partial first-attempt capture/log can't mask the retry.
launch_once() {
  rm -f "$ABS_CAP"/placement_*.png
  rm -f "$LOG_FILE"
  echo "[verify_placement] launching shipped exe -batchmode (headless RT-readback, -verifyPlacement): $EXE"
  echo "[verify_placement]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -batchmode \
    -verifyPlacement -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero self-assert
# failure is NOT a wedge — never retry it (it would mask a genuine placement-obstacle regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_placement] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_placement] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[PlacementVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_placement]   /' || true
fi

# Check 1 — the exit code IS the gate. PlacementVerifyCapture self-asserts clearOk && treeObstructed &&
# boulderObstructed, else Quit(1). A non-zero exe_rc means the ghost did NOT read GREEN on clear ground, did NOT
# read RED over a tree, did NOT read RED over a registered boulder, OR the harness wiring was missing from
# Boot.unity — exactly the #302-class break this gate exists to catch.
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_placement] FAILED — -verifyPlacement self-assert reported the placement-obstacle read did NOT hold (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the placement frames must be real swapchain content (not black/uniform/magenta).
# Three frames expected (clear_green + tree_red + boulder_red); require >= 2 so one missing frame alone still
# passes but a near-total capture failure does not.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 2
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_placement] PLACEMENT CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_placement] PLACEMENT CAPTURE GATE PASSED — ghost GREEN on clear ground, RED over a tree, RED over a registered boulder, proven in the shipped build"
exit 0
