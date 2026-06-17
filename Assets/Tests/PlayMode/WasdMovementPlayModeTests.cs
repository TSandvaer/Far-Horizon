using System.Collections;
using System.Collections.Generic;
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
    /// AC5 regression guard for WASD locomotion (ticket 86ca9yq2x) — the load-bearing test:
    ///   WASD input → CAMERA-RELATIVE move (AC1) + Walk anim drive (AC4) + feet GROUNDED on the terrain (AC3).
    ///
    /// REUSES the LocomotionSamplingHarness foreshore-rig pattern (PR #60 — ticket 86ca9a36g): a renderer-
    /// DISABLED flat NavMesh/collision PROXY slab (Y=0) over the area PLUS a renderer-ENABLED VISIBLE terrain
    /// that DIPS shoreward, a REAL NavMeshAgent, and a synthetic 100×-cm→m skinned mesh so the production
    /// CastawayCharacter.MeasureRenderedSoleWorldY BakeMesh path runs against the actual cm→m trap. The
    /// DIFFERENCE: this drives the player via the NEW production WasdMovement (camera-relative, via the
    /// input-independent SetInputOverride seam), NOT ClickToMove.MoveTo — so it proves WASD specifically keeps
    /// the float fix intact (AC3's "keep terrain/NavMesh grounding").
    ///
    /// WHY THE OVERRIDE SEAM, NOT REAL KEYSTROKES: a headless PlayMode run can't inject keyboard input, so
    /// WasdMovement exposes SetInputOverride (the WASD analog of ClickToMove.MoveTo) which feeds the SAME
    /// Update path. WHY READ THE PER-FRAME GAUGES, NOT SETTLED FEET: headless Time.deltaTime≈0 stalls the
    /// smoothing lerp (the documented headless-time trap), so grounding is asserted on the per-frame-recomputed
    /// MeshBottomWorldY / GroundHitWorldY (the harness's load-bearing pattern), not the lerped position.
    ///
    /// CATCHES THE BUG CLASS, NOT AN INSTANCE: (1) the move is asserted to follow the CAMERA's planar forward
    /// (a regression to world-forward fails), (2) agent.velocity is asserted NON-ZERO while holding input (the
    /// quantity CastawayCharacter reads to flip Idle→Walk — a regression that doesn't drive the agent fails the
    /// anim/facing too), (3) the baked sole stays on the visible ground EVERY frame (the walk-float false-green).
    /// </summary>
    public class WasdMovementPlayModeTests
    {
        private GameObject _proxySlab;      // renderer-DISABLED flat collision/NavMesh proxy (the snap must skip)
        private GameObject _visibleTerrain; // renderer-ENABLED visible terrain that DIPS shoreward (the foreshore)
        private GameObject _surfaceGo;
        private GameObject _playerGo;
        private GameObject _avatarGo;
        private GameObject _smrGo;
        private GameObject _camGo;
        private NavMeshAgent _agent;
        private WasdMovement _wasd;
        private CastawayCharacter _castaway;
        private OrbitCamera _orbit;

        private const float YDip = -0.5f;
        private const float GroundTol = 0.05f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Fresh empty scene (cross-fixture leak fix, mirrors the harness): OUR synthetic colliders must be
            // the only Ground surfaces when the snap raycast fires.
            var empty = SceneManager.CreateScene("WasdHarnessIsolated_" + System.Guid.NewGuid().ToString("N"));
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
            _proxySlab.transform.position = new Vector3(0f, -0.5f, 0f); // top face at Y=0
            _proxySlab.transform.localScale = new Vector3(40f, 1f, 40f);
            _proxySlab.GetComponent<MeshRenderer>().enabled = false;

            // (2) Renderer-ENABLED VISIBLE terrain: a ramp flat at +Z, dipping to YDip at −Z (the foreshore).
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

            // (4) The orbit camera (facing +Z by default — yaw 0). The WASD move is camera-relative, so the
            // camera defines "forward". A real Camera so WasdMovement can resolve its basis if cameraTransform
            // is left null too; here we wire it explicitly (the production path).
            _camGo = new GameObject("Main Camera");
            _camGo.AddComponent<Camera>();
            _orbit = _camGo.AddComponent<OrbitCamera>();

            // (5) The player root: the agent + the production ClickToMove (for EnsureOnNavMesh + the gate) +
            // the NEW WasdMovement driving it.
            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, 0f, 6f); // spawn mid-ramp (room to walk both ways)
            _agent = _playerGo.AddComponent<NavMeshAgent>();
            _agent.radius = 0.4f; _agent.height = 1.8f; _agent.speed = 4f;
            _agent.acceleration = 24f; _agent.angularSpeed = 999f; _agent.stoppingDistance = 0.1f;
            _agent.updateRotation = false; _agent.updateUpAxis = false;
            var ctm = _playerGo.AddComponent<ClickToMove>();
            ctm.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            _orbit.target = _playerGo.transform;

            _wasd = _playerGo.AddComponent<WasdMovement>();
            _wasd.moveSpeed = 4f;
            _wasd.cameraTransform = _camGo.transform;
            _wasd.clickToMove = ctm;

            // (6) The avatar child carrying CastawayCharacter (the production grounding/snap under test) + a
            // synthetic 100×-cm→m skinned mesh so the BakeMesh sole path runs against the real cm→m trap.
            LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero;
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = true;
            _castaway.modelSoleGround = false; // no real Animator/clip → no per-clip lift; synthetic sole at root
            _castaway.snapRate = 40f;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            AttachSyntheticSkinnedMesh_With100xCmToMNode(_avatarGo.transform);

            yield return null; // Awake resolves agent/camera
            yield return null; // ClickToMove warps onto the NavMesh; WasdMovement.Start disables click
            Assert.IsTrue(_agent.isOnNavMesh, "the agent must be on the NavMesh before driving WASD");
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
        // AC3 (handoff) — WASD disables click-to-move on Start; a click no longer drives the player.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator Wasd_DisablesClickToMove_OnStart()
        {
            yield return null;
            var ctm = _playerGo.GetComponent<ClickToMove>();
            Assert.IsFalse(ctm.clickEnabled,
                "WasdMovement.Start must disable ClickToMove's click handling (the WASD pivot replaces " +
                "click-to-move as the locomotion — AC3). The programmatic MoveTo seam stays valid.");
        }

        // ===================================================================================================
        // AC1 + AC4 — holding WASD forward MOVES the agent in the CAMERA-RELATIVE direction, and agent.velocity
        // (the quantity CastawayCharacter reads for the Idle→Walk blend + facing) goes NON-ZERO.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator HoldForward_MovesCameraRelative_AndDrivesAgentVelocity()
        {
            _orbit.SetYaw(0f);            // camera faces +Z → camera-relative forward is +Z
            yield return null;
            Vector3 spawn = _playerGo.transform.position;
            _wasd.SetInputOverride(new Vector2(0f, 1f)); // hold W

            float start = Time.time;
            bool sawVelocity = false;
            // Drive a real wall-clock window (headless deltas ≈0; sample over Time.time).
            while (Time.time - start < 5f)
            {
                if (new Vector2(_agent.velocity.x, _agent.velocity.z).magnitude > 0.5f) sawVelocity = true;
                // The move direction WasdMovement resolved must be camera-relative forward (+Z) every frame.
                if (_wasd.HasInput)
                    Assert.Greater(_wasd.LastMoveDir.z, 0.9f,
                        "holding W with the camera facing +Z must resolve a +Z (camera-relative forward) move " +
                        "direction — got " + _wasd.LastMoveDir);
                if (_playerGo.transform.position.z > spawn.z + 4f) break; // moved a real distance +Z
                yield return null;
            }
            _wasd.ClearInputOverride();

            Assert.IsTrue(sawVelocity,
                "holding W must drive the NavMeshAgent's velocity NON-ZERO — that is the exact quantity " +
                "CastawayCharacter reads to flip Idle→Walk (AC4) and to yaw the model toward travel (AC2). " +
                "A WASD driver that doesn't move the agent ships a frozen Idle character.");
            // Camera faces +Z, so W (camera-relative forward) moves the player +Z (z increases).
            Assert.Greater(_playerGo.transform.position.z, spawn.z + 2f,
                "holding W (camera-relative forward, camera facing +Z) must move the player a real distance +Z " +
                $"(camera-forward axis); moved from z={spawn.z:F2} to z={_playerGo.transform.position.z:F2}.");
        }

        // ===================================================================================================
        // AC1 — the move tracks the CAMERA HEADING: orbit the camera 180° and holding W reverses the world move.
        // (The camera-relative guarantee — a world-forward regression would move the same way regardless.)
        // ===================================================================================================
        [UnityTest]
        public IEnumerator MoveDirection_TracksCameraHeading_NotWorldAxis()
        {
            // Camera yaw 0 → forward +Z.
            _orbit.SetYaw(0f);
            yield return null;
            _wasd.SetInputOverride(new Vector2(0f, 1f));
            yield return null; yield return null;
            Vector3 dirYaw0 = _wasd.LastMoveDir;

            // Orbit the camera 180° → forward −Z. The SAME W input must now resolve a −Z move (camera-relative).
            _orbit.SetYaw(180f);
            yield return null; yield return null;
            Vector3 dirYaw180 = _wasd.LastMoveDir;
            _wasd.ClearInputOverride();

            Assert.Greater(dirYaw0.z, 0.9f, "at camera yaw 0, W resolves +Z");
            Assert.Less(dirYaw180.z, -0.9f,
                "at camera yaw 180, the SAME W input must resolve −Z — the move tracks the CAMERA heading, not " +
                "a fixed world axis (the camera-relative AC). A world-forward regression would give +Z both times.");
        }

        // ===================================================================================================
        // AC3 (LOAD-BEARING) — driving WASD across the dipping foreshore keeps the TRUE rendered sole grounded
        // on the VISIBLE terrain EVERY frame (the float fix stays intact under WASD, not just under MoveTo).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator Wasd_FeetStayGrounded_OnVisibleTerrain_EveryFrame()
        {
            Vector3 spawn = _playerGo.transform.position;
            _wasd.SetInputOverride(new Vector2(0f, -1f)); // hold S → move toward −Z (the dipped shore)
            // Wait: camera faces +Z, so S (back) moves −Z, toward the dip. (Either direction exercises the snap;
            // −Z reaches the deepest dip where the float saga was worst.)

            int checkedFrames = 0;
            float worstGap = float.NegativeInfinity;
            int worstFrame = -1;
            float worstSole = 0f, worstGround = 0f;
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
                    if (gap > worstGap) { worstGap = gap; worstFrame = frame; worstSole = sole; worstGround = ground; }
                    Assert.LessOrEqual(sole, ground + GroundTol,
                        $"frame {frame}: the TRUE rendered sole (baked actual-lowest-vertex {sole:F4}) floats " +
                        $"ABOVE the visible ground ({ground:F4}) by {gap:F4}u (> tol {GroundTol}) while driving " +
                        "WASD. The float fix must stay intact under WASD locomotion (AC3 — keep terrain/NavMesh " +
                        "grounding): feet on the VISIBLE sand every frame, not just at the standing endpoints.");
                }
                if (_playerGo.transform.position.z < spawn.z - 6f) break; // reached the dip
                yield return null;
            }
            _wasd.ClearInputOverride();

            Assert.Greater(checkedFrames, 10,
                $"the grounding assertion must have RUN on many frames (got {checkedFrames}); too few means the " +
                "baked-sole gauge never went valid — the test would pass vacuously without testing grounding.");
            Assert.Less(_playerGo.transform.position.z, spawn.z - 2f,
                "the WASD drive must have actually walked the player toward the foreshore dip (the in-motion " +
                "grounding coverage); a path that never moved never exercises the snap divergence.");
            Debug.Log($"[WasdHarness] grounded {checkedFrames} frames under WASD; worst gap {worstGap:F4}u at " +
                      $"frame {worstFrame} (sole {worstSole:F4} vs ground {worstGround:F4}).");
        }

        // Same synthetic 100×-cm→m skinned mesh as LocomotionSamplingHarness — the cm→m trap the production
        // Hyper3D/Mixamo FBX carries, so the production MeasureRenderedSoleWorldY BakeMesh path is exercised.
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
