using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Gates the AXE-ON-THE-STUMP's visibility as the INVERSE of <see cref="HeldAxe"/> (ticket 86ca8ce6y —
    /// SOAKFIX2). The Sponsor's literal ask: "stump is there but no axe" — an axe should be VISIBLY present
    /// resting on/in the chopping-block stump FROM SPAWN, so (a) a hero axe is ALWAYS on screen and (b) it
    /// doubles as the diegetic "walk here" cue. On reaching the stump the craft fires (Inventory.HasAxe →
    /// true): the stump-axe HIDES and the HELD axe (HeldAxe) APPEARS — which reads as "the kid picks it up".
    ///
    /// This is the mirror of HeldAxe: HeldAxe shows when HasAxe; this shows when NOT HasAxe. Subscribes to
    /// Inventory.Changed + applies the current state on enable, so it is correct at spawn (no axe yet →
    /// stump-axe SHOWN) and after the craft (→ hidden), with no per-frame polling. The Inventory reference
    /// is wired editor-time (serialized) with an Awake FindObjectOfType fallback.
    ///
    /// The stump-axe mesh is the SAME sourced hatchet FBX as the held axe (one asset, identical read),
    /// attached editor-time to the CraftSpot and serialized into Boot.unity (the editor-vs-runtime trap).
    /// </summary>
    public class StumpAxe : MonoBehaviour
    {
        [Tooltip("The ledger whose HasAxe drives this stump-axe's visibility (INVERSE of HeldAxe). " +
                 "Wired editor-time; scene-found fallback in Awake.")]
        public Inventory inventory;

        private Renderer[] _renderers;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
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

        // Show the stump-axe only BEFORE it's crafted (the INVERSE of HeldAxe). If there is no inventory
        // wired (defensive), default to VISIBLE so a wiring regression fails loud in the soak (an axe stuck
        // on the stump forever) rather than a silently-empty stump — the Sponsor's exact "no axe" complaint.
        private void Apply()
        {
            bool show = inventory == null || !inventory.HasAxe;
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers)
                if (r != null) r.enabled = show;
        }
    }
}
