using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// Applies active <see cref="StatusEffectSpec"/>s to a <see cref="Health"/> (Combat POC 86cah7xxp,
    /// AC6). The general data-driven framework the POC ships — DoT-capable, ships BLEED only. Lives on the
    /// SAME GameObject as the Health it damages (player OR enemy — bleed works BOTH ways, AC7). A weapon's
    /// onHitStatus (AC4) is <see cref="Apply"/>'d to the struck target's controller; an enemy bite can
    /// Apply a bleed to the player's controller.
    ///
    /// === Time model (AC3/AC6 — mirror WarmthNeed §Time; headless-safe) ===
    /// Each active effect ticks damage over REAL <see cref="Time.time"/> deltas accumulated in Update — NOT
    /// per-frame Time.deltaTime (headless PlayMode sees deltaTime≈0, so a deltaTime DoT would silently never
    /// tick in CI or a build). <see cref="TickSeconds"/> is the deterministic EditMode-driveable hook (no
    /// scene/Update needed) — the same shape SurvivalNeed exposes. An effect EXPIRES when its accumulated
    /// active time reaches its duration; the DoT routes through the shared <see cref="Health.ApplyDamage"/>
    /// seam (AC1/AC6) so a bleed is subject to the target's resistance + tier multiplier exactly like a hit.
    ///
    /// NO MUTABLE STATICS (instance state only) — StaticStateResetTests needs no reset here.
    /// </summary>
    public sealed class StatusEffectController : MonoBehaviour
    {
        [Tooltip("The Health this controller damages via active status effects. Serialized editor-time; an " +
                 "Awake fallback grabs a Health on the same GameObject (the build-safety net).")]
        public Health health;

        // An in-flight effect: the spec + how much of its duration has elapsed.
        private struct Active
        {
            public StatusEffectSpec Spec;
            public float Elapsed;
        }

        private readonly List<Active> _active = new List<Active>();
        private float _lastTickTime;
        private bool _started;

        /// <summary>How many status effects are currently active (a bleed adds one; it drops off on expiry).
        /// Exposed so a test asserts a bleed is applied, ticks, then expires.</summary>
        public int ActiveCount => _active.Count;

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
        /// Apply a status effect (AC6) — a weapon's onHitStatus, or an enemy bite's bleed. Ignores an
        /// inactive spec (<see cref="StatusEffectSpec.None"/> / zeroed) so a weapon with no on-hit status
        /// adds nothing. Bleed does NOT stack refresh-to-full in the POC: each Apply adds a fresh instance
        /// (the framework is list-based so a later ticket can add stack/refresh policy without a reshape).
        /// A no-op on a null/dead Health.
        /// </summary>
        public void Apply(StatusEffectSpec spec)
        {
            if (!spec.IsActive) return;
            if (health == null || health.IsDead) return;
            _active.Add(new Active { Spec = spec, Elapsed = 0f });
        }

        /// <summary>
        /// Deterministically tick every active effect over <paramref name="seconds"/> (AC6 — the DoT). Public
        /// so EditMode drives it without a scene/Update/wall-clock (headless deltaTime≈0). Each Bleed removes
        /// damagePerSecond × seconds through <see cref="Health.ApplyDamage"/> (so resistance + tier apply),
        /// clamps the tick to the effect's REMAINING duration (an over-long tick can't over-damage past
        /// expiry), and DROPS the effect when its duration is spent. Stops applying once the Health is dead.
        /// </summary>
        public void TickSeconds(float seconds)
        {
            if (seconds <= 0f || _active.Count == 0 || health == null) return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Active a = _active[i];
                float remaining = a.Spec.durationSeconds - a.Elapsed;
                if (remaining <= 0f) { _active.RemoveAt(i); continue; }

                // Tick only the portion of `seconds` this effect is still alive for (so a big tick that
                // overshoots the duration deals exactly the remaining bleed, not the full tick's worth).
                float slice = Mathf.Min(seconds, remaining);
                if (!health.IsDead && a.Spec.kind == StatusEffectKind.Bleed)
                    health.ApplyDamage(a.Spec.damagePerSecond * slice, a.Spec.damageType);

                a.Elapsed += slice;
                if (a.Elapsed >= a.Spec.durationSeconds || health.IsDead)
                    _active.RemoveAt(i); // expired (or the target died — drop the DoT)
                else
                    _active[i] = a;
            }
        }
    }
}
