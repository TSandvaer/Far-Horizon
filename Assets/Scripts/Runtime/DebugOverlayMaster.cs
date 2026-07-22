using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The F10 DEBUG-OVERLAY MASTER toggle (ticket 86caju054 — re-homed off the retired SneakIsolationTool).
    ///
    /// A neutral, single-purpose key handler: pressing F10 flips the shared <see cref="DebugOverlays.Visible"/>
    /// layer master, which every dev/debug overlay's <c>OnGUI</c> reads (WorldLookNudgeTool F10 / AxeNudgeTool F9 /
    /// FloatDiagnostic F8 / CameraFollowNudgeTool F7 / HeldWeaponCycleDebug / PondNudge).
    /// Before this ticket that flip lived inside <c>SneakIsolationTool.Update</c>; when the Sponsor retired the
    /// sneak-isolation panel (86caju054, "the sneak isolation panel should die"), the F10 master was re-homed HERE
    /// so F10 keeps toggling the remaining overlays together while the sneak readout + its F5/F6 handles are gone.
    ///
    /// DEFAULT = inert: <see cref="DebugOverlays.Visible"/> starts hidden, so a normal launch / soak / CI capture
    /// shows a clean screen until F10 is pressed. DANISH-KEYBOARD-SAFE (the project rule
    /// [[sponsor-danish-keyboard-layout]]): F10 is an F-key (same physical position on Danish vs US). F1 is the
    /// dev console (SettingsPanel) — NOT consumed here; F2 is UNBOUND (the legacy F2 DebugOverlayToggle master was
    /// removed in 86cah90cp round-3, leaving F10 the single master).
    ///
    /// This carries no mutable runtime static (the master flag + its mandatory SubsystemRegistration reset live on
    /// <see cref="DebugOverlays"/>), so the StaticStateResetTests whole-asmdef audit is unaffected.
    /// </summary>
    public class DebugOverlayMaster : MonoBehaviour
    {
        [Tooltip("Show/hide the whole dev/debug overlay layer (WorldLookNudgeTool + AxeNudge + FloatDiagnostic + " +
                 "the held-weapon/pond panels). F10 — the SINGLE debug-overlay master key, Danish-keyboard-safe. " +
                 "Flips the shared DebugOverlays.Visible master. F1 is the dev console — NOT consumed here; F2 is " +
                 "UNBOUND (the legacy F2 master was removed, 86cah90cp round-3).")]
        public KeyCode overlayToggleKey = KeyCode.F10;

        void Update()
        {
            // The ONLY normal-play cost: one key poll. F10 flips the shared master (86caju054 — re-homed off the
            // retired SneakIsolationTool); every dev overlay's OnGUI early-returns while the master is hidden.
            if (Input.GetKeyDown(overlayToggleKey))
            {
                DebugOverlays.Toggle();
                Debug.Log("[DebugOverlayMaster] debug overlays " +
                          (DebugOverlays.Visible ? "ON (F10 to hide) — world-look + nudge panels"
                                                 : "off (clean screen)"));
            }
        }
    }
}
