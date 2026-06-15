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

        // ---- VISTA (Uma §2 near-vista + Erik far-vista 86ca8t9rh, CONSTRAINED per Sponsor soak of
        //      a89f508 / 86ca8t9pq reopened) ----
        //
        // SPONSOR SOAK-FIX (a89f508): the first impl built 4 FULL 360-degree concentric rings (62 peaks at
        // even angular spread, radii 240/360/520/1000u, all at y=0) — which (1) WALLED the entire horizon so
        // no open sky / clouds were visible, and (2) placed every peak far outside the ~45u island footprint
        // at y=0, i.e. rising out of the open SEA all around. The Sponsor verbatim: "The mountains are all
        // over (too many), I can't see any sky or clouds" + "Mountains should not be on the water but on
        // this island or other islands." This DELIBERATELY DEVIATES from Erik's full-encircling concentric-
        // ring recommendation (86ca8t9rh Route A): the rings are CONSTRAINED to discrete land/island
        // footprints, NOT a continuous horizon ring.
        //
        // NEW MODEL: a SPARSE set of DISCRETE mountain clusters ("islands"), each a tight little group of
        // peaks sharing a raised landmass base, placed at SPECIFIC azimuths with WIDE OPEN-SKY GAPS between
        // them so open sky dominates the upper frame and the clouds read. Far islands sit in the sea as
        // distinct landmasses (open sea between them — NOT a ring); one near range sits on THIS island
        // (behind the inland meadow). Total ~18 peaks (was 62), covering only a fraction of the horizon.
        // Static silhouette decoration: no collider, no NavMesh, shadow-casting OFF (far + flat-lit).
        static void BuildVista(GameObject envRoot, int seed)
        {
            var rnd = new System.Random(seed);
            var vistaRoot = new GameObject("Vista");
            vistaRoot.transform.SetParent(envRoot.transform, false);

            // ATMOSPHERIC FADE via the per-cluster _Tint (Uma §2): the tint LERPS the cluster toward the
            // horizon sky stop as it recedes — near = full saturation (tint ~white, the mesh's full grey-to-
            // snow contrast reads), far = tinted toward #DCE8E4 so the silhouette desaturates + lifts toward
            // the sky it dissolves into (the fade AND the seam-kill colour at the horizon line). Bound to the
            // single horizon-stop anchor (no drift).
            Color horizon = FarHorizon.WorldLookPalette.SkyHorizon;
            Color FadeTint(float k) => Color.Lerp(Color.white, horizon, Mathf.Clamp01(k)); // k=0 crisp .. 1 sky

            // Discrete cluster placements (centreAzimuthDeg measured CCW from +X; +Z is the inland/forward
            // arc the orbit cam looks toward, i.e. ~90deg). Each cluster is a small ISLAND landmass — peaks
            // grouped within a tight footprint, NOT spread around the whole ring. WIDE azimuth gaps between
            // clusters = open sky + open sea between the islands.
            //
            //   name, azimuthDeg, distance, peaks, baseR, height, snowline, body, snow, fadeK, raise
            //   raise = the landmass base lift (+y) so the cluster reads as LAND rising from the sea, not
            //           peaks poking straight out of the water (Sponsor fix #2). Near inland range sits on
            //           this island's far meadow (small lift); far sea-islands get a low landmass shelf.
            var clusters = new[]
            {
                // ON THIS ISLAND — a near range behind the inland meadow (the forward/inland arc, ~90deg).
                // Sits on land (the meadow rises to ~+1.6u inland; this range is just beyond it), modest
                // height so it backs the forest without walling the sky.
                new MtnCluster("Vista_Inland",   90f, 200f, 4, 55f,  85f,  0.60f, MtnBody,    MtnSnow,    0.10f, 2f),

                // FAR ISLANDS in the sea — a FEW distinct landmasses with WIDE open-sea/open-sky gaps. Spread
                // across the forward + side arcs only (NOT the full ring): the seaward-behind arc is left as
                // OPEN sea+sky so the orbit never sees a continuous wall.
                new MtnCluster("Vista_Island_NW", 130f, 430f, 3, 75f,  150f, 0.56f, MtnRimBody, MtnRimSnow, 0.45f, 4f),
                new MtnCluster("Vista_Island_NE",  55f, 470f, 3, 80f,  165f, 0.55f, MtnRimBody, MtnRimSnow, 0.50f, 4f),
                new MtnCluster("Vista_Island_E",    8f, 560f, 2, 95f,  175f, 0.54f, MtnRimBody, MtnRimSnow, 0.62f, 4f),
                new MtnCluster("Vista_Island_W",  168f, 540f, 2, 95f,  170f, 0.54f, MtnRimBody, MtnRimSnow, 0.62f, 4f),

                // ONE distant island near the horizon line (the endless-horizon read, Erik) — nearly the
                // horizon stop itself, reads as "almost sky", a single far landmass (not a ring).
                new MtnCluster("Vista_Far",       100f, 950f, 2, 150f, 240f, 0.52f, MtnRimBody, MtnRimSnow, 0.82f, 6f),
            };

            int total = 0;
            foreach (var c in clusters)
            {
                BuildMountainCluster(vistaRoot, c, FadeTint(c.fadeK), rnd);
                total += c.peaks;
            }
            Debug.Log($"[world-trace] BuildVista built {clusters.Length} DISCRETE island clusters " +
                      $"({total} peaks, was 62 full-ring) — open sky+sea between them, peaks raised onto land " +
                      "(Sponsor soak-fix: not a wall, not on the water)");
        }

        // One discrete mountain cluster ("island"): centre azimuth + distance + count + shape.
        struct MtnCluster
        {
            public string name; public float azimuthDeg, distance; public int peaks;
            public float baseR, height, snowline; public Color body, snow; public float fadeK, raise;
            public MtnCluster(string name, float azimuthDeg, float distance, int peaks, float baseR,
                float height, float snowline, Color body, Color snow, float fadeK, float raise)
            { this.name = name; this.azimuthDeg = azimuthDeg; this.distance = distance; this.peaks = peaks;
              this.baseR = baseR; this.height = height; this.snowline = snowline; this.body = body;
              this.snow = snow; this.fadeK = fadeK; this.raise = raise; }
        }

        // Build one DISCRETE cluster of faceted peaks grouped into a tight island footprint around a centre
        // point (NOT spread around a 360-degree ring). The peaks share a small angular+radial spread so they
        // overlap into one landmass silhouette, and the whole cluster is RAISED on its `raise` base lift so
        // it reads as LAND rising from the sea (Sponsor fix #2 — not poking straight out of the water). Each
        // peak faces inward toward the player. Per-cluster atmospheric _Tint; grey-to-snow baked in the mesh.
        static void BuildMountainCluster(GameObject parent, MtnCluster c, Color tint, System.Random rnd)
        {
            var clusterRoot = new GameObject(c.name);
            clusterRoot.transform.SetParent(parent.transform, false);
            var mat = MakeVertexColorMat("LPMtnMat_" + c.name, tint);

            float ca = c.azimuthDeg * Mathf.Deg2Rad;
            // Tight footprint: the peaks span a SMALL arc (so the cluster reads as one island, with open sky
            // either side), and a small radial spread (so a couple of peaks stack into a range, not a line).
            // The cluster's angular HALF-width shrinks with peak count so a 2-peak island stays compact.
            float arcHalf = (0.045f + 0.018f * c.peaks); // radians: ~3.6deg (2 peaks) .. ~6.8deg (4 peaks)

            for (int p = 0; p < c.peaks; p++)
            {
                // Spread peaks across the cluster's small arc + radial band (deterministic jitter).
                float t = c.peaks == 1 ? 0f : (p / (float)(c.peaks - 1) - 0.5f) * 2f; // -1..1 across the arc
                float a = ca + t * arcHalf + (float)(rnd.NextDouble() - 0.5) * 0.02f;
                float r = c.distance * (0.94f + (float)rnd.NextDouble() * 0.12f);
                float h = c.height * (0.82f + (float)rnd.NextDouble() * 0.40f);
                float br = c.baseR * (0.85f + (float)rnd.NextDouble() * 0.45f);
                // RAISE the cluster onto a landmass base (+y) so it reads as LAND, not water-piercing peaks.
                var pos = new Vector3(Mathf.Cos(a) * r, c.raise, Mathf.Sin(a) * r);

                var mesh = LowPolyMeshes.FacetedMountain(br, h, 6 + rnd.Next(0, 4), c.snowline,
                    c.body, c.snow, rnd.Next());

                var peak = new GameObject("LP_Mountain");
                peak.transform.SetParent(clusterRoot.transform, false);
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
                // Mark static so URP static-batches the cluster into 1-2 draw calls (Erik perf note).
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
