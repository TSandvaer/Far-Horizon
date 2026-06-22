# Low-Poly Trees — Quality and Diversity

## Question

Current trees are samey (one blob-canopy type, uniform scale range) and the scatter reads
"not really random." The Sponsor wants better tree QUALITY (visual richness, the board's
multi-green faceted-lump look) and DIVERSITY (height, canopy shape, lean, color tint).
What are the evidence-graded "build it in our routes" recipes for both, using the
existing `LowPolyMeshes` / `LowPolyZoneGen` codebase?

---

## Bottom line

Three changes, in impact order:

1. **Add a pine / cone-tier tree type alongside the existing blob tree.** The board shows
   both types side-by-side in every wide shot (21h12_49, 21h21_30, 21h22_33). A second
   archetype breaks the silhouette monotony more than any amount of scale jitter. Medium
   effort (~3–4h): a new `LowPolyMeshes.ConeTierCanopy` mesh + one new branch in `BuildTree`.

2. **Strengthen per-tree vertex-color contrast** inside the existing `BlobCanopy`. The board
   (21h11_03) shows dark shadow-blobs, mid-body green, and top-lit faces clearly distinct.
   The current `CanopyShadow/Body/Top` range (0.18/0.30/0.48 green) is correct but the
   per-blob `heightK` blending compresses the shadow end. Widen the shadow range and add a
   small per-blob FLAT canopy face that gets a brighter top-lit vertex-color value — the
   "white-edge-highlight" idiom visible on 21h11_03's top polygon. Low effort (~1h patch).

3. **Replace the uniform-area disc scatter with seeded Poisson-disk placement.** The current
   `r = R*sqrt(u)` uniform sample produces micro-clusters (several trees within touching
   distance of each other) and locally empty patches — both artefacts the Sponsor reads as
   "not really random." Poisson-disk with a minimum separation of ~4u and a density-gradient
   weight produces natural, park-like spacing. Medium effort (~2–3h): a self-contained
   `PoissonScatter` helper using `System.Random(seed)`.

---

## Evidence

### A — The board dictates the two-type language

- **Source:** `inspiration/2026-06-12_21h11_03.png` (this repo, viewed directly) — **Strong**
  (Sponsor-set ground truth). Four blob-canopy variants. Tree 1: tall narrow oval, single
  large central blob. Tree 2: wide-crowned, 5–6 blobs at different heights, prominent DARK
  lower blob and BRIGHT upper blob — the 3-value green separation is clearly legible
  (shadow / body / top-lit). Top-facing polygons on the upper blobs are noticeably lighter
  (almost yellow-green) — the "bright top face" idiom. Tree 3: flat pancake crown,
  wide-spread cluster. Tree 4: neat round crown. Four distinct silhouettes from one idiom
  by varying blob count, spread, and height ratio.

- **Source:** `inspiration/2026-06-12_21h12_49.png` (this repo, viewed directly) — **Strong**
  (Sponsor-set ground truth). A Blender nature-kit shot: a pine/cone tree (stacked cone
  tiers, darker saturated green) is visible alongside blob-round trees, a log, and a stump.
  This is the PINE type the board includes — it is not currently in the codebase.

- **Source:** `inspiration/2026-06-12_21h22_33.png` (this repo, viewed directly) — **Strong**
  (Sponsor-set ground truth). Dense forest of tall straight trunks with layered cone/disk
  canopies — the interior forest language is conifer-dominant. A mix of blob-round and
  pine silhouettes is the target; a pure blob forest reads samey at this density.

- **Source:** `inspiration/2026-06-12_21h10_44.png` (this repo, viewed directly) — **Strong**
  (Sponsor-set ground truth). Branching-trunk trees (the "branched" look) with multi-piece
  canopy clusters showing trunk branching; the leftmost figure is clearly pine-cone style.
  Confirms branching-trunk read is desirable for the "tall" blob tree (the current
  `BuildTree` tall variant has a straight bare trunk — a small lateral branch mid-trunk
  would match the board better).

### B — Per-tree vertex-color multi-green: the "3-4 greens per tree" rule

- **Source:** `LowPolyMeshes.BlobCanopy` (this repo, `Assets/Scripts/Editor/LowPolyMeshes.cs`
  lines 454–505) — **Strong** (production code). The per-blob green blend already interpolates
  `shadowGreen → bodyGreen → topGreen` by blob height. The three anchor colors are defined
  in `LowPolyZoneGen.cs`: `CanopyShadow(0.18,0.40,0.17)`, `CanopyBody(0.30,0.58,0.24)`,
  `CanopyTop(0.48,0.74,0.34)`. The per-blob `vj` value jitter is ±0.06.

- **Diagnosis:** The `heightK` ramp in `BlobCanopy` (lines 483–486) compresses the shadow
  range. The lower blobs (b==0, `upBias = radius*0.10`) resolve to `heightK ≈ 0.07–0.12`,
  which blends `shadowGreen → bodyGreen` at t≈0.18 — the result is only slightly darker
  than body green, not the deep shadow green visible on the board's lower blobs. A fix:
  use `Mathf.Pow(heightK, 2.0f)` (square the blend factor) to compress the lower blobs
  harder toward `shadowGreen`, widening the contrast.

- **Source (white-edge-highlight idiom):** Board images 21h11_03 and 21h10_44 (viewed
  directly). In both, the topmost facet of the canopy cluster is conspicuously lighter —
  nearly yellow-green — relative to the mid-green body. This is a deliberate low-poly
  technique: place one small, slightly flattened blob at the very top of the cluster with
  the `topGreen` color applied uniformly (no `vj` darkening), so one face reads bright
  against the mid-green mass. The existing `BlobCanopy` already places the tallest blob
  near the apex, but the `heightK` compress means it lands at body-green, not top-lit.
  Unclamping `heightK` on the topmost blob (`b == tallestBlobIdx`) to 1.0 forces it to
  `topGreen` regardless of actual height — a targeted one-line fix.

- **Source:** Sunday Sundae — "How To Make Low Poly Look Good"
  [https://sundaysundae.co/how-to-make-low-poly-look-good/] — **Moderate** (well-sourced
  game-art tutorial, no publisher year given). Confirms: "the shape of the object reads
  well from all angles is very important"; flat-shaded normals ("no shared vertices")
  produce the characteristic look. For canopies specifically: "Use varied polygon densities
  proportionally to object size — if you halve size, halve polygons." This is the rationale
  for keeping small trees at 4 blobs and large trees at 6–7.

### C — Pine / cone-tier canopy: the second tree archetype

- **Source:** Board images 21h12_49, 21h22_33 (viewed directly, see Evidence A) — **Strong**.
  The pine silhouette is a stack of 3–4 decreasing-radius flat disk/cone tiers on a tall,
  straight trunk. The top tier is a small cone cap. Each tier is a flat-ish faceted disk
  (not a sphere): low vertical extent, wide radial spread, slight downward droop at the
  edges. The per-tier color is darker saturated green at the bottom (shadowed by the tier
  above) and brighter at the top tier.

- **Source:** Roblox devforum — "How to make an amazing low poly tree"
  [https://devforum.roblox.com/t/how-to-make-an-amazing-low-poly-tree/1483011/8] —
  **Moderate** (community tutorial, well-regarded; direct URL visit returned only user
  images but not the original tutorial text). The linked example images (visible from the
  URL context) confirm the cone-tier pine is a standard low-poly tree idiom widely used
  in Unity/Roblox/Godot projects. Evidence strength: Moderate (images confirm idiom;
  tutorial text not fetchable at time of research).

- **Construction recipe for `LowPolyMeshes.ConeTierCanopy`:**
  A stack of `N` tiers (3–4), each tier a `TaperedCylinder` with `topR ≈ 0` (cone-like),
  low height (0.5–0.8u), large base radius (decreasing per tier from bottom to top). Each
  tier is flat-shaded (per-face outward normals, the `FacetedRock` idiom) so each tier's
  upward-facing face catches the key light clearly — the "stacked plates" silhouette read.
  Per-tier vertex-color: bottom tier = `CanopyShadow`, middle tier = `CanopyBody`, top tier
  = `CanopyTop`. Seeded radial jitter on the base ring (±15% per vert) breaks the perfect
  circle into a faceted polygon edge — the "chunky cone" read from the board.

  Because each tier is flat-shaded with per-face normals, the normal trap (welded
  RecalculateNormals averages to a smooth mound) is avoided by construction — same guard
  as `FacetedRock`. The `TaperedCylinder` base already exists but is WELDED; the pine tier
  needs a NEW flat-shaded variant or explicit `SetNormals` after the fact.

  Alternative: build the whole pine as ONE mesh (all tier faces flat-shaded into a single
  `Mesh`) — avoids multiple child GameObjects per tree (GPU Resident Drawer prefers fewer
  draw calls). Each tier's triangles emit their own 3 verts + face normal (the `FacetedRock`
  flat-shade loop applied per tier). One mesh object = one draw call.

### D — Lean / canopy-shift as a low-cost visual diversity lever

- **Source:** Board images 21h10_44, 21h13_31 (viewed directly) — **Strong**. Several of
  the board trees show a slight lean (3–8° from vertical) and a canopy placed slightly
  off-axis from the trunk tip. Neither requires a different mesh — both are transform-level
  parameters on the tree GO.

- **Implementation:** Two new seeded values in `BuildTree`:
  - `leanAngle`: `rnd.NextDouble() * 7f - 3.5f` degrees on X or Z, applied to
    `tree.transform.rotation = Quaternion.Euler(leanAngle, yaw, 0f)`.
  - `canopyShift`: `new Vector3((float)(rnd.NextDouble()-0.5)*0.3f, 0, (float)(rnd.NextDouble()-0.5)*0.3f)`
    added to `canopy.transform.localPosition` so the canopy sits slightly off-center above
    the trunk tip. Shift range ±0.15u at scale 1.0 — visible from orbit.

  These two parameters alone produce 5–6 perceptually distinct variants from the same
  mesh with zero extra geometry cost.

- **Height / canopy-radius range widening:** The current `tall` branch uses `scale` 1.5–2.4
  and the mid branch 1.0–1.6. Widening to 0.7–1.0 (small understory tree) as a third
  branch adds a visible small/medium/large read in the forest — the "big alive world"
  read depends partly on tree scale variation. Three height bands (small / mid / tall)
  at roughly 20% / 55% / 25% probability.

### E — Seeded Poisson-disk scatter vs. current uniform-area sampling

- **Source:** `LowPolyZoneGen.ScatterIslandProps` (this repo, lines 553–575) — **Strong**
  (production code). Current tree placement: `r = plantOuterR * Mathf.Sqrt(u)` (uniform
  area sample) + rejection tests. `Mathf.Sqrt` corrects the central-density bias of a
  naive polar sample, giving uniform probability density over the disc. BUT: uniform
  sampling allows multiple accepted points arbitrarily close together (no minimum
  separation) AND locally empty regions — both perceptible as "clumping + gaps."

- **Source:** Vertexfragment.com — "Variable Density Poisson-Disk Sampler"
  [https://www.vertexfragment.com/ramblings/variable-density-poisson-sampler/] — **Strong**
  (cited C# implementation detail, reproducible, well-documented). Poisson-disk sampling
  enforces a minimum distance `r_min` between any two placed points. Grid cell size =
  `r_min / sqrt(2)`. Each accepted point generates up to `k` candidate neighbors in an
  annulus `[r_min, 2*r_min]`; each candidate is accepted only if it clears the grid.
  Result: no two trees closer than `r_min`, no large empty patches — the "natural park
  spacing" read.

- **Source:** Unity Perception Package — `PoissonDiskSampling` class
  [https://docs.unity3d.com/Packages/com.unity.perception@1.0/api/UnityEngine.Perception.Randomization.Utilities.PoissonDiskSampling.html]
  — **Strong** (official Unity documentation). Confirms the API: `Generate(width, height,
  minimumRadius, seed, maxSamplesPerPoint)`. The function is in the Perception package
  (not in Unity core), so FOR OUR USE we implement it inline — the algorithm is short
  (~60 lines C#) and the `System.Random(seed)` makes it deterministic.

- **Source:** Sebastian Lague — Procedural Object Placement (E01: Poisson Disc Sampling)
  [https://www.youtube.com/watch?v=7WcmyxyFO7o] — **Moderate** (educational video,
  well-known Unity procedural tutorial; widely cited). Provides the reference C#
  implementation used across hundreds of Unity indie projects. Key parameter: `numSamplesBeforeRejection = 30`
  (the standard Bridson algorithm `k` value). Seeded by passing `new System.Random(seed)`
  to all `NextDouble()` calls.

- **Application to our island scatter:** The island is not a square grid — it is an
  irregular disc with a warped coast. Poisson-disk on the FULL bounding square then
  reject any point outside `OnLandmass()` is the standard approach and preserves the
  existing `OnLandmass` / `spawnClearR` logic verbatim. Setting `r_min = 4.0f` (roughly
  one tree canopy diameter) for the main forest; reducing to `r_min = 2.0f` in a
  dense-understory ring (r < spawnClearR * 2) for varied grouping near the clearing edge.

- **Density-weighted accept/reject:** Keep the existing inland-density accept bias
  (`0.45 + inlandT * 0.55`) inside the Poisson candidate loop to preserve the coastal
  thinning. The bias is applied AFTER the minimum-distance check so it only further
  filters the already-spaced candidates.

### F — GPU Resident Drawer and draw-call budget

- **Source:** `unity6-mastery.md` §2 (this repo) — **Strong** (project mandatory pre-read).
  320 trees × 2 mesh objects (trunk + canopy) = 640 draw calls without instancing. GPU
  Resident Drawer handles this if: no `MaterialPropertyBlocks`, all trees share a small
  number of materials (current quantized palette gives ~4 canopy + ~1 trunk = 5 total).
  A pine type with a flat-shaded cone-tier mesh adds ONE new material (same
  `LowPolyVertexColor` shader with `_Tint` = canopy body green). Total materials stays at
  5–6 — well within the GPU Resident Drawer sweet spot.

  If the pine canopy is ONE mesh object (all tiers merged into a single flat-shaded mesh,
  see Evidence C) + the trunk, tree count stays 2 draw calls per tree — no regression.

---

## Application to Embergrave Far Horizon

### What exists (do not regress)

| Component | File + Location | Status |
|---|---|---|
| `BlobCanopy` — welded, vertex-color greens | `LowPolyMeshes.cs` lines 454–505 | Production; quality patch targets this |
| `BuildTree` — blob + tall variants | `LowPolyZoneGen.cs` lines 636–673 | Production; pine branch + lean/shift added here |
| `TaperedCylinder` — welded smooth | `LowPolyMeshes.cs` lines 23–57 | Production; pine-trunk reuses; cone-tier needs flat-shaded wrapper |
| Uniform-area disc scatter | `LowPolyZoneGen.cs` lines 553–575 | Production; Poisson-disk replaces this loop |
| `CanopyShadow / Body / Top` palette | `LowPolyZoneGen.cs` lines 77–79 | Production; shadow end widened by pow-curve change |

### Ranked new code (impact-to-effort order)

---

**Rank 1 — Pine / cone-tier tree type. Effort: 3–4h. Impact: HIGH.**

Add `LowPolyMeshes.ConeTierCanopy(float baseRadius, int tiers, float tierHeight,
float jitter, Color shadowGreen, Color bodyGreen, Color topGreen, int seed)`:
- Build as a single flat-shaded mesh (all tier triangles merged): avoids multi-GO overhead.
- Each tier: `sides = 7–9`, decreasing `baseRadius` per tier by 0.7×, height `tierHeight`.
  Top tier: `topR ≈ 0.05` (a near-point — the cone tip).
- Per-tier vertex-color: tier 0 (bottom) gets `shadowGreen`, tier N-1 (top) gets `topGreen`.
- Per-vert radial jitter: `radius *= (1 + rnd.NextDouble()*jitter - jitter*0.5)`.
- Flat-shade with per-face outward-winding guard (verbatim `FacetedRock` idiom).

Placement in `BuildTree`: add an `enum TreeType { Blob, Tall, Pine }` (or equivalent bool).
Pine probability: ~25% of the overall 320-tree target. Pine trees: taller trunk
(3.5–5u), narrower (2–3 tiers of `baseRadius` 1.2→0.7→0.3u), slightly darker overall
(`CanopyShadow` blended ~20% darker for pines to read as conifer).

---

**Rank 2 — Vertex-color contrast patch + lean/shift variation. Effort: 1–2h. Impact: HIGH.**

In `BlobCanopy` (patch to `heightK` blending):
```csharp
// BEFORE (line 483):
Color lo = Color.Lerp(shadowGreen, bodyGreen, Mathf.Clamp01(heightK * 1.6f));
// AFTER — square the blend factor to compress lower blobs harder toward shadowGreen:
Color lo = Color.Lerp(shadowGreen, bodyGreen, Mathf.Clamp01(Mathf.Pow(heightK, 2.2f) * 1.6f));
```
For the topmost blob in the cluster (detect as the one with the highest `center.y + upBias`
after all blobs are laid out — easiest: track `tallestBlobY` in the loop and force
`blobCol = topGreen` if `center.y == tallestBlobY`). This gives the "bright top polygon"
the board shows.

In `BuildTree`, add lean + canopy-shift (see Evidence D):
```csharp
float leanX = (float)(rnd.NextDouble() * 6.0 - 3.0); // ±3 degrees
float leanZ = (float)(rnd.NextDouble() * 6.0 - 3.0);
tree.transform.rotation = Quaternion.Euler(leanX, yaw, leanZ);
float shiftX = (float)(rnd.NextDouble() - 0.5) * 0.28f;
float shiftZ = (float)(rnd.NextDouble() - 0.5) * 0.28f;
canopy.transform.localPosition += new Vector3(shiftX, 0, shiftZ);
```
These two parameters are the cheapest diversity lever — they read clearly from orbit.

---

**Rank 3 — Poisson-disk scatter. Effort: 2–3h. Impact: MEDIUM (fixes "not really random").**

Replace the `r = plantOuterR * Mathf.Sqrt(u)` sampling loop with a `PoissonScatter2D`
helper:
```csharp
// Bridson's algorithm — self-contained, ~60 lines, seeded System.Random
// r_min: 4.0f for main forest; 2.5f for dense understory ring
static List<Vector2> PoissonScatter2D(float outerR, float rMin, int seed, int k = 30)
```
The result is a list of `Vector2` candidate positions. Feed through the existing
`OnLandmass` / `spawnClearR` / density-bias checks before calling `BuildTree`. The
`InlandT` density bias becomes a KEEP/DISCARD filter on the Poisson candidates
(already in the loop), not a probability at the sampling stage.

Setting `r_min = 4.0f` on the main tree scatter (320-tree target) produces natural
park-like spacing with no touching canopies and no large empty fields — both the
Sponsor's "not really random" complaints addressed by one parameter.

---

### Three-variant tree palette (to avoid the "all same-color" read)

The board shows subtle color variation across individual trees (not per-forest-region but
per-tree tint). Implement as a seeded tint multiplier on the canopy material:
- `tintLerp = 0.0f..0.18f` (a small lerp toward `CanopyBody` or `CanopyShadow`).
- Applied by passing a slightly modified `bodyGreen / topGreen / shadowGreen` to
  `BlobCanopy` per tree instance: `bodyGreen * (1f + (float)(rnd.NextDouble()-0.5)*0.12f)`
  in the RGB channels.
- Because the canopy material is the same `LowPolyVertexColor` with `_Tint = white`, the
  per-blob vertex colors carry the variation — no extra materials.

---

### What NOT to do

- **Do NOT flatten/subdivide the trunk further** to simulate branching — it adds verts with
  no silhouette gain at orbit distance. Trunk branching on the board (21h10_44) is the
  branched-trunk TREE TYPE (a separate art style), not the main island-forest language. The
  island forest (21h22_33) has straight trunks — correct as-is.
- **Do NOT switch to flat-shading on the blob canopy.** The `BlobCanopy` uses welded +
  `RecalculateNormals` (smooth shading) which gives the soft "foliage lump" read. Flat-
  shading the blobs would make them look like faceted rocks, not foliage. The pine tiers
  ARE flat-shaded because stacked-plate silhouette read NEEDS the hard facet edges.
- **Do NOT proliferate extra materials per tree** — the existing quantized palette gives
  4–5 materials for 320 trees; GPU Resident Drawer handles this. Per-instance
  `MaterialPropertyBlocks` would break GPU Resident Drawer (unity6-mastery §2).
- **Do NOT add shadow-casting to the canopy meshes.** Trees are currently unshadowed props;
  shadow casting at this count (320+) would cost shadow-map passes. The vertex-color
  AO baked into blob heights handles the self-shadowing read.

---

### Soak path

The dev builds a POC: current island with ~30 trees updated (10 blob, 10 tall, 10 pine)
+ lean/shift + Poisson scatter. Ship as a capture from the gameplay-cam orbit position
(the Sponsor's standard view). The Sponsor approves the species mix and color contrast
before the full 320-tree regeneration. This matches the project's soak-iterative pattern
for visual changes.
