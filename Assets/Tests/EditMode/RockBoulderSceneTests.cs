using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the scatter-rock "doesn't read as a rock" defect (ticket 86ca8m5zu).
    ///
    /// HISTORY — TWO rejected procedural shapes, both for the SAME root reason:
    ///   v1 (soak 5f7e7ba): FacetedSphere(0.6, subdiv:0, jitter:0.30) — the bare 8-face OCTAHEDRON read
    ///       as an angular dark SPIKE "sticking up... all over."
    ///   v2 (soak 30ef320): FacetedSphere(0.6, subdiv:2, jitter:0.14) — a SMOOTH ball whose welded
    ///       RecalculateNormals AVERAGES every facet into a continuous gradient -> reads as a soft dark
    ///       MOUND, NOT stone. Sponsor: "rocks still doesnt look like rocks and now there is just way too
    ///       few of them, before was a decent amount."
    ///
    /// THE FIX (v3 — the PERCEPT, not a proxy): LowPolyMeshes.FacetedRock — an ANGULAR, FLAT-SHADED
    /// (per-FACE normals, UNWELDED) irregular stone chunk whose facets each catch the key light at a
    /// DIFFERENT value (a per-facet value baked into vertex colour). The hard facet-to-facet value contrast
    /// is the entire "reads as carved stone" signal (board ref inspiration/2026-06-12_21h10_44.png) — and a
    /// smooth-welded sphere destroys it by construction. (Blender-MCP sourcing was the dispatch's PRIMARY
    /// route; the MCP was unreachable, so this is the reworked-angular-procedural FALLBACK.)
    ///
    /// THESE GUARDS ASSERT THE PERCEPT, not a roundness proxy (the unity-conventions §"Guard the PERCEPT"
    /// trap: the v2 guards ENFORCED roundness — the very thing that read as a mound). A wrong-looking smooth
    /// mound must FAIL here:
    ///   - FLAT-SHADED: vertices are unwelded per-face (vertexCount == triangleCount*3) so each facet is a
    ///     distinct flat plane — a welded smooth sphere (verts << tris*3) fails.
    ///   - VALUE CONTRAST: the baked vertex-colour values span a wide range (light tops / dark sides) — a
    ///     single-value mesh (a flat mound) fails.
    ///   - ANGULAR/IRREGULAR silhouette: anisotropic radial spread (not a clean sphere shell).
    ///   - DENSITY restored to the "decent amount" (~22), still CLUSTERED + spawn-clear (not "all over").
    /// </summary>
    public class RockBoulderSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        // ---- MESH-shape guards: the FLAT-SHADED ANGULAR STONE percept (catch a revert to a smooth ball) ----

        [Test]
        public void RockMesh_IsFlatShaded_UnweldedPerFace_NotSmoothSphere()
        {
            // FLAT shading is the load-bearing stone read: every triangle owns its 3 verts + the face normal,
            // so vertexCount == triangleCount*3. A WELDED smooth FacetedSphere (the rejected v2 mound) shares
            // verts across faces -> vertexCount is FAR less than tris*3. This guard fails on a revert to any
            // welded/RecalculateNormals sphere.
            var rock = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 1234);
            int triCount = rock.triangles.Length / 3;
            Assert.AreEqual(triCount * 3, rock.vertexCount,
                $"the rock must be FLAT-SHADED (every face owns its 3 verts -> verts == tris*3 = {triCount * 3}); " +
                $"got {rock.vertexCount} verts for {triCount} tris. A welded smooth sphere (verts << tris*3) is " +
                "the rejected smooth-MOUND regression (86ca8m5zu v2) — flat per-facet planes read as stone.");

            // Sanity: the rejected v2 boulder (welded subdiv-2 sphere) had only 66 verts for 128 tris — prove
            // FacetedRock is NOT that shape.
            var smoothMound = LowPolyMeshes.FacetedSphere(0.6f, 2, jitter: 0.14f, seed: 1234);
            Assert.Less(smoothMound.vertexCount, smoothMound.triangles.Length,
                "sanity: the rejected smooth sphere is welded (verts < tris) — FacetedRock must NOT be welded");
        }

        [Test]
        public void RockMesh_FaceNormalsAreFlat_NeighboursDiffer_NotSmoothGradient()
        {
            // A flat-shaded mesh has the SAME normal on all 3 verts of a face, and ADJACENT faces have
            // DIFFERENT normals (a hard crease, not a smooth gradient). Assert (a) each triple is internally
            // identical (flat face) and (b) the set of distinct face normals is large (many distinct planes,
            // the chunky-stone read) — a smooth sphere would have ~every vertex normal unique & continuous.
            var rock = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 55);
            var nrm = rock.normals;
            int tris = rock.triangles.Length / 3;
            int distinctPlanes = 0;
            var seen = new System.Collections.Generic.List<Vector3>();
            for (int f = 0; f < tris; f++)
            {
                Vector3 n0 = nrm[f * 3], n1 = nrm[f * 3 + 1], n2 = nrm[f * 3 + 2];
                Assert.AreEqual(1f, n0.magnitude, 0.05f, $"face {f} normal must be unit length");
                Assert.Less(Vector3.Distance(n0, n1), 0.01f, $"face {f} must be FLAT (all 3 verts share the face normal)");
                Assert.Less(Vector3.Distance(n0, n2), 0.01f, $"face {f} must be FLAT (all 3 verts share the face normal)");
                if (!seen.Any(s => Vector3.Distance(s, n0) < 0.08f)) { seen.Add(n0); distinctPlanes++; }
            }
            Assert.Greater(distinctPlanes, 12,
                $"the rock must have many DISTINCT facet planes (got {distinctPlanes}) — a chunky faceted stone, " +
                "not a smooth gradient surface (the smooth-mound regression has near-continuous normals)");
        }

        [Test]
        public void RockMesh_HasPerFacetValueContrast_LightTopsDarkSides_ReadsAsStone()
        {
            // The facet-to-facet VALUE contrast is the stone read: top-facing facets are baked light, side/
            // down facets dark, in vertex colour. Assert the colour value SPANS a wide range — a single-value
            // mesh (a flat-tone mound) would have ~zero spread and fail here.
            var rock = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 9);
            var cols = rock.colors;
            Assert.AreEqual(rock.vertexCount, cols.Length,
                "every rock vertex must carry a vertex COLOUR (the per-facet value the stone shader multiplies)");
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var c in cols) { float v = c.r; lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
            // The baked value floor was deliberately LIFTED to 0.80 (so side facets never crush to BLACK — the
            // first capture's shard bug), which narrows the baked range to ~0.18; the bulk of the stone's
            // facet-to-facet contrast now comes from the FLAT-SHADING LIGHTING (each facet's own N·L), which
            // the captures confirm reads strongly. So this guard only needs to prove the baked value is NOT
            // flat (light tops vs darker sides), not a specific magnitude.
            Assert.Greater(hi - lo, 0.12f,
                $"the rock must have FACET VALUE CONTRAST (range {lo:F2}..{hi:F2}) — light tops, darker sides; a " +
                "flat single-value mesh (range ~0) reads as a smooth MOUND, not carved stone (86ca8m5zu)");
            Assert.LessOrEqual(hi, 1.05f, "facet value must stay sane (no HDR blow-out before the warm-grey tint)");
        }

        [Test]
        public void RockMesh_IsAngularIrregular_NotAcleanSphereShell()
        {
            // A real rock is an irregular angular lump: the radial distances of its verts SPREAD (anisotropic
            // displacement + carved notches). A clean sphere shell has near-constant radius. Assert a wide
            // radial spread so a revert to a tidy sphere fails.
            var rock = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 77);
            var verts = rock.vertices;
            float maxR = 0f, minR = float.MaxValue;
            foreach (var v in verts)
            {
                float r = v.magnitude;
                maxR = Mathf.Max(maxR, r);
                if (r > 1e-4f) minR = Mathf.Min(minR, r);
            }
            Assert.Greater(maxR - minR, 0.16f,
                $"the rock silhouette must be IRREGULAR/angular (radial spread {minR:F2}..{maxR:F2}) — a clean " +
                "sphere shell (near-constant radius) is the smooth-ball regression");
        }

        [Test]
        public void RockMesh_IsChunky_TallerThanAflatMound()
        {
            // The Sponsor's mound complaint includes a too-FLAT read. Assert the rock's height is a real
            // fraction of its width (chunky), not pancaked. FacetedRock keeps yScale >= 0.70 of the lump.
            var rock = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 3);
            var b = rock.bounds.size;
            float widthXZ = Mathf.Max(b.x, b.z);
            Assert.Greater(b.y / Mathf.Max(widthXZ, 1e-4f), 0.45f,
                $"the rock must be CHUNKY (height {b.y:F2} vs width {widthXZ:F2}) — taller than a flat mound");
        }

        [Test]
        public void RockMesh_IsDeterministic_StableAcrossRebuild()
        {
            // The baked scene must be reproducible on rebase-regenerate (binary-scene regenerate-on-rebase).
            var a = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 4242);
            var b = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 4242);
            Assert.AreEqual(a.vertexCount, b.vertexCount, "same seed -> same vertex count (deterministic bake)");
            Assert.AreEqual(a.vertices[0], b.vertices[0], "same seed -> identical geometry");
        }

        // ---- SCENE-presence guards: the rocks the exe actually ships ----

        private static MeshFilter[] FindRockMeshes()
        {
            return Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "RockMesh" && mf.sharedMesh != null)
                .ToArray();
        }

        [Test]
        public void BootScene_ScattersRocks_DecentAmount_NotTooFew_NotAllOver()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var rocks = FindRockMeshes();

            // BIG ROUND ISLAND (86ca9a7qn): the island is ~50× the old strip's area, so the rock count
            // scales up to ~60 clustered outcrops on the hills (still a SPARSE density per unit area — rock
            // piles, not a uniform speckle). Assert a floor (>=30, so a revert to the old ~22 strip count
            // reads too-sparse on the big island) AND a ceiling (<=90, so a uniform all-over speckle fails).
            Assert.GreaterOrEqual(rocks.Length, 30,
                $"rocks must be a DECENT AMOUNT on the big island (found {rocks.Length}) — the bigger disc needs " +
                "more outcrops than the old ~22 strip count to not read empty.");
            Assert.LessOrEqual(rocks.Length, 90,
                $"rocks must NOT be a uniform 'all over' speckle (found {rocks.Length}); ~60 clustered outcrops " +
                "across the big island is the target, not hundreds sprinkled everywhere");
        }

        [Test]
        public void BootScene_Rocks_AreFlatShadedStone_NotSmoothMounds()
        {
            // Every shipped rock mesh must be the FLAT-SHADED FacetedRock (verts == tris*3 + carries vertex
            // colours), not a welded smooth sphere (the rejected mound).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var rocks = FindRockMeshes();
            Assert.Greater(rocks.Length, 0, "must have rocks to check");
            foreach (var mf in rocks)
            {
                var m = mf.sharedMesh;
                int tris = m.triangles.Length / 3;
                Assert.AreEqual(tris * 3, m.vertexCount,
                    $"shipped rock '{mf.name}' must be FLAT-SHADED (verts == tris*3); got {m.vertexCount}/{tris} — " +
                    "a welded smooth sphere is the rejected MOUND regression (86ca8m5zu v2)");
                Assert.AreEqual(m.vertexCount, m.colors.Length,
                    $"shipped rock '{mf.name}' must carry per-facet vertex COLOURS (the stone value contrast)");
            }
        }

        [Test]
        public void BootScene_Rocks_AreWarmLightStone_NotDarkSilhouette()
        {
            // The rock TINT must be the LIFTED warm-light stone (so the facets catch the key under the warm fog
            // instead of crushing to a dark blob). The FacetedRock vertex value (0.62..1.0) multiplies onto the
            // _Tint in the LowPolyVertexColor shader, so the readable albedo is _Tint * value — assert the _Tint
            // is warm (R>=B) and mid-LIGHT (R>0.5), sub-1.0.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var rocks = FindRockMeshes();
            Assert.Greater(rocks.Length, 0, "must have rocks to check");
            foreach (var mf in rocks)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr, $"{mf.name} must have a MeshRenderer");
                var mat = mr.sharedMaterial;
                Assert.IsNotNull(mat, $"{mf.name} must have a material");
                // The rock uses the vertex-colour shader (_Tint), or the URP/Lit fallback (_BaseColor).
                Color c = mat.HasProperty("_Tint") ? mat.GetColor("_Tint")
                        : mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.magenta;

                Assert.Less(c.r, 1f, $"{mf.name} red tint must be sub-1.0 (HDR-clamp-safe)");
                Assert.Less(c.g, 1f, $"{mf.name} green tint must be sub-1.0");
                Assert.Less(c.b, 1f, $"{mf.name} blue tint must be sub-1.0");
                Assert.GreaterOrEqual(c.r, c.b,
                    $"{mf.name} tint ({c.r:F2},{c.g:F2},{c.b:F2}) must be WARM-leaning stone (R >= B)");
                Assert.Greater(c.r, 0.5f,
                    $"{mf.name} tint red {c.r:F2} is too DARK — the rock must be lifted warm-light stone so it " +
                    "doesn't silhouette dark under the Zone-D fog (86ca8m5zu)");
            }
        }

        [Test]
        public void BootScene_Rocks_AreClusteredInOutcrops_SpawnClear()
        {
            // The fix clusters rocks into outcrops AND keeps the spawn (0,0,6) clear. Assert NO rock sits in
            // the spawn-exclusion box so the spawn beach foreground reads clean.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var rocks = FindRockMeshes();
            Assert.Greater(rocks.Length, 0, "must have rocks to check");
            foreach (var mf in rocks)
            {
                Vector3 p = mf.transform.parent != null ? mf.transform.parent.position : mf.transform.position;
                bool inSpawnBox = Mathf.Abs(p.x) < 6f && p.z < 10f && p.z > 2f;
                Assert.IsFalse(inSpawnBox,
                    $"rock at ({p.x:F1},{p.z:F1}) sits in the spawn-clear zone — the spawn (0,0,6) foreground " +
                    "must stay open (the clustered + spawn-excluded fix)");
            }
        }
    }
}
