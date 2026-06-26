using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A PICKABLE axe in the world (ticket 86caa4bya AC3 — the PoC pickup; later the axe is CRAFTED). The
    /// castaway walks within <see cref="pickupRadius"/> of it and the axe is added to the inventory +
    /// auto-placed in BELT SLOT 1 (it's a tool) via <see cref="Inventory.PickUpAxe"/>; the world axe is
    /// then consumed (its renderers hide) so it isn't duplicated.
    ///
    /// Uses the project's proven proximity idiom (the same planar-XZ polling CraftSpot / ChopTree /
    /// CampfirePlacement use — NOT ClickToMove.onArrived, which is a single non-multicast Action). Cheap:
    /// one Vector2.Distance/frame, no per-frame allocation.
    ///
    /// === Relationship to the existing stump-craft flow ===
    /// The shipped CraftSpot/StumpAxe flow (86ca8ce6y) ALSO yields the axe to the belt on reaching the
    /// stump (CraftAxe → PickUpAxe). This component is the literal "a pickable axe lying in the world"
    /// the AC describes — an INDEPENDENT PoC pickup, not a parallel inventory system (it calls the SAME
    /// Inventory.PickUpAxe seam). One of the two is enough to exercise AC3; both share the model.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// Authored editor-time into Boot.unity (MovementCameraScene), NOT at Awake. The pickable mesh + this
    /// component + its Inventory/player references serialize into the scene. An EditMode scene-presence
    /// test guards it ships.
    /// </summary>
    public class AxePickup : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory the picked-up axe is added to. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity triggers the pickup. Wired at bootstrap; falls " +
                 "back to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Tooltip("The pickable axe's visual root, hidden once picked up. Falls back to this transform.")]
        public Transform visual;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway picks up the axe. Generous — arriving " +
                 "near the axe counts (mirrors CraftSpot/ChopTree radii).")]
        public float pickupRadius = 2.0f;

        [Tooltip("#100 BUG-1: whether this PoC pickup is ACTIVE at spawn. The Boot scene carries TWO redundant " +
                 "axe-acquisition cues — the StumpAxe craft block AND this AC3 PoC pickup — both gated on " +
                 "Inventory.HasAxe, so the Sponsor saw 'the axe in two places' and 'pick up one, both " +
                 "disappear'. To make the dial-tool soak unambiguous (ONE clear axe), the scene leaves the " +
                 "StumpAxe as the single VISIBLE spawn axe and authors this pickup INACTIVE: the component + " +
                 "Inventory/player wiring still serialize (the AC3 PoC + its EditMode guard carry forward), but " +
                 "the visual is hidden and the proximity pickup does not fire. Flip this true to re-enable the " +
                 "standalone PoC pickup. NOT a deletion of the PoC — a spawn-visibility gate.")]
        public bool activeAtSpawn = true;

        private bool _pickedUp;

        /// <summary>True once the axe has been picked up here. Exposed for PlayMode tests + capture.</summary>
        public bool HasPickedUp => _pickedUp;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            if (visual == null) visual = transform;

            // #100 BUG-1: when authored inactive, hide the redundant second world axe at spawn so the dial-tool
            // soak presents ONE clear acquisition axe (the StumpAxe craft cue). The pickup logic also stands
            // down (Update early-outs on !activeAtSpawn) so an invisible axe can never be silently collected.
            if (!activeAtSpawn) HideVisual();
        }

        void Update()
        {
            if (!activeAtSpawn || _pickedUp || inventory == null || player == null) return;

            Vector2 axe = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            if (Vector2.Distance(axe, here) > pickupRadius) return;

            // Reached the axe — add it + auto-place in belt slot 1 (the tool seam).
            if (inventory.PickUpAxe())
                PickupTrace("picked up the axe -> belt slot 1");

            // Latch + consume the world axe regardless (a pre-owned axe still consumes the world pickup so
            // we never leave a duplicate lying around).
            _pickedUp = true;
            ConsumeWorldAxe();
        }

        // Hide the world axe's renderers (it's now in the belt). Keep the GameObject (its transform may be
        // read by tests/bounds) — only the visuals go.
        private void ConsumeWorldAxe() => HideVisual();

        // Hide the pickup's visual renderers (the GameObject + component + wiring stay — the AC3 PoC + its
        // EditMode presence/wiring guard carry forward; #100 BUG-1 uses this to suppress the redundant second
        // spawn axe).
        private void HideVisual()
        {
            var root = visual != null ? visual : transform;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = false;
        }

        // [AxePickup] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call
        // (AND its argument evaluation, incl. the string concatenation) from the shipped IL2CPP release exe so
        // the trace never costs the player a string alloc + log write (unity6-mastery §5 "no Debug.Log in hot
        // paths" / §10 "strip all logging from shipping builds"). The _pickedUp latch keeps it one-shot.
        // Matches the project dev-log gate convention (BerryBush/DrinkAction/EatBerryAction/FreshwaterPond).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void PickupTrace(string msg) => Debug.Log("[AxePickup] " + msg);
    }
}
