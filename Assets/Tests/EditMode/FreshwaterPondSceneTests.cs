using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the FRESHWATER POND (ticket 86caamkv7, AC2/AC2a/AC6). Reads the serialized Boot
    /// scene (binary scenes can't be GUID-grepped — the EditMode reader is authoritative) and pins:
    ///
    ///  • the pond is PRESENT in the shipped scene (serialized, not an Awake add — editor-vs-runtime trap);
    ///  • its FreshwaterPond drink seam is wired (thirst + player refs serialize);
    ///  • the pond is COLLIDER-FREE on every piece (AC2a — a collider would carve the NavMesh bake / block the
    ///    ground raycast; collider-free is what PROVES the pond cannot shrink NavMesh coverage). This is the
    ///    seed-42-lock guard's pond half: the pond is authored OUTSIDE the seeded LowPolyZoneGen stream
    ///    (MovementCameraScene, not ScatterIslandProps), so it provably cannot perturb the seed-42 island
    ///    silhouette / scatter; collider-free proves it also cannot perturb the NavMesh. (The whole-island
    ///    NavMesh COVERAGE non-regression is asserted end-to-end by RoundIslandNavCoveragePlayModeTests.)
    ///  • the pond water reads as FRESH water (the pond's own PondShallow/PondDeep blue, distinct from the sea).
    ///
    /// Sibling of BushSceneTests / BeachDebrisSceneTests (scene-presence + no-collider contract guards).
    /// </summary>
    public class FreshwaterPondSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesFreshwaterPond_Serialized_AndWired()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            FreshwaterPond pond = FindInScene<FreshwaterPond>(scene);
            Assert.IsNotNull(pond,
                "the Boot scene must carry the FreshwaterPond serialized into the scene — the thirst source " +
                "ships from this scene, not an Awake add (unity-conventions.md editor-vs-runtime trap)");

            Assert.IsNotNull(pond.player, "the FreshwaterPond's player ref must serialize (the proximity gate reads it)");
            Assert.IsNotNull(pond.thirst, "the FreshwaterPond's ThirstNeed ref must serialize (the drink->AddWater seam)");
            Assert.Greater(pond.EffectiveDrinkRadius, 0f, "the pond must have a positive drink reach");
        }

        [Test]
        public void BootScene_CarriesDrinkAction_WiredToThePond()
        {
            // The DRINK INPUT call-site (Q) must serialize into the scene with its pond ref wired (BootstrapProject
            // adds the DrinkAction; MovementCameraScene wires the pond after authoring it). Drop either and the
            // shipped build has no way to drink — this reds in headless CI before a soak. Sibling intent to the
            // EatBerryAction wiring for hunger.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            DrinkAction drink = FindInScene<DrinkAction>(scene);
            Assert.IsNotNull(drink,
                "the Boot scene must carry the DrinkAction (the Q drink-input call-site) serialized — without " +
                "it nothing in the build invokes the drink seam (the dual-spawn 'tested but never invoked' class)");
            Assert.IsNotNull(drink.pond,
                "the DrinkAction's pond ref must serialize (wired by MovementCameraScene after the pond is authored)");
        }

        [Test]
        public void Pond_IsColliderFree_CannotCarveNavMeshOrBlockRaycast()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindInScene<FreshwaterPond>(scene);
            Assert.IsNotNull(pond, "the pond must be present");

            // AC2a: the pond must NOT shrink NavMesh coverage. The mechanism that PROVES it: every piece of the
            // pond (water surface, bank, accents) is collider-free, so it contributes NOTHING to the NavMesh
            // bake (which collects PhysicsColliders) and never blocks the click-move ground raycast. A collider
            // sneaking onto the pond would be the silent killer (it would carve a hole in the walkable disc).
            var colliders = pond.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(0, colliders.Length,
                "the pond + bank + accents must be COLLIDER-FREE so they cannot carve the NavMesh bake or " +
                "block the ground raycast (AC2a seed-42/NavMesh lock — the player walks up to drink)");
        }

        [Test]
        public void Pond_WaterReadsAsFreshWater_DistinctBluePalette()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindInScene<FreshwaterPond>(scene);
            Assert.IsNotNull(pond, "the pond must be present");

            // Find the pond water mesh (the PondWater child) and confirm it carries the FRESH-water blue
            // palette (PondShallow/PondDeep) — distinct from the salt sea's teal. The freshwater tell is B > G.
            var mf = FindChildMeshNamed(pond.transform, "LP_PondWater");
            Assert.IsNotNull(mf, "the pond must carry the freshwater water-surface mesh (LP_PondWater)");
            var cols = mf.colors;
            Assert.Greater(cols.Length, 0, "the pond water mesh must carry vertex colours (the fresh-blue gradient)");

            // Every vertex colour should be the pond's own fresh blue (PondShallow or PondDeep) — assert at
            // least one vert reads B > G (the freshwater lean the sea's teal never has: the sea keeps G >= B).
            bool anyFreshBlue = false;
            foreach (var c in cols) if (c.b > c.g + 0.001f) { anyFreshBlue = true; break; }
            Assert.IsTrue(anyFreshBlue,
                "the pond water must read as FRESH water — at least one vertex leans BLUE (B > G), the " +
                "freshwater tell the salt sea's teal (G >= B) never shows (Uma §1a)");
        }

        // BuildPondWaterMesh is deterministic (no RNG) — the capture is byte-stable build to build. A guard
        // so a future change that injected jitter (breaking the stable capture) reds here.
        [Test]
        public void BuildPondWaterMesh_IsDeterministic()
        {
            var a = LowPolyZoneGen.BuildPondWaterMesh(2.6f);
            var b = LowPolyZoneGen.BuildPondWaterMesh(2.6f);
            Assert.AreEqual(a.vertexCount, b.vertexCount, "the pond water mesh vertex count is deterministic");
            var va = a.vertices; var vb = b.vertices;
            for (int i = 0; i < va.Length; i++)
                Assert.AreEqual(va[i], vb[i], "the pond water mesh geometry is deterministic (no RNG) — a stable capture");
            // The fresh-blue palette is in the build (PondShallow rim, PondDeep centre): assert B > G shows up.
            bool freshLean = false;
            foreach (var c in a.colors) if (c.b > c.g + 0.001f) { freshLean = true; break; }
            Assert.IsTrue(freshLean, "the built pond water carries the freshwater B>G lean");
        }

        [Test]
        public void BootScene_CarriesFreshwaterPondVerifyCapture_Serialized()
        {
            // The shipped-build pond capture component (FreshwaterPondVerifyCapture) must SERIALIZE into the
            // Boot scene — the component-in-source-but-not-in-scene trap (CaptureGate.cs shipped inert in
            // PR #6 the same way). If it isn't on the Boot object, a reviewer's -verifyPond re-run produces
            // ZERO pond frames (the exact silent failure the shipped-build capture gate exists to catch), so
            // "the pond renders fresh-blue + grounded in the shipped build" goes UNPROVEN. Inert at normal
            // play (no -verifyPond flag) — this only guards the verify path's presence, never gameplay.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var cap = FindInScene<FreshwaterPondVerifyCapture>(scene);
            Assert.IsNotNull(cap,
                "the Boot scene must carry FreshwaterPondVerifyCapture serialized onto the Boot object — " +
                "without it a -verifyPond re-run captures nothing (the component-in-source-but-not-in-scene " +
                "silent-killer the capture gate exists to prevent)");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7) — the pond water disc must sit ABOVE the real terrain ====
        // The pond shipped GREEN-DOMINANT (B-G=-0.139) NOT because the water material was wrong (its vertex
        // colours are fresh-blue — guarded above) but because the pond was authored at the assumed flat Y=0
        // while the real Zone-D terrain at the inland pond (7,-3) sits ~+0.40 → the opaque terrain OCCLUDED
        // the sunken fresh-blue disc and the shipped render read terrain-green. WorldBootstrap.RegroundFresh-
        // waterPond lifts the pond root so the water sits PondWaterClearanceAboveTerrain above the ground.
        // This guards the bug CLASS: the pond water surface must be measurably ABOVE the terrain it sits over,
        // so it can never be occluded again (the vertex-colour test green-passed through the entire defect —
        // it tests the wrong layer; THIS asserts the on-terrain geometry that actually decides visibility).
        [Test]
        public void Pond_WaterSurface_SitsAboveTerrain_NotOccluded()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindAnyInScene<FreshwaterPond>();
            Assert.IsNotNull(pond, "the pond must be present");
            var waterT = pond.transform.Find("PondWater");
            Assert.IsNotNull(waterT, "the pond must carry the PondWater surface child");
            float waterWorldY = waterT.position.y;

            var ground = GameObject.Find("Ground_Play");
            Assert.IsNotNull(ground, "the play-space terrain (Ground_Play) must exist (re-ground raycasts it)");
            var col = ground.GetComponent<MeshCollider>();
            Assert.IsNotNull(col, "the terrain must carry a MeshCollider (the re-ground raycast surface)");

            Vector3 p = pond.transform.position;
            var ray = new Ray(new Vector3(p.x, 200f, p.z), Vector3.down);
            Assert.IsTrue(col.Raycast(ray, out RaycastHit hit, 400f),
                $"the terrain ray at the pond XZ ({p.x:F1},{p.z:F1}) must hit Ground_Play");
            float terrainY = hit.point.y;

            // The water surface must sit ABOVE the terrain (not buried) by a clear margin — else the opaque
            // terrain occludes the fresh-blue disc and the shipped render reads terrain-green again. A small
            // floor (0.03) tolerates terrain facet jitter while catching a re-sink.
            Assert.Greater(waterWorldY, terrainY + 0.03f,
                $"the pond WATER surface (worldY {waterWorldY:F3}) must sit ABOVE the terrain at the pond " +
                $"({terrainY:F3}) — the green-dominant defect (86cadj4g7) was the disc SUNK under the terrain. " +
                "WorldBootstrap.RegroundFreshwaterPond must lift it onto the ground.");
        }

        // === REGRESSION GUARD (ticket 86cadj4g7) — the depth-fade foam must not FLOOD the flat disc ========
        // Once the disc sits just above the FLAT terrain, the shared depth-fade foam (foam = saturate(1 -
        // gap/_FoamDistance)) fires UNIFORMLY across the whole disc unless _FoamDistance is SMALLER than the
        // near-uniform water→terrain gap (the clearance) — at the sea-scale _FoamDistance the pond shipped
        // PALE/near-white (B-G≈-0.01), the foam-flood half of the same defect. This pins the invariant that
        // makes the open-water disc read fresh-blue: the pond material's _FoamDistance MUST be < the
        // water→terrain clearance so gap > _FoamDistance → foam=0 in open water (foam then only rides the
        // thin bank band). Guards against a future re-tune raising _FoamDistance back into flood territory.
        [Test]
        public void Pond_FoamDistance_BelowWaterTerrainGap_NoFoamFlood()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FreshwaterPond pond = FindAnyInScene<FreshwaterPond>();
            Assert.IsNotNull(pond, "the pond must be present");
            var waterT = pond.transform.Find("PondWater");
            var wmr = waterT != null ? waterT.GetComponent<MeshRenderer>() : null;
            Assert.IsNotNull(wmr, "the pond water surface must carry a MeshRenderer");
            var mat = wmr.sharedMaterial;
            Assert.IsNotNull(mat, "the pond water must carry a material");

            // Only meaningful on the LowPolyWater shader (the depth-fade foam path). The fallback URP/Lit pond
            // has no foam, so the flood can't happen — skip the assert there.
            if (!mat.HasProperty("_FoamDistance"))
            {
                Assert.Pass("pond material has no _FoamDistance (URP/Lit fallback — no depth-fade foam to flood)");
                return;
            }
            float foamDistance = mat.GetFloat("_FoamDistance");

            // The actual water→terrain gap (the clearance the re-ground established).
            var ground = GameObject.Find("Ground_Play");
            var col = ground != null ? ground.GetComponent<MeshCollider>() : null;
            Assert.IsNotNull(col, "Ground_Play MeshCollider must exist to measure the water→terrain gap");
            Vector3 p = pond.transform.position;
            Assert.IsTrue(col.Raycast(new Ray(new Vector3(p.x, 200f, p.z), Vector3.down), out RaycastHit hit, 400f),
                "the terrain ray at the pond XZ must hit Ground_Play");
            float gap = waterT.position.y - hit.point.y;

            Assert.Greater(gap, foamDistance,
                $"the pond water→terrain gap ({gap:F3}) MUST exceed _FoamDistance ({foamDistance:F3}) so the " +
                "open-water disc reads foam=0 (fresh-blue) — a _FoamDistance >= the gap FLOODS the flat disc " +
                "toward warm-white FoamEdge (the pale/green-dominant defect, ticket 86cadj4g7).");
        }

        private static T FindAnyInScene<T>() where T : Component
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        private static Mesh FindChildMeshNamed(Transform root, string meshName)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.name == meshName) return mf.sharedMesh;
            return null;
        }
    }
}
