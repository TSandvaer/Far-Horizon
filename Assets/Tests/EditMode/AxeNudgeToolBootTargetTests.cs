using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cahnmjv deliverable 3 — the F9 weapon-nudge tool must BOOT on the ARM-POSE target, not the
    /// RUN-target zero-engagement trap. The RUN target (index 4) is INERT at walk/idle (run weight 0), so
    /// booting there — or landing there while Tab-hunting — reads as a dead tool until the Sponsor happens to
    /// be running (he burned two sessions on exactly this). The arm target (index 2) engages immediately at
    /// idle and is the one he dials most (it produced the baked RightArmEuler/LeftArmEuler). This pins the
    /// boot default so a future edit that changes it (or reverts to the held/RUN target) reds headlessly.
    /// </summary>
    public class AxeNudgeToolBootTargetTests
    {
        [Test]
        public void Boots_OnTheArmPoseTarget_NotTheRunZeroEngagementTrap()
        {
            var go = new GameObject("AxeNudgeToolBootTargetTest");
            try
            {
                var tool = go.AddComponent<AxeNudgeTool>();
                Assert.AreEqual(AxeNudgeTool.ArmTargetIndex, tool.ActiveTargetIndex,
                    "the F9 tool must boot on the ARM-POSE target (index 2) so it engages at idle — not the " +
                    "held (0) default nor the RUN-target zero-engagement trap (4)");
                Assert.AreEqual(2, AxeNudgeTool.ArmTargetIndex, "the arm-pose target index is 2 (contract)");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
