#!/usr/bin/env bash
# check_corrupt_build.sh — CORRUPT-BUILD canary for the warm (clean:false) self-hosted
# runner (ticket 86cagr0zu).
#
# THE BUG CLASS (OBSERVED, #197 crouch v5, 2026-06-30 — unity-conventions.md §Process
# notes): the warm runner keeps its gitignored Library/ between runs. A concurrent or
# cancelled prior build can leave a PARTIALLY-WRITTEN Library / STALE ScriptAssemblies.
# Unity then loads the scene against a script-layout that no longer matches the compiled
# assembly and emits SERIALIZATION-MISMATCH ("WasdMovement Read 84 expected 88") /
# MISSING-SCRIPT / BROKEN-ASSEMBLY console lines — and ships a build where WASD / NavMesh /
# runtime systems are INERT at play. `artifact-exists != build-good`: the exe uploads fine
# but is broken.
#
# WHY THE EXISTING GATES MISS IT:
#   * EditMode + peer review run in the EDITOR (fresh domain), so they PASS on a corrupt
#     shipped build — they are NOT canaries for this.
#   * check_unity_log.sh (the console-error gate) deliberately fails ONLY on
#     `error CS####` / `Compilation failed` / `Fatal` / `Unhandled exception`. A
#     serialization-layout line is a WARNING, so it slips straight through; and the
#     `no valid NavMesh` line is ALLOWLISTED there as the benign recovered first-frame
#     race — so that gate cannot (and must not) be the corrupt-build detector.
#   * The only signal on #197 was a windowed capture gate going RED, which was WRONGLY
#     dismissed as a launch flake.
#
# THIS SCRIPT closes that gap: it scans Unity logs for the corruption SIGNATURES a healthy
# bootstrap+build never emits (a fresh bootstrap re-bakes Boot.unity from current code +
# recompiles, so a healthy run never mismatches). It is wired into ci.yml TWICE:
#   * BUILD job (build-time logs: bootstrap.log / editmode.log / build.log) — catches the
#     mismatch at scene-load, headlessly, on ANY runner, BEFORE the expensive serial
#     capture job; the build job then targeted-cleans ScriptAssemblies so the re-run heals.
#   * CAPTURE job (runtime player logs: capture.log / verify-*.log) — ATTRIBUTES a red
#     capture gate to a corrupt build ("re-run clean") instead of a dismissed flake, and
#     catches the case where corruption only manifests once the exe actually runs.
#
# FALSE-POSITIVE SAFETY: the signatures are corruption-only wordings; the known-benign CI
# console lines (URP first-import terrain warnings, "Couldn't find preset Terrain", the
# recovered `no valid NavMesh` race) do NOT match, so a healthy run is never flagged.
# Guarded by tests/scripts/test_gate_scripts.sh (the #197 literal + benign + clean cases).
#
# Usage: check_corrupt_build.sh <log> [<log> ...]
#   Exit 0 = no corruption signatures (or a missing/empty log — a missing log is not a
#            corruption signal; the producing step's own gate covers absence).
#   Exit 1 = a corrupt-build signature was found (NAMED verdict + the matching lines).
#   Exit 2 = usage error.
set -uo pipefail

[ "$#" -ge 1 ] || { echo "usage: check_corrupt_build.sh <log> [<log> ...]" >&2; exit 2; }

# CORRUPT SIGNATURES — a hit on ANY of these = a stale-Library / partial-ScriptAssemblies
# corrupt build. Each wording is emitted by Unity ONLY on a serialization-layout /
# missing-script / broken-assembly condition; a healthy bootstrap+build never emits them.
#   1. Serialization byte/layout mismatch — the #197 tell ("... Read 84 expected 88").
#      The gap is bounded (.{0,40}) so it stays anchored to the "Read N ... expected M"
#      shape and cannot drift into unrelated prose that happens to contain both words.
#   2. Explicit "different serialization layout when loading" phrasing.
#   3. Missing MonoBehaviour script reference (the compiled type the scene points at is
#      gone from the stale assembly).
#   4. A broken/unloadable managed assembly (a partially-written ScriptAssemblies DLL).
CORRUPT_RE='[Rr]ead [0-9]+ .{0,40}[Ee]xpected [0-9]+|different serialization layout|referenced script.*is missing|has a missing script|Unloading broken assembly|TypeLoadException|Could not load (type|signature)'

fail=0
for log in "$@"; do
  if [ ! -f "$log" ]; then
    # A missing log is NOT a corruption signal — the producing step's own gate covers
    # absence (e.g. the build-result gate fails if build.log has no result=Succeeded).
    echo "[corrupt-build] (skip) log not present: $log"
    continue
  fi
  echo "=== corrupt-build scan: $log ==="
  hits=$(grep -nE "$CORRUPT_RE" "$log" || true)
  if [ -n "$hits" ]; then
    echo "[corrupt-build] CORRUPT BUILD DETECTED in $log — stale Library / ScriptAssemblies signature(s):"
    printf '%s\n' "$hits" | sed 's/^/    /'
    fail=1
  fi
done

if [ "$fail" -ne 0 ]; then
  echo "[corrupt-build] ------------------------------------------------------------------"
  echo "[corrupt-build] This is a CORRUPT WARM-RUNNER BUILD, NOT a launch flake (86cagr0zu)."
  echo "[corrupt-build] Do NOT serve a soak off this build — WASD / NavMesh / runtime systems"
  echo "[corrupt-build] can be INERT even though the exe rendered a frame."
  echo "[corrupt-build] Recovery: the BUILD job targeted-cleans Library/ScriptAssemblies + Bee"
  echo "[corrupt-build] on this failure so the re-run recompiles fresh (PackageCache stays warm)."
  echo "[corrupt-build] If it RECURS, delete the runner's Library/ or set clean:true for ONE run."
  echo "[corrupt-build] See unity-conventions.md §Process notes + ticket 86cagr0zu."
  echo "[corrupt-build] CORRUPT-BUILD GATE FAILED"
  exit 1
fi
echo "[corrupt-build] CORRUPT-BUILD GATE PASSED (no stale-assembly / serialization-mismatch signatures)"
