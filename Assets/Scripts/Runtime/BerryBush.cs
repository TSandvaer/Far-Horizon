using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A harvestable BERRY BUSH (ticket 86caa5zz3; E-LOOT migration 86caf7a6q) — the world food source for
    /// the merged hunger loop. The castaway walks up to a RIPE berry bush and presses E to LOOT its berries
    /// (the universal pick-up/loot verb — DECISIONS 2026-06-27): the berries go to the inventory as a
    /// stackable <c>berry</c> resource (<see cref="InventoryModel.AddItem"/> against the catalog's berry
    /// <see cref="ItemDef"/> — it never mints a parallel item type, per item-model contract §9). The BUSH
    /// PERSISTS; only the BERRIES deplete on harvest and REGROW after a tweakable delay (AC4).
    ///
    /// === The interaction (E-LOOT — 86caf7a6q AC1/AC2/AC4) — IMPLEMENTS <see cref="IPickable"/> ===
    /// The bush is an <see cref="IPickable"/> on the shared E-loot surface: the player-side
    /// <see cref="PickableLooter"/> resolves the nearest in-range pickable and calls <see cref="TryLoot"/>
    /// when E is pressed. <see cref="TryLoot"/> wraps the existing <see cref="Harvest"/> seam (add berries +
    /// go bare + schedule regrow) into the one-loot loot-contract. This REPLACES the old proximity-auto
    /// harvest: walking into range no longer harvests — the player must press E (AC4 "no proximity-auto").
    /// Berries are looted by hand (no tool gate — unlike the axe-gated chop). The bush's own
    /// <see cref="harvestRadius"/> (scaled by the bush size) is its <see cref="IPickable.LootRange"/>.
    ///
    /// === Regrowth (AC4) — bush persists, berries deplete + regrow, TWEAKABLE within [min,max] ===
    /// On harvest the bush goes BARE (berries hidden) and schedules a regrow at a RANDOM time within
    /// [<see cref="regrowMinSeconds"/>, <see cref="regrowMaxSeconds"/>] (like tree-regrowth / stone-respawn).
    /// When the timer elapses the berries return (RIPE again) and can be harvested anew. The min/max are
    /// serialized tweakable fields (the data side); registering a `berry regrowth time` SETTING in the dev
    /// settings panel is a FOLLOW-UP — no settings/dev-tweak panel exists on main yet (gated on the panel
    /// foundation; memory `sponsor-wants-unified-dev-tweak-console`). NOT a mid-PR scope expansion.
    ///
    /// === Eat (AC5 / AC5a / AC5b — ticket 86caa5zz3) — CONSUME side only ===
    /// NOTE: eating is the CONSUME side (left-click use the selected belt item — 86caf7a30), DISTINCT from
    /// the E-LOOT side this migration owns. <see cref="EatBerry"/> stays as the tested consume seam; it is
    /// NOT bound to E any more (E is now loot). The in-game eat INPUT binding moves to 86caf7a30.
    ///
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
    public class BerryBush : MonoBehaviour, IPickable
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory harvested berries are added to. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform (E-loot is resolved by the PickableLooter, not the bush — this stays " +
                 "wired for the legacy/test harness). Wired at bootstrap; falls back to the ClickToMove root.")]
        public Transform player;

        [Tooltip("The berries visual root — shown when RIPE, hidden when BARE (regrowing). Wired at " +
                 "bootstrap. If unwired the harvest still works (berries just don't visibly toggle).")]
        public Transform berriesVisual;

        [Tooltip("The hunger need the no-arg EatBerry() restores. Wired at bootstrap; cached from a scene " +
                 "search in Awake as a build-safety net. May be null — eating then consumes the berry " +
                 "gracefully with no restore (AC5b, no null-ref).")]
        public HungerNeed hunger;

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
        private float _regrowAt;            // wall-clock time the berries regrow (when bare)
        private System.Random _rng;
        private bool _tracedFirstHarvest;   // one-shot trace guards (don't spam the log per frame)

        /// <summary>True when berries are present + harvestable. False while regrowing (bare). Exposed for
        /// PlayMode tests + the visual toggle.</summary>
        public bool IsRipe => _ripe;

        /// <summary>Wall-clock time the berries are scheduled to regrow (only meaningful while bare).</summary>
        public float RegrowAt => _regrowAt;

        // ============================================================================================
        // IPickable — the WORLD-ITEM side of the shared E-loot surface (86caf7a6q AC1/AC2). The
        // PickableLooter resolves the nearest in-range CanLoot pickable and calls TryLoot on E.
        // ============================================================================================

        /// <summary>IPickable: the bush is loot-able when it is a berry bush AND currently RIPE — a bare/
        /// regrowing or decorative bush is skipped by the looter's nearest-in-range resolve (so E never
        /// "loots nothing" off a spent bush).</summary>
        public bool CanLoot => hasBerries && _ripe && inventory != null;

        /// <summary>IPickable: the bush's world position (the looter measures planar XZ distance to this).</summary>
        public Vector3 LootPosition => transform.position;

        /// <summary>IPickable: the bush's loot reach — its own <see cref="harvestRadius"/> SCALED by the bush
        /// visual size (localScale.x: 0.55–1.5× across the scatter) so a small bush isn't loot-able from
        /// metres away and a large one only from a step closer — the reach matches what the player sees
        /// (preserves the size-scaled radius the old proximity harvest used).</summary>
        public float LootRange => harvestRadius * transform.localScale.x;

        /// <summary>
        /// IPickable.TryLoot (86caf7a6q AC1) — loot ONE harvest of berries into <paramref name="inv"/>: the
        /// whole transaction is the existing <see cref="Harvest"/> seam (add berries via the canonical
        /// <see cref="ItemCatalog.BerryId"/> + go BARE + schedule regrow). Returns true iff at least one
        /// berry actually landed (a full pack lands 0 → returns false, a clean no-op for the looter). Harvest
        /// preserves its existing tested behaviour: the bush still goes bare on a wasted (full-pack) harvest
        /// — TryLoot just reports it back via the false return. Uses the wired <see cref="inventory"/> (the
        /// bush owns its inventory ref); <paramref name="inv"/> is accepted for the interface contract and
        /// used when the bush's own ref is unset (test/edge safety).
        /// </summary>
        public bool TryLoot(Inventory inv)
        {
            if (inventory == null) inventory = inv;
            if (inventory == null || !CanLoot) return false;
            return Harvest() > 0;
        }

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            // Cache the HungerNeed once (build-safety net for the no-arg EatBerry overload) — never a
            // per-eat FindObjectOfType (unity6-mastery §6 "no per-frame/per-use Find"). The serialized
            // ref (wired at bootstrap) is the source of truth; this fills it only if unwired.
            if (hunger == null) hunger = FindObjectOfType<HungerNeed>();
            _rng = new System.Random(regrowSeed != 0 ? regrowSeed : Environment.TickCount);
            ApplyRipeVisual();
        }

        void Update()
        {
            // Update ONLY regrows the berries (bush persists; only berries toggle — AC4). The HARVEST is no
            // longer proximity-auto (86caf7a6q AC4 "no proximity-auto"): the player presses E and the
            // PickableLooter calls TryLoot. Walking into range does NOTHING here — there is no proximity
            // distance read at all, which is the structural proof the bush can't auto-harvest.
            if (hasBerries && !_ripe && Time.time >= _regrowAt)
                Regrow();
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
                BushTrace("Harvest +" + added + " berry (yield=" + berriesPerHarvest +
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
                BushTrace("EatBerry -1 berry (restore=" + (hunger != null) +
                          ") -> berries=" + inventory.Model.CountItem(ItemCatalog.BerryId));
            return eaten;
        }

        /// <summary>Convenience overload: eat one berry, restoring through the CACHED <see cref="hunger"/>
        /// ref (wired at bootstrap; Awake-cached fallback) — never a per-use FindObjectOfType. Graceful
        /// when no HungerNeed exists (AC5b). UI / eat-action call-site.</summary>
        public bool EatBerry() => EatBerry(hunger);

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
            BushTrace("Regrow -> berries RIPE again (bush persisted)");
        }

        // [bush-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call
        // (AND its argument evaluation, incl. the string concatenation) from the shipped IL2CPP release exe,
        // so the trace never costs the player a string alloc + log write (unity6-mastery §5 "no Debug.Log in
        // hot paths" / §10 "strip all logging from shipping builds"). The first-time guards keep it one-shot.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void BushTrace(string msg) => Debug.Log("[bush-trace] " + msg);

        // Show/hide the berries visual to match ripe state (the bush body stays; only berries toggle).
        private void ApplyRipeVisual()
        {
            if (berriesVisual != null)
                berriesVisual.gameObject.SetActive(hasBerries && _ripe);
        }
    }
}
