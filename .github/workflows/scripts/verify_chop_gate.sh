#!/usr/bin/env bash
# verify_chop_gate.sh — shipped-build CHOP → PILE → E-LOOT capture gate (ticket 86cafecuj).
# Sibling of verify_loot_gate.sh / verify_pond_gate.sh / verify_settings_gate.sh / capture_gate.sh,
# purpose-built for tree-chop AC8: prove the FULL end-to-end loop in the SHIPPED exe — craft axe →
# chop a tree → the tree FELLS → a lootable LOG PILE spawns → E loots it (the REAL PickableLooter
# discovery path) → wood lands in the inventory.
#
# WHY THIS GATE EXISTS (discovered during PR #165 review, 2026-06-28): -verifyChop already drives the
# REAL looter path (ChopVerifyCapture.cs:114-115 RequestLoot()), but it was NOT a CI gate
# (grep -rn verifyChop .github/workflows/ → zero hits). The generic -captureGate (step 7) only sanity-
# checks the spawn frame, and the PlayMode/unit tests MASK the looter (ChopTreePlayModeTests hand-called
# DiscoverPickables(); LogPileTests bypass the looter). So a real break in the chop→pile→E-loot path
# (e.g. PR #165 head bbac84f: the pile was never registered, PickableLooter.EnsureDiscovered only re-scans
# when _pickables.Count==0 but a serialized pickable keeps it >0) slipped straight through GREEN CI —
# caught only by code review. This gate closes that hole: a pile-not-discovered regression turns CI RED.
#
# This launches the BUILT exe HEADLESS (-batchmode, no -nographics) with -verifyChop, which drives
# ChopVerifyCapture: it gets the axe (craft seam), fells the DEMO tree + loots its dropped pile via the
# real PickableLooter.RequestLoot path,
# then fells + loots a REAL seed-42 SCATTER tree (discovered at runtime, never hardcoded) for a SECOND,
# INDEPENDENT wood gain. It writes chop_before.png (spawn, no wood) + chop_after.png (demo tree, wood in
# readout) + chop_wood_axe.png (the WOOD axe selected + in hand) + chop_scatter.png (scatter tree, MORE
# wood) and SELF-ASSERTS:
#   demoWood (felled + LOOTED the demo pile) && scatterDiscovered (InstanceCount>1 — the scatter LP_Trees
#   were found, not just the demo) && scatterTarget (a reachable scatter tree was picked) &&
#   woodAxeSelected && scatterWood (a 2nd independent wood gain from the scatter tree's pile).
#
# It calls Application.Quit(pass ? 0 : 1), so the exe's exit code IS the gate verdict — this wrapper just
# launches it and propagates that, with a frame_check.py backstop on the PNGs (real rendered content, not
# black/uniform/magenta).
#
# TIER COVERAGE (86cav8y74, from Tess's PR #327 comment 5031539815 NIT 2, verbatim: "-verifyChop drives the
# STONE axe, so the WOOD-tier chop is not shipped-build-gated. […] selecting a wood axe (instead of
# PickUpAxe) in ChopVerifyCapture would make the exact soak-4 regression class shipped-build-gated"). The
# DEMO-tree phase still uses the STONE axe (that coverage is unchanged); the SCATTER phase now GRANTS +
# SELECTS the **WOOD** axe through the real AddToolToBelt/SelectBelt seams and asserts the discriminator
# triple — wood-axe selected AND IsAnyAxeSelectedInBelt AND **NOT** IsAxeSelectedInBelt (the stone-only
# predicate MUST be false; without that term a "fix" that re-selected stone would green this gate while the
# wood chop stayed broken). soak-4 was exactly a stone-only chop gate: the Sponsor tested WOOD first and
# could not chop. If ChopTree's axe gate ever re-narrows to stone-only, the scatter chop yields no wood and
# this gate goes RED.
#
# HEADLESS since 86cag93zb: -batchmode, NO -nographics, NO window — ChopVerifyCapture.ShotTo renders
# Camera.main into an OFFSCREEN RenderTexture (RenderTextureCapture -> SubmitRenderRequest), which works
# without a swapchain; the legacy backbuffer ScreenCapture does not. (86cav8y74 corrected this header: it
# still claimed the gate was windowed-and-not-batchmode long after the conversion — a reviewer re-running
# the gate off that stale line would re-add the windowed/nographics flags and get black false-empty frames,
# the #287 trap. tests/scripts/test_gate_scripts.sh's launch-mode invariant is the machine-checked source of
# truth. NOTE that invariant greps the WHOLE file for the windowed flag literal, so do NOT spell that flag
# out anywhere in a headless gate's comments — it reads as a windowed launch and reds the check.)
# A wall-clock timeout fails a hung launch instead of blocking CI forever (mirrors capture_gate.sh). Note
# the chop loop drives TWO trees with multi-second NavMesh moves + chop/loot cooldowns, so the cap is
# higher than the settings/loot/pond gates' 120s.
#
# Usage: verify_chop_gate.sh <FarHorizon.exe> [<captureDir>] [<logFile>]
#   captureDir default: ci-out/chop-caps   logFile default: ci-out/verify-chop.log
set -uo pipefail

EXE="${1:-}"
CAP_DIR="${2:-ci-out/chop-caps}"
LOG_FILE="${3:-ci-out/verify-chop.log}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "$EXE" ]; then
  echo "usage: verify_chop_gate.sh <FarHorizon.exe> [captureDir] [logFile]" >&2
  exit 2
fi
if [ ! -f "$EXE" ]; then
  echo "[verify_chop] FAILED — exe not found: $EXE" >&2
  echo "[verify_chop]   the build step must run first (FarHorizonBuilder.BuildWindows)" >&2
  exit 1
fi

mkdir -p "$CAP_DIR"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
mkdir -p "$(dirname "$LOG_FILE")"

# Wall-clock cap so a hung launch fails instead of blocking CI forever. WEDGE HARDENING
# (86cafzaeb; adopts #189's capture_gate/pond pattern): 300 (was 180 — the chop sequence drives
# two trees: axe craft + two ~multi-second NavMesh moves + chop/loot cooldowns; 300 gives real
# margin over the longest healthy run AND matches the uniform gate cap), `-k 15` hard-KILLs
# (SIGKILL) a player that ignores the soft SIGTERM 15s later so a wedged D3D12 present-loop
# process can't linger into the retry / the next gate.
LAUNCH_TIMEOUT=300

# launch_once — clear stale artifacts, launch the headless exe under timeout, set exe_rc.
# Re-clears EVERY attempt so a partial first-attempt capture/log can't mask the retry.
launch_once() {
  rm -f "$ABS_CAP"/chop_*.png
  rm -f "$LOG_FILE"
  echo "[verify_chop] launching shipped exe -batchmode (headless RT-readback, -verifyChop): $EXE"
  echo "[verify_chop]   captureDir=$ABS_CAP logFile=$LOG_FILE"
  # HEADLESS (86cag93zb): -batchmode, NO -nographics (real D3D12 device), NO window. ChopVerifyCapture
  # captures Camera.main into an offscreen RT (SubmitRenderRequest); its self-asserts are LOGIC
  # (WoodCount / InstanceCount), so the demoWood/scatterWood verdict is unchanged. -logFile redirects
  # the Player.log so the verdict lines are grep-able here. The component calls Application.Quit(0/1).
  set +e
  timeout -k 15 "${LAUNCH_TIMEOUT}" "$EXE" \
    -batchmode \
    -verifyChop -captureDir "$ABS_CAP" -logFile "$LOG_FILE"
  exe_rc=$?
  set -e
}

launch_once
# ONE retry, ONLY on a timeout-hang (rc 124 = the first-frame present-loop wedge). A real non-zero
# self-assert failure is NOT a wedge — never retry it (it would mask a genuine chop-loop regression).
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_chop] WARN — exe did not self-quit within ${LAUNCH_TIMEOUT}s (timeout-hang; likely the present-loop wedge) — retrying ONCE" >&2
  launch_once
fi
if [ "$exe_rc" -eq 124 ]; then
  echo "[verify_chop] FAILED — exe did not self-quit within ${LAUNCH_TIMEOUT}s (hung launch, including the retry)" >&2
fi

# Echo the component's ground-truth verdict line(s) for the CI log.
if [ -f "$LOG_FILE" ]; then
  grep -F "[ChopVerifyCapture]" "$LOG_FILE" | sed 's/^/[verify_chop]   /' || true
fi

# Check 1 — the exit code IS the gate. ChopVerifyCapture self-asserts the FULL chop→pile→E-loot loop
# (demoWood via the REAL PickableLooter path) AND the scatter-tree choppability, else Quit(1). A non-zero
# exe_rc means the chop did NOT yield looted wood, the pile was never discovered by the live looter, the
# scatter trees weren't found, OR the wiring was missing from Boot.unity — exactly the #165-class break.
exe_gate_rc=0
if [ "$exe_rc" -ne 0 ]; then
  echo "[verify_chop] FAILED — -verifyChop self-assert reported the chop→pile→E-loot loop did NOT complete (exe_rc=$exe_rc)" >&2
  exe_gate_rc=1
fi

# Check 2 — frame backstop: the chop frames must be real swapchain content (not black/uniform/magenta).
# Three frames expected (chop_before + chop_after + chop_scatter); require >= 2 so a missing 'before'
# alone still passes but a near-total capture failure does not.
set +e
python3 "$HERE/frame_check.py" "$ABS_CAP" --min-frames 2
frame_rc=$?
set -e

if [ "$exe_gate_rc" -ne 0 ] || [ "$frame_rc" -ne 0 ]; then
  echo "[verify_chop] CHOP CAPTURE GATE FAILED (exe_rc=$exe_rc frame_rc=$frame_rc)" >&2
  exit 1
fi
echo "[verify_chop] CHOP CAPTURE GATE PASSED — craft axe → chop → fell → log pile → E-loot → wood in inventory, proven end-to-end (demo + real scatter tree) in the shipped build"
exit 0
