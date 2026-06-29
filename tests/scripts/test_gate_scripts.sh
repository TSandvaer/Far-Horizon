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
# $3 = "identical" → tweaked frame is a byte-copy of open (the PR #83 pixel-identical bug).
make_fake_exe() {
  local exe="$1" changed="$2" ident="${3:-}"
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

# 5. Check 3 (visible-tweak diff) is QUARANTINED-NON-FATAL (86cabe3e5). With changedLive=True
#    (the live param DID change) but settings_tweaked.png a BYTE-COPY of settings_open.png (the
#    SYNTHETIC -verifySettings drive bypasses the UI Toolkit ChangeEvent so the readout never
#    repaints under capture), the gate now PASSES (rc=0) — Checks 1+2 (the real shipped-build
#    backstops) both pass, and the pixel-identical diff_rc is logged for signal but does NOT red
#    the gate. The quarantine marker must be present so the un-tweaked frame is visible in CI. The
#    REAL drag repaints (Tess+Drew confirmed); proper fix tracked in 86cabe3e5. This assertion is
#    the regression guard for the quarantine: if Check 3 ever silently becomes fatal again (or the
#    quarantine marker is dropped) this fails.
make_fake_exe "$TMP/fake_identical.sh" "True" "identical"
assert_rc_and_grep 0 "QUARANTINED-non-fatal" "pixel-identical tweaked frame is quarantined-non-fatal (86cabe3e5)" \
  -- bash "$SETTINGS_GATE" "$TMP/fake_identical.sh" "$TMP/scaps_id" "$TMP/slog_id.log"

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

echo "==================================="
printf '%d passed, %d failed\n' "$pass" "$fail"
[ "$fail" -eq 0 ] || { echo "GATE-SCRIPT TESTS FAILED"; exit 1; }
echo "GATE-SCRIPT TESTS PASSED"
