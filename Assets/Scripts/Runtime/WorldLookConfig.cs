using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// LIVE-DIALABLE world-look knobs (ticket 86ca8t9pq — Sponsor soak of 4457d47 "doesn't look good"
    /// + "consult Erik for fixing sky, cloud, mountain, water"). These mirror the editor-time bootstrap
    /// values so the F9 WorldLookNudgeTool can re-tune the look IN THE SHIPPED BUILD and the Sponsor
    /// bakes the numbers he likes — instead of the team grinding blind soak iterations on a look only the
    /// Sponsor can judge ([[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]]).
    ///
    /// TWO ROLES:
    ///   (1) BAKE-TIME DEFAULTS — the editor bootstrap (WorldBootstrap / QualityPassGen) reads the static
    ///       defaults below so the shipped scene is built with these values (one source of truth: change a
    ///       default here -> the next bootstrap bakes it).
    ///   (2) RUNTIME RE-TUNE — the WorldLookNudgeTool mutates the LIVE RenderSettings (fog distance/colour),
    ///       the skybox material stops, and the cloud/mountain transforms directly in the running build; the
    ///       Sponsor reads the dialed values off the panel/log and reports them to bake.
    ///
    /// MOUNTAIN double-fade fix (the "floating translucent shards" root cause, Drew diagnosis): the far
    /// clusters were faded toward the horizon stop TWICE (per-cluster tint + Exp^2 fog). MtnFadeCap caps the
    /// tint contribution; MtnDistanceScale pulls the clusters IN out of the fog ghost band. Both are dialable.
    /// </summary>
    public static class WorldLookConfig
    {
        // ---- MOUNTAINS (the double-fade fix) ----
        // Cap on the per-cluster atmospheric TINT lerp toward the horizon stop (0 = no tint fade, mesh full
        // contrast; 1 = the old uncapped fade that ghosted the far clusters). 0.25 keeps a readable grey-to-
        // snow silhouette and lets the FOG alone do the recession.
        public const float MtnFadeCap = 0.25f;
        // Multiplier on every cluster's distance from the player. <1 pulls the islands IN so the Exp^2 fog
        // (which hits 38% blend at 430u, 90% at 950u) no longer washes them to ghosts. 0.55 lands the far
        // clusters in the ~240-310u band (fog blend ~15-21%) — atmospheric but solid, not see-through.
        public const float MtnDistanceScale = 0.55f;

        // ---- FOG (seam-kill atmosphere — Erik Route A / Uma §3) ----
        public const float FogDensityDefault = 0.0016f;

        // ---- CLOUDS (Uma §1) ----
        public const int CloudCountMin = 6;
        public const int CloudCountMax = 10;
    }
}
