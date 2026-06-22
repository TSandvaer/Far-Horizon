# World-Resource Prop Quality — Rocks, Bushes/Berries, Campfire, Crafting Table

## Question

The M-U2 gameplay wave (tickets 86caa4c5c / 86caa4c96 / 86caa5zz3) requires a set of in-world
resource props the player interacts with: scattered stones to pick up, berry bushes to forage,
a campfire to build (warmth need), and a crafting-table stump (cut-log surface, M-U2 scope).
What are the concrete "build it in our routes" recipes for each prop — faceting idiom, silhouette
recipe, vertex-color / AO baking approach, seeded variation, and winding/normal traps to avoid
by construction?

**SCOPE NOTE:** Trees (86cabc73q), grass (86cabc737), and sky (86cabc743) each have dedicated
deep-dive tickets. This note covers ONLY the wave's resource props: rocks/stones, bushes/berries,
campfire geometry, crafting-table stump. Cross-refs the tree ticket where shapes overlap.

---

## Bottom line

Every wave prop fits the codebase's two-idiom palette already in production:

- **FLAT-SHADED + vertex-color value** (rocks, campfire log stack, crafting stump): per-face normals
  forced outward, vertex-color value baked per face (light tops / mid sides / dark undersides). The
  `FacetedRock` builder in `LowPolyMeshes.cs` is the validated template; new props reuse its flat-shade
  + outward-winding pattern verbatim.
- **WELDED + smooth normals + vertex-color tint** (bush foliage blobs, campfire flame cone): the
  `BlobCanopy` / `Cone` idiom already in `LowPolyMeshes.cs`. Bushes ARE small blob trees; use
  `BlobCanopy` with a smaller radius, more saturated greens, and berry-dot sub-objects.

New `LowPolyMeshes` methods needed: `FacetedLog` (cylinders, flat-shaded, seeded tilt) and
`BerryBush` wrapper (reuses `BlobCanopy` + scatter of small `FacetedSphere` berry nubs). New
`LowPolyZoneGen` methods: `BuildBush`, `BuildCampfire`, `BuildCraftingStump`. The `Cone` mesh
(already present) is the campfire flame; `TaperedCylinder` is the log trunk — both need only
callers, not new mesh code.

---

## Evidence

### A — Flat-shading idiom: per-face normals are the entire "reads as carved stone/wood" signal

- **Source:** Game Developer — "How to Make Low-Poly Look Good"
  [https://www.gamedeveloper.com/design/how-to-make-low-poly-look-good] — **Strong** (industry
  reference, widely cited). Rule: disable smoothed normals ("no shared vertices"). Each face
  catches the directional light at its own N·L value — that per-face value step is what reads as
  faceted stone or chopped log, not a smooth mound. Silhouette is the primary quality gate: "the
  shape must read clearly from all angles."

- **Source:** Hextant Studios — "Rendering Flat-Shaded / Low-Poly Style Models in Unity"
  [https://hextantstudios.com/unity-flat-low-poly-shader/] — **Strong** (Unity-specific, covers
  both the dFdx/dFdy fragment-shader approach AND the explicit-per-face-normal approach). Confirms
  the URP-safe route is explicit per-face normals set with `mesh.SetNormals()` before assigning
  triangles — not the dFdx runtime approach (which requires a custom shader pass and introduces
  SSAO artifacts on non-coplanar quads). Unity SSAO artifacts are the key pitfall to avoid.

- **Source:** `LowPolyMeshes.FacetedRock` (this repo, `Assets/Scripts/Editor/LowPolyMeshes.cs`
  lines 291–434) — **Strong** (production-validated, passed two soak rounds). The exact idiom:
  emit every triangle with its own 3 verts + the face normal, force outward winding, bake
  per-face value into vertex color (`val = Lerp(0.80f, 1.0f, up)` where `up = fn.y*0.5+0.5`).
  `mesh.SetNormals(normals)` is called explicitly — `RecalculateNormals` is NOT called after
  `SetNormals` (that would re-smooth). Same idiom is used in `FacetedMountain`, `CloudBlob`, and
  `FacetedLandmass` — it is the project's flat-shade standard.

- **The outward-winding trap (confirmed on this project, lines 395–405).** After anisotropic
  displacement, any face can wind CW-as-seen-from-outside; its computed normal points inward;
  URP's default `Cull Back` culls it. Fix: `if (Vector3.Dot(fn, faceCentre) < 0) { fn = -fn;
  swap(v1, v2); }` before emitting. Every flat-shaded prop must include this guard.

### B — Seeded variation: per-prop geometry diversity without material churn

- **Source:** RockStudio procedural generator (Unity Discussions,
  [https://discussions.unity.com/t/rockstudio-2-3-a-low-poly-procedural-rock-generator/663157]) —
  **Moderate** (community showcase, not official). Confirms the industry pattern: a seed controls
  the RNG for all axis-scale and radial-jitter values, making each instance unique but deterministic.
  The critical note: "setting the same seed produces identical instances" — reproducibility is the
  hard requirement for our binary-scene regenerate-on-rebase workflow.

- **Source:** `LowPolyZoneGen.BuildRock` (this repo, lines 676–698) — **Strong** (production
  code). The established pattern: `FacetedRock(0.55f, jitter:0.38f, seed: rnd.Next())` — a new
  seed per rock instance from the stream of a single `System.Random` seeded from the zone seed.
  Rotation adds a random yaw (0–360°) and a small tilt (±10°) so the rock doesn't sit axis-
  aligned. Tint color is given a ±4% luminance jitter per instance so adjacent rocks don't
  clamp to identical albedo.

### C — Bush / foliage blobs: reuse `BlobCanopy`, not a new mesh idiom

- **Source:** `LowPolyMeshes.BlobCanopy` (this repo, lines 454–505) — **Strong** (production-
  validated, passes scene tests). A cluster of 3–6 overlapping faceted spheroids, welded within
  each blob, with per-blob vertex-color green baked by height (`shadowGreen → bodyGreen →
  topGreen`). `RecalculateNormals` on a welded multi-blob mesh → smooth shading on the
  individual blob faces → reads as a soft foliage lump. This is already the tree-canopy idiom;
  a bush is the SAME construction at a smaller radius.

- **Source:** Art direction board (this repo, `art-direction.md` §Nature family, ground-detail
  pair `21h22_33`) — **Strong** (Sponsor-set ground truth). "Tall pines, RICH layered ground
  cover — mushrooms, stumps, logs, rocks — along a worn dirt trail." The bush silhouette from
  the board (21h10_44, bottom-left corner) is exactly a compact spheroid cluster — 2–3 overlapping
  blobs on a short stump-like trunk, slightly asymmetric. No individual leaves, no card quads.

- **Berry dots:** The inspiration shows small round coloured nubs on the bush surface (berry read
  is 100% silhouette — a few protruding dot-spheres on the bush surface, scaled ~0.08–0.12u).
  Each berry is a `FacetedSphere(radius:0.08, subdiv:0, jitter:0.05, seed)` — the subdiv-0
  octahedron (8 faces) reads as a faceted dot at this scale. Welded + `RecalculateNormals`.
  Berry colour: warm red-orange (`Color(0.72f, 0.22f, 0.12f)` is the board's berry read).
  Berry positions: scatter 4–8 on the outer surface of the uppermost blob (bias toward the
  top-facing hemisphere so they're visible from the gameplay camera above).

- **Thin-foliage normal trap (confirmed, must avoid).** `unity-conventions.md` §"Thin double-
  sided foliage": card-quad bushes built with opposite-wound front/back faces on SHARED verts
  produce zero-averaged normals after `RecalculateNormals` → dark shards. Bushes as SOLID BLOB
  VOLUMES sidestep this entirely — the blobs are closed spheroids, never thin cards. Same rule
  that exempts `BlobCanopy` from the dark-shard trap. Do not use quad-card approaches.

### D — Campfire geometry: existing `Cone` + `TaperedCylinder` are sufficient

- **Source:** `LowPolyMeshes.Cone` (this repo, lines 1043–1074) — **Strong** (production code,
  carries a doc comment explicitly noting "U2-4 campfire flame"). A warm low-poly tongue of fire:
  `Cone(baseR:0.22f, height:0.75f, sides:6)` welded + `RecalculateNormals`. Tint via
  `MakeFlatColorMat` with the fire-orange palette.

- **Source:** `LowPolyMeshes.TaperedCylinder` (this repo, lines 23–57) — **Strong** (production
  code, used for tree trunks). Logs in the campfire pile are tapered cylinders at a very small
  scale: `TaperedCylinder(botR:0.08f, topR:0.07f, height:0.35f, sides:6)`, placed in a crossed
  pile (2–3 logs, each given a seeded Y-rotation of 0°/60°/120° and a slight tilt) under the
  flame cone. The crossed-log silhouette is the primary campfire read at orbit distance.

- **Source:** Game Developer "How to Make Low-Poly Look Good" (Evidence A above) — **Strong**.
  Confirms: "allocate polygons proportionally to object size and importance." A campfire at 5u
  scale gets 6-sided primitives; the flame gets a Cone, not a high-res mesh. Both are already
  the right polygon budget.

- **Flame vs particle trade-off note.** Several Unity tutorials (Medium: "Stylized Low-Poly Fire
  in Unity URP + Particle System",
  [https://medium.com/@adamy1558/how-to-create-a-stylized-low-poly-fire-in-unity-urp-particle-system-77dfcceb0200])
  recommend particle-system fire for the M-U2 campfire interaction (color-over-lifetime:
  transparent yellow → solid red → transparent black). Particles produce the liveness the Sponsor
  prefers. The geometry `Cone` is the STATIC base (always visible, zero particle cost); an
  URP Particles shader cone atop it adds the animated flame lick. This hybrid is the
  recommended approach: static geometry for the prop silhouette, optional particle on top for
  warmth-need-active feedback. **M-U2 scope (per CLAUDE.md):** the campfire must read as a campfire
  and satisfy the warmth-need loop; animated particle flame is an enhancement, not a hard M-U2
  requirement.

### E — Crafting-table stump: flat-shaded cylinder cluster, `CraftStumpMat` already stubbed

- **Source:** `Assets/Settings/CraftStumpMat.mat` (this repo, confirmed by grep) — **Strong**
  (the orchestrator already created the material asset stub). The stump is a cut-log surface:
  a flat-topped `TaperedCylinder` (slightly tapered, 8 sides, height 0.6u, radius 0.35u) as
  the stump body, flat-shaded. The cut top face reads as a chopped log surface — add a
  concentric ring of vertex-color value gradient (lighter outer ring = bark, slightly darker
  centre = exposed wood grain). The `FacetedRock`-style flat-shading idiom (Evidence A) gives
  the wood-grain read without a texture.

- **Source:** Art direction board (this repo, `art-direction.md` §`21h12_49` — Blender nature
  kit) — **Strong** (Sponsor-set). The kit shows logs, stumps, and rocks in the same prop vocabulary:
  chunky, short, faceted. The stump is roughly cylinder-proportioned with a slightly irregular
  top. No need for a complex mesh — a 8-sided flat-topped cylinder at scale ~(0.35, 0.6, 0.35)u
  reads correctly.

- **M-U2 OOS note:** The crafting table itself (the placed interactive surface) is M-U3+ per
  CLAUDE.md. M-U2 scope needs ONLY the stump geometry as a world prop (the axe-chop destination
  / campfire assembly surface). The interactive crafting system is out of scope for this research.

### F — GPU Resident Drawer compatibility (all new props)

- **Source:** `unity6-mastery.md` §2 (this repo) — **Strong** (project mandatory pre-read).
  GPU Resident Drawer disqualifiers: `MaterialPropertyBlocks`, `sortingLayerID`, per-GO >128
  materials, `OnWillRenderObject`. Keep all props as plain `MeshRenderer + MeshFilter` without
  `MaterialPropertyBlocks`. Use shared material instances (same shader, different `_Tint`)
  via `MakeFlatColorMat` / `RockVertexColorMat` pattern — the SRP-Batcher groups them by shader
  variant; GPU Resident Drawer instances them. The `BuildRock` pattern (per-instance tint jitter
  via a new material per rock, quantized to reduce variants) is the established approach.

  The lighting rule: campfire glow MUST use an unshadowed point light or baked/emissive — a
  shadowed point light costs 6 shadow-map passes per frame. A small unshadowed orange point light
  radius 5u for the warmth-active feedback is the correct approach.

### G — Vertex-color AO baking (dirt-contact darkening, ambient occlusion substitute)

- **Source:** `LowPolyZoneGen.BuildIslandTerrainMesh` / `IslandColorAt` (this repo) — **Strong**
  (production code, confirmed by grep). The terrain already bakes a foam/sand-damp contact-
  darkening into vertex color. The same principle applies to props: the underside faces of the
  rock, the base ring of the bush stump, and the log-bottom faces of the campfire can be given
  a slightly darker vertex-color value (multiply by 0.70–0.80) to read as ground-contact shadow.
  This is BAKED AT BOOTSTRAP TIME (deterministic, no runtime cost), not a screen-space AO pass.

- **Source:** ameye.dev "Stylized Water Shader" §vertex-color depth gradient (cited in
  `water-shader-research.md` §Evidence C) — **Moderate**. The principle extends: any surface
  where depth/occlusion varies naturally (top-lit vs ground-shadowed) benefits from a baked
  vertex-color value ramp rather than relying purely on real-time lighting.

---

## Application to Far Horizon — Adopt-in-Our-Code Plan

### What exists (do not regress)

| Element | File | Status |
|---|---|---|
| `FacetedRock` mesh (flat-shaded, vertex-color value) | `LowPolyMeshes.cs` lines 291–434 | Production-validated; ROCKS/STONES prop is fully covered |
| `BuildRock` + scatter in `ScatterIslandProps` | `LowPolyZoneGen.cs` lines 676–698, 580–597 | Production; ~60 rocks scattered on the big island |
| `RockBoulderSceneTests` (percept guards) | `Assets/Tests/EditMode/RockBoulderSceneTests.cs` | 6 guards; catches regression to smooth mound |
| `BlobCanopy` mesh (welded, vertex-color greens) | `LowPolyMeshes.cs` lines 454–505 | Production; BUSH foliage reuses this |
| `GrassClump` mesh (up-biased normals, two-sided) | `LowPolyMeshes.cs` lines 988–1041 | Production; GRASS (separate ticket 86cabc737) |
| `TaperedCylinder` mesh (welded, smooth) | `LowPolyMeshes.cs` lines 23–57 | Production; campfire logs + stump body |
| `Cone` mesh (welded, smooth) | `LowPolyMeshes.cs` lines 1043–1074 | Production; campfire flame cone |
| `CraftStumpMat.mat` | `Assets/Settings/CraftStumpMat.mat` | Stub asset; geometry + binding needed |
| `MakeMeshObject` / `MakeFlatColorMat` / `RockVertexColorMat` | `LowPolyZoneGen.cs` lines 718–727, 882–950 | Production helpers; all new props use these |

### New code needed (net-new surface)

**1. `LowPolyMeshes.FacetedLog(float radius, float height, float jitter, int seed)`**

Flat-shaded cylinder: 8 sides, per-face outward-winding guard, vertex-color value ramp (side
faces mid-warm-brown, end caps slightly lighter to read as cut wood grain). Parametric so logs
in a campfire pile can vary in length (`height` 0.25–0.45u) and radius (0.06–0.10u). Seeded.
Used by `BuildCampfire` for the crossed-log pile under the flame.

The end-cap (top/bottom circle) is a fan of flat-shaded triangles from a center vert; each
triangle gets the same face normal (+Y or -Y) and a slightly lighter value to read as cut wood.

**2. `LowPolyZoneGen.BuildBush(GameObject parent, Vector3 at, float scale, System.Random rnd)`**

Mirrors `BuildTree` structure: a short stump (`TaperedCylinder` height 0.25u, radius 0.12u,
`TrunkCol`) + a compact `BlobCanopy` (radius 0.65–0.90u, 3–4 blobs, `BushBodyGreen`,
`BushTopGreen`, `BushShadowGreen`). Berry sub-objects: 4–8 `FacetedSphere(0.08f, subdiv:0,
jitter:0.05f, seed)` scattered on the outer upper hemisphere of the canopy GO, each rotated
random Y, tinted `BerryOrange = Color(0.72f, 0.22f, 0.12f)`. No new mesh method required —
only `BlobCanopy` + existing primitives.

Palette anchors (new constants in `LowPolyZoneGen`):
```csharp
// PATTERN — not production code
public static readonly Color BushBodyGreen   = new Color(0.24f, 0.55f, 0.20f); // mid saturated leaf
public static readonly Color BushTopGreen    = new Color(0.34f, 0.70f, 0.26f); // top-lit bright
public static readonly Color BushShadowGreen = new Color(0.14f, 0.38f, 0.13f); // under-canopy shadow
public static readonly Color BerryOrange     = new Color(0.72f, 0.22f, 0.12f); // warm red-orange
```

Seeded variation: `BlobCanopy(canopyR, blobCount, ...)` seed from `rnd.Next()`. Berry count and
scatter positions from the same `rnd` stream. Scale jitter: `scale = 0.7f + rnd.NextDouble()*0.6f`
at the call site.

**3. `LowPolyZoneGen.BuildCampfire(GameObject parent, Vector3 at, System.Random rnd)`**

Structure:
- **Log pile:** 3 `FacetedLog` instances, each Y-rotated 0°/55°/110° and tilted ±8° for a natural
  crossed-pile read. Tinted `LogBrown = Color(0.40f, 0.26f, 0.14f)`.
- **Flame:** `Cone(baseR:0.22f, height:0.75f, sides:6)` placed at the pile centre, Y=0 (cone base
  flush with the log top). Tinted warm orange-yellow `Color(0.95f, 0.55f, 0.10f)`. A second
  smaller cone (`baseR:0.13f, height:0.55f, sides:5`) offset slightly in X for asymmetry.
- **Glow light (warmth-active):** an unshadowed orange `Light` (type `Point`, range 5u, intensity
  1.5, colour `Color(1.0f, 0.45f, 0.05f)`) — enabled/disabled by the warmth-need system at
  runtime. NOT a shadow-casting light (6-pass cost, unity6-mastery §3).
- **Interactive trigger:** a `SphereCollider` (radius 1.5u, `isTrigger:true`) for the warmth-need
  proximity detection. Devon/Drew own the script binding; this covers only the geometry/light.

**4. `LowPolyZoneGen.BuildCraftingStump(GameObject parent, Vector3 at, System.Random rnd)`**

A `TaperedCylinder(botR:0.35f, topR:0.32f, height:0.55f, sides:8)` — slightly tapered for
a natural stump look, 8 sides for readable facets at this scale. Flat-shading is not strictly
needed (the cylinder is welded + smooth-normals in the existing idiom), but a slightly rougher
normal approach (sides:8 = visibly faceted sides) gives the chopped-log read. Tinted
`LogBrown`. The existing `CraftStumpMat.mat` asset stub should be wired to the
`LowPolyVertexColor` shader with `_Tint = LogBrown`.

The cut-top ring: bake a light-value (0.90f) to the top-cap vert colours and a slightly darker
outer ring (0.75f) so the cut surface reads with depth. This requires the existing cylinder's
top-cap fan to carry vertex colors — a small extension to `TaperedCylinder` or a post-process
in `BuildCraftingStump` calling `mesh.SetColors()`.

### Seeded variation summary

All four props draw from the same `System.Random(seed + offset)` stream as the rest of
`ScatterIslandProps`. The offsets (e.g. `seed + 556` for bushes, `seed + 557` for campfire)
keep streams independent. The campfire is PLACED at a fixed authored position (the survival-
loop centre), not scattered — its seed drives only the log-pile tilt randomness.

### Scatter integration for M-U2 bushes

Add a `ScatterBushes` block to `ScatterIslandProps` (or a new `ScatterSurvivalProps` for all
M-U2 interactive props):
- Bush target: 15–25 bushes, biased toward the mid-ring (not the coastal fringe, not the
  spawn clear) so the player finds them naturally. Inland-density bias: same
  `Mathf.InverseLerp(plantOuterR, 0f, rr)` pattern as grass and trees.
- The campfire and crafting stump are authored at a fixed position (close to spawn but not
  in the `spawnClearR` exclusion zone) — they are NOT scattered.

### EditMode tests to add

Following the `RockBoulderSceneTests` pattern, add `BushSceneTests`:
- Bush foliage blobs exist and carry vertex colors (no regression to plain grey GO).
- Berry sub-objects are present and tinted warm-red (not white/default).
- Bush count in scene is within a sensible range (15–30).
- Campfire has a log-pile child and a flame cone child (scene-presence guard).
- CraftingStump GO exists in the scene (scene-presence guard).

### Ranked priority (M-U2 readiness)

**Rank 1 — `BuildBush` + berry sub-objects. Effort: 3–4h.**
The hunger-need loop (berries) is a core M-U2 AC (ticket 86caa5zz3). Bush geometry is the
player-facing interaction point. Reuses `BlobCanopy` + existing primitives — no new mesh code.

**Rank 2 — `BuildCampfire` (geometry). Effort: 2–3h.**
The warmth-need campfire is a core M-U2 AC (ticket 86caa4c5c). `Cone` + `TaperedCylinder`
already exist; `BuildCampfire` is a caller + the glow-light setup. The interactive warmth
script is Devon/Drew's surface; this is pure geometry/scene-composition.

**Rank 3 — `BuildCraftingStump`. Effort: 1–2h.**
Required for the crafting interaction (86caa4c5c). Low effort — `TaperedCylinder` + `CraftStumpMat.mat`
wiring. Can land in the same PR as `BuildCampfire`.

**Rank 4 — `FacetedLog` (new mesh). Effort: 1–2h.**
Only needed for the campfire log-pile. If time-constrained, use `TaperedCylinder` directly for
logs in `BuildCampfire` (already present, good-enough silhouette at campfire scale). `FacetedLog`
is a quality upgrade (flat-shaded chopped-wood look vs smooth cylinder look) — addressable as a
follow-up once the gameplay loop is green.

---

## What NOT to do

- **Do NOT use card-quad bushes** (thin double-sided quads as leaves). The up-biased-normal hack
  from `GrassClump` applies only to flat blades; a bush-scale card would still read as a dark
  shard from side angles. Solid blob volumes are the correct idiom (Evidence C).

- **Do NOT use `RecalculateNormals` on flat-shaded props** (rocks, logs, stump). That re-smooths
  the face normals into a gradient, destroying the stone/wood read (the smooth-mound regression
  documented in `RockBoulderSceneTests.cs`). Call `mesh.SetNormals(normals)` explicitly and skip
  `RecalculateNormals`.

- **Do NOT shadow-cast the campfire glow light.** Point light with shadows = 6 shadow-map passes.
  An unshadowed point light gives the warmth glow correctly at zero extra shadow cost.

- **Do NOT proliferate unique shaders** for berry colour / log colour / bush tint — use material
  instances of `LowPolyVertexColor` with different `_Tint` values. The SRP-Batcher groups by
  shader variant; multiple material instances of one shader are cheap.

- **Do NOT use `FacetedSphere(subdiv:0)` at boulder scale** (the rejected bare-octahedron spike,
  86ca8m5zu v1). `FacetedSphere(subdiv:0)` is acceptable for berry dots at 0.08u (the 8-face
  octahedron reads as a faceted dot, not a spike at that scale). Use `FacetedRock` for any prop
  ≥ 0.3u radius.

---

## Queued follow-up (flagged, not in this slice)

- **Trees** (86cabc73q) — `BlobCanopy` / `TaperedCylinder` are the same building blocks; the tree
  ticket covers canopy variety, pine variants, and the tree-specific scatter tuning.
- **Grass** (86cabc737) — `GrassClump` exists; the grass ticket covers ground-cover density,
  blade palette, and LOD.
- **Sky** (86cabc743) — `CloudBlob` / gradient skybox; separate ticket.
- **Snake (character mesh)** — separate character surface, not a world-prop concern.
