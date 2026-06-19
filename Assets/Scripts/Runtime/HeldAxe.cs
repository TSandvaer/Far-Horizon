using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Gates the HELD sourced hatchet's VISIBILITY (ticket 86ca8ce6y; AC4 86caa4bya).
    ///
    /// The sourced axe is attached editor-time to the chibi's right-hand bone and SERIALIZES into
    /// Boot.unity riding that bone (MovementCameraScene.AttachHeroAxeToHand). This component flips the
    /// axe's renderers on/off — VISIBILITY ONLY (the seat/follow pose is owned by HeldAxeRig and is
    /// Sponsor-LOCKED; this component never touches it).
    ///
    /// AC4 (86caa4bya) — the held axe shows IN-HAND only when the axe is the SELECTED belt item, NOT
    /// merely owned: it gates on <see cref="Inventory.IsAxeSelectedInBelt"/>. Axe in belt slot 2 with
    /// slot 1 selected -> hidden; select slot 2 -> shown; axe moved off the belt into the pack ->
    /// hidden (still owned, but not the selected belt item). This SUPERSEDES the old HasAxe (ownership)
    /// gate: before the belt existed, "owns the axe" == "holds the axe", so HasAxe was correct; with the
    /// belt, selection is the right signal (item-model contract §5). The CHOP gate (ChopTree) is
    /// independent of this visual — it reads Inventory.HasAxe (ownership) directly.
    ///
    /// Subscribes to Inventory.Changed (fires on pickup/select/move) + applies the current state on
    /// enable, so it is correct at spawn (no axe -> hidden) and after every selection/move (no per-frame
    /// polling). The Inventory reference is wired editor-time (serialized) with an Awake FindObjectOfType
    /// fallback.
    /// </summary>
    public class HeldAxe : MonoBehaviour
    {
        [Tooltip("The ledger whose HasAxe drives this held axe's visibility. Wired editor-time; " +
                 "scene-found fallback in Awake.")]
        public Inventory inventory;

        private Renderer[] _renderers;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            // Cache the hatchet's renderers (the sourced FBX is a single mesh, but cache the subtree so a
            // future multi-part axe still toggles whole).
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        void OnEnable()
        {
            if (inventory != null) inventory.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            if (inventory != null) inventory.Changed -= Apply;
        }

        // Show the held axe only when the axe is the SELECTED belt item (AC4). If there is no inventory
        // wired (defensive), default to VISIBLE so a wiring regression fails loud in the soak (a visible
        // axe with no inventory) rather than a silently-invisible hero tool.
        private void Apply()
        {
            bool show = inventory == null || inventory.IsAxeSelectedInBelt;
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers)
                if (r != null) r.enabled = show;
        }
    }
}
