using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the chop mechanic (ticket 86caa4c5c — the gameplay-wave successor to the U2-3
    /// thin chop 86ca8bdd8). Proves, driving the player transform directly + the chop-click via the
    /// programmatic RequestChopClick() seam (CHANGE 1 — the chop is now per LEFT-CLICK, not proximity-auto;
    /// a headless run can't inject a real mouse button) to isolate ChopTree's range + gate logic from
    /// pathfinding/input:
    ///
    ///   AC1 (the TRIGGER, CHANGE 1) — the chop is INITIATED by a left-click, NOT proximity-auto:
    ///          • at the tree WITHOUT a click → NO chop (standing at the tree no longer auto-chops);
    ///          • ONE click in range → exactly ONE chop (one strike per click).
    ///   AC1 (the load-bearing gate) — a click still requires the axe to be the SELECTED belt item
    ///        (Inventory.IsAxeSelectedInBelt), NOT merely OWNED. Three cases:
    ///          • axe selected + click at the tree → chops (positive);
    ///          • NO axe at all + click at the tree → nothing (negative, the "chopping without the axe does nothing");
    ///          • axe OWNED but a different belt slot selected → nothing (the NEW selection-gate case — this
    ///            is what supersedes the old HasAxe gate; HasAxe would have wrongly chopped here).
    ///   AC1 (swing) — each landed chop fires the ChopPoseDriver swing (SwingNormT goes active).
    ///   AC2 — chopping yields WOOD (ItemCatalog.WoodId) into the inventory; it stacks/ticks per chop-click.
    ///   AC3 — after chopsToFell click-chops the tree FELLS to a stump, then REGROWS after the (tweakable)
    ///        timer into a standing, choppable tree (chop count reset). The stump persists through the window.
    ///   AC6 — these tests ARE the AC6 regression guard (click → wood; deplete → stump → regrow).
    ///
    /// The pure CHANGE-1 guard truth-table (no chop while a panel is open / pointer over UI / RMB held) is
    /// unit-asserted headlessly in EditMode (ChopClickGateTests over ChopTree.ShouldChopOnClick); these
    /// PlayMode tests cover the live click-drives-a-chop loop.
    ///
    /// Headless time discipline (unity-conventions.md / playbook E6/E7): all waits are real Time.time /
    /// WaitForSeconds windows — NEVER WaitForEndOfFrame (does not fire in -batchmode), NEVER per-frame
    /// deltaTime assertions (deltaTime ≈ 0 headless). The swing + regrow are anchored on Time.time.
    /// </summary>
    public class ChopTreePlayModeTests
    {
        private GameObject _invGo;
        private GameObject _playerGo;
        private GameObject _treeGo;
        private GameObject _driverGo;
        private Inventory _inv;
        private ChopTree _tree;
        private ChopPoseDriver _driver;
        private CastawayArmPose _armPose;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the tree

            // A swing driver + arm pose so the swing-fires assertion exercises the real wiring. The arm-pose
            // needs no bones to test SwingNormT (the driver's timer is bone-independent).
            _driverGo = new GameObject("CastawayAvatar");
            _armPose = _driverGo.AddComponent<CastawayArmPose>();
            _driver = _driverGo.AddComponent<ChopPoseDriver>();
            _driver.armPose = _armPose;
            _driver.swingDuration = 0.5f;

            _treeGo = new GameObject("ChopTree");
            _treeGo.transform.position = Vector3.zero;
            _tree = _treeGo.AddComponent<ChopTree>();
            _tree.inventory = _inv;
            _tree.player = _playerGo.transform;
            _tree.visual = _treeGo.transform;
            _tree.poseDriver = _driver;
            _tree.chopRadius = 2.2f;
            _tree.woodPerChop = 1;
            _tree.chopsToFell = 3;
            _tree.chopInterval = 0f;          // no click cooldown in the test (each requested click chops)
            _tree.inventoryUI = null;         // no UI rig → the over-UI guard is skipped (modal/RMB are false)
            _tree.regrowthMinSeconds = 0.4f;  // short so the regrow window is testable in headless wall-clock
            _tree.regrowthMaxSeconds = 0.6f;
            _tree.regrowSeed = 12345;         // deterministic regrow roll
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_treeGo);
            Object.Destroy(_driverGo);
        }

        // Place the axe in the belt AND make it the selected belt item (CraftAxe puts it in slot 0, which is
        // the default selected slot — so on a fresh inventory this is already true; assert to be explicit).
        private void SelectAxe()
        {
            _inv.CraftAxe();
            Assert.IsTrue(_inv.IsAxeSelectedInBelt, "precondition: the axe is the SELECTED belt item");
        }

        private void StandAtTree() => _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);

        // CHANGE 1 — request ONE chop-click (the input-independent seam) then advance a frame so ChopTree.Update
        // consumes the latch + lands (or rejects) the chop. UiInputGate is forced closed (no modal panel) so the
        // gate decision in a headless test is range + axe-selected only.
        private IEnumerator ClickChop()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked); // ensure no modal-panel gate in the test
            _tree.RequestChopClick();
            yield return null;
        }
        private bool _gateTracked;

        // === CHANGE 1 — at the tree WITHOUT a click does NOTHING (the proximity-auto trigger is REMOVED) ===
        [UnityTest]
        public IEnumerator AtTreeWithSelectedAxe_NoClick_DoesNotChop()
        {
            SelectAxe();
            StandAtTree();

            // Stand in range, axe selected, but NEVER request a click — the old proximity-auto would have
            // chopped here; the new left-click trigger must NOT.
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            Assert.AreEqual(0, _inv.WoodCount, "standing at the tree WITHOUT a click must not chop (CHANGE 1)");
            Assert.AreEqual(0, _tree.Chops, "no click -> no chops land (proximity-auto is removed)");
            Assert.IsFalse(_tree.IsFelled, "no click -> the tree is never felled");
        }

        // === CHANGE 1 — ONE click in range with the selected axe lands EXACTLY ONE chop (one strike/click) ===
        [UnityTest]
        public IEnumerator OneClickInRange_LandsExactlyOneChop()
        {
            SelectAxe();
            StandAtTree();
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood yet");

            yield return ClickChop();

            Assert.AreEqual(1, _tree.Chops, "ONE click -> exactly ONE chop (one strike per click)");
            Assert.AreEqual(_tree.woodPerChop, _inv.WoodCount, "one chop -> woodPerChop wood");

            // A second frame WITHOUT a new click does not chop again (the click does not repeat).
            yield return null;
            Assert.AreEqual(1, _tree.Chops, "no further chop without a NEW click (one chop per click)");
        }

        // === AC1 negative — NO axe at all + a click at the tree does nothing (the success-test's classic case) ===
        [UnityTest]
        public IEnumerator ClickAtTreeWithoutAxe_YieldsNoWood_NeverFells()
        {
            Assert.IsFalse(_inv.HasAxe, "precondition: no axe owned");
            Assert.IsFalse(_inv.IsAxeSelectedInBelt, "precondition: no axe selected");

            StandAtTree();
            for (int i = 0; i < 5; i++) yield return ClickChop();

            Assert.AreEqual(0, _inv.WoodCount, "no axe -> no wood, even clicking at the tree");
            Assert.AreEqual(0, _tree.Chops, "no axe -> no chops land");
            Assert.IsFalse(_tree.IsFelled, "no axe -> the tree is never felled");
        }

        // === AC1 negative (the NEW selection gate) — axe OWNED but NOT the selected belt item does nothing.
        //     This is the case the old HasAxe gate got WRONG (it would have chopped). ===
        [UnityTest]
        public IEnumerator ClickAtTreeWithAxeOwnedButNotSelected_YieldsNoWood_NeverFells()
        {
            _inv.CraftAxe();
            Assert.IsTrue(_inv.HasAxe, "precondition: axe is OWNED");
            // Deselect the axe by selecting a different (empty) belt slot.
            _inv.Model.SelectBelt(1);
            Assert.IsFalse(_inv.IsAxeSelectedInBelt, "precondition: axe owned but NOT the selected belt item");

            StandAtTree();
            for (int i = 0; i < 5; i++) yield return ClickChop();

            Assert.AreEqual(0, _inv.WoodCount, "axe not SELECTED -> no wood (the selection gate, not just owned)");
            Assert.AreEqual(0, _tree.Chops, "axe not selected -> no chops land");
            Assert.IsFalse(_tree.IsFelled, "axe not selected -> the tree is never felled");
        }

        // === AC1 positive + AC2 — with the axe SELECTED, clicking in range yields wood and (after enough
        //     clicks) fells the tree ===
        [UnityTest]
        public IEnumerator ClickAtTreeWithSelectedAxe_YieldsWood_AndFells()
        {
            SelectAxe();
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood yet");

            StandAtTree();
            // One click per chop needed to fell (chopsToFell clicks).
            for (int i = 0; i < _tree.chopsToFell; i++) yield return ClickChop();

            Assert.IsTrue(_tree.IsFelled, "with the selected axe, chopsToFell clicks in range fell the tree");
            Assert.AreEqual(_tree.chopsToFell, _tree.Chops, "fells after exactly chopsToFell click-chops");
            Assert.AreEqual(_tree.chopsToFell * _tree.woodPerChop, _inv.WoodCount,
                "total wood == chopsToFell * woodPerChop (wood ticks up per click into the WoodId stack)");
        }

        // === AC1 (swing) — a landed chop FIRES the procedural swing (SwingNormT goes active mid-swing) ===
        [UnityTest]
        public IEnumerator Chop_FiresTheSwing()
        {
            SelectAxe();
            Assert.GreaterOrEqual(_driver.SwingNormT, 1f, "precondition: at rest (no swing yet)");

            // Drive a single chop directly (isolates the swing-trigger seam from the chop pacing).
            _tree.Chop();
            Assert.AreEqual(1, _tree.Chops, "the direct chop landed");

            // Sample mid-swing on the next frame — SwingNormT must be active (>0 and <1). Time.time advances
            // a real (small) amount between yields in PlayMode, so the swing has started but not finished.
            yield return null;
            Assert.IsTrue(_driver.IsSwinging, "a landed chop must put the swing driver into an active swing");
            Assert.Greater(_driver.SwingNormT, 0f, "the swing is progressing after a chop");

            // After the full duration the swing returns to rest and the offset is ~zero (carry pose restored).
            yield return new WaitForSeconds(_driver.swingDuration + 0.1f);
            Assert.GreaterOrEqual(_driver.SwingNormT, 1f, "the swing returns to rest after its duration");
            Assert.AreEqual(0f, _armPose.swingOverrideEuler.magnitude, 0.05f,
                "at rest the driver writes Vector3.zero (the locked carry pose is byte-unchanged)");
        }

        // === AC3 — a felled stump REGROWS after the (tweakable) timer into a standing, choppable tree ===
        [UnityTest]
        public IEnumerator FelledStump_RegrowsAfterTimer_IntoAChoppableTree()
        {
            SelectAxe();
            StandAtTree();

            // Fell it by clicking chopsToFell times.
            for (int i = 0; i < _tree.chopsToFell; i++) yield return ClickChop();
            Assert.IsTrue(_tree.IsFelled, "tree felled (now a stump)");
            int woodAtFell = _inv.WoodCount;

            // The stump persists — clicking it while felled yields NO wood (a stump is not choppable).
            for (int i = 0; i < 3; i++) yield return ClickChop();
            Assert.IsTrue(_tree.IsFelled, "the stump persists through the regrow window (AC4)");
            Assert.AreEqual(woodAtFell, _inv.WoodCount, "a felled stump yields no wood (clicking it does nothing)");

            // Wait past the max regrow time + the rise tween — the stump regrows into a standing tree.
            float start = Time.time;
            while (Time.time - start < _tree.regrowthMaxSeconds + 1.5f && _tree.IsFelled) yield return null;
            Assert.IsFalse(_tree.IsFelled, "the stump regrew into a standing tree after the timer (AC3)");
            Assert.AreEqual(0, _tree.Chops, "a regrown tree resets its chop count — it can be chopped anew");

            // And the regrown tree is choppable again — a click in range (axe selected) yields fresh wood.
            yield return ClickChop();
            Assert.Greater(_inv.WoodCount, woodAtFell, "the regrown tree is choppable again — a click yields wood");
        }

        // === Out of range with the axe selected — a click never chops (proximity is required too) ===
        [UnityTest]
        public IEnumerator ClickOutOfRangeWithSelectedAxe_DoesNotChop()
        {
            SelectAxe();
            // Player stays far away (SetUp put it at (20,0,20)); click anyway.
            for (int i = 0; i < 5; i++) yield return ClickChop();

            Assert.AreEqual(0, _inv.WoodCount, "out of range -> a click yields no wood even with the axe selected");
            Assert.AreEqual(0, _tree.Chops, "out of range -> no chops");
            Assert.IsFalse(_tree.IsFelled, "out of range -> not felled");
        }
    }
}
