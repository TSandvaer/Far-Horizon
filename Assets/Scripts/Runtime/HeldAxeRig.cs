using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Per-frame driver that seats the HELD hatchet at the chibi's right hand. It SPLITS the two pose channels so
    /// each rides the right frame — but as of 86ca9zcjn the axe now rides the RAW HAND BONE so it FOLLOWS the
    /// arm's natural swing during locomotion (the Sponsor's design choice, soak 6bcc1bc):
    ///
    ///   - POSITION → a HAND-LOCAL offset, rotated by the RAW hand's rotation, re-applied every frame:
    ///         transform.position = hand.position + hand.rotation * offsetFromHand   (cm-scale, hand-LOCAL)
    ///     The offset is rotated by the hand rotation, so it TRACKS THE HAND THROUGH EVERY FACING — the axe
    ///     stays seated in the grip no matter which way the castaway turns (86ca9qwvd). Because it rides the RAW
    ///     hand position, the axe also FOLLOWS the per-step arm-swing + bob (86ca9zcjn — the Sponsor wants the
    ///     held axe to swing WITH the arm). It is NOT hand.TransformPoint(offset) — that would re-apply the
    ///     bone's lossyScale and blow the offset up to metres (the §FBX lossy-bone trap); we rotate by
    ///     hand.rotation ONLY, never the scale, so the cm offset stays cm (the F9 nudge stays ~2 cm/click).
    ///
    ///   - ROTATION → HAND-RELATIVE, re-applied every frame:
    ///         transform.rotation = hand.rotation * Quaternion.Euler(relEuler)
    ///     so the haft TURNS WITH the hand through every facing (the soakfix8 fix the Sponsor approved — KEPT).
    ///     Inverse(hand.rotation) * transform.rotation == Euler(relEuler) is invariant across facings, which is
    ///     exactly the rotation-tracks-hand contract.
    ///
    /// SCALE is left to the hierarchy: the axe stays a CHILD of the hand bone, so its world size is
    /// localScale × the bone's lossyScale (reads as a hero hatchet). Only the POSITION + ROTATION are
    /// overridden in world space here; scale rides the bone as before.
    ///
    /// FOLLOW-THE-ARM (86ca9zcjn — Sponsor design choice, soak 6bcc1bc): the Sponsor explicitly REVERSED the
    /// old "the axe changes position when I walk" preference — he now WANTS the held axe to follow the right
    /// arm's natural swing during walk/run/jump (a stabilized axe stays put → reads DETACHED from the hand
    /// mid-stride). So the prior SWING-STABILIZER / grip-anchor (86ca8rdkp swingStabilize) AND the
    /// vertical-decouple bounce/ratchet fix (86ca9ykp0) are REMOVED: the axe rides the RAW hand bone. The
    /// raw hand returns to its pose every walk cycle (no anchor integration, no eased accumulation), so the
    /// follow is BOUNDED BY CONSTRUCTION — there is no monotonic ratchet (86ca9zcjn AC3). The facing fix
    /// (86ca9xz00 / 86ca9qwvd) is KEPT verbatim — facing/turning still passes through immediately because the
    /// raw hand already carries the facing yaw.
    ///
    /// LIGHT DAMP (86ca9zcjn AC2 — SMOOTH, not floppy): an OPTIONAL low-pass on the followed hand POSE
    /// (<see cref="followDamp"/>) is available to de-JITTER without re-locking the swing. It defaults to 0 (raw
    /// follow — the full per-step swing is visible, the Sponsor's choice). A SMALL positive value damps
    /// high-frequency jitter while leaving the per-step swing clearly visible (it is NOT the old fully-locked
    /// grip-anchor — there is no slow body-frame integration, just a per-frame ease toward the live raw hand,
    /// so it cannot ratchet). Keep it small; do NOT crank it up to re-lock the axe (the ticket: "if it reads
    /// wild, damp it, don't lock it").
    ///
    /// RUN INTO-HEAD — CALM THE ARM, NEVER MOVE THE AXE OFF THE HAND (86caa83wn soak #2, 2026-06-18): an earlier
    /// pass added a world-Y soft-clamp HERE that moved the AXE down independently of the hand while RUNNING/
    /// AIRBORNE. That DETACHED the axe from the hand during the run arm-swing (the Sponsor's exact soak #2
    /// report: "when i run the axe is no longer in the hand") — moving the axe-Y but not the hand-X/Z pulls the
    /// haft out of the grip. That clamp is REMOVED. The axe now rides the hand RIGIDLY at ALL times — walk, idle,
    /// run, jump — exactly like the Sponsor-approved walk/idle seat. The run into-head problem is solved on the
    /// OTHER side: <see cref="CastawayArmPose"/> reduces the RIGHT-arm vertical swing amplitude while
    /// <see cref="CastawayCharacter.IsRunning"/> (a lowered-arm additive offset weighted by a smoothed run value),
    /// so the HAND itself stays lower during the run — and because the gripped axe follows the hand, the axe
    /// stays below the head AND stays in the hand. The run-lower amount is RUNTIME-DIALABLE on the F9 AxeNudgeTool
    /// (the RUN target) so the Sponsor tunes the running carry himself in the soak (his direct-tweak preference).
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

        [Tooltip("POSITION channel — the axe is seated at hand.position + hand.rotation * this offset every " +
                 "frame. This offset is HAND-LOCAL (expressed in the hand bone's own frame): it is rotated by " +
                 "the RAW hand each frame so it TRACKS the hand through every facing AND follows the arm's " +
                 "natural swing — 86ca9zcjn. cm-scale units rotated by the hand's rotation ONLY (never its " +
                 "lossyScale), so a nudge step is a sensible ~2 cm and the axe stays seated no matter which way " +
                 "the castaway turns OR how it was acquired (spawn-in-hand == picked-up). 86caa83wn: dialed, " +
                 "displayed AND baked in THIS hand-local frame end to end (no WORLD-frame round-trip) so the " +
                 "Sponsor's F9 dial reproduces at every facing. Field name kept (worldOffsetFromHand) so the " +
                 "serialized scene + the F9 AxeNudgeTool wiring carry forward — but the value is HAND-LOCAL.")]
        public Vector3 worldOffsetFromHand = new Vector3(0.003f, -0.017f, 0.009f);

        [Tooltip("ROTATION channel — the axe's rotation is hand.rotation * Euler(this), HAND-RELATIVE, so " +
                 "the haft turns WITH the hand through every facing (the soakfix8 rotation-tracks-hand fix).")]
        public Vector3 relEuler = new Vector3(4.1f, 95.8f, -56.1f);

        [Header("Follow the arm's natural swing (86ca9zcjn — Sponsor design choice, soak 6bcc1bc)")]
        [Tooltip("OPTIONAL light low-pass on the followed hand POSE (per-second smoothing rate), to DE-JITTER " +
                 "without re-locking the swing (86ca9zcjn AC2 — SMOOTH, not floppy). 0 = follow the RAW hand " +
                 "(the full per-step arm-swing is visible — the Sponsor's choice, the default). A SMALL positive " +
                 "value (e.g. ~25/s) eases the followed pose toward the live raw hand per frame, shaving " +
                 "high-frequency jitter while leaving the per-step swing clearly visible. It eases toward the " +
                 "LIVE hand (no slow body-frame anchor integration), so it CANNOT ratchet. Do NOT crank it up " +
                 "to re-lock the axe — 'if it reads wild, damp it, don't lock it'.")]
        public float followDamp = 0f;

        // The damped followed hand pose (only used when followDamp > 0). Eased per-frame toward the LIVE raw
        // hand pose in WORLD space, so it tracks the swing with a small lag and CANNOT integrate/ratchet.
        private Vector3 _dampedPos;
        private Quaternion _dampedRot;
        private bool _dampInit;

        /// <summary>The current followed hand WORLD position the axe pivot rides (the raw hand, or the lightly
        /// damped hand when followDamp &gt; 0) — exposed so the PlayMode regression can assert the axe follows
        /// the hand's natural swing within tolerance with no cumulative drift (86ca9zcjn AC3).</summary>
        public Vector3 FollowPos { get; private set; }

        void Awake()
        {
            // Fallback: if the hand wasn't wired editor-time, the bone is this object's parent (the axe is
            // serialized as a child of the right-hand bone). Defensive — the authored path always wires it.
            if (hand == null) hand = transform.parent;
        }

        void LateUpdate()
        {
            if (hand == null) return;

            // FOLLOW THE RAW HAND (86ca9zcjn): the axe rides the hand bone's live world pose, so it swings WITH
            // the arm during walk/run/jump (the Sponsor's design choice). The raw hand returns to its pose every
            // walk cycle, so the follow is bounded by construction — no anchor integration, no ratchet. The
            // facing yaw is already carried by the raw hand, so turning passes through immediately (86ca9xz00
            // contract preserved — the haft turns with the hand).
            Vector3 followPos = hand.position;
            Quaternion followRot = hand.rotation;

            // OPTIONAL LIGHT DAMP (AC2): if enabled, ease the followed pose toward the LIVE raw hand per frame
            // (frame-rate-independent). This shaves high-frequency jitter while leaving the per-step swing
            // visible. It eases toward the LIVE hand (not a slow body-frame anchor), so it lags the swing
            // slightly but CANNOT integrate a baseline / hip-lift → no ratchet (the 86ca9ykp0 ratchet was the
            // body-frame anchor; that mechanism is gone). Default followDamp = 0 → pure raw follow.
            if (followDamp > 0f)
            {
                if (!_dampInit)
                {
                    _dampedPos = followPos; _dampedRot = followRot; _dampInit = true;
                }
                else
                {
                    float a = 1f - Mathf.Exp(-followDamp * Time.deltaTime);
                    _dampedPos = Vector3.Lerp(_dampedPos, followPos, a);
                    _dampedRot = Quaternion.Slerp(_dampedRot, followRot, a);
                }
                followPos = _dampedPos; followRot = _dampedRot;
            }

            // NO axe-side vertical clamp (86caa83wn soak #2): the axe rides the hand RIGIDLY. The earlier world-Y
            // clamp HERE moved the axe down independently of the hand → detached it from the grip during the run
            // arm-swing ("when i run the axe is no longer in the hand"). The run into-head problem is solved on
            // the ARM side (CastawayArmPose lowers the right arm while running) so the HAND stays low and the
            // gripped axe follows it — staying both below the head AND in the hand.

            // POSITION in HAND-LOCAL space (86ca9qwvd): rotate the cm-scale offset by the hand rotation so it
            // TRACKS the hand through every facing — the axe stays seated in the grip no matter which way the
            // castaway turns. We rotate by followRot (the SAME hand rotation the ROTATION channel uses, so
            // position + rotation ride the identical frame), NOT by the bone's lossyScale — a pure rotation
            // preserves the offset's magnitude, so the cm offset stays cm and a nudge step is still ~2 cm
            // (NOT hand.TransformPoint, which would re-apply the §FBX lossy scale).
            // ROTATION hand-relative off the hand rotation (the soakfix8 channel, unchanged).
            transform.position = followPos + followRot * worldOffsetFromHand;
            transform.rotation = followRot * Quaternion.Euler(relEuler);
            FollowPos = followPos;
        }
    }
}
