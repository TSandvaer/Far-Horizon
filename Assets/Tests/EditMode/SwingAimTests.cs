using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cb6v03j — guards for the SWING-AIM fix (the held weapon must point INTO the strike, not away from it).
    ///
    /// EVERY number below is MEASURED in the shipped exe, never invented. The baseline figures come from a
    /// -verifySwings run with the deltas forced to zero (the -swingAimFaultZero control), and the post-fix figures
    /// from the same build with them baked; both runs are named in the PR body. A threshold test fed invented
    /// numbers proves only that arithmetic works.
    ///
    /// WHAT THIS SUITE IS FOR, specifically. Three of the tests below exist because the corresponding bug ACTUALLY
    /// SHIPPED into a build during this ticket and was caught only by re-running the shipped-build gate — an
    /// expensive way to find each one. They are regression guards for real incidents, not hypotheticals:
    ///   * <see cref="SwingAimTable_IsNotAllZero_86cb6v03j"/> — a C# static-initialiser ORDER trap silently made the
    ///     whole lookup table Vector3.zero.
    ///   * <see cref="ComposeSeat_AtZeroAimWeight_IsBitIdenticalToThePreFixSeat_86cb6v03j"/> — the carry-pose
    ///     regression guarantee, which the pivot rewrite could have broken invisibly (the carry pose is not what
    ///     the aim gate looks at).
    ///   * <see cref="ComposeSeat_AimRotationPivotsAboutTheHand_86cb6v03j"/> — the wrong pivot moved the hand along
    ///     the haft and reddened an unrelated, previously-green gate leg.
    /// </summary>
    public class SwingAimTests
    {
        // The seat the shipped HeroAxe carries, so the algebra below is exercised on real values rather than on
        // conveniently round ones.
        private static readonly Vector3 SeatOffset = new Vector3(0.1312f, 0.1409f, 0.0593f);
        private static readonly Vector3 SeatEuler = new Vector3(12.0f, -8.0f, -82.0f);
        private static readonly Quaternion HandRot = Quaternion.Euler(37f, -114f, 61f);
        private static readonly Vector3 HandPos = new Vector3(3.5f, 1.4f, -2.25f);

        // ==========================================================================================================
        // THE CRITERION
        // ==========================================================================================================

        /// <summary>The bound is the SIGN of a cosine — the geometric boundary between "the head leads into the
        /// half-space he is attacking into" and "the head sweeps the strike backwards". It must stay 0: any nonzero
        /// value would be a tuned threshold, and a tuned threshold on this axis is how a gate ends up calibrated
        /// against what a fix achieves rather than against what the quantity means.</summary>
        [Test]
        public void PassFloor_IsTheGeometricSignBoundary_NotATunedNumber_86cb6v03j()
        {
            Assert.AreEqual(0f, SwingPointRead.StrikeFwdDotPassFloor, 1e-6f,
                "The swing-aim pass floor must remain exactly 0 (the sign change of dot(haft, facing)). A nonzero " +
                "floor is a tuned threshold; see the class doc on SwingPointRead.StrikeFwdDotPassFloor.");
        }

        /// <summary>Every MEASURED pre-fix mean reds, and every MEASURED post-fix mean greens. This is the test that
        /// makes the guard non-vacuous: it pins the criterion against the real readings on both sides of the fix.
        /// Pickaxe is absent on purpose — it is excluded from the gate by scope (its swing seat is the
        /// Sponsor-passed mine delta), and its measured -0.463 would red.</summary>
        [Test]
        public void StrikeAimOk_RedsEveryMeasuredPreFixMean_AndGreensEveryMeasuredPostFixMean_86cb6v03j()
        {
            // MEASURED, -verifySwings with -swingAimFaultZero (the fix removed).
            var preFix = new (string cls, float mean)[]
            { ("axe", 0.134f), ("dagger", -0.368f), ("spear", -0.326f), ("sword", -0.081f) };
            // MEASURED, same build, deltas baked.
            var postFix = new (string cls, float mean)[]
            { ("axe", 0.219f), ("dagger", 0.395f), ("spear", 0.843f), ("sword", 0.113f) };

            int reds = 0;
            foreach (var (cls, mean) in preFix)
                if (!SwingPointRead.StrikeAimOk(true, mean, cls, out _)) reds++;
            Assert.AreEqual(3, reds,
                "Three of the four gated classes measured a NEGATIVE mean fwdDot before the fix (dagger -0.368, " +
                "spear -0.326, sword -0.081) and must red; the axe measured +0.134 and legitimately passes this " +
                "criterion even unfixed. If this count changes, either the criterion moved or the baseline did.");

            foreach (var (cls, mean) in postFix)
                Assert.IsTrue(SwingPointRead.StrikeAimOk(true, mean, cls, out string why),
                    "Post-fix measured mean for " + cls + " must pass: " + why);
        }

        /// <summary>An unmeasured read FAILS CLOSED. "We could not measure it" must never render as "it is fine" —
        /// the vacuity direction SwingSeatGate's liveness term exists to kill.</summary>
        [Test]
        public void StrikeAimOk_FailsClosedWhenUnmeasured_86cb6v03j()
        {
            Assert.IsFalse(SwingPointRead.StrikeAimOk(false, 0.9f, "axe", out string whyA), whyA);
            StringAssert.Contains("UNMEASURED", whyA);
            Assert.IsFalse(SwingPointRead.StrikeAimOk(true, float.NaN, "axe", out string whyB), whyB);
            StringAssert.Contains("UNMEASURED", whyB);
        }

        // ==========================================================================================================
        // THE BAKED TABLE
        // ==========================================================================================================

        /// <summary>
        /// REGRESSION GUARD FOR A BUG THAT SHIPPED INTO A BUILD DURING THIS TICKET. The lookup was first written as
        /// <c>static readonly Vector3[] SwingAimEulerByClass = { SwingAimAxe, ... }</c> declared ABOVE the scalars it
        /// names. C# runs static field initialisers in DECLARATION order, so the array was built while every scalar
        /// was still Vector3.zero: the table shipped all-zeros, the delta applied at weight 1.00 for the right class
        /// every frame, and the shipped gate re-measured the axe at fwdDot -0.448 BYTE-IDENTICAL to the unfixed run.
        /// It looked exactly like "the fix does nothing" rather than like a compile-order bug.
        ///
        /// The lookup is a switch now, which has no initialisation order to get wrong — but a future refactor back
        /// to a table would reintroduce it silently, so this test asserts the OBSERVABLE consequence rather than the
        /// implementation: the four fixed classes must return non-zero.
        /// </summary>
        [Test]
        public void SwingAimTable_IsNotAllZero_86cb6v03j()
        {
            Assert.AreNotEqual(Vector3.zero, HeldToolRig.SwingAimEulerForClass(CastawayCharacter.WeaponClassAxe));
            Assert.AreNotEqual(Vector3.zero, HeldToolRig.SwingAimEulerForClass(CastawayCharacter.WeaponClassDagger));
            Assert.AreNotEqual(Vector3.zero, HeldToolRig.SwingAimEulerForClass(CastawayCharacter.WeaponClassSpear));
            Assert.AreNotEqual(Vector3.zero, HeldToolRig.SwingAimEulerForClass(CastawayCharacter.WeaponClassSword));
        }

        /// <summary>The PICKAXE is excluded from this fix by scope — its swing seat is the Sponsor-passed
        /// mineSeatEulerDelta (86cay4282, five rounds). A non-zero entry here would rotate the haft off the left
        /// palm the shipped gate measures at 0.239 SW, i.e. rework a bar the ticket forbids reworking.</summary>
        [Test]
        public void SwingAimPickaxe_IsZeroBecauseItsSwingSeatIsSponsorPassed_86cb6v03j()
        {
            Assert.AreEqual(Vector3.zero,
                HeldToolRig.SwingAimEulerForClass(CastawayCharacter.WeaponClassPickaxe));
        }

        /// <summary>An unmapped class (including the -1 "nothing holds the pose" sentinel) contributes NO rotation.
        /// Fail-safe toward leaving the approved seat alone.</summary>
        [Test]
        public void SwingAimEulerForClass_UnmappedIsIdentity_86cb6v03j()
        {
            Assert.AreEqual(Vector3.zero, HeldToolRig.SwingAimEulerForClass(-1));
            Assert.AreEqual(Vector3.zero, HeldToolRig.SwingAimEulerForClass(99));
        }

        // ==========================================================================================================
        // THE SEAT COMPOSITION
        // ==========================================================================================================

        /// <summary>
        /// THE CARRY-POSE REGRESSION GUARANTEE. At swing-aim weight 0 the seat must be BIT-FOR-BIT what it was
        /// before this ticket — that is what protects the Sponsor's approved in-hand seat on all 15 baked
        /// held-weapon poses, and it is invisible to the aim gate (which only ever looks at swing frames), so it
        /// needs its own guard. The pivot rewrite in particular could have broken it silently.
        /// </summary>
        [Test]
        public void ComposeSeat_AtZeroAimWeight_IsBitIdenticalToThePreFixSeat_86cb6v03j()
        {
            // The pre-fix composition, written out here rather than called, so this is a comparison against an
            // INDEPENDENT statement of the old behaviour rather than against the code under test.
            Vector3 expectedPos = HandPos + HandRot * SeatOffset;
            Quaternion expectedRot = HandRot * Quaternion.Euler(SeatEuler);

            HeldToolRig.ComposeSeat(HandPos, HandRot, SeatOffset, SeatEuler, Vector3.zero, Vector3.zero, 0f,
                                    new Vector3(-15.5f, 44.7f, 8.3f), 0f, out Vector3 pos, out Quaternion rot);

            Assert.AreEqual(expectedPos.x, pos.x, 1e-6f, "carry-pose seat POSITION moved at aim weight 0");
            Assert.AreEqual(expectedPos.y, pos.y, 1e-6f, "carry-pose seat POSITION moved at aim weight 0");
            Assert.AreEqual(expectedPos.z, pos.z, 1e-6f, "carry-pose seat POSITION moved at aim weight 0");
            Assert.Less(Quaternion.Angle(expectedRot, rot), 1e-3f,
                "carry-pose seat ROTATION moved at aim weight 0");
        }

        /// <summary>The back-compat overload (used by the pre-existing seat suites and the editor fit) must equal
        /// the new one at zero aim — otherwise an existing green test would be silently testing a different
        /// composition than the one that ships.</summary>
        [Test]
        public void ComposeSeat_LegacyOverloadEqualsZeroAim_86cb6v03j()
        {
            HeldToolRig.ComposeSeat(HandPos, HandRot, SeatOffset, SeatEuler, Vector3.zero, Vector3.zero, 0f,
                                    out Vector3 legacyPos, out Quaternion legacyRot);
            HeldToolRig.ComposeSeat(HandPos, HandRot, SeatOffset, SeatEuler, Vector3.zero, Vector3.zero, 0f,
                                    new Vector3(31f, -77f, 12f), 0f, out Vector3 pos, out Quaternion rot);
            Assert.AreEqual(legacyPos.x, pos.x, 1e-6f);
            Assert.AreEqual(legacyPos.y, pos.y, 1e-6f);
            Assert.AreEqual(legacyPos.z, pos.z, 1e-6f);
            Assert.Less(Quaternion.Angle(legacyRot, rot), 1e-3f);
        }

        /// <summary>
        /// REGRESSION GUARD FOR THE SECOND BUG THAT SHIPPED DURING THIS TICKET. The aim rotation must pivot about
        /// THE HAND. Pivoting about the tool ORIGIN (the haft's butt) swung the stick THROUGH the hand and slid the
        /// grip from u 0.2004 to u 0.2350..0.2560, reddening the previously-green chop-seat along-haft leg; pivoting
        /// about the haft point at u 0.2004 was ALSO wrong (rotating a line about a point on it still moves the foot
        /// of the perpendicular from an off-line point) and pushed u further out, to 0.2697..0.2929.
        ///
        /// The invariant that makes both seat legs exact rather than approximate: the HAND's position expressed in
        /// the TOOL's own frame is unchanged by the aim rotation. u and the perpendicular distance are both
        /// functions of exactly that, so both are preserved by construction. Asserted directly here.
        /// </summary>
        [Test]
        public void ComposeSeat_AimRotationPivotsAboutTheHand_86cb6v03j()
        {
            Vector3 aim = new Vector3(-15.5f, 44.7f, 8.3f);

            HeldToolRig.ComposeSeat(HandPos, HandRot, SeatOffset, SeatEuler, Vector3.zero, Vector3.zero, 0f,
                                    aim, 0f, out Vector3 pos0, out Quaternion rot0);
            HeldToolRig.ComposeSeat(HandPos, HandRot, SeatOffset, SeatEuler, Vector3.zero, Vector3.zero, 0f,
                                    aim, 1f, out Vector3 pos1, out Quaternion rot1);

            // The hand, expressed in the tool's own frame, must be the SAME vector before and after.
            Vector3 handInTool0 = Quaternion.Inverse(rot0) * (HandPos - pos0);
            Vector3 handInTool1 = Quaternion.Inverse(rot1) * (HandPos - pos1);
            Assert.Less((handInTool0 - handInTool1).magnitude, 1e-5f,
                "The aim rotation must pivot about the HAND: the hand's position in the tool's own frame moved " +
                "from " + handInTool0 + " to " + handInTool1 + ". Any movement here is the grip sliding along the " +
                "haft, which reds the chop-seat along-haft leg (measured: u 0.2004 -> 0.2350..0.2560).");

            // ...and the rotation must genuinely have been applied, or the assert above is vacuous.
            Assert.Greater(Quaternion.Angle(rot0, rot1), 5f,
                "The aim delta produced no rotation at weight 1 — the pivot assert above would then be trivially " +
                "satisfied and prove nothing.");
        }

        /// <summary>The aim term must ease by SLERP, not by euler-scaling. At the baked magnitudes (the spear's
        /// delta is a large rotation) euler-scaling names a different axis at intermediate weights and would sweep
        /// the weapon through orientations no dial ever specified. Detected as: the half-weight rotation is the
        /// geodesic half, i.e. its angle from identity is half the full angle.</summary>
        [Test]
        public void ComposeSeat_AimEasesAlongTheGeodesic_NotByEulerScaling_86cb6v03j()
        {
            Vector3 aim = HeldToolRig.SwingAimSpear;
            HeldToolRig.ComposeSeat(HandPos, HandRot, SeatOffset, SeatEuler, Vector3.zero, Vector3.zero, 0f,
                                    aim, 0f, out _, out Quaternion rot0);
            HeldToolRig.ComposeSeat(HandPos, HandRot, SeatOffset, SeatEuler, Vector3.zero, Vector3.zero, 0f,
                                    aim, 0.5f, out _, out Quaternion rotHalf);
            HeldToolRig.ComposeSeat(HandPos, HandRot, SeatOffset, SeatEuler, Vector3.zero, Vector3.zero, 0f,
                                    aim, 1f, out _, out Quaternion rot1);

            float full = Quaternion.Angle(rot0, rot1);
            float half = Quaternion.Angle(rot0, rotHalf);
            Assert.Greater(full, 30f, "the spear's baked delta should be a large rotation; this test is about large ones");
            Assert.AreEqual(full * 0.5f, half, 1.0f,
                "The aim term must SLERP: at weight 0.5 the rotation from identity should be exactly half the full " +
                "angle. Euler-scaling does not satisfy this at large angles — it names a different axis part-way " +
                "through and tumbles the weapon during the ease.");
        }

        // ==========================================================================================================
        // THE STATE GATE
        // ==========================================================================================================

        /// <summary>The per-class attack gate mirrors the proven mine-gate transition pairing: it engages on the
        /// FIRST frame of the crossfade IN (so an additive offset is never a crossfade late) and drops on the FIRST
        /// frame of the crossfade OUT (so it starts returning on the same frame the body does).</summary>
        [Test]
        public void AttackClassHoldingPose_IsTransitionPaired_86cb6v03j()
        {
            int axe = Animator.StringToHash(CastawayCharacter.AttackAxeStateName);
            int loco = Animator.StringToHash("Locomotion");

            Assert.AreEqual(CastawayCharacter.WeaponClassAxe,
                CastawayCharacter.AttackClassHoldingPoseFor(axe, false, 0),
                "settled in AttackAxe must hold the pose");
            Assert.AreEqual(CastawayCharacter.WeaponClassAxe,
                CastawayCharacter.AttackClassHoldingPoseFor(loco, true, axe),
                "the FIRST frame of the crossfade IN must already hold — otherwise the delta engages a full " +
                "transition late, after the swing already reads");
            Assert.AreEqual(-1,
                CastawayCharacter.AttackClassHoldingPoseFor(axe, true, loco),
                "the FIRST frame of the crossfade OUT must release — the hand-back window, mirroring " +
                "MineSwingHoldsPoseFor");
            Assert.AreEqual(-1,
                CastawayCharacter.AttackClassHoldingPoseFor(loco, false, 0),
                "plain locomotion holds no attack pose");
        }

        /// <summary>Each class routes to its OWN state, and the gate reports that class — not merely "some attack".
        /// A gate that conflated them would apply one class's aim delta during another's swing.</summary>
        [Test]
        public void AttackClassHoldingPose_ReportsThePerClassState_86cb6v03j()
        {
            var expected = new (int cls, string state)[]
            {
                (CastawayCharacter.WeaponClassAxe,     CastawayCharacter.AttackAxeStateName),
                (CastawayCharacter.WeaponClassPickaxe, CastawayCharacter.AttackPickaxeState),
                (CastawayCharacter.WeaponClassDagger,  CastawayCharacter.AttackDaggerStateName),
                (CastawayCharacter.WeaponClassSpear,   CastawayCharacter.AttackSpearStateName),
                (CastawayCharacter.WeaponClassSword,   CastawayCharacter.AttackSwordStateName),
            };
            foreach (var (cls, state) in expected)
            {
                Assert.AreEqual(state, CastawayCharacter.AttackStateNameForClass(cls));
                Assert.AreEqual(cls,
                    CastawayCharacter.AttackClassHoldingPoseFor(Animator.StringToHash(state), false, 0),
                    "class " + cls + " must be reported for state " + state);
            }
        }
    }
}
