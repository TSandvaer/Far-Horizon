using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// DEBUG / SOAK-VIEWING handle (ticket 86cabh907) — lets the Sponsor SEE each member of the in-house
    /// weapon family HELD by the castaway, in-engine, by pressing a key to CYCLE the held mesh through the
    /// full 15-item family (86catvb6u §3 completed the tier grid): STONE axe/knife/sword/spear/pickaxe ->
    /// IRON pickaxe/axe/knife/sword/spear -> WOOD axe/knife/sword/spear/pickaxe -> (wrap). The castaway visibly
    /// wields each weapon as it is cycled. (86cam9q5f appended both pickaxe tiers, 86camz9vh the iron blades,
    /// 86catvb6u the wood tier — so ALL 15 held seats are judged + F9-dialed in-hand via this discrete picker.)
    ///
    /// 86cahngdg — BELT SELECTION NOW OWNS THE HELD VISUAL (the soak-224 crossed-visual fix). The old
    /// framing ("belt -> wield is a LATER ticket") is OBSOLETE for the axe + spear: this component now
    /// SUBSCRIBES to <see cref="Inventory.Changed"/> and SYNCS the displayed mesh to the SELECTED belt
    /// weapon (axe -> index 0, spear -> index <see cref="SpearFamilyIndex"/>) via the SAME proven
    /// ResolveMeshes/ApplyCurrent swap the [B] key uses. The soak-224 defect was exactly this missing
    /// coupling: the seat's VISIBILITY gate (HeldAxe) fired on IsAxeSelectedInBelt while the DISPLAYED
    /// mesh stayed whatever [B] last cycled — so with the spear mesh displayed, selecting the axe slot
    /// rendered the SPEAR in hand, and selecting the spear slot rendered EMPTY hands (no spear predicate,
    /// no mesh sync). Selection is now AUTHORITATIVE:
    ///   - a held-visual weapon (axe/spear) selected  -> the mesh syncs to it; the [B] cycle REFUSES
    ///     (logs why) so the debug handle can never re-create the crossed state in a soak;
    ///   - no held-visual weapon selected -> [B] still cycles for the knife/sword look-soak aid, and
    ///     <see cref="DebugViewActive"/> lets the HeldAxe gate show that debug view empty-handed; ANY
    ///     inventory change clears the debug view and re-asserts the selection (self-healing).
    ///
    /// The knife/sword remain [B]-only (no belt items exist for them); their real equip is a later ticket.
    /// This handle still never touches the inventory, the belt, the chop gate, or any gameplay state.
    ///
    /// HOW IT WORKS — swap the MESH on the SHARED HeldTool seat:
    ///   - The component is serialized onto the HeroAxe object (the held seat) editor-time (MovementCamera
    ///     Scene.AttachHeroAxeToHand). HeroAxe is pose-driven by <see cref="HeldAxeRig"/> and visibility-
    ///     gated by <see cref="HeldAxe"/>; this handle NEVER touches the seat transform, the rig, or the
    ///     visibility gate — it only swaps the MeshFilter.sharedMesh (+ a per-weapon mesh-holder offset).
    ///   - The AXE (index 0) starts as the shipped default. As of 86caffwv5 round-7 its held seat is NO LONGER
    ///     zero-locked: the Sponsor RETIRED the axe lock and dialed a real axe-class in-hand seat (the "same dial
    ///     for rock and metal" directive), so ApplyCurrent composes WeaponMeshLocal*[0] on the captured baseline
    ///     exactly like the other weapons (the axe SCALE stays 1.0 — only offset/euler were dialed; the scale-dial
    ///     still refuses the axe). The handle starts on the axe, so a soak that never presses the key sees the axe.
    ///   - knife / sword / spear seat on the SAME shared seat. Their FBX grip-origin is (0,0,0) with the
    ///     blade up +Z (bl_10/bl_11/bl_12), whereas the axe FBX uses a grip-MIDPOINT origin (z=0.45) and is
    ///     height-normalized; they are also un-normalized (taller). So each needs a SMALL per-weapon mesh-
    ///     holder offset/scale to seat REASONABLY in the hand. These are ROUGH look-soak values, NOT the
    ///     precise grip — the precise per-weapon grip is the later equip ticket.
    ///
    /// MESH SOURCE AT RUNTIME: AssetDatabase is editor-only, so the weapon meshes are pulled from
    /// Resources/WeaponSetLineup.prefab (built by WeaponPackAssetGen.BuildLineupPrefab — the same prefab
    /// the WeaponSetVerifyCapture uses). The prefab carries all four weapon meshes (axe/knife/sword/spear
    /// child objects named after their FBX), so the build path has a runtime-loadable source with no extra
    /// asset plumbing.
    ///
    /// INPUT + HUD: pure legacy-Input + IMGUI (the project idiom — AxeNudgeTool / BootHud / SurvivalHud),
    /// build-safe (no new-Input-System / shader dependency). The HUD label is ALWAYS shown (this is a soak-
    /// viewing aid, not a gated tuning panel) so the Sponsor always knows which weapon is held + the key.
    ///
    /// STATIC STATE: instance fields only — NO mutable runtime statics, so no SubsystemRegistration reset
    /// is needed (unity-conventions.md §Configurable Enter Play Mode; StaticStateResetTests stays green).
    /// </summary>
    public class HeldWeaponCycleDebug : MonoBehaviour
    {
        [Tooltip("Cycle the held weapon (axe -> knife -> sword -> spear -> wrap). B is free in normal play " +
                 "(WASD+Space+1..9 are the gameplay keys; B is only otherwise consumed inside the F9 nudge " +
                 "panel, which is mutually exclusive with normal play).")]
        public KeyCode cycleKey = KeyCode.B;

        // LIVE SCALE DIAL (this soak's reframe — the held knife/sword/spear read MUCH SMALLER than the axe).
        // Scale the CURRENT held weapon's mesh-holder up/down LIVE so the Sponsor dials each weapon's in-hand
        // size BY EYE and reads the number off the HUD/log to bake into WeaponMeshScale. ~5% steps. Two key
        // pairs each direction so a non-US-layout keyboard still has a working pair. The AXE (index 0) is
        // Sponsor-LOCKED — the dial REFUSES to scale it (it always restores the captured original TRS).
        //   ] or =  -> scale the held weapon UP   (+5%)
        //   [ or -  -> scale the held weapon DOWN (-5%)
        // Chosen to NOT collide with the always-on [B] cycle NOR the F9 AxeNudgeTool's keys (F9/Tab/B/arrows/
        // PgUp-Dn/TGYHUJ/Shift/Ctrl) — bracket+equals+minus are free in both normal play and that panel.
        public KeyCode scaleUpKey = KeyCode.RightBracket;   // ]
        public KeyCode scaleUpKeyAlt = KeyCode.Equals;      // =
        public KeyCode scaleDownKey = KeyCode.LeftBracket;  // [
        public KeyCode scaleDownKeyAlt = KeyCode.Minus;     // -
        [Tooltip("Per-keypress multiplicative scale step for the live dial (1.05 = +5% up / -5% down).")]
        public float scaleStep = 1.05f;

        // DANISH-SAFE OVERALL-HELD-SCALE LETTER KEYS (86cakkfz9 v3 dial-in; ABSORBS 86cajuuz0). The
        // bracket/equals/minus scale keys above are US-position PUNCTUATION that does NOT register on the
        // Sponsor's DANISH LAPTOP ([[sponsor-danish-keyboard-layout]]). O / I are LETTER keys that sit at ~the
        // same physical position on Danish vs US, so they always land. They drive the SAME overall-held-scale
        // dial as ]/=/[/- (scale the CURRENT held weapon's mesh-holder ±5%; the axe is Sponsor-LOCKED and
        // refuses). O = held weapon BIGGER (+5%), I = held weapon SMALLER (-5%). Both free: not WASD/Space/
        // Shift, not 1..9 belt, not [B] cycle, not [N] arm-switch, not [K] F9 target-cycle, not TGYHUJ F9
        // rotation, not the F9 arrows/PgUp-Dn.
        //
        // 86cajuuz0 ABSORBED — the axe HEAD-vertex resize dial is REMOVED (these O/I keys formerly drove it).
        // WHY: the axe head is now AUTHORED Blender geometry (wpn_axe_stone_01 — a knapped biface); runtime
        // vertex-scaling an authored head distorts it into the Sponsor-rejected "chipping"
        // ([[weapon-asset-material-honest-pattern-via-geometry]]). Head SIZE is a Blender re-author now, NOT a
        // runtime dial. Overall held-scale is the honest in-game handle; for the axe's own size use the
        // settings-console HeldScale row (a locked-baseline multiplier). "Don't leave dead keys" (86cakkfz9).
        public KeyCode scaleUpKeyDanish = KeyCode.O;        // O — Danish-safe letter, held weapon BIGGER
        public KeyCode scaleDownKeyDanish = KeyCode.I;      // I — Danish-safe letter, held weapon SMALLER

        // The LIVE family meshes' names inside Resources/WeaponSetLineup.prefab (the child object names = the
        // FBX file-name-without-extension; see WeaponPackAssetGen.BuildFamilyPrefab). Order = cycle order.
        // 86cajkk7h: indices 0-3 are the STONE tier (the first-craft weapons); the iron nodes for axe/knife/
        // sword/spear also live in the family prefab (contrast capture) but are NOT cycled.
        // 86cam9q5f: the PICKAXE (the 5th tool type) is APPENDED in BOTH tiers — index 4 (stone) + index 5
        // (iron) — so BOTH pickaxes are judged IN-HAND via this discrete mesh-swap picker (spec §4/I-1, Bar 5;
        // the pickaxe has no belt pickup yet — belt/crafting is I-2+ — so the [B] cycle is the only in-hand
        // view). Appending KEEPS indices 0-3 (AxeFamilyIndex 0, SpearFamilyIndex 3) unchanged so the belt-
        // selection sync + its EditMode contract are untouched. PUBLIC + static so the EditMode guard reads
        // the cycle contract directly (no reflection): the family prefab MUST carry a mesh node for each of
        // these, or cycling resolves nothing.
        // 86camz9vh (③): APPEND the 4 iron weapons (axe/knife/sword/spear iron — indices 6-9) so the shipped iron
        // FBX set (#254/#283) is judged IN-HAND via this picker (spec §DRAFT③ Bar 5 — "iron in-hand via the
        // picker"), mirroring how 86cam9q5f appended the iron pickaxe. Append-only: indices 0-5 (the soaked stone
        // tier + both pickaxes) are BYTE-IDENTICAL, so the belt-selection sync + its EditMode contract are
        // untouched. The iron variants share their stone counterpart's family haft/grip (same seat — see the seat
        // arrays below). The belt→held-mesh SYNC for the non-pickaxe iron weapons is the deferred "real equip"
        // follow-up (the wood/stone dagger/sword are [B]-only too); ③ makes them SOAKABLE in-hand via [B].
        // 86catvb6u (v4 ACTIVATION §3 — the F9 nudge session must reach ALL 15 held seats): APPEND the 5 WOOD-tier
        // tools (indices 10-14). #304 imported the wood FBX + laid them into the lineup prefab for the tier-
        // contrast capture but EXPLICITLY DEFERRED adding them to the [B] picker ("that needs per-weapon seat
        // arrays = seating scope" — WeaponPackAssetGen §WOOD) — THIS is that seating scope. Append-only: indices
        // 0-9 (the soaked stone + iron tiers) stay BYTE-IDENTICAL, so the belt-selection sync + its EditMode
        // contract (AxeFamilyIndex 0, SpearFamilyIndex 3, Pickaxe* 4/5, *Iron 6-9) are untouched. The wood row
        // shares the family haft/grip (blender-asset-pipeline family-extension route), so each wood tool seats
        // from its stone counterpart's baked seat (see WeaponMeshScale/Offset/Euler below). knife_wood is the
        // dagger_wood item (§6a). With wood in the cycle, the F9 tool's generalized HELD target (it dials whatever
        // CurrentIndex points at) reaches all 15 — the Sponsor nudges each in the dial session, then the dev bakes.
        public static readonly string[] WeaponNodeNames =
            { "wpn_axe_stone_01", "wpn_knife_stone_01", "wpn_sword_stone_01", "wpn_spear_stone_01",
              "wpn_pickaxe_stone_01", "wpn_pickaxe_iron_01",
              "wpn_axe_iron_01", "wpn_knife_iron_01", "wpn_sword_iron_01", "wpn_spear_iron_01",
              "wpn_axe_wood_01", "wpn_knife_wood_01", "wpn_sword_wood_01", "wpn_spear_wood_01", "wpn_pickaxe_wood_01" };
        public static readonly string[] WeaponLabels =
            { "AXE", "KNIFE", "SWORD", "SPEAR", "PICKAXE STONE", "PICKAXE IRON",
              "AXE IRON", "DAGGER IRON", "SWORD IRON", "SPEAR IRON",
              "AXE WOOD", "DAGGER WOOD", "SWORD WOOD", "SPEAR WOOD", "PICKAXE WOOD" };
        public const string LineupResourcePath = "WeaponSetLineup"; // Assets/Resources/WeaponSetLineup.prefab

        /// <summary>The axe's family index (0 — the Sponsor-LOCKED default seat).</summary>
        public const int AxeFamilyIndex = 0;
        /// <summary>The spear's index in <see cref="WeaponNodeNames"/> (86cahngdg — the belt-selection sync
        /// maps IsSpearSelectedInBelt to THIS index; pinned by an EditMode contract test so a family
        /// reorder cannot silently cross the held visual again).</summary>
        public const int SpearFamilyIndex = 3;
        /// <summary>The stone pickaxe's cycle index (86cam9q5f — the 5th tool type, stone tier).</summary>
        public const int PickaxeStoneFamilyIndex = 4;
        /// <summary>The iron pickaxe's cycle index (86cam9q5f — the 5th tool type, iron tier).</summary>
        public const int PickaxeIronFamilyIndex = 5;
        /// <summary>The iron AXE's cycle index (86camz9vh ③ — appended for the in-hand iron soak, Bar 5).</summary>
        public const int AxeIronFamilyIndex = 6;
        /// <summary>The iron DAGGER's cycle index (86camz9vh ③ — the wpn_knife_iron_01 mesh, "dagger" per §6a).</summary>
        public const int DaggerIronFamilyIndex = 7;
        /// <summary>The iron SWORD's cycle index (86camz9vh ③).</summary>
        public const int SwordIronFamilyIndex = 8;
        /// <summary>The iron SPEAR's cycle index (86camz9vh ③).</summary>
        public const int SpearIronFamilyIndex = 9;
        /// <summary>The wood AXE's cycle index (86catvb6u §3 — the wood tier appended for the 15-seat F9 dial session).</summary>
        public const int AxeWoodFamilyIndex = 10;
        /// <summary>The wood DAGGER's cycle index (86catvb6u §3 — the wpn_knife_wood_01 mesh, "dagger_wood" per §6a).</summary>
        public const int DaggerWoodFamilyIndex = 11;
        /// <summary>The wood SWORD's cycle index (86catvb6u §3).</summary>
        public const int SwordWoodFamilyIndex = 12;
        /// <summary>The wood SPEAR's cycle index (86catvb6u §3).</summary>
        public const int SpearWoodFamilyIndex = 13;
        /// <summary>The wood PICKAXE's cycle index (86catvb6u §3).</summary>
        public const int PickaxeWoodFamilyIndex = 14;

        /// <summary>
        /// 86cahngdg — the PURE selection -> family-index mapping the belt sync applies (extracted so the
        /// EditMode guard pins it without component lifecycle): the SELECTED belt weapon owns the held
        /// visual. Axe selected -> 0; spear -> <see cref="SpearFamilyIndex"/>; STONE pickaxe ->
        /// <see cref="PickaxeStoneFamilyIndex"/>; IRON pickaxe -> <see cref="PickaxeIronFamilyIndex"/>
        /// (I-2 86cakkmr0 — the 5th tool type is now belt-selectable, so the belt→held mesh sync must map it
        /// or the held pickaxe never shows the right mesh — the soak-fail). Neither (empty / berry / water /
        /// weapon-in-pack...) -> -1 = selection does not drive a held-weapon mesh (the HeldAxe gate hides the
        /// seat; the displayed mesh is left alone). Deterministic priority: axe > spear > pickaxe-stone >
        /// pickaxe-iron (only one belt slot is selected, so at most one flag is ever true in play; the tie-
        /// break is pinned for the contract test).
        /// </summary>
        public static int SelectionIndexFor(bool axeSelected, bool spearSelected,
                                            bool pickaxeStoneSelected, bool pickaxeIronSelected)
            => axeSelected ? AxeFamilyIndex
             : spearSelected ? SpearFamilyIndex
             : pickaxeStoneSelected ? PickaxeStoneFamilyIndex
             : pickaxeIronSelected ? PickaxeIronFamilyIndex
             : -1;

        /// <summary>
        /// 86caffwuz / 86caffwv5 soak-3 — the WOOD-tier selection → family-index map (the ADDITIVE wood sibling of
        /// <see cref="SelectionIndexFor"/>). The soak-3 blocker: a crafted WOOD weapon selected in the belt showed
        /// NOTHING in the hand because <see cref="SelectionIndexFor"/> knows only the stone/iron ids (returned -1).
        /// This maps the 5 wood ids to their family mesh indices (10-14 — appended by 86catvb6u; the seats already
        /// mirror the stone tier). Kept SEPARATE from <see cref="SelectionIndexFor"/> so the soaked stone/iron/pickaxe
        /// decision table is BYTE-UNCHANGED (no regression); the belt sync + gate compose it as a FALLBACK (stone/iron
        /// first, then wood). Only one belt slot is selected in play, so at most one flag is true; the tie-break order
        /// (axe > dagger > sword > spear > pickaxe) is pinned for the contract test. -1 = no wood weapon selected.
        /// </summary>
        public static int WoodSelectionIndexFor(bool axeWoodSelected, bool daggerWoodSelected,
                                                bool swordWoodSelected, bool spearWoodSelected,
                                                bool pickaxeWoodSelected)
            => axeWoodSelected ? AxeWoodFamilyIndex
             : daggerWoodSelected ? DaggerWoodFamilyIndex
             : swordWoodSelected ? SwordWoodFamilyIndex
             : spearWoodSelected ? SpearWoodFamilyIndex
             : pickaxeWoodSelected ? PickaxeWoodFamilyIndex
             : -1;

        /// <summary>86caffwv5 soak-3 — the wood-tier selection → family-index map read straight off an
        /// <see cref="Inventory"/> (the belt sync + the [B]-refusal both need it). Composes
        /// <see cref="WoodSelectionIndexFor"/> from the inventory's 5 wood selection predicates. -1 if the
        /// inventory is null or no wood weapon is selected.</summary>
        public static int WoodSelectionIndexFor(Inventory inv)
            => inv == null ? -1
             : WoodSelectionIndexFor(inv.IsAxeWoodSelectedInBelt, inv.IsDaggerWoodSelectedInBelt,
                                     inv.IsSwordWoodSelectedInBelt, inv.IsSpearWoodSelectedInBelt,
                                     inv.IsPickaxeWoodSelectedInBelt);

        /// <summary>
        /// 86cav8xu8 — TRUE when the SELECTED belt item owns a held-visual weapon mesh (any tier that
        /// <see cref="SyncHeldVisualToSelection"/> maps to a family index): the stone/spear/pickaxe set
        /// (<see cref="SelectionIndexFor"/> ≥ 0) OR any wood tier (<see cref="WoodSelectionIndexFor"/> ≥ 0). This is
        /// the SINGLE source of truth for "a haft is shown in the hand" — <see cref="HeldAxe.ShouldShow"/> (the mesh
        /// visibility gate) AND <see cref="CastawayFingerCurl"/> (the grip that closes around the haft) both read it,
        /// so the finger-curl can never drift from the mesh it wraps. Widens the finger-curl past the old stone-axe/
        /// spear-only read (the wood/iron/pickaxe held-visual then read as an OPEN 'mangled' hand around the haft).</summary>
        public static bool IsHeldVisualWeaponSelected(Inventory inv)
            => inv != null
               && (SelectionIndexFor(inv.IsAxeSelectedInBelt, inv.IsSpearSelectedInBelt,
                                     inv.IsPickaxeStoneSelectedInBelt, inv.IsPickaxeIronSelectedInBelt) >= 0
                   || WoodSelectionIndexFor(inv) >= 0);

        // Per-weapon mesh-holder compensation (look-soak — read proportionate to the AXE in the hand; the
        // exact precise grip is OOS, the later equip ticket). Index 0 (axe) is ALWAYS zero/identity — the axe
        // seat is Sponsor-LOCKED and is restored to its captured original.
        //
        // SOAK REFRAME (this ticket): the FIRST values {1, 0.55, 0.50, 0.42} read MUCH SMALLER than the axe in
        // the hand — the down-scale was far too aggressive. The MODELS are correctly sized (the Blender family
        // render was Sponsor-accepted); only the HELD scale was wrong. So these are BUMPED UP to read
        // proportionate to the axe's in-hand presence: a knife a solid knife, a sword sword-sized, a spear
        // long — all much closer to 1.0. The axe holds at 1.0 and looks right; match that apparent presence.
        // 86caffwuz BAKE (build 5caf1be): the Sponsor SOAKED + CONFIRMED these held scales — he dialed each
        // weapon in-hand via the unified settings console and these are the values he settled on, so they ARE
        // the approval (he left the scales at these and adjusted the OFFSETS — see WeaponMeshLocalOffset). The
        // axe holds at 1.0 (Sponsor-LOCKED). knife 0.85 / sword 0.95 / spear 0.90 are the baked committed
        // defaults; assert the committed value, not just regen ([[unity-procedural-committed-assets-go-stale]]).
        // PUBLIC so the EditMode guard pins the axe-locked-default contract (index 0 == identity) + the dialed values.
        // 86cajkk7h: these 86caffwuz-dialed seats CARRY to the STONE tier as the soak STARTING seat — the new
        // knife/sword/spear are authored at ~the same imported size (family-normalized) AND the SAME grip-origin
        // fractions as the retired meshes (measured: knife 0.17, sword 0.14 along the long axis — unchanged), so
        // the dialed offsets/scales seat them comparably. The Sponsor re-confirms + micro-dials in THIS ticket's
        // soak via the unified console's held-weapon rows; the equality-pin below is the drift regression-guard.
        // 86cam9q5f: the two pickaxe tiers (index 4/5) START from the AXE seat — unit scale. The family-
        // extension route (blender-asset-pipeline §3) built the pickaxe by keeping the stone-axe haft/grip
        // verbatim, so it shares the axe's grip origin + familyGlobalScale and seats at the axe's baseline
        // (scale 1.0) with no per-weapon compensation. The Sponsor micro-dials in-hand at the picker soak
        // (Bar 5/8 — HeldWeaponPlacement); these are the predicted starting seats to bake if he nudges them.
        // 86camz9vh (③): the 4 iron weapons (idx 6-9) START from their STONE counterpart's soaked held scale —
        // the iron variant shares the same family haft/grip + overall size (blender-asset-pipeline family-
        // extension), so it seats identically. axe_iron←axe(1.0), knife_iron←knife(0.85), sword_iron←sword(0.95),
        // spear_iron←spear(0.90). The Sponsor micro-dials in-hand at the picker soak (Bar 5 — HeldWeaponPlacement).
        // 86catvb6u §3: the 5 WOOD tools (idx 10-14) START from their STONE counterpart's soaked held scale — the
        // wood variant shares the same family haft/grip + overall size (blender-asset-pipeline family-extension),
        // so it seats identically. axe_wood←axe(1.0), knife_wood←knife(0.85), sword_wood←sword(0.95),
        // spear_wood←spear(0.90), pickaxe_wood←pickaxe(1.0). The Sponsor micro-dials each in-hand at the F9 dial session.
        // 86caffwv5 round-7 FINAL BAKE + "use the same dial for rock and metal" (Sponsor-directed, verbatim):
        // the round-6 wood dial is now the ONE seat per weapon CLASS — each class's WOOD value is applied to its
        // STONE and IRON tiers too, so all three tiers of a weapon read identically in-hand. This REPLACES every
        // previously-approved per-tier seat, INCLUDING the original stone axe's (the Sponsor's explicit call; see
        // the PR body seat-replacement flag). Per class: axe (0/6/10) scale 1.0; dagger/knife (1/7/11) 0.771;
        // sword (2/8/12) 0.950; spear (3/9/13) 0.900; pickaxe (4/5/14) 1.0. Values harvested from + verified
        // against Build/soak-swings-6/sponsor-final-dial-Player.log (last-logged nudge line per class, Danish-locale
        // decimals). DAGGER unchanged (soak-5 provisional IS final — Sponsor left it untouched).
        public static readonly float[] WeaponMeshScale =
            { 1f, 0.771f, 0.95f, 0.90f, 1f, 1f, 1f, 0.771f, 0.95f, 0.90f, 1f, 0.771f, 0.95f, 0.90f, 1f };
        // Local-space drop applied to the mesh-holder child for the non-axe weapons (their origin is the grip
        // BASE, so they need pulling back along the blade axis to sit the grip in the palm). Axe = zero.
        // 86caffwuz BAKE (build 5caf1be): these are the Sponsor's DIALED in-hand offsets — he soaked + nudged
        // each weapon into place via the unified settings console's 7 held-weapon rows, and these committed
        // numbers ARE the approval ([[verify-soak-builds-or-bake-and-judge]] — bake the dialed values, assert
        // the committed on-disk constant). The earlier {-0.34/-0.80/-1.50} z-only pull-backs were FIRST-GUESS
        // look-soak seats; the Sponsor re-seated each by eye (small Y drop into the palm + a slight X for the
        // sword/spear). The axe stays zero (Sponsor-LOCKED seat, bar #6 — don't regress a praised grip).
        // 86cakkfz9 v3 DIAL-IN BAKE (dial exe stamp d306552, F9 WEAPON NUDGE TOOL generalized HELD target): the
        // Sponsor re-positioned each non-axe weapon in-hand on the v3 build and asked to bake the dialed offsets.
        // Recovered from Player-prev.log final resting lines (Danish-locale decimals) + cross-checked against the
        // ticket screenshot table (identical): knife (-0.020,0.020,0.000), sword (-0.020,0.040,0.000), spear
        // (-0.020,0.560,0.000). Only the OFFSETS changed this round — the held SCALES (0.85/0.95/0.90) + the
        // zero eulers he dialed match the prior 86caffwuz bake, so those are unchanged. SUPERSEDES the 5caf1be
        // offsets ({0,-0.100,-0.020}/{-0.020,-0.120,0}/{-0.020,-0.120,0}). Axe = zero (Sponsor-LOCKED seat).
        // 86caffwv5 round-7 FINAL BAKE + "same dial for rock and metal": each class's WOOD offset applied to all
        // three tiers (verified against Build/soak-swings-6/sponsor-final-dial-Player.log). The AXE (0/6/10) is NO
        // LONGER a zero-locked seat — the Sponsor RETIRED the axe lock and dialed a real in-hand seat for the whole
        // axe class (see the PR body seat-replacement flag + the ApplyCurrent index-0 change that composes it).
        public static readonly Vector3[] WeaponMeshLocalOffset =
        {
            new Vector3(-0.020f, 0.000f, 0.000f),    // axe stone     (86caffwv5 final — axe class dial)
            new Vector3(-0.020f, -0.020f, 0.080f),   // knife/dagger  (86caffwv5 final — dagger class dial)
            new Vector3(0.020f, -0.040f, 0.080f),    // sword         (86caffwv5 final — sword class dial)
            new Vector3(-0.140f, 0.100f, -0.220f),   // spear         (86caffwv5 final — spear class dial)
            new Vector3(-0.020f, 0.020f, -0.020f),   // pickaxe stone (86caffwv5 final — pickaxe class dial)
            new Vector3(-0.020f, 0.020f, -0.020f),   // pickaxe iron  ← pickaxe (same class dial)
            new Vector3(-0.020f, 0.000f, 0.000f),    // axe iron      ← axe    (same class dial)
            new Vector3(-0.020f, -0.020f, 0.080f),   // dagger iron   ← dagger
            new Vector3(0.020f, -0.040f, 0.080f),    // sword iron    ← sword
            new Vector3(-0.140f, 0.100f, -0.220f),   // spear iron    ← spear
            new Vector3(-0.020f, 0.000f, 0.000f),    // axe wood      (Sponsor round-6 dial — the class master)
            new Vector3(-0.020f, -0.020f, 0.080f),   // dagger wood   (soak-5 provisional IS final — Sponsor left it)
            new Vector3(0.020f, -0.040f, 0.080f),    // sword wood    (Sponsor round-6 dial)
            new Vector3(-0.140f, 0.100f, -0.220f),   // spear wood    (Sponsor round-6 dial)
            new Vector3(-0.020f, 0.020f, -0.020f),   // pickaxe wood  (Sponsor round-6 dial)
        };
        // Per-weapon mesh-holder LOCAL-euler offset (86cabh907 soak round 2 — the F9 nudge tool was AXE-ONLY;
        // the Sponsor could not angle the knife/sword/spear in-hand). The non-axe weapons seat on the SAME
        // shared seat the axe uses; this is a per-weapon rotation tweak composed ON TOP of the seat so each
        // sits at a believable in-hand angle. Axe = zero/identity (the axe's hold is the shared-seat baseline,
        // dialed via the F9 held target's rig fields — not here). First-guess identity for all; the F9 tool's
        // generalized HELD target dials each weapon's offset+euler+scale by eye and the Sponsor reads the
        // values to bake here.
        // 86caffwv5 round-7 FINAL BAKE + "same dial for rock and metal": each class's WOOD euler applied to all three
        // tiers (verified against Build/soak-swings-6/sponsor-final-dial-Player.log). The AXE euler is NO LONGER zero
        // — the Sponsor dialed a real axe-class hold this round; ApplyCurrent now composes index 0's euler too (the
        // lock is retired). Pickaxe carries the Sponsor's round-6 gimbal-free crosswise-head dial (supersedes the old
        // 8,10,0 STARTING point).
        public static readonly Vector3[] WeaponMeshLocalEuler =
        {
            new Vector3(-79.4f, 58.6f, -31.3f),   // axe stone     (86caffwv5 final — axe class dial)
            new Vector3(-70.0f, 20.0f, 0.0f),     // knife/dagger  (86caffwv5 final — dagger class dial)
            new Vector3(-59.5f, 51.9f, -11.3f),   // sword         (86caffwv5 final — sword class dial)
            new Vector3(-41.6f, -41.9f, 60.8f),   // spear         (86caffwv5 final — spear class dial)
            new Vector3(-62.0f, 50.0f, -30.7f),   // pickaxe stone (86caffwv5 final — pickaxe class dial)
            new Vector3(-62.0f, 50.0f, -30.7f),   // pickaxe iron  ← pickaxe
            new Vector3(-79.4f, 58.6f, -31.3f),   // axe iron      ← axe
            new Vector3(-70.0f, 20.0f, 0.0f),     // dagger iron   ← dagger
            new Vector3(-59.5f, 51.9f, -11.3f),   // sword iron    ← sword
            new Vector3(-41.6f, -41.9f, 60.8f),   // spear iron    ← spear
            new Vector3(-79.4f, 58.6f, -31.3f),   // axe wood      (Sponsor round-6 dial — the class master)
            new Vector3(-70.0f, 20.0f, 0.0f),     // dagger wood   (soak-5 provisional IS final — Sponsor left it)
            new Vector3(-59.5f, 51.9f, -11.3f),   // sword wood    (Sponsor round-6 dial)
            new Vector3(-41.6f, -41.9f, 60.8f),   // spear wood    (Sponsor round-6 dial — gimbal-free reach)
            new Vector3(-62.0f, 50.0f, -30.7f),   // pickaxe wood  (Sponsor round-6 dial — gimbal-free crosswise head)
        };

        private MeshFilter _meshHolder;     // the child MeshFilter on HeroAxe (the FBX mesh node)
        private Mesh[] _meshes;             // resolved family meshes, indexed to match WeaponNodeNames
        private Mesh _axeOriginalMesh;      // the shipped axe mesh — restored when cycling back to the axe
        private Vector3 _holderOrigPos;     // captured original mesh-holder local TRS (axe = LOCKED default)
        private Quaternion _holderOrigRot;
        private Vector3 _holderOrigScale;
        private int _index;                // 0 = axe (default), 1 = knife, 2 = sword, 3 = spear
        private bool _resolved;
        private GUIStyle _labelStyle, _keyStyle;

        // 86cahngdg — the belt-selection sync state. _inventory is resolved lazily (the serialized wiring
        // lives on the sibling HeldTool gate; no NEW serialized field, so the committed Boot.unity needs no
        // regen — [[unity-procedural-committed-assets-go-stale]]). _debugView marks an empty-handed [B]
        // look-soak view (knife/sword/...) the HeldAxe gate shows; cleared on any inventory change.
        private Inventory _inventory;
        private bool _inventoryResolved;
        private bool _debugView;
        private HeldTool _gateTool;         // the sibling visibility gate on this seat (lazily resolved; see ResolveGate)

        /// <summary>True while the [B] debug cycle is showing a weapon WITHOUT a held-visual weapon being
        /// selected on the belt (the empty-handed knife/sword look-soak aid). The <see cref="HeldAxe"/>
        /// visibility gate ORs this in; any inventory change clears it (selection re-asserts).</summary>
        public bool DebugViewActive => _debugView;

        // LIVE per-weapon scale — seeded from the baked WeaponMeshScale defaults, then mutated by the live dial
        // ([ ] / - =) so the Sponsor can dial the CURRENT weapon's in-hand size by eye. Index 0 (axe) is never
        // changed by the SCALE dial (the axe's uniform size is locked); its HEAD is dialed separately below.
        // The HUD/log surface the live value to bake.
        private float[] _liveScale;
        // LIVE per-weapon mesh-holder offset + euler — seeded from the baked WeaponMeshLocalOffset/Euler, then
        // mutated by the F9 AxeNudgeTool's generalized HELD target (86cabh907 soak round 2) so the Sponsor
        // positions+angles each weapon in-hand. Index 0 (axe) stays at zero (its hold is the shared-seat rig
        // baseline). The F9 tool reads/writes these via the public accessors below.
        private Vector3[] _liveOffset;
        private Vector3[] _liveEuler;

        // 86cakkfz9: the LIVE axe HEAD-SIZE dial state (per-instance clone + head-vertex indices + junction
        // pivot) is REMOVED — the axe head is authored Blender geometry now; runtime vertex-scaling it distorts
        // the knapped biface (the rejected "chipping"). Head SIZE is a Blender re-author. See the O/I key note.

        /// <summary>The currently-held weapon index (0=axe,1=knife,2=sword,3=spear) — read by the F9 tool so
        /// its generalized HELD target dials whichever weapon is shown.</summary>
        public int CurrentIndex => _index;
        /// <summary>Label of the currently-held weapon (AXE/KNIFE/SWORD/SPEAR) — for the F9 tool's panel.</summary>
        public string CurrentLabel => WeaponLabels[Mathf.Clamp(_index, 0, WeaponLabels.Length - 1)];
        /// <summary>Live per-weapon mesh-holder offset for the F9 tool to read (the bake value).</summary>
        public Vector3 CurrentOffset => _liveOffset != null ? _liveOffset[_index] : WeaponMeshLocalOffset[_index];
        /// <summary>Live per-weapon mesh-holder euler for the F9 tool to read (the bake value).</summary>
        public Vector3 CurrentEuler => _liveEuler != null ? _liveEuler[_index] : WeaponMeshLocalEuler[_index];
        /// <summary>Live per-weapon held scale for the F9 tool to read (the bake value).</summary>
        public float CurrentScale => _liveScale != null ? _liveScale[_index] : WeaponMeshScale[_index];

        /// <summary>The MeshFilter the cycle drives (the WeaponMeshHolder child, post-#100 re-home) — exposed
        /// so the belt-selection sync + the [B] cycle swap the family meshes on the SAME holder. Null until Awake.</summary>
        public MeshFilter MeshHolder => _meshHolder;
        /// <summary>The captured original (shipped) axe mesh — index-0 baseline; restored when cycling back to
        /// the axe as the "current" source-of-truth for index 0.</summary>
        public Mesh AxeOriginalMesh => _axeOriginalMesh;
        /// <summary>True while the AXE (index 0) is the currently-displayed weapon — the length picker only acts
        /// on the axe (knife/sword/spear have no shaft-length variants).</summary>
        public bool IsAxeHeld => _index == 0;

        /// <summary>
        /// F9-tool entry point (86cabh907 soak round 2): nudge the CURRENTLY-HELD weapon's in-hand placement —
        /// mesh-holder offset (dp) + euler (dr) + a multiplicative scale factor (scaleFactor; 1 = no change).
        /// The AXE (index 0) routes scale/offset/euler nudges to NOTHING here (its hold is the shared-seat rig
        /// baseline the F9 tool nudges directly on the HeldAxeRig); for the axe this method only re-applies. For
        /// knife/sword/spear it edits the live per-weapon arrays + re-seats the mesh-holder immediately so the
        /// dial shows this frame. Returns true if a non-axe weapon was actually edited.
        /// </summary>
        public bool NudgeCurrentWeapon(Vector3 dp, Vector3 dr, float scaleFactor)
        {
            if (_index == 0) return false; // axe hold = shared-seat rig (F9 tool nudges HeldAxeRig directly)
            if (!_resolved) ResolveMeshes();
            _liveOffset[_index] += dp;
            _liveEuler[_index] += dr;
            if (!Mathf.Approximately(scaleFactor, 1f))
                _liveScale[_index] = Mathf.Max(0.01f, _liveScale[_index] * scaleFactor);
            ApplyCurrent();
            return true;
        }

        // 86cakkfz9: the axe HEAD-size API (HeadFactorMin/Max, DialAxeHead, SetAxeHeadFactor) is REMOVED —
        // head SIZE is authored Blender geometry now, not a runtime dial (see the O/I key note above). The F9
        // AxeNudgeTool's AXE-HEAD target + its mouse slider are removed in the same change.

        private void Awake()
        {
            // No GUILayout.* in this OnGUI (explicit Rects only) — skip IMGUI's Layout event pass (86cahhfp4 C2a).
            // FIRST line: the mesh-resolve below can early-return, and the layout opt-out must always apply.
            useGUILayout = false;

            // Seed the LIVE scale/offset/euler from the baked defaults (copy — never mutate the static arrays).
            // The dials edit these per-weapon; index 0 (axe) is seeded like every other class from its baked
            // defaults — the round-7 "same dial for rock and metal" bake RETIRED the axe zero-lock, so index 0 now
            // carries a real dialed axe-class seat (86cav8xu8 r7 NIT 3; DECISIONS 2026-07-22 one-dial-per-class).
            _liveScale = (float[])WeaponMeshScale.Clone();
            _liveOffset = (Vector3[])WeaponMeshLocalOffset.Clone();
            _liveEuler = (Vector3[])WeaponMeshLocalEuler.Clone();

            // Find the imported FBX mesh node (the MeshFilter the cycle drives).
            var fbxMesh = GetComponentInChildren<MeshFilter>(true);
            if (fbxMesh == null)
            {
                Debug.LogWarning("[HeldWeaponCycleDebug] no MeshFilter under HeroAxe — cannot swap held weapon meshes");
                return;
            }

            // #100 BUG-2 FIX (the empirical root cause — diagnose-via-trace, EditMode hierarchy probe on the
            // fresh ab16bbb Boot scene): the in-house axe FBX is a SINGLE-node FBX with preserveHierarchy:0, so
            // Unity COLLAPSES the mesh node onto the imported ROOT — the MeshFilter lands on the HeroAxe object
            // ITSELF, the SAME transform HeldToolRig.LateUpdate (DefaultExecutionOrder 100) overwrites every
            // frame (transform.position/rotation = hand-seat). The previous code wrote the per-weapon
            // offset/euler onto THAT transform, so the rig STOMPED them next frame → the F9 nudge "did nothing"
            // for knife/sword/spear (only localScale survived, since the rig leaves scale alone — which is why
            // the [ ] scale dial worked but offset/euler didn't). Fix: the displayed mesh lives on a dedicated
            // CHILD "WeaponMeshHolder" the rig never touches, and the per-weapon TRS is driven there.
            //
            // 86cabh907 FINAL bake: the holder is now AUTHORED AT EDIT-TIME (MovementCameraScene.EnsureWeaponMesh
            // Holder), carrying the LOWER-THIRD grip shift (HeldAxeGripShiftY) so it SERIALIZES into Boot.unity
            // (static EditMode bounds == runtime). So on the shipped scene the MeshFilter is ALREADY on the child
            // holder → the `else` branch below captures THAT holder (with its authored grip offset) as the axe's
            // locked baseline. The re-home branch is kept as a FALLBACK for any scene where the mesh is still on
            // the rig-driven root (e.g. a stale/old Boot.unity before this bake) — it builds the holder at
            // IDENTITY (no grip shift) so an un-migrated scene still cycles, just without the new lower-third seat.
            if (fbxMesh.transform.GetComponent<HeldToolRig>() != null)
            {
                // The mesh is on the rig-driven root — split it onto a child holder.
                var rootMr = fbxMesh.GetComponent<MeshRenderer>();
                var holderGo = new GameObject("WeaponMeshHolder");
                holderGo.transform.SetParent(fbxMesh.transform, false); // identity local TRS under the root
                var holderMf = holderGo.AddComponent<MeshFilter>();
                holderMf.sharedMesh = fbxMesh.sharedMesh;
                var holderMr = holderGo.AddComponent<MeshRenderer>();
                if (rootMr != null)
                {
                    holderMr.sharedMaterials = rootMr.sharedMaterials;
                    holderMr.shadowCastingMode = rootMr.shadowCastingMode;
                    holderMr.receiveShadows = rootMr.receiveShadows;
                    // Remove the ROOT renderer/filter so only the child holder draws (one mesh on screen, not
                    // two). Destroying them (vs disabling) keeps the HeldTool visibility gate's renderer cache
                    // clean — it caches the SUBTREE, and the child holder's renderer is the only one left.
                    Destroy(rootMr);
                    var rootMf = fbxMesh; // == the root MeshFilter (fbxMesh.transform is the rig root)
                    Destroy(rootMf);
                }
                _meshHolder = holderMf;
                // The displayed renderer moved to a NEW child the visibility gate may have cached BEFORE this
                // re-home (Awake order is not guaranteed). Make the gate re-scan so it owns the child holder's
                // renderer (the held axe still hides until selected; shows on craft/pickup — #100 must NOT
                // leave the axe stuck visible/invisible). Both HeldAxe + StumpAxe are siblings on other objects.
                var gate = GetComponent<HeldTool>();
                if (gate != null) gate.RefreshRenderers();
            }
            else
            {
                // The mesh is already on a non-rig child (a multi-node FBX) — drive it directly.
                _meshHolder = fbxMesh;
            }

            // Capture the holder's ORIGINAL local TRS — the baseline the per-weapon seat (incl. the axe class,
            // 86caffwv5 round-7) composes ON TOP of (identity for the re-homed-child case; the FBX node's local
            // TRS otherwise).
            _axeOriginalMesh = _meshHolder.sharedMesh;
            var ht = _meshHolder.transform;
            _holderOrigPos = ht.localPosition;
            _holderOrigRot = ht.localRotation;
            _holderOrigScale = ht.localScale;

            // 86caffwv5 round-7 — SEAT THE AXE (index 0) AT SPAWN so the equipped stone axe reflects the Sponsor's
            // axe-class dial from the first frame. SyncHeldVisualToSelection skips ApplyCurrent when the selection
            // already matches _index (desired==0 at spawn), so without this call the array[0] seat would only apply
            // after cycling away and back. When array[0] is zero/identity/1 (the old locked default) this is a no-op
            // (byte-identical to the plain baseline). Uses _axeOriginalMesh (already captured) — _meshes is resolved
            // lazily and the axe never depends on it.
            ApplyCurrent();
        }

        // 86cahngdg — resolve the Inventory WITHOUT a new serialized field (the committed Boot.unity carries
        // this component already; adding a field would deserialize null there and invite a scene regen). The
        // sibling HeldTool gate on this seat object carries the editor-time-wired inventory; fall back to the
        // scene singleton (the project idiom every held/pickup component uses).
        private Inventory ResolveInventory()
        {
            if (!_inventoryResolved)
            {
                _inventoryResolved = true;
                var gate = ResolveGate();
                _inventory = (gate != null && gate.inventory != null)
                    ? gate.inventory
                    : FindObjectOfType<Inventory>();
            }
            return _inventory;
        }

        private HeldTool ResolveGate()
        {
            // 86cajt6jz — re-resolve while null; do NOT permanently cache a null gate. This component's
            // OnEnable can run BEFORE the sibling HeldTool gate exists when the two are AddComponent'd
            // sequentially (AddComponent on an active GO runs Awake+OnEnable synchronously, so the cycle
            // resolves the gate before the gate component is even added — the PlayMode rig; also any runtime
            // AddComponent path). Caching that null would leave the empty-handed [B] debug cycle unable to
            // re-apply the visibility gate FOREVER (CycleHeldWeaponDebug's gateTool.RefreshRenderers is
            // skipped), so the debug view never shows through the gate. In the SHIPPED scene both components
            // deserialize together (all Awakes run before all OnEnables), so GetComponent finds the gate on
            // the first call — behaviour is UNCHANGED there; this only hardens the add-order-dependent path.
            // GetComponent re-runs only while still null, then the reference sticks.
            if (_gateTool == null) _gateTool = GetComponent<HeldTool>();
            return _gateTool;
        }

        private void OnEnable()
        {
            var inv = ResolveInventory();
            if (inv != null) inv.Changed += SyncHeldVisualToSelection;
            SyncHeldVisualToSelection(); // correct at spawn/enable (no polling)
        }

        private void OnDisable()
        {
            if (_inventory != null) _inventory.Changed -= SyncHeldVisualToSelection;
        }

        /// <summary>
        /// 86cahngdg — the belt-selection -> held-visual SYNC (the soak-224 crossed-visual fix). Fired on
        /// every <see cref="Inventory.Changed"/> (pickup / select / move / consume): maps the SELECTED belt
        /// weapon to its family index (<see cref="SelectionIndexFor"/>) and, when a held-visual weapon is
        /// selected, swaps the displayed mesh to it via the SAME ResolveMeshes/ApplyCurrent path [B] uses —
        /// so the mesh in the hand ALWAYS matches the selected weapon. Any selection change also CLEARS the
        /// empty-handed [B] debug view. Ends by re-poking the sibling HeldTool gate: both this handler and
        /// the gate's own Apply subscribe to Inventory.Changed with UNDEFINED relative order, so the gate
        /// could otherwise apply visibility against the PRE-sync state (a stale DebugViewActive) — the
        /// RefreshRenderers re-apply makes the end-of-change state deterministic regardless of handler order.
        /// Public so the shipped-build -verifyHeldBelt gate and the PlayMode regression drive the REAL path.
        /// </summary>
        public void SyncHeldVisualToSelection()
        {
            var inv = ResolveInventory();
            if (inv == null || _meshHolder == null) return;

            int desired = SelectionIndexFor(inv.IsAxeSelectedInBelt, inv.IsSpearSelectedInBelt,
                                            inv.IsPickaxeStoneSelectedInBelt, inv.IsPickaxeIronSelectedInBelt);
            // 86caffwv5 soak-3 — ADDITIVE wood fallback: a crafted WOOD weapon selected in the belt used to return
            // -1 here (SelectionIndexFor knows only stone/iron) → the seat stayed EMPTY (the Sponsor's blocker). The
            // stone/iron path above is byte-unchanged; wood only fills the previously-empty case.
            if (desired < 0) desired = WoodSelectionIndexFor(inv);
            if (desired >= 0)
            {
                _debugView = false; // selection owns the held visual
                if (desired != _index)
                {
                    if (!_resolved) ResolveMeshes();
                    _index = desired;
                    ApplyCurrent();
                    Debug.Log("[HeldWeaponCycleDebug] belt selection -> held visual " + WeaponLabels[_index] +
                              " (" + WeaponNodeNames[_index] + ")");
                }
            }
            else if (_debugView)
            {
                // No held-visual weapon selected any more — an inventory change clears the [B] debug view
                // (the gate hides the seat; the mesh is left for the next [B]/selection to drive).
                _debugView = false;
            }

            // Deterministic end state regardless of Changed-handler order (see summary).
            var gateTool = ResolveGate();
            if (gateTool != null) gateTool.RefreshRenderers();
        }

        /// <summary>
        /// 86cahngdg — the [B] debug cycle, extracted so tests + the -verifyHeldBelt gate can drive the REAL
        /// key path. REFUSED (returns false, logs why) while a held-visual weapon (axe/spear) is the selected
        /// belt item — selection owns the visual, so the debug handle can never re-create the soak-224
        /// crossed state (spear mesh shown while the axe is selected). With NO held-visual weapon selected it
        /// cycles as before and marks <see cref="DebugViewActive"/> so the gate shows the look-soak view.
        /// </summary>
        public bool CycleHeldWeaponDebug()
        {
            if (_meshHolder == null) return false; // Awake found no MeshFilter — nothing to cycle
            var inv = ResolveInventory();
            if (inv != null && (SelectionIndexFor(inv.IsAxeSelectedInBelt, inv.IsSpearSelectedInBelt,
                                                  inv.IsPickaxeStoneSelectedInBelt, inv.IsPickaxeIronSelectedInBelt) >= 0
                                || WoodSelectionIndexFor(inv) >= 0)) // 86caffwv5 soak-3 — wood selection owns the visual too
            {
                Debug.Log("[HeldWeaponCycleDebug] [" + cycleKey + "] cycle REFUSED — the selected belt weapon " +
                          "owns the held visual (86cahngdg). Select an empty/non-weapon belt slot to use the " +
                          "debug look-soak cycle.");
                return false;
            }
            if (!_resolved) ResolveMeshes();
            _index = (_index + 1) % WeaponNodeNames.Length;
            _debugView = true;
            ApplyCurrent();
            var gateTool = ResolveGate();
            if (gateTool != null) gateTool.RefreshRenderers(); // show the debug view through the gate
            Debug.Log("[HeldWeaponCycleDebug] held weapon -> " + WeaponLabels[_index] +
                      " (" + WeaponNodeNames[_index] + ")  [DEBUG cycle, key=" + cycleKey + "]");
            return true;
        }

        /// <summary>
        /// 86cam9q5f — CAPTURE-ONLY: force the displayed held mesh to a specific family index, mirroring the
        /// [B] cycle's swap WITHOUT the belt-selection refusal. Used by the shipped-build -verifyHeldPickaxe
        /// gate to seat each pickaxe tier (index <see cref="PickaxeStoneFamilyIndex"/> /
        /// <see cref="PickaxeIronFamilyIndex"/>) in the hand for the Bar-5 in-hand capture — the pickaxe has
        /// no belt pickup yet (belt/crafting is I-2+), so the picker is the only in-hand view. Resolves the
        /// family meshes if needed, sets the index, applies the per-weapon seat, marks the debug view + pokes
        /// the sibling HeldTool gate so the seat shows through it. NOT a gameplay path (only a -verify flag
        /// calls it); instance state only, so no SubsystemRegistration reset is needed.
        /// </summary>
        public void ShowWeaponForCaptureDebug(int index)
        {
            if (_meshHolder == null) return;
            if (!_resolved) ResolveMeshes();
            _index = Mathf.Clamp(index, 0, WeaponNodeNames.Length - 1);
            _debugView = true;
            ApplyCurrent();
            var gateTool = ResolveGate();
            if (gateTool != null) gateTool.RefreshRenderers();
            Debug.Log("[HeldWeaponCycleDebug] capture view -> " + WeaponLabels[_index] +
                      " (" + WeaponNodeNames[_index] + ")  [86cam9q5f -verifyHeldPickaxe]");
        }

        // Resolve the family meshes from the lineup prefab lazily (the first cycle), so a soak that never
        // presses the key pays nothing and the axe never depends on the lineup prefab being present.
        private void ResolveMeshes()
        {
            _resolved = true;
            _meshes = new Mesh[WeaponNodeNames.Length];
            _meshes[0] = _axeOriginalMesh; // the shipped held-axe mesh is the source of truth for index 0

            var prefab = Resources.Load<GameObject>(LineupResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[HeldWeaponCycleDebug] Resources/" + LineupResourcePath +
                                 " missing — knife/sword/spear cannot be resolved; only the axe will show");
                return;
            }
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                // Match the family node by name (the lineup child names = FBX names). Skip the axe (index 0
                // is the SHIPPED held mesh, not the lineup's un-normalized axe — they read identically, but
                // the shipped one is the Sponsor-locked source).
                for (int i = 1; i < WeaponNodeNames.Length; i++)
                    if (mf.name == WeaponNodeNames[i]) _meshes[i] = mf.sharedMesh;
            }
            for (int i = 1; i < _meshes.Length; i++)
                if (_meshes[i] == null)
                    Debug.LogWarning("[HeldWeaponCycleDebug] lineup prefab missing mesh node '" + WeaponNodeNames[i] + "'");
        }

        private void Update()
        {
            if (_meshHolder == null) return;

            // [B] — cycle the held weapon (86cahngdg: refused while a held-visual weapon is selected — the
            // belt selection owns the visual; see CycleHeldWeaponDebug).
            if (Input.GetKeyDown(cycleKey))
            {
                CycleHeldWeaponDebug();
                return;
            }

            // LIVE OVERALL-HELD-SCALE DIAL ([ ] / - = US-punct; O / I Danish-safe letters) — scale the CURRENT
            // held weapon's mesh-holder up/down in ~5% steps so the Sponsor dials its in-hand size by eye +
            // reads the value to bake. REFUSES the axe (Sponsor-LOCKED): on index 0 the dial only logs that the
            // axe is locked (use the settings-console HeldScale row for the axe), never changes the seat.
            bool up = Input.GetKeyDown(scaleUpKey) || Input.GetKeyDown(scaleUpKeyAlt) || Input.GetKeyDown(scaleUpKeyDanish);
            bool down = Input.GetKeyDown(scaleDownKey) || Input.GetKeyDown(scaleDownKeyAlt) || Input.GetKeyDown(scaleDownKeyDanish);
            if (up || down)
            {
                if (_index == 0)
                {
                    Debug.Log("[HeldWeaponCycleDebug] AXE seat is Sponsor-LOCKED — held-scale dial refused. " +
                              "Cycle [" + cycleKey + "] to a knife/sword/spear to dial its held size, or use " +
                              "the settings-console HeldScale row for the axe.");
                    return;
                }
                if (!_resolved) ResolveMeshes(); // dialing before any cycle still resolves + applies cleanly
                float factor = up ? scaleStep : 1f / scaleStep;
                _liveScale[_index] = Mathf.Max(0.01f, _liveScale[_index] * factor);
                ApplyCurrent();
                Debug.Log("[HeldWeaponCycleDebug] " + WeaponLabels[_index] + " held scale -> " +
                          _liveScale[_index].ToString("F3") + "  (bake into WeaponMeshScale[" + _index + "])");
                return;
            }
        }

        // Swap the displayed mesh + apply the per-weapon mesh-holder compensation. The axe (index 0) restores the
        // captured original axe MESH, then COMPOSES the axe-class seat (WeaponMeshLocalOffset/Euler/Scale[0]) on it —
        // the SAME composition every other weapon uses (the round-7 "same dial for rock and metal" bake retired the
        // axe zero-lock, so index 0 is a real dialed seat, no longer byte-unchanged — 86cav8xu8 r7 NIT 3).
        private void ApplyCurrent()
        {
            Mesh m = (_meshes != null && _index < _meshes.Length) ? _meshes[_index] : null;
            if (m == null) m = _axeOriginalMesh; // fall back to the axe if a family mesh failed to resolve

            var t = _meshHolder.transform;
            if (_index == 0)
            {
                // AXE (86caffwv5 round-7 — the "same dial for rock and metal" retires the axe LOCK): restore the
                // shipped authored axe MESH, then COMPOSE the axe-class seat (WeaponMeshLocalOffset/Euler/Scale[0])
                // on the captured baseline — the SAME composition the other weapons use. Backward-compatible: when
                // the array[0] was zero/identity/1 (the old locked default) this was byte-identical to the plain
                // restore; the Sponsor's round-6 axe dial makes it a real seat now. (86cakkfz9: the runtime head-dial
                // clone is gone — head SIZE is authored Blender geometry now, not a runtime vertex deform.)
                _meshHolder.sharedMesh = _axeOriginalMesh;
                t.localPosition = _holderOrigPos + _liveOffset[0];
                t.localRotation = _holderOrigRot * Quaternion.Euler(_liveEuler[0]);
                t.localScale = _holderOrigScale * _liveScale[0];
            }
            else
            {
                // knife / sword / spear — look-soak seat on the SHARED seat. Compose the per-weapon LIVE
                // offset/euler/scale ON TOP of the axe's captured baseline so the weapon rides the same seat
                // the axe does, just nudged to seat + angle reasonably in the hand. All three come from the
                // LIVE arrays (seeded from the WeaponMeshLocal* defaults) so the F9 dial shows immediately.
                _meshHolder.sharedMesh = m;
                t.localPosition = _holderOrigPos + _liveOffset[_index];
                t.localRotation = _holderOrigRot * Quaternion.Euler(_liveEuler[_index]);
                t.localScale = _holderOrigScale * _liveScale[_index];
            }
        }

        // 86cakkfz9: ResolveAxeHead / ApplyAxeHead (the runtime axe-head vertex-scale dial) are REMOVED — head
        // SIZE is authored Blender geometry now; vertex-scaling the knapped biface is the rejected "chipping".

        private void OnGUI()
        {
            // Overlay master gate (86cafd6d6): the dev/debug overlay layer is HIDDEN by default — a normal launch /
            // soak / CI capture shows a clean screen (this also un-buries the #158 loot prompt this bottom-center
            // panel was burying). F10 (DebugOverlayMaster, the single overlay master) reveals it.
            if (!DebugOverlays.Visible) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                _labelStyle.normal.textColor = new Color(1f, 0.85f, 0.45f); // warm-gold, matches the nudge-tool header
                _keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _keyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            }

            // Bottom-CENTER, clear of SurvivalHud's bottom-left hotbar + the AxeNudgeTool's right panel.
            // Taller now: carries the live scale/head read-out + both dial-key hints (soak round 2).
            float w = 470f, h = 82f;
            float x = Mathf.Max(8f, (Screen.width - w) * 0.5f);
            float y = Screen.height - h - 10f;
            var panel = new Rect(x, y, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Line 1: which weapon + its LIVE held-scale read-out (the bake number). The AXE is Sponsor-LOCKED
            // (scale 1.000); knife/sword/spear show their dialed held scale.
            string readOut = _index == 0
                ? "scale 1.000 LOCKED"
                : "scale " + (_liveScale != null ? _liveScale[_index].ToString("F3") : WeaponMeshScale[_index].ToString("F3"));
            GUI.Label(new Rect(x + 10f, y + 5f, w - 20f, 20f),
                "DEBUG — held weapon: " + WeaponLabels[_index] + "   " + readOut, _labelStyle);
            // Line 2: the cycle key. Line 3: overall held-scale dial (US-punct + Danish-safe letters).
            GUI.Label(new Rect(x + 10f, y + 24f, w - 20f, 18f),
                "[" + cycleKey + "] debug cycle (refused while a belt weapon is selected — selection owns the visual)", _keyStyle);
            GUI.Label(new Rect(x + 10f, y + 42f, w - 20f, 18f),
                "[O] / [I] bigger/smaller held scale (Danish-safe; = [ ]/[=] / [[]/[-])  — knife/sword/spear; axe LOCKED", _keyStyle);
            GUI.Label(new Rect(x + 10f, y + 60f, w - 20f, 18f),
                "axe size: settings-console HeldScale row (head SIZE is a Blender re-author, not a runtime dial)", _keyStyle);
        }
    }
}
