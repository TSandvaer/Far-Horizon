#!/usr/bin/env bash
# build_poc_island.sh — build + capture the NEXT-ISLAND POC exe (ticket 86caa9zpp).
#
# The POC is a STAND-ALONE scene (Assets/Scenes/NextIslandPoc.unity), NOT Boot.unity, so it is NOT wired
# into the CI capture job (which drives the shipped Boot exe). This script is the POC's shipped-build path:
# it builds the POC exe + runs the SAME capture gate the soak uses + the POC-specific perf/silhouette
# capture, then prints the soak handoff block (exe path + HUD stamp) for the Sponsor's walk-soak.
#
# It chains (mirrors serve_soak.sh, with the POC re-author inserted):
#   1. BootstrapProject.Run            — full project setup (URP + character/weapon import + HEAD stamp).
#   2. NextIslandPocScene.Build        — RE-authors the POC scene + registers it as the ONLY build scene.
#   3. FarHorizonBuilder.BuildWindows  — ships the POC (the last-registered scene).
#   4. verify_build_stamp.py           — FAILS LOUD unless the shipped stamp's sha == HEAD.
#   5. capture_gate.sh                 — generic -captureGate frame-sanity from the SHIPPED POC exe.
#   6. -verifyPocIsland launch         — the POC perf verdict + side-profile silhouette PNGs + [poc-trace] log.
#   7. cleanup                         — discards the bootstrap's tracked-asset churn (leave worktree as found).
#
# Usage: tools/debug/build_poc_island.sh [--keep-churn]
# Pre-req: a CLEAN worktree at the commit to build (cleanup restores tracked assets). Unity on this machine.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"   # tools/debug/ -> tools/ -> repo root
cd "$ROOT"

KEEP_CHURN=0
while [ $# -gt 0 ]; do
  case "$1" in
    --keep-churn) KEEP_CHURN=1; shift ;;
    *) echo "[build_poc] unknown arg: $1" >&2; exit 2 ;;
  esac
done

UNITY="${UNITY:-/c/Program Files/Unity/Hub/Editor/6000.4.11f1/Editor/Unity.exe}"
EXE="Build/Windows/FarHorizon.exe"
STAMP="Assets/Resources/BuildStamp.txt"
OUT_DIR="ci-out/poc"
CAP_DIR="$OUT_DIR/caps"
POC_CAP_DIR="$OUT_DIR/poc-island"
LOG_DIR="$OUT_DIR/logs"

step() { echo ""; echo "=== [build_poc] $1 ==="; }
die()  { echo "[build_poc] FAILED — $1" >&2; exit 1; }

[ -x "$UNITY" ] || die "Unity not found at '$UNITY' (set \$UNITY to override)"
if [ -n "$(git status --porcelain)" ]; then
  die "worktree is dirty — build from a CLEAN checkout (cleanup would clobber your edits). Commit first."
fi
HEAD_SHA="$(git rev-parse --short HEAD)" || die "cannot resolve HEAD sha"
mkdir -p "$CAP_DIR" "$POC_CAP_DIR" "$LOG_DIR"
CHURN_ROOTS="Assets ProjectSettings"

# --- 1. bootstrap (shared setup + fresh HEAD stamp) ------------------------
step "1/7 bootstrap (BootstrapProject.Run) — URP + character prep + stamp HEAD $HEAD_SHA"
"$UNITY" -batchmode -quit -nographics -projectPath "$ROOT" \
  -executeMethod FarHorizon.EditorTools.BootstrapProject.Run \
  -logFile "$LOG_DIR/bootstrap.log" \
  || die "bootstrap exited non-zero (see $LOG_DIR/bootstrap.log)"
grep -q '\[BootstrapProject\] complete' "$LOG_DIR/bootstrap.log" \
  || die "bootstrap did not log completion (see $LOG_DIR/bootstrap.log)"

# --- 2. author the POC scene + register it as the only build scene ---------
step "2/7 author POC scene (NextIslandPocScene.Build)"
"$UNITY" -batchmode -quit -nographics -projectPath "$ROOT" \
  -executeMethod FarHorizon.EditorTools.NextIslandPocScene.Build \
  -logFile "$LOG_DIR/poc-author.log" \
  || die "POC author exited non-zero (see $LOG_DIR/poc-author.log)"
grep -q '\[poc-build\] complete' "$LOG_DIR/poc-author.log" \
  || die "POC author did not log completion (see $LOG_DIR/poc-author.log)"

# --- 3. build the POC exe --------------------------------------------------
step "3/7 build POC exe (FarHorizonBuilder.BuildWindows)"
"$UNITY" -batchmode -quit -nographics -projectPath "$ROOT" \
  -executeMethod FarHorizon.EditorTools.FarHorizonBuilder.BuildWindows \
  -logFile "$LOG_DIR/build.log" \
  || die "build exited non-zero (see $LOG_DIR/build.log)"
grep -qE '\[FarHorizonBuilder\] result=Succeeded' "$LOG_DIR/build.log" \
  || die "build.log has no 'result=Succeeded' line (see $LOG_DIR/build.log)"
[ -f "$EXE" ] || die "build produced no exe at $EXE"

# --- 4. stamp-vs-HEAD guard ------------------------------------------------
step "4/7 verify shipped stamp matches HEAD $HEAD_SHA"
python3 "$ROOT/.github/workflows/scripts/verify_build_stamp.py" "$STAMP" "$HEAD_SHA" \
  || die "shipped stamp does not match HEAD — refusing a build-ambiguous exe"

# --- 5. generic capture-gate frame sanity from the SHIPPED POC exe ---------
step "5/7 capture-gate frame sanity (capture_gate.sh)"
bash "$ROOT/.github/workflows/scripts/capture_gate.sh" "$EXE" "$CAP_DIR" 4 "$LOG_DIR/capture.log" \
  || die "capture gate failed — the shipped POC exe did not render real frames (see $CAP_DIR)"

# --- 6. POC perf verdict + side-profile silhouette -------------------------
# The POC-specific windowed capture: perf FPS + the mandatory mountain side-profile silhouette + the
# on-mountain climb + NavMesh coverage, all as PNGs + [poc-trace] log lines from the SHIPPED exe.
step "6/7 POC perf + silhouette (-verifyPocIsland)"
rm -f "$POC_CAP_DIR"/*.png
set +e
timeout -k 15 420 "$EXE" \
  -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
  -verifyPocIsland -captureDir "$POC_CAP_DIR" -logFile "$LOG_DIR/verify-poc.log"
POC_RC=$?
set -e
if [ "$POC_RC" -ne 0 ] && [ "$POC_RC" -ne 124 ]; then
  echo "[build_poc] WARN — -verifyPocIsland exited $POC_RC (see $LOG_DIR/verify-poc.log)" >&2
fi
echo ""
echo "[build_poc] --- POC perf + walkability trace ([poc-trace] lines from the SHIPPED exe) ---"
grep -E '\[poc-trace\] (PERF|NAVMESH|climb RESULT|SIDE-PROFILE)' "$LOG_DIR/verify-poc.log" 2>/dev/null || \
  echo "[build_poc]   (no [poc-trace] perf/nav lines — inspect $LOG_DIR/verify-poc.log)"

# --- 7. cleanup bootstrap churn --------------------------------------------
if [ "$KEEP_CHURN" -eq 0 ]; then
  step "7/7 cleanup — discarding bootstrap's tracked-asset churn"
  # shellcheck disable=SC2086
  git checkout -- $CHURN_ROOTS 2>/dev/null || true
  if [ -n "$(git status --porcelain)" ]; then
    echo "[build_poc] WARN — worktree not fully restored after cleanup:" >&2
    git status --porcelain >&2
  fi
else
  step "7/7 cleanup SKIPPED (--keep-churn)"
fi

ABS_EXE="$(cd "$(dirname "$EXE")" && pwd)/$(basename "$EXE")"
ABS_POC_CAP="$(cd "$POC_CAP_DIR" && pwd)"
echo ""
echo "============================================================"
echo " POC SOAK READY — hand this to the Sponsor (double-click to run)"
echo "============================================================"
echo "  exe          : $ABS_EXE"
echo "  HUD stamp    : BUILD zoned | <UTC> | $HEAD_SHA   (verify top-right before judging)"
echo "  perf/silh.   : $ABS_POC_CAP/poc_*.png  +  $LOG_DIR/verify-poc.log"
echo "  head sha     : $HEAD_SHA"
echo "------------------------------------------------------------"
echo "  Walk-soak: launch the exe, confirm the HUD stamp reads $HEAD_SHA,"
echo "  then walk the island (feels big ~2-3 min cross?), climb the snow"
echo "  mountain (a real climbable hill?), and watch the frame rate (60fps?)."
echo "============================================================"
