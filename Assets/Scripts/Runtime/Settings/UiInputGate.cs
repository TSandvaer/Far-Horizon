using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A tiny global flag: "a modal gameplay-UI panel is open, so swallow world/locomotion input this
    /// frame" (ticket 86caa4bqp). UI Toolkit panels do NOT block the legacy <c>Input.*</c> polling that
    /// WasdMovement / OrbitCamera read (research §E1 — input bleeds through an open panel), so each
    /// input consumer checks <see cref="CaptureWorldInput"/> and skips its world input while a panel owns
    /// the screen. The SettingsPanel sets it on open and clears it on close; future modal panels
    /// (inventory Tab, 86caa4bya) set it the same way.
    ///
    /// MUTABLE RUNTIME STATIC → carries the required [RuntimeInitializeOnLoadMethod(SubsystemRegistration)]
    /// reset (unity-conventions.md §Configurable Enter Play Mode): with domain reload disabled the flag
    /// would otherwise persist "open" across editor play-entries and dead-stick movement on the next Play.
    /// The reset clears it to false on every play-entry. (StaticStateResetTests enforces this mechanically.)
    /// </summary>
    public static class UiInputGate
    {
        private static int _openPanels;
        private static bool _pointerOverConsole;

        /// <summary>True when a modal gameplay-UI panel is open and world/locomotion input must be swallowed.</summary>
        public static bool CaptureWorldInput => _openPanels > 0;

        /// <summary>
        /// True while the mouse pointer is INSIDE the (non-modal) dev-console panel rect (ticket 86cabeqj9 soak
        /// NIT — SCROLL passthrough). The dev console is NON-MODAL (it does NOT push <see cref="CaptureWorldInput"/>
        /// merely by being open — WASD/orbit stay live on purpose), so the camera zoom's <c>Input.GetAxisRaw
        /// ("Mouse ScrollWheel")</c> would ALSO fire while the Sponsor scrolls the wheel over the panel — the
        /// camera zooms under his cursor. A UI Toolkit <c>WheelEvent.StopPropagation</c> can NOT stop the legacy
        /// <c>Input.*</c> polling the OrbitCamera reads (the exact reason <see cref="CaptureWorldInput"/> exists —
        /// research §E1). So the panel sets this on pointer-enter / clears it on pointer-leave, and the OrbitCamera
        /// gates ONLY its scroll-zoom on it — WASD/orbit are untouched (the intentional non-modal passthrough).
        /// </summary>
        public static bool PointerOverConsole => _pointerOverConsole;

        /// <summary>The SettingsPanel sets this on pointer-enter/leave of its panel rect (86cabeqj9 scroll NIT).</summary>
        public static void SetPointerOverConsole(bool over) => _pointerOverConsole = over;

        /// <summary>A panel opened — increment the open count (so two panels don't un-gate each other).</summary>
        public static void PushPanel() => _openPanels++;

        /// <summary>A panel closed — decrement (clamped at 0).</summary>
        public static void PopPanel() => _openPanels = Mathf.Max(0, _openPanels - 1);

        /// <summary>Force the gate to a known open/closed state (the SettingsPanel uses this idempotently).</summary>
        public static void SetPanelOpen(bool open, ref bool tracked)
        {
            if (open && !tracked) { PushPanel(); tracked = true; }
            else if (!open && tracked) { PopPanel(); tracked = false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _openPanels = 0;
            _pointerOverConsole = false;
        }
    }
}
