using System;
using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// The damage-TYPE ↔ resistance/weakness hook (Combat POC 86cah7xxp, AC8a). A per-target TAG that
    /// modulates the incoming damage by its <see cref="DamageType"/> — "pierce beats X" real, WITHOUT an
    /// O(weapon × mob) table (AC8a): a target carries ONE small profile keyed by damage TYPE (3 entries),
    /// never a per-weapon matrix.
    ///
    /// The multiplier per type: 1 = neutral, &gt;1 = WEAK to that type (takes more), &lt;1 = RESISTANT
    /// (takes less). So a spear (Pierce) against a pierce-WEAK snake does MORE than a neutral/resistant hit
    /// — the AC8a "pierce on pierce-weak &gt; neutral/resistant" assertion. Data-driven + serializable so a
    /// target authors its own profile in the Inspector; the default profile is all-neutral (no matchup).
    ///
    /// Pure struct-like value logic in <see cref="Multiplier"/> (dependency-free) so the matchup math is
    /// unit-testable headlessly. Shared vocabulary: the snake POC (86caaz4vn) authors ITS profile against
    /// this SAME type — this POC owns it (parallel-shared-concept naming discipline).
    /// </summary>
    [Serializable]
    public struct ResistanceProfile
    {
        [Tooltip("Damage multiplier vs SLASH (axe). 1 = neutral, >1 = weak (takes more), <1 = resistant.")]
        public float slashMul;
        [Tooltip("Damage multiplier vs PIERCE (spear). 1 = neutral, >1 = weak (takes more), <1 = resistant.")]
        public float pierceMul;
        [Tooltip("Damage multiplier vs BLUNT. 1 = neutral, >1 = weak, <1 = resistant.")]
        public float bluntMul;

        /// <summary>An all-neutral profile (every type × 1.0) — no matchup. The default a target uses until
        /// it authors weaknesses. A snake authors pierceMul &gt; 1 (soft body, weak to a thrust) here.</summary>
        public static ResistanceProfile Neutral => new ResistanceProfile { slashMul = 1f, pierceMul = 1f, bluntMul = 1f };

        /// <summary>
        /// The resistance/weakness multiplier for an incoming <paramref name="type"/>. A ZERO or negative
        /// authored multiplier (an un-initialized default struct reads all-zero) is treated as NEUTRAL (1),
        /// so a target that forgot to author its profile takes normal damage rather than becoming invincible
        /// (the silent-zero trap). Clamped ≥ 0 defensively (a negative multiplier would HEAL on hit).
        /// </summary>
        public float Multiplier(DamageType type)
        {
            float m;
            switch (type)
            {
                case DamageType.Pierce: m = pierceMul; break;
                case DamageType.Blunt:  m = bluntMul;  break;
                default:                m = slashMul;  break;
            }
            // A default(struct) reads all-zero; treat "unauthored" (<= 0) as neutral so a missing profile
            // never makes a target immortal. A real resistance is authored as e.g. 0.5, never 0.
            return m <= 0f ? 1f : m;
        }
    }
}
