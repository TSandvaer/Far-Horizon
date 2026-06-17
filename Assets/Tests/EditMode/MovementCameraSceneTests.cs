using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode regression guards on the AUTHORED Boot scene (ticket 86ca86fme). These assert the
    /// REAL serialized scene state the bootstrap produced — not a runtime-assembled hierarchy — so
    /// the editor-vs-runtime divergence class can't silently ship a broken movement+camera layer:
    ///
    ///  - The orbit camera, player+agent+ClickToMove, and flat ground are all SERIALIZED into the
    ///    scene (the Awake-no-serialize trap, unity-conventions.md §editor-vs-runtime).
    ///  - The baked NavMesh is SAVED AS AN ASSET (Assets/NavMesh/BootNavMesh.asset) — the single
    ///    most load-bearing ship-or-die fact: bake-in-memory ships a DEAD click-to-move in the
    ///    standalone player (unity-conventions.md §NavMesh; spike FINDINGS iter-3).
    ///  - The ClickToMove's markerPrefab reference is wired (PoE click feedback ships).
    ///
    /// If a future change reverts to runtime hierarchy assembly, forgets to save the NavMesh asset,
    /// or drops the orbit camera, exactly one of these fails in headless CI — before a Sponsor soak.
    /// </summary>
    public class MovementCameraSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string NavMeshDataPath = "Assets/NavMesh/BootNavMesh.asset";

        private Scene OpenBoot()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            return scene;
        }

        private T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        [Test]
        public void BootScene_HasOrbitCamera_TargetingPlayer()
        {
            var scene = OpenBoot();
            var orbit = FindInScene<OrbitCamera>(scene);
            Assert.IsNotNull(orbit, "the Boot scene must carry the OrbitCamera rig (U3 port)");
            Assert.IsNotNull(orbit.target, "the orbit camera must target the player at author time");

            var player = FindInScene<ClickToMove>(scene);
            Assert.IsNotNull(player, "the Boot scene must carry the player's ClickToMove");
            Assert.AreSame(player.transform, orbit.target,
                "the orbit camera must target the ClickToMove player root");
        }

        [Test]
        public void BootScene_OrbitCamera_PitchClampIs8to70_Default55()
        {
            // drew/ocean-camera-fix: the shipped Boot.unity orbit camera must carry the WIDENED pitch band
            // (floor 35->8) so the Sponsor can tilt down to the horizon + see the seaward beach ocean (the
            // 35 floor framed the sea as a far fogged 'grey pond' — OceanCameraDiag trace). defaultPitch
            // stays the Sponsor-LOCKED 55. A regression of the serialized minPitch back to 35 fails here.
            var scene = OpenBoot();
            var orbit = FindInScene<OrbitCamera>(scene);
            Assert.IsNotNull(orbit);
            Assert.AreEqual(8f, orbit.minPitch, 0.001f, "min pitch clamp must be the widened 8 (tilt to horizon)");
            Assert.AreEqual(70f, orbit.maxPitch, 0.001f, "max pitch clamp must be 70 (unchanged)");
            Assert.AreEqual(55f, orbit.defaultPitch, 0.001f, "default framing is the Sponsor-LOCKED 55");
        }

        [Test]
        public void BootScene_Player_HasNavMeshAgent_And_ClickToMove()
        {
            var scene = OpenBoot();
            var ctm = FindInScene<ClickToMove>(scene);
            Assert.IsNotNull(ctm, "player must carry ClickToMove");
            var agent = ctm.GetComponent<NavMeshAgent>();
            Assert.IsNotNull(agent, "ClickToMove requires a NavMeshAgent on the same GameObject");
        }

        [Test]
        public void BootScene_ClickToMove_MarkerPrefab_IsWired()
        {
            var scene = OpenBoot();
            var ctm = FindInScene<ClickToMove>(scene);
            Assert.IsNotNull(ctm);
            Assert.IsNotNull(ctm.markerPrefab,
                "the ClickToMove must reference a serialized ClickMarker prefab (PoE click feedback " +
                "ships; a null here means the prefab wiring regressed)");
        }

        [Test]
        public void BootScene_Player_HasWasdMovement_WiredCameraRelative()
        {
            // WASD locomotion (ticket 86ca9yq2x) must SERIALIZE onto the player in Boot.unity (the
            // component-in-source-but-not-in-scene trap — an Awake-added driver would ship the player
            // un-driveable). And it must be wired camera-relative (cameraTransform = the orbit camera) +
            // to the ClickToMove it replaces.
            var scene = OpenBoot();
            var wasd = FindInScene<WasdMovement>(scene);
            Assert.IsNotNull(wasd, "the Boot scene must carry the player's WasdMovement (the WASD pivot, 86ca9yq2x)");
            Assert.IsNotNull(wasd.GetComponent<NavMeshAgent>(),
                "WasdMovement must sit on the player root that carries the NavMeshAgent (it drives the agent velocity)");
            Assert.IsNotNull(wasd.cameraTransform,
                "WasdMovement.cameraTransform must be wired editor-time (the orbit camera) so movement is " +
                "camera-relative without an Awake Camera.main lookup (the editor-vs-runtime serialization trap)");
            var orbit = FindInScene<OrbitCamera>(scene);
            Assert.IsNotNull(orbit);
            Assert.AreSame(orbit.transform, wasd.cameraTransform,
                "WasdMovement must be camera-relative to the ORBIT camera (W = the way the orbit cam faces — AC1)");
            Assert.IsNotNull(wasd.clickToMove,
                "WasdMovement must reference the ClickToMove it replaces (to disable click-to-move on Start — AC3)");
            // JUMP (86ca9yq3q) — the avatar that owns the jump arc must be wired editor-time so Space's rising
            // edge calls CastawayCharacter.TryJump() (the component-in-source-but-not-in-scene trap — an Awake
            // GetComponentInChildren is the fallback only). A regression that drops this ships an inert Space key.
            Assert.IsNotNull(wasd.castaway,
                "WasdMovement.castaway (the jump owner) must be wired editor-time (the avatar's CastawayCharacter)");
            var castaway = FindInScene<CastawayCharacter>(scene);
            Assert.IsNotNull(castaway);
            Assert.AreSame(castaway, wasd.castaway,
                "WasdMovement must reference the scene's CastawayCharacter as the jump owner (Space → TryJump)");
        }

        [Test]
        public void BootScene_ClickToMove_IsDisabled_WasdReplacesIt()
        {
            // AC3: the Sponsor-directed pivot REPLACES click-to-move with WASD. The shipped scene must carry
            // ClickToMove with clickEnabled FALSE (WasdMovement disables it on Start; the editor-time default is
            // serialized true, but the contract is that WASD owns locomotion). We assert the serialized intent:
            // the player carries BOTH (ClickToMove kept for its programmatic MoveTo seam) AND a WasdMovement that
            // will disable the click path. A regression that dropped WasdMovement would leave click-to-move live.
            var scene = OpenBoot();
            var ctm = FindInScene<ClickToMove>(scene);
            var wasd = FindInScene<WasdMovement>(scene);
            Assert.IsNotNull(ctm, "ClickToMove stays in the scene (its MoveTo seam serves the verify captures)");
            Assert.IsNotNull(wasd, "WasdMovement must be present — it is what disables click-to-move on Start (AC3)");
            Assert.AreSame(ctm, wasd.clickToMove,
                "WasdMovement must hold the SAME ClickToMove it disables — the click-to-move → WASD handoff");
        }

        [Test]
        public void BootScene_HasWasdVerifyCapture_OnBoot()
        {
            // The shipped-build WASD capture must be serialized onto Boot (component-in-source-but-not-in-scene
            // trap — it would ship inert otherwise, producing zero verify frames). Inert unless -verifyWasd.
            var scene = OpenBoot();
            var cap = FindInScene<WasdVerifyCapture>(scene);
            Assert.IsNotNull(cap, "the Boot scene must carry WasdVerifyCapture (the -verifyWasd shipped-build gate)");
            Assert.IsNotNull(cap.player, "WasdVerifyCapture must reference the WasdMovement it drives");
        }

        [Test]
        public void NavMesh_IsSavedAsAsset_NotBakeInMemory()
        {
            // The ship-or-die guard: the baked NavMeshData must exist as a project asset so it
            // embeds in the standalone build. Bake-in-memory passes editor checks but ships a dead
            // click-to-move in the player (unity-conventions.md §NavMesh).
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshDataPath);
            Assert.IsNotNull(data,
                "the baked NavMesh must be SAVED as an asset at " + NavMeshDataPath +
                " (else the standalone build ships NO NavMesh -> click-to-move silently dead)");
        }

        [Test]
        public void BootScene_HasWalkableGround_OnGroundLayer()
        {
            var scene = OpenBoot();
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(groundLayer, 0, "'Ground' layer must be defined in TagManager");

            bool foundGround = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.layer == groundLayer && root.GetComponent<MeshCollider>() != null)
                {
                    foundGround = true;
                    break;
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.gameObject.layer == groundLayer && t.GetComponent<MeshCollider>() != null)
                    {
                        foundGround = true;
                        break;
                    }
                if (foundGround) break;
            }
            Assert.IsTrue(foundGround,
                "the Boot scene must contain a walkable ground (MeshCollider on the Ground layer) " +
                "so the NavMesh bake + click-raycast have a surface");
        }
    }
}
