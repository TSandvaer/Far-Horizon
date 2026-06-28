using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode truth-table guard for the LOOT PROXIMITY PROMPT text (ticket 86cafc6ud AC2/AC3). The prompt's
    /// show/hide + the GENERIC item-naming are the pure static <see cref="LootPrompt.BuildLabel"/>, so the whole
    /// decision is asserted headlessly here with NO scene/OnGUI rig — sibling of
    /// <see cref="PickableLooter.ShouldLootOnKey"/>'s LootKeyGateTests. The live resolve→prompt loop (the prompt
    /// reads the SAME nearest the E press uses) is covered in PlayMode (PickableLooterPlayModeTests
    /// NearestInRange_*).
    ///
    /// BuildLabel(target, lootKey) must be:
    ///   • "" (prompt HIDDEN) when target is null            — nothing in range (AC2);
    ///   • "" (prompt HIDDEN) when the target has no name    — defensive: never a half-built "Press E to pick up ";
    ///   • "Press E to pick up {DisplayName}" otherwise      — the GENERIC name flows straight through (AC3);
    ///   • the key rendered as the LITERAL letter (E)        — layout-agnostic on the Danish keyboard (AC3).
    /// </summary>
    public class LootPromptTests
    {
        // A minimal IPickable test double — only DisplayName matters for the prompt-text resolution.
        private class FakePickable : IPickable
        {
            private readonly string _name;
            public FakePickable(string name) { _name = name; }
            public bool CanLoot => true;
            public Vector3 LootPosition => Vector3.zero;
            public float LootRange => 1f;
            public string DisplayName => _name;
            public bool TryLoot(Inventory inv) => false;
        }

        [Test]
        public void NullTarget_HidesPrompt_EmptyLabel()
        {
            Assert.AreEqual("", LootPrompt.BuildLabel(null, KeyCode.E),
                "nothing in range -> empty label -> the prompt is HIDDEN (AC2)");
        }

        [Test]
        public void EmptyName_HidesPrompt_NoHalfBuiltLabel()
        {
            Assert.AreEqual("", LootPrompt.BuildLabel(new FakePickable(""), KeyCode.E),
                "a nameless pickable -> empty label (never a dangling 'Press E to pick up ')");
        }

        [Test]
        public void Berries_NamesTheNearest_LiteralEKey()
        {
            Assert.AreEqual("Press E to pick up berries", LootPrompt.BuildLabel(new FakePickable("berries"), KeyCode.E),
                "the prompt names the nearest pickable + uses the LITERAL E key (Danish-layout safe, AC3)");
        }

        [Test]
        public void GenericName_FlowsThrough_WaterAndWood_ZeroRework()
        {
            // The load-bearing genericity (86cafc6ud): the prompt reads the pickable's OWN DisplayName, so the
            // pond's "water" (86cafc6vx) and the log-pile's "wood" (86caf9u5t) slot in with NO change here.
            Assert.AreEqual("Press E to pick up water", LootPrompt.BuildLabel(new FakePickable("water"), KeyCode.E),
                "a future water source's DisplayName flows straight through — zero rework in the prompt (AC3)");
            Assert.AreEqual("Press E to pick up wood", LootPrompt.BuildLabel(new FakePickable("wood"), KeyCode.E),
                "the log-pile/stick 'wood' DisplayName flows through identically");
        }
    }
}
