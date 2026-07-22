using System;
using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// HIT POINTS — a DEDICATED MonoBehaviour, NOT a <see cref="FarHorizon.SurvivalNeed"/> (Combat POC
    /// 86cah7xxp, LOCKED decision 1 / hard-constraint 1). Needs (warmth/hunger/thirst) rest at a FLOOR and
    /// never hit zero; HP takes ACUTE damage and 0 HP = DEATH. The shapes differ, so HP is its own type —
    /// regen (AC3) READS the SurvivalNeed surface but HP is never folded into it.
    ///
    /// === THE SHARED ENEMY-HP SURFACE (hard-constraint 2 / AC7) ===
    /// This ONE component is BOTH the player HP (AC1) and the enemy HP (AC7 — the snake). It lands FIRST
    /// (this POC) so it OWNS the type + surface; the snake POC (86caaz4vn) adds a Health to the snake and
    /// authors its <see cref="resistance"/> — it does NOT re-declare an enemy-HP type (parallel-shared-
    /// concept naming discipline: the type is `FarHorizon.Combat.Health`, the guard/event names below are
    /// the pinned vocabulary). A bite deals damage to the player through the SAME <see cref="ApplyDamage"/>
    /// seam a weapon uses against the snake — one seam, both directions.
    ///
    /// === Public read surface (the HUD CONTRACT — AC1/AC9; the SurvivalHud bar binds EXACTLY this) ===
    ///   float  Current   : current HP (0..Max).
    ///   float  Max       : max HP (per-difficulty-tier, AC8b).
    ///   float  Current01 : HP normalized 0..1 (the HP bar fill). Always clamped.
    ///   bool   IsDead    : Current &lt;= 0.
    ///   event Action&lt;float&gt; Changed : fires Current01 on ANY change (damage/heal/set). The HUD
    ///                        subscribes and NEVER polls (AC9). Byte-shape-identical to SurvivalNeed.Changed
    ///                        so the ONE SurvivalHud DrawNeedBar widget renders HP with no new code path.
    ///   event Action Died : fires ONCE on the transition to dead (drives AC2 tiered death).
    ///
    /// === ONE damage entry (AC1) ===
    /// <see cref="ApplyDamage"/> is the SINGLE mutation seam every source shares — bleed DoT (AC6), enemy
    /// hits (AC7), hazards. It applies the per-target <see cref="resistance"/> matchup (AC8a) to the raw
    /// amount by <see cref="DamageType"/>, subtracts, clamps at 0, fires Changed, and fires Died on the
    /// 0-crossing. Idempotent at 0 (a hit on an already-dead target does nothing but is not an error).
    ///
    /// NO decay/Update — HP does not tick on its own (unlike a need). Regen is DRIVEN externally by
    /// <see cref="HealthRegen"/> (AC3) so the Time.time-window discipline lives in ONE place, not two.
    /// NO MUTABLE STATICS (instance state only) — the StaticStateResetTests audit needs no reset here.
    /// </summary>
    public sealed class Health : MonoBehaviour
    {
        [Header("HP (per-difficulty-tier max — AC8b)")]
        [Tooltip("Max HP (the bar's full value). Per-tier: ApplyDifficulty(tier) sets this from the " +
                 "easy/med/hard maxes below. The single field the read surface + damage path use.")]
        public float max = 100f;

        [Tooltip("Start at max HP on Start(). A freshly-spawned/respawned entity begins at full HP.")]
        public bool startFull = true;

        [Header("Damage-type ↔ resistance (AC8a — 'pierce beats X'; NO weapon×mob table)")]
        [Tooltip("The per-target damage-TYPE resistance/weakness profile. Neutral (all 1.0) by default; a " +
                 "snake authors pierceMul > 1 (soft body, weak to a thrust). Modulates ApplyDamage.")]
        public ResistanceProfile resistance = ResistanceProfile.Neutral;

        [Header("Per-tier HP max (AC8b — easy / med / hard). ApplyDifficulty copies into max.")]
        [Tooltip("Max HP on EASY (roomier for kids). ApplyDifficulty(Easy) copies this into max.")]
        public float easyMax = 120f;
        [Tooltip("Max HP on MEDIUM (the default tier). ApplyDifficulty(Medium) copies this into max.")]
        public float medMax = 100f;
        [Tooltip("Max HP on HARD (leaner for adults). ApplyDifficulty(Hard) copies this into max.")]
        public float hardMax = 80f;

        [Header("Per-tier damage-taken multiplier (AC8b). ApplyDifficulty copies into damageTakenMul.")]
        [Tooltip("Incoming-damage multiplier on EASY (< 1 softens every hit — kid-friendly).")]
        public float easyDamageTakenMul = 0.6f;
        [Tooltip("Incoming-damage multiplier on MEDIUM (1 = as-authored).")]
        public float medDamageTakenMul = 1f;
        [Tooltip("Incoming-damage multiplier on HARD (> 1 — every hit hurts more).")]
        public float hardDamageTakenMul = 1.35f;

        [Tooltip("ACTIVE incoming-damage multiplier (the single field ApplyDamage reads; the difficulty tier " +
                 "writes it via ApplyDifficulty). Applied on TOP of the per-type resistance matchup.")]
        public float damageTakenMul = 1f;

        private float _current;
        private bool _started;

        /// <summary>Fires Current01 (0..1) on ANY HP change — damage/heal/set. The HUD subscribes, never
        /// polls (AC9). Byte-shape-identical to SurvivalNeed.Changed so the ONE bar widget renders HP.</summary>
        public event Action<float> Changed;

        /// <summary>Fires exactly ONCE on the transition to dead (Current crosses to &lt;= 0). Drives the
        /// AC2 tiered-death handler. A subsequent ApplyDamage on a dead target does NOT re-fire it.</summary>
        public event Action Died;

        /// <summary>Current HP (0..Max). Seeds lazily on first read (EditMode has no Start lifecycle).</summary>
        public float Current { get { EnsureStarted(); return _current; } }

        /// <summary>Max HP (the bar's full value; per-difficulty-tier, AC8b).</summary>
        public float Max => max;

        /// <summary>HP normalized to 0..1 — the HP bar fill (AC9). Always clamped. Seeds lazily on read.</summary>
        public float Current01 { get { EnsureStarted(); return max <= 0f ? 0f : Mathf.Clamp01(_current / max); } }

        /// <summary>True once HP is at/below zero — death (AC1/AC2). Seeds lazily on read so a freshly-added
        /// (unstarted) full-HP Health does NOT read as dead in EditMode (which has no Start lifecycle).</summary>
        public bool IsDead { get { EnsureStarted(); return _current <= 0f; } }

        private void Start()
        {
            EnsureStarted();
        }

        // Seed HP once. Called from Start() in a scene AND lazily by the read properties + ApplyDamage/Heal so
        // an AddComponent-then-read/damage path (EditMode has no lifecycle) sees full HP first — mirroring
        // SurvivalNeed's seed-on-first-use, but here the READS also trigger it (a need is only ever driven by a
        // mutating call first; HP is often READ first, e.g. the HUD/IsDead gate). Idempotent.
        private void EnsureStarted()
        {
            if (_started) return;
            _started = true;
            _current = startFull ? max : 0f;
            Changed?.Invoke(Current01);
        }

        /// <summary>
        /// Set the per-tier HP MAX + incoming-damage multiplier (AC8b). Refills to the new max when at/above
        /// the old max (a fresh spawn on a difficulty change stays full); otherwise keeps the current HP
        /// (re-clamped to the new max). The active <see cref="max"/> / <see cref="damageTakenMul"/> stay the
        /// single fields the read surface + damage path use, so a live tier change takes effect immediately.
        /// </summary>
        public void ApplyDifficulty(FarHorizon.SurvivalNeed.DifficultyTier tier)
        {
            EnsureStarted();
            bool wasFull = _current >= max;
            switch (tier)
            {
                case FarHorizon.SurvivalNeed.DifficultyTier.Easy:
                    max = easyMax; damageTakenMul = easyDamageTakenMul; break;
                case FarHorizon.SurvivalNeed.DifficultyTier.Hard:
                    max = hardMax; damageTakenMul = hardDamageTakenMul; break;
                default:
                    max = medMax; damageTakenMul = medDamageTakenMul; break;
            }
            SetCurrent(wasFull ? max : Mathf.Min(_current, max));
        }

        /// <summary>
        /// THE single damage seam (AC1) — bleed (AC6) + enemy hits (AC7) + hazards all route through this.
        /// The effective damage = <paramref name="amount"/> × the per-type <see cref="resistance"/> matchup
        /// (AC8a) × the active <see cref="damageTakenMul"/> (AC8b tier). Subtracts, clamps at 0, fires
        /// Changed, and fires <see cref="Died"/> once on the 0-crossing. A zero/negative amount is a no-op;
        /// a hit on an already-dead target is a no-op (no re-death). Returns the ACTUAL HP removed (0 if the
        /// target was dead / the amount was non-positive) so a caller can attribute damage.
        /// </summary>
        public float ApplyDamage(float amount, DamageType type)
        {
            EnsureStarted();
            if (amount <= 0f || _current <= 0f) return 0f;

            float effective = amount * resistance.Multiplier(type) * Mathf.Max(0f, damageTakenMul);
            if (effective <= 0f) return 0f;

            float before = _current;
            bool wasAlive = _current > 0f;
            SetCurrent(_current - effective);
            float removed = before - _current;

            if (wasAlive && _current <= 0f) Died?.Invoke(); // one-shot death on the 0-crossing
            return removed;
        }

        /// <summary>Heal (a regen tick — AC3 — or a future heal source). Adds <paramref name="amount"/>,
        /// clamped to max, fires Changed on a real change. A no-op on a DEAD target (regen must not revive a
        /// corpse — revival is the AC2 respawn path, not a heal). Returns the new Current01.</summary>
        public float Heal(float amount)
        {
            EnsureStarted();
            if (amount <= 0f || _current <= 0f) return Current01;
            SetCurrent(_current + amount);
            return Current01;
        }

        /// <summary>Restore fully (a respawn — AC2). Sets HP to max, fires Changed. Used by the death
        /// handler to bring the castaway back at full HP.</summary>
        public void RestoreFull()
        {
            EnsureStarted();
            SetCurrent(max);
        }

        // Single write path: clamp to [0, max], fire Changed only on a real value change.
        private void SetCurrent(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, max);
            if (Mathf.Approximately(clamped, _current)) return;
            _current = clamped;
            Changed?.Invoke(Current01);
        }
    }
}
