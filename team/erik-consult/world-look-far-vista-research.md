# World-Look — URP Far-Horizon Vista Technique

## Question

In Unity 6 / URP, what is the best low-poly-faithful technique for rendering a BIG/ENDLESS
far-horizon vista (distant layered faceted mountain silhouettes + atmospheric fade dissolving
into the sky with no horizon seam) for the Far Horizon desktop game?
Uma's world-look brief deferred this to Erik. Drew will implement from this note.

## Bottom line

Use **Geo Rings + Sky-Matched Exponential Height Fog** (Route A) as the primary
implementation. Author 2–3 concentric rings of faceted low-poly mountain meshes at
500 / 1 000 / 1 500 u from origin, each ring progressively lower-poly and tinted toward
the horizon fog color. Drive distance-fade via URP's built-in Exponential Squared fog
with fog color locked to Uma's horizon stop (`#DCE8E4`). Back the whole stack with a
3-stop gradient skybox shader authored in Shader Graph. This is the lowest-seam,
lowest-complexity, most art-controllable route for a single desktop hero scene and
fully preserves the faceted low-poly aesthetic.

## Evidence

### 1. URP Fog — modes and behavior

**Source:** Catlike Coding, "Rendering 14, Fog" — Unity rendering tutorial series,
independent author (Jasper Flick), 2018 / continuously cited.
URL: https://catlikecoding.com/unity/tutorials/rendering/part-14/
**Strength: Strong** (canonical independent deep-dive; code matches Unity source).

- URP exposes three fog modes: **Linear** (configurable start/end distance, easy to art-direct),
  **Exponential** (`2^(−cd)`, smoother, never fully zero), **Exponential Squared**
  (`2^(−(cd)²)`, clear near the camera, accelerates density at distance).
- **Exponential Squared** is the standard choice for vista atmospherics: keeps the
  immediate playspace crisp, then rolls distance into a dense haze band — exactly the
  "dissolves into sky" look.
- Fog DOES NOT apply to the skybox object by default — so fog color and skybox
  horizon color must be set to the same value to avoid a seam. This is a structural
  URP constraint, not a bug.

**Source:** Unity Discussions community thread, "What's the best way to deal with fog and
horizon in URP?", Unity Forums, 2021–2023.
URL: https://discussions.unity.com/t/whats-the-best-way-to-deal-with-fog-and-horizon-in-urp/780400
**Strength: Moderate** (community consensus across multiple practitioners; no single
maintainer post, but repeated convergence on same approach).

- Practitioner consensus: the easiest seam fix is to set fog color == skybox
  horizon color, then tune the skybox horizon exponent so the gradient exactly
  covers the fog band. No custom shader required for this route.
- More advanced route (for perfect blending): a fullscreen post-process pass that
  samples the skybox cubemap and lerps into it — adds a second render pass,
  only worth it if the color-match approach fails QA.

### 2. 3-stop gradient skybox in Shader Graph

**Source:** Tim Coster, "Unity ShaderGraph Procedural Skybox Tutorial," CosterGraphics,
2019, still URP-valid.
URL: https://timcoster.com/2019/09/03/unity-shadergraph-skybox-quick-tutorial/
**Strength: Moderate** (practitioner tutorial; well-reproduced technique; predates
Shader Graph 14 but the node graph is stable across versions).

- Uses the world-position Y (green channel, −1 to 1) remapped to 0–1 as the blend
  driver, exposing three HDR color properties: sky, horizon, ground.
- Two Exponent parameters control how fast the sky-to-horizon and horizon-to-ground
  transitions occur — this is the direct knob for setting horizon bandwidth.
- To match Uma's 3-stop gradient (`#7FB4D6` → `#AAD0E2` → horizon `#DCE8E4`):
  set Sky = `#7FB4D6`, Horizon = `#AAD0E2`, and then set URP Fog color = `#DCE8E4`
  (so the fog and the bottom of the skybox are the same pale warm teal).
  The "ground" color in the shader sits below the actual horizon geometry and is
  never seen — only the sky-to-horizon band matters.

**Source:** Jannik Boysen, "Creating a procedural skybox in Unity Shader Graph,"
Medium, 2021.
URL: https://medium.com/@jannik_boysen/procedural-skybox-shader-137f6b0cb77c
**Strength: Moderate** (practitioner; confirms the technique independently; adds the
tip to use a Power node with value ~25 on the V-component to control edge softness
and fog merge).

### 3. Faceted distant-mesh silhouette rings

**Source:** Indie devlog, "Devlog 9: Shader recipe for low-poly mountains in Unity,"
itch.io, 2023.
URL: https://itch.io/devlog/696127/devlog-9-shader-recipe-for-low-poly-mountains-in-unity.amp
**Strength: Moderate** (single developer; reproducible recipe; GPU-based DDX/DDY
per-face normal approach matches the project's own flat-shaded patterns from
`unity-conventions.md` Low-poly mesh patterns section).

- GPU normal reconstruction via DDX/DDY gives the flat-shaded faceted appearance
  WITHOUT unindexed vertex duplication — this is the same technique as the project's
  existing terrain. Distant rings can reuse the same material.
- Height-based vertex-color tinting (centroid-encoded per face) lets rings carry a
  snow-cap tint at peak vertices and a green/rock mid-band without a texture — zero
  additional texture fetches, consistent with the chunky art board.

**Source:** Keijiro Takayama, "UnitySkyboxShaders — Custom skybox shaders," GitHub,
maintained.
URL: https://github.com/keijiro/UnitySkyboxShaders
**Strength: Strong** (Keijiro is a Unity Technologies Developer Advocate; these shaders
are widely used reference implementations).

- Confirms the 3-color (top, horizon, bottom) skybox shader pattern as idiomatic.
  These shaders are BIRP; the Shader Graph port from Coster follows the same
  math. No URP incompatibility.

### 4. Far-plane billboard / impostor layer

**Source:** Simplygon, "Rendering vegetation impostors," Simplygon.com, 2022.
URL: https://simplygon.com/posts/4bf1787d-6d76-48a7-9111-787d6985005c
**Source:** Amplify Impostors Manual, Amplify Creations Wiki, maintained.
URL: https://wiki.amplify.pt/index.php?title=Unity_Products%3AAmplify_Impostors%2FManual
**Strength: Strong** (vendor documentation; URP support explicitly confirmed in both).

- Impostors are baked multi-angle billboard cards placed at the far distance. They
  render cheaply and cut draw calls dramatically for large distant objects.
- However, for FAR mountain RINGS (not individual trees), the bake step is
  non-trivial: you need a capture rig for each azimuth slice, and a mountain ring is
  not a compact object. The technique is well-suited to INDIVIDUAL mountain PEAKS
  at 2 000+ u but adds tooling complexity upfront.
- At human-scale orbit camera (player typically 30–100 u from the character,
  camera pitched 30–55°), distant mountain rings are already a tiny fraction of
  screen pixels; their raw-mesh draw cost is low even at LOD0.

### 5. Height-fog only (no distant geometry)

**Source:** Better Fog (staggart.xyz), SC Post Effects Fog documentation, 2025.
URL: https://staggart.xyz/unity/sc-post-effects/scpe-docs/?section=fog
**Strength: Moderate** (shipped asset, URP-confirmed, widely used).

- A post-process height fog can paint a convincing haze band without any geometry
  at the horizon. Color can sample the skybox.
- Limitation: pure fog with no silhouette reads as EMPTY sky at the horizon, not
  as a BIG world with distance. The silhouette contrast of mountain ridgelines is the
  primary perceptual cue for "world feels large" — the depth layer needs BOTH
  silhouette geometry AND atmosphere, not atmosphere alone.

### 6. Valheim — "Lo-Fi HD" as a comparable title

**Source:** Chris Kaleiki, "Why is Valheim so popular?" Theorycraft Substack, 2021.
URL: https://theorycraft.substack.com/p/why-is-valheim-so-popular
**Strength: Weak** (editorial analysis, not a technical breakdown; engine is custom, not Unity).

- Valheim uses heavy fog even in open biomes deliberately to shorten view distance
  and ADD mystery / scale, not to compensate for missing geometry. The fog is an
  explicit artistic choice.
- The "beautiful skybox that appears almost like a painting when you look at things at
  a distance" observation supports the technique: fog + a painted distant silhouette
  reads as big, not as clipped.

### 7. Unity URP performance (draw distance, desktop)

**Source:** Unity Technologies, "Configure for better performance in URP," Unity 6
Manual, 2024.
URL: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/configure-for-better-performance.html
**Strength: Strong** (official docs, Unity 6 era).

- On desktop, Forward rendering + SRP Batcher is the baseline. No performance
  barrier to placing 2–3 static mesh rings at 500–1 500 u for a single scene.
- Static batching and GPU instancing are both available for URP; a ring authored as
  separate faceted sections can batch.
- Far clip plane: no documented URP limit. Setting camera far clip to 2 000 u covers
  the ring depth stack with room to spare; URP frustum culling will cull ring
  sections behind the player.

## Application to Far Horizon

### The 4-layer depth stack Drew should build

```
Layer 0 — Gameplay terrain (existing Zone-D geo, 0–200 u)
Layer 1 — Near silhouette ring  (~500 u, faceted mountains, ~400 tris)
Layer 2 — Far silhouette ring   (~1 000 u, simpler silhouette, ~150 tris)
Layer 3 — Gradient skybox (3-stop Shader Graph, infinite)
           + Exponential Squared fog (fog color == horizon stop)
```

The rings sit on a cylinder at constant world-Y, scaled so their peaks crest above
the horizon line of the skybox gradient (above the fog's dense band). Coloring:
Layer 1 darker, more saturated (closer read), Layer 2 tinted toward fog color
(`#DCE8E4`), making it read as further.

### Sky-tint mapping (Uma's 3 stops)

| Stop     | Hex       | Role in implementation                          |
|----------|-----------|-------------------------------------------------|
| Sky      | `#7FB4D6` | Skybox Sky color property                       |
| Mid      | `#AAD0E2` | Skybox Horizon color property (upper blend)     |
| Horizon  | `#DCE8E4` | **Fog color** AND bottom of the skybox gradient |

Setting fog color == `#DCE8E4` is the seam-kill: the skybox gradient fades to
`#DCE8E4` at the horizon, and all distant geometry fades to `#DCE8E4` via the fog —
they meet at the same color. No visible seam, no post-process pass required.

### "Does it FEEL big" — the key design principle

The perceptual cue research (parallax, atmospheric perspective) is unambiguous: the
big-world read comes from CONTRAST between a large silhouette mass and a wide open
sky — NOT from fog alone. The layered ring approach makes the "far horizon" a
literal destination visible from frame 1 of gameplay. Fog alone gives "misty void";
silhouette rings give "there is a whole world out there."

### Perf budget

- 2 rings of chunky low-poly faceted meshes (400 + 150 tris) are negligible on
  any desktop GPU. Static batching collapses them to 1–2 draw calls.
- Camera far clip at 2 000 u covers the full stack. Frustum culling handles player
  rotation.
- No impostor bake, no capture rig, no additional render passes required for this
  route. Total new tooling cost: 1 Blender script for the ring geometry, 1 Shader
  Graph skybox material.

### What NOT to do

- **Pure billboard/impostor layer for the rings**: over-engineered for this scale.
  Reserve impostors for individual distant-tree detail if the scene needs M-U5+
  foreground density at range. For the horizon band, raw static mesh is cheaper
  to author and art-direct.
- **HDRP Gradient Sky override**: HDRP's built-in `Gradient Sky` volume override
  is purpose-built for this, but the project is URP-locked (URP is the confirmed
  pipeline). Do not switch pipelines for this feature.
- **Volumetric fog** (Buto / ray-march passes): adds a second render pass and
  cost that a stylized desktop title doesn't need. Reserve for a future M-U5+ pass
  if the Sponsor requests light shafts / God rays.

### Drew's implementation sketch (no code)

1. **Skybox material (Shader Graph, URP Skybox target):** 3 color properties (Sky /
   Horizon / Ground, all HDR). Blend driver = world-position Y remapped to 0–1.
   Two Exponent parameters control how tight the gradient bands are (start with
   Exp1 = 4, Exp2 = 2; tune to taste in editor). Export as `SkyboxGradient.mat`.

2. **Mountain ring meshes (Blender):** One script generates two concentric faceted
   mountain-ridge silhouettes (a randomized peak-and-valley polygon extruded
   downward). Export as `MountainRingNear.fbx` and `MountainRingFar.fbx`. Import
   with Normals = Calculate, Smoothing Angle ~30° (keeps facets crisp). Mark as
   Static.

3. **Materials:** Near ring = URP/Lit with base color in the project's mid-grey/blue-
   green palette (faceted silhouette, no texture). Far ring = same material but
   tinted toward `#DCE8E4` (use the project's palette system, or a dedicated mat
   slot). Both can use the flat-shaded vertex-color approach from the existing
   terrain shader if vertex-color tinting is already in the pipeline.

4. **Fog (Window > Rendering > Lighting > Environment):** Mode = Exponential
   Squared, Color = `#DCE8E4`, Density = tune until the far ring (1 000 u) reads
   as faintly misty but the near ring (500 u) still has clear silhouette contrast.
   Start at Density 0.0003 and walk up.

5. **LOD Group (optional):** Attach a LOD Group to each ring with LOD0 = full mesh,
   Culled at 120% screen height (i.e., never culled — ring always in frame from
   any orbit position). This future-proofs it for M-U5+ if camera range expands.

6. **Skybox assignment:** Assign `SkyboxGradient.mat` to the scene's Skybox
   Material slot (Lighting window). In Camera settings, ensure the clear flag is
   Skybox (default). No per-camera clear-color override needed.

7. **Seam verification:** Spin the orbit camera to the horizon line and visually
   confirm: the mountain rings fade to `#DCE8E4`, the skybox gradient base is
   `#DCE8E4`. If a seam is visible, narrow the Skybox Exponent2 parameter (widen
   the horizon gradient band) until it swallows the seam.

## Route ranking

| Rank | Route                              | Seam risk | Art control | Complexity | Low-poly faithful |
|------|-------------------------------------|-----------|-------------|------------|-------------------|
| 1    | Geo rings + Exp² fog + gradient sky | Low       | High        | Low        | Yes               |
| 2    | Geo rings + height fog post-pass    | Very low  | Medium      | Medium     | Yes               |
| 3    | Billboard / impostor layer          | Low       | Low         | High       | Partial (baked)   |
| 4    | Fog only (no geo)                   | None      | Low         | Very low   | N/A               |

**Route 1 is the recommendation.** Route 2 is the upgrade path if Route 1 produces a
visible seam under any skybox color iteration. Routes 3 and 4 are rejected for this
phase.

## Evidence-strength summary

- Route geometry concept: **Moderate** (practitioner devlogs + comparable titles)
- URP fog behavior and skybox non-application: **Strong** (official docs + Catlike Coding)
- 3-color gradient skybox Shader Graph: **Moderate** (multiple independent practitioners)
- Sky-match seam-kill technique: **Moderate** (community consensus; no single official doc)
- Desktop perf budget: **Strong** (official URP configure-for-perf doc)
- "Big world feel" perceptual basis: **Weak** (editorial analysis; no quantified study)
