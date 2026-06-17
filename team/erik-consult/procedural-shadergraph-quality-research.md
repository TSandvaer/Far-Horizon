# Procedural Mesh + URP Shader Graph — Quality Uplift Research

## Question

We are committed to in-house procedural mesh generation + URP custom shaders (Sponsor declined paid AI-3D
tools, 2026-06-15). How do we level up the quality of what these routes already produce for the chunky
faceted flat-shaded low-poly look (orbit camera, URP, Windows desktop)? Ranked by impact-vs-effort, with
concrete "adopt at `<file>`" sketches.

## Bottom line

The codebase already implements several hard-won patterns correctly (outward winding enforcement,
per-face normals on FacetedRock/CloudBlob, up-biased foliage normals, per-blob vertex-color AO on
trees, seam-kill fog). The five highest-impact remaining wins are: **(1)** a ddx/ddy flat-shading mode
added to `LowPolyVertexColor.shader` — eliminates the entire winding-inversion bug class for any prop
that opts in; **(2)** depth-fade intersection foam in a new `LowPolyWater.shader` — the biggest single
read-quality gap in the current ocean; **(3)** the `QuantizeFine` pending merge for near-neutral props
(confirmed code-path bug, fix already authored); **(4)** a Fresnel/rim term in the existing shader —
1-line addition, free for all props; **(5)** chamfer-highlight geometry in Blender MCP for hero props
(the "white edge plane" the board shows on axes and tools). Toon hard-band ramp and screen-space outlines
are explicitly ruled out — they fight the existing look.

---

## Evidence

### A — Flat-shading mode via ddx/ddy (fragment-shader derivative trick)

- **Source:** Hextant Studios, "Rendering Flat-Shaded / Low-Poly Style Models in Unity"
  [https://hextantstudios.com/unity-flat-low-poly-shader/] — **Strong** (step-by-step, Unity URP,
  reproducible). Core technique: `normalize(cross(ddy(worldPositionWS), ddx(worldPositionWS)))` in
  the fragment shader computes the true per-face normal from the triangle geometry at runtime. No mesh
  topology change needed; vertex count stays minimal (welded mesh OK).

- **Why it matters for Far Horizon:** `FacetedRock`, `CloudBlob`, and `FacetedMountain` in
  `LowPolyMeshes.cs` currently use the explicit-per-face-normal approach (every triangle emits its own
  3 verts with the baked face normal). This works but tripled vert count is required, and winding must
  be forced outward manually (`Vector3.Dot(fn, faceCentre) < 0` flip — the same pattern appears 4
  times in the file). The ddx/ddy approach renders the same flat look WITHOUT needing unwelded verts or
  outward-winding enforcement — the fragment only runs on visible (front-facing) triangles, so a
  winding-inverted face is simply culled and never computes a wrong normal. It does not remove the need
  to fix inverted windings (culled faces are still invisible), but it removes the hidden danger of a
  winding flip that PASSES the editor but shades dark in the shipped build.

- **Implementation note:** `IN.positionWS` already passes through the shader as `TEXCOORD1`. The
  additional derivative instructions add ~2 ALU ops per fragment — negligible at desktop resolution
  on URP Forward+. Expose as `_FlatShading (Toggle) = 0` so the flag-off default leaves terrain,
  canopy, and water unaffected (smooth normals stay intact for the welded terrain roll).

### B — Depth-fade intersection foam for water

- **Source:** Cyanilux, "Depth Shader Tutorials for URP"
  [https://www.cyanilux.com/tutorials/depth/] — **Strong** (deep technical, covers HLSL + Shader
  Graph paths, URP-specific, cited across multiple shipped URP projects).

- **Source:** Daniel Ilett, "Unity Shader Graph Basics Part 8 — Scene Intersections" (2024-05-21)
  [https://danielilett.com/2024-05-21-tut7-12-intro-to-shader-graph-part-8/] — **Strong** (current,
  step-by-step, Unity 6 notes included, Shader Graph and HLSL paths both covered).

- **Source:** ameye.dev, "Stylized Water Shader"
  [https://ameye.dev/notes/stylized-water-shader/] — **Moderate** (shipped-title postmortem,
  confirms depth-fade foam is the industry standard for stylized water shore lines).

- **What it says:** Sample `SampleSceneDepth(screenUV)` → `LinearEyeDepth(rawDepth, _ZBufferParams)`,
  subtract the fragment's own eye depth (`IN.positionCS.w`), divide by `_FoamDistance` (~1.5u),
  saturate → a 0→1 mask that is 1 near any intersecting object (beach, rock, stump) and 0 in open
  water. Lerp toward `_FoamColor` (near-white warm) where mask is high.

- **Critical prerequisite:** The water shader **must be Transparent queue** (2501+). Opaque shaders
  cannot sample the depth texture they are simultaneously writing to — Unity saves the opaque depth
  buffer only after all opaques finish, before transparents begin. This is the main reason the current
  `LowPolyVertexColor.shader` (Tags: `Queue=Geometry`) cannot be extended in-place; a new
  `LowPolyWater.shader` derived from it is needed, adding `"Queue"="Transparent"` + `Blend SrcAlpha
  OneMinusSrcAlpha` + `ZWrite Off`. The URP Asset must also have `Depth Texture` + `Opaque Texture`
  enabled (both are checkbox toggles, zero render cost on a desktop target).

- **Compose with the existing baked foam:** `LowPolyZoneGen` bakes a static `FoamEdge` vertex-color
  band on the terrain mesh at the static waterline. The depth-fade foam from the water-shader side is
  COMPLEMENTARY — it catches dynamic intersections (rocks, stumps, future piers) that the static band
  can't. Both coexist.

- **Opaque water tradeoff note** (`unity-conventions.md` §Build stripping / URP water): the current
  opaque water was intentionally chosen to preserve Exp² fog composition (transparent surfaces don't
  compose with URP fog the same way, re-opening the sea↔sky horizon problem). Moving water to
  Transparent reopens that. Mitigation: the `_FogCap` mechanism already in `LowPolyVertexColor.shader`
  can be ported into the new transparent water shader — the shader applies the fog-cap manually rather
  than relying on the opaque fog path. This keeps the teal-at-horizon intact.

### C — Toon ramp lighting / Fresnel rim (additive term)

- **Source:** Delt06/Daniel Ilett, "Toon Shaders Pro for URP" [https://danielilett.com/toon-shaders-pro/toon/]
  — **Moderate** (well-produced, widely shipped in Unity community, covers Fresnel power + ramp in
  Shader Graph and HLSL).

- **Source:** Minions Art, "Toon Shader Lighting Update (BIRP & URP)"
  [https://www.patreon.com/posts/toon-shader-birp-59854502] — **Moderate** (broadly cited in Unity
  stylized discussions, covers the rim/Fresnel idiom).

- **What it says:** A cheap Fresnel term is `pow(1 - saturate(dot(normalWS, viewDirWS)), rimPower)`.
  At `rimPower ~2–3` it gives a soft wrap-around highlight on object silhouettes. At `rimPower ~6–8`
  it produces a thin bright outline. The board's tool/prop family (`inspiration/21h08_08` axe,
  `21h06_54` pickaxe) shows a white-plane read on the top edge of props — lower powers (~2) on the
  whole shader would approximate this effect on organic shapes; it is NOT an exact substitute for a
  chamfer plane (see D), but it is a free upgrade for any prop that never gets a Blender pass.

- **What NOT to adopt:** A toon hard-band ramp (step function on ndotl turning lighting into 2 solid
  bands) fights the existing smooth-faceted look the Sponsor approved. The Zone-D look is faceted
  SMOOTH shading (continuous diffuse gradient over coarse polygons), not cel-shaded bands. Keep the
  existing `ndotl * mainLight.color * shadowAttenuation + SampleSH` path. The Fresnel addition is
  purely additive and can be turned to `_RimIntensity = 0` by default so no props regress.

### D — White-edge highlight as chamfer geometry in Blender (hero props)

- **Source:** RetroStyleGames, "Low Poly Game Art: An Ultimate Guide"
  [https://retrostylegames.com/blog/low-poly-game-art-an-ultimate-guide/] — **Moderate** (practitioner
  guide, widely cited in stylized-art community, covers the geometry-edge highlight idiom).

- **Source:** Inspiration board direct observation: `inspiration/2026-06-12_21h08_08.png` (axe),
  `21h06_54.png` (pickaxe), `21h07_20.png` (sword) — **Strong** (ground truth). The white highlight
  on the axe head is a DISCRETE bright polygon face on the top edge, not a Fresnel wrap on the body.
  It reads as a caught-sun chamfer. Pure shader Fresnel produces a wrap around the entire silhouette;
  the board shows a flat bright plane on ONE specific face (the axe bevel) — that requires geometry.

- **Route in our pipeline:** Blender MCP `execute_blender_code` adds a bevel/chamfer face on the top
  edge of the axe head geometry with a distinct material index carrying `Color(0.92, 0.90, 0.84)`.
  The existing `AxeAssetGen.cs` drives the Blender creation route. Future props (campfire, stump,
  chest) follow the same pattern. This is authoring-time work per prop, not a runtime shader change.

### E — Vertex-color AO baking for crevice depth on rocks/props

- **Source:** Delt06/vertex-ao [https://github.com/Delt06/vertex-ao] — **Moderate** (open-source,
  reproducible, URP-compatible AO baker for Unity).

- **Source:** sundaysundae.co, "How to Make Low Poly Look Good"
  [https://sundaysundae.co/how-to-make-low-poly-look-good/] — **Moderate** (practitioner, covers AO
  baking to vertex color as a standard low-poly quality technique).

- **What it says:** Baking a geometric AO value into vertex color alpha in Blender (`bake` operator,
  AO type, read into vertex colour) adds subtle self-shadowing at crevices/joints with zero runtime
  cost — it is a constant baked value, not a per-frame computation. In `LowPolyVertexColor.shader`
  this costs one `lerp` per fragment: `finalCol *= lerp(1.0, IN.color.a, _AOStrength)`. Default
  `_AOStrength = 0` (complete no-op for all existing terrain, canopy, and water, which carry no AO
  in their vertex alpha). Rock props and stumps gain contact-shadow depth at crevices.

- **Current state:** `FacetedRock` in `LowPolyMeshes.cs` already bakes a per-facet VALUE step into
  vertex color RGB (light tops, darker sides). That is a directional-light proxy, not AO. Adding
  Blender-baked AO into vertex alpha would be additive — it gives the CONCAVE geometry near the
  ground contact point the dark contact-shadow read the board's rocks show.

### F — Fine quantizer for near-neutral props (confirmed code-path bug — pending merge)

- **Source:** `unity-conventions.md` §Low-poly mesh patterns, "The coarse 12-step palette quantizer
  splits NEAR-NEUTRAL warm tints into a pink `R>G=B` cast" — **Strong** (empirically observed,
  diagnosed via `TINTDIAG` instrumentation, fix authored on `drew/rocks-sourced` branch at `d358176`,
  ticket `86ca8m5zu`). This is a confirmed bug, not research speculation.

- **What it says:** `LowPolyZoneGen.cs Quantize()` snaps each colour channel to 12 steps. A near-grey
  warm ramp (R≈G≈B, chroma < 0.1) snaps to `(0.667, 0.583, 0.583)` = R>G=B = pink. Fix: use the
  24-step `QuantizeFine` for any prop whose `max(R,G,B) - min(R,G,B) < 0.10`. Already implemented
  and tested on the unmerged branch.

### G — Seeded shape variation for scatter (existing pattern, extend going forward)

- **Source:** `LowPolyMeshes.cs` — `FacetedRock`, `MessyHairCap`, `FacetedMountain`, `BlobCanopy`
  all accept a `seed` parameter; `LowPolyZoneGen.cs` passes distinct seeds per scatter instance
  (explicit `seed: i + 3701` etc.). **Strong** (in-codebase, confirmed working).

- **What it says:** The seed-per-instance pattern already gives variation in shape, proportion, and
  vertex-color. This is the right approach. The opportunity: pine trees currently use a `TaperedCylinder`
  + `FacetedSphere` stack — adding a seeded LEAN (a small xz translation of the apex) and a
  seeded height ± 20% would increase variety at zero new shader cost. Similarly, rock scatter in
  `LowPolyZoneGen.BuildRock` passes a `seed: i` sequence — adding a seeded Y-rotation (achieved by
  rotating the mesh or the GameObject) ensures rocks never align identically.

---

## Application to Far Horizon — Ranked recommendations

**Rank 1 — Ship `QuantizeFine` merge. Effort: zero new code. Impact: high (all near-neutral props).**
The pink-cast bug is confirmed, the fix is authored and tested on `drew/rocks-sourced`. Every near-neutral
rock, trunk, and stump is affected. Merge the PR.
- File: `Assets/Scripts/Editor/LowPolyZoneGen.cs` `Quantize()` → `QuantizeFine()` for chroma < 0.1.

**Rank 2 — Add flat-shading mode to `LowPolyVertexColor.shader`. Effort: 2–4h. Impact: high (props/rocks).**
Add `_FlatShading (Toggle) = 0` property. In frag, when flat-shading is on, override normalWS:
```hlsl
// PATTERN ONLY — not production code
#if _FLATSHADING_ON
    float3 ddxPos = ddx(IN.positionWS);
    float3 ddyPos = ddy(IN.positionWS);
    float3 normalWS = normalize(cross(ddyPos, ddxPos));
#else
    float3 normalWS = normalize(IN.normalWS);
#endif
```
`IN.positionWS` is already in the Varyings struct (`TEXCOORD1`). The shader keyword keeps the
non-flat path free of any ddx/ddy cost. Rock and future Blender-MCP props set this toggle on their
material instance; terrain/water/canopy are unaffected (toggle off by default).
- File: `Assets/Shaders/LowPolyVertexColor.shader` (Properties block + frag stage).

**Rank 3 — Add depth-fade intersection foam (new `LowPolyWater.shader`). Effort: 4–8h. Impact: high for ocean read.**
Steps:
1. Enable `Depth Texture` + `Opaque Texture` in the active URP Asset (Project Settings → Graphics).
2. Author `Assets/Shaders/LowPolyWater.shader`: fork `LowPolyVertexColor.shader`, change Tags to
   `"Queue"="Transparent" "RenderType"="Transparent"`, add `Blend SrcAlpha OneMinusSrcAlpha` + `ZWrite Off`.
3. In frag: sample scene depth, compute intersection mask, lerp toward foam colour.
4. Port the `_FogCap` fog-floor logic (present in `LowPolyVertexColor.shader` lines 131–145) into the
   new water frag to preserve the sea↔sky teal-at-horizon read (the opaque fog trick is gone; the cap
   must be applied manually in the transparent path).
5. Register in `AlwaysIncludedShaders` at bootstrap (same pattern as `LowPolyVertexColor.shader` in
   `WorldBootstrap.EnsureShaderAlwaysIncluded`).
- File: new `Assets/Shaders/LowPolyWater.shader` + `WorldBootstrap.cs` registration call.

**Rank 4 — Add Fresnel/rim term to `LowPolyVertexColor.shader`. Effort: 1–2h. Impact: medium (all props).**
One property + one line in frag:
```hlsl
// PATTERN ONLY — not production code
_RimColor ("Rim Color", Color) = (0.95, 0.92, 0.85, 1)
_RimPower ("Rim Power", Float) = 3
_RimIntensity ("Rim Intensity", Float) = 0
// in frag, after finalCol is assembled:
float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
finalCol += _RimColor.rgb * rim * _RimIntensity;
```
`_RimIntensity` defaults to 0 — zero cost on all current terrain/canopy/water materials. Per-prop
material instances opt in.
- File: `Assets/Shaders/LowPolyVertexColor.shader` (Properties block + frag, after the fog-cap block).

**Rank 5 — Chamfer-highlight geometry in Blender MCP for hero props. Effort: ~1h per prop. Impact: high fidelity on specific props.**
The white-edge plane on the board's axe (`21h08_08`) is a discrete chamfer face with a distinct
material slot carrying near-white colour (~`0.92, 0.90, 0.84`). Author this in Blender MCP when
creating or revising any hero prop (axe, campfire, stump). The Fresnel (Rank 4) is the fallback for
props that skip the Blender pass; the chamfer is exact-board-match for props that get it.
- Applies to: any `execute_blender_code` prop-creation script (especially `AxeAssetGen.cs` successor).

**Rank 6 — Vertex-color AO alpha baking in Blender for rocks/props. Effort: 2–4h (Blender bake + shader 1-line). Impact: medium.**
Bake Blender AO to vertex color alpha before FBX export. Add `_AOStrength (Float) = 0` +
`finalCol *= lerp(1.0, IN.color.a, _AOStrength)` in the frag. Default off — no regression on
existing terrain/canopy. Rock and stump material instances set `_AOStrength ~0.5` for contact depth.
- File: `Assets/Shaders/LowPolyVertexColor.shader` (1 line in frag) + Blender MCP bake script.

**Rank 7 — Seeded lean + height variation on scatter instances. Effort: <1h. Impact: low–medium (scene diversity).**
In `LowPolyZoneGen.cs` scatter loops, apply a seeded Y-axis rotation and ±20% height scale per
instance on pine trunks and rocks. Currently rocks are seeded per shape but never rotated, so groups
can look aligned. A `go.transform.Rotate(0, rnd.Next(360), 0)` per scatter instance costs nothing.
- File: `Assets/Scripts/Editor/LowPolyZoneGen.cs` scatter placement loops.

---

## What NOT to do

- **Toon hard-band ramp (Step node on ndotl):** the board is smooth-shaded, not cel-shaded. Hard light
  bands fight the chunky-faceted look the Sponsor approved. The existing `ndotl + SH` path is correct.

- **Screen-space outlines (Sobel depth/normals Renderer Feature):** expensive at desktop resolution
  on a large island scene; the board's "edge highlight" is a geometry-specific chamfer plane (one face
  on the axe), not a full-silhouette outline. Reserve for a future explicit decision.

- **Converting welded terrain to flat-shaded (unwelded):** the welded terrain grid + smooth normals IS
  the Zone-D dune look (soft rolling gradient). Flat-shading the terrain reads as a spike polyhedron.
  Only props/rocks benefit from the ddx/ddy flat mode.

- **Transparent water without porting `_FogCap`:** moving water to Transparent queue without manually
  reapplying the fog-floor logic reopens the sea↔sky teal-at-horizon problem that the opaque path
  (`unity-conventions.md` §SRP-Batcher) was specifically built to solve.

---

## Current codebase baseline (what is already correct — do not regress)

| Pattern | Where | Status |
|---|---|---|
| Outward winding enforcement on flat-shaded meshes | `FacetedRock`, `CloudBlob`, `FacetedMountain`, `FacetedLandmass` in `LowPolyMeshes.cs` | Correct — do not simplify the `Dot(fn, faceCentre)` flip. |
| Per-face explicit normals (flat-shaded meshes) | Same + `FacetedRock.SetNormals()` — explicit, not `RecalculateNormals` | Correct — do not call `RecalculateNormals` on flat-shaded meshes. |
| Up-biased normals on foliage blades | `GrassClump` `nUp = (Vector3.up * 0.85f + outward * 0.15f)` | Correct. |
| Distinct verts per face for double-sided foliage | `GrassClump` front + back verts | Correct — removing this reverts the iter-8 dark-shard bug. |
| Per-blob vertex-color AO proxy on canopy | `BlobCanopy` height-keyed RGB value blend | Correct (3-value green). |
| Seeded shape variation per instance | `FacetedRock`, `MessyHairCap`, `BlobCanopy`, `FacetedMountain` | Correct. |
| SRP-Batcher compliance (all props inside cbuffer) | `LowPolyVertexColor.shader` lines 60–66 | Correct — every new property must go inside `CBUFFER_START(UnityPerMaterial)`. |
| `_FogCap` fog-floor (teal water at horizon) | `LowPolyVertexColor.shader` lines 131–145 | Correct — port to any water shader variant, never remove. |
| Seam-kill: fog colour == `WorldLookPalette.SkyHorizon` | `QualityPassGen.EnableGlobalFog()` | Correct — lock to the single constant, never drift. |
