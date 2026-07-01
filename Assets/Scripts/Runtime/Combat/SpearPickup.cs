using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// A PICKABLE spear in the world (Combat POC 86cah7xxp, AC4 — the second contrasting craftable weapon's
    /// acquisition). The castaway walks within <see cref="pickupRadius"/> of it and the spear is added to the
    /// belt via <see cref="FarHorizon.Inventory.PickUpSpear"/>; the world spear is then consumed (renderers
    /// hide) so it isn't duplicated. A direct sibling of <see cref="FarHorizon.AxePickup"/> — the SAME proven
    /// planar-XZ proximity idiom (CraftSpot / ChopTree / AxePickup), calling the SAME kind of Inventory seam
    /// (PickUpSpear mirrors PickUpAxe). The POC uses a pickup for acquisition; a proper craft-at-station is the
    /// same seam a later ticket flips to (find-in-world is OOS — this is a wired, deterministic acquisition).
    ///
    /// Serialized editor-time into Boot.unity (MovementCameraScene.BuildCombat), NOT at Awake (editor-vs-
    /// runtime trap) — the pickable mesh + this component + its Inventory/player refs serialize into the scene.
    /// </summary>
    public sealed class SpearPickup : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory the picked-up spear is added to. Wired at bootstrap; scene-found fallback.")]
        public FarHorizon.Inventory inventory;

        [Tooltip("The player transform whose proximity triggers the pickup. Wired at bootstrap; falls back " +
                 "to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Tooltip("The pickable spear's visual root, hidden once picked up. Falls back to this transform.")]
        public Transform visual;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway picks up the spear (mirrors AxePickup).")]
        public float pickupRadius = 2.0f;

        private bool _pickedUp;

        /// <summary>True once the spear has been picked up here. Exposed for PlayMode tests + capture.</summary>
        public bool HasPickedUp => _pickedUp;

        private void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<FarHorizon.Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<FarHorizon.ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            if (visual == null) visual = transform;
        }

        private void Update()
        {
            if (_pickedUp || inventory == null || player == null) return;

            Vector2 spear = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            if (Vector2.Distance(spear, here) > pickupRadius) return;

            if (inventory.PickUpSpear())
                PickupTrace("picked up the spear -> belt");

            _pickedUp = true;
            HideVisual(); // consume the world spear (now on the belt)
        }

        private void HideVisual()
        {
            var root = visual != null ? visual : transform;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void PickupTrace(string msg) => Debug.Log("[SpearPickup] " + msg);
    }
}
