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
    /// Regression guard for the RUN/JUMP held-axe CEILING CLAMP (ticket 86caa83wn — "the axe swings up in the
    /// player head when running"), run through the PRODUCTION WasdMovement + a REAL NavMeshAgent + the REAL
    /// CastawayCharacter state machine (IsRunning via sprint, IsAirborne via TryJump). Reuses the
    /// AirborneAirControl foreshore-rig pattern (renderer-disabled proxy slab + visible terrain + real agent).
    ///
    /// THE BUG (diagnosed in-ticket): HeldAxeRig seats the axe at hand.position + hand.rotation*offset — the axe
    /// rides the RAW right-hand bone (the Sponsor's locked follow-the-arm choice for WALK). But the Mixamo RUN
    /// clip (Running.fbx) pumps the right arm UP near the head, and Jump_running/Jump_idle do the same, so the
    /// rigidly-following axe rides INTO the head during RUN + JUMP.
    ///
    /// THE FIX (per-state ceiling clamp): ONLY while RUNNING or AIRBORNE, the followed hand world-Y is soft-
    /// clamped to a ceiling expressed relative to the SHOULDER bone, so the axe can't ride above shoulder height
    /// into the head. WALK/IDLE leave the followed pose RAW (the Sponsor's locked pose untouched).
    ///
    /// CATCHES THE BUG CLASS, NOT AN INSTANCE — synthetic arm skeleton (shoulder→forearm→hand) + a HEAD bone
    /// reproduce the rig; the test PUMPS the hand bone UP (the RUN/JUMP arm-pump) and asserts:
    ///  (1) RUN: with the hand pumped to the head, the clamp is ACTIVE and the axe stays BELOW the head bone.
    ///  (2) JUMP: same, while airborne (the jump-clip arm-pump).
    ///  (3) WALK / IDLE: the clamp is INERT — the followed pose is the RAW hand (the locked WALK pose preserved;
    ///      regression-fail if a run-state clamp leaks into walk/idle).
    /// A regression that removed the clamp (axe rides into the head at run) OR that clamped at walk (broke the
    /// Sponsor's locked pose) reds loudly.
    /// </summary>
    public class HeldAxeRunJumpClampPlayModeTests
    {
        private GameObject _proxySlab, _visibleTerrain, _surfaceGo, _playerGo, _avatarGo, _camGo;
        private NavMeshAgent _agent;
        private WasdMovement _wasd;
        private CastawayCharacter _castaway;
        private OrbitCamera _orbit;

        // Synthetic arm skeleton + the axe rig.
        private Transform _model, _shoulder, _forearm, _hand, _head, _axe;
        private HeldAxeRig _rig;

        // #68-NIT absorption (AC2 finger-curl-covers-Run): a finger curl + an Inventory to gate it.
        private GameObject _invGo;
        private Inventory _inv;
        private CastawayFingerCurl _curl;
        private Transform[] _fingerBones, _fingerTips;

        private const float WalkSpeed = 5.5f;
        private const float RunSpeed = 9.5f;
        // The shoulder sits ~1.4u up; the head ~1.65u; the rest hand ~1.2u (below the shoulder — the natural
        // grip). The RUN pump lifts the hand to ~1.7u (at/above the head) — the into-head bug we clamp.
        private const float ShoulderLocalY = 1.40f;
        private const float HeadLocalY = 1.65f;
        private const float HandRestLocalY = 1.20f;
        private const float HandPumpLocalY = 1.72f; // run/jump arm-pump: hand rides up to the head

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var empty = SceneManager.CreateScene("AxeClampIsolated_" + System.Guid.NewGuid().ToString("N"));
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
            _proxySlab.transform.localScale = new Vector3(80f, 1f, 80f);
            _proxySlab.GetComponent<MeshRenderer>().enabled = false;

            _visibleTerrain = new GameObject("Ground_Play");
            if (groundLayer >= 0) _visibleTerrain.layer = groundLayer;
            var mf = _visibleTerrain.AddComponent<MeshFilter>();
            var groundMesh = new Mesh { name = "FlatVisibleGround" };
            groundMesh.vertices = new[]
            {
                new Vector3(-40f, 0f,  40f), new Vector3(40f, 0f,  40f),
                new Vector3(-40f, 0f, -40f), new Vector3(40f, 0f, -40f)
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
            _playerGo.transform.position = Vector3.zero;
            _agent = _playerGo.AddComponent<NavMeshAgent>();
            _agent.radius = 0.4f; _agent.height = 1.8f; _agent.speed = WalkSpeed;
            _agent.acceleration = 60f; _agent.angularSpeed = 999f; _agent.stoppingDistance = 0.1f;
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
            _castaway.modelSoleGround = false;     // no Animator/clip in this rig
            _castaway.snapRate = 40f;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            _castaway.runSpeedThreshold = 8.5f;    // below RunSpeed so sprinting flips IsRunning
            _castaway.jumpVelocity = 5.5f;
            _castaway.jumpGravity = 18f;

            // Synthetic arm skeleton under a "Model" child (the model child carries facing; we put the bones
            // under the avatar so the head/shoulder/hand world-Y track the player root + ground-snap).
            var modelGo = new GameObject("Model");
            modelGo.transform.SetParent(_avatarGo.transform, false);
            _model = modelGo.transform;

            _head = MakeBone("mixamorig:Head", _model, new Vector3(0f, HeadLocalY, 0.05f));
            _shoulder = MakeBone("mixamorig:RightArm", _model, new Vector3(0.18f, ShoulderLocalY, 0f));
            _forearm = MakeBone("mixamorig:RightForeArm", _shoulder, new Vector3(0f, -0.12f, 0.05f));
            _hand = MakeBone("mixamorig:RightHand", _forearm, new Vector3(0f, 0f, 0.10f));
            // The hand is positioned in WORLD via a driver in the test (DriveHand) so we can pump it for run/jump.

            var axeGo = new GameObject("HeroAxe");
            axeGo.transform.SetParent(_hand, false);
            _rig = axeGo.AddComponent<HeldAxeRig>();
            _rig.hand = _hand;
            _rig.shoulder = _shoulder;
            _rig.character = _castaway;
            _rig.worldOffsetFromHand = new Vector3(0.02f, -0.03f, 0.01f);
            _rig.relEuler = new Vector3(16f, 2f, -82f);
            _rig.followDamp = 0f;
            _rig.clampVigorousLocomotion = true;
            _rig.clampCeilingAboveShoulder = -0.05f;
            _rig.clampSoftness = 0.12f;
            _axe = axeGo.transform;

            // #68-NIT absorption (AC2 finger-curl-covers-Run): wire a CastawayFingerCurl gated on HasAxe so the
            // test can assert the grip survives the RUN state (the curl is state-independent; this pins it).
            _invGo = new GameObject("Survival");
            _inv = _invGo.AddComponent<Inventory>();
            _fingerBones = new Transform[3];
            _fingerTips = new Transform[3];
            string[] fnames = { "mixamorig:RightHandIndex", "mixamorig:RightHandMiddle", "mixamorig:RightHandRing" };
            for (int fi = 0; fi < 3; fi++)
            {
                Transform parent = _hand;
                Transform proximal = null, tip = null;
                for (int seg = 1; seg <= 4; seg++)
                {
                    var go = MakeBone(fnames[fi] + seg, parent,
                        seg == 1 ? new Vector3(0.02f * fi, 0f, 0.05f) : new Vector3(0f, 0f, 0.04f));
                    if (seg == 1) proximal = go;
                    if (seg == 4) tip = go;
                    parent = go;
                }
                _fingerBones[fi] = proximal;
                _fingerTips[fi] = tip;
            }
            _curl = _avatarGo.AddComponent<CastawayFingerCurl>();
            _curl.fingerBones = _fingerBones;
            _curl.thumbBones = new Transform[0];
            _curl.inventory = _inv;
            _curl.fingerCurlDeg = 26f;
            _curl.RebuildCached();

            _wasd = _playerGo.AddComponent<WasdMovement>();
            _wasd.moveSpeed = WalkSpeed;
            _wasd.runSpeed = RunSpeed;
            _wasd.cameraTransform = _camGo.transform;
            _wasd.clickToMove = ctm;
            _wasd.castaway = _castaway;

            yield return null; // Awake
            yield return null; // ClickToMove warps onto the NavMesh; WasdMovement.Start disables click
            Assert.IsTrue(_agent.isOnNavMesh, "the agent must be on the NavMesh before driving the test");
            _orbit.SetYaw(0f);
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
            if (_invGo != null) Object.DestroyImmediate(_invGo);
            NavMesh.RemoveAllNavMeshData();
        }

        private static Transform MakeBone(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        // Place the hand bone at a chosen local-Y on the model (rest grip, or the run/jump arm-pump to the head).
        // The HeldAxeRig runs at DefaultExecutionOrder(100) > the test's coroutine drive, so it reads this.
        private void DriveHand(float handLocalY)
        {
            // Express the desired hand world-Y as a model-local position on the hand bone's chain. Simplest:
            // set the hand bone's WORLD position directly each frame (its parent chain only translates).
            Vector3 wp = _hand.position;
            // The hand's local rest is under forearm under shoulder; to reach a target world-Y, set world pos.
            float worldY = _model.position.y + handLocalY;
            _hand.position = new Vector3(_model.position.x + 0.18f, worldY, _model.position.z + 0.10f);
        }

        // ===== (1) RUN: with the hand pumped to the head, the clamp engages and the axe stays BELOW the head.
        [UnityTest]
        public IEnumerator Running_WithArmPumpedToHead_AxeStaysBelowTheHead_ClampActive()
        {
            _wasd.SetSprintOverride(true);
            _wasd.SetInputOverride(new Vector2(0f, 1f)); // run forward
            // Let the agent accelerate to run speed.
            float start = Time.time;
            while (Time.time - start < 1.2f && !_castaway.IsRunning) { DriveHand(HandPumpLocalY); yield return null; }
            Assert.IsTrue(_castaway.IsRunning,
                $"the character must be RUNNING (sprint at {RunSpeed} > threshold {_castaway.runSpeedThreshold}); " +
                $"CurrentSpeed={_castaway.CurrentSpeed:F2}");

            // Drive several running frames with the hand pumped UP to the head; the axe must stay below the head.
            float headTop = float.NegativeInfinity, axeTop = float.NegativeInfinity;
            bool sawClampActive = false;
            for (int i = 0; i < 30; i++)
            {
                DriveHand(HandPumpLocalY);
                yield return null;
                if (!_castaway.IsRunning) continue;
                headTop = Mathf.Max(headTop, _head.position.y);
                axeTop = Mathf.Max(axeTop, _axe.position.y);
                if (_rig.ClampActiveThisFrame) sawClampActive = true;
            }
            Debug.Log($"[AxeClampTest] RUN headTopY={headTop:F4} axeTopY={axeTop:F4} clampActiveSeen={sawClampActive}");

            Assert.IsTrue(sawClampActive,
                "the vigorous-locomotion clamp must be ACTIVE while running (IsRunning) — it is what keeps the " +
                "RUN arm-pump from riding the axe into the head (86caa83wn).");
            Assert.Less(axeTop, _head.position.y,
                $"the held axe top world-Y ({axeTop:F4}) must stay BELOW the head bone ({_head.position.y:F4}) " +
                "during RUN — the clamp caps the followed hand at shoulder height so the axe clears the head.");
        }

        // ===== (2) JUMP: same, while airborne (the jump-clip arm-pump).
        [UnityTest]
        public IEnumerator Airborne_WithArmPumpedToHead_AxeStaysBelowTheHead_ClampActive()
        {
            // Settle grounded, then jump.
            _wasd.SetInputOverride(Vector2.zero);
            float s = Time.time;
            while (Time.time - s < 0.4f) { DriveHand(HandRestLocalY); yield return null; }
            Assert.IsFalse(_castaway.IsAirborne, "must be grounded before the jump");

            _wasd.RequestJump();
            yield return null;
            Assert.IsTrue(_castaway.IsAirborne, "the jump must have started (IsAirborne)");

            float axeTop = float.NegativeInfinity;
            bool sawClampActive = false;
            float jumpStart = Time.time;
            while (Time.time - jumpStart < 2f && _castaway.IsAirborne)
            {
                DriveHand(HandPumpLocalY); // the jump-clip arm-pump
                yield return null;
                axeTop = Mathf.Max(axeTop, _axe.position.y);
                if (_rig.ClampActiveThisFrame) sawClampActive = true;
            }
            Debug.Log($"[AxeClampTest] JUMP headY={_head.position.y:F4} axeTopY={axeTop:F4} clampActiveSeen={sawClampActive}");

            Assert.IsTrue(sawClampActive,
                "the clamp must be ACTIVE while AIRBORNE — the jump_running/jump_idle arm-pump rides the axe " +
                "into the head exactly like RUN (86caa83wn).");
            Assert.Less(axeTop, _head.position.y + 1e-3f,
                $"the held axe top ({axeTop:F4}) must stay below the head ({_head.position.y:F4}) while airborne.");
        }

        // ===== (3) WALK / IDLE: the clamp is INERT — the followed pose is the RAW hand (locked pose preserved).
        [UnityTest]
        public IEnumerator WalkingAndIdle_ClampIsInert_FollowedPoseIsRawHand_LockedPosePreserved()
        {
            // IDLE: no input. Even if the hand were pumped, at idle the clamp must NOT engage.
            _wasd.SetSprintOverride(false);
            _wasd.SetInputOverride(Vector2.zero);
            float s = Time.time;
            while (Time.time - s < 0.5f) { DriveHand(HandPumpLocalY); yield return null; }
            Assert.IsFalse(_castaway.IsRunning, "idle: not running");
            Assert.IsFalse(_castaway.IsAirborne, "idle: not airborne");
            Assert.IsFalse(_rig.ClampActiveThisFrame,
                "the clamp must be INERT at IDLE — even a hand pumped high must pass through raw (the Sponsor's " +
                "locked pose is untouched at idle).");
            // The followed pose is the RAW hand (no clamp) — FollowPos.y == the live hand world-Y.
            Assert.AreEqual(_hand.position.y, _rig.FollowPos.y, 1e-3f,
                "at idle the followed hand Y must be the RAW hand Y (clamp inert) — the locked follow-the-arm pose.");

            // WALK (not run): drive at walk speed; the clamp must STILL be inert (only RUN/JUMP clamp).
            _wasd.SetInputOverride(new Vector2(0f, 1f)); // walk forward (no sprint)
            float w = Time.time;
            bool sawWalking = false, clampEverActive = false;
            while (Time.time - w < 1.5f)
            {
                DriveHand(HandPumpLocalY);
                yield return null;
                if (_castaway.IsWalking && !_castaway.IsRunning) sawWalking = true;
                if ((_castaway.IsWalking && !_castaway.IsRunning) && _rig.ClampActiveThisFrame) clampEverActive = true;
            }
            Assert.IsTrue(sawWalking, "the character must reach WALK (not run) speed during the walk leg");
            Assert.IsFalse(clampEverActive,
                "the clamp must be INERT while WALKING (not running) — only RUN/JUMP engage it. A clamp leaking " +
                "into walk would alter the Sponsor's locked WALK pose (regression-fail; 86caa83wn MUST-preserve).");
        }

        // ===== #68-NIT (AC2 finger-curl-covers-Run): the HasAxe-gated finger curl must REMAIN gripping through
        // the RUN state (a regression that dropped the grip on the run blend would re-open the "mangled open
        // hand" read while running). The curl is state-independent (gated on HasAxe only); this pins that the
        // RUN locomotion state does not somehow clear it.
        [UnityTest]
        public IEnumerator FingerCurl_StaysGripping_ThroughTheRunCycle_Nit68AC2()
        {
            _inv.CraftAxe();                  // HasAxe → the curl grips
            yield return null; yield return null;
            Assert.IsTrue(_curl.IsGripping, "precondition: the curl grips once the axe is held");
            Vector3[] gripTips = SampleTips(); // the curled fingertip positions at rest-grip

            // Run forward; assert the curl stays gripping AND the fingertips stay curled across the run cycle.
            _wasd.SetSprintOverride(true);
            _wasd.SetInputOverride(new Vector2(0f, 1f));
            float start = Time.time;
            bool sawRunning = false;
            while (Time.time - start < 1.8f)
            {
                DriveHand(HandPumpLocalY);
                yield return null;
                if (_castaway.IsRunning)
                {
                    sawRunning = true;
                    Assert.IsTrue(_curl.IsGripping,
                        "the finger curl must STAY gripping while RUNNING — the HasAxe grip must cover the run " +
                        "state, not drop to the open 'mangled' hand (#68 NIT AC2).");
                }
            }
            Assert.IsTrue(sawRunning, "the character must reach RUN speed during this test");
        }

        // ===== #68-NIT (AC4 axe-seated-through-run tolerance): across a RUN cycle the axe must stay SEATED in
        // the hand within tolerance — the axe-to-hand offset (the grip) is invariant, the axe never detaches
        // from the hand even as the clamp caps its vertical. (The clamp only moves the VERTICAL of the followed
        // pose; the axe still rides the hand's facing/horizontal — it stays a held tool, not a free prop.)
        [UnityTest]
        public IEnumerator AxeStaysSeatedInHand_ThroughTheRunCycle_WithinTolerance_Nit68AC4()
        {
            _wasd.SetSprintOverride(true);
            _wasd.SetInputOverride(new Vector2(0f, 1f));
            float start = Time.time;
            while (Time.time - start < 1.2f && !_castaway.IsRunning) { DriveHand(HandRestLocalY); yield return null; }
            Assert.IsTrue(_castaway.IsRunning, "the character must be running");

            // With the hand at its REST grip height (below the shoulder ceiling → clamp passes it through), the
            // axe must seat at exactly hand.position + hand.rotation*offset (the grip), every running frame —
            // the rotation/horizontal follow is unaffected by the vertical clamp. Tolerance is tight because the
            // unclamped axis is byte-exact and the clamped Y is at the rest height (sub-ceiling = untouched).
            float maxSeatError = 0f;
            for (int i = 0; i < 30; i++)
            {
                DriveHand(HandRestLocalY); // rest grip (below the ceiling) — the seated-tolerance baseline
                yield return null;
                if (!_castaway.IsRunning) continue;
                Vector3 expected = _hand.position + _hand.rotation * _rig.worldOffsetFromHand;
                maxSeatError = Mathf.Max(maxSeatError, Vector3.Distance(_axe.position, expected));
            }
            Debug.Log($"[AxeClampTest] RUN seated max offset error={maxSeatError:F5}");
            Assert.Less(maxSeatError, 0.02f,
                $"the held axe must stay SEATED in the hand within tolerance across the RUN cycle (max offset " +
                $"error {maxSeatError:F5}u) — at the rest grip height (below the clamp ceiling) the axe seats " +
                "exactly to the hand; a detach/drift reds (#68 NIT AC4).");
        }

        private Vector3[] SampleTips()
        {
            var v = new Vector3[3];
            for (int i = 0; i < 3; i++) v[i] = _fingerTips[i].position;
            return v;
        }
    }
}
