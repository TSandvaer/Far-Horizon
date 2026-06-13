using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Gates the HELD sourced hatchet's visibility on <see cref="Inventory.HasAxe"/> (ticket 86ca8ce6y).
    ///
    /// The sourced axe is attached editor-time to the chibi's right-hand bone (RightHand_010) and
    /// SERIALIZES into Boot.unity riding that bone (MovementCameraScene.AttachHeroAxeToHand). This
    /// component flips the axe's renderers on/off so the kid is empty-handed until the craft fires
    /// (Inventory.CraftAxe sets HasAxe), then holds the hatchet — the craft reads as "the kid picks up
    /// the axe". The CHOP gate (ChopTree) is independent of this visual: it reads Inventory.HasAxe (data)
    /// directly, so the chop still fires regardless of the renderer state.
    ///
    /// Subscribes to Inventory.Changed (fires on craft) + applies the current state on enable, so it is
    /// correct both at spawn (no axe -> hidden) and after a craft (-> shown), with no per-frame polling.
    /// The Inventory reference is wired editor-time (serialized) with an Awake FindObjectOfType fallback.
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

        // Show the held axe only once it's crafted. If there is no inventory wired (defensive), default to
        // VISIBLE so a wiring regression fails loud in the soak (a visible axe with no craft) rather than a
        // silently-invisible hero tool.
        private void Apply()
        {
            bool show = inventory == null || inventory.HasAxe;
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers)
                if (r != null) r.enabled = show;
        }
    }
}
