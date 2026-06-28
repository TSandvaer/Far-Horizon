# Low-Poly Stylized Trees, Grass, and Sky — Unity 6 / URP Implementation Options

## Question

What are the implementable technical options for trees (`86cabc73q`), grass (`86cabc737`), and sky (`86cabc743`) for the Far Horizon visual-polish wave? The Sponsor gates the actual look; this note makes impl ready.

## Bottom Line

**Trees:** Add a simple SRP-Batcher-compatible wind shader (vertex-colour alpha masks the canopy, vertex offset from a sine pair keyed on world XZ) to the existing `LowPolyVertexColor.shader`; split the trunk and canopy into separate material instances at different `_WaveAmp` settings. No engine change needed; the existing blob-canopy mesh already has the geometry needed.

**Grass:** Extend the existing `BuildGrassClump` scatter with an in-shader grass-wave (same sine-in-vertex trick at `_WaveAmp` scale); for the density the island uses (~600 clumps today, scalable to ~1500) the existing SRP-Batcher path is sufficient. A GPU-instanced compute path is a documented fallback only if desktop profiling shows CPU submission as the bottleneck.

**Sky:** The existing `FarHorizon/GradientSkybox` shader is already shipped, tuned, and seam-killed. The only open option is animated low-poly clouds as world-space GameObjects using the existing `CloudBlob` mesh (`LowPolyMeshes.cs`) — slow horizontal drift via a simple `Transform.position` advance in a MonoBehaviour. No sky-shader change is warranted.

---

## Evidence

### Trees — wind / LOD / batching

**Source 1** — NedMakesGames, "Creating a Foliage Shader in Unity URP Shader Graph," Medium, 2021. URL: https://nedmakesgames.medium.com/creating-a-foliage-shader-in-unity-urp-shader-graph-5854bf8dc4c2. **Strength: Moderate.** Demonstrates using UV.y (or a custom TexCoord channel) as a wind-dampen mask so the base of trunk/grass doesn't sway. Vertex displacement formula: wind direction × strength × noise sample + a cross-direction turbulence term. Shader Graph version; HLSL port of the same logic is straightforward.

**Source 2** — Unity 6 Manual, "Enable the GPU Resident Drawer in URP," Unity Technologies. URL: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html. **Strength: Strong (official docs).** Confirms `unity6-mastery.md §2` disqualifiers: `MaterialPropertyBlock` on a `MeshRenderer` opts it out of GPU Resident Drawer. Per-tree wind driven by a per-instance `MaterialPropertyBlock` therefore breaks batching. The correct path for per-tree variation is a seeded per-instance world-position hash baked into a constant CBUFFER property (already done by `BuildTree`'s position-keyed `leanRnd`) or a single global time-based sine that all instances share.

**Source 3** — polygon-wind GitHub (RenanBomtempo). URL: https://github.com/RenanBomtempo/polygon-wind. **Strength: Weak (2017, legacy pipeline).** Describes vertex-colour channel masking: red = branches (vertical sway), blue = leaves (wiggle). The idiom transfers to HLSL; the actual shader is not URP-compatible. Referenced for the vertex-colour masking pattern only.

**Source 4** — LMH Poly, "Unity Wind Shader For Low Poly Trees Pack." URL: https://www.lmhpoly.com/tutorials/unity-wind-shader-for-low-poly-trees-pack. **Strength: Weak (blog/tutorial, pipeline unclear).** Confirms vertex-colour anchor masking is a standard pattern in the low-poly community; notes the "5 trunk colours = no movement" idiom as a common extension. Referenced for context only.

**Key constraint identified from Sources 1–3:** Any wind implementation that requires a per-instance `MaterialPropertyBlock` breaks GPU Resident Drawer. The correct architecture is:
- Wind driven entirely by time + world-position in the vertex shader (all instances read the same uniform `_WaveAmp`, `_WaveLen`, `_WaveSpeed` — already in `LowPolyVertexColor.shader`).
- Canopy vs trunk masking via vertex colour ALPHA (trunk verts baked to alpha=0 = no sway; canopy verts baked to alpha=1 = full sway). This reuses the existing vertex-colour channel (currently carries per-blob VALUE for directional-light proxy in RGB; alpha is currently 1 everywhere, so assigning a sway mask to alpha is non-breaking if `_AOStrength = 0` on tree materials).
- A second crossed sine (perpendicular wind direction at a different frequency) gives organic canopy wiggle — same pattern already in `LowPolyVertexColor.shader`'s water swell (`_WaveAmp`, `_WaveLen`).

**LOD for trees:** Unity 6 GPU Resident Drawer supports distance-based LOD switching. Far Horizon's `BuildTree` currently places ~320 trees with NavMeshObstacle carve. LOD Group on the tree prefab (LOD0 = full mesh at <40u, LOD1 = trunk-only at 40–80u, Culled at >80u) is the recommended path if profiling shows the draw budget is hit. This is a future-PR concern given the low-poly vertex counts (~300–600 verts per tree including trunk + canopy blobs).

### Grass — rendering technique comparison

**Source 5** — Daniel Ilett, "Six Grass Rendering Techniques in Unity," danielilett.com, 2022-12-05. URL: https://danielilett.com/2022-12-05-tut6-2-six-grass-techniques/. **Strength: Moderate (well-sourced technical write-up, covers 6 options).** Summary of relevant options:

| Method | SRP Batcher | Wind | Perf (desktop) | Notes |
|---|---|---|---|---|
| Mesh Grass (scene objects) | Yes (batched) | Vertex shader | Good for moderate counts | Current path in Far Horizon |
| Procedural via Compute Shader | N/A (single draw call) | Vertex shader | Best for dense coverage | Requires compute support (DX11+); breaks SRP Batcher's CPU savings |
| Geometry/Tessellation Shader | No | Per-vertex | Poor; not recommended | Recomputes mesh every frame |
| Terrain Detail System | Instancing | Built-in limited | Good | Requires Unity Terrain object; not used in Far Horizon |
| Billboards | Yes | Static | Good for distance only | Reads as flat card up close |

Far Horizon uses the Mesh Grass path (existing `BuildGrassClump`, ~600–1500 instances). The SRP Batcher already batches them via `LowPolyVertexColor.shader`. Compute-shader-instanced grass is the perf ceiling route if density must scale to 5000+ blades; at Far Horizon's target density (~1500 clumps, each 7 blades = ~10k verts) the existing path is adequate.

**Source 6** — Cyanilux, "GPU Instanced Grass Breakdown," cyanilux.com, 2025. URL: https://www.cyanilux.com/tutorials/gpu-instanced-grass-breakdown/. **Strength: Strong (detailed technical, recently updated 2025).** 500,000 instances on a 256×256 grid = 89,000 after frustum culling → 500+ FPS on high-end hardware. The author notes the tradeoff: GPU instancing via `Graphics.RenderMeshPrimitives` + `ComputeBuffer` bypasses SRP Batcher (no per-material CBUFFER). For Far Horizon's scale (island radius ~150u, ~1500 clumps) the existing SRP Batcher path avoids the complexity; the compute path is the documented ceiling lever for future M-U4+ dense-foliage expansion.

**Source 7** — Unity 6 Manual, "Grass and other details." URL: https://docs.unity3d.com/Manual/terrain-Grass.html. **Strength: Strong (official).** The Unity Terrain Detail system uses instancing and is GPU-instancing-compatible as of Unity 2021.2+. However, Far Horizon's terrain is a procedural `MeshFilter` (`BuildIslandTerrainMesh`) not a `UnityEngine.TerrainData` asset; the Terrain Detail API cannot be applied without a full terrain object. Ruled out for this project.

### Sky — gradient skybox and cloud options

**Source 8** — `Assets/Shaders/GradientSkybox.shader` + `Assets/Scripts/Editor/QualityPassGen.cs` in this repo (read directly). **Strength: Strong (ground truth, shipped code).** The `FarHorizon/GradientSkybox` shader already implements a 3-stop vertical gradient (zenith `#7FB4D6` → mid `#AAD0E2` → horizon `#DCE8E4`) tuned to the Sponsor-approved "cheerful sky" (mid-point lowered to 0.18 post-soak). It is registered in `AlwaysIncludedShaders` and seam-killed: fog colour == `SkyHorizon` constant (`WorldLookPalette.SkyHorizon`). No further sky-shader work is needed or warranted — any change risks reopening the seam (fog colour drift).

**Source 9** — Unity Asset Store: "Polyverse Skies | Low Poly Skybox Shaders." URL: https://assetstore.unity.com/packages/vfx/shaders/polyverse-skies-low-poly-skybox-shaders-104017. **Strength: Weak (third-party paid asset, no benchmark).** A low-poly-specific skybox asset with 3-colour gradient + animated clouds. Not needed given the existing shipped shader; cited as evidence that the current approach is the standard community solution.

**Cloud approach:** The codebase already defines `LowPolyMeshes.CloudBlob()` — the same clustered-spheroid construction as `BlobCanopy` but with flat (hard) normals and a wider/flatter cluster for a chunky toy cloud read. These are scattered as world-space `GameObject`s in `LowPolyZoneGen`. The visual-polish option for clouds is:
- A slow world-space drift: a `CloudDrift` MonoBehaviour that advances `transform.position.x` by a constant wind speed (0.3–1.0 u/s) and wraps position when it exits the island bounding box back to the opposite side.
- No shader change required; `LowPolyVertexColor.shader` already handles the cloud mesh.
- Shadow casting: disable per-cloud `MeshRenderer` shadow casting (they are at altitude, far from ground; `unity6-mastery.md §3` recommends disabling shadow casting on small/distant objects).

---

## Application to Embergrave / Far Horizon

### Trees (`86cabc73q`) — Recommended approach

**Extend `LowPolyVertexColor.shader` with a canopy-sway toggle.** Add a `_SwayAmp` property (default 0 = no change to trunk material). In the vertex stage, when `_SwayAmp > 0`, sample `IN.color.a` (vertex alpha) as the sway mask and displace `posOS.xz` by a sine pair identical in structure to the existing wave swell:

```hlsl
if (_SwayAmp > 0.0) {
    float3 posWS0 = TransformObjectToWorld(posOS);
    float k = 6.2831853 / max(_SwayLen, 0.001);
    float t = _Time.y * _SwaySpeed;
    float sway = (sin(posWS0.x * k * 0.6 + t) + sin(posWS0.z * k * 0.8 + t * 1.2) * 0.7);
    float mask = IN.color.a; // 0 = trunk (baked), 1 = canopy
    posOS.x += sway * _SwayAmp * mask * 0.5;
    posOS.z += cos(posWS0.x * k + t * 0.9) * _SwayAmp * mask * 0.3;
}
```

The trunk `GrassClump` material keeps `_SwayAmp = 0`. The canopy `CanopyVertexColorMat()` instance gets `_SwayAmp ~ 0.08–0.15`. The vertex-colour alpha mask (baked in `BlobCanopy`) holds the spatial weighting — all live in the same `CBUFFER_START(UnityPerMaterial)` block for SRP Batcher compliance.

**No `MaterialPropertyBlock` anywhere** — all variation is world-position-seeded or global-uniform. GPU Resident Drawer remains eligible.

Bake alpha=0 into trunk vertices (in `LowPolyMeshes.TaperedCylinder`) and alpha=1 into canopy blob vertices (in `LowPolyMeshes.BlobCanopy`) so a single shared shader handles both mesh types with no per-object branch.

**LOD:** Defer to a profiling PR after M-U2 closes; current tree vert counts are low enough that 320 trees at 600 verts each = ~192k verts, well within a desktop GPU budget.

**Cost:** ~15 min shader edit + ~30 min alpha-bake in both mesh constructors + EditMode test verifying the sway is non-zero on canopy and zero on trunk. Low risk; `_SwayAmp = 0` default preserves every existing test's expected render.

### Grass (`86cabc737`) — Recommended approach

**Reuse the same `_WaveAmp` mechanism already in `LowPolyVertexColor.shader`.** The grass clump material already differs from the terrain material only in tint; setting `_WaveAmp ~ 0.04` on the grass material and `_WaveLen ~ 4` gives a short-period rustling wave. No new shader code needed at all — the existing water swell path works identically on grass quads.

The UV.y-based dampen (blade base stays fixed, tip sways) is not currently implemented in `GrassClump`'s geometry — all verts are at mixed heights. For the low-poly chunky style this is acceptable: the whole clump rocks gently, which reads as wind in a tuft (not a physically-correct bend). If per-blade bend is wanted, bake UV.y = 0 at base verts and 1 at tip verts in `GrassClump()` and use `IN.texcoord.y` as the sway mask instead of world-height.

**Density scaling:** current scatter places ~600 clumps. Doubling to 1200 with SRP Batcher is safe; beyond 3000 consider switching the grass clumps to a `Graphics.RenderMeshInstanced` call (breaking SRP Batcher for that one draw but collapsing N calls to 1). Threshold should be profiling-driven on the shipped exe, not assumed.

**Cost:** shader change is ~5 lines in the existing material setup in `BuildGrassClump`. Zero new shader files.

### Sky (`86cabc743`) — Recommended approach

**No shader change.** The `FarHorizon/GradientSkybox` shader is Sponsor-tuned, seam-killed, and shipping. The seam relies on `fogColor == SkyHorizon` as a locked constant; any sky stop change requires a paired fog colour update (the seam-kill is documented in `QualityPassGen.EnableGlobalFog()` and enforced by the `_FogCap` term on the water shader).

**Cloud drift** is the only additive option. `CloudBlob` meshes already exist on the island. Add a `CloudDrift.cs` MonoBehaviour:
- `[SerializeField] float driftSpeed = 0.5f; Vector3 _origin;`
- In `Update`: `transform.position += Vector3.right * driftSpeed * Time.deltaTime;`
- Wrap: when `transform.position.x > IslandRadius + 50`, teleport to `_origin.x - IslandRadius - 50`.
- Disable shadow casting on the cloud `MeshRenderer` in `WorldBootstrap`.

This gives the Sponsor a living sky without any shader changes or seam risk. The Sponsor gates whether this is wanted — research only.

**Ruled out:**
- Skybox texture cubemaps: incompatible with the procedural gradient colour control.
- Volumetric clouds (Altos, etc.): overkill for the chunky toy style; the CloudBlob mesh IS the right primitive.
- HDRI: no art pipeline for it; the gradient IS the style.

---

## Options matrix

| Surface | Option | SRP Batcher | Perf | New shader? | Evidence strength |
|---|---|---|---|---|---|
| Trees | Sine-sway via `_SwayAmp` in existing shader, vertex-alpha mask | Yes | Negligible | No (extend existing) | Moderate |
| Trees | Per-tree `MaterialPropertyBlock` for unique sway | No (breaks GPU Resident Drawer) | N/A | No | Strong (ruled out) |
| Trees | LOD Group (trunk-only at distance) | Yes | Saves ~60% verts at distance | No | Strong (doc) |
| Grass | `_WaveAmp` on existing grass material | Yes | Negligible | No | Strong (existing code) |
| Grass | `Graphics.RenderMeshInstanced` for 3000+ clumps | No | 1 draw call | Minimal | Moderate |
| Grass | Geometry shader | No | Poor | Yes | Weak (ruled out) |
| Sky | No change (gradient skybox stays) | N/A | Zero | No | Strong |
| Sky | CloudBlob drift MonoBehaviour | N/A | Negligible | No | Strong (existing mesh) |
| Sky | Third-party skybox asset | Varies | Unknown | External | Weak |

---

## Caveats and open questions for Sponsor

1. **Wind feel is a soak call.** `_SwayAmp` and `_SwaySpeed` values above are starting points; the Sponsor judges whether the sway reads "alive" vs "seasick." The recommended approach builds in the properties so the Sponsor can adjust them via the existing dev-tweak console (ticket `86ca...` — settings panel).
2. **Alpha channel reassignment in `BlobCanopy`.** Currently vertex colour alpha = 1 everywhere (not used). Reassigning alpha to `sway mask` is non-breaking IF `_AOStrength = 0` on canopy materials (which it is — AO is opt-in for rocks/props only). Confirm before committing the alpha bake.
3. **Cloud drift wrapping radius.** Needs to match `IslandShoreR` constant in `LowPolyZoneGen`; hardcoding is fragile. Pass the radius as a serialized field set at bootstrap time.
4. **LOD for trees** is not recommended as a first PR — profile first on the shipped build (a dense jungle at 320 trees with low-poly vert counts is unlikely to be the desktop bottleneck before other systems are added).

---

*Sources: NedMakesGames foliage shader (Medium, 2021) · Daniel Ilett six-grass techniques (danielilett.com, 2022) · Cyanilux GPU Instanced Grass (cyanilux.com, 2025) · Unity 6 Manual GPU Resident Drawer (docs.unity3d.com/6000.0/) · Unity 6 Manual Grass and other details (docs.unity3d.com) · polygon-wind GitHub (RenanBomtempo) · LMH Poly wind-shader tutorial · Polyverse Skies asset page · Existing Far Horizon shaders (ground truth): `Assets/Shaders/GradientSkybox.shader`, `Assets/Shaders/LowPolyVertexColor.shader`, `Assets/Scripts/Editor/LowPolyZoneGen.cs`, `Assets/Scripts/Editor/LowPolyMeshes.cs`, `Assets/Scripts/Editor/QualityPassGen.cs`*
