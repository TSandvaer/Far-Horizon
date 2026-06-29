#!/usr/bin/env bash
# verify_sky_gate.sh — shipped-build SKY-FACING (SUN-DISK) capture gate (ticket 86cabc743 — Erik
# low-poly-sky research POC). Sibling of verify_pond_gate.sh / capture_gate.sh, purpose-built for the
# sun-disk POC's success-test: the warm-gold SUN DISK renders in the SHIPPED exe (the OPEN QUESTION —
# does the URP _MainLightPosition bind in the Background/skybox pass in the IL2CPP build?) AND the
# bright near-white cyan clouds still CONTRAST against the cheerful blue sky (Erik's 2nd open Q — the S2
# contrast fix must survive the build). The generic -captureGate (step 7) only shoots the DEFAULT SPAWN
# frame looking DOWN over the shoulder — it never tilts up to the sky, so the sun disk + cloud-vs-sky
# contrast have ZERO built-frame evidence without this gate.
#
# This launches the BUILT exe WINDOWED with -verifySky, which drives SkyVerifyCapture: it parks a
# dedicated sky camera (the gameplay OrbitCamera clamps pitch <=70 and frames the player from above — it
# cannot tilt up to the Sun at elevation ~48deg), aims it at the LIVE Sun direction (sky_sun.png) and up
# into the cloud band (sky_clouds.png), and SELF-ASSERTS two percepts: (1) SUN VISIBLE — the centre disk
# reads BRIGHTER + WARMER (R>=B) than the blue sky surround, and (2) CLOUD-VS-SKY CONTRAST — a non-trivial
# fraction of frame pixels read well above the sky median luma (the bright clouds). The component calls
# Application.Quit(1) if the sun is not visible OR the cloud contrast collapsed (or the Sun is missing),
# so the exe's exit code IS the gate verdict; frame_check.py is a swapchain-content backstop on the PNGs.
#
# Windowed (NOT -batchmode — ScreenCapture + the post stack need a real swapchain, spike iter-4 /
# unity-conventions.md). A wall-clock timeout fails a hung launch instead of blocking CI forever.
#
# Usage: verify_sky_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/sky-caps   logFile default: ci-out/verify-sky.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/sky-caps}"
LOG_FILE="${3:-ci-out/verify-sky.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_sky_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_sky] FAILED — exe not found: $EXE" >&2
  echo "[verify_sky]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Clear any stale captures so frame_check only sees THIS run's sky frames.
rm -f "$ABS_CAP"/sky_*.png
rm -f "$LOG_FILE"

echo "[verify_sky] launching shipped exe windowed (-verifySky): $EXE"
echo "[verify_sky]   captureDir=$ABS_CAP logFile=$LOG_FILE"

# Windowed + small so it never grabs the desktop; -verifySky drives SkyVerifyCapture; -logFile redirects
# the standalone player's Player.log so the SUN + CONTRAST verdict lines are grep-able here. The component
# calls Application.Quit(0/1) when done; cap wall-clock so a hung launch fails.
LAUNCH_TIMEOUT=120
set +e
timeout "${LAUNCH_TIMEOUT}" "$EXE" \
  -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
  -verifySky -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
exe_rc=$?
set -e
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_sky] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[SkyVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_sky]   /' || true
fi

# Check 1 — the exit code IS the gate. The component self-asserts SUN VISIBLE (warmer+brighter than the
# sky surround) AND cloud-vs-sky CONTRAST, else Quit(1). A non-zero exe_rc means the sun disk did NOT
# render / read warm-bright in the shipped exe OR the clouds washed into the sky (or the Sun is missing).
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_sky] FAILED — -verifySky self-assert reported the sun disk NOT visible OR cloud-vs-sky contrast collapsed (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the sky frames must be real swapchain content (not black/uniform/magenta).
# Two frames expected (sky_sun + sky_clouds); require >= 1 so a partial capture still gives signal.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 1
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_sky] SKY CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_sky] SKY CAPTURE GATE PASSED — the warm-gold sun disk renders + the clouds contrast against the blue sky in the shipped build"
exit 0
