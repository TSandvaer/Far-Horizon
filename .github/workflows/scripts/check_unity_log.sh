#!/usr/bin/env bash
# check_unity_log.sh — zero-error gate on a Unity batchmode log, with the
# URP first-import warning allowlist.
#
# unity-conventions.md § "Build stripping & shaders": the URP package emits
# `Terrain Standard 4 Layers URP` shader-dependency warnings on first import —
# package-internal noise, not project errors. A "zero console errors" gate MUST
# allowlist them or every cold-import CI run goes red on package noise.
#
# This gate fails on genuine compile errors (`error CS####`), Unity
# `Compilation failed`, and any uncaught exception markers — but NOT on the
# allowlisted URP/terrain shader-dependency warnings.
#
# Usage: check_unity_log.sh <unity.log> [<unity.log> ...]
set -uo pipefail

[ "$#" -ge 1 ] || { echo "usage: check_unity_log.sh <log> [<log> ...]"; exit 2; }

# Lines matching ANY of these are package-internal noise → ignored.
ALLOWLIST='Terrain Standard 4 Layers URP|Couldn'"'"'t find preset .Terrain|shader.*Terrain Standard 4 Layers'

fail=0
for log in "$@"; do
  if [ ! -f "$log" ]; then
    echo "[check_unity_log] WARN: log not found: $log"
    continue
  fi
  echo "=== scanning $log ==="

  # Real compile errors / compilation failures / fatal exceptions.
  errors=$(grep -nE 'error CS[0-9]+|Compilation failed|Fatal error|Unhandled exception' "$log" \
           | grep -vE "$ALLOWLIST" || true)
  if [ -n "$errors" ]; then
    echo "[check_unity_log] COMPILE/FATAL errors in $log:"
    printf '%s\n' "$errors" | sed 's/^/    /'
    fail=1
  fi

  # Report (but do not fail on) any allowlisted lines so the allowlist stays auditable.
  allowed=$(grep -nE "$ALLOWLIST" "$log" || true)
  if [ -n "$allowed" ]; then
    n=$(printf '%s\n' "$allowed" | grep -c . )
    echo "[check_unity_log] allowlisted URP/terrain first-import warning lines: $n (ignored)"
  fi
done

if [ "$fail" -ne 0 ]; then
  echo "[check_unity_log] LOG GATE FAILED — real errors present"
  exit 1
fi
echo "[check_unity_log] LOG GATE PASSED (no non-allowlisted errors)"
