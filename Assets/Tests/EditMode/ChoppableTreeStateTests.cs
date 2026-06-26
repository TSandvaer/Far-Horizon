using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the per-tree CHOP STATE extracted in CHANGE (a) (86caa4c5c — every scatter tree
    /// choppable). <see cref="ChoppableTreeState"/> owns ONE tree's deplete → stump → regrow lifecycle; ChopTree
    /// holds a list of them (the demo tree + every scatter LP_Tree) and resolves the nearest in-range one. These
    /// tests pin the per-instance state machine headlessly (the live multi-tree resolve + Start-time scatter
    /// discovery are exercised in ChopTreePlayModeTests):
    ///   • a fresh tree is STANDING + choppable, 0 chops;
    ///   • LandChop advances the count and returns false until the felling chop, then true (felled);
    ///   • a felled tree is NOT choppable (LandChop is a no-op) and its RegrowAt is scheduled within [min,max];
    ///   • two independent instances deplete independently (felling one leaves the other standing).
    /// No scene/Input/Animator rig — pure object state. Mirrors the project's "pure decision is unit-testable"
    /// discipline (ShouldChopOnClick / TrunkObstacleLocalRadius).
    /// </summary>
    public class ChoppableTreeStateTests
    {
        // A bare transform to stand in for a tree's visual root (the state tweens it; here we only assert state).
        private static Transform NewVisual(Vector3 at)
        {
            var go = new GameObject("LP_Tree");
            go.transform.position = at;
            return go.transform;
        }

        [Test]
        public void FreshTree_IsStandingAndChoppable_ZeroChops()
        {
            var v = NewVisual(Vector3.zero);
            var s = new ChoppableTreeState(v, 12345);
            Assert.IsTrue(s.IsChoppable, "a fresh tree is standing and choppable");
            Assert.IsFalse(s.Felled, "a fresh tree is not felled");
            Assert.AreEqual(0, s.Chops, "a fresh tree has no chops landed");
            Object.DestroyImmediate(v.gameObject);
        }

        [Test]
        public void LandChop_AdvancesCount_ThenFellsOnFinalChop()
        {
            var v = NewVisual(Vector3.zero);
            var s = new ChoppableTreeState(v, 12345);
            const int chopsToFell = 3;

            bool f1 = s.LandChop(chopsToFell, 0.4f, 0.6f);
            Assert.IsFalse(f1, "chop 1 of 3 does not fell");
            Assert.AreEqual(1, s.Chops, "chop 1 landed");
            Assert.IsTrue(s.IsChoppable, "still standing after chop 1");

            bool f2 = s.LandChop(chopsToFell, 0.4f, 0.6f);
            Assert.IsFalse(f2, "chop 2 of 3 does not fell");
            Assert.AreEqual(2, s.Chops, "chop 2 landed");

            bool f3 = s.LandChop(chopsToFell, 0.4f, 0.6f);
            Assert.IsTrue(f3, "the chopsToFell-th chop FELLS the tree");
            Assert.AreEqual(3, s.Chops, "chop 3 landed");
            Assert.IsTrue(s.Felled, "the tree is now felled (a stump)");
            Assert.IsFalse(s.IsChoppable, "a felled stump is NOT choppable");
            Object.DestroyImmediate(v.gameObject);
        }

        [Test]
        public void FelledStump_IsNotChoppable_LandChopIsNoOp()
        {
            var v = NewVisual(Vector3.zero);
            var s = new ChoppableTreeState(v, 12345);
            for (int i = 0; i < 3; i++) s.LandChop(3, 0.4f, 0.6f);
            Assert.IsTrue(s.Felled, "precondition: felled");

            int chopsAtFell = s.Chops;
            bool felled = s.LandChop(3, 0.4f, 0.6f);
            Assert.IsFalse(felled, "LandChop on a stump returns false (no fell)");
            Assert.AreEqual(chopsAtFell, s.Chops, "a stump takes no further chops (the resolver skips it too)");
            Object.DestroyImmediate(v.gameObject);
        }

        [Test]
        public void RegrowAt_IsScheduledWithinMinMax_OnFelling()
        {
            var v = NewVisual(Vector3.zero);
            var s = new ChoppableTreeState(v, 12345);
            float t0 = Time.time;
            for (int i = 0; i < 3; i++) s.LandChop(3, 5f, 9f);

            Assert.IsTrue(s.Felled, "felled");
            float delay = s.RegrowAt - t0;
            Assert.GreaterOrEqual(delay, 5f - 0.01f, "the regrow delay is at least the min (organic random in [min,max] — AC3)");
            Assert.LessOrEqual(delay, 9f + 0.01f, "the regrow delay is at most the max");
            Object.DestroyImmediate(v.gameObject);
        }

        [Test]
        public void TwoInstances_DepleteIndependently()
        {
            var va = NewVisual(new Vector3(0f, 0f, 0f));
            var vb = NewVisual(new Vector3(10f, 0f, 0f));
            var a = new ChoppableTreeState(va, 111);
            var b = new ChoppableTreeState(vb, 222);

            for (int i = 0; i < 3; i++) a.LandChop(3, 0.4f, 0.6f);
            Assert.IsTrue(a.Felled, "tree A felled");
            Assert.IsFalse(b.Felled, "tree B is UNAFFECTED by chopping A — independent per-instance state (CHANGE (a))");
            Assert.AreEqual(0, b.Chops, "tree B has no chops");
            Assert.IsTrue(b.IsChoppable, "tree B is still standing and choppable");

            Object.DestroyImmediate(va.gameObject);
            Object.DestroyImmediate(vb.gameObject);
        }

        [Test]
        public void ScatterTreeName_MatchesLowPolyZoneGenBuildTreeName()
        {
            // The resolver discovers scatter trees by this exact GameObject name (LowPolyZoneGen.BuildTree
            // names every scatter tree "LP_Tree"). If the scatter generator's name ever drifts, the discovery
            // silently finds nothing → only the demo tree chops again (the bug this change fixes). Pin it.
            Assert.AreEqual("LP_Tree", ChopTree.ScatterTreeName,
                "the scatter-tree discovery key must match LowPolyZoneGen.BuildTree's GameObject name");
        }
    }
}
