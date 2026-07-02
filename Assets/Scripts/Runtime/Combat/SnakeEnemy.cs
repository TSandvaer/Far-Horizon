using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// The SNAKE enemy (Combat POC 86cah7xxp, AC7) — a minimal damageable enemy proving the SHARED enemy-HP
    /// surface (hard-constraint 2). It has a <see cref="Health"/> (a mirror of the player HP model), an
    /// authored <see cref="ResistanceProfile"/> (pierce-WEAK — a soft body a thrust beats, AC8a), a
    /// <see cref="StatusEffectController"/> (so a weapon's bleed applies to it — bleed works BOTH ways,
    /// AC6/AC7), and a BITE that deals damage to the player through the SAME <see cref="Health.ApplyDamage"/>
    /// seam a weapon uses against the snake (one seam, both directions, AC7).
    ///
    /// === Shared surface (hard-constraint 2 / the snake POC 86caaz4vn) ===
    /// This POC LANDS the enemy <see cref="Health"/> type + the resistance vocabulary FIRST, so it OWNS them.
    /// The dedicated snake POC (86caaz4vn — currently gated) builds the snake's AI/movement/spawning on TOP
    /// of THIS Health + ResistanceProfile — it does NOT re-declare an enemy-HP type. This component is the
    /// minimal proof-of-surface; the full snake behavior is that ticket's scope (OOS here).
    ///
    /// The bite is a PLACEHOLDER trigger (<see cref="Bite"/>) the POC drives programmatically — no AI/pursuit
    /// (OOS). It proves the bite → player-damage path (+ optional bleed) works through the shared seam.
    ///
    /// The POC ships the snake with a pierce-weak profile so AC8a is demonstrable: a Pierce (spear) hit does
    /// MORE to the snake than a neutral/resistant (slash/axe) hit of the same base damage.
    ///
    /// NO MUTABLE STATICS (instance state only) — StaticStateResetTests needs no reset here.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class SnakeEnemy : MonoBehaviour
    {
        /// <summary>Snake HP default — leaner than the player (a snake dies in a few hits). Named so the
        /// bootstrap authoring + the tests read one source.</summary>
        public const float SnakeMaxHp = 24f;

        /// <summary>The snake's PIERCE weakness (AC8a): a spear (Pierce) hit is multiplied by this (&gt;1 =
        /// takes MORE). A soft-bodied snake is weak to a thrust; neutral to slash/blunt.</summary>
        public const float SnakePierceWeakness = 1.6f;

        /// <summary>The snake's BITE base damage to the player (through the shared seam). The player's
        /// resistance (neutral) + difficulty tier still modulate it in Health.ApplyDamage.
        /// KEPT for surface stability (86cah7xxp tests author it directly); the REAL snake (86caaz4vn)
        /// supersedes it at runtime via the per-tier maps below + <see cref="ApplyDifficulty"/>.</summary>
        public const float SnakeBiteDamage = 8f;

        // === Per-tier bite damage (86caaz4vn AC4 — MODERATE, difficulty-scaled; gentle on easy,
        // threatening on hard). The EXACT idiom Health/SurvivalNeed use for per-tier values:
        // easy/med/hard fields + ApplyDifficulty copies the active tier's value into the live field.
        /// <summary>Bite damage on EASY (gentle for kids — ~6% of the default 100 player HP).</summary>
        public const float SnakeEasyBiteDamage = 6f;
        /// <summary>Bite damage on MEDIUM (the default — a real chunk, ~12% of default player HP).</summary>
        public const float SnakeMedBiteDamage = 12f;
        /// <summary>Bite damage on HARD (threatening for adults — ~18% of default player HP).</summary>
        public const float SnakeHardBiteDamage = 18f;

        [Header("Bite (AC7 — deals damage to the player through the shared Health.ApplyDamage seam)")]
        [Tooltip("Base bite damage dealt to the player's Health (before the player's resistance + tier " +
                 "modulate it in ApplyDamage). A snake bite is Pierce-typed (fangs). The active difficulty " +
                 "tier writes this via ApplyDifficulty (86caaz4vn AC4) — the per-tier fields below are the map.")]
        public float biteDamage = SnakeBiteDamage;

        [Header("Per-tier bite damage (86caaz4vn AC4). ApplyDifficulty copies into biteDamage.")]
        [Tooltip("Bite damage on EASY (gentle for kids). ApplyDifficulty(Easy) copies this into biteDamage.")]
        public float easyBiteDamage = SnakeEasyBiteDamage;
        [Tooltip("Bite damage on MEDIUM (the default tier). ApplyDifficulty(Medium) copies this into biteDamage.")]
        public float medBiteDamage = SnakeMedBiteDamage;
        [Tooltip("Bite damage on HARD (threatening for adults). ApplyDifficulty(Hard) copies this into biteDamage.")]
        public float hardBiteDamage = SnakeHardBiteDamage;

        [Tooltip("Optional bleed the bite applies to the player (AC6/AC7 — bleed works BOTH ways). " +
                 "StatusEffectSpec.None = the bite applies no bleed. The POC ships a light bleed so the " +
                 "enemy→player bleed path is exercised.")]
        public StatusEffectSpec biteBleed = StatusEffectSpec.None;

        private Health _health;

        // Resolve the snake's Health lazily (EditMode has no Awake on AddComponent; the Bite seam + the Health
        // property both call this so a headless test never needs SendMessage("Awake")).
        private Health ResolveHealth()
        {
            if (_health == null) _health = GetComponent<Health>();
            return _health;
        }

        /// <summary>The snake's own HP (AC7). Dies at 0 (mirror of the player model). The shared surface.</summary>
        public Health Health => ResolveHealth();

        /// <summary>
        /// Set the ACTIVE bite damage from the difficulty tier (86caaz4vn AC4 — "read the active difficulty
        /// setting"; gentle on easy, threatening on hard). Mirrors <see cref="Health.ApplyDifficulty"/> /
        /// SurvivalNeed.ApplyDifficulty: the per-tier map fields are copied into the single live
        /// <see cref="biteDamage"/> the bite path reads, so a live tier change takes effect on the NEXT bite.
        /// Pure field copy (no scene deps) — EditMode asserts the easy &lt; med &lt; hard ordering directly.
        /// </summary>
        public void ApplyDifficulty(FarHorizon.SurvivalNeed.DifficultyTier tier)
        {
            switch (tier)
            {
                case FarHorizon.SurvivalNeed.DifficultyTier.Easy: biteDamage = easyBiteDamage; break;
                case FarHorizon.SurvivalNeed.DifficultyTier.Hard: biteDamage = hardBiteDamage; break;
                default:                                          biteDamage = medBiteDamage;  break;
            }
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            // Author the pierce-weak resistance profile (AC8a) if the component ships with a neutral/unset
            // one (a bare rig). A scene-authored profile (bootstrap) still wins — only seed when unset.
            if (_health != null && _health.resistance.pierceMul <= 0f)
            {
                _health.resistance = new ResistanceProfile
                {
                    slashMul = 1f,
                    pierceMul = SnakePierceWeakness, // WEAK to pierce (spear) — AC8a
                    bluntMul = 1f,
                };
            }
            // Default the bite bleed to a light bleed if unset (the enemy→player bleed proof). A scene-authored
            // spec wins. Kept small so it's a nag, not a killer.
            if (!biteBleed.IsActive) biteBleed = StatusEffectSpec.MakeBleed(1.5f, 3f);
        }

        /// <summary>
        /// Bite the given player <paramref name="playerHealth"/> (AC7) — deals <see cref="biteDamage"/>
        /// through the SHARED <see cref="Health.ApplyDamage"/> seam (Pierce — fangs), and applies the
        /// <see cref="biteBleed"/> to the player's status controller if the bite carries one (AC6/AC7 —
        /// enemy→player bleed). A no-op if the SNAKE is dead (a dead snake doesn't bite) or the target is
        /// null/dead. Returns the HP the bite removed (0 if no-op). Placeholder trigger — no AI (OOS).
        /// </summary>
        public float Bite(Health playerHealth)
        {
            var self = ResolveHealth();
            if (self == null || self.IsDead) return 0f;
            if (playerHealth == null || playerHealth.IsDead) return 0f;

            float removed = playerHealth.ApplyDamage(biteDamage, DamageType.Pierce);

            if (biteBleed.IsActive)
            {
                var sec = playerHealth.GetComponent<StatusEffectController>();
                if (sec != null) sec.Apply(biteBleed);
            }
            return removed;
        }
    }
}
