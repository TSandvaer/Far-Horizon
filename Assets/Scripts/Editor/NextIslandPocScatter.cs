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

        static Material _canopyMat, _trunkMat, _rockMat, _grassMat;

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
            _canopyMat = _trunkMat = _rockMat = _grassMat = null;

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
            // a bare-rock crag). Below ~45% of a peak's OWN height the flank is gentle grass/lower-rock →
            // trees are fine; above that it is steep bare rock → no trees. PER-PEAK fraction (86cahwx6w) so
            // the tree line scales to each massif — the same 0.45 line the Sponsor approved on the hero.
            bool OnBareMountain(float x, float z) =>
                NextIslandPocGen.MountainHeightFracAt(x, z) > 0.45f;

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

            Debug.Log($"[poc-trace] Scatter: {treesPlaced} trees (target {treeTarget}), {rocksPlaced} rocks, " +
                      $"{clumpsPlaced} grass clumps — seed {seed}");
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

        static void MarkStaticBatch(GameObject go) =>
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(go,
                UnityEditor.StaticEditorFlags.BatchingStatic);

        static GameObject MakeMeshObject(GameObject parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Material CanopyMat() => _canopyMat ??= VertexColorMat("LPPocCanopyMat", Color.white, sway: true);
        static Material RockMat()   => _rockMat   ??= VertexColorMat("LPPocRockMat", RockCol, sway: false);
        static Material GrassMat()  => _grassMat  ??= VertexColorMat("LPPocGrassMat", new Color(0.36f, 0.54f, 0.24f), sway: false);

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
