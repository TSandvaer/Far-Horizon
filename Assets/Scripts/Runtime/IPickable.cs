using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The WORLD-ITEM side of the shared E-LOOT surface (ticket 86caf7a6q — the E-loot FOUNDATION).
    /// ANY world item the player can LOOT by pressing E implements this: a berry bush, a fallen stick
    /// (86caa96rd), a loose stone (86caa4c96), and — pending the Sponsor's water-acquisition answer —
    /// a water source. The player-side <see cref="PickableLooter"/> finds every IPickable in the scene,
    /// resolves the NEAREST in-range loot-able one, and calls <see cref="TryLoot"/> on it when E is pressed.
    ///
    /// === Pinned vocabulary (parallel-shared-concept — Devon is the type-author, Pattern A) ===
    /// These names are PINNED so the downstream pickable tickets consume the SAME identifiers (a divergent
    /// name is mergeability-blocking, not a NIT — ticket CONSTRAINTS). Every world pickable references this
    /// interface; the looter references it; nobody re-invents a per-item pickup interface:
    ///   • interface  <see cref="IPickable"/>            — this world-item-side surface (THE export site).
    ///   • component  <see cref="PickableLooter"/>       — the player-side E-loot interactor.
    ///   • method     <see cref="TryLoot"/>(Inventory)   — loot ONE; true iff exactly one landed.
    ///   • query      <see cref="CanLoot"/>              — is this item currently loot-able? (ripe / present)
    ///   • query      <see cref="LootPosition"/>         — world position for the nearest-in-range resolve.
    ///   • query      <see cref="LootRange"/>            — this item's own loot reach (per-item radius).
    ///   • query      <see cref="DisplayName"/>          — the GENERIC item-name the loot prompt shows.
    ///
    /// === Why a per-item INTERFACE, not a manager instance-list (vs ChopTree's _instances) ===
    /// ChopTree is a SINGLE manager iterating a registered <c>_instances</c> list (one component owns every
    /// tree). The pickable FAMILY is heterogeneous — bushes, sticks, stones, water — authored by different
    /// tickets across different builders; a shared interface lets each item type be an ordinary scene
    /// component that the looter discovers, with NO central registry every new pickable must wire into. The
    /// looter's nearest-in-range resolve (<see cref="PickableLooter.ResolveNearestPickable"/>) mirrors
    /// ChopTree's <c>ResolveNearestChoppable</c> shape — same planar-XZ, nearest-wins rule (AC3).
    ///
    /// === The loot CONTRACT (AC1/AC4) ===
    /// <see cref="TryLoot"/> is the WHOLE loot transaction for one item: add the canonical
    /// <see cref="ItemCatalog"/> resource (berry id / WoodId / StoneId — NEVER a parallel id) to the
    /// inventory AND consume/deplete the world instance per that item's own rules (sticks/stones consumed;
    /// a bush's berries deplete + regrow). It returns true ONLY when exactly one loot actually landed, so a
    /// full-pack or not-loot-able item is a clean no-op the looter can move past. The looter NEVER touches
    /// the inventory directly — each item owns its own id + consume rule (no item desyncs the HUD counts).
    /// </summary>
    public interface IPickable
    {
        /// <summary>True when this item can currently be looted (e.g. a ripe bush; a present stick/stone).
        /// A bare/regrowing bush, an already-consumed stick, or a decorative non-pickable returns false —
        /// the looter skips it in the nearest-in-range resolve so E never "loots nothing" off a spent item.</summary>
        bool CanLoot { get; }

        /// <summary>The item's world position — the looter measures planar (XZ) distance to the player
        /// against this for the nearest-in-range resolution (AC3). Height is ignored (height-robust, same
        /// idiom as ChopTree / BerryBush / CraftSpot).</summary>
        Vector3 LootPosition { get; }

        /// <summary>This item's own loot reach (planar XZ radius). Each pickable carries its own radius so a
        /// small stick can require getting close while a big bush is loot-able from a step away — the looter
        /// uses THIS, not one global radius, so per-item tuning lives with the item (AC3).</summary>
        float LootRange { get; }

        /// <summary>
        /// The GENERIC display name the proximity prompt shows — "Press E to {GatherVerb} {DisplayName}" (ticket
        /// 86cafc6ud AC2/AC3). Each pickable returns its OWN human-readable resource word: a berry bush returns
        /// "berries", a stick "wood", a stone "stones" — and the pond returns "water" / the tree-chop
        /// log-pile "wood", slotting into the SAME prompt with ZERO rework (the load-bearing genericity: the
        /// prompt reads the pickable's own name, never a per-item switch in the HUD). Lower-case, plural/mass
        /// noun matching the inventory resource ("berries"/"wood"/"stones"/"water"). The prompt and the actual
        /// loot agree because both come from the looter's ONE nearest-in-range resolve (single source of truth).
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The GATHER VERB the proximity prompt uses for this pickable — "Press E to {GatherVerb} {DisplayName}"
        /// (ticket 86cafc6vx, Uma water-acquisition-spec §2a). DEFAULTS to "pick up" (a default interface member,
        /// so the existing object pickables — berry bush / stick / stone / log-pile — inherit it with NO change:
        /// they keep "Press E to pick up berries/wood/stones"). The freshwater POND overrides it to "collect"
        /// because you don't pick water UP, you GATHER it ("Press E to collect water" — the Sponsor's framing,
        /// 2026-06-28). This is a tiny GENERIC extension on the shared interface (one optional string), NOT a
        /// pond-special-case prompt branch — the single-source-of-truth <see cref="LootPrompt"/> path stays
        /// item-agnostic; only the COPY fits the action. Lower-case (it sits mid-sentence after "Press E to ").
        /// </summary>
        string GatherVerb => "pick up";

        /// <summary>
        /// LOOT exactly ONE from this item into <paramref name="inventory"/> — the whole transaction (AC1):
        /// add the canonical <see cref="ItemCatalog"/> resource AND consume/deplete the world instance per
        /// this item's rules. Returns true IFF exactly one loot actually landed (so a full pack, a
        /// not-loot-able item, or a null inventory is a clean no-op the looter moves past — never a
        /// half-applied loot). The item owns its OWN catalog id + consume rule; the looter never assumes one.
        /// </summary>
        /// <param name="inventory">The inventory to loot into. A null inventory is a no-op (returns false).</param>
        /// <returns>True iff exactly one item was looted into the inventory.</returns>
        bool TryLoot(Inventory inventory);
    }
}
