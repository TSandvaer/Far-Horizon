using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the ② BOULDER-MINING content (ticket 86camz9v7) is SERIALIZED into the Boot scene the exe
    /// ships — not added at Awake (the editor-vs-runtime serialization trap would mangle/drop an Awake-built
    /// component or visual). Sibling of MineSceneTests; same regression-guard intent: drop
    /// MovementCameraScene.BuildBoulders (or its wiring) and this goes RED in headless CI, rather than the shipped
    /// build silently lacking the "mine a boulder for stone" beat.
    ///
    /// Also pins the DISTINCTNESS the ② constraints require: the boulder pool is a SEPARATE root ("Boulders" with
    /// "Boulder" children whose mesh is "BoulderMesh"), distinct from the decorative scatter rocks ("RockMesh",
    /// guarded by RockBoulderSceneTests) AND the ore nodes ("OreNode", guarded by MineSceneTests) — so the boulder
    /// author provably did NOT mutate the seed-42 scatter stream nor collide with the ore pool.
    /// </summary>
    public class BoulderSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesMineBoulder_WiredToInventoryPlayerCharacterRootAndSpawner()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            MineBoulder mine = FindInScene<MineBoulder>(scene);
            Assert.IsNotNull(mine,
                "the Boot scene must carry the MineBoulder — the ② 'mine a boulder for stone' beat (serialized, not Awake-built)");
            Assert.IsNotNull(mine.inventory, "MineBoulder.inventory must be wired editor-time");
            Assert.IsNotNull(mine.player, "MineBoulder.player must be wired editor-time");
            Assert.IsNotNull(mine.character,
                "MineBoulder.character must be wired editor-time so each strike plays the melee swing in the build");
            Assert.IsNotNull(mine.boulderRoot,
                "MineBoulder.boulderRoot must be wired editor-time so the manager discovers the authored boulder pool");
            Assert.IsNotNull(mine.stonePileSpawner,
                "MineBoulder.stonePileSpawner must be wired editor-time so a broken boulder drops a lootable stone pile");
        }

        [Test]
        public void BootScene_CarriesStonePileSpawner_AndBoulderPool()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var spawner = FindInScene<StonePileSpawner>(scene);
            Assert.IsNotNull(spawner, "the Boot scene must carry the StonePileSpawner (the stone-drop factory)");
            Assert.Greater(spawner.StoneYield, 0, "StoneYield must be positive — a broken boulder must drop stone");

            MineBoulder mine = FindInScene<MineBoulder>(scene);
            Assert.IsNotNull(mine, "the Boot scene must carry the MineBoulder");
            Assert.IsNotNull(mine.boulderRoot, "MineBoulder.boulderRoot must be wired");

            int boulders = CountBoulders(mine.boulderRoot);
            Assert.Greater(boulders, 4,
                "the boulder pool must carry several Boulder instances (the seeded discrete VOLUME pool) — found " + boulders);
        }

        [Test]
        public void BootScene_MineBoulder_ShipsWithSaneStrikeAndRadiusDefaults()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            MineBoulder mine = FindInScene<MineBoulder>(scene);
            Assert.IsNotNull(mine, "the Boot scene must carry the MineBoulder");

            Assert.Greater(mine.mineRadius, 0f, "mineRadius must be positive — a zero radius never mines");
            Assert.GreaterOrEqual(mine.strikesToBreak, MineBoulder.StrikesToBreakMin,
                "strikesToBreak must be >= the floor — else a boulder breaks on strike 0");
            Assert.LessOrEqual(mine.strikesToBreak, MineBoulder.StrikesToBreakMax,
                "strikesToBreak must be <= the ceiling");
        }

        [Test]
        public void BootScene_Boulders_AreFlatShadedStone_NotSmoothMounds()
        {
            // Every boulder mesh must be the FLAT-SHADED FacetedRock (verts == tris*3 + carries vertex colours) —
            // the "reads as carved stone" percept (RockBoulderSceneTests family), not a welded smooth sphere.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var boulders = FindBoulderMeshes();
            Assert.Greater(boulders.Length, 0, "must have boulders to check");
            foreach (var mf in boulders)
            {
                var m = mf.sharedMesh;
                int tris = m.triangles.Length / 3;
                Assert.AreEqual(tris * 3, m.vertexCount,
                    $"boulder '{mf.name}' must be FLAT-SHADED (verts == tris*3); got {m.vertexCount}/{tris} — " +
                    "a welded smooth sphere is the rejected MOUND regression");
                Assert.AreEqual(m.vertexCount, m.colors.Length,
                    $"boulder '{mf.name}' must carry per-facet vertex COLOURS (the stone value contrast)");
            }
        }

        [Test]
        public void BootScene_Boulders_AreLargerThanOreNodes_TheVolumeSource()
        {
            // ② calls for LARGER boulders (the VOLUME stone source) — assert each boulder mesh is meaningfully
            // bigger than the ~0.58u-radius ore outcrops (bounds width > 1.5u ≈ radius > 0.75u).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var boulders = FindBoulderMeshes();
            Assert.Greater(boulders.Length, 0, "must have boulders to check");
            foreach (var mf in boulders)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                float widthXZ = Mathf.Max(size.x, size.z);
                Assert.Greater(widthXZ, 1.5f,
                    $"boulder '{mf.name}' must be LARGE (width {widthXZ:F2}u) — the volume source, clearly bigger " +
                    "than the ~0.58u-radius ore outcrops");
            }
        }

        [Test]
        public void BootScene_BoulderPool_IsDistinctFromScatterRocks_AndOreNodes()
        {
            // The boulders must NOT be named "RockMesh" (the scatter-rock scan RockBoulderSceneTests guards) nor
            // "OreNode" (the ore pool) — they are a SEPARATE mineable pool, so neither the seed-42 scatter nor the
            // ore pool is perturbed by the boulder author.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var boulders = FindBoulderMeshes();
            Assert.Greater(boulders.Length, 0, "must have boulders to check");
            foreach (var mf in boulders)
            {
                Assert.AreNotEqual("RockMesh", mf.gameObject.name,
                    "a boulder mesh must NOT be named 'RockMesh' — that would inflate the scatter-rock count guard");
                // Its parent Boulder node lives under the "Boulders" root, never under "OreNodes".
                Transform t = mf.transform;
                while (t != null)
                {
                    Assert.AreNotEqual("OreNodes", t.name, "a boulder must NOT live under the ore-node pool root");
                    t = t.parent;
                }
            }
        }

        private static MeshFilter[] FindBoulderMeshes()
        {
            return Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "BoulderMesh" && mf.sharedMesh != null)
                .ToArray();
        }

        private static int CountBoulders(Transform root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == MineBoulder.BoulderNodeName) n++;
            return n;
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
    }
}
