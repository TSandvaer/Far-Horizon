using System;
using UnityEngine;

namespace FarHorizon.Settings
{
    /// <summary>
    /// ARCHETYPE C (Uma §2.2 .setting-row--stepper) — an int bound to a live param via a getter/setter,
    /// rendered as a [ − ] value [ + ] stepper. NOTHING wires one this ticket (no int gameplay param
    /// exists yet) — but the type EXISTS now so the DOWNSTREAM tickets (belt-slot count, inventory-slot
    /// count, stack size — 86caa4bya) slot their settings in with a few lines and zero new UI work. That
    /// is the extensible-registry contract proven end to end across all three archetypes (AC2).
    ///
    /// Same single-write-authority + clamp + PlayerPrefs-persist + extension-hook discipline as the float
    /// and range entries; pure C# so it's fully EditMode-testable (AC6 covers it with a sample int param).
    /// </summary>
    public sealed class IntSettingEntry : SettingEntry
    {
        private readonly Func<int> _get;
        private readonly Action<int> _set;
        private readonly int _default;

        /// <summary>Smallest value the stepper can reach.</summary>
        public int Min { get; }

        /// <summary>Largest value the stepper can reach.</summary>
        public int Max { get; }

        /// <summary>How much one [ − ] / [ + ] press changes the value (defaults to 1).</summary>
        public int Step { get; }

        public override Archetype Kind => Archetype.Stepper;

        public IntSettingEntry(string id, string label, Func<int> get, Action<int> set,
            int min, int max, int step = 1, bool available = true, string unit = "")
            : base(id, label, available, unit)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            Min = Mathf.Min(min, max);
            Max = Mathf.Max(min, max);
            Step = Mathf.Max(1, step);
            _default = ClampValue(_get());
        }

        public int Value => ClampValue(_get());
        public int Default => _default;

        /// <summary>Set the live int (clamp to [Min, Max], write, persist) — the single write authority.</summary>
        public int SetValue(int v)
        {
            if (!Available) return Value;
            int clamped = ClampValue(v);
            _set(clamped);
            PlayerPrefs.SetInt(PrefsKey, clamped);
            return clamped;
        }

        /// <summary>One [ + ] press: increase by <see cref="Step"/> (clamped).</summary>
        public int Increment() => SetValue(Value + Step);

        /// <summary>One [ − ] press: decrease by <see cref="Step"/> (clamped).</summary>
        public int Decrement() => SetValue(Value - Step);

        public override void Apply()
        {
            if (!Available) return;
            _set(Value);
        }

        public override void LoadFromPrefs()
        {
            if (!Available) return;
            if (PlayerPrefs.HasKey(PrefsKey)) SetValue(PlayerPrefs.GetInt(PrefsKey));
        }

        public override void ResetToDefault()
        {
            if (!Available) return;
            SetValue(_default);
        }

        private int ClampValue(int v) => Mathf.Clamp(v, Min, Max);
    }
}
