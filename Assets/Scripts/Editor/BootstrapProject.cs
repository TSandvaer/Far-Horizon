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
            AssetDatabase.CreateAsset(urp, UrpAssetPath);
            AssetDatabase.SaveAssets();

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

            // U2-5 (86ca8bdge): the diegetic-light survival HUD — segmented ember warmth glow-bar +
            // quiet warm-cream inventory ledger (team/uma-ux/u2-5-survival-hud-spec.md). SUPERSEDES the
            // U2-1 WarmthReadout + U2-2 InventoryReadout placeholders (removed). Both data references are
            // wired editor-time here (SERIALIZED, NOT an Awake FindObjectOfType) per the editor-vs-runtime
            // trap; WarmthNeedSceneTests / CraftSceneTests guard the serialized presence + wiring.
            var hud = survivalGo.AddComponent<SurvivalHud>();
            hud.warmth = warmth;       // serialized reference, no Awake FindObjectOfType in the build
            hud.inventory = inventory; // serialized reference, no Awake FindObjectOfType in the build

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
