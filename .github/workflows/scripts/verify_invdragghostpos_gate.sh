#!/usr/bin/env bash
# verify_invdragghostpos_gate.sh — shipped-build INVENTORY DRAG-GHOST POSITION capture gate
# (ticket 86cafhgun — promotes the existing -verifyInvDragGhostPos probe, 86caffw9h, to a required
# CI gate). Sibling of verify_heldbelt_gate.sh / verify_pond_gate.sh, purpose-built for the
# drag-ghost success-test ("the dragged bag item rides the cursor at ANY window size — never
# scale× off toward the bottom-right"). The bug class: InventoryUI.PositionGhostAtScreenPoint
# must apply the PanelScaleMode.ScaleWithScreenSize panel SCALE in the flip-then-ScreenToPanel
# conversion; a scale-less regression is INVISIBLE at exactly 1920x1080 (panel scale ~= 1), so
# THIS GATE MUST RUN AT A NON-1080p RESOLUTION — the explicit -screen-width 2560 -screen-height
# 1440 below is load-bearing, and the probe SELF-GUARDS it: it Quit(3)s ("panel scale ~= 1") if
# the window came up ~1080p, so a silently-clamped window FAILS the gate rather than false-greening.
#
# This launches the BUILT exe WINDOWED with -verifyInvDragGhostPos, driving
# InventoryDragGhostPosVerifyCapture: open the pack with berries, BEGIN a real drag, let the
# production cursor-driven positioning path run, then SELF-ASSERT the ghost's panel-space center
# equals the expected panel point for the live cursor (tol 2 panel px). Quit codes: 0 = pass;
# 2 = Inventory/InventoryUI missing; 3 = panel scale ~= 1 (window not actually 2560x1440 —
# resolution/wiring problem, NOT a code regression); 4 = ghost/panel never laid out; 5 = ghost
# diverged from the cursor (the REAL regression). The exe's exit code IS the gate verdict;
# frame_check.py is a swapchain-content backstop on inv_drag_ghost_pos.png (written on the pass
# and the rc-5 divergence path; the FATAL 2/3/4 paths write no PNG and fail on exit code alone).
#
# Windowed (NOT -batchmode — ScreenCapture needs a real swapchain, spike iter-4 /
# unity-conventions.md). WEDGE HARDENING (86cafzaeb; mirrors capture_gate.sh /
# verify_pond_gate.sh): LAUNCH_TIMEOUT 300, `timeout -k 15` SIGKILLs a SIGTERM-ignoring hung
# player, and a single rc==124-only retry re-launches ONCE on a first-frame present-loop wedge
# before declaring failure (a real non-zero gate failure is NEVER retried).
#
# Usage: verify_invdragghostpos_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/invdragghost-caps   logFile default: ci-out/verify-invdragghostpos.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/invdragghost-caps}"
LOG_FILE="${3:-ci-out/verify-invdragghostpos.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_invdragghostpos_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_invdragghostpos] FAILED — exe not found: $EXE" >&2
  echo "[verify_invdragghostpos]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
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
  rm -f "$ABS_CAP"/inv_drag_ghost_pos*.png
  rm -f "$LOG_FILE"
  echo "[verify_invdragghostpos] launching shipped exe windowed 2560x1440 (-verifyInvDragGhostPos): $EXE"
  echo "[verify_invdragghostpos]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  # 2560x1440 is LOAD-BEARING (see header): the scale bug is invisible at 1080p and the probe
  # Quit(3)s if the panel scale reads ~1. -logFile redirects the player log so the
  # cursor/ghost/panelScale verdict lines are grep-able here.
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 2560 -screen-height 1440 \
    -verifyInvDragGhostPos -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero
# self-assert failure is NOT a wedge — never retry it (it would mask a genuine ghost-position regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_invdragghostpos] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_invdragghostpos] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[inv-drag-ghost]" "$LOG_FILE" | sed 's/^/[verify_invdragghostpos]   /' || true
fi

# Check 1 — the exit code IS the gate. Decode the probe's quit codes so a red is triageable at a
# glance: rc 3 is a RESOLUTION/WIRING problem (the window did not come up non-1080p — fix the gate
# environment), rc 5 is the REAL drag-ghost regression, 2/4 are missing-wiring FATALs.
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  case "$exe_rc" in
    3) echo "[verify_invdragghostpos] FAILED — panel scale ~= 1: the window did NOT come up at a non-1080p size (requested 2560x1440); the probe is meaningless at ~1080p. Gate-environment problem, NOT a code regression — check the runner's display/window clamping" >&2 ;;
    5) echo "[verify_invdragghostpos] FAILED — the drag ghost DIVERGED from the cursor (> 2 panel px): the scale-aware PositionGhostAtScreenPoint fix is broken (the 86caffw9h regression)" >&2 ;;
    *) echo "[verify_invdragghostpos] FAILED — -verifyInvDragGhostPos self-assert failed (exe_rc=$exe_rc; 2=Inventory/UI missing, 4=ghost/panel never laid out — wiring FATALs)" >&2 ;;
  esac
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the ghost frame must be real swapchain content (not black/uniform/
# magenta). One frame expected (inv_drag_ghost_pos.png).
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 1
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_invdragghostpos] INV-DRAG-GHOST CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_invdragghostpos] INV-DRAG-GHOST CAPTURE GATE PASSED — the dragged item rides the cursor at 2560x1440 (panel scale != 1) in the shipped build"
exit 0
