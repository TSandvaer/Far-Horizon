using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The F2 master on/off for the LEGACY dev/debug instrument-overlay layer (ticket 86cafd6d6; key moved
    /// F1→F2 by 86cabeqj9 NIT). Polls the legacy Input key each frame and flips
    /// <see cref="DebugOverlays.Visible"/>; every legacy dev overlay's OnGUI reads that shared flag and
    /// early-returns when hidden (the clean default).
    ///
    /// F1/F2 DE-CONFLICT (86cabeqj9 soak NIT — the Sponsor's chosen resolution). The dev CONSOLE
    /// (<see cref="FarHorizon.SettingsPanel"/>) KEEPS F1 and now polls F1 DIRECTLY (it no longer rides this
    /// shared flag). The LEGACY IMGUI overlays (axe-shaft length, pond recess/foam, the F7-F10 nudge panels,
    /// the F8 float-gap readout) move to F2 here — so F1 opens ONLY the console and F2 reveals ONLY the
    /// legacy overlays. Before this they shared <see cref="DebugOverlays.Visible"/>, so one F1 popped BOTH
    /// (the soak complaint). The full F7-F10→console absorption (ticket 86caber95) is separate; this only
    /// splits the F1/F2 master keys. NOTE — the per-tool F7-F10 SUB-toggles are unchanged: they still take
    /// effect only while this F2 master is ON.
    ///
    /// LAYOUT-AGNOSTIC (AC4): F2 is an F-key — Danish-keyboard-safe (the alpha/F-key block is the same
    /// physical position on Danish vs US; punctuation keys are NOT — [[sponsor-danish-keyboard-layout]]).
    /// F2 is verified UNBOUND elsewhere in the project (the console takes F1; per-tool sub-toggles are
    /// F7/F8/F9/F10).
    ///
    /// LEGACY INPUT (unity-conventions.md §Input System): the project is activeInputHandler=0; this reads
    /// UnityEngine.Input.GetKeyDown(KeyCode.F2) like every other debug toggle here — no new Input System
    /// dependency (a flip to the New Input System would dead-stick every legacy consumer at once).
    ///
    /// Serialized onto the Boot object editor-time (NOT an Awake-add) per the editor-vs-runtime
    /// serialization trap; DebugOverlayToggleSceneTests guards its serialized presence. The component does
    /// NO gameplay work — its only per-frame cost is one key poll, gated so the overlay layer stays hidden
    /// until F2 is pressed.
    /// </summary>
    public class DebugOverlayToggle : MonoBehaviour
    {
        [Tooltip("The master on/off key for the LEGACY dev/debug overlay layer. F2 — layout-agnostic (F-keys " +
                 "are Danish-keyboard-safe) and verified unbound elsewhere (the dev console takes F1; per-tool " +
                 "sub-toggles are F7-F10). Moved F1→F2 by 86cabeqj9 so F1 opens ONLY the console.")]
        public KeyCode toggleKey = KeyCode.F2;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                DebugOverlays.Toggle();
                Debug.Log("[DebugOverlayToggle] legacy dev/debug overlays " +
                          (DebugOverlays.Visible ? "ON (F2 to hide)" : "off (clean screen)"));
            }
        }
    }
}
