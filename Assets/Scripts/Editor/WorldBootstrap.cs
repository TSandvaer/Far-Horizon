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

        // ---- WORLD-LOOK POLISH palettes (ticket 86ca8t9pq — Uma world-look brief §1/§2) ----
        // CLOUD 3-value cyan (Uma §1 anchor swatches — warm-leaning cyan, NOT cold steel blue; all
        // sub-0.95 HDR-clamp-safe so the reduced-but-present bloom doesn't bloom-clip the bright caps).
        static readonly Color CloudBody   = new Color(0.56f, 0.85f, 0.88f); // #8FD8E0 light cyan body
        static readonly Color CloudTop    = new Color(0.77f, 0.93f, 0.94f); // #C4ECEF bright top-lit cap
        static readonly Color CloudShadow = new Color(0.42f, 0.73f, 0.78f); // #6BBAC6 soft teal underside

        // NEAR-VISTA + FAR-RING mountain palette (Uma §2 anchor swatches — the foreground rock palette
        // shifted toward the sky-tint as it recedes: lighter, lower-sat, warmer; faded ranges blend
        // toward the horizon stop #DCE8E4 for the seamless dissolve). All sub-0.95.
        static readonly Color MtnBody     = new Color(0.60f, 0.65f, 0.68f); // #9AA6AE hazy warm grey-blue
        static readonly Color MtnSnow     = new Color(0.91f, 0.93f, 0.93f); // #E8ECEC pale warm white cap
        static readonly Color MtnRimBody  = new Color(0.71f, 0.77f, 0.80f); // #B6C4CC farthest range (near-sky)
        // == the horizon sky stop, by reference, so the farthest range's cap dissolves EXACTLY into the
        // sky/fog (Erik Q2 lockstep — can't drift from the seam-kill anchor).
        static readonly Color MtnRimSnow  = FarHorizon.WorldLookPalette.SkyHorizon; // #DCE8E4 (dissolves)

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
            // Clear the per-bootstrap cloud/mountain material cache so a re-run does not return materials
            // owned by the destroyed scene (the editor keeps statics across executeMethod invocations —
            // same reason LowPolyZoneGen.ResetMaterialCache exists).
            _vcMatCache.Clear();

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

            // ---- WORLD-LOOK POLISH (ticket 86ca8t9pq) ----
            // CLOUDS (Uma §1): chunky faceted cyan blobs high overhead, slow lateral drift. Built under
            // the env root so they travel with the environment + serialize into Boot.unity (NOT Awake —
            // the editor-vs-runtime trap; only the drift translate runs at runtime via CloudDrift).
            BuildClouds(envRoot, ZoneSeed + 7001);
            // VISTA (Uma §2 near-vista + Erik far-vista research 86ca8t9rh Route A): faceted mountain
            // silhouette ranges — a NEAR band (150-400u, crisp facets) + two FAR concentric rings
            // (~500u / ~1000u, tinted toward the horizon stop) so the eye ladders out to a horizon that
            // feels BIG. The atmospheric fade is the Exp^2 distance fog (colour == horizon stop, the
            // seam-kill). Static silhouette decoration — no collider, no NavMesh.
            BuildVista(envRoot, ZoneSeed + 9001);

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
                // Far clip must reach past the FAR vista ring (~1000u radius + its base extent) so the
                // distant horizon silhouettes are never frustum-clipped (Erik far-vista 86ca8t9rh: "set
                // camera far clip to ~2000u to cover the ring depth stack with room to spare"). The Exp^2
                // fog has dissolved everything past ~600u to the horizon stop anyway, so the extra reach
                // costs ~nothing (rings are static-batched, frustum-culled behind the player).
                mainCam.farClipPlane = Mathf.Max(mainCam.farClipPlane, 1600f);
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

        // ---- CLOUDS (Uma world-look brief §1) ----
        // Scatter 5-9 chunky faceted cyan cloud blobs high overhead (~30-60u), each 8-18u in its long
        // axis (8-18x the ~1u player), with a slow lateral drift along one shared wind direction (slight
        // per-cloud speed variance). Built editor-time + serialized into Boot.unity (the editor-vs-runtime
        // trap — only the CloudDrift translate runs at runtime). Deterministic from seed (reproducible
        // baked scene on rebase-regenerate). DIAGNOSTIC trace: a one-shot [world-trace] line per the
        // no-new-class-without-trace discipline.
        static void BuildClouds(GameObject envRoot, int seed)
        {
            var rnd = new System.Random(seed);
            var cloudsRoot = new GameObject("Clouds");
            cloudsRoot.transform.SetParent(envRoot.transform, false);

            var cloudMat = MakeVertexColorMat("LPCloudMat", Color.white);
            int count = 5 + rnd.Next(0, 5); // 5-9 (Uma §1: sparse, purposeful)

            // Single shared wind direction (Uma §1) — a gentle cross-play-space drift (mostly +X with a
            // touch of +Z). The drift band is centred on the play space; clouds wrap at +/- a half-span.
            var wind = new Vector3(1f, 0f, 0.18f).normalized;
            const float bandHalfSpan = 90f; // wide enough that the wrap is off-frame from the spawn

            // Spread the clouds across the sky dome above + around the play space (biased over + ahead so
            // the orbit cam catches them in the upper third). Altitude 30-60u (Uma §1).
            for (int c = 0; c < count; c++)
            {
                float radius = 4f + (float)rnd.NextDouble() * 5f; // long axis ~8-18u (radius 4-9)
                int blobs = 3 + rnd.Next(0, 4);                   // 3-6 spheroids (board sheet)
                var mesh = LowPolyMeshes.CloudBlob(radius, blobs, CloudBody, CloudTop, CloudShadow, rnd.Next());

                var cloud = new GameObject("LP_Cloud");
                cloud.transform.SetParent(cloudsRoot.transform, false);
                float px = Mathf.Lerp(-110f, 110f, (float)rnd.NextDouble());
                float pz = Mathf.Lerp(-40f, 130f, (float)rnd.NextDouble());
                float py = 30f + (float)rnd.NextDouble() * 30f; // 30-60u altitude
                cloud.transform.position = new Vector3(px, py, pz);
                cloud.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);

                var mf = cloud.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = cloud.AddComponent<MeshRenderer>();
                mr.sharedMaterial = cloudMat;
                // Clouds are far overhead — no self-shadow chunk on the play space, no receive.
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                // Drift (serialized config; only the translate runs at runtime).
                var drift = cloud.AddComponent<FarHorizon.CloudDrift>();
                drift.windDir = wind;
                drift.speed = 0.22f + (float)rnd.NextDouble() * 0.26f; // 0.22-0.48 u/s (Uma §1)
                drift.wrapHalfSpan = bandHalfSpan;
                drift.bandCentre = new Vector3(0f, py, pz); // wrap relative to this cloud's own lane
            }
            Debug.Log($"[world-trace] BuildClouds placed {count} clouds (wind={wind}, alt 30-60u, drift 0.22-0.48 u/s)");
        }

        // ---- VISTA (Uma §2 near-vista + Erik far-vista research 86ca8t9rh Route A) ----
        // The layered far-horizon: a NEAR band of overlapping faceted mountain silhouettes (150-400u,
        // crisp facets, full grey-to-snow value) + two FAR concentric rings (~500u / ~1000u) tinted
        // progressively toward the horizon stop so they dissolve into the Exp^2 fog (colour == horizon
        // stop — the seam-kill). Each ring is a circle of FacetedMountain peaks facing the play space.
        // Static silhouette decoration: no collider, no NavMesh, shadow-casting OFF (far + flat-lit).
        static void BuildVista(GameObject envRoot, int seed)
        {
            var rnd = new System.Random(seed);
            var vistaRoot = new GameObject("Vista");
            vistaRoot.transform.SetParent(envRoot.transform, false);

            // ATMOSPHERIC FADE via the per-range _Tint (Uma §2): the tint LERPS the range toward the
            // horizon sky stop as the range recedes — near = full saturation (tint ~white, the mesh's full
            // grey-to-snow contrast reads), far = tinted toward #DCE8E4 so the silhouette desaturates +
            // lifts toward the sky it dissolves into (a multiplicative tint toward the horizon stop both IS
            // the fade AND lands the far rings on the seam-kill colour at the horizon line). The fade
            // FRACTION grows with distance; bound to the single horizon-stop anchor (no drift).
            Color horizon = FarHorizon.WorldLookPalette.SkyHorizon;
            Color FadeTint(float k) => Color.Lerp(Color.white, horizon, Mathf.Clamp01(k)); // k=0 crisp .. 1 sky

            // NEAR band (Uma §2 far-landmass 150-400u): 2-3 overlapping ranges at staggered distance for
            // parallax depth — the "range behind range" read. Centred toward the inland/forward arc the
            // orbit cam looks at, plus a wrap-around so the seaward orbit also sees a horizon.
            BuildMountainRing(vistaRoot, "Vista_Near",  ringRadius: 240f, peaks: 14, baseR: 60f,
                height: 90f,  snowline: 0.62f, body: MtnBody,    snow: MtnSnow,    tint: FadeTint(0.05f), rnd: rnd);
            BuildMountainRing(vistaRoot, "Vista_Near2", ringRadius: 360f, peaks: 16, baseR: 80f,
                height: 130f, snowline: 0.58f, body: MtnBody,    snow: MtnSnow,    tint: FadeTint(0.30f), rnd: rnd);

            // FAR rings (Erik Route A — ~500u / ~1000u, progressively tinted toward the horizon stop so
            // they dissolve seamlessly into the fog at the horizon line; lower-poly per ring). The farthest
            // ring is nearly the horizon stop itself — it reads as "almost sky" (Uma §2 farthest range).
            BuildMountainRing(vistaRoot, "Vista_FarRing1", ringRadius: 520f, peaks: 18, baseR: 110f,
                height: 180f, snowline: 0.55f, body: MtnRimBody, snow: MtnRimSnow, tint: FadeTint(0.55f), rnd: rnd);
            BuildMountainRing(vistaRoot, "Vista_FarRing2", ringRadius: 1000f, peaks: 14, baseR: 200f,
                height: 300f, snowline: 0.52f, body: MtnRimBody, snow: MtnRimSnow, tint: FadeTint(0.80f), rnd: rnd);
            Debug.Log("[world-trace] BuildVista built 4 mountain ranges (near 240/360u + far rings 520/1000u, fade->horizon)");
        }

        // Build one ring of faceted mountain peaks at `ringRadius` around the play-space origin, each peak
        // facing inward toward the player. The per-range atmospheric tint (lighter/near-sky as it recedes)
        // is applied via the material _Tint; the grey-to-snow value contrast is baked in the mesh.
        static void BuildMountainRing(GameObject parent, string name, float ringRadius, int peaks,
            float baseR, float height, float snowline, Color body, Color snow, Color tint, System.Random rnd)
        {
            var ringRoot = new GameObject(name);
            ringRoot.transform.SetParent(parent.transform, false);
            var mat = MakeVertexColorMat("LPMtnMat_" + name, tint);

            for (int p = 0; p < peaks; p++)
            {
                // Even angular spread + jitter so peaks overlap irregularly (no stamped clones).
                float a = (p / (float)peaks) * Mathf.PI * 2f + (float)(rnd.NextDouble() - 0.5) * 0.25f;
                float r = ringRadius * (0.92f + (float)rnd.NextDouble() * 0.16f);
                float h = height * (0.8f + (float)rnd.NextDouble() * 0.5f);
                float br = baseR * (0.85f + (float)rnd.NextDouble() * 0.5f);
                var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);

                var mesh = LowPolyMeshes.FacetedMountain(br, h, 6 + rnd.Next(0, 4), snowline,
                    body, snow, rnd.Next());

                var peak = new GameObject("LP_Mountain");
                peak.transform.SetParent(ringRoot.transform, false);
                peak.transform.position = pos;
                // Face the peak inward toward the origin so its broadest silhouette reads to the player.
                peak.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);

                var mf = peak.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = peak.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // far + flat-lit
                mr.receiveShadows = false;
                mr.staticShadowCaster = false;
                // Mark static so URP static-batches the ring into 1-2 draw calls (Erik perf note).
                GameObjectUtility.SetStaticEditorFlags(peak,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
            }
        }

        // A shared vertex-color material (the same FarHorizon/LowPolyVertexColor shader the terrain /
        // canopy / rock use) for clouds + mountains: the mesh bakes its colours into per-vertex COLOR
        // (cloud cyan values / mountain grey-to-snow values) and this multiplies a per-instance _Tint
        // onto them (white = colours unmodified; a near-sky tint fades a far range). URP/Lit IGNORES
        // vertex colour, so this is the only path that renders the baked values. The shader is already
        // registered in AlwaysIncludedShaders (BuildEnvironment) so it does not strip. Falls back to a
        // flat URP/Lit (baked values lost, but never magenta) if unresolved. Cached per-bootstrap.
        static readonly System.Collections.Generic.Dictionary<string, Material> _vcMatCache =
            new System.Collections.Generic.Dictionary<string, Material>();
        static Material MakeVertexColorMat(string baseName, Color tint)
        {
            string key = baseName + "_" + Mathf.RoundToInt(tint.r * 255) + "_" +
                         Mathf.RoundToInt(tint.g * 255) + "_" + Mathf.RoundToInt(tint.b * 255);
            if (_vcMatCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            Material mat;
            if (vc != null)
            {
                mat = new Material(vc) { name = key };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", tint);
            }
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
                mat.SetColor("_BaseColor", tint);
                mat.SetFloat("_Smoothness", 0.06f);
                Debug.LogWarning("[WorldBootstrap] vertex-color shader not found; " + baseName + " falls back to flat URP/Lit");
            }
            _vcMatCache[key] = mat;
            return mat;
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
