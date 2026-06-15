using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// BIG ROUND ISLAND geometry + scene-presence guards (ticket 86ca9a7qn — Sponsor: "make the island
    /// much much bigger, round, with water on all sides ... quite big with elevation (hills). dense
    /// forest/jungle with high trees"). This is the FEASIBILITY-FIRST proof, in code: the radial height
    /// field is sampled directly (no build) to prove ROUNDNESS + WATER-ON-ALL-SIDES + ELEVATION, plus the
    /// shipped Boot scene is read to prove the dense tall jungle + the all-sides sea + the SEPARATE mountain
    /// islands actually serialize in (binary scenes can't be GUID-grepped — the EditMode reader is the
    /// authoritative check, the component/mesh-in-source-but-not-serialized failure class).
    ///
    /// These REPLACE the strip-era contract: the old beach-to-meadow strip (seaward-Z gradient, foam at a
    /// fixed Z, vista at strip distances) is the NEW WORLD BASIS gone radial. Each assert pins a Sponsor
    /// requirement so a regression to the strip (or a silently-shrunk island) fails in headless CI.
    /// </summary>
    public class RoundIslandTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        // ---- RADIAL HEIGHT-FIELD PROOF (no build — samples HeightAtRadial directly) ----

        [Test]
        public void Island_IsRound_LandInsideCore_SeaOutsideShore_UniformCoastRadius()
        {
            // ROUNDNESS PROOF: sample the radial height field around 24 azimuths. The coast (where the land
            // crosses WaterY) must sit at ~IslandShoreR for EVERY azimuth (a true round landmass, not a
            // strip) — interior is land (above WaterY), well past the shore is sea (below WaterY).
            const int azimuths = 24;
            float ox = 17.3f, oz = 41.9f; // fixed noise offset (the bootstrap seeds it; roundness is offset-stable)
            var coastRadii = new System.Collections.Generic.List<float>();
            for (int a = 0; a < azimuths; a++)
            {
                float ang = a / (float)azimuths * Mathf.PI * 2f;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                // March outward, find where the surface drops through WaterY (the coast).
                float coast = -1f;
                for (float r = LowPolyZoneGen.IslandCoreR; r <= LowPolyZoneGen.IslandFalloffEnd; r += 0.5f)
                {
                    float h = LowPolyZoneGen.HeightAtRadial(dx * r, dz * r, ox, oz);
                    if (h <= LowPolyZoneGen.WaterY) { coast = r; break; }
                }
                Assert.Greater(coast, 0f, $"azimuth {a*15}deg must have a coast (land dipping below the sea)");
                coastRadii.Add(coast);

                // Interior (well inside the core) is LAND; well past the shore is SEA — on every azimuth.
                Assert.Greater(LowPolyZoneGen.HeightAtRadial(dx * 40f, dz * 40f, ox, oz), LowPolyZoneGen.WaterY,
                    $"azimuth {a*15}deg interior (r=40) must be LAND (above the sea)");
                Assert.Less(LowPolyZoneGen.HeightAtRadial(dx * 145f, dz * 145f, ox, oz), LowPolyZoneGen.WaterY,
                    $"azimuth {a*15}deg far (r=145) must be SEA (below sea level — water on this side)");
            }
            // The coast radius must be ~uniform around the island (round, not a strip/lobe). Allow ±12u for
            // the noise-rolled shore wobble (a believable coast), but the spread proves it's a disc.
            float min = coastRadii.Min(), max = coastRadii.Max(), mean = coastRadii.Average();
            Assert.Less(max - min, 24f,
                $"the coast radius must be ~UNIFORM around the island (round) — spread {max-min:F1}u " +
                $"(min {min:F1}, max {max:F1}, mean {mean:F1}); a strip/lobe would spread wildly");
            Assert.That(mean, Is.InRange(LowPolyShoreLo, LowPolyShoreHi),
                $"the mean coast radius {mean:F1}u must be ~IslandShoreR ({LowPolyZoneGen.IslandShoreR}u) — the " +
                "round coast sits at the shore radius");
        }
        private const float LowPolyShoreLo = 108f, LowPolyShoreHi = 132f; // IslandShoreR 120 ± shore wobble

        [Test]
        public void Island_IsMuchBigger_ThanTheOldStrip()
        {
            // SIZE PROOF: the island diameter (2 × IslandShoreR) must be MUCH bigger than the old strip
            // (~90u wide × ~68u deep). A silently-shrunk island fails here (the showstopper "don't shrink it").
            float diameter = LowPolyZoneGen.IslandShoreR * 2f;
            Assert.Greater(diameter, 180f,
                $"the round island must be MUCH bigger than the old ~90×68 strip — diameter {diameter:F0}u " +
                "(IslandShoreR " + LowPolyZoneGen.IslandShoreR + "u). The Sponsor asked for 'much much bigger'.");
        }

        [Test]
        public void Island_HasRealElevation_HillsNotAFlatDisc()
        {
            // ELEVATION PROOF: sample the inland land on a grid; the height range must show real hills
            // (max-min well above the flat-disc base), proving "quite big with elevation (hills)".
            float ox = 17.3f, oz = 41.9f;
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            for (float x = -70f; x <= 70f; x += 5f)
            for (float z = -70f; z <= 70f; z += 5f)
            {
                if (Mathf.Sqrt(x * x + z * z) > LowPolyZoneGen.IslandCoreR) continue; // land interior only
                float h = LowPolyZoneGen.HeightAtRadial(x, z, ox, oz);
                lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
            }
            float range = hi - lo;
            Assert.Greater(range, 4f,
                $"the island interior must have REAL hills/elevation — height range {range:F1}u (lo {lo:F1}, " +
                $"hi {hi:F1}); a flat disc would be near-zero. Sponsor: 'quite big with elevation (hills)'.");
        }

        [Test]
        public void Island_SpawnRegion_IsNearFlat_ForTheSurvivalLoop()
        {
            // The immediate spawn + loop centre (within ~12u of origin) must stay near-flat so the loop
            // objects sit on level dry land + click-move reads clean (the spawn-flatten in HeightAtRadial).
            float ox = 17.3f, oz = 41.9f;
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            for (float x = -12f; x <= 12f; x += 2f)
            for (float z = -6f; z <= 18f; z += 2f) // around the spawn (0,6) + loop spots
            {
                float h = LowPolyZoneGen.HeightAtRadial(x, z, ox, oz);
                lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
            }
            Assert.Less(hi - lo, 2.5f,
                $"the spawn/loop centre must stay near-flat (range {hi-lo:F2}u) so the survival-loop objects " +
                "sit level and click-move reads clean (the spawn-flatten)");
            Assert.Greater(lo, LowPolyZoneGen.WaterY + 0.1f,
                $"the spawn/loop centre must be DRY (lowest {lo:F2}u > WaterY {LowPolyZoneGen.WaterY}) — the " +
                "castaway spawns on dry land, not in the sea");
        }

        // ---- SHIPPED-SCENE PROOF (reads Boot.unity) ----

        [Test]
        public void ShippedScene_HasBigRoundIslandTerrain_WithVertexColors()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "the Boot scene must carry the round-island terrain (Ground_Play)");
            var mf = ground.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf?.sharedMesh, "the island terrain mesh must serialize into the scene");
            Assert.Greater(mf.sharedMesh.colors.Length, 0, "the island terrain must carry per-vertex colours (sand/grass/rock/foam ramp)");
            // The terrain bounds must span the big island (much bigger than the old ~90u strip width).
            var b = ground.GetComponent<MeshRenderer>().bounds;
            Assert.Greater(b.size.x, 240f, $"the island terrain must span the big island (X extent {b.size.x:F0}u > 240u)");
            Assert.Greater(b.size.z, 240f, $"the island terrain must span the big island (Z extent {b.size.z:F0}u > 240u)");
        }

        [Test]
        public void ShippedScene_WaterOnAllSides_LargeSeaPlaneCentredAtOrigin()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the Boot scene must carry the all-sides sea (Water_Play)");
            var mr = water.GetComponent<MeshRenderer>();
            // The sea must extend FAR on BOTH X and Z (all sides), not just the −Z strip.
            Assert.Greater(mr.bounds.size.x, 300f, $"the sea must extend far on X (all-sides) — X extent {mr.bounds.size.x:F0}u");
            Assert.Greater(mr.bounds.size.z, 300f, $"the sea must extend far on Z (all-sides) — Z extent {mr.bounds.size.z:F0}u");
            // The sea must surround the island on ALL FOUR sides: its bounds reach well past the coast in
            // +X, −X, +Z, −Z (the old strip only covered −Z). Centre at origin.
            Assert.Less(mr.bounds.min.x, -LowPolyZoneGen.IslandShoreR - 20f, "sea must reach past the coast on −X");
            Assert.Greater(mr.bounds.max.x, LowPolyZoneGen.IslandShoreR + 20f, "sea must reach past the coast on +X");
            Assert.Less(mr.bounds.min.z, -LowPolyZoneGen.IslandShoreR - 20f, "sea must reach past the coast on −Z");
            Assert.Greater(mr.bounds.max.z, LowPolyZoneGen.IslandShoreR + 20f, "sea must reach past the coast on +Z");
        }

        [Test]
        public void ShippedScene_SeaCarriesRadialFoamRing_AllTheWayAround()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "Water_Play must exist");
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.vertices; var cols = mesh.colors;
            var foam = LowPolyZoneGen.FoamEdge;
            // Foam verts must appear in MULTIPLE azimuth quadrants (a ring, not a single-side strip line).
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
            Assert.Greater(foamVerts, 0, "the sea must carry a foam ring at the coast");
            Assert.AreEqual(4, quads.Count(q => q),
                "the foam must form a RADIAL RING — foam verts must appear in ALL FOUR azimuth quadrants " +
                "(a round coast all the way around), not just the −Z strip the old design foamed");
        }

        [Test]
        public void ShippedScene_DenseTallJungle_ManyTreesWithTallVariants()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var trees = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "LP_Tree" || mf.gameObject.transform.parent?.name == "LP_Tree")
                .Select(mf => mf.gameObject.transform.parent != null && mf.gameObject.transform.parent.name == "LP_Tree"
                              ? mf.gameObject.transform.parent.gameObject : mf.gameObject)
                .Distinct()
                .ToArray();
            // Count distinct LP_Tree roots.
            var treeRoots = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "LP_Tree").ToArray();
            Assert.Greater(treeRoots.Length, 150,
                $"the island must carry a DENSE jungle (>150 trees) — found {treeRoots.Length}. Sponsor: " +
                "'dense forest/jungle with high trees'.");
            // TALL variants: at least some trees must be tall (canopy high above the ground). Measure the
            // tallest tree's canopy world-Y above its root.
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
                $"the jungle must have TALL trees — tallest canopy top ~{maxCanopyTop:F1}u above ground " +
                "(>6u). A sparse short scatter is the old strip read.");
        }

        [Test]
        public void ShippedScene_MountainsOnSeparateIslands_OffTheMainIsland()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            // Every vista landmass base (LP_Landmass) must sit OFF the main round island: its near-edge
            // (centre distance − dome radius) must clear the main coast (IslandShoreR) with margin. This is
            // the Sponsor's "mountains should sit on other islands" — proven against the BIGGER island.
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
                    $"mountain island '{r.gameObject.name}' near-edge ({nearEdge:F0}u) must clear the main " +
                    $"island coast (IslandShoreR {LowPolyZoneGen.IslandShoreR}u + margin) — the mountains must " +
                    "sit on SEPARATE islands in the sea, OFF the main island (Sponsor requirement).");
            }
            // And no peak may sit ON the main island disc.
            var peaks = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "LP_Mountain").ToArray();
            Assert.Greater(peaks.Length, 6, "the vista must carry several mountain peaks on the separate islands");
            foreach (var p in peaks)
            {
                float d = new Vector2(p.position.x, p.position.z).magnitude;
                Assert.Greater(d, LowPolyZoneGen.IslandShoreR + 15f,
                    $"peak '{p.name}' (dist {d:F0}u) must be OFF the main island (> coast {LowPolyZoneGen.IslandShoreR}u) " +
                    "— mountains live on the distant islands, not the main island");
            }
        }
    }
}
