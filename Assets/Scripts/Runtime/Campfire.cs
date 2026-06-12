using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The campfire — the loop's CLOSE (ticket 86ca8bdep, U2-4). The thing the cold castaway builds to
    /// answer the warmth need: a lit fire restores/holds warmth while the castaway stands by it. This is
    /// the milestone's heart — warmth decays (U2-1) -> craft axe (U2-2) -> chop tree for wood (U2-3) ->
    /// BUILD + LIGHT this campfire -> warm again.
    ///
    /// === Two-stage state: built, then lit ===
    /// The campfire ships UNBUILT/UNLIT (CampfirePlacement, gated on wood, builds + lights it; success
    /// test "no wood -> no campfire"). This component owns the LIT state + the warmth restore:
    ///   bool IsLit  : true once lit. While lit AND the player is within warmRadius, warmth is restored.
    ///   Light(WarmthNeed) : lights the fire and binds the need it warms. Idempotent.
    /// The flame visual + the warm point Light are toggled with the lit state (a campfire only glows when
    /// lit). Both are already-serialized children (authored editor-time, MovementCameraScene.BuildCampfire)
    /// — runtime only flips their .enabled/.SetActive, never builds hierarchy at Awake (the editor-vs-runtime
    /// serialization trap, unity-conventions.md: an Awake-built flame/light could ship mangled/absent).
    ///
    /// === Warmth restore — proximity, same idiom as CraftSpot/ChopTree ===
    /// We REUSE the proven planar-XZ-proximity seam (poll distance to the player root each Update), NOT a
    /// new interaction model: when lit and the player is within warmRadius, we add warmth over real elapsed
    /// time via WarmthNeed.AddWarmth (restoreRate * dt, integrated over Time.time — headless-safe, the same
    /// reason WarmthNeed decays over a Time.time window and not per-frame deltaTime). Step out of the radius
    /// and the restore stops (and decay resumes) — the warmth is felt as "by the fire" vs "out in the cold".
    /// restoreRate is tuned well above WarmthNeed.decayPerSecond so the bar visibly CLIMBS at the fire.
    ///
    /// === Thin per ticket ===
    /// No fuel/burn-down, no fire spread, no cooking (all out of scope — M-U3/U4). The fire, once lit,
    /// stays lit; the only dynamic is "are you near it (warming) or not (cooling)".
    /// </summary>
    public class Campfire : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The need this campfire restores when lit + the player is near. Wired at bootstrap; " +
                 "Light() can also bind it. Falls back to a scene search.")]
        public WarmthNeed warmth;

        [Tooltip("The player transform whose proximity (while lit) restores warmth. Wired at bootstrap; " +
                 "falls back to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Tooltip("The flame visual root, shown only when the fire is lit. Wired at bootstrap.")]
        public GameObject flameVisual;

        [Tooltip("The warm point Light, enabled only when the fire is lit (the glow into the Zone-D dusk). " +
                 "Wired at bootstrap.")]
        public Light fireLight;

        [Header("Warmth restore")]
        [Tooltip("Planar (XZ) distance within which a LIT campfire warms the castaway. Generous so " +
                 "'standing by the fire' counts (mirrors CraftSpot/ChopTree radii).")]
        public float warmRadius = 3.0f;

        [Tooltip("Warmth restored per second while lit + in range. Tuned WELL above WarmthNeed.decayPerSecond " +
                 "(~0.55) so the bar visibly CLIMBS at the fire (the felt payoff of closing the loop).")]
        public float restoreRate = 14f;

        // Runtime state.
        private bool _lit;
        private float _lastTickTime;
        private bool _ticking;

        /// <summary>True once the fire has been lit. While lit + the player is near, warmth restores.</summary>
        public bool IsLit => _lit;

        void Awake()
        {
            if (warmth == null) warmth = FindAnyObjectByType<WarmthNeed>();
            if (player == null)
            {
                var ctm = FindAnyObjectByType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            // Ship dark: the fire does not glow until lit. Defensive — the authored scene already
            // serializes them off, but never assume (a re-enabled-in-editor flame must still ship dark).
            ApplyLitVisuals();
        }

        /// <summary>
        /// Light the fire (CampfirePlacement calls this once the wood is paid + the fire is placed).
        /// Binds the need it warms if one is passed, switches on the flame + glow, and arms the
        /// warmth restore. Idempotent — lighting an already-lit fire just (re)binds the need.
        /// </summary>
        public void Light(WarmthNeed need = null)
        {
            if (need != null) warmth = need;
            _lit = true;
            _lastTickTime = Time.time; // seed the restore window so the first tick isn't a huge dt
            ApplyLitVisuals();
            Debug.Log("[Campfire] LIT — warmth restore armed (rate=" + restoreRate +
                      "/s within " + warmRadius + "u; need bound: " + (warmth != null) + ")");
        }

        void Update()
        {
            if (!_lit || warmth == null || player == null) return;

            // Planar XZ distance to the player root — height-robust, same as CraftSpot/ChopTree.
            Vector2 fire = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            bool inRange = Vector2.Distance(fire, here) <= warmRadius;

            if (!inRange)
            {
                _ticking = false; // left the fire — stop restoring (decay resumes via WarmthNeed.Update)
                return;
            }

            // Just arrived at the fire: seed the window so the first restore tick uses a small dt, not
            // the whole gap since the fire was lit / since we last stood here.
            if (!_ticking)
            {
                _ticking = true;
                _lastTickTime = Time.time;
                return;
            }

            float now = Time.time;
            float dt = now - _lastTickTime;
            _lastTickTime = now;
            // Integrate restore over REAL elapsed time (not per-frame deltaTime — headless Time.deltaTime~=0,
            // unity-conventions.md §headless time). WarmthNeed.AddWarmth clamps at max.
            if (dt > 0f) warmth.AddWarmth(restoreRate * dt);
        }

        // Show the flame + enable the glow only when lit; both are pre-serialized children (no Awake-built
        // hierarchy). Safe to call repeatedly.
        private void ApplyLitVisuals()
        {
            if (flameVisual != null) flameVisual.SetActive(_lit);
            if (fireLight != null) fireLight.enabled = _lit;
        }
    }
}
