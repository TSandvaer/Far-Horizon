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
# Five authoritative checks, ALL must pass:
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
#   4. both drawers SHOW ROWS (#247 empty-drawers guard) — the row-visibility probe proves the F1 + F3
#      ScrollView viewports didn't collapse to zero height (header + footer but no rows).
#   5. the int-stepper columns have ROOM (#247 v2 F1-cramp guard) — the smallest resolved [−]/value/[+]
#      cell width per drawer proves the stepper control didn't collapse and overlap its glyphs. Check 1
#      (whole-frame) and Check 4 (row overlaps viewport) both PASS on a crushed stepper — a WITHIN-row
#      column crush is invisible to them, which is exactly how the F1 cramp reached the soak.
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

# Check 4 — the drawers actually SHOW ROWS (#247 empty-drawers regression guard). This gate went GREEN
# on the PR #247 build while BOTH drawers rendered header + footer but ZERO setting rows: the two-drawer
# split wrapped each shell in a scoped container with auto height 0 (its only child, the position:absolute
# scrim, is out of flow), so the panel's percentage max-height + the rows ScrollView's flex-grow resolved
# against a zero-height block → the ScrollView viewport collapsed → the rows were clipped out of view.
# Check 1 (frame_check) could not catch it: it checks the WHOLE 1280x720 frame (the green gameplay world =
# obviously not black/uniform → pass), never the panel region. SettingsVerifyCapture now probes GROUND TRUTH
# — the count of rows whose world rect overlaps the ScrollView VIEWPORT (+ the viewport's resolved height) per
# drawer — and logs `DEV rows visible: N / M routed (viewportHeight=Hpx)` + `PLAYER rows visible: N / M ...`.
# FAIL if EITHER drawer shows 0 visible rows, or if the proof line is absent.
rows_rc=0
if [ ! -f "$LOG_FILE" ]; then
  echo "[verify_settings] FAILED — no player log at $LOG_FILE; cannot verify the drawers show rows (#247)" >&2
  rows_rc=1
elif grep -qE "rows visible: 0 " "$LOG_FILE"; then
  echo "[verify_settings] FAILED — a drawer showed ZERO visible rows (#247 empty-drawers regression): the " \
       "panel rendered its header + footer but no setting rows (a collapsed flex-grow ScrollView viewport)." >&2
  grep -F "rows visible:" "$LOG_FILE" | sed 's/^/[verify_settings]   /' || true
  rows_rc=1
elif grep -qE "DEV rows visible: [1-9]" "$LOG_FILE" && grep -qE "PLAYER rows visible: [1-9]" "$LOG_FILE"; then
  echo "[verify_settings] row-render proof (#247): both drawers show > 0 rows:"
  grep -F "rows visible:" "$LOG_FILE" | sed 's/^/[verify_settings]   /'
else
  echo "[verify_settings] FAILED — missing the #247 row-visibility proof line for one/both drawers " \
       "(expected 'DEV rows visible: N / M' AND 'PLAYER rows visible: N / M', both N>0)." >&2
  grep -F "rows visible:" "$LOG_FILE" | sed 's/^/[verify_settings]   /' || true
  rows_rc=1
fi

# Check 5 — the int-stepper rows have ROOM (#247 v2 F1-cramp regression guard). The Sponsor re-soak of
# soak-247-v2 confirmed the empty-drawers fix, but flagged the F1 PLAYER drawer's int-stepper rows (Belt
# slots + Inventory stack size) as CRAMPED: the [−]/value/[+] columns clipped/overlapped ("− 5 [+ 5] 5"
# jammed). Root cause: the stepper control was flex-grow:1 with the DEFAULT flex-shrink:1, so on the F1 rows
# (no v-scrollbar → the row fit on one line) it was the only shrinkable child and collapsed below its 100px
# content, crushing the 28/44px cells; F3 read fine because its scrollbar narrowed the row past the wrap point.
# Neither frame_check (Check 1, whole-frame) nor the row-visibility probe (Check 4, viewport overlap) can see
# a WITHIN-row column crush — a crushed stepper still counts as a visible row. SettingsVerifyCapture now probes
# GROUND TRUTH — the smallest resolved [−]/value/[+] cell width per drawer — and logs
# `PLAYER STEPPER fit (#247 v2): minCellWidth=Npx` + `DEV STEPPER fit (#247 v2): minCellWidth=Npx`.
# FAIL if EITHER drawer's steppers crushed (minCellWidth < 20px; healthy = a 28px button), or a line is absent.
# A minCellWidth of -1 means the drawer legitimately has no stepper row (nothing to crush) → treated as pass.
stepper_rc=0
check_stepper_fit() {   # $1 = grep tag   $2 = human label
  local line w ok
  line=$(grep -F "$1" "$LOG_FILE" | head -n1)
  if [ -z "$line" ]; then
    echo "[verify_settings] FAILED — missing the #247 v2 $2 stepper-fit proof line (expected '$1 (#247 v2): minCellWidth=Npx')" >&2
    stepper_rc=1
    return
  fi
  w=$(printf '%s\n' "$line" | grep -oE 'minCellWidth=-?[0-9]+(\.[0-9]+)?' | head -n1 | cut -d= -f2)
  # threshold 20px: a healthy row keeps every cell at its design width (button 28px / value 44px); a crushed
  # flex-shrink control collapses them well below 10px. -1 = no stepper row in that drawer → legit skip (pass).
  ok=$(awk -v w="$w" 'BEGIN { print (w+0 >= 20.0 || w+0 < 0) ? 1 : 0 }')
  if [ "$ok" != "1" ]; then
    echo "[verify_settings] FAILED — $2 int-stepper columns CRUSHED (#247 v2): minCellWidth=${w}px < 20px — the [−]/value/[+] cells collapsed below their 28/44px design widths (the 'not enough room' overlap the Sponsor flagged). The stepper control must be flex-shrink:0 + min-width so the row WRAPS instead of crushing." >&2
    stepper_rc=1
  else
    echo "[verify_settings]   $2 stepper-fit OK: $line"
  fi
}
if [ ! -f "$LOG_FILE" ]; then
  echo "[verify_settings] FAILED — no player log at $LOG_FILE; cannot verify the stepper fit (#247 v2)" >&2
  stepper_rc=1
else
  echo "[verify_settings] stepper-fit proof (#247 v2 — F1 int-stepper rows have room, F3 not regressed):"
  check_stepper_fit "PLAYER STEPPER fit" "F1 (player)"
  check_stepper_fit "DEV STEPPER fit" "F3 (dev)"
fi

# All FIVE checks gate the merge: panel rendered (Check 1) + live param changed (Check 2) +
# tweak VISIBLE in the shipped frame (Check 3, un-quarantined 86cabe3e5) + BOTH drawers have rows
# (Check 4, #247 empty-drawers guard) + int-stepper columns have room (Check 5, #247 v2 F1-cramp guard).
if [ "$frame_rc" -ne 0 ] || [ "$log_rc" -ne 0 ] || [ "$diff_rc" -ne 0 ] || [ "$rows_rc" -ne 0 ] || [ "$stepper_rc" -ne 0 ]; then
  echo "[verify_settings] SETTINGS CAPTURE GATE FAILED (frames_rc=$frame_rc log_rc=$log_rc diff_rc=$diff_rc rows_rc=$rows_rc stepper_rc=$stepper_rc)" >&2
  exit 1
fi
echo "[verify_settings] SETTINGS CAPTURE GATE PASSED — panel rendered + live tweak took effect + tweak VISIBLE + both drawers have rows + stepper columns have room (stepper_rc=$stepper_rc)"
exit 0
