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

        // BLOB-SHADOW SCENE-PRESENCE GUARD (ticket 86ca8ca1m — "blob shadow re-fit" AC). The contact
        // shadow disc must be SERIALIZED into the Boot scene under the player root (a child of the
        // ClickToMove root), with a real renderer + mesh — the component-not-serialized-into-scene
        // failure class (unity-conventions.md §editor-vs-runtime: a builder can exist in source while the
        // scene never carries the object, shipping the feature silently inert). Binary scenes can't be
        // GUID-grepped, so this EditMode reader is the authoritative check. Also pins the re-fit radius
        // (the disc bounds must span the chunky footprint, not collapse to a point).
        [Test]
        public void BootScene_HasBlobShadow_UnderPlayer_SizedToChunkyStance()
        {
            var scene = OpenBoot();
            var ctm = FindInScene<ClickToMove>(scene);
            Assert.IsNotNull(ctm, "the Boot scene must carry the player");

            var shadow = ctm.transform.Find(EditorTools.MovementCameraScene.BlobShadowObjectName);
            Assert.IsNotNull(shadow,
                "the player must carry a serialized '" + EditorTools.MovementCameraScene.BlobShadowObjectName +
                "' child (the contact/blob shadow — component-not-serialized guard)");

            var mr = shadow.GetComponent<MeshRenderer>();
            var mf = shadow.GetComponent<MeshFilter>();
            Assert.IsNotNull(mr, "the blob shadow must have a MeshRenderer serialized in the scene");
            Assert.IsNotNull(mf, "the blob shadow must have a MeshFilter serialized in the scene");
            Assert.IsNotNull(mf.sharedMesh, "the blob-shadow disc mesh must be present (built editor-time)");
            Assert.IsNotNull(mr.sharedMaterial, "the blob-shadow material must be serialized inline");

            // The shadow must never cast its own real shadow (it IS the fake shadow) or block raycasts.
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, mr.shadowCastingMode,
                "the blob shadow must not cast a real shadow");
            Assert.IsNull(shadow.GetComponent<Collider>(),
                "the blob shadow must have NO collider (must not block the click raycast or NavMesh bake)");

            // Re-fit: the disc must span the chunky footprint (diameter ~= 2*radius), not a point.
            float diameter = mf.sharedMesh.bounds.size.x;
            Assert.Greater(diameter, EditorTools.MovementCameraScene.BlobShadowRadius,
                "the blob-shadow disc must span the chunky footprint (re-fit radius=" +
                EditorTools.MovementCameraScene.BlobShadowRadius + "), not collapse — diameter=" +
                diameter.ToString("0.00"));
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
