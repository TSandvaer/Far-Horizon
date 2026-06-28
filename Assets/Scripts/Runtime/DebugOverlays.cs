using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The ONE shared visibility flag for the dev/debug INSTRUMENT-overlay layer (ticket 86cafd6d6 —
    /// "assign a F1 key for debug overlays"). F1 master-toggles every dev overlay together via this flag;
    /// each dev overlay's <c>OnGUI</c> early-returns when <see cref="Visible"/> is false (the clean default).
    ///
    /// === What this gates (the dev/debug INSTRUMENT layer) ===
    ///   - HeldWeaponCycleDebug   ("DEBUG — held weapon: …", bottom-center; the #158 loot-prompt-burier)
    ///   - HeldAxeLengthPicker    ("AXE SHAFT LENGTH: …", top-center)
    ///   - PondNudge              ("POND RECESS / FOAM …", top-center)
    ///   - FloatDiagnostic        (F8 GAP readout)        — F8 stays a SUB-toggle; F1 is the master switch
    ///   - AxeNudgeTool           (F9 nudge panel)        — F9 stays a SUB-toggle
    ///   - CameraFollowNudgeTool  (F7 follow-lerp panel)  — F7 stays a SUB-toggle
    ///   - WorldLookNudgeTool     (F10 world-look panel)  — F10 stays a SUB-toggle
    /// The four F-key panels were ALREADY inert-by-default (their own <c>_active</c> gate); F1 makes it ONE
    /// master switch so a single key reveals/hides the whole instrument layer. Their per-tool F7-F10 keys remain
    /// (they only take effect while the master is ON).
    ///
    /// === What this does NOT gate (always-on, intentionally) ===
    ///   - BootHud      — the BUILD STAMP + "Far Horizon" title plate (soak/capture verification needs the
    ///                    stamp every frame; AC3).
    ///   - SurvivalHud  — ALL real gameplay UI: the warmth/hunger/thirst need bars + the inventory ledger (AC3).
    ///   - The loot prompt + any other player-facing UI.
    /// Only the dev/debug instrument overlays toggle; gameplay UI is untouched.
    ///
    /// === DEFAULT = HIDDEN (AC2) ===
    /// <see cref="Visible"/> starts <c>false</c> so a normal launch / soak / CI capture shows a CLEAN screen
    /// (this also un-buries the loot prompt — fixes the #158 collision). F1 reveals the layer when the
    /// Sponsor/dev wants to tune; F1 again hides it.
    ///
    /// === Static-state-reset discipline (unity-conventions.md §Configurable Enter Play Mode) ===
    /// <see cref="Visible"/> is a mutable runtime static. With domain reload disabled in the editor it would
    /// PERSIST across play-entries (a stale "overlays on" surviving a re-Play), so it carries the mandatory
    /// <see cref="ResetStaticState"/> [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] reset that
    /// re-seeds it to the hidden default each play-entry. StaticStateResetTests' whole-asmdef audit asserts
    /// this exists (a missing reset = that test reds).
    /// </summary>
    public static class DebugOverlays
    {
        /// <summary>
        /// Master visibility for the dev/debug instrument-overlay layer. Default HIDDEN (AC2). F1 toggles it
        /// (<see cref="DebugOverlayToggle"/>); every dev overlay's OnGUI reads it and early-returns when false.
        /// </summary>
        public static bool Visible;

        /// <summary>Reveal the dev-overlay layer (used by the verify-capture path which can't synthesize an
        /// F1 key-down, e.g. FloatDiagnostic.ShowOverlay). Idempotent.</summary>
        public static void Show() => Visible = true;

        /// <summary>Hide the dev-overlay layer.</summary>
        public static void Hide() => Visible = false;

        /// <summary>Flip the layer (the F1 handler).</summary>
        public static void Toggle() => Visible = !Visible;

        /// <summary>
        /// Per-play-entry reset (mandatory for every mutable runtime static — domain-reload-disabled
        /// discipline). Re-seeds the HIDDEN default so a previous play-session's "overlays on" can't leak
        /// into the next editor Play.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Visible = false;
    }
}
