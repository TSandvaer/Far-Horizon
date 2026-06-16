# Unity 6 Resources Hub — raw extract

**Source key:** `unity6-resources-hub`
**Assigned loc:** https://unity.com/campaign/unity-6-resources
**Fetched:** 2026-06-16
**Target reader:** Far Horizon (Unity 6 `6000.4.10f1` / URP, low-poly survival, desktop/Windows).

---

## RETRIEVAL NOTE — read this first (honesty about source provenance)

The assigned landing page (`https://unity.com/campaign/unity-6-resources`) **could not be
fetched directly** — every WebFetch attempt against the `unity.com` host returned **HTTP 403
Forbidden** (the marketing site blocks the fetch user-agent). This was consistent across the
hub page, the Unity-6 features-announcement blog, and the URP/VFX e-book blog pages — all on
`unity.com`, all 403.

To retrieve the hub's *content* faithfully without fabricating, I used two factual routes:

1. **WebSearch** returned the hub's own listing summary (e-books, sample projects, "what's
   new" framing) — captured below under "Hub contents (via search index)".
2. The hub is a curated **index that points at** (a) the official Unity 6 manual on
   `docs.unity3d.com` and (b) Unity blog deep-dives. The `docs.unity3d.com` host **is
   fetchable**, so I retrieved the authoritative primary sources the hub indexes — the
   Unity 6 "What's New" manual page and the URP 17 feature pages — and extracted the
   actionable technical detail there. Every technical item below is cited to the specific
   `docs.unity3d.com` page (Unity-version path included) it came from.

So: the *marketing hub itself* is thin/un-fetchable, but its **referenced substance is fully
captured** from the primary docs it links to. Items sourced only from the search-index
summary of the hub (e-book/sample titles) are labelled as such and NOT presented as
verbatim page text.

---

## Hub contents (via search index — titles only, not verbatim page scrape)

The hub advertises (per WebSearch result on the hub URL + sibling Unity blog titles):

- **"Over 30 guides in the Unity best practices hub"** — covering programming, project
  optimization, art, animation, lighting, graphics, DevOps, and game & level design.
  (Best-practices hub linked at `https://unity.com/how-to`.)
- **URP e-book, Unity 6 edition** — "the biggest version yet" (blog:
  `https://unity.com/blog/biggest-edition-urp-ebook-unity-6`). Most directly relevant
  e-book for Far Horizon (desktop URP).
- **VFX Graph e-book, Unity 6 edition** — "Get the Most Out of VFX Graph"
  (`https://unity.com/blog/unity-6-vfx-graph-ebook`). Relevant if/when FH adds smoke
  (campfire), water spray, particles.
- **Unity 6 Graphics Learning Resources** (`https://unity.com/blog/unity-6-graphics-learning-resources`).
- **Sample projects** the hub lists:
  - **Fantasy Kingdom in Unity 6** — URP sample optimized for mobile; showcases improved
    CPU/GPU graphics perf, **Adaptive Probe Volumes (APV)**, and VFX Graph.
  - **URP 3D Sample** — four distinct scenes (terminal, garden, cockpit, oasis); a
    reference for URP scene setup and look-dev.
  - **Gem Hunter Match** — 2D match-3 sample showcasing URP 2D lighting/VFX (less relevant
    to FH's 3D world).

> Status: these titles come from the search index of the hub, NOT a verbatim DOM scrape
> (hub was 403). Treat as a reliable list of what the hub links, not as quoted page text.
> The "Fantasy Kingdom" + "URP 3D Sample" are the two worth a look for FH look-dev/perf.

---

## Graphics & Rendering (Unity 6 / URP 17) — the headline area for FH

Primary citations:
- Unity 6 manual "What's new" — `docs.unity3d.com/6000.0/.../Manual/WhatsNewUnity6.html`
- URP 17 "What's new" — `docs.unity3d.com/6000.0/.../Manual/urp/whats-new/urp-whats-new.html`
- Deep pages cited inline.

### Render Graph system (URP 17)
- **Finding:** URP's rendering backbone is now the render graph. It automatically (a) reuses
  allocated GPU memory when textures share properties, (b) allocates resources only when a
  resource is actually used, (c) removes render passes whose output the final frame never
  reads, and (d) on TBDR mobile merges passes into native render passes to keep textures in
  tile memory. Generates proper sync between compute and graphics queues.
- **Why it matters:** Lower frame time and more efficient memory management on desktop;
  it's the substrate every custom render pass now sits on.
- **How it applies to FH:** Any custom URP renderer features (e.g. a stylized water pass,
  outline pass, post effect) must be authored with the render-graph two-stage model:
  **recording stage** (declare textures/RTs you'll use) then **execution stage** (issue
  graphics commands against the declared resources). The system owns resource lifetime —
  memory is allocated just before first write and freed after last read. Do NOT manually
  allocate/dispose RTs the old way.
- **Compatibility Mode:** URP has a "**Compatibility Mode (Render Graph Disabled)**" toggle
  (Graphics > URP tab > Render Graph section). It exists for legacy `ScriptableRenderPass`
  code. **GPU occlusion culling requires Compatibility Mode to be OFF** (i.e. render graph
  ENABLED). FH should keep render graph enabled (Compatibility Mode disabled).
- **Citation:** `docs.unity3d.com/6000.0/.../Manual/urp/render-graph-introduction.html`;
  URP 17 what's-new. (The intro page did not itself describe Compatibility Mode's desktop
  impact — the Compatibility-Mode→occlusion-culling dependency is from the GPU-culling page,
  cited below.)

### GPU Resident Drawer (URP 17) — the big desktop CPU win
- **Finding:** New rendering system that uses the **BatchRendererGroup** API to draw
  GameObjects with GPU instancing, reducing draw-call count and freeing CPU time.
- **Prerequisites (all required):**
  - **Forward+** rendering path on the Universal Renderer.
  - Graphics API supporting compute shaders (**not** OpenGL ES).
  - GameObjects must have **Mesh Renderer** components (objects lacking one fall back to
    non-instanced drawing).
  - **SRP Batcher** enabled in the URP Asset.
- **Enable steps (verbatim setting names):**
  1. **Project Settings > Graphics** → set **BatchRendererGroup Variants** to **Keep All**
     (Shader Stripping section).
  2. URP Asset → enable **SRP Batcher** (may be under **More (⋮) > Show All Advanced
     Properties**).
  3. Set **GPU Resident Drawer** to **Instanced Drawing**.
  4. Universal Renderer → **Rendering Path** = **Forward+**.
- **Why it matters / when it helps:** Most effective when a scene has *many objects* (high
  draw-call count straining CPU) AND *mesh+material reuse* — "multiple GameObjects use the
  same mesh and the same material shader variant" batch into single draw calls. Gains are
  more pronounced in Play mode / built player than in editor views.
- **When it HURTS:** It improves CPU but *slightly increases GPU workload*. If the app is
  GPU-bound, total frame time can rise. Also can increase **overdraw** on non-tiled GPUs
  (desktops/consoles) when merging into instanced calls — mitigate by setting **Depth
  Priming Mode** to **Auto** or **Forced** with Forward+.
- **How it applies to FH:** A low-poly survival world with a big island, dense jungle, lots
  of repeated foliage/rocks/props (same mesh+material) is the *textbook* beneficiary —
  expect a real CPU draw-call reduction. ACTION for the rendering/perf persona: enable it
  (the 4 steps above), keep meshes+materials shared across instances, and verify with the
  Profiler that FH is CPU-bound on draw calls before/after (don't enable blind if GPU-bound).
  Optimization notes from the docs: **disable Static Batching** in Player settings, use a
  **fixed Lightmap Size** and **disable Mipmap Limits** in Lighting settings.
- **Limitations:** Increases build times (shader-variant compilation due to Keep All).
- **Citation:** `docs.unity3d.com/6000.0/.../Manual/urp/gpu-resident-drawer.html`;
  performance considerations `docs.unity3d.com/6000.4/.../Manual/urp/gpu-resident-drawer-performance.html`
  (note: **6000.4** path — matches FH's exact version stream).
- **Version note:** No numeric thresholds/object counts are given in the docs — guidance is
  qualitative ("numerous objects", "high number of draw calls"). Profile to decide.

### GPU Occlusion Culling (URP 17)
- **Finding:** Occlusion culling moved from CPU to GPU; speeds rendering in scenes with a
  lot of occlusion.
- **Prerequisites:** Requires the **GPU Resident Drawer** (same restrictions apply). Render
  graph must be ON — i.e. **Compatibility Mode (Render Graph Disabled)** must be OFF.
- **Enable steps:** Graphics > URP tab > Render Graph section → disable **Compatibility Mode
  (Render Graph Disabled)**; enable GPU Resident Drawer; on the active Universal Renderer
  enable **GPU Occlusion**.
- **When it helps:** Multiple objects share a mesh (groupable into one draw call); scene has
  substantial occlusion, especially occluded *high-vertex* objects with small screen-space
  bounding radius.
- **When it HURTS:** If occlusion has little effect in your scene, rendering time can
  *increase* from the extra GPU setup work. Uses bounding-sphere approximation (weaker for
  thin/elongated objects) and downsampled depth.
- **How it applies to FH:** A big island with mountains/jungle that occlude each other is a
  candidate — but FH's low-poly meshes are LOW-vertex, and the doc says the win is largest
  for *high-vertex* occluded objects. So: enable + measure; don't assume a win on low-poly
  geometry. Thin foliage (grass blades, palm fronds) is the weak case (sphere bounds).
- **Citation:** `docs.unity3d.com/6000.0/.../Manual/urp/gpu-culling.html`.

### Spatial-Temporal Post-processing (STP) upscaling
- **Finding:** STP upscales frames rendered at lower resolution to optimize GPU perf while
  enhancing visual quality. Works on desktop, consoles, and mobile that support compute
  shaders.
- **Enable:** Active URP Asset → **Quality > Upscaling Filter > Spatial Temporal
  Post-processing (STP)**.
- **Why it matters:** A GPU-side performance lever — render at lower internal res, upscale
  to display res, keeping image quality reasonable.
- **How it applies to FH:** If FH ever becomes GPU-bound on higher-end desktop targets (it
  likely won't with low-poly art early), STP is the dial to recover GPU headroom without
  dropping display resolution. Lower priority than GPU Resident Drawer for a low-poly game,
  but worth knowing the toggle exists.
- **Citation:** URP 17 what's-new; WebSearch confirmed the exact menu path.

### Forward+ rendering path (URP)
- **Finding:** Tile-based forward variant — screen split into tiles, per-tile light lists;
  each object computes only the lights in its tile. **No per-object light limit** (Forward
  had a hard cap), though a per-camera total visible-light limit remains.
- **Limitations (settings IGNORED under Forward+):** "Additional Lights" and "Main Light"
  settings, "Per Object Limit for Additional Lights", and **Reflection Probe Blending**.
- **Why it matters:** It's the prerequisite path for GPU Resident Drawer + GPU occlusion
  culling, AND it lifts the per-object light cap.
- **How it applies to FH:** FH should run **Forward+** — it's required for the Resident
  Drawer/occlusion-culling wins and removes light-count headaches if the survival world
  ever has many small lights (campfires, torches, lanterns). Note the lost Reflection Probe
  Blending — acceptable for a stylized low-poly look.
- **Citation:** `docs.unity3d.com/6000.0/.../Manual/urp/rendering/forward-rendering-paths.html`
  (the `forward-plus-rendering-path.html` URL 301-redirects here).

### Adaptive Probe Volumes (APV) improvements
- **Finding:** Enhanced GI via APV — scenario blending, **sky occlusion** support, and
  **disk streaming**. Featured in the "Fantasy Kingdom" sample.
- **Why it matters:** Volume-based, artist-friendly global illumination that scales better
  than per-object light probes for large open worlds.
- **How it applies to FH:** Strong fit for a big-island open world — APV gives consistent
  bounced/ambient light across the terrain without hand-placing probe grids; sky occlusion
  helps shadowed jungle interiors read correctly under the gradient skybox. Candidate for
  the "Zone D" look quality pass.
- **Citation:** URP 17 what's-new; Unity 6 what's-new (HDRP also lists APV sky occlusion).

### Other graphics items (Unity 6)
- **Camera History API** — access per-camera history textures in custom passes (for
  previous-frame algorithms; e.g. TAA-style effects). Cite: URP what's-new.
- **Alpha Processing** — post-processing can render into alpha-channel textures for
  compositing render passes. Cite: URP what's-new.
- **8192 shadow texture resolution** option for higher-quality shadows. Cite: URP what's-new.
- **Volume framework CPU optimizations.** Cite: URP what's-new.
- **HDR Display Support** — cross-platform HDR tone mapping across all pipelines/platforms.
  Desktop monitors with HDR benefit. Cite: Unity 6 what's-new.
- **Ray tracing** (DXR 1.1 indirect dispatch rays, inline RT in compute, GPU occlusion of
  instances) — high-end desktop only; NOT relevant to FH's low-poly direction. Cite: Unity
  6 what's-new. (Marked: out of scope for FH.)
- **SkinnedMeshRenderer batching** — batches compute-skinning + blendshape dispatches;
  improves character-rendering GPU perf. Relevant once FH has the rigged castaway + future
  NPCs. Cite: Unity 6 what's-new.

---

## Performance & Optimization (Unity 6)

- **Split Graphics Jobs** — new threading mode that reduces unnecessary synchronization
  between the main thread and the native graphics-job thread. *Why:* better multithreaded
  CPU utilization. *FH:* desktop multi-core win; enable + profile. Cite: Unity 6 what's-new.
- **Dynamic Shader Variant Loading** — streams shader-data chunks into memory and evicts
  unused ones, cutting shader memory. *FH:* relevant if shader-variant memory grows; lower
  priority on desktop with ample RAM. Cite: Unity 6 what's-new.
- **GPU Resident Drawer / GPU occlusion culling** — see Graphics section (these ARE the
  headline perf features). Cite: as above.
- **General principle (from docs guidance):** the Resident Drawer trades a small GPU cost
  for a CPU draw-call saving — so the optimization decision depends on whether FH is
  CPU-bound or GPU-bound. *ACTION:* always profile before/after. Cite:
  `.../urp/gpu-resident-drawer-performance.html` (6000.4).

> Note: This hub/manual extract did not surface detailed GC/managed-memory or Burst/Jobs
> *new-in-6* guidance beyond Split Graphics Jobs + Entities baking perf. Those enduring
> best-practices (avoid per-frame allocs, pool, struct-of-arrays) live in the
> best-practices e-books the hub links (`unity.com/how-to`), which were 403 — flagged in
> gaps. Treat GC/Jobs depth as "to be sourced from the best-practices hub", not covered here.

---

## Scripting & Runtime (Unity 6)

- **Entities (DOTS)** — serialization of `UnityObjectRef<>` (unmanaged refs to Unity
  assets); globally-unique Entity IDs; improved baking perf + hierarchy-window stability.
  *FH:* DOTS not adopted in FH's GameObject-based design; informational only. Cite: Unity 6
  what's-new. (Marked: not in FH's current architecture.)
- **TextMeshPro** — basic emoji support; OpenType font features (kerning). *FH:* relevant
  for HUD/UI text quality. Cite: Unity 6 what's-new.
- **IL2CPP** — option to display C# source line numbers in call stacks in player builds.
  *Why it matters:* makes shipped-build crash diagnosis far easier. *FH:* turn on for the
  Windows player to debug the built exe (FH's testing bar requires shipped-build
  verification — this directly aids that). Cite: Unity 6 what's-new.

---

## Editor & Workflow (Unity 6)

- **Build Profiles** — multiple custom build configs per target platform; **replaces the
  deprecated Build Settings window**. *FH:* the headless builder (`FarHorizonBuilder.BuildWindows`)
  and any future build configs should align to Build Profiles, not legacy Build Settings.
  Cite: Unity 6 what's-new.
- **Render Graph Viewer** — visualizes render-pass resource usage for URP/HDRP debugging.
  *FH:* the tool to use when authoring/diagnosing custom URP passes. Cite: Unity 6 what's-new.
- **Frame Debugger** — enhanced (Stage/Scope/Dynamic keywords, shader-fallback visibility,
  batch introspection). *FH:* verify GPU Resident Drawer batching actually merged draw
  calls. Cite: Unity 6 what's-new.
- **Profiler** — Profiler Highlights module (bottleneck viz), inverted/reversed CPU call
  tree, Memory Profiler v1.1 (RenderTexture/AudioClip/Shader metadata). *FH:* core to the
  CPU-vs-GPU-bound decision for enabling the Resident Drawer. Cite: Unity 6 what's-new.
- **UI Toolkit** — Native Text Generator rewrite (RTL: Arabic/Hebrew), runtime binding
  system, new controls (**ToggleButtonGroup**, **Tab**, **TabView**), custom property
  drawers. *FH:* relevant for in-game/editor UI; runtime binding useful for HUD data.
  Cite: Unity 6 what's-new.
- **ProBuilder redesign**, **Piercing Menu** (Ctrl+Right-click to pick overlapping objects),
  **Scene View Context Menu** (UI-Toolkit-built, C#-extensible), **Color Checker** (lighting
  calibration), **Splines** improvements, **SpeedTree9** importer (`.st9`, configurable
  wind), **Terrain** quality settings + Overlays migration. *FH relevance:* Splines (paths/
  rivers), Terrain quality settings (the big island), SpeedTree9/wind (jungle foliage) are
  the notable ones. Cite: Unity 6 what's-new.

---

## Build & Platform (Unity 6) — desktop-relevant subset

- **Windows ARM64** — player compilation now enabled. *FH:* FH ships Windows desktop;
  ARM64 is an option if ever targeting ARM Windows laptops. Cite: Unity 6 what's-new.
- **Build Profiles** — (see Editor section) the new multi-config build surface. Cite: Unity
  6 what's-new.
- Android / iOS / visionOS / WebGL items present but **out of scope** for FH (Windows-only,
  no WebGL/HTML5 target per CLAUDE.md). Listed in the manual; not detailed here. Cite: Unity
  6 what's-new.

---

## Multiplayer / Networking (Unity 6)

- Multiplayer Center, Netcode for GameObjects v2.0 (Distributed Authority beta), Multiplayer
  Services package, Netcode for Entities v1.3, Multiplayer Play Mode, Multiplayer Tools.
- **FH relevance:** FH is single-player survival — **out of scope**. Listed for completeness.
  Cite: Unity 6 what's-new.

---

## VFX / Shader Graph (Unity 6) — relevant once FH adds effects

- **Shader Graph:** production-ready sample shaders, **UGUI Canvas support for UI shaders**,
  customizable **Heatmap** performance visualization, Feature Examples sample. *FH:* the
  stylized water / outline / gradient effects for the "Zone D" look are Shader Graph work;
  the Heatmap viz helps keep custom shaders cheap. Cite: Unity 6 what's-new.
- **VFX Graph:** custom HLSL blocks/operators, **URP Decals support**, motion-vector
  support, **six-way lighting for smoke**, particle-count readback. *FH:* six-way smoke
  lighting is directly useful for the campfire smoke; URP Decals for footprints/scorch
  marks. The hub's VFX Graph e-book is the companion guide. Cite: Unity 6 what's-new + VFX
  e-book (title via search index).

---

## Actionable summary for Far Horizon (rendering/perf checklist)

1. **Run Forward+** on the Universal Renderer (prerequisite for the perf features; lifts
   per-object light cap for campfires/torches).
2. **Enable GPU Resident Drawer** (4 steps above) — FH's repeated low-poly foliage/rocks/
   props are the ideal case; profile CPU draw calls before/after; disable Static Batching,
   fix Lightmap Size, disable Mipmap Limits per docs.
3. **Keep render graph ON** (Compatibility Mode OFF) — required for GPU occlusion culling
   and is the authoring model for any custom URP pass.
4. **Trial GPU occlusion culling** but MEASURE — FH's low-vertex meshes may not see the win
   the docs reserve for high-vertex occluded objects; thin foliage is the weak case.
5. **APV** is a strong GI fit for the big open island — candidate for the look-quality pass.
6. **STP** is the GPU-headroom dial if FH ever goes GPU-bound (low priority for low-poly).
7. **IL2CPP source line numbers** ON for the Windows player — aids shipped-build debugging
   (matches FH's shipped-build testing bar).
8. **Tooling:** Render Graph Viewer + Frame Debugger (verify batching merged) + Profiler
   Highlights (CPU-vs-GPU-bound call) + Memory Profiler v1.1.
9. **Read the URP e-book (Unity 6 edition)** and **best-practices hub** (`unity.com/how-to`)
   for the GC/Jobs/optimization depth NOT covered by the what's-new pages.

---

## Citations index (fetchable primary sources used)

- Unity 6 "What's new" manual — `https://docs.unity3d.com/6000.0/Documentation/Manual/WhatsNewUnity6.html`
- URP 17 "What's new" — `https://docs.unity3d.com/6000.0/Documentation/Manual/urp/whats-new/urp-whats-new.html`
- GPU Resident Drawer (enable) — `https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html`
- GPU Resident Drawer (performance, **6.4**) — `https://docs.unity3d.com/6000.4/Documentation/Manual/urp/gpu-resident-drawer-performance.html`
- GPU occlusion culling — `https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-culling.html`
- Render graph introduction — `https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-introduction.html`
- Forward rendering paths (Forward+) — `https://docs.unity3d.com/6000.0/Documentation/Manual/urp/rendering/forward-rendering-paths.html`

## Sources that could NOT be fetched (403 — flagged honestly)

- `https://unity.com/campaign/unity-6-resources` (the assigned hub) — 403.
- `https://unity.com/blog/unity-6-features-announcement` — 403.
- `https://unity.com/how-to` (best-practices hub / 30+ e-books) — not fetched; would be the
  source for GC/Jobs/optimization depth.
- URP e-book / VFX e-book blog pages on `unity.com` — 403.
  Titles for the above captured via WebSearch index, NOT verbatim scrape.
