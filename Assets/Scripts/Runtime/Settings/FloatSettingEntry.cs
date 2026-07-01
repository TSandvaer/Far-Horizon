using System;
using UnityEngine;

namespace FarHorizon.Settings
{
    /// <summary>
    /// ARCHETYPE A (Uma §2.2 .setting-row--slider) — a single float bound to a live gameplay param via a
    /// getter/setter pair, rendered as a slider + a fixed-width numeric readout. Used by walk speed (AC3,
    /// live) and — as named extension hooks (AC3, Available=false) — run speed / jump height / tool-use
    /// speed until those features land.
    ///
    /// THE BIND CONTRACT: the entry never stores the value itself — it READS the live param through
    /// <see cref="_get"/> and WRITES it through <see cref="_set"/>, so the entry and the game can never
    /// disagree (changing the slider drives the param immediately, AC2). The setter is the single
    /// authority: <see cref="SetValue"/> clamps to [Min, Max], writes the param, then persists to
    /// PlayerPrefs (AC5) — one path, no split write.
    /// </summary>
    public sealed class FloatSettingEntry : SettingEntry
    {
        private readonly Func<float> _get;
        private readonly Action<float> _set;
        private readonly float _default;

        /// <summary>Slider lower bound (the smallest value the Sponsor can dial).</summary>
        public float Min { get; }

        /// <summary>Slider upper bound (the largest value the Sponsor can dial).</summary>
        public float Max { get; }

        public override Archetype Kind => Archetype.Slider;

        /// <param name="get">Reads the live param (e.g. () =&gt; wasd.moveSpeed).</param>
        /// <param name="set">Writes the live param (e.g. v =&gt; wasd.moveSpeed = v) — drives the game immediately.</param>
        /// <param name="min">Slider floor.</param>
        /// <param name="max">Slider ceiling.</param>
        /// <param name="available">false = a not-yet-built extension hook (AC3), greyed + "(soon)"; setter inert.</param>
        public FloatSettingEntry(string id, string label, Func<float> get, Action<float> set,
            float min, float max, bool available = true, string unit = "")
            : base(id, label, available, unit)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            Min = Mathf.Min(min, max);
            Max = Mathf.Max(min, max);
            // Capture the param's value at registration time as the bake-able default (AC5 reset).
            _default = ClampValue(_get());
        }

        /// <summary>The live param value, clamped into [Min, Max] for display.</summary>
        public float Value => ClampValue(_get());

        /// <summary>The captured default (the value at registration — the current shipped default).</summary>
        public float Default => _default;

        /// <summary>
        /// Set the live param (AC2 — drives the game immediately): clamp to [Min, Max], write through the
        /// bind setter, then persist to PlayerPrefs (AC5). The SINGLE write authority. Inert (returns the
        /// current value) for an unavailable extension hook — we never drive a param that doesn't exist.
        /// </summary>
        public float SetValue(float v)
        {
            if (!Available) return Value;
            float clamped = ClampValue(v);
            _set(clamped);
            PlayerPrefs.SetFloat(PrefsKey, clamped);
            return clamped;
        }

        public override void Apply()
        {
            if (!Available) return;
            _set(Value); // re-assert the (clamped) live value
        }

        public override void LoadFromPrefs()
        {
            if (!Available) return;
            if (PlayerPrefs.HasKey(PrefsKey)) SetValue(PlayerPrefs.GetFloat(PrefsKey));
        }

        public override void ResetToDefault()
        {
            if (!Available) return;
            SetValue(_default);
        }

        /// <summary>True when the live value has been dialed off its registration-time default (AC9 badge).
        /// An unavailable hook never drives a param, so it can never differ (no false badge on a greyed row).</summary>
        public override bool DiffersFromDefault => Available && !Mathf.Approximately(Value, _default);

        private float ClampValue(float v) => Mathf.Clamp(v, Min, Max);
    }
}
