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

## Accuracy + performance gates (Erik research, 2026-06-15)

Folded from Erik's developer-accuracy / performance note (`team/erik-consult/developer-accuracy-performance-research.md`, not auto-read). These three gates close recurring failure classes the float saga + world-look churn exposed; they bind alongside the 6-point "complete" rubric above. Full adoption is ticketed (`86ca9a340` / `86ca9a36g` / `86ca9a3b3`) — these lines make the GATE mandatory now, even before the harness/tooling lands.

1. **Diagnose-Before-Fix.** A `fix(...)` PR MUST state the DIAGNOSED root cause + the cited evidence (trace excerpt / log line / failing assertion + values) in the PR body BEFORE the fix — not "tried X, seems better." This formalizes the isolation-probe method; it exists because guess-fixes cost 2–4 soak-overturns per defect (the float saga overturned its own root-cause framing ≥3×; the world-look fix-shape was wrong twice — only trace caught the real cause each time). A fix PR whose body asserts a fix without naming the diagnosed cause + evidence is bounced.
2. **PlayMode locomotion-sampling tests.** Any feature whose correctness is PER-FRAME during motion (grounding, held-prop envelope, finger-curl, camera follow) ships a `[UnityTest]` that `yield return null`-samples the assertion EVERY frame across a real WALK — not just a standing/spawn snapshot. The "tests green but Sponsor sees during-walk elevation" gap is exactly the standing-only assertion missing the motion sample (the smoothing-lag float AND the sole-vs-root float both passed at-rest tests). Sample per-frame through real `Update` + a real `Time.time` window (never the headless `Time.deltaTime~=0` trap — see "Multi-step-loop coverage" below).
3. **SRP-batcher Frame-Debugger audit before any new visual pass.** Before shipping a new shader / material / scatter surface, audit the Frame Debugger that the SRP batcher is actually batching (no per-instance break) — `CBUFFER_START(UnityPerMaterial)` completeness + no live `MaterialPropertyBlock` breaking the batch. Catches the silent perf regression where a new visual pass quietly drops the frame rate before it reaches the Sponsor.

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
4. **Frame-Debugger / SRP-Batcher audit (any new shader or renderer).** Before the Self-Test Report is posted — i.e. BEFORE merge, NOT after the Sponsor's soak — any PR that adds a new shader, material, scatter surface, or visual renderer must verify in the **Frame Debugger** (or the Rendering Debugger's SRP Batcher stats panel) that the new renderer falls INSIDE an SRP batch. Confirm two things and quote the result in the Self-Test Report: (a) **no `MaterialPropertyBlock`-induced break** — the renderer carries no live `MaterialPropertyBlock`/`SetPropertyBlock` (an MPB on a MeshRenderer disables SRP batching for that renderer AND is a GPU Resident Drawer disqualifier — unity6-mastery §2; it is also mutually exclusive with GPU Instancing — Erik §D Evidence D1); and (b) **all shader properties live inside `CBUFFER_START(UnityPerMaterial)` … `CBUFFER_END`** (a property declared outside the cbuffer silently drops the shader out of the SRP-batchable set). This catches the FPS-regression class where a new visual pass quietly breaks batching — a regression that would otherwise only surface in a Sponsor soak, never at PR time. Colour scatter renderers via distinct inline `sharedMaterial` instances (cheap — SRP batches by shader VARIANT, not material count: unity6-mastery §2), never via per-instance MPB. (This operationalizes the "SRP-batcher Frame-Debugger audit" gate declared in §Accuracy + performance gates item 3.)

**The standard capture component:** new verification captures use the reusable `CaptureGate` MonoBehaviour (launched with `-captureGate`, serialized into the Boot scene), not a new one-off hook — the gate scripts inspect its `capture_NN.png` output. One-off probes (`-verifyMove`, feature-specific tours) remain fine for proving a SPECIFIC behavior, but the black/empty-frame backstop standardizes on `CaptureGate`.

---

## Multi-step-loop coverage — the full-cycle convention (U2-7, ticket 86ca8bdhy)

When a feature is a CHAIN of beats that hand state to each other (the M-U2 survival loop: decay → craft → chop → place → restore), per-beat suites tested in isolation are necessary but NOT sufficient — they each spin up their OWN throwaway rig, so a regression in the HAND-OFF between beats (the chopped wood reaching the placement gate; the lit fire restoring the SAME need instance the decay drained) can pass every isolated suite and still ship a broken loop. The convention is a two-tier loop gate:

1. **One in-process end-to-end PlayMode test on ONE shared rig** drives the whole chain in a single sequence, asserting at each beat on the SAME state the previous beat mutated (`SurvivalLoopPlayModeTests.FullCycle_EndToEnd_ClosesTheLoop` is the template). This catches hand-off regressions headless/fast; it runs through real `Update` + a real `Time.time` window (never per-frame deltas — the headless `Time.deltaTime~=0` trap, unity-conventions.md §headless time).
2. **The shipped-build `-verifyLoop` capture** (`CampfireVerifyCapture`, logs `LOOP CLOSED=`) drives the SAME chain through the real NavMesh + click-move in the exe and quits non-zero if the loop does not close — the editor-vs-runtime backstop the in-process test cannot provide.

**Lifecycle trap when constructing the rig:** a `WarmthNeed` (or any `MonoBehaviour` with `Start`-seeded state) added via `AddComponent` in `[SetUp]` runs `Start()` BEFORE the test body executes — set `startFull = true` in SetUp so `Start()` deterministically seeds the value, then re-seed per-test AFTER the first `yield return null` via the public hooks (`SatisfyFull`/`AddWarmth`); never rely on a SetUp-set inspector flag still being read at `Start` time.

**Success-test discipline (the loop-break catch):** a multi-step-loop PR must DEMONSTRATE the end-to-end test catches a deliberate break of the closing seam (PR body documents: break locally → red, with the failing assertion + values; restore → green). For U2-7 the break was no-op'ing `Campfire.AddWarmth` — warmth then kept decaying at the fire (`0.88 < 0.99`), turning the Beat-4 `Assert.Greater` red exactly as designed.
