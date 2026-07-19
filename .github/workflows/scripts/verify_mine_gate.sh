#!/usr/bin/env bash
# verify_mine_gate.sh — shipped-build MINE (select pickaxe -> strike node -> break -> E-loot -> iron ore)
# capture gate (ticket 86camk1x4 — the CI-wiring follow-up to PR #287; Tess QA note N1 verbatim:
# "-verifyMine not wired into CI ... I-3/I-4 mine regressions otherwise uncaught", + N3: the author
# captures were gitignored with no artifact — wiring fixes both). Direct sibling of verify_chop_gate.sh
# (same headless RT-readback launch, same NavMesh-move + strike/loot mechanics), purpose-built for the
# mine loop's shipped-build proof: SELECT a stone pickaxe (the mine gate) -> teleport to a real ore node ->
# drive the active-left-click mine (MineOre.RequestMineClick) until the node BREAKS + drops an ore pile ->
# press E (PickableLooter.RequestLoot) to loot it -> iron_ore lands in the inventory.
#
# WHY THIS GATE EXISTS: -verifyMine (MineVerifyCapture.cs) already drives the REAL mine->break->pile->E-loot
# path and self-asserts iron_ore in the inventory, and it PASSES manually since PR #287 — but it was NEVER
# a CI gate (grep -rn verifyMine .github/workflows/ -> zero hits before this). So a real break in the mine
# loop (the I-3/I-4 mine regressions Tess flagged) would slip straight through GREEN CI exactly like the
# #165 chop class did: the generic -captureGate (step 7) only shoots the default spawn frame — the player
# stands mid-field with no pickaxe selected and no node in range, so nothing about the mine loop is exercised.
# This gate closes that hole: a mine-loop regression turns CI RED.
#
# This launches the BUILT exe with -verifyMine, which drives MineVerifyCapture: wait for the agent on the
# NavMesh, GRANT + SELECT a stone pickaxe, teleport to a runtime-discovered reachable ore node, strike + loot
# until iron_ore arrives, and SELF-ASSERT haveNode && gotOre (iron_ore count rose). It writes mine_before.png
# (spawn, no ore) + mine_pickaxe_selected.png (pickaxe in hand) + mine_after.png (at the node, ore in the
# readout). It calls Application.Quit(pass ? 0 : 1), so the exe's exit code IS the gate verdict — this wrapper
# just launches it and propagates that, with a frame_check.py backstop on the PNGs (a real swapchain frame,
# not black/uniform/magenta).
#
# HEADLESS via RT-readback (86cag93zb, same as verify_chop/verify_heldbelt): -batchmode, NO -nographics
# (a real D3D12 device inits), NO window — MineVerifyCapture renders Camera.main into an offscreen RT
# (RenderTextureCapture.CaptureCameraToTexture) and its self-asserts are LOGIC (iron_ore count), so the
# mine verdict is unchanged headless. WEDGE HARDENING (mirrors capture_gate.sh / verify_chop_gate.sh):
# LAUNCH_TIMEOUT 300, `timeout -k 15` SIGKILLs a SIGTERM-ignoring hung player, and a single rc==124-only
# retry re-launches ONCE on a first-frame present-loop wedge before declaring failure (a real non-zero gate
# failure is NEVER retried — that would mask a genuine mine-loop regression). The mine coroutine drives a
# ~multi-second NavMesh settle + a 25s strike/loot loop, so 300 gives real margin over the longest healthy run.
#
# Usage: verify_mine_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/mine-caps   logFile default: ci-out/verify-mine.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/mine-caps}"
LOG_FILE="${3:-ci-out/verify-mine.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_mine_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_mine] FAILED — exe not found: $EXE" >&2
  echo "[verify_mine]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Wall-clock cap so a hung launch fails instead of blocking CI forever. 300 gives real margin over the
# longest healthy launch (a ~3s NavMesh settle + a 25s strike/loot loop + captures). `-k 15` hard-KILLs
# (SIGKILL) a player that ignores the soft SIGTERM 15s later so a wedged D3D12 present-loop process can't
# linger into the retry / the next gate.
LAUNCH_TIMEOUT=300

# launch_once — clear stale artifacts, launch the exe under timeout, set exe_rc. Re-clears EVERY attempt
# so a partial first-attempt capture/log can't mask the retry.
launch_once() {
  rm -f "$ABS_CAP"/mine_*.png
  rm -f "$LOG_FILE"
  echo "[verify_mine] launching shipped exe -batchmode (headless RT-readback, -verifyMine): $EXE"
  echo "[verify_mine]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  # HEADLESS (86cag93zb): -batchmode, NO -nographics (real D3D12 device), NO window. MineVerifyCapture
  # captures Camera.main into an offscreen RT (CaptureCameraToTexture); its self-asserts are LOGIC
  # (iron_ore count), so the haveNode/gotOre verdict is unchanged. -logFile redirects the Player.log so
  # the verdict lines are grep-able here. The component calls Application.Quit(0/1).
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -batchmode \
    -verifyMine -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero
# self-assert failure is NOT a wedge — never retry it (it would mask a genuine mine-loop regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_mine] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_mine] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[MineVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_mine]   /' || true
fi

# Check 1 — the exit code IS the gate. MineVerifyCapture self-asserts the FULL select-pickaxe -> mine ->
# break -> pile -> E-loot loop (iron_ore count rose via the REAL MineOre.RequestMineClick +
# PickableLooter.RequestLoot path), else Quit(1). A non-zero exe_rc means no reachable ore node was found,
# the node never broke, the pile was never looted, OR the wiring was missing from Boot.unity — exactly the
# I-3/I-4 mine-loop break Tess flagged.
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_mine] FAILED — -verifyMine self-assert reported the select-pickaxe -> mine -> break -> E-loot -> iron ore loop did NOT complete (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the mine frames must be real swapchain content (not black/uniform/magenta).
# Three frames expected (mine_before + mine_pickaxe_selected + mine_after); require >= 2 so a missing
# 'before' alone still passes but a near-total capture failure does not.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 2
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_mine] MINE CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_mine] MINE CAPTURE GATE PASSED — select pickaxe → strike node → break → E-loot → iron ore in inventory, proven end-to-end in the shipped build"
exit 0
