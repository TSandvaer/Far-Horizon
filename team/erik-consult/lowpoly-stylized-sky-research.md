# Low-Poly Stylized Sky — POC Research

## Question

What is the best low-poly stylized sky approach for Far Horizon's Unity 6 URP Zone-D look
(chunky-cartoon, warm/lush, small-character/big-world), and what exactly should the dev POC build
for the Sponsor to soak?

## Bottom line

**A custom HLSL gradient skybox shader (already shipping as `GradientSkybox.shader`) extended with
a sun disc + smooth halo is the correct approach.** Mesh-based faceted cloud blobs (already
shipping as `CloudBlob` + `CloudDrift`) are the correct cloud primitive — they match the art board
exactly. The POC adds three things in priority order: **(1) sun disc + warm halo in
`GradientSkybox.shader`** via dot-product mask on the directional light direction (~1h); **(2) URP
Lens Flare (SRP) on the directional light** for the "sunshine shimmer" at zero mesh cost (~30min);
**(3) warm-cream top-cap colour** on `CloudBlob` to read sun-lit rather than sky-lit (~30min). All
three are dialable from the existing `WorldLookNudgeTool` (F9) before the Sponsor soaks. Day/time
tinting is out of scope for the POC — keep the directional light angle fixed.

## Evidence

### Source 1 — Inspiration images (`inspiration/2026-06-12_21h10_44.png`, `21h13_31.png`, `21h16_13.png`)
**[Own project files — Sponsor-selected ground truth, Strong]**
The three sky-relevant images establish: the sky is a clean bright blue (NOT overcast, NOT dramatic),
clouds are hard-faceted MESH blobs in cyan-to-white range (`21h10_44` shows the asset sheet
explicitly), NOT smooth, NOT noise-shader, NOT billboards. `21h13_31` shows a warm diffuse feel
with soft ground shadows implying an off-frame sun; `21h16_13` shows a distant mountain + pine
scene with a small raining cloud blob and a clean sky above. No sun disc is visible in any reference,
but the warm-lit feel requires one in scene to drive the Lens Flare and give the directional light
visual presence.

### Source 2 — Kelvin van Hoorn, "Unity Skybox Shader Tutorial" (2022)
URL: https://kelvinvanhoorn.com/tutorials/unity_skybox_shader/
**[Strong — cited technique based on documented Unity built-in uniforms]**
The canonical dot-product sun disc technique: `step(1.0 - _SunRadius², dot(viewDir, sunDir))`.
`_WorldSpaceLightPos0` carries the directional light direction as a Unity built-in uniform — no
per-frame C# push needed at runtime (shader reads it automatically). A `smoothstep` on the same
dot at a slightly wider radius produces the halo glow without extra geometry.

### Source 3 — Tim Coster, "Unity ShaderGraph Procedural Skybox Tutorial" (2019, still current technique)
URL: https://timcoster.com/2019/09/03/unity-shadergraph-skybox-quick-tutorial/
**[Moderate — tutorial; technique is correct and maps to our HLSL path]**
Gradient skybox via three exposed Color properties (Sky / Horizon / Ground) blended with Power
nodes on remapped world-space Y. Key finding: gradients cannot be exposed as Shader Graph properties
— confirmed our HLSL approach (three `_ZenithColor` / `_MidColor` / `_HorizonColor` properties in
`GradientSkybox.shader`) is the right choice. A ShaderGraph variant would lose the property
exposability we rely on for the `WorldLookNudgeTool`.

### Source 4 — Unity 6 Docs: Screen Space Lens Flare (URP)
URL: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shared/lens-flare/post-processing-screen-space-lens-flare.html
**[Strong — official Unity 6 documentation]**
Built into URP 17 (Unity 6), zero extra package. Requires Bloom Intensity > 0 (already satisfied:
`QualityPassGen.cs` sets bloom at 0.25). Flare component added directly to the directional light
provides per-element artistic control (warm golden circle + horizontal streak) and correct
geometry occlusion. The sun disc (Source 2) provides the bright screen pixel the flare reads from.

### Source 5 — Unity Forum, "What's the best way to deal with fog and horizon in URP?"
URL: https://discussions.unity.com/t/whats-the-best-way-to-deal-with-fog-and-horizon-in-urp/780400
**[Moderate — practitioner discussion, Unity forums]**
Key finding: **URP built-in fog applies only to mesh surfaces, NOT to the scene background
(skybox).** This means the skybox horizon colour and the fog colour must be matched manually — the
fog never "fades into" the skybox automatically. Our codebase already handles this correctly:
`QualityPassGen.EnableGlobalFog()` sets `RenderSettings.fogColor = WorldLookPalette.SkyHorizon`,
which is the same constant as the skybox's `_HorizonColor`. The `_FogCap` floor in
`LowPolyVertexColor.shader` additionally prevents the teal ocean from showing through fog at the
horizon. **Do not change `_HorizonColor` without also updating `RenderSettings.fogColor`** — the
seam protection depends on the two being identical.

### Source 6 — Unity Docs: RenderSettings scripting API
URL: https://docs.unity3d.com/ScriptReference/RenderSettings-fogColor.html
**[Strong — official Unity scripting reference]**
`RenderSettings.fogColor` and `RenderSettings.skybox.SetVector()` are the runtime hooks for
keeping fog and sky in sync. `DynamicGI.UpdateEnvironment()` must be called after changing the
skybox material's colours at runtime to update the ambient probe — relevant if we ever add
a day-tint feature in a later milestone.

### Source 7 — Existing codebase: `GradientSkybox.shader`, `CloudBlob`, `CloudDrift`, `WorldBootstrap`, `WorldLookNudgeTool`
**[Own project files — Strong ground truth]**
The codebase already ships: a 3-stop HLSL gradient skybox (Zenith `#608ED9` / Mid `#99C7E6` /
Horizon `#E1E6D7`); faceted `CloudBlob` geometry with 3-value vertex-colour palette
(Body `#C7EAF2` / Top `#E6F7FC` / Shadow `#5AA8B5`), 6–10 instances at alt 28–42u;
`CloudDrift` lateral translate ~0.3u/s (the Sponsor's "lively not static" requirement already met);
`WorldLookNudgeTool` F9 dial for sky stops + cloud count+scale. What is MISSING: a sun disc in
the sky, a lens flare on the directional light, and a warm (rather than cold-cyan) top-cap on
the clouds.

## Application to Embergrave / Far Horizon

### Approach selection: why the custom HLSL gradient skybox shader wins

| Approach | Fit for Zone-D chunky look | Fog seam risk | GPU cost | GC per frame | Notes |
|---|---|---|---|---|---|
| **Custom HLSL gradient skybox** (our approach) | Exact — we control all stops | None (horizon matched by palette constant) | One background draw call, ~10 ALU instructions | Zero — pure shader, no C# in hot path | Already shipping; extend in place |
| Procedural Skybox (Unity built-in) | Partial — realistic Rayleigh scattering reads naturalistic, not cartoon | Low but tunable | Similar | Zero | No Shader Graph; `_WorldSpaceLightPos0` auto-available but less control |
| Skybox material (6-sided / cubemap) | Wrong — baked texture loses runtime colour control the NudgeTool relies on | Seam fixed at bake time | One background draw | Zero | Ruled out: no runtime dial |
| Sky-dome mesh (large inverted sphere) | Viable but redundant | Must manually disable fog on the dome mesh | Adds a MeshRenderer draw call; can break GPU Resident Drawer if using MaterialPropertyBlock | Zero if no MPB | No gain over shader route for our case |
| Shader Graph skybox | Same as HLSL but loses gradient property exposability (confirmed Source 3) | Same | Same | Zero | Ruled out: property limitation |

**GPU Resident Drawer note:** skyboxes render through Unity's dedicated background draw, NOT through
MeshRenderer instancing. GPU Resident Drawer simply does not apply to the skybox pass — it is
irrelevant to approach selection. A sky-dome mesh WOULD go through MeshRenderer and WOULD be
subject to GPU Resident Drawer disqualifiers (e.g. MaterialPropertyBlock) — another reason to
prefer the shader-only route.

### Day / time tint: scope decision

A day-tint would require: (a) C# script updating `_ZenithColor` / `_MidColor` / `_HorizonColor`
+ `RenderSettings.fogColor` + `DynamicGI.UpdateEnvironment()` per tick; (b) matching light colour
changes. Technically straightforward via `RenderSettings.skybox.SetColor()`. However it belongs in
a **later milestone** (weather/atmosphere system), not the POC. The POC keeps the directional light
angle fixed. The `WorldLookNudgeTool` F9 dial already lets the Sponsor adjust sky stops manually,
which is sufficient for the soak.

### POC build spec (single approach, three additive ranks)

**Rank 1 — Sun disc + warm halo in `GradientSkybox.shader`** (~1h, impact HIGH)

`GradientSkybox.shader` frag addition after the gradient colour is computed:

```hlsl
// Sun disc (dot-product sphere-cap mask)
float3 sunDir = normalize(_SunDir.xyz);
float sunDot  = dot(normalize(IN.dir), sunDir);
float sunMask = step(1.0 - _SunRadius * _SunRadius, sunDot);
float halo    = smoothstep(1.0 - _SunGlowRadius * _SunGlowRadius,
                           1.0 - _SunRadius * _SunRadius, sunDot);
col = lerp(col, _SunGlowColor.rgb * _SunGlowStrength, halo);
col = lerp(col, _SunColor.rgb, sunMask);
```

New properties: `_SunDir (Vector)`, `_SunRadius (Range 0.01,0.15) = 0.04`,
`_SunColor (HDR Color) = (1.0, 0.96, 0.75, 1)` (warm yellow-white),
`_SunGlowRadius (Range 0.05,0.35) = 0.18`, `_SunGlowColor (HDR Color) = (1.0, 0.88, 0.55, 1)`,
`_SunGlowStrength (Range 0,1) = 0.25`.

`_SunDir` can be set from `_WorldSpaceLightPos0` automatically in shader (directional light
direction is a Unity built-in); or pushed once from `SkyboxSunSync.cs` in `Start()` via
`RenderSettings.skybox.SetVector("_SunDir", -light.transform.forward)`. The MonoBehaviour route
gives explicit control and aligns with our editor-vs-runtime serialization convention.

Expose `_SunRadius`, `_SunColor`, `_SunGlowStrength` in `WorldLookNudgeTool` F9 dial.

**Rank 2 — URP Lens Flare (SRP) on the directional light** (~30min, impact HIGH)

Add `Lens Flare (SRP)` component to the directional light in `BootstrapProject.Run` (or serialize
the `.asset` under `Assets/Settings/`). Configure: warm golden circle element (orange-yellow ~0.3
intensity) + horizontal streak (low intensity ~0.15). Feeds from the existing bloom at 0.25
intensity — no bloom change needed. The sun disc from Rank 1 provides the bright screen pixel the
flare amplifies. The flare correctly occludes when the sun is behind geometry (Unity 6 built-in
behaviour).

**Rank 3 — Warm sunlit top-cap on `CloudBlob`** (~30min, impact MEDIUM)

In `WorldBootstrap.cs`, change `CloudTop` from `new Color(0.90f, 0.97f, 0.99f)` (cold cyan-white)
toward `new Color(0.97f, 0.96f, 0.90f)` (~#F7F5E6, warm cream). The shadow underside `#5AA8B5`
stays — the value-contrast (bright warm top / cool teal bottom) is the whole 3-value read.
Dialable from the existing cloud-colour nudge target in `WorldLookNudgeTool` before baking.

### What to EXCLUDE from the POC

| Temptation | Why not |
|---|---|
| Shader Graph noise clouds (animated, smooth) | Conflicts with hard-facet art board identity; adds Shader Graph dependency on a well-served HLSL route |
| Billboard sprite clouds | Read as flat sprites at close orbit distance; not the art board |
| Volumetric cloud assets (Cloudscape, Altos) | Paid third-party; overkill; smooth look wrong |
| Day/night cycle / time-of-day tint | Out of scope per ticket; adds complexity the Sponsor hasn't requested |
| Animated cloud mesh vertex deformation | CloudDrift translate already gives the lively feel; vertex noise conflicts with geometric facets |
| Sky-dome mesh | Redundant; adds a MeshRenderer + potential GPU Resident Drawer friction; shader route is simpler |

### Testing bar for the POC

Visual-only change (no game logic). Dev posts a Self-Test Report with:
- Gameplay-orbit captures (post-processing ON, real orbit-cam pitch) showing (a) sun disc in sky,
  (b) warm halo around it, (c) clouds drifting with warm top-cap visible, (d) no sky wash / horizon
  seam regression (the PR #48 class).
- Confirm `_HorizonColor` still matches `RenderSettings.fogColor` (fog-seam guard intact).

Tess approves on the captures. Sponsor soaks the shipped exe.

### Files in scope

| File | Change |
|---|---|
| `Assets/Shaders/GradientSkybox.shader` | Add sun disc + halo properties + frag logic (in-place) |
| `Assets/Scripts/Runtime/SkyboxSunSync.cs` | NEW — pushes directional light dir to skybox material on Start() |
| `Assets/Scripts/Runtime/WorldBootstrap.cs` | Change `CloudTop` warm-cream tint |
| `Assets/Scripts/Editor/WorldLookNudgeTool.cs` | Expose new sun disc properties on the F9 dial |
| Boot scene / `BootstrapProject.Run` | Add `SkyboxSunSync` MonoBehaviour + Lens Flare component to directional light |
