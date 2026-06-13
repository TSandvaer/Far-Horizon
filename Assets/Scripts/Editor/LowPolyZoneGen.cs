using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// PROCEDURAL low-poly smooth-shaded terrain + mesh scatter for the production environment.
    /// Ported from the eval spike (EmbergraveUnitySlice, Assets/Scripts/Editor/LowPolyZoneGen.cs,
    /// iter-5/8 — READ-ONLY working spec per ticket 86ca86fux). This is the Sponsor-approved
    /// "Zone D" look (verbatim: "i love zone D + quality") built as the ACTUAL play space the
    /// character moves through — NOT a side-by-side A/B/C/D comparison vignette like the spike had.
    ///
    /// WHY PROCEDURAL (not a CC0 pack): the spike permitted procedural ("procedural is fine if
    /// headless pack-import is clunky"). FBX/GLB pack import in -batchmode needs per-mesh import
    /// settings (normals mode, smoothing angle, material assignment) that are GUI-shaped and fragile
    /// to drive headlessly. Procedural welded meshes give deterministic, reproducible-from-code
    /// geometry AND let us control the smooth-shading directly: a WELDED mesh (shared verts at face
    /// seams) + Mesh.RecalculateNormals() averages vertex normals across faces, which IS the
    /// technical definition of "low-poly with smooth shading" (averaged vertex normals, NOT
    /// flat/faceted per-face normals). The Sponsor judges the LOOK; the source is an implementation
    /// detail a CC0/ArtDrop path can replace later with no scene-shape change.
    ///
    /// PALETTE: WARM / LUSH per art-direction.md + inspiration/*.png (sunny survival POC) — sand=warm
    /// tan, field=warm harmonious greens, water=warm-leaning teal. All sub-1.0 / HDR-safe. This is
    /// the warm/lush direction, NOT the retired "dark sinister iso building" direction.
    /// </summary>
    public static class LowPolyZoneGen
    {
        // ---- WARM / LUSH palette (sub-1.0, per art-direction.md survival north-star) ----
        // FIRST-FRAME WARMTH TUNE (ticket 86ca8ce7j, absorbs pale-shore 86ca8a0u6): the shipped first
        // frame (capture_00, stamp 659e8d1) read WASHED-OUT — the player spawns on the sand band and the
        // pale sun-bleached SandHi (0.90,0.83,0.62) + warm fog + bloom + postExposure compounded into a
        // near-white cream wash. Pull the sand toward a warmer, less-blown tan (more saturated, lower
        // value) so the shore reads warm-golden, not bleached. The board (21h13_31) shore is a warm
        // amber-tan, not paper-white. Greens pushed slightly more saturated to ride the board's
        // saturated-but-warm rule. All still sub-1.0 / HDR-safe.
        static readonly Color SandLo = new Color(0.74f, 0.62f, 0.40f); // warm golden tan (was .78/.69/.49)
        static readonly Color SandHi = new Color(0.82f, 0.71f, 0.47f); // sunlit warm sand, NOT bleached (was .90/.83/.62)
        static readonly Color SandDamp = new Color(0.60f, 0.50f, 0.34f); // damp shore sand (warmer)
        static readonly Color GrassLo = new Color(0.30f, 0.48f, 0.20f); // mid leaf green (more saturated)
        static readonly Color GrassHi = new Color(0.48f, 0.64f, 0.28f); // sunlit grass
        static readonly Color GrassRise = new Color(0.38f, 0.56f, 0.24f); // meadow rise
        static readonly Color RockCol = new Color(0.55f, 0.52f, 0.47f);  // warm grey stone
        static readonly Color TrunkCol = new Color(0.42f, 0.30f, 0.19f); // warm bark
        static readonly Color LeafLo = new Color(0.26f, 0.42f, 0.20f);   // canopy shadow (legacy)
        static readonly Color LeafHi = new Color(0.44f, 0.58f, 0.28f);   // canopy lit (legacy)

        // ---- BLOB-CANOPY greens (board v2, ticket 86ca8ce7j) — the "3-4 green values per tree" rule.
        // Anchors from team/uma-ux/style-guide-v2.md §6 (derived by eye from inspiration/21h11_03 +
        // 21h10_44): vivid mid-green body, brighter top-lit green, deep shadow-side green. Saturated
        // but warm-leaning + sub-1.0 (HDR-safe). These are the multi-VALUE clustering that makes the
        // overlapping blobs read as foliage, not a single green ball. Tuned slightly warmer/softer than
        // the raw guide swatches so they sit in the warm Zone-D key without reading neon.
        static readonly Color CanopyBody   = new Color(0.30f, 0.58f, 0.24f); // vivid mid-green body
        static readonly Color CanopyTop    = new Color(0.48f, 0.74f, 0.34f); // bright top-lit green
        static readonly Color CanopyShadow = new Color(0.18f, 0.40f, 0.17f); // deep shadow-side green

        // ---- BEACH OCEAN water gradient (drew/beach-water-scene; Uma beach-water-direction §1).
        // Toy-bright teal that catches the sun — calm/inviting, NOT a realistic reflective shader-ocean.
        // The gradient is baked per-VERTEX (near-shore bright -> seaward deeper) and rides the existing
        // FarHorizon/LowPolyVertexColor shader, so the water reads SMOOTH against the FACETED shore
        // (Uma: the contrast is what makes the coast pop). All channels sub-1.0 — the Zone-D post stack
        // (bloom + warm grade + postExposure) compounds bright values; sub-1.0 survives without blooming
        // to white (the pale-shore first-frame lesson). Anchors derived by eye from inspiration/21h16_52
        // (lake-cabin) + Tess-QA-pinnable values in the brief's §1 color table. ----
        // BRIGHTENED + saturated (drew/ocean-camera-fix, 2026-06-13). The first soak-fix anchors
        // (#3FA6B0 shallow / #2E7E96 deep) shipped technically-teal but read as a PALE GREY strip in the
        // shipped seaward gameplay view (pixel-sampled the capture: the visible far water came out
        // (0.66,0.62,0.44) — warm grey-yellow, NOT teal). Root cause (trace + math): the visible water is
        // FAR (the near bright band is occluded by the beach crest), so it gets (a) the warm directional
        // key + warm SH ambient in the LowPolyVertexColor shader, (b) the warm distance fog (0.80,0.80,
        // 0.74), and (c) the warm post grade (WhiteBalance +12, warm colour filter) — three warm washes
        // that desaturate the original mid-value teal to grey. Pushing the teal BRIGHTER and more
        // saturated (higher G+B, B>G slightly, low R) makes it survive all three and land visibly teal in
        // the SHIPPED frame. Still sub-1.0 every channel (HDR-clamp-safe — verified against the post stack
        // in the shipped seaward capture, not just the editor).
        public static readonly Color WaterShallow = new Color(0.20f, 0.72f, 0.80f); // bright sunlit teal shallows
        public static readonly Color WaterDeep    = new Color(0.16f, 0.60f, 0.72f); // saturated seaward teal
        // Foam edge line baked into the beach mesh's seaward-most rows (Uma §2). Warm off-white, sub-1.0
        // (NOT pure white — would bloom). Exposed for the scene-presence test's color-pin check.
        public static readonly Color FoamEdge     = new Color(0.91f, 0.89f, 0.82f); // #E8E2D0 warm foam

        // Result of building the low-poly zone: the ground GameObject (Ground-layered, NavMesh +
        // raycast surface) so the caller can parent scatter + bake NavMesh over it.
        public class ZoneResult
        {
            public GameObject root;        // zone container
            public GameObject ground;      // the terrain mesh (on Ground layer, has MeshCollider)
        }

        /// <summary>
        /// Build a low-poly smooth-shaded zone: a height-varied terrain MESH (dunes near the shore
        /// rising to a gentle meadow inland), vertex-color gradient material (beach->field ramp by
        /// height + Z), low-poly mesh scatter (trees / rocks / grass clumps), and a simple water plane
        /// at the beach edge. Smooth normals via welded grid + RecalculateNormals.
        ///
        /// PORT NOTE: the spike's per-edge SEAM-FLATTENING (pulling X-edge columns to y=0 so a flat
        /// NavMesh connector could mate the side-by-side zones) is INTENTIONALLY DROPPED here — there
        /// is no neighbouring zone to stitch to in the single production play space, so the interior
        /// dunes + meadow rise are the whole surface. Everything else is carried verbatim.
        /// </summary>
        public static ZoneResult BuildZone(
            GameObject parent, string zoneId, float minX, float maxX,
            float shoreZ, float inlandFarZ, int seed, Material vertexColorMat, Material waterMat)
        {
            var root = new GameObject("Zone_" + zoneId);
            root.transform.SetParent(parent.transform, false);

            float cx = (minX + maxX) * 0.5f;
            float width = maxX - minX;
            float depth = inlandFarZ - shoreZ;
            float cz = (shoreZ + inlandFarZ) * 0.5f;

            var ground = BuildTerrainMesh(root, "Ground_" + zoneId, vertexColorMat,
                new Vector3(cx, 0f, cz), width, depth, shoreZ, inlandFarZ, seed);

            var scatterRoot = new GameObject("LowPolyScatter");
            scatterRoot.transform.SetParent(root.transform, false);
            ScatterLowPolyProps(scatterRoot, minX, maxX, shoreZ, inlandFarZ, seed,
                ground.GetComponent<MeshCollider>());

            BuildWaterEdge(root, "Water_" + zoneId, waterMat, cx, width, shoreZ);

            return new ZoneResult { root = root, ground = ground };
        }

        // ---- Terrain mesh: subdivided welded plane with gentle height (dunes -> meadow rise) ----
        // Height profile along Z: flat beach near the shore, then a gentle dune ripple, then a
        // smooth rise into the inland meadow. Plus low-amplitude multi-octave noise so the surface
        // is organic (no flat tabletop). Vertex colors ramp sand->grass by height+Z so the material
        // reads beach->field WITHOUT a texture (URP/Lit with vertex color is unreliable; we use a
        // tiny vertex-color shader material — see WorldBootstrap.MakeTerrainVertexColorMaterial).
        const int SegX = 40, SegZ = 56; // enough subdivision for smooth slopes + clean NavMesh
        static GameObject BuildTerrainMesh(GameObject parent, string name, Material mat,
            Vector3 center, float width, float depth, float shoreZ, float inlandFarZ, int seed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = center;
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) go.layer = groundLayer; // -1 if the project lacks a Ground layer

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            var rnd = new System.Random(seed);
            float ox = (float)rnd.NextDouble() * 100f, oz = (float)rnd.NextDouble() * 100f;

            int vCount = (SegX + 1) * (SegZ + 1);
            var verts = new Vector3[vCount];
            var cols = new Color[vCount];
            for (int z = 0; z <= SegZ; z++)
            for (int x = 0; x <= SegX; x++)
            {
                int i = z * (SegX + 1) + x;
                float fx = (float)x / SegX, fz = (float)z / SegZ;
                float worldZ = Mathf.Lerp(shoreZ, inlandFarZ, fz);
                float localX = (fx - 0.5f) * width;
                float localZ = worldZ - center.z;

                float h = HeightAt(fz, fx, ox, oz);
                verts[i] = new Vector3(localX, h, localZ);
                cols[i] = GroundColorAt(fz, h, fx, seed);
            }

            var tris = new int[SegX * SegZ * 6];
            int ti = 0;
            for (int z = 0; z < SegZ; z++)
            for (int x = 0; x < SegX; x++)
            {
                int i = z * (SegX + 1) + x;
                tris[ti++] = i; tris[ti++] = i + SegX + 1; tris[ti++] = i + 1;
                tris[ti++] = i + 1; tris[ti++] = i + SegX + 1; tris[ti++] = i + SegX + 2;
            }

            var mesh = new Mesh { name = name + "_mesh" };
            mesh.indexFormat = vCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = tris;
            // SMOOTH SHADING: the grid is WELDED (each interior vertex is shared by its surrounding
            // faces), so RecalculateNormals AVERAGES the face normals into smooth vertex normals —
            // the technical "low-poly with smooth shading" look (vs a hard-edged per-face split).
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh; // NavMesh bakes on the actual sloped surface

            return go;
        }

        // Gentle height field. fz=0 shore .. fz=1 deep inland. Returns a small world-Y so the
        // NavMesh agent (walkable slope) can traverse it. Kept low-amplitude so slopes stay bakeable
        // (NavMesh default max slope ~45deg) and the character reads grounded on it.
        static float HeightAt(float fz, float fx, float ox, float oz)
        {
            // BEACH SLOPES DOWN INTO THE SEA (drew/ocean-camera-fix, 2026-06-13). The prior profile put a
            // beach hump (~Y0.39 at Z+2) BETWEEN the locked spawn (Z+6) and the water (near-edge Z-10.5),
            // so the seaward orbit looked OVER a dune at a distant fogged sea strip — the ocean was
            // structurally occluded (shipped-capture pixel-sample: zero teal reached the seaward frame).
            // The fix: the shore band now DIPS BELOW sea level near fz=0 (a gentle beach that slopes down
            // into the water, the inspiration/21h16_52 read) and rises smoothly to the meadow only past the
            // spawn — so from the spawn you look slightly DOWN-shore onto open water, no occluding crest.
            // shoreDip < 0 near the shore so the beach passes below WaterY (-0.20) and the coastline reads
            // as land dipping into the sea (Uma §2). The meadow rise (inland of the spawn) is unchanged.
            // A single MONOTONIC ramp from the underwater shore (fz=0, ~Y-0.55) up to the spawn-band
            // beach level (~fz0.27, ~Y0.0) — NO intermediate hump between the spawn and the water, so the
            // seaward view looks slightly DOWN-shore onto open water with nothing occluding it. The meadow
            // rise begins only INLAND of the spawn (past fz0.30) so the journey-forward still climbs.
            float beachRamp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.0f, 0.27f, fz)); // 0 at shore -> 1 at spawn band
            float shoreDip = Mathf.Lerp(-0.55f, 0.02f, beachRamp);                          // underwater shore up to ~flat beach at spawn
            float dune = Mathf.Sin(fz * 9f) * 0.06f * beachRamp * (1f - fz);                // very soft ripple on the dry beach only
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.95f, fz)) * 1.6f; // meadow rise (inland of spawn)
            // organic low-amplitude noise (multi-octave) so nothing is a flat tabletop
            float n = (Mathf.PerlinNoise(ox + fx * 4f, oz + fz * 4f) - 0.5f) * 0.5f
                    + (Mathf.PerlinNoise(ox + fx * 9f, oz + fz * 9f) - 0.5f) * 0.22f;
            // shoreDip is the monotonic beach ramp (underwater shore -> ~flat at the spawn band); rise is
            // the inland meadow; noise is suppressed at the shore (clean waterline) and full inland.
            return shoreDip + dune + rise + n * (0.10f + beachRamp * 0.20f + rise * 0.4f);
        }

        // Vertex color ramps warm sand (shore, low) -> warm grass (inland, higher). Multi-tone so
        // no big region is one flat color (carries the spike's "no flat single-color ground" bar into
        // the low-poly look). Slight per-vertex hash jitter keeps facets reading distinct.
        static Color GroundColorAt(float fz, float height, float fx, int seed)
        {
            // sand near the shore, blending to grass past the transition band
            float grassT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.42f, fz));
            Color sand = Color.Lerp(SandDamp, Color.Lerp(SandLo, SandHi, Mathf.Clamp01(fz * 3f)),
                                    Mathf.Clamp01(fz * 4f));
            Color grass = Color.Lerp(GrassLo, GrassHi, Mathf.Clamp01(grassT));
            // higher ground reads as the sunlit meadow rise
            grass = Color.Lerp(grass, GrassRise, Mathf.Clamp01((height - 0.6f) * 0.5f));
            Color c = Color.Lerp(sand, grass, grassT);

            // FOAM BAND (drew/beach-water-scene; Uma §2 task D): the seaward-most rows of the beach mesh
            // (fz≈0, where the sloping sand passes below the water plane) carry the warm off-white foam
            // line — a single calm stylized waterline, baked into vertex color so it rides this same
            // terrain shader (no new object, no particles — Uma: "a single calm foam line is the entire
            // treatment"). A narrow seaward band: strongest at the very edge (fz=0), gone by fz~0.06.
            // Sub-1.0 foam (NOT pure white) so the post stack's bloom doesn't blow it out.
            float foamT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.055f, 0.0f, fz));
            c = Color.Lerp(c, FoamEdge, foamT);

            // per-vertex value jitter so adjacent facets differ slightly (alive, not flat)
            float j = (Hash01(Mathf.RoundToInt(fx * 997f), Mathf.RoundToInt(fz * 991f), seed) - 0.5f) * 0.10f;
            c.r = Mathf.Clamp01(c.r + j); c.g = Mathf.Clamp01(c.g + j); c.b = Mathf.Clamp01(c.b + j);
            c.a = 1f;
            return c;
        }

        // ---- Low-poly mesh scatter: trees, rocks, grass clumps (all welded smooth-shaded meshes) ----
        static void ScatterLowPolyProps(GameObject parent, float minX, float maxX,
            float shoreZ, float inlandFarZ, int seed, MeshCollider groundCol)
        {
            var rnd = new System.Random(seed + 555);
            // FIRST-FRAME TUNE (86ca8ce7j, absorbs pale-shore 86ca8a0u6): the shipped first frame read
            // washed-out because the player spawns at origin (~Z0, the sand band) and the tree band only
            // started at the 0.40 lerp (~Z+15) — every tree was BEHIND/above the orbit camera's framing,
            // so the frame was empty pale sand with zero warm/lush content. Bring the field band toward
            // the shore (0.28 lerp, ~Z+7) so blob canopies fill the spawn frame's mid-ground, and add a
            // dedicated NEAR-SPAWN hero cluster off to the sides (clear of the loop spots at origin) so
            // the very first frame reads "a warm wooded shore", not a bleached desert. Trees carve the
            // NavMesh (the player still paths around them), so density near spawn stays readable.
            float fieldStartZ = Mathf.Lerp(shoreZ, inlandFarZ, 0.28f);

            // Trees — clustered inland. Density scales gently with the (wider) production extents.
            int treeClusters = Mathf.RoundToInt(7f * Mathf.Clamp((maxX - minX) / 43f, 1f, 3f));
            for (int c = 0; c < treeClusters; c++)
            {
                float cxp = Mathf.Lerp(minX + 3f, maxX - 3f, (float)rnd.NextDouble());
                float czp = Mathf.Lerp(fieldStartZ, inlandFarZ - 3f, (float)rnd.NextDouble());
                int n = 1 + rnd.Next(0, 3);
                for (int i = 0; i < n; i++)
                {
                    float x = cxp + (float)(rnd.NextDouble() - 0.5) * 5f;
                    float z = czp + (float)(rnd.NextDouble() - 0.5) * 5f;
                    if (x < minX + 1f || x > maxX - 1f) continue;
                    BuildTree(parent, GroundPoint(groundCol, x, z), 0.85f + (float)rnd.NextDouble() * 0.5f,
                        rnd, true);
                }
            }

            // NEAR-SPAWN hero trees — trees framed into the first gameplay frame, spread to BOTH sides
            // and across the inland arc around the spawn (0,0,6) so the warm-wooded-shore read is
            // BALANCED (not clustered to one corner), while staying clear of the loop spots (craft 8,6 /
            // tree -9,-7 / fire 4,-8). Z ~ +11..+18 (the grass band ahead of the spawn) so they sit on
            // green ground in the orbit camera's inland view; a couple wider out frame the edges.
            (float x, float z)[] heroSpots =
            {
                (-13f, 12f), (13f, 12f),   // near, both sides of the inland view
                (-20f, 16f), (20f, 16f),   // mid, framing the edges
                (-5f, 18f),  (6f, 17f),    // a pair ahead, flanking the journey forward
            };
            foreach (var (hx, hz) in heroSpots)
            {
                if (hx < minX + 1f || hx > maxX - 1f) continue;
                BuildTree(parent, GroundPoint(groundCol, hx, hz), 1.0f + (float)rnd.NextDouble() * 0.45f,
                    rnd, true);
            }

            // Rocks — both the beach (near shore) and the field. Smaller near shore.
            int rocks = Mathf.RoundToInt(22f * Mathf.Clamp((maxX - minX) / 43f, 1f, 3f));
            for (int i = 0; i < rocks; i++)
            {
                float x = Mathf.Lerp(minX + 1.5f, maxX - 1.5f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(shoreZ + 1.5f, inlandFarZ - 2f, (float)rnd.NextDouble());
                float scale = 0.4f + (float)rnd.NextDouble() * 0.7f;
                BuildRock(parent, GroundPoint(groundCol, x, z), scale, rnd);
            }

            // Grass clumps — low spiky low-poly tufts, dense in the field, sparse toward the shore.
            int clumps = Mathf.RoundToInt(60f * Mathf.Clamp((maxX - minX) / 43f, 1f, 3f));
            for (int i = 0; i < clumps; i++)
            {
                float x = Mathf.Lerp(minX + 1f, maxX - 1f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(shoreZ + 4f, inlandFarZ - 1f, (float)rnd.NextDouble());
                float fz = Mathf.InverseLerp(shoreZ, inlandFarZ, z);
                // density follows the field gradient (probabilistic accept)
                if (rnd.NextDouble() > Mathf.Clamp01((fz - 0.30f) * 1.6f)) continue;
                BuildGrassClump(parent, GroundPoint(groundCol, x, z),
                    0.5f + (float)rnd.NextDouble() * 0.4f, rnd);
            }
        }

        // Raycast straight down onto the terrain collider to find the surface Y at (x,z) so props
        // sit ON the sloped ground (not floating / buried). Falls back to y=0 if the ray misses.
        static Vector3 GroundPoint(MeshCollider groundCol, float x, float z)
        {
            if (groundCol != null)
            {
                var ray = new Ray(new Vector3(x, 50f, z), Vector3.down);
                if (groundCol.Raycast(ray, out RaycastHit hit, 200f))
                    return hit.point;
            }
            return new Vector3(x, 0f, z);
        }

        // A low-poly tree: tapered trunk (welded cylinder, few sides) + a faceted icosphere-ish
        // canopy (welded, smooth-normaled). Optional NavMeshObstacle carve so the agent paths around.
        static void BuildTree(GameObject parent, Vector3 at, float scale, System.Random rnd, bool carve)
        {
            var tree = new GameObject("LP_Tree");
            tree.transform.SetParent(parent.transform, false);
            tree.transform.position = at;
            tree.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            tree.transform.localScale = Vector3.one * scale;

            float trunkH = 1.6f;
            var trunk = MakeMeshObject(tree, "Trunk", LowPolyMeshes.TaperedCylinder(0.18f, 0.12f, trunkH, 6),
                MakeFlatColorMat(TrunkCol, "LPTrunkMat"));
            trunk.transform.localPosition = Vector3.zero;

            // BLOB CANOPY (board v2): a CLUSTER of overlapping faceted spheroids in multi-VALUE greens
            // (CanopyShadow/Body/Top baked per-blob into vertex color), NOT a single smooth dome. 4-6
            // blobs per tree reads like inspiration/21h11_03's four variants. Solid volumes -> sidesteps
            // the thin-foliage normal trap (unity-conventions.md). One shared vertex-color material
            // renders all blobs' greens (no per-blob material churn).
            int blobCount = 4 + rnd.Next(0, 3); // 4-6
            var canopy = MakeMeshObject(tree, "Canopy",
                LowPolyMeshes.BlobCanopy(1.15f, blobCount, CanopyBody, CanopyTop, CanopyShadow, rnd.Next()),
                CanopyVertexColorMat());
            canopy.transform.localPosition = new Vector3(0f, trunkH + 0.55f, 0f);

            if (carve)
            {
                var obstacle = tree.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
                obstacle.radius = 0.4f;
                obstacle.height = 2.6f;
                obstacle.center = new Vector3(0f, 1.3f, 0f);
            }
        }

        static void BuildRock(GameObject parent, Vector3 at, float scale, System.Random rnd)
        {
            var rock = new GameObject("LP_Rock");
            rock.transform.SetParent(parent.transform, false);
            rock.transform.position = at;
            rock.transform.rotation = Quaternion.Euler(
                (float)rnd.NextDouble() * 20f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 20f);
            rock.transform.localScale = new Vector3(scale, scale * (0.6f + (float)rnd.NextDouble() * 0.4f), scale);
            MakeMeshObject(rock, "RockMesh",
                LowPolyMeshes.FacetedSphere(0.6f, 0, jitter: 0.30f, seed: rnd.Next()),
                MakeFlatColorMat(RockCol * (0.85f + (float)rnd.NextDouble() * 0.3f), "LPRockMat"));
        }

        // Grass tufts (the iter-8 dark-shard FIX lives in LowPolyMeshes.GrassClump — up-biased normals
        // both faces). Blade tint is pulled toward the LIT grass end of the terrain ramp (GrassHi) so
        // the tuft reads brighter than the ground it sits on (a tuft buried in same-tone ground
        // disappears).
        static void BuildGrassClump(GameObject parent, Vector3 at, float scale, System.Random rnd)
        {
            var clump = new GameObject("LP_GrassClump");
            clump.transform.SetParent(parent.transform, false);
            clump.transform.position = at;
            clump.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            clump.transform.localScale = Vector3.one * scale;
            // Bias toward GrassHi (sunlit) so the tuft pops off the ground ramp instead of blending in.
            Color blade = Color.Lerp(GrassLo, GrassHi, 0.55f + (float)rnd.NextDouble() * 0.45f);
            MakeMeshObject(clump, "Blades",
                LowPolyMeshes.GrassClump(0.55f, 7, rnd.Next()),
                MakeFlatColorMat(blade, "LPGrassMat"));
        }

        static GameObject MakeMeshObject(GameObject parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        // ---- The beach OCEAN (drew/beach-water-scene; Uma beach-water-direction §1-2) ----
        // SUPERSEDES the original 10x10 Unity-primitive Plane (single-tone over-glossy teal, scale.z=4,
        // tucked at shoreZ-14 — a small dim sheet that ended in a HARD plane-edge inside the fog and read
        // as "a puddle", not "the sea the castaway washed in from"). Diagnostic trace (drew/beach-water,
        // 2026-06-13) confirmed the water DID ship in Boot.unity but at pos(0,-0.25,-26) scale(9.9,1,4),
        // meshColors=0 (a primitive Plane carries no vertex colors so the gradient shader had nothing to
        // ride), smoothness 0.88 — invisible-as-a-beach for all those reasons, not absent.
        //
        // Now a WELDED SUBDIVIDED GRID so (1) the near->far teal vertex-color gradient has verts to
        // interpolate across, (2) the in-shader swell has verts to displace, and (3) RecalculateNormals
        // averages to the SMOOTH water sheet that pops against the faceted shore. Extends FAR seaward
        // (well past the fog line) so its far edge dissolves into the warm haze — the "far horizon" the
        // game points at — never a visible plane-edge. Sits just below the shore's lowest verts so the
        // beach's faceted edge dips INTO the water (the coastline is where the sloping beach passes below
        // the water Y, no hard seam). Editor-time authored + serialized (NOT Awake) — the swell is the
        // only animated part and it runs in the shader, so zero runtime geometry construction.
        const float WaterY = -0.20f;          // just under the shore's ~ -0.07 min Y -> beach dips in
        const float WaterSeawardDepth = 220f; // extends ~220u seaward of the shore -> lost in fog
        // Near edge sits a touch INLAND of the Zone-D terrain shore (shoreZ=-12) so the beach's faceted
        // edge dips INTO the water with no gap seam, but SEAWARD of the trimmed TestGround placeholder
        // (SeawardGroundZ=-10) so the flat slab never overhangs + occludes the sea (the diagnostic's
        // occlusion finding — drew/beach-water, 2026-06-13). With overlap 1.5, near edge = shoreZ+1.5 =
        // -10.5: just seaward of TestGround's -10 edge, just inland of the terrain shore. No surface gap.
        const float WaterInlandOverlap = 1.5f;
        const int WaterSegX = 24, WaterSegZ = 40; // enough verts for a smooth gradient + swell
        static void BuildWaterEdge(GameObject parent, string name, Material waterMat,
            float cx, float width, float shoreZ)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.layer = 0; // not walkable / not NavMesh (no collider added at all)
            go.transform.position = new Vector3(cx, WaterY, 0f);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            // Water is a flat sheet — it should not cast shadows onto itself / the shore (a low-poly
            // sea doesn't self-shadow), and receiving the shore's shadow keeps the near band grounded.
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Span: WIDER than the land (so the sea wraps past the coast at the edges of the frame) and
            // from a little inland of the shore (overlap, tucked under the beach) out to the deep sea.
            float waterWidth = width * 1.6f;
            float nearZ = shoreZ + WaterInlandOverlap;          // a touch inland of the shore
            float farZ = shoreZ - WaterSeawardDepth;            // far out to sea (lost in fog)

            int vCount = (WaterSegX + 1) * (WaterSegZ + 1);
            var verts = new Vector3[vCount];
            var cols = new Color[vCount];
            for (int z = 0; z <= WaterSegZ; z++)
            for (int x = 0; x <= WaterSegX; x++)
            {
                int i = z * (WaterSegX + 1) + x;
                float fx = (float)x / WaterSegX, fz = (float)z / WaterSegZ;
                float worldZ = Mathf.Lerp(nearZ, farZ, fz); // fz=0 near shore, fz=1 deep sea
                float localX = (fx - 0.5f) * waterWidth;
                verts[i] = new Vector3(localX, 0f, worldZ); // local origin at (cx, WaterY, 0)
                // Near-shore BRIGHT shallows dominate the band the eye actually sees, fading to deeper
                // teal only FAR out. Keyed off WORLD Z (a fixed bright band off the coast) rather than
                // the 0..1 grid fraction, because the grid runs 220u deep — a 0..1 ease would put the
                // deep teal across most of the near water, and the distance fog + warm post grade already
                // desaturate the far water, so without a WIDE bright band the visible sea reads grey, not
                // the toy-bright teal the shore needs (shipped-capture finding, drew/beach-water).
                // WIDENED 70u->130u (drew/ocean-camera-fix): when the camera tilts down to the horizon
                // (the now-allowed flat pitch) the upper frame is dominated by the 50-150u mid-sea, not
                // the very-near band — so the bright teal must extend further out to keep the SEA reading
                // teal across the frame before it dissolves into the warm horizon haze (OceanCameraDiag
                // confirmed the old 70u band let the warm-graded deep teal read grey beyond ~Z-60).
                float seawardDist = nearZ - worldZ; // 0 at the coast, grows out to sea
                float depthT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seawardDist / 130f));
                Color c = Color.Lerp(WaterShallow, WaterDeep, depthT);
                c.a = 1f;
                cols[i] = c;
            }

            var tris = new int[WaterSegX * WaterSegZ * 6];
            int ti = 0;
            for (int z = 0; z < WaterSegZ; z++)
            for (int x = 0; x < WaterSegX; x++)
            {
                int i = z * (WaterSegX + 1) + x;
                tris[ti++] = i; tris[ti++] = i + WaterSegX + 1; tris[ti++] = i + 1;
                tris[ti++] = i + 1; tris[ti++] = i + WaterSegX + 1; tris[ti++] = i + WaterSegX + 2;
            }

            var mesh = new Mesh { name = name + "_mesh" };
            mesh.indexFormat = vCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = tris;
            // WELDED grid -> RecalculateNormals averages to a smooth flat-up sheet (the smooth-water read
            // that contrasts the faceted shore). The swell is applied in the shader, not baked here, so
            // the serialized mesh is the calm rest pose.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }

        // ---- Materials ----
        // A flat-color URP/Lit material (smooth shading reads via the averaged vertex normals on the
        // mesh, not via texture). Colors are QUANTIZED to a coarse palette grid before keying so the
        // cache collapses the per-instance jitter into a SMALL number of shared materials (a few
        // greens/greys/browns) instead of one .mat per unique jittered color — avoiding asset churn.
        // The materials are NOT persisted as standalone .mat assets; assigned to sharedMaterial they
        // serialize INLINE into the saved scene (works in the standalone build). Cached per-bootstrap.
        static readonly Dictionary<string, Material> _flatCache = new Dictionary<string, Material>();
        static Material MakeFlatColorMat(Color c, string baseName)
        {
            Color q = Quantize(c);
            string key = baseName + "_" + ColorKey(q);
            if (_flatCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = key;
            mat.SetColor("_BaseColor", q);
            mat.SetFloat("_Smoothness", 0.06f); // matte: low-poly reads by shape + shading, not gloss
            _flatCache[key] = mat;
            return mat;
        }

        // The blob canopies bake their multi-value greens into per-vertex COLOR, so they need the
        // vertex-color shader (URP/Lit IGNORES vertex color — unity-conventions.md). One shared, cached,
        // INLINE material (no .mat asset) renders every tree's canopy; the shader is already registered
        // in AlwaysIncludedShaders by WorldBootstrap so it doesn't strip in the standalone build. _Tint
        // stays white so the vertex greens come through unmodified. Falls back to a single mid-green
        // URP/Lit (vertex color lost, but never magenta/broken) if the shader is somehow unresolved.
        static Material _canopyMat;
        static Material CanopyVertexColorMat()
        {
            if (_canopyMat != null) return _canopyMat;
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vc != null)
            {
                _canopyMat = new Material(vc) { name = "LPBlobCanopyMat" };
                if (_canopyMat.HasProperty("_Tint")) _canopyMat.SetColor("_Tint", Color.white);
            }
            else
            {
                _canopyMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "LPBlobCanopyMat" };
                _canopyMat.SetColor("_BaseColor", CanopyBody);
                _canopyMat.SetFloat("_Smoothness", 0.06f);
                Debug.LogWarning("[LowPolyZoneGen] vertex-color shader not found; blob canopy falls back to flat green");
            }
            return _canopyMat;
        }

        // Clear the per-bootstrap material cache so a re-run does not return materials owned by a
        // destroyed scene (the editor keeps the static cache across executeMethod invocations).
        public static void ResetMaterialCache() { _flatCache.Clear(); _canopyMat = null; }

        // Snap each channel to a coarse 12-step grid so jittered colors collapse into a small set.
        static Color Quantize(Color c)
        {
            const float steps = 12f;
            return new Color(
                Mathf.Round(c.r * steps) / steps,
                Mathf.Round(c.g * steps) / steps,
                Mathf.Round(c.b * steps) / steps, 1f);
        }

        static string ColorKey(Color c) =>
            Mathf.RoundToInt(c.r * 255) + "_" + Mathf.RoundToInt(c.g * 255) + "_" + Mathf.RoundToInt(c.b * 255);

        // Vertex-color terrain material: URP/Lit does NOT multiply vertex color, so for LIT smooth
        // shading WITH vertex color we use the tiny custom shader shipped in Assets/Shaders
        // (LowPolyVertexColor.shader). If that shader resolves, use it; else fall back to URP/Lit
        // white (still smooth-shaded, just single-tone) so the build never breaks. The shader is
        // registered in AlwaysIncludedShaders by WorldBootstrap so the standalone build does not
        // strip it (the spike's magenta lesson).
        public static Material MakeTerrainVertexColorMaterial(string assetPath)
        {
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            Material mat;
            if (vc != null)
            {
                mat = new Material(vc);
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.06f);
                Debug.LogWarning("[LowPolyZoneGen] vertex-color shader not found; terrain falls back to white URP/Lit");
            }
            AssetDatabase.CreateAsset(mat, assetPath);
            return mat;
        }

        // The beach-ocean material (drew/beach-water-scene; Uma §1 task A). RIDES the existing
        // FarHorizon/LowPolyVertexColor shader (the same one canopy + terrain use) so the near->far teal
        // gradient baked into the water grid's vertex colors actually renders — URP/Lit IGNORES vertex
        // color, so the old URP/Lit water shipped single-tone (the diagnostic confirmed meshColors=0 was
        // moot anyway because the primitive Plane had none). The vertex-color shader is a flat diffuse +
        // SH-ambient lit model: the "moderate gloss / catches the warm sky" read (Uma's smoothness ~0.6
        // intent) comes from that soft lit shading + the bright sub-1.0 teal, NOT a mirror specular — a
        // high-gloss reflective water would break the toy (Uma §1: pull the gloss DOWN from 0.88). The
        // shader is registered in AlwaysIncludedShaders by WorldBootstrap/MovementCameraScene so it does
        // not strip. _WaveAmp > 0 turns ON the in-shader swell for the WATER ONLY (default 0 elsewhere).
        // Falls back to a sub-1.0 teal URP/Lit (gradient + swell lost, but never magenta) if unresolved.
        public static Material MakeWaterMaterial(string assetPath)
        {
            Material mat;
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vc != null)
            {
                mat = new Material(vc) { name = "LowPolyWaterMat" };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Color.white); // vertex teal unmodified
                // Gentle large-wavelength swell (Uma §1: "a breath, not surf"). amp ~0.06u, long
                // wavelength, slow. The shader displaces Y in-vertex; nothing runs per-frame on the CPU.
                if (mat.HasProperty("_WaveAmp")) mat.SetFloat("_WaveAmp", 0.06f);
                if (mat.HasProperty("_WaveLen")) mat.SetFloat("_WaveLen", 16f);
                if (mat.HasProperty("_WaveSpeed")) mat.SetFloat("_WaveSpeed", 0.8f);
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "LowPolyWaterMat" };
                mat.SetColor("_BaseColor", WaterShallow); // sub-1.0 bright teal (gradient lost in fallback)
                mat.SetFloat("_Smoothness", 0.6f);
                mat.SetFloat("_Metallic", 0.0f);
                Debug.LogWarning("[LowPolyZoneGen] vertex-color shader not found; water falls back to flat teal URP/Lit (no gradient, no swell)");
            }
            AssetDatabase.CreateAsset(mat, assetPath);
            return mat;
        }

        static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                int h = seed;
                h = h * 73856093 ^ x * 19349663 ^ y * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h & 0x7fffffff) % 100000) / 100000f;
            }
        }
    }
}
