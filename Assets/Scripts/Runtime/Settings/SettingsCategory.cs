using System.Collections.Generic;

namespace FarHorizon.Settings
{
    /// <summary>
    /// The PLAYER-vs-DEV routing map for the split settings UI (ticket 86cah8ukr). The single
    /// <see cref="SettingsRegistry"/> is built ONCE (all Populate* bindings UNCHANGED — "route views, don't
    /// re-bind"); the two panel views (F1 player Settings + F3 dev console) each filter that one registry by
    /// this predicate. Classification is keyed purely by the setting's stable <c>Id</c>, EXTERNAL to
    /// registration, so no Add*/Populate* call has to carry a category flag (the registry + every binding stays
    /// byte-identical; only the view routing is new).
    ///
    /// THE PLAYER-FACING SET (Sponsor-confirmed 2026-07-03 walkthrough — INCLUDING the three flagged defaults):
    ///   • belt slots + inventory stack size (the two inventory rows a player legitimately tunes);
    ///   • warmth/hunger/thirst decay ON/OFF toggles + their decay-rate sliders (the survival-difficulty dials).
    /// EVERYTHING ELSE is dev console (F3): world-look, arm-pose, camera/zoom, held-weapon, locomotion (incl.
    /// walk/run speed → DEV), resource timers/yields, INVENTORY SLOTS → DEV, console UI + TEXT scale → DEV.
    /// The three Sponsor-flagged defaults — <c>inventory_slots</c>, walk/run speed, UI <c>console_text_scale</c> —
    /// deliberately fall to DEV by being ABSENT from the player allowlist below (they are not enumerated here).
    ///
    /// Pure C# (no UnityEngine) so the categorization guard (AC4) is fully EditMode-testable with no scene: the
    /// test asserts F1 shows EXACTLY the player rows and the dev view shows NONE of them (and vice-versa) — the
    /// mis-route bug class (a dev-only knob leaking into the player panel, or a player dial buried in the dev
    /// console) is a categorization regression this map + its test pin.
    /// </summary>
    public static class SettingsCategory
    {
        /// <summary>The player-facing allowlist (F1 Settings). Everything NOT in here is dev console (F3).</summary>
        private static readonly HashSet<string> PlayerIds = new HashSet<string>
        {
            SettingsCatalog.BeltSlotsId,       // belt_slots
            SettingsCatalog.StackSizeId,       // inventory_stack_size
            SettingsCatalog.WarmthEnabledId,   // warmth_enabled
            SettingsCatalog.HungerEnabledId,   // hunger_enabled
            SettingsCatalog.ThirstEnabledId,   // thirst_enabled
            SettingsCatalog.WarmthDecayId,     // warmth_decay_rate
            SettingsCatalog.HungerDecayId,     // hunger_decay_rate
            SettingsCatalog.ThirstDecayId,     // thirst_decay_rate
        };

        /// <summary>
        /// The CONDITIONAL-VISIBILITY dependency map: a per-need decay-rate SLIDER (key) is shown only while its
        /// on/off TOGGLE (value) is ON. When the toggle flips OFF the slider row hides (a disabled need has no
        /// rate to tune); flipping it back ON re-reveals the slider. The panel evaluates this on build, on every
        /// toggle change, and after a reset-to-defaults. Both endpoints are PLAYER-facing (they live in the F1
        /// panel together), so the show/hide is entirely within that one view.
        /// </summary>
        private static readonly Dictionary<string, string> DecaySliderToGate = new Dictionary<string, string>
        {
            { SettingsCatalog.WarmthDecayId, SettingsCatalog.WarmthEnabledId },
            { SettingsCatalog.HungerDecayId, SettingsCatalog.HungerEnabledId },
            { SettingsCatalog.ThirstDecayId, SettingsCatalog.ThirstEnabledId },
        };

        /// <summary>True when this setting id belongs in the PLAYER-facing F1 Settings panel.</summary>
        public static bool IsPlayer(string id) => id != null && PlayerIds.Contains(id);

        /// <summary>True when this setting id belongs in the DEV console (F3) — everything not player-facing.</summary>
        public static bool IsDev(string id) => !IsPlayer(id);

        /// <summary>The number of player-facing ids (guard anchor for the categorization test).</summary>
        public static int PlayerCount => PlayerIds.Count;

        /// <summary>The decay-slider → gating-toggle pairs for the conditional-visibility pass (AC1/AC4).</summary>
        public static IEnumerable<KeyValuePair<string, string>> DecaySliderGates => DecaySliderToGate;

        /// <summary>The on/off toggle id that gates <paramref name="sliderId"/>'s visibility, or null if the row
        /// is not a conditionally-visible decay slider.</summary>
        public static string GateToggleFor(string sliderId)
            => sliderId != null && DecaySliderToGate.TryGetValue(sliderId, out var gate) ? gate : null;
    }
}
