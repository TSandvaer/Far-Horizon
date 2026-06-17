#!/usr/bin/env bash
# clean_packagecache_tmp.sh — pre-bootstrap PackageCache hygiene for the self-hosted
# Unity CI runner (ticket 86caahtbe).
#
# WHY: the self-hosted runner reuses its workspace between runs, so
# Library/PackageCache PERSISTS (it is .gitignored — actions/checkout does not wipe
# it). When two Unity instances touch the cache at once (a CI build racing a local
# serve_soak / -verify build, or a superseded-then-cancelled CI run), the package
# resolver leaves STRANDED `.tmp-*` staging dirs in Library/PackageCache. The
# resolver then does an atomic RENAME of a fresh `.tmp-*` ONTO a locked/stranded
# target dir — that rename is the EPERM bite-point (`com.unity.shadergraph`,
# `com.unity.modules.terrainphysics` observed; unity-conventions.md §Headless +
# §Process notes). Deleting the stranded `.tmp-*` BEFORE resolve removes the locked
# target so the next rename succeeds.
#
# SAFE / IDEMPOTENT by design:
#   * Only touches Library/PackageCache/.tmp-* — never resolved package dirs, never
#     anything tracked. PackageCache is fully regenerable from Packages/manifest.json.
#   * No-op when no .tmp-* dirs exist (the common warm-cache case).
#   * Never fails the build: a delete error (e.g. a *currently*-held lock from a
#     genuinely concurrent build) is logged and tolerated — the bootstrap step is
#     still the authoritative gate. We clean what we can; we do not abort CI on a
#     housekeeping miss.
#
# Run from the repo root (the CI workspace). Bash on the Windows self-hosted runner
# (Git-for-Windows bash is available; the other gate scripts use `shell: bash`).
set -u

PC="Library/PackageCache"

if [ ! -d "$PC" ]; then
  echo "[pc-tmp-clean] no $PC dir yet (cold/first run) — nothing to clean"
  exit 0
fi

# Enumerate stranded staging dirs. nullglob so the loop is a clean no-op when none.
shopt -s nullglob
tmp_dirs=( "$PC"/.tmp-* )
shopt -u nullglob

if [ "${#tmp_dirs[@]}" -eq 0 ]; then
  echo "[pc-tmp-clean] no stranded $PC/.tmp-* dirs — cache clean"
  exit 0
fi

echo "[pc-tmp-clean] found ${#tmp_dirs[@]} stranded staging dir(s); removing:"
removed=0
failed=0
for d in "${tmp_dirs[@]}"; do
  echo "  - $d"
  if rm -rf "$d" 2>/dev/null; then
    removed=$((removed + 1))
  else
    # A live lock (genuinely concurrent build) — tolerate; bootstrap is the gate.
    echo "    [warn] could not remove $d (may be locked by a concurrent build) — tolerated"
    failed=$((failed + 1))
  fi
done

echo "[pc-tmp-clean] done: removed=$removed tolerated=$failed"
exit 0
