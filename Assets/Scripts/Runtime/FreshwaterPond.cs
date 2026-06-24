using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A FRESHWATER POND (ticket 86caamkv7) — the world thirst source for the merged survival loop. The
    /// castaway walks up to the pond and DRINKS FROM HAND (proximity + interact, NO tool, NOT an inventory
    /// item — distinct from the berry-bush HARVEST-to-inventory): each scoop restores a SMALL amount of thirst
    /// (<see cref="ThirstNeed.AddWater"/>), repeatable while in range ("drinks with his hand, satisfying small
    /// amount of thirst with EACH scoop" — vision-far-horizon-game-concept.md). No tool required for v1
    /// (a cup/container craft is explicitly OUT of scope — "later").
    ///
    /// === The proximity gate (AC3 — load-bearing) ===
    /// This component tracks whether the player is within <see cref="drinkRadius"/> (planar XZ, height-robust,
    /// the same idiom as BerryBush/ChopTree/CraftSpot) and exposes it as <see cref="PlayerInRange"/>. The drink
    /// seam (<see cref="DrinkScoop"/> / <see cref="ThirstNeed.TryDrinkScoop"/>) restores thirst ONLY when the
    /// player is in range — a scoop far from the pond does NOTHING (a drink that works anywhere green-passes
    /// "thirst rose" but breaks the fiction; the FAR-from-pond negative is the silent-killer guard, AC6).
    ///
    /// === NOT an inventory item (AC3) ===
    /// Drinking touches NO inventory at all — there is no item created or consumed (water is not berries). The
    /// drink seam restores thirst DIRECTLY through <see cref="ThirstNeed.AddWater"/>. A copy-paste from the
    /// berry eat-action that routed water through inventory would be a design break (Tess silent-killer #4).
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The pond GameObject + this component + its water-surface visual + ThirstNeed/player refs are authored
    /// editor-time into Boot.unity (MovementCameraScene.BuildFreshwaterPond), NOT at Awake — an Awake-built
    /// interaction/visual could ship MANGLED/absent (the legs-up class). FreshwaterPondSceneTests guards the
    /// scene presence + that the refs serialize. NO collider — the player walks up to drink; it never blocks
    /// the ground raycast or the NavMesh bake (built collider-free, BEFORE the bake).
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[pond-trace]` lines on the first in-range drink + the first out-of-range scoop attempt so the
    /// pond's runtime behaviour is readable from the build log (the diagnose-via-trace discipline; sibling of
    /// the [bush-trace]/[eat-trace] lines).
    /// </summary>
    public class FreshwaterPond : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The thirst need a scoop restores. Wired at bootstrap; scene-found fallback. May be null — " +
                 "drinking then does nothing gracefully, no null-ref (AC3b).")]
        public ThirstNeed thirst;

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

        /// <summary>True when the player is within drinking range of the pond (the proximity gate the drink
        /// seam reads). Recomputed each Update; exposed for the drink-action + PlayMode tests.</summary>
        public bool PlayerInRange { get; private set; }

        /// <summary>The effective planar drink range: the pond's surface radius + the reach margin (drink from
        /// the edge of a wide pond, not only the centre).</summary>
        public float EffectiveDrinkRadius => pondSurfaceRadius + drinkRadius;

        void Awake()
        {
            // Serialized refs are the source of truth (bootstrap wires them editor-time). These are a
            // build-safety net only — never the path the shipped build relies on.
            if (thirst == null) thirst = FindObjectOfType<ThirstNeed>();
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
