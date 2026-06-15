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
        // ROCK-AS-BOULDER soak-fix (86ca8m5zu, Sponsor soak 5f7e7ba: "items sticking up... all over").
        // The old warm-grey RockCol (0.55,0.52,0.47) silhouetted DARK under the Zone-D warm fog + grade at
        // small scatter size — a grey-blob spike read. Lifted to a warmer, LIGHTER sun-bleached stone so the
        // boulder reads as warm rock catching the key, not a dark shard: R>G>B warm lean, value pulled up so
        // it sits bright against the warm fog (0.80,0.80,0.74) instead of crushing to a dark silhouette.
        // Still sub-1.0 / HDR-clamp-safe (the per-instance *0.85..1.15 jitter in BuildRock keeps the top of
        // the band under 1.0). The board-v2 grey rocks (21h10_44) read as light warm stone, not charcoal.
        // v2 verify-capture + TINTDIAG instrumentation: the rocks read PINK because the coarse 12-step
        // material quantizer SPLIT the warm-grey ramp — R rounded UP across the 0.625 step while G/B rounded
        // DOWN, yielding (0.667, 0.583, 0.583) = R>G=B = a pink cast. The fix is NOT the base colour (any
        // base near 0.6 hits the same step boundary) — it's to quantize the ROCK tint on a FINER 24-step grid
        // (RockVertexColorMat -> QuantizeFine) that preserves the R>=G>=B warm-grey order. With this base on
        // the fine grid, TINTDIAG shows all tints stay warm-grey (no R>G=B pink), 4 distinct mats (low churn).
        static readonly Color RockCol = new Color(0.62f, 0.60f, 0.555f); // warm stone-grey (fine-quantized; no pink split)
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
        // NEAR-BAND SATURATION LIFT (drew/ocean-beach-soakfix2, 2026-06-13). Sponsor soaked main and
        // flagged the NEAR band as a PALE SKY-CYAN, not a believable teal sea. Pixel-sampled the shipped
        // seaward capture (ci-out/sea-caps/sea_seaward.png, stamp 3a548a7) to confirm + diagnose:
        //   near-band albedo (56,219,242)  ->  on-frame (148,164,145)   [a pale grey-green]
        //   teal saturation G+B-2R:  albedo 349  ->  on-frame 13  (~96% of the chroma destroyed)
        // Per-channel the post stack pushes R UP ~+90 (warm directional key * ndotl + warm SH ambient +
        // warm fog 0.80/0.80/0.74 + warm grade), and crushes B DOWN ~-97. The PRIOR fix chased this by
        // pushing the albedo BRIGHTER + more B-dominant (0.22/0.86/0.95) — exactly backwards: a
        // TOP-of-band B-dominant cyan is what BLOOMS to pale grey under the Zone-D bloom + warm grade, so
        // brighter made it MORE washed-out, not more teal. The lever is the OPPOSITE: DEEPEN (pull the
        // value down off the top of the band so bloom adds rather than blows out) + SATURATE (lower R hard
        // so the warm push lands controlled, keep G/B mid-high with B>=G so the warm-B-crush still leaves
        // a clean GREEN-LEANING teal). Tuned empirically against the shipped seaward capture (N>=2 builds,
        // pixel-sampled the near band each time) to land on-frame ~(70,160,165) — the saturated mid-value
        // teal the inspiration/21h16_52 lake + 21h16_13 river read at, NOT a pale near-white cyan. Still
        // sub-1.0 every channel (HDR-clamp-safe). The FAR band (WaterDeep) + foam are unchanged — this is
        // a near-band saturation lift, not a re-tune of the whole sea (AC1 scope).
        public static readonly Color WaterShallow = new Color(0.10f, 0.62f, 0.66f); // deep saturated near-shore teal
        public static readonly Color WaterDeep    = new Color(0.10f, 0.50f, 0.60f); // deeper seaward teal-blue (far band)
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

        // ============================================================================================
        // BIG ROUND ISLAND (ticket 86ca9a7qn — Sponsor: "make the island much much bigger, round, with
        // water on all sides. ... quite big with elevation (hills). dense forest/jungle with high trees").
        // ============================================================================================
        // THE NEW WORLD BASIS. The old beach-to-meadow STRIP (a 1D Z-profile over an X±45 / Z -12..56
        // rectangle, water on the −Z side only) is replaced by a RADIAL-FALLOFF round landmass centred at
        // the world origin (= the spawn + survival-loop centre), with water on ALL sides. The mountains
        // move OFF the main island onto their own distant islands ringing the sea (WorldBootstrap.BuildVista,
        // pushed clear of the bigger island).
        //
        // RADIAL HEIGHT MODEL (HeightAtRadial below):
        //   r = distance from origin in the XZ plane.
        //   - r <  IslandCoreR  : the land PLATEAU (full inland height + hills).
        //   - IslandCoreR..IslandShoreR : a smooth falloff ring (the land slopes down to the coast).
        //   - r ~  IslandShoreR : the WATERLINE (the radial coast — true round, water on all sides).
        //   - r >  IslandShoreR : the seabed drops below sea level (the surrounding sea).
        // Hills are multi-octave Perlin noise added over the plateau, amplitude ramped DOWN toward the
        // coast (a clean waterline) and UP inland (real elevation). The result is a believable round
        // island with rolling hills, not a flat disc.
        //
        // SIZE: IslandShoreR 120u → ~240u diameter, MUCH bigger than the old ~90×68 strip (bold default
        // per the brief — the Sponsor dials from the capture). The terrain mesh is a square grid covering
        // the island disc + a sea margin; the water plane covers well past it to the fog horizon.
        public const float IslandCoreR  = 78f;   // inside this radius = the land plateau (full hills)
        public const float IslandShoreR = 120f;  // the radial waterline (the round coast) — ~240u diameter
        public const float IslandFalloffEnd = 150f; // seabed has fully dropped below the sea by here
        // The inland BASE surface sits at ~Y0 (so it agrees with the spawn at Y0, the survival-loop objects,
        // and the flat NavMesh proxy at the centre). HILLS rise ABOVE this base; the coast falls BELOW it.
        public const float IslandPlateauH = 0.15f; // base inland land height just above the sea (hills rise from here)
        public const float IslandHillAmp = 9.0f;   // peak hill amplitude inland (real elevation / hills — Sponsor)
        public const float IslandSeabedDrop = -9f; // how far the seabed sinks past the shore (well below WaterY)
        // The terrain grid covers the disc + a sea margin so the round coast reads against open water on
        // every side (the grid is square; the ROUNDNESS comes from the radial height, not the grid shape).
        public const float IslandGridHalf = 165f;  // square terrain grid half-extent (covers shore 120 + margin)
        const int IslandSegX = 120, IslandSegZ = 120; // subdivision across the big disc (smooth slopes + NavMesh)

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
            // BIG ROUND ISLAND (86ca9a7qn): the strip params (minX/maxX/shoreZ/inlandFarZ) are now the
            // LEGACY signature; the round island is sized by the Island* constants and centred at origin.
            // (Caller WorldBootstrap still passes the old extents — they're ignored for the island shape but
            // kept so the signature + any other call site stay source-compatible.)
            var root = new GameObject("Zone_" + zoneId);
            root.transform.SetParent(parent.transform, false);

            var ground = BuildIslandTerrainMesh(root, "Ground_" + zoneId, vertexColorMat, seed);

            var scatterRoot = new GameObject("LowPolyScatter");
            scatterRoot.transform.SetParent(root.transform, false);
            ScatterIslandProps(scatterRoot, seed, ground.GetComponent<MeshCollider>());

            BuildIslandWater(root, "Water_" + zoneId, waterMat, seed);

            return new ZoneResult { root = root, ground = ground };
        }

        // ============================================================================================
        // BIG ROUND ISLAND terrain mesh (86ca9a7qn). A square welded grid covering the island disc + a sea
        // margin; the ROUND landmass comes from the RADIAL height field (HeightAtRadial), not the grid shape.
        // WELDED + RecalculateNormals = the smooth-shaded low-poly look (averaged vertex normals).
        // ============================================================================================
        static GameObject BuildIslandTerrainMesh(GameObject parent, string name, Material mat, int seed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = Vector3.zero; // island centred at origin (= spawn / loop centre)
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) go.layer = groundLayer; // -1 if the project lacks a Ground layer

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            var rnd = new System.Random(seed);
            float ox = (float)rnd.NextDouble() * 100f, oz = (float)rnd.NextDouble() * 100f;

            int vCount = (IslandSegX + 1) * (IslandSegZ + 1);
            var verts = new Vector3[vCount];
            var cols = new Color[vCount];
            float size = IslandGridHalf * 2f;
            for (int z = 0; z <= IslandSegZ; z++)
            for (int x = 0; x <= IslandSegX; x++)
            {
                int i = z * (IslandSegX + 1) + x;
                float fx = (float)x / IslandSegX, fz = (float)z / IslandSegZ;
                float wx = (fx - 0.5f) * size;    // world X (local == world; root at origin)
                float wz = (fz - 0.5f) * size;    // world Z
                float h = HeightAtRadial(wx, wz, ox, oz);
                verts[i] = new Vector3(wx, h, wz);
                cols[i] = IslandColorAt(wx, wz, h, seed);
            }

            var tris = new int[IslandSegX * IslandSegZ * 6];
            int ti = 0;
            for (int z = 0; z < IslandSegZ; z++)
            for (int x = 0; x < IslandSegX; x++)
            {
                int i = z * (IslandSegX + 1) + x;
                tris[ti++] = i; tris[ti++] = i + IslandSegX + 1; tris[ti++] = i + 1;
                tris[ti++] = i + 1; tris[ti++] = i + IslandSegX + 1; tris[ti++] = i + IslandSegX + 2;
            }

            var mesh = new Mesh { name = name + "_mesh" };
            mesh.indexFormat = vCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.RecalculateNormals(); // welded grid -> averaged smooth normals (low-poly smooth shading)
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh; // NavMesh bakes on the actual sloped island surface

            return go;
        }

        // RADIAL ROUND-ISLAND HEIGHT FIELD (86ca9a7qn). World XZ -> world Y. The island is a radial
        // falloff centred at origin with multi-octave Perlin HILLS over the land:
        //   r < IslandCoreR              : land PLATEAU (full hills) — IslandPlateauH + hill noise
        //   IslandCoreR..IslandShoreR    : a smooth land→coast falloff (the round shore slopes down)
        //   r ~ IslandShoreR             : the WATERLINE (crosses WaterY ~ the radial coast)
        //   IslandShoreR..IslandFalloffEnd: the seabed drops to IslandSeabedDrop (well below WaterY)
        // The hill amplitude is ramped DOWN toward the coast (a clean round waterline) and UP inland
        // (real elevation). Public so the proof/geometry tests can sample it directly.
        public static float HeightAtRadial(float wx, float wz, float ox, float oz)
        {
            float r = Mathf.Sqrt(wx * wx + wz * wz);

            // Radial land profile: a plateau out to the core, then a SmoothStep falloff from the plateau
            // height down THROUGH THE WATERLINE (which lands EXACTLY at IslandShoreR — so the round coast,
            // the radial foam ring, and the wet-shelf all align at r==IslandShoreR) to the sunk seabed past
            // the shore. At r==IslandShoreR: landMask 0 + coastFall 0 -> baseH == WaterY (the waterline).
            float landMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(IslandCoreR, IslandShoreR, r));
            float coastFall = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(IslandShoreR, IslandFalloffEnd, r));
            float baseH = Mathf.Lerp(IslandPlateauH, WaterY, 1f - landMask)        // plateau -> waterline AT the shore
                        + Mathf.Lerp(0f, IslandSeabedDrop - WaterY, coastFall);    // past the shore, sink the seabed

            // HILLS: multi-octave Perlin elevation over the land. Amplitude ramped by landMask so hills are
            // full inland and fade to nothing at the coast (a clean waterline, no spiky shore). Noise is in
            // WORLD space (offset by ox/oz) so it's deterministic + continuous across the whole grid.
            float hill = (Mathf.PerlinNoise(ox + wx * 0.012f, oz + wz * 0.012f) - 0.5f) * 2f * 0.62f  // broad rolling hills
                       + (Mathf.PerlinNoise(ox + wx * 0.030f, oz + wz * 0.030f) - 0.5f) * 2f * 0.28f  // mid undulation
                       + (Mathf.PerlinNoise(ox + wx * 0.075f, oz + wz * 0.075f) - 0.5f) * 2f * 0.10f; // fine roughness
            // Bias hills upward (a ridge-line feel) + scale by amplitude, full on land, off at the coast.
            float hillH = (hill * 0.5f + 0.5f) * IslandHillAmp * landMask * landMask;

            // A SPAWN-FLATTEN: keep the immediate spawn + survival-loop centre gentle so the loop objects sit
            // on near-flat dry land (the loop reads + the click-move stays clean). The flatten holds fully out
            // to ~16u (covering the spawn (0,6) + loop spots) then eases the hills in over the next ~16u so the
            // jungle hills don't start abruptly at the clearing edge.
            float spawnFlat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(16f, 32f, r)); // 0 at clearing -> 1 past it
            hillH *= (0.06f + 0.94f * spawnFlat);

            return baseH + hillH;
        }

        // Island vertex colour by ELEVATION + radial position: a warm SAND ring at the coast, warm GRASS
        // over the land, and ROCK on the high hills (the elevation reads in colour, not just shape). A
        // radial FOAM ring at the waterline. Per-vertex jitter keeps facets distinct.
        static Color IslandColorAt(float wx, float wz, float height, int seed)
        {
            float r = Mathf.Sqrt(wx * wx + wz * wz);

            // SAND ring: a band just inland of the waterline (the beach). GRASS over the land interior.
            // ROCK on the high hilltops (elevation). Blend by radius for sand→grass, by height for grass→rock.
            float sandT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(IslandShoreR - 18f, IslandShoreR - 6f, r));
            float beachBlend = sandT; // 0 interior grass .. 1 coastal sand
            Color sand = Color.Lerp(SandLo, SandHi, Mathf.Clamp01((height + 0.2f) * 1.5f));
            Color grass = Color.Lerp(GrassLo, GrassHi, Mathf.Clamp01(Mathf.InverseLerp(IslandShoreR, 0f, r)));
            grass = Color.Lerp(grass, GrassRise, Mathf.Clamp01((height - 2.5f) * 0.18f)); // sunlit rise on hills
            // ROCK on the highest hilltops (above ~55% of the hill amplitude) — exposed stone reads elevation.
            Color rock = RockCol;
            float rockT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(IslandPlateauH + IslandHillAmp * 0.55f,
                                                                      IslandPlateauH + IslandHillAmp * 0.85f, height));
            Color land = Color.Lerp(grass, rock, rockT);
            Color c = Color.Lerp(land, sand, beachBlend);

            // RADIAL FOAM ring: a warm off-white band centred on the waterline (where the sloping land passes
            // below WaterY). Baked into the terrain's coastal verts so the wet sand edge meets the water mesh's
            // radial surf ring at the same coast (the soft wash, now all the way around the round coast).
            float foamDist = Mathf.Abs(r - IslandShoreR);
            float foamT = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((foamDist - 2.5f) / 4f));
            c = Color.Lerp(c, FoamEdge, foamT * 0.9f);

            // per-vertex value jitter so adjacent facets differ slightly (alive, not flat)
            float j = (Hash01(Mathf.RoundToInt(wx * 13f), Mathf.RoundToInt(wz * 13f), seed) - 0.5f) * 0.10f;
            c.r = Mathf.Clamp01(c.r + j); c.g = Mathf.Clamp01(c.g + j); c.b = Mathf.Clamp01(c.b + j);
            c.a = 1f;
            return c;
        }

        // ============================================================================================
        // BIG ROUND ISLAND scatter (86ca9a7qn): DENSE jungle with TALL trees across the whole land disc,
        // thinning at the coast; rock outcrops on the hills; grass tufts in the interior. Radial placement
        // (sample anywhere on the disc, accept by a land/coast probability), NOT the strip's inland-Z band.
        // Sponsor: "dense forest/jungle with high trees." Tree density + height pushed WAY up vs the old
        // sparse inland scatter, while keeping a near-spawn clearing so the survival loop reads.
        // ============================================================================================
        static void ScatterIslandProps(GameObject parent, int seed, MeshCollider groundCol)
        {
            var rnd = new System.Random(seed + 555);

            // The walkable/plant-able land disc: inside ~IslandShoreR-6 (the sand ring + grass + hills),
            // outside the spawn clearing. Tree placement samples the whole disc and accepts by a DENSITY
            // that rises from the coast inland (jungle interior dense, coastal fringe lighter).
            float plantR = IslandShoreR - 6f;     // outer plant radius (just inland of the sand ring)
            float spawnClearR = 13f;              // keep the survival-loop centre an open clearing

            // ---- DENSE TALL JUNGLE ----
            // Far more trees than the strip (~13 clusters → ~26 there). The island disc area is ~50× larger,
            // so a high target tree count reads as a real jungle, not a sparse scatter. Cap for perf headroom.
            int treeTarget = 320;     // dense jungle target (perf-measured; the showstopper risk #1)
            int treesPlaced = 0, treeGuard = 0;
            while (treesPlaced < treeTarget && treeGuard++ < treeTarget * 6)
            {
                // Uniform-area sample over the disc: r = plantR*sqrt(u) so trees aren't centre-biased.
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantR * Mathf.Sqrt((float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                if (rr < spawnClearR) continue;                 // open clearing at the loop centre
                // Density rises inland: accept more readily away from the coast (jungle interior dense).
                float inlandT = Mathf.InverseLerp(plantR, 0f, rr); // 0 coast .. 1 centre
                if (rnd.NextDouble() > Mathf.Clamp01(0.45f + inlandT * 0.55f)) continue;
                // TALL trees (Sponsor "high trees"): bigger base scale + a tall-jungle variant.
                bool tall = rnd.NextDouble() < 0.55f;
                float scale = tall ? (1.5f + (float)rnd.NextDouble() * 0.9f)   // tall jungle tree
                                   : (1.0f + (float)rnd.NextDouble() * 0.6f);  // mid blob tree
                BuildTree(parent, GroundPoint(groundCol, x, z), scale, rnd, true, tall);
                treesPlaced++;
            }

            // ---- ROCK OUTCROPS on the hills ----
            // Clustered boulders, biased toward the higher inland ground (the hill rock reads elevation).
            // Natural piles (2-4 each), spawn-clear, across the disc.
            int rockTarget = 60, rocksPlaced = 0, rockGuard = 0;
            while (rocksPlaced < rockTarget && rockGuard++ < rockTarget * 6)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = (plantR - 8f) * Mathf.Sqrt((float)rnd.NextDouble());
                float cxp = Mathf.Cos(ang) * rr, czp = Mathf.Sin(ang) * rr;
                if (rr < spawnClearR + 4f) continue;             // stay clear of the loop centre
                int n = 2 + rnd.Next(0, 3); // 2-4 boulders per outcrop
                for (int i = 0; i < n && rocksPlaced < rockTarget; i++)
                {
                    float x = cxp + (float)(rnd.NextDouble() - 0.5) * 3.6f;
                    float z = czp + (float)(rnd.NextDouble() - 0.5) * 3.6f;
                    if (Mathf.Sqrt(x * x + z * z) > plantR) continue;
                    float scale = 0.55f + (float)rnd.NextDouble() * 1.0f;
                    BuildRock(parent, GroundPoint(groundCol, x, z), scale, rnd);
                    rocksPlaced++;
                }
            }

            // ---- GRASS TUFTS — dense ground cover across the interior, sparse at the coast. ----
            int clumpTarget = 360, clumpsPlaced = 0, clumpGuard = 0;
            while (clumpsPlaced < clumpTarget && clumpGuard++ < clumpTarget * 5)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = plantR * Mathf.Sqrt((float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                float inlandT = Mathf.InverseLerp(plantR, 0f, rr);
                if (rnd.NextDouble() > Mathf.Clamp01(0.25f + inlandT * 0.7f)) continue;
                BuildGrassClump(parent, GroundPoint(groundCol, x, z),
                    0.5f + (float)rnd.NextDouble() * 0.5f, rnd);
                clumpsPlaced++;
            }

            Debug.Log($"[world-trace] ScatterIslandProps placed {treesPlaced} trees (dense tall jungle), " +
                      $"{rocksPlaced} rocks, {clumpsPlaced} grass tufts across the round island (plantR {plantR:F0}u)");
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
        // BIG ROUND ISLAND (86ca9a7qn): `tall` builds a TALL JUNGLE tree (long bare trunk + a high canopy)
        // for the "high trees" the Sponsor asked for; the default builds the original blob tree.
        static void BuildTree(GameObject parent, Vector3 at, float scale, System.Random rnd, bool carve,
            bool tall = false)
        {
            var tree = new GameObject("LP_Tree");
            tree.transform.SetParent(parent.transform, false);
            tree.transform.position = at;
            tree.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            tree.transform.localScale = Vector3.one * scale;

            // TALL JUNGLE: a much taller, slightly thinner trunk (the canopy rides high) so the forest reads
            // as HIGH trees from orbit. The mid tree keeps the original chunky proportions.
            float trunkH = tall ? (3.6f + (float)rnd.NextDouble() * 1.4f) : 1.6f;
            float botR = tall ? 0.22f : 0.18f, topR = 0.12f;
            var trunk = MakeMeshObject(tree, "Trunk", LowPolyMeshes.TaperedCylinder(botR, topR, trunkH, 6),
                MakeFlatColorMat(TrunkCol, "LPTrunkMat"));
            trunk.transform.localPosition = Vector3.zero;

            // BLOB CANOPY (board v2): a CLUSTER of overlapping faceted spheroids in multi-VALUE greens
            // (CanopyShadow/Body/Top baked per-blob into vertex color), NOT a single smooth dome. 4-6
            // blobs per tree reads like inspiration/21h11_03's four variants. Solid volumes -> sidesteps
            // the thin-foliage normal trap (unity-conventions.md). One shared vertex-color material
            // renders all blobs' greens (no per-blob material churn). Tall trees get a fuller crown.
            int blobCount = tall ? (5 + rnd.Next(0, 3)) : (4 + rnd.Next(0, 3));
            float canopyR = tall ? 1.55f : 1.15f;
            var canopy = MakeMeshObject(tree, "Canopy",
                LowPolyMeshes.BlobCanopy(canopyR, blobCount, CanopyBody, CanopyTop, CanopyShadow, rnd.Next()),
                CanopyVertexColorMat());
            canopy.transform.localPosition = new Vector3(0f, trunkH + (tall ? 0.9f : 0.55f), 0f);

            if (carve)
            {
                var obstacle = tree.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
                obstacle.radius = tall ? 0.5f : 0.4f;
                obstacle.height = trunkH + 1.6f;
                obstacle.center = new Vector3(0f, (trunkH + 1.6f) * 0.5f, 0f);
            }
        }

        static void BuildRock(GameObject parent, Vector3 at, float scale, System.Random rnd)
        {
            var rock = new GameObject("LP_Rock");
            rock.transform.SetParent(parent.transform, false);
            rock.transform.position = at;
            // FACETED STONE soak-fix2 (86ca8m5zu): both prior procedural shapes were rejected — the subdiv-0
            // octahedron read as a SPIKE, the subdiv-2 FacetedSphere read as a smooth dark MOUND (its weld
            // averages every facet into a continuous gradient — no stone read). The redo uses LowPolyMeshes
            // .FacetedRock: an ANGULAR, FLAT-SHADED (per-face normals) irregular chunk whose facets each catch
            // the key light at a different value — the board's (21h10_44) chunky-stone look. The mesh itself
            // carries the squat-chunky proportion + lumpiness, so the transform applies ONLY a yaw + a gentle
            // tilt (no thin Y-squash — that flattened it toward a mound). A light random uniform scale gives a
            // natural size range within an outcrop.
            rock.transform.rotation = Quaternion.Euler(
                (float)rnd.NextDouble() * 10f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 10f);
            rock.transform.localScale = Vector3.one * scale;
            // FacetedRock: subdiv-1 base (32 facets), anisotropic chunky displacement, FLAT face normals +
            // per-facet value baked to vertex colour. The warm-grey base goes in _Tint; the vertex value
            // (0.62 dark sides .. 1.0 light tops) multiplies onto it -> facet-to-facet stone value contrast.
            MakeMeshObject(rock, "RockMesh",
                LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: rnd.Next()),
                RockVertexColorMat(RockCol * (0.96f + (float)rnd.NextDouble() * 0.08f)));
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
        public const float WaterY = -0.20f;          // sea surface; the round coast is where the land dips below it
        // BIG ROUND ISLAND water (86ca9a7qn): the sea is now a LARGE SQUARE PLANE centred at origin covering
        // the island disc + the surrounding sea on ALL sides, out to the fog horizon — replacing the old
        // −Z-only seaward strip grid. The ROUND coast comes from the radial terrain dipping below WaterY at
        // IslandShoreR; the water plane just fills everything below that all the way around.
        public const float WaterHalfExtent = 700f;   // half-size of the square sea plane — reaches well past the
                                                     // island to the fog horizon on every side (sea-to-horizon, all sides)
        const int WaterSeg = 96;                      // subdivision so the radial foam ring + swell have verts
        // RADIAL SHORELINE FOAM ring (carries the accepted soft-wash foam, now ALL the way around the round
        // coast): a warm-white surf ring centred on the radial waterline (r == IslandShoreR), riding the
        // gentle coastal wet-shelf the swell laps. Near-dense ring resolution lands several rows in the band.
        const float WaterFoamCoreU = 2.5f;            // full-strength foam plateau within this radial band
        const float WaterFoamBandU = 4.5f;            // foam fades softly to clear beyond the core
        const float WaterFoamStrength = 0.92f;        // peak foam blend at the waterline (sub-1 keeps a hint of teal)
        static void BuildIslandWater(GameObject parent, string name, Material waterMat, int seed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.layer = 0; // not walkable / not NavMesh (no collider)
            go.transform.position = new Vector3(0f, WaterY, 0f); // sea centred at origin

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // GRID LATTICE: a square plane centred at origin. The depth gradient + radial foam ring key off
            // RADIAL distance from origin (not Z), so they read identically on every coast around the island.
            int lat = WaterSeg + 1;
            float size = WaterHalfExtent * 2f;
            var gridPos = new Vector3[lat * lat];
            var gridCol = new Color[lat * lat];
            for (int z = 0; z < lat; z++)
            for (int x = 0; x < lat; x++)
            {
                int i = z * lat + x;
                float fx = (float)x / WaterSeg, fz = (float)z / WaterSeg;
                float localX = (fx - 0.5f) * size;
                float localZ = (fz - 0.5f) * size;
                gridPos[i] = new Vector3(localX, 0f, localZ);
                float r = Mathf.Sqrt(localX * localX + localZ * localZ);
                // Depth gradient by radial distance OUT from the coast (bright near the round shore, deepening
                // seaward; the fog + warm grade desaturate the far water, so a wide bright near band keeps teal).
                float seawardDist = Mathf.Max(0f, r - IslandShoreR); // 0 at the coast, grows out to sea
                float depthT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seawardDist / 130f));
                Color c = Color.Lerp(WaterShallow, WaterDeep, depthT);
                // RADIAL FOAM ring: full-strength foam plateau on the waterline circle, fading past the core.
                float foamDist = Mathf.Abs(r - IslandShoreR);
                float foamT = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((foamDist - WaterFoamCoreU) / WaterFoamBandU));
                c = Color.Lerp(c, FoamEdge, foamT * WaterFoamStrength);
                c.a = 1f;
                gridCol[i] = c;
            }

            // UNWELDED FLAT-SHADED facets with explicit +Y normals (the faceted-sea look + the winding/normal
            // contract guarded by WaterFacesUpTests). EmitTri forces every face normal UP regardless of source
            // winding, so the all-sides plane is never backface-culled (the −Z-grid cull-back class).
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            void EmitTri(int a, int b, int c2)
            {
                Vector3 p0 = gridPos[a], p1 = gridPos[b], p2 = gridPos[c2];
                Vector3 fn = Vector3.Cross(p1 - p0, p2 - p0);
                if (fn.sqrMagnitude < 1e-12f) return;
                fn.Normalize();
                if (fn.y < 0f) fn = -fn; // water faces UP regardless of source winding (kept-contract)
                int bi = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                cols.Add(gridCol[a]); cols.Add(gridCol[b]); cols.Add(gridCol[c2]);
                normals.Add(fn); normals.Add(fn); normals.Add(fn);
                tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
            }
            for (int z = 0; z < WaterSeg; z++)
            for (int x = 0; x < WaterSeg; x++)
            {
                int i = z * lat + x;
                EmitTri(i, i + 1, i + lat);
                EmitTri(i + 1, i + lat + 1, i + lat);
            }

            var mesh = new Mesh { name = name + "_mesh" };
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetNormals(normals); // explicit flat per-face normals (UNWELDED faceted sea, all +Y)
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            Debug.Log($"[world-trace] BuildIslandWater built all-sides sea plane (half-extent {WaterHalfExtent:F0}u, " +
                      $"radial foam ring at r={IslandShoreR:F0}u, {verts.Count} verts)");
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

        // ROCK vertex-color material (86ca8m5zu SOAKFIX2). The FacetedRock mesh bakes a per-FACET VALUE
        // (0.62 dark sides .. 1.0 light tops) into vertex colour — the facet-to-facet contrast that reads as
        // carved stone. URP/Lit IGNORES vertex colour, so the rock needs the FarHorizon/LowPolyVertexColor
        // shader (albedo = vertexColor * _Tint), same as the blob canopy. The warm-grey rock base goes in
        // _Tint (quantized + cached so jittered tints collapse to a small set — no .mat asset churn); the
        // vertex value multiplies onto it so every facet is a different value of the SAME warm-grey stone.
        // Falls back to flat URP/Lit (value contrast lost but never magenta) if the shader is unresolved.
        static readonly Dictionary<string, Material> _rockCache = new Dictionary<string, Material>();
        static Material RockVertexColorMat(Color baseTint)
        {
            // FINE quantization (24-step, not the coarse 12-step the other props use): the coarse grid split
            // the warm-grey rock ramp into a pink (R>G=B) cast (TINTDIAG). The fine grid preserves R>=G>=B —
            // still only ~4 distinct rock mats, so churn stays low.
            Color q = QuantizeFine(baseTint);
            string key = "LPRockMat_" + ColorKey(q);
            if (_rockCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            Material mat;
            if (vc != null)
            {
                mat = new Material(vc) { name = key };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", q);
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
                mat.SetColor("_BaseColor", q);
                mat.SetFloat("_Smoothness", 0.05f);
                Debug.LogWarning("[LowPolyZoneGen] vertex-color shader not found; rock falls back to flat grey");
            }
            _rockCache[key] = mat;
            return mat;
        }

        // Clear the per-bootstrap material cache so a re-run does not return materials owned by a
        // destroyed scene (the editor keeps the static cache across executeMethod invocations).
        public static void ResetMaterialCache() { _flatCache.Clear(); _canopyMat = null; _rockCache.Clear(); }

        // Snap each channel to a coarse 12-step grid so jittered colors collapse into a small set.
        static Color Quantize(Color c)
        {
            const float steps = 12f;
            return new Color(
                Mathf.Round(c.r * steps) / steps,
                Mathf.Round(c.g * steps) / steps,
                Mathf.Round(c.b * steps) / steps, 1f);
        }

        // Finer 24-step quantizer for the ROCK tint — the coarse 12-step grid split the warm-grey ramp into
        // a pink (R>G=B) cast (TINTDIAG). 24 steps preserve R>=G>=B while still collapsing the per-instance
        // jitter to ~4 distinct rock materials (low churn).
        static Color QuantizeFine(Color c)
        {
            const float steps = 24f;
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
                // Gentle large-wavelength swell (Uma §1: "a breath, not surf"). The shader displaces Y
                // in-vertex; nothing runs per-frame on the CPU. SOFT-WASH RESTORE (86ca8t9pq S1): bump the
                // amplitude 0.06 -> 0.10u + a touch faster so the foam surf LINE at the coast visibly
                // BREATHES (advances/retreats up the gentle wet shelf) — the "water washing up on shore"
                // the Sponsor liked. Still a calm swell, not surf (a longer wavelength keeps it soft).
                if (mat.HasProperty("_WaveAmp")) mat.SetFloat("_WaveAmp", 0.10f);
                if (mat.HasProperty("_WaveLen")) mat.SetFloat("_WaveLen", 18f);
                if (mat.HasProperty("_WaveSpeed")) mat.SetFloat("_WaveSpeed", 0.95f);
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
