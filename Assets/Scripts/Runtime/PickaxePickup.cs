using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A PICKABLE stone pickaxe in the world (ticket 86cakkmr0 / I-2 — the mine-loop SOAK enabler). The castaway
    /// walks within <see cref="pickupRadius"/> and the stone pickaxe (<see cref="ItemCatalog.PickaxeStoneId"/>) is
    /// added to the belt; the world pickaxe is then consumed (renderers hide). A direct sibling of
    /// <see cref="AxePickup"/> / <see cref="FarHorizon.Combat.SpearPickup"/> — the SAME proven planar-XZ proximity
    /// idiom. The mining VERB (<see cref="MineOre"/>) is active-left-click; the tool ACQUISITION being proximity-
    /// auto is a one-shot pickup consistent with the axe/spear (NOT a repeated action, so not the proximity-auto
    /// class [[active-input-not-proximity-auto-for-actions]] warns against).
    ///
    /// === Why this exists in I-2 (the soak-enabler, [[sponsor-rejects-unsoakable-placeholders]]) ===
    /// I-2 is a SOAK-SURFACE PR whose mine verb GATES on a pickaxe being the SELECTED belt item. For the Sponsor's
    /// mine-loop soak to be live-triggerable, the pickaxe must be OBTAINABLE in-game — this pickup is the honest,
    /// established acquisition (the axe/spear precedent). The real pickaxe mesh (I-1 / #283 delivered) is used;
    /// the iron-tier CRAFT unlock is I-4 (OOS here). NOTE the picked-up pickaxe lands in the first free belt slot
    /// (NOT auto-selected if the axe already holds slot 0) — the soak checklist tells the player to SELECT it
    /// (belt number key) before mining.
    ///
    /// === Adds to the belt via the public Model/Catalog API (no Inventory.cs edit — scope discipline) ===
    /// Mirrors what <see cref="Inventory.PickUpSpear"/> does internally (AddToolToBelt) but through the public
    /// <see cref="Inventory.Model"/> / <see cref="Inventory.Catalog"/> surface, so this I-2 feature does not touch
    /// the shared Inventory façade. Idempotent: a second pickup while a stone pickaxe is already owned is a no-op.
    ///
    /// Serialized editor-time into Boot.unity (MovementCameraScene.BuildPickaxePickup), NOT at Awake (editor-vs-
    /// runtime trap). Placed CLEAR of the player spawn by MORE than pickupRadius so it can never auto-grab belt
    /// slot 0 at spawn (the PR #224 chop-capture regression class — guarded by MineSceneTests).
    /// </summary>
    public sealed class PickaxePickup : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory the picked-up pickaxe is added to. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity triggers the pickup. Wired at bootstrap; falls back to the " +
                 "ClickToMove root, then a scene search.")]
        public Transform player;

        [Tooltip("The pickable pickaxe's visual root, hidden once picked up. Falls back to this transform.")]
        public Transform visual;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway picks up the pickaxe (mirrors AxePickup/SpearPickup).")]
        public float pickupRadius = 2.0f;

        private bool _pickedUp;

        /// <summary>True once the pickaxe has been picked up here. Exposed for PlayMode tests + capture.</summary>
        public bool HasPickedUp => _pickedUp;

        private void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            if (visual == null) visual = transform;
        }

        private void Update()
        {
            if (_pickedUp || inventory == null || player == null) return;

            Vector2 pick = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            if (Vector2.Distance(pick, here) > pickupRadius) return;

            if (TryGrant())
                PickupTrace("picked up the stone pickaxe -> belt");

            _pickedUp = true;
            HideVisual();
        }

        /// <summary>Add the stone pickaxe to the belt via the public Model/Catalog API (mirrors PickUpSpear's
        /// AddToolToBelt). Idempotent — returns false if a stone pickaxe is already owned. Public so a PlayMode
        /// test / verify capture can grant the pickaxe without a proximity walk.</summary>
        public bool TryGrant()
        {
            if (inventory == null) return false;
            var model = inventory.Model;
            var catalog = inventory.Catalog;
            if (model == null || catalog == null) return false;
            if (model.OwnsItem(ItemCatalog.PickaxeStoneId)) return false; // one-shot
            var pickaxe = catalog.ById(ItemCatalog.PickaxeStoneId);
            if (pickaxe == null) return false;
            return model.AddToolToBelt(pickaxe).HasValue;
        }

        private void HideVisual()
        {
            var root = visual != null ? visual : transform;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void PickupTrace(string msg) => Debug.Log("[PickaxePickup] " + msg);
    }
}
