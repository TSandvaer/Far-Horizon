using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Integration guard: the Sponsor-approved Zone-D look (ticket 86ca86fux) actually renders in the
    /// PRODUCTION scene as the play space — lighting / fog / post / skybox / terrain / scatter, all
    /// serialized into Boot.unity by the bootstrap (NOT assembled at runtime — the editor-vs-runtime
    /// serialization rule, unity-conventions.md). If a future change drops the post volume, reverts the
    /// warm key, disables fog, or strips the terrain, exactly one of these fails in headless CI before
    /// it ships.
    ///
    /// These assert the REAL saved-scene + project state the bootstrap produced (not tautologies). They
    /// run against the committed Boot.unity (the bytes the exe ships), so they are the editor-side half
    /// of the shipped-build verification gate — the windowed-capture is the other half.
    /// </summary>
    public class ZoneDLookTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [SetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
        }

        [Test]
        public void Environment_RootExists_AsAdditiveScopedContainer()
        {
            // The environment is a single additive root so U3's player/camera stay independent
            // (SCOPED CONTRACT). A missing root means the Zone-D build was dropped from the scene.
            var env = GameObject.Find("Environment");
            Assert.IsNotNull(env, "the Boot scene must contain the additive 'Environment' root " +
                "(the Zone-D play space lives under it, scoped away from U3's player/camera)");
        }

        [Test]
        public void Terrain_LowPolyGround_ExistsWithVertexColorsAndCollider()
        {
            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "the low-poly play-space terrain (Ground_Play) must exist");

            var mf = ground.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "terrain must have a MeshFilter");
            Assert.IsNotNull(mf.sharedMesh, "terrain mesh must be assigned");
            Assert.Greater(mf.sharedMesh.colors.Length, 0,
                "terrain mesh must carry per-vertex COLORS (the warm sand->grass ramp is baked in " +
                "vertex color, not a texture) — the Zone-D multi-tone ground");

            Assert.IsNotNull(ground.GetComponent<MeshCollider>(),
                "terrain must have a MeshCollider so the NavMesh bakes on the actual sloped surface");
        }

        [Test]
        public void Scatter_GrassClumpsPresent_TheFoliageFix()
        {
            // The iter-8 grass fix is the headline scope item; the play space must actually contain
            // grass-clump scatter (not just the mesh-gen guard in isolation).
            var clumps = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Count(r => r.gameObject.name == "Blades");
            Assert.Greater(clumps, 0,
                "the play space must scatter grass clumps (the iter-8 up-biased-normal foliage fix " +
                "must be VISIBLE in the scene, not only correct in LowPolyMeshTests)");
        }

        [Test]
        public void Water_EdgePresent()
        {
            Assert.IsNotNull(GameObject.Find("Water_Play"),
                "the warm-teal water edge must exist at the beach (part of the Zone-D look)");
        }

        [Test]
        public void Trees_BlobCanopies_Present_MultiBlobVolumeWithVertexGreens()
        {
            // Board v2 (ticket 86ca8ce7j): the world's trees migrated to the BLOB-CANOPY language —
            // clustered faceted spheroids in multi-value greens (NOT single-dome FacetedSphere canopies).
            // The canopy meshes carry their greens in per-vertex COLOR and are solid multi-blob volumes.
            // Assert the play space actually scatters blob canopies (the headline scope item must be
            // VISIBLE in the saved scene, not only correct in LowPolyMeshTests).
            var canopies = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "Canopy" && mf.sharedMesh != null)
                .ToArray();
            Assert.Greater(canopies.Length, 0,
                "the play space must scatter blob-canopy trees (Canopy meshes) — the board-v2 tree pass");

            // A blob canopy is a multi-blob cluster: a single subdiv-1 FacetedSphere is 18 verts; a
            // cluster is well above that. AND it must carry per-vertex green colours (the multi-value rule).
            var sample = canopies[0].sharedMesh;
            Assert.Greater(sample.vertexCount, 40,
                "a Canopy mesh must be a multi-blob CLUSTER (>40 verts), not a single-dome sphere " +
                "(the pre-board single-FacetedSphere regression)");
            Assert.Greater(sample.colors.Length, 0,
                "the blob canopy must carry per-vertex COLORS (the multi-value greens baked into vertex " +
                "color so one vertex-color material renders the whole canopy)");
        }

        [Test]
        public void Lighting_SingleWarmDirectionalKey()
        {
            var dirs = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l.type == LightType.Directional).ToArray();
            Assert.AreEqual(1, dirs.Length,
                "exactly ONE directional key (the warm 'Sun') — two would double-expose and break " +
                "look parity with the approved Zone-D pass");
            var sun = dirs[0];
            Assert.AreEqual("Sun", sun.gameObject.name, "the single directional light must be the warm Sun");
            Assert.Greater(sun.color.r, sun.color.b,
                "the key must be WARM (red channel > blue) — the warm-amber Zone-D sun, not a cool key");
        }

        [Test]
        public void Atmosphere_WarmFogEnabled()
        {
            Assert.IsTrue(RenderSettings.fog, "global warm-haze fog must be enabled (the Zone-D atmosphere)");
            Assert.Greater(RenderSettings.fogColor.r, RenderSettings.fogColor.b,
                "fog must be WARM (red > blue) — the warm-haze direction, not a cold mist");
        }

        [Test]
        public void Skybox_GradientAssigned()
        {
            Assert.IsNotNull(RenderSettings.skybox,
                "a gradient skybox must be assigned (warm horizon -> cool sky), not a flat clear color");
        }

        [Test]
        public void PostProcessing_GlobalVolumeWithProfile()
        {
            var vol = GameObject.Find("ZoneD_PostVolume");
            Assert.IsNotNull(vol, "the Zone-D post-processing volume must exist in the play space");
            var v = vol.GetComponent<Volume>();
            Assert.IsNotNull(v, "ZoneD_PostVolume must carry a Volume component");
            Assert.IsTrue(v.isGlobal, "production post volume must be GLOBAL (graded everywhere, " +
                "not a side-by-side comparison box like the spike's local volume)");
            Assert.IsNotNull(v.sharedProfile, "the volume must reference a serialized profile asset");
            Assert.IsTrue(v.sharedProfile.Has<Bloom>(), "Zone-D profile must include Bloom");
            Assert.IsTrue(v.sharedProfile.Has<ColorAdjustments>(), "Zone-D profile must include warm Color Grading");
            Assert.IsTrue(v.sharedProfile.Has<WhiteBalance>(), "Zone-D profile must include WhiteBalance (the 5th serialized component — guard against a future drop, Tess Nit 2)");
            Assert.IsTrue(v.sharedProfile.Has<Vignette>(), "Zone-D profile must include Vignette");
            Assert.IsTrue(v.sharedProfile.Has<Tonemapping>(), "Zone-D profile must include filmic Tonemapping");
        }

        [Test]
        public void Camera_PostProcessingEnabled()
        {
            var cam = Camera.main;
            Assert.IsNotNull(cam, "the scene must have a main camera");
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            Assert.IsNotNull(data, "the camera must carry URP additional-camera data");
            Assert.IsTrue(data.renderPostProcessing,
                "the camera must render post-processing so the Zone-D Volume stack is visible");
        }

        [Test]
        public void VertexColorShader_RegisteredAlwaysIncluded_NoMagentaStrip()
        {
            // The custom terrain shader is stripped from the standalone build unless it is in
            // GraphicsSettings AlwaysIncludedShaders (the spike's magenta failure class). Assert it's
            // registered so the terrain never ships magenta — a build-only failure the editor hides.
            var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            Assert.IsNotNull(arr, "GraphicsSettings must expose m_AlwaysIncludedShaders");

            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            Assert.IsNotNull(vc, "the LowPolyVertexColor shader must resolve in the editor");

            bool registered = false;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == vc) { registered = true; break; }
            Assert.IsTrue(registered,
                "FarHorizon/LowPolyVertexColor must be in AlwaysIncludedShaders or the terrain ships " +
                "MAGENTA in the standalone build (the spike's shader-strip lesson)");
        }

        [Test]
        public void NavMesh_BakedAndSavedAsAsset_ShipsInBuild()
        {
            // Bake-in-memory ships a dead click-to-move; the data must be SAVED as an asset to embed
            // in the standalone build (unity-conventions.md NavMesh note). U3 consumes this surface.
            var navData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>("Assets/NavMesh/PlayNavMesh.asset");
            Assert.IsNotNull(navData,
                "the baked NavMesh must be saved as Assets/NavMesh/PlayNavMesh.asset so it ships in " +
                "the build (bake-in-memory ships a dead click-to-move)");
        }
    }
}
