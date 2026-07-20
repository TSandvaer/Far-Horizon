using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// POST-ANIMATION per-foot YAW offset for the castaway (ticket 86catvb6u — the Sponsor's chosen fix for the
    /// v4 "pigeon-toed walk" defect). A sibling driver to <see cref="CastawayArmPose"/> / <see cref="CastawayFingerCurl"/>:
    /// it runs in LateUpdate AFTER the Animator has written the clip pose, then composes a small OUTWARD yaw (about
    /// the character-vertical axis) onto each foot bone so the visible toes read straight.
    ///
    /// WHY (diagnose-via-trace, 86catvb6u): CastawayV4DefectDiag measured that v4's foot BONE pose is IDENTICAL to
    /// v3's at the live-ticked Walk state (both instruments — SampleAnimation + a real Animator tick — agree), so the
    /// retarget/clip/rig PATH is clean. v4's BIND rest feet are splayed +/-16deg (v3 +/-5deg) but the walk/idle clips
    /// fully override the bone. The residual pigeon read is v4's chunky foot MESH geometry vs its bone — not fixable
    /// by re-rig OR clip. The Sponsor chose a RUNTIME COUNTER-ROTATE he dials by eye (F9 nudge tool, FOOT-YAW target):
    /// this offset yaws each foot outward until the toes read straight, then the dialed value is baked here.
    ///
    /// IDIOM (procedural-animation-verbs.md — the additive-LateUpdate offset chain): NOT a new Animator clip/state/
    /// layer/mask. The offset is composed onto the clip pose each LateUpdate. Zero at <see cref="footYawDeg"/>==0 =>
    /// the foot pose is byte-unchanged (v3/v2/old ship 0 => their feet are untouched; only v4 defaults non-zero).
    /// The yaw is applied about the WORLD-UP axis expressed in the foot's PARENT frame (a clean vertical yaw, no
    /// bone-local-axis guess — the measured-axis discipline), MIRRORED left/right so both toes swing outward together.
    ///
    /// DefaultExecutionOrder(70): AFTER the Animator + CastawayArmPose(50) + CastawayFingerCurl(60). The feet have no
    /// downstream seat consumer (unlike the hand's HeldAxeRig@100), and the yaw is about vertical so it does NOT
    /// affect the ground-snap (CastawayCharacter grounds the avatar ROOT-Y, independent of foot yaw).
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): authored editor-time by MovementCameraScene
    /// (AddFootYaw) with the foot bones resolved from SkinnedMeshRenderer.bones + serialized into Boot.unity.
    /// STATIC STATE: instance fields only — no mutable runtime statics (StaticStateResetTests stays green).
    /// </summary>
    [DefaultExecutionOrder(70)]
    public class CastawayFootYaw : MonoBehaviour
    {
        [Tooltip("Left + right foot bones (wired editor-time from SkinnedMeshRenderer.bones).")]
        public Transform leftFoot;
        public Transform rightFoot;

        [Tooltip("Per-foot yaw (deg) about the character-vertical, MIRRORED L/R. SIGN (truthful, as the Sponsor " +
                 "judged it): NEGATIVE = toes OUTWARD (un-pigeons the toes — the direction his dialed −15 " +
                 "straightened them); POSITIVE = toes inward. 0 = feet byte-unchanged. Default = the Sponsor's " +
                 "F9-dialed −15.0 (FOOT-YAW target), baked as-dialed so the shipped visual == what he settled on.")]
        public float footYawDeg;

        void LateUpdate()
        {
            if (Mathf.Approximately(footYawDeg, 0f)) return; // byte-unchanged feet when not dialed (non-v4 path)
            ApplyYaw(leftFoot, +footYawDeg);
            ApplyYaw(rightFoot, -footYawDeg);
        }

        // Yaw the foot about the WORLD-UP axis (a clean vertical yaw) expressed in the foot's PARENT local frame,
        // so it composes on the clip pose without assuming which of the foot bone's own local axes is "up".
        private static void ApplyYaw(Transform foot, float deg)
        {
            if (foot == null || foot.parent == null) return;
            Vector3 upInParent = foot.parent.InverseTransformDirection(Vector3.up);
            foot.localRotation = Quaternion.AngleAxis(deg, upInParent) * foot.localRotation;
        }
    }
}
