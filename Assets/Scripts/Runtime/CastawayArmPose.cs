using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// POST-ANIMATION arm posing for the Hyper3D + Mixamo castaway (ticket 86ca8rdkp soak-fixes #2 + #3).
    /// A sibling driver to <see cref="HeldAxeRig"/>: it runs in LateUpdate AFTER the Animator has written
    /// the imported Idle/Walk clip pose for this frame, then applies a SMALL RELATIVE rotation OFFSET to the
    /// upper-arm bones. This is the only place arm pose CAN be changed — the arms are driven by the imported
    /// Mixamo clips (we can't author the FBX takes), so an additive post-anim offset on the bones is the
    /// mechanism the ticket prescribes ("a sibling driver to HeldAxeRig, NOT just the axe transform").
    ///
    /// TWO Sponsor soak directives this addresses:
    ///   #3 "his arms are very tight into the body when idle" → relax BOTH upper arms a bit AWAY from the
    ///      torso (an outward roll on each shoulder), so the silhouette is less pinched.
    ///   #2 "when the axe is picked up his right hand should be a bit away from the body and raised 10-15°"
    ///      → the RIGHT upper arm gets an EXTRA away-from-body + raised offset (a "ready" carry pose), on
    ///      top of the relax. The axe rides the hand (HeldAxeRig), so lifting the arm lifts the held axe.
    ///
    /// RE-SOAK (the Sponsor: "the auto pose made it even WORSE when the axe is equipped — axe held too high/
    /// forward — do we need a nudging tool for the arm?"). The per-arm offsets are now PER-ARM LOCAL EULERS
    /// (rightArmEuler / leftArmEuler) that the build-gated F9 AxeNudgeTool dials DIRECTLY in-game (arm-pose
    /// target) — what-you-dial-is-what-you-get, like the held/stump axe. The named deg fields seed a SANE
    /// CONSERVATIVE default (arms only slightly off torso; the axe-arm only slightly raised — NOT the prior
    /// 16° lift + 20° spread that read "too high/forward"); the Sponsor finalizes by dialing + baking.
    ///
    /// WHY A RELATIVE OFFSET (multiply), not an absolute set: the clip animates the arm every frame; we want
    /// to NUDGE that animated pose by a fixed amount, not REPLACE it (which would kill the walk arm-swing).
    ///   bone.localRotation = clipPose * Quaternion.Euler(offsetEuler)
    /// applied in LateUpdate so it composes on top of whatever the Animator left for THIS frame. The offset
    /// is in the BONE'S LOCAL space, so it rides the arm through every facing + every clip frame.
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): authored editor-time by MovementCameraScene
    /// (BuildPlayer → AddArmPose) and serialized onto the avatar root, with the bone refs resolved from the
    /// SkinnedMeshRenderer.bones array (the real skeleton) so it ships in Boot.unity. No Awake-assembled
    /// hierarchy.
    ///
    /// RUN-SWING REDUCTION (86caa83wn soak #2, 2026-06-18 — "when i run the axe is no longer in the hand"): the
    /// Sponsor's chosen approach is to KEEP the axe rigidly in the hand (the HeldAxeRig world-Y clamp that
    /// DETACHED it is removed) and instead CALM the run arm-swing here. The Mixamo RUN clip pumps the right arm
    /// UP near the head; this driver subtracts an additive LOWERED-arm offset on the RIGHT upper-arm, weighted by
    /// a SMOOTHED <see cref="CastawayCharacter.IsRunning"/> value (0 at walk/idle → 1 at full run). Because the
    /// gripped axe FOLLOWS the hand (HeldAxeRig rides the raw hand bone), lowering the hand keeps the axe BELOW
    /// the head AND in the hand — one change fixes both. At WALK/IDLE the run-weight is 0, so the Sponsor's
    /// locked WALK/IDLE arm pose is byte-unchanged. The lower amount is the F9 RUN dial's target — the Sponsor
    /// dials <see cref="runLowerEuler"/> WHILE running and bakes the value (his direct-tweak preference).
    ///
    /// DefaultExecutionOrder(50): AFTER CastawayCharacter (order 0, which yaws the model + sets IsRunning) and
    /// the Animator (which writes bone poses in its own update), BEFORE HeldAxeRig (order 100, which reads the
    /// hand world transform) — so the right hand has its final posed position when the axe seats to it.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class CastawayArmPose : MonoBehaviour
    {
        [Header("Bones (wired editor-time from the SkinnedMeshRenderer.bones skeleton)")]
        [Tooltip("The right UPPER-arm bone (mixamorig:RightArm). Drives the right-arm carry pose (#2) + the " +
                 "run-swing-reduction lower (86caa83wn).")]
        public Transform rightUpperArm;
        [Tooltip("The left UPPER-arm bone (mixamorig:LeftArm). Driven by the idle-relax offset (#3).")]
        public Transform leftUpperArm;

        // The EMPIRICAL bone-LOCAL axis mapping (RuntimeRigTrace -armTrace, 2026-06-15, on this exact rig):
        // a +rotation about the upper-arm bone's LOCAL X spreads the hand AWAY from the torso for BOTH arms
        // (right wrist → −X world / left wrist → +X world — both OUTWARD, SAME sign), with a small +Y raise. A
        // +rotation about LOCAL Z spreads further AND lifts the hand (+Y) AND reaches it forward/back (the
        // "ready carry" feel). Local Y is a near-useless twist. So the pose is authored in LOCAL X (spread) +
        // LOCAL Z (raise/reach), NOT a guessed Z-roll mirror — the trace overturned the first guess.
        //
        // RE-SOAK (the Sponsor's "the auto pose made it even WORSE when the axe is equipped, axe held too high/
        // forward — do we need a nudging tool for the arm?"). Two changes this wave:
        //   (1) the offsets now live as PER-ARM LOCAL EULERS (rightArmEuler / leftArmEuler) that the F9
        //       AxeNudgeTool dials DIRECTLY in-game (arm-pose target) — what-you-dial-is-what-you-get, like
        //       the held/stump axe. The named deg fields below SEED these eulers (the conservative default);
        //       the Sponsor finalizes by dialing the eulers and pasting RightArmEuler/LeftArmEuler to bake.
        //   (2) a SANE CONSERVATIVE default: arms only SLIGHTLY off the torso; the axe-arm only SLIGHTLY
        //       raised (NOT the prior 16° lift + 20° spread that read as "axe held too high/forward").

        [Header("#3 — relax BOTH arms away from the torso (the pinched-idle fix) — conservative default")]
        [Tooltip("Degrees of LOCAL-X rotation added to BOTH upper arms to spread them a LITTLE off the torso " +
                 "(less pinched, NOT flung wide). +X spreads BOTH arms OUTWARD (same sign — measured), so NO " +
                 "mirror is applied. Seeds rightArmEuler.x + leftArmEuler.x.")]
        public float relaxSpreadDeg = 9f;

        [Header("#2 — right arm: a bit away from body + only SLIGHTLY raised (the held-axe carry) — conservative")]
        [Tooltip("EXTRA degrees of LOCAL-X spread on the RIGHT upper arm only (on top of the relax). Seeds " +
                 "rightArmEuler.x above the left.")]
        public float rightCarryExtraSpreadDeg = 3f;
        [Tooltip("Degrees of LOCAL-Z on the RIGHT upper arm — lifts/reaches the hand for the carry. Kept SMALL " +
                 "(the prior 16° read 'axe held too high/forward'). Seeds rightArmEuler.z.")]
        public float rightCarryRaiseDeg = 7f;

        [Header("Per-arm LOCAL-euler offsets — what the F9 AxeNudgeTool dials in-game (bake these)")]
        // RE-SOAK #1 (86ca8rdkp): the Sponsor DIALED the arm pose in-game via F9 and reported these eulers
        // (European decimal commas -> dot-decimal, digit-checked). They are BAKED here as the shipped defaults
        // — this is what-he-dialed-is-what-ships. seedEulersFromDegFields ships FALSE so RebuildCached does NOT
        // re-derive over them from the deg fields (the deg fields stay only as the conservative SEED path the
        // EditMode seed test still exercises). The F9 tool still nudges these so he can re-tune later.
        [Tooltip("RIGHT upper-arm LOCAL-euler offset (deg) — BAKED from the Sponsor's F9 dial (re-soak #1). " +
                 "The F9 nudge tool (arm-pose target) edits this; paste RightArmEuler to re-bake.")]
        public Vector3 rightArmEuler = new Vector3(-4.0f, -50.0f, -3.0f);
        [Tooltip("LEFT upper-arm LOCAL-euler offset (deg) — BAKED from the Sponsor's F9 dial (re-soak #1). " +
                 "Dialed via F9; paste LeftArmEuler to re-bake.")]
        public Vector3 leftArmEuler = new Vector3(-5.0f, 22.0f, 0.0f);

        // When true, RebuildCached re-DERIVES the per-arm eulers from the named deg fields (the conservative
        // authored SEED). Ships FALSE (re-soak #1) so the Sponsor's BAKED dialed eulers above are authoritative
        // and a RebuildCached can't clobber them. The F9 nudge tool also clears this before dialing; the
        // EditMode seed test flips it TRUE to exercise the deg-field seed path. The editor author keeps it
        // FALSE (AddArmPose no longer re-seeds — the dialed eulers ship verbatim).
        [HideInInspector] public bool seedEulersFromDegFields = false;

        [Header("RUN-swing reduction (86caa83wn soak #2 — calm the run arm so the held axe stays below the head)")]
        [Tooltip("The CastawayCharacter whose IsRunning state weights the run-lower. Wired editor-time " +
                 "(serialized); a runtime fallback resolves it from this object / the parent chain. If unresolved " +
                 "the run-lower is INERT (the arm follows the raw clip) — fail-safe toward the Sponsor's locked " +
                 "pose, so a missing wire can never lower the arm at the wrong time.")]
        public CastawayCharacter character;

        [Tooltip("Additive LOCAL-euler offset (deg) blended onto the RIGHT upper-arm at FULL RUN, on top of the " +
                 "carry pose — the run-swing reduction. The Mixamo RUN clip pumps the right arm UP near the head; " +
                 "this LOWERS the arm so the hand (and the gripped axe that follows it) stays below the head while " +
                 "running. The raise axis on this rig is LOCAL-Z (the -armTrace measurement, same axis the carry " +
                 "raise uses), so a NEGATIVE Z lowers the arm. Default (0,0,-22) lowers the right arm ~22° at full " +
                 "run. Blended by a SMOOTHED IsRunning weight (0 at walk/idle → this at full run), so WALK/IDLE is " +
                 "untouched (weight 0). The F9 AxeNudgeTool (RUN target) dials this in-game while running; paste " +
                 "RunLowerEuler to bake. The shipped default is set editor-time by " +
                 "MovementCameraScene.ArmRunLowerEuler (kept in sync with this).")]
        // 86caa83wn soak #3 (build 2993c1c, 2026-06-18): kept in sync with MovementCameraScene.ArmRunLowerEuler
        // — the Sponsor's F9-dialed run carry (-10,12,-42). This is the runtime FALLBACK default; the shipped
        // value is baked editor-time into Boot.unity from ArmRunLowerEuler (the authoritative ship source).
        public Vector3 runLowerEuler = new Vector3(-10f, 12f, -42f);

        [Tooltip("Per-second blend rate for the run-lower weight (how fast the arm eases down on run-start / back " +
                 "up on run-end). Frame-rate-independent. Higher = snappier; ~8/s gives a smooth ~0.4s ease that " +
                 "tracks run start/stop without popping. The blend is what keeps WALK/IDLE byte-unchanged: the " +
                 "weight rests at 0 until IsRunning, and returns to 0 on stop.")]
        public float runLowerBlendRate = 8f;

        // ACTION-VERB SWING OFFSET (chop 86caa4c5c; later: drink/pick-up/throw — Erik's procedural-action-verb
        // playbook). A one-shot LOCAL-euler offset a verb driver (ChopPoseDriver, order 10 — BEFORE this order
        // 50) writes each frame DURING a swing, then resets to Vector3.zero at rest. It is right-multiplied onto
        // the RIGHT-arm clip pose AFTER the carry + run-lower offsets, so the swing composes on top of the held-
        // axe carry pose and the gripped axe (HeldAxeRig, order 100) follows the swung hand automatically. At
        // rest the driver writes Vector3.zero → Quaternion.Euler(zero) is identity → ZERO cost and the locked
        // carry pose is byte-unchanged. NOT serialized as a tuned default (it is a transient runtime channel the
        // driver owns); [HideInInspector] so it never looks like an authored pose field.
        [HideInInspector] public Vector3 swingOverrideEuler = Vector3.zero;

        // Cached so the offset composes on the clip pose, not on the prior frame's offset (which would drift).
        private Quaternion _rightOffsetQ, _leftOffsetQ;

        // The SMOOTHED run weight (0 at walk/idle, eases to 1 at full run). Drives the run-lower amount applied
        // to the right arm. Public-getter so the F9 RUN dial panel + a PlayMode regression can read it.
        private float _runWeight;
        /// <summary>The current smoothed run weight (0 walk/idle → 1 full run) the run-lower is scaled by.
        /// Exposed so the F9 RUN dial surfaces whether the lower is engaged (run to judge) and the regression
        /// can assert it rests at 0 at walk/idle (the locked pose untouched) and rises while running.</summary>
        public float RunWeight => _runWeight;

        void Awake()
        {
            RebuildCached();
            // Fallback wiring for the run-lower's IsRunning source (the authored path wires it editor-time so it
            // serializes; this keeps it working on a runtime-built rig). Fail-safe: if unresolved, _runWeight
            // stays 0 → the run-lower is inert → the arm follows the raw clip (the locked pose).
            if (character == null) character = GetComponentInParent<CastawayCharacter>();
        }

        /// <summary>Rebuild the cached offset quaternions. If <see cref="seedEulersFromDegFields"/> is set
        /// (the authored default), the per-arm eulers are first re-derived from the named deg fields; the F9
        /// nudge tool clears that flag so it can dial rightArmEuler/leftArmEuler directly. Public so the editor
        /// author + the nudge tool can re-apply after editing the fields.</summary>
        public void RebuildCached()
        {
            if (seedEulersFromDegFields)
            {
                // BOTH arms: the relax is a +local-X spread (same sign — measured outward for both). The RIGHT
                // arm gets EXTRA local-X spread + a local-Z raise/reach (the carry).
                leftArmEuler = new Vector3(relaxSpreadDeg, 0f, 0f);
                rightArmEuler = new Vector3(relaxSpreadDeg + rightCarryExtraSpreadDeg, 0f, rightCarryRaiseDeg);
            }
            // Compose euler in the bone's LOCAL frame (Unity's Quaternion.Euler order Z,X,Y). The offset is
            // right-multiplied onto the clip pose in LateUpdate so it rides every facing + every clip frame.
            _leftOffsetQ = Quaternion.Euler(leftArmEuler);
            _rightOffsetQ = Quaternion.Euler(rightArmEuler);
        }

        void LateUpdate()
        {
            // RUN-LOWER weight (86caa83wn soak #2): ease the smoothed run weight toward 1 while IsRunning, back
            // toward 0 otherwise. Frame-rate-independent. At walk/idle the weight rests at 0 → the run-lower
            // offset below is the identity → the Sponsor's locked pose is byte-unchanged. If the character is
            // unresolved (fail-safe), target stays 0 so the arm never lowers at the wrong time.
            float target = (character != null && character.IsRunning) ? 1f : 0f;
            float a = 1f - Mathf.Exp(-Mathf.Max(0f, runLowerBlendRate) * Time.deltaTime);
            _runWeight = Mathf.Lerp(_runWeight, target, a);

            // Compose the offset ON the clip's animated localRotation for THIS frame (the Animator already
            // ran). bone.localRotation here is the clip pose; right-multiplying by the offset rotates in the
            // bone's local frame, preserving the walk arm-swing while nudging the rest position.
            if (rightUpperArm != null)
            {
                // The RIGHT arm gets the carry offset PLUS the run-lower (scaled by the smoothed run weight). The
                // run-lower is a LOCAL-euler offset (the rig's raise axis is local-Z; a negative-Z lowers), so at
                // full run the right arm — and the hand + gripped axe that follow it — is held lower, below the
                // head. Scaled by _runWeight (0 at walk/idle → the run-lower vanishes → the locked pose is intact).
                Quaternion runLowerQ = Quaternion.Euler(runLowerEuler * _runWeight);
                // ACTION-VERB SWING (chop 86caa4c5c): right-multiply the one-shot swing offset (written by
                // ChopPoseDriver order 10, BEFORE this) AFTER the carry + run-lower, so a chop swing composes on
                // top of the carry pose. Zero at rest → identity → the locked pose is byte-unchanged.
                Quaternion swingQ = Quaternion.Euler(swingOverrideEuler);
                rightUpperArm.localRotation = rightUpperArm.localRotation * _rightOffsetQ * runLowerQ * swingQ;
            }
            if (leftUpperArm != null)
                leftUpperArm.localRotation = leftUpperArm.localRotation * _leftOffsetQ;
        }
    }
}
