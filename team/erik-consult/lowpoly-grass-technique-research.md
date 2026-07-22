# Dense Low-Poly Blade Grass — Unity 6 URP Technique Selection

## Question

What is the best technique for the dense chunky-blade-tuft grass meadow visible in `inspiration/low poly grass.png` in Unity 6 URP, across a large island, with no sway, no per-asset textures, and lush per-tuft green variation? Which technique should the POC use?

## Bottom line

**Use Unity Terrain Details with GPU instancing and a custom `LowPolyGrass` Shader Graph.** A hand-authored tuft mesh (3–5 blades per tuft, ~10–18 tris) is painted via the Terrain Detail system at medium density; the Shader Graph replaces the built-in material and encodes tint variation from UV.y (blade base→tip gradient) plus a seeded per-tuft hue nudge driven by world-position hash. At mid-distance (>15 m) a quad-cross ("X") detail variant culls most blade geometry. Flowers go in the same Detail system as separate prefab types. This path is already in the engine (no extra packages), stays in the GPU Resident Drawer's safe zone (no MaterialPropertyBlocks), and matches exactly what the Sponsor's reference image shows.

Geometry shaders are the wrong call: they are unreliable on DX12/Vulkan (the direction Unity 6 is heading) and force procedural vertex injection that can't be pre-authored as chunky low-poly. Procedural `RenderMeshIndirect` (Cyanilux/MangoButtermilch class) is the right choice for unlimited scale but adds a C# + Compute Shader layer with no density-painter UI — overkill for a curated island where a terrain painter is faster.

---

## Evidence

### Source 1 — "Six Grass Rendering Techniques in Unity" — Daniel Ilett, danielilett.com, 2022, https://danielilett.com/2022-12-05-tut6-2-six-grass-techniques/

Surveys Mesh Grass, Geometry+Tessellation, Procedural Compute Rendering, Billboarding, Unity Terrain Details, and Impostors in URP. Directly states: "Geometry shaders … have been largely left in the past by some developers … Some devices like the Oculus Quest don't support them at all." Rates procedural rendering (compute shader + DrawMeshInstancedIndirect) as "very fast" but notes it requires "platform support for compute shaders." Terrain Details with instancing is flagged as "convenient painting tools." **Strength: Moderate** (technical write-up, multiple techniques compared; not a Unity official source).

### Source 2 — Unity Manual, "Grass and other details" — Unity Technologies, 2024, https://docs.unity3d.com/Manual/terrain-Grass.html

Confirms: (a) custom Shader Graph materials work on instanced detail meshes; (b) GPU instancing uses persistent constant buffers for CPU/GPU efficiency; (c) the Healthy/Dry color UI disappears under GPU instancing — tint variation must come from the shader. LOD Group is not compatible with Terrain Details — use the shader to fade at distance or rely on the Terrain Detail Distance setting. Batches of up to 1,023 instances share one draw call; light probes/lightmaps are not supported for instanced details (acceptable — stylized flat-shaded grass with APV sky-colour ambient is the correct path anyway). **Strength: Strong** (official Unity documentation, Unity 6 version).

### Source 3 — Unity Manual, "Make a GameObject compatible with the GPU Resident Drawer" — Unity Technologies, 2024, https://docs.unity3d.com/6000.0/Documentation/Manual/urp/make-object-compatible-gpu-rendering.html

Lists disqualifiers for the GPU Resident Drawer: MaterialPropertyBlocks; `OnWillRenderObject`/`OnBecameVisible`/`OnBecameInvisible` callbacks; >128 materials per GO; real-time GI. Terrain Detail instancing paths around all of these by design — it is the GPU's own batching, not the Resident Drawer path, but it does NOT disqualify the rest of the scene's GPU Resident Drawer. Procedural `RenderMeshIndirect` also bypasses GPU Resident Drawer safely (it is a separate Graphics API draw path). **Strength: Strong** (official Unity 6 documentation).

### Source 4 — "GPU Instanced Grass Breakdown" — Cyanilux, cyanilux.com, 2024, https://www.cyanilux.com/tutorials/gpu-instanced-grass-breakdown/

Shows that `Graphics.RenderMeshPrimitives`/`RenderMeshIndirect` with a ComputeBuffer renders millions of blades in one draw call, with per-instance `float4 color` variation available without MaterialPropertyBlocks. Notes Unity 6 required changing the Instance ID node. Frustum culling reduces 500K instances to ~89K visible. Excellent for unlimited runtime scatter but requires a C# GrassManager + Compute Shader — there is no paint tool; positions must be generated procedurally or read from a density map. **Strength: Moderate** (detailed technical tutorial, verified on Unity 6 + URP 17, single author).

### Source 5 — Unity Shader Graph package samples (`com.unity.shadergraph@93ee0fdc6ad8` / ProductionReady/Environment/Details/Grass/) — found in `c:/Trunk/PRIVATE/EmbergraveUnitySlice` package cache

Unity ships `GrassWindTerrainDetails.shadergraph` + three LOD blade FBXs (`grass_bladeNoLOD10/15/30.FBX`) as production-ready Shader Graph samples, paired with three materials at 10/15/30 density. This is the Terrain Details + GPU instancing + Shader Graph path demonstrated at production scale directly by Unity. The mesh FBXs are single-blade meshes that can be assembled into a tuft prefab. **Strength: Strong** (first-party Unity samples, found locally in the project's package cache — ground truth for what the engine ships).

### Source 6 — Inspiration image `inspiration/low poly grass.png` — Sponsor reference, 2026-06-29

The target image is a ground-level meadow shot with dense pointed blade-mesh tufts (each tuft has 4–6 crossed blades, ~15–20 cm tall), saturated bright green, slight blade-to-blade hue variation within a tuft, no sway, no textures — pure vertex-colored faceted geometry. White daisy clusters (a flat 5-petal flower mesh) are scattered in patches, 5–8 flowers per cluster; scattered purple spires. Faceted rocks from the existing `FacetedRock` generator are visible in foreground. The image confirms this is NOT a billboard / quad-sprite grass — it is genuine 3D blade geometry. **Strength: Strong** (direct Sponsor reference, visual ground truth).

### Source 7 — "GitHub Unity-Grass-Instancer" — MangoButtermilch, GitHub, 2024, https://github.com/MangoButtermilch/Unity-Grass-Instancer

Six progressive approaches from plain instancing → frustum culling → chunking → occlusion culling → high-perf occlusion → infinite. Tested on Unity 2022.3 + URP 14 and Unity 6 + URP 17. Chunk-based frustum culling reduces ~500K blades to only those visible. The system is a reference implementation for if/when the island scale exceeds what Terrain Details can handle — not the POC starting point. **Strength: Moderate** (open-source project, confirmed Unity 6 compatibility, no official endorsement).

---

## Application to Embergrave/Far Horizon

### The five candidates evaluated against project constraints

| Technique | Chunky 3D blades | Shared-palette (no per-asset tex) | Tint variation | Large island perf | Paint tool | Unity 6 safe |
|---|---|---|---|---|---|---|
| **Terrain Details + GPU instancing + Shader Graph** | Yes (custom mesh) | Yes (vertex color or UV) | Yes (shader-driven) | Good with cull distance | Yes (Terrain paint) | Yes |
| Geometry + Tessellation shaders | Procedural only | Yes | Partial | Variable; DX12/Vulkan fragile | No | Fragile |
| Procedural RenderMeshIndirect (compute) | Yes (custom mesh) | Yes | Yes (ComputeBuffer) | Excellent | No (code/density map) | Yes |
| Unity Terrain Details (billboards) | No — quads | Yes | Limited | Best perf | Yes | Yes |
| Plain mesh scatter (Prefabs/LODGroups) | Yes | Yes | Per-material only | Poor at density | Manual only | Yes |

**Winner: Terrain Details + GPU instancing + custom Shader Graph**, for these reasons specific to Far Horizon:

1. **The Sponsor target IS chunky 3D blade meshes.** Billboards are ruled out by the reference image. Geometry shaders cannot produce the hand-authored chunky faceted-blade geometry the Sponsor expects — they generate thin mathematical strips.

2. **No per-asset textures.** The existing `LowPolyVertexColor` shader is the palette discipline. The Shader Graph for grass encodes colour entirely in vertex colour (RGBA) + UV.y for base-to-tip gradient — zero textures. This is the same discipline already in use for `FacetedRock`, `BlobCanopy`, `GrassClump`.

3. **Tint variation without MaterialPropertyBlocks.** The Shader Graph reads a world-position-based pseudo-random offset added to a base green hue, applied per-vertex in the vertex stage. No MaterialPropertyBlock → no GPU Resident Drawer disqualification for other scene objects.

4. **Island scale is curated, not infinite.** The island uses a Unity Terrain (`FacetedLandmass`). Terrain Details integrate directly with the terrain — no separate scatter system needed. Density falloff + Detail Distance (set to ~30 m for blades, ~60 m for low-detail X-cross variant) keeps draw count bounded.

5. **Stationary requirement is trivially met.** The `GrassWindTerrainDetails` Shader Graph sample includes wind animation; simply remove the `AnimatedGrassPhase` subgraph node. The remaining Shader Graph is pure tint + vertex-color unlit — cheaper than the wind version.

6. **GPU Resident Drawer compatibility.** Terrain Detail rendering is a separate GPU path that neither requires nor interferes with the scene's GPU Resident Drawer batch. The rest of the island's props (rocks, trees, campfire) stay fully in the GPU Resident Drawer path.

7. **Geometry shader fragility is a disqualifier.** Unity 6 defaults DX12; geometry shaders are not natively supported in DX12's shader model and require either Metal/Vulkan fallback or the legacy DX11 path. This is confirmed fragile behaviour per danielilett.com (Source 1). The project should not build a core visual system on a deprecated shader stage.

### Tint variation implementation detail

The shared-palette discipline rules out per-tuft MaterialPropertyBlocks. The correct approach for the Terrain Details path is to encode tint in **vertex color alpha** of the blade mesh: bake a per-blade random value (0.0–1.0) into vertex alpha at mesh creation time. In the Shader Graph, `lerp(BaseGreen, DarkGreen, vertex.alpha)` gives blade-to-blade variation within a tuft and tuft-to-tuft variation across the meadow. This is zero runtime cost (baked constant), consistent with Rec 6 (vertex-color AO alpha baking) from `lowpoly-quality.md`, and already in the established pattern for `BlobCanopy` per-blob value blending.

### Flower accents

Fold flowers into the same Terrain Details system as **separate Detail types** (not a separate scatter system). Author a white daisy cluster mesh (5 petals + yellow centre, ~12 tris, vertex-colored) and a purple spike mesh (~8 tris). Register as additional Detail types on the same Terrain layer, painted at low density (~10% of blade density) in the same pass the developer paints grass. This costs nothing extra in architecture — Detail types share the same GPU instancing path. Do NOT create a separate scatter/prefab system for flowers; that adds a second rendering path for no gain.

### Rocks

Existing `FacetedRock` generator is correct. Place rocks as scene Prefabs (not Terrain Details) to keep collider + seeded-shape variation. They already render via `LowPolyVertexColor` + the GPU Resident Drawer path.

### LOD / cull strategy

- **Blade tuft mesh (near):** 3–5 blades per tuft, ~10–18 tris. Terrain Detail Distance = 25–30 m.
- **X-cross quad variant (mid):** 2 crossed quads, ~4 tris. Second Detail type with same material; paint at same density. Detail Distance = 50–60 m.
- **Beyond 60 m:** nothing (grass is hidden by fog + far-plane cull). The island is bounded; the big-world feel comes from the mountain backdrop and fog, not from grass at extreme range.
- **Density dial:** start the POC at Terrain Detail Density = 6–8 (moderate); let the Sponsor's soak decide whether to increase to 10–12 (lush) or reduce for performance. Profile on the built exe before committing to maximum density.

### Upgrade path to RenderMeshIndirect

If the island scale + density chosen in the soak pushes the Terrain Detail system past its per-chunk 1,023-batch limit, the procedural `RenderMeshIndirect` path (Source 4 / Source 7) is a clean upgrade: same blade mesh, same Shader Graph material, swap the painting for a density map or runtime scatter grid. This upgrade does not require re-authoring the mesh or shader — only the scatter management layer changes.

---

## POC build spec (for Drew/Devon)

The POC is entirely new code — there is no existing grass in `Assets/Scripts/`. The spike (`EmbergraveUnitySlice`) had a flat `GrassMat.mat` using the standard URP Lit shader (found in the eval repo), which is NOT the target.

1. **Blade tuft mesh.** Author a single tuft prefab in Blender MCP: 3–4 pointed blade triangles crossed at the base, ~10–14 tris, vertex-colored in the world palette (bright-mid green base, slightly darker at vertex base). Bake a random value (0.0–1.0) per blade into vertex alpha for tint variation. Export FBX to `Assets/Art/Environment/Grass/GrassTuft_Blade.fbx`.

2. **Shader Graph.** Create `Assets/Art/Environment/Grass/LowPolyGrass.shadergraph` (URP Unlit target, Two Sided). Vertex stage: `lerp(BaseGreen, DarkGreen, vertex.alpha)` for tint; no wind. Fragment: output the tinted vertex colour directly (no texture sample). Keep all properties inside `CBUFFER_START(UnityPerMaterial)` for SRP Batcher compliance. Reference: `GrassWindTerrainDetails.shadergraph` in the Shader Graph package samples (already in the project's PackageCache — use it as a structural template, strip the wind subgraph).

3. **Terrain Detail registration.** On the island Terrain component, add two Detail types: (a) `GrassTuft_Blade` prefab + `LowPolyGrass` material, Render Mode = Grass (GPU Instancing); (b) `GrassTuft_Cross` quad variant + same material. Paint with Detail Distance = 28 m (blades) / 55 m (cross). Start at Density = 7.

4. **Flower Details.** Add two more Detail types: daisy cluster + purple spike, vertex-colored, same material. Paint at ~10–15% of grass density.

5. **Verify.** Build + capture from the gameplay camera (not editor). Check: blades are 3D (not billboards), hue varies tuft-to-tuft, flowers visible, no sway, rocks sit in grass correctly, no GPU Resident Drawer disqualifications introduced (Frame Debugger → confirm scene prop batches are unchanged).

**Gotchas:**
- Terrain Details GPU instancing drops Healthy/Dry color UI. Tint variation must come entirely from the Shader Graph (vertex alpha channel as described above).
- LOD Group components are incompatible with Terrain Details. Use the Detail Distance fade + the X-cross second type instead.
- The `_EnableInstancingVariants: 0` flag seen in the spike's `GrassMat.mat` means GPU instancing was OFF in the spike. This must be enabled (`Enable GPU Instancing` checkbox on the material) or the Terrain system falls back to a less efficient path.
- Up-biased normals: `lowpoly-quality.md` §1 already enforces `nUp = up*0.85 + outward*0.15` for foliage (it's in `GrassClump` in the Godot era). The Unity blade mesh should replicate this normal bias so base-to-tip lighting gradient reads warm (not dark at the base). Set vertex normals manually in Blender before export.
