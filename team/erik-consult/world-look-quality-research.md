# World-Look Quality Research — Sky, Clouds, Mountains, Water

## Question

The Sponsor soaked build `4457d47` and said "it doesn't look good." Four specific systems are broken:

1. **Mountains** — render as floating, translucent shards.
2. **Water** — flat hard-diagonal teal edge with no shoreline treatment.
3. **Sky** — plain gradient, possibly overwriting geometry (prior Drew PR #48 skybox-wash class).
4. **Clouds** — sparse, barely visible.

What is the best stylized low-poly URP technique for each, and how should Drew implement them?

Style lock: chunky faceted flat-shaded, warm/cheerful, camera = human-scale orbit. Windows desktop hero scene. Teal-to-deep-blue sea meets a sand beach.

---

## Bottom Line

**Mountains:** Force opaque surface type + ensure normals point outward on the procedural mesh. Floating/transparent = wrong surface type or inverted normals, not a lighting problem. Fix first, restyle second.

**Water:** Depth-fade shoreline foam + shallow-to-deep color gradient + modest low-amplitude per-vertex sine displacement on an unwelded mesh (flat-shaded facets) = the correct route for this style. Skip Gerstner complexity; the art board reads FLAT calm water with only a visible shoreline line.

**Sky:** Replace or repair the current custom skybox shader. Use a clean two-color gradient skybox shader (standard `Queue=Background` / `ZWrite Off` / `Cull Off` / correct positionCS — NOT the `xyww` depth trick) so it sits behind all geometry. The art board sky is a clear warm blue — no horizon glow needed.

**Clouds:** Author chunky low-poly blob-cloud meshes (the art board shows cartoonish rounded triangulated puffs) placed as static prefabs in world space. Use URP/Simple Lit (opaque) so they receive ambient light and cast shadows. No particles needed for hero scene.

---

## Evidence

### Mountains

- **Unity docs — URP Lit Shader, Surface Options** [Unity 6 docs, 2024, https://docs.unity3d.com/6000.0/Documentation/Manual/urp/lit-shader.html] — `Surface Type: Opaque` maps to `Queue=Geometry`, `Transparent` to `Queue=Transparent`. Transparent mountains would sort after skybox but write no depth, yielding see-through shards. **Strength: Strong (official docs).**

- **Unity issue tracker — "URP Transparent materials don't behave correctly"** [Unity Issue Tracker, 2022+, https://issuetracker.unity3d.com/issues/urp-materials-with-urp-shaders-which-have-surface-type-set-to-transparent-do-not-behave-correctly] — known that a mistaken Surface Type of Transparent produces bogus z-sorting artifacts. **Strength: Strong (official tracker).**

- **Project precedent — `-Z grid / backface cull` failure class** [unity-conventions.md, Far Horizon project, 2026-06-13] — Drew's own incident: procedural meshes with inverted winding render 0 visible pixels from any above-looking camera. The magenta-diff trace is the diagnostic. **Strength: Strong (internal, observed).**

- **Flat shading normals requirement** [hextantstudios.com/unity-flat-low-poly-shader, 2024] — flat shading = unwelded verts with per-face normals; each face normal must point outward. If the mountain builder shares winding logic with the sea grid, the same inversion bug may apply. **Strength: Moderate (well-sourced write-up).**

### Water

- **danielzeller/Lowpoly-Water-Unity** [GitHub, active, https://github.com/danielzeller/Lowpoly-Water-Unity] — reference implementation: depth-based edge blend (scene depth texture compare → `saturate(invFade * (depth - screenPos.w))`), dual-sample foam texture for shoreline, no vertex displacement, color-blend at surface. Closest to the Monument Valley flat-water aesthetic the art board shows. **Strength: Strong (open-source, widely cited).**

- **Cyanilux shoreline shader breakdown** [cyanilux.com, December 2024, https://www.cyanilux.com/tutorials/shoreline-shader-breakdown/] — depth-fade approach using Shader Graph `Screen Position` + `Object Y` + `Step` node to produce a sharp shoreline foam line without UV warping at orbit-camera angles. Directly addresses the "hard diagonal edge" problem: a depth-reconstructed intersection does not warp as the camera rotates. **Strength: Strong (technical tutorial, URP Shader Graph, 2024).**

- **danielilett.com stylised water URP** [2020, still-valid URP technique, https://danielilett.com/2020-04-05-tut5-3-urp-stylised-water/] — depth-buffer intersection foam: "the difference between surface depth and scene depth; when small, draw foam." Two-color shallow/deep lerp driven by the same depth difference. **Strength: Moderate (2020 but technique is pipeline-stable).**

- **Unity production-ready Shader Graph sample — WaterSimple_FoamMask** [Unity Shader Graph 17.4 docs, 2024, https://docs.unity3d.com/Packages/com.unity.shadergraph@17.4/manual/Shader-Graph-Sample-Production-Ready-Detail.html] — official pond shader uses 3 Gerstner subgraphs + manual foam mask. For this project Gerstner is overkill (art board water is visually flat-calm); useful only as a reference for the Shader Graph node structure. **Strength: Strong (official docs).**

- **Roystan toon water** [roystan.net, 2019, https://roystan.net/articles/toon-water/] — depth-banded color + normals-buffer foam. Built-in RP, needs adapting; but the stepped-color-band concept is informative. The art board does NOT show stepped color bands — it shows a smooth shallow-to-deep gradient — so the stepped variant is not recommended. **Strength: Moderate (dated, different RP).**

### Sky

- **Unity conventions.md — custom URP skybox shader wash class** [Far Horizon project, Drew PR #48, 2026-06-13] — confirmed failure mode: a custom shader that sets `positionCS.xyww` on a Background-queue SubShader makes the gradient render as a full-screen fill OVER all geometry. The diagnostic is `-flatSky` isolation probe. **Strength: Strong (internal, observed, named failure class).**

- **kelvinvanhoorn.com Unity skybox shader tutorial** [2024, https://kelvinvanhoorn.com/tutorials/unity_skybox_shader/] — correct recipe: `Queue=Background`, `ZWrite Off`, `Cull Off`, standard `GetVertexPositionInputs()` for positionCS, world-space position used as view direction. NO `xyww` depth trick; the background queue already ensures correct render order without the depth trick. **Strength: Strong (comprehensive tutorial, URP-aligned).**

- **keijiro/UnitySkyboxShaders — Gradient Skybox.shader** [GitHub, https://github.com/keijiro/UnitySkyboxShaders] — minimal two-color gradient skybox by a well-known Unity developer; top/bottom color + attitude tilt. Correct render state, no wash, zero complexity. Can be used directly or adapted into Shader Graph. **Strength: Strong (authoritative open-source reference).**

- **Polyverse Skies** [Unity Asset Store, v3.6.0 Nov 2025, https://assetstore.unity.com/packages/vfx/shaders/polyverse-skies-low-poly-skybox-shaders-104017] — commercial; specifically targets low-poly stylized games, multiple gradient presets, URP 7.1.8+, confirmed Nov 2025 update. If Drew wants to buy a pre-built low-poly sky that exactly matches the art style (faceted horizon bands), this is the best-fit paid option (~$15–25 on Asset Store). **Strength: Moderate (store listing + ArtStation preview).**

### Clouds

- **Art board reference** [`inspiration/2026-06-12_21h10_44.png`] — chunky rounded polygon cloud puffs, bright teal/white, flat-shaded triangulated blobs. NOT billboards or particle puffs; solid 3D objects with visible facets. Same visual language as the blob trees. **Strength: Strong (Sponsor-approved ground truth).**

- **Medium — stylized clouds with Shader Graph + Shuriken** [Mike Young, 2024, https://medium.com/@mikeyoung_97230/creating-stylized-clouds-with-shader-graph-and-shuriken-in-unity3d-ec8f12fb5f0a] — spawns cloud-shaped 3D meshes as particle instances with vertex offset noise + bloom. Useful context, but for Far Horizon the simpler route (static prefabs, no particles, URP/Simple Lit opaque) matches the art board better and avoids the complexity overhead for a hero scene. **Strength: Moderate (valid technique, different goal).**

- **Stylized Low Poly Clouds asset** [Unity Asset Store, Ember Glitch, https://assetstore.unity.com/packages/3d/environments/stylized-low-poly-clouds-150282] — pre-built low-poly cloud meshes. If Blender MCP cloud geometry is slower than needed, this is a fast source of style-matching meshes. Price: ~$15. **Strength: Moderate (store listing, no independent review found).**

---

## Application to Far Horizon — Ranked Recommendations

### 1. Mountains (CRITICAL — fix first)

**Problem diagnosis:** Floating + translucent = one of two root causes (or both):
- The material's Surface Type is `Transparent` (wrong); switch to `Opaque`.
- The procedural mountain mesh has inverted normals (wrong winding, same class as the sea-grid bug). Apply the magenta-diff trace: set mountain material to magenta, build shipped exe, pixel-diff. Zero change = backface-culled = winding issue.

**Drew implements it like this:**

1. Open the mountain prefab/material in the Inspector. If Surface Type is not `Opaque`, set it to `Opaque`. Rebuild + check.
2. If still floating/translucent after step 1, run the magenta-diff probe (sentinel material → shipped build → diff vs normal build). If mountain pixels = ~0, the mesh has inverted normals.
3. Fix: in the mountain mesh builder (C# procedural or Blender-sourced FBX), flip the triangle winding OR call `mesh.RecalculateNormals()` after mesh generation and assert all face normals have positive Y (EditMode test: `MountainFacesUpTests`).
4. Grounded look: the mountain base must merge flush with the terrain surface. If peaks appear to float, add a skirt — extend the base geometry downward 1–2 units below Y=0 so the join is buried under the sand/grass rather than sitting on top of it.
5. Flat-shaded look: import from Blender with Shade Flat applied (no smooth normals), or in the C# builder use unwelded verts + `mesh.RecalculateNormals()` with `RecalculateNormals(0f)` (0° smoothing angle = per-face normals).

**No new shader needed.** URP/Lit or URP/Simple Lit on Opaque is correct. Vertex-color tinted if needed for snow-cap (grey peak, brown base).

---

### 2. Water (HIGH — primary visual polish)

**Recommended route: depth-gradient + soft depth-fade shoreline + flat faceted mesh, NO Gerstner waves.**

The art board (`21h16_13`, `21h16_52`) shows calm flat water with a visible water line at the shore — not animated waves. Modest per-vertex sine is acceptable for liveliness, but the primary win is the shoreline and the depth color gradient.

**Drew implements it like this:**

A. **Mesh:** Replace the current flat plane with an unwelded grid mesh where each triangle has its own vertex copies (no shared verts). This produces flat-shaded facets (each face one solid color) consistent with the rest of the world look. Low vertex count (32×32 or less) is fine. Ensure triangle winding produces upward normals (the existing `WaterFacesUpTests` EditMode guard covers this).

B. **Shader (Shader Graph, URP Unlit or Simple Lit):**
   - **Depth gradient color:** sample `Scene Depth` node (eye-space linear). Compare depth-behind-water-surface minus water-surface depth. Map 0→shallow teal (`#4FC3C3` approx), 1→deep blue-green (`#1A6B7A` approx) via a `Lerp` node. Control the transition range with a `Foam Depth` float (start ~1.0–2.0 world units).
   - **Shoreline foam:** at depth difference < `Foam Threshold` (0.3–0.6 world units), blend in a white foam color via `Step` or `Smoothstep` node. Optionally scroll a simple noise texture along the foam band to animate the edge.
   - **Subtle animation:** add a low-amplitude (0.05–0.1 world units) sine displacement in the vertex stage driven by `_Time` + vertex world XZ position. This gives life without breaking the flat-facet look (the displacement is smaller than a facet height, so facets still read solid).
   - **Specular sparkle:** use `Simple Lit` surface so the Directional Light produces a specular highlight dot. Keep Smoothness low (~0.3) for a broad, chunky highlight appropriate to the style.
   - **Opacity:** opaque (`Surface Type = Opaque`). The depth gradient creates the illusion of transparency; actual transparency adds sorting bugs with no benefit.

C. **Shoreline geometry:** the hard diagonal edge is the current water mesh boundary. With the depth-fade foam, the visual edge softens into a foam line regardless of the mesh edge shape. If the diagonal mesh edge is still visible as a hard geometry seam, either (a) extend the water mesh further onto the beach and let the foam fade hide it, or (b) reshape the mesh edge to follow the terrain contour more closely. Option (a) is simpler.

D. **URP depth texture requirement:** enable `Depth Texture` in the URP Renderer Asset (already on for the scene's post-processing; verify it is not accidentally disabled).

---

### 3. Sky (HIGH — blocks the whole scene read if it washes geometry)

**Recommended route: replace the broken custom shader with a correct two-color gradient skybox shader (custom HLSL or Shader Graph) using the standard Background queue render state.**

If the current shader is the Drew PR #48 class (positionCS.xyww full-screen fill), any gradient adjustments are futile until the render state is fixed.

**Drew implements it like this:**

1. Diagnose with the `-flatSky` isolation probe (already documented in `unity-conventions.md`): swap `RenderSettings.skybox` to Unity's built-in `Skybox/Gradient` material temporarily. If the scene suddenly looks correct (geometry visible, warm world), the issue is the current custom shader's render state, not the colors.

2. Fix options (choose one):
   - **Option A — keijiro gradient shader (fastest):** copy `Gradient Skybox.shader` from keijiro/UnitySkyboxShaders. Set `Tags { "Queue"="Background" "RenderType"="Background" }`, `ZWrite Off`, `Cull Off`. Assign `_TopColor` = warm sky blue (`#87CEEB`-ish), `_BottomColor` = horizon warm pale (`#E8D5A3`). No geometry overdraw, no wash.
   - **Option B — Shader Graph gradient sky:** create an Unlit Shader Graph with `Override Render State: ZWrite Off, Cull Off`. Use `World Space Position` node → normalize → dot with `(0,1,0)` → remap via `Remap` node → `Lerp` between two color properties. Assign to a `Skybox` material slot. Correct positionCS comes from the graph's built-in transform.
   - **Option C — Polyverse Skies ($15–25):** drop-in low-poly gradient skybox asset specifically designed for this art style; fastest path if Sponsor approves a small asset spend.

3. Warm palette target: sky top = bright warm blue; horizon = paler, slightly warm. Match the art board references (`21h13_31`, `21h16_13`): those skies are a clean clear cyan-blue, not dramatic — they read as backdrop, not a hero element.

4. **Do not use `positionCS.xyww`.** That trick pin-holes depth to the far plane to avoid overdraw, but Shader Graph's `Vertex Stage` implementation may apply it as a full-screen subShader instead. The correct approach is simply `Queue=Background` — that ordering already prevents overdraw.

---

### 4. Clouds (MEDIUM — readability / completeness)

**Recommended route: author 2–3 chunky blob-cloud 3D meshes in Blender MCP, place as static prefabs in the hero scene at ~20–40 units altitude, opaque URP/Simple Lit.**

The art board (`21h10_44`) shows discrete chunky cartoon cloud puffs — they look like faceted lumpy spheroids, not flat billboards. They are the same language as the blob-canopy trees: convex, saturated, rounded.

**Drew implements it like this:**

A. **Mesh:** via Blender MCP, create 2–3 cloud variants — a sphere with a few bumps pushed out via proportional editing (Grab + Proportional), then apply Shade Flat. Export FBX. Aim for 200–500 tris per cloud. This is a 5-minute Blender operation per variant; no booleans needed.

   Alternatively, search Sketchfab for `low poly cloud` (downloadable:true) — there are CC0/CC-BY options. The `Stylized Low Poly Clouds` Asset Store pack (~$15) is also a fast option if the Sponsor approves.

B. **Material:** URP/Simple Lit, Surface Type = Opaque, `_BaseColor` = bright teal-white (`#D4F0F0`–`#FFFFFF`). Keep Smoothness near 0 for a matte flat look. The clouds should receive ambient light and appear slightly self-lit to read against the sky. If they look dark, increase Emission to a low white value.

C. **Placement:** scatter 4–8 instances at Y = 25–50 world units (high enough to be "in the sky" from the orbit camera's max zoom-out angle). Scale vary ×1.5–×0.7 per instance for variety. Position toward the horizon at X/Z = ±60–100 units so they frame the far view without crowding the action space.

D. **No animation for now.** Static clouds matching the art board is the baseline. Gentle slow translation (clouds drifting) can be a follow-up script.

E. **Scale budget:** 4–8 cloud prefabs at 200–500 tris = 800–4000 tris total. Trivial on desktop.

---

## Trade-off Summary

| System | Recommended route | Alternative | Cost |
|---|---|---|---|
| Mountains | Fix surface type + winding | — | No new shader |
| Water | Depth-gradient + depth-fade foam + flat mesh | Poseidon asset (~$0, free tier) | Custom Shader Graph |
| Sky | keijiro gradient shader or Shader Graph | Polyverse Skies (~$15–25) | Minimal |
| Clouds | Blender MCP blob meshes + Simple Lit opaque | Asset Store cloud pack (~$15) | Blender time |

**Sequencing for Drew:** Fix mountains first (unblocks clear read of the scene), then sky (unblocks correct visual baseline), then water (primary feel), then clouds (polish).

---

---

## 3D-Agent.com Evaluation

### What it is

3D-Agent is a Blender plugin (AI assistant, not an API or web generator) that generates 3D models from text descriptions directly inside Blender. Output: quad-dominant mesh topology, UV-unwrapped, commercial license on all paid tiers. Export formats: OBJ, FBX, GLB, USDZ. Workflow: text prompt → geometry in the Blender viewport → iterate with follow-up prompts.

**Pricing (per their website, June 2026):**
- Basic: $19/mo — 100 prompts, 30 steps each
- Pro: $29/mo or $264/yr — 200 prompts, 80 steps
- Ultra: $89/mo or $708/yr — 800 prompts, 100 steps
- Free tier: download only, 0 automatic prompts (unusable for generation)

**Evidence strength: Strong** — fetched directly from https://3d-agent.com/ in this session.

### What it cannot do (for Far Horizon)

The service produces photorealistic, production-quality assets. The website shows zero style-control options, no low-poly toggle, no chunky/cartoon output, and no gallery demonstrating stylized results. The output style appears to target realistic architectural / character models. The Far Horizon art direction is the opposite — chunky, flat-shaded, minimum-poly, saturated cartoon. A text prompt of "chunky low-poly mountain with flat shading" into a photorealistic generator would likely produce either a realistic mountain or a low-density realistic mesh, not the toy-like faceted aesthetic the board shows.

Hyper3D Rodin (existing route) has the same limitation: it generates from image references and produces relatively high-poly photorealistic meshes that need significant retopology / stylization for the Far Horizon look. The character pipeline doc notes this is why procedural routes (`LowPolyMeshes`/`FacetedRock`) and Blender MCP direct-authoring are the primary world-asset routes.

### Could it help with world-look assets (mountains / water / sky / clouds / terrain)?

No meaningful benefit over existing routes for this style:

- **Mountains / terrain:** the Blender MCP already models these procedurally with exact poly-count control. A text-to-3D that outputs realistic topology provides no shortcut to the chunky faceted look — you'd still need to decimate + apply flat shading, at which point the Blender MCP's direct low-poly approach is faster.
- **Water / sky / clouds:** these are shader and mesh problems, not generation problems. No AI 3D generator adds value here.

### Could it help with characters or props?

**Characters:** already covered by Hyper3D web → Mixamo (character-pipeline.md, validated 2026-06-15). 3D-Agent is a Blender plugin generating inside Blender; it has no Mixamo auto-rig and no image-to-3D path. Adding another text-to-3D tool for characters, at $19–89/mo, adds cost and workflow without addressing the rig/animation gap. Hyper3D web UI is the validated route for characters.

**Props (axe, campfire, etc.):** for simple low-poly props, the Blender MCP (`execute_blender_code`) already generates them with full style control (poly count, flat shading, exact dimensions). 3D-Agent's strength is complexity and realism (medieval church, modern house); simple chunky props don't need that.

### Recommendation

**Do not adopt at this time.** Evidence from the website: no style control, no low-poly mode, and the example outputs suggest photorealistic / detailed output incompatible with the Far Horizon art direction. The existing three-route stack (procedural C# builders, Blender MCP, Hyper3D web + Mixamo for characters) is sufficient and validated.

**Sponsor decision needed only if:** the Sponsor wants to evaluate it firsthand. The Basic tier at $19/mo is within the 100–200 USD/mo tooling tolerance, so a trial would not break budget — but the style mismatch evidence is strong enough that I would not recommend a trial without a specific use case the existing routes demonstrably cannot cover.

**Evidence strength: Moderate** — based on homepage fetch only. No hands-on test, no community reviews found. The absence of any low-poly/stylized output examples is itself a signal, but absence of evidence is not proof of absence of capability.

### Can AI generation produce on-style world assets? (Broader evaluation)

A survey of AI 3D generators useful for low-poly stylized output (source: aifreeforever.com/blog/how-to-create-low-poly-3d-art-with-ai-using-these-prompts, fetched June 2026) identifies five tools: AI Free Forever image generator, Bylo.ai, Meshy.ai, LumaLabs Genie, CGDream. The key finding for Far Horizon:

**The "low poly" prompt problem:** these generators produce low-poly IMAGE renders, NOT low-poly game-engine meshes with flat normals. A prompt of `"low poly mountain, geometric, faceted, 3D isometric render, warm palette"` gives you a stylized image of a mountain — not an FBX you can import into Unity with correct flat-shaded normals and a 100–500-tri budget. The mesh itself may still be high-poly with a visual aesthetic that looks low-poly.

**Meshy.ai** is the exception most worth noting: it outputs `.obj`/`.fbx`/`.stl`/`.blend` (actual geometry, not images), exports 4 assets/month free, and accepts prompts with style guidance. This could generate rough rock/mountain mesh bases to retopologize in Blender. The polygon output for stylized prompts is reported as "100–500 polygons" for background objects with appropriate prompts. However, far-Horizon's chunky cartoon aesthetic requires deliberate flat-shading and winding control that no generator guarantees — a generated mesh would still need the Blender MCP cleaning pass (Apply Shade Flat, verify winding, check normals).

**Prompt techniques worth trying (Meshy.ai or LumaLabs Genie for world props):**
- Mountains/rocks: `"low poly geometric mountain peak, flat shaded facets, chunky stylized, angular polygon faces, minimal detail, game asset, 200 polygons, warm grey-brown tones"`
- Clouds: `"low poly cloud puff, blob shape, faceted geometric polygon mesh, chunky cartoon, bright white-blue, game asset, 150 polygons"`
- The guide's formula: `[Style] + [Subject] + [Technical Details] + [Color]` = "low poly geometric + mountain + 200 polygons flat shaded + warm grey"

**Bottom-line on AI generation for world assets:** viable as a **rough-base source** (same role as Sketchfab sourcing today — get a rough shape, clean it in Blender MCP). NOT a replace-Blender-MCP path. Meshy.ai free tier (4 assets/month) is worth a one-off trial for mountain/rock rough bases before committing to Blender-from-scratch; no cost. 3D-Agent specifically adds nothing here beyond Meshy.ai since it lacks style control.

**For characters/props vs Hyper3D image-to-3D → Mixamo:** AI generators producing raw mesh have no auto-rig; Hyper3D web → Mixamo is the only validated route for rigged characters. For UN-rigged props (rocks, stumps, campfire), Meshy.ai text-to-mesh is a fast rough-base option — but the Blender MCP direct-script approach is faster for simple shapes and gives exact poly-count control. Prefer Blender MCP for props; try Meshy.ai for complex organic shapes (rocks, coral, boulders) where manual blocking is slow.

**Evidence strength: Moderate** — aifreeforever blog survey is a non-authoritative roundup; Meshy.ai not tested firsthand against the Far Horizon art target. Treat as "worth one free trial" rather than validated.

---

## Sources

- [Unity 6 docs — URP Lit Shader](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/lit-shader.html) — official
- [Unity Issue Tracker — transparent surface type bug](https://issuetracker.unity3d.com/issues/urp-materials-with-urp-shaders-which-have-surface-type-set-to-transparent-do-not-behave-correctly) — official
- [danielzeller/Lowpoly-Water-Unity](https://github.com/danielzeller/Lowpoly-Water-Unity) — open-source reference
- [Cyanilux shoreline shader breakdown, Dec 2024](https://www.cyanilux.com/tutorials/shoreline-shader-breakdown/) — depth-fade foam technique
- [danielilett.com stylised water URP](https://danielilett.com/2020-04-05-tut5-3-urp-stylised-water/) — intersection foam fundamentals
- [Unity Shader Graph production-ready pond](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.4/manual/Shader-Graph-Sample-Production-Ready-Detail.html) — official Gerstner + foam reference
- [Roystan toon water](https://roystan.net/articles/toon-water/) — stepped color banding reference
- [kelvinvanhoorn.com — Unity skybox shader tutorial](https://kelvinvanhoorn.com/tutorials/unity_skybox_shader/) — correct render state
- [keijiro/UnitySkyboxShaders](https://github.com/keijiro/UnitySkyboxShaders) — gradient skybox reference implementation
- [Polyverse Skies — Asset Store](https://assetstore.unity.com/packages/vfx/shaders/polyverse-skies-low-poly-skybox-shaders-104017) — low-poly sky commercial option
- [Stylized Low Poly Clouds — Asset Store](https://assetstore.unity.com/packages/3d/environments/stylized-low-poly-clouds-150282) — cloud mesh commercial option
- [Mike Young — stylized clouds with Shader Graph](https://medium.com/@mikeyoung_97230/creating-stylized-clouds-with-shader-graph-and-shuriken-in-unity3d-ec8f12fb5f0a) — particle cloud technique
- [hextantstudios.com flat/low-poly shader](https://hextantstudios.com/unity-flat-low-poly-shader/) — normals + flat shading
- [Unity Blending Modes docs URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/blending-modes.html) — surface type / queue reference
