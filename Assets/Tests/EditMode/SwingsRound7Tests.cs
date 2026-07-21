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
            // is 0.4u (MovementCameraScene). A boulder footprint ~1.2u → player stands ~1.6u from centre < 2.4u.
            Transform v = MakeMineableVisual("Boulder", 1.2f);
            var state = new MineableNodeState(v, 7);
            const float agentRadius = 0.4f;
            const float boulderMineRadius = 2.4f;
            Assert.Less(state.MovementCarveRadius + agentRadius, boulderMineRadius,
                "carve radius + agent radius must stay under the 2.4u boulder mine range so mining still passes from touching distance");
            Object.DestroyImmediate(v.gameObject);
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
