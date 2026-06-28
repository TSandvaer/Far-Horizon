using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the TIGHTENED loot ranges + the generic DisplayName (ticket 86cafc6ud AC1/AC3 — the
    /// Sponsor's #155 soak: "I can loot lootables from too far"). Asserts the per-item <see cref="IPickable.LootRange"/>
    /// is now ARM'S-REACH (out of the OLD generous range no longer loots; close still loots) and that each
    /// pickable returns its GENERIC DisplayName the prompt shows. These run on bare AddComponent'd props (no
    /// scene/bootstrap) so they pin the DATA-side tightening + the resolve behaviour directly — the bug class
    /// is "the radius is too big", caught here at the source.
    ///
    /// Range is PER-ITEM and SCALED by localScale.x (LootRange = radius * scale). The looter's resolve uses
    /// dSq &lt;= range*range, so a pickable at distance d is in range iff d &lt;= radius*scale. The OLD radii
    /// (bush 2.0, stick 1.6, stone 1.6) let E loot from ~1.6–2u; the NEW radii (bush 1.2, stick/stone 1.0)
    /// require arm's-reach. The tests assert the NEW range admits a close target and EXCLUDES a target that the
    /// OLD range would have admitted.
    /// </summary>
    public class LootRangeTightenTests
    {
        // The OLD generous radii (pre-86cafc6ud) — a target at this distance must now be OUT of range.
        private const float OldBushRadius = 2.0f;
        private const float OldStickRadius = 1.6f;
        private const float OldStoneRadius = 1.6f;

        private static GameObject Make<T>(out T comp) where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            comp = go.AddComponent<T>();
            return go;
        }

        // --- BERRY BUSH ---------------------------------------------------------------------------

        [Test]
        public void BerryBush_LootRange_TightenedToArmsReach()
        {
            var go = Make<BerryBush>(out var bush);
            try
            {
                // At scale 1, the new LootRange is the new radius. It must be SMALLER than the old 2.0 (the fix),
                // and still a believable step-away (> a hand's width).
                Assert.Less(bush.LootRange, OldBushRadius,
                    "the bush loot range is TIGHTER than the old generous 2.0 (the #155 'loot from too far' fix)");
                Assert.Greater(bush.LootRange, 0.5f,
                    "but still a believable step-away (not unusably tiny)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void BerryBush_DisplayName_IsBerries()
        {
            var go = Make<BerryBush>(out var bush);
            try { Assert.AreEqual("berries", ((IPickable)bush).DisplayName); }
            finally { Object.DestroyImmediate(go); }
        }

        // --- STICK --------------------------------------------------------------------------------

        [Test]
        public void Stick_LootRange_TightenedToArmsReach()
        {
            var go = Make<StickProp>(out var stick);
            try
            {
                Assert.Less(stick.LootRange, OldStickRadius,
                    "the stick loot range is TIGHTER than the old 1.6 — you stoop close to pick a stick up");
                Assert.Greater(stick.LootRange, 0.5f, "but still reachable arm's-reach");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Stick_DisplayName_IsWood()
        {
            var go = Make<StickProp>(out var stick);
            try { Assert.AreEqual("wood", ((IPickable)stick).DisplayName); }
            finally { Object.DestroyImmediate(go); }
        }

        // --- STONE --------------------------------------------------------------------------------

        [Test]
        public void Stone_LootRange_TightenedToArmsReach()
        {
            var go = Make<StoneProp>(out var stone);
            try
            {
                Assert.Less(stone.LootRange, OldStoneRadius,
                    "the stone loot range is TIGHTER than the old 1.6 — you stoop close to pick a pebble up");
                Assert.Greater(stone.LootRange, 0.5f, "but still reachable arm's-reach");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Stone_DisplayName_IsStones()
        {
            var go = Make<StoneProp>(out var stone);
            try { Assert.AreEqual("stones", ((IPickable)stone).DisplayName); }
            finally { Object.DestroyImmediate(go); }
        }

        // --- The RESOLVE end-to-end: out-of-OLD-range no longer loots; close still does -----------

        [Test]
        public void Looter_DoesNotResolve_StickAtOldRange_ButResolvesClose()
        {
            // A stick placed at the OLD radius (1.6u) away from the player. With the tightened range it is now
            // OUT of reach (the resolve returns null); a stick at arm's-reach (0.4u) is still resolved. This is
            // the exact "walking near-but-not-adjacent -> no loot; close -> loot" AC1 behaviour, through the
            // looter's REAL ResolveNearestPickable (the single source of truth the prompt + E press both use).
            var playerGo = new GameObject("Player");
            var invGo = new GameObject("Inventory");
            var inv = invGo.AddComponent<Inventory>();
            var looter = playerGo.AddComponent<PickableLooter>();
            looter.player = playerGo.transform;
            looter.inventory = inv;

            var farGo = Make<StickProp>(out var farStick);
            farStick.inventory = inv;
            farGo.transform.position = new Vector3(OldStickRadius, 0f, 0f); // 1.6u — in the OLD range, out of the NEW

            looter.DiscoverPickables();
            Assert.IsNull(looter.ResolveNearestPickable(playerGo.transform.position),
                "a stick at the OLD 1.6u range is NO LONGER in reach after the tighten (the #155 fix)");

            // Move it to arm's-reach -> the resolve finds it again (the looter still loots when close).
            farGo.transform.position = new Vector3(0.4f, 0f, 0f);
            Assert.AreSame(farStick, looter.ResolveNearestPickable(playerGo.transform.position),
                "a stick at arm's-reach IS resolved -> close + E still loots (AC1 'close -> loot' preserved)");

            Object.DestroyImmediate(farGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(invGo);
        }
    }
}
