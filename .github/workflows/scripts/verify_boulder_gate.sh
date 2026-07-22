#!/usr/bin/env bash
# verify_boulder_gate.sh — shipped-build BOULDER-MINE (select wood pickaxe -> strike boulder -> break ->
# E-loot -> stone) capture gate (ticket 86catr79g — the CI-wiring follow-up to PR #303/② the way #299
# wired -verifyMine; Tess QA PASS on PR #303 verbatim: "-verifyBoulder ships with ② and passes on the
# shipped exe ... but is NOT CI-wired — boulder-mining regressions otherwise uncaught, exactly the
# -verifyMine gap class"). Direct sibling of verify_mine_gate.sh (same headless RT-readback launch, same
# NavMesh-move + strike/loot mechanics), purpose-built for the boulder loop's shipped-build proof: SELECT a
# WOOD pickaxe (the ② boulder-mine entry gate) -> teleport to a real boulder -> drive the active-left-click
# mine (MineBoulder.RequestMineClick) until the boulder BREAKS + drops a stone pile -> press E
# (PickableLooter.RequestLoot) to loot it -> stone lands in the inventory.
#
# WHY THIS GATE EXISTS: -verifyBoulder (BoulderVerifyCapture.cs) already drives the REAL boulder->break->pile->
# E-loot path and self-asserts stone in the inventory, and it PASSES manually since ② (PR #303) — but it was
# NEVER a CI gate (grep -rn verifyBoulder .github/workflows/ -> zero hits before this). So a real break in the
# boulder loop (the boulder-mining regressions Tess flagged) would slip straight through GREEN CI exactly like
# the -verifyMine gap class did: the generic -captureGate (step 7) only shoots the default spawn frame — the
# player stands mid-field with no pickaxe selected and no boulder in range, so nothing about the boulder loop
# is exercised. This gate closes that hole: a boulder-loop regression turns CI RED.
#
# This launches the BUILT exe with -verifyBoulder, which drives BoulderVerifyCapture: wait for the agent on the
# NavMesh, GRANT + SELECT a wood pickaxe, teleport to a runtime-discovered reachable boulder, strike + loot
# until stone arrives, and SELF-ASSERT haveBoulder && gotStone (stone count rose). It writes boulder_before.png
# (spawn, no stone) + boulder_side.png (a boulder side-profile, Bar 4) + boulder_after.png (at the boulder,
# stone in the readout). It calls Application.Quit(pass ? 0 : 1), so the exe's exit code IS the gate verdict —
# this wrapper just launches it and propagates that, with a frame_check.py backstop on the PNGs (a real
# swapchain frame, not black/uniform/magenta).
#
# HEADLESS via RT-readback (86cag93zb, same as verify_mine/verify_chop/verify_heldbelt): -batchmode, NO
# -nographics (a real D3D12 device inits), NO window — BoulderVerifyCapture renders Camera.main into an
# offscreen RT (RenderTextureCapture.CaptureCameraToTexture) and its self-asserts are LOGIC (stone count), so
# the boulder verdict is unchanged headless. WEDGE HARDENING (mirrors capture_gate.sh / verify_mine_gate.sh):
# LAUNCH_TIMEOUT 300, `timeout -k 15` SIGKILLs a SIGTERM-ignoring hung player, and a single rc==124-only
# retry re-launches ONCE on a first-frame present-loop wedge before declaring failure (a real non-zero gate
# failure is NEVER retried — that would mask a genuine boulder-loop regression). The boulder coroutine drives a
# ~multi-second NavMesh settle + a 25s strike/loot loop, so 300 gives real margin over the longest healthy run.
#
# Usage: verify_boulder_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/boulder-caps   logFile default: ci-out/verify-boulder.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/boulder-caps}"
LOG_FILE="${3:-ci-out/verify-boulder.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_boulder_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_boulder] FAILED — exe not found: $EXE" >&2
  echo "[verify_boulder]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
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
  rm -f "$ABS_CAP"/boulder_*.png
  rm -f "$LOG_FILE"
  echo "[verify_boulder] launching shipped exe -batchmode (headless RT-readback, -verifyBoulder): $EXE"
  echo "[verify_boulder]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  # HEADLESS (86cag93zb): -batchmode, NO -nographics (real D3D12 device), NO window. BoulderVerifyCapture
  # captures Camera.main into an offscreen RT (CaptureCameraToTexture); its self-asserts are LOGIC
  # (stone count), so the haveBoulder/gotStone verdict is unchanged. -logFile redirects the Player.log so
  # the verdict lines are grep-able here. The component calls Application.Quit(0/1).
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -batchmode \
    -verifyBoulder -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero
# self-assert failure is NOT a wedge — never retry it (it would mask a genuine boulder-loop regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_boulder] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_boulder] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[BoulderVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_boulder]   /' || true
fi

# Check 1 — the exit code IS the gate. BoulderVerifyCapture self-asserts the FULL select-wood-pickaxe -> mine
# -> break -> pile -> E-loot loop (stone count rose via the REAL MineBoulder.RequestMineClick +
# PickableLooter.RequestLoot path), else Quit(1). A non-zero exe_rc means no reachable boulder was found,
# the boulder never broke, the pile was never looted, OR the wiring was missing from Boot.unity — exactly the
# boulder-loop break Tess flagged.
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_boulder] FAILED — -verifyBoulder self-assert reported the select-wood-pickaxe -> mine -> break -> E-loot -> stone loop did NOT complete (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the boulder frames must be real swapchain content (not black/uniform/magenta).
# Three frames expected (boulder_before + boulder_side + boulder_after); require >= 2 so a missing
# 'before' alone still passes but a near-total capture failure does not.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 2
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_boulder] BOULDER CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_boulder] BOULDER CAPTURE GATE PASSED — select wood pickaxe → strike boulder → break → E-loot → stone in inventory, proven end-to-end in the shipped build"
exit 0
