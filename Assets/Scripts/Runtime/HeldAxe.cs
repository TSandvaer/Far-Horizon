using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Gates the HELD sourced hatchet's VISIBILITY (ticket 86ca8ce6y; AC4 86caa4bya).
    ///
    /// The sourced axe is attached editor-time to the chibi's right-hand bone and SERIALIZES into
    /// Boot.unity riding that bone (MovementCameraScene.AttachHeroAxeToHand). This component flips the
    /// axe's renderers on/off — VISIBILITY ONLY (the seat/follow pose is owned by HeldAxeRig and is
    /// Sponsor-LOCKED; this component never touches it).
    ///
    /// AC4 (86caa4bya) — the held axe shows IN-HAND only when the axe is the SELECTED belt item, NOT
    /// merely owned: it gates on <see cref="Inventory.IsAxeSelectedInBelt"/>. Axe in belt slot 2 with
    /// slot 1 selected -> hidden; select slot 2 -> shown; axe moved off the belt into the pack ->
    /// hidden (still owned, but not the selected belt item). This SUPERSEDES the old HasAxe (ownership)
    /// gate: before the belt existed, "owns the axe" == "holds the axe", so HasAxe was correct; with the
    /// belt, selection is the right signal (item-model contract §5). The CHOP gate (ChopTree) is
    /// independent of this visual — it reads Inventory.HasAxe (ownership) directly.
    ///
    /// Subscribes to Inventory.Changed (fires on pickup/select/move) + applies the current state on
    /// enable, so it is correct at spawn (no axe -> hidden) and after every selection/move (no per-frame
    /// polling). The Inventory reference is wired editor-time (serialized) with an Awake FindObjectOfType
    /// fallback.
    /// </summary>
    /// 86cabh907: HeldAxe is now a thin subclass of the SHARED HeldTool gate — the renderer-toggle +
    /// Inventory.Changed wiring live ONCE in the base. The axe-specific bit is the selection predicate
    /// (the axe shows when it is the selected belt item). The inventory field is inherited (same name →
    /// the committed Boot.unity wiring + HeroAxeSceneTests' held.inventory read carry forward unchanged).
    public class HeldAxe : HeldTool
    {
        // Show the held axe only when the axe is the SELECTED belt item (AC4 86caa4bya). The defensive
        // no-inventory case (default VISIBLE so a wiring regression fails loud) is handled by the base.
        protected override bool ShouldShow() => inventory.IsAxeSelectedInBelt;
    }
}
