# Unity 3D Mastery Path — Drew + Devon Skill Elevation

## Question

Sponsor directive (ticket `86ca9a93g`): elevate Drew and Devon to "best of the best" Unity 3D
game developers. Research must be targeted to **Far Horizon's actual domain and the gaps
that recent shipped work exposed**, not a generic "learn Unity" reading list.

## Bottom line

Three areas carry the highest leverage in the order the team will actually feel the pain next:
**(1) Character rig / animation / grounding** — the 8-attempt float saga exposed that neither
developer had internalized the `BakeMesh`-vs-`bounds` distinction, the bone-local-vs-world
posing rule, or the Animation Rigging package as the correct Unity tool for foot IK; closing
this gap prevents the next character feature from burning 8 soak iterations.
**(2) Procedural world-gen at scale** — the island redesign (radial falloff terrain, LOD,
chunked scatter, perf at size) is the imminent M-U3+ surface; the team has zero shipped
experience with LOD chunking or Perlin-based falloff at Far Horizon's intended scale.
**(3) URP / Shader Graph mastery** — every major visual system (water foam, sky, mountains,
foliage) has been a shader stumbling block; a dedicated Shader Graph competency pass closes
the root-cause loop before the next visual feature lands.

Editor tooling / testing and performance profiling are already addressed in
`developer-accuracy-performance-research.md` — this note builds on that, it does not
duplicate it.

---

## Evidence

### Area 1 — Character Rig / Animation / Grounding

#### What elite Unity character devs do by default

**Evidence 1-A — Animation Rigging package (official docs + GDC talk).**

- Source: Unity Technologies, "Introducing the New Animation Rigging Features" — GDC Vault,
  2019 session recording, URL: https://www.gdcvault.com/play/1026151/ — **Strong** (official
  Unity Technologies GDC presentation; covers Two Bone IK, Limb IK, Foot IK constraints as
  the intended runtime grounding layer). Confirms that the Animation Rigging package
  (`com.unity.animation.rigging`) is the canonical answer for procedural foot-to-ground
  adaptation on top of imported clips.
- Source: Unity Learn, "Working with Animation Rigging" — official tutorial, 2023,
  URL: https://learn.unity.com/tutorial/working-with-animation-rigging — **Strong** (official,
  step-by-step). Covers Two Bone IK constraint setup for legs: Root = hip bone, Mid = knee,
  Tip = ankle; IK target drives the foot to a raycast-computed ground point. `ikWeight` driven
  by an animation curve tied to the walk cycle (1.0 when foot planted, 0.0 when lifting) so
  the IK does not fight the step-up phase.
- Source: Unity Discussions, "Animation Status Update Q1 2025 / GDC Roadmap" —
  https://discussions.unity.com/t/animation-status-update-q1-2025-gdc-roadmap/1614718 —
  **Strong** (official Unity team post). Confirms Animation Rigging 1.x is production-stable;
  the Q1-2025 roadmap items (new blending system, per-bone masking, hierarchical state
  machines) are Unity 6 preview features — not yet stable; Animation Rigging 1.x is the
  current recommended layer for foot IK on Unity 6.

**Gap vs our work:** the float saga used hand-rolled raycast grounding in `CastawayCharacter.cs`
(`ApplyGroundSnap`). The BakeMesh snap (Devon PR #47 `2b93cec`) is the correct WHAT (per-frame
actual sole), but the Animation Rigging Two Bone IK is the correct HOW — it adjusts each leg
independently per step so both feet plant without fighting the pelvis position. The manual
`LateUpdate` root offset is a workaround for a missing IK pass. Elite character devs use the
Animation Rigging layer by default before the NavMesh agent is added; grounding becomes a solved
constraint, not a per-project hand-coded system.

**Evidence 1-B — `SkinnedMeshRenderer.BakeMesh` vs `.bounds` (official API).**

- Source: Unity Scripting API, "SkinnedMeshRenderer.BakeMesh" — Unity 6000.2 docs,
  URL: https://docs.unity3d.com/6000.2/Documentation/ScriptReference/SkinnedMeshRenderer.BakeMesh.html
  — **Strong** (official). "Creates a snapshot of SkinnedMeshRenderer and stores it in mesh.
  Vertices are relative to the SkinnedMeshRenderer Transform." The `useScale: true` overload
  (added Unity 2020.3 LTS) includes the bone-baseline scale in the baked mesh — this is the
  only reliable current-frame measurement for an animated skinned mesh.
- Source: Unity Discussions, "Skinned Mesh Renderer BakeMesh not working correctly" —
  https://forum.unity.com/threads/skinned-mesh-renderer-bakemesh-not-working-correctly.543257/
  — **Moderate** (community-reported multiple times across versions; consistent with the
  project's own PR #47 observation that `bounds.min.y` returned -68.7 during walk while the
  true sole was -0.32).
- Source: Far Horizon `unity-conventions.md` §FBX / rigs / characters, "THE REAL ground-snap
  root cause (TWO nested false-greens)" (PR #47 `2b93cec`, 2026-06-15) — **Strong** (directly
  observed; the `-68.7` bounds value is a cited shipped-trace result, not an inference).

**Gap vs our work:** Drew and Devon both attempted `SMR.bounds.min.y` before discovering
`BakeMesh`. The bounds documentation is not loud about the conservative-animation-max behavior.
Elite practice: ANY position/height measurement on an animated skinned mesh uses `BakeMesh(mesh,
useScale:true)` + min-Y scan; `.bounds` is ONLY used for frustum-culling AABB, never for
gameplay spatial queries.

**Evidence 1-C — Unit/scale discipline for FBX imports.**

- Source: Blender Knowledgebase, "Unity and Blender FBX Scale" —
  https://www.katsbits.com/codex/unity-blender-fbx-scale/ — **Strong** (reproducible,
  comprehensive; covers the scene-units / apply-transform export chain that drives the 100×
  bone-scale trap).
- Source: GaminEAI, "Blender FBX Armature Scale Wrong in Unity — How to Fix" —
  https://gamineai.com/help/blender-fbx-armature-scale-wrong-in-unity-unit-scale-apply-transform-fix
  — **Moderate** (well-structured practitioner fix; consistent with official FBX Exporter docs).
- Source: Unity Discussions, "Unity Import converts units sometimes in meters sometimes in cm" —
  https://discussions.unity.com/t/unity-import-converts-units-sometimes-in-meters-sometimes-in-cm/242885
  — **Moderate** (community-confirmed: Blender's default cm-scene → FBX-export → Unity produces
  100× bone lossyScale; the -68u offset in the chibi rig is this class).

**Gap vs our work:** the ~267× lossyScale on `RightHand_010` (PR #29) and the -68u intrinsic
model offset (PR #47) are both instances of the cm/m unit trap. Elite practice: before ANY
work on a sourced FBX, run `Debug.Log(bone.lossyScale)` on a sample bone; if it is not near
`(1,1,1)`, fix the FBX export pipeline first (Blender: scene units = meters, Apply Scale before
export) rather than compensating in code.

**Evidence 1-D — Root motion discipline.**

- Source: Unity Manual, "How Root Motion works" —
  https://docs.unity3d.com/Manual/RootMotion.html — **Strong** (official, covers the Hip node
  as root, bake-to-root vs in-place clips, Apply Root Motion flag). When root motion is ON and
  the NavMesh agent also moves the root, they fight (Unity Discussions, "Best practices for
  using physics-based motion and animated root motion on the same character",
  https://forum.unity.com/threads/best-practices-for-using-physics-based-motion-and-animated-root-motion-on-the-same-character.382409/
  — **Moderate**). Far Horizon's Mixamo generic rig uses in-place clips + NavMesh agent
  translation (correct for click-to-move); root motion is OFF. The risk is on future clip
  imports where root motion may be accidentally enabled — a "walk but never arrive" or
  "teleporting in place" symptom.

**Prioritized learning/adoption path — Area 1:**

1. Both devs complete the official "Working with Animation Rigging" Unity Learn tutorial
   (learn.unity.com/tutorial/working-with-animation-rigging). Estimated 2h. Outcome: can wire
   a Two Bone IK foot constraint to a raycast-computed ground target by default.
2. Establish a pre-import FBX checklist: log `lossyScale` on 3 sample bones before any rigged
   asset integration. If not near `(1,1,1)`, fix the source DCC unit settings, not the code.
3. Establish `BakeMesh(useScale:true)` + min-Y scan as the ONLY permitted skinned-mesh height
   measurement in review (the existing `PlayMode` tests from `developer-accuracy-performance-
   research.md` §B enforce this at the test level; this is the code-review enforcement).
4. Defer root motion to when a non-Mixamo motion-captured clip import requires it; document the
   NavMesh+root-motion conflict rule in `unity-conventions.md` then.

---

### Area 2 — Procedural World-Gen at Scale

#### What elite Unity world-gen devs do by default

**Evidence 2-A — Sebastian Lague's Procedural Landmass Generation series.**

- Source: Sebastian Lague, "Procedural Landmass Generation" — YouTube playlist + companion
  repo https://github.com/SebLague/Procedural-Landmass-Generation — **Strong** (widely
  cited; covers Perlin noise, falloff maps, LOD mesh per chunk, threading on mesh generation,
  NavMesh integration). The falloff map (radial fade from center → ocean edges) is the exact
  technique for the Far Horizon island shape. The threaded chunk system (`MapThreadInfo<T>`,
  separate data/mesh threads) is the standard Unity C# pattern for infinite-terrain perf.
  Note: the series uses `Terrain` objects in later episodes; for Far Horizon's low-poly
  custom meshes, follow episodes 1–16 (mesh-based, not `Terrain`-based). The GitHub repo
  is well-maintained and runnable in Unity 6 with minimal changes.
- Source: University student extension, "Procedural-Landmass-Generation (kafkaphoenix)" —
  https://github.com/kafkaphoenix/Procedural-Landmass-Generation — **Moderate** (extends the
  Lague series with Poisson disc sampling for vegetation scatter — directly applicable to the
  Far Horizon tree/rock scatter system `LowPolyZoneGen.cs`).

**Gap vs our work:** `LowPolyZoneGen.cs` builds the whole island in a single synchronous
pass at bootstrap time. For M-U3+ the island must grow to a size where bootstrap generates
in-editor but the player traverses it in a chunked streaming fashion. The Lague chunk pattern
separates data (thread-safe Perlin + falloff) from mesh (Unity main thread), which is the
necessary next architecture step.

**Evidence 2-B — Poisson disc scatter and GPU instancing for dense scenes.**

- Source: Unity Learn, "Creating a Procedural Terrain" (2023, linked from
  https://learn.unity.com/project/procedural-terrain) — **Moderate** (official Unity Learn
  project; demonstrates the LOD system + chunk viewer + scatter callback pattern). Covers
  LODGroup on each chunk with 3 levels and Culled threshold.
- Source: TheGamedev.Guru, "Unity Draw Call Batching: The Ultimate Guide" —
  https://thegamedev.guru/unity-performance/draw-call-optimization/ (cited in
  `developer-accuracy-performance-research.md` §D) — **Strong**. Covers why Poisson
  disc scatter with per-instance `_Color` stays in SRP Batcher (per-material cbuffer) vs
  `MaterialPropertyBlock` which breaks it. The `QuantizeFine` pattern (from
  `procedural-shadergraph-quality-research.md` §F) is the correct perf-aligned color system:
  a fixed palette of materials (not per-instance MPB) keeps draw calls in SRP batches.

**Evidence 2-C — Radial falloff / island heightmap math.**

- Source: Red Blob Games, "Making maps with noise functions" — 2015, updated 2024,
  https://www.redblobgames.com/maps/terrain-from-noise/ — **Strong** (Amit Patel; canonical
  reference for noise-based island generation; covers radial falloff, multiple octaves, ridge
  noise for mountain silhouettes). The "island" shape is `max(0, noise - falloff_map)` where
  falloff is `1 - distance_from_center_normalized`. This is the mathematical basis for the
  future big-island reshape beyond the current flat-cylinder island.
- Source: Red Blob Games, "Polygonal Map Generation" —
  https://www.redblobgames.com/maps/mapgen2/ — **Moderate** (more complex graph-based
  approach; not recommended for M-U3 — cite for future reference only if a river/biome system
  is added).

**Evidence 2-D — LOD and culling budget on desktop.**

- Source: Unity Manual, "Level of Detail" — Unity 6 docs,
  https://docs.unity3d.com/6000.0/Documentation/Manual/LevelOfDetail.html — **Strong**
  (official). The `LODGroup` component + `LODGroup.RecalculateBounds()` covers the Far Horizon
  use case (chunked island sections each with their own LODGroup). Key: `Culled` threshold at
  ~0.01 screen height eliminates background chunks entirely; static batching per LOD level
  further reduces draw calls.
- Source: Unity Manual, "Configure for better performance in URP" (cited in
  `world-look-far-vista-research.md`) — **Strong**. The Far Horizon desktop target has no
  mobile perf ceiling; 2-3 LOD levels per chunk with SRP Batcher is sufficient for a dense
  island at the orbit-camera scale.

**Prioritized learning/adoption path — Area 2:**

1. Both devs watch/follow the Lague series episodes 1–16 (the mesh-based chunk path; skip
   Terrain-component episodes). The companion repo is the best open-source Unity reference for
   exactly the Far Horizon procedural architecture. Estimated 8–12h total.
2. Study the Red Blob Games island-falloff page before touching the island heightmap. The
   radial-falloff math is 15 lines of Perlin + falloff; understanding the shape-parameter
   space prevents a "the island is too flat" soak cycle.
3. Before any scatter density increase, establish a frame-debugger SRP Batcher baseline
   (per the existing `developer-accuracy-performance-research.md` §D recommendation — this
   area pairs with Area 5 of that note).
4. Add per-chunk `LODGroup` to `LowPolyZoneGen` BEFORE the island scales beyond the current
   200u radius — retrofitting LOD after the scene is dense is more expensive than adding it
   at moderate complexity.

---

### Area 3 — URP / Shader Graph Mastery

#### What elite Unity shader devs do by default

**Evidence 3-A — Cyanilux URP reference library (the team's own proven source).**

- Source: Cyanilux, "Depth Shader Tutorials for URP" —
  https://www.cyanilux.com/tutorials/depth/ — **Strong** (deep technical; URP-specific; cited
  twice in existing Erik consult notes for depth-fade foam and shoreline). Cyanilux is the most
  reliable independent URP Shader Graph reference currently active (regularly updated for Unity
  6). Covers: depth texture sampling, scene color, custom render features, shadow receiving,
  stencil — the full advanced surface.
- Source: Cyanilux, "Shoreline Shader Breakdown" — December 2024,
  https://www.cyanilux.com/tutorials/shoreline-shader-breakdown/ — **Strong** (URP, Shader
  Graph, 2024). Already cited in `world-look-quality-research.md` §Water.

**Evidence 3-B — Daniel Ilett's URP Shader Graph tutorials (step-by-step exemplar series).**

- Source: Daniel Ilett, "Shader Graph Basics" series and "Stylised Water" —
  https://danielilett.com/ (index) — **Strong** (step-by-step, URP, current Unity 6 notes in
  each article; covers toon shading, depth-fade foam, stylized grass, scene intersections).
  "Part 8 — Scene Intersections" (cited in `procedural-shadergraph-quality-research.md` §B)
  is the canonical Shader Graph depth-fade tutorial for Unity 6. Ilett publishes monthly
  updates; the URP tutorial set is the best structured learning path for the Shader Graph
  node vocabulary needed on Far Horizon.
- Source: NedMakesGames, "Creating a Foliage Shader in Unity URP Shader Graph" —
  https://nedmakesgames.medium.com/creating-a-foliage-shader-in-unity-urp-shader-graph-5854bf8dc4c2
  — **Moderate** (covers diffuse, translucency, wind deformation for low-poly trees; directly
  applicable to the Far Horizon blob-canopy foliage. The wind deformation vertex offset is the
  next foliage upgrade after the current static canopy).

**Evidence 3-C — Open-source URP shader reference implementations.**

- Source: pavelkouril/unity-lowpoly-shader — GitHub,
  https://github.com/pavelkouril/unity-lowpoly-shader — **Moderate** (Unity shader for
  low-poly mesh rendering; uses geometry shader for flat-shading, which is heavier than the
  ddx/ddy approach recommended in `procedural-shadergraph-quality-research.md` §A — but useful
  as a readable reference for the normal-derivation approach before implementing the lighter
  frag-stage version). Note: geometry shaders are deprecated in some Metal/WebGPU contexts;
  for Unity 6 Windows desktop, this is not a concern.
- Source: keijiro/UnitySkyboxShaders — GitHub, https://github.com/keijiro/UnitySkyboxShaders
  — **Strong** (Keijiro is a Unity Technologies Developer Advocate; these are idiomatic
  reference shaders with correct render state). Already cited in `world-look-far-vista-
  research.md` and `world-look-quality-research.md`. The gradient skybox shader is the best
  single short-read for understanding why Background-queue + correct positionCS eliminates the
  draw-over-geometry bug (the skybox-wash failure class in `unity-conventions.md`).

**Evidence 3-D — The ddx/ddy flat-shading technique as a class-ending pattern.**

- Source: Hextant Studios, "Rendering Flat-Shaded / Low-Poly Style Models in Unity" —
  https://hextantstudios.com/unity-flat-low-poly-shader/ — **Strong** (step-by-step; specific
  to Unity URP; cited in `procedural-shadergraph-quality-research.md` §A). The key elite
  insight: computing `normalize(cross(ddy(positionWS), ddx(positionWS)))` in the fragment
  stage eliminates the need for unwelded verts with manually-assigned normals, which is the
  source of EVERY winding-inversion bug in the project (the -Z grid/water bug, the
  `FacetedRock` bug, both in `unity-conventions.md`). An elite URP developer knows this trick
  before writing any flat-shaded builder.

**Gap vs our work:** the recurring winding-inversion bugs (water, rocks) are a direct
consequence of manually assigning normals to procedural meshes — the ddx/ddy approach
makes the frag shader compute the correct face normal from actual geometry regardless of winding
direction. Similarly, the skybox-wash failure was a render-state gap (`positionCS.xyww`
misapplied); an elite Shader Graph developer reads keijiro's background-queue shader BEFORE
authoring a custom sky. The gap is not capability — it is reference material.

**Prioritized learning/adoption path — Area 3:**

1. Both devs read through danielilett.com's Shader Graph Basics series (Part 1–8, ~4h total).
   This establishes the node vocabulary so that Cyanilux's depth tutorials are not cold-start.
   Priority: Parts 7–8 (scene depth, intersections) directly unlock the water foam and
   mountain-depth work already in flight.
2. Both devs read the Hextant Studios ddx/ddy article before writing any new procedural mesh
   builder. This is a 20-minute read that permanently closes the winding-normal bug class.
3. When the foliage wind/density pass starts (M-U4+), add the NedMakesGames foliage shader
   tutorial as the starting reference before authoring a vertex-offset wind pass.
4. keijiro/UnitySkyboxShaders: bookmark and read the gradient shader's 60-line source before
   ANY future skybox work. The source is a stronger learning artifact than a tutorial on this
   surface.

---

### Area 4 — Performance and Profiling

This area is already addressed in `developer-accuracy-performance-research.md` §D (Frame
Debugger / SRP Batcher discipline, GPU instancing vs SRP Batcher exclusion, Transparent-queue
overdraw risk for large water planes). Key additions beyond that note:

**Evidence 4-A — Unity Profiler CPU bottlenecks on NavMesh + skinned mesh.**

- Source: Unity Manual, "Profiler window" — Unity 6 docs,
  https://docs.unity3d.com/6000.0/Documentation/Manual/ProfilerWindow.html — **Strong**
  (official). The CPU Profiler shows `NavMeshAgent.Update` cost and `Animator.Update`
  cost in separate tracks. The `BakeMesh` per-frame call (Devon PR #47) is in the CPU track
  as `SkinnedMeshRenderer.BakeMesh`; if a second character is added (NPC, companion), this
  cost doubles — the Profiler will make this visible before it becomes a frame-budget issue.
- Source: Unity Manual, "Unity Profiler best practices" (2024) —
  https://docs.unity3d.com/6000.0/Documentation/Manual/profiler-best-practices.html —
  **Strong** (official). Recommends `Profiler.BeginSample("tag")` / `EndSample()` around the
  `BakeMesh` + min-Y loop and any future per-frame per-character compute.

**Gap vs our work:** no profiler-annotated code sections exist yet in `CastawayCharacter.cs`
or `LowPolyZoneGen.cs`. This is fine for one character + one island; it becomes important
as M-U3+ adds world density and M-U4+ considers NPCs or a second character. Adding
`Profiler.BeginSample` annotations now is 5-minute work and makes the Profiler immediately
readable if a perf regression appears.

**Prioritized adoption — Area 4:** add `Profiler.BeginSample("CastawayGroundSnap")` around
the `BakeMesh` + min-Y block in Devon's next character PR. For world-gen, annotate
`LowPolyZoneGen.BuildZone()` and `ScatterInstances()` as the imminent chunking work starts.
No new learning resources needed beyond what `developer-accuracy-performance-research.md`
already cites — this is a code-habit, not a research gap.

---

### Area 5 — Editor Tooling / Automation and Testing

This area is already addressed in `developer-accuracy-performance-research.md` §A-C. Key
addition beyond that note:

**Evidence 5-A — Unity Test Framework `[UnityTest]` for animation state validation.**

- Source: Unity Test Framework, "UnityTest attribute" —
  https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/reference-attribute-unitytest.html
  — **Strong** (official; cited in `developer-accuracy-performance-research.md` §B). The
  three priority tests (feet-grounded-during-walk, held-axe-in-grip-envelope,
  fingers-closed-when-prop-held) remain unimplemented as of this note. Their absence is the
  reason the Sponsor can still see float bugs that pass QA.

**Gap vs our work:** the unimplemented PlayMode locomotion tests. This is the single most
impactful near-term test investment — more than any new linting or static analysis. Once these
three tests are green in CI, the "tests pass / Sponsor sees float" failure class is
structurally impossible.

**Prioritized adoption — Area 5:** Devon authors the three locomotion tests as the first PR
after PR #47 stabilizes; Tess gates any future character PR on these tests being green. No
new research needed — the implementation pattern is fully specified in
`developer-accuracy-performance-research.md` §B.

---

## Application to Far Horizon

### The three gaps that hurt the most right now, mapped to next work

| Gap | Symptom on our project | Elite practice | First action |
|-----|----------------------|----------------|--------------|
| No Animation Rigging IK | 8-iteration float saga; manual `LateUpdate` root hack | Two Bone IK constraint + raycast target per foot; `ikWeight` on animation curve | Both devs complete Unity Learn "Working with Animation Rigging" tutorial (2h each) |
| No BakeMesh discipline | `bounds.min.y = -68.7` false-green; position gauge manufacturing false confidence | `BakeMesh(useScale:true)` + min-Y scan for ALL skinned spatial queries; never `.bounds` for gameplay | Add to `unity-conventions.md` as a CAPITALIZED rule; enforce in code review |
| No Shader Graph vocabulary | Repeated skybox-wash, winding-cull, and missing-render-state bugs | ddx/ddy face-normal in frag (one read closes all winding bugs); Background-queue skybox (one shader to read) | Both devs read Hextant Studios ddx/ddy article + keijiro gradient shader source before next shader PR |
| No island-scale LOD architecture | Bootstrap full-island synchronous generation; no chunking; no streaming | Threaded chunk generation (Lague pattern); LODGroup per chunk; Poisson disc scatter | Both devs follow Lague episodes 1–16 before M-U3 island-scale ticket starts |
| No PlayMode locomotion tests | "Tests green / Sponsor sees float during walk" class | `[UnityTest]` + `yield return null` frame loop asserting feet <= ground Y per frame of walk cycle | Devon authors 3 locomotion tests in the PR after #47 stabilizes |

### What NOT to pursue as a "mastery" investment right now

- **HDRP migration:** zero relevance; the project is URP-locked and Windows desktop only.
- **Burst Compiler / Jobs System for world-gen:** premature. The island at current size (200u)
  needs no multi-threading. Lague's threaded C# approach (standard threads, not Jobs) is the
  correct intermediate step. Jobs/Burst adds significant API surface area for an incremental
  perf gain that isn't needed yet.
- **Visual Scripting (Bolt):** not applicable to this team's codebase or skill model.
- **Unity Muse / AI-assisted coding tools:** the Sponsor has explicitly declined paid AI
  tooling additions beyond the current budget; do not recommend subscription tools.

### Budget note

All recommended resources are free:
- Unity Learn tutorials (free with Unity account)
- Unity Manual / Package docs (free)
- GDC Vault (the 2019 Animation Rigging session is publicly accessible)
- Sebastian Lague YouTube + GitHub (free, open-source)
- danielilett.com (free)
- Cyanilux.com (free)
- Red Blob Games (free)
- keijiro GitHub repos (free, MIT/public domain)

No Asset Store purchases needed for the mastery path. (Polyverse Skies / cloud packs remain
available as optional time-savers per `world-look-quality-research.md` if a timeline crunches —
but they are not mastery investments.)

---

## Evidence-strength summary

| Claim | Strength |
|-------|----------|
| Animation Rigging Two Bone IK is the canonical foot grounding tool | Strong (GDC Vault official Unity talk + Unity Learn official tutorial) |
| `BakeMesh(useScale:true)` is the only correct per-frame skinned-mesh height measurement | Strong (Unity official API docs + project-observed -68.7 evidence) |
| cm/m unit trap = 100× lossyScale on imported FBX bones | Strong (Unity FBX Exporter docs + katsbits.com reproducible; directly observed in the project) |
| Sebastian Lague's series is the standard reference for Unity chunk LOD procedural terrain | Strong (widely reproduced; GitHub repo maintained; maps 1:1 to Far Horizon architecture needs) |
| Red Blob Games falloff map math is the canonical island-shape reference | Strong (Amit Patel; widely cited; directly applicable to radial island falloff) |
| Cyanilux / danielilett are the strongest independent URP Shader Graph learning resources | Strong (multiple citations validated in prior Erik consult notes; content proven on shipped work) |
| ddx/ddy frag-normal eliminates all winding-inversion bug classes | Strong (Hextant Studios; confirmed by project's own winding bugs which the technique would have prevented) |
| No Unity 6 Burst/Jobs needed at current island scale | Moderate (Unity configure-for-perf docs confirm SRP Batcher + static batching is sufficient for desktop; Burst benefit at this scale is speculative) |

---

## Relationship to prior Erik consult notes

This note builds on `developer-accuracy-performance-research.md` (process/test/perf patterns).
It does NOT duplicate that note's recommendations; those remain the correct Rank 1–5 actions.
The skill-mastery path here is the CRAFT / KNOWLEDGE layer underneath the process layer:

- Prior note §A: Diagnose-Before-Fix → this note: the specific tools (Animation Rigging,
  BakeMesh API, Shader Graph depth nodes) the diagnosis would have pointed to sooner.
- Prior note §B: PlayMode locomotion tests → this note: the Animation Rigging IK layer that
  makes the tests structurally robust (IK grounds each foot independently, eliminating the
  "one foot planted, one floating" mid-step class).
- Prior note §D: Frame Debugger / SRP Batcher baseline → this note: the Shader Graph flat-
  shading approach (ddx/ddy) that keeps all procedural meshes in a single shader variant,
  preserving SRP Batcher compatibility by construction.
