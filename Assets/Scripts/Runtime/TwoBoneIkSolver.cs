using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// 86cay4282 round 4 — AN ANALYTIC TWO-BONE IK SOLVE, as a pure function.
    ///
    /// WHY HAND-ROLLED RATHER THAN `com.unity.animation.rigging` (the decision, so nobody re-opens it blind):
    ///
    ///   1. **ORDER.** Animation Rigging's constraints evaluate inside the Animator's PlayableGraph, i.e. during
    ///      `Animator.Update` — which runs BEFORE every MonoBehaviour `LateUpdate`. This project's held-prop chain is
    ///      four LateUpdate drivers (`CastawayArmPose` 50 → `CastawayFingerCurl` 60 → `CastawayHandPose` 65 →
    ///      `HeldToolRig` 100), and the target this solve aims at is a point ON THE HAFT, which only exists after the
    ///      seat has run at order 100. A rigging constraint would therefore (a) aim at a haft that has not been placed
    ///      yet, and (b) have its own output overwritten by orders 50/65 immediately afterwards. That is round 2's
    ///      ordering failure in a new place, and it is not fixable by constraint tuning.
    ///   2. **IDIOM.** `procedural-animation-verbs.md` states the one arm-modification idiom as an additive
    ///      `LateUpdate` offset with NO new Animator clip / state / layer / AvatarMask. Animation Rigging inserts a
    ///      playable layer into the Animator graph — the thing that doc forbids.
    ///   3. **RISK.** The package is not in `Packages/manifest.json`. Adding one re-triggers package resolution on the
    ///      self-hosted runner, and a cold-cache resolve cancelling mid-bootstrap is a documented CI flake class here
    ///      (`unity-conventions.md` §Headless — "Failed to resolve packages: operation cancelled" shipped a stale
    ///      scene to the EditMode gate on #103). A ~120-line pure function carries none of that.
    ///   4. **TESTABILITY.** As a pure static it is pinned in EditMode with no rig, no Animator and no clock — the
    ///      same seam discipline as <see cref="HeldToolRig.ComposeSeat"/> and
    ///      <see cref="CastawayArmPose.NextMineDeGripWeight"/>. A rigging constraint is only observable through a
    ///      live PlayableGraph.
    ///
    /// The rig is **Generic, not Humanoid**, so there is no muscle space to fight either way — this is plain transform
    /// geometry on `mixamorig:*` bones.
    ///
    /// THE SOLVE. Root (shoulder) and mid (elbow) are the two rotating joints; the TIP is any point rigidly attached
    /// below the mid — here the PALM CENTRE, not the wrist bone, because the thing being satisfied is "the haft passes
    /// through the closed hand". Given the two segment lengths the elbow angle closes by the law of cosines; the
    /// remaining freedom is a rotation of the whole chain about the root→target axis, which is what the POLE VECTOR
    /// pins. Both are computed, never searched.
    ///
    /// THE TWO UGLY FAILURE MODES THIS GUARDS (the brief's explicit asks):
    ///   • **Never snap the arm straight.** The target distance is clamped into a shell strictly INSIDE full extension
    ///     (<see cref="StraightArmFraction"/>), and beyond that shell <see cref="Result.reachWeight"/> eases 1→0 over
    ///     <c>reachFalloff</c> metres so the arm hands the pose back to the clip instead of holding a locked stretch.
    ///     A fully-extended arm is also where the elbow plane degenerates, so the same clamp is what keeps the pole
    ///     well-conditioned.
    ///   • **The elbow cannot flip.** The pole is EXPLICIT: primary = the clip's own elbow (preserve the animator's
    ///     bend plane, which moves continuously because the clip does), fallback = a named direction when the clip
    ///     elbow projects too close to the axis to define a plane. A refused pole returns
    ///     <see cref="Result.solved"/> = false and the caller writes NOTHING — never a guessed plane.
    /// </summary>
    public static class TwoBoneIkSolver
    {
        /// <summary>The fraction of full extension the target is clamped to. Strictly below 1 for two reasons: a
        /// fully-straight arm READS as locked/broken, and at full extension the elbow sits ON the root→target axis so
        /// the bend plane degenerates. At 0.98 with equal segments the elbow still stands ~20% of a segment length off
        /// the axis (sqrt(1 − 0.98²) ≈ 0.199), which is a comfortably conditioned plane.</summary>
        public const float StraightArmFraction = 0.98f;

        /// <summary>The multiple of the minimum reachable distance (|a − b|) the target is clamped OUT to. A target
        /// closer than the arm can fold is the mirror of over-reach and equally degenerate.</summary>
        public const float FoldFraction = 1.05f;

        /// <summary>Minimum squared length of the pole's component perpendicular to the chain axis for that pole to
        /// define a usable bend plane. In metres²; 1e-4 = 1 cm of perpendicular offset.</summary>
        public const float PoleEpsSq = 1e-4f;

        /// <summary>One solve. <see cref="solved"/> false means the caller must write NO bone — an unsolvable frame is
        /// never a licence to write a partial or guessed pose.</summary>
        public struct Result
        {
            /// <summary>False = degenerate input (zero-length segment, target on the root, or no usable pole plane).
            /// Callers must leave the bones on the clip pose.</summary>
            public bool solved;
            /// <summary>WORLD rotation for the ROOT bone (upper arm).</summary>
            public Quaternion upperRotation;
            /// <summary>WORLD rotation for the MID bone (forearm).</summary>
            public Quaternion lowerRotation;
            /// <summary>1 while the target is inside the reachable shell; eases to 0 across <c>reachFalloff</c> metres
            /// beyond it. Multiply this into the caller's own engagement weight so an out-of-reach frame BLENDS OUT
            /// rather than holding a straight-armed stretch.</summary>
            public float reachWeight;
            /// <summary>root→target distance, metres (unclamped — the real ask).</summary>
            public float targetDistance;
            /// <summary>The chain's full extension, metres (segment a + segment b).</summary>
            public float maxReach;
            /// <summary>True when <see cref="targetDistance"/> had to be pulled into the solvable shell.</summary>
            public bool clamped;
            /// <summary>True when the PRIMARY pole (the clip's own elbow) was too close to the chain axis to define a
            /// plane and the named fallback direction was used instead. Surfaced so a build that silently runs on the
            /// fallback the whole swing is visible rather than inferred.</summary>
            public bool poleFromFallback;
        }

        /// <summary>
        /// Solve a two-bone chain so <paramref name="tipPos"/> reaches <paramref name="target"/>.
        ///
        /// All arguments are WORLD space. <paramref name="tipPos"/> need not be a bone — any point rigidly attached
        /// below the mid joint works (the caller passes the PALM CENTRE), because the solve only ever uses
        /// |tip − mid| as the second segment length.
        /// </summary>
        /// <param name="rootPos">shoulder joint position.</param>
        /// <param name="rootRot">shoulder bone's CURRENT world rotation (the clip pose) — the returned upper rotation
        /// is this one re-aimed, so the bone's roll about its own axis is inherited from the clip rather than invented.</param>
        /// <param name="midPos">elbow joint position (also the PRIMARY pole hint — see <paramref name="poleHint"/>).</param>
        /// <param name="midRot">forearm bone's CURRENT world rotation (the clip pose).</param>
        /// <param name="tipPos">the point that must land on the target.</param>
        /// <param name="target">where the tip should go.</param>
        /// <param name="poleHint">WORLD point the elbow should aim toward. Pass the clip's own elbow
        /// (<paramref name="midPos"/>) to PRESERVE the animator's bend plane — the continuous, flip-free choice.
        ///
        /// ⚠ PASS THE CHAIN'S OWN ELBOW, NOT A FIXED WORLD POINT — a measured property, not a preference
        /// (`TwoBoneIkSolverTests.APoleNEARLYALIGNEDWithTheChainAxis_...`). The bend plane is defined by the pole's
        /// component PERPENDICULAR to the root→target axis, so as a pole approaches parallel with that axis the
        /// perpendicular shrinks and its DIRECTION becomes hypersensitive: the plane swings by ~(parallel/perp) × the
        /// axis rotation, and the elbow (standing a·sin(A) off the axis) moves proportionally. With a FIXED straight-down
        /// pole and a target sweeping through straight-down, that measured ~5× amplification turns a 4 mm target step
        /// into a 2.1 cm elbow step — which renders as the arm twitching. Using the chain's own elbow makes the two
        /// components a·cos(A) and a·sin(A), so the amplification is cot(A) ≤ 1 for any root angle ≥ 45° — de-amplifying
        /// over the whole working range. This is exactly why <paramref name="poleFallbackDir"/> is a LAST RESORT that
        /// measured 0 frames of use across the shipped mine clip, not a co-equal option.</param>
        /// <param name="poleFallbackDir">WORLD direction used when <paramref name="poleHint"/> projects too close to
        /// the chain axis to define a plane. Must be a real measured direction, not a guess.</param>
        /// <param name="reachFalloff">metres of over-reach across which <see cref="Result.reachWeight"/> eases 1→0.
        /// 0 = a hard cut at the shell edge (not recommended: it pops).</param>
        /// <param name="straightArmFraction">the fraction of full extension the target is clamped to. Exposed rather
        /// than fixed at <see cref="StraightArmFraction"/> because it is the ONE knob trading "how close does the tip
        /// get" against "how straight does the arm go", and that trade must be priced from measurement per rig/verb
        /// rather than assumed (86cay4282 round 4 — the trade curve is in the PR body).</param>
        public static Result Solve(Vector3 rootPos, Quaternion rootRot,
                                   Vector3 midPos, Quaternion midRot,
                                   Vector3 tipPos, Vector3 target,
                                   Vector3 poleHint, Vector3 poleFallbackDir,
                                   float reachFalloff,
                                   float straightArmFraction = StraightArmFraction)
        {
            var res = new Result
            {
                solved = false,
                upperRotation = rootRot,
                lowerRotation = midRot,
                reachWeight = 0f,
            };

            float aLen = (midPos - rootPos).magnitude;      // shoulder → elbow
            float bLen = (tipPos - midPos).magnitude;       // elbow → tip (forearm + hand-to-palm)
            if (aLen < 1e-4f || bLen < 1e-4f) return res;   // degenerate chain — write nothing

            Vector3 toTarget = target - rootPos;
            float c = toTarget.magnitude;
            if (c < 1e-4f) return res;                      // target sits on the shoulder — no direction exists

            res.targetDistance = c;
            res.maxReach = aLen + bLen;

            // REACH WEIGHT. Measured against the SAME shell the clamp uses, so "clamped" and "blending out" cannot
            // disagree: at the shell edge the weight is exactly 1 and starts falling only past it.
            float shell = res.maxReach * Mathf.Clamp(straightArmFraction, 0.10f, StraightArmFraction);
            float over = c - shell;
            res.reachWeight = over <= 0f ? 1f
                            : reachFalloff <= 1e-6f ? 0f
                            : Mathf.Clamp01(1f - over / reachFalloff);

            float cMin = Mathf.Abs(aLen - bLen) * FoldFraction + 1e-4f;
            float cs = Mathf.Clamp(c, cMin, shell);
            res.clamped = Mathf.Abs(cs - c) > 1e-5f;

            Vector3 axis = toTarget / c;

            // THE POLE PLANE. Primary = the clip's own elbow offset off the new axis (continuous ⇒ flip-free).
            Vector3 poleDir = Vector3.ProjectOnPlane(poleHint - rootPos, axis);
            if (poleDir.sqrMagnitude < PoleEpsSq)
            {
                poleDir = Vector3.ProjectOnPlane(poleFallbackDir, axis);
                res.poleFromFallback = true;
            }
            if (poleDir.sqrMagnitude < PoleEpsSq) return res;   // no usable plane — REFUSE, never guess one
            poleDir.Normalize();

            // Rotating `axis` by a POSITIVE angle about n = axis × poleDir carries it toward poleDir (right-hand
            // rule; verified: axis=(1,0,0), poleDir=(0,1,0) ⇒ n=(0,0,1) and +90° about n gives (0,1,0)). So the elbow
            // lands on the POLE side by construction, which is what makes the flip impossible rather than unlikely.
            Vector3 n = Vector3.Cross(axis, poleDir);
            if (n.sqrMagnitude < 1e-10f) return res;
            n.Normalize();

            // Law of cosines at the root: the angle between the upper segment and the chain axis.
            float cosA = (aLen * aLen + cs * cs - bLen * bLen) / (2f * aLen * cs);
            float aDeg = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f)) * Mathf.Rad2Deg;

            Vector3 midWanted = rootPos + (Quaternion.AngleAxis(aDeg, n) * axis) * aLen;
            Vector3 tipWanted = rootPos + axis * cs;

            // UPPER: re-aim the existing bone direction at the wanted elbow. FromToRotation is the MINIMAL arc, so it
            // adds no roll about the bone's own axis — the clip's roll (and therefore the forearm/hand twist the
            // animator authored) is preserved rather than invented.
            Quaternion rUpper = Quaternion.FromToRotation(midPos - rootPos, midWanted - rootPos);
            res.upperRotation = rUpper * rootRot;

            // LOWER: after the upper turn, the tip has been carried rigidly about the root. Re-aim the forearm so the
            // tip lands on the target. |tipAfter − midWanted| == bLen and |tipWanted − midWanted| == bLen by the law
            // of cosines above, so this is a pure direction change too.
            Vector3 tipAfter = rootPos + rUpper * (tipPos - rootPos);
            Quaternion rLower = Quaternion.FromToRotation(tipAfter - midWanted, tipWanted - midWanted);
            res.lowerRotation = rLower * (rUpper * midRot);

            res.solved = true;
            return res;
        }

        /// <summary>
        /// Where the tip ENDS UP for a given solve, computed the way the caller applies it — used by the tests and the
        /// editor fit so "did the tip reach the target" is answered by the same algebra the runtime writes, not by a
        /// re-derivation beside it (the tautological-assert / mirrored-implementation trap,
        /// <c>unity-conventions.md</c> §Editor-vs-runtime).
        /// </summary>
        public static Vector3 ResolvedTip(in Result res, Vector3 rootPos, Quaternion rootRot,
                                          Vector3 midPos, Quaternion midRot, Vector3 tipPos)
        {
            if (!res.solved) return tipPos;
            // upper turn about the root, then the lower turn about the (already moved) elbow.
            Quaternion rUpper = res.upperRotation * Quaternion.Inverse(rootRot);
            Vector3 midAfter = rootPos + rUpper * (midPos - rootPos);
            Vector3 tipAfterUpper = rootPos + rUpper * (tipPos - rootPos);
            Quaternion rLower = res.lowerRotation * Quaternion.Inverse(rUpper * midRot);
            return midAfter + rLower * (tipAfterUpper - midAfter);
        }
    }
}
