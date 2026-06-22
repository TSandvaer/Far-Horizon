using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A harvestable BERRY BUSH (ticket 86caa5zz3) — the world food source for the merged hunger loop.
    /// The castaway walks up to a RIPE berry bush and HARVESTS its berries (proximity + interact, no tool
    /// required — the stone-pickup idiom, NOT the axe-gated chop): the berries go to the inventory as a
    /// stackable <c>berry</c> resource (<see cref="InventoryModel.AddItem"/> against the catalog's berry
    /// <see cref="ItemDef"/> — it never mints a parallel item type, per item-model contract §9). The BUSH
    /// PERSISTS; only the BERRIES deplete on harvest and REGROW after a tweakable delay (AC4).
    ///
    /// === The interaction (AC3) — proximity, no tool, mirrors ChopTree's proven seam ===
    /// Polling planar XZ distance to the player root each Update is the robust "the castaway reached the
    /// bush" seam (same as ChopTree / CraftSpot — onArrived is fragile, see ChopTree's note). Reaching a
    /// RIPE bush harvests once (edge-triggered: one harvest per arrival, re-arms when the player leaves).
    /// Unlike ChopTree there is NO axe gate — berries are picked by hand.
    ///
    /// === Regrowth (AC4) — bush persists, berries deplete + regrow, TWEAKABLE within [min,max] ===
    /// On harvest the bush goes BARE (berries hidden) and schedules a regrow at a RANDOM time within
    /// [<see cref="regrowMinSeconds"/>, <see cref="regrowMaxSeconds"/>] (like tree-regrowth / stone-respawn).
    /// When the timer elapses the berries return (RIPE again) and can be harvested anew. The min/max are
    /// serialized tweakable fields (the data side); registering a `berry regrowth time` SETTING in the dev
    /// settings panel is a FOLLOW-UP — no settings/dev-tweak panel exists on main yet (gated on the panel
    /// foundation; memory `sponsor-wants-unified-dev-tweak-console`). NOT a mid-PR scope expansion.
    ///
    /// === Eat (AC5 / AC5a / AC5b) — CONSUME side only ===
    /// <see cref="EatBerry"/> consumes exactly ONE berry from the inventory (all-or-nothing) and, IF a
    /// <see cref="HungerNeed"/> is present, restores hunger via its atomic seam
    /// <see cref="HungerNeed.TryEatBerry"/>; otherwise it just consumes (AC5b graceful no-HungerNeed — no
    /// null-ref). What eating RESTORES (the hunger amount) is OWNED by 86caamkp8 (HungerNeed.AddFood); this
    /// ticket owns ONLY the consume side. The ATOMIC eat-&gt;hunger test lives in 86caamkp8 (it owns
    /// AddFood) — this ticket does NOT duplicate it (AC5a — that duplication IS the dual-spawn gap).
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The bush GameObject + this component + its berries visual + Inventory/player refs are authored
    /// editor-time into Boot.unity (MovementCameraScene.BuildBerryBush), NOT at Awake — an Awake-built
    /// interaction/visual could ship MANGLED/absent (the legs-up class). BushSceneTests guards the scene
    /// presence + that the refs serialize, sibling of ChopSceneTests.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[bush-trace]` lines on harvest + regrow + eat so the bush's runtime state is readable
    /// from the build log (the diagnose-via-trace discipline; sibling of the [world-trace] scatter lines).
    /// </summary>
    public class BerryBush : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory harvested berries are added to. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity triggers a harvest. Wired at bootstrap; falls " +
                 "back to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Tooltip("The berries visual root — shown when RIPE, hidden when BARE (regrowing). Wired at " +
                 "bootstrap. If unwired the harvest still works (berries just don't visibly toggle).")]
        public Transform berriesVisual;

        [Header("Variant")]
        [Tooltip("Only a berry-bush variant carries berries (AC3). A plain (decorative) bush sets this " +
                 "false — it never yields berries and is never harvestable. The world scatter mixes both.")]
        public bool hasBerries = true;

        [Header("Harvest (AC3)")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the bush and harvests. Generous " +
                 "(reward for arriving), mirrors ChopTree.chopRadius / CraftSpot.craftRadius.")]
        public float harvestRadius = 2.0f;

        [Tooltip("Berries yielded per harvest. Small so a single bush is a top-up, not a stockpile — the " +
                 "castaway forages across several bushes (vision: 'harvest some').")]
        public int berriesPerHarvest = 3;

        [Header("Regrowth (AC4 — TWEAKABLE; settings-panel registration is a follow-up)")]
        [Tooltip("Minimum seconds before harvested berries regrow. The actual regrow time is RANDOM in " +
                 "[min,max] (like tree-regrowth / stone-respawn). Tweakable; a future `berry regrowth " +
                 "time` setting will drive these.")]
        public float regrowMinSeconds = 20f;

        [Tooltip("Maximum seconds before harvested berries regrow (random within [min,max]).")]
        public float regrowMaxSeconds = 40f;

        [Tooltip("Deterministic seed for the regrow-time roll (so headless tests are reproducible). 0 = " +
                 "use a time-based seed at runtime.")]
        public int regrowSeed = 0;

        // Runtime state.
        private bool _ripe = true;          // true = berries present + harvestable; false = bare, regrowing
        private bool _atBush;               // was the player in range last frame (edge-trigger re-arm)
        private float _regrowAt;            // wall-clock time the berries regrow (when bare)
        private System.Random _rng;
        private bool _tracedFirstHarvest;   // one-shot trace guards (don't spam the log per frame)

        /// <summary>True when berries are present + harvestable. False while regrowing (bare). Exposed for
        /// PlayMode tests + the visual toggle.</summary>
        public bool IsRipe => _ripe;

        /// <summary>Wall-clock time the berries are scheduled to regrow (only meaningful while bare).</summary>
        public float RegrowAt => _regrowAt;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            _rng = new System.Random(regrowSeed != 0 ? regrowSeed : Environment.TickCount);
            ApplyRipeVisual();
        }

        void Update()
        {
            // Regrow the berries when the timer elapses (bush persists; only berries toggle — AC4).
            if (hasBerries && !_ripe && Time.time >= _regrowAt)
            {
                Regrow();
            }

            if (!hasBerries || inventory == null || player == null) return;

            // Planar XZ distance only — height-robust, same as ChopTree / CraftSpot.
            Vector2 bush = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            bool inRange = Vector2.Distance(bush, here) <= harvestRadius;

            if (!inRange)
            {
                _atBush = false;   // re-arm: a fresh arrival can harvest again (once berries are ripe)
                return;
            }

            // Edge-triggered: harvest ONCE per arrival at a ripe bush (not every frame in range).
            if (!_atBush)
            {
                _atBush = true;
                if (_ripe) Harvest();
            }
        }

        /// <summary>
        /// Harvest the ripe berries: add <see cref="berriesPerHarvest"/> <c>berry</c> resources to the
        /// inventory (the item-model AddItem seam — stacks per the resource stack-size), then go BARE and
        /// schedule regrowth. Returns the number of berries that ACTUALLY landed in the inventory (less
        /// than the yield only if the pack is full). A no-op (returns 0) if not ripe / not a berry bush /
        /// no inventory. Public so PlayMode tests can drive it directly (isolating it from pathfinding).
        /// </summary>
        public int Harvest()
        {
            if (!hasBerries || !_ripe || inventory == null) return 0;

            var catalog = inventory.Catalog;
            ItemDef berry = catalog != null ? catalog.ById(ItemCatalog.BerryId) : null;
            if (berry == null) return 0;

            int leftover = inventory.Model.AddItem(berry, berriesPerHarvest);
            int added = berriesPerHarvest - leftover;

            _ripe = false;
            ApplyRipeVisual();
            ScheduleRegrow();

            if (!_tracedFirstHarvest)
            {
                _tracedFirstHarvest = true;
                Debug.Log("[bush-trace] Harvest +" + added + " berry (yield=" + berriesPerHarvest +
                          ", leftover=" + leftover + ") -> berries=" +
                          inventory.Model.CountItem(ItemCatalog.BerryId) +
                          "; bush BARE, regrow in " + (_regrowAt - Time.time).ToString("F1") + "s");
            }
            return added;
        }

        /// <summary>
        /// EAT one berry (AC5 — CONSUME side, owned here). All-or-nothing: removes exactly one
        /// <c>berry</c> from the inventory; returns false (and changes NOTHING) if none are held. IF a
        /// <see cref="HungerNeed"/> is present it routes through the atomic seam
        /// <see cref="HungerNeed.TryEatBerry"/> (consume + restore are inseparable); otherwise it just
        /// consumes (AC5b graceful no-HungerNeed — no null-ref on a missing AddFood). What eating RESTORES
        /// is OWNED by 86caamkp8 — this method does NOT decide the hunger amount.
        /// </summary>
        /// <param name="hunger">Optional hunger need; null -> consume only (graceful, AC5b).</param>
        /// <returns>True iff exactly one berry was consumed.</returns>
        public bool EatBerry(HungerNeed hunger)
        {
            if (inventory == null) return false;

            // The all-or-nothing consume delegate: remove exactly one berry across stacks. RemoveItem is
            // itself all-or-nothing (debits nothing + returns false if none held), so a no-berry eat is a
            // clean no-op (no negative inventory — AC5a no-berry case).
            Func<bool> consumeOneBerry = () => inventory.Model.RemoveItem(ItemCatalog.BerryId, 1);

            bool eaten;
            if (hunger != null)
            {
                // Atomic: HungerNeed.TryEatBerry runs the consume delegate and ONLY restores if it
                // succeeded — consume + restore inseparable (the seam 86caamkp8 owns).
                eaten = hunger.TryEatBerry(consumeOneBerry);
            }
            else
            {
                // AC5b graceful no-HungerNeed: consume only, no restore, no null-ref.
                eaten = consumeOneBerry();
            }

            if (eaten)
                Debug.Log("[bush-trace] EatBerry -1 berry (restore=" + (hunger != null) +
                          ") -> berries=" + inventory.Model.CountItem(ItemCatalog.BerryId));
            return eaten;
        }

        /// <summary>Convenience overload: eat one berry, finding a HungerNeed in the scene if present
        /// (AC5b graceful when absent). Number-key / UI eat-action call-site.</summary>
        public bool EatBerry() => EatBerry(FindObjectOfType<HungerNeed>());

        // Schedule the regrow at a RANDOM time within [min,max] (AC4). Min clamped non-negative; max
        // clamped to >= min so a mis-authored max never schedules a regrow in the past.
        private void ScheduleRegrow()
        {
            float min = Mathf.Max(0f, regrowMinSeconds);
            float max = Mathf.Max(min, regrowMaxSeconds);
            float delay = min + (float)_rng.NextDouble() * (max - min);
            _regrowAt = Time.time + delay;
        }

        // Berries return: ripe again, visible again. The BUSH persisted the whole time (AC4).
        private void Regrow()
        {
            _ripe = true;
            ApplyRipeVisual();
            Debug.Log("[bush-trace] Regrow -> berries RIPE again (bush persisted)");
        }

        // Show/hide the berries visual to match ripe state (the bush body stays; only berries toggle).
        private void ApplyRipeVisual()
        {
            if (berriesVisual != null)
                berriesVisual.gameObject.SetActive(hasBerries && _ripe);
        }
    }
}
