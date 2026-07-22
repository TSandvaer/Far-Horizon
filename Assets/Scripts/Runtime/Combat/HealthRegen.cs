using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// NEEDS-GATED HP regen (Combat POC 86cah7xxp, AC3). Slowly heals the player's <see cref="Health"/> —
    /// but ONLY while ALL of warmth AND hunger AND thirst are above threshold. A CRITICAL need STALLS regen
    /// (the default policy; a slow-drain-instead-of-stall variant is a Sponsor soak-tunable, exposed below).
    ///
    /// === Reads the need surface, never writes it (AC3 / hard-constraint 1) ===
    /// Regen READS SurvivalNeed <see cref="SurvivalNeed.Current01"/> / <see cref="SurvivalNeed.IsCritical"/>
    /// — it does NOT add/write a need, and HP is NOT a need (Health is a dedicated type, not a SurvivalNeed).
    /// The three needs are duck-typed the same way the SurvivalHud reads them: warmth is a standalone
    /// WarmthNeed; hunger/thirst extend SurvivalNeed — all three expose Current01 / IsCritical, so the gate
    /// reads those two per need. Any need may be null (a bare rig) → that need simply doesn't gate.
    ///
    /// === Time model (AC3 — mirror WarmthNeed §Time; headless-safe) ===
    /// Integrates regen over REAL <see cref="Time.time"/> deltas accumulated in Update — NOT per-frame
    /// Time.deltaTime (headless PlayMode sees deltaTime≈0). <see cref="TickSeconds"/> is the deterministic
    /// EditMode-driveable hook (no scene/Update). Regen adds regenPerSecond × seconds via
    /// <see cref="Health.Heal"/> (which clamps at max + no-ops on a dead target).
    ///
    /// The GATE decision is the pure static <see cref="ShouldRegen"/> so the whole truth-table (all-satisfied
    /// → regen; any below threshold OR critical → stall) is unit-testable headlessly, driving the need state
    /// via the SurvivalNeed test surface (AC10).
    ///
    /// NO MUTABLE STATICS (instance state only) — StaticStateResetTests needs no reset here.
    /// </summary>
    public sealed class HealthRegen : MonoBehaviour
    {
        [Tooltip("The player Health this regen heals. Serialized editor-time; Awake grabs a sibling Health.")]
        public Health health;

        [Header("Needs the regen reads (never writes) — AC3. Any null need simply doesn't gate.")]
        [Tooltip("Warmth need (standalone WarmthNeed). Wired editor-time; null → warmth doesn't gate.")]
        public WarmthNeed warmth;
        [Tooltip("Hunger need. Wired editor-time; null → hunger doesn't gate.")]
        public HungerNeed hunger;
        [Tooltip("Thirst need. Wired editor-time; null → thirst doesn't gate.")]
        public ThirstNeed thirst;

        [Header("Regen tuning (Sponsor soak-tunable)")]
        [Tooltip("HP restored per second while ALL needs are satisfied + none critical (AC3). Slow — regen " +
                 "is a background recovery, not a heal source.")]
        public float regenPerSecond = 2f;

        [Tooltip("A need at/below this fraction (0..1) does NOT satisfy the regen gate — regen only runs " +
                 "while every need is ABOVE this. Above the needs' own criticalThreshold so regen stalls " +
                 "BEFORE a need goes fully critical (recovery needs headroom).")]
        [Range(0f, 1f)]
        public float needThreshold01 = 0.4f;

        [Tooltip("DEFAULT policy (AC3): a CRITICAL need STALLS regen (regenPerSecond → 0). When ON, a " +
                 "critical need instead SLOW-DRAINS HP at criticalDrainPerSecond (the Sponsor-soak-tunable " +
                 "alternative). Default OFF = stall.")]
        public bool criticalSlowDrains = false;

        [Tooltip("HP lost per second while a need is CRITICAL and criticalSlowDrains is ON (the soak-tunable " +
                 "slow-drain alternative to a plain stall). Ignored when criticalSlowDrains is OFF.")]
        public float criticalDrainPerSecond = 1f;

        private float _lastTickTime;
        private bool _started;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
        }

        private void Start()
        {
            _lastTickTime = Time.time;
            _started = true;
        }

        private void Update()
        {
            if (!_started) return;
            float now = Time.time;
            float dt = now - _lastTickTime;
            _lastTickTime = now;
            if (dt > 0f) TickSeconds(dt);
        }

        /// <summary>
        /// Deterministically advance regen over <paramref name="seconds"/> (AC3). Public so EditMode drives
        /// it without a scene/Update/wall-clock (headless deltaTime≈0). When the gate is satisfied
        /// (<see cref="ShouldRegen"/>) heals regenPerSecond × seconds; when a need is critical + slow-drain
        /// is ON, DAMAGES criticalDrainPerSecond × seconds through the shared seam; otherwise STALLS (no-op).
        /// A no-op on a null/dead Health.
        /// </summary>
        public void TickSeconds(float seconds)
        {
            if (seconds <= 0f || health == null || health.IsDead) return;

            bool anyCritical = IsAnyCritical();
            bool gate = ShouldRegen(
                NeedValue(warmth), NeedCritical(warmth),
                NeedValue(hunger), NeedCritical(hunger),
                NeedValue(thirst), NeedCritical(thirst),
                needThreshold01);

            if (gate)
            {
                health.Heal(regenPerSecond * seconds);
            }
            else if (anyCritical && criticalSlowDrains)
            {
                // The Sponsor-soak-tunable alternative to a plain stall: a critical need slowly ACHES HP down.
                health.ApplyDamage(criticalDrainPerSecond * seconds, DamageType.Blunt);
            }
            // else: STALL (default) — a need below threshold pauses regen; nothing happens this tick.
        }

        /// <summary>
        /// PURE needs-gate decision (AC3 — the unit-testable truth-table). Regen runs ONLY when ALL three
        /// needs are ABOVE <paramref name="threshold01"/> AND none is critical. A need passed as "absent"
        /// (a NaN value + not-critical) does NOT gate — so a bare rig with one need still regenerates on
        /// that need alone. A critical need (isCritical true) always FAILS the gate (stalls). Static +
        /// dependency-free so the EditMode test asserts the whole table (all satisfied → true; any below
        /// threshold OR any critical → false) driving need state via the SurvivalNeed surface.
        /// </summary>
        public static bool ShouldRegen(
            float warmth01, bool warmthCritical,
            float hunger01, bool hungerCritical,
            float thirst01, bool thirstCritical,
            float threshold01)
        {
            return NeedSatisfies(warmth01, warmthCritical, threshold01)
                && NeedSatisfies(hunger01, hungerCritical, threshold01)
                && NeedSatisfies(thirst01, thirstCritical, threshold01);
        }

        // A single need satisfies the gate when it is NOT critical AND strictly ABOVE the threshold. An
        // "absent" need (NaN value) is treated as satisfied (a null need doesn't gate — a bare rig).
        private static bool NeedSatisfies(float value01, bool critical, float threshold01)
        {
            if (float.IsNaN(value01)) return true; // absent need — does not gate
            if (critical) return false;            // a critical need always stalls regen (AC3)
            return value01 > threshold01;
        }

        // Read a need's Current01 (NaN if the need is null/absent — the gate treats NaN as "doesn't gate").
        // WarmthNeed IS-A SurvivalNeed (refactored onto the base by 86cabgvgw), so ONE overload binds all
        // three through the shared base surface (Current01 / IsCritical) — no per-type overload needed.
        private static float NeedValue(SurvivalNeed n) => n != null ? n.Current01 : float.NaN;
        private static bool NeedCritical(SurvivalNeed n) => n != null && n.IsCritical;

        private bool IsAnyCritical()
            => (warmth != null && warmth.IsCritical)
            || (hunger != null && hunger.IsCritical)
            || (thirst != null && thirst.IsCritical);
    }
}
