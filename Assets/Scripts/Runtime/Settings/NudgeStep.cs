using UnityEngine;

namespace FarHorizon.Settings
{
    /// <summary>
    /// The SINGLE SOURCE OF TRUTH for the dev-console AC6 nudge STEP formula (86cagpk72 NIT — the base step
    /// was duplicated between <see cref="FarHorizon.SettingsPanel"/> and its EditMode test, so a drift in the
    /// <c>0.01f</c> base-step constant would NOT trip a test red). Both sides now call THESE helpers, so the
    /// asserted value IS the shipped value.
    ///
    /// Pure C# (no UnityEngine.Object, no <c>Input.*</c> polling) so the formula is fully unit-testable in
    /// EditMode (the headless-time trap, unity-conventions.md) — the panel calls it from <c>NudgeActive</c>;
    /// <c>DevConsoleTests</c> calls the SAME helpers instead of re-implementing them.
    ///
    /// The MODIFIER multiply (Shift = 5x / Ctrl = 0.2x) stays in <see cref="FarHorizon.SettingsPanel"/> because
    /// it reads live <c>Input.GetKey</c> (not headless-testable); the tests pass the multiplier explicitly (the
    /// 5f / 0.2f constants ARE asserted directly). What was unguarded — and is now shared — is the base-step
    /// SIZE per archetype.
    /// </summary>
    public static class NudgeStep
    {
        /// <summary>The base per-nudge step for a single-float slider: 1% of the dialable band, floored at
        /// 0.01 (so a nudge is a sensible fine increment on any band, e.g. 0.11 u/s on the 1–12 walk band).</summary>
        public static float ForSlider(FloatSettingEntry e) => Mathf.Max(0.01f, (e.Max - e.Min) * 0.01f);

        /// <summary>The base per-nudge step for a range (moves the whole window): 1% of the wider hard-limit
        /// span, floored at 0.01.</summary>
        public static float ForRange(RangeSettingEntry e) => Mathf.Max(0.01f, (e.UpperLimit - e.LowerLimit) * 0.01f);

        /// <summary>The int-stepper nudge count for a given modifier multiplier: the entry's own step scaled by
        /// the modifier, floored at 1 (so Ctrl=0.2x never yields a zero step).</summary>
        public static int ForStepper(IntSettingEntry e, float modifier) => Mathf.Max(1, Mathf.RoundToInt(e.Step * modifier));
    }
}
