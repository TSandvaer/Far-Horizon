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

The editor-vs-runtime backstop. The standard `CaptureGate` MonoBehaviour (in the Boot
scene) renders N frames from the BUILT exe (windowed — `ScreenCapture` needs a real
swapchain, never `-batchmode`); `frame_check.py` then fails on black / empty / uniform /
all-magenta (shader-strip) frames, or on zero frames captured.

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

## Tests for the gates

The gate scripts are themselves tested (the bar applies to itself):

```bash
tests/scripts/test_gate_scripts.sh
```

covers both the console-error nit fixes and the capture gate's black/uniform/magenta/
zero-frame cases on a tmp tree, with no Unity dependency.
