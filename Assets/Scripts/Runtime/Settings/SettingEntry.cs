using System;
using UnityEngine;

namespace FarHorizon.Settings
{
    /// <summary>
    /// One tweakable setting in the in-game soak-tuning panel (ticket 86caa4bqp).
    ///
    /// THE EXTENSIBLE-REGISTRY CONTRACT (AC2 — the headline AC). Every setting the panel can show is a
    /// <see cref="SettingEntry"/>: a named, typed entry BOUND to a live gameplay param via plain
    /// getter/setter delegates. Changing the value drives the game IMMEDIATELY (no restart) so the
    /// Sponsor soak-tunes; the chosen values are then BAKED as new defaults (the give-him-the-knob
    /// pattern, cf. the F9 axe-nudge tool). Registering a FUTURE setting is a few lines — pick the typed
    /// subclass (<see cref="FloatSettingEntry"/> / <see cref="RangeSettingEntry"/> /
    /// <see cref="IntSettingEntry"/>), give it an id + label + the bind delegates, and add it to the
    /// <see cref="SettingsRegistry"/>. NO new UI code, NO panel rebuild (the panel renders any entry
    /// generically off the archetype <see cref="Kind"/>).
    ///
    /// PURE C# (no UnityEngine.Object base) so the whole registry + binding + clamp contract is fully
    /// unit-testable in EditMode — where the UIDocument render loop is not reliable and Time.deltaTime≈0
    /// (the headless-time trap, unity-conventions.md). The UI Toolkit panel (<see cref="SettingsPanel"/>)
    /// is a thin VIEW driven from these entries; AC6 tests the entries directly with no scene.
    ///
    /// PERSISTENCE (AC5): each entry persists its value via PlayerPrefs under a stable key derived from
    /// <see cref="Id"/>, so soak tweaks survive a relaunch. The bind setter is the single authority — it
    /// applies the live effect AND the entry writes PlayerPrefs in the same step (no split write path).
    ///
    /// AVAILABILITY (AC3): an entry whose underlying feature does not exist yet (run-speed / jump-height /
    /// tool-use-speed) registers with <see cref="Available"/> = false — a clearly-named EXTENSION HOOK
    /// that the panel renders GREYED with a "(soon)" tag (Uma §2.2 .setting-row--disabled). We never fake
    /// a param that doesn't exist; the hook just reserves the row so the downstream ticket fills it in.
    /// </summary>
    public abstract class SettingEntry
    {
        /// <summary>The row archetypes (Uma §2.2). The panel renders an entry generically off this.</summary>
        public enum Archetype
        {
            /// <summary>Single float → a slider + a live numeric readout (walk/run/jump/tool-use speed).</summary>
            Slider,
            /// <summary>Min–max pair → a dual-thumb range + two readouts, clamps the live system (zoom/pitch range).</summary>
            Range,
            /// <summary>Int → a [−] value [+] stepper (belt/inventory slots, stack size — downstream tickets).</summary>
            Stepper,
            /// <summary>Bool → an on/off toggle switch (per-need on/off + future flags — 86cabeqj9 AC7). The
            /// generic typed-field + nudge affordances treat it as 0/1 so every archetype gets type/nudge.</summary>
            Toggle,
        }

        /// <summary>Stable id (used for the PlayerPrefs key + test lookup). e.g. "walk_speed", "zoom_range".</summary>
        public string Id { get; }

        /// <summary>Human label shown left of the control (e.g. "Walk speed", "Zoom range").</summary>
        public string Label { get; }

        /// <summary>The row archetype the panel renders this entry as.</summary>
        public abstract Archetype Kind { get; }

        /// <summary>
        /// Whether the underlying feature EXISTS yet (AC3). false = a named extension hook for a not-yet-built
        /// feature (run/jump/chop tool-use): the panel greys the row + tags it "(soon)" and never drives a
        /// param. true = a live, tweakable setting. We never bind a param that doesn't exist — the hook just
        /// reserves the row so the downstream ticket wires it in with a few lines.
        /// </summary>
        public bool Available { get; }

        /// <summary>Optional unit suffix for the readout (e.g. "u/s", "°"). Empty when none.</summary>
        public string Unit { get; }

        protected SettingEntry(string id, string label, bool available, string unit)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("SettingEntry id must be non-empty", nameof(id));
            Id = id;
            Label = label ?? id;
            Available = available;
            Unit = unit ?? string.Empty;
        }

        /// <summary>The PlayerPrefs key this entry persists under (AC5). Namespaced so it never collides.</summary>
        public string PrefsKey => "fh.settings." + Id;

        /// <summary>
        /// Push the entry's CURRENT live value back through its bind setter (re-applies the live effect).
        /// Called on registry <see cref="SettingsRegistry.ApplyAll"/> so loaded-from-PlayerPrefs values
        /// drive the game on startup. A no-op for an unavailable (extension-hook) entry.
        /// </summary>
        public abstract void Apply();

        /// <summary>Load this entry's value from PlayerPrefs (if present) and apply it. No-op if unavailable.</summary>
        public abstract void LoadFromPrefs();

        /// <summary>Reset this entry to its captured default value and apply it (AC5 reset-to-defaults).</summary>
        public abstract void ResetToDefault();

        /// <summary>
        /// True when the entry's CURRENT live value differs from its registration-time (baked) default
        /// (86cabeqj9 AC9 — the "differs-from-baked-defaults" badge). The panel shows a badge on any entry
        /// where this is true so the Sponsor sees at a glance what he has diverged from; ResetToDefault()
        /// clears it (AC10). Always false for an unavailable extension hook (it never drives a param, so it
        /// can never differ — no false badge on a greyed row). Each archetype compares its own value shape
        /// (float / both range ends / int / bool) against the default it captured at registration.
        /// </summary>
        public abstract bool DiffersFromDefault { get; }
    }
}
