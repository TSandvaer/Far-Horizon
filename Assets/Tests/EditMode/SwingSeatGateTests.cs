using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cayp0ay — guards for the SWING-TIME held-weapon SEAT gate (<see cref="SwingSeatGate"/>, driven by
    /// <c>SwingVerifyCapture.ChopSeatPass</c>).
    ///
    /// EVERY input below is a MEASURED value, not an invented one. The two liveness anchors come from running the
    /// SAME shipped exe twice with the SAME -verifySwings flag and only the launch mode differing; the seat anchor
    /// comes from the chop-seat pass's own measurement run. The PR body names both runs. That matters because a
    /// threshold test fed invented numbers proves only that arithmetic works.
    ///
    /// WHY THESE ARE EDITMODE AND NOT A CAPTURE-GATE ASSERT: -verifySwings has no wrapper script and ci.yml never
    /// invokes it (tests/scripts/test_gate_scripts.sh says so in its own gate-wiring block), so nothing it asserts
    /// blocks a merge today. EditMode DOES block a merge. So the pass criteria live in a pure static that EditMode
    /// pins, and the capture pass consumes it — the same "one seam, no disagreement" split TwoHandGripRead uses.
    /// </summary>
    public class SwingSeatGateTests
    {
        // ---- MEASURED inputs (see the class summary + the PR body for the runs that produced them) --------------
        private const float HeadlessPeakTiltDeg = 2.6f;    // launch mode: -batchmode      => the swing did NOT pose
        private const float WindowedPeakTiltDeg = 42.2f;   // launch mode: windowed        => the swing DID pose
        private const float ChopPeakTiltDeg = 43.3f;       // the chop-seat pass's own measured liveness figure
        private const float ApprovedSeatSW = 0.4027f;      // measured worst right-hand-to-haft on the approved seat
        private const int PlentySamples = 119;             // the chop-seat pass's own measured scored-sample count
        // One haft radius expressed in u on the wood axe's own haft, MEASURED on the same run (haft radius
        // 0.0448 m / measured haft length). Filled from the chop-seat pass's ALONG-HAFT log line.
        private const float ApprovedDriftU = 0.0512f;   // 0.0448 m haft radius / 0.8748 m measured haft length

        // =========================================================================================================
        // LIVENESS — the precondition. Each leg reds exactly one constructed failing instance and names it.
        // =========================================================================================================

        /// <summary>
        /// THE CORE GUARD. The measured NOT-POSING reading must red, and the measured POSING reading must green,
        /// against the same floor. Without this the gate reports a grip number computed on an idle skeleton — which
        /// is exactly what the shipped two-hand pass did headless: 4696 frames scored, 39.5 cm reported, 2.6 deg of
        /// torso tilt behind it.
        /// </summary>
        [Test]
        public void Posed_RedsTheMeasuredNonPosingRun_AndGreensTheMeasuredPosingRun_86cayp0ay()
        {
            bool notPosed = SwingSeatGate.Posed(4696, 3, HeadlessPeakTiltDeg, out string whyNot);
            Assert.IsFalse(notPosed,
                "The measured NOT-POSING run (peak torso tilt " + HeadlessPeakTiltDeg + " deg, headless launch of " +
                "the shipped exe) must NOT be accepted as a posed swing - a grip figure measured on an idle " +
                "skeleton is not a swing verdict. Floor is " + SwingSeatGate.TorsoTiltPosedFloorDeg + " deg.");
            StringAssert.Contains("SWING NEVER POSED", whyNot,
                "A liveness RED must NAME itself so the failure is diagnosable from the line alone; got: " + whyNot);

            bool posed = SwingSeatGate.Posed(PlentySamples, 3, WindowedPeakTiltDeg, out string whyYes);
            Assert.IsTrue(posed,
                "The measured POSING run (peak torso tilt " + WindowedPeakTiltDeg + " deg, windowed launch of the " +
                "SAME exe with the SAME flag) must be accepted - a floor that reds a real swing is useless. Got: " +
                whyYes);
        }

        /// <summary>ANTI-VACUITY: zero scored samples must FAIL, never pass. A gate that iterates nothing and
        /// returns true is the shape this project has measured twice.</summary>
        [Test]
        public void Posed_FailsClosed_OnZeroScoredSamples_86cayp0ay()
        {
            bool ok = SwingSeatGate.Posed(0, 0, WindowedPeakTiltDeg, out string why);
            Assert.IsFalse(ok, "0 scored samples must FAIL closed - an empty window is not a pass. Got: " + why);
            StringAssert.Contains("SWING NEVER SCORED", why, "The empty-window RED must name itself; got: " + why);
        }

        /// <summary>ANTI-VACUITY: an unmeasurable pose (unresolved hips/head bones -> NaN) must FAIL, never pass.
        /// "We do not know" must never render as "it is fine".</summary>
        [Test]
        public void Posed_FailsClosed_OnUnmeasurableTilt_86cayp0ay()
        {
            bool ok = SwingSeatGate.Posed(PlentySamples, 3, float.NaN, out string why);
            Assert.IsFalse(ok, "A NaN tilt must FAIL closed - unmeasured is not a pass. Got: " + why);
            StringAssert.Contains("SWING POSE UNMEASURABLE", why, "The unmeasurable RED must name itself; got: " + why);
        }

        /// <summary>
        /// PHASE COVERAGE: thousands of samples clustered in ONE phase must not pass. The hand line wanders
        /// 21.0 deg mean / 36.6 deg max about its own mean through a swing, so a single phase can be a good moment
        /// of a bad swing - which is why the criterion counts phases, not frames.
        /// </summary>
        [Test]
        public void Posed_FailsWhenSamplesCoverFewerThanThreePhases_86cayp0ay()
        {
            bool ok = SwingSeatGate.Posed(4696, 1, WindowedPeakTiltDeg, out string why);
            Assert.IsFalse(ok,
                "4696 samples in ONE phase must FAIL - frames are cheap, phases are what has to be covered. Got: " + why);
            StringAssert.Contains("PHASE COVERAGE TOO THIN", why, "The coverage RED must name itself; got: " + why);
            Assert.IsTrue(SwingSeatGate.Posed(PlentySamples, SwingSeatGate.RequiredPhases, WindowedPeakTiltDeg, out _),
                "Full phase coverage on a posed swing must pass.");
        }

        /// <summary>The floor must sit strictly BETWEEN the two measured readings. If it ever drifts outside that
        /// interval it either reds every real swing or greens every idle one, and this test says which.</summary>
        [Test]
        public void TorsoTiltFloor_SitsBetweenTheTwoMeasuredLaunchModeReadings_86cayp0ay()
        {
            Assert.Greater(SwingSeatGate.TorsoTiltPosedFloorDeg, HeadlessPeakTiltDeg,
                "The liveness floor must be ABOVE the measured NOT-POSING reading or a non-posing run passes.");
            Assert.Less(SwingSeatGate.TorsoTiltPosedFloorDeg, WindowedPeakTiltDeg,
                "The liveness floor must be BELOW the measured POSING reading or every real swing reds.");
            Assert.AreEqual(Mathf.Sqrt(HeadlessPeakTiltDeg * WindowedPeakTiltDeg),
                SwingSeatGate.TorsoTiltPosedFloorDeg, 1e-4f,
                "The floor must remain the GEOMETRIC MEAN of the two measured readings - the placement that is the " +
                "same multiplicative distance from each. A hand-edited value here is an invented threshold.");
        }

        // =========================================================================================================
        // SEAT — the displacement bound.
        // =========================================================================================================

        /// <summary>
        /// THE SUCCESS-TEST GUARD, at the arithmetic layer: the approved measured seat must PASS and a ~30 cm seat
        /// error - the magnitude the historical 36.6 cm cap permitted - must RED, naming the measured value and the
        /// phase. (The shipped-build half of the same proof is the -swingSeatFaultCm 30 run in the PR body.)
        /// </summary>
        [Test]
        public void SeatOk_PassesTheApprovedSeat_AndRedsAThirtyCentimetreError_86cayp0ay()
        {
            const float swM = 0.4442f;   // the live shoulder width measured on the same run
            Assert.IsTrue(SwingSeatGate.SeatOk(ApprovedSeatSW, swM, 0.90f, out string okWhy),
                "The MEASURED approved seat (" + ApprovedSeatSW + " SW) must pass its own bound. Got: " + okWhy);

            float thirtyCmSW = ApprovedSeatSW + 0.30f / swM;
            Assert.IsFalse(SwingSeatGate.SeatOk(thirtyCmSW, swM, 0.90f, out string badWhy),
                "A 30 cm seat error must RED - that is the magnitude the historical 36.6 cm cap permitted while the " +
                "Sponsor's eye caught the defect. Got: " + badWhy);
            StringAssert.Contains("SEAT DRIFTED OFF THE HAND", badWhy, "The seat RED must name itself; got: " + badWhy);
            StringAssert.Contains("phase 0.90", badWhy,
                "The seat RED must name the swing PHASE it was sampled at, not just the number; got: " + badWhy);
        }

        /// <summary>ANTI-VACUITY: an unmeasured seat (no valid reading taken -> the sentinel -1) must FAIL closed.
        /// This is the leg that stops "the haft segment never resolved" from reading as "the seat is fine".</summary>
        [Test]
        public void SeatOk_FailsClosed_WhenNoReadingWasTaken_86cayp0ay()
        {
            Assert.IsFalse(SwingSeatGate.SeatOk(-1f, 0.4442f, float.NaN, out string why),
                "An unmeasured seat must FAIL closed. Got: " + why);
            StringAssert.Contains("SEAT UNMEASURED", why, "The unmeasured RED must name itself; got: " + why);
        }

        /// <summary>
        /// THE BOUND MUST STAY ANCHORED. It is the measured approved seat plus exactly ONE haft radius of drift -
        /// both terms measured off shipped meshes. This is the test that reds a future "just widen it a bit" edit,
        /// which is precisely how 0.80 SW / 36.6 cm shipped.
        /// </summary>
        [Test]
        public void ChopSeatBound_IsTheMeasuredApprovedSeatPlusOneHaftRadius_86cayp0ay()
        {
            Assert.AreEqual(TwoHandGripRead.HaftRadiusM / TwoHandGripRead.ReferenceShoulderWidthM,
                SwingSeatGate.AllowedSeatDriftSW, 1e-6f,
                "The allowed drift must remain the haft's own measured cross-section radius, normalised.");
            Assert.AreEqual(SwingSeatGate.MeasuredWorstChopRightHaftSW + SwingSeatGate.AllowedSeatDriftSW,
                SwingSeatGate.ChopRightHaftPassSW, 1e-6f,
                "The bound must remain measured-approved-seat + one haft radius. Any other value is a chosen " +
                "headroom, which is the failure mode this gate exists downstream of.");
            Assert.Less(SwingSeatGate.ChopRightHaftPassSW, SwingSeatGate.MeasuredWorstChopRightHaftSW + 0.30f / 0.4442f,
                "The bound must stay BELOW the approved seat plus 30 cm, or the success-test error passes.");
        }

        /// <summary>LIVENESS IS A PRECONDITION, not a co-equal term: when the swing did not pose, the composed
        /// verdict must fail with the LIVENESS reason and must NOT surface a seat number that would read as a real
        /// measurement of a real swing. This is the leg that reds if someone re-orders the two terms.</summary>
        [Test]
        public void Verdict_ReportsTheLivenessReason_NotASeatNumber_WhenTheSwingNeverPosed_86cayp0ay()
        {
            bool ok = SwingSeatGate.Verdict(4696, 3, HeadlessPeakTiltDeg, ApprovedSeatSW, 0.4442f, 0.90f,
                                            SwingSeatGate.MeasuredApprovedChopUMin,
                                            SwingSeatGate.MeasuredApprovedChopUMax, ApprovedDriftU,
                                            out string why);
            Assert.IsFalse(ok, "A non-posing window must never produce a passing verdict. Got: " + why);
            StringAssert.Contains("SWING NEVER POSED", why, "The composed RED must be the LIVENESS reason; got: " + why);
            StringAssert.DoesNotContain("seat held", why,
                "A non-posing window must NOT report a seat conclusion - the number in it is an idle-pose reading.");
        }

        /// <summary>The composed verdict passes only when BOTH terms hold, on the measured chop run's own figures.
        /// Removing either conjunct reds this.</summary>
        [Test]
        public void Verdict_PassesOnTheMeasuredChopRun_86cayp0ay()
        {
            Assert.IsTrue(SwingSeatGate.Verdict(PlentySamples, 3, ChopPeakTiltDeg, ApprovedSeatSW, 0.4442f, 0.90f,
                                                SwingSeatGate.MeasuredApprovedChopUMin,
                                                SwingSeatGate.MeasuredApprovedChopUMax, ApprovedDriftU,
                                                out string why),
                "The chop-seat pass's own MEASURED run must pass the criteria it is gated on. Got: " + why);
        }

        /// <summary>
        /// COMPOSITION GUARD (a) — <see cref="SwingSeatGate.Verdict"/> must RED when ONLY the along-haft leg fails.
        ///
        /// WHY THIS EXISTS, AND IT IS NOT HYPOTHETICAL: the AlongOk_* cases above prove the along-haft FUNCTION
        /// behaves, and Verdict_PassesOnTheMeasuredChopRun proves the composition greens - but neither pins that
        /// Verdict actually CONSULTS the along result. Mutating <c>return seat &amp;&amp; along;</c> to
        /// <c>return seat &amp;&amp; (along || true);</c> killed ZERO of the 13 tests that existed before this one
        /// (demonstrated-RED matrix M9, 2026-08-01) - i.e. the ALONG axis could be silently un-gated while every
        /// test stayed green. That is precisely the shape this whole ticket exists downstream of: a leg that is
        /// present, tested in isolation, and wired to nothing.
        /// </summary>
        [Test]
        public void Verdict_RedsWhenOnlyTheAlongHaftLegFails_86cayp0ay()
        {
            const float haftLenM = 0.8748f;
            float slidU = SwingSeatGate.MeasuredApprovedChopUMax + 0.30f / haftLenM;

            // Posed swing + a PERFECT perpendicular seat: the only thing wrong is where along the stick the hand is.
            bool ok = SwingSeatGate.Verdict(PlentySamples, 3, ChopPeakTiltDeg, ApprovedSeatSW, 0.4442f, 0.90f,
                                            SwingSeatGate.MeasuredApprovedChopUMin, slidU, ApprovedDriftU,
                                            out string why);
            Assert.IsFalse(ok,
                "A 30 cm slide ALONG the haft must red the COMPOSED verdict even though liveness and the " +
                "perpendicular seat are both fine - otherwise the along leg is computed and discarded. Got: " + why);
            StringAssert.Contains("GRIP SLID ALONG THE HAFT", why,
                "The composed RED must carry the along-haft reason; got: " + why);
        }

        /// <summary>
        /// COMPOSITION GUARD (b) — the symmetric case: <see cref="SwingSeatGate.Verdict"/> must RED when ONLY the
        /// perpendicular seat leg fails. Pins the OTHER conjunct, so neither can be dropped silently.
        /// </summary>
        [Test]
        public void Verdict_RedsWhenOnlyThePerpendicularSeatLegFails_86cayp0ay()
        {
            const float swM = 0.4442f;
            float thirtyCmSW = ApprovedSeatSW + 0.30f / swM;

            // Posed swing + the hand at exactly the approved place ALONG the haft: only the perpendicular is wrong.
            bool ok = SwingSeatGate.Verdict(PlentySamples, 3, ChopPeakTiltDeg, thirtyCmSW, swM, 0.90f,
                                            SwingSeatGate.MeasuredApprovedChopUMin,
                                            SwingSeatGate.MeasuredApprovedChopUMax, ApprovedDriftU,
                                            out string why);
            Assert.IsFalse(ok,
                "A 30 cm perpendicular seat error must red the COMPOSED verdict even though liveness and the " +
                "along-haft position are both fine. Got: " + why);
            StringAssert.Contains("SEAT DRIFTED OFF THE HAND", why,
                "The composed RED must carry the seat reason; got: " + why);
        }

        /// <summary>
        /// THE BLIND-SIDE GUARD. A perpendicular distance-to-LINE cannot see the tool sliding ALONG its own axis -
        /// translate the haft parallel to itself and the line maps onto itself, so the distance does not move AT
        /// ALL. That is a property of the metric, not an observation.
        ///
        /// ⚠ It is deliberately NOT backed here by "a 30 cm fault left the perpendicular unchanged" - an earlier
        /// version of this docstring said exactly that, and it was wrong: that reading came from an injector
        /// writing a field the rig stomps every LateUpdate, so NEITHER axis moved and the run measured nothing.
        /// With the injection landing, a 30 cm hand-local +X fault moves both (0.4027 -> 0.7172 SW perpendicular,
        /// u 0.2004 -> 0.0107 along). See SwingSeatGate's ALONG-HAFT block for the full correction.
        /// </summary>
        [Test]
        public void AlongOk_PassesTheApprovedGrip_AndRedsAThirtyCentimetreSlide_86cayp0ay()
        {
            const float haftLenM = 0.8748f;   // measured on the wood axe's own haft, same run
            Assert.IsTrue(SwingSeatGate.AlongOk(SwingSeatGate.MeasuredApprovedChopUMin,
                                                SwingSeatGate.MeasuredApprovedChopUMax, ApprovedDriftU,
                                                out string okWhy),
                "The MEASURED approved grip position must pass its own bound. Got: " + okWhy);

            float slidU = SwingSeatGate.MeasuredApprovedChopUMax + 0.30f / haftLenM;
            Assert.IsFalse(SwingSeatGate.AlongOk(SwingSeatGate.MeasuredApprovedChopUMin, slidU, ApprovedDriftU,
                                                 out string badWhy),
                "A 30 cm slide ALONG the haft must RED - it is invisible to the perpendicular bound, which is " +
                "exactly why this leg exists. Got: " + badWhy);
            StringAssert.Contains("GRIP SLID ALONG THE HAFT", badWhy, "The along RED must name itself; got: " + badWhy);
        }

        /// <summary>ANTI-VACUITY: an unmeasured along-haft reading must FAIL closed, never pass.</summary>
        [Test]
        public void AlongOk_FailsClosed_WhenNoReadingWasTaken_86cayp0ay()
        {
            Assert.IsFalse(SwingSeatGate.AlongOk(float.NaN, float.NaN, ApprovedDriftU, out string why),
                "An unmeasured along-haft reading must FAIL closed. Got: " + why);
            StringAssert.Contains("ALONG-HAFT UNMEASURED", why, "The unmeasured RED must name itself; got: " + why);
        }

        /// <summary>Phase bucketing must span the whole swing and clamp rather than run off the end (an Animator
        /// normalizedTime can exceed 1). A bucketer that returns the same index for every input would make the
        /// phase-coverage leg above vacuous, so it is pinned here.</summary>
        [Test]
        public void PhaseBucket_SpansTheSwingAndClamps_86cayp0ay()
        {
            Assert.AreEqual(0, SwingSeatGate.PhaseBucket(0f), "wind-up must bucket to 0");
            Assert.AreEqual(1, SwingSeatGate.PhaseBucket(0.5f), "mid-swing must bucket to 1");
            Assert.AreEqual(SwingSeatGate.RequiredPhases - 1, SwingSeatGate.PhaseBucket(0.99f),
                "impact must bucket to the last phase");
            Assert.AreEqual(SwingSeatGate.RequiredPhases - 1, SwingSeatGate.PhaseBucket(7.3f),
                "a normalizedTime past 1 must CLAMP into the last phase, never index off the end");
            Assert.AreEqual(0, SwingSeatGate.PhaseBucket(-4f), "a negative must clamp into the first phase");
        }

        // =========================================================================================================
        // WIRING — the state name the pass scores on must exist on the SHIPPED controller.
        // =========================================================================================================

        /// <summary>
        /// <c>SwingVerifyCapture.AttackAxeState</c> is a literal duplicated from the controller build
        /// (CharacterAssetGen WireAttackClass). If the state is ever renamed, the chop-seat pass would score ZERO
        /// frames and - without this test - the only symptom would be the anti-vacuity leg firing at runtime in a
        /// gate CI does not run. So the duplication is pinned against the committed controller asset itself.
        /// </summary>
        [Test]
        public void AttackAxeStateName_ExistsOnTheShippedController_86cayp0ay()
        {
            const string path = "Assets/Art/Character/Castaway/CastawayAnimator.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Assert.IsNotNull(ctrl, "The shipped Animator controller is missing at " + path);

            bool found = false;
            foreach (var layer in ctrl.layers)
                foreach (var st in layer.stateMachine.states)
                    if (st.state != null && st.state.name == SwingVerifyCapture.AttackAxeState) found = true;

            Assert.IsTrue(found,
                "No state named '" + SwingVerifyCapture.AttackAxeState + "' exists on " + path + ". The chop-seat " +
                "pass scores only frames that state owns on layer 0, so a rename makes it score ZERO frames - " +
                "silently, in a gate ci.yml does not invoke. Update SwingVerifyCapture.AttackAxeState to match " +
                "CharacterAssetGen's WireAttackClass literal.");
        }
    }
}
