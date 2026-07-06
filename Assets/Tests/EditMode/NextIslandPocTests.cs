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
            // Edge-to-edge crossing distance ~= the island diameter (~2×MeanShoreR). 86cahwx6w grew the island
            // to ~1200u: walk (5.5 u/s) ~3.6 min, run (9.5 u/s) ~2.1 min — a realistic mixed crossing still
            // lands in the 2-3+ min "feels big, not a slog" envelope. The bounds stay #226's: the WALK crossing
            // >= ~2 min (feels big) and the RUN crossing <= ~3.2 min (not a slog).
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
            // The hero must be a REAL tall mass: the peak height dwarfs the rolling hills, and the summit
            // sits well ABOVE the surrounding land (a giant hill, not a bump).
            // (86cahwx6w: #226's "ONE peak, not many" clause is SUPERSEDED by the multi-mountain destination —
            // the dominance contract is now: the hero DOMINATES the non-mountain land AND is the TALLEST peak
            // of the range. MultiPeak_SecondaryMassifs_AreShorterAndSteeperThanHero owns the range shape.)
            Off(out float ox, out float oz);
            Assert.Greater(NextIslandPocGen.MtnPeakHeight, NextIslandPocGen.HillAmp * 4f,
                $"the hero peak ({NextIslandPocGen.MtnPeakHeight:F0}u) must dwarf the rolling hills " +
                $"({NextIslandPocGen.HillAmp:F0}u) — a dominant giant hill (AC3).");
            float summitY = NextIslandPocGen.HeightAtRadial(NextIslandPocGen.MtnCenterX, NextIslandPocGen.MtnCenterZ, ox, oz);
            Assert.Greater(summitY, NextIslandPocGen.MtnPeakHeight * 0.7f,
                $"the summit terrain Y ({summitY:F0}u) must reach most of the peak height — a real tall summit.");
            // The hero summit dominates every NON-MOUNTAIN interior sample (outside EVERY peak's foot).
            float highestNonMtn = 0f;
            for (int a = 0; a < 24; a++)
            for (float rr = 40f; rr < NextIslandPocGen.CoreR; rr += 40f)
            {
                float ang = a / 24f * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                if (InsideAnyPeakFoot(x, z)) continue; // we want the NON-mountain land height
                float y = NextIslandPocGen.HeightAtRadial(x, z, ox, oz);
                if (y > highestNonMtn) highestNonMtn = y;
            }
            Assert.Greater(summitY, highestNonMtn * 3f,
                $"the hero summit ({summitY:F0}u) must DOMINATE the non-mountain land ({highestNonMtn:F0}u).");
            // And the hero must be the TALLEST peak of the range (the approved snow-cap peak stays dominant).
            for (int i = 1; i < NextIslandPocGen.Peaks.Length; i++)
                Assert.Greater(NextIslandPocGen.MtnPeakHeight, NextIslandPocGen.Peaks[i].height * 1.2f,
                    $"the hero ({NextIslandPocGen.MtnPeakHeight:F0}u) must stay clearly the TALLEST peak — " +
                    $"peak[{i}] is {NextIslandPocGen.Peaks[i].height:F0}u (silhouette steps DOWN from the hero).");
        }

        private static bool InsideAnyPeakFoot(float x, float z)
        {
            foreach (var p in NextIslandPocGen.Peaks)
                if (Vector2.Distance(new Vector2(x, z), new Vector2(p.cx, p.cz)) < p.footR) return true;
            return false;
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
            // Points far from EVERY peak but with a real hill height (interior). Colour must NOT be white.
            // (Sampled at a bigger ring than #226's 120 — on the grown island r=120 sits entirely inside the
            // hero foot and would skip every azimuth, making the loop vacuous.)
            int sampled = 0;
            for (int a = 0; a < 16; a++)
            {
                float ang = a / 16f * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * 400f, z = Mathf.Sin(ang) * 400f;
                if (InsideAnyPeakFoot(x, z)) continue; // skip every peak's footprint
                float y = NextIslandPocGen.HeightAtRadial(x, z, ox, oz);
                Color c = NextIslandPocGen.ColorAt(x, z, y, ox, oz, NextIslandPocScene.PocSeed);
                Assert.Less(Mathf.Min(c.r, c.g, c.b), 0.78f,
                    $"a non-mountain interior point (r=400, {x:F0},{z:F0}, y={y:F0}) must NOT read snow-white " +
                    "— snow is PEAK-relative (only snow-capped crowns whiten), not white-everywhere-high.");
                sampled++;
            }
            Assert.Greater(sampled, 4, "the non-mountain ring must actually sample points outside every foot " +
                "(a vacuous skip-everything loop guards nothing).");
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
                // 86cahwx6w: the island size + the secondary peaks are ALSO duplicated in the capture (the
                // coverage-trace extent, the overhead framing, and the whole-range side-profile framing key
                // off them) — pin them so a gen re-tune cannot silently desync the shipped-build evidence.
                Assert.AreEqual(NextIslandPocGen.MeanShoreR, cap.meanShoreRadius, 0.001f,
                    "capture meanShoreRadius must match the gen (#226 buried this as a local const — pinned now).");
                var p1 = NextIslandPocGen.Peaks[1];
                var p2 = NextIslandPocGen.Peaks[2];
                Assert.AreEqual(p1.cx, cap.secondaryPeak1.x, 0.001f, "capture secondaryPeak1 centre X must match Peaks[1].");
                Assert.AreEqual(p1.cz, cap.secondaryPeak1.z, 0.001f, "capture secondaryPeak1 centre Z must match Peaks[1].");
                Assert.AreEqual(p1.height, cap.secondaryPeak1.y, 0.001f, "capture secondaryPeak1 height (y) must match Peaks[1].");
                Assert.AreEqual(p2.cx, cap.secondaryPeak2.x, 0.001f, "capture secondaryPeak2 centre X must match Peaks[2].");
                Assert.AreEqual(p2.cz, cap.secondaryPeak2.z, 0.001f, "capture secondaryPeak2 centre Z must match Peaks[2].");
                Assert.AreEqual(p2.height, cap.secondaryPeak2.y, 0.001f, "capture secondaryPeak2 height (y) must match Peaks[2].");
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

        // ============================================================================================
        // ISLAND 2.0-A (ticket 86cahwx6w) — size growth + the multi-mountain range.
        // REGRESSION GUARDS for the new contract: the island grew markedly; the range is 2-3 peaks with
        // the HERO byte-kept (climb tuning + snow-cap faceting approved on #226/#230); secondaries are
        // shorter + steeper + cannot orphan walkable ground; the range connects as ridges from one landmass.
        // ============================================================================================

        // The #226-approved hero dome, pinned INLINE (deliberately not via the gen's constants): if a future
        // edit re-tunes the hero's shape — height, foot, power, centre, or the combining function — the
        // byte-kept guard below reds. This is the "KEEP the climb tuning" contract as a test.
        private static float HeroDomeReference226(float wx, float wz)
        {
            float dx = wx - 90f, dz = wz + 60f;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d >= 300f) return 0f;
            float t = d / 300f;
            float dome = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI);
            return 135f * Mathf.Pow(dome, 1.25f);
        }

        [Test]
        public void Island2_SizeGrew_MarkedlyBiggerThan226_CoastAmpScaledProportionally()
        {
            // The ticket's size AC: land diameter ~1100-1300u (~1.4-1.6× #226's 800u). Guard the FLOOR (the
            // "markedly bigger" claim) without pinning the exact default — the Sponsor dials inside the band.
            float diameter = NextIslandPocGen.MeanShoreR * 2f;
            Assert.GreaterOrEqual(diameter, 1080f,
                $"island 2.0 must be markedly bigger than #226 (~800u) — diameter {diameter:F0}u must be >=1080u (~1.35×).");
            // CoastIrregAmp scales PROPORTIONALLY with the size (the ticket constraint): the amp:radius ratio
            // must stay the #226-approved 70/400 = 0.175 so the bigger coast keeps the approved wander character.
            float ratio = NextIslandPocGen.CoastIrregAmp / NextIslandPocGen.MeanShoreR;
            Assert.AreEqual(0.175f, ratio, 0.02f,
                $"CoastIrregAmp/MeanShoreR ({ratio:F3}) must stay ~the #226-approved 0.175 — scale the amp with the size.");
            // The terrain grid must keep ~the #226 cell density (~3.9u) so fidelity + NavMesh resolution carry.
            float cell = NextIslandPocGen.GridHalf * 2f / NextIslandPocGen.Seg;
            Assert.AreEqual(3.9f, cell, 0.25f,
                $"terrain cell size ({cell:F2}u) must stay ~#226's 3.9u — grow Seg with GridHalf.");
        }

        [Test]
        public void MultiPeak_RangeIsTwoToThreePeaks_HeroIsIndexZeroBuiltFromTheConstants()
        {
            // The ticket's default band: 2-3 peaks. Index 0 is ALWAYS the hero, built from the Mtn* constants
            // (the capture calibration + the climb tests key off those — they must not drift apart).
            int n = NextIslandPocGen.Peaks.Length;
            Assert.That(n, Is.InRange(2, 3), $"the range must be 2-3 peaks (ticket default band) — got {n}.");
            var hero = NextIslandPocGen.Peaks[0];
            Assert.AreEqual(NextIslandPocGen.MtnCenterX, hero.cx, 0.001f, "Peaks[0] must BE the hero (cx = MtnCenterX).");
            Assert.AreEqual(NextIslandPocGen.MtnCenterZ, hero.cz, 0.001f, "Peaks[0] must BE the hero (cz = MtnCenterZ).");
            Assert.AreEqual(NextIslandPocGen.MtnPeakHeight, hero.height, 0.001f, "Peaks[0] must BE the hero (height).");
            Assert.AreEqual(NextIslandPocGen.MtnFootRadius, hero.footR, 0.001f, "Peaks[0] must BE the hero (footR).");
            Assert.IsTrue(hero.climbableDome, "the hero must stay the CLIMBABLE raised-cosine dome (Sponsor-approved).");
            Assert.IsTrue(hero.snowCap, "the hero must keep its snow cap (Sponsor-approved).");
        }

        [Test]
        public void MultiPeak_SecondaryMassifs_AreShorterAndSteeperThanHero()
        {
            // The ticket contract: secondaries are SHORTER (silhouette steps down) and STEEPER (non-climbable
            // rock massifs) than the hero. Steepness is measured on the actual profile: the TIP slope must
            // exceed the 45° NavMesh agent max (an un-climbable crown), where the hero's whole flank stays
            // under it (AC3_HeroMountain_IsClimbable guards that side).
            Assert.Greater(NextIslandPocGen.Peaks.Length, 1,
                "the range must have >=1 secondary peak or this per-peak loop is vacuous (guards a Length==1 regression).");
            for (int i = 1; i < NextIslandPocGen.Peaks.Length; i++)
            {
                var p = NextIslandPocGen.Peaks[i];
                Assert.Less(p.height, NextIslandPocGen.MtnPeakHeight,
                    $"peak[{i}] ({p.height:F0}u) must be SHORTER than the hero ({NextIslandPocGen.MtnPeakHeight:F0}u).");
                Assert.IsFalse(p.climbableDome, $"peak[{i}] must use the steep PEAKED profile (a rock massif, not a second hero dome).");
                // Numeric tip slope over the first metres of the radial profile.
                float h0 = NextIslandPocGen.PeakHeightAt(i, p.cx, p.cz);
                float h3 = NextIslandPocGen.PeakHeightAt(i, p.cx + 3f, p.cz);
                float tipSlopeDeg = Mathf.Atan2(h0 - h3, 3f) * Mathf.Rad2Deg;
                Assert.Greater(tipSlopeDeg, 45f,
                    $"peak[{i}] tip slope ({tipSlopeDeg:F1}°) must exceed the 45° agent max — a NON-climbable steep crown " +
                    "(the ticket's 'shorter/steeper rock massifs'; the hero stays the one climbable peak).");
            }
        }

        [Test]
        public void MultiPeak_SteepCrowns_CannotOrphanWalkableGround_SlopeMonotoneOutward()
        {
            // THE NO-ORPHAN GUARD (ticket constraint: steep features must not orphan walkable patches). The
            // peaked profile's slope must DECREASE MONOTONICALLY outward: then the >45° zone is one contiguous
            // cap containing the summit, with NOTHING walkable above it — no stranded walkable NavMesh island
            // (a raised-cosine secondary would flunk this: its flat summit disc is walkable but unreachable
            // across the steep mid-flank ring). Guards the bug CLASS: any future secondary-profile change that
            // re-introduces a walkable-above-steep band reds here before it ships an orphaned NavMesh patch.
            Assert.Greater(NextIslandPocGen.Peaks.Length, 1,
                "the range must have >=1 secondary peak or this per-peak loop is vacuous (guards a Length==1 regression).");
            for (int i = 1; i < NextIslandPocGen.Peaks.Length; i++)
            {
                var p = NextIslandPocGen.Peaks[i];
                const float dr = 1f;
                bool wentBelow = false; float crossR = 0f;
                for (float rr = 0f; rr < p.footR - dr; rr += dr)
                {
                    float h0 = NextIslandPocGen.PeakHeightAt(i, p.cx + rr, p.cz);
                    float h1 = NextIslandPocGen.PeakHeightAt(i, p.cx + rr + dr, p.cz);
                    float slopeDeg = Mathf.Atan2(Mathf.Abs(h0 - h1), dr) * Mathf.Rad2Deg;
                    if (!wentBelow && slopeDeg < 45f) { wentBelow = true; crossR = rr; }
                    if (wentBelow)
                        Assert.Less(slopeDeg, 45f,
                            $"peak[{i}]: profile slope re-exceeded 45° at r={rr:F0} after dropping below it at " +
                            $"r={crossR:F0} — a walkable band above a steep band ORPHANS a NavMesh patch (ticket constraint).");
                }
                Assert.IsTrue(wentBelow, $"peak[{i}]: the profile must ease under 45° before its foot (the walkable skirt " +
                    "that lets the massif merge into the landmass).");
                // And the crown-facet displacement zone must sit INSIDE the un-walkable cap, so the ±SnowFacetAmp
                // jag never perturbs ground an agent can stand on (the hero's climb-bounded displacement is
                // guarded separately by SnowFacetDisplacement_IsBounded_AndClimbable).
                float facetR = 0f;
                for (float rr = 0f; rr < p.footR; rr += 0.5f)
                    if (NextIslandPocGen.MountainHeightFracAt(p.cx + rr, p.cz) >= NextIslandPocGen.SnowFacetFrac) facetR = rr;
                    else break;
                Assert.Less(facetR, crossR,
                    $"peak[{i}]: the facet-displacement zone (r<={facetR:F0}) must sit inside the un-walkable crown " +
                    $"(r<={crossR:F0}) so the angular jag never touches agent-walkable ground.");
            }
        }

        [Test]
        public void MultiPeak_HeroHeightField_ByteKept_ClimbTuningUnregressed()
        {
            // THE PRESERVATION GUARD (ticket: KEEP the hero's climb tuning + snow-cap faceting). The combined
            // multi-peak field must equal the #226 hero dome EXACTLY (a) across the hero's core — summit, snow
            // cap, facet zone (r<=150 covers the snow zone's ~107u) — and (b) along the whole spawn-facing
            // approach sector out to the foot (the flank the Sponsor actually climbs). If a secondary's foot
            // creeps into either region, or the combining function stops preserving the max, this reds.
            for (int a = 0; a < 16; a++)
            for (float rr = 0f; rr <= 150f; rr += 10f)
            {
                float ang = a / 16f * Mathf.PI * 2f;
                float x = NextIslandPocGen.MtnCenterX + Mathf.Cos(ang) * rr;
                float z = NextIslandPocGen.MtnCenterZ + Mathf.Sin(ang) * rr;
                Assert.AreEqual(HeroDomeReference226(x, z), NextIslandPocGen.MountainHeightAt(x, z), 1e-4f,
                    $"hero core must be BYTE-KEPT vs #226 at ({x:F0},{z:F0}) — the approved summit/snow/facet region.");
                Assert.AreEqual(HeroDomeReference226(x, z) / 135f, NextIslandPocGen.MountainHeightFracAt(x, z), 1e-5f,
                    $"hero core height-FRACTION must be byte-kept at ({x:F0},{z:F0}) — the snow band + facet zone key off it.");
            }
            // (b) the spawn-facing approach sector (the climbed flank): hero→spawn azimuth ±60°, foot-to-summit.
            float spawnAng = Mathf.Atan2(NextIslandPocGen.SpawnZ - NextIslandPocGen.MtnCenterZ,
                                         NextIslandPocGen.SpawnX - NextIslandPocGen.MtnCenterX);
            for (int a = -6; a <= 6; a++)
            for (float rr = 0f; rr < NextIslandPocGen.MtnFootRadius; rr += 12f)
            {
                float ang = spawnAng + a * (10f * Mathf.Deg2Rad);
                float x = NextIslandPocGen.MtnCenterX + Mathf.Cos(ang) * rr;
                float z = NextIslandPocGen.MtnCenterZ + Mathf.Sin(ang) * rr;
                Assert.AreEqual(HeroDomeReference226(x, z), NextIslandPocGen.MountainHeightAt(x, z), 1e-4f,
                    $"the spawn-side climb flank must be BYTE-KEPT vs #226 at ({x:F0},{z:F0}) (azimuth offset {a * 10}°).");
            }
        }

        [Test]
        public void MultiPeak_RangeConnects_AsRidgesFromOneLandmass_NotStampedCones()
        {
            // THE ANCHOR SENTENCE AS AN ASSERT (Bar 4): "several mountains read as ridges rising from one
            // landmass, not cones stamped on a pancake." Mechanically: along the line between the hero and
            // each secondary, the combined mountain field must NEVER drop to the flat plateau — a real
            // connecting shoulder/col stays elevated between peaks. (A stamped-cone layout — feet not
            // overlapping — would dip to ~0 between them and red here.)
            var hero = NextIslandPocGen.Peaks[0];
            Assert.Greater(NextIslandPocGen.Peaks.Length, 1,
                "the range must have >=1 secondary peak or this per-peak loop is vacuous (guards a Length==1 regression).");
            for (int i = 1; i < NextIslandPocGen.Peaks.Length; i++)
            {
                var p = NextIslandPocGen.Peaks[i];
                float minH = float.MaxValue;
                for (float s = 0.1f; s <= 0.9f; s += 0.02f)
                {
                    float x = Mathf.Lerp(hero.cx, p.cx, s);
                    float z = Mathf.Lerp(hero.cz, p.cz, s);
                    float h = NextIslandPocGen.MountainHeightAt(x, z);
                    if (h < minH) minH = h;
                }
                Assert.Greater(minH, 8f,
                    $"the col between the hero and peak[{i}] must stay a REAL connecting shoulder (min {minH:F1}u " +
                    "along the ridge line must exceed 8u above the plateau) — ridges from one landmass, not stamped cones.");
            }
        }

        [Test]
        public void MultiPeak_CrownColours_SnowCapVsBareRock_PerPeakBanding()
        {
            // Peak-relative banding (the multi-peak sibling of AC3_HeroMountain_HasHeightThresholdSnowCap):
            // a snowCap secondary's summit reads SNOW-white (its own small crown); a snowCap=false massif's
            // summit reads bare grey ROCK (Bar 3 material-honest — stone reads as stone, never arbitrary),
            // even though BOTH crowns facet. Also guards the SnowFracAt/heightFrac split in ColorAt.
            Off(out float ox, out float oz);
            Assert.Greater(NextIslandPocGen.Peaks.Length, 1,
                "the range must have >=1 secondary peak or this per-peak loop is vacuous (guards a Length==1 regression).");
            for (int i = 1; i < NextIslandPocGen.Peaks.Length; i++)
            {
                var p = NextIslandPocGen.Peaks[i];
                float y = NextIslandPocGen.HeightAtRadial(p.cx, p.cz, ox, oz);
                Color c = NextIslandPocGen.ColorAt(p.cx, p.cz, y, ox, oz, NextIslandPocScene.PocSeed);
                if (p.snowCap)
                {
                    Assert.Greater(Mathf.Min(c.r, c.g, c.b), 0.78f,
                        $"peak[{i}] is snowCap=true — its summit must read SNOW (near-white); got {c}.");
                }
                else
                {
                    Assert.Less(Mathf.Min(c.r, c.g, c.b), 0.78f,
                        $"peak[{i}] is snowCap=false — its summit must read bare ROCK, never snow-white; got {c}.");
                    Assert.Less(Mathf.Abs(c.g - c.r), 0.12f,
                        $"peak[{i}] bare-rock summit must read GREY (r≈g, not green grass); got {c}.");
                }
                // Every crown facets (the chunky read) regardless of snow — the #230 idiom extended per-peak.
                Assert.IsTrue(NextIslandPocGen.IsSnowFacetZone(p.cx, p.cz),
                    $"peak[{i}]'s crown must be in the facet zone (chunky angular crown, snow or rock).");
            }
        }

        [Test]
        public void MultiPeak_RockBand_IsPerPeakProfile_CragFlankReadsStoneNotGreenHill()
        {
            // REGRESSION GUARD for the per-peak-BAND class (86cahwx6w capture-pass-2 finding): with the old
            // GLOBAL 0.40..0.62 height-frac band, a peaked m=1.8 massif rocked only its top ~quarter of foot
            // radius → the whole crag read "a green hill with a rock tip" (material-dishonest: steep bare
            // crags hold no soil, Bar 3). Per-peak banding starts rock LOW on the crags. Mechanically: at 30%
            // of a secondary's foot radius (height frac ≈ 0.34 on m=1.8 — under the old 0.40 start, so the
            // OLD code reads GRASS there and this test reds on it) the flank must read fully GREY ROCK.
            Off(out float ox, out float oz);
            Assert.Greater(NextIslandPocGen.Peaks.Length, 1,
                "the range must have >=1 secondary peak or this per-peak loop is vacuous (guards a Length==1 regression).");
            for (int i = 1; i < NextIslandPocGen.Peaks.Length; i++)
            {
                var p = NextIslandPocGen.Peaks[i];
                // Sample toward -X (both secondaries' -X flank is hero-free: the hero foot does not reach).
                float x = p.cx - p.footR * 0.30f, z = p.cz;
                Assert.Greater(NextIslandPocGen.RockBandT(x, z), 0.9f,
                    $"peak[{i}] at 30% of its foot radius must be fully in the ROCK band (per-peak banding) " +
                    "— a near-zero band here is the old global 0.40-start band = the green-hill-with-rock-tip defect.");
                float y = NextIslandPocGen.HeightAtRadial(x, z, ox, oz);
                Color c = NextIslandPocGen.ColorAt(x, z, y, ox, oz, NextIslandPocScene.PocSeed);
                Assert.Less(Mathf.Abs(c.g - c.r), 0.12f,
                    $"peak[{i}] mid-flank (30% foot radius) must read GREY stone (r≈g), not green grass; got {c}.");
                Assert.Less(Mathf.Min(c.r, c.g, c.b), 0.78f,
                    $"peak[{i}] mid-flank must read ROCK, not snow-white; got {c}.");
            }
            // Hero byte-keep drift guard: the hero's per-peak band must stay the #226/#230-approved LITERAL
            // values. Pinned to the literals (NOT to the Hero*Frac consts the field is INITIALISED from — that
            // was tautological: Peaks[0].rockStartFrac == HeroRockStartFrac by construction, so it passed for
            // ANY value). A retune of the const now flows into Peaks[0] and REDS this — the drift guard bites.
            Assert.AreEqual(0.40f, NextIslandPocGen.Peaks[0].rockStartFrac, 1e-6f,
                "the hero's rockStartFrac must stay the approved LITERAL 0.40 (byte-kept look; a retune reds this).");
            Assert.AreEqual(0.62f, NextIslandPocGen.Peaks[0].rockFullFrac, 1e-6f,
                "the hero's rockFullFrac must stay the approved LITERAL 0.62 (byte-kept look; a retune reds this).");
        }

        [Test]
        public void MultiPeak_TreeLine_IsPerPeak_NoForestOnACragRockBand()
        {
            // The scatter sibling of the per-peak rock band: the tree line must be PER-PEAK too, or trees
            // stand on the crag's low-starting stone (a forest on bare rock). Crag tree line (0.10 frac ≈
            // 51% of foot radius) sits just OUTSIDE the rock-start ring; the hero keeps the approved 0.45.
            Assert.Greater(NextIslandPocGen.Peaks.Length, 1,
                "the range must have >=1 secondary peak or this per-peak loop is vacuous (guards a Length==1 regression).");
            for (int i = 1; i < NextIslandPocGen.Peaks.Length; i++)
            {
                var p = NextIslandPocGen.Peaks[i];
                // 40% of foot radius → height frac ≈ 0.20 on m=1.8: above the crag tree line (no trees)…
                Assert.IsTrue(NextIslandPocGen.AboveTreeLine(p.cx - p.footR * 0.40f, p.cz),
                    $"peak[{i}] at 40% of its foot radius must be ABOVE the crag tree line (frac ≈ 0.20 > 0.10) " +
                    "— under the old global 0.45 line trees grew here, standing on the rock band.");
                // …while near the foot the gentle grassy skirt still takes forest.
                Assert.IsFalse(NextIslandPocGen.AboveTreeLine(p.cx - p.footR * 0.90f, p.cz),
                    $"peak[{i}]'s grassy foot skirt (90% of foot radius) must stay BELOW the tree line (forest ok).");
            }
            // Hero line unchanged (approved 0.45): 40% up the dome is treed, 75% out is not. Sampled toward
            // -X, where no secondary foot reaches the hero flank.
            Assert.AreEqual(0.45f, NextIslandPocGen.Peaks[0].treeLineFrac, 1e-6f,
                "the hero's treeLineFrac must stay the approved LITERAL 0.45 (byte-kept scatter line; NOT the " +
                "HeroTreeLineFrac const it is initialised from — that was tautological; a retune reds this).");
            float hx = NextIslandPocGen.MtnCenterX, hz = NextIslandPocGen.MtnCenterZ, hf = NextIslandPocGen.MtnFootRadius;
            Assert.IsTrue(NextIslandPocGen.AboveTreeLine(hx - hf * 0.40f, hz),
                "40% out on the hero dome (height frac ≈ 0.59 > 0.45) must be above the hero tree line.");
            Assert.IsFalse(NextIslandPocGen.AboveTreeLine(hx - hf * 0.75f, hz),
                "75% out on the hero dome (height frac ≈ 0.09 < 0.45) must be below the hero tree line (forest ok).");
        }

        [Test]
        public void MultiPeak_SecondariesSitOnLand_ClearOfTheSpawn()
        {
            // Placement invariants: every secondary summit sits INSIDE the full-strength land core (its peak
            // is never coast-damped into a mound), and every foot stays clear of the spawn clearing (the
            // spawn must stay flat sea-level ground — the run-2 regression, multi-peak edition).
            Assert.Greater(NextIslandPocGen.Peaks.Length, 1,
                "the range must have >=1 secondary peak or this per-peak loop is vacuous (guards a Length==1 regression).");
            for (int i = 1; i < NextIslandPocGen.Peaks.Length; i++)
            {
                var p = NextIslandPocGen.Peaks[i];
                float centerR = Mathf.Sqrt(p.cx * p.cx + p.cz * p.cz);
                Assert.Less(centerR, NextIslandPocGen.CoreR,
                    $"peak[{i}] centre (r={centerR:F0}) must sit inside the land core ({NextIslandPocGen.CoreR:F0}u) " +
                    "so its summit gets full landMask strength (not a coast-damped mound).");
                float dSpawn = Mathf.Sqrt((p.cx - NextIslandPocGen.SpawnX) * (p.cx - NextIslandPocGen.SpawnX) +
                                          (p.cz - NextIslandPocGen.SpawnZ) * (p.cz - NextIslandPocGen.SpawnZ));
                Assert.Greater(dSpawn, p.footR + NextIslandPocGen.SpawnFlattenFullR,
                    $"peak[{i}] foot (r={p.footR:F0} @ {dSpawn:F0}u from spawn) must clear the spawn clearing.");
            }
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

        [Test]
        public void Scatter_RejectsTreesAndGrass_AboveThePerPeakTreeLine()
        {
            // INTEGRATION guard (independent-review NIT): the pure AboveTreeLine PREDICATE is unit-tested, but
            // nothing exercised NextIslandPocScatter.Scatter() itself — so a future edit that drops the
            // OnBareMountain rejection (or mis-wires it) would ship a forest growing up a snow cap / standing on
            // a crag's bare rock band with every existing test still green. This runs the REAL scatter over the
            // REAL built terrain collider and asserts no placed tree/grass sits above the per-peak tree line.
            var parent = new GameObject("PocScatterTestParent");
            try
            {
                var vcMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                GameObject ground = NextIslandPocGen.BuildPocIsland(parent, NextIslandPocScene.PocSeed, vcMat, waterMat);
                var col = ground.GetComponent<MeshCollider>();
                Assert.IsNotNull(col, "the POC terrain must carry a MeshCollider for the scatter to ground onto.");

                // Non-vacuity: the tree-line predicate must actually reject SOMEWHERE (the mountains exist),
                // else "no tree above the line" would pass trivially on a mountain-free island.
                Assert.IsTrue(NextIslandPocGen.AboveTreeLine(NextIslandPocGen.MtnCenterX, NextIslandPocGen.MtnCenterZ),
                    "the hero summit must be ABOVE the tree line — the predicate must be non-trivially true (else vacuous).");

                var scatterRoot = new GameObject("PocScatterRoot");
                scatterRoot.transform.SetParent(parent.transform, false);
                // A modest target keeps the EditMode run fast; the rejection logic is count-independent. The
                // scatter grounds each prop with a straight-down raycast, so child.position.x/z == the scatter
                // x/z it tested against OnBareMountain -> AboveTreeLine (a vertical ray preserves x/z exactly).
                NextIslandPocScatter.Scatter(scatterRoot, NextIslandPocScene.PocSeed, col, 250);

                int trees = 0, grass = 0, treesAbove = 0, grassAbove = 0;
                foreach (Transform child in scatterRoot.transform)
                {
                    Vector3 p = child.position;
                    bool above = NextIslandPocGen.AboveTreeLine(p.x, p.z);
                    if (child.name == "LP_Tree") { trees++; if (above) treesAbove++; }
                    else if (child.name == "LP_Grass") { grass++; if (above) grassAbove++; }
                }
                Assert.Greater(trees, 0, "the scatter must actually place trees (else the rejection guard is vacuous).");
                Assert.Greater(grass, 0, "the scatter must actually place grass (else the rejection guard is vacuous).");
                Assert.AreEqual(0, treesAbove,
                    $"{treesAbove}/{trees} trees landed ABOVE the per-peak tree line — Scatter must reject trees on the " +
                    "snow-cap flank / crag rock band (NextIslandPocScatter.OnBareMountain -> AboveTreeLine).");
                Assert.AreEqual(0, grassAbove,
                    $"{grassAbove}/{grass} grass clumps landed ABOVE the per-peak tree line — grass must be rejected on " +
                    "the bare/snow flank too (a forest/lawn on bare rock is material-dishonest).");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void PocTreeTarget_ScalesWithIslandArea_VsThe226Anchor_NotDegenerate()
        {
            // Proportionality guard (independent-review NIT): PocTreeTarget is a SOAK-TUNED default, so pin it to
            // the AREA-SCALING relationship — NOT a hard ==1260 (that would red on any legitimate soak retune).
            // The #226 anchor: 560 trees at MeanShoreR=400u; forest DENSITY is trees/area, so the grown island's
            // target scales with (MeanShoreR/400)^2 to hold the #226-approved density (see the PocTreeTarget
            // comment in NextIslandPocScene). A degenerate value (an un-scaled leftover 560, or 0) reds here.
            const float anchorTrees = 560f, anchorShoreR = 400f;
            float expected = anchorTrees * Mathf.Pow(NextIslandPocGen.MeanShoreR / anchorShoreR, 2f);
            // FLOOR: the bigger island must never carry FEWER trees than the smaller #226 island (a density collapse).
            Assert.GreaterOrEqual(NextIslandPocScene.PocTreeTarget, (int)anchorTrees,
                $"PocTreeTarget ({NextIslandPocScene.PocTreeTarget}) must be >= the #226 count ({(int)anchorTrees}) — " +
                "the grown island must not carry fewer trees than the smaller island it scaled from.");
            // PROPORTIONALITY: within ±30% of the area-scaled anchor (soak-tuning headroom, no degenerate value).
            Assert.That(NextIslandPocScene.PocTreeTarget, Is.EqualTo(expected).Within(expected * 0.30f),
                $"PocTreeTarget ({NextIslandPocScene.PocTreeTarget}) must scale ~with island AREA vs the #226 anchor " +
                $"(560 @ 400u -> ~{expected:F0} @ {NextIslandPocGen.MeanShoreR:F0}u, ±30%) — soak-tuned, not a hard 1260.");
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

        // ============================================================================================
        // ISLAND 2.0-B (ticket 86cakk4w8 — C2): rocky WALLS + huge stone SLABS. FREE-STANDING FacetedRock hero
        // PROPS scattered via NextIslandPocScatter (DISTINCT from C1's per-peak terrain banding). These run the
        // REAL scatter over the REAL built terrain collider — the bug CLASS each guards is named in-test.
        // ============================================================================================

        // Run the C2 scatter over the built POC terrain + collect the wall/slab feature roots (a shared helper so
        // each test asserts one property without re-building). Returns the scatter root; out-lists the features.
        private static GameObject ScatterC2(out System.Collections.Generic.List<Transform> walls,
                                            out System.Collections.Generic.List<Transform> slabs,
                                            GameObject parent, int seed)
        {
            var vcMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject ground = NextIslandPocGen.BuildPocIsland(parent, seed, vcMat, waterMat);
            var col = ground.GetComponent<MeshCollider>();
            Assert.IsNotNull(col, "the POC terrain must carry a MeshCollider for the scatter to ground onto.");
            var root = new GameObject("PocScatterRoot");
            root.transform.SetParent(parent.transform, false);
            NextIslandPocScatter.Scatter(root, seed, col, 120);
            walls = new System.Collections.Generic.List<Transform>();
            slabs = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in root.transform)
            {
                if (child.name == "LP_RockWall") walls.Add(child);
                else if (child.name == "LP_StoneSlab") slabs.Add(child);
            }
            return root;
        }

        [Test]
        public void C2_Scatter_PlacesRockyWalls_AndStoneSlabs_InTheDefaultBands()
        {
            // The Sponsor-soak default bands (ticket 86cakk4w8): 3-6 rocky walls + 8-15 stone slabs. Guards the
            // guard-loop STARVATION bug class — if the peak/landmass rejection were too aggressive the scatter
            // would place FEWER than target (silent under-population); asserting IN-band proves placement is not
            // starved on the real terrain.
            var parent = new GameObject("C2CountParent");
            try
            {
                ScatterC2(out var walls, out var slabs, parent, NextIslandPocScene.PocSeed);
                Assert.That(walls.Count, Is.InRange(3, 6),
                    $"the scatter must place 3-6 rocky WALLS (default band) — got {walls.Count} (guard-loop starved?).");
                Assert.That(slabs.Count, Is.InRange(8, 15),
                    $"the scatter must place 8-15 stone SLABS (default band) — got {slabs.Count} (guard-loop starved?).");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void C2_RockFeatures_RejectPeakFootprints_NeverClippedIntoAMountain()
        {
            // THE PEAK-REJECT GUARD (ticket constraint): a wall/slab footprint must never overlap any of C1's
            // three Peaks[] feet — clipping a hero prop into a mountain reads broken + risks orphaning the
            // walkable surface. SeatOnGround only moves Y (a straight-down ground raycast preserves x/z), so the
            // feature root x/z == the scatter point tested against the peak feet.
            var parent = new GameObject("C2PeakParent");
            try
            {
                ScatterC2(out var walls, out var slabs, parent, NextIslandPocScene.PocSeed);
                Assert.Greater(walls.Count + slabs.Count, 0, "the scatter must place features (else this guard is vacuous).");
                foreach (var f in walls.Concat(slabs))
                foreach (var p in NextIslandPocGen.Peaks)
                {
                    float d = Vector2.Distance(new Vector2(f.position.x, f.position.z), new Vector2(p.cx, p.cz));
                    Assert.Greater(d, p.footR,
                        $"'{f.name}' @ ({f.position.x:F0},{f.position.z:F0}) sits {d:F0}u from peak ({p.cx:F0},{p.cz:F0}) " +
                        $"— inside its foot ({p.footR:F0}u). Walls/slabs must go on flats/foreshore/cols, never clipped into a peak.");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void C2_RockFeatures_UseNewSeedStreams_Deterministic_C1ScatterUnperturbed()
        {
            // NEW seed streams (walls seed+1111 / slabs seed+1212) — never mutate C1's seed+555 stream. Two proofs:
            // (1) DETERMINISM: two scatters at the same seed place walls/slabs at identical positions (a reproducible
            //     baked scene). (2) C1-UNPERTURBED: the tree/rock/grass COUNTS are identical across the two runs
            //     (the C2 loops run on separate Random instances AFTER the C1 loops, so they cannot re-roll C1).
            var pa = new GameObject("C2DetA");
            var pb = new GameObject("C2DetB");
            try
            {
                var ra = ScatterC2(out var wallsA, out var slabsA, pa, NextIslandPocScene.PocSeed);
                var rb = ScatterC2(out var wallsB, out var slabsB, pb, NextIslandPocScene.PocSeed);
                Assert.AreEqual(wallsA.Count, wallsB.Count, "wall count must be deterministic across identical seeds.");
                Assert.AreEqual(slabsA.Count, slabsB.Count, "slab count must be deterministic across identical seeds.");
                for (int i = 0; i < wallsA.Count; i++)
                    Assert.Less(Vector3.Distance(wallsA[i].position, wallsB[i].position), 1e-3f,
                        $"wall {i} must land at the SAME position across identical seeds (reproducible baked scene).");
                int treesA = CountNamed(ra, "LP_Tree"), treesB = CountNamed(rb, "LP_Tree");
                int rocksA = CountNamed(ra, "LP_Rock"), rocksB = CountNamed(rb, "LP_Rock");
                int grassA = CountNamed(ra, "LP_Grass"), grassB = CountNamed(rb, "LP_Grass");
                Assert.Greater(treesA, 0, "the C1 tree loop must still run (non-vacuous).");
                Assert.AreEqual(treesA, treesB, "C1 tree count must be deterministic (the C2 streams must not perturb it).");
                Assert.AreEqual(rocksA, rocksB, "C1 rock count must be deterministic (C2 streams disjoint).");
                Assert.AreEqual(grassA, grassB, "C1 grass count must be deterministic (C2 streams disjoint).");
            }
            finally { Object.DestroyImmediate(pa); Object.DestroyImmediate(pb); }
        }

        [Test]
        public void C2_RockFeatures_HeroCasterPolicy_ShadowsOn_AndStaticBatched()
        {
            // Caster policy (ticket constraint): large hero silhouette features (walls, big slabs) KEEP shadows +
            // static-batch (the shadow pass is the dominant GPU cost — silhouette features need shadows). Guards a
            // regression that ships them castShadows:false (a silent silhouette-flattening).
            var parent = new GameObject("C2CasterParent");
            try
            {
                ScatterC2(out var walls, out var slabs, parent, NextIslandPocScene.PocSeed);
                foreach (var f in walls.Concat(slabs))
                {
                    var mr = f.GetComponentInChildren<MeshRenderer>();
                    Assert.IsNotNull(mr, $"'{f.name}' must carry a MeshRenderer.");
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, mr.shadowCastingMode,
                        $"'{f.name}' is a hero feature — it must CAST shadows (silhouette read).");
                    var flags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(f.gameObject);
                    Assert.IsTrue(flags.HasFlag(UnityEditor.StaticEditorFlags.BatchingStatic),
                        $"'{f.name}' must be BatchingStatic (URP static-batches the hero rock — the perf lever).");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void C2_RockFeatures_CarveNavMesh_NoOrphan_SolidToTheAgent()
        {
            // THE NO-ORPHAN + SOLID GUARD (ticket constraint: "walls off-NavMesh or carved so they don't orphan
            // the walkable surface"). Each wall/slab carries a CARVING Box NavMeshObstacle — the tree idiom. Carving
            // only SUBTRACTS the footprint (never creates a walkable island), so a free-standing feature can NEVER
            // orphan a walkable patch; and the NavMeshAgent player cannot enter the carved footprint => can't walk
            // up a vertical wall / through a boulder. (It also leaves the BAKED asset unchanged — runtime carve.)
            var parent = new GameObject("C2NavParent");
            try
            {
                ScatterC2(out var walls, out var slabs, parent, NextIslandPocScene.PocSeed);
                Assert.Greater(walls.Count + slabs.Count, 0, "the scatter must place features (else this guard is vacuous).");
                foreach (var f in walls.Concat(slabs))
                {
                    var obs = f.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                    Assert.IsNotNull(obs, $"'{f.name}' must carry a NavMeshObstacle (solid to the agent + off-NavMesh).");
                    Assert.IsTrue(obs.carving, $"'{f.name}' obstacle must CARVE (subtract-only => no orphaned walkable patch).");
                    Assert.AreEqual(UnityEngine.AI.NavMeshObstacleShape.Box, obs.shape,
                        $"'{f.name}' obstacle must be a Box (a wall/slab footprint, not a capsule).");
                    Assert.Greater(obs.size.sqrMagnitude, 0f, $"'{f.name}' obstacle must have a real footprint size.");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void C2_RockFeatures_ShareOneMaterialPerClass_TonalVariationIsVertexColour()
        {
            // T-A (ticket constraint): tonal variation is VERTEX-COLOUR only, NEVER per-material — so all walls
            // share ONE material + all slabs share ONE material (per-material tint would break the SRP-Batcher
            // ~1-draw read). And the FacetedRock mesh must carry per-vertex COLOURS (that IS the tonal variation).
            var parent = new GameObject("C2MatParent");
            try
            {
                ScatterC2(out var walls, out var slabs, parent, NextIslandPocScene.PocSeed);
                AssertOneSharedMaterialAndVertexColours(walls, "rocky wall");
                AssertOneSharedMaterialAndVertexColours(slabs, "stone slab");
                // The two classes use DISTINCT materials (walls a touch cooler/darker than slabs) — but still just 2.
                var wm = walls[0].GetComponentInChildren<MeshRenderer>().sharedMaterial;
                var sm = slabs[0].GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Assert.AreNotSame(wm, sm, "walls and slabs use their own shared material each (2 hero-rock mats total).");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        private static void AssertOneSharedMaterialAndVertexColours(
            System.Collections.Generic.List<Transform> features, string label)
        {
            Assert.Greater(features.Count, 0, $"the scatter must place {label}s (else this guard is vacuous).");
            Material shared = features[0].GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.IsNotNull(shared, $"a {label} must carry a material.");
            foreach (var f in features)
            {
                var mr = f.GetComponentInChildren<MeshRenderer>();
                Assert.AreSame(shared, mr.sharedMaterial,
                    $"every {label} must share ONE material (tonal variation is vertex-colour, never per-material — T-A).");
                var mesh = f.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.Greater(mesh.colors.Length, 0,
                    $"the {label} FacetedRock mesh must carry per-vertex COLOURS (the per-facet tonal variation).");
            }
        }

        [Test]
        public void C2_Walls_ReadWideRockFace_Slabs_ReadFlatTopped_AndBothSitOnTheGround()
        {
            // THE PHYSICAL-ANCHOR GUARD (lowpoly-quality §0). A rocky WALL is a WIDE near-vertical rock FACE you
            // can't walk up => its silhouette must be WIDER than it is tall (the real-world anchor: a wall is wide
            // relative to its height), and it must RISE above its thin depth (the un-walkable face). The pre-fix
            // 5×24×7 needle read as a shard/monolith — this reds on that class. A stone SLAB is a flat-topped
            // boulder sitting ON the ground => WIDER than it is tall. Both bases must MEET the terrain across their
            // whole FOOTPRINT (not float on the downhill edge / not fully buried) — the grounding-fix guard.
            var parent = new GameObject("C2ShapeParent");
            try
            {
                var root = ScatterC2(out var walls, out var slabs, parent, NextIslandPocScene.PocSeed);
                var ground = FindChild(parent.transform, "Ground_Poc").GetComponent<MeshCollider>();
                foreach (var w in walls)
                {
                    // Measure the wall's TRUE oriented dimensions (mesh-local bounds × lossyScale), NOT the world
                    // AABB: a thin-wide box rotated to a diagonal yaw balloons its AABB depth toward its width, so
                    // an AABB read would false-judge the proportions. Local size × the WallMesh lossyScale (the
                    // (wide,hgt,thk) parent scale) gives the honest face proportions independent of yaw.
                    var mf = w.GetComponentInChildren<MeshFilter>();
                    Vector3 ls = mf.transform.lossyScale;
                    Vector3 ms = mf.sharedMesh.bounds.size;
                    float dx = ms.x * Mathf.Abs(ls.x), dy = ms.y * Mathf.Abs(ls.y), dz = ms.z * Mathf.Abs(ls.z);
                    float width = Mathf.Max(dx, dz), depth = Mathf.Min(dx, dz), height = dy;
                    // WIDE (not a shard): a wall reads as a broad rock run, wider than tall.
                    Assert.Greater(width, height,
                        $"rocky WALL '{w.name}' must read WIDE — width {width:F1}u must exceed height {height:F1}u " +
                        "(a broad rock face, NOT a needle/shard — the pre-fix 5×24×7 monolith).");
                    // A real face, not a small rock: the wide run is a landmark scale.
                    Assert.Greater(width, 14f,
                        $"rocky WALL '{w.name}' width {width:F1}u must be a landmark-scale rock face (>14u), not a small rock.");
                    // NEAR-VERTICAL FACE: rises clearly above its thin depth (you can't walk up it).
                    Assert.Greater(height, depth * 1.1f,
                        $"rocky WALL '{w.name}' must rise NEAR-VERTICAL — height {height:F1}u must exceed its thin " +
                        $"depth {depth:F1}u (a face you can't walk up).");
                    AssertSeatedOnGround(w, ground, "wall");
                }
                foreach (var s in slabs)
                {
                    Bounds b = s.GetComponentInChildren<MeshRenderer>().bounds;
                    float footprint = Mathf.Max(b.size.x, b.size.z);
                    Assert.Less(b.size.y, footprint,
                        $"stone SLAB '{s.name}' must read FLAT-TOPPED — height {b.size.y:F1}u must be under its footprint " +
                        $"{footprint:F1}u (a wide boulder sitting ON the ground, not a tall spike).");
                    AssertSeatedOnGround(s, ground, "slab");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        // A feature "sits ON the ground" when its mesh base meets the terrain across its WHOLE FOOTPRINT — not
        // floating above ANYWHERE (the pre-fix centre-only check was blind to the seaward-half float on a slope)
        // and not fully buried. Samples the terrain at an 8-point footprint ring + the centre and asserts NO
        // sample leaves an air gap under the base. This is the regression guard for the FLOAT bug CLASS.
        private static void AssertSeatedOnGround(Transform feature, MeshCollider ground, string label)
        {
            Bounds b = feature.GetComponentInChildren<MeshRenderer>().bounds;
            float underside = b.min.y;
            float rx = b.size.x * 0.5f, rz = b.size.z * 0.5f;
            float maxAirGap = float.MinValue; float minGround = float.MaxValue; int samples = 0;
            void Sample(float gx, float gz)
            {
                var ray = new Ray(new Vector3(gx, 300f, gz), Vector3.down);
                if (!ground.Raycast(ray, out RaycastHit hit, 600f)) return;
                float airGap = underside - hit.point.y;   // >0 base above ground here (floating)
                if (airGap > maxAirGap) maxAirGap = airGap;
                if (hit.point.y < minGround) minGround = hit.point.y;
                samples++;
            }
            Sample(b.center.x, b.center.z);
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                Sample(b.center.x + Mathf.Cos(a) * rx, b.center.z + Mathf.Sin(a) * rz);
            }
            Assert.Greater(samples, 4,
                $"the {label} at ({feature.position.x:F0},{feature.position.z:F0}) must stand over terrain at its footprint.");
            // NO FLOAT anywhere on the footprint: the max air gap across all samples must be at/under the surface
            // (a small +tol allows facet micro-relief; the pre-fix seaward-half float was multiple metres).
            Assert.Less(maxAirGap, 0.6f,
                $"the {label} base must not FLOAT above the terrain at ANY footprint point — max air gap {maxAirGap:F1}u " +
                "(this is the seaward-half saucer defect; SeatConform seats to the lowest footprint ground).");
            // Not fully buried: the top must sit above the lowest footprint ground by a real fraction of its height.
            Assert.Greater(b.max.y, minGround + b.size.y * 0.25f,
                $"the {label} must not be FULLY buried — top {b.max.y:F1}u vs lowest footprint ground {minGround:F1}u " +
                $"(feature height {b.size.y:F1}u).");
        }

        private static int CountNamed(GameObject root, string name)
        {
            int n = 0;
            foreach (Transform child in root.transform) if (child.name == name) n++;
            return n;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
