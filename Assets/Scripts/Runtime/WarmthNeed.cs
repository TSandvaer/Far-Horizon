using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The single survival need for the M-U2 thin loop (ticket 86ca8bd9m, Sponsor-locked WARMTH).
    ///
    /// Castaway-washed-ashore fiction: wet and cold, warmth ticks DOWN over time and creates the
    /// *why* of the loop — the campfire (U2-4) is what answers it via the satisfaction hook
    /// (<see cref="AddWarmth"/> / <see cref="SatisfyFull"/>). This is the loop SPINE: U2-2 (axe) and
    /// the rest of M-U2 build on this need existing and reading clearly.
    ///
    /// Thin per Sponsor decision 4/6: ONE need, no second need, no shelter, no death-spiral — warmth
    /// decays, reads clearly, and can be satisfied. Below <see cref="floor01"/> it stops decaying (a
    /// simple floor, NOT a fail-state — death/fail design is explicitly out of scope this ticket).
    ///
    /// === Public data surface (U2-5 HUD — Uma/Devon — consumes EXACTLY this) ===
    ///   float  Current01   : warmth normalized to 0..1 (the HUD bar fill). Always clamped.
    ///   float  Current     : raw warmth in the configured 0..<see cref="max"/> range.
    ///   float  Max         : the configured maximum (bar's full value).
    ///   bool   IsCritical  : Current01 <= criticalThreshold01 (HUD may flash/warn).
    ///   event Action&lt;float&gt; Changed : fires with Current01 whenever warmth changes
    ///                        (decay tick OR satisfaction). The HUD subscribes and never polls.
    /// The HUD should treat this component as READ-ONLY (subscribe to Changed, read Current01);
    /// only the campfire (U2-4) writes, via AddWarmth/SatisfyFull. Keep this surface stable.
    ///
    /// === Time model (unity-conventions.md §headless time) ===
    /// Decay is integrated over REAL elapsed time (Time.time deltas accumulated in Update), NOT a
    /// per-frame Time.deltaTime assumption — headless PlayMode runs see Time.deltaTime ~= 0 per frame,
    /// so a deltaTime-based decay would never tick in CI. Sampling the Time.time window makes the
    /// decay real both in the shipped exe and in headless tests. The model is also driveable
    /// deterministically for EditMode via <see cref="TickSeconds"/> (no scene/Update needed).
    /// </summary>
    public class WarmthNeed : MonoBehaviour
    {
        [Header("Range")]
        [Tooltip("Maximum warmth (the HUD bar's full value). Warmth starts here unless startFull is off.")]
        public float max = 100f;

        [Tooltip("Start warmth at max on Start(). The castaway begins merely chilled, not freezing.")]
        public bool startFull = true;

        [Header("Decay")]
        [Tooltip(
            "Warmth lost per second. Tuned so a FULL bar drains in ~max/decayPerSecond seconds " +
            "(100 / 0.55 ~= 182s ~= 3 min) — comfortably longer than one craft->chop->campfire " +
            "loop cycle, so the need is felt as pressure without punishing a normal-paced loop " +
            "(ticket 86ca8bd9m scope: 'one loop cycle fits comfortably inside the decay window').")]
        public float decayPerSecond = 0.55f;

        [Tooltip(
            "Decay stops at this fraction of max (a simple floor, NOT a fail-state — death/fail is " +
            "out of scope this ticket). The castaway gets cold and stays cold; the campfire restores.")]
        [Range(0f, 1f)]
        public float floor01 = 0.05f;

        [Header("Readout")]
        [Tooltip("At/below this fraction the need reads as critical (HUD may warn). Cosmetic this ticket.")]
        [Range(0f, 1f)]
        public float criticalThreshold01 = 0.25f;

        // Raw current warmth (0..max). Backing field; mutated only through SetCurrent so Changed fires.
        private float _current;

        // Accumulates real Time.time between Updates so decay integrates over the wall-clock window
        // rather than a (headless-zero) per-frame deltaTime. Seeded in Start().
        private float _lastTickTime;
        private bool _started;

        /// <summary>Fires with Current01 (0..1) whenever warmth changes — decay OR satisfaction.
        /// U2-5's HUD subscribes to this and never polls.</summary>
        public event Action<float> Changed;

        /// <summary>Warmth normalized to 0..1 — the HUD bar fill. Always clamped.</summary>
        public float Current01 => max <= 0f ? 0f : Mathf.Clamp01(_current / max);

        /// <summary>Raw current warmth in the 0..max range.</summary>
        public float Current => _current;

        /// <summary>The configured maximum (the bar's full value).</summary>
        public float Max => max;

        /// <summary>True when warmth is at/below the critical readout threshold.</summary>
        public bool IsCritical => Current01 <= criticalThreshold01;

        void Start()
        {
            _current = startFull ? max : _current;
            _lastTickTime = Time.time;
            _started = true;
            // Announce the initial value so a HUD that subscribed before Start() paints immediately.
            Changed?.Invoke(Current01);
        }

        void Update()
        {
            if (!_started) return;
            float now = Time.time;
            float dt = now - _lastTickTime;
            _lastTickTime = now;
            if (dt > 0f) TickSeconds(dt);
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
        /// Satisfaction hook the campfire (U2-4) calls. Adds warmth (clamped to max), returns the new
        /// Current01. This is THE seam U2-4 wires the campfire to — keep the signature stable.
        /// </summary>
        public float AddWarmth(float amount)
        {
            if (amount != 0f) SetCurrent(_current + amount);
            return Current01;
        }

        /// <summary>Restore warmth fully (a campfire that fully warms the castaway). Convenience over
        /// AddWarmth(max); returns the new Current01 (== 1).</summary>
        public float SatisfyFull()
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
