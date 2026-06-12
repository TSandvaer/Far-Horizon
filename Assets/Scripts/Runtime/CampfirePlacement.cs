using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The campfire BUILD interaction (ticket 86ca8bdep, U2-4) — the Don't-Starve "place a structure from
    /// your mats" seed, kept THIN. A world spot the castaway click-moves to; reaching it WITH ENOUGH WOOD
    /// builds + lights the campfire (debiting the wood). The WOOD GATE is the load-bearing negative case
    /// (success test / ticket: "no wood -> no campfire"): too little wood -> reaching the spot does
    /// nothing, the fire stays unbuilt, no wood is spent.
    ///
    /// === Why a placement SPOT, not free cursor placement ===
    /// Free "place anywhere the cursor is" is the full Don't-Starve build cursor — out of scope this thin
    /// milestone (no build-mode UI, no ghost-preview). We seed the IDEA (mats -> a placed, lit structure)
    /// with the project's proven proximity idiom: the castaway click-moves to a prepared fire-pit spot,
    /// and arriving (with wood) raises the fire. A future ticket can grow this into a real placement cursor;
    /// the data seam (Inventory.SpendWood -> Campfire.Light) is what U2-4 establishes and stays stable.
    ///
    /// === Why proximity (same idiom as CraftSpot/ChopTree), not ClickToMove.onArrived ===
    /// onArrived is a single non-multicast Action that fires once at the LAST clicked point — fragile here.
    /// Poll planar XZ distance to the player root each Update: the castaway "reaches the fire pit", just
    /// like the craft spot and the tree. Cheap, no per-frame allocation.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The fire-pit spot + the (unlit) Campfire GameObject + this component + its Inventory/Campfire/player
    /// references are authored editor-time into Boot.unity (MovementCameraScene.BuildCampfire), NOT at
    /// Awake — an Awake-built campfire/flame could ship MANGLED/absent (the legs-up class). The campfire
    /// ships UNLIT; this component lights it when the player arrives with wood. CampfireSceneTests guards
    /// the scene presence + that the refs serialize, sibling of ChopSceneTests.
    /// </summary>
    public class CampfirePlacement : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger the wood cost is debited from. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The campfire this spot builds + lights when the player arrives with wood. Wired at " +
                 "bootstrap; falls back to a scene search.")]
        public Campfire campfire;

        [Tooltip("The player transform whose proximity (with enough wood) builds the fire. Wired at " +
                 "bootstrap; falls back to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Tooltip("The need the lit campfire restores — passed into Campfire.Light so the fire is bound to " +
                 "it. Wired at bootstrap; falls back to a scene search.")]
        public WarmthNeed warmth;

        [Header("Build")]
        [Tooltip("Wood units the campfire costs. Must be <= what one tree yields (chopsToFell*woodPerChop) " +
                 "so a single felled tree affords a fire — the loop closes from one chop session.")]
        public int woodCost = 3;

        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the fire pit and (with wood) builds " +
                 "the fire. Mirrors CraftSpot/ChopTree radii.")]
        public float buildRadius = 2.2f;

        // One-shot guard: once built, the spot is spent (the fire is lit, no re-build).
        private bool _built;

        /// <summary>True once the campfire has been built + lit here. Exposed for tests + capture.</summary>
        public bool HasBuilt => _built;

        void Awake()
        {
            if (inventory == null) inventory = FindAnyObjectByType<Inventory>();
            if (campfire == null) campfire = FindAnyObjectByType<Campfire>();
            if (warmth == null) warmth = FindAnyObjectByType<WarmthNeed>();
            if (player == null)
            {
                var ctm = FindAnyObjectByType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
        }

        void Update()
        {
            if (_built || inventory == null || campfire == null || player == null) return;

            // Planar XZ distance to the player root — height-robust, same as CraftSpot/ChopTree.
            Vector2 spot = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            if (Vector2.Distance(spot, here) > buildRadius) return;

            // === THE WOOD GATE ===  reaching the pit does NOTHING unless the wood can be paid in full.
            // SpendWood is all-or-nothing: too little wood -> false, no debit, the fire stays unbuilt
            // (success test: "no wood -> no campfire"). We DON'T latch on a failed attempt — the castaway
            // can walk away, chop more wood, and come back to build it.
            if (!inventory.SpendWood(woodCost))
            {
                Debug.Log("[CampfirePlacement] at fire pit but not enough wood (have " +
                          inventory.WoodCount + ", need " + woodCost + ") — no campfire");
                return;
            }

            // Paid: build + light the fire, bind the need it warms. Latch so it's a one-shot.
            campfire.Light(warmth);
            _built = true;
            Debug.Log("[CampfirePlacement] built + lit the campfire (paid " + woodCost +
                      " wood; wood now=" + inventory.WoodCount + ")");
        }
    }
}
