using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// NEXT-ISLAND POC generator (ticket 86caa9zpp — Sponsor: "a much-bigger random-shape next island
    /// with a REAL snow-capped mountain"). A STAND-ALONE proof that the EXISTING low-poly world-gen,
    /// SCALED UP, produces a feels-big organic walkable island with ONE dominant walkable snow-cap peak
    /// — and (the #1 deliverable) that a single scaled terrain mesh + LOD on the existing low-poly +
    /// GPU-Resident-Drawer approach holds 60fps in the shipped build.
    ///
    /// REAL-WORLD ANCHOR (the physical-feature gate, lowpoly-quality.md §0): an ISLAND is land rising OUT
    /// of the sea; a MOUNTAIN is a giant hill rising UP from the island, grass at its foot → bare stone
    /// higher → SNOW collecting on its cold peak. The build must satisfy THAT sentence, not just a
    /// seed/metric — the mountain is a climbable giant hill baked INTO the terrain heightfield, NOT a
    /// horizon-backdrop prop (that is what WorldBootstrap.FacetedMountain already does for the start
    /// island's distant vista; this POC's peak is WALKABLE terrain).
    ///
    /// SCOPE / REUSE (ticket contract): this REUSES the start island's radial-falloff + azimuth-warp
    /// coast idiom (ShoreRadiusAt-style organic outline, faceted flat-shaded family) SCALED UP, and the
    /// SAME FarHorizon/LowPolyVertexColor shader (via LowPolyZoneGen.MakeTerrainVertexColorMaterial /
    /// MakeWaterMaterial) — it does NOT fork or touch the seed-42 LowPolyZoneGen path (that must stay
    /// byte-untouched — "close to perfect"). It is a PARALLEL big-island generator sized for the POC.
    ///
    /// The snow cap is a HEIGHT-THRESHOLD white VERTEX-COLOUR on the faceted mesh (white above a height
    /// fraction; NO snow texture), preserving the flat-shaded Zone-D look + the shared-palette ~1-draw-
    /// call budget (the constraint: snow = height-threshold white material, not a texture).
    /// </summary>
    public static class NextIslandPocGen
    {
        // ============================================================================================
        // SIZE — tunable DEFAULT (the Sponsor dials from the soak; flagged as a default, not a mandate).
        // ============================================================================================
        // Target crossing time ~2-3 min at WASD speed (walk 5.5 u/s, run 9.5 u/s — WasdMovement). A land
        // diameter ~800u (mean shore radius ~400u) lands a mixed walk/run crossing squarely in the 2-3
        // min band: at walk 5.5 → ~145s (2.4 min) edge-to-edge; at run 9.5 → ~84s (1.4 min). ~3.3× the
        // seed-42 start island's 240u diameter → genuinely "much bigger" (AC1) while keeping the single-
        // scaled-mesh perf question honestly testable (AC4). Do NOT size for the eventual ~10-min target
        // up front — only scale up if the perf verdict stays green (ticket AC2).
        public const float MeanShoreR = 400f;   // mean radial waterline (~800u diameter). Azimuth-warped below.
        public const float CoreR = 300f;        // inside this radius = the land plateau + full hills
        public const float FalloffEnd = 470f;   // seabed has fully dropped below the sea by here (past the coast)

        // Organic coast (AC1) — the mean shore is WARPED by low-frequency azimuth noise so the outline is
        // irregular (bays + headlands), NOT a circle/star. Amplitude scaled up with the island.
        public const float CoastIrregAmp = 70f;    // ±u the coast wanders off the mean (broad bays + headlands)
        public const float CoastNoiseRadius = 2.0f; // circle radius the azimuth noise samples (small = broad lazy bays)

        public const float BeachWidth = 34f;       // u of flat sand strip inland of the waterline (scaled up)
        public const float PlateauH = 0.2f;        // base inland land height just above the sea (hills rise from here)
        // Rolling-hill amplitude inland (real elevation). Kept MODERATE so the combined hill-noise stays under
        // the 45° NavMesh agent max on its steepest local octave — a bigger amplitude with the high-freq octave
        // produced local ~70° spikes that would orphan patches from the walkable surface (the NavMesh-coverage
        // trace + PlayMode gate verify the walkable result). Real rolling hills, not un-climbable crags.
        public const float HillAmp = 13f;
        public const float SeabedDrop = -22f;      // how far the seabed sinks past the shore (well below WaterY)

        public const float WaterY = -0.20f;        // sea surface (matches LowPolyZoneGen so the shader/fog read the same)

        // Terrain grid: a square welded grid covering the disc + a sea margin. The ROUNDNESS comes from the
        // radial height field, not the grid shape (deep-sea cells are clipped). 260 seg over ~1000u ≈ 3.9u
        // cells — the perf-honest resolution for the #1 finding (a bigger grid = the chunked-terrain question).
        public const float GridHalf = 500f;        // square grid half-extent (covers shore 400 + margin)
        public const int Seg = 260;                // subdivision across the big disc (smooth slopes + NavMesh)

        // ============================================================================================
        // THE HERO MOUNTAIN — ONE dominant walkable snow-capped peak (AC3). A giant Gaussian-ish hill baked
        // INTO the heightfield at a fixed off-centre spot (so the island reads asymmetric, not a bullseye).
        // ============================================================================================
        public const float MtnCenterX = 90f;       // off-centre so the island isn't a symmetric bullseye
        public const float MtnCenterZ = -60f;
        public const float MtnPeakHeight = 135f;   // peak rises this far above the plateau (a confident giant hill)
        // Foot spans a BROAD radius so the flank stays CLIMBABLE — max slope under the 45° NavMesh agent max
        // (a raised-cosine dome's steepest mid-flank ≈ π·peak/(2·foot); 135/300 → ~35°, comfortably walkable).
        // A narrower foot made a 59° wall the agent could not climb (the AC3 slope guard caught it).
        public const float MtnFootRadius = 300f;   // the mountain's foot spans this radius (a broad climbable base)
        // Snow line: the top ~28% of the peak height reads snow (tunable default — Sponsor dials the line).
        // Above SnowlineFrac × MtnPeakHeight (measured from the plateau) the vertex colour is the white snow cap.
        public const float SnowlineFrac = 0.72f;   // faces above this height fraction of the peak read snow

        // ============================================================================================
        // SNOW-CAP FACETING (ticket 86cahmxh6 — Sponsor: the snow cap "less smooth and round, a bit more
        // chunky/faceted"). The board's snow (inspiration/2026-06-12_21h12_49.png + 21h16_13.png) reads as
        // ANGULAR broken white PLANES, not a smooth rounded dome. The snow ZONE of the terrain mesh gets
        // FLAT per-face normals + a small angular height displacement so it reads chunky/faceted; the
        // grass/rock/beach zones stay welded-smooth (the Zone-D dune look — never wholesale flat-shaded per
        // lowpoly-quality §3). The snow stays a HEIGHT-THRESHOLD white VERTEX COLOUR (carried constraint —
        // NO snow texture); ColorAt is UNCHANGED (keyed on the smooth MountainHeightAt), so the banding +
        // the snow-cap regression guard stay green. Climbability is protected by BOUNDS: the displacement is
        // small at a coarse cell (worst added slope ~atan2(2*amp, cell) ≈ 20°) stacked on the near-FLAT
        // summit of the raised-cosine dome → stays well under the 45° NavMesh agent max (verified by the
        // shipped -verifyPocIsland NAVMESH-COVERAGE + highest-reachable-Y trace).
        // A face/vert is in the snow zone when its mountain-height fraction >= SnowFacetFrac. Set a touch
        // BELOW SnowlineFrac so the faceting covers the whole visible white band with a small blend margin.
        public const float SnowFacetFrac = 0.66f;   // faces above this mountain-height fraction get faceted
        public const float SnowFacetAmp = 2.2f;      // ±u of angular displacement on snow verts (climb-bounded)
        public const float SnowFacetCell = 22f;      // world-u cell of the angular snow-facet noise (coarse = broad
                                                     // chunky planes; wide enough that adjacent grid verts (~3.9u)
                                                     // share a plane → inter-vert slope stays climbable)

        // Spawn clearing — a flat clearing OPPOSITE the hero mountain so the player starts on gentle sea-level
        // ground and walks ACROSS the island toward the peak (the "small character, far horizon" read). It CANNOT
        // be the world origin: the mountain foot (300u) centred at (90,-60) blankets the origin, so a spawn there
        // sits ~83u UP the flank (run-2 finding). At (-180,150) — 342u from the mountain centre, OUTSIDE the foot —
        // the spawn terrain is the flat plateau (~0.2u) and the walk toward the peak is a ~340u traverse (a good
        // chunk of the feels-big crossing). Foot 300 on a 400-r island CANNOT both clear the origin AND fit fully
        // on land at a climbable slope (the whole-dome-on-land window is empty for foot>=260), so we move the SPAWN,
        // not the mountain (which keeps the 37° climbable-slope tuning + the mountain tests untouched).
        public const float SpawnX = -180f;
        public const float SpawnZ = 150f;
        public const float SpawnFlattenHoldR = 22f;   // hills fully damped within this radius of the SPAWN (flat clearing)
        public const float SpawnFlattenFullR = 55f;   // hills back to full by here

        // ---- Palette (reuse the start-island warm/lush anchors so the POC reads as the SAME world) ----
        static readonly Color SandLo    = new Color(0.78f, 0.64f, 0.39f);
        static readonly Color SandHi    = new Color(0.88f, 0.75f, 0.49f);
        static readonly Color GrassLo   = new Color(0.30f, 0.48f, 0.20f);
        static readonly Color GrassHi   = new Color(0.48f, 0.64f, 0.28f);
        static readonly Color GrassRise = new Color(0.38f, 0.56f, 0.24f);
        static readonly Color RockLo    = new Color(0.42f, 0.40f, 0.37f); // dark bare rock (mountain flank)
        static readonly Color RockHi    = new Color(0.60f, 0.58f, 0.55f); // lighter high rock just under the snow
        static readonly Color SnowWhite = new Color(0.92f, 0.93f, 0.95f); // the snow cap (sub-1.0, HDR-clamp-safe)
        static readonly Color WaterShallow = new Color(0.10f, 0.62f, 0.66f);
        static readonly Color WaterDeep    = new Color(0.10f, 0.50f, 0.60f);
        static readonly Color FoamEdge     = new Color(0.91f, 0.89f, 0.82f);

        /// <summary>
        /// Build the POC big island (terrain + walkable snow-cap mountain baked in + water on all sides)
        /// under a single root, return the ground GameObject so the caller can bake NavMesh + parent scatter.
        /// vertexColorMat/waterMat are the SHARED FarHorizon shaders (LowPolyZoneGen material makers) — the
        /// POC does NOT introduce a new shader (the snow is a vertex colour on the same material).
        /// </summary>
        public static GameObject BuildPocIsland(GameObject parent, int seed, Material vertexColorMat, Material waterMat)
        {
            var root = new GameObject("PocIsland");
            root.transform.SetParent(parent.transform, false);

            GameObject ground = BuildTerrainMesh(root, "Ground_Poc", vertexColorMat, seed);
            BuildWater(root, "Water_Poc", waterMat, seed);

            return ground;
        }

        // The per-seed noise offset (deterministic from the seed — a different seed re-rolls the coast +
        // hills but keeps the same character). Same idiom as LowPolyZoneGen.SeedOffset.
        public static void SeedOffset(int seed, out float ox, out float oz)
        {
            var rnd = new System.Random(seed);
            ox = (float)rnd.NextDouble() * 100f;
            oz = (float)rnd.NextDouble() * 100f;
        }

        // The WARPED coast radius at a world-XZ azimuth — low-freq azimuth noise on a circle (wraps at
        // 0/360) modulates the mean by ±CoastIrregAmp. SAME idiom as LowPolyZoneGen.ShoreRadiusAt, scaled up.
        public static float ShoreRadiusAt(float wx, float wz, float ox, float oz)
        {
            float ang = Mathf.Atan2(wz, wx);
            float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);
            float n1 = Mathf.PerlinNoise(ox + (cx * CoastNoiseRadius + 50f),
                                         oz + (cz * CoastNoiseRadius + 50f)) - 0.5f;
            float n2 = Mathf.PerlinNoise(ox * 1.7f + (cx * CoastNoiseRadius * 1.9f + 90f),
                                         oz * 1.7f + (cz * CoastNoiseRadius * 1.9f + 90f)) - 0.5f;
            float warp = (n1 * 2f * 0.68f + n2 * 2f * 0.32f);
            warp = Mathf.Clamp(warp * 1.25f, -1f, 1f);
            return MeanShoreR + warp * CoastIrregAmp;
        }

        /// <summary>
        /// The HERO MOUNTAIN height bump at world XZ (≥ 0, added on top of the plateau/hills). A smooth
        /// radial cosine dome centred at (MtnCenterX, MtnCenterZ) reaching MtnPeakHeight at the centre and
        /// fading to 0 at MtnFootRadius — a broad CLIMBABLE giant hill, NOT a spike. Pure function of XZ
        /// (deterministic; byte-stable capture). PUBLIC so the shape/perf tests + colour band sample it.
        /// </summary>
        public static float MountainHeightAt(float wx, float wz)
        {
            float dx = wx - MtnCenterX, dz = wz - MtnCenterZ;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d >= MtnFootRadius) return 0f;
            float t = d / MtnFootRadius;                 // 0 centre .. 1 foot
            // Raised cosine dome: smooth, walkable slope (no vertical wall) but a confident tall summit. A
            // gentle power keeps the summit rounded + the flanks climbable (max slope well under the NavMesh
            // agent's 45° — a steep spike would orphan the peak from the walkable surface).
            float dome = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI); // 1 centre .. 0 foot
            return MtnPeakHeight * Mathf.Pow(dome, 1.25f);
        }

        /// <summary>
        /// The mountain-height FRACTION at world XZ (0 at/below the foot .. 1 at the summit), relative to the
        /// peak height. The snow band + the snow-facet zone key off this so only the hero peak snows/facets.
        /// PUBLIC so the faceting tests sample it. (Same value ColorAt uses for the banding.)
        /// </summary>
        public static float MountainHeightFracAt(float wx, float wz) =>
            Mathf.Clamp01(MountainHeightAt(wx, wz) / MtnPeakHeight);

        /// <summary>True if this XZ is in the SNOW-FACET zone (mountain-height fraction >= SnowFacetFrac) —
        /// its terrain face gets flat per-face normals + the angular snow displacement (the chunky cap).</summary>
        public static bool IsSnowFacetZone(float wx, float wz) =>
            MountainHeightFracAt(wx, wz) >= SnowFacetFrac;

        /// <summary>
        /// The angular chunky-facet displacement added to a snow-zone vertex's HEIGHT (ticket 86cahmxh6). A
        /// small coarse-cell angular perturbation so the snow surface reads as broken planes, not a smooth
        /// dome. Fades in from 0 at SnowFacetFrac (a SmoothStep) so there is no cliff between the smooth rock
        /// flank and the faceted cap. BOUNDED (SnowFacetAmp small, SnowFacetCell coarse) so the added slope
        /// stays climbable on the near-flat summit. Returns 0 outside the snow zone. PUBLIC so tests sample it.
        /// </summary>
        public static float SnowFacetDisplace(float wx, float wz, int seed)
        {
            float frac = MountainHeightFracAt(wx, wz);
            if (frac < SnowFacetFrac) return 0f;
            // Fade the displacement in over the first slice of the snow zone so the rock→snow seam is smooth.
            float ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SnowFacetFrac, SnowFacetFrac + 0.10f, frac));
            // Coarse-cell CONTINUOUS angular noise (NO hard quantization — the FLAT per-face normals in the mesh
            // build are what make each grid quad read as a distinct angular PLANE; the displacement only needs to
            // give those planes VARIED heights so the cap is broken/chunky, not a smooth dome). A continuous
            // (smoothly-varying) height keeps the inter-vert gradient bounded → the cap stays CLIMBABLE, while
            // the per-face flat shading delivers the hard-edged faceted read the Sponsor asked for.
            float n = AngularFacetNoise(wx, wz, seed);            // 0..1, smooth
            return (n - 0.5f) * 2f * SnowFacetAmp * ramp;         // ±SnowFacetAmp, faded by the ramp
        }

        // Coarse-cell angular noise for the snow facets: two offset Perlin octaves at the snow-facet cell. The
        // dominant octave is broad (whole-plane height variation); the second is a touch finer for asymmetry.
        // Continuous (no quantization) — bounded gradient keeps the faceted cap climbable.
        static float AngularFacetNoise(float wx, float wz, int seed)
        {
            SeedOffset(seed, out float ox, out float oz);
            float f = 1f / SnowFacetCell;
            float a = Mathf.PerlinNoise(ox + wx * f + 13.1f, oz + wz * f + 7.7f);
            float b = Mathf.PerlinNoise(ox * 1.3f + wx * f * 1.7f + 41.3f, oz * 1.3f + wz * f * 1.7f + 29.9f);
            return Mathf.Clamp01(a * 0.7f + b * 0.3f);
        }

        /// <summary>
        /// The organic island HEIGHT FIELD at world XZ (the single source of truth — the visible mesh, its
        /// collider, and the NavMesh all flow from it). Big-island version of LowPolyZoneGen.HeightAtRadial:
        /// interior land at plateau + rolling hills + the HERO MOUNTAIN bump; a flat sand beach strip easing
        /// to the warped waterline; the seabed sinking past the coast. PUBLIC so the shape/perf tests sample it.
        /// </summary>
        public static float HeightAtRadial(float wx, float wz, float ox, float oz)
        {
            float r = Mathf.Sqrt(wx * wx + wz * wz);
            float coast = ShoreRadiusAt(wx, wz, ox, oz);

            float baseH = PlateauH;

            // Shore transition: the sand eases gently down from the plateau to the waterline over BeachWidth
            // (a flat beach, not a long ramp); the seabed sinks past the coast.
            float seabed = WaterY + (SeabedDrop - WaterY) *
                           Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coast, FalloffEnd, r));
            float beachT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coast - BeachWidth, coast, r));
            float beachH = Mathf.Lerp(PlateauH, WaterY, beachT);

            float h;
            if (r >= coast) h = Mathf.Min(beachH, seabed);      // past the waterline — the beach dip + seabed
            else if (r >= coast - BeachWidth) h = beachH;        // the shore transition (flat sand strip)
            else h = baseH;                                       // flat interior land (grass at plateau level)

            // landMask: 1 well inland, easing to 0 at the coast — fades the hills off at the shore (clean
            // waterline) and keeps hills off the flat beach strip.
            float landMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CoreR, coast - BeachWidth, r));

            // Rolling hills over the LAND interior only (faded off by landMask). Damped near the spawn origin
            // so the immediate spawn is a flat clearing.
            float hillH = HillHeightAt(wx, wz, ox, oz, landMask, r);
            if (r < coast - BeachWidth) h += hillH;

            // THE HERO MOUNTAIN — the walkable giant hill, added on the interior land only (its foot is well
            // inside the coast). Fades off at the shore via the SAME landMask so a coast-clipped mountain edge
            // can't punch through the waterline.
            if (r < coast - BeachWidth)
                h += MountainHeightAt(wx, wz) * Mathf.Clamp01(landMask * 1.15f);

            return h;
        }

        /// <summary>The damped rolling-hill height at world XZ (factored out so tests/colour can reuse it).
        /// Multi-octave Perlin over the interior (landMask² fade) + spawn-flatten near origin.</summary>
        public static float HillHeightAt(float wx, float wz, float ox, float oz, float landMask, float r)
        {
            // LOW-frequency octaves (broad rolling hills, gentle gradients — the high-freq crag octave was
            // dropped: at HillAmp it produced local ~70° spikes that would orphan NavMesh patches). Wavelengths
            // are long (freq ≤ 0.020) so the slope between adjacent verts stays walkable.
            float hill = (Mathf.PerlinNoise(ox + wx * 0.005f, oz + wz * 0.005f) - 0.5f) * 2f * 0.68f
                       + (Mathf.PerlinNoise(ox + wx * 0.012f, oz + wz * 0.012f) - 0.5f) * 2f * 0.32f;
            float hillH = (hill * 0.5f + 0.5f) * HillAmp * landMask * landMask;
            // Flatten the hills into a clearing around the SPAWN point (NOT the origin — the spawn moved off-origin
            // to clear the mountain foot). Distance is measured to (SpawnX,SpawnZ) so the player spawns on flat ground.
            float dSpawn = Mathf.Sqrt((wx - SpawnX) * (wx - SpawnX) + (wz - SpawnZ) * (wz - SpawnZ));
            float spawnFlat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SpawnFlattenHoldR, SpawnFlattenFullR, dSpawn));
            return hillH * (0.06f + 0.94f * spawnFlat);
        }

        // The big square welded terrain grid; the ROUND landmass comes from the radial height field (clip the
        // deep-sea cells so no square edge reads). WELDED + RecalculateNormals = the smooth-shaded low-poly
        // look (averaged vertex normals — the same technical look as the start island). The snow cap + rock +
        // grass + sand bands are baked per-vertex into COLOUR (height-threshold snow — NO texture).
        static GameObject BuildTerrainMesh(GameObject parent, string name, Material mat, int seed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = Vector3.zero;
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) go.layer = groundLayer;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            SeedOffset(seed, out float ox, out float oz);

            // ---- Grid verts + colours. The snow-facet zone (ticket 86cahmxh6) gets an angular height
            //      DISPLACEMENT so the cap reads chunky; grass/rock/beach heights are unchanged. Colour keys
            //      off the SMOOTH MountainHeightAt (via ColorAt) so the banding + snow-cap guard are unchanged. ----
            int vCount = (Seg + 1) * (Seg + 1);
            var verts = new Vector3[vCount];
            var cols = new Color[vCount];
            float size = GridHalf * 2f;
            for (int z = 0; z <= Seg; z++)
            for (int x = 0; x <= Seg; x++)
            {
                int i = z * (Seg + 1) + x;
                float fx = (float)x / Seg, fz = (float)z / Seg;
                float wx = (fx - 0.5f) * size;
                float wz = (fz - 0.5f) * size;
                float h = HeightAtRadial(wx, wz, ox, oz);
                h += SnowFacetDisplace(wx, wz, seed);   // angular chunk on the snow cap only (0 elsewhere)
                verts[i] = new Vector3(wx, h, wz);
                cols[i] = ColorAt(wx, wz, h, ox, oz, seed);
            }

            // CLIP the terrain to the irregular landmass (+ a sea-shelf skirt): a cell is emitted only if at
            // least one corner is within (warped coast + a margin). Deep-sea cells dropped → no square grid
            // edge reads from overhead; the big water plane fills the rest to the fog horizon.
            //
            // NORMALS (ticket 86cahmxh6): the mesh carries EXPLICIT normals — NOT RecalculateNormals. Non-snow
            // triangles stay WELDED with SMOOTH averaged normals (the Zone-D dune look, unchanged); snow-zone
            // triangles are emitted UNWELDED with FLAT per-face normals (the chunky faceted cap — the
            // LowPolyMeshes.FacetedRock idiom). One mesh, one collider (the NavMesh bakes the real surface).
            const float clipMargin = 40f;
            var outVerts = new List<Vector3>(vCount + 4096);
            var outCols = new List<Color>(vCount + 4096);
            var outNormals = new List<Vector3>(vCount + 4096);
            var outTris = new List<int>(Seg * Seg * 6);

            // Welded verts keep their original grid slot in outVerts (0..vCount-1); we accumulate smooth normals
            // for them from the NON-SNOW faces only, then append the snow faces' own unwelded verts afterwards.
            for (int i = 0; i < vCount; i++)
            {
                outVerts.Add(verts[i]);
                outCols.Add(cols[i]);
                outNormals.Add(Vector3.zero);   // accumulator → normalized after the non-snow pass
            }

            var snowFaces = new List<int>(); // flat indices (a,b,c) of snow-zone triangles, resolved in pass 2
            void ConsiderTri(int a, int b, int c)
            {
                // A triangle is a SNOW face if its centroid is in the snow-facet zone (keyed on the SMOOTH
                // dome so the classification is stable + matches the colour band, independent of the display).
                float cxw = (verts[a].x + verts[b].x + verts[c].x) / 3f;
                float czw = (verts[a].z + verts[b].z + verts[c].z) / 3f;
                if (IsSnowFacetZone(cxw, czw))
                {
                    snowFaces.Add(a); snowFaces.Add(b); snowFaces.Add(c); // flat-shade in pass 2
                    return;
                }
                // Non-snow: welded, smooth. Accumulate the face normal onto its 3 shared verts.
                Vector3 fn = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (fn.y < 0f) fn = -fn;                // island faces up; keep the up-orientation
                outNormals[a] += fn; outNormals[b] += fn; outNormals[c] += fn;
                outTris.Add(a); outTris.Add(b); outTris.Add(c);
            }

            for (int z = 0; z < Seg; z++)
            for (int x = 0; x < Seg; x++)
            {
                int i = z * (Seg + 1) + x;
                if (!CellNearLandmass(verts, i, Seg, ox, oz, clipMargin)) continue;
                ConsiderTri(i, i + Seg + 1, i + 1);
                ConsiderTri(i + 1, i + Seg + 1, i + Seg + 2);
            }

            // Normalize the accumulated smooth normals for the welded (non-snow) verts. A vert touched only by
            // snow faces (never accumulated) stays zero here but is not referenced by any welded triangle, so
            // its normal is inert; give it up as a safe default.
            for (int i = 0; i < vCount; i++)
                outNormals[i] = outNormals[i].sqrMagnitude > 1e-10f ? outNormals[i].normalized : Vector3.up;

            // Pass 2 — emit the SNOW faces UNWELDED with FLAT per-face normals (own 3 verts each). This is what
            // makes the cap read as angular planes (each facet lit by its own N·L) rather than a smooth dome.
            int snowFaceCount = 0;
            for (int t = 0; t < snowFaces.Count; t += 3)
            {
                int a = snowFaces[t], b = snowFaces[t + 1], c = snowFaces[t + 2];
                Vector3 v0 = verts[a], v1 = verts[b], v2 = verts[c];
                Vector3 fn = Vector3.Cross(v1 - v0, v2 - v0);
                if (fn.sqrMagnitude < 1e-10f) continue;
                fn.Normalize();
                if (fn.y < 0f) { fn = -fn; var tmp = v1; v1 = v2; v2 = tmp; } // keep the up-facing front side
                int bi = outVerts.Count;
                outVerts.Add(v0); outVerts.Add(v1); outVerts.Add(v2);
                outCols.Add(cols[a]); outCols.Add(cols[b]); outCols.Add(cols[c]);
                outNormals.Add(fn); outNormals.Add(fn); outNormals.Add(fn);
                outTris.Add(bi); outTris.Add(bi + 1); outTris.Add(bi + 2);
                snowFaceCount++;
            }

            var mesh = new Mesh { name = name + "_mesh" };
            mesh.indexFormat = outVerts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(outVerts);
            mesh.SetColors(outCols);
            mesh.SetNormals(outNormals);   // EXPLICIT (smooth non-snow + flat snow) — never RecalculateNormals
            mesh.SetTriangles(outTris, 0);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh; // NavMesh bakes on the actual sloped island + faceted snow surface

            Debug.Log($"[poc-trace] BuildTerrainMesh '{name}': {outVerts.Count} verts, {outTris.Count / 3} tris " +
                      $"({snowFaceCount} FLAT snow-facet faces + welded-smooth rest; clipped from {Seg * Seg * 2} " +
                      $"full-grid), meanShoreR={MeanShoreR:F0}u peakH={MtnPeakHeight:F0}u snowFacetFrac={SnowFacetFrac:F2} seed={seed}");
            return go;
        }

        static bool CellNearLandmass(Vector3[] verts, int i, int seg, float ox, float oz, float margin)
        {
            int stride = seg + 1;
            int[] corners = { i, i + 1, i + stride, i + stride + 1 };
            foreach (int ci in corners)
            {
                Vector3 v = verts[ci];
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                if (r <= ShoreRadiusAt(v.x, v.z, ox, oz) + margin) return true;
            }
            return false;
        }

        /// <summary>
        /// Island vertex COLOUR: grass over the interior, warm sand at the beach, and on the HERO MOUNTAIN a
        /// grass→bare-rock→SNOW banding by HEIGHT (the height-threshold snow cap — NO texture). Foam near the
        /// warped waterline. PUBLIC so the snow-threshold + banding tests sample it. The snow band is keyed on
        /// the height ABOVE the plateau relative to the mountain peak height, so it reads as a real snow-capped
        /// summit (white only near the top) rather than white-everywhere-high.
        /// </summary>
        public static Color ColorAt(float wx, float wz, float height, float ox, float oz, int seed)
        {
            float r = Mathf.Sqrt(wx * wx + wz * wz);
            float coast = ShoreRadiusAt(wx, wz, ox, oz);

            // Base grass over the interior (brighter toward the centre / on sunlit rises).
            Color grass = Color.Lerp(GrassLo, GrassHi, Mathf.Clamp01(Mathf.InverseLerp(coast, 0f, r)));
            grass = Color.Lerp(grass, GrassRise, Mathf.Clamp01((height - 6f) * 0.06f));

            // MOUNTAIN banding by HEIGHT ABOVE THE PLATEAU relative to the peak (the snow-cap read). Grass at
            // the foot → bare rock up the flank → SNOW near the summit. Keyed on the mountain contribution so
            // only the hero peak snows (a rolling hill inland never reaches the snow band).
            float mtnH = MountainHeightAt(wx, wz);                 // this XZ's mountain contribution
            float heightFrac = Mathf.Clamp01(mtnH / MtnPeakHeight); // 0 foot .. 1 summit (mountain-relative)
            Color land = grass;
            // Rock starts ~40% up the peak, ramping in; snow starts at SnowlineFrac.
            float rockT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.62f, heightFrac));
            Color rock = Color.Lerp(RockLo, RockHi, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, SnowlineFrac, heightFrac)));
            land = Color.Lerp(land, rock, rockT);
            // SNOW cap above the snow line (height-threshold white — the constraint: NO snow texture).
            float snowT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SnowlineFrac, SnowlineFrac + 0.12f, heightFrac));
            land = Color.Lerp(land, SnowWhite, snowT);

            // Coastal SAND band just inland of the warped coast (beach sectors).
            float coastBandT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coast - BeachWidth, coast - BeachWidth * 0.5f, r));
            Color sand = Color.Lerp(SandLo, SandHi, Mathf.Clamp01((height + 0.2f) * 1.5f));
            Color c = Color.Lerp(land, sand, coastBandT);

            // FOAM following the warped waterline (baked into the terrain so the coast foam reads even where
            // the flat water plane is occluded by the sand shelf).
            float foamDist = Mathf.Abs(r - coast);
            float foamT = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((foamDist - 6f) / 12f));
            c = Color.Lerp(c, FoamEdge, foamT * 0.9f);

            // Per-vertex value jitter so adjacent facets differ slightly (alive, not flat).
            float j = (Hash01(Mathf.RoundToInt(wx * 5f), Mathf.RoundToInt(wz * 5f), seed) - 0.5f) * 0.08f;
            c.r = Mathf.Clamp01(c.r + j); c.g = Mathf.Clamp01(c.g + j); c.b = Mathf.Clamp01(c.b + j);
            c.a = 1f;
            return c;
        }

        // The all-sides sea: a large square plane centred at origin, colours + foam keyed off RADIAL distance
        // from the warped coast (same idiom as LowPolyZoneGen.BuildIslandWater, scaled up). Extends past the
        // fog horizon so its far edge dissolves into haze. UNWELDED flat-shaded, top face front (the cull-back
        // contract). Sits at WaterY; the round coast is where the terrain dips below it.
        static void BuildWater(GameObject parent, string name, Material waterMat, int seed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.layer = 0;
            go.transform.position = new Vector3(0f, WaterY, 0f);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            const float halfExtent = 1600f; // reaches well past the ~800u island to the fog horizon, all sides
            const int waterSeg = 180;       // fine enough that the warped foam ring lands verts every quadrant
            SeedOffset(seed, out float wox, out float woz);
            int lat = waterSeg + 1;
            float size = halfExtent * 2f;
            var gridPos = new Vector3[lat * lat];
            var gridCol = new Color[lat * lat];
            for (int z = 0; z < lat; z++)
            for (int x = 0; x < lat; x++)
            {
                int i = z * lat + x;
                float fx = (float)x / waterSeg, fz = (float)z / waterSeg;
                float localX = (fx - 0.5f) * size;
                float localZ = (fz - 0.5f) * size;
                gridPos[i] = new Vector3(localX, 0f, localZ);
                float r = Mathf.Sqrt(localX * localX + localZ * localZ);
                float coast = ShoreRadiusAt(localX, localZ, wox, woz);
                float seawardDist = Mathf.Max(0f, r - coast);
                float depthT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seawardDist / 320f));
                Color c = Color.Lerp(WaterShallow, WaterDeep, depthT);
                float foamDist = Mathf.Abs(r - coast);
                float foamT = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((foamDist - 10f) / 22f));
                c = Color.Lerp(c, FoamEdge, foamT * 0.9f);
                c.a = 1f;
                gridCol[i] = c;
            }

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
                int bi = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                cols.Add(gridCol[a]); cols.Add(gridCol[b]); cols.Add(gridCol[c2]);
                if (fn.y >= 0f)
                {
                    normals.Add(fn); normals.Add(fn); normals.Add(fn);
                    tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
                }
                else
                {
                    fn = -fn;
                    normals.Add(fn); normals.Add(fn); normals.Add(fn);
                    tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 1);
                }
            }
            for (int z = 0; z < waterSeg; z++)
            for (int x = 0; x < waterSeg; x++)
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
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            Debug.Log($"[poc-trace] BuildWater '{name}': half-extent {halfExtent:F0}u, {verts.Count} verts, all-sides sea");
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
