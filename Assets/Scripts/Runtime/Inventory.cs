using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The held-resource ledger for the M-U2 thin survival loop (ticket 86ca8bdaq, U2-2).
    ///
    /// The entry to the loop is crafting the axe (the chop tool); this is the data SEED the
    /// inventory readout reads. Deliberately THIN: two held resources this milestone — the axe
    /// (a have/have-not flag) and wood (a running count the chop step, U2-3, will increment).
    /// No item grid, no recipe system, no durability (all out of scope per ticket / future texture).
    ///
    /// === Public data surface (U2-5 HUD — Uma/Devon — consumes EXACTLY this) ===
    /// VOCABULARY CONTRACT (team/uma-ux/u2-5-survival-hud-spec.md §7 Q4 — these EXACT names):
    ///   bool  HasAxe     : true once the axe is crafted. The HUD shows the axe slot only when true
    ///                      (absent-when-false — no "axe x0" clutter, per the diegetic-quiet spec).
    ///   int   WoodCount  : live wood tally (0 before any chop). The HUD leans absent-when-zero.
    ///   event Action Changed : fires whenever HasAxe or WoodCount changes (craft OR future chop).
    ///                      The HUD subscribes and never polls — same pattern as WarmthNeed.Changed.
    /// The HUD treats this component as READ-ONLY (subscribe to Changed, read HasAxe / WoodCount);
    /// only gameplay (the craft spot here, the chop step in U2-3) writes, via CraftAxe / AddWood.
    /// Keep this surface stable — U2-5's wiring consumes these names verbatim.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// Serialized into the Boot scene editor-time (BootstrapProject), NOT added at Awake — an
    /// Awake-only add could ship mangled/absent. CraftSceneTests guards the scene presence,
    /// the sibling of WarmthNeedSceneTests.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        // Backing fields — mutated only through the single write paths so Changed fires consistently.
        [SerializeField, Tooltip("Whether the axe has been crafted. Starts false; the craft spot sets it.")]
        private bool _hasAxe;

        [SerializeField, Tooltip("Wood units held. Starts 0; the chop step (U2-3) increments it.")]
        private int _woodCount;

        /// <summary>Fires whenever HasAxe or WoodCount changes (craft OR chop). The HUD subscribes
        /// and never polls — mirrors WarmthNeed.Changed so U2-5 wires both the same way.</summary>
        public event Action Changed;

        /// <summary>True once the axe is crafted. The HUD shows the axe slot only when true.</summary>
        public bool HasAxe => _hasAxe;

        /// <summary>Live wood tally. 0 before any chop (U2-3). The HUD leans absent-when-zero.</summary>
        public int WoodCount => _woodCount;

        /// <summary>
        /// Craft the axe — THE entry to the survival loop (the single recipe this milestone). Idempotent:
        /// crafting an axe you already hold is a no-op (no Changed, no double-fire). Returns true only on
        /// the transition false->true, so the craft spot can play its one-shot feedback exactly once.
        /// </summary>
        public bool CraftAxe()
        {
            if (_hasAxe) return false; // already crafted — idempotent, no event
            _hasAxe = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Add wood to the ledger (the chop step, U2-3, calls this). Clamped at 0 — never negative.
        /// Seeded here so U2-2's inventory surface is complete the moment U2-3 wires the chop, with
        /// no second data-surface PR. A zero/negative amount is a no-op (no Changed).
        /// </summary>
        public int AddWood(int amount)
        {
            if (amount <= 0) return _woodCount;
            _woodCount += amount;
            Changed?.Invoke();
            return _woodCount;
        }

        /// <summary>
        /// Spend wood from the ledger — the seam the campfire (U2-4) consumes to BUILD: a campfire
        /// costs wood, so placing one debits it here. ALL-OR-NOTHING gate (matches the Don't-Starve
        /// "you have the mats or you don't" feel): if the ledger holds fewer than <paramref name="amount"/>,
        /// NOTHING is spent and false is returned (no partial debit, no Changed) — this is the
        /// load-bearing "no wood -> no campfire" negative case. On success debits the wood, fires
        /// Changed (so the HUD's wood count drops), and returns true. A zero/negative amount is a
        /// no-op that returns true (asking to spend nothing always "succeeds", changes nothing).
        /// </summary>
        public bool SpendWood(int amount)
        {
            if (amount <= 0) return true;        // spending nothing trivially succeeds, no event
            if (_woodCount < amount) return false; // can't afford -> spend nothing (the build gate)
            _woodCount -= amount;
            Changed?.Invoke();
            return true;
        }
    }
}
