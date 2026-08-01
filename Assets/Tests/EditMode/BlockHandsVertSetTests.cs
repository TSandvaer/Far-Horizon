using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the bug CLASS that made round-10's first pass measure nothing (ticket 86cau4za2).
    ///
    /// THE BUG, IN ONE SENTENCE: a vertex set built by picking ONE vert per position leaves that position's
    /// coincident duplicates in the COMPLEMENTARY set, and every set-vs-set distance metric then reads 0 by
    /// construction — because each member's own twin is sitting in the other set at distance zero.
    ///
    /// WHY IT IS EASY TO SHIP: low-poly faceted meshes duplicate vertices at every hard-edge normal split
    /// (unity6-mastery.md §8 — "expect more vertices than triangle count"); the castaway v4 hand carries exactly
    /// x4 verts per distinct position (236 verts / 59 positions per hand, ci-out/blockhands-r10c.log). Nothing in
    /// the code looks wrong, the metric returns a clean number, and a clean 0.00mm reads as "flush" rather than as
    /// "measuring nothing". Two further metrics built on the same sets agreed with it, which read as corroboration.
    ///
    /// These tests are deliberately SYNTHETIC — no FBX import, no AssetDatabase — so they cannot go stale-Library
    /// flaky (unity6-mastery.md §7) and they fail for exactly one reason: the set-construction invariant broke.
    /// </summary>
    public class BlockHandsVertSetTests
    {
        private const float Eps = 0.001f;
        private const int Dupes = 4; // the multiplicity the real v4 hand mesh carries

        // Three "block" positions and two "thumb" positions, each duplicated Dupes times, exactly as a faceted
        // hard-edge mesh stores them. The thumb sits 9 units away from the nearest block position, so the correct
        // standoff is unambiguous and any 0 result is a construction failure rather than a geometry coincidence.
        private static Vector3[] BuildMesh(out List<int> thumbSeedOnePerPosition, out List<int> allThumb)
        {
            var block = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f) };
            var thumb = new[] { new Vector3(10f, 0f, 0f), new Vector3(11f, 0f, 0f) };

            var verts = new List<Vector3>();
            foreach (var p in block) for (int d = 0; d < Dupes; d++) verts.Add(p);
            thumbSeedOnePerPosition = new List<int>();
            allThumb = new List<int>();
            foreach (var p in thumb)
                for (int d = 0; d < Dupes; d++)
                {
                    if (d == 0) thumbSeedOnePerPosition.Add(verts.Count); // the naive "nearest match" pick
                    allThumb.Add(verts.Count);
                    verts.Add(p);
                }
            return verts.ToArray();
        }

        private static List<int> Complement(int total, IEnumerable<int> set)
        {
            var s = new HashSet<int>(set);
            var res = new List<int>();
            for (int i = 0; i < total; i++) if (!s.Contains(i)) res.Add(i);
            return res;
        }

        private static float MinDistance(Vector3[] verts, IEnumerable<int> a, IEnumerable<int> b)
        {
            float best = float.MaxValue;
            foreach (int i in a)
                foreach (int j in b)
                {
                    float d = (verts[i] - verts[j]).sqrMagnitude;
                    if (d < best) best = d;
                }
            return Mathf.Sqrt(best);
        }

        [Test]
        public void ClusterComplete_ClosesASetOverItsCoincidentDuplicates_86cau4za2()
        {
            var verts = BuildMesh(out var seed, out var allThumb);
            Assert.AreEqual(2, seed.Count, "precondition: the naive pick takes one vert per thumb position");

            var closed = CastawayV4DefectDiag.ClusterComplete(verts, seed, Eps);

            Assert.AreEqual(allThumb.Count, closed.Count,
                $"ClusterComplete must recover every duplicate at each seeded position ({Dupes} per position), " +
                "otherwise the twins stay in the complementary set and every distance metric collapses to 0");
            CollectionAssert.AreEquivalent(allThumb, closed);
        }

        [Test]
        public void NaiveOneVertPerPosition_LeavesTwinsInTheOtherSet_AndCollapsesTheMetricToZero_86cau4za2()
        {
            var verts = BuildMesh(out var seed, out _);
            var naiveBlock = Complement(verts.Length, seed);

            // This is the defect, asserted as a property rather than described in a comment: the two sets overlap
            // in POSITION even though they share no INDEX.
            Assert.IsFalse(CastawayV4DefectDiag.CoincidenceDisjoint(verts, seed, naiveBlock, Eps),
                "the naive construction must be detectably NOT coincidence-disjoint — that is the whole defect");

            // ...and the consequence: the judged distance is 0 while the thumb is plainly 9 units away.
            Assert.AreEqual(0f, MinDistance(verts, seed, naiveBlock), 1e-4f,
                "with twins in the block set the standoff is 0 by construction — a clean number measuring nothing");
        }

        [Test]
        public void ClusterCompleteSets_AreCoincidenceDisjoint_AndTheMetricSeesTheRealGap_86cau4za2()
        {
            var verts = BuildMesh(out var seed, out _);
            var thumb = new List<int>(CastawayV4DefectDiag.ClusterComplete(verts, seed, Eps));
            var block = Complement(verts.Length, thumb);

            Assert.IsTrue(CastawayV4DefectDiag.CoincidenceDisjoint(verts, thumb, block, Eps),
                "cluster-complete sets must share no position with their complement");

            // The thumb's nearest position is (10,0,0) against the block's (1,0,0) => 9 units.
            Assert.AreEqual(9f, MinDistance(verts, thumb, block), 1e-3f,
                "the corrected sets must measure the REAL gap, not 0");
        }

        [Test]
        public void CoincidenceDisjoint_DoesNotFalselyPassOnATightButNonZeroGap_86cau4za2()
        {
            // Guard the guard: a tolerance large enough to swallow real geometry would make every set look
            // disjoint. Two positions one Eps*10 apart must NOT be treated as coincident.
            var verts = new[] { Vector3.zero, Vector3.zero, new Vector3(Eps * 10f, 0f, 0f), new Vector3(Eps * 10f, 0f, 0f) };
            var a = new List<int> { 0, 1 };
            var b = new List<int> { 2, 3 };

            Assert.IsTrue(CastawayV4DefectDiag.CoincidenceDisjoint(verts, a, b, Eps),
                "positions further apart than the tolerance are distinct, not coincident");
            Assert.AreEqual(2, CastawayV4DefectDiag.ClusterComplete(verts, new[] { 0 }, Eps).Count,
                "ClusterComplete must not over-collect across a real gap");
        }
    }
}
