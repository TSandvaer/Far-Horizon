using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// POST-ANIMATION arm posing for the Hyper3D + Mixamo castaway (ticket 86ca8rdkp soak-fixes #2 + #3).
    /// A sibling driver to <see cref="HeldAxeRig"/>: it runs in LateUpdate AFTER the Animator has written
    /// the imported Idle/Walk clip pose for this frame, then applies a SMALL RELATIVE rotation OFFSET to the
    /// upper-arm bones. This is the only place arm pose CAN be changed — the arms are driven by the imported
    /// Mixamo clips (we can't author the FBX takes), so an additive post-anim offset on the bones is the
    /// mechanism the ticket prescribes ("a sibling driver to HeldAxeRig, NOT just the axe transform").
    ///
    /// TWO Sponsor soak directives this addresses:
    ///   #3 "his arms are very tight into the body when idle" → relax BOTH upper arms a bit AWAY from the
    ///      torso (an outward roll on each shoulder), so the silhouette is less pinched.
    ///   #2 "when the axe is picked up his right hand should be a bit away from the body and raised 10-15°"
    ///      → the RIGHT upper arm gets an EXTRA away-from-body + raised offset (a "ready" carry pose), on
    ///      top of the relax. The axe rides the hand (HeldAxeRig), so lifting the arm lifts the held axe.
    ///
    /// RE-SOAK (the Sponsor: "the auto pose made it even WORSE when the axe is equipped — axe held too high/
    /// forward — do we need a nudging tool for the arm?"). The per-arm offsets are now PER-ARM LOCAL EULERS
    /// (rightArmEuler / leftArmEuler) that the build-gated F9 AxeNudgeTool dials DIRECTLY in-game (arm-pose
    /// target) — what-you-dial-is-what-you-get, like the held/stump axe. The named deg fields seed a SANE
    /// CONSERVATIVE default (arms only slightly off torso; the axe-arm only slightly raised — NOT the prior
    /// 16° lift + 20° spread that read "too high/forward"); the Sponsor finalizes by dialing + baking.
    ///
    /// WHY A RELATIVE OFFSET (multiply), not an absolute set: the clip animates the arm every frame; we want
    /// to NUDGE that animated pose by a fixed amount, not REPLACE it (which would kill the walk arm-swing).
    ///   bone.localRotation = clipPose * Quaternion.Euler(offsetEuler)
    /// applied in LateUpdate so it composes on top of whatever the Animator left for THIS frame. The offset
    /// is in the BONE'S LOCAL space, so it rides the arm through every facing + every clip frame.
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): authored editor-time by MovementCameraScene
    /// (BuildPlayer → AddArmPose) and serialized onto the avatar root, with the bone refs resolved from the
    /// SkinnedMeshRenderer.bones array (the real skeleton) so it ships in Boot.unity. No Awake-assembled
    /// hierarchy.
    ///
    /// DefaultExecutionOrder(50): AFTER CastawayCharacter (order 0, which yaws the model) and the Animator
    /// (which writes bone poses in its own update), BEFORE HeldAxeRig (order 100, which reads the hand world
    /// transform) — so the right hand has its final posed position when the axe seats to it.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class CastawayArmPose : MonoBehaviour
    {
        [Header("Bones (wired editor-time from the SkinnedMeshRenderer.bones skeleton)")]
        [Tooltip("The right UPPER-arm bone (mixamorig:RightArm). Drives the right-arm carry pose (#2).")]
        public Transform rightUpperArm;
        [Tooltip("The left UPPER-arm bone (mixamorig:LeftArm). Driven by the idle-relax offset (#3).")]
        public Transform leftUpperArm;

        // The EMPIRICAL bone-LOCAL axis mapping (RuntimeRigTrace -armTrace, 2026-06-15, on this exact rig):
        // a +rotation about the upper-arm bone's LOCAL X spreads the hand AWAY from the torso for BOTH arms
        // (right wrist → −X world / left wrist → +X world — both OUTWARD, SAME sign), with a small +Y raise. A
        // +rotation about LOCAL Z spreads further AND lifts the hand (+Y) AND reaches it forward/back (the
        // "ready carry" feel). Local Y is a near-useless twist. So the pose is authored in LOCAL X (spread) +
        // LOCAL Z (raise/reach), NOT a guessed Z-roll mirror — the trace overturned the first guess.
        //
        // RE-SOAK (the Sponsor's "the auto pose made it even WORSE when the axe is equipped, axe held too high/
        // forward — do we need a nudging tool for the arm?"). Two changes this wave:
        //   (1) the offsets now live as PER-ARM LOCAL EULERS (rightArmEuler / leftArmEuler) that the F9
        //       AxeNudgeTool dials DIRECTLY in-game (arm-pose target) — what-you-dial-is-what-you-get, like
        //       the held/stump axe. The named deg fields below SEED these eulers (the conservative default);
        //       the Sponsor finalizes by dialing the eulers and pasting RightArmEuler/LeftArmEuler to bake.
        //   (2) a SANE CONSERVATIVE default: arms only SLIGHTLY off the torso; the axe-arm only SLIGHTLY
        //       raised (NOT the prior 16° lift + 20° spread that read as "axe held too high/forward").

        [Header("#3 — relax BOTH arms away from the torso (the pinched-idle fix) — conservative default")]
        [Tooltip("Degrees of LOCAL-X rotation added to BOTH upper arms to spread them a LITTLE off the torso " +
                 "(less pinched, NOT flung wide). +X spreads BOTH arms OUTWARD (same sign — measured), so NO " +
                 "mirror is applied. Seeds rightArmEuler.x + leftArmEuler.x.")]
        public float relaxSpreadDeg = 9f;

        [Header("#2 — right arm: a bit away from body + only SLIGHTLY raised (the held-axe carry) — conservative")]
        [Tooltip("EXTRA degrees of LOCAL-X spread on the RIGHT upper arm only (on top of the relax). Seeds " +
                 "rightArmEuler.x above the left.")]
        public float rightCarryExtraSpreadDeg = 3f;
        [Tooltip("Degrees of LOCAL-Z on the RIGHT upper arm — lifts/reaches the hand for the carry. Kept SMALL " +
                 "(the prior 16° read 'axe held too high/forward'). Seeds rightArmEuler.z.")]
        public float rightCarryRaiseDeg = 7f;

        [Header("Per-arm LOCAL-euler offsets — what the F9 AxeNudgeTool dials in-game (bake these)")]
        [Tooltip("RIGHT upper-arm LOCAL-euler offset (deg). X = spread off torso, Z = raise/reach. Seeded from " +
                 "the deg fields above; the Sponsor dials it via F9 (arm-pose target) and bakes RightArmEuler.")]
        public Vector3 rightArmEuler;
        [Tooltip("LEFT upper-arm LOCAL-euler offset (deg). X = spread off torso. Seeded from relaxSpreadDeg; " +
                 "dialed via F9 and baked as LeftArmEuler.")]
        public Vector3 leftArmEuler;

        // When true, RebuildCached re-DERIVES the per-arm eulers from the named deg fields (the authored
        // default seed). The F9 nudge tool sets this FALSE before dialing so a re-Rebuild can't clobber the
        // Sponsor's live dial; the editor author seeds with it TRUE.
        [HideInInspector] public bool seedEulersFromDegFields = true;

        // Cached so the offset composes on the clip pose, not on the prior frame's offset (which would drift).
        private Quaternion _rightOffsetQ, _leftOffsetQ;

        void Awake()
        {
            RebuildCached();
        }

        /// <summary>Rebuild the cached offset quaternions. If <see cref="seedEulersFromDegFields"/> is set
        /// (the authored default), the per-arm eulers are first re-derived from the named deg fields; the F9
        /// nudge tool clears that flag so it can dial rightArmEuler/leftArmEuler directly. Public so the editor
        /// author + the nudge tool can re-apply after editing the fields.</summary>
        public void RebuildCached()
        {
            if (seedEulersFromDegFields)
            {
                // BOTH arms: the relax is a +local-X spread (same sign — measured outward for both). The RIGHT
                // arm gets EXTRA local-X spread + a local-Z raise/reach (the carry).
                leftArmEuler = new Vector3(relaxSpreadDeg, 0f, 0f);
                rightArmEuler = new Vector3(relaxSpreadDeg + rightCarryExtraSpreadDeg, 0f, rightCarryRaiseDeg);
            }
            // Compose euler in the bone's LOCAL frame (Unity's Quaternion.Euler order Z,X,Y). The offset is
            // right-multiplied onto the clip pose in LateUpdate so it rides every facing + every clip frame.
            _leftOffsetQ = Quaternion.Euler(leftArmEuler);
            _rightOffsetQ = Quaternion.Euler(rightArmEuler);
        }

        void LateUpdate()
        {
            // Compose the offset ON the clip's animated localRotation for THIS frame (the Animator already
            // ran). bone.localRotation here is the clip pose; right-multiplying by the offset rotates in the
            // bone's local frame, preserving the walk arm-swing while nudging the rest position.
            if (rightUpperArm != null)
                rightUpperArm.localRotation = rightUpperArm.localRotation * _rightOffsetQ;
            if (leftUpperArm != null)
                leftUpperArm.localRotation = leftUpperArm.localRotation * _leftOffsetQ;
        }
    }
}
