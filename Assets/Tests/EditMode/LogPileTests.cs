using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the lootable LOG PILE (ticket 86caf9u5t — the tree-chop wood rework). A felled tree
    /// drops a <see cref="LogPile"/> holding the whole tree's wood; the player loots it with E (the whole pile or
    /// what fits; the remainder persists; full pack never loses wood). These tests pin the loot CONTRACT + the
    /// despawn schedule headlessly (the timed despawn tween + the live looter path are in the PlayMode tests):
    ///   • a fresh pile holds its spawned log count + is loot-able (AC2);
    ///   • TryLoot grabs the WHOLE pile when the pack has room → inventory gains all the logs, pile consumed (AC2);
    ///   • a FULL pack → TryLoot lands what fits + the remainder PERSISTS in the pile (no wood lost — AC7);
    ///   • a full pack with ZERO room → TryLoot is a clean no-op (false), the pile is NOT consumed (AC7);
    ///   • despawn is scheduled at spawn + DespawnSeconds (AC5);
    ///   • the pile's DisplayName is "wood" (the shared prompt name — AC4).
    /// Mirrors the project's "the loot contract is unit-testable" discipline (StickProp / StoneProp tests).
    /// </summary>
    public class LogPileTests
    {
        private GameObject _invGo;
        private GameObject _spawnerGo;
        private Inventory _inv;
        private LogPileSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true; // bare rig — ignore any incidental component logs
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _spawnerGo = new GameObject("LogPileSpawner");
            _spawner = _spawnerGo.AddComponent<LogPileSpawner>();
            _spawner.WoodYield = 10;
            _spawner.DespawnSeconds = 180f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pile in Object.FindObjectsByType<LogPile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(pile.gameObject);
            Object.DestroyImmediate(_invGo);
            Object.DestroyImmediate(_spawnerGo);
            LogAssert.ignoreFailingMessages = false;
        }

        private LogPile Spawn(int logs)
        {
            _spawner.WoodYield = logs;
            return _spawner.SpawnAt(new Vector3(3f, 0f, 4f), _inv, null);
        }

        [Test]
        public void SpawnAt_BuildsPile_HoldingTheYield_AndLootable()
        {
            var pile = Spawn(10);
            Assert.IsNotNull(pile, "the spawner mints a pile");
            Assert.AreEqual(10, pile.LogsRemaining, "the pile holds the spawned log count (the tree's whole yield)");
            Assert.IsTrue(pile.CanLoot, "a fresh pile with logs + an inventory is loot-able");
            Assert.AreEqual("wood", pile.DisplayName, "the prompt name is the shared 'wood' (AC4)");
            Assert.AreEqual(new Vector3(3f, 0f, 4f), pile.LootPosition, "the pile sits at the spawn position");
        }

        [Test]
        public void TryLoot_GrabsTheWholePile_WhenPackHasRoom_AndConsumesIt()
        {
            var pile = Spawn(8);
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood");

            bool looted = pile.TryLoot(_inv);

            Assert.IsTrue(looted, "looting a pile into an empty pack succeeds");
            Assert.AreEqual(8, _inv.WoodCount, "one E grabs the WHOLE pile -> all 8 logs land in the inventory (AC2)");
            Assert.AreEqual(0, pile.LogsRemaining, "the pile is emptied");
            Assert.IsFalse(pile.CanLoot, "an emptied pile is consumed (no longer loot-able — gone immediately)");
            Assert.IsFalse(pile.gameObject.activeSelf, "the consumed pile's GameObject is deactivated (disappears)");
        }

        [Test]
        public void TryLoot_FullishPack_LandsWhatFits_RemainderPersists_NoWoodLost()
        {
            // Fill the pack so only a FEW wood slots' worth of room remains, then loot a big pile: only what fits
            // lands, the rest STAYS in the pile (AC7 — full pack never loses wood; come back after freeing room).
            var wood = _inv.Catalog.ById(ItemCatalog.WoodId);
            int capacity = CountFreeWoodCapacity();
            Assert.Greater(capacity, 2, "precondition: the empty pack can hold more than 2 wood");

            // Leave room for exactly 3 wood by pre-filling capacity-3.
            int prefill = capacity - 3;
            _inv.Model.AddItem(wood, prefill);
            Assert.AreEqual(prefill, _inv.WoodCount, "precondition: pre-filled to leave room for 3");

            var pile = Spawn(10); // a 10-log pile, but only 3 fit
            bool looted = pile.TryLoot(_inv);

            Assert.IsTrue(looted, "a partial loot (some logs fit) still returns true");
            Assert.AreEqual(prefill + 3, _inv.WoodCount, "exactly what FITS landed (3), no more");
            Assert.AreEqual(7, pile.LogsRemaining, "the un-looted remainder (7) PERSISTS in the pile (AC7 — no wood lost)");
            Assert.IsTrue(pile.CanLoot, "the pile is still loot-able for the remainder (come back after freeing room)");
            Assert.IsTrue(pile.gameObject.activeSelf, "a partially-looted pile is NOT consumed");
        }

        [Test]
        public void TryLoot_FullPack_ZeroRoom_IsACleanNoOp_PileNotConsumed()
        {
            // Fill the pack completely (no wood room at all) → looting lands 0 → false → the pile is untouched.
            var wood = _inv.Catalog.ById(ItemCatalog.WoodId);
            int capacity = CountFreeWoodCapacity();
            _inv.Model.AddItem(wood, capacity);   // pack now full of wood
            Assert.AreEqual(0, CountFreeWoodCapacity(), "precondition: zero wood room left");
            int woodFull = _inv.WoodCount;

            var pile = Spawn(10);
            bool looted = pile.TryLoot(_inv);

            Assert.IsFalse(looted, "a full pack lands 0 -> TryLoot returns false (clean no-op)");
            Assert.AreEqual(woodFull, _inv.WoodCount, "no wood added on a full pack");
            Assert.AreEqual(10, pile.LogsRemaining, "the pile keeps ALL its logs (NOT consumed — come back for it, AC7)");
            Assert.IsTrue(pile.CanLoot, "the un-looted pile is still loot-able");
        }

        [Test]
        public void Despawn_IsScheduledAtSpawnPlusDespawnSeconds()
        {
            float t0 = Time.time;
            _spawner.DespawnSeconds = 120f;
            var pile = _spawner.SpawnAt(Vector3.zero, _inv, null);

            float despawnIn = pile.DespawnAt - t0;
            Assert.GreaterOrEqual(despawnIn, 120f - 0.05f, "despawn is scheduled at spawn-time + DespawnSeconds (AC5)");
            Assert.LessOrEqual(despawnIn, 120f + 0.05f, "despawn is scheduled at spawn-time + DespawnSeconds (not later)");
            Assert.AreEqual(120f, pile.DespawnSeconds, 1e-3f, "the pile reports its despawn lifetime (the live setting)");
        }

        [Test]
        public void NullInventory_SpawnAt_ReturnsNull()
        {
            Assert.IsNull(_spawner.SpawnAt(Vector3.zero, null, null),
                "spawning with no inventory yields no pile (the pile needs an inventory to loot into)");
        }

        // The empty pack's free wood capacity = how many wood AddItem accepts before returning leftover. Probe it
        // on a throwaway inventory so the real one stays clean.
        private int CountFreeWoodCapacity()
        {
            var probeGo = new GameObject("ProbeInv");
            var probe = probeGo.AddComponent<Inventory>();
            var wood = probe.Catalog.ById(ItemCatalog.WoodId);
            // Add a huge amount; capacity = amount that fit.
            int huge = 100000;
            int leftover = probe.Model.AddItem(wood, huge);
            int cap = huge - leftover;
            Object.DestroyImmediate(probeGo);
            // Subtract whatever the REAL inventory already holds (its remaining room is cap - current).
            return cap - _inv.WoodCount;
        }
    }
}
