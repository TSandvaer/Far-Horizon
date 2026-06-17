#!/usr/bin/env bash
# bootstrap_with_retry.sh — run BootstrapProject.Run with EPERM-aware retries
# (ticket 86caahtbe).
#
# WHY a retry, not just a pre-clean: the PackageCache EPERM-on-rename is NOT only a
# *pre-stranded* `.tmp` problem. The trace on run 27699769706 (cold runner, NO
# concurrent CI build, NO pre-existing Library/PackageCache) still EPERM'd:
#
#   com.unity.test-framework.performance: EPERM: operation not permitted, rename
#   '...\Library\PackageCache\.tmp-62620-Tirt2kz536qX\package'
#   -> '...\Library\PackageCache\com.unity.test-framework.performance@aa81a99c4a75'
#
# i.e. the resolver CREATES the `.tmp-*` during the run, then its atomic rename onto
# the (cold, not-yet-existing) target is blocked — a lingering file handle from a
# just-killed/cancelled Unity resolver (run 27699120969 was cancelled mid-resolve
# ~65s before this run started), or an AV/Search-indexer touching the freshly-written
# tree, holds the destination path "delete-pending" on NTFS. A PRE-bootstrap clean
# cannot remove a `.tmp` that doesn't exist yet, so the recurrence needs an in-run
# RETRY: on an EPERM-rename failure, nuke the (regenerable) partial PackageCache so
# the next attempt re-resolves from clean, wait for handles to settle, and re-run.
# The doc's "warm re-run clears it" pattern, automated.
#
# Usage: bootstrap_with_retry.sh <unity-exe> <project-path> <log-file>
# Exits non-zero only if bootstrap still fails after all attempts (the build gate).

set -u

UNITY="${1:?unity exe path required}"
PROJECT="${2:?project path required}"
LOG="${3:?log file path required}"

MAX_ATTEMPTS=3        # 1 initial + 2 retries
SETTLE_SECONDS=20     # let a lingering resolver handle / indexer release the path
PC="Library/PackageCache"

run_bootstrap() {
  # -quit so the editor exits; exit code is advisory (we also scan the log).
  "$UNITY" -batchmode -quit -nographics \
    -projectPath "$PROJECT" \
    -executeMethod FarHorizon.EditorTools.BootstrapProject.Run \
    -logFile "$LOG"
}

# The exact failure signature we retry on (EPERM during package rename). Any OTHER
# bootstrap failure (compile error, missing method, real exception) is NOT retried —
# retrying those just wastes the runner; they fail fast and surface normally.
is_eperm_rename() {
  [ -f "$LOG" ] && grep -qE "EPERM: operation not permitted, rename .*PackageCache" "$LOG"
}

attempt=1
while [ "$attempt" -le "$MAX_ATTEMPTS" ]; do
  echo "[bootstrap-retry] attempt $attempt/$MAX_ATTEMPTS"
  if run_bootstrap; then
    echo "[bootstrap-retry] bootstrap succeeded on attempt $attempt"
    exit 0
  fi

  rc=$?
  echo "[bootstrap-retry] attempt $attempt exited rc=$rc"

  if ! is_eperm_rename; then
    echo "[bootstrap-retry] failure is NOT a PackageCache EPERM-rename — not retrying (real error; see log)"
    exit "$rc"
  fi

  if [ "$attempt" -ge "$MAX_ATTEMPTS" ]; then
    echo "[bootstrap-retry] EPERM persisted through $MAX_ATTEMPTS attempts — giving up (runner cache may be hard-wedged; manual Library/PackageCache delete needed — unity-conventions.md §Process notes)" >&2
    exit "$rc"
  fi

  echo "[bootstrap-retry] EPERM-rename detected — deleting regenerable $PC and settling ${SETTLE_SECONDS}s before retry"
  # PackageCache is fully regenerable from Packages/manifest.json. Removing it on an
  # EPERM clears the half-written/locked target so the re-resolve starts clean.
  rm -rf "$PC" 2>/dev/null || echo "[bootstrap-retry] (partial $PC removal — some handles may still be held; the settle wait covers this)"
  sleep "$SETTLE_SECONDS"
  attempt=$((attempt + 1))
done

# Unreachable, but be explicit.
exit 1
