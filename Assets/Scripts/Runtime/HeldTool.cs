using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Generalized visibility gate for a HELD tool/weapon (axe, knife, sword, spear, …) — ticket
    /// 86cabh907, Route A weapon set. Flips the held item's renderers on/off based on a selection
    /// predicate (<see cref="ShouldShow"/>), subscribing to <see cref="Inventory.Changed"/> so it is
    /// correct at spawn and after every selection/move (no per-frame polling). This is VISIBILITY ONLY —
    /// the seat/follow pose is owned by <see cref="HeldToolRig"/> and is never touched here.
    ///
    /// <see cref="HeldAxe"/> is a thin back-compat subclass that gates on the axe being the selected belt
    /// item (the held-axe visibility contract, AC4 86caa4bya); the wiring + caching live here so any
    /// future family item reuses the same gate by overriding <see cref="ShouldShow"/>.
    /// </summary>
    public class HeldTool : MonoBehaviour
    {
        [Tooltip("The ledger whose selection drives this held tool's visibility. Wired editor-time; " +
                 "scene-found fallback in Awake.")]
        public Inventory inventory;

        private Renderer[] _renderers;

        protected virtual void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            // Cache the renderer subtree (a single-mesh FBX, but cache the subtree so a future multi-part
            // tool still toggles whole).
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        /// Re-scan the renderer subtree and re-apply the current visibility (#100): the HeldWeaponCycleDebug
        /// soak handle re-homes the displayed mesh onto a NEW child holder at Awake (the rig-stomp fix), which
        /// adds a renderer this gate may have cached before. Calling this after that re-home keeps the gate
        /// authoritative over the child holder's renderer regardless of Awake order. Idempotent.
        /// </summary>
        public void RefreshRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            Apply();
        }

        protected virtual void OnEnable()
        {
            if (inventory != null) inventory.Changed += Apply;
            Apply();
        }

        protected virtual void OnDisable()
        {
            if (inventory != null) inventory.Changed -= Apply;
        }

        /// <summary>Whether this held tool should be visible for the current inventory state. Override per
        /// item. Defensive default when no inventory is wired: VISIBLE, so a wiring regression fails loud in
        /// the soak (a visible tool with no inventory) rather than a silently-invisible item.</summary>
        protected virtual bool ShouldShow() => true;

        protected void Apply()
        {
            bool show = inventory == null || ShouldShow();
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers)
                if (r != null) r.enabled = show;
        }
    }
}
