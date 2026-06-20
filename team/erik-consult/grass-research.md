# Stylized Grass — Technique Survey and Build Recipe

## Question

The Sponsor wants experimental ground grass (POC to soak) in Far Horizon's chunky warm
low-poly style. What technique best fits URP + our flat-faceted look + Windows desktop
performance + the orbit camera? How should density/LOD, ground-blending, and gentle
wind/sway be handled? What existing codebase routes apply?

---

## Bottom line

**Recommended: Rank 1 — Procedural mesh-scatter with a vertex-displacement Shader Graph wind
shader, built directly into the existing `LowPolyZoneGen / LowPolyMeshes` routes.** Our
`BuildGrassClump` + `GrassClump` mesh already exist and the approach is already proven for
spawn-centre clumps. The POC extends what is there: more clumps, a wider placement radius,
a two-pass LOD (full clumps ≤ 25u, suppressed > 50u), a vertex-displacement wind shader
on the blade tips, and a tight ground-color match. A GPU-instanced mass-field (Rank 2) is
the right option if the Sponsor's soak shows density is the bottleneck; shell texturing
(Rank 3) does not fit the faceted style and carries heavy overdraw.

---

## Evidence

### E-1 — The six techniques (danielilett survey)
- **Source:** Danielilett — "Six Grass Rendering Techniques in Unity" [https://danielilett.com/2022-12-05-tut6-2-six-grass-techniques/] — **Strong** (in-engine Unity demo,
  comprehensive; reproduced across many cited tutorials).

  Technique summary relevant to Far Horizon:

  | Technique | How it works | Desktop perf | Low-poly fit | Wind/sway | Verdict |
  |---|---|---|---|---|---|
  | **Mesh cards** (scatter) | Individual `MeshFilter` instances placed in world | Scales with count; GPU Resident Drawer gives one draw call per shared-material batch | Excellent — we fully control the mesh, normals, vertex color | Yes — vertex displacement in shader or baked LUT | **OUR EXISTING ROUTE** |
  | Geometry / tessellation shaders | Tessellate terrain, geometry shader emits blades | GPU overhead per frame; "geometry shaders left in the past"; platform incompatibility | Fragile normal output, hard to match faceted look | Complex | Ruled out |
  | **Procedural / compute** (`Graphics.DrawMeshInstanced` / `DrawMeshInstancedIndirect`) | Compute buffer of transform matrices, single draw call regardless of count | "Savagely efficient" — one draw call; 500FPS+ example at 500K blades | Fully controllable mesh shape | Wind via vertex shader in the instanced shader | Best option for mass-density (Rank 2) |
  | Billboard | Textured quads toward camera | Very low vertex count, suited for distant grass | Does NOT fit our style (we have no textures; pure vertex-color pipeline) | Limited | Ruled out |
  | Unity Terrain detail | Built-in terrain paint | Fast to author; instancing supported | Locked to terrain quad-strip, billboard under the hood; opaque to our vertex-color material | Limited | Incompatible — no Unity Terrain in our world |
  | Impostors | Pre-captured multi-angle atlas | Highest texture memory; offline pipeline | Requires texture atlas authoring pipeline (opposite of our vertex-color route) | None | Ruled out |

### E-2 — GPU instanced mesh grass in URP
- **Source:** Cyanilux — "GPU Instanced Grass Breakdown" [https://www.cyanilux.com/tutorials/gpu-instanced-grass-breakdown/] — **Strong** (cited by Unity documentation,
  well-reproduced tutorial with source code). Key finding: `Graphics.RenderMeshPrimitives` +
  per-instance transform buffer in a Structured Buffer. GPU frustum culling via compute
  shader reduces rendered instances from ~500K to ~89K in real play. One draw call regardless
  of blade count. Wind via `Time` into a `Sine` node on the vertex shader — "light swaying
  effect." Confirmed URP; Unity 2021.2+; Unity 6 improved the Instance ID node.

  Application to Far Horizon: this is Rank 2 — adopt it only if the POC soak reveals the
  mesh-scatter approach lacks density. Compute shader path requires `#pragma kernel` and
  a `ComputeShader` asset — more setup than extending `LowPolyMeshes`, but still within
  the procedural / no-texture / vertex-color pipeline.

### E-3 — Wind vertex displacement via Shader Graph (URP)
- **Source:** Arvind G — "Creating a Stylized Grass Shader Using Unity Shader Graph (Part 2)"
  [https://blog.arvindg.com/creating-a-stylized-grass-shader-using-unity-shader-graph-part-2]
  — **Moderate** (well-structured tutorial, Unity 2020.3 / Shader Graph 10.x, fully
  compatible with URP in Unity 6). Wind sub-graph: `Time → Sine` × `WindSpeed` for temporal
  frequency; `Position (XZ) → Sine` × `WindFreq` for spatial variation (adjacent blades sway
  slightly out of phase); the result is added as a vertex-position offset on blade tips only
  (UV.y > 0.5 or a height mask). Three parameters: `WindSpeed`, `WindFreq`, `WindDirection`.

  Application to Far Horizon: add a `FarHorizon/GrassBlade` Shader Graph (or extend the
  existing `LowPolyVertexColor` shader with wind keywords) with these three parameters.
  The same shader works on both the mesh-scatter (Rank 1) and the GPU-instanced path (Rank 2).

### E-4 — Shell texturing
- **Source:** Unity Discussions — "Shell Texturing in Shader Graph w/ URP"
  [https://discussions.unity.com/t/how-to-shell-texturing-in-shader-graph-w-urp/1490195]
  — **Weak** (forum post, no benchmark data, 2024). Shell texturing renders N copies of the
  base mesh at increasing offsets, masked by a noise texture. This produces a dense "fur"
  look. Limitation: overdraw is proportional to shell count × coverage. With a warm
  low-poly faceted mesh (no textures, vertex-color only) the noise mask is meaningless —
  the shells just stack our green vertex-color blades with no differentiation. The result
  reads as a solid green blob, not distinct blades. Not recommended.

### E-5 — Our existing GrassClump (production code, codebase read)
- **Source:** `Assets/Scripts/Editor/LowPolyMeshes.cs` lines 963–1041 and
  `Assets/Scripts/Editor/LowPolyZoneGen.cs` lines 599–616 — **Strong** (ground truth).

  What we have: `GrassClump(float height, int blades, int seed)` generates 5+ two-sided
  blades, each with its own front/back vertex sets (the iter-8 dark-shard fix: no shared
  verts, explicit up-biased normals, wider blades 0.11–0.16u half-width). `BuildGrassClump`
  scatters 360 clumps over the island interior, inland-biased. The blade color comes from
  the `GrassLo / GrassHi` palette, biased toward sunlit `GrassHi` so tufts pop off the
  terrain.

  What we do NOT have yet:
  - Wind/sway on the blades (static)
  - Density beyond 360 clumps (each ~7 blades = ~2520 blades total)
  - Per-blade LOD (clumps always rendered at full resolution)
  - Ground-color blending (current approach: blade tint is static grass-green regardless
    of what terrain zone it sits on)

### E-6 — Art direction: what the Sponsor expects
- **Source:** `inspiration/2026-06-12_21h22_33.png` (this repo, viewed directly) —
  **Strong** (Sponsor-set ground truth). Dense forest meadow at player height: grass
  blades plus wildflowers plus mushrooms along a worn dirt trail. The grass reads as
  distinct individual blades, not a continuous carpet — sparse enough to see through,
  dense enough to feel lush. Height roughly 0.2–0.5× the character knee height.

- **Source:** `inspiration/2026-06-12_21h13_31.png` (viewed directly) — **Strong**.
  Rolling grassland: grass is implied by the ground color gradient, not individual blades
  from orbit distance. Suggests the visible blade density matters only within ~30u of the
  camera — distant ground can read as terrain vertex color alone.

- **Source:** Memory `sponsor-prefers-natural-lively-motion` — **Strong** (project
  memory, confirmed across multiple soaks). "default to lively/animated (axe FOLLOWS
  the arm; water has MOVING waves; foam PULSES), only lightly damp — don't lock it
  static." Static grass will fail a soak. Wind sway is required.

---

## Application to Far Horizon

### Technique ranking (impact × fit, highest first)

**Rank 1 — Extend `BuildGrassClump` + `GrassClump` mesh + wind Shader Graph. Effort: ~4–6h.**

This is the build-in-our-routes recipe. No new system, no new asset pipeline.

Steps:
1. **Density lift.** In `LowPolyZoneGen.ScatterIslandProps`, raise `clumpTarget` from 360
   to 900–1200. At 7 blades per clump, 1000 clumps = ~7000 blades — a visible carpet
   without approaching the polygon budget concern. The existing `OnLandmass` / inland-bias
   logic stays. Use Poisson-disk scatter (the recipe from `low-poly-trees-research.md` §E)
   with `r_min = 1.2u` for clumps to spread them evenly.

2. **Two-pass LOD.** Wrap `BuildGrassClump` placement in a camera-distance gate: within
   25u, use full 7-blade clump; beyond 50u, skip entirely (the terrain vertex-color reads
   as a continuous green ground at orbit distance — the `21h13_31` orbit shot confirms this).
   This is a static LOD at bootstrap time, which is appropriate for our baked scatter
   approach; no runtime distance check is needed because the island is bootstrapped once.
   Alternative: use Unity LOD Group — attach an empty child past 50u — but this adds GO
   overhead per clump. Simpler: just skip distant clumps at bootstrap (the camera spawns
   near origin; most play happens in the centre ring).

3. **Wind Shader Graph.** Author `Assets/Shaders/GrassBlade.shadergraph` (URP Lit or
   Unlit base graph, vertex color input for the green). Wind sub-graph: `Time * WindSpeed
   → Sin → multiply by blade-height UV mask (UV.y, where base verts have UV.y=0,
   tip verts have UV.y=1) → add to Position as XZ offset` in the direction of `WindDir
   (Vector3 property)`. Add `WindFreq` spatial variation: `(Position.xz * WindFreq) →
   Sin → small offset`. Three exposed properties: `WindSpeed`, `WindFreq`, `WindDir`.
   Keep amplitude small (0.03–0.06u max) — the Sponsor wants lively-not-thrashing.
   Assign to `BuildGrassClump`'s material instead of the current flat-color mat.

   NOTE: `GrassClump` currently has no UVs set; `FinishWithNormals` returns a mesh with
   only verts/normals/tris. The wind shader needs a height mask. Two options: (a) bake
   the tip/base distinction into vertex COLOR alpha channel (set `a = 1` for tip verts,
   `a = 0` for base verts — alpha is unused by the current vertex-color shader); (b) add
   UV0.y = 0 at base, UV0.y = 1 at tip in `GrassClump`. Option (a) is the zero-extra-
   pipeline path (no UV set, uses the `Color.a` that `GrassClump` doesn't currently write).
   Option (b) is cleaner for the shader. Pick (b) — add `mesh.SetUVs(0, uvs)` after
   `FinishWithNormals`.

4. **Ground-color blending.** Pass the local terrain height to `BuildGrassClump` (already
   available from `GroundPoint`); lerp blade color between `GrassLo` (lower ground, shadowed)
   and `GrassHi` (raised ground, sunlit) based on the Perlin height at that point — mirroring
   the terrain `IslandColorAt` grass ramp. Currently the blade color is independently sampled
   from the GrassLo/GrassHi ramp with a random bias. Use `IslandColorAt` as a single source
   of truth: call `IslandColorAt(x, z, GroundPoint.y, ox, oz, seed)` and pull out the grass
   component for the blade tint. This ensures blade color tracks terrain color.

**Rank 2 — GPU-instanced mass grass field. Effort: ~8–12h. For future sprint if Rank 1 density is insufficient.**

Follow Cyanilux's `Graphics.DrawMeshInstancedIndirect` approach. A `GrassManager` MonoBehaviour
holds a `ComputeBuffer` of `Matrix4x4` positions (built once from a Poisson-disk field
restricted to `OnLandmass`). The wind is applied per-vertex in a URP unlit shader that reads
instance transform from the buffer. Frustum culling in a compute shader removes blades outside
the camera frustum each frame.

Effort is higher but the approach scales to 50K+ blades on the island without a draw-call
penalty. Fit: still within the vertex-color / no-texture pipeline (the instanced shader just
reads a flat green color from a per-instance color channel in the buffer). Recommended only
if the Sponsor's soak of Rank 1 finds density lacking at close range.

Do NOT dispatch this in parallel with Rank 1 POC — it is a second implementation of the same
visual feature and would conflict in the same `ScatterIslandProps` pass.

**Rank 3 — Shell texturing. Not recommended.**

Shell texturing's visual output depends on a noise/alpha mask that punches holes in each
shell to simulate blade spacing. Our vertex-color-only pipeline has no texture input, so
every shell would be fully opaque green — resulting in a stacked solid blob, not grass. The
overdraw cost (N shells × island grass coverage) would be significant with no style benefit.
Ruled out.

---

### Density and LOD guidance

| Distance from camera | Approach | Rationale |
|---|---|---|
| 0–12u (close, player feet) | Full 7-blade clumps, full wind | Player clearly sees individual blades |
| 12–25u | 5-blade clumps, full wind | Board (21h22_33) shows visible grass to mid-range |
| 25–50u | Optional: reduce to 3-blade clumps, half wind amplitude | Transition zone |
| >50u | Skip clumps entirely | Terrain vertex-color reads as grass from orbit; confirmed by 21h13_31 |

For the POC soak, use a single density tier (360 → 900 clumps, 7 blades each, no per-distance
variation) and let the Sponsor judge. Add LOD tiers only if soak feedback flags the distant
carpet as "too sparse" or "too heavy."

### Wind sway numbers (starting values for the POC)

These are starting points for Shader Graph properties; the Sponsor dials them at soak:

| Property | Starting value | Rationale |
|---|---|---|
| `WindSpeed` | 0.8 | ~1 cycle/1.2s — perceptibly alive, not frantic |
| `WindFreq` | 0.4 | Neighbours sway slightly out of phase — not a uniform carpet |
| `WindDir` | `(1, 0, 0.3)` (normalized) | A diagonal from the sea (warm-island feel) |
| Max tip offset | 0.04u | Visible at close range; blade height is ~0.3–0.5u so 0.04u = 8–13% lean |

The Sponsor noted for the water foam: "PULSES". Apply the same principle: a low-frequency
modulation of `WindSpeed` amplitude (`Sin(Time * 0.2) * 0.3 + 0.7` multiplied onto the
sway magnitude) so the grass appears to breathe in and out — gusts rather than constant
metronome sway.

---

### Codebase entry points (what to grep / read before implementing)

- `Assets/Scripts/Editor/LowPolyMeshes.cs` — `GrassClump` (lines 988–1041): extend here
  for UV addition and any blade-geometry changes.
- `Assets/Scripts/Editor/LowPolyZoneGen.cs` — `ScatterIslandProps` (lines 520–616), specifically
  `BuildGrassClump` (lines 703–716): clump count, distribution, and color source.
- `LowPolyZoneGen.IslandColorAt` — the terrain color function; reuse for blade ground-tint.
- `LowPolyZoneGen.GroundPoint` — raycast for the terrain Y at a scatter point; already used.
- `team/erik-consult/low-poly-trees-research.md §E` — Poisson-disk scatter recipe (the same
  `PoissonScatter2D` helper covers both trees and grass placement).

---

### What NOT to do

- **Do NOT add UV coordinates as a TexCoord that another system expects to be 0.** Check
  that no existing mesh inspector or test pins `GrassClump.uv == null`. The `LowPolyMeshTests.cs`
  file covers geometry; verify the test before adding UVs.
- **Do NOT use `MaterialPropertyBlocks` on individual clumps.** GPU Resident Drawer disqualifier
  (`unity6-mastery.md §2`). All clumps must share the same material instance; the wind
  properties are global uniforms, not per-instance overrides.
- **Do NOT cast shadows from grass.** 900 clumps × shadow-map contribution = significant cost.
  Disable `ShadowCastingMode.Off` on all grass MeshRenderers (the existing `BuildGrassClump`
  does not set this explicitly — dev should add it).
- **Do NOT make the wind shader a URP Lit graph.** `LowPolyVertexColor` is URP Unlit
  (lighting handled in vertex color, not the PBR path). Keep grass unlit for consistency and
  for the SRP Batcher (all grass on one shader variant = one SRP Batcher batch).

---

### POC soak recipe

1. Raise `clumpTarget` from 360 → 900. Apply Poisson-disk scatter with `r_min = 1.2u`.
2. Add UV0.y height mask to `GrassClump` mesh.
3. Author `GrassBlade.shadergraph` with the three wind properties and assign to grass clumps.
4. Set all grass MeshRenderers to `ShadowCastingMode.Off`.
5. Bootstrap + `serve_soak.sh` → capture from gameplay-orbit cam at ~15u from the player spawn.
6. Present to Sponsor with the wind property sheet: "adjust WindSpeed and WindDir, then lock."

The Sponsor approves wind feel before integration of Rank 2 density path.

---

*Committed-artifact note: this research note must be on `main` before any ticket that cites it
as LOCKED authority merges (per Erik's committed-artifact citation rule).*
