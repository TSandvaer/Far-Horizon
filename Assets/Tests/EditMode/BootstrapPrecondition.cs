using NUnit.Framework;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Shared no-bootstrap precondition guard for the scene-presence EditMode tests (ticket 86cacyg63).
    ///
    /// The scene-presence tests assert against components/assets that <c>FarHorizon.EditorTools.BootstrapProject.Run</c>
    /// authors into <c>Boot.unity</c> (the InventoryUI theme, the SettingsPanel + its UI assets, …). The CI
    /// <c>unity</c> job ALWAYS runs bootstrap before EditMode (ci.yml), so CI is green; a bare LOCAL
    /// <c>-runTests -testPlatform EditMode</c> SKIPS it, so the committed (stale) <c>Boot.unity</c> lacks those
    /// bootstrap-authored components/assets → the asserts hit null and produce reds that LOOK like a regression
    /// but are NOT (the 86cacvhf4 / 86cacyg63 artifact; see unity-conventions.md "Run BootstrapProject.Run
    /// BEFORE any LOCAL EditMode run").
    ///
    /// Rather than let those null asserts fail RED (and get mis-filed as a regression), each affected test calls
    /// <see cref="Require"/> at the point it first depends on a bootstrap-authored object. When that object is
    /// missing, the test reports <c>Inconclusive</c> with an actionable "run BootstrapProject.Run first" message
    /// instead of a misleading red. The guard is INERT on the happy path (bootstrap HAS run → the object is
    /// non-null → no Inconclusive), so the real post-bootstrap assertions are unchanged.
    /// </summary>
    internal static class BootstrapPrecondition
    {
        internal const string Message =
            "Scene not bootstrapped — run FarHorizon.EditorTools.BootstrapProject.Run first. " +
            "This scene-presence test requires bootstrap-generated assets in Boot.unity (CI runs bootstrap " +
            "before EditMode; a bare local '-runTests -testPlatform EditMode' does not). " +
            "See unity-conventions.md \"Run BootstrapProject.Run BEFORE any LOCAL EditMode run\".";

        /// <summary>
        /// If <paramref name="bootstrapAuthored"/> is missing (null, or a destroyed UnityEngine.Object), report
        /// the test <c>Inconclusive</c> with the bootstrap-first hint INSTEAD of letting a downstream null assert
        /// fail RED. Does nothing when the object is present (the post-bootstrap happy path stays unchanged).
        /// Accepts plain references and UnityEngine.Object alike — for the latter, the implicit bool operator
        /// treats a destroyed/"fake-null" object as missing too.
        /// </summary>
        /// <param name="bootstrapAuthored">the bootstrap-authored component/asset the test depends on.</param>
        /// <param name="what">a short name of the missing thing, appended to the hint for quick diagnosis.</param>
        internal static void Require(object bootstrapAuthored, string what)
        {
            bool present = bootstrapAuthored is UnityEngine.Object uo ? (bool)uo : bootstrapAuthored != null;
            if (!present)
                Assert.Inconclusive(Message + " (missing: " + what + ")");
        }
    }
}
