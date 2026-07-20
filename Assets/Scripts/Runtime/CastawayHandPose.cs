using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// POST-ANIMATION HAND-orientation offsets for the castaway (ticket 86catvb6u round-8 — the Mixamo RE-RIG
    /// hand saga). A sibling driver to <see cref="CastawayArmPose"/> / <see cref="CastawayFingerCurl"/> /
    /// <see cref="CastawayFootYaw"/>: it runs in LateUpdate AFTER the Animator writes the clip pose AND after the
    /// arm-pose (order 50) + finger-curl (order 60), then composes additive rotations onto the hand + thumb bones.
    ///
    /// TWO independent knobs PER SIDE (round-8), because the Sponsor's residual was: "I cannot turn the hand, the
    /// only way to point the thumb towards the body is to twist the wrist and arm also" — he wanted hand-segment
    /// control BELOW the wrist, independent of the coarse wrist orientation:
    ///   - WRIST (rotate the HAND bone, mixamorig:RightHand / :LeftHand) — the coarse palm/wrist orientation. The
    ///     Sponsor F9-dialed the right to (10,-120,-20) = a CORRECT right arm; the left ships its MEASURED mirror
    ///     (CastawayV4DefectDiag round-8, anchored on the Sponsor-correct right — NOT the mirror of the wrong left).
    ///   - THUMB/HAND (rotate the THUMB bone, mixamorig:RightHandThumb1 / :LeftHandThumb1) — orients the thumb
    ///     WITHOUT twisting the wrist/arm. Ships 0 (the Sponsor taste-dials it via the F9 HAND knob, L and R).
    ///
    /// WHY (measured — CastawayV4DefectDiag round-8): the re-rig's upper ARMS are mirror-symmetric (bind delta
    /// 1.2deg) but the right HAND BIND FRAME is rolled 176.4deg off-mirror; the idle/walk clips re-pose both hands
    /// to within 18.1deg of each other, but at an orientation ~120deg from natural — so BOTH hands ship TWISTED
    /// (mirror-of-each-other yet both wrong). Round-7's "symmetric arms => correct hands, no compensation" was
    /// REFUTED. The mirror-of-left approach (round-5) failed because the LEFT is ALSO wrong. Round-8 anchors on the
    /// Sponsor-verified ABSOLUTE right (10,-120,-20) and derives the left as its render-mirror.
    ///
    /// IDIOM (procedural-animation-verbs.md — the additive-LateUpdate offset chain): NO new Animator clip/state/
    /// layer/mask. bone.localRotation = clipPose * Euler(offset). Zero at every euler==0 => byte-unchanged (v3/v2/
    /// old ship 0 for all four — their hands read fine; only v4 defaults the two WRIST eulers non-zero).
    ///
    /// DefaultExecutionOrder(65): AFTER the Animator + CastawayArmPose(50) + CastawayFingerCurl(60), BEFORE
    /// HeldAxeRig(100) — so the hand has its final corrected orientation before the axe seats onto it (the axe
    /// follows the corrected hand). The finger + thumb bones (curled at order 60) are children of the hand, so they
    /// ride the WRIST rotation rigidly (the grip stays intact); the THUMB offset composes on top of any curl.
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): authored editor-time by MovementCameraScene
    /// (AddHandPose) with the bones resolved from SkinnedMeshRenderer.bones + serialized into Boot.unity.
    /// STATIC STATE: instance fields only — no mutable runtime statics (StaticStateResetTests stays green).
    /// </summary>
    [DefaultExecutionOrder(65)]
    public class CastawayHandPose : MonoBehaviour
    {
        [Header("Hand bones (mixamorig:RightHand / :LeftHand) — the WRIST-level rotation")]
        [Tooltip("The right HAND bone (mixamorig:RightHand). Wired editor-time from SkinnedMeshRenderer.bones.")]
        public Transform rightHand;
        [Tooltip("The left HAND bone (mixamorig:LeftHand). Wired editor-time from SkinnedMeshRenderer.bones.")]
        public Transform leftHand;

        [Header("WRIST offsets (deg, LOCAL-euler on the hand bone) — coarse palm/wrist orientation")]
        [Tooltip("Additive LOCAL-euler offset on the RIGHT hand bone. v4 default = the Sponsor F9-dialed (10,-120," +
                 "-20) = a correct right arm. 0 = hand byte-unchanged. F9-dialable (WRIST target, right side).")]
        public Vector3 rightWristEuler;
        [Tooltip("Additive LOCAL-euler offset on the LEFT hand bone. v4 default = the MEASURED render-mirror of the " +
                 "Sponsor-correct right (CastawayV4DefectDiag round-8). 0 = hand byte-unchanged. F9-dialable (WRIST " +
                 "target, left side).")]
        public Vector3 leftWristEuler;

        [Header("Thumb bones (mixamorig:RightHandThumb1 / :LeftHandThumb1) — the HAND-segment (thumb) rotation")]
        [Tooltip("The right THUMB base bone. Wired editor-time; the HAND knob rotates it to orient the thumb " +
                 "independently of the wrist.")]
        public Transform rightThumb;
        [Tooltip("The left THUMB base bone. Wired editor-time; the HAND knob rotates it to orient the thumb " +
                 "independently of the wrist.")]
        public Transform leftThumb;

        [Header("THUMB/HAND offsets (deg, LOCAL-euler on the thumb bone) — thumb orientation below the wrist")]
        [Tooltip("Additive LOCAL-euler offset on the RIGHT thumb base bone. Ships 0 — the Sponsor taste-dials it " +
                 "via the F9 HAND knob (right side) to point the thumb without twisting the wrist/arm.")]
        public Vector3 rightThumbEuler;
        [Tooltip("Additive LOCAL-euler offset on the LEFT thumb base bone. Ships 0 — F9-dialable (HAND knob, left).")]
        public Vector3 leftThumbEuler;

        void LateUpdate()
        {
            // Each offset composes on the clip pose for THIS frame (right-multiply in the bone's local frame).
            // A zero euler => identity => the bone is byte-unchanged (the non-v4 rollback path + the un-dialed thumb).
            Apply(rightHand, rightWristEuler);
            Apply(leftHand, leftWristEuler);
            Apply(rightThumb, rightThumbEuler);
            Apply(leftThumb, leftThumbEuler);
        }

        private static void Apply(Transform bone, Vector3 euler)
        {
            if (bone == null || euler == Vector3.zero) return;
            bone.localRotation = bone.localRotation * Quaternion.Euler(euler);
        }
    }
}
