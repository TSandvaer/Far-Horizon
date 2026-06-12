using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;
using FarHorizon;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Editor-time authoring of the movement + camera foundation INTO the Boot scene
    /// (ticket 86ca86fme — the deliberate port of the spike's PoE click-to-move + orbit camera).
    ///
    /// Why editor-time and not Awake(): the editor-vs-runtime serialization trap
    /// (unity-conventions.md §"Editor-vs-runtime divergence"). A hierarchy assembled in Awake()
    /// passes EditMode checks but can ship MANGLED in the player. The player, orbit camera, flat
    /// walkable ground, and — critically — the BAKED NAVMESH-AS-SAVED-ASSET are all authored here
    /// (executeMethod), saved into Boot.unity, so the shipped exe references serialized assets.
    ///
    /// NavMesh-as-asset: NavMeshSurface bake-in-memory works in the editor but ships a DEAD
    /// click-to-move in the standalone player (no surface) unless the NavMeshData is SAVED as an
    /// asset and assigned (unity-conventions.md §NavMesh; spike FINDINGS iter-3). We save it.
    ///
    /// Scope (U3): player + camera systems + a minimal FLAT walkable test ground only. The real
    /// environment (terrain/lighting/skybox/post) is U5's surface (Drew) — this builder keeps the
    /// boot-scene ground deliberately minimal so the two lanes rebase cleanly.
    /// </summary>
    public static class MovementCameraScene
    {
        private const string NavMeshDir = "Assets/NavMesh";
        private const string PrefabDir = "Assets/Prefabs";
        private const string SettingsDir = "Assets/Settings";
        private const string NavMeshDataPath = NavMeshDir + "/BootNavMesh.asset";
        private const string MarkerPrefabPath = PrefabDir + "/ClickMarker.prefab";

        // Flat test ground extent (X/Z half-size). Big enough to prove pathfinding across a real
        // distance; small enough to stay a "test ground", not an environment (U5 owns that).
        private const float GroundHalf = 30f;

        // On-screen height (world units) for the castaway avatar root. The FBX is normalized to ~1u
        // intrinsic by CharacterAssetGen, so this scale maps directly onto height. Matched to the
        // NavMeshAgent height (1.8u) so the visible character lines up with the agent capsule.
        private const float PlayerVisualHeight = 1.8f;

        /// <summary>
        /// Author the player + orbit camera + flat ground + saved NavMesh into the CURRENT open
        /// scene. The caller (BootstrapProject.BuildBootScene) has already created the scene with
        /// a directional light + the BootHud/BootScreenshot object; this adds the movement+camera
        /// layer and REPLACES the static boot camera with the orbit camera. Returns nothing; logs
        /// each load-bearing step so a headless run is auditable.
        /// </summary>
        public static void Author(GameObject existingBootCamera)
        {
            EnsureDirs();

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0)
            {
                Debug.LogError("[MovementCameraScene] 'Ground' layer is not defined in TagManager — " +
                               "the click-raycast + NavMesh layer mask depend on it");
                groundLayer = 0; // fall back to Default so the bake still produces a surface
            }

            GameObject ground = BuildFlatGround(groundLayer);
            ClickMarker markerPrefab = BuildClickMarkerPrefab();
            GameObject player = BuildPlayer(markerPrefab, groundLayer);
            BuildOrbitCamera(player, existingBootCamera);

            // U2-2 (86ca8bdaq): the craft spot — the entry to the survival loop. A world marker the
            // castaway click-moves to; reaching it crafts the axe (one recipe, no UI tree). Authored
            // editor-time so the spot mesh + CraftSpot's Inventory/player references SERIALIZE into
            // Boot.unity (editor-vs-runtime trap). The Inventory + InventoryReadout live on the
            // Survival object (added by BootstrapProject before this runs); we find + wire them here.
            BuildCraftSpot(player, groundLayer);

            // Bake AFTER the walkable ground exists, then SAVE the data as an asset so it ships.
            BakeAndSaveNavMesh(ground, groundLayer);

            // Wire the verification-only shipped-build movement capture onto the Boot object so the
            // testing bar's shipped-build gate can prove click-move in the BUILT exe (-verifyMove).
            WireMovementVerifyCapture(player);

            Debug.Log("[MovementCameraScene] authored player + orbit camera + flat ground + NavMesh");
        }

        private static void EnsureDirs()
        {
            foreach (var d in new[] { NavMeshDir, PrefabDir, SettingsDir })
                Directory.CreateDirectory(Path.GetFullPath(d));
            AssetDatabase.Refresh();
        }

        // A flat subdivided plane on the Ground layer with a MeshCollider, so the NavMesh bake
        // (PhysicsColliders collection) AND the click-to-move ground raycast both hit it.
        // Subdivided (not a single quad) so the baked NavMesh has clean geometry.
        private static GameObject BuildFlatGround(int groundLayer)
        {
            var go = new GameObject("TestGround");
            go.layer = groundLayer;
            go.transform.position = Vector3.zero;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            const int seg = 20;
            float size = GroundHalf * 2f;
            var verts = new Vector3[(seg + 1) * (seg + 1)];
            var uvs = new Vector2[verts.Length];
            for (int z = 0; z <= seg; z++)
                for (int x = 0; x <= seg; x++)
                {
                    int i = z * (seg + 1) + x;
                    float fx = (float)x / seg, fz = (float)z / seg;
                    verts[i] = new Vector3((fx - 0.5f) * size, 0f, (fz - 0.5f) * size);
                    uvs[i] = new Vector2(fx * seg, fz * seg);
                }
            var tris = new int[seg * seg * 6];
            int ti = 0;
            for (int z = 0; z < seg; z++)
                for (int x = 0; x < seg; x++)
                {
                    int i = z * (seg + 1) + x;
                    tris[ti++] = i; tris[ti++] = i + seg + 1; tris[ti++] = i + 1;
                    tris[ti++] = i + 1; tris[ti++] = i + seg + 1; tris[ti++] = i + seg + 2;
                }
            var mesh = new Mesh { name = "TestGround_mesh" };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            // A simple, build-safe URP/Lit material so the ground isn't pink in the shipped build.
            // U5 will replace the environment surface; this is a neutral placeholder, not art.
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(0.42f, 0.46f, 0.40f)); // muted moss-grey
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
                AssetDatabase.CreateAsset(mat, SettingsDir + "/TestGroundMat.mat");
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            return go;
        }

        // A flat ring quad on the ground that the ClickToMove spawns at the move destination.
        // Built as a prefab asset so ClickToMove's serialized markerPrefab reference ships.
        private static ClickMarker BuildClickMarkerPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            root.name = "ClickMarker";
            Object.DestroyImmediate(root.GetComponent<Collider>()); // marker must not block raycasts
            // Lie flat on the ground, ~0.8u ring.
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            root.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                var mat = new Material(unlit);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.5f, 0.9f)); // warm marker
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                AssetDatabase.CreateAsset(mat, SettingsDir + "/ClickMarkerMat.mat");
                root.GetComponent<MeshRenderer>().sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(unlit);
            }

            root.AddComponent<ClickMarker>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, MarkerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab != null ? prefab.GetComponent<ClickMarker>() : null;
        }

        // World position of the craft spot on the flat test ground. Distinct from spawn (origin) so the
        // craft is a real click-move journey, comfortably inside the GroundHalf=30 walkable extent, and
        // on the NavMesh. CraftVerifyCapture drives the player here to prove the craft in the shipped exe.
        public static readonly Vector3 CraftSpotPosition = new Vector3(8f, 0f, 6f);

        // The craft spot (U2-2, 86ca8bdaq): a low-poly marker the castaway click-moves to; reaching it
        // crafts the axe. A small build-safe URP/Lit "stump/bench" placeholder (no art polish — U2-4+
        // and Uma own the real craft-station look). NO collider so it never blocks the ground raycast or
        // the NavMesh (the player walks ONTO the spot). CraftSpot's Inventory + player refs are wired
        // editor-time so they serialize into Boot.unity (editor-vs-runtime trap).
        private static void BuildCraftSpot(GameObject player, int groundLayer)
        {
            var spot = new GameObject("CraftSpot");
            spot.transform.position = CraftSpotPosition;

            // A small low cylinder as a placeholder "crafting stump" so the spot is VISIBLE in the exe
            // (the player needs something to walk toward). Primitive cylinder, collider stripped.
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "CraftStump";
            Object.DestroyImmediate(visual.GetComponent<Collider>()); // no block on raycast / NavMesh
            visual.transform.SetParent(spot.transform, false);
            visual.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f); // squat stump
            visual.transform.localPosition = new Vector3(0f, 0.35f, 0f);  // sit on the ground

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(0.45f, 0.32f, 0.20f)); // warm timber brown
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                AssetDatabase.CreateAsset(mat, SettingsDir + "/CraftStumpMat.mat");
                visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }

            var craft = spot.AddComponent<CraftSpot>();
            craft.player = player.transform;
            craft.inventory = Object.FindObjectOfType<Inventory>();
            if (craft.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire CraftSpot to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");

            // Wire the verification-only shipped-build CRAFT capture (drives the player to the spot,
            // proves the axe is crafted in the BUILT exe) onto the Boot object — sibling of the
            // movement-verify capture. Inert unless launched with -verifyCraft.
            WireCraftVerifyCapture(player);

            Debug.Log("[MovementCameraScene] authored CraftSpot at " + CraftSpotPosition +
                      " (inventory wired: " + (craft.inventory != null) + ")");
        }

        private static void WireCraftVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host CraftVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<CraftVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<CraftVerifyCapture>();
            cap.player = player.GetComponent<ClickToMove>();
            cap.inventory = Object.FindObjectOfType<Inventory>();
            cap.craftSpot = CraftSpotPosition;
        }

        // The player: NavMeshAgent + ClickToMove + the U6 castaway avatar (replaces the U3 capsule
        // placeholder). ClickToMove owns the agent; the camera follows the player root; the
        // CastawayCharacter self-drives its Idle<->Walk anim + facing off the agent's velocity.
        private static GameObject BuildPlayer(ClickMarker markerPrefab, int groundLayer)
        {
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;

            var agent = player.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.8f;
            agent.speed = 5.5f;
            agent.angularSpeed = 999f;
            agent.acceleration = 30f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;

            // U6 (86ca86fz9): the real 3D player avatar — a rigged CC0 low-poly CLOTHED character
            // (Quaternius Animated-Men, Smooth_Male_Casual.fbx, CC0) with the warm castaway recolor.
            // The avatar lives on a child root scaled to the on-screen height; the FBX origin is at the
            // feet (localPosition zero -> grounded feet on the agent's ground point). Built editor-time
            // (BuildInEditor) so the SkinnedMeshRenderer + bones + controller reference SERIALIZE into
            // Boot.unity (the editor-vs-runtime serialization lesson — no Awake-assembled hierarchy to
            // ship mangled). Ensure URP/Lit is always-included so the recolor materials don't strip to
            // magenta in the stripped player.
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null) EnsureShaderAlwaysIncluded(litShader);

            var avatarGo = new GameObject("CastawayAvatar");
            avatarGo.transform.SetParent(player.transform, false);
            avatarGo.transform.localPosition = Vector3.zero;
            // The FBX is normalized to ~1u intrinsic; scale the avatar root to the agent height (1.8u)
            // so the visible character matches the agent capsule + grounds correctly.
            avatarGo.transform.localScale = Vector3.one * PlayerVisualHeight;

            var castaway = avatarGo.AddComponent<CastawayCharacter>();
            castaway.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            castaway.animatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAssetGen.ControllerPath);
            if (castaway.modelPrefab == null)
                Debug.LogError("[MovementCameraScene] castaway FBX not found at " + CharacterAssetGen.FbxPath +
                               " — run CharacterAssetGen.PrepareCharacter() before authoring the scene");
            // Build the Model child + materials NOW (editor) so they serialize into Boot.unity.
            castaway.BuildInEditor();

            var ctm = player.AddComponent<ClickToMove>();
            ctm.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            ctm.markerPrefab = markerPrefab;

            return player;
        }

        // Replace the static boot camera with the OrbitCamera rig targeting the player. We reuse
        // the existing camera GameObject (keeps its MainCamera tag + AudioListener) and add the
        // URP camera data + OrbitCamera component, so the boot smoke test still finds a Camera.
        private static void BuildOrbitCamera(GameObject player, GameObject existingBootCamera)
        {
            GameObject camGo = existingBootCamera != null ? existingBootCamera : new GameObject("Main Camera");
            var cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.13f, 0.18f); // deep dusk blue (U5 owns the skybox)
            cam.fieldOfView = 45f;
            camGo.tag = "MainCamera";
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            if (camGo.GetComponent<UniversalAdditionalCameraData>() == null)
                camGo.AddComponent<UniversalAdditionalCameraData>();

            var orbit = camGo.GetComponent<OrbitCamera>();
            if (orbit == null) orbit = camGo.AddComponent<OrbitCamera>();
            orbit.target = player.transform;
            orbit.defaultPitch = 55f;   // Sponsor-preferred top-down-ish framing (inside 35-70)
            orbit.minPitch = 35f;
            orbit.maxPitch = 70f;
            orbit.distance = 14f;
        }

        // Bake the NavMesh from the walkable ground, then SAVE the data as an asset and assign it
        // so the standalone build SHIPS a NavMesh (else click-to-move is silently dead — the
        // spike's iter-3 lesson + unity-conventions.md §NavMesh).
        private static void BakeAndSaveNavMesh(GameObject ground, int groundLayer)
        {
            var surfaceGo = new GameObject("NavMeshSurface");
            var surface = surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            if (groundLayer >= 0) surface.layerMask = 1 << groundLayer;
            surface.BuildNavMesh();

            if (surface.navMeshData != null)
            {
                Directory.CreateDirectory(Path.GetFullPath(NavMeshDir));
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(surface);
                Debug.Log("[MovementCameraScene] NavMesh baked + SAVED -> " + NavMeshDataPath +
                          " (data assigned: " + (surface.navMeshData != null) + ")");
            }
            else
            {
                Debug.LogError("[MovementCameraScene] NavMesh bake produced NO data — " +
                               "click-to-move would be dead in the build");
            }
        }

        // Attach the verification-only movement capture to the Boot object (the GameObject that
        // already carries BootHud + BootScreenshot), wiring it to the player. Inert unless the
        // build is launched with -verifyMove, so it never affects the normal game or boot capture.
        private static void WireMovementVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] 'Boot' object not found — movement-verify " +
                                 "capture not wired (the shipped-build movement gate hook is absent)");
                return;
            }
            var cap = bootGo.GetComponent<MovementVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<MovementVerifyCapture>();
            cap.player = player.GetComponent<ClickToMove>();
            EditorUtility.SetDirty(bootGo);
        }

        // Add a shader to GraphicsSettings.AlwaysIncludedShaders so the standalone build does NOT
        // strip it (the iter-2/3 magenta lesson). Idempotent.
        private static void EnsureShaderAlwaysIncluded(Shader shader)
        {
            if (shader == null) return;
            var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return; // present
            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[MovementCameraScene] added shader to AlwaysIncludedShaders -> " + shader.name);
        }
    }
}
