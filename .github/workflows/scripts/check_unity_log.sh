#!/usr/bin/env bash
# check_unity_log.sh — zero-error gate on a Unity batchmode log, with a tightly
# scoped allowlist of known-benign console lines.
#
# unity-conventions.md § "Build stripping & shaders": the URP package emits
# `Terrain Standard 4 Layers URP` shader-dependency warnings on first import —
# package-internal noise, not project errors. A "zero console errors" gate MUST
# allowlist them or every cold-import CI run goes red on package noise.
#
# unity-conventions.md § "NavMesh" + PR #3 (U3): the standalone player logs one
# `Failed to create agent because there is no valid NavMesh` line on the first
# frame — a documented NavMeshSurface.OnEnable-after-agent-create init-order race
# that ClickToMove.EnsureOnNavMesh recovers from the same frame. It is benign +
# recovered, so the console gate allowlists it; otherwise every boot false-fails.
#
# This gate fails on genuine compile errors (`error CS####`), Unity
# `Compilation failed`, and any uncaught exception markers.
#
# --------------------------------------------------------------------------
# NIT-1 FIX (Tess, PR #2 review): the allowlist is NO LONGER subtracted from the
# error-detection scan. The previous shape `grep '<errors>' | grep -v '<allow>'`
# meant a real `error CS####` line that ALSO contained an allowlisted substring
# (e.g. `error CS0246: Terrain Standard 4 Layers URP shader type missing`) was
# silently dropped → false PASS (Tess reproduced this). The fix splits concerns:
#   * the ERROR scan (error CS / Compilation failed / Fatal / Unhandled exception)
#     decides pass/fail and is NEVER filtered through the allowlist — a real error
#     is always caught, even when the line also mentions an allowlisted phrase;
#   * the allowlist recognises + counts the known-benign lines for the audit
#     print only. It cannot suppress an error line.
# Whitelist-by-substring on the same pass that detects errors is the anti-pattern
# Tess flagged; this script no longer does it.
#
# Usage: check_unity_log.sh <unity.log> [<unity.log> ...]
set -uo pipefail

[ "$#" -ge 1 ] || { echo "usage: check_unity_log.sh <log> [<log> ...]"; exit 2; }

# Error markers — a hit on ANY of these fails the gate. This is matched against
# the FULL log and is NEVER filtered through the benign list below.
ERROR_RE='error CS[0-9]+|Compilation failed|Fatal error|Unhandled exception'

# Known-benign console lines, anchored to their recognisable SHAPE (not a bare
# substring), reported in the audit print only:
#   1. URP first-import terrain shader-dependency warnings (package noise).
#   2. The "couldn't find preset ... Terrain" companion line.
#   3. The recovered NavMesh init-order race (PR #3 nit 1).
BENIGN_RE='Terrain Standard 4 Layers URP|Couldn'"'"'t find preset.*Terrain|Failed to create agent because there is no valid NavMesh'

fail=0
for log in "$@"; do
  if [ ! -f "$log" ]; then
    echo "[check_unity_log] WARN: log not found: $log"
    continue
  fi
  echo "=== scanning $log ==="

  # ---- ERROR scan: NO allowlist subtraction. A real error is always caught. ----
  errors=$(grep -nE "$ERROR_RE" "$log" || true)
  if [ -n "$errors" ]; then
    echo "[check_unity_log] COMPILE/FATAL errors in $log:"
    printf '%s\n' "$errors" | sed 's/^/    /'
    fail=1
  fi

  # ---- Audit print: count benign lines that are NOT themselves error lines, so
  #      the allowlist stays visible. Never used to suppress the error scan. ----
  benign=$(grep -nE "$BENIGN_RE" "$log" | grep -vE "$ERROR_RE" || true)
  if [ -n "$benign" ]; then
    n=$(printf '%s\n' "$benign" | grep -c .)
    echo "[check_unity_log] allowlisted benign console lines (URP first-import / recovered NavMesh race): $n (ignored)"
  fi
done

if [ "$fail" -ne 0 ]; then
  echo "[check_unity_log] LOG GATE FAILED — real errors present"
  exit 1
fi
echo "[check_unity_log] LOG GATE PASSED (no non-allowlisted errors)"
