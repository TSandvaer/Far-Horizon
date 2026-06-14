using NUnit.Framework;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the grass-clump "dark angular shard" defect, ported from the eval spike
    /// (EmbergraveUnitySlice, Assets/Tests/EditMode/LowPolyMeshTests.cs, iter-8). Sponsor soak iter7:
    /// "what are the things sticking up from the ground?" — the scatter grass clumps shaded near-black
    /// and read as shards, not grass.
    ///
    /// ROOT CAUSE (verified by reading the iter-5 geometry, not hypothesized): the old GrassClump drew
    /// each blade as ONE triangle TWICE on the SAME 3 verts with OPPOSITE winding ("front + back face").
    /// On the welded mesh, RecalculateNormals averaged the two coincident opposite-facing face normals
    /// at each shared vertex -> the averaged vertex normal was ~ZERO -> URP/Lit gave the blade ~no
    /// diffuse light -> near-black "shard" read from every angle.
    ///
    /// The fix builds the back face from its OWN duplicated verts with explicit UP-BIASED normals (a
    /// real two-sided blade lit as overhead foliage) and sets normals directly (no RecalculateNormals
    /// to re-cancel them). These guards catch a regression back to the cancelling-normal shape: they
    /// assert EVERY blade vertex has a well-formed (near-unit-length, non-zero) normal. A
    /// degenerate/zero normal — the exact dark-shard signature — fails here in headless CI before it
    /// ships as a visible shard.
    /// </summary>
    public class LowPolyMeshTests
    {
        [Test]
        public void GrassClump_HasNoDegenerateNormals_NotDarkShards()
        {
            var mesh = LowPolyMeshes.GrassClump(0.55f, 7, seed: 4242);
            var normals = mesh.normals;

            Assert.Greater(normals.Length, 0, "grass mesh must have per-vertex normals");
            Assert.AreEqual(mesh.vertexCount, normals.Length, "every vertex must carry a normal");

            // The dark-shard bug produced ~zero-length (cancelled-average) normals. A correctly-lit
            // blade has a unit-length normal. Assert every normal is well-formed.
            for (int i = 0; i < normals.Length; i++)
            {
                float len = normals[i].magnitude;
                Assert.Greater(len, 0.5f,
                    $"vertex {i} normal is near-zero ({len:F3}) — the dark-shard cancellation regression " +
                    "(two coincident opposite-wound faces averaging to ~0). Blades must keep distinct " +
                    "front/back verts with explicit up-biased normals.");
                Assert.AreEqual(1f, len, 0.05f, $"vertex {i} normal must be ~unit length (got {len:F3})");
            }
        }

        [Test]
        public void GrassClump_IsTwoSided_WithDistinctFrontAndBackVerts()
        {
            // A genuinely two-sided blade has DUPLICATED verts for the back face (not shared with the
            // front), so the vertex count is 6 per blade (3 front + 3 back). The old buggy shape reused
            // the same 3 verts for both faces (3 per blade) — the cancellation cause. Assert the 6/blade
            // shape so a revert to shared-vert "back face on the same triangle" fails here.
            int blades = 7;
            var mesh = LowPolyMeshes.GrassClump(0.55f, blades, seed: 99);
            Assert.AreEqual(blades * 6, mesh.vertexCount,
                "each blade must have its OWN front + back verts (6/blade) so normals don't cancel — " +
                "a shared-vert blade (3/blade) is the dark-shard regression");
            // Two triangles per blade (front + back), 3 indices each.
            Assert.AreEqual(blades * 2 * 3, mesh.triangles.Length,
                "each blade must contribute a front + a back triangle");
        }

        [Test]
        public void GrassClump_BladesPointMostlyUpward_NotFlatShards()
        {
            // A grass tuft's normals should have a meaningful upward component (the blade catches the
            // warm key from above on BOTH faces — the up-bias fix). A flat shard lying over, or a
            // down-pointing back face, would not. Sample ALL normals and confirm the average points up.
            var mesh = LowPolyMeshes.GrassClump(0.55f, 9, seed: 7);
            var normals = mesh.normals;
            float upSum = 0f; int count = 0;
            for (int i = 0; i < normals.Length; i++) { upSum += normals[i].y; count++; }
            Assert.Greater(count, 0, "must have normals to sample");
            Assert.Greater(upSum / count, 0.5f,
                "blade normals must average strongly upward (both faces up-biased so neither shades " +
                "dark) — a low average means a face points away from the overhead key (the shard read)");
        }

        // ---- Blob-canopy guards (board v2 tree language, ticket 86ca8ce7j) ----

        [Test]
        public void BlobCanopy_IsMultiBlobCluster_NotSingleDome()
        {
            // The board's blob canopy is a CLUSTER of several spheroids, NOT one smooth dome
            // (inspiration/21h11_03). A single FacetedSphere(radius,1) has 18 verts (subdiv-1
            // octahedron). A 5-blob cluster has far more — assert the vertex count is well above a
            // single dome so a regression back to one-sphere canopy fails here.
            var single = LowPolyMeshes.FacetedSphere(1.15f, 1, 0.18f, 1);
            var canopy = LowPolyMeshes.BlobCanopy(1.15f, 5, Color.green, Color.green, Color.green, 1);
            Assert.Greater(canopy.vertexCount, single.vertexCount * 2,
                "a blob canopy must be a multi-blob CLUSTER (far more verts than a single dome) — " +
                "a single-sphere canopy is the pre-board regression");
        }

        [Test]
        public void BlobCanopy_CarriesMultiValueGreens_NotOneFlatGreen()
        {
            // The "3-4 green values per tree" rule (style-guide §4) is baked into per-vertex COLOR.
            // Pass three DISTINCT greens; the mesh must carry more than one distinct vertex colour
            // (the multi-value clustering that reads as foliage, not a green ball). A regression that
            // flattens the canopy to one green would have a single distinct colour here.
            var body = new Color(0.30f, 0.58f, 0.24f);
            var top = new Color(0.48f, 0.74f, 0.34f);
            var shadow = new Color(0.18f, 0.40f, 0.17f);
            var canopy = LowPolyMeshes.BlobCanopy(1.15f, 5, body, top, shadow, 42);
            var cols = canopy.colors;
            Assert.AreEqual(canopy.vertexCount, cols.Length, "every canopy vertex must carry a colour");
            var distinct = new System.Collections.Generic.HashSet<Color>(cols);
            Assert.Greater(distinct.Count, 1,
                "the canopy must carry MULTIPLE green values (the multi-value blob clustering) — a " +
                "single distinct colour is the flat-green-ball regression");
            // Every colour must be green-dominant (G > R and G > B) — never a magenta/error tint.
            foreach (var c in distinct)
                Assert.IsTrue(c.g >= c.r && c.g >= c.b,
                    $"canopy colour ({c.r:F2},{c.g:F2},{c.b:F2}) must be green-dominant (G largest)");
        }

        [Test]
        public void BlobCanopy_IsSolidVolume_AllNormalsUnitLength_NoThinFoliageShards()
        {
            // The blob canopy is a SOLID volume (welded spheroids), which is the WHOLE point of the
            // blob language — it sidesteps the iter-8 thin-foliage near-black-shard normal trap
            // (unity-conventions.md). Assert every normal is well-formed unit length (no degenerate
            // cancellation) — a regression to thin double-sided cards would fail here.
            var canopy = LowPolyMeshes.BlobCanopy(1.15f, 5, Color.green, Color.green, Color.green, 7);
            var normals = canopy.normals;
            Assert.AreEqual(canopy.vertexCount, normals.Length, "every canopy vertex must carry a normal");
            for (int i = 0; i < normals.Length; i++)
                Assert.AreEqual(1f, normals[i].magnitude, 0.05f,
                    $"blob-canopy vertex {i} normal must be ~unit length (welded solid volume, smooth " +
                    "shading) — a near-zero normal is the thin-foliage dark-shard signature");
        }

        [Test]
        public void BlobCanopy_IsDeterministic_SameSeedSameMesh()
        {
            // Deterministic from seed so the baked scene is reproducible on rebase-regenerate
            // (binary-scene regenerate-on-rebase relies on stable geometry).
            var a = LowPolyMeshes.BlobCanopy(1.15f, 5, Color.green, Color.green, Color.green, 123);
            var b = LowPolyMeshes.BlobCanopy(1.15f, 5, Color.green, Color.green, Color.green, 123);
            Assert.AreEqual(a.vertexCount, b.vertexCount, "same seed must produce the same vertex count");
        }

        // ---- Smooth-shaded primitive guards (welded -> averaged normals, no degenerates) ----

        [Test]
        public void FacetedSphere_IsWeldedSmooth_AllNormalsUnitLength()
        {
            var mesh = LowPolyMeshes.FacetedSphere(0.6f, 1, jitter: 0.18f, seed: 5);
            var normals = mesh.normals;
            Assert.AreEqual(mesh.vertexCount, normals.Length, "every vertex must carry a normal");
            for (int i = 0; i < normals.Length; i++)
                Assert.AreEqual(1f, normals[i].magnitude, 0.05f,
                    $"faceted-sphere vertex {i} normal must be ~unit length (welded + RecalculateNormals " +
                    "= smooth shading); a near-zero normal would shade the canopy/rock dark");
        }

        [Test]
        public void TaperedCylinder_HasCleanNormals()
        {
            var mesh = LowPolyMeshes.TaperedCylinder(0.18f, 0.12f, 1.6f, 6);
            var normals = mesh.normals;
            Assert.Greater(mesh.vertexCount, 0, "trunk mesh must have verts");
            for (int i = 0; i < normals.Length; i++)
                Assert.AreEqual(1f, normals[i].magnitude, 0.05f,
                    $"trunk vertex {i} normal must be ~unit length");
        }

        // ---- FacetedRock: FLAT-shaded angular stone (86ca8m5zu v2 — the redo) ----

        [Test]
        public void FacetedRock_IsFlatShaded_UnweldedPerFace()
        {
            // Unlike FacetedSphere (welded, smooth), FacetedRock emits every face with its OWN 3 verts so
            // each facet is a distinct flat plane (the carved-stone read). verts == tris*3.
            var rock = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 7);
            int tris = rock.triangles.Length / 3;
            Assert.AreEqual(tris * 3, rock.vertexCount, "FacetedRock must be flat-shaded (verts == tris*3)");
            Assert.Greater(tris, 20, "FacetedRock must have a chunky facet count (subdiv-1 base ~32 faces)");
        }

        [Test]
        public void FacetedRock_CarriesPerFacetValueColour()
        {
            // The per-facet value (light tops / dark sides) is baked into vertex colour for the stone read.
            var rock = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 8);
            var cols = rock.colors;
            Assert.AreEqual(rock.vertexCount, cols.Length, "every vert carries the facet value colour");
            float lo = 1f, hi = 0f;
            foreach (var c in cols) { lo = Mathf.Min(lo, c.r); hi = Mathf.Max(hi, c.r); }
            Assert.Greater(hi - lo, 0.2f, "facet value must span light..dark (the stone contrast)");
        }

        [Test]
        public void FacetedRock_NormalsAreUnitLength_NoDegenerates()
        {
            var rock = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 11);
            var n = rock.normals;
            for (int i = 0; i < n.Length; i++)
                Assert.AreEqual(1f, n[i].magnitude, 0.05f,
                    $"FacetedRock normal {i} must be ~unit length (a degenerate normal shades the stone dark)");
        }
    }
}
