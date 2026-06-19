#!/usr/bin/env bash
# verify_settings_gate.sh — shipped-build SETTINGS-PANEL capture gate (ticket 86caa4bqp).
#
# Sibling of capture_gate.sh, but purpose-built for the settings panel's success-test
# ("shipped-build capture: the panel OPENS + a tweak TAKES EFFECT LIVE" — Tess QA bounce
# on PR #83). The generic -captureGate renders a closed-world gameplay frame with NO panel,
# so it can never prove the panel ships or that a tweak changes a live param. This gate
# launches the BUILT exe WINDOWED with -verifySettings, which drives SettingsVerifyCapture:
# it opens the panel, drives the walk-speed slider to max, and writes
#   settings_closed.png  settings_open.png  settings_tweaked.png
# plus the ground-truth log line `[SettingsVerifyCapture] WALK SPEED tweak: ... changedLive=True`.
#
# Two authoritative checks, BOTH must pass:
#   1. frame_check.py on the panel PNGs — the open + tweaked frames are not black/uniform/
#      magenta (the panel actually RENDERED in the shipped player, not just the editor).
#   2. a grep of the player log for `changedLive=True` — the LIVE param actually changed
#      (a rendered panel that doesn't drive the game would still pass #1; this is the
#      end-to-end "tweak takes effect" proof the success-test demands).
#
# Windowed (NOT -batchmode — ScreenCapture needs a real swapchain, spike iter-4 /
# unity-conventions.md). The component calls Application.Quit() when done; a wall-clock
# timeout fails a hung launch instead of blocking CI forever (mirrors capture_gate.sh).
#
# Usage: verify_settings_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/settings-caps   logFile default: ci-out/verify-settings.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/settings-caps}"
LOG_FILE="${3:-ci-out/verify-settings.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_settings_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_settings] FAILED — exe not found: $EXE" >&2
  echo "[verify_settings]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Clear any stale captures so frame_check only sees THIS run's panel frames.
rm -f "$ABS_CAP"/settings_*.png
rm -f "$LOG_FILE"

echo "[verify_settings] launching shipped exe windowed (-verifySettings): $EXE"
echo "[verify_settings]   captureDir=$ABS_CAP logFile=$LOG_FILE"

# Windowed + small so it never grabs the desktop; -verifySettings drives
# SettingsVerifyCapture; -logFile redirects the standalone player's Player.log so the
# `changedLive=True` line is grep-able here. The component calls Application.Quit() when
# done; cap wall-clock so a hung launch fails instead of blocking CI forever.
LAUNCH_TIMEOUT=120
set +e
timeout "${LAUNCH_TIMEOUT}" "$EXE" \
  -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
  -verifySettings -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
rc=$?
set -e
if [ "$rc" -eq 124 ]; then
  echo "[verify_settings] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s; inspecting whatever it captured" >&2
fi

# Check 1 — the panel frames rendered (open + tweaked must be real content). Three frames
# expected (closed/open/tweaked); require >= 2 so a missing closed frame alone doesn't pass.
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 2
frame_rc=$?

# Check 2 — the live-effect ground-truth proof. The success-test is "a tweak takes effect
# LIVE", so a rendered panel is necessary but not sufficient; the log must prove the param
# changed. Fail loud if the log is missing or never reports changedLive=True.
log_rc=0
if [ ! -f "$LOG_FILE" ]; then
  echo "[verify_settings] FAILED — no player log at $LOG_FILE; cannot verify the live tweak took effect" >&2
  log_rc=1
elif grep -qF "changedLive=True" "$LOG_FILE"; then
  echo "[verify_settings] live-tweak proof:"
  grep -F "[SettingsVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_settings]   /'
else
  echo "[verify_settings] FAILED — player log has no 'changedLive=True'; the walk-speed tweak did NOT change the live param" >&2
  grep -F "[SettingsVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_settings]   /' || true
  log_rc=1
fi

if [ "$frame_rc" -ne 0 ] || [ "$log_rc" -ne 0 ]; then
  echo "[verify_settings] SETTINGS CAPTURE GATE FAILED (frames_rc=$frame_rc log_rc=$log_rc)" >&2
  exit 1
fi
echo "[verify_settings] SETTINGS CAPTURE GATE PASSED — panel rendered + live tweak took effect"
exit 0
