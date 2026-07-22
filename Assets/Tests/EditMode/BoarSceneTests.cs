using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using FarHorizon.Combat;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Scene-presence guards for the wild boar (ticket 86cah7ydt AC1/AC2/AC5/AC6): the shipped Boot.unity must
    /// carry the FULL boar — components + the part children + the warm-brown read + a reachable-but-not-insta-
    /// aggro spawn — serialized (the component-in-source-but-not-in-scene trap ships an inert/invisible boar,
    /// the #224 UNSOAKABLE failure re-opened). Mirrors SnakeSceneTests.
    /// </summary>
    public class BoarSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, 6f);

        private static GameObject FindBoar()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Boar") return root;
            return null;
        }

        [Test]
        public void BootScene_CarriesTheBoar_AllComponentsSerialized()
        {
            var boar = FindBoar();
            BootstrapPrecondition.Require(boar, "the Boar root (MovementCameraScene.BuildBoar)");

            var hp = boar.GetComponent<Health>();
            Assert.IsNotNull(hp, "boar Health (the shared 86cah7xxp seam — AC1)");
            Assert.IsNotNull(boar.GetComponent<BoarEnemy>(), "BoarEnemy (the gore seam)");
            Assert.IsNotNull(boar.GetComponent<StatusEffectController>(), "StatusEffectController (gore→player bleed — AC4)");
            Assert.IsNotNull(boar.GetComponent<NavMeshAgent>(), "NavMeshAgent (AC2 — NavMesh movement)");

            var ai = boar.GetComponent<BoarAI>();
            Assert.IsNotNull(ai, "BoarAI (AC2 — wander/aggro/windup/charge)");
            Assert.IsNotNull(ai.player, "BoarAI.player wired");
            Assert.IsNotNull(ai.playerHealth, "BoarAI.playerHealth wired (the gore target)");
            Assert.IsNotNull(ai.deathHandler, "BoarAI.deathHandler wired (the LIVE difficulty tier — AC6)");

            var rig = boar.GetComponent<BoarBodyRig>();
            Assert.IsNotNull(rig, "BoarBodyRig (AC5 — the body pose driver)");
            Assert.AreSame(ai, rig.ai, "the rig renders THIS boar's AI states");
            Assert.IsNotNull(rig.parts, "rig parts wired");
            Assert.AreEqual(BoarBodyRig.PartCount, rig.parts.Length, "all 7 parts (body/head/4 legs/tail) present");
            foreach (var p in rig.parts) Assert.IsNotNull(p, "every part reference survives serialization");

            // AC1/AC3: the shared weak-to-pierce / slash-resistant tag is authored on the scene boar.
            Assert.Greater(hp.resistance.Multiplier(DamageType.Pierce), 1f, "scene boar is weak-to-pierce (AC1/AC3)");
            Assert.Less(hp.resistance.Multiplier(DamageType.Slash), 1f, "scene boar is slash-resistant (AC3)");
        }

        [Test]
        public void BootScene_Boar_ReadsAsAWarmBrownQuadruped_NotABushBlob()
        {
            var boar = FindBoar();
            BootstrapPrecondition.Require(boar, "the Boar root");
            var rig = boar.GetComponent<BoarBodyRig>();
            BootstrapPrecondition.Require(rig, "BoarBodyRig");

            int warmParts = 0, rendered = 0;
            foreach (var p in rig.parts)
            {
                var mf = p.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf, "part " + p.name + " has a MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, "part " + p.name + " mesh is baked + serialized");
                var cols = mf.sharedMesh.colors;
                Assert.Greater(cols.Length, 0, "part " + p.name + " bakes vertex colours");
                float r = 0f, g = 0f;
                foreach (var c in cols) { r += c.r; g += c.g; }
                r /= cols.Length; g /= cols.Length;
                if (r >= g) warmParts++; // >= so the cream-tusk-heavy head (near-neutral mean) still counts warm

                var mr = p.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr, "part " + p.name + " renders");
                Assert.IsNotNull(mr.sharedMaterial, "part " + p.name + " carries the inline BoarMat");
                rendered++;
            }
            Assert.AreEqual(rendered, warmParts, "every part reads warm (R>=G) — never a green bush-blob (AC5)");

            // The head reads DISTINCT — a snouted head part exists in front of the body along +Z.
            var head = rig.parts[BoarBodyRig.HeadIndex];
            Assert.Greater(head.localPosition.z, 0.3f, "the head sits FORWARD of the body (a distinct snouted head — AC5)");
        }

        [Test]
        public void BootScene_Boar_SpawnsFindable_ButDoesNotInstaAggro()
        {
            var boar = FindBoar();
            BootstrapPrecondition.Require(boar, "the Boar root");

            // AC5: a KNOWN reachable spot near the player's path.
            float dist = Vector2.Distance(new Vector2(boar.transform.position.x, boar.transform.position.z),
                                          new Vector2(PlayerSpawn.x, PlayerSpawn.z));
            Assert.That(dist, Is.InRange(3f, 12f),
                "the boar spawns close enough to FIND, far enough not to insta-aggro (measured " + dist.ToString("0.0") + "u)");

            var ai = boar.GetComponent<BoarAI>();
            Assert.Less(ai.aggroRadius, dist, "the boar must NOT aggro the player at spawn (approach triggers it — AC2)");
            // The reach story is baked into the range gap: the charge starts BEYOND a spear's reach (3.6).
            Assert.Greater(ai.chargeRange, WeaponCatalog.SpearReach,
                "the charge starts beyond the spear's reach so the rush crosses the spear band before goring (AC3)");

            // AC6: the per-tier gore map is authored on the scene boar (gentle easy < threatening hard).
            var enemy = boar.GetComponent<BoarEnemy>();
            Assert.Less(enemy.easyGoreDamage, enemy.medGoreDamage, "easy < med gore (AC6)");
            Assert.Less(enemy.medGoreDamage, enemy.hardGoreDamage, "med < hard gore (AC6)");

            // Distinct from the snake — the two enemies never share a spot. The snake spawn is (5,0,4)
            // (MovementCameraScene.SnakePosition); the boar is authored on the opposite side of the loop.
            float toSnake = Vector2.Distance(
                new Vector2(boar.transform.position.x, boar.transform.position.z), new Vector2(5f, 4f));
            Assert.Greater(toSnake, 4f, "the boar spawns clear of the snake (5,4) — two distinct enemies");
        }

        [Test]
        public void BootScene_CarriesBoarVerifyCapture_Serialized()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            FarHorizon.BoarVerifyCapture cap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cap = root.GetComponentInChildren<FarHorizon.BoarVerifyCapture>(true);
                if (cap != null) break;
            }
            BootstrapPrecondition.Require(cap, "BoarVerifyCapture on the Boot object");
            Assert.IsNotNull(cap.player, "BoarVerifyCapture.player wired (it walks the REAL player)");
        }
    }
}
