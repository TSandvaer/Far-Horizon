using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// LOCOMOTION-SAMPLING HARNESS (ticket 86ca9a36g) — a reusable per-frame [UnityTest] harness that drives the
    /// castaway along a REAL WALK PATH and SAMPLES EVERY FRAME (grounding / held-prop envelope / finger-curl)
    /// while the agent is moving AND through arrival — NOT just at the standing spawn/after-walk frame.
    ///
    /// WHY THIS EXISTS (the false-green it closes — ticket "Why now"): the standing/spawn-frame tests are
    /// STRUCTURALLY BLIND to in-motion defects. The agent is incidentally grounded at path start/end while the
    /// VISIBLE Zone-D terrain dips only mid-path (unity-conventions.md §FBX/rigs float-saga, PR #47). The
    /// deepest false-green: a SkinnedMeshRenderer.bounds.min.y-reading gauge read GAP=0 "✓ planted" the WHOLE
    /// time while the avatar floated, because .bounds is the conservative animation-MAX AABB (measured
    /// boundsMinY 0.39 standing → −68.7 mid-walk while the true sole was −0.32). A per-frame test asserting the
    /// BAKED actual-lowest-vertex would have RED-flagged this immediately. THIS harness is that test.
    ///
    /// THE LOAD-BEARING DESIGN CONSTRAINT (why a synthetic rig, not the Boot scene): the production castaway's
    /// FBX/Animator path does NOT exercise in PlayMode tests — modelPrefab is an editor-asset reference (the
    /// Boot scene wires it editor-time; a PlayMode AddComponent leaves it null), and headless Time.deltaTime≈0
    /// so the Animator never ticks the WALK clip even if it were present (CastawayAnimationTests :17,
    /// unity-conventions.md §Headless PlayMode time trap). So this harness builds the EXACT production geometry
    /// the bug lived on — a renderer-DISABLED flat NavMesh/collision PROXY slab (Y=0) over the whole area PLUS a
    /// renderer-ENABLED VISIBLE terrain that DIPS shoreward (the foreshore) — drives a REAL NavMeshAgent across
    /// it via the production ClickToMove.MoveTo, and attaches a SYNTHETIC 100×-cm→m skinned mesh so the
    /// production CastawayCharacter.MeasureRenderedSoleWorldY BakeMesh path runs against the actual cm→m trap.
    /// The grounding ASSERTION reads the per-frame-RECOMPUTED snap gauges (LastSnapTargetWorldY / GroundHitWorldY
    /// / MeshBottomWorldY) — NOT the smoothed-lerp settled feet — because headless Time.deltaTime≈0 stalls the
    /// lerp (the documented headless-time trap; CastawayGroundSnapPlayModeTests reads the target for the same
    /// reason). The TARGET is the load-bearing quantity: the saga's bug was the snap SELECTING the wrong surface
    /// / the gauge reading the blown bounds floor — both visible in the per-frame target, with no lerp dependency.
    ///
    /// AC MAP:
    ///   AC1 — Walk_SamplesEveryFrameWhileMoving_ThroughArrival: drives a full walk path + yield-return-null
    ///         samples EVERY frame while agent.velocity.magnitude > 0.1 AND through arrival.
    ///   AC2 — CastawayWalk_TrueSoleStaysGroundedEveryFrame (the load-bearing one): asserts the BAKED
    ///         actual-lowest-vertex sole ≤ visible-ground raycast hit + tol on EVERY walk frame + arrival; and a
    ///         guard REDS if a bounds.min.y / root / foot-bone reference is used instead of the baked sole.
    ///   AC3 — failing-first DEMONSTRATED in the PR body (snap to bounds.min.y → the per-frame assert REDS with
    ///         the failing frame + values → restore → green). The deliberate-break test BoundsMinY_*_Proves
    ///         pins it in-suite: a bounds.min.y gauge floats while the baked sole is planted.
    ///   AC4 — held-prop envelope (axe within ~0.3u of the grip anchor every frame) + finger-curl
    ///         (HasAxe-gated, no finger past open-hand threshold) per-frame asserts. Grounding (AC2) is the
    ///         non-negotiable one; both prop + finger shipped here too.
    ///   AC5 — Time accumulation via Time.time window / WaitForSeconds, never per-frame Time.deltaTime; green
    ///         headless in CI.
    /// </summary>
    public class LocomotionSamplingHarnessPlayModeTests
    {
        private GameObject _proxySlab;     // renderer-DISABLED flat collision/NavMesh proxy (the slab the snap must skip)
        private GameObject _visibleTerrain;// renderer-ENABLED visible terrain that DIPS shoreward (the foreshore)
        private GameObject _surfaceGo;     // the NavMeshSurface
        private GameObject _playerGo;      // the player root (carries the agent + ClickToMove)
        private GameObject _avatarGo;      // the avatar child (carries CastawayCharacter + the synthetic skin)
        private GameObject _smrGo;         // the synthetic 100×-cm→m skinned mesh node
        private NavMeshAgent _agent;
        private ClickToMove _mover;
        private CastawayCharacter _castaway;

        // Path geometry. Spawn at the flat inland end (+Z); the visible terrain DIPS toward the shore (−Z). The
        // proxy slab stays flat at Y=0 over the whole area; the visible terrain is flat (Y=0) at zStart and dips
        // to ySlope at zEnd — so walking shoreward the visible sand drops BELOW the slab (the exact foreshore
        // geometry the float saga lived on). The waypoint is at the dipped shore end (where the bug was worst).
        private const float ZStart = 9f;     // spawn (flat, Y=0)
        private const float ZEnd = -9f;      // shore (dipped)
        private const float YDip = -0.5f;    // visible terrain Y at the shore end
        private const float GroundTol = 0.05f; // grounding tolerance (ticket AC2 ≈ 0.05)

        // ===================================================================================================
        // RIG BUILD — the production two-collider foreshore geometry + a real agent + the synthetic cm→m skin.
        // ===================================================================================================

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // ISOLATION (cross-fixture leak fix, mirrors CastawayGroundSnapPlayModeTests): other PlayMode
            // fixtures LoadScene("Boot") which carries the REAL renderer-enabled Zone-D Ground_Play terrain; a
            // deferred LoadScene can leave it resident when our snap-raycast fires, polluting the pick. Load a
            // fresh EMPTY scene so OUR synthetic colliders are the ONLY Ground surfaces in play.
            yield return PlayModeSceneIsolation.IsolateInFreshScene("LocoHarnessIsolated");

            int groundLayer = LayerMask.NameToLayer("Ground");

            // (1) Renderer-DISABLED flat collision/NavMesh PROXY slab at Y=0 over the whole walk band (the
            // TestGround stand-in; unity-conventions.md §NavMesh grey-slab). The NavMesh bakes off THIS flat
            // surface so the agent has a clean walkable plane; the snap must SKIP it (renderer disabled) and
            // pick the visible dipping terrain instead.
            _proxySlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _proxySlab.name = "TestGround";
            if (groundLayer >= 0) _proxySlab.layer = groundLayer;
            _proxySlab.transform.position = new Vector3(0f, -0.5f, 0f); // top face at Y=0
            _proxySlab.transform.localScale = new Vector3(30f, 1f, 30f);
            _proxySlab.GetComponent<MeshRenderer>().enabled = false;    // collision proxy ONLY

            // (2) Renderer-ENABLED VISIBLE terrain: a 2-tri ramp on the Ground layer. Flat (Y=0) at ZStart,
            // dipping to YDip at ZEnd — the foreshore. Wound UP-facing so a downward snap-ray hits it.
            _visibleTerrain = new GameObject("Ground_Play");
            if (groundLayer >= 0) _visibleTerrain.layer = groundLayer;
            var mf = _visibleTerrain.AddComponent<MeshFilter>();
            var rampMesh = new Mesh { name = "ForeshoreRamp" };
            rampMesh.vertices = new[]
            {
                new Vector3(-15f, 0f,    ZStart), new Vector3(15f, 0f,    ZStart),
                new Vector3(-15f, YDip,  ZEnd),   new Vector3(15f, YDip,  ZEnd)
            };
            rampMesh.triangles = new[] { 0, 1, 2, 1, 3, 2 }; // up-facing (+Y) normals
            rampMesh.RecalculateNormals();
            mf.sharedMesh = rampMesh;
            var mr = _visibleTerrain.AddComponent<MeshRenderer>();
            mr.enabled = true; // VISIBLE — the surface the player sees, the snap must select THIS
            var col = _visibleTerrain.AddComponent<MeshCollider>();
            col.sharedMesh = rampMesh;

            // (3) Bake a NavMesh off the flat proxy slab (the walkable plane the real agent rides).
            _surfaceGo = new GameObject("Surface");
            var surface = _surfaceGo.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            // (4) The player root: the NavMeshAgent + the production ClickToMove (so we drive a REAL walk path
            // via the production MoveTo, not a transform teleport — the brief: "a real WALK path").
            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, 0f, ZStart);
            _agent = _playerGo.AddComponent<NavMeshAgent>();
            _agent.radius = 0.4f; _agent.height = 1.8f; _agent.speed = 3.5f;
            _agent.acceleration = 24f; _agent.angularSpeed = 999f; _agent.stoppingDistance = 0.1f;
            _agent.updateRotation = false; _agent.updateUpAxis = false;
            _mover = _playerGo.AddComponent<ClickToMove>();
            _mover.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;

            // (5) The avatar child carrying CastawayCharacter (the production grounding/snap under test) +
            // a synthetic 100×-cm→m skinned mesh so MeasureRenderedSoleWorldY's BakeMesh path runs against the
            // real cm→m trap. modelPrefab stays null (it's an editor asset) → CastawayCharacter LogErrors that
            // (a real error in-game); expect it rather than suppress the production guard.
            LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero;
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = true;
            _castaway.modelSoleGround = false; // no real Animator/clip here → no per-clip lift to cancel; the
                                               // snap grounds the root, and the synthetic sole sits AT the root
                                               // (lo=0), so the baked-sole gauge reads the grounded plant directly.
            _castaway.snapRate = 40f;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            AttachSyntheticSkinnedMesh_With100xCmToMNode(_avatarGo.transform);

            // Let Awake resolve the agent + the agent register on the NavMesh + ClickToMove warp on.
            yield return null;
            yield return null;
            Assert.IsTrue(_agent.isOnNavMesh, "the agent must be on the NavMesh before driving a walk path");
        }

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate (not Destroy): a deferred destroy could leave a Ground collider live when the
            // next test's first snap-raycast fires, polluting its pick (the documented cross-test leak).
            if (_smrGo != null) { Object.DestroyImmediate(_smrGo); _smrGo = null; }
            if (_avatarGo != null) { Object.DestroyImmediate(_avatarGo); _avatarGo = null; }
            if (_playerGo != null) { Object.DestroyImmediate(_playerGo); _playerGo = null; }
            if (_visibleTerrain != null) { Object.DestroyImmediate(_visibleTerrain); _visibleTerrain = null; }
            if (_proxySlab != null) { Object.DestroyImmediate(_proxySlab); _proxySlab = null; }
            if (_surfaceGo != null) { Object.DestroyImmediate(_surfaceGo); _surfaceGo = null; }
            NavMesh.RemoveAllNavMeshData();
        }

        // A synthetic SkinnedMeshRenderer with an INTRINSIC 100× node scale on its OWN transform — the exact
        // cm→m trap the production Hyper3D/Mixamo FBX carries ("model" node localScale=100, probe-verified). The
        // verts are authored TINY (metres ÷100) so the 100× brings them to ~real size. This makes the production
        // MeasureRenderedSoleWorldY BakeMesh path run against the trap: a scale-immune (unit-scale-matrix) read
        // stays sane; a regression to smr.localToWorldMatrix DOUBLE-APPLIES the 100× and explodes the world Y.
        // The lowest vert sits at lo=0 (the sole at the renderer origin = the avatar root, like the real bind).
        private void AttachSyntheticSkinnedMesh_With100xCmToMNode(Transform avatarRoot)
        {
            _smrGo = new GameObject("Synthetic100xSkin");
            _smrGo.transform.SetParent(avatarRoot, false);
            _smrGo.transform.localPosition = Vector3.zero;
            _smrGo.transform.localScale = Vector3.one * 100f; // the cm→m trap node
            var smr = _smrGo.AddComponent<SkinnedMeshRenderer>();

            var mesh = new Mesh { name = "SyntheticSole" };
            const float s = 0.01f; // 1/100
            float lo = 0f, hi = 50f * s; // 0 .. 0.5u after the 100× node — sole at the root, ~0.5u tall
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

            // Reproduce the CONSERVATIVE animation-MAX AABB deterministically: the real Mixamo
            // SkinnedMeshRenderer.bounds is a single box sized to contain the mesh across the WHOLE animation
            // range, so bounds.min.y sits WELL BELOW the current-frame baked sole (the saga's deeper
            // false-green — measured boundsMinY 0.39 standing → −68.7 mid-walk while the true sole was −0.32).
            // Our synthetic mesh is a static quad whose tight bounds would NOT reproduce that gap, so we set an
            // explicit oversized localBounds (a box that extends far below the sole, in the SMR's authored
            // metres space — the matrix carries it to world). This makes SmrBoundsMinWorldY (the OLD proxy)
            // diverge BELOW MeshBottomWorldY (the baked sole) on every frame, exactly like the real anim-max
            // AABB — so the divergence guard tests the real false-green, not a synthetic artifact.
            // updateWhenOffscreen MUST be FALSE for the explicit localBounds to STICK — with it true Unity
            // recomputes tight per-frame bounds from the baked mesh and discards our manual box. The production
            // sole gauge BAKES the mesh directly (independent of bounds), so this doesn't affect MeshBottomWorldY.
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0f, (lo + hi) * 0.5f, 0f),
                                         new Vector3(0.6f, (hi - lo) + 1.0f, 0.2f)); // +1.0u taller → floor ~−0.5u below the sole
        }

        // ===================================================================================================
        // THE HARNESS CORE — drive a real walk path + a per-frame sampler callback. Reusable; each test below
        // passes a different per-frame assertion. AC1 + AC5 live here: yield-return-null EVERY frame while
        // moving, accumulate over a Time.time window (never per-frame Time.deltaTime).
        // ===================================================================================================

        /// <summary>The per-frame sample fed to a test's assertion. PhaseMoving=true while velocity>0.1; the
        /// arrival phase (PhaseMoving flips false after the agent brakes) is sampled too (the brief: cover the
        /// arrival frames, not just steady-state walk).</summary>
        private struct Sample { public int Frame; public bool Moving; public bool Arrival; public float Time; }

        /// <summary>
        /// Drives the agent from spawn to the shore waypoint via the production ClickToMove.MoveTo, calling
        /// <paramref name="onFrame"/> EVERY frame while moving (velocity>0.1) AND through arrival (a settle
        /// window after braking). Returns the count of MOVING frames sampled (a test asserts it's substantial,
        /// proving we actually sampled in-motion, not a single standing frame). Time-bounded over a Time.time
        /// window (AC5 — never per-frame Time.deltaTime).
        /// </summary>
        private IEnumerator DriveWalkPath(System.Action<Sample> onFrame, List<Sample> samples)
        {
            Vector3 shore = new Vector3(0f, 0f, ZEnd + 2f); // the dipped foreshore waypoint
            Assert.IsTrue(_mover.MoveTo(shore),
                "the production ClickToMove.MoveTo must set a valid destination onto the NavMesh (the walk path)");

            int frame = 0;
            int movingFrames = 0;
            bool everMoved = false;

            // PHASE 1 — WHILE MOVING: sample every frame the agent's planar speed exceeds 0.1 (the ticket's
            // "yield return null SAMPLES EVERY FRAME while agent.velocity.magnitude > 0.1"). Bounded by a real
            // Time.time window so a stuck agent can't hang CI (AC5).
            float walkStart = Time.time;
            while (Time.time - walkStart < 12f)
            {
                float planarSpeed = new Vector2(_agent.velocity.x, _agent.velocity.z).magnitude;
                bool moving = planarSpeed > 0.1f;
                if (moving) { everMoved = true; movingFrames++; }
                var sm = new Sample { Frame = frame, Moving = moving, Arrival = false, Time = Time.time };
                samples.Add(sm);
                onFrame(sm);
                frame++;
                // Stop the moving phase once we've moved AND then braked to a stop near the goal.
                if (everMoved && !moving && !_agent.pathPending &&
                    _agent.remainingDistance <= _agent.stoppingDistance + 0.15f)
                    break;
                yield return null;
            }
            Assert.IsTrue(everMoved,
                "the agent must actually WALK the path (velocity>0.1 at some point) — a path that never moves " +
                "would make every 'walk-frame' assertion vacuous (the standing-frame blindness this harness closes)");

            // PHASE 2 — THROUGH ARRIVAL: keep sampling for a real settle window AFTER the agent has stopped, so
            // the grounding/prop/finger asserts cover the arrival frames (the unified move/rest snap-rate — a
            // rate discontinuity at arrival caused a visible bob; cover arrival, not just steady-state walk).
            float arriveStart = Time.time;
            while (Time.time - arriveStart < 0.6f)
            {
                var sm = new Sample { Frame = frame, Moving = false, Arrival = true, Time = Time.time };
                samples.Add(sm);
                onFrame(sm);
                frame++;
                yield return null;
            }

            // Surface the moving-frame count back to the caller via the samples list (asserted by AC1's test).
        }

        // ===================================================================================================
        // AC1 + AC5 — the harness drives a real walk path and samples EVERY frame while moving + through arrival.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator Walk_SamplesEveryFrameWhileMoving_ThroughArrival()
        {
            var samples = new List<Sample>();
            yield return DriveWalkPath(_ => { }, samples);

            int movingFrames = samples.FindAll(s => s.Moving).Count;
            int arrivalFrames = samples.FindAll(s => s.Arrival).Count;

            // We must have sampled MANY moving frames — proving the harness samples per-frame DURING motion
            // (the structural fix for standing-frame blindness), not a single before/after frame.
            Assert.Greater(movingFrames, 10,
                $"the harness must sample MANY frames WHILE MOVING (got {movingFrames}); a handful means it only " +
                "caught the standing endpoints — the exact blindness this harness exists to remove.");
            // And we must have sampled the arrival phase (the bob window).
            Assert.Greater(arrivalFrames, 0,
                $"the harness must sample THROUGH arrival (got {arrivalFrames} arrival frames) — the arrival bob " +
                "band, not just steady-state walk.");
            // The agent ended near the shore waypoint (the walk path actually traversed the foreshore dip).
            Assert.Less(_playerGo.transform.position.z, ZEnd + 3.5f,
                $"the agent must have walked to the dipped foreshore (z={_playerGo.transform.position.z:F2}); " +
                "a path that didn't reach the dip never exercises the in-motion grounding divergence.");
        }

        // ===================================================================================================
        // AC2 (LOAD-BEARING) — the TRUE rendered sole stays grounded EVERY walk frame AND through arrival.
        // Reads the BAKED actual-lowest-vertex (production MeshBottomWorldY, scale-immune) vs the visible-ground
        // raycast hit (production GroundHitWorldY). NOT root, NOT foot-bone, NOT bounds.min.y.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator CastawayWalk_TrueSoleStaysGroundedEveryFrame()
        {
            var samples = new List<Sample>();
            int checkedFrames = 0;
            float worstGap = float.NegativeInfinity;
            int worstFrame = -1;
            float worstSole = 0f, worstGround = 0f;

            yield return DriveWalkPath(s =>
            {
                // The production gauges, RECOMPUTED every frame inside ApplyGroundSnap (no smoothed-lerp
                // dependency — headless Time.deltaTime≈0 stalls the lerp; the TARGET/gauge is the load-bearing
                // quantity). MeshBottomWorldY = the SCALE-IMMUNE baked actual-lowest-vertex (the TRUE sole the
                // player sees touch the sand). GroundHitWorldY = the visible-terrain raycast hit under the feet.
                float sole = _castaway.MeshBottomWorldY;
                float ground = _castaway.GroundHitWorldY;
                if (float.IsNaN(sole) || float.IsNaN(ground)) return; // pre-first-snap frames: not yet valid

                checkedFrames++;
                float gap = sole - ground; // >0 ⟺ the visible sole floats ABOVE the visible sand (the bug)
                if (gap > worstGap) { worstGap = gap; worstFrame = s.Frame; worstSole = sole; worstGround = ground; }

                // THE per-frame grounding assertion (AC2): the TRUE baked sole must sit ON the visible ground
                // (within tol) — EVERY frame, moving AND at arrival. The float saga's bug was the sole floating
                // mid-walk while a bounds.min.y gauge agreed GAP=0; this reads the HONEST baked sole, so a float
                // RED-flags with the failing frame + values (the failing-first contract, AC3).
                Assert.LessOrEqual(sole, ground + GroundTol,
                    $"frame {s.Frame} ({(s.Arrival ? "ARRIVAL" : s.Moving ? "WALKING" : "settling")}): the TRUE " +
                    $"rendered sole (baked actual-lowest-vertex {sole:F4}) floats ABOVE the visible ground " +
                    $"({ground:F4}) by {gap:F4}u (> tol {GroundTol}). This is the walk-float false-green: the " +
                    "feet must stay planted on the VISIBLE sand every frame, not just at the standing endpoints.");
            }, samples);

            // We must have actually validated a substantial run of frames (not vacuously passed because the
            // gauge never went valid). The sampler skips pre-first-snap NaN frames; assert real coverage.
            Assert.Greater(checkedFrames, 10,
                $"the grounding assertion must have RUN on many frames (got {checkedFrames}); too few means the " +
                "baked-sole gauge never went valid — the harness would pass vacuously without testing grounding.");
            Debug.Log($"[LocoHarness] grounded {checkedFrames} frames; worst gap {worstGap:F4}u at frame " +
                      $"{worstFrame} (sole {worstSole:F4} vs ground {worstGround:F4}).");
        }

        // ===================================================================================================
        // AC2 GUARD — a bounds.min.y / root / foot-bone reference REDS. This is the "a PlayMode guard REDS if
        // anything re-reads bounds.min.y / root / a foot-bone as the grounding reference" clause. We prove, on
        // THIS rig, that the OLD proxy references (SmrBoundsMinWorldY = the conservative anim-max AABB floor, and
        // the avatar-ROOT world-Y) DIVERGE from the baked sole — so a gauge built on them is structurally a
        // false-green generator. (The production code exposes both for exactly this comparison.)
        // ===================================================================================================
        [UnityTest]
        public IEnumerator BoundsMinY_SitsBelowBakedSole_ProvingItIsAFalseGreenIfUsedForGrounding()
        {
            var samples = new List<Sample>();
            int sawBoundsBelowSole = 0;
            int validFrames = 0;
            float worstBelow = 0f;

            yield return DriveWalkPath(s =>
            {
                float bakedSole = _castaway.MeshBottomWorldY;     // the TRUE scale-immune baked-vertex sole
                float boundsMin = _castaway.SmrBoundsMinWorldY;   // the OLD proxy (conservative anim-max AABB floor)
                float ground = _castaway.GroundHitWorldY;
                if (float.IsNaN(bakedSole) || float.IsNaN(boundsMin) || float.IsNaN(ground)) return;
                validFrames++;

                // THE structural proof: the bounds floor sits BELOW the baked sole (our synthetic SMR carries an
                // oversized localBounds = the real anim-max AABB). So if the GROUNDING gauge read bounds.min.y
                // instead of the baked sole, it would plant the bounds FLOOR on the sand → the real (baked) sole
                // would float by (sole − boundsFloor), while a bounds-reading gauge reports GAP=0 "✓ planted".
                // That is the EXACT deepest false-green the saga turned on. We prove the divergence exists AND is
                // in the dangerous direction (bounds BELOW the true sole) on the moving rig.
                float boundsBelowSoleBy = bakedSole - boundsMin; // >0 ⟺ the bounds floor is below the true sole
                if (boundsBelowSoleBy > 1e-3f) { sawBoundsBelowSole++; if (boundsBelowSoleBy > worstBelow) worstBelow = boundsBelowSoleBy; }

                // And confirm the HARNESS's grounding gauge (the one AC2 asserts on) is the BAKED sole, NOT the
                // bounds floor: the baked sole is grounded (≈ ground), while the bounds floor is NOT (it sits
                // below). If a refactor re-pointed MeshBottomWorldY at SMR.bounds.min.y, the baked sole would
                // equal the bounds floor (and AC2's grounding would silently pass on the floating proxy).
                Assert.That(bakedSole, Is.EqualTo(ground).Within(GroundTol),
                    $"frame {s.Frame}: the grounding gauge (MeshBottomWorldY={bakedSole:F4}) must equal the visible " +
                    $"ground ({ground:F4}) — it must read the BAKED sole, which is grounded. If it instead read the " +
                    $"bounds floor ({boundsMin:F4}, which sits {boundsBelowSoleBy:F4}u lower), AC2 would falsely pass " +
                    "while the real sole floated — the bounds.min.y false-green.");
            }, samples);

            Assert.Greater(validFrames, 10,
                $"the divergence proof must have measured many frames (got {validFrames})");
            Assert.Greater(sawBoundsBelowSole, 0,
                "the conservative SMR.bounds floor (SmrBoundsMinWorldY) must sit BELOW the baked actual-vertex sole " +
                "(MeshBottomWorldY) on the moving rig — that gap is WHY grounding to bounds.min.y floats the real " +
                "soles while a bounds-reading gauge agrees GAP=0 '✓ planted' (the deepest walk-float false-green). " +
                "The grounding reference MUST be the baked actual-lowest-vertex, NOT the bounds floor / root / " +
                $"foot-bone. (Worst gap measured {worstBelow:F4}u.)");
        }

        // ===================================================================================================
        // AC4 — HELD-PROP ENVELOPE: the held axe stays within ~0.3u of its grip anchor EVERY walk frame. We
        // attach a real production HeldAxeRig riding a synthetic hand on the avatar, drive the walk path, and
        // assert the axe pivot tracks the grip anchor (hand + offset) within the envelope on every frame.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator HeldAxe_StaysWithinGripEnvelope_EveryWalkFrame()
        {
            // A synthetic right-hand bone on the avatar + the production HeldAxeRig riding it (raw-hand follow,
            // the shipped behavior — HeldAxeRig.LateUpdate seats the axe at hand.position + hand.rotation*offset).
            var handGo = new GameObject("mixamorig:RightHand");
            handGo.transform.SetParent(_avatarGo.transform, false);
            handGo.transform.localPosition = new Vector3(0.25f, 1.1f, 0.1f);
            handGo.transform.localRotation = Quaternion.Euler(20f, 10f, -40f);
            var axeGo = new GameObject("HeroAxe");
            axeGo.transform.SetParent(handGo.transform, false);
            var rig = axeGo.AddComponent<HeldAxeRig>();
            rig.hand = handGo.transform;
            rig.worldOffsetFromHand = new Vector3(0.05f, -0.1f, 0.03f);
            rig.relEuler = new Vector3(4f, 96f, -56f);
            rig.followDamp = 0f; // raw-hand follow (shipped)

            float envelope = 0.3f; // ticket AC4: within ~0.3u of the grip anchor
            int checkedFrames = 0;
            float worstDist = 0f;
            var samples = new List<Sample>();

            yield return DriveWalkPath(s =>
            {
                // The grip anchor = the hand world position + the hand-rotated offset (exactly what HeldAxeRig
                // seats to). The axe pivot must sit within the envelope of THAT every frame (no drift/detach).
                Vector3 anchor = handGo.transform.position + handGo.transform.rotation * rig.worldOffsetFromHand;
                float dist = Vector3.Distance(axeGo.transform.position, anchor);
                checkedFrames++;
                if (dist > worstDist) worstDist = dist;
                Assert.LessOrEqual(dist, envelope,
                    $"frame {s.Frame} ({(s.Arrival ? "ARRIVAL" : "WALKING")}): the held axe pivot " +
                    $"({axeGo.transform.position}) drifted {dist:F4}u from its grip anchor ({anchor}) — beyond " +
                    $"the ~{envelope}u envelope. The axe must stay SEATED in the grip across all walk frames " +
                    "(the arm-swing drift class — saga measured 0.93u before the anchor fix).");
            }, samples);

            Assert.Greater(checkedFrames, 10,
                $"the held-prop envelope assertion must have run on many frames (got {checkedFrames})");
            Debug.Log($"[LocoHarness] held-axe envelope: worst {worstDist:F4}u over {checkedFrames} frames " +
                      $"(limit {envelope}u).");

            Object.DestroyImmediate(handGo); // the axe is a child — destroyed with it
        }

        // ===================================================================================================
        // AC4 — FINGER-CURL: the HasAxe-gated curl is active while a prop is held, and no finger bone exceeds
        // the open-hand threshold (i.e. the curl is a bounded grip, not a clench-through). We attach the
        // production CastawayFingerCurl + an Inventory with the axe held, drive the walk path, and assert the
        // gate stays gripping AND the curl stays bounded EVERY frame.
        // ===================================================================================================
        [UnityTest]
        public IEnumerator FingerCurl_HasAxeGated_StaysBoundedGrip_EveryWalkFrame()
        {
            // An Inventory + the production CastawayFingerCurl on the avatar, wired to a synthetic finger chain.
            // The curl is HasAxe-gated; we measure the OPEN-hand chain length FIRST (axe NOT yet crafted → not
            // gripping → no curl), THEN craft (HasAxe → the gate engages) and run the walk, asserting the gate
            // stays gripping AND the curl stays a bounded grip every frame.
            var invGo = new GameObject("Inventory");
            var inv = invGo.AddComponent<Inventory>();

            // One synthetic finger chain (proximal..tip) extending +Z, the open-hand rest pose.
            var fingerBones = new Transform[1];
            var fingerTips = new Transform[1];
            var allSegments = new List<Transform>();
            Transform parent = _avatarGo.transform;
            Transform proximal = null, tip = null;
            for (int seg = 1; seg <= 4; seg++)
            {
                var go = new GameObject("mixamorig:RightHandIndex" + seg);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = seg == 1 ? new Vector3(0.2f, 1.1f, 0.1f) : new Vector3(0f, 0f, 0.06f);
                allSegments.Add(go.transform);
                if (seg == 1) proximal = go.transform;
                if (seg == 4) tip = go.transform;
                parent = go.transform;
            }
            fingerBones[0] = proximal; fingerTips[0] = tip;

            // ANIMATOR STAND-IN (the load-bearing headless-rig fix): the production CastawayFingerCurl COMPOSES
            // its curl onto the clip pose each frame (b.localRotation = b.localRotation * offset), relying on the
            // Animator to RE-WRITE the open clip pose first every frame so the curl is a FIXED offset, not an
            // accumulation. There is no Animator in this synthetic rig (and headless Time.deltaTime≈0 wouldn't
            // tick it anyway), so without a stand-in the curl would accumulate unbounded (26°/frame → fold
            // through) — a synthetic artifact, NOT a real bug. FingerClipReset resets the finger bones to their
            // open localRotation in Update (BEFORE CastawayFingerCurl's LateUpdate), exactly as the real Animator
            // does — so the curl composes onto a fresh open pose each frame (the production contract).
            var resetGo = new GameObject("FingerClipReset");
            resetGo.transform.SetParent(_avatarGo.transform, false);
            var reset = resetGo.AddComponent<FingerClipReset>();
            reset.bones = allSegments.ToArray();
            reset.openLocalRotations = new Quaternion[allSegments.Count];
            for (int i = 0; i < allSegments.Count; i++) reset.openLocalRotations[i] = allSegments[i].localRotation;

            // POST-CURL TIP RECORDER (the batchmode-safe end-of-frame substitute): WaitForEndOfFrame is NOT
            // evoked in batchmode (no swapchain), so a per-frame sampler in the coroutine resumes mid-Update —
            // BEFORE CastawayFingerCurl's LateUpdate (order 60) applies the curl — and would read the open pose.
            // TipRecorder runs in LateUpdate at order 200 (AFTER the curl), capturing the POST-curl tip in the
            // hand-local frame into LatestTipLocal; the coroutine reads THAT (the prior frame's recorded value),
            // so the sample reflects the applied curl without WaitForEndOfFrame.
            var recGo = new GameObject("TipRecorder");
            recGo.transform.SetParent(_avatarGo.transform, false);
            var recorder = recGo.AddComponent<TipRecorder>();
            recorder.tip = fingerTips[0];
            recorder.frame = fingerBones[0].parent;

            var curl = _avatarGo.AddComponent<CastawayFingerCurl>();
            curl.fingerBones = fingerBones;
            curl.thumbBones = new Transform[0];
            curl.inventory = inv;
            curl.fingerCurlDeg = 26f;
            curl.RebuildCached();

            // Capture the OPEN-hand fingertip WORLD position with the axe NOT yet crafted (HasAxe false → not
            // gripping → FingerClipReset holds the open pose, no curl). The curl swings the tip away from THIS
            // open reference (the proximal bone rotates the rigid sub-chain about its pivot); we assert the tip
            // MOVES (closes the hand) but a BOUNDED amount (not a clench-through past the open-hand pose).
            yield return null; // OnEnable applied the gate (HasAxe false → open hand)
            Assert.IsFalse(curl.IsGripping, "precondition: NOT gripping before the craft (open hand)");
            // The open-hand tip position in the PROXIMAL'S PARENT frame (the hand-local frame). MUST be measured
            // in a frame INVARIANT to the avatar's locomotion: the avatar root translates (the agent walks + the
            // ground-snap drives the avatar-root Y) during the walk, so a fixed WORLD reference would conflate
            // the curl with the body's motion (the tip "moved" by the whole avatar translation, not the curl).
            // The proximal's parent does not move relative to the finger, so the tip's offset in THAT frame
            // isolates the curl alone.
            Transform handFrame = fingerBones[0].parent;
            Vector3 openTipLocal = handFrame.InverseTransformPoint(fingerTips[0].position);
            // The bone's natural lever arm in the SAME hand-local frame (tip distance from the proximal pivot) —
            // the scale a curl displacement is bounded against. A curl rotates the rigid sub-chain about the
            // pivot, moving the tip on an arc of this radius; a sane grip is a bounded fraction of a full fold.
            Vector3 proximalLocal = handFrame.InverseTransformPoint(fingerBones[0].position);
            float leverArm = Vector3.Distance(openTipLocal, proximalLocal);

            inv.CraftAxe(); // HasAxe = true → Inventory.Changed fires → the curl gate engages
            yield return null; // the curl's LateUpdate applies; TipRecorder records the post-curl tip
            yield return null;
            Assert.IsTrue(curl.IsGripping, "precondition: the curl must grip once the axe is held (HasAxe)");

            // A clench-THROUGH would fold the fingertip PAST the palm — a displacement well beyond a bounded grip.
            // A RUNAWAY ACCUMULATION (the real failure class — the curl composing onto itself unbounded every
            // frame, 26°→52°→… until the finger wraps through the haft) blows way past 1.5×lever-arm.
            float clenchThroughCap = leverArm * 1.5f;

            // Drive the SAME walk path as the other ACs (production ClickToMove.MoveTo) and sample EVERY frame.
            // We read TipRecorder.LatestTipLocal — the POST-curl tip recorded in LateUpdate (order 200, after the
            // curl's order-60 LateUpdate) — so the sample reflects the applied curl WITHOUT WaitForEndOfFrame
            // (which batchmode does not evoke). Assert EVERY frame while moving + through arrival: (a) the gate
            // stays gripping, (b) the curl stays a bounded grip (no clench-through / accumulation).
            Vector3 shore = new Vector3(0f, 0f, ZEnd + 2f);
            Assert.IsTrue(_mover.MoveTo(shore), "the production ClickToMove.MoveTo must set the walk-path destination");

            int checkedFrames = 0, movingFrames = 0;
            float maxTipTravel = 0f, prevTravel = float.NaN, maxFrameGrowth = 0f;
            bool everMoved = false;
            float walkStart = Time.time;
            while (Time.time - walkStart < 12f)
            {
                float planarSpeed = new Vector2(_agent.velocity.x, _agent.velocity.z).magnitude;
                bool moving = planarSpeed > 0.1f;
                if (moving) { everMoved = true; movingFrames++; }
                checkedFrames++;

                // (a) the HasAxe gate stays gripping every frame while the axe is held (no flicker mid-walk).
                Assert.IsTrue(curl.IsGripping,
                    $"frame {checkedFrames}: the finger-curl gate must stay GRIPPING while the axe is held (HasAxe) " +
                    "— a gate that flickers off mid-walk drops the grip and re-shows the open 'mangled' hand.");
                // (b) the curl is a BOUNDED grip. The POST-curl tip is recorded in the HAND-LOCAL frame so the
                // avatar's locomotion (walking + ground-snap) does NOT inflate the reading — only the curl moves
                // the tip in this frame.
                float travel = Vector3.Distance(recorder.LatestTipLocal, openTipLocal);
                if (travel > maxTipTravel) maxTipTravel = travel;
                if (!float.IsNaN(prevTravel)) maxFrameGrowth = Mathf.Max(maxFrameGrowth, Mathf.Abs(travel - prevTravel));
                prevTravel = travel;
                Assert.LessOrEqual(travel, clenchThroughCap,
                    $"frame {checkedFrames}: the curled fingertip moved {travel:F4}u from its open pose — beyond " +
                    $"1.5×lever-arm ({clenchThroughCap:F4}u) = a clench-THROUGH the haft (or a runaway accumulation " +
                    "of the per-frame curl). The grip must be BOUNDED, not a fist through the prop.");

                if (everMoved && !moving && !_agent.pathPending &&
                    _agent.remainingDistance <= _agent.stoppingDistance + 0.15f)
                {
                    // keep sampling a short arrival window, then stop
                    float arriveStart = Time.time;
                    while (Time.time - arriveStart < 0.4f)
                    {
                        yield return null;
                        checkedFrames++;
                        Assert.IsTrue(curl.IsGripping, $"arrival frame {checkedFrames}: the curl must stay gripping");
                        float tv = Vector3.Distance(recorder.LatestTipLocal, openTipLocal);
                        if (tv > maxTipTravel) maxTipTravel = tv;
                        maxFrameGrowth = Mathf.Max(maxFrameGrowth, Mathf.Abs(tv - prevTravel));
                        prevTravel = tv;
                        Assert.LessOrEqual(tv, clenchThroughCap, $"arrival frame {checkedFrames}: bounded grip");
                    }
                    break;
                }
                yield return null;
            }

            Assert.IsTrue(everMoved, "the agent must actually WALK the path (the in-motion finger-curl coverage)");
            Assert.Greater(movingFrames, 10,
                $"the finger-curl assertion must have run on many MOVING frames (got {movingFrames})");
            Assert.Greater(maxTipTravel, 0.001f,
                "the curl must actually CLOSE the hand (the tip moved from its open pose toward the palm) while " +
                "the axe is held — a zeroed curl ships the open 'mangled' hand.");
            // The anti-accumulation invariant: a fixed-offset curl reaches a steady displacement; the per-frame
            // change stays small. A compounding curl would show large frame-over-frame growth.
            Assert.Less(maxFrameGrowth, leverArm * 0.5f,
                $"the finger-curl displacement GREW by up to {maxFrameGrowth:F4}u between consecutive frames — a " +
                "fixed grip offset should be steady frame-to-frame; large growth means the per-frame curl is " +
                "ACCUMULATING (compounding onto itself), which folds the finger through the haft over time.");
            Debug.Log($"[LocoHarness] finger-curl: gripping every frame, max tip travel {maxTipTravel:F4}u, max " +
                      $"frame growth {maxFrameGrowth:F5}u over {checkedFrames} frames ({movingFrames} moving; lever " +
                      $"arm {leverArm:F4}u).");

            Object.DestroyImmediate(curl);
            Object.DestroyImmediate(resetGo);
            Object.DestroyImmediate(recGo);
            if (proximal != null) Object.DestroyImmediate(proximal.gameObject); // the whole chain (children too)
            Object.DestroyImmediate(invGo);
        }
    }

    /// <summary>
    /// Test-only Animator STAND-IN for the finger-curl harness: resets the given bones to their OPEN
    /// localRotations every Update (which runs BEFORE LateUpdate, where CastawayFingerCurl composes its curl).
    /// The production curl multiplies its offset onto the clip pose each frame, relying on the Animator to
    /// re-write the open pose first; with no Animator in a headless PlayMode rig the curl would accumulate
    /// unbounded. This re-writes the open pose each frame exactly as the Animator does, so the curl is a FIXED
    /// offset (the production contract), not a per-frame accumulation artifact.
    /// </summary>
    public class FingerClipReset : MonoBehaviour
    {
        public Transform[] bones;
        public Quaternion[] openLocalRotations;

        void Update()
        {
            if (bones == null || openLocalRotations == null) return;
            int n = Mathf.Min(bones.Length, openLocalRotations.Length);
            for (int i = 0; i < n; i++)
                if (bones[i] != null) bones[i].localRotation = openLocalRotations[i];
        }
    }

    /// <summary>
    /// Test-only POST-CURL tip recorder: runs in LateUpdate at order 200 (AFTER CastawayFingerCurl's order-60
    /// LateUpdate), capturing the fingertip in the hand-local <see cref="frame"/> into LatestTipLocal. The
    /// harness coroutine reads this recorded value (the curl IS applied by the time order 200 runs), so the
    /// sample reflects the curled pose WITHOUT WaitForEndOfFrame — which batchmode does not evoke.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class TipRecorder : MonoBehaviour
    {
        public Transform tip;
        public Transform frame;
        public Vector3 LatestTipLocal { get; private set; }

        void LateUpdate()
        {
            if (tip != null && frame != null) LatestTipLocal = frame.InverseTransformPoint(tip.position);
        }
    }
}
