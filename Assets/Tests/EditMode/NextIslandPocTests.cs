using System.Linq;
using NUnit.Framework;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// NEXT-ISLAND POC geometry + snow-cap + regression guards (ticket 86caa9zpp — a much-bigger organic
    /// walkable island with ONE dominant WALKABLE snow-capped mountain). The height/colour fields are sampled
    /// DIRECTLY (no build) to prove the shape + the height-threshold snow cap; each assert pins an AC / a
    /// standing bar so a regression (the POC gen breaking in a future PR) fails headlessly in CI.
    ///
    /// REGRESSION GUARD (ticket requirement): AC3_HeroMountain_HasHeightThresholdSnowCap +
    /// AC1_Coastline_IsOrganicAndMuchBigger are the named tests that fail if this POC's gen breaks later —
    /// they assert the snow-threshold-white-material presence + the peak height + the organic much-bigger
    /// outline directly off NextIslandPocGen (no scene rig).
    /// </summary>
    public class NextIslandPocTests
    {
        private static void Off(out float ox, out float oz) =>
            NextIslandPocGen.SeedOffset(NextIslandPocScene.PocSeed, out ox, out oz);

        // ---- AC1 / Bar 1 — ORGANIC IRREGULAR coastline, MUCH bigger than the start island ----

        [Test]
        public void AC1_Coastline_IsOrganicAndMuchBigger()
        {
            Off(out float ox, out float oz);
            const int azimuths = 48;
            var coast = new System.Collections.Generic.List<float>();
            for (int a = 0; a < azimuths; a++)
            {
                float ang = a / (float)azimuths * Mathf.PI * 2f;
                coast.Add(NextIslandPocGen.ShoreRadiusAt(Mathf.Cos(ang), Mathf.Sin(ang), ox, oz));
            }
            float min = coast.Min(), max = coast.Max(), mean = coast.Average();
            // IRREGULAR (Bar 1): the coast must wander a real amount (a circle spreads ~0).
            Assert.Greater(max - min, NextIslandPocGen.CoastIrregAmp * 0.6f,
                $"the POC coast must be IRREGULAR (bays + headlands) — spread {max - min:F0}u must exceed " +
                $"~0.6×CoastIrregAmp; a circle spreads ~0.");
            float variance = coast.Select(r => (r - mean) * (r - mean)).Average();
            Assert.Greater(Mathf.Sqrt(variance), NextIslandPocGen.CoastIrregAmp * 0.22f,
                "the POC coast stddev must be a real fraction of the warp amplitude (not a circle).");
            // MUCH BIGGER: the POC island diameter must dwarf the start island's ~240u (IslandShoreR 120).
            float pocDiameter = NextIslandPocGen.MeanShoreR * 2f;
            Assert.Greater(pocDiameter, LowPolyZoneGen.IslandShoreR * 2f * 2.5f,
                $"the POC island ({pocDiameter:F0}u diameter) must be MUCH bigger than the start island " +
                $"({LowPolyZoneGen.IslandShoreR * 2f:F0}u) — at least 2.5× (feels-big, AC1).");
        }

        [Test]
        public void AC1_WaterOnAllSides_InteriorIsLand_FarOutIsSea()
        {
            Off(out float ox, out float oz);
            const int azimuths = 24;
            for (int a = 0; a < azimuths; a++)
            {
                float ang = a / (float)azimuths * Mathf.PI * 2f;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                float coast = NextIslandPocGen.ShoreRadiusAt(dx, dz, ox, oz);
                float inland = coast - 60f, faroff = coast + 60f;
                Assert.Greater(NextIslandPocGen.HeightAtRadial(dx * inland, dz * inland, ox, oz), NextIslandPocGen.WaterY,
                    $"azimuth {a * 15}deg interior (r={inland:F0}) must be LAND (above sea)");
                Assert.Less(NextIslandPocGen.HeightAtRadial(dx * faroff, dz * faroff, ox, oz), NextIslandPocGen.WaterY,
                    $"azimuth {a * 15}deg far (r={faroff:F0}) must be SEA (water on this side)");
            }
        }

        // ---- AC2 — CROSS-TIME lands in the ~2-3 min band at WASD walk/run speed (a tunable DEFAULT) ----

        [Test]
        public void AC2_CrossTime_IsInTheTwoToThreeMinuteBand()
        {
            // Edge-to-edge crossing distance ~= the island diameter (~2×MeanShoreR). At the shipped WASD walk
            // (5.5 u/s) a full walk crossing should be ~2-3 min; a run (9.5 u/s) faster. A realistic mixed
            // walk/run therefore lands squarely in the 2-3 min band. Assert the WALK crossing is >= ~2 min
            // (feels big) and the RUN crossing is <= ~3 min (not a slog) — the band the ticket targets.
            const float walk = 5.5f, run = 9.5f;
            float crossing = NextIslandPocGen.MeanShoreR * 2f;
            float walkMin = crossing / walk / 60f;
            float runMin = crossing / run / 60f;
            Assert.GreaterOrEqual(walkMin, 2.0f,
                $"a WALK crossing ({walkMin:F1} min over {crossing:F0}u at {walk} u/s) must feel BIG (>=2 min).");
            Assert.LessOrEqual(runMin, 3.2f,
                $"a RUN crossing ({runMin:F1} min at {run} u/s) must not be a slog (<=~3 min) — the 2-3 min band.");
        }

        // ---- AC3 / Bar 4 — the HERO MOUNTAIN: a real CLIMBABLE giant hill with a height-threshold SNOW cap ----

        [Test]
        public void AC3_HeroMountain_IsAConfidentGiantHill_NotAHorizonBackdrop()
        {
            // The mountain must be a REAL tall mass: the peak height dwarfs the rolling hills, and the summit
            // sits well ABOVE the surrounding land (a giant hill, not a bump).
            Off(out float ox, out float oz);
            Assert.Greater(NextIslandPocGen.MtnPeakHeight, NextIslandPocGen.HillAmp * 4f,
                $"the hero peak ({NextIslandPocGen.MtnPeakHeight:F0}u) must dwarf the rolling hills " +
                $"({NextIslandPocGen.HillAmp:F0}u) — a dominant giant hill (AC3).");
            float summitY = NextIslandPocGen.HeightAtRadial(NextIslandPocGen.MtnCenterX, NextIslandPocGen.MtnCenterZ, ox, oz);
            Assert.Greater(summitY, NextIslandPocGen.MtnPeakHeight * 0.7f,
                $"the summit terrain Y ({summitY:F0}u) must reach most of the peak height — a real tall summit.");
            // ONE dominant peak: the summit is far higher than any non-mountain interior sample (not two peaks).
            float highestNonMtn = 0f;
            for (int a = 0; a < 24; a++)
            for (float rr = 40f; rr < NextIslandPocGen.CoreR; rr += 40f)
            {
                float ang = a / 24f * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                // Skip samples inside the mountain foot (we want the NON-mountain land height).
                if (Vector2.Distance(new Vector2(x, z), new Vector2(NextIslandPocGen.MtnCenterX, NextIslandPocGen.MtnCenterZ))
                    < NextIslandPocGen.MtnFootRadius) continue;
                float y = NextIslandPocGen.HeightAtRadial(x, z, ox, oz);
                if (y > highestNonMtn) highestNonMtn = y;
            }
            Assert.Greater(summitY, highestNonMtn * 3f,
                $"the hero summit ({summitY:F0}u) must DOMINATE the rest of the island ({highestNonMtn:F0}u) — ONE peak, not many.");
        }

        [Test]
        public void AC3_HeroMountain_IsClimbable_SlopeUnderNavMeshAgentMax()
        {
            // The hero mountain's OWN flank (the raised-cosine dome) must be WALKABLE — its max slope under the
            // default NavMesh agent max (45°) so the baked NavMesh covers the peak (a vertical wall would orphan
            // it). Measure the DOME contribution (MountainHeightAt) radially — this isolates the mountain shape
            // AC3 is about, independent of the small-scale rolling-hill noise + the coast drop (which
            // HeightAtRadial layers on and are verified separately by the NavMesh-coverage trace + the walkable
            // PlayMode gate). A comfortable margin (<42°) under 45° keeps the baked flank continuous.
            float dr = 4f;
            float maxSlopeDeg = 0f;
            for (float rr = 2f; rr < NextIslandPocGen.MtnFootRadius; rr += dr)
            {
                float h0 = NextIslandPocGen.MountainHeightAt(NextIslandPocGen.MtnCenterX + rr, NextIslandPocGen.MtnCenterZ);
                float h1 = NextIslandPocGen.MountainHeightAt(NextIslandPocGen.MtnCenterX + rr + dr, NextIslandPocGen.MtnCenterZ);
                float slope = Mathf.Atan2(Mathf.Abs(h1 - h0), dr) * Mathf.Rad2Deg;
                if (slope > maxSlopeDeg) maxSlopeDeg = slope;
            }
            Assert.Less(maxSlopeDeg, 42f,
                $"the hero mountain dome max slope ({maxSlopeDeg:F1}°) must be comfortably under the 45° NavMesh " +
                "agent max so the whole peak is CLIMBABLE (a walkable giant hill, not a wall — AC3/Bar4).");
        }

        [Test]
        public void AC3_HeroMountain_HasHeightThresholdSnowCap()
        {
            // The snow cap is a HEIGHT-THRESHOLD WHITE VERTEX COLOUR (NO texture): the summit reads WHITE
            // (snow), the foot reads GREEN (grass), and the mid-flank reads ROCK (not white) — the real
            // grass→rock→snow banding of a snow-capped mountain. This is the REGRESSION GUARD for the snow cap.
            Off(out float ox, out float oz);

            // Summit vertex colour must be near-WHITE (the snow cap).
            float sy = NextIslandPocGen.HeightAtRadial(NextIslandPocGen.MtnCenterX, NextIslandPocGen.MtnCenterZ, ox, oz);
            Color summit = NextIslandPocGen.ColorAt(NextIslandPocGen.MtnCenterX, NextIslandPocGen.MtnCenterZ, sy, ox, oz, NextIslandPocScene.PocSeed);
            Assert.Greater(Mathf.Min(summit.r, summit.g, summit.b), 0.78f,
                $"the mountain SUMMIT must read SNOW (near-white) — got {summit} (min channel <=0.78 is not snow).");

            // Foot of the mountain (near the foot radius) must read GRASS (green: g clearly the largest channel).
            float footR = NextIslandPocGen.MtnFootRadius * 0.9f;
            float fx = NextIslandPocGen.MtnCenterX + footR, fz = NextIslandPocGen.MtnCenterZ;
            float fy = NextIslandPocGen.HeightAtRadial(fx, fz, ox, oz);
            Color foot = NextIslandPocGen.ColorAt(fx, fz, fy, ox, oz, NextIslandPocScene.PocSeed);
            Assert.Greater(foot.g, foot.r, "the mountain FOOT must read GRASS (green dominant), not snow.");
            Assert.Greater(foot.g, foot.b, "the mountain FOOT must read GRASS (green dominant), not snow.");
            Assert.Less(Mathf.Min(foot.r, foot.g, foot.b), 0.7f, "the FOOT must NOT be white (snow only at the top).");

            // Mid-flank (~60% up) must read ROCK (grey-ish: r,g,b close together, NOT green-dominant, NOT white).
            // Find a radius where the mountain contribution is ~60% of the peak.
            float midR = 0f;
            for (float rr = 4f; rr < NextIslandPocGen.MtnFootRadius; rr += 2f)
            {
                float mh = NextIslandPocGen.MountainHeightAt(NextIslandPocGen.MtnCenterX + rr, NextIslandPocGen.MtnCenterZ);
                if (mh / NextIslandPocGen.MtnPeakHeight <= 0.6f) { midR = rr; break; }
            }
            float mx = NextIslandPocGen.MtnCenterX + midR, mz = NextIslandPocGen.MtnCenterZ;
            float my = NextIslandPocGen.HeightAtRadial(mx, mz, ox, oz);
            Color mid = NextIslandPocGen.ColorAt(mx, mz, my, ox, oz, NextIslandPocScene.PocSeed);
            // Rock: not white (some channel < snow), and NOT strongly green-dominant like grass.
            Assert.Less(Mathf.Min(mid.r, mid.g, mid.b), 0.78f, "the mid-flank must read bare ROCK, not snow-white.");
            Assert.Less(mid.g - mid.r, 0.12f, "the mid-flank must read grey ROCK, not green grass (g not strongly > r).");
        }

        [Test]
        public void SnowCap_IsMountainRelative_NotWhiteEverywhereHigh()
        {
            // The snow-cap threshold is keyed on the MOUNTAIN contribution, not raw height — so a tall rolling
            // HILL inland (which has NO mountain contribution) must NOT read snow, even if its Y is high.
            Off(out float ox, out float oz);
            // A point far from the mountain but with a real hill height (interior). Its colour must NOT be white.
            for (int a = 0; a < 16; a++)
            {
                float ang = a / 16f * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * 120f, z = Mathf.Sin(ang) * 120f;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(NextIslandPocGen.MtnCenterX, NextIslandPocGen.MtnCenterZ))
                    < NextIslandPocGen.MtnFootRadius) continue; // skip the mountain footprint
                float y = NextIslandPocGen.HeightAtRadial(x, z, ox, oz);
                Color c = NextIslandPocGen.ColorAt(x, z, y, ox, oz, NextIslandPocScene.PocSeed);
                Assert.Less(Mathf.Min(c.r, c.g, c.b), 0.78f,
                    $"a non-mountain interior point (r=120, {x:F0},{z:F0}, y={y:F0}) must NOT read snow-white " +
                    "— snow is MOUNTAIN-relative (only the hero peak snows), not white-everywhere-high.");
            }
        }

        // ---- SPAWN — the player spawns on FLAT sea-level ground, OFF the mountain flank (run-2 regression) ----

        [Test]
        public void Spawn_IsFlatSeaLevelGround_NotUpTheMountainFlank()
        {
            // REGRESSION GUARD (run-2 bug): the mountain foot (300u) centred at (90,-60) blankets the world ORIGIN,
            // so a spawn at the origin sat ~83u UP the flank — the player started elevated, not on gentle ground.
            // The spawn moved to (SpawnX,SpawnZ), which MUST be OUTSIDE the mountain foot (near-zero mountain
            // contribution) and on flat land near sea level. If a future re-tune drifts the spawn back under the
            // foot, this reds.
            Off(out float ox, out float oz);
            float spawnMtn = NextIslandPocGen.MountainHeightAt(NextIslandPocGen.SpawnX, NextIslandPocGen.SpawnZ);
            Assert.Less(spawnMtn, 1.0f,
                $"the SPAWN must be OUTSIDE the mountain foot — mountain contribution at spawn ({spawnMtn:F1}u) must " +
                "be ~0 (a spawn on the flank starts the player 80u up a hill, not on gentle ground).");
            float spawnY = NextIslandPocGen.HeightAtRadial(NextIslandPocGen.SpawnX, NextIslandPocGen.SpawnZ, ox, oz);
            Assert.Less(spawnY, 3.0f,
                $"the SPAWN terrain Y ({spawnY:F1}u) must be near sea level (flat clearing) — not up the flank.");
            Assert.Greater(spawnY, NextIslandPocGen.WaterY,
                $"the SPAWN ({spawnY:F1}u) must be on LAND (above the sea).");
            // And the spawn must be far enough from the mountain that walking to the peak is a real traverse.
            float distToMtn = Mathf.Sqrt(
                (NextIslandPocGen.SpawnX - NextIslandPocGen.MtnCenterX) * (NextIslandPocGen.SpawnX - NextIslandPocGen.MtnCenterX) +
                (NextIslandPocGen.SpawnZ - NextIslandPocGen.MtnCenterZ) * (NextIslandPocGen.SpawnZ - NextIslandPocGen.MtnCenterZ));
            Assert.Greater(distToMtn, NextIslandPocGen.MtnFootRadius,
                $"the spawn ({distToMtn:F0}u from the mountain centre) must be OUTSIDE the foot ({NextIslandPocGen.MtnFootRadius:F0}u) " +
                "— the player walks ACROSS toward the peak.");
        }

        // ---- AC5 — the START ISLAND is UNTOUCHED (the POC uses its OWN constants) ----

        [Test]
        public void AC5_StartIsland_Untouched_PocUsesItsOwnConstants()
        {
            // The POC must NOT have re-tuned the seed-42 start island — its constants stay at the shipped values
            // (this fails if a future edit accidentally couples the POC to LowPolyZoneGen). Pin the load-bearing
            // start-island constants the POC could have disturbed.
            Assert.AreEqual(120f, LowPolyZoneGen.IslandShoreR, "start-island IslandShoreR must stay 120 (untouched).");
            Assert.AreEqual(42, LowPolyZoneGen.IslandSeed, "start-island IslandSeed must stay 42 (untouched).");
            Assert.AreEqual(9.0f, LowPolyZoneGen.IslandHillAmp, 0.001f, "start-island IslandHillAmp must stay 9.0 (untouched).");
            // The POC is its OWN, much bigger island — a distinct mean shore radius (proves it did not reuse
            // the start-island size).
            Assert.AreNotEqual(LowPolyZoneGen.IslandShoreR, NextIslandPocGen.MeanShoreR,
                "the POC must be its OWN size, not the start island's IslandShoreR.");
        }

        // ---- The POC ships as a STAND-ALONE scene registered path (not Boot.unity) ----

        [Test]
        public void Poc_ScenePath_IsStandalone_NotBoot()
        {
            Assert.AreEqual("Assets/Scenes/NextIslandPoc.unity", NextIslandPocScene.PocScenePath,
                "the POC scene must be a STAND-ALONE scene (AC1), NOT Boot.unity.");
            Assert.AreNotEqual("Assets/NavMesh/PlayNavMesh.asset", NextIslandPocScene.PocNavMeshPath,
                "the POC NavMesh must be its OWN asset (not the start island's PlayNavMesh).");
            Assert.AreNotEqual("Assets/NavMesh/BootNavMesh.asset", NextIslandPocScene.PocNavMeshPath,
                "the POC NavMesh must be its OWN asset (not the start island's BootNavMesh).");
        }

        // ---- CALIBRATION — the runtime verify-capture + the PlayMode mirror stay in lockstep with the gen ----

        [Test]
        public void VerifyCapture_MountainConstants_MatchTheGen()
        {
            // PocIslandVerifyCapture duplicates the mountain centre/peak/foot as plain floats (the runtime
            // capture can't reference the editor gen). Pin them to the gen so a re-tune of the mountain can't
            // silently desync the side-profile camera framing.
            var cap = new GameObject().AddComponent<FarHorizon.PocIslandVerifyCapture>();
            try
            {
                Assert.AreEqual(NextIslandPocGen.MtnCenterX, cap.mountainCenter.x, 0.001f, "capture MtnCenterX must match the gen.");
                Assert.AreEqual(NextIslandPocGen.MtnCenterZ, cap.mountainCenter.z, 0.001f, "capture MtnCenterZ must match the gen.");
                Assert.AreEqual(NextIslandPocGen.MtnPeakHeight, cap.mountainPeakHeight, 0.001f, "capture peak height must match the gen.");
                Assert.AreEqual(NextIslandPocGen.MtnFootRadius, cap.mountainFootRadius, 0.001f, "capture foot radius must match the gen.");
            }
            finally { Object.DestroyImmediate(cap.gameObject); }
        }

        [Test]
        public void PlayModeMirror_MountainProportion_MatchesTheGen_SoTheSlopeMatches()
        {
            // The PlayMode test uses a SCALED-DOWN mountain (PeakH 27 / FootR 60) whose peak/foot PROPORTION
            // must match the gen's (135/300) so the SLOPE — the walkable-vs-wall property under test — is the
            // same. If the gen's proportion changes, this fails → a signal to re-scale the PlayMode mirror.
            const float mirrorPeak = 27f, mirrorFoot = 60f; // must match NextIslandPocPlayModeTests
            float genRatio = NextIslandPocGen.MtnPeakHeight / NextIslandPocGen.MtnFootRadius;
            float mirrorRatio = mirrorPeak / mirrorFoot;
            Assert.AreEqual(genRatio, mirrorRatio, 0.02f,
                $"the PlayMode mirror peak/foot ratio ({mirrorRatio:F3}) must match the gen ({genRatio:F3}) so " +
                "the slope (walkable-vs-wall) is the same — re-scale the PlayMode mirror if the gen changes.");
        }

        // ---- MESH INTEGRATION — the built POC terrain carries the snow verts + is clipped + has a collider ----

        [Test]
        public void BuiltPocTerrain_HasSnowWhiteVerts_IsClipped_AndCarriesACollider()
        {
            // Build the POC island (terrain + water) headlessly and assert the SHIPPED mesh actually carries
            // the snow-cap white vertex colours + is clipped to the landmass + has a MeshCollider (the walkable
            // surface). Fallback materials (URP/Lit) are fine here — we assert the MESH data, not shading.
            var parent = new GameObject("PocTestParent");
            try
            {
                var vcMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                GameObject ground = NextIslandPocGen.BuildPocIsland(parent, NextIslandPocScene.PocSeed, vcMat, waterMat);
                Assert.IsNotNull(ground, "BuildPocIsland must return the ground object.");

                var mf = ground.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf?.sharedMesh, "the POC terrain mesh must be built.");
                var mesh = mf.sharedMesh;
                Assert.Greater(mesh.colors.Length, 0, "the POC terrain must carry per-vertex colours.");

                // At least one vertex must be near-WHITE (the snow cap) — the height-threshold snow present.
                int snowVerts = mesh.colors.Count(c => Mathf.Min(c.r, c.g, c.b) > 0.80f);
                Assert.Greater(snowVerts, 0,
                    "the built POC terrain must carry SNOW-white vertices (the height-threshold snow cap present).");

                // CLIPPED: fewer triangles than a full square grid (deep-sea cells dropped → no square edge).
                int fullGrid = NextIslandPocGen.Seg * NextIslandPocGen.Seg * 2;
                int actual = mesh.triangles.Length / 3;
                Assert.Less(actual, (int)(fullGrid * 0.92f),
                    $"the POC terrain must be CLIPPED to the landmass — {actual} tris vs a full {fullGrid}-tri grid.");

                Assert.IsNotNull(ground.GetComponent<MeshCollider>(), "the POC terrain must carry a MeshCollider (the walkable surface).");

                // Water on all sides: a Water_Poc child reaching well past the coast.
                var water = FindChild(parent.transform, "Water_Poc");
                Assert.IsNotNull(water, "the POC must build the all-sides sea (Water_Poc).");
                var wmr = water.GetComponent<MeshRenderer>();
                Assert.Greater(wmr.bounds.size.x, NextIslandPocGen.MeanShoreR * 2f,
                    "the POC sea must extend well past the island on all sides.");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        // ---- SNOW-CAP FACETING (ticket 86cahmxh6) — the snow ZONE reads chunky/faceted, not a smooth dome ----

        [Test]
        public void SnowZone_IsFaceted_LowerZonesStaySmooth()
        {
            // REGRESSION GUARD (ticket 86cahmxh6): the SNOW-facet zone of the built terrain must carry FLAT
            // per-face normals (adjacent snow faces differ sharply → an angular chunky cap), while the
            // grass/rock lower zones stay WELDED-SMOOTH (adjacent faces share near-equal normals → the Zone-D
            // dune look, unchanged). Catches the BUG CLASS — if a future edit reverts the snow to smooth-welded
            // (e.g. drops the flat-normal pass or RecalculateNormals-es the whole mesh), this reds.
            var parent = new GameObject("PocFacetParent");
            try
            {
                var vcMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                GameObject ground = NextIslandPocGen.BuildPocIsland(parent, NextIslandPocScene.PocSeed, vcMat, waterMat);
                var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
                var verts = mesh.vertices;
                var normals = mesh.normals;
                var tris = mesh.triangles;
                Assert.AreEqual(verts.Length, normals.Length, "the mesh must carry explicit per-vertex normals.");

                // Classify each triangle by its centroid (snow-facet zone vs not) and, per class, measure the
                // spread of face-vs-vertex-normal agreement. A FLAT face has all 3 vertex normals == the face
                // normal (dot ~1); a SMOOTH welded region has vertex normals that are AVERAGES (dot < 1 on
                // slopes, and adjacent faces' normals diverge from the per-triangle face normal).
                int snowFlat = 0, snowTotal = 0, smoothWelded = 0, smoothTotal = 0;
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    Vector3 v0 = verts[a], v1 = verts[b], v2 = verts[c];
                    float cxw = (v0.x + v1.x + v2.x) / 3f;
                    float czw = (v0.z + v1.z + v2.z) / 3f;
                    Vector3 fn = Vector3.Cross(v1 - v0, v2 - v0);
                    if (fn.sqrMagnitude < 1e-10f) continue;
                    fn.Normalize();
                    // A FLAT-shaded face: all three vertex normals equal the face normal (within eps).
                    bool flat = Vector3.Dot(normals[a], fn) > 0.999f
                             && Vector3.Dot(normals[b], fn) > 0.999f
                             && Vector3.Dot(normals[c], fn) > 0.999f;
                    if (NextIslandPocGen.IsSnowFacetZone(cxw, czw))
                    {
                        snowTotal++; if (flat) snowFlat++;
                    }
                    else
                    {
                        smoothTotal++;
                        // "welded-smooth": at least one vertex normal is an AVERAGE (dot < 0.999 with THIS
                        // face's normal) — i.e. the vertex is shared by faces of differing orientation.
                        if (Vector3.Dot(normals[a], fn) < 0.999f
                         || Vector3.Dot(normals[b], fn) < 0.999f
                         || Vector3.Dot(normals[c], fn) < 0.999f) smoothWelded++;
                    }
                }

                Assert.Greater(snowTotal, 0, "there must be a snow-facet zone on the hero peak.");
                // The snow zone must be OVERWHELMINGLY flat-shaded (the chunky cap). Allow a tiny slack for
                // degenerate/boundary faces.
                Assert.Greater(snowFlat / (float)snowTotal, 0.9f,
                    $"the SNOW zone must be FLAT-shaded (chunky/faceted) — {snowFlat}/{snowTotal} snow faces carry " +
                    "per-face normals; a smooth-welded cap would score ~0 (ticket 86cahmxh6).");
                // The lower (grass/rock) zones must stay welded-smooth on their SLOPES — a meaningful fraction of
                // non-snow faces share averaged (non-face) vertex normals (the dune look, unchanged). Flat plateau
                // faces legitimately read as flat, so this is a presence check, not a majority.
                Assert.Greater(smoothWelded, 0,
                    "the grass/rock lower zones must stay WELDED-SMOOTH (averaged normals) — the Zone-D dune look " +
                    "must NOT be wholesale flat-shaded (lowpoly-quality §3).");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void SnowFacetDisplacement_IsBounded_AndClimbable()
        {
            // The snow-facet displacement must be CLIMB-BOUNDED: (1) it is 0 outside the snow zone (grass/rock
            // heights untouched — climbability of the flank as-approved); (2) inside the snow zone its magnitude
            // never exceeds SnowFacetAmp; (3) the ADDED local slope from the displacement between adjacent grid
            // verts stays comfortably under the 45° NavMesh agent max, so the faceted cap does not orphan the
            // summit from the walkable surface (the shipped -verifyPocIsland NavMesh trace is the ground truth;
            // this is the deterministic guard). The Sponsor APPROVED the climb — this pins it.
            int seed = NextIslandPocScene.PocSeed;

            // (1) zero outside the snow zone — sample the mid-flank (below SnowFacetFrac) + the flat interior.
            float midR = 0f;
            for (float rr = 4f; rr < NextIslandPocGen.MtnFootRadius; rr += 2f)
                if (NextIslandPocGen.MountainHeightFracAt(NextIslandPocGen.MtnCenterX + rr, NextIslandPocGen.MtnCenterZ) <= 0.55f)
                { midR = rr; break; }
            Assert.AreEqual(0f,
                NextIslandPocGen.SnowFacetDisplace(NextIslandPocGen.MtnCenterX + midR, NextIslandPocGen.MtnCenterZ, seed), 1e-4f,
                "the snow-facet displacement must be 0 on the mid-flank (below the snow zone — grass/rock untouched).");
            Assert.AreEqual(0f, NextIslandPocGen.SnowFacetDisplace(0f, 0f, seed), 1e-4f,
                "the snow-facet displacement must be 0 far from the mountain (flat interior untouched).");

            // (2)+(3) inside the snow zone: bounded magnitude + climbable added slope between adjacent grid verts.
            float cell = NextIslandPocGen.GridHalf * 2f / NextIslandPocGen.Seg; // ~3.85u grid step
            float maxAbs = 0f, maxAddedSlopeDeg = 0f;
            int samples = 0;
            // Walk a fine XZ raster over the snow-cap footprint and measure the displacement gradient.
            for (float x = NextIslandPocGen.MtnCenterX - 80f; x <= NextIslandPocGen.MtnCenterX + 80f; x += cell)
            for (float z = NextIslandPocGen.MtnCenterZ - 80f; z <= NextIslandPocGen.MtnCenterZ + 80f; z += cell)
            {
                if (!NextIslandPocGen.IsSnowFacetZone(x, z)) continue;
                samples++;
                float d = NextIslandPocGen.SnowFacetDisplace(x, z, seed);
                if (Mathf.Abs(d) > maxAbs) maxAbs = Mathf.Abs(d);
                // added-slope contribution from the displacement gradient in +X and +Z (adjacent grid verts).
                float dx = NextIslandPocGen.SnowFacetDisplace(x + cell, z, seed) - d;
                float dz = NextIslandPocGen.SnowFacetDisplace(x, z + cell, seed) - d;
                float addedSlopeDeg = Mathf.Atan2(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)), cell) * Mathf.Rad2Deg;
                if (addedSlopeDeg > maxAddedSlopeDeg) maxAddedSlopeDeg = addedSlopeDeg;
            }
            Assert.Greater(samples, 0, "the snow-cap footprint must be sampled (there must be a snow zone).");
            Assert.LessOrEqual(maxAbs, NextIslandPocGen.SnowFacetAmp + 1e-3f,
                $"the snow-facet displacement magnitude ({maxAbs:F2}u) must not exceed SnowFacetAmp ({NextIslandPocGen.SnowFacetAmp:F2}u).");
            // The near-flat summit + the smooth rock flank already sit well under 45°; the added displacement
            // slope must leave real headroom (a comfortable margin, not right at the limit).
            Assert.Less(maxAddedSlopeDeg, 30f,
                $"the snow-facet ADDED slope between adjacent grid verts ({maxAddedSlopeDeg:F1}°) must leave real " +
                "headroom under the 45° NavMesh agent max so the faceted cap stays CLIMBABLE (Sponsor APPROVED the climb).");
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
