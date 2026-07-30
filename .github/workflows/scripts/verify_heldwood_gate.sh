#!/usr/bin/env bash
# verify_heldwood_gate.sh — shipped-build WOOD-TIER-IN-HAND capture gate (ticket 86cav8y74, closing the
# coverage gap Tess raised on PR #327, comment 5025894753, verbatim: "round-3 fixed the wood belt→held path
# but shipped NO shipped-build capture that drives a wood index through the hand. -verifyHeldBelt covers
# stone axe/spear; -verifyHeldPickaxe covers stone/iron pickaxe. […] This is the 'green test ≠
# rendered-in-hand' gap that let soak-3 ship — worth closing before the next wood-tier change.").
#
# WHY THIS GATE EXISTS: the soak-3 defect was "I craft a wooden axe, select it in the belt, and NOTHING is
# in the hand" — the wood item ids satisfied neither HeldAxe.ShouldShow nor the belt→held mesh sync, so the
# seat stayed hidden. Round-3 fixed it, but every shipped-build gate stopped at the stone/iron tiers:
#   * the generic -captureGate shoots the default SPAWN frame (nothing acquired, no belt driven);
#   * -verifyHeldBelt drives the stone axe + spear (and it CANNOT be extended to carry wood: the belt is 5
#     slots, so adding the 5 wood tools would fill it and destroy that gate's "empty slot selected ->
#     hidden" STATE-3 — hence a separate flag, not an extension);
#   * -verifyHeldPickaxe covers stone/iron pickaxe and is author-run evidence, NOT CI-wired
#     (`git grep verifyHeldPickaxe -- .github/` = 0), so extending IT would not gate anything either.
# So a re-break of the wood belt→held path would slip straight through GREEN CI exactly like the #165 chop /
# -verifyMine / -verifyBoulder class.
#
# This launches the BUILT exe with -verifyHeldWood, driving AxeVerifyCapture.RunHeldWoodVerification:
#   STATE-0  belt EMPTY at boot -> hands EMPTY (the negative control; a gate that only ever asserts "shown"
#            cannot tell a working seat from a permanently-visible one)
#   STATE-1..5  GRANT + SELECT each of the five wood tools (axe / dagger / sword / spear / pickaxe) through
#            the REAL InventoryModel.AddToolToBelt + SelectBelt seams — Inventory.Changed ->
#            SyncHeldVisualToSelection -> WoodSelectionIndexFor -> ApplyCurrent, i.e. the EXACT soak-3
#            mechanism — and per tool SELF-ASSERT: renderer ENABLED, CurrentIndex == that tool's wood family
#            index, WoodSelectionIndexFor agrees, the holder's sharedMesh IS the committed
#            WeaponSetLineup.prefab node named WeaponNodeNames[index] (an identity check, so a stale/short
#            lineup prefab falling back to the AXE mesh is caught — a vertex-count-differs assert would
#            MISS that), the mesh is not the axe baseline, and DebugViewActive is FALSE (selection, not the
#            [B] picker, owns the visual)
#   FINAL    re-select the WOOD AXE after the other four were displayed -> its mesh must RETURN (the
#            soak-224 crossed-state regression in its wood flavour)
# The component calls Application.Quit(1) on ANY failed state (or missing HeroAxe/cycle/Inventory wiring from
# Boot.unity), so the exe's exit code IS the gate verdict — this wrapper propagates it, with a frame_check.py
# backstop on the PNGs (real frames, not black/uniform/magenta).
# Frames written: held_wood_empty.png + held_wood_{axe,dagger,sword,spear,pickaxe}.png and their _close
# siblings (11 total).
#
# HEADLESS (86cag93zb): -batchmode, NO -nographics (real D3D12 device), NO window — the component captures
# Camera.main into an offscreen RT via SubmitRenderRequest and its self-asserts are LOGIC, so the verdict is
# capture-mechanism-independent. Do NOT "convert" this to a windowed launch (test_gate_scripts.sh's
# launch-mode invariant reds that).
# WEDGE HARDENING (86cafzaeb; mirrors verify_heldbelt_gate.sh): LAUNCH_TIMEOUT 300, `timeout -k 15` SIGKILLs
# a SIGTERM-ignoring hung player, and a single rc==124-only retry re-launches ONCE on a first-frame
# present-loop wedge before declaring failure (a real non-zero gate failure is NEVER retried).
#
# Usage: verify_heldwood_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/heldwood-caps   logFile default: ci-out/verify-heldwood.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/heldwood-caps}"
LOG_FILE="${3:-ci-out/verify-heldwood.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_heldwood_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_heldwood] FAILED — exe not found: $EXE" >&2
  echo "[verify_heldwood]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Wall-clock cap so a hung launch fails instead of blocking CI forever. 300 gives real margin over the
# longest healthy launch. `-k 15` hard-KILLs (SIGKILL) a player that ignores the soft SIGTERM 15s later, so a
# wedged D3D12 present-loop process can't linger into the retry / the next gate.
LAUNCH_TIMEOUT=300

# launch_once — clear stale artifacts, launch the exe under timeout, set exe_rc. Re-clears EVERY attempt so a
# partial first-attempt capture/log can't mask the retry.
launch_once() {
  rm -f "$ABS_CAP"/held_wood_*.png
  rm -f "$LOG_FILE"
  echo "[verify_heldwood] launching shipped exe -batchmode (headless RT-readback, -verifyHeldWood): $EXE"
  echo "[verify_heldwood]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -batchmode \
    -verifyHeldWood -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero
# self-assert failure is NOT a wedge — never retry it (it would mask a genuine wood-tier regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_heldwood] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_heldwood] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[AxeVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_heldwood]   /' || true
fi

# Check 1 — the exit code IS the gate. The component self-asserts the empty control, all five wood tools
# (shown + right index + right lineup-node mesh + not-the-axe-fallback + selection-owns-visual) and the
# wood-axe return, else Quit(1). A non-zero exe_rc means a wood state failed (or the Boot.unity wiring
# was missing).
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_heldwood] FAILED — -verifyHeldWood self-assert reported a WOOD-tier weapon does NOT render in-hand from its belt selection (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the held-wood frames must be real content (not black/uniform/magenta). Eleven
# frames expected (empty + 5 tools × gameplay/close); require >= 6 so a partial capture still gives signal
# but a near-total capture failure does not pass.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 6
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_heldwood] HELD-WOOD CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_heldwood] HELD-WOOD CAPTURE GATE PASSED — every wood tool selected in the belt renders ITS OWN mesh in-hand in the shipped build (the soak-3 class)"
exit 0
