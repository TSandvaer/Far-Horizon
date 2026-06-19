using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the in-game EAT INPUT (ticket 86caa5zz3 / #101 — "I can't eat berries").
    ///
    /// The code eat-seam was already tested (BerryBush.EatBerry / HungerNeed.TryEatBerry), but NOTHING in the
    /// build invoked it — there was no player input bound to eating. <see cref="EatBerryAction"/> is that
    /// missing call-site (the E key, collision-free vs the RMB orbit camera). These tests drive the action's
    /// public seam <see cref="EatBerryAction.TryEatOneBerry"/> directly (isolating the eat LOGIC from the key
    /// device — the keypath itself is one Input.GetKeyDown line + a shipped-build capture) and prove the input
    /// integration END TO END:
    ///   - eat with a berry held -> EXACTLY one berry consumed AND hunger restored (atomic, the same
    ///     all-or-nothing TryEatBerry seam — consume + restore inseparable);
    ///   - eat with NO berries -> clean no-op (false, no negative inventory, no hunger change);
    ///   - eat with NO HungerNeed wired -> consumes gracefully (no null-ref, AC5b).
    ///
    /// SCOPE NOTE: this is the eat-INPUT integration seam, distinct from BerryBush's own EatBerry consume
    /// test (BerryBushPlayModeTests, AC5a). It asserts both the decrement AND the restore because verifying
    /// the input feeds hunger end-to-end is exactly the gap the #101 soak exposed.
    /// </summary>
    public class EatBerryActionPlayModeTests
    {
        private GameObject _go;
        private Inventory _inv;
        private HungerNeed _hunger;
        private EatBerryAction _eat;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("EatActionTest");
            _inv = _go.AddComponent<Inventory>();
            _hunger = _go.AddComponent<HungerNeed>();
            _hunger.max = 100f;
            _hunger.startFull = false;          // start hungry so the restore is observable
            _hunger.berryRestoreAmount = 18f;

            _eat = _go.AddComponent<EatBerryAction>();
            _eat.inventory = _inv;
            _eat.hunger = _hunger;
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_go);

        private int Berries => _inv.Model.CountItem(ItemCatalog.BerryId);

        private void GiveBerries(int n)
        {
            var def = _inv.Catalog.ById(ItemCatalog.BerryId);
            Assert.IsNotNull(def, "the catalog must define the berry item");
            _inv.Model.AddItem(def, n);
        }

        // === The eat input consumes EXACTLY one berry AND restores hunger (atomic, end-to-end) ========
        [Test]
        public void EatInput_ConsumesOneBerry_AndRestoresHunger()
        {
            GiveBerries(3);
            Assert.AreEqual(3, Berries, "precondition: 3 berries held");
            // Drive hunger to a known low value so the restore is measurable.
            _hunger.TickSeconds(0f); // ensure started path is sane (no-op tick)
            float hungerBefore = _hunger.Current; // starts at 0 (startFull=false) before any tick

            bool ate = _eat.TryEatOneBerry();

            Assert.IsTrue(ate, "eating with a berry held succeeds (the eat input fires the seam)");
            Assert.AreEqual(2, Berries, "the eat input consumes EXACTLY one berry (the stack decrements)");
            Assert.AreEqual(hungerBefore + _hunger.berryRestoreAmount, _hunger.Current, 0.01f,
                "the eat input restores exactly berryRestoreAmount hunger (consume + restore are atomic)");
        }

        // === No berries -> clean no-op (false, no negative inventory, no hunger change) ================
        [Test]
        public void EatInput_WithNoBerries_IsNoOp()
        {
            Assert.AreEqual(0, Berries, "precondition: no berries");
            float hungerBefore = _hunger.Current;

            bool ate = _eat.TryEatOneBerry();

            Assert.IsFalse(ate, "eating with no berries returns false");
            Assert.AreEqual(0, Berries, "no negative inventory — the all-or-nothing consume debits nothing");
            Assert.AreEqual(hungerBefore, _hunger.Current, 0.0001f,
                "no berry -> NO hunger restore (the atomic seam restores only if the consume succeeded)");
        }

        // === Graceful with no HungerNeed wired -> consumes, no restore, no null-ref (AC5b) =============
        [Test]
        public void EatInput_WithoutHungerNeed_ConsumesGracefully()
        {
            _eat.hunger = null; // no hunger need wired
            GiveBerries(2);

            bool ate = false;
            Assert.DoesNotThrow(() => ate = _eat.TryEatOneBerry(),
                "eating with no HungerNeed must NOT null-ref on a missing AddFood (AC5b graceful)");
            Assert.IsTrue(ate, "the berry is still consumed when no hunger need is present");
            Assert.AreEqual(1, Berries, "graceful no-HungerNeed: exactly one berry consumed, restore no-op'd");
        }

        // === Repeated eats drain the stack one at a time until empty, then no-op ======================
        [Test]
        public void EatInput_RepeatedEats_DrainOneAtATime_ThenNoOp()
        {
            GiveBerries(2);
            Assert.IsTrue(_eat.TryEatOneBerry(), "1st eat consumes a berry");
            Assert.AreEqual(1, Berries);
            Assert.IsTrue(_eat.TryEatOneBerry(), "2nd eat consumes the last berry");
            Assert.AreEqual(0, Berries);
            Assert.IsFalse(_eat.TryEatOneBerry(), "3rd eat with an empty stack is a no-op");
            Assert.AreEqual(0, Berries, "no negative inventory after draining the stack");
        }

        // === No inventory wired -> safe no-op (build-safety: never null-ref) ==========================
        [Test]
        public void EatInput_WithNoInventory_IsSafeNoOp()
        {
            _eat.inventory = null;
            bool ate = false;
            Assert.DoesNotThrow(() => ate = _eat.TryEatOneBerry(),
                "eating with no inventory wired must be a safe no-op, never a null-ref");
            Assert.IsFalse(ate, "no inventory -> nothing eaten");
        }
    }
}
