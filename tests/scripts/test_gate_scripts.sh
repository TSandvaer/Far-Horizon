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
    && printf 'm_EditorVersion: 6000.4.10f1\n' > ProjectSettings/ProjectVersion.txt \
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

echo "==================================="
printf '%d passed, %d failed\n' "$pass" "$fail"
[ "$fail" -eq 0 ] || { echo "GATE-SCRIPT TESTS FAILED"; exit 1; }
echo "GATE-SCRIPT TESTS PASSED"
