using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The PLAYER side of the shared E-LOOT surface (ticket 86caf7a6q — the E-loot FOUNDATION). Pressing E
    /// LOOTS the nearest in-range <see cref="IPickable"/> world item into the inventory: a berry bush, a
    /// stick (86caa96rd), a stone (86caa4c96), and — pending the Sponsor's water answer — a water source.
    /// This REPLACES proximity-auto pickup with an ACTIVE input ([[active-input-not-proximity-auto-for-actions]]:
    /// the Sponsor's explicit "walking up isn't enough — press E" preference) and the per-item bespoke
    /// pickup bindings (the berry bush's old proximity-auto harvest is now routed through this — AC2/AC4).
    ///
    /// === The interaction (AC1/AC3 — E, nearest-in-range, NOT proximity-auto) ===
    /// Each Update, if E rose this frame (or the programmatic <see cref="RequestLoot"/> latch fired — the
    /// headless/shipped-build seam, the input-independent analog of a real key, mirroring ChopTree's
    /// RequestChopClick), resolve the NEAREST loot-able pickable within ITS OWN <see cref="IPickable.LootRange"/>
    /// of the player and call <see cref="IPickable.TryLoot"/> on it. Walking into range does NOTHING until E
    /// is pressed — the load-bearing not-auto rule (AC4 success test: "walk into range with NO E press →
    /// nothing looted"). If several pickables are in range, the NEAREST (planar XZ) wins (AC3) — the same
    /// nearest-wins rule + resolve shape as ChopTree.ResolveNearestChoppable.
    ///
    /// === The guards (mirror ChopTree's world-input guards, minus the mouse-only ones) ===
    /// E loots only when no modal gameplay-UI panel owns the screen (<see cref="UiInputGate.CaptureWorldInput"/>
    /// — don't loot a bush while the inventory/settings panel is open and the player is typing/clicking in it).
    /// The over-UI / RMB-drag guards ChopTree needs are MOUSE-specific (a left-click can land on the
    /// inventory UI or during a camera-orbit drag); E is a keyboard key, so only the modal-panel guard applies.
    /// The decision is a PURE static truth-table (<see cref="ShouldLootOnKey"/>) so the whole guard set is
    /// unit-testable headlessly with no scene/Input rig (sibling of ChopTree.ShouldChopOnClick).
    ///
    /// === Extension point for WATER (AC5 — gated on the Sponsor's water-acquisition answer) ===
    /// The looter is item-AGNOSTIC: it finds IPickables, resolves nearest, and calls TryLoot — it knows
    /// NOTHING about berries vs sticks vs water. When the Sponsor decides how water enters the belt
    /// (E-at-pond-fills-a-slot vs needs-a-container), a water source becomes an IPickable whose TryLoot adds
    /// the water item — and it slots into THIS looter with ZERO rework here (the clean extension point the
    /// ticket requires). No water code lives here now (AC5 deferred).
    ///
    /// === Why a per-frame scene scan is acceptable (and how it stays cheap) ===
    /// Pickables are discovered on Awake (the serialized scene set) + lazily re-discovered only if the cache
    /// is empty (so a pickable authored/spawned later is still found) — NOT a per-frame FindObjectsOfType
    /// (unity6-mastery §5/§6 "no per-frame Find"). The per-press resolve walks the cached list once (one
    /// sqrMagnitude per pickable, no allocation) — and it runs ONLY on the E rising edge, not every frame.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The component + its Inventory/player refs are wired editor-time by BootstrapProject/MovementCameraScene
    /// (serialized into Boot.unity), NOT added at Awake — an Awake-added interactor could ship absent (the
    /// component-in-source-but-not-in-scene class). The Awake fallbacks are a build-safety net only.
    /// PickableLooterSceneTests guards the serialized presence + wiring.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[loot-trace]` lines on the first successful loot + the first empty-press (E with nothing in
    /// range) so the loot input's runtime behaviour is readable from the build log (diagnose-via-trace).
    /// </summary>
    public class PickableLooter : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory looted items land in. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose position the nearest-in-range resolve measures from. Wired at " +
                 "bootstrap; falls back to the ClickToMove root, then this transform.")]
        public Transform player;

        [Header("Input")]
        [Tooltip("The key that LOOTS the nearest in-range pickable. E by default — the universal loot/pick-up " +
                 "key (DECISIONS 2026-06-27: E = loot world items, left-click = use the held belt item). A " +
                 "LETTER key, so it is layout-agnostic on the Sponsor's Danish keyboard " +
                 "([[sponsor-danish-keyboard-layout]]).")]
        public KeyCode lootKey = KeyCode.E;

        // Cached pickable set (the serialized scene pickables, discovered once). Re-discovered lazily only
        // when empty so a later-authored/spawned pickable is still found — never a per-frame Find.
        private readonly List<IPickable> _pickables = new List<IPickable>();
        private bool _discovered;

        // Programmatic latch — the input-independent analog of a real E rising edge (headless / shipped-build
        // capture seam, mirroring ChopTree.RequestChopClick). Consumed once per Update so it can't stick.
        private bool _lootRequested;

        // One-shot trace guards (don't spam the log per press).
        private bool _tracedFirstLoot;
        private bool _tracedFirstEmpty;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                player = ctm != null ? ctm.transform : transform;
            }
            DiscoverPickables();
        }

        void Update()
        {
            // Read the real E rising edge OR consume the programmatic latch. Consume the latch
            // unconditionally each frame (one loot per RequestLoot) so it can't stick across frames.
            bool keyEdge = _lootRequested || Input.GetKeyDown(lootKey);
            _lootRequested = false;
            if (!keyEdge) return;

            TryLootNearest();
        }

        /// <summary>
        /// Attempt to loot the NEAREST in-range loot-able pickable (AC1/AC3). The whole loot decision:
        ///   • a pickable is in reach (the nearest, within its own LootRange) — supplied by the resolve;
        ///   • no modal gameplay-UI panel owns the screen (<see cref="ShouldLootOnKey"/> guard).
        /// On success the item's <see cref="IPickable.TryLoot"/> runs the full transaction (add the canonical
        /// resource + consume/deplete the world instance). Returns true iff exactly one item was looted.
        /// Public + scene-driven so the PlayMode test + the shipped-build capture drive a loot without a key
        /// device — and an empty/out-of-range press is a clean no-op (the not-auto / nothing-in-range case).
        /// </summary>
        public bool TryLootNearest()
        {
            if (inventory == null || player == null) return Empty();

            IPickable target = ResolveNearestPickable(player.position);
            bool inRange = target != null;

            if (!ShouldLootOnKey(inRange, UiInputGate.CaptureWorldInput))
                return Empty();

            bool looted = target.TryLoot(inventory);
            if (looted)
            {
                if (!_tracedFirstLoot)
                {
                    _tracedFirstLoot = true;
                    LootTrace("looted 1 from the nearest pickable (key=" + lootKey + ")");
                }
                return true;
            }
            return Empty(); // in range but TryLoot declined (full pack / spent item) — clean no-op
        }

        // First empty press (E with nothing loot-able in range) → one-shot trace; always returns false.
        private bool Empty()
        {
            if (!_tracedFirstEmpty)
            {
                _tracedFirstEmpty = true;
                LootTrace("loot key pressed with NO loot-able pickable in range -> no-op (key=" + lootKey + ")");
            }
            return false;
        }

        /// <summary>
        /// Request ONE loot programmatically — the input-independent analog of an E rising edge (the headless
        /// PlayMode + shipped-build capture seam, mirroring ChopTree.RequestChopClick). Latched + consumed on
        /// the next Update (one loot per call). The nearest-in-range + modal-panel guards still apply.
        /// </summary>
        public void RequestLoot() => _lootRequested = true;

        /// <summary>
        /// The SINGLE source of truth the proximity prompt reads (ticket 86cafc6ud AC3): the nearest loot-able
        /// <see cref="IPickable"/> in reach of the player RIGHT NOW, or null when nothing is in range. The prompt
        /// (<see cref="LootPrompt"/>) calls THIS — the SAME <see cref="ResolveNearestPickable"/> the E press
        /// uses against the SAME <see cref="player"/> position — so the prompt and the actual loot can never
        /// disagree about what's reachable (if the prompt names "berries", pressing E loots THAT bush). Returns
        /// null (prompt hides) when player/inventory is unwired or nothing loot-able is within its own LootRange.
        /// One cheap cached-list pass (no allocation, no Find) — the prompt may call it once per frame.
        /// </summary>
        public IPickable NearestInRange()
        {
            if (player == null) return null;
            return ResolveNearestPickable(player.position);
        }

        /// <summary>
        /// Resolve the NEAREST loot-able <see cref="IPickable"/> within ITS OWN <see cref="IPickable.LootRange"/>
        /// of <paramref name="from"/> (AC3 — nearest-in-range wins). Spent / not-loot-able pickables
        /// (<see cref="IPickable.CanLoot"/> == false) are skipped. Returns null when nothing loot-able is in
        /// reach (E is then a harmless no-op). Planar (XZ) distance only (height-robust — same as
        /// ChopTree.ResolveNearestChoppable / BerryBush / CraftSpot). Picks the smallest distance; on a tie the
        /// earlier-discovered one wins (deterministic). Discovers the pickable set lazily if not yet cached.
        /// </summary>
        public IPickable ResolveNearestPickable(Vector3 from)
        {
            EnsureDiscovered();
            IPickable best = null;
            float bestSq = float.MaxValue;
            Vector2 here = new Vector2(from.x, from.z);
            for (int i = 0; i < _pickables.Count; i++)
            {
                IPickable p = _pickables[i];
                if (p == null || !p.CanLoot) continue;
                Vector3 pos = p.LootPosition;
                float dSq = (here - new Vector2(pos.x, pos.z)).sqrMagnitude;
                float range = p.LootRange;
                if (dSq <= range * range && dSq < bestSq)
                {
                    bestSq = dSq;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// PURE loot-on-a-key decision (the unit-testable guard truth-table — sibling of ChopTree.
        /// ShouldChopOnClick). Given an E rising edge this frame, decide whether ONE loot should land:
        ///   • <paramref name="inRange"/>     — SOME loot-able pickable is in reach (the nearest);
        ///   • NOT <paramref name="uiPanelOpen"/> — no modal gameplay-UI panel owns the screen.
        /// Both must hold. Static + dependency-free so the EditMode guard asserts the table with no scene/Input
        /// rig. (E is a keyboard key, so ChopTree's mouse-only over-UI / RMB-drag guards do not apply here.)
        /// </summary>
        public static bool ShouldLootOnKey(bool inRange, bool uiPanelOpen)
            => inRange && !uiPanelOpen;

        /// <summary>
        /// Register ONE runtime-spawned <see cref="IPickable"/> into the cached set so the looter finds it on the
        /// NEXT E press — the seam a RUNTIME drop (a felled tree's <see cref="LogPile"/>, 86caf9u5t) uses to enter
        /// the loot set. The lazy <see cref="EnsureDiscovered"/> re-scan ONLY fires when the cache is EMPTY, so a
        /// pile spawned while ≥1 serialized pickable already exists (the live build ALWAYS has the bush/stick/stone)
        /// would otherwise NEVER be discovered → never looted (the #165 bug). This explicit registration is the
        /// fix: <see cref="LogPileSpawner.SpawnAt"/> calls it on every spawned pile. Discovers the serialized set
        /// first if Awake hasn't run yet (so the new pile JOINS the set, not REPLACES it), and dedups (a double
        /// register is a no-op) so a re-registered or re-discovered pile is never double-counted. Null is ignored.
        /// </summary>
        public void RegisterPickable(IPickable pickable)
        {
            if (pickable == null) return;
            EnsureDiscovered();                       // seed the serialized set first; never REPLACE it with one item
            if (!_pickables.Contains(pickable))       // dedup — a double register (or a later re-discover) is a no-op
                _pickables.Add(pickable);
        }

        /// <summary>
        /// Force a re-scan of the scene for <see cref="IPickable"/> components — the test/bootstrap seam for
        /// a pickable set that changes after Awake (a PlayMode test that adds a pickable post-construction).
        /// Replaces the cache. The live build relies on the Awake discovery + the lazy empty re-discover.
        /// </summary>
        public void DiscoverPickables()
        {
            _pickables.Clear();
            // FindObjectsOfType<MonoBehaviour> + interface filter: Unity can't FindObjectsOfType<IPickable>
            // directly (it's an interface, not a UnityEngine.Object). One-shot at Awake (NOT per-frame).
            var behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPickable pick)
                    _pickables.Add(pick);
            _discovered = true;
        }

        // Discover the set on first use if Awake hasn't run yet (EditMode/PlayMode tests that call the
        // resolve before/without a full lifecycle), AND re-discover if the cache is empty (a pickable
        // authored after Awake) — never a per-frame Find.
        private void EnsureDiscovered()
        {
            if (!_discovered || _pickables.Count == 0) DiscoverPickables();
        }

        // [loot-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call +
        // its argument evaluation (the string concat) from the shipped IL2CPP release exe so the trace costs
        // the player nothing (unity6-mastery §5 "no Debug.Log in hot paths" / §10 "strip logging from shipping
        // builds"). The first-press guards keep it one-shot. Matches the project dev-log gate convention
        // (BerryBush [bush-trace] / EatBerryAction [eat-trace] / AxePickup [AxePickup]).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LootTrace(string msg) => Debug.Log("[loot-trace] " + msg);
    }
}
