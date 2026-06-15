using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// THE WALK-FLOAT regression guard (ticket 86ca8rdkp attempt-9 — the bug class every PRIOR ground test
    /// MISSED). The Sponsor's soak of e1289ef showed the castaway hovering ~0.5-1u above the sand WHILE
    /// WALKING. Diagnose-via-trace OVERTURNED the e1289ef hypothesis (which grounded the avatar ROOT and
    /// assumed the rendered feet ride it because "the clips are in-place"):
    ///
    ///   ClipBaselineDiagnose (scale-immune baked sole, avatarRoot@world-0) MEASURED the truth — the IDLE clip
    ///   plants the sole at root-relative -0.003 (feet ON the root), but the WALK clip plants it at +0.63..+0.69:
    ///   the Mixamo Walk clip's HIPS/body are authored ~0.66u HIGHER than Idle's, so the WHOLE rendered mesh
    ///   floats ~0.66u while walking even though the root is correctly grounded (the shipped [FloatTrace] read
    ///   proxyRootGap≈0 AND BAKED_SOLE rising to +0.66 mid-stride — REAL float, not gauge noise).
    ///
    ///   It is NOT a scale/snap/shadow bug, and it is NOT fixable via clip import flags (lockRootHeightY /
    ///   heightFromFeet govern ROOT-MOTION extraction; with applyRootMotion=false the mesh is sampled IN-PLACE
    ///   from the raw bone curves — PROVEN: re-importing the Walk clip with those flags left the scale-immune
    ///   WALK sole at +0.66 unchanged). The lift lives in the BONE pose, per-clip.
    ///
    /// FIX (CastawayCharacter.modelSoleGround): ground the VISIBLE rendered SOLE (scale-immune) by offsetting
    /// the MODEL CHILD's local-Y so the feet plant in BOTH states (Idle residual ~0; Walk residual ~+0.66
    /// cancelled). Scale-immune (unit-scale TRS, never smr.localToWorldMatrix) so the FBX 100× cm→m node never
    /// double-applies (no ±68 runaway).
    ///
    /// WHY THIS GUARD IS THE LISTENER-WIRING-GRADE CATCH: every prior ground test used a synthetic static SMR
    /// or no mesh, so they passed during the ENTIRE walk-float era (the "pickup_count > 0 passed during the
    /// dual-spawn era" silent-killer class). THIS loads the REAL serialized Boot scene (the bytes the exe
    /// ships), drives the real NavMeshAgent into the WALK state, and asserts the SCALE-IMMUNE rendered sole is
    /// grounded WHILE WALKING — the exact percept the Sponsor judged. The deliberate-break (modelSoleGround
    /// OFF) proves the fix is load-bearing: the WALK sole floats ~0.66 again.
    ///
    /// (The judgment-grade proof of the fix is the gameplay-cam SHIPPED captures in the PR; this guard keeps
    /// the bug class from recurring silently in CI before a soak — the testing bar's regression-guard line.)
    /// </summary>
    public class CastawayWalkClipGroundedPlayModeTests
    {
        private CastawayCharacter _castaway;
        private NavMeshAgent _agent;

        // Load the real Boot scene + resolve the wired player avatar (the serialized FBX + Animator + WALK clip).
        private IEnumerator LoadBootAndResolve()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            // A couple frames for the scene load + Awake/Start to wire the agent + the avatar.
            yield return null;
            yield return null;

            _castaway = Object.FindObjectOfType<CastawayCharacter>();
            Assert.IsNotNull(_castaway, "the Boot scene must carry a serialized CastawayCharacter avatar");
            _agent = _castaway.GetComponentInParent<NavMeshAgent>();
            Assert.IsNotNull(_agent, "the avatar's player root must carry a NavMeshAgent");

            // Turn the F8 frame-trace ON so the diagnostic readouts (MeshFloatGap etc.) are computed every frame
            // even without the overlay (they update inside ApplyGroundSnap regardless; this just mirrors a soak).
            _castaway.SetFrameTrace(true);

            // Settle a couple frames so the agent registers on the NavMesh.
            yield return null;
            yield return null;
            Assert.IsTrue(_agent.isOnNavMesh, "the agent must be on the baked NavMesh");
        }

        // Drive the agent to a destination and BLOCK until it reads WALKING (the agent picked up speed). Returns
        // with the agent mid-traverse so the caller can sample the WALK-clip-driven sole. Times out loudly.
        private IEnumerator WalkUntilMoving(Vector3 dest)
        {
            Assert.IsTrue(NavMesh.SamplePosition(dest, out var hit, 6f, NavMesh.AllAreas),
                "the walk destination must sample onto the NavMesh");
            _agent.SetDestination(hit.position);

            float start = Time.time;
            while (Time.time - start < 5f)
            {
                if (_castaway.IsWalking && _agent.velocity.sqrMagnitude > 1f) yield break;
                yield return null;
            }
            Assert.Fail("the agent never reached the WALK state — cannot test the walk-clip grounding " +
                        "(agent.velocity=" + _agent.velocity.magnitude.ToString("0.00") + ")");
        }

        // THE CORE GUARD: while the WALK clip is driving the mesh, the SCALE-IMMUNE rendered sole must be
        // grounded on the visible terrain (MeshFloatGap ≈ 0) — NOT floating ~0.66u up (the e1289ef walk-float).
        // Sampled across SEVERAL walking frames (the lift is per-clip-frame; we assert the MAX gap stays small).
        [UnityTest]
        public IEnumerator WalkClipSole_StaysGrounded_NotFloating_AcrossTheStride()
        {
            yield return LoadBootAndResolve();
            Assert.IsTrue(_castaway.modelSoleGround,
                "the avatar must ship with modelSoleGround ENABLED (the walk-clip body-lift fix)");

            // Walk toward the foreshore (the worst-float band — the seaward dip). A point shoreward of spawn.
            yield return WalkUntilMoving(new Vector3(0f, 0f, -6f));

            // Sample the scale-immune rendered-sole gap across a window of WALKING frames. The lift is the
            // clip's per-frame body height; the model-sole grounding must hold the gap near 0 throughout.
            float maxAbsGap = 0f;
            int walkingFrames = 0;
            float start = Time.time;
            while (Time.time - start < 1.5f)
            {
                if (_castaway.IsWalking && !float.IsNaN(_castaway.MeshFloatGap))
                {
                    maxAbsGap = Mathf.Max(maxAbsGap, Mathf.Abs(_castaway.MeshFloatGap));
                    walkingFrames++;
                }
                yield return null;
            }

            Assert.Greater(walkingFrames, 5,
                "the test must observe several WALKING frames with a valid sole measurement");
            // The scale-immune rendered sole must sit within ~8cm of the visible sand the WHOLE stride. The bug
            // floated it ~0.66u; a regression (model grounding removed, or the clip-lift re-introduced) blows this.
            Assert.Less(maxAbsGap, 0.08f,
                $"while WALKING, the SCALE-IMMUNE rendered sole must stay grounded on the visible sand " +
                $"(|MeshFloatGap| < 0.08); peaked at {maxAbsGap:F3}. The e1289ef walk-float was ~0.66u — the " +
                "Mixamo Walk clip authors the body ~0.66u higher than Idle, and the root snap alone (e1289ef) " +
                "grounded the ROOT while the rendered mesh floated. modelSoleGround cancels that per-clip lift.");
        }

        // The DELIBERATE-BREAK half (success-test discipline): with modelSoleGround OFF, the WALK-clip body-lift
        // is NOT cancelled, so the scale-immune rendered sole floats well above the sand while walking — proving
        // the model-sole grounding (not some other effect) is what plants the walking feet.
        [UnityTest]
        public IEnumerator ModelSoleGroundOff_WalkClipSoleFloats_ProvingTheFixIsLoadBearing()
        {
            yield return LoadBootAndResolve();
            _castaway.modelSoleGround = false;   // disable the fix — the per-clip lift is no longer cancelled

            yield return WalkUntilMoving(new Vector3(0f, 0f, -6f));

            // Find the PEAK walk-clip float across the stride. Without the fix it reaches the clip's ~0.66u lift.
            float maxGap = 0f;
            int walkingFrames = 0;
            float start = Time.time;
            while (Time.time - start < 1.5f)
            {
                if (_castaway.IsWalking && !float.IsNaN(_castaway.MeshFloatGap))
                {
                    maxGap = Mathf.Max(maxGap, _castaway.MeshFloatGap);
                    walkingFrames++;
                }
                yield return null;
            }

            Assert.Greater(walkingFrames, 5, "must observe several WALKING frames");
            // Without the model grounding the WALK clip lifts the sole well off the sand (the bug). Assert a
            // float clearly larger than the grounded threshold — the e1289ef walk-float the Sponsor saw.
            Assert.Greater(maxGap, 0.3f,
                $"with modelSoleGround OFF the WALK clip's body-lift must float the rendered sole well off the " +
                $"sand (peak MeshFloatGap > 0.3; the clip authors ~0.66u of lift); got {maxGap:F3}. A small gap " +
                "here would mean the fix isn't load-bearing (something else is grounding the walking feet).");
        }

        // GROUNDED-AT-REST cross-check: the Idle clip must ALSO stay grounded (the fix must not REGRESS the
        // standing case that e1289ef got right). At rest the scale-immune sole gap must be ~0.
        [UnityTest]
        public IEnumerator IdleClipSole_StaysGrounded_AtRest()
        {
            yield return LoadBootAndResolve();
            // No move — let the Idle clip play + the snap settle.
            for (int i = 0; i < 40; i++) yield return null;

            Assert.IsFalse(_castaway.IsWalking, "the avatar must read idle at rest");
            Assert.IsFalse(float.IsNaN(_castaway.MeshFloatGap), "the rest-state sole gap must measure");
            Assert.Less(Mathf.Abs(_castaway.MeshFloatGap), 0.05f,
                $"at REST the scale-immune rendered sole must be grounded (|MeshFloatGap| < 0.05); got " +
                $"{_castaway.MeshFloatGap:F3}. (e1289ef got the standing case right; the walk-clip fix must " +
                "not regress it.)");
        }
    }
}
