using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The F1 master on/off for the dev/debug instrument-overlay layer (ticket 86cafd6d6). Polls the
    /// legacy Input key each frame and flips <see cref="DebugOverlays.Visible"/>; every dev overlay's OnGUI
    /// reads that shared flag and early-returns when hidden (the clean default).
    ///
    /// LAYOUT-AGNOSTIC (AC4): F1 is an F-key — Danish-keyboard-safe (the alpha/F-key block is the same
    /// physical position on Danish vs US; punctuation keys are NOT — [[sponsor-danish-keyboard-layout]]).
    /// F1 is verified UNBOUND elsewhere in the project (existing dev toggles are F7/F8/F9/F10).
    ///
    /// LEGACY INPUT (unity-conventions.md §Input System): the project is activeInputHandler=0; this reads
    /// UnityEngine.Input.GetKeyDown(KeyCode.F1) like every other debug toggle here — no new Input System
    /// dependency (a flip to the New Input System would dead-stick every legacy consumer at once).
    ///
    /// Serialized onto the Boot object editor-time (NOT an Awake-add) per the editor-vs-runtime
    /// serialization trap; DebugOverlayToggleSceneTests guards its serialized presence. The component does
    /// NO gameplay work — its only per-frame cost is one key poll, gated so the overlay layer stays hidden
    /// until F1 is pressed.
    /// </summary>
    public class DebugOverlayToggle : MonoBehaviour
    {
        [Tooltip("The master on/off key for the dev/debug overlay layer. F1 — layout-agnostic (F-keys are " +
                 "Danish-keyboard-safe) and verified unbound elsewhere (existing dev toggles are F7-F10).")]
        public KeyCode toggleKey = KeyCode.F1;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                DebugOverlays.Toggle();
                Debug.Log("[DebugOverlayToggle] dev/debug overlays " +
                          (DebugOverlays.Visible ? "ON (F1 to hide)" : "off (clean screen)"));
            }
        }
    }
}
