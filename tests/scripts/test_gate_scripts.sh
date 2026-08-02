#!/usr/bin/env bash
# test_gate_scripts.sh — unit-style checks for the CI gate scripts (ticket 86ca86g7k).
#
# The testing bar applies to itself: the gate scripts are tested where testable.
# This runner exercises BOTH bug-class regressions Tess flagged + the new capture
# gate's content checks, on a tmp tree, with zero Unity dependency. Run locally or
# in the license-free `structure` CI job.
#
#   tests/scripts/test_gate_scripts.sh
#
# Each check asserts an exit code AND (where it matters) an output substring, so a
# script that "passes for the wrong reason" still fails the test. Fails loud with a
# per-check summary; exit 1 on any failure.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
SCRIPTS="$ROOT/.github/workflows/scripts"
LOG_GATE="$SCRIPTS/check_unity_log.sh"
FRAME_CHECK="$SCRIPTS/frame_check.py"
VERIFY_STAMP="$SCRIPTS/verify_build_stamp.py"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

pass=0; fail=0
ok()  { printf '[ OK ] %s\n' "$1"; pass=$((pass+1)); }
bad() { printf '[FAIL] %s\n' "$1"; fail=$((fail+1)); }

# assert_rc <expected-rc> <label> -- <cmd...>
assert_rc() {
  local exp="$1" label="$2"; shift 3   # drop expected, label, and the literal --
  local out; out="$("$@" 2>&1)"; local rc=$?
  if [ "$rc" -eq "$exp" ]; then ok "$label (rc=$rc)"; else
    bad "$label — expected rc=$exp got rc=$rc"; printf '%s\n' "$out" | sed 's/^/        /'
  fi
}

# assert_rc_and_grep <expected-rc> <needle> <label> -- <cmd...>
assert_rc_and_grep() {
  local exp="$1" needle="$2" label="$3"; shift 4
  local out; out="$("$@" 2>&1)"; local rc=$?
  if [ "$rc" -eq "$exp" ] && printf '%s' "$out" | grep -qF "$needle"; then
    ok "$label (rc=$rc, matched '$needle')"
  else
    bad "$label — expected rc=$exp + '$needle'; got rc=$rc"
    printf '%s\n' "$out" | sed 's/^/        /'
  fi
}

# assert_rc_grep_present_absent <expected-rc> <needle-present> <needle-absent> <label> -- <cmd...>
# Passes iff rc matches AND the present-needle IS in the output AND the absent-needle is NOT.
# Proves a message SPLIT: the correct message fires and the WRONG (mis-attributing) one does not.
assert_rc_grep_present_absent() {
  local exp="$1" yes="$2" no="$3" label="$4"; shift 5
  local out; out="$("$@" 2>&1)"; local rc=$?
  if [ "$rc" -eq "$exp" ] && printf '%s' "$out" | grep -qF "$yes" && ! printf '%s' "$out" | grep -qF "$no"; then
    ok "$label (rc=$rc, matched '$yes', absent '$no')"
  else
    bad "$label — expected rc=$exp + '$yes' present + '$no' absent; got rc=$rc"
    printf '%s\n' "$out" | sed 's/^/        /'
  fi
}

echo "=== check_unity_log.sh ==="

# 1. Clean log → PASS (rc 0).
printf 'some normal line\n[BootstrapProject] complete\n' > "$TMP/clean.log"
assert_rc_and_grep 0 "LOG GATE PASSED" "clean log passes" -- bash "$LOG_GATE" "$TMP/clean.log"

# 2. Real compile error → FAIL (rc 1).
printf 'Assets/Foo.cs(3,5): error CS0103: name does not exist\nCompilation failed\n' > "$TMP/err.log"
assert_rc_and_grep 1 "LOG GATE FAILED" "real error fails" -- bash "$LOG_GATE" "$TMP/err.log"

# 3. URP first-import warning only → PASS (allowlisted).
printf "shader Terrain Standard 4 Layers URP could not be found\nCouldn't find preset for Terrain\n" > "$TMP/urp.log"
assert_rc_and_grep 0 "LOG GATE PASSED" "URP first-import warning allowlisted" -- bash "$LOG_GATE" "$TMP/urp.log"

# 4. NIT 2 — recovered NavMesh init-order race line → PASS (allowlisted).
printf 'Failed to create agent because there is no valid NavMesh\n[MovementVerifyCapture] agent on NavMesh: True\n' > "$TMP/nav.log"
assert_rc_and_grep 0 "LOG GATE PASSED" "recovered NavMesh race allowlisted (nit 2)" -- bash "$LOG_GATE" "$TMP/nav.log"

# 5. NIT 1 — a REAL error line that ALSO contains an allowlisted substring must
#    STILL FAIL. This is the masking false-negative Tess reproduced; it is the
#    load-bearing regression guard for the nit-1 fix.
printf 'Assets/Foo.cs(1,1): error CS0246: Terrain Standard 4 Layers URP type missing\n' > "$TMP/mask.log"
assert_rc_and_grep 1 "LOG GATE FAILED" "nit-1: error co-located with allowlisted substring still fails" -- bash "$LOG_GATE" "$TMP/mask.log"

# 6. Mixed: a benign NavMesh line AND a real error in the same log → FAIL.
printf 'Failed to create agent because there is no valid NavMesh\nFatal error: boom\n' > "$TMP/mixed.log"
assert_rc_and_grep 1 "LOG GATE FAILED" "benign + real error together fails" -- bash "$LOG_GATE" "$TMP/mixed.log"

echo "=== check_corrupt_build.sh (warm-runner corrupt-build canary, 86cagr0zu) ==="
# THE bug class this guards (unity-conventions.md §Process notes, OBSERVED #197 v5): the
# warm clean:false runner intermittently ships a CORRUPT exe from a stale/partial
# Library/ScriptAssemblies — Unity loads the scene against a mismatched layout and emits
# a SERIALIZATION-MISMATCH ("WasdMovement Read 84 expected 88") / MISSING-SCRIPT /
# BROKEN-ASSEMBLY line, shipping a build with inert WASD/NavMesh. EditMode + review PASS
# on it (editor, fresh domain) and the console-error gate does NOT scan serialization
# warnings, so this canary is the only build-time signal. Load-bearing cases: the exact
# #197 literal fires; clean + benign logs (incl. the allowlisted NavMesh race the console
# gate treats as benign) do NOT false-positive.
CORRUPT_GATE="$SCRIPTS/check_corrupt_build.sh"

# 1. THE #197 literal serialization mismatch → FAIL (rc 1), NAMED as a corrupt build.
printf 'Loaded scene Boot\nWasdMovement Read 84 expected 88\n' > "$TMP/corrupt_197.log"
assert_rc_and_grep 1 "CORRUPT BUILD DETECTED" "corrupt: #197 'Read 84 expected 88' serialization mismatch fails" \
  -- bash "$CORRUPT_GATE" "$TMP/corrupt_197.log"

# 2. The verbose Unity serialization-layout wording → FAIL.
printf 'A script behaviour has a different serialization layout when loading. (Read 12 Bytes but expected 20 bytes)\n' > "$TMP/corrupt_layout.log"
assert_rc_and_grep 1 "CORRUPT-BUILD GATE FAILED" "corrupt: 'different serialization layout' fails" \
  -- bash "$CORRUPT_GATE" "$TMP/corrupt_layout.log"

# 3. Missing MonoBehaviour script reference (stale assembly dropped the type) → FAIL.
printf "The referenced script (Assembly-CSharp) on this Behaviour is missing!\n" > "$TMP/corrupt_missing.log"
assert_rc_and_grep 1 "CORRUPT BUILD DETECTED" "corrupt: missing referenced script fails" \
  -- bash "$CORRUPT_GATE" "$TMP/corrupt_missing.log"

# 4. Broken/unloadable managed assembly (partial ScriptAssemblies DLL) → FAIL.
printf 'Unloading broken assembly Assets/... , this can cause crashes\n' > "$TMP/corrupt_broken.log"
assert_rc_and_grep 1 "CORRUPT-BUILD GATE FAILED" "corrupt: broken assembly fails" \
  -- bash "$CORRUPT_GATE" "$TMP/corrupt_broken.log"

# 5. LOAD-BEARING false-positive guard — a CLEAN log with the KNOWN-BENIGN CI console lines
#    (URP first-import terrain warnings + the recovered NavMesh race the console gate
#    allowlists) must NOT be flagged. If this ever false-positives, every healthy warm run
#    goes red — the corrupt-build gate would be worse than useless.
printf '%s\n' \
  '[BootstrapProject] complete' \
  'shader Terrain Standard 4 Layers URP could not be found' \
  "Couldn't find preset for Terrain" \
  'Failed to create agent because there is no valid NavMesh' \
  '[FarHorizonBuilder] result=Succeeded size=54000000' \
  > "$TMP/corrupt_clean.log"
assert_rc_and_grep 0 "CORRUPT-BUILD GATE PASSED" "corrupt: clean+benign log does NOT false-positive (load-bearing)" \
  -- bash "$CORRUPT_GATE" "$TMP/corrupt_clean.log"

# 6. A missing log is NOT a corruption signal (the producing step's own gate covers absence)
#    → PASS. Prevents the canary from red-ing a run where a log simply wasn't written.
assert_rc_and_grep 0 "CORRUPT-BUILD GATE PASSED" "corrupt: missing log is not a corruption signal" \
  -- bash "$CORRUPT_GATE" "$TMP/does_not_exist.log"

# 7. Multi-log: one clean + one corrupt → FAIL (scans every log handed to it).
assert_rc_and_grep 1 "CORRUPT BUILD DETECTED" "corrupt: one corrupt log among several fails the batch" \
  -- bash "$CORRUPT_GATE" "$TMP/corrupt_clean.log" "$TMP/corrupt_197.log"

echo "=== clean_scriptassemblies.sh (targeted warm-runner heal, 86cagr0zu) ==="
# The heal must delete ONLY the regenerable compiled-script + Bee caches so a corrupt warm
# runner recompiles fresh on the re-run, while Library/PackageCache (the clean:false win)
# stays WARM. Load-bearing: PackageCache survives; idempotent on absent dirs.
SA_CLEAN="$SCRIPTS/clean_scriptassemblies.sh"

# 1. Removes ScriptAssemblies + Bee, PRESERVES PackageCache → PASS + PackageCache still there.
SAPROJ="$TMP/sa_proj"
mkdir -p "$SAPROJ/Library/ScriptAssemblies" "$SAPROJ/Library/Bee" "$SAPROJ/Library/PackageCache/com.unity.x"
printf 'x' > "$SAPROJ/Library/ScriptAssemblies/Assembly-CSharp.dll"
printf 'x' > "$SAPROJ/Library/Bee/artifact.bin"
printf 'x' > "$SAPROJ/Library/PackageCache/com.unity.x/pkg.txt"
assert_rc_and_grep 0 "PackageCache left WARM" "sa-clean: runs clean + reports PackageCache preserved" \
  -- bash "$SA_CLEAN" "$SAPROJ"
if [ ! -e "$SAPROJ/Library/ScriptAssemblies" ] && [ ! -e "$SAPROJ/Library/Bee" ]; then
  ok "sa-clean: ScriptAssemblies + Bee removed"
else
  bad "sa-clean: ScriptAssemblies/Bee NOT removed"
fi
# THE load-bearing assert — the expensive warm cache must survive (else this is just clean:true).
if [ -f "$SAPROJ/Library/PackageCache/com.unity.x/pkg.txt" ]; then
  ok "sa-clean: Library/PackageCache PRESERVED (warm-build win intact)"
else
  bad "sa-clean: Library/PackageCache was wiped — this would kill the clean:false warm-build win"
fi

# 2. Idempotent — second run on the now-absent dirs still exits 0, no error.
assert_rc_and_grep 0 "PackageCache left WARM" "sa-clean: idempotent on absent dirs" \
  -- bash "$SA_CLEAN" "$SAPROJ"

echo "=== frame_check.py ==="

# Build tiny PNG fixtures with stdlib only (no Pillow dep) so the test runs
# anywhere the gate runs.
python3 - "$TMP" <<'PY'
import os, sys, struct, zlib, random
out = sys.argv[1]

def write_png(path, pixels, w, h):
    def chunk(typ, data):
        c = struct.pack(">I", len(data)) + typ + data
        return c + struct.pack(">I", zlib.crc32(typ + data) & 0xFFFFFFFF)
    raw = bytearray()
    for y in range(h):
        raw.append(0)  # filter none
        for x in range(w):
            r, g, b = pixels(x, y)
            raw += bytes((r, g, b))
    sig = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)  # 8-bit RGB
    idat = zlib.compress(bytes(raw))
    with open(path, "wb") as f:
        f.write(sig + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b""))

W = H = 64
# black/empty
write_png(os.path.join(out, "black.png"), lambda x, y: (0, 0, 0), W, H)
# uniform mid-grey (dead frame: clear colour with nothing drawn)
write_png(os.path.join(out, "uniform.png"), lambda x, y: (90, 90, 90), W, H)
# all-magenta (shader strip)
write_png(os.path.join(out, "magenta.png"), lambda x, y: (255, 0, 255), W, H)
# good: varied content (gradient + a bright square = real variance, sane luma)
def good(x, y):
    base = (x * 3) % 200 + 20
    if 20 <= x < 44 and 20 <= y < 44:
        return (240, 230, 120)  # bright HUD-ish block
    return (base, base // 2 + 30, 200 - base // 2)
write_png(os.path.join(out, "good.png"), good, W, H)

# good-frames dir (two good frames) for the multi-frame + min-frames test
gd = os.path.join(out, "goodset")
os.makedirs(gd, exist_ok=True)
write_png(os.path.join(gd, "capture_00.png"), good, W, H)
write_png(os.path.join(gd, "capture_01.png"), good, W, H)

# empty dir (zero frames)
os.makedirs(os.path.join(out, "emptyset"), exist_ok=True)
PY

assert_rc_and_grep 0 "CAPTURE GATE PASSED" "good frame passes" -- python3 "$FRAME_CHECK" "$TMP/good.png"
assert_rc_and_grep 1 "black/empty" "black frame fails" -- python3 "$FRAME_CHECK" "$TMP/black.png"
assert_rc_and_grep 1 "uniform/dead" "uniform frame fails" -- python3 "$FRAME_CHECK" "$TMP/uniform.png"
assert_rc_and_grep 1 "magenta" "magenta frame fails (shader strip)" -- python3 "$FRAME_CHECK" "$TMP/magenta.png"
assert_rc_and_grep 0 "2 frame(s) have real content" "good multi-frame dir passes" -- python3 "$FRAME_CHECK" "$TMP/goodset"
assert_rc_and_grep 1 "found 0 frame(s)" "zero frames fails (silent-killer guard)" -- python3 "$FRAME_CHECK" "$TMP/emptyset" --min-frames 1

echo "=== verify_build_stamp.py (soak stale-stamp guard, 86ca86gde) ==="

# THE bug class this guards (unity-conventions.md §Headless/CLI): BuildWindows
# does NOT regenerate BuildStamp.txt, the stamp is committed, so a soak that skips
# bootstrap ships a stale sha and the Sponsor can't tell which build they're
# running. The guard must FAIL on a sha != HEAD, not just on a missing file.

# 1. Stamp sha == expected HEAD → PASS.
printf 'zoned | 2026-06-12T14:43:12Z | a3edf04\n' > "$TMP/stamp_match.txt"
assert_rc_and_grep 0 "matching HEAD" "stamp matching HEAD passes" \
  -- python3 "$VERIFY_STAMP" "$TMP/stamp_match.txt" a3edf04

# 2. THE regression guard — stamp sha != HEAD → FAIL (today's manual-soak incident:
#    stamp said 28d9de7 while HEAD had moved on). This is the load-bearing case;
#    a "file exists + parses" check alone passed all through the stale-stamp era.
printf 'zoned | 2026-06-12T14:43:12Z | 28d9de7\n' > "$TMP/stamp_stale.txt"
assert_rc_and_grep 1 "STALE STAMP" "stale stamp (sha != HEAD) fails" \
  -- python3 "$VERIFY_STAMP" "$TMP/stamp_stale.txt" a3edf04

# 3. Malformed stamp (missing the sha field) → FAIL loud, not silent-pass.
printf 'zoned | 2026-06-12T14:43:12Z\n' > "$TMP/stamp_malformed.txt"
assert_rc_and_grep 1 "malformed stamp" "malformed stamp fails loud" \
  -- python3 "$VERIFY_STAMP" "$TMP/stamp_malformed.txt" a3edf04

# 4. Empty stamp field (half-written) → FAIL.
printf 'zoned |  | a3edf04\n' > "$TMP/stamp_empty.txt"
assert_rc_and_grep 1 "malformed stamp" "empty middle field fails" \
  -- python3 "$VERIFY_STAMP" "$TMP/stamp_empty.txt" a3edf04

# 5. Unreadable / missing file → FAIL (a check that can't read its input is a
#    failure, never a pass).
assert_rc_and_grep 1 "cannot read" "missing stamp file fails loud" \
  -- python3 "$VERIFY_STAMP" "$TMP/does_not_exist.txt" a3edf04

echo "=== structure_check.sh throwaway-artifact guard ==="

# The structure check reads `git ls-files`, so test it inside a throwaway git repo
# with a minimally-valid project tree + one stray artifact, and assert it fails.
# This pins the new guard (VerifyCaptures/ + <platform>-results.xml are throwaway).
STRUCT="$SCRIPTS/structure_check.sh"
make_min_repo() {
  local d="$1"
  ( cd "$d" \
    && git init -q && git config user.email t@t && git config user.name t \
    && mkdir -p Assets/Scripts/Runtime Assets/Scripts/Editor \
               Assets/Tests/EditMode Assets/Tests/PlayMode Packages ProjectSettings \
               .github/workflows \
    && printf '%s\n' \
        'name: CI' \
        'concurrency:' \
        '  group: ci-${{ github.ref }}' \
        '  cancel-in-progress: true' \
        'jobs:' \
        '  structure: { runs-on: ubuntu-latest, steps: [] }' \
        '  build:' \
        '    runs-on: [self-hosted, windows, unity]' \
        '    concurrency: { group: unity-build, cancel-in-progress: false }' \
        '    steps:' \
        '      - run: .github/workflows/scripts/check_corrupt_build.sh ci-out/build.log' \
        '      - run: .github/workflows/scripts/clean_scriptassemblies.sh "$GITHUB_WORKSPACE"' \
        '  capture:' \
        '    runs-on: [self-hosted, windows, unity, capture]' \
        '    concurrency: { group: unity-capture, cancel-in-progress: false }' \
        '    steps:' \
        '      - run: .github/workflows/scripts/check_corrupt_build.sh ci-out/capture.log' \
        '  playmode:' \
        '    runs-on: [self-hosted, windows, unity]' \
        '    concurrency: { group: unity-capture, cancel-in-progress: false }' \
        '    steps: []' \
        > .github/workflows/ci.yml \
    && printf '{"name":"FarHorizon.Runtime"}\n'   > Assets/Scripts/Runtime/FarHorizon.Runtime.asmdef \
    && printf '{"name":"FarHorizon.Editor"}\n'    > Assets/Scripts/Editor/FarHorizon.Editor.asmdef \
    && printf '{"name":"FarHorizon.EditTests"}\n' > Assets/Tests/EditMode/FarHorizon.EditTests.asmdef \
    && printf '{"name":"FarHorizon.PlayTests"}\n' > Assets/Tests/PlayMode/FarHorizon.PlayTests.asmdef \
    && for f in Assets/Scripts/Runtime/FarHorizon.Runtime.asmdef \
                Assets/Scripts/Editor/FarHorizon.Editor.asmdef \
                Assets/Tests/EditMode/FarHorizon.EditTests.asmdef \
                Assets/Tests/PlayMode/FarHorizon.PlayTests.asmdef; do
         printf 'fileFormatVersion: 2\nguid: 00000000000000000000000000000000\n' > "$f.meta"; done \
    && printf 'public static void Run()' > Assets/Scripts/Editor/BootstrapProject.cs \
    && printf 'fileFormatVersion: 2\nguid: 00000000000000000000000000000001\n' > Assets/Scripts/Editor/BootstrapProject.cs.meta \
    && printf 'public static void BuildWindows()' > Assets/Scripts/Editor/FarHorizonBuilder.cs \
    && printf 'fileFormatVersion: 2\nguid: 00000000000000000000000000000002\n' > Assets/Scripts/Editor/FarHorizonBuilder.cs.meta \
    && printf '{}\n' > Packages/manifest.json \
    && printf 'm_EditorVersion: 6000.4.11f1\n' > ProjectSettings/ProjectVersion.txt \
    && git add -A >/dev/null 2>&1 ) || return 1
}

CLEAN_REPO="$TMP/clean_repo"; mkdir -p "$CLEAN_REPO"; make_min_repo "$CLEAN_REPO"
assert_rc_and_grep 0 "structure check PASSED" "structure: clean minimal repo passes" \
  -- bash -c "cd '$CLEAN_REPO' && bash '$STRUCT'"

STRAY_REPO="$TMP/stray_repo"; mkdir -p "$STRAY_REPO"; make_min_repo "$STRAY_REPO"
( cd "$STRAY_REPO" && printf '<x/>' > editmode-results.xml && mkdir -p VerifyCaptures \
  && printf 'x' > VerifyCaptures/a.png && git add -f editmode-results.xml VerifyCaptures/a.png >/dev/null 2>&1 )
assert_rc_and_grep 1 "structure check FAILED" "structure: stray results.xml + VerifyCaptures flagged" \
  -- bash -c "cd '$STRAY_REPO' && bash '$STRUCT'"

# THE 86cafk5vb regression guard — a root-level NUnit dump named WITHOUT the
# `-results` suffix (PR #177's `editmode-bake176.xml`) must be caught BY CONTENT.
# The old filename-suffix gate (`-results.xml` / `test-results*.xml`) MISSED it; the
# new gate inspects the file head for the NUnit `<test-run` root element. We plant the
# real header shape (matching the committed stray file's first two lines) and `git add -f`
# (it's now also gitignored by `/editmode*.xml`, mirroring how `git add -A` slipped it in).
NUNIT_REPO="$TMP/nunit_content_repo"; mkdir -p "$NUNIT_REPO"; make_min_repo "$NUNIT_REPO"
( cd "$NUNIT_REPO" \
  && printf '%s\n%s\n' '<?xml version="1.0" encoding="utf-8"?>' \
       '<test-run id="2" testcasecount="20" result="Passed" total="20" passed="20" failed="0">' \
       > editmode-bake176.xml \
  && git add -f editmode-bake176.xml >/dev/null 2>&1 )
assert_rc_and_grep 1 "structure check FAILED" "structure: root NUnit XML w/o -results suffix flagged by CONTENT (86cafk5vb / #177)" \
  -- bash -c "cd '$NUNIT_REPO' && bash '$STRUCT'"

# FALSE-POSITIVE guard — a genuine root-level project XML that is NOT an NUnit dump
# (e.g. a mono-style `<mconfig>` config, or any non-test XML) must NOT trip the content
# gate. The content gate keys ONLY on the `<test-run` marker, so a normal XML passes.
# This pins the "zero false positives by design" contract while the gate is content-based.
LEGIT_XML_REPO="$TMP/legit_xml_repo"; mkdir -p "$LEGIT_XML_REPO"; make_min_repo "$LEGIT_XML_REPO"
( cd "$LEGIT_XML_REPO" \
  && printf '%s\n%s\n' '<?xml version="1.0" encoding="utf-8"?>' '<mconfig><configuration/></mconfig>' \
       > project_config.xml \
  && git add -f project_config.xml >/dev/null 2>&1 )
assert_rc_and_grep 0 "structure check PASSED" "structure: non-NUnit root XML does NOT false-positive (86cafk5vb)" \
  -- bash -c "cd '$LEGIT_XML_REPO' && bash '$STRUCT'"

# ---------------------------------------------------------------------------
# CI concurrency-invariant guard (ticket 86caammpq — the merged-branch orphan-hold
# fix). structure_check.sh check #6 pins the concurrency shape so a future edit can't
# silently revert the runner-contending jobs back to ref-scoped (which re-introduces
# the orphan-hold that forced manual `gh run cancel`). These cases prove the guard
# FIRES on each regression direction — the regression test for the fix itself.
# ---------------------------------------------------------------------------

# NEGATIVE A — build job reverted to ref-scoped + cancel:true (orphan-hold returns).
CONC_BUILD_REPO="$TMP/conc_build_repo"; mkdir -p "$CONC_BUILD_REPO"; make_min_repo "$CONC_BUILD_REPO"
( cd "$CONC_BUILD_REPO" \
  && sed -i 's/{ group: unity-build, cancel-in-progress: false }/{ group: unity-build-${{ github.ref }}, cancel-in-progress: true }/' .github/workflows/ci.yml \
  && git add -A >/dev/null 2>&1 )
assert_rc_and_grep 1 "concurrency invariants BROKEN" "structure: ref-scoped build job flagged (86caammpq orphan-hold guard)" \
  -- bash -c "cd '$CONC_BUILD_REPO' && bash '$STRUCT'"

# NEGATIVE B — top-level group made repo-wide (breaks same-ref supersede — wrong direction).
CONC_TOP_REPO="$TMP/conc_top_repo"; mkdir -p "$CONC_TOP_REPO"; make_min_repo "$CONC_TOP_REPO"
( cd "$CONC_TOP_REPO" \
  && sed -i 's/  group: ci-${{ github.ref }}/  group: ci-fixed/; s/^  cancel-in-progress: true$/  cancel-in-progress: false/' .github/workflows/ci.yml \
  && git add -A >/dev/null 2>&1 )
assert_rc_and_grep 1 "concurrency invariants BROKEN" "structure: repo-wide top-level (lost same-ref supersede) flagged (86caammpq)" \
  -- bash -c "cd '$CONC_TOP_REPO' && bash '$STRUCT'"

# NEGATIVE C — capture job made cross-ref cancel (would DROP a contending verdict — 86cah17eq).
CONC_CAP_REPO="$TMP/conc_cap_repo"; mkdir -p "$CONC_CAP_REPO"; make_min_repo "$CONC_CAP_REPO"
( cd "$CONC_CAP_REPO" \
  && sed -i 's/{ group: unity-capture, cancel-in-progress: false }/{ group: unity-capture, cancel-in-progress: true }/' .github/workflows/ci.yml \
  && git add -A >/dev/null 2>&1 )
assert_rc_and_grep 1 "concurrency invariants BROKEN" "structure: capture cancel-in-progress:true flagged (86cah17eq drop-verdict guard)" \
  -- bash -c "cd '$CONC_CAP_REPO' && bash '$STRUCT'"

# NEGATIVE D — the corrupt-build canary reference dropped from ci.yml (86cagr0zu wiring guard).
# A future ci.yml edit that removes the check_corrupt_build.sh step re-opens the "warm-runner
# corrupt build dismissed as a launch flake" gap. structure_check check #7 must catch it.
CANARY_DROP_REPO="$TMP/canary_drop_repo"; mkdir -p "$CANARY_DROP_REPO"; make_min_repo "$CANARY_DROP_REPO"
( cd "$CANARY_DROP_REPO" \
  && sed -i '/check_corrupt_build.sh/d' .github/workflows/ci.yml \
  && git add -A >/dev/null 2>&1 )
assert_rc_and_grep 1 "corrupt-build canary wiring BROKEN" "structure: dropped corrupt-build canary flagged (86cagr0zu)" \
  -- bash -c "cd '$CANARY_DROP_REPO' && bash '$STRUCT'"

echo "=== verify_settings_gate.sh (settings-panel capture gate, 86caa4bqp) ==="

# THE bug class this guards (Tess QA bounce, PR #83): the settings success-test is
# "the panel OPENS + a tweak TAKES EFFECT LIVE". A gate that only checks that frames
# rendered would PASS a panel that draws but never drives the game — so the gate must
# ALSO require the `changedLive=True` ground-truth log line. We exercise the gate's
# pass/fail logic with a FAKE exe (a bash script that writes controllable frames + log
# from the -captureDir / -logFile args it is handed), zero Unity dependency. All
# fixtures live under $TMP so nothing is left in the tracked tree.
SETTINGS_GATE="$SCRIPTS/verify_settings_gate.sh"

# Shared PNG writer (reuses the same 'good'-frame shape as the frame_check fixtures).
# $4 = "tweaked" → paint an extra block so settings_tweaked.png VISIBLY differs from
# settings_open.png (mimics the repainted readout); anything else → identical frame.
PNG_HELPER="$TMP/_make_settings_pngs.py"
cat > "$PNG_HELPER" <<'PY'
import os, sys, struct, zlib
d = sys.argv[1]; os.makedirs(d, exist_ok=True)
def write_png(path, w, h, tweaked=False):
    def chunk(typ, data):
        return struct.pack(">I", len(data)) + typ + data + struct.pack(">I", zlib.crc32(typ+data)&0xFFFFFFFF)
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            base = (x*3)%200+20
            # A second bright block ONLY on the tweaked frame = the repainted readout region.
            if tweaked and 44<=x<60 and 44<=y<60: raw += bytes((20,240,40))
            elif 20<=x<44 and 20<=y<44: raw += bytes((240,230,120))
            else: raw += bytes((base, base//2+30, 200-base//2))
    with open(path,"wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",w,h,8,2,0,0,0))
                +chunk(b"IDAT",zlib.compress(bytes(raw)))+chunk(b"IEND",b""))
# sys.argv[2] = "identical" → tweaked frame is a byte-copy of open (the PR #83 bug repro).
identical = len(sys.argv) > 2 and sys.argv[2] == "identical"
write_png(os.path.join(d,"settings_closed.png"),64,64)
write_png(os.path.join(d,"settings_open.png"),64,64)
write_png(os.path.join(d,"settings_tweaked.png"),64,64, tweaked=not identical)
PY

# Standalone fixtures for the direct frames_differ.py unit tests (open vs differ vs identical).
FRAMES_DIFFER="$SCRIPTS/frames_differ.py"
DIFFDIR="$TMP/frames_differ_fix"; python3 "$PNG_HELPER" "$DIFFDIR"                 # tweaked DIFFERS
IDENTDIR="$TMP/frames_ident_fix"; python3 "$PNG_HELPER" "$IDENTDIR" identical      # tweaked IDENTICAL

# Fake-exe factory: writes 3 good PNGs into -captureDir and a log into -logFile.
# $1 = output path, $2 = the changedLive token (True / False / empty = no proof line),
# $3 = "identical" → tweaked frame is a byte-copy of open (the PR #83 pixel-identical bug),
# $4 = #247 row-visibility proof emission (Check 4 — DEV at the open frame + PLAYER at the F1
#      drawer; the real SettingsVerifyCapture logs BOTH from VisibleRowCount ground truth):
#        "both"   (default) → DEV rows visible: 9/62 AND PLAYER rows visible: 8/8 (both > 0) — the
#                             faithful mirror of a healthy shipped run; Check 4 passes.
#        "devzero"          → DEV rows visible: 0/62 (one collapsed drawer, the #247 regression) —
#                             Check 4's zero-rows branch must red the gate.
#        "none"             → emit NEITHER proof line — Check 4's absent-proof branch must red it.
# $5 = #247 v2 stepper-fit proof emission (Check 5 — the smallest resolved [−]/value/[+] cell
#      width per drawer; the real SettingsVerifyCapture logs BOTH from MinStepperCellWidth ground
#      truth). Check 5 greps 'PLAYER STEPPER fit' / 'DEV STEPPER fit' + extracts minCellWidth
#      (>= 20px pass; < 20px crush fail; -1 no-stepper-row legit pass):
#        "both"   (default) → both drawers minCellWidth=28.0px (healthy 28px button) — Check 5 passes.
#        "crush"            → PLAYER minCellWidth=8.0px (the F1 [−]/value/[+] crush the #247 v2 gate
#                             catches; F3 stays roomy) — Check 5's crush branch must red the gate.
#        "none"             → emit NEITHER stepper-fit line — Check 5's absent-proof branch must red it.
make_fake_exe() {
  local exe="$1" changed="$2" ident="${3:-}" rows="${4:-both}" stepper="${5:-both}"
  cat > "$exe" <<FAKE
#!/usr/bin/env bash
capdir=""; logf=""
while [ \$# -gt 0 ]; do
  case "\$1" in
    -captureDir) capdir="\$2"; shift 2;;
    -logFile)    logf="\$2"; shift 2;;
    *) shift;;
  esac
done
python3 "$PNG_HELPER" "\$capdir" "$ident"
if [ -n "$changed" ]; then
  echo "[SettingsVerifyCapture] WALK SPEED tweak: before=5.00 setTo=9.00 liveAfter=9.00 changedLive=$changed (AC2)" >> "\$logf"
fi
# #247 row-visibility proof lines — mirror the real SettingsVerifyCapture emission so the gate's
# Check 4 (both drawers show > 0 rows) and this fixture path AGREE. Check 4 greps per-drawer:
# 'DEV rows visible: [1-9]' AND 'PLAYER rows visible: [1-9]' (pass) / 'rows visible: 0 ' (fail).
case "$rows" in
  both)    echo "[SettingsVerifyCapture] DEV rows visible: 9 / 62 routed (viewportHeight=699.0px; #247 empty-drawers guard)" >> "\$logf"
           echo "[SettingsVerifyCapture] PLAYER rows visible: 8 / 8 routed (viewportHeight=565.5px; #247 empty-drawers guard)" >> "\$logf";;
  devzero) echo "[SettingsVerifyCapture] DEV rows visible: 0 / 62 routed (viewportHeight=0.0px; #247 empty-drawers guard)" >> "\$logf"
           echo "[SettingsVerifyCapture] PLAYER rows visible: 8 / 8 routed (viewportHeight=565.5px; #247 empty-drawers guard)" >> "\$logf";;
  none)    : ;;  # emit NEITHER proof line (the absent-proof-line case)
esac
# #247 v2 stepper-fit proof lines (Check 5) — mirror the real SettingsVerifyCapture emission
# (SettingsVerifyCapture.cs PLAYER/DEV STEPPER fit) so the gate's Check 5 (both drawers'
# [−]/value/[+] columns have room) and this fixture path AGREE. Check 5 greps per-drawer:
# 'PLAYER STEPPER fit' / 'DEV STEPPER fit' + extracts minCellWidth (>= 20px pass / < 20px crush).
case "$stepper" in
  both)  echo "[SettingsVerifyCapture] PLAYER STEPPER fit (#247 v2): minCellWidth=28.0px stepperRows=2 (must be > 20px)" >> "\$logf"
         echo "[SettingsVerifyCapture] DEV STEPPER fit (#247 v2): minCellWidth=28.0px stepperRows=2 (must be > 20px OR -1/no-steppers)" >> "\$logf";;
  crush) echo "[SettingsVerifyCapture] PLAYER STEPPER fit (#247 v2): minCellWidth=8.0px stepperRows=2 (must be > 20px)" >> "\$logf"
         echo "[SettingsVerifyCapture] DEV STEPPER fit (#247 v2): minCellWidth=28.0px stepperRows=2 (must be > 20px OR -1/no-steppers)" >> "\$logf";;
  none)  : ;;  # emit NEITHER stepper-fit line (the absent-proof-line case)
esac
echo "[SettingsVerifyCapture] verification complete -> \$capdir" >> "\$logf"
exit 0
FAKE
  chmod +x "$exe"
}

# 1. Good frames + changedLive=True + tweaked frame DIFFERS → PASS.
make_fake_exe "$TMP/fake_pass.sh" "True"
assert_rc_and_grep 0 "SETTINGS CAPTURE GATE PASSED" "panel rendered + live tweak + visible-diff passes" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_pass.sh" "$TMP/scaps_pass" "$TMP/slog_pass.log"

# 2. THE load-bearing case — frames render but changedLive=False → FAIL. A frame-only
#    gate would green this (the exact gap that let PR #83 ship without proving the tweak).
make_fake_exe "$TMP/fake_nochange.sh" "False"
assert_rc_and_grep 1 "SETTINGS CAPTURE GATE FAILED" "rendered panel w/o live change fails" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_nochange.sh" "$TMP/scaps_nc" "$TMP/slog_nc.log"

# 3. No SettingsVerifyCapture proof line at all (panel never drove the tweak) → FAIL.
make_fake_exe "$TMP/fake_silent.sh" ""
assert_rc_and_grep 1 "no 'changedLive=True'" "missing proof line fails loud" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_silent.sh" "$TMP/scaps_si" "$TMP/slog_si.log"

# 4. Missing exe → FAIL loud (the build step must run first).
assert_rc_and_grep 1 "exe not found" "missing exe fails loud" \
  -- bash "$SETTINGS_GATE" "$TMP/does_not_exist.exe" "$TMP/scaps_x" "$TMP/slog_x.log"

# 5. Check 3 (visible-tweak diff) is FATAL again — UN-QUARANTINED (86cabe3e5). This is THE
#    regression guard for the bug class this ticket fixes: even with changedLive=True (the live
#    param DID change), if settings_tweaked.png is a BYTE-COPY of settings_open.png — i.e. the
#    tweak did NOT repaint the captured frame, the exact symptom of reverting to the SYNTHETIC
#    entry-setter + RefreshReadouts drive instead of a real dispatched ChangeEvent — the gate must
#    now FAIL (rc=1). 86cabe3e5 made the -verifySettings harness drive the tweak via a real
#    ChangeEvent (SettingsPanel.DriveFloat/DriveRangeChangeEventForCapture), so a real run repaints;
#    a pixel-identical tweaked frame therefore means a regression back to the synthetic drive and
#    reds the gate. If Check 3 is ever silently re-quarantined / made non-fatal, this assertion fails.
make_fake_exe "$TMP/fake_identical.sh" "True" "identical"
assert_rc_and_grep 1 "visible-diff sub-check FAILED" "pixel-identical tweaked frame FAILS the gate (un-quarantined 86cabe3e5)" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_identical.sh" "$TMP/scaps_id" "$TMP/slog_id.log"

# 6. THE #247 EMPTY-DRAWERS GUARD — Check 4 is FATAL. A drawer reporting ZERO visible rows FAILS the
#    gate even with changedLive=True AND a visibly-different tweaked frame (Checks 1-3 all GREEN): the
#    exact PR #247 symptom — the panel drew its header + footer but the flex-grow rows ScrollView
#    collapsed against a zero-height drawer container → zero rows rendered, and frame_check (whole-frame
#    only) waved the green gameplay world through. This isolates Check 4 as the sole failing check.
make_fake_exe "$TMP/fake_devzero.sh" "True" "" "devzero"
assert_rc_and_grep 1 "a drawer showed ZERO visible rows" "empty DEV drawer (0 rows) FAILS the gate (#247)" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_devzero.sh" "$TMP/scaps_dz" "$TMP/slog_dz.log"

# 7. #247 — the row-visibility proof line ABSENT for both drawers FAILS the gate. A build that never
#    probed row visibility (an older/regressed SettingsVerifyCapture) cannot silently pass Check 4;
#    the check demands the ground-truth proof line, not its mere absence. Checks 1-3 green; Check 4 fails.
make_fake_exe "$TMP/fake_norows.sh" "True" "" "none"
assert_rc_and_grep 1 "missing the #247 row-visibility proof line" "absent row-visibility proof FAILS the gate (#247)" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_norows.sh" "$TMP/scaps_nr" "$TMP/slog_nr.log"

# 8. THE #247 v2 F1-CRAMP GUARD — Check 5 is FATAL. A drawer whose int-stepper [−]/value/[+]
#    columns crush below their design width (minCellWidth < 20px) FAILS the gate even with
#    changedLive=True, a visibly-different tweaked frame, AND both drawers showing rows (Checks
#    1-4 all GREEN): the exact PR #247 v2 symptom the Sponsor re-soak flagged — the F1 stepper
#    control (flex-grow:1 + default flex-shrink:1) collapsed and overlapped its glyphs on the
#    no-scrollbar F1 rows. A within-row column crush is invisible to Check 1 (whole-frame) and
#    Check 4 (row overlaps viewport). This isolates Check 5 as the sole failing check.
make_fake_exe "$TMP/fake_crush.sh" "True" "" "both" "crush"
assert_rc_and_grep 1 "int-stepper columns CRUSHED" "crushed F1 stepper column FAILS the gate (#247 v2)" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_crush.sh" "$TMP/scaps_cr" "$TMP/slog_cr.log"

# ── WEDGE HARDENING (86cajt6kq) ──────────────────────────────────────────────────────────────
# THE flake this fixes: the windowed -verifySettings exe intermittently WEDGES in its present loop
# and is killed by the timeout, TRUNCATING the capture before settings_tweaked.png (written LAST).
# Old behaviour: the missing frame drove Check 3 to fire the CANNED "synthetic entry-setter drive
# (PR #83)" text — mis-attributing a missing-frame wedge to a historical content bug. Fix = (AC1) a
# bounded retry on the wedge signature, (AC2) a message SPLIT so the PR-#83 text fires ONLY on a
# present-but-identical frame, never on a truncation.
#
# Wedge fake-exe factory: emits every OTHER proof line (changedLive / rows / stepper) so the ONLY
# failing check is Check 3's missing-tweaked-frame branch — isolating the wedge-vs-content split.
#   mode "always"  → EVERY invocation writes ONLY closed + open (deletes tweaked) and exits 124 (the
#                    timeout-wedge signature). Both the run AND its retry truncate.
#   mode "recover" → attempt 1 truncates + exits 124; attempt 2 (the retry) writes ALL 3 frames +
#                    exits 0. Proves the bounded retry RECOVERS a one-shot wedge.
# The gate wipes capdir+logfile at the START of each attempt, so an inter-attempt counter FILE
# (outside both) is how the exe knows which attempt it is on. (Named distinctly from the cross-gate
# make_wedge_exe below — this one emits the settings-specific proof lines + models truncation.)
make_settings_wedge_exe() {
  local exe="$1" mode="$2" counter="$3"
  cat > "$exe" <<FAKE
#!/usr/bin/env bash
capdir=""; logf=""
while [ \$# -gt 0 ]; do
  case "\$1" in
    -captureDir) capdir="\$2"; shift 2;;
    -logFile)    logf="\$2"; shift 2;;
    *) shift;;
  esac
done
n=0; [ -f "$counter" ] && n=\$(cat "$counter"); n=\$((n+1)); echo "\$n" > "$counter"
emit_proof() {
  echo "[SettingsVerifyCapture] WALK SPEED tweak: before=5.00 setTo=9.00 liveAfter=9.00 changedLive=True (AC2)" >> "\$logf"
  echo "[SettingsVerifyCapture] DEV rows visible: 9 / 62 routed (viewportHeight=699.0px; #247 empty-drawers guard)" >> "\$logf"
  echo "[SettingsVerifyCapture] PLAYER rows visible: 8 / 8 routed (viewportHeight=565.5px; #247 empty-drawers guard)" >> "\$logf"
  echo "[SettingsVerifyCapture] PLAYER STEPPER fit (#247 v2): minCellWidth=28.0px stepperRows=2 (must be > 20px)" >> "\$logf"
  echo "[SettingsVerifyCapture] DEV STEPPER fit (#247 v2): minCellWidth=28.0px stepperRows=2 (must be > 20px OR -1/no-steppers)" >> "\$logf"
}
if [ "$mode" = "recover" ] && [ "\$n" -ge 2 ]; then
  python3 "$PNG_HELPER" "\$capdir"          # full 3-frame capture on the retry
  emit_proof
  exit 0
else
  python3 "$PNG_HELPER" "\$capdir"          # writes closed + open + tweaked ...
  rm -f "\$capdir/settings_tweaked.png"      # ... then TRUNCATE at settings_open.png (the wedge)
  emit_proof
  exit 124                                   # timeout-wedge exit signature
fi
FAKE
  chmod +x "$exe"
}

# 9. AC2 de-mislead — a persistent wedge (BOTH attempts truncate) reports the WEDGE/TRUNCATION
#    message naming the last frame present, and does NOT fire the canned PR-#83 "synthetic
#    entry-setter drive" text. This is the mis-attribution the ticket fixes.
make_settings_wedge_exe "$TMP/fake_wedge.sh" "always" "$TMP/wedge_ctr"
assert_rc_grep_present_absent 1 "capture TRUNCATED (WEDGE)" "synthetic entry-setter drive" \
  "wedge truncation → WEDGE message, NOT the PR-#83 canned regression (86cajt6kq AC2)" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_wedge.sh" "$TMP/scaps_wedge" "$TMP/slog_wedge.log"

# 10. AC2 — the wedge message NAMES the last frame actually present (closed → open → tweaked; the
#     capture truncated at settings_open.png), so the truncation point is legible in the CI log.
assert_rc_and_grep 1 "last frame actually present = settings_open.png" \
  "wedge message names the last frame present (86cajt6kq AC2)" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_wedge.sh" "$TMP/scaps_wedge2" "$TMP/slog_wedge2.log"

# 11. AC1 retry — a one-shot wedge (attempt 1 truncates + exits 124; the retry captures cleanly)
#     RECOVERS to a PASS. GATE PASSED here is only reachable via the retry: attempt 1 wrote no
#     tweaked frame, so without the bounded retry Check 3 would red the gate.
make_settings_wedge_exe "$TMP/fake_recover.sh" "recover" "$TMP/recover_ctr"
assert_rc_and_grep 0 "SETTINGS CAPTURE GATE PASSED" \
  "bounded retry RECOVERS a one-shot wedge (86cajt6kq AC1)" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_recover.sh" "$TMP/scaps_recover" "$TMP/slog_recover.log"

# 12. AC2 — the content-regression path is UNCHANGED: a present-but-pixel-identical tweaked frame
#     STILL fires the canned PR-#83 text (and is NOT mis-labelled a wedge/truncation). This is the
#     other side of the split — the message the wedge path must NOT steal.
make_fake_exe "$TMP/fake_ident2.sh" "True" "identical"
assert_rc_grep_present_absent 1 "synthetic entry-setter drive" "capture TRUNCATED (WEDGE)" \
  "present-but-identical frame → PR-#83 text, NOT the wedge message (86cajt6kq AC2)" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_ident2.sh" "$TMP/scaps_id2" "$TMP/slog_id2.log"

echo "=== frames_differ.py (visible-tweak diff, 86caa4bqp re-QA) ==="

# 6. Direct unit: a tweaked frame that DIFFERS from open → PASS.
assert_rc_and_grep 0 "differ" "frames_differ: changed frames pass" \
  -- python3 "$FRAMES_DIFFER" "$DIFFDIR/settings_open.png" "$DIFFDIR/settings_tweaked.png"

# 7. Direct unit: a byte-identical tweaked frame → FAIL (the bug class, isolated).
assert_rc_and_grep 1 "PIXEL-IDENTICAL" "frames_differ: identical frames fail" \
  -- python3 "$FRAMES_DIFFER" "$IDENTDIR/settings_open.png" "$IDENTDIR/settings_tweaked.png"

# 8. Direct unit: a missing frame → FAIL loud (a check that can't read its input is a failure).
assert_rc_and_grep 1 "not found" "frames_differ: missing frame fails loud" \
  -- python3 "$FRAMES_DIFFER" "$DIFFDIR/settings_open.png" "$TMP/does_not_exist.png"

echo "=== bootstrap_with_retry.sh (86cabtc83 — completion-marker gate + cold-cancel retry) ==="
# The wrapper must NOT report success unless BootstrapProject.Run finished end-to-end
# (logged "[BootstrapProject] complete" → Boot.unity re-baked). The 6000.4.11f1 cold-cache
# package-resolve was CANCELLED before Run() ran, yet the old wrapper reported success off
# the advisory -quit exit code → EditMode tested the STALE committed Boot.unity → 6 false-RED
# scene-presence failures. These cases pin the marker-gate + the broadened retry signature.
RETRY="$SCRIPTS/bootstrap_with_retry.sh"
# A fake Unity: ignores its args, writes a canned -logFile body + exit code chosen by env
# vars, so we can drive the wrapper deterministically with zero real Unity.
FAKE_UNITY="$TMP/fake_unity.sh"
cat > "$FAKE_UNITY" <<'FAKE'
#!/usr/bin/env bash
log=""
while [ $# -gt 0 ]; do
  if [ "$1" = "-logFile" ]; then log="$2"; shift 2; continue; fi
  shift
done
[ -n "$log" ] && printf '%b' "$FAKE_LOG_BODY" > "$log"
exit "${FAKE_EXIT:-0}"
FAKE
chmod +x "$FAKE_UNITY"
RETRY_PROJ="$TMP/retry_proj"; mkdir -p "$RETRY_PROJ/Library/PackageCache"

# Case A: Run() COMPLETED (marker present) + exit 0 → wrapper SUCCESS.
assert_rc_and_grep 0 "bootstrap COMPLETE" "bootstrap-retry: completion marker → success" \
  -- env FAKE_EXIT=0 FAKE_LOG_BODY='[BootstrapProject] start\nauthored stuff\n[BootstrapProject] complete\n' \
     bash -c "cd '$RETRY_PROJ' && bash '$RETRY' '$FAKE_UNITY' '$RETRY_PROJ' '$RETRY_PROJ/boot.log'"

# Case B (THE 86cabtc83 BUG): advisory exit 0 BUT no completion marker (the cold-cancel
# signature) → wrapper must NOT report success. It retries the transient flake, then gives
# up NON-ZERO — never a silent green. A non-re-baked scene MUST fail the gate. SETTLE_SECONDS=0
# keeps the test fast (the real CI value is the script default 20s).
assert_rc_and_grep 1 "no '[BootstrapProject] complete' marker" "bootstrap-retry: cold-cancel + no marker → NOT success (the 86cabtc83 bug)" \
  -- env SETTLE_SECONDS=0 FAKE_EXIT=0 FAKE_LOG_BODY='[Package Manager] Failed to resolve packages: operation cancelled.\nApplication will terminate with return code 1\n' \
     bash -c "cd '$RETRY_PROJ' && bash '$RETRY' '$FAKE_UNITY' '$RETRY_PROJ' '$RETRY_PROJ/boot.log'"

# Case C: a REAL compile error (no marker, no transient signature) → fail FAST, no retry.
assert_rc_and_grep 1 "not retrying (real error" "bootstrap-retry: real error → fail fast, no retry" \
  -- env FAKE_EXIT=1 FAKE_LOG_BODY='Assets/Foo.cs(3,5): error CS0103: name does not exist\nCompilation failed\n' \
     bash -c "cd '$RETRY_PROJ' && bash '$RETRY' '$FAKE_UNITY' '$RETRY_PROJ' '$RETRY_PROJ/boot.log'"

echo "=== capture_gate.sh / verify_pond_gate.sh (present-loop WEDGE hardening, wf_b92193a7-ba9) ==="
# The windowed capture launch intermittently HANGS at the first-frame present loop on the self-hosted
# runner. The hardening: a single rc==124-ONLY retry (re-launch ONCE on a timeout-hang), `timeout -k 15`
# (SIGKILL a SIGTERM-ignoring hung player), LAUNCH_TIMEOUT 300, and -logFile on capture_gate. These cases
# pin the RETRY LOGIC with a fake exe (zero Unity dependency): a fake that EXITS 124 makes `timeout` return
# 124 (timeout passes through a child's exit status when the child exits before the deadline), exercising
# the rc==124 branch deterministically without a real 300s hang. The actual present-wedge only manifests on
# the runner; what we CAN test here is "do we retry on 124, and ONLY on 124".
CAPTURE_GATE="$SCRIPTS/capture_gate.sh"
POND_GATE="$SCRIPTS/verify_pond_gate.sh"

# Capture-frame PNG writer (good content so frame_check passes on the success path).
CAP_PNG_HELPER="$TMP/_make_capture_pngs.py"
cat > "$CAP_PNG_HELPER" <<'PY'
import os, sys, struct, zlib
d = sys.argv[1]; n = int(sys.argv[2]) if len(sys.argv) > 2 else 2
os.makedirs(d, exist_ok=True)
def chunk(typ, data):
    return struct.pack(">I", len(data)) + typ + data + struct.pack(">I", zlib.crc32(typ+data)&0xFFFFFFFF)
def good(x, y):
    base = (x*3)%200+20
    if 20<=x<44 and 20<=y<44: return (240,230,120)
    return (base, base//2+30, 200-base//2)
def write_png(path, w=64, h=64):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            r,g,b = good(x,y); raw += bytes((r,g,b))
    with open(path,"wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",w,h,8,2,0,0,0))
                +chunk(b"IDAT",zlib.compress(bytes(raw)))+chunk(b"IEND",b""))
for i in range(n):
    write_png(os.path.join(d, "capture_%02d.png" % i))
PY

# Fake-exe factory for the WEDGE retry tests. Reads -captureDir/-logFile from its args.
# $1 = output path  $2 = behaviour:
#   "hang-then-pass" — attempt 1 exits 124 (no frames); attempt 2 writes good frames + exits 0  (retry RECOVERS)
#   "hang-always"    — every attempt exits 124, no frames  (retry exhausted → frame_check fails on 0 frames)
#   "fail-fast"      — attempt 1 exits 1 (a real non-zero gate failure), no frames  (must NOT retry)
# A per-exe counter file tracks attempt N across invocations.
make_wedge_exe() {
  local exe="$1" behaviour="$2"
  local counter="$exe.attempts"; rm -f "$counter"
  cat > "$exe" <<FAKE
#!/usr/bin/env bash
capdir=""
while [ \$# -gt 0 ]; do
  case "\$1" in
    -captureDir) capdir="\$2"; shift 2;;
    *) shift;;
  esac
done
n=\$(( \$(cat "$counter" 2>/dev/null || echo 0) + 1 )); echo "\$n" > "$counter"
case "$behaviour" in
  hang-then-pass)
    if [ "\$n" -eq 1 ]; then exit 124; fi
    python3 "$CAP_PNG_HELPER" "\$capdir" 2; exit 0;;
  hang-always) exit 124;;
  fail-fast)   exit 1;;
esac
FAKE
  chmod +x "$exe"
}

# assert_attempts <exe> <expected-count> <label> — read the per-exe counter file directly (the make_wedge_exe
# fake writes its run count to "<exe>.attempts"); proves the retry fired (or didn't) the exact right number.
assert_attempts() {
  local exe="$1" exp="$2" label="$3"
  local got; got="$(cat "$exe.attempts" 2>/dev/null || echo 0)"
  if [ "$got" = "$exp" ]; then ok "$label (ran ${got}×)"; else bad "$label — expected ${exp}× got ${got}×"; fi
}

# 1. capture_gate: a single timeout-hang RECOVERS on the one retry → PASS, and the exe ran TWICE.
make_wedge_exe "$TMP/cap_hang_then_pass.sh" "hang-then-pass"
assert_rc_and_grep 0 "CAPTURE GATE PASSED" "capture_gate: hang-then-pass recovers on retry" \
  -- bash "$CAPTURE_GATE" "$TMP/cap_hang_then_pass.sh" "$TMP/cap_htp_caps" 2 "$TMP/cap_htp.log"
assert_attempts "$TMP/cap_hang_then_pass.sh" 2 "capture_gate: hang-then-pass ran exactly twice (retry fired once)"

# 2. capture_gate: a persistent timeout-hang exhausts the ONE retry → FAIL (0 frames), exe ran exactly TWICE
#    (NOT a loop — exactly one retry).
make_wedge_exe "$TMP/cap_hang_always.sh" "hang-always"
assert_rc_and_grep 1 "found 0 frame(s)" "capture_gate: persistent hang fails after one retry (0 frames)" \
  -- bash "$CAPTURE_GATE" "$TMP/cap_hang_always.sh" "$TMP/cap_ha_caps" 2 "$TMP/cap_ha.log"
assert_attempts "$TMP/cap_hang_always.sh" 2 "capture_gate: persistent hang ran exactly twice (one retry, no infinite loop)"

# 3. THE load-bearing guard — capture_gate: a REAL non-zero gate failure (rc!=124) is NOT retried.
#    The exe ran exactly ONCE; retrying a real failure would waste a runner cycle / mask a render fail.
make_wedge_exe "$TMP/cap_fail_fast.sh" "fail-fast"
assert_rc 1 "capture_gate: real non-124 failure → no frames → gate fails" \
  -- bash "$CAPTURE_GATE" "$TMP/cap_fail_fast.sh" "$TMP/cap_ff_caps" 2 "$TMP/cap_ff.log"
assert_attempts "$TMP/cap_fail_fast.sh" 1 "capture_gate: real non-124 failure ran exactly ONCE (no retry on a real failure)"

# 4. verify_pond_gate: a real non-124 self-assert failure is NOT retried (ran exactly ONCE). The pond gate
#    treats a non-zero exe_rc as the verdict; only rc==124 (the present-wedge) earns the one retry.
make_wedge_exe "$TMP/pond_fail_fast.sh" "fail-fast"
assert_rc_and_grep 1 "POND CAPTURE GATE FAILED" "verify_pond: real non-124 self-assert fails the gate" \
  -- bash "$POND_GATE" "$TMP/pond_fail_fast.sh" "$TMP/pond_ff_caps" "$TMP/pond_ff.log"
assert_attempts "$TMP/pond_fail_fast.sh" 1 "verify_pond: real non-124 self-assert ran exactly ONCE (no retry on a real failure)"

# 5. verify_pond_gate: a single present-wedge (rc 124) retries ONCE (exe ran exactly twice). Both attempts
#    hang here (hang-always) so the gate still fails — but the RETRY COUNT proves the rc==124 branch fired.
make_wedge_exe "$TMP/pond_hang_always.sh" "hang-always"
assert_rc 1 "verify_pond: persistent present-wedge fails after one retry" \
  -- bash "$POND_GATE" "$TMP/pond_hang_always.sh" "$TMP/pond_ha_caps" "$TMP/pond_ha.log"
assert_attempts "$TMP/pond_hang_always.sh" 2 "verify_pond: present-wedge (rc 124) ran exactly twice (one retry)"

echo "=== ALL verify_*_gate.sh — uniform wedge-retry semantics (86cafzaeb) ==="
# 86cafzaeb adopted #189's hardening (LAUNCH_TIMEOUT 300, `timeout -k 15`, single rc==124-only
# retry with per-attempt stale-clear) on EVERY windowed verify gate — settings/loot/water/chop/
# sky were still on the old single-launch 120/180s shape; heldbelt + invdragghostpos shipped
# hardened from day one (86cahx2p5 / 86cafhgun). capture_gate + pond keep their dedicated #189
# recovery tests above; this loop pins the retry SEMANTICS uniformly per gate — the bug class,
# not the instance: (a) a REAL non-124 failure runs the exe exactly ONCE (retrying a real
# failure wastes a runner cycle / masks a genuine render fail), (b) a persistent timeout-hang
# (rc 124) retries exactly ONCE (two runs, never a loop) and the gate still FAILS. All seven
# wrappers share the `<exe> [capdir] [logfile]` CLI, so one loop covers them. A NEW verify gate
# wired into ci.yml must be appended to this list (the loop is the regression guard that keeps
# the hardened pattern uniform). Every gate prints the shared "CAPTURE GATE FAILED" token on
# its aggregate fail path, so the grep needle is uniform too.
for g in settings loot water chop sky heldbelt invdragghostpos placement mine boulder buildmenu boar heldwood swings weaponfind; do
  G="$SCRIPTS/verify_${g}_gate.sh"
  make_wedge_exe "$TMP/${g}_ff.sh" "fail-fast"
  assert_rc_and_grep 1 "CAPTURE GATE FAILED" "verify_${g}: real non-124 failure fails the gate" \
    -- bash "$G" "$TMP/${g}_ff.sh" "$TMP/${g}_ff_caps" "$TMP/${g}_ff.log"
  assert_attempts "$TMP/${g}_ff.sh" 1 "verify_${g}: real non-124 failure ran exactly ONCE (no retry on a real failure)"
  make_wedge_exe "$TMP/${g}_ha.sh" "hang-always"
  assert_rc_and_grep 1 "CAPTURE GATE FAILED" "verify_${g}: persistent 124-hang fails after one retry" \
    -- bash "$G" "$TMP/${g}_ha.sh" "$TMP/${g}_ha_caps" "$TMP/${g}_ha.log"
  assert_attempts "$TMP/${g}_ha.sh" 2 "verify_${g}: persistent 124-hang ran exactly TWICE (one retry, no loop)"
done

echo "=== verify_swings_gate.sh — evidence-presence check (86caynve9) ==="
# THE bug class this guards, and why the exit code alone cannot: SwingVerifyCapture DELIBERATELY
# makes a missing precondition LOUD IN THE LOG rather than RED. Its own source says so —
# `_releaseOk` "defaults TRUE only so a SKIPPED pass cannot red the whole gate; a skip is LOUD in
# the log instead" (SwingVerifyCapture.cs:593) — and the fold pass (:224), the two-hand grip pass
# (:317), the left-arm pin (:276), the held-weapon force (:309) and the F9 panel (:749) each warn
# verbatim "do NOT read a PASS here as proof …" while leaving their criterion flag at its `true`
# initialiser. So `pass = allRouted && meshStayed && foldOk && gripOk && _releaseOk` (:564) can be
# TRUE on a build where the palm was never measured once — exit 0, green CI, and #354's headline
# regression guard silently gating nothing. CI-wiring the exit code WITHOUT converting those
# warnings into a red would ship exactly the rubber-stamp this ticket exists to remove.
#
# TWO-SIDED, on the #363 model: every case below states the expected verdict BEFORE the run, the
# NEGATIVES are the real defect shapes (not synthetic strings), and case S3 is the load-bearing
# CONTRASTIVE PAIR — it proves the false-green is real by showing that BOTH checks the
# verify_boar_gate.sh template carries (exit code + frame backstop) PASS that same input, so the
# evidence check is the only thing standing between it and a green.
#
# Fixture provenance: every token in the PASS body is copied from a REAL passing -verifySwings
# Player.log (Drew's round-5 soak4-swings run: 12/12 frames, `releaseOk=True => PASS=True`), floats
# included — the fixtures keep that run's COMMA decimal separator on purpose, so these cases also
# pin the gate's locale-invariance (a numeric needle would false-red on a Danish-locale runner).
SWINGS_GATE="$SCRIPTS/verify_swings_gate.sh"

# Writes the 12 frames a full PASS run produces, under their real names (the gate's per-attempt
# stale-clear globs swing_*.png, so the names are load-bearing).
SWING_PNG_HELPER="$TMP/_make_swing_pngs.py"
cat > "$SWING_PNG_HELPER" <<'PY'
import os, sys, struct, zlib
d = sys.argv[1]
names = ["swing_axe", "swing_pickaxe", "swing_dagger", "swing_spear", "swing_sword",
         "swing_pickaxe_fold", "swing_pickaxe_fold_side", "swing_pickaxe_twohand",
         "swing_pickaxe_twohand_front", "swing_pickaxe_panel", "swing_pickaxe_release",
         "swing_pickaxe_release_side"]
n = int(sys.argv[2]) if len(sys.argv) > 2 else len(names)
os.makedirs(d, exist_ok=True)
def chunk(typ, data):
    return struct.pack(">I", len(data)) + typ + data + struct.pack(">I", zlib.crc32(typ+data)&0xFFFFFFFF)
def write_png(path, w=64, h=64):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            base = (x*3) % 200 + 20
            raw += bytes((base, base//2+30, 200-base//2))
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                +chunk(b"IDAT", zlib.compress(bytes(raw)))+chunk(b"IEND", b""))
for nm in names[:n]:
    write_png(os.path.join(d, nm + ".png"))
PY

# write_swings_log <outfile> <mode> — a token-faithful -verifySwings Player.log body.
# Modes are the real shapes: pass / skip-ik / skip-bones / skip-guard / palm-fail / release-fail / truncated.
write_swings_log() {
  local out="$1" mode="$2"
  : > "$out"
  if [ "$mode" = "skip-guard" ]; then
    echo "[SwingVerifyCapture] agent on NavMesh: True castaway=True animator=False smr=True" >> "$out"
  else
    echo "[SwingVerifyCapture] agent on NavMesh: True castaway=True animator=True smr=True" >> "$out"
  fi
  for c in "0 (axe)" "1 (pickaxe)" "2 (dagger)" "3 (spear)" "4 (sword)"; do
    echo "[SwingVerifyCapture] fired swing class=$c routed=True" >> "$out"
  done
  if [ "$mode" = "truncated" ]; then return 0; fi     # died before any verdict was reported
  if [ "$mode" = "skip-guard" ]; then
    # 86caynve9 (Drew's #369 review, comment 5136309565) — the WORST shape, and the reason the four
    # in-block REQUIRED_NEEDLES may never be pruned as "redundant with the summary line". Models the
    # PRE-else `if (castaway != null && animator != null)` guard being false with castaway NON-null:
    # every measurement is skipped with ZERO log output — NO "fold pass SKIPPED", NO "two-hand grip pass
    # SKIPPED", NO "[swing-release] SKIPPED", NO "no CastawayLeftArmHaftIk" — so EVERY ABSENT needle is
    # silent and the ABSENT half of Check 2 cannot see this run at all. allRouted still computes TRUE
    # (SwingVerifyCapture.cs:164 = `castaway != null`) and foldOk/gripOk/_releaseOk keep their `true`
    # INITIALISERS, so the summary line below is a full-green PASS on a run that measured nothing, and
    # the exe exits 0. ONLY the four in-block presence needles (pinEngaged / palmMeasured /
    # leftPalmOnHaft / rightWristOnHaft) are missing here. If a future cleanup prunes them because
    # "releaseOk=True already covers it", THIS case greens and the gate is a rubber stamp.
    echo "[SwingVerifyCapture] verification complete -> caps allRouted=True meshStayed=True foldOk=True gripOk=True releaseSettleFrames=-1 releaseBudgetFrames=-1 releaseOk=True => PASS=True" >> "$out"
    return 0
  fi
  if [ "$mode" = "skip-bones" ]; then
    # SwingVerifyCapture.cs:224 — Hips/Head unresolved, so the WHOLE fold+grip+panel+release block
    # never runs and foldOk/gripOk/_releaseOk keep their `true` initialisers.
    echo "[SwingVerifyCapture] fold pass SKIPPED — mixamorig:Hips/Head not found on the live rig; the mine-pose evidence is MISSING from this run (do not read a PASS here as proof the fold is fixed)." >> "$out"
    echo "[SwingVerifyCapture] verification complete -> caps allRouted=True meshStayed=True foldOk=True gripOk=True releaseSettleFrames=-1 releaseBudgetFrames=-1 releaseOk=True => PASS=True" >> "$out"
    return 0
  fi
  if [ "$mode" = "skip-ik" ]; then
    # :276 / :628 — the left-arm haft PIN is absent from the build, so both the grip pass's palm
    # figures and the release pass are meaningless; the component says so and passes anyway.
    echo "[swing-twohand] no CastawayLeftArmHaftIk in the shipped scene — the LEFT-HAND PIN this round delivers is ABSENT from this build. Every palm figure below would then be the clip's own unpinned hand; do NOT read a PASS as proof the left hand was moved." >> "$out"
    echo "[swing-twohand] engaged=True (peak seat weight 1,00 > 0,50) pinEngaged=False (peak pin weight 0,00 > 0,50) palmMeasured=True leftPalmOnHaft=True (0,239 SW) rightWristOnHaft=True (0,001 <= 0,30 SW) => gripOk=True." >> "$out"
    echo "[swing-release] SKIPPED — no CastawayLeftArmHaftIk in the shipped scene, so there is nothing to release; do NOT read the PASS above as proof the left arm lets go." >> "$out"
    echo "[SwingVerifyCapture] verification complete -> caps allRouted=True meshStayed=True foldOk=True gripOk=True releaseSettleFrames=-1 releaseBudgetFrames=-1 releaseOk=True => PASS=True" >> "$out"
    return 0
  fi
  echo "[SwingVerifyCapture] pickaxe fold: peakTilt=42,1deg <= 50deg ceiling => foldOk=True" >> "$out"
  echo "[swing-twohand] LEFT-ARM PIN: present=True peak weight 1,00 solved 82/156 frames, REACHING on 74, pole-fallback on 0" >> "$out"
  case "$mode" in
    palm-fail)
      # The AC2 mutation shape: zero the left-arm pin weight and the palm leaves the haft. Round 3
      # measured 0,615 SW = 28,2 cm here — the figure the Sponsor rejected by eye.
      echo "[swing-twohand] engaged=True (peak seat weight 1,00 > 0,50) pinEngaged=False (peak pin weight 0,00 > 0,50) palmMeasured=True leftPalmOnHaft=False (0,615 SW = 28,2 cm <= 0,293 SW = 13,0 cm, the mesh-measured touch bound) rightWristOnHaft=True (0,001 <= 0,30 SW) => gripOk=False." >> "$out"
      echo "[swing-release] crossfade OUT measured 6 frames; pin weight fell to <= 0,02 at +5 frames (budget 11) => releaseOk=True" >> "$out"
      echo "[SwingVerifyCapture] verification complete -> caps allRouted=True meshStayed=True foldOk=True gripOk=False releaseSettleFrames=5 releaseBudgetFrames=11 releaseOk=True => PASS=False" >> "$out";;
    release-fail)
      echo "[swing-twohand] engaged=True (peak seat weight 1,00 > 0,50) pinEngaged=True (peak pin weight 1,00 > 0,50) palmMeasured=True leftPalmOnHaft=True (0,239 SW = 10,6 cm <= 0,293 SW = 13,0 cm) rightWristOnHaft=True (0,001 <= 0,30 SW) => gripOk=True." >> "$out"
      echo "[swing-release] FAIL — crossfadeOutFrames=6 settleFrames=28 (peak pin weight 1,00). A NEGATIVE settle means the arm never let go, which IS the Sponsor's reported defect." >> "$out"
      echo "[SwingVerifyCapture] verification complete -> caps allRouted=True meshStayed=True foldOk=True gripOk=True releaseSettleFrames=28 releaseBudgetFrames=11 releaseOk=False => PASS=False" >> "$out";;
    *)
      echo "[swing-twohand] engaged=True (peak seat weight 1,00 > 0,50) pinEngaged=True (peak pin weight 1,00 > 0,50) palmMeasured=True leftPalmOnHaft=True (0,239 SW = 10,6 cm <= 0,293 SW = 13,0 cm) rightWristOnHaft=True (0,001 <= 0,30 SW) => gripOk=True." >> "$out"
      echo "[swing-release] crossfade OUT measured 6 frames; pin weight fell to <= 0,02 at +5 frames (budget 11) => releaseOk=True" >> "$out"
      echo "[SwingVerifyCapture] verification complete -> caps allRouted=True worstMeshGap=1,94u meshStayed=True pickaxePeakTilt=42,1deg foldOk=True worstLeftPALM=0,239SW=10,6cm palmMeasured=True pinPeakWeight=1,00 gripOk=True releaseSettleFrames=5 releaseBudgetFrames=11 releaseOk=True => PASS=True" >> "$out";;
  esac
}

# make_swings_exe <exe> <log-body-file> <exit-rc> [frame-count]
make_swings_exe() {
  local exe="$1" body="$2" rc="$3" frames="${4:-12}"
  local counter="$exe.attempts"; rm -f "$counter"
  cat > "$exe" <<FAKE
#!/usr/bin/env bash
capdir=""; logf=""
while [ \$# -gt 0 ]; do
  case "\$1" in
    -captureDir) capdir="\$2"; shift 2;;
    -logFile)    logf="\$2"; shift 2;;
    *) shift;;
  esac
done
n=\$(( \$(cat "$counter" 2>/dev/null || echo 0) + 1 )); echo "\$n" > "$counter"
python3 "$SWING_PNG_HELPER" "\$capdir" $frames
cat "$body" >> "\$logf"
exit $rc
FAKE
  chmod +x "$exe"
}

# S1 (positive) — a faithful PASS run: exit 0, 12 real frames, every criterion reported True and no
# skip warning. PRE-REGISTERED: gate exits 0 and prints SWINGS CAPTURE GATE PASSED.
write_swings_log "$TMP/swings_pass.body" "pass"
make_swings_exe "$TMP/swings_pass.sh" "$TMP/swings_pass.body" 0
assert_rc_and_grep 0 "SWINGS CAPTURE GATE PASSED" \
  "verify_swings S1: faithful PASS run (all criteria True, no skips) → GREEN" \
  -- bash "$SWINGS_GATE" "$TMP/swings_pass.sh" "$TMP/swings_pass_caps" "$TMP/swings_pass.log"

# S2 — locale invariance, stated separately because it is the reason the needles are boolean-only.
# S1's fixture carries the shipped Danish-locale COMMA decimals (0,239SW / 42,1deg) throughout; a
# numeric needle would red that build on the real runner while greening an en-US one.
assert_rc_and_grep 0 "evidence OK      : 'leftPalmOnHaft=True'" \
  "verify_swings S2: comma-decimal (Danish-locale) floats do NOT disturb the boolean needles" \
  -- bash "$SWINGS_GATE" "$TMP/swings_pass.sh" "$TMP/swings_locale_caps" "$TMP/swings_locale.log"

# S3 (THE load-bearing negative + contrastive pair) — the left-arm haft IK is ABSENT from the build,
# so the palm figures describe the clip's own unpinned hand and the release pass never ran. The
# component says exactly that in two warnings and STILL exits 0 with PASS=True, by design.
# PRE-REGISTERED: gate exits 1, naming the missing-pin evidence; and the two checks the
# verify_boar_gate.sh template carries would BOTH have greened it (proven in S3b/S3c).
write_swings_log "$TMP/swings_skipik.body" "skip-ik"
make_swings_exe "$TMP/swings_skipik.sh" "$TMP/swings_skipik.body" 0
assert_rc_and_grep 1 "evidence SKIPPED : 'no CastawayLeftArmHaftIk'" \
  "verify_swings S3: exit-0 run whose left-arm PIN is absent → RED (the false-green this gate closes)" \
  -- bash "$SWINGS_GATE" "$TMP/swings_skipik.sh" "$TMP/swings_skipik_caps" "$TMP/swings_skipik.log"
# S3b — the SAME input under an exit-code-only gate: the exe itself returns 0.
assert_rc 0 "verify_swings S3b: contrastive — that same input EXITS 0 (an exit-code-only gate greens it)" \
  -- bash "$TMP/swings_skipik.sh" -captureDir "$TMP/swings_ctl_caps" -logFile "$TMP/swings_ctl.log"
# S3c — and its frames are real content, so the frame backstop greens it too. Exit code + frames are
# the ENTIRE gate in the boar/mine template; only the evidence check separates S3 from S1.
assert_rc_and_grep 0 "CAPTURE GATE PASSED" \
  "verify_swings S3c: contrastive — that same input's frames PASS frame_check (the backstop greens it too)" \
  -- python3 "$FRAME_CHECK" "$TMP/swings_ctl_caps" --min-frames 5

# S4 — the OTHER skip shape: Hips/Head unresolved, so the whole fold+grip+panel+release block never
# runs and all three flags keep their `true` initialisers. PRE-REGISTERED: exit 1, naming the fold skip.
write_swings_log "$TMP/swings_skipbones.body" "skip-bones"
make_swings_exe "$TMP/swings_skipbones.sh" "$TMP/swings_skipbones.body" 0 5
assert_rc_and_grep 1 "evidence SKIPPED : 'fold pass SKIPPED'" \
  "verify_swings S4: exit-0 run whose fold/grip/release block never ran → RED" \
  -- bash "$SWINGS_GATE" "$TMP/swings_skipbones.sh" "$TMP/swings_skipbones_caps" "$TMP/swings_skipbones.log"

# S4b (86caynve9, Drew's #369 review comment 5136309565) — the SILENT sibling of S4, and the anti-pruning
# guard on the four in-block REQUIRED_NEEDLES. S4's skipped block at least SHOUTS ("fold pass SKIPPED"), so
# the ABSENT half of Check 2 reds it. This shape shouts NOTHING: the whole measurement block is skipped by
# the `castaway != null && animator != null` guard with castaway NON-null, so no skip warning of any kind is
# written, EVERY ABSENT needle is silent, `allRouted` still computes TRUE and the summary line reports
# `foldOk=True gripOk=True releaseOk=True => PASS=True` on a run that measured nothing. The ONLY detectors
# left are the four in-block presence needles. PRE-REGISTERED: exit 1, naming a load-bearing in-block
# criterion (pinEngaged) as MISSING — and it must be an `evidence MISSING`, never an `evidence SKIPPED`,
# because proving the PRESENCE half is sufficient ALONE is the entire point of this case. Prune any of the
# four needles and this test greens; that is the regression it exists to catch.
write_swings_log "$TMP/swings_skipguard.body" "skip-guard"
make_swings_exe "$TMP/swings_skipguard.sh" "$TMP/swings_skipguard.body" 0
assert_rc_and_grep 1 "evidence MISSING : 'pinEngaged=True'" \
  "verify_swings S4b: exit-0 run whose rig-guard skipped every check SILENTLY (no skip warning at all) → RED via the in-block needles alone" \
  -- bash "$SWINGS_GATE" "$TMP/swings_skipguard.sh" "$TMP/swings_skipguard_caps" "$TMP/swings_skipguard.log"

# S5 — the AC2 mutation shape: zero the left-arm pin weight, the palm leaves the haft at the round-3
# figure the Sponsor rejected (0,615 SW = 28,2 cm), gripOk=False, exe exits 1.
# PRE-REGISTERED: exit 1, and the palm criterion is named as missing.
write_swings_log "$TMP/swings_palm.body" "palm-fail"
make_swings_exe "$TMP/swings_palm.sh" "$TMP/swings_palm.body" 1
assert_rc_and_grep 1 "evidence MISSING : 'leftPalmOnHaft=True'" \
  "verify_swings S5: left palm off the haft (the round-3 defect) → RED, naming the palm criterion" \
  -- bash "$SWINGS_GATE" "$TMP/swings_palm.sh" "$TMP/swings_palm_caps" "$TMP/swings_palm.log"
assert_attempts "$TMP/swings_palm.sh" 1 "verify_swings S5: a real palm failure ran the exe ONCE (never retried)"

# S6 — the round-5 term: the left arm does not let go inside its budget. PRE-REGISTERED: exit 1,
# naming the release criterion. This is the #354 half that has no other automated coverage at all.
write_swings_log "$TMP/swings_release.body" "release-fail"
make_swings_exe "$TMP/swings_release.sh" "$TMP/swings_release.body" 1
assert_rc_and_grep 1 "evidence MISSING : 'releaseOk=True'" \
  "verify_swings S6: left arm never releases (the round-4 soak defect) → RED, naming the release term" \
  -- bash "$SWINGS_GATE" "$TMP/swings_release.sh" "$TMP/swings_release_caps" "$TMP/swings_release.log"

# S7 — a run that dies before reporting a verdict but still exits 0 (frames present). Without the
# "verification complete" needle this is indistinguishable from a pass. PRE-REGISTERED: exit 1.
write_swings_log "$TMP/swings_trunc.body" "truncated"
make_swings_exe "$TMP/swings_trunc.sh" "$TMP/swings_trunc.body" 0
assert_rc_and_grep 1 "evidence MISSING : '[SwingVerifyCapture] verification complete'" \
  "verify_swings S7: exit-0 run that never reached its verdict → RED" \
  -- bash "$SWINGS_GATE" "$TMP/swings_trunc.sh" "$TMP/swings_trunc_caps" "$TMP/swings_trunc.log"

# S8 — a run that produces a perfect log but almost no frames still reds on the backstop, so the
# evidence check cannot substitute for real swapchain content (the #287 false-empty class).
make_swings_exe "$TMP/swings_fewframes.sh" "$TMP/swings_pass.body" 0 2
assert_rc_and_grep 1 "SWINGS CAPTURE GATE FAILED" \
  "verify_swings S8: perfect log but only 2 frames → RED (evidence check does not replace the frame backstop)" \
  -- bash "$SWINGS_GATE" "$TMP/swings_fewframes.sh" "$TMP/swings_few_caps" "$TMP/swings_few.log"

echo "=== gate launch-mode invariant (86cag93zb — headless RT-readback vs windowed overlay) ==="
# THE bug class this guards: a HEADLESS-converted scene-content gate silently reverting to a windowed
# launch (or a windowed overlay gate losing its window) — either way the CI capture step would launch
# the exe in the WRONG mode and the fix would regress unnoticed. The 4 scene-content gates render
# Camera.main / a dedicated cam into an offscreen RenderTexture (SubmitRenderRequest) and MUST launch
# -batchmode (NO -screen-fullscreen 0); the OVERLAY + soak-fragile gates still need a real swapchain and
# MUST keep -screen-fullscreen 0 (their IMGUI/UI-Toolkit overlay never composites into a camera RT —
# backbuffer capture is dead headless). Static grep over the LAUNCH line only ('^\s*-batchmode' can't
# match a '#'-prefixed comment; '-screen-fullscreen 0' appears only in the windowed launch flag line) —
# zero Unity dependency, runs every PR in the license-free structure job. A NEW gate must be added to the
# matching list here (mirrors the wedge-retry loop above — this is the launch-mode regression guard).
#
# ⚠ RESIDUAL BLIND SPOTS of BOTH asserts below (86caynve7 AC3) — DOCUMENTED, deliberately NOT chased.
# They are the price of a static grep in a deliberately Unity-free job; the runtime-truth guard for
# what actually reaches the exe is the capture gate itself. Do not read either assert as complete.
#   1. LINE-START IS NOT LAUNCH-POSITION. `^[[:space:]]*` pins a flag to the start of SOME line, not
#      to the `timeout … "$EXE" \` continuation it belongs to. A stale flags block, a commented-out
#      -launch leftover, or a second launch site that drops the flag would still green. Latent only
#      today: each windowed gate has 3 `"$EXE"` references, of which 2 are `-z`/`-f` guards and
#      exactly 1 is a real launch. The same axis cuts the other way on an anchored NEGATIVE — a
#      `-batchmode` appended MID-line (`-screen-fullscreen 0 -batchmode \`) escapes the anchor and
#      greens. Anchoring the windowed negative is nonetheless required, because the unanchored
#      alternative reds all 8 windowed gates on their header prose (see the asymmetry note below).
#   2. STATIC IS NOT RUNTIME. Nothing here proves the flag reaches the exe — a conditional launch, an
#      `EXTRA_FLAGS` var, or a `$@` passthrough all defeat it.
assert_launch_headless() { # <script-name>
  local s="$SCRIPTS/$1"
  # ⚠ LEAVE THE NEGATIVE HALF BELOW UNANCHORED — DELIBERATELY. This is NOT an oversight and NOT a
  # symmetry bug to "clean up" against the anchored negative in assert_launch_windowed. Verbatim
  # from the 86caynve7 source review (#353 comment 5109934787 §3): "Anchoring L849 to
  # `^[[:space:]]*` would *introduce* a false-PASS, not remove one: a headless gate that appended
  # the flag mid-line (`-batchmode -screen-fullscreen 0 \`) would stop being detected and would
  # green. The current unanchored form is the conservative one. Any follow-up should record 'leave
  # L849 unanchored deliberately', not 'anchor L849 for symmetry'." Proven empirically on this
  # branch: appending `-screen-fullscreen 0` mid-line to verify_chop_gate.sh's launch line REDS the
  # unanchored form and FALSE-PASSES an anchored one. Cheap to re-check — it is a 2-line mutation.
  if grep -qE '^[[:space:]]*-batchmode' "$s" && ! grep -q -- '-screen-fullscreen 0' "$s"; then
    ok "launch-mode: $1 launches -batchmode (headless RT-readback), no windowed swapchain"
  else
    bad "launch-mode: $1 MUST launch -batchmode and NOT -screen-fullscreen 0 (86cag93zb headless conversion regressed)"
  fi
}
assert_launch_windowed() { # <script-name>
  local s="$SCRIPTS/$1"
  # ANCHORED (86caxj8zw, #344 NIT): the needle must match a LAUNCH LINE, not header PROSE. An
  # unanchored `grep -q -- '-screen-fullscreen 0'` false-PASSED verify_boar_gate.sh — the only
  # windowed gate whose header comment also names the flag (2 hits vs the other seven's 1) — so
  # deleting its real launch flags left the assert GREEN, exactly the headless conversion this
  # loop exists to red. `^[[:space:]]*` pins the flag to the start of a continuation line (all 8
  # windowed gates write it as the first token of the flags line), which a `#`-prefixed comment
  # can never satisfy. Mirrors the already-anchored '^[[:space:]]*-batchmode' above.
  #
  # TWO-SIDED (86caynve7): the presence half above is not sufficient on its own. Until this ticket
  # the assert tested ONLY for presence of the windowed flag, so a gate carrying BOTH `-batchmode`
  # AND the window flags stayed GREEN — mutation-proven on verify_boar_gate.sh with `-batchmode`
  # added as its own flag line and the window flags left intact: `134 passed, 0 failed`, exit 0,
  # `[ OK ] launch-mode: verify_boar_gate.sh keeps -screen-fullscreen 0 on the launch line`. That
  # mutation is exactly the regression this loop exists to red: `-batchmode` kills
  # ScreenCapture.CaptureScreenshot + WaitForEndOfFrame, so the gate captures BLACK FRAMES while
  # its logic half still exits 0 (the #287 false-empty class). The negative half below closes it.
  #
  # ⚠ THE ASYMMETRY WITH assert_launch_headless IS DELIBERATE — do NOT "fix" it. THIS side's
  # negative must be ANCHORED; the HEADLESS side's negative must stay UNANCHORED (see the verbatim
  # do-not-anchor note in that function). Measured on origin/main @ 840a1c6, re-measured on this
  # branch: all 8 windowed gates name `-batchmode` in header PROSE — 1 unanchored hit each for
  # settings/loot/water/invdragghostpos/pond/buildmenu, 2 for weaponset/boar — but 0 of them have
  # an ANCHORED hit, so an anchored negative reds NONE of them today while an unanchored one would
  # red all eight immediately. The 8 headless gates carry 0 `-screen-fullscreen 0` hits of ANY
  # kind, and for a negation unanchored is the conservative direction. They are not symmetric, and
  # not by accident. Re-measure both counts before touching either needle.
  if grep -qE '^[[:space:]]*-screen-fullscreen 0' "$s" && ! grep -qE '^[[:space:]]*-batchmode' "$s"; then
    ok "launch-mode: $1 keeps -screen-fullscreen 0 and adds no -batchmode on the launch line (overlay/soak-fragile gate needs a window)"
  else
    bad "launch-mode: $1 MUST keep -screen-fullscreen 0 on its LAUNCH LINE **AND** MUST NOT add -batchmode there (its IMGUI/UI-Toolkit overlay is dead headless — an added -batchmode yields BLACK frames while the logic half still exits 0; a header-comment mention of either flag does not count)"
  fi
}
HEADLESS_GATES=(capture_gate.sh verify_chop_gate.sh verify_heldbelt_gate.sh verify_sky_gate.sh
                verify_placement_gate.sh verify_mine_gate.sh verify_boulder_gate.sh verify_heldwood_gate.sh)
# verify_boar_gate.sh (86cavg2k1 NIT 2) belongs on the WINDOWED side for the same reason as
# verify_weaponset_gate.sh: BoarVerifyCapture uses ScreenCapture.CaptureScreenshot + WaitForEndOfFrame,
# both dead under -batchmode — a "helpful" headless conversion would silently produce black frames while
# the logic half still exited 0 (the #287 false-empty class). This entry reds that conversion.
# verify_swings_gate.sh (86caynve9) is WINDOWED on BOTH halves of the boundary sentence:
# SwingVerifyCapture captures via ScreenCapture.CaptureScreenshot (SwingVerifyCapture.cs:962 - a
# BACKBUFFER read, dead under -batchmode) AND its F9 mine-seat pass photographs a screen-space IMGUI
# OVERLAY (swing_pickaxe_panel.png), which never composites into a camera RenderTexture. A "helpful"
# headless conversion would silently produce BLACK frames while the logic half still exited 0 (the
# #287 false-empty class). This entry reds that conversion.
#
# verify_weaponfind_gate.sh (86cah7y5b) registers WINDOWED for the same reason, and it is registered HERE
# rather than on #351's own branch because the two changes never touched the same file: the glob-driven
# gate-wiring loop below (86cav8y74) landed on main AFTER #351 forked, so git merged this file with ZERO
# textual conflict while the loop's launch-mode-registration half went red on the new wrapper - a SEMANTIC
# collision the merge itself cannot surface. Measured on the merged tree, not assumed: the wrapper carries
# an ANCHORED '-screen-fullscreen 0' on its launch line (verify_weaponfind_gate.sh:63) and ZERO anchored
# '-batchmode' hits, so it satisfies BOTH halves of the two-sided assert above (86caynve7). It must stay
# windowed: WeaponFindVerifyCapture uses ScreenCapture.CaptureScreenshot + WaitForEndOfFrame, and
# weaponfind_side.png is the side-profile silhouette a human eyeballs before review.
WINDOWED_GATES=(verify_settings_gate.sh verify_loot_gate.sh verify_water_gate.sh
                verify_invdragghostpos_gate.sh verify_pond_gate.sh verify_weaponset_gate.sh
                verify_buildmenu_gate.sh verify_boar_gate.sh verify_swings_gate.sh
                verify_weaponfind_gate.sh)
for s in "${HEADLESS_GATES[@]}"; do assert_launch_headless "$s"; done
for s in "${WINDOWED_GATES[@]}"; do assert_launch_windowed  "$s"; done

echo "=== every verify_*_gate.sh is CI-WIRED + launch-mode-registered (86cav8y74) ==="
# THE BUG CLASS THIS GUARDS: a capture gate that is authored, self-asserting, and PASSES when run by hand
# — but that ci.yml never invokes, so it gates NOTHING and a real regression ships behind green CI. This is
# not hypothetical: `-verifySwings` (SwingVerifyCapture.cs) has ZERO hits under .github/ as of 86cav8y74 —
# it has passed manually since PR #327 and has never blocked a merge. -verifyMine (#299), -verifyBoulder
# (#303) and -verifyBoar (86cavg2k1 NIT 2) each spent weeks in exactly that state before someone noticed by
# grep. Both gaps THIS ticket closes are the same shape one layer down (a gate covering stone/iron but not
# wood). So: make the omission MECHANICAL rather than grep-when-you-remember.
#   (a) every wrapper must be INVOKED in ci.yml — an unwired wrapper is dead weight pretending to be a gate;
#   (b) every wrapper must appear in exactly one launch-mode list above — so its -batchmode-vs-windowed
#       choice is pinned and a "helpful" conversion reds (the #287 false-empty class).
# Deliberately scoped to the WRAPPERS, not to every -verify* flag: several flags (-verifyAxe,
# -verifyHeldPickaxe, -verifyCastaway, -verifySwings…) are author-run Self-Test evidence BY DESIGN and have
# no wrapper — asserting on flags would demand a CI gate for each, which is a scope call, not a defect.
# Adding a wrapper is the act that declares "this should gate", and that is what this check holds you to.
ALL_LAUNCH_REGISTERED=("${HEADLESS_GATES[@]}" "${WINDOWED_GATES[@]}")
CI_YML="$ROOT/.github/workflows/ci.yml"
if [ ! -f "$CI_YML" ]; then
  bad "gate-wiring: ci.yml not found at $CI_YML"
else
  for path in "$SCRIPTS"/verify_*_gate.sh; do
    g="$(basename "$path")"
    if grep -qF "scripts/$g" "$CI_YML"; then
      ok "gate-wiring: $g is invoked by ci.yml"
    else
      bad "gate-wiring: $g exists but ci.yml NEVER invokes it — it gates nothing (the -verifySwings class). Wire it into the capture job or delete it."
    fi
    reg=0
    for r in "${ALL_LAUNCH_REGISTERED[@]}"; do [ "$r" = "$g" ] && reg=1; done
    if [ "$reg" -eq 1 ]; then
      ok "gate-wiring: $g is registered in a launch-mode list"
    else
      bad "gate-wiring: $g is not in HEADLESS_GATES or WINDOWED_GATES — its launch mode is unpinned (a headless/windowed flip would regress silently)"
    fi
  done
fi

echo "=== parse_test_results.py skip-handling (86camz787 — PlayMode advisory→required) ==="
# THE bug class this guards: the shared NUnit result gate must treat SKIPPED/IGNORED
# tests as PASS on the PlayMode job (--allow-skips) while STILL reddening on a real
# failure — AND that skip-tolerance must NOT leak into the strict EditMode (required)
# gate. Unity rolls a run carrying [Ignore]-skips up to a compound run-level
# result="Skipped:Ignored"; the strict result==Passed check reds it even at failed==0
# (proven live: main run 29683732044). Zero Unity dependency — synthetic <test-run> XMLs.
PARSE="$SCRIPTS/parse_test_results.py"
# mk_testrun_xml <file> <result> <total> <passed> <failed> <inconclusive> <skipped>
mk_testrun_xml() {
  printf '<?xml version="1.0" encoding="utf-8"?>\n<test-run id="2" testcasecount="%s" result="%s" total="%s" passed="%s" failed="%s" inconclusive="%s" skipped="%s"><test-suite type="TestSuite" result="%s"><test-case name="t1" result="Passed"/></test-suite></test-run>\n' \
    "$3" "$2" "$3" "$4" "$5" "$6" "$7" "$2" > "$1"
}
PT="$TMP/pt"; mkdir -p "$PT"
# The core fix: --allow-skips greens a Skipped:Ignored run at failed==0.
mk_testrun_xml "$PT/skips.xml" "Skipped:Ignored" 302 289 0 0 13
assert_rc_and_grep 0 "TEST GATE PASSED" \
  "parse --allow-skips: Skipped:Ignored + failed=0 → GREEN" \
  -- python3 "$PARSE" --allow-skips "$PT/skips.xml" PlayMode
# Regression guard #1: --allow-skips must NOT hide a real failure.
mk_testrun_xml "$PT/fail.xml" "Failed:Child" 302 286 3 0 13
assert_rc_and_grep 1 "TEST GATE FAILED" \
  "parse --allow-skips: failed>0 → still RED" \
  -- python3 "$PARSE" --allow-skips "$PT/fail.xml" PlayMode
# Regression guard #2: inconclusive still reds under --allow-skips (Assert.Inconclusive == hard red).
mk_testrun_xml "$PT/inconc.xml" "Inconclusive" 302 289 0 2 11
assert_rc_and_grep 1 "TEST GATE FAILED" \
  "parse --allow-skips: inconclusive>0 → still RED" \
  -- python3 "$PARSE" --allow-skips "$PT/inconc.xml" PlayMode
# Regression guard #3: an empty run (asmdef didn't load) still reds even with --allow-skips.
mk_testrun_xml "$PT/empty.xml" "Skipped" 0 0 0 0 0
assert_rc 1 "parse --allow-skips: total=0 empty run → still RED" \
  -- python3 "$PARSE" --allow-skips "$PT/empty.xml" PlayMode
# A normal all-pass run is green with the flag too.
mk_testrun_xml "$PT/pass.xml" "Passed" 302 302 0 0 0
assert_rc_and_grep 0 "TEST GATE PASSED" \
  "parse --allow-skips: all-Passed → GREEN" \
  -- python3 "$PARSE" --allow-skips "$PT/pass.xml" PlayMode
# EditMode (required) gate contract UNCHANGED: WITHOUT the flag a skip-run stays RED,
# so the fix cannot silently relax the required gate.
assert_rc_and_grep 1 "TEST GATE FAILED" \
  "parse strict (no flag): Skipped:Ignored → still RED (EditMode required gate unchanged)" \
  -- python3 "$PARSE" "$PT/skips.xml" EditMode
assert_rc_and_grep 0 "TEST GATE PASSED" \
  "parse strict (no flag): all-Passed → GREEN (EditMode normal path unchanged)" \
  -- python3 "$PARSE" "$PT/pass.xml" EditMode

echo "=== check_docs_markup.sh — leaked tool-call markup in committed *.md (86cayxtw8) ==="
# THE BUG CLASS: an authoring agent's own tool-call closing tags land in a doc's BODY,
# always as a bare tag alone on a line at column 0, always at the file's end. Five
# committed *.md carried it at b9abf7b and every one had PASSED a peer review — the
# blind spot is that reviewers read diff hunks, never the last two lines.
#
# Both DIRECTIONS are proven here, per team/TESTING_BAR.md § "Doc-staleness greps"
# ("a staleness grep is not keepable until its RED has been DEMONSTRATED"): the guard
# must go RED on a real offender AND GREEN on a clean tree. A guard proven only in the
# passing direction proves nothing.
#
# The GREEN cases double as the EXIT-CODE-TRAP regression (#360): a grep-based guard
# exits non-zero when it finds NOTHING, so a `$?`-gated implementation would red on a
# clean tree. Every GREEN assertion below pins rc=0.
DM="$SCRIPTS/check_docs_markup.sh"

# mk_md_repo <dir> — a minimal tracked git tree the guard can enumerate with ls-files.
# autocrlf pinned OFF so the CRLF fixture stays CRLF on disk on every platform.
mk_md_repo() {
  local d="$1"; mkdir -p "$d"
  git -C "$d" init -q 2>/dev/null
  git -C "$d" config core.autocrlf false
  git -C "$d" config commit.gpgsign false
}
add_md() { # add_md <dir> <relpath>  (content on stdin) — writes then TRACKS the file
  local d="$1" p="$2"; mkdir -p "$(dirname "$d/$p")"; cat > "$d/$p"
  git -C "$d" add -- "$p" 2>/dev/null
}

DMT="$TMP/docsmarkup"

# --- (1) GREEN: a clean tracked tree. Also the exit-code-trap guard (empty output ⇒ rc 0).
mk_md_repo "$DMT/clean"
printf '# Doc\n\nA normal paragraph.\n' | add_md "$DMT/clean" "team/a.md"
assert_rc_and_grep 0 "no leaked tool-call markup" \
  "docs-markup: clean tracked tree → GREEN (rc=0; empty grep output must NOT be read as failure)" \
  -- bash "$DM" "$DMT/clean"

# --- (2) RED: the real shipped shape — bare closing tags alone on a line, LF, at EOF.
mk_md_repo "$DMT/lf"
printf '# Doc\n\nBody.\n</content>\n</invoke>\n' | add_md "$DMT/lf" "team/b.md"
assert_rc_and_grep 1 "leaked tool-call markup" \
  "docs-markup: LF offender (the shipped 5-for-5 shape) → RED" \
  -- bash "$DM" "$DMT/lf"
assert_rc_and_grep 1 "team/b.md:4" \
  "docs-markup: RED output NAMES the offending file:line (attribution, not just a count)" \
  -- bash "$DM" "$DMT/lf"

# --- (3) RED: CRLF offender. .gitattributes pins eol for *.sh/*.py/*.yml/*.yaml/*.ps1 and
# packages-lock.json but NOT *.md, so a CRLF-in-blob *.md is possible. On a Linux runner
# grep does NOT strip the CR, so a strict `>$` anchor would MISS it — a SILENT false
# GREEN, the failure direction that never gets looked at. (Git-for-Windows grep 3.0 opens
# text-mode and strips CR, so this case only DISCRIMINATES on the Ubuntu runner; the
# static assertion in (7) is what keeps the tolerance from being "simplified" away.)
mk_md_repo "$DMT/crlf"
printf '# Doc\r\n\r\nBody.\r\n</content>\r\n</invoke>\r\n' | add_md "$DMT/crlf" "team/c.md"
assert_rc_and_grep 1 "leaked tool-call markup" \
  "docs-markup: CRLF offender → RED (trailing-CR tolerance; strict >\$ would false-GREEN on Linux)" \
  -- bash "$DM" "$DMT/crlf"

# --- (4) GREEN: an INDENTED fenced XML example. This is the column-0 anchor earning its
# keep — the leak mechanism always emits at column 0, while a legitimate doc example is
# almost always indented. Deliberately NOT solved with a fenced-block EXCLUSION: an
# exclusion grows the exclusion set ⇒ false GREEN, silent (see the AC4 asymmetry).
mk_md_repo "$DMT/fence"
printf '# Doc\n\nExample:\n\n```xml\n <invoke>\n  <parameter>x</parameter>\n </invoke>\n```\n' \
  | add_md "$DMT/fence" "team/d.md"
assert_rc_and_grep 0 "no leaked tool-call markup" \
  "docs-markup: INDENTED fenced XML example → GREEN (column-0 anchor discriminates)" \
  -- bash "$DM" "$DMT/fence"

# --- (5) GREEN: prose that MENTIONS the tags inline (backticked). The guard's own
# documentation must not self-red, or the next author deletes the guard.
mk_md_repo "$DMT/prose"
printf '# Doc\n\nThe guard matches a bare `%s` or `%s` alone on a line.\n' '</content>' '</invoke>' \
  | add_md "$DMT/prose" "team/e.md"
assert_rc_and_grep 0 "no leaked tool-call markup" \
  "docs-markup: inline backticked mention in prose → GREEN (guard docs don't self-red)" \
  -- bash "$DM" "$DMT/prose"

# --- (6) RED for the WIDE tag vocabulary, open AND close forms, incl. the antml: prefix.
# None of these appear in the measured 5-file set; they are matched on purpose because
# widening the MATCH set can only yield a false RED — loud, someone looks at it. One
# assertion per variant so a narrowed vocabulary reds a NAMED case instead of silently
# shrinking coverage. Tags are COMPOSED from parts, never written literally: writing a
# bare closing tag into a file is how this whole bug class propagates (it truncated an
# authoring call while this very test block was being written — the mechanism is live).
CT='</'; OT='<'; GT='>'
dm_variants=(
  "${CT}parameter${GT}"
  "${CT}function_calls${GT}"
  "${CT}function_results${GT}"
  "${OT}invoke${GT}"
  "${CT}invoke${GT}"
  "${CT}antml:invoke${GT}"
  "${CT}antml:parameter${GT}"
)
vi=0
for tag in "${dm_variants[@]}"; do
  vi=$((vi + 1))
  dmd="$DMT/v$vi"; mk_md_repo "$dmd"
  printf '# Doc\n\nBody.\n%s\n' "$tag" | add_md "$dmd" "team/v.md"
  assert_rc_and_grep 1 "leaked tool-call markup" \
    "docs-markup: vocabulary variant '$tag' alone at column 0 → RED" \
    -- bash "$DM" "$dmd"
done

# --- (7) STATIC assertions on the pattern itself. Behavioural case (3) only
# DISCRIMINATES on Linux (Git-for-Windows grep 3.0 opens text-mode and strips the CR,
# measured), so a static assert is what stops a future "simplification" from
# reintroducing the silent CRLF false-GREEN. Mirrors assert_launch_windowed's
# anchored-static-grep style earlier in this file.
if grep -q 'PATTERN=' "$DM" && grep -q '\[\[:space:\]\]\*\$' "$DM"; then
  ok "docs-markup: pattern keeps trailing-whitespace tolerance — a CRLF-in-blob *.md cannot false-GREEN on the Ubuntu runner"
else
  bad "docs-markup: pattern LOST its trailing-whitespace tolerance — a CRLF-in-blob *.md would silently pass on Linux"
fi
if grep -q "PATTERN='\^" "$DM"; then
  ok "docs-markup: pattern stays anchored at column 0 — indented fenced XML examples stay GREEN"
else
  bad "docs-markup: pattern lost its column-0 anchor — indented XML examples will false-RED"
fi

# --- (8) The SCOPE claims are TESTED, not merely asserted in the header/PR body:
# (a) an UNTRACKED offending *.md is out of scope (the guard enumerates git ls-files);
# (b) a NON-.md offender is out of scope (AC3 scopes the guard to *.md).
# Both are disclosed holes; a disclosed hole with a test is a bound, a disclosed hole
# without one is a promise.
mk_md_repo "$DMT/untracked"
printf '# Doc\n\nok\n' | add_md "$DMT/untracked" "team/tracked.md"
printf '# Stray\n%s\n' "${CT}content${GT}" > "$DMT/untracked/team/stray.md"   # written, NOT git-added
assert_rc_and_grep 0 "no leaked tool-call markup" \
  "docs-markup: UNTRACKED offending *.md → GREEN (scope is TRACKED files — disclosed bound, not an oversight)" \
  -- bash "$DM" "$DMT/untracked"
mk_md_repo "$DMT/nonmd"
printf '# Doc\n\nok\n' | add_md "$DMT/nonmd" "team/ok.md"
printf 'notes\n%s\n' "${CT}content${GT}" | add_md "$DMT/nonmd" "team/notes.txt"
assert_rc_and_grep 0 "no leaked tool-call markup" \
  "docs-markup: offending *.txt → GREEN (scope is *.md only — the AC3 bound)" \
  -- bash "$DM" "$DMT/nonmd"

echo "=== check_committed_lineup.py — committed-bytes drift guard (86cayp0p9) ==="

# THE bug class this guards: Assets/Resources/WeaponSetLineup.prefab is generated by
# WeaponPackAssetGen but COMMITTED, and builds ship the COMMITTED snapshot. #304 added
# the wood row to the generator without re-baking the prefab, and the drift sat live on
# main because every Unity job bootstraps (re-bakes) before reading
# ([[unity-procedural-committed-assets-go-stale]], 86cahvntg).
#
# These cases prove the guard is TWO-SIDED: it goes RED on a drifted commit, not merely
# green on a matching one. A guard only ever observed green is indistinguishable from a
# guard that cannot fail. Zero Unity dependency — throwaway git repos, real `git show`.
LINEUP="$SCRIPTS/check_committed_lineup.py"

# mk_lineup_prefab <out-file> <spec>...   spec = "<name>" | "<name>@<parent-name>"
# Emits Unity prefab YAML: one root ("WeaponSetLineup") plus one GameObject+Transform
# per spec. A spec with @parent is nested UNDER that node instead of under the root —
# that's the depth-awareness fixture (a nested node must NOT satisfy the contract).
mk_lineup_prefab() {
  local out="$1"; shift
  python3 - "$out" "$@" <<'PY'
import sys
out, specs = sys.argv[1], sys.argv[2:]
ROOT_GO, ROOT_TF = 9001, 9002
nodes = []            # (name, go_anchor, tf_anchor, parent_name_or_None)
for i, spec in enumerate(specs):
    name, _, parent = spec.partition("@")
    nodes.append((name, 1000 + i, 2000 + i, parent or None))
tf_of = {n[0]: n[2] for n in nodes}
kids = {ROOT_TF: []}
for name, go, tf, parent in nodes:
    kids.setdefault(tf, [])
    kids[tf_of[parent] if parent else ROOT_TF].append(tf)

def go_block(anchor, name, tf):
    return (f"--- !u!1 &{anchor}\nGameObject:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            "  serializedVersion: 6\n  m_Component:\n"
            f"  - component: {{fileID: {tf}}}\n  m_Layer: 0\n  m_Name: {name}\n"
            "  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n"
            "  m_StaticEditorFlags: 0\n  m_IsActive: 1\n")

def tf_block(anchor, go, father):
    children = kids.get(anchor, [])
    body = ("  m_Children: []\n" if not children else
            "  m_Children:\n" + "".join(f"  - {{fileID: {c}}}\n" for c in children))
    return (f"--- !u!4 &{anchor}\nTransform:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {go}}}\n  serializedVersion: 2\n"
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
            "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
            "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n"
            + body + f"  m_Father: {{fileID: {father}}}\n"
            "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n")

parts = ["%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n",
         go_block(ROOT_GO, "WeaponSetLineup", ROOT_TF), tf_block(ROOT_TF, ROOT_GO, 0)]
for name, go, tf, parent in nodes:
    parts += [go_block(go, name, tf),
              tf_block(tf, go, tf_of[parent] if parent else ROOT_TF)]
open(out, "w", encoding="utf-8").write("".join(parts))
PY
}

# The generator fixture — shaped like the real WeaponPackAssetGen: Dir + PrefabPath
# consts, `Dir + "/x.fbx"` path consts, an ALIAS const with no literal, a RETIRED const
# that no row set names, two row sets, and a BuildFamilyPrefab whose string literal
# carries a { brace } (so the body brace-matcher must skip literals).
mk_lineup_generator() {
  cat > "$1" <<'CS'
namespace FarHorizon.EditorTools
{
    public static class WeaponPackAssetGen
    {
        public const string Dir = "Assets/Art/Props/WeaponPack";

        public const string AxeFbxPath = Dir + "/wpn_axe_stone_01.fbx";
        public const string KnifeFbxPath = Dir + "/wpn_knife_stone_01.fbx";
        public const string AxeWoodFbxPath = Dir + "/wpn_axe_wood_01.fbx";
        public const string KnifeWoodFbxPath = Dir + "/wpn_knife_wood_01.fbx";
        // Retired but still declared - rowed by nobody, so never demanded.
        public const string RetiredFlintFbxPath = Dir + "/wpn_axe_flint_01.fbx";
        // Alias const - carries no Dir + "..." literal of its own.
        public const string HeroAxeFbxPath = AxeFbxPath;

        public const string PrefabPath = "Assets/Resources/WeaponSetLineup.prefab";

        private static readonly (string path, float x)[] StoneSet =
        {
            (AxeFbxPath,   -0.75f),
            (KnifeFbxPath, -0.25f),
        };

        private static readonly (string path, float x)[] WoodSet =
        {
            (AxeWoodFbxPath,   -0.75f),
            (KnifeWoodFbxPath, -0.25f),
        };

        private static void BuildFamilyPrefab(Material mat)
        {
            var root = new GameObject("WeaponSetLineup { not a brace }");
            AddRow(root.transform, WoodSet, -1.1f, mat);
            AddRow(root.transform, StoneSet, 0f, mat);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }

        private static void AddRow(Transform parent, (string path, float x)[] set, float z, Material mat)
        {
        }
    }
}
CS
}

# mk_lineup_repo <dir> <prefab-spec>...  -> a committed throwaway repo
mk_lineup_repo() {
  local d="$1"; shift
  mkdir -p "$d/Assets/Scripts/Editor" "$d/Assets/Resources"
  mk_lineup_generator "$d/Assets/Scripts/Editor/WeaponPackAssetGen.cs"
  mk_lineup_prefab "$d/Assets/Resources/WeaponSetLineup.prefab" "$@"
  ( cd "$d" && git init -q && git config user.email t@t && git config user.name t \
    && git add -A >/dev/null 2>&1 && git commit -qm fixture >/dev/null 2>&1 ) || return 1
}
guard_in() { bash -c "cd '$1' && python3 '$LINEUP' ${2:-HEAD}"; }

# --- GREEN half: committed bytes match the generator contract -----------------
# Also pins RETIRED-CONST IMMUNITY: RetiredFlintFbxPath is declared but rowed by no set,
# so wpn_axe_flint_01 must NOT be demanded. A guard that keyed on "every *FbxPath const"
# instead of the AddRow'd row sets would false-RED here.
LU_OK="$TMP/lineup_ok"
mk_lineup_repo "$LU_OK" wpn_axe_wood_01 wpn_knife_wood_01 wpn_axe_stone_01 wpn_knife_stone_01 StandLight
assert_rc_grep_present_absent 0 "carries all 4 generator node(s)" "wpn_axe_flint_01" \
  "lineup: matching commit GREEN + retired-const not demanded (86cayp0p9)" \
  -- guard_in "$LU_OK"

# --- RED half: the #304 shape - a whole row absent from the committed prefab ---
LU_DRIFT="$TMP/lineup_drift"
mk_lineup_repo "$LU_DRIFT" wpn_axe_stone_01 wpn_knife_stone_01 StandLight
assert_rc_and_grep 1 "is MISSING 2 generator node(s): [wpn_axe_wood_01, wpn_knife_wood_01]" \
  "lineup: drifted commit REDS and NAMES the missing nodes (86cayp0p9)" \
  -- guard_in "$LU_DRIFT"

# --- RED: depth-awareness - a nested node must NOT satisfy the contract --------
# The false-GREEN this kills: a flat `grep m_Name` over the file would count a node
# parented one level deep, but AddRow parents every weapon DIRECTLY under the root.
LU_NEST="$TMP/lineup_nested"
mk_lineup_repo "$LU_NEST" wpn_axe_stone_01 wpn_knife_stone_01 wpn_axe_wood_01 \
                          "wpn_knife_wood_01@wpn_axe_wood_01" StandLight
assert_rc_and_grep 1 "is MISSING 1 generator node(s): [wpn_knife_wood_01]" \
  "lineup: node present but NESTED still REDS (direct-child contract, 86cayp0p9)" \
  -- guard_in "$LU_NEST"

# --- RED: an extra/renamed weapon node is committed drift too -----------------
LU_EXTRA="$TMP/lineup_extra"
mk_lineup_repo "$LU_EXTRA" wpn_axe_wood_01 wpn_knife_wood_01 wpn_axe_stone_01 \
                           wpn_knife_stone_01 wpn_axe_bronze_01 StandLight
assert_rc_and_grep 1 "UNEXPECTED weapon node(s) not in the generator contract: [wpn_axe_bronze_01]" \
  "lineup: extra wpn_ node REDS (86cayp0p9)" \
  -- guard_in "$LU_EXTRA"

# --- The COMMIT, never the working tree (the ticket's core constraint) --------
# A working-tree read would pass on a dirty checkout no reviewer ever sees, and would
# also be defeated by a bootstrap re-bake. Both directions are pinned.
LU_WT_FIXED="$TMP/lineup_wt_fixed"
mk_lineup_repo "$LU_WT_FIXED" wpn_axe_stone_01 wpn_knife_stone_01 StandLight
mk_lineup_prefab "$LU_WT_FIXED/Assets/Resources/WeaponSetLineup.prefab" \
  wpn_axe_wood_01 wpn_knife_wood_01 wpn_axe_stone_01 wpn_knife_stone_01 StandLight
assert_rc_and_grep 1 "is MISSING 2 generator node(s)" \
  "lineup: working-tree FIX does not green a drifted COMMIT (86cayp0p9)" \
  -- guard_in "$LU_WT_FIXED"

LU_WT_BROKEN="$TMP/lineup_wt_broken"
mk_lineup_repo "$LU_WT_BROKEN" wpn_axe_wood_01 wpn_knife_wood_01 wpn_axe_stone_01 \
                               wpn_knife_stone_01 StandLight
mk_lineup_prefab "$LU_WT_BROKEN/Assets/Resources/WeaponSetLineup.prefab" \
  wpn_axe_stone_01 wpn_knife_stone_01 StandLight
assert_rc_and_grep 0 "carries all 4 generator node(s)" \
  "lineup: working-tree BREAKAGE does not red a clean COMMIT (86cayp0p9)" \
  -- guard_in "$LU_WT_BROKEN"

# --- The guard grades the NAMED ref, so a re-bake commit flips it green -------
# This is the ticket's success test in miniature: drifted commit RED, re-bake commit GREEN,
# on the same repo, discriminated only by ref.
LU_REF="$TMP/lineup_ref"
mk_lineup_repo "$LU_REF" wpn_axe_stone_01 wpn_knife_stone_01 StandLight
mk_lineup_prefab "$LU_REF/Assets/Resources/WeaponSetLineup.prefab" \
  wpn_axe_wood_01 wpn_knife_wood_01 wpn_axe_stone_01 wpn_knife_stone_01 StandLight
( cd "$LU_REF" && git add -A >/dev/null 2>&1 && git commit -qm rebake >/dev/null 2>&1 )
assert_rc_and_grep 1 "is MISSING 2 generator node(s)" \
  "lineup: HEAD~1 (pre-re-bake commit) REDS (86cayp0p9)" \
  -- guard_in "$LU_REF" 'HEAD~1'
assert_rc_and_grep 0 "carries all 4 generator node(s)" \
  "lineup: HEAD (re-bake commit) GREEN (86cayp0p9)" \
  -- guard_in "$LU_REF" 'HEAD'

# --- Parse failures are LOUD (exit 2), never a quiet pass ---------------------
# A guard that silently narrows its expected set is the thing lying. Each of these
# would otherwise be a false GREEN.
#
# Each negative fixture below is built by MUTATING the canonical generator, so each is
# paired with an assertion that the mutation ACTUALLY LANDED. Caught live while writing
# these: a sed whose whitespace didn't match the fixture no-op'd, the repo stayed
# canonical, and the "red" case passed green while testing nothing. A mutation-driven
# negative case must pin its own mutation, or it silently degrades into a second copy
# of the positive case.
# Prints `count=<n>` so the assertion is EXACT — a bare "0" needle would also match
# an output of "10", which is the same silent-degradation shape this pins against.
grep_count_in() {
  local needle="$1" file="$2" n
  n="$(grep -c -F -- "$needle" "$file" 2>/dev/null)" || n=0
  printf 'count=%s\n' "$n"
}

LU_NOROW="$TMP/lineup_no_addrow"
mk_lineup_repo "$LU_NOROW" wpn_axe_wood_01 wpn_knife_wood_01 wpn_axe_stone_01 \
                           wpn_knife_stone_01 StandLight
( cd "$LU_NOROW" \
  && sed -i '/AddRow(root.transform/d' Assets/Scripts/Editor/WeaponPackAssetGen.cs \
  && git add -A >/dev/null 2>&1 && git commit -qm no-addrow >/dev/null 2>&1 )
assert_rc_and_grep 0 "count=0" "lineup fixture: AddRow-deletion mutation actually landed" \
  -- grep_count_in "AddRow(root.transform" "$LU_NOROW/Assets/Scripts/Editor/WeaponPackAssetGen.cs"
assert_rc_and_grep 2 "found ZERO \`AddRow(<parent>, <set>, ...)\` calls" \
  "lineup: BuildFamilyPrefab with no AddRow calls fails LOUD, not empty-green (86cayp0p9)" \
  -- guard_in "$LU_NOROW"

LU_BADCONST="$TMP/lineup_bad_const"
mk_lineup_repo "$LU_BADCONST" wpn_axe_wood_01 wpn_knife_wood_01 wpn_axe_stone_01 \
                              wpn_knife_stone_01 StandLight
( cd "$LU_BADCONST" \
  && sed -i 's/(KnifeWoodFbxPath,/(GhostFbxPath,/' \
       Assets/Scripts/Editor/WeaponPackAssetGen.cs \
  && git add -A >/dev/null 2>&1 && git commit -qm ghost >/dev/null 2>&1 )
assert_rc_and_grep 0 "count=1" "lineup fixture: ghost-const mutation actually landed" \
  -- grep_count_in "(GhostFbxPath," "$LU_BADCONST/Assets/Scripts/Editor/WeaponPackAssetGen.cs"
assert_rc_and_grep 2 "does not resolve to a" \
  "lineup: unresolvable row-set member fails LOUD (would shrink the contract) (86cayp0p9)" \
  -- guard_in "$LU_BADCONST"

LU_NOPREFAB="$TMP/lineup_no_prefab"
mk_lineup_repo "$LU_NOPREFAB" wpn_axe_wood_01 wpn_knife_wood_01 wpn_axe_stone_01 \
                              wpn_knife_stone_01 StandLight
( cd "$LU_NOPREFAB" && git rm -q Assets/Resources/WeaponSetLineup.prefab >/dev/null 2>&1 \
  && git commit -qm drop-prefab >/dev/null 2>&1 )
assert_rc_and_grep 2 "cannot read" \
  "lineup: missing committed prefab fails LOUD (86cayp0p9)" \
  -- guard_in "$LU_NOPREFAB"

echo "==================================="
printf '%d passed, %d failed\n' "$pass" "$fail"
[ "$fail" -eq 0 ] || { echo "GATE-SCRIPT TESTS FAILED"; exit 1; }
echo "GATE-SCRIPT TESTS PASSED"
