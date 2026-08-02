using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode regression for the 86cahngdg soak-224 crossed-visual defect — the FULL component seam:
    /// Inventory.Changed -> HeldWeaponCycleDebug.SyncHeldVisualToSelection (mesh) + HeldAxe.Apply
    /// (visibility) on ONE shared seat. The defect: the gate fired on IsAxeSelectedInBelt while the
    /// DISPLAYED mesh stayed whatever [B] last cycled — axe selected rendered the SPEAR (stale mesh, gate
    /// on), spear selected rendered EMPTY hands (no spear predicate). Sibling of
    /// InventoryBeltHeldAxePlayModeTests (the axe-only AC4 table, still green unchanged); this suite adds
    /// the two-weapon table across BOTH pickup orders (AC2) + the [B]-debug-cycle landmine.
    ///
    /// Asserts the PERCEPT pair after EACH transition: Renderer.enabled (visibility) AND the holder's
    /// sharedMesh identity (WHICH weapon) — the crossed defect was exactly a true renderer with a wrong
    /// mesh, so a renderer-only assert would false-green it. The spear mesh resolves from the committed
    /// Resources/WeaponSetLineup.prefab (the same source the shipped sync uses).
    /// </summary>
    // 86cajt6jz (FH-PMTRIAGE-DEBUGCYCLE) — HEADLESS RED RESOLVED. DebugCycle_… failed headless at the
    // "debug view SHOWS through the gate" assert (line ~154) because HeldWeaponCycleDebug.ResolveGate
    // PERMANENTLY cached a NULL HeldTool: the cycle is AddComponent'd BEFORE the sibling HeldAxe gate in
    // SetUp, and AddComponent on an active GO runs OnEnable synchronously, so the cycle's OnEnable->
    // ResolveGate ran GetComponent<HeldTool>() while the gate did not yet exist. With the gate null-cached,
    // CycleHeldWeaponDebug() could never call gateTool.RefreshRenderers(), so the empty-handed [B] look-soak
    // view never re-applied visibility and the renderer stayed disabled. NOT a mesh-resolve bug
    // (renderer.enabled is written ONLY by HeldTool.Apply — ResolveMeshes/ApplyCurrent never touch it) and
    // NOT a timing/Time.captureDeltaTime window (there is none in this transition-only test). Fix:
    // HeldWeaponCycleDebug.ResolveGate re-resolves while null; the SHIPPED scene is unchanged (both
    // components deserialize together — all Awakes before all OnEnables — so GetComponent finds the gate
    // first try). This class (esp. DebugCycle + its cycle-first SetUp order) IS the regression guard; the
    // shipped -verifyHeldBelt gate drives only the SelectBelt/Inventory.Changed path and never exercises [B].
    public class HeldBeltWeaponVisualPlayModeTests
    {
        private GameObject _invGo;
        private GameObject _seatGo;
        private Inventory _inv;
        private MeshRenderer _renderer;
        private HeldWeaponCycleDebug _cycle;
        private HeldAxe _gate;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            // The seat rig: a cube primitive = MeshFilter+MeshRenderer on the root (the collapsed
            // single-node-FBX topology, no HeldToolRig here) — the cycle's Awake captures the root
            // MeshFilter as the holder and its mesh as the locked axe baseline. Cycle FIRST so the
            // gate's Awake can cache it (mirrors the shipped scene where both deserialize together).
            _seatGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(_seatGo.GetComponent<Collider>());
            _renderer = _seatGo.GetComponent<MeshRenderer>();
            // REGRESSION-CRITICAL ORDER (86cajt6jz): cycle FIRST so the gate's Awake caches it. This also
            // reproduces the add-order that EXPOSED the null-gate bug — the cycle's synchronous OnEnable
            // resolves its HeldTool gate BEFORE HeldAxe is added below, so a permanently-null-cached gate
            // would silently break the empty-handed [B] cycle's RefreshRenderers. Do NOT reorder these two
            // (it would both null the gate's _cycle back-ref AND blind this guard).
            _cycle = _seatGo.AddComponent<HeldWeaponCycleDebug>();
            _gate = _seatGo.AddComponent<HeldAxe>();
            _gate.inventory = _inv;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_seatGo);
        }

        private Mesh Holder() => _cycle.MeshHolder != null ? _cycle.MeshHolder.sharedMesh : null;

        // AC1 + AC2 (order A): axe-then-spear — the visual follows the SELECTION through the full table.
        [UnityTest]
        public IEnumerator AxeThenSpear_HeldVisualFollowsSelection()
        {
            yield return null; // OnEnable wiring
            Assert.IsFalse(_renderer.enabled, "spawn: nothing owned -> hidden");

            _inv.PickUpAxe(); // slot 0, selected by default
            yield return null;
            Assert.IsTrue(_renderer.enabled, "axe selected -> seat SHOWN");
            Assert.IsTrue(_cycle.IsAxeHeld, "axe selected -> the AXE is the displayed weapon");
            Assert.AreSame(_cycle.AxeOriginalMesh, Holder(), "axe selected -> the AXE mesh in the holder");

            _inv.PickUpSpear(); // slot 1, NOT selected
            yield return null;
            Assert.IsTrue(_renderer.enabled, "axe still selected after the spear pickup -> still shown");
            Assert.AreSame(_cycle.AxeOriginalMesh, Holder(),
                "SOAK-224 HYPOTHESIS GUARD: picking up the spear must NOT overwrite the held axe mesh");

            _inv.Model.SelectBelt(1); // the spear
            yield return null;
            Assert.IsTrue(_renderer.enabled,
                "SOAK-224 DEFECT HALF 2: spear selected -> seat SHOWN (used to be EMPTY hands)");
            Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex, _cycle.CurrentIndex,
                "spear selected -> the SPEAR is the displayed weapon");
            Assert.IsNotNull(Holder(), "spear mesh resolved from the committed lineup prefab");
            Assert.AreNotSame(_cycle.AxeOriginalMesh, Holder(),
                "spear selected -> the holder carries the SPEAR mesh, not the axe");

            _inv.Model.SelectBelt(2); // empty slot
            yield return null;
            Assert.IsFalse(_renderer.enabled, "empty slot selected -> EMPTY hands");

            _inv.Model.SelectBelt(0); // back to the axe
            yield return null;
            Assert.IsTrue(_renderer.enabled, "axe re-selected -> shown");
            Assert.AreSame(_cycle.AxeOriginalMesh, Holder(),
                "SOAK-224 DEFECT HALF 1: axe selected AFTER the spear was displayed -> the AXE mesh " +
                "returns (used to render the stale SPEAR mesh in hand)");
        }

        // AC2 (order B): spear-then-axe — the spear lands SELECTED (slot 0) and must show immediately.
        [UnityTest]
        public IEnumerator SpearThenAxe_HeldVisualFollowsSelection()
        {
            yield return null;

            _inv.PickUpSpear(); // slot 0, selected by default
            yield return null;
            Assert.IsTrue(_renderer.enabled, "spear-first pickup lands selected -> seat SHOWN");
            Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex, _cycle.CurrentIndex,
                "spear selected -> the SPEAR is the displayed weapon");
            Assert.AreNotSame(_cycle.AxeOriginalMesh, Holder(), "the SPEAR mesh, not the axe baseline");

            _inv.PickUpAxe(); // slot 1, NOT selected
            yield return null;
            Assert.IsTrue(_renderer.enabled, "spear still selected after the axe pickup -> still shown");
            Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex, _cycle.CurrentIndex,
                "acquiring the axe must not steal the held visual from the selected spear");

            _inv.Model.SelectBelt(1); // the axe
            yield return null;
            Assert.IsTrue(_renderer.enabled, "axe selected -> shown");
            Assert.IsTrue(_cycle.IsAxeHeld, "axe selected -> the AXE is the displayed weapon");
            Assert.AreSame(_cycle.AxeOriginalMesh, Holder(),
                "axe selected -> the AXE mesh (order-independent — AC2)");
        }

        // I-2 (86cakkmr0) SOAK-FAIL regression — the belt-selected PICKAXE must SHOW in-hand with the PICKAXE
        // mesh. The defect (confirmed by the -verifyMine held-seat isolation: rendererEnabled=False,
        // holderMesh=wpn_axe_stone_01): selecting the pickaxe belt slot satisfied NEITHER the HeldAxe.ShouldShow
        // predicate (axe/spear only) NOR the SelectionIndexFor mesh sync (-1) — so the seat renderer stayed
        // DISABLED and the holder still carried the AXE mesh. Asserts the PERCEPT pair (renderer.enabled AND the
        // holder mesh identity) exactly like the soak-224 axe/spear table above — a renderer-only assert would
        // false-green the wrong-mesh half.
        [UnityTest]
        public IEnumerator PickaxeSelected_ShowsPickaxeInHand()
        {
            yield return null; // OnEnable wiring
            Assert.IsFalse(_renderer.enabled, "spawn: nothing owned -> hidden");

            var slot = _inv.Model.AddToolToBelt(_inv.Catalog.ById(ItemCatalog.PickaxeStoneId));
            Assert.IsTrue(slot.HasValue, "stone pickaxe acquired onto the belt");
            _inv.Model.SelectBelt(slot.Value.Index);
            yield return null;

            Assert.IsTrue(_renderer.enabled,
                "SOAK-FAIL FIX: pickaxe selected -> seat SHOWN (used to be EMPTY hands — ShouldShow omitted the pickaxe)");
            Assert.AreEqual(HeldWeaponCycleDebug.PickaxeStoneFamilyIndex, _cycle.CurrentIndex,
                "pickaxe selected -> the STONE pickaxe is the displayed weapon");
            Assert.IsNotNull(Holder(), "the pickaxe mesh resolved from the committed lineup prefab");
            Assert.AreNotSame(_cycle.AxeOriginalMesh, Holder(),
                "SOAK-FAIL FIX: the holder carries the PICKAXE mesh, not the stale axe baseline (SelectionIndexFor now maps it)");

            _inv.Model.SelectBelt((slot.Value.Index + 1) % _inv.BeltSlotCount); // deselect the pickaxe
            yield return null;
            Assert.IsFalse(_renderer.enabled, "deselecting the pickaxe -> EMPTY hands again (owned != selected)");
        }

        // 86caffwv5 soak-3 — the WOOD tier: a crafted WOOD weapon selected in the belt must SHOW in-hand at its wood
        // family index. The defect (Sponsor soak-3): a crafted wooden axe showed NOTHING in the hand — the wood ids
        // satisfied NEITHER HeldAxe.ShouldShow (axe/spear/pickaxe only) NOR the SelectionIndexFor mesh sync (-1) → the
        // seat renderer stayed DISABLED. Asserts the SHOW + the wood INDEX (the gate + sync seam this fix wired). The
        // WOOD MESH IDENTITY is intentionally NOT asserted here: the committed lineup prefab drifted stale (missing the
        // wood nodes — separate ticket 86catwzhy; CI's bootstrap re-bakes it), so in a no-bootstrap run the holder
        // falls back to the axe mesh while the INDEX + VISIBILITY (the fix's contract) are correct regardless.
        // The Sponsor's LITERAL soak-3 case ("if I craft a wooden axe ... nothing is in the hand when its selected").
        // A single wood tool on the belt keeps a KNOWN-empty slot for the deselect step (the all-5 table is the
        // EditMode HeldBeltVisualSyncTests.WoodTierSelected_SelectionTable_MapsToTheWoodMesh_NotEmptyHands guard).
        [UnityTest]
        public IEnumerator WoodAxeSelected_ShowsInHand_AtTheWoodAxeIndex()
        {
            yield return null; // OnEnable wiring
            Assert.IsFalse(_renderer.enabled, "spawn: nothing owned -> hidden");

            var slot = _inv.Model.AddToolToBelt(_inv.Catalog.ById(ItemCatalog.AxeWoodId));
            Assert.IsTrue(slot.HasValue, "wood axe acquired onto the belt (a belt-eligible Tool)");
            _inv.Model.SelectBelt(slot.Value.Index);
            yield return null;

            Assert.IsTrue(_renderer.enabled,
                "soak-3 FIX: the crafted WOOD axe selected -> seat SHOWN (used to be EMPTY hands — the gate omitted the wood tier)");
            Assert.AreEqual(HeldWeaponCycleDebug.AxeWoodFamilyIndex, _cycle.CurrentIndex,
                "wood axe selected -> the WOOD-axe family index is displayed (the belt→held sync now maps the wood tier)");

            _inv.Model.SelectBelt((slot.Value.Index + 1) % _inv.BeltSlotCount); // a known-empty slot (only one tool held)
            yield return null;
            Assert.IsFalse(_renderer.enabled, "wood axe deselected -> EMPTY hands again (owned != selected)");
        }

        // The [B] debug-cycle landmine: with a weapon selected the cycle REFUSES (it could otherwise
        // re-create the exact soak-224 crossed state in one keypress); empty-handed it still works as the
        // knife/sword look-soak aid, and ANY selection change re-asserts the selection over the debug view.
        [UnityTest]
        public IEnumerator DebugCycle_RefusedWhileWeaponSelected_SelectionReassertsOverDebugView()
        {
            yield return null;

            _inv.PickUpAxe();   // axe selected
            _inv.PickUpSpear();
            yield return null;
            Assert.IsTrue(_cycle.IsAxeHeld, "precondition: axe selected + displayed");

            Assert.IsFalse(_cycle.CycleHeldWeaponDebug(),
                "with the axe selected the [B] debug cycle is REFUSED — selection owns the held visual");
            yield return null;
            Assert.IsTrue(_cycle.IsAxeHeld, "the refused cycle did not move the displayed weapon");
            Assert.AreSame(_cycle.AxeOriginalMesh, Holder(), "the AXE mesh is untouched");

            _inv.Model.SelectBelt(2); // empty slot -> hidden, no weapon owns the visual
            yield return null;
            Assert.IsFalse(_renderer.enabled, "empty selected -> hidden");

            Assert.IsTrue(_cycle.CycleHeldWeaponDebug(),
                "empty-handed the debug cycle still works (the knife/sword look-soak aid)");
            yield return null;
            Assert.IsTrue(_cycle.DebugViewActive, "the debug view is active");
            Assert.IsTrue(_renderer.enabled, "the debug view SHOWS through the gate (empty-handed look-soak)");
            Assert.AreEqual(1, _cycle.CurrentIndex, "cycled off the axe to the next family weapon (knife)");

            _inv.Model.SelectBelt(0); // re-select the axe — selection re-asserts over the debug view
            yield return null;
            Assert.IsFalse(_cycle.DebugViewActive, "an inventory change CLEARS the debug view");
            Assert.IsTrue(_renderer.enabled, "axe selected -> shown");
            Assert.IsTrue(_cycle.IsAxeHeld, "selection re-asserted the AXE over the debug view");
            Assert.AreSame(_cycle.AxeOriginalMesh, Holder(), "the AXE mesh is back (no stale debug mesh)");
        }

        // 86cav8y74 — the ALL-FIVE wood table at the component seam. WoodAxeSelected_… above covers only the wood
        // AXE (the Sponsor's literal soak-3 case); nothing anywhere asserted that the OTHER four wood tools
        // (dagger / sword / spear / pickaxe) each resolve to THEIR OWN family index through the live
        // Inventory.Changed -> SyncHeldVisualToSelection seam — a wood tier that fell through the sync would render
        // whatever the previous selection left in the hand, and every existing test would stay green.
        //
        // SCOPE SPLIT (deliberate, matching the WoodAxeSelected_… caveat above): this asserts VISIBILITY + INDEX
        // only, NOT mesh identity. A no-bootstrap run can read a committed lineup prefab that drifted short of the
        // wood nodes (the #304 / 86catwzhy class), in which case ApplyCurrent legitimately falls back to the axe
        // mesh while the index + visibility contract this test owns is still correct. Mesh IDENTITY is gated one
        // layer out, where the prefab is always freshly baked: the shipped-build -verifyHeldWood capture gate
        // (AxeVerifyCapture.RunHeldWoodVerification) asserts the holder's sharedMesh IS the lineup node for the
        // selected index, and CommittedLineupDriftGuardTests + WoodTierShippedGateTests pin the committed prefab's
        // wood nodes in EditMode. Do not "strengthen" this test into asserting identity — it would go red for the
        // stale-prefab reason rather than the behaviour it guards.
        //
        // Belt is 5 slots and there are exactly 5 wood tools, so the belt fills and there is no known-empty slot
        // to deselect into — the deselect->hidden half stays owned by the single-tool test above.
        [UnityTest]
        public IEnumerator EveryWoodTool_Selected_ShowsInHand_AtItsOwnWoodIndex()
        {
            yield return null; // OnEnable wiring
            Assert.IsFalse(_renderer.enabled, "spawn: nothing owned -> hidden");

            var tools = new (string id, int index, string label)[]
            {
                (ItemCatalog.AxeWoodId,     HeldWeaponCycleDebug.AxeWoodFamilyIndex,     "wood axe"),
                (ItemCatalog.DaggerWoodId,  HeldWeaponCycleDebug.DaggerWoodFamilyIndex,  "wood dagger"),
                (ItemCatalog.SwordWoodId,   HeldWeaponCycleDebug.SwordWoodFamilyIndex,   "wood sword"),
                (ItemCatalog.SpearWoodId,   HeldWeaponCycleDebug.SpearWoodFamilyIndex,   "wood spear"),
                (ItemCatalog.PickaxeWoodId, HeldWeaponCycleDebug.PickaxeWoodFamilyIndex, "wood pickaxe"),
            };
            Assert.GreaterOrEqual(_inv.BeltSlotCount, tools.Length,
                "precondition: the belt holds all five wood tools at once");

            var slots = new int[tools.Length];
            for (int n = 0; n < tools.Length; n++)
            {
                var placed = _inv.Model.AddToolToBelt(_inv.Catalog.ById(tools[n].id));
                Assert.IsTrue(placed.HasValue, tools[n].label + " acquired onto the belt (a belt-eligible Tool)");
                Assert.AreEqual(SlotArea.Belt, placed.Value.Area,
                    tools[n].label + " landed on the BELT, not the pack (a pack landing would make the SelectBelt " +
                    "below select an unrelated slot and the assertions would judge a state never driven)");
                slots[n] = placed.Value.Index;
            }

            for (int n = 0; n < tools.Length; n++)
            {
                _inv.Model.SelectBelt(slots[n]);
                yield return null;
                Assert.IsTrue(_renderer.enabled,
                    "soak-3 CLASS: " + tools[n].label + " selected -> seat SHOWN (the defect was EMPTY hands for " +
                    "every wood id — HeldAxe.ShouldShow reads IsHeldVisualWeaponSelected, which must cover this tier)");
                Assert.AreEqual(tools[n].index, _cycle.CurrentIndex,
                    tools[n].label + " selected -> ITS OWN wood family index is displayed. A wood tier missing from " +
                    "WoodSelectionIndexFor leaves the PREVIOUS selection's weapon in the hand — which reads as the " +
                    "wrong weapon, not as empty hands, so a visibility-only assert would pass right through it.");
                Assert.IsFalse(_cycle.DebugViewActive,
                    "the BELT SELECTION owns the visual for " + tools[n].label + " (not the [B] debug view)");
            }

            // The crossed-state regression in its WOOD flavour (the soak-224 shape, never tested on wood): after
            // the other four have been displayed, re-selecting the wood AXE must come back to the wood-axe index —
            // never leave the wood pickaxe's mesh in the hand.
            _inv.Model.SelectBelt(slots[0]);
            yield return null;
            Assert.IsTrue(_renderer.enabled, "wood axe re-selected -> shown");
            Assert.AreEqual(HeldWeaponCycleDebug.AxeWoodFamilyIndex, _cycle.CurrentIndex,
                "CROSSED-STATE (wood flavour): selecting the wood AXE after the other four wood tools were " +
                "displayed must return the WOOD-AXE index, never leave the last tool's mesh in the hand");
        }

        // 86caxjx26 — the STONE-BLADE pair at the same component seam (the wood table's shape, extended rather
        // than paralleled per AC4). dagger_stone + sword_stone were the LAST two roster ids with no held-visual
        // map row: crafting a Stone Dagger and selecting it rendered EMPTY hands in the shipped build. This
        // drives the REAL Inventory.Changed -> SyncHeldVisualToSelection -> HeldAxe.Apply chain, so it fails if
        // the new map is added but not composed into HeldVisualIndexFor / IsHeldVisualWeaponSelected.
        //
        // SCOPE SPLIT — identical to the wood test above and for the same reason: VISIBILITY + INDEX only, NOT
        // mesh identity. A no-bootstrap run can read a committed lineup prefab whose nodes drifted, in which case
        // ApplyCurrent legitimately falls back to the axe mesh while the index + visibility contract this test
        // owns is still correct. Do not "strengthen" this into an identity assert — it would red for the stale-
        // prefab reason rather than the behaviour it guards. Mesh identity for these two is covered one layer out
        // by the shipped-build capture in this PR's Self-Test Report.
        [UnityTest]
        public IEnumerator EveryStoneBlade_Selected_ShowsInHand_AtItsOwnStoneIndex()
        {
            yield return null; // OnEnable wiring
            Assert.IsFalse(_renderer.enabled, "spawn: nothing owned -> hidden");

            var blades = new (string id, int index, string label)[]
            {
                (ItemCatalog.DaggerStoneId, HeldWeaponCycleDebug.DaggerStoneFamilyIndex, "stone dagger"),
                (ItemCatalog.SwordStoneId,  HeldWeaponCycleDebug.SwordStoneFamilyIndex,  "stone sword"),
            };
            Assert.GreaterOrEqual(_inv.BeltSlotCount, blades.Length + 1,
                "precondition: the belt holds both stone blades AND a spare slot to deselect into");

            var slots = new int[blades.Length];
            for (int n = 0; n < blades.Length; n++)
            {
                var placed = _inv.Model.AddToolToBelt(_inv.Catalog.ById(blades[n].id));
                Assert.IsTrue(placed.HasValue, blades[n].label + " acquired onto the belt (a belt-eligible Tool)");
                Assert.AreEqual(SlotArea.Belt, placed.Value.Area,
                    blades[n].label + " landed on the BELT, not the pack (a pack landing would make the SelectBelt " +
                    "below select an unrelated slot and the assertions would judge a state never driven)");
                slots[n] = placed.Value.Index;
            }

            for (int n = 0; n < blades.Length; n++)
            {
                _inv.Model.SelectBelt(slots[n]);
                yield return null;
                Assert.IsTrue(_renderer.enabled,
                    "EMPTY-HANDS CLASS (4th occurrence): " + blades[n].label + " selected -> seat SHOWN. The defect " +
                    "was nothing in the hand for both stone blades — HeldAxe.ShouldShow reads " +
                    "IsHeldVisualWeaponSelected, which must now cover this pair too.");
                Assert.AreEqual(blades[n].index, _cycle.CurrentIndex,
                    blades[n].label + " selected -> ITS OWN stone family index is displayed. A blade missing from " +
                    "the composed map leaves the PREVIOUS selection's weapon in the hand — the WRONG weapon, not " +
                    "empty hands, which a visibility-only assert would pass straight through.");
                Assert.IsFalse(_cycle.DebugViewActive,
                    "the BELT SELECTION owns the visual for " + blades[n].label + " (not the [B] debug view)");
            }

            // The crossed-state regression in its stone-blade flavour, then the deselect->hidden half (the wood
            // test could not cover deselect: five wood tools fill the five belt slots, leaving none empty).
            _inv.Model.SelectBelt(slots[0]);
            yield return null;
            Assert.AreEqual(HeldWeaponCycleDebug.DaggerStoneFamilyIndex, _cycle.CurrentIndex,
                "CROSSED-STATE (stone-blade flavour): re-selecting the stone DAGGER after the sword was displayed " +
                "must return the stone-DAGGER index, never leave the sword's mesh in the hand");

            _inv.Model.SelectBelt(_inv.BeltSlotCount - 1); // a slot neither blade occupies
            yield return null;
            Assert.IsFalse(_renderer.enabled,
                "an empty slot selected -> the seat is HIDDEN again (ownership is not selection; the new map must " +
                "not latch the seat on once a stone blade has been held)");
        }
    }
}
