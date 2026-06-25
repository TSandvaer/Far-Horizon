using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the chop mechanic (ticket 86caa4c5c — the gameplay-wave successor to the U2-3
    /// thin chop 86ca8bdd8). Proves, driving the player transform directly to isolate ChopTree's proximity +
    /// gate logic from pathfinding:
    ///
    ///   AC1 (the load-bearing gate) — the chop requires the axe to be the SELECTED belt item
    ///        (Inventory.IsAxeSelectedInBelt), NOT merely OWNED. Three cases:
    ///          • axe selected + at the tree → chops (positive);
    ///          • NO axe at all + at the tree → nothing (negative, the "chopping without the axe does nothing");
    ///          • axe OWNED but a different belt slot selected → nothing (the NEW selection-gate case — this
    ///            is what supersedes the old HasAxe gate; HasAxe would have wrongly chopped here).
    ///   AC1 (swing) — each landed chop fires the ChopPoseDriver swing (SwingNormT goes active).
    ///   AC2 — chopping yields WOOD (ItemCatalog.WoodId) into the inventory; it stacks/ticks per chop.
    ///   AC3 — after chopsToFell the tree FELLS to a stump, then REGROWS after the (tweakable) timer into a
    ///        standing, choppable tree (chop count reset). The stump persists through the regrow window.
    ///   AC6 — these tests ARE the AC6 regression guard (chop → wood; deplete → stump → regrow).
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
            _tree.chopInterval = 0.05f;       // fast so the test fells within a few frames of wall-clock
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

        // === AC1 negative — NO axe at all at the tree does nothing (the success-test's classic case) ===
        [UnityTest]
        public IEnumerator AtTreeWithoutAxe_YieldsNoWood_NeverFells()
        {
            Assert.IsFalse(_inv.HasAxe, "precondition: no axe owned");
            Assert.IsFalse(_inv.IsAxeSelectedInBelt, "precondition: no axe selected");

            StandAtTree();
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            Assert.AreEqual(0, _inv.WoodCount, "no axe -> no wood, even standing at the tree");
            Assert.AreEqual(0, _tree.Chops, "no axe -> no chops land");
            Assert.IsFalse(_tree.IsFelled, "no axe -> the tree is never felled");
        }

        // === AC1 negative (the NEW selection gate) — axe OWNED but NOT the selected belt item does nothing.
        //     This is the case the old HasAxe gate got WRONG (it would have chopped). ===
        [UnityTest]
        public IEnumerator AtTreeWithAxeOwnedButNotSelected_YieldsNoWood_NeverFells()
        {
            _inv.CraftAxe();
            Assert.IsTrue(_inv.HasAxe, "precondition: axe is OWNED");
            // Deselect the axe by selecting a different (empty) belt slot.
            _inv.Model.SelectBelt(1);
            Assert.IsFalse(_inv.IsAxeSelectedInBelt, "precondition: axe owned but NOT the selected belt item");

            StandAtTree();
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            Assert.AreEqual(0, _inv.WoodCount, "axe not SELECTED -> no wood (the selection gate, not just owned)");
            Assert.AreEqual(0, _tree.Chops, "axe not selected -> no chops land");
            Assert.IsFalse(_tree.IsFelled, "axe not selected -> the tree is never felled");
        }

        // === AC1 positive + AC2 — with the axe SELECTED, reaching the tree yields wood and fells it ===
        [UnityTest]
        public IEnumerator AtTreeWithSelectedAxe_YieldsWood_AndFells()
        {
            SelectAxe();
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood yet");

            StandAtTree();
            float start = Time.time;
            while (Time.time - start < 2f && !_tree.IsFelled) yield return null;

            Assert.IsTrue(_tree.IsFelled, "with the selected axe, standing at the tree fells it within the window");
            Assert.AreEqual(_tree.chopsToFell, _tree.Chops, "fells after exactly chopsToFell chops");
            Assert.AreEqual(_tree.chopsToFell * _tree.woodPerChop, _inv.WoodCount,
                "total wood == chopsToFell * woodPerChop (wood ticks up per chop into the WoodId stack)");
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

            // Fell it.
            float start = Time.time;
            while (Time.time - start < 2f && !_tree.IsFelled) yield return null;
            Assert.IsTrue(_tree.IsFelled, "tree felled (now a stump)");
            int woodAtFell = _inv.WoodCount;

            // The stump persists for a moment (no more wood while felled, even standing at it).
            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(_tree.IsFelled, "the stump persists through the regrow window (AC4)");
            Assert.AreEqual(woodAtFell, _inv.WoodCount, "a felled stump yields no wood until it regrows");

            // Wait past the max regrow time + the rise tween — the stump regrows into a standing tree.
            start = Time.time;
            while (Time.time - start < _tree.regrowthMaxSeconds + 1.5f && _tree.IsFelled) yield return null;
            Assert.IsFalse(_tree.IsFelled, "the stump regrew into a standing tree after the timer (AC3)");
            Assert.AreEqual(0, _tree.Chops, "a regrown tree resets its chop count — it can be chopped anew");

            // And the regrown tree is choppable again — standing at it (axe selected) yields fresh wood.
            start = Time.time;
            while (Time.time - start < 1f && _inv.WoodCount <= woodAtFell) yield return null;
            Assert.Greater(_inv.WoodCount, woodAtFell, "the regrown tree is choppable again — fresh wood yields");
        }

        // === Out of range with the axe selected never chops (proximity is required too) ===
        [UnityTest]
        public IEnumerator OutOfRangeWithSelectedAxe_DoesNotChop()
        {
            SelectAxe();
            // Player stays far away.
            for (int i = 0; i < 10; i++) yield return null;

            Assert.AreEqual(0, _inv.WoodCount, "out of range -> no wood even with the axe selected");
            Assert.AreEqual(0, _tree.Chops, "out of range -> no chops");
            Assert.IsFalse(_tree.IsFelled, "out of range -> not felled");
        }
    }
}
