using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// 86cb6v03j — WHICH WAY DOES THE HELD WEAPON POINT WHILE IT IS BEING SWUNG?
    ///
    /// THE REAL-WORLD ANCHOR, stated first because every number below has to satisfy it rather than the other way
    /// round: <b>a swung axe / pickaxe / sword is a LEVER. The head (or blade tip) is the FAR end of the stick from
    /// the hand, so through the strike it sweeps the OUTSIDE of the arc — it travels FASTER than the hand and it
    /// arrives at the target AHEAD of the hand. A weapon that points back toward the body, or that trails the hand
    /// through the arc, is being CARRIED through the swing rather than swung.</b>
    ///
    /// WHY THIS IS A SEPARATE READ FROM <see cref="SwingSeatGate"/>. The seat gate scores (a) the RIGHT hand's
    /// PERPENDICULAR distance to the haft LINE and (b) the hand's ALONG-haft position u. Both are properties of
    /// WHERE THE HAND SITS ON THE STICK. Neither one changes AT ALL if the whole stick is rotated about the hand:
    /// spin the haft 180 deg about the grip point and the perpendicular distance is identical, u is identical, and
    /// both gates stay green while the axe now points backwards over the shoulder. That is not an empirical
    /// observation that might not replicate — it is a property of the metrics (a distance to a LINE is invariant
    /// under rotation of that line about the measured point, up to the point-to-line foot moving, and u is measured
    /// ALONG the rotated line). ORIENTATION is therefore a THIRD, independent defect axis, and it is exactly the
    /// axis ticket 86cazq5c0 records as an open verification hole ("no along-haft-ONLY shipped-build control").
    ///
    /// WHY EVERY QUANTITY HERE IS A PER-FRAME SERIES AND NOT A VALUE AT AN INSTANT. The #436 incident
    /// ([[feel-gates-need-an-eye-time-floor]]) is the precedent: a value-at-one-instant assert cannot express what
    /// the eye consumes over a swing window. Two of the four readings below (<see cref="Read.speedRatio"/>,
    /// <see cref="Read.leadDot"/>) are DERIVATIVES — they do not exist at a single frame at all, they require the
    /// previous frame's positions — so this read is structurally incapable of being collapsed to a one-frame assert.
    ///
    /// FRAME CONVENTION — AND A MEASURED CORRECTION TO THE OBVIOUS CHOICE. The first cut of this read built the
    /// usual torso frame from the rig's own geometry (up = hips->head, right = LeftArm->RightArm,
    /// fwd = cross(up, right)) — the frame procedural-animation-verbs.md prescribes for judging a POSE. It also
    /// logged that frame's agreement with the model transform's own forward, and the shipped run answered:
    /// <c>torsoFwd.model = -0.83 (axe) / -0.06 (pickaxe) / -0.63 (dagger) / -0.30 (spear) / -0.03 (sword)</c>.
    /// That is not a sign ambiguity, it is a frame that ROTATES: <c>right</c> is taken from the two upper-ARM bones,
    /// and a swing is precisely the event that swings them, so mid-swing the constructed forward can end up
    /// PERPENDICULAR to the direction the character is actually attacking along. A pose-judging frame is the right
    /// tool for "is the torso folded"; it is the wrong tool for "is the weapon pointing where he is attacking".
    ///
    /// So the FACING axis used here is <b>the model transform's own forward</b> — the transform
    /// <see cref="CastawayCharacter"/> yaws toward the character's facing ("the visual owns facing",
    /// unity-conventions.md §FBX/rigs). That is the axis the PLAYER's eye uses to decide whether a weapon points
    /// where the strike is aimed, and it is stable through the swing because the swing does not yaw the body.
    /// <see cref="Read.up"/>-relative terms use WORLD up, which needs no frame at all.
    ///
    /// ANGLES ARE REPORTED RAW, DISTANCES ARE SHOULDER-WIDTH NORMALISED — the project convention
    /// (procedural-animation-verbs.md: "Normalise distances; report ANGLES raw"). Everything here except
    /// <see cref="Read.headSpeed"/>/<see cref="Read.gripSpeed"/> is already scale-free (unit-vector dots and a
    /// ratio), so nothing is normalised twice.
    /// </summary>
    public static class SwingPointRead
    {
        /// <summary>One frame's pointing read. All dots are of UNIT vectors, so each is a cosine in [-1, 1].</summary>
        public struct Read
        {
            /// <summary>False when the haft segment, the torso frame or the previous frame was unusable. A false
            /// read must never be scored — unmeasured is not a pass.</summary>
            public bool valid;

            /// <summary>THE LEVER TERM. dot(haftDir, normalize(grip - chest)): +1 = the head points straight AWAY
            /// from the body along the arm (the stick is extended outward, which is what a swing needs); 0 = the
            /// haft lies across the body; -1 = the head points back INTO the chest.</summary>
            public float extendDot;

            /// <summary>THE OUTSIDE-OF-THE-ARC TERM, and the one that cannot be faked by a static pose: the head's
            /// world speed divided by the grip's. A weapon extended outward from the pivot has its head further out
            /// than the hand, so it MUST sweep faster (&gt;= 1). Below 1 the head is INBOARD of the hand — the
            /// weapon is pointing back toward the pivot. A pure translation (a spear thrust) gives ~1.</summary>
            public float speedRatio;

            /// <summary>dot(haftDir, normalize(gripVelocity)): +1 = the head points where the hand is travelling
            /// (head leads); -1 = the head trails the hand through the arc. Near 0 for a slash whose blade is
            /// perpendicular to the hand's travel, so this is DIAGNOSTIC, not universal — read it per class.</summary>
            public float leadDot;

            /// <summary>THE AIM TERM. dot(haftDir, model.forward): +1 = the head points along the direction the
            /// character is facing, i.e. into the strike; 0 = across it; -1 = backwards, away from whatever is being
            /// attacked. This is the quantity the Sponsor's report is about.</summary>
            public float fwdDot;

            /// <summary>dot(haftDir, worldUp) — is the head ABOVE (+) or BELOW (-) the grip? Frame-free.</summary>
            public float upDot;

            /// <summary>Head world speed, m/s (raw — the ratio is the scale-free form).</summary>
            public float headSpeed;

            /// <summary>Haft BUTT-end world speed, m/s. ⚠ This is a point on the WEAPON, so it MOVES when the
            /// weapon is re-aimed. Use it for <see cref="speedRatio"/>/<see cref="leadDot"/>, never to define which
            /// frames are being judged — see <see cref="handSpeed"/>.</summary>
            public float gripSpeed;

            /// <summary>THE HAND BONE's world speed, m/s — the only speed here that the clip owns outright.
            ///
            /// ⚠ IT EXISTS BECAUSE ITS ABSENCE WAS A REAL DEFECT. The strike window was first keyed off
            /// <see cref="gripSpeed"/> under the belief that "the grip" meant the hand. It does not: gripW is the
            /// haft's BUTT ENDPOINT, a point on the weapon, which the aim delta rotates. So the window moved every
            /// time the fix moved, and the fit oscillated instead of converging (axe residual 141.0 -> 110.0 while
            /// its window went 44 -> 69 frames; sword 85.8 -> 118.7 with its window 34 -> 18). The judged MOMENT
            /// must never be defined using a quantity the repair changes — and the hand bone is the one point in
            /// this measurement the seat provably cannot move.</summary>
            public float handSpeed;

            /// <summary>Haft length, m — so a reader can tell a short dagger's read from a spear's.</summary>
            public float haftLenM;

            /// <summary>THE OWNING-LAYER DISCRIMINATOR. The haft direction expressed in the RIGHT HAND BONE's own
            /// frame: <c>Inverse(hand.rotation) * haftDirWorld</c>.
            ///
            /// This is CONSTANT through a swing BY CONSTRUCTION — the seat is
            /// <c>toolRotation = hand.rotation * Euler(seatEuler)</c> and the mesh holder is rigidly parented under
            /// it, so the haft's direction relative to the hand depends only on the SEAT dials
            /// (<see cref="HeldAxeRig.relEuler"/> composed with this class's
            /// <see cref="HeldWeaponCycleDebug.WeaponMeshLocalEuler"/>) and NOT on the clip. The clip supplies
            /// <c>hand.rotation</c> and nothing else.
            ///
            /// So: if this vector is constant across the swing (it must be, and the gate logs its spread as the
            /// check) then WORLD pointing = clip's hand rotation x this constant, and the two candidate owners are
            /// cleanly separated. Together with <see cref="handAxisBestFwdDown"/> it answers "could ANY seat dial
            /// have made this point the right way", which is what decides whether the fix belongs on the seat or on
            /// the arm-pose/clip side.</summary>
            public Vector3 haftInHandLocal;

            /// <summary>The best (largest) dot against the FORWARD-AND-DOWN strike direction achievable by any of
            /// the six signed hand-bone axes (+/-right, +/-up, +/-forward) at this frame.
            ///
            /// WHY IT DECIDES THE OWNING LAYER. A seat re-dial can rotate the weapon to ANY fixed orientation
            /// relative to the hand — so the set of world directions a re-dialled seat could make the haft point is
            /// exactly the set of directions reachable from the hand's frame, and the six signed axes sample it.
            /// If one of them already points forward-and-down at the strike, then the hand is oriented FINE and a
            /// SEAT dial can fix the pointing. If none does — i.e. the strike direction is not reachable from this
            /// hand's frame at all — then the HAND itself is turned the wrong way and the fix is on the clip /
            /// <see cref="CastawayArmPose"/> side, where a seat dial could never reach it.</summary>
            public float handAxisBestFwdDown;

            /// <summary>Which signed hand axis achieved <see cref="handAxisBestFwdDown"/>: 0..5 =
            /// +right,-right,+up,-up,+forward,-forward. Named in the log so the answer is actionable rather than a
            /// bare score.</summary>
            public int handAxisBestIndex;
        }

        /// <summary>Human names for <see cref="Read.handAxisBestIndex"/>.</summary>
        public static readonly string[] HandAxisNames =
            { "+right", "-right", "+up", "-up", "+forward", "-forward" };

        // ==============================================================================================
        // THE PASS CRITERION (86cb6v03j)
        // ==============================================================================================

        /// <summary>
        /// THE BOUND, and it is a GEOMETRIC BOUNDARY rather than a tuned threshold — which is the whole reason it
        /// can be trusted. <see cref="Read.fwdDot"/> is the cosine between the weapon's heading and the direction
        /// the character is attacking along, so its SIGN is the line between "the head is in the half-space he is
        /// attacking into" and "the head is behind him". Zero is not a number anybody picked; it is where the
        /// meaning changes.
        ///
        /// ⚠ WHICH STATISTIC OF THE WINDOW IS GATED, AND WHY IT IS THE MEAN AND NOT THE MINIMUM. The first cut gated
        /// the window's MINIMUM — "the weapon must not point backwards at ANY moment of the fast phase" — which
        /// sounds strictly better and is in fact WRONG, as the measurement showed immediately: the axe's strike
        /// window spans phase 0.177..0.397 and its fwdDot sweeps from -0.902 to +0.907 INSIDE that window. Of course
        /// it does. A swing is a rotation, so the head's heading necessarily rotates through a wide arc while the
        /// hand is fast; requiring every fast frame to face forward is requiring the weapon not to swing. A
        /// criterion no correct build could satisfy is not a strict criterion, it is a broken one.
        ///
        /// The MEAN over the window is the quantity that actually distinguishes the two cases: a weapon that leads
        /// into the strike spends the fast phase predominantly pointing forward and averages positive, while one
        /// that trails through the arc averages negative. It is also stable (an average over ~7-20 frames rather
        /// than one spiky frame) and it is a DURATION measure, which is the #436 lesson
        /// ([[feel-gates-need-an-eye-time-floor]]) applied rather than restated. The window's min and max are
        /// REPORTED alongside so the sweep's shape stays visible and this choice stays auditable.
        ///
        /// ⚠ THE DIRECTION THIS BOUND CAN FAIL IN, stated plainly. A cap calibrated against what a fix ACHIEVES can
        /// never catch a fix that achieves too little — that is what let <c>LeftHaftPassSW = 0.80</c> print PASS for
        /// three rounds over a hand gripping air (procedural-animation-verbs.md, "derive a pass cap from what the
        /// thing MEANS"). This one is not derived from the fix: the fit aims the mean AT the aim direction (residual
        /// 0), while the bound only asks the mean to stay on the forward side, so a fix that under-achieves by up to
        /// 90 deg still reds. What it deliberately does NOT claim is that a weapon at mean fwdDot = +0.05 LOOKS
        /// right — only that it is no longer sweeping the strike backwards. Tightening it toward the fitted residual
        /// would be calibrating against achievement again; the Sponsor's eye at the soak is the bar for "looks
        /// right", and this is the bar for "is not backwards".
        /// </summary>
        public const float StrikeFwdDotPassFloor = 0f;

        /// <summary>
        /// Does the weapon point INTO the strike at the judged frame? <paramref name="why"/> always names the
        /// measured value, its angle-off-facing, and the class — so a RED reads as a defect report, not a boolean.
        /// FAILS CLOSED on an unmeasured read: an absent measurement must never render as "it is fine".
        /// </summary>
        public static bool StrikeAimOk(bool measured, float strikeFwdDot, string className, out string why)
        {
            if (!measured || float.IsNaN(strikeFwdDot))
            {
                why = "AIM UNMEASURED (" + className + ") - no valid strike-frame reading was taken (the swing never " +
                      "posed, the mesh was wrong, or the haft was unresolvable). Unmeasured is NOT a pass.";
                return false;
            }
            bool ok = strikeFwdDot > StrikeFwdDotPassFloor;
            float deg = Mathf.Acos(Mathf.Clamp(strikeFwdDot, -1f, 1f)) * Mathf.Rad2Deg;
            why = (ok ? "sweeps into the strike" : "SWEEPS THE STRIKE BACKWARDS") + " (" + className +
                  ") - MEAN fwdDot " + strikeFwdDot.ToString("F3", Inv) + " = " + deg.ToString("F0", Inv) +
                  " deg off the direction the character is facing, averaged across the STRIKE WINDOW (every frame " +
                  "at or above half this swing's own peak HAND speed) (bound: > " +
                  StrikeFwdDotPassFloor.ToString("F2", Inv) + ", i.e. inside 90 deg = the head leads into the " +
                  "half-space he is attacking into). Re-measure the unfixed baseline any time with " +
                  "-swingAimFaultZero, which forces every swing-aim delta to zero in the shipped exe.";
            return ok;
        }

        /// <summary>Invariant formatting: these lines are quoted into PR bodies and may be grepped, and this
        /// project's own runner is a comma-decimal locale (the same reason <see cref="SwingSeatGate"/> pins it).
        /// </summary>
        private static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>The direction a strike is aimed: forward along the character's facing and downward, normalised.
        /// Used only by <see cref="Read.handAxisBestFwdDown"/> as the reachability probe direction — it is a
        /// diagnostic reference, never a pass criterion (a chop, a slash and a thrust do not share one aim vector).
        /// 45 degrees is the bisector of "straight ahead" and "straight down", so it is not biased toward either.
        /// </summary>
        public static Vector3 StrikeAim(Vector3 modelForward)
        {
            Vector3 f = modelForward.sqrMagnitude > 1e-8f ? modelForward.normalized : Vector3.forward;
            return (f + Vector3.down).normalized;
        }

        /// <summary>Speeds below this (m/s) mean the hand is essentially stationary, so
        /// <see cref="Read.speedRatio"/> and <see cref="Read.leadDot"/> are ratios/directions of noise. Frames
        /// under it are marked INVALID rather than scored — a divide-by-nearly-zero produces a confident number
        /// from nothing, which is the vacuity shape <see cref="SwingSeatGate"/>'s liveness term exists to kill.
        /// 0.10 m/s is two orders below the ~4-10 m/s a real strike reaches on this rig.</summary>
        public const float MinScoredGripSpeedMps = 0.10f;

        /// <summary>
        /// Measure one frame. PURE — every input is passed in, so an EditMode test can pin the algebra without a
        /// live Animator, and the shipped gate and the test score identical inputs identically (the one-seam
        /// discipline <see cref="TwoHandGripRead"/> / <see cref="SwingSeatGate"/> established).
        /// </summary>
        /// <param name="gripW">Haft BUTT/grip end, world.</param>
        /// <param name="headW">Haft HEAD/tip end, world.</param>
        /// <param name="prevGripW">Previous frame's grip, world.</param>
        /// <param name="prevHeadW">Previous frame's head, world.</param>
        /// <param name="dt">Seconds between the two samples.</param>
        /// <param name="chestW">Chest anchor — the midpoint of the two upper-arm bones. Used ONLY by
        /// <see cref="Read.extendDot"/>.</param>
        /// <param name="handRot">The RIGHT hand bone's world rotation — the frame the seat composes onto.</param>
        /// <param name="modelForward">The model transform's own forward: the direction the character is FACING,
        /// and therefore attacking along. NOT a constructed torso frame — see the class summary for why that was
        /// measured to rotate mid-swing and replaced.</param>
        public static Read Measure(Vector3 gripW, Vector3 headW, Vector3 prevGripW, Vector3 prevHeadW, float dt,
                                   Vector3 chestW, Quaternion handRot, Vector3 modelForward,
                                   Vector3 handW, Vector3 prevHandW)
        {
            Read r = default;
            r.valid = false;
            r.speedRatio = float.NaN;
            r.leadDot = float.NaN;
            r.extendDot = float.NaN;
            r.fwdDot = float.NaN;
            r.upDot = float.NaN;
            r.handAxisBestFwdDown = float.NaN;
            r.handAxisBestIndex = -1;

            if (dt <= 1e-6f) return r;

            Vector3 haft = headW - gripW;
            r.haftLenM = haft.magnitude;
            if (r.haftLenM < 1e-4f) return r;
            Vector3 haftDir = haft / r.haftLenM;

            Vector3 fwd = modelForward.sqrMagnitude > 1e-8f ? modelForward.normalized : Vector3.forward;

            Vector3 outward = gripW - chestW;
            if (outward.sqrMagnitude < 1e-8f) return r;
            r.extendDot = Vector3.Dot(haftDir, outward.normalized);

            Vector3 vHead = (headW - prevHeadW) / dt;
            Vector3 vGrip = (gripW - prevGripW) / dt;
            r.headSpeed = vHead.magnitude;
            r.gripSpeed = vGrip.magnitude;
            r.handSpeed = (handW - prevHandW).magnitude / dt;
            if (r.gripSpeed < MinScoredGripSpeedMps) return r;   // stationary tool: the ratio would be noise/noise

            r.speedRatio = r.headSpeed / r.gripSpeed;
            r.leadDot = Vector3.Dot(haftDir, vGrip / r.gripSpeed);
            r.fwdDot = Vector3.Dot(haftDir, fwd);
            r.upDot = Vector3.Dot(haftDir, Vector3.up);

            // THE SEAT'S OWN CONSTANT — frame-invariant, so this is the seat dials' contribution isolated from the
            // clip's. Its SPREAD across a swing is logged by the caller as the check that the construction holds.
            r.haftInHandLocal = Quaternion.Inverse(handRot) * haftDir;

            // REACHABILITY: could ANY seat dial have aimed the weapon into the strike from this hand pose?
            Vector3 aim = StrikeAim(fwd);
            Vector3[] axes = { handRot * Vector3.right,   -(handRot * Vector3.right),
                               handRot * Vector3.up,      -(handRot * Vector3.up),
                               handRot * Vector3.forward, -(handRot * Vector3.forward) };
            r.handAxisBestFwdDown = -2f;
            for (int i = 0; i < axes.Length; i++)
            {
                float d = Vector3.Dot(axes[i].normalized, aim);
                if (d > r.handAxisBestFwdDown) { r.handAxisBestFwdDown = d; r.handAxisBestIndex = i; }
            }

            r.valid = true;
            return r;
        }
    }
}
