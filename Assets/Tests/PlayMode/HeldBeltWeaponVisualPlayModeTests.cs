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
    }
}
