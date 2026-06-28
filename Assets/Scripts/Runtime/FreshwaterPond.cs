using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A FRESHWATER POND (ticket 86caamkv7; E-LOOT water acquisition 86cafc6vx) — the world thirst SOURCE for
    /// the merged survival loop. The castaway walks up to the pond and presses E to COLLECT one unit of water
    /// into the belt (the SAME E-loot verb as berries/sticks/stones/wood — DECISIONS 2026-06-27 / Sponsor's
    /// Option A 2026-06-28); the carried water is then drunk ANYWHERE by left-click on the selected water belt
    /// item (LeftClickConsume.TryDrinkOneWater → ThirstNeed.AddWater, #156). The fiction: "the pond is a well
    /// you fill from" (Uma water-acquisition-spec.md) — gather at the pond, drink on the trail.
    ///
    /// === IMPLEMENTS <see cref="IPickable"/> — the GET side (86cafc6vx AC1/AC5) — the SAME idiom as BerryBush ===
    /// The pond is an <see cref="IPickable"/> on the shared E-loot surface: the player-side
    /// <see cref="PickableLooter"/> discovers every IPickable (its editor-time Awake scan), resolves the nearest
    /// in-range one, and calls <see cref="TryLoot"/> when E is pressed. <see cref="TryLoot"/> adds exactly ONE
    /// <see cref="ItemCatalog.WaterId"/> "water" per press (mirrors stick/stone's one-per-press, NOT LogPile's
    /// whole-pile grab). The pond is an INFINITE source — <see cref="CanLoot"/> stays true while an inventory is
    /// wired (the player is in range is the looter's job), so repeated E presses each yield one water (a standing
    /// well never runs dry — AC5). The pond is SERIALIZED into Boot.unity, so the looter AUTO-DISCOVERS it: NO
    /// <see cref="PickableLooter.RegisterPickable"/> call (that seam is RUNTIME-spawn-only, e.g. LogPile — AC1).
    /// The pond's prompt VERB is "collect" (<see cref="GatherVerb"/>) so the prompt reads "Press E to collect
    /// water" (you gather water, you don't pick it up — Uma §2a).
    ///
    /// === ONE water model: the proximity DRINK seam is COVERAGE-ONLY now (86cafc6vx AC4 — Option B) ===
    /// The OLD drink-at-pond model (drink FROM HAND at the pond) is RETIRED as a live path: the drink INPUT
    /// (<see cref="DrinkAction"/>) has been dormant since #156 (`inputEnabled=false` — Q no longer drinks) and
    /// drinking is now the belt-item left-click (LeftClickConsume). There is exactly ONE way water restores
    /// thirst: E-loot → belt → left-click. The proximity SEAM below
    /// (<see cref="DrinkScoop"/> / <see cref="PlayerInRange"/> / <see cref="ThirstNeed.TryDrinkScoop"/>) is KEPT
    /// — but as TEST-COVERAGE-ONLY scaffolding (FreshwaterPondPlayModeTests / DrinkActionPlayModeTests +
    /// the pond geometry/nav/verify-capture calibration tests depend on `pondSurfaceRadius`/`EffectiveDrinkRadius`).
    /// It is bound to NO input (the DrinkAction key stands down), so it is NOT a second live drink path (AC4: the
    /// constraint is "no LIVE second drink path," not a prescribed deletion — deleting the seam would break a
    /// large green geometry/nav suite for zero player-facing gain). The drink RESTORE (<see cref="ThirstNeed.AddWater"/>)
    /// is now driven by LeftClickConsume; the pond's own DrinkScoop is unreachable from any shipped input.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The pond GameObject + this component + its water-surface visual + ThirstNeed/player/inventory refs are
    /// authored editor-time into Boot.unity (MovementCameraScene.BuildFreshwaterPond), NOT at Awake — an
    /// Awake-built interaction/visual could ship MANGLED/absent (the legs-up class). FreshwaterPondSceneTests
    /// guards the scene presence + that the refs serialize. NO collider — the player walks up to drink; it never
    /// blocks the ground raycast or the NavMesh bake (built collider-free, BEFORE the bake).
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[pond-trace]` lines on the first loot (the GET side) + the first in-range drink + the first
    /// out-of-range scoop attempt so the pond's runtime behaviour is readable from the build log (the
    /// diagnose-via-trace discipline; sibling of the [bush-trace]/[loot-trace] lines).
    ///
    /// NO MUTABLE STATICS (instance state only) — needs no [RuntimeInitializeOnLoadMethod] reset (StaticStateResetTests).
    /// </summary>
    public class FreshwaterPond : MonoBehaviour, IPickable
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The thirst need a scoop restores (the COVERAGE-ONLY proximity drink seam). Wired at bootstrap; " +
                 "scene-found fallback. May be null — the drink seam then does nothing gracefully, no null-ref.")]
        public ThirstNeed thirst;

        [Tooltip("The inventory the E-looted water lands in (86cafc6vx AC1 — the GET side). Wired at bootstrap; " +
                 "scene-found fallback in Awake (build-safety net; the serialized ref is the source of truth). " +
                 "May be null — CanLoot is then false (the pond is not loot-able until an inventory is wired).")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity gates drinking. Wired at bootstrap; falls back to the " +
                 "ClickToMove root, then a scene search.")]
        public Transform player;

        [Header("Drink (AC3)")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the pond and can drink. Generous " +
                 "(reward for arriving), mirrors BerryBush.harvestRadius / CraftSpot.craftRadius. The EFFECTIVE " +
                 "radius adds the pond's own surface radius so a big pond is reachable from its edge.")]
        public float drinkRadius = 2.0f;

        [Tooltip("The pond water-surface radius (world units). Added to drinkRadius so the castaway can drink " +
                 "from the EDGE of a wide pond, not only from its centre. Set by the scene author to match the " +
                 "visual water disc; tests may set it directly.")]
        public float pondSurfaceRadius = 2.6f;

        private bool _tracedFirstDrink;     // one-shot trace guards (don't spam the log per scoop)
        private bool _tracedFirstFar;
        private bool _tracedFirstLoot;      // one-shot guard for the E-loot (GET side) trace

        /// <summary>True when the player is within drinking range of the pond (the proximity gate the drink
        /// seam reads). Recomputed each Update; exposed for the drink-action + PlayMode tests.</summary>
        public bool PlayerInRange { get; private set; }

        /// <summary>The effective planar drink range: the pond's surface radius + the reach margin (drink from
        /// the edge of a wide pond, not only the centre). ALSO the pond's E-loot reach (<see cref="LootRange"/>)
        /// — keyed to the VISIBLE waterline (pondSurfaceRadius is wired to LowPolyZoneGen.PondWaterlineRadius at
        /// bootstrap), so the "Press E to collect water" prompt + the loot fire where the player SEES water, not
        /// at the buried disc rim (#130 ROUND 8 "follow the visible waterline" lesson, AC5).</summary>
        public float EffectiveDrinkRadius => pondSurfaceRadius + drinkRadius;

        // ============================================================================================
        // IPickable — the WORLD-ITEM side of the shared E-loot surface (86cafc6vx AC1/AC5). The
        // PickableLooter auto-discovers the serialized pond, resolves the nearest in-range CanLoot pickable,
        // and calls TryLoot on E. This is the GET side that closes the thirst loop (the USE side — left-click
        // drink — is shipped in LeftClickConsume/#156). The pond is an INFINITE, repeatable source.
        // ============================================================================================

        /// <summary>IPickable: the pond is loot-able while an inventory is wired (86cafc6vx AC5 — INFINITE
        /// source: it never depletes, so CanLoot stays true and repeated E presses each yield one water; the
        /// pond is a standing well, not a one-shot pickup). The looter's nearest-in-range resolve handles the
        /// proximity (the pond's own <see cref="LootRange"/>); CanLoot only gates on having somewhere to loot
        /// INTO. A null inventory → false (not loot-able until wired) so E is a clean no-op, never a null-ref.</summary>
        public bool CanLoot => inventory != null;

        /// <summary>IPickable: the pond's world position (the looter measures planar XZ distance to this for the
        /// nearest-in-range resolve — height-robust, the same idiom as BerryBush / StickProp / ChopTree).</summary>
        public Vector3 LootPosition => transform.position;

        /// <summary>IPickable: the pond's E-loot reach — its <see cref="EffectiveDrinkRadius"/>, which is keyed to
        /// the VISIBLE waterline (pondSurfaceRadius == LowPolyZoneGen.PondWaterlineRadius, wired at bootstrap),
        /// NOT the buried nominal disc rim (#130 ROUND 8 lesson, AC5). So the "Press E to collect water" prompt
        /// appears + E loots exactly where the player SEES the water's edge (the source is the waterline, not the
        /// disc; the exact reach is Sponsor-soak-tunable via drinkRadius). The looter uses THIS per-item radius.</summary>
        public float LootRange => EffectiveDrinkRadius;

        /// <summary>IPickable: the generic prompt name (86cafc6ud) — the pond yields "water" (the canonical
        /// <see cref="ItemCatalog.WaterId"/> resource, the SAME id the left-click drink consumes — #156). The
        /// prompt shows "Press E to collect water" (the verb is <see cref="GatherVerb"/>; the name is this).
        /// Lower-case mass noun, matching the inventory resource word (per IPickable.DisplayName doc, AC7).</summary>
        public string DisplayName => "water";

        /// <summary>IPickable: the pond's gather VERB is "collect" (NOT the default "pick up") — you GATHER water
        /// from a well, you don't pick it up like an object (Uma water-acquisition-spec §2a; Sponsor's framing
        /// "collect water"). So the prompt reads "Press E to COLLECT water" while objects keep "pick up". A tiny
        /// generic override on the shared interface, NOT a pond-special-case prompt branch (AC7).</summary>
        public string GatherVerb => "collect";

        /// <summary>
        /// IPickable.TryLoot (86cafc6vx AC1) — COLLECT exactly ONE <see cref="ItemCatalog.WaterId"/> "water" into
        /// <paramref name="inv"/> (the GET side that closes the thirst loop): the whole transaction is add one
        /// water via the canonical catalog seam — the pond is INFINITE, so it is NEVER consumed/depleted (AC5),
        /// each E press is one more water. Returns true IFF exactly one water actually landed (a full pack lands
        /// 0 → returns false, a clean no-op the looter moves past — mirror StickProp/LogPile). Uses the wired
        /// <see cref="inventory"/> (the pond owns its inventory ref); <paramref name="inv"/> is accepted for the
        /// interface contract + used when the pond's own ref is unset (test/edge safety). NEVER a parallel water
        /// id, NEVER touches Inventory outside the catalog seam — the consume side (#156) reads this SAME id.
        /// </summary>
        public bool TryLoot(Inventory inv)
        {
            if (inventory == null) inventory = inv;
            if (inventory == null) return false;

            var catalog = inventory.Catalog;
            ItemDef water = catalog != null ? catalog.ById(ItemCatalog.WaterId) : null;
            if (water == null) return false; // no water def in catalog — clean no-op, not a null-ref

            // Add exactly ONE water (one-per-press — mirrors stick/stone, NOT LogPile's whole-pile grab; AC5).
            // AddItem returns the leftover that did NOT fit; a full pack lands 0 → return false (the pond is NOT
            // "consumed" — it is infinite; the player frees room + presses E again). Never lose/over-add water.
            int leftover = inventory.Model.AddItem(water, 1);
            int added = 1 - leftover;
            if (added <= 0) return false; // full pack — nothing landed, clean no-op (the looter moves past)

            if (!_tracedFirstLoot)
            {
                _tracedFirstLoot = true;
                PondTrace("E-loot +1 water (the GET side) -> water=" +
                          inventory.Model.CountItem(ItemCatalog.WaterId) + "; pond is INFINITE (always loot-able)");
            }
            return true;
        }

        void Awake()
        {
            // Serialized refs are the source of truth (bootstrap wires them editor-time). These are a
            // build-safety net only — never the path the shipped build relies on.
            if (thirst == null) thirst = FindObjectOfType<ThirstNeed>();
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
        }

        void Update()
        {
            // Recompute the proximity gate each frame. Planar XZ distance only (height-robust, same as
            // BerryBush/ChopTree). Scalar squared-distance — no Vector2 construction / sqrt per frame
            // (unity6-mastery §5 GC/hot-path rule). NO inventory, NO harvest, NO regrow — the pond just
            // gates drinking; the restore happens on a drink-action press (repeatable while in range).
            if (player == null) { PlayerInRange = false; return; }
            Vector3 pp = transform.position, qp = player.position;
            float dx = pp.x - qp.x, dz = pp.z - qp.z;
            float eff = EffectiveDrinkRadius;
            PlayerInRange = dx * dx + dz * dz <= eff * eff;
        }

        /// <summary>
        /// Drink ONE hand-scoop (AC3 — the drink seam, owned by this lane). Restores
        /// <see cref="ThirstNeed.waterScoopAmount"/> thirst through the atomic
        /// <see cref="ThirstNeed.TryDrinkScoop"/> seam, which gates on <see cref="PlayerInRange"/> — so a scoop
        /// far from the pond restores NOTHING. Repeatable: each call is one scoop, the player drinks several
        /// times. NO inventory touched (water is not berries). Returns true iff a scoop was actually drunk
        /// (in range AND restored). Graceful when no <see cref="ThirstNeed"/> is wired (AC3b — no null-ref).
        /// Public + scene-free so the PlayMode/EditMode tests drive it without a key device.
        /// </summary>
        public bool DrinkScoop()
        {
            // Proximity is load-bearing AND inseparable from the restore: TryDrinkScoop runs the in-range
            // predicate and ONLY restores when it returns true. With no ThirstNeed wired, drinking is a clean
            // no-op (AC3b graceful) — a scoop still requires being in range, but restores nothing.
            bool drank = thirst != null
                ? thirst.TryDrinkScoop(() => PlayerInRange)   // atomic: restore ONLY if in range
                : false;                                       // AC3b graceful no-ThirstNeed: nothing to restore

            if (drank)
            {
                if (!_tracedFirstDrink)
                {
                    _tracedFirstDrink = true;
                    PondTrace("drank 1 scoop (amount=" + thirst.waterScoopAmount.ToString("F0") +
                              ") -> thirst=" + thirst.Current01.ToString("F2"));
                }
            }
            else if (!PlayerInRange && !_tracedFirstFar)
            {
                _tracedFirstFar = true;
                PondTrace("drink attempted OUT of range -> no-op (proximity gate held)");
            }
            return drank;
        }

        // [pond-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call
        // (AND its argument evaluation, incl. the string concatenation) from the shipped IL2CPP release exe so
        // the trace never costs the player a string alloc + log write (unity6-mastery §5 "no Debug.Log in hot
        // paths" / §10 "strip all logging from shipping builds"). The first-time guards keep it one-shot.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void PondTrace(string msg) => Debug.Log("[pond-trace] " + msg);
    }
}
