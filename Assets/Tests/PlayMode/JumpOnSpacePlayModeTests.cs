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
    /// AC5 regression guard for JUMP-ON-SPACE (ticket 86ca9yq3q — the load-bearing PlayMode test):
    ///   press Space (via WasdMovement.RequestJump — the input-independent seam) → the avatar root Y RISES then
    ///   RETURNS to grounded (AC1: vertical impulse → arc → land, control returns); the per-frame GROUND SNAP is
    ///   SUSPENDED while airborne (AC3 — else modelSoleGround / the root snap would PIN the feet mid-jump) and
    ///   RE-ENGAGES on landing; a held prop parented to the hand stays seated through the arc (AC4); AND a
    ///   regression guard that the GROUNDED-state float fix is UNCHANGED (AC3/AC5 — the airborne gate is an
    ///   "only-when-grounded" wrapper, NOT a change to ApplyGroundSnap).
    ///
    /// REUSES the RunOnShift / WasdMovement foreshore-rig pattern (PR #60/#68): a renderer-DISABLED flat NavMesh
    /// PROXY slab (Y=0) PLUS a renderer-ENABLED VISIBLE terrain, a REAL NavMeshAgent driven via the production
    /// WasdMovement, and a synthetic 100×-cm→m skinned mesh so the production CastawayCharacter BakeMesh sole path
    /// runs against the cm→m trap. Jump is triggered via WasdMovement.RequestJump (the headless Space analog).
    ///
    /// WHY THE ARC ADVANCES HEADLESSLY: AdvanceJump uses a nominal 1/60 step when Time.deltaTime≈0 (the documented
    /// -batchmode headless-time trap), so the ballistic arc rises + lands deterministically in a headless run —
    /// unlike the Animator (which never ticks the clip headlessly; the clip-side is guarded in EDITMODE).
    ///
    /// CATCHES THE BUG CLASS, NOT AN INSTANCE: (1) Space lifts the avatar root off the grounded baseline (a
    /// regression that ignores the jump never leaves the ground), (2) the snap is SUSPENDED airborne (a regression
    /// that keeps snapping pins the feet — JumpHeight would stay ~0 / the root never rises), (3) the avatar RETURNS
    /// to the grounded baseline on landing (a regression that never lands leaves the player floating), (4) the held
    /// prop stays parented + within tolerance of the hand through the arc (AC4 — no detach/null), (5) the GROUNDED
    /// snap is byte-equivalent with/without the jump code in the path (AC5 regression — the float fix is intact).
    /// </summary>
    public class JumpOnSpacePlayModeTests
    {
        private GameObject _proxySlab;
        private GameObject _visibleTerrain;
        private GameObject _surfaceGo;
        private GameObject _playerGo;
        private GameObject _avatarGo;
        private GameObject _smrGo;
        private GameObject _handBone;
        private GameObject _heldProp;
        private GameObject _camGo;
        private NavMeshAgent _agent;
        private WasdMovement _wasd;
        private CastawayCharacter _castaway;
        private OrbitCamera _orbit;

        private const float WalkSpeed = 4f;
        private const float JumpVelocity = 5.5f;
        private const float JumpGravity = 18f;
        private const float GroundTol = 0.05f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var empty = SceneManager.CreateScene("JumpIsolated_" + System.Guid.NewGuid().ToString("N"));
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

            // (2) Renderer-ENABLED VISIBLE terrain — a flat patch at Y=0 (we jump in place; the grounding path
            // is the foreshore-dip case the run/walk tests cover, so here a flat visible ground keeps the jump
            // arc the variable under test).
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

            // (3) Bake a NavMesh off the flat proxy slab.
            _surfaceGo = new GameObject("Surface");
            var surface = _surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            // (4) The orbit camera (facing +Z by default).
            _camGo = new GameObject("Main Camera");
            _camGo.AddComponent<Camera>();
            _orbit = _camGo.AddComponent<OrbitCamera>();

            // (5) The player root: agent + ClickToMove + the production WasdMovement (the jump owner is wired below).
            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, 0f, 6f);
            _agent = _playerGo.AddComponent<NavMeshAgent>();
            _agent.radius = 0.4f; _agent.height = 1.8f; _agent.speed = WalkSpeed;
            _agent.acceleration = 48f; _agent.angularSpeed = 999f; _agent.stoppingDistance = 0.1f;
            _agent.updateRotation = false; _agent.updateUpAxis = false;
            var ctm = _playerGo.AddComponent<ClickToMove>();
            ctm.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            _orbit.target = _playerGo.transform;

            // (6) The avatar child carrying CastawayCharacter + a synthetic 100×-cm→m skinned mesh + a hand bone
            // with a held prop child (the AC4 axe-stays-seated surface).
            LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero;
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = true;
            _castaway.modelSoleGround = false; // no real Animator/clip → synthetic sole at root (grounding-only rig)
            _castaway.snapRate = 40f;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            _castaway.jumpVelocity = JumpVelocity;
            _castaway.jumpGravity = JumpGravity;
            AttachSyntheticSkinnedMesh_With100xCmToMNode(_avatarGo.transform);
            AttachHandWithHeldProp(_avatarGo.transform);

            // The production WASD wired to the agent + the castaway (the jump owner). RequestJump is the Space seam.
            _wasd = _playerGo.AddComponent<WasdMovement>();
            _wasd.moveSpeed = WalkSpeed;
            _wasd.runSpeed = WalkSpeed * 2f;
            _wasd.cameraTransform = _camGo.transform;
            _wasd.clickToMove = ctm;
            _wasd.castaway = _castaway;

            yield return null; // Awake resolves agent/camera/castaway
            yield return null; // ClickToMove warps onto the NavMesh; WasdMovement.Start disables click
            Assert.IsTrue(_agent.isOnNavMesh, "the agent must be on the NavMesh before driving the jump");
        }

        [TearDown]
        public void TearDown()
        {
            if (_heldProp != null) Object.DestroyImmediate(_heldProp);
            if (_handBone != null) Object.DestroyImmediate(_handBone);
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
        // AC1 + AC3 (LOAD-BEARING) — Space (idle) → the avatar root RISES (arc), the ground snap is SUSPENDED
        // while airborne, then the root RETURNS to the grounded baseline on landing + control returns.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator SpaceWhileIdle_RootRises_SnapSuspendedAirborne_ThenReGrounds()
        {
            // Settle grounded first; capture the grounded baseline local-Y the snap planted.
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;
            Assert.IsFalse(_castaway.IsAirborne, "must be grounded before the jump");
            float groundedLocalY = _avatarGo.transform.localPosition.y;

            // Press Space (idle — no move input).
            _wasd.RequestJump();
            yield return null;
            Assert.IsTrue(_castaway.IsAirborne, "Space must start a jump (IsAirborne true on the frame after the press)");

            // Watch the arc: the root must rise above the grounded baseline, the snap stays suspended (the apex
            // height is clearly > 0), then it lands back at the grounded baseline.
            float maxHeight = 0f;
            float maxLocalY = groundedLocalY;
            bool sawAirborne = false, landed = false;
            start = Time.time;
            while (Time.time - start < 3f)
            {
                if (_castaway.IsAirborne)
                {
                    sawAirborne = true;
                    maxHeight = Mathf.Max(maxHeight, _castaway.JumpHeight);
                    maxLocalY = Mathf.Max(maxLocalY, _avatarGo.transform.localPosition.y);
                }
                else if (sawAirborne)
                {
                    landed = true;
                    break;
                }
                yield return null;
            }

            Assert.IsTrue(landed, "the jump must LAND (IsAirborne returns false) — a jump that never lands leaves " +
                "the player floating (control never returns; AC1)");
            Assert.Greater(maxHeight, 0.3f,
                $"the jump arc must rise clearly off the ground (apex JumpHeight {maxHeight:F3} > 0.3u) — a " +
                "regression that keeps snapping the feet down (snap NOT suspended) pins JumpHeight near 0 (AC3).");
            Assert.Greater(maxLocalY, groundedLocalY + 0.3f,
                $"the avatar root local-Y must RISE above the grounded baseline ({groundedLocalY:F3}) during the " +
                $"arc (peaked {maxLocalY:F3}) — proving the airborne ground-snap suspension lets the body leave " +
                "the ground (AC3).");

            // RE-GROUNDED: after landing the snap resumes; the root settles back to (≈) the grounded baseline.
            start = Time.time;
            while (Time.time - start < 0.5f) yield return null;
            Assert.IsFalse(_castaway.IsAirborne, "after landing the castaway must be grounded again (control returned)");
            Assert.That(_avatarGo.transform.localPosition.y, Is.EqualTo(groundedLocalY).Within(0.05f),
                $"after landing the avatar root must RETURN to the grounded baseline ({groundedLocalY:F3}); got " +
                $"{_avatarGo.transform.localPosition.y:F3} — the snap must re-engage with the grounded behavior intact.");
        }

        // ===================================================================================================
        // AC1 — can jump WHILE MOVING (WASD held). The agent keeps owning XZ; the jump adds the vertical arc.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator SpaceWhileMoving_JumpsAndKeepsMoving()
        {
            _orbit.SetYaw(0f);
            yield return null;
            _wasd.SetInputOverride(new Vector2(0f, 1f)); // hold W
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;
            float zBefore = _playerGo.transform.position.z;

            _wasd.RequestJump();
            yield return null;
            Assert.IsTrue(_castaway.IsAirborne, "must be able to jump WHILE MOVING (AC1)");

            // While airborne the XZ keeps advancing (the agent owns it; the jump doesn't zero the move velocity).
            bool advancedWhileAirborne = false;
            start = Time.time;
            while (Time.time - start < 3f)
            {
                if (_castaway.IsAirborne && _playerGo.transform.position.z > zBefore + 0.05f)
                    advancedWhileAirborne = true;
                if (!_castaway.IsAirborne && advancedWhileAirborne) break;
                yield return null;
            }
            _wasd.ClearInputOverride();

            Assert.IsTrue(advancedWhileAirborne,
                "the player must keep MOVING (XZ advances) while airborne — the jump only adds the vertical arc, " +
                "the agent still owns horizontal travel (AC1: jump while moving).");
        }

        // ===================================================================================================
        // AC4 — the held prop (parented to the hand bone) stays SEATED + attached through the whole jump arc.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator HeldProp_StaysSeated_ThroughTheJumpArc()
        {
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            // The prop is a child of the hand bone with a fixed local pose — its hand-LOCAL offset must be
            // invariant through the arc (the arc moves the whole avatar; the prop rides the hierarchy).
            Vector3 localPosBefore = _heldProp.transform.localPosition;

            _wasd.RequestJump();
            yield return null;
            Assert.IsTrue(_castaway.IsAirborne, "jump must have started");

            float worstLocalDrift = 0f;
            int checkedFrames = 0;
            start = Time.time;
            while (Time.time - start < 3f)
            {
                Assert.IsNotNull(_heldProp, "the held prop must not be destroyed during the jump (AC4 — no detach)");
                Assert.AreEqual(_handBone.transform, _heldProp.transform.parent,
                    "the held prop must stay PARENTED to the hand bone through the arc (AC4 — no detach/reparent)");
                worstLocalDrift = Mathf.Max(worstLocalDrift,
                    Vector3.Distance(_heldProp.transform.localPosition, localPosBefore));
                checkedFrames++;
                if (!_castaway.IsAirborne) break;
                yield return null;
            }

            Assert.Greater(checkedFrames, 5, "the AC4 assertion must have run across many airborne frames");
            Assert.Less(worstLocalDrift, 1e-4f,
                $"the held prop's HAND-LOCAL pose must stay invariant through the jump arc (drift {worstLocalDrift:E2} " +
                "≈ 0) — it rides the hand via the hierarchy; the jump must not break the axe rig (AC4).");
        }

        // ===================================================================================================
        // AC5 REGRESSION — the GROUNDED-state float fix is UNCHANGED. With the jump code in the path but NO jump
        // fired, the grounded snap must STILL plant the synthetic sole on the visible ground every frame (the
        // 9-attempt float fix's grounded behavior is intact; the airborne gate is an only-when-grounded wrapper).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator GroundedFloatFix_IsUnchanged_WhenNotJumping()
        {
            yield return null;
            int checkedFrames = 0;
            float worstGap = float.NegativeInfinity;
            float start = Time.time;
            while (Time.time - start < 2.5f)
            {
                Assert.IsFalse(_castaway.IsAirborne, "no jump fired → must stay grounded the whole time");
                float sole = _castaway.MeshBottomWorldY;
                float ground = _castaway.GroundHitWorldY;
                if (!float.IsNaN(sole) && !float.IsNaN(ground))
                {
                    checkedFrames++;
                    float gap = sole - ground;
                    if (gap > worstGap) worstGap = gap;
                    Assert.LessOrEqual(sole, ground + GroundTol,
                        $"GROUNDED (no jump): the rendered sole ({sole:F4}) floats above the visible ground " +
                        $"({ground:F4}) by {gap:F4}u — the grounded float fix must be UNCHANGED by the jump code " +
                        "(AC5 regression: the airborne gate is an only-when-grounded wrapper, NOT a snap change).");
                }
                yield return null;
            }
            Assert.Greater(checkedFrames, 10,
                $"the grounded-snap assertion must have run on many frames (got {checkedFrames}) — too few means " +
                "the baked-sole gauge never went valid and the regression guard passed vacuously.");
            Debug.Log($"[JumpOnSpace] grounded float fix intact: {checkedFrames} frames, worst gap {worstGap:F4}u.");
        }

        // ===================================================================================================
        // AC1 (edge) — no double-jump: a Space press while already airborne is ignored (TryJump grounded-only).
        // ===================================================================================================
        [UnityTest]
        public IEnumerator SpaceMidAir_IsIgnored_NoDoubleJump()
        {
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            _wasd.RequestJump();
            yield return null;
            Assert.IsTrue(_castaway.IsAirborne, "first jump started");
            float heightAtMidPress = _castaway.JumpHeight;

            // Re-press mid-air at the apex-ish: it must NOT restart/boost the arc.
            yield return null; yield return null;
            bool reJumpReturned = _castaway.TryJump(); // call the API directly to read the return value
            Assert.IsFalse(reJumpReturned, "TryJump while airborne must return false (no double-jump)");

            // The arc still completes to a single landing.
            bool landed = false;
            start = Time.time;
            while (Time.time - start < 3f)
            {
                if (!_castaway.IsAirborne) { landed = true; break; }
                yield return null;
            }
            Assert.IsTrue(landed, "the single jump must still land normally after the ignored mid-air re-press");
        }

        // Same synthetic 100×-cm→m skinned mesh as RunOnShift / WasdMovement tests — the cm→m trap the production
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

        // A hand bone with a held prop child (a stand-in for HeroAxe riding mixamorig:RightHand). The prop is a
        // fixed-local-pose child of the hand, so it rides the avatar through the jump arc via the hierarchy —
        // exactly how the real held axe (which HeldAxeRig re-seats off the hand each frame) survives the jump.
        private void AttachHandWithHeldProp(Transform avatarRoot)
        {
            _handBone = new GameObject("mixamorig:RightHand");
            _handBone.transform.SetParent(avatarRoot, false);
            _handBone.transform.localPosition = new Vector3(0.3f, 1.0f, 0.1f);

            _heldProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _heldProp.name = "HeroAxe";
            Object.DestroyImmediate(_heldProp.GetComponent<Collider>());
            _heldProp.transform.SetParent(_handBone.transform, false);
            _heldProp.transform.localPosition = new Vector3(0.02f, -0.05f, 0.01f);
            _heldProp.transform.localRotation = Quaternion.Euler(10f, 90f, -50f);
            _heldProp.transform.localScale = Vector3.one * 0.4f;
        }
    }
}
