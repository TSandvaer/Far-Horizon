# Low-Poly Trees — Implementation-Ready Spec

**Ticket:** `86cabc73q` "Make nice low poly trees"
**Research base:** `team/erik-consult/low-poly-trees-research.md` (evidence, citations, per-file sketches)
**Pre-read evidence:** Inspiration board images `21h10_44`, `21h11_03`, `21h12_49`, `21h13_31`, `21h16_13`, `21h21_30`, `21h22_33` — viewed directly.

---

## What the board shows (ground truth, not a summary)

- **21h11_03:** Four blob trees side by side. Each has 4–6 distinct spheroid blobs. The topmost blob on every tree is conspicuously brighter (near yellow-green); the lowest blobs are deep shadow-green. Three clearly distinct green values per tree — not a gradient, a stepped read.
- **21h10_44, 21h12_49:** A pine / cone-tier type appears alongside the blob trees in both. Stacked flat disk-like tiers, dark saturated green, tall straight trunk. This type is NOT in the current codebase.
- **21h13_31, 21h21_30:** Mixed blob + pine forests on rolling hills. Small / mid / tall size variation visible — understorey small trees exist, not every tree is a jungle giant.
- **21h22_33:** Dense interior forest, conifer-dominant, straight trunks, layered canopy at ground level.
- **21h16_13:** Wide vista — pine silhouettes dominate the horizon; the small-character/big-world read depends on trees being tall relative to the player.

**Two tree archetypes are mandated by the board.** The current codebase has blob + tall-blob only. A pine / cone-tier type is required.

---

## 1. Mesh generation

### Codebase baseline (do not regress)

| Component | File | What it does |
|---|---|---|
| `BlobCanopy(radius, blobs, bodyGreen, topGreen, shadowGreen, seed)` | `LowPolyMeshes.cs` line 488 | Welded spheroid cluster, `RecalculateNormals` (smooth) — gives the soft foliage-lump read. **Do NOT flat-shade this.** Flat-shading blobs makes them read as faceted rocks, not foliage. |
| `TaperedCylinder(botR, topR, height, sides)` | `LowPolyMeshes.cs` line 23 | Welded smooth trunk. Reused by both tree types. |
| `BuildTree(parent, at, scale, rnd, carve, tall)` | `LowPolyZoneGen.cs` line 679 | Creates trunk + blob canopy. Lean/height variation already added (ticket `86caamnra`). |
| `ScatterIslandProps` | `LowPolyZoneGen.cs` line 530 | 320-tree disc scatter via `r = R*sqrt(u)` uniform sampling. |

### New mesh: `LowPolyMeshes.ConeTierCanopy`

**Purpose:** the pine / cone-tier archetype visible throughout the board.

**Signature:**
```
ConeTierCanopy(float baseRadius, int tiers, float tierHeight, float jitter,
               Color shadowGreen, Color bodyGreen, Color topGreen, int seed) → Mesh
```

**Construction — build as ONE merged flat-shaded mesh (all tier triangles into a single `Mesh` object):**
- `tiers` = 3 or 4 (default 3 for short pine, 4 for tall pine).
- Tier 0 (bottom): `baseRadius`, wide. Each subsequent tier: `baseRadius *= 0.65f`. Top tier: `topR ≈ 0.05f` (near-cone-point cap).
- `sides` per tier: 7–9 (produces a chunky polygon edge, not a smooth circle).
- Per-vert radial jitter: `r *= (1.0 + rnd.NextDouble() * jitter - jitter * 0.5)` where `jitter ≈ 0.18`. Breaks the perfect circle into the chunky polygon silhouette.
- `tierHeight` ≈ 0.55–0.8u per tier (flat disk, not a tall cone volume).
- Per-tier vertex-color: tier 0 → `shadowGreen`; middle tier(s) → `bodyGreen`; top tier → `topGreen`.
- **Flat-shade each tier with per-face outward-winding enforcement** — verbatim the `FacetedRock` idiom (`Vector3.Dot(fn, faceCentre) < 0` flip, per-triangle 3-vert emission). Each tier's upward face catches key light as a distinct bright plane — the "stacked plates" read from `21h12_49`.
- Why one mesh object: GPU Resident Drawer prefers fewer draw calls per tree. One mesh = one draw call for the pine canopy (same as blob). Multiple child GOs per tier would multiply draw calls without visual gain.

**Vert budget:** with 4 tiers × 9 sides × 3 verts/triangle × 2 triangles/face ≈ 216 verts per pine canopy. Plus 6-sided trunk ≈ 72 verts. Total per pine tree: ~290 verts. Blob tree: ~400–600 verts (per existing `BlobCanopy`). **Both well inside the "genuinely low-poly" budget.** No LOD subdivision needed at these counts.

### `BuildTree` extension — add pine branch

In `LowPolyZoneGen.cs BuildTree`, add a third variant alongside `tall`:

```
enum (or bool pair): TreeType { Blob, TallBlob, Pine }
```

Pine probability in the scatter: **~25% of the 320-tree target** (~80 pines). Pines read best in the background — bias placement toward mid-to-outer island radius (inland density already favors this naturally).

Pine dimensions:
- Trunk: `trunkH = 3.8 + rnd.NextDouble() * 1.8f` (taller than blob trees — pines are the "height markers").
- Trunk `botR = 0.18f, topR = 0.10f`, 5 sides (slightly slimmer silhouette).
- Canopy: `ConeTierCanopy(baseRadius: 1.1f, tiers: 3 or 4, tierHeight: 0.6f, jitter: 0.18f, CanopyShadow, CanopyBody, CanopyTop, rnd.Next())`.
- Canopy position: `localPosition = (0, trunkH + 0.2f, 0)` — sits tight against the trunk tip (pines have low canopy start, unlike blob trees).
- Pine-specific color: multiply `CanopyShadow`, `CanopyBody`, `CanopyTop` by `0.88f` before passing (slightly darker overall; conifer read vs deciduous blob). No extra material — same `LowPolyVertexColor` shader, vertex color carries the difference.

**Height/scale bands — three sizes** (currently two):
| Band | Probability | Scale range | Type assignment |
|---|---|---|---|
| Small understory | 20% | 0.7–1.0 | Blob |
| Mid blob | 55% | 1.0–1.6 | Blob or TallBlob (existing split) |
| Tall / pine | 25% | 1.5–2.8 | Pine (half) + TallBlob (half) |

This gives the "small elements in a big alive world" read: standing players perceive understorey trees at their eye level and tall pines looming above.

**Lean + canopy shift (already in codebase per ticket `86caamnra`):** preserve as-is. Both parameters are already implemented in `BuildTree` via the `leanRnd` sub-stream.

---

## 2. Shader

### Canopy material (existing — carry forward)

Both tree types use **`LowPolyVertexColor.shader`** (FarHorizon/LowPolyVertexColor) via `CanopyVertexColorMat()` (one shared cached inline material, `_Tint = white`). The multi-value greens are baked per-vertex into vertex color at mesh-gen time. **No per-tree material instances, no `MaterialPropertyBlocks`** — both would break GPU Resident Drawer (`unity6-mastery.md §2`).

Pine canopy uses the SAME shared `CanopyVertexColorMat()` — darker greens are baked into vert colors, not expressed via a different material. Total materials for 320 trees: ~2 (trunk + canopy). GPU Resident Drawer handles this with a single instanced batch per material.

### Vertex-color contrast patch (BlobCanopy)

The existing `heightK` ramp in `BlobCanopy` compresses lower blobs toward body-green rather than shadow-green. Per the research note Evidence B:

- Change the `heightK` blend factor to `Mathf.Pow(heightK, 2.2f)` — squares the ramp, pushing lower blobs harder toward `CanopyShadow`. This widens the light/shadow contrast to match `21h11_03`.
- For the topmost blob in each cluster, force `blobCol = topGreen` (clamp `heightK = 1.0f` for `b == tallestBlobIdx`). This produces the bright apex read the board shows on every tree.

**Files:** `Assets/Scripts/Editor/LowPolyMeshes.cs` — `BlobCanopy` method, ~lines 483–490.

### `_FlatShading` toggle on pine tiers

The `ConeTierCanopy` uses per-face explicit normals (same as `FacetedRock`) — it does NOT need the `_FlatShading` ddx/ddy toggle (Rec 2 from the quality spec). That toggle is for props that skip the explicit-normal path. Pine tiers bake flat normals directly.

Do NOT enable `_FlatShading` on the blob canopy — smooth normals (`RecalculateNormals`) are intentional for the foliage-lump read.

### Shadow casting

Set `shadowCastingMode = ShadowCastingMode.Off` on all tree canopy `MeshRenderer` components (per `unity6-mastery.md §3` and existing pattern on water). At 320 trees the shadow-map cost would be prohibitive. Trunk shadow casting: off as well (trunk is narrow — the visual benefit does not justify the cost). Vertex-color height gradient already carries the self-shadowing read.

### Wind / sway

**Out of scope for this ticket.** Wind animation in Unity 6 / URP for procedural meshes requires either Shader Graph vertex displacement (needs `positionOS` in the vertex stage + a sine-wave with time) or a custom `IJobParallelFor` bone substitute. Neither is trivially composable with the current `LowPolyVertexColor.shader` without a Shader Graph rewrite. File as a follow-up if the Sponsor requests it. The board images show static trees; the "alive world" feel currently comes from water movement and character animation, not foliage sway.

---

## 3. Scatter + perf

### Replace uniform-area sampling with Poisson-disk

**Current:** `r = plantOuterR * Mathf.Sqrt(u)` — uniform probability over the disc, but allows arbitrarily close neighbours and produces perceptible micro-clusters + empty patches (the "not really random" Sponsor complaint).

**Replace with:** a self-contained `PoissonScatter2D` helper (~60 lines C#, Bridson's algorithm, `System.Random(seed)` for determinism):

```
static List<Vector2> PoissonScatter2D(float outerR, float rMin, int seed, int k = 30)
// r_min = 4.0f for main forest (one canopy diameter separation minimum)
// r_min = 2.5f optionally for a dense-understory ring if desired
// k = 30 (standard Bridson rejection count)
```

Feed the returned positions through the existing `OnLandmass` / `spawnClearR` / `inlandT` density-bias checks — these are point filters, not generators, so they compose unchanged. The `inlandT` bias becomes a keep/discard filter on Poisson candidates.

**Seed discipline:** implement as `new System.Random(seed + 555)` to match the existing scatter stream key (same `seed + 555` offset used by `ScatterIslandProps`). The Poisson grid itself is internal to the helper — it does NOT draw on the shared `rnd` stream, preserving seed-42 island/scatter/NavMesh byte-identity.

**Reference:** Sebastian Lague "Procedural Object Placement E01: Poisson Disc Sampling" [https://www.youtube.com/watch?v=7WcmyxyFO7o] (well-known Unity tutorial, widely reproduced C# implementation). The `System.Random`-seeded version is the correct adaptation for deterministic placement.

### GPU Resident Drawer + draw call budget

From `unity6-mastery.md §2`:
- **Enable GPU Resident Drawer** (`URP Asset > GPU Resident Drawer = Instanced Drawing`).
- **Keep `BatchRendererGroup Variants = Keep All`** in Project Settings > Graphics.
- **No `MaterialPropertyBlocks`** on any tree `MeshRenderer` — they disqualify the GO from the instanced path.
- **No `OnWillRenderObject` / `OnBecameVisible` callbacks** on tree GOs.

With 2 materials (trunk + canopy) and 320 trees, GPU Resident Drawer should batch into 2 instanced draw calls total — confirmed appropriate by the mastery doc's pattern for plain MeshRenderers sharing a small material set.

**Draw call math:** 320 trees × 2 mesh objects (trunk + canopy) = 640 MeshRenderer components. With GPU Resident Drawer instanced drawing, same-material components collapse to 1 draw call per material variant. Result: ~2 draw calls for all 320 blob trees + ~2 draw calls for ~80 pine trees = **4 draw calls total** for the entire island forest. This is the target; verify in Frame Debugger.

### LOD and billboard

**Recommendation: do NOT add LODGroup at this vertex budget.** Each tree is ~300–600 verts. At 400 trees the total vertex count is ~200K — well within the per-frame budget for a desktop GPU. LOD transitions introduce visual pop (distance-based is allowed under GPU Resident Drawer per mastery doc, but cross-fade animated transitions are not and should not be used).

**Billboard impostor:** also not recommended at this stage. Billboard impostor trees (Unity Terrain Tree system) require the Unity Terrain workflow, which is not the current procedural-mesh route. SpeedTree is a paid tool. The Sponsor declined paid AI-3D tools (memory `in-house-asset-routes-over-paid-tools`). At 400 low-vert trees without shadow casting the perf is fine without billboards. Profile first on a development build (`FarHorizon.exe`) before adding LOD complexity.

If distance culling is needed: set `MeshRenderer.forceRenderingOff = true` for trees beyond a cull distance, or use Camera far-clip-plane + URP Volume fog to hide the draw-distance edge (the fog already serves this purpose per `unity-conventions.md`).

### Seeded scatter rotation (already in codebase)

Y-rotation via `leanRnd` sub-stream is already present per ticket `86caamnra`. The Poisson-disk replacement preserves this — the yaw is applied in `BuildTree` per-instance, independent of the sampling method.

---

## 4. Build-ready recommendation

### Implementation order for Drew / Devon

**Step 1 — Vertex-color contrast patch (1–2 h, lowest effort, highest visual payoff):**
In `BlobCanopy` (`LowPolyMeshes.cs` ~line 483):
- Square the `heightK` blend: `Mathf.Pow(heightK, 2.2f)`.
- Force topmost blob to `topGreen` unconditionally (track `tallestBlobY` across the blob loop; set `blobCol = topGreen` when `center.y` is max).

**Step 2 — Pine canopy mesh + BuildTree extension (3–4 h):**
- Author `LowPolyMeshes.ConeTierCanopy` per spec above.
- Add `pine` branch to `BuildTree` (scale, trunk dims, canopy placement, darker green pass-through).
- Add small understory tree probability band (scale 0.7–1.0, blob type).
- Wire `ScatterIslandProps` to dispatch ~25% pines from the 320-tree target.

**Step 3 — Poisson-disk scatter (2–3 h):**
- Add `PoissonScatter2D` helper (self-contained, ~60 lines).
- Replace the `r = R*sqrt(u)` loop in `ScatterIslandProps` with Poisson candidates → existing filter chain.
- `r_min = 4.0f`. Verify `treeTarget = 320` is still reached (increase candidate attempts if needed).

**Step 4 — Soak POC before full 320-tree regeneration:**
Build a capture from the gameplay-cam orbit position with ~30 representative trees (10 blob, 10 tall, 10 pine, size variation visible, Poisson spacing visible). Sponsor approves species mix and contrast before committing the full scatter. This matches the soak-iterative pattern for visual changes (memory `soak-handoff-path-and-explicit-test-checklist`).

### Acceptance / capture gate

Per `TESTING_BAR.md` and `CLAUDE.md` shipped-build capture gate:

1. **EditMode test:** `ConeTierCanopy` mesh has `vertexCount > 0`, `normals.Length == vertexCount` (flat-shaded normals are explicit, not auto-calculated), no vertices at origin-only (mesh is non-degenerate). Mirrors `FacetedRock` test pattern.
2. **Built-exe capture** from gameplay-cam orbit position at ~45° elevation, showing:
   - At least one pine silhouette visible (distinct cone-tier shape, darker green).
   - At least one blob tree with bright apex blob visible (contrast patch applied).
   - No two trees touching canopies (Poisson min-separation enforced).
   - No pink-cast trunks (QuantizeFine fix applied, ticket `86caamnhf` must be merged first or bundled).
3. **Frame Debugger check:** confirm GPU Resident Drawer is batching tree draws (≤4 draw calls for all tree canopy + trunk combinations in the scene). This is a dev-build capture, not a shipped-build requirement — run once, document in PR body.
4. Tess sign-off on the orbit capture before merge.

### QuantizeFine dependency

The `QuantizeFine` fix (ticket `86caamnhf`) must land before or with the trees ticket — trunk color `TrunkCol = (0.35, 0.22, 0.12)` has chroma 0.23 (above the 0.10 threshold) so it would NOT be affected, but if any tree-adjacent prop (stump, rocks at the base) uses near-neutral warm tints, the pink cast would re-appear next to the newly-improved tree colors. Recommend merging `86caamnhf` first to avoid a mixed-quality scene in the soak.

---

## Out of scope (explicit)

- Grass (`86cabc737`) — do NOT touch `BuildGrassClump` or `ScatterIslandProps` grass loops in this ticket.
- Sky (`86cabc743`) — no changes to cloud generation or sky gradient.
- Wind / sway — no vertex animation.
- ChopTree interaction — `ChopTree.cs` uses the tree by transform reference; the visual changes here are purely additive (new GO hierarchy shape). The `ChopTree.visual` field targets the tree root, which is unaffected by trunk/canopy child structure changes. No `ChopTree.cs` edits needed.

---

## Sequencing opinion — trees vs grass vs sky

**Trees first** (`86cabc73q`), then grass (`86cabc737`), then sky (`86cabc743`). Rationale: trees are the dominant silhouette element in every inspiration board shot and in the "small player in a big world" read — they have the highest visual ROI. Grass is important for ground cover but reads at player-level; the orbit-camera primary view is dominated by tree canopies. Sky is the lowest priority because the current gradient skybox already reads clean and the board's sky is simple (no clouds at the visible scale that would overshadow tree improvements). Trees also block the most raw Sponsor feedback variance — get them soaked first.
