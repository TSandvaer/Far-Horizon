using NUnit.Framework;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode truth-table guard for the E-LOOT key gate (ticket 86caf7a6q — pressing E loots the nearest
    /// in-range pickable, but NOT while a modal gameplay-UI panel owns the screen). The decision is the pure
    /// static <see cref="PickableLooter.ShouldLootOnKey"/>, so the WHOLE guard table is asserted headlessly
    /// here — the live E→loot loop is covered in PlayMode (PickableLooterPlayModeTests). Sibling of
    /// <see cref="ChopTree.ShouldChopOnClick"/>'s ChopClickGateTests.
    ///
    /// ShouldLootOnKey(inRange, uiPanelOpen) must be:
    ///   true  ONLY when inRange && !uiPanelOpen;
    ///   false if NOTHING is in range (the not-auto / nothing-to-loot case) OR a modal panel is open
    ///   (don't loot a bush while the player is clicking in the inventory/settings panel).
    /// (E is a keyboard key, so ChopTree's mouse-only over-UI / RMB-drag guards do NOT apply — the loot gate
    /// has exactly the two preconditions.)
    /// </summary>
    public class LootKeyGateTests
    {
        [Test]
        public void Loots_OnlyWhenInRange_AndNoPanelOpen()
        {
            Assert.IsTrue(PickableLooter.ShouldLootOnKey(inRange: true, uiPanelOpen: false),
                "E loots ONLY when something is in range AND no modal panel owns the screen");
        }

        [Test]
        public void DoesNotLoot_WhenNothingInRange()
        {
            Assert.IsFalse(PickableLooter.ShouldLootOnKey(inRange: false, uiPanelOpen: false),
                "E with nothing in range loots nothing (the nearest-in-range resolve found no pickable)");
        }

        [Test]
        public void DoesNotLoot_WhenPanelOpen_EvenInRange()
        {
            Assert.IsFalse(PickableLooter.ShouldLootOnKey(inRange: true, uiPanelOpen: true),
                "E does NOT loot while a modal gameplay-UI panel owns the screen — even with a pickable in range");
        }

        [Test]
        public void DoesNotLoot_WhenBothPreconditionsFail()
        {
            Assert.IsFalse(PickableLooter.ShouldLootOnKey(inRange: false, uiPanelOpen: true),
                "nothing in range AND a panel open -> no loot (both gates fail)");
        }
    }
}
