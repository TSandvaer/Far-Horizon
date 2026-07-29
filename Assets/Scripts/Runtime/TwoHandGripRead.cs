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
        //   pre-fix (zero delta)      lHaft mean 1.269  MAX 1.445 SW | rHaft mean 0.166 MAX 0.179 SW | 89.7 deg off
        //   SHIPPED (refined fit)     lHaft mean 0.454  MAX 0.612 SW | rHaft mean 0.025 MAX 0.027 SW | 31.9 deg off
        //
        // The LEFT cap is deliberately LOOSE (0.80 vs the shipped 0.612): it sits clear above the shipped worst frame
        // so neither real frame-timing jitter nor the Sponsor re-dialling the seat at the soak can red a build over a
        // taste change, yet well below the 1.445 pre-fix worst frame so a reverted / inverted / ungated delta DOES
        // red. The RIGHT cap is TIGHT (0.30 vs the shipped 0.027) because the right hand is the tool's REAL physical
        // grip — a right hand off its own haft is a worse defect than a phantom left hand slightly off it, and a
        // single shared loose cap would not catch it.
        //
        // The residual is NOT slack in the fix: it is the mine clip's OWN hand-line direction spread (21.0 deg mean /
        // 36.6 deg max about its mean, measured in the hand's frame). The seat delta is ONE CONSTANT, so it can only
        // match the mean direction; removing the rest would need a per-frame solve (IK), which is out of scope.
        // ==========================================================================================================

        /// <summary>Pass cap for the LEFT (phantom-grip) hand's distance to the haft line, in shoulder-widths.</summary>
        public const float LeftHaftPassSW = 0.80f;

        /// <summary>Pass cap for the RIGHT (real, physical grip) hand's distance to the haft line, shoulder-widths.
        /// Tighter than the left: the tool is seated in this hand, so it must sit ON its own haft.</summary>
        public const float RightHaftPassSW = 0.30f;

        /// <summary>One frame's two-hand-grip geometry. <see cref="valid"/> is false when the rig could not be
        /// measured (degenerate shoulder span or a zero-length haft) — callers must NOT read a false as a pass.</summary>
        public struct Read
        {
            public bool valid;
            /// <summary>LEFT hand distance to the haft segment, shoulder-widths.</summary>
            public float leftHaftSW;
            /// <summary>RIGHT hand distance to the haft segment, shoulder-widths.</summary>
            public float rightHaftSW;
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
        {
            var r = new Read();
            float sw = (rightUpperArm - leftUpperArm).magnitude;
            Vector3 seg = haftHead - haftGrip;
            if (sw < 1e-5f || seg.sqrMagnitude < 1e-10f) return r;   // valid stays FALSE — never read as a pass

            r.valid = true;
            r.shoulderWidth = sw;
            r.leftHaftSW = DistanceToSegment(leftHand, haftGrip, haftHead, out r.leftU) / sw;
            r.rightHaftSW = DistanceToSegment(rightHand, haftGrip, haftHead, out r.rightU) / sw;
            r.handSepSW = (leftHand - rightHand).magnitude / sw;

            float ang = Vector3.Angle(seg, rightHand - leftHand);
            r.toolVsHandLineDeg = ang > 90f ? 180f - ang : ang;
            return r;
        }

        /// <summary>Does this frame read as a two-hand grip? BOTH hands must be on the haft within their caps.
        /// An invalid read is NEVER a pass (the "a metric is green on nonsense" guard).</summary>
        public static bool Pass(in Read r) =>
            r.valid && r.leftHaftSW <= LeftHaftPassSW && r.rightHaftSW <= RightHaftPassSW;

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
