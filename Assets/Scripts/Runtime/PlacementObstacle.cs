using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Marks a GameObject as a NO-BUILD zone for placement (ticket 86catqxm0 — the ② adopt-me seam). Add this to
    /// any placeable / mineable (e.g. ②'s minable boulders, 86camz9v7) and the crafting-table placement ghost
    /// reads RED over it — NO collider needed (the world is deliberately collider-free; see
    /// <see cref="PlacementObstacleRegistry"/> for the full rationale). Self-registers on enable / unregisters on
    /// disable, so a POOLED object that toggles active (e.g. the ore-rarity dial disabling spare nodes) projects
    /// its no-build zone ONLY while it is live.
    ///
    /// NO mutable statics (instance-only) — needs no [RuntimeInitializeOnLoadMethod] reset (StaticStateResetTests).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlacementObstacle : MonoBehaviour
    {
        [Tooltip("Planar (XZ) radius of the no-build zone this object projects — roughly its footprint radius. " +
                 "Soak-tune. Default 0.6.")]
        public float footprintRadius = 0.6f;

        void OnEnable() => PlacementObstacleRegistry.Register(transform, footprintRadius);
        void OnDisable() => PlacementObstacleRegistry.Unregister(transform);
    }
}
