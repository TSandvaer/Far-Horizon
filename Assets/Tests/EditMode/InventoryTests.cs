using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Deterministic EditMode coverage for the U2-2 inventory data surface (ticket 86ca8bdaq).
    ///
    /// The held-resource ledger (HasAxe / WoodCount) is pure logic — no scene, no Update — so it's fully
    /// driveable here. Guards the VOCABULARY CONTRACT names U2-5's HUD consumes verbatim, the single-write
    /// Changed-event discipline (HUD subscribes, never polls), and the craft idempotency the craft spot
    /// relies on.
    /// </summary>
    public class InventoryTests
    {
        private GameObject _go;
        private Inventory _inv;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Inventory");
            _inv = _go.AddComponent<Inventory>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void StartsEmpty_NoAxe_NoWood()
        {
            Assert.IsFalse(_inv.HasAxe, "the castaway starts with no axe (the loop begins by crafting it)");
            Assert.AreEqual(0, _inv.WoodCount, "no wood before any chop (U2-3)");
        }

        [Test]
        public void CraftAxe_SetsHasAxe_AndReturnsTrue_OnTheTransition()
        {
            bool changed = false;
            _inv.Changed += () => changed = true;

            bool crafted = _inv.CraftAxe();

            Assert.IsTrue(crafted, "CraftAxe returns true on the false->true transition (one-shot feedback)");
            Assert.IsTrue(_inv.HasAxe, "HasAxe is true after crafting");
            Assert.IsTrue(changed, "Changed fires on craft so the HUD repaints without polling");
        }

        [Test]
        public void CraftAxe_IsIdempotent_NoDoubleFire()
        {
            _inv.CraftAxe();
            int fires = 0;
            _inv.Changed += () => fires++;

            bool second = _inv.CraftAxe();

            Assert.IsFalse(second, "crafting an axe you already hold returns false (no transition)");
            Assert.AreEqual(0, fires, "no Changed event on a no-op re-craft");
            Assert.IsTrue(_inv.HasAxe, "still holds the axe");
        }

        [Test]
        public void AddWood_Accumulates_AndFiresChanged()
        {
            int fires = 0;
            _inv.Changed += () => fires++;

            Assert.AreEqual(3, _inv.AddWood(3), "AddWood returns the new total");
            Assert.AreEqual(5, _inv.AddWood(2), "wood accumulates");
            Assert.AreEqual(5, _inv.WoodCount, "WoodCount reflects the accumulated total");
            Assert.AreEqual(2, fires, "Changed fires once per real add");
        }

        [Test]
        public void AddWood_ZeroOrNegative_IsNoOp()
        {
            int fires = 0;
            _inv.Changed += () => fires++;

            Assert.AreEqual(0, _inv.AddWood(0), "adding zero wood is a no-op");
            Assert.AreEqual(0, _inv.AddWood(-4), "negative wood never decrements (clamped no-op)");
            Assert.AreEqual(0, _inv.WoodCount, "WoodCount stays at 0");
            Assert.AreEqual(0, fires, "no Changed event on a no-op add");
        }
    }
}
