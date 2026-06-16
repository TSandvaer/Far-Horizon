using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// COAST POLISH guards (ticket 86ca9xyqa). The Sponsor APPROVED the seed-42 organic island shape but
    /// flagged five coast issues on the PR #50 soak. Each test pins one AC so a regression fails in headless
    /// CI. The shape is sampled directly from the radial field (no build) where it's a geometry contract; the
    /// shipped Boot scene's baked terrain vertex colours are read where the contract is "what actually ships"
    /// (binary scenes can't be GUID-grepped — the EditMode reader is the authoritative check, per
    /// unity-conventions.md). The seed offset is derived via SeedOffset(IslandSeed) so the tests sample the
    /// SAME warped island the build ships (SEED 42 is LOCKED — these tests do NOT re-roll the shape).
    ///
    ///   #5a WATER MEETS THE SAND — the beach dips BELOW WaterY over the wet shelf (water laps it, no dry gap)
    ///   #5b WIDER BEACH          — the sand strip spans a named minimum width
    ///   #5c WARM GOLDEN SAND     — the sand anchors are warm-golden AND the shipped beach verts read warm
    ///   #6a FOAM BACK            — the shipped terrain carries a foam band at the waterline on every side
    ///   #6b SMOOTH WATERLINE     — finer coastline tessellation (the staircase metric)
    /// </summary>
    public class CoastPolishTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        private static void Offset(out float ox, out float oz) =>
            LowPolyZoneGen.SeedOffset(LowPolyZoneGen.IslandSeed, out ox, out oz);

        // ---- #5a — WATER MEETS THE SAND: the beach dips BELOW WaterY over a wet shelf so the flat water
        //      plane laps onto the sand. The PRIOR profile reached WaterY only exactly AT the coast (a dry
        //      knife-edge). Sample beach sectors and require a real submerged wet band inside the coast. ----
        [Test]
        public void AC5a_BeachDipsBelowWaterline_SoWaterLapsTheSand_NoDryGap()
        {
            Offset(out float ox, out float oz);
            const int azimuths = 144;
            int beachSectorsChecked = 0, withWetShelf = 0;
            for (int a = 0; a < azimuths; a++)
            {
                float ang = a / (float)azimuths * Mathf.PI * 2f;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                if (LowPolyZoneGen.CliffinessAt(dx, dz, ox, oz) > 0.25f) continue; // beach sectors only
                beachSectorsChecked++;
                float coast = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox, oz);
                // Just INSIDE the coast (the seaward wet shelf) the terrain must sit BELOW WaterY → the flat
                // water plane covers it (the water laps onto the sand). Sample mid-shelf.
                float rShelf = coast - LowPolyZoneGen.WetShelfWidth * 0.5f;
                float h = LowPolyZoneGen.HeightAtRadial(dx * rShelf, dz * rShelf, ox, oz);
                if (h < LowPolyZoneGen.WaterY - 0.01f) withWetShelf++;
            }
            Assert.Greater(beachSectorsChecked, 0, "expected at least one beach sector to sample");
            // Essentially every beach sector must have a submerged wet shelf (water meets the sand all around).
            Assert.Greater(withWetShelf, beachSectorsChecked * 0.9f,
                $"the water must LAP the sand on beach sectors — only {withWetShelf}/{beachSectorsChecked} beach " +
                $"azimuths have a submerged wet shelf (terrain below WaterY inside the coast); the dry-gap " +
                "knife-edge is back if this is low.");
        }

        // ---- #5b — WIDER BEACH: the sand strip must span a named minimum (it was a thin 9u line). ----
        [Test]
        public void AC5b_BeachStrip_IsWiderThanTheOldNineUnitLine()
        {
            Assert.GreaterOrEqual(LowPolyZoneGen.BeachWidth, 14f,
                $"the beach must be WIDER than the old 9u line — BeachWidth is {LowPolyZoneGen.BeachWidth}u (≥14u).");
            // And the geometry must actually carry a sand strip that wide: on a beach sector the land stays at
            // ~grass level at the strip's inner edge and only descends toward the water across the strip.
            Offset(out float ox, out float oz);
            float bestAng = 0f, minCliff = 1f;
            for (int a = 0; a < 144; a++)
            {
                float ang = a / 144f * Mathf.PI * 2f;
                float c = LowPolyZoneGen.CliffinessAt(Mathf.Cos(ang), Mathf.Sin(ang), ox, oz);
                if (c < minCliff) { minCliff = c; bestAng = ang; }
            }
            float dx = Mathf.Cos(bestAng), dz = Mathf.Sin(bestAng);
            float coast = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox, oz);
            float rInner = coast - LowPolyZoneGen.BeachWidth + 1f;
            float hInner = LowPolyZoneGen.HeightAtRadial(dx * rInner, dz * rInner, ox, oz);
            Assert.Greater(hInner, LowPolyZoneGen.WaterY,
                $"the inner edge of the {LowPolyZoneGen.BeachWidth}u beach strip must be above water (a real " +
                $"sand band, not all submerged) — h={hInner:F2} at r={rInner:F0}.");
        }

        // ---- #5c — WARM GOLDEN SAND: anchors are warm-golden (R>G>B, real warmth) AND the shipped beach
        //      verts read warm. The organic rewrite did NOT cool the anchors — it dragged the seaward sand to
        //      the dark damp tone on every sector. These guard both the anchors AND the shipped result. ----
        [Test]
        public void AC5c_SandAnchors_AreWarmGolden_RgbOrderedWithRealWarmth()
        {
            foreach (var (name, c) in new[] {
                ("SandLo", LowPolyZoneGen.SandLo), ("SandHi", LowPolyZoneGen.SandHi),
                ("SandDamp", LowPolyZoneGen.SandDamp) })
            {
                Assert.Greater(c.r, c.g, $"{name} must be warm (R>G) — {c}");
                Assert.Greater(c.g, c.b, $"{name} must be golden (G>B) — {c}");
                // Real warmth: R noticeably above B (a grey/cool sand has R≈B).
                Assert.Greater(c.r - c.b, 0.20f, $"{name} must be clearly WARM (R-B>0.20) — {c} (R-B={c.r-c.b:F2})");
                Assert.Greater(c.r, 0.6f, $"{name} must be a bright sunlit sand, not a dark cool damp — {c}");
            }
        }

        [Test]
        public void AC5c_ShippedBeachVerts_ReadWarmGolden_NotCoolDamp()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "Ground_Play must exist");
            var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.vertices; var cols = mesh.colors;
            Offset(out float ox, out float oz);
            // Collect verts in the DRY sand band on BEACH sectors (just inland of the waterline, above foam),
            // and require the sand-coloured ones to read warm-golden (R>G>B, clear warmth) — not cool damp.
            int warmSand = 0, sampled = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                float coast = LowPolyZoneGen.ShoreRadiusAt(v.x, v.z, ox, oz);
                if (LowPolyZoneGen.CliffinessAt(v.x, v.z, ox, oz) > 0.25f) continue; // beach sectors
                // The dry sand band: inland of the wet shelf, within the beach strip, clear of the foam core.
                if (r > coast - LowPolyZoneGen.WetShelfWidth - 1f) continue;
                if (r < coast - LowPolyZoneGen.BeachWidth) continue;
                Color c = cols[i];
                // Skip foam-dominated verts (near-white) — we want the SAND read.
                if (c.r > 0.82f && c.g > 0.80f && c.b > 0.74f) continue;
                // Skip grass (G>R) — only sand-leaning verts.
                if (c.g >= c.r) continue;
                sampled++;
                if (c.r > c.g && c.g > c.b && (c.r - c.b) > 0.10f) warmSand++;
            }
            Assert.Greater(sampled, 0, "expected to sample dry-sand beach verts in the shipped scene");
            Assert.Greater(warmSand, sampled * 0.85f,
                $"the shipped beach sand must read WARM GOLDEN (R>G>B, R-B>0.10) — only {warmSand}/{sampled} " +
                "sand verts are warm; the cool/dark damp drag is back if this is low.");
        }

        // ---- #6a — FOAM BACK: the shipped terrain must carry a foam band at the waterline on every side
        //      (the water mesh's foam is occluded by the sand shelf for r<coast, so the visible foam is the
        //      terrain's). Sample the shipped terrain verts for foam-coloured verts in all four quadrants. ----
        [Test]
        public void AC6a_ShippedTerrain_CarriesFoamAtTheWaterline_AllSides()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "Ground_Play must exist");
            var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.vertices; var cols = mesh.colors;
            var foam = LowPolyZoneGen.FoamEdge;
            Offset(out float ox, out float oz);
            var quads = new bool[4];
            int foamVerts = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                float coast = LowPolyZoneGen.ShoreRadiusAt(v.x, v.z, ox, oz);
                if (Mathf.Abs(r - coast) > 6f) continue; // only the waterline band
                Color c = cols[i];
                bool isFoam = c.r > 0.78f && c.g > 0.76f && c.b > 0.68f &&
                              Mathf.Abs(c.r - foam.r) < 0.16f && Mathf.Abs(c.g - foam.g) < 0.16f;
                if (!isFoam) continue;
                foamVerts++;
                float deg = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg; if (deg < 0) deg += 360f;
                quads[Mathf.Clamp((int)(deg / 90f), 0, 3)] = true;
            }
            Assert.Greater(foamVerts, 0, "the terrain must carry foam at the waterline (AC#6a — foam back)");
            Assert.AreEqual(4, quads.Count(q => q),
                $"foam must follow the warped waterline in ALL FOUR quadrants (every side) — {foamVerts} foam verts.");
        }

        // ---- #6b — SMOOTH WATERLINE: the coastline tessellation must be FINER than before so the terrain↔
        //      water staircase reads as a smooth wave. The grid cell size is the step size of the staircase. ----
        [Test]
        public void AC6b_CoastlineTessellation_IsFinerThanBefore_SmallerSteps()
        {
            float cell = (LowPolyZoneGen.IslandGridHalf * 2f) / LowPolyZoneGen.IslandSegX;
            // 150 segs over 330u = 2.2u cells (the jagged steps). The fix raises seg count → smaller cells.
            Assert.Less(cell, 2.0f,
                $"the coastline grid cell (the staircase step size) must be < 2.0u for a smooth waterline — " +
                $"it is {cell:F2}u (seg {LowPolyZoneGen.IslandSegX} over {LowPolyZoneGen.IslandGridHalf * 2f:F0}u).");
            Assert.GreaterOrEqual(LowPolyZoneGen.IslandSegX, 200,
                $"coastline subdivision must be ≥200 (was 150) — it is {LowPolyZoneGen.IslandSegX}.");
        }

        // ---- SHIPPED-SCENE wet-shelf proof: the terrain mesh must actually carry verts BELOW WaterY just
        //      inside the coast on beach sectors (the wet shelf is in the shipped geometry, not just the field). ----
        [Test]
        public void AC5a_ShippedTerrain_HasSubmergedWetShelf_InsideTheCoast()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "Ground_Play must exist");
            var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.vertices;
            Offset(out float ox, out float oz);
            int submergedInsideCoast = 0;
            foreach (var v in verts)
            {
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                float coast = LowPolyZoneGen.ShoreRadiusAt(v.x, v.z, ox, oz);
                if (LowPolyZoneGen.CliffinessAt(v.x, v.z, ox, oz) > 0.25f) continue; // beach sectors
                // INSIDE the coast (r < coast) but BELOW WaterY = the lapped wet shelf.
                if (r < coast && r > coast - LowPolyZoneGen.WetShelfWidth - 0.5f && v.y < LowPolyZoneGen.WaterY - 0.01f)
                    submergedInsideCoast++;
            }
            Assert.Greater(submergedInsideCoast, 50,
                $"the shipped terrain must carry a submerged wet shelf inside the coast (water laps the sand) — " +
                $"only {submergedInsideCoast} verts are below WaterY just inside the coast on beach sectors.");
        }
    }
}
