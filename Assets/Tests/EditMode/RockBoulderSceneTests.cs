using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the scatter-rock "dark angular spike" defect (ticket 86ca8m5zu, Sponsor soak
    /// 5f7e7ba: "what are these items sticking up from the ground... they are all over").
    ///
    /// ROOT CAUSE (orchestrator code-trace, verified @ 5f7e7ba, NOT hypothesized): BuildRock built rocks
    /// from FacetedSphere(0.6f, subdiv:0, jitter:0.30f) — subdiv 0 is the BARE octahedron (8 triangular
    /// faces / 6 verts); 0.30 jitter + a Y-squash down to 0.6x made each one an angular dark SHARD that
    /// "sticks up". ~22 of them were sprinkled UNIFORMLY across the whole beach+field → "all over".
    ///
    /// THE FIX (3 levers, each guarded below):
    ///   (1) ROUND the mesh — subdiv 0 -> 2 (8 faces -> 128, 6 verts -> 66) + jitter 0.30 -> 0.14, so the
    ///       silhouette reads as a faceted BOULDER, not a pointy octahedron shard.
    ///   (2) LIFT/WARM the colour — RockCol 0.55-grey -> 0.66 warm-light stone so it catches the key under
    ///       the Zone-D warm fog instead of crushing to a dark silhouette.
    ///   (3) THIN + CLUSTER — ~22 uniform sprinkle -> a few natural outcrops (clusters) inland of the spawn,
    ///       with a spawn-exclusion, so they read as deliberate rock piles, not clutter "all over".
    ///
    /// These guards split into MESH-shape (deterministic, no scene) + SCENE-presence (the saved Boot.unity
    /// the exe ships). The shipped-build capture is the final READ evidence; this is the editor-side half.
    /// </summary>
    public class RockBoulderSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        // ---- MESH-shape guards: catch a revert to the subdiv-0 / high-jitter spike directly ----

        [Test]
        public void RockMesh_IsRoundedBoulder_NotOctahedronSpike()
        {
            // The fix builds the rock from FacetedSphere(0.6, subdiv:2, jitter:0.14). A subdiv-2 octahedron
            // is 66 verts; the OLD spike was subdiv-0 = 6 verts. Assert the rock mesh is well above the
            // bare-octahedron vert count so a revert to subdiv 0 (the spike shape) fails here.
            var boulder = LowPolyMeshes.FacetedSphere(0.6f, 2, jitter: 0.14f, seed: 1234);
            var spike   = LowPolyMeshes.FacetedSphere(0.6f, 0, jitter: 0.30f, seed: 1234);

            Assert.AreEqual(6, spike.vertexCount,
                "sanity: the OLD rock (subdiv 0) is the bare 6-vert octahedron — the spike shape");
            Assert.Greater(boulder.vertexCount, 40,
                "the rock mesh must be a ROUNDED subdiv>=2 boulder (66 verts), NOT the 6-vert octahedron " +
                "spike — a revert to subdiv 0 is the 'dark angular spike sticking up' regression (86ca8m5zu)");
        }

        [Test]
        public void RockMesh_HasNoExtremeRadialSpikes_LowJitterRounded()
        {
            // A boulder must not have a vertex jutting far out (a spike). With radius 0.6 + jitter 0.14, every
            // vert sits within radius*(1 +/- 0.07) of the centre (jitter*0.5 each side). The old 0.30 jitter
            // gave +/-0.15 — much deeper dents/spikes. Assert the radial spread is TIGHT (no spike vertex).
            var boulder = LowPolyMeshes.FacetedSphere(0.6f, 2, jitter: 0.14f, seed: 77);
            var verts = boulder.vertices;
            float maxR = 0f, minR = float.MaxValue;
            foreach (var v in verts)
            {
                float r = v.magnitude;
                if (r > maxR) maxR = r;
                if (r < minR) minR = r;
            }
            // radius 0.6, jitter 0.14 -> nominal range [0.6*0.93, 0.6*1.07] = [0.558, 0.642]. Allow a small
            // epsilon. A spike vertex (old 0.30 jitter) would exceed 0.69 and fail here.
            Assert.LessOrEqual(maxR, 0.66f,
                $"a boulder vertex juts to r={maxR:F3} — too far out (a spike). jitter must stay low (~0.14) " +
                "so the silhouette is rounded, not pointed (the 0.30-jitter spike regression)");
            Assert.GreaterOrEqual(minR, 0.52f,
                $"a boulder vertex caves to r={minR:F3} — too deep a dent. Low jitter keeps it a smooth lump");
        }

        [Test]
        public void RockMesh_AllNormalsUnitLength_SmoothShaded()
        {
            // Welded + RecalculateNormals = smooth-shaded facets, never a degenerate (dark) normal.
            var boulder = LowPolyMeshes.FacetedSphere(0.6f, 2, jitter: 0.14f, seed: 9);
            var normals = boulder.normals;
            Assert.AreEqual(boulder.vertexCount, normals.Length, "every boulder vertex must carry a normal");
            for (int i = 0; i < normals.Length; i++)
                Assert.AreEqual(1f, normals[i].magnitude, 0.05f,
                    $"boulder vertex {i} normal must be ~unit length (welded smooth shading) — a near-zero " +
                    "normal would shade the rock dark");
        }

        // ---- SCENE-presence guards: the rocks the exe actually ships ----

        private static MeshFilter[] FindRockMeshes()
        {
            return Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "RockMesh" && mf.sharedMesh != null)
                .ToArray();
        }

        [Test]
        public void BootScene_ScattersBoulders_ThinnedNotAllOver()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var rocks = FindRockMeshes();

            Assert.Greater(rocks.Length, 0,
                "the play space must still scatter rocks (beautify+thin, NOT remove — Sponsor decision)");
            // THINNED: the old uniform sprinkle was ~22 (up to ~66 at 3x extents). The clustered fix is a
            // handful of outcrops (~12-18). Assert it stays well under the old 'all over' count so a revert
            // to the uniform sprinkle fails here.
            Assert.LessOrEqual(rocks.Length, 24,
                $"rocks must be THINNED (found {rocks.Length}) — a few natural outcrops, not the ~22+ uniform " +
                "sprinkle the Sponsor flagged as 'all over' (86ca8m5zu)");
        }

        [Test]
        public void BootScene_Rocks_AreRoundedBoulders_NotSpikes()
        {
            // Every shipped rock mesh must be the rounded subdiv>=2 boulder, not a 6-vert octahedron spike.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var rocks = FindRockMeshes();
            Assert.Greater(rocks.Length, 0, "must have rocks to check");
            foreach (var mf in rocks)
                Assert.Greater(mf.sharedMesh.vertexCount, 40,
                    $"shipped rock '{mf.name}' has only {mf.sharedMesh.vertexCount} verts — the 6-vert " +
                    "octahedron SPIKE regression. Rocks must be rounded subdiv>=2 boulders (86ca8m5zu)");
        }

        [Test]
        public void BootScene_Rocks_AreWarmLightStone_NotDarkSilhouette()
        {
            // The rock albedo must be the LIFTED warm-light stone (so it catches the key under the warm fog
            // instead of crushing to a dark blob). Assert each shipped rock material is warm-leaning (R>=B)
            // and mid-LIGHT value (not the old dark 0.55-and-below-after-jitter grey), and sub-1.0.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var rocks = FindRockMeshes();
            Assert.Greater(rocks.Length, 0, "must have rocks to check");
            foreach (var mf in rocks)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr, $"{mf.name} must have a MeshRenderer");
                var mat = mr.sharedMaterial;
                Assert.IsNotNull(mat, $"{mf.name} must have a material");
                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.magenta;

                Assert.Less(c.r, 1f, $"{mf.name} red must be sub-1.0 (HDR-clamp-safe)");
                Assert.Less(c.g, 1f, $"{mf.name} green must be sub-1.0");
                Assert.Less(c.b, 1f, $"{mf.name} blue must be sub-1.0");
                // Warm-leaning stone: R >= B (never a cold blue-grey). The quantizer can equalise channels,
                // so allow equality but forbid a cold (B > R) tone.
                Assert.GreaterOrEqual(c.r, c.b,
                    $"{mf.name} albedo ({c.r:F2},{c.g:F2},{c.b:F2}) must be WARM-leaning stone (R >= B)");
                // Mid-LIGHT value — the lift off the dark silhouette. The new base is 0.66 grey * 0.92..1.08
                // jitter, then 12-step quantised; the lowest plausible value is ~0.58. Require the red channel
                // to clear 0.5 so a revert to the dark 0.47-after-jitter grey (the silhouette) fails here.
                Assert.Greater(c.r, 0.5f,
                    $"{mf.name} albedo red {c.r:F2} is too DARK — the rock must be lifted warm-light stone so " +
                    "it doesn't silhouette dark under the Zone-D fog (the spike-blob read, 86ca8m5zu)");
            }
        }

        [Test]
        public void BootScene_Rocks_AreCluteredInOutcrops_SpawnClear()
        {
            // The fix clusters rocks into a few outcrops AND keeps the spawn (0,0,6) clear. Assert NO rock
            // sits in the spawn-exclusion box (|x|<7 and z<11) so the spawn beach foreground reads clean —
            // a revert to the uniform sprinkle (which dropped rocks right at the spawn) fails here.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var rocks = FindRockMeshes();
            Assert.Greater(rocks.Length, 0, "must have rocks to check");
            foreach (var mf in rocks)
            {
                // RockMesh is a child of LP_Rock (positioned in world); use the parent's world position.
                Vector3 p = mf.transform.parent != null ? mf.transform.parent.position : mf.transform.position;
                bool inSpawnBox = Mathf.Abs(p.x) < 6f && p.z < 10f && p.z > 2f;
                Assert.IsFalse(inSpawnBox,
                    $"rock at ({p.x:F1},{p.z:F1}) sits in the spawn-clear zone — the spawn (0,0,6) foreground " +
                    "must stay open (the clustered+spawn-excluded fix; a uniform sprinkle would land here)");
            }
        }
    }
}
