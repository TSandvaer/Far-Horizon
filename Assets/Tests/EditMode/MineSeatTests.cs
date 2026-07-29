using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cay4282 round 2 — the MINE-STATE HELD-WEAPON SEAT (the Sponsor's DIRECTION REVERSAL, verbatim: "we need to
    /// position the axe for a two hand grip").
    ///
    /// THE REAL-WORLD ANCHOR these tests are written against (lowpoly-quality.md §0): a two-handed grip is ONE HAFT
    /// PASSING THROUGH BOTH HANDS — both palms closed on the same stick, so the stick's LINE runs through both of
    /// them. Every assert below is about that sentence, not about a number that happens to be green.
    ///
    /// THE MEASUREMENT (AttackClipPoseDiag MINE-SEAT FIT pass, live rig, shipped repaired pickaxe clip, 61 samples;
    /// the live re-measure of the shipped delta reproduced the closed-form prediction exactly):
    ///     candidate            left-hand-to-haft      right-hand-to-haft   tool vs hand line   haft-torso clear
    ///     ZERO (round-1 seat)  mean 1.277  MAX 1.476  mean 0.166 / 0.179   90.0 deg            0.468
    ///     SHIPPED (refined)    mean 0.445  MAX 0.615  mean 0.000 / 0.000   32.7 deg            0.557
    /// (both rows are WRIST-INCLUSIVE: CastawayHandPose order 65 feeds the seat's hand.rotation)
    ///
    /// WHAT THIS FILE PINS — each a bug CLASS, not a value:
    ///   1. REST IS BYTE-UNCHANGED. At weight 0 both seat channels are the exact identity, so the carry / idle /
    ///      walk / run / jump / crouch seat and the other four swings are bit-for-bit the Sponsor-approved seat.
    ///      This is THE regression guard: the seat is shared by all 15 baked held-weapon poses.
    ///   2. THE DELTA ACTUALLY MOVES THE HAFT at weight 1 (the control for 1 — otherwise 1 would pass against a
    ///      mechanism wired to nothing, the tautological-assert trap).
    ///   3. THE GEOMETRY READ is a real measurement: the pure distance-to-segment maths behaves, an off-haft hand
    ///      FAILS, and an unmeasurable rig is NEVER a pass ("a metric is green on nonsense").
    ///   4. THE THRESHOLDS BRACKET THE MEASUREMENT — loose enough that a Sponsor re-dial cannot red a build, tight
    ///      enough that the pre-fix geometry DOES red. A cap outside that bracket is a cap that proves nothing.
    ///   5. THE SHIP SOURCE: MovementCameraScene.HeldToolMineSeat*Delta is what AttachHeroAxeToHand bakes into
    ///      Boot.unity, so a drifting runtime field default cannot silently become the shipped value.
    ///   6. ONE GATE, ONE EASE for the arm offset and the seat offset — they must never move out of step.
    /// </summary>
    public class MineSeatTests
    {
        private const float Dt = 1f / 60f;

        // The measured figures the caps are calibrated against (AttackClipPoseDiag MINE-SEAT FIT, live re-measure).
        private const float PreFixWorstLeftSW = 1.476f;
        private const float ShippedWorstLeftSW = 0.615f;
        private const float PreFixWorstRightSW = 0.179f;
        private const float ShippedWorstRightSW = 0.000f;

        // ==============================================================================================
        // 1 + 2 — REST IS BYTE-UNCHANGED, AND THE DELTA REALLY MOVES THE HAFT.
        // ==============================================================================================

        [Test]
        public void AtZeroWeight_BothSeatChannels_AreTheExactIdentity_SoEveryOtherStateIsByteUnchanged()
        {
            // The production composition (HeldToolRig.LateUpdate):
            //   offset = seatOffsetFromHand + mineSeatOffsetDelta * w
            //   rot    = Euler(seatEuler) * Euler(mineSeatEulerDelta * w)
            Vector3 seatOffset = MovementCameraScene.HeldAxeV4LocalOffsetFromHand;
            Vector3 seatEuler = MovementCameraScene.HeldAxeV4RelEuler;
            Vector3 dPos = MovementCameraScene.HeldToolMineSeatOffsetDelta;
            Vector3 dRot = MovementCameraScene.HeldToolMineSeatEulerDelta;

            Vector3 restOffset = seatOffset + dPos * 0f;
            Quaternion restRot = Quaternion.Euler(seatEuler) * Quaternion.Euler(dRot * 0f);

            Assert.AreEqual(seatOffset, restOffset,
                "at weight 0 the position delta adds Vector3.zero, so the approved one-handed seat OFFSET is exact.");
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.Euler(seatEuler), restRot), 1e-4f,
                "at weight 0 Euler(delta*0) is the identity quaternion, so the approved seat ROTATION is exact. " +
                "This seat is shared by all 15 baked held-weapon poses — a non-identity rest would move every one.");
        }

        [Test]
        public void AtFullWeight_TheSeatDeltaGenuinelyMovesTheHaft_TheControlForTheRestAssert()
        {
            Vector3 seatEuler = MovementCameraScene.HeldAxeV4RelEuler;
            Vector3 dPos = MovementCameraScene.HeldToolMineSeatOffsetDelta;
            Vector3 dRot = MovementCameraScene.HeldToolMineSeatEulerDelta;

            Assert.Greater(dPos.magnitude, 0.05f,
                "the shipped seat must actually SLIDE the haft (measured fit: -0.249, -0.393, -0.311 hand-local) — a " +
                "zero here means the round-2 fix ships inert and the Sponsor gets the identical broken build back.");
            float turned = Quaternion.Angle(Quaternion.Euler(seatEuler),
                                            Quaternion.Euler(seatEuler) * Quaternion.Euler(dRot));
            Assert.Greater(turned, 40f,
                $"the shipped seat must actually TURN the haft onto the hand line (got {turned:F1} deg); the measured " +
                "fit takes the tool from 90.0 deg off the hand line to 32.7 deg.");
        }

        // ==============================================================================================
        // 3 — THE GEOMETRY READ IS A REAL MEASUREMENT.
        // ==============================================================================================

        [Test]
        public void DistanceToSegment_MeasuresToTheSegment_AndReportsTheUnclampedPosition()
        {
            // A point beside the middle of the segment: distance is the perpendicular, u is 0.5.
            float d = TwoHandGripRead.DistanceToSegment(new Vector3(0.5f, 0.25f, 0f),
                                                        Vector3.zero, new Vector3(1f, 0f, 0f), out float u);
            Assert.AreEqual(0.25f, d, 1e-4f);
            Assert.AreEqual(0.5f, u, 1e-4f);

            // A point BEYOND the head end: the distance clamps to the endpoint, but u reports >1 so a hand that has
            // slid off the end of the haft is VISIBLE on the readout rather than silently reported as "on it".
            d = TwoHandGripRead.DistanceToSegment(new Vector3(1.5f, 0f, 0f),
                                                  Vector3.zero, new Vector3(1f, 0f, 0f), out u);
            Assert.AreEqual(0.5f, d, 1e-4f);
            Assert.Greater(u, 1f, "u must stay UNCLAMPED so an off-the-end hand is legible on the panel.");
        }

        [Test]
        public void AHandOnTheHaft_Passes_AndAHandOffIt_Fails()
        {
            // A synthetic rig: shoulders 1 unit apart (so shoulder-widths == world units), a haft along +X, and both
            // hands sitting ON it -> the anchor sentence is satisfied and the read must pass.
            Vector3 lArm = new Vector3(-0.5f, 0f, 0f), rArm = new Vector3(0.5f, 0f, 0f);
            Vector3 grip = new Vector3(-0.6f, 1f, 0f), head = new Vector3(0.9f, 1f, 0f);
            Vector3 lOn = new Vector3(-0.3f, 1f, 0f), rOn = new Vector3(0.4f, 1f, 0f);
            // ROUND 4: the LEFT criterion is the PALM CENTRE, so palms must be SUPPLIED — the 6-arg overload flags
            // palmMeasured=false and Pass() then fails CLOSED on purpose (a wrist figure scored against a palm cap is a
            // different, easier question). That fail-closed behaviour is asserted separately below.
            var on = TwoHandGripRead.Measure(lArm, rArm, lOn, rOn, grip, head, lOn, rOn, palmMeasured: true);
            Assert.IsTrue(on.valid);
            Assert.IsTrue(TwoHandGripRead.Pass(on),
                $"both hands on one haft IS a two-hand grip (L palm {on.leftPalmHaftSW:F3} / R wrist " +
                $"{on.rightHaftSW:F3} SW).");
            Assert.Less(on.toolVsHandLineDeg, 1f, "a haft through both hands lies along the line through them.");

            // Now lift ONLY the left hand well off the haft — the exact defect shape (the right hand still grips the
            // tool, the left grips air). It MUST fail, and it must fail on the LEFT cap.
            Vector3 lOff = new Vector3(-0.3f, 1.9f, 0f);
            var off = TwoHandGripRead.Measure(lArm, rArm, lOff, rOn, grip, head, lOff, rOn, palmMeasured: true);
            Assert.IsTrue(off.valid);
            Assert.Greater(off.leftPalmHaftSW, TwoHandGripRead.LeftHaftPassSW);
            Assert.IsFalse(TwoHandGripRead.Pass(off),
                "a left hand gripping air must FAIL — that is the Sponsor's reported defect, and a read that passes " +
                "it cannot be used as the gate for this fix.");
        }

        [Test]
        public void AnUNMEASURED_Palm_FailsClosed_NeverScoringTheWristAgainstThePalmCap()
        {
            // 86cay4282 round 4. The whole reason the round-3 gate was green on a hand 28 cm off the shaft is that the
            // criterion answered an easier question than the anchor sentence. The palm cap must therefore refuse to
            // score a WRIST figure: an unmeasured palm is "we do not know", and this project's cautionary case (the
            // pond lift->mound saga) is exactly a metric that stayed green on nonsense.
            Vector3 lArm = new Vector3(-0.5f, 0f, 0f), rArm = new Vector3(0.5f, 0f, 0f);
            Vector3 grip = new Vector3(-0.6f, 1f, 0f), head = new Vector3(0.9f, 1f, 0f);
            Vector3 lOn = new Vector3(-0.3f, 1f, 0f), rOn = new Vector3(0.4f, 1f, 0f);

            var wristOnly = TwoHandGripRead.Measure(lArm, rArm, lOn, rOn, grip, head);   // 6-arg = no palms
            Assert.IsTrue(wristOnly.valid, "the read itself is valid — the geometry is measurable");
            Assert.IsFalse(wristOnly.palmMeasured);
            Assert.IsFalse(TwoHandGripRead.Pass(wristOnly),
                "…yet it must NOT pass: the left criterion is palm-anchored, and silently accepting a wrist figure in " +
                "its place is how a cap loses the meaning this round just gave it. Same geometry WITH palms supplied " +
                "passes (asserted above), so this is a real fail-closed and not an unsatisfiable gate.");
        }

        [Test]
        public void TheLeftCap_IsDerivedFromTheMeshGeometry_NotFromWhatAFitAchieved()
        {
            // THE ROUND-4 CORRECTION, pinned. Round 3's left cap was 0.80 SW because that is what a constant seat could
            // ACHIEVE with headroom; the Sponsor's soak then found 0.80 SW is 36.6 cm, i.e. a hand a quarter of a metre
            // off the shaft passing. This asserts the cap is now the GEOMETRIC definition of contact, computed from the
            // two mesh-measured radii — so it cannot drift back into being calibrated against achievability.
            Assert.AreEqual((TwoHandGripRead.LeftHandRadiusM + TwoHandGripRead.HaftRadiusM) /
                            TwoHandGripRead.ReferenceShoulderWidthM,
                            TwoHandGripRead.LeftHaftPassSW, 1e-6f,
                "LeftHaftPassSW must BE (hand radius + haft radius) / shoulder width — the definition of the two solids " +
                "touching. If this ever becomes a hand-tuned literal again, the cap has stopped meaning 'touching'.");

            // …and it must be a real tightening, in units a human reads. NOT a tautology: both operands trace to
            // independently-measured mesh quantities, and the 0.80 is the literal round-3 value it replaces.
            const float Round3Cap = 0.80f;
            Assert.Less(TwoHandGripRead.LeftHaftPassSW, Round3Cap * 0.5f,
                $"the cap must TIGHTEN substantially: round 3 permitted {Round3Cap:F2} SW = " +
                $"{Round3Cap * TwoHandGripRead.ReferenceShoulderWidthM * 100f:F1} cm; round 4 permits " +
                $"{TwoHandGripRead.LeftHaftPassSW:F3} SW = " +
                $"{TwoHandGripRead.LeftHaftPassSW * TwoHandGripRead.ReferenceShoulderWidthM * 100f:F1} cm.");

            // The snug (median-radius) bound is reported alongside and must be strictly tighter than the graze bound
            // that gates — if they ever coincide, one of the two measurements has been lost.
            Assert.Less(TwoHandGripRead.LeftHaftSnugSW, TwoHandGripRead.LeftHaftPassSW,
                "the 'haft well inside the fist' bound (median hand radius) must be tighter than the 'grazing' bound " +
                "(max hand radius) that gates; the gate uses the generous one so jitter cannot red an approved build.");
        }

        [Test]
        public void AnUnmeasurableRig_IsNeverAPass()
        {
            // Degenerate shoulder span (a collapsed/unposed rig) and a zero-length haft (no mesh resolved). Both are
            // "we do not know", and "we do not know" must never read as "the grip is fine" — the metric-green-on-
            // nonsense guard that the pond lift->mound saga is this project's cautionary case for.
            var noShoulders = TwoHandGripRead.Measure(Vector3.zero, Vector3.zero, Vector3.one, Vector3.one,
                                                      Vector3.zero, Vector3.right);
            Assert.IsFalse(noShoulders.valid);
            Assert.IsFalse(TwoHandGripRead.Pass(noShoulders));

            var noHaft = TwoHandGripRead.Measure(new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                                                 Vector3.zero, Vector3.zero, Vector3.one, Vector3.one);
            Assert.IsFalse(noHaft.valid);
            Assert.IsFalse(TwoHandGripRead.Pass(noHaft));
        }

        // ==============================================================================================
        // 4 — THE THRESHOLDS BRACKET THE MEASUREMENT.
        // ==============================================================================================

        [Test]
        public void TheCaps_BracketTheRound4Measurement_AndTheRound3BuildNowREDS()
        {
            // ROUND 4 REVERSES THE ROUND-3 BRACKET, DELIBERATELY. Round 3 asserted the LEFT cap sat ABOVE its own
            // shipped worst frame (0.615 SW) so a re-dial could not red a build. That headroom is precisely what let a
            // 28 cm gap pass, and the Sponsor's soak caught it. The cap is now the geometric touching bound, which is
            // BELOW the round-3 build's worst frame — i.e. the build this round replaces now REDS. That is the point.
            Assert.Less(TwoHandGripRead.LeftHaftPassSW, ShippedWorstLeftSW,
                $"the LEFT cap ({TwoHandGripRead.LeftHaftPassSW:F3} SW = " +
                $"{TwoHandGripRead.LeftHaftPassSW * TwoHandGripRead.ReferenceShoulderWidthM * 100f:F1} cm) must now sit " +
                $"BELOW round 3's shipped worst frame ({ShippedWorstLeftSW:F3} SW = " +
                $"{ShippedWorstLeftSW * TwoHandGripRead.ReferenceShoulderWidthM * 100f:F1} cm). If it does not, the cap " +
                "still permits the defect the Sponsor reported.");
            Assert.Less(TwoHandGripRead.LeftHaftPassSW, PreFixWorstLeftSW,
                $"…and a fortiori below the pre-fix worst frame ({PreFixWorstLeftSW:F3} SW), so a reverted / inverted / " +
                "ungated delta cannot pass either.");

            // The ROUND-4 shipped measurement must fit UNDER the cap with real jitter margin, or the gate flaps red on
            // an approved build. Measured on the live rig: worst-frame PALM->haft 0.1075 m at the shipped shell.
            const float Round4WorstPalmM = 0.1075f;
            float capM = TwoHandGripRead.LeftHaftPassSW * TwoHandGripRead.ReferenceShoulderWidthM;
            Assert.Less(Round4WorstPalmM, capM,
                $"the shipped round-4 worst frame ({Round4WorstPalmM * 100f:F1} cm) must fit under the cap " +
                $"({capM * 100f:F1} cm).");
            Assert.Greater(capM - Round4WorstPalmM, 0.015f,
                $"…with at least 1.5 cm of margin (measured {(capM - Round4WorstPalmM) * 100f:F1} cm). A cap sitting " +
                "millimetres above the shipped measurement red-flaps on real frame-timing jitter in the 60 fps gate — " +
                "which is why the GRAZE bound (max hand radius) gates and the snug bound only reports.");

            Assert.Greater(TwoHandGripRead.RightHaftPassSW, ShippedWorstRightSW,
                $"the RIGHT cap ({TwoHandGripRead.RightHaftPassSW:F2}) must clear the shipped {ShippedWorstRightSW:F3} SW.");
            Assert.Greater(TwoHandGripRead.RightHaftPassSW, PreFixWorstRightSW,
                "the RIGHT cap is not a revert-detector (the right hand was already near its own haft pre-fix at " +
                $"{PreFixWorstRightSW:F3} SW) — it exists to catch a delta that pulls the haft OUT of the hand the " +
                "tool is physically seated in, which no left-hand cap can see.");
            // NOTE: round 3 asserted RIGHT < LEFT ("the right cap must be tighter"). That comparison is now MEANINGLESS
            // and is deliberately NOT re-asserted: the two caps no longer answer the same question or use the same
            // anchor. The right cap is a WRIST tolerance chosen to catch the haft leaving the hand it is seated in; the
            // left cap is a PALM-anchored geometric contact bound. Round 4's left cap happens to be the tighter of the
            // two (0.293 vs 0.300) precisely because it is derived rather than chosen, and asserting an ordering between
            // two different quantities would just re-introduce a number nobody can justify.
        }

        // ==============================================================================================
        // 5 — THE SHIP SOURCE.
        // ==============================================================================================

        [Test]
        public void ShippedSeatDelta_ComesFromMovementCameraScene_NotTheRuntimeFieldDefault()
        {
            var go = new GameObject("MineSeatDefaultProbe");
            try
            {
                var rig = go.AddComponent<HeldToolRig>();
                // AttachHeroAxeToHand writes HeldToolMineSeat*Delta onto the component, and THAT is what serializes
                // into Boot.unity ([[unity-procedural-committed-assets-go-stale]] — the build ships the committed
                // scene). Pin them equal so a drifting runtime default cannot become the shipped value unnoticed,
                // the same convention runLowerEuler/ArmRunLowerEuler and mineDeGripEuler/ArmMineDeGripEuler follow.
                Assert.AreEqual(MovementCameraScene.HeldToolMineSeatOffsetDelta, rig.mineSeatOffsetDelta,
                    "HeldToolRig.mineSeatOffsetDelta (the runtime fallback) must stay in sync with " +
                    "MovementCameraScene.HeldToolMineSeatOffsetDelta (the authoritative bake source).");
                Assert.AreEqual(MovementCameraScene.HeldToolMineSeatEulerDelta, rig.mineSeatEulerDelta,
                    "HeldToolRig.mineSeatEulerDelta must stay in sync with the MovementCameraScene bake source.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MirroredConstants_MatchTheShipSource()
        {
            // MineSeatPlayModeTests lives in the all-platform PlayTests asmdef, which deliberately does NOT reference
            // the Editor asmdef, so it mirrors these MovementCameraScene constants as literals. That mirror is a
            // silent-drift hole: a re-bake would leave the PlayMode test measuring a fiction while staying green.
            // This closes it from the EditMode side (which CAN see both) — change a bake constant and THIS reds,
            // naming the file to update. The pre-existing MineDeGripPlayModeTests mirror is pinned here too.
            Assert.AreEqual(new Vector3(-0.2491f, -0.3928f, -0.3109f), MovementCameraScene.HeldToolMineSeatOffsetDelta,
                "MineSeatPlayModeTests.MineSeatOffsetDelta mirror is stale");
            Assert.AreEqual(new Vector3(-24.7f, 70.0f, 23.7f), MovementCameraScene.HeldToolMineSeatEulerDelta,
                "MineSeatPlayModeTests.MineSeatEulerDelta mirror is stale");
            Assert.AreEqual(new Vector3(-22.0f, 250.0f, -30.0f), MovementCameraScene.CastawayV4RightWristEuler,
                "MineSeatPlayModeTests.RightWristEuler mirror is stale - LOAD-BEARING: the seat reads hand.rotation, " +
                "so a wrist change silently re-aims the haft");
            Assert.AreEqual(new Vector3(-21.8f, 282.6f, 3.7f), MovementCameraScene.CastawayV4LeftWristEuler,
                "MineSeatPlayModeTests.LeftWristEuler mirror is stale");
            Assert.AreEqual(new Vector3(0.0182f, 0.0415f, 0.0492f), MovementCameraScene.HeldAxeV4LocalOffsetFromHand,
                "MineSeatPlayModeTests.SeatOffset mirror is stale");
            Assert.AreEqual(new Vector3(-48.9f, -125.0f, -106.3f), MovementCameraScene.HeldAxeV4RelEuler,
                "MineSeatPlayModeTests.SeatEuler mirror is stale");
            Assert.AreEqual(new Vector3(-5f, -22f, 0f), MovementCameraScene.CastawayV4RightArmEuler,
                "the CarryRightEuler mirror (both PlayMode suites) is stale");
            Assert.AreEqual(new Vector3(-5f, 22f, 0f), MovementCameraScene.CastawayV4LeftArmEuler,
                "the CarryLeftEuler mirror (both PlayMode suites) is stale");
            Assert.AreEqual(0.45f, MovementCameraScene.HeldAxeLocalScaleUniform, 1e-4f,
                "MineSeatPlayModeTests.HeldScaleUniform mirror is stale");
            Assert.AreEqual(0f, MovementCameraScene.HeldAxeGripShiftY, 1e-4f,
                "MineSeatPlayModeTests.GripShiftY mirror is stale");
        }

        // ==============================================================================================
        // 6 — ONE GATE, ONE EASE (the arm offset and the seat offset can never move out of step).
        // ==============================================================================================

        [Test]
        public void TheSeatWeight_UsesTheSameProductionEasePolicy_AsTheArmOffset()
        {
            // HeldToolRig steps its weight through CastawayArmPose.NextMineDeGripWeight — the SAME function, not a
            // mirrored copy. This pins the shared rate contract: at equal rates the two weights are identical frame
            // by frame, so the haft can never ease onto the hands out of step with the arm.
            var go = new GameObject("MineSeatRateProbe");
            try
            {
                var rig = go.AddComponent<HeldToolRig>();
                var pose = go.AddComponent<CastawayArmPose>();
                Assert.AreEqual(pose.mineDeGripBlendRate, rig.mineSeatBlendRate, 1e-4f,
                    "the seat blend rate must match the arm de-grip blend rate — one gate, one ease.");

                float wArm = 0f, wSeat = 0f;
                for (int i = 0; i < 30; i++)
                {
                    wArm = CastawayArmPose.NextMineDeGripWeight(wArm, true, pose.mineDeGripBlendRate, Dt);
                    wSeat = CastawayArmPose.NextMineDeGripWeight(wSeat, true, rig.mineSeatBlendRate, Dt);
                    Assert.AreEqual(wArm, wSeat, 1e-6f, "the two weights must track identically, frame by frame");
                }
                Assert.Greater(wSeat, 0.9f, "and must reach engagement inside the swing's wind-up (~0.25 s to 95%)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheSeatWeight_RestsAtZero_OutsideTheMineSwing()
        {
            float w = 1f;
            for (int i = 0; i < 120; i++) w = CastawayArmPose.NextMineDeGripWeight(w, false, 12f, Dt);
            Assert.Less(w, 1e-4f,
                "outside the mine swing the seat weight must fall to 0 so the approved one-handed seat is restored " +
                "— an offset that lingers would move the carry seat the Sponsor locked across five soak rounds.");
        }

        // ==============================================================================================
        // The FRONT-VIEW SNAP framing aid (same ticket — 'framing must not be the Sponsor's problem').
        // ==============================================================================================

        [Test]
        public void FrontSnapYaw_PutsTheCameraInFrontOfTheCharacter_ForEveryFacing()
        {
            // The camera looks ALONG its own yaw heading, so to see the character's FRONT it must look along the
            // OPPOSITE of the facing. A sign error here yields a shot of the character's BACK — which would look
            // like a plausible capture and silently make the whole framing aid useless.
            foreach (float facingDeg in new[] { 0f, 37f, 90f, 180f, 270f, -125f })
            {
                Vector3 fwd = Quaternion.Euler(0f, facingDeg, 0f) * Vector3.forward;
                float yaw = OrbitCamera.FrontSnapYaw(fwd);
                Vector3 camLook = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Assert.Less(Vector3.Dot(camLook, fwd), -0.999f,
                    $"facing {facingDeg} deg: the snapped camera must look INTO the character's face (dot -1), got " +
                    $"dot {Vector3.Dot(camLook, fwd):F3} at yaw {yaw:F1}.");
            }
        }

        [Test]
        public void FrontSnapYaw_ToleratesADegenerateFacing()
        {
            Assert.AreEqual(0f, OrbitCamera.FrontSnapYaw(Vector3.up), 1e-4f,
                "a straight-up facing has no horizontal component; the snap must fall back rather than NaN the yaw.");
        }

        [Test]
        public void FrontSnap_FramesCloserThanTheGameplayZoomFloor_OrItCannotResolveTheHands()
        {
            var go = new GameObject("FrontSnapProbe");
            try
            {
                var cam = go.AddComponent<OrbitCamera>();
                Assert.Less(cam.frontSnapDistance, cam.minDistance,
                    $"the snap distance ({cam.frontSnapDistance}) must be CLOSER than the gameplay zoom floor " +
                    $"({cam.minDistance}) — that floor is exactly why the default frame renders the castaway at " +
                    "~55x95 px and cannot resolve which hand is on the haft.");
                Assert.AreEqual(cam.minDistance, cam.ZoomFloor(), 1e-4f,
                    "un-snapped, the zoom floor is the untouched gameplay floor.");
                cam.ToggleFrontSnap();
                Assert.IsTrue(cam.FrontSnapActive);
                Assert.AreEqual(cam.frontSnapDistance, cam.ZoomFloor(), 1e-4f,
                    "snapped, the floor drops to the snap distance so a stray wheel tick cannot yank the camera back " +
                    "out to 6u mid-judgement.");
                Assert.LessOrEqual(cam.frontSnapPitch, 20f,
                    "the snap must be near LEVEL — hand-on-haft is a lateral read and a high pitch foreshortens " +
                    "exactly the geometry being judged (the pond top-down lesson).");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FrontSnap_IsAToggle_ThatRestoresThePreviousOrbitExactly()
        {
            var go = new GameObject("FrontSnapRestoreProbe");
            try
            {
                var cam = go.AddComponent<OrbitCamera>();
                cam.SetYaw(123f);
                cam.SetPitch(48f);
                cam.SetDistance(19f);
                float yaw0 = cam.Yaw, pitch0 = cam.Pitch, dist0 = cam.Distance;

                cam.ToggleFrontSnap();
                Assert.AreNotEqual(dist0, cam.Distance, "the snap must actually change the framing");
                cam.ToggleFrontSnap();

                Assert.IsFalse(cam.FrontSnapActive);
                Assert.AreEqual(yaw0, cam.Yaw, 1e-3f, "releasing the snap must restore the Sponsor's own orbit yaw");
                Assert.AreEqual(pitch0, cam.Pitch, 1e-3f, "…its pitch");
                Assert.AreEqual(dist0, cam.Distance, 1e-3f,
                    "…and its zoom. A framing AID that leaves the framing changed behind his back is a new annoyance, " +
                    "not a fix.");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
