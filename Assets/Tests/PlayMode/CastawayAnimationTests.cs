using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode tests for the U6 castaway avatar's locomotion-state driver (ticket 86ca86fz9).
    ///
    /// The castaway self-drives its Idle&lt;-&gt;Walk Animator blend off the player NavMeshAgent's
    /// velocity (no ClickToMove edit). These tests prove the STATE SWITCH end-to-end on a real moving
    /// agent — the bug CLASS being: the anim blend never flipping (frozen idle while walking, or
    /// stuck-walking after arrival). We assert BOTH directions over a real Time.time window (headless
    /// Time.deltaTime ~= 0, so never sample per-frame — unity-conventions.md §headless time).
    ///
    /// The Animator/FBX model isn't instantiated here (it's an editor-asset reference wired at author
    /// time); IsWalking is computed purely from agent velocity, so the state logic is testable with a
    /// real agent + no model. A separate EditMode test pins the serialized skinned/boned avatar + the
    /// looping clips + the controller wiring (CastawayCharacterTests).
    /// </summary>
    public class CastawayAnimationTests
    {
        private GameObject _ground;
        private GameObject _surfaceGo;
        private GameObject _playerGo;
        private NavMeshAgent _agent;
        private CastawayCharacter _castaway;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.localScale = new Vector3(6f, 1f, 6f); // 60x60 walkable

            _surfaceGo = new GameObject("Surface");
            var surface = _surfaceGo.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = Vector3.zero;
            _agent = _playerGo.AddComponent<NavMeshAgent>();
            _agent.radius = 0.4f; _agent.height = 1.8f; _agent.speed = 6f;
            _agent.acceleration = 40f; _agent.angularSpeed = 999f; _agent.stoppingDistance = 0.1f;
            _agent.updateRotation = false; _agent.updateUpAxis = false;

            // The avatar lives on a CHILD of the player root (so its height-scale doesn't scale the
            // agent) — exactly how MovementCameraScene wires it. It resolves the agent via parent.
            // We deliberately leave modelPrefab null: the velocity-state logic (IsWalking) is computed
            // purely from agent velocity and needs no instantiated model. CastawayCharacter.BuildModel
            // correctly LogErrors on a null modelPrefab (it IS an error in the real game), so we expect
            // that one log here rather than suppress the production guard — Unity's test runner fails on
            // any unexpected LogError otherwise.
            LogAssert.Expect(LogType.Error,
                "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
            var avatarGo = new GameObject("CastawayAvatar");
            avatarGo.transform.SetParent(_playerGo.transform, false);
            _castaway = avatarGo.AddComponent<CastawayCharacter>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_ground);
            Object.Destroy(_surfaceGo);
            Object.Destroy(_playerGo);
            NavMesh.RemoveAllNavMeshData();
        }

        // The core state-switch guard: idle at rest -> walking while traversing -> idle again on
        // arrival. Asserts BOTH transitions on a real moving agent, sampled over Time.time windows.
        [UnityTest]
        public IEnumerator IsWalking_FlipsTrueWhileMoving_AndFalseAfterArrival()
        {
            // Let Awake() resolve the agent + a couple frames so the agent registers on the NavMesh.
            yield return null;
            yield return null;
            Assert.IsTrue(_agent.isOnNavMesh, "agent must be on the NavMesh before driving it");

            // At rest, the character must read idle (not walking).
            Assert.IsFalse(_castaway.IsWalking, "the castaway must read idle at rest (velocity ~0)");

            // Drive a real move across the ground.
            var dest = new Vector3(14f, 0f, 10f);
            Assert.IsTrue(NavMesh.SamplePosition(dest, out var hit, 4f, NavMesh.AllAreas),
                "the destination must sample onto the NavMesh");
            _agent.SetDestination(hit.position);

            // Within a short window the agent picks up speed -> IsWalking must become true.
            bool sawWalking = false;
            float start = Time.time;
            while (Time.time - start < 4f)
            {
                if (_castaway.IsWalking) { sawWalking = true; break; }
                yield return null;
            }
            Assert.IsTrue(sawWalking,
                "the castaway must read WALKING while the agent traverses (the Idle->Walk switch); " +
                "agent.velocity=" + _agent.velocity.magnitude.ToString("0.00"));

            // Let the agent reach the destination + brake.
            start = Time.time;
            while (Time.time - start < 8f)
            {
                if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f
                    && _agent.velocity.sqrMagnitude < 0.02f)
                    break;
                yield return null;
            }

            // After arrival + brake, give LateUpdate a frame to observe the ~0 velocity, then assert idle.
            yield return null;
            Assert.IsFalse(_castaway.IsWalking,
                "the castaway must read IDLE again after arrival (the Walk->Idle switch); " +
                "agent.velocity=" + _agent.velocity.magnitude.ToString("0.00"));
        }

        // Verify-capture determinism hook (ticket 86ca8fevz): FaceWorldYawInstant pins the body yaw to a
        // KNOWN value immediately, so the castaway verify capture frames a deterministic FRONT every run
        // (the "+Z front" assumption was non-deterministic — PR #31/#36). Asserts the pin is exact +
        // repeatable + survives a moving frame's lerp being overridden. This is the regression guard for
        // the facing half of the determinism fix (the framing-math half is VerifyCaptureFramingTests).
        [UnityTest]
        public IEnumerator FaceWorldYawInstant_PinsKnownYaw_Deterministically()
        {
            yield return null;
            yield return null;

            _castaway.FaceWorldYawInstant(0f);
            Assert.AreEqual(0f, _castaway.BodyYaw, 1e-4f, "pinning yaw 0 must set BodyYaw exactly to 0");

            // Repeatable: a second identical pin yields the identical value (no drift / accumulation).
            _castaway.FaceWorldYawInstant(0f);
            Assert.AreEqual(0f, _castaway.BodyYaw, 1e-4f, "re-pinning the same yaw must be idempotent");

            // An arbitrary yaw is honored exactly.
            _castaway.FaceWorldYawInstant(90f);
            Assert.AreEqual(90f, _castaway.BodyYaw, 1e-4f, "pinning yaw 90 must set BodyYaw exactly to 90");

            // After a frame of LateUpdate (which lerps facing off velocity), re-pinning to 0 must SNAP it
            // back deterministically — the verify capture's pin always wins over whatever the lerp left.
            yield return null;
            _castaway.FaceWorldYawInstant(0f);
            Assert.AreEqual(0f, _castaway.BodyYaw, 1e-4f,
                "re-pinning after a LateUpdate frame must snap the yaw deterministically (capture pin wins)");
        }

        // Guard the threshold: a tiny residual drift below walkSpeedThreshold must NOT read as walking
        // (prevents a jittery idle-twitch from flickering the walk blend). We verify the at-rest agent
        // stays idle across several frames even with the agent active on the mesh.
        [UnityTest]
        public IEnumerator IsWalking_StaysFalse_WhenAgentIsStationary()
        {
            yield return null;
            yield return null;
            Assert.IsTrue(_agent.isOnNavMesh);

            float start = Time.time;
            while (Time.time - start < 1.5f)
            {
                Assert.IsFalse(_castaway.IsWalking,
                    "a stationary agent must never flicker into the walk state");
                yield return null;
            }
        }
    }
}
