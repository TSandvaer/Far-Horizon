#!/usr/bin/env bash
# clean_scriptassemblies.sh — targeted regenerable-clean of the warm runner's compiled
# script + build caches, WITHOUT the blanket clean:true that would kill the warm-build win
# (ticket 86cagr0zu — the detect-and-prevent half of the corrupt-build canary).
#
# WHY: a corrupt warm build (check_corrupt_build.sh fires) comes from a STALE / partially-
# written Library/ScriptAssemblies left by a concurrent/cancelled prior build. A bare
# `gh run rerun` on the SAME warm runner keeps failing because Unity's warm-cache mtime
# heuristic thinks the stale DLLs are current and skips the recompile. Deleting ONLY the
# regenerable compiled-script + build-backend caches forces a fresh recompile on the next
# bootstrap so the re-run HEALS, while the expensive Library/PackageCache (the whole point
# of clean:false — unity-conventions.md §Process notes, ticket 86caahtbe) stays WARM.
#
# WHAT IT TOUCHES (all fully regenerable, none tracked, none the package cache):
#   * Library/ScriptAssemblies  — the compiled Assembly-CSharp / FarHorizon.*.dll set (the
#                                 stale/partial DLLs that cause the serialization mismatch).
#   * Library/Bee               — the Bee build-backend intermediate (a partial build leaves
#                                 stale artifacts here; drop it so the rebuild is clean).
# It NEVER touches Library/PackageCache (warm-build win) or anything tracked.
#
# SAFE / IDEMPOTENT by design (mirrors clean_packagecache_tmp.sh):
#   * No-op when the dirs are absent (cold/first run, or already cleaned).
#   * NEVER fails the build: a delete error (e.g. a live lock) is logged + tolerated — the
#     corrupt-build canary already failed the run RED; this is best-effort recovery for the
#     NEXT run, not itself a gate.
#
# Run from the repo root (the CI workspace); the project root defaults to `.`.
# Usage: clean_scriptassemblies.sh [<project-root>]
set -u

ROOT="${1:-.}"

removed=0
tolerated=0
missing=0
for sub in "Library/ScriptAssemblies" "Library/Bee"; do
  target="$ROOT/$sub"
  if [ ! -e "$target" ]; then
    echo "[sa-clean] (skip) not present: $target"
    missing=$((missing + 1))
    continue
  fi
  echo "[sa-clean] removing regenerable cache: $target"
  if rm -rf "$target" 2>/dev/null; then
    removed=$((removed + 1))
  else
    echo "  [warn] could not remove $target (may be locked) — tolerated; the re-run may still be cold" >&2
    tolerated=$((tolerated + 1))
  fi
done

echo "[sa-clean] done: removed=$removed tolerated=$tolerated absent=$missing (PackageCache left WARM)"
exit 0
