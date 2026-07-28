#!/usr/bin/env bash
# verify_weaponfind_gate.sh — shipped-build FIND-IN-WORLD WEAPON capture gate (ticket 86cah7y5b AC6).
# Sibling of verify_loot_gate.sh / verify_water_gate.sh / verify_mine_gate.sh, purpose-built for the
# find-in-world route's success test: "the resting weapon is visible in the world at gameplay framing, and
# the SAME weapon is visible IN THE CASTAWAY'S HAND after the loot."
#
# WHY THIS EXISTS: the generic CI -captureGate only shoots the DEFAULT SPAWN frame, where the player stands
# mid-field far from the seeded find — so BOTH halves of the deliverable (the resting weapon + the weapon
# in-hand) would have ZERO built-frame evidence. Worse, the specific class this guards has already shipped
# TWICE: a PlayMode `renderer.enabled` assert let an INVISIBLE-IN-HAND weapon through on soak-3 and soak-4
# (86cav8y74). So the component asserts the in-hand state three independent ways (seat renderer enabled AND
# the belt->held sync landed on the IRON SWORD family index AND the seat's world bounds are real + inside
# the camera frustum) instead of trusting one boolean.
#
# It also shoots a SIDE-PROFILE frame. The find is a physical thing whose up-vs-down read matters — an iron
# sword driven POINT-DOWN into a stump. Blade-down-vs-blade-up is invisible top-down and at player-eye and
# obvious side-on (lowpoly-quality.md §0; the PR #130 pond->mound lesson), so weaponfind_side.png exists for
# a human to eyeball, paired with the component's geometric "blade stays below the stump top even at peak
# bob" assert.
#
# This launches the BUILT exe WINDOWED with -verifyWeaponFind, which drives WeaponFindVerifyCapture; the
# component calls Application.Quit(1) if ANY self-assert fails (or the wiring is missing from Boot.unity), so
# the exe's exit code IS the gate verdict. This wrapper launches it windowed, propagates that code, and adds
# a frame_check.py backstop on the PNGs (real swapchain frames, not black/uniform/magenta).
#
# Windowed (NOT -batchmode — ScreenCapture needs a real swapchain, spike iter-4 / unity-conventions.md).
# A wall-clock timeout fails a hung launch instead of blocking forever (mirrors verify_loot_gate.sh).
#
# Usage: verify_weaponfind_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/weaponfind-caps   logFile default: ci-out/verify-weaponfind.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/weaponfind-caps}"
LOG_FILE="${3:-ci-out/verify-weaponfind.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_weaponfind_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_weaponfind] FAILED — exe not found: $EXE" >&2
  echo "[verify_weaponfind]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Wall-clock cap so a hung launch fails instead of blocking forever; `-k 15` hard-KILLs a player that ignores
# the soft SIGTERM, so a wedged D3D12 present-loop process can't linger into the retry / the next gate.
LAUNCH_TIMEOUT=300

launch_once() {
  rm -f "$ABS_CAP"/weaponfind_*.png
  rm -f "$LOG_FILE"
  echo "[verify_weaponfind] launching shipped exe windowed (-verifyWeaponFind): $EXE"
  echo "[verify_weaponfind]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -verifyWeaponFind -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero self-assert
# failure is NOT a wedge — never retry it (that would mask a genuine regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_weaponfind] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_weaponfind] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[WeaponFindVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_weaponfind]   /' || true
fi

# Check 1 — the exit code IS the gate (the component self-asserts rest+embed, prompt, loot, second-press-no-op,
# and the three-way in-hand proof, else Quit(1)).
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_weaponfind] FAILED — -verifyWeaponFind self-assert reported the find route did NOT complete (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: real swapchain content, not black/uniform/magenta. Three frames expected
# (rest + side profile + in-hand); require >= 2 so a partial capture still gives usable signal.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 2
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_weaponfind] WEAPON-FIND CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_weaponfind] WEAPON-FIND CAPTURE GATE PASSED — the resting sword renders in the world, E loots it once, and it renders IN-HAND in the shipped build"
echo "[verify_weaponfind]   NOTE: weaponfind_side.png is the SIDE-PROFILE silhouette — a human must eyeball blade-DOWN / grip-UP before review (lowpoly-quality.md §0)"
exit 0
