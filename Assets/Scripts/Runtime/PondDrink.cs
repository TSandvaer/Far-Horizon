using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The DRINK-FROM-HAND interaction at the freshwater pond (ticket 86caamkv7, AC3 / AC3a) — the
    /// "satisfy thirst in the world" beat: the castaway click/WASD-moves to the pond, and when in
    /// reach an INTERACT scoops water by hand, restoring a SMALL amount of thirst per scoop. Repeatable.
    /// No tool, no inventory item (NOT like berries — there is no item to consume).
    ///
    /// === Why proximity polling (same idiom as ChopTree / CraftSpot), not a collider trigger ===
    /// Polling planar XZ distance to the player root each Update is the proven, allocation-free seam the
    /// castaway "reaches the pond" — robust (no fragile single-shot onArrived clobbering), cheap (one
    /// Vector2.Distance/frame). REUSES ChopTree's pattern rather than inventing a fresh interaction idiom.
    /// The pond TRANSFORM this component sits on is the proximity anchor: at runtime it is the real
    /// seed-42 pond Drew places (AC2/AC2a, sequenced separately); in tests it is a STAND-IN pond position
    /// (the drink LOGIC is what this ticket owns — the real seed-42 placement is Drew's lane).
    ///
    /// === The scoop gate is PROXIMITY, not an axe / inventory consume ===
    /// Unlike ChopTree (axe-gated) and berries (inventory-consume), the ONLY gate here is being in pond
    /// reach. A scoop FAR from the pond does NOTHING — <see cref="ThirstNeed.TryDrink"/> takes the
    /// in-reach predicate and only restores when true. The interact is edge-or-interval driven so holding
    /// at the pond scoops at a paced rate (one sip at a time), not every frame.
    ///
    /// === Graceful no-ThirstNeed (AC2b/AC3 degrade) ===
    /// If no ThirstNeed is wired/found, drinking is a safe no-op (no null-ref) — mirrors hunger's AC2b.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// At runtime this component + its thirst/player refs are authored editor-time onto the pond GameObject
    /// (Drew's bootstrap pond authoring), NOT at Awake — an Awake-built interaction could ship mangled.
    /// The Awake FindObjectOfType fallbacks are a build-safety net only. The drink-seam test (AC3a, owned
    /// here) drives this component directly with a stand-in pond + player + ThirstNeed.
    /// </summary>
    public class PondDrink : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The thirst need a scoop restores. Wired at bootstrap; scene-found fallback. Null -> drink no-ops.")]
        public ThirstNeed thirst;

        [Tooltip("The player transform whose proximity to the pond enables a scoop. Wired at bootstrap; " +
                 "falls back to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the pond and can drink. Generous " +
                 "enough that arriving near the water counts — reward for getting here, not a pixel-precise " +
                 "landing. Mirrors ChopTree.chopRadius / CraftSpot.craftRadius.")]
        public float drinkRadius = 2.5f;

        [Tooltip("Seconds between auto-scoops while the player stands at the pond — paces the sips one at " +
                 "a time (the 'small amount with each scoop' beat), not a guzzle every frame.")]
        public float scoopInterval = 0.6f;

        [Tooltip("Auto-scoop on a paced interval while the player stands in reach. On by default so the " +
                 "drink reads with no extra input binding this ticket; an explicit input can also call " +
                 "TryScoop() directly. Mirrors ChopTree's stand-at-the-tree auto-chop.")]
        public bool autoScoopWhileInReach = true;

        // Runtime state.
        private float _nextScoopAt;  // wall-clock time the next auto-scoop may land
        private bool _atPond;        // was the player in reach last frame (edge detect — first sip is immediate)
        private int _scoops;         // scoops landed (exposed for PlayMode tests + capture)

        /// <summary>Scoops landed so far. Exposed for PlayMode tests + capture evidence.</summary>
        public int Scoops => _scoops;

        /// <summary>True when the player root is within <see cref="drinkRadius"/> of the pond (planar XZ).
        /// This is the in-reach predicate <see cref="ThirstNeed.TryDrink"/> gates on.</summary>
        public bool PlayerInReach
        {
            get
            {
                if (player == null) return false;
                Vector2 pond = new Vector2(transform.position.x, transform.position.z);
                Vector2 here = new Vector2(player.position.x, player.position.z);
                return Vector2.Distance(pond, here) <= drinkRadius;
            }
        }

        void Awake()
        {
            if (thirst == null) thirst = FindObjectOfType<ThirstNeed>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
        }

        void Update()
        {
            if (!autoScoopWhileInReach || player == null) return;

            bool inReach = PlayerInReach;
            if (!inReach)
            {
                _atPond = false;
                return;
            }

            // Just arrived: scoop immediately so the drink feels responsive, then pace by scoopInterval.
            if (!_atPond)
            {
                _atPond = true;
                _nextScoopAt = 0f; // scoop now
            }

            if (Time.time >= _nextScoopAt)
            {
                TryScoop();
                _nextScoopAt = Time.time + scoopInterval;
            }
        }

        /// <summary>
        /// Attempt ONE hand-scoop: restore <see cref="ThirstNeed.waterScoopAmount"/> thirst IFF the player
        /// is in pond reach. Returns true iff a scoop happened. Far from the pond (or no ThirstNeed wired)
        /// -> a safe no-op (false), no thirst change. This is the seam the auto-scoop AND any explicit
        /// interact input both route through — the proximity gate lives in ONE place.
        /// </summary>
        public bool TryScoop()
        {
            if (thirst == null) return false;        // graceful no-ThirstNeed degrade (AC2b)
            if (!thirst.TryDrink(PlayerInReach)) return false; // proximity gate -> far = nothing
            _scoops++;
            return true;
        }
    }
}
