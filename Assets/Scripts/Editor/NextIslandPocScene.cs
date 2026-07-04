using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// NEXT-ISLAND POC scene bootstrap (ticket 86caa9zpp). Authors a STAND-ALONE
    /// Assets/Scenes/NextIslandPoc.unity — a much-bigger organic walkable island with ONE dominant
    /// WALKABLE snow-capped mountain — and builds it into the Windows exe so the Sponsor can walk-soak it
    /// and the perf verdict (AC4, the #1 deliverable) is measured from the SHIPPED build.
    ///
    /// STAND-ALONE (AC1 + the scoped contract): the POC scene does NOT load / reference / wire to the
    /// seed-42 start island (Boot.unity), and this bootstrap does NOT touch the seed-42 LowPolyZoneGen path
    /// (that stays byte-untouched — "close to perfect"). The player + orbit-camera + WASD + jump machinery
    /// is REUSED from MovementCameraScene.Author (the exact stack the Sponsor already soaks) so the walk
    /// feels identical — then the flat test ground is swapped for the big POC terrain and the NavMesh is
    /// re-baked on it (a POC-specific asset, NOT the start island's BootNavMesh/PlayNavMesh).
    ///
    /// TWO-STEP BUILD CONTRACT (see build_poc.sh): the POC exe is built by chaining
    ///   1. BootstrapProject.Run                      — full project setup (URP pipeline + character/weapon
    ///                                                   asset import + build stamp). We REUSE this so the
    ///                                                   POC never re-implements ConfigureUrp / character prep.
    ///                                                   (It also authors Boot.unity + registers it as the
    ///                                                   build scene — both HARMLESSLY OVERRIDDEN by step 2.)
    ///   2. NextIslandPocScene.Build (this)           — authors the POC scene + RE-registers it as the ONLY
    ///                                                   enabled build scene (overriding Boot).
    ///   3. FarHorizonBuilder.BuildWindows            — ships the POC (the last-registered scene).
    /// Then verify_build_stamp + capture_gate run the SAME shipped-build gate the soak uses. Run step 2 via:
    ///   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.NextIslandPocScene.Build
    /// </summary>
    public static class NextIslandPocScene
    {
        public const string PocScenePath = "Assets/Scenes/NextIslandPoc.unity";
        public const string PocNavMeshPath = "Assets/NavMesh/PocNavMesh.asset";
        private const string SettingsDir = "Assets/Settings";

        // The POC island seed — a DEFAULT (the Sponsor dials variants from the soak). Deterministic coast +
        // hills + hero-mountain foot. 7 rolls a pleasant broad-bayed outline for the default handoff.
        public const int PocSeed = 7;

        /// <summary>
        /// The full POC build entry (executeMethod). Mirrors BootstrapProject.Run's setup so the POC exe is
        /// a valid standalone: URP pipeline + character/weapon prep (the player avatar needs them) + build
        /// stamp, then authors the POC scene, registers it as the ONLY build scene, and builds the exe.
        /// </summary>
        public static void Build()
        {
            Debug.Log("[poc-build] start (step 2/3 — authors the POC scene; step 1 BootstrapProject.Run ran the " +
                      "shared URP + character + stamp setup)");
            AuthorPocScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[poc-build] complete — POC registered as the only build scene; run FarHorizonBuilder.BuildWindows next");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Author the stand-alone POC scene into a NEW empty scene, save it, and register it as the ONLY
        /// enabled build scene (so FarHorizonBuilder.BuildWindows ships the POC, not Boot). Returns the scene.
        /// Split out so an EditMode test can author + open it without triggering a build/exit.
        /// </summary>
        public static UnityEngine.SceneManagement.Scene AuthorPocScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- Base camera + Boot host object (the verify-capture / HUD host, mirrors BuildBootScene) ----
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.10f, 0.13f, 0.18f);
            cam.farClipPlane = 2200f; // the big island + far sea reach past the default clip
            camGo.tag = "MainCamera";

            var lightGo = new GameObject("Directional Light");
            var light0 = lightGo.AddComponent<Light>();
            light0.type = LightType.Directional;
            light0.color = new Color(1f, 0.96f, 0.88f);
            light0.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var hudGo = new GameObject("Boot");
            hudGo.AddComponent<FarHorizon.BootHud>();          // the BUILD stamp HUD (soak identity)
            // FPS counter (86cahmxmt) — the ask CAME from this scene's walk-soak (#226 item 3), so the island
            // POC gets the same under-stamp readout as the Boot scene. Default ON; the F1 console row toggles it.
            hudGo.AddComponent<FarHorizon.FpsCounterHud>();
            hudGo.AddComponent<FarHorizon.CaptureGate>();      // generic -captureGate frame sanity
            hudGo.AddComponent<FarHorizon.FullscreenBoot>();   // fill the Sponsor's widescreen on a normal launch
            // The POC's shipped-build verify capture (perf verdict + side-profile silhouette). Serialized
            // editor-time (NOT Awake) per the editor-vs-runtime trap; INERT unless launched with -verifyPocIsland.
            hudGo.AddComponent<FarHorizon.PocIslandVerifyCapture>();

            // ---- Player + orbit camera + WASD + jump — the SAME stack the Sponsor soaks (reused wholesale) ----
            // MovementCameraScene.Author builds the full player/camera/WASD/jump + a flat TestGround + bakes a
            // NavMesh on it + wires the start-island survival objects near origin. We keep the player/camera and
            // SWAP the ground below (the POC terrain replaces the flat proxy). This is the lowest-risk way to
            // give the Sponsor an identical-feeling walk WITHOUT re-authoring (or editing) the movement lane.
            MovementCameraScene.Author(camGo);

            // ---- Swap the flat proxy for the big POC island + walkable snow-cap mountain ----
            // Destroy the flat TestGround + Author's (disabled) NavMeshSurface so the ONLY walkable surface is
            // the POC terrain we bake below. (Author's BootNavMesh.asset is left as-is on disk — untouched — but
            // nothing in THIS scene references it once its surface object is gone.)
            DestroyByName("TestGround");
            DestroyByName("NavMeshSurface");

            // Build the POC island (terrain + walkable hero mountain + water on all sides) under an env root,
            // reusing the SHARED FarHorizon vertex-color + water shaders (no new shader). The seed-42 gen is
            // NOT touched — this is a parallel big-island generator.
            var envRoot = new GameObject("PocEnvironment");
            BuildPocLook(envRoot, cam);

            LowPolyZoneGen.ResetMaterialCache();
            var vcMat = LowPolyZoneGen.MakeTerrainVertexColorMaterial(SettingsDir + "/PocTerrainMat.mat");
            var waterMat = LowPolyZoneGen.MakeWaterMaterial(SettingsDir + "/PocWaterMat.mat");
            var grounds = new GameObject("PocGrounds");
            grounds.transform.SetParent(envRoot.transform, false);
            GameObject ground = NextIslandPocGen.BuildPocIsland(grounds, PocSeed, vcMat, waterMat);

            // Dense forest/rock/grass across the big island (so the perf verdict is measured with a realistic
            // prop load). REUSES the public LowPolyMeshes primitives; the seed-42 scatter is untouched.
            var scatterRoot = new GameObject("PocScatter");
            scatterRoot.transform.SetParent(grounds.transform, false);
            NextIslandPocScatter.Scatter(scatterRoot, PocSeed, ground.GetComponent<MeshCollider>(), PocTreeTarget);

            // ---- The authoritative POC NavMesh: an ENABLED surface over the big terrain (walkable island +
            //      climbable mountain), saved as a POC-specific asset so it ships in the standalone exe. ----
            BakePocNavMesh(grounds, ground);

            // ---- Re-seat the player onto the POC terrain surface (Author spawned it for the flat proxy) ----
            ReseatPlayerOnPocTerrain(ground);

            // Wire the POC verify capture to the player (it drives an orbit onto the mountain + measures perf).
            WirePocVerifyCapture();

            EditorSceneManager.SaveScene(scene, PocScenePath);
            Debug.Log("[poc-build] POC scene saved -> " + PocScenePath);

            // Register the POC scene as the ONLY enabled build scene — so BuildWindows ships the POC.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(PocScenePath, true) };
            Debug.Log("[poc-build] EditorBuildSettings scene -> " + PocScenePath);
            return scene;
        }

        // Dense-but-perf-honest forest target for the POC (the #1 finding: does the EXISTING low-poly gen +
        // static batching hold 60fps as the island scales — a real prop load is part of that question).
        // Scaled with the ISLAND AREA (86cahwx6w: 560 × (600/400)² = 1260) so the grown island keeps the
        // #226-approved forest DENSITY — the same 560 trees on 2.25× the land would read sparser than the
        // island the Sponsor judged. Perf-gated by the -perfProbe re-measure. default — Sponsor-soak tunes.
        public const int PocTreeTarget = 1260;

        // ---- Zone-D look for the POC (lighting + gradient skybox + warm fog + post) — mirrors
        //      WorldBootstrap's look setup WITHOUT calling WorldBootstrap (which rebuilds the start island). ----
        static void BuildPocLook(GameObject envRoot, Camera mainCam)
        {
            // Register the shared shaders as always-included so the standalone build does not strip them to
            // magenta (the spike lesson) — BEFORE the materials key off them.
            foreach (var name in new[] { "FarHorizon/LowPolyVertexColor", "FarHorizon/LowPolyWater",
                                          "Universal Render Pipeline/Lit" })
            {
                var sh = Shader.Find(name);
                if (sh != null) EnsureShaderAlwaysIncluded(sh);
            }

            // Warm directional key + cool ambient fill (the smooth-shading recipe, same values as
            // WorldBootstrap.BuildLighting). Remove any placeholder directional light first so ONE warm key lits.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.gameObject.name != "Sun")
                    Object.DestroyImmediate(l.gameObject);
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(envRoot.transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.93f, 0.80f);
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.60f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.50f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.30f, 0.24f);

            // Gradient skybox + skybox-driven ambient + warm global fog + post (the Zone-D quality pass).
            QualityPassGen.BuildGradientSkybox();
            QualityPassGen.EnableGlobalFog();
            QualityPassGen.BuildGlobalPostVolume();
            var postVol = GameObject.Find("ZoneD_PostVolume");
            if (postVol != null) postVol.transform.SetParent(envRoot.transform, false);
            QualityPassGen.EnableCameraPostProcessing(Camera.main);
            var gameCam = Camera.main;
            if (gameCam != null)
            {
                gameCam.clearFlags = CameraClearFlags.Skybox;
                // Far clip reaches past the far sea so the horizon is never frustum-clipped (fog dissolves it).
                gameCam.farClipPlane = Mathf.Max(gameCam.farClipPlane, 2200f);
            }
        }

        // The authoritative POC NavMesh: an ENABLED NavMeshSurface over ALL colliders (the big POC terrain +
        // mountain), fine voxel so the hill/mountain slopes resolve into one continuous walkable surface. Saved
        // as a POC-specific asset (NOT BootNavMesh/PlayNavMesh) so it ships in the standalone exe. Mirrors
        // WorldBootstrap.BakeNavMesh's whole-island bake (the runtime-live surface pattern).
        static void BakePocNavMesh(GameObject groundsRoot, GameObject ground)
        {
            var surface = groundsRoot.GetComponent<NavMeshSurface>();
            if (surface == null) surface = groundsRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.22f;    // fine enough for the big grid + mountain slopes; coarser than the
                                          // start island's 0.16 because the POC grid is bigger (perf-honest bake)
            surface.overrideTileSize = false;
            surface.BuildNavMesh();

            if (surface.navMeshData != null)
            {
                Directory.CreateDirectory(Path.GetFullPath("Assets/NavMesh"));
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(PocNavMeshPath);
                if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, PocNavMeshPath);
                else EditorUtility.CopySerialized(surface.navMeshData, existing);
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(surface);
                // ENABLED (unlike Author's disabled slab surface) — this IS the live POC walkable surface.
                surface.enabled = true;
                Debug.Log("[poc-trace] POC NavMesh baked + saved -> " + PocNavMeshPath +
                          " (voxel=0.22, collectAll, ENABLED — the live walkable surface)");
                TracePocNavCoverage();
            }
            else
            {
                Debug.LogError("[poc-trace] POC NavMesh bake produced NO data — the island would be un-walkable");
            }
        }

        // Coverage trace (the ground-truth that the agent can reach the WHOLE big island + up the mountain) —
        // sample NavMesh.SamplePosition across the land disc at the terrain surface Y + report the walkable
        // fraction, at bake time (the just-baked surface is live in the editor). Mirrors WorldBootstrap's trace.
        static void TracePocNavCoverage()
        {
            NextIslandPocGen.SeedOffset(PocSeed, out float ox, out float oz);
            const int rings = 12, azimuths = 16;
            int total = 0, covered = 0;
            float mtnHiCovered = 0f; // highest reachable Y (proves the mountain is climbable)
            for (int ri = 1; ri <= rings; ri++)
            {
                for (int ai = 0; ai < azimuths; ai++)
                {
                    float ang = ai / (float)azimuths * Mathf.PI * 2f;
                    float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                    float coast = NextIslandPocGen.ShoreRadiusAt(dx, dz, ox, oz);
                    float rr = (coast - (NextIslandPocGen.BeachWidth + 10f)) * ri / rings;
                    float x = dx * rr, z = dz * rr;
                    float y = NextIslandPocGen.HeightAtRadial(x, z, ox, oz);
                    total++;
                    if (NavMesh.SamplePosition(new Vector3(x, y, z), out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    {
                        covered++;
                        if (hit.position.y > mtnHiCovered) mtnHiCovered = hit.position.y;
                    }
                }
            }
            // Sample the mountain summit approach directly (a ring near the peak) so the climbable-peak claim is
            // measured, not inferred.
            float peakReach = 0f;
            for (int ai = 0; ai < 24; ai++)
            {
                float ang = ai / 24f * Mathf.PI * 2f;
                for (float rr = 10f; rr <= NextIslandPocGen.MtnFootRadius; rr += 20f)
                {
                    float x = NextIslandPocGen.MtnCenterX + Mathf.Cos(ang) * rr;
                    float z = NextIslandPocGen.MtnCenterZ + Mathf.Sin(ang) * rr;
                    float y = NextIslandPocGen.HeightAtRadial(x, z, ox, oz);
                    if (NavMesh.SamplePosition(new Vector3(x, y, z), out NavMeshHit h, 4f, NavMesh.AllAreas))
                        if (h.position.y > peakReach) peakReach = h.position.y;
                }
            }
            float pct = total > 0 ? 100f * covered / total : 0f;
            Debug.Log($"[poc-trace] NAVMESH COVERAGE: {covered}/{total} land samples walkable ({pct:F1}%) across the " +
                      $"~{NextIslandPocGen.MeanShoreR * 2f:F0}u island; highest reachable Y={mtnHiCovered:F1}u; " +
                      $"mountain reach up to Y={peakReach:F1}u (peakH ~{NextIslandPocGen.MtnPeakHeight:F0}u — the " +
                      "hero peak must be CLIMBABLE, not a wall).");
        }

        // Re-seat the player root onto the POC terrain surface at the spawn clearing (Author spawned it at
        // (0,0,6) for the flat proxy; the POC terrain plateau is ~0.2u, so a small raise + a NavMesh warp keeps
        // the agent on the baked surface). Placed at the flat spawn clearing near origin.
        static void ReseatPlayerOnPocTerrain(GameObject ground)
        {
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogWarning("[poc-trace] no Player to reseat"); return; }
            var col = ground.GetComponent<MeshCollider>();
            // The flat spawn clearing OPPOSITE the mountain (NextIslandPocGen.Spawn*) — NOT the origin (the mountain
            // foot blankets the origin → an 83u-up-the-flank spawn; run-2 fix). The player walks ACROSS toward the peak.
            Vector3 p = new Vector3(NextIslandPocGen.SpawnX, 0f, NextIslandPocGen.SpawnZ);
            if (col != null)
            {
                var ray = new Ray(new Vector3(p.x, 300f, p.z), Vector3.down);
                if (col.Raycast(ray, out RaycastHit hit, 600f)) p.y = hit.point.y;
            }
            player.transform.position = p;
            EditorUtility.SetDirty(player);
            Debug.Log($"[poc-trace] reseated Player onto POC terrain @ {p.ToString("F2")}");
        }

        static void WirePocVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            var player = GameObject.Find("Player");
            if (bootGo == null) return;
            var cap = bootGo.GetComponent<FarHorizon.PocIslandVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<FarHorizon.PocIslandVerifyCapture>();
            if (player != null) cap.player = player.GetComponent<ClickToMove>();
            EditorUtility.SetDirty(bootGo);
        }

        static void DestroyByName(string n)
        {
            var go = GameObject.Find(n);
            if (go != null) { Object.DestroyImmediate(go); Debug.Log("[poc-trace] destroyed: " + n); }
        }

        static void EnsureShaderAlwaysIncluded(Shader shader)
        {
            if (shader == null) return;
            var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
