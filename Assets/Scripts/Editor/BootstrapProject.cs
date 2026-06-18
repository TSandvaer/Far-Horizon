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
        // grass. 360 clears the worst-case vantage: island diameter 240 + max orbit reach 26 = 266u, plus ~35%
        // headroom above the cascade-border fade band. Sized to the WHOLE roamable island from any vantage.
        public const float ShadowDistanceU = 360f;     // was 160 (spawn-locked era); 50 URP-default before that
        public const float ShadowCascadeBorder = 0.35f; // wider fade band than the 0.2 default (soft boundary)

        // FLICKER FIX (86caayvfz): the 360u distance above on a SINGLE cascade stretched the 2048^2 main-light
        // shadow map over the whole frustum -> ~4 texels/world-unit near-field (vs ~9 at the prior 160u, ~18 at
        // the URP-default 50u). At that texel density the shadow edge crawls between texel-quantization steps as
        // the camera/player moves -> shimmer the Sponsor read as "light flickering / lag" (build adde6b0). The
        // lever flagged at the 360 fix ("if crispness regresses, +1 cascade") is the minimal kill: split the
        // 360u frustum across 4 cascades so the NEAR cascade carries the full 2048^2 over only the first slice
        // (~24u at the default 6.7% split) -> ~85 texels/unit near-field, DENSER than even the old 50u build,
        // while shadowDistance stays 360 so the green-line band stays gone. Splits are the URP 4-cascade
        // defaults (cascade0 ends 6.7%, c1 20%, c2 46.7% of 360u); cascadeBorder 0.35 still softens the OUTER
        // boundary. Cost: 4 shadow-map render passes vs 1 on the single directional light -> negligible on the
        // static low-poly world (one sun, no additional-light shadows; m_AdditionalLightShadowsSupported 0).
        public const int ShadowCascadeCount = 4;        // was 1 (single stretched cascade -> shimmer)
        public static readonly Vector3 ShadowCascade4Split = new Vector3(0.067f, 0.2f, 0.467f); // URP 4-cascade default
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
            // player roam the full ~240u-diameter island, so the far shore re-enters the ring; ShadowDistanceU
            // is now 360 to clear the whole roamable island from any orbit vantage (see the const for the math).
            // FIX: push shadowDistance out past the roamable area + widen the cascade-border fade so the
            // boundary never lands in frame AND fades softly if it ever does. This is the URP shadow DISTANCE
            // setting — it PRESERVES the tree shadows near spawn (well within 360u) and does NOT touch the Sun
            // light or the tree-shadow setup (the ticket's OOS). Set here so it bakes into FarHorizonURP.asset
            // reproducibly on every bootstrap (not a hand-edit that silently reverts).
            urp.shadowDistance = ShadowDistanceU;
            urp.cascadeBorder = ShadowCascadeBorder;
            // 86caayvfz: 4 cascades recover near-field texel density at the 360u distance (kills the shimmer
            // the single stretched cascade caused) while keeping the green-line distance fix intact. Splits set
            // to the URP 4-cascade defaults so the near cascade is the densest slice. See the const block above.
            urp.shadowCascadeCount = ShadowCascadeCount;
            urp.cascade4Split = ShadowCascade4Split;
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
