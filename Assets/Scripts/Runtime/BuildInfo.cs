using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Build-identity stamp loaded from Resources/BuildStamp.txt (written at bootstrap).
    ///
    /// Carries the spike's "BUILD &lt;tag&gt; | &lt;UTC&gt; | &lt;sha&gt;" ritual forward into the
    /// production project (FINDINGS.txt iter-3): every soak screenshot must self-identify the
    /// build that produced it, the desktop analogue of the HTML5 BuildInfo-SHA convention. We
    /// hit build-identity ambiguity twice in the spike (could not tell an old build from a new
    /// one off a soak screenshot), so the stamp earned its keep — it ships from day one here.
    ///
    /// Format written by BootstrapProject.WriteBuildStamp:  "&lt;tag&gt; | &lt;UTC ISO&gt; | &lt;git-sha&gt;"
    /// e.g.  "boot | 2026-06-12T13:55:01Z | a1b2c3d"
    /// </summary>
    public static class BuildInfo
    {
        private static string _stamp;

        /// <summary>
        /// Clears the lazily-cached stamp on every Play-Mode entry.
        ///
        /// With Configurable Enter Play Mode set to "Do not reload Domain or Scene"
        /// (ProjectSettings/EditorSettings.asset, ticket 86ca9a39q), the domain is NOT reloaded
        /// between editor play sessions, so this static field PERSISTS its value across play-entries.
        /// Without this reset, a play-entry that occurred BEFORE a fresh BuildStamp.txt was written
        /// would keep serving the stale cached stamp. The reset re-arms the lazy load so each
        /// play-entry re-reads Resources/BuildStamp.txt.
        ///
        /// Headless test runs + CI builds RELOAD the domain regardless of this setting, so they
        /// always start with _stamp == null; this reset is purely an editor-iteration safety net.
        ///
        /// RULE (unity-conventions.md §"Configurable Enter Play Mode"): every NEW mutable runtime
        /// static field/event must add a [RuntimeInitializeOnLoadMethod(SubsystemRegistration)]
        /// reset, or it will accumulate stale state across editor play-entries with reload disabled.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _stamp = null;
        }

        /// <summary>The full build stamp string, loaded once from Resources/BuildStamp.txt.</summary>
        public static string Stamp
        {
            get
            {
                if (_stamp == null)
                {
                    var ta = Resources.Load<TextAsset>("BuildStamp");
                    _stamp = ta != null ? ta.text.Trim() : "unknown";
                }
                return _stamp;
            }
        }
    }
}
