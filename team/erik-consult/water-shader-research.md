# Stylized Water Shader — URP Recipe + Adopt-in-Our-Code Plan

## Question

The M-U2 thirst loop requires a freshwater POND the castaway can drink from. The ocean already
exists (flat teal plane, vertex-color gradient, sine swell in `LowPolyVertexColor.shader`) but
reads as visually thin — no depth-fade shoreline, no dynamic intersection foam, no shallow/deep
gradient distinction per context. Ticket 86ca8x038 (Sponsor: "both, WATER FIRST"). The question:
what is the concrete URP Shader Graph / HLSL recipe for the stylized low-poly water effect, and
how do we adopt it into the existing codebase without breaking the flat faceted look?

## Bottom line

The existing water infra (`LowPolyVertexColor.shader` + `BuildIslandWater` + `MakeWaterMaterial`)
is a sound base but is **locked to Opaque queue** — which blocks depth-fade intersection foam (the
single highest-impact visual upgrade). The recipe: fork a new **`LowPolyWater.shader`** (Transparent
queue, ZWrite Off), add (1) depth-fade foam via `SampleSceneDepth` / `LinearEyeDepth`, (2) a
shallow-to-deep vertex-color ramp (already baked in mesh data — the shader just needs to read it),
(3) a secondary gentle ripple term added to the existing crossed-sine swell for the pond, (4) a
low-power Fresnel brightening (~2.0) at grazing angles, and (5) port the `_FogCap` fog-floor
manually into the transparent path to preserve the sea↔sky horizon. Use a **shared material
instance, tuned per-context via material properties**: pond gets shorter wavelength, lower amp,
stronger foam distance; ocean gets larger wavelength, wider foam band. The foam reads as a soft
warm-white edge dissolve, not surf — matching the flat-calm water in the inspiration images.

---

## Evidence

### A — Depth-fade intersection foam (the load-bearing technique)

- **Source:** Cyanilux, "Depth Shader Tutorials for URP"
  [https://www.cyanilux.com/tutorials/depth/] — **Strong** (exhaustive technical reference,
  covers HLSL + Shader Graph paths, URP-specific, cited in multiple shipped URP projects).
  Core recipe (HLSL): include `DeclareDepthTexture.hlsl`; `SampleSceneDepth(screenUV)` →
  `LinearEyeDepth(rawDepth, _ZBufferParams)` → subtract fragment eye depth (`-IN.positionVS.z`
  or `IN.positionCS.w`) → divide by `_FoamDistance` → `saturate` → the foam mask is `1 - result`:
  value 1 near any intersecting opaque (shore, rock, stump), 0 in open water.

- **Source:** Daniel Ilett, "Unity Shader Graph Basics Part 8 — Scene Intersections"
  [https://danielilett.com/2024-05-21-tut7-12-intro-to-shader-graph-part-8/] — **Strong** (2024,
  step-by-step, Unity 6 notes, Shader Graph + HLSL paths covered). Confirms: water shader MUST be
  Transparent surface (Unity saves opaque depth buffer before transparents start — a transparent
  shader can then sample that buffer; an opaque shader sampling it would read itself). The intersection
  distance is controlled by two knobs: `IntersectionPower` (sharpness of the foam edge) and
  `OcclusionStrength` (overall intensity). For a soft toy-style read, keep power low (0.5–1.0) and
  let the saturate + lerp do the blending.

- **Source:** Roystan, "Toon Water Shader" [https://roystan.net/articles/toon-water/] — **Strong**
  (widely reproduced reference implementation; covers the BIRP variant but the depth math is
  identical in URP). Additional detail: the foam distance can be varied by the dot product between
  the water surface normal and the underlying geometry normal — vertical surfaces (rocks) get more
  foam depth, flat surfaces (sand) get less. This is optional complexity; a flat `_FoamDistance`
  uniform works well for our scale.

- **Critical prerequisite (confirmed):** URP Asset must have **Depth Texture** + **Opaque Texture**
  enabled (Project Settings → Graphics → URP Asset → General). Both are zero render-cost checkbox
  toggles on a desktop Windows target. If these are off, `SampleSceneDepth` returns 0 everywhere
  and the foam mask saturates to solid white. The `WorldBootstrap` or `BootstrapProject` runner
  should assert these are enabled as part of the URP Asset setup.

### B — Transparent queue and the fog-cap carry-over

- **Source:** Unity docs — URP Render Queue
  [https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-render-targets-introduction.html]
  + `unity-conventions.md` §Build stripping / URP water — **Strong** (in-codebase, diagnosed and
  fixed on `86ca9yn57`). The existing opaque water was intentionally chosen because transparent
  surfaces don't compose with URP's Exp² fog the same way: the fog factor `ComputeFogIntensity`
  is applied in the fragment, but `MixFog` bakes the sky colour in a way that lets the `_FogCap`
  technique work identically on both paths. Moving to Transparent does NOT break the fog-cap logic —
  the `_FogCap` block in `LowPolyVertexColor.shader` lines 131–145 uses a manual `max(fogIntensity,
  _FogCap)` and a manual `lerp(finalCol, fogColor, 1-intensity)` that works regardless of surface
  type. This block must be ported verbatim into the new water shader. The fog keyword `#pragma
  multi_compile_fog` must also be carried across.

- **Tradeoff:** Moving to Transparent adds the water draw to the transparent pass (after opaques),
  which means the GPU sorts it per-frame. For a single large plane (ocean) or a small disc (pond)
  this is one draw call each — negligible on a desktop Windows target.

### C — Shallow-to-deep gradient

- **Source:** ameye.dev, "Stylized Water Shader" [https://ameye.dev/notes/stylized-water-shader/]
  — **Moderate** (postmortem of a shipped stylized water; widely cited). Standard approach: lerp
  two colours (`_ShallowColor`, `_DeepColor`) by a depth mask (0 near shore, 1 in open water).
  The depth mask is `saturate(depthDifference / _DepthColorDistance)` — the same depth difference
  used for foam but with a larger falloff distance (`_DepthColorDistance` >> `_FoamDistance`).

- **Application to our code:** The `BuildIslandWater` mesh already bakes the shallow-to-deep
  gradient into vertex colors (`WaterShallow` → `WaterDeep` via `depthT`). The current
  `LowPolyVertexColor.shader` reads `IN.color.rgb * _Tint.rgb` — the gradient is already there.
  We do NOT need a camera-depth-based colour gradient on top; the baked vertex colour is the
  gradient. The only addition is the foam mask (depth-based) that blends warm-white foam ON TOP
  of the vertex colour near intersections. This keeps the flat low-poly look (no per-pixel smooth
  colour transition — the faceted mesh gives the gradient its chunky staircase).

- **Pond specifics:** A small pond mesh can be built with a simple radial disc; the vertex colours
  can be a simple `WaterShallow` everywhere (pond is shallow — no need for a deep-band gradient)
  or a mild centre-to-edge ramp. The foam ring at the pond's bank is the visually load-bearing part.

### D — Vertex displacement waves (swell + ripple)

- **Source:** Daniel Ilett, "Stylised Water in Shader Graph and URP"
  [https://danielilett.com/2020-04-05-tut5-3-urp-stylised-water/] — **Strong** (step-by-step, URP,
  Shader Graph, reproducible). Two techniques:
  (1) **Large-wavelength sine swell** (what we already have: crossed sines on world XZ). Good for
  the ocean, too large for a pond.
  (2) **Short-wavelength ripple** — a second pair of crossed sines at `_RippleLen` (~2–4u) and
  `_RippleAmp` (~0.02u). These are imperceptible individually on a 700u ocean but give a pond
  the sense of surface texture without making it choppy.

- **Normals:** The existing shader leaves normals as-baked (averaged flat-water normals) and does
  not recompute them after swell displacement. This is correct for the ocean (a 0.45u swell on a
  700u plane gives <0.1° normal error, invisible). For the pond, ripple amplitude is tiny — same
  logic applies. Do not recompute normals in-shader; the cost outweighs the benefit and fights the
  flat look.

- **Sponsor motion preference (memory `sponsor-prefers-natural-lively-motion`):** The swell must
  visibly move (amp/wavelength tuned so crests cross the framed view). The pond ripples should be
  lighter (shorter amp, shorter period — reads as gentle surface agitation). Use `_Time.y` keyed off
  world XZ (not local — the mesh's world position is the anchor, so the pattern is continuous across
  the full plane).

### E — Fresnel / rim brightening at grazing angles

- **Source:** Prior research `procedural-shadergraph-quality-research.md` §C — **Strong** (in-repo,
  reviewed). Pattern: `pow(1 - saturate(dot(normalWS, viewDirWS)), _RimPower)` at `_RimPower ~2.0`
  gives a soft brightening at the silhouette edge. For water this reads as a sky-glint at grazing
  angles — the familiar "lake catches the light at the edge of frame" effect. Keep intensity low
  (`_RimIntensity ~0.12`) — we want a subtle brightening, not a specular ring. Default to off
  (`_RimIntensity = 0`) so the material instance can tune per-context.

- **Art direction alignment:** The inspiration images (`21h16_52` cabin over lake, `21h16_13`
  winding river) show flat calm bright-teal water with no visible specular highlights. The water
  reads by COLOUR (bright saturated teal), not by reflection. A very subtle fresnel (power 2,
  intensity 0.08–0.12) is acceptable; anything higher fights the toy read.

### F — Performance on URP / Windows desktop

- **Source:** Unity Forum — Stylized Water 2 URP [https://discussions.unity.com/t/stylized-water-2-urp]
  / Poseidon Low-Poly Water [https://unityassetcollection.com/low-poly-water-builtin-urp-poseidon-free-download/]
  — **Moderate** (asset community, not official docs). Consensus: depth-fade foam (one `SampleSceneDepth`
  + `LinearEyeDepth` + two arithmetic ops per fragment) costs roughly 0.05–0.1ms/frame on a desktop
  GPU at 1080p with one full-screen water plane. At our scale (one ocean plane + one small pond disc)
  the total transparent draw cost is under 0.2ms. The wave swell is vertex-shader work (sin ops per
  vertex) — the ocean grid has 160² = 25 600 verts, each doing 2 sin calls; this is <0.05ms on modern
  desktop. Not a performance concern for Windows desktop.

- **SRP-Batcher compatibility:** Every new property must go inside `CBUFFER_START(UnityPerMaterial)`
  (see `LowPolyVertexColor.shader` lines 60–66 and `unity-conventions.md` §SRP-Batcher compliance).
  Depth texture sampling (`SampleSceneDepth`) is outside the CBUFFER — it is a global sampler
  declared by `DeclareDepthTexture.hlsl`. This is correct and does not break SRP-Batcher.

### G — Art direction — what the inspiration images actually show for water

Reading the inspiration images directly:

- **`21h16_13`** (winding river): flat, bright-saturated teal ribbon, no visible foam or surface
  ripple, hard faceted edge where it meets the green bank. The water is distinguished purely by its
  colour against the terrain. No wave animation visible (static render).
- **`21h16_52`** (cabin over lake): calm flat teal body of water, hard chunky intersection with
  the terrain, no soft shoreline fade, no visible foam band. The lake reads as a bright coloured
  plane, toy-like.
- **`21h21_30`** (forest stream): a narrow flat teal ribbon, clean hard edge against the grassy
  bank.

**Implication:** The "Zone D" water target is **colour-dominant, not effect-dominant**. The depth-fade
foam is a SUBTLE warm-white edge softener, not a surf band. Foam distance should be SHORT (1.0–1.5u)
so it reads as a soft edge glow, not a crashing wave. The vertex swell and ripple are the liveness;
the foam is the quality marker that elevates from "flat plane" to "it has a shore."

---

## Application to Far Horizon — Adopt-in-Our-Code Plan

### Repo baseline (what exists — do not regress)

| Element | File | Status |
|---|---|---|
| Water mesh (700u square grid, 160² verts) | `LowPolyZoneGen.BuildIslandWater` | Correct |
| Vertex colour gradient (WaterShallow → WaterDeep → FoamEdge ring) | `BuildIslandWater` line 801–807 | Correct — do not overwrite with foam from shader alone |
| In-shader swell (crossed sines, `_WaveAmp=0.45`, `_WaveLen=11`, `_WaveSpeed=1.1`) | `LowPolyVertexColor.shader` lines 96–103 | Correct — carry to new shader |
| Fog-cap (`_FogCap = 0.5` on water material) | `LowPolyVertexColor.shader` lines 131–145 + `MakeWaterMaterial` | Critical — must port to new transparent shader |
| Opaque queue (blocks depth foam) | `LowPolyVertexColor.shader` Tags line 41 | This is the constraint to lift with a new shader |
| SRP-Batcher compliance (CBUFFER) | `LowPolyVertexColor.shader` lines 60–66 | Required in new shader |
| AlwaysIncludedShaders registration | `WorldBootstrap.EnsureShaderAlwaysIncluded` | New shader needs same registration |
| Foam baked into terrain mesh vertex colour | `IslandColorAt` lines 502–504 | Complementary — keep; the shader-side foam catches dynamic objects |
| Foam baked into water mesh vertex colour ring | `BuildIslandWater` lines 804–807 | Complementary — the shader-side foam ADDS dynamic intersections on top |

### Step-by-step adoption plan

**Step 1 — Enable URP Asset depth / opaque textures (prerequisite)**
In `BootstrapProject.Run` (or asserted in the existing URP Asset config), enable:
- `universalRenderPipelineAsset.supportsCameraDepthTexture = true`
- `universalRenderPipelineAsset.supportsCameraOpaqueTexture = true` (needed for opaque-texture-based
  refraction if we ever want it; not strictly required for depth-only foam, but harmless to enable)

These are per-URP-Asset settings. The bootstrap already configures the URP Asset; add these two lines
in the same pass. Zero render-cost on desktop.

**Step 2 — Author `Assets/Shaders/LowPolyWater.shader`**

Fork `LowPolyVertexColor.shader`. Key changes:

Tags:
```hlsl
Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
```

Blend + depth state (ForwardLit pass):
```hlsl
Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off
```

New HLSL include (top of HLSLPROGRAM, after Core.hlsl + Lighting.hlsl):
```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
```

New CBUFFER properties (inside `CBUFFER_START(UnityPerMaterial)` — SRP-Batcher rule):
```hlsl
float _FoamDistance;   // world-units depth difference that maps to full foam (ocean ~1.5, pond ~0.8)
float _FoamStrength;   // global foam intensity (0–1; ocean ~0.7, pond ~0.85)
float4 _FoamColor;     // warm off-white: (0.91, 0.89, 0.82, 1) = FoamEdge from the palette
float _RippleAmp;      // short-wavelength ripple amplitude (pond ~0.02, ocean 0 → use WaveAmp only)
float _RippleLen;      // short-wavelength ripple period in world-units (pond ~3.0)
float _RippleSpeed;    // ripple phase speed (pond ~0.6)
float _RimIntensity;   // Fresnel brightening 0=off (default), 0.1 = subtle sky-glint
float _RimPower;       // Fresnel power (2.0 = soft wrap)
```

Keep from parent shader: `_Tint`, `_WaveAmp`, `_WaveLen`, `_WaveSpeed`, `_FogCap`.

Varyings: add `float4 positionNDC : TEXCOORD3;` to carry the NDC position for depth sampling.

Vert changes: add `OUT.positionNDC = ComputeScreenPos(pos.positionCS);` after the existing pos block.
Add ripple term alongside existing swell (guard on `_RippleAmp > 0`):
```hlsl
// PATTERN — not production code
if (_RippleAmp > 0.0) {
    float kr = 6.2831853 / max(_RippleLen, 0.001);
    float tr = _Time.y * _RippleSpeed;
    float ripple = sin(posWS0.x * kr + tr) + sin(posWS0.z * kr * 1.17 + tr * 0.83) * 0.7;
    posOS.y += ripple * _RippleAmp * 0.5;
}
```

Frag changes — add after `finalCol` assembly (before fog-cap block):
```hlsl
// DEPTH-FADE INTERSECTION FOAM — PATTERN, not production code
float2 screenUV = IN.positionNDC.xy / IN.positionNDC.w;
float rawDepth = SampleSceneDepth(screenUV);
float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
float fragEyeDepth = IN.positionNDC.w;   // .w of ComputeScreenPos == eye depth in URP
float depthDiff = saturate((sceneEyeDepth - fragEyeDepth) / max(_FoamDistance, 0.001));
float foamMask = (1.0 - depthDiff) * _FoamStrength;
finalCol = lerp(finalCol, _FoamColor.rgb, foamMask);
```

Frag changes — Fresnel (add before foam lerp so foam overrides it near the shore):
```hlsl
// PATTERN — not production code
float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
finalCol += finalCol * rim * _RimIntensity;
```

Port fog-cap block verbatim (lines 131–145 of `LowPolyVertexColor.shader`) — unchanged.

Alpha: return `half4(finalCol, 1.0)` — full opacity. Transparency is used to GET INTO the
transparent queue for depth sampling, not to make the water visually semi-transparent. If
partial alpha is desired at water edges later, expose `_Alpha` property; default 1.

ShadowCaster pass: carry unchanged from `LowPolyVertexColor.shader`.

**Step 3 — Update `MakeWaterMaterial` and add `MakePondMaterial`**

In `LowPolyZoneGen.cs`, change `MakeWaterMaterial` to reference `"FarHorizon/LowPolyWater"` instead
of `"FarHorizon/LowPolyVertexColor"`. Set the new foam / ripple defaults for the ocean:

```csharp
// PATTERN — not production code
mat.SetFloat("_FoamDistance", 1.5f);   // ocean foam band: 1.5 world-units reads as soft surf fringe
mat.SetFloat("_FoamStrength", 0.65f);  // moderate — baked vertex foam ring already carries the bulk
mat.SetColor("_FoamColor", new Color(0.91f, 0.89f, 0.82f, 1f)); // == FoamEdge constant
mat.SetFloat("_RippleAmp", 0.0f);      // ocean: use the big swell only (ripple lost at sea scale)
mat.SetFloat("_RimIntensity", 0.0f);   // off by default for ocean (toy read dominates)
```

Add a new method `MakePondMaterial(string assetPath)` for the freshwater pond:

```csharp
// PATTERN — not production code
mat.SetFloat("_WaveAmp", 0.04f);       // pond: tiny vertical motion (calm, not ocean-scale)
mat.SetFloat("_WaveLen", 4.0f);        // short ripple period
mat.SetFloat("_WaveSpeed", 0.5f);      // slow, gentle
mat.SetFloat("_RippleAmp", 0.015f);    // secondary crossed ripple — surface agitation
mat.SetFloat("_RippleLen", 2.5f);
mat.SetFloat("_RippleSpeed", 0.4f);
mat.SetFloat("_FoamDistance", 0.7f);   // tight foam band — the pond is small; foam reads at the bank
mat.SetFloat("_FoamStrength", 0.85f);  // stronger — the pond bank is the primary liveness cue
mat.SetColor("_FoamColor", new Color(0.91f, 0.89f, 0.82f, 1f));
mat.SetFloat("_FogCap", 0.0f);         // pond is near-field — fog-cap not needed (it's never far)
mat.SetFloat("_RimIntensity", 0.0f);   // off; the pond reads by colour, not glint
```

**Step 4 — Add a `BuildPond` method for the M-U2 thirst feature**

The pond doesn't yet exist in the codebase (`grep` on "pond/Pond/thirst" found no existing builder).
A new `BuildPond(GameObject parent, string name, Material pondMat, Vector3 centre, float radius)`
in `LowPolyZoneGen.cs` builds a low-poly disc at `WaterY` (or slightly above terrain level). The
disc should have enough subdivisions to show the ripple (~16-sided polygon is enough for a ~5u
radius pond). The vertex colours: all `WaterShallow` (pond is shallow, no need for a deep band).
The terrain mesh at the pond location needs a corresponding depression — either an authored dip at
the pond's XZ coordinates (handled by the `BuildIslandHeight` terrain generator, lowering those
cells below `WaterY`) or a separate terrain edit sculpting the hollow. This is the Devon/Drew
implementation detail; Erik flags it as the architectural seam.

**Step 5 — Register new shader in AlwaysIncludedShaders**

In `WorldBootstrap.EnsureShaderAlwaysIncluded` (wherever the existing registration lives), add:
```csharp
// PATTERN — not production code
EnsureShaderAlwaysIncluded("FarHorizon/LowPolyWater");
```
Same pattern as the existing `LowPolyVertexColor` registration. Without this the shader strips in
the standalone build → magenta water.

### Shared vs separate material — recommendation

**Use a single `LowPolyWater.shader`, two material INSTANCES** (ocean mat + pond mat), configured
by property values. This is the SRP-Batcher-safe pattern: two material instances each get their own
CBUFFER block; the shader variant is shared. Do NOT use two separate shader files — the fog-cap port,
foam math, and ShadowCaster pass would duplicate needlessly.

The ocean material keeps the current asset path (`Assets/Materials/LowPolyWaterMat.asset`). The pond
material is a new asset (`Assets/Materials/LowPolyPondMat.asset`). `BootstrapProject` / `MakeWaterMaterial`
already creates the ocean mat; `MakePondMaterial` creates the pond mat (new method).

### Two-context tuning table

| Property | Ocean | Pond | Rationale |
|---|---|---|---|
| `_WaveAmp` | 0.45 | 0.04 | Ocean swell is visible at 700u; pond just breathes |
| `_WaveLen` | 11 | 4 | More crests in frame for ocean; shorter period for pond ripple |
| `_WaveSpeed` | 1.1 | 0.5 | Ocean travels; pond gently agitates |
| `_RippleAmp` | 0 | 0.015 | Ripple lost at ocean scale; adds surface texture to pond |
| `_FoamDistance` | 1.5u | 0.7u | Ocean beach gets a ~1.5u warm fringe; pond bank gets tight foam |
| `_FoamStrength` | 0.65 | 0.85 | Pond bank foam is the primary liveness signal |
| `_FogCap` | 0.5 | 0.0 | Ocean must not dissolve to sky; pond is near-field |

### Ranked impact-vs-effort

**Rank 1 — Depth-fade intersection foam on the ocean (new `LowPolyWater.shader` fork). Effort: 4–6h. Impact: high.**
The single largest read-quality gap. The ocean currently has baked vertex-colour foam (static) but
no dynamic intersection detection — rocks, stumps, and any new terrain features that intersect the
water line show a hard knife-edge join. The depth-fade foam replaces that with a soft warm-white
fringe that makes the coastline read as a shore instead of a seam. Also enables the pond shore to
read correctly when the pond is built for M-U2.

File: new `Assets/Shaders/LowPolyWater.shader` + `MakeWaterMaterial` reference update +
`WorldBootstrap` registration + URP Asset depth texture enable.

**Rank 2 — Pond builder + `MakePondMaterial` for M-U2 thirst. Effort: 2–4h (shader already done after Rank 1). Impact: critical for feature.**
The pond does not yet exist. The `LowPolyWater.shader` from Rank 1 covers the visual side; Rank 2
is the mesh builder + material factory + terrain depression integration. Short-wavelength ripple
(`_RippleAmp 0.015`) gives the pond the lively-but-calm surface feel the Sponsor prefers.

File: new `BuildPond` method in `LowPolyZoneGen.cs` + `MakePondMaterial` + terrain sculpt at
pond location.

**Rank 3 — Subtle Fresnel rim on the ocean (optional upgrade to Rank 1 shader). Effort: 30min (additive to Rank 1). Impact: low-medium.**
A `pow(1 - dot(n,v), 2.0) * 0.08` term adds a barely-perceptible brightening at grazing view
angles that the eye reads as "the water catches the sky." Default off (`_RimIntensity = 0`) —
enable on the ocean material instance via material property. Only activate after a soak confirms
the foam read is right; don't tune two things at once.

File: `Assets/Shaders/LowPolyWater.shader` (already inside Rank 1 scope — zero extra file cost).

---

## What NOT to do

- **Refraction / opaque texture sampling:** The inspiration images show zero visible refraction —
  the water is opaque bright teal. Refraction (sampling `_CameraOpaqueTexture` behind the water
  surface) would make the water partially transparent-to-the-seabed, fighting the toy read. Exclude
  for M-U2; revisit only if the Sponsor explicitly asks for "you can see through the water."

- **Voronoi surface foam texture:** Some stylized water shaders (Zelda Wind Waker-style) add
  animated voronoi patterns on the water surface as a foam-texture detail. This fights the flat
  faceted look the board shows (clean colour, no surface noise). The Sponsor-approved Zone D look
  is smooth teal plane with a soft shore edge — not textured.

- **Converting ocean back to Opaque after adding depth foam:** Once the shader moves to Transparent
  to enable depth sampling, do NOT attempt a hybrid (opaque shader + depth sample trick). The
  render-queue ordering is the fundamental reason transparent shaders can read the opaque depth
  buffer; there is no clean way around it without a custom Renderer Feature (overkill for this
  project).

- **Hard-band toon step on foam edge:** A `step(0.5, foamMask)` creates a binary hard foam ring
  (the Roystan toon-water style). For Far Horizon's soft toy look, use `saturate` + `smoothstep`
  for a gradual dissolve. The hard ring works for cel-shaded games; our board shows no hard foam line.

- **Texture UV scrolling for waves:** Several stylized water tutorials animate waves by scrolling
  a noise texture. This adds a texture import and UV state to the material. Our vertex-sine approach
  is procedural, zero-texture, and already approved by the Sponsor (the swell ships). Keep it.

---

## Codebase baseline summary (do not regress)

| What | File | Rule |
|---|---|---|
| `_FogCap` fog-floor on water material | `LowPolyVertexColor.shader` → port to `LowPolyWater.shader` | Port verbatim; never remove |
| `_WaveAmp` crossed-sine swell | `LowPolyVertexColor.shader` lines 96–103 | Port verbatim |
| Baked vertex-colour foam ring on water mesh | `BuildIslandWater` lines 804–807 | Complementary — keep; the shader foam is additive |
| Baked vertex-colour foam on terrain mesh | `IslandColorAt` lines 502–504 | Keep — independent of shader path |
| SRP-Batcher CBUFFER | All new properties inside `CBUFFER_START(UnityPerMaterial)` | Required |
| AlwaysIncludedShaders registration | `WorldBootstrap.EnsureShaderAlwaysIncluded` | Required for new shader |
| Winding / facing on water grid | `BuildIslandWater` — normals point +Y; culling is Back | Do not change |

---

## Queued follow-up (flagged, not done in this slice)

**World-resource prop quality** — the next research slice covers stylized quality uplift on in-world
props (berries on bushes, campfire geometry, stone-pile crafting table, etc.) that are required for
the M-U2 gameplay wave. These are separate from the water shader and do not gate on this slice.
Ticket 86ca8x038 remains in progress until that slice also lands.
