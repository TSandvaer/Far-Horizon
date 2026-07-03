#!/usr/bin/env bash
# verify_heldbelt_gate.sh — shipped-build HELD-BELT-SELECTION capture gate (ticket 86cahx2p5,
# the CI wiring follow-up to PR #232 / 86cahngdg — the soak-224 crossed-visual fix). Sibling of
# verify_pond_gate.sh / verify_chop_gate.sh, purpose-built for the belt-selection success-test
# ("the held visual follows the SELECTED belt weapon in the SHIPPED build": axe selected -> the
# AXE mesh in hand; spear selected -> the SPEAR mesh; empty selected -> hidden; back to axe ->
# the mesh RETURNS, never the stale spear). The generic -captureGate only shoots the default
# spawn frame (no weapons acquired, no belt driven), so the whole selection table had ZERO
# built-frame evidence — #232's fix was proven by PlayMode tests + a manual soak only.
#
# This launches the BUILT exe WINDOWED with -verifyHeldBelt, which drives
# AxeVerifyCapture.RunHeldBeltVerification: acquire axe + spear via the REAL Inventory pickup
# seams, then drive the belt-selection table end to end (STATE-1 axe / STATE-2 spear / STATE-3
# empty / STATE-4 axe re-selected) and SELF-ASSERT each state's renderer visibility + held mesh
# identity (vertex-count swap proof included). The component calls Application.Quit(1) on ANY
# failed state (or missing HeroAxe/cycle/Inventory wiring from Boot.unity), so the exe's exit
# code IS the gate verdict — this wrapper launches it windowed and propagates that, with a
# frame_check.py backstop on the PNGs (real swapchain frames, not black/uniform/magenta).
# Frames written: held_axe_gameplay/close, held_spear_gameplay/close, held_empty_gameplay (5).
# NOTE: STATE-4 (axe re-selected) is asserted via the exit code + the GATE-PASS log line but
# writes NO PNG today — emitting one needs an AxeVerifyCapture.cs (runtime) change, tracked as
# remaining scope on 86cahx2p5; do not "fix" it from this wrapper.
#
# Windowed (NOT -batchmode — ScreenCapture needs a real swapchain, spike iter-4 /
# unity-conventions.md). WEDGE HARDENING (86cafzaeb; mirrors capture_gate.sh /
# verify_pond_gate.sh): LAUNCH_TIMEOUT 300, `timeout -k 15` SIGKILLs a SIGTERM-ignoring hung
# player, and a single rc==124-only retry re-launches ONCE on a first-frame present-loop wedge
# before declaring failure (a real non-zero gate failure is NEVER retried).
#
# Usage: verify_heldbelt_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/heldbelt-caps   logFile default: ci-out/verify-heldbelt.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/heldbelt-caps}"
LOG_FILE="${3:-ci-out/verify-heldbelt.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_heldbelt_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_heldbelt] FAILED — exe not found: $EXE" >&2
  echo "[verify_heldbelt]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Wall-clock cap so a hung launch fails instead of blocking CI forever. 300 gives real margin over
# the longest healthy launch. `-k 15` hard-KILLs (SIGKILL) a player that ignores the soft SIGTERM
# 15s later, so a wedged D3D12 present-loop process can't linger into the retry / the next gate.
LAUNCH_TIMEOUT=300

# launch_once — clear stale artifacts, launch the windowed exe under timeout, set exe_rc. Re-clears
# EVERY attempt so a partial first-attempt capture/log can't mask the retry.
launch_once() {
  rm -f "$ABS_CAP"/held_*.png
  rm -f "$LOG_FILE"
  echo "[verify_heldbelt] launching shipped exe windowed (-verifyHeldBelt): $EXE"
  echo "[verify_heldbelt]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  # Windowed + small so it never grabs the desktop; -verifyHeldBelt drives the AxeVerifyCapture
  # held-belt path; -logFile redirects the player log so the STATE-1..4 verdict lines are grep-able.
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -verifyHeldBelt -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero
# self-assert failure is NOT a wedge — never retry it (it would mask a genuine selection regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_heldbelt] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_heldbelt] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[AxeVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_heldbelt]   /' || true
fi

# Check 1 — the exit code IS the gate. The component self-asserts all four selection states (axe
# shown+right mesh, spear shown+right mesh+verts differ, empty hidden, axe RETURNS), else Quit(1).
# A non-zero exe_rc means a selection state failed (or the wiring was missing from Boot.unity).
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_heldbelt] FAILED — -verifyHeldBelt self-assert reported the held visual does NOT follow the belt selection (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the held-weapon frames must be real swapchain content (not black/
# uniform/magenta). Five frames expected (axe gameplay+close, spear gameplay+close, empty
# gameplay); require >= 3 so a partial capture still gives signal but a near-total capture
# failure does not pass.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 3
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_heldbelt] HELD-BELT CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_heldbelt] HELD-BELT CAPTURE GATE PASSED — the held visual follows the belt selection (axe/spear/empty/axe-returns) in the shipped build"
exit 0
