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
        // The 3 sky stops. CHEERFUL-SKY SOAK-FIX (86ca8t9pq S2, Sponsor soak of fa9f1b1: "sky is a greyish
        // blue, with no clear indications of clouds"). DIAGNOSE-VIA-TRACE OVERTURNED my W4 framing (W4 made
        // the ZENITH saturated, but the gameplay over-shoulder orbit looks slightly DOWN at pitch 55, so its
        // view rays into the sky have a LOW dir.y — the visible sky band is the GradientSkybox's t<=_MidPoint
        // region, i.e. lerp(Horizon, Mid). W4's saturated ZENITH only renders high overhead, which the orbit
        // barely frames. So the band the Sponsor actually SEES was the pale Mid (0.60,0.78,0.90) -> cream
        // Horizon (0.88,0.90,0.84) blend = greyish blue, then desaturated further by the warm post grade +
        // fog. FIX: push the VISIBLE band cheerful — a more saturated clear-day MID blue + a brighter cheerful
        // ZENITH, and LOWER _MidPoint (QualityPassGen/GradientSkybox) so the saturated blue drops into the
        // gameplay frame. The board (21h16_13 / 21h13_31) is a bright cheerful blue right down to the horizon
        // haze. Horizon stays the warm seam-kill anchor (== fog colour == far-range tint) but a touch cooler-
        // bright so it reads sky-cheerful, not cream-grey. All sub-1.0 / HDR-clamp-safe. F9/F10 SKY-dialable.
        public static readonly Color SkyZenith  = new Color(0.30f, 0.58f, 0.88f); // #4D94E0 cheerful deep clear-day blue
        public static readonly Color SkyMid      = new Color(0.45f, 0.70f, 0.92f); // #73B3EB saturated cheerful mid sky-blue (was pale .60/.78/.90)
        // THE load-bearing seam-kill anchor: the horizon stop. fog colour == this == the bottom of the
        // gradient skybox == the farthest vista range tint family. Kept light + warm-leaning so the vista
        // dissolves into it, but pulled toward a brighter sky-cheerful tone (less cream-grey) so the lower
        // visible sky band reads cheerful, not greyish.
        public static readonly Color SkyHorizon  = new Color(0.80f, 0.89f, 0.92f); // #CCE3EB bright cheerful sky-haze horizon
    }
}
