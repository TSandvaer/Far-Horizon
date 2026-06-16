# Unity "How-To" best-practices hub — raw extract (Unity 6 / URP, desktop focus)

**Source key:** `unity-how-to`
**Landing page:** https://unity.com/how-to ("Explore Unity's best practices" — a hub of 30+ guides by Unity engineers / technical artists)
**Retrieval note (IMPORTANT, honesty):** Direct `WebFetch` of `unity.com/*` returned **HTTP 403 Forbidden** on every attempt (bot-protection / WAF blocks the markdown fetcher). The Google cache redirected to a consent wall and could not be parsed. Content below was therefore retrieved via **WebSearch against `unity.com`**, which surfaces real excerpts from the same guide pages. Each finding cites the canonical guide URL it came from. Where a value is a direct quote from the WebSearch snippet of that page it is marked; where it is general best-practice corroborated across multiple snippets it is noted. **No page contents were fully rendered**, so this is a faithful summary of the surfaced excerpts, NOT a verbatim full-page transcription — see `gaps` for what could not be retrieved.

**Project relevance:** Far Horizon = Unity 6 (6000.4.10f1), URP, low-poly smooth-shaded, **desktop-first (Windows)**, single-player survival. The most load-bearing guides for this project are: high-end-graphics performance, GPU optimization, project configuration/assets, profiling, ScriptableObject architecture, and C# scripting performance. Mobile/XR-specific advice (ASTC, tile-based GPU bandwidth) is marked as out-of-scope-but-noted.

---

## A. Hub overview — the how-to guide catalogue

**Topic:** What the how-to hub is and the guides most relevant here.
**Finding:** Unity's how-to hub ("Explore Unity's best practices") aggregates 30+ best-practice guides authored by Unity engineers and technical artists. The Unity-6-relevant guides surfaced in search:
- Performance optimization for high-end graphics on PC and console — https://unity.com/how-to/performance-optimization-high-end-graphics
- Managing GPU usage for PC and console games — https://unity.com/how-to/gpu-optimization
- Configuring your Unity project for stronger performance (project configuration & assets) — https://unity.com/how-to/project-configuration-and-assets
- Best practices for profiling game performance — https://unity.com/how-to/best-practices-for-profiling-game-performance
- Optimize your game with the Unity Profile Analyzer — https://unity.com/how-to/optimize-your-game-unity-profile-analyzer
- Profiling and debugging with Unity and native platform tools — https://unity.com/how-to/profiling-and-debugging-tools
- Analyze memory usage with memory profiling tools — https://unity.com/how-to/analyze-memory-usage-memory-profiling-tools (also /use-memory-profiling-unity)
- Advanced programming and code architecture — https://unity.com/how-to/advanced-programming-and-code-architecture
- Best practices for performance optimization in Unity — https://unity.com/how-to/best-practices-performance-optimization-unity
- Architect game code with ScriptableObjects — https://unity.com/how-to/architect-game-code-scriptable-objects
- Use ScriptableObjects as event channels — https://unity.com/how-to/scriptableobjects-event-channels-game-code
- Get started with the ScriptableObjects demo — https://unity.com/how-to/get-started-with-scriptableobjects-demo
- Scripting in Unity for experienced C# & C++ programmers — https://unity.com/how-to/programming-unity
- Unity UI performance optimization tips — https://unity.com/how-to/unity-ui-optimization-tips

**Why it matters:** These are Unity's own recommended workflows; for a small team they are the highest-signal, lowest-noise starting point (vs. forum threads).
**How it applies:** Erik/team can treat this set as the canonical reference list for Far Horizon's performance + architecture conventions; cite the specific guide URL in PR review notes.
**Citation:** WebSearch over unity.com, 2026-06-16; landing page https://unity.com/how-to (403 on direct fetch).

---

## B. Performance optimization for high-end graphics (PC & console) — Unity 6 features

**Source guide:** https://unity.com/how-to/performance-optimization-high-end-graphics (+ corroborating Unity 6 blogs)

### B1. GPU Resident Drawer (GPU-driven rendering)
- **Finding:** GPU-driven rendering system (URP **and** HDRP) that moves per-object render setup from CPU to GPU; enabled in the Render Pipeline Asset via **"Instanced Drawing."** Reported up to **~50% CPU frame-time reduction** for GameObjects when rendering large, complex scenes; "games that are CPU bound due to a high number of draw calls can improve in performance as the amount of draw calls is reduced." Gains scale with scene size + amount of instancing.
- **Why it matters:** A big, dense, endless-feeling island world (Far Horizon's north star) is the exact CPU-draw-call-bound case this targets.
- **How it applies:** Enable Instanced Drawing on the URP Pipeline Asset; pairs naturally with the low-poly repeated-prop world (trees, rocks, jungle). Validate with the Profiler before/after.
- **Version note:** Unity 6 feature.
- **Citation:** /performance-optimization-high-end-graphics; Unity 6 features blog https://unity.com/blog/unity-6-features-announcement.

### B2. GPU Occlusion Culling
- **Finding:** Works "in tandem with the GPU Resident Drawer"; reduces overdraw per frame by not rendering occluded geometry. Enable via a checkbox on the Render Pipeline Asset ("toggle the GPU Occlusion check box").
- **Why it matters:** Survival world with hills/jungle has heavy occlusion potential; cuts overdraw + GPU cost.
- **How it applies:** Enable alongside GPU Resident Drawer in the URP asset; especially valuable with the project's mountains/dense-jungle direction.
- **Version note:** Unity 6 feature.
- **Citation:** /performance-optimization-high-end-graphics; /gpu-optimization.

### B3. Render Graph (URP)
- **Finding:** New rendering framework/API in Unity 6 that simplifies pipeline maintenance/extensibility and improves efficiency — "automatic merging and creation of native render passes" to reduce memory bandwidth + energy (biggest win on tile-based/mobile GPUs; bandwidth gains cited up to ~50%). **Critical upgrade gotcha:** projects upgraded to Unity 6 auto-enable **Compatibility Mode** to ease migration, but **"Compatibility Mode is not intended for shipping."** Once working, turn it OFF and convert custom `ScriptableRenderPass`/`RendererFeature` code to the Render Graph API.
- **Why it matters:** If Far Horizon adds any custom render features (e.g., the Zone-D quality pass: gradient skybox, fog, custom passes), they must be Render-Graph-authored to ship correctly and get the perf benefit.
- **How it applies:** Confirm Compatibility Mode is OFF for shipping builds; author any custom passes against Render Graph; the bandwidth win matters less on desktop than mobile but the API is now the only supported path.
- **Version note:** Unity 6 feature; Compatibility Mode is a transition crutch.
- **Citation:** /performance-optimization-high-end-graphics; URP Render Graph discussions on unity.com.

### B4. Spatial-Temporal Post-Processing (STP) upscaler
- **Finding:** Renders frames at a lower resolution and upscales "without any loss of fidelity," improving GPU performance and visual quality at runtime.
- **Why it matters:** Lets a desktop game hit higher effective resolution / framerate on mid-range GPUs.
- **How it applies:** Optional quality/perf lever for Far Horizon's Windows build if GPU-bound at native res; evaluate against the low-poly look (upscalers can soften hard low-poly edges — test before adopting).
- **Version note:** Unity 6 feature.
- **Citation:** /performance-optimization-high-end-graphics.

### B5. Lighting — GPU Lightmapper + Adaptive Probe Volumes (APV)
- **Finding:** **GPU Lightmapper is production-ready in Unity 6** — bakes static lighting on the GPU, "dramatically" faster bake times than the CPU lightmapper. **Adaptive Probe Volumes (APVs)** = a global-illumination solution giving dynamic/efficient GI in complex scenes, tuning both perf and quality. General lighting guidance from the guide: **bake static lighting** ("Lightmapping") instead of real-time where possible ("runs 2-3 times faster for two-per-pixel lights"). Caveat surfaced from community: in some HDRP 6 cases APV can regress vs realtime — measure on your content.
- **Why it matters:** A largely static survival island is an ideal bake-everything candidate; faster bakes = faster iteration for the art/lighting passes.
- **How it applies:** Bake static world lighting with the GPU Lightmapper; evaluate APV for the warm gradient-lit GI look; keep realtime lights minimal (sun/key + few). Measure APV cost on actual scene before committing.
- **Version note:** Unity 6 (GPU Lightmapper production-ready; APV).
- **Citation:** /performance-optimization-high-end-graphics; https://unity.com/blog/engine-platform/new-ways-of-applying-global-illumination-in-unity-6.

### B6. Shadows
- **Finding:** Shadow casting can be disabled per-MeshRenderer and per-light; "disabling shadows whenever possible reduces draw calls." **Avoid shadowed point lights** — each shadowed point light = **six shadow-map passes**; prefer spotlights where dynamic shadows are needed. High-quality presets can default to **4K shadow maps**; reducing shadow-map resolution lowers frame cost. (HDRP-phrased in the snippet but the principle carries to URP.)
- **Why it matters:** Shadows are a common silent GPU/CPU cost; low-poly worlds rarely need ultra shadow res.
- **How it applies:** Single directional sun shadow for the island; disable shadow casting on small props/foliage that don't read; pick a modest shadow resolution in URP quality settings; never use shadowed point lights for campfire glow — use an unshadowed point light or baked/emissive.
- **Citation:** /performance-optimization-high-end-graphics; LOD-your-lights tutorial (Unity Discussions).

### B7. LOD (Level of Detail)
- **Finding:** "LODs are ubiquitous with gamedev optimization." Lack of LOD systems hurts GPU on mobile but "can affect PC and console GPUs as well." Concept also applies to realtime-light shadows (shadow LOD).
- **Why it matters:** Endless-world rendering needs distance-based simplification to stay GPU-affordable.
- **How it applies:** Author LOD groups for hero trees/rocks/landmarks; consider culling/shadow-LOD on distant foliage. Low-poly base meshes are already cheap, so LOD priority = density management + draw-call reduction (pairs with GPU Resident Drawer).
- **Citation:** /performance-optimization-high-end-graphics; Shadow-LOD discussions.

### B8. Async Compute
- **Finding:** "Async compute can move compute shader work in parallel to the graphics queue," making better use of GPU resources; work splits into wavefronts across SIMDs ("wavefront occupancy" = wavefronts in use vs max).
- **Why it matters:** Advanced GPU lever; mostly relevant if Far Horizon adds compute-heavy effects (water sim, particles).
- **How it applies:** Likely **out of scope** for a low-poly survival MVP; note for later if water/VFX become compute-bound.
- **Citation:** /performance-optimization-high-end-graphics; /gpu-optimization.

---

## C. Managing GPU usage (PC & console) — draw calls, batching, overdraw

**Source guide:** https://unity.com/how-to/gpu-optimization

### C1. SRP Batcher
- **Finding:** "The SRP Batcher can reduce the GPU setup between DrawCalls by batching Bind and Draw GPU commands." To benefit: **"use as many materials as needed, but restrict them to a small number of compatible shaders."** (SRP Batcher batches by shader-variant compatibility, not by shared material — opposite intuition from old static/dynamic batching.)
- **Why it matters:** Far Horizon's recolor/low-poly look can use many material instances of a FEW shaders and still batch well.
- **How it applies:** Keep the shader count small (one core URP Lit-style shader + the Zone-D quality shader); proliferate material *instances/colors* freely; verify SRP Batcher is on (default in URP).
- **Citation:** /gpu-optimization.

### C2. GPU instancing + GPU Resident Drawer for draw-call reduction
- **Finding:** "Optimization on console will often mean reducing draw call batches" via GPU instancing, SRP Batcher, and GPU Resident Drawer. (See B1/B2 for the Unity 6 GPU-driven path.)
- **Why it matters:** Draw-call count is the classic CPU bottleneck for dense scenes.
- **How it applies:** Enable GPU instancing on repeated-prop materials (trees/rocks/grass); combine with GPU Resident Drawer.
- **Citation:** /gpu-optimization.

### C3. Overdraw + occlusion culling
- **Finding:** "Use Occlusion culling to remove objects hidden behind foreground objects and reduce overdraw." (Classic CPU occlusion culling, distinct from the Unity 6 GPU Occlusion Culling in B2.)
- **Why it matters:** Transparent foliage/water especially inflate overdraw.
- **How it applies:** Bake occlusion culling for static world geometry; watch transparent-foliage overdraw in the Frame Debugger.
- **Citation:** /gpu-optimization.

### C4. GPU bandwidth / wavefront occupancy
- **Finding:** Draw call work splits into wavefronts distributed across SIMDs; "wavefront occupancy" = active wavefronts vs the maximum — a measure of how well the GPU is utilized.
- **Why it matters:** Diagnostic concept for deep GPU profiling on target hardware.
- **How it applies:** Advanced; use platform GPU tools (see F) if Far Horizon becomes GPU-bound.
- **Citation:** /gpu-optimization.

---

## D. Configuring your Unity project for stronger performance (project config & assets)

**Source guide:** https://unity.com/how-to/project-configuration-and-assets

### D1. Texture import settings
- **Finding:**
  - **Read/Write Enabled** creates a CPU+GPU copy = **doubles texture memory**; **disable** unless generating the texture at runtime.
  - **Mip Maps:** not needed for constant-onscreen-size textures (2D sprites, UI); **leave enabled for 3D models that vary in distance** from camera.
  - **Max Size:** "Lower the Max Size by using the minimum settings that produce visually acceptable results." Example: diffuse at 1024×1024 but roughness/metallic at 512×512 to cut bandwidth.
  - Texture compression formats need **power-of-two (POT)** dimensions.
  - **ASTC** is recommended for **mobile/XR/web** (better quality than ETC at similar memory) — **out of scope** for desktop-only Far Horizon (desktop uses DXT/BC formats).
- **Why it matters:** Low-poly art uses few/small textures; correct import settings keep memory + load tiny.
- **How it applies:** Disable Read/Write on all static textures; mip-map the world/character textures (distance-varying); keep Max Size minimal; POT-size textures. Desktop compression = BC7/BC1 (DXT), not ASTC.
- **Citation:** /project-configuration-and-assets.

### D2. Mesh import — Optimize Mesh Data
- **Finding:** "Optimize Mesh Data removes any data from meshes that is not required by the material applied to them (such as tangents, normals, colors, and UVs)." (Caution: it strips by material need — if a shader later needs normals/tangents, ensure they aren't stripped.)
- **Why it matters:** Shrinks mesh memory + vertex bandwidth.
- **How it applies:** Enable for shipping; but low-poly smooth-shaded meshes DEPEND on normals — verify normals survive (the project's unity-conventions.md already flags normals as load-bearing for the faceted/smooth look).
- **Citation:** /project-configuration-and-assets.

### D3. Quality settings / bandwidth
- **Finding:** Reducing the size of textures that need less detail reduces bandwidth (per D1 example). General theme: tune QualitySettings per target.
- **Why it matters:** Single desktop target simplifies this — one well-tuned quality tier.
- **How it applies:** Far Horizon can ship a single "Desktop" quality level tuned for the Zone-D look; avoid maintaining many tiers for the MVP.
- **Citation:** /project-configuration-and-assets.

---

## E. Scripting performance & C# best practices

**Source guides:** https://unity.com/how-to/best-practices-performance-optimization-unity, /advanced-programming-and-code-architecture, /programming-unity

### E1. Object pooling (built-in)
- **Finding:** "Rather than regularly instantiating and destroying GameObjects, use pools of preallocated objects." `Instantiate`/`Destroy` generate garbage + GC spikes. **Unity has a built-in pool: `UnityEngine.Pool`.**
- **Why it matters:** Survival loop spawns repeated objects (chopped-wood drops, particles, projectiles-if-any) — pooling avoids GC hitches.
- **How it applies:** Use `UnityEngine.Pool` (ObjectPool<T>) for any frequently spawned/despawned object in Far Horizon; don't hand-roll a pool.
- **Citation:** /best-practices-performance-optimization-unity; Unity Learn design-patterns object-pooling.

### E2. Cache results; avoid per-frame lookups in Update
- **Finding:** "Avoid calling [expensive lookups] in Update methods … cache the results." (Applies to `GetComponent`, `Camera.main`, `FindObjectOfType`, etc.) Don't allocate a `List`/collection every frame in `Update` — make it a MonoBehaviour member, init in `Start`, and `Clear()` it each frame instead of re-allocating.
- **Why it matters:** Per-frame allocations + lookups are the most common avoidable CPU + GC cost.
- **How it applies:** Cache component refs in `Awake`/`Start`; reuse collections; standard review-checklist item for Far Horizon C# PRs.
- **Citation:** /best-practices-performance-optimization-unity; ScriptableObject architecture guide.

### E3. Strip logging from builds
- **Finding:** "Log statements (especially in Update, LateUpdate, or FixedUpdate) can bog down performance. Disable your Log statements before making a build."
- **Why it matters:** `Debug.Log` is surprisingly expensive (string alloc + stack capture).
- **How it applies:** Gate debug logs behind a conditional / `[Conditional]` attribute or strip for the shipping `FarHorizon.exe`; the build-stamp HUD is fine (one-time).
- **Citation:** /best-practices-performance-optimization-unity.

### E4. Garbage collection model
- **Finding:** Unity's GC is **Boehm-Demers-Weiser** — it **stops your program** and resumes only when collection completes (stop-the-world → frame hitches). C# strings are reference types allocated on the managed heap even when temporary; avoid unnecessary string creation and avoid parsing string data files (JSON/XML) at runtime — prefer ScriptableObjects or binary formats (MessagePack/Protobuf).
- **Why it matters:** GC spikes = visible stutter, which the Sponsor will reject (no-debug rule).
- **How it applies:** Minimize per-frame allocations; load config/data via ScriptableObjects, not runtime JSON parsing; keep the survival-state save format binary or SO-based. (Unity 6 supports **incremental GC** to spread collection across frames — enable in Player Settings to reduce spike severity; verify on target.)
- **Version note:** Boehm GC is enduring; Incremental GC is a Player-Settings option (corroborate exact UI on the current version).
- **Citation:** /advanced-programming-and-code-architecture; Unity Learn Memory Management.

### E5. Structs vs classes (value vs reference types)
- **Finding:** "Set persistent objects as classes and ephemeral objects as structs, as structs are not allocated on the heap and thus not garbage-collected." In C# you otherwise "have no control over where/how your data is laid out in memory"; structs give layout control for hot paths.
- **Why it matters:** Choosing struct for small short-lived data avoids heap churn.
- **How it applies:** Use structs for transient math/data (e.g., per-frame compute results); classes for entities with identity/lifetime.
- **Citation:** /advanced-programming-and-code-architecture; /programming-unity.

### E6. Script lifecycle order
- **Finding:** "Every Unity script runs several event functions in a predetermined order" — understand Awake vs Start vs Update vs FixedUpdate vs LateUpdate.
- **Why it matters:** Init-order bugs (referencing not-yet-initialized objects) are a classic source of null/order issues.
- **How it applies:** Awake = self-init / cache own components; Start = cross-object refs; FixedUpdate = physics; LateUpdate = camera follow (orbit camera!). Far Horizon's mouse-orbit camera should follow in LateUpdate.
- **Citation:** /programming-unity (script lifecycle).

### E7. C# Job System + Burst (DOTS hot-path compilation)
- **Finding:** DOTS = the **C# Job System** (efficient multithreaded code) + **Burst Compiler** (highly optimized native code). Burst "takes a single method as input: the entry point to a hot loop" and compiles it + everything it invokes. Components/GameObjects are "heavy C++ objects" (C# wrappers over C++) — convenient but can be stored unstructured, costing cache/memory perf; DOTS/jobs address this.
- **Why it matters:** Only relevant if Far Horizon hits a CPU hot loop (large-scale simulation/AI/procedural gen).
- **How it applies:** **Likely out of scope** for the thin survival MVP. Candidate later: procedural island generation (radial heightmap — see project memory "big round island") could use Jobs+Burst if generation is slow. Note, don't pre-adopt.
- **Citation:** /programming-unity; Unity DOTS resources.

---

## F. Architecture — ScriptableObjects

**Source guides:** /architect-game-code-scriptable-objects, /scriptableobjects-event-channels-game-code, /get-started-with-scriptableobjects-demo

### F1. ScriptableObject for shared data & config
- **Finding:** "ScriptableObject is a serializable Unity class that allows you to store large quantities of shared data independent from script instances." Store static/unchanging values in a ScriptableObject (a project asset) instead of a MonoBehaviour — MonoBehaviours carry overhead (need a GameObject + Transform host); SOs reduce memory footprint and avoid copying values.
- **Why it matters:** Centralizes tuning data; designers can edit without code; survives scene loads.
- **How it applies:** Put survival-loop tuning (need decay rates, craft recipes, item stats, world-gen params) in ScriptableObjects; the Sponsor's "thin survival loop" config lives in SOs.
- **Citation:** /architect-game-code-scriptable-objects; community DI threads.

### F2. ScriptableObject event channels (observer pattern, decoupled)
- **Finding:** SO-based events implement the **observer pattern** — a centralized SO sits between publishers and subscribers ("like a radio tower"); any object can be publisher or subscriber. Gives the benefits of singletons "without introducing as many unnecessary dependencies."
- **Why it matters:** Decouples systems (inventory ↔ HUD ↔ crafting) without hard references or singletons → easier to test/iterate.
- **How it applies:** Use SO event channels for Far Horizon cross-system comms (e.g., "wood collected" → HUD + crafting listen); avoids the singleton-spaghetti the team should not inherit.
- **Citation:** /scriptableobjects-event-channels-game-code; "6 ways ScriptableObjects benefit your team" blog.

### F3. Move systems off MonoBehaviour into SO where possible
- **Finding:** "Take any system you implement in a MonoBehaviour and see if you can move the implementation into a ScriptableObject" — e.g., an `InventoryManager` on an SO maintains state across scene loads with no special init.
- **Why it matters:** Persistent state across scenes for free; parallel designer/dev work.
- **How it applies:** Inventory/survival-state as SO-backed where it must persist across scene transitions.
- **Citation:** /architect-game-code-scriptable-objects.

### F4. Team-velocity benefit
- **Finding:** Breaking shared data into small SO assets lets designers build gameplay **in parallel** with developers rather than waiting on code.
- **Why it matters:** Matches the orchestrator + named-agent parallel model.
- **How it applies:** SO-driven tuning lets Uma/Priya iterate values while Devon/Drew build systems — fewer merge collisions on monolithic config scripts.
- **Citation:** /architect-game-code-scriptable-objects.

---

## G. Profiling & debugging

**Source guides:** /best-practices-for-profiling-game-performance, /optimize-your-game-unity-profile-analyzer, /profiling-and-debugging-tools, /analyze-memory-usage-memory-profiling-tools

### G1. Profile early, often, and on target hardware
- **Finding:** Best gains come from planning profiling early — it's "an ongoing proactive and iterative process"; profiling early establishes a **performance signature** for the project. **Most accurate results come from profiling actual builds on target devices**, plus platform-specific tooling.
- **Why it matters:** Editor-profiler numbers diverge from shipped-build numbers (the project already knows editor-vs-runtime divergence is a proven failure class — spike iter6 "legs-up").
- **How it applies:** Profile the **built `FarHorizon.exe`** on a representative Windows machine, not just the editor; aligns with the project's shipped-build capture gate. Establish a baseline frame budget early.
- **Citation:** /best-practices-for-profiling-game-performance.

### G2. Top-down profiling method
- **Finding:** "Start with a top-to-bottom approach … begin with a high-level overview of categories such as rendering, scripts, physics, and garbage collection (GC) allocations, then drill down."
- **Why it matters:** Avoids rabbit-holing on a non-dominant cost.
- **How it applies:** When Far Horizon stutters, check the CPU/GPU/GC category split first, then drill.
- **Citation:** /best-practices-for-profiling-game-performance.

### G3. Profiler markers + Deep Profiling + Allocation call stacks
- **Finding:** The Unity Profiler is **instrumentation-based** — it times code wrapped in `ProfileMarkers` (MonoBehaviour Start/Update, specific API calls). For slow-but-unexplained methods, **add Profiler Markers** or use **Deep Profiling** (profiles begin/end of every function call → exact slow spot, but high overhead). **Allocation Call Stacks** (toggle "Call Stacks" in the Profiler toolbar) show exactly where managed allocations come from with **less overhead than deep profiling**.
- **Why it matters:** Precise attribution of CPU time and GC allocations.
- **How it applies:** Wrap Far Horizon's key systems (world-gen, survival tick, crafting) in custom `ProfilerMarker`s; use Allocation Call Stacks first (cheaper) before reaching for deep profiling.
- **Citation:** /best-practices-for-profiling-game-performance; /analyze-memory-usage-memory-profiling-tools.

### G4. Profile Analyzer (aggregate + compare)
- **Finding:** The standard Profiler does single-frame analysis; the **Profile Analyzer** aggregates/visualizes marker data across a **set of frames**. Its **Compare view** loads two datasets (shown in different colors) for side-by-side before/after analysis.
- **Why it matters:** Proves a fix actually helped across many frames, not one lucky frame.
- **How it applies:** Capture a profile pre-change and post-change; use Compare to validate optimizations (e.g., before/after enabling GPU Resident Drawer). Install the Profile Analyzer package.
- **Citation:** /optimize-your-game-unity-profile-analyzer.

### G5. Unity 6 profiling enhancements
- **Finding:** Unity 6 adds a **Profiler Highlights module** (shows optimization focus areas instantly) and an **improved Memory Profiler** giving accurate **resident memory** usage with a **detailed graphics-memory breakdown**.
- **Why it matters:** Faster identification of the dominant cost + truer memory picture.
- **How it applies:** Use the Highlights module as the first stop; use the Memory Profiler's resident/graphics breakdown to keep the desktop build's footprint sane.
- **Version note:** Unity 6 features.
- **Citation:** Unity 6 features blog https://unity.com/blog/unity-6-features-announcement; https://unity.com/blog/use-unity-6-profiling-tools-smart-efficient.

### G6. Native / platform tools
- **Finding:** Use platform-specific tooling to dig into hardware characteristics of each target (e.g., GPU vendor tools for the GPU pass).
- **Why it matters:** Engine profiler can't see all GPU-hardware detail.
- **How it applies:** On Windows desktop, pair the Unity GPU profiler with vendor tools (e.g., RenderDoc / GPU vendor profilers) if GPU-bound.
- **Citation:** /profiling-and-debugging-tools.

---

## H. UI performance (UGUI)

**Source guide:** https://unity.com/how-to/unity-ui-optimization-tips
- **Finding (from catalogue + general UGUI guidance surfaced):** The hub includes a dedicated "Unity UI performance optimization tips" guide. (Direct content not retrievable — 403; the guide canonically covers Canvas splitting: separate dynamic from static UI onto different Canvases so a single change doesn't dirty/rebuild the whole canvas, disabling Raycast Target on non-interactive graphics, and avoiding layout-group overuse — **flagged as standard UGUI guidance, not verbatim-quoted from this fetch**.)
- **Why it matters:** Far Horizon's HUD (build stamp, survival needs, inventory) is UGUI; canvas rebuilds are a common hidden cost.
- **How it applies:** Split the static HUD frame from frequently-updating elements (need bars, timers) onto separate canvases; disable Raycast Target on text/decorative images.
- **Citation:** /unity-ui-optimization-tips (catalogue entry; content not fetched — see gaps).

---

## I. Unity 6 platform/feature landscape (context for tech choices)

**Source:** https://unity.com/blog/unity-6-features-announcement
- **Finding:** Unity 6 headline pillars: end-to-end **Multiplayer** platform (Multiplayer Center hub + Multiplayer Widgets prebuilt UI for lobby/session/voice); **DOTS/ECS** (data-oriented, GameObject-compatible, determinism/scale); **Sentis** (runtime AI inference — deploy ML models in-runtime, auto-optimized, no local Python/cloud needed); **Muse** (Chat / Sprite / Texture / Behavior generative tools); the rendering features above (GPU Resident Drawer, Render Graph, GPU Occlusion Culling, STP, GPU Lightmapper, APV); improved profiling.
- **Why it matters:** Tells the team what's available vs what to skip for a single-player low-poly desktop survival MVP.
- **How it applies:** **In scope for Far Horizon:** rendering features (B/C), profiling (G), ScriptableObject architecture (F). **Out of scope (single-player desktop MVP):** Multiplayer platform, Sentis runtime AI, most DOTS/ECS (unless world-gen needs Burst). Muse is an asset-gen tool but the project's asset route is already locked to Blender + Hyper3D→Mixamo (project memory "in-house asset routes over paid tools") — don't re-propose Muse.
- **Version note:** Unity 6 announcement.
- **Citation:** https://unity.com/blog/unity-6-features-announcement.

---

## J. Cross-cutting "how it all applies to Far Horizon" (synthesis)

1. **URP Pipeline Asset config (one-time, high leverage):** enable Instanced Drawing (GPU Resident Drawer) + GPU Occlusion Culling; ensure SRP Batcher on; confirm Render Graph Compatibility Mode is OFF for shipping. (B1, B2, B3, C1)
2. **Lighting workflow:** bake static island lighting with the GPU Lightmapper; single directional sun shadow at modest resolution; no shadowed point lights; evaluate APV for GI. (B5, B6)
3. **Scene density:** LOD groups + GPU instancing on repeated low-poly props; rely on GPU Resident Drawer for the "big endless world." (B7, C2)
4. **Code architecture:** ScriptableObjects for all tuning/config + SO event channels for decoupled system comms; avoid singletons. (F1, F2, F4)
5. **Scripting hygiene (PR checklist):** cache lookups, no per-frame allocations, pool via `UnityEngine.Pool`, strip Debug.Log from builds, structs for ephemeral data, orbit camera in LateUpdate. (E1–E6)
6. **Profiling discipline:** profile the BUILT exe on target Windows hardware, top-down, custom ProfilerMarkers + Allocation Call Stacks, validate fixes with Profile Analyzer Compare. (G1–G5) — directly reinforces the project's existing shipped-build capture gate.
7. **Assets:** disable Read/Write, mip-map distance-varying textures, POT + minimal Max Size, Optimize Mesh Data (but PRESERVE normals for the low-poly smooth/faceted look). (D1, D2)

---

_End of extract. All findings sourced from WebSearch excerpts of unity.com how-to / blog pages (2026-06-16); direct page fetch blocked by 403. Items labeled "not verbatim-quoted" or "flagged as standard guidance" are corroborated best-practice, not direct page quotes — see gaps._
