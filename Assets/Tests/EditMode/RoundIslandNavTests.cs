using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// BIG ROUND ISLAND N1 ("can't walk everywhere") + N2 ("disappears under a hill") shipped-scene guards
    /// (ticket 86ca9a7qn). These read the serialized Boot scene (binary scenes can't be GUID-grepped — the
    /// EditMode reader is authoritative) and pin the structural facts that the click-to-move + camera fixes
    /// depend on, so a regression fails in headless CI before a Sponsor soak:
    ///
    ///  N1 — the NavMesh must cover the WHOLE island, not just the flat centre:
    ///   • the authoritative whole-island PlayNavMesh.asset exists (baked over the sloped terrain);
    ///   • the slab-era BootNavMesh NavMeshSurface is DISABLED at runtime (it must not add its disconnected
    ///     flat-Y=0 disc as a second competing NavMesh — that dual-overlap was part of the partial coverage);
    ///   • the island terrain (Ground_Play) carries a MeshCollider on the Ground layer (the bake surface).
    ///
    ///  N2 — the orbit camera must not clip under/through hills:
    ///   • the serialized OrbitCamera carries a non-zero terrainMask (the Ground layer) so the hill-collision
    ///     raycasts actually hit the island terrain (a zero mask = inert = the bug).
    /// </summary>
    public class RoundIslandNavTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string PlayNavMeshPath = "Assets/NavMesh/PlayNavMesh.asset";

        [Test]
        public void Nav_WholeIslandNavMesh_IsSavedAsAsset()
        {
            // The authoritative island NavMesh (baked over the sloped terrain in WorldBootstrap.BakeNavMesh)
            // must be SAVED as an asset so it ships — bake-in-memory ships a dead click-to-move (the N1 root
            // cause's sibling, unity-conventions.md §NavMesh).
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(PlayNavMeshPath);
            Assert.IsNotNull(data,
                "the whole-island NavMesh must be SAVED at " + PlayNavMeshPath +
                " (the authoritative bake over the sloped island terrain — covers UP/DOWN/ACROSS the hills)");
        }

        [Test]
        public void Nav_SlabEraBootNavSurface_IsDisabledAtRuntime_NoCompetingFlatDisc()
        {
            // The slab-era NavMeshSurface (BootNavMesh, baked when only the flat 60×60 TestGround existed) must
            // be DISABLED in the shipped scene so it does NOT register its flat-Y=0 disc as a SECOND,
            // disconnected NavMesh competing with the whole-island PlayNavMesh (part of the N1 partial-coverage
            // bug — the agent could warp onto the isolated slab disc and not reach the hills beyond ±30u).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var slabSurfaceGo = GameObject.Find("NavMeshSurface");
            Assert.IsNotNull(slabSurfaceGo,
                "the slab-era NavMeshSurface GameObject must still exist (its BootNavMesh asset still ships)");
            var slabSurface = slabSurfaceGo.GetComponent<NavMeshSurface>();
            Assert.IsNotNull(slabSurface, "the NavMeshSurface GameObject must carry a NavMeshSurface component");
            Assert.IsFalse(slabSurface.enabled,
                "the slab-era NavMeshSurface must be DISABLED so it never adds its flat disc as a competing " +
                "NavMesh at runtime — only the whole-island PlayNavMesh may be live (N1)");

            // The authoritative island surface (on the Grounds root) must EXIST and be ENABLED.
            var islandSurfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(s => s.enabled).ToArray();
            Assert.Greater(islandSurfaces.Length, 0,
                "exactly the whole-island NavMeshSurface (Grounds) must remain ENABLED — the live island NavMesh");
        }

        [Test]
        public void Nav_IslandTerrain_HasMeshColliderOnGroundLayer_TheBakeSurface()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "the Boot scene must carry the round-island terrain (Ground_Play)");
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(groundLayer, 0, "'Ground' layer must be defined in TagManager");
            Assert.AreEqual(groundLayer, ground.layer,
                "the island terrain must be on the Ground layer (the NavMesh layerMask + click groundMask)");
            var col = ground.GetComponent<MeshCollider>();
            Assert.IsNotNull(col?.sharedMesh,
                "the island terrain must carry a MeshCollider (the NavMesh bakes on the actual sloped surface " +
                "— without it the agent has no walkable hills, the N1 partial-coverage cause)");
        }

        [Test]
        public void Camera_OrbitCarriesTerrainMask_ForHillCollision_N2()
        {
            // N2: the serialized OrbitCamera must carry a NON-ZERO terrainMask (the Ground layer) so its
            // hill-collision + above-ground raycasts actually hit the island terrain. A zero mask = inert =
            // the camera clips under/through hills again (the bug). Drop the BuildOrbitCamera mask wiring and
            // this goes red.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            OrbitCamera orbit = Object.FindObjectsByType<OrbitCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault();
            Assert.IsNotNull(orbit, "the Boot scene must carry the OrbitCamera");
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.AreNotEqual(0, orbit.terrainMask.value,
                "the orbit camera's terrainMask must be NON-ZERO (the Ground layer) so the hill-collision " +
                "raycasts hit the island terrain — a zero mask makes the N2 hill-clip fix inert");
            if (groundLayer >= 0)
                Assert.AreEqual(1 << groundLayer, orbit.terrainMask.value,
                    "the orbit camera's terrainMask must be exactly the Ground layer");
            Assert.Greater(orbit.groundClearance, 0f, "groundClearance must be positive (camera stays above ground)");
        }
    }
}
