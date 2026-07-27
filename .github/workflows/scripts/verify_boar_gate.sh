#!/usr/bin/env bash
# verify_boar_gate.sh — shipped-build WILD-BOAR (find → aggro → windup → CHARGE → gore → spear-kill →
# despawn, plus the spear-out-damages-axe MATCHUP measurement) capture gate (ticket 86cavg2k1 NIT 2 — the
# CI-wiring follow-up from Devon's PR #332 peer-review, review 4754125364: "`git grep verifyBoar --
# .github/workflows/ci.yml` = 0 today; the gate is baked into the committed Boot scene
# (BoarSceneTests.BootScene_CarriesBoarVerifyCapture_Serialized) but never runs in CI, so boar-render
# regressions are ungated"). Direct sibling of verify_weaponset_gate.sh (the WINDOWED launch shape) and of
# verify_mine_gate.sh / verify_boulder_gate.sh (the self-asserting-component + frame-backstop shape).
#
# WHY THIS GATE EXISTS: -verifyBoar (BoarVerifyCapture.cs) already drives the REAL boar loop end-to-end on
# the shipped exe — it WALKS the player through the production WasdMovement override seam (nothing
# teleports), so aggro/windup/charge fire exactly as they do for the Sponsor — and it PASSED the 2026-07-22
# boar soak. But it was NEVER a CI gate (grep -rn verifyBoar .github/workflows/ → zero hits before this), so
# a real break in the boar loop (or in its render: the body rig, the BoarMat, the charge pose) would slip
# straight through GREEN CI exactly like the -verifyMine / -verifyBoulder gap class. The generic
# -captureGate (step 7) only shoots the DEFAULT SPAWN frame — the player stands mid-field, the boar is not
# aggroed and may not even be in frame, so nothing about the boar loop is exercised. This gate closes that
# hole: a boar-loop OR boar-render regression turns CI RED.
#
# This launches the BUILT exe WINDOWED with -verifyBoar, which drives BoarVerifyCapture: settle the agents,
# yaw the REAL OrbitCamera at the wandering boar and assert it is IN FRAME (AC5), shoot a dedicated LOW
# SIDE-PROFILE frame (lowpoly-quality §0 silhouette gate — a boar is a 4-legged animal standing ON the
# ground with a humped back + snout + tusks; the stance is obvious side-on, invisible top-down), WALK the
# real player in until aggro → windup → charge → gore land (asserting the gore HP delta equals the tier's
# expected value through the shared seam), MEASURE one axe hit vs one spear hit on the live boar's
# resistance (the spear MUST out-damage the higher-base axe — the emergent matchup, not a table), then
# spear-kill the boar and wait for the despawn. It calls Application.Quit(pass ? 0 : 1), so the exe's exit
# code IS the gate verdict — this wrapper just launches it and propagates that, with a frame_check.py
# backstop on the PNGs (a real swapchain frame, not black/uniform/magenta).
#
# WINDOWED (-screen-fullscreen 0), NOT -batchmode: BoarVerifyCapture uses ScreenCapture.CaptureScreenshot +
# WaitForEndOfFrame, both DEAD under -batchmode (no swapchain to read back + no end-of-frame render pass to
# resume the coroutine — spike iter-4 / unity-conventions.md §Headless). Held-mesh + a live Animator + a
# world-camera judge, so this gate stays on the pinned runner-1 windowed capture lane with the other
# windowed gates; it CANNOT convert to the 86cag93zb headless RT-readback path without a component rewrite.
#
# WEDGE HARDENING (86cafzaeb; mirrors capture_gate.sh / verify_weaponset_gate.sh / verify_boulder_gate.sh):
# `timeout -k 15` SIGKILLs a SIGTERM-ignoring hung player, and a single rc==124-only retry re-launches ONCE
# on a first-frame present-loop wedge before declaring failure (a real non-zero gate failure is NEVER
# retried — that would mask a genuine boar-loop regression). LAUNCH_TIMEOUT is 360 rather than the siblings'
# 300: the boar coroutine is the LONGEST of the verify family — a ~30-frame settle + a 25s WALK-to-aggro
# deadline + up to 8 spear hits spaced 0.4s + a despawnSeconds+2s despawn wait + 8 captures — so 300 leaves
# too little headroom over a healthy-but-slow run to distinguish a wedge from normal completion.
#
# Usage: verify_boar_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/boar-caps   logFile default: ci-out/verify-boar.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/boar-caps}"
LOG_FILE="${3:-ci-out/verify-boar.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_boar_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_boar] FAILED — exe not found: $EXE" >&2
  echo "[verify_boar]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Wall-clock cap so a hung launch fails instead of blocking CI forever. 360 (vs the siblings' 300) gives
# real margin over the LONGEST healthy boar run — see the header note. `-k 15` hard-KILLs (SIGKILL) a player
# that ignores the soft SIGTERM 15s later so a wedged D3D12 present-loop process can't linger into the
# retry / the next gate.
LAUNCH_TIMEOUT=360

# launch_once — clear stale artifacts, launch the windowed exe under timeout, set exe_rc. Re-clears EVERY
# attempt so a partial first-attempt capture/log can't mask the retry.
launch_once() {
  rm -f "$ABS_CAP"/boar_*.png
  rm -f "$LOG_FILE"
  echo "[verify_boar] launching shipped exe windowed (-verifyBoar): $EXE"
  echo "[verify_boar]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  # Windowed + small so it never grabs the desktop; -verifyBoar drives BoarVerifyCapture (which also sets
  # Application.runInBackground for its own launch — an unfocused window would otherwise pause the player
  # mid-coroutine and hang the gate, the SnakeVerifyCapture lesson). -logFile redirects the standalone
  # player's Player.log so the verdict lines are grep-able here. The component calls Application.Quit(0/1).
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -verifyBoar -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero
# self-assert failure is NOT a wedge — never retry it (it would mask a genuine boar-loop regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_boar] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_boar] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log — including the MATCHUP measurement and
# the final `GATE PASS/FAIL: inFrame=… aggro=… windup=… charge=… gore=… spearBeatsAxe=… died=… despawned=…`
# line, so a red names WHICH half of the loop broke without downloading the artifact.
if [ -f "$LOG_FILE" ]; then
  grep -F "[BoarVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_boar]   /' || true
fi

# Check 1 — the exit code IS the gate. BoarVerifyCapture self-asserts the FULL loop (findable → aggro →
# windup → charge → gore of the EXPECTED tier amount → spear-out-damages-axe → death → despawn), else
# Quit(1). A non-zero exe_rc means the boar never aggroed/charged, the gore landed the wrong amount, the
# spear stopped beating the axe (the matchup collapsed), the boar never died/despawned, OR the wiring was
# missing from Boot.unity — exactly the boar-loop regressions this gate exists to catch.
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_boar] FAILED — -verifyBoar self-assert reported the find → aggro → windup → charge → gore → spear-kill → despawn loop did NOT complete (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the boar frames must be real swapchain content (not black/uniform/magenta — the
# last would be a shader-strip regression on the inline BoarMat). A PASSING run always writes at least the
# four UNCONDITIONAL frames (boar_findable, boar_side_profile, boar_death, boar_despawned); the aggro /
# windup / charge / spear_kill frames are state-conditional, so require >= 4 — enough that a near-total
# capture failure fails here, without making a state-conditional frame load-bearing twice (the exit code
# already gates those states).
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 4
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_boar] BOAR CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_boar] BOAR CAPTURE GATE PASSED — find → aggro → windup → CHARGE → gore → spear-kill → despawn, and the spear out-damages the axe on the pierce-weak boar, proven end-to-end in the shipped build"
exit 0
