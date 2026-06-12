using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Builds the production play-space ENVIRONMENT — the Sponsor-approved Zone-D low-poly quality
    /// look (terrain mesh + scatter + water + lighting + gradient skybox + warm fog + post volume),
    /// assembled editor-time and SAVED into the Boot scene (the editor-vs-runtime serialization rule,
    /// unity-conventions.md: anything that must exist in the build is built at editor time and
    /// serialized, never assembled in Awake).
    ///
    /// SCOPED to ENVIRONMENT only (ticket 86ca86fux): terrain, scatter, water, lighting/fog/post,
    /// skybox, shaders. It does NOT touch the player / camera / input systems (Devon's U3). The
    /// environment is built under a single additive root GameObject named "Environment" so U3's
    /// player/camera additions are independent and rebase coordination is a clean separation (first
    /// merged wins; the loser rebases — both only add their own root, neither rewrites the other).
    ///
    /// Called by BootstrapProject.Run AFTER the Boot scene is authored, so the URP asset + camera +
    /// directional light already exist when this layers the environment on.
    /// </summary>
    public static class WorldBootstrap
    {
        const string SettingsDir = "Assets/Settings";
        const string EnvRootName = "Environment";

        // Production play-space extents. A single deep, wide beach-to-meadow play space (the spike's
        // Zone D was ~43u wide x ~ShoreZ..InlandFarZ deep; production widens it so the world reads big
        // per the "small player, big alive world" north-star). Z runs shore (front) -> deep inland.
        const float ZoneMinX = -45f, ZoneMaxX = 45f;
        const float ShoreZ = -12f, InlandFarZ = 56f;
        const int ZoneSeed = 13001; // the spike's Zone-D seed, for look parity with the approved pass

        /// <summary>
        /// Build the full Zone-D environment into the currently-open scene and return the env root.
        /// Idempotent: a pre-existing "Environment" root is destroyed first so a re-run rebuilds clean.
        /// </summary>
        public static GameObject BuildEnvironment()
        {
            Debug.Log("[WorldBootstrap] BuildEnvironment begin");
            Directory.CreateDirectory(Path.GetFullPath(SettingsDir));

            // Register the custom vertex-color terrain shader as always-included BEFORE materials key
            // off it, so the standalone build does not strip it to magenta (the spike's iter-2/3
            // lesson; unity-conventions.md "Build stripping & shaders").
            var vcShader = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vcShader != null) EnsureShaderAlwaysIncluded(vcShader);
            else Debug.LogWarning("[WorldBootstrap] LowPolyVertexColor shader not found at bootstrap");
            // URP/Lit (used by the flat-color scatter materials) — also pin it so scatter never strips.
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null) EnsureShaderAlwaysIncluded(litShader);

            // Clear any prior environment root so re-running rebuilds deterministically.
            var existing = GameObject.Find(EnvRootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var envRoot = new GameObject(EnvRootName);

            // ---- Lighting: warm directional key + cool ambient fill (the smooth-shading recipe) ----
            BuildLighting(envRoot);

            // ---- Quality base: gradient skybox + skybox-driven (cool) ambient + warm global fog ----
            QualityPassGen.BuildGradientSkybox();
            QualityPassGen.EnableGlobalFog();

            // ---- The play space: low-poly smooth-shaded terrain + scatter + water ----
            LowPolyZoneGen.ResetMaterialCache();
            var vcMat = LowPolyZoneGen.MakeTerrainVertexColorMaterial(SettingsDir + "/LowPolyTerrainMat.mat");
            var waterMat = LowPolyZoneGen.MakeWaterMaterial(SettingsDir + "/LowPolyWaterMat.mat");
            var grounds = new GameObject("Grounds");
            grounds.transform.SetParent(envRoot.transform, false);
            var zone = LowPolyZoneGen.BuildZone(grounds, "Play", ZoneMinX, ZoneMaxX,
                ShoreZ, InlandFarZ, ZoneSeed, vcMat, waterMat);

            // ---- Quality pass: global post volume + camera post-processing ----
            QualityPassGen.BuildGlobalPostVolume();
            // Re-parent the post volume under the env root so it travels with the environment.
            var postVol = GameObject.Find("ZoneD_PostVolume");
            if (postVol != null) postVol.transform.SetParent(envRoot.transform, false);
            // Enable post on the existing main camera. Post-rebase this IS U3's orbit camera (it is
            // Camera.main by now), so the Zone-D Volume stack renders through the gameplay camera.
            // Defensive — no-op if no camera yet.
            var mainCam = Camera.main;
            QualityPassGen.EnableCameraPostProcessing(mainCam);
            // The gradient skybox is part of the Zone-D look, and U3's orbit-camera author sets the
            // camera clearFlags to SolidColor (deferring the skybox decision to U5 — "U5 owns the
            // skybox"). Re-assert Skybox clear on the gameplay camera so the assigned gradient sky
            // actually reads behind the silhouettes instead of a flat dusk-blue fill.
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.Skybox;
                mainCam.farClipPlane = Mathf.Max(mainCam.farClipPlane, 400f);
            }

            // ---- NavMesh: bake on the real sloped terrain + SAVE as an asset so it ships in the
            //      standalone build (the spike's iter-3 "NavMesh not shipping" lesson). ----
            BakeNavMesh(grounds);

            Debug.Log("[WorldBootstrap] BuildEnvironment complete -> " + zone.ground.name);
            return envRoot;
        }

        // Warm directional key (~48deg) models the sun; cool ambient fill keeps shadowed facets from
        // going black. The warm-key / cool-fill contrast over the averaged-normal low-poly geometry is
        // the primary driver of the "smooth-shaded looks good" read. (QualityPassGen.BuildGradientSkybox
        // switches ambientMode to Skybox afterward; this Trilight setup is the fallback base.)
        static void BuildLighting(GameObject parent)
        {
            // Remove any pre-existing default directional light (the boot scene's placeholder) so the
            // play space is lit by ONE warm key — two directional lights would double-expose and break
            // look parity with the approved Zone-D pass (which had a single warm Sun).
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.gameObject.name != "Sun")
                    Object.DestroyImmediate(l.gameObject);

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent.transform, false);
            var light = sunGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.93f, 0.80f);  // warm amber key
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.60f, 0.74f);     // cool sky fill
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.50f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.30f, 0.24f);  // warm ground bounce
        }

        // Bake a NavMesh over the play-space ground and SAVE the data as an asset so it embeds in the
        // standalone build (bake-in-memory ships a dead click-to-move — unity-conventions.md NavMesh
        // note + the spike's iter-3 finding). The surface is added under the grounds root; U3's
        // click-to-move consumes the same baked surface.
        static void BakeNavMesh(GameObject groundsRoot)
        {
            var surface = groundsRoot.GetComponent<NavMeshSurface>();
            if (surface == null) surface = groundsRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            if (surface.navMeshData != null)
            {
                string navDir = "Assets/NavMesh";
                Directory.CreateDirectory(Path.GetFullPath(navDir));
                string navPath = navDir + "/PlayNavMesh.asset";
                var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(navPath);
                if (existing == null)
                    AssetDatabase.CreateAsset(surface.navMeshData, navPath);
                else
                    EditorUtility.CopySerialized(surface.navMeshData, existing);
                AssetDatabase.SaveAssets();
                Debug.Log("[WorldBootstrap] NavMesh baked + saved -> " + navPath);
            }
            else
            {
                Debug.LogWarning("[WorldBootstrap] NavMesh bake produced no data");
            }
        }

        // Add a shader to GraphicsSettings.AlwaysIncludedShaders so the standalone build does NOT strip
        // it (the actual root cause of the spike's magenta blob in the shipped player). Ported verbatim
        // from SliceBootstrap.EnsureShaderAlwaysIncluded.
        static void EnsureShaderAlwaysIncluded(Shader shader)
        {
            if (shader == null) return;
            var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) { Debug.LogWarning("[WorldBootstrap] AlwaysIncludedShaders prop not found"); return; }

            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    Debug.Log("[WorldBootstrap] shader already in AlwaysIncludedShaders -> " + shader.name);
                    return;
                }

            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[WorldBootstrap] added shader to AlwaysIncludedShaders -> " + shader.name);
        }
    }
}
