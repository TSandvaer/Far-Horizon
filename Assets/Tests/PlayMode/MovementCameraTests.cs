using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode tests for the U3 movement + camera port (ticket 86ca86fme).
    ///
    /// Movement: builds a minimal NavMesh-backed scene at runtime and proves click-to-move
    /// (via MoveTo, the input-independent seam) pathfinds AND the agent actually REACHES the
    /// destination. Reaching is sampled over a real Time.time window — never per-frame deltas —
    /// because Time.deltaTime ~= 0 per frame in headless runs (unity-conventions.md §headless time).
    ///
    /// Camera: proves the orbit camera obeys the 35-70 pitch clamp from every entry point
    /// (default, programmatic SetPitch above/below, repeated drives) — the AC's binding guarantee.
    ///
    /// These catch the BUG CLASS, not just an instance: the movement test asserts an end-to-end
    /// position delta (agent arrives), not merely "SetDestination was called"; the camera tests
    /// assert the clamp holds at the boundaries AND beyond them, not just at the default.
    /// </summary>
    public class MovementCameraTests
    {
        private GameObject _ground;
        private GameObject _surfaceGo;
        private GameObject _playerGo;
        private ClickToMove _player;

        [SetUp]
        public void SetUp()
        {
            // Flat ground with a baked NavMesh (navigation-package surface).
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.localScale = new Vector3(6f, 1f, 6f); // 60x60 walkable

            _surfaceGo = new GameObject("Surface");
            var surface = _surfaceGo.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = Vector3.zero;
            var agent = _playerGo.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f; agent.height = 1.8f; agent.speed = 8f;
            agent.acceleration = 40f; agent.angularSpeed = 999f; agent.stoppingDistance = 0.1f;
            agent.updateRotation = false; agent.updateUpAxis = false;
            _player = _playerGo.AddComponent<ClickToMove>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_ground);
            Object.Destroy(_surfaceGo);
            Object.Destroy(_playerGo);
            NavMesh.RemoveAllNavMeshData();
        }

        // ---- Movement ----

        [UnityTest]
        public IEnumerator ClickToMove_PathfindsAndReaches_Destination()
        {
            // Let Start() + EnsureOnNavMesh() place the agent on the mesh.
            yield return null;
            yield return null;
            Assert.IsTrue(_player.Agent.isOnNavMesh, "agent must be on the NavMesh after Start()");

            var dest = new Vector3(12f, 0f, 8f);
            bool set = _player.MoveTo(dest);
            Assert.IsTrue(set, "MoveTo must set a valid destination on the NavMesh");
            Assert.IsTrue(_player.Agent.hasPath || _player.Agent.pathPending,
                "the agent must have (or be computing) a path after MoveTo");

            // Walk a real wall-clock window so the agent actually traverses the path. Headless
            // Time.deltaTime ~= 0, so we sample over Time.time, not per-frame deltas.
            float start = Time.time;
            while (Time.time - start < 8f)
            {
                float planar = Vector3.Distance(
                    new Vector3(_playerGo.transform.position.x, 0f, _playerGo.transform.position.z),
                    new Vector3(dest.x, 0f, dest.z));
                if (!_player.Agent.pathPending && planar <= 0.6f)
                    break;
                yield return null;
            }

            float finalPlanar = Vector3.Distance(
                new Vector3(_playerGo.transform.position.x, 0f, _playerGo.transform.position.z),
                new Vector3(dest.x, 0f, dest.z));
            Assert.LessOrEqual(finalPlanar, 0.6f,
                "the agent must REACH the clicked destination (end-to-end pathfind), not merely " +
                "have SetDestination called — final planar distance was " + finalPlanar.ToString("0.00"));
        }

        [UnityTest]
        public IEnumerator MoveTo_OffMeshPoint_SnapsToNearestMeshPoint()
        {
            yield return null;
            yield return null;
            // A point slightly OFF the mesh (a couple units above the ground, as a raycast hit on a
            // sloped/raised surface would produce) snaps onto the NavMesh via the 4u SamplePosition
            // radius. (A point >4u from any mesh would NOT snap — covered by the FarOutsideMesh test.)
            bool set = _player.MoveTo(new Vector3(6f, 2f, -6f));
            Assert.IsTrue(set, "an off-mesh point within the 4u sample radius must snap onto the NavMesh");
        }

        [UnityTest]
        public IEnumerator MoveTo_FarOutsideMesh_ReturnsFalse_NoThrow()
        {
            yield return null;
            yield return null;
            // Well beyond the 4u sample radius from any mesh -> no valid destination, returns false.
            bool set = _player.MoveTo(new Vector3(10000f, 0f, 10000f));
            Assert.IsFalse(set, "a point far outside the NavMesh must NOT set a destination");
            Assert.IsTrue(_player.Agent.isOnNavMesh, "the agent must remain valid after a failed MoveTo");
        }

        // ---- Camera ----

        [UnityTest]
        public IEnumerator OrbitCamera_Default_Pitch_IsInsideClampBand()
        {
            var (camGo, orbit) = MakeOrbitRig();
            yield return null; // Start() applies the default

            Assert.GreaterOrEqual(orbit.Pitch, orbit.minPitch, "default pitch must be >= minPitch");
            Assert.LessOrEqual(orbit.Pitch, orbit.maxPitch, "default pitch must be <= maxPitch");
            Assert.AreEqual(55f, orbit.defaultPitch, "the Sponsor-preferred default framing is 55deg");
            DestroyRig(camGo, orbit);
        }

        [UnityTest]
        public IEnumerator OrbitCamera_SetPitch_ClampsAboveAndBelowBand()
        {
            var (camGo, orbit) = MakeOrbitRig();
            yield return null;

            orbit.SetPitch(120f); // above max
            Assert.LessOrEqual(orbit.Pitch, orbit.maxPitch, "pitch driven above max must clamp to maxPitch");
            Assert.AreEqual(orbit.maxPitch, orbit.Pitch, 0.001f, "clamp lands exactly on maxPitch");

            orbit.SetPitch(-30f); // below min
            Assert.GreaterOrEqual(orbit.Pitch, orbit.minPitch, "pitch driven below min must clamp to minPitch");
            Assert.AreEqual(orbit.minPitch, orbit.Pitch, 0.001f, "clamp lands exactly on minPitch");

            orbit.SetPitch(50f); // inside
            Assert.AreEqual(50f, orbit.Pitch, 0.001f, "a pitch inside the band is honored as-is");
            DestroyRig(camGo, orbit);
        }

        [UnityTest]
        public IEnumerator OrbitCamera_RepeatedDrives_NeverEscapeBand()
        {
            var (camGo, orbit) = MakeOrbitRig();
            yield return null;

            // Hammer the pitch from both extremes repeatedly; it must never escape the clamp.
            for (int i = 0; i < 20; i++)
            {
                orbit.SetPitch(i % 2 == 0 ? 200f : -200f);
                Assert.That(orbit.Pitch, Is.InRange(orbit.minPitch, orbit.maxPitch),
                    "pitch must stay within [" + orbit.minPitch + "," + orbit.maxPitch + "] every drive");
                yield return null;
            }
            DestroyRig(camGo, orbit);
        }

        [UnityTest]
        public IEnumerator OrbitCamera_Zoom_ClampsToDistanceBand()
        {
            var (camGo, orbit) = MakeOrbitRig();
            yield return null;

            orbit.SetDistance(1000f);
            Assert.LessOrEqual(orbit.Distance, orbit.maxDistance, "zoom-out clamps to maxDistance");
            orbit.SetDistance(0f);
            Assert.GreaterOrEqual(orbit.Distance, orbit.minDistance, "zoom-in clamps to minDistance");
            DestroyRig(camGo, orbit);
        }

        [UnityTest]
        public IEnumerator OrbitCamera_Follows_Target()
        {
            var (camGo, orbit) = MakeOrbitRig();
            yield return null;
            Vector3 firstCamPos = camGo.transform.position;

            // Move the target far; the camera position must change (it follows).
            orbit.target.position = new Vector3(40f, 0f, 40f);
            // Let the follow lerp run a real window.
            float start = Time.time;
            while (Time.time - start < 1.5f) yield return null;

            Assert.AreNotEqual(firstCamPos, camGo.transform.position,
                "the orbit camera must follow its target when the target moves");
            DestroyRig(camGo, orbit);
        }

        // Build a standalone orbit rig (no scene NavMesh needed) targeting a movable dummy.
        private (GameObject, OrbitCamera) MakeOrbitRig()
        {
            var targetGo = new GameObject("OrbitTarget");
            var camGo = new GameObject("OrbitCam");
            camGo.AddComponent<Camera>();
            var orbit = camGo.AddComponent<OrbitCamera>();
            orbit.target = targetGo.transform;
            orbit.defaultPitch = 55f;
            orbit.minPitch = 35f;
            orbit.maxPitch = 70f;
            orbit.distance = 14f;
            return (camGo, orbit);
        }

        // Destroy both the cam rig and its target dummy (the target is a sibling GO, not a child).
        private void DestroyRig(GameObject camGo, OrbitCamera orbit)
        {
            if (orbit != null && orbit.target != null) Object.Destroy(orbit.target.gameObject);
            Object.Destroy(camGo);
        }
    }
}
