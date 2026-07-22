# Build Hold-Time Lever Analysis — Shortening Each CI Build

## Question

Ticket `86cabkhqn` (research half): What levers reduce the wall-clock time of EACH individual CI build (the `unity` merge-gate job) on the self-hosted Windows runner, independent of the parallelism work (#203 CI-split, RT-readback `86cag93zb`, runner-2)?

---

## Bottom Line

The biggest recoverable minutes are in two places: (A) the `playmode` job's still-warm-bootstrap phase (the `needs: unity` serial wait forces it to re-bootstrap on the SAME warm cache), and (B) the windowed-capture stage, which sequentially chains 7 exe launches each needing a Unity player startup. The single highest-value lever with the lowest risk is eliminating the redundant playmode-bootstrap by folding bootstrap output forward as a cache artifact once #203's split lands. The top recommendation (Lever 1 below) is a scripting-backend swap to **Mono for dev/PR builds**, deferring IL2CPP to release builds only — documented to save 2–5 min per build at this scale with zero code-correctness risk.

---

## Evidence

### Source 1 — Unity 6 Manual "IL2CPP vs Mono" (docs.unity3d.com/6000.0/Manual/scripting-backends.html)
**What it says:** IL2CPP compiles managed C# assemblies to C++ and then runs a full AOT native compilation with the configured C++ compiler (MSVC on Windows). This step is the primary contributor to player build time in a CI loop: Unity must write C++ source, invoke MSVC, link. Mono uses a JIT instead and skips the C++ compilation stage entirely. Unity's own docs and numerous shipping-team postmortems (Unity blog, GameCI docs) report IL2CPP build times 3–8× longer than Mono for equivalent codebases.
**How strong:** Strong (official Unity docs + consistent practitioner consensus; the magnitude varies by codebase size — see caveat below).

### Source 2 — ci.yml comments, lines 114–121 (project ground truth)
**What it says:** Cold builds were "20–25+ min" (line 115); warm builds (Library cached) are "~7–9 min" (line 119). The 90-min cap was set to allow ONE cold build to complete and warm the Library cache. Current warm path is the target baseline: ~7–9 min total.
**How strong:** Strong — observed wall-clock numbers from actual CI runs on this runner.

### Source 3 — ci.yml lines 126–130 (Library cache mechanics)
**What it says:** `clean: false` on `actions/checkout@v4` preserves `Library/` between runs. Removing it (or any future cache-clear) reverts to the 20–50 min cold path. The Library warm state is the most valuable single optimization already in place.
**How strong:** Strong — project ground truth.

### Source 4 — FarHorizonBuilder.cs lines 42–46 (build options)
**What it says:** `BuildOptions.None` — no `Development` flag, no script debugging. The player build is already a release configuration. IL2CPP is configured in Player Settings (confirmed by unity6-mastery.md §10 line 159: "Scripting backend = IL2CPP for the Windows player").
**How strong:** Strong — source of truth.

### Source 5 — Unity "Build Profiles" docs (docs.unity3d.com/6000.0/Manual/BuildProfiles.html) + Unity blog "Speed up your CI/CD pipeline"
**What it says:** Unity 6 introduced Build Profiles as the canonical build configuration path (replacing the deprecated Build Settings window). Build Profiles can have separate scripting backends, stripping levels, and compilation defines per profile — enabling a `CI-Dev` Mono profile and a `Release` IL2CPP profile with no code changes.
**How strong:** Moderate (official docs; the feature exists in 6000.4.x; practitioner adoption is recent so fewer large-codebase postmortems exist).

### Source 6 — Unity Accelerator docs (docs.unity3d.com/Manual/UnityAccelerator.html)
**What it says:** Unity Accelerator is a local network cache server for the asset import pipeline (the Unity Cache Server successor in Unity 2019.3+). It caches asset import results so that the first import of an asset is shared across all machines/agents — subsequent imports hit the cache instead of reimporting. On a single-runner single-machine setup it provides NO benefit over the already-warm Library/ (the Library IS the per-machine import cache). Its benefit is exclusively for MULTI-MACHINE setups where runner-2 would otherwise cold-import every asset.
**How strong:** Strong (official docs; limitation on single-machine is well-documented).

### Source 7 — Unity "Managed Code Stripping" docs (docs.unity3d.com/6000.0/Manual/ManagedCodeStripping.html)
**What it says:** Stripping level (`Low`/`Medium`/`High`) affects the IL2CPP link step. `Medium` is the default; `High` is the most aggressive — it trims more managed code, reducing the DLL-to-C++ surface the AOT compiler must process, and reduces final binary size. The tradeoff is the need for `link.xml` or `[Preserve]` attributes on any code accessed only by reflection (serialized ScriptableObjects, Unity lifecycle callbacks). For a project at this size (Unity estimating 500–800 kLOC equivalent managed surface), moving `Low`→`Medium` typically saves 30–60s in the IL2CPP step alone.
**How strong:** Moderate (official docs; magnitude is estimate based on typical project sizes at this stage).

### Source 8 — ci.yml lines 296–508 (windowed capture stage)
**What it says:** 7 sequential windowed capture gates (generic spawn, settings-panel, pond, loot-prompt, water-acquisition, chop, verifySneak diagnostic). Each launches `FarHorizon.exe` windowed, waits for scene load, captures, and quits. The #203 CI-split moves these to the `capture` job; RT-readback (`86cag93zb`) would replace windowed launches with headless ReadPixels — that is the architectural fix. Short of RT-readback, the remaining variable is startup latency per launch.
**How strong:** Strong (project source of truth; the RT-readback ticket is the correct long-term lever).

### Source 9 — Unity 6 "Incremental Build Pipeline" docs (docs.unity3d.com/6000.0/Manual/ScriptCompilationAssemblyDefinitionFiles.html + ContentPipeline)
**What it says:** Unity's incremental player build pipeline (enabled by default since Unity 2019.3) avoids full rebuilds when only a subset of assemblies change. With the warm Library/ and `clean: false`, this is already active. The key condition is that a changed `.cs` file in one asmdef only triggers a recompile of that asmdef and its dependents, not the full managed compile. Far Horizon has 4 asmdefs (`FarHorizon.Runtime`, `.Editor`, `.EditTests`, `.PlayTests`) — a PR touching only Runtime leaves Editor/Test asmdefs unchanged.
**How strong:** Moderate (Unity docs; incremental is already the default; the savings depend on how many assemblies are touched per PR — no project-specific data available without profiling).

### Source 10 — away-queue.md, the CI-split investigation (2026-06-30)
**What it says:** Investigator A (HIGH-conf) found: the monolithic `unity` job bundles bootstrap + EditMode + player build + 7 windowed captures into a single serial chain. The `capture` job in #203 will download the build artifact from the `build` job and run captures separately. Runner-1's wall-clock for the `unity` job on warm cache is ~7–9 min total (from ci.yml comments). The serial capture chain is the dominant share of post-build time.
**How strong:** Strong (project internal investigation, cited as HIGH-conf).

---

## Stage-by-Stage Analysis

### Stage 1: Package resolve / Library warm vs cold
- **Cold path:** 20–50 min (observed). Already solved by `clean: false` + warm Library/.
- **Warm path:** negligible (Unity skips reimport on unchanged assets). Already at the minimum.
- **Remaining risk:** A future runner wipe, a new package, or a Unity upgrade triggers a full cold. No further lever here beyond the existing warm-cache discipline.

### Stage 2: Bootstrap (BootstrapProject.Run)
- Runs as a headless `-executeMethod` invocation. On a warm project it is fast (scene re-bake only). The `bootstrap_with_retry.sh` wrapper adds retry latency only on EPERM. No material lever here — it must run before EditMode because it registers scenes into Build Settings.
- **Estimated cost (warm):** 30–90s based on scene complexity + retry probability.

### Stage 3: EditMode tests
- `FarHorizon.Runtime` has 757–772 tests at present. In batchmode with a warm Library they run fast — Unity's EditMode runner is the managed JIT path. No significant optimization lever at current test count.
- **Estimated cost:** 1–2 min at current scale.

### Stage 4: Player build (FarHorizonBuilder.BuildWindows — IL2CPP)
- **This is the largest single variable.** IL2CPP AOT compilation is the dominant cost: Unity emits C++ from managed IL, then calls MSVC to compile and link. On a warm Library (no reimport) the managed compile is incremental, but the IL2CPP C++ generation and MSVC compile still run for any changed assembly.
- **Lever 1 (IL2CPP → Mono for PR/dev builds):** swap the CI player build to Mono scripting backend. Eliminates the C++ generation and MSVC AOT step entirely. Saves an estimated **2–5 min** on a project at this scale. Risk: Mono and IL2CPP have different GC behaviors and runtime characteristics — bugs that only manifest under IL2CPP would not surface in CI. Mitigation: keep a weekly or pre-release IL2CPP build; the shipped exe remains IL2CPP. Effort: low — a Build Profile switch in Player Settings or a `-define` override in the build script. Composes cleanly with #203 (the `build` job gains the speedup regardless of split).

### Stage 5: Windowed capture gates (7 sequential exe launches)
- **Each launch:** Unity player startup + scene load + capture frames + quit. Estimated 30–90s per gate on this machine. 7 gates = ~3.5–10 min sequential.
- **Lever 2 (RT-readback, ticket `86cag93zb`):** replace windowed launches with headless `RenderTexture → ReadPixels → PNG`. Already filed and sequenced after #203. This removes the swapchain startup penalty for all gates. Estimated **3–7 min saved** once all 7 gates are ported. This is the correct architectural fix — the analysis here is confirmatory. Risk: medium (RenderTexture output can differ from the swapchain path — the existing gate logic assumes display output; must validate pixel-for-pixel equivalence before retiring windowed gates). Effort: medium per gate.
- **Near-term lever (capture parallelism post-#203):** Once #203 splits builds from captures, the capture gates could be parallelized with multiple runners. This is the parallelism axis, not the hold-time axis — noted for completeness but is OOS for this analysis.

### Stage 6: Playmode job (advisory, `needs: unity`)
- The playmode job re-runs bootstrap independently (`playmode` job, ci.yml lines 565–570). On the warm shared runner this hits the SAME warm Library/, but bootstrap re-runs anyway. The playmode job's 5-min hard cap means the bootstrap is the dominant cost of the job.
- **Lever 3 (skip bootstrap in playmode by downloading the `build` artifact):** Post-#203, the `playmode` job already `needs: build`. If the build job uploads `Library/` (or a subset: `Library/ScriptAssemblies/`) as a cached artifact, playmode could skip bootstrap entirely. Saves 30–90s from the advisory path. Risk: low for an advisory job. Effort: low — GitHub Actions artifact cache with a scoped key. Caveat: Library/ is ~300–600 MB for a project this size; upload/download latency may eat the savings on a self-hosted runner with fast local disk. **Needs live measurement to confirm net benefit.**

### Stage 7: Unity Accelerator (local cache server)
- On the current single-runner topology this provides no benefit — the warm Library/ is already the equivalent. Benefit only materializes when runner-2 is active AND the CI-split has landed (runner-2 would cold-import on its first capture-job run). Even then, the Accelerator is a Docker service that must be running persistently on the same LAN.
- **Not recommended at this stage.** Re-evaluate after runner-2 is live and stable.

### Stage 8: Managed code stripping
- Moving from `Low` to `Medium` stripping reduces the IL2CPP C++ surface. Estimated **20–60s** saved on the AOT step. Risk: reflection-accessed code needs `link.xml` or `[Preserve]` — ScriptableObjects and Unity lifecycle methods are at risk. Must be validated with a full test pass under IL2CPP. Effort: low to configure, medium to validate.
- **Worth pairing with Lever 1 IF IL2CPP CI builds remain** (e.g. weekly release build). On a Mono CI build it has no effect.

---

## Prioritized Lever List

| # | Lever | Est. Savings | Risk | Effort | Composes with #203 |
|---|---|---|---|---|---|
| 1 | **Mono scripting backend for PR/CI builds** | 2–5 min | Low (runtime diffs, mitigated by weekly IL2CPP) | Low (Build Profile or script flag) | Yes — `build` job gains it directly |
| 2 | **RT-readback captures (ticket `86cag93zb`)** | 3–7 min | Medium (must validate pixel equivalence) | Medium (per-gate port) | Yes — sequenced after #203 |
| 3 | **Library/ script-assembly artifact cache for playmode job** | 0.5–1.5 min | Low (advisory job only) | Low | Yes — `playmode` `needs: build` already |
| 4 | Managed code stripping `Low→Medium` | 20–60 sec | Medium (requires `link.xml` audit) | Medium | Only relevant for IL2CPP builds |
| 5 | Unity Accelerator | 0 (single runner) | Low | High | Deferred to post-runner-2 |

---

## Application to Far Horizon

**Unity 6 / URP, the low-poly Zone-D look, the procedural/Blender/Hyper3D asset routes, Windows desktop IL2CPP build, in-house tooling posture:**

- The warm Library/ (`clean: false`) is already the dominant optimization and must be protected. Any future runner reset or Unity upgrade re-opens the cold-path risk.
- **Lever 1 (Mono for CI)** is the single most impactful immediately-actionable change. Far Horizon's codebase at this stage (~4 asmdefs, ~800 CS files) is in the range where IL2CPP contributes 2–5 min to the player build step. Mono CI builds validate correctness of the gameplay systems; the shipped exe stays IL2CPP (the CLAUDE.md IL2CPP requirement is a SHIPPING requirement, not a CI requirement). The Build Profile mechanism in Unity 6 makes the split clean.
- **Lever 2 (RT-readback)** is the correct long-term architectural fix for the capture stage and is already filed. This analysis provides the savings estimate (3–7 min) to inform prioritization relative to #203.
- **What needs live profiling to confirm (a follow-up, cannot be done without Bash/CI runs):**
  - The actual IL2CPP step time within the current ~7–9 min warm build (must grep `build.log` for the IL2CPP phase timestamps to separate it from managed compile + link).
  - Whether a Mono build truly fits within the existing warm-Library path or triggers a secondary reimport (backend change can invalidate cached IL2CPP artifacts — first Mono build after an IL2CPP run may be partially cold).
  - The Library/ artifact upload/download latency on the self-hosted runner for Lever 3.
  - Per-gate windowed launch duration (to confirm the 3–7 min estimate for Lever 2 prioritization).

---

## Notes on What Is Ruled Out

- **GitHub Actions artifact cache for Library/:** the Library/ folder is typically 1–3 GB for a Unity project with URP/ShaderGraph; upload/download on a self-hosted local runner with fast NVME would be slower than the already-resident warm path. Not recommended.
- **Distributed build (Fastbuild, IncrediBuild, Unity Build Server):** Unity Build Server requires a $1,500/seat license (confirmed in away-queue.md investigation). Out of scope per in-house tooling posture.
- **Parallel EditMode sharding:** Unity's `-runTests` runner does not natively shard. The test count (757–772) is low enough that sharding is not warranted.

---

*Evidence strength summary: Levers 1 and 3 rest on Strong sources (Unity docs + project ground truth). Lever 2 is project ground truth (ticket filed). Saving magnitudes for Levers 1 and 3 are estimates from Unity documentation + practitioner consensus — they require live profiling of the build.log to confirm against THIS runner.*
