using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// One-shot batchmode bootstrap for the Far Horizon project. Establishes the URP render
    /// pipeline (a bare -createProject yields a BUILT-IN-RP project — the URP asset has to be
    /// created + assigned to GraphicsSettings in code, per the spike's FINDINGS.txt), sets
    /// ProductName / CompanyName = "Far Horizon", authors the minimal Boot scene from code,
    /// registers it in EditorBuildSettings, and writes the build stamp.
    ///
    /// Run via:
    ///   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.BootstrapProject.Run
    ///
    /// Idempotent: re-running overwrites the URP assets + scene in place. This is the production
    /// analogue of the spike's SliceBootstrap — but minimal (a clean boot scene only, no slice
    /// gameplay), since the systems get ported deliberately in later M-U1 tickets.
    /// </summary>
    public static class BootstrapProject
    {
        private const string SettingsDir = "Assets/Settings";
        private const string ResourcesDir = "Assets/Resources";
        private const string ScenesDir = "Assets/Scenes";
        private const string UrpAssetPath = SettingsDir + "/FarHorizonURP.asset";

        // AC0 LINE FIX (86ca9qwr3): main-light shadow distance pushed past the visible play area so the
        // shadow-distance boundary never lands in the framed grass; a wider cascade-border fade softens the
        // boundary if it ever does. See ConfigureUrp for the full trace-diagnosis rationale.
        // REGRESSION FIX (86caarn6y): 160 was sized for the spawn-locked play area, but WASD (#63) + run (#68)
        // now let the player roam the full ~240u-diameter island (IslandShoreR 120). Orbiting back across the
        // island puts the far shore beyond 160u of the camera eye, re-entering the boundary ring into visible
        // grass.
        // PERSISTENT-FLICKER FIX (86caayvfz, 2nd pass): 4 cascades killed the NEAR-field crawl but the Sponsor
        // STILL saw flicker (build a5282c6: "still present, maybe too aggressive"). Diagnosis: the 360u distance
        // forces every cascade to stretch thin -> the FAR cascades (cascade 2/3 spanning ~168u..360u) still sit
        // at only ~5 texels/unit, so mid/far shadow edges crawl no matter how dense the near cascade is. More
        // cascades cannot fix the far field at 360u — the budget is simply spread over too much world. The
        // Sponsor's "too aggressive" hint is the real lever: combine (1) SOFT (filtered) shadows so the edge is
        // blurred across texels and per-frame texel-quantization stops reading as flicker, (2) a GENTLER band fix
        // — back the distance OFF to 220u (still clears the gameplay-framed grass + roam reach) and let a WIDER
        // cascade-border (0.5) fade the boundary softly in the far distance where the Exp2 fog already hides it,
        // instead of shoving distance past the whole island, and (3) a denser 4096 shadow map. 220u (vs 360u)
        // is also 1.6x denser per cascade everywhere, independently cutting crawl. This is the "fade the band
        // gently instead of pushing distance huge" approach the ticket invited.
        public const float ShadowDistanceU = 220f;     // was 360 (band-clear-by-distance); 160/50 earlier
        public const float ShadowCascadeBorder = 0.5f; // wider soft fade so the boundary never reads as a hard band

        // 4 cascades stay: the near cascade still carries the densest texels (the near-field crawl fix from the
        // 1st pass). With distance backed to 220u the splits cover: cascade0 0..14.7u, c1 ..44u, c2 ..103u,
        // c3 ..220u — every cascade is denser than at 360u. Splits are the URP 4-cascade defaults.
        public const int ShadowCascadeCount = 4;
        public static readonly Vector3 ShadowCascade4Split = new Vector3(0.067f, 0.2f, 0.467f); // URP 4-cascade default

        // SOFT (filtered) main-light shadows: the 7x7 tent filter (m_SoftShadowQuality 2, already High) blurs the
        // shadow edge across multiple texels so the per-frame texel snap that reads as "flicker" is averaged out
        // — the direct flicker kill the cascade work alone could not deliver, and it reads SOFTER ("less
        // aggressive" per the Sponsor). m_SoftShadowsSupported is serialized-only (the public supportsSoftShadows
        // is get-only), so it's set via SerializedObject in ConfigureUrp, the same pattern the project uses for
        // m_AlwaysIncludedShaders. Cost: a wider PCF tap on one directional light — negligible static-world cost.
        public const bool SoftShadows = true;
        // Denser shadow map: 4096 doubles linear texel density across ALL cascades vs 2048 (the brute density
        // lever, cheap for ONE static directional light with no additional-light shadows). Backs up the
        // distance-reduction + soft filter so far cascades also hold up.
        public const int MainLightShadowmapResolution = 4096; // was 2048
        private const string UrpRendererPath = SettingsDir + "/FarHorizonRenderer.asset";
        private const string BootScenePath = ScenesDir + "/Boot.unity";
        private const string BuildStampPath = ResourcesDir + "/BuildStamp.txt";

        public static void Run()
        {
            Debug.Log("[BootstrapProject] start");
            EnsureDirs();
            SetProjectIdentity();
            ConfigureUrp();
            // U6 (86ca86fz9): configure the castaway FBX importer (Generic+CreateFromThisModel,
            // height-normalize, loop Idle/Walk) + build the Idle<->Walk AnimatorController BEFORE the
            // scene is authored, so BuildBootScene's player build (MovementCameraScene.BuildPlayer)
            // can instantiate + serialize the avatar's SkinnedMeshRenderer/bones/controller.
            CharacterAssetGen.PrepareCharacter();
            // Ticket 86cabh907 (Route A weapon SET): import the IN-HOUSE re-made knapped-flint axe + the
            // matched knife/sword/spear, wire the shared palette material, build the
            // Resources/WeaponSetLineup.prefab that WeaponSetVerifyCapture loads for the shipped-build
            // capture. The in-house flint axe REPLACES the retired CC-BY Sketchfab axe (Viktor.G) for the
            // held / stump / pickup gameplay axe — AxeAssetGen + the CastawayAxe asset + its CC-BY license
            // are removed in this PR (attribution obligation retired). Runs BEFORE the scene is authored so
            // MovementCameraScene.AttachHeroAxeToHand can parent the imported flint axe under the hand bone.
            WeaponPackAssetGen.PrepareWeaponPack();
            WriteBuildStamp("zoned");
            var scene = BuildBootScene();

            // U5 (ticket 86ca86fux): layer the Sponsor-approved Zone-D production environment
            // (terrain / scatter / water / lighting / fog / post / skybox) onto the boot scene as an
            // additive "Environment" root, then re-save so it ships in the build. Built AFTER the boot
            // scene exists so Camera.main is available for camera post-processing. ENVIRONMENT-only —
            // the player/camera/input systems land under their own root (U3 + the U6 castaway avatar,
            // authored inside BuildBootScene -> MovementCameraScene.Author). This combined boot scene
            // now carries the full first-slice: Zone-D environment + orbit camera + castaway player.
            WorldBootstrap.BuildEnvironment();

            // CHANGE (a) 86caa4c5c — wire the ChopTree's scatterRoot ref now that WorldBootstrap.BuildEnvironment
            // has authored the LowPolyScatter root (it did NOT exist at BuildChopTree time — the boot scene's
            // player/craft/chop are authored at line 96, the environment scatter only lands here). With it
            // serialized, EVERY scatter LP_Tree is choppable in the shipped build (the chop resolves the
            // nearest in-range tree, AC5) without the runtime GameObject.Find fallback. A READ-only ref — the
            // seed-42 scatter itself is untouched. The Start()-time name-scan remains the build-safety net.
            MovementCameraScene.WireChopScatterRoot();

            // F3 FIX (86caa4c96 / Devon REQUEST_CHANGES) — UNIFY to ONE StoneRespawner now that BOTH respawner
            // authoring sites have run: BuildWiredStone (pre-scatter, BuildBootScene) used to add a player-side
            // respawner binding nothing, and ScatterIslandProps (here, inside BuildEnvironment) added the
            // scatter-root one the 70 stones bind. Two respawners => SettingsPanel.FindObjectOfType picked one
            // arbitrarily => the `stone respawn time` slider was a DEAD KNOB. WireStoneScatterRoot canonicalises
            // to the scatter-bound respawner, destroys any stray, and points the SettingsPanel + the wired stone
            // at it — exactly ONE survives + the slider drives the SAME instance the scatter stones read. Mirrors
            // the WireChopScatterRoot post-scatter wiring above (same pre/post ordering, same READ-only contract).
            MovementCameraScene.WireStoneScatterRoot();

            // 86cabn67w (Devon NIT #1) — wire the SettingsPanel.berryBushes array EDITOR-TIME now that BOTH
            // bush-authoring sites have run: BuildBerryBush (the fixed-position wired bush, in BuildBootScene
            // AFTER BuildSettingsPanel) and WorldBootstrap.BuildEnvironment (the ~32 scatter LP_BerryBush
            // instances, just above). At BuildSettingsPanel time NO bush existed to serialize, so the panel
            // shipped relying ONLY on the runtime Awake FindObjectsByType fallback (the same runtime-Find shape
            // that went DEAD for the stone-respawner). This serializes the full bush set so the `Berry regrowth
            // time` row's fan-out reaches ALL bushes in the shipped build via the editor-time ship-path. Mirrors
            // the WireChopScatterRoot / WireStoneScatterRoot post-scatter wiring (same pre/post ordering); UNLIKE
            // stones there is no canonicalisation — it only COLLECTS + serializes (READ/wire-only; seed-42 untouched).
            MovementCameraScene.WireBerryBushes();

            // 86caber95 AC2 — back-wire SettingsPanel.worldLook to the WorldLookTunables seam now that BOTH
            // exist: the panel was authored in BuildBootScene (line ~96, BEFORE the environment), the seam was
            // added onto hudGo above (during BuildEnvironment). Serializes the ref so the F10-migrated fog/sky/
            // cloud/mountain/sun rows ship LIVE without a runtime FindObjectOfType (the editor-vs-runtime ship-
            // path discipline; the Awake fallback stays the bare-scene safety net). Mirrors WireStoneScatterRoot.
            MovementCameraScene.WireWorldLookConsole();

            EditorSceneManager.SaveScene(scene, BootScenePath);
            Debug.Log("[BootstrapProject] Zone-D environment built + boot scene re-saved (full slice: env + castaway player)");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BootstrapProject] complete");
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        private static void EnsureDirs()
        {
            foreach (var d in new[] { SettingsDir, ResourcesDir, ScenesDir })
                Directory.CreateDirectory(Path.GetFullPath(d));
            AssetDatabase.Refresh();
        }

        private static void SetProjectIdentity()
        {
            PlayerSettings.productName = "Far Horizon";
            PlayerSettings.companyName = "Far Horizon";
            Debug.Log("[BootstrapProject] productName/companyName = Far Horizon");
        }

        // The spike's load-bearing URP step: bare -createProject produces a built-in-RP project,
        // so we create a URP asset + a Universal renderer and assign them to GraphicsSettings +
        // every QualitySettings level, exactly as the spike did.
        private static void ConfigureUrp()
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            // DEPTH-FADE FOAM PREREQUISITE (ticket 86caamnmb — blocker 3, trace-diagnosed from CI
            // test-results-editmode.xml ActiveUrpAsset failure + Assets/Settings/FarHorizonRenderer.asset
            // m_CopyDepthMode:1). The transparent LowPolyWater frag samples the resolved OPAQUE scene depth
            // (_CameraDepthTexture) DURING the transparent pass to find water↔object intersections (foam).
            // A FRESH UniversalRendererData defaults CopyDepthMode to AfterTransparents, which copies depth
            // AFTER the water has drawn -> the foam samples STALE/EMPTY depth and reads nothing. Force
            // AfterOpaques (0) so the depth copy holds resolved OPAQUE depth while the transparent water draws.
            // Set HERE (not on the committed .asset) because bootstrap RE-CREATES FarHorizonRenderer.asset
            // from a default instance every run — a hand-edit to the committed asset is silently reverted.
            renderer.copyDepthMode = CopyDepthMode.AfterOpaques;
            AssetDatabase.CreateAsset(renderer, UrpRendererPath);

            var urp = UniversalRenderPipelineAsset.Create(renderer);
            // DEPTH-FADE FOAM PREREQUISITE (ticket 86caamnmb — blocker 2, trace-diagnosed from CI
            // ActiveUrpAsset_HasDepthAndOpaqueTextureEnabled failure: Expected True/was False). URP only
            // generates _CameraDepthTexture (+ _CameraOpaqueTexture) when the URP Asset requests them, and
            // UniversalRenderPipelineAsset.Create ships them OFF by default. Without Depth Texture the
            // transparent foam frag's SampleSceneDepth reads garbage -> foam-less water silently ships.
            // Set HERE (not on the committed FarHorizonURP.asset) because bootstrap RE-CREATES the URP asset
            // from Create() every run, wiping any committed m_RequireDepthTexture edit — the same
            // "bake reproducibly in bootstrap, not a hand-edit that reverts" pattern as the shadow params below.
            urp.supportsCameraDepthTexture = true;
            // R1 DEAD-COST REMOVAL (ticket 86cahhff6, plan §5 Tier-1 item 3). The full-screen OPAQUE-texture
            // copy (_CameraOpaqueTexture) is generated + downsampled every shipped frame, but NOTHING samples it:
            // a repo-wide grep of Assets/ for `_CameraOpaqueTexture` finds only this setter (+ its comment) and
            // the LowPolyWaterShaderTests pin — LowPolyWater.shader reads only scene DEPTH, no opaque colour /
            // refraction. So the copy is pure per-frame bandwidth waste. Turn it OFF. Depth Texture stays ON
            // (foam samples _CameraDepthTexture — the Sponsor-praised depth-fade foam). Set HERE (not on the
            // committed asset) because bootstrap RE-CREATES FarHorizonURP.asset from Create() each run.
            urp.supportsCameraOpaqueTexture = false;
            // S3 (ticket 86cahhff6, plan §5 Tier-1 item 12 hygiene rider). Clear m_UseAdaptivePerformance: the
            // Adaptive Performance provider is a mobile/thermal-throttling subsystem (URP defaults the flag ON),
            // a no-op on the Windows desktop target and never wired here — clearing it is intent clarity, not a
            // behaviour change. Public settable property (UniversalRenderPipelineAsset.useAdaptivePerformance);
            // set HERE so it bakes into FarHorizonURP.asset reproducibly (a committed-asset edit would revert).
            urp.useAdaptivePerformance = false;
            // AC0 "LINE THROUGH THE ISLAND" FIX (ticket 86ca9qwr3 — trace-diagnosed). The dead-straight
            // world-fixed dark streak the Sponsor flagged is the URP MAIN-LIGHT REAL-TIME SHADOW-DISTANCE
            // BOUNDARY: directional shadows render only within shadowDistance of the camera, and the hard
            // edge where shadow-receiving stops paints a dark band on the terrain (a large-radius arc that
            // reads as a straight line at island scale). The default UniversalRenderPipelineAsset.Create
            // ships shadowDistance=50 — SMALLER than the grass the gameplay orbit frames, so the boundary
            // lands IN the visible play area. (Proof: -diagLine isolation probe — line survives hiding the
            // scatter, vanishes with -noShadows, MOVES with -shadowDist, and TRACKS the camera when it pans,
            // i.e. a camera-relative ring that reads "world-fixed" because the orbit stayed over spawn.)
            // REGRESSION (86caarn6y): 160 cleared the spawn-locked area, but WASD (#63) + run (#68) let the
            // player roam the full ~240u-diameter island, so the far shore re-enters the ring.
            // PERSISTENT-FLICKER FIX (86caayvfz, 2nd pass): the 360u distance was the root of the lingering
            // shimmer — it stretched even 4 cascades thin in the FAR field (~5 texels/unit). Back the distance
            // OFF to 220u + widen the cascade-border fade (0.5) so the boundary fades GENTLY in the far distance
            // (where fog hides it) instead of as a hard band — the gentler band fix the ticket invited — while
            // SOFT shadows + a 4096 map kill the crawl percept. PRESERVES the tree shadows near spawn (well
            // within 220u) and does NOT touch the Sun light / tree-shadow setup (the ticket's OOS). Set here so
            // it bakes into FarHorizonURP.asset reproducibly on every bootstrap (not a hand-edit that reverts).
            urp.shadowDistance = ShadowDistanceU;
            urp.cascadeBorder = ShadowCascadeBorder;
            // 4 cascades stay (near-field density). Splits set to the URP 4-cascade defaults. See const block.
            urp.shadowCascadeCount = ShadowCascadeCount;
            urp.cascade4Split = ShadowCascade4Split;
            // 4096 main-light shadow map (was 2048): doubles linear texel density across all cascades. Public
            // mainLightShadowmapResolution is settable.
            urp.mainLightShadowmapResolution = MainLightShadowmapResolution;
            AssetDatabase.CreateAsset(urp, UrpAssetPath);
            AssetDatabase.SaveAssets();

            // SOFT (filtered) shadows: m_SoftShadowsSupported is serialized-only (public supportsSoftShadows is
            // get-only), so set it via SerializedObject after the asset is on disk — the same SerializedObject
            // editor-write pattern the project uses for m_AlwaysIncludedShaders (CharacterAssetGen/WorldBootstrap).
            // The 7x7 tent filter (m_SoftShadowQuality already 2 = High) blurs the shadow edge across texels so
            // the per-frame texel snap that reads as flicker is averaged out. This is the direct kill for the
            // "still flickering / too aggressive" percept (build a5282c6) that the cascade work alone could not
            // deliver. SaveAssets after so the flag persists into the committed FarHorizonURP.asset.
            var urpSo = new SerializedObject(urp);
            var softProp = urpSo.FindProperty("m_SoftShadowsSupported");
            if (softProp != null)
            {
                softProp.boolValue = SoftShadows;
                urpSo.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogWarning("[BootstrapProject] m_SoftShadowsSupported not found on URP asset — " +
                    "soft shadows NOT enabled; the flicker fix is incomplete (86caayvfz)");
            }

            GraphicsSettings.defaultRenderPipeline = urp;
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            Debug.Log("[BootstrapProject] URP pipeline created + assigned (" + levels + " quality levels)");
        }

        // Write "&lt;tag&gt; | &lt;UTC ISO&gt; | &lt;git-sha&gt;" to Resources/BuildStamp.txt. Read at runtime by
        // BuildInfo + shown by BootHud so every soak screenshot self-identifies its build.
        private static void WriteBuildStamp(string tag)
        {
            string sha = ResolveGitSha();
            string utc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string stamp = $"{tag} | {utc} | {sha}";
            Directory.CreateDirectory(Path.GetFullPath(ResourcesDir));
            File.WriteAllText(Path.GetFullPath(BuildStampPath), stamp);
            AssetDatabase.Refresh();
            Debug.Log("[BootstrapProject] build stamp -> " + stamp);
        }

        private static string ResolveGitSha()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetFullPath(".")
                };
                using var p = System.Diagnostics.Process.Start(psi);
                string outp = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(3000);
                return string.IsNullOrEmpty(outp) ? "nogit" : outp;
            }
            catch
            {
                return "nogit";
            }
        }

        // Boot scene authored from code, then RETURNED so Run() can layer the Zone-D environment
        // (U5) on top and re-save. Builds, in order:
        //   1. a base Main Camera + AudioListener + the BootHud/BootScreenshot object;
        //   2. the U3 movement+camera foundation (MovementCameraScene.Author) — player + orbit
        //      camera (which UPGRADES the base camera into the orbit rig, superseding any boot-scene
        //      capture framing) + flat walkable ground + saved-asset NavMesh, so click-to-move ships;
        //   3. (back in Run) the U5 Zone-D environment under an additive "Environment" root, whose
        //      camera post-enable lands on the U3 orbit camera (Camera.main) by then.
        //
        // Editor-time authoring (not Awake) is mandatory: the editor-vs-runtime serialization trap
        // (unity-conventions.md) ships Awake-built hierarchies MANGLED. Everything load-bearing is
        // built here + saved into Boot.unity.
        private static UnityEngine.SceneManagement.Scene BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Base Main Camera. Deliberately minimal: MovementCameraScene.Author (below) UPGRADES
            // this exact GameObject into the U3 orbit rig (orbit component + target + URP camera
            // data), which SUPERSEDES any fixed boot-scene capture framing — the combined scene's
            // camera is the gameplay orbit camera, not a static capture cam. clearFlags is left at
            // Skybox so the Zone-D gradient sky reads behind the silhouettes; backgroundColor is the
            // fallback if the skybox is ever absent.
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.10f, 0.13f, 0.18f); // fallback if no skybox
            cam.farClipPlane = 400f;
            camGo.tag = "MainCamera";

            // A placeholder directional light so the scene is lit even before the Zone-D environment
            // layers its warm "Sun" key on top (WorldBootstrap.BuildLighting adds the real key).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var hudGo = new GameObject("Boot");
            hudGo.AddComponent<BootHud>();
            // FPS counter (86cahmxmt — Sponsor #226 walk-soak item 3): "FPS <current> | avg <rolling>" on a
            // plate directly UNDER the build stamp, so every perf soak has an on-screen ground-truth number in
            // the same self-identifying corner. Ships ENABLED (default ON — Sponsor-soak tunes via the F1
            // console's `FPS counter` row, which drives this component's enabled flag; SettingsCatalog
            // .PopulateFps). Serialized editor-time (NOT Awake) per the editor-vs-runtime trap;
            // FpsCounterHudSceneTests guards its serialized presence + enabled default.
            hudGo.AddComponent<FpsCounterHud>();
            // NOTE — the legacy F2 debug-overlay master (DebugOverlayToggle, #208-era F1→F2 by 86cabeqj9) was
            // REMOVED in 86cah90cp round-3 (Sponsor-directed 2026-07-03): F10 is now the SINGLE key for the
            // debug-overlay layer. F10 (DebugOverlayMaster.overlayToggleKey) flips the shared DebugOverlays.Visible,
            // and the WorldLookNudgeTool also rides F10, so one F10 press reveals BOTH panels together; F1 (dev
            // console / SettingsPanel) stays untouched. The layer still defaults HIDDEN (clean screen on a normal
            // launch / soak / CI capture) and still gates ONLY dev overlays — never the build stamp or gameplay UI.
            hudGo.AddComponent<BootScreenshot>();
            // Standard shipped-build capture component (testing-bar capture gate, 86ca86g7k).
            // Serialized into the scene editor-time (NOT Awake) per the editor-vs-runtime
            // serialization trap; inert unless the exe is launched with -captureGate.
            hudGo.AddComponent<CaptureGate>();
            // Weapon-set STYLE-CHECKPOINT capture (86cabh907) — loads Resources/WeaponSetLineup and
            // captures the in-house knapped-flint axe from the built exe. Serialized editor-time (NOT
            // Awake) per the editor-vs-runtime trap; INERT unless launched with -verifyWeaponAxe.
            hudGo.AddComponent<WeaponSetVerifyCapture>();
            // World-look polish verify capture (86ca8t9pq) — orbits to Uma's per-surface criteria
            // (default-pitch clouds + low-pitch vista/sky dissolve). Serialized editor-time (NOT Awake)
            // per the editor-vs-runtime trap; INERT unless the exe is launched with -verifyWorldLook.
            hudGo.AddComponent<WorldLookVerifyCapture>();
            // FLAT-SHADING A/B verify capture (86caamnjb) — renders one welded smooth sphere TWICE on the
            // FarHorizon/LowPolyVertexColor material (keyword OFF then ON) so the SHIPPED-build smooth-vs-
            // faceted A/B is judged from real frames (the ddx/ddy toggle's AC5 evidence; editor capture is
            // necessary-not-sufficient). Serialized editor-time (NOT Awake) per the editor-vs-runtime trap;
            // INERT unless the exe is launched with -verifyFlatShading. FlatShadingVerifyCaptureSceneTests
            // guards its serialized presence.
            hudGo.AddComponent<FarHorizon.FlatShadingVerifyCapture>();
            // FRESNEL/RIM A/B verify capture (86caamnnj) — renders one welded smooth sphere TWICE on the
            // FarHorizon/LowPolyVertexColor material (_RimIntensity 0 then dialed) so the SHIPPED-build
            // rim-OFF-vs-dialed A/B is judged from real frames (the rim term's AC4 evidence; editor capture
            // is necessary-not-sufficient). Serialized editor-time (NOT Awake) per the editor-vs-runtime trap;
            // INERT unless the exe is launched with -verifyRim. CaptureGateSceneTests guards its serialized presence.
            hudGo.AddComponent<FarHorizon.RimVerifyCapture>();
            // LOOT-PROMPT SHOW-CASE verify capture (86cafc6ud — Tess QA #158 block: the generic -captureGate
            // only shoots the default spawn frame, where the player is OUTSIDE the tightened ~1.0–1.2u loot
            // range, so the "Press E to pick up berries" prompt is correctly HIDDEN → the prompt's SHOW state
            // had ZERO built-frame evidence). This drives the SHOW state: it teleports the player IN loot range
            // of a ripe bush so LootPrompt resolves it + OnGUI paints the tooltip, captures loot_prompt.png, and
            // SELF-ASSERTS both the logic (NearestInRange resolves the bush + the label is set) AND the render
            // (the IMGUI plate+label actually painted into the frame). Serialized editor-time (NOT Awake) per the
            // editor-vs-runtime trap; INERT unless the exe is launched with -verifyLoot.
            hudGo.AddComponent<FarHorizon.LootPromptVerifyCapture>();
            // WATER ACQUISITION end-to-end verify capture (86cafc6vx AC6 — the GET side that closes the thirst
            // loop). The generic -captureGate only shoots the default spawn frame (player OUTSIDE the pond's loot
            // range → the "Press E to collect water" prompt is HIDDEN + no loot/drink beat fires), so the one
            // surface this ticket delivers — collect water at the pond + drink it → thirst rises — had ZERO
            // built-frame evidence. -verifyWater drives the WHOLE loop in the BUILT exe: teleport the player IN
            // the pond's loot range (agent.Warp — NOT MoveTo, DEAD under WASD) so LootPrompt paints "Press E to
            // collect water", then RequestLoot → one water in the inventory, then select + RequestUseClick →
            // thirst RISES. Self-asserts all four beats + captures water_prompt.png + water_drink.png. Serialized
            // editor-time (NOT Awake) per the editor-vs-runtime trap; INERT unless launched with -verifyWater.
            hudGo.AddComponent<FarHorizon.WaterAcquisitionVerifyCapture>();
            // World-look NUDGE TOOL (86ca8t9pq soak rework) — F9-gated in-build dialing of sky gradient
            // stops / fog distance+colour (seam-kill preserved) / cloud scale+altitude / mountain
            // distance+scale, so the Sponsor finalizes the LOOK himself + reports values to bake (sibling
            // of AxeNudgeTool). Serialized editor-time per the editor-vs-runtime trap; INERT unless F9.
            hudGo.AddComponent<FarHorizon.WorldLookNudgeTool>();
            // WORLD-LOOK CONSOLE SEAM (86caber95 AC2 — F10 → dev-console rows). The single binding surface the
            // SettingsPanel's fog/sky/cloud/mountain/sun rows read/write through; it resolves the SAME
            // RenderSettings/skybox-material/cloud/vista handles the F10 WorldLookNudgeTool dials (lazily, at
            // runtime — the world exists by then). Serialized editor-time onto hudGo so it ships in Boot.unity;
            // MovementCameraScene.WireWorldLookConsole back-wires SettingsPanel.worldLook to it (the panel was
            // built earlier in BuildBootScene, so this post-environment wiring mirrors WireStoneScatterRoot).
            if (hudGo.GetComponent<FarHorizon.WorldLookTunables>() == null)
                hudGo.AddComponent<FarHorizon.WorldLookTunables>();
            // POND RECESS live nudge handle (ticket 86cadj4g7 — Sponsor #130 re-soak: he dials the final pond
            // recess depth IN THE SHIPPED BUILD + reports the value to bake). ALWAYS-LIVE (unlike the F-key-
            // toggle-gated nudge tools) — the on-screen panel shows the current recess the whole soak; PgUp/PgDn
            // step the recess (flush->shipped->deeper, default SHIPPED). The foam dial was DROPPED (#130 third
            // re-soak — the freshwater pond foam is baked OFF, not a runtime control). LAYOUT-AGNOSTIC keys
            // (Danish-keyboard-safe). Starts at the shipped default so a soak that never presses a key sees
            // exactly the shipped pond. Serialized editor-time per the editor-vs-runtime trap.
            hudGo.AddComponent<FarHorizon.PondNudge>();
            // SOAKFIX8 (86ca8ce6y FIX3): force borderless-fullscreen-at-native on a NORMAL launch so the
            // Sponsor's double-click fills his widescreen (the Player Setting alone loses to stale persisted
            // Screenmanager registry values written by the windowed capture gate). INERT on any capture/verify
            // launch (-captureGate/-verify*/-shot/-screen-fullscreen), so QA's windowed captures are untouched.
            // Serialized editor-time per the editor-vs-runtime trap; FullscreenBootSceneTests guards its presence.
            hudGo.AddComponent<FullscreenBoot>();

            // U2-1 (86ca8bd9m): the single survival need — WARMTH decays over time and drives the
            // M-U2 loop; the campfire (U2-4) answers it via WarmthNeed's satisfaction hook. SERIALIZED
            // into the scene here editor-time (NOT Awake) per the editor-vs-runtime serialization trap
            // (unity-conventions.md) — an Awake-only add could ship mangled/absent; WarmthNeedSceneTests
            // guards this.
            var survivalGo = new GameObject("Survival");
            var warmth = survivalGo.AddComponent<WarmthNeed>();

            // U2-2 (86ca8bdaq): the inventory seed — the held-resource ledger (HasAxe / WoodCount) the
            // craft step writes and U2-5's HUD reads. SERIALIZED here editor-time (NOT Awake) per the
            // editor-vs-runtime trap. Added BEFORE MovementCameraScene.Author so CraftSpot (authored
            // there) finds + wires this Inventory.
            var inventory = survivalGo.AddComponent<Inventory>();

            // HUNGER (86caamkp8): the second survival need — hunger decays as a SLOWER background
            // pressure than warmth and is restored by eating berries (the eat-action lives in the
            // bushes/inventory lane; HungerNeed.AddFood is the restore seam). IS-A SurvivalNeed (the
            // shared base this ticket owns; thirst 86caamkv7 extends it next). SERIALIZED here
            // editor-time (NOT Awake) per the editor-vs-runtime trap — HungerNeedSceneTests guards it.
            // Author the slower-than-warmth decay defaults onto the serialized component (Reset() only
            // runs on an editor add, not on a headless AddComponent, so set them explicitly here so the
            // shipped scene carries the gentler pressure). #101: the HUNGER BAR that renders this IS now
            // wired to SurvivalHud below (the loop-verify piece — folds in part of need-meter 86caamkxv).
            var hunger = survivalGo.AddComponent<HungerNeed>();
            hunger.easyDecayPerSecond = HungerNeed.HungerEasyDecayPerSecond;
            hunger.medDecayPerSecond  = HungerNeed.HungerMedDecayPerSecond;
            hunger.hardDecayPerSecond = HungerNeed.HungerHardDecayPerSecond;
            hunger.decayPerSecond     = HungerNeed.HungerMedDecayPerSecond; // medium tier by default
            // #101 EAT-REFILL FIX (root cause: the eat seam was correct, but hunger shipped startFull=true so
            // an eat clamped against an already-full bar with NO visible change — and SetCurrent's
            // Approximately early-return meant Changed never even fired). Start hunger PRESSURED WITH HEADROOM
            // (the fiction: "he starts to get hungry") so pressing E VISIBLY refills the hunger bar. Hunger's
            // 0.35/sec decay would take ~30s real-time to drop one of the 10 segments, so without this the
            // refill is unobservable in a soak. (Warmth keeps startFull=true — the campfire restores it.)
            hunger.startFull       = false;
            hunger.startFraction01 = HungerNeed.HungerStartFraction01; // 0.55 -> ~5 of 10 segments at spawn

            // THIRST (86caamkv7): the THIRD survival need — thirst decays as a FASTER pressure than hunger
            // (the fiction: "thirsty AFTER eating the berries" — it becomes pressing sooner) and is restored by
            // DRINKING FROM HAND at the freshwater pond (the drink interaction lives on FreshwaterPond, authored
            // by MovementCameraScene; ThirstNeed.AddWater is the restore seam — NOT an inventory item, distinct
            // from berries). IS-A SurvivalNeed (the shared base hunger owns; thirst EXTENDS it). SERIALIZED here
            // editor-time (NOT Awake) per the editor-vs-runtime trap — ThirstNeedSceneTests guards it. Author
            // the faster-than-hunger decay defaults onto the serialized component (Reset() only runs on an
            // editor add, not on a headless AddComponent), and ship pressured-with-headroom so a scoop VISIBLY
            // raises the bar (the #101 eat-refill fix applied to thirst). The thirst bar that RENDERS this is
            // owned by the need-meter HUD ticket (86caamkxv) — this ticket only exposes the read surface.
            var thirst = survivalGo.AddComponent<ThirstNeed>();
            thirst.easyDecayPerSecond = ThirstNeed.ThirstEasyDecayPerSecond;
            thirst.medDecayPerSecond  = ThirstNeed.ThirstMedDecayPerSecond;
            thirst.hardDecayPerSecond = ThirstNeed.ThirstHardDecayPerSecond;
            thirst.decayPerSecond     = ThirstNeed.ThirstMedDecayPerSecond; // medium tier by default
            thirst.startFull       = false;
            thirst.startFraction01 = ThirstNeed.ThirstStartFraction01; // 0.50 -> ~5 of 10 segments at spawn

            // U2-5 (86ca8bdge): the diegetic-light survival HUD — segmented ember warmth glow-bar +
            // quiet warm-cream inventory ledger (team/uma-ux/u2-5-survival-hud-spec.md). SUPERSEDES the
            // U2-1 WarmthReadout + U2-2 InventoryReadout placeholders (removed). Both data references are
            // wired editor-time here (SERIALIZED, NOT an Awake FindObjectOfType) per the editor-vs-runtime
            // trap; WarmthNeedSceneTests / CraftSceneTests guard the serialized presence + wiring.
            var hud = survivalGo.AddComponent<SurvivalHud>();
            hud.warmth = warmth;       // serialized reference, no Awake FindObjectOfType in the build
            hud.hunger = hunger;       // #101: the HUNGER bar — the player SEES hunger deplete + refill on eating
            hud.thirst = thirst;       // 86caamkxv: the THIRST bar — the player SEES thirst deplete + refill on drinking (Q)
            hud.inventory = inventory; // serialized reference, no Awake FindObjectOfType in the build

            // EAT INPUT (#101 — "I can't eat berries"): the in-game call-site for the (already-tested) eat
            // seam. Pressing E consumes one berry from the inventory + restores hunger through the atomic
            // HungerNeed.TryEatBerry path. SERIALIZED here editor-time (NOT Awake) per the editor-vs-runtime
            // trap; the Inventory + HungerNeed refs are wired so the shipped build never relies on a
            // FindObjectOfType. EatBerryActionSceneTests guards the serialized presence + wiring.
            var eat = survivalGo.AddComponent<EatBerryAction>();
            eat.inventory = inventory; // serialized reference
            eat.hunger = hunger;       // serialized reference

            // DRINK INPUT (86caamkv7): the proximity-drink-at-pond seam component. 86caf7a30 REMOVES its Q-key
            // trigger (DrinkAction.inputEnabled defaults false — drinking moves to left-click the selected water
            // belt item), but the component stays for its tested seam + the scene-presence guard. SERIALIZED here
            // editor-time per the editor-vs-runtime trap. The pond ref is wired by MovementCameraScene AFTER the
            // pond is authored; an Awake FindObjectOfType is the build-safety net. DrinkActionSceneTests guards it.
            survivalGo.AddComponent<DrinkAction>();

            // LEFT-CLICK CONSUME (86caf7a30 — the unified input model): with a CONSUMABLE selected in the belt, a
            // LEFT-CLICK consumes one unit + applies its type-specific restore (berry -> eat/AddFood, water ->
            // drink/AddWater). The consumable sibling of ChopTree's axe->chop (disjoint branches on the selected-
            // item kind — never a double-fire on the shared left-click). REUSES the SHIPPED restores: the eat seam
            // is EatBerryAction.TryEatOneBerry (atomic consume+AddFood); the drink removes one WaterId + AddWater.
            // SERIALIZED here editor-time (NOT Awake) per the editor-vs-runtime trap; the Inventory/HungerNeed/
            // ThirstNeed/EatBerryAction/InventoryUI refs are wired so the shipped build never relies on a
            // FindObjectOfType. LeftClickConsumeSceneTests guards the serialized presence + wiring.
            var consume = survivalGo.AddComponent<LeftClickConsume>();
            consume.inventory = inventory; // serialized reference
            consume.hunger = hunger;       // serialized reference (berry -> AddFood)
            consume.thirst = thirst;       // serialized reference (water -> AddWater)
            consume.eatSeam = eat;         // reuse the SHIPPED atomic berry eat-seam (no re-implemented restore)
            // consume.inventoryUI is wired below once the InventoryUI exists (it is added later in this method,
            // like ChopTree's inventoryUI ref) — see the LeftClickConsume UI-wire after the InventoryUI add.

            // DIAGNOSTIC-ONLY: inert unless launched with -invDiag (86cabfa21 / the #90 soak trace).
            survivalGo.AddComponent<InventoryDiag>();

            // VERIFY-ONLY: inert unless launched with -verifyInvIcons (#90 icon-centering shipped-build
            // capture — opens the pack with wood+axe and shoots the laid-out grid for the by-eye check).
            survivalGo.AddComponent<InventoryVerifyCapture>();

            // VERIFY-ONLY: inert unless launched with -verifyInvDragDim (#90 drag-DUPLICATE fix shipped-build
            // capture — opens the pack with berries+axe, BEGINS a drag, and shoots the frame proving the
            // SOURCE slot is dimmed/empty while only the #drag-ghost carries the item).
            survivalGo.AddComponent<InventoryDragSourceDimVerifyCapture>();

            // VERIFY-ONLY: inert unless launched with -verifyInvDragGhostPos (86caffw9h drag-ghost MISPOSITION
            // fix shipped-build capture — opens the pack, drives a KNOWN cursor through the production ghost
            // positioning, and ASSERTS the ghost center lands on the cursor + quits non-zero if it diverges).
            // ⚠ MUST be launched at a NON-1080p window (e.g. 2560x1440) — the bug is invisible at scale ~= 1.
            survivalGo.AddComponent<InventoryDragGhostPosVerifyCapture>();

            // VERIFY-ONLY: inert unless launched with -verifyConsume (86caf7a30 left-click-consume shipped-build
            // capture — seeds a berry + water on the belt, SELECTs each + drives a left-click consume, and shoots
            // the frame proving the unit was consumed AND the hunger/thirst bar visibly rose). Refs wired here
            // (all the consume deps were added above); SERIALIZED per the editor-vs-runtime trap.
            var consumeCap = survivalGo.AddComponent<LeftClickConsumeVerifyCapture>();
            consumeCap.inventory = inventory;
            consumeCap.hunger = hunger;
            consumeCap.thirst = thirst;
            consumeCap.consume = consume;

            // U3 port: author the player + orbit camera (upgrades camGo) + flat ground + saved
            // NavMesh into this scene, then save. MovementCameraScene owns the movement+camera lane.
            MovementCameraScene.Author(camGo);

            EditorSceneManager.SaveScene(scene, BootScenePath);
            Debug.Log("[BootstrapProject] boot scene saved -> " + BootScenePath);

            // Register the boot scene as the (only) build scene.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
            Debug.Log("[BootstrapProject] EditorBuildSettings scene -> " + BootScenePath);
            return scene;
        }
    }
}
