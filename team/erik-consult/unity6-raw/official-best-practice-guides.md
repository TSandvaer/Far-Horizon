# Unity 6 / URP Best-Practice Extract — Official Best-Practice Guides

**Source key:** `official-best-practice-guides`
**Landing page fetched:** https://docs.unity3d.com/2022.3/Documentation/Manual/best-practice-guides.html
**Project relevance:** Far Horizon = Unity 6 (6000.4.10f1) + URP, **desktop-first (Windows PC)**, low-poly survival game.

> ## IMPORTANT — what this source actually is (read before trusting depth)
>
> The requested landing page (`best-practice-guides.html`) is **NOT a content page** — it is a curated **index of Unity's marketing e-books** (PDFs hosted on `unity.com/resources/...`). Those e-book URLs (`console-pc-game-performance-optimization-unity-6`, `best-practices-version-control-unity-6`, `introduction-to-urp-advanced-creators-unity-6`, etc.) are **gated downloads** — every direct fetch returned **HTTP 403 Forbidden** (they require a marketing-form submission / download wall that an unauthenticated fetch cannot pass). I could **not** retrieve the e-book PDFs themselves; see `gaps`.
>
> To deliver actionable, citable knowledge instead of nothing, I pivoted to the **canonical Unity Manual pages** that those e-books distill, which ARE openly fetchable. Critically: the 2022.3 manual best-practice/performance pages **301-redirect to the current Unity 6 manual** (`docs.unity3d.com/Manual/...`), so the content below is **Unity-6-current**, not stale 2022.3. Each item cites the specific manual page it came from. Where a page returned only an index (no leaf content), I drilled into the leaf pages and cite those.
>
> **Version flag:** Manual pages below are Unity 6 current (post-redirect). One GPU-culling page self-identified as "Unity 6.5" docs — flagged inline. The eval spike is on 6000.4.10f1; treat the GPU Resident Drawer / GPU occlusion culling / Render Graph guidance as **Unity-6-era features that exist in 6.4 but verify exact menu paths against the installed editor**.

---

## 1. Project organization & version control

### Smart Merge (UnityYAMLMerge) — semantic merge of scenes & prefabs
- **Finding:** Unity ships `UnityYAMLMerge` to merge `.unity` (scene) and `.prefab` files *semantically* (object-by-object) rather than as raw text. Configured under **Edit > Project Settings > Version Control**, with three modes: **Off**, **Premerge** (auto-accept clean merges; produce premerged file for conflicts), **Ask** (smart-merge with a conflict dialog — the default).
- **Why it matters:** Scene/prefab files are YAML and merge catastrophically with a plain text differ. For an orchestrator + named-agent team running parallel worktrees that all touch `Boot.unity`, semantic merge is the difference between mergeable PRs and constant scene-corruption.
- **Apply to Far Horizon:** Far Horizon already regenerates `Boot.unity` headlessly (`BootstrapProject.Run`), which sidesteps scene merges — but for hand-edited prefabs/scenes, wire UnityYAMLMerge into git so persona PRs that touch the same prefab don't clobber each other. Tool path (Windows): `C:\Program Files\Unity\Editor\Data\Tools\UnityYAMLMerge.exe`.
- **Concrete git config** (`.gitconfig` / repo `.git/config`):
  ```
  [merge]
  tool = unityyamlmerge
  [mergetool "unityyamlmerge"]
  trustExitCode = false
  cmd = '<path>/UnityYAMLMerge.exe' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
  ```
- **`mergerules.txt`** (in `Editor/Data/Tools`) tunes per-array/per-field merge behaviour and float-comparison epsilon (e.g. `float *.Transform.m_LocalPosition.x 0.0000005`). `--nomappinginoneline` keeps YAML multi-line consistent with Unity's serializer.
- **Citation:** Manual — *Smart Merge* (`docs.unity3d.com/Manual/SmartMerge.html`).

### Force Text serialization + Visible Meta Files (implied by Smart Merge requirement)
- **Finding:** For Smart Merge / diffable assets to work, assets must serialize as text. Set **Edit > Project Settings > Editor > Asset Serialization > Mode = Force Text**, and **Version Control > Mode = Visible Meta Files**.
- **Why it matters:** Binary-serialized assets cannot be merged or meaningfully diffed; hidden `.meta` files get orphaned in VCS.
- **Apply to Far Horizon:** Verify both are set (the project already relies on `.meta` files for empty dirs per CLAUDE.md — "Empty dirs carry `.meta` files" — which only works under Visible Meta Files). **Version-specific note:** the *Force Text / Visible Meta Files* settings paths are inferred from the Smart Merge page's prerequisites, not quoted verbatim from a dedicated page in this fetch — verify menu wording against the installed 6000.4.10f1 editor.
- **Citation:** Manual — *Smart Merge* (prerequisites); flagged as inferred.

### `.gitignore` for Unity projects
- **Finding:** Standard Unity VCS practice ignores generated/local dirs. Far Horizon's `CLAUDE.md` already documents ignoring `*.log`, `test-results*.xml`, `Captures/`, `Build/`.
- **Apply to Far Horizon:** Beyond the project's list, the canonical Unity ignore set also covers `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln` (regenerated by the editor). CLAUDE.md's note that CI must upload artifacts before cleanup (because `Build/` is ignored) is the correct pattern.
- **Citation:** Project `CLAUDE.md` (`.gitignore note`) cross-referenced with standard Unity VCS guidance; the gated e-book *Best practices for project organization and version control (Unity 6)* (`unity.com/resources/best-practices-version-control-unity-6`, **403 — not retrieved**) is the authoritative deep source.

---

## 2. Performance analysis & profiling (the entry discipline)

- **Finding:** Unity's performance section (`Manual/analysis.html`, redirected from the old best-practice URL) organises optimization into: **Memory in Unity**, **Unity Profiler**, **Graphics performance and profiling**, **Runtime performance scaling**, **Project Auditor**, **Profiling tools reference**.
- **Profile-first rule:** Before optimizing, profile to determine whether you are **CPU-bound or GPU-bound** — "strategies for fixing these problems are quite different." Always **profile on a development build on the target device**, NOT in the editor — "The Unity Editor works in a different way to a build."
- **Why it matters:** Optimizing the wrong bottleneck wastes cycles; editor profiling lies about real-build perf (this exactly matches Far Horizon's hard-won "editor-vs-runtime divergence is a proven failure class" / shipped-build capture gate).
- **Apply to Far Horizon:** The project's **shipped-build capture gate** is the correct embodiment of "profile/verify on the built exe, not the editor." Extend it: take Profiler + Frame Debugger captures from a **Development Build** of `FarHorizon.exe`, not the editor, when investigating perf. **Project Auditor** is a Unity package that statically flags perf issues — a candidate CI add-on.
- **Tools called out:** Unity Profiler (CPU/GPU/Memory modules), **Memory Profiler** (separate package), **Profile Analyzer** (compare captures), **Frame Debugger** (per-draw-call inspection), **Project Auditor** (static analysis).
- **Citation:** Manual — *Analyzing your app* (`docs.unity3d.com/Manual/analysis.html`); *Reduce rendering work* (`OptimizingGraphicsPerformance.html`); *Track GC allocations* (`performance-track-garbage-collection.html`).

---

## 3. Memory & garbage collection

### Managed memory model
- **Finding:** Unity manages C# memory automatically via a garbage collector; you don't manually free managed memory. Memory splits into **Managed memory** (GC-controlled heap), **Native memory** (Unity's C++ layer), and **C# unmanaged memory** (native containers you control directly, e.g. for jobs/Burst).
- **Why it matters:** GC pauses cause frame-time spikes; understanding which bucket an allocation lands in tells you whether GC pressure or native asset memory is your problem.
- **Citation:** Manual — *Memory in Unity* (`performance-memory.html`).

### Incremental Garbage Collection (key Unity desktop knob)
- **Finding:** Unity uses the **Boehm-Demers-Weiser** GC. **Incremental GC** (default-ON) spreads collection across multiple frames in small time-slices instead of one **stop-the-world** pause. Stop-the-world mode halts the main CPU thread until the entire managed heap is processed — potentially "hundreds of milliseconds." Incremental mode shows as a small per-frame slice ("darker green fringe") in the Profiler.
- **Enable:** **Project Settings > Player > Configuration > Use incremental GC** (on by default). Runtime control via `GarbageCollector.GCMode` API.
- **Trade-off:** Incremental GC adds **write barriers** (overhead when object references change). If references change too frequently, the collector can **fall back to a full non-incremental collection** — so very allocation/reference-churny code partially defeats it.
- **Why it matters / apply to Far Horizon:** For a 60 fps desktop survival game, incremental GC keeps frame-time smooth (steady 60 vs periodic drops). Keep it ON; but the real win is **reducing allocations** so the GC rarely runs at all (next item).
- **Citation:** Manual — *Incremental garbage collection* (`performance-incremental-garbage-collection.html`); *Garbage collector* overview (`performance-managed-memory.html`).

### Tracking & reducing GC allocations
- **Finding:** Diagnose allocations via the **CPU Usage Profiler > GC.Alloc column** (bytes allocated per frame) and the **Memory Profiler module > "GC allocated in frame"**. Enable **Call Stacks mode** to get full call-stack traces for each `GC.Alloc` sample and pinpoint the exact line.
- **Goal:** Drive **per-frame managed allocations toward zero** in hot paths (Update/FixedUpdate/per-frame gameplay).
- **Apply to Far Horizon:** Watch the GC.Alloc column on a dev build during the survival loop (move, chop, campfire). Common culprits to audit in persona code reviews: per-frame `string` concatenation, boxing value types, LINQ in Update, allocating arrays from APIs like `GetComponents`/`Physics.RaycastAll`/`Mesh.vertices`, closures/lambdas captured per-frame. (Note: this specific *enumerated culprit list* lives in the gated e-book / `performance-gc-avoid-reflection.html`; the manual page I fetched documents the **tracking method**, not the full culprit list — see `gaps`.)
- **Citation:** Manual — *Track GC allocations* (`performance-track-garbage-collection.html`). Culprit enumeration: partially un-retrieved (gated e-book).

### Reflection overhead
- **Finding:** Unity calls out **C# reflection** as a specific allocation/perf source with a dedicated page (`performance-gc-avoid-reflection.html`) — avoid runtime reflection in hot paths; cache reflection results.
- **Citation:** Manual — *Managed memory* index links *Avoid C# reflection overhead*.

---

## 4. Draw-call reduction & batching (the highest-leverage URP perf area)

### Method comparison table (Unity 6, URP-relevant)
| Method | What it does | Best for | Key limitation |
|---|---|---|---|
| **SRP Batcher** | Reduces render-state (GPU setup) updates between draws | **URP/HDRP — enable it** | Needs compatible shaders; DX12/Vulkan benefit most |
| **GPU Resident Drawer** | Auto GPU instancing via BatchRendererGroup | **URP primary choice for many objects** | Forward+/Deferred+ only; compute-shader platforms; not Built-In |
| **BatchRendererGroup API** | Low-level GPU hardware instancing | Advanced/custom only | GPU Resident Drawer preferred instead |
| **GPU Instancing (material checkbox)** | Hardware instancing of same mesh+material | Built-In Pipeline w/ many instances | Creates extra shader variants |
| **Static Batching** | Combines static meshes into shared buffers | Built-In Pipeline | **Incompatible with GPU Resident Drawer / BRG** |
| **Dynamic Batching** | CPU-combines small meshes per-frame | — | **Deprecated — do not use** |

- **URP recommendation (verbatim intent):** **Enable SRP Batcher + GPU Resident Drawer; DISABLE the per-material GPU Instancing checkbox** (it would only add redundant shader variants).
- **Priority order when several are enabled:** static meshes → static batching; dynamic meshes with compatible shaders → SRP Batcher (+ GPU Resident Drawer / BRG); remaining compatible → GPU Instancing; leftover → dynamic batching.
- **Citation:** Manual — *Choose a method for optimizing draw calls* (`optimizing-draw-calls-choose-method.html`).

### Static batching numbers
- **Finding:** Static batching combines static meshes into world-space vertex/index buffers; **each buffer holds up to 64,000 vertices** (Unity creates multiple batches as needed). Can run at build time or runtime (`StaticBatchingUtility`). Mark objects with the **Batching Static** flag.
- **Apply to Far Horizon:** For the static island world (rocks, trees, terrain props) under URP, prefer **GPU Resident Drawer** over static batching (they are mutually exclusive, and GPU Resident Drawer is the URP-recommended path). Reserve static batching for Built-In only.
- **Citation:** Manual — *Static and dynamic batching* (`DrawCallBatching.html`).

### Dynamic batching numbers (deprecated)
- **Finding:** Dynamic batching caps: **≤ 300 vertices and ≤ 900 vertex attributes per mesh**, transforms verts to world space on the CPU per frame. Limitations: not in HDRP; needs matching lightmap textures/UVs for lit objects; first pass only of multi-pass shaders; can't batch mixed negative/positive scale. **Officially deprecated** — use other methods.
- **Apply to Far Horizon:** Do not rely on dynamic batching; it's a legacy fallback.
- **Citation:** Manual — *Static and dynamic batching* (`DrawCallBatching.html`).

---

## 5. URP & rendering performance

### GPU Resident Drawer (Unity 6 headline feature)
- **Finding:** Auto-applies GPU instancing to GameObjects via BatchRendererGroup → fewer draw calls + lower CPU overhead. **Requirements:**
  - **Rendering path: Forward+ or Deferred+ only.**
  - Compute-shader-capable platform (excludes OpenGL ES, VisionOS) — fine for Windows desktop.
  - **Project Settings > Graphics > Shader Stripping > BatchRendererGroup Variants = "Keep All".**
  - **SRP Batcher enabled** in the URP Asset; **GPU Resident Drawer = "Instanced Drawing".**
  - GameObject constraints: Mesh Renderer **without MaterialPropertyBlocks**; no custom `sortingLayerID`/`sortingOrder`; **≤ 128 materials per GameObject**; no Text Mesh / no `OnWillRenderObject`/`OnBecameVisible`/`OnBecameInvisible` MonoBehaviour render callbacks; Light Probes can't use Proxy Volume / Anchor Override. **Realtime Enlighten GI must be disabled.**
  - Limitations: **LOD animated cross-fade unsupported** (falls back to distance-based); `Light.shadowMatrixOverride` doesn't affect shadow-caster culling.
- **Why it matters / apply to Far Horizon:** A big endless island with many repeated meshes (trees, rocks, grass clumps) is exactly the GPU Resident Drawer's sweet spot. **This dictates a rendering-path decision: choose Forward+** to unlock it. Audit that world props use plain Mesh Renderers without MaterialPropertyBlocks so they qualify.
- **Citation:** Manual — *GPU Resident Drawer* (`urp/gpu-resident-drawer.html`).

### GPU Occlusion Culling (companion to GPU Resident Drawer)
- **Finding:** Uses the GPU (not CPU) to cull objects hidden behind others. **Requires GPU Resident Drawer enabled first**; then toggle **GPU Occlusion** in the active Universal Renderer. Conservative method: objects approximated by **bounding spheres** + a **downsampled depth buffer** at multiple resolution levels. Most effective when: many objects share a mesh, scene has significant occlusion with high-vertex occluded objects, and occluded objects have small screen-space radii. **For scenes with little occlusion, GPU overhead can outweigh the benefit** (can increase render time).
- **Apply to Far Horizon:** A dense jungle/island with mountains is a strong occlusion candidate — but **A/B test it on the built exe**: enable, capture, compare frame time. Don't assume it helps a sparse beach. (Page self-identified as Unity **6.5** docs — feature exists in 6.x; verify toggle in 6000.4.10f1.)
- **Citation:** Manual — *GPU occlusion culling* (`urp/gpu-culling.html`, labelled Unity 6.5).

### General URP perf levers (from "Understand performance in URP")
- **Finding / concrete levers:**
  - **Disable HDR** if not needed → smaller color buffer (memory + bandwidth).
  - **Disable URP volume-per-frame updates** when volumes aren't changing → CPU saving.
  - **Disable the Opaque Texture** and **additional-light shadows** when unused → fewer render passes.
  - **Enable SRP Batcher** → less GPU submission overhead.
  - **Reduce render scale** → fewer pixels (mobile-focused, but applies to scaling on weaker PCs).
- **Apply to Far Horizon:** For the Zone-D look (bloom/grading/fog/skybox), HDR is likely *wanted* for bloom quality — keep it but be deliberate. Turn off Opaque Texture / extra-light shadows unless a shader needs them.
- **Citation:** Manual — *Understand performance in URP* (`urp/understand-performance.html`). **Note:** this manual page is thin on Forward vs Forward+ vs Deferred *numeric* trade-offs; the gated e-book *Introduction to URP for advanced creators (Unity 6)* (`unity.com/resources/introduction-to-urp-advanced-creators-unity-6`, **403 — not retrieved**) is the deep source — see `gaps`.

### Reduce rendering work (CPU-bound vs GPU-bound)
- **CPU-bound fixes:** fewer draw calls (skybox for distant geo, occlusion culling, reduce camera far-clip + per-layer cull distances); **bake lighting/shadows** via lightmapping where static; in Forward, **limit per-pixel real-time lights**; use real-time shadows **sparingly**; optimize Reflection Probes; batch efficiently.
- **GPU-bound fixes:**
  - *Fill rate / overdraw:* reduce overlapping transparent layers (UI, particles); use the editor **Overdraw draw-mode** to find hotspots; cheaper fragment shaders (Mobile/Unlit categories); Dynamic Resolution to scale render targets.
  - *Memory bandwidth:* enable **mipmaps** for textures viewed at varying distance; use proper **texture compression** formats.
  - *Vertex processing:* cheaper vertex shaders; **minimize triangle count + UV seams/hard edges** (each adds vertices); implement **LOD**.
- **`OnDemandRendering` API:** throttle frame rate for static content (menus) to save power without stopping scripts.
- **Apply to Far Horizon:** Low-poly faceted meshes already minimize triangles — but **hard edges / faceted normals split vertices**, so faceted low-poly is *more* vertices than its tri-count implies; keep meshes genuinely low-poly. Bake lighting where the world is static; keep real-time lights/shadows few (one sun + baked GI fits "soft gradient lighting"). Use the Overdraw view to check fog/transparency/particle overdraw for the campfire & water.
- **Citation:** Manual — *Reduce rendering work on the GPU or CPU* (`OptimizingGraphicsPerformance.html`).

### LOD & mesh data
- **Finding:** Unity provides **LOD** (`lod-landing.html`) to cut GPU work on distant meshes, **mesh data compression** (`compressing-mesh-data-optimization.html`) to shrink memory, and **async texture/mesh loading** (`loading-texture-mesh-data-asynchronously.html`).
- **Apply to Far Horizon:** For an "endless" island, LOD on trees/rocks is high-value; async loading supports streaming distant terrain without frame hitches. (Note: GPU Resident Drawer's LOD cross-fade limitation above interacts here — distance-based LOD switching only.)
- **Citation:** Manual — *Graphics performance and profiling* index (`graphics-performance-profiling.html`).

---

## 6. Runtime performance scaling

- **Finding:** Unity frames runtime scaling as "balance visual quality with performance by dynamically scaling settings as the app runs." Primary system documented: **Adaptive Performance** (adjusts quality from real-time thermal/power state) — **mobile/thermal-oriented**, less relevant to desktop.
- **Apply to Far Horizon (desktop):** Adaptive Performance is thermal-focused (mobile/XR); for a Windows desktop game the practical levers are **Quality Settings tiers**, **render scale**, **`Application.targetFrameRate`** and **`QualitySettings.vSyncCount`**, and **Dynamic Resolution** for weaker GPUs. **Note:** the manual *runtime-performance-scaling* page is an index/navigation hub and does **not** give concrete `targetFrameRate`/`vSyncCount`/dynamic-resolution code — those specifics were not in this fetch (see `gaps`).
- **Citation:** Manual — *Runtime performance scaling* (`runtime-performance-scaling.html`) — index only.

---

## 7. Scripting & architecture (index-level — deep content is in gated e-books)

- **Finding:** The landing index links five scripting guides, all **gated e-books** (not fetchable): *C# style guide: write cleaner code that scales*; *Level up your code with game programming patterns*; *Create modular game architecture with ScriptableObjects*; *Introduction to DOTS for advanced Unity developers*; *Ultimate guide to advanced multiplayer networking*.
- **Why it matters / apply to Far Horizon:** The **ScriptableObject modular-architecture** guide is the most relevant to a single-player survival game — ScriptableObject-based data (item/recipe/need definitions) + event channels decouple systems and survive domain reloads cleanly. The **game programming patterns** guide (object pooling, state machines, command, observer) maps directly to the survival loop (pooling for chopped-wood/particles, state machine for player + campfire). DOTS/multiplayer are **out of scope** for this single-player desktop game.
- **Citation:** Index page only; e-book bodies **403 — not retrieved**. Authoritative URLs: `unity.com/resources/create-modular-game-architecture-with-scriptable-objects-ebook`, `unity.com/resources/level-up-your-code-with-game-programming-patterns`, `unity.com/resources/create-code-c-sharp-style-guide-e-book`.

---

## 8. Testing & build (cross-reference)

- The landing index does **not** include a dedicated testing/build e-book; Unity's testing guidance lives in the **Unity Test Framework** manual (EditMode/PlayMode, NUnit) — outside this source. Far Horizon's `team/TESTING_BAR.md` + paired EditMode/PlayMode + shipped-build capture gate already encode the right bar; this source adds the **"profile/verify on a Development Build of the exe, not the editor"** principle (§2), which reinforces the existing capture gate.
- **Citation:** Cross-reference — not from this source's pages directly.

---

## Net actionable picks for Far Horizon (priority-ordered)
1. **Rendering path = Forward+** to unlock **GPU Resident Drawer** (auto-instancing for the repeated island props) — biggest CPU draw-call win. (§4, §5)
2. **Enable SRP Batcher + GPU Resident Drawer; disable per-material GPU Instancing checkbox**; set BatchRendererGroup Variants = Keep All. (§4)
3. **Keep Incremental GC on; drive per-frame GC.Alloc to ~0** in the survival-loop hot paths; verify via Profiler GC.Alloc column on a **dev build of the exe**. (§3)
4. **Bake lighting** where the world is static; keep real-time lights/shadows minimal (fits the soft-gradient look). (§5)
5. **A/B test GPU Occlusion Culling on the built exe** for the dense jungle/mountain scenes — don't assume; measure. (§5)
6. **LOD on trees/rocks** for the "endless" island; mind that GPU Resident Drawer forces distance-based (not cross-fade) LOD. (§5)
7. **Wire UnityYAMLMerge** into git for hand-edited prefabs/scenes; confirm Force Text + Visible Meta Files. (§1)
8. Adopt **ScriptableObject-based data + event channels** for items/recipes/needs; **object pooling + state machines** for the survival loop. (§7)
