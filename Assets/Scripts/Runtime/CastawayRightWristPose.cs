using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// POST-ANIMATION right-WRIST (hand-bone) rotation offset for the castaway (ticket 86catvb6u round-5 — the
    /// Sponsor's "right hand does not mirror the left" defect, DIRECT-KNOB fallback after the mirror-left arm-pose
    /// euler missed). A sibling driver to <see cref="CastawayArmPose"/> / <see cref="CastawayFingerCurl"/> /
    /// <see cref="CastawayFootYaw"/>: runs in LateUpdate AFTER the Animator writes the clip pose AND after the
    /// arm-pose (order 50) + finger-curl (order 60), then composes an additive rotation onto the RIGHT HAND bone.
    ///
    /// WHY (measured — CastawayV4DefectDiag round-5): the mirror-left ARM euler could NOT fix the right hand
    /// because the arm-pose rotates the UPPER ARM (repositions the arm) — it cannot correct the HAND BONE's own
    /// rolled bind frame. The v4 Mixamo auto-rig gave the right hand a bind frame rolled ~11.7deg off the mirrored
    /// left (18.1deg live with the arm-pose), so the palm reads turned. This wrist offset rotates the hand BONE
    /// directly to make the rendered right hand mirror the left; the derived correction (Inv(R_right)·mirror(R_left)
    /// at idle) is the seeded default, then the Sponsor F9-dials the exact grip by eye (the direct-knob rule —
    /// build-an-instrument after fix-miss #2).
    ///
    /// IDIOM (procedural-animation-verbs.md — the additive-LateUpdate offset chain): NO new Animator clip/state/
    /// layer/mask. bone.localRotation = clipPose * Euler(wristEuler). Zero at wristEuler==0 => byte-unchanged
    /// (v3/v2/old ship 0 — their right hand reads fine; only v4 defaults non-zero).
    ///
    /// DefaultExecutionOrder(65): AFTER the Animator + CastawayArmPose(50) + CastawayFingerCurl(60), BEFORE
    /// HeldAxeRig(100) — so the hand has its final corrected orientation before the axe seats onto it (the axe
    /// follows the corrected hand). The finger bones (curled at order 60) are children of the hand, so they ride
    /// this hand rotation rigidly (the grip stays intact).
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): authored editor-time by MovementCameraScene
    /// (AddRightWristPose) with the hand bone resolved from SkinnedMeshRenderer.bones + serialized into Boot.unity.
    /// STATIC STATE: instance fields only — no mutable runtime statics (StaticStateResetTests stays green).
    /// </summary>
    [DefaultExecutionOrder(65)]
    public class CastawayRightWristPose : MonoBehaviour
    {
        [Tooltip("The right HAND bone (mixamorig:RightHand). Wired editor-time from SkinnedMeshRenderer.bones.")]
        public Transform rightHand;

        [Tooltip("Additive LOCAL-euler offset (deg) on the right hand bone — corrects the v4 auto-rig's rolled " +
                 "right-hand bind frame so the palm mirrors the left. 0 = hand byte-unchanged. Default = the " +
                 "measured Inv(R_right)·mirror(R_left) correction; the Sponsor F9-dials it (WRIST target) by eye.")]
        public Vector3 wristEuler;

        void LateUpdate()
        {
            if (wristEuler == Vector3.zero) return; // byte-unchanged hand when not dialed (non-v4 path)
            if (rightHand != null) rightHand.localRotation = rightHand.localRotation * Quaternion.Euler(wristEuler);
        }
    }
}
