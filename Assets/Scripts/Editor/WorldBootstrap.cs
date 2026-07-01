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
        // AC6 (86ca9qwr3 — give-him-the-knob): the island SEED drives the organic coast warp + cliff/beach
        // layout, so a different seed = a different island OUTLINE of the same character. The bootstrap bakes
        // LowPolyZoneGen.IslandSeed; it can be overridden at bootstrap time with `-islandSeed N` so the Sponsor's
        // chosen variant re-bakes without a source edit. Default is the Sponsor-pick once chosen (set in
        // LowPolyZoneGen.IslandSeed). 13001 kept as the legacy Zone-D parity seed for the non-island scatter.
        const int ZoneSeed = 13001; // the spike's Zone-D seed, for look parity with the approved pass

        // ---- SUN ORIENTATION (ticket 86cag25az — sun-lower, folded into #194) ----
        // The warm directional "Sun" key's rotation. Euler X = the sun's ELEVATION above the horizon (the
        // -35 yaw/azimuth does NOT change elevation). QualityPassGen.ResolveSunDirection reads -light.forward
        // off this same Sun, so the baked sky-material _SunDirection (the visual disk) and the shading light
        // stay consistent.
        // SPONSOR-ACCEPTED BAKE (soak of 55bde02, 2026-06-30): the Sponsor live-dialed elevation on the F10
        // WorldLookNudgeTool SUN target IN THE SHIPPED BUILD, looking out over the OCEAN horizon, and accepted
        // 18° — the disk reads VISIBLE + warm over the water at the gameplay over-shoulder framing. So the
        // dial-from history is settled here at 18°: the earlier 48° was overhead-only (never framed), and the
        // first dry-run 18° looked occluded ONLY in the dedicated sky_gameplay capture that yaws toward the
        // inland blob-canopy treeline — over the OCEAN azimuth there is no canopy, which is where the Sponsor
        // judged it. 25° was the intermediate dial-from; the Sponsor's live soak superseded it to 18°.
        public const float SunElevationDeg = 18f; // Sponsor-accepted (soak 55bde02): low warm sun, visible over the ocean; deg above horizon
        public const float SunAzimuthDeg   = -35f; // azimuth/yaw — unchanged (does not affect elevation)

        // ---- WORLD-LOOK POLISH palettes (ticket 86ca8t9pq — Uma world-look brief §1/§2) ----
        // CLOUD 3-value cyan (Uma §1 anchor swatches — warm-leaning cyan, NOT cold steel blue; all
        // sub-0.95 HDR-clamp-safe so the reduced-but-present bloom doesn't bloom-clip the bright caps).
        // CLOUD-CONTRAST SOAK-FIX (86ca8t9pq S2, Sponsor soak of fa9f1b1: "no clear indications of clouds").
        // The clouds DID ship + drift (trace-confirmed in W3) at a readable size/altitude — but their light-
        // cyan body (#8FD8E0) sat too CLOSE in value to the (now brighter, S2) cheerful blue sky, so they
        // washed into it (low contrast) + the warm post grade desaturated them further. FIX: push the cloud
        // BODY + CAP brighter toward a near-white sunlit puff (the board 21h10_44 / 21h16_13 clouds read as
        // bright WHITE-cyan puffs that POP against the blue), keeping R the smallest channel (cyan-leaning,
        // the CloudBlob multi-value guard) + every channel sub-1.0 (HDR-clamp). The shadow underside stays a
        // deeper teal so the chunky facets keep their top-lit/under-shadow value step (the 3-value read).
        static readonly Color CloudBody   = new Color(0.78f, 0.92f, 0.95f); // #C7EAF2 bright near-white cyan body (POPS on blue)
        static readonly Color CloudTop    = new Color(0.90f, 0.97f, 0.99f); // #E6F7FC brilliant top-lit cap (sub-1.0)
        static readonly Color CloudShadow = new Color(0.50f, 0.78f, 0.85f); // #80C7D9 teal underside (keeps the 3-value facet step)

        // NEAR-VISTA + FAR-RING mountain palette. WARMER CHUNKY-MOUNTAIN SOAK-FIX (86ca8t9pq W2, Sponsor
        // soak of b54482c: "mountains STILL not acceptable — they read as flat grey triangles. Improve the
        // STYLE per the board: more faceting / chunkier forms / snow caps / warmer color variation").
        // ROOT CAUSE (diagnose-via-trace, the b54482c horizon captures): the NEAR backing range (fadeK 0.10,
        // ~no atmospheric tint) rendered the RAW MtnBody (0.60,0.65,0.68) — a COLD grey-BLUE (B>G>R) that
        // reads as a flat slab against the warm sky, and the snow cap (MtnSnow) barely differed from the sky
        // so no grey-to-snow contrast showed. The board mountains (inspiration/21h16_13 + 21h12_49) are WARM
        // grey-BROWN at the base (R>=G>=B), with a BRIGHT near-white snow cap and clear facet-to-facet value
        // steps. FIX: (1) warm the body to a grey-brown (R>=G>=B, more saturated so facets vary), (2) keep
        // the snow a bright near-white cap that pops against the warm body, (3) the mesh-level snow band +
        // facet value-jitter are widened/boosted in LowPolyMeshes.FacetedMountain so the chunky facets read.
        // All sub-0.95 (HDR-clamp-safe). Live-dialable via the F9 WorldLookNudgeTool MOUNTAINS target
        // (colour/snow/faceting added this pass) so the Sponsor bakes the final look.
        // WARMTH STRENGTHENED (W2 attempt 2 — instrument-confirmed): the first warm bake (0.56,0.50,0.45)
        // STILL read cold blue-grey on-frame (pixel-sampled the shipped horizon capture: mountain landed
        // (96,108,120), B>R by ~0.09). ROOT CAUSE (diagnose-via-trace): the skybox-driven AMBIENT is now a
        // saturated BLUE (the W4 sky zenith 0.38,0.62,0.85) and the DISTANT flat-lit mountain is dominated by
        // that cool ambient, not the warm directional key — so a mild warm albedo gets dragged blue. FIX:
        // push MtnBody to a STRONG warm tan-brown (R-B = 0.24, was 0.11) so the warm albedo survives the cold
        // ambient and lands neutral-to-warm on-frame. Verified by re-sampling the shipped capture (R>=B).
        static readonly Color MtnBody     = new Color(0.62f, 0.50f, 0.38f); // #9E8061 strong warm tan-brown (survives the cold ambient)
        static readonly Color MtnSnow     = new Color(0.95f, 0.94f, 0.90f); // #F2F0E6 bright warm near-white snow cap (pops on warm body)
        static readonly Color MtnRimBody  = new Color(0.70f, 0.67f, 0.66f); // #B3ABA8 farthest range (warm-leaning neutral as it recedes)
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
            // The new TRANSPARENT depth-fade FOAM water shader (ticket 86caamnmb AC1) — pin it too or the
            // standalone build strips it to the URP/Lit fallback (no foam, no fog-cap, unity-conventions.md
            // §Build stripping). Registered BEFORE MakeWaterMaterial keys off it below.
            var waterShader = Shader.Find("FarHorizon/LowPolyWater");
            if (waterShader != null) EnsureShaderAlwaysIncluded(waterShader);
            else Debug.LogWarning("[WorldBootstrap] LowPolyWater shader not found at bootstrap");
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
            // AC6: the island shape is seeded by LowPolyZoneGen.IslandSeed, overridable at bootstrap with
            // `-islandSeed N` so the Sponsor's chosen variant re-bakes without a source edit.
            int islandSeed = ResolveIslandSeed();
            Debug.Log($"[world-trace] island shape seed = {islandSeed} (override via -islandSeed)");
            var zone = LowPolyZoneGen.BuildZone(grounds, "Play", ZoneMinX, ZoneMaxX,
                ShoreZ, InlandFarZ, islandSeed, vcMat, waterMat);

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
            BakeNavMesh(grounds, islandSeed);

            // ---- GROUND the freshwater pond IN ITS CARVED BOWL (ticket 86cadj4g7 — #130 re-soak) ----
            // The pond is authored by MovementCameraScene.Author at the assumed flat-ground Y=0, BEFORE this
            // terrain (and its bowl carve) exist. LowPolyZoneGen.HeightAtRadial now CARVES a recessed bowl at
            // the pond XZ (PondDepressionDelta), so the real terrain here is a basin: a floor ~0.55u below the
            // local plateau, walls sloping up to undisturbed grass. This positions the pond root so the WATER
            // SURFACE sits just below the original ground level (where the bowl rim is) while the carved FLOOR
            // sits below the water — the player wades in KNEE-DEEP (the NavMesh, baked onto the same carved
            // collider, follows the floor down; the water is above it). REPLACES the old LIFT (which raised the
            // water +0.10u ABOVE the terrain → the collar read as a raised lip casting a shadow + the water read
            // flush, not sunken — the #130 defect). The bowl solves occlusion NATURALLY (water in a visible
            // low spot, no lift). Grounded by RAYCAST against the carved Ground_Play collider, so it tracks any
            // future terrain/seed/bowl re-tune with no hardcoded height. Runs AFTER BakeNavMesh so the carved
            // mesh + collider exist to raycast (and the NavMesh has already baked the walkable bowl floor).
            GroundPondInBowl();

            Debug.Log("[WorldBootstrap] BuildEnvironment complete -> " + zone.ground.name);
            return envRoot;
        }

        // The KNEE-DEEP wade depth: the water surface sits this far ABOVE the carved BOWL FLOOR, so the player
        // standing on the floor (NavMesh agent Y follows it) is submerged knee-deep. ROUND 9 (Sponsor round-8
        // soak "step over the shore straight INTO knee-deep water"): this is the dispatch's "knee-deep 0.75u at
        // the centre" — the SUNK percept is now this DEPTH below the waterline. The bowl FLOOR is carved FloorDrop
        // (1.05 = RECESS 0.30 + WADE 0.75) below the plateau, so the water surface (floor + this wade) sits RECESS
        // (0.30u) BELOW the original ground level → still a clearly RECESSED/sunk pool, but shallow enough that the
        // dry shore lip rising back to the rim is a SHORT traversable step-over (fill ≈0.90 of the mouth, no
        // walkable dry slope). Sourced from the SHARED LowPolyZoneGen.PondWadeDepth so a re-tune moves the floor +
        // the grounding in lockstep. Foam is baked OFF (#130 "still pool") so there is no _FoamDistance flood
        // constraint anymore; the 0.75u wade depth dwarfs even a dialed-up light foam → bank-only, never a flood.
        const float PondWaterDepthAboveFloor = LowPolyZoneGen.PondWadeDepth; // 0.75 — shared with the bowl carve
        // The PondWater child's local Y inside the FreshwaterPond root (MovementCameraScene.PondSurfaceY). The
        // root grounding targets: water world Y = floorY + depth  ⇒  rootY = floorY + depth − localY.
        const float PondWaterLocalY = -0.06f;

        /// <summary>
        /// GROUND the freshwater pond in its CARVED BOWL (ticket 86cadj4g7 — #130 re-soak). The pond is authored
        /// (MovementCameraScene.Author) at the assumed flat Y=0 BEFORE this terrain exists. LowPolyZoneGen.
        /// HeightAtRadial now carves a recessed BOWL at the pond XZ (PondDepressionDelta), so the real terrain
        /// here is a basin. This positions the pond root so the water SURFACE sits PondWaterDepthAboveFloor
        /// above the carved bowl FLOOR — the player wades in KNEE-DEEP (the NavMesh, baked on the same carved
        /// collider, follows the floor down; the water is above it). REPLACES the old LIFT (which raised the
        /// water ABOVE the terrain → the collar read as a raised lip, the water read flush). Grounded by RAYCAST
        /// against the carved Ground_Play collider (the same surface the player walks + the NavMesh baked onto),
        /// so it tracks any terrain/seed/bowl re-tune with no hardcoded height. Runs AFTER BakeNavMesh so the
        /// carved mesh + collider exist to raycast. Idempotent: re-runs land at the same height.
        /// </summary>
        static void GroundPondInBowl()
        {
            var pond = Object.FindAnyObjectByType<FarHorizon.FreshwaterPond>(FindObjectsInactive.Include);
            if (pond == null)
            {
                Debug.LogWarning("[pond-bowl] no FreshwaterPond in scene — nothing to ground");
                return;
            }

            // The carved terrain surface: the Zone-D play ground (Ground_Play) carries a MeshCollider on the
            // Ground layer (BakeNavMesh ran on it). Raycast straight DOWN through the pond's XZ from well above
            // — this samples the carved BOWL FLOOR (the pond centre sits inside PondBowlInnerRadius, the flat
            // floor band), the same surface the NavMesh baked + the player's agent will stand on.
            var groundGo = GameObject.Find("Ground_Play");
            var col = groundGo != null ? groundGo.GetComponent<MeshCollider>() : null;
            if (col == null)
            {
                Debug.LogWarning("[pond-bowl] no Ground_Play MeshCollider to raycast — pond left at authored Y " +
                                 "(grounding skipped — investigate the terrain build order)");
                return;
            }

            Vector3 p = pond.transform.position;
            var ray = new Ray(new Vector3(p.x, 200f, p.z), Vector3.down);
            if (!col.Raycast(ray, out RaycastHit hit, 400f))
            {
                Debug.LogWarning($"[pond-bowl] terrain raycast MISSED at pond XZ ({p.x:F1},{p.z:F1}) — " +
                                 "pond left at authored Y (the bowl grounding would not apply)");
                return;
            }

            float floorY = hit.point.y;  // the carved bowl floor at the pond centre
            float targetRootY = floorY + PondWaterDepthAboveFloor - PondWaterLocalY;
            float beforeY = p.y;
            pond.transform.position = new Vector3(p.x, targetRootY, p.z);
            EditorUtility.SetDirty(pond.gameObject);

            float waterY = targetRootY + PondWaterLocalY;
            Debug.Log($"[pond-bowl] carved bowl floorY at pond ({p.x:F1},{p.z:F1}) = {floorY:F3}; " +
                      $"set pond root Y {beforeY:F3} -> {targetRootY:F3} " +
                      $"(water surface world Y = {waterY:F3} = floor + {PondWaterDepthAboveFloor:F2} knee-deep " +
                      $"depth) — the pool sits RECESSED in the bowl; the player wades in knee-deep (ticket 86cadj4g7)");
        }

        // Warm directional key (low ~18° sun, Sponsor-accepted) models the sun; cool ambient fill keeps
        // shadowed facets from going black. The warm-key / cool-fill contrast over the averaged-normal
        // low-poly geometry is the primary driver of the "smooth-shaded looks good" read. (QualityPassGen.BuildGradientSkybox
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
            // SUN-LOWER (ticket 86cag25az — folded into the #194 sky PR). The disk was baked at elevation 48°
            // (Euler X); the gameplay over-shoulder orbit (default pitch 55 looking DOWN, clamped to [8,70])
            // physically can't tilt up far enough to frame a sun that high — so the Sponsor's first #194 soak
            // saw the warm-gold disk only in the dedicated -verifySky shot, never in normal play ("baked too
            // high to see"). LOWER the elevation to the SPONSOR-ACCEPTED 18° (Euler X; yaw/azimuth -35 unchanged)
            // so the disk sits in the low warm band the orbit frames when the player looks toward the HORIZON
            // over the OCEAN — the far-horizon north-star framing (the Sponsor live-dialed + accepted 18° on the
            // 55bde02 soak, sun visible + warm over the water). The Euler X IS the sun's elevation above the
            // horizon (the -35 yaw doesn't change elevation — verified). This is the CAUSE-level fix (the disk
            // was where the LIGHT pointed, just too high): lowering the actual Sun light lowers BOTH the shading
            // direction AND the baked _SunDirection (QualityPassGen.ResolveSunDirection reads -light.forward off
            // THIS light), so the visual disk and the light stay consistent — a low warm sun reads as a coherent
            // late-afternoon key (longer warm shadows), suiting the warm Zone-D palette.
            sunGo.transform.rotation = Quaternion.Euler(SunElevationDeg, SunAzimuthDeg, 0f);
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

            // CLOUDS-VISIBLE SOAK-FIX (86ca8t9pq W3, Sponsor soak of b54482c: "clouds NOT visible — make the
            // chunky faceted clouds actually show — lower / bigger / more / repositioned into the visible sky
            // per the board"). ROOT CAUSE (diagnose-via-trace, the b54482c run log + the default/drift caps):
            // the clouds DID ship (6 in scene, trace-confirmed drifting) but at altitude 42-58u they sat ABOVE
            // the default gameplay orbit's framing (pitch 55 looks slightly DOWN over the shoulder) — only tiny
            // specks reached the top frame edge. The board (21h16_13/21h13_31) puts chunky clouds LOWER in the
            // sky band where they read at orbit distance. FIX: (1) LOWER the altitude band (26-42u, was 30-60u
            // — still > the 25u eye-line floor the scene test guards), (2) BIGGER blobs (radius 5-9.5 -> long
            // axis ~10-19u, within the 6-22u scale-test band), (3) MORE of them (the 6-10 count is already eased
            // up), (4) BIAS the lateral spread tighter over + ahead of the play space (the forward/inland arc
            // the orbit faces) so the clouds land IN the visible sky, not off behind the camera.
            for (int c = 0; c < count; c++)
            {
                float radius = 5f + (float)rnd.NextDouble() * 2.5f; // long axis ~11-18u (radius 5-7.5) — kept <22u (scale-test ceiling)
                int blobs = 5 + rnd.Next(0, 2);                     // 5-6 spheroids (chunkier board read; S2 — fuller puffs)
                var mesh = LowPolyMeshes.CloudBlob(radius, blobs, CloudBody, CloudTop, CloudShadow, rnd.Next());

                var cloud = new GameObject("LP_Cloud");
                cloud.transform.SetParent(cloudsRoot.transform, false);
                // Tighter lateral spread, biased over + AHEAD of the play space (the inland +Z arc the orbit
                // faces) so the clouds sit in the VISIBLE sky band, not scattered off behind the camera.
                float px = Mathf.Lerp(-75f, 75f, (float)rnd.NextDouble());
                float pz = Mathf.Lerp(-10f, 95f, (float)rnd.NextDouble());
                float py = 28f + (float)rnd.NextDouble() * 14f; // 28-42u altitude (>25u eye-line floor; into the visible sky band)
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
            Debug.Log($"[world-trace] BuildClouds placed {count} clouds (wind={wind}, alt 28-42u, " +
                      $"bright near-white cyan body S2 contrast-fix, drift 0.22-0.48 u/s)");
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
            // BIG ROUND ISLAND (86ca9a7qn): the main island is now a MUCH bigger round landmass
            // (IslandShoreR 120u). The Sponsor wants "the mountains [to] sit on other islands" — so EVERY
            // mountain cluster is a SEPARATE island in the surrounding sea, its near-edge clear of the main
            // island's coast (≥ IslandShoreR + margin). Water is on ALL sides now, so the islands can RING
            // more of the horizon (spread across all azimuths) while keeping WIDE open-sky/open-sea gaps so
            // the horizon is never a continuous wall (the Sponsor's earlier "can't see sky" complaint). The
            // Exp² fog supplies the atmospheric recession; the per-cluster tint is capped (double-fade fix).
            // Distances are pushed OUT past the new island (was 430-700u for the old small strip) so the
            // nearest landmass near-edge clears the 120u coast + ~footprint with margin.
            var clusters = new[]
            {
                // Distinct mountain ISLANDS ringing the sea around the big round island — spread across all
                // azimuths (water on all sides), each a separate landmass clearly OFF the main island. WIDE
                // azimuth gaps between them keep the horizon open (sky + open sea show between the islands).
                //   name, azimuthDeg, distance, peaks, baseR, height, snowline, body, snow, fadeK, raise
                new MtnCluster("Vista_Island_N",   90f, 620f, 4, 70f, 150f, 0.42f, MtnBody,    MtnSnow,    0.18f, 4f),
                new MtnCluster("Vista_Island_NE",  40f, 700f, 3, 80f, 165f, 0.45f, MtnBody,    MtnSnow,    0.35f, 6f),
                new MtnCluster("Vista_Island_E",   -8f, 820f, 3, 90f, 175f, 0.44f, MtnRimBody, MtnRimSnow, 0.55f, 8f),
                new MtnCluster("Vista_Island_SW", 215f, 760f, 2, 85f, 160f, 0.44f, MtnRimBody, MtnRimSnow, 0.55f, 8f),
                new MtnCluster("Vista_Island_W",  160f, 700f, 3, 80f, 165f, 0.45f, MtnBody,    MtnSnow,    0.45f, 6f),
            };

            int total = 0;
            float minClearance = float.PositiveInfinity;
            string nearestName = "";
            foreach (var c in clusters)
            {
                var cc = c;
                cc.distance = c.distance * distScale; // pull IN (double-fade fix) — out of the fog ghost band
                BuildMountainCluster(vistaRoot, cc, FadeTint(cc.fadeK), rnd);
                total += cc.peaks;
                // PROOF (86ca9a7qn): each mountain ISLAND must sit OFF the main round island. The island's
                // footprint radius ≈ baseR*(1.15+0.25*peaks); its near-edge = distance − footprintR. Report the
                // clearance from the main island coast (IslandShoreR) — must be POSITIVE (a separate island).
                float footprintR = cc.baseR * (1.15f + 0.25f * cc.peaks);
                float nearEdge = cc.distance - footprintR;
                float clearance = nearEdge - LowPolyZoneGen.IslandShoreR;
                if (clearance < minClearance) { minClearance = clearance; nearestName = cc.name; }
                Debug.Log($"[world-trace]   {cc.name}: dist={cc.distance:F0}u footprintR={footprintR:F0}u " +
                          $"nearEdge={nearEdge:F0}u clearance-from-main-coast={clearance:F0}u");
            }
            Debug.Log($"[world-trace] BuildVista built {clusters.Length} SEPARATE mountain islands ({total} peaks) " +
                      $"ringing the round-island sea — nearest near-edge clearance {minClearance:F0}u ({nearestName}); " +
                      $"all OFF the main island (coast r={LowPolyZoneGen.IslandShoreR:F0}u). fadeCap={fadeCap:F2} " +
                      $"distScale={distScale:F2} (double-fade capped + landmass bases) — solid silhouettes, not shards");
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

                // MORE SIDES (S3 detail): 9-12 radial columns (was 6-9) so the multi-ring ridge lines +
                // stepped rockface read as a detailed low-poly peak, not a coarse pyramid.
                var mesh = LowPolyMeshes.FacetedMountain(br, h, 9 + rnd.Next(0, 4), c.snowline,
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
        //
        // BIG ROUND ISLAND N1 ("can't walk everywhere", 86ca9a7qn). This bake is the AUTHORITATIVE
        // whole-island NavMesh: it runs AFTER LowPolyZoneGen.BuildZone has built the sloped island terrain
        // collider (Ground_Play), collects ALL physics colliders (NOT layer-restricted), and bakes with a
        // FINE pinned voxel so the 330u island grid + the hill slopes + the radial foreshore dip resolve
        // cleanly into ONE continuous walkable surface. The agent can then path UP/DOWN/ACROSS the hills, not
        // just the flat centre. (The island's max slope ~33deg — slope-probed — sits under the default agent
        // maxSlope 45deg, so the default agent type already covers every hill; the partial coverage was the
        // OLD flat-slab-only bake, not a slope limit.) A coverage trace dumps the walkable fraction sampled
        // across the disc so the N1 fix is readable from the log, not just judged by eye.
        static void BakeNavMesh(GameObject groundsRoot, int islandSeed)
        {
            var surface = groundsRoot.GetComponent<NavMeshSurface>();
            if (surface == null) surface = groundsRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // Pin a fine voxel so the big island grid + hill slopes resolve into one continuous surface
            // (NavMeshSurface reads slope/climb from the agent type — the default 45deg covers the ~33deg hills).
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.16f;
            surface.overrideTileSize = false;
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
                Debug.Log("[WorldBootstrap] NavMesh baked + saved -> " + navPath + " (voxel=0.16, collectAll)");
                TraceNavMeshCoverage(islandSeed);
            }
            else
            {
                Debug.LogWarning("[WorldBootstrap] NavMesh bake produced no data");
            }
        }

        // COVERAGE TRACE (BIG ROUND ISLAND N1 instrument, 86ca9a7qn). Sample NavMesh.SamplePosition across the
        // walkable land disc (rings × azimuths) at the terrain surface Y, and report the walkable fraction +
        // the worst uncovered ring. This is the ground-truth check that the agent can reach the WHOLE island —
        // run at bake time (the just-baked surface is live in the editor's navigation system) so the N1 fix is
        // a measured number in the bootstrap log, not an eyeball judgement. (Trees carve NavMeshObstacles at
        // RUNTIME — they don't subtract from this static bake — so the static fraction is the reachable land.)
        static void TraceNavMeshCoverage(int islandSeed)
        {
            const int rings = 12, azimuths = 16;
            LowPolyZoneGen.SeedOffset(islandSeed, out float ox, out float oz); // SAME warp offset as the terrain
            int total = 0, covered = 0;
            float worstUncoveredRing = -1f; int worstRingMisses = 0;
            for (int ri = 1; ri <= rings; ri++)
            {
                int ringMiss = 0;
                for (int ai = 0; ai < azimuths; ai++)
                {
                    float ang = ai / (float)azimuths * Mathf.PI * 2f;
                    float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                    // Sample the WALKABLE land at this azimuth: ring fraction of the WARPED coast minus the
                    // coastal fringe (so we sample real grass, not the beach strip / sea in a bay).
                    float coast = LowPolyZoneGen.ShoreRadiusAt(dx, dz, ox, oz);
                    float rr = (coast - (LowPolyZoneGen.BeachWidth + 4f)) * ri / rings;
                    float x = dx * rr, z = dz * rr;
                    float y = LowPolyZoneGen.HeightAtRadial(x, z, ox, oz);
                    total++;
                    if (UnityEngine.AI.NavMesh.SamplePosition(new Vector3(x, y, z),
                            out _, 3f, UnityEngine.AI.NavMesh.AllAreas))
                        covered++;
                    else
                        ringMiss++;
                }
                if (ringMiss > worstRingMisses) { worstRingMisses = ringMiss; worstUncoveredRing = ri / (float)rings; }
            }
            float pct = total > 0 ? 100f * covered / total : 0f;
            Debug.Log($"[world-trace] NAVMESH COVERAGE: {covered}/{total} land samples walkable ({pct:F1}%) " +
                      $"across the ORGANIC island (rings 1..{rings} × {azimuths} azimuths, sampled inside the warped coast). " +
                      $"worst ring fraction={worstUncoveredRing:F2} missed {worstRingMisses}/{azimuths}. " +
                      "N1: the agent must reach the WHOLE island (≥90% expected post-fix).");
        }

        // AC6 (86ca9qwr3): the island shape seed. Defaults to LowPolyZoneGen.IslandSeed (the Sponsor's pick
        // once chosen); overridable at bootstrap with `-islandSeed N` so seed VARIANTS can be baked + captured
        // for the Sponsor to choose, and the chosen one re-baked, without a source edit each time.
        static int ResolveIslandSeed()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-islandSeed" && int.TryParse(args[i + 1], out int s)) return s;
            return LowPolyZoneGen.IslandSeed;
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
