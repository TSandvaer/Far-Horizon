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
            // SLIGHTLY MORE clouds now the mountain wall is gone + sky opens (Sponsor soak #4: "I can't
            // see any sky or clouds" — fixing the mountains reveals the sky, so put a few more up there).
            int cMin = FarHorizon.WorldLookConfig.CloudCountMin, cMax = FarHorizon.WorldLookConfig.CloudCountMax;
            int count = cMin + rnd.Next(0, cMax - cMin + 1); // 6-10 (Uma §1: sparse, purposeful — eased up)

            // Single shared wind direction (Uma §1) — a gentle cross-play-space drift (mostly +X with a
            // touch of +Z). The drift band is centred on the play space; clouds wrap at +/- a half-span.
            var wind = new Vector3(1f, 0f, 0.18f).normalized;
            const float bandHalfSpan = 90f; // wide enough that the wrap is off-frame from the spawn

            // Spread the clouds across the sky dome above + around the play space (biased over + ahead so
            // the orbit cam catches them in the upper third). Altitude 30-60u (Uma §1).
            for (int c = 0; c < count; c++)
            {
                float radius = 4f + (float)rnd.NextDouble() * 5f; // long axis ~8-18u (radius 4-9) — Uma §1 band
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

        // ---- VISTA (Uma §2 near-vista + Erik far-vista 86ca8t9rh, CONSTRAINED per Sponsor soaks of
        //      a89f508 / 8fdfc1a / 4457d47, ticket 86ca8t9pq reopened twice) ----
        //
        // SOAK HISTORY: (a89f508) 4 FULL 360-degree rings (62 peaks) WALLED the horizon -> "I can't see
        // any sky or clouds" + "Mountains should not be on the water but on this island or other islands."
        // (8fdfc1a) constrained to ~18 peaks in discrete clusters -> open sky restored. (4457d47) STILL
        // "doesn't look good" -> "mountains render as FLOATING TRANSLUCENT shards."
        //
        // DIAGNOSIS-VIA-TRACE (Drew, this pass — Erik's surface-type + winding hypotheses BOTH REFUTED):
        //   - NOT Surface Type = Transparent: the mesh uses FarHorizon/LowPolyVertexColor, an OPAQUE shader
        //     (RenderType=Opaque, Queue=Geometry, frag alpha=1) — no transparency at the shader level.
        //   - NOT inverted winding: FacetedMountain.EmitFace enforces OUTWARD winding per-face, and the
        //     existing EditMode guard FacetedMountain_AllFacesPointOutward_NotBackfaceCulled is GREEN.
        //   - REAL CAUSE = DOUBLE-FADE. The far clusters were faded toward the horizon stop #DCE8E4 TWICE:
        //     once by the per-cluster _Tint (fadeK 0.45-0.82 -> mesh 45-82% sky-coloured BEFORE lighting),
        //     then AGAIN by the Exp^2 fog (colour == #DCE8E4) which blends 38% (430u) .. 90% (950u) by
        //     distance. Net ~70-95% horizon-coloured = faint, washed, see-through silhouettes floating with
        //     no grounding = "floating translucent shards." A LOOK/composition bug, not a winding/surface bug.
        //
        // FIX SHAPE (all live-dialable via the F9 WorldLookNudgeTool so the Sponsor bakes the final look):
        //   1. KILL THE DOUBLE-FADE: cap the per-cluster tint at WorldLookConfig.MtnFadeCap (default 0.25)
        //      so the MESH keeps its grey-to-snow contrast and FOG ALONE does the atmospheric recession.
        //   2. PULL THE CLUSTERS IN (distance x WorldLookConfig.MtnDistanceScale, default 0.55) so fog no
        //      longer ghosts them, and DROP the 950u Vista_Far (90% fog = pure waste; replaced by a visible
        //      mid-far island). Distances are within the fog's crisp-to-mid band.
        //   3. GROUND THEM ON VISIBLE LANDMASS BASES: each cluster gets a low faceted landmass shelf
        //      (BuildLandmassBase) extending below the peaks down past the sea surface, so the cluster reads
        //      as an ISLAND rising from the water, not peaks floating in mid-air.
        // Static silhouette decoration: no collider, no NavMesh, shadow-casting OFF (far + flat-lit).
        static void BuildVista(GameObject envRoot, int seed)
        {
            var rnd = new System.Random(seed);
            var vistaRoot = new GameObject("Vista");
            vistaRoot.transform.SetParent(envRoot.transform, false);

            // ATMOSPHERIC FADE via the per-cluster _Tint (Uma §2) — but CAPPED (Drew double-fade fix): the
            // tint lerps toward the horizon stop only up to MtnFadeCap so the mesh keeps a readable grey-to-
            // snow silhouette; the Exp^2 fog (colour == the same horizon stop) supplies the rest of the
            // atmospheric recession. Bound to the single horizon-stop anchor (no drift from the seam-kill).
            Color horizon = FarHorizon.WorldLookPalette.SkyHorizon;
            float fadeCap = FarHorizon.WorldLookConfig.MtnFadeCap;
            float distScale = FarHorizon.WorldLookConfig.MtnDistanceScale;
            Color FadeTint(float k) => Color.Lerp(Color.white, horizon, Mathf.Clamp01(k) * fadeCap);

            // Discrete cluster placements (centreAzimuthDeg measured CCW from +X; +Z is the inland/forward
            // arc the orbit cam looks toward, i.e. ~90deg). Each cluster is a small ISLAND landmass — peaks
            // grouped within a tight footprint, NOT spread around the whole ring. WIDE azimuth gaps between
            // clusters = open sky + open sea between the islands.
            //
            //   name, azimuthDeg, distance, peaks, baseR, height, snowline, body, snow, fadeK, raise
            //   raise = the landmass base lift (+y) of the peak group; BuildLandmassBase then extends a
            //           faceted shelf from raise DOWN past the sea so the cluster reads as grounded LAND.
            // CLUSTER PLACEMENT — the near-edge of EVERY island landmass base MUST clear the play space.
            // ROOT-CAUSE FIX (86ca8t9pq pale-frame regression, instrument-confirmed): the feeeaad landmass
            // bases (BuildLandmassBase, added to ground the "floating shards") were placed too CLOSE with too
            // LARGE a footprint — Vista_Inland (dist 200×0.55=110u, footprint ~118u radius, outer ~142u) had a
            // near-edge at worldZ ≈ -32, so its grey-blue dome DRAPED OVER the entire play terrain (X±45,
            // Z -12..56). At the gameplay orbit (pitch 55, dist 14) the camera saw the landmass dome, NOT the
            // sand/grass — the "pale void / floating water / elevated" percept the Sponsor hit repeatedly
            // (water-Y, sea-extent + occlusion all refuted; the -hideVista isolation proved the terrain
            // renders perfect sand/grass once the vista is hidden). The landmass bases have NO collider, so
            // the centre PHYSICS ray passed THROUGH them to Ground_Play — the camDiag hit was a red herring;
            // the RENDER ray hit the dome. FIX: push every island OUT + shrink footprints so the nearest
            // landmass near-edge is ≥ ~120u from origin (the play footprint reaches ~72u at the inland-forward
            // corner). Verified by the near-edge math: all clusters now clear the play space by ≥50u. (Bake-
            // time DEFAULTS; the F9 WorldLookNudgeTool still re-dials distance/scale live for the Sponsor.)
            var clusters = new[]
            {
                // ON THIS ISLAND'S BACKING RANGE — a modest range BEHIND the inland meadow (forward arc ~90deg),
                // grounded WELL beyond the play terrain (near-edge ~worldZ +128) so it backs the forest without
                // ever draping over the play space. Smaller footprint (baseR 42) so the near range stays compact.
                new MtnCluster("Vista_Inland",   90f, 430f, 4, 42f,  85f,  0.60f, MtnBody,    MtnSnow,    0.10f, 2f),

                // FAR ISLANDS in the sea — a FEW distinct landmasses with WIDE open-sea/open-sky gaps. Spread
                // across the forward + side arcs only (NOT the full ring): the seaward-behind arc is left as
                // OPEN sea+sky so the orbit never sees a continuous wall. Pushed OUT + footprints trimmed so
                // every near-edge clears the play space (the pale-frame fix); the Exp² fog gives the recession.
                new MtnCluster("Vista_Island_NW", 130f, 560f, 3, 70f,  150f, 0.56f, MtnBody,    MtnSnow,    0.35f, 6f),
                new MtnCluster("Vista_Island_NE",  55f, 600f, 3, 75f,  165f, 0.55f, MtnBody,    MtnSnow,    0.45f, 6f),
                new MtnCluster("Vista_Island_E",    8f, 700f, 2, 90f,  175f, 0.54f, MtnRimBody, MtnRimSnow, 0.62f, 8f),
                new MtnCluster("Vista_Island_W",  168f, 680f, 2, 90f,  170f, 0.54f, MtnRimBody, MtnRimSnow, 0.62f, 8f),
            };

            int total = 0;
            foreach (var c in clusters)
            {
                var cc = c;
                cc.distance = c.distance * distScale; // pull IN (double-fade fix) — out of the fog ghost band
                BuildMountainCluster(vistaRoot, cc, FadeTint(cc.fadeK), rnd);
                total += cc.peaks;
            }
            Debug.Log($"[world-trace] BuildVista built {clusters.Length} grounded island clusters " +
                      $"({total} peaks) — fadeCap={fadeCap:F2} distScale={distScale:F2} (double-fade KILLED: " +
                      "tint capped + clusters pulled in + landmass bases) — solid silhouettes, not shards");
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
        // overlap into one silhouette, and the whole cluster sits on a LANDMASS BASE (BuildLandmassBase: a
        // low faceted shelf from `raise` DOWN past the sea surface) so it reads as an ISLAND rising from the
        // water, NOT peaks floating in mid-air (the "floating shards" fix). Each peak faces inward toward the
        // player. Per-cluster atmospheric _Tint (capped — see BuildVista); grey-to-snow baked in the mesh.
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

            // The landmass base spans the whole peak footprint: take the cluster's mean direction + a
            // footprint radius covering the peak spread, and extend a faceted shelf from `raise` down past
            // the sea. Reads as the island the peaks stand on (the grounding fix).
            float meanR = c.distance;
            var clusterCentre = new Vector3(Mathf.Cos(ca) * meanR, 0f, Mathf.Sin(ca) * meanR);
            float footprintR = c.baseR * (1.15f + 0.25f * c.peaks); // covers the arc+radial peak spread
            BuildLandmassBase(clusterRoot, c, clusterCentre, footprintR, tint, mat, rnd);

            for (int p = 0; p < c.peaks; p++)
            {
                // Spread peaks across the cluster's small arc + radial band (deterministic jitter).
                float t = c.peaks == 1 ? 0f : (p / (float)(c.peaks - 1) - 0.5f) * 2f; // -1..1 across the arc
                float a = ca + t * arcHalf + (float)(rnd.NextDouble() - 0.5) * 0.02f;
                float r = c.distance * (0.94f + (float)rnd.NextDouble() * 0.12f);
                float h = c.height * (0.82f + (float)rnd.NextDouble() * 0.40f);
                float br = c.baseR * (0.85f + (float)rnd.NextDouble() * 0.45f);
                // Seat the peak ON the landmass base (+raise) so it rises from the island shelf, not water.
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

        // The LANDMASS BASE under a mountain cluster (Drew "floating shards" grounding fix): a low, broad
        // faceted island shelf that the peaks stand on. It rises from BELOW the sea surface (so the coast
        // is the waterline, never a gap under floating peaks) up to the cluster's `raise` height, with an
        // irregular faceted rim in the SAME hard-faceted language as the peaks/rocks. Tinted with the same
        // (capped) per-cluster atmospheric tint so it recedes in lockstep with its peaks.
        static void BuildLandmassBase(GameObject clusterRoot, MtnCluster c, Vector3 centre, float radius,
            Color tint, Material mat, System.Random rnd)
        {
            // The shelf top sits at `raise`; it sinks BELOW the sea (down to seaSink) so there's no visible
            // gap between the island and the water (the grounding read). A gentle dome top so peaks have a
            // believable foot, faceted sides down to a sunk rim.
            float top = c.raise;
            float seaSink = -8f; // well below WaterY (-0.20) so the coast is the waterline, no floating gap
            var mesh = LowPolyMeshes.FacetedLandmass(radius, top - seaSink, 9 + rnd.Next(0, 3),
                c.body, rnd.Next());

            var island = new GameObject("LP_Landmass");
            island.transform.SetParent(clusterRoot.transform, false);
            island.transform.position = new Vector3(centre.x, seaSink, centre.z);

            var mf = island.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = island.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.staticShadowCaster = false;
            GameObjectUtility.SetStaticEditorFlags(island,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
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
