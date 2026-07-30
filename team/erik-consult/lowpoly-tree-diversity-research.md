# Low-Poly Tree Diversity — Quality + Variety POC Recommendation

## Question

The Sponsor noted the forest reads as "a lot of the same tree, spread kinda randomly" (ticket `86cabc73q`).
What is the most cost-effective approach to deliver improved quality + visual diversity + natural placement
for a soak-ready POC — while preserving the 1-draw-call discipline and the seed-42 island lock?

## Bottom line

Three layers of work, each independently soakable: **(1)** two new canopy archetypes (conical "pine" + flat
layered "acacia") added to `BuildTree` via archetype enum, so the forest mixes three silhouettes instead of
one; **(2)** per-tree green-hue tint jitter (±0.05 HSV shift baked into vertex color at generation time)
so instances of the same archetype no longer read as a flat-green clone; **(3)** Poisson-disc minimum-
distance scatter (Bridson O(n) algorithm, min-sep ~4u) replacing the current rejection-sample uniform-disc,
so trees organically cluster at forest-density interior rather than spreading in a visually-even wash.
All three can ship as one PR that passes existing scatter tests, re-generates `Boot.unity`, and provides a
capture for the Sponsor to soak.

---

## Evidence

### A — Archetype diversity (canopy shape contrast)

- **Source:** Emek Can Özben, "Lowpoly Environment Design Tricks," 80.lv,
  [https://80.lv/articles/emek-can-ozben-low-poly-environment] — **Moderate** (practitioner article,
  shipped environment art). Key quote: "I deliberately combined spiky trees with a contrast of rounded trees
  to add visual variety while maintaining stylistic coherence." The author explicitly calls out shape contrast
  (spiky/conical vs rounded/blob) as the primary lever for breaking repetition in low-poly forests.

- **Source:** Art direction board direct observation — `inspiration/21h11_03.png` (four tree variants),
  `21h12_49.png` (Blender nature kit shot, pine + blob trees side by side), `21h16_13.png` (pine forest
  behind campfire with a blob tree visible at left) — **Strong** (ground truth). The inspiration board
  already shows three readable archetypes in-frame: tall conical pine (needle-cluster stacked tiers), mid-
  height round blob (the current generator), and short acacia-ish flat-top (wide spread canopy over a
  branchless trunk). These are the board's exact tree vocabulary; the generator currently only covers the
  round blob variant.

- **Source:** Roblox DevForum, "How to make an amazing low poly tree," post #8 user result,
  [https://devforum.roblox.com/t/how-to-make-an-amazing-low-poly-tree/1483011/8] — **Weak** (this is
  the community response post, not the tutorial body; the tutorial's technique was reconstructed from the
  page fetch: ico-sphere + randomize function for each canopy cluster, organic trunk irregularity via
  vertex displacement). The underlying geometry approach — multiple ico-sphere clusters composited to
  form a canopy — is identical in principle to the existing `BlobCanopy` clustered-spheroid generator.
  The reference demonstrates that shape variation (irregular vs round vs tiered) is the standard creative
  lever, not shader or material changes.

- **Application:** Three archetypes are enough to break repetition without adding significant maintenance
  cost. `BuildTree` currently dispatches on a `bool tall` — extend to a 3-value enum:
  - **BLOB** (current): `BlobCanopy` with 4–7 spheroids, round crown. The current generator, unchanged.
  - **CONICAL** (new): three stacked `BlobCanopy` tiers of decreasing radius (bottom 1.4u, mid 1.0u, top
    0.6u) offset vertically at trunkH × 0.4 / 0.65 / 0.9, plus a taller TaperedCylinder trunk (fewer
    sides, 5 sides, to read pine-ish). Approximates a pine/fir silhouette from a distance using the
    existing generator with no new code path in LowPolyMeshes.cs.
  - **FLAT** (new): 2–3 `BlobCanopy` blobs at near-equal height offset (low crown height variance),
    with wider radius (1.6u) and a shorter trunk (0.8× the blob trunk height). Reads as acacia/oak flat
    top. Same generator, different parameter layout.
  
  Archetype assignment: drive by `rnd.NextDouble()` at scatter time — e.g. 45% BLOB / 35% CONICAL /
  20% FLAT — so each archetype reads common enough to feel like a real biome, not an isolated specimen.

### B — Per-tree hue tint jitter (color variation without new materials)

- **Source:** Low Poly Trees Bundle (Superhive/Blender Market), product description,
  [https://superhivemarket.com/products/lowpolytreesbundle] — **Moderate** (shipped commercial asset,
  production-validated). Uses a single shared palette texture + per-instance color variation to make
  trees read as distinct across 38+ variants. The design decision — one material, per-instance color
  — is a direct confirmation the approach is compatible with the 1-draw-call discipline.

- **Source:** `LowPolyZoneGen.cs` codebase (`CanopyBody`, `CanopyTop`, `CanopyShadow` constants
  at lines 77–79) + `BlobCanopy` function signature — **Strong** (in-codebase, confirmed working).
  The 3-value green palette is already baked per-blob into vertex color at generation time. Introducing
  a seeded per-tree ±0.03 HSV-value offset on `CanopyBody`/`CanopyTop`/`CanopyShadow` before passing
  them to `BlobCanopy` costs zero shader changes (vertex color is already the shading path) and zero
  extra draw calls (the shared `CanopyVertexColorMat()` continues to be the single cached material).
  The pattern is identical to the existing per-blob VALUE jitter in `BlobCanopy` (lines 521–526 of
  `LowPolyMeshes.cs`) — just shifted one level up (per-tree, not per-blob).

- **Source:** `BlobCanopy` implementation — the function already applies a per-blob colour VALUE jitter
  (`vj = (float)rnd.NextDouble() * 0.12f - 0.06f`) within a single tree's canopy. A per-tree HSV-hue
  shift (`±0.05` = about 18° on the hue wheel, keeping the greens in green territory: 90–140° range
  stays safely green even at ±18°) stacks on top and is already the correct code pattern.

### C — Natural placement: Poisson-disc minimum-separation scatter

- **Source:** Bridson, Robert, "Fast Poisson Disk Sampling in Arbitrary Dimensions," SIGGRAPH 2007
  Sketches — referenced by Colin Veron, "Poisson Disc Sampling,"
  [https://coleslow.dev/blog/poisson-disc-sampling/] — **Strong** (canonical algorithm, O(n) time).
  Key finding: pure random (rejection-sample on a disc) produces visible clustering and conspicuous
  voids; Poisson-disc enforces a minimum separation `r` with Bridson's algorithm, producing a
  "Voronoi-like" distribution where no area is degenerate. The article notes both Unity terrain scatter
  and Unreal foliage painter use variants of Poisson-disc internally.

- **Source:** Fast Poisson Disk Sampling for Unity (C# gist),
  [https://gist.github.com/a3geek/8532817159b77c727040cf67c92af322] — **Moderate** (community-
  maintained, widely cited in Unity forums; implements Bridson's algorithm in C# with a grid
  accelerator). Drop-in reference for Unity Editor tool authors.

- **Source:** Gregory Schlomoff, "Poisson-disc sampling in Unity,"
  [http://gregschlom.com/devlog/2014/06/29/Poisson-disc-sampling-Unity.html] — **Moderate** (Unity-
  specific practitioner writeup, reproducible implementation). Notes `r` (minimum separation) as the
  only meaningful parameter — lower `r` = denser forest; higher `r` = sparse.

- **Application to Embergrave current code:** `ScatterIslandProps` (lines 563–584 of
  `LowPolyZoneGen.cs`) currently uses a **rejection-sample uniform-disc loop** — `r = R×sqrt(u)` to
  avoid centre-bias, reject outside `OnLandmass`, reject inside `spawnClearR`, accept probabilistically
  based on `inlandT`. This produces roughly uniform random placement across the disc. The resulting
  visual: trees are spread in a wash that looks neither clustered (no forest clumps) nor strictly
  grid-like, but the even density at every scale is what reads as "random" in the Sponsor's subjective
  read — real forests have dense clumps separated by natural gaps.

  Poisson-disc minimum-separation scatter (min `r ~4u`) would instead guarantee no two trees closer than
  4u while still allowing the rejection-accept by `inlandT` to bias inland density. The Bridson
  algorithm produces points in O(n) even for the 320-tree target — practically instant at generation
  time (it runs once at `Boot.unity` generation, not at runtime).

  **Critical constraint:** the seed-42 island shape / waterline / NavMesh lock (`SeededScatterVariationTests`
  AC7a) is NOT affected by changing the scatter algorithm, because those are driven by `SeedOffset` /
  `HeightAtRadial` / `ShoreRadiusAt` / `CliffinessAt` — world-gen fields separate from the scatter
  stream. The tree PLACEMENT will change (different positions), but the island geometry will not.
  The existing `SeededScatterVariation` tests pin the height-variation and lean; the tree position
  tests check "on landmass" (not specific positions) — so a placement algorithm change passes those
  tests by construction as long as every planted tree lands on the landmass.

  **Cluster-density alternative:** a lighter change short of full Poisson-disc is to scatter trees in
  micro-clusters: pick a cluster centre on the disc, then scatter 4–8 trees within a 6u radius of that
  centre (tight sub-disc), repeat for ~40 cluster centres. This emulates real forest stand dynamics
  (trees seed together) with very simple code — each cluster appears as a dense grove, and the
  between-cluster gaps read as clearings. Requires ~20 lines of code. Less theoretically principled
  than Bridson but produces a visually compelling forest-clump look directly.

---

## Application to Far Horizon

### What the Sponsor is seeing

The current `BuildTree` has two variants (`tall` bool) that both use `BlobCanopy` — effectively one canopy
silhouette at two height scales. The uniform-area disc scatter spreads 320 trees across the island with no
minimum separation and no clustering. From the orbit camera the canopies blend into a single green wash
at near-uniform density. No shape contrast, minimal hue variation, no perceptual "forest clumps."

### Recommended POC: three-layer patch, single PR

**Layer 1 — 3 canopy archetypes (highest impact, low risk).** Replace the `bool tall` dispatch in
`BuildTree` with a 3-way enum (BLOB / CONICAL / FLAT). Author the CONICAL and FLAT variants using
the existing `BlobCanopy` generator with different parameter layouts (stacked tiers vs wide low crown).
No new mesh generators, no new materials, no new shaders. Single shared `CanopyVertexColorMat()` continues.
The CONICAL tier stack requires 3 `BlobCanopy` calls instead of 1 per tree — `~3×` the canopy mesh
generation cost, but this is a one-off `Boot.unity` bake, not a runtime cost.

**Layer 2 — Per-tree hue-value tint jitter (medium impact, zero shader risk).** Derive a per-tree colour
seed from the plant position (same `leanRnd` sub-stream pattern already in `BuildTree`). Shift all three
canopy constants by `±0.03` on VALUE and `±0.05` on the green HUE channel before passing to `BlobCanopy`.
This makes same-archetype trees read as distinct individuals. Zero material changes, zero draw-call
increase, zero test breakage.

**Layer 3 — Cluster scatter (medium impact, medium code risk).** Replace the rejection-sample loop with
micro-cluster placement: 40 cluster centres scattered on the landmass interior, 8 trees per cluster in
a 6u sub-radius. Total: 320 trees, same count, but reading as 40 distinct groves. The `inlandT` density
bias applies to cluster-centre acceptance, not individual-tree acceptance. The seed-42 island shape is
untouched. Existing "on landmass" tests pass. `Boot.unity` must be regenerated and committed.

**Cost estimate:**
- Layer 1: ~3–4h Dev time (new enum, two new parameter layouts, test updates)
- Layer 2: ~1h Dev time (5–10 lines in `BuildTree`)
- Layer 3: ~2h Dev time (new scatter loop, `Boot.unity` regen)
- Total: ~6–7h Devon/Drew time; one PR; soak-ready

**Quality bar for the soak:** the Sponsor should be able to see (from the orbit camera) three distinct
tree silhouettes (round blob, conical/pine, flat-top), individual trees with slightly different green tones,
and clusters of trees with visible gaps between groves rather than a uniform wash. Predict-Before-Soak
recommendation: "You should see grove-clusters with clearings between them, and three readable tree shapes
in the canopy mix — the round blobs from before, a spiky pine-ish shape, and a flatter spread canopy."

**Draw-call discipline:** the shared `CanopyVertexColorMat()` continues. The new archetypes add mesh
variety but not material variety — the 1-draw-call-per-tree-canopy discipline holds. Trunk material
`LPTrunkMat` is also shared. Total material count unchanged.

**Palette constraint:** all three archetype greens derive from the same `CanopyBody` / `CanopyTop` /
`CanopyShadow` constants — the hue jitter shifts these within the warm-green band, never outside it.
The unified world palette (single URP Shader `LowPolyVertexColor`) is unaffected.

---

## What NOT to do

- **Do not add new shader materials per archetype** — a separate "pine green" material breaks the 1-draw-
  call discipline and forces extra `AlwaysIncludedShaders` entries.
- **Do not use Unity Terrain Detail trees / SpeedTree** — the project's procedural generation route is
  locked; SpeedTree adds billboard complexity and doesn't integrate with the vertex-color shader.
- **Do not add a wind/sway shader to solve the "same tree" look** — motion (Devon's current task) and
  shape diversity are orthogonal. Shape diversity is the correct fix for "looks the same"; sway is for
  "looks static." Both are wanted; neither substitutes for the other.
- **Do not generate 5+ archetype variants upfront** — three is the perceptual threshold beyond which
  the Sponsor likely won't notice additional distinction from the orbit camera. Scope to three; expand
  if the Sponsor's soak calls for it.
