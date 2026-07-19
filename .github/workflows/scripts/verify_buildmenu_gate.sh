#!/usr/bin/env bash
# verify_buildmenu_gate.sh — shipped-build BUILD-MENU capture gate (ticket 86catpvpa).
#
# Sibling of verify_settings_gate.sh (both drive a UI-Toolkit modal panel). The generic -captureGate
# renders a closed-world gameplay frame with NO menu, so it can never prove the build menu ships, that a
# greyed row is non-interactive, or that selecting a row enters the ① ghost placement flow. This gate
# launches the BUILT exe WINDOWED with -verifyBuildMenu, which drives BuildMenuVerifyCapture: it opens the
# menu, proves an unaffordable row is non-interactive, grants the table's materials, selects the row, and
# writes
#   buildmenu_closed.png  buildmenu_open.png  buildmenu_placing.png
# plus the ground-truth log lines:
#   [BuildMenuVerifyCapture] rows=N ... menuOpen=True gated=True
#   [BuildMenuVerifyCapture] unaffordable NON-interactive: ... unaffordableNonInteractive=True
#   [BuildMenuVerifyCapture] select entered ghost: ... selectedEnteredGhost=True
#
# Four authoritative checks, ALL must pass:
#   1. frame_check.py on the menu PNGs — the open + placing frames are not black/uniform/magenta (the menu
#      actually RENDERED in the shipped player, not just the editor).
#   2. a grep of the player log for `selectedEnteredGhost=True` — the REGRESSION GUARD: selecting a
#      buildable row entered the ① free-cursor ghost placement flow (a menu that opens but never enters the
#      ghost flow would still pass #1; this is the end-to-end "select → ghost" proof).
#   3. a grep for `unaffordableNonInteractive=True` (a greyed row is non-interactive — the AC) AND `gated=True`
#      (the open menu is MODAL, so a click can never leak to a world verb — constraint 4).
#   4. frames_differ.py buildmenu_open.png vs buildmenu_closed.png — the menu is VISIBLE (open != closed): the
#      panel really appeared over the world, not a no-op open.
#
# Windowed (NOT -batchmode — ScreenCapture needs a real swapchain; the UI-Toolkit overlay composites to the
# backbuffer, not a camera RenderTexture — unity-conventions.md §Headless). The component calls
# Application.Quit() when done; a wall-clock timeout fails a hung launch (mirrors verify_settings_gate.sh).
#
# Usage: verify_buildmenu_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/buildmenu-caps   logFile default: ci-out/verify-buildmenu.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/buildmenu-caps}"
LOG_FILE="${3:-ci-out/verify-buildmenu.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_buildmenu_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_buildmenu] FAILED — exe not found: $EXE" >&2
  echo "[verify_buildmenu]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Wall-clock cap so a hung launch fails instead of blocking CI forever (adopts the settings/pond pattern:
# 300s, `-k 15` hard-KILLs a player that ignores the soft SIGTERM so a wedged present-loop can't linger).
LAUNCH_TIMEOUT=300

launch_once() {
  rm -f "$ABS_CAP"/buildmenu_*.png
  rm -f "$LOG_FILE"
  echo "[verify_buildmenu] launching shipped exe windowed (-verifyBuildMenu): $EXE"
  echo "[verify_buildmenu]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -verifyBuildMenu -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the present-loop wedge) — uniform with every verify_*_gate.sh.
if [ "$rc" -eq 124 ]; then
  echo "[verify_buildmenu] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$rc" -eq 124 ]; then
  echo "[verify_buildmenu] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s on the retry either; the checks below report whether the capture truncated" >&2
fi

# Check 1 — the menu frames rendered (closed/open/placing; require >= 2 so a single frame doesn't pass).
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 2
frame_rc=$?
set -e

# Check 2 — the REGRESSION GUARD: selecting a buildable row entered the ① ghost flow.
guard_rc=0
if [ ! -f "$LOG_FILE" ]; then
  echo "[verify_buildmenu] FAILED — no player log at $LOG_FILE; cannot verify select-enters-ghost" >&2
  guard_rc=1
elif grep -qF "selectedEnteredGhost=True" "$LOG_FILE"; then
  echo "[verify_buildmenu] select-enters-ghost proof:"
  grep -F "[BuildMenuVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_buildmenu]   /'
else
  echo "[verify_buildmenu] FAILED — player log has no 'selectedEnteredGhost=True'; selecting the (affordable) build-menu row did NOT enter the ① ghost placement flow (the regression guard)" >&2
  grep -F "[BuildMenuVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_buildmenu]   /' || true
  guard_rc=1
fi

# Check 3 — greyed row NON-interactive AND the open menu is MODAL (no click leak, constraint 4).
model_rc=0
if [ ! -f "$LOG_FILE" ]; then
  echo "[verify_buildmenu] FAILED — no player log at $LOG_FILE; cannot verify non-interactive/modal" >&2
  model_rc=1
else
  if ! grep -qF "unaffordableNonInteractive=True" "$LOG_FILE"; then
    echo "[verify_buildmenu] FAILED — no 'unaffordableNonInteractive=True'; a greyed (unaffordable) row was NOT non-interactive (it should refuse selection + not enter placement)" >&2
    model_rc=1
  fi
  if ! grep -qF "gated=True" "$LOG_FILE"; then
    echo "[verify_buildmenu] FAILED — no 'gated=True'; the OPEN menu did not swallow world input (constraint 4: the modal menu must gate UiInputGate so a click never leaks to a world verb)" >&2
    model_rc=1
  fi
fi

# Check 4 — the menu is VISIBLE: buildmenu_open.png must differ from buildmenu_closed.png.
diff_rc=0
OPEN_PNG="$ABS_CAP/buildmenu_open.png"
CLOSED_PNG="$ABS_CAP/buildmenu_closed.png"
if [ ! -f "$OPEN_PNG" ] || [ ! -f "$CLOSED_PNG" ]; then
  diff_rc=1
  last_present="(none — no frames captured)"
  for f in buildmenu_closed.png buildmenu_open.png buildmenu_placing.png; do
    [ -f "$ABS_CAP/$f" ] && last_present="$f"
  done
  echo "[verify_buildmenu] FAILED — capture TRUNCATED (WEDGE): a required frame is absent; last frame actually present = $last_present. The -verifyBuildMenu exe did not finish the capture sequence — a serial rerun has cleared this flake class before." >&2
else
  set +e
  python3 "$HERE/frames_differ.py" "$OPEN_PNG" "$CLOSED_PNG"
  diff_rc=$?
  set -e
  if [ "$diff_rc" -ne 0 ]; then
    echo "[verify_buildmenu] FAILED — visible-diff sub-check FAILED: buildmenu_open.png did not visibly differ from buildmenu_closed.png. The menu did NOT render over the world under capture (a no-op open)." >&2
  fi
fi

# All FOUR checks gate the merge: menu rendered (Check 1) + select enters the ① ghost flow (Check 2,
# regression guard) + greyed row non-interactive + modal (Check 3) + menu VISIBLE (Check 4).
if [ "$frame_rc" -ne 0 ] || [ "$guard_rc" -ne 0 ] || [ "$model_rc" -ne 0 ] || [ "$diff_rc" -ne 0 ]; then
  echo "[verify_buildmenu] BUILD-MENU CAPTURE GATE FAILED (frames_rc=$frame_rc guard_rc=$guard_rc model_rc=$model_rc diff_rc=$diff_rc)" >&2
  exit 1
fi
echo "[verify_buildmenu] BUILD-MENU CAPTURE GATE PASSED — menu rendered + select enters the ① ghost flow + greyed row non-interactive + modal + menu VISIBLE"
exit 0
