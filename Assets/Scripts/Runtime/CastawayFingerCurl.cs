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

        [Header("Curl (deg of LOCAL-X per bone — measured: +X curls toward the palm)")]
        [Tooltip("Degrees of LOCAL-X curl per finger bone (Index/Middle/Ring). The hand closes around the " +
                 "haft. Conservative-but-real — a too-small curl leaves the open-hand 'mangled' read; too big " +
                 "clenches a fist through the haft. The Sponsor can re-judge from the build.")]
        public float fingerCurlDeg = 26f;
        [Tooltip("Degrees of LOCAL-X curl per THUMB bone — the thumb opposes the grip, curled less.")]
        public float thumbCurlDeg = 14f;

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
            _thumbOffset = Quaternion.Euler(thumbCurlDeg, 0f, 0f);
        }

        // Gripping = ANY held-visual weapon is the SELECTED belt item (shown in hand), or alwaysCurl (AC4). Reads
        // the SAME predicate as the mesh-visibility gate (HeldAxe.ShouldShow → HeldWeaponCycleDebug
        // .IsHeldVisualWeaponSelected), so the hand closes around EXACTLY the haft that is actually shown and the two
        // can never drift. 86cav8xu8 WIDENING (the held-visual grip sibling, NOT the chop gate): the old read was
        // stone-axe/spear-ONLY, so selecting a WOOD/IRON axe, a pickaxe, or a wood dagger/sword showed the mesh in
        // an OPEN splayed hand — the documented "mangled finger" percept, now for every non-stone-axe/spear tier.
        // Widening to the full held-visual set closes the hand around all of them. (The spear's Sponsor-dialed seat
        // was dialed WITH the curl active, so gripping the selected weapon reproduces the approved read.)
        private void ApplyGate()
        {
            _gripping = alwaysCurl || HeldWeaponCycleDebug.IsHeldVisualWeaponSelected(inventory);
        }

        /// <summary>Whether the curl is currently applied (the hand is gripping). Exposed for the PlayMode
        /// regression so it can assert the gate flips with HasAxe.</summary>
        public bool IsGripping => _gripping;

        /// <summary>Whether the curl is ACTUALLY applied this frame — gripping (belt selection) OR forced via
        /// alwaysCurl (the F9 GRIP-CURL dial). The dial surfaces this so a no-visible-effect state is
        /// distinguishable from a broken handler (86catvb6u; the applied-readout rule from
        /// procedural-animation-verbs.md's run-lower-engagement sibling).</summary>
        public bool IsApplied => _gripping || alwaysCurl;

        void LateUpdate()
        {
            // 86catvb6u — check alwaysCurl LIVE too (not only via the ApplyGate-cached _gripping): the F9 GRIP-CURL
            // dial sets alwaysCurl=true at runtime WITHOUT an Inventory.Changed event, so the curl must engage the
            // SAME frame the Sponsor forces it — else the dial writes fingerCurlDeg but the hand never changes (the
            // "wired but conditionally inert" trap this doc's run-lower-engagement sibling warns about; the Sponsor
            // saw exactly this — fingerCurlDeg=390 on the HUD, zero visible effect, because the belt axe was not
            // SELECTED [B]-cycling a wood axe does not set the belt selection so _gripping stayed false).
            if (!_gripping && !alwaysCurl) return; // empty hand keeps the natural open clip pose
            if (fingerBones != null)
                foreach (var b in fingerBones)
                    if (b != null) b.localRotation = b.localRotation * _fingerOffset;
            if (thumbBones != null)
                foreach (var b in thumbBones)
                    if (b != null) b.localRotation = b.localRotation * _thumbOffset;
        }
    }
}
