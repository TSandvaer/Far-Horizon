using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A fallen STICK / BRANCH (ticket 86caa96rd) — the LOW-yield wood source. The castaway walks up to a
    /// stick on the ground and presses E to LOOT it (the universal pick-up/loot verb — DECISIONS 2026-06-27):
    /// the stick yields exactly ONE <see cref="ItemCatalog.WoodId"/> "wood" into the inventory and is then
    /// CONSUMED (removed from the world). This is FAR LESS than chopping a tree (the high-yield source,
    /// 86caa4c5c) — a stick is a quick one-wood gather, the early-game scavenge before the axe (AC3).
    ///
    /// === IMPLEMENTS <see cref="IPickable"/> — the shared E-loot surface (86caf7a6q AC1/AC2) ===
    /// The stick is an <see cref="IPickable"/> on the shared E-loot surface, the SAME idiom as
    /// <see cref="BerryBush"/> (the reference impl): the player-side <see cref="PickableLooter"/> discovers
    /// every IPickable, resolves the nearest in-range one, and calls <see cref="TryLoot"/> when E is pressed.
    /// The stick adds NO bespoke pickup input, NO second looter, NO parallel pickable interface — it just IS
    /// an IPickable; the looter finds it. Walking into range does NOTHING until E (the not-auto rule the
    /// looter enforces — AC6).
    ///
    /// === The loot CONTRACT (AC2/AC3) ===
    /// <see cref="TryLoot"/> is the WHOLE one-loot transaction: add ONE <see cref="ItemCatalog.WoodId"/>
    /// "wood" to the inventory (the canonical id verbatim — never a parallel "stick"/"branch" id; the model's
    /// AddItem seam stacks it per the wood stack-size) AND consume the world stick (deactivate it so it can't
    /// be looted twice). Returns true IFF exactly one wood actually landed — a full pack lands 0 → returns
    /// false, a clean no-op the looter moves past (the stick is NOT consumed on a declined loot, so the player
    /// can come back for it once there's room). The stick owns its OWN id + consume rule; the looter never
    /// assumes one.
    ///
    /// === No respawn in v1 (AC5) ===
    /// A looted stick is a FINITE early-game gather — it does NOT respawn on a per-spot timer (unlike the
    /// berry bush, which persists + regrows berries; a stick is consumed whole). Once looted it is gone from
    /// the world. (Surfacing a respawn option to the Sponsor at soak is a follow-up if the scatter reads thin.)
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The stick GameObject + its mesh + this component + the inventory ref are authored editor-time by the
    /// world scatter (LowPolyZoneGen.BuildStick), serialized into Boot.unity — NOT added at Awake (an
    /// Awake-built interaction/visual could ship MANGLED/absent, the legs-up class). StickSceneTests guards
    /// the scene presence + that the inventory ref serializes, sibling of BushSceneTests.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[stick-trace]` lines on the first successful loot + the first declined loot so the stick's
    /// runtime state is readable from the build log (the diagnose-via-trace discipline; sibling of the
    /// [bush-trace] / [loot-trace] lines). EDITOR-only (stripped from the shipped IL2CPP release exe).
    /// </summary>
    public class StickProp : MonoBehaviour, IPickable
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory the looted wood is added to. Wired at bootstrap by the scatter; scene-found " +
                 "fallback in Awake (build-safety net only — the serialized ref is the source of truth).")]
        public Inventory inventory;

        [Header("Loot (AC2/AC3)")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the stick and can loot it on E. " +
                 "ARM'S-REACH (86cafc6ud — the Sponsor's #155 'I can loot from too far' soak): a stick on the " +
                 "ground requires getting genuinely close (tighter than a bush — you stoop to pick it up), " +
                 "tightened from the old 1.6. Mirrors the per-item LootRange idiom; SCALED by the stick size " +
                 "so a longer branch is loot-able from a touch farther. This is the stick's own IPickable.LootRange.")]
        public float lootRadius = 1.0f;

        [Tooltip("Wood yielded per stick pickup. ONE — the named-constant low-yield contrast vs chopping a " +
                 "tree (AC3). Kept a field (not a magic literal) so it can be promoted to a setting later " +
                 "if the Sponsor asks; v1 stays the 1-wood contrast.")]
        public int woodPerStick = WoodPerStickDefault;

        /// <summary>The AC3 low-yield contrast: a stick yields ONE wood per pickup (FAR less than a tree
        /// chop). A NAMED constant (not a magic literal scattered through the build) so a future setting can
        /// drive it without hunting down call-sites — the ticket's "keep it a named constant" requirement.</summary>
        public const int WoodPerStickDefault = 1;

        // Runtime state — a stick is loot-able until it is consumed (looted whole; no regrow — AC5).
        private bool _consumed;
        private bool _tracedFirstLoot;     // one-shot trace guards (don't spam the log per press)
        private bool _tracedFirstDeclined;

        /// <summary>True until this stick has been looted. Exposed for PlayMode tests + the looter's resolve
        /// (a consumed stick is skipped — so E never "loots nothing" off an already-picked stick).</summary>
        public bool IsAvailable => !_consumed;

        // ============================================================================================
        // IPickable — the WORLD-ITEM side of the shared E-loot surface (86caf7a6q AC1/AC2). The
        // PickableLooter resolves the nearest in-range CanLoot pickable and calls TryLoot on E.
        // ============================================================================================

        /// <summary>IPickable: the stick is loot-able while it is present (not yet consumed) AND an inventory
        /// is wired. A consumed stick returns false — the looter's nearest-in-range resolve skips it (so E
        /// never targets an already-looted stick).</summary>
        public bool CanLoot => !_consumed && inventory != null;

        /// <summary>IPickable: the stick's world position (the looter measures planar XZ distance to this for
        /// the nearest-in-range resolve — height-robust, the same idiom as BerryBush / ChopTree).</summary>
        public Vector3 LootPosition => transform.position;

        /// <summary>IPickable: the stick's loot reach — its own <see cref="lootRadius"/> SCALED by the stick
        /// size (localScale.x: the scatter varies it) so a longer branch is loot-able from a touch farther
        /// and a tiny twig requires getting close (the reach matches what the player sees; mirrors BerryBush.
        /// LootRange). The looter uses THIS per-item radius, not one global radius.</summary>
        public float LootRange => lootRadius * transform.localScale.x;

        /// <summary>IPickable: the generic prompt name (86cafc6ud) — a stick yields "wood" (the canonical
        /// WoodId resource). The prompt shows "Press E to pick up wood" — the SAME word the future tree-chop
        /// log-pile (86caf9u5t) returns, so both read identically with no per-item HUD branch.</summary>
        public string DisplayName => "wood";

        /// <summary>
        /// IPickable.TryLoot (86caf7a6q AC1 / AC2) — loot this stick into <paramref name="inv"/>: the whole
        /// transaction is add ONE <see cref="ItemCatalog.WoodId"/> "wood" (the canonical id verbatim, via the
        /// item-model AddItem seam — stacks per the wood stack-size) AND consume the world stick (deactivate
        /// it). Returns true IFF the wood actually landed (a full pack lands 0 → returns false and the stick is
        /// NOT consumed — a clean no-op the looter moves past; the player can re-loot it once there's room).
        /// Uses the wired <see cref="inventory"/> (the stick owns its inventory ref); <paramref name="inv"/> is
        /// accepted for the interface contract + used when the stick's own ref is unset (test/edge safety).
        /// </summary>
        public bool TryLoot(Inventory inv)
        {
            if (inventory == null) inventory = inv;
            if (inventory == null || _consumed) return false;

            var catalog = inventory.Catalog;
            ItemDef wood = catalog != null ? catalog.ById(ItemCatalog.WoodId) : null;
            if (wood == null) return Declined("no wood def in catalog");

            int amount = Mathf.Max(1, woodPerStick);
            int leftover = inventory.Model.AddItem(wood, amount);
            int added = amount - leftover;
            if (added <= 0) return Declined("inventory full (added 0)");

            // Consume the world stick: hide it so it can't be looted twice AND it visibly disappears (looted
            // whole — no regrow, AC5). SetActive(false) (not Destroy) keeps the GameObject around for the
            // looter's cached list (CanLoot now false → skipped) without a per-loot Destroy churn.
            _consumed = true;
            gameObject.SetActive(false);

            if (!_tracedFirstLoot)
            {
                _tracedFirstLoot = true;
                StickTrace("looted +" + added + " wood (yield=" + amount + ", leftover=" + leftover +
                           ") -> wood=" + inventory.Model.CountItem(ItemCatalog.WoodId) + "; stick CONSUMED");
            }
            return true;
        }

        // A declined loot (full pack / no wood def) — the stick is NOT consumed; the looter reports false.
        private bool Declined(string why)
        {
            if (!_tracedFirstDeclined)
            {
                _tracedFirstDeclined = true;
                StickTrace("loot DECLINED (" + why + ") -> stick NOT consumed, clean no-op");
            }
            return false;
        }

        void Awake()
        {
            // Build-safety net only: the serialized inventory ref (wired by the scatter) is the source of
            // truth. A scene-found fallback so a test/edge-built stick still loots; never a per-loot Find
            // (unity6-mastery §6 "no per-frame/per-use Find").
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
        }

        // [stick-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call
        // (AND its argument evaluation, incl. the string concatenation) from the shipped IL2CPP release exe,
        // so the trace never costs the player a string alloc + log write (unity6-mastery §5 "no Debug.Log in
        // hot paths" / §10 "strip all logging from shipping builds"). The first-time guards keep it one-shot.
        // Matches the project dev-log gate convention (BerryBush [bush-trace] / PickableLooter [loot-trace]).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void StickTrace(string msg) => Debug.Log("[stick-trace] " + msg);
    }
}
