using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// NEXT-ISLAND POC generator (ticket 86caa9zpp base; GROWN by 86cahwx6w island 2.0-A — Sponsor: "the
    /// Island should be bigger and more diverse with more mountains"). A STAND-ALONE proof that the
    /// EXISTING low-poly world-gen, SCALED UP, produces a feels-big organic walkable island with a
    /// MULTI-PEAK RANGE — the Sponsor-approved climbable snow-cap HERO plus shorter/steeper non-climbable
    /// rock massifs — and that a single scaled terrain mesh + the existing low-poly approach holds 60fps.
    ///
    /// REAL-WORLD ANCHOR (the physical-feature gate, lowpoly-quality.md §0): an ISLAND is land rising OUT
    /// of the sea; a MOUNTAIN is a giant hill rising UP from the island, grass at its foot → bare stone
    /// higher → SNOW collecting on its cold peak; an island with SEVERAL mountains reads as RIDGES RISING
    /// FROM ONE LANDMASS — connected cols and shoulders — not cones stamped on a pancake. The build must
    /// satisfy THOSE sentences, not just a seed/metric — the peaks are baked INTO the terrain heightfield,
    /// NOT horizon-backdrop props (that is what WorldBootstrap.FacetedMountain already does for the start
    /// island's distant vista; this POC's hero peak is WALKABLE terrain).
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
        // ISLAND 2.0-A (ticket 86cahwx6w): grown from the #226-approved 400u mean shore (~800u land
        // diameter) to 600u (~1200u diameter, 1.5× — the middle of the ticket's 550-650u default band).
        // Crossing at WASD speeds: walk 5.5 u/s → ~3.6 min edge-to-edge, run 9.5 u/s → ~2.1 min — the
        // "markedly bigger" read while a run-crossing stays under the ~3-min slog bound. The coast /
        // core / falloff constants scale PROPORTIONALLY (×1.5) so the #226-approved shape character is
        // preserved at the new size. Perf-gated: re-measured via -perfProbe on the -development build
        // before staging (the #226 verdict was BOUNDED to ~800u — elite-techniques.md).
        // default 600 — Sponsor-soak tunes (band ~550-650).
        public const float MeanShoreR = 600f;   // mean radial waterline (~1200u diameter). Azimuth-warped below.
        public const float CoreR = 450f;        // inside this radius = the land plateau + full hills (×1.5)
        public const float FalloffEnd = 705f;   // seabed has fully dropped below the sea by here (past the coast;
                                                // = MeanShoreR + CoastIrregAmp, the same margin idiom as #226's 470)

        // Organic coast (AC1 / Bar 1) — the mean shore is WARPED by low-frequency azimuth noise so the outline
        // is irregular (bays + headlands), NOT a circle/star. Amplitude scaled PROPORTIONALLY with the island
        // (ticket constraint: 70 × 1.5 = 105) so the bigger coast keeps the #226-approved wander character —
        // same angular bay frequency, physically grander bays. default 105 — Sponsor-soak tunes.
        public const float CoastIrregAmp = 105f;   // ±u the coast wanders off the mean (broad bays + headlands)
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
        // radial height field, not the grid shape (deep-sea cells are clipped). 386 seg over ~1490u ≈ 3.86u
        // cells — the SAME cell density as #226's 260/1000 (≈3.9u), so terrain fidelity + NavMesh resolution
        // carry unchanged to the bigger island (a coarser grid would blockier the approved look; a finer one
        // re-opens the perf question harder than the size does).
        public const float GridHalf = 745f;        // square grid half-extent (covers worst coast 705 + clip margin 40)
        public const int Seg = 386;                // subdivision across the big disc (smooth slopes + NavMesh)

        // ============================================================================================
        // THE MOUNTAIN RANGE (ticket 86cahwx6w — island 2.0-A). REAL-WORLD ANCHOR: an island with several
        // mountains reads as RIDGES RISING FROM ONE LANDMASS — connected shoulders and cols between peaks —
        // not cones stamped on a pancake. THREE peaks (default; Sponsor-soak tunes the count/heights):
        //   [0] the HERO — the Sponsor-approved climbable snow-cap peak, its dome constants BYTE-KEPT from
        //       #226/#230 (climb tuning + chunky snow-cap faceting must not regress);
        //   [1][2] two SHORTER, STEEPER non-climbable rock massifs for silhouette variety (Bar 1: asymmetric,
        //       off-centre; heights 105/78 vs the hero's 135 so the side-on skyline steps down a real amount).
        // The combined field is the MAX of the per-peak contributions: where only the hero contributes the
        // field is IDENTICAL to #226 (the hero's approved shape is untouched by construction), and where feet
        // overlap the max forms a natural col/saddle — the connected-ridge read the anchor sentence demands
        // (peak feet DELIBERATELY overlap the hero's so the range hangs together as one massif).
        // ============================================================================================
        public const float MtnCenterX = 90f;       // HERO — off-centre so the island isn't a symmetric bullseye
        public const float MtnCenterZ = -60f;
        public const float MtnPeakHeight = 135f;   // HERO peak height (BYTE-KEPT — the approved climb tuning)
        // Foot spans a BROAD radius so the flank stays CLIMBABLE — max slope under the 45° NavMesh agent max
        // (a raised-cosine dome's steepest mid-flank ≈ π·peak/(2·foot); 135/300 → ~35°, comfortably walkable).
        // A narrower foot made a 59° wall the agent could not climb (the AC3 slope guard caught it).
        public const float MtnFootRadius = 300f;   // the mountain's foot spans this radius (a broad climbable base)

        // HERO banding constants (BYTE-KEPT — these are the #226/#230-approved global values, now carried
        // per-peak so secondaries can band to their own steeper profile without touching the hero's look).
        public const float HeroRockStartFrac = 0.40f;  // rock ramps in from 40% of the hero's height
        public const float HeroRockFullFrac  = 0.62f;  // fully rock by 62%
        public const float HeroTreeLineFrac  = 0.45f;  // scatter rejects trees/grass above 45% (the approved line)
        // SECONDARY (peaked m=1.8) banding: chosen so the ROCK READ covers a comparable share of the FOOT
        // RADIUS as the hero's approved band does on its dome. The hero's 0.40 height-frac lands at ~51% of
        // its foot radius; on the m=1.8 peaked profile 0.40 lands at only ~26% — the "green hill with a rock
        // tip" defect. 0.12 height-frac on m=1.8 lands at ~49% of foot radius (rock coverage matches the
        // approved hero read); fully rock by 0.35 (~29% radius). Tree line 0.10 (~51% radius) sits just
        // OUTSIDE the rock-start ring so trees never stand on stone. defaults — Sponsor-soak tunes.
        public const float CragRockStartFrac = 0.12f;
        public const float CragRockFullFrac  = 0.35f;
        public const float CragTreeLineFrac  = 0.10f;

        /// <summary>One peak of the POC range. The HERO (index 0) uses the #226 raised-cosine dome (broad,
        /// climbable); secondaries use a PEAKED profile — h = H·(1 − sin(t·π/2))^m — whose slope is STEEPEST
        /// AT THE TIP and decreases MONOTONICALLY outward. That monotonicity is load-bearing: the >45° zone is
        /// a single simply-connected summit cap with NOTHING walkable above it, so a steep massif cannot
        /// orphan a walkable NavMesh patch BY CONSTRUCTION (the ticket's no-orphan constraint) — a raised-
        /// cosine secondary would have a flat WALKABLE summit disc cut off by its steep mid-flank ring.</summary>
        public struct Peak
        {
            public float cx, cz;      // centre (world XZ)
            public float height;      // peak height above the plateau
            public float footR;       // contribution fades to 0 here
            public float power;       // dome exponent (hero) / peak exponent m (secondaries)
            public bool climbableDome; // true = hero raised-cosine dome; false = steep peaked profile
            public bool snowCap;      // does this peak's crown read snow? (colour only — faceting is all-peaks)
            // PER-PEAK banding (86cahwx6w capture-pass-2 finding): on the hero's bulging dome, rock-from-40%-
            // height covers ~half the footprint radius — but on a PEAKED profile 40% height is only the top
            // ~quarter of the radius, so a steep crag dressed in the shared band read as "a green hill with a
            // rock tip". Steep bare crags hold no soil (material-honest, Bar 3): secondaries start rock LOW.
            // The hero carries the OLD global values (0.40/0.62/0.45) so its approved look is byte-kept.
            public float rockStartFrac;  // rock colour ramps in from this per-peak height fraction
            public float rockFullFrac;   // fully rock by this fraction
            public float treeLineFrac;   // scatter rejects trees/grass above this fraction of THIS peak
        }

        /// <summary>The range (defaults — Sponsor-soak tunes counts/heights/placement). Index 0 is ALWAYS the
        /// hero and is built from the Mtn* constants above so the approved values cannot drift apart.</summary>
        public static readonly Peak[] Peaks =
        {
            // [0] HERO — Sponsor-approved climbable snow-cap peak (#226 dome, #230 faceting). DO NOT RE-TUNE.
            //     Banding = the OLD global values (Hero* consts) so the approved look is byte-kept.
            new Peak { cx = MtnCenterX, cz = MtnCenterZ, height = MtnPeakHeight, footR = MtnFootRadius,
                       power = 1.25f, climbableDome = true, snowCap = true,
                       rockStartFrac = HeroRockStartFrac, rockFullFrac = HeroRockFullFrac,
                       treeLineFrac = HeroTreeLineFrac },
            // [1] NE massif — shorter + steeper, its own small snow crown (the board's 21h16_13 second peak).
            //     Foot 200 gives it MOUNTAIN MASS (the first capture pass at foot 140 read as a thin
            //     pinnacle, not a massif — author-eyeball reject); power 1.8 keeps the crown steep: tip
            //     slope ≈ atan(1.8·(π/2)·105/200) ≈ 56° (> the 45° agent max — non-climbable crown).
            //     Crag banding (Crag* consts): steep bare crags hold no soil — rock starts LOW.
            //     default height 105 / foot 200 / m 1.8 — Sponsor-soak tunes.
            new Peak { cx = 330f, cz = 150f, height = 105f, footR = 200f,
                       power = 1.8f, climbableDome = false, snowCap = true,
                       rockStartFrac = CragRockStartFrac, rockFullFrac = CragRockFullFrac,
                       treeLineFrac = CragTreeLineFrac },
            // [2] SE massif — smallest + bare ROCK (no snow: silhouette + material variety; Bar 3 material-
            //     honest grey stone). Same mass-vs-needle fix: foot 115→160, power 1.8 → tip ≈ 54°.
            //     default 78/160/m 1.8 — Sponsor-soak tunes.
            new Peak { cx = 250f, cz = -285f, height = 78f, footR = 160f,
                       power = 1.8f, climbableDome = false, snowCap = false,
                       rockStartFrac = CragRockStartFrac, rockFullFrac = CragRockFullFrac,
                       treeLineFrac = CragTreeLineFrac },
        };
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
        // Displacement must be LARGE ENOUGH to VISIBLY break the silhouette + tilt the flat facets (a ±2u
        // perturbation on a 135u peak is invisible — the first pass read as a smooth white dome). With a wide
        // cell the planes are BROAD, so a big amplitude still keeps the inter-vert slope climbable (the added
        // slope is amp·gradient/cell — a wide cell divides it down). ±8u @ 40u cell → the summit reads as
        // distinct angular planes while the added inter-vert slope stays well under the 45° NavMesh max on the
        // near-flat summit (verified by the shipped NavMesh trace + the bounded-slope EditMode guard).
        public const float SnowFacetAmp = 8f;        // ±u of angular displacement on snow verts (climb-bounded by the wide cell)
        public const float SnowFacetCell = 40f;      // world-u cell of the angular snow-facet noise (WIDE = broad chunky
                                                     // planes; a big amp over a wide cell keeps the inter-vert slope climbable)

        // Spawn clearing — a flat clearing OPPOSITE the mountain range so the player starts on gentle sea-level
        // ground and walks ACROSS the island toward the peaks (the "small character, far horizon" read). It CANNOT
        // be the world origin: the hero foot (300u) centred at (90,-60) blankets the origin, so a spawn there
        // sits ~83u UP the flank (run-2 finding). Scaled PROPORTIONALLY with the island (86cahwx6w: ×1.5 from
        // #226's (-180,150)) so the spawn keeps the SAME relative position on the grown island: at (-270,225) —
        // ~459u from the hero centre, well OUTSIDE every peak foot (secondaries are E/SE, 500u+) — the spawn
        // terrain is the flat plateau and the walk toward the hero peak is a ~460u traverse. The whole range
        // sits EAST of the spawn, so the opening vista reads all three silhouettes layered in depth.
        public const float SpawnX = -270f;
        public const float SpawnZ = 225f;
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
        /// ONE peak's height contribution at world XZ (≥ 0). The HERO (climbableDome) is the #226 raised-
        /// cosine dome — BYTE-IDENTICAL to the approved single-mountain formula. Secondaries use the PEAKED
        /// profile h = H·(1 − sin(t·π/2))^m: steepest at the tip (slope m·(π/2)·H/F > 45° by tuning),
        /// monotonically gentler outward — so the crown is un-climbable but the skirt eases into the landmass
        /// (a ridge rising from the island, not a stamped cone) and NO walkable patch is orphaned above the
        /// steep band. Pure function of XZ (deterministic; byte-stable capture). PUBLIC for the shape tests.
        /// </summary>
        public static float PeakHeightAt(int peakIndex, float wx, float wz)
        {
            Peak p = Peaks[peakIndex];
            float dx = wx - p.cx, dz = wz - p.cz;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d >= p.footR) return 0f;
            float t = d / p.footR;                        // 0 centre .. 1 foot
            if (p.climbableDome)
            {
                // Raised cosine dome (the HERO — #226): smooth, walkable slope (no vertical wall) but a
                // confident tall summit. The gentle power keeps the summit rounded + the flanks climbable
                // (max slope well under the NavMesh agent's 45°).
                float dome = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI); // 1 centre .. 0 foot
                return p.height * Mathf.Pow(dome, p.power);
            }
            // PEAKED steep massif: sharp rocky crown, monotone-decreasing slope outward (no-orphan property).
            float shoulder = 1f - Mathf.Sin(t * Mathf.PI * 0.5f);   // 1 centre .. 0 foot, tip-steep
            return p.height * Mathf.Pow(shoulder, p.power);
        }

        /// <summary>
        /// The RANGE height bump at world XZ (≥ 0, added on top of the plateau/hills): the MAX of the per-peak
        /// contributions. MAX (not sum) is the preservation choice: wherever only the hero contributes the
        /// field equals the #226-approved hero dome EXACTLY (climb tuning untouched by construction); where
        /// feet overlap, max forms a natural col/saddle between peaks (the connected-ridge read). PUBLIC so
        /// the shape/perf tests + colour band sample it.
        /// </summary>
        public static float MountainHeightAt(float wx, float wz)
        {
            float h = 0f;
            for (int i = 0; i < Peaks.Length; i++)
            {
                float c = PeakHeightAt(i, wx, wz);
                if (c > h) h = c;
            }
            return h;
        }

        /// <summary>
        /// The mountain-height FRACTION at world XZ (0 at/below every foot .. 1 at a summit) — PER-PEAK
        /// relative: the max over peaks of (contribution / that peak's OWN height). On the hero this is
        /// byte-identical to the #226 single-peak fraction; on a secondary it reaches 1 at ITS summit, so the
        /// rock banding + the crown facet zone scale to each massif proportionally (a 78u massif gets a
        /// faceted rocky crown near ITS top, not at 66% of the hero's height it can never reach).
        /// The facet zone + scatter rejection key off this. PUBLIC so the faceting tests sample it.
        /// </summary>
        public static float MountainHeightFracAt(float wx, float wz)
        {
            float f = 0f;
            for (int i = 0; i < Peaks.Length; i++)
            {
                float c = PeakHeightAt(i, wx, wz) / Peaks[i].height;
                if (c > f) f = c;
            }
            return Mathf.Clamp01(f);
        }

        /// <summary>
        /// The SNOW fraction at world XZ — like <see cref="MountainHeightFracAt"/> but over SNOW-CAPPED peaks
        /// only. ColorAt's snow band keys off this, so a snowCap=false massif (bare rock, Bar 3 material-
        /// honest) never whitens even though its crown still facets. On the hero: byte-identical to #226.
        /// </summary>
        public static float SnowFracAt(float wx, float wz)
        {
            float f = 0f;
            for (int i = 0; i < Peaks.Length; i++)
            {
                if (!Peaks[i].snowCap) continue;
                float c = PeakHeightAt(i, wx, wz) / Peaks[i].height;
                if (c > f) f = c;
            }
            return Mathf.Clamp01(f);
        }

        /// <summary>
        /// The ROCK-BAND blend (0 grass .. 1 fully rock) at world XZ — PER-PEAK (86cahwx6w capture-pass-2):
        /// each peak ramps rock in over ITS OWN rockStartFrac..rockFullFrac height band, and the max wins.
        /// On the hero this is byte-identical to the old global 0.40..0.62 SmoothStep; on a peaked m=1.8
        /// massif the band starts LOW so the crag reads STONE over a comparable share of its foot radius
        /// (the global band rocked only its top ~quarter → "a green hill with a rock tip"; steep bare crags
        /// hold no soil — Bar 3 material-honest). PUBLIC so the banding tests sample it.
        /// </summary>
        public static float RockBandT(float wx, float wz)
        {
            float t = 0f;
            for (int i = 0; i < Peaks.Length; i++)
            {
                Peak p = Peaks[i];
                float frac = PeakHeightAt(i, wx, wz) / p.height;
                float ti = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(p.rockStartFrac, p.rockFullFrac, frac));
                if (ti > t) t = ti;
            }
            return t;
        }

        /// <summary>
        /// True when world XZ is above SOME peak's tree line (per-peak treeLineFrac of that peak's OWN
        /// height) — the scatter rejects trees/grass here so a forest never grows up a snow cap or stands
        /// on a crag's low-starting rock band. Hero keeps the approved 0.45 line; crag lines sit lower,
        /// just outside their rock-start ring. PUBLIC — the scatter + the tree-line tests consume it.
        /// </summary>
        public static bool AboveTreeLine(float wx, float wz)
        {
            for (int i = 0; i < Peaks.Length; i++)
                if (PeakHeightAt(i, wx, wz) / Peaks[i].height > Peaks[i].treeLineFrac) return true;
            return false;
        }

        /// <summary>True if this XZ is in the crown FACET zone (per-peak height fraction >= SnowFacetFrac) —
        /// its terrain face gets flat per-face normals + the angular displacement (the chunky cap). Applies
        /// to EVERY peak's crown (86cahwx6w): the hero's snow cap stays the #230-approved chunky faceting,
        /// and a bare-rock massif's crown reads as angular broken stone (same idiom, rock colour).</summary>
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

            // THE MOUNTAIN RANGE — the walkable hero + steep secondary massifs (max-combined), added on the
            // interior land only. Fades off at the shore via the SAME landMask so a coast-clipped mountain
            // edge can't punch through the waterline.
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
                // PER-FACE VALUE CONTRAST (the FacetedRock signal, LowPolyMeshes:444) — reinforce the flat-shading
                // so adjacent snow planes read as DISTINCT facets even under the near-white albedo + bloom (the
                // in-shader ndotl alone washes out on white). Up-facing facets stay bright; tilted facets a touch
                // dimmer. Kept in a HIGH band (>=0.80) so it still reads as SNOW (never grey) — the summit stays
                // white for the snow-cap guard. The snow albedo is near-white, so this modest step is what makes
                // the crystalline plane-to-plane break visible from the side profile.
                float faceUp = Mathf.Clamp01(fn.y);                 // 1 flat-up .. 0 vertical
                float faceVal = Mathf.Lerp(0.80f, 1.0f, faceUp);    // dimmer tilted facets .. bright tops (never grey)
                Color c0 = cols[a] * faceVal, c1 = cols[b] * faceVal, c2 = cols[c] * faceVal;
                c0.a = cols[a].a; c1.a = cols[b].a; c2.a = cols[c].a; // preserve alpha (AO channel)
                int bi = outVerts.Count;
                outVerts.Add(v0); outVerts.Add(v1); outVerts.Add(v2);
                outCols.Add(c0); outCols.Add(c1); outCols.Add(c2);
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
        /// Island vertex COLOUR: grass over the interior, warm sand at the beach, and on the MOUNTAIN RANGE a
        /// grass→bare-rock→SNOW banding by PER-PEAK height fraction (the height-threshold snow cap — NO
        /// texture; snow only on snowCap peaks). Foam near the warped waterline. PUBLIC so the snow-threshold
        /// + banding tests sample it. The banding is keyed per-peak-relative, so each summit reads as a real
        /// snow-capped (or bare-rock) crown rather than white-everywhere-high.
        /// </summary>
        public static Color ColorAt(float wx, float wz, float height, float ox, float oz, int seed)
        {
            float r = Mathf.Sqrt(wx * wx + wz * wz);
            float coast = ShoreRadiusAt(wx, wz, ox, oz);

            // Base grass over the interior (brighter toward the centre / on sunlit rises).
            Color grass = Color.Lerp(GrassLo, GrassHi, Mathf.Clamp01(Mathf.InverseLerp(coast, 0f, r)));
            grass = Color.Lerp(grass, GrassRise, Mathf.Clamp01((height - 6f) * 0.06f));

            // MOUNTAIN banding by the PER-PEAK height fraction (the snow-cap read). Grass at each foot → bare
            // rock up each flank → SNOW near a snow-capped summit. PER-PEAK relative (86cahwx6w): each massif
            // bands proportionally to its OWN height, so the 105u NE peak carries its own small rock band +
            // snow crown and the 78u SE massif reads bare grey stone to its top (snowCap=false → SnowFracAt
            // never rises there). A rolling hill inland has NO peak contribution and never banding.
            float heightFrac = MountainHeightFracAt(wx, wz);  // max over ALL peaks, per-peak relative
            float snowFrac = SnowFracAt(wx, wz);              // max over SNOW-CAPPED peaks only
            Color land = grass;
            // Rock ramps in over each peak's OWN rockStartFrac..rockFullFrac band (per-peak — see RockBandT;
            // hero byte-kept at 0.40..0.62); snow starts at SnowlineFrac (snow-capped peaks only).
            float rockT = RockBandT(wx, wz);
            Color rock = Color.Lerp(RockLo, RockHi, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, SnowlineFrac, heightFrac)));
            land = Color.Lerp(land, rock, rockT);
            // SNOW cap above the snow line (height-threshold white — the constraint: NO snow texture).
            float snowT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SnowlineFrac, SnowlineFrac + 0.12f, snowFrac));
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

            // Scaled with the island (86cahwx6w): the worst-case coast is now MeanShoreR+CoastIrregAmp=705u,
            // and the Exp² fog (density 0.0016) needs ~1100u+ of open sea past the coast to fully dissolve the
            // sea edge (at #226's 1130u span the edge read 96% fogged; 1900−705=1195u keeps that). waterSeg
            // scales with the extent so the foam-ring vert density (~18u cells) matches the approved #226 read.
            const float halfExtent = 1900f; // reaches well past the ~1200u island to the fog horizon, all sides
            const int waterSeg = 212;       // fine enough that the warped foam ring lands verts every quadrant
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
