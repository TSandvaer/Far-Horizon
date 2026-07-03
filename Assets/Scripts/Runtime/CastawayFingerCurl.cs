using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// POST-ANIMATION right-hand FINGER CURL for the Hyper3D + Mixamo castaway (ticket 86ca8rdkp re-soak #4 —
    /// "his right finger is mangled"). A sibling driver to <see cref="CastawayArmPose"/> / <see cref="HeldAxeRig"/>:
    /// it runs in LateUpdate AFTER the Animator has written the imported Idle/Walk clip pose, then composes a
    /// small +local-X CURL onto each right-hand finger bone so the hand CLOSES around the held axe haft.
    ///
    /// DIAGNOSE-VIA-TRACE (diagnostic-traces-before-hypothesized-fixes) — OVERTURNED the ticket hypothesis:
    /// the ticket guessed "bad finger-bone weights / a finger collapsing" (a skinning artifact). The
    /// -fingerTrace REFUTED that: every right-hand finger bone reads lossyScale (1.8,1.8,1.8) (the avatar
    /// scale, uniform — NO degenerate bone), every localScale (1,1,1), the dominant-weighted verts sit
    /// 0.008–0.026u from their bone with tiny (0.01–0.045u) spread — the skinning is CLEAN. The "mangled"
    /// read is a POSE mismatch: the imported Idle/Walk clips pose the hand OPEN/relaxed (Mixamo clips grip
    /// nothing), so a held axe in an open splayed hand reads as broken/mangled fingers from the gameplay cam.
    /// FIX is therefore NOT re-weighting — it is CURLING the fingers into a grip (this driver), gated on the
    /// axe actually being held.
    ///
    /// MEASURED CURL AXIS (-fingerAxisTrace, on this exact rig): a +20° rotation about each finger proximal
    /// bone's LOCAL X moves the fingertip DOWN (−Y) + FORWARD (+Z) in hand-local space — i.e. it curls the
    /// finger toward the palm. Local Z splays sideways (abduction); local Y is a near-useless twist. So the
    /// curl is authored about LOCAL X — measured, not guessed (the overturned-axis lesson).
    ///
    /// THE THUMB IS MIRRORED (86cahnmjv "finger sticks out like it's broken" — measured via
    /// CharacterAssetGen.ThumbOpposeAxisTrace): on this rig the thumb chain's local frame is FLIPPED vs the
    /// fingers, so the original blanket +X thumb curl moved the thumb tip AWAY from the fist (+14°X measured
    /// dDist +0.028 — it actively pushed the thumb OUT of the grip; the state-grid -verifyHands captures show
    /// it as a stiff straight digit dangling below the haft, worst on the thin spear + the crouch diagonal,
    /// while the empty hand reads fine — which is why the #186 empty-idle gate missed it). The measured
    /// oppose family is NEGATIVE X on every thumb joint, with small −Y/−Z assists: (−18,−8,−8) per joint
    /// closes the tip-to-fist gap 0.042→0.0135 (the thumb wraps the gripping fingers like a real haft grip).
    ///
    /// WHY A RELATIVE OFFSET (multiply), not an absolute set: the clip animates the fingers every frame; we
    /// NUDGE that pose by a fixed curl, preserving any clip motion. bone.localRotation = clip * Euler(curl,0,0).
    ///
    /// GATED ON A HELD WEAPON BEING SHOWN: the curl applies ONLY when a held-visual weapon (axe OR spear —
    /// 86cahngdg) is the SELECTED belt item (Inventory.IsAxeSelectedInBelt / IsSpearSelectedInBelt — AC4
    /// 86caa4bya), coherent with HeldAxe's visibility. Empty-handed OR with the weapon in a non-selected
    /// belt slot / in the pack, the hand keeps its natural open clip pose — we only close the hand when a
    /// haft is actually shown in it. This SUPERSEDES the old HasAxe (ownership) gate (before the belt,
    /// owning == holding; now selection is the right signal — item-model contract §5). Subscribes to
    /// Inventory.Changed + applies on enable, so it is correct at spawn (no weapon → open hand) and after
    /// every selection/move (→ gripping only when a weapon is in hand), no per-frame polling of the ledger.
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): authored editor-time by MovementCameraScene
    /// (BuildPlayer → AddFingerCurl) and serialized onto the avatar root with the finger bones resolved from
    /// the SkinnedMeshRenderer.bones array (the real skeleton), so it ships in Boot.unity. No Awake assembly.
    ///
    /// DefaultExecutionOrder(60): AFTER CastawayCharacter (0) + CastawayArmPose (50) and the Animator, BEFORE
    /// HeldAxeRig (100) — so the fingers have their final curled pose before the axe seats (kept consistent).
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class CastawayFingerCurl : MonoBehaviour
    {
        [Header("Right-hand finger bones (wired editor-time from SkinnedMeshRenderer.bones)")]
        [Tooltip("The right-hand finger bones to curl (proximal..distal of Index/Middle/Ring). Resolved + " +
                 "serialized editor-time. The thumb is curled less (it opposes the grip from the other side).")]
        public Transform[] fingerBones;
        [Tooltip("The right-hand THUMB bones — curled by thumbCurlDeg (a gentler opposing grip).")]
        public Transform[] thumbBones;

        [Header("Curl (deg per bone — axes MEASURED per chain; the thumb frame is mirrored vs the fingers)")]
        [Tooltip("Degrees of LOCAL-X curl per finger bone (Index/Middle/Ring). The hand closes around the " +
                 "haft. Conservative-but-real — a too-small curl leaves the open-hand 'mangled' read; too big " +
                 "clenches a fist through the haft. The Sponsor can re-judge from the build.")]
        public float fingerCurlDeg = 26f;
        [Tooltip("LOCAL euler offset per THUMB bone (86cahnmjv — measured via ThumbOpposeAxisTrace). The " +
                 "thumb chain's frame is MIRRORED vs the fingers on this rig: oppose = NEGATIVE X (+X pushes " +
                 "the thumb OUT of the grip — the 'finger sticks out like it's broken' defect). (−18,−8,−8) " +
                 "closes the measured tip-to-fist gap 0.042→0.0135 so the thumb wraps the gripping fingers.")]
        public Vector3 thumbCurlEuler = new Vector3(-18f, -8f, -8f);

        [Header("Gate")]
        [Tooltip("The inventory whose IsAxeSelectedInBelt gates the curl (AC4). Wired editor-time; " +
                 "scene-found fallback in Awake. When the axe is not the selected belt item (or none is " +
                 "held), the hand keeps its natural OPEN clip pose (we only grip a haft actually in hand).")]
        public Inventory inventory;
        [Tooltip("If true, always curl regardless of selection (verification/diagnostic only).")]
        public bool alwaysCurl = false;

        private Quaternion _fingerOffset, _thumbOffset;
        private bool _gripping;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            RebuildCached();
        }

        void OnEnable()
        {
            if (inventory != null) inventory.Changed += ApplyGate;
            ApplyGate();
        }

        void OnDisable()
        {
            if (inventory != null) inventory.Changed -= ApplyGate;
        }

        /// <summary>Recompute the per-bone curl quaternions (call after editing the deg fields editor-time).</summary>
        public void RebuildCached()
        {
            _fingerOffset = Quaternion.Euler(fingerCurlDeg, 0f, 0f); // +local-X = curl toward the palm (measured)
            _thumbOffset = Quaternion.Euler(thumbCurlEuler);         // −X oppose family (measured; thumb frame mirrored)
        }

        /// <summary>The composed thumb offset (read-only) — exposed so the oppose-direction regression
        /// (EditMode, real FBX) asserts the SHIPPED offset moves the thumb tip TOWARD the fist.</summary>
        public Quaternion ThumbOffset { get { return _thumbOffset; } }

        // Gripping = a held-visual weapon (axe OR spear — 86cahngdg) is the SELECTED belt item (shown in
        // hand), or alwaysCurl (AC4). Coherent with HeldAxe's visibility — the hand only closes around a
        // haft that is actually shown. The spear joined the predicate with the soak-224 crossed-visual fix:
        // the spear's Sponsor-dialed in-hand seat (5caf1be) was dialed WITH the curl active (axe selected
        // while the spear was [B]-displayed), so gripping the selected spear reproduces the approved read;
        // an open hand through the spear haft is the documented "mangled finger" percept.
        private void ApplyGate()
        {
            _gripping = alwaysCurl || (inventory != null &&
                        (inventory.IsAxeSelectedInBelt || inventory.IsSpearSelectedInBelt));
        }

        /// <summary>Whether the curl is currently applied (the hand is gripping). Exposed for the PlayMode
        /// regression so it can assert the gate flips with HasAxe.</summary>
        public bool IsGripping => _gripping;

        void LateUpdate()
        {
            if (!_gripping) return; // empty hand keeps the natural open clip pose
            if (fingerBones != null)
                foreach (var b in fingerBones)
                    if (b != null) b.localRotation = b.localRotation * _fingerOffset;
            if (thumbBones != null)
                foreach (var b in thumbBones)
                    if (b != null) b.localRotation = b.localRotation * _thumbOffset;
        }
    }
}
