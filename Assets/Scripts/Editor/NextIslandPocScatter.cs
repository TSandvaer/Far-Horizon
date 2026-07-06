using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// NEXT-ISLAND POC scatter (ticket 86caa9zpp) — a DENSE low-poly forest + rock + grass load across the
    /// big POC island so the perf verdict (AC4 — the #1 deliverable) is measured with a REALISTIC prop
    /// count at the target size, not on a bare terrain. REUSES the public LowPolyMeshes primitives
    /// (TaperedCylinder / BlobCanopy / FacetedRock / GrassClump) + the SHARED FarHorizon/LowPolyVertexColor
    /// shader — a SELF-CONTAINED mirror of LowPolyZoneGen's scatter idiom, so the seed-42 LowPolyZoneGen path
    /// stays BYTE-UNTOUCHED (the scoped-contract constraint: do not edit / fork the start-island gen).
    ///
    /// Trees carve a NavMeshObstacle so the agent paths around them (same as the start island); grass/rocks
    /// are decoration. Trees + rocks are marked BatchingStatic so URP static-batches the forest (the perf
    /// lever the #1 finding leans on). The scatter thins at the coast + keeps the spawn clearing open, and
    /// is REJECTED on the hero mountain's steep upper flank so the walkable snow peak reads clean (a forest
    /// growing up a snow-cap would be wrong).
    /// </summary>
    public static class NextIslandPocScatter
    {
        // Palette (mirror the start island's blob-canopy greens + bark so the POC reads as the SAME world).
        static readonly Color TrunkCol     = new Color(0.42f, 0.30f, 0.19f);
        static readonly Color CanopyBody   = new Color(0.30f, 0.58f, 0.24f);
        static readonly Color CanopyTop    = new Color(0.48f, 0.74f, 0.34f);
        static readonly Color CanopyShadow = new Color(0.18f, 0.40f, 0.17f);
        static readonly Color RockCol      = new Color(0.62f, 0.60f, 0.555f);
        // Hero rock-feature tints (island 2.0-B / C2). NEAR-NEUTRAL warm-grey, sub-1.0 all channels, R>=G>=B —
        // routed through the local QuantizeFine so they never pink-cast (lowpoly-quality.md §1 Rec 1: the coarse
        // 12-step grid split R≈G≈B into R>G=B). Walls a touch cooler/darker than the small scatter rocks so the
        // big cliffs read with more gravitas; slabs mid-grey stone. Tonal variation is VERTEX-COLOUR only (the
        // FacetedRock per-facet value + AO the mesh bakes), NEVER per-material (T-A) — hence ONE shared mat each.
        static readonly Color WallCol      = new Color(0.56f, 0.55f, 0.52f);
        static readonly Color SlabCol      = new Color(0.60f, 0.585f, 0.55f);

        static Material _canopyMat, _trunkMat, _rockMat, _grassMat, _wallMat, _slabMat;

        /// <summary>
        /// Scatter the POC forest/rocks/grass under `parent`, grounded onto `groundCol` (the POC terrain
        /// collider). Deterministic from `seed` (reproducible baked scene). `treeTarget` is exposed so the
        /// perf-sweep can dial the density (the #1 finding: does the EXISTING approach hold 60fps as the
        /// island scales — density is part of that question).
        /// </summary>
        public static void Scatter(GameObject parent, int seed, MeshCollider groundCol, int treeTarget)
        {
            // Reset the per-build material cache so a re-run does not return materials owned by a destroyed
            // scene (the editor keeps statics across executeMethod invocations).
            _canopyMat = _trunkMat = _rockMat = _grassMat = _wallMat = _slabMat = null;

            var rnd = new System.Random(seed + 555);
            NextIslandPocGen.SeedOffset(seed, out float ox, out float oz);

            float plantOuterR = NextIslandPocGen.MeanShoreR + NextIslandPocGen.CoastIrregAmp;
            float coastalFringe = NextIslandPocGen.BeachWidth + 8f;   // keep trees this far inland of the coast
            float spawnClearR = 30f;                                  // open clearing at the SPAWN point (not origin)
            bool OnLandmass(float x, float z) =>
                Mathf.Sqrt(x * x + z * z) <= NextIslandPocGen.ShoreRadiusAt(x, z, ox, oz) - coastalFringe;
            // Keep an open clearing around the SPAWN (which moved off-origin to clear the mountain foot, run-2 fix)
            // so the player spawns into open ground, not inside a tree.
            bool InSpawnClearing(float x, float z, float extra) =>
                Mathf.Sqrt((x - NextIslandPocGen.SpawnX) * (x - NextIslandPocGen.SpawnX) +
                           (z - NextIslandPocGen.SpawnZ) * (z - NextIslandPocGen.SpawnZ)) < spawnClearR + extra;

            // Reject trees on every peak's STEEP UPPER flank + crown (a forest must not grow up a snow cap or
            // a bare-rock crag). PER-PEAK tree line (86cahwx6w capture-pass-2): the hero keeps the Sponsor-
            // approved 0.45 line; the steep m=1.8 crags pull it DOWN (treeLineFrac 0.10 ≈ 51% of foot radius)
            // so trees never stand on their low-starting rock band — see NextIslandPocGen.AboveTreeLine.
            bool OnBareMountain(float x, float z) =>
                NextIslandPocGen.AboveTreeLine(x, z);

            // ---- DENSE FOREST ----
            int treesPlaced = 0, treeGuard = 0;
            while (treesPlaced < treeTarget && treeGuard++ < treeTarget * 8)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantOuterR * Mathf.Sqrt((float)rnd.NextDouble()); // uniform-area over the disc
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                if (InSpawnClearing(x, z, 0f)) continue;
                if (!OnLandmass(x, z)) continue;
                if (OnBareMountain(x, z)) continue;                    // no forest on the snow-cap flank
                float inlandT = Mathf.InverseLerp(plantOuterR, 0f, rr);
                if (rnd.NextDouble() > Mathf.Clamp01(0.45f + inlandT * 0.55f)) continue;
                bool tall = rnd.NextDouble() < 0.55f;
                float scale = tall ? (1.5f + (float)rnd.NextDouble() * 0.9f)
                                   : (1.0f + (float)rnd.NextDouble() * 0.6f);
                BuildTree(parent, GroundPoint(groundCol, x, z), scale, rnd, tall);
                treesPlaced++;
            }

            // ---- ROCK OUTCROPS (clustered boulders, biased to the higher inland ground) ----
            int rockTarget = Mathf.Max(20, treeTarget / 5), rocksPlaced = 0, rockGuard = 0;
            while (rocksPlaced < rockTarget && rockGuard++ < rockTarget * 8)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantOuterR * Mathf.Sqrt((float)rnd.NextDouble());
                float cxp = Mathf.Cos(ang) * rr, czp = Mathf.Sin(ang) * rr;
                if (InSpawnClearing(cxp, czp, 8f)) continue;
                int n = 2 + rnd.Next(0, 3);
                for (int i = 0; i < n && rocksPlaced < rockTarget; i++)
                {
                    float x = cxp + (float)(rnd.NextDouble() - 0.5) * 7f;
                    float z = czp + (float)(rnd.NextDouble() - 0.5) * 7f;
                    if (!OnLandmass(x, z)) continue;
                    float scale = 0.7f + (float)rnd.NextDouble() * 1.6f;
                    BuildRock(parent, GroundPoint(groundCol, x, z), scale, rnd);
                    rocksPlaced++;
                }
            }

            // ---- GRASS TUFTS (dense interior ground cover, sparse at the coast) ----
            int clumpTarget = Mathf.Max(120, treeTarget), clumpsPlaced = 0, clumpGuard = 0;
            while (clumpsPlaced < clumpTarget && clumpGuard++ < clumpTarget * 6)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantOuterR * Mathf.Sqrt((float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                if (!OnLandmass(x, z)) continue;
                if (OnBareMountain(x, z)) continue;                    // no grass on the bare/snow flank
                float inlandT = Mathf.InverseLerp(plantOuterR, 0f, rr);
                if (rnd.NextDouble() > Mathf.Clamp01(0.25f + inlandT * 0.7f)) continue;
                BuildGrassClump(parent, GroundPoint(groundCol, x, z),
                    0.6f + (float)rnd.NextDouble() * 0.6f, rnd);
                clumpsPlaced++;
            }

            // ---- ROCKY WALLS + HUGE STONE SLABS (island 2.0-B / C2, ticket 86cakk4w8) ----
            // FREE-STANDING FacetedRock hero PROPS scattered onto the settled 600u land — DISTINCT from C1's
            // per-peak terrain rock-banding (untouched). NEW seed streams (seed+1111 / seed+1212) so C1's
            // tree/rock/grass streams above are BYTE-UNTOUCHED (a re-run reproduces the soaked C1 scatter
            // exactly). Both classes reject footprints overlapping the three Peaks[] feet (walls/slabs go on
            // flats/foreshore/inter-peak cols, never clipped into a mountain foot) + the spawn clearing.
            int wallsPlaced = ScatterWalls(parent, seed, groundCol, plantOuterR, OnLandmass, InSpawnClearing);
            int slabsPlaced = ScatterSlabs(parent, seed, groundCol, plantOuterR, OnLandmass, InSpawnClearing);

            Debug.Log($"[poc-trace] Scatter: {treesPlaced} trees (target {treeTarget}), {rocksPlaced} rocks, " +
                      $"{clumpsPlaced} grass clumps, {wallsPlaced} rocky walls, {slabsPlaced} stone slabs — seed {seed}");
        }

        // Reject a footprint that overlaps ANY of C1's three peaks (hero 90,-60 r300; NE 330,150 r200; SE
        // 250,-285 r160) — plus a margin for the prop's own base — so a wall/slab is never clipped into a
        // mountain foot (which would read broken + risk orphaning the walkable surface). Keyed off Peaks[]
        // directly (no hardcoded centres) so a peak re-tune re-rejects automatically.
        static bool OverlapsAnyPeakFoot(float x, float z, float margin)
        {
            foreach (var p in NextIslandPocGen.Peaks)
            {
                float dx = x - p.cx, dz = z - p.cz;
                if (dx * dx + dz * dz < (p.footR + margin) * (p.footR + margin)) return true;
            }
            return false;
        }

        // 3-6 WIDE faceted ROCKY WALLS (default — Sponsor-soak tunes). seed+1111 (a NEW stream — never mutate
        // C1's seed+555 above). Returns the count placed (for the trace + the Predict-Before-Soak grade). Each
        // candidate is REJECTED where the local footprint slope is too steep (a wide wall conformed onto a steep
        // slope would bury one end ugly) or the footprint spills over water — the guard-loop retries elsewhere.
        static int ScatterWalls(GameObject parent, int seed, MeshCollider groundCol, float plantOuterR,
            System.Func<float, float, bool> onLandmass, System.Func<float, float, float, bool> inSpawnClearing)
        {
            var rnd = new System.Random(seed + 1111);
            int target = 3 + rnd.Next(0, 4);                 // 3..6
            int placed = 0, guard = 0;
            while (placed < target && guard++ < target * 60)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantOuterR * Mathf.Sqrt((float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                if (inSpawnClearing(x, z, 14f)) continue;
                if (!onLandmass(x, z)) continue;
                if (OverlapsAnyPeakFoot(x, z, 6f)) continue;
                if (BuildWall(parent, groundCol, x, z, rnd, placed)) placed++;
            }
            return placed;
        }

        // 8-15 huge flat-topped STONE SLABS (default — Sponsor-soak tunes), scale ~3-6, some partly embedded.
        // seed+1212 (a NEW stream, disjoint from walls' seed+1111 and C1's seed+555). Same slope/water reject so
        // a slab is seated ON the ground (its downhill underside contacts terrain), never floating on a slope.
        static int ScatterSlabs(GameObject parent, int seed, MeshCollider groundCol, float plantOuterR,
            System.Func<float, float, bool> onLandmass, System.Func<float, float, float, bool> inSpawnClearing)
        {
            var rnd = new System.Random(seed + 1212);
            int target = 8 + rnd.Next(0, 8);                 // 8..15
            int placed = 0, guard = 0;
            while (placed < target && guard++ < target * 60)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantOuterR * Mathf.Sqrt((float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                if (inSpawnClearing(x, z, 12f)) continue;
                if (!onLandmass(x, z)) continue;
                if (OverlapsAnyPeakFoot(x, z, 4f)) continue;
                if (BuildSlab(parent, groundCol, x, z, rnd, placed)) placed++;
            }
            return placed;
        }

        // Raycast a point down onto the ground collider (grounds the prop base on the sloped terrain).
        static Vector3 GroundPoint(MeshCollider groundCol, float x, float z)
        {
            if (groundCol != null)
            {
                var ray = new Ray(new Vector3(x, 300f, z), Vector3.down);
                if (groundCol.Raycast(ray, out RaycastHit hit, 600f))
                    return hit.point;
            }
            return new Vector3(x, 0f, z);
        }

        // Terrain height (world Y) at a world XZ via a straight-down ray onto the ground collider. Returns
        // float.NaN when the ray misses (off the landmass / over water) so callers can reject footprints that
        // spill past the coast (a wall half over the sea would float over water).
        static float GroundY(MeshCollider groundCol, float x, float z)
        {
            if (groundCol != null)
            {
                var ray = new Ray(new Vector3(x, 300f, z), Vector3.down);
                if (groundCol.Raycast(ray, out RaycastHit hit, 600f)) return hit.point.y;
            }
            return float.NaN;
        }

        // Is the terrain under a `radius` footprint at (cx,cz) gentle enough to seat a feature cleanly? Samples
        // an 8-point ring + the centre; rejects if any sample misses terrain (footprint spills over water) or if
        // the terrain spread across the footprint exceeds `maxDrop` (too steep — conforming would bury one end).
        // This is the "REJECT placements where local slope exceeds a threshold" arm of the grounding fix.
        static bool FootprintSlopeOk(MeshCollider groundCol, float cx, float cz, float radius, float maxDrop)
        {
            if (groundCol == null) return true;
            float lo = float.MaxValue, hi = float.MinValue;
            float cy = GroundY(groundCol, cx, cz);
            if (float.IsNaN(cy)) return false;
            lo = Mathf.Min(lo, cy); hi = Mathf.Max(hi, cy);
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                float gy = GroundY(groundCol, cx + Mathf.Cos(a) * radius, cz + Mathf.Sin(a) * radius);
                if (float.IsNaN(gy)) return false;             // footprint edge off the landmass → reject
                lo = Mathf.Min(lo, gy); hi = Mathf.Max(hi, gy);
            }
            return (hi - lo) <= maxDrop;
        }

        // A low-poly tree: tapered trunk + a blob canopy (multi-value greens via vertex colour), a
        // NavMeshObstacle carve so the agent paths around it, static-batched. Mirrors LowPolyZoneGen.BuildTree
        // (the reused primitive), self-contained so the start-island file stays byte-untouched.
        static void BuildTree(GameObject parent, Vector3 at, float scale, System.Random rnd, bool tall)
        {
            var tree = new GameObject("LP_Tree");
            tree.transform.SetParent(parent.transform, false);
            tree.transform.position = at;
            float yaw = (float)rnd.NextDouble() * 360f;
            tree.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            tree.transform.localScale = new Vector3(scale, scale, scale);

            float trunkH = tall ? (3.6f + (float)rnd.NextDouble() * 1.4f) : 1.6f;
            float botR = tall ? 0.22f : 0.18f, topR = 0.12f;
            var trunk = MakeMeshObject(tree, "Trunk", LowPolyMeshes.TaperedCylinder(botR, topR, trunkH, 6), TrunkMat());
            trunk.transform.localPosition = Vector3.zero;

            int blobCount = tall ? (5 + rnd.Next(0, 3)) : (4 + rnd.Next(0, 3));
            float canopyR = tall ? 1.55f : 1.15f;
            var canopy = MakeMeshObject(tree, "Canopy",
                LowPolyMeshes.BlobCanopy(canopyR, blobCount, CanopyBody, CanopyTop, CanopyShadow, rnd.Next()),
                CanopyMat());
            canopy.transform.localPosition = new Vector3(0f, trunkH + (tall ? 0.9f : 0.55f), 0f);

            var obstacle = tree.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
            obstacle.radius = botR + 0.12f / Mathf.Max(0.0001f, scale); // snug collar on the stem at every scale
            obstacle.height = trunkH + 1.6f;
            obstacle.center = new Vector3(0f, (trunkH + 1.6f) * 0.5f, 0f);

            MarkStaticBatch(tree);
        }

        static void BuildRock(GameObject parent, Vector3 at, float scale, System.Random rnd)
        {
            var rock = new GameObject("LP_Rock");
            rock.transform.SetParent(parent.transform, false);
            rock.transform.position = at;
            rock.transform.rotation = Quaternion.Euler(
                (float)rnd.NextDouble() * 10f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 10f);
            rock.transform.localScale = Vector3.one * scale;
            MakeMeshObject(rock, "RockMesh",
                LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: rnd.Next()), RockMat());
            MarkStaticBatch(rock);
        }

        static void BuildGrassClump(GameObject parent, Vector3 at, float scale, System.Random rnd)
        {
            var clump = new GameObject("LP_Grass");
            clump.transform.SetParent(parent.transform, false);
            clump.transform.position = at;
            clump.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            clump.transform.localScale = Vector3.one * scale;
            var go = MakeMeshObject(clump, "GrassMesh",
                LowPolyMeshes.GrassClump(0.7f, 7, rnd.Next()), GrassMat());
            // Grass reads a fixed GRASS-GREEN _Tint (the mesh carries no colour); a mid leaf green.
            MarkStaticBatch(clump);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // A WIDE faceted ROCKY WALL (island 2.0-B / C2) — a FacetedRock stretched into a broad near-vertical rock
        // FACE: WIDTH dominates (18..40u run), a moderate HEIGHT (9..16u), and a THIN depth (4..6.5u) => a rock
        // wall/face you can't walk up, NOT a needle/shard (the pre-fix 5×24×7 read as a monolith slice; the
        // real-world anchor is that a wall is WIDE relative to its height — lowpoly-quality.md §0). Keeps the
        // FacetedRock idiom (flat-shaded facets + per-facet vertex-colour, NEVER RecalculateNormals). Hero feature
        // => shadows ON + static-batch + carving NavMeshObstacle (off-NavMesh, no orphan). Rejects (returns false)
        // where the local footprint slope is too steep / spills over water so the guard-loop retries elsewhere.
        static bool BuildWall(GameObject parent, MeshCollider groundCol, float cx, float cz, System.Random rnd, int idx)
        {
            float wide = 18f + (float)rnd.NextDouble() * 22f;   // 18..40 — the DOMINANT width (a long rock face)
            float hgt  = 9f + (float)rnd.NextDouble() * 7f;     // 9..16 — moderate rise (< width => a WALL not a spike)
            float thk  = 4f + (float)rnd.NextDouble() * 2.5f;   // 4..6.5 — thin depth (< height => near-vertical face)
            // Slope/water reject BEFORE building: SeatConform already grounds the wall on any slope, so this only
            // needs to reject GENUINELY steep / cliff-edge / over-water footprints (where conforming would bury one
            // end deep). Generous maxDrop keyed to the height so rolling-hill interior stays plentiful (no starve).
            if (!FootprintSlopeOk(groundCol, cx, cz, wide * 0.5f, hgt * 0.85f + 2f)) return false;

            var wall = new GameObject("LP_RockWall");
            wall.transform.SetParent(parent.transform, false);
            float yaw  = (float)rnd.NextDouble() * 360f;
            float tilt = ((float)rnd.NextDouble() - 0.5f) * 6f; // +/-3deg organic lean (stays near-vertical)
            wall.transform.rotation = Quaternion.Euler(tilt, yaw, tilt * 0.5f);
            wall.transform.localScale = new Vector3(wide, hgt, thk);

            var mesh = LowPolyMeshes.FacetedRock(0.5f, jitter: 0.42f, seed: rnd.Next());
            var body = MakeMeshObject(wall, "WallMesh", mesh, WallMat(), castShadows: true);

            SeatConform(wall, body, groundCol, cx, cz, embed: 0.5f + (float)rnd.NextDouble() * 0.6f);
            GroundingTrace(body, groundCol, cx, cz, "wall", idx);
            AddCarveObstacle(wall, mesh);
            MarkStaticBatch(wall);
            return true;
        }

        // A huge flat-topped STONE SLAB (island 2.0-B / C2) — a FacetedRock spread WIDE + FLATTENED so it reads as
        // a big boulder sitting ON the ground with a broad flat top (scale ~3-6). ~40% are PARTLY EMBEDDED (the
        // ticket's "some part-embedded"). Same hero caster policy (shadows ON + static-batch) + carving obstacle.
        // Grounding CONFORMS to the local slope (SeatConform) so the downhill underside contacts terrain — the
        // pre-fix single-centre-point seat floated the seaward half on a slope (the poc_slab_side saucer defect).
        static bool BuildSlab(GameObject parent, MeshCollider groundCol, float cx, float cz, System.Random rnd, int idx)
        {
            float s    = 3f + (float)rnd.NextDouble() * 3f;             // 3..6 base scale (huge boulder)
            float wide = s * (1.3f + (float)rnd.NextDouble() * 0.7f);   // wide footprint
            float deep = s * (1.3f + (float)rnd.NextDouble() * 0.7f);
            float flat = s * (0.40f + (float)rnd.NextDouble() * 0.35f); // FLATTENED Y => a flat-topped slab
            // Slope/water reject: a big flat slab on a steep slope can't sit flat (it would float one edge or bury
            // the other); reject and retry on gentler ground. maxDrop ~ the slab's own flattened height.
            if (!FootprintSlopeOk(groundCol, cx, cz, Mathf.Max(wide, deep) * 0.5f, flat * 0.9f + 0.5f)) return false;

            var slab = new GameObject("LP_StoneSlab");
            slab.transform.SetParent(parent.transform, false);
            float yaw  = (float)rnd.NextDouble() * 360f;
            float tilt = ((float)rnd.NextDouble() - 0.5f) * 10f;       // +/-5deg — a boulder resting at an angle
            slab.transform.rotation = Quaternion.Euler(tilt, yaw, tilt * 0.6f);
            slab.transform.localScale = new Vector3(wide, flat, deep);

            var mesh = LowPolyMeshes.FacetedRock(0.5f, jitter: 0.34f, seed: rnd.Next());
            var body = MakeMeshObject(slab, "SlabMesh", mesh, SlabMat(), castShadows: true);

            float worldH = body.GetComponent<MeshRenderer>().bounds.size.y;
            bool embedded = rnd.NextDouble() < 0.4;
            float embed = embedded ? worldH * (0.30f + (float)rnd.NextDouble() * 0.25f) : 0.3f;
            SeatConform(slab, body, groundCol, cx, cz, embed);
            GroundingTrace(body, groundCol, cx, cz, "slab", idx);
            AddCarveObstacle(slab, mesh);
            MarkStaticBatch(slab);
            return true;
        }

        // Seat a rock feature CONFORMED to the local slope: sample the terrain across the feature's WORLD footprint
        // (centre + an 8-point ring at the footprint half-extents) and seat the base to the LOWEST sampled ground
        // (minus `embed`). This guarantees the DOWNHILL underside contacts terrain — no air gap under the seaward
        // half (the pre-fix single-centre seat floated the downhill half on a slope: the poc_slab_side saucer).
        // The uphill side embeds into the slope, which reads as a grounded boulder. x/z stay at the scatter point.
        static void SeatConform(GameObject root, GameObject body, MeshCollider groundCol, float cx, float cz, float embed)
        {
            root.transform.position = new Vector3(cx, 0f, cz);
            var mr = body.GetComponent<MeshRenderer>();
            Bounds b = mr.bounds;                              // world AABB after rotation + non-uniform scale
            float rx = b.size.x * 0.5f, rz = b.size.z * 0.5f;
            float minG = float.MaxValue;
            float cy = GroundY(groundCol, cx, cz);
            if (!float.IsNaN(cy)) minG = cy;
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                float gy = GroundY(groundCol, cx + Mathf.Cos(a) * rx, cz + Mathf.Sin(a) * rz);
                if (!float.IsNaN(gy) && gy < minG) minG = gy;
            }
            if (minG == float.MaxValue) minG = 0f;             // fully off-collider fallback (should not happen post-reject)
            float bottom = mr.bounds.min.y;                    // current world underside (root just placed at y=0)
            root.transform.position += new Vector3(0f, (minG - embed) - bottom, 0f);
        }

        // Per-feature GROUNDING TRACE (ticket 86cakk4w8 pre-soak fix): after seating, sample the terrain at the
        // feature's centre + an 8-point footprint ring and report the MAX air gap = (underside − terrainY) over
        // those points. A positive max means the underside FLOATS above terrain somewhere (the defect); the
        // SeatConform seat drives it firmly negative (underside at/under the lowest footprint ground). Printed
        // for EVERY placed wall/slab so a regression re-floats loudly in the shipped-exe log.
        static void GroundingTrace(GameObject body, MeshCollider groundCol, float cx, float cz, string label, int idx)
        {
            Bounds b = body.GetComponent<MeshRenderer>().bounds;
            float underside = b.min.y;
            float rx = b.size.x * 0.5f, rz = b.size.z * 0.5f;
            float maxAirGap = float.MinValue; int samples = 0;
            void Sample(float gx, float gz)
            {
                float gy = GroundY(groundCol, gx, gz);
                if (float.IsNaN(gy)) return;
                float airGap = underside - gy;                 // >0 => underside ABOVE terrain => FLOATS here
                if (airGap > maxAirGap) maxAirGap = airGap;
                samples++;
            }
            Sample(cx, cz);
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                Sample(cx + Mathf.Cos(a) * rx, cz + Mathf.Sin(a) * rz);
            }
            Debug.Log($"[poc-trace] C2 grounding {label}#{idx} @({cx:F0},{cz:F0}): underside={underside:F2}u " +
                      $"maxAirGap={maxAirGap:F2}u over {samples} footprint pts (>0 => FLOATS; must be <=0 — grounded).");
        }

        // A carving NavMeshObstacle sized to the feature footprint — the SAME idiom the trees use (BuildTree). The
        // baked PocNavMesh.asset is UNCHANGED (obstacles carve at RUNTIME over the baked surface), so the bake-time
        // NavMesh-coverage trace stays green. Carving only SUBTRACTS the footprint => a free-standing wall/slab can
        // NEVER orphan a walkable patch (it creates no walkable island). The NavMeshAgent player cannot enter the
        // footprint => can't walk up a vertical wall / through a boulder; a vertical jump can't cross it either (the
        // agent owns world XZ — CastawayCharacter). Box in LOCAL mesh units; the root lossyScale scales it to world.
        static void AddCarveObstacle(GameObject root, Mesh mesh)
        {
            var obs = root.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obs.carving = true;
            obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obs.center = mesh.bounds.center;
            obs.size = mesh.bounds.size;
        }

        static void MarkStaticBatch(GameObject go) =>
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(go,
                UnityEditor.StaticEditorFlags.BatchingStatic);

        static GameObject MakeMeshObject(GameObject parent, string name, Mesh mesh, Material mat, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            // Caster policy (C2): hero silhouette features (walls, big slabs) keep shadows; decoration-scale debris
            // ships castShadows:false (the shadow pass is the dominant GPU cost — poly-plan headline). Default true
            // preserves the existing tree/rock On behaviour; grass keeps its explicit post-create Off below.
            mr.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // 24-step quantizer (mirrors LowPolyZoneGen.QuantizeFine) — snaps a near-neutral warm-grey tint to the fine
        // grid so it preserves R>=G>=B and never pink-casts (the coarse 12-step grid collapsed R≈G≈B to R>G=B;
        // lowpoly-quality.md §1 Rec 1), while collapsing tints to a few shared materials (SRP-Batcher-friendly).
        static Color QuantizeFine(Color c)
        {
            const float steps = 24f;
            return new Color(
                Mathf.Round(c.r * steps) / steps,
                Mathf.Round(c.g * steps) / steps,
                Mathf.Round(c.b * steps) / steps, 1f);
        }

        static Material CanopyMat() => _canopyMat ??= VertexColorMat("LPPocCanopyMat", Color.white, sway: true);
        static Material RockMat()   => _rockMat   ??= VertexColorMat("LPPocRockMat", RockCol, sway: false);
        static Material GrassMat()  => _grassMat  ??= VertexColorMat("LPPocGrassMat", new Color(0.36f, 0.54f, 0.24f), sway: false);
        // ONE shared material per hero-rock class (T-A: tonal variation is vertex-colour only, never per-material) —
        // near-neutral warm-grey tints routed through QuantizeFine so they never pink-cast (lowpoly-quality §1).
        static Material WallMat()   => _wallMat   ??= VertexColorMat("LPPocWallMat", QuantizeFine(WallCol), sway: false);
        static Material SlabMat()   => _slabMat   ??= VertexColorMat("LPPocSlabMat", QuantizeFine(SlabCol), sway: false);

        static Material TrunkMat()
        {
            if (_trunkMat != null) return _trunkMat;
            _trunkMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "LPPocTrunkMat" };
            _trunkMat.SetColor("_BaseColor", TrunkCol);
            _trunkMat.SetFloat("_Smoothness", 0.06f);
            return _trunkMat;
        }

        // Shared vertex-color material on the FarHorizon/LowPolyVertexColor shader (the one canopy/rock/grass
        // use). The mesh bakes its colours into per-vertex COLOR; _Tint multiplies (white = unmodified). Falls
        // back to a flat URP/Lit (never magenta) if unresolved. Canopy opts into the wind-sway term.
        static Material VertexColorMat(string name, Color tint, bool sway)
        {
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            Material mat;
            if (vc != null)
            {
                mat = new Material(vc) { name = name };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", tint);
                if (sway)
                {
                    if (mat.HasProperty("_SwayAmp"))   mat.SetFloat("_SwayAmp", 0.10f);
                    if (mat.HasProperty("_SwayLen"))   mat.SetFloat("_SwayLen", 4f);
                    if (mat.HasProperty("_SwaySpeed")) mat.SetFloat("_SwaySpeed", 1f);
                }
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                mat.SetColor("_BaseColor", tint);
                mat.SetFloat("_Smoothness", 0.06f);
            }
            return mat;
        }
    }
}
