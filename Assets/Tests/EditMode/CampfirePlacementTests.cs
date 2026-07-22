using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the ⑤ campfire place-to-build seam (ticket 86camz9w7) that does NOT need a scene or the
    /// Awake lifecycle:
    ///   • the IBuildPlaceable identity the build menu (86catpvpa) keys on (label + costs + not-yet-built);
    ///   • the cost re-alignment to the vision's "stone AND wood" (NIT-3 — wood AND stone, campfire the cheapest);
    ///   • the all-or-nothing affordability truth-table at the campfire's costs (short of EITHER mat → refused).
    /// The pure placement-validity / rotation / plane helpers are shared VERBATIM with the ① table and covered by
    /// CraftingTablePlacementTests (constraint 1 — no parallel flow); the debit→reveal→light end-to-end is
    /// CampfirePlayModeTests. This class pins only the campfire-SPECIFIC seam.
    /// </summary>
    public class CampfirePlacementTests
    {
        [Test]
        public void CostDefaults_AreWoodAndStone_AndTheCheapestStructure()
        {
            // NIT-3: the campfire costs wood AND stone (the vision + its own mesh — a ring of fire-STONES around
            // crossed LOGS), re-aligned from the 3-wood-only baseline.
            Assert.AreEqual(3, CampfirePlacement.CampfireWoodCostDefault, "campfire wood default = 3 (the §5 baseline)");
            Assert.AreEqual(2, CampfirePlacement.CampfireStoneCostDefault, "campfire stone default = 2 (NIT-3)");
            Assert.Greater(CampfirePlacement.CampfireWoodCostDefault, 0, "the campfire must cost wood");
            Assert.Greater(CampfirePlacement.CampfireStoneCostDefault, 0,
                "the campfire must cost STONE too — the vision's 'stone AND wood' (NIT-3)");

            // Design guard: the campfire is the CHEAPEST structure (a basic open fire vs a stone furnace) — total
            // mats below the forge's (6+12). Pins the cost family so a future retune can't quietly invert it.
            int campfireTotal = CampfirePlacement.CampfireWoodCostDefault + CampfirePlacement.CampfireStoneCostDefault;
            int forgeTotal = ForgePlacement.ForgeWoodCostDefault + ForgePlacement.ForgeStoneCostDefault;
            Assert.Less(campfireTotal, forgeTotal,
                "the campfire must cost fewer total mats than the forge (a basic fire is cheaper than a stone furnace)");
        }

        [Test]
        public void IBuildPlaceable_Identity_LabelCostsAndNotYetBuilt()
        {
            var go = new GameObject("CampfirePlacementProbe");
            try
            {
                var place = go.AddComponent<CampfirePlacement>();
                IBuildPlaceable row = place;

                Assert.AreEqual("Campfire", row.BuildDisplayName,
                    "the build-menu row label + per-structure identifier must be 'Campfire'");
                Assert.AreEqual(CampfirePlacement.CampfireWoodCostDefault, row.BuildWoodCost,
                    "the menu row's wood cost mirrors the placement's woodCost default");
                Assert.AreEqual(CampfirePlacement.CampfireStoneCostDefault, row.BuildStoneCost,
                    "the menu row's stone cost mirrors the placement's stoneCost default (NIT-3 'stone AND wood')");
                Assert.IsFalse(row.IsBuildComplete,
                    "a fresh campfire placement is NOT built (the menu row stays selectable until placed)");
                Assert.IsFalse(row.CanAffordBuild,
                    "with no Inventory wired the row reads unaffordable (greyed + non-interactive)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- NIT-3: all-or-nothing affordability at the campfire's wood+stone cost (the shared ForgePlacement seam) ----

        [Test]
        public void Afford_WhenBothMatsMet_True()
        {
            Assert.IsTrue(ForgePlacement.CanAfford(wood: 3, stone: 2, woodCost: 3, stoneCost: 2),
                "exactly 3 wood + 2 stone affords the campfire");
            Assert.IsTrue(ForgePlacement.CanAfford(wood: 9, stone: 9, woodCost: 3, stoneCost: 2),
                "a surplus of both mats affords the campfire");
        }

        [Test]
        public void Afford_ShortOfEitherMat_False()
        {
            Assert.IsFalse(ForgePlacement.CanAfford(wood: 2, stone: 2, woodCost: 3, stoneCost: 2),
                "2 wood < cost 3 → NOT affordable (all-or-nothing: short wood refuses even with the stone)");
            Assert.IsFalse(ForgePlacement.CanAfford(wood: 3, stone: 1, woodCost: 3, stoneCost: 2),
                "1 stone < cost 2 → NOT affordable (all-or-nothing: short stone refuses even with the wood)");
            Assert.IsFalse(ForgePlacement.CanAfford(wood: 0, stone: 0, woodCost: 3, stoneCost: 2),
                "empty-handed → NOT affordable");
        }
    }
}
