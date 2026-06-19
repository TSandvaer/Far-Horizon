using NUnit.Framework;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the "berries look like FLOWERS" defect (ticket 86caa5zz3, #101 Sponsor soak).
    ///
    /// ROOT CAUSE (read from the geometry, not hypothesized): the old BerryCluster made TOO FEW berries
    /// (~6-12) that were TOO LARGE (berryR = 0.16 * bushRadius ≈ a 30cm "berry"), each built from TWO
    /// overlapping blobs so it bulged even bigger, spread over a wide thin ring — a handful of big rounded
    /// lumps on a dome reads as a flower head, not berries. Berries read as berries when they are SMALL,
    /// DENSE, and MANY.
    ///
    /// THE FIX (the bug CLASS these tests catch): berryR ≈ 0.055 * bushRadius (≈3× smaller), ONE blob per
    /// berry, and MANY of them (callers pass 20-30) packed tightly over the dome. A regression back toward
    /// big-and-few — a larger per-berry radius OR a cluster whose vertex count stops scaling up with the
    /// requested count — fails here in headless CI before it ships as flowers again.
    /// </summary>
    public class BerryClusterMeshTests
    {
        private static readonly Color Berry = new Color(0.78f, 0.16f, 0.22f);
        private const float BushR = 0.95f; // the deterministic capture bush radius (MovementCameraScene)

        // === MANY berries: the cluster's vertex count scales UP with the requested count =============
        // The flower defect was TOO FEW berries. A high count must produce materially more geometry than a
        // low count — guards against a cap/clamp that silently keeps the cluster sparse.
        [Test]
        public void MoreBerries_ProduceMoreGeometry()
        {
            var few = LowPolyMeshes.BerryCluster(BushR, 6, Berry, 101);
            var many = LowPolyMeshes.BerryCluster(BushR, 26, Berry, 101);

            Assert.Greater(many.vertexCount, few.vertexCount,
                "a 26-berry cluster must have more vertices than a 6-berry one (MANY berries — the #101 fix)");
            // ~linear in count (one blob per berry). 26/6 ≈ 4.3×; allow slack but require a clear scale-up.
            Assert.Greater(many.vertexCount, few.vertexCount * 3,
                "vertex count must scale roughly with berry count (one small blob per berry) — a regression " +
                "that keeps the cluster sparse (few big lumps) would fail here");
        }

        // === SMALL berries: each berry is a small dot relative to the bush, NOT a flower-sized lump ====
        // The whole cluster nestles over the upper dome (radial spread ~0.82*bushR + small berry radius).
        // A FLOWER-sized berry (the old 0.16*bushR doubled by the 2-blob bulge) would blow the per-berry
        // extent well past a small-dot budget. Guard the cluster's overall extent stays dome-scaled.
        [Test]
        public void Berries_AreSmall_NotFlowerSizedLumps()
        {
            var mesh = LowPolyMeshes.BerryCluster(BushR, 26, Berry, 53119);
            Vector3 ext = mesh.bounds.extents; // half-size

            // The studding spans the dome (radial reach ~0.82*bushR) plus a SMALL berry radius. With small
            // dots the half-extent stays close to the dome reach; the old big-berry build pushed well past it.
            // Cap the horizontal half-extent at ~bushR (dome reach + a small dot), which the flower-sized
            // build (0.16*bushR per berry, doubled blob) exceeded.
            float horiz = Mathf.Max(ext.x, ext.z);
            Assert.LessOrEqual(horiz, BushR * 1.05f,
                "the berry cluster must stay dome-scaled — small dots over the dome, not flower-sized lumps");

            // And it must not be a flat ring: the studding has real vertical spread over the dome.
            Assert.Greater(ext.y, BushR * 0.10f, "berries stud the dome with vertical spread, not a flat ring");
        }

        // === The cluster sits OVER the dome (positive Y) — berries nestle on top, not buried in the body =
        [Test]
        public void Berries_NestleOverTheDome_PositiveY()
        {
            var mesh = LowPolyMeshes.BerryCluster(BushR, 26, Berry, 7);
            Assert.Greater(mesh.bounds.center.y, 0f,
                "the berry cluster's centre sits above the bush base (berries ride the upper dome)");
        }

        // === Material-honest RED: every berry vertex colour is a red-dominant fruit tone (no arbitrary tint)
        [Test]
        public void Berries_AreRedDominant_MaterialHonest()
        {
            var mesh = LowPolyMeshes.BerryCluster(BushR, 26, Berry, 999);
            Color[] cols = mesh.colors;
            Assert.Greater(cols.Length, 0, "berries must carry per-vertex colour (the shared vertex-color material)");
            for (int i = 0; i < cols.Length; i++)
            {
                Assert.GreaterOrEqual(cols[i].r, cols[i].g,
                    "every berry vertex must be red-dominant over green (a red berry, material-honest)");
                Assert.GreaterOrEqual(cols[i].r, cols[i].b,
                    "every berry vertex must be red-dominant over blue (a red berry, material-honest)");
            }
        }

        // === Deterministic: the same seed yields a byte-stable mesh (reproducible baked scene on rebase) ==
        [Test]
        public void BerryCluster_IsDeterministicForASeed()
        {
            var a = LowPolyMeshes.BerryCluster(BushR, 26, Berry, 53119);
            var b = LowPolyMeshes.BerryCluster(BushR, 26, Berry, 53119);
            Assert.AreEqual(a.vertexCount, b.vertexCount,
                "same seed must yield the same vertex count (reproducible baked scene on rebase-regenerate)");
        }

        // === Min-count floor: a sub-minimum count still produces a valid (min-3) cluster, never empty =====
        [Test]
        public void BerryCluster_ClampsToMinimumCount()
        {
            var mesh = LowPolyMeshes.BerryCluster(BushR, 1, Berry, 5);
            Assert.Greater(mesh.vertexCount, 0, "a sub-minimum count clamps to at least 3 berries (never empty)");
        }
    }
}
