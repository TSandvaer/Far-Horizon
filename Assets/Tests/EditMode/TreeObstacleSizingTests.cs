using NUnit.Framework;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the TREE-BARRIER FIX (ticket 86caa4c5c CHANGE 2 — the Sponsor's "invisible barrier
    /// MUCH larger than the trunk; can't walk up close", worst on the TALL scatter trees).
    ///
    /// ROOT CAUSE (pinned by this guard): a scatter tree's NavMeshObstacle.radius is a LOCAL value, multiplied
    /// by the tree's localScale (scale 1.0..2.4). The OLD fixed `tall ? 0.5 : 0.4` therefore carved an
    /// effective WORLD radius of radius×scale — up to 0.5×2.4 = 1.2u for a tall tree whose trunk is only
    /// botR×scale ≈ 0.53u (a carve > 2× the trunk silhouette), plus the bake erodes the walkable boundary by
    /// the agent radius (0.4u). The player stopped ~1.6u from a ~0.5u-wide trunk — the reported barrier.
    ///
    /// FIX: <see cref="LowPolyZoneGen.TrunkObstacleLocalRadius"/> sizes the LOCAL radius so the WORLD footprint
    /// == the trunk's WORLD radius + a small fixed clearance at EVERY scale (the carve no longer grows with
    /// scale). This guards the BUG CLASS:
    ///   1. effective WORLD carve == trunk world radius + TrunkObstacleClearance (NOT radius×scale unbounded);
    ///   2. a tall tree's carve is a SNUG collar (≤ ~1.3× its trunk), not the old > 2× barrier;
    ///   3. the carve is scale-stable: the WORLD clearance past the trunk is identical for a small and a tall
    ///      tree (the OLD fixed local radius made the tall tree's clearance balloon).
    /// </summary>
    public class TreeObstacleSizingTests
    {
        // The scatter-tree trunk bottom radii (LowPolyZoneGen.BuildTree): tall 0.22, mid 0.18.
        private const float TallBotR = 0.22f;
        private const float MidBotR = 0.18f;
        // The scatter scale bands (LowPolyZoneGen.ScatterIslandProps): tall 1.5..2.4, mid 1.0..1.6.
        private const float TallScaleMax = 2.4f;
        private const float MidScaleMax = 1.6f;

        [Test]
        public void EffectiveWorldCarve_EqualsTrunkRadiusPlusClearance_AtEveryScale()
        {
            // The whole point of the fix: localRadius × scale == trunkWorldRadius + clearance, for any scale.
            foreach (float scale in new[] { 1.0f, 1.6f, 2.0f, 2.4f })
            {
                float localR = LowPolyZoneGen.TrunkObstacleLocalRadius(TallBotR, scale);
                float worldCarve = localR * scale;
                float trunkWorld = TallBotR * scale;
                Assert.AreEqual(trunkWorld + LowPolyZoneGen.TrunkObstacleClearance, worldCarve, 1e-4f,
                    $"at scale {scale} the effective WORLD carve must be the trunk world radius + a fixed " +
                    "clearance (NOT radius×scale unbounded — the tall-tree barrier root cause)");
            }
        }

        [Test]
        public void TallTreeCarve_IsASnugCollar_NotMoreThanTwiceTheTrunk()
        {
            // The reported symptom: the carve was > 2× the trunk silhouette on tall trees. The fix keeps the
            // carve a snug collar — world carve / trunk world radius ≈ 1 + clearance/trunkWorld, comfortably
            // under 2× even at the LARGEST tall scale.
            float worldCarve = LowPolyZoneGen.TrunkObstacleLocalRadius(TallBotR, TallScaleMax) * TallScaleMax;
            float trunkWorld = TallBotR * TallScaleMax;
            Assert.Less(worldCarve, 2f * trunkWorld,
                "a tall tree's carve must be a SNUG collar on the trunk, NOT the old > 2× barrier");
            Assert.Greater(worldCarve, trunkWorld,
                "the carve must still cover the trunk (the agent paths AROUND it — not a free walk-through)");

            // Concretely: the OLD fixed local 0.5 carved 0.5×2.4 = 1.2u; the new carve is far smaller.
            Assert.Less(worldCarve, 0.5f * TallScaleMax,
                "the new tall-tree carve must be well under the OLD 0.5×scale = 1.2u barrier");
        }

        [Test]
        public void WorldClearancePastTrunk_IsScaleStable_SmallVsTall()
        {
            // The clearance the player gets past the visible trunk must be the SAME world distance for a small
            // mid tree and a big tall tree (the OLD fixed local radius made the tall tree's clearance balloon).
            float tallCarve = LowPolyZoneGen.TrunkObstacleLocalRadius(TallBotR, TallScaleMax) * TallScaleMax;
            float tallClearance = tallCarve - TallBotR * TallScaleMax;

            float midCarve = LowPolyZoneGen.TrunkObstacleLocalRadius(MidBotR, 1.0f) * 1.0f;
            float midClearance = midCarve - MidBotR * 1.0f;

            Assert.AreEqual(midClearance, tallClearance, 1e-4f,
                "the WORLD clearance past the trunk must be identical at any scale (a fixed collar) — " +
                "the OLD radius×scale grew it with scale, which is the tall-tree barrier");
            Assert.AreEqual(LowPolyZoneGen.TrunkObstacleClearance, tallClearance, 1e-4f,
                "the clearance is exactly TrunkObstacleClearance (the single named source)");
        }

        [Test]
        public void DegenerateScale_DoesNotDivideByZero()
        {
            float r = LowPolyZoneGen.TrunkObstacleLocalRadius(MidBotR, 0f);
            Assert.IsFalse(float.IsNaN(r) || float.IsInfinity(r),
                "a degenerate 0 scale must be clamped (no divide-by-zero) — the radius stays finite");
            Assert.Greater(r, 0f, "the obstacle radius stays positive for a degenerate scale");
        }
    }
}
