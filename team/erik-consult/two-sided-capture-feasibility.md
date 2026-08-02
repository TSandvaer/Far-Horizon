# Two-Sided Capture Feasibility (ticket `86caz5na6` AC4)

## Question

Ticket `86caz5na6` (Tess's review of PR #380, comment `5137658173`) found that every bar-#10
check runs on the CUED INSTANCE ALONE — it measures *presence*, never *discrimination*. Her
proposed fix (AC4, the load-bearing AC) is an artifact: a capture with the cued instance and a
non-cued instance IN THE SAME FRAME, where a naive viewer points at the cued one. Priya/Tess need
to know, before writing AC4's exact text: **is a two-sided capture artifact feasible given this
project's windowed-capture constraints, and can any part of "a naive viewer points at it" be
automated as a CI gate, or is this inherently a human-judged soak aid?**

## Bottom line

A two-sided (cued + non-cued, same-frame) capture is **mechanically feasible** — this project
already has the exact building blocks (a purpose-built `*VerifyCapture` component pattern that
spawns controlled instances, disables gameplay cameras, and frames them with a deterministic pure
function) — but **no such component exists today**; it must be authored fresh, and it must run
**windowed** on the same runner-1-pinned, already-contended capture lane as every other visual
gate. The "naive viewer points at it" criterion itself is **NOT fully automatable** — it is a
Gestalt/attention judgment with no saliency tooling in this project's stack — so the artifact's
core claim stays a **soak/QA-judged aid**. However, the *narrower* amplitude question (evasion #1:
"invisible at gameplay distance") IS automatable, and better solved WITHOUT a capture at all: a
pure-geometry EditMode test that converts a channel's world-unit amplitude into an on-screen pixel
displacement at a stated FOV/distance, reusing the project's existing deterministic framing math.

## Evidence

- **`Assets/Scripts/Runtime/CaptureGate.cs`, `WeaponSetVerifyCapture.cs`, `VerifyCaptureFraming.cs`,
  `.github/workflows/scripts/capture_gate.sh`** — read directly from source in `Far-Horizon-erik-wt`
  (pinned at `363c1a0`, stale vs `main`'s `90d024b`). **Strong evidence for the ARCHITECTURE
  pattern** (this is foundational capture infra, reused by a dozen `*VerifyCapture` siblings, so the
  pattern is unlikely to have changed shape even if line numbers have moved) — but exact line
  numbers / current API surface should be reconfirmed by the orchestrator against `main` before
  anyone cites this note's code snippets as current fact. What the source shows: (1) `CaptureGate`
  is the STANDARD merge-gate capture — it captures whatever is in view at the gameplay spawn point,
  with zero control over scene content; (2) `WeaponSetVerifyCapture` is a purpose-built capture that
  instantiates a controlled prefab **far from the live scene** (`y=500`), disables every other
  camera, computes bounds via an `Encapsulate()` helper over multiple renderers, and calls
  `VerifyCaptureFraming.ComputeFrame(center, size, viewDir, fov, aspect, fill)` — a **pure,
  deterministic function** (no floors, no fallbacks) that derives camera position/rotation from
  settled bounds; (3) both `CaptureGate` and every `*VerifyCapture` component are **inert unless
  windowed** — `ScreenCapture.CaptureScreenshot` returns "Failed to capture screen shot" under
  `-batchmode` per the code's own comment, because there is no real swapchain/GPU frame headlessly.
- **`.claude/docs/unity-conventions.md`** (read from the same stale worktree, but a curated
  incident-log doc, lower churn risk) — **Strong.** Documents the false-green capture-rig trap
  directly relevant here: "Even a real shipped-exe frame lies if the `-verify<X>` mode
  frames/lights the subject differently than actual play... an isolated verify capture is a
  smoke-test that the asset EXISTS, NOT proof of how it READS in play." This is the exact trap a
  naively-built two-sided capture would fall into if built as an isolated, neutrally-lit rig shot
  (as `WeaponSetVerifyCapture`'s slate-grey `SolidColor` clear does) rather than sampling the real
  scene's directional light / fog Volume / post-processing at a stated gameplay distance.
- **`.claude/docs/blender-asset-pipeline.md` §12** — **Strong.** Confirms the DEFAULT gameplay
  capture cannot be relied on for a specific held/world object's framing (it's taken at spawn,
  rear-orbit angle) — reinforcing that the two-sided artifact needs a purpose-built capture, not
  the standard merge-gate one.
- **Ticket `86cah7y5b`** (fetched live via ClickUp MCP this session — current, not stale) — **Strong,
  directly on point.** AC3 ships the found `sword_iron`'s attract cue as **bob-ONLY** (Fresnel rim
  is unreachable on the shared `URP/Unlit` weapon material per the ticket's Finding 1), and AC5's
  count/rarity dial is a `SettingsCatalog` difficulty knob that raises **how many** `sword_iron`
  instances spawn per region — it does **not** produce a cued/non-cued pair. Every found-weapon
  instance gets the SAME AC3 bob uniformly; there is no naturally-occurring un-cued `sword_iron` in
  the live world to pair against. **This is the single biggest constraint**: a genuine cued-vs-
  non-cued comparison cannot be captured from ordinary gameplay at any dial setting — it has no
  natural occurrence in-game and must be manufactured in a purpose-built two-instance scene.
- **Project memory `single-unity-build-slot-serializes-orchestration`** (dated 28 days old per its
  own header — treat cited specifics as needing reconfirmation, but the shape is corroborated by
  the `unity-conventions.md` CI-architecture section read the same session) — **Moderate-strong.**
  Confirms windowed captures are **pinned to runner-1** post the `#203` CI split (`build` job is
  headless/either-runner; `capture`+advisory `playmode` share `unity-capture`, locked to runner-1);
  "unity+structure green" does not free the runner — the advisory `playmode` job holds it ~15 more
  minutes. Any new windowed capture step lands on this same contended lane.
- **`team/erik-consult/ci-build-capture-split-spec.md`** (own prior research, read from the stale
  worktree) — **Moderate.** Explicitly labels the `-nographics`/display-adapter-perturbation claim
  "plausible, unverified hypothesis" — I am carrying that same honesty forward here rather than
  overstating certainty. `playmode-enter-headless-deadlock-research.md` corroborates from Unity
  community reports (forum-level, not Unity-staff-confirmed) that `-nographics` is unsupported/hangs
  for anything needing a real graphics context, consistent with `CaptureGate.cs`'s own comment.
- **My own engineering-feasibility read of "can a mechanical proxy stand in for a naive viewer" —
  Weak-to-none as evidence, this is analysis, not a citation.** No saliency-model or
  attention-prediction tooling exists anywhere in this project's toolchain, and the project's
  in-house-tooling posture (`[[in-house-asset-routes-over-paid-tools]]`) argues against reaching for
  an external ML saliency scorer to answer a Gestalt question. A pixel-diff between two bounding
  regions is NOT a valid proxy for "which one draws the eye" — a difference can exist while reading
  as *worse* (e.g. the cued instance renders darker/occluded) rather than *more special*; a
  threshold on that metric would pass cases a human would reject.

## Application to Far Horizon

**Q1 — two instances, one frame, at gameplay framing.** Feasible as a NEW component built on the
existing `*VerifyCapture` pattern (reuse `Encapsulate()` over both instances' renderers + extend
`VerifyCaptureFraming.ComputeFrame` to fit a combined bounds), but two design forks matter:
(a) an **isolated rig** (spawn both far from the world, neutral clear, tight product-shot framing —
cheap, fast to build, but per the false-green lesson above it does NOT answer "reads as special at
gameplay distance," which is exactly the claim evasion #1 is about) vs (b) an **in-scene hybrid**
(spawn both instances at a real in-scene position so they inherit the actual directional light/fog
Volume/post-processing, with the capture camera held at a STATED distance and FOV matching gameplay
framing, not a tight product angle). (b) is the version that actually answers Tess's question and
is the one worth building; (a) is cheaper but would ship a weaker artifact than the desaturate test
it's meant to match in objectivity. Either way, this is new authoring work, not a config flip.

**Q2 — windowed vs headless.** Yes, must be windowed — no exception exists anywhere in this
project's capture stack, and none is plausible (`ScreenCapture` needs a real swapchain). Cost: it
runs on the SAME runner-1-pinned lane already shared by the merge-gate capture and the advisory
`playmode` job — a lane already documented as the project's dominant orchestration constraint (≤1–2
Unity-build tickets in flight, captures serialize). Wiring this as a per-PR blocking gate would
stack more time onto that contended lane; making it an **on-demand invocation** (`-verifyXxx` style,
run only when a bar-#10-relevant PR needs the evidence) keeps the cost bounded to "one extra
windowed launch when someone actually needs it," matching how `WeaponSetVerifyCapture` is already
used.

**Q3 — automatable gate vs soak aid.** Honest answer: **this is inherently a soak/QA aid, not a
CI gate**, for the "naive viewer points at it" claim itself — that is a human-attention judgment
with no valid mechanical stand-in in this project's toolchain (see Evidence). Do not manufacture an
automated pass/fail here; that would recreate the same "consumes a sentence instead of an artifact"
weakness Tess flagged in the merged desk check, just dressed as code. What CAN be pulled out and
automated is narrower: convert the amplitude question (Q4) into a pure-geometry check that runs
without any capture at all.

**Q4 — bounding magnitude.** Two different things hide under "magnitude," and only one is
automatable: (1) **geometric/pixel magnitude** — given a channel's world-unit amplitude (e.g. a
0.05u bob) and a STATED camera FOV + distance (e.g. gameplay FOV at 5m, the exact distance Tess
named), the on-screen peak pixel displacement is pure trigonometry — the same math
`VerifyCaptureFraming` already runs in reverse. This can be an EditMode unit test asserting
"amplitude A at distance D and FOV F produces ≥ P pixels" with **no capture, no runner contention,
full CI-native determinism** — this is the right instrument for closing evasion #1 mechanically, and
it is cheaper than the capture artifact. (2) **Perceived/rendered magnitude** — does it actually
catch a human eye against a busy, textured, foggy backdrop, accounting for material contrast and
anti-aliasing — this is NOT capturable by geometry alone and needs either the two-sided capture (a
human judges) or a saliency model (out of scope, in-house-tooling posture). Recommend AC1's text
require BOTH: the geometry floor (automatable, cheap, CI-gated) AND the two-sided capture as the
human-judged soak evidence for the Gestalt claim — do not let one substitute for the other.
