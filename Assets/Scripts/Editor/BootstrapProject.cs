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
            // Ticket 86ca8ce6y (RE-DONE): import the SOURCED hero axe FBX (rustic hatchet) — downsample
            // its oversized atlas, normalize scale, static prop — BEFORE the scene is authored, so
            // MovementCameraScene.AttachHeroAxeToHand can parent the imported mesh under the chibi's hand
            // bone and serialize it into Boot.unity. (Replaces the retired procedural HeroAxeMesh.)
            AxeAssetGen.PrepareAxe();
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
            AssetDatabase.CreateAsset(renderer, UrpRendererPath);

            var urp = UniversalRenderPipelineAsset.Create(renderer);
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
            hudGo.AddComponent<BootScreenshot>();
            // Standard shipped-build capture component (testing-bar capture gate, 86ca86g7k).
            // Serialized into the scene editor-time (NOT Awake) per the editor-vs-runtime
            // serialization trap; inert unless the exe is launched with -captureGate.
            hudGo.AddComponent<CaptureGate>();
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
            // World-look NUDGE TOOL (86ca8t9pq soak rework) — F9-gated in-build dialing of sky gradient
            // stops / fog distance+colour (seam-kill preserved) / cloud scale+altitude / mountain
            // distance+scale, so the Sponsor finalizes the LOOK himself + reports values to bake (sibling
            // of AxeNudgeTool). Serialized editor-time per the editor-vs-runtime trap; INERT unless F9.
            hudGo.AddComponent<FarHorizon.WorldLookNudgeTool>();
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

            // U2-5 (86ca8bdge): the diegetic-light survival HUD — segmented ember warmth glow-bar +
            // quiet warm-cream inventory ledger (team/uma-ux/u2-5-survival-hud-spec.md). SUPERSEDES the
            // U2-1 WarmthReadout + U2-2 InventoryReadout placeholders (removed). Both data references are
            // wired editor-time here (SERIALIZED, NOT an Awake FindObjectOfType) per the editor-vs-runtime
            // trap; WarmthNeedSceneTests / CraftSceneTests guard the serialized presence + wiring.
            var hud = survivalGo.AddComponent<SurvivalHud>();
            hud.warmth = warmth;       // serialized reference, no Awake FindObjectOfType in the build
            hud.hunger = hunger;       // #101: the HUNGER bar — the player SEES hunger deplete + refill on eating
            hud.inventory = inventory; // serialized reference, no Awake FindObjectOfType in the build

            // EAT INPUT (#101 — "I can't eat berries"): the in-game call-site for the (already-tested) eat
            // seam. Pressing E consumes one berry from the inventory + restores hunger through the atomic
            // HungerNeed.TryEatBerry path. SERIALIZED here editor-time (NOT Awake) per the editor-vs-runtime
            // trap; the Inventory + HungerNeed refs are wired so the shipped build never relies on a
            // FindObjectOfType. EatBerryActionSceneTests guards the serialized presence + wiring.
            var eat = survivalGo.AddComponent<EatBerryAction>();
            eat.inventory = inventory; // serialized reference
            eat.hunger = hunger;       // serialized reference

            // DIAGNOSTIC-ONLY: inert unless launched with -invDiag (86cabfa21 / the #90 soak trace).
            survivalGo.AddComponent<InventoryDiag>();

            // VERIFY-ONLY: inert unless launched with -verifyInvIcons (#90 icon-centering shipped-build
            // capture — opens the pack with wood+axe and shoots the laid-out grid for the by-eye check).
            survivalGo.AddComponent<InventoryVerifyCapture>();

            // VERIFY-ONLY: inert unless launched with -verifyInvDragDim (#90 drag-DUPLICATE fix shipped-build
            // capture — opens the pack with berries+axe, BEGINS a drag, and shoots the frame proving the
            // SOURCE slot is dimmed/empty while only the #drag-ghost carries the item).
            survivalGo.AddComponent<InventoryDragSourceDimVerifyCapture>();

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
