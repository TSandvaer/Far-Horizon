#!/usr/bin/env bash
# scope_gates.sh — decide WHICH shipped-build capture gates a diff needs to run.
#
# WHY THIS EXISTS. The `capture` job in ci.yml runs SIXTEEN capture gates on EVERY
# run, scoped to nothing. Eight of them launch the built exe WINDOWED on the
# self-hosted runner — which is the Sponsor's own laptop — so every push replays the
# campfire, the sky and the pond as windows popping open and shut, "testing the same
# things every time". This script is the path-FILTER that makes fifteen of the sixteen
# CONDITIONAL on what the diff actually touches. It deletes no gate and weakens no
# gate: it changes WHEN each one runs, never WHETHER it exists.
#
# THE ONE ALWAYS-ON GATE IS `capture_gate.sh` (the generic `-captureGate`), and it is
# deliberately NOT routed by this script — see the header of the `capture` job in
# ci.yml for the justification. Every other gate is feature-scoped by construction.
#
# ================================ FAIL OPEN ==================================
# The load-bearing safety property: EVERY uncertainty runs MORE gates, never fewer.
#   * a changed path that matches NO rule below            -> ALL gates
#   * an empty / unobtainable changed-path list            -> ALL gates
#   * a git failure, a missing/!valid base sha             -> ALL gates
#   * this script erroring out (it still writes all-true)  -> ALL gates
# And a second, independent belt at the YAML layer: ci.yml gates on
# `steps.scope.outputs.run_<key> != 'false'`, so a MISSING output (this step never
# ran, or crashed before writing) also runs the gate. A skipped gate must never be
# reachable by accident — only by an explicit `false` this script wrote on purpose.
# `tests/scripts/test_scope_gates.sh` pins both halves.
# =============================================================================
#
# Usage:
#   scope_gates.sh --github-output          # CI: compute the diff, write $GITHUB_OUTPUT + a log table
#   scope_gates.sh --files <file|->         # decide from a newline-separated path list; print run_<k>=<v>
#   scope_gates.sh --map <path>             # print the gate keys ONE path triggers (ALL / NONE / "a b c")
#   scope_gates.sh --map-many [file|-]      # same, for MANY paths, one process: "<path>\t<keys>" per line
#   scope_gates.sh --keys                   # print the routed gate keys, one per line
#   scope_gates.sh --always                 # print the ALWAYS-ON (unrouted) gate wrapper names
#
# Env read in --github-output mode (ci.yml supplies them; both optional -> fail open):
#   SCOPE_BASE_SHA   the diff base (pull_request base sha, or push `before`)
#   SCOPE_HEAD_SHA   the diff head (defaults to HEAD)
set -uo pipefail

# ---------------------------------------------------------------------------
# The routed gate keys. ORDER IS THE ci.yml STEP ORDER (keep them in step order so a
# reader can diff this list against the job top-to-bottom). `capture` is NOT here —
# it is the always-on gate and carries no condition at all.
#
# A NEW capture gate wired into ci.yml MUST be appended here AND given routing rules
# in gates_for_path() below. `tests/scripts/test_scope_gates.sh` reds if a wrapper
# registered in test_gate_scripts.sh's launch-mode lists has no key here, no ci.yml
# condition, or a key no path can ever reach.
# ---------------------------------------------------------------------------
ROUTED_KEYS="settings buildmenu pond loot water chop placement heldbelt sky invdragghostpos weaponset mine boulder boar heldwood"

# The gate wrappers that run UNCONDITIONALLY. Exactly one today, by design.
ALWAYS_GATES="capture_gate.sh"

# ---------------------------------------------------------------------------
# gates_for_path <path> -> "ALL" | "NONE" | "<key> [<key>...]"
#
# The `case` arms are ordered SPECIFIC -> GENERAL and the first match wins; bash
# `case` globs match `/` freely, so a later general arm can swallow an earlier
# specific one if the order is inverted. The trailing `.meta` is stripped by the
# caller, so no arm needs a `.meta` variant.
#
# Three tiers:
#   NONE — cannot change a single pixel or exit code of the SHIPPED exe.
#   ALL  — a shared surface every gate rides (the scene, the rig, the camera, the
#          inventory/looter seams, the render config, CI itself).
#   keys — feature-scoped. Evidence for each mapping: the gate's own component
#          references (measured with a type-name grep over Assets/Scripts/Runtime)
#          plus the seams each gate's ci.yml step header already names.
#
# When in doubt an arm lists MORE keys. Widening one is a one-line edit; a missing
# key is a regression that ships.
# ---------------------------------------------------------------------------
# GATES_RESULT is set by gates_for_path; read it instead of $(gates_for_path ...) in hot
# loops — a command substitution per path forks a subshell, and the reachability proof in
# tests/scripts/test_scope_gates.sh maps ~1400 paths (measured 30s -> <2s with this change).
GATES_RESULT=""
gates_for_path() {
  _gates_case "${1%.meta}"
}
# Assigns GATES_RESULT directly (no echo) so the hot loop forks nothing at all.
_gates_case() {
  local p="$1"
  case "$p" in
    # ---------------- NONE: cannot affect the shipped exe ----------------
    *.md)                                   GATES_RESULT=NONE ;;
    team/*|.claude/*|inspiration/*|docs/*)  GATES_RESULT=NONE ;;
    art-src/*|tools/*)                      GATES_RESULT=NONE ;;
    Assets/Tests/*)                         GATES_RESULT=NONE ;;
    .gitignore|.gitattributes|.editorconfig|.nvmrc|LICENSE*|*.gitkeep) GATES_RESULT=NONE ;;

    # ---------------- ALL: CI + project-level surfaces ----------------
    # A gate script, the workflow, or the gate harness changed -> re-run everything,
    # including the routing this very script performs.
    .github/*|tests/*)                      GATES_RESULT=ALL ;;
    ProjectSettings/*|Packages/*)           GATES_RESULT=ALL ;;
    # The committed scene EVERY gate loads; the render config every frame goes through.
    Assets/Scenes/*|Assets/Settings/*|Assets/Shaders/*|Assets/Resources/*) GATES_RESULT=ALL ;;
    Assets/NavMesh/*|Assets/Prefabs/*)      GATES_RESULT=ALL ;;
    "Assets/UI Toolkit/"*)                  GATES_RESULT=ALL ;;
    Assets/*.asset)                         GATES_RESULT=ALL ;;
    # The hero rig + its clips: every gate renders the castaway.
    Assets/Art/Character/*)                 GATES_RESULT=ALL ;;

    # ---------------- ALL: shared runtime seams ----------------
    # Combat is shared damage/weapon infrastructure, not the boar's private lane —
    # measured: ChopTree/MineOre/MineBoulder/Inventory all reference MeleeAttack or
    # WeaponCatalog. Items/ holds ItemCatalog + InventoryModel (every loot gate).
    Assets/Scripts/Runtime/Combat/*)        GATES_RESULT=ALL ;;
    Assets/Scripts/Runtime/Items/*)         GATES_RESULT=ALL ;;
    Assets/Scripts/Runtime/FarHorizon.Runtime.asmdef) GATES_RESULT=ALL ;;
    # The rig chain (procedural-animation-verbs.md's order 50->110 contract).
    Assets/Scripts/Runtime/Castaway*)       GATES_RESULT=ALL ;;
    Assets/Scripts/Runtime/TwoBoneIkSolver.cs|Assets/Scripts/Runtime/TwoHandGripRead.cs) GATES_RESULT=ALL ;;
    # Capture plumbing every gate's frames go through.
    Assets/Scripts/Runtime/CaptureGate.cs|Assets/Scripts/Runtime/RenderTextureCapture.cs|Assets/Scripts/Runtime/VerifyCaptureFraming.cs) GATES_RESULT=ALL ;;
    # Camera / locomotion / boot: every capture frames through these.
    Assets/Scripts/Runtime/OrbitCamera.cs|Assets/Scripts/Runtime/WasdMovement.cs|Assets/Scripts/Runtime/ClickToMove.cs) GATES_RESULT=ALL ;;
    Assets/Scripts/Runtime/BuildInfo.cs|Assets/Scripts/Runtime/FullscreenBoot.cs|Assets/Scripts/Runtime/BootHud.cs|Assets/Scripts/Runtime/SurvivalHud.cs) GATES_RESULT=ALL ;;
    # Inventory + looting + the left-click arbiter: 8+ gates drive these seams.
    Assets/Scripts/Runtime/Inventory.cs|Assets/Scripts/Runtime/PickableLooter.cs|Assets/Scripts/Runtime/IPickable.cs) GATES_RESULT=ALL ;;
    Assets/Scripts/Runtime/LeftClickConsume.cs|Assets/Scripts/Runtime/SurvivalNeed.cs) GATES_RESULT=ALL ;;
    # Editor code that BAKES the scene / builds the exe / generates the assets.
    Assets/Scripts/Editor/BootstrapProject.cs|Assets/Scripts/Editor/MovementCameraScene.cs|Assets/Scripts/Editor/WorldBootstrap.cs) GATES_RESULT=ALL ;;
    Assets/Scripts/Editor/QualityPassGen.cs|Assets/Scripts/Editor/LowPolyZoneGen.cs|Assets/Scripts/Editor/LowPolyMeshes.cs) GATES_RESULT=ALL ;;
    Assets/Scripts/Editor/FarHorizonBuilder.cs|Assets/Scripts/Editor/CharacterAssetGen.cs|Assets/Scripts/Editor/NextIslandPoc*) GATES_RESULT=ALL ;;

    # ---------------- feature-scoped: settings / dev panels ----------------
    Assets/Scripts/Runtime/SettingsVerifyCapture.cs)  GATES_RESULT="settings" ;;
    Assets/Scripts/Runtime/Settings/*)                GATES_RESULT="settings buildmenu invdragghostpos" ;;  # UiInputGate is shared across panels
    Assets/Scripts/Runtime/DebugOverlay*.cs|Assets/Scripts/Runtime/INudgePanel.cs) GATES_RESULT="settings" ;;
    Assets/Scripts/Runtime/CameraFollowNudgeTool.cs)  GATES_RESULT="settings" ;;
    Assets/Scripts/Runtime/WorldLookNudgeTool.cs)     GATES_RESULT="settings pond sky" ;;
    Assets/Scripts/Runtime/PondNudge.cs)              GATES_RESULT="settings pond" ;;
    Assets/Scripts/Runtime/AxeNudgeTool.cs)           GATES_RESULT="settings heldbelt heldwood" ;;
    Assets/Scripts/Runtime/HeldWeaponCycleDebug.cs)   GATES_RESULT="settings heldbelt heldwood mine boulder" ;;
    Assets/Scripts/Runtime/WarmthNeed.cs)             GATES_RESULT="settings" ;;
    Assets/UI/*)                                      GATES_RESULT="settings buildmenu invdragghostpos" ;;

    # ---------------- feature-scoped: build menu / placement ----------------
    Assets/Scripts/Runtime/BuildMenuUI.cs|Assets/Scripts/Runtime/BuildMenuVerifyCapture.cs) GATES_RESULT="buildmenu" ;;
    Assets/Scripts/Runtime/CraftingMenuUI.cs)         GATES_RESULT="buildmenu chop" ;;
    Assets/Scripts/Runtime/CraftingTable*.cs)         GATES_RESULT="buildmenu chop placement" ;;
    Assets/Scripts/Runtime/Campfire*.cs|Assets/Scripts/Runtime/Forge*.cs) GATES_RESULT="buildmenu placement mine" ;;
    Assets/Scripts/Runtime/HeldWeaponPlacement.cs)    GATES_RESULT="buildmenu placement heldbelt heldwood" ;;
    Assets/Scripts/Runtime/IBuildPlaceable.cs)        GATES_RESULT="buildmenu placement" ;;
    Assets/Scripts/Runtime/PlacementObstacle*.cs|Assets/Scripts/Runtime/PlacementVerifyCapture.cs) GATES_RESULT="placement" ;;

    # ---------------- feature-scoped: world look / pond / sky ----------------
    Assets/Scripts/Runtime/FreshwaterPond.cs)         GATES_RESULT="pond water" ;;
    Assets/Scripts/Runtime/FreshwaterPondVerifyCapture.cs) GATES_RESULT="pond" ;;
    Assets/Scripts/Runtime/WorldLook*.cs)             GATES_RESULT="pond sky" ;;
    Assets/Scripts/Runtime/CloudDrift.cs|Assets/Scripts/Runtime/SkyVerifyCapture.cs) GATES_RESULT="sky" ;;

    # ---------------- feature-scoped: loot / needs ----------------
    Assets/Scripts/Runtime/LootPrompt.cs)             GATES_RESULT="loot water" ;;
    Assets/Scripts/Runtime/LootPromptVerifyCapture.cs) GATES_RESULT="loot" ;;
    Assets/Scripts/Runtime/BerryBush.cs|Assets/Scripts/Runtime/EatBerryAction.cs|Assets/Scripts/Runtime/HungerNeed.cs) GATES_RESULT="loot" ;;
    Assets/Scripts/Runtime/StickProp.cs)              GATES_RESULT="loot chop" ;;
    Assets/Scripts/Runtime/ThirstNeed.cs|Assets/Scripts/Runtime/DrinkAction.cs|Assets/Scripts/Runtime/WaterAcquisitionVerifyCapture.cs) GATES_RESULT="water" ;;

    # ---------------- feature-scoped: chop ----------------
    Assets/Scripts/Runtime/ChopTree.cs)               GATES_RESULT="chop placement" ;;
    Assets/Scripts/Runtime/ChopVerifyCapture.cs|Assets/Scripts/Runtime/LogPile*.cs|Assets/Scripts/Runtime/StumpAxe.cs) GATES_RESULT="chop" ;;
    Assets/Scripts/Runtime/AxePickup.cs)              GATES_RESULT="chop heldbelt heldwood placement" ;;

    # ---------------- feature-scoped: held visual (belt + wood tiers) ----------------
    Assets/Scripts/Runtime/Held*.cs)                  GATES_RESULT="chop heldbelt heldwood mine boulder" ;;
    Assets/Scripts/Runtime/AxeVerifyCapture.cs)       GATES_RESULT="heldbelt heldwood" ;;
    Assets/Scripts/Runtime/PickaxePickup.cs)          GATES_RESULT="mine boulder heldbelt heldwood" ;;

    # ---------------- feature-scoped: inventory UI ----------------
    Assets/Scripts/Runtime/InventoryUI.cs)            GATES_RESULT="invdragghostpos buildmenu" ;;
    Assets/Scripts/Runtime/InventoryDragGhostPosVerifyCapture.cs) GATES_RESULT="invdragghostpos" ;;

    # ---------------- feature-scoped: weapon set / mine / boulder / boar ----------------
    Assets/Scripts/Runtime/WeaponSetVerifyCapture.cs) GATES_RESULT="weaponset" ;;
    Assets/Scripts/Editor/WeaponPackAssetGen.cs)      GATES_RESULT="weaponset heldbelt heldwood chop mine boulder" ;;
    Assets/Art/Props/*)                               GATES_RESULT="weaponset heldbelt heldwood chop mine boulder" ;;
    Assets/Scripts/Runtime/MineOre.cs|Assets/Scripts/Runtime/MineVerifyCapture.cs|Assets/Scripts/Runtime/OrePile*.cs) GATES_RESULT="mine" ;;
    Assets/Scripts/Runtime/MineBoulder.cs)            GATES_RESULT="boulder placement" ;;
    Assets/Scripts/Runtime/BoulderVerifyCapture.cs|Assets/Scripts/Runtime/Stone*.cs) GATES_RESULT="boulder" ;;
    Assets/Scripts/Runtime/BoarVerifyCapture.cs)      GATES_RESULT="boar" ;;

    # ---------------- author-run probes with NO CI wrapper ----------------
    # These components are INERT unless their own -verify* flag is passed, and no gate
    # wrapper passes it (test_gate_scripts.sh's gate-wiring loop is what holds that
    # true). A compile break in one reds the BUILD job, not a capture gate. This arm
    # sits AFTER every wired *VerifyCapture arm above, so it can only ever catch the
    # unwired remainder. A newly-WIRED gate whose component lands here is caught by
    # test_scope_gates.sh's registration guard, not by this arm.
    Assets/Scripts/Runtime/*VerifyCapture.cs)         GATES_RESULT=NONE ;;
    Assets/Scripts/Runtime/*Diag*.cs|Assets/Scripts/Runtime/*Trace*.cs|Assets/Scripts/Runtime/*Probe*.cs) GATES_RESULT=NONE ;;
    Assets/Scripts/Runtime/BootScreenshot.cs|Assets/Scripts/Runtime/ClickMarker.cs|Assets/Scripts/Runtime/FpsCounterHud.cs) GATES_RESULT=NONE ;;

    # ---------------- FALL OPEN ----------------
    # Anything unmapped runs everything. This is the rule that makes an incomplete
    # table SAFE: a new file nobody routed costs runner time, never coverage.
    *)                                                GATES_RESULT=ALL ;;
  esac
}

# ---------------------------------------------------------------------------
# decide <path-list-file> -> prints `run_<key>=true|false`, one per line, in ROUTED_KEYS order.
# Also prints a `scope_reason=` line naming WHY (all-open / routed / empty).
# ---------------------------------------------------------------------------
decide() {
  local list="$1"
  local all=0 reason="routed" selected=" "

  if [ ! -s "$list" ]; then
    all=1; reason="fail-open: empty changed-path list (no diff obtainable)"
  else
    while IFS= read -r path; do
      [ -z "$path" ] && continue
      gates_for_path "$path"
      case "$GATES_RESULT" in
        ALL)  all=1; reason="fail-open: '$path' matched an ALL rule (shared surface or unmapped path)" ;;
        NONE) : ;;
        *)    for k in $GATES_RESULT; do
                case "$selected" in *" $k "*) : ;; *) selected="$selected$k " ;; esac
              done ;;
      esac
      [ "$all" -eq 1 ] && break
    done < "$list"
  fi

  echo "scope_reason=$reason"
  for k in $ROUTED_KEYS; do
    if [ "$all" -eq 1 ]; then
      echo "run_$k=true"
    else
      case "$selected" in *" $k "*) echo "run_$k=true" ;; *) echo "run_$k=false" ;; esac
    fi
  done
}

# ---------------------------------------------------------------------------
# changed_paths -> writes the changed-path list to $1. Emits an EMPTY file on ANY
# doubt, which decide() turns into all-gates-run. Never fails the step.
# ---------------------------------------------------------------------------
changed_paths() {
  local out="$1"
  : > "$out"
  local base="${SCOPE_BASE_SHA:-}" head="${SCOPE_HEAD_SHA:-HEAD}"

  case "$base" in
    ""|0000000000000000000000000000000000000000)
      echo "[scope] no usable base sha (SCOPE_BASE_SHA='$base') — failing OPEN" >&2
      return 0 ;;
  esac
  if ! git cat-file -e "${base}^{commit}" 2>/dev/null; then
    echo "[scope] base sha $base is not a known commit here — failing OPEN" >&2
    return 0
  fi
  if ! git cat-file -e "${head}^{commit}" 2>/dev/null; then
    echo "[scope] head ref $head is not a known commit here — failing OPEN" >&2
    return 0
  fi
  if ! git diff --name-only "$base" "$head" > "$out" 2>/dev/null; then
    echo "[scope] git diff $base..$head failed — failing OPEN" >&2
    : > "$out"
    return 0
  fi
  if [ ! -s "$out" ]; then
    echo "[scope] git diff $base..$head listed NO files — failing OPEN" >&2
  fi
  return 0
}

main() {
  local mode="${1:---github-output}"
  case "$mode" in
    --keys)   printf '%s\n' $ROUTED_KEYS; return 0 ;;
    --always) printf '%s\n' $ALWAYS_GATES; return 0 ;;
    --map)    gates_for_path "${2:-}"; printf '%s\n' "$GATES_RESULT"; return 0 ;;
    --map-many)
      # ONE process for N paths — the reachability proof in tests/scripts/test_scope_gates.sh
      # maps every tracked file in the repo, which is ~1400 paths and must not be ~1400 forks.
      local src="${2:--}" path tmp
      tmp="$(mktemp)"
      if [ "$src" = "-" ]; then cat > "$tmp"; else cat "$src" > "$tmp"; fi
      while IFS= read -r path; do
        [ -z "$path" ] && continue
        gates_for_path "$path"
        printf '%s\t%s\n' "$path" "$GATES_RESULT"
      done < "$tmp"
      rm -f "$tmp"
      return 0 ;;
    --files)
      local src="${2:--}" tmp rc
      tmp="$(mktemp)"
      if [ "$src" = "-" ]; then cat > "$tmp"; else cat "$src" > "$tmp"; fi
      decide "$tmp"; rc=$?
      rm -f "$tmp"
      return $rc ;;
    --github-output)
      local tmp; tmp="$(mktemp)"
      changed_paths "$tmp"
      echo "[scope] changed paths ($(wc -l < "$tmp" | tr -d ' ')):"
      sed 's/^/[scope]   /' "$tmp"
      local decision; decision="$(decide "$tmp")"
      rm -f "$tmp"
      printf '%s\n' "$decision" | sed 's/^/[scope] /'
      if [ -n "${GITHUB_OUTPUT:-}" ]; then
        printf '%s\n' "$decision" >> "$GITHUB_OUTPUT"
      else
        echo "[scope] WARN — GITHUB_OUTPUT unset; ci.yml's '!= false' conditions fail OPEN (all gates run)" >&2
      fi
      echo "[scope] always-on (unrouted): $ALWAYS_GATES"
      return 0 ;;
    *)
      echo "usage: scope_gates.sh [--github-output|--files <file|->|--map <path>|--keys|--always]" >&2
      return 2 ;;
  esac
}

main "$@"
