using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The FORGE / FURNACE — the "work half" of the iron earn (ticket 86cakkmvc / I-3 of the iron chain). A NEW
    /// buildable structure DISTINCT from the campfire (Sponsor Q3 locked, DECISIONS 2026-07-06): a stone furnace
    /// the castaway BUILDS from wood + stone (like the campfire, but a separate structure + its own spot), then
    /// SMELTS iron-ore into iron ingots at over a TIMER. The grind (fuel + ore + real seconds) IS the reward gate —
    /// Model A's "work-led earn", not an instant convert.
    ///
    /// === Two-stage state: built, then smelting (mirrors Campfire's built→lit) ===
    /// The forge ships UNBUILT (<see cref="ForgePlacement"/>, gated on wood+stone, builds it; the load-bearing
    /// negative case "not enough mats → no furnace, no debit"). This component owns the BUILT state + the smelt
    /// runtime:
    ///   bool IsBuilt   : true once built. Smelting only runs on a built forge.
    ///   Build()        : raises the forge (ForgePlacement calls it once the mats are paid). Idempotent.
    ///   IsSmelting     : true while a batch is converting; the glow + point Light track this (a forge only glows
    ///                    while it's working — the "life" signal, Bar 2).
    ///
    /// === The smelt loop — one batch at a time, over real elapsed time ===
    /// While BUILT + the player is within <see cref="smeltRadius"/> + the forge is idle + the pack holds enough
    /// ore + fuel, the forge AUTO-BEGINS one smelt: it debits <see cref="orePerIngot"/> iron-ore + <see cref="fuelPerSmelt"/>
    /// fuel (wood) up front, runs a <see cref="smeltSeconds"/> timer, and on completion adds ONE iron-ingot to the
    /// pack; then the next batch begins if mats remain. This is the campfire's proximity idiom (arrive → the station
    /// works), NOT the chop/mine active-click VERB — smelting is a tended STATION, so proximity-auto-tend is the
    /// right model (and is the documented campfire precedent), not a violation of
    /// [[active-input-not-proximity-auto-for-actions]] (which governs one-click-one-strike VERBS). A completed batch
    /// finishes even if the player wanders off (a forge keeps burning); only STARTING a batch needs proximity.
    ///
    /// === The recipe is built FROM the live dials (they can never disagree) ===
    /// <see cref="CurrentRecipe"/> assembles a <see cref="SmeltRecipe"/> (I-0) from the three live smelt-cost dials
    /// (<see cref="orePerIngot"/> / <see cref="fuelPerSmelt"/> / <see cref="smeltSeconds"/>), so the recipe the loop
    /// runs and the values the difficulty dials show are ONE source. The started batch snapshots its recipe so a
    /// mid-smelt dial change never rewrites an in-flight batch.
    ///
    /// === Smelt-cost LIVE dials (spec I-3 — the I-0 extension hooks go live here) ===
    /// The three smelt-cost setting ids (smelt_ore_per_ingot / smelt_fuel_cost / smelt_time) flip LIVE bound to the
    /// three fields below via <see cref="SettingsCatalog.PopulateSmeltLive"/> (the second difficulty dial; the ore-
    /// rarity dial went live on the node ticket I-2). Each is seeded from IronDifficultyPresets.Medium.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The forge structure + this component + the glow/Light children + its Inventory/player refs are authored
    /// editor-time into Boot.unity (MovementCameraScene.BuildForge), NOT at Awake — an Awake-built furnace/glow
    /// could ship MANGLED/absent (the legs-up class). It ships UNBUILT + cold; ForgePlacement raises it, this
    /// component lights the glow while smelting. ForgeSceneTests guards the scene presence + that the refs serialize
    /// (sibling of CampfireSceneTests).
    ///
    /// === The deterministic clock seam (#288 / 86camdk1h — the folded review NIT) ===
    /// The smelt timer reads <see cref="Now"/>, which is <c>Time.time</c> in the shipped build but a TEST-injected
    /// deterministic clock in a PlayMode test (the <see cref="TestClock"/> seam, STRIPPED from ship via
    /// UNITY_INCLUDE_TESTS — the SAME pattern ChopTree uses). Headless -batchmode does NOT honor Time.captureDeltaTime
    /// (proven ineffective in CI), so a smelt-timer PlayMode test OWNS the clock + advances it a fixed step per frame,
    /// making "the timer elapsed" deterministic while the shipped gate logic is byte-unchanged.
    ///
    /// NO MUTABLE STATICS (instance state only) — needs no [RuntimeInitializeOnLoadMethod] reset (StaticStateResetTests).
    /// </summary>
    public class Forge : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger ore + fuel are debited from and the smelted ingots are added to. Wired at bootstrap; " +
                 "scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity (with a built forge + mats) begins a smelt. Wired at " +
                 "bootstrap; falls back to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Tooltip("The warm glow visual shown only while the forge is actively smelting (the firebox glow). Wired " +
                 "at bootstrap; a null is tolerated (the smelt still runs, just no glow).")]
        public GameObject glowVisual;

        [Tooltip("The warm point Light enabled only while smelting (the heat glow into the Zone-D look). Wired at " +
                 "bootstrap; a null is tolerated.")]
        public Light forgeLight;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the forge and (built + with mats) begins a " +
                 "smelt. Mirrors CampfirePlacement/ChopTree radii.")]
        public float smeltRadius = 3.0f;

        [Header("Smelt-cost dials (the smelt_* settings flip these LIVE — I-3 flips the I-0 hooks)")]
        [Tooltip("Raw iron-ore consumed per ingot — the smelt-cost MATERIAL dial. -1 = seed from " +
                 "IronDifficultyPresets.Medium in Awake (so a bare test can override first). Clamped to " +
                 "[SmeltOrePerIngotMin, SmeltOrePerIngotMax]. default from the Medium preset — Sponsor-soak tunes.")]
        public int orePerIngot = -1;

        [Tooltip("Fuel (wood) units consumed per smelt — the smelt-cost FUEL dial. -1 = seed from the Medium preset " +
                 "in Awake. Clamped to [SmeltFuelCostMin, SmeltFuelCostMax]. default from Medium — Sponsor-soak tunes.")]
        public int fuelPerSmelt = -1;

        [Tooltip("Real seconds one smelt takes — the smelt-cost TIME dial (the work-led earn is the WAIT). -1 = seed " +
                 "from the Medium preset in Awake. Clamped to [SmeltTimeMin, SmeltTimeMax]. default from Medium — " +
                 "Sponsor-soak tunes.")]
        public float smeltSeconds = -1f;

        // Build state — the forge ships unbuilt; ForgePlacement raises it once the mats are paid.
        private bool _built;

        // Smelt runtime.
        private bool _smelting;
        private float _smeltEndsAt;
        private float _smeltStartedAt;
        private SmeltRecipe _activeRecipe;   // snapshot of the recipe the in-flight batch runs (dial-change-proof)
        private int _completedSmelts;

        private bool _seeded;

        /// <summary>True once the forge has been built here. Smelting only runs on a built forge.</summary>
        public bool IsBuilt => _built;

        /// <summary>True while a smelt batch is converting (the glow + Light track this).</summary>
        public bool IsSmelting => _smelting;

        /// <summary>How many smelt batches have completed (each = one ingot at the default output count). For tests + capture.</summary>
        public int CompletedSmelts => _completedSmelts;

        /// <summary>Smelt progress 0→1 of the in-flight batch (0 when idle). Reads the deterministic clock.</summary>
        public float SmeltProgress01
        {
            get
            {
                if (!_smelting || _activeRecipe == null) return 0f;
                float dur = Mathf.Max(0.0001f, _activeRecipe.Seconds);
                return Mathf.Clamp01((Now - _smeltStartedAt) / dur);
            }
        }

        /// <summary>The live smelt recipe assembled FROM the three smelt-cost dials — ore→ingot at the current
        /// fuel + seconds. The loop and the dials share this ONE source so they never disagree.</summary>
        public SmeltRecipe CurrentRecipe => new SmeltRecipe(
            ItemCatalog.IronOreId, orePerIngot, ItemCatalog.IronIngotId, 1, fuelPerSmelt, smeltSeconds);

        void Awake()
        {
            SeedDialsFromPresetIfUnset();
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            // Ship cold: the glow does not show until the forge is actively smelting. Defensive — the authored
            // scene already serializes them off, but never assume (the editor-vs-runtime trap).
            ApplySmeltVisuals();
        }

        // Seed the dials from the Medium preset unless a test/inspector already set them (>= 0). The scene author
        // sets explicit values, so the sentinel only fires on a bare test rig.
        private void SeedDialsFromPresetIfUnset()
        {
            if (_seeded) return;
            _seeded = true;
            var med = IronDifficultyPresets.Medium;
            if (orePerIngot < 0) orePerIngot = med.OrePerIngot;
            if (fuelPerSmelt < 0) fuelPerSmelt = med.FuelPerSmelt;
            if (smeltSeconds < 0f) smeltSeconds = med.SecondsPerSmelt;
        }

        /// <summary>
        /// Build (raise) the forge — <see cref="ForgePlacement"/> calls this once the wood + stone are paid.
        /// Idempotent (building an already-built forge is a no-op). A built forge can then smelt.
        /// </summary>
        public void Build()
        {
            if (_built) return;
            _built = true;
            ApplySmeltVisuals();
            Debug.Log("[Forge] BUILT — smelting armed (orePerIngot=" + orePerIngot + " fuelPerSmelt=" +
                      fuelPerSmelt + " smeltSeconds=" + smeltSeconds + ")");
        }

        void Update()
        {
            if (!_built) return;

            // COMPLETE an in-flight batch when its timer elapses (reads the deterministic clock).
            if (_smelting && Now >= _smeltEndsAt)
            {
                CompleteSmelt();
            }

            // BEGIN the next batch when idle + the player is in range + the pack holds enough ore + fuel.
            if (!_smelting && PlayerInRange() && HasSmeltMats(CurrentRecipe))
            {
                BeginSmelt();
            }
        }

        private bool PlayerInRange()
        {
            if (player == null) return false;
            Vector2 forge = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            return Vector2.Distance(forge, here) <= smeltRadius;
        }

        /// <summary>True when the pack holds enough ore + fuel to run the given recipe once (all-or-nothing check).</summary>
        public bool HasSmeltMats(SmeltRecipe r)
        {
            if (r == null || inventory == null || inventory.Model == null) return false;
            return inventory.Model.CountItem(r.InputItemId) >= r.InputCount
                && inventory.WoodCount >= r.FuelCost;
        }

        /// <summary>
        /// Force-begin ONE smelt if the forge is built + idle + the pack holds the mats — the input-independent
        /// seam (range-INDEPENDENT, the analog of MineOre.Mine / RequestMineClick) the PlayMode test + the shipped
        /// capture drive. Returns true if a batch actually started. The regular loop starts batches on proximity;
        /// this lets a headless rig start one without a positioned player.
        /// </summary>
        public bool RequestSmelt()
        {
            if (!_built || _smelting) return false;
            return BeginSmelt();
        }

        // BEGIN one smelt: snapshot the recipe from the live dials, debit the ore + fuel up front (all-or-nothing —
        // we checked HasSmeltMats, so both debits succeed), and arm the timer. Returns true on start.
        private bool BeginSmelt()
        {
            SmeltRecipe r = CurrentRecipe;
            if (!HasSmeltMats(r)) return false;

            // Debit both inputs. Ore via the model (iron_ore is a Resource, like the OrePile loot path); fuel via
            // the wood spend seam (fuel = wood — SmeltRecipe.FuelCost). Both are all-or-nothing; both succeed here.
            bool oreOk = inventory.Model.RemoveItem(r.InputItemId, r.InputCount);
            bool fuelOk = inventory.SpendWood(r.FuelCost);
            if (!oreOk || !fuelOk)
            {
                // Defensive: a race between the check and the debit (should not happen single-threaded) — do not
                // start a half-paid smelt. (No rollback needed: RemoveItem/SpendWood are each all-or-nothing.)
                Debug.LogWarning("[Forge] smelt debit failed after the mats check (oreOk=" + oreOk +
                                 " fuelOk=" + fuelOk + ") — not starting");
                return false;
            }

            _activeRecipe = r;
            _smelting = true;
            _smeltStartedAt = Now;
            _smeltEndsAt = Now + Mathf.Max(0f, r.Seconds);
            ApplySmeltVisuals();
            Debug.Log("[Forge] SMELT started (−" + r.InputCount + " " + r.InputItemId + " −" + r.FuelCost +
                      " fuel; " + r.Seconds.ToString("F1") + "s -> " + r.OutputCount + " " + r.OutputItemId + ")");
            return true;
        }

        // COMPLETE the in-flight batch: add the ingot output to the pack, go idle. The next Update starts the next
        // batch if mats + proximity remain.
        private void CompleteSmelt()
        {
            _smelting = false;
            SmeltRecipe r = _activeRecipe;
            _activeRecipe = null;

            if (r != null && inventory != null && inventory.Catalog != null && inventory.Model != null)
            {
                ItemDef ingot = inventory.Catalog.ById(r.OutputItemId);
                if (ingot != null)
                {
                    inventory.Model.AddItem(ingot, r.OutputCount);
                    _completedSmelts++;
                    Debug.Log("[Forge] SMELT complete (+" + r.OutputCount + " " + r.OutputItemId + " -> " +
                              r.OutputItemId + "=" + inventory.Model.CountItem(r.OutputItemId) +
                              "; batches=" + _completedSmelts + ")");
                }
            }
            ApplySmeltVisuals();
        }

        // Show the glow + enable the point Light only while smelting; both are pre-serialized children (no
        // Awake-built hierarchy). Safe to call repeatedly.
        private void ApplySmeltVisuals()
        {
            if (glowVisual != null) glowVisual.SetActive(_smelting);
            if (forgeLight != null) forgeLight.enabled = _smelting;
        }

        // ============================================================================================
        // Live-dial setters (SettingsCatalog.PopulateSmeltLive binds here). Each clamps to its [Min,Max] band.
        // ============================================================================================

        /// <summary>Set the ore-per-ingot smelt-cost dial (clamped). Bound LIVE to smelt_ore_per_ingot.</summary>
        public void SetOrePerIngot(int v)
            => orePerIngot = Mathf.Clamp(v, SettingsCatalog.SmeltOrePerIngotMin, SettingsCatalog.SmeltOrePerIngotMax);

        /// <summary>Set the fuel-per-smelt smelt-cost dial (clamped). Bound LIVE to smelt_fuel_cost.</summary>
        public void SetFuelPerSmelt(int v)
            => fuelPerSmelt = Mathf.Clamp(v, SettingsCatalog.SmeltFuelCostMin, SettingsCatalog.SmeltFuelCostMax);

        /// <summary>Set the seconds-per-smelt smelt-cost dial (clamped). Bound LIVE to smelt_time.</summary>
        public void SetSmeltSeconds(float v)
            => smeltSeconds = Mathf.Clamp(v, SettingsCatalog.SmeltTimeMin, SettingsCatalog.SmeltTimeMax);

#if UNITY_INCLUDE_TESTS
        /// <summary>86cakkmvc / #288 pattern — TEST-ONLY deterministic clock (public seam, STRIPPED from ship builds
        /// via UNITY_INCLUDE_TESTS; the codebase's "public for tests" convention — mirrors ChopTree.TestClock). The
        /// smelt timer is TIME-SPACED off Time.time; headless -batchmode does NOT honor Time.captureDeltaTime (proven
        /// ineffective in CI), so a PlayMode smelt-timer test injects this fake clock and advances it a fixed step per
        /// frame — a WORKING captureDeltaTime — making "the timer elapsed" deterministic while the shipped gate logic
        /// is byte-unchanged. Null → Time.time (the default), so an unset clock is production-identical even in a test
        /// build.</summary>
        public System.Func<float> TestClock { get; set; }
#endif

        /// <summary>The smelt-timer clock. <c>Time.time</c> in the shipped IL2CPP build (the TestClock seam is
        /// compiled out → a plain Time.time read, production byte-identical); a PlayMode test may override it
        /// deterministically (#288 pattern).</summary>
        private float Now
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (TestClock != null) return TestClock();
#endif
                return Time.time;
            }
        }
    }
}
