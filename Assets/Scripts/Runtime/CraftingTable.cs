using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The PLACED crafting table (ticket 86camz9uz / crafting-redesign ① — replaces the auto-craft
    /// <c>CraftSpot</c> stump). A thin state+visual marker: it ships INVISIBLE (no pre-placed marker — the
    /// unified place-to-build rule, spec §2), and <see cref="CraftingTablePlacement"/> reveals it at the
    /// player-chosen pose. Once revealed, the castaway walks up to it and <see cref="CraftingMenuUI"/> opens
    /// the recipe menu.
    ///
    /// The table mesh (a flat work surface on legs — Bar 4 anchor: "a crafting table is a flat work surface
    /// on legs standing ON the ground") is a child authored editor-time (MovementCameraScene) so it
    /// SERIALIZES into Boot.unity (unity-conventions.md §editor-vs-runtime), its renderers DISABLED at author
    /// time so the table is invisible until placed. NO mutable statics (StaticStateResetTests stays green).
    /// </summary>
    public class CraftingTable : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The table's visual root (the flat-top-on-legs mesh). Its renderers are disabled until the " +
                 "table is placed (invisible-until-placed, spec §2). Falls back to this transform.")]
        public Transform visual;

        // The table ships UNBUILT/invisible; the placement Reveal()s it once. Instance state only.
        private bool _built;

        /// <summary>True once the table has been placed + revealed in the world. The menu only opens on a
        /// built table; scene-presence tests assert it ships false (invisible until placed).</summary>
        public bool IsBuilt => _built;

        void Awake()
        {
            if (visual == null) visual = transform;
            // Ship invisible — the renderers stay whatever the scene serialized (disabled at author time),
            // but re-assert hidden at Awake so a stale/edited scene can never spawn a pre-visible table.
            if (!_built) SetVisualEnabled(false);
        }

        /// <summary>
        /// Reveal the table at <paramref name="pose"/> (the placement's confirmed ghost pose): move this
        /// transform there, enable the visual renderers, latch built. Idempotent — a second call re-reveals
        /// at the new pose but the table is a one-per-game structure in ①. The placement calls this after the
        /// all-or-nothing material debit succeeds.
        /// </summary>
        public void Reveal(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            _built = true;
            SetVisualEnabled(true);
        }

        private void SetVisualEnabled(bool on)
        {
            var root = visual != null ? visual : transform;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = on;
        }
    }
}
