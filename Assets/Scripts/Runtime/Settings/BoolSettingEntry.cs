using System;
using UnityEngine;

namespace FarHorizon.Settings
{
    /// <summary>
    /// ARCHETYPE D — a BOOL bound to a live gameplay flag via a getter/setter pair, rendered as an on/off
    /// Toggle switch (ticket 86cabeqj9 AC7). Added so the per-need on/off (ticket b — 86cabeqwf) and any
    /// future feature FLAG register in "a few lines" exactly like the float/range/int archetypes:
    /// <code>registry.AddBool("hunger_enabled", "Hunger on", () =&gt; hunger.enabled, v =&gt; hunger.enabled = v);</code>
    ///
    /// Same single-write-authority + PlayerPrefs-persist + extension-hook discipline as the other entries
    /// (the setter applies the live effect AND persists in one step; an unavailable hook is inert). Pure C#
    /// so it is fully EditMode-testable (AC11(iv)).
    ///
    /// GENERIC TYPE/NUDGE (AC5/AC6): the console adds a typed-field + nudge keys GENERICALLY across every
    /// archetype. A bool has no slider band, so it participates via <see cref="SetFromNumeric"/> /
    /// <see cref="NumericValue"/> (false=0, true=1): typing a number ≥ 0.5 or nudging up sets it true,
    /// 0 / nudging down sets it false — so the same type/nudge code path drives a bool with no special case.
    /// </summary>
    public sealed class BoolSettingEntry : SettingEntry
    {
        private readonly Func<bool> _get;
        private readonly Action<bool> _set;
        private readonly bool _default;

        public override Archetype Kind => Archetype.Toggle;

        /// <param name="get">Reads the live flag (e.g. () =&gt; hunger.enabled).</param>
        /// <param name="set">Writes the live flag (e.g. v =&gt; hunger.enabled = v) — drives the game immediately.</param>
        /// <param name="available">false = a not-yet-built extension hook (AC3), greyed + "(soon)"; setter inert.</param>
        public BoolSettingEntry(string id, string label, Func<bool> get, Action<bool> set,
            bool available = true, string unit = "")
            : base(id, label, available, unit)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            // Capture the flag's value at registration time as the bake-able default (AC9/AC10).
            _default = _get();
        }

        /// <summary>The live flag value.</summary>
        public bool Value => _get();

        /// <summary>The captured default (the flag value at registration — the current shipped default).</summary>
        public bool Default => _default;

        /// <summary>
        /// Set the live flag (AC2 — drives the game immediately), then persist to PlayerPrefs as 0/1 (AC5).
        /// The SINGLE write authority. Inert (returns the current value) for an unavailable extension hook.
        /// </summary>
        public bool SetValue(bool v)
        {
            if (!Available) return Value;
            _set(v);
            PlayerPrefs.SetInt(PrefsKey, v ? 1 : 0);
            return v;
        }

        /// <summary>Flip the flag (the toggle / nudge action).</summary>
        public bool Toggle() => SetValue(!Value);

        // ----- Generic type/nudge bridge (AC5/AC6): a bool reads/writes as 0 / 1 -----

        /// <summary>The flag as a number for the generic typed-field/nudge path (false=0, true=1).</summary>
        public float NumericValue => Value ? 1f : 0f;

        /// <summary>Set from the generic typed-field/nudge path: ≥ 0.5 → true, else false (single authority).</summary>
        public bool SetFromNumeric(float v) => SetValue(v >= 0.5f);

        public override void Apply()
        {
            if (!Available) return;
            _set(Value); // re-assert the live value
        }

        public override void LoadFromPrefs()
        {
            if (!Available) return;
            if (PlayerPrefs.HasKey(PrefsKey)) SetValue(PlayerPrefs.GetInt(PrefsKey) != 0);
        }

        public override void ResetToDefault()
        {
            if (!Available) return;
            SetValue(_default);
        }

        /// <summary>True when the live flag has been toggled off its registration-time default (AC9 badge).
        /// An unavailable hook never drives a flag, so it can never differ (no false badge on a greyed row).</summary>
        public override bool DiffersFromDefault => Available && Value != _default;
    }
}
