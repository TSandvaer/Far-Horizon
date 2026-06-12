using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The craft interaction for the M-U2 thin loop (ticket 86ca8bdaq, U2-2) — the ENTRY to the loop.
    ///
    /// A world location the castaway click-moves to (U3 click-to-move baseline); when the player is
    /// within <see cref="craftRadius"/> of it, the single recipe fires and produces the axe via
    /// <see cref="Inventory.CraftAxe"/>. Deliberately THIN per ticket: one recipe, no crafting UI tree,
    /// no menu — the interaction IS "walk here, get the axe". (Crafting menus/recipe systems are future
    /// texture; explicitly out of scope.)
    ///
    /// === Why proximity, not ClickToMove.onArrived ===
    /// ClickToMove.onArrived is a single non-multicast System.Action that fires once per arrival at the
    /// LAST clicked point — coupling here would be fragile (clobbers any other subscriber) and would miss
    /// the craft if the player clicks slightly short of the spot or walks past it. Polling planar distance
    /// to the player root each Update is the robust seam: the castaway "reaches the gather/craft spot" and
    /// the axe is produced, regardless of exactly where the click landed. Cheap (one Vector2.Distance/frame),
    /// no per-frame allocation. The agent + ClickToMove live on the Player ROOT (the avatar is a child), so
    /// the player root's transform is the thing that moves — that's what we measure against.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The craft-spot GameObject (a low-poly marker mesh + this component) and its Inventory + player
    /// references are authored editor-time into Boot.unity (MovementCameraScene), NOT at Awake — an
    /// Awake-built interaction could ship mangled/absent. CraftSceneTests guards the scene presence
    /// and that the references are serialized, sibling of WarmthNeedSceneTests.
    /// </summary>
    public class CraftSpot : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger this spot writes the crafted axe into. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity triggers the craft. Wired at bootstrap; " +
                 "falls back to the ClickToMove root (the moving agent), then a 'Player'-tagged search.")]
        public Transform player;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the spot and the axe is crafted. " +
                 "Generous enough that arriving near the spot counts — the craft is the reward for getting here, " +
                 "not a pixel-precise landing.")]
        public float craftRadius = 2.0f;

        // One-shot guard: the recipe fires exactly once even though Update polls every frame.
        private bool _crafted;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
        }

        void Update()
        {
            if (_crafted || inventory == null || player == null) return;

            // Planar distance only — ignore any Y offset between the spot's mesh origin and the
            // player root's ground point (they sit on the same flat ground, but be height-robust).
            Vector2 spot = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            if (Vector2.Distance(spot, here) > craftRadius) return;

            // Reached the spot — craft the axe (idempotent at the Inventory layer too).
            if (inventory.CraftAxe())
            {
                Debug.Log("[CraftSpot] reached craft spot — crafted axe. HasAxe=" + inventory.HasAxe);
            }
            // Latch regardless of CraftAxe's return so we never re-poll-craft once we've been in range
            // (covers the already-had-axe case, e.g. a future reload path that pre-seeds the axe).
            _crafted = true;
        }

        /// <summary>True once the player has reached the spot and the recipe has fired (or was already
        /// satisfied). Exposed for the verification capture + PlayMode tests to assert end-to-end.</summary>
        public bool HasCrafted => _crafted;
    }
}
