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
    /// JUMP REWORK runtime-param guards (ticket 86ca9yq3q — the Sponsor-soak rework). These pin the RUNTIME half
    /// of the two reworked behaviors — the param state CastawayCharacter drives into the Animator each frame —
    /// while the EditMode CastawayJumpControllerTests pin the CONTROLLER WIRING (which states those params route
    /// to). Together they prove the end-to-end fix without a headless Animator clip tick (a documented false-green
    /// — unity-conventions.md §verification-lessons: "a PlayMode anim-grounding test is a FALSE-GREEN; headless
    /// Time.deltaTime≈0 so the Animator never ticks the clip").
    ///
    ///   (A) THE FLOATING-BUG CLASS — held-movement landing. The Sponsor: "the walk animation is not picked up if
    ///       W is held down while i jump … then it looks like floating." The controller routes the jump states
    ///       back to Locomotion on (Grounded && Moving) — so the load-bearing RUNTIME precondition is: ON THE
    ///       LANDING FRAME with movement held, the character must drive Grounded=true (IsAirborne==false) AND
    ///       Moving=true (IsWalking==true) in the SAME frame. If IsWalking were false on landing (the bug: the
    ///       character "stops" in the anim sense while still translating), the controller would route to Idle and
    ///       the walk anim wouldn't resume — the floating percept. We assert both are true on the grounded frame.
    ///   (B) CLIP SELECTION BY MOVEMENT STATE — the clip choice keys off IsWalking at jump time: a standing jump
    ///       reads IsWalking==false (→ JumpIdle), a moving jump reads IsWalking==true (→ JumpRunning). We assert
    ///       the IsWalking state the controller's AnyState→JumpIdle/JumpRunning conditions consume.
    ///
    /// (Why runtime-state, not an Animator-state read: see the false-green note above. The Animator-state ROUTING
    /// off these exact params is pinned deterministically in EditMode against the production controller asset.)
    ///
    /// REUSES the JumpOnSpace foreshore-rig pattern (renderer-disabled proxy slab + visible terrain + a real
    /// NavMeshAgent driven via the production WasdMovement + a synthetic 100×-cm→m skinned mesh).
    /// </summary>
    public class JumpClipSelectionAndLandingPlayModeTests
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
        private const float JumpVelocity = 5.5f;
        private const float JumpGravity = 18f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var empty = SceneManager.CreateScene("JumpAnim_" + System.Guid.NewGuid().ToString("N"));
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
            _proxySlab.transform.localScale = new Vector3(40f, 1f, 40f);
            _proxySlab.GetComponent<MeshRenderer>().enabled = false;

            _visibleTerrain = new GameObject("Ground_Play");
            if (groundLayer >= 0) _visibleTerrain.layer = groundLayer;
            var mf = _visibleTerrain.AddComponent<MeshFilter>();
            var groundMesh = new Mesh { name = "FlatVisibleGround" };
            groundMesh.vertices = new[]
            {
                new Vector3(-20f, 0f,  20f), new Vector3(20f, 0f,  20f),
                new Vector3(-20f, 0f, -20f), new Vector3(20f, 0f, -20f)
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
            _playerGo.transform.position = new Vector3(0f, 0f, 6f);
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
            _castaway.modelSoleGround = false;
            _castaway.snapRate = 40f;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            _castaway.jumpVelocity = JumpVelocity;
            _castaway.jumpGravity = JumpGravity;
            AttachSyntheticSkinnedMesh_With100xCmToMNode(_avatarGo.transform);

            _wasd = _playerGo.AddComponent<WasdMovement>();
            _wasd.moveSpeed = WalkSpeed;
            _wasd.runSpeed = WalkSpeed * 2f;
            _wasd.cameraTransform = _camGo.transform;
            _wasd.clickToMove = ctm;
            _wasd.castaway = _castaway;

            yield return null;
            yield return null;
            Assert.IsTrue(_agent.isOnNavMesh, "the agent must be on the NavMesh before driving the jump");
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
        // (A) THE FLOATING-BUG CLASS — on the LANDING frame with W held, the character drives Grounded=true AND
        // Moving=true (IsAirborne==false && IsWalking==true). That is the EXACT precondition the controller's
        // jump→Locomotion (Grounded && Moving) transition consumes — so the Walk/Run blend resumes on the grounded
        // frame instead of stalling in the finished jump pose while translating (the Sponsor's "floating").
        // ===================================================================================================
        [UnityTest]
        public IEnumerator HeldMovementLanding_DrivesGroundedAndMoving_SameFrame_FloatingBugGuard()
        {
            _orbit.SetYaw(0f);
            yield return null;

            // Hold W and settle into a real walk.
            _wasd.SetInputOverride(new Vector2(0f, 1f));
            float start = Time.time;
            while (Time.time - start < 0.6f) yield return null;
            Assert.IsTrue(_castaway.IsWalking, "must be walking (W held) before the jump");

            _wasd.RequestJump();
            yield return null;
            Assert.IsTrue(_castaway.IsAirborne, "the jump must start while moving");

            // Ride the arc; capture the FIRST grounded frame's runtime state (W still held throughout).
            bool sawGroundedFrame = false;
            bool walkingOnLanding = false;
            start = Time.time;
            while (Time.time - start < 3f)
            {
                if (!_castaway.IsAirborne)
                {
                    sawGroundedFrame = true;
                    walkingOnLanding = _castaway.IsWalking; // read in the SAME frame Grounded flipped true
                    break;
                }
                yield return null;
            }
            _wasd.ClearInputOverride();

            Assert.IsTrue(sawGroundedFrame, "the jump must land (IsAirborne returns false)");
            Assert.IsTrue(walkingOnLanding,
                "FLOATING BUG: on the landing frame the character must STILL read IsWalking==true (W held) while " +
                "IsAirborne==false — i.e. it drives Grounded=true AND Moving=true in the same frame, the exact " +
                "precondition the controller's jump→Locomotion(Grounded&&Moving) transition consumes. If IsWalking " +
                "were false here the controller would route to Idle and the walk anim wouldn't resume = the Sponsor's " +
                "'floating' (grounded, no sink, but no locomotion while translating).");
        }

        // Second-order guard on (A): a few frames AFTER landing with W still held, the character keeps reading
        // walking (the locomotion state is sustained, not a one-frame blip) — so the resumed Walk/Run is stable.
        [UnityTest]
        public IEnumerator AfterHeldMovementLanding_StaysWalking_LocomotionSustained()
        {
            _orbit.SetYaw(0f);
            yield return null;
            _wasd.SetInputOverride(new Vector2(0f, 1f));
            float start = Time.time;
            while (Time.time - start < 0.6f) yield return null;
            Assert.IsTrue(_castaway.IsWalking, "walking before the jump");

            _wasd.RequestJump();
            yield return null;
            Assert.IsTrue(_castaway.IsAirborne, "jump started");

            start = Time.time;
            while (Time.time - start < 3f) { if (!_castaway.IsAirborne) break; yield return null; }
            Assert.IsFalse(_castaway.IsAirborne, "landed");

            // Sustained over a window (W still held).
            int walkingFrames = 0, total = 0;
            start = Time.time;
            while (Time.time - start < 0.5f)
            {
                total++;
                if (_castaway.IsWalking) walkingFrames++;
                yield return null;
            }
            _wasd.ClearInputOverride();
            Assert.Greater(total, 5, "the post-landing window must have sampled several frames");
            Assert.AreEqual(total, walkingFrames,
                $"after landing with W held the character must KEEP reading walking ({walkingFrames}/{total} frames) " +
                "— a sustained locomotion resume, not a one-frame blip back into idle.");
        }

        // ===================================================================================================
        // (B) CLIP SELECTION — the IsWalking state the controller's AnyState→Jump conditions read at jump time:
        // a standing jump reads IsWalking==false (→ JumpIdle); a moving jump reads IsWalking==true (→ JumpRunning).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator JumpMovementState_DrivesClipSelection_IdleVsRunning()
        {
            // --- standing jump: IsWalking must be FALSE the frame the jump starts (→ JumpIdle) ---
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;
            Assert.IsFalse(_castaway.IsWalking, "standing still before the idle jump");

            _wasd.RequestJump();
            yield return null;
            Assert.IsTrue(_castaway.IsAirborne, "the idle jump must start");
            Assert.IsFalse(_castaway.IsWalking,
                "a STANDING jump must read IsWalking==false at jump time → the controller's AnyState→JumpIdle " +
                "(Jump && !Moving) selects the Jump_idle clip.");

            // Let it land + settle.
            start = Time.time;
            while (Time.time - start < 3f) { if (!_castaway.IsAirborne) break; yield return null; }
            start = Time.time;
            while (Time.time - start < 0.3f) yield return null;

            // --- moving jump: IsWalking must be TRUE the frame the jump starts (→ JumpRunning) ---
            _orbit.SetYaw(0f);
            _wasd.SetInputOverride(new Vector2(0f, 1f));
            start = Time.time;
            while (Time.time - start < 0.6f) yield return null;
            Assert.IsTrue(_castaway.IsWalking, "moving before the running jump");

            _wasd.RequestJump();
            yield return null;
            bool walkingAtMovingJump = _castaway.IsWalking;
            Assert.IsTrue(_castaway.IsAirborne, "the moving jump must start");
            _wasd.ClearInputOverride();
            Assert.IsTrue(walkingAtMovingJump,
                "a MOVING jump must read IsWalking==true at jump time → the controller's AnyState→JumpRunning " +
                "(Jump && Moving) selects the Jump_running clip.");
        }

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
