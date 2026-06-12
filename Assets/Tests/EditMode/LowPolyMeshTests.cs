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
    }
}
