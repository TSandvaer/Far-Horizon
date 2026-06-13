using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode scene-presence guard for the BEACH OCEAN (drew/beach-water-scene; Uma
    /// beach-water-direction §4). The water is a VISUAL surface, so the shipped-build capture gate is
    /// the final evidence — but binary scenes can't be GUID-grepped, so this EditMode reader is the
    /// authoritative check that the re-tuned ocean actually lives in the Boot.unity the exe ships
    /// (the component/mesh-in-source-but-not-serialized failure class, unity-conventions.md).
    ///
    /// Each assertion guards a SPECIFIC bug the diagnostic trace found in the original water (2026-06-13,
    /// drew/beach-water): it shipped as a 10x10 primitive Plane with meshColors=0 (single-tone — the
    /// gradient shader had nothing to ride), scale.z=4 (~40u deep, a hard far edge inside the fog),
    /// URP/Lit smoothness 0.88 (mirror-glossy, breaks the toy). If a future change reverts any of those,
    /// exactly one of these fails in headless CI before it ships.
    /// </summary>
    public class WaterSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        // Slack for the gradient-anchor pins through serialization float round-trip (matches the
        // HeroAxeSceneTests convention) — guards drift/regression, not a pixel-exact match.
        private const float ColorTol = 0.03f;

        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

        [Test]
        public void Ocean_ExistsInShippedScene_WithVertexColorGradient()
        {
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the Boot scene must carry the beach ocean (Water_Play) — it ships via " +
                "WorldBootstrap into Boot.unity (the diagnostic confirmed it WAS present, just unfinished)");

            var mf = water.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "the ocean must have a MeshFilter");
            Assert.IsNotNull(mf.sharedMesh, "the ocean mesh must be serialized into the scene");

            // THE headline bug: the old primitive Plane had meshColors=0, so the near->far teal gradient
            // had nothing to ride and the water shipped single-tone. A welded grid with baked vertex
            // colors is the fix — assert the colors are actually present.
            Assert.Greater(mf.sharedMesh.colors.Length, 0,
                "the ocean mesh must carry per-vertex COLORS (the near->far teal gradient is baked in " +
                "vertex color, ridden by FarHorizon/LowPolyVertexColor — a primitive Plane has none)");

            // And it must be a subdivided GRID (not the 10x10=121-vert primitive), so the gradient has
            // verts to interpolate across AND the swell has verts to displace.
            Assert.Greater(mf.sharedMesh.vertexCount, 200,
                "the ocean must be a subdivided welded grid (>200 verts), not the original 10x10 plane " +
                "(the gradient + in-shader swell need real subdivision)");
        }

        [Test]
        public void Ocean_GradientEndpoints_MatchTealAnchors_ShallowToDeep()
        {
            var water = GameObject.Find("Water_Play");
            Assert.IsNotNull(water, "the ocean (Water_Play) must be present");
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.vertices;
            var cols = mesh.colors;
            Assert.AreEqual(verts.Length, cols.Length, "every ocean vert must carry a color");

            // The gradient runs near-shore BRIGHT -> seaward DEEPER along local Z. Find the near-shore
            // vert (max local Z, since the grid runs nearZ at fz=0 toward farZ seaward = decreasing Z)
            // and the seaward-most vert (min local Z), and check their colors against the anchors.
            int nearIdx = 0, farIdx = 0;
            for (int i = 1; i < verts.Length; i++)
            {
                if (verts[i].z > verts[nearIdx].z) nearIdx = i; // largest Z = nearest shore
                if (verts[i].z < verts[farIdx].z) farIdx = i;   // smallest Z = deepest sea
            }
            AssertColorNear(cols[nearIdx], LowPolyZoneGen.WaterShallow,
                "near-shore vert must be the BRIGHT shallow teal (#3FA6B0)");
            AssertColorNear(cols[farIdx], LowPolyZoneGen.WaterDeep,
                "seaward vert must be the DEEPER teal (#2E7E96)");

            // Every channel sub-1.0 (HDR-clamp discipline — the post stack must not bloom the sea white).
            foreach (var c in new[] { cols[nearIdx], cols[farIdx] })
            {
                Assert.Less(c.r, 1f, "water teal red channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.Less(c.g, 1f, "water teal green channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.Less(c.b, 1f, "water teal blue channel must be sub-1.0 (HDR-clamp-safe)");
            }
        }

        [Test]
        public void Ocean_ExtendsFarSeaward_PastTheFogLine_NoHardNearEdge()
        {
            var water = GameObject.Find("Water_Play");
            var mr = water.GetComponent<MeshRenderer>();
            // The original water spanned ~40u of Z (scale.z=4). The re-tuned ocean must extend FAR
            // seaward so its far edge dissolves into the fog rather than reading as a hard plane edge.
            float depthZ = mr.bounds.size.z;
            Assert.Greater(depthZ, 150f,
                "the ocean must extend far seaward (>150u of depth) so its far edge is lost in the warm " +
                "fog haze — the 'far horizon' read — not a visible hard plane edge inside the fog");
            // And wider than the land so the sea wraps the coast at the edges of the frame.
            Assert.Greater(mr.bounds.size.x, 90f,
                "the ocean must be wider than the play space so it fills the seaward frame edge-to-edge");
        }

        [Test]
        public void Ocean_Material_RidesVertexColorShader_WithSwellEnabled()
        {
            var water = GameObject.Find("Water_Play");
            var mr = water.GetComponent<MeshRenderer>();
            var mat = mr.sharedMaterial;
            Assert.IsNotNull(mat, "the ocean must have a material");
            Assert.AreEqual("FarHorizon/LowPolyVertexColor", mat.shader.name,
                "the ocean must render through FarHorizon/LowPolyVertexColor so the vertex-color teal " +
                "gradient shows (URP/Lit IGNORES vertex color -> the old single-tone water)");

            // The gentle swell is in-shader (_WaveAmp > 0). This is the WATER ONLY — the terrain/canopy
            // materials share this shader but leave _WaveAmp at its 0 default (so they don't wobble).
            Assert.IsTrue(mat.HasProperty("_WaveAmp"), "the shader must expose _WaveAmp (the in-shader swell)");
            Assert.Greater(mat.GetFloat("_WaveAmp"), 0f,
                "the ocean material must enable the gentle swell (_WaveAmp > 0) — a dead-still plane reads " +
                "as ice/glass and kills the 'alive world' feel (Uma §1)");
            Assert.Less(mat.GetFloat("_WaveAmp"), 0.2f,
                "the swell must stay SUBTLE (a breath, not surf) — small amplitude only");
        }

        [Test]
        public void Terrain_CarriesFoamBand_AtTheSeawardShoreRows()
        {
            // The foam line (Uma §2 task D) is baked into the seaward-most rows of the BEACH MESH vertex
            // colors (rides the terrain shader, no new object). Assert the warm off-white foam color
            // actually appears in the terrain's vertex colors — a regression that drops the foam bake
            // would leave the shoreline with no waterline read.
            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "the play-space terrain (Ground_Play) must exist");
            var cols = ground.GetComponent<MeshFilter>().sharedMesh.colors;
            Assert.Greater(cols.Length, 0, "terrain must carry vertex colors");

            // Count verts close to the warm foam anchor (#E8E2D0). The foam band is a narrow seaward
            // strip, so only a handful of rows qualify — assert at least a few exist.
            var foam = LowPolyZoneGen.FoamEdge;
            int foamVerts = cols.Count(c =>
                Mathf.Abs(c.r - foam.r) < 0.12f &&
                Mathf.Abs(c.g - foam.g) < 0.12f &&
                Mathf.Abs(c.b - foam.b) < 0.12f &&
                c.r > 0.8f && c.g > 0.8f); // foam is the brightest band on the beach
            Assert.Greater(foamVerts, 0,
                "the beach mesh must carry a FOAM band (warm off-white #E8E2D0) baked into its seaward-most " +
                "vertex rows — the calm stylized waterline (Uma §2)");
        }

        [Test]
        public void BootScene_CarriesSeaVerifyCapture_OnTheBootObject()
        {
            // The orbit-to-sea framing check (Uma §4 task F) needs a committed, repeatable shipped-build
            // capture path: the SeaVerifyCapture must be SERIALIZED onto the Boot object (sibling of the
            // movement/craft/chop/loop captures). Drop the WireSeaVerifyCapture authoring and this goes
            // RED — the -verifySea path silently vanishing from the shipped scene is the failure class
            // this guards (component-in-source-but-not-serialized, unity-conventions.md).
            var boot = GameObject.Find("Boot");
            Assert.IsNotNull(boot, "the Boot scene must carry the 'Boot' object (host of the verify captures)");
            Assert.IsNotNull(boot.GetComponent<FarHorizon.SeaVerifyCapture>(),
                "the Boot object must carry the SeaVerifyCapture component (the committed -verifySea " +
                "orbit-to-sea capture path), serialized into the scene — not Awake-added");
        }

        private static void AssertColorNear(Color c, Color anchor, string label)
        {
            Assert.AreEqual(anchor.r, c.r, ColorTol, $"{label}: red channel");
            Assert.AreEqual(anchor.g, c.g, ColorTol, $"{label}: green channel");
            Assert.AreEqual(anchor.b, c.b, ColorTol, $"{label}: blue channel");
        }
    }
}
