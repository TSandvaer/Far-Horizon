using NUnit.Framework;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// I-0 foundation guard (ticket 86cakkmgw) — the SmeltRecipe data type round-trips input→output (ore count
    /// in, ingot count out, fuel + seconds) and the IronIngot(preset) factory ties the recipe to the difficulty
    /// dials so they can never disagree. Pure value type, no Unity scene.
    ///
    /// Guards BITE: a swapped field, a factory that reads the wrong preset member, or a preset table that isn't
    /// monotonic across the earn axis turns these red.
    /// </summary>
    public class SmeltRecipeTests
    {
        [Test]
        public void SmeltRecipe_RoundTripsInputToOutput()
        {
            var r = new SmeltRecipe("iron_ore", 3, "iron_ingot", 1, 2, 12f);
            Assert.AreEqual("iron_ore", r.InputItemId);
            Assert.AreEqual(3, r.InputCount);
            Assert.AreEqual("iron_ingot", r.OutputItemId);
            Assert.AreEqual(1, r.OutputCount);
            Assert.AreEqual(2, r.FuelCost);
            Assert.AreEqual(12f, r.Seconds, 1e-4f);
        }

        [Test]
        public void IronIngotFactory_BindsRecipeToThePreset()
        {
            var med = IronDifficultyPresets.Medium;
            var r = SmeltRecipe.IronIngot(med);

            Assert.AreEqual(ItemCatalog.IronOreId, r.InputItemId, "consumes iron_ore");
            Assert.AreEqual(med.OrePerIngot, r.InputCount, "ore-per-ingot comes from the preset (smelt-cost dial)");
            Assert.AreEqual(ItemCatalog.IronIngotId, r.OutputItemId, "yields iron_ingot");
            Assert.AreEqual(1, r.OutputCount, "one ingot per smelt");
            Assert.AreEqual(med.FuelPerSmelt, r.FuelCost, "fuel comes from the preset (smelt-cost dial)");
            Assert.AreEqual(med.SecondsPerSmelt, r.Seconds, 1e-4f, "seconds come from the preset (smelt-cost dial)");
        }

        [Test]
        public void Presets_ResolvePerTier_AndAreMonotonicAcrossTheEarnAxis()
        {
            var easy = IronDifficultyPresets.For(SurvivalNeed.DifficultyTier.Easy);
            var med = IronDifficultyPresets.For(SurvivalNeed.DifficultyTier.Medium);
            var hard = IronDifficultyPresets.For(SurvivalNeed.DifficultyTier.Hard);

            // The 'For' switch resolves the right preset (Medium is the default/fallback).
            Assert.AreEqual(IronDifficultyPresets.Easy.OreNodeCount, easy.OreNodeCount);
            Assert.AreEqual(IronDifficultyPresets.Medium.OreNodeCount, med.OreNodeCount);
            Assert.AreEqual(IronDifficultyPresets.Hard.OreNodeCount, hard.OreNodeCount);

            // Harder = ore RARER (fewer nodes) and smelt COSTLIER (more ore/fuel/time). The dials' whole point.
            Assert.Greater(easy.OreNodeCount, med.OreNodeCount, "easy has MORE ore nodes than medium (common)");
            Assert.Greater(med.OreNodeCount, hard.OreNodeCount, "hard has FEWER ore nodes than medium (sparse)");
            Assert.LessOrEqual(easy.OrePerIngot, hard.OrePerIngot, "hard costs at least as much ore per ingot");
            Assert.LessOrEqual(easy.FuelPerSmelt, hard.FuelPerSmelt, "hard costs at least as much fuel");
            Assert.Less(easy.SecondsPerSmelt, hard.SecondsPerSmelt, "hard smelts SLOWER (the work-led earn)");
        }
    }
}
