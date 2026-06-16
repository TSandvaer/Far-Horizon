using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// THE silhouette/normal-direction class regression guard for the beach ocean
    /// (drew/ocean-beach-soakfix2, ticket 86ca8fet0).
    ///
    /// THE BUG IT CATCHES: the sea was INVISIBLE in the seaward gameplay view across SIX shipped builds.
    /// The ticket (and every prior fix attempt) framed it as the flat ground depth-OCCLUDING the water.
    /// A magenta-diff (paint the water bright magenta, capture the shipped -verifySea seaward frame,
    /// count water px) + a -seaWaterOnly probe (hide BOTH ground renderers) proved that wrong: the sea
    /// rendered ZERO px even with nothing in front of it. ROOT CAUSE: the water grid lays out
    /// nearZ -> farZ in DECREASING world Z (the sea runs away from the shore), the OPPOSITE Z direction
    /// to the terrain mesh — so reusing the terrain's triangle index order wound the WATER faces
    /// DOWNWARD. RecalculateNormals then produced -Y normals, and the shader's default Cull Back culled
    /// every water triangle from any camera looking DOWN at the sea (the gameplay orbit). The water was
    /// never occluded — it was backface-culled.
    ///
    /// This guard reads the SERIALIZED Water_Play mesh from the shipped Boot scene and asserts its
    /// vertex normals point UP (+Y). If a future change re-inverts the winding (or RecalculateNormals
    /// flips for any reason), this goes RED in headless CI BEFORE a build — the day-one guard that
    /// would have saved six builds. It is a pure-geometry EditMode test (no build, no capture) so it
    /// runs every CI cheaply; the magenta-diff (count_water.py) is the heavier shipped-build evidence.
    /// </summary>
    public class WaterFacesUpTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

        [Test]
        public void Water_NormalsPointUp_NotBackfaceCulledFromTheGameplayCamera()
        {
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the Boot scene must carry the beach ocean (Water_Play)");
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh, "the ocean mesh must be serialized into the scene");

            var normals = mesh.normals;
            Assert.Greater(normals.Length, 0, "the ocean mesh must carry normals (RecalculateNormals)");

            // EVERY water vertex normal must point UP (+Y dominant). A near-flat sea sheet has normals
            // ~= (0, +1, 0); the swell is in-shader, so the serialized rest-pose normals are flat-up.
            // If the winding is inverted, RecalculateNormals yields (0, -1, 0) -> Cull Back hides the sea.
            int downFacing = normals.Count(n => n.y <= 0f);
            Assert.AreEqual(0, downFacing,
                $"ALL {normals.Length} ocean vertex normals must point UP (+Y) so the sea is not " +
                $"backface-culled from the above-looking gameplay camera — found {downFacing} with " +
                "y<=0 (the INVERTED-WINDING bug that made the sea invisible for six builds). The water " +
                "grid runs in -Z (nearZ->farZ), opposite the terrain, so its triangle winding must be " +
                "REVERSED vs the terrain's to face up (LowPolyZoneGen.BuildWaterEdge).");

            // And the mesh-space transform must not be flipping them back down (no negative-Y scale).
            float worldUpY = water.transform.TransformDirection(Vector3.up).y;
            Assert.Greater(worldUpY, 0.5f,
                "the Water_Play transform must not flip the up-normal back down (no negative Y scale)");
        }

        [Test]
        public void Water_TriangleWinding_FrontFaceIsUp_NotJustTheNormalAttribute()
        {
            // THE real cull guard (ticket 86ca9yn57 — "water reads SAME as sky"). The sibling
            // Water_NormalsPointUp test asserts the NORMAL ATTRIBUTE is +Y — but URP `Cull Back` culls by
            // triangle WINDING (screen-space vertex order), NOT by the normal attribute. The prior soakfix
            // forced the normal +Y (that test stayed GREEN) while the emitted triangle winding still made the
            // TOP the BACK face -> the above-looking gameplay camera saw a culled back-face -> the sea rendered
            // 0 px and the SKY showed through == the Sponsor's "no difference between water and sky" (proven by
            // -waterProbe: ray hits Water_Play at 10-15u, insideBounds, yet 0 water px). This guard recomputes
            // the GEOMETRIC winding normal from each triangle's EMITTED vertex ORDER (the thing culling actually
            // uses) and asserts it points UP — so a re-inverted winding fails HERE even if the normal attribute
            // is (wrongly) forced +Y. It is the perceptual-vs-proxy lesson: guard the thing that makes the sea
            // visible (winding), not a proxy (normal attribute) a culled mesh can satisfy.
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "Water_Play must exist");
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            Assert.Greater(tris.Length, 0, "the ocean mesh must carry triangles");

            int downWound = 0, sampled = 0;
            // Sample every Nth triangle (the grid is large; a stride keeps the test fast while covering all
            // quadrants — a systematic winding inversion shows in any sample).
            int stride = Mathf.Max(1, (tris.Length / 3) / 600);
            for (int t = 0; t < tris.Length; t += 3 * stride)
            {
                Vector3 p0 = verts[tris[t]], p1 = verts[tris[t + 1]], p2 = verts[tris[t + 2]];
                Vector3 wn = Vector3.Cross(p1 - p0, p2 - p0); // winding normal (CCW front-face convention)
                if (wn.sqrMagnitude < 1e-12f) continue;
                sampled++;
                if (wn.y <= 0f) downWound++;
            }
            Assert.Greater(sampled, 0, "must have sampled some non-degenerate water triangles");
            Assert.AreEqual(0, downWound,
                $"every sampled water triangle's WINDING must make the TOP the FRONT face (winding-normal +Y) " +
                $"so URP Cull Back keeps the sea from the above-looking gameplay camera — found {downWound}/" +
                $"{sampled} wound DOWN (the backface-cull-invisible-sea bug that made 'water read same as sky'). " +
                "The normal ATTRIBUTE being +Y is NOT sufficient — culling uses the winding (LowPolyZoneGen." +
                "BuildIslandWater.EmitTri must REVERSE the source winding when it faces down).");
        }

        [Test]
        public void Water_FirstNormal_IsFlatUp()
        {
            // A tight, fast pin on the first vertex's normal — the value the diagnostic trace logged
            // going (0,-1,0) -> (0,+1,0) at the fix. Guards the headline regression directly.
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "Water_Play must exist");
            var normals = water.GetComponent<MeshFilter>().sharedMesh.normals;
            Assert.Greater(normals.Length, 0, "ocean mesh must have normals");
            Assert.Greater(normals[0].y, 0.9f,
                $"the ocean's first vertex normal must be flat-UP (~(0,1,0)) — was {normals[0]} " +
                "(a -Y normal is the backface-cull-invisible-sea bug)");
        }
    }
}
