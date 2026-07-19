namespace FarHorizon
{
    /// <summary>
    /// The SHARED build-menu seam (ticket 86catpvpa — the C build menu). This is the Pattern-A
    /// "vocabulary contract" the build menu is authored against (parallel-shared-concept discipline):
    /// EVERY placeable structure that appears as a row in <see cref="BuildMenuUI"/> implements this
    /// interface, and ③ forge (86camz9vh) + ⑤ campfire (86camz9w7) + any future placeable REGISTER a
    /// row into the ONE menu via <see cref="BuildMenuUI.RegisterPlaceable"/> — they do NOT fork a
    /// parallel menu. Keep the member NAMES stable: they are the seam ③/⑤ consume.
    ///
    /// Both shipped placement drivers already carry the ① ghost-placement flow this fronts —
    /// <see cref="CraftingTablePlacement"/> (① 86camz9uz) and <see cref="ForgePlacement"/> (③) — so
    /// implementing this interface on them is a thin adapter over their existing
    /// <c>EnterPlacement()</c> / <c>CanAfford*()</c> / <c>Is*Built</c> seams. A future ⑤-converted
    /// CampfirePlacement (once it gains the invisible-until-placed ghost flow — that conversion is ⑤'s
    /// scope, not this ticket's) implements the SAME six members and adds one registration line in
    /// MovementCameraScene.BuildCampfire; no menu change.
    ///
    /// The menu's contract (see <see cref="BuildMenuUI"/>): a row is greyed + NON-interactive when
    /// <see cref="CanAffordBuild"/> is false OR <see cref="IsBuildComplete"/> is true; selecting an
    /// affordable, not-yet-built row calls <see cref="BeginBuildPlacement"/>, which MUST enter the ①
    /// free-cursor ghost-placement flow (the regression guard — a select that does not enter the ghost
    /// reds the EditMode test).
    /// </summary>
    public interface IBuildPlaceable
    {
        /// <summary>Stable display name of this placeable (the row label + the per-structure identifier
        /// the seam keys on) — e.g. "Crafting Table", "Forge", "Campfire".</summary>
        string BuildDisplayName { get; }

        /// <summary>Wood the structure costs (shown in the row's cost text; drives the greyed state).</summary>
        int BuildWoodCost { get; }

        /// <summary>Stone the structure costs (shown in the row's cost text; drives the greyed state).</summary>
        int BuildStoneCost { get; }

        /// <summary>True iff the pack can currently afford this structure (both materials). The menu paints
        /// the row greyed + refuses selection when false.</summary>
        bool CanAffordBuild { get; }

        /// <summary>True once this structure has been built (one-per-world in the current design) — the
        /// menu hides / disables the row so it can't be placed twice.</summary>
        bool IsBuildComplete { get; }

        /// <summary>Enter the ① free-cursor ghost-placement flow for this structure (free-cursor ghost /
        /// left-click place / Escape cancel / scroll rotate — the ① mechanics VERBATIM, no parallel flow).
        /// The menu calls this after closing itself when an affordable, un-built row is selected.</summary>
        void BeginBuildPlacement();
    }
}
