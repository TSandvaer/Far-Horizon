# Unity 6 Mastery — Comprehensive Reference for Far Horizon

## Question

What do Devon and Drew need to know about Unity 6 (6000.4.10f1) / URP to build Far Horizon correctly? Covering: what is new in Unity 6; project architecture; scripting patterns; CPU/GPU/memory/GC performance; URP and rendering; UI Toolkit; assets and Addressables; Input System; testing; build and platform.

## Bottom Line

Unity 6 introduces GPU-driven rendering (GPU Resident Drawer, GPU Occlusion Culling, Render Graph) that directly benefits Far Horizon's big-island, dense-jungle world. ScriptableObject-based data and event channels are the right architecture for the survival loop. The URP Advanced Creators e-book and the Console/PC Performance e-book are the two highest-priority external reads; both were 403-blocked at research time but are free from unity.com.

---

## 1. Unity 6 — What Is New (Features Relevant to Far Horizon)

### 1.1 GPU Resident Drawer (URP 17 / Unity 6)

**Finding:** Moves per-object render setup from CPU to GPU using the BatchRendererGroup API. Reported up to ~50% CPU frame-time reduction for scenes with high draw-call counts. Auto-applies GPU instancing to qualifying GameObjects.

**Prerequisites (all required):**
- Rendering path: **Forward+** or Deferred+ on the Universal Renderer.
- Compute-shader-capable GPU (Windows desktop = fine; excludes OpenGL ES).
- `Project Settings > Graphics > Shader Stripping > BatchRendererGroup Variants = Keep All`.
- SRP Batcher enabled in the URP Asset.
- GPU Resident Drawer = **Instanced Drawing** in the URP Asset.

**GameObject constraints that disqualify objects from instancing:** MaterialPropertyBlocks on the MeshRenderer; `sortingLayerID`/`sortingOrder` set; >128 materials per GO; Text Mesh; `OnWillRenderObject` / `OnBecameVisible` / `OnBecameInvisible` MonoBehaviour callbacks; Light Probes set to Proxy Volume / Anchor Override; **Realtime Enlighten GI enabled**.

**LOD interaction:** GPU Resident Drawer forces **distance-based LOD switching only** — cross-fade animated LOD transitions fall back.

**When it can hurt:** small GPU cost increase; if the game is GPU-bound (not CPU-bound on draw calls) total frame time may rise. Always profile before/after enabling.

**How it applies to Far Horizon:** A large island with many repeated low-poly props (trees, rocks, grass) is the textbook use case. Disable Static Batching (incompatible with GPU Resident Drawer / BRG). Use a fixed Lightmap Size and disable Mipmap Limits per docs.

**Citation strength:** Strong. Sources: `unity6-resources-hub.md` (docs.unity3d.com/6000.0/Manual/urp/gpu-resident-drawer.html; docs.unity3d.com/6000.4/Manual/urp/gpu-resident-drawer-performance.html — note 6000.4 path matches FH's stream); `official-best-practice-guides.md` (Manual/optimizing-draw-calls-choose-method.html); `unity-how-to.md` (unity.com/how-to/performance-optimization-high-end-graphics, GPU Resident Drawer entry).

---

### 1.2 GPU Occlusion Culling (URP 17 / Unity 6)

**Finding:** Moves occlusion culling from CPU to GPU. Objects approximated by bounding spheres and a downsampled depth buffer at multiple resolution levels.

**Prerequisites:** GPU Resident Drawer enabled first; Render Graph ON (Compatibility Mode OFF).

**Enable:** Universal Renderer → GPU Occlusion toggle.

**When it helps most:** many objects share a mesh; scene has significant occlusion; occluded objects are high-vertex with small screen-space radius.

**When it can hurt:** if the scene has little occlusion, GPU setup overhead can increase render time. Low-poly geometry is the weaker case (the gain is largest for high-vertex occluded objects). Thin foliage (grass blades, palm fronds) performs poorly (sphere bounds approximation).

**Version note:** one GPU-culling doc page self-identifies as "Unity 6.5"; feature exists in 6.x — verify toggle wording against installed 6000.4.10f1.

**How it applies to Far Horizon:** The dense jungle and mountains are a good occlusion candidate. A/B test on the built exe; do not assume a win on low-poly geometry.

**Citation strength:** Strong. Sources: `unity6-resources-hub.md` (docs.unity3d.com/6000.0/Manual/urp/gpu-culling.html); `official-best-practice-guides.md`; `unity-how-to.md`.

---

### 1.3 Render Graph (URP 17 / Unity 6)

**Finding:** URP's rendering backbone is now the render graph. It reuses GPU memory automatically, removes unused render passes, generates proper compute/graphics queue sync, and on TBDR mobile merges passes to keep textures in tile memory.

**Two-stage authoring model:** (1) recording stage — declare textures/RTs you will use; (2) execution stage — issue graphics commands against declared resources. The system owns resource lifetime; do NOT manually allocate/dispose RTs the old way.

**Compatibility Mode:** a "Compatibility Mode (Render Graph Disabled)" toggle exists in `Project Settings > Graphics > URP > Render Graph`. It eases migration from legacy `ScriptableRenderPass` code. **GPU Occlusion Culling requires Compatibility Mode to be OFF.** Compatibility Mode is NOT intended for shipping builds. Turn it off and convert any custom passes to the Render Graph API before shipping.

**How it applies to Far Horizon:** Any custom URP Renderer Features (Zone-D quality pass: gradient skybox, fog, outline) MUST be authored against the Render Graph two-stage model. The Render Graph Viewer tool (`Window > Analysis > Render Graph Viewer`) is the diagnostic for custom pass resource usage.

**Citation strength:** Strong. Sources: `unity6-resources-hub.md` (docs.unity3d.com/6000.0/Manual/urp/render-graph-introduction.html); `unity-how-to.md` (performance-optimization-high-end-graphics, Render Graph entry); `unity6-learning-resources-thread.md` (URP e-book chapter list).

---

### 1.4 Adaptive Probe Volumes (APV)

**Finding:** Volume-based global illumination. Unity 6 adds scenario blending, sky occlusion support, and disk streaming. Featured in the "Fantasy Kingdom" Unity 6 sample. The GPU Lightmapper is now production-ready in Unity 6 — dramatically faster bake times than the CPU lightmapper.

**How it applies to Far Horizon:** A strong GI fit for a big open island. Consistent bounced/ambient light across terrain without hand-placed probe grids. Sky occlusion helps jungle interiors read correctly under the gradient skybox. APV + GPU Lightmapper is the recommended baking stack. Measure APV cost on actual scene; community reports occasional regression vs realtime in edge cases.

**Citation strength:** Moderate-strong. Sources: `unity6-resources-hub.md` (URP 17 what's-new); `unity-how-to.md` (new-ways-of-applying-global-illumination-in-unity-6 blog).

---

### 1.5 Forward+ Rendering Path

**Finding:** Tile-based forward variant. Screen splits into tiles; per-tile light lists. No per-object light limit (Forward had a hard cap). **Required prerequisite for GPU Resident Drawer and GPU Occlusion Culling.**

**Settings ignored under Forward+:** "Additional Lights" and "Main Light" per-object settings; "Per Object Limit for Additional Lights"; Reflection Probe Blending.

**How it applies to Far Horizon:** Set the Universal Renderer to Forward+. Removes per-object light cap for campfires/torches. The loss of Reflection Probe Blending is acceptable for a stylized low-poly look.

**Citation strength:** Strong. Source: `unity6-resources-hub.md` (docs.unity3d.com/6000.0/Manual/urp/rendering/forward-rendering-paths.html).

---

### 1.6 Spatial-Temporal Post-Processing (STP) Upscaling

**Finding:** Renders at lower internal resolution, upscales to display resolution. Enable in URP Asset: `Quality > Upscaling Filter > Spatial Temporal Post-processing (STP)`.

**How it applies to Far Horizon:** A GPU-headroom lever if Far Horizon ever becomes GPU-bound at native resolution. Low priority for a low-poly game early in development. Caution: upscalers can soften hard low-poly edges — test before adopting.

**Citation strength:** Strong. Source: `unity6-resources-hub.md` (URP 17 what's-new).

---

### 1.7 Other Unity 6 Headlines Relevant to Far Horizon

| Feature | What it is | FH relevance |
|---|---|---|
| **Build Profiles** | Multi-config build targets; replaces Build Settings window | `FarHorizonBuilder.BuildWindows` should align to Build Profiles |
| **Split Graphics Jobs** | Reduces sync between main thread and native graphics-job thread | Desktop multi-core CPU win; enable + profile |
| **SkinnedMeshRenderer batching** | Batches compute-skinning + blendshape dispatches | Relevant once the rigged castaway + NPCs are in-scene |
| **IL2CPP C# source line numbers** | Option to display in player call stacks | Turn ON for the Windows player — critical for shipped-build debugging per the FH testing bar |
| **TextMeshPro emoji + OpenType** | Basic emoji, kerning | HUD/UI text quality |
| **Dynamic Shader Variant Loading** | Streams/evicts shader data chunks | Lower priority on desktop with ample RAM |
| **Render Graph Viewer** | Visualizes URP pass resource usage | Use when authoring/diagnosing custom passes |
| **Frame Debugger enhancements** | Stage/Scope/Dynamic keywords, batch introspection | Verify GPU Resident Drawer merged draw calls |
| **Profiler Highlights module** | Bottleneck visualization | First stop for "where is the problem?" |
| **Memory Profiler v1.1** | RenderTexture/AudioClip/Shader metadata | Accurate resident memory + graphics breakdown |

**Citation strength:** Strong for all. Source: `unity6-resources-hub.md` (Unity 6 What's New, docs.unity3d.com/6000.0/Documentation/Manual/WhatsNewUnity6.html).

---

## 2. Project Architecture and Organization

### 2.1 Folder Structure

**Recommended top-level `Assets/` layout (validated across sources):**
- `Art/` — sprites, textures, models, materials
- `Prefabs/` — reusable GameObjects
- `Scenes/` — Unity scenes
- `Scripts/` — subdivided into `Editor/`, `Runtime/`, `Test/`
- `ThirdParty/` — imported external packages
- `Documentation/` — readmes, diagrams

Far Horizon already follows the Editor/Runtime/Test split via asmdefs (`FarHorizon.Runtime` / `FarHorizon.Editor` / `FarHorizon.EditTests` / `FarHorizon.PlayTests`). Namespace folder `FarHorizon` under `Scripts/Runtime/` is the correct placement.

**Citation strength:** Moderate (version-agnostic; enduring practice). Source: `rivello-best-practices.md` (GitHub template README).

---

### 2.2 Assembly Definitions (asmdefs)

**Finding:** Split code into named assemblies with explicit dependencies. Prevents accidental editor-only code in runtime builds. Unity's Test Framework requires separate test asmdefs. Commit-triggered builds validate the asmdef graph.

**How it applies to Far Horizon:** `FarHorizon.Runtime` / `FarHorizon.Editor` / `FarHorizon.EditTests` / `FarHorizon.PlayTests` — already in place per CLAUDE.md. Each agent-authored feature should add to the correct asmdef rather than creating a new one.

**Citation strength:** Moderate (version-agnostic; enduring). Source: `rivello-best-practices.md`.

---

### 2.3 Scene Strategy

**Finding:** Splitting a game across scenes distributes load times and enables independent testing. The first scene in the Scenes-In-Build list (index 0) is the entry point.

**How it applies to Far Horizon:** `Boot.unity` must remain index 0 in the build list. The headless bootstrapper (`BootstrapProject.Run`) regenerates it; keep this as the canonical scene-generation point.

**Citation strength:** Strong (enduring Unity rule). Sources: `unity-manual-pdf.md` (Ch10, Build Settings); `purdue-intro-unity-pdf.md` (slide 5).

---

### 2.4 Version Control

**Smart Merge (UnityYAMLMerge):** Unity ships `UnityYAMLMerge.exe` for semantic merging of `.unity` and `.prefab` files (object-by-object, not raw text). Required git config snippet:
```
[merge]
  tool = unityyamlmerge
[mergetool "unityyamlmerge"]
  trustExitCode = false
  cmd = '<path>/UnityYAMLMerge.exe' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
```

**Force Text + Visible Meta Files:** Required for Smart Merge and meaningful diffs. Set in `Edit > Project Settings > Editor > Asset Serialization > Mode = Force Text` and `Version Control > Mode = Visible Meta Files`. Far Horizon's CLAUDE.md already relies on `.meta` files for empty dirs (the Visible Meta Files prerequisite).

**`.gitignore` additions beyond CLAUDE.md's current list:** Standard Unity ignore set also covers `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln` (regenerated by the editor).

**How it applies to Far Horizon:** Far Horizon regenerates `Boot.unity` headlessly (sidesteps scene merges). For any hand-edited prefabs/scenes touched by multiple personas in parallel worktrees, wire UnityYAMLMerge into git.

**Citation strength:** Strong. Source: `official-best-practice-guides.md` (Manual/SmartMerge.html).

---

### 2.5 Enter Play Mode Options

**Finding:** Skips domain reload and/or scene reload on entering Play mode — the single biggest editor iteration speedup. **Requirement:** code must not depend on static state being reset by domain reload; all statics that must reset need explicit `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` reset handlers.

**How it applies to Far Horizon:** Enable in `Project Settings > Editor > Enter Play Mode Options`. Audit static state in any shared system (BootstrapProject, event channels, object pools). Static state not explicitly reset silently corrupts cross-play-session state.

**Citation strength:** Moderate (version-agnostic; enduring). Source: `rivello-best-practices.md`.

---

## 3. Scripting Patterns and Architecture

### 3.1 ScriptableObject-Based Data (the primary data architecture)

**Finding:** ScriptableObjects store large quantities of shared data independent of script instances. Advantages over MonoBehaviours: no GameObject/Transform overhead, survive scene loads, designer-editable, serializable as project assets. Unity's own guidance recommends SO-based config for any tuning data that needs to persist or be shared.

**Patterns:**
- **Data containers:** item definitions, craft recipes, survival-need parameters, world-gen parameters → SO assets. Not classes with MonoBehaviour lifecycle.
- **Event channels (observer pattern):** an SO "event channel" asset sits between publishers and subscribers. Any system can publish or subscribe without hard references. Avoids singleton spaghetti. Example: "wood collected" SO event → HUD + crafting both listen without knowing each other. 
- **System-as-SO:** move a system from MonoBehaviour to SO where it must persist across scene transitions (inventory state, player progression).

**How it applies to Far Horizon:** All survival-loop tuning data (need decay rates, craft recipes, item stats) should be SO assets. Cross-system events (chop → inventory, inventory → HUD) should use SO event channels.

**Citation strength:** Strong. Sources: `unity-how-to.md` (architect-game-code-scriptable-objects, scriptableobjects-event-channels-game-code); `official-best-practice-guides.md` (scripting architecture index); `rivello-best-practices.md` (Best Practices 3).

---

### 3.2 Design Patterns for the Survival Loop (Unity 6 Sample Reference)

The Unity Technologies "Level Up Your Code with Design Patterns and SOLID" sample (`assetstore.unity.com`, target `6000.0.11f1`, URP-only, free) covers:
- **Object pooling** — spawned props, particles, resource drops
- **State machine** — player states (idle/moving/chopping), campfire states
- **Command** — click-to-move instruction queue, undo-able actions
- **Observer** — need/inventory events (pairs with SO event channels)
- **Factory** — prop/item instantiation
- **Strategy** — swappable behaviour components

**How it applies to Far Horizon:** Mine this sample for the M-U2 survival loop (need → craft axe → chop → campfire). The patterns are the same regardless of game content.

**Citation strength:** Strong (official Unity Technologies sample, URP-only, Unity 6 target). Source: `unity6-learning-resources-thread.md`.

---

### 3.3 MonoBehaviour Lifecycle Order (Enduring)

| Callback | When | Use for |
|---|---|---|
| `Awake` | Before Start; after all objects instantiated | Self-init; cache own components (`GetComponent`) |
| `Start` | After all Awakes complete | Cross-object references; register to events |
| `Update` | Once per rendered frame | Input; per-frame logic |
| `FixedUpdate` | Fixed physics timestep | Rigidbody forces; physics queries |
| `LateUpdate` | After all Updates | Camera follow (orbit camera MUST go here) |

**Critical rule:** always multiply per-frame values by `Time.deltaTime` in `Update` for frame-rate independence. Use `Time.fixedDeltaTime` semantics inside `FixedUpdate`.

**How it applies to Far Horizon:** Far Horizon's mouse-orbit camera follow MUST be in `LateUpdate`. Physics-based character movement goes in `FixedUpdate`. Input polling in `Update`.

**Citation strength:** Strong (enduring Unity rule). Sources: `unity-how-to.md` (E6); `purdue-intro-unity-pdf.md` (slide 24); `unity-manual-pdf.md` (Ch3).

---

### 3.4 Component-Access Best Practices

- **Cache `GetComponent<T>()` in `Awake` or `Start`** — never call per-frame.
- **Cache `Camera.main`** — it does a tag search; non-trivial cost if called per-frame.
- **Avoid `GameObject.Find` / `FindObjectOfType` in hot paths** — use serialized Inspector references or a registry instead.
- **Do not use `SendMessage`/`BroadcastMessage`** in gameplay code — reflection-based, slow; use direct method calls or SO event channels.
- **Use `[RequireComponent(typeof(X))]` attribute** when a script always needs a sibling component.

**Citation strength:** Strong (enduring Unity guidance). Sources: `unity-how-to.md` (E2); `purdue-intro-unity-pdf.md` (slides 25, 30).

---

### 3.5 Static State and Singletons

**Finding:** Global mutable static state is an anti-pattern. Static fields persist across domain reloads only when Enter Play Mode Options is OFF. Singletons "give the benefits of shared data but introduce dependencies."

**Preferred alternatives:** ScriptableObject data + SO event channels (see §3.1). If a singleton is truly needed, implement with DI-capable interfaces.

**Citation strength:** Moderate. Sources: `unity-how-to.md` (F2); `unity-manual-pdf.md` (Ch3 caveat).

---

### 3.6 C# Scripting Fundamentals (Baseline)

- **File name MUST match class name** or the component shows "Missing" in the Inspector — true in Unity 6.
- **`[SerializeField] private`** serializes non-public fields; Inspector value overrides the code default at runtime (the code initializer is not the runtime value when a scene/prefab has a saved value).
- **`public` fields** appear in the Inspector without an attribute but break encapsulation — prefer `[SerializeField] private`.
- **Quaternions:** use `Quaternion.Slerp`, `LookRotation`, `Euler`, `identity`. Never lerp Euler angles across the 360/0 wrap — use Slerp on Quaternions instead.
- **`Physics.Raycast(Camera.ScreenPointToRay(Input.mousePosition), out hit, float.MaxValue, groundMask)`** — the canonical click-to-move ground detection. Use a LayerMask (ground layer only).
- **Trigger vs Collider callbacks:** `OnTriggerEnter` fires when a trigger volume is crossed; `OnCollisionEnter` fires on solid collision. At least one object in a trigger/collision pair must have a Rigidbody.

**Citation strength:** Strong (enduring). Sources: `purdue-intro-unity-pdf.md` (slides 18, 28, 32, 34); `unity-manual-pdf.md` (Ch3, Ch4).

---

## 4. Performance: CPU / GPU / Memory / GC

### 4.1 The Profiling-First Rule

**Profile before optimizing.** Determine whether the bottleneck is CPU-bound or GPU-bound — strategies differ. **Always profile on a development build of the target exe, not the editor.** Editor profiler numbers diverge from shipped-build numbers (the project already knows editor-vs-runtime divergence is a proven failure class — spike iter6 "legs-up" incident; shipped-build capture gate exists for this reason).

**Profiling tools (Unity 6):**
- **Unity Profiler** — CPU/GPU/memory modules; Allocation Call Stacks; Profiler Highlights module (Unity 6: first-stop bottleneck viz).
- **Memory Profiler v1.1** (Unity 6) — accurate resident memory; graphics-memory breakdown.
- **Profile Analyzer** — aggregate/compare captures across frames; Compare view for before/after validation.
- **Frame Debugger** — per-draw-call inspection; verify GPU Resident Drawer merged draw calls.
- **Project Auditor** — static analysis package; CI candidate.

**Profiling method:** top-down. Start with CPU/GPU/GC category split; drill to the dominant cost. Add `ProfilerMarker`s around key systems (world-gen, survival tick, crafting). Use Allocation Call Stacks for GC allocation attribution before reaching for Deep Profiling.

**Citation strength:** Strong. Sources: `unity-how-to.md` (G1–G5); `official-best-practice-guides.md` (§2, analysis.html).

---

### 4.2 Draw-Call Batching Priority Order (URP)

| Priority | Method | Notes |
|---|---|---|
| 1 | **SRP Batcher** | Default in URP; reduce shader count, not material count |
| 2 | **GPU Resident Drawer** | Unity 6 headline; Forward+ required; most powerful for repeated props |
| 3 | **GPU Instancing (material)** | Enable instancing checkbox on individual materials — **DISABLE when using GPU Resident Drawer** (redundant, adds shader variants) |
| — | **Static Batching** | **Incompatible with GPU Resident Drawer/BRG** — do NOT use under URP when GPU Resident Drawer is on |
| — | **Dynamic Batching** | **Deprecated** — do not use |

**SRP Batcher batches by shader-variant compatibility, not by shared material.** Use many material instances of a FEW shaders; don't proliferate shader types.

**Static Batching limit:** up to 64,000 vertices per buffer; creates multiple buffers. Marked for exclusion under GPU Resident Drawer.

**Citation strength:** Strong. Sources: `official-best-practice-guides.md` (§4, Manual/optimizing-draw-calls-choose-method.html, DrawCallBatching.html); `unity-how-to.md` (C1, C2).

---

### 4.3 GC / Managed Memory

**Unity GC type:** Boehm-Demers-Weiser — stop-the-world pause halts the main thread until collection completes (can be hundreds of milliseconds).

**Incremental GC (Unity 6):** default ON. Spreads collection across frames in time-slices. Enable: `Project Settings > Player > Configuration > Use incremental GC`. Runtime control: `GarbageCollector.GCMode`. **Trade-off:** write barriers add overhead; very allocation-heavy code can force fallback to a full non-incremental collection, defeating the benefit.

**Goal:** Drive per-frame managed allocations toward zero in hot paths (Update / FixedUpdate / per-frame gameplay).

**Allocation call stack tracking:** Profiler CPU Usage module > GC.Alloc column + enable Call Stacks mode.

**Common GC culprits (enumerate in code review):**
- Per-frame `string` concatenation or formatting
- Boxing value types (int/float/struct stored as `object`)
- LINQ in hot paths (`Update`)
- Arrays from APIs: `GetComponents<T>()`, `Physics.RaycastAll()`, `Mesh.vertices`
- Closures/lambda allocations captured per-frame
- `new List<>()` / `new []` in Update — instead make a field, `Clear()` each frame

**Object pooling:** Use `UnityEngine.Pool` (built-in `ObjectPool<T>`) for any frequently spawned/despawned object. Do NOT `Instantiate`+`Destroy` per frame.

**Avoid runtime JSON/XML parsing** for config — load config via ScriptableObjects or binary formats (MessagePack/Protobuf) to avoid string allocation.

**Citation strength:** Strong. Sources: `official-best-practice-guides.md` (§3, performance-incremental-garbage-collection.html, performance-track-garbage-collection.html); `unity-how-to.md` (E1–E4).

---

### 4.4 GPU-Side Performance

**Shadows:**
- Disable shadow casting per-MeshRenderer for props/foliage that don't need it.
- Each **shadowed point light = 6 shadow-map passes** — never use for campfire glow; use unshadowed point or baked/emissive instead.
- Shadow distance, cascades (Cascaded Shadow Maps), and resolution are URP Quality / URP Asset settings.

**Overdraw:**
- Use the Scene View Overdraw draw mode to find hotspots.
- Transparent foliage and water are the worst overdraw contributors.
- Bake CPU-side occlusion culling for static world geometry.

**Fill rate:**
- Cheaper fragment shaders for distant/bulk props (URP Simple Lit or Unlit).
- Dynamic Resolution as a fallback for weaker GPUs.

**Texture:**
- Disable **Read/Write** on all static textures (doubles memory).
- Enable mip maps for distance-varying 3D textures; disable for constant-size UI/sprites.
- Max Size: use the smallest setting that is visually acceptable.
- **Power-of-two dimensions** for GPU compression to work.
- **Desktop compression = BC7/BC1 (DXT)**, not ASTC (mobile/XR only).

**Vertex processing:**
- Faceted/hard-normal low-poly meshes split vertices at hard edges — a faceted mesh is more vertices than its triangle count implies. Keep meshes genuinely low-poly.
- "Optimize Mesh Data" (`Project Settings > Player`) strips vertex attributes not required by the applied shader. **Caution:** verify normals are NOT stripped — they are load-bearing for the smooth-faceted look.
- LOD groups for hero trees/rocks/landmarks; mind that GPU Resident Drawer forces distance-based LOD only.

**Async compute:** GPU lever for compute-heavy passes (water sim, particles). Out of scope for the low-poly MVP; note for later.

**Citation strength:** Strong. Sources: `unity-how-to.md` (B6, B7, B8, C3, D1, D2); `official-best-practice-guides.md` (§5, OptimizingGraphicsPerformance.html).

---

### 4.5 CPU-Side Performance

**Lighting (baked vs realtime):** Static world → bake everything with the GPU Lightmapper. Realtime lights/shadows: single directional sun + minimal additional lights. APV for GI quality pass.

**Culling:** Camera far-clip-plane + per-layer cull distances for depth-of-field fog. CPU occlusion culling baked for static world. GPU Occlusion Culling layered on top (see §1.2).

**LOD and density management:** LOD groups cut GPU work on distant objects and reduce draw calls feeding the GPU Resident Drawer.

**SkinnedMeshRenderer:** Unity 6 batches compute-skinning dispatches — performance win once the rigged castaway + NPC pipeline is in play.

**C# Job System + Burst:** Multithreaded hot loops compiled to optimized native code. Out of scope for the thin survival MVP. Candidate for procedural island generation if generation is slow.

**Citation strength:** Strong. Sources: `unity-how-to.md` (E7, B5, G1–G5); `unity6-resources-hub.md` (Performance section).

---

## 5. URP and Rendering

### 5.1 URP Setup Checklist for Far Horizon

1. **Universal Renderer → Rendering Path = Forward+** (prerequisite for GPU Resident Drawer, GPU Occlusion Culling, and removes per-object light limit).
2. **URP Asset → Enable SRP Batcher** (default ON; confirm not disabled).
3. **URP Asset → GPU Resident Drawer = Instanced Drawing**.
4. **Project Settings > Graphics > Shader Stripping > BatchRendererGroup Variants = Keep All**.
5. **Disable Static Batching** in Player settings (incompatible with GPU Resident Drawer).
6. **Render Graph Compatibility Mode = OFF** (required for GPU Occlusion Culling; correct for all shipping builds).
7. **GPU Occlusion = ON** on the active Universal Renderer (A/B test — measure before committing).
8. **Disable Opaque Texture and additional-light shadows** if not used by any shader.
9. **HDR = ON** for the Zone-D bloom quality (bloom quality degrades without HDR).
10. **Confirm incremental GC is ON** (`Player Settings > Configuration > Use incremental GC`).
11. **IL2CPP source line numbers ON** for the Windows player (aids shipped-build crash diagnosis).

**Citation strength:** Strong (composite from multiple sources). Sources: `unity6-resources-hub.md`; `official-best-practice-guides.md`; `unity-how-to.md`.

---

### 5.2 URP Volume / Post-Processing (Zone-D Look)

The URP Advanced Creators e-book (free, unity.com/resources/introduction-to-urp-advanced-creators-unity-6) is the canonical reference for the Volume + Local Volume framework. Its chapter list (confirmed from the Unity learning resources thread) covers exactly the systems behind the Zone-D look: bloom/color grading, fog, Local Volume controls, Renderer Features, Render Graph authoring.

**Known Volume components in URP (standard):**
- Bloom — zone D "soft warm glow"
- Color Grading / Tonemapping
- Ambient Occlusion
- Depth of Field / Vignette
- Screen-space Ambient Occlusion (SSAO)

**Source note:** The URP e-book was 403-blocked at research time. The chapter enumeration above is from the official Unity learning thread, not the PDF itself. Treat the e-book as must-read before any URP post-processing work.

**Citation strength:** Moderate (chapter list from thread; PDF not retrieved). Source: `unity6-learning-resources-thread.md` (§1).

---

### 5.3 Shader Workflow (Shader Graph)

**Finding:** Far Horizon uses URP + Shader Graph for stylized effects. The built-in Standard shader renders magenta in URP — never use it. Use URP Lit / Simple Lit / Unlit as base, or author Shader Graph shaders for custom effects (gradient skybox, stylized water, outline).

**Unity 6 Shader Graph additions:** Production-ready sample shaders; UGUI Canvas support for UI shaders; customizable Heatmap performance visualization.

**ShaderLab + HLSL** remains the hand-shader-authoring path for advanced passes inside Render Graph Renderer Features.

**Citation strength:** Moderate. Sources: `unity6-resources-hub.md` (Shader Graph section); `purdue-intro-unity-pdf.md` (shading caveat); `unity-manual-pdf.md` (materials caveat).

---

### 5.4 Lighting on a Low-Poly World

- Single directional light (sun) + baked GI (APV).
- No shadowed point lights (6 passes each). Campfire → unshadowed point + baked emissive.
- Bake the static island with the GPU Lightmapper (production-ready in Unity 6).
- LOD shadows: disable shadow casting on small/distant props.
- Fog: camera far-clip + URP fog volume hides the draw-distance edge and contributes to the "world feels BIG" feel.
- Camera far-clip-plane tuning: start tight, open as needed.

**Citation strength:** Strong. Sources: `unity-how-to.md` (B5, B6, B7); `unity-manual-pdf.md` (Ch9 "camera clip + fog" tip).

---

## 6. UI Toolkit

### 6.1 When to Use UI Toolkit

UI Toolkit is Unity's modern, recommended runtime UI system. Benefits over uGUI: faster iteration (USS global management), better rendering performance (dynamic atlases, Render Hints), cleaner team collaboration (logic/structure/style split). Unity 6 includes it built-in — no package install needed.

**How it applies to Far Horizon:** Any new runtime UI (HUD beyond the build-stamp banner, survival-need bars, crafting menu, inventory) should use UI Toolkit. The existing build-stamp HUD can remain uGUI if touch-free; migrate on next meaningful touch.

---

### 6.2 Core Setup

- **UIDocument component** on a GameObject holds a Panel Settings asset + Source Asset (UXML VisualTreeAsset).
- **Panel Settings:** `Scale With Screen Size` + Reference Resolution (e.g. 1920×1080) + Match (dominant axis) — the standard desktop multi-resolution configuration.
- Separate Panel Settings for HUD vs menus (independent scale/sort).
- UI Toolkit elements do NOT appear in Scene view — only Game view / UI Builder preview.

---

### 6.3 UXML / USS / Flexbox

- **UXML** = structure (≈ HTML); **USS** = appearance (≈ CSS); **C#** = interaction logic.
- All styling in **USS selectors**, never inline styles (UXML is the source-of-truth for structure; USS for appearance).
- **BEM naming:** `block__element--modifier` — `inventory__slot--equipped`, `hud__bar--health`. Semantic, not presentational.
- **Flexbox/Yoga** layout engine. Unity 6 default Grow = 1 — new elements expand to fill their container unless given explicit Width/Height. Set explicit sizes to avoid surprise full-bleed expansion.
- **USS variables** (USS custom properties) as design tokens for palette/spacing. Note: UI Builder variable editing requires Unity 6.1+.
- **Pseudo-classes** (`:hover`, `:active`, `:focus`) + **USS transitions** give free hover/active animation without C# code.
- **Avoid overly broad selectors** (anything ending in `*` or targeting generic `.unity-*` classes) — expensive style-resolution on large hierarchies.

---

### 6.4 Runtime Data Binding (Unity 6 New)

**Finding:** Unity 6 introduces a runtime data binding system. Visual element properties link directly to data-source objects (ScriptableObjects, MonoBehaviours, custom classes). Changes propagate automatically.

- Mark bindable members `[CreateProperty]` for compile-time property bags (no reflection, fastest).
- Backing fields: `[SerializeField, DontCreateProperty] int m_Value;` + `[CreateProperty] public int Value { get; set; }`.
- **Binding modes:** `ToTarget` (source→UI, read-only; use for HUD readouts); `TwoWay` (bidirectional; use for settings sliders); `ToSource` (UI→source only); `ToTargetOnce` (one-shot).
- **Data-source inheritance:** children automatically inherit the parent's data source. Set one data source on a container; all children bind by path.
- **Unresolved bindings:** set Data Source Type + path in UXML, leave source empty; assign at runtime via `myElement.dataSource = x;` — best of both (designer paths + code flexibility).
- **Type converters:** transform raw data to UI format (e.g. float health% → StyleColor gradient). Register globally (`ConverterGroups.RegisterConverterGroup`) or per-binding. Keep converters trivial; heavy logic belongs in the data source.
- **ListView direct binding (Unity 6):** assign a list SO as data source, provide a UXML item template with unresolved bindings, set `listView.dataSource` at runtime — no per-row wiring.

**How it applies to Far Horizon:** HUD health/hunger/inventory counts bind to game-state SOs. Crafting/inventory lists use ListView direct binding. Avoid hard-coding `data-source` in UXML for panels that re-point at runtime.

---

### 6.5 Custom Controls (Unity 6 New Pattern)

```csharp
[UxmlElement]
public partial class SurvivalBar : VisualElement
{
    [UxmlAttribute] public float MaxValue { get; set; }
    public SurvivalBar() { /* init in ctor — NO Awake/OnEnable */ }
}
```

- `[UxmlElement]` registers the control in the UI Builder Library.
- `[UxmlAttribute]` exposes properties as Inspector fields; supports `Range`, `Tooltip`, etc.
- Initialize in the constructor or `AttachToPanelEvent` — NO `Awake` / `OnEnable` / `OnDestroy`.
- `SetValueWithoutNotify(value)` to update visual state without firing a ChangeEvent (avoids update loops).

---

### 6.6 UI Performance Rules

| Issue | Rule |
|---|---|
| Showing/hiding | `display:none` (cheapest for frequent toggles); `RemoveFromHierarchy` (cheapest for rare dialogs); NEVER `opacity:0` (everything still runs) |
| Animation | Animate **transforms** (`translate`/`scale`/`rotate`), NOT layout props (`width`/`height`). Enable `UsageHints.DynamicTransform` (per element) or `GroupTransform` (per animated parent) |
| Batching | ≤8 textures per batch (uber shader limit). Atlas icons via Sprite Atlas (static) + dynamic atlas (runtime inventory) |
| Masking | Rectangular masks (shader-based, no stencil) preferred over rounded (stencil-based, breaks batches, max 7 nest depth) |
| Vertex Budget | Raise in Panel Settings if Frame Debugger shows many draw calls from one Panel |
| Selectors | Keep shallow and specific; avoid `*` or `.unity-*` broad selectors |
| Lists | Use ListView **virtualization** for scrollable collections (renders only visible rows) |
| Memory | Split large UXML/USS into small modular templates; use Addressables for on-demand UI loading |

**Profiling UI:** Frame Debugger (draw calls/batches) + `SetPanelChangeReceiver` (Panel Settings — logs every UI change, dev builds only) to trace mystery updates.

**Citation strength:** Strong. Source: `ui-toolkit-advanced-unity6-pdf.md` (§8, pp. 130–148).

---

## 7. Assets and Addressables

### 7.1 Texture Import Rules

- **Read/Write Enabled = OFF** for all static textures. ON creates CPU+GPU copy = doubles memory.
- **Mip Maps:** ON for 3D distance-varying textures; OFF for UI, constant-size sprites.
- **Max Size:** minimum that is visually acceptable. Example: diffuse at 1024, roughness/metallic at 512.
- **Power-of-two dimensions** for compression to work.
- **Desktop compression = BC7/BC1 (DXT)**, NOT ASTC (mobile/XR only).

**Citation strength:** Strong. Source: `unity-how-to.md` (D1).

---

### 7.2 Mesh Import

- **Optimize Mesh Data** (Player Settings) strips unused vertex attributes by material need. **Confirm normals survive** — they are load-bearing for the low-poly smooth-faceted look (per unity-conventions.md).
- **FBX from Blender** is the interchange format. Low-poly faceted meshes: normals are split at hard edges (each face has its own normal set → more vertices than triangle count). Keep meshes genuinely low-poly to contain vertex count.
- Prefer **primitive colliders** (sphere/capsule/box) for interactable props over Mesh Colliders (accurate but expensive).

**Citation strength:** Strong. Sources: `unity-how-to.md` (D2); `purdue-intro-unity-pdf.md` (slide 13); `unity-manual-pdf.md` (Ch1, colliders section).

---

### 7.3 Addressables

**Finding:** Addressables (replaces Asset Bundles) is the modern Unity system for memory-controlled, on-demand asset streaming. Appropriate when asset count and world complexity grow beyond single-scene bounds.

**How it applies to Far Horizon:** For an MVP single-scene island, Addressables is premature. When the "big round island" world needs streaming chunks (distant terrain, biome-specific props), Addressables becomes the memory management path. Also relevant for UI: large UXML/USS files load all referenced assets into memory; use Addressables + `RemoveFromHierarchy` + `Addressables.Release` to unload out-of-view UI assets.

**Citation strength:** Moderate (version-agnostic guidance). Source: `rivello-best-practices.md` (Efficient Settings section); `ui-toolkit-advanced-unity6-pdf.md` (§8 memory section).

---

## 8. Input System

**Finding:** The legacy Input Manager (`UnityEngine.Input`, named axes/buttons via `Input.GetAxis`) is the old path. Unity 6 strongly favors the **new Input System package** (action maps, device abstraction, `InputAction`, `PlayerInput` component).

**Click-to-move pattern:** `Camera.ScreenPointToRay(Mouse.current.position.ReadValue())` → `Physics.Raycast(ray, out hit, float.MaxValue, groundLayerMask)` → `hit.point` = move destination.

**How it applies to Far Horizon:** The eval spike at `c:/Trunk/PRIVATE/EmbergraveUnitySlice` implemented click-to-move; follow that reference for the action-map wiring. Use the new Input System for all new input work.

**Coverage gap:** The raw research sources (all 2019-era decks + version-agnostic articles) give no Input System specifics. The official Unity 6 Input System docs at `docs.unity3d.com/Packages/com.unity.inputsystem@latest` are the authoritative reference.

**Citation strength:** Moderate (coverage thin in this research set). Sources: `purdue-intro-unity-pdf.md` (raycasting slide); `rivello-best-practices.md` (packages section naming Input System); `unity-manual-pdf.md` (Input Manager as legacy caveat).

---

## 9. Testing

**Framework:** Unity Test Framework (NUnit) with **EditMode** and **PlayMode** test modes. Assembly definitions separate test code (`FarHorizon.EditTests` / `FarHorizon.PlayTests`).

**Project testing bar (from CLAUDE.md / team/TESTING_BAR.md):**
- Paired EditMode/PlayMode tests
- Green checks
- **Shipped-build verification** (built exe runs; capture evidence for visual changes)
- Tess sign-off

**Profiling on dev builds:** the "profile on the built exe, not the editor" discipline is NOT just a perf rule — it is the testing discipline for visual/gameplay behavior (editor-vs-runtime divergence caused the spike iter6 "legs-up" incident).

**IL2CPP C# source line numbers (Unity 6):** Enable in Player Settings for the Windows player to get C# line numbers in player crash call stacks. Direct aid to shipped-build debugging.

**Test Runner entry point (headless):** `-runTests -testPlatform EditMode|PlayMode` per CLAUDE.md.

**Citation strength:** Moderate (testing framework guidance is thin in the raw sources; best content is in the project's own TESTING_BAR.md). Sources: `rivello-best-practices.md` (Testing section); `official-best-practice-guides.md` (§8 cross-reference); `unity6-resources-hub.md` (IL2CPP note).

---

## 10. Build and Platform

**Target:** Windows Standalone (`Build/Windows/FarHorizon.exe`). No HTML5/WebGL target (confirmed in CLAUDE.md).

**Build Profiles (Unity 6):** Multi-config build surface replacing the deprecated Build Settings window. `FarHorizonBuilder.BuildWindows` headless entry point should be aligned to Build Profiles.

**Scripting Backend = IL2CPP** for the Windows player. IL2CPP converts C# to C++ before compilation → better runtime performance than Mono.

**Scenes-in-Build:** `Boot.unity` must be index 0 (the entry point).

**Windows standalone output:** `.exe` + `*_Data/` folder. Both are required; the `_Data` folder contains the assets.

**Strip debug symbols / Debug.Log:** Strip all `Debug.Log` calls from shipping builds (string alloc + stack capture cost). Use `[Conditional("DEVELOPMENT_BUILD")]` or `Debug.unityLogger.logEnabled = false` in shipping configuration. (Note: the build-stamp HUD log is one-time, not per-frame — acceptable.)

**CI artifact discipline:** `Build/`, `Captures/`, `*.log`, `test-results*.xml` are gitignored; CI MUST upload these artifacts before cleanup.

**Standalone Resolution Dialog was removed in Unity 2019.1** — do not expect it in Unity 6 builds. Resolution is handled in-game via `Screen.SetResolution` or settings menus.

**Citation strength:** Strong (Windows standalone, Scenes-in-Build, IL2CPP). Sources: `unity6-resources-hub.md` (Build Profiles); `unity-manual-pdf.md` (Ch10, Build Settings); `rivello-best-practices.md` (IL2CPP, automated workflows); official Unity 6 what's-new (IL2CPP source lines).

---

## Sources Read — Coverage and Gaps

### Sources (8 readers)

| Key | What it is | Strength | Coverage |
|---|---|---|---|
| `unity6-resources-hub` | Unity 6 + URP 17 What's New manual pages (docs.unity3d.com) | Strong — official primary docs | GPU Resident Drawer, GPU Occlusion Culling, Render Graph, Forward+, APV, STP, editor tooling. **Gaps:** hub page itself 403; GC/Jobs depth; numeric Forward vs Forward+ vs Deferred trade-offs |
| `unity-how-to` | Unity's 30+ best-practice guides (via WebSearch excerpts; 403 on direct fetch) | Moderate-strong — Unity engineer content, excerpt-level | GPU perf, batching, scripting perf, GC, SO architecture, profiling, asset config. **Gaps:** UGUI optimization guide body not retrieved; Input System absent; guides body is excerpt-level |
| `official-best-practice-guides` | Unity 6 Manual pages (Smart Merge, batching methods, GC, URP perf) | Strong — official primary docs | Version control, GC/incremental GC, draw-call batching table, URP perf levers. **Gaps:** e-book bodies all 403; Forward vs Deferred numeric trade-offs; runtime-scaling code specifics |
| `rivello-best-practices` | Samuel Rivello Medium series + GitHub template | Weak-moderate — high-level/promotional; engine-version-agnostic | Folder structure, SO-for-data, IL2CPP, Enter Play Mode Options, Addressables, LTS, automated builds. **Gaps:** zero Unity-6-specific content; naming rules in paid course only |
| `unity-manual-pdf` | Packt "Unity Game Development Essentials" (2009, Unity 2.5) | Weak for specifics — 16-year-old book | Enduring 3D fundamentals: coordinate spaces, GameObject/Component, Update/FixedUpdate/deltaTime, colliders, prefabs, power-of-2 textures, Scenes-in-Build, camera far-clip + fog. Everything specific (language, UI, particles, animation, quality presets) is obsolete |
| `purdue-intro-unity-pdf` | Purdue VR course deck (Unity 2019.1.11f1, 37 slides) | Moderate for foundations — intro-level, 2019-era | GameObject/Component, lifecycle, Transform/Vector3/Quaternion, GetComponent, colliders/triggers, raycasting, prefabs. **Gaps:** zero Unity-6/URP/Input System/testing/Addressables/perf/animation content; VR slides irrelevant |
| `ui-toolkit-advanced-unity6-pdf` | Unity 6 UI Toolkit e-book (147pp, 2025) | Strong — official Unity Technologies; deeply retrieved | UXML/USS/Flexbox, BEM naming, runtime data binding (Unity 6), `[UxmlElement]`/`[UxmlAttribute]` custom controls, full performance chapter (batching, 8-texture limit, vertex budget, masking, transform-vs-layout, show/hide cost). **Gaps:** Localization detail (pp. 101–118) deprioritized |
| `unity6-learning-resources-thread` | Official Unity Discussions catalog (Oct 2024) | Moderate — index/catalog only; e-book bodies not retrieved | E-book and sample project catalog; chapter topic lists for URP e-book + Console/PC perf e-book; Design-Patterns/SOLID sample package metadata. **Gaps:** e-book PDF contents all 403 |

### Systematic Gaps (what this research set does NOT cover)

- **URP e-book body** (free, unity.com/resources/introduction-to-urp-advanced-creators-unity-6) — chapter list only; Volume/post-processing specifics, APV setup detail, Renderer Feature authoring, Render Graph code patterns not retrieved. **Action: read this e-book before any URP post-processing or Renderer Feature work.**
- **Console/PC Performance e-book body** (free, unity.com/resources/console-pc-game-performance-optimization-unity-6) — title + topic-scope only; 100+ pages of concrete tips not retrieved. **Action: read this e-book before any performance optimization sprint.**
- **Input System specifics** — new Input System action-map wiring, `InputAction`, `PlayerInput` component, device abstraction. Use the eval spike reference and `docs.unity3d.com/Packages/com.unity.inputsystem@latest`.
- **Addressables deep coverage** — streaming, memory management, async loading API specifics.
- **Animation / Mecanim / Humanoid rig** — covered by `.claude/docs/character-pipeline.md` already; not duplicated here.
- **Forward vs Forward+ vs Deferred numeric trade-offs** — the gated URP e-book is the source; not retrieved.
- **GC / memory deep culprit list** — the full boxing/LINQ/closure enumeration lives in the gated Console/PC perf e-book.
- **NavMesh / pathfinding** — not covered in any source; click-to-move movement target delivery mechanism not documented.

---

## Key Reading List (priority order for the team)

1. **URP Advanced Creators e-book (Unity 6, free):** `unity.com/resources/introduction-to-urp-advanced-creators-unity-6`
2. **Console/PC Performance e-book (Unity 6, free):** `unity.com/resources/console-pc-game-performance-optimization-unity-6`
3. **Design-Patterns/SOLID sample (URP, Unity 6):** Asset Store, "Level Up Your Code with Design Patterns and SOLID"
4. **Project Org / Version Control e-book (Unity 6, free):** `unity.com/resources/best-practices-version-control-unity-6` (skim non-Unity-VCS chapters)

---

## Gap-Fill — 2026-06-16

Sources used for this pass: Unity 6 Manual pages on `docs.unity3d.com` (all direct-fetched and confirmed 200); Unity Input System package docs at `docs.unity3d.com/Packages/com.unity.inputsystem@1.7` and `@1.14`; Addressables package docs at `docs.unity3d.com/Packages/com.unity.addressables@1.14`; Unity Scripting API for `Application.targetFrameRate`; WebSearch for GC culprits (Unity Manual 6000.x confirmed). The gated `unity.com/resources/console-pc-game-performance-optimization-unity-6` e-book was again 403; items drawn from it in this pass are routed through the reachable Manual sub-pages it distills.

---

### GF-1. GC Allocation Culprits — Concrete List With No-Alloc Alternatives

**Gap filled from:** `docs.unity3d.com/6000.0/Documentation/Manual/performance-reference-types.html` (Strong — official primary docs); `docs.unity3d.com/Manual/Coroutines.html` (Strong — official primary docs); `docs.unity3d.com/6000.0/Documentation/Manual/programming-best-practices.html` + WebSearch excerpts from the Unity 6 Manual (Strong — official).

#### 1. Boxing — most common hidden allocation in Unity codebases

**What it is:** A value type (`int`, `float`, `struct`, `enum`) is implicitly converted to `object` and placed on the managed heap.

**Common triggers in Unity code:**
- Passing value types to `object`-typed parameters: `Debug.Log(someInt)` (log formatter takes `object`).
- Non-generic collection APIs: `ArrayList`, `Hashtable` (store everything as `object`).
- Interface dispatch on a struct: calling an interface method on a struct boxes the struct.
- `Enum` comparisons using `.Equals(object)` instead of the typed `==`.

```csharp
// BAD — boxes x on every call:
int x = 1;
object y = new object();
y.Equals(x);

// GOOD — no boxing:
x.Equals(1);  // or direct comparison
```

**No-alloc alternatives:** Use generic methods (`List<T>`, `Dictionary<TKey, TValue>`, `IEquatable<T>`). Never pass a value type to an `object`-typed parameter in a hot path.

**Citation strength:** Strong. Source: `docs.unity3d.com/6000.0/Documentation/Manual/performance-reference-types.html`

---

#### 2. String Concatenation

**What it is:** Strings in C# are immutable reference types. Every `+` or `+=` operation allocates a new string on the heap and abandons the old one to GC.

```csharp
// BAD — one new string per iteration:
string result = "";
for (int i = 0; i < parts.Length; i++)
    result += parts[i];

// GOOD — StringBuilder, single final allocation:
var sb = new StringBuilder(64);
sb.Clear();
foreach (var p in parts) sb.Append(p);
string result = sb.ToString();
```

**Hot-path rule:** No string concatenation in `Update`, `FixedUpdate`, or any per-frame callback. The build-stamp HUD writes once at startup — acceptable. Per-frame score/timer display: use a pre-built format string or TextMeshPro's `SetText(float)` overload (no alloc).

**Citation strength:** Strong. Source: `docs.unity3d.com/6000.0/Documentation/Manual/performance-reference-types.html`

---

#### 3. LINQ in Hot Paths

**What it is:** LINQ methods (`Where`, `Select`, `OrderBy`, `FirstOrDefault`, etc.) allocate enumerators, delegates, and intermediate arrays. Their use in `Update`/`FixedUpdate` is one of the most common per-frame GC sources in Unity projects.

**Rule:** No `using System.Linq;` imports in any MonoBehaviour that runs code per-frame. LINQ is fine at startup / in editor tools / in non-hot-path initialization logic.

**Alternative:** Pre-compute and cache results at `Start`; use `for` loops with cached arrays; or use Burst-compiled `NativeArray<T>` queries for hot paths that need query-style access.

**Citation strength:** Strong. Source: `docs.unity3d.com/6000.0/Documentation/Manual/programming-best-practices.html` (excerpt via WebSearch confirming "avoid use of LINQ in runtime code, especially in the context of Update or FixedUpdate and other hot paths").

---

#### 4. Closures and Lambdas in Hot Paths

**What it is:** A closure (anonymous method or lambda) that captures variables from its enclosing scope forces the compiler to generate a hidden class to hold the captured state. Instantiating that class is a heap allocation.

```csharp
// BAD — closure captures 'desiredDivisor', allocates hidden class each Sort call:
int desiredDivisor = GetDivisor();
list.Sort((x, y) => (int)x.CompareTo((int)(y / desiredDivisor)));

// GOOD — named static comparer, no capture:
list.Sort(MyComparer.Instance);
```

**Special case — `WaitUntil` / `WaitWhile`:** These accept a `Func<bool>` predicate. If you write `new WaitUntil(() => someCondition)`, the lambda allocates. Cache the predicate as a named field instead.

**Citation strength:** Strong. Source: `docs.unity3d.com/6000.0/Documentation/Manual/performance-reference-types.html`

---

#### 5. Coroutine `yield` Allocations

**What it is:** `StartCoroutine(SomeMethod())` itself allocates an `IEnumerator` state machine object. The specific `yield return` instruction matters:

| Yield instruction | Allocates? | Notes |
|---|---|---|
| `yield return null` | **No** — no allocation | Cheapest; delays one frame |
| `new WaitForSeconds(t)` | **Yes** — allocates each call | Class on managed heap |
| `new WaitForFixedUpdate()` | **Yes** | Same |
| `new WaitUntil(...)` | **Yes** + lambda allocation | Cache both |
| `yield return someRoutine` | Allocates the nested routine | Once per nest |

**No-alloc pattern for `WaitForSeconds`:**
```csharp
// Cache at field level — allocates ONCE:
private static readonly WaitForSeconds s_tickWait = new WaitForSeconds(0.5f);

IEnumerator SurvivalTick()
{
    while (true)
    {
        ApplyNeedDecay();
        yield return s_tickWait;  // zero per-tick allocation
    }
}
```

**`params` modifier allocation:** Methods with `params object[] args` allocate an array for every call even if the params are empty. Avoid `params` in any method on a hot code path; prefer explicit overloads.

**Unity 6 note:** Unity 6 promotes `Awaitable` (the new async/await integration) as an alternative to coroutines with better performance characteristics. For the survival tick, a cached-`WaitForSeconds` coroutine is still the simplest correct pattern; `Awaitable` is worth evaluating for complex async sequences.

**Citation strength:** Strong. Source: `docs.unity3d.com/Manual/Coroutines.html`; WebSearch confirms Unity's recommendation to cache yield instructions.

---

#### 6. API Calls That Return Managed Arrays

Several Unity APIs silently allocate a new managed array on every call:

| API | Allocates | No-alloc alternative |
|---|---|---|
| `GetComponents<T>()` | New `T[]` every call | Cache result in `Awake`; or `GetComponents<T>(list)` with a pre-allocated `List<T>` |
| `Physics.RaycastAll()` | New `RaycastHit[]` | Use `Physics.RaycastNonAlloc(ray, results[], ...)` with a pre-allocated array |
| `Mesh.vertices` (get) | New `Vector3[]` copy | Use `Mesh.GetVertices(list)` or `Mesh.GetNativeVertexBufferPtr` |
| `FindObjectsOfType<T>()` | New array every call | Cache at startup; never call per-frame |
| `Camera.allCameras` | New array every call | Cache or use `Camera.main` (itself also expensive — cache that too) |

**Citation strength:** Strong (composite from Unity 6 Manual GC section + official best-practice guides).

---

### GF-2. Runtime Performance Scaling — `targetFrameRate`, `vSyncCount`, Dynamic Resolution / STP

**Gap filled from:** `docs.unity3d.com/ScriptReference/Application-targetFrameRate.html` (Strong — official Scripting API reference); `docs.unity3d.com/6000.2/Documentation/ScriptReference/QualitySettings-vSyncCount.html` (Strong — official); round-1 research note §1.6 (STP) carried through.

#### `Application.targetFrameRate` vs `QualitySettings.vSyncCount`

**The rule: vSyncCount always wins.** When `QualitySettings.vSyncCount != 0`, `Application.targetFrameRate` is silently ignored.

```csharp
// Desktop: let vSync govern frame pacing (hardware-based, no microstutter):
QualitySettings.vSyncCount = 1;   // Lock to display refresh (60/120/144 Hz)
// Application.targetFrameRate is irrelevant here

// Mobile / uncapped (e.g. benchmark or load screen) — disable vSync first:
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 60; // software-based; subject to microstutter

// Uncapped — desktop renders "as fast as possible":
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = -1; // default
```

**Desktop recommendation (Far Horizon):** Use `QualitySettings.vSyncCount = 1` in the shipped build. vSyncCount is a hardware-based sync mechanism and produces smooth frame pacing. `targetFrameRate` is software-based and cannot eliminate microstutter on desktop. The official scripting docs say: "It's recommended to use `QualitySettings.vSyncCount` over `Application.targetFrameRate` because vSyncCount implements a hardware-based synchronization mechanism."

**vSyncCount values:**
- `0` — disabled; `targetFrameRate` governs (or uncapped if -1)
- `1` — sync every display refresh (60 fps on a 60 Hz display)
- `2` — sync every other refresh (30 fps on 60 Hz)

**When `targetFrameRate` IS useful:** Deliberately throttle to conserve battery on mobile, or to cap a headless test build where vSync is irrelevant.

**Citation strength:** Strong. Sources: `docs.unity3d.com/ScriptReference/Application-targetFrameRate.html`; `docs.unity3d.com/6000.2/Documentation/ScriptReference/QualitySettings-vSyncCount.html`.

#### Dynamic Resolution and STP (carry-forward from §1.6 with additions)

Dynamic Resolution renders at a lower internal resolution and upscales. Two modes in URP:

- **Hardware Dynamic Resolution** — GPU-native scaling on supported APIs (DX12, Vulkan). Lower driver overhead. Not universally available.
- **Software Dynamic Resolution** — Unity-side pixel scaling; broader support.

**Enable:** URP Asset → `Quality > Upscaling Filter`. Options: Bilinear, FSR (AMD), **Spatial-Temporal Post-Processing (STP)** (Unity 6 native). STP uses temporal history to reconstruct detail.

**When to use for Far Horizon:** Low-poly geometry already runs light; dynamic resolution is a fallback lever for GPU-bound scenes (dense jungle at camera + heavy fog + bloom all active simultaneously). Do not enable by default — it softens hard polygon edges and may harm the intended low-poly aesthetic. Profile first; enable only if a measured GPU-bound frame budget requires it.

**Citation strength:** Strong for API (official docs); Moderate for STP-on-low-poly caveat (no numeric benchmark — engineering judgment from the aesthetic properties of temporal upscalers on sharp edges).

---

### GF-3. Forward vs Forward+ vs Deferred — Trade-offs (URP)

**Gap filled from:** `docs.unity3d.com/6000.0/Documentation/Manual/urp/rendering/forward-rendering-paths.html` (Strong — official URP Manual); `docs.unity3d.com/6000.0/Documentation/Manual/render-pipelines-feature-comparison.html` (Strong — official).

| | **Forward** | **Forward+** | **Deferred** |
|---|---|---|---|
| **Light limit (opaque objects)** | 1 directional + up to 8 per-object | Unlimited per-tile (no per-object cap) | Unlimited for opaque |
| **Light limit (transparent objects)** | Same per-object limit | Same as Forward | Shaded in forward mode — same as Forward+ |
| **Tile-based light culling** | No | Yes — screen divided into tiles; per-tile light lists | No |
| **Reflection Probe Blending** | Yes | **No** — setting ignored | — |
| **Per-object light settings in URP Asset** | Honored | **Ignored** | — |
| **MSAA hardware anti-aliasing** | Yes | Yes | **No** (use TAA/SMAA instead) |
| **GPU Resident Drawer prerequisite** | No (requires Forward+) | **Yes — this is the required path** | No (GPU Resident Drawer requires Forward+) |
| **GPU Occlusion Culling prerequisite** | No | **Yes — requires Forward+** | No |
| **Transparency** | Native | Native | Transparents fall back to forward shading |
| **Memory cost** | Lower (one pass per light per object) | Slightly higher (tile buffer overhead) | Highest (G-buffer: albedo/normal/specular/depth textures) |
| **When to choose** | Very few lights; mobile/XR; GPU Resident Drawer not needed | **Far Horizon's chosen path.** Many lights; GPU Resident Drawer + GPU Occlusion Culling required. | Many dynamic lights on opaque geometry; MSAA not needed; typically HDRP territory |

**Why Far Horizon is on Forward+:** The project enabled GPU Resident Drawer (up to ~50% CPU draw-call savings for a repeated-prop island world) and GPU Occlusion Culling (dense jungle). Both require Forward+. Additionally, removing the per-object light cap means the campfire point light + future torches/lanterns carry no artificial limit. The loss of Reflection Probe Blending is acceptable for the stylized low-poly look.

**Deferred ruling:** Deferred has no per-opaque-light limit but cannot use MSAA, requires a G-buffer (4+ render targets), costs more memory bandwidth, and does not support GPU Resident Drawer. For a low-poly survival game targeting 60 fps on desktop where MSAA may be desirable, Deferred has no compelling advantage over Forward+. Ruled out.

**Citation strength:** Strong. Sources: `docs.unity3d.com/6000.0/Documentation/Manual/urp/rendering/forward-rendering-paths.html`; feature comparison table from `render-pipelines-feature-comparison.html`.

---

### GF-4. Addressables — Workflow, Async Load/Release, Memory

**Gap filled from:** `docs.unity3d.com/Packages/com.unity.addressables@1.14/manual/MemoryManagement.html` (Strong — official package docs); WebSearch over Addressables 2024 (Moderate — corroborates the core API).

#### What Addressables does

Addressables replaces the deprecated `Resources` folder and Unity's older `AssetBundles` API. Assets are addressed by a string key (or an `AssetReference`); the system resolves where they live (local or remote) transparently. It is **reference-counted**: every load call increments a ref-count; every `Release` call decrements it; the asset unloads when the count reaches zero.

#### Core workflow (Unity 6 / Addressables 2.x)

**1. Mark assets Addressable**
In the Project window: right-click any asset → **Addressable** toggle (or Inspector checkbox). The address defaults to the asset path; rename to a stable key (e.g. `"Props/Tree01"`).

**2. Groups and Labels**
- **Groups** control how assets are packed into AssetBundles (one group = one or more bundles per build strategy). Separate groups by load-time pattern: startup assets vs. streaming world chunks vs. UI.
- **Labels** let you load a collection of assets with one call (`Addressables.LoadAssetsAsync<T>("Label", callback)`).

**3. Async load**
```csharp
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

AsyncOperationHandle<GameObject> handle =
    Addressables.LoadAssetAsync<GameObject>("Props/Tree01");

handle.Completed += op =>
{
    if (op.Status == AsyncOperationStatus.Succeeded)
    {
        GameObject instance = Instantiate(op.Result);
        // Store 'handle' — you must keep it to release later
    }
};
```

Or `await` in an async method (Unity 6 `Awaitable` or standard `Task`):
```csharp
var handle = Addressables.LoadAssetAsync<GameObject>("Props/Tree01");
await handle.Task;
var prefab = handle.Result;
```

**4. Release — the mandatory discipline**

Every `LoadAssetAsync` call that is no longer needed MUST be released. Forgetting to release is the #1 Addressables memory leak.

```csharp
// Release the handle — decrements ref-count; unloads when count = 0
Addressables.Release(handle);

// For instantiated GameObjects loaded via LoadAssetAsync + Instantiate:
Addressables.ReleaseInstance(gameObject); // decrements the underlying asset's ref-count
```

**Critical rule on AssetBundles and partial unload:** You cannot partially unload a bundle. If 5 assets share a bundle and you release 4 of them, the bundle stays loaded until all 5 are released. Design groups so that assets with similar lifetime (e.g. all jungle-biome props) share a group.

#### Application to Far Horizon

For the current single-scene MVP island, Addressables is **premature overhead** — use direct scene references. Introduce Addressables when: (a) world streaming chunks are needed for the "big round island" (terrain cells, biome-specific prop sets loaded/unloaded as the player moves), or (b) UI panels become large enough that loading them all at startup is measurable. When Addressables is introduced, start with a simple two-group split: `AlwaysLoaded` (startup assets, player character, core systems) and `StreamingWorld` (terrain, biome props).

**Citation strength:** Strong for the core API pattern. Source: `docs.unity3d.com/Packages/com.unity.addressables@1.14/manual/MemoryManagement.html`. The 2.x API shape is the same; verified against WebSearch for 2024 usage.

---

### GF-5. Input System — Actionable Setup for WASD + Jump + Sprint (Locomotion Milestone)

**Gap filled from:** `docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/QuickStartGuide.html` (Strong — official docs); `docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/ActionBindings.html` (Strong — official docs); `docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/ActionAssets.html` (Strong — official docs); `docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/PlayerInput.html` (Strong — official docs). **This is the highest-value gap — directly feeds tickets 86ca9yq2x / 86ca9yq34 / 86ca9yq3q.**

#### Step 0 — Install the package

`Window > Package Manager > Unity Registry > Input System`. Installing prompts to disable the old Input Manager; confirm. Project Settings will add an **Input System Package** section.

#### Step 1 — Create the Input Actions asset

`Assets > Create > Input Actions` → name it `FarHorizonInputActions.inputactions`.

Double-click to open the Actions Editor. You will see two columns: **Action Maps** (left) and **Actions + Bindings** (right).

#### Step 2 — Create the `Player` action map

Click `+` in the Action Maps column → name it `Player`.

#### Step 3 — Define actions

| Action name | Action Type | Control Type | Notes |
|---|---|---|---|
| `Move` | Value | Vector 2 | WASD + Arrow keys + Gamepad stick |
| `Jump` | Button | — | Space bar + Gamepad South |
| `Sprint` | Button | — | Left Shift + Gamepad East (or hold) |

For each action, right-click the action and choose the binding type:

**`Move` — 2D Vector Composite**
Right-click `Move` → **Add 2D Vector Composite**. Name it `WASD`. Expand it; set the four child bindings:
- Up → `<Keyboard>/w`
- Down → `<Keyboard>/s`
- Left → `<Keyboard>/a`
- Right → `<Keyboard>/d`

Add a second composite for **Arrow Keys** (same structure, different paths). Add a **Gamepad Stick** binding: `<Gamepad>/leftStick`.

**`Jump`** → **Add Binding** → path: `<Keyboard>/space`. Add second binding: `<Gamepad>/buttonSouth`.

**`Sprint`** → **Add Binding** → path: `<Keyboard>/leftShift`. Add second binding: `<Gamepad>/buttonEast`.

In code, `2DVector` composites produce normalized diagonals by default (mode `Digital Normalized`). The result is a `Vector2` where cardinal directions are `(1,0)`, `(0,1)`, etc., and diagonals are approximately `(0.71, 0.71)`.

#### Step 4 — Generate a C# wrapper class (recommended)

In the Inspector for `FarHorizonInputActions.inputactions`, check **Generate C# Class** → **Apply**. Unity generates `FarHorizonInputActions.cs` with fully typed accessors (no string lookups, no null-ref risk).

#### Step 5a — `PlayerInput` component (simplest wiring)

Add `PlayerInput` component to the player GameObject. Assign `FarHorizonInputActions` as the **Actions** asset. Set **Notification** to `Invoke C# Events` (most testable) or `Send Messages` (simplest, less testable).

**`Invoke C# Events` (recommended):**
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerLocomotion : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.8f;
    [SerializeField] private float jumpForce = 7f;

    private Rigidbody _rb;
    private Vector2 _moveInput;
    private bool _isSprinting;

    private void Awake() => _rb = GetComponent<Rigidbody>();

    // Called by PlayerInput via Invoke C# Events:
    public void OnMove(InputAction.CallbackContext ctx)
        => _moveInput = ctx.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)                        // fired once on press
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    public void OnSprint(InputAction.CallbackContext ctx)
        => _isSprinting = ctx.ReadValueAsButton(); // true while held

    private void FixedUpdate()
    {
        float speed = moveSpeed * (_isSprinting ? sprintMultiplier : 1f);
        var move = new Vector3(_moveInput.x, 0f, _moveInput.y) * speed;
        _rb.MovePosition(_rb.position + move * Time.fixedDeltaTime);
    }
}
```

**`Send Messages` alternative (even simpler, less testable):**
Define methods named `OnMove`, `OnJump`, `OnSprint` on a MonoBehaviour on the same GameObject. Unity calls them via reflection — fine for a single player, slightly heavier than `Invoke C# Events`.

#### Step 5b — Direct polling (no `PlayerInput`, full control)

Use the generated C# wrapper directly. Suitable when you don't need automatic device assignment:

```csharp
private FarHorizonInputActions _input;

private void Awake()
{
    _input = new FarHorizonInputActions();
    _input.Player.Enable();
}

private void OnDisable() => _input.Player.Disable();

private void Update()
{
    Vector2 move = _input.Player.Move.ReadValue<Vector2>();
    bool jumping = _input.Player.Jump.IsPressed();
    bool sprinting = _input.Player.Sprint.IsPressed();
    // ... apply to CharacterController or Rigidbody
}
```

#### Choosing `PlayerInput` vs direct polling

| | `PlayerInput` component | Direct polling via wrapper |
|---|---|---|
| **Multiplayer device assignment** | Automatic per-player device filtering | Manual — use `InputSystem.actions` only if single-player |
| **Setup complexity** | Inspector-driven (no code) | Code-only; no Inspector wiring |
| **Testability** | Harder (depends on Inspector wiring) | Easier (instantiate wrapper in tests) |
| **Recommended for Far Horizon** | Single-player → either works; **direct wrapper is slightly simpler for single-player** | |

**Far Horizon is single-player.** Either approach works. Recommended: direct wrapper (`new FarHorizonInputActions()`) — no hidden Inspector state, testable in EditMode.

#### The old Input Manager path — DO NOT USE for new code

`Input.GetAxis("Horizontal")`, `Input.GetButtonDown("Jump")` etc. are the legacy path. The new Input System is installed, the project targets Unity 6, and the locomotion milestone specifically introduces new input. Do not mix old and new systems.

**Citation strength:** Strong. Sources: `docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/QuickStartGuide.html`; `docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/ActionBindings.html`; `docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/ActionAssets.html`; `docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/PlayerInput.html`.

---

### Gap Status After This Pass

| Gap | Status |
|---|---|
| GC allocation culprits — full concrete list | **Filled** (GF-1): boxing, strings, LINQ, closures, coroutine yields, API array returns — all with no-alloc alternatives |
| Runtime perf scaling: `targetFrameRate` / `vSyncCount` / STP | **Filled** (GF-2): API, interaction rules, desktop recommendation |
| Forward vs Forward+ vs Deferred trade-offs | **Filled** (GF-3): feature matrix, why FH is on Forward+, Deferred ruled out |
| Addressables workflow | **Filled** (GF-4): groups/labels/load/release API, memory rules, FH timing guidance |
| Input System — WASD + Jump + Sprint actionable setup | **Filled** (GF-5): end-to-end binding guide, `PlayerInput` vs direct wrapper, C# examples |
| URP e-book body (Volume / Renderer Features / Render Graph code) | **Still open** — `unity.com` blocked; read the e-book directly before custom pass work |
| Console/PC Performance e-book body | **Still open** — same block; GF-1 covers the GC chapter's core content via reachable Manual sub-pages |
| NavMesh / pathfinding for click-to-move | **Still open** — not in scope of this pass; use Unity Manual NavMesh docs directly |
