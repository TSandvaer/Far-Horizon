using NUnit.Framework;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the pure placement-validity truth-table (ticket 86camz9uz / crafting-redesign ① —
    /// the place-to-build ghost's valid/invalid read, spec §2). No scene needed — the decision is a static.
    /// </summary>
    public class CraftingTablePlacementTests
    {
        private const float MinDist = 1.0f;
        private const float MinNormalY = 0.85f;

        [Test]
        public void Valid_OnFlatGround_FarEnough()
        {
            Assert.IsTrue(CraftingTablePlacement.IsValidPlacement(
                groundFound: true, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY),
                "flat ground, ghost 2.5u out → valid");
        }

        [Test]
        public void Invalid_NoGround()
        {
            Assert.IsFalse(CraftingTablePlacement.IsValidPlacement(
                groundFound: false, normalY: 1.0f, distFromPlayer: 2.5f, MinDist, MinNormalY),
                "no ground under the ghost (over water / off the island edge) → BLOCKED");
        }

        [Test]
        public void Invalid_TooSteep()
        {
            Assert.IsFalse(CraftingTablePlacement.IsValidPlacement(
                groundFound: true, normalY: 0.50f, distFromPlayer: 2.5f, MinDist, MinNormalY),
                "a steep slope (normalY 0.5 < 0.85) → BLOCKED (a table can't stand on a cliff)");
        }

        [Test]
        public void Invalid_TooCloseToPlayer()
        {
            Assert.IsFalse(CraftingTablePlacement.IsValidPlacement(
                groundFound: true, normalY: 1.0f, distFromPlayer: 0.5f, MinDist, MinNormalY),
                "ghost right on top of the player (0.5 < 1.0) → BLOCKED (never build on self)");
        }
    }
}
