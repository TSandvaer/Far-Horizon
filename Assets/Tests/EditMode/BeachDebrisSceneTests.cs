using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode scene-presence guard for the M-U3-SCENE-4 (86ca8feuf) washed-ashore shipwreck debris.
    /// The debris is pure VISUAL set-dressing (no MonoBehaviour), so the shipped-build capture gate is
    /// the final evidence that it READS — but binary scenes can't be GUID-grepped, so this EditMode
    /// reader is the authoritative check that the debris actually lives in the Boot.unity the exe ships
    /// (the mesh-in-source-but-not-serialized failure class, unity-conventions.md).
    ///
    /// Each assertion guards a SPECIFIC contract from the ticket:
    ///  - the debris is SERIALIZED into Boot.unity (drop BuildBeachDebris and it goes RED, rather than
    ///    the shipped scene silently lacking the landing-narrative props);
    ///  - NO piece carries a Collider — the load-bearing AC2 contract: it must NOT block the click-move
    ///    ground raycast or the NavMesh. A regression that adds a collider (e.g. forgetting to strip a
    ///    primitive's collider) would silently break pathing near the spawn — this catches it in CI;
    ///  - the pieces are on the warm-brown wood palette + sub-1.0 (HDR-clamp discipline);
    ///  - the scatter stays MODEST (sparse, not clutter — style-guide-v2 §5).
    /// </summary>
    public class BeachDebrisSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        private static GameObject FindDebrisRoot(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "BeachDebris") return root;
                var t = root.transform.Find("BeachDebris");
                if (t != null) return t.gameObject;
                // The debris root is authored at scene root, but tolerate a nested parent.
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "BeachDebris") return child.gameObject;
            }
            return null;
        }

        [Test]
        public void BootScene_CarriesBeachDebris_WithSeveralPieces()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var debris = FindDebrisRoot(scene);
            Assert.IsNotNull(debris,
                "the Boot scene must carry the BeachDebris root — the washed-ashore shipwreck scatter " +
                "(planks + crate + barrel) authored editor-time + serialized, not Awake-built");

            var renderers = debris.GetComponentsInChildren<MeshRenderer>(true);
            Assert.Greater(renderers.Length, 2,
                "the debris must carry several serialized mesh pieces (a few planks + a crate + a barrel)");

            // MODEST, not clutter: a handful of pieces, not a dozen. Sparse is the brief (style-guide-v2 §5).
            Assert.LessOrEqual(renderers.Length, 10,
                "the debris must stay SPARSE (<=10 pieces) — a tasteful washed-ashore scatter, not clutter");

            // Each piece must have a serialized mesh (not an empty MeshFilter that ships blank).
            foreach (var mr in renderers)
            {
                var mf = mr.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf, $"{mr.name} must have a MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, $"{mr.name}'s mesh must be serialized into the scene");
            }
        }

        [Test]
        public void BeachDebris_HasNoColliders_DoesNotBlockPathingOrRaycast()
        {
            // THE load-bearing AC2 contract: the debris is diegetic decoration ONLY — it must NOT block
            // the click-move ground raycast or the NavMesh. Built collider-free; a regression that leaves
            // a primitive's auto-added Collider on (or adds one) would silently bury the spawn shore in
            // an invisible wall the player can't path through. Guard every piece.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var debris = FindDebrisRoot(scene);
            Assert.IsNotNull(debris, "the BeachDebris root must exist");

            var colliders = debris.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(0, colliders.Length,
                "NO debris piece may carry a Collider — it is set-dressing, not an obstacle. A collider " +
                "would block the click-move ground raycast / NavMesh near the spawn (AC2). Found: " +
                string.Join(", ", colliders.Select(c => c.gameObject.name)));
        }

        [Test]
        public void BeachDebris_IsOnWarmBrownWoodPalette_SubOne()
        {
            // The debris is the warm-brown wood family (axe-haft / chop-trunk / campfire-log palette,
            // style-guide-v2 §6), and every channel sub-1.0 (HDR-clamp discipline — the Zone-D post stack
            // must not bloom it). Assert each piece's albedo is a warm brown (R > G > B, mid-low value)
            // and sub-1.0.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var debris = FindDebrisRoot(scene);
            Assert.IsNotNull(debris, "the BeachDebris root must exist");

            foreach (var mr in debris.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mat = mr.sharedMaterial;
                Assert.IsNotNull(mat, $"{mr.name} must have a material");
                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.magenta;

                Assert.Less(c.r, 1f, $"{mr.name} red channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.Less(c.g, 1f, $"{mr.name} green channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.Less(c.b, 1f, $"{mr.name} blue channel must be sub-1.0 (HDR-clamp-safe)");

                // Warm brown wood: R is the dominant channel, B the least — the warm timber read.
                Assert.Greater(c.r, c.g, $"{mr.name} must be a WARM brown (R > G) — the wood family palette");
                Assert.Greater(c.g, c.b, $"{mr.name} must be a warm brown (G > B) — not a cold/grey tone");
            }
        }

        [Test]
        public void BeachDebris_IsAtTheSpawnShore_Seaward()
        {
            // The debris narrates "washed ashore near the landing": it sits on the beach just SEAWARD of
            // the locked spawn (Z+6), in the seaward gameplay view, clear of the loop spots. Assert the
            // scatter centre is seaward of the spawn and roughly at the shore (not flung inland).
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var debris = FindDebrisRoot(scene);
            Assert.IsNotNull(debris, "the BeachDebris root must exist");

            // Spawn is at Z+6 (locked). The debris must be SEAWARD of it (smaller Z) so it's "between the
            // castaway and the sea" — the washed-ashore read — not inland behind him.
            Assert.Less(debris.transform.position.z, 6f,
                "the debris must sit SEAWARD of the locked spawn (Z+6) — on the beach toward the sea, the " +
                "washed-ashore read — not inland behind the castaway");
            // And near the shore band (not flung far out / far inland).
            Assert.Greater(debris.transform.position.z, -10f,
                "the debris must stay on the visible beach band (inland of the waterline ~Z-10.5), not " +
                "out in the water");
        }
    }
}
