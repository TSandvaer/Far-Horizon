using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cay4282 round 4 — THE ANALYTIC TWO-BONE SOLVE, pinned with no rig, no Animator and no clock.
    ///
    /// This exists as a pure static precisely so its correctness is not a claim about a live skeleton. The tests below
    /// are written against the two UGLY FAILURE MODES the brief names plus the refusal contract, because each of them
    /// is a way an IK ships something worse than the defect it fixes:
    ///   • THE ARM SNAPS STRAIGHT. A target beyond reach must leave the arm strictly bent and hand back a
    ///     <c>reachWeight</c> that eases out, never hold a locked stretch. Asserted on the ELBOW ANGLE, not on the
    ///     clamp's internal arithmetic — a proxy assert on the arithmetic can be satisfied while the pose still locks.
    ///   • THE ELBOW FLIPS. The pole is explicit, so the elbow must land on the POLE SIDE for every target direction,
    ///     and must move CONTINUOUSLY as the target sweeps (a flip shows up as a discontinuity, which is what a
    ///     per-frame driver renders as a snap).
    ///   • A DEGENERATE INPUT MUST REFUSE. <c>solved == false</c> means the caller writes NOTHING; a solver that
    ///     returns a plausible-looking pose on nonsense is the "metric green on nonsense" family in pose form.
    ///
    /// The reach/pole/clamp behaviour is verified through <see cref="TwoBoneIkSolver.ResolvedTip"/> — the same algebra
    /// the runtime applies — rather than a re-derivation beside it, so a test cannot go green against a broken
    /// production path (the tautological-assert / mirrored-implementation trap, unity-conventions.md §Editor-vs-runtime).
    /// </summary>
    public class TwoBoneIkSolverTests
    {
        // A canonical bent-arm chain, roughly the castaway's measured left arm: shoulder→elbow 0.2819 m,
        // elbow→palm 0.2582 m (AttackClipPoseDiag `[left-ik]`), bent about 90deg like the mine clip's own pose.
        private const float ALen = 0.2819f;
        private const float BLen = 0.2582f;

        private static void Chain(out Vector3 root, out Quaternion rootRot,
                                  out Vector3 mid, out Quaternion midRot, out Vector3 tip)
        {
            root = new Vector3(0.2f, 1.4f, -0.1f);          // deliberately NOT the origin
            rootRot = Quaternion.Euler(17f, -130f, 44f);    // …and deliberately not identity
            mid = root + Vector3.down * ALen;               // upper arm hanging down
            midRot = Quaternion.Euler(-8f, 60f, 12f);
            tip = mid + Vector3.forward * BLen;             // forearm folded forward => ~90deg elbow
        }

        private static float Elbow(Vector3 root, Vector3 mid, Vector3 tip) =>
            Vector3.Angle(root - mid, tip - mid);

        // =================================================================================================
        // 1 — AN IN-REACH TARGET IS HIT EXACTLY.
        // =================================================================================================

        [Test]
        public void AnInReachTarget_IsReachedExactly_ByTheAlgebraTheRuntimeApplies()
        {
            Chain(out Vector3 root, out Quaternion rootRot, out Vector3 mid, out Quaternion midRot, out Vector3 tip);
            // 0.40 m from the shoulder — comfortably inside the 0.5401 m extension and outside the fold minimum.
            Vector3 target = root + new Vector3(0.3f, -0.2f, 0.15f).normalized * 0.40f;

            var res = TwoBoneIkSolver.Solve(root, rootRot, mid, midRot, tip, target,
                                            poleHint: mid, poleFallbackDir: Vector3.back, reachFalloff: 0.30f);
            Assert.IsTrue(res.solved);
            Assert.IsFalse(res.clamped, "0.40 m sits inside the shell, so nothing should have been clamped");
            Assert.AreEqual(1f, res.reachWeight, 1e-5f, "an in-reach target must not blend out at all");

            Vector3 landed = TwoBoneIkSolver.ResolvedTip(res, root, rootRot, mid, midRot, tip);
            Assert.Less((landed - target).magnitude, 1e-3f,
                $"the tip must land ON the target (off by {(landed - target).magnitude * 1000f:F2} mm). This is checked " +
                "through ResolvedTip — the same composition the driver writes — so a wrong rotation ORDER cannot pass.");
        }

        [Test]
        public void SegmentLengths_AreConserved_SoTheSolveIsARotationNotAStretch()
        {
            Chain(out Vector3 root, out Quaternion rootRot, out Vector3 mid, out Quaternion midRot, out Vector3 tip);
            Vector3 target = root + new Vector3(-0.1f, -0.4f, 0.2f).normalized * 0.42f;
            var res = TwoBoneIkSolver.Solve(root, rootRot, mid, midRot, tip, target,
                                            poleHint: mid, poleFallbackDir: Vector3.back, reachFalloff: 0.30f);
            Assert.IsTrue(res.solved);

            // Reconstruct both joints the way the runtime does and check the bones did not change length. A solve that
            // "reaches" by stretching a bone would satisfy a tip-position assert while shipping a deformed arm.
            Quaternion rUpper = res.upperRotation * Quaternion.Inverse(rootRot);
            Vector3 midAfter = root + rUpper * (mid - root);
            Vector3 tipAfter = TwoBoneIkSolver.ResolvedTip(res, root, rootRot, mid, midRot, tip);
            Assert.AreEqual(ALen, (midAfter - root).magnitude, 1e-4f, "the upper segment must keep its length");
            Assert.AreEqual(BLen, (tipAfter - midAfter).magnitude, 1e-4f, "the lower segment must keep its length");
        }

        // =================================================================================================
        // 2 — THE ARM MUST NEVER SNAP STRAIGHT (the brief's first named ugly failure mode).
        // =================================================================================================

        [Test]
        public void AnOutOfReachTarget_LeavesTheArmSTRICTLY_BENT_AndNeverFullyExtended()
        {
            Chain(out Vector3 root, out Quaternion rootRot, out Vector3 mid, out Quaternion midRot, out Vector3 tip);
            // 0.90 m — far beyond the 0.5401 m extension. This is the shipped situation on 80/166 measured frames, so
            // it is the COMMON path, not an edge case.
            Vector3 target = root + Vector3.forward * 0.90f;

            var res = TwoBoneIkSolver.Solve(root, rootRot, mid, midRot, tip, target,
                                            poleHint: mid, poleFallbackDir: Vector3.back, reachFalloff: 0.30f);
            Assert.IsTrue(res.solved, "an out-of-reach target is still solvable — it clamps, it does not refuse");
            Assert.IsTrue(res.clamped);

            Quaternion rUpper = res.upperRotation * Quaternion.Inverse(rootRot);
            Vector3 midAfter = root + rUpper * (mid - root);
            Vector3 tipAfter = TwoBoneIkSolver.ResolvedTip(res, root, rootRot, mid, midRot, tip);
            float elbow = Elbow(root, midAfter, tipAfter);
            Assert.Less(elbow, 175f,
                $"the elbow reached {elbow:F1}deg. A fully-straight arm (180) reads as locked/dislocated — the exact " +
                "ugly failure mode the reach clamp exists to prevent. StraightArmFraction " +
                $"({TwoBoneIkSolver.StraightArmFraction:F2}) is what bounds this.");
            Assert.Less((tipAfter - root).magnitude, (ALen + BLen) * 0.999f,
                "…and the tip must stay strictly inside full extension, which is the same guarantee expressed as a " +
                "distance rather than an angle.");
        }

        [Test]
        public void ReachWeight_HoldsFullStrengthAcrossTheHOLDBand_ThenEasesTo0_Monotonically()
        {
            // ⚠ THE HOLD BAND IS THE ROUND-4 SHIPPED-GATE CORRECTION, pinned. Without it the ease begins at the shell
            // edge, so the frames with the LARGEST over-reach — exactly the ones that need the reach most — get the pin
            // at PARTIAL strength and the tip ends up further from its target than the clamped solve would have put it.
            // Measured on the shipped exe: worst frame reachWeight 0.65 and a 13.5 cm palm gap against a 13.0 cm bound,
            // i.e. the blend-out itself caused the FAIL. A clamped solve cannot over-extend the arm, so holding full
            // strength across the real working range is the correct behaviour and the ease guards only absurd targets.
            Chain(out Vector3 root, out Quaternion rootRot, out Vector3 mid, out Quaternion midRot, out Vector3 tip);
            const float falloff = 0.20f, hold = 0.25f;
            float shell = (ALen + BLen) * TwoBoneIkSolver.StraightArmFraction;

            float AtDistance(float d)
            {
                var r = TwoBoneIkSolver.Solve(root, rootRot, mid, midRot, tip, root + Vector3.forward * d,
                                              poleHint: mid, poleFallbackDir: Vector3.back, reachFalloff: falloff,
                                              straightArmFraction: TwoBoneIkSolver.StraightArmFraction,
                                              reachHold: hold);
                return r.reachWeight;
            }

            Assert.AreEqual(1f, AtDistance(shell - 0.01f), 1e-5f, "inside the shell the pin is at full strength");
            Assert.AreEqual(1f, AtDistance(shell), 1e-4f, "…and exactly AT the shell edge, with no step");
            Assert.AreEqual(1f, AtDistance(shell + 0.105f), 1e-4f,
                "…and at the MEASURED worst over-reach against the shipped seat (10.5 cm) it must STILL be 1. This is " +
                "the assert that would have caught round 4's first build, which ran 0.65 here.");
            Assert.AreEqual(1f, AtDistance(shell + hold), 1e-4f, "…all the way to the end of the hold band");

            float mid50 = AtDistance(shell + hold + falloff * 0.5f);
            Assert.Greater(mid50, 0.3f); Assert.Less(mid50, 0.7f);
            Assert.AreEqual(0f, AtDistance(shell + hold + falloff + 0.01f), 1e-5f,
                "beyond the falloff the pin is fully released, so the limb is back on the clip pose rather than holding " +
                "a stretch at an absurd target");

            // MONOTONIC across the whole range — a non-monotonic weight would read as the limb hunting.
            float prev = 1.0001f;
            for (int i = 0; i <= 30; i++)
            {
                float w = AtDistance(shell + (hold + falloff) * i / 30f);
                Assert.LessOrEqual(w, prev + 1e-5f, "reachWeight must fall monotonically");
                prev = w;
            }
        }

        [Test]
        public void WithNoHoldBand_TheEaseStartsAtTheShellEdge_TheRegressionTheShippedGateCaught()
        {
            // The NEGATIVE control for the test above: prove the hold band is doing real work rather than being a
            // no-op parameter. With reachHold = 0 (round 4's first build) the measured worst over-reach lands at a
            // partial weight — which is precisely the defect, so this must FAIL to be 1.
            Chain(out Vector3 root, out Quaternion rootRot, out Vector3 mid, out Quaternion midRot, out Vector3 tip);
            float shell = (ALen + BLen) * TwoBoneIkSolver.StraightArmFraction;
            var r = TwoBoneIkSolver.Solve(root, rootRot, mid, midRot, tip, root + Vector3.forward * (shell + 0.105f),
                                          poleHint: mid, poleFallbackDir: Vector3.back, reachFalloff: 0.30f,
                                          straightArmFraction: TwoBoneIkSolver.StraightArmFraction, reachHold: 0f);
            Assert.Less(r.reachWeight, 0.7f,
                $"with NO hold band the measured worst over-reach must ease to a partial weight (got " +
                $"{r.reachWeight:F2}; the shipped gate measured 0.65). If this ever reads 1, the hold-band parameter has " +
                "stopped mattering and the test above is vacuous.");
        }

        // =================================================================================================
        // 3 — THE ELBOW CANNOT FLIP (the brief's second named ugly failure mode).
        // =================================================================================================

        [Test]
        public void TheElbow_LandsOnThePoleSide_ForEveryTargetDirection()
        {
            Chain(out Vector3 root, out Quaternion rootRot, out Vector3 mid, out Quaternion midRot, out Vector3 tip);
            Vector3 pole = root + Vector3.down * 0.5f;    // an EXPLICIT pole point, well off any likely axis

            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0.25f, Mathf.Sin(a)).normalized;
                Vector3 target = root + dir * 0.40f;
                var res = TwoBoneIkSolver.Solve(root, rootRot, mid, midRot, tip, target,
                                                poleHint: pole, poleFallbackDir: Vector3.back, reachFalloff: 0.30f);
                Assert.IsTrue(res.solved, $"direction {i} must solve");

                Quaternion rUpper = res.upperRotation * Quaternion.Inverse(rootRot);
                Vector3 midAfter = root + rUpper * (mid - root);
                Vector3 axis = (target - root).normalized;
                Vector3 elbowOff = Vector3.ProjectOnPlane(midAfter - root, axis);
                Vector3 poleOff = Vector3.ProjectOnPlane(pole - root, axis);
                Assert.Greater(Vector3.Dot(elbowOff.normalized, poleOff.normalized), 0.99f,
                    $"at direction {i} the elbow sat on the WRONG side of the chain axis. The pole is explicit, so the " +
                    "elbow side is determined by construction — a negative dot here is the flip that renders as the " +
                    "arm snapping inside out mid-swing.");
            }
        }

        /// <summary>Sweep the target along a straight line and return the largest single-step elbow displacement, using
        /// the PRODUCTION pole idiom (the chain's own elbow) unless a fixed pole is given.</summary>
        private static float MaxElbowStep(Vector3 from, Vector3 to, int steps, Vector3? fixedPole = null)
        {
            Chain(out Vector3 root, out Quaternion rootRot, out Vector3 mid, out Quaternion midRot, out Vector3 tip);
            float worst = 0f; Vector3 prev = Vector3.zero; bool have = false;
            for (int i = 0; i <= steps; i++)
            {
                Vector3 target = root + Vector3.Lerp(from, to, i / (float)steps);
                var res = TwoBoneIkSolver.Solve(root, rootRot, mid, midRot, tip, target,
                                                poleHint: fixedPole.HasValue ? root + fixedPole.Value : mid,
                                                poleFallbackDir: Vector3.back, reachFalloff: 0.30f);
                if (!res.solved) continue;
                Quaternion rUpper = res.upperRotation * Quaternion.Inverse(rootRot);
                Vector3 elbow = root + rUpper * (mid - root);
                if (have) worst = Mathf.Max(worst, (elbow - prev).magnitude);
                prev = elbow; have = true;
            }
            Assert.IsTrue(have, "the sweep must have produced at least one solve");
            return worst;
        }

        [Test]
        public void SweepingTheTarget_MovesTheElbowCONTINUOUSLY_NoDiscontinuityToRenderAsASnap()
        {
            // 200 steps of a wide sweep is far finer than a 60 fps frame step over a 3.5 s swing, so any per-frame
            // discontinuity the driver could render shows up here as an oversized step. The pole is the PRODUCTION one
            // (the chain's own elbow — the driver passes `elbow` as poleHint), because that is the configuration whose
            // continuity actually ships.
            float worst = MaxElbowStep(new Vector3(0.35f, -0.20f, -0.25f), new Vector3(0.05f, -0.10f, 0.32f), 200);
            Assert.Less(worst, 0.02f,
                $"the elbow's worst single-step move was {worst * 100f:F2} cm. A jump is a FLIP; the target moved " +
                "smoothly (~4 mm per step), so the pose must too.");
        }

        [Test]
        public void APoleNEARLYALIGNEDWithTheChainAxis_AmplifiesTargetMotionIntoElbowMotion_AKnownBoundedProperty()
        {
            // A REAL PROPERTY OF POLE-VECTOR IK, found by the continuity test above rather than reasoned about, and
            // recorded here instead of being tuned away.
            //
            // The bend plane is defined by the pole's component PERPENDICULAR to the root->target axis. As the pole
            // approaches parallel with that axis, the perpendicular component shrinks and its DIRECTION becomes
            // hypersensitive: a small axis rotation swings the plane by roughly (parallel/perp) x that rotation, and the
            // elbow — which stands a·sin(A) off the axis — moves proportionally. Measured here: sweeping a target
            // through the straight-DOWN direction with a straight-DOWN FIXED pole moves the elbow ~2.1 cm per 4 mm
            // target step (a ~5x amplification), versus ~0.5 cm in the well-conditioned sweep above.
            //
            // WHY PRODUCTION IS NOT EXPOSED TO IT: the driver passes the CLIP'S OWN ELBOW as the pole, whose parallel
            // and perpendicular components are a·cos(A) and a·sin(A) — so the amplification is cot(A), which is <= 1 for
            // any root angle >= 45deg, i.e. de-amplifying rather than amplifying over the working range. The measured
            // live figure across the shipped mine clip is `pole perp MIN 0.147 m` at the shipped pin (u 0.35), an
            // order of magnitude above the solver's degeneracy threshold. A FIXED world-space pole is what would need
            // this guard, and the fallback is the only place one is used.
            //
            // The bound below is deliberately generous — it records the pathology's SCALE rather than pretending it is
            // absent, and a change that makes it materially worse still reds.
            float amplified = MaxElbowStep(new Vector3(0.35f, -0.20f, -0.25f), new Vector3(-0.30f, -0.15f, 0.30f), 200,
                                           fixedPole: Vector3.down * 0.5f);
            Assert.Greater(amplified, 0.015f,
                "this configuration is EXPECTED to amplify (that is the point of the test) — if it stops doing so the " +
                "note above has gone stale and should be re-measured rather than trusted.");
            Assert.Less(amplified, 0.06f,
                $"…but the amplification must stay BOUNDED (measured {amplified * 100f:F2} cm/step). An unbounded jump " +
                "would be a genuine elbow FLIP rather than plane hypersensitivity.");
        }

        [Test]
        public void WhenTheClipElbowCannotDefineAPlane_TheNAMEDFallbackIsUsed_AndSaysSo()
        {
            // Arrange the degenerate case on purpose: the target directly along the chain's own current direction, so
            // the clip elbow projects to ~zero off the axis and cannot define a bend plane.
            Vector3 root = Vector3.zero;
            Quaternion rootRot = Quaternion.identity, midRot = Quaternion.identity;
            Vector3 mid = new Vector3(0f, -ALen, 0f);
            Vector3 tip = new Vector3(0f, -(ALen + BLen), 0f);      // a STRAIGHT chain: elbow is ON the axis
            Vector3 target = new Vector3(0f, -0.40f, 0f);           // straight down the same line

            var res = TwoBoneIkSolver.Solve(root, rootRot, mid, midRot, tip, target,
                                            poleHint: mid, poleFallbackDir: Vector3.forward, reachFalloff: 0.30f);
            Assert.IsTrue(res.solved, "the fallback pole must rescue the solve rather than refusing it");
            Assert.IsTrue(res.poleFromFallback,
                "…and it must SAY it used the fallback. A build silently running on the fallback for a whole swing is a " +
                "real finding a reviewer should be able to read, not infer.");
        }

        [Test]
        public void WithNoUsablePoleAtAll_TheSolveREFUSES_RatherThanGuessingAPlane()
        {
            Vector3 root = Vector3.zero;
            Quaternion q = Quaternion.identity;
            Vector3 mid = new Vector3(0f, -ALen, 0f);
            Vector3 tip = new Vector3(0f, -(ALen + BLen), 0f);
            Vector3 target = new Vector3(0f, -0.40f, 0f);
            // Both the clip elbow AND the fallback lie along the axis => no plane exists.
            var res = TwoBoneIkSolver.Solve(root, q, mid, q, tip, target,
                                            poleHint: mid, poleFallbackDir: Vector3.down, reachFalloff: 0.30f);
            Assert.IsFalse(res.solved,
                "with no usable bend plane the solve must REFUSE, so the caller writes NOTHING. Inventing a plane here " +
                "picks an arbitrary elbow side, which is a flip waiting to happen on the next frame.");
            Assert.AreEqual(q, res.upperRotation, "a refused solve must hand back the INPUT rotations untouched");
            Assert.AreEqual(q, res.lowerRotation);
        }

        // =================================================================================================
        // 4 — DEGENERATE INPUT REFUSES.
        // =================================================================================================

        [Test]
        public void DegenerateChainsAndTargets_Refuse_AndReturnTheInputPoseUntouched()
        {
            Vector3 root = new Vector3(1f, 2f, 3f);
            Quaternion rr = Quaternion.Euler(10f, 20f, 30f), mr = Quaternion.Euler(-5f, 40f, 8f);

            // zero-length upper segment
            var a = TwoBoneIkSolver.Solve(root, rr, root, mr, root + Vector3.forward * BLen,
                                          root + Vector3.forward * 0.3f, root + Vector3.down, Vector3.back, 0.3f);
            Assert.IsFalse(a.solved); Assert.AreEqual(rr, a.upperRotation); Assert.AreEqual(mr, a.lowerRotation);

            // zero-length lower segment
            Vector3 mid = root + Vector3.down * ALen;
            var b = TwoBoneIkSolver.Solve(root, rr, mid, mr, mid, root + Vector3.forward * 0.3f,
                                          root + Vector3.down, Vector3.back, 0.3f);
            Assert.IsFalse(b.solved);

            // target sitting exactly on the root — no direction exists
            var c = TwoBoneIkSolver.Solve(root, rr, mid, mr, mid + Vector3.forward * BLen, root,
                                          root + Vector3.down, Vector3.back, 0.3f);
            Assert.IsFalse(c.solved);

            foreach (var r in new[] { a, b, c })
                Assert.AreEqual(0f, r.reachWeight,
                    "a refused solve must report ZERO reach weight, so a caller that multiplies by it writes nothing " +
                    "even if it ignores `solved`. Two independent ways to fail closed, not one.");
        }

        [Test]
        public void ATargetTooCLOSE_ToFold_IsClampedOutward_NotSolvedIntoAnImpossiblePose()
        {
            Chain(out Vector3 root, out Quaternion rootRot, out Vector3 mid, out Quaternion midRot, out Vector3 tip);
            // |a - b| = 0.0237 m is the tightest the chain can fold. Ask for 5 mm.
            Vector3 target = root + Vector3.forward * 0.005f;
            var res = TwoBoneIkSolver.Solve(root, rootRot, mid, midRot, tip, target,
                                            poleHint: mid, poleFallbackDir: Vector3.back, reachFalloff: 0.30f);
            Assert.IsTrue(res.solved);
            Assert.IsTrue(res.clamped, "a target inside the fold minimum must be clamped OUT, not solved impossibly");
            Assert.AreEqual(1f, res.reachWeight, 1e-5f,
                "…and it must NOT blend out: the reach weight is about OVER-reach only. A too-close target is a normal " +
                "pose the arm can hold, so releasing the pin there would drop the grip for no reason.");
            Vector3 landed = TwoBoneIkSolver.ResolvedTip(res, root, rootRot, mid, midRot, tip);
            Assert.Greater((landed - root).magnitude, Mathf.Abs(ALen - BLen) * 0.99f,
                "the tip must sit at or outside the fold minimum");
        }

        [Test]
        public void TheStraightArmFraction_IsAHardCeiling_NoCallerCanRequestAFullyStraightArm()
        {
            Chain(out Vector3 root, out Quaternion rootRot, out Vector3 mid, out Quaternion midRot, out Vector3 tip);
            Vector3 target = root + Vector3.forward * 5f;    // absurdly far, so the clamp is fully binding

            // A caller passing 1.0 (or more) must still be held below full extension: the dial is clamped INSIDE the
            // solver, so a bad serialized value or a runaway [Z] press cannot produce a locked arm.
            foreach (float requested in new[] { 1.0f, 2.0f, 50f })
            {
                var res = TwoBoneIkSolver.Solve(root, rootRot, mid, midRot, tip, target,
                                                poleHint: mid, poleFallbackDir: Vector3.back,
                                                reachFalloff: 100f, straightArmFraction: requested);
                Assert.IsTrue(res.solved);
                Vector3 landed = TwoBoneIkSolver.ResolvedTip(res, root, rootRot, mid, midRot, tip);
                Assert.LessOrEqual((landed - root).magnitude,
                                   (ALen + BLen) * TwoBoneIkSolver.StraightArmFraction + 1e-4f,
                    $"a requested fraction of {requested} must still be capped at StraightArmFraction " +
                    $"({TwoBoneIkSolver.StraightArmFraction:F2}) — the ceiling belongs to the solver, not to the " +
                    "caller's serialized field.");
            }
        }
    }
}
