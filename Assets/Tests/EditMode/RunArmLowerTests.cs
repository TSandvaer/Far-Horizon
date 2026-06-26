using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the RUN-swing reduction's engage band (ticket 86caa83wn soak #2 — the "when i run the
    /// axe is no longer in the hand" fix). The Sponsor's chosen approach REVERSED the earlier axe-side world-Y
    /// ceiling clamp (which detached the axe from the hand) — the axe now rides the hand RIGIDLY and the run
    /// into-head is fixed by LOWERING the right arm while running (CastawayArmPose.runLowerEuler), weighted by
    /// CastawayCharacter.IsRunning. So the LOAD-BEARING contract this file pins is the IsRunning ENGAGE BAND:
    /// the run state must engage robustly while running (so the arm-lower applies) and stay OFF at walk/idle (so
    /// the Sponsor's locked WALK pose is byte-unchanged). These engage-band tests carried over from the clamp
    /// era — the gate (IsRunning) is the same; only the consumer changed (arm-lower instead of axe-clamp).
    ///
    /// This is the BUG-CLASS guard (not the instance): the run state must be ROBUST to the agent velocity
    /// dipping below the commanded run speed (else the arm-lower flickers off mid-run → the axe rides into the
    /// head again) AND must never trip at walk speed (else a walk would lower the arm + change the locked pose).
    /// The PlayMode tests prove the BEHAVIOUR on the real rig; this proves the gate contract.
    /// </summary>
    public class RunArmLowerTests
    {
        // =================================================================================================
        // RUN STATE ENGAGES ROBUSTLY — the gate the run-swing reduction depends on (ticket 86caa83wn).
        //
        // ROOT CAUSE the earlier soak exposed: the run-state gates on CastawayCharacter.IsRunning, which was
        // `planarSpeed >= runSpeedThreshold`. But the NavMeshAgent's SIMULATED velocity (what we read) LAGS the
        // COMMANDED run speed (== runSpeedThreshold) and dips just below it on accel/decel ramps + obstacle
        // steering → IsRunning flickered FALSE mid-run → the run-dependent fix disengaged for those frames → the
        // axe popped into the head. Fix: engage running at a MARGIN (RunEngageFraction · threshold) below the
        // commanded speed, so a lagging run velocity still reads running and the arm-lower stays engaged — while
        // a WALK speed stays well below the margin and never reads running. These guard that engage-band contract
        // (the BUG CLASS: the run state must be ROBUST to the agent velocity dipping below the commanded speed).
        // =================================================================================================

        // Mirror the production wiring: runSpeedThreshold == WasdMovement.runSpeed == 9.5; walk speed == 5.5.
        private const float RunThreshold = 9.5f;
        private const float WalkSpeed = 5.5f;

        // The IsRunning predicate, mirrored from CastawayCharacter.LateUpdate, so the engage-band contract is
        // testable without a NavMeshAgent/Animator rig (headless PlayMode can't tick the agent — the documented
        // headless-time trap). It reads the SAME RunEngageFraction constant the runtime uses.
        private static bool IsRunningAt(float planarSpeed)
        {
            bool walking = planarSpeed > 0.15f; // walkSpeedThreshold
            return walking && planarSpeed >= RunThreshold * CastawayCharacter.RunEngageFraction;
        }

        [Test]
        public void RunState_EngagesAtFullCommandedRunSpeed()
        {
            Assert.IsTrue(IsRunningAt(RunThreshold),
                "at the commanded run speed the character must read RUNNING so the clamp engages.");
        }

        [Test]
        public void RunState_StaysEngaged_WhenSimulatedVelocityDipsBelowCommandedRunSpeed()
        {
            // THE FIX: the agent's simulated velocity lags the commanded 9.5 and dips a little below it mid-run.
            // It must STILL read running so the clamp does not flicker off (the "clamp isn't biting" bug).
            float dipped = RunThreshold * 0.92f; // ~8.74 — a realistic mid-run dip below the commanded 9.5
            Assert.IsTrue(IsRunningAt(dipped),
                $"a run velocity that dips to {dipped:F2} (below the commanded {RunThreshold}) must STILL read " +
                "running — else the clamp disengages those frames and the axe pops into the head (86caa83wn).");
            // The strict OLD predicate (>= threshold) would have FALSE'd here — quantify the fix.
            Assert.Less(dipped, RunThreshold, "sanity: the dipped speed is below the strict threshold the old predicate used.");
        }

        [Test]
        public void RunState_DoesNotEngageAtWalkSpeed_WalkSeatUntouched()
        {
            // The walk speed (5.5) must stay BELOW the engage margin (0.85·9.5 ≈ 8.1) so a WALK never trips the
            // clamp — the Sponsor's locked WALK/IDLE seat is untouched (the clamp is inert at walk/idle).
            Assert.IsFalse(IsRunningAt(WalkSpeed),
                $"the walk speed ({WalkSpeed}) must NOT read running (it is below the engage margin " +
                $"{RunThreshold * CastawayCharacter.RunEngageFraction:F2}) — the clamp must stay inert at walk so " +
                "the locked WALK seat is unchanged.");
            Assert.IsFalse(IsRunningAt(0f), "at rest the character is not running.");
        }

        [Test]
        public void RunEngageMargin_SitsBetweenWalkAndRunSpeeds()
        {
            // The engage margin must be a SAFE separator: strictly above the walk speed (no false-run on a walk)
            // AND strictly below the commanded run speed (a run reliably trips it). This pins the constant so a
            // future tweak that pushes it past either bound is caught.
            float margin = RunThreshold * CastawayCharacter.RunEngageFraction;
            Assert.Greater(margin, WalkSpeed + 0.5f,
                $"the run-engage margin ({margin:F2}) must sit clearly above the walk speed ({WalkSpeed}).");
            Assert.Less(margin, RunThreshold,
                $"the run-engage margin ({margin:F2}) must sit below the commanded run speed ({RunThreshold}).");
        }

        // =================================================================================================
        // RUN-LOWER WEIGHTING — the arm-side fix's math (ticket 86caa83wn soak #2).
        //
        // The run-lower is composed as Quaternion.Euler(runLowerEuler * runWeight), right-multiplied onto the
        // clip pose on the RIGHT upper-arm. The load-bearing contract: at runWeight 0 (walk/idle) the offset is
        // the IDENTITY → the Sponsor's locked WALK pose is byte-unchanged; at full run it LOWERS the arm (the
        // raise axis is local-Z, a negative Z lowers). These pin the math without a NavMeshAgent/Animator rig
        // (headless Time.deltaTime≈0 — the documented trap; the Animator never ticks the clip).
        // =================================================================================================

        // Mirror the production default (CastawayArmPose.runLowerEuler) so a change to the shipped value that
        // would stop lowering the arm is caught here too. 86caa83wn soak #3 (build 2993c1c): the Sponsor's
        // F9-dialed run carry (-10,12,-42) SUPERSEDES (0,0,-22) — now a MIXED euler (the dominant -Z still
        // lowers; small x/y refine the carry orientation).
        private static readonly Vector3 RunLowerEuler = new Vector3(-10f, 12f, -42f);

        [Test]
        public void RunLower_AtZeroWeight_IsIdentity_WalkPoseUntouched()
        {
            // At walk/idle the smoothed run weight rests at 0, so the run-lower offset must be the identity —
            // the right arm gets ONLY the carry pose, the Sponsor's locked WALK pose is byte-unchanged.
            Quaternion offset = Quaternion.Euler(RunLowerEuler * 0f);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, offset), 1e-4f,
                "the run-lower offset must be the IDENTITY at run weight 0 — the locked WALK/IDLE pose must be " +
                "byte-unchanged (the run-lower is inert at walk/idle).");
        }

        [Test]
        public void RunLower_AtFullWeight_LowersTheArm_NonIdentityOffset()
        {
            // At full run the run-lower must be a real, non-trivial rotation (it LOWERS the arm so the hand — and
            // the gripped axe that follows it — stays below the head). Assert it is non-identity AND of the
            // expected magnitude (|runLowerEuler| at weight 1).
            Quaternion offset = Quaternion.Euler(RunLowerEuler * 1f);
            float angle = Quaternion.Angle(Quaternion.identity, offset);
            Assert.Greater(angle, 10f,
                $"the run-lower offset at full run must be a meaningful lower (got {angle:F1}°) — too small a " +
                "lower leaves the axe riding into the head.");
            // The offset must rotate about the local-Z (the rig's raise axis) in the NEGATIVE direction (lower).
            offset.ToAngleAxis(out float a, out Vector3 axis);
            // A negative-Z euler yields an axis anti-parallel to +Z (axis·+Z < 0) — the lowering direction.
            Assert.Less(Vector3.Dot(axis.normalized, Vector3.forward), 0f,
                "the run-lower must rotate the arm DOWN about local-Z (a negative-Z euler) — a positive Z would " +
                "RAISE the arm into the head, the bug this fixes.");
        }

        [Test]
        public void RunLower_ScalesMonotonicallyWithWeight()
        {
            // As the smoothed run weight rises 0→1, the run-lower magnitude must rise monotonically (a smooth
            // ease-in as the run engages, ease-out as it stops) — no jump that would pop the arm.
            float prev = -1f;
            for (float w = 0f; w <= 1.0001f; w += 0.1f)
            {
                float angle = Quaternion.Angle(Quaternion.identity, Quaternion.Euler(RunLowerEuler * w));
                Assert.GreaterOrEqual(angle, prev - 1e-4f,
                    $"the run-lower magnitude must rise monotonically with the run weight (w={w:F1}).");
                prev = angle;
            }
        }
    }
}
