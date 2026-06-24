using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The THIRST survival need (ticket 86caamkv7). IS-A <see cref="SurvivalNeed"/> — it inherits the whole
    /// decay/floor/satisfy/HUD-contract surface (Pattern A: hunger 86caamkp8 OWNS the shared base
    /// <see cref="SurvivalNeed"/>; thirst EXTENDS the merged type, it does NOT re-declare a base) and adds the
    /// water-specific satisfaction hook <see cref="AddWater(float)"/> plus the drink-a-scoop seam
    /// <see cref="TryDrinkScoop"/>.
    ///
    /// Castaway fiction (vision-far-horizon-game-concept.md): "The young man begins to get thirsty AFTER
    /// EATING THE BERRIES and goes look for a pond of fresh water. He finds a small pond and drinks with his
    /// hand, satisfying SMALL amount of thirst with EACH SCOOP." So thirst decays as a FASTER pressure than
    /// hunger (the fiction makes thirst PRESSING "after the berries" — default medDecayPerSecond 0.45 &gt;
    /// HungerNeed's 0.35, &lt; WarmthNeed's 0.55: thirst nags sooner than hunger but the body still holds water
    /// longer than warmth bleeds away by a fire's absence) and a single hand-scoop restores only a SMALL fixed
    /// amount (the player scoops several times at the pond — "small amount with each scoop").
    ///
    /// === The drink seam (AC3 / AC3a — this ticket OWNS the drink-&gt;thirst test) ===
    /// Drinking is NOT an inventory item (unlike berries — AC3): it is a PROXIMITY + INTERACT at the
    /// freshwater pond (<see cref="FreshwaterPond"/>), each scoop a small restore, repeatable, NO tool, NO
    /// inventory entry created or consumed. The drink interaction (proximity gate + the input call-site) lives
    /// in <see cref="FreshwaterPond"/> + <see cref="DrinkAction"/>; the RESTORE side is <see cref="AddWater"/>
    /// here. <see cref="TryDrinkScoop"/> is the atomic seam: it runs an all-or-nothing "can the castaway drink
    /// here right now?" predicate (the pond passes <c>() =&gt; pond.PlayerInRange</c>) and ONLY restores when
    /// the predicate returns true — so a scoop FAR from the pond restores NOTHING (the proximity gate is
    /// load-bearing; a drink that works anywhere green-passes "thirst rose" but breaks the fiction).
    ///
    /// AC3b GRACEFUL NO-THIRSTNEED: the pond drink-action guards "if a ThirstNeed is present -&gt; AddWater;
    /// else just no-op" so drinking before this need is wired never null-refs (mirrors hunger's AC2b).
    /// </summary>
    public class ThirstNeed : SurvivalNeed
    {
        [Header("Water (drink-a-scoop restore)")]
        [Tooltip(
            "Thirst restored per hand-scoop drunk at the pond (vision: 'small amount of thirst with each " +
            "scoop'). Small vs max (default 14 of 100) so a single scoop is a sip, not a full drink — the " +
            "castaway scoops several times. Tweakable via the settings panel ('water scoop amount').")]
        public float waterScoopAmount = 14f;

        // Thirst's FASTER-than-hunger decay defaults (the fiction: "thirsty AFTER eating the berries" — it
        // becomes pressing sooner than hunger): the per-tier rates sit ABOVE HungerNeed's (0.18/0.35/0.60) but
        // BELOW WarmthNeed's 0.55-med. These are the values the bootstrap authors into the serialized
        // component; the inspector value still wins at runtime once serialized (unity6-mastery §5). Kept as
        // consts so the bootstrap authoring + the EditMode default-guard read ONE source.
        public const float ThirstEasyDecayPerSecond = 0.26f;
        public const float ThirstMedDecayPerSecond  = 0.45f;  // the default tier; > hunger's 0.35, < warmth's 0.55
        public const float ThirstHardDecayPerSecond = 0.75f;

        // EAT-REFILL-PARITY: thirst ships PRESSURED-WITH-HEADROOM (not startFull) so a scoop VISIBLY raises the
        // bar (the #101 hunger fix, applied to thirst: a scoop against an already-full bar shows no change AND
        // SetCurrent's Approximately early-return means Changed never fires). 0.50 -> ~5 of 10 segments at
        // spawn; a scoop (14 of 100 = ~1.4 segments) climbs unmistakably. Above the floor (0.05), below full so
        // there's room both to decay into AND to refill into. The bootstrap authors this onto the serialized
        // component (Reset() doesn't run on a headless AddComponent); a const so bootstrap + the default-guard
        // read ONE source (matches the decay-const pattern above + HungerNeed.HungerStartFraction01).
        public const float ThirstStartFraction01 = 0.50f;

        /// <summary>Editor-time defaults when the component is first added — thirst decays FASTER than hunger
        /// (the "thirsty after the berries" fiction) but slower than warmth. Runtime still honors a serialized
        /// inspector/scene override (unity6-mastery §5); the bootstrap authors the same values so the shipped
        /// scene carries the faster pressure.</summary>
        private void Reset()
        {
            easyDecayPerSecond = ThirstEasyDecayPerSecond;
            medDecayPerSecond  = ThirstMedDecayPerSecond;
            hardDecayPerSecond = ThirstHardDecayPerSecond;
            decayPerSecond     = ThirstMedDecayPerSecond; // start on the medium tier
            // ship pressured-with-headroom so a scoop VISIBLY refills (see ThirstStartFraction01).
            startFull       = false;
            startFraction01 = ThirstStartFraction01;
        }

        /// <summary>
        /// The water satisfaction hook the drink-action calls — adds thirst (clamped to max), returns the new
        /// Current01. This is THE seam the pond drink interaction (<see cref="FreshwaterPond"/>) wires to.
        /// Default amount is <see cref="waterScoopAmount"/> via the no-arg overload; an explicit amount overrides.
        /// </summary>
        public float AddWater(float amount) => Satisfy(amount);

        /// <summary>Drink one hand-scoop's worth: restore the configured <see cref="waterScoopAmount"/>.
        /// Convenience over AddWater(waterScoopAmount); returns the new Current01.</summary>
        public float AddWater() => Satisfy(waterScoopAmount);

        /// <summary>
        /// The ATOMIC drink-a-scoop seam (AC3a — owned here). Runs the all-or-nothing
        /// <paramref name="canDrinkHere"/> predicate (the proximity gate — the pond passes
        /// <c>() =&gt; pond.PlayerInRange</c>); ONLY if it returns true does it restore
        /// <see cref="waterScoopAmount"/> thirst. Returns true iff a scoop was drunk (in range AND restored).
        /// When the predicate returns false (NOT at the pond) NOTHING happens — no thirst change, no Changed,
        /// no inventory touched (thirst is NOT berries — there is no inventory in this path at all). This makes
        /// the proximity gate inseparable from the restore: a scoop far from the pond can never restore thirst.
        /// </summary>
        public bool TryDrinkScoop(System.Func<bool> canDrinkHere)
        {
            if (canDrinkHere == null) return false;
            if (!canDrinkHere()) return false; // not at the pond -> no restore (proximity is load-bearing)
            AddWater();
            return true;
        }
    }
}
