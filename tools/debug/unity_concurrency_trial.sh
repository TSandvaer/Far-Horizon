#!/usr/bin/env bash
# unity_concurrency_trial.sh — controlled A/B harness for the "can two Unity builds run
# at once on this machine?" question (spike ticket 86cabkhjg).
#
# WHY this exists: the project caps Unity-build concurrency at 1 because of the
# `EPERM: operation not permitted, rename ...Library\PackageCache\.tmp-*` bootstrap
# failure. That cap serialises the whole orchestration. Erik's research
# (team/erik-consult/concurrent-unity-build-isolation-research.md) proposed per-instance
# UPM/Bee cache isolation as the fix. This harness makes the claim FALSIFIABLE: it runs
# the same concurrent-bootstrap workload under three conditions and diffs the outcomes.
#
#   default   — TWO concurrent runs with NO cache env vars at all. This is the exact
#               PRODUCTION condition two personas hit today (both share the real
#               %LOCALAPPDATA%\Unity\cache\upm\db store, including its `tmp` dir, and
#               the real %LOCALAPPDATA%\Unity\Caches\bee). This is the FAILURE-STATE
#               leg the spike must reproduce before any fix can be called a fix.
#   shared    — TWO concurrent runs both pointed at ONE *cold* throwaway UPM + Bee
#               root. Structurally the same sharing as `default` but with maximum
#               write contention (every package must be fetched + written, so both
#               instances are writing the shared store simultaneously rather than
#               reading a warm one).
#   isolated  — TWO concurrent runs, each with its OWN UPM_CACHE_ROOT +
#               UPM_CACHE_PATH + UPM_NPM_CACHE_PATH + BEE_CACHE_DIRECTORY. The
#               proposed-fix leg.
#   solo      — ONE run, cold throwaway cache. The control leg (throughput baseline).
#
# Every leg starts from a COLD per-project `Library/PackageCache` (deleted before
# launch) because that is the directory the EPERM rename targets — a warm PackageCache
# skips package resolution entirely and cannot reproduce the bug.
#
# The real user-level global caches (%LOCALAPPDATA%\Unity\cache,
# %LOCALAPPDATA%\Unity\Caches\bee) are NEVER touched: every leg redirects to a
# throwaway root under $SPIKE_ROOT, so the Sponsor's warm caches survive the spike.
#
# Cache roots live OUTSIDE both worktrees so `serve_soak.sh`'s dirty-worktree guard is
# never tripped and `git status` stays clean.
#
# PHASE selects the workload:
#   bootstrap (default) — BootstrapProject.Run. Resets Library/PackageCache first, so
#                         package RESOLUTION (the EPERM rename site) is exercised.
#   build               — FarHorizonBuilder.BuildWindows. Leaves PackageCache warm and
#                         resets only the Bee cache, because the shared surface a
#                         concurrent BUILD contends is the machine-level Bee cache
#                         (IL2CPP/Mono artifacts), not package resolution.
#
# Usage:
#   tools/debug/unity_concurrency_trial.sh <solo|shared|isolated> <trial-id>
#   PHASE=build tools/debug/unity_concurrency_trial.sh <solo|shared|isolated> <trial-id>
#
# Env overrides: PHASE, UNITY, WT_A, WT_B, WT_A_WIN, WT_B_WIN, SPIKE_ROOT, SPIKE_ROOT_WIN.
#
# Output: $SPIKE_ROOT/logs/<mode>-<trial>-{A,B}.log plus a one-line verdict per
# instance on stdout (grep-able: `[trial]`).

set -u

MODE="${1:?mode required: solo|shared|isolated}"
TRIAL="${2:?trial id required (e.g. 1)}"
PHASE="${PHASE:-bootstrap}"
case "$PHASE" in
  bootstrap) EXEC_METHOD="FarHorizon.EditorTools.BootstrapProject.Run" ;;
  build)     EXEC_METHOD="FarHorizon.EditorTools.FarHorizonBuilder.BuildWindows" ;;
  *) echo "[trial] unknown PHASE '$PHASE' (bootstrap|build)" >&2; exit 2 ;;
esac

UNITY="${UNITY:-C:\\Program Files\\Unity\\Hub\\Editor\\6000.4.11f1\\Editor\\Unity.exe}"

# POSIX paths (for rm/ls/grep) and Windows paths (for Unity.exe args + env values).
WT_A="${WT_A:-/c/Trunk/PRIVATE/Far-Horizon-drew-conc-a-wt}"
WT_B="${WT_B:-/c/Trunk/PRIVATE/Far-Horizon-drew-conc-b-wt}"
WT_A_WIN="${WT_A_WIN:-C:\\Trunk\\PRIVATE\\Far-Horizon-drew-conc-a-wt}"
WT_B_WIN="${WT_B_WIN:-C:\\Trunk\\PRIVATE\\Far-Horizon-drew-conc-b-wt}"

# Short path on purpose: the UPM cache is a deep content-addressed tree and a long root
# invites MAX_PATH truncation, which would confound the result with a path-length bug.
SPIKE_ROOT="${SPIKE_ROOT:-/c/Users/538252/AppData/Local/Temp/fh-conc}"
SPIKE_ROOT_WIN="${SPIKE_ROOT_WIN:-C:\\Users\\538252\\AppData\\Local\\Temp\\fh-conc}"

LOGS="$SPIKE_ROOT/logs"
mkdir -p "$LOGS"

LOG_A="$LOGS/${PHASE}-${MODE}-${TRIAL}-A.log"
LOG_B="$LOGS/${PHASE}-${MODE}-${TRIAL}-B.log"
LOG_A_WIN="$SPIKE_ROOT_WIN\\logs\\${PHASE}-${MODE}-${TRIAL}-A.log"
LOG_B_WIN="$SPIKE_ROOT_WIN\\logs\\${PHASE}-${MODE}-${TRIAL}-B.log"

# ---------------------------------------------------------------------------
# Cache-root assignment — the ONLY variable that differs between shared/isolated.
# ---------------------------------------------------------------------------
case "$MODE" in
  default)
    # Empty => launch() sets no cache env vars at all; both instances inherit Unity's
    # real user-level defaults. Nothing to pre-clean (we never wipe the real cache).
    UPM_A=""; BEE_A=""; UPM_B=""; BEE_B=""
    CLEAN_DIRS=( )
    ;;
  shared)
    UPM_A="$SPIKE_ROOT_WIN\\c-$TRIAL\\upm";  BEE_A="$SPIKE_ROOT_WIN\\c-$TRIAL\\bee"
    UPM_B="$UPM_A";                          BEE_B="$BEE_A"
    CLEAN_DIRS=( "$SPIKE_ROOT/c-$TRIAL" )
    ;;
  isolated)
    UPM_A="$SPIKE_ROOT_WIN\\a-$TRIAL\\upm";  BEE_A="$SPIKE_ROOT_WIN\\a-$TRIAL\\bee"
    UPM_B="$SPIKE_ROOT_WIN\\b-$TRIAL\\upm";  BEE_B="$SPIKE_ROOT_WIN\\b-$TRIAL\\bee"
    CLEAN_DIRS=( "$SPIKE_ROOT/a-$TRIAL" "$SPIKE_ROOT/b-$TRIAL" )
    ;;
  solo)
    UPM_A="$SPIKE_ROOT_WIN\\s-$TRIAL\\upm";  BEE_A="$SPIKE_ROOT_WIN\\s-$TRIAL\\bee"
    UPM_B=""; BEE_B=""
    CLEAN_DIRS=( "$SPIKE_ROOT/s-$TRIAL" )
    ;;
  *) echo "[trial] unknown mode '$MODE' (default|shared|isolated|solo)" >&2; exit 2 ;;
esac

# ---------------------------------------------------------------------------
# Reset: cold per-project PackageCache + cold throwaway global caches.
# ---------------------------------------------------------------------------
echo "[trial] phase=$PHASE mode=$MODE trial=$TRIAL — resetting to cold state"
if [ "$PHASE" = bootstrap ]; then
  # PackageCache is the EPERM rename site — it MUST be cold or resolution is skipped.
  rm -rf "$WT_A/Library/PackageCache" 2>/dev/null || echo "[trial] (partial PackageCache-A removal)"
  [ "$MODE" = solo ] || rm -rf "$WT_B/Library/PackageCache" 2>/dev/null || echo "[trial] (partial PackageCache-B removal)"
fi
# `default` has no throwaway roots (it uses the REAL user caches, which we never wipe),
# so guard the expansion — an empty array under `set -u` is not portable.
if [ "${#CLEAN_DIRS[@]}" -gt 0 ]; then
  for d in "${CLEAN_DIRS[@]}"; do rm -rf "$d" 2>/dev/null || true; done
fi
rm -f "$LOG_A" "$LOG_B" 2>/dev/null || true

launch() {          # launch <upm-root|""> <bee-dir|""> <project-win> <log-win>
  # Unity reads UPM_CACHE_ROOT / UPM_CACHE_PATH / UPM_NPM_CACHE_PATH /
  # BEE_CACHE_DIRECTORY from the launching process environment (per-launch, not
  # persistent) — hence `env ... Unity.exe` rather than a Hub launch. An EMPTY
  # upm-root means "set nothing" (the `default` leg), NOT "set to empty string" —
  # an empty value would be a third, untested condition.
  if [ -z "$1" ]; then
    "$UNITY" -batchmode -quit -nographics \
      -projectPath "$3" -executeMethod "$EXEC_METHOD" -logFile "$4"
  else
    env UPM_CACHE_ROOT="$1" \
        UPM_CACHE_PATH="$1\\packages" \
        UPM_NPM_CACHE_PATH="$1\\npm" \
        BEE_CACHE_DIRECTORY="$2" \
        "$UNITY" -batchmode -quit -nographics \
          -projectPath "$3" -executeMethod "$EXEC_METHOD" -logFile "$4"
  fi
}

now() { date -u +%H:%M:%S.%3N; }

T0="$(now)"
echo "[trial] $MODE-$TRIAL A launch at $T0  (UPM_CACHE_ROOT=${UPM_A:-<unset: real user cache>})"
launch "$UPM_A" "$BEE_A" "$WT_A_WIN" "$LOG_A_WIN" & PID_A=$!
PID_B=""
if [ "$MODE" != solo ]; then
  echo "[trial] $MODE-$TRIAL B launch at $(now)  (UPM_CACHE_ROOT=${UPM_B:-<unset: real user cache>})"
  launch "$UPM_B" "$BEE_B" "$WT_B_WIN" "$LOG_B_WIN" & PID_B=$!
fi

# Overlap proof: snapshot the live Unity.exe set a few seconds in, so the report can
# quote two concurrent PIDs rather than asserting concurrency from wall-clock alone.
( sleep 25; echo "[trial] overlap-probe at $(now):"; tasklist 2>/dev/null | grep -iE '^Unity\.exe' || echo "  (no Unity.exe seen)" ) &
PROBE=$!

wait "$PID_A"; RC_A=$?
END_A="$(now)"
RC_B=""
if [ -n "$PID_B" ]; then wait "$PID_B"; RC_B=$?; fi
END_B="$(now)"
wait "$PROBE" 2>/dev/null || true

# ---------------------------------------------------------------------------
# Verdict per instance. SUCCESS is gated on the completion MARKER, never on Unity's
# advisory -quit exit code (a cancelled cold package-resolve exits 0 without ever
# running the -executeMethod — ticket 86cabtc83, bootstrap_with_retry.sh).
# ---------------------------------------------------------------------------
verdict() {         # verdict <label> <log> <rc>
  local label="$1" log="$2" rc="$3" mark="MISSING" eperm="-" cancel="-" ipc="-"
  [ -f "$log" ] || { echo "[trial] $label rc=$rc NO-LOG"; return; }
  if [ "$PHASE" = build ]; then
    # FarHorizonBuilder logs `result=<BuildResult> size=<bytes>` (FarHorizonBuilder.cs:67)
    # and Exit(2) + "BUILD FAILED" on anything but Succeeded (:76-77).
    grep -qE "\[FarHorizonBuilder\] result=Succeeded" "$log" && mark="result=Succeeded"
    grep -oE "\[FarHorizonBuilder\] result=[A-Za-z]+ size=[0-9]+ bytes" "$log" | tail -1
  else
    grep -qF "[BootstrapProject] complete" "$log" && mark="complete"
  fi
  grep -qE "EPERM: operation not permitted, rename" "$log" && eperm="EPERM"
  grep -qF "Failed to resolve packages: operation cancelled" "$log" && cancel="RESOLVE-CANCELLED"
  grep -qE "IPC stream failed to read \(Not connected\)" "$log" && ipc="UPM-IPC-DROP"
  echo "[trial] $label rc=$rc marker=$mark eperm=$eperm resolve=$cancel ipc=$ipc log=$log"
}

echo "[trial] --- results: mode=$MODE trial=$TRIAL ---"
echo "[trial] A window ${T0} -> ${END_A}"
verdict "A" "$LOG_A" "$RC_A"
if [ -n "$PID_B" ]; then
  echo "[trial] B window ${T0} -> ${END_B}"
  verdict "B" "$LOG_B" "$RC_B"
fi
if [ "${#CLEAN_DIRS[@]}" -eq 0 ]; then
  echo "[trial] cache roots: none redirected — both instances used the REAL user caches"
else
  echo "[trial] cache roots populated (independent population is the isolation proof):"
  for d in "${CLEAN_DIRS[@]}"; do
    if [ -d "$d" ]; then echo "  $(du -sh "$d" 2>/dev/null | cut -f1)  $d"; else echo "  (absent)  $d"; fi
  done
fi
