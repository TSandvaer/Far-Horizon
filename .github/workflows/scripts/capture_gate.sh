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
# WEDGE HARDENING (capture-flake investigation wf_b92193a7-ba9): the windowed launch
# intermittently HANGS at the first-frame present loop (D3D12 init → 1 frame runs →
# present wedges) on the self-hosted runner — non-deterministic, a different gate each
# run, and every gate also PASSES on >=1 attempt. So:
#   * LAUNCH_TIMEOUT is 300s (was 120 — a passing tail already reached ~44s and a
#     settings launch rendered 3 valid frames yet still blew 120 on self-quit; 120
#     had no margin).
#   * `timeout -k 15` SIGKILLs a SIGTERM-ignoring hung player 15s after the soft TERM,
#     so a wedged process can't linger into the retry / the next gate.
#   * ONE rc==124-only retry per gate (mirror of bootstrap_with_retry.sh's transient
#     retry): a single timeout-hang re-launches ONCE before declaring failure; a real
#     non-zero gate failure (NOT 124) is NEVER retried.
#   * -logFile ci-out/capture.log redirects the player log so a future wedge is
#     diagnosable (verify_pond/verify_loot already redirect; this one did not until now).
#
# Usage: capture_gate.sh <FarHorizon.exe> [<captureDir>] [<frames>] [<logFile>]
#   captureDir default: ci-out/caps   frames default: 4   logFile default: ci-out/capture.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/caps}"
FRAMES="${3:-4}"
LOG_FILE="${4:-ci-out/capture.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: capture_gate.sh <FarHorizon.exe> [captureDir] [frames] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[capture_gate] FAILED — exe not found: $EXE" >&2
  echo "[capture_gate]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Wall-clock cap so a hung launch (the exe never renders / never quits) fails instead
# of blocking CI forever. 300 gives real margin over the longest healthy launch.
# `-k 15` hard-KILLs (SIGKILL) a player that ignores the soft SIGTERM 15s later, so a
# wedged D3D12 present-loop process can't linger and hold a swapchain into the retry.
LAUNCH_TIMEOUT=300

# launch_once — clear stale artifacts, launch the windowed exe under timeout, return
# its rc. Re-clears EVERY attempt so a partial first-attempt capture/log can't mask
# the retry (a leftover good frame must never satisfy frame_check on a re-hang).
launch_once() {
  # Clear any stale captures so frame_check only sees THIS attempt's frames.
  rm -f "$ABS_CAP"/capture_*.png
  rm -f "$LOG_FILE"
  echo "[capture_gate] launching shipped exe -batchmode (headless RT-readback): $EXE"
  echo "[capture_gate]   frames=$FRAMES captureDir=$ABS_CAP logFile=$LOG_FILE"
  # HEADLESS (86cag93zb): -batchmode with NO -nographics (a real D3D12 device inits) and NO window.
  # CaptureGate now renders Camera.main into an offscreen RenderTexture (SubmitRenderRequest) instead of
  # the backbuffer ScreenCapture, so it works window-less — removing the windowed-swapchain contention
  # that pinned the capture job to one runner. The component calls Application.Quit() when done.
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -batchmode \
    -captureGate -captureFrames "$FRAMES" -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the present-loop wedge). A real non-zero
# gate failure is NOT a wedge — do not retry it (that would just waste a runner cycle
# and could mask a genuine render failure).
if [ "$rc" -eq 124 ]; then
  echo "[capture_gate] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the first-frame present-loop wedge wf_b92193a7-ba9) — retrying ONCE" >&2
  launch_once
  if [ "$rc" -eq 124 ]; then
    echo "[capture_gate] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s on the retry either; inspecting whatever it captured" >&2
  fi
fi

# Authoritative pass/fail: the OUT-OF-ENGINE frame inspection. Require at least 1
# frame (zero frames = the exe never rendered = a failure, same silent-killer
# shape as the test-result total>0 rule).
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 1
