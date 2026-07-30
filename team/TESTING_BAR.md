# Testing Bar — Far Horizon (Unity translation)

**Sponsor directive (2026-05-02, carried from RandomGame):** "I want you to use a lot of time testing, I don't want to debug and return findings all the time."

Translation: by the time anything reaches the Sponsor for sign-off, it must already have been hammered thoroughly. Sponsor's role is **acceptance**, not bug-finding. This document is binding on every role; Tess enforces it. Engine surfaces translated to Unity 2026-06-12.

A ticket is "complete" only when ALL of:

1. **Paired tests.** Every behavior change ships EditMode and/or PlayMode tests in the same PR (`FarHorizon.EditTests` / `FarHorizon.PlayTests`). Bug fixes pin the regression first. Edge probes (negative-path + boundary) are part of this, not optional extras.
2. **Green checks.** Full EditMode + PlayMode suites green — verified from the `-testResults` XML's `<test-run result="Passed">` line, not exit codes. CI (from U4 onward) chains BootstrapProject.Run → tests → FarHorizonBuilder.BuildWindows.
3. **Shipped-build verification** (successor to RandomGame's HTML5 gate). Editor evidence is never sufficient — the editor-vs-runtime divergence class (Awake-no-serialize, shader stripping, NavMesh-not-shipped; see `.claude/docs/unity-conventions.md`) is proven by spike incidents. Anything UX/visually-visible needs evidence captured from the BUILT exe (windowed launch, in-game capture, HUD build-stamp visible) attached to the PR/ticket.
4. **Self-Test Report.** UX-visible PRs carry an author-posted Self-Test Report comment (what was run, on which build stamp, what was observed — concrete values only, never invented) before Tess reviews.
5. **Tess sign-off.** QA review verdict (APPROVE / APPROVE_WITH_NITS / REQUEST_CHANGES) as a PR comment. Tess-authored PRs get a Drew/Devon peer reviewer instead (Tess can't self-QA).
6. **Sponsor soak** only where the gate is subjective feel or first-of-class visuals — right-size the ask; always include the exe path + the expected HUD build stamp. **State the bar (bounded silence).** The soak ask must NAME the quality bar this iteration tested + the surfaces in scope, and list the bars NOT tested — "this looks done" is not a convergence claim; "tested bar B on surfaces S; NOT tested: …" is. See § "Predict-Before-Soak + bounded silence" below.

---

## Accuracy + performance gates (Erik research, 2026-06-15)

Folded from Erik's developer-accuracy / performance note (`team/erik-consult/developer-accuracy-performance-research.md`, not auto-read). These three gates close recurring failure classes the float saga + world-look churn exposed; they bind alongside the 6-point "complete" rubric above. Full adoption is ticketed (`86ca9a340` / `86ca9a36g` / `86ca9a3b3`) — these lines make the GATE mandatory now, even before the harness/tooling lands.

1. **Diagnose-Before-Fix.** A `fix(...)` PR MUST state the DIAGNOSED root cause + ONE cited isolation result in the PR body BEFORE the fix — not "tried X, seems better." This formalizes the isolation-probe method; it exists because guess-fixes cost 2–4 soak-overturns per defect (the float saga overturned its own root-cause framing ≥3×; the world-look fix-shape was wrong twice — only trace caught the real cause each time). A fix PR whose body asserts a fix without naming the diagnosed cause + evidence is bounced. **Full requirement + worked example: § "Diagnose-Before-Fix — the pre-fix PR convention" below.**
2. **PlayMode locomotion-sampling tests.** Any feature whose correctness is PER-FRAME during motion (grounding, held-prop envelope, finger-curl, camera follow) ships a `[UnityTest]` that `yield return null`-samples the assertion EVERY frame across a real WALK — not just a standing/spawn snapshot. The "tests green but Sponsor sees during-walk elevation" gap is exactly the standing-only assertion missing the motion sample (the smoothing-lag float AND the sole-vs-root float both passed at-rest tests). Sample per-frame through real `Update` + a real `Time.time` window (never the headless `Time.deltaTime~=0` trap — see "Multi-step-loop coverage" below).
3. **SRP-batcher Frame-Debugger audit before any new visual pass.** Before shipping a new shader / material / scatter surface, audit the Frame Debugger that the SRP batcher is actually batching (no per-instance break) — `CBUFFER_START(UnityPerMaterial)` completeness + no live `MaterialPropertyBlock` breaking the batch. Catches the silent perf regression where a new visual pass quietly drops the frame rate before it reaches the Sponsor.
4. **Predict-Before-Soak (the forward sibling of Diagnose-Before-Fix).** Any soak-gated / feel / first-of-class-visual PR MUST carry a falsifiable PRE-soak prediction in its Self-Test Report — "I expect the soak to show Y; I expect Z to NOT appear" — written BEFORE the build is served, then graded against the soak outcome. Diagnose-Before-Fix names the CAUSE before a fix (backward-looking); this names the expected RESULT before serving (forward-looking). It exists because the most expensive recurring loop on this project is asserting "fixed/removed" with no pre-registered prediction a soak then refutes (`claim-removed-but-soak-shows-present`; the 8-soak jump-pullback saga; `soak-fail-test-pass`). Tess bounces a soak-gated PR whose Self-Test Report has no graded prediction + bounded convergence claim. **Full requirement + worked shape: § "Predict-Before-Soak + bounded silence" below.**

---

## Diagnose-Before-Fix — the pre-fix PR convention (ticket 86ca9a340; Erik research §A + Rank 1)

**Source:** Erik R&D note `team/erik-consult/developer-accuracy-performance-research.md` §A + Rank 1 — Erik's #1-impact, zero-tooling recommendation. This section is the binding spec for the one-line gate stated in "Accuracy + performance gates" item 1 above.

**The gate.** Any `fix(...)` PR whose subject is a diagnosed defect MUST carry, in the PR body, BEFORE the fix description, both of:

- **(a) the diagnosed ROOT CAUSE in one sentence** — what the actual broken thing is, named concretely (the system + the mechanism), not "it looked wrong so I changed X."
- **(b) ONE cited isolation result that confirms (a)** — a single piece of evidence that the named cause is real and that other hypotheses were ruled out. Acceptable forms:
  - a **probe flag + its output** (`-seaWaterOnly` → `0 water px`; `-footTrace` → `foot-float -0.003u, shadow-to-feet 0.087u`),
  - a **magenta-diff pixel count** (sentinel-color rebuild + pixel-diff: `248/921600 changed = 0.027%` ⇒ mesh contributes no visible pixels),
  - a **trace dump line** (`[FloatTrace]` / `[TINTDIAG]` / `ClipBaselineDiagnose: Idle −0.003, Walk +0.63`),
  - a **guard that reds→greens** (a failing assertion + its values that the fix flips green).

One isolation result is the floor, not a transcript of every probe — cite the ONE that nailed the cause.

**Enforcement.** **Tess bounces** any `fix(...)` PR whose body asserts a change without naming the diagnosed cause + a cited isolation result (REQUEST_CHANGES, one-line note: "Diagnose-Before-Fix: PR states a fix but no diagnosed root cause + cited isolation result"). A guess-fix that only describes the change does not pass the bar.

**Why it pays.** Every walk-float / world-look convergence in `.claude/docs/unity-conventions.md` happened ONLY after an isolation probe forced the real system to surface; the reverse pattern (guess a fix → rebuild → soak → get contradicted) cost 2–4 build-and-soak rounds per defect. Stating the diagnosis up-front collapses those rounds into one and lets the reviewer check the *cause*, not just the diff.

**Worked example (what "good" looks like — from the walk-float saga; cited to `.claude/docs/unity-conventions.md` §FBX/rigs, walk-float saga + ground-snap entries, do not re-derive here):**

> **Diagnosed root cause:** The avatar reads as "still floating" not because the body is ungrounded but because the **blob shadow is stranded ~9 cm above the snapped feet** — the body IS grounded; the shadow rides a fixed Y while the feet snap to the visible terrain, so the *shadow-to-feet gap* is what reads as float.
> **Cited isolation result:** `-footTrace` overturned the body-snap hypothesis — measured **foot-float −0.003u** (feet ARE planted) while **shadow-to-feet was 0.087u**, dropping to **0.023u** after making the shadow track the snapped feet.
> **Fix:** make the blob shadow track the snapped feet rather than a fixed Y.

The diagnosis sentence names the real system (shadow-vs-feet, not root grounding); the single `-footTrace` line both confirms it AND rules out the obvious-but-wrong hypothesis (body-snap). That is the shape every `fix(...)` PR must hit. (The broader saga overturned its root-cause framing ~6× — NavMesh-slab → renderer-enabled-hit → blob-shadow → `SkinnedMeshRenderer.bounds` false-green → `BakeMesh` actual-lowest-vertex — each overturn forced by a probe; see the saga entry for the full chain.)

**Out of scope — when the gate does NOT apply.** Plain logic bugs that just need a stack trace are NOT in scope (Erik §A: "Plain logic bugs ... are NOT in scope"). A `fix(...)` whose root cause is obvious from a thrown exception, a failing unit test's assertion message, or a null-ref stack trace does not need a manufactured isolation probe — the stack trace IS the diagnosis. The gate targets the **diagnose-via-trace / asset-is-fine-the-view-is-the-problem class**: defects where the visible symptom names the WRONG system (a colour tweak that can't fix a not-rendering mesh; a water-Y change for a composition problem) and an isolation probe is the only thing that surfaces the real cause. When in doubt, the one-sentence diagnosis is cheap — write it.

---

## Predict-Before-Soak + bounded silence (the forward half of Diagnose-Before-Fix; ticket-free, applies now)

**Why this exists.** Diagnose-Before-Fix forces you to name the *cause* before a fix (backward-looking). It does nothing about the project's single most expensive loop: serving a build with a "fixed/removed/done" claim that the Sponsor's soak then refutes — `claim-removed-but-soak-shows-present`, the 8-soak jump-pullback saga, `soak-fail-test-pass`. The cure is a falsifiable prediction written BEFORE the build is served and GRADED against the soak, plus a convergence claim bounded to a NAMED bar.

**The gate (soak-gated / feel / first-of-class-visual PRs).** The Self-Test Report MUST carry, before the build reaches the Sponsor:

- **(a) Prediction (pre-soak, falsifiable):** one line in the shape *"I expect the soak to show **Y**. I expect **Z** to NOT appear."* — concrete, observable, specific enough to be wrong. Not "it should look better."
- **(b) Convergence claim (bounded silence):** *"Tested bar **B** on surfaces **S**; bars NOT tested: **…**."* A "done"/"removed"/"fixed" claim is only honest against a NAMED bar; an unbounded "looks done" is the form most often overturned by the next soak testing a *different* bar. (You already run three independent evaluator families — peer APPROVE + Tess PASS + Sponsor soak; this is the missing named-bar discipline that turns "looks done" into an honest convergence claim.)
- **(c) After the soak — Outcome vs prediction:** was the prediction borne out? A refuted prediction is a *finding*, not a failure — it means the claim's foundation was wrong. Per `claim-removed-but-soak-shows-present`: STOP and deep-investigate WHY the foundation was wrong before re-fixing; never re-assert "removed" until a capture of the actual symptom region proves it.

**Enforcement.** Tess bounces (REQUEST_CHANGES, one-line note: "Predict-Before-Soak: soak-gated PR with no falsifiable pre-soak prediction / unbounded convergence claim") any soak-gated PR whose Self-Test Report lacks (a)+(b). Pairs with — does not replace — Diagnose-Before-Fix on `fix(...)` PRs.

**Out of scope.** Mechanical `chore`/`docs`/`test` PRs, and non-soak-gated changes whose correctness is fully covered by a green paired test — there is nothing for a soak to refute.

**Upstream pre-empt.** The *cheapest* place to win this is BEFORE the dispatch — name the Sponsor's real bar up front via the `/name-the-bar` skill (`team/quality-bars.md`), so the prediction in (a) is written against a *confirmed* bar instead of a guessed one.

**Worked shape:**
> **Prediction (pre-soak):** I expect the pond to read as an organic, irregular outline at gameplay-cam height; I expect NO perfect-circle silhouette and NO "pond-in-a-mound" raised rim.
> **Convergence claim (bounded):** Tested the *organic-outline* bar on the gameplay-cam side profile; NOT tested: foam pulse, reflection, night lighting.
> **Outcome vs prediction (post-soak):** …

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

---

## Gate-trust contrastive-pair test (when a CI gate smells false-green)

A green gate is only worth trusting if it can go RED. When a verify-gate is SUSPECTED of false-green — it passes but the symptom it is supposed to catch is present in the soak (`unity-verify-gate-false-green` — a stale pre-gate verify-log poisoned the evidence; the false-green hid the #130 pond foam for 3 rounds via a warm `ci-out/` + `clean:false`) — do NOT just re-read the gate. PROVE it discriminates with a pre-registered pair:

1. **Pre-register (in writing, BEFORE running):** state what the gate SHOULD do on a case it must PASS vs a case it must FAIL — e.g. "clean build WITH the fix → PASS; clean build with the fix REVERTED → FAIL." If you cannot state how a passing vs a failing run look different, the gate is unfalsifiable and its green means nothing.
2. **Run BOTH** — the should-pass case and the should-fail case — from the SAME clean state (cold `ci-out/`, `clean:true`; a warm cache is exactly how the false-green slips in — see `unity-procedural-committed-assets-go-stale`).
3. **Verdict:** the gate is trustworthy ONLY if the should-fail case actually goes RED. If both go green, the gate is not discriminating — fix the gate before trusting ANY of its greens. Trust the POST-gate artifact log + a UTF-16LE DLL string search over a managed assembly (ASCII grep/`strings` lies on wide literals), never a pre-gate `ci-out/` upload.

Borrowed from the reference "earned-autonomy" suite's `probe` skill (contrastive-pair + pre-registration), applied to **gate-trust** rather than agent-reasoning. Use it the next time a soak contradicts a green gate, not as a standing per-PR gate.

---

## Doc-staleness greps — negate the marker context, and DEMONSTRATE the red (ticket `86cayw6ve`)

The sibling of the section above, for the **docs** lane. A docs ticket's success test is usually a grep — *"`grep '<retired phrase>' <doc>` → zero hits."* That form is **unpassable by construction** the moment the doc does the right thing, because the two legitimate shapes both CONTAIN the phrase:

- a **`⚠ CORRECTED` retirement marker**, which quotes the wording it retires *in order to* retire it (the house pattern this doc family merged — `team/uma-ux/item-icon-bake-recipe.md` §2.1's markers quote the retired axe descriptor verbatim); and
- a **DECISIONS citation**, which is accurate AS HISTORY and is protected precisely because rewriting it falsifies the record.

**Why an unpassable test is worse than no test.** A future reader does one of two things with a guard that can never go green: deletes it (the guard is lost), or "fixes" the doc to satisfy it — striking the correction marker or the DECISIONS citation. **The second outcome is the damaging one:** it destroys the record of what was wrong, which is the whole reason the marker exists. Observed on `86caynyq7` / PR #358: `grep -n 'slate/steel' team/uma-ux/ui-toolkit-panels-ux-spec.md` → zero hits reported **2 hits before AND after** the fix, and satisfying it literally would have required breaking two of the ticket's own constraints to satisfy a third.

**The keepable form — negate the CONTEXT MARKER, never the line number:**

    grep -n -i '<retired phrase>' <doc> | grep -v 'CORRECTED\|DECISIONS'   # → expect ZERO LINES

- **Judge the OUTPUT, not the exit code** — a clean run's trailing `grep -v` exits **1**, so an exit-code check reads a passing guard as a failure (same family as "grep the `-testResults` XML's `result=` line, exit codes lie" in item 2 of the rubric).
- **Match the phrase case-INSENSITIVELY (`-i`); keep the EXCLUSION case-SENSITIVE.** A retired phrase's natural shape at a sentence start is *capitalized*, so a case-sensitive match walks straight past a live claim that opens with it. Measured on this doc family: `Slate/steel is the current axe-icon palette. Bake it.` → `lines=0` without `-i`, `lines=1` with it. The asymmetry is deliberate and also measured — the marker words are an ALL-CAPS convention, so folding case into the *second* grep too silently EXCLUDES any live-claim line that merely contains lowercase prose "decisions"/"corrected": `Bake decisions aside, the axe icon is Slate/steel today.` is CAUGHT by `grep -v` (`lines=1`) and MISSED by `grep -vi` (`lines=0`). **Widen the match; never widen the exclusion.**
- **Anchor on the marker word** (`CORRECTED` / `DECISIONS` / `RETIRED`), **never on a line number.** Line numbers move; `86caynyq7` deliberately refused to quote its own pre-merge anchors as current for exactly this reason.
- **Count LINES, not occurrences.** The guard is line-scoped, so a line count is the only thing its output evidences. A doc can legitimately gain an occurrence — a marker or citation quoting the phrase once more — with the line count unchanged, so reporting "2 → 2" without saying *lines* overstates what was verified. When the two numbers diverge, state both and label which is which.
- **State the real bar in words too:** "no line *asserts* the retired phrase as CURRENT" — that is the criterion; the grep is only its mechanical proxy.

**The gate — a staleness grep is not keepable until its RED has been DEMONSTRATED.** Same discipline as the contrastive-pair section above, at docs cost: inject a live claim carrying the phrase into the file, run the guard, confirm it emits the line, revert. Then **state in the PR body which half you actually proved** (green-on-current-tree, red-on-injected-claim, or both). A guard that cannot fail is a false-confidence generator — the `LeftHaftPassSW` cap printed `PASS ✓` for three rounds on a defect it could not see.

**Disclose the residual hole.** The `grep -v` form is line-scoped, so a live claim reintroduced ON a line that also contains `CORRECTED` or `DECISIONS` still slips through. That narrows the hole; it does not close it (closing it needs sentence-level parsing — deliberately not built). Say so when citing this pattern, the same way the source-scan-guard entry in `.claude/docs/unity-conventions.md` states that per-file counting narrows but does not close its hole.

**And disclose what `-i` does NOT buy you.** The case-fold closes the CASE variant of the hole and nothing else — measured with `-i` in place: a live claim sharing a line with `DECISIONS` (`Per DECISIONS, the axe icon is Slate/steel today — bake it that way.`) is still missed (`lines=0`), and so is the `CORRECTED` arm (`lines=0`); a re-spaced variant (`slate / steel`) is still missed too, because a literal grep matches bytes, not sentences. Adding `-i` therefore does not let you drop the residual-hole disclosure — it makes the disclosure *shorter by exactly one case*.

**⚠ And one about this rulebook itself.** The illustrations above quote the retired phrase literally (they are injections, not claims), so **this section does not pass the guard it documents** — measured: run against this file, the guard emits exactly one line, the case-insensitivity bullet above (no line number quoted here, per that bullet's own rule). That is by design, because the guard is scoped to the doc under policy (`<doc>`), never to this file. Don't aim it here, and don't "fix" the illustrations to make it quiet.
