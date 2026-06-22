using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The SHARED abstract base for every M-U2 survival need (ticket 86caamkp8 owns this base —
    /// Pattern A: hunger lands FIRST of the hunger/thirst pair, so it defines the base; thirst
    /// 86caamkv7 EXTENDS this merged type rather than re-declaring a base).
    ///
    /// Generalizes the surface proven by <see cref="WarmthNeed"/> (Sponsor-locked WARMTH, ticket
    /// 86ca8bd9m) so the U2 needs share ONE decay/floor/satisfy model and the need-meter HUD
    /// (86caamkxv) binds all three needs with a SINGLE code path (it reads only this base surface).
    ///
    /// === Public data surface (the HUD CONTRACT — binds EXACTLY this; byte-identical to WarmthNeed) ===
    ///   float  Current01   : the need normalized to 0..1 (the HUD bar fill). Always clamped.
    ///   float  Current     : raw value in the configured 0..<see cref="max"/> range.
    ///   float  Max         : the configured maximum (the bar's full value).
    ///   bool   IsCritical  : Current01 &lt;= criticalThreshold01 (HUD may flash/warn).
    ///   event Action&lt;float&gt; Changed : fires with Current01 whenever the value changes
    ///                        (decay tick OR satisfaction). The HUD subscribes and never polls.
    /// The HUD treats a need as READ-ONLY (subscribe to Changed, read Current01); only gameplay
    /// writes, via a typed satisfaction hook the subclass exposes (e.g. HungerNeed.AddFood / the
    /// campfire's AddWarmth) which routes through the protected <see cref="Satisfy"/> primitive here.
    /// Keep this surface STABLE — divergence between this base and a sibling need's usage is
    /// mergeability-blocking (parallel-shared-concept naming discipline), NOT a NIT.
    ///
    /// === Difficulty tiering (difficulty directive — easy/med/hard rates) ===
    /// The decay rate is NOT a single hardcoded constant: <see cref="ApplyDifficulty"/> sets
    /// <see cref="decayPerSecond"/> from the per-tier <see cref="easyDecayPerSecond"/> /
    /// <see cref="medDecayPerSecond"/> / <see cref="hardDecayPerSecond"/> fields (the settings panel
    /// 86caa4bqp drives the live tier), so the same need reads as gentle for kids or punishing for
    /// adults without re-tuning code. The active <see cref="decayPerSecond"/> stays the single field
    /// every test + the decay path reads, so existing test shape is unchanged.
    ///
    /// === Time model (unity-conventions.md §headless time) ===
    /// Decay integrates over REAL elapsed time (Time.time deltas accumulated in Update), NOT a
    /// per-frame Time.deltaTime assumption — headless PlayMode sees Time.deltaTime ~= 0 per frame,
    /// so a deltaTime-based decay would silently never tick in CI or the build. Sampling the Time.time
    /// window makes the decay real both in the shipped exe and in headless tests. The model is also
    /// driveable deterministically for EditMode via <see cref="TickSeconds"/> (no scene/Update needed).
    /// </summary>
    public abstract class SurvivalNeed : MonoBehaviour
    {
        [Header("Range")]
        [Tooltip("Maximum value (the HUD bar's full value). The need starts here unless startFull is off.")]
        public float max = 100f;

        [Tooltip("Start at max on Start(). The castaway begins merely pressured, not at the floor.")]
        public bool startFull = true;

        [Tooltip(
            "When startFull is OFF, seed the need at this fraction of max on Start() (0..1). Default 0 " +
            "(preserves the historic startFull=false -> empty-at-start contract the tests rely on). A need " +
            "that should START PRESSURED-BUT-WITH-HEADROOM (e.g. hunger, so eating a berry VISIBLY refills " +
            "the bar instead of clamping against an already-full max) sets startFull=false + a mid value " +
            "like 0.55. Ignored entirely when startFull is ON.")]
        [Range(0f, 1f)]
        public float startFraction01 = 0f;

        [Header("Decay")]
        [Tooltip(
            "ACTIVE decay lost per second. This is the single field the decay path + tests read; the " +
            "difficulty tier (easy/med/hard) writes it via ApplyDifficulty. A FULL bar drains in " +
            "~max/decayPerSecond seconds.")]
        public float decayPerSecond = 0.55f;

        [Tooltip(
            "Decay stops at this fraction of max (a simple floor, NOT a fail-state — death/fail is " +
            "out of scope). The castaway gets pressured and stays pressured; a satisfaction hook restores.")]
        [Range(0f, 1f)]
        public float floor01 = 0.05f;

        [Header("Readout")]
        [Tooltip("At/below this fraction the need reads as critical (HUD may warn).")]
        [Range(0f, 1f)]
        public float criticalThreshold01 = 0.25f;

        [Header("Difficulty tiers (easy / med / hard decay rates — difficulty directive)")]
        [Tooltip("Decay/sec on EASY (gentle for kids). ApplyDifficulty(Easy) copies this into decayPerSecond.")]
        public float easyDecayPerSecond = 0.30f;
        [Tooltip("Decay/sec on MEDIUM (the default tier). ApplyDifficulty(Medium) copies this into decayPerSecond.")]
        public float medDecayPerSecond = 0.55f;
        [Tooltip("Decay/sec on HARD (punishing for adults). ApplyDifficulty(Hard) copies this into decayPerSecond.")]
        public float hardDecayPerSecond = 0.90f;

        // Raw current value (0..max). Backing field; mutated only through SetCurrent so Changed fires.
        private float _current;

        // Accumulates real Time.time between Updates so decay integrates over the wall-clock window
        // rather than a (headless-zero) per-frame deltaTime. Seeded in Start().
        private float _lastTickTime;
        private bool _started;

        /// <summary>The difficulty tier — drives which per-tier decay rate is active. The settings
        /// panel (86caa4bqp) sets it; mirrors the gameplay-wave difficulty selector vocabulary.</summary>
        public enum DifficultyTier { Easy, Medium, Hard }

        /// <summary>Fires with Current01 (0..1) whenever the value changes — decay OR satisfaction.
        /// The need-meter HUD (86caamkxv) subscribes to this and never polls.</summary>
        public event Action<float> Changed;

        /// <summary>The value normalized to 0..1 — the HUD bar fill. Always clamped.</summary>
        public float Current01 => max <= 0f ? 0f : Mathf.Clamp01(_current / max);

        /// <summary>Raw current value in the 0..max range.</summary>
        public float Current => _current;

        /// <summary>The configured maximum (the bar's full value).</summary>
        public float Max => max;

        /// <summary>True when the value is at/below the critical readout threshold.</summary>
        public bool IsCritical => Current01 <= criticalThreshold01;

        protected virtual void Start()
        {
            // startFull -> seed at max. Otherwise seed at startFraction01*max (default 0 preserves the
            // historic "startFull=false -> empty" contract; a pressured-with-headroom need like hunger
            // sets a mid fraction so a satisfaction hook VISIBLY refills the bar — #101 eat-refill fix).
            _current = startFull ? max : Mathf.Clamp01(startFraction01) * max;
            _lastTickTime = Time.time;
            _started = true;
            // Announce the initial value so a HUD that subscribed before Start() paints immediately.
            Changed?.Invoke(Current01);
        }

        protected virtual void Update()
        {
            if (!_started) return;
            float now = Time.time;
            float dt = now - _lastTickTime;
            _lastTickTime = now;
            if (dt > 0f) TickSeconds(dt);
        }

        /// <summary>
        /// Set the ACTIVE <see cref="decayPerSecond"/> from the per-tier rate (difficulty directive).
        /// The settings panel / difficulty selector calls this; the decay path always reads the single
        /// active field, so a live tier change takes effect immediately with no restart.
        /// </summary>
        public void ApplyDifficulty(DifficultyTier tier)
        {
            switch (tier)
            {
                case DifficultyTier.Easy: decayPerSecond = easyDecayPerSecond; break;
                case DifficultyTier.Hard: decayPerSecond = hardDecayPerSecond; break;
                default: decayPerSecond = medDecayPerSecond; break;
            }
        }

        /// <summary>
        /// Integrate decay over <paramref name="seconds"/> of elapsed time. Public + deterministic so
        /// EditMode tests can drive decay without a scene/Update/wall-clock (headless Time.deltaTime~=0).
        /// Clamps at the floor (decay never pushes below floor01*max).
        /// </summary>
        public void TickSeconds(float seconds)
        {
            if (seconds <= 0f) return;
            float floor = floor01 * max;
            if (_current <= floor) return; // already at/below the floor — decay rests here
            // Clamp the decayed value to the floor: a single large tick (or a long Update gap) must
            // not overshoot past the floor down to 0 — the floor is the resting point, not 0.
            SetCurrent(Mathf.Max(floor, _current - decayPerSecond * seconds));
        }

        /// <summary>
        /// The protected satisfaction PRIMITIVE every typed hook routes through (HungerNeed.AddFood,
        /// the campfire's AddWarmth, thirst's drink). Adds <paramref name="amount"/> (clamped to max),
        /// returns the new Current01. Keeps the single write path so Changed fires consistently.
        /// </summary>
        protected float Satisfy(float amount)
        {
            if (amount != 0f) SetCurrent(_current + amount);
            return Current01;
        }

        /// <summary>Restore fully (a satisfaction that completely answers the need). Returns the new
        /// Current01 (== 1). Protected — a subclass exposes a typed convenience if it wants one.</summary>
        protected float SatisfyFull()
        {
            SetCurrent(max);
            return Current01;
        }

        // Single write path: clamp to [0, max] and fire Changed only on an actual value change.
        private void SetCurrent(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, max);
            if (Mathf.Approximately(clamped, _current)) return;
            _current = clamped;
            Changed?.Invoke(Current01);
        }
    }
}
