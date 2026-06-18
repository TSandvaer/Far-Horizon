using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the PURE soft-ceiling clamp math (ticket 86caa83wn — the "axe swings into the head
    /// when running" fix). HeldAxeRig.SoftClampMax is the unit-testable core: while the Mixamo RUN/JUMP clip
    /// pumps the right-hand bone UP near the head, the followed hand world-Y must be brought DOWN to (or below)
    /// a shoulder-relative ceiling — but a hand-Y already below the ceiling must be left UNTOUCHED (the
    /// follow-the-arm choice is kept; we only cap the vigorous into-head overshoot).
    ///
    /// This is the BUG-CLASS guard (not the instance): it pins the clamp's contract — never exceeds the
    /// ceiling, leaves sub-ceiling values alone, and eases smoothly through the soft knee (no hard pop that
    /// would read jerky). The PlayMode tests prove the BEHAVIOUR on the real rig; this proves the math.
    /// </summary>
    public class HeldAxeClampTests
    {
        [Test]
        public void SoftClampMax_LeavesValuesWellBelowTheCeiling_Untouched()
        {
            // A hand-Y comfortably below the ceiling (the WALK/IDLE grip, or the lower part of the run swing)
            // must pass through UNCHANGED — the Sponsor's follow-the-arm choice is preserved below the cap.
            const float ceiling = 2.0f, softness = 0.12f;
            foreach (float v in new[] { 0.0f, 1.0f, 1.5f, ceiling - softness - 0.001f })
                Assert.AreEqual(v, HeldAxeRig.SoftClampMax(v, ceiling, softness), 1e-5f,
                    $"a hand-Y ({v}) well below the ceiling ({ceiling}) must be left untouched — the clamp only " +
                    "caps the vigorous into-head overshoot, never the normal grip.");
        }

        [Test]
        public void SoftClampMax_NeverReturnsAboveTheCeiling_EvenForAHandPumpedFarAboveTheHead()
        {
            // The RUN arm-pump lifts the hand far above the shoulder ceiling (toward the head). The clamp must
            // NEVER let the result exceed the ceiling — that is what keeps the axe out of the head.
            const float ceiling = 2.0f, softness = 0.12f;
            foreach (float v in new[] { ceiling, ceiling + 0.1f, ceiling + 0.5f, ceiling + 2.0f, 100f })
                Assert.LessOrEqual(HeldAxeRig.SoftClampMax(v, ceiling, softness), ceiling + 1e-5f,
                    $"a hand-Y ({v}) pumped above the shoulder ceiling ({ceiling}) must be clamped to at most the " +
                    "ceiling — the axe must stay below the head during RUN/JUMP (86caa83wn).");
        }

        [Test]
        public void SoftClampMax_IsMonotonicAndSmooth_NoHardPopAtTheKnee()
        {
            // The soft knee must be C1-smooth: as the raw value rises through the knee the result rises
            // monotonically and continuously toward the ceiling (no discontinuous jump that reads as a pop).
            const float ceiling = 2.0f, softness = 0.12f;
            float prev = HeldAxeRig.SoftClampMax(0f, ceiling, softness);
            for (float v = 0.01f; v <= ceiling + 0.5f; v += 0.01f)
            {
                float r = HeldAxeRig.SoftClampMax(v, ceiling, softness);
                Assert.GreaterOrEqual(r, prev - 1e-6f, $"clamp output must be monotonic non-decreasing (v={v})");
                // The output slope must be <= the input slope (the clamp only ever SLOWS the rise, never speeds it).
                float step = r - prev;
                Assert.LessOrEqual(step, 0.01f + 1e-6f, $"clamp output must not rise faster than its input (v={v})");
                prev = r;
            }
        }

        [Test]
        public void SoftClampMax_HardClampWhenSoftnessZero()
        {
            // softness <= 0 → a plain max-clamp (no knee). Below stays, above snaps to the ceiling exactly.
            const float ceiling = 1.0f;
            Assert.AreEqual(0.5f, HeldAxeRig.SoftClampMax(0.5f, ceiling, 0f), 1e-6f);
            Assert.AreEqual(ceiling, HeldAxeRig.SoftClampMax(5f, ceiling, 0f), 1e-6f);
            Assert.AreEqual(ceiling, HeldAxeRig.SoftClampMax(5f, ceiling, -1f), 1e-6f);
        }

        // =================================================================================================
        // CLAMP ENGAGES ON RUN — the "clamp isn't biting" fix (ticket 86caa83wn fix 2).
        //
        // ROOT CAUSE the soak exposed: the clamp gates on CastawayCharacter.IsRunning, which was
        // `planarSpeed >= runSpeedThreshold`. But the NavMeshAgent's SIMULATED velocity (what we read) LAGS the
        // COMMANDED run speed (== runSpeedThreshold) and dips just below it on accel/decel ramps + obstacle
        // steering → IsRunning flickered FALSE mid-run → the clamp disengaged for those frames → the axe popped
        // into the head. Fix: engage running at a MARGIN (RunEngageFraction · threshold) below the commanded
        // speed, so a lagging run velocity still reads running and the clamp stays engaged — while a WALK speed
        // stays well below the margin and never reads running. These guard that engage-band contract (the BUG
        // CLASS: the run state must be ROBUST to the agent velocity dipping below the commanded run speed).
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
    }
}
