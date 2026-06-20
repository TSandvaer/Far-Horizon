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

MAX_ATTEMPTS=3                       # 1 initial + 2 retries
SETTLE_SECONDS="${SETTLE_SECONDS:-20}"  # let a lingering resolver handle / indexer release the path (env-overridable for tests)
PC="Library/PackageCache"

run_bootstrap() {
  # -quit so the editor exits; exit code is advisory (we also scan the log).
  "$UNITY" -batchmode -quit -nographics \
    -projectPath "$PROJECT" \
    -executeMethod FarHorizon.EditorTools.BootstrapProject.Run \
    -logFile "$LOG"
}

# SUCCESS is gated on the COMPLETION MARKER, not on Unity's advisory -quit exit code.
# BootstrapProject.Run logs "[BootstrapProject] complete" ONLY after it has authored +
# RE-SAVED Boot.unity end-to-end (BootstrapProject.cs:108, just before EditorApplication
# .Exit(0)). If that line is absent, Run() did NOT finish — the scene was NOT re-baked —
# regardless of what the editor's exit code says.
#
# WHY this gate exists (ticket 86cabtc83, editor 6000.4.11f1 upgrade): on the COLD-cache
# first run of the NEW editor, package resolution was CANCELLED before Run() executed at
# all — the log ended in `[Package Manager] Failed to resolve packages: operation
# cancelled.` (+ `IPCStream (Upm-...): IPC stream failed to read (Not connected)`), Run()
# never started, and yet the wrapper reported SUCCESS off the advisory exit code. EditMode
# then opened the STALE COMMITTED Boot.unity (PR #77 era — no HungerNeed/AxePickup/
# InventoryUI/CameraFollowNudgeTool, followLerp=12) → 6 false-RED scene-presence failures
# that looked like an upgrade regression but were a non-re-baked-scene artifact. The
# warm-cache bootstrap on the SAME editor authored everything + logged "complete" (proof
# the bootstrap CODE is fine on 6000.4.11f1). Gating on the marker closes the silent-green
# hole for this transient AND any future one. See unity-conventions.md §Headless / CLI.
bootstrap_completed() {
  [ -f "$LOG" ] && grep -qF "[BootstrapProject] complete" "$LOG"
}

# Transient PackageCache / package-resolve failures we RETRY on (a fresh attempt on a
# cleaned cache clears them). Any OTHER bootstrap failure (compile error, missing method,
# real exception) is NOT retried — retrying those just wastes the runner; they fail fast
# and surface normally.
#   (1) EPERM during package rename (the original signature — ticket 86caahtbe).
#   (2) Package-resolve CANCELLED / UPM IPC drop (the 6000.4.11f1 cold-cache signature —
#       ticket 86cabtc83). Distinct string from EPERM, so the EPERM-only guard missed it.
is_transient_pkgcache_failure() {
  [ -f "$LOG" ] && grep -qE "EPERM: operation not permitted, rename .*PackageCache|Failed to resolve packages: operation cancelled|IPC stream failed to read \(Not connected\)" "$LOG"
}

attempt=1
while [ "$attempt" -le "$MAX_ATTEMPTS" ]; do
  echo "[bootstrap-retry] attempt $attempt/$MAX_ATTEMPTS"
  run_bootstrap
  rc=$?
  echo "[bootstrap-retry] attempt $attempt exited rc=$rc"

  # SUCCESS = the completion marker is present (Run() re-baked the scene end-to-end).
  # The advisory exit code alone is NOT trusted — a cancelled cold package-resolve let
  # the editor exit 0 without ever running Run() (86cabtc83).
  if bootstrap_completed; then
    echo "[bootstrap-retry] bootstrap COMPLETE (re-baked Boot.unity) on attempt $attempt"
    exit 0
  fi
  echo "[bootstrap-retry] no '[BootstrapProject] complete' marker — Run() did NOT finish (scene NOT re-baked)"

  # No completion marker = bootstrap did NOT re-bake the scene. We must exit NON-ZERO
  # even when Unity's advisory exit code was 0 (the 86cabtc83 cold-cancel let it exit 0
  # without running Run()). Normalize a 0 advisory code to 1 so the CI step goes RED.
  fail_rc="$rc"; [ "$fail_rc" -eq 0 ] && fail_rc=1

  if ! is_transient_pkgcache_failure; then
    echo "[bootstrap-retry] failure is NOT a transient PackageCache/resolve flake — not retrying (real error; see log)"
    exit "$fail_rc"
  fi

  if [ "$attempt" -ge "$MAX_ATTEMPTS" ]; then
    echo "[bootstrap-retry] transient PackageCache/resolve flake persisted through $MAX_ATTEMPTS attempts — giving up (runner cache may be hard-wedged; manual Library/PackageCache delete needed — unity-conventions.md §Process notes)" >&2
    exit "$fail_rc"
  fi

  echo "[bootstrap-retry] transient PackageCache/resolve flake detected — deleting regenerable $PC and settling ${SETTLE_SECONDS}s before retry"
  # PackageCache is fully regenerable from Packages/manifest.json. Removing it on a
  # transient flake clears any half-written/locked target so the re-resolve starts clean.
  rm -rf "$PC" 2>/dev/null || echo "[bootstrap-retry] (partial $PC removal — some handles may still be held; the settle wait covers this)"
  sleep "$SETTLE_SECONDS"
  attempt=$((attempt + 1))
done

# Unreachable, but be explicit.
exit 1
