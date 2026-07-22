#!/usr/bin/env bash
# verify_loot_gate.sh — shipped-build LOOT-PROMPT capture gate (ticket 86cafc6ud — Tess QA #158 block).
# Sibling of verify_pond_gate.sh / verify_settings_gate.sh / capture_gate.sh, purpose-built for the
# loot-proximity prompt's success-test ("the 'Press E to pick up berries' tooltip RENDERS legibly in the
# SHIPPED build when the player is in loot range"). The generic CI -captureGate only shoots the DEFAULT
# SPAWN frame — where the player stands mid-field, OUTSIDE the tightened ~1.0–1.2u loot range, so the prompt
# is correctly HIDDEN → the prompt's SHOW state (the whole deliverable) had ZERO built-frame evidence.
#
# This launches the BUILT exe WINDOWED with -verifyLoot, which drives LootPromptVerifyCapture: it teleports
# the player IN loot range of a RIPE bush (so LootPrompt resolves it + OnGUI paints the tooltip), captures
# loot_prompt_far.png (player at spawn → prompt HIDDEN, the contrast frame) + loot_prompt.png (player in range
# → prompt SHOWING), and SELF-ASSERTS both (1) the LOGIC (looter.NearestInRange resolves the bush + the label
# is set) AND (2) the RENDER (the IMGUI plate+label actually painted into the swapchain frame). The component
# calls Application.Quit(1) if EITHER fails (or the bush/looter/prompt is missing from Boot.unity), so the
# exe's exit code IS the gate verdict — this wrapper just launches it windowed and propagates that, with a
# frame_check.py backstop on the PNGs (a real swapchain frame, not black/uniform/magenta).
#
# Windowed (NOT -batchmode — ScreenCapture needs a real swapchain, spike iter-4 / unity-conventions.md).
# A wall-clock timeout fails a hung launch instead of blocking CI forever (mirrors capture_gate.sh).
#
# Usage: verify_loot_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/loot-caps   logFile default: ci-out/verify-loot.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/loot-caps}"
LOG_FILE="${3:-ci-out/verify-loot.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_loot_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_loot] FAILED — exe not found: $EXE" >&2
  echo "[verify_loot]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
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

# launch_once — clear stale artifacts, launch the windowed exe under timeout, set exe_rc.
# Re-clears EVERY attempt so a partial first-attempt capture/log can't mask the retry.
launch_once() {
  rm -f "$ABS_CAP"/loot_prompt*.png
  rm -f "$LOG_FILE"
  echo "[verify_loot] launching shipped exe windowed (-verifyLoot): $EXE"
  echo "[verify_loot]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  # Windowed + small so it never grabs the desktop; -verifyLoot drives LootPromptVerifyCapture;
  # -logFile redirects the standalone player's Player.log so the SHOW/RENDER verdict lines are
  # grep-able here. The component calls Application.Quit(0/1) when done.
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -verifyLoot -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero
# self-assert failure is NOT a wedge — never retry it (it would mask a genuine prompt regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_loot] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_loot] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[LootPromptVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_loot]   /' || true
fi

# Check 1 — the exit code IS the gate. The component self-asserts the SHOW state (NearestInRange resolves the
# bush + the label is set) AND that the IMGUI prompt actually RENDERED into the frame, else Quit(1). A non-zero
# exe_rc means the prompt did NOT reach its show state OR did not paint (or the wiring was missing from Boot.unity).
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_loot] FAILED — -verifyLoot self-assert reported the prompt did NOT show/render in range (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the loot-prompt frames must be real swapchain content (not black/uniform/magenta).
# Two frames expected (loot_prompt_far + loot_prompt); require >= 1 so a partial capture still gives signal.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 1
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_loot] LOOT-PROMPT CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_loot] LOOT-PROMPT CAPTURE GATE PASSED — the 'Press E to pick up berries' prompt SHOWS + renders legibly in range in the shipped build"
exit 0
