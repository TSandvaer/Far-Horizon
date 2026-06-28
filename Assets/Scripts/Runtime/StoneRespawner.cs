using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The shared STONE-RESPAWN config (ticket 86caa4c96 AC3 / AC3a) — the single "stone-spawn component" the
    /// `stone respawn time` SETTING binds to. Every <see cref="StoneProp"/> in the world reads its respawn
    /// window from THIS one component (a per-instance fallback band when unwired), so ONE slider in the dev
    /// settings panel retunes EVERY stone's respawn timer at once — rather than the setting having to fan out
    /// across hundreds of scattered StoneProps.
    ///
    /// This mirrors how the chop feature centralises its tunable on a single host (the `tree regrowth time`
    /// setting binds to ChopTree.regrowthMin/Max — SettingsCatalog.PopulateChop): the SETTING binds to ONE
    /// live field-pair, and the gameplay reads through it. Here <see cref="SettingsCatalog.PopulateStones"/>
    /// binds `stone respawn time` to <see cref="RespawnMinSeconds"/> / <see cref="RespawnMaxSeconds"/>.
    ///
    /// === The respawn window is a RANGE (AC3 — "ideally a RANDOM value within a min/max RANGE") ===
    /// Each StoneProp rolls its own respawn delay uniformly within [<see cref="RespawnMinSeconds"/>,
    /// <see cref="RespawnMaxSeconds"/>] (organic, staggered respawns — stones don't all pop back at once). The
    /// default ~10-min window is the AC3 "~10 min default". The settings RANGE row clamps within
    /// <see cref="SettingsCatalog.StoneRespawnLower"/> .. <see cref="SettingsCatalog.StoneRespawnUpper"/> so the
    /// Sponsor can soak fast (near-instant) OR a realistic scarcity (long).
    ///
    /// === No mutable statics (StaticStateResetTests) ===
    /// Pure instance state (the two serialized seconds fields). The defaults are <c>const</c>. So this type has
    /// NO mutable runtime static and needs NO SubsystemRegistration reset (the StaticStateResetTests guard) —
    /// the sibling of WarmthNeed (instance-only state).
    ///
    /// === Serialization ===
    /// Authored editor-time + serialized into Boot.unity (LowPolyZoneGen.ScatterIslandProps adds one on the
    /// scatter root, before the stones, so every scatter StoneProp wires to it). NOT added at Awake.
    /// </summary>
    public class StoneRespawner : MonoBehaviour
    {
        /// <summary>AC3 "~10 min default" — the lower bound of the default respawn window (8 min).</summary>
        public const float DefaultMinSeconds = 8f * 60f;   // 480s

        /// <summary>AC3 "~10 min default" — the upper bound of the default respawn window (12 min), so the
        /// default rolls around the ~10-min centre.</summary>
        public const float DefaultMaxSeconds = 12f * 60f;  // 720s

        [Header("Respawn window (AC3 — TWEAKABLE; the `stone respawn time` setting drives these)")]
        [Tooltip("Minimum seconds before a looted stone respawns. The actual respawn time is a RANDOM value in " +
                 "[min,max] (organic, staggered — stones don't all pop back at once). The `stone respawn time` " +
                 "RANGE setting (SettingsCatalog.PopulateStones) drives this live; default ~10 min (AC3).")]
        public float RespawnMinSeconds = DefaultMinSeconds;

        [Tooltip("Maximum seconds before a looted stone respawns (random within [min,max]). The `stone respawn " +
                 "time` RANGE setting drives this live.")]
        public float RespawnMaxSeconds = DefaultMaxSeconds;
    }
}
