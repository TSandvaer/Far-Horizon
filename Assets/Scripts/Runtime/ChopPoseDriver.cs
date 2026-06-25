using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The PLAYER CHOP SWING — a windup→strike→return ADDITIVE bone-offset on the right upper arm, authored
    /// PROCEDURALLY per Erik's procedural-action-verb playbook (ticket 86cae5tb3, `team/erik-consult/
    /// procedural-action-verb-animation.md`). Chop ticket 86caa4c5c AC1.
    ///
    /// === Why a driver, NOT a new Animator clip (AC1 correction, ticket V5) ===
    /// There is NO chop/strike/swing clip in the repo (the 16 Castaway FBX are Idle/Walk/Run/Jump/Pick-Up +
    /// hit-REACTION clips only — verified 2026-06-25). The project's ONLY arm idiom is a post-Animator
    /// ADDITIVE bone-rotation offset on <see cref="CastawayArmPose"/> (LateUpdate; zero Animator layers /
    /// zero AvatarMask by design — playbook E8). This driver writes a one-shot swing offset INTO
    /// <see cref="CastawayArmPose.swingOverrideEuler"/>; ArmPose right-multiplies it onto the right-arm clip
    /// pose; <see cref="HeldAxeRig"/> (order 100) then seats the held axe off the FINAL hand transform, so the
    /// axe follows the swing automatically — no axe-side change needed.
    ///
    /// === Execution order (playbook E3) ===
    /// [DefaultExecutionOrder(10)] — runs in LateUpdate BEFORE CastawayArmPose (order 50). A LateUpdate-writing
    /// driver MUST be &lt; 50 so it writes its curve value into ArmPose's swingOverrideEuler BEFORE ArmPose's
    /// LateUpdate composes it. (HeldAxeRig is order 100, after both — it reads the posed hand last.)
    ///
    /// === The swing shape (playbook Step 2 / CHOP per-verb) ===
    /// Three serialized <see cref="AnimationCurve"/> (one per LOCAL euler axis), sampled at normalised time
    /// t∈[0,1] over <see cref="swingDuration"/>. CHOP is a downward arc: a small −local-Z windup back, then
    /// through to a deeper −Z strike, then ease back to 0 (rest). The rig's raise axis is LOCAL-Z (the
    /// −armTrace measurement, same axis CastawayArmPose's carry-raise uses) → a NEGATIVE Z lowers the arm
    /// (the strike). The curves are Inspector-tweakable so the feel can be tuned without recompile. At rest
    /// (t≥1) the driver writes Vector3.zero → identity → the locked carry pose is byte-unchanged (zero cost).
    ///
    /// === Tool-use speed (AC1 / ticket V1) ===
    /// <see cref="swingSpeed"/> scales the PLAYBACK RATE of the procedural swing (not a clip's speed multiplier
    /// — there is no clip). The settings panel's `tool-use speed` row binds to it live (SettingsCatalog
    /// .PopulateChop flips the reserved ToolSpeedId hook live + drives this field). 1× = the authored
    /// duration; 2× = twice as fast; 0.5× = half speed. Effective duration = swingDuration / swingSpeed.
    ///
    /// === Headless test access (playbook E6/E7) ===
    /// The swing is timed by <see cref="Time"/>.time anchoring (NOT deltaTime accumulation — headless
    /// deltaTime≈0). <see cref="SwingNormT"/> (0→1 over the swing, ≥1 at rest) is a readable property so
    /// PlayMode tests assert SwingNormT&gt;0 mid-swing + swingOverrideEuler≈0 at rest WITHOUT a
    /// WaitForEndOfFrame (which never fires in -batchmode). <see cref="IsSwinging"/> is the active flag.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// Authored editor-time onto the castaway (MovementCameraScene.AddArmPose, alongside CastawayArmPose),
    /// the armPose ref serialized. ChopTree (the chop mechanic) holds a serialized ref to this driver and
    /// calls <see cref="TriggerSwing"/> on each chop. ChopSceneTests guards the scene presence + wiring.
    ///
    /// NO MUTABLE STATICS (instance state only) — the Configurable-Enter-Play-Mode static-reset audit
    /// (StaticStateResetTests) needs no [RuntimeInitializeOnLoadMethod] reset for this class.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ChopPoseDriver : MonoBehaviour
    {
        [Header("Target (wired editor-time, serialized)")]
        [Tooltip("The CastawayArmPose this driver feeds its swing offset into. Wired at bootstrap " +
                 "(MovementCameraScene.AddArmPose); an Awake fallback resolves it from this object / parent " +
                 "chain. If unresolved the swing is INERT (no arm to drive) — fail-safe.")]
        public CastawayArmPose armPose;

        [Header("Swing shape (LOCAL-euler degrees per axis, sampled at normalised t∈[0,1] — Inspector-tweakable)")]
        [Tooltip("LOCAL-X curve (deg). Small outward windup spread; near-0 for a clean downward chop.")]
        public AnimationCurve swingX = DefaultSwingX();
        [Tooltip("LOCAL-Y curve (deg). The rig's local-Y is a near-useless twist (−armTrace) — left flat (0).")]
        public AnimationCurve swingY = DefaultSwingY();
        [Tooltip("LOCAL-Z curve (deg) — the STRIKE axis. The rig raise axis is local-Z; NEGATIVE Z LOWERS the " +
                 "arm. Windup back (+Z) → deep strike (−Z) → return (0). This is the chop arc.")]
        public AnimationCurve swingZ = DefaultSwingZ();

        [Header("Timing")]
        [Tooltip("Authored swing duration in seconds at 1× speed (windup→strike→return). ~0.55s reads as a " +
                 "purposeful chop. Effective duration = swingDuration / swingSpeed.")]
        public float swingDuration = 0.55f;

        [Tooltip("TOOL-USE SPEED multiplier (AC1) — scales the swing PLAYBACK RATE. 1× = authored duration; " +
                 "2× = twice as fast; 0.5× = half speed. The settings-panel `tool-use speed` row drives this " +
                 "live (SettingsCatalog.PopulateChop). Clamped to a sane band so a dial can't stall (0) or " +
                 "blur (huge) the swing.")]
        public float swingSpeed = 1f;

        // Speed clamp band — keep in sync with the ToolSpeedId slider band in SettingsCatalog.
        public const float SwingSpeedMin = 0.25f;
        public const float SwingSpeedMax = 3f;

        // Runtime swing state. Anchored on Time.time (NOT deltaTime accumulation — headless deltaTime≈0,
        // playbook E6). float.NegativeInfinity = "no swing has ever fired" → SwingNormT clamps to 1 (rest).
        private float _swingStart = float.NegativeInfinity;

        /// <summary>Effective swing duration after the tool-use-speed multiplier (clamped > 0).</summary>
        public float EffectiveDuration =>
            Mathf.Max(0.001f, swingDuration / Mathf.Clamp(swingSpeed, SwingSpeedMin, SwingSpeedMax));

        /// <summary>Normalised swing position 0→1 over the (effective) duration; ≥1 at rest. Exposed for
        /// PlayMode tests (assert &gt;0 mid-swing, ==1 at rest) + the verify capture log.</summary>
        public float SwingNormT => Mathf.Clamp01((Time.time - _swingStart) / EffectiveDuration);

        /// <summary>True while a swing is actively playing (SwingNormT &lt; 1). False at rest.</summary>
        public bool IsSwinging => SwingNormT < 1f;

        /// <summary>The current swing offset euler this driver wrote to the arm pose this frame (zero at
        /// rest). Exposed for tests so they can assert the offset returns to ~0 after the swing without
        /// reaching into CastawayArmPose.</summary>
        public Vector3 CurrentSwingEuler { get; private set; }

        /// <summary>Trigger a chop swing NOW (anchored on Time.time). ChopTree calls this each chop. Re-
        /// triggering mid-swing restarts the arc (a fast chop cadence reads as repeated strikes).</summary>
        public void TriggerSwing() => _swingStart = Time.time;

        void Awake()
        {
            // Fail-safe wiring: the authored path wires armPose editor-time (serialized); this resolves it on
            // a runtime-built rig. If unresolved the swing writes nothing → the arm follows the raw clip.
            if (armPose == null) armPose = GetComponentInParent<CastawayArmPose>();
        }

        // Order 10 — runs in LateUpdate BEFORE CastawayArmPose (order 50), so the offset is written into the
        // arm pose BEFORE ArmPose composes it onto the right-arm clip pose this frame (playbook E3).
        void LateUpdate()
        {
            if (armPose == null) return;

            float t = SwingNormT;
            if (t >= 1f)
            {
                // At rest — write zero so the locked carry pose is byte-unchanged (do not leave a stale
                // offset that would drift the rest pose). Idempotent: re-writing zero each rest frame is free.
                CurrentSwingEuler = Vector3.zero;
                armPose.swingOverrideEuler = Vector3.zero;
                return;
            }

            // Sample the three curves at the normalised swing time → the LOCAL-euler offset for this frame.
            // The pre-existing serialised curves are evaluated (no new AnimationCurve allocation — playbook
            // E4: Evaluate() on a serialised curve does not allocate per frame).
            Vector3 e = new Vector3(swingX.Evaluate(t), swingY.Evaluate(t), swingZ.Evaluate(t));
            CurrentSwingEuler = e;
            armPose.swingOverrideEuler = e;
        }

        // === The authored CHOP arc (windup→strike→return) — Inspector-overridable defaults ===========
        // Shapes per the playbook's CHOP curve. Degrees. t=0 rest, small windup back, strike peak, return.

        // LOCAL-X: a small outward spread on the windup, settling back. Keeps the elbow off the torso during
        // the strike so the swing reads, then returns to 0.
        private static AnimationCurve DefaultSwingX() => new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.20f, 8f),
            new Keyframe(0.55f, 4f),
            new Keyframe(1.00f, 0f));

        // LOCAL-Y: flat — the rig's local-Y is a near-useless twist (−armTrace). No contribution to the chop.
        private static AnimationCurve DefaultSwingY() => new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(1.00f, 0f));

        // LOCAL-Z: the STRIKE. The rig raise axis is local-Z; +Z raises (windup back/up), −Z lowers (the
        // downward strike). Windup ~+18° back, deep strike ~−48° (a strong downward chop), then ease to 0.
        private static AnimationCurve DefaultSwingZ() => new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.22f, 18f),   // windup back/up
            new Keyframe(0.55f, -48f),  // strike — arm swings down hard
            new Keyframe(1.00f, 0f));   // return to rest
    }
}
