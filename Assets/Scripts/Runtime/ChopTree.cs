using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The chop interaction for the M-U2 thin survival loop (ticket 86ca8bdd8, U2-3) — the "do work
    /// in the world" beat: a tool (the axe, U2-2) wired to a world-interaction (a tree) that yields a
    /// resource (wood, into U2-2's Inventory seed).
    ///
    /// A choppable tree the castaway click-moves to (U3 click-to-move baseline); when the player is
    /// within <see cref="chopRadius"/> AND holds the axe (<see cref="Inventory.HasAxe"/>), the tree is
    /// chopped — wood is added to the ledger over a few discrete chops, then the tree is FELLED. The
    /// AXE GATE is the load-bearing rule (success test: "chopping without the axe does nothing"): no
    /// axe -> proximity does nothing, no wood, the tree stays standing. Deliberately THIN per ticket:
    /// one tree, no respawn ecology, no tool durability, no second resource type (all out of scope).
    ///
    /// === Why proximity (same idiom as CraftSpot), not ClickToMove.onArrived ===
    /// onArrived is a single non-multicast Action that fires once at the LAST clicked point — fragile
    /// here (clobbers other subscribers, misses if the click lands slightly short). Polling planar XZ
    /// distance to the player root each Update is the robust seam the castaway "reaches the tree", just
    /// like CraftSpot. Cheap (one Vector2.Distance/frame), no per-frame allocation. The agent +
    /// ClickToMove live on the Player ROOT (the avatar is a child), so the player root transform is the
    /// thing that moves — that's what we measure against. We deliberately REUSE CraftSpot's proven
    /// proximity pattern rather than invent a fresh interaction idiom.
    ///
    /// === Chop feedback — thin but FELT (ticket: this loop must be fun) ===
    /// Each chop fires on a short interval while the axe-holding player stands at the tree, adding
    /// <see cref="woodPerChop"/> wood, until <see cref="chopsToFell"/> chops fell it. On the felling
    /// chop the tree visibly SINKS + tips (a runtime transform tween on the already-serialized visual,
    /// no Awake-built hierarchy) so the world reacts to the player's work. Uma's HUD/feel pass (U2-5/6)
    /// owns richer feedback (animation/sfx); the per-chop hook here is the stub the ticket permits.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The tree GameObject (a Zone-D low-poly trunk+canopy) + this component + its Inventory/player
    /// references are authored editor-time into Boot.unity (MovementCameraScene.BuildChopTree), NOT at
    /// Awake — an Awake-built interaction/visual could ship MANGLED/absent (the legs-up class).
    /// ChopSceneTests guards the scene presence + that the refs serialize, sibling of CraftSceneTests.
    /// </summary>
    public class ChopTree : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger chopped wood is added to. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity (with an axe) triggers a chop. Wired at " +
                 "bootstrap; falls back to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Tooltip("The tree's visual root, tweened on felling (sink + tip). Wired at bootstrap; " +
                 "falls back to this transform so the chop is still felt even if unwired.")]
        public Transform visual;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the tree and (with an axe) " +
                 "chops. Generous enough that arriving near the tree counts — reward for getting here, " +
                 "not a pixel-precise landing. Mirrors CraftSpot.craftRadius.")]
        public float chopRadius = 2.2f;

        [Tooltip("Wood units yielded per chop. Thin: 1 by default so the readout ticks up visibly.")]
        public int woodPerChop = 1;

        [Tooltip("Chops needed to fell the tree. After this many, the tree falls and is spent " +
                 "(no respawn — out of scope). Small so the loop reads quickly.")]
        public int chopsToFell = 3;

        [Tooltip("Seconds between chops while the axe-holding player stands at the tree — paces the " +
                 "chops so wood ticks up one at a time, not all in a single frame (the 'felt' beat).")]
        public float chopInterval = 0.6f;

        // Runtime state.
        private int _chops;          // chops landed so far
        private bool _felled;        // true once chopsToFell reached — the tree is spent
        private float _nextChopAt;   // wall-clock time the next chop may land
        private bool _atTree;        // was the (axe-holding) player in range last frame (edge detect)

        // Felling tween state (runtime-only transform animation on the serialized visual).
        private bool _felling;
        private float _fellT;
        private Vector3 _standPos;
        private Quaternion _standRot;
        private const float FellDuration = 0.5f;

        /// <summary>Chops landed so far (0..chopsToFell). Exposed for PlayMode tests + capture.</summary>
        public int Chops => _chops;

        /// <summary>True once the tree has been fully chopped down. The tree is spent (no respawn).</summary>
        public bool IsFelled => _felled;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            if (visual == null) visual = transform;
        }

        void Update()
        {
            // Drive the one-shot felling tween independently of input, then bail (a felled tree is spent).
            if (_felling) { StepFelling(); return; }
            if (_felled || inventory == null || player == null) return;

            // Planar distance only — ignore any Y offset between the tree origin and the player root
            // ground point (height-robust, same as CraftSpot).
            Vector2 tree = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            bool inRange = Vector2.Distance(tree, here) <= chopRadius;

            // === THE AXE GATE ===  proximity alone does nothing; the axe is required to chop.
            // Without it, an out-of-axe player standing at the tree yields no wood and never fells it
            // (success test: "chopping without the axe does nothing"). This is the load-bearing rule.
            if (!inRange || !inventory.HasAxe)
            {
                _atTree = false;
                return;
            }

            // Just arrived (with an axe): land the first chop immediately so the loop feels responsive,
            // then pace subsequent chops by chopInterval.
            if (!_atTree)
            {
                _atTree = true;
                _nextChopAt = 0f; // chop now
            }

            if (Time.time >= _nextChopAt)
            {
                Chop();
                _nextChopAt = Time.time + chopInterval;
            }
        }

        // Land one chop: yield wood, advance the count, and fell the tree on the final chop.
        private void Chop()
        {
            inventory.AddWood(Mathf.Max(1, woodPerChop));
            _chops++;
            Debug.Log("[ChopTree] chop " + _chops + "/" + chopsToFell +
                      " -> wood=" + inventory.WoodCount);

            if (_chops >= chopsToFell)
            {
                _felled = true;
                BeginFelling();
                Debug.Log("[ChopTree] tree FELLED after " + _chops + " chops (total wood=" +
                          inventory.WoodCount + ")");
            }
        }

        // Start the thin-but-felt felling tween: capture the standing pose so StepFelling can sink+tip.
        private void BeginFelling()
        {
            if (visual == null) visual = transform;
            _standPos = visual.position;
            _standRot = visual.rotation;
            _felling = true;
            _fellT = 0f;
        }

        // One frame of the felling tween — the tree sinks into the ground and tips over, then stops.
        // Pure transform animation on the already-serialized visual (no Awake-built hierarchy to ship
        // mangled). Headless deltas are ~0, so this is wall-clock paced via Time.time-independent
        // unscaledDeltaTime accumulation; it is purely cosmetic, so a headless run simply lands at the
        // end state quickly without affecting the wood/felled assertions.
        private void StepFelling()
        {
            if (visual == null) { _felling = false; return; }
            _fellT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_fellT / FellDuration);
            float ease = k * k * (3f - 2f * k); // smoothstep
            visual.position = _standPos + Vector3.down * (0.6f * ease);
            visual.rotation = _standRot * Quaternion.Euler(70f * ease, 0f, 0f);
            if (k >= 1f) _felling = false;
        }
    }
}
