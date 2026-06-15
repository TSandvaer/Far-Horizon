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
    /// localScale × the bone's lossyScale (reads as a hero hatchet). Only the POSITION + ROTATION are
    /// overridden in world space here; scale rides the bone as before.
    ///
    /// RE-SOAK #3 (86ca8rdkp — "the axe in hand position changes when I walk"): the imported Walk clip swings
    /// the hand bone ~0.9u peak-to-peak (a ~0.64u vertical bob), and the rigid ~0.8u axe rode the whole swing
    /// → it visibly shifted while walking. FIX (swingStabilize / a GRIP ANCHOR): instead of riding the raw
    /// swinging hand, the axe rides a grip anchor held in the BODY-ROOT local frame, which translates + YAWS
    /// with the body (so the haft still turns with facing — the soakfix8 contract) but does NOT arm-swing. The
    /// anchor eases slowly toward the hand's average body-local pose, so a real re-grip is honored but the
    /// per-step walk swing is removed (-walkAxeTrace: axe peak-to-peak 0.93u → 0.18u). Stabilizing in the
    /// body-root frame (not WORLD) is load-bearing: a world-space low-pass LAGS locomotion and makes the
    /// root-local swing WORSE (measured 0.93u → 1.5u).
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

        [Header("Swing stabilization (86ca8rdkp re-soak #3 — 'the axe position changes when I walk')")]
        [Tooltip("How much to STABILIZE the held axe against the walk arm-swing. -walkAxeTrace proved the hand " +
                 "bone swings ~0.9u peak-to-peak through the Walk cycle AND rotates — and the rigid ~0.8u axe " +
                 "rode BOTH, so its rendered position swept >1u (the Sponsor's 'the axe position changes when I " +
                 "walk'). This low-passes the hand POSE (position AND rotation) the axe follows: the axe tracks " +
                 "the hand's SETTLED pose (a believable steady grip) — high-frequency per-step swing removed, " +
                 "while LOW-frequency body-facing turns still pass through (the haft still turns when you turn). " +
                 "0 = follow the raw swing (the bug); 1 = fully stabilized.")]
        [Range(0f, 1f)] public float swingStabilize = 1f;
        [Tooltip("How fast the GRIP ANCHOR (the body-local hand rest pose the steady axe rides) eases toward " +
                 "the hand's CURRENT body-local pose. Small (per-second) = the anchor barely tracks the per-step " +
                 "swing → a rock-steady grip; it still drifts slowly toward the hand's average pose so a deliberate " +
                 "re-grip / pose change is honored. The anchor is in the BODY-ROOT local frame, so the axe turns " +
                 "with body FACING (the soakfix8 contract) but does NOT bob with the walk swing.")]
        public float anchorTrackPerSec = 0.12f;

        [Tooltip("The reference frame the grip is anchored IN — the avatar/body root (which translates + yaws " +
                 "with the body but does NOT arm-swing). The grip anchor lives in THIS frame, so the axe rides " +
                 "the body (facing turns pass through) without the per-step arm-swing. Wired editor-time to the " +
                 "CastawayCharacter transform; falls back to the first CastawayCharacter ancestor.")]
        public Transform stabilizeFrame;

        // The GRIP ANCHOR — the hand's rest pose in the body-root local frame. The steady axe rides this anchor
        // (transformed by the live body root), so the per-step swing is removed while body facing passes through.
        // The anchor eases slowly toward the live hand body-local pose (anchorTrackPerSec) so a real re-grip is
        // honored but the fast walk swing is not.
        private Vector3 _anchorLocalPos;
        private Quaternion _anchorLocalRot;
        private bool _anchorInit;

        /// <summary>The current followed (stabilized) hand WORLD position the axe pivot rides — exposed so the
        /// PlayMode regression can assert the stabilization removes the per-step swing (the axe follows the
        /// settled grip, not the raw swing).</summary>
        public Vector3 FollowPos { get; private set; }

        void Awake()
        {
            // Fallback: if the hand wasn't wired editor-time, the bone is this object's parent (the axe is
            // serialized as a child of RightHand_010). Defensive — the authored path always wires it.
            if (hand == null) hand = transform.parent;
            // Fallback stabilize frame: the avatar root carrying CastawayCharacter (translates with locomotion,
            // does not arm-swing). If unwired, walk up to the first ancestor with a CastawayCharacter.
            if (stabilizeFrame == null)
            {
                var cc = GetComponentInParent<CastawayCharacter>();
                if (cc != null) stabilizeFrame = cc.transform;
            }
        }

        void LateUpdate()
        {
            if (hand == null) return;

            Vector3 rawHandPos = hand.position;
            Quaternion rawHandRot = hand.rotation;
            Vector3 followPos;
            Quaternion followRot;

            // STABILIZE the grip the axe rides (re-soak #3). The axe is rigid + ~0.8u long, so BOTH the hand's
            // per-step position swing AND its rotation swing sweep the axe's rendered position (measured: the
            // hand swings ~0.9u peak-to-peak through the Walk cycle, dominated by a ~0.64u vertical bob). A
            // low-pass filter can't strongly attenuate a swing whose period (~0.6s) is near its own time
            // constant without big lag, so instead we ride a GRIP ANCHOR: the hand's rest pose held in the
            // BODY-ROOT local frame. The body root translates + yaws with the body but does NOT arm-swing, so an
            // anchor in its frame gives a rock-steady grip that STILL turns with body facing (the soakfix8
            // rotation-tracks-hand contract). The anchor eases SLOWLY (anchorTrackPerSec) toward the hand's
            // current body-local pose so a deliberate re-grip is honored, but the fast walk swing is not.
            float t = Mathf.Clamp01(swingStabilize);
            Transform frame = stabilizeFrame;
            if (frame == null || t <= 0f)
            {
                // No stabilize frame (or disabled): follow the raw hand (the pre-re-soak behavior).
                followPos = rawHandPos; followRot = rawHandRot;
            }
            else
            {
                // Hand pose expressed in the body-root local frame (swing only — locomotion removed).
                Vector3 localPos = frame.InverseTransformPoint(rawHandPos);
                Quaternion localRot = Quaternion.Inverse(frame.rotation) * rawHandRot;
                if (!_anchorInit)
                {
                    _anchorLocalPos = localPos; _anchorLocalRot = localRot; _anchorInit = true;
                }
                else
                {
                    // Ease the anchor SLOWLY toward the live body-local hand pose (frame-rate-independent). A
                    // small per-second rate barely follows the fast walk swing → steady grip; it still converges
                    // on the average rest pose so a re-grip / pose change is eventually honored.
                    float a = 1f - Mathf.Exp(-anchorTrackPerSec * Time.deltaTime);
                    _anchorLocalPos = Vector3.Lerp(_anchorLocalPos, localPos, a);
                    _anchorLocalRot = Quaternion.Slerp(_anchorLocalRot, localRot, a);
                }
                // The followed body-local pose blends the raw hand with the steady anchor by swingStabilize.
                Vector3 followLocalPos = Vector3.Lerp(localPos, _anchorLocalPos, t);
                Quaternion followLocalRot = Quaternion.Slerp(localRot, _anchorLocalRot, t);
                followPos = frame.TransformPoint(followLocalPos);
                followRot = frame.rotation * followLocalRot;
            }

            // POSITION in WORLD space (immune to the bone's lossyScale) + ROTATION hand-relative off the
            // STABILIZED hand rotation. The VALUES driven are world-units / hand-relative so the nudge tool
            // edits sensible numbers.
            transform.position = followPos + worldOffsetFromHand;
            transform.rotation = followRot * Quaternion.Euler(relEuler);
            FollowPos = followPos;
        }
    }
}
