using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using FarHorizon.Combat;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Scene-presence guards for the REAL snake (ticket 86caaz4vn AC1/AC2/AC6): the shipped Boot.unity must
    /// carry the FULL serpent — components + the segment children + the warm banded read + a reachable
    /// spawn — serialized (the component-in-source-but-not-in-scene trap ships an inert/invisible snake,
    /// which is exactly the #224 UNSOAKABLE failure re-opened).
    ///
    /// Regression guard: drop/rename anything in MovementCameraScene.BuildSnake (the SnakeAI wiring, the
    /// segment children, the banded colours, the spawn placement) and the matching test goes red.
    /// </summary>
    public class SnakeSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        // The player spawn BuildBootScene authors (0,0,6) — the findability anchor (AC6: the snake spawns
        // near the player's typical path, NOT hidden up a mountain).
        private static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, 6f);

        private static GameObject FindSnake()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Snake") return root;
            return null;
        }

        [Test]
        public void BootScene_CarriesTheRealSnake_AllComponentsSerialized()
        {
            var snake = FindSnake();
            BootstrapPrecondition.Require(snake, "the Snake root (MovementCameraScene.BuildSnake)");

            Assert.IsNotNull(snake.GetComponent<Health>(), "snake Health (the shared 86cah7xxp seam)");
            Assert.IsNotNull(snake.GetComponent<SnakeEnemy>(), "SnakeEnemy (the bite seam)");
            Assert.IsNotNull(snake.GetComponent<StatusEffectController>(), "StatusEffectController (weapon bleed)");
            Assert.IsNotNull(snake.GetComponent<NavMeshAgent>(), "NavMeshAgent (AC2 — NavMesh movement)");

            var ai = snake.GetComponent<SnakeAI>();
            Assert.IsNotNull(ai, "SnakeAI (AC2/AC3 — wander/aggro/telegraph/lunge)");
            Assert.IsNotNull(ai.player, "SnakeAI.player wired (proximity aggro needs the player root)");
            Assert.IsNotNull(ai.playerHealth, "SnakeAI.playerHealth wired (the bite target — AC4)");
            Assert.IsNotNull(ai.deathHandler, "SnakeAI.deathHandler wired (the LIVE difficulty tier — AC4)");

            var chain = snake.GetComponent<SnakeBodyChain>();
            Assert.IsNotNull(chain, "SnakeBodyChain (AC1/AC3 — the serpent body + verb poses)");
            Assert.AreSame(ai, chain.ai, "the chain renders THIS snake's AI states");
            Assert.IsNotNull(chain.segments, "chain segments wired");
            Assert.GreaterOrEqual(chain.segments.Length, 10,
                "a segmented serpent (head + >=9 links), never a blob (the #224 bush-blob class)");
            foreach (var seg in chain.segments)
                Assert.IsNotNull(seg, "every chain segment reference survives serialization");
        }

        [Test]
        public void BootScene_Snake_ReadsAsALongBandedSerpent_NotABushBlob()
        {
            var snake = FindSnake();
            BootstrapPrecondition.Require(snake, "the Snake root (MovementCameraScene.BuildSnake)");
            var chain = snake.GetComponent<SnakeBodyChain>();
            BootstrapPrecondition.Require(chain, "SnakeBodyChain");

            // AC1 size: ~1.5–2m nose-to-tail. Measure the AUTHORED layout arc (head → last link).
            float arc = chain.segmentSpacing * (chain.segments.Length - 1) + 0.26f; // + the head length
            Assert.That(arc, Is.InRange(1.4f, 2.2f),
                "nose-to-tail ~1.5–2m (AC1) — measured " + arc.ToString("0.00") + "m");

            // AC1 banding + warm contrast: every segment carries baked vertex colour; the mean is WARM
            // (R > G — never bush-green), and the body alternates two DISTINCT bands.
            float prevR = -1f;
            bool sawAlternation = false;
            int warmSegments = 0;
            for (int i = 0; i < chain.segments.Length; i++)
            {
                var mf = chain.segments[i].GetComponent<MeshFilter>();
                Assert.IsNotNull(mf, "segment " + i + " has a MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, "segment " + i + " mesh is baked + serialized");
                var cols = mf.sharedMesh.colors;
                Assert.Greater(cols.Length, 0, "segment " + i + " bakes vertex colours");
                float r = 0f, g = 0f;
                foreach (var c in cols) { r += c.r; g += c.g; }
                r /= cols.Length; g /= cols.Length;
                if (r > g) warmSegments++;
                if (i > 1 && Mathf.Abs(r - prevR) > 0.10f) sawAlternation = true; // adjacent bands differ
                prevR = r;

                var mr = chain.segments[i].GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr, "segment " + i + " renders");
                Assert.IsNotNull(mr.sharedMaterial, "segment " + i + " carries the inline SnakeMat");
            }
            Assert.AreEqual(chain.segments.Length, warmSegments,
                "EVERY segment reads warm (R>G) — the anti-bush-green contrast pin (AC1)");
            Assert.IsTrue(sawAlternation, "adjacent body links alternate distinct bands (the banded read)");

            // The head is DISTINCT: wider than the first body link (the wedge silhouette).
            var headMesh = chain.segments[0].GetComponent<MeshFilter>().sharedMesh;
            var firstLink = chain.segments[1].GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(headMesh.bounds.size.x, firstLink.bounds.size.x * 1.15f,
                "the head reads WIDER than the body (a distinct head, not another link — AC1)");
        }

        [Test]
        public void BootScene_Snake_SpawnsOnThePlayersPath_AndBiteIsModerate()
        {
            var snake = FindSnake();
            BootstrapPrecondition.Require(snake, "the Snake root (MovementCameraScene.BuildSnake)");

            // AC6: a KNOWN reachable spot near the player's typical path — never hidden up a mountain.
            float dist = Vector2.Distance(new Vector2(snake.transform.position.x, snake.transform.position.z),
                                          new Vector2(PlayerSpawn.x, PlayerSpawn.z));
            Assert.That(dist, Is.InRange(3f, 9f),
                "the snake spawns close enough to FIND walking out of spawn, far enough not to insta-aggro " +
                "(measured " + dist.ToString("0.0") + "u; aggro radius must be < spawn distance)");

            var ai = snake.GetComponent<SnakeAI>();
            Assert.Less(ai.aggroRadius, dist,
                "the snake must NOT aggro the player at spawn (approach triggers it — AC2/AC6)");

            // AC4: the per-tier bite map is authored on the scene snake (gentle easy < threatening hard).
            var enemy = snake.GetComponent<SnakeEnemy>();
            Assert.Less(enemy.easyBiteDamage, enemy.medBiteDamage, "easy < med (AC4)");
            Assert.Less(enemy.medBiteDamage, enemy.hardBiteDamage, "med < hard (AC4)");

            // AC5 kill math: the axe (14 slash, snake slash-neutral) kills the 24 HP snake in exactly 2 hits.
            var hp = snake.GetComponent<Health>();
            Assert.AreEqual(SnakeEnemy.SnakeMaxHp, hp.max, 1e-3f, "snake HP stays the shared-surface 24");
            Assert.Greater(WeaponCatalog.AxeDamage * 2f, hp.max,
                "two axe hits close the loop (find → fight → kill — AC5/AC6)");
        }

        [Test]
        public void BootScene_CarriesSnakeVerifyCapture_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            FarHorizon.SnakeVerifyCapture cap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cap = root.GetComponentInChildren<FarHorizon.SnakeVerifyCapture>(true);
                if (cap != null) break;
            }
            BootstrapPrecondition.Require(cap, "SnakeVerifyCapture on the Boot object");
            Assert.IsNotNull(cap.player, "SnakeVerifyCapture.player wired (it walks the REAL player)");
        }
    }
}
