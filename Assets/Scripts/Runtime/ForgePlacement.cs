using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The FORGE BUILD interaction (ticket 86cakkmvc / I-3) — the direct sibling of <see cref="CampfirePlacement"/>,
    /// adapted for a wood+STONE material gate (a stone furnace costs more than an open fire). A world spot the
    /// castaway reaches; arriving WITH ENOUGH WOOD + STONE builds the forge (debiting both). The MATERIAL GATE is
    /// the load-bearing negative case (success test / ticket: "not enough mats → no furnace, no debit"): too little
    /// wood OR stone → reaching the spot does nothing, the forge stays unbuilt, no mats are spent.
    ///
    /// === Why a placement SPOT + proximity (same idiom as CampfirePlacement/CraftSpot/ChopTree) ===
    /// Free cursor placement is the full Don't-Starve build cursor (out of scope this milestone). We reuse the
    /// proven proximity idiom: poll planar XZ distance to the player root each Update; arriving (with the mats)
    /// raises the forge. The data seam (Inventory.SpendWood + SpendStone → Forge.Build) is what I-3 establishes.
    ///
    /// === All-or-nothing across BOTH mats (the negative case) ===
    /// The gate checks the pack holds BOTH woodCost wood AND stoneCost stone BEFORE debiting either — so a partial
    /// pay can never happen (no wood spent if stone is short, or vice-versa). We DON'T latch on a failed attempt:
    /// the castaway can walk away, gather more, and come back to build it. <see cref="CanAfford"/> is a PURE static
    /// so the affordability truth-table is unit-testable without a scene.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The forge-build spot + the (unbuilt) Forge GameObject + this component + its Inventory/Forge/player references
    /// are authored editor-time into Boot.unity (MovementCameraScene.BuildForge), NOT at Awake — an Awake-built
    /// forge could ship MANGLED/absent (the legs-up class). ForgeSceneTests guards the scene presence + that the
    /// refs serialize (sibling of CampfireSceneTests).
    /// </summary>
    public class ForgePlacement : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger the wood + stone cost is debited from. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The forge this spot builds when the player arrives with the mats. Wired at bootstrap; falls back " +
                 "to a scene search.")]
        public Forge forge;

        [Tooltip("The player transform whose proximity (with enough mats) builds the forge. Wired at bootstrap; " +
                 "falls back to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Header("Build cost")]
        [Tooltip("Wood units the forge costs. A stone furnace costs more than the open campfire (structure, not a " +
                 "fire pit). default 4 — Sponsor-soak tunes.")]
        public int woodCost = 4;

        [Tooltip("Stone units the forge costs — the STONE half of the gate (a stone furnace needs stone). Reachable " +
                 "from the small-stone gathers + the ore-node cluster. default 5 — Sponsor-soak tunes.")]
        public int stoneCost = 5;

        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the build spot and (with mats) builds the " +
                 "forge. Mirrors CampfirePlacement/CraftSpot/ChopTree radii.")]
        public float buildRadius = 2.2f;

        // One-shot guard: once built, the spot is spent.
        private bool _built;

        /// <summary>True once the forge has been built here. Exposed for tests + capture.</summary>
        public bool HasBuilt => _built;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (forge == null) forge = FindObjectOfType<Forge>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
        }

        void Update()
        {
            if (_built || inventory == null || forge == null || player == null) return;

            // Planar XZ distance to the player root — height-robust, same as CampfirePlacement.
            Vector2 spot = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            if (Vector2.Distance(spot, here) > buildRadius) return;

            TryBuild();
        }

        /// <summary>
        /// Attempt to build the forge from the pack: ALL-OR-NOTHING across BOTH mats. Checks the pack can afford
        /// wood + stone BEFORE debiting either (no partial pay), then debits both + raises the forge. Returns true
        /// IFF the forge was actually built. The input-independent seam (proximity in Update calls it; a test/capture
        /// can call it directly). A failed attempt spends nothing + leaves the forge unbuilt (walk away, gather, retry).
        /// </summary>
        public bool TryBuild()
        {
            if (_built || inventory == null || forge == null) return false;

            // THE MATERIAL GATE — reaching the spot does NOTHING unless BOTH mats can be paid in full.
            if (!CanAfford(inventory.WoodCount, inventory.StoneCount, woodCost, stoneCost))
            {
                Debug.Log("[ForgePlacement] at the build spot but not enough mats (have wood=" +
                          inventory.WoodCount + "/" + woodCost + ", stone=" + inventory.StoneCount + "/" +
                          stoneCost + ") — no forge");
                return false;
            }

            // Checked → both debits succeed (each is all-or-nothing; the check guarantees no partial pay).
            bool woodOk = inventory.SpendWood(woodCost);
            bool stoneOk = inventory.SpendStone(stoneCost);
            if (!woodOk || !stoneOk)
            {
                Debug.LogWarning("[ForgePlacement] mats debit failed after the affordability check (woodOk=" +
                                 woodOk + " stoneOk=" + stoneOk + ") — not building");
                return false;
            }

            forge.Build();
            _built = true;
            Debug.Log("[ForgePlacement] built the forge (paid " + woodCost + " wood + " + stoneCost +
                      " stone; wood now=" + inventory.WoodCount + ", stone now=" + inventory.StoneCount + ")");
            return true;
        }

        /// <summary>
        /// PURE affordability truth-table (unit-testable without a scene): the forge is affordable IFF the pack
        /// holds AT LEAST woodCost wood AND AT LEAST stoneCost stone. The all-or-nothing gate — both must clear.
        /// </summary>
        public static bool CanAfford(int wood, int stone, int woodCost, int stoneCost)
            => wood >= woodCost && stone >= stoneCost;
    }
}
