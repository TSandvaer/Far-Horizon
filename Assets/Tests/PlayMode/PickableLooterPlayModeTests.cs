using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the E-LOOT FOUNDATION (ticket 86caf7a6q) — the SHARED loot surface
    /// (<see cref="IPickable"/> + <see cref="PickableLooter"/>) that downstream world resources
    /// (sticks 86caa96rd, stones 86caa4c96) consume. Proves the surface independently of any one item
    /// type, using a FAKE pickable so the looter contract is verified item-agnostically (the "the loot
    /// surface is consumable by a fake pickable" AC6 requirement):
    ///   - E loots the in-range fake pickable -> exactly one TryLoot fires (AC1);
    ///   - standing in range WITHOUT pressing E loots NOTHING (AC4 — not proximity-auto);
    ///   - E out of range loots nothing (AC3 — nearest-in-range resolve finds nothing);
    ///   - several pickables in range -> the NEAREST wins (AC3);
    ///   - a not-CanLoot (spent) pickable is skipped even in range (AC3);
    ///   - a pickable whose TryLoot declines (e.g. full pack) is a clean no-op for the looter (AC1).
    ///
    /// A FakePickable records its TryLoot calls + reports a tunable position/range/CanLoot/loot-result, so
    /// these tests exercise the LOOTER's resolution + guard logic with NO real item (the surface contract).
    /// </summary>
    public class PickableLooterPlayModeTests
    {
        // A minimal IPickable test double — records loot calls, exposes tunable surface fields. This is the
        // "fake pickable" the shared surface must be consumable by (AC6) — proof the looter is item-agnostic.
        private class FakePickable : MonoBehaviour, IPickable
        {
            public bool canLoot = true;
            public float range = 2f;
            public bool lootSucceeds = true;   // TryLoot returns this (e.g. false = full pack / spent)
            public int LootCalls;              // how many times TryLoot was invoked
            public Inventory LastInventory;    // the inventory passed to the most recent TryLoot

            public bool CanLoot => canLoot;
            public Vector3 LootPosition => transform.position;
            public float LootRange => range;

            public bool TryLoot(Inventory inv)
            {
                LootCalls++;
                LastInventory = inv;
                return lootSucceeds;
            }
        }

        private GameObject _invGo;
        private GameObject _playerGo;
        private Inventory _inv;
        private PickableLooter _looter;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = Vector3.zero;

            _looter = _playerGo.AddComponent<PickableLooter>();
            _looter.inventory = _inv;
            _looter.player = _playerGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
        }

        private FakePickable SpawnPickable(Vector3 pos)
        {
            var go = new GameObject("FakePickable");
            go.transform.position = pos;
            var p = go.AddComponent<FakePickable>();
            return p;
        }

        // === AC1: E in range loots the nearest pickable (one TryLoot, with our inventory) ===
        [UnityTest]
        public IEnumerator PressE_InRange_LootsThePickable()
        {
            var p = SpawnPickable(new Vector3(1f, 0f, 0f)); // within range 2
            _looter.DiscoverPickables(); // pick up the just-spawned fake

            _looter.RequestLoot();
            yield return null;

            Assert.AreEqual(1, p.LootCalls, "E in range fires TryLoot exactly once");
            Assert.AreSame(_inv, p.LastInventory, "the looter passes its own inventory to TryLoot");

            Object.Destroy(p.gameObject);
        }

        // === AC4 (not proximity-auto): standing in range WITHOUT E never loots ===
        [UnityTest]
        public IEnumerator InRange_NoKeyPress_NeverLoots()
        {
            var p = SpawnPickable(new Vector3(0.5f, 0f, 0.5f));
            _looter.DiscoverPickables();

            for (int i = 0; i < 20; i++) yield return null; // many frames, NO RequestLoot

            Assert.AreEqual(0, p.LootCalls,
                "standing in range with no E press loots NOTHING — the looter is NOT proximity-auto (AC4)");
            Object.Destroy(p.gameObject);
        }

        // === AC3: E out of range loots nothing (the nearest-in-range resolve finds nothing in reach) ===
        [UnityTest]
        public IEnumerator PressE_OutOfRange_LootsNothing()
        {
            var p = SpawnPickable(new Vector3(50f, 0f, 50f)); // far beyond range
            _looter.DiscoverPickables();

            _looter.RequestLoot();
            yield return null;

            Assert.AreEqual(0, p.LootCalls, "E out of range never reaches TryLoot (resolve finds nothing in range)");
            Object.Destroy(p.gameObject);
        }

        // === AC3: several pickables in range -> the NEAREST is looted ===
        [UnityTest]
        public IEnumerator SeveralInRange_NearestIsLooted()
        {
            var near = SpawnPickable(new Vector3(0.5f, 0f, 0f)); // 0.5 away
            var far  = SpawnPickable(new Vector3(1.8f, 0f, 0f)); // 1.8 away (both within range 2)
            _looter.DiscoverPickables();

            _looter.RequestLoot();
            yield return null;

            Assert.AreEqual(1, near.LootCalls, "the NEAREST in-range pickable is looted (AC3)");
            Assert.AreEqual(0, far.LootCalls, "the farther pickable is NOT looted (only the nearest)");

            Object.Destroy(near.gameObject);
            Object.Destroy(far.gameObject);
        }

        // === AC3: a spent (not CanLoot) pickable is skipped even when it is the nearest ===
        [UnityTest]
        public IEnumerator NearestButSpent_IsSkipped_NextLootableWins()
        {
            var spentNear = SpawnPickable(new Vector3(0.3f, 0f, 0f));
            spentNear.canLoot = false;                          // spent -> not loot-able
            var ripeFar = SpawnPickable(new Vector3(1.5f, 0f, 0f));
            _looter.DiscoverPickables();

            _looter.RequestLoot();
            yield return null;

            Assert.AreEqual(0, spentNear.LootCalls, "a not-CanLoot pickable is never looted, even when nearest");
            Assert.AreEqual(1, ripeFar.LootCalls, "the nearest LOOT-ABLE pickable is looted instead (AC3)");

            Object.Destroy(spentNear.gameObject);
            Object.Destroy(ripeFar.gameObject);
        }

        // === AC1: a pickable whose TryLoot DECLINES (full pack / spent mid-resolve) is a clean no-op ===
        [UnityTest]
        public IEnumerator InRangeButTryLootDeclines_IsCleanNoOp()
        {
            var p = SpawnPickable(new Vector3(0.5f, 0f, 0f));
            p.lootSucceeds = false; // e.g. a full pack — TryLoot returns false
            _looter.DiscoverPickables();

            bool looted = false;
            _looter.RequestLoot();
            yield return null;
            // The press resolved a target + called TryLoot, but TryLoot declined -> TryLootNearest reports false.
            looted = _looter.TryLootNearest(); // direct call to read the boolean (still declines)

            Assert.AreEqual(2, p.LootCalls, "TryLoot is attempted (once per loot request) — the target was in range");
            Assert.IsFalse(looted, "a declined TryLoot (full pack) is a clean false no-op for the looter (AC1)");

            Object.Destroy(p.gameObject);
        }

        // === The modal-panel guard: E does NOT loot while a UI panel owns the screen (AC — input guard) ===
        [UnityTest]
        public IEnumerator UiPanelOpen_PressE_DoesNotLoot()
        {
            var p = SpawnPickable(new Vector3(0.5f, 0f, 0f));
            _looter.DiscoverPickables();

            UiInputGate.PushPanel(); // a modal gameplay-UI panel is open
            try
            {
                _looter.RequestLoot();
                yield return null;
                Assert.AreEqual(0, p.LootCalls,
                    "E does not loot while a modal panel owns the screen (the world-input guard)");
            }
            finally { UiInputGate.PopPanel(); }

            // And once the panel closes, E loots again (proves the guard, not a dead looter).
            _looter.RequestLoot();
            yield return null;
            Assert.AreEqual(1, p.LootCalls, "after the panel closes, E loots again");

            Object.Destroy(p.gameObject);
        }
    }
}
