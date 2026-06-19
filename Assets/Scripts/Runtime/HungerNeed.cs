using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The HUNGER survival need (ticket 86caamkp8). IS-A <see cref="SurvivalNeed"/> — it inherits the
    /// whole decay/floor/satisfy/HUD-contract surface and adds the food-specific satisfaction hook
    /// <see cref="AddFood"/> plus the eat-a-berry seam <see cref="TryEatBerry"/>.
    ///
    /// Castaway fiction (vision-far-horizon-game-concept.md): "he starts to get hungry. He searches
    /// for bushes with berries and harvest some to give SMALL satisfaction to his hunger." So hunger
    /// decays as a SLOWER background pressure than warmth (food is found, not constantly lost — default
    /// medDecayPerSecond 0.35 &lt; WarmthNeed's 0.55) and a berry restores only a SMALL fixed amount.
    ///
    /// === The eat seam (AC2 / AC2a — this ticket OWNS the atomic eat-&gt;hunger test) ===
    /// Eating a berry is ATOMIC: consume exactly one berry from inventory AND restore exactly the
    /// per-berry amount, all-or-nothing. The CONSUME side lives in the inventory/bushes lane
    /// (86caa5zz3 / 86caa4bya — InventoryModel.TryConsumeSelected / RemoveItem("berry")); the RESTORE
    /// side is <see cref="AddFood"/> here. <see cref="TryEatBerry"/> bridges them WITHOUT coupling this
    /// ticket to the not-yet-merged generic-item Inventory: it takes an all-or-nothing consume delegate
    /// (the bushes eat-action passes <c>() =&gt; inventory.Model.TryConsumeSelected()</c> /
    /// <c>RemoveItem(BerryId, 1)</c>) and only restores when the consume actually succeeded — so a
    /// half-wired seam (berry consumed with no restore, OR restore with no consume) cannot ship.
    ///
    /// AC2b GRACEFUL NO-HUNGERNEED: the bushes eat-action guards "if a HungerNeed is present -&gt;
    /// AddFood; else just consume" so eating before this need is wired no-ops the restore (no null-ref).
    /// </summary>
    public class HungerNeed : SurvivalNeed
    {
        [Header("Food (eat-a-berry restore)")]
        [Tooltip(
            "Hunger restored per berry eaten (vision: 'small satisfaction to his hunger'). Small vs max " +
            "(default 18 of 100) so a berry is a top-up, not a full meal. Tweakable via the settings panel.")]
        public float berryRestoreAmount = 18f;

        // Hunger's SLOWER-than-warmth decay defaults (food is found, not constantly lost): the per-tier
        // rates sit below WarmthNeed's 0.55. These are the values Reset() / the bootstrap author into the
        // serialized component; the inspector value still wins at runtime once serialized (unity6-mastery
        // §5). Kept as consts so the bootstrap authoring + the EditMode default-guard read one source.
        public const float HungerEasyDecayPerSecond = 0.18f;
        public const float HungerMedDecayPerSecond  = 0.35f;  // the default tier; < warmth's 0.55
        public const float HungerHardDecayPerSecond = 0.60f;

        /// <summary>Editor-time defaults when the component is first added — hunger decays SLOWER than
        /// warmth. Runtime still honors a serialized inspector/scene override (unity6-mastery §5); the
        /// bootstrap authors the same values so the shipped scene carries the slower pressure.</summary>
        private void Reset()
        {
            easyDecayPerSecond = HungerEasyDecayPerSecond;
            medDecayPerSecond  = HungerMedDecayPerSecond;
            hardDecayPerSecond = HungerHardDecayPerSecond;
            decayPerSecond     = HungerMedDecayPerSecond; // start on the medium tier
        }

        /// <summary>
        /// The food satisfaction hook the eat-action calls — adds hunger (clamped to max), returns the
        /// new Current01. This is THE seam the berry eat-action (86caa5zz3) wires to. Default amount is
        /// <see cref="berryRestoreAmount"/> when called with no argument; an explicit amount overrides.
        /// </summary>
        public float AddFood(float amount) => Satisfy(amount);

        /// <summary>Eat one berry's worth: restore the configured <see cref="berryRestoreAmount"/>.
        /// Convenience over AddFood(berryRestoreAmount); returns the new Current01.</summary>
        public float AddFood() => Satisfy(berryRestoreAmount);

        /// <summary>
        /// The ATOMIC eat-a-berry seam (AC2a — owned here). Runs the all-or-nothing
        /// <paramref name="consumeOneBerry"/> delegate (the inventory consume side); ONLY if it returns
        /// true does it restore <see cref="berryRestoreAmount"/> hunger. Returns true iff a berry was
        /// eaten (consumed AND restored). When the delegate returns false (no berry held) NOTHING
        /// happens — no hunger change, no Changed, and the inventory is untouched (the delegate already
        /// debited nothing). This guarantees consume and restore are inseparable.
        /// </summary>
        public bool TryEatBerry(Func<bool> consumeOneBerry)
        {
            if (consumeOneBerry == null) return false;
            if (!consumeOneBerry()) return false; // no berry -> all-or-nothing, restore nothing
            AddFood();
            return true;
        }
    }
}
