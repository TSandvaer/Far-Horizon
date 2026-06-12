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
        public void BootScene_OrbitCamera_PitchClampIs35to70_Default55()
        {
            var scene = OpenBoot();
            var orbit = FindInScene<OrbitCamera>(scene);
            Assert.IsNotNull(orbit);
            Assert.AreEqual(35f, orbit.minPitch, 0.001f, "min pitch clamp must be 35 (AC)");
            Assert.AreEqual(70f, orbit.maxPitch, 0.001f, "max pitch clamp must be 70 (AC)");
            Assert.AreEqual(55f, orbit.defaultPitch, 0.001f, "default framing is the Sponsor-preferred 55");
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
