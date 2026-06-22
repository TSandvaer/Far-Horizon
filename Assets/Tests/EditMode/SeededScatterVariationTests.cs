using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the seeded scatter rotation/lean/height variation (ticket 86caamnra — Erik R&D
    /// §G / Rank 7). The seed-per-instance pattern already gave shape + Y-yaw variation; Rec 7 adds the two
    /// MISSING variations the note calls out for trunks: a seeded ±20% HEIGHT scale + a small APEX LEAN
    /// (a few degrees off vertical). Rocks already carry a seeded Y-yaw + tilt (BuildRock) — that part of
    /// Rec 7 was already satisfied; this guard pins the NEW tree variation + the LOCK.
    ///
    /// THE LOCK (AC7a): the seed-42 island SHAPE / NavMesh / waterline must be UNCHANGED. Those are driven
    /// by SeedOffset/HeightAtRadial/ShoreRadiusAt/CliffinessAt — the WORLD-GEN fields — NOT by the scatter
    /// `rnd` stream. The tree variation draws from a per-tree DERIVED sub-stream keyed off plant POSITION
    /// (NOT extra draws on the shared scatter rnd), so the existing tree/rock/grass PLACEMENT is byte-
    /// identical and the world geometry is untouched by construction. This guard asserts BOTH: the variation
    /// is present (varied height + real lean) AND the world-gen fields are unchanged (the lock).
    /// </summary>
    public class SeededScatterVariationTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        private static void Offset(out float ox, out float oz) =>
            LowPolyZoneGen.SeedOffset(LowPolyZoneGen.IslandSeed, out ox, out oz);

        private static Transform[] FindTrees()
        {
            return Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "LP_Tree").ToArray();
        }

        // ---- AC7a — the NEW tree HEIGHT variation + APEX LEAN ----

        [Test]
        public void Trees_HaveSeededHeightVariation_NotAllUniformY()
        {
            // ±20% non-uniform HEIGHT scale: the trees' Y-scale relative to their X-scale (girth) must SPAN a
            // real range — a uniform Vector3.one * scale (the prior code) would have Y/X == 1 for every tree.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var trees = FindTrees();
            Assert.Greater(trees.Length, 50, $"need a meaningful tree population for the distribution check — {trees.Length}");

            var yOverX = trees.Where(t => t.localScale.x > 1e-4f)
                              .Select(t => t.localScale.y / t.localScale.x).ToArray();
            float min = yOverX.Min(), max = yOverX.Max();
            // The authored band is 0.80..1.20. Require a real spread (a revert to uniform Y/X==1 fails) and
            // both a genuinely SHORT and a genuinely TALL tree present.
            Assert.Greater(max - min, 0.20f,
                $"the trees must carry seeded ±20% HEIGHT variation — Y/X spread {min:F2}..{max:F2} must be real " +
                "(a uniform scale gives every tree Y/X == 1, spread 0)");
            Assert.Less(min, 0.92f, "the scatter includes genuinely SHORT trees (varied, not cloned)");
            Assert.Greater(max, 1.08f, "the scatter includes genuinely TALL trees (varied, not cloned)");
            Assert.GreaterOrEqual(min, 0.80f - 0.02f, "no tree shorter than the authored ~0.80 floor");
            Assert.LessOrEqual(max, 1.20f + 0.02f, "no tree taller than the authored ~1.20 ceiling");
        }

        [Test]
        public void Trees_HaveSeededApexLean_NotAllPerfectlyUpright()
        {
            // APEX LEAN: each tree is tilted a few degrees off vertical (the trunk's local up no longer == world
            // up). Assert MOST trees have a real lean (the up-axis tilt angle is in the authored 3..8° band) and
            // that the lean DIRECTIONS vary (not all leaning the same way — a per-instance seed, not a global tilt).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var trees = FindTrees();
            Assert.Greater(trees.Length, 50, "need trees to check lean");

            int leaned = 0; var dirs = new System.Collections.Generic.List<float>();
            foreach (var t in trees)
            {
                Vector3 up = t.up; // the tree's local up in world space
                float tiltDeg = Vector3.Angle(up, Vector3.up);
                if (tiltDeg > 1f) leaned++;
                // lean direction = the azimuth of the projected-up tilt
                Vector3 flat = new Vector3(up.x, 0f, up.z);
                if (flat.sqrMagnitude > 1e-6f) dirs.Add(Mathf.Atan2(up.z, up.x) * Mathf.Rad2Deg);
            }
            Assert.Greater(leaned, trees.Length * 0.9f,
                $"nearly every tree must have an APEX LEAN (off vertical) — only {leaned}/{trees.Length} leaned " +
                "(a revert to a perfectly-upright tree fails)");
            // sample the lean angles are in a believable small band (not a topple)
            float maxTilt = trees.Max(t => Vector3.Angle(t.up, Vector3.up));
            Assert.Less(maxTilt, 12f, $"the lean must stay SMALL (max {maxTilt:F1}° < 12°) — a gentle lean, not a topple");
            // directions vary (a global tilt would cluster; per-instance seeds spread across the compass)
            Assert.Greater(dirs.Max() - dirs.Min(), 90f,
                "the lean DIRECTIONS must vary across instances (per-tree seed) — not all leaning the same way");
        }

        [Test]
        public void Trees_StillSeatedOnGround_LeanDoesNotLiftTheBase()
        {
            // The lean is composed as a tilt of the whole tree about a HORIZONTAL axis, so the trunk BASE stays
            // at the plant point (only the apex leans). Assert every tree root sits at ~the ground it was
            // planted on (not lifted into the air by the transform) — the lean must not float the trunk.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Offset(out float ox, out float oz);
            var trees = FindTrees();
            int floating = 0;
            foreach (var t in trees)
            {
                float groundH = LowPolyZoneGen.HeightAtRadial(t.position.x, t.position.z, ox, oz);
                // the tree root should be within ~1.5u of the sampled ground (GroundPoint raycast + slope)
                if (Mathf.Abs(t.position.y - groundH) > 1.5f) floating++;
            }
            Assert.Less(floating, trees.Length / 20,
                $"trees must stay SEATED on the ground (the lean tilts the apex, not the base) — {floating}/" +
                $"{trees.Length} are >1.5u off the sampled ground");
        }

        // ---- AC7a — THE LOCK: the seed-42 world-gen fields are UNCHANGED ----

        [Test]
        public void Seed42_IslandShapeFields_AreDeterministic_AndUnchanged()
        {
            // The world-gen fields (the island SHAPE / waterline / NavMesh surface) are driven by SeedOffset/
            // HeightAtRadial/ShoreRadiusAt/CliffinessAt — NOT the scatter rnd. The scatter variation draws from
            // a derived per-tree sub-stream, so it CANNOT perturb these. Assert determinism (same input ->
            // same output) on a sweep around the island; a regression that routed world-gen through the
            // scatter stream (or otherwise drifted the seed-42 shape) would break this.
            Offset(out float ox, out float oz);
            const int azimuths = 64;
            for (int a = 0; a < azimuths; a++)
            {
                float ang = a / (float)azimuths * Mathf.PI * 2f;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                float coast1 = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox, oz);
                float coast2 = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox, oz);
                Assert.AreEqual(coast1, coast2, 1e-6f, "ShoreRadiusAt must be deterministic (the warped coast is stable)");

                float r = coast1 - 10f;
                float h1 = LowPolyZoneGen.HeightAtRadial(dx * r, dz * r, ox, oz);
                float h2 = LowPolyZoneGen.HeightAtRadial(dx * r, dz * r, ox, oz);
                Assert.AreEqual(h1, h2, 1e-6f, "HeightAtRadial must be deterministic (the terrain surface is stable)");

                float c1 = LowPolyZoneGen.CliffinessAt(dx, dz, ox, oz);
                float c2 = LowPolyZoneGen.CliffinessAt(dx, dz, ox, oz);
                Assert.AreEqual(c1, c2, 1e-6f, "CliffinessAt must be deterministic (the beach/cliff layout is stable)");
            }
            // The waterline anchor + seed must be the LOCKED values (a change here would re-shape the island).
            Assert.AreEqual(42, LowPolyZoneGen.IslandSeed, "the island seed must stay LOCKED at 42 (world-is-big-round-island)");
            Assert.AreEqual(120f, LowPolyZoneGen.IslandShoreR, 1e-4f, "the mean waterline radius must be unchanged");
        }

        [Test]
        public void ShippedScene_TreesStillOnLandmass_LeanDidNotPushThemIntoTheSea()
        {
            // The lean/height change must not have shifted any tree off the warped landmass (the AC5 contract
            // from RoundIslandTests, re-checked here because the transform changed). The plant point is
            // unchanged (the sub-stream only affects rotation/scale), so this should hold.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Offset(out float ox, out float oz);
            var trees = FindTrees();
            int offLand = trees.Count(t =>
            {
                float coast = LowPolyZoneGen.ShoreRadiusAt(t.position.x, t.position.z, ox, oz);
                return Mathf.Sqrt(t.position.x * t.position.x + t.position.z * t.position.z) > coast;
            });
            Assert.AreEqual(0, offLand,
                $"every tree must still sit on the warped landmass — {offLand}/{trees.Length} are past the coast " +
                "(the lean/height change must not move the plant point)");
        }
    }
}
