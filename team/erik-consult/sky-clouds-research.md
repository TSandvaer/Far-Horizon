# Sky + Clouds + Sunshine — Stylized Low-Poly Recipe

## Question

How do we best make a nice stylized sky with clouds and sunshine for Far Horizon's chunky
warm Zone-D look (warm/lush, faceted low-poly, bright & cheerful)? Sponsor's lead: mesh-based
low-poly clouds. What approach should the devs build as a POC for the Sponsor to soak and approve?
This is an ADDITIVE pass on top of the existing `GradientSkybox.shader` + `CloudBlob` + `CloudDrift`
that already ships in the build.

## Bottom line

The existing codebase already has a solid base: a 3-stop gradient skybox, faceted cyan `CloudBlob`
meshes, and `CloudDrift` lateral motion. The Sponsor wants more visual quality — brighter/nicer sky,
richer clouds, a visible sun. The recommended recipe for the POC is three additive improvements in
priority order: **(1) Sun disc in the `GradientSkybox.shader`** via a dot-product mask on the
directional light direction — highest Sponsor-visible impact, no new GameObjects, fully within OUR
existing custom HLSL shader (1–2h dev). **(2) Enhanced mesh cloud quality** — richer faceting and
a warm sunlit top-cap colour that reads against the sky (the current bright-white-cyan already
solved the contrast problem; this is about adding a soft warm highlight on the top face). **(3) URP
Screen Space Lens Flare** (post-processing, built into Unity 6 URP) on the directional light as a
warm bloom halo around the sun — adds the "sunshine feel" at zero new mesh cost. Do NOT switch to
shader-based cloud approaches (Shader Graph noise clouds, volumetric assets) — they break the
chunky hard-facet identity the art board demands and they depart from OUR already-built mesh route.

## Evidence

### Source 1 — Sponsor inspiration images (`inspiration/2026-06-12_21h10_44.png`, `21h16_13.png`, `21h13_31.png`)
**[Own project files, ground truth]** The cloud references show: hard-faceted cyan-to-white geometry
blobs floating against a clean bright-blue sky, NO visible sun disc in any reference, but `21h13_31`
shows a sky with warm diffuse light and soft ground shadows implying a sun off-frame. Clouds are
clearly MESH geometry (visible polygon edges), NOT smooth, NOT billboard sprites, NOT noise-shader.
The `21h10_44` asset sheet shows the canonical cloud silhouette: a cluster of 3–5 overlapping faceted
blobs forming a puffy mass wider than it is tall, in bright cyan-to-white range.
**Strength: Strong** — Sponsor-selected ground truth.

### Source 2 — Josh Marinacci "Procedural Geometry — Low Poly Clouds" (Medium, 2019)
URL: https://medium.com/@joshmarinacci/procedural-geometry-low-poly-clouds-b86a0e66bcad
**Summary:** Three.js tutorial demonstrating the technique the Sponsor flagged. Approach: merge 3
sphere geometries (two radius-1.5 flanking, one radius-2.0 centre) into a single mesh, apply
per-vertex random jitter (±0.2), flatten the base with a `chopBottom` min-y clamp, render
flat-shaded (`flatShading: true`) with `MeshLambertMaterial`. Dual directional lights provide the
top-lit/shadow value step. Runtime: Three.js / WebGL, not Unity, but the CONCEPT maps cleanly.
**Strength: Moderate** — technique-demonstrating article, single blog post, but directly cited by
Sponsor. The Three.js implementation is conceptually identical to our existing `CloudBlob`
(`AppendFlatBlob` = the sphere-merge + flat normals + vertex colour). Our impl already exceeds this
reference in fidelity (3-value colour palette, outward-winding guard, SRP-batcher-compatible).

### Source 3 — Kelvin van Hoorn "Unity Skybox Shader" tutorial
URL: https://kelvinvanhoorn.com/tutorials/unity_skybox_shader/
**Summary:** Demonstrates a pure HLSL skybox shader with an embedded sun disc. The sun disc is a
`step()` mask on `dot(viewDir, sunDir)`: when the dot product exceeds a threshold `(1 - radius²)`, the
pixel is inside the sun disc and receives sun colour. The `_WorldSpaceLightPos0` built-in carries the
directional light's direction. A `Range(0,1)` `_SunRadius` property drives the size. Bloom
post-processing adds the soft glow halo around the disc without any extra geometry.
**Strength: Strong** — cited technique is based on well-known dot-product sphere-cap math; the
`_WorldSpaceLightPos0` hook into Unity's built-in lighting uniforms is documented Unity API. The
implementation is pure HLSL compatible with OUR existing `GradientSkybox.shader` pattern (same shader,
same pass, no new asset type needed).

### Source 4 — Pinwheel Studios "Unity sky shader — procedural animated sky in ShaderLab"
URL: https://www.pinwheelstud.io/post/unity-sky-shader-making-an-procedural-animated-sky-in-shaderlab
**Summary:** Alternative sun disc approach using ray-sphere intersection. The dot-product mask (Source 3)
is simpler, faster, and sufficient for a static sun (we have no day/night cycle). Both approaches
agree the sun disc lives inside the skybox shader pass.
**Strength: Moderate** — useful corroboration of the technique class; no new approach information.

### Source 5 — Unity 6 Docs: Screen Space Lens Flare (URP)
URL: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shared/lens-flare/post-processing-screen-space-lens-flare.html
**Summary:** Unity 6 URP includes a built-in Screen Space Lens Flare post-processing override. It
identifies bright screen areas (including our sun disc if we add it as an emissive/bright pixel in
the skybox) and renders warm streaks/warped flares around them. Key constraint: requires Bloom
override to have Intensity > 0 (we already have bloom in `QualityPassGen.cs`). The Lens Flare (SRP)
component can also be added directly to the directional light — it renders independently of bloom
and gives fine-grained artistic control (element count, stretch, colour, occlusion by geometry).
**Strength: Strong** — official Unity 6 documentation, directly applicable to our URP + URP Volume
pipeline. Zero external package dependency; it is baked into URP 17 (Unity 6).

### Source 6 — Mike Young "Stylized clouds with Shader Graph and Shuriken in Unity3D" (Medium)
URL: https://medium.com/@mikeyoung_97230/creating-stylized-clouds-with-shader-graph-and-shuriken-in-unity3d-ec8f12fb5f0a
**Summary:** Uses noise-animated Shader Graph + particle system for fluffy animated clouds. The clouds
deform over time via noise-driven vertex offset, producing an undulating blobby shape.
**Strength: Moderate** — technique works, but the output is smooth/animated-organic, NOT faceted
geometric. Ruled out for our case: (a) it conflicts with the hard-facet identity the art board demands,
(b) it introduces a per-particle overdraw cost, (c) it moves away from OUR procedural mesh route.
Included to explain why we reject it.

### Source 7 — Existing codebase: `GradientSkybox.shader`, `CloudBlob` in `LowPolyMeshes.cs`, `CloudDrift.cs`, `WorldBootstrap.cs`, `WorldLookNudgeTool.cs`
**[Own project files, ground truth]**
- `GradientSkybox.shader`: 3-stop vertical gradient, standard skybox render state (already correct post
  the PR #48 wash-bug fix). Zenith `#608ED9`, Mid `#99C7E6`, Horizon `#E1E6D7`. Pure HLSL, no Shader
  Graph dependency. The sun disc can be added in the same `frag` function without a new pass.
- `CloudBlob`: `AppendFlatBlob` — faceted spheroid cluster, hard face normals (flat-shaded), outward
  winding enforced, 3-value vertex-colour palette (Body `#C7EAF2` / Top `#E6F7FC` / Shadow `#5AA8B5`).
  6–10 clouds placed at alt 28–42u, radius 5–7.5u.
- `CloudDrift`: lateral `Transform.position` += `windDir * speed * deltaTime` in `Update()`. Speed
  0.22–0.48 u/s, single shared wind direction. Wraps at band edge. **Already drifting — the Sponsor's
  "lively, not static" motion requirement is already met.**
- `WorldLookNudgeTool`: F9 runtime dial for sky stops / cloud count+scale. Sun disc parameters
  (`_SunRadius`, `_SunColor`, `_SunStrength`) should be added to this nudge surface so the Sponsor
  dials the final sun look himself.
**Strength: Strong** — read from source.

## Application to Far Horizon

### What we have vs what is missing

| Surface | Current state | Gap |
|---|---|---|
| Sky gradient | 3-stop warm HLSL gradient, tuned | No sun disc, no sun direction |
| Clouds | Faceted cyan mesh blobs, 6–10, drifting | Art-board refs show bright WHITE clouds; current body is near-white but no warm top-cap flush |
| Sun/sunshine | Directional light exists (scene lighting) | No visible sun object in sky |
| Drift/motion | `CloudDrift` lateral translate ~0.3u/s | Meets "lively not static" |
| Bloom | On, intensity 0.25 | Can feed sun disc glow |

### Ranked POC recipe (build in this order, each is independently soakable)

**Rank 1 — Sun disc in `GradientSkybox.shader`** (effort: ~1h, impact: HIGH)

Add two properties to the existing shader: `_SunDir` (set from C# matching the scene's directional
light rotation) and `_SunRadius` (Range 0.01, 0.15) / `_SunColor` (warm HDR yellow-white). In `frag`,
after computing the gradient colour, compute:

```hlsl
float sunDot = dot(normalize(IN.dir), normalize(_SunDir.xyz));
float sunMask = step(1.0 - _SunRadius * _SunRadius, sunDot);
col = lerp(col, _SunColor.rgb, sunMask);
```

A small `_SunGlow` smooth halo around the disc is a second `smoothstep` on the same dot:

```hlsl
float halo = smoothstep(1.0 - _SunGlowRadius * _SunGlowRadius,
                        1.0 - _SunRadius * _SunRadius, sunDot);
col = lerp(col, _SunGlowColor.rgb * _SunGlowStrength, halo);
```

The directional light direction is read from C# at runtime and pushed to the skybox material:
`RenderSettings.skybox.SetVector("_SunDir", -light.transform.forward)`.
A `SkyboxSunSync.cs` MonoBehaviour (added to the boot scene, serialized editor-time per our
editor-vs-runtime trap convention) does this once in `Start()` — no per-frame cost.
Add `_SunRadius`, `_SunColor`, `_SunGlowRadius`, `_SunGlowStrength` to the `WorldLookNudgeTool`
so the Sponsor dials the sun size and warmth himself.
**Registration note:** the shader already lives in `GraphicsSettings.AlwaysIncludedShaders` via
`QualityPassGen` — no new registration needed.

**Rank 2 — URP Lens Flare (SRP) on the directional light** (effort: ~30min, impact: HIGH)

Add a `Lens Flare (SRP)` component to the directional light in the Boot scene (serialized editor-time).
Configure: a warm golden circle flare element (warm orange-yellow, low intensity ~0.3), a horizontal
streak element (the "sunshine shimmer" read at low intensity). No extra bloom needed — our existing
bloom at intensity 0.25 feeds it. The flare occludes correctly when the sun is behind geometry (Unity
6 URP's built-in Lens Flare handles occlusion per-pixel). This adds the "sunshine feel" the Sponsor
is after without touching the sky shader.
**Risk:** the flare won't appear without a bright source. The sun disc in Rank 1 provides that source
as a bright skybox pixel. Do Rank 1 first; add the lens flare immediately after.

**Rank 3 — Warm sunlit top-cap on `CloudBlob`** (effort: ~1–2h, impact: MEDIUM)

The current top-cap colour `#E6F7FC` (brilliant near-white cyan) is correct for contrast but reads
as cold-blue in the warm scene lighting. The board (`21h13_31`, `21h16_13`) shows clouds with a
warm-cream/ivory top face in direct sun, a body that is bright white, and a teal shadow underside.
Change `CloudTop` in `WorldBootstrap.cs` from `new Color(0.90f, 0.97f, 0.99f)` toward a warm
near-white like `new Color(0.97f, 0.96f, 0.90f)` (#F7F5E6 — warm cream). This makes the cloud
read as SUN-LIT rather than sky-lit. The shadow underside (`#5AA8B5`) stays the same — the value
contrast (bright warm top / cool teal bottom) is the whole 3-value read.
**This change is dialable live from the existing `WorldLookNudgeTool`** (Sponsor can adjust the
colour stops himself before the devs bake it).

### What to AVOID for this game

- **Shader Graph noise clouds** — smooth organic look conflicts with hard-facet art board; introduces
  Shader Graph dependency on a surface that is already well-served by our HLSL mesh route.
- **Volumetric / billboard cloud assets** (Cloudscape, Altos, etc.) — paid/third-party packages,
  billboards read as flat sprites at close orbit distance, not stylistically compatible.
- **Day/night cycle hooks** — out of scope per ticket; adding `_WorldSpaceLightPos0` read to the sky
  shader does NOT imply adding a day/night cycle. Keep the directional light angle fixed.
- **Animated cloud mesh deformation** — the CloudDrift translate already gives life; mesh vertex
  animation (noise deform) is visual noise that conflicts with the static geometric facet read.

### Integration notes for Devon / Drew

- `GradientSkybox.shader` lives at `Assets/Shaders/GradientSkybox.shader` — edit in place; it is
  already registered. No new shader assets needed.
- Add `SkyboxSunSync.cs` under `Assets/Scripts/Runtime/` — a short MonoBehaviour that pushes
  `-light.transform.forward` to `RenderSettings.skybox` once on `Start()`. Must be added to Boot
  scene via `BootstrapProject.Run` (serialized editor-time per the editor-vs-runtime trap).
- Add sun-disc properties to `WorldLookNudgeTool.cs` so the Sponsor dials them in the F9 tool.
- Cloud top-cap colour change: `WorldBootstrap.cs` line `static readonly Color CloudTop = ...`.
  Dialable from the existing cloud-colour nudge target in `WorldLookNudgeTool`.
- Lens Flare: add component in `BootstrapProject.Run` to the directional light GO (or serialize
  the `.asset` under `Assets/Settings/`).
- **Testing bar:** this is a visual-only change (no game logic). Dev authors a Self-Test Report with
  gameplay-orbit captures (post-processing ON, the orbit cam at real pitch) showing (a) sun disc
  visible in sky, (b) clouds drifting, (c) no sky wash regression (the PR #48 class). Tess approves.
  Sponsor soaks the shipped exe.
- `WorldLookConfig.cs` does NOT need new constants for the sun disc defaults — they live as
  Properties defaults in the shader itself (same pattern as `_ZenithColor`, `_MidColor`, etc.).

---

## Consolidated POC-brief addenda (folded from `lowpoly-stylized-sky-research.md`, 2026-06-29)

A second Erik note (POC-brief framing) covered the same bottom line, the same three additive ranks,
and the same exclusions as above. To avoid a duplicate file it was merged here; the sections below
are the brief-specific content this recipe did not already carry.

### Approach-selection trade-off table (why custom HLSL gradient skybox wins)

| Approach | Fit for Zone-D chunky look | Fog seam risk | GPU cost | GC per frame | Notes |
|---|---|---|---|---|---|
| **Custom HLSL gradient skybox** (our approach) | Exact — we control all stops | None (horizon matched by palette constant) | One background draw call, ~10 ALU instructions | Zero — pure shader, no C# in hot path | Already shipping; extend in place |
| Procedural Skybox (Unity built-in) | Partial — realistic Rayleigh scattering reads naturalistic, not cartoon | Low but tunable | Similar | Zero | No Shader Graph; `_WorldSpaceLightPos0` auto-available but less control |
| Skybox material (6-sided / cubemap) | Wrong — baked texture loses runtime colour control the NudgeTool relies on | Seam fixed at bake time | One background draw | Zero | Ruled out: no runtime dial |
| Sky-dome mesh (large inverted sphere) | Viable but redundant | Must manually disable fog on the dome mesh | Adds a MeshRenderer draw call; can break GPU Resident Drawer if using MaterialPropertyBlock | Zero if no MPB | No gain over shader route for our case |
| Shader Graph skybox | Same as HLSL but loses gradient property exposability | Same | Same | Zero | Ruled out: property limitation |

### Fog composition / horizon-seam dependency (NEW — not in the recipe above)

**Source:** Unity Forum — "What's the best way to deal with fog and horizon in URP?"
(https://discussions.unity.com/t/whats-the-best-way-to-deal-with-fog-and-horizon-in-urp/780400)
**[Moderate — practitioner discussion]** + Unity Docs `RenderSettings.fogColor`
(https://docs.unity3d.com/ScriptReference/RenderSettings-fogColor.html) **[Strong — official]**.

- **URP built-in fog applies only to mesh surfaces, NOT to the scene background (skybox).** The skybox
  horizon colour and the fog colour must be matched manually — the fog never "fades into" the skybox
  automatically.
- The codebase already handles this: `QualityPassGen.EnableGlobalFog()` sets
  `RenderSettings.fogColor = WorldLookPalette.SkyHorizon`, the same constant as the skybox's
  `_HorizonColor`. The `_FogCap` floor in `LowPolyVertexColor.shader` additionally prevents the teal
  ocean showing through fog at the horizon.
- **Do not change `_HorizonColor` without also updating `RenderSettings.fogColor`** — the seam
  protection depends on the two being identical. Add this to the POC's regression check (alongside
  the PR #48 wash guard).
- `DynamicGI.UpdateEnvironment()` must be called after changing the skybox material's colours at
  runtime to update the ambient probe — relevant only if a later milestone adds a day-tint feature.

### GPU-Resident-Drawer note (NEW)

Skyboxes render through Unity's dedicated background draw, NOT through MeshRenderer instancing — so
GPU Resident Drawer simply does not apply to the skybox pass; it is irrelevant to approach selection.
A **sky-dome mesh** WOULD go through MeshRenderer and WOULD be subject to GPU-Resident-Drawer
disqualifiers (e.g. MaterialPropertyBlock) — an additional reason to prefer the shader-only route.

### Day / time-tint scope decision (NEW)

A day-tint would require (a) a C# script updating `_ZenithColor` / `_MidColor` / `_HorizonColor` +
`RenderSettings.fogColor` + `DynamicGI.UpdateEnvironment()` per tick, and (b) matching light-colour
changes. Technically straightforward via `RenderSettings.skybox.SetColor()`, but it belongs in a
**later milestone** (weather/atmosphere system), not this POC. The POC keeps the directional light
angle fixed; the `WorldLookNudgeTool` F9 dial already lets the Sponsor adjust sky stops manually,
which is sufficient for the soak.

### Files-in-scope (consolidated)

| File | Change |
|---|---|
| `Assets/Shaders/GradientSkybox.shader` | Add sun disc + halo properties + frag logic (in-place) |
| `Assets/Scripts/Runtime/SkyboxSunSync.cs` | NEW — pushes directional light dir to skybox material on `Start()` |
| `Assets/Scripts/Runtime/WorldBootstrap.cs` | Change `CloudTop` to warm-cream tint |
| `Assets/Scripts/Editor/WorldLookNudgeTool.cs` | Expose new sun-disc properties on the F9 dial |
| Boot scene / `BootstrapProject.Run` | Add `SkyboxSunSync` MonoBehaviour + Lens Flare (SRP) component to the directional light |
