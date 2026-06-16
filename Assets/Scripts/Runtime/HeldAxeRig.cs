using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Per-frame driver that seats the HELD hatchet at the chibi's right hand (ticket 86ca8ce6y — SOAKFIX9,
    /// the held-axe POSITION fix; 86ca9qwvd — the HAND-LOCAL space fix). It SPLITS the two pose channels so
    /// each rides the right frame:
    ///
    ///   - POSITION → a HAND-LOCAL offset, rotated by the hand's rotation, re-applied every frame:
    ///         transform.position = hand.position + hand.rotation * offsetFromHand   (cm-scale, hand-LOCAL)
    ///     The offset is rotated by the (stabilized) hand rotation, so it TRACKS THE HAND THROUGH EVERY
    ///     FACING — the axe stays seated in the grip no matter which way the castaway turns. (86ca9qwvd —
    ///     the real bug THIS wave: soakfix9 applied the offset as a RAW WORLD vector that did NOT rotate
    ///     with the hand, so it was only correct at the spawn facing; turning the character left the axe
    ///     behind in world space — see HeldAxeStaysSeatedAcrossFacingsPlayModeTests.) The offset stays in
    ///     sensible cm units, so the F9 nudge still moves the axe ~2 cm per click (a pure rotation preserves
    ///     the step magnitude). It is NOT hand.TransformPoint(offset) — that would re-apply the bone's
    ///     ~267×/1.8× lossyScale and blow the offset up to metres (the §FBX lossy-bone trap); we rotate by
    ///     hand.rotation ONLY, never the scale, so the cm offset stays cm.
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
    /// swinging hand, the axe rides a grip anchor held in a BODY-LOCAL frame, which translates + YAWS
    /// with facing (so the haft still turns with facing — the soakfix8 contract) but does NOT arm-swing. The
    /// anchor eases slowly toward the hand's average body-local pose, so a real re-grip is honored but the
    /// per-step walk swing is removed (-walkAxeTrace: axe peak-to-peak 0.93u → 0.18u). Stabilizing in the
    /// body frame (not WORLD) is load-bearing: a world-space low-pass LAGS locomotion and makes the
    /// root-local swing WORSE (measured 0.93u → 1.5u).
    ///
    /// FACING-FRAME FIX (86ca9xz00 — "the held axe LAGS facing / seated only one direction"): the grip-anchor
    /// frame (<see cref="stabilizeFrame"/>) MUST be the FACING-CARRYING transform. CastawayCharacter applies
    /// facing to its MODEL CHILD (_model.localRotation = Euler(0, bodyYaw, 0)), NOT to its root — "the visual
    /// owns facing". When stabilizeFrame was wired to the ROOT (which never yaws), a turn left the hand's facing
    /// yaw expressed in the root-local anchor pose, where the slow anchor (anchorTrackPerSec ≈ 0.12/s ≈ 8s time
    /// constant) EASED IT AWAY → the axe rotation lagged facing by ~8s and read as "only seated one direction."
    /// Wiring stabilizeFrame to _model (CastawayCharacter.ModelTransform) makes facing pass through the anchor
    /// IMMEDIATELY (the anchor frame yaws WITH facing, so there is no facing component to ease away); only the
    /// per-step arm-swing — the hand relative to _model — is still damped. The fix is entirely in WHICH
    /// transform the frame points at; the easing math here is unchanged.
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
                 "frame (86ca9qwvd: HAND-LOCAL, rotated by the hand so it TRACKS the hand through every facing). " +
                 "cm-scale units rotated by the hand's rotation ONLY (never its lossyScale), so a nudge step is " +
                 "a sensible ~2 cm and the axe stays seated no matter which way the castaway turns. Field name " +
                 "kept (worldOffsetFromHand) so the serialized scene + the F9 AxeNudgeTool wiring carry forward.")]
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

        [Tooltip("The reference frame the grip is anchored IN — the FACING-CARRYING model child (which " +
                 "translates + YAWS with facing but does NOT arm-swing). The grip anchor lives in THIS frame, " +
                 "so the axe rides facing (turns pass through IMMEDIATELY) without the per-step arm-swing. " +
                 "86ca9xz00: this MUST be the model child (CastawayCharacter.ModelTransform), NOT the root — " +
                 "facing is applied to _model.localRotation, not the root, so a root frame puts the facing yaw " +
                 "into the slow grip-anchor pose and EASES IT AWAY (the axe lags facing). Wired editor-time to " +
                 "CastawayCharacter.ModelTransform; Awake falls back to the first CastawayCharacter ancestor's " +
                 "model child (then its transform if the model is unresolved).")]
        public Transform stabilizeFrame;

        // The GRIP ANCHOR — the hand's rest pose in the body-root local frame. The steady axe rides this anchor
        // (transformed by the live body root), so the per-step swing is removed while body facing passes through.
        // The anchor eases slowly toward the live hand body-local pose (anchorTrackPerSec) so a real re-grip is
        // honored but the fast walk swing is not.
        private Vector3 _anchorLocalPos;
        private Quaternion _anchorLocalRot;
        private bool _anchorInit;

        // WALK BOUNCE + RATCHET FIX (86ca9ykp0): the FIXED grip height (world units) of the followed grip ABOVE
        // the grounded (bob-removed) stabilizeFrame, captured ONCE at anchor-init. The axe VERTICAL is
        // reconstructed from the grounded body + this constant, so it cannot bounce (no live modelSoleGround bob)
        // nor ratchet (no eased accumulation in the vertical). Horizontal seat + rotation still ride the anchor.
        private float _gripHeightAboveGrounded;
        private bool _gripHeightInit;

        /// <summary>The current followed (stabilized) hand WORLD position the axe pivot rides — exposed so the
        /// PlayMode regression can assert the stabilization removes the per-step swing (the axe follows the
        /// settled grip, not the raw swing).</summary>
        public Vector3 FollowPos { get; private set; }

        /// <summary>The grip-anchor's local-Y in the stabilizeFrame (the eased body-local hand rest pose) —
        /// exposed READ-ONLY for the -axeWalkTrace instrument (86ca9ykp0) so the per-frame dump can show
        /// whether the anchor itself ratchets/drifts vs the stabilizeFrame Y-bob. NaN until the anchor inits.</summary>
        public float AnchorLocalY => _anchorInit ? _anchorLocalPos.y : float.NaN;

        void Awake()
        {
            // Fallback: if the hand wasn't wired editor-time, the bone is this object's parent (the axe is
            // serialized as a child of RightHand_010). Defensive — the authored path always wires it.
            if (hand == null) hand = transform.parent;
            // Fallback stabilize frame: the FACING-CARRYING model child of the CastawayCharacter (translates
            // with locomotion + YAWS with facing, does not arm-swing). 86ca9xz00 — facing lives on the model
            // child (_model.localRotation), NOT the CastawayCharacter root, so anchoring there would let the
            // slow grip-anchor EASE THE FACING YAW AWAY (the axe lags facing). Prefer the model child; only if
            // it is unresolved (model not built yet) fall back to the root so the axe still rides the body.
            if (stabilizeFrame == null)
            {
                var cc = GetComponentInParent<CastawayCharacter>();
                if (cc != null) stabilizeFrame = cc.ModelTransform != null ? cc.ModelTransform : cc.transform;
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

                // ===== WALK BOUNCE + RATCHET FIX (86ca9ykp0). DIAGNOSE (PlayMode -axeWalkTrace + the
                // HeldAxeWalkBounce diagnose test) PINNED two coupled defects, BOTH from stabilizeFrame=_model
                // (the 86ca9xz00 facing fix — KEPT) carrying the modelSoleGround Y-bob into the VERTICAL channel:
                //   - BOUNCE: frame.TransformPoint re-applies _model's live per-frame Y-bob (modelSoleGround
                //     cancels the Mixamo WALK hip-lift on _model.localPosition.y) → followPos.y bobs per step.
                //   - RATCHET: the grip anchor eases toward the hand's pose IN the _model frame; the hand bone
                //     carries the WALK hip-lift (~+0.66u model-local) while walking, so _anchorLocalPos.y
                //     integrates UP each walk leg and the SLOW anchor (anchorTrackPerSec) doesn't ease back to
                //     the idle rest between legs → the settled axe-Y climbs MONOTONICALLY (the Sponsor's "settles
                //     HIGHER each step"). Confirmed: settledAxeY 1.0975→1.1002 monotone over 6 steps, RED test.
                //
                // FIX: DECOUPLE the axe VERTICAL from the _model-Y / anchor feedback loop entirely. The horizontal
                // seat + rotation still ride the facing-carrying anchor (facing passes through — 86ca9xz00 kept;
                // arm-swing still damped — 86ca8rdkp kept). But followPos.Y is reconstructed from a STABLE vertical
                // reference that carries NEITHER the per-frame bob NOR the accumulating anchor:
                //   stableY = (frame world-Y with the modelSoleGround bob REMOVED) + a FIXED grip-height offset
                //             captured ONCE at anchor-init (the rest hand-above-grounded-body height).
                // The bob-free frame Y = frame.position.y − rootScale·frameLocalY (frame.localPosition.x/z are 0,
                // so removing the local-Y bob yields the GROUNDED avatar-root Y the body actually stands at). The
                // grip-height offset is constant, so the axe vertical CANNOT bounce (no live bob) and CANNOT
                // ratchet (no eased accumulation) — it rides the grounded body only. modelSoleGround is UNTOUCHED
                // (we only READ frame.localPosition.y to subtract it for the axe; the model child still bobs to
                // keep the rendered SOLE planted). Returns to baseline by construction (a fixed offset).
                float bob = frame.localPosition.y * StableYScale(frame);   // the modelSoleGround world-Y bob
                float frameGroundedY = frame.position.y - bob;             // frame Y with the bob removed
                if (!_gripHeightInit)
                {
                    // Capture the rest grip height ONCE: the followed grip's world-Y above the grounded frame.
                    _gripHeightAboveGrounded = followPos.y - frameGroundedY;
                    _gripHeightInit = true;
                }
                // Reconstruct the VERTICAL from the grounded body + the fixed grip height (no bob, no ratchet).
                followPos.y = frameGroundedY + _gripHeightAboveGrounded;
            }

            // POSITION in HAND-LOCAL space (86ca9qwvd): rotate the cm-scale offset by the (stabilized) hand
            // rotation so it TRACKS the hand through every facing — the axe stays seated in the grip no matter
            // which way the castaway turns. The prior soakfix9 added a RAW WORLD offset (followPos + offset)
            // that did NOT rotate with the hand, so it was only correct at the spawn facing (turning the
            // character left the axe behind). We rotate by followRot (the SAME stabilized hand rotation the
            // ROTATION channel uses, so position + rotation ride the identical frame), NOT by the bone's
            // lossyScale — a pure rotation preserves the offset's magnitude, so the cm offset stays cm and a
            // nudge step is still ~2 cm (NOT hand.TransformPoint, which would re-apply the §FBX lossy scale).
            // ROTATION hand-relative off the STABILIZED hand rotation (the soakfix8 channel, unchanged).
            transform.position = followPos + followRot * worldOffsetFromHand;
            transform.rotation = followRot * Quaternion.Euler(relEuler);
            FollowPos = followPos;
        }

        // The world-Y scale that converts the stabilizeFrame's LOCAL-Y (the modelSoleGround bob, applied on
        // frame.localPosition.y) into WORLD units, so we can subtract the bob from frame.position.y. The frame
        // (_model) carries localScale=1; the avatar-root PARENT carries the height-scale, so the parent's world
        // Y-scale is the conversion factor. Defaults to 1 (no parent / unit scale) so the synthetic PlayMode rig
        // (parent scale 1) and the shipped scaled rig both compute the bob correctly. Guarded against ~0.
        private static float StableYScale(Transform frame)
        {
            if (frame == null || frame.parent == null) return 1f;
            float s = frame.parent.lossyScale.y;
            return Mathf.Abs(s) < 1e-4f ? 1f : s;
        }
    }
}
