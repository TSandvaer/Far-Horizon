using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A loose SMALL STONE / pebble (ticket 86caa4c96) — an E-LOOT pickable scattered across the seed-42
    /// island. The castaway walks up to a small stone on the ground and presses E to LOOT it (the universal
    /// pick-up/loot verb — DECISIONS 2026-06-27): the stone yields exactly ONE <see cref="ItemCatalog.StoneId"/>
    /// "stone" into the inventory and is then CONSUMED from that spot. UNLIKE the BIGGER rocks (boulders, the
    /// FUTURE pickaxe-MINING target — OOS here), only these SMALL stones are pickable. This is the sibling of
    /// the fallen STICK (<see cref="StickProp"/>, the 1-wood gather) — same E-loot idiom, different resource.
    ///
    /// === IMPLEMENTS <see cref="IPickable"/> — the shared E-loot surface (86caf7a6q AC1/AC2) ===
    /// The stone is an <see cref="IPickable"/> on the shared E-loot surface, the SAME idiom as
    /// <see cref="StickProp"/> / <see cref="BerryBush"/> (the reference impls): the player-side
    /// <see cref="PickableLooter"/> discovers every IPickable, resolves the nearest in-range one, and calls
    /// <see cref="TryLoot"/> when E is pressed. The stone adds NO bespoke pickup input, NO second looter, NO
    /// parallel pickable interface — it just IS an IPickable; the looter finds it. Walking into range does
    /// NOTHING until E (the not-auto rule the looter enforces — AC5).
    ///
    /// === The loot CONTRACT (AC2) ===
    /// <see cref="TryLoot"/> is the WHOLE one-loot transaction: add ONE <see cref="ItemCatalog.StoneId"/>
    /// "stone" to the inventory (the canonical id verbatim — never a parallel "pebble"/"smallstone" id; the
    /// model's AddItem seam stacks it per the stone stack-size) AND consume the world stone (hide its visual
    /// so it can't be looted twice). Returns true IFF exactly one stone actually landed — a full pack lands 0
    /// → returns false, a clean no-op the looter moves past (the stone is NOT consumed on a declined loot, so
    /// the player can come back for it once there's room). The stone owns its OWN id + consume rule; the looter
    /// never assumes one.
    ///
    /// === RESPAWN (AC3) — the stone REGROWS on a per-spot timer (DIFFERS from the stick) ===
    /// A stick is a FINITE one-off gather (consumed whole, never returns). A STONE RESPAWNS: on loot the spot
    /// goes EMPTY (the stone visual hides) and schedules a respawn at a RANDOM time within
    /// [<see cref="RespawnMinSeconds"/>, <see cref="RespawnMaxSeconds"/>] (like the berry-bush regrow / tree
    /// regrowth). When the timer elapses the stone REAPPEARS (loot-able again). The min/max are the live-tunable
    /// SOURCE the `stone respawn time` setting drives (AC3a) — but they live on the shared
    /// <see cref="StoneRespawner"/> (the single "stone-spawn component" the setting binds to), so one slider
    /// retunes EVERY stone's respawn window. Each StoneProp reads the window through its
    /// <see cref="respawner"/> ref (a per-instance fallback band when unset, for a test/edge stone). Because
    /// the timer runs in <see cref="Update"/>, the component must stay ACTIVE while empty — so loot hides a
    /// CHILD visual (<see cref="stoneVisual"/>), NOT the whole GameObject (deactivating the GO would freeze the
    /// respawn timer, the BerryBush precedent).
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The stone GameObject + its mesh + this component + the inventory/respawner refs are authored editor-time
    /// by the world scatter (LowPolyZoneGen.BuildStone) + the fixed wired stone (MovementCameraScene.
    /// BuildWiredStone), serialized into Boot.unity — NOT added at Awake (an Awake-built interaction/visual
    /// could ship MANGLED/absent, the legs-up class). StoneSceneTests guards the scene presence + that the refs
    /// serialize, sibling of StickSceneTests.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[stone-trace]` lines on the first successful loot + the first declined loot + the first
    /// respawn so the stone's runtime state is readable from the build log (the diagnose-via-trace discipline;
    /// sibling of the [stick-trace] / [bush-trace] / [loot-trace] lines). EDITOR-only (stripped from the
    /// shipped IL2CPP release exe).
    /// </summary>
    public class StoneProp : MonoBehaviour, IPickable
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory the looted stone is added to. Wired at bootstrap by the scatter; scene-found " +
                 "fallback in Awake (build-safety net only — the serialized ref is the source of truth).")]
        public Inventory inventory;

        [Tooltip("The stone visual root — shown when PRESENT, hidden when EMPTY (respawning). Wired by the " +
                 "scatter. If unwired the loot still works (the stone just doesn't visibly toggle); the whole " +
                 "object is the fallback visual.")]
        public Transform stoneVisual;

        [Tooltip("The shared stone-respawn config (the single 'stone-spawn component' the `stone respawn time` " +
                 "setting binds to — AC3a). When set, the respawn window is read from it so ONE slider retunes " +
                 "EVERY stone. May be null (a test/edge stone) → the per-instance fallback band below is used.")]
        public StoneRespawner respawner;

        [Header("Loot (AC2)")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the stone and can loot it on E. " +
                 "ARM'S-REACH (86cafc6ud — the Sponsor's #155 'I can loot from too far' soak): a pebble on the " +
                 "ground requires getting genuinely close (you stoop to pick it up), tightened from the old 1.6. " +
                 "Mirrors the per-item LootRange idiom; SCALED by the stone size so a bigger small-stone is " +
                 "loot-able from a touch farther. This is the stone's own IPickable.LootRange.")]
        public float lootRadius = 1.0f;

        [Tooltip("Stone yielded per pickup. ONE — a small stone is a single-stone gather (the early-game " +
                 "scavenge before the pickaxe). Kept a field (not a magic literal) so it can be promoted to a " +
                 "setting later if the Sponsor asks; v1 stays the 1-stone gather.")]
        public int stonePerPickup = StonePerPickupDefault;

        [Header("Respawn fallback (AC3 — used only when no shared respawner is wired)")]
        [Tooltip("Per-instance fallback minimum respawn seconds (used ONLY when no shared StoneRespawner is " +
                 "wired). The shared respawner is the live-tunable source the setting drives; this band keeps a " +
                 "lone test/edge stone respawning sanely.")]
        public float respawnMinFallback = StoneRespawner.DefaultMinSeconds;

        [Tooltip("Per-instance fallback maximum respawn seconds (used only when no shared respawner is wired).")]
        public float respawnMaxFallback = StoneRespawner.DefaultMaxSeconds;

        [Tooltip("Deterministic seed for the respawn-time roll (so headless tests are reproducible). 0 = use " +
                 "a time-based seed at runtime.")]
        public int respawnSeed = 0;

        /// <summary>The AC contrast: a small stone yields ONE stone per pickup. A NAMED constant (not a magic
        /// literal scattered through the build) so a future setting can drive it without hunting down
        /// call-sites — the sibling of <see cref="StickProp.WoodPerStickDefault"/>.</summary>
        public const int StonePerPickupDefault = 1;

        // Runtime state — a stone is PRESENT (loot-able) until looted, then EMPTY (respawning) until the timer
        // elapses, then PRESENT again. The respawn loop persists the spot; only the stone toggles (AC3).
        private bool _present = true;
        private float _respawnAt;          // wall-clock time the stone respawns (only meaningful while empty)
        private System.Random _rng;
        private bool _tracedFirstLoot;     // one-shot trace guards (don't spam the log per press)
        private bool _tracedFirstDeclined;
        private bool _tracedFirstRespawn;

        /// <summary>True while the stone is PRESENT (loot-able). False while EMPTY (respawning). Exposed for
        /// PlayMode tests + the visual toggle + the looter's resolve (an empty spot is skipped — so E never
        /// "loots nothing" off a spot that's mid-respawn).</summary>
        public bool IsAvailable => _present;

        /// <summary>Wall-clock time the stone is scheduled to respawn (only meaningful while empty). Exposed
        /// for PlayMode tests (assert the timer scheduled within the tweakable window).</summary>
        public float RespawnAt => _respawnAt;

        // ============================================================================================
        // IPickable — the WORLD-ITEM side of the shared E-loot surface (86caf7a6q AC1/AC2). The
        // PickableLooter resolves the nearest in-range CanLoot pickable and calls TryLoot on E.
        // ============================================================================================

        /// <summary>IPickable: the stone is loot-able while it is PRESENT (not mid-respawn) AND an inventory is
        /// wired. An empty (respawning) stone returns false — the looter's nearest-in-range resolve skips it
        /// (so E never targets a spot that's mid-respawn).</summary>
        public bool CanLoot => _present && inventory != null;

        /// <summary>IPickable: the stone's world position (the looter measures planar XZ distance to this for
        /// the nearest-in-range resolve — height-robust, the same idiom as StickProp / BerryBush).</summary>
        public Vector3 LootPosition => transform.position;

        /// <summary>IPickable: the stone's loot reach — its own <see cref="lootRadius"/> SCALED by the stone
        /// size (localScale.x: the scatter varies it) so a bigger small-stone is loot-able from a touch farther
        /// and a tiny pebble requires getting close (the reach matches what the player sees; mirrors StickProp.
        /// LootRange). The looter uses THIS per-item radius, not one global radius.</summary>
        public float LootRange => lootRadius * transform.localScale.x;

        /// <summary>IPickable: the generic prompt name (86cafc6ud) — a stone yields "stones" (the canonical
        /// StoneId resource). The prompt shows "Press E to pick up stones".</summary>
        public string DisplayName => "stones";

        /// <summary>
        /// IPickable.TryLoot (86caf7a6q AC1 / 86caa4c96 AC2) — loot this stone into <paramref name="inv"/>: the
        /// whole transaction is add ONE <see cref="ItemCatalog.StoneId"/> "stone" (the canonical id verbatim,
        /// via the item-model AddItem seam — stacks per the stone stack-size) AND consume the world stone (go
        /// EMPTY + schedule respawn — AC3). Returns true IFF the stone actually landed (a full pack lands 0 →
        /// returns false and the stone is NOT consumed — a clean no-op the looter moves past; the player can
        /// re-loot it). Uses the wired <see cref="inventory"/> (the stone owns its inventory ref);
        /// <paramref name="inv"/> is accepted for the interface contract + used when the stone's own ref is
        /// unset (test/edge safety).
        /// </summary>
        public bool TryLoot(Inventory inv)
        {
            if (inventory == null) inventory = inv;
            if (inventory == null || !_present) return false;

            var catalog = inventory.Catalog;
            ItemDef stone = catalog != null ? catalog.ById(ItemCatalog.StoneId) : null;
            if (stone == null) return Declined("no stone def in catalog");

            int amount = Mathf.Max(1, stonePerPickup);
            int leftover = inventory.Model.AddItem(stone, amount);
            int added = amount - leftover;
            if (added <= 0) return Declined("inventory full (added 0)");

            // Consume the stone at this spot: go EMPTY (hide the visual) + schedule a respawn (AC3). The
            // COMPONENT stays active (Update runs the respawn timer); only the stone visual toggles — the
            // BerryBush precedent (deactivating the whole GameObject would freeze the respawn timer).
            _present = false;
            ApplyPresentVisual();
            ScheduleRespawn();

            if (!_tracedFirstLoot)
            {
                _tracedFirstLoot = true;
                StoneTrace("looted +" + added + " stone (yield=" + amount + ", leftover=" + leftover +
                           ") -> stone=" + inventory.Model.CountItem(ItemCatalog.StoneId) +
                           "; spot EMPTY, respawn in " + (_respawnAt - Time.time).ToString("F1") + "s");
            }
            return true;
        }

        // A declined loot (full pack / no stone def) — the stone is NOT consumed; the looter reports false.
        private bool Declined(string why)
        {
            if (!_tracedFirstDeclined)
            {
                _tracedFirstDeclined = true;
                StoneTrace("loot DECLINED (" + why + ") -> stone NOT consumed, clean no-op");
            }
            return false;
        }

        void Awake()
        {
            // Build-safety net only: the serialized inventory/respawner refs (wired by the scatter) are the
            // source of truth. A scene-found fallback so a test/edge-built stone still loots/respawns; never a
            // per-loot Find (unity6-mastery §6 "no per-frame/per-use Find").
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (respawner == null) respawner = FindObjectOfType<StoneRespawner>();
            _rng = new System.Random(respawnSeed != 0 ? respawnSeed : Environment.TickCount);
            ApplyPresentVisual();
        }

        void Update()
        {
            // Update ONLY respawns the stone (the spot persists; only the stone toggles — AC3). The LOOT is NOT
            // proximity-auto (86caf7a6q AC5): the player presses E and the PickableLooter calls TryLoot. Walking
            // into range does NOTHING here — there is no proximity distance read at all, which is the structural
            // proof the stone can't auto-loot.
            if (!_present && Time.time >= _respawnAt)
                Respawn();
        }

        /// <summary>Schedule the respawn at a RANDOM time within the tweakable [min,max] window (AC3). The
        /// window comes from the shared <see cref="StoneRespawner"/> when wired (so one slider retunes every
        /// stone — AC3a), else the per-instance fallback band. Min clamped non-negative; max clamped to >= min
        /// so a mis-authored max never schedules a respawn in the past. Public so PlayMode tests can drive it.</summary>
        public void ScheduleRespawn()
        {
            float min = respawner != null ? respawner.RespawnMinSeconds : respawnMinFallback;
            float max = respawner != null ? respawner.RespawnMaxSeconds : respawnMaxFallback;
            min = Mathf.Max(0f, min);
            max = Mathf.Max(min, max);
            // Lazily init the RNG in case ScheduleRespawn is driven before Awake (a bare PlayMode/test rig).
            if (_rng == null) _rng = new System.Random(respawnSeed != 0 ? respawnSeed : Environment.TickCount);
            float delay = min + (float)_rng.NextDouble() * (max - min);
            _respawnAt = Time.time + delay;
        }

        // The stone returns: present again, visible again. The SPOT persisted the whole time (AC3).
        private void Respawn()
        {
            _present = true;
            ApplyPresentVisual();
            if (!_tracedFirstRespawn)
            {
                _tracedFirstRespawn = true;
                StoneTrace("Respawn -> stone PRESENT again (spot persisted, loot-able)");
            }
        }

        // Show/hide the stone visual to match present state (the SPOT/component stays; only the stone toggles).
        // Falls back to the whole GameObject's renderer if no explicit visual child is wired (still keeps the
        // component active so the respawn timer runs — we toggle the renderer-bearing child, not the GO).
        private void ApplyPresentVisual()
        {
            if (stoneVisual != null)
                stoneVisual.gameObject.SetActive(_present);
        }

        // [stone-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call
        // (AND its argument evaluation, incl. the string concatenation) from the shipped IL2CPP release exe,
        // so the trace never costs the player a string alloc + log write (unity6-mastery §5 "no Debug.Log in
        // hot paths" / §10 "strip all logging from shipping builds"). The first-time guards keep it one-shot.
        // Matches the project dev-log gate convention (StickProp [stick-trace] / BerryBush [bush-trace]).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void StoneTrace(string msg) => Debug.Log("[stone-trace] " + msg);
    }
}
