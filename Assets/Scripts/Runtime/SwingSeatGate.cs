using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// 86cayp0ay — THE PASS CRITERIA for a SWING-TIME held-weapon SEAT read, as pure statics so the shipped-build
    /// gate and the EditMode suite score identical inputs identically (the same one-seam discipline
    /// <see cref="TwoHandGripRead"/> established: a panel, a gate and a test must never disagree about what passes).
    ///
    /// TWO criteria, and the FIRST one is the reason this file exists at all.
    ///
    /// =============================================================================================================
    /// 1. POSE LIVENESS — "did the skeleton ACTUALLY POSE during the window I scored?"
    ///
    /// MEASURED, not hypothesised. The SAME shipped exe, the SAME -verifySwings flag, differing ONLY in launch mode
    /// (Far-Horizon-drew-conc-b-wt Build/Windows/FarHorizon.exe @ 90d024b, 2026-08-01):
    ///
    ///     -batchmode          LIVE peak torso tilt   2.6 deg at +0.00s | left palm-to-haft 0.844 SW = 39.5 cm
    ///     -screen-fullscreen 0  LIVE peak torso tilt 42.2 deg at +1.97s | left palm-to-haft 0.239 SW = 10.6 cm
    ///
    /// Headless, the Animator STATE MACHINE advances normally (per-class routing succeeded 5/5, the
    /// AnyState->AttackPickaxe crossfade was detected and exited, the eased seat weight reached 1.00 and released at
    /// its authored rate) and <c>Time.deltaTime</c> is healthy (~0.00054 s, ~1850 fps) — but the BONES do not take the
    /// swing pose. A 2.6 deg peak torso tilt is the idle stance. So the grip pass scored 4696 frames of a NON-SWING
    /// and produced a confident 39.5 cm verdict from them.
    ///
    /// THAT is the vacuity shape this criterion kills, and note which direction it runs: the danger here is NOT the
    /// familiar "iterate nothing and pass" (an empty loop over an unresolved seat). It is the INVERSE — iterate
    /// thousands of frames of a pose that never happened and emit a confident NUMBER. Both are the same defect at
    /// root: the gate could not tell "I measured the thing" from "I measured something else", so it reported anyway.
    /// A grip figure is meaningless unless the swing posed, therefore the gate must PROVE the swing posed BEFORE it
    /// is allowed to report a grip verdict at all. "We could not measure it" must never render as "it is fine".
    ///
    /// THE FLOOR IS DERIVED, NOT PICKED. <see cref="TorsoTiltPosedFloorDeg"/> is the GEOMETRIC MEAN of the two
    /// measured readings above, so it sits the same MULTIPLICATIVE distance (4.03x) above the not-posing reading as
    /// it does below the posing one — the placement that maximises margin to both measured states at once. A round
    /// number picked between them would be an invented threshold; this one is a function of two measurements and
    /// moves if either is re-measured.
    ///
    /// =============================================================================================================
    /// 2. SEAT — "is the haft still passing through the hand it is SEATED in, at swing frames?"
    ///
    /// The held tool is seated on the RIGHT hand (<see cref="HeldToolRig.ApplySeat"/>:
    /// <c>position = hand.position + hand.rotation * offset</c>). The question a swing asks that a REST pose cannot
    /// is whether that stays true while the arm pose (order 50) and the WRIST euler (order 65) are both moving the
    /// hand the seat reads. The measurable form is the RIGHT hand's distance to the haft LINE, shoulder-width
    /// normalised (scale-immune, and directly comparable with every other figure in this codebase).
    ///
    /// This criterion is scored on the LIVE runtime skeleton, so orders 50 / 60 / 65 / 70 / 100 / 110 are all included
    /// by construction — there is no re-implementation of the chain to leave order 65 out of (the incident that made
    /// two self-authored instruments agree at 0.615 SW while the shipped exe measured 1.220;
    /// procedural-animation-verbs.md).
    ///
    /// The bound is the MEASURED APPROVED seat distance PLUS one haft radius of allowed drift — never a round number
    /// chosen for headroom. Note the direction: <c>LeftHaftPassSW = 0.80</c> failed because a cap set from what a fit
    /// could ACHIEVE permitted 36.6 cm and printed PASS for three rounds over a hand gripping air. A displacement
    /// bound is not vulnerable that way — it does not ask "is this good enough", it asks "is this still where the
    /// approved seat put it", and the reference is a measurement of the approved build rather than of an attempt.
    /// </summary>
    public static class SwingSeatGate
    {
        /// <summary>Every number this gate prints is formatted INVARIANT. The verdict line is the evidence
        /// artifact a reviewer quotes and a script may grep, so it must not change shape with the machine locale —
        /// this project's own runner is a comma-decimal locale, and the first version of these messages emitted
        /// "phase 0,90". Sibling of the ASCII-only rule PR #399 landed for NUnit failure messages.</summary>
        private static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        // ==========================================================================================================
        // POSE LIVENESS
        // ==========================================================================================================

        /// <summary>Peak torso tilt off vertical measured on a run where the swing genuinely posed (windowed launch,
        /// -verifySwings, pickaxe mine swing). MEASURED — see the class summary for the run.</summary>
        public const float MeasuredPosedTorsoTiltDeg = 42.2f;

        /// <summary>Peak torso tilt off vertical measured on a run where the swing did NOT pose (headless launch of
        /// the SAME exe with the SAME flag). This is the idle stance, and it is what a non-posing window reads.
        /// MEASURED — see the class summary.</summary>
        public const float MeasuredNotPosedTorsoTiltDeg = 2.6f;

        /// <summary>The liveness floor: the GEOMETRIC MEAN of the two measured readings above (~10.47 deg), i.e. the
        /// point that is the same multiplicative distance (4.03x) from each. Not a chosen round number — a function
        /// of two measurements. Re-derive it if either is re-measured; do NOT hand-edit it.</summary>
        public static readonly float TorsoTiltPosedFloorDeg =
            Mathf.Sqrt(MeasuredNotPosedTorsoTiltDeg * MeasuredPosedTorsoTiltDeg);

        /// <summary>Minimum number of DISTINCT swing phases that must each contribute at least one scored sample.
        /// 3 = wind-up / mid / impact. WHY a phase count and not a frame count: the hand line wanders 21.0 deg mean /
        /// 36.6 deg max about its own mean through a swing (procedural-animation-verbs.md), so thousands of samples
        /// clustered in one phase can all land in a good moment of a bad swing. Frames are cheap; PHASES are the
        /// thing that has to be covered.</summary>
        public const int RequiredPhases = 3;

        /// <summary>
        /// Did the window this gate scored actually contain a posed swing? FAILS CLOSED on every "we do not know":
        /// no scored samples, fewer than <see cref="RequiredPhases"/> phases covered, or a NaN tilt.
        /// <paramref name="why"/> always NAMES the offending quantity so a RED is diagnosable from the line alone.
        /// </summary>
        public static bool Posed(int scoredSamples, int phasesCovered, float peakTorsoTiltDeg, out string why)
        {
            if (scoredSamples <= 0)
            {
                why = "SWING NEVER SCORED - 0 samples were scored in the measured window, so there is no grip " +
                      "reading to judge. An empty window is NOT a pass.";
                return false;
            }
            if (float.IsNaN(peakTorsoTiltDeg) || float.IsInfinity(peakTorsoTiltDeg))
            {
                why = "SWING POSE UNMEASURABLE - peak torso tilt came back " + peakTorsoTiltDeg +
                      " (the hips/head bones did not resolve). Unmeasured is NOT a pass.";
                return false;
            }
            if (phasesCovered < RequiredPhases)
            {
                why = "SWING PHASE COVERAGE TOO THIN - " + phasesCovered + " of " + RequiredPhases +
                      " phases (wind-up/mid/impact) carried a scored sample. Samples clustered in one phase can all " +
                      "land in a good moment of a bad swing.";
                return false;
            }
            if (peakTorsoTiltDeg < TorsoTiltPosedFloorDeg)
            {
                why = "SWING NEVER POSED - peak torso tilt " + peakTorsoTiltDeg.ToString("F1", Inv) + " deg is below the " +
                      TorsoTiltPosedFloorDeg.ToString("F1", Inv) + " deg liveness floor, i.e. the skeleton held its idle " +
                      "stance for the whole window (a headless launch of this exe measures " +
                      MeasuredNotPosedTorsoTiltDeg.ToString("F1", Inv) + " deg here; a windowed one measures " +
                      MeasuredPosedTorsoTiltDeg.ToString("F1", Inv) + " deg). Every grip figure from this window is a " +
                      "reading of the IDLE pose and must NOT be reported as a swing verdict.";
                return false;
            }
            why = "swing posed (peak torso tilt " + peakTorsoTiltDeg.ToString("F1", Inv) + " deg >= " +
                  TorsoTiltPosedFloorDeg.ToString("F1", Inv) + " deg floor, " + phasesCovered + "/" + RequiredPhases +
                  " phases covered over " + scoredSamples + " scored samples)";
            return true;
        }

        // ==========================================================================================================
        // SEAT
        // ==========================================================================================================

        /// <summary>The WORST right-hand wrist-to-haft distance MEASURED over a real chop swing in the shipped exe,
        /// WOOD axe selected through the production belt seam, on the APPROVED seat: 0.4027 SW = 17.9 cm at swing
        /// phase 0.90 (windowed -verifySwings, 119 scored samples, 3/3 phases, peak torso tilt 43.3 deg; the run is
        /// named in the PR body). This is the ACHIEVED value the bound is anchored to. It is NOT a tolerance and must
        /// never be edited to make a build pass — if the approved seat is re-dialled, RE-MEASURE it.</summary>
        public const float MeasuredWorstChopRightHaftSW = 0.4027f;

        /// <summary>How far the haft LINE may drift off its approved distance to the wrist before this reds: the
        /// HAFT'S OWN CROSS-SECTION RADIUS, shoulder-width normalised (0.0448 m / 0.4580 m = 0.0978 SW = 4.5 cm; both
        /// measured constants come from <see cref="TwoHandGripRead"/>, where they were taken off the shipped meshes).
        /// A geometric quantity, not a chosen headroom: a haft that has slid more than its own radius off where the
        /// approved seat puts it is no longer in the same place in the hand.</summary>
        public static readonly float AllowedSeatDriftSW =
            TwoHandGripRead.HaftRadiusM / TwoHandGripRead.ReferenceShoulderWidthM;

        /// <summary>Pass bound for the RIGHT hand's wrist-to-haft distance at swing frames, shoulder-widths.
        /// DERIVED: <see cref="MeasuredWorstChopRightHaftSW"/> + <see cref="AllowedSeatDriftSW"/> = 0.5005 SW
        /// (~22.2 cm at the reference shoulder width). Deliberately NOT a round number, and deliberately NOT reused
        /// from <see cref="TwoHandGripRead.RightHaftPassSW"/> (0.30 SW — the TWO-HAND pass's own cap, never anchored
        /// to a chop-swing measurement, and it would red the approved one-handed seat outright).
        ///
        /// ⚠ WHAT THIS BOUND CLAIMS, AND WHAT IT DOES NOT. It is a SEAT-DISPLACEMENT bound: "the haft line still sits
        /// where the approved seat puts it relative to the hand bone, throughout the swing". It is NOT a claim that
        /// the hand is TOUCHING the haft. The anchor is <c>mixamorig:RightHand</c>, the WRIST joint, and on this
        /// fist-hand rig the approved seat legitimately measures 17.9 cm from it — the grip point sits well forward
        /// of the wrist (the knuckle is 11.1 cm out from the wrist on this rig, <see cref="TwoHandGripRead"/>
        /// <c>:113</c>/<c>:134</c>, so the palm centre — <c>midpoint(hand, knuckle)</c> — is 5.6 cm out;
        /// procedural-animation-verbs.md §"a PALM is not a WRIST" carries the 5.6 cm half of that pair).
        /// A TOUCHING criterion needs the palm anchor and a mesh-derived contact bound; that is what
        /// <see cref="TwoHandGripRead.LeftHaftPassSW"/> is, for the hand that has no seat of its own. Do not read
        /// this number as the same kind of claim.</summary>
        public static readonly float ChopRightHaftPassSW =
            MeasuredWorstChopRightHaftSW + AllowedSeatDriftSW;

        /// <summary>
        /// Is the haft still in the hand it is seated in? <paramref name="why"/> NAMES the measured value, its
        /// centimetre conversion and the swing phase it was sampled at, so a RED reads as a defect report rather
        /// than as a boolean.
        /// </summary>
        public static bool SeatOk(float worstRightHaftSW, float shoulderWidthM, float atNormalizedPhase,
                                  out string why)
        {
            if (worstRightHaftSW < 0f || float.IsNaN(worstRightHaftSW))
            {
                why = "SEAT UNMEASURED - no valid right-hand-to-haft reading was taken (a degenerate shoulder span " +
                      "or an unresolvable haft segment). Unmeasured is NOT a pass.";
                return false;
            }
            bool ok = worstRightHaftSW <= ChopRightHaftPassSW;
            why = (ok ? "seat held" : "SEAT DRIFTED OFF THE HAND") + " - worst right-hand-to-haft " +
                  worstRightHaftSW.ToString("F4", Inv) + " SW = " + (worstRightHaftSW * shoulderWidthM * 100f).ToString("F1", Inv) +
                  " cm at swing phase " + atNormalizedPhase.ToString("F2", Inv) + " (bound " +
                  ChopRightHaftPassSW.ToString("F4", Inv) + " SW = " +
                  (ChopRightHaftPassSW * shoulderWidthM * 100f).ToString("F1", Inv) + " cm = the measured approved seat " +
                  MeasuredWorstChopRightHaftSW.ToString("F4", Inv) + " SW + one haft radius " +
                  AllowedSeatDriftSW.ToString("F4", Inv) + " SW of allowed drift)";
            return ok;
        }

        // ==========================================================================================================
        // ALONG-HAFT — the component the perpendicular distance THROWS AWAY.
        //
        // WHY IT IS GATED, stated at the strength the evidence actually supports.
        //
        // (1) GEOMETRIC, and this is the load-bearing argument: a perpendicular distance-to-LINE cannot see the tool
        //     sliding along its own axis. Translating the haft parallel to itself maps the line onto itself, so the
        //     perpendicular distance is unchanged BY CONSTRUCTION - not as an empirical observation that might not
        //     replicate, but as a property of the metric. This is the failure procedural-animation-verbs.md
        //     documents ("A distance-to-LINE metric leaves the along-line position unscored - and that is where the
        //     next defect hides"), and the rule it states is the fix: compute the discarded component, surface it,
        //     and decide explicitly whether to gate it. It is gated.
        //
        // (2) ⚠ AN EARLIER VERSION OF THIS COMMENT CITED A MEASUREMENT THAT DOES NOT SUPPORT THE CLAIM, and the
        //     correction is kept here because the mistake is instructive. It read: "-swingSeatFaultCm 30 left the
        //     perpendicular reading at 0.4027 SW - byte-identical to the clean run", offered as proof that the
        //     perpendicular axis is blind. It was not. That reading came from the FIRST injector, which wrote
        //     HeldToolRig.seatOffsetFromHand - the field HeldAxeRig.ApplySeat stomps every LateUpdate (see
        //     VerifySeatFaultTookEffect). The seat never moved, so BOTH axes were byte-identical; the run measured
        //     the injector's own inertness, not the metric's blind side. With the injection writing the field the
        //     rig CONSUMES, a 30 cm hand-local +X fault moves BOTH axes (measured 2026-08-01, same exe, one flag
        //     apart): perpendicular 0.4027 -> 0.7172 SW, along-haft u 0.2004 -> 0.0107. So that fault is NOT an
        //     along-only control and must never be quoted as one.
        //
        // (3) WHAT ACTUALLY DEMONSTRATES THIS LEG IS LOAD-BEARING is mutation M9 in the demonstrated-RED matrix:
        //     un-gating it in Verdict (`seat && along` -> `seat && (along || true)`) is caught by
        //     SwingSeatGateTests.Verdict_RedsWhenOnlyTheAlongHaftLegFails_86cayp0ay. An along-ONLY shipped-build
        //     control would need the fault injected along the haft AXIS rather than along hand-local +X; that is
        //     not built, and this comment does not pretend it is.
        // ==========================================================================================================

        /// <summary>Along-haft position of the hand on the APPROVED seat, MEASURED over a real chop swing
        /// (0 = butt/grip end, 1 = head end). The run is named in the PR body.</summary>
        public const float MeasuredApprovedChopUMin = 0.2004f;

        /// <summary>Upper end of the same measured range.</summary>
        public const float MeasuredApprovedChopUMax = 0.2004f;

        /// <summary>
        /// Has the hand slid along the haft, away from where the approved seat puts it? The allowed slide is ONE
        /// HAFT RADIUS expressed in u (<paramref name="allowedDriftU"/> = haft radius / live haft length) — the same
        /// geometric quantity the perpendicular bound allows, applied to the other axis. A hand that has moved more
        /// than the stick's own thickness along the stick is not holding it in the same place.
        /// </summary>
        public static bool AlongOk(float minU, float maxU, float allowedDriftU, out string why)
        {
            if (float.IsNaN(minU) || float.IsNaN(maxU) || minU > maxU || float.IsNaN(allowedDriftU))
            {
                why = "ALONG-HAFT UNMEASURED - no valid along-haft reading was taken (u range " + minU + ".." + maxU +
                      ", allowed drift " + allowedDriftU + "). Unmeasured is NOT a pass.";
                return false;
            }
            if (float.IsNaN(MeasuredApprovedChopUMin) || float.IsNaN(MeasuredApprovedChopUMax))
            {
                why = "ALONG-HAFT BOUND NOT YET ANCHORED - the approved along-haft position has not been measured, " +
                      "so there is nothing to compare against. FAILS CLOSED: an unanchored bound must never green.";
                return false;
            }
            float lo = MeasuredApprovedChopUMin - allowedDriftU;
            float hi = MeasuredApprovedChopUMax + allowedDriftU;
            bool ok = minU >= lo && maxU <= hi;
            why = (ok ? "grip position held" : "GRIP SLID ALONG THE HAFT") + " - hand at u " +
                  minU.ToString("F4", Inv) + ".." + maxU.ToString("F4", Inv) + " against the measured approved " +
                  MeasuredApprovedChopUMin.ToString("F4", Inv) + ".." + MeasuredApprovedChopUMax.ToString("F4", Inv) +
                  " +/- one haft radius (" + allowedDriftU.ToString("F4", Inv) + " u), i.e. the window " +
                  lo.ToString("F4", Inv) + ".." + hi.ToString("F4", Inv) +
                  " (0 = BUTT/grip end, 1 = HEAD end; outside 0..1 means the hand is off the end of the haft)";
            return ok;
        }

        /// <summary>
        /// The composed verdict. LIVENESS IS A PRECONDITION, not a co-equal term: when the swing did not pose, the
        /// seat figures are readings of the idle stance and this returns false with the LIVENESS reason, never with a
        /// seat number that would read as a real measurement of a real swing.
        ///
        /// The two seat legs are BOTH required and they are INDEPENDENT axes: perpendicular distance answers "is the
        /// haft still the same distance from the hand", along-haft answers "is the hand still at the same place on
        /// the stick". A 30 cm slide is invisible to the first and obvious to the second; a 30 cm lift is the
        /// reverse. Either alone is a gate with a documented blind side.
        /// </summary>
        public static bool Verdict(int scoredSamples, int phasesCovered, float peakTorsoTiltDeg,
                                   float worstRightHaftSW, float shoulderWidthM, float atNormalizedPhase,
                                   float minU, float maxU, float allowedDriftU,
                                   out string why)
        {
            if (!Posed(scoredSamples, phasesCovered, peakTorsoTiltDeg, out why)) return false;
            string posedWhy = why;
            bool seat = SeatOk(worstRightHaftSW, shoulderWidthM, atNormalizedPhase, out string seatWhy);
            bool along = AlongOk(minU, maxU, allowedDriftU, out string alongWhy);
            why = posedWhy + "; " + seatWhy + "; " + alongWhy;
            return seat && along;
        }

        /// <summary>Which of <see cref="RequiredPhases"/> equal buckets a normalized swing position falls in.
        /// Clamped, so a normalizedTime that has run past 1 (a looping or overshooting state) still lands in the
        /// last bucket rather than off the end.</summary>
        public static int PhaseBucket(float normalized)
            => Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(normalized) * RequiredPhases), 0, RequiredPhases - 1);
    }
}
