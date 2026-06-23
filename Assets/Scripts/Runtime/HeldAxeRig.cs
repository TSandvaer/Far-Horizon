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
    public class HeldAxeRig : HeldToolRig
    {
        // 86cabh907: HeldAxeRig is now a thin subclass of the SHARED HeldToolRig — the seat math
        // (raw-hand follow + hand-local offset + hand-relative euler + optional damp) lives ONCE in the
        // base. The axe keeps its OWN serialized field names (worldOffsetFromHand / relEuler) so the
        // committed Boot.unity, the F9 AxeNudgeTool, and the soak-tuning tests all carry forward
        // UNCHANGED — these alias the base seat (synced into base.seatOffsetFromHand / base.seatEuler
        // every LateUpdate, BEFORE the base drives, so an F9 runtime nudge to worldOffsetFromHand still
        // moves the axe). hand / followDamp / FollowPos are INHERITED from the base (same names → every
        // existing reference resolves; the scene re-bake also writes the base hand/followDamp).

        [Tooltip("POSITION channel — the axe is seated at hand.position + hand.rotation * this offset every " +
                 "frame. This offset is HAND-LOCAL (expressed in the hand bone's own frame): it is rotated by " +
                 "the RAW hand each frame so it TRACKS the hand through every facing AND follows the arm's " +
                 "natural swing — 86ca9zcjn. cm-scale units rotated by the hand's rotation ONLY (never its " +
                 "lossyScale), so a nudge step is a sensible ~2 cm and the axe stays seated no matter which way " +
                 "the castaway turns OR how it was acquired (spawn-in-hand == picked-up). 86caa83wn: dialed, " +
                 "displayed AND baked in THIS hand-local frame end to end (no WORLD-frame round-trip) so the " +
                 "Sponsor's F9 dial reproduces at every facing. Field name kept (worldOffsetFromHand) so the " +
                 "serialized scene + the F9 AxeNudgeTool wiring carry forward — but the value is HAND-LOCAL. " +
                 "86cabh907: aliases base HeldToolRig.seatOffsetFromHand.")]
        public Vector3 worldOffsetFromHand = new Vector3(0.003f, -0.017f, 0.009f);

        [Tooltip("ROTATION channel — the axe's rotation is hand.rotation * Euler(this), HAND-RELATIVE, so " +
                 "the haft turns WITH the hand through every facing (the soakfix8 rotation-tracks-hand fix). " +
                 "86cabh907: aliases base HeldToolRig.seatEuler.")]
        public Vector3 relEuler = new Vector3(4.1f, 95.8f, -56.1f);

        // The axe rides the hand RIGIDLY (no axe-side vertical clamp — 86caa83wn soak #2: an earlier world-Y
        // clamp HERE detached the axe from the hand during the run arm-swing; the run into-head is fixed on the
        // ARM side via CastawayArmPose.runLowerEuler). The base HeldToolRig has no clamp field, so the
        // HeroAxeSceneTests "no clamp field" reflection guard stays green.

        protected override void LateUpdate()
        {
            // Sync the axe's serialized alias fields into the shared base seat BEFORE the base drives, so an
            // F9 AxeNudgeTool runtime nudge to worldOffsetFromHand / relEuler still moves the axe this frame.
            seatOffsetFromHand = worldOffsetFromHand;
            seatEuler = relEuler;
            base.LateUpdate();
        }
    }
}
