using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The campfire — the loop's CLOSE (ticket 86ca8bdep, U2-4). The thing the cold castaway builds to
    /// answer the warmth need: a lit fire restores/holds warmth while the castaway stands by it. This is
    /// the milestone's heart — warmth decays (U2-1) -> craft axe (U2-2) -> chop tree for wood (U2-3) ->
    /// BUILD + LIGHT this campfire -> warm again.
    ///
    /// === Invisible-until-placed → placed (revealed + lit) — the unified place-to-build flow (⑤ 86camz9w7) ===
    /// The campfire is now one of the THREE invisible-until-placed structures (Sponsor-locked, spec §0.1 —
    /// like the ① table + ③ forge). It ships INVISIBLE + UNLIT: there is NO pre-visible fire pit. The
    /// castaway gathers the wood+stone, PLACES the campfire via <see cref="CampfirePlacement"/>'s ghost +
    /// left-click confirm (the ① placement system VERBATIM), and the confirm REVEALS the structure AND
    /// LIGHTS it in one beat (<see cref="Build"/>) — the mats are paid for a lit fire. This component owns:
    ///   bool IsPlaced : true once the structure is revealed at the placed pose (the invisible-until-placed latch).
    ///   bool IsLit    : true once lit. While lit AND the player is within warmRadius, warmth is restored.
    ///   Build(pose, need) : place at the confirmed ghost pose — reveal the structure, self-register the
    ///                       no-build zone (#302), and Light it (binds the need). Idempotent.
    ///   Light(WarmthNeed) : lights the fire + binds the need it warms (the SHIPPED warmth seam, unchanged —
    ///                       spec §2 regression boundary). Idempotent.
    /// The structure visual (stone ring + logs) ships with its renderers DISABLED; Build reveals them. The
    /// flame visual + the warm point Light are toggled with the LIT state (a campfire only glows when lit) —
    /// the flame is excluded from the structure-reveal toggle (like the forge's smelt-glow), staying
    /// lit-state-driven. All are already-serialized children (authored editor-time,
    /// MovementCameraScene.BuildCampfire) — runtime only flips their .enabled/.SetActive, never builds
    /// hierarchy at Awake (the editor-vs-runtime serialization trap, unity-conventions.md: an Awake-built
    /// flame/light could ship mangled/absent — the legs-up class).
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

        [Tooltip("The structure visual root (the stone ring + logs). Its renderers ship DISABLED (invisible-" +
                 "until-placed, spec §0.1) — the place-to-build flow reveals them on Build. Excludes the flame " +
                 "child (lit-state-driven). Falls back to this transform.")]
        public Transform visual;

        [Tooltip("The flame visual root, shown only when the fire is lit. Wired at bootstrap.")]
        public GameObject flameVisual;

        [Tooltip("The warm point Light, enabled only when the fire is lit (the glow into the Zone-D dusk). " +
                 "Wired at bootstrap.")]
        public Light fireLight;

        [Tooltip("The no-build zone this campfire projects ONCE PLACED (the #302 PlacementObstacle seam) so a " +
                 "later table/forge/campfire placement ghost reads RED over it. Authored disabled; Build() " +
                 "enables it. Optional (null → the campfire simply doesn't self-register).")]
        public PlacementObstacle placementObstacle;

        [Header("Warmth restore")]
        [Tooltip("Planar (XZ) distance within which a LIT campfire warms the castaway. Generous so " +
                 "'standing by the fire' counts (mirrors CraftSpot/ChopTree radii).")]
        public float warmRadius = 3.0f;

        [Tooltip("Warmth restored per second while lit + in range. Tuned WELL above WarmthNeed.decayPerSecond " +
                 "(~0.55) so the bar visibly CLIMBS at the fire (the felt payoff of closing the loop).")]
        public float restoreRate = 14f;

        // Runtime state.
        private bool _placed;   // the structure has been revealed at the placed pose (invisible-until-placed latch)
        private bool _lit;
        private float _lastTickTime;
        private bool _ticking;

        /// <summary>True once the campfire structure has been placed + revealed in the world. Ships false
        /// (invisible until placed); scene-presence tests assert the structure renderers ship disabled.</summary>
        public bool IsPlaced => _placed;

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
            if (visual == null) visual = transform;
            // Ship INVISIBLE (invisible-until-placed, spec §0.1) — re-assert the structure hidden at Awake so a
            // stale/edited scene can never spawn a pre-visible campfire (the Sponsor-locked no-pre-visible rule).
            if (!_placed) SetVisualEnabled(false);
            // Ship dark: the fire does not glow until lit. Defensive — the authored scene already
            // serializes them off, but never assume (a re-enabled-in-editor flame must still ship dark).
            ApplyLitVisuals();
        }

        /// <summary>
        /// Place the campfire at <paramref name="position"/>/<paramref name="rotation"/> (the placement's
        /// confirmed ghost pose) — move this transform there, REVEAL the structure (stone ring + logs),
        /// self-register the no-build zone (#302 seam), and LIGHT it (binds <paramref name="need"/>).
        /// <see cref="CampfirePlacement.TryConfirm"/> calls this after the all-or-nothing wood+stone debit
        /// succeeds. Idempotent (placing an already-placed campfire re-
        /// binds the need via Light but does not re-reveal). Placing == lighting: the mats buy a LIT fire.
        /// </summary>
        public void Build(Vector3 position, Quaternion rotation, WarmthNeed need = null)
        {
            if (!_placed)
            {
                transform.SetPositionAndRotation(position, rotation);
                _placed = true;
                SetVisualEnabled(true);                                          // reveal the structure
                if (placementObstacle != null) placementObstacle.enabled = true; // self-register the no-build zone (#302)
            }
            Light(need); // the shipped warmth seam — lights + binds the need, shows the flame + glow
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

        // Enable/disable the structure's renderers (invisible-until-placed). EXCLUDES the flame child — the
        // flame is toggled independently by the LIT state (ApplyLitVisuals via SetActive), so revealing the
        // structure must not fight it (mirrors Forge.SetVisualEnabled excluding the smelt glow). The fire
        // Light lives on the pit ROOT (not under visual), so it is likewise untouched here — lit-state-driven.
        private void SetVisualEnabled(bool on)
        {
            var root = visual != null ? visual : transform;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (flameVisual != null && r.transform.IsChildOf(flameVisual.transform)) continue; // lit-driven
                r.enabled = on;
            }
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
