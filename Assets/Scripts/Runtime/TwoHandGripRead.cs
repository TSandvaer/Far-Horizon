using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// 86cay4282 round 2 — THE ONE measurement of "does the held tool read as a TWO-HAND grip", shared by every
    /// consumer so a panel, a gate and a test can never disagree about what passes.
    ///
    /// THE REAL-WORLD ANCHOR (lowpoly-quality.md §0). A two-handed grip means ONE HAFT PASSING THROUGH BOTH HANDS:
    /// a person holding a pickaxe two-handed has both palms closed around the same stick, so the stick's LINE runs
    /// through both of them. That sentence — not a byte, a colour or a seed — is what the build has to satisfy. The
    /// measurable form of it is each hand's distance to the haft LINE: near zero for both = one stick through both
    /// hands; large for one = that hand is gripping air, which is exactly the "swinging with both hands but the axe
    /// is only in the right" defect the Sponsor reported.
    ///
    /// WHY THIS QUANTITY AND NOT HAND SEPARATION. Round 1 measured hand SEPARATION and treated the hands being close
    /// as the defect — then the Sponsor reversed the premise ("we need to position the axe for a two hand grip"), so
    /// close hands are CORRECT and separation is no longer a defect signal at all: it says nothing about where the
    /// haft is. Distance-to-haft is the quantity the reversed goal is defined by, and it is the one drawn on the F9
    /// panel + asserted by the shipped-build gate.
    ///
    /// SCALE-IMMUNE: every distance is normalised by SHOULDER WIDTH (the left-to-right upper-arm span), so the same
    /// thresholds hold regardless of avatar scale, hero version or clip breathing — the same convention
    /// AttackClipPoseDiag measures in, so an editor figure and a runtime figure are directly comparable.
    /// </summary>
    public static class TwoHandGripRead
    {
        // ==========================================================================================================
        // THRESHOLDS — set FROM MEASUREMENT (AttackClipPoseDiag MINE-SEAT FIT pass on the live rig, 61 samples of
        // the shipped repaired pickaxe clip; full table in the PR body):
        //
        //   pre-fix (zero delta)      lHaft mean 1.277  MAX 1.476 SW | rHaft mean 0.166 MAX 0.179 SW | 90.0 deg off
        //   SHIPPED (refined fit)     lHaft mean 0.445  MAX 0.615 SW | rHaft mean 0.000 MAX 0.000 SW | 32.7 deg off
        //
        // Both rows are WRIST-INCLUSIVE: CastawayHandPose (order 65) composes the wrist offset onto the hand bones
        // between the arm pose (50) and the seat (100), and the seat reads hand.rotation, so a fit measured without
        // it aims the haft at a hand a quarter-turn away. An earlier pass omitted it and the shipped gate measured
        // 1.220 SW against a predicted 0.611.
        //
        // ⚠ ROUND 4 SUPERSEDES THE LEFT CAP THIS BLOCK DESCRIBED. Round 3 set the LEFT cap "deliberately LOOSE"
        // (0.80 SW vs the then-shipped 0.615) so jitter or a Sponsor re-dial could not red a build. The Sponsor's soak
        // then found what no gate could see: 0.80 SW is 36.6 cm, so a hand a QUARTER OF A METRE off the shaft passed.
        // A cap set from what a fit ACHIEVES cannot catch a fit that achieves too little. The left cap is now derived
        // from the geometric definition of touching — see the round-4 block immediately below. The RIGHT cap stays
        // 0.30 SW against the WRIST, unchanged: the right hand is the tool's REAL physical grip, a right hand off its
        // own haft is a worse defect than a phantom left hand slightly off it, and the right-hand grip is out of
        // round-4 scope.
        //
        // The round-3 residual diagnosis stands and is WHY round 4 is a per-frame solve: the mine clip's own hand-line
        // direction spreads 21.0 deg mean / 36.6 deg max about its mean, and ONE CONSTANT seat can only match the
        // mean — the residual IS the wander. Round 3 recorded that removing it "would need a per-frame solve (IK),
        // which is out of scope"; round 4 is that solve (CastawayLeftArmHaftIk).
        // ==========================================================================================================

        // ==========================================================================================================
        // 86cay4282 ROUND 4 — THE LEFT CAP, RE-DERIVED FROM WHAT *TOUCHING* MEANS.
        //
        // The Sponsor, soaking round 3, verbatim: "R/V only manipulates the right hand, which is great, but what about
        // the left hand? its not even touching the shaft". He is right, and the round-3 numbers prove it: at the
        // measured mean shoulder width 0.4580 m the shipped left hand sat 0.445 SW = 20.4 cm mean / 0.615 SW = 28.2 cm
        // worst off the haft — while the old cap of 0.80 SW PERMITTED 36.6 cm. That cap was calibrated from what a
        // static seat could ACHIEVE, not from what "one haft passing through both hands" MEANS, so the gate was green
        // on a hand gripping air by a quarter of a metre. A cap derived from the achievable cannot ever catch this.
        //
        // THE DEFINITION, and it is geometric: two solids TOUCH when the distance between their axes is at most the sum
        // of their radii. Both radii are MEASURED OFF THE SHIPPED MESHES (AttackClipPoseDiag `[hand-mesh]`, stone
        // pickaxe + castaway v4):
        //   • the bare haft's cross-section radius = 0.0526 of the haft length x 0.8516 m  => 0.0448 m (4.5 cm)
        //   • the LEFT HAND mesh's cross-section radius about the wrist->knuckle axis, over the 164 vertices
        //     dominant-weighted to mixamorig:LeftHand => MEDIAN 0.0658 m (6.6 cm), MAX 0.0894 m (8.9 cm)
        //
        // The cap uses the MAX hand radius — the GRAZE bound. It is the honest choice for a PASS criterion: at that
        // distance the two meshes are still in contact, so anything beyond it is unambiguously NOT touching, and the
        // bound is derived rather than tuned. (The median gives the tighter "haft well inside the fist" bound, 0.1106 m
        // = 0.2415 SW; it is reported by the instruments but is NOT the gate, because the shipped build measures 10.7 cm
        // worst and 3.6 mm of margin would flap red on real frame-timing jitter.)
        //
        // NET EFFECT: the left cap TIGHTENS 2.7x, 0.80 SW -> 0.293 SW (36.6 cm -> 13.4 cm). The shipped round-4 build
        // measures a 10.7 cm worst frame, so it passes with ~2.7 cm of jitter margin while the round-3 build it
        // replaces (28.2 cm) now REDS — which is the property a cap derived from the definition has and a cap derived
        // from the achievable does not.
        //
        // ANCHORED ON THE PALM, NOT THE WRIST. The knuckle sits 0.1112 m from the wrist bone on this rig, so the palm
        // centre is 0.0556 m (5.6 cm) IN FRONT of it. A wrist-anchored criterion is therefore a different question from
        // a palm-anchored one, and round 3's PR body already flagged the mismatch as an "honest gap". The LEFT cap now
        // scores `leftPalmHaftSW`. The RIGHT hand keeps its WRIST figure and its 0.30 SW cap COMPLETELY UNCHANGED
        // (round-4 scope: the right-hand grip is out of scope), so nothing about the approved seat's own gate moves.
        // ==========================================================================================================

        /// <summary>Bare-haft cross-section radius, metres. MEASURED off the stone-pickaxe mesh (AttackClipPoseDiag
        /// `[hand-mesh]`: 0.0526 of the 0.8516 m haft length). Re-measure on any new weapon class.</summary>
        public const float HaftRadiusM = 0.0448f;

        /// <summary>LEFT-hand mesh cross-section radius about the wrist→knuckle axis, metres — the MAX over the 164
        /// vertices dominant-weighted to <c>mixamorig:LeftHand</c> (the graze bound). MEASURED, castaway v4.</summary>
        public const float LeftHandRadiusM = 0.0894f;

        /// <summary>The MEDIAN of the same vertex set, metres — the tighter "haft well inside the fist" bound. Reported
        /// by the panel and the gate for context; deliberately NOT the pass criterion (see the block above).</summary>
        public const float LeftHandRadiusMedianM = 0.0658f;

        /// <summary>The shoulder width the caps are normalised against, metres. MEASURED mean over the shipped mine
        /// clip's judged window (`[seat-fit]` 0.4580 m / `[left-ik]` 0.4569 m over 166 samples). Only used to express a
        /// METRIC tolerance as a SCALE-IMMUNE one — the live read always normalises by the live width.</summary>
        public const float ReferenceShoulderWidthM = 0.4580f;

        /// <summary>Pass cap for the LEFT hand's PALM-CENTRE distance to the haft line, in shoulder-widths. NOT a tuned
        /// number: it is <c>(LeftHandRadiusM + HaftRadiusM) / ReferenceShoulderWidthM</c> = the geometric definition of
        /// the hand mesh and the haft mesh being in contact. ≈ 0.293 SW ≈ 13.4 cm.</summary>
        public const float LeftHaftPassSW = (LeftHandRadiusM + HaftRadiusM) / ReferenceShoulderWidthM;

        /// <summary>The tighter "haft well inside the fist" bound, same derivation with the MEDIAN hand radius
        /// (≈ 0.241 SW ≈ 11.1 cm). Reported, not gated.</summary>
        public const float LeftHaftSnugSW = (LeftHandRadiusMedianM + HaftRadiusM) / ReferenceShoulderWidthM;

        /// <summary>Pass cap for the RIGHT (real, physical grip) hand's WRIST distance to the haft line,
        /// shoulder-widths. UNCHANGED from round 3 — the right-hand grip is out of round-4 scope, and the tool is
        /// seated in this hand so it must sit ON its own haft.</summary>
        public const float RightHaftPassSW = 0.30f;

        /// <summary>One frame's two-hand-grip geometry. <see cref="valid"/> is false when the rig could not be
        /// measured (degenerate shoulder span or a zero-length haft) — callers must NOT read a false as a pass.</summary>
        public struct Read
        {
            public bool valid;
            /// <summary>LEFT WRIST-BONE distance to the haft segment, shoulder-widths. Kept for continuity with the
            /// round-2/3 figures (and because it is what the arm's own reach is felt in) — but it is NO LONGER the
            /// pass criterion: see <see cref="leftPalmHaftSW"/>.</summary>
            public float leftHaftSW;
            /// <summary>RIGHT WRIST-BONE distance to the haft segment, shoulder-widths. Still the RIGHT hand's pass
            /// criterion, unchanged (round-4 scope leaves the right-hand grip alone).</summary>
            public float rightHaftSW;
            /// <summary>LEFT PALM-CENTRE distance to the haft segment, shoulder-widths — THE round-4 pass criterion.
            /// The palm, not the wrist, is what closes around a haft: the knuckle is 11.1 cm from the wrist on this
            /// rig, so the two anchors differ by 5.6 cm and only the palm one answers "is it touching the shaft".
            /// NaN-free but meaningless unless <see cref="palmMeasured"/> is true.</summary>
            public float leftPalmHaftSW;
            /// <summary>RIGHT PALM-CENTRE distance to the haft segment, shoulder-widths. Reported for symmetry so the
            /// two hands are comparable in the same anchor; NOT a pass criterion (the right cap is unchanged).</summary>
            public float rightPalmHaftSW;
            /// <summary>True when real palm centres were supplied. FALSE means the palm figures are wrist figures and
            /// must NOT be read as a palm measurement — "we do not know" never renders as "it is fine".</summary>
            public bool palmMeasured;
            /// <summary>Where along the haft each hand's closest point falls (0 = grip end, 1 = head end).
            /// UNCLAMPED, so a hand that has slid off the end of the haft is visible as &lt;0 or &gt;1.</summary>
            public float leftU, rightU;
            /// <summary>Angle (deg, folded to 0..90) between the haft and the line through both hands. A haft read
            /// is a LINE, not an arrow, so the fold is correct: 0 = the tool lies along the grip the eye reads.</summary>
            public float toolVsHandLineDeg;
            /// <summary>Hand separation in shoulder-widths. Reported for continuity with the round-1 measurements
            /// (and because it explains the residual) — it is NOT a pass criterion any more.</summary>
            public float handSepSW;
            /// <summary>The shoulder width used to normalise, in world units.</summary>
            public float shoulderWidth;
        }

        /// <summary>
        /// Measure one frame. All arguments are WORLD positions; <paramref name="haftGrip"/>/<paramref name="haftHead"/>
        /// are the two ends of the held tool's long axis (see <see cref="HeldToolRig.TryGetHaftSegment"/>).
        /// PURE — no Unity object access, no allocation, no frame state — so the F9 panel, the shipped-build gate and
        /// the EditMode tests all score identical geometry identically.
        /// </summary>
        public static Read Measure(Vector3 leftUpperArm, Vector3 rightUpperArm,
                                   Vector3 leftHand, Vector3 rightHand,
                                   Vector3 haftGrip, Vector3 haftHead)
            => Measure(leftUpperArm, rightUpperArm, leftHand, rightHand, haftGrip, haftHead,
                       leftHand, rightHand, palmMeasured: false);

        /// <summary>
        /// 86cay4282 round 4 — the same measurement with real PALM CENTRES, which is what the left-hand pass criterion
        /// is now defined on. <paramref name="palmMeasured"/> must be false when the caller only has wrist positions:
        /// the palm fields then mirror the wrists and every consumer is told so, rather than being handed a wrist figure
        /// dressed as a palm one.
        /// </summary>
        public static Read Measure(Vector3 leftUpperArm, Vector3 rightUpperArm,
                                   Vector3 leftHand, Vector3 rightHand,
                                   Vector3 haftGrip, Vector3 haftHead,
                                   Vector3 leftPalm, Vector3 rightPalm, bool palmMeasured)
        {
            var r = new Read();
            float sw = (rightUpperArm - leftUpperArm).magnitude;
            Vector3 seg = haftHead - haftGrip;
            if (sw < 1e-5f || seg.sqrMagnitude < 1e-10f) return r;   // valid stays FALSE — never read as a pass

            r.valid = true;
            r.shoulderWidth = sw;
            r.leftHaftSW = DistanceToSegment(leftHand, haftGrip, haftHead, out r.leftU) / sw;
            r.rightHaftSW = DistanceToSegment(rightHand, haftGrip, haftHead, out r.rightU) / sw;
            r.leftPalmHaftSW = DistanceToSegment(leftPalm, haftGrip, haftHead, out _) / sw;
            r.rightPalmHaftSW = DistanceToSegment(rightPalm, haftGrip, haftHead, out _) / sw;
            r.palmMeasured = palmMeasured;
            r.handSepSW = (leftHand - rightHand).magnitude / sw;

            float ang = Vector3.Angle(seg, rightHand - leftHand);
            r.toolVsHandLineDeg = ang > 90f ? 180f - ang : ang;
            return r;
        }

        /// <summary>
        /// Does this frame read as a two-hand grip?
        ///   • LEFT — the PALM CENTRE must be within <see cref="LeftHaftPassSW"/>, the mesh-derived TOUCHING bound.
        ///   • RIGHT — the WRIST must be within <see cref="RightHaftPassSW"/>, exactly as in round 3 (unchanged).
        /// An invalid read is NEVER a pass, and an UNMEASURED palm is never a pass either: a wrist figure scored against
        /// a palm cap is a different, easier question, and silently accepting it is how a cap loses its meaning (the
        /// failure this round exists to correct). "We could not measure it" must fail closed.
        /// </summary>
        public static bool Pass(in Read r) =>
            r.valid && r.palmMeasured &&
            r.leftPalmHaftSW <= LeftHaftPassSW && r.rightHaftSW <= RightHaftPassSW;

        /// <summary>
        /// Distance from <paramref name="p"/> to the SEGMENT a..b, with <paramref name="u"/> reporting where along
        /// the segment the closest point falls — UNCLAMPED for the caller's readout (so a hand off the end of the
        /// haft is visible) while the distance itself is measured to the CLAMPED closest point on the segment.
        /// A degenerate (zero-length) segment falls back to the distance to <paramref name="a"/> with u = 0.
        /// </summary>
        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b, out float u)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-10f) { u = 0f; return (p - a).magnitude; }
            u = Vector3.Dot(p - a, ab) / len2;
            return (p - (a + ab * Mathf.Clamp01(u))).magnitude;
        }
    }
}
