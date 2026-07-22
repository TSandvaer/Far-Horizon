namespace FarHorizon
{
    /// <summary>
    /// Shared contract for every BUILD-GATED debug NUDGE-PANEL tool (AxeNudgeTool / WorldLookNudgeTool /
    /// CameraFollowNudgeTool — and any future panel). Extracted from PR #187's hard-coded 3-type check
    /// (ticket 86cafz9jr follow-up): <see cref="PondNudge.AnyNudgePanelActive"/> used to name the three
    /// concrete tool types one-by-one, so adding a 4th panel meant remembering to extend a hand-maintained
    /// list — and a missed edit silently re-introduced the #176 PgUp/PgDn collision class #187 just fixed.
    ///
    /// Now every nudge panel implements this interface and the deconflict gate discovers the active set
    /// THROUGH the interface (a <c>MonoBehaviour</c> scene scan filtered by <c>is INudgePanel</c>), so the
    /// gate stays correct automatically as panels are added/removed — no type list to forget.
    ///
    /// CONTRACT: the always-live <see cref="PondNudge"/> recess handle yields PgUp/PgDn whenever ANY
    /// <c>INudgePanel.IsActive</c> is true (the active/focused panel owns the key). The implementers are
    /// mutually exclusive among themselves (each <c>Activate()</c> deactivates the siblings), so at most one
    /// is ever active — but the gate only needs "is any active", which this single member answers.
    /// </summary>
    public interface INudgePanel
    {
        /// <summary>True while this nudge panel is toggled ON (and thus owns PgUp/PgDn + the shared dial keys).</summary>
        bool IsActive { get; }
    }
}
