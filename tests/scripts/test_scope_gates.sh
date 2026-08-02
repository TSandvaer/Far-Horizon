#!/usr/bin/env bash
# test_scope_gates.sh — unit + wiring guard for the capture-gate path router.
#
# WHAT THIS GUARDS. `.github/workflows/scripts/scope_gates.sh` makes 15 of the 16
# shipped-build capture gates CONDITIONAL on what a diff touches. That introduces a
# NEW way for a gate to gate nothing — not "the wrapper is never invoked" (the
# -verifySwings class `test_gate_scripts.sh` already reds) but "the wrapper is
# invoked behind a condition that is never true, or behind no condition at all in a
# fail-CLOSED form". A gate skipped on every run is indistinguishable, on the PR
# surface, from a gate that passed. So the router needs its own two-sided guard:
#
#   * DISCRIMINATION — the mapping actually routes: a weapon path turns the weapon
#     gate on and the pond gate off, a docs path turns everything off, an unknown
#     path turns everything ON (fail-open).
#   * WIRING — every wrapper registered in test_gate_scripts.sh's launch-mode lists
#     is either the declared ALWAYS-ON gate (and carries NO condition) or has (a) a
#     key in ROUTED_KEYS, (b) a `!= 'false'` condition on its ci.yml step, and (c) at
#     least one REAL path in the tree that reaches that key. Registration is
#     three-part now, and a merge cannot see any of the three
#     (unity-conventions.md §Headless: "registration is invisible to a merge").
#
# FAIL-OPEN IS THE INVARIANT UNDER TEST, not a nicety. Every uncertainty must run
# MORE gates. The two arms that enforce it — the script's `*) ALL` fallthrough and
# ci.yml's `!= 'false'` (never `== 'true'`) form — each have a dedicated check below.
#
#   tests/scripts/test_scope_gates.sh
#
# Zero Unity dependency; runs in the license-free `structure` job on every PR.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
SCRIPTS="$ROOT/.github/workflows/scripts"
SCOPE="$SCRIPTS/scope_gates.sh"
CI_YML="$ROOT/.github/workflows/ci.yml"
GATE_TESTS="$HERE/test_gate_scripts.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

pass=0; fail=0
ok()  { printf '[ OK ] %s\n' "$1"; pass=$((pass+1)); }
bad() { printf '[FAIL] %s\n' "$1"; fail=$((fail+1)); }

if [ ! -f "$SCOPE" ]; then
  bad "scope_gates.sh not found at $SCOPE"
  printf '\n%d passed, %d failed\n' "$pass" "$fail"; exit 1
fi
[ -f "$CI_YML" ]     || { bad "ci.yml not found at $CI_YML";     printf '\n%d passed, %d failed\n' "$pass" "$fail"; exit 1; }
[ -f "$GATE_TESTS" ] || { bad "test_gate_scripts.sh not found";  printf '\n%d passed, %d failed\n' "$pass" "$fail"; exit 1; }

ROUTED_KEYS="$(bash "$SCOPE" --keys | tr '\n' ' ')"
ALWAYS_GATES="$(bash "$SCOPE" --always | tr '\n' ' ')"

# decide_for <path...> -> the `run_<k>=<v>` block for a diff containing exactly those paths
decide_for() { printf '%s\n' "$@" | bash "$SCOPE" --files -; }

# assert_run <expected true|false> <key> <label> -- <path...>
assert_run() {
  local exp="$1" key="$2" label="$3"; shift 4
  local got; got="$(decide_for "$@" | sed -n "s/^run_${key}=//p")"
  if [ "$got" = "$exp" ]; then ok "$label (run_$key=$got)"
  else bad "$label — expected run_$key=$exp, got '${got:-<absent>}'"; fi
}

# assert_map <expected> <path> — the raw arm result for ONE path
assert_map() {
  local exp="$1" p="$2"
  local got; got="$(bash "$SCOPE" --map "$p")"
  if [ "$got" = "$exp" ]; then ok "map: $p -> $got"
  else bad "map: $p — expected '$exp', got '$got'"; fi
}

echo "=== tier rules: NONE / ALL / feature-scoped ==="
assert_map NONE ".claude/docs/unity-conventions.md"
assert_map NONE "team/STATE.md"
assert_map NONE "Assets/Tests/PlayMode/ChopTreePlayModeTests.cs"
assert_map NONE "tools/debug/REGISTRY.md"
# A capture COMPONENT with no CI wrapper is inert; a compile break in it reds the BUILD
# job, never a capture gate. (test_gate_scripts.sh's gate-wiring loop is what keeps
# "no wrapper" true for these.)
assert_map NONE "Assets/Scripts/Runtime/SwingVerifyCapture.cs"
assert_map ALL  ".github/workflows/ci.yml"
assert_map ALL  "tests/scripts/test_gate_scripts.sh"
assert_map ALL  "Assets/Scenes/Boot.unity"
assert_map ALL  "Assets/Shaders/LowPolyVertexColor.shader"
assert_map ALL  "ProjectSettings/ProjectSettings.asset"
assert_map ALL  "Assets/Scripts/Runtime/CastawayArmPose.cs"
assert_map ALL  "Assets/Scripts/Runtime/Inventory.cs"
assert_map ALL  "Assets/Scripts/Runtime/Combat/BoarAI.cs"
assert_map ALL  "Assets/Scripts/Editor/BootstrapProject.cs"
# The committed build stamp is bootstrap side-effect churn that CI overwrites before
# every build and no gate reads — but its Resources/ SIBLING (the weapon lineup prefab
# the weaponset gate loads) must stay ALL. Both arms are asserted so a future reordering
# that swallows the specific arm under the general one reds here.
assert_map NONE "Assets/Resources/BuildStamp.txt"
assert_map NONE "Assets/Resources/BuildStamp.txt.meta"
assert_map ALL  "Assets/Resources/WeaponSetLineup.prefab"
# The `.meta` sibling of a mapped file must route IDENTICALLY — Unity commits one per
# asset, so a routing table blind to `.meta` would silently fail open on half the diff.
assert_map "chop placement" "Assets/Scripts/Runtime/ChopTree.cs"
assert_map "chop placement" "Assets/Scripts/Runtime/ChopTree.cs.meta"

echo "=== the ACCEPTANCE cases from the ticket ==="
# (1) docs-only: every content gate skips. The `capture` JOB still runs and still
#     reports — nothing here is job- or workflow-level — so the merge gate stays green.
for k in $ROUTED_KEYS; do
  assert_run false "$k" "docs-only diff skips $k" -- ".claude/docs/game-juice.md" "team/DECISIONS.md"
done
# (2) a weapon-code change still runs the weapon gates.
assert_run true weaponset "weapon-pack art change RUNS weaponset" -- "Assets/Art/Props/WeaponPack/wpn_axe_iron_01.fbx"
assert_run true weaponset "WeaponPackAssetGen change RUNS weaponset" -- "Assets/Scripts/Editor/WeaponPackAssetGen.cs"
assert_run true heldwood  "weapon-pack art change RUNS heldwood"   -- "Assets/Art/Props/WeaponPack/wpn_axe_iron_01.fbx"
# ...and does NOT drag in the unrelated windowed gates (this is the whole point).
assert_run false pond "weapon-pack art change SKIPS pond" -- "Assets/Art/Props/WeaponPack/wpn_axe_iron_01.fbx"
assert_run false boar "weapon-pack art change SKIPS boar" -- "Assets/Art/Props/WeaponPack/wpn_axe_iron_01.fbx"

echo "=== feature routing: on-target and off-target, per gate ==="
assert_run true  chop     "ChopTree.cs RUNS chop"        -- "Assets/Scripts/Runtime/ChopTree.cs"
assert_run false settings "ChopTree.cs SKIPS settings"   -- "Assets/Scripts/Runtime/ChopTree.cs"
assert_run true  pond     "FreshwaterPond.cs RUNS pond"  -- "Assets/Scripts/Runtime/FreshwaterPond.cs"
assert_run true  water    "FreshwaterPond.cs RUNS water" -- "Assets/Scripts/Runtime/FreshwaterPond.cs"
assert_run false chop     "FreshwaterPond.cs SKIPS chop" -- "Assets/Scripts/Runtime/FreshwaterPond.cs"
assert_run true  loot     "BerryBush.cs RUNS loot"       -- "Assets/Scripts/Runtime/BerryBush.cs"
assert_run false mine     "BerryBush.cs SKIPS mine"      -- "Assets/Scripts/Runtime/BerryBush.cs"
assert_run true  boar     "BoarVerifyCapture RUNS boar"  -- "Assets/Scripts/Runtime/BoarVerifyCapture.cs"
assert_run true  sky      "CloudDrift.cs RUNS sky"       -- "Assets/Scripts/Runtime/CloudDrift.cs"
assert_run true  settings "SettingsPanel.uss RUNS settings" -- "Assets/UI/SettingsPanel.uss"
assert_run true  invdragghostpos "InventoryUI.cs RUNS invdragghostpos" -- "Assets/Scripts/Runtime/InventoryUI.cs"
assert_run true  boulder  "MineBoulder.cs RUNS boulder"  -- "Assets/Scripts/Runtime/MineBoulder.cs"
assert_run true  placement "MineBoulder.cs RUNS placement (the registry branch)" -- "Assets/Scripts/Runtime/MineBoulder.cs"
assert_run true  mine     "MineOre.cs RUNS mine"         -- "Assets/Scripts/Runtime/MineOre.cs"
assert_run true  heldbelt "HeldAxeRig.cs RUNS heldbelt"  -- "Assets/Scripts/Runtime/HeldAxeRig.cs"
assert_run true  buildmenu "BuildMenuUI.cs RUNS buildmenu" -- "Assets/Scripts/Runtime/BuildMenuUI.cs"

echo "=== FAIL-OPEN: every uncertainty runs MORE gates ==="
# An UNMAPPED path is the commonest future case (a new .cs nobody routed). It must run
# everything — an incomplete table may cost runner time, never coverage.
assert_map ALL "Assets/Scripts/Runtime/SomeFileNobodyRoutedYet.cs"
assert_map ALL "Assets/Art/SomeNewArtFolder/thing.fbx"
for k in $ROUTED_KEYS; do
  assert_run true "$k" "unmapped path fails OPEN for $k" -- "Assets/Scripts/Runtime/SomeFileNobodyRoutedYet.cs"
done
# A docs path mixed WITH an unmapped path must still fail open (the ALL wins).
assert_run true pond "docs + unmapped path fails OPEN" -- "team/STATE.md" "Assets/Scripts/Runtime/Nobody.cs"
# An EMPTY changed-path list (git gave us nothing) must run everything.
empty_out="$(printf '' | bash "$SCOPE" --files -)"
if printf '%s' "$empty_out" | grep -q 'scope_reason=fail-open' \
   && ! printf '%s' "$empty_out" | grep -q '=false'; then
  ok "empty changed-path list fails OPEN (all 15 true, reason names it)"
else
  bad "empty changed-path list did NOT fail open"; printf '%s\n' "$empty_out" | sed 's/^/        /'
fi
# A missing / zero base sha must run everything (the push-to-a-new-branch case).
for base in "" "0000000000000000000000000000000000000000" "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef"; do
  out="$(cd "$ROOT" && SCOPE_BASE_SHA="$base" GITHUB_OUTPUT="" bash "$SCOPE" --github-output 2>&1)"
  if printf '%s' "$out" | grep -q 'run_pond=true' && ! printf '%s' "$out" | grep -q '=false'; then
    ok "unusable base sha '${base:-<empty>}' fails OPEN"
  else
    bad "unusable base sha '${base:-<empty>}' did NOT fail open"; printf '%s\n' "$out" | sed 's/^/        /'
  fi
done
# The script must NEVER exit non-zero — a red scope step would leave the outputs
# unwritten, and only ci.yml's `!= 'false'` belt would be holding the door.
(cd "$ROOT" && SCOPE_BASE_SHA="" GITHUB_OUTPUT="" bash "$SCOPE" --github-output >/dev/null 2>&1)
if [ $? -eq 0 ]; then ok "scope_gates.sh --github-output exits 0 even with no usable base"
else bad "scope_gates.sh --github-output exited non-zero — the CI step would go red"; fi

echo "=== every routed key is REACHABLE from a real path in the tree ==="
# A key with no reachable path is a gate that never runs — the exact false-green this
# file exists to prevent. Proven against the ACTUAL tracked tree, not a hand-kept
# witness list, so it cannot rot into a tautology as files are added and renamed.
(cd "$ROOT" && git ls-files) > "$TMP/tracked.txt" 2>/dev/null || : > "$TMP/tracked.txt"
if [ ! -s "$TMP/tracked.txt" ]; then
  bad "reachability: could not list tracked files (git ls-files empty)"
else
  bash "$SCOPE" --map-many "$TMP/tracked.txt" \
    | awk -F'\t' '$2!="ALL" && $2!="NONE" {print $2}' | tr ' ' '\n' | sort -u > "$TMP/reachable.txt"
  for k in $ROUTED_KEYS; do
    if grep -qx "$k" "$TMP/reachable.txt"; then
      ok "reachability: some tracked path routes to '$k'"
    else
      bad "reachability: NO tracked path routes to '$k' — that gate can only ever run via fail-open, i.e. it is effectively unrouted"
    fi
  done
fi

echo "=== ci.yml wiring: registration is THREE-part (key + condition + reachability) ==="
# Pull the launch-mode-registered wrappers straight from test_gate_scripts.sh so the two
# guards cannot drift apart. Re-find BY NAME, never by line number.
REGISTERED="$(sed -n '/^HEADLESS_GATES=(/,/^WINDOWED_GATES=(/p;/^WINDOWED_GATES=(/,/)$/p' "$GATE_TESTS" \
  | grep -oE '(capture_gate|verify_[a-z]+_gate)\.sh' | sort -u)"
if [ -z "$REGISTERED" ]; then
  bad "wiring: could not read HEADLESS_GATES/WINDOWED_GATES out of test_gate_scripts.sh (re-find them BY NAME — a range that no longer contains those declarations is stale, not empty)"
fi

# step_if_for <needle> — the `if:` value of the ci.yml step whose run: block names
# <needle>. Comment lines are skipped (several gate names appear in job prose).
step_if_for() {
  awk -v needle="$1" '
    /^      - name: / { cur = ""; next }
    /^        if: /   { cur = substr($0, 13); next }
    /^[[:space:]]*#/  { next }
    index($0, needle) > 0 { print cur; found=1 }
    END { if (!found) print "<NOT-INVOKED>" }
  ' "$CI_YML" | sort -u
}

for g in $REGISTERED; do
  key="$(printf '%s' "$g" | sed -n 's/^verify_\(.*\)_gate\.sh$/\1/p')"
  ifs="$(step_if_for "scripts/$g")"

  case " $ALWAYS_GATES " in
    *" $g "*)
      # THE ALWAYS-ON GATE. It must carry NO scope condition — if someone routes it,
      # a docs-only run has ZERO shipped-build evidence and nobody would notice.
      if [ "$ifs" = "always()" ]; then
        ok "wiring: $g is the ALWAYS-ON gate and carries no scope condition"
      else
        bad "wiring: $g is declared ALWAYS-ON (scope_gates.sh ALWAYS_GATES) but its ci.yml step's if: is '$ifs' — the always-on gate must be unconditional, or a docs-only run ships with no shipped-build evidence at all"
      fi
      continue ;;
  esac

  if [ -z "$key" ]; then
    bad "wiring: $g does not match verify_<key>_gate.sh, so no routing key can be derived — either name it that way or declare it in ALWAYS_GATES"
    continue
  fi

  case " $ROUTED_KEYS " in
    *" $key "*) ok "wiring: $g has routing key '$key' in ROUTED_KEYS" ;;
    *) bad "wiring: $g is CI-wired + launch-mode-registered but '$key' is NOT in scope_gates.sh ROUTED_KEYS — it would run on every path rule that fails open and be invisible to the router otherwise" ;;
  esac

  want="always() && steps.scope.outputs.run_${key} != 'false'"
  if [ "$ifs" = "$want" ]; then
    ok "wiring: $g's ci.yml step gates on run_$key != 'false'"
  else
    bad "wiring: $g's ci.yml step if: is '$ifs' — expected exactly \"$want\". A gate invoked with no condition ignores the router; a gate invoked with a DIFFERENT key gates on someone else's diff."
  fi

  # The paired upload step must carry the SAME condition, so a skipped gate leaves no
  # half-written artifact and a run gate always uploads its evidence.
  n_cond="$(grep -cF "steps.scope.outputs.run_${key} != 'false'" "$CI_YML")"
  if [ "$n_cond" -ge 2 ]; then
    ok "wiring: run_$key gates >=2 steps (the gate + its evidence upload)"
  else
    bad "wiring: run_$key gates only $n_cond step(s) — the gate and its upload-artifact step must share the condition, else a skipped gate warns on a missing artifact or a run gate loses its evidence"
  fi
done

echo "=== ci.yml conditions are FAIL-OPEN in form (!= 'false', never == 'true') ==="
# `== 'true'` is fail-CLOSED: an unwritten output (scope step crashed / GITHUB_OUTPUT
# unset) would SKIP every gate and the job would still go green. `!= 'false'` runs them.
closed="$(grep -nE "steps\.scope\.outputs\.run_[a-z]+ *==" "$CI_YML" || true)"
if [ -z "$closed" ]; then
  ok "no ci.yml condition uses the fail-CLOSED == form"
else
  bad "ci.yml carries fail-CLOSED == conditions — an unwritten scope output would skip these gates silently:"
  printf '%s\n' "$closed" | sed 's/^/        /'
fi
# And nothing may gate on a key the router does not emit (a typo'd key is permanently
# empty, which under `!= 'false'` runs forever — noisy, not dangerous — but under any
# future edit is a live hazard, and it always means the author meant a real key).
for used in $(grep -oE "steps\.scope\.outputs\.run_[a-z]+" "$CI_YML" | sed 's/.*run_//' | sort -u); do
  case " $ROUTED_KEYS " in
    *" $used "*) ok "ci.yml condition key '$used' is emitted by scope_gates.sh" ;;
    *) bad "ci.yml gates on 'run_$used' but scope_gates.sh never emits that key — check the spelling against --keys" ;;
  esac
done

echo "=== a ROUTED gate can still go RED (the router did not disarm the wrapper) ==="
# The composed proof, at rung 2 (synthetic fixture — the real subject needs the single
# Unity build slot + the self-hosted runner). Two halves, both required:
#   (a) the router says RUN for a chop-touching diff, and
#   (b) the wrapper it routes to still FAILS on a broken exe.
# Neither half alone is the claim; a router that says RUN into a wrapper that cannot
# fail is exactly the noise-for-nothing trade this PR must not make.
chop_run="$(decide_for "Assets/Scripts/Runtime/ChopTree.cs" | sed -n 's/^run_chop=//p')"
cat > "$TMP/broken_exe.sh" <<'FAKE'
#!/usr/bin/env bash
# A shipped exe that launches and immediately fails its own self-assertion.
echo "[fake] verify component self-assert FAILED" >&2
exit 1
FAKE
chmod +x "$TMP/broken_exe.sh"
gate_out="$(bash "$SCRIPTS/verify_chop_gate.sh" "$TMP/broken_exe.sh" "$TMP/chop_caps" "$TMP/chop.log" 2>&1)"
gate_rc=$?
if [ "$chop_run" = "true" ] && [ "$gate_rc" -ne 0 ] && printf '%s' "$gate_out" | grep -qF "CAPTURE GATE FAILED"; then
  ok "routed-and-still-red: ChopTree.cs -> run_chop=true, and verify_chop_gate.sh on a broken exe exits $gate_rc with CAPTURE GATE FAILED"
else
  bad "routed-and-still-red: run_chop='$chop_run', gate rc=$gate_rc — a routed gate that cannot go red is worse than the noise it replaced"
  printf '%s\n' "$gate_out" | sed 's/^/        /'
fi
# ...and the SKIP direction is a genuine skip, not a silent pass: a docs-only diff must
# emit an explicit `false` (an ABSENT key would be indistinguishable from a crash).
# Capture first, THEN grep: `set -o pipefail` + `grep -q`'s early close makes a
# `decide_for | grep -q` pipeline report SIGPIPE, not the match.
docs_decision="$(decide_for "team/STATE.md")"
if printf '%s\n' "$docs_decision" | grep -qx 'run_chop=false'; then
  ok "routed-and-still-red: a docs-only diff emits an EXPLICIT run_chop=false (never an absent key)"
else
  bad "routed-and-still-red: docs-only diff did not emit an explicit run_chop=false"
fi

printf '\n%d passed, %d failed\n' "$pass" "$fail"
[ "$fail" -eq 0 ] || exit 1
