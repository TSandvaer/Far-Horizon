using NUnit.Framework;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Guards on the PROTOTYPE iron-ingot bar mesh (ticket 86camyvwn, IconBaker prototype).
    ///
    /// These are the code form of the lowpoly-quality.md §0 anchor + silhouette gate: the real-world anchor is
    /// "an iron ingot is a small CAST BAR that rests FLAT on its WIDE base, with a smaller flat top and four
    /// gently sloping sides — low and long, not a cube or a spike," and the side profile must read as a
    /// trapezoid WIDER AT THE BOTTOM. Each assert below can FAIL for a real regression (they are not
    /// tautologies restating an initializer — unity-conventions.md §Editor-vs-runtime "tautological assert"):
    /// invert the ring order, swap base/top extents, lift the base off y=0, or regress the winding, and one of
    /// these goes red.
    ///
    /// Pure mesh math — no scene, no bootstrap, no build.
    /// </summary>
    public class IconBakerProtoTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void IngotBar_RestsOnItsBase_LowestPointIsY0()
        {
            var mesh = IconBakerProto.IngotBarMesh();
            try
            {
                float minY = float.MaxValue;
                foreach (var v in mesh.vertices) if (v.y < minY) minY = v.y;
                Assert.AreEqual(0f, minY, Eps,
                    "the ingot must sit ON the ground plane (base at y=0), not float or sink");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void IngotBar_SideProfileIsATrapezoid_WiderAtTheBottom()
        {
            var mesh = IconBakerProto.IngotBarMesh();
            try
            {
                Bounds b = mesh.bounds;
                float baseWidth = SpanXAtY(mesh, b.min.y);
                float topWidth = SpanXAtY(mesh, b.max.y);
                Assert.Greater(baseWidth, topWidth + 0.02f,
                    "the base must be measurably WIDER than the top — an upside-down wedge (or a straight " +
                    "box) contradicts the cast-bar anchor. base=" + baseWidth + " top=" + topWidth);
                Assert.Greater(topWidth, 0.05f,
                    "the top must be a real flat plateau, not a knife edge/spike");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void IngotBar_ReadsAsABar_LowAndLong_NotACube()
        {
            var mesh = IconBakerProto.IngotBarMesh();
            try
            {
                Vector3 size = mesh.bounds.size;
                Assert.Greater(size.x / size.y, 2.0f,
                    "length:height must read as a BAR (>2:1), not a cube. size=" + size);
                Assert.Greater(size.x / size.z, 1.4f,
                    "the bar must be longer than it is deep. size=" + size);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void IngotBar_EveryFaceWindsOutward_NotCulledByCullBack()
        {
            // unity-conventions.md §Low-poly mesh patterns: an inward-wound flat-shaded face is silently culled
            // by URP `Cull Back` (the −Z sea grid + FacetedRock bugs). Guard the WINDING, not the normal — the
            // normal is a proxy a culled mesh can satisfy.
            var mesh = IconBakerProto.IngotBarMesh();
            try
            {
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                Vector3 centre = Vector3.zero;
                for (int i = 0; i < v.Length; i++) centre += v[i];
                centre /= Mathf.Max(1, v.Length);

                int inward = 0;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    Vector3 fn = Vector3.Cross(b - a, c - a).normalized;
                    Vector3 fc = (a + b + c) / 3f;
                    if (Vector3.Dot(fn, fc - centre) < 0f) inward++;
                }
                Assert.AreEqual(0, inward, "every face must wind OUTWARD; " + inward + " wound inward");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void IngotBar_IsFlatShaded_DistinctVertsPerFace_WithPerFaceValueSteps()
        {
            var mesh = IconBakerProto.IngotBarMesh();
            try
            {
                // 10 quads = 20 tris = 40 distinct verts (4 per quad, never welded — welding would average the
                // normals and lose the facets, lowpoly-quality.md §1).
                Assert.AreEqual(20, mesh.triangles.Length / 3, "expected 20 tris (10 quads)");
                Assert.AreEqual(40, mesh.vertexCount, "flat shading requires distinct verts per face");

                Color[] cols = mesh.colors;
                Assert.AreEqual(mesh.vertexCount, cols.Length,
                    "the shader does albedo = IN.color.rgb * _Tint.rgb — a missing colour stream loses the " +
                    "per-facet value proxy");
                float min = 1f, max = 0f;
                foreach (var c in cols) { if (c.r < min) min = c.r; if (c.r > max) max = c.r; }
                Assert.Greater(max - min, 0.2f,
                    "per-face value steps must give real facet-to-facet contrast (the light proxy)");
                Assert.LessOrEqual(max, 1f, "vertex-colour value must stay <= 1 (no HDR overshoot on a chip)");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        // Widest X span among verts within Eps of the given Y plane.
        private static float SpanXAtY(Mesh mesh, float y)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var v in mesh.vertices)
            {
                if (Mathf.Abs(v.y - y) > 1e-3f) continue;
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
            }
            return maxX - minX;
        }
    }
}
