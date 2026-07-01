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
        public void Sun_LoweredTowardHorizon_ForGameplayFraming()
        {
            // SUN-LOWER (ticket 86cag25az — folded into the #194 sky PR). The warm-gold disk was baked at
            // elevation 48° (Euler X) — too high for the pitch-[8,70]-clamped over-shoulder orbit to ever
            // frame in normal play, so the Sponsor's first #194 soak couldn't SEE the sun. The fix LOWERS the
            // "Sun" key toward the horizon so the disk sits in the upper-sky band the orbit frames when looking
            // toward the horizon. Guard the COMMITTED Sun rotation (the bytes the exe ships): elevation must be
            // clearly LOWERED (well under the old 48°) but still a SKY sun (above the horizon). A future change
            // that reverts the elevation back up reds here before it ships an invisible sun again.
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.name == "Sun");
            Assert.IsNotNull(sun, "the warm 'Sun' directional key must exist");

            // The sun's ELEVATION above the horizon = the to-sun vector's Y angle (-light.forward; the same
            // direction QualityPassGen bakes into _SunDirection). Compute it the way the bake does.
            Vector3 toSun = -sun.transform.forward;
            float elevationDeg = Mathf.Asin(Mathf.Clamp(toSun.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            Assert.Less(elevationDeg, 30f,
                $"the Sun must be LOWERED toward the horizon (elevation < 30°, was 48° — too high to frame at " +
                $"the gameplay orbit pitch); measured {elevationDeg:F1}° (ticket 86cag25az)");
            Assert.Greater(elevationDeg, 2f,
                $"the Sun must stay ABOVE the horizon (a sky sun, not on/below the waterline); measured {elevationDeg:F1}°");

            // The baked sky-material _SunDirection (the VISUAL disk) must MATCH the lowered light direction —
            // a mismatch would render the disk somewhere other than where the light comes from. Allow a small
            // tolerance (normalisation / float).
            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_SunDirection"))
            {
                Vector4 sd = sky.GetVector("_SunDirection");
                Vector3 diskDir = new Vector3(sd.x, sd.y, sd.z).normalized;
                float align = Vector3.Dot(diskDir, toSun.normalized);
                Assert.Greater(align, 0.99f,
                    "the sky material's baked _SunDirection (the visual disk) must align with the lowered Sun " +
                    "light direction (QualityPassGen.ResolveSunDirection bakes -light.forward) — the disk and " +
                    $"the shading light must agree; dot={align:F3}");
            }
        }

        [Test]
        public void Atmosphere_DistanceFogEnabled_ColourMatchesHorizonStop()
        {
            // WORLD-LOOK RE-TUNE (ticket 86ca8t9pq — Uma world-look brief §3 + Erik far-vista 86ca8t9rh):
            // the fog contract CHANGED. The Zone-D fog was a warm-haze (R>B); the world-look pass makes fog
            // the §2 atmospheric-fade / vista seam-kill — its colour must EQUAL the horizon sky stop
            // (#DCE8E4) so the distant mountains dissolve into the SAME colour the sky fades to (no horizon
            // seam; URP does not fog the skybox). The horizon stop is warm-bright cream-blue (G>B>R), so the
            // old "R>B warm" heuristic no longer holds — the warmth now lives in the grade. Assert the new
            // contract: fog ON + colour == the horizon stop anchor.
            Assert.IsTrue(RenderSettings.fog, "distance fog must be enabled (the §2 atmospheric fade)");
            Color horizon = FarHorizon.WorldLookPalette.SkyHorizon;
            Assert.AreEqual(horizon.r, RenderSettings.fogColor.r, 0.01f, "fog R must == the horizon sky stop (seam-kill)");
            Assert.AreEqual(horizon.g, RenderSettings.fogColor.g, 0.01f, "fog G must == the horizon sky stop (seam-kill)");
            Assert.AreEqual(horizon.b, RenderSettings.fogColor.b, 0.01f, "fog B must == the horizon sky stop (seam-kill)");
        }

        [Test]
        public void Skybox_GradientAssigned()
        {
            Assert.IsNotNull(RenderSettings.skybox,
                "a gradient skybox must be assigned (warm horizon -> cool sky), not a flat clear color");
        }

        [Test]
        public void Skybox_SunDisk_SponsorBakedDefaultsSet()
        {
            // SUN-DISK POC (ticket 86cabc743 — Erik low-poly-sky research) + the SPONSOR-ACCEPTED bake (soak
            // 2026-07-01, ticket 86cah90cp): the GradientSkybox material must carry the three sun-disk properties
            // with the Sponsor-dialed hue + biggest-in-range size (QualityPassGen sets them). Guard the COMMITTED
            // material the exe ships (RenderSettings.skybox is the serialized scene value), not a runtime
            // tautology — a future change that drops/alters the sun params fails here before shipping.
            var sky = RenderSettings.skybox;
            Assert.IsNotNull(sky, "a skybox material must be assigned");
            // Only meaningful when the custom gradient shader resolved (the fallback Skybox/Procedural path
            // has its own _SunSize and is NOT the sun-disk POC — assert against the gradient shader by name).
            Assert.AreEqual("FarHorizon/GradientSkybox", sky.shader.name,
                "the skybox must use the custom FarHorizon/GradientSkybox shader (the sun disk lives there); " +
                "the Skybox/Procedural fallback would mean the custom shader failed to resolve at bootstrap");

            Assert.IsTrue(sky.HasProperty("_SunColor"), "the sky material must expose _SunColor (the sun-disk POC)");
            Assert.IsTrue(sky.HasProperty("_SunSize"), "the sky material must expose _SunSize");
            Assert.IsTrue(sky.HasProperty("_SunHardness"), "the sky material must expose _SunHardness");

            Color sun = sky.GetColor("_SunColor");
            // The Sponsor-dialed hue (0.74,0.84,0.26) is a warm YELLOW-GREEN toy sun (G highest, B near-zero) —
            // a distinct dial from the prior soft warm white (0.98,0.86,0.86). Warm-not-cold still holds (R >> B).
            Assert.Greater(sun.r, sun.b,
                "the sun disk must be WARM (R > B) — a cold/blue sun is a style mismatch with the warm Zone-D " +
                "sky. The Sponsor-dialed hue is a warm yellow-green (0.74,0.84,0.26): B near-zero (ticket 86cah90cp)");
            Assert.AreEqual(QualityPassGen_SunColor.r, sun.r, 0.01f, "sun R must match the QualityPassGen Sponsor-accepted default");
            Assert.AreEqual(QualityPassGen_SunColor.g, sun.g, 0.01f, "sun G must match the QualityPassGen Sponsor-accepted default");
            Assert.AreEqual(QualityPassGen_SunColor.b, sun.b, 0.01f, "sun B must match the QualityPassGen Sponsor-accepted default");
            Assert.Greater(sky.GetFloat("_SunHardness"), 1f,
                "_SunHardness must be a crisp-disk exponent (>1), not flattened to a sky-wide glow");
            float size = sky.GetFloat("_SunSize");
            // The shader's _SunSize range is [0.95, 0.9999] (LOWER = BIGGER disk). The Sponsor accepted 0.95 —
            // the biggest disk in-range. Guard the exact accepted value (the disk must not shrink toward a
            // pinpoint near 1.0, nor drift out of the shader's lower bound).
            Assert.AreEqual(QualityPassGen_SunSize, size, 0.001f,
                "_SunSize must match the Sponsor-accepted 0.95 (the biggest disk in the shader's [0.95,0.9999] range)");
        }

        // Forwarded constants so this test asset (PlayTests/EditTests asmdef) reads the same Sponsor-accepted
        // defaults QualityPassGen ships, without depending on the editor-only QualityPassGen type directly.
        // (soak 2026-07-01, ticket 86cah90cp — warm yellow-green hue + biggest-in-range size.)
        private static readonly Color QualityPassGen_SunColor = new Color(0.74f, 0.84f, 0.26f, 1f);
        private const float QualityPassGen_SunSize = 0.95f;

        [Test]
        public void SkyVerifyCapture_WiredIntoBootScene()
        {
            // The SKY-FACING capture component (the SHIPPED-build sun-disk evidence) must be SERIALIZED into
            // Boot.unity (the component-in-source-but-not-in-scene trap — it would ship inert otherwise, and
            // the -verifySky CI gate would find nothing). Assert it rode the bootstrap into the saved scene.
            var caps = Object.FindObjectsByType<SkyVerifyCapture>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, caps.Length,
                "exactly one SkyVerifyCapture must be wired into Boot.unity (the -verifySky shipped-build " +
                "sun-disk capture gate drives it; a missing component ships an inert gate)");
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
        public void Shadows_BandGoneViaSoftFade_AndFlickerKilledBySoftFilterPlusDensity()
        {
            // GREEN-LINE FIX (86ca9qwr3 / 86caarn6y) + FLICKER FIX (86caayvfz, 2nd pass), guarded as ONE
            // contract. The 1st-pass approach (push shadowDistance to 360u + 4 cascades) cleared the band
            // but the Sponsor STILL saw flicker (build a5282c6: "still present, maybe too aggressive") —
            // because 360u stretches even 4 cascades thin in the FAR field (~5 texels/unit). The fix swaps
            // the band strategy from "huge distance" to "GENTLE soft fade" + kills the crawl percept with
            // SOFT shadows + a denser map:
            //   (a) Band-gone via SOFT FADE, not huge distance: a moderate distance that covers the framed
            //       grass + roam reach, PAIRED with a WIDE cascade-border so the boundary fades softly in
            //       the far distance (where fog hides it) instead of as a hard band. Distance no longer has
            //       to span the whole island — so it must NOT regress back to the huge 360u (re-opens the
            //       far-field crawl) NOR shrink below the framed play area (re-opens the band).
            //   (b) Flicker-killed by SOFT (filtered) shadows: the 7x7 tent filter blurs the edge across
            //       texels so per-frame texel-quantization stops reading as flicker. This is the direct kill
            //       the cascade work alone could not deliver. m_SoftShadowsSupported must be ON.
            //   (c) Density backstop: 4 cascades (near-field) + a 4096 main-light map (all cascades). A single
            //       cascade or a low-res map re-opens the crawl.
            // Read the COMMITTED URP asset (the bytes the exe ships), not a runtime tautology.
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/Settings/FarHorizonURP.asset");
            Assert.IsNotNull(urp, "the committed FarHorizonURP.asset must load as a UniversalRenderPipelineAsset");

            // (a) Band-gone via soft fade: distance must cover the framed grass + roam reach (>= ~180u) but
            // must NOT regress to the huge 360u that re-opens the far-field crawl (<= ~280u). The wide
            // cascade-border (>= 0.45) is what actually fades the boundary now, not the distance.
            Assert.GreaterOrEqual(urp.shadowDistance, 180f,
                "shadowDistance must cover the gameplay-framed grass + roam reach or the shadow-distance " +
                "boundary re-enters the visible grass as the green-line band (86caarn6y)");
            Assert.LessOrEqual(urp.shadowDistance, 280f,
                "shadowDistance must NOT regress to the huge 360u band-by-distance approach — that stretches " +
                "even 4 cascades thin in the far field and re-opens the flicker (86caayvfz 2nd pass)");
            Assert.GreaterOrEqual(urp.cascadeBorder, 0.45f,
                "cascadeBorder must stay wide (>=0.45) — the soft fade is what removes the band now that the " +
                "distance is moderate, not a hard cutoff pushed past the island (86caayvfz 2nd pass)");

            // (b) Flicker-killed by SOFT (filtered) shadows — the load-bearing new lever this pass.
            Assert.IsTrue(urp.supportsSoftShadows,
                "soft (filtered) shadows must be ON — the 7x7 tent filter blurs the shadow edge so texel " +
                "crawl stops reading as 'flicker / too aggressive' (86caayvfz 2nd pass, build a5282c6)");

            // (c) Density backstop: 4 cascades (near) + 4096 map (all cascades).
            Assert.GreaterOrEqual(urp.shadowCascadeCount, 2,
                "main-light shadows must use >=2 cascades for near-field texel density (we ship 4); a single " +
                "cascade crawls (86caayvfz)");
            Assert.GreaterOrEqual(urp.mainLightShadowmapResolution, 4096,
                "main-light shadow map must be >=4096 — doubling density vs 2048 across all cascades backs up " +
                "the distance-reduction + soft filter so far cascades hold up (86caayvfz 2nd pass)");
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
