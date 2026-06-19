using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Generalized per-frame driver that seats ANY held tool/weapon (axe, knife, sword, spear, …) at
    /// the castaway's hand bone via ONE shared seat system (ticket 86cabh907, Route A weapon set). This
    /// is the generalization of the soak-locked HELD-AXE rig: the seating math is identical, so any
    /// family item mounts to the hand via the SAME locked seat the axe uses — no per-weapon hold logic.
    ///
    /// <see cref="HeldAxeRig"/> is a thin back-compat subclass (the axe's serialized scene wiring + the
    /// soak-tuning tests reference HeldAxeRig by name); it adds NOTHING — the seat behaviour lives here.
    ///
    /// The seat (carried verbatim from the soak-locked HeldAxeRig — Sponsor soak #5, build 2d90a68):
    ///   - POSITION → a HAND-LOCAL offset, rotated by the RAW hand's rotation every frame:
    ///         transform.position = hand.position + hand.rotation * seatOffsetFromHand   (cm-scale, hand-LOCAL)
    ///     so it TRACKS the hand through every facing AND follows the arm's natural swing (it rides the
    ///     RAW hand bone). It is NOT hand.TransformPoint(offset) — that would re-apply the bone's lossyScale
    ///     and blow the offset up to metres (the §FBX lossy-bone trap); we rotate by hand.rotation ONLY.
    ///   - ROTATION → HAND-RELATIVE, re-applied every frame:
    ///         transform.rotation = hand.rotation * Quaternion.Euler(seatEuler)
    ///     so the haft TURNS WITH the hand through every facing.
    ///
    /// SCALE rides the hierarchy (the tool stays a CHILD of the hand bone); only POSITION + ROTATION are
    /// world-driven here. FOLLOW-THE-ARM: the tool rides the RAW hand, so it swings WITH the arm during
    /// walk/run/jump (the Sponsor's design choice, 86ca9zcjn). The raw hand returns to its pose every walk
    /// cycle → the follow is BOUNDED by construction (no ratchet). An OPTIONAL light low-pass (<see
    /// cref="followDamp"/>, default 0) de-jitters WITHOUT re-locking the swing.
    ///
    /// DefaultExecutionOrder(100): runs AFTER CastawayCharacter (default 0) so the body-yaw + the animated
    /// hand pose for THIS frame are final before we read the hand transform — no one-frame facing lag.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HeldToolRig : MonoBehaviour
    {
        [Tooltip("The hand bone the tool is seated at. Wired editor-time (serialized); Awake searches the " +
                 "parent chain as a fallback.")]
        public Transform hand;

        [Tooltip("POSITION channel — the tool is seated at hand.position + hand.rotation * this offset every " +
                 "frame. HAND-LOCAL (the hand bone's own frame), cm-scale, rotated by the RAW hand each frame " +
                 "so it tracks the hand through every facing. (Field name kept as worldOffsetFromHand on the " +
                 "axe subclass for serialization/F9-tool continuity; the value is HAND-LOCAL end to end.)")]
        public Vector3 seatOffsetFromHand = new Vector3(0.1312f, 0.1409f, 0.0593f);

        [Tooltip("ROTATION channel — the tool's rotation is hand.rotation * Euler(this), HAND-RELATIVE, so " +
                 "the haft turns WITH the hand through every facing.")]
        public Vector3 seatEuler = new Vector3(12.0f, -8.0f, -82.0f);

        [Header("Follow the arm's natural swing (86ca9zcjn — Sponsor design choice)")]
        [Tooltip("OPTIONAL light low-pass on the followed hand POSE (per-second smoothing rate), to DE-JITTER " +
                 "without re-locking the swing. 0 = follow the RAW hand (the full per-step arm-swing is visible " +
                 "— the Sponsor's choice, the default). A SMALL positive value eases toward the LIVE hand per " +
                 "frame (cannot ratchet). Do NOT crank it up to re-lock — 'if it reads wild, damp it, don't lock it'.")]
        public float followDamp = 0f;

        private Vector3 _dampedPos;
        private Quaternion _dampedRot;
        private bool _dampInit;

        /// <summary>The current followed hand WORLD position the tool pivot rides (the raw hand, or the lightly
        /// damped hand when followDamp &gt; 0) — exposed so the PlayMode regression can assert the tool follows
        /// the hand's natural swing within tolerance with no cumulative drift.</summary>
        public Vector3 FollowPos { get; private set; }

        protected virtual void Awake()
        {
            // Fallback: if the hand wasn't wired editor-time, the bone is this object's parent (the tool is
            // serialized as a child of the hand bone). Defensive — the authored path always wires it.
            if (hand == null) hand = transform.parent;
        }

        protected virtual void LateUpdate()
        {
            if (hand == null) return;

            // FOLLOW THE RAW HAND: the tool rides the hand bone's live world pose, so it swings WITH the arm
            // during walk/run/jump. The raw hand returns to its pose every walk cycle → bounded by construction,
            // no ratchet; the facing yaw is already carried by the raw hand, so turning passes through immediately.
            Vector3 followPos = hand.position;
            Quaternion followRot = hand.rotation;

            if (followDamp > 0f)
            {
                if (!_dampInit) { _dampedPos = followPos; _dampedRot = followRot; _dampInit = true; }
                else
                {
                    float a = 1f - Mathf.Exp(-followDamp * Time.deltaTime);
                    _dampedPos = Vector3.Lerp(_dampedPos, followPos, a);
                    _dampedRot = Quaternion.Slerp(_dampedRot, followRot, a);
                }
                followPos = _dampedPos; followRot = _dampedRot;
            }

            // POSITION in HAND-LOCAL space: rotate the cm-scale offset by the hand rotation so it TRACKS the
            // hand through every facing. We rotate by followRot (the SAME hand rotation the ROTATION channel
            // uses) — NOT by the bone's lossyScale (that would re-apply the §FBX lossy scale and blow the
            // offset up to metres). ROTATION hand-relative off the hand rotation.
            transform.position = followPos + followRot * seatOffsetFromHand;
            transform.rotation = followRot * Quaternion.Euler(seatEuler);
            FollowPos = followPos;
        }
    }
}
