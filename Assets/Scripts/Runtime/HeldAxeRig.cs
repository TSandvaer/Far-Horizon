using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Per-frame driver that seats the HELD hatchet at the chibi's right hand (ticket 86ca8ce6y — SOAKFIX9,
    /// the held-axe POSITION fix). It SPLITS the two pose channels so each rides the right frame:
    ///
    ///   - POSITION → a WORLD-space offset from the hand bone, re-applied every frame:
    ///         transform.position = hand.position + worldOffsetFromHand   (WORLD units)
    ///     The offset is in WORLD units, so the F9 nudge moves the axe a SENSIBLE world distance
    ///     (~0.02u ≈ 2 cm per click). The prior soakfix8 posed POSITION as a localPosition on the bone —
    ///     but RightHand_010 carries a ~267× lossyScale (probe-verified, unity-conventions.md §FBX), so a
    ///     0.02 LOCAL step became ~5.3 WORLD units: one nudge click flung the axe ~5 m off-screen (the
    ///     Sponsor's exact soakfix9 bug). Driving position in WORLD space sidesteps the bone's lossy scale
    ///     entirely — the 267× never touches a world-space setter.
    ///
    ///   - ROTATION → HAND-RELATIVE, re-applied every frame:
    ///         transform.rotation = hand.rotation * Quaternion.Euler(relEuler)
    ///     so the haft TURNS WITH the hand through every facing (the soakfix8 fix the Sponsor approved —
    ///     KEPT). Inverse(hand.rotation) * transform.rotation == Euler(relEuler) is invariant across facings,
    ///     which is exactly the rotation-tracks-hand contract.
    ///
    /// SCALE is left to the hierarchy: the axe stays a CHILD of the hand bone, so its world size is
    /// localScale × the bone's 267× lossyScale (≈1.0u — unchanged, reads as a hero hatchet). Only the
    /// POSITION + ROTATION are overridden in world space here; scale rides the bone as before.
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): the axe + this component are authored
    /// editor-time (MovementCameraScene.AttachHeroAxeToHand) and SERIALIZE into Boot.unity riding the bone.
    /// AttachHeroAxeToHand also bakes an equivalent STATIC localPosition/localRotation so a static editor
    /// load (the EditMode bounds guards) sees the same seated pose this driver re-asserts at runtime.
    ///
    /// DefaultExecutionOrder(100): runs AFTER CastawayCharacter (default order 0) so the body-yaw + the
    /// animated hand pose for THIS frame are final before we read the hand transform — no one-frame facing lag.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HeldAxeRig : MonoBehaviour
    {
        [Tooltip("The right-hand bone the axe is seated at. Wired editor-time (serialized); Awake searches " +
                 "the parent chain as a fallback.")]
        public Transform hand;

        [Tooltip("POSITION channel — the axe is seated at hand.position + this WORLD-space offset every " +
                 "frame. WORLD units, so a nudge step is a sensible ~2 cm (NOT a 267×-lossy-scale local step).")]
        public Vector3 worldOffsetFromHand = new Vector3(0.003f, -0.017f, 0.009f);

        [Tooltip("ROTATION channel — the axe's rotation is hand.rotation * Euler(this), HAND-RELATIVE, so " +
                 "the haft turns WITH the hand through every facing (the soakfix8 rotation-tracks-hand fix).")]
        public Vector3 relEuler = new Vector3(4.1f, 95.8f, -56.1f);

        void Awake()
        {
            // Fallback: if the hand wasn't wired editor-time, the bone is this object's parent (the axe is
            // serialized as a child of RightHand_010). Defensive — the authored path always wires it.
            if (hand == null) hand = transform.parent;
        }

        void LateUpdate()
        {
            if (hand == null) return;
            // POSITION in WORLD space (immune to the bone's 267× lossyScale) + ROTATION hand-relative
            // (tracks the bone through turns). Setting world position/rotation on a child re-solves the
            // local transform internally, but the VALUES we drive are world-units / hand-relative — so the
            // nudge tool edits sensible numbers and the 267× scale never multiplies a position step.
            transform.position = hand.position + worldOffsetFromHand;
            transform.rotation = hand.rotation * Quaternion.Euler(relEuler);
        }
    }
}
