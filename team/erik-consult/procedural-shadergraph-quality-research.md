# Procedural Mesh + URP Shader Graph — Quality Uplift Research

## Question

We are committed to in-house procedural mesh generation + URP custom shaders (Sponsor declined paid AI-3D tools, 2026-06-15). How do we level up the quality of what these routes already produce — the chunky faceted flat-shaded low-poly look (orbit camera, URP, Windows desktop)? Ranked by impact-vs-effort, with concrete "adopt at `<file>`" sketches.

## Bottom line

Three changes deliver the most visible uplift with the least disruption to the existing pipeline: **(1)** add a flat-shading mode to `LowPolyVertexColor.shader` via the ddx/ddy derivative trick (hardware-computes per-face normals in the frag shader, zero mesh-topology changes, eliminates the recurring winding-inversion bug class by construction), **(2)** extend the water shader with a depth-fade intersection foam line using URP's `Scene Depth` + `Opaque Depth Texture` (the biggest single read-quality gap in the current ocean), and **(3)** adopt a 24-step fine quantizer for near-neutral props (the `QuantizeFine` fix from the conventions doc, currently pending on `drew/rocks-sourced` — merge it). Secondary wins: a Fresnel/rim pass and geometry-baked AO via vertex alpha for chunkier props in future Blender exports.

---

## Evidence

### A — Flat-shading via ddx/ddy (the derivative trick)

- **Source:** Hextant Studios, "Rendering Flat-Shaded / Low-Poly Style Models in Unity" — [https://hextantstudios.com/unity-flat-low-poly-shader/](https://hextantstudios.com/unity-flat-low-poly-shader/) — **Strong** (step-by-step, specific to Unity URP, reproducible).
- **Source:** Unity forum thread "Procedurally generated flat shaded mesh" — [https://forum.unity.com/threads/procedurally-generated-flat-shaded-mesh.436653/](https://forum.unity.com/threads/procedurally-generated-flat-shaded-mesh.436653/) — **Moderate** (community-verified pattern, widely referenced).
- **What it says:** Two routes exist for flat/per-face shading. Route A (unwelded vertices, explicit per-face normals in the mesh) requires tripling vertex count and is exactly the recurring winding-inversion bug surface (`unity-conventions.md` §Low-poly mesh patterns: the −Z grid bug, the FacetedRock bug — both are outward-winding failures on builder-assigned normals). Route B (fragment-shader ddx/ddy) computes `normalize(cross(ddy(worldPositionWS), ddx(worldPositionWS)))` in the fragment stage — the GPU differentiates position across the triangle automatically, giving the true face normal. Vertex count stays minimal (welded mesh), normal assignment is zero, winding-inversion bugs on explicitly-assigned normals are structurally impossible.

### B — URP depth-fade / intersection foam for water

- **Source:** Cyanilux, "Depth Shader Tutorials for URP" — [https://www.cyanilux.com/tutorials/depth/](https://www.cyanilux.com/tutorials/depth/) — **Strong** (deep technical, covers HLSL + Shader Graph paths, URP-specific).
- **Source:** Daniel Ilett, "Unity Shader Graph Basics Part 8 — Scene Intersections" (2024-05-21) — [https://danielilett.com/2024-05-21-tut7-12-intro-to-shader-graph-part-8/](https://danielilett.com/2024-05-21-tut7-12-intro-to-shader-graph-part-8/) — **Strong** (step-by-step, URP, current Unity 6 notes included).
- **Source:** ameye.dev, "Stylized Water Shader" — [https://ameye.dev/notes/stylized-water-shader/](https://ameye.dev/notes/stylized-water-shader/) — **Moderate** (comprehensive shipped-title postmortem on stylized water; 403 on direct fetch but content confirmed via search result snippets).
- **What it says:** The `Scene Depth (Eye)` node samples the opaque depth buffer; subtracting the fragment's own clip-space W gives intersection distance. Where the difference is small (beach/shore edge), apply a foam color overlay. **Prerequisite:** `Depth Texture` + `Opaque Texture` must be enabled in the URP Asset (Project Settings → Graphics → URP Asset). The water shader must be Transparent queue (2501+) or force `Render Queue = Transparent` — opaque shaders cannot read the depth texture they are writing. This is a one-setting enable + shader extension, not a mesh change.

### C — Toon ramp lighting / rim Fresnel

- **Source:** DElt06 / Daniel Ilett, "Toon Shaders in Unity — From Shader Graph to Custom HLSL" — [https://medium.com/@chitranshnishad27/toon-shaders-in-unity-from-shader-graph-to-custom-hlsl-08252b2d64a2](https://medium.com/@chitranshnishad27/toon-shaders-in-unity-from-shader-graph-to-custom-hlsl-08252b2d64a2) — **Moderate** (well-produced tutorial, community-tested).
- **Source:** Minions Art, "Toon Shader Lighting Update (BIRP & URP)" — [https://www.patreon.com/posts/toon-shader-birp-59854502](https://www.patreon.com/posts/toon-shader-birp-59854502) — **Moderate** (widely shipped, referenced by multiple Unity toon-shader discussions).
- **What it says:** A cheap rim/Fresnel pass is `saturate(1 - dot(normalWS, viewDirWS))` raised to a power. At low power (~2) on props it gives the "light wrapping around the silhouette" read that matches the board's white-edge-highlight language on the tools/props family (`inspiration/21h08_08` axe, `21h06_54` pickaxe). This can be added as an additive term in the existing `LowPolyVertexColor.shader` frag function — one line of HLSL — exposed as `_RimPower` and `_RimColor` properties defaulting off (0 intensity, no production regression).

### D — White-edge highlight as explicit chamfer plane in Blender

- **Source:** RetroStyleGames, "Low Poly Game Art: An Ultimate Guide" — [https://retrostylegames.com/blog/low-poly-game-art-an-ultimate-guide/](https://retrostylegames.com/blog/low-poly-game-art-an-ultimate-guide/) — **Moderate** (practitioner guide, well-cited in stylized-art community).
- **Source:** Sunday Sundae, "How to Make Low Poly Look Good" — [https://sundaysundae.co/how-to-make-low-poly-look-good/](https://sundaysundae.co/how-to-make-low-poly-look-good/) — **Moderate** (practitioner, covers lighting, silhouette, specularity for low-poly).
- **What it says:** The white-edge highlight on the board's tool props (`21h08_08` axe: white plane on the top edge of the head) is NOT a shader Fresnel on the body — it is a discrete bright/near-white polygon inset as the top-facing chamfer/bevel face of the axe head, with a separate material slot carrying a near-white colour. This is a **Blender-authored geometry decision**, not a Unity shader trick. The equivalent in our route: Blender MCP `execute_blender_code` adds a `bevel modifier` (or manual face on the top edge of the axe head geometry) with a separate material index carrying `Color(0.92, 0.90, 0.84)`. Applied to future props authored via `AxeAssetGen.cs` / Blender MCP output. Pure shader Fresnel reads differently (wraps the whole silhouette); the board shows a flat bright plane on one specific face — that specificity requires geometry, not a wrap.

### E — Vertex-color AO baking for chunkier props

- **Source:** Delt06 / vertex-ao GitHub — [https://github.com/Delt06/vertex-ao](https://github.com/Delt06/vertex-ao) — **Moderate** (open-source, reproducible, URP-compatible).
- **Source:** Unity Forum "Procedurally generated flat shaded mesh" (thread above) — **Moderate**.
- **What it says:** Baking a simple geometric AO value into vertex color (alpha channel, or a spare RGB channel) at Blender export time — using the Blender `bake` operator with AO type, read into vertex colour — adds subtle self-shadowing at crevices/joints without any runtime cost. The existing `LowPolyVertexColor.shader` already reads vertex color RGB; an alpha-channel AO multiplier (`finalCol *= lerp(1.0, vertexColor.a, _AOStrength)`) costs ~1 instruction per fragment. Most useful for the rocks and future campfire/stump props.

### F — Fine quantizer for near-neutral props (pending merge)

- **Source:** `unity-conventions.md` §Low-poly mesh patterns, "coarse 12-step palette quantizer splits NEAR-NEUTRAL warm tints into a pink cast" — **Strong** (empirically observed + documented; fix on `drew/rocks-sourced` branch at `d358176`, ticket `86ca8m5zu`).
- **What it says:** The 12-step quantizer in `LowPolyZoneGen.cs:Quantize()` snaps near-grey channels to `0.667/0.583/0.583` = R>G=B = pink. Fix is `QuantizeFine` (24-step) for props whose chroma < ~0.1 (all channels within 0.1 of each other). This is a confirmed code path bug, not research — it's listed here because it is the highest-confidence quick win and it is unmerged.

---

## Application to Embergrave / Far Horizon

### Ranked recommendations — impact vs effort

**Rank 1 — Ship `QuantizeFine` (merge `drew/rocks-sourced`). Effort: zero. Impact: high.**
The pink-cast bug is confirmed, the fix is authored and tested. Every near-neutral rock, trunk, and stump is affected. No research needed — merge the PR.
- File: `Assets/Scripts/Editor/LowPolyZoneGen.cs` `Quantize()` → `QuantizeFine()` for chroma < 0.1 props.

**Rank 2 — Add flat-shading mode to `LowPolyVertexColor.shader` via ddx/ddy. Effort: 2–4h. Impact: high.**
Replace `IN.normalWS` in the fragment function with the derivative-computed face normal:
```hlsl
// Pattern only — NOT production code
float3 ddxPos = ddx(IN.positionWS);
float3 ddyPos = ddy(IN.positionWS);
float3 faceNormalWS = normalize(cross(ddyPos, ddxPos));
// Use faceNormalWS instead of normalize(IN.normalWS) in the ndotl + SH calls
```
Expose as `_FlatShading` toggle (0 = smooth/current, 1 = flat). Welded mesh stays welded — no topology change. Winding-bug class becomes structurally impossible for any prop using this mode (the derivative is correct regardless of winding direction: if the face is backface-culled, the fragment never runs). The rocks (`FacetedRock`, currently on the unmerged branch) and any future Blender-MCP props are the primary beneficiaries — but the flag-off default means terrain/water/canopy are unaffected.
- File: `Assets/Shaders/LowPolyVertexColor.shader` (frag stage + Properties block).
- Compose: add `TEXCOORD1` passthrough for `positionWS` in Varyings if not already present (it is — `TEXCOORD1 : positionWS` is there already in the current shader).

**Rank 3 — Add depth-fade intersection foam to the water shader. Effort: 4–8h (URP Asset setting change + shader extension). Impact: high for the ocean read.**
Prerequisites:
1. Enable `Depth Texture` + `Opaque Texture` in the URP Asset (Project Settings → Graphics → the active UniversalRenderPipelineAsset). Both are toggles; zero runtime cost on a desktop target.
2. Change water material's `Render Queue` to Transparent (2501) — our water is currently Geometry/Opaque queue per the `LowPolyVertexColor.shader` Tags. This means a second shader variant or a new `LowPolyWaterShader.shader` derived from the existing one, with `"Queue"="Transparent"` and `"RenderType"="Transparent"` tags, plus `Blend SrcAlpha OneMinusSrcAlpha` and `ZWrite Off`.
3. In the frag: sample `SampleSceneDepth(screenUV)` → `LinearEyeDepth(rawDepth, _ZBufferParams)`, subtract fragment's own eye depth (`IN.positionCS.w`), divide by `_FoamDistance` (expose as property, ~1.5u), saturate → foam mask. Lerp toward `_FoamColor` (near-white warm, sub-1.0) where the mask is high.

The near-shore foam line is currently baked as a vertex-color band on the terrain mesh (`FoamEdge` colour in `LowPolyZoneGen.GroundColorAt`). The depth-fade foam FROM THE WATER side reads it dynamically — the two approaches are complementary (terrain-baked foam at the static waterline, depth-fade foam anywhere a prop, rock, or future pier intersects the water).
- File: new `Assets/Shaders/LowPolyWater.shader` (fork of `LowPolyVertexColor.shader`, add Transparent queue + depth includes + foam frag logic).

**Rank 4 — Add Fresnel/rim term to `LowPolyVertexColor.shader`. Effort: 1–2h. Impact: medium.**
One-line addition in frag:
```hlsl
// Pattern only — NOT production code
float rim = pow(1.0 - saturate(dot(faceNormalWS, viewDirWS)), _RimPower);
finalCol += _RimColor.rgb * rim * _RimIntensity;
```
Expose `_RimColor (Color) = (0.95, 0.92, 0.85, 1)` (near-white warm), `_RimPower (Float) = 3`, `_RimIntensity (Float) = 0` (defaults off). Per-prop override via material instance. Matches the board's white-edge-highlight on the axe head — and works on any procedural prop without Blender geometry changes. Secondary read vs. the chamfer-plane approach (below) but costs nothing for props that don't get a Blender pass.
- File: `Assets/Shaders/LowPolyVertexColor.shader` (one new property + one frag line, gated by `_RimIntensity > 0` to avoid any cost on terrain/canopy/water).

**Rank 5 — Chamfer-highlight plane in Blender for hero props. Effort: per-prop (~1h per prop in Blender MCP). Impact: high fidelity for specific props, medium across the board.**
For the axe (and future campfire, stump, chest props): add a narrow top-edge face in Blender MCP with a distinct material index (`_HighlightMat`), assign near-white colour (0.92, 0.90, 0.84). Import as FBX; the Unity pipeline assigns the material by slot index. This matches the board exactly (`21h08_08`) — the white face is discrete, not a wrap. The Fresnel (Rank 4) is a complementary fallback for props that skip this Blender pass.
- Applies to: Blender MCP `execute_blender_code` scripts for any new prop authored in the project.

**Rank 6 — Vertex-color AO baking for rocks/props in Blender. Effort: 2–4h (Blender bake setup + shader extension). Impact: medium; best for rocks and stump.**
Bake AO to vertex colour alpha in Blender before FBX export. In `LowPolyVertexColor.shader` frag, multiply `finalCol *= lerp(1.0, IN.color.a, _AOStrength)`. Default `_AOStrength = 0` (no-op for current terrain + canopy, which don't carry AO alpha). Rock/prop materials set `_AOStrength ~0.5`. Adds contact-shadow depth at crevices on rocks and stumps without texture cost.
- File: `Assets/Shaders/LowPolyVertexColor.shader` (one lerp in frag) + Blender MCP bake script.

### The quantizer discipline — apply going forward

- Any prop with `max(R,G,B) - min(R,G,B) < 0.10` (near-grey) should use the 24-step quantizer. Saturated props (trees, water, grass) are safe on the 12-step. This is a rule-of-thumb Devon/Drew can apply as a comment in `LowPolyZoneGen.cs:MakeFlatColorMat`.

### What NOT to do

- **Toon hard-band ramp (step function on ndotl):** the board is smooth-shaded, not cel-shaded — two hard light bands would fight the chunky-faceted look the Sponsor approved. The existing smooth diffuse + SH is correct. Avoid.
- **Screen-space outlines (Sobel depth/normals):** tempting but expensive at desktop resolution, and the board's "edge highlight" is geometry-specific (one face on the axe), not a full-silhouette outline. Reserve for a future explicit decision.
- **Converting terrain mesh to flat-shaded (unwelded):** the welded terrain grid + smooth normals IS the Zone-D look (the soft rolling dune read). Flat-shading the terrain would make it read as a spike polyhedron. Only props/rocks benefit from the ddx/ddy flat mode.
