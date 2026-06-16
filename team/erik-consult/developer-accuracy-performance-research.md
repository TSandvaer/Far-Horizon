# Developer Accuracy and Performance — Research Note

## Question

Sponsor-directed (ticket `86ca92vrk`): what patterns, techniques, and tool-level changes would most improve **developer accuracy** (root-cause diagnosis, test coverage of in-flight defects, pre-soak regression detection) and **iteration performance** (edit-play-verify turnaround, build speed, runtime FPS budget) on the Far Horizon team? Research grounded in our actual failure modes, not generic Unity advice.

## Bottom line

Four specific investments cover virtually every documented failure class: **(1)** formalize the team's existing isolation-probe pattern into a discipline (Diagnose-Before-Fix standard) — this is already the winning method but currently ad hoc and only applied after a failed guess; **(2)** add PlayMode locomotion-sampling tests using `[UnityTest]` IEnumerator frame loops to catch the "tests green but Sponsor sees elevation DURING WALK" gap; **(3)** enable Configurable Enter Play Mode (disable domain reload; optionally disable scene reload) to cut the editor play-cycle cost — meaningful on a project this size; **(4)** instrument the URP SRP Batcher using the Frame Debugger before adding any new transparent pass (water foam, clouds, future VFX) to protect the desktop FPS budget. All four are cheap to adopt and directly map to confirmed pain on this team.

---

## Evidence

### A — Diagnosis Accuracy: Isolate-Before-Guess (the "diagnose via trace" discipline)

**Our failure history (source: `unity-conventions.md` §Editor-vs-runtime, multiple entries):**
Walk-elevation diagnosed 4+ times (NavMesh-slab grounding; blob-shadow-float; foot-bone-offset; NavMesh slab vs visible terrain — each fix overturned the prior root cause). Water-elevation: overturned 3× (water-Y → seaward-composition → terrain-shader → vista-islands-draping). Mountains: floating-translucent overturned once (winding/surface hypothesis refuted by trace; real cause = double-fade). All converged only after an isolation probe forced the real system to surface.

**Evidence A1 — Unity Frame Debugger as isolation instrument.**
- Source: Unity Manual, "Rendering Profiler module reference" — [https://docs.unity3d.com/Manual/ProfilerRendering.html](https://docs.unity3d.com/Manual/ProfilerRendering.html) — **Strong** (official docs). States the Frame Debugger "is the best tool for investigating which draw call batches your render thread is issuing to the GPU" and exposes per-draw-call state (shader, material, batching reason). Used by Drew to prove water was 0 visible pixels via magenta-diff (confirmed in `unity-conventions.md` §"Is what I'm seeing" magenta cross-build trace).
- Source: Unity Manual, "Debugging and diagnostics" — [https://docs.unity3d.com/6000.0/Documentation/Manual/debugging-and-diagnostics.html](https://docs.unity3d.com/6000.0/Documentation/Manual/debugging-and-diagnostics.html) — **Strong** (official). Covers runtime-mode inspection, stack-trace logging, and the Debug class for asserting observable state mid-run.
- Source: Unity "Speed up and improve QA testing with Unity's Debug class" — [https://unity.com/how-to/improve-qa-testing-debugging-debug-class](https://unity.com/how-to/improve-qa-testing-debugging-debug-class) — **Moderate** (official how-to). Covers conditional `Debug.Log` patterns and runtime inspector-style dumps.

**What the evidence says:** The Frame Debugger, a command-line `-hideX`/`-noFog`/`-seaWaterOnly` custom launch flag (described in `unity-conventions.md` as the team's own magenta-diff protocol), and runtime `Debug.Log` dumps (CAMDIAG, ROCKDIAG, TINTDIAG) are the fastest path to refuting a false root-cause hypothesis. Every confirmed success on this team was an **isolation probe first, hypothesis second** workflow. The current failure pattern is the reverse: guess a fix, rebuild, soak, get contradicted. The isolation probe collapses 2-4 build-and-soak rounds into 1.

**Evidence A2 — "Asset is fine, VIEW is the problem" class.**
The team has named this class at least 6 times: magenta-diff (water invisible = culled, not occluded), seaward-composition (water fine, camera doesn't reach), vista-draping (terrain fine, mountains buried it), skybox-wash (`positionCS.xyww` draws over geometry), blob-shadow vs body position, NavMesh slab vs visual terrain. The pattern is consistent: **the symptom names the wrong system**. The isolation discipline — verify the symptom system is even visible before tuning it — is the structural fix.

**Evidence A3 — Team's own rule (strong self-evidence):**
- Source: `unity-conventions.md` §"'Can't see X in the build' is often a CAMERA-reach / occlusion bug, NOT an asset bug", §"Is what I'm seeing actually the asset? — the magenta cross-build trace", §"A custom URP skybox shader drawing OVER geometry..." — **Strong** (empirically validated multiple times in shipped builds on this project). These entries enumerate the isolation probe pattern the team uses successfully when it remembers to use it.

**Application to Far Horizon:** The team should codify Diagnose-Before-Fix as a mandatory step: before any fix commit, the developer must cite ONE isolation result that confirms the root cause. This is already done post-hoc on successes; making it pre-fix would eliminate the build-soak cycle for refuted hypotheses. Cost: zero new tooling. Leverage: documented 3-4 avoidable soak rounds on water/elevation alone.

---

### B — Test Accuracy: PlayMode Locomotion-Sampling Tests

**Our failure history:** Tess QA'd "grounded" as PASS. Sponsor sees elevation DURING WALK. Tests assert standing/after-walk; miss the mid-locomotion case. Same class: blob-shadow stranded above feet while body is grounded reads as floating; finger-open clip-hand while axe equipped reads as mangled. All are **in-motion defects that standing-frame tests are structurally blind to**.

**Evidence B1 — `[UnityTest]` frame-sampling coroutine pattern.**
- Source: Unity Test Framework docs, "UnityTest attribute" — [https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/reference-attribute-unitytest.html](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/reference-attribute-unitytest.html) — **Strong** (official). "UnityTest attribute allows coroutines to run tests over multiple frames." PlayMode `[UnityTest]` returns `IEnumerator`; inside the loop `yield return null` advances one frame. You can assert any transform, position, or renderer state ON EVERY FRAME during a walk cycle.
- Source: Unity Scripting API, `WaitUntil` — [https://docs.unity3d.com/ScriptReference/WaitUntil.html](https://docs.unity3d.com/ScriptReference/WaitUntil.html) — **Strong** (official). "Suspends coroutine execution until the supplied delegate evaluates to true. The supplied delegate is executed each frame after Update and before LateUpdate." The delegate can assert e.g. `agent.isOnNavMesh && feet.y <= visibleGround.y + epsilon` on every frame of a walk sequence.
- Source: Practical PlayMode Testing in Unity3D — [https://medium.com/xrpractices/practical-playmode-testing-in-unity3d-5ea455bf28b0](https://medium.com/xrpractices/practical-playmode-testing-in-unity3d-5ea455bf28b0) — **Moderate** (practitioner). Confirms the pattern: `yield return null` in a loop samples each Update; `WaitForSeconds(animDuration)` + assertion loop covers in-progress animation state.

**Evidence B2 — What to assert in the locomotion test.**
The walk-elevation failure is root-caused in `unity-conventions.md`: `NavMeshAgent` plants root at Y≈0.084 (NavMesh slab), but visual terrain dips to Y≈0.020; fix = `ApplyGroundSnap` raycast to renderer-ENABLED surface. The correct PlayMode test therefore: (a) trigger walk to a waypoint; (b) while `agent.velocity.magnitude > 0.1`, yield each frame; (c) assert `feet.y <= visibleGround.y + tolerance` on EVERY frame. The standing-frame test (`after_walk`) cannot catch the mid-walk snap failure because the agent may incidentally be grounded at the start/end of a path while the visual terrain dips only mid-path.

The finger-open/prop-stability classes are analogous: assert the prop's world position remains within N units of the hand bone across all 60 Walk frames, not just at idle pose.

**Evidence B3 — Unity's headless PlayMode time trap already documented.**
`unity-conventions.md`: "`Time.deltaTime ≈ 0` per frame in headless runs — never assert on per-frame deltas; sample over a real `Time.time` window instead." The locomotion-sampling test must use `Time.time` accumulation or explicit `WaitForSeconds(walkDuration)`, not `yield return new WaitForFixedUpdate()` in headless CI (where fixed-update timing is unreliable).

**Application to Far Horizon:** Three tests Devon should author once PR #47 stabilizes:
1. `CastawayWalk_FeetRemainGroundedDuringWholeCycle` — coroutine walking 10 frames of a navigation cycle, asserting `feet.y <= terrain.y + 0.05` each frame.
2. `CastawayWalk_HeldAxeStaysWithinGripEnvelope` — assert `axe.worldPosition` stays within 0.3u of hand bone across the walk cycle.
3. `CastawayWalk_FingersClosedWhenPropHeld` — assert curl-driver active and no finger bone deviates beyond expected curl angle.

---

### C — Iteration Performance: Configurable Enter Play Mode

**Evidence C1 — Domain Reload disable, official Unity 6 docs.**
- Source: Unity Manual, "Configuring how Unity enters Play mode" — [https://docs.unity3d.com/6000.4/Documentation/Manual/configurable-enter-play-mode.html](https://docs.unity3d.com/6000.4/Documentation/Manual/configurable-enter-play-mode.html) — **Strong** (official, Unity 6000.4, our exact version). Offers three settings under Edit → Project Settings → Editor → "When entering Play Mode": (a) Reload Scene Only (domain kept, scene reloads), (b) Reload Domain Only (scene kept), (c) Do not reload Domain or Scene. The manual states "these reloading actions take time to perform, and the amount of time increases as your scripts and scenes become more complex. When you frequently make and preview changes, the cumulative time spent waiting to enter Play mode can significantly slow down your development process."
- Source: Unity Manual, "Enter Play mode with domain reload disabled" — [https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html](https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html) — **Strong** (official). Specifies the developer responsibility: static fields and static event handlers persist between runs; use `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` to reset static state on each play entry. Without this, static singletons and event handlers accumulate across play sessions → subtle test/editor-only bugs.
- Source: Unity discussions + CodeArchPedia summary — [https://openillumi.com/en/en-unity-speedup-domain-reloading-disable/](https://openillumi.com/en/en-unity-speedup-domain-reloading-disable/) — **Moderate** (community; consistent with official). Reports enter-play-mode dropping from "several seconds" to "sub-second" on medium-complexity projects when both reloads are disabled.

**Evidence C2 — Incremental build pipeline.**
- Source: Unity Manual, "Incremental build pipeline" — [https://docs.unity3d.com/6000.1/Documentation/Manual/incremental-build-pipeline.html](https://docs.unity3d.com/6000.1/Documentation/Manual/incremental-build-pipeline.html) — **Strong** (official Unity 6.1). "The incremental build pipeline skips build actions that don't have any changes for their inputs, and reuses build results from previous builds (stored in the project's Library/Bee directory)." Covers: asset serialization, code compilation, data compression, signing. Script-only changes (no asset changes) will only recompile + relink, skipping asset baking. Note: cache is PER-MACHINE LOCAL — CI on the self-hosted runner reuses the same `Library/Bee` if the worktree hasn't changed, but the `git checkout -- Assets/ ProjectSettings/` restore wipes Library/Bee-relevant inputs, so cache effectiveness on bootstrapped builds depends on what changed. The team's `serve_soak.sh` chain (bootstrap → BuildWindows) always fully rebuilds, which is correct for correctness but kills the incremental cache. A fast-path "scripts-only changed" build path (skipping `BootstrapProject.Run`) would reuse the cache — viable for logic-only PRs where no scene/material changes are needed.

**Evidence C3 — Asset import cache (Unity Accelerator / local cache).**
- Source: Unity Manual, Build AssetBundles in parallel / multi-process import — [https://docs.unity3d.com/6000.1/Documentation/Manual/Build-MultiProcess.html](https://docs.unity3d.com/6000.1/Documentation/Manual/Build-MultiProcess.html) — **Strong** (official). Parallel asset import + an Accelerator cache server can share import artifacts across machines. Not immediately applicable (single self-hosted runner), but if a second dev machine or cloud CI adds, this is the right lever.

**Application to Far Horizon:**
- **Now:** Enable Configurable Enter Play Mode on every worktree. Set to "Do not reload Domain or Scene" for the fastest iteration during logic work; switch back to "Reload Scene" when testing scene-presence or bootstrap-output contracts. Each persona must add `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` to reset any static fields in their new code.
- **Near-term:** Add a "scripts-only" fast build path alongside `serve_soak.sh` for PRs that change only `.cs` files — skip `BootstrapProject.Run`, call `FarHorizonBuilder.BuildWindows` directly, verify stamp separately. Saves the bootstrap regeneration cost (~30-90s) on logic-only changes. A `verify_build_stamp.py` guard keeps it honest (exits loud if scene content drifted).
- **Not recommended:** disabling the `serve_soak.sh` full rebuild for any soak or capture-gate build — correctness requires the bootstrap chain; this is only for the editor-play-cycle.

---

### D — Runtime Performance: URP SRP Batcher Discipline for Visual Additions

**Evidence D1 — SRP Batcher rules and what breaks them.**
- Source: TheGamedev.Guru, "Unity Draw Call Batching: The Ultimate Guide" — [https://thegamedev.guru/unity-performance/draw-call-optimization/](https://thegamedev.guru/unity-performance/draw-call-optimization/) — **Strong** (deep technical, reproducible rules, 2024 updated). Key rules:
  - SRP Batcher requires shaders to declare material properties in a `CBUFFER_START(UnityPerMaterial)` / `CBUFFER_END` block; properties outside this block break compatibility.
  - `MaterialPropertyBlock` usage on a renderer disables SRP Batcher for that renderer (confirmed: "a renderer that uses MaterialPropertyBlock is not SRP-Batcher-compatible").
  - SRP Batcher and GPU Instancing are **mutually exclusive** for a given renderer — enabling GPU Instancing on a material disables SRP batching for it.
  - The team's `LowPolyVertexColor.shader` needs to declare all `_Color`, `_RimColor`, `_RimPower`, `_AOStrength` etc. inside `CBUFFER_START(UnityPerMaterial)` to remain SRP-compatible.
- Source: Unity Manual, "Scriptable Render Pipeline Batcher in URP" — [https://docs.unity3d.com/Manual/SRPBatcher.html](https://docs.unity3d.com/Manual/SRPBatcher.html) — **Strong** (official). "The SRP Batcher reduces the CPU time Unity requires to prepare and dispatch draw calls for materials that use the same shader variant."
- Source: Unity "Managing GPU usage for PC and console games" — [https://unity.com/how-to/gpu-optimization](https://unity.com/how-to/gpu-optimization) — **Strong** (official how-to). Identifies overdraw from transparent objects as the primary GPU cost driver on PC. "Transparent rendering generates very high overdraw, because each transparent pixel is shaded multiple times."

**Evidence D2 — Transparent water and foam overdraw risk.**
- Source: Unity Discussions, "Low-Resolution Transparent Rendering for URP" — [https://discussions.unity.com/t/low-resolution-transparent-rendering-for-urp-vfx-optimization-pass/1718461](https://discussions.unity.com/t/low-resolution-transparent-rendering-for-urp-vfx-optimization-pass/1718461) — **Moderate** (community). Confirms transparent pass (foam, water) incurs full-screen fragment evaluation over the water geometry area at native resolution. For a large ocean plane (600u extend per Drew's PR #48), this is a significant fragment cost if water is in the Transparent queue. The team's current decision (Drew PR #48) to keep water on the Opaque queue with vertex-color foam deliberately avoids this — correct call for the desktop budget.
- Source: `procedural-shadergraph-quality-research.md` §C and §Rank 3 (depth-fade foam requires Transparent queue + `SampleSceneDepth`) — **Strong** (this team's own prior research). Identifies the tradeoff explicitly: depth-fade foam requires a Transparent water shader; URP Exp² fog may not composite correctly with a Transparent material at the waterline. Drew's opaque-foam-vertex-color choice sidesteps this. Future depth-fade foam is a deliberate later upgrade, not a missed win.

**Evidence D3 — Frame Debugger as the runtime profiling instrument.**
- Source: Unity Manual, "Rendering Profiler module reference" — [https://docs.unity3d.com/Manual/ProfilerRendering.html](https://docs.unity3d.com/Manual/ProfilerRendering.html) — **Strong** (official). Frame Debugger shows SRP batch count, shader variant, and which renderers fall outside the batch. Running it on the shipped exe (connected via Unity Profiler remote) or in-editor reveals any `MaterialPropertyBlock` misuse or unregistered shader that breaks batching.
- Source: "Profiling and debugging with Unity and native platform tools" — [https://unity.com/how-to/profiling-and-debugging-tools](https://unity.com/how-to/profiling-and-debugging-tools) — **Moderate** (official how-to). Covers the workflow: Profile → identify GPU-bound frames → Frame Debugger to isolate expensive passes.

**Application to Far Horizon:**
- Run the Frame Debugger on the current `Boot.unity` shipped build to establish a baseline draw-call count and confirm all terrain/canopy/rocks/clouds/character are inside SRP batcher batches.
- The custom `LowPolyVertexColor.shader` must keep all properties in `CBUFFER_START(UnityPerMaterial)` — currently unknown whether this is satisfied; any `[MaterialPropertyBlock]` usage in procedural code (e.g. per-instance vertex-color or `_Color` overrides) would break batching for every affected renderer.
- Rule for new visual passes: any new shader (water foam, future campfire, future particle-like effects) must be verified in the Frame Debugger before merge — not after the Sponsor's soak.
- The Exp² fog currently in use is a built-in URP feature (zero overdraw cost on opaque geometry; only additive cost on transparents) — confirmed safe at current usage.
- Shadow cost: the character + held axe are the primary shadow casters; their per-frame bone evaluation cost is in the CPU profile, not the GPU shadow pass (low-poly mesh, single directional shadow). Not currently a bottleneck — monitor if more characters or dynamic foliage add shadow casters.

---

## Application to Far Horizon

### Ranked recommendations — impact × effort

**Rank 1 — Diagnose-Before-Fix protocol (Effort: zero new tooling. Impact: eliminates 2-4 soak-overturns per major defect.)**
Formalize the existing isolation-probe discipline as a PR convention: before opening a fix PR, the author must describe in the PR body ONE isolation result confirming root cause (e.g. "magenta-diff confirmed the mesh draws 0 px → culled, not occluded"; "-groundTrace confirmed feet at Y=0.020"). This is already the winning pattern on every success; codifying it pre-fix eliminates the refuted-root-cause rebuild loop. Entry point: add one line to `team/orchestrator/dispatch-template.md` under the "evidence gate" section. No code changes.

**Rank 2 — PlayMode locomotion-sampling tests (Effort: 2-4h per test class, once per system. Impact: closes "tests green / Sponsor sees defect DURING WALK" gap permanently.)**
Use `[UnityTest]` + `IEnumerator` coroutine with `yield return null` frame loop. Three priority tests for PR #47 / #48 follow-on work:
- `CastawayWalk_FeetRemainGroundedDuringWholeCycle` — assert `feet.y <= visibleGroundY + 0.05` on every frame while `agent.velocity.magnitude > 0.1`.
- `CastawayWalk_HeldAxeStaysWithinGripEnvelope` — assert prop within 0.3u of hand bone across all Walk frames.
- `CastawayWalk_FingersClosedWhenPropHeld` — assert curl-driver active; no finger bone exceeds open-hand angle threshold.
Key constraint: use `Time.time` accumulation not per-frame `Time.deltaTime` in headless CI (the `Time.deltaTime ≈ 0` headless trap from `unity-conventions.md`).

**Rank 3 — Configurable Enter Play Mode (Effort: 30 min setup across worktrees. Impact: sub-second play-cycle entry for logic iteration.)**
Enable "Do not reload Domain or Scene" in every worktree's Project Settings. Add `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` to reset static state in `WarmthNeed`, `CastawayCharacter`, and any other class with static fields. Keep `serve_soak.sh` chain as the full-correctness path; disable-reload is for editor iteration only. Does NOT affect headless test runs (they ignore this setting) or CI builds.

**Rank 4 — Frame Debugger SRP Batcher audit before new visual passes (Effort: 1h baseline; 15 min per new PR. Impact: prevents FPS regression from MaterialPropertyBlock misuse or transparent-queue overdraw.)**
Author a checklist item in `team/TESTING_BAR.md` under the visual-UX gate: "any new shader or renderer must be verified in the Frame Debugger (or SRP Batcher stats panel) before the Self-Test Report is posted." The current Opaque-queue water decision is correct — document in `unity-conventions.md §Build stripping & shaders` that Transparent-queue water breaks URP fog composition and incurs full overdraw on large ocean extents; the depth-fade foam upgrade (from `procedural-shadergraph-quality-research.md`) is deferred to a deliberate post-stabilization PR.

**Rank 5 — Scripts-only fast build path (Effort: 1-2h. Impact: saves 30-90s bootstrap cost on logic-only PRs.)**
Add a `fast_build.sh` alongside `serve_soak.sh` that skips `BootstrapProject.Run` and calls `FarHorizonBuilder.BuildWindows` directly, outputting a stamp-mismatch warning (not a hard fail) when boot content is unchanged. Validated by a `verify_build_stamp.py` check that confirms the scene content fingerprint matches. Only safe when the PR touches no scene/prefab/material content. Gate: the Self-Test Report must note which build path was used.

### What NOT to pursue right now

- **Automated pixel-diff / visual regression against the shipped exe:** tools like Percy, Backstop, Playwright screenshot comparison are web-app-native; the Unity equivalent (Graphics Testing Framework / `UnityEngine.TestTools.Graphics`) is an experimental package requiring separate baseline management and is significantly more setup than the payoff warrants at this team size. The current capture-gate + Tess human-judge workflow is lighter and already catches the false-negative classes documented in `unity-conventions.md`. Revisit when the team scales to ≥3 devs running simultaneous scene PRs.
- **Unity Accelerator / distributed import cache:** single self-hosted runner, single-machine worktrees — the setup cost exceeds the gain until CI moves to cloud runners.
- **GPU Instancing for procedural terrain/scatter:** the SRP Batcher already handles the terrain, rocks, and scatter geometry more efficiently (different `_Color` per-instance is fine with per-material cbuffer). GPU instancing is only a win for meshes that share EXACT material property values — procedural scatter with per-instance color tinting does not qualify. Confirmed: SRP Batcher and GPU Instancing are mutually exclusive per renderer.

---

## Evidence-strength summary

| Claim | Strength |
|-------|----------|
| Isolate-before-guess eliminates multi-soak root-cause loops | Strong (4-6 project-confirmed instances in unity-conventions.md) |
| `[UnityTest]` + `yield return null` loop can assert mid-walk state | Strong (official Unity Test Framework docs) |
| Disable Domain Reload speeds enter-play-mode | Strong (Unity 6000.4 official manual) + Moderate (community confirmation) |
| `RuntimeInitializeOnLoadMethod` resets static state | Strong (official Unity 6 manual) |
| SRP Batcher breaks on `MaterialPropertyBlock` usage | Strong (official Unity docs + TheGamedev.Guru deep technical) |
| Transparent queue on large water plane incurs high overdraw | Moderate (community discussions, consistent with official overdraw guidance) |
| Incremental build pipeline reuses Library/Bee | Strong (official Unity 6.1 manual) |
| `Time.deltaTime ≈ 0` in headless runs | Strong (unity-conventions.md empirically validated) |
