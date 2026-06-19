using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The THIRST survival need (ticket 86caamkv7). IS-A <see cref="SurvivalNeed"/> — it inherits the
    /// whole decay/floor/satisfy/HUD-contract surface and adds the water-specific satisfaction hook
    /// <see cref="AddWater"/> plus the drink-from-hand seam <see cref="TryDrink"/>.
    ///
    /// Castaway fiction (vision-far-horizon-game-concept.md): "The young man begins to get thirsty after
    /// eating the berries and goes look for a pond of fresh water. He finds a small pond and drinks with
    /// his hand, satisfying small amount of thirst with each scoop." So thirst decays FASTER than hunger
    /// (pressing "after eating the berries" — default medDecayPerSecond 0.45 sits between hunger's 0.35
    /// and warmth's 0.55) and a hand-scoop restores only a SMALL fixed amount.
    ///
    /// === The drink seam (AC3 / AC3a — this ticket OWNS the drink-scoop -&gt; thirst test) ===
    /// Drinking is NOT an inventory item (unlike berries) — it is a PROXIMITY + INTERACT at a freshwater
    /// pond (drink-from-hand). The interaction lives in <see cref="PondDrink"/> (the pond GameObject Drew
    /// places carries it, mirroring ChopTree's proximity idiom); the RESTORE side is <see cref="AddWater"/>
    /// here. <see cref="TryDrink"/> bridges them WITHOUT coupling this need to the pond geometry: it takes
    /// an "is the castaway in pond reach?" predicate and only restores when the scoop is actually IN reach
    /// — so a scoop FAR from the pond does NOTHING (no thirst change, no Changed). No tool, no inventory
    /// entry is ever created or consumed (thirst is NOT berries).
    ///
    /// AC2b/AC3 GRACEFUL NO-THIRSTNEED: the pond drink-action guards "if a ThirstNeed is present -&gt;
    /// AddWater; else just no-op" (<see cref="PondDrink"/> resolves the need via the serialized ref / a
    /// scene search and no-ops the restore if none is wired) so drinking before this need exists no-ops
    /// the restore (no null-ref) — mirrors hunger's AC2b graceful-degrade.
    /// </summary>
    public class ThirstNeed : SurvivalNeed
    {
        [Header("Water (drink-from-hand scoop restore)")]
        [Tooltip(
            "Thirst restored per hand-scoop at the pond (vision: 'satisfying small amount of thirst with " +
            "each scoop'). Small vs max (default 12 of 100 — smaller than a berry's 18, since a hand-scoop " +
            "holds less than a cup; 'later player can craft a cup' is OOS) so a scoop is a sip, not a guzzle. " +
            "Repeatable. Tweakable via the settings panel.")]
        public float waterScoopAmount = 12f;

        // Thirst's FASTER-than-hunger decay defaults (it gets pressing "after eating the berries"): the
        // per-tier rates sit between hunger's (0.35 medium) and warmth's (0.55 medium). These are the
        // values Reset() / the bootstrap author into the serialized component; the inspector value still
        // wins at runtime once serialized (unity6-mastery §5). Kept as consts so the bootstrap authoring +
        // the EditMode default-guard read one source.
        public const float ThirstEasyDecayPerSecond = 0.25f;
        public const float ThirstMedDecayPerSecond  = 0.45f;  // the default tier; hunger 0.35 < thirst < warmth 0.55
        public const float ThirstHardDecayPerSecond = 0.75f;

        /// <summary>Editor-time defaults when the component is first added — thirst decays FASTER than
        /// hunger (pressing after eating) but SLOWER than warmth. Runtime still honors a serialized
        /// inspector/scene override (unity6-mastery §5); the bootstrap authors the same values so the
        /// shipped scene carries the right pressure.</summary>
        private void Reset()
        {
            easyDecayPerSecond = ThirstEasyDecayPerSecond;
            medDecayPerSecond  = ThirstMedDecayPerSecond;
            hardDecayPerSecond = ThirstHardDecayPerSecond;
            decayPerSecond     = ThirstMedDecayPerSecond; // start on the medium tier
        }

        /// <summary>
        /// The water satisfaction hook the drink-action calls — adds thirst (clamped to max), returns the
        /// new Current01. This is THE seam the pond drink-action (<see cref="PondDrink"/>) wires to.
        /// Default amount is <see cref="waterScoopAmount"/> when called with no argument; an explicit
        /// amount overrides. (Mirrors HungerNeed.AddFood / WarmthNeed.AddWarmth.)
        /// </summary>
        public float AddWater(float amount) => Satisfy(amount);

        /// <summary>Drink one hand-scoop's worth: restore the configured <see cref="waterScoopAmount"/>.
        /// Convenience over AddWater(waterScoopAmount); returns the new Current01.</summary>
        public float AddWater() => Satisfy(waterScoopAmount);

        /// <summary>
        /// The drink-from-hand seam (AC3a — owned here). Restores <see cref="waterScoopAmount"/> thirst
        /// ONLY if <paramref name="inPondReach"/> is true (the castaway is at the pond). Returns true iff
        /// a scoop happened. A scoop FAR from the pond (predicate false) does NOTHING — no thirst change,
        /// no Changed — so a half-wired interaction (drinking from anywhere) cannot ship. No inventory is
        /// touched (thirst is NOT berries — there is no item to consume). Repeatable: each in-reach call
        /// is another scoop. Mirrors HungerNeed.TryEatBerry's all-or-nothing shape, but the gate here is
        /// PROXIMITY rather than an inventory consume.
        /// </summary>
        public bool TryDrink(bool inPondReach)
        {
            if (!inPondReach) return false; // not at the pond -> no scoop, restore nothing
            AddWater();
            return true;
        }
    }
}
