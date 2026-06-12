# Testing Bar — Far Horizon (Unity translation)

**Sponsor directive (2026-05-02, carried from RandomGame):** "I want you to use a lot of time testing, I don't want to debug and return findings all the time."

Translation: by the time anything reaches the Sponsor for sign-off, it must already have been hammered thoroughly. Sponsor's role is **acceptance**, not bug-finding. This document is binding on every role; Tess enforces it. Engine surfaces translated to Unity 2026-06-12.

A ticket is "complete" only when ALL of:

1. **Paired tests.** Every behavior change ships EditMode and/or PlayMode tests in the same PR (`FarHorizon.EditTests` / `FarHorizon.PlayTests`). Bug fixes pin the regression first. Edge probes (negative-path + boundary) are part of this, not optional extras.
2. **Green checks.** Full EditMode + PlayMode suites green — verified from the `-testResults` XML's `<test-run result="Passed">` line, not exit codes. CI (from U4 onward) chains BootstrapProject.Run → tests → FarHorizonBuilder.BuildWindows.
3. **Shipped-build verification** (successor to RandomGame's HTML5 gate). Editor evidence is never sufficient — the editor-vs-runtime divergence class (Awake-no-serialize, shader stripping, NavMesh-not-shipped; see `.claude/docs/unity-conventions.md`) is proven by spike incidents. Anything UX/visually-visible needs evidence captured from the BUILT exe (windowed launch, in-game capture, HUD build-stamp visible) attached to the PR/ticket.
4. **Self-Test Report.** UX-visible PRs carry an author-posted Self-Test Report comment (what was run, on which build stamp, what was observed — concrete values only, never invented) before Tess reviews.
5. **Tess sign-off.** QA review verdict (APPROVE / APPROVE_WITH_NITS / REQUEST_CHANGES) as a PR comment. Tess-authored PRs get a Drew/Devon peer reviewer instead (Tess can't self-QA).
6. **Sponsor soak** only where the gate is subjective feel or first-of-class visuals — right-size the ask; always include the exe path + the expected HUD build stamp.

---

## test-evidence convention — what the bar expects on a PR

So every PR carries the same shape of proof (and reviewers/CI know exactly what to look for), the mechanical gates are:

**Mechanical gates (CI, `.github/workflows/`):** these run automatically; a PR is not green until they pass.

| Gate | Script (under `.github/workflows/scripts/`) | What it proves | Fails on |
|------|--------|----------------|----------|
| Structure | `structure_check.sh` | repo hygiene, asmdefs, entry-point methods present | committed artifacts, missing `.meta`, renamed entry point |
| Console-error | `check_unity_log.sh` | no compile/fatal errors in any Unity log | `error CS####` / `Compilation failed` / `Fatal error` / `Unhandled exception` (URP first-import + recovered-NavMesh-race lines allowlisted by **shape**, never subtracted from the error scan) |
| Test-result | `parse_test_results.py` | EditMode + PlayMode genuinely green | `result != Passed`, any failure, or `total == 0` (an empty run is a failure) |
| Build-result | `ci.yml` build-gate | the Windows exe actually built | no `[FarHorizonBuilder] result=Succeeded` line |
| **Shipped-build capture** | `capture_gate.sh` + `frame_check.py` | the BUILT exe renders REAL frames (editor-vs-runtime backstop) | black / empty / uniform / all-magenta (shader-strip) frames, or **zero** frames captured |

**Author evidence on the PR (UX-visible PRs):**

1. **Paired tests** in the same PR — EditMode and/or PlayMode, with edge probes; bug fixes pin the regression first. Script-level gate logic gets bash/python unit-style checks (`tests/scripts/`).
2. **Self-Test Report comment** — what was run, **on which build stamp** (`BUILD <tag> | <UTC> | <sha>` from the HUD), what was observed. Concrete values only, never invented.
3. **Shipped-build capture** — run `.github/workflows/scripts/capture_gate.sh Build/Windows/FarHorizon.exe` against your own build and attach/quote the `frame_check.py` PASS line + the build stamp. Editor evidence is necessary, never sufficient (unity-conventions.md §editor-vs-runtime).

**The standard capture component:** new verification captures use the reusable `CaptureGate` MonoBehaviour (launched with `-captureGate`, serialized into the Boot scene), not a new one-off hook — the gate scripts inspect its `capture_NN.png` output. One-off probes (`-verifyMove`, feature-specific tours) remain fine for proving a SPECIFIC behavior, but the black/empty-frame backstop standardizes on `CaptureGate`.
