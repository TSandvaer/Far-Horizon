# What Still Needs a Window — residual headless-capture feasibility (follow-up to `86cag93zb`)

## Question

`86cag93zb`'s RT-readback spike (prior note, `headless-rendertexture-readback-research.md`, committed
`b21f258`) proved `-batchmode` (without `-nographics`) + `SubmitRenderRequest` is the right target for
headless capture, and PR #355 shipped it for a held-mesh gate. The project's own doc rule — *"RT-readback
works for pure world-camera gates, but a held-mesh + live Animator judge stays windowed"* — is now
suspected too strong on the held-mesh half. Narrow question: **what, precisely, still needs a real
swapchain/window once `SubmitRenderRequest`-to-RenderTexture is available**, specifically (a) is "live
Animator ⇒ windowed" real or a myth, (b) what legitimately remains windowed, (c) the honest maximum of
the 8 remaining gates that could go headless plus a per-gate discriminating test.

## Bottom line

**(a) Is very likely a myth, but is NOT fully settled by documented sources — and there is one concrete,
cheap, ALREADY-AVAILABLE check that would settle it before anyone writes new code.** Nothing in Unity's
docs ties Animator *time advancement* to graphics/windowing — the Animator's state machine and clip
evaluation are CPU-side and this repo's own tests already prove they tick under `-batchmode`
(`WaitForSeconds`-driven swing tests pass headlessly). The genuine, sourced, unresolved risk is narrower
than "Animator ⇒ windowed": Unity's **default import culling mode, `AnimatorCullingMode.CullUpdateTransforms`**,
gates *bone-transform writes* (not the state machine) on `Renderer.isVisible`, and there is no Unity
documentation stating whether `SubmitRenderRequest` — which runs "outside the render loop" — updates that
visibility flag in time for the same-frame Animator evaluation. The fix, if needed, is one line
(`Animator.cullingMode = AlwaysAnimate`) and costs nothing to apply preemptively. **Do this before anything
else: check whether `verify_chop_gate.sh` (already headless + green on `main`) captures a frame WHILE
`TriggerChop`'s swing is in progress, not just before/after.** If it does, (a) is already empirically
settled by shipped work and no spike is needed. I could not check this myself (no repo access this
session) — it is the single cheapest next step.

**(b) Genuinely windowed: less than the project's doc currently claims.** OS-cursor/window-focus sampling
stays windowed (repo already proved `Input.mousePosition` reads inert `(0,0)` headlessly) — but this is
close to moot, since the repo-wide invariant already has every capture gate triggering via a synthetic
domain-seam call, never raw `Input`. UI Toolkit Screen-Space-Overlay content is the real remaining
blocker for camera-RT capture — BUT there is a **documented, official, apparently-untried escape hatch**:
`PanelSettings.targetTexture`, present in the Unity 6000.4 Manual/Scripting API for this project's exact
version, lets a UI Toolkit panel render to its *own* RenderTexture, independent of any camera. This is
architecturally different from what the project has tried so far (redirecting a *camera's* target texture)
and is not ruled out by any evidence gathered here or in the prior note. Post-processing/URP Volumes: no
documented or reported obstacle found anywhere — they run inside the same SRP pipeline `SubmitRenderRequest`
already uses; graded Likely-fine, untested. Resolution/DPI-dependent UI scaling (`Screen.width/height` in
batchmode vs. an explicit RT's pixel size) is an open, cheap-to-check risk, not yet confirmed either way.

**(c) Honest maximum: cannot be given as a verified number without reading the actual 8 gates, which this
session is barred from doing.** The evidence bounds it categorically instead: the repo's own doc already
names at least 3 of some-N windowed gates as UI-Toolkit-overlay-inclusive (settings panel, loot/inventory
drag, water-HUD) — none of those convert via camera RT without first spiking `targetTexture`. Any
pure-world-camera + static-mesh gate converts with high confidence (proven 3× already). Any
pure-world-camera + mid-Animator-clip gate (the "boar gate" named in the doc is a plausible candidate) is
gated on the (a) check above. Below is the per-gate checklist to get the real count.

## Evidence

### Q(a) — Does Animator time genuinely require a window?

- **Animator state-machine/clip evaluation is CPU-side; nothing in Unity's docs ties it to graphics
  device init.** `AnimatorUpdateMode` (Normal syncs to the Update-phase game loop; Fixed to physics) and
  `Animator.Update(deltaTime)` (manual, deterministic advance) are both official Scripting API —
  https://docs.unity3d.com/ScriptReference/AnimatorUpdateMode.html,
  https://docs.unity3d.com/ScriptReference/Animator.Update.html. Neither page mentions batchmode,
  `-nographics`, or any rendering precondition. Evidence: Strong (official docs), for "the mechanism has
  no documented graphics dependency" — this is an absence-of-constraint argument, not a positive proof.
- **Repo-internal, Strong: the game loop (Update/LateUpdate) and real elapsed time DO advance under
  `-batchmode` PlayMode tests.** `unity-conventions.md` §Headless/CLI rituals: "`WaitForEndOfFrame` is NOT
  evoked in `-batchmode`" (a *different*, narrower gap — end-of-frame render-pass callback specifically) but
  `WaitForSeconds`/`WaitForSecondsRealtime` are explicitly headless-safe and are the prescribed pattern in
  `procedural-animation-verbs.md` for asserting `SwingNormT > 0 mid-swing`. This proves ordinary per-frame
  ticking (Update/LateUpdate, and therefore Animator's own Update-phase evaluation) functions under
  `-batchmode` — the deltaTime-per-frame is tiny/noisy (documented repo trap: "never assert on per-frame
  deltas; sample over a real `Time.time` window instead") but real wall-clock time visibly accumulates
  across frames. Caveat: `SwingNormT` is this project's own custom additive-curve parameter
  (`CastawayArmPose`), driven by a script, not necessarily proof the underlying **imported Mixamo Animator
  Controller clip** ticks and writes bone poses the same way — see the culling-mode risk below, which is
  about bone-transform *writes*, not state-machine progression.
- **The genuine, unresolved risk: `AnimatorCullingMode.CullUpdateTransforms` (the DEFAULT for imported
  models) gates bone-transform writes on `Renderer.isVisible`, independent of state-machine progression.**
  Official Scripting API — https://docs.unity3d.com/ScriptReference/AnimatorCullingMode.CullUpdateTransforms.html
  — "retarget, IK and write of Transforms are disabled when renderers are not visible, but the state
  machine and root motion will always be evaluated." `AlwaysAnimate` —
  https://docs.unity3d.com/ScriptReference/AnimatorCullingMode.AlwaysAnimate.html — "will always animate
  the entire character, even when offscreen," is the documented override. That the default import setting
  is `CullUpdateTransforms` is corroborated (Moderate — a Unity Discussions thread, not an official manual
  statement, but consistent and matches the general-optimization framing of the enum docs) —
  https://discussions.unity.com/t/how-does-animator-culling-work/176574.
- **The load-bearing gap: no source found stating whether `SubmitRenderRequest` marks the rendered
  renderer(s) `isVisible` in time for the SAME-frame (or even same-request) Animator evaluation.**
  `Renderer.isVisible` official docs — https://docs.unity3d.com/ScriptReference/Renderer-isVisible.html —
  describe visibility as "considered visible when it needs to be rendered in the scene," but say nothing
  about `SubmitRenderRequest` specifically. `SubmitRenderRequest` itself is documented as running "outside
  of the Unity render loop," processed "sequentially within your script, no callback mechanism involved" —
  https://docs.unity3d.com/6000.3/Documentation/Manual/urp/User-Render-Requests.html,
  https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Camera.SubmitRenderRequest.html. Being
  "outside the render loop" is exactly what makes its interaction with the ordinary per-frame
  cull/visibility bookkeeping (which normally updates during the automatic per-frame render) genuinely
  ambiguous from docs alone. **This is squarely unresolved — grade: unresolved, not inferred.**
- **The cheap, correct mitigation regardless of the answer: force `Animator.cullingMode = AlwaysAnimate`
  on any rig captured via RT-readback.** This is documented (Strong) to disable the optimization
  entirely, sidestepping the visibility-timing question rather than depending on its answer.
- **The cheapest test to settle it costs nothing new: check what `verify_chop_gate.sh` already captures.**
  Per the consult brief, this gate is "already headless on `main`" (from the partly-merged `86cag93zb`
  AC1/2/3 work, PR #248/#250). If its capture includes a frame taken WHILE `TriggerChop`'s swing Animator
  clip is playing (not merely before/after), that is already-shipped, repo-internal, Strong evidence that a
  live-Animator mid-clip pose renders correctly via headless RT-readback, and (a) is settled with zero new
  work. I could not verify this from this session (no repo access) — it is the single highest-value next
  check, ahead of building anything new.

### Q(b) — What legitimately remains windowed?

- **UI Toolkit Screen-Space-Overlay content: real blocker for CAMERA-RT capture, but an unexplored escape
  hatch exists.** The project's own established finding (`unity-conventions.md` §Headless/CLI rituals,
  PR-confirmed): "screen-space overlays composite to the backbuffer/swapchain, not into a camera's
  RenderTexture," corroborated by a Unity-staff forum quote on batchmode screenshot capture excluding the
  UIDocument (Moderate — https://discussions.unity.com/t/running-ui-toolkit-with-unity-in-batch-mode-for-visual-test-in-the-ci/891977,
  cited in the prior note). **But `PanelSettings.targetTexture` is official, documented for this project's
  exact Unity version (6000.4)** — https://docs.unity3d.com/6000.4/Documentation/ScriptReference/UIElements.PanelSettings-targetTexture.html
  — "Specifies a Render Texture to render the panel's UI on," listed under the Screen-Space-Overlay
  properties of the Panel Settings Manual page (https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Runtime-Panel-Settings.html:
  "Set the render texture used to render the panel" — Target Texture property; "the target texture takes
  precedence over the target display"). Evidence: Strong that the API exists and applies to this project's
  version; **Hypothesis** that it actually works under `-batchmode` — nobody has tried it in this repo, and
  the existing "hard boundary" finding was established via camera-RT redirection, not panel-targetTexture
  redirection, so it is not evidence against this specific lever. Caveat even if it works: it renders the
  panel to its OWN RT, separate from the world camera's RT — a gate needing world background AND UI
  overlay composited in one judged frame needs an extra compositing step (two RTs blended), not a drop-in
  swap; a UI-content-only gate (no world background needed) needs none of that.
- **OS cursor / window focus: genuinely windowed for anything that SAMPLES it as the judged evidence, but
  largely moot for this repo's gates.** Repo-proven (Strong, PR-cited): `Camera.main` is non-null while
  `Input.mousePosition` sits at `(0,0)` headlessly (`unity-conventions.md` §Headless/CLI rituals, PR #294).
  But the repo-wide invariant (measured 2026-07-30, `86cav8y74`/PR #355) already establishes that **every**
  capture gate triggers through a synthetic domain-seam call (`RequestChopClick`-style latch, `RequestLoot()`
  + `Warp`), never raw `Input`, because the legacy Input Manager has no injection API. So this constraint
  rarely bites a NEW gate's trigger; it would only bite a gate whose JUDGED CONTENT is itself a live-cursor
  readout (e.g., a cursor-position HUD element), which no evidence here suggests exists.
- **Post-processing / URP Volumes: no obstacle found, graded Likely-fine, untested.** No Unity
  documentation, issue-tracker report, or forum thread was found stating post-processing/Volumes behave
  differently under `-batchmode` or with `SubmitRenderRequest` specifically — they execute inside the same
  URP pipeline that `SubmitRenderRequest` already renders through
  (https://docs.unity3d.com/6000.0/Documentation/Manual/urp/post-processing/custom-post-processing-with-volume.html).
  Absence of a documented problem is not proof of correctness — grade **Likely**, and recommend a one-time
  pixel-diff of a post-processed RT-readback frame against a windowed-capture baseline of the identical
  scene before trusting any gate whose evidence depends on bloom/grading/vignette specifically (same
  sRGB/linear caution already flagged in the prior note for plain color).
- **Resolution/DPI-dependent reads: open, untested risk, not confirmed either way.** An RT-readback
  capture's pixel dimensions are whatever the RT was explicitly created at in C#, independent of
  `Screen.width/height` — but UI Toolkit's "Scale With Screen Size" panel scaling reads `Screen.width/height`
  at layout time, and no source was found confirming batchmode's reported `Screen.width/height` (with or
  without `-screen-width`/`-screen-height`) matches the RT's actual pixel size. If any gate's captured
  content includes screen-scaled UI (a candidate even under the `targetTexture` escape hatch above), a
  mismatch here could shift layout vs. the windowed baseline it's compared against. **Hypothesis**, cheap
  to check: compare a headless RT capture's UI layout against a windowed capture's at the same declared
  resolution.

### Q(c) — Per-gate discriminating checklist

Apply in order; the first YES that applies routes the gate.

1. **UI-Toolkit-overlay check.** Does the gate's JUDGED evidence require UI Toolkit panel content
   (settings, dev console, inventory/loot UI, HUD bars) visible in the captured frame?
   - NO → go to 2.
   - YES → not auto-convertible via camera RT (Strong). Spike `PanelSettings.targetTexture` (see Q(b))
     before writing this gate off as permanently windowed — if the judged content is UI-only (no world
     background needed in the same frame), this may convert on its own; if world+UI must appear together,
     it needs a two-RT composite step, which is more work but not a structural dead end.
2. **Native-OS-state check.** Does the gate's JUDGED evidence (not merely its trigger) read real OS cursor
   position, window focus, or other live desktop state?
   - NO → go to 3.
   - YES → stays windowed (Strong — `Input.mousePosition` reads inert headlessly, no injection API).
3. **Static-mesh check.** Is the captured pose a rest/static mesh state — no Animator clip actively
   advancing at capture time?
   - YES → converts with high confidence, same pattern as the 3 gates already proven headless
     (spawn/chop-idle-or-rest/held-belt/sky class).
   - NO (mid-clip Animator pose, e.g. a creature mid-attack) → go to 4.
4. **Mid-clip Animator check — the open one.** First, check (nearly free) whether `verify_chop_gate.sh`
   already captures a frame mid-swing (see Q(a)). If yes, apply the same pattern here (force
   `AlwaysAnimate` + capture mid-clip) and treat as converted pending a quick smoke test. If
   `verify_chop_gate.sh` does NOT already do this, run the spike before converting: force
   `Animator.cullingMode = AlwaysAnimate` on the rig; advance to two known times via `Animator.Update(dt)`
   steps (t=0 rest, t=mid-clip) under `-batchmode` (no `-nographics`); `SubmitRenderRequest`-capture both;
   diff against an EditMode `AnimationClip.SampleAnimation` ground truth at the same times. Frames differing
   by the expected pose delta = converts; frames identical (or stuck at rest) = the culling-mode/evaluation-
   order risk is real and needs a deeper fix (e.g., explicit `Animator.Update` call immediately before the
   `SubmitRenderRequest` call, in the same method, to control ordering).

**Honest maximum, stated as a bound, not a count:** the repo's own doc already names ≥3 of the 8 as
UI-Toolkit-overlay-inclusive (settings panel, loot/inventory drag, water-HUD) — none of those convert
without the `targetTexture` spike, which is untested. That leaves at most 5 of 8 as pure-world-camera
candidates; of those, whichever are static-mesh convert now, and whichever are mid-Animator-clip (the
"boar gate" is the named candidate) are gated on step 4 above. **I cannot give a verified number without
reading the actual 8 gates, which this session is barred from doing — the checklist above is the
mechanism to produce the real number, not this note.**

## Application to Far Horizon

1. **Before writing any new code for `86cag93zb`'s next round: check what `verify_chop_gate.sh` already
   captures.** If it already samples mid-swing, (a) is closed and the doc rule ("live Animator ⇒ windowed")
   should be corrected/removed rather than treated as still-true friction.
2. **Apply `Animator.cullingMode = AlwaysAnimate` to any rig captured via RT-readback as a preemptive,
   zero-cost mitigation**, regardless of whether the `verify_chop_gate.sh` check resolves the ambiguity —
   it removes a documented (if unconfirmed-in-this-exact-API-interaction) failure mode for free.
3. **Spike `PanelSettings.targetTexture` as its own small ticket before conceding the UI-Toolkit-overlay
   gates (settings/dev-console/inventory-drag/water-HUD) as permanently windowed.** This is a documented,
   version-matched API nobody has tried yet in this repo — the existing "hard boundary" doc language was
   earned from a different lever (camera-RT redirection) and should not be read as having ruled this one
   out.
4. **Run the 4-question checklist against each of the 8 real gates** (requires repo access this session
   didn't have) to get the actual convertible count — do not carry this note's bound forward as if it were
   the answer.
5. **Update `unity-conventions.md`'s "hard boundary" language** once either the `verify_chop_gate.sh` check
   or the `targetTexture` spike produces a real result — the current wording states a stronger constraint
   than the evidence here supports, and per the project's own repeated lesson (§Process notes, the grep-
   staleness family), a written-down constraint that outlives its evidence is exactly the failure mode that
   over-constrains real work.
6. **In-house-tooling posture:** this is CI/build-pipeline engineering, not an asset-pipeline or licensing
   question — no interaction with the procedural/Blender/Hyper3D routes or cost/licensing posture.
