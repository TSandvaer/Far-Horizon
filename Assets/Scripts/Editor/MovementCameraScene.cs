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

        // ---- Hero axe anchor colors (ticket 86ca8ce6y, Tess-verified anchors from style-guide-v2.md §6 /
        // inspiration/2026-06-12_21h08_08.png). Sub-1.0 / HDR-safe. The HeroAxeMesh builder is geometry-
        // only and color-agnostic; these are the SINGLE source of truth for the prop palette, exposed so
        // HeroAxeSceneTests can assert the shipped material colors stay within the guide's anchors. ----
        public static readonly Color AxeHeadColor = new Color(0.64f, 0.23f, 0.19f); // barn red   #A33B30
        public static readonly Color AxeBevelColor = new Color(0.89f, 0.89f, 0.86f); // pale steel #E4E2DC
        public static readonly Color AxeHaftColor = new Color(0.48f, 0.32f, 0.19f); // warm brown #7A5230
        // Name of the hero-axe GameObject under the CraftSpot — the scene-presence test keys on it.
        public const string HeroAxeObjectName = "HeroAxe";

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

            // U2-3 (86ca8bdd8): the choppable tree — the "do work in the world" beat. A Zone-D low-poly
            // tree the castaway click-moves to; reaching it WITH the axe (U2-2) chops it for wood. Authored
            // editor-time so the tree mesh + ChopTree's Inventory/player/visual refs SERIALIZE into
            // Boot.unity (editor-vs-runtime trap). Built AFTER the craft spot (so the loop reads
            // spawn -> craft axe -> chop tree) and BEFORE the NavMesh bake (the tree has no collider, so
            // it neither blocks the ground raycast nor the bake — the player walks up to it).
            BuildChopTree(player, groundLayer);

            // U2-4 (86ca8bdep): the campfire — the loop's CLOSE. A human-scale fire pit the castaway
            // click-moves to; arriving WITH WOOD (from the chop, U2-3) builds + lights it, and the lit
            // fire RESTORES warmth (U2-1's AddWarmth seam) while the castaway stands by it — warmth decays
            // -> craft axe -> chop tree -> build campfire -> warm again. Authored editor-time so the fire
            // mesh + warm Light + Campfire/CampfirePlacement refs SERIALIZE into Boot.unity (editor-vs-runtime
            // trap). Built AFTER the tree (so the loop reads spawn -> craft -> chop -> build fire) and BEFORE
            // the NavMesh bake (the fire-pit has no collider — the player walks up to it).
            BuildCampfire(player, groundLayer);

            // Bake AFTER the walkable ground exists, then SAVE the data as an asset so it ships.
            BakeAndSaveNavMesh(ground, groundLayer);

            // Wire the verification-only shipped-build movement capture onto the Boot object so the
            // testing bar's shipped-build gate can prove click-move in the BUILT exe (-verifyMove).
            WireMovementVerifyCapture(player);

            // Wire the verification-only shipped-build SEA capture (drew/beach-water-scene; Uma §4 task F).
            // The default orbit framing looks INLAND; this drives an orbit-to-seaward yaw in the BUILT exe
            // so the beach ocean is captured filling the frame (the inland -captureGate frames can't judge
            // it). Inert unless launched with -verifySea. Sibling of the movement/craft/chop/loop captures.
            WireSeaVerifyCapture();

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
        // crafts the axe. A small chopping-block stump the castaway walks ONTO, with the STYLIZED HERO AXE
        // (ticket 86ca8ce6y — the style-wave anchor) displayed resting in it so the loop's hero tool reads
        // "in the world" the whole time (style-guide-v2.md §3: the axe is on-screen constantly). NO collider
        // so it never blocks the ground raycast or the NavMesh. The stump + axe meshes + CraftSpot's
        // Inventory/player refs are authored editor-time so they serialize into Boot.unity (editor-vs-runtime
        // trap — an Awake-built prop ships mangled, the "legs-up" class).
        private static void BuildCraftSpot(GameObject player, int groundLayer)
        {
            var spot = new GameObject("CraftSpot");
            spot.transform.position = CraftSpotPosition;

            // A small low cylinder as the "chopping block" the player walks toward. Primitive cylinder,
            // collider stripped. The hero axe rests in it (below).
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

            // The stylized HERO AXE displayed resting in the block, head up — the style-wave anchor.
            BuildHeroAxe(spot, new Vector3(0f, 0.70f, 0f));

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

        // The stylized HERO AXE (ticket 86ca8ce6y — the FIRST style-wave anchor, establishes the board's
        // tool language). A single 3-submesh mesh (HEAD / BEVEL / HAFT — HeroAxeMesh.Build) with a matching
        // 3-material array (barn-red head / pale-steel edge bevel / warm-brown haft, the Tess-verified
        // anchors). The mesh + the inline materials serialize into Boot.unity (no .mat churn, same idiom as
        // LowPolyZoneGen scatter props) — editor-time authored, NOT Awake-built (the "legs-up" trap).
        //
        // Read like inspiration/2026-06-12_21h08_08.png: faceted barn-red wedge head, a distinct near-white
        // chamfer plane along the cutting edge (the signature — GEOMETRY catching light, not a texture line),
        // a small horn/poll, and a chunky gently-bent brown haft. Hard-faceted (flat normals) so the bevel
        // reads as a separate bright plane — see HeroAxeMesh for the shading rationale.
        private static void BuildHeroAxe(GameObject parent, Vector3 localPos)
        {
            var axe = new GameObject(HeroAxeObjectName);
            axe.transform.SetParent(parent.transform, false);
            axe.transform.localPosition = localPos;
            // Display pose: stand head-up, canted a touch so the bright bevel + faceted cheeks catch the key
            // light at orbit distance (mild hand-made tilt, not a museum-square mount).
            axe.transform.localRotation = Quaternion.Euler(0f, 35f, 12f);

            var mf = axe.AddComponent<MeshFilter>();
            mf.sharedMesh = HeroAxeMesh.Build();
            var mr = axe.AddComponent<MeshRenderer>();

            // 3-material array in submesh order (HEAD=0, BEVEL=1, HAFT=2). Inline URP/Lit, matte so the
            // low-poly reads by shape + facet shading, not gloss — except the bevel gets a touch more
            // smoothness so the near-white edge plane CATCHES the key as the signature highlight.
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            var mats = new Material[HeroAxeMesh.SUBMESH_COUNT];
            mats[HeroAxeMesh.SUBMESH_HEAD] = MakeAxeMat(litShader, AxeHeadColor, 0.10f, "HeroAxeHeadMat");
            mats[HeroAxeMesh.SUBMESH_BEVEL] = MakeAxeMat(litShader, AxeBevelColor, 0.45f, "HeroAxeBevelMat");
            mats[HeroAxeMesh.SUBMESH_HAFT] = MakeAxeMat(litShader, AxeHaftColor, 0.06f, "HeroAxeHaftMat");
            mr.sharedMaterials = mats;
            if (litShader != null) EnsureShaderAlwaysIncluded(litShader);

            // Wire the verification-only shipped-build AXE CLOSE-UP capture (drives a dedicated camera
            // onto the cutting edge so the signature bevel plane is framed edge-on) onto the Boot object —
            // sibling of the craft/chop/movement verify captures. Inert unless launched with -verifyAxe.
            // This gives the edge-bevel claim a committed, repeatable shipped-build capture path (the
            // -verifyCraft shot frames the axe top-down where the bevel faces away — Tess's PR #21 NIT).
            WireAxeVerifyCapture();

            Debug.Log("[MovementCameraScene] authored HeroAxe under CraftSpot (submeshes=" +
                      mf.sharedMesh.subMeshCount + ", head/bevel/haft anchors applied)");
        }

        private static void WireAxeVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host AxeVerifyCapture");
                return;
            }
            if (bootGo.GetComponent<AxeVerifyCapture>() == null)
                bootGo.AddComponent<AxeVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        // A flat-color URP/Lit material for an axe part. NOT persisted as a .mat asset — assigned to
        // sharedMaterials it serializes INLINE into the saved scene (ships in the standalone build), the
        // same churn-free idiom LowPolyZoneGen uses for scatter props.
        private static Material MakeAxeMat(Shader litShader, Color c, float smoothness, string name)
        {
            if (litShader == null) return null;
            var mat = new Material(litShader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
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

        // World position of the choppable tree on the flat test ground (U2-3, 86ca8bdd8). Distinct from
        // spawn (origin) and the craft spot (8,6) so the loop is a real journey: spawn -> craft axe ->
        // chop tree. Comfortably inside the GroundHalf=30 walkable extent + on the NavMesh.
        // ChopVerifyCapture drives the player here (after the craft spot) to prove the chop in the exe.
        public static readonly Vector3 ChopTreePosition = new Vector3(-9f, 0f, -7f);

        // The choppable tree (U2-3, 86ca8bdd8): a Zone-D low-poly tree (welded trunk + faceted canopy,
        // the same smooth-shaded mesh idiom as LowPolyZoneGen's scatter trees — we RIDE the established
        // Zone-D look, not invent a fresh prop, per the art-direction gate). The castaway click-moves to
        // it; reaching it WITH the axe chops it for wood. NO collider on the tree so it never blocks the
        // ground raycast or the NavMesh (the player walks up to the trunk). ChopTree's Inventory + player
        // + visual refs are wired editor-time so they serialize into Boot.unity (editor-vs-runtime trap).
        private static void BuildChopTree(GameObject player, int groundLayer)
        {
            var tree = new GameObject("ChopTree");
            tree.transform.position = ChopTreePosition;

            // The visual: a low-poly trunk + canopy, parented so ChopTree can tween the whole visual on
            // felling (sink + tip) without moving the interaction's proximity origin. Built editor-time
            // (meshes assigned to MeshFilters + inline materials) so it serializes into the scene.
            var visual = new GameObject("TreeVisual");
            visual.transform.SetParent(tree.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // Warm bark trunk (sub-1.0, HDR-safe). The canopy is now a BLOB CANOPY (board v2,
            // 86ca8ce7j): a cluster of overlapping faceted spheroids in multi-value greens, so the
            // choppable tree reads in the SAME blob-canopy language as the scatter trees (it must NOT
            // be a lone single-dome tree next to clustered ones — art-direction fidelity). Only the
            // VISUAL mesh changes here; ChopTree's behavior/wiring/tests are untouched (the canopy is a
            // child under the same tweened visual root, so felling still sinks+tips the whole tree).
            Color trunkCol = new Color(0.42f, 0.30f, 0.19f); // warm bark (LowPolyZoneGen.TrunkCol)

            const float trunkH = 1.8f;
            BuildTreePart(visual, "Trunk", LowPolyMeshes.TaperedCylinder(0.22f, 0.15f, trunkH, 6),
                trunkCol, Vector3.zero, "ChopTrunkMat");
            BuildBlobCanopyPart(visual, "Canopy",
                LowPolyMeshes.BlobCanopy(1.30f, 5, ChopCanopyBody, ChopCanopyTop, ChopCanopyShadow, 8123),
                new Vector3(0f, trunkH + 0.65f, 0f));

            var chop = tree.AddComponent<ChopTree>();
            chop.player = player.transform;
            chop.inventory = Object.FindObjectOfType<Inventory>();
            chop.visual = visual.transform;
            if (chop.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire ChopTree to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");

            // Wire the verification-only shipped-build CHOP capture (drives the player to craft the axe,
            // then to the tree, proves wood is yielded in the BUILT exe) onto the Boot object — sibling
            // of the craft/movement verify captures. Inert unless launched with -verifyChop.
            WireChopVerifyCapture(player);

            Debug.Log("[MovementCameraScene] authored ChopTree at " + ChopTreePosition +
                      " (inventory wired: " + (chop.inventory != null) + ")");
        }

        // Blob-canopy greens for the choppable tree (board v2, 86ca8ce7j) — same 3-value palette family
        // as LowPolyZoneGen's scatter canopies (style-guide-v2 §6), so the choppable tree matches the
        // world's trees. Multi-value greens are baked into the mesh's vertex color by BlobCanopy.
        private static readonly Color ChopCanopyBody   = new Color(0.30f, 0.58f, 0.24f);
        private static readonly Color ChopCanopyTop    = new Color(0.48f, 0.74f, 0.34f);
        private static readonly Color ChopCanopyShadow = new Color(0.18f, 0.40f, 0.17f);

        // Build the chop tree's blob canopy: a child with the BlobCanopy mesh (multi-value greens in
        // vertex color) + an INLINE vertex-color material (serializes into the scene, no .mat churn).
        // The canopy bakes its greens into vertex color, so it needs the FarHorizon/LowPolyVertexColor
        // shader (URP/Lit ignores vertex color — unity-conventions.md); that shader is registered in
        // AlwaysIncludedShaders so it never strips. Falls back to a flat mid-green URP/Lit if the
        // shader is unresolved (vertex greens lost, but never magenta).
        private static void BuildBlobCanopyPart(GameObject parent, string name, Mesh mesh, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();

            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vc != null)
            {
                var mat = new Material(vc) { name = "ChopCanopyMat" };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Color.white);
                mr.sharedMaterial = mat; // inline -> serializes into the scene
                EnsureShaderAlwaysIncluded(vc);
            }
            else
            {
                var litShader = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(litShader) { name = "ChopCanopyMat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ChopCanopyBody);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.06f);
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
                Debug.LogWarning("[MovementCameraScene] vertex-color shader not found; chop canopy flat-green fallback");
            }
        }

        // Build one part of the chop tree's visual (trunk or canopy): a child GameObject with the given
        // welded smooth-shaded mesh + an inline URP/Lit material (serializes into the scene, no .mat
        // churn). Matte smoothness so the low-poly reads by shape + shading, not gloss.
        private static void BuildTreePart(GameObject parent, string name, Mesh mesh, Color color,
            Vector3 localPos, string matName)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = matName };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.06f);
                mr.sharedMaterial = mat; // inline -> serializes into the scene, no asset churn
                EnsureShaderAlwaysIncluded(litShader);
            }
        }

        private static void WireChopVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host ChopVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<ChopVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<ChopVerifyCapture>();
            cap.player = player.GetComponent<ClickToMove>();
            cap.inventory = Object.FindObjectOfType<Inventory>();
            cap.craftSpot = CraftSpotPosition;
            cap.treeSpot = ChopTreePosition;
            EditorUtility.SetDirty(bootGo);
        }

        // World position of the campfire fire-pit on the flat test ground (U2-4, 86ca8bdep). Distinct from
        // spawn (origin), the craft spot (8,6) and the tree (-9,-7) so the loop is a real journey: spawn ->
        // craft axe -> chop tree -> BUILD FIRE. Comfortably inside the GroundHalf=30 walkable extent + on
        // the NavMesh. CampfireVerifyCapture drives the player here (last) to prove the loop closes in the exe.
        public static readonly Vector3 FirePitPosition = new Vector3(4f, 0f, -8f);

        // The campfire (U2-4, 86ca8bdep): a HUMAN-SCALE fire pit — a ring of low-poly stones around stacked
        // logs with a warm flame, lit by a warm point Light into the Zone-D dusk (art board: the fire "sets
        // the bar the whole world must meet"; warm cohesive palette + human-scale landmark, ~knee-high, NOT a
        // bonfire monument). It ships UNLIT (flame hidden, light off); reaching the pit WITH WOOD builds +
        // lights it. NO collider on the pit so it never blocks the ground raycast or the NavMesh (the player
        // walks up to the fire). Campfire + CampfirePlacement refs are wired editor-time so they serialize
        // into Boot.unity (editor-vs-runtime trap). We RIDE the established Zone-D welded-smooth-shaded mesh
        // idiom (stones = faceted spheres, logs = tapered cylinders, flame = a cone) — not a fresh prop style.
        private static void BuildCampfire(GameObject player, int groundLayer)
        {
            var pit = new GameObject("Campfire");
            pit.transform.position = FirePitPosition;

            var visual = new GameObject("CampfireVisual");
            visual.transform.SetParent(pit.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // Palette (Zone-D, sub-1.0 HDR-safe). Stones: cool grey with tonal variation; logs: warm bark
            // (matches the chop-tree trunk so the wood READS as "the wood you chopped"); flame: saturated
            // warm orange accent (the controlled accent-for-life the art board calls for).
            Color stoneCol = new Color(0.50f, 0.52f, 0.50f); // cool low-poly stone grey
            Color logCol = new Color(0.42f, 0.30f, 0.19f);  // warm bark (== ChopTree trunk)
            Color flameCol = new Color(0.98f, 0.55f, 0.16f); // saturated ember-orange

            // --- ring of fire-stones (human-scale: ~0.22u stones in a ~0.7u ring, knee-high pit) ---
            const int stoneCount = 7;
            const float ringR = 0.62f;
            for (int i = 0; i < stoneCount; i++)
            {
                float a = i / (float)stoneCount * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a) * ringR, 0.10f, Mathf.Sin(a) * ringR);
                // Per-stone tonal jitter (quantized small, inline material — no asset churn).
                float j = (i % 3) * 0.03f;
                var col = new Color(stoneCol.r - j, stoneCol.g - j, stoneCol.b - j);
                BuildCampfirePart(visual, "Stone" + i,
                    LowPolyMeshes.FacetedSphere(0.20f + (i % 2) * 0.04f, 0, 0.35f, 4100 + i),
                    col, pos, 0.06f, "CampfireStoneMat" + i);
            }

            // --- two crossed logs in the pit (warm bark, the chopped wood) ---
            BuildCampfireLog(visual, "LogA", logCol, new Vector3(0f, 0.16f, 0f), new Vector3(0f, 0f, 18f), 25f);
            BuildCampfireLog(visual, "LogB", logCol, new Vector3(0f, 0.18f, 0f), new Vector3(0f, 90f, 18f), -25f);

            // --- the flame (a warm low-poly tongue) — its OWN child so Campfire can toggle it with lit ---
            var flameGo = new GameObject("Flame");
            flameGo.transform.SetParent(visual.transform, false);
            flameGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            BuildCampfirePart(flameGo, "FlameCone", LowPolyMeshes.Cone(0.22f, 0.55f, 7),
                flameCol, Vector3.zero, 0.0f, "CampfireFlameMat", emissive: true);
            // A small inner brighter flame for depth (warm yellow core).
            BuildCampfirePart(flameGo, "FlameCore", LowPolyMeshes.Cone(0.12f, 0.40f, 6),
                new Color(1f, 0.82f, 0.35f), new Vector3(0f, 0.04f, 0f), 0.0f, "CampfireFlameCoreMat", emissive: true);
            flameGo.SetActive(false); // ships UNLIT — Campfire shows it on Light()

            // --- the warm point Light (the glow into the Zone-D dusk) — disabled until lit ---
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(pit.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var fireLight = lightGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.66f, 0.32f); // warm firelight
            // Tuned DOWN from the first soak (intensity 3.2/range 9 blew out to a white orb, not a contained
            // fire — caught in the -verifyLoop loop_warm capture). A knee-high campfire casts a SOFT warm
            // pool, not a floodlight: lower intensity + tighter range read as "a real little fire" per the
            // art board (human-scale landmark, controlled warm accent), letting the emissive flame stay the
            // bright focal point rather than a bloom blob.
            fireLight.intensity = 1.5f;
            fireLight.range = 6f;
            fireLight.shadows = LightShadows.None; // thin: no shadow cost on the placeholder
            fireLight.enabled = false; // ships off — Campfire enables it on Light()

            // The Campfire component owns the lit state + warmth restore.
            var fire = pit.AddComponent<Campfire>();
            fire.warmth = Object.FindObjectOfType<WarmthNeed>();
            fire.player = player.transform;
            fire.flameVisual = flameGo;
            fire.fireLight = fireLight;
            if (fire.warmth == null)
                Debug.LogError("[MovementCameraScene] no WarmthNeed in scene to wire Campfire to — " +
                               "BootstrapProject must add the Survival WarmthNeed before MovementCameraScene.Author");

            // The CampfirePlacement component: the wood-gated build interaction.
            var place = pit.AddComponent<CampfirePlacement>();
            place.inventory = Object.FindObjectOfType<Inventory>();
            place.campfire = fire;
            place.player = player.transform;
            place.warmth = fire.warmth;
            if (place.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire CampfirePlacement to");

            // Wire the verification-only shipped-build LOOP capture (-verifyLoop drives the FULL cycle:
            // decay -> craft -> chop -> build fire -> warmth restored) onto the Boot object.
            WireCampfireVerifyCapture(player);

            Debug.Log("[MovementCameraScene] authored Campfire at " + FirePitPosition +
                      " (ships unlit; warmth wired: " + (fire.warmth != null) +
                      ", inventory wired: " + (place.inventory != null) + ")");
        }

        // One log of the campfire: a tapered cylinder laid down (rotated) + tilted, warm bark inline material.
        private static void BuildCampfireLog(GameObject parent, string name, Color col, Vector3 localPos,
            Vector3 euler, float tiltX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            // Lay the cylinder on its side (rotate 90 on Z so its length runs along X) then yaw/tilt per euler.
            go.transform.localRotation = Quaternion.Euler(euler.x, euler.y, 90f) * Quaternion.Euler(tiltX, 0f, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = LowPolyMeshes.TaperedCylinder(0.07f, 0.05f, 0.9f, 6);
            var mr = go.AddComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = name + "Mat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.06f);
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }
        }

        // Build one campfire part (stone / flame cone): a child with a welded smooth-shaded mesh + inline
        // URP/Lit material (serializes into the scene, no .mat churn). emissive=true makes the flame GLOW
        // (warm emission) so it reads as fire even before the point light, and survives the stripped build
        // (URP/Lit emission is built-in, no custom shader to strip).
        private static void BuildCampfirePart(GameObject parent, string name, Mesh mesh, Color color,
            Vector3 localPos, float smoothness, string matName, bool emissive = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = matName };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (emissive)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", color * 1.15f); // warm glow, trimmed so bloom doesn't blow out
                }
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }
        }

        private static void WireCampfireVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host CampfireVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<CampfireVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<CampfireVerifyCapture>();
            cap.player = player.GetComponent<ClickToMove>();
            cap.inventory = Object.FindObjectOfType<Inventory>();
            cap.warmth = Object.FindObjectOfType<WarmthNeed>();
            cap.campfire = Object.FindObjectOfType<Campfire>();
            cap.craftSpot = CraftSpotPosition;
            cap.treeSpot = ChopTreePosition;
            cap.firePit = FirePitPosition;
            var place = Object.FindObjectOfType<CampfirePlacement>();
            if (place != null) cap.woodCost = place.woodCost; // the loop must carry enough wood to the pit
            EditorUtility.SetDirty(bootGo);
        }

        // The player: NavMeshAgent + ClickToMove + the U6 castaway avatar (replaces the U3 capsule
        // placeholder). ClickToMove owns the agent; the camera follows the player root; the
        // CastawayCharacter self-drives its Idle<->Walk anim + facing off the agent's velocity.
        private static GameObject BuildPlayer(ClickMarker markerPrefab, int groundLayer)
        {
            var player = new GameObject("Player");
            // FIRST-FRAME TUNE (86ca8ce7j, absorbs pale-shore 86ca8a0u6): the castaway still "washes
            // ashore" (Sponsor-locked narrative — spawn near the shore), but the ORIGIN spawn sat on the
            // palest, emptiest sand band with the field+trees too far inland to frame, giving the
            // washed-out first frame. Nudge spawn a short way inland to the damp-sand→grass EDGE (Z+6):
            // still ashore (the shore/water is just behind), but now the warm grass band + the near-spawn
            // blob canopies are in the orbit camera's inland view. Stays comfortably on the NavMesh + the
            // loop spots (craft 8,6 / tree -9,-7 / fire 4,-8) remain a short click-walk away.
            player.transform.position = new Vector3(0f, 0f, 6f);

            var agent = player.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.8f;
            agent.speed = 5.5f;
            agent.angularSpeed = 999f;
            agent.acceleration = 30f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;

            // The real 3D player avatar — the "Mini Chibi Kid" sourced CC0-Attribution rigged low-poly
            // chunky-cartoon character (ticket 86ca8ca1m — SUPERSEDES the Quaternius Animated-Men base
            // the realistic head couldn't cartoon-ify). The avatar lives on a child root scaled to the
            // on-screen height; the FBX origin is at the feet (localPosition zero -> grounded feet on
            // the agent's ground point). Built editor-time (BuildInEditor) so the SkinnedMeshRenderer +
            // bones + controller reference SERIALIZE into Boot.unity (the editor-vs-runtime
            // serialization lesson — no Awake-assembled hierarchy to ship mangled). Ensure URP/Lit is
            // always-included so the toon materials don't strip to magenta in the stripped player.
            //
            // NO bone-scale dials: the chibi's big-head toy proportions are INTRINSIC to the mesh (the
            // PR #25 head/limb-scale path is dropped for this base — the mesh ships chunky as imported).
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

            // CONTACT / BLOB SHADOW (ticket 86ca8ca1m — "blob shadow fit to its footprint" AC). A soft
            // dark ground disc under the castaway's feet, fit (radius) to the chibi's blocky stance so
            // the toy-chunky silhouette grounds. Lives on the PLAYER ROOT (NOT the avatar child) so the
            // avatar's height-scale doesn't scale it AND so it stays world-flat under the feet
            // regardless of the avatar's yaw/anim. Editor-time authored (mesh + inline transparent
            // vertex-color material) so it serializes into Boot.unity — the editor-vs-runtime trap.
            BuildBlobShadow(player);

            var ctm = player.AddComponent<ClickToMove>();
            ctm.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            ctm.markerPrefab = markerPrefab;

            return player;
        }

        // ---- Blob/contact shadow anchors (ticket 86ca8ca1m). FIT to the chibi footprint. Radius in
        // world units (the player root is unscaled at 1u, so this is the on-ground size). Tone = a soft
        // warm-neutral dark (not pure black — pure black reads harsh under the warm Zone-D key); alpha =
        // the disc's opaque-core falloff strength. Exposed for the scene-presence test. ----
        public const string BlobShadowObjectName = "BlobShadow";
        public static readonly Color BlobShadowColor = new Color(0.08f, 0.07f, 0.06f); // soft warm-dark
        // The chibi's intrinsic footprint half-extent in its ~1u-normalized local space is ~0.36 (X) /
        // ~0.42 (Z) (probe: intrinsic X 1.44u / Z 1.68u over a 1.82u-tall mesh, normalized to 1u). On
        // the PlayerVisualHeight=1.8 avatar root that is ~0.65 (X) / ~0.76 (Z) world half-extent — but
        // the Z extent is dominated by the toes/heel spread, not the standing footprint. A radius of
        // 0.55 grounds the standing-pose footprint (feet + a soft margin) without a saucer that extends
        // past the silhouette. Soak-tunable if the Sponsor wants a wider/tighter pool.
        public const float BlobShadowRadius = 0.55f;
        public const float BlobShadowCenterAlpha = 0.5f;  // soft, not a hard black disc

        // Build the castaway's contact/blob shadow as a flat ground disc under the player root. The disc
        // mesh bakes a radial alpha falloff (LowPolyMeshes.BlobShadowDisc); an inline transparent
        // vertex-color material (FarHorizon/BlobShadowVertexColor, always-included so it survives the
        // stripped build) renders it as a soft dark pool. Sits just above y=0 to avoid z-fighting the
        // ground. NO collider — it must never block the click raycast or the NavMesh bake.
        private static void BuildBlobShadow(GameObject player)
        {
            var go = new GameObject(BlobShadowObjectName);
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f); // hover a hair to beat z-fight
            go.transform.localRotation = Quaternion.identity;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = LowPolyMeshes.BlobShadowDisc(BlobShadowRadius, 18, BlobShadowColor, BlobShadowCenterAlpha);
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // a fake shadow casts none
            mr.receiveShadows = false;

            var vc = Shader.Find("FarHorizon/BlobShadowVertexColor");
            if (vc != null)
            {
                var mat = new Material(vc) { name = "BlobShadowMat" };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Color.white);
                mr.sharedMaterial = mat; // inline -> serializes into the scene, no .mat churn
                EnsureShaderAlwaysIncluded(vc);
            }
            else
            {
                // Fallback: an unlit transparent flat disc (vertex falloff lost — a flat dark disc — but
                // never magenta). Tints the URP/Unlit base to the shadow tone with a fixed alpha.
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit != null)
                {
                    var mat = new Material(unlit) { name = "BlobShadowMat" };
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", new Color(BlobShadowColor.r, BlobShadowColor.g,
                                                             BlobShadowColor.b, BlobShadowCenterAlpha));
                    if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mr.sharedMaterial = mat;
                    EnsureShaderAlwaysIncluded(unlit);
                }
                Debug.LogWarning("[MovementCameraScene] blob-shadow vertex-color shader not found; flat-disc fallback");
            }

            Debug.Log("[MovementCameraScene] authored BlobShadow under Player (radius=" + BlobShadowRadius +
                      ", fit to chibi stance)");
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
            orbit.defaultPitch = 55f;   // Sponsor-preferred top-down-ish framing (inside 35-70) — LOCKED
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

        // Attach the verification-only SEA capture to the Boot object (the -verifySea orbit-to-seaward
        // ocean shot). Inert unless launched with -verifySea. Serialized into the scene editor-time (NOT
        // Awake) per the editor-vs-runtime trap; WaterSceneTests guards its serialized presence.
        private static void WireSeaVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] 'Boot' object not found — sea-verify capture not wired");
                return;
            }
            if (bootGo.GetComponent<SeaVerifyCapture>() == null)
                bootGo.AddComponent<SeaVerifyCapture>();
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
