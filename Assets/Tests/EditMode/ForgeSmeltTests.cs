using NUnit.Framework;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Pure-logic EditMode guards for the I-3 forge/smelt foundation (ticket 86cakkmvc) — no scene, no .asset:
    /// the ForgePlacement affordability truth-table, the SmeltRecipe the smelt-cost preset seeds, and the
    /// monotonic difficulty-preset map. Sibling of the I-0 ItemCatalog tests + MineOre's ShouldMineOnClick table.
    ///
    /// REGRESSION GUARD: <see cref="CanAfford_IsAllOrNothing_AcrossBothMats"/> reds if the forge build gate ever
    /// stops requiring BOTH wood AND stone (the "not enough mats → no furnace" negative case, the load-bearing
    /// campfire-gate sibling). Break the gate to `wood || stone` (or drop the stone half) and this goes red.
    /// </summary>
    public class ForgeSmeltTests
    {
        // === ForgePlacement.CanAfford — the all-or-nothing wood+stone build gate (pure truth-table) ===

        [Test]
        public void CanAfford_IsAllOrNothing_AcrossBothMats()
        {
            const int woodCost = 4, stoneCost = 5;

            // Both sufficient → affordable.
            Assert.IsTrue(ForgePlacement.CanAfford(4, 5, woodCost, stoneCost), "exactly enough of both affords");
            Assert.IsTrue(ForgePlacement.CanAfford(9, 9, woodCost, stoneCost), "surplus of both affords");

            // Either short → NOT affordable (the load-bearing negative case).
            Assert.IsFalse(ForgePlacement.CanAfford(3, 5, woodCost, stoneCost), "1 wood short → no build");
            Assert.IsFalse(ForgePlacement.CanAfford(4, 4, woodCost, stoneCost), "1 stone short → no build");
            Assert.IsFalse(ForgePlacement.CanAfford(0, 9, woodCost, stoneCost), "no wood → no build even with stone");
            Assert.IsFalse(ForgePlacement.CanAfford(9, 0, woodCost, stoneCost), "no stone → no build even with wood");
            Assert.IsFalse(ForgePlacement.CanAfford(0, 0, woodCost, stoneCost), "nothing → no build");
        }

        [Test]
        public void CanAfford_ZeroCost_IsTriviallyAffordable()
        {
            Assert.IsTrue(ForgePlacement.CanAfford(0, 0, 0, 0), "a zero-cost forge is affordable from an empty pack");
        }

        // === SmeltRecipe from the difficulty presets — ore→ingot with the right fuel + seconds ===

        [Test]
        public void SmeltRecipe_FromMediumPreset_RoundTripsOreToIngot()
        {
            var med = IronDifficultyPresets.Medium;
            SmeltRecipe r = SmeltRecipe.IronIngot(med);

            Assert.AreEqual(ItemCatalog.IronOreId, r.InputItemId, "the smelt input is iron ore");
            Assert.AreEqual(med.OrePerIngot, r.InputCount, "ore-in count = the preset's ore-per-ingot");
            Assert.AreEqual(ItemCatalog.IronIngotId, r.OutputItemId, "the smelt output is an iron ingot");
            Assert.AreEqual(1, r.OutputCount, "one smelt yields ONE ingot");
            Assert.AreEqual(med.FuelPerSmelt, r.FuelCost, "fuel cost = the preset's fuel-per-smelt");
            Assert.AreEqual(med.SecondsPerSmelt, r.Seconds, "seconds = the preset's seconds-per-smelt (the earn IS the wait)");
        }

        [Test]
        public void SmeltCostPresets_AreMonotonic_HarderCostsMore()
        {
            var e = IronDifficultyPresets.Easy;
            var m = IronDifficultyPresets.Medium;
            var h = IronDifficultyPresets.Hard;

            // The SMELT-COST axis (material / fuel / time) rises Easy < Medium < Hard — costlier = harder.
            Assert.Less(e.OrePerIngot, m.OrePerIngot, "easy ore-per-ingot < medium");
            Assert.Less(m.OrePerIngot, h.OrePerIngot, "medium ore-per-ingot < hard");
            Assert.Less(e.FuelPerSmelt, m.FuelPerSmelt, "easy fuel < medium");
            Assert.Less(m.FuelPerSmelt, h.FuelPerSmelt, "medium fuel < hard");
            Assert.Less(e.SecondsPerSmelt, m.SecondsPerSmelt, "easy smelt-time < medium");
            Assert.Less(m.SecondsPerSmelt, h.SecondsPerSmelt, "medium smelt-time < hard");
        }

        [Test]
        public void SmeltCostDefaults_AreWithinTheRegisteredSettingBands()
        {
            // Every preset value the smelt-cost dials seed from must sit inside the SettingsCatalog [Min,Max] band
            // the live dial clamps to — else the seeded default is out of the dial's own range.
            foreach (var p in new[] { IronDifficultyPresets.Easy, IronDifficultyPresets.Medium, IronDifficultyPresets.Hard })
            {
                Assert.GreaterOrEqual(p.OrePerIngot, SettingsCatalog.SmeltOrePerIngotMin);
                Assert.LessOrEqual(p.OrePerIngot, SettingsCatalog.SmeltOrePerIngotMax);
                Assert.GreaterOrEqual(p.FuelPerSmelt, SettingsCatalog.SmeltFuelCostMin);
                Assert.LessOrEqual(p.FuelPerSmelt, SettingsCatalog.SmeltFuelCostMax);
                Assert.GreaterOrEqual(p.SecondsPerSmelt, SettingsCatalog.SmeltTimeMin);
                Assert.LessOrEqual(p.SecondsPerSmelt, SettingsCatalog.SmeltTimeMax);
            }
        }
    }
}
