namespace FarHorizon
{
    /// <summary>
    /// One difficulty tier's iron-progression preset, expressed as DATA (ticket 86cakkmgw / I-0). The two
    /// Sponsor-locked dials (DECISIONS 2026-07-06 — ore rarity + smelt cost) resolve to concrete numbers
    /// PER TIER here, so the node ticket (I-2) and forge ticket (I-3) read ONE source instead of minting
    /// their own tier tables. Reuses <see cref="SurvivalNeed.DifficultyTier"/> (Easy/Medium/Hard) — the
    /// existing tier enum — rather than a parallel one.
    ///
    /// The four fields are the two dials decomposed:
    ///   • <see cref="OreNodeCount"/>    — the ORE-RARITY dial: iron-ore nodes per island (easy common /
    ///     hard sparse). I-2 flips <c>iron_ore_rarity</c> LIVE against this.
    ///   • <see cref="OrePerIngot"/>     — the SMELT-COST dial (material): raw ore consumed per ingot.
    ///   • <see cref="FuelPerSmelt"/>    — the SMELT-COST dial (fuel): fuel (wood) units per smelt.
    ///   • <see cref="SecondsPerSmelt"/> — the SMELT-COST dial (time): real seconds a smelt takes.
    /// I-3 flips <c>smelt_ore_per_ingot</c> / <c>smelt_fuel_cost</c> / <c>smelt_time</c> LIVE against these.
    ///
    /// Every number is the AUTHOR'S PREDICTION, flagged `default X — Sponsor-soak tunes` — the soak dials
    /// them, then the dialed values bake back here (the give-him-the-knob-then-bake pattern).
    /// </summary>
    [System.Serializable]
    public struct IronDifficulty
    {
        /// <summary>Iron-ore nodes per island — the ore-rarity dial (easy common / hard sparse).</summary>
        public int OreNodeCount;
        /// <summary>Raw iron-ore consumed per ingot — the smelt-cost material dial.</summary>
        public int OrePerIngot;
        /// <summary>Fuel (wood) units consumed per smelt — the smelt-cost fuel dial.</summary>
        public int FuelPerSmelt;
        /// <summary>Real seconds a smelt takes — the smelt-cost time dial (the work-led earn is the WAIT).</summary>
        public float SecondsPerSmelt;

        public IronDifficulty(int oreNodeCount, int orePerIngot, int fuelPerSmelt, float secondsPerSmelt)
        {
            OreNodeCount = oreNodeCount;
            OrePerIngot = orePerIngot;
            FuelPerSmelt = fuelPerSmelt;
            SecondsPerSmelt = secondsPerSmelt;
        }
    }

    /// <summary>
    /// The three per-tier iron-progression presets (ticket 86cakkmgw / I-0) — the DATA the two difficulty
    /// dials seed from. EASY = ore common / cheap-fast smelt; MEDIUM = the balanced default; HARD = ore
    /// sparse / fuel-and-time-costly smelt (DECISIONS 2026-07-06: both dials, per-tier easy/med/hard).
    ///
    /// Every value is a PREDICTION (`default X — Sponsor-soak tunes`); the node/forge soaks dial them and
    /// bake back here. Monotonic by design across the earn axis: node count Easy&gt;Med&gt;Hard (rarer =
    /// harder); ore-per-ingot / fuel / seconds Easy&lt;Med&lt;Hard (costlier = harder).
    /// </summary>
    public static class IronDifficultyPresets
    {
        // default X — Sponsor-soak tunes (all four values, all three tiers).
        public static readonly IronDifficulty Easy   = new IronDifficulty(oreNodeCount: 24, orePerIngot: 1, fuelPerSmelt: 1, secondsPerSmelt: 5f);
        public static readonly IronDifficulty Medium = new IronDifficulty(oreNodeCount: 14, orePerIngot: 2, fuelPerSmelt: 2, secondsPerSmelt: 12f);
        public static readonly IronDifficulty Hard   = new IronDifficulty(oreNodeCount: 8,  orePerIngot: 3, fuelPerSmelt: 4, secondsPerSmelt: 25f);

        /// <summary>Resolve the preset for a difficulty tier (Medium is the default/fallback).</summary>
        public static IronDifficulty For(SurvivalNeed.DifficultyTier tier)
        {
            switch (tier)
            {
                case SurvivalNeed.DifficultyTier.Easy: return Easy;
                case SurvivalNeed.DifficultyTier.Hard: return Hard;
                default: return Medium;
            }
        }
    }
}
