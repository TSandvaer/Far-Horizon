using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86caffwv5 round-7 EditMode guards for the two NEW behaviours (the seat re-bake is guarded in
    /// HeroAxeSceneTests):
    ///   • TASK 2 — the movement CARVE (MineableNodeState): a mineable adds a carving NavMeshObstacle (the SAME
    ///     mechanism trees use to block the NavMeshAgent player — a physics collider would NOT stop a NavMeshAgent),
    ///     sized to its footprint so the player is blocked at the surface yet can still stand within mine range; the
    ///     carve toggles OFF when the node is broken (no invisible wall at a mined-away spot).
    ///   • TASK 3 — the above-head INTERACTION PROMPT priority table (LootPrompt.ResolveInteractionPrompt): the
    ///     pure priority "Mine stone" > "Mine iron" > "Needs stone pickaxe" > "Chop" > loot, asserted headlessly.
    /// </summary>
    public class SwingsRound7Tests
    {
        // ---- TASK 3: interaction-prompt priority (pure, no scene/OnGUI rig) ----

        [Test]
        public void Prompt_BoulderMine_WinsOverEverything()
        {
            Assert.AreEqual("Mine stone",
                LootPrompt.ResolveInteractionPrompt(true, true, true, true, "Press E to pick up wood"),
                "a boulder-mine-ready state shows 'Mine stone' ahead of ore/chop/loot (the player is holding a pickaxe at a boulder)");
        }

        [Test]
        public void Prompt_OreMine_TierAware_MineIron()
        {
            Assert.AreEqual("Mine iron",
                LootPrompt.ResolveInteractionPrompt(false, true, false, true, "Press E to pick up wood"),
                "ore in range with a stone/iron pickaxe → 'Mine iron' (ahead of chop/loot)");
        }

        [Test]
        public void Prompt_OreRefusal_WoodPickaxe_NeedsStonePickaxe()
        {
            Assert.AreEqual("Needs stone pickaxe",
                LootPrompt.ResolveInteractionPrompt(false, false, true, true, "Press E to pick up wood"),
                "ore in range with a WOOD pickaxe → the refusal cue 'Needs stone pickaxe' (the Sponsor-approved cue)");
        }

        [Test]
        public void Prompt_Chop_WhenNoMinePrompt()
        {
            Assert.AreEqual("Chop",
                LootPrompt.ResolveInteractionPrompt(false, false, false, true, "Press E to pick up berries"),
                "a tree in range with an axe → 'Chop' (falls out of the same seam; ahead of loot)");
        }

        [Test]
        public void Prompt_Loot_WhenNoVerbReady()
        {
            Assert.AreEqual("Press E to pick up berries",
                LootPrompt.ResolveInteractionPrompt(false, false, false, false, "Press E to pick up berries"),
                "with no tool verb ready the existing loot prompt shows (unchanged copy)");
        }

        [Test]
        public void Prompt_Hidden_WhenNothingActionable()
        {
            Assert.AreEqual("",
                LootPrompt.ResolveInteractionPrompt(false, false, false, false, ""),
                "nothing in range → empty label → the prompt is HIDDEN");
            Assert.AreEqual("",
                LootPrompt.ResolveInteractionPrompt(false, false, false, false, null),
                "a null loot label is treated as hidden (defensive)");
        }

        // ---- TASK 2: the movement carve (MineableNodeState) ----

        // Build a mineable-node visual with a real renderer of the given world half-extent, so the carve's
        // footprint-measurement has valid bounds (the constructor measures the combined renderer bounds).
        private static Transform MakeMineableVisual(string name, float halfExtent)
        {
            var node = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube); // 1x1x1 → extent 0.5
            var col = mesh.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col); // the world is deliberately collider-free
            mesh.transform.SetParent(node.transform, false);
            mesh.transform.localScale = Vector3.one * (halfExtent * 2f); // extent = halfExtent
            return node.transform;
        }

        // 86caffwv5 round-8 — a NON-square footprint (different X/Z half-extents) so the guard can prove the carve
        // hugs the SNUG (min) planar extent, not the circumscribing (max) spike that produced the Sponsor's gap.
        private static Transform MakeMineableVisualXZ(string name, float halfX, float halfZ)
        {
            var node = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = mesh.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            mesh.transform.SetParent(node.transform, false);
            mesh.transform.localScale = new Vector3(halfX * 2f, halfZ * 2f, halfZ * 2f); // extents = (halfX, halfZ, halfZ) → wide X, narrow Z
            return node.transform;
        }

        [Test]
        public void Carve_StandingNode_BlocksMovement_WithACarvingObstacle()
        {
            Transform v = MakeMineableVisual("Boulder", 1.2f); // a boulder-sized footprint
            var state = new MineableNodeState(v, 123);

            Assert.IsTrue(state.HasMovementCarve,
                "a mineable with a renderer must carry a movement carve (the NavMeshObstacle that blocks the NavMeshAgent player — a collider would not)");
            Assert.IsTrue(state.MovementCarveActive,
                "a STANDING mineable must be actively carving (enabled + carving) so the player can't walk inside it");
            Assert.Greater(state.MovementCarveRadius, 0.1f,
                "the carve radius must be sized to the footprint (measured from the renderer bounds), not zero");

            Object.DestroyImmediate(v.gameObject);
        }

        [Test]
        public void Carve_Radius_LeavesMineRangeReachable()
        {
            // The Sponsor's constraint: with a carve the player stands at the SURFACE (carve + agent radius), not
            // the centre — the 2.4u boulder mine range must still pass from touching distance. Player agent radius
            // is 0.4u (MovementCameraScene). 86caffwv5 round-8: a 1.2u footprint → carve 0.95u → player stands
            // ~1.35u from centre (< the round-7 1.71u, well under 2.4u — tightening only helps the mine reach).
            Transform v = MakeMineableVisual("Boulder", 1.2f);
            var state = new MineableNodeState(v, 7);
            const float agentRadius = 0.4f;
            const float boulderMineRadius = 2.4f;
            Assert.Less(state.MovementCarveRadius + agentRadius, boulderMineRadius,
                "carve radius + agent radius must stay under the 2.4u boulder mine range so mining still passes from touching distance");
            Object.DestroyImmediate(v.gameObject);
        }

        // 86caffwv5 round-8 (TASK 2 FIX — the Sponsor's soak-7 "blocked a body-length from the boulder's visual
        // edge; invisible-wall feel"). The carve-size-tracks-bounds invariant: the carve is now sized so the
        // player's STAND position (carve + agentStandoff) lands at the visual surface + a small clearance, NOT at
        // the circumscribing facet spike the round-7 Mathf.Max footprint used.

        [Test]
        public void Carve_IsTighterThanBounds_HugsFootprintNotSpike()
        {
            Transform v = MakeMineableVisual("Boulder", 1.2f);
            var state = new MineableNodeState(v, 5);

            // The measured footprint == the bounds half-extent (1.2 for this cube); the carve is TIGHTER than it
            // (footprint + clearance − agentStandoff), the whole point of the round-8 fix — the round-7 carve == the
            // footprint (a body-length gap once the agent-radius standoff is added on top).
            Assert.AreEqual(1.2f, state.MovementCarveFootprint, 1e-4f, "footprint == the cube's bounds half-extent");
            Assert.Less(state.MovementCarveRadius, state.MovementCarveFootprint,
                "the round-8 carve must be TIGHTER than the raw bounds footprint (so the player stands at the surface, not a body-length out)");
            Assert.AreEqual(MineableNodeState.MovementCarveWorldRadius(1.2f), state.MovementCarveRadius, 1e-4f,
                "the carve radius must equal the pure MovementCarveWorldRadius(footprint) formula");
            Object.DestroyImmediate(v.gameObject);
        }

        [Test]
        public void Carve_StandPosition_LandsAtVisualEdge_NotABodyLengthOut()
        {
            // blocked-from-centre = carve + agentStandoff. The fix pins this at footprint + clearance = the visual
            // surface + a hair — so the player touches the boulder, not stops a body-length short.
            Transform v = MakeMineableVisual("Boulder", 1.2f);
            var state = new MineableNodeState(v, 11);
            float blocked = state.MovementCarveRadius + MineableNodeState.CarveAgentStandoff;
            float expectedAtSurface = state.MovementCarveFootprint + MineableNodeState.CarveClearance; // 1.35
            Assert.AreEqual(expectedAtSurface, blocked, 1e-4f,
                "player stand distance (carve + agentStandoff) must land at the visual edge + clearance, not beyond it");
            // And it must be MUCH closer than the round-7 circumscribe-based block (footprint + agentStandoff = 1.6
            // for this cube; a lumpy boulder's spike-based block was ~1.71u in the soak-7 capture).
            float round7Block = state.MovementCarveMaxExtent + MineableNodeState.CarveAgentStandoff;
            Assert.Less(blocked, round7Block,
                "the round-8 block must be closer to the boulder than the round-7 circumscribe-based block");
            Object.DestroyImmediate(v.gameObject);
        }

        [Test]
        public void Carve_HugsNarrowAxis_UsesMinNotMaxExtent()
        {
            // A wide-X / narrow-Z footprint: the carve must be sized from the NARROW (min) extent so it hugs the
            // narrow silhouette (a circle can't hug tighter without a gap on that axis). Round-7's Mathf.Max would
            // have blocked at the WIDE spike on a narrow approach — exactly the Sponsor's gap.
            Transform v = MakeMineableVisualXZ("Boulder", 1.4f, 0.9f); // extents (1.4, .., 0.9) → min 0.9, max 1.4
            var state = new MineableNodeState(v, 13);
            Assert.AreEqual(0.9f, state.MovementCarveFootprint, 1e-4f, "footprint == the NARROW (min) XZ half-extent");
            Assert.AreEqual(1.4f, state.MovementCarveMaxExtent, 1e-4f, "maxExtent == the WIDE (max) XZ half-extent");
            Assert.AreEqual(MineableNodeState.MovementCarveWorldRadius(0.9f), state.MovementCarveRadius, 1e-4f,
                "the carve is sized from the NARROW extent, not the wide spike");
            Object.DestroyImmediate(v.gameObject);
        }

        [Test]
        public void Carve_Radius_GrowsWithFootprint_TracksBounds()
        {
            Transform small = MakeMineableVisual("SmallBoulder", 0.8f);
            Transform big = MakeMineableVisual("BigBoulder", 1.5f);
            var sState = new MineableNodeState(small, 21);
            var bState = new MineableNodeState(big, 22);
            Assert.Greater(bState.MovementCarveRadius, sState.MovementCarveRadius,
                "a bigger boulder (bigger footprint) must get a bigger carve — the carve tracks the bounds");
            Assert.GreaterOrEqual(sState.MovementCarveRadius, MineableNodeState.CarveFloorRadius,
                "even a small node's carve is floored so it still cuts the navmesh (never a free walk-through)");
            Object.DestroyImmediate(small.gameObject);
            Object.DestroyImmediate(big.gameObject);
        }

        [Test]
        public void Carve_BrokenNode_StopsBlocking_NoInvisibleWall()
        {
            Transform v = MakeMineableVisual("Boulder", 1.2f);
            var state = new MineableNodeState(v, 99);
            Assert.IsTrue(state.MovementCarveActive, "precondition: standing node is carving");

            bool broke = state.LandStrike(1, 999f, 999f); // strikesToBreak=1 → breaks on the first strike
            Assert.IsTrue(broke, "precondition: the node broke");
            state.Tick(); // the carve lock-step runs at the top of Tick

            Assert.IsFalse(state.MovementCarveActive,
                "a broken / mined-away node must STOP carving so the player isn't blocked by an invisible wall at the empty spot");
            Object.DestroyImmediate(v.gameObject);
        }

        [Test]
        public void Carve_BareRig_NoRenderer_NoCarve_NoCrash()
        {
            var node = new GameObject("Boulder"); // no renderer child
            var state = new MineableNodeState(node.transform, 1);
            Assert.IsFalse(state.HasMovementCarve,
                "a bare rig with no renderer gets no carve (null-safe — can't measure a footprint)");
            Object.DestroyImmediate(node);
        }
    }
}
