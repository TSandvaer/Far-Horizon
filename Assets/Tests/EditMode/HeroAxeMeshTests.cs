using NUnit.Framework;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Unit guards for the M-U2 hero-axe MESH (ticket 86ca8ce6y — the first style-wave anchor). These
    /// assert the prop's geometry CONTRACT — the things that make it read like the board's tool language
    /// (inspiration/2026-06-12_21h08_08.png + style-guide-v2.md §3) — so a regression that flattens the
    /// silhouette, drops the signature edge-bevel plane, or collapses the 3-material split fails in
    /// headless CI rather than shipping a wrong-looking hero tool.
    ///
    /// The bug CLASS these catch (not just one instance):
    ///   - the bevel submesh going empty -> the signature near-white cutting-edge plane disappears
    ///     (the single most identity-defining detail of the prop family, style-guide §1.3);
    ///   - the submesh count drifting off 3 -> the head/bevel/haft anchor colors can no longer be
    ///     assigned per-part (the material array maps by submesh index);
    ///   - degenerate/zero normals -> faceted planes shading dark (the hard-faceted look depends on
    ///     well-formed per-face normals).
    /// Scene-presence + shipped-color anchoring is the sibling guard HeroAxeSceneTests.
    /// </summary>
    public class HeroAxeMeshTests
    {
        [Test]
        public void HeroAxe_HasThreeSubmeshes_HeadBevelHaft()
        {
            var mesh = HeroAxeMesh.Build();
            Assert.AreEqual(HeroAxeMesh.SUBMESH_COUNT, mesh.subMeshCount,
                "the hero axe must split into exactly 3 submeshes (HEAD/BEVEL/HAFT) so each carries its " +
                "own guide anchor color via a matching 3-material array");
        }

        [Test]
        public void HeroAxe_BevelSubmesh_HasGeometry_TheSignatureEdgePlane()
        {
            // The edge-bevel is the identity detail of the whole tool family (style-guide §1.3 / §3.2) —
            // GEOMETRY catching light, not a texture line. If the bevel submesh has no triangles, the
            // near-white cutting-edge plane never renders and the axe loses its signature read.
            var mesh = HeroAxeMesh.Build();
            int[] bevel = mesh.GetTriangles(HeroAxeMesh.SUBMESH_BEVEL);
            Assert.Greater(bevel.Length, 0,
                "the BEVEL submesh must carry the cutting-edge chamfer geometry — an empty bevel means the " +
                "signature near-white edge plane is gone (the prop family's identity detail)");

            // The head must dominate the bevel in triangle count (the bevel is a thin accent, not the body).
            int[] head = mesh.GetTriangles(HeroAxeMesh.SUBMESH_HEAD);
            Assert.Greater(head.Length, bevel.Length,
                "the barn-red HEAD body must have more geometry than its thin edge bevel accent");
        }

        [Test]
        public void HeroAxe_HaftSubmesh_HasGeometry()
        {
            var mesh = HeroAxeMesh.Build();
            int[] haft = mesh.GetTriangles(HeroAxeMesh.SUBMESH_HAFT);
            Assert.Greater(haft.Length, 0, "the HAFT submesh must carry the handle geometry");
        }

        [Test]
        public void HeroAxe_AllNormalsAreWellFormed_FacetsShadeClean()
        {
            // Hard-faceted look depends on well-formed per-face normals. A zero/degenerate normal shades
            // the facet dark (the same failure class as the grass dark-shard bug, different prop).
            var mesh = HeroAxeMesh.Build();
            var normals = mesh.normals;
            Assert.AreEqual(mesh.vertexCount, normals.Length, "every vertex must carry a normal");
            for (int i = 0; i < normals.Length; i++)
                Assert.AreEqual(1f, normals[i].magnitude, 0.05f,
                    $"hero-axe vertex {i} normal must be ~unit length (got {normals[i].magnitude:F3}) — a " +
                    "near-zero normal shades the facet dark");
        }

        [Test]
        public void HeroAxe_Silhouette_ReadsAsAxe_HeadAboveHaft_EdgeFacesMinusX()
        {
            // The silhouette is the readability bar (style-guide §1.2: reads at orbit distance from its
            // outline). Assert the bounding shape: the head sits ABOVE the haft body, the cutting edge
            // reaches into -X (the blade), and the prop is taller than it is wide (an axe, not a blob).
            var mesh = HeroAxeMesh.Build();
            var b = mesh.bounds;
            Assert.Greater(b.size.y, b.size.x,
                "the axe must be taller than it is wide — a head-up tool silhouette, not a blob");
            Assert.Greater(b.max.y, 1.0f,
                "the head must reach up near the top of the ~1.3u haft (head-up silhouette)");
            Assert.Less(b.min.x, -0.3f,
                "the cutting edge must flare into -X (the blade reaches well past the haft centerline)");
        }
    }
}
