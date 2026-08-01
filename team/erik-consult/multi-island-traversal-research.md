# Multi-Island Traversal — Engine Capability Research

## Question

Ticket `86caa9zju` (the JOURNEY-half: boat + sail from the start island to a second, much
bigger island) is `sponsor-gate` / deferred, awaiting its own `/grill-me` now that the sibling
DESTINATION POC (`86caa9zpp`, the big single-island terrain-gen) has landed and soaked
(PR #226 merged 2026-07-02, PR #278 merged 2026-07-07). Before that grill, Priya/the Sponsor
need an engine-capability read (not a design spec) on: what breaks first when the playable
world goes from one bounded island to island + open water + second island; which Unity 6
streaming mechanism (if any) fits; whether the existing water shader survives being crossed
for minutes; and the smallest POC that would produce a real answer rather than a demo.

## Bottom line

**Nothing measured today is close to a performance ceiling** — the existing ~1200u single
island runs at 8x frame-time headroom (488fps uncapped vs the 60fps/16.67ms budget). **What
actually breaks first is the world-authoring model**, not perf: both existing terrain
generators compute their radial shore/height fields directly from **world origin** (not a
per-island center parameter), and each existing "island" scene ships its own full-extent
"all-sides ocean" plane — putting two islands in one world today would either need new
center-offset plumbing or would produce two overlapping/duplicated ocean meshes. The
GPU-Resident-Drawer-vs-Static-Batching reconciliation flag in `elite-techniques.md` is **NOT
forced open by scale** — its root cause (the scatter uses procedurally-unique per-instance
meshes, which GPU Resident Drawer cannot instance regardless of world size) is independent of
how many islands exist; multi-island scale raises the *cost* of leaving it unresolved (more
statically-batched duplicate geometry) without changing whether it's *forced*. **Addressables
and a Sebastian-Lague-style chunk-LOD system are both overkill for a fixed two-island world**
— the project's own doc already frames chunk-LOD's trigger as "the world stops fitting in one
hand-built scene," and two named islands with bounded diameters haven't crossed that line even
at 3x today's measured size. The **depth-fade transparent water shader has already landed**
(`LowPolyWater.shader`, confirmed live on `origin/main`-lagging local tree, used for both ocean
and pond) and is architecturally traversal-safe (continuous world-space waves, no UV tiling
seam) — the open question is a perf **re-measurement** at the new extent, not a rebuild. The
smallest de-risking POC is to place the *already-built* destination island at a real offset
from the start island in one shared scene, dedupe the ocean, and re-run the *already-existing*
perf/NavMesh instrumentation — before any boat-control-feel work, which the ticket itself
gates separately.

---

## Evidence

### 1. What breaks first

- **In-repo, directly read from source (Strong — verified against this session's local
  worktree; NOT independently cross-checked against `origin/main`, see Application section for
  the staleness caveat).** `NextIslandPocGen.ShoreRadiusAt` computes the warped coast radius
  from `Mathf.Atan2(wz, wx)` and `HeightAtRadial`/`MountainHeightAt`/`ColorAt` all key off
  `r = sqrt(wx²+wz²)` — i.e. distance from **world origin (0,0)**, not from a per-island center
  parameter. `LowPolyZoneGen`'s start-island generator uses the identical origin-centered
  idiom (`IslandCoreR`/`IslandGridHalf` constants, no center offset argument anywhere in its
  public API). Neither generator accepts an island-center parameter today.
  (`c:/Trunk/PRIVATE/Far-Horizon/Assets/Scripts/Editor/NextIslandPocGen.cs:247-258`,
  `c:/Trunk/PRIVATE/Far-Horizon/Assets/Scripts/Editor/LowPolyZoneGen.cs:236,247`.)
- **In-repo, directly read (Strong, same caveat).** `NextIslandPocGen.BuildWater` builds a
  square "all-sides sea" plane centred on **its own island's local root** at `halfExtent=1900u`
  (a ~3800u² plane) so the coast dissolves into fog on every side
  (`NextIslandPocGen.cs:685-706`). The start island's `LowPolyZoneGen.BuildIslandWater` does the
  analogous thing at its own (smaller) scale. **Consequence:** simply translating a second
  island's root GameObject to a real-world offset (which Unity's local-space mesh authoring
  *does* support cheaply — the vertices themselves stay local, only the root transform moves)
  would carry a full second copy of a 3800u²-class ocean plane along with it, overlapping the
  first island's own full-extent ocean in the gap between them — two coincident water surfaces
  at the same `WaterY`, a correctness/z-fighting problem, not a perf one. This is the
  architectural fact that "breaks first": today's code was built and measured as two
  **stand-alone, single-island worlds** (confirmed: `NextIslandPocScene.Build()` registers its
  scene as the **ONLY** enabled build scene, overriding `Boot.unity` rather than coexisting with
  it — `NextIslandPocScene.cs:145-147`), not as a shared multi-island world. Reconciling this is
  real, scoped engineering (an island-center parameter + one shared ocean authored across both
  landmasses), but it is small compared to a perf rewrite.
- **In-repo, measured (Strong for what it measures — single dev machine, single GPU, one
  fixed scene, explicitly bounded by the author's own doc).**
  `team/analysis/2026-07-07-island2-c4-perf.md` (Devon, PR #278, 2026-07-07): the fully-populated
  ~1200u-diameter single island (825 broadleaf + 435 pine trees ×2 renderers each, 210 bushes,
  1449 grass clumps, 252 shadow-casting rocks, ≈4.45k total renderers) held the shipped release
  exe at the 60fps vSync cap with **58fps as the single worst frame during a 12s traversal/climb
  window**; the uncapped development build measured **2.05ms/frame avg = 488fps**, an ~8x margin
  under the 16.67ms/60fps budget; GPU frame time 0.21ms (GPU "trivially loaded"); 56 avg draw
  calls; 0.44KB/frame GC; shadow-caster draws 35 avg/42 max = 2.7% of the CPU frame (vegetation
  is `castShadows:false` by policy). The doc's own explicit bounds: **"NOT tested: weak iGPU / a
  second machine... a much LARGER island... do not free-grow past ~1200u without re-running."**
  This means: whatever ceiling exists is well past what's been reached so far, but the specific
  number for "island + open water gap + second island" is **not yet measured — say so rather
  than extrapolate it.**
- **Reasoned inference from Strong general facts (label: Inference, not measured for this
  scenario).** Static batching (confirmed live via `StaticEditorFlags.BatchingStatic` on scatter
  props in both `WorldBootstrap.cs:611-652` and `NextIslandPocScatter.cs:668-669`) works by
  Unity combining each batched group's separate mesh vertex data into one new **combined** mesh
  per batch — memory cost is roughly proportional to the sum of each instance's own vertex data,
  not shared. GPU Resident Drawer / `BatchRendererGroup`-based instancing (Unity official docs,
  see below) instead references one shared source mesh per unique asset plus small per-instance
  transform data — memory cost does not multiply with instance count the same way. **At 2x-3x
  the renderer count (a second, bigger island), static batching's memory duplication cost grows
  roughly linearly while an instanced approach's would not** — this is a reasoned consequence of
  the two techniques' documented mechanisms, not a number anyone has measured on this project.
  If RAM (not frame time) becomes the actual first wall at multi-island scale, this is the
  lever to reach for — but it hasn't been shown to be the wall yet.
- **NavMesh bake cost at scale — labelled Hypothesis, not measured.** The POC's
  `NavMeshSurface.collectObjects = CollectObjects.All` bakes over the combined bounds of every
  collider in the scene (`NextIslandPocScene.cs:215-245`, voxel size 0.22). Two islands separated
  by a real open-water gap would plausibly voxelize a much larger combined AABB even though the
  water gap itself contributes no walkable surface — this is a real candidate cost, but it has
  not been measured and should not be cited as a known number. It is cheap to test directly in
  the smallest POC (§4).

### 2. GPU Resident Drawer vs. Static Batching — does multi-island force the flag open?

- **Source — Unity official docs, "GPU Resident Drawer performance considerations"**
  [docs.unity3d.com/6000.4/Documentation/Manual/urp/gpu-resident-drawer-performance.html]
  — **Strong** (official Unity 6 manual, version-matched to this project's `6000.4.11f1`).
  Verbatim instruction under "Ways to speed up the GPU resident drawer": *"Go to Project
  Settings > Player. In the Other Settings section, disable Static Batching."* The page frames
  this as a performance recommendation for GRD, not as a hard error/incompatibility — the two
  can technically coexist, but Unity's own guidance is to turn Static Batching off to get GRD's
  benefit.
- **Source — Unity official docs, "Enable the GPU Resident Drawer in URP"**
  [docs.unity3d.com/6000.4/Documentation/Manual/urp/gpu-resident-drawer.html] — **Strong**
  (official manual, same version). Requirements confirmed: GameObjects must use materials that
  support `BatchRendererGroup`, a GameObject/renderer is excluded past 128 materials, and the
  GPU must support compute shaders (excludes OpenGL ES and VisionOS — irrelevant here, this
  project is Windows desktop / IL2CPP-or-Mono only). The fetched page did not state a per-instance
  unique-mesh restriction explicitly — that restriction is this project's own documented finding
  (`unity6-mastery.md` §1: *"GRD can't instance the world's unique per-instance meshes"*),
  carried here as project-internal, not re-verified against a fresh official source this session.
- **In-repo confirmed current state (Strong).** `Assets/Settings/FarHorizonURP.asset:86` reads
  `m_GPUResidentDrawerMode: 0` — GPU Resident Drawer is OFF today; the SRP Batcher
  (`m_UseSRPBatcher: 1`) is the live batching path. Independently corroborated on `origin/main`
  @ `1f2f3c8` from three separate artifacts during peer review of this note:
  `Assets/Settings/FarHorizonURP.asset:86` (`m_GPUResidentDrawerMode: 0`),
  `.claude/docs/unity6-mastery.md:13` ("the shipped config runs plain Forward + GPU Resident
  Drawer OFF"), and `team/analysis/2026-07-07-island2-c4-perf.md:91` ("GPU Resident Drawer /
  Forward+ (still off — not needed at this load)").
- **Conclusion (Erik's synthesis, not a citation).** The multi-island case does **not**
  mechanically force the GRD-vs-Static-Batching flag open, because the actual blocker — the
  low-poly scatter's seeded per-instance mesh *shape* variation (`FacetedRock`, `BlobCanopy`
  etc. generate a geometrically distinct mesh per instance, not a shared mesh with varied
  transforms) — is an asset-authoring-pattern fact, independent of how many islands or how much
  world exists. GRD's `BatchRendererGroup` instancing gets its win from many objects sharing
  ONE source mesh; procedurally-unique-per-instance meshes don't qualify regardless of scale.
  Scaling the world up changes the *economics* of leaving Static Batching as-is (more duplicated
  batched geometry in memory) without changing whether flipping GRD on would even help — that
  would first require changing the scatter's authoring pattern (shared base mesh + per-instance
  color/shape jitter via GPU-side data, not distinct baked geometry), which is a separate,
  orthogonal decision this ticket does not need to make.

### 3. Streaming / loading options in Unity 6 at this scale

- **"One bigger mesh" (current architecture, extended).** Already the load-bearing pattern for
  both existing islands (single welded terrain mesh + a single ocean mesh, `StaticEditorFlags`
  on scatter props). Per §1, this is proven safe up to ~1200u single-island with large headroom,
  and is **not overkill** at anything close to today's measured scale — it is the cheapest
  extension (no new subsystem) if the two-island world's total footprint stays in the
  low-thousands of world units. It does need the center-offset + shared-ocean fix from §1 before
  it can represent two islands correctly.
- **Additive scene loading — `SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive)`.**
  **Source — general Unity 6 community/documentation synthesis via web search (Moderate — not
  independently confirmed via a direct fetched official-manual quote this session; treat as
  well-supported community consensus, not a verbatim citation).** Multi-scene composition is a
  documented, supported pattern for large streaming worlds — load a "manager" scene once, then
  additively load/unload content scenes with `SceneManager.LoadSceneAsync`/`UnloadSceneAsync`.
  Caveat surfaced by the same search (Moderate, general-audience blog/forum consensus, not
  project-specific): plain additive scene unloading does **not** automatically release memory —
  an explicit `Resources.UnloadUnusedAssets()` (or Addressables-managed release) is typically
  paired with it. **Application-specific complication (Strong — in-repo read):** the two existing
  island scenes are architecturally NOT designed to coexist — `NextIslandPocScene.Build()`
  overwrites `EditorBuildSettings.scenes` to make its own scene the **only** one shipped
  (`NextIslandPocScene.cs:145-147`), and `Boot.unity` is a binary, bootstrap-regenerated scene
  with its own well-documented merge-conflict trap (`unity-conventions.md` §Binary-scene PR
  conflicts — "regenerate-on-rebase, never hand-merge"). A genuine second persistent scene asset
  doubles that surface. Additive loading is architecturally the right fit **if** the design wants
  a real load boundary between the islands (e.g. the boat crossing masks a load), but it is real,
  scoped work here, not a config flip — and the ticket's own "at-sea reveal: the snow-cap peak
  reads as a distant sea-beacon you sail toward" provisional AC implies island B must be
  *visible* well before the player is near it, which argues for loading it early/concurrently
  during the crossing rather than at a hard cut, which additive loading supports but does not
  give for free.
- **Addressables.** Addressables is a content-reference/delivery layer on top of the same
  `SceneManager` scene-loading APIs (it does not replace them) — its main value-adds are
  async dependency tracking, remote/downloadable content, and grouping for MANY interchangeable
  chunks. **This is overkill for the present ticket's scope**, which is exactly two fixed,
  procedurally-generated (not baked-art) islands, with no remote-content or DLC plan documented
  anywhere in this project's CLAUDE.md or team docs. The in-house-tooling posture
  (`in-house-asset-routes-over-paid-tools` memory; CLAUDE.md's declared procedural-first
  posture) also argues against adopting a heavier content-management subsystem the project
  doesn't otherwise need. Reach for Addressables only if the world design later grows into an
  open-ended many-island / many-chunk streaming world — a materially different scope than "two
  named islands."
- **Hand-rolled chunk-LOD (Sebastian Lague "Procedural Landmass Generation").** Already indexed
  in `elite-techniques.md` as *"the reference... when the world stops fitting in one
  hand-built scene."* The same doc's own #226/#278 checkpoint explicitly concludes chunk-LOD is
  **"NOT YET NEEDED"** at ~1200u with the 8x headroom cited in §1. **This is overkill for the
  present ticket.** A fixed two-island world with bounded diameters — even at a hypothetical 3x
  of today's measured size (see §4's sizing note) — is still "one hand-built scene" in the sense
  the doc's own trigger describes; it has not crossed into unbounded/continuously-generated
  terrain, which is the actual problem chunk-LOD solves.

### 4. Water at traversal scale

- **In-repo, directly read from source (Strong — same lagging-tree caveat as above).**
  `Assets/Shaders/LowPolyWater.shader` is confirmed live: `Tags { "RenderType"="Transparent"
  ... "Queue"="Transparent" }`, `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`, and a per-fragment
  depth-fade foam term (`SampleSceneDepth` → `saturate(1.0 - depthGap / _FoamDistance)`) —
  i.e. the Rec 3 depth-fade water from `lowpoly-quality.md` §2 (ticket `86caamnmb`) has already
  landed, not merely "filed" as that doc's own §4 table (written earlier) implies. Both
  `LowPolyZoneGen.MakeWaterMaterial` (ocean) and `LowPolyZoneGen.MakePondMaterial` (pond) build
  material instances on this **same** shader (`LowPolyZoneGen.cs:2039,2106`) — confirming the
  water-shader-research.md's "one shader, tuned per-context via material properties" plan was
  followed through. **Consequence for `unity-conventions.md`:** its §Build stripping & shaders
  bullet "Opaque-queue water is the FPS-protecting CHOICE" is now stale prose describing a
  superseded decision — flagging this for a doc-hygiene pass, not fixing it here (out of Erik's
  scope; Priya/maintain-docs own doc upkeep).
- **Does it survive minutes of open-water crossing? Architecturally yes; performance is a
  re-measure, not a rebuild (Erik's synthesis from the directly-read source + water-shader-research.md).**
  - **No tiling/UV-seam risk:** the vertex-displacement swell is driven by `_Time.y` sampled
    against **world-space XZ** (continuous), not a scrolling UV texture — confirmed as the
    deliberate choice in `water-shader-research.md` §D and consistent with the shipped shader's
    absence of any tiling texture sampler. A single continuous mesh with world-space-keyed waves
    has no seam to hit no matter how far it's crossed.
  - **Foam mid-ocean:** the depth-fade foam mask only lights up near an intersecting opaque
    surface (shore, rock, hull once a boat exists); in open water far from any such surface the
    mask correctly reads ~0 — nothing about "far from shore" breaks the shader; it just shows
    plain water, which is correct.
  - **Floating-point precision at distance from world origin:** not yet a practical concern at
    the scale implied by this ticket (low thousands of world units) — Unity's float32 world-space
    precision issues become material in the tens-of-thousands-plus range, well past anything
    discussed here. Flagging it as a "watch for later," not a current risk (Inference, not
    measured on this project).
  - **Uniform mesh density is the one genuine "not free" item.** The current ocean grid is
    authored at ONE fixed subdivision density (`waterSeg=212` over a 3800u² plane in the POC
    scene — `NextIslandPocGen.cs:705-706`; arithmetic on directly-read source constants, not a
    runtime measurement: `(212+1)² = 45,369` grid vertices) tuned so the near-shore foam ring has
    enough verts (comment: "~18u cells"). That same density gets paid for across open mid-ocean
    too, where no foam/shore interaction happens and the fine density buys nothing. This is a
    real, sourced observation (Inference/judgment, not measured) — the fix, if it turns out to
    matter, is a coarser mid-ocean / finer near-shore authoring split (an offline authoring
    choice, not a runtime LOD system), not a shader rewrite.
  - **A single un-split mesh always fully submits when any part is in the camera frustum**
    (general Unity renderer-culling behavior — Moderate, not fetched from a specific official
    page this session, but standard/uncontroversial Unity rendering behavior: Unity culls per
    `Renderer` by its combined bounds, not per-triangle within one renderer). For a single big
    ocean mesh, this means the *whole* plane's vertex-shader cost is paid whenever any of it is
    visible — which, mid-crossing, is essentially always. This has already been implicitly
    exercised by the existing measured island (its own ocean plane is part of the 0.94M/1.08M
    triangle/vertex-in-frustum figure from the C4 doc), so it isn't a *new* risk class — but a
    resized/duplicated ocean for two islands would add to that same bucket, and should be
    re-measured together with everything else in §4's POC, not assumed safe by extrapolation.
  - **Verdict:** the shader itself does not need new work to be crossed for minutes — it is
    architecturally water-context-agnostic already (ocean and pond share it today). What's
    needed is a perf re-measure at the new (bigger, two-island) extent using the SAME
    `-perfProbe`/capture-gate instrumentation already built for #226/#278, not a "separate piece
    of work" in the sense of new shader engineering.

---

## Application to Far Horizon

- **Unity 6 / URP fit:** everything found here is version-matched to this project's pinned
  `6000.4.11f1` — the two official-docs citations were fetched from the `6000.4` manual tree
  specifically (not a newer/older version extrapolated across).
- **Zone-D look:** nothing in this research proposes changing the faceted/flat-shaded, welded-
  smooth terrain look, the shared-palette scatter materials, or the vertex-color water gradient —
  the multi-island question is purely a scale/architecture question layered on top of the
  already-approved look.
- **Asset-pipeline routes:** unaffected — both islands are built by the procedural route
  (`LowPolyZoneGen`/`NextIslandPocGen`), consistent with the in-house-first posture; no new
  Blender or Hyper3D asset class is implicated by this research.
- **Shipped-exe (Windows desktop) build:** the smallest POC (below) should follow the project's
  existing shipped-build capture-gate discipline (`serve_soak.sh`/`build_poc_island.sh`-class
  entry points already exist for exactly this kind of perf verdict) — reuse, don't reinvent.
- **In-house-tooling posture:** the "Addressables and chunk-LOD are overkill here" finding is a
  direct application of that posture — don't reach for heavier infrastructure than the current,
  fixed two-island scope needs.
- **Staleness caveat (per this dispatch's own instruction and `unity-conventions.md`'s own
  documented warning about this exact tree):** this research reads the orchestrator's local
  worktree on `orch/coordination`, which may lag `origin/main`. Every code-path claim above was
  taken from that local tree; none were independently re-verified against `origin/main` at the
  time of writing (no git access in this session).

  **RESOLVED at peer review — do not re-run the enumeration.** The claims *were* subsequently
  verified against `origin/main` @ `1f2f3c8` in the review of this note. The headline finding
  reproduced on both counted sets: exactly **2** island terrain generators exist on `main`
  (`LowPolyZoneGen.BuildIslandTerrainMesh`, `NextIslandPocGen.BuildTerrainMesh`), and **7 of 7**
  public field-sampling methods across them lack a centre parameter.

  ⚠ **One trap for anyone re-checking this:** the `ox`/`oz` parameters in those signatures *look*
  like a centre offset and are not. They come from `SeedOffset(seed, out ox, out oz)` and are
  consumed only as Perlin sample-space offsets — they re-roll the coast *shape*, they do not move
  the island. **Do not confirm or refute the origin-keying claim from the signatures alone; read
  the bodies** (`Atan2(wz, wx)` and `sqrt(wx*wx + wz*wz)` on raw world XZ, with no centre
  subtraction).

### The smallest POC that would actually de-risk this (Q4)

**Reuse, don't rebuild — the destination island generator and its perf instrumentation already
exist and are already proven safe at ~1200u.** The smallest POC that produces a *real* answer
rather than a demo:

1. Add a center-offset parameter to `NextIslandPocGen` (and, if the start island needs to move
   too, `LowPolyZoneGen`) so an island's radial math can be evaluated relative to an arbitrary
   world-space center, not hard-baked to origin.
2. Place the (already-built) destination island at a real world-space distance from the start
   island in ONE shared scene; build exactly ONE ocean plane sized to cover both landmasses plus
   the gap, deleting the second per-island "all-sides sea" duplicate.
3. Re-run the **existing** `-perfProbe` + NavMesh-coverage + shadow-caster-policy instrumentation
   (the same methodology as PR #226/#278) at the new combined extent — this answers the memory/
   draw-call/NavMesh-bake questions from §1 and the water-at-scale question from §4 with real
   numbers, on the SAME machine/GPU class already used as the baseline (state that scope
   explicitly in the result, per the existing doc's own bounds discipline).
4. **Do not build boat control/sail feel in this POC.** The ticket itself gates that behind its
   own separate `/grill-me`; a scripted or debug-toggle crossing (or even a temporary flycam) is
   sufficient to exercise the perf/streaming/water questions without pre-empting the
   not-yet-locked boat design.
5. **Do not reach for Addressables, additive-scene streaming, or chunk-LOD in this POC** — per
   §3, none of them are earned yet at this scope; build the cheapest "one shared world" version
   first and only escalate to a streaming architecture if step 3's numbers show a real problem
   the single-scene version can't absorb.

This is a small, mostly-plumbing change riding on top of already-measured, already-safe
generators — the kind of POC that turns "we think it'll probably be fine" into a cited number,
without spending any effort on the parts of the design (boat feel, sail distance, the two-island
narrative wiring) that are explicitly not locked yet.
