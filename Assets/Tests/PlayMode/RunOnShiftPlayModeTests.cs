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
    /// AC5 regression guard for RUN-ON-SHIFT (ticket 86ca9yq34 — the load-bearing PlayMode test):
    ///   hold Shift while moving (WASD) → FASTER move speed (AC1) + the RUN state (drives the Walk<->Run blend
    ///   tree) + feet GROUNDED on the visible terrain through the run cycle (AC3) — and releasing Shift returns
    ///   to walk speed (AC1). Sprint at REST does not run (no travel to speed up).
    ///
    /// REUSES the WasdMovementPlayModeTests / LocomotionSamplingHarness foreshore-rig pattern (PR #60): a
    /// renderer-DISABLED flat NavMesh/collision PROXY slab (Y=0) PLUS a renderer-ENABLED VISIBLE terrain that
    /// DIPS shoreward, a REAL NavMeshAgent driven via the production WasdMovement (camera-relative, via the
    /// input-independent SetInputOverride + SetSprintOverride seams), and a synthetic 100×-cm→m skinned mesh so
    /// the production CastawayCharacter.MeasureRenderedSoleWorldY BakeMesh path runs against the cm→m trap.
    ///
    /// WHY THE OVERRIDE SEAMS, NOT REAL KEYSTROKES: a headless PlayMode run can't inject keyboard input, so
    /// WasdMovement exposes SetInputOverride (the WASD move analog) + SetSprintOverride (the Shift analog) which
    /// feed the SAME Update path. WHY ASSERT COMMANDED SPEED, NOT DISPLACEMENT: headless Time.deltaTime≈0 stalls
    /// real traversal, so the run vs walk SPEED is asserted on the COMMANDED quantity (WasdMovement.CurrentSpeed /
    /// agent.velocity magnitude / CastawayCharacter.CurrentSpeed), and grounding on the per-frame-recomputed
    /// MeshBottomWorldY / GroundHitWorldY (the harness's load-bearing per-frame-gauge pattern).
    ///
    /// CATCHES THE BUG CLASS, NOT AN INSTANCE: (1) Shift drives a STRICTLY FASTER agent velocity than no-Shift
    /// (a regression that ignores Shift fails — the run never speeds up), (2) CastawayCharacter reads that faster
    /// speed and flips IsRunning + drives a higher Speed param (the blend-tree input — a regression that doesn't
    /// surface the speed leaves the Run clip un-blended), (3) the baked sole stays on the visible ground EVERY
    /// frame WHILE RUNNING (the run-clip hip-lift float false-green — AC3), (4) release-Shift returns to walk
    /// speed, (5) Shift at rest does NOT run.
    /// </summary>
    public class RunOnShiftPlayModeTests
    {
        private GameObject _proxySlab;
        private GameObject _visibleTerrain;
        private GameObject _surfaceGo;
        private GameObject _playerGo;
        private GameObject _avatarGo;
        private GameObject _smrGo;
        private GameObject _camGo;
        private NavMeshAgent _agent;
        private WasdMovement _wasd;
        private CastawayCharacter _castaway;
        private OrbitCamera _orbit;

        private const float WalkSpeed = 4f;
        private const float RunSpeed = 8f;   // 2× walk — a clear run, comfortably above the run threshold
        private const float YDip = -0.5f;
        private const float GroundTol = 0.05f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var empty = SceneManager.CreateScene("RunOnShiftIsolated_" + System.Guid.NewGuid().ToString("N"));
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

            // (1) Renderer-DISABLED flat NavMesh/collision PROXY slab at Y=0 (the snap must skip it).
            _proxySlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _proxySlab.name = "TestGround";
            if (groundLayer >= 0) _proxySlab.layer = groundLayer;
            _proxySlab.transform.position = new Vector3(0f, -0.5f, 0f);
            _proxySlab.transform.localScale = new Vector3(40f, 1f, 40f);
            _proxySlab.GetComponent<MeshRenderer>().enabled = false;

            // (2) Renderer-ENABLED VISIBLE terrain: a ramp flat at +Z dipping to YDip at −Z (the foreshore).
            _visibleTerrain = new GameObject("Ground_Play");
            if (groundLayer >= 0) _visibleTerrain.layer = groundLayer;
            var mf = _visibleTerrain.AddComponent<MeshFilter>();
            var rampMesh = new Mesh { name = "ForeshoreRamp" };
            rampMesh.vertices = new[]
            {
                new Vector3(-20f, 0f,   16f), new Vector3(20f, 0f,   16f),
                new Vector3(-20f, YDip, -16f), new Vector3(20f, YDip, -16f)
            };
            rampMesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            rampMesh.RecalculateNormals();
            mf.sharedMesh = rampMesh;
            _visibleTerrain.AddComponent<MeshRenderer>().enabled = true;
            _visibleTerrain.AddComponent<MeshCollider>().sharedMesh = rampMesh;

            // (3) Bake a NavMesh off the flat proxy slab.
            _surfaceGo = new GameObject("Surface");
            var surface = _surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            // (4) The orbit camera (facing +Z by default — yaw 0). Run is camera-relative like walk.
            _camGo = new GameObject("Main Camera");
            _camGo.AddComponent<Camera>();
            _orbit = _camGo.AddComponent<OrbitCamera>();

            // (5) The player root: the agent + ClickToMove + the WasdMovement driving it (walk + run speeds).
            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, 0f, 6f);
            _agent = _playerGo.AddComponent<NavMeshAgent>();
            _agent.radius = 0.4f; _agent.height = 1.8f; _agent.speed = RunSpeed; // agent cap ≥ run speed
            _agent.acceleration = 48f; _agent.angularSpeed = 999f; _agent.stoppingDistance = 0.1f;
            _agent.updateRotation = false; _agent.updateUpAxis = false;
            var ctm = _playerGo.AddComponent<ClickToMove>();
            ctm.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            _orbit.target = _playerGo.transform;

            _wasd = _playerGo.AddComponent<WasdMovement>();
            _wasd.moveSpeed = WalkSpeed;
            _wasd.runSpeed = RunSpeed;
            _wasd.cameraTransform = _camGo.transform;
            _wasd.clickToMove = ctm;

            // (6) The avatar child carrying CastawayCharacter + a synthetic 100×-cm→m skinned mesh. The run
            // threshold is set BETWEEN walk and run speed so IsRunning flips only when sprinting.
            LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero;
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = true;
            _castaway.modelSoleGround = false; // no real Animator/clip → synthetic sole at root (grounding-only rig)
            _castaway.snapRate = 40f;
            _castaway.runSpeedThreshold = (WalkSpeed + RunSpeed) * 0.5f; // 6 — between walk(4) and run(8)
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            AttachSyntheticSkinnedMesh_With100xCmToMNode(_avatarGo.transform);

            yield return null; // Awake resolves agent/camera
            yield return null; // ClickToMove warps onto the NavMesh; WasdMovement.Start disables click
            Assert.IsTrue(_agent.isOnNavMesh, "the agent must be on the NavMesh before driving run-on-Shift");
        }

        [TearDown]
        public void TearDown()
        {
            if (_smrGo != null) Object.DestroyImmediate(_smrGo);
            if (_avatarGo != null) Object.DestroyImmediate(_avatarGo);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_visibleTerrain != null) Object.DestroyImmediate(_visibleTerrain);
            if (_proxySlab != null) Object.DestroyImmediate(_proxySlab);
            if (_surfaceGo != null) Object.DestroyImmediate(_surfaceGo);
            NavMesh.RemoveAllNavMeshData();
        }

        // ===================================================================================================
        // AC1 — holding Shift while moving drives a STRICTLY FASTER agent velocity than walking, and releasing
        // Shift returns to the walk speed. (The run/walk SPEED split is the heart of AC1.)
        // ===================================================================================================
        [UnityTest]
        public IEnumerator ShiftHeld_DrivesRunSpeed_ReleaseReturnsToWalk()
        {
            _orbit.SetYaw(0f);
            yield return null;

            // WALK: hold W, no Shift → walk speed.
            _wasd.SetInputOverride(new Vector2(0f, 1f));
            _wasd.SetSprintOverride(false);
            float walkVel = 0f;
            float start = Time.time;
            while (Time.time - start < 2f)
            {
                float v = new Vector2(_agent.velocity.x, _agent.velocity.z).magnitude;
                if (v > walkVel) walkVel = v;
                yield return null;
            }
            Assert.IsFalse(_wasd.IsSprinting, "no Shift → not sprinting");
            Assert.That(_wasd.CurrentSpeed, Is.EqualTo(WalkSpeed).Within(0.01f),
                $"no Shift → WasdMovement commands the WALK speed ({WalkSpeed}); got {_wasd.CurrentSpeed:F2}");

            // RUN: keep W, now hold Shift → run speed (strictly faster).
            _wasd.SetSprintOverride(true);
            float runVel = 0f;
            start = Time.time;
            while (Time.time - start < 2f)
            {
                float v = new Vector2(_agent.velocity.x, _agent.velocity.z).magnitude;
                if (v > runVel) runVel = v;
                yield return null;
            }
            Assert.IsTrue(_wasd.IsSprinting, "Shift held WHILE MOVING → sprinting");
            Assert.That(_wasd.CurrentSpeed, Is.EqualTo(RunSpeed).Within(0.01f),
                $"Shift+move → WasdMovement commands the RUN speed ({RunSpeed}); got {_wasd.CurrentSpeed:F2}");
            Assert.Greater(runVel, walkVel + 1f,
                $"holding Shift must drive a STRICTLY FASTER agent velocity than walking (run {runVel:F2} vs " +
                $"walk {walkVel:F2}) — a regression that ignores Shift ships a run that never speeds up (AC1).");

            // RELEASE: drop Shift → back to walk speed (AC1 — release returns to walk).
            _wasd.SetSprintOverride(false);
            yield return null; yield return null;
            Assert.IsFalse(_wasd.IsSprinting, "releasing Shift → no longer sprinting");
            Assert.That(_wasd.CurrentSpeed, Is.EqualTo(WalkSpeed).Within(0.01f),
                $"releasing Shift must return to the WALK speed ({WalkSpeed}); got {_wasd.CurrentSpeed:F2}");

            _wasd.ClearInputOverride();
            _wasd.ClearSprintOverride();
        }

        // ===================================================================================================
        // AC1 (anim drive) — CastawayCharacter reads the faster run velocity and flips IsRunning + surfaces a
        // higher Speed (the Walk<->Run blend-tree input). A regression that doesn't surface the run speed leaves
        // the Run clip un-blended (the character walks at run speed).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator CastawayReadsRunSpeed_FlipsIsRunning_AndDrivesHigherSpeed()
        {
            _orbit.SetYaw(0f);
            yield return null;
            _wasd.SetInputOverride(new Vector2(0f, 1f));

            // WALK: moving but not sprinting → IsWalking true, IsRunning FALSE, Speed in the walk band.
            _wasd.SetSprintOverride(false);
            bool sawWalkingNotRunning = false;
            float walkSpeedSeen = 0f;
            float start = Time.time;
            while (Time.time - start < 2.5f)
            {
                if (_castaway.IsWalking && !_castaway.IsRunning)
                {
                    sawWalkingNotRunning = true;
                    walkSpeedSeen = _castaway.CurrentSpeed;
                }
                yield return null;
            }
            Assert.IsTrue(sawWalkingNotRunning,
                "while walking (Shift up) the castaway must read WALKING but NOT running (the blend stays in the " +
                "walk band) — IsRunning=" + _castaway.IsRunning + " speed=" + _castaway.CurrentSpeed.ToString("0.00"));

            // RUN: hold Shift → IsRunning true, Speed in the run band (strictly above the walk speed seen).
            _wasd.SetSprintOverride(true);
            bool sawRunning = false;
            float runSpeedSeen = 0f;
            start = Time.time;
            while (Time.time - start < 2.5f)
            {
                if (_castaway.IsRunning) { sawRunning = true; runSpeedSeen = _castaway.CurrentSpeed; break; }
                yield return null;
            }
            _wasd.ClearInputOverride();
            _wasd.ClearSprintOverride();

            Assert.IsTrue(sawRunning,
                "holding Shift while moving must flip CastawayCharacter.IsRunning (the agent reaches run speed " +
                "≥ runSpeedThreshold) — a regression that doesn't surface the run speed leaves the Run clip " +
                "un-blended; CurrentSpeed=" + _castaway.CurrentSpeed.ToString("0.00"));
            Assert.Greater(runSpeedSeen, walkSpeedSeen + 1f,
                $"the Speed the blend tree consumes must be STRICTLY HIGHER while running ({runSpeedSeen:F2}) than " +
                $"walking ({walkSpeedSeen:F2}) — the faster Speed is what blends the Run clip in (AC1).");
            Assert.GreaterOrEqual(runSpeedSeen, _castaway.runSpeedThreshold,
                $"the running Speed ({runSpeedSeen:F2}) must reach the run threshold ({_castaway.runSpeedThreshold:F2}) " +
                "— else the blend never reaches the Run clip.");
        }

        // ===================================================================================================
        // AC3 (LOAD-BEARING) — running across the dipping foreshore keeps the TRUE rendered sole grounded on the
        // VISIBLE terrain EVERY frame. The run-clip hip-lift float fix (modelSoleGround, UNTOUCHED) must hold at
        // run speed, not just walk speed. (Synthetic rig: the snap/grounding path is exercised; the clip-lift
        // half is proven separately in EDITMODE via CastawayRunClipGroundedTests — the headless-time trap.)
        // ===================================================================================================
        [UnityTest]
        public IEnumerator Running_FeetStayGrounded_OnVisibleTerrain_EveryFrame()
        {
            _orbit.SetYaw(0f);
            yield return null;
            Vector3 spawn = _playerGo.transform.position;
            _wasd.SetInputOverride(new Vector2(0f, -1f)); // hold S → toward −Z (the deepest dip)
            _wasd.SetSprintOverride(true);                // RUNNING toward the dip

            int checkedFrames = 0;
            float worstGap = float.NegativeInfinity;
            int frame = 0;
            float start = Time.time;
            while (Time.time - start < 6f)
            {
                float sole = _castaway.MeshBottomWorldY;
                float ground = _castaway.GroundHitWorldY;
                frame++;
                if (!float.IsNaN(sole) && !float.IsNaN(ground))
                {
                    checkedFrames++;
                    float gap = sole - ground;
                    if (gap > worstGap) worstGap = gap;
                    Assert.LessOrEqual(sole, ground + GroundTol,
                        $"frame {frame}: the TRUE rendered sole ({sole:F4}) floats ABOVE the visible ground " +
                        $"({ground:F4}) by {gap:F4}u (> tol {GroundTol}) while RUNNING. The float fix must hold " +
                        "under run speed (AC3 — feet on the visible sand every frame, not just while walking).");
                }
                if (_playerGo.transform.position.z < spawn.z - 6f) break;
                yield return null;
            }
            _wasd.ClearInputOverride();
            _wasd.ClearSprintOverride();

            Assert.Greater(checkedFrames, 10,
                $"the grounding assertion must have RUN on many frames (got {checkedFrames}); too few means the " +
                "baked-sole gauge never went valid — the test would pass vacuously.");
            Assert.Less(_playerGo.transform.position.z, spawn.z - 2f,
                "the run must have actually traversed the player toward the foreshore dip (in-motion grounding " +
                "coverage at run speed).");
            Debug.Log($"[RunOnShift] grounded {checkedFrames} frames while RUNNING; worst gap {worstGap:F4}u.");
        }

        // ===================================================================================================
        // AC1 (edge) — Sprint at REST does NOT run (no travel to speed up). Holding Shift with no move input
        // must keep IsSprinting false + the commanded speed 0 (the character stays Idle, not a running-in-place).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator ShiftAtRest_DoesNotRun()
        {
            yield return null;
            _wasd.SetInputOverride(Vector2.zero); // no move
            _wasd.SetSprintOverride(true);        // Shift held

            float start = Time.time;
            while (Time.time - start < 1f)
            {
                Assert.IsFalse(_wasd.IsSprinting,
                    "holding Shift at a STANDSTILL must not read as sprinting (no travel to speed up) — sprint " +
                    "gates on HasInput");
                Assert.That(_wasd.CurrentSpeed, Is.EqualTo(0f).Within(0.001f),
                    "Shift at rest commands zero speed (the character holds Idle, not a run-in-place)");
                yield return null;
            }
            _wasd.ClearInputOverride();
            _wasd.ClearSprintOverride();
        }

        // Same synthetic 100×-cm→m skinned mesh as LocomotionSamplingHarness / WasdMovementPlayModeTests — the
        // cm→m trap the production Hyper3D/Mixamo FBX carries, so the production MeasureRenderedSoleWorldY
        // BakeMesh path is exercised.
        private void AttachSyntheticSkinnedMesh_With100xCmToMNode(Transform avatarRoot)
        {
            _smrGo = new GameObject("Synthetic100xSkin");
            _smrGo.transform.SetParent(avatarRoot, false);
            _smrGo.transform.localPosition = Vector3.zero;
            _smrGo.transform.localScale = Vector3.one * 100f;
            var smr = _smrGo.AddComponent<SkinnedMeshRenderer>();

            var mesh = new Mesh { name = "SyntheticSole" };
            const float s = 0.01f;
            float lo = 0f, hi = 50f * s;
            mesh.vertices = new[]
            {
                new Vector3(-20f * s, lo, 0f), new Vector3(20f * s, lo, 0f),
                new Vector3(-20f * s, hi, 0f), new Vector3(20f * s, hi, 0f)
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            var bw = new BoneWeight[4];
            for (int i = 0; i < 4; i++) { bw[i].boneIndex0 = 0; bw[i].weight0 = 1f; }
            mesh.boneWeights = bw;
            mesh.bindposes = new[] { Matrix4x4.identity };
            smr.bones = new[] { _smrGo.transform };
            smr.rootBone = _smrGo.transform;
            smr.sharedMesh = mesh;
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0f, (lo + hi) * 0.5f, 0f),
                                         new Vector3(0.6f, (hi - lo) + 1.0f, 0.2f));
        }
    }
}
