using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Gates the HELD weapon seat's VISIBILITY (ticket 86ca8ce6y; AC4 86caa4bya; 86cahngdg).
    ///
    /// The held weapon seat is attached editor-time to the chibi's right-hand bone and SERIALIZES into
    /// Boot.unity riding that bone (MovementCameraScene.AttachHeroAxeToHand). This component flips the
    /// seat's renderers on/off — VISIBILITY ONLY (the seat/follow pose is owned by HeldAxeRig and is
    /// Sponsor-LOCKED; this component never touches it).
    ///
    /// 86cahngdg (the soak-224 crossed-visual fix) — the gate now fires when the SELECTED belt item is ANY
    /// held-visual weapon (axe OR spear), not the axe alone: with the axe-only predicate, selecting the
    /// spear hid the whole seat -> EMPTY hands even though the spear was the selected weapon. The DISPLAYED
    /// mesh is synced to the selection by <see cref="HeldWeaponCycleDebug.SyncHeldVisualToSelection"/> (the
    /// other half of the same fix — the gate shows the seat, the sync guarantees the seat shows the RIGHT
    /// weapon). The gate also ORs in <see cref="HeldWeaponCycleDebug.DebugViewActive"/> so the empty-handed
    /// [B] look-soak view (knife/sword) still renders; any inventory change clears that flag.
    ///
    /// AC4 (86caa4bya) semantics carry forward per weapon: the held weapon shows IN-HAND only when it is
    /// the SELECTED belt item, NOT merely owned. Axe in belt slot 2 with slot 1 selected -> hidden; select
    /// slot 2 -> shown; weapon moved off the belt into the pack -> hidden. The CHOP gate (ChopTree) is
    /// independent of this visual — it reads Inventory.HasAxe (ownership) directly.
    ///
    /// Subscribes to Inventory.Changed (fires on pickup/select/move) + applies the current state on
    /// enable, so it is correct at spawn (no weapon -> hidden) and after every selection/move (no per-frame
    /// polling). The Inventory reference is wired editor-time (serialized) with an Awake FindObjectOfType
    /// fallback.
    /// </summary>
    /// 86cabh907: HeldAxe is a thin subclass of the SHARED HeldTool gate — the renderer-toggle +
    /// Inventory.Changed wiring live ONCE in the base. The class KEEPS its HeldAxe name even though it now
    /// gates the spear too: the committed Boot.unity references this script by GUID/name, so a rename would
    /// force a binary-scene regen for a pure predicate change ([[unity-procedural-committed-assets-go-stale]]).
    public class HeldAxe : HeldTool
    {
        private HeldWeaponCycleDebug _cycle; // sibling on the seat (may be null in bare test rigs)

        protected override void Awake()
        {
            base.Awake();
            _cycle = GetComponent<HeldWeaponCycleDebug>();
        }

        // Show the seat when the SELECTED belt item is a held-visual weapon (axe/spear/pickaxe), or while the
        // [B] debug look-soak view is active. I-2 (86cakkmr0) added the PICKAXE (both tiers) — the soak-fail was
        // exactly this omission: selecting the pickaxe belt slot satisfied NEITHER axe nor spear, so ShouldShow
        // returned false and the seat renderers stayed DISABLED -> empty hands (confirmed by the -verifyMine
        // held-seat isolation: rendererEnabled=False, holderMesh=wpn_axe_stone_01). The mesh half is fixed in
        // HeldWeaponCycleDebug.SelectionIndexFor (now maps the pickaxe tiers). The defensive no-inventory case
        // (default VISIBLE so a wiring regression fails loud) is handled by the base.
        protected override bool ShouldShow()
            => inventory.IsAxeSelectedInBelt
               || inventory.IsSpearSelectedInBelt
               || inventory.IsPickaxeStoneSelectedInBelt
               || inventory.IsPickaxeIronSelectedInBelt
               || (_cycle != null && _cycle.DebugViewActive);
    }
}
