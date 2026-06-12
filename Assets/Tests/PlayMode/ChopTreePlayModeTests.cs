using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the U2-3 chop interaction (ticket 86ca8bdd8).
    ///
    /// Proves the axe-gated chop actually FIRES through ChopTree.Update when an AXE-HOLDING player
    /// reaches the tree, and — the load-bearing negative case (success test: "chopping without the axe
    /// does nothing") — that an AXE-LESS player at the tree yields NO wood and never fells it. Also
    /// proves the tree fells after chopsToFell chops and that wood ticks up per chop. We drive the
    /// player transform directly, isolating ChopTree's proximity + axe-gate logic from pathfinding
    /// (NavMesh/click-move is covered by the verify capture).
    /// </summary>
    public class ChopTreePlayModeTests
    {
        private GameObject _invGo;
        private GameObject _playerGo;
        private GameObject _treeGo;
        private Inventory _inv;
        private ChopTree _tree;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the tree

            _treeGo = new GameObject("ChopTree");
            _treeGo.transform.position = Vector3.zero;
            _tree = _treeGo.AddComponent<ChopTree>();
            _tree.inventory = _inv;
            _tree.player = _playerGo.transform;
            _tree.visual = _treeGo.transform;
            _tree.chopRadius = 2.2f;
            _tree.woodPerChop = 1;
            _tree.chopsToFell = 3;
            _tree.chopInterval = 0.05f; // fast so the test fells within a few frames of wall-clock
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_treeGo);
        }

        // === THE AXE GATE (negative case) — the success-test's "chopping without the axe does nothing" ===
        [UnityTest]
        public IEnumerator AtTreeWithoutAxe_YieldsNoWood_NeverFells()
        {
            Assert.IsFalse(_inv.HasAxe, "precondition: no axe");

            // Stand the axe-less player ON the tree and let many frames + wall-clock pass.
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // well within chopRadius
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null; // real wall-clock window

            Assert.AreEqual(0, _inv.WoodCount, "no axe -> no wood, even standing at the tree");
            Assert.AreEqual(0, _tree.Chops, "no axe -> no chops land");
            Assert.IsFalse(_tree.IsFelled, "no axe -> the tree is never felled");
        }

        // === Positive: with the axe, reaching the tree yields wood and fells it ===
        [UnityTest]
        public IEnumerator AtTreeWithAxe_YieldsWood_AndFells()
        {
            _inv.CraftAxe();
            Assert.IsTrue(_inv.HasAxe, "precondition: axe in hand");
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood yet");

            // Walk the axe-holding player onto the tree; chops should land over a short wall-clock window.
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);

            float start = Time.time;
            while (Time.time - start < 2f && !_tree.IsFelled) yield return null;

            Assert.IsTrue(_tree.IsFelled, "with the axe, standing at the tree fells it within the window");
            Assert.AreEqual(_tree.chopsToFell, _tree.Chops, "fells after exactly chopsToFell chops");
            Assert.AreEqual(_tree.chopsToFell * _tree.woodPerChop, _inv.WoodCount,
                "total wood == chopsToFell * woodPerChop (wood ticks up per chop)");
        }

        // === Felled tree is spent: no further wood after it falls (no respawn — out of scope) ===
        [UnityTest]
        public IEnumerator FelledTree_YieldsNoMoreWood()
        {
            _inv.CraftAxe();
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);

            float start = Time.time;
            while (Time.time - start < 2f && !_tree.IsFelled) yield return null;
            Assert.IsTrue(_tree.IsFelled, "tree felled");
            int woodAtFell = _inv.WoodCount;

            // Keep standing at the felled tree for another window — wood must NOT keep climbing.
            start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            Assert.AreEqual(woodAtFell, _inv.WoodCount, "a felled tree is spent — no further wood");
        }

        // === Out of range with the axe never chops (proximity is required too) ===
        [UnityTest]
        public IEnumerator OutOfRangeWithAxe_DoesNotChop()
        {
            _inv.CraftAxe();
            // Player stays far away.
            for (int i = 0; i < 10; i++) yield return null;

            Assert.AreEqual(0, _inv.WoodCount, "out of range -> no wood even with the axe");
            Assert.AreEqual(0, _tree.Chops, "out of range -> no chops");
            Assert.IsFalse(_tree.IsFelled, "out of range -> not felled");
        }
    }
}
