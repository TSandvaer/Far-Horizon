#!/usr/bin/env bash
# structure_check.sh — license-free Far Horizon CI gate.
#
# Runs on GitHub-hosted ubuntu (no Unity license required) so EVERY PR gets a
# real required-status-check immediately, independent of the self-hosted Unity
# runner. Validates project-structure invariants that have actually bitten us:
#   - no Unity-generated artifacts committed (Library/Temp/Build/Logs/*.log/test-results*.xml)
#   - every Assets/ asset has a sibling .meta (and vice-versa) — the empty-dir/.meta
#     trap from unity-conventions.md
#   - the four asmdefs + their entry-point .cs files are present and well-formed JSON
#   - the headless entry-point methods referenced by the Unity job still exist by name
#   - Packages/manifest.json parses and pins the editor version we build against
#
# Fails loud (exit 1) with a per-check summary. Zero false positives by design:
# it only asserts on facts the repo controls, never on Unity-import side effects.
set -uo pipefail

fail=0
note() { printf '  %s\n' "$1"; }
ok()   { printf '[ OK ] %s\n' "$1"; }
bad()  { printf '[FAIL] %s\n' "$1"; fail=1; }

echo "=== Far Horizon structure check ==="

# ---------------------------------------------------------------------------
# 1. No Unity-generated artifacts in the tracked index.
#    (.gitignore should already block these; this is the belt-and-suspenders
#    gate so a stray `git add -A` can never land Library/Build/logs in history.)
# ---------------------------------------------------------------------------
artifacts=$(git ls-files \
  | grep -E '^(Library|Temp|obj|Build|Builds|Logs|UserSettings|MemoryCaptures|Recordings|Captures|VerifyCaptures|ci-out)/' \
  || true)
# NUnit result XMLs: the canonical `test-results*.xml` AND the local-run
# `<platform>-results.xml` shape (e.g. editmode-results.xml / playmode-results.xml).
# Both are throwaway verification output, never committed.
logs=$(git ls-files | grep -E '(\.log$|(^|/)test-results.*\.xml$|(^|/)[A-Za-z0-9_-]*-results\.xml$)' || true)

# NUnit result XML by CONTENT, not just by filename (ticket 86cafk5vb).
# The filename-suffix gate above (`-results.xml` / `test-results*.xml`) MISSED a
# stray dump named `editmode-bake176.xml` in PR #177 — it lacked the `-results`
# suffix, so neither this gate nor .gitignore caught it; only human review did.
# Close the class: any ROOT-LEVEL `*.xml` whose head contains the NUnit `<test-run`
# root element is a local `-runTests` dump (its defining marker — see the real
# editmode-results.xml header), throwaway, never committed, REGARDLESS of filename.
# Root-scoped on purpose: ci-out/ + Captures/ are already gitignored + caught by the
# artifacts dir check above, and a genuine project XML (e.g. mono's <mconfig> config.xml)
# lives under a subdir and does not carry the <test-run marker — so this stays
# false-positive-free (the script's design contract).
nunit_xml=""
while IFS= read -r xml; do
  [ -z "$xml" ] && continue
  case "$xml" in */*) continue ;; esac          # root level only (no slash in path)
  [ -f "$xml" ] || continue                       # only inspect files present in the worktree
  if head -c 4096 "$xml" 2>/dev/null | grep -q '<test-run'; then
    nunit_xml="${nunit_xml}${xml}
"
  fi
done < <(git ls-files '*.xml')

if [ -n "$artifacts$logs$nunit_xml" ]; then
  bad "Unity-generated artifacts are committed:"
  printf '%s\n' "$artifacts" "$logs" "$nunit_xml" | grep -v '^$' | sed 's/^/       /'
else
  ok "no Unity-generated artifacts in the index"
fi

# ---------------------------------------------------------------------------
# 2. .meta presence. Every tracked Assets/ asset FILE must have a sibling .meta.
#    A missing .meta is the false-positive-free, high-value direction.
#
#    We deliberately do NOT flag "orphan" .meta files (a .meta whose asset is
#    absent): git cannot track empty directories, so an EMPTY-DIR's .meta is
#    committed WITHOUT its directory ever appearing in the worktree — that is the
#    documented "empty dirs carry .meta so the Assets layout survives commits"
#    invariant (unity-conventions.md), not an orphan. Distinguishing a legit
#    empty-dir meta from a stale deleted-file meta is not reliably possible from
#    git alone (it needs Unity's import), so flagging orphans here produces false
#    positives on exactly the metas the project is required to preserve. The
#    Unity job's actual import is the authoritative orphan-meta detector.
# ---------------------------------------------------------------------------
meta_problems=0
while IFS= read -r asset; do
  case "$asset" in
    *.meta) continue ;;
  esac
  if [ ! -f "${asset}.meta" ] && [ "$(git ls-files "${asset}.meta")" = "" ]; then
    note "missing .meta for: $asset"
    meta_problems=$((meta_problems+1))
  fi
done < <(git ls-files 'Assets/*' | grep -vE '\.meta$')

if [ "$meta_problems" -eq 0 ]; then
  ok "every tracked Assets/ file has its .meta"
else
  bad ".meta presence broken ($meta_problems missing) — see unity-conventions.md"
fi

# ---------------------------------------------------------------------------
# 3. asmdefs present + valid JSON.
# ---------------------------------------------------------------------------
expected_asmdefs=(
  "Assets/Scripts/Runtime/FarHorizon.Runtime.asmdef"
  "Assets/Scripts/Editor/FarHorizon.Editor.asmdef"
  "Assets/Tests/EditMode/FarHorizon.EditTests.asmdef"
  "Assets/Tests/PlayMode/FarHorizon.PlayTests.asmdef"
)
asmdef_ok=1
for a in "${expected_asmdefs[@]}"; do
  if [ ! -f "$a" ]; then
    note "missing asmdef: $a"; asmdef_ok=0; continue
  fi
  if ! python3 -c "import json,sys; json.load(open(sys.argv[1]))" "$a" 2>/dev/null; then
    note "asmdef is not valid JSON: $a"; asmdef_ok=0
  fi
done
if [ "$asmdef_ok" -eq 1 ]; then ok "all 4 asmdefs present + valid JSON"; else bad "asmdef set incomplete/invalid"; fi

# ---------------------------------------------------------------------------
# 4. Headless entry-point methods the Unity job invokes still exist by name.
#    A rename here silently breaks the -executeMethod chain; pin it.
# ---------------------------------------------------------------------------
check_method() {
  local file="$1" sig="$2" label="$3"
  if [ -f "$file" ] && grep -qF "$sig" "$file"; then
    ok "$label present ($file)"
  else
    bad "$label NOT found — '$sig' in $file (the Unity job's -executeMethod chain depends on it)"
  fi
}
check_method "Assets/Scripts/Editor/BootstrapProject.cs"  "public static void Run()"          "BootstrapProject.Run"
check_method "Assets/Scripts/Editor/FarHorizonBuilder.cs" "public static void BuildWindows()" "FarHorizonBuilder.BuildWindows"

# ---------------------------------------------------------------------------
# 5. Packages/manifest.json parses + pins the editor version we expect.
# ---------------------------------------------------------------------------
if python3 -c "import json; json.load(open('Packages/manifest.json'))" 2>/dev/null; then
  ok "Packages/manifest.json parses"
else
  bad "Packages/manifest.json is not valid JSON"
fi
EXPECTED_UNITY="6000.4.11f1"
if grep -qF "m_EditorVersion: ${EXPECTED_UNITY}" ProjectSettings/ProjectVersion.txt; then
  ok "ProjectVersion pinned to ${EXPECTED_UNITY}"
else
  bad "ProjectVersion.txt does not pin ${EXPECTED_UNITY} (self-hosted runner image must match)"
fi

# ---------------------------------------------------------------------------
# 6. CI concurrency invariants (ticket 86caammpq — the merged-branch orphan-hold
#    fix). The single self-hosted runner is orphan-HELD by a merged/superseded
#    branch's run if the runner-contending jobs are ref-scoped: a merge-to-main run
#    is a DIFFERENT ref/group, so it never supersedes/serializes behind the stale
#    PR-branch run, which holds the runner to timeout / forces a manual gh run
#    cancel. The fix: the runner-contending JOBS (build/capture/playmode) use
#    REPO-WIDE QUEUE groups (absolute name, NO `${{ github.ref }}` suffix,
#    cancel-in-progress: false) so all runs across all refs serialize into one
#    bounded queue, no verdict dropped. The TOP-LEVEL group stays REF-SCOPED — it
#    is the same-ref supersede mechanism and holds no runner. Pin the shape here so
#    a future edit can't silently revert it (the "fix it back to ref-scoped" trap
#    the ci.yml comments warn against). Uses Python's YAML if available, else a
#    literal-grep fallback (both license-free on hosted ubuntu).
# ---------------------------------------------------------------------------
CI_YML=".github/workflows/ci.yml"
if [ ! -f "$CI_YML" ]; then
  bad "ci.yml not found at $CI_YML (concurrency-invariant check cannot run)"
elif python3 -c "import yaml" 2>/dev/null; then
  conc_check=$(python3 - "$CI_YML" <<'PYEOF'
import sys, yaml
d = yaml.safe_load(open(sys.argv[1], encoding="utf-8"))
# NB: PyYAML maps the YAML key `on:` to the Python bool True; `concurrency` is fine.
top = d.get("concurrency", {})
jobs = d.get("jobs", {})
errs = []
# Top level: MUST stay ref-scoped + cancel-in-progress (same-ref supersede).
if "${{ github.ref }}" not in str(top.get("group", "")):
    errs.append("top-level concurrency.group must be ref-scoped (same-ref supersede); got %r" % top.get("group"))
if top.get("cancel-in-progress") is not True:
    errs.append("top-level cancel-in-progress must be true (same-ref supersede); got %r" % top.get("cancel-in-progress"))
# Runner-contending jobs: MUST be repo-wide queue (no ref suffix, cancel:false).
for jn in ("build", "capture", "playmode"):
    jc = jobs.get(jn, {}).get("concurrency")
    if not jc:
        errs.append("job %r missing a concurrency group (must be a repo-wide queue — 86caammpq)" % jn)
        continue
    g = str(jc.get("group", ""))
    if "${{ github.ref }}" in g or "github.ref" in g:
        errs.append("job %r concurrency.group must be REPO-WIDE (no github.ref suffix — orphan-hold, 86caammpq); got %r" % (jn, g))
    if jc.get("cancel-in-progress") is not False:
        errs.append("job %r cancel-in-progress must be false (queue, never drop a verdict — 86cah17eq); got %r" % (jn, jc.get("cancel-in-progress")))
if errs:
    print("\n".join(errs)); sys.exit(1)
sys.exit(0)
PYEOF
) && ci_rc=0 || ci_rc=1
  if [ "$ci_rc" -eq 0 ]; then
    ok "ci.yml concurrency invariants hold (top-level ref-scoped supersede; build/capture/playmode repo-wide queue — 86caammpq)"
  else
    bad "ci.yml concurrency invariants BROKEN (86caammpq orphan-hold guard):"
    printf '%s\n' "$conc_check" | grep -v '^$' | sed 's/^/       /'
  fi
else
  # Fallback: literal grep for the required group tokens (PyYAML unavailable).
  ci_fail=0
  grep -qE 'group:[[:space:]]*ci-\$\{\{[[:space:]]*github\.ref' "$CI_YML" || { note "top-level ci-\${{ github.ref }} group not found"; ci_fail=1; }
  grep -qE '^[[:space:]]+group:[[:space:]]*unity-build[[:space:]]*$' "$CI_YML" || { note "build job repo-wide 'unity-build' group not found (still ref-scoped?)"; ci_fail=1; }
  grep -qE '^[[:space:]]+group:[[:space:]]*unity-capture[[:space:]]*$' "$CI_YML" || { note "capture/playmode 'unity-capture' group not found"; ci_fail=1; }
  if [ "$ci_fail" -eq 0 ]; then
    ok "ci.yml concurrency invariants hold (grep fallback — 86caammpq)"
  else
    bad "ci.yml concurrency invariants BROKEN (86caammpq orphan-hold guard, grep fallback)"
  fi
fi

echo "==================================="
if [ "$fail" -ne 0 ]; then
  echo "structure check FAILED"
  exit 1
fi
echo "structure check PASSED"
