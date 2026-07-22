# Low-Poly Stylized Sky — Mesh-Based Clouds + Sun Disk

Ticket: `86cabc743` | Research date: 2026-06-29 | Author: Erik (consult)

---

## Question

What is the best approach for a stylized low-poly sky — chunky mesh-based clouds
and a warm sun disk — that matches the Far Horizon "Zone D" look and the
inspiration board, built on top of our existing gradient skybox? Covers mesh
technique, shading, sun disk, animation, and URP/batching performance budget for
Windows desktop.

---

## Bottom line

**The cloud mesh system already exists in the codebase and is correct.** `CloudBlob`
(clustered flat-shaded spheroid blobs) + `CloudDrift` (slow lateral wrap) match the
board exactly: `21h10_44` shows chunky teal-cyan faceted puffs; `21h16_13` shows them
floating above the pine/mountain vista. The one thing genuinely missing is a **sun disk
in `GradientSkybox.shader`**: the current 3-stop gradient has no sun. The recommended
POC is a single shader patch — add a sun disk via a dot-product of the view direction
against `_MainLightPosition` and expose `_SunSize`, `_SunColor`, `_SunHardness`
properties — plus a cloud colour quality pass to ensure clouds read against the
cheerful blue sky (the S2 fix moved their palette brighter; verify the contrast holds
in a built capture before the soak).

---

## Evidence

### E1 — Mesh-based low-poly cloud algorithm (the `CloudBlob` pattern)

- **Source:** Marinacci, J. "Procedural Geometry: Low Poly Clouds," Medium, 2024.
  `https://medium.com/@joshmarinacci/procedural-geometry-low-poly-clouds-b86a0e66bcad`
  **Strength: Moderate** (well-sourced tutorial, Three.js not Unity, but the
  algorithm is geometry — engine-agnostic).

  Algorithm: merge N sphere primitives with translated centres, jitter each vertex
  ±0.2u on all axes, optionally flatten the bottom (enforce a minimum Y). Call
  `computeFlatVertexNormals()` / set `flatShading = true`. Two directional lights give
  the top-lit / underside value step. The Three.js implementation maps cleanly to
  Unity C# — the only difference is `mesh.RecalculateNormals()` is replaced by
  explicit per-face normals (unwelded verts, one normal per face, exactly what
  `AppendFlatBlob` already does in `LowPolyMeshes.cs`).

- **Source:** Far Horizon codebase, `Assets/Scripts/Editor/LowPolyMeshes.cs`,
  function `CloudBlob` + helper `AppendFlatBlob`, commit history on `main` (read
  directly 2026-06-29).
  **Strength: Strong** (ground truth — the code is in production on `main`).

  `CloudBlob` implements: N=3-6 clustered spheroid blobs, each an octahedron
  subdivided and jittered with a seeded RNG, flattened by `yScale=0.78`, with
  distinct per-blob vertex colours (body/top/shadow cyan). Each blob uses UNWELDED
  verts (3 verts per triangle, own face normal) — hard 0° smoothing — producing the
  crisp chunky facets the board shows. Winding is explicitly outward-checked (the
  Cull-Back pattern from `FacetedRock`). Triangle count per blob at `subdiv=1`: an
  octahedron starts 8 faces → after subdiv-1 expansion ≈ 32 faces per blob, so a
  5-blob cloud ≈ 160 flat-shaded triangles (480 unwelded verts). Extremely cheap.

- **Source:** Far Horizon codebase, `Assets/Scripts/Runtime/CloudDrift.cs` (read
  2026-06-29).
  **Strength: Strong** (in production).

  Per-cloud lateral drift: `transform.position += _wind * speed * Time.deltaTime`.
  Wind direction shared, speed varies 0.22–0.48 u/s, wrap-around at band edges. No
  rotation (Uma: "a tumbling cloud reads as debris"). Serialised at bootstrap; only
  the translate runs at runtime.

- **Source:** Far Horizon codebase, `Assets/Scripts/Editor/WorldBootstrap.cs`,
  `BuildClouds()` (read 2026-06-29).
  **Strength: Strong**.

  5–6 clouds instantiated at Y=30–60u altitude, radius 5–7.5u, positioned ahead and
  inland over the play space. Shadow casting OFF, receive shadows OFF (clouds are too
  far overhead to contribute meaningful shadows on the play ground). Each cloud is a
  plain `MeshRenderer` with no `MaterialPropertyBlock`, so it qualifies for both SRP
  Batcher batching and GPU Resident Drawer instancing (the two requirements from
  `unity6-mastery.md §2`).

### E2 — Flat shading via ddx/ddy fragment shader derivative

- **Source:** Hextant Studios. "Rendering Flat-Shaded / Low-Poly Style Models in
  Unity." `https://hextantstudios.com/unity-flat-low-poly-shader/`
  **Strength: Moderate** (single technical write-up, but the shader math is standard
  HLSL and well-established).

  The alternative to unwelded verts: compute the face normal at runtime in the
  fragment shader via `normalize(cross(ddy(positionWS), ddx(positionWS)))`. Allows
  shared/welded vertices (no vertex duplication). No per-vertex normal storage needed.
  Cost: ~2 extra ALU ops per fragment.

  **Application:** this is the `_FlatShading` toggle already planned in `lowpoly-quality.md`
  Rec 2 (ticket `86caamnjb`). For clouds it is NOT needed — `CloudBlob` already uses
  the correct unwelded-explicit approach. The ddx/ddy trick is most valuable for
  PROCEDURAL props that were generated with welded verts (rocks, stumps).

### E3 — Sun disk in a Shader Graph / HLSL skybox

- **Source:** Coster, T. "Unity ShaderGraph Procedural Skybox Tutorial Pt.1,"
  CosterGraphics, 2019 (URP-compatible technique, confirmed in use across Unity 2021+).
  `https://timcoster.com/2019/09/03/unity-shadergraph-skybox-quick-tutorial/`
  **Strength: Moderate** (Shader Graph, not raw HLSL, but the math is identical).

  Pattern: obtain the main light direction; compute
  `dot(viewDir, -lightDir)` — this dot product is 1.0 directly facing the sun, falling
  toward 0.0 away from it; apply `pow(saturate(dot), _SunHardness)` to control the
  disk edge sharpness; scale by `_SunSize` and multiply by `_SunColor` (HDR for
  bloom interaction); add to the sky gradient with `Add`. For an HLSL skybox (which
  `GradientSkybox.shader` already is), the `_MainLightPosition` URP built-in carries
  the directional light's world-space direction. The math is the same as the Shader
  Graph pattern; it just goes in the fragment function directly.

  **Key URP HLSL access pattern:** `#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"` then `Light mainLight = GetMainLight();` gives `mainLight.direction`.

- **Source:** Unity Manual, URP Lighting HLSL API.
  `https://docs.unity3d.com/6000.4/Documentation/Manual/reduce-draw-calls-landing-urp.html`
  **Strength: Strong** (official docs; `GetMainLight()` / `_MainLightPosition` are
  stable URP public API since URP 10, Unity 2021).

- **Source:** Far Horizon codebase, `Assets/Shaders/GradientSkybox.shader` (read
  2026-06-29).
  **Strength: Strong**.

  Current shader: 3-stop vertical gradient, no sun. Already in `UnityPerMaterial`
  CBUFFER (SRP Batcher compliant). The sun disk can be added as 3 new CBUFFER
  properties (`_SunColor`, `_SunSize`, `_SunHardness`) + 4 lines in the fragment
  function. Zero structural changes to the existing gradient or the seam-kill logic.

### E4 — URP draw-call batching for cloud meshes

- **Source:** Unity Docs. "Optimizing draw calls in URP," Unity 6 (6000.4).
  `https://docs.unity3d.com/6000.4/Documentation/Manual/reduce-draw-calls-landing-urp.html`
  **Strength: Strong** (official, URP 6).

  SRP Batcher batches objects sharing the same shader variant; GPU Resident Drawer
  reduces those to instanced draws. Cloud meshes use `LowPolyVertexColor.shader` — the
  same shader as world props — so the 5–6 cloud instances batch with the rest of the
  scene's vertex-colour geometry. Zero additional draw calls if they share the shader;
  at worst 1 draw call per cloud if the SRP Batcher can't merge them with other scene
  geometry. For 5-6 clouds: net ≤6 additional draw calls on a desktop GPU that handles
  2000+ without stalling. Not a performance concern.

- **Source:** Far Horizon `unity6-mastery.md` §2 (project doc, read 2026-06-29).
  **Strength: Strong** (project-level synthesis of official Unity 6 docs).

  GPU Resident Drawer disqualifiers include `MaterialPropertyBlock` on MeshRenderer and
  `OnWillRenderObject` callbacks. Cloud MeshRenderers in `BuildClouds()` use neither —
  they are plain MeshRenderers with a shared material reference. They are eligible for
  GPU Resident Drawer batching.

### E5 — Overdraw / fill budget for sky meshes

- **Source:** TheGamedev.Guru. "Unity Overdraw: Improving GPU Performance."
  `https://thegamedev.guru/unity-gpu-performance/overdraw-optimization/`
  **Strength: Moderate** (established technical blog, consistent with Unity official
  guidance).

  Cloud meshes render opaque (same queue as world geometry). They do NOT contribute
  to overdraw in the classical sense (they do not layer on top of each other in the
  sky — they are separated by altitude and position). The gradient skybox itself is a
  single full-screen Background-queue pass. Because clouds are opaque MeshRenderers,
  the depth buffer occludes the skybox behind them: the skybox fragment shader only
  runs on sky pixels NOT covered by clouds. This is the correct setup — no
  transparency, no layered overdraw cost.

### E6 — Inspiration board sky / cloud evidence (visual)

- **Source:** Inspiration images, Far Horizon board v2 (read 2026-06-29):
  `inspiration/2026-06-12_21h10_44.png`, `21h16_13.png`, `21h13_31.png`,
  `21h16_52.png`, `21h21_30.png`.
  **Strength: Strong** (Sponsor-set art direction ground truth).

  `21h10_44` (nature kit): row of 5 distinct cloud shapes across the top — each is a
  cluster of 3-4 chunky faceted blobs, **bright teal/cyan** (#8FD8E0 tone or brighter),
  flat-shaded with visible polygon facets. Some blobs have a brighter cap facet and a
  darker underside — matching the 3-value (body/top/shadow) cyan palette in `WorldLookPalette.cs`.
  Sky BEHIND the clouds is plain solid blue — no sun disk visible in this image.

  `21h16_13` (the key vista): bright BLUE sky (clear-day saturated blue), two chunky
  cloud masses floating mid-sky, one darker (storm / shadow) with rain streaks. No
  visible sun disk in the image, but the sunlit warmth on terrain and tree tops implies
  a sun at roughly high-afternoon angle. This is the closest thing to a "full sky" in
  the board.

  `21h13_31` (rolling grassland feel): sky bleaches to a very pale blue-white near the
  horizon, a distant cloudy mountain ridge. No distinct clouds overhead; the sky
  atmosphere IS the read. Very similar to our current gradient skybox render.

  `21h16_52` (cabin/lake vista): warm low-angle light, soft sky gradient pale to blue,
  no visible cloud geometry. Sun warmth implied in the scene lighting.

  `21h21_30` (forest path): bright blue sky at the top edge, no cloud overhead,
  implied sun in the lighting warmth.

  **Summary from board:** clouds should be chunky teal-to-white faceted puffs (already
  what CloudBlob produces), on a bright blue sky (already what the cheerful-sky soak
  fix achieved). The sun disk is artistically implied but not literally represented in
  the board images. A SMALL warm sun disk in the sky is an additive quality pass, not
  a style mismatch.

### E7 — Colour contrast: cloud vs sky (the S2 soak fix context)

- **Source:** Far Horizon codebase, `Assets/Scripts/Editor/WorldBootstrap.cs` and
  `Assets/Scripts/Runtime/WorldLookPalette.cs`, comments on the S2 soak fix
  (read 2026-06-29).
  **Strength: Strong** (ground truth in the committed code).

  The S2 soak ("sky is a greyish blue, with no clear indications of clouds") revealed
  that the original light-cyan cloud body (#8FD8E0) washed into a then-pale sky. The
  fix pushed cloud body to #C7EAF2 (bright near-white cyan) and cap to #E6F7FC
  (brilliant). The sky was simultaneously pushed to a saturated cheerful blue
  (SkyMid #73B3EB, SkyZenith #4D94E0). The current committed palette gives roughly:
  cloud body 0.78/0.92/0.95 vs sky mid 0.45/0.70/0.92 — cloud is noticeably brighter
  and lighter than the sky, which should give adequate contrast. This needs confirmation
  in a built capture before the POC soak.

---

## Application to Far Horizon

### What already exists (do not re-implement)

| Component | Location | State |
|---|---|---|
| `CloudBlob` mesh generator | `LowPolyMeshes.cs` | CORRECT — flat-shaded spheroid cluster |
| `CloudDrift` slow lateral drift | `CloudDrift.cs` | CORRECT — translate-only, no rotation |
| `BuildClouds()` scene placement | `WorldBootstrap.cs` | CORRECT — 5-6 clouds, Y=30-60u |
| `GradientSkybox.shader` | `Assets/Shaders/` | CORRECT gradient; missing sun disk |
| Cloud palette | `WorldLookPalette.cs` + `WorldBootstrap.cs` | Correct after S2 fix; verify in build |

### What the POC should ADD (the delta from current)

#### POC item 1 — Sun disk in `GradientSkybox.shader` (HIGH VALUE, ~30 min)

Patch the existing HLSL fragment function. No structural changes to the gradient.

```hlsl
// Add to includes at top of Pass:
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Add to CBUFFER_START(UnityPerMaterial):
float4 _SunColor;        // warm yellow-white, HDR OK (bloom hits it)
float  _SunSize;         // radius of the disk in dot-product space (~0.997 for small sun)
float  _SunHardness;     // pow exponent — higher = harder edge (50-200 for a crisp disk)

// In frag(), after the gradient col is computed, before return:
Light mainLight = GetMainLight();
float3 viewDir = normalize(IN.dir);
float sunDot = saturate(dot(viewDir, mainLight.direction));
// pow sharpens the disk; _SunSize scales how far the glow falls off
float sunMask = pow(max(sunDot - _SunSize, 0.0) / (1.0 - _SunSize + 0.0001), _SunHardness);
col += _SunColor.rgb * sunMask;
```

Bootstrap values to start with: `_SunColor = (1.0, 0.92, 0.70, 1)` (warm gold),
`_SunSize = 0.9985` (small disk at a fixed high-angle position), `_SunHardness = 120`.

These are starting values for the soak — the Sponsor dials them. The sun disk is
additive (`col +=`) so bloom in the post-stack will add a warm corona for free.

**Build note:** `QualityPassGen.BuildGradientSkybox()` sets the sky material
properties at bootstrap time. The new properties must also be set there with the
warm-gold defaults. The sun `_SunColor` should ALSO be registered in
`GraphicsSettings.AlwaysIncludedShaders` registration path — already covered because
the whole `GradientSkybox` shader is registered; adding properties does not change the
strip guard.

#### POC item 2 — Cloud contrast verification in the built exe (REQUIRED before soak)

The S2 cheerful-sky + cloud-body colour changes are committed; verify they ACTUALLY
contrast visibly in the shipped build. The soak should include a sky-facing capture
(camera tilted up into the sky, clouds visible). Do NOT rely on editor preview only.
This is not new code — it is a verification step before presenting to the Sponsor.

#### POC item 3 — Cloud count / composition sanity check (LOW EFFORT)

The board's `21h16_13` has 2 cloud masses. Our `BuildClouds()` places 5–6 individual
blobs. 5-6 blob-clusters is correct — each cluster is one "cloud mass" (3-6 sub-blobs
inside it). Verify the sky does not feel cluttered vs. airy. If cluttered, reduce
`count` from 5–6 to 3–4. This is a seed/count parameter, not a code change.

### Performance budget verdict

| Item | Cost |
|---|---|
| 5-6 cloud meshes (≈160 tris each, opaque, no MPB) | ≤6 draw calls; batched via SRP Batcher / GPU Resident Drawer. **Negligible.** |
| `CloudDrift.Update()` per cloud | 1 `Transform.position` write + 1 `Vector3.Dot` per frame, 5-6 instances. **Negligible.** |
| Sun disk in `GradientSkybox.shader` | 4 extra ALU ops per sky fragment (1 dot, 1 pow, 1 multiply, 1 add). The skybox runs on every sky-visible pixel, but it is already running — the marginal cost is ~5% of the existing skybox fragment cost. **Negligible on desktop.** |
| Overdraw | None. Cloud meshes are opaque and occlude the skybox behind them. **Zero additional overdraw.** |

Desktop Windows target has no meaningful performance constraint for this sky system.
Profile anyway if a future scene has 20+ cloud instances.

### What is ruled OUT (fight the style or are unnecessary)

| Option | Why not |
|---|---|
| Shader Graph skybox rewrite | The raw HLSL `GradientSkybox.shader` already works and is SRP Batcher compliant; a Shader Graph replacement introduces a `.shadergraph` asset, a compiled sub-graph, and a more complex pipeline with no benefit for the style we want. |
| Noise-animated vertex-offset clouds (Mike Young Medium approach) | That technique produces volumetric-blobby undulating meshes — the board wants CRISP FACETED chunky puffs, not smooth blobby animated masses. Already ruled out by `CloudBlob`'s hard-normal design. |
| Particle system cloud spawning | Adds a particle component and runtime spawn overhead; the existing BuildClouds placement is editor-baked and deterministic — cheaper and stable. |
| Billboard / quad cloud sprites | Zero facets; entirely wrong for the Zone D style. Not on the board. |
| Volumetric / raymarched clouds | Wrong aesthetic register and wrong cost for a low-poly title. Board is clearly geometric, not volumetric. |
| Third-party cloud assets (Altos, Cloudscape, FastSky) | In-house-first posture (memory `in-house-asset-routes-over-paid-tools`); our own `CloudBlob` already achieves the board look. These are not the default. |
| Lens flare asset for sun | Over-engineered; a simple bloom + warm disk in the skybox is the Zone D read. Keep it chunky and toy-like. |

---

## Recommended dev POC — step-by-step

**Scope:** One PR. Estimate 2-3h dev, 1h QA.

1. **Patch `GradientSkybox.shader`**: add `_SunColor`, `_SunSize`, `_SunHardness` to
   the `UnityPerMaterial` CBUFFER and the Properties block. Add `GetMainLight()`
   include. Add the sun dot-product + pow + additive line to `frag()`. See §POC item 1
   above for exact HLSL.

2. **Patch `QualityPassGen.BuildGradientSkybox()`**: set the three new properties
   on the `sky` Material with the warm-gold defaults before `AssetDatabase.CreateAsset`.

3. **Build the exe** (not just editor preview). The skybox is a prime candidate for
   editor-vs-runtime shader divergence (it is registered in AlwaysIncludedShaders, but
   verify the built exe shows the sun disk).

4. **Take a sky-facing capture** (camera tilted ~30-45° upward, inland direction,
   afternoon lighting). Verify: (a) sun disk visible as a warm yellow circle, (b) clouds
   read as bright near-white cyan blobs against the blue sky, (c) no seam at the horizon
   (fog colour == horizon stop still holds).

5. **Deliver to Sponsor soak** with the built exe path + explicit "test THIS": tilt
   camera to sky; confirm sun visible overhead; confirm clouds pop against blue.

**Evidence-strength summary for the POC approach:**
- Cloud mesh system (Strong — production code, confirmed on board): adopt as-is.
- Sun disk shader math (Moderate — established pattern in use across many shipped
  titles, not specifically verified on Unity 6 URP `GetMainLight()` in a skybox pass):
  the one uncertainty is whether `GetMainLight()` works inside the skybox pass. The
  skybox pass runs after opaque and before transparent; it has access to URP lighting
  data in principle, but the actual test is the built exe. The fallback if `GetMainLight()`
  does not bind in the skybox pass: declare `float4 _MainLightPosition;` manually in
  the CBUFFER instead, which is always available as a URP shader keyword.

---

## Sources (full list)

- Marinacci, J. "Procedural Geometry: Low Poly Clouds." Medium, 2024.
  `https://medium.com/@joshmarinacci/procedural-geometry-low-poly-clouds-b86a0e66bcad`
- Hextant Studios. "Rendering Flat-Shaded / Low-Poly Style Models in Unity."
  `https://hextantstudios.com/unity-flat-low-poly-shader/`
- Coster, T. "Unity ShaderGraph Procedural Skybox Tutorial Pt.1." CosterGraphics, 2019.
  `https://timcoster.com/2019/09/03/unity-shadergraph-skybox-quick-tutorial/`
- Unity Technologies. "Optimizing draw calls in URP." Unity 6 Manual.
  `https://docs.unity3d.com/6000.4/Documentation/Manual/reduce-draw-calls-landing-urp.html`
- Young, M. "Creating stylized clouds with Shader Graph and Shuriken in Unity3D." Medium.
  `https://medium.com/@mikeyoung_97230/creating-stylized-clouds-with-shader-graph-and-shuriken-in-unity3d-ec8f12fb5f0a`
- Catlike Coding. "Icosphere" (procedural mesh series). 2022.
  `https://catlikecoding.com/unity/tutorials/procedural-meshes/icosphere/`
- Far Horizon codebase (production ground truth):
  - `Assets/Scripts/Editor/LowPolyMeshes.cs` — `CloudBlob` + `AppendFlatBlob`
  - `Assets/Scripts/Runtime/CloudDrift.cs`
  - `Assets/Scripts/Editor/WorldBootstrap.cs` — `BuildClouds()`
  - `Assets/Scripts/Runtime/WorldLookPalette.cs`
  - `Assets/Shaders/GradientSkybox.shader`
  - `inspiration/2026-06-12_21h10_44.png`, `21h16_13.png`, `21h13_31.png`
