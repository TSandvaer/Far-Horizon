using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Canonical world-look polish palette anchors (ticket 86ca8t9pq — Uma world-look brief §3 sky-tint).
    /// Lives in the RUNTIME layer (not the editor bootstrap) so BOTH the editor bootstrap
    /// (QualityPassGen / WorldBootstrap, which set the sky + fog + far-range tints) AND the runtime +
    /// test code (PlayMode runtime guards, which assert the seam-kill holds at runtime) read the SAME
    /// values — the seam-kill relies on an EXACT colour match between the fog colour and the horizon sky
    /// stop (Erik far-vista Q2: "if the horizon stop shifts later, fog color updates in lockstep").
    /// Binding all three (sky horizon / fog / farthest-range tint) to ONE constant makes that drift
    /// structurally impossible.
    ///
    /// All sub-1.0 / HDR-clamp-safe per Uma's per-swatch verification.
    /// </summary>
    public static class WorldLookPalette
    {
        // The 3 sky stops (Uma §3).
        public static readonly Color SkyZenith  = new Color(0.50f, 0.71f, 0.84f); // #7FB4D6 soft warm blue
        public static readonly Color SkyMid      = new Color(0.67f, 0.82f, 0.89f); // #AAD0E2 pale warm blue
        // THE load-bearing seam-kill anchor: the warm horizon stop. fog colour == this == the bottom of
        // the gradient skybox == the farthest vista range tint family.
        public static readonly Color SkyHorizon  = new Color(0.86f, 0.91f, 0.89f); // #DCE8E4 warm pale cream-blue
    }
}
