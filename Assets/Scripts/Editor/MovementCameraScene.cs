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

        // ---- Hero axe (ticket 86ca8ce6y — RE-DONE). The procedural HeroAxeMesh wedge is RETIRED (it did
        // not read as an axe); the axe is now the SOURCED rustic hatchet "One-handed stylized axe" by
        // Viktor.G (Sketchfab, CC-Attribution — see AxeAssetGen + the committed license file). It is
        // ATTACHED to the chibi's RIGHT HAND bone (RightHand_010 — probe-verified, see AxeAssetGen
        // DiagnoseTrace) so the castaway HOLDS it, and is shown only once crafted (HeldAxe gates on
        // Inventory.HasAxe). Name kept "HeroAxe" so the verify-capture + scene-presence guards key on it. ----
        public const string HeroAxeObjectName = "HeroAxe";

        // ---- HAIR (ticket 86ca8ca1m soak-fix). The Sponsor soaked 46f2a9d: the castaway read as wearing
        // a CAP, not hair. The original cap meshes (dome Object_41 + brim Object_42) are HIDDEN by
        // CastawayCharacter.HideCap (the dome alone left a cap-shaped arc sticking up — not hair). A clean
        // procedural sandy-ginger HAIR skull-cap (LowPolyMeshes.HairCap) is parented to the chibi's HEAD
        // bone so it rides the head every frame, sized + seated on the crown. Editor-time authored (the
        // mesh + inline material) so it SERIALIZES into Boot.unity (the editor-vs-runtime trap). ----
        public const string HairObjectName = "CastawayHair";
        public const string HeadBoneToken = "head";
        public static readonly Color HairMeshColor = new Color(0.86f, 0.55f, 0.26f); // sandy-ginger (warm R>G>B; lifted so the shadowed dome facets don't read dark-brown)
        // The head bone carries the chibi's huge import-compensation lossy scale (PROBE-VERIFIED 267.30×,
        // like the right-hand bone — see the axe ScaleTrace finding). A child mesh INHERITS it, so the
        // hair's head-local pose + scale must be tiny. Derived from the shipped capture + the scale probe:
        // the skull (Object_36) is ~1.42u wide in world; a hair-cap radius ~0.32u world reads as a snug
        // skull-cap, so localScale ~0.0012 (0.0012 × 267.3 × radius 1.0 ≈ 0.32u world radius). LocalPos
        // rides the 267× too: localY ~0.0030 lifts the cap ~0.8u above the head bone (worldY 0.78) to sit
        // on the crown (~1.6u).
        // SOAKFIX7 (86ca8ca1m — REVERT-TO-FLAT): the soakfix6 TuftedHair lobes read as orange
        // spikes/antlers poking out of the dome (Sponsor: "id rather have the FLAT hair than this"). Hair is
        // back to the clean FLAT MessyHairCap (the pre-TuftedHair 5f7e7ba state WITH the soakfix5 front-
        // fringe fix). The close-up SEAM fix (86ca8m3t2) is KEPT: localY stays DROPPED (0.0029 -> 0.0026)
        // and the cap cut is DEEPENED (-0.15 -> -0.30) so the flat dome's skirt dips below the skull crown
        // and overlaps it flush (no air gap at hero magnification — pinned by CastawayHair_SeatsFlushToHeadBone).
        public static readonly Vector3 HairLocalScale = new Vector3(0.00104f, 0.00098f, 0.00108f);
        public static readonly Vector3 HairLocalPos = new Vector3(0f, 0.0026f, 0.00020f);
        // MessyHairCap generation params — named so the EditMode crown-spread guard builds the EXACT
        // shipped mesh (no magic-number drift between the scene build and the test).
        public const float HairCapRadius = 1.0f;
        public const float HairCapYScale = 0.88f;
        public const float HairCapCut = -0.30f;   // skirt drops below the crown so the flat cap seats flush on the skull (anti-seam, 86ca8m3t2)
        public const int HairCapSubdiv = 3;
        public const float HairCapJitter = 0.34f;
        public const int HairCapSeed = 73101;

        // The chibi rig's right-hand bone (probe-verified by AxeAssetGen.DiagnoseTrace, 2026-06-13). The
        // rig also carries a "RightHand.Dummy_011" helper bone — the attach search matches the real
        // RightHand_010 by token and EXCLUDES "dummy" (same trap class as the mesh-group "head" node vs
        // the Head_05 bone, unity-conventions.md / CastawayProportions).
        public const string RightHandBoneToken = "righthand";

        // Attach pose for the sourced hatchet (SOAKFIX2 2026-06-13 — the NO-AXE soak fix). The held axe must
        // read UNMISTAKABLY in the GAMEPLAY orbit view (dist 14u, pitch 55°), not just the -verifyAxe close-up.
        //
        // ROOT CAUSE the gameplay-view trace (AxeAssetGen.GameplayViewTrace) PROVED the Sponsor's "no axe":
        // the prior pose (scale 0.0015 → 0.43u longest, blade-DOWN at the HIP, max.y=0.71) projected to only
        // ~3.7% of the frame height at the orbit framing — a thin vertical sliver hanging by the leg, lost
        // beside a chibi whose own silhouette is only ~8% of frame from 55° top-down. The -verifyAxe capture
        // ZOOMS its own camera to whatever size the axe is, so it framed a perfect hatchet — FALSE-GREEN, the
        // exact class that bit the castaway. (See gameplay_orbit_axe.png evidence in the PR body.)
        //
        // THE BONE'S LOCAL FRAME IS ROTATED (PROBE-VERIFIED): RightHand_010's local +Y maps to world
        // (0.48,-0.84,0.23) — mostly DOWN. So localPosition.y does NOT lift the axe world-up (a lift sweep via
        // localPos went the WRONG way). Fix: pose the held axe in WORLD space after parenting (Unity back-
        // solves + serializes the local transform), so the pose is intuitive + robust to the bone frame.
        //
        // VALUES (dialed via AxeAssetGen.DialInTrace + SOAKFIX4 PoseTrace against the orbit render):
        // SOAKFIX4 (the Sponsor's "handle sticks out by the EAR"): the prior +0.55 up-offset seated the axe
        // CENTER at world-y 0.66 with its TOP at 1.040 — and the chibi's head bone is at world-y 0.775, so
        // the haft top rose level with the EAR (PoseTrace: HeroAxe max.y 1.040 vs head-bone 0.775; the huge
        // head + short arms put "chest height" at ear height). FIX: DROP the up-offset +0.55 -> +0.05 so the
        // axe rides at the hand at the kid's SIDE (handle top now clears below the shoulder, well under the
        // head), and re-pitch the euler to BLADE-DOWN-AT-THE-SIDE (head/blade hangs toward the hip, handle
        // angled up just past the hand) — NOT the prior blade-flat-up tip that lifted the haft. A residual
        // tilt is kept (not dead-vertical) so the head still reads as an axe-head shape from the 55° top-down
        // orbit (a fully vertical prop foreshortens to a dot — the visibility lesson). Scale 0.0040 (×267
        // lossy ≈ 1.0u longest) UNCHANGED — the size already read as a clear hero hatchet; only height+pitch
        // move. Re-measured by PoseTrace post-change; final read is the SHIPPED build (editor RT is
        // framing-only, not colour — unity-conventions.md).
        // SOAKFIX7 (86ca8ce6y — the held axe BAKED at the Sponsor's dialed-in grip). The Sponsor finalized
        // the in-hand seat IN-GAME via the build-gated AxeNudgeTool (F9) and confirmed it "perfect"; these
        // are his last reported nudge-panel values, baked as the held DEFAULT (replacing the soakfix6
        // placeholder grip). offsetFromHand pulls the haft into the palm (gap closed); the euler orients the
        // hatchet so it reads as GRIPPED, not a hanging prop. Parented to RightHand_010 (tracks Idle/Walk).
        // Scale unchanged (~1.0u reads at the orbit). The F9 AxeNudgeTool stays build-gated/inert for any
        // future re-tune. Sponsor-reported values (European decimal commas -> dot-decimal here).
        public static readonly float HeldAxeLocalScaleUniform = 0.0040f;
        public static readonly Vector3 HeldAxeWorldOffsetFromHand = new Vector3(-0.018f, -0.038f, -0.021f);
        public static readonly Vector3 HeldAxeWorldEuler = new Vector3(0.0f, 9.9f, 124.7f);

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

            // M-U3-SCENE-4 (86ca8feuf): shipwreck debris at the landing. A MODEST washed-ashore scatter
            // — a few weathered planks + a half-buried crate + a barrel — on the beach just SEAWARD of the
            // spawn, narrating "the castaway crawled out of that sea" (Sponsor: NARRATE; Uma §3 beat 4).
            // Diegetic set-dressing ONLY: NO collider on any piece, so it never blocks the click-move
            // ground raycast or the NavMesh bake (built BEFORE the bake; collider-free pieces don't
            // contribute to the PhysicsColliders bake). Authored editor-time so the meshes + inline
            // materials SERIALIZE into Boot.unity (editor-vs-runtime "legs-up" trap); BeachDebrisSceneTests
            // guards its serialized presence + the no-collider contract.
            BuildBeachDebris(groundLayer);

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

            // Wire the verification-only shipped-build ROCK capture (86ca8m5zu — the boulder soak-fix). The
            // default orbit frames the SPAWN; the rocks live as outcrops in the FIELD band, so this drives a
            // multi-angle orbit onto the outcrop centroid in the BUILT exe so the boulders are judged from
            // gameplay distance (not a hero close-up). Inert unless launched with -verifyRock.
            WireRockVerifyCapture();

            Debug.Log("[MovementCameraScene] authored player + orbit camera + flat ground + NavMesh");
        }

        private static void EnsureDirs()
        {
            foreach (var d in new[] { NavMeshDir, PrefabDir, SettingsDir })
                Directory.CreateDirectory(Path.GetFullPath(d));
            AssetDatabase.Refresh();
        }

        // Seaward edge of the flat test ground. The seaward slab spanned Z[-30..+30] at Y=0; the trim
        // to -10 (drew/beach-water) stopped it overhanging the water's near band. Kept at -10 — the
        // ROOT-CAUSE of the invisible sea was NOT this slab occluding the water (the magenta-diff +
        // -seaWaterOnly probe proved the sea rendered ZERO px even with BOTH grounds hidden): it was the
        // water mesh's INVERTED triangle winding (faces pointed DOWN -> Cull Back hid the sea from the
        // above-camera). Fixed in LowPolyZoneGen.BuildWaterEdge. This slab edge stays as the prior trim.
        private const float SeawardGroundZ = -10f;

        // A flat subdivided plane on the Ground layer with a MeshCollider, so the NavMesh bake
        // (PhysicsColliders collection) AND the click-to-move ground raycast both hit it.
        // Subdivided (not a single quad) so the baked NavMesh has clean geometry.
        //
        // NON-RENDERING (drew/ocean-beach-soakfix2, soak #40 stamp 31ce95c). The Sponsor saw a "gray slab
        // on the beach" breaking the sand read. Diagnostic trace (centerline Y compare, MovementCameraScene
        // vs LowPolyZoneGen height fields): this flat Y=0 placeholder slab pokes ABOVE the SANDY Zone-D
        // terrain across the seaward foreshore band (Z ~ -10..+3) — exactly where the beach DIPS toward the
        // sea (terrain Y goes -0.53 -> -0.02, all below this slab's Y=0). Its muted moss-grey (0.42,0.46,0.40)
        // top was therefore the topmost surface on the beach -> the grey slab. (Inland of ~Z+4 the sand rises
        // above Y=0 and already hid it, which is why it only showed ON the beach.) The slab is a pure dev
        // placeholder ("U5 will replace the environment surface; not art") whose ONLY load-bearing role is
        // its COLLIDER (NavMesh bake + click-raycast); the Zone-D terrain is the real visible ground. Fix:
        // keep the collider (NavMesh + click-move unchanged — the player walks the SAME baked surface as
        // before, so no float/path regression), DISABLE the renderer so the sandy terrain is the only thing
        // drawn on the beach. The MeshRenderer is kept-but-disabled (not removed) so .bounds still resolves
        // for WaterSceneTests.Ocean_NotOccludedByFlatGround_SeawardSlabTrimmed.
        private static GameObject BuildFlatGround(int groundLayer)
        {
            var go = new GameObject("TestGround");
            go.layer = groundLayer;
            go.transform.position = Vector3.zero;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            const int seg = 20;
            float sizeX = GroundHalf * 2f;
            // Asymmetric Z span: seaward edge trimmed to SeawardGroundZ; inland edge stays at +GroundHalf.
            float minZ = SeawardGroundZ, maxZ = GroundHalf;
            var verts = new Vector3[(seg + 1) * (seg + 1)];
            var uvs = new Vector2[verts.Length];
            for (int z = 0; z <= seg; z++)
                for (int x = 0; x <= seg; x++)
                {
                    int i = z * (seg + 1) + x;
                    float fx = (float)x / seg, fz = (float)z / seg;
                    verts[i] = new Vector3((fx - 0.5f) * sizeX, 0f, Mathf.Lerp(minZ, maxZ, fz));
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

            // A simple, build-safe URP/Lit material kept on the (disabled) renderer so it never ships pink
            // if anything re-enables it. U5 will replace the environment surface; this is a neutral
            // placeholder, not art — and now it does NOT draw (see the NON-RENDERING note above).
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

            // The slab is a COLLISION/NAVMESH proxy only — the sandy Zone-D terrain is the visible ground.
            // Disable rendering so the grey placeholder slab no longer pokes through the beach (soak #40).
            mr.enabled = false;

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

        // Name of the serialized axe-on-the-stump GameObject (SOAKFIX2). Distinct from HeroAxeObjectName so
        // the -verifyAxe HeroAxe search + the held-axe scene guard never resolve the stump one by mistake.
        public const string StumpAxeObjectName = "StumpAxe";

        // The axe-on-the-stump's pose, in CraftSpot-LOCAL space (the CraftSpot is unscaled at world 1u, so
        // these are intuitive world units — NO 267× bone trap here, unlike the held axe). The stump top sits
        // at world-y ≈ 0.70 (cylinder localScale.y 0.35 → 0.70 tall; PoseTrace: CraftStump TOP.y 0.700).
        //
        // SOAKFIX4 (the Sponsor's "head BURIED in the block, only the handle pokes out"): the prior pose
        // (localPos.y 1.15, scale 1.4, near-vertical) put the axe HEAD — which is the LOW half of the mesh
        // (PoseTrace: longAxis Y, the wide steel end is local-Y -0.974..-0.474) — DOWN at world min.y -0.266,
        // i.e. BELOW the ground and buried inside the block; only the thin haft (the HIGH half) rose above the
        // 0.700 stump top. FIX: pose it as an axe STUCK IN the block — the HEAD's blade edge bites at/into the
        // TOP of the block (~0.70) and the HAFT angles UP-and-out so the whole handle reads above the block.
        // Done by (a) RAISING localPos.y so the head sits at the top not the bottom, (b) LEANING the axe ~26°
        // off vertical (a lodged-in-the-block axe leans, it does not stand to attention), (c) trimming the
        // scale 1.4 -> 1.1 so the head doesn't overhang the small block. Re-measured by PoseTrace post-change
        // (head bottom at ~block-top, handle top ~1.4 clearly visible from spawn). Final read is the SHIPPED
        // build (editor RT is framing/size-only, not colour — unity-conventions.md).
        // SOAKFIX7 (86ca8ce6y — the stump axe BAKED at the Sponsor's dialed-in pose). The Sponsor finalized
        // the in-block transform IN-GAME via the build-gated AxeNudgeTool (F9: cycles held/stump target,
        // nudges XYZ + pitch/yaw/roll, reads the live values off the HUD) and confirmed it "perfect"; these
        // are his last reported nudge-panel values, baked as the stump DEFAULT (replacing the soakfix5
        // placeholder). The axe reads as stuck SQUARELY in the block — head biting the top, haft angled
        // up-and-out. The F9 AxeNudgeTool stays build-gated/inert for any future re-tune. Sponsor-reported
        // values (European decimal commas -> dot-decimal here). Scale unchanged (1.1u reads at spawn).
        public static readonly Vector3 StumpAxeLocalPos = new Vector3(-0.210f, 1.540f, 0.430f);
        public static readonly Vector3 StumpAxeLocalEuler = new Vector3(12.0f, 53.0f, 48.0f);
        public static readonly float StumpAxeLocalScaleUniform = 1.1f;

        // The craft spot (U2-2, 86ca8bdaq): a low-poly marker the castaway click-moves to; reaching it
        // crafts the axe. A small chopping-block stump the castaway walks ONTO. NO collider so it never
        // blocks the ground raycast or the NavMesh. The stump mesh + CraftSpot's Inventory/player refs are
        // authored editor-time so they serialize into Boot.unity (editor-vs-runtime trap — an Awake-built
        // prop ships mangled, the "legs-up" class).
        //
        // SOAKFIX2 (the Sponsor's "stump is there but no axe"): an axe is PLANTED in the stump and visible
        // FROM SPAWN (StumpAxe component, the inverse gate of HeldAxe). It is the always-on-screen hero axe +
        // the diegetic "walk here" cue. On reaching the spot the craft fires: the stump-axe HIDES and the
        // HELD axe APPEARS (AttachHeroAxeToHand) — reading as "the kid picks it up".
        private static void BuildCraftSpot(GameObject player, int groundLayer)
        {
            var spot = new GameObject("CraftSpot");
            spot.transform.position = CraftSpotPosition;

            // A small low cylinder as the "chopping block" the player walks toward. Primitive cylinder,
            // collider stripped. The hero axe is planted in it (below).
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

            // SOAKFIX2: plant the always-visible-from-spawn axe in the stump (the Sponsor's literal ask).
            AttachStumpAxe(spot);

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

        // Attach the SOURCED hero axe (ticket 86ca8ce6y — RE-DONE) to the chibi's RIGHT HAND bone so the
        // castaway HOLDS the hatchet. The procedural HeroAxeMesh wedge is RETIRED. The imported FBX
        // (AxeAssetGen — rustic leather-wrapped hatchet, single mesh + baseColor atlas) is instantiated as
        // a child of the RightHand_010 bone (probe-verified by AxeAssetGen.DiagnoseTrace) so it RIDES the
        // hand's animated transform every frame; the attach-local pose puts the handle in the palm + the
        // blade forward, and the attach-local scale brings the normalized prop to a believable hatchet size.
        //
        // SERIALIZATION (unity-conventions.md §editor-vs-runtime): called AFTER the avatar is built
        // editor-time (castaway.BuildInEditor in BuildPlayer), so the bone hierarchy exists and the axe
        // child SERIALIZES into Boot.unity under the bone — NOT an Awake-built attach (the "legs-up" /
        // component-not-serialized classes). A HeldAxe runtime component gates the renderer on
        // Inventory.HasAxe (hidden until crafted; the craft reads as "the kid picks up the axe").
        private static void AttachHeroAxeToHand(GameObject player)
        {
            var castaway = player.GetComponentInChildren<CastawayCharacter>(true);
            if (castaway == null)
            {
                Debug.LogError("[MovementCameraScene] no CastawayCharacter to attach the hero axe to");
                return;
            }

            Transform hand = FindRightHandBone(castaway.transform);
            if (hand == null)
            {
                Debug.LogError("[MovementCameraScene] could not find the chibi's right-hand bone ('" +
                               RightHandBoneToken + "', excl. dummy) — the hero axe has nothing to attach to. " +
                               "Re-run AxeAssetGen.DiagnoseTrace to dump the rig.");
                return;
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(AxeAssetGen.FbxPath);
            if (fbx == null)
            {
                Debug.LogError("[MovementCameraScene] sourced axe FBX not found at " + AxeAssetGen.FbxPath +
                               " — run AxeAssetGen.PrepareAxe() before authoring the scene");
                return;
            }

            // Instantiate the imported hatchet under the hand bone (editor-time -> serializes into
            // Boot.unity). Plain Instantiate (not InstantiatePrefab) — same idiom as the chibi avatar
            // (CastawayCharacter.BuildModel): bakes the mesh/renderer into the scene with no prefab link.
            var axe = Object.Instantiate(fbx);
            axe.name = HeroAxeObjectName;
            axe.transform.SetParent(hand, false);
            // Scale is uniform-local (rides the 267× bone lossy → ~1.0u). Position + rotation are set in
            // WORLD space AFTER parenting (the bone's local frame is rotated, so local-Y ≠ world-up — see
            // the const-block finding); Unity back-solves the local transform that serializes into Boot.unity.
            axe.transform.localScale = Vector3.one * HeldAxeLocalScaleUniform;
            axe.transform.position = hand.position + HeldAxeWorldOffsetFromHand;
            axe.transform.rotation = Quaternion.Euler(HeldAxeWorldEuler);

            // Gate visibility on HasAxe: hidden until the craft fires, then the kid is holding it.
            var held = axe.GetComponent<HeldAxe>();
            if (held == null) held = axe.AddComponent<HeldAxe>();
            held.inventory = Object.FindObjectOfType<Inventory>();

            // URP/Lit (the imported material) must survive the stripped build.
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null) EnsureShaderAlwaysIncluded(litShader);

            // Wire the verification-only shipped-build AXE CLOSE-UP capture onto the Boot object — sibling
            // of the craft/chop/movement verify captures. Inert unless launched with -verifyAxe. It frames
            // the held hatchet close so the silhouette/leather-wrap read rides a committed shipped-build path.
            WireAxeVerifyCapture();

            // Wire the BUILD-GATED debug AxeNudgeTool (86ca8ce6y SOAKFIX5) — inert behind the F9 toggle, lets
            // the Sponsor dial + read off the final held/stump axe transforms in-game (the axe-nudge reframe).
            WireAxeNudgeTool();

            int rendCount = axe.GetComponentsInChildren<MeshRenderer>(true).Length;
            Debug.Log("[MovementCameraScene] attached HeroAxe (sourced hatchet) to bone '" + hand.name +
                      "' (renderers=" + rendCount + ", HasAxe-gated)");
        }

        // SOAKFIX2: plant the SOURCED hatchet in the chopping-block stump so an axe is VISIBLE FROM SPAWN
        // (the Sponsor's literal "stump is there but no axe"). Same sourced FBX as the held axe — one asset,
        // identical read. Parented to the CraftSpot (unscaled world-1u, so NO 267× bone trap — the local
        // pose is intuitive). A StumpAxe component gates it as the INVERSE of HasAxe: shown at spawn, HIDDEN
        // once crafted (the held axe appears at the same instant → "the kid picks it up"). Editor-time so
        // the axe mesh + StumpAxe wiring SERIALIZE into Boot.unity (the editor-vs-runtime trap).
        private static void AttachStumpAxe(GameObject craftSpot)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(AxeAssetGen.FbxPath);
            if (fbx == null)
            {
                Debug.LogError("[MovementCameraScene] sourced axe FBX not found at " + AxeAssetGen.FbxPath +
                               " — run AxeAssetGen.PrepareAxe() before authoring the scene; no stump axe planted");
                return;
            }

            var axe = Object.Instantiate(fbx);
            axe.name = StumpAxeObjectName;
            axe.transform.SetParent(craftSpot.transform, false);
            axe.transform.localPosition = StumpAxeLocalPos;
            axe.transform.localRotation = Quaternion.Euler(StumpAxeLocalEuler);
            axe.transform.localScale = Vector3.one * StumpAxeLocalScaleUniform;

            // Gate visibility as the INVERSE of HasAxe: shown at spawn, hidden once crafted.
            var stump = axe.GetComponent<StumpAxe>();
            if (stump == null) stump = axe.AddComponent<StumpAxe>();
            stump.inventory = Object.FindObjectOfType<Inventory>();

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null) EnsureShaderAlwaysIncluded(litShader);

            int rendCount = axe.GetComponentsInChildren<MeshRenderer>(true).Length;
            Debug.Log("[MovementCameraScene] planted StumpAxe in the chopping block (renderers=" + rendCount +
                      ", inverse-HasAxe-gated, visible from spawn)");
        }

        // Attach the procedural sandy-ginger HAIR skull-cap to the chibi's HEAD bone (86ca8ca1m soak-fix).
        // The original cap meshes are hidden (CastawayCharacter.HideCap); this is the replacement hair so
        // the castaway reads young/hopeful with a head of hair. Parented to the head bone so it rides the
        // head's animated transform; the head-local pose+scale are tiny because the head bone carries the
        // chibi's huge import-compensation lossy scale (the same finding as the right-hand bone / axe).
        // Editor-time (mesh + inline URP/Lit material) so it SERIALIZES into Boot.unity — the
        // editor-vs-runtime trap (an Awake-built attach ships mangled / absent).
        private static void AttachHair(CastawayCharacter castaway)
        {
            Transform head = CastawayProportions.FindHeadBone(castaway.transform);
            if (head == null)
            {
                Debug.LogError("[MovementCameraScene] no Head bone found to attach hair to — the cap->hair " +
                               "fix leaves a bald crown. Re-run the rig dump.");
                return;
            }

            var hair = new GameObject(HairObjectName);
            hair.transform.SetParent(head, false);
            hair.transform.localPosition = HairLocalPos;
            hair.transform.localRotation = Quaternion.identity;
            hair.transform.localScale = HairLocalScale;

            var mf = hair.AddComponent<MeshFilter>();
            // MESSY hair (86ca8ca1m — FLAT dome, SOAKFIX7 revert). The soakfix6 TuftedHair lobe redo read as
            // orange spikes/antlers poking out of the brown dome (Sponsor: "id rather have the FLAT hair than
            // this"), so the hair is back to the clean FLAT MessyHairCap: the same cut-dome jittered into
            // faceted tufts with the apex de-spiked (no single pole point) and the soakfix5 front-fringe tame
            // (no forward-jutting brow lobe). subdiv 3 for tuft definition; jitter 0.34; seed deterministic.
            // The close-up SEAM fix (86ca8m3t2) is KEPT via the deepened cut (-0.30) + dropped localY so the
            // flat skirt seats flush on the skull (pinned by CastawayHair_SeatsFlushToHeadBone).
            mf.sharedMesh = LowPolyMeshes.MessyHairCap(
                HairCapRadius, HairCapYScale, HairCapCut, HairCapSubdiv, HairCapJitter, HairCapSeed);
            var mr = hair.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = "CastawayHairMat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", HairMeshColor);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f); // matte hair
                mr.sharedMaterial = mat; // inline -> serializes into the scene, no .mat churn
                EnsureShaderAlwaysIncluded(litShader);
            }
            Debug.Log("[MovementCameraScene] attached CastawayHair skull-cap to head bone '" + head.name +
                      "' (cap meshes hidden; sandy-ginger hair)");
        }

        // Find the chibi's right-hand bone from the SKINNED-MESH BONE ARRAY (the actual skeleton — skips
        // mesh-group nodes). Matches the RightHandBoneToken and EXCLUDES "dummy" (the rig carries a
        // RightHand.Dummy_011 helper — same duplicate-name trap class as the mesh-group "head" node vs the
        // Head_05 bone, unity-conventions.md / CastawayProportions). Falls back to a transform scan.
        private static Transform FindRightHandBone(Transform avatarRoot)
        {
            var smr = avatarRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.bones != null)
            {
                foreach (var bone in smr.bones)
                {
                    if (bone == null) continue;
                    string n = bone.name.ToLowerInvariant();
                    if (n.Contains(RightHandBoneToken) && !n.Contains("dummy") && !n.Contains("end"))
                        return bone;
                }
            }
            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains(RightHandBoneToken) && !n.Contains("dummy") && !n.Contains("end"))
                    return t;
            }
            return null;
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

        // Wire the BUILD-GATED debug AxeNudgeTool onto the Boot object so it SERIALIZES into Boot.unity (the
        // editor-vs-runtime trap — a component in source but not in the scene ships inert). The tool is INERT
        // in normal play (asleep behind the F9 toggle), so it never affects a soak; it lets the Sponsor dial
        // + read off the final axe transforms in-game (86ca8ce6y SOAKFIX5 — the axe-nudge reframe).
        private static void WireAxeNudgeTool()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host AxeNudgeTool");
                return;
            }
            if (bootGo.GetComponent<AxeNudgeTool>() == null)
                bootGo.AddComponent<AxeNudgeTool>();
            EditorUtility.SetDirty(bootGo);
        }

        private static void WireCastawayVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host CastawayVerifyCapture");
                return;
            }
            if (bootGo.GetComponent<CastawayVerifyCapture>() == null)
                bootGo.AddComponent<CastawayVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        private static void WireHairVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host HairVerifyCapture");
                return;
            }
            if (bootGo.GetComponent<HairVerifyCapture>() == null)
                bootGo.AddComponent<HairVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
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

        // ---- M-U3-SCENE-4 (86ca8feuf): washed-ashore shipwreck debris ----
        // Centre of the debris scatter: on the beach just SEAWARD of the locked spawn (Z+6), slightly
        // LEFT of centre so it reads in the seaward orbit-cam's foreground WITHOUT sitting on the
        // spawn->craft (8,6) / chop (-9,-7) / fire (4,-8) loop path. Z-3 is on the warm sand band the
        // seaward gameplay view frames in its lower-foreground (between spawn and the waterline ~Z-10.5).
        public static readonly Vector3 BeachDebrisCenter = new Vector3(-3.2f, 0f, -3.0f);

        // Warm-brown weathered wood family — the axe-haft / chop-trunk / campfire-log palette
        // (style-guide-v2 §6; == ChopTree trunkCol / Campfire logCol). Sub-1.0, HDR-clamp-safe.
        private static readonly Color DebrisWood     = new Color(0.42f, 0.30f, 0.19f); // warm bark plank
        private static readonly Color DebrisWoodWorn = new Color(0.36f, 0.27f, 0.18f); // slightly greyed/weathered
        private static readonly Color DebrisCrate    = new Color(0.47f, 0.34f, 0.21f); // a touch lighter crate timber

        // A MODEST, tasteful shipwreck scatter. Chunky-cartoon faceted toy pieces (board v2): a few
        // weathered planks lying flat / askew + a half-buried crate + a barrel on its side. Purposeful,
        // not clutter (style-guide-v2 §5 "decoration serves the anchor"). NO colliders anywhere — pure
        // set-dressing; the player click-moves freely THROUGH the debris (AC2: must not block pathing or
        // the ground raycast). Built editor-time + serialized into Boot.unity (no Awake assembly).
        private static void BuildBeachDebris(int groundLayer)
        {
            var root = new GameObject("BeachDebris");
            root.transform.position = BeachDebrisCenter;
            // The debris sits on the Default layer (NOT Ground) so even if a future change adds a collider
            // by mistake, it wouldn't silently join the Ground raycast mask — but the real guarantee is
            // that no piece gets a collider at all (asserted by BeachDebrisSceneTests).

            // --- a few weathered planks: thin flat boxes lying on the sand at slight yaws + tilts, as if
            //     washed up and dropped. Local offsets keep them a loose, natural-looking pile. ---
            // (localPos, eulerYaw, lengthScale, tiltDeg, color)
            BuildDebrisPlank(root, "PlankA", new Vector3(0.0f, 0.06f, 0.0f),  18f, 1.9f,  2f, DebrisWood);
            BuildDebrisPlank(root, "PlankB", new Vector3(0.7f, 0.05f, 0.5f), -34f, 1.6f, -3f, DebrisWoodWorn);
            BuildDebrisPlank(root, "PlankC", new Vector3(-0.6f, 0.09f, -0.4f), 62f, 1.4f,  6f, DebrisWood);
            BuildDebrisPlank(root, "PlankD", new Vector3(0.2f, 0.14f, -0.7f),  -8f, 1.2f, 14f, DebrisWoodWorn);

            // --- a half-buried crate: a chunky cube tilted + sunk so it reads "dug into the wet sand". ---
            BuildDebrisCrate(root, "Crate", new Vector3(-1.5f, 0.12f, 0.7f), new Vector3(8f, 22f, -6f), 0.62f);

            // --- a barrel on its side: a stout tapered cylinder laid down, weathered, rolled to a stop. ---
            BuildDebrisBarrel(root, "Barrel", new Vector3(1.6f, 0.28f, -0.3f), 74f);

            Debug.Log("[MovementCameraScene] authored BeachDebris at " + BeachDebrisCenter +
                      " (planks+crate+barrel; NO colliders — non-blocking set-dressing)");
        }

        // A weathered plank: a thin flat box (primitive Cube, collider stripped) laid flat on the sand,
        // yawed + slightly tilted. Inline matte URP/Lit warm-brown material (serializes into the scene).
        private static void BuildDebrisPlank(GameObject parent, string name, Vector3 localPos,
            float yaw, float lengthScale, float tilt, Color col)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // set-dressing: never blocks raycast/NavMesh
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(tilt, yaw, 0f);
            // Long, narrow, thin: a board. lengthScale ~1.2-1.9u long, ~0.18u wide, ~0.06u thick.
            go.transform.localScale = new Vector3(lengthScale, 0.06f, 0.18f);
            ApplyDebrisMaterial(go, name + "Mat", col);
        }

        // A half-buried crate: a chunky cube, tilted + sunk into the sand (low Y + a downward tilt so the
        // far corner dips below the surface). Collider stripped.
        private static void BuildDebrisCrate(GameObject parent, string name, Vector3 localPos,
            Vector3 euler, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * size;
            ApplyDebrisMaterial(go, name + "Mat", DebrisCrate);
        }

        // A barrel on its side: a stout tapered cylinder laid down (rotated 90 on Z so its length runs
        // along X) + yawed, rolled to rest in the sand. Reuses the faceted low-poly cylinder idiom.
        private static void BuildDebrisBarrel(GameObject parent, string name, Vector3 localPos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 90f);
            var mf = go.AddComponent<MeshFilter>();
            // Slightly barrel-bellied: wider mid via near-equal end radii on a short stout body.
            mf.sharedMesh = LowPolyMeshes.TaperedCylinder(0.26f, 0.26f, 0.7f, 8);
            var mr = go.AddComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = name + "Mat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", DebrisWoodWorn);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }
        }

        // Inline matte URP/Lit material for a debris piece (warm-brown, low gloss — the faceted toy read,
        // not realistic driftwood). Serializes into the scene; no .mat asset churn.
        private static void ApplyDebrisMaterial(GameObject go, string matName, Color col)
        {
            var mr = go.GetComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = matName };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
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
            // Build the Model child + materials NOW (editor) so they serialize into Boot.unity. This also
            // HIDES the original cap meshes (CastawayCharacter.HideCap) so the castaway reads as having
            // hair, not a cap (86ca8ca1m soak-fix).
            castaway.BuildInEditor();

            // CAP -> HAIR (86ca8ca1m soak-fix): add the clean sandy-ginger hair skull-cap on the head bone
            // (the hidden cap meshes leave a bare crown; this is the hair). Editor-time so it serializes.
            AttachHair(castaway);

            // Wire the verification-only shipped-build CASTAWAY CLOSE-UP capture (drives a dedicated
            // camera onto the avatar's front so the recolored identity — warm khaki shirt, sandy-ginger
            // hair, bare-feet skin — is judgeable from a SHIPPED frame). The gameplay-orbit capture frames
            // the character too small to judge colour; this gives the recolor (86ca8ca1m) a committed,
            // repeatable shipped-build path (the PR #21 lesson — a detail claim needs a committed shot,
            // not a throwaway). Inert unless launched with -verifyCastaway. Sibling of AxeVerifyCapture.
            WireCastawayVerifyCapture();

            // Wire the verification-only FIXED-ORBIT hair-silhouette capture at the TILT-TO-HORIZON angle
            // (86ca8ce6y SOAKFIX3 — the deepened brown-spike fix). The Sponsor sees the crown spike only at
            // the low orbit pitch; the head-to-feet -verifyCastaway close-up can't validate a
            // silhouette-at-the-horizon problem (a subject-fit close-up frames at a fixed apparent size —
            // unity-conventions.md visibility-gate rule). This rides the REAL gameplay orbit camera at the
            // tilt pitch. Inert unless launched with -verifyHair. Sibling of CastawayVerifyCapture.
            WireHairVerifyCapture();

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

            // Attach the SOURCED hero axe (ticket 86ca8ce6y) to the chibi's right-hand bone AFTER the
            // avatar is built (the bone hierarchy must exist), so the held hatchet serializes into
            // Boot.unity under the bone (the editor-vs-runtime serialization trap). HasAxe-gated.
            AttachHeroAxeToHand(player);

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
            orbit.defaultPitch = 55f;   // Sponsor-preferred top-down-ish framing (inside 8-70) — LOCKED
            // Floor WIDENED 35->8 (drew/ocean-camera-fix): lets the Sponsor tilt down to the horizon +
            // see the seaward beach ocean (the 35 floor framed the sea as a far fogged "grey pond" —
            // OceanCameraDiag trace, 2026-06-13). Default 55 + max 70 unchanged.
            orbit.minPitch = 8f;
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

        // Wire the verification-only ROCK capture (86ca8m5zu) onto the Boot object so it SERIALIZES into
        // Boot.unity (the component-in-source-but-not-in-scene trap — it would ship inert otherwise). Inert
        // unless launched with -verifyRock; never affects a normal play/boot/soak.
        private static void WireRockVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] 'Boot' object not found — rock-verify capture not wired");
                return;
            }
            if (bootGo.GetComponent<RockVerifyCapture>() == null)
                bootGo.AddComponent<RockVerifyCapture>();
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
