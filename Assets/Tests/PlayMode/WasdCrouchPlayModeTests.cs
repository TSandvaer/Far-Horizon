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
    /// AC7 regression guard for CROUCH-on-Ctrl-hold (ticket 86caa3kur) — the load-bearing test:
    ///   Ctrl-hold → CROUCH stance; still → crouch-idle, moving → SNEAK (reduced speed); release → stand.
    ///
    /// REUSES the WasdMovementPlayModeTests foreshore rig MINUS the synthetic skinned-mesh sole (grounding is
    /// AC4 / verify-only here — covered by the WASD harness + the soak; this test owns the INPUT→state→speed
    /// contract). A REAL NavMeshAgent + the production WasdMovement driven via the input-independent override
    /// seams (SetCrouchOverride / SetInputOverride / SetSprintOverride — a headless run can't inject real Ctrl/
    /// Shift/WASD keystrokes), so the SAME production Update path runs.
    ///
    /// WHY READ THE STATE GAUGES, NOT SETTLED MOTION: headless Time.deltaTime≈0 stalls physical traversal (the
    /// documented headless-time trap), so the contract is asserted on the per-frame COMMANDED quantities —
    /// WasdMovement.IsCrouching / CurrentSpeed / IsSprinting + CastawayCharacter.IsCrouching (the bool that
    /// drives the Animator Crouch lane) — not on displacement. This is the SAME discipline the run/jump AC5
    /// PlayMode tests use (assert the commanded speed/state, not the headless displacement).
    ///
    /// CATCHES THE BUG CLASS, NOT AN INSTANCE: (1) Ctrl activates the crouch stance EVEN AT A STANDSTILL (the
    /// Crouching Idle — a regression that gates crouch on movement fails), (2) crouching WHILE MOVING commands
    /// a SNEAK speed strictly SLOWER than the stand-walk (a regression that doesn't reduce speed fails), (3)
    /// CROUCH WINS over sprint — Ctrl+Shift sneaks, never runs (AC2), (4) the avatar's Animator Crouch bool
    /// tracks the input (the wire from input → the PR #186 crouch lane), (5) releasing Ctrl returns to standing.
    /// </summary>
    public class WasdCrouchPlayModeTests
    {
        private GameObject _ground;
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
        private const float SneakSpeed = 3f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Fresh isolated scene so OUR flat ground is the only NavMesh surface.
            yield return PlayModeSceneIsolation.IsolateInFreshScene("WasdCrouchIsolated");

            int groundLayer = LayerMask.NameToLayer("Ground");

            // Flat ground (top face at Y=0) — bake a NavMesh off it.
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "TestGround";
            if (groundLayer >= 0) _ground.layer = groundLayer;
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);
            _ground.transform.localScale = new Vector3(40f, 1f, 40f);

            _surfaceGo = new GameObject("Surface");
            var surface = _surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            // Orbit camera facing +Z (yaw 0) — the move is camera-relative.
            _camGo = new GameObject("Main Camera");
            _camGo.AddComponent<Camera>();
            _orbit = _camGo.AddComponent<OrbitCamera>();

            // Player root: agent + ClickToMove (warp-on-NavMesh + the gate) + the production WasdMovement.
            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, 0f, 0f);
            _agent = _playerGo.AddComponent<NavMeshAgent>();
            _agent.radius = 0.4f; _agent.height = 1.8f; _agent.speed = WalkSpeed;
            _agent.acceleration = 24f; _agent.angularSpeed = 999f; _agent.stoppingDistance = 0.1f;
            _agent.updateRotation = false; _agent.updateUpAxis = false;
            var ctm = _playerGo.AddComponent<ClickToMove>();
            ctm.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            _orbit.target = _playerGo.transform;

            _wasd = _playerGo.AddComponent<WasdMovement>();
            _wasd.moveSpeed = WalkSpeed;
            _wasd.runSpeed = RunSpeed;
            _wasd.sneakSpeed = SneakSpeed;
            _wasd.cameraTransform = _camGo.transform;
            _wasd.clickToMove = ctm;

            // The avatar child carrying CastawayCharacter — no modelPrefab (a bare rig; the Crouch bool is driven
            // on its Animator if present, else the IsCrouching mirror carries the contract). Expect the no-prefab
            // build error (the synthetic-rig path — same as the WASD harness).
            LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero;
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = false;        // grounding is AC4 / verify-only here
            _castaway.modelSoleGround = false;
            _wasd.castaway = _castaway;

            yield return null; // Awake resolves agent/camera/castaway
            yield return null; // ClickToMove warps onto the NavMesh; WasdMovement.Start disables click
            Assert.IsTrue(_agent.isOnNavMesh, "the agent must be on the NavMesh before driving crouch");
        }

        [TearDown]
        public void TearDown()
        {
            if (_avatarGo != null) Object.DestroyImmediate(_avatarGo);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_ground != null) Object.DestroyImmediate(_ground);
            if (_surfaceGo != null) Object.DestroyImmediate(_surfaceGo);
            NavMesh.RemoveAllNavMeshData();
        }

        // ===================================================================================================
        // AC1 — Ctrl-hold at a STANDSTILL activates the CROUCH stance (the Crouching Idle), and the avatar's
        // Crouch bool tracks it. Crouch does NOT require movement (unlike sprint).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator CtrlHold_AtStandstill_ActivatesCrouch_AndDrivesAvatarBool()
        {
            // No move input — just hold Ctrl.
            _wasd.SetCrouchOverride(true);
            yield return null; yield return null;

            Assert.IsTrue(_wasd.IsCrouching,
                "holding Ctrl at a standstill must activate the CROUCH stance (the Crouching Idle lowered " +
                "stance) — crouch does NOT gate on movement (AC1).");
            Assert.IsTrue(_castaway.IsCrouching,
                "the avatar's Crouch state (which drives the Animator Crouch bool → CrouchIdle) must track the " +
                "WasdMovement crouch input — the wire from input to the PR #186 crouch lane.");

            _wasd.ClearCrouchOverride();
        }

        // ===================================================================================================
        // AC1 — releasing Ctrl returns to the standing stance.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator ReleaseCtrl_ReturnsToStanding()
        {
            _wasd.SetCrouchOverride(true);
            yield return null; yield return null;
            Assert.IsTrue(_wasd.IsCrouching, "sanity: crouch active while Ctrl held");

            _wasd.SetCrouchOverride(false);   // release Ctrl
            yield return null; yield return null;

            Assert.IsFalse(_wasd.IsCrouching, "releasing Ctrl must return to the standing stance (AC1).");
            Assert.IsFalse(_castaway.IsCrouching, "the avatar's Crouch bool must clear on release.");
            _wasd.ClearCrouchOverride();
        }

        // ===================================================================================================
        // AC1 — crouching WHILE MOVING commands a SNEAK speed strictly SLOWER than the stand-walk (Sneak Walk).
        // Asserted on the COMMANDED speed (headless deltaTime≈0 stalls physical traversal — the documented trap).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator CrouchWhileMoving_CommandsReducedSneakSpeed()
        {
            // Baseline: hold W, NOT crouching → walk speed.
            _wasd.SetInputOverride(new Vector2(0f, 1f));
            _wasd.SetCrouchOverride(false);
            yield return null; yield return null;
            Assert.IsTrue(_wasd.HasInput, "sanity: W is held");
            Assert.AreEqual(WalkSpeed, _wasd.CurrentSpeed, 1e-3f,
                "moving without crouch must command the WALK speed (the baseline).");

            // Now crouch while still holding W → the reduced SNEAK speed.
            _wasd.SetCrouchOverride(true);
            yield return null; yield return null;
            Assert.IsTrue(_wasd.IsCrouching, "crouch active while moving");
            Assert.AreEqual(SneakSpeed, _wasd.CurrentSpeed, 1e-3f,
                "crouching WHILE MOVING must command the reduced SNEAK speed (the Sneak Walk — AC1).");
            Assert.Less(_wasd.CurrentSpeed, WalkSpeed,
                "the sneak speed must be strictly SLOWER than the stand-walk (a crouched move is a sneak).");

            _wasd.ClearInputOverride();
            _wasd.ClearCrouchOverride();
        }

        // ===================================================================================================
        // SNEAK-WALK STUTTER fix (86caa3kur re-soak) — WasdMovement.Start must APPLY the smooth-direct-drive
        // config to the REAL agent (autoBraking OFF + high acceleration), so the agent's internal simulation
        // TRACKS the directly-commanded velocity instead of FIGHTING it (the slow-speed hitching). The pure
        // SmoothDirectDriveConfig is EditMode-tested; THIS pins the WIRE from Start to the agent (the
        // EditMode-wiring-guard lesson — a pure-core test passing while the wire is dropped is a false-green).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator Start_AppliesSmoothDirectDrive_ToTheAgent_AutoBrakingOff_HighAccel()
        {
            // SetUp already ran WasdMovement.Start (two yields after AddComponent). The agent was authored with
            // the scene defaults (acceleration 24, autoBraking true) here; Start must have OVERRIDDEN them.
            yield return null;

            Assert.IsFalse(_agent.autoBraking,
                "WasdMovement.Start must turn the agent's autoBraking OFF — with it ON the agent decelerates the " +
                "root toward the zero desiredVelocity (no path under WASD), fighting the directly-commanded " +
                "velocity each frame = the slow-speed sneak STUTTER (86caa3kur re-soak).");
            Assert.AreEqual(WasdMovement.SmoothDriveAcceleration, _agent.acceleration, 1e-3f,
                "WasdMovement.Start must set the agent's acceleration to the high SmoothDriveAcceleration so the " +
                "simulated velocity SNAPS to the commanded velocity (no slow ramp = no slow-speed jitter). The " +
                $"agent still has the scene default ({_agent.acceleration}) — the smooth-drive wire was dropped.");
            // ATTEMPT 2 — the CONFIRMED sneak-hitch cause: local RVO avoidance perturbs the per-frame sim velocity
            // (NavMeshAgent.velocity docs), a large fraction of the step at the slow sneak speed = the jitter the
            // trace measured. Start must turn it OFF (the wire from Start to the agent — a pure-core test passing
            // while the wire is dropped is a false-green, the EditMode-wiring-guard lesson).
            Assert.AreEqual(UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance, _agent.obstacleAvoidanceType,
                "WasdMovement.Start must turn the agent's obstacleAvoidanceType to NoObstacleAvoidance — local RVO " +
                "avoidance perturbs the per-frame sim velocity, the CONFIRMED slow-speed sneak translation jitter " +
                "(run 28432489421). The agent still has the scene default — the avoidance-off wire was dropped.");
        }

        // ===================================================================================================
        // SNEAK-WALK HITCH fix (86caa3kur re-soak attempt 2 — CONFIRMED by the CI Animator+motion trace, run
        // 28432489421). The clip plays smoothly; the hitch was the NavMeshAgent's per-frame velocity being
        // PERTURBED by the local RVO avoidance pass (NavMeshAgent.velocity docs: reading velocity "may differ from
        // what you set due to collision avoidance"), a large FRACTION of the step at the slow 3 u/s sneak. The fix
        // turns avoidance OFF in Start (covered by Start_AppliesSmoothDirectDrive above) — the velocity-drive
        // channel itself is UNCHANGED. THIS test pins the two load-bearing CONTRACTS the fix must preserve (the
        // EditMode-wiring-guard lesson — assert the wire, not just the pure core; headless Time.deltaTime≈0 can't
        // measure the jitter itself):
        //   (1) the grounded move STILL populates agent.velocity (OrbitCamera's follow-lead + CastawayCharacter's
        //       Walk<->Run blend + facing ALL read followVelocitySource.velocity / agent.velocity — turning
        //       avoidance off must NOT zero the commanded velocity);
        //   (2) the grounded move keeps the agent ON the NavMesh (avoidance-off must not desync the agent off-mesh).
        // Catches the BUG CLASS (velocity-coupling dropped / agent driven off-mesh), not a jitter instance.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator GroundedMove_PopulatesAgentVelocity_AndKeepsAgentOnNavMesh()
        {
            // Hold W (grounded walk — the camera + blend read agent.velocity off this; the sneak shares the path).
            _wasd.SetInputOverride(new Vector2(0f, 1f));
            _wasd.SetCrouchOverride(false);
            yield return null; yield return null;

            Assert.IsTrue(_wasd.HasInput, "sanity: W is held (a grounded move frame ran)");
            Assert.IsTrue(_agent.isOnNavMesh,
                "the grounded move must keep the agent ON the NavMesh (turning RVO avoidance off must not desync " +
                "the agent off-mesh and break grounding/queries).");
            float planar = new Vector2(_agent.velocity.x, _agent.velocity.z).magnitude;
            Assert.Greater(planar, 1e-3f,
                "the grounded move must POPULATE agent.velocity (planar magnitude > 0) — OrbitCamera's follow-lead " +
                "+ CastawayCharacter's Walk<->Run blend + facing ALL read agent.velocity; the avoidance-off hitch " +
                "fix leaves the velocity-drive channel intact. A regression that zeroed the commanded velocity " +
                "would silently kill the camera lead (zero velocity here).");

            // Same contract holds for the crouched SNEAK (the path the fix targets) — velocity still populated.
            _wasd.SetCrouchOverride(true);
            yield return null; yield return null;
            Assert.IsTrue(_wasd.IsCrouching, "sanity: sneaking");
            float sneakPlanar = new Vector2(_agent.velocity.x, _agent.velocity.z).magnitude;
            Assert.Greater(sneakPlanar, 1e-3f,
                "the crouched SNEAK grounded move must ALSO populate agent.velocity (the camera/blend coupling is " +
                "identical for walk and sneak).");
            Assert.IsTrue(_agent.isOnNavMesh, "the sneak grounded move keeps the agent on the NavMesh too.");

            _wasd.ClearInputOverride();
            _wasd.ClearCrouchOverride();
        }

        // ===================================================================================================
        // AC2 — CROUCH WINS over sprint: Ctrl+Shift while moving drops to the SNEAK speed, never the run.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator CrouchWinsOverSprint_CtrlPlusShift_Sneaks_NotRuns()
        {
            // Hold W + Shift (sprint) → run speed (baseline, no crouch).
            _wasd.SetInputOverride(new Vector2(0f, 1f));
            _wasd.SetSprintOverride(true);
            _wasd.SetCrouchOverride(false);
            yield return null; yield return null;
            Assert.IsTrue(_wasd.IsSprinting, "sanity: Shift while moving sprints");
            Assert.AreEqual(RunSpeed, _wasd.CurrentSpeed, 1e-3f, "sprint commands the run speed (baseline).");

            // Now ALSO hold Ctrl (crouch) — crouch must WIN: sneak speed, sprint suppressed.
            _wasd.SetCrouchOverride(true);
            yield return null; yield return null;
            Assert.IsTrue(_wasd.IsCrouching, "crouch active");
            Assert.IsFalse(_wasd.IsSprinting,
                "CROUCH WINS (AC2): holding Ctrl while Shift is held must SUPPRESS the sprint state.");
            Assert.AreEqual(SneakSpeed, _wasd.CurrentSpeed, 1e-3f,
                "Ctrl+Shift while moving must command the SNEAK speed, NOT the run speed (crouch wins — AC2).");

            _wasd.ClearInputOverride();
            _wasd.ClearSprintOverride();
            _wasd.ClearCrouchOverride();
        }
    }
}
