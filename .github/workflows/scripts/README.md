# CI gate scripts — Far Horizon

The mechanical testing-bar gates (see [`team/TESTING_BAR.md`](../../../team/TESTING_BAR.md)
§ test-evidence convention). Each one decides pass/fail from authoritative evidence,
never from a Unity exit code (unity-conventions.md: "exit codes lie on some failure
classes"). All are invoked by [`../ci.yml`](../ci.yml).

| Script | Runs in | Gate |
|--------|---------|------|
| `structure_check.sh` | hosted `structure` job (no Unity license) | repo hygiene, asmdefs, `.meta` presence, entry-point methods, manifest pin |
| `check_unity_log.sh` | self-hosted `unity` job | zero compile/fatal errors in the Unity logs |
| `parse_test_results.py` | self-hosted `unity` job | EditMode + PlayMode green from the NUnit `<test-run>` line (`total>0` enforced) |
| `capture_gate.sh` + `frame_check.py` | self-hosted `unity` job | the BUILT exe renders real frames (editor-vs-runtime backstop) |
| `scope_gates.sh` | self-hosted `capture` job (first step) | decides WHICH capture gates a diff needs; emits `run_<gate>=true\|false` |

⚠ **The "Runs in" column's `unity` job no longer exists** — PR #203 / `86cafz9tg` split it into
`build` + `capture`. Read `check_unity_log.sh` / `parse_test_results.py` as `build`, and
`capture_gate.sh` / `frame_check.py` as `capture`. Not corrected in place here because the row
text is cited elsewhere; flagged rather than silently churned.

## Console-error gate — `check_unity_log.sh`

Fails on `error CS####` / `Compilation failed` / `Fatal error` / `Unhandled exception`.
Allowlists, by **shape** (not bare substring), two known-benign lines: the URP
first-import terrain shader-dependency warning, and the recovered NavMesh init-order
race (`Failed to create agent because there is no valid NavMesh`, recovered same-frame
by `ClickToMove.EnsureOnNavMesh`). The allowlist is used only for the audit print —
it is **never** subtracted from the error scan, so a real error line that happens to
mention an allowlisted phrase still fails (the masking false-negative fixed in 86ca86g7k).

```bash
.github/workflows/scripts/check_unity_log.sh ci-out/*.log
```

## Shipped-build capture gate — `capture_gate.sh` + `frame_check.py`

The editor-vs-runtime backstop, and **the one ALWAYS-ON capture gate** (see
`scope_gates.sh` below). The standard `CaptureGate` MonoBehaviour (in the Boot scene)
renders N frames from the BUILT exe; `frame_check.py` then fails on black / empty /
uniform / all-magenta (shader-strip) frames, or on zero frames captured.

⚠ **CORRECTED (`86cag93zb`).** This paragraph used to read *"(windowed — `ScreenCapture`
needs a real swapchain, never `-batchmode`)"*. That is no longer true of THIS gate:
`CaptureGate` was converted to headless RT-readback (`RenderTextureCapture` →
`RenderPipeline.SubmitRenderRequest`) and `capture_gate.sh` now launches `-batchmode`
with NO window — see the script's own header and `CaptureGate.cs:19-23`. The retired
sentence is still correct for the eight gates that judge BACKBUFFER pixels; which gate is
which is pinned by `HEADLESS_GATES` / `WINDOWED_GATES` in `tests/scripts/test_gate_scripts.sh`
(take the launch mode from those lists, never from prose — `unity-conventions.md` §Headless).

Run it locally against your own build before posting a Self-Test Report:

```bash
# 1. build (or take the CI artifact):
#    Unity.exe -batchmode -quit -projectPath . \
#      -executeMethod FarHorizon.EditorTools.FarHorizonBuilder.BuildWindows
# 2. capture + gate:
.github/workflows/scripts/capture_gate.sh Build/Windows/FarHorizon.exe ci-out/caps 4
```

`frame_check.py` decodes PNGs with Pillow when present, else a dependency-free stdlib
decoder (8-bit RGB/RGBA, non-interlaced — what Unity's `ScreenCapture` emits), so no
third-party install is required on the runner.

## Capture-gate path router — `scope_gates.sh`

The `capture` job holds SIXTEEN gates. Running all sixteen on every push meant a one-line
docs change replayed the campfire, the sky and the pond — and because the self-hosted
runner is the Sponsor's own laptop, the eight WINDOWED ones did that as visible windows
popping open and shut. This script scopes fifteen of them to what the diff touches.
**No gate is deleted and no gate is weakened — this changes WHEN each runs.**

`capture_gate.sh` is the ONE always-on gate: it is the only scene-agnostic one (every
`verify_*` sibling teleports the player to a specific feature and asserts about it), it is
the pair `team/TESTING_BAR.md` already designates as *the* shipped-build capture gate, and
it is headless, so the always-on cost is zero windows.

```bash
# what does ONE path trigger?  -> ALL | NONE | "<key> <key> ..."
.github/workflows/scripts/scope_gates.sh --map Assets/Scripts/Runtime/ChopTree.cs

# what would a whole diff run?  -> run_<key>=true|false per routed gate
printf 'Assets/Scripts/Runtime/ChopTree.cs\n' | .github/workflows/scripts/scope_gates.sh --files -
```

**Fail-open is the invariant.** Every uncertainty runs MORE gates: an unmapped path, an
empty diff, an unusable base sha, or a crashed scope step all end in all-gates-run — and
ci.yml gates on `!= 'false'` (never `== 'true'`) so a MISSING output also runs the gate.
A gate can only be skipped by an explicit `false` the router wrote on purpose.

**Adding a new capture gate now needs THREE registrations, not two:** the launch-mode list
in `test_gate_scripts.sh`, a key in `ROUTED_KEYS` + routing rules in `scope_gates.sh`, and
the `run_<key> != 'false'` condition on both its ci.yml steps. `tests/scripts/test_scope_gates.sh`
reds on each omission by name.

## Tests for the gates

The gate scripts are themselves tested (the bar applies to itself):

```bash
tests/scripts/test_gate_scripts.sh    # console-error nits + capture-gate black/uniform/magenta/zero-frame + gate wiring
tests/scripts/test_scope_gates.sh     # the path router: mapping, fail-open, three-part registration, routed-and-still-red
```

both on a tmp tree, with no Unity dependency.
