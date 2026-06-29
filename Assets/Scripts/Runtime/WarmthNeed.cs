using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The WARMTH survival need (ticket 86ca8bd9m, Sponsor-locked). IS-A <see cref="SurvivalNeed"/> —
    /// it inherits the whole decay/floor/satisfy/HUD-contract surface (the shared base owned by
    /// 86caamkp8; hunger + thirst extend the same type) and adds only the warmth-specific satisfaction
    /// hook <see cref="AddWarmth"/>. Refactored onto the base by 86cabgvgw so all three needs share ONE
    /// code path by inheritance, not by a hand-maintained copy of the surface — a PURE structural
    /// refactor: the public surface, decay feel, floor, critical readout, Changed event and tuned
    /// defaults are byte-for-byte the ones warmth shipped with (max 100, startFull, 0.55/sec decay).
    ///
    /// Castaway-washed-ashore fiction: wet and cold, warmth ticks DOWN over time and creates the
    /// *why* of the loop — the campfire (U2-4) is what answers it via the satisfaction hook
    /// (<see cref="AddWarmth"/> / the base's SatisfyFull). This is the loop SPINE: U2-2 (axe) and the
    /// rest of M-U2 build on this need existing and reading clearly.
    ///
    /// Warmth decays as the FASTEST of the three needs (default medDecayPerSecond 0.55, tuned so a FULL
    /// bar drains in ~max/decayPerSecond = 100/0.55 ~= 182s ~= 3 min — comfortably longer than one
    /// craft->chop->campfire loop cycle, so the need is felt as pressure without punishing a normal-paced
    /// loop, per ticket 86ca8bd9m: 'one loop cycle fits comfortably inside the decay window'). Below the
    /// base's floor01 it stops decaying (a simple floor, NOT a fail-state — death/fail is out of scope).
    ///
    /// === Public data surface — inherited UNCHANGED from <see cref="SurvivalNeed"/> ===
    /// Current01 / Current / Max / IsCritical / Changed: the HUD CONTRACT (the U2-5 SurvivalHud and the
    /// need-meter HUD 86caamkxv read EXACTLY this, byte-identical across all three needs). The HUD treats
    /// this component as READ-ONLY (subscribe to Changed, read Current01); only the campfire (U2-4) writes,
    /// via <see cref="AddWarmth"/> / the base SatisfyFull. The decay-over-Time.time-window model, the
    /// deterministic EditMode-driveable TickSeconds, and the difficulty tiers all live on the base.
    /// </summary>
    public class WarmthNeed : SurvivalNeed
    {
        // Warmth keeps the base's default tuning (max 100, startFull=true, decayPerSecond 0.55, floor 0.05,
        // critical 0.25) — those defaults already ARE warmth's Sponsor-accepted values, so no Reset()/field
        // override is needed (unlike hunger/thirst, which decay slower and ship pressured-with-headroom).
        // The campfire restores warmth in full, so startFull=true is correct (no #101 eat-refill-headroom
        // concern: a campfire restore is always visible because the bar has decayed by the time it's lit).

        /// <summary>
        /// Satisfaction hook the campfire (U2-4) calls. Adds warmth (clamped to max), returns the new
        /// Current01. This is THE seam U2-4 wires the campfire to — routes through the base's protected
        /// Satisfy primitive so Changed fires consistently. Keep the signature stable.
        /// </summary>
        public float AddWarmth(float amount) => Satisfy(amount);

        /// <summary>Restore warmth fully (a campfire that fully warms the castaway). Returns the new
        /// Current01 (== 1). PUBLIC on WarmthNeed — part of warmth's Sponsor-accepted surface the
        /// regression-guard tests assert; re-exposes the base's protected SatisfyFull so the public
        /// surface is preserved byte-identically through the refactor (hunger/thirst do not surface it).</summary>
        public new float SatisfyFull() => base.SatisfyFull();
    }
}
