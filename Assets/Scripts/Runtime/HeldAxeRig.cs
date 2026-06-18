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
    /// VIGOROUS-LOCOMOTION CEILING CLAMP (86caa83wn — "the axe swings up in the player head when running"): the
    /// follow-the-arm choice is KEPT for WALK/IDLE (the Sponsor's locked WALK pose, byte-unchanged) — but the
    /// Mixamo RUN clip (Running.fbx) pumps the right arm UP near the head, and the Jump_running / Jump_idle
    /// clips do the same, so the rigidly-following axe rides INTO the head during RUN + JUMP. A light DAMP alone
    /// does NOT stop that macro into-head swing (the swing is a sustained pose, not jitter — ticket-confirmed),
    /// so we add a PER-STATE VERTICAL CEILING: ONLY while the character is RUNNING or AIRBORNE (read off
    /// <see cref="CastawayCharacter"/>), the followed hand WORLD-Y is soft-clamped so it cannot rise above a
    /// ceiling expressed RELATIVE TO THE SHOULDER bone (<see cref="shoulder"/>, the right upper-arm). The
    /// shoulder reference is itself a bone in the SAME hierarchy, so the ceiling RIDES the body's vertical
    /// motion automatically — the jump arc, the walk bob, and the ground-snap all move the shoulder, so the
    /// clamp never fights grounding and never pins the axe to a fixed world-Y. At WALK/IDLE the clamp is INERT
    /// (the followed hand passes through raw — the Sponsor's pose is untouched). The clamp ceiling +
    /// engage-state + softness are RUNTIME-DIALABLE on the F9 AxeNudgeTool so the Sponsor tunes the exact "below
    /// shoulder height" feel in the soak (his direct-tweak preference). Horizontal follow (X/Z) is NEVER
    /// clamped — only the vertical into-head swing.
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
                 "frame (86ca9qwvd: HAND-LOCAL, rotated by the RAW hand so it TRACKS the hand through every " +
                 "facing AND follows the arm's natural swing — 86ca9zcjn). cm-scale units rotated by the hand's " +
                 "rotation ONLY (never its lossyScale), so a nudge step is a sensible ~2 cm and the axe stays " +
                 "seated no matter which way the castaway turns. Field name kept (worldOffsetFromHand) so the " +
                 "serialized scene + the F9 AxeNudgeTool wiring carry forward.")]
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

        [Header("Vigorous-locomotion ceiling clamp (86caa83wn — 'axe swings into the head when running')")]
        [Tooltip("ENABLE the per-state vertical ceiling clamp. When ON, the followed hand WORLD-Y is soft-" +
                 "clamped — ONLY while the character is RUNNING or AIRBORNE — so the axe cannot rise above " +
                 "shoulder height into the head (the Mixamo RUN/JUMP clips pump the arm up near the head). At " +
                 "WALK/IDLE the clamp is INERT (the Sponsor's locked WALK pose is untouched). Default ON.")]
        public bool clampVigorousLocomotion = true;

        [Tooltip("The ceiling offset (world units) ABOVE THE SHOULDER bone the followed hand may rise to while " +
                 "RUNNING/AIRBORNE. The shoulder rides the body's vertical motion (bob/jump/ground-snap), so the " +
                 "ceiling tracks it automatically — this is a 'below shoulder height' value, so a NEGATIVE value " +
                 "keeps the axe BELOW the shoulder (the natural grip), and 0 caps it AT the shoulder. The default " +
                 "(-0.25) keeps the axe a clear ~25cm BELOW the shoulder so the run/jump arm-pump can't carry " +
                 "it up to the head (86caa83wn soak-refine 2026-06-18: -0.05 was too high — the shoulder sits " +
                 "only ~0.3u below the head, so a 5cm-below-shoulder ceiling was still near the head). " +
                 "Sponsor-dialable on the F9 nudge tool (CLAMP target) in the soak. The shipped default is set " +
                 "editor-time by MovementCameraScene.HeldAxeClampCeilingAboveShoulder (kept in sync with this).")]
        public float clampCeilingAboveShoulder = -0.25f;

        [Tooltip("Soft-clamp blend width (world units). The clamp is a SOFT knee, not a hard cap — within this " +
                 "band below the ceiling the followed Y eases toward the ceiling so the axe never POPS to a hard " +
                 "stop (which would read jerky). 0 = a hard clamp. ~0.12 gives a smooth shoulder-height tuck.")]
        public float clampSoftness = 0.12f;

        [Tooltip("The SHOULDER bone (right upper-arm, mixamorig:RightArm) the clamp ceiling is expressed " +
                 "relative to. Wired editor-time (serialized); a runtime fallback walks UP from the hand bone " +
                 "to find the upper-arm. If unresolved the clamp falls back to INERT (raw follow) so a missing " +
                 "wire can NEVER pin the axe to a wrong world-Y — fail safe toward the Sponsor's follow choice.")]
        public Transform shoulder;

        [Tooltip("The CastawayCharacter whose IsRunning / IsAirborne state gates the clamp. Wired editor-time " +
                 "(serialized); a runtime fallback resolves it from the parent chain. If unresolved the clamp is " +
                 "INERT (the axe follows raw — fail safe toward the Sponsor's locked WALK/IDLE pose).")]
        public CastawayCharacter character;

        // The damped followed hand pose (only used when followDamp > 0). Eased per-frame toward the LIVE raw
        // hand pose in WORLD space, so it tracks the swing with a small lag and CANNOT integrate/ratchet.
        private Vector3 _dampedPos;
        private Quaternion _dampedRot;
        private bool _dampInit;

        /// <summary>The current followed hand WORLD position the axe pivot rides (the raw hand, or the lightly
        /// damped hand when followDamp &gt; 0) — exposed so the PlayMode regression can assert the axe follows
        /// the hand's natural swing within tolerance with no cumulative drift (86ca9zcjn AC3).</summary>
        public Vector3 FollowPos { get; private set; }

        /// <summary>Whether the vigorous-locomotion ceiling clamp is ACTIVE this frame (86caa83wn): the clamp is
        /// enabled AND the character is RUNNING or AIRBORNE AND the shoulder reference resolved. Exposed so the
        /// PlayMode regression asserts the clamp engages ONLY for RUN/JUMP (and is INERT at WALK/IDLE), and so
        /// the F9 tool can surface the state.</summary>
        public bool ClampActiveThisFrame { get; private set; }

        void Awake()
        {
            // Fallback: if the hand wasn't wired editor-time, the bone is this object's parent (the axe is
            // serialized as a child of the right-hand bone). Defensive — the authored path always wires it.
            if (hand == null) hand = transform.parent;
            // Fallbacks for the clamp references (the authored path wires both; these keep the clamp working
            // on a runtime-built rig). The character is the CastawayCharacter up the parent chain; the shoulder
            // is the upper-arm bone above the hand (walk up: hand -> forearm -> upper-arm).
            if (character == null && hand != null) character = hand.GetComponentInParent<CastawayCharacter>();
            if (shoulder == null && hand != null) shoulder = ResolveShoulderFromHand(hand);
        }

        // Walk UP from the hand bone to the right upper-arm: hand(wrist) -> forearm(elbow) -> upper-arm(shoulder).
        // The Mixamo rig names them mixamorig:RightHand / RightForeArm / RightArm. Match the upper-arm by name
        // ("rightarm" but NOT "righthand"/"rightforearm"); fall back to the grandparent (2 levels up the bone
        // chain is the upper-arm on a standard skeleton) if no name matches.
        private static Transform ResolveShoulderFromHand(Transform handBone)
        {
            Transform t = handBone.parent;
            int hops = 0;
            while (t != null && hops < 4)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("rightarm") && !n.Contains("righthand") && !n.Contains("forearm")) return t;
                t = t.parent;
                hops++;
            }
            // Geometric fallback: the grandparent of the wrist is the upper-arm on a standard 3-bone arm chain.
            return handBone.parent != null ? handBone.parent.parent : null;
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

            // VIGOROUS-LOCOMOTION CEILING CLAMP (86caa83wn): ONLY while RUNNING or AIRBORNE, soft-clamp the
            // followed hand WORLD-Y so it can't ride above shoulder height into the head. WALK/IDLE pass through
            // raw (the Sponsor's locked pose untouched). The ceiling is expressed relative to the shoulder bone
            // so it tracks the body's vertical motion (bob/jump/ground-snap) — never a fixed world-Y. Only the Y
            // is touched; X/Z (the horizontal arm-swing follow) is never clamped. Fail-safe: if the clamp is
            // disabled, the character/shoulder are unresolved, or the state isn't vigorous, the clamp is INERT.
            ClampActiveThisFrame = false;
            if (clampVigorousLocomotion && shoulder != null && character != null &&
                (character.IsRunning || character.IsAirborne))
            {
                float ceiling = shoulder.position.y + clampCeilingAboveShoulder;
                followPos.y = SoftClampMax(followPos.y, ceiling, clampSoftness);
                ClampActiveThisFrame = true;
            }

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

        /// <summary>
        /// PURE soft-ceiling clamp (the unit-testable core of the 86caa83wn into-head fix). Returns
        /// <paramref name="value"/> unchanged while it is well below <paramref name="ceiling"/>; eases it
        /// toward the ceiling within a <paramref name="softness"/>-wide knee; and never returns above the
        /// ceiling. A soft knee (not a hard cap) so the axe doesn't POP to a hard stop as the run arm-pump
        /// approaches the head (which would read jerky). softness ≤ 0 → a hard max-clamp. Static +
        /// dependency-free so the EditMode/PlayMode guards assert "a hand-Y pumped above the shoulder ceiling
        /// is brought DOWN to (or below) it, while a hand-Y below the ceiling is left untouched" with no rig.
        /// </summary>
        public static float SoftClampMax(float value, float ceiling, float softness)
        {
            if (value <= ceiling - Mathf.Max(0f, softness)) return value; // well below the ceiling — untouched
            if (softness <= 0f) return Mathf.Min(value, ceiling);         // hard clamp
            // Within the soft knee (ceiling - softness .. +inf): map the excess through a saturating curve that
            // approaches the ceiling asymptotically, so the result is ALWAYS < ceiling and C1-smooth at the knee.
            // Let x = (value - (ceiling - softness)) / softness  (0 at the knee, grows past 1 above the ceiling).
            // result = (ceiling - softness) + softness * (1 - exp(-x))   → approaches the ceiling, never exceeds.
            float kneeBase = ceiling - softness;
            float x = (value - kneeBase) / softness;
            return kneeBase + softness * (1f - Mathf.Exp(-x));
        }
    }
}
