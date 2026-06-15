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
        // The 3 sky stops. NICER-SKY SOAK-FIX (86ca8t9pq W4, Sponsor soak of b54482c: "sky not nice —
        // improve the gradient sky (warmer/nicer per the board's cheerful palette)"). The b54482c sky read
        // PALE + slightly cold — the old mid/horizon stops washed to near-white so the dome looked flat and
        // bleached, not the board's (inspiration/21h16_13 + 21h13_31) cheerful saturated blue with a soft
        // warm horizon. FIX: a more SATURATED cheerful sky-blue at the zenith (deeper B, the board's clear-
        // day blue), a clean mid blue, and a WARM pale horizon (kept warm so the seam-kill still dissolves
        // the vista into it). The horizon stays the single seam-kill anchor (== fog colour == far-range tint).
        // All sub-1.0 / HDR-clamp-safe. Live-dialable via the F9 WorldLookNudgeTool SKY target.
        public static readonly Color SkyZenith  = new Color(0.38f, 0.62f, 0.85f); // #61A0D9 cheerful saturated sky-blue
        public static readonly Color SkyMid      = new Color(0.60f, 0.78f, 0.90f); // #99C7E6 clean mid blue
        // THE load-bearing seam-kill anchor: the warm horizon stop. fog colour == this == the bottom of
        // the gradient skybox == the farthest vista range tint family. Kept warm (R>=B) + a touch more
        // saturated so the horizon glows warm-cheerful, not bleached white.
        public static readonly Color SkyHorizon  = new Color(0.88f, 0.90f, 0.84f); // #E0E6D6 warm pale cream horizon
    }
}
