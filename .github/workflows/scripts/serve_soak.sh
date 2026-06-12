#!/usr/bin/env bash
# serve_soak.sh — ONE-command repeatable Sponsor-soak handoff (ticket 86ca86gde).
#
# The Sponsor-soak handoff used to be served by hand: run BuildWindows, zip the
# exe, send it. That path hit the stale-stamp trap on today's first soak — served
# off main 28d9de7 but the SHIPPED HUD stamp read an OLDER committed sha, so the
# Sponsor could not tell which build they were running. Root cause
# (unity-conventions.md §Headless/CLI): BuildWindows does NOT regenerate
# Assets/Resources/BuildStamp.txt; ONLY BootstrapProject.Run (WriteBuildStamp)
# writes it, and the stamp is COMMITTED, so it is ALWAYS stale vs a later HEAD.
#
# This script makes that incident impossible. From a clean checkout at the commit
# you want to soak, it chains:
#   1. BootstrapProject.Run      — freshly stamps HEAD into BuildStamp.txt
#   2. FarHorizonBuilder.BuildWindows — builds Build/Windows/FarHorizon.exe
#   3. verify_build_stamp.py     — FAILS LOUD unless the shipped stamp's sha == HEAD
#   4. capture_gate.sh           — captures N real frames from the SHIPPED exe
#                                  (reuses U7's gate; frame_check.py fails on
#                                  black/empty/uniform/magenta — no duplication)
#   5. cleanup                   — discards the bootstrap's tracked-asset churn so
#                                  the worktree is left exactly as found
# then prints the handoff block: the exact exe path + the HUD build stamp the
# Sponsor must see + the capture dir. Fail loud at every step — a soak the
# Sponsor can't trust is worse than no soak.
#
# Usage:
#   .github/workflows/scripts/serve_soak.sh [--tag <tag>] [--frames N] [--keep-churn]
#     --tag        stamp tag (default: zoned, matching BootstrapProject)
#     --frames     capture frame count (default 4)
#     --keep-churn skip the bootstrap-churn cleanup (debugging only)
#
# Pre-req: a CLEAN worktree at the commit to soak (the script refuses a dirty tree
# so cleanup can't clobber your uncommitted work). Unity must be on this machine.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"   # scripts/ -> workflows/ -> .github/ -> repo root
cd "$ROOT"

TAG="zoned"
FRAMES=4
KEEP_CHURN=0
while [ $# -gt 0 ]; do
  case "$1" in
    --tag) TAG="$2"; shift 2 ;;
    --frames) FRAMES="$2"; shift 2 ;;
    --keep-churn) KEEP_CHURN=1; shift ;;
    *) echo "[serve_soak] unknown arg: $1" >&2; exit 2 ;;
  esac
done

UNITY="${UNITY:-/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe}"
EXE="Build/Windows/FarHorizon.exe"
STAMP="Assets/Resources/BuildStamp.txt"
SOAK_DIR="ci-out/soak"        # gitignored (ci-out/)
CAP_DIR="$SOAK_DIR/caps"
LOG_DIR="$SOAK_DIR/logs"

step() { echo ""; echo "=== [serve_soak] $1 ==="; }
die()  { echo "[serve_soak] FAILED — $1" >&2; exit 1; }

# --- pre-flight ------------------------------------------------------------
[ -x "$UNITY" ] || die "Unity not found at '$UNITY' (set \$UNITY to override)"

# Refuse a dirty worktree: step 5's cleanup restores the tracked assets bootstrap
# rewrites, which would clobber genuine uncommitted edits. Clean checkout only.
if [ -n "$(git status --porcelain)" ]; then
  die "worktree is dirty — soak from a CLEAN checkout (cleanup would clobber your edits). Stash or commit first."
fi

HEAD_SHA="$(git rev-parse --short HEAD)" || die "cannot resolve HEAD sha"
mkdir -p "$CAP_DIR" "$LOG_DIR"

# BootstrapProject.Run rewrites a moving set of tracked assets: the stamp, the URP
# asset + renderer + materials (Assets/Settings), the boot scene, AND — via
# CharacterAssetGen.PrepareCharacter — the Animator controller + FBX import meta
# (Assets/Art/Character) + GraphicsSettings.asset. Enumerating that set by hand is
# fragile (it drifts as bootstrap grows — proven: an initial Settings/Scenes-only
# list left Art/Character + GraphicsSettings churn behind). Because the pre-flight
# REQUIRES a clean tree, ANY tracked modification under these roots after the run
# came from bootstrap, so step 5 restores the whole roots — complete + drift-proof.
CHURN_ROOTS="Assets ProjectSettings"

# --- 1. bootstrap (fresh stamp = HEAD) -------------------------------------
step "1/5 bootstrap (BootstrapProject.Run) — fresh-stamping HEAD $HEAD_SHA"
"$UNITY" -batchmode -quit -nographics \
  -projectPath "$ROOT" \
  -executeMethod FarHorizon.EditorTools.BootstrapProject.Run \
  -logFile "$LOG_DIR/bootstrap.log" \
  || die "bootstrap exited non-zero (see $LOG_DIR/bootstrap.log)"
grep -q '\[BootstrapProject\] complete' "$LOG_DIR/bootstrap.log" \
  || die "bootstrap did not log completion (see $LOG_DIR/bootstrap.log)"

# --- 2. build --------------------------------------------------------------
step "2/5 build (FarHorizonBuilder.BuildWindows)"
"$UNITY" -batchmode -quit -nographics \
  -projectPath "$ROOT" \
  -executeMethod FarHorizon.EditorTools.FarHorizonBuilder.BuildWindows \
  -logFile "$LOG_DIR/build.log" \
  || die "build exited non-zero (see $LOG_DIR/build.log)"
grep -qE '\[FarHorizonBuilder\] result=Succeeded' "$LOG_DIR/build.log" \
  || die "build.log has no 'result=Succeeded' line (see $LOG_DIR/build.log)"
[ -f "$EXE" ] || die "build produced no exe at $EXE"

# --- 3. stamp-vs-HEAD guard (THE stale-stamp backstop) ---------------------
step "3/5 verify shipped stamp matches HEAD $HEAD_SHA"
python3 "$HERE/verify_build_stamp.py" "$STAMP" "$HEAD_SHA" \
  || die "shipped stamp does not match HEAD — refusing to hand a build-ambiguous exe to the Sponsor"

# --- 4. capture from the SHIPPED exe (reuse U7's gate) ---------------------
step "4/5 capture from shipped exe (capture_gate.sh, $FRAMES frames)"
bash "$HERE/capture_gate.sh" "$EXE" "$CAP_DIR" "$FRAMES" \
  || die "capture gate failed — the shipped exe did not render real frames (see $CAP_DIR)"

# --- 5. cleanup bootstrap churn (leave worktree as found) ------------------
if [ "$KEEP_CHURN" -eq 0 ]; then
  step "5/5 cleanup — discarding bootstrap's tracked-asset churn"
  # shellcheck disable=SC2086
  git checkout -- $CHURN_ROOTS 2>/dev/null || true
  # Belt-and-suspenders: the tree must be exactly as found (clean precondition +
  # full-root restore). Any remaining modification is a cleanup escape — warn loud.
  if [ -n "$(git status --porcelain)" ]; then
    echo "[serve_soak] WARN — worktree not fully restored after cleanup:" >&2
    git status --porcelain >&2
  fi
else
  step "5/5 cleanup SKIPPED (--keep-churn)"
fi

# --- handoff block ---------------------------------------------------------
SHIPPED_STAMP="$TAG | <UTC> | $HEAD_SHA"   # tag+sha known; UTC is in the build
ABS_EXE="$(cd "$(dirname "$EXE")" && pwd)/$(basename "$EXE")"
ABS_CAP="$(cd "$CAP_DIR" && pwd)"
echo ""
echo "============================================================"
echo " SOAK READY — hand this to the Sponsor (double-click to run)"
echo "============================================================"
echo "  exe        : $ABS_EXE"
echo "  HUD stamp  : BUILD $TAG | <UTC> | $HEAD_SHA   (verify this in the top-right before judging)"
echo "  captures   : $ABS_CAP/capture_*.png"
echo "  head sha   : $HEAD_SHA"
echo "------------------------------------------------------------"
echo "  Soak protocol: launch the exe, confirm the top-right HUD stamp"
echo "  reads sha $HEAD_SHA, THEN judge. A mismatched sha = wrong build;"
echo "  re-serve via this script (never a bare BuildWindows)."
echo "============================================================"
