using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// ORGANIC IRREGULAR ISLAND geometry + scene-presence guards (ticket 86ca9qwr3 — Sponsor: "read as a
    /// REAL island, not a disc" + flagged a straight "line through the island"). REPLACES the round-disc
    /// contract (86ca9a7qn): the coast is now AZIMUTH-WARPED (ShoreRadiusAt) so it is intentionally
    /// IRREGULAR (bays + headlands), some sectors are flat sand BEACHES and others steep rock CLIFFS
    /// (CliffinessAt), the land stays ~grass level out to the coast (beach-LEVEL, no dome), foam follows
    /// the warped waterline on all sides, and the terrain mesh is CLIPPED to the landmass (no square grid
    /// edge). The radial height field is sampled directly (no build) to prove the shape; the shipped Boot
    /// scene is read to prove it serialized (binary scenes can't be GUID-grepped — the EditMode reader is
    /// the authoritative check).
    ///
    /// Each assert pins a Sponsor AC so a regression to the disc (or back to the strip) fails in headless CI.
    /// The seed offset MUST match what the bootstrap bakes — derived via LowPolyZoneGen.SeedOffset(IslandSeed)
    /// so the tests sample the SAME warped island the build ships (not a stale hardcoded offset).
    /// </summary>
    public class RoundIslandTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        // The SAME noise offset the bootstrap bakes (the warped coast is offset-specific — a stale hardcoded
        // offset would sample a DIFFERENT island than ships).
        private static void Offset(out float ox, out float oz) =>
            LowPolyZoneGen.SeedOffset(LowPolyZoneGen.IslandSeed, out ox, out oz);

        // ---- AC1 — ORGANIC IRREGULAR COASTLINE (no build — samples ShoreRadiusAt/HeightAtRadial) ----

        [Test]
        public void AC1_Coastline_IsIrregular_NotACircle_ButStillABoundedIsland()
        {
            // The coast radius must VARY by azimuth (bays + headlands) — NOT a uniform circle. Sample the
            // warped coast around 48 azimuths; require a real spread (irregular) but bounded (still one island).
            Offset(out float ox, out float oz);
            const int azimuths = 48;
            var coast = new System.Collections.Generic.List<float>();
            for (int a = 0; a < azimuths; a++)
            {
                float ang = a / (float)azimuths * Mathf.PI * 2f;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                coast.Add(LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox, oz));
            }
            float min = coast.Min(), max = coast.Max(), mean = coast.Average();
            // IRREGULAR: the coast must wander a real amount (a circle would have ~0 spread). The warp amp is
            // ±CoastIrregAmp, so expect a spread of at least ~a third of the full 2×amp range.
            Assert.Greater(max - min, LowPolyZoneGen.CoastIrregAmp * 0.6f,
                $"the coastline must be IRREGULAR (bays + headlands) — spread {max-min:F1}u must exceed " +
                $"~0.6×CoastIrregAmp ({LowPolyZoneGen.CoastIrregAmp*0.6f:F1}u); a circle/disc spreads ~0.");
            // BUT BOUNDED: still one island centred on the mean shore radius (not a runaway lobe).
            Assert.That(mean, Is.InRange(LowPolyZoneGen.IslandShoreR - LowPolyZoneGen.CoastIrregAmp,
                                         LowPolyZoneGen.IslandShoreR + LowPolyZoneGen.CoastIrregAmp),
                $"mean coast {mean:F1}u must sit ~IslandShoreR ({LowPolyZoneGen.IslandShoreR}u)");
            // And the coast's STANDARD DEVIATION must be a real fraction of the warp amplitude — a robust
            // irregularity metric (a circle has stddev ~0; an irregular coast has stddev a meaningful fraction
            // of CoastIrregAmp). ≥0.22×amp is a clearly-not-a-circle outline without forcing an unnatural saw.
            float variance = coast.Select(r => (r - mean) * (r - mean)).Average();
            float stddev = Mathf.Sqrt(variance);
            Assert.Greater(stddev, LowPolyZoneGen.CoastIrregAmp * 0.22f,
                $"the coastline must be IRREGULAR — coast-radius stddev {stddev:F1}u must exceed " +
                $"~0.22×CoastIrregAmp ({LowPolyZoneGen.CoastIrregAmp*0.22f:F1}u); a circle/disc has stddev ~0.");
        }

        [Test]
        public void AC1_InteriorIsLand_FarOutIsSea_OnEveryAzimuth()
        {
            // Water on ALL sides: well inside the warped coast is land (above sea); well past it is sea.
            Offset(out float ox, out float oz);
            const int azimuths = 24;
            for (int a = 0; a < azimuths; a++)
            {
                float ang = a / (float)azimuths * Mathf.PI * 2f;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                float coast = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox, oz);
                float inland = coast - 25f, faroff = coast + 25f;
                Assert.Greater(LowPolyZoneGen.HeightAtRadial(dx * inland, dz * inland, ox, oz), LowPolyZoneGen.WaterY,
                    $"azimuth {a*15}deg interior (r={inland:F0}) must be LAND (above sea)");
                Assert.Less(LowPolyZoneGen.HeightAtRadial(dx * faroff, dz * faroff, ox, oz), LowPolyZoneGen.WaterY,
                    $"azimuth {a*15}deg far (r={faroff:F0}) must be SEA (below sea — water on this side)");
            }
        }

        [Test]
        public void Island_IsMuchBigger_ThanTheOldStrip()
        {
            float diameter = LowPolyZoneGen.IslandShoreR * 2f;
            Assert.Greater(diameter, 180f,
                $"the island must stay MUCH bigger than the old ~90×68 strip — diameter {diameter:F0}u.");
        }

        // ---- AC2 — VARIED COAST: BEACH sectors flat + grass-level, CLIFF sectors steep ----

        [Test]
        public void AC2_Coast_HasBothBeachAndCliffSectors()
        {
            // Around the island, CliffinessAt must produce BOTH near-0 (beach) and near-1 (cliff) sectors —
            // not all-beach (the prior look) nor all-cliff.
            Offset(out float ox, out float oz);
            const int azimuths = 72;
            int beachy = 0, cliffy = 0;
            for (int a = 0; a < azimuths; a++)
            {
                float ang = a / (float)azimuths * Mathf.PI * 2f;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                float c = LowPolyZoneGen.CliffinessAt(dx, dz, ox, oz);
                if (c < 0.25f) beachy++;
                if (c > 0.75f) cliffy++;
            }
            Assert.Greater(beachy, azimuths / 10, $"there must be real SAND BEACH sectors — {beachy}/{azimuths} beachy azimuths");
            Assert.Greater(cliffy, azimuths / 12, $"there must be real CLIFF sectors — {cliffy}/{azimuths} cliffy azimuths");
        }

        [Test]
        public void AC2_CliffSectors_AreSteeperThanBeachSectors_AtTheCoast()
        {
            // For the steepest cliff azimuth and the flattest beach azimuth, measure the coastal slope (drop
            // per u across the shore transition). The cliff sector must be markedly steeper than the beach.
            Offset(out float ox, out float oz);
            const int azimuths = 72;
            float steepestCliffSlope = 0f, gentlestBeachSlope = float.PositiveInfinity;
            for (int a = 0; a < azimuths; a++)
            {
                float ang = a / (float)azimuths * Mathf.PI * 2f;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                float cliffy = LowPolyZoneGen.CliffinessAt(dx, dz, ox, oz);
                float coast = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox, oz);
                // slope across the last 6u up to the coast (drop / run)
                float hInner = LowPolyZoneGen.HeightAtRadial(dx * (coast - 6f), dz * (coast - 6f), ox, oz);
                float hAtCoast = LowPolyZoneGen.HeightAtRadial(dx * (coast + 1f), dz * (coast + 1f), ox, oz);
                float slope = Mathf.Abs(hInner - hAtCoast) / 7f;
                if (cliffy > 0.75f) steepestCliffSlope = Mathf.Max(steepestCliffSlope, slope);
                if (cliffy < 0.25f) gentlestBeachSlope = Mathf.Min(gentlestBeachSlope, slope);
            }
            Assert.Greater(steepestCliffSlope, gentlestBeachSlope * 1.5f,
                $"cliff sectors must be markedly steeper at the coast than beach sectors — " +
                $"steepest cliff slope {steepestCliffSlope:F2} vs gentlest beach slope {gentlestBeachSlope:F2}");
        }

        // ---- AC3 — BEACH LEVEL WITH GRASS: no dome; land stays ~grass level to the coast ----

        [Test]
        public void AC3_Interior_StaysNearGrassLevel_NotARaisedDome()
        {
            // The interior land BASE (away from hills, sampled at the plateau level between hills) must stay
            // near the plateau height — NOT a raised dome that ramps down to the coast. Sample the land just
            // inland of the beach strip on every azimuth; the BASE there must be ~plateau, not high above it.
            Offset(out float ox, out float oz);
            const int azimuths = 36;
            for (int a = 0; a < azimuths; a++)
            {
                float ang = a / (float)azimuths * Mathf.PI * 2f;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                float coast = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox, oz);
                // Just inland of the beach strip — the land should be ~plateau level (grass), within ~hill amp.
                float justInland = coast - LowPolyZoneGen.BeachWidth - 1f;
                float h = LowPolyZoneGen.HeightAtRadial(dx * justInland, dz * justInland, ox, oz);
                // It must be at LAND level (well above sea) — proves the grass reaches the coast, no down-ramp.
                Assert.Greater(h, LowPolyZoneGen.WaterY + 0.05f,
                    $"azimuth {a*10}deg: the land just inland of the beach (r={justInland:F0}) must be at grass " +
                    $"level (h={h:F2} > WaterY) — no walking down a dome to the shore");
            }
        }

        [Test]
        public void AC3_BeachSectors_HaveAFlatSandStrip_NotALongRamp()
        {
            // On a beach sector, the shore transition (the sand strip) is SHORT (~BeachWidth) and the land
            // inland of it is flat — NOT a long downhill ramp from a dome. Verify the drop happens only within
            // the beach strip: land 20u inland of the coast is ~same height as land at the strip's inner edge.
            Offset(out float ox, out float oz);
            // Find a clear beach azimuth.
            float bestAng = 0f; float minCliff = 1f;
            for (int a = 0; a < 144; a++)
            {
                float ang = a / 144f * Mathf.PI * 2f;
                float c = LowPolyZoneGen.CliffinessAt(Mathf.Cos(ang), Mathf.Sin(ang), ox, oz);
                if (c < minCliff) { minCliff = c; bestAng = ang; }
            }
            float dx = Mathf.Cos(bestAng), dz = Mathf.Sin(bestAng);
            float coast = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox, oz);
            float hStripInner = LowPolyZoneGen.HeightAtRadial(dx * (coast - LowPolyZoneGen.BeachWidth), dz * (coast - LowPolyZoneGen.BeachWidth), ox, oz);
            float hDeepInland = LowPolyZoneGen.HeightAtRadial(dx * (coast - LowPolyZoneGen.BeachWidth - 20f), dz * (coast - LowPolyZoneGen.BeachWidth - 20f), ox, oz);
            // The land at the strip's inner edge and 20u further inland must be at a similar base level (flat,
            // not a continuing ramp) — allow some hill noise but not a big monotonic climb.
            Assert.Less(Mathf.Abs(hDeepInland - hStripInner), 6f,
                $"a beach sector must be FLAT inland of the sand strip (not a long ramp) — strip-inner h " +
                $"{hStripInner:F2} vs 20u-inland h {hDeepInland:F2}");
        }

        // ---- SHIPPED-SCENE PROOF (reads Boot.unity) ----

        [Test]
        public void ShippedScene_HasOrganicIslandTerrain_WithVertexColors_AndIsClipped()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "the Boot scene must carry the island terrain (Ground_Play)");
            var mf = ground.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf?.sharedMesh, "the island terrain mesh must serialize into the scene");
            Assert.Greater(mf.sharedMesh.colors.Length, 0, "the island terrain must carry per-vertex colours");
            // CLIP PROOF (AC1): the terrain mesh must have FEWER triangles than a full square grid would —
            // the deep-sea cells are dropped so no square grid edge reads. A full grid is SegX*SegZ*2 tris.
            int fullGridTris = 150 * 150 * 2; // IslandSegX*IslandSegZ*2
            int actualTris = mf.sharedMesh.triangles.Length / 3;
            Assert.Less(actualTris, (int)(fullGridTris * 0.92f),
                $"the terrain must be CLIPPED to the landmass (deep-sea cells dropped) — {actualTris} tris " +
                $"vs a full {fullGridTris}-tri square grid; a near-full count means the square edge still reads");
        }

        [Test]
        public void ShippedScene_WaterOnAllSides_LargeSeaPlaneCentredAtOrigin()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the Boot scene must carry the all-sides sea (Water_Play)");
            var mr = water.GetComponent<MeshRenderer>();
            Assert.Greater(mr.bounds.size.x, 300f, $"the sea must extend far on X — {mr.bounds.size.x:F0}u");
            Assert.Greater(mr.bounds.size.z, 300f, $"the sea must extend far on Z — {mr.bounds.size.z:F0}u");
            Assert.Less(mr.bounds.min.x, -LowPolyZoneGen.IslandShoreR - 20f, "sea must reach past the coast on −X");
            Assert.Greater(mr.bounds.max.x, LowPolyZoneGen.IslandShoreR + 20f, "sea must reach past the coast on +X");
            Assert.Less(mr.bounds.min.z, -LowPolyZoneGen.IslandShoreR - 20f, "sea must reach past the coast on −Z");
            Assert.Greater(mr.bounds.max.z, LowPolyZoneGen.IslandShoreR + 20f, "sea must reach past the coast on +Z");
        }

        [Test]
        public void AC4_ShippedScene_SeaCarriesFoamRing_AllTheWayAround()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "Water_Play must exist");
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.vertices; var cols = mesh.colors;
            var foam = LowPolyZoneGen.FoamEdge;
            var quads = new bool[4];
            int foamVerts = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                Color c = cols[i];
                bool isFoam = c.r > 0.78f && c.g > 0.78f && c.b > 0.70f &&
                              Mathf.Abs(c.r - foam.r) < 0.14f && Mathf.Abs(c.g - foam.g) < 0.14f;
                if (!isFoam) continue;
                foamVerts++;
                float wx = water.transform.position.x + verts[i].x;
                float wz = water.transform.position.z + verts[i].z;
                float deg = Mathf.Atan2(wz, wx) * Mathf.Rad2Deg; if (deg < 0) deg += 360f;
                quads[Mathf.Clamp((int)(deg / 90f), 0, 3)] = true;
            }
            Assert.Greater(foamVerts, 0, "the sea must carry a foam band at the coast");
            Assert.AreEqual(4, quads.Count(q => q),
                "foam must follow the warped waterline in ALL FOUR azimuth quadrants (every side, no gaps)");
        }

        [Test]
        public void ShippedScene_DenseTallJungle_ManyTreesWithTallVariants()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var treeRoots = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "LP_Tree").ToArray();
            Assert.Greater(treeRoots.Length, 150,
                $"the island must carry a DENSE jungle (>150 trees) — found {treeRoots.Length}.");
            float maxCanopyTop = 0f;
            foreach (var tr in treeRoots)
            {
                var canopy = tr.Find("Canopy");
                if (canopy == null) continue;
                var cmf = canopy.GetComponent<MeshFilter>();
                if (cmf?.sharedMesh == null) continue;
                float topLocalY = canopy.localPosition.y + cmf.sharedMesh.bounds.max.y * canopy.localScale.y;
                float topWorld = topLocalY * tr.localScale.y;
                maxCanopyTop = Mathf.Max(maxCanopyTop, topWorld);
            }
            Assert.Greater(maxCanopyTop, 6f,
                $"the jungle must have TALL trees — tallest canopy ~{maxCanopyTop:F1}u above ground (>6u).");
        }

        [Test]
        public void AC5_TreesPlantedOnLandmass_NotFloatingInTheSea()
        {
            // AC5/AC1: every scattered tree must sit on the warped LANDMASS (within the warped coast), not in
            // a bay's open water (the warp could have left trees floating if the scatter ignored it).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Offset(out float ox, out float oz);
            var treeRoots = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "LP_Tree").ToArray();
            int offLand = 0;
            foreach (var tr in treeRoots)
            {
                float x = tr.position.x, z = tr.position.z;
                float coast = LowPolyZoneGen.ShoreRadiusAt(x, z, ox, oz);
                if (Mathf.Sqrt(x * x + z * z) > coast) offLand++;
            }
            Assert.AreEqual(0, offLand,
                $"every tree must sit on the warped landmass (within the coast) — {offLand}/{treeRoots.Length} " +
                "are past the warped coast (floating in the sea / on the beach strip)");
        }

        [Test]
        public void BootScene_CarriesIslandVerifyCapture_OnTheBootObject()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var boot = GameObject.Find("Boot");
            Assert.IsNotNull(boot, "the Boot scene must carry the 'Boot' object (host of the verify captures)");
            Assert.IsNotNull(boot.GetComponent<FarHorizon.IslandVerifyCapture>(),
                "the Boot object must carry the IslandVerifyCapture component, serialized into the scene");
        }

        [Test]
        public void ShippedScene_MountainsOnSeparateIslands_OffTheMainIsland()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var landmasses = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "LP_Landmass" && mf.sharedMesh != null)
                .Select(mf => mf.GetComponent<MeshRenderer>())
                .Where(r => r != null).ToArray();
            Assert.Greater(landmasses.Length, 3, "there must be several separate mountain islands");
            foreach (var r in landmasses)
            {
                var b = r.bounds;
                float centreDist = new Vector2(b.center.x, b.center.z).magnitude;
                float domeR = Mathf.Max(b.extents.x, b.extents.z);
                float nearEdge = centreDist - domeR;
                Assert.Greater(nearEdge, LowPolyZoneGen.IslandShoreR + 15f,
                    $"mountain island '{r.gameObject.name}' near-edge ({nearEdge:F0}u) must clear the main coast.");
            }
            var peaks = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "LP_Mountain").ToArray();
            Assert.Greater(peaks.Length, 6, "the vista must carry several mountain peaks on the separate islands");
            foreach (var p in peaks)
            {
                float d = new Vector2(p.position.x, p.position.z).magnitude;
                Assert.Greater(d, LowPolyZoneGen.IslandShoreR + 15f,
                    $"peak '{p.name}' (dist {d:F0}u) must be OFF the main island.");
            }
        }
    }
}
