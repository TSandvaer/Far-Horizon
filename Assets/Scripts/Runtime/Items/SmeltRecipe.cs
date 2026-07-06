using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A smelting recipe (ticket 86cakkmgw / I-0 — the shared iron vocabulary, §1). Expresses one smelt as
    /// INPUT (an item id + count) → OUTPUT (an item id + count) at a FUEL cost over a duration in SECONDS:
    /// the forge ticket (I-3) consumes ore + fuel, runs the timer, and yields ingots. Model A's "work-led
    /// earn" lives in the fuel + seconds, not in an instant convert.
    ///
    /// A plain <c>[Serializable]</c> value record (NOT a ScriptableObject) so it round-trips in pure-logic
    /// EditMode with no .asset — I-0 is the non-build data foundation. I-3 holds instances (a smelt queue is
    /// fine) built from the difficulty preset via <see cref="IronIngot"/>. Get-only properties mirror the
    /// ItemDef/WeaponDef immutable-identity style; the SerializeField backing fields let a future ticket
    /// author a recipe in the Inspector if it ever wants to.
    /// </summary>
    [System.Serializable]
    public sealed class SmeltRecipe
    {
        [SerializeField, Tooltip("The consumed input item id (e.g. iron_ore).")]
        private string _inputItemId;
        [SerializeField, Tooltip("How many input items one smelt consumes.")]
        private int _inputCount;
        [SerializeField, Tooltip("The produced output item id (e.g. iron_ingot).")]
        private string _outputItemId;
        [SerializeField, Tooltip("How many output items one smelt yields.")]
        private int _outputCount;
        [SerializeField, Tooltip("Fuel (wood) units one smelt consumes — the smelt-cost fuel dial.")]
        private int _fuelCost;
        [SerializeField, Tooltip("Real seconds one smelt takes — the smelt-cost time dial (the earn is the wait).")]
        private float _seconds;

        /// <summary>The consumed input item id (e.g. <see cref="ItemCatalog.IronOreId"/>).</summary>
        public string InputItemId => _inputItemId;
        /// <summary>How many input items one smelt consumes.</summary>
        public int InputCount => _inputCount;
        /// <summary>The produced output item id (e.g. <see cref="ItemCatalog.IronIngotId"/>).</summary>
        public string OutputItemId => _outputItemId;
        /// <summary>How many output items one smelt yields.</summary>
        public int OutputCount => _outputCount;
        /// <summary>Fuel (wood) units one smelt consumes — the smelt-cost fuel dial.</summary>
        public int FuelCost => _fuelCost;
        /// <summary>Real seconds one smelt takes — the smelt-cost time dial.</summary>
        public float Seconds => _seconds;

        public SmeltRecipe(string inputItemId, int inputCount, string outputItemId, int outputCount,
            int fuelCost, float seconds)
        {
            _inputItemId = inputItemId;
            _inputCount = inputCount;
            _outputItemId = outputItemId;
            _outputCount = outputCount;
            _fuelCost = fuelCost;
            _seconds = seconds;
        }

        /// <summary>
        /// The canonical iron ore→ingot recipe for a difficulty preset: consumes
        /// <see cref="IronDifficulty.OrePerIngot"/> <see cref="ItemCatalog.IronOreId"/> + the preset's
        /// <see cref="IronDifficulty.FuelPerSmelt"/> fuel over <see cref="IronDifficulty.SecondsPerSmelt"/>
        /// seconds, yielding ONE <see cref="ItemCatalog.IronIngotId"/>. The forge ticket (I-3) builds its
        /// live recipe from the smelt-cost dials through this factory so the dials and the recipe never
        /// disagree.
        /// </summary>
        public static SmeltRecipe IronIngot(IronDifficulty preset)
            => new SmeltRecipe(ItemCatalog.IronOreId, preset.OrePerIngot,
                               ItemCatalog.IronIngotId, 1, preset.FuelPerSmelt, preset.SecondsPerSmelt);
    }
}
