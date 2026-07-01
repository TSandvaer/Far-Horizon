using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// Regression guard for AIRBORNE AIR-CONTROL (ticket 86caac81y — "thrown violently to the sides" fix), run
    /// through the PRODUCTION WasdMovement + a REAL NavMeshAgent + a REAL jump (CastawayCharacter.TryJump).
    ///
    /// THE BUG: while airborne, WasdMovement drove _agent.velocity = LastMoveDir * fullSpeed each frame — the
    /// SAME full-speed snap as grounded — so a single A/D press in flight redirected the horizontal velocity to
    /// ±moveSpeed sideways INSTANTLY (the Sponsor's violent throw, PR #69 soak). The fix steers via a capped
    /// acceleration that preserves carried-in momentum.
    ///
    /// CATCHES THE BUG CLASS, NOT AN INSTANCE:
    ///  (1) AIRBORNE: pressing A/D while airborne must NOT snap the agent's lateral speed to the full move speed —
    ///      the lateral velocity stays a SUBTLE nudge (well under moveSpeed) over the early-flight frames. A
    ///      regression that re-uses the grounded full-speed snap fails loudly (lateral speed ≈ moveSpeed).
    ///  (2) FORWARD MOMENTUM: a forward (W) jump's +Z momentum is largely PRESERVED while strafing mid-air (the
    ///      PR #69 carried-momentum AC stays intact).
    ///  (3) GROUNDED UNCHANGED: on the ground, A/D still commands the FULL move speed immediately (the grounded
    ///      crisp-keyboard feel is NOT regressed by the airborne branch).
    ///
    /// REUSES the JumpOnSpace foreshore-rig pattern: a renderer-DISABLED flat NavMesh PROXY slab + a renderer-
    /// ENABLED visible terrain + a REAL NavMeshAgent driven by the production WasdMovement; jump via RequestJump
    /// (the headless Space analog). The arc advances headlessly (AdvanceJump's nominal 1/60 step), so IsAirborne
    /// is true for real frames during which we exercise the air-control path.
    /// </summary>
    public class AirborneAirControlPlayModeTests
    {
        private GameObject _proxySlab;
        private GameObject _visibleTerrain;
        private GameObject _surfaceGo;
        private GameObject _playerGo;
        private GameObject _avatarGo;
        private GameObject _camGo;
        private NavMeshAgent _agent;
        private WasdMovement _wasd;
        private CastawayCharacter _castaway;
        private OrbitCamera _orbit;

        private const float WalkSpeed = 5.5f;
        private const float RunSpeed = 9.5f;
        private const float JumpVelocity = 5.5f;
        private const float JumpGravity = 18f;
        private const float AirAccel = 9f;     // production default airControlAccel (86caambxh: Sponsor soak 2026-07-01 raised 5→9, snappier mid-air sideways air-steer)
        private const float AirCap = 5.5f;     // production default airControlMaxSpeed

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var empty = SceneManager.CreateScene("AirControlIsolated_" + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != empty && s.isLoaded)
                {
                    var op = SceneManager.UnloadSceneAsync(s);
                    if (op != null) while (!op.isDone) yield return null;
                }
            }

            int groundLayer = LayerMask.NameToLayer("Ground");

            _proxySlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _proxySlab.name = "TestGround";
            if (groundLayer >= 0) _proxySlab.layer = groundLayer;
            _proxySlab.transform.position = new Vector3(0f, -0.5f, 0f);
            _proxySlab.transform.localScale = new Vector3(60f, 1f, 60f);
            _proxySlab.GetComponent<MeshRenderer>().enabled = false;

            _visibleTerrain = new GameObject("Ground_Play");
            if (groundLayer >= 0) _visibleTerrain.layer = groundLayer;
            var mf = _visibleTerrain.AddComponent<MeshFilter>();
            var groundMesh = new Mesh { name = "FlatVisibleGround" };
            groundMesh.vertices = new[]
            {
                new Vector3(-30f, 0f,  30f), new Vector3(30f, 0f,  30f),
                new Vector3(-30f, 0f, -30f), new Vector3(30f, 0f, -30f)
            };
            groundMesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            groundMesh.RecalculateNormals();
            mf.sharedMesh = groundMesh;
            _visibleTerrain.AddComponent<MeshRenderer>().enabled = true;
            _visibleTerrain.AddComponent<MeshCollider>().sharedMesh = groundMesh;

            _surfaceGo = new GameObject("Surface");
            var surface = _surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            _camGo = new GameObject("Main Camera");
            _camGo.AddComponent<Camera>();
            _orbit = _camGo.AddComponent<OrbitCamera>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, 0f, 0f);
            _agent = _playerGo.AddComponent<NavMeshAgent>();
            _agent.radius = 0.4f; _agent.height = 1.8f; _agent.speed = WalkSpeed;
            _agent.acceleration = 48f; _agent.angularSpeed = 999f; _agent.stoppingDistance = 0.1f;
            _agent.updateRotation = false; _agent.updateUpAxis = false;
            var ctm = _playerGo.AddComponent<ClickToMove>();
            ctm.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            _orbit.target = _playerGo.transform;

            LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero;
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = true;
            _castaway.modelSoleGround = false; // no Animator/clip in this rig
            _castaway.snapRate = 40f;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            _castaway.jumpVelocity = JumpVelocity;
            _castaway.jumpGravity = JumpGravity;

            _wasd = _playerGo.AddComponent<WasdMovement>();
            _wasd.moveSpeed = WalkSpeed;
            _wasd.runSpeed = RunSpeed;
            _wasd.airControlAccel = AirAccel;
            _wasd.airControlMaxSpeed = AirCap;
            _wasd.cameraTransform = _camGo.transform;
            _wasd.clickToMove = ctm;
            _wasd.castaway = _castaway;

            yield return null; // Awake resolves agent/camera/castaway
            yield return null; // ClickToMove warps onto the NavMesh; WasdMovement.Start disables click
            Assert.IsTrue(_agent.isOnNavMesh, "the agent must be on the NavMesh before driving the jump");
            _orbit.SetYaw(0f); // camera faces +Z → D = +X strafe
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_avatarGo != null) Object.DestroyImmediate(_avatarGo);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_visibleTerrain != null) Object.DestroyImmediate(_visibleTerrain);
            if (_proxySlab != null) Object.DestroyImmediate(_proxySlab);
            if (_surfaceGo != null) Object.DestroyImmediate(_surfaceGo);
            NavMesh.RemoveAllNavMeshData();
        }

        // ===================================================================================================
        // THE LOAD-BEARING GUARD — pressing A/D WHILE AIRBORNE must NOT snap the lateral velocity to the full
        // move speed (the violent throw). It stays a subtle nudge over the early-flight frames.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator AirborneStrafe_IsASubtleNudge_NotAFullSpeedSnap()
        {
            // Settle grounded at rest (no input → zero horizontal velocity carried in).
            _wasd.SetInputOverride(Vector2.zero);
            float start = Time.time;
            while (Time.time - start < 0.4f) yield return null;
            Assert.IsFalse(_castaway.IsAirborne, "must be grounded before the jump");

            // Jump straight up (no carried horizontal momentum), THEN hold D mid-air.
            _wasd.RequestJump();
            yield return null;
            Assert.IsTrue(_castaway.IsAirborne, "jump must have started");

            _wasd.SetInputOverride(new Vector2(1f, 0f)); // hold D (camera +X)

            // Sample the lateral (X) agent speed over the first few airborne frames after the press. With the OLD
            // full-speed snap this would be ≈ WalkSpeed (5.5) within ONE frame. With the fix it ramps gently.
            float worstLateralEarly = 0f;
            int airborneFramesSampled = 0;
            start = Time.time;
            while (Time.time - start < 0.18f && _castaway.IsAirborne) // ~early-flight window
            {
                yield return null;
                if (!_castaway.IsAirborne) break;
                worstLateralEarly = Mathf.Max(worstLateralEarly, Mathf.Abs(_agent.velocity.x));
                airborneFramesSampled++;
            }
            _wasd.SetInputOverride(Vector2.zero);

            Assert.Greater(airborneFramesSampled, 2,
                "the air-control assertion must have sampled several airborne frames (else it passes vacuously).");
            // The early-flight lateral speed must be a SMALL fraction of the grounded move speed — the subtle
            // nudge. The old snap hit the full 5.5 u/s in one frame; the fix keeps it well below half that early.
            Assert.Less(worstLateralEarly, WalkSpeed * 0.5f,
                $"airborne A/D must be a SUBTLE nudge — early-flight lateral speed was {worstLateralEarly:F3} u/s; " +
                $"the OLD bug snapped it to ~{WalkSpeed} u/s instantly (the violent throw, 86caac81y).");
        }

        // ===================================================================================================
        // FORWARD MOMENTUM PRESERVED — a forward (W) jump keeps moving forward while a mid-air D nudge is applied
        // (PR #69 carried-momentum AC stays intact through the air-control fix).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator ForwardJump_KeepsForwardMomentum_WhileStrafingMidAir()
        {
            _wasd.SetInputOverride(new Vector2(0f, 1f)); // hold W → build forward (+Z) velocity grounded first
            float start = Time.time;
            while (Time.time - start < 0.4f) yield return null;
            Assert.Greater(_agent.velocity.z, WalkSpeed * 0.5f, "should be moving forward (+Z) before the jump");

            _wasd.RequestJump();
            yield return null;
            Assert.IsTrue(_castaway.IsAirborne, "jump-while-moving must have started");

            // Switch to D mid-air (steer): forward +Z must remain largely preserved (momentum), with only a
            // gentle +X gain — never a hard redirect that kills the forward travel.
            _wasd.SetInputOverride(new Vector2(1f, 0f));
            bool sawPreservedForward = false;
            start = Time.time;
            while (Time.time - start < 0.18f && _castaway.IsAirborne)
            {
                yield return null;
                if (_agent.velocity.z > WalkSpeed * 0.6f) sawPreservedForward = true;
            }
            _wasd.SetInputOverride(Vector2.zero);

            Assert.IsTrue(sawPreservedForward,
                "the forward (+Z) momentum carried into the jump must be largely preserved while strafing mid-air " +
                "(PR #69 carried-momentum AC — the air-control fix steers, it doesn't hard-redirect).");
        }

        // ===================================================================================================
        // GROUNDED UNCHANGED — on the ground, A/D still commands the FULL move speed immediately (the airborne
        // branch must NOT regress the crisp grounded keyboard feel).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator Grounded_Strafe_StillCommandsFullSpeed_Immediately()
        {
            Assert.IsFalse(_castaway.IsAirborne, "this test is grounded-only (no jump)");
            _wasd.SetInputOverride(new Vector2(1f, 0f)); // hold D on the ground
            yield return null; // one Update commands the velocity
            yield return null; // agent applies it

            // Grounded, the commanded velocity is LastMoveDir * moveSpeed = full speed on the SAME frame.
            Assert.That(_agent.velocity.magnitude, Is.EqualTo(WalkSpeed).Within(WalkSpeed * 0.2f),
                $"grounded A/D must still drive ~full move speed ({WalkSpeed} u/s) immediately — the airborne " +
                $"air-control branch must NOT slow grounded movement. Got {_agent.velocity.magnitude:F3} u/s.");
            _wasd.SetInputOverride(Vector2.zero);
        }
    }
}
