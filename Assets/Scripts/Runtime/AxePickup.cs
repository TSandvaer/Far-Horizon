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
        }

        void Update()
        {
            if (_pickedUp || inventory == null || player == null) return;

            Vector2 axe = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            if (Vector2.Distance(axe, here) > pickupRadius) return;

            // Reached the axe — add it + auto-place in belt slot 1 (the tool seam).
            if (inventory.PickUpAxe())
                Debug.Log("[AxePickup] picked up the axe -> belt slot 1");

            // Latch + consume the world axe regardless (a pre-owned axe still consumes the world pickup so
            // we never leave a duplicate lying around).
            _pickedUp = true;
            ConsumeWorldAxe();
        }

        // Hide the world axe's renderers (it's now in the belt). Keep the GameObject (its transform may be
        // read by tests/bounds) — only the visuals go.
        private void ConsumeWorldAxe()
        {
            var root = visual != null ? visual : transform;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = false;
        }
    }
}
