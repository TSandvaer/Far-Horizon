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
# Three authoritative checks, ALL must pass:
#   1. frame_check.py on the panel PNGs — the open + tweaked frames are not black/uniform/
#      magenta (the panel actually RENDERED in the shipped player, not just the editor).
#   2. a grep of the player log for `changedLive=True` — the LIVE param actually changed
#      (a rendered panel that doesn't drive the game would still pass #1; this is the
#      end-to-end "tweak takes effect" proof the success-test demands).
#   3. frames_differ.py settings_open.png vs settings_tweaked.png — the tweak is VISIBLE.
#      FATAL (un-quarantined 86cabe3e5). Checks 1+2 BOTH passed on PR #83 while settings_tweaked.png
#      was a PIXEL-IDENTICAL copy of settings_open.png, because the -verifySettings harness drove the
#      tweak via the entry setter + RefreshReadouts, which bypasses the real UI Toolkit ChangeEvent →
#      the synthetic drive never repainted the slider readout under capture. 86cabe3e5 replaced that
#      synthetic drive with a REAL dispatched ChangeEvent (SettingsPanel.DriveFloat/DriveRange-
#      ChangeEventForCapture — the same event a user's drag fires), so the captured frame now repaints
#      and this sub-check is authoritative again: a byte-identical tweaked frame REDS the gate, catching
#      any regression back to the synthetic drive.
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

# Wall-clock cap so a hung launch fails instead of blocking CI forever. WEDGE HARDENING
# (86cafzaeb; adopts #189's capture_gate/pond pattern): 300 (was 120 — no margin), `-k 15`
# hard-KILLs (SIGKILL) a player that ignores the soft SIGTERM 15s later so a wedged D3D12
# present-loop process can't linger into the retry / the next gate.
LAUNCH_TIMEOUT=300

# launch_once — clear stale artifacts, launch the windowed exe under timeout, set rc. Re-clears
# EVERY attempt so a partial first-attempt capture/log can't mask the retry.
launch_once() {
  rm -f "$ABS_CAP"/settings_*.png
  rm -f "$LOG_FILE"
  echo "[verify_settings] launching shipped exe windowed (-verifySettings): $EXE"
  echo "[verify_settings]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  # Windowed + small so it never grabs the desktop; -verifySettings drives
  # SettingsVerifyCapture; -logFile redirects the standalone player's Player.log so the
  # `changedLive=True` line is grep-able here. The component calls Application.Quit() when done.
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -verifySettings -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real
# non-zero exit is NOT a wedge — never retry it. NOTE: this gate's PASS criteria are checks
# 1+2 below (frames + changedLive), NOT the exe exit code — unchanged by the hardening.
if [ "$rc" -eq 124 ]; then
  echo "[verify_settings] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$rc" -eq 124 ]; then
  echo "[verify_settings] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s on the retry either; inspecting whatever it captured" >&2
fi

# Check 1 — the panel frames rendered (open + tweaked must be real content). Three frames
# expected (closed/open/tweaked); require >= 2 so a missing closed frame alone doesn't pass.
# set +e guard (86cafzaeb): errexit is active after launch_once — unguarded, a frame fail
# would abort HERE and skip the aggregate verdict line below (same rationale as Check 3's).
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 2
frame_rc=$?
set -e

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

# Check 3 — the tweak is VISIBLE: settings_tweaked.png must differ from settings_open.png.
# A repainted readout label changes only a small region, so the floor is tiny but strictly > 0
# (a byte-identical copy fails). This is the PR #83 re-QA regression guard.
#
# ── FATAL again, un-quarantined 86cabe3e5 ────────────────────────────────────
# It was quarantined-non-fatal because the -verifySettings harness drove the tweak via the entry
# setter + RefreshReadouts, which bypasses the real UI Toolkit ChangeEvent → the synthetic drive
# never repainted the slider readout under capture, so settings_tweaked.png came out pixel-identical
# to settings_open.png even though changedLive=True. 86cabe3e5 replaced that synthetic drive with a
# REAL dispatched ChangeEvent on the bound control (SettingsPanel.DriveFloat/DriveRange-
# ChangeEventForCapture — the same event a user's drag fires), so the captured frame now repaints.
# This sub-check is therefore authoritative again: a byte-identical tweaked frame REDS the gate,
# catching any regression back to the synthetic drive.
diff_rc=0
OPEN_PNG="$ABS_CAP/settings_open.png"
TWEAKED_PNG="$ABS_CAP/settings_tweaked.png"
if [ ! -f "$OPEN_PNG" ] || [ ! -f "$TWEAKED_PNG" ]; then
  echo "[verify_settings] FAILED — missing open/tweaked frame for the visible-tweak diff " \
       "(open=$OPEN_PNG tweaked=$TWEAKED_PNG)" >&2
  diff_rc=1
else
  # set +e around the call: script runs under `set -e` (re-enabled after the launch block above),
  # so an unguarded non-zero exit here would ABORT before the aggregate verdict line. Capture rc.
  set +e
  python3 "$HERE/frames_differ.py" "$OPEN_PNG" "$TWEAKED_PNG"
  diff_rc=$?
  set -e
fi
if [ "$diff_rc" -ne 0 ]; then
  echo "[verify_settings] FAILED — tweaked-frame visible-diff sub-check FAILED: settings_tweaked.png did not visibly differ from settings_open.png. The tweak did NOT repaint the panel under capture — a regression back to the synthetic entry-setter drive (the PR #83 re-QA bug). Drive the tweak via a real dispatched ChangeEvent (86cabe3e5)." >&2
fi

# All three checks gate the merge: panel rendered (Check 1) + live param changed (Check 2) +
# tweak VISIBLE in the shipped frame (Check 3, un-quarantined 86cabe3e5).
if [ "$frame_rc" -ne 0 ] || [ "$log_rc" -ne 0 ] || [ "$diff_rc" -ne 0 ]; then
  echo "[verify_settings] SETTINGS CAPTURE GATE FAILED (frames_rc=$frame_rc log_rc=$log_rc diff_rc=$diff_rc)" >&2
  exit 1
fi
echo "[verify_settings] SETTINGS CAPTURE GATE PASSED — panel rendered + live tweak took effect + tweak VISIBLE in the shipped frame (diff_rc=$diff_rc)"
exit 0
