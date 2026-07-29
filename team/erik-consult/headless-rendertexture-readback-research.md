# Headless RenderTexture-Readback Captures — Feasibility Research (ticket `86cag93zb` AC1 pre-work)

## Question

Ticket `86cag93zb` proposes refactoring the CI capture/verify gates from launching the shipped
exe **windowed** (`-screen-fullscreen 0`, a real Win32 swapchain/D3D present loop — the thing that
breaks when a 2nd runner comes online) to a **RenderTexture readback** pattern
(`Camera.targetTexture` → `Texture2D.ReadPixels`/`AsyncGPUReadback` → write PNG in C#) that runs
**headless** and is claimed to be **session-independent**, removing the 1-runner capture pin.
AC1 requires a spike proving this produces a valid, non-black, content-correct frame under
`-batchmode -nographics` before any refactor work starts. This note is that spike's pre-work: what
does Unity's own documentation, issue tracker, and community evidence say is actually achievable —
*before* a build slot is spent finding out empirically.

**Dependency check (verified via ClickUp MCP, not inferred):** `86cafz9tg` (the CI-split ticket)
has status **`complete`**, closed — confirmed by a direct `get_task_details` fetch during this
research, not assumed from its absence on the open-tickets list. The "sequence after 86cafz9tg"
precondition in `86cag93zb` is satisfied.

## Bottom line

**`-nographics` is documented to skip graphics-device initialization entirely — it precludes ANY
Camera/RenderTexture rendering, not just backbuffer capture — so `-batchmode -nographics` is the
wrong target combination for this ticket's goal.** The viable target is **`-batchmode` WITHOUT
`-nographics`** on a machine with a real, already-logged-in Windows session (Session 1) — this
does initialize a real GPU device and, per forum/community reports (not official docs), lets a
camera render to a RenderTexture without a visible/focused window. That gets you OFF the
swapchain-present/compositor-focus dependency that breaks 2-runner windowed capture today —
which is the actual, achievable prize. It does **NOT** get you a true Session-0 (Windows-service,
nobody-logged-in) headless capture: GPU/Direct3D access is blocked for Windows services by
Session-0 isolation, an OS-level restriction independent of any Unity command-line flag. So
"session-independent" should be scoped honestly in the ticket/AC language: *independent of the
windowed-swapchain/compositor-focus contention*, not *independent of an interactive Windows
login*. Camera.Render() as a manual/explicit call is documented as unsupported under URP and has
a separate Windows issue-tracker report of halting scene time under `-batchmode` — the correct
API surface is either the normal automatic per-frame render of an enabled camera with
`targetTexture` set, or URP's `SubmitRenderRequest`/`SingleCameraRequest` (2022.2+, present in
Unity 6) for genuinely out-of-loop single-shot renders.

## Evidence

### Q1 — Does `-nographics` preclude RenderTexture rendering entirely?

- **Unity Manual, "Desktop headless mode"** (official, Strong) —
  https://docs.unity3d.com/Manual/desktop-headless-mode.html — "Desktop headless mode allows you
  to run applications in batchmode on any desktop platform **without initializing the graphics
  device**," via `-batchmode` + `-nographics` together. No graphics device = nothing can render to
  anything, RenderTexture included.
- **Unity Manual, Player command-line arguments (6000.4)** (official, Strong) —
  https://docs.unity3d.com/6000.4/Documentation/Manual/PlayerCommandLineArguments.html —
  "`-nographics`: When you use this argument in batch mode, Unity doesn't initialize a graphics
  device." Confirms the same at the current project's Unity version's own doc branch.
- **HDRP + batchmode + nographics** (Unity Discussions, Moderate — a concrete, reproducible error
  message, not opinion) — https://discussions.unity.com/t/hdrp-batchmode-nographics/845751 — the
  built-player error is literally `"Platform StandaloneWindows64 with graphics API Null is not
  supported with HDRP."` This shows `-nographics` doesn't just "skip a window," it sets the
  **graphics API itself to `Null`** — a distinct, named `GraphicsDeviceType.Null` device that some
  pipelines outright refuse to run under. URP does not appear to hard-refuse (no equivalent error
  found for URP), which is consistent with the observed failure mode being **silent black frames**
  rather than a startup error under URP.
- **Forum report of black RT frames specifically under `-nographics`** (Moderate — a specific,
  reproducible community report, not a single opinion) — surfaced via a Unity Answers/Discussions
  thread found through search: a dev doing off-screen RT rendering with `-batchmode` got a correct
  image with no CLI args, and **all-black** once `-nographics` was added — matching the "no device"
  explanation exactly.
- **Verdict:** documented + corroborated. `-nographics` is not a "no window" flag, it is a
  "no GPU at all" flag. The only combination worth spiking is `-batchmode` alone.

### Q2 — `Camera.Render()` → RenderTexture → `Texture2D.ReadPixels` headless: supported? Which graphics API?

- **`-batchmode` without `-nographics` does initialize a real graphics device** (Moderate —
  consistent across two independent community sources, not an official doc statement) —
  https://partiallydisassembled.net/posts/unity-headless.html: "You can still run batch mode
  without the `-nographics` option if you need the graphics device… the unlocked framerate is not
  nearly as high" — and a parallel forum thread found via search states the same trade-off. Neither
  source is Unity-official, so grade Moderate, not Strong; the official manual pages are silent on
  what `-batchmode` alone does to graphics-device init, which is itself a gap the spike should
  close empirically (this is exactly what AC1 asks for).
- **`Camera.Render()` is explicitly unsupported for on-demand renders in URP** (Moderate-Strong —
  Unity staff response quoted in a Discussions thread) —
  https://discussions.unity.com/t/is-there-any-way-to-do-a-single-camera-render-with-urp/774008 —
  "Camera.Render() hook is listed as 'not supported' in URP." Unity staff point to two
  alternatives instead: `UniversalRenderPipeline.RenderSingleCamera()` (requires hooking
  `RenderPipelineManager.beginCameraRendering`) or `SubmitRenderRequest()` (2022.2+), with the
  caveat that `SubmitRenderRequest` explicitly throws if called **inside** the render loop
  ("prevent its usage in functions called by the render loop to avoid recursive rendering
  trouble" — Unity staff quote in-thread).
- **Windows-specific issue-tracker report: calling `Camera.Render()` in `-batchmode` halts
  scene/time** (Strong source class — official issue tracker — but Moderate confidence here
  because the full body/fix-status could not be fetched past a redirect in this session; the title
  alone is confirmed via search: "[Windows] Calling Camera.Render() in headless mode (-batchmode)
  causes the scene/time to halt," https://issuetracker.unity3d.com/issues/calling-camera-dot-render-in-headless-mode-batchmode-causes-the-scene-slash-time-to-halt).
  **Flag explicitly: I could not confirm the fix/won't-fix status or affected version range — treat
  as `Likely relevant` risk to test in the spike, not a confirmed current-version defect.**
- **The standard, lower-risk pattern is NOT to call `Camera.Render()` manually at all**: assign
  `camera.targetTexture`, leave the camera enabled, let Unity's normal per-frame SRP execution
  render into it automatically each frame (this is how most shipped screenshot/RT-capture code
  works and avoids both the URP-unsupported-hook problem and the issue-tracker halt report), then
  synchronize the readback with `WaitForEndOfFrame` or `RenderPipelineManager.endContextRendering`
  before calling `ReadPixels`/`AsyncGPUReadback` (Moderate — pattern assembled from the official
  URP Manual page on rendering to a Render Texture, https://docs.unity3d.com/6000.3/Documentation/Manual/urp/rendering-to-a-render-texture.html,
  and its own recommendation to use `WaitForEndOfFrame`/`endContextRendering` for synchronization).
- **Graphics API choice (D3D11/D3D12/Vulkan):** no Unity documentation was found stating one API
  behaves differently from another for RT-readback correctness specifically. `-force-d3d11`,
  `-force-d3d12`, `-force-vulkan` are documented, Windows-supported flags (Unity Manual,
  PlayerCommandLineArguments, Strong/official). **Hypothesis, not sourced:** D3D11 is the
  historically most-tested Unity Windows backend and the lowest-risk first target for the spike;
  D3D12 is what the *current* windowed capture already uses per the ticket body ("a real Win32
  swapchain / D3D12 present loop") — worth testing both since the project's default may already be
  D3D12-pinned via existing player settings.

### Q3 — Does URP change the answer vs built-in? Render Graph / Forward+ / swapchain requirement?

- **A camera rendering to a RenderTexture in URP is documented as needing a second, screen-
  presenting camera — but only for the on-screen-preview use case, not for RT population itself**
  (official Manual, Strong for what it says, Moderate for how it applies here) —
  https://docs.unity3d.com/6000.3/Documentation/Manual/urp/rendering-to-a-render-texture.html:
  "If you have a Camera that is rendering to a Render Texture, you must have a second Camera that
  then renders that Render Texture to the screen." Read literally this is about needing a preview
  path (e.g. CCTV-monitor effect), not a hard requirement for the RT to contain valid pixels —
  **but the doc doesn't explicitly disclaim the requirement for the no-preview-at-all case, so this
  is exactly the ambiguity AC1's spike should resolve rather than assume.**
  Unity 6 also ships a documented **headless-native alternative**:
  `SingleCameraRequest`/`SubmitRenderRequest`
  (https://docs.unity3d.com/6000.3/Documentation/Manual/urp/User-Render-Requests.html) —
  "To trigger a camera to render to a render texture **outside of the Universal Render Pipeline
  (URP) rendering loop**, use the `SingleCameraRequest` and `SubmitRenderRequest` APIs" — this is
  the API family that doesn't need a screen-presenting sibling camera at all, and is the
  Unity-recommended mechanism for exactly this "render on demand to a texture, no display" case.
  **If the simple `targetTexture`-only pattern fails to populate reliably in the spike, this is the
  documented fallback to try next, not a random guess.**
- **Render Graph / Forward+ specific impact:** no Unity documentation or issue-tracker/forum
  report was found stating Render Graph or the Forward+ rendering path introduces a swapchain
  dependency for RT-only rendering, or otherwise changes headless RT-readback behavior.
  **Hypothesis, not sourced:** Forward+ affects light-culling/tiling internals and Render Graph is
  an internal pass-scheduling abstraction; neither is documented as requiring a live backbuffer
  target to execute a graph, so no material difference vs built-in is *expected* — but this is
  inferred from absence of contrary evidence, not confirmed, and should be explicitly logged as
  "not yet disproven" in the spike's write-up rather than assumed safe.

### Q4 — Known failure modes

- **Black/garbage frames under `-nographics`:** documented above (Q1) — this is the headline
  failure mode and the reason the ticket's own AC1 wording ("`-batchmode -nographics`") targets the
  wrong flag combination; the spike should immediately re-scope to `-batchmode` alone.
- **UI Toolkit elements fail to render to RenderTexture in batchmode** (Moderate — Unity staff
  present in-thread, unresolved) —
  https://discussions.unity.com/t/running-ui-toolkit-with-unity-in-batch-mode-for-visual-test-in-the-ci/891977 —
  "Screencapture is not supported well in batchmode and the result of the image is not including
  the UI Document." A Unity staff member ("Antoine") pointed to the internal **Graphics Tests
  Framework**/Image Comparison utilities as the sanctioned path, without a definitive public fix.
  **Relevant only if any of Far Horizon's capture gates include UI Toolkit HUD elements
  (need-meter bars, build stamp) in the captured frame** — flag this explicitly to whoever runs the
  spike; Canvas-based (non-UI-Toolkit) UI reportedly renders fine per the same thread.
- **Frame-timing / warm-up:** no official Unity documentation states an exact frame count before an
  RT is guaranteed valid. The documented, load-bearing synchronization primitives are
  `WaitForEndOfFrame` and `RenderPipelineManager.endContextRendering`
  (both official Scripting API, Strong for existence, no official guidance on minimum frame count).
  Treat "how many frames to pump" as unknown-until-measured — this is squarely an AC1 spike output,
  not something to hardcode from a guess.
- **`AsyncGPUReadback` vs synchronous `ReadPixels`** (official Scripting API docs, Strong):
  `Texture2D.ReadPixels` — "waits for the GPU to complete all previous work first" (i.e.
  synchronous/blocking) — https://docs.unity3d.com/ScriptReference/Texture2D.ReadPixels.html.
  `AsyncGPUReadback` — "adds a few frames of latency" but reads "without any stall (GPU or CPU)" —
  https://docs.unity3d.com/ScriptReference/Rendering.AsyncGPUReadback.html — and requires
  `SystemInfo.supportsAsyncGPUReadback` to be true on the active device. **Under a `Null` graphics
  device this will certainly be false; under a real D3D11/D3D12/Vulkan device it should be true**
  but was not separately verified in official docs for the exact `-batchmode`-without-`-nographics`
  combination — another concrete spike checkpoint.
- **sRGB/linear color-space mismatch shifting perceptual-gate pixel values** (Moderate — assembled
  from official docs + general color-pipeline knowledge, not a single citable "gotcha" page):
  `RenderTexture.sRGB` (https://docs.unity3d.com/ScriptReference/RenderTexture-sRGB.html) documents
  that in Linear color space, render textures perform linear↔sRGB conversion on write/sample. The
  existing windowed capture reads the **presented backbuffer**, which has already gone through the
  display-referred sRGB conversion; a raw off-screen `RenderTexture` created without matching sRGB
  read/write settings can produce **systematically different pixel values** for the same rendered
  scene. This is a real, concrete risk to the perceptual-diff gates (`frame_check.py`) — the spike
  must diff RT-readback pixels against a known-good windowed-capture baseline of the *same frame*,
  not just eyeball "looks non-black."

### Q5 — Session-independence: does GPU RT work under a non-interactive Windows session?

- **Windows Session 0 isolation blocks GPU/Direct3D access for services** (Strong — Microsoft's own
  architecture, documented across multiple independent, non-Unity sources, consistent with 20 years
  of Windows service architecture; this is an OS constraint, not a Unity one) —
  Microsoft TechCommunity ("Application Compatibility - Session 0 Isolation",
  https://techcommunity.microsoft.com/blog/askperf/application-compatibility---session-0-isolation/372361/replies/3749378)
  and corroborating vendor docs (NVIDIA vGPU known-issues docs, Citrix HDX docs) all describe the
  same mechanism: Windows services run in Session 0, which since Vista/Server 2008 has **no access
  to the GPU** associated with a user desktop, specifically to stop services from touching the
  interactive user's graphics session. This means: **running the capture player as a genuine
  Windows *service* with nobody logged in will very likely not get real GPU acceleration no matter
  what Unity command-line flags are used** — it is a layer below Unity's own graphics-device
  init.
- **Our own runner's documented history is consistent with this**: the project's memory entry
  "Runner Unity license needs the interactive user" (`runner-unity-license-needs-interactive-user.md`)
  already records that Unity **licensing** breaks in service/non-interactive contexts. This research
  adds the parallel, distinct constraint that **GPU rendering** (not just licensing) is also
  Session-0-restricted — two independent reasons the runner needs to stay in an interactive session,
  not one.
- **What IS achievable and well-supported: an interactive (Session 1) but unattended/disconnected
  RDP session still renders** (Moderate — Microsoft Q&A + support-vendor corroboration, consistent
  pattern across sources) — a Microsoft Q&A thread on this exact topic states Direct3D fails
  specifically "when not connected to an RDP session" because "the virtual server does not detect a
  display," but **works** while RDP-connected (the session counts as a display) —
  https://learn.microsoft.com/en-us/answers/questions/3230721/direct3d-fails-only-when-not-connected-to-virtual
  — and the well-known `tscon`-disconnect trick (used broadly in render-farm/automation contexts,
  documented informally across multiple sources found in this research) exists precisely to give
  back the console session's display so rendering keeps working after disconnect. This matches the
  GPU-render-farm pattern of HDMI/DisplayPort "dummy plug" devices
  (https://www.gpu-mart.com/blog/when-we-need-an-hdmi-dummy-plug) that trick a GPU into believing a
  monitor is attached — several independent vendor/community sources agree the GPU can otherwise
  drop into a reduced-capability state with no display detected at all.
- **Verdict:** "session-independent" should be understood as **"independent of the specific
  swapchain-present + desktop-compositor-focus contention that breaks concurrent windowed
  captures today,"** not as **"runs with no Windows login at all."** The latter is blocked by an OS
  mechanism (Session 0 isolation) that no Unity command-line flag changes. The former (staying in
  an interactive, logged-in session per runner, each doing off-screen RT rendering with no visible
  focused window) is the realistic, evidence-supported target — and it is still a genuine win: it
  removes the *specific* contention this ticket names (GPU/desktop-compositor contention between
  two windowed present loops), which is what's pinning the capture job to one runner today.

### Q6 — What must be true for N-runner parallelism?

- **Separate machines/GPUs:** trivially fine — no shared resource, no citation needed beyond basic
  hardware independence.
- **Same machine, shared GPU, two Unity processes:** Windows' WDDM driver model time-slices a GPU
  across multiple D3D/Vulkan device contexts as a matter of normal OS design (this is how ordinary
  Windows multitasking with multiple GPU-accelerated apps works generally) — Moderate confidence
  this generalizes to two headless Unity RT-only renders correctly, but **no Unity-specific source
  was found confirming N-simultaneous-instance correctness or throughput**; the one directly
  relevant report found (ml-agents GitHub discussion,
  https://discussions.unity.com/t/... via search, Weak/anecdotal) states "only one Unity instance
  can render at a time, with others pausing when they lose focus" — but this is very likely
  conflated with a **separate, well-documented, and directly actionable Unity setting**:
  `Application.runInBackground` — official Scripting API, Strong —
  https://docs.unity3d.com/ScriptReference/Application-runInBackground.html: **"By default, this is
  set to false and the application pauses when it is in the background."** A capture player that
  loses OS focus (exactly what happens when a 2nd process/window is also active) will **pause
  entirely** unless `runInBackground` is explicitly set true. **This is a concrete, testable,
  low-cost first check for the spike** — it's plausible that some or all of the currently-observed
  "2nd runner breaks captures" symptom is this focus-pause behavior rather than (or in addition to)
  a hard GPU/driver contention limit, and RT-readback headless mode sidesteps window-focus entirely
  since there's no window to lose focus from.
- **Driver-level risk under real contention:** this project's own memory entry "GPU TDR BSODs +
  NVIDIA driver updated" documents that this specific runner has hit Timeout Detection and Recovery
  issues before — TDR is a well-known, generically-documented NVIDIA/Windows driver mechanism that
  kills/resets a GPU context whose kernel exceeds a timeout, and heavier concurrent GPU load
  (two simultaneous renders) statistically increases the chance any single one blows the timeout.
  **Likely, not proven for this specific case:** N-runner RT-readback parallelism on a *shared* GPU
  raises TDR risk versus today's already-serialized-to-1 pattern; this is a reason to prefer
  separate machines/GPUs per runner over stacking runners on one box, even after RT-readback
  removes the compositor-contention blocker.

## Application to Far Horizon

1. **Re-scope AC1's spike flags immediately.** Test `-batchmode` **without** `-nographics` as the
   headless target, not `-batchmode -nographics` as currently worded. The documented behavior of
   `-nographics` (skips graphics-device init entirely, `GraphicsDeviceType.Null`) makes any RT
   render impossible under it — this isn't a tuning problem, it's the wrong flag.
2. **Don't call `Camera.Render()` manually.** Use the passive pattern (enabled camera +
   `targetTexture`, let per-frame SRP rendering populate it, synchronize readback via
   `WaitForEndOfFrame`/`RenderPipelineManager.endContextRendering`). If that doesn't reliably
   populate the RT without a screen-presenting sibling camera, fall back to URP's
   `SubmitRenderRequest`/`SingleCameraRequest` (available in the project's Unity 6000.4) — both are
   Unity-documented, unlike a manual `Camera.Render()` call which is explicitly flagged unsupported
   under URP and separately reported (Windows issue tracker) to halt scene time under `-batchmode`.
3. **Validate pixel-value parity against the existing gate baseline, not just "non-black."** Diff
   the RT-readback frame against a windowed-capture frame of the identical scene/camera state
   before trusting the perceptual gates (`frame_check.py`) against it — the sRGB/linear conversion
   difference between a presented backbuffer and a raw off-screen RT is a real, documented risk
   category (not confirmed present in this codebase, but plausible and cheap to check).
4. **Rename/rescope the "session-independent" framing before it ships as a claim.** This spike can
   credibly remove the swapchain/compositor-focus contention that pins captures to one runner
   today — that's real and valuable. It cannot make captures run with no Windows login (Session 0
   service) — that's an OS-level Session-0-isolation restriction, unrelated to Unity flags. If AC4's
   eventual N-runner rollout assumes literal service-mode/no-login runners, that assumption needs a
   sponsor-visible correction; if it assumes N interactive, logged-in runners each doing off-screen
   RT capture, the evidence here supports it.
5. **Check `Application.runInBackground` as a cheap, testable first hypothesis** for part of the
   currently-observed 2-runner contention, independent of the RT-readback refactor — it's a one-line
   change with an official-docs-confirmed default (`false`, i.e. pauses on focus loss) that could be
   conflated with the "GPU contention" diagnosis.
6. **UI Toolkit note:** if any capture gate's frame includes UI Toolkit elements (verify against
   whichever HUD/build-stamp overlay technology Far Horizon uses — not confirmed in this
   external-sources-only research), flag the batchmode UI Toolkit rendering gap found in Q4 before
   assuming a like-for-like visual match.
7. **In-house-tooling posture:** this is a CI/build-pipeline engineering question, not an
   asset-pipeline or licensing-cost question — no interaction with the procedural/Blender/Hyper3D
   routes or the in-house-first posture. No cost/licensing implication either way.
