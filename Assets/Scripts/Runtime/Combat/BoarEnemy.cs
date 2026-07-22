using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// The WILD BOAR enemy (ticket 86cah7ydt — the SECOND enemy that proves the weapon-vs-mob MATCHUP is
    /// EMERGENT, not scripted). A pure CONSUMER of the Combat POC's shared surface (86cah7xxp): the SAME
    /// <see cref="Health"/> component (AC1), the SAME <see cref="ResistanceProfile"/> damage-type↔resistance
    /// TAG (AC1/AC3), and the SAME <see cref="StatusEffectController"/> bleed framework (AC4). It adds NO new
    /// combat code path — it is DATA (HP + a weak-to-PIERCE resistance tag + per-tier gore) plus a CHARGE
    /// behaviour (<see cref="BoarAI"/>). The direct mirror of <see cref="SnakeEnemy"/>.
    ///
    /// === THE CORE PROOF — "spear beats boar" EMERGES, it is NOT hardcoded (AC3) ===
    /// There is NO weapon×mob matchup table anywhere. The spear advantage falls out of TWO INDEPENDENT
    /// systemic facts that COMPOSE:
    ///   (a) REACH — the spear's long <see cref="WeaponDef.Reach"/> (3.6 vs the axe's 2.0) lets the player
    ///       hit the CHARGING boar before its gore lands (the charge, <see cref="BoarAI"/>, makes reach
    ///       matter). Pure weapon attribute (POC AC4) — nothing boar-specific.
    ///   (b) WEAK-TO-PIERCE — the boar authors <see cref="BoarPierceWeakness"/> (&gt;1) on the shared
    ///       <see cref="Health.resistance"/> hook, so a Pierce (spear) hit is amplified; it is mildly
    ///       slash-RESISTANT (<see cref="BoarSlashResist"/> &lt;1 — a thick bristly hide/shoulder shield) so
    ///       the axe stays USABLE but worse (AC3: worse, not blocked). Pure per-target TAG (POC AC8) — NOT a
    ///       lookup. Delete the tag (Neutral) and the spear's bonus VANISHES — the systemic path, not a table.
    ///
    /// === Gore (AC2/AC4) — the charge's payload, through the ONE shared seam ===
    /// <see cref="Gore"/> deals <see cref="goreDamage"/> to the player through the SAME
    /// <see cref="Health.ApplyDamage"/> seam a weapon uses against the boar (one seam, both directions) — the
    /// tusk gore is Pierce-typed. It optionally applies <see cref="goreBleed"/> to the player's status
    /// controller (AC4 — the SECOND consumer of the POC bleed framework after the snake bite, proving the
    /// framework generalises). A no-op if the boar is dead or the target is null/dead.
    ///
    /// NO MUTABLE STATICS (instance state only) — StaticStateResetTests needs no reset here.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class BoarEnemy : MonoBehaviour
    {
        // === Per-tier HP (AC6 — a boar is TOUGHER than the snake's 24; gentle-for-kids on easy, leaner-but-
        //     meaner on hard). ApplyDifficulty copies the active tier's value into the Health.max the seam reads. ===
        /// <summary>Boar HP on EASY (roomier for kids — fewer, softer fights).</summary>
        public const float BoarEasyMaxHp = 32f;
        /// <summary>Boar HP on MEDIUM (the default — a real fight, ~3 spear / ~4 axe hits at default weapons).</summary>
        public const float BoarMedMaxHp = 40f;
        /// <summary>Boar HP on HARD (leaner bar but a nastier charge).</summary>
        public const float BoarHardMaxHp = 50f;

        /// <summary>The boar's PIERCE weakness (AC1/AC3 — the systemic proof): a spear (Pierce) hit is
        /// multiplied by this (&gt;1 = takes MORE). Stronger than the snake's 1.6 — the boar is THE
        /// matchup-proof enemy, so the pierce advantage must READ clearly at the soak.</summary>
        public const float BoarPierceWeakness = 2.0f;
        /// <summary>The boar's SLASH resistance (AC3 — a thick bristly hide / shoulder shield): a slash (axe)
        /// hit is multiplied by this (&lt;1 = takes LESS). Keeps the axe USABLE but visibly worse than the
        /// spear (worse, not blocked). Blunt stays neutral (1.0). Still just the per-target resistance TAG —
        /// NOT a matchup table.</summary>
        public const float BoarSlashResist = 0.75f;

        // === Per-tier GORE damage (AC2/AC6 — the charge's payload; a boar gore hits harder than a snake bite). ===
        /// <summary>Gore damage on EASY (gentle for kids).</summary>
        public const float BoarEasyGoreDamage = 10f;
        /// <summary>Gore damage on MEDIUM (the default — a real chunk of the 100 player HP).</summary>
        public const float BoarMedGoreDamage = 18f;
        /// <summary>Gore damage on HARD (threatening for adults — a bad hit if you let it reach you).</summary>
        public const float BoarHardGoreDamage = 26f;

        [Header("HP (AC1/AC6 — per-tier; ApplyDifficulty copies the active tier's max into Health.max)")]
        [Tooltip("Boar HP on EASY.")] public float easyMaxHp = BoarEasyMaxHp;
        [Tooltip("Boar HP on MEDIUM (the default tier).")] public float medMaxHp = BoarMedMaxHp;
        [Tooltip("Boar HP on HARD.")] public float hardMaxHp = BoarHardMaxHp;

        [Header("Gore (AC2/AC4 — deals damage to the player through the shared Health.ApplyDamage seam)")]
        [Tooltip("Base gore damage dealt to the player (before the player's resistance + tier modulate it in " +
                 "ApplyDamage). A boar gore is Pierce-typed (tusks). The active tier writes this via " +
                 "ApplyDifficulty (AC6) — the per-tier fields below are the map.")]
        public float goreDamage = BoarMedGoreDamage;

        [Header("Per-tier gore damage (AC6). ApplyDifficulty copies into goreDamage.")]
        [Tooltip("Gore damage on EASY (gentle for kids).")] public float easyGoreDamage = BoarEasyGoreDamage;
        [Tooltip("Gore damage on MEDIUM (the default tier).")] public float medGoreDamage = BoarMedGoreDamage;
        [Tooltip("Gore damage on HARD (threatening for adults).")] public float hardGoreDamage = BoarHardGoreDamage;

        [Tooltip("The bleed the gore applies to the player (AC4 — the 2nd status-framework consumer after the " +
                 "snake bite; bleed works BOTH ways). StatusEffectSpec.None = the gore applies no bleed. The " +
                 "POC design's default: gore applies bleed on a successful gore hit.")]
        public StatusEffectSpec goreBleed = StatusEffectSpec.None;

        private Health _health;

        // Resolve the boar's Health lazily (EditMode has no Awake on AddComponent; the SnakeEnemy precedent).
        private Health ResolveHealth()
        {
            if (_health == null) _health = GetComponent<Health>();
            return _health;
        }

        /// <summary>The boar's own HP (AC1). Dies at 0 (mirror of the player / snake model). The shared surface.</summary>
        public Health Health => ResolveHealth();

        /// <summary>
        /// The boar's authored weak-to-PIERCE / slash-RESISTANT resistance profile (AC1/AC3) — the per-target
        /// TAG that makes the pierce matchup systemic. Pierce &gt; 1 (weak), slash &lt; 1 (resistant), blunt
        /// neutral. Static so the bootstrap authoring + the tests read ONE source (the SnakeEnemy precedent).
        /// </summary>
        public static ResistanceProfile BoarResistance => new ResistanceProfile
        {
            slashMul = BoarSlashResist,
            pierceMul = BoarPierceWeakness,
            bluntMul = 1f,
        };

        /// <summary>
        /// Set the ACTIVE per-tier HP-max + gore damage from the difficulty tier (AC6 — "read the active
        /// difficulty setting"; gentle on easy, threatening on hard). Mirrors <see cref="SnakeEnemy.ApplyDifficulty"/>:
        /// the per-tier map fields are copied into the single live fields the fight reads. The HP-max write goes
        /// to the shared <see cref="Health.max"/>; a full-HP boar is refilled to the new max (RestoreFull) so a
        /// tier change at spawn starts the boar at the tier's full HP. Pure field copy (no scene deps) — EditMode
        /// asserts the easy &lt; med &lt; hard orderings directly.
        /// </summary>
        public void ApplyDifficulty(FarHorizon.SurvivalNeed.DifficultyTier tier)
        {
            var h = ResolveHealth();
            switch (tier)
            {
                case FarHorizon.SurvivalNeed.DifficultyTier.Easy:
                    goreDamage = easyGoreDamage; if (h != null) h.max = easyMaxHp; break;
                case FarHorizon.SurvivalNeed.DifficultyTier.Hard:
                    goreDamage = hardGoreDamage; if (h != null) h.max = hardMaxHp; break;
                default:
                    goreDamage = medGoreDamage; if (h != null) h.max = medMaxHp; break;
            }
            // A boar at/above its old max starts the new tier full (a fresh spawn); a wounded boar keeps its HP
            // re-clamped to the new max (RestoreFull only when full so a live mid-fight tier change is fair).
            if (h != null && h.Current >= h.Max) h.RestoreFull();
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            // Author the weak-to-pierce / slash-resistant profile (AC1/AC3) if the component ships neutral/unset
            // (a bare rig). A scene-authored profile (bootstrap) still wins — only seed when unset.
            if (_health != null && _health.resistance.pierceMul <= 0f)
                _health.resistance = BoarResistance;
            // Default the gore bleed to an active bleed if unset (the enemy→player bleed proof — AC4). A
            // scene-authored spec wins. A boar gore bleeds a bit heavier than the snake bite's 1.5.
            if (!goreBleed.IsActive) goreBleed = StatusEffectSpec.MakeBleed(2f, 3f);
        }

        /// <summary>
        /// Gore the given player <paramref name="playerHealth"/> (AC2/AC4) — deals <see cref="goreDamage"/>
        /// through the SHARED <see cref="Health.ApplyDamage"/> seam (Pierce — tusks), and applies the
        /// <see cref="goreBleed"/> to the player's status controller if the gore carries one (AC4 —
        /// enemy→player bleed, the 2nd status consumer). A no-op if the BOAR is dead (a dead boar doesn't gore)
        /// or the target is null/dead. Returns the HP the gore removed (0 if no-op).
        /// </summary>
        public float Gore(Health playerHealth)
        {
            var self = ResolveHealth();
            if (self == null || self.IsDead) return 0f;
            if (playerHealth == null || playerHealth.IsDead) return 0f;

            float removed = playerHealth.ApplyDamage(goreDamage, DamageType.Pierce);

            if (goreBleed.IsActive)
            {
                var sec = playerHealth.GetComponent<StatusEffectController>();
                if (sec != null) sec.Apply(goreBleed);
            }
            return removed;
        }
    }
}
