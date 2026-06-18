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

        /// <summary>True when a modal gameplay-UI panel is open and world/locomotion input must be swallowed.</summary>
        public static bool CaptureWorldInput => _openPanels > 0;

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
        }
    }
}
