using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// The #165 REGRESSION GUARD (PR #165 REQUEST_CHANGES — Drew comment 4826621229): a runtime-spawned
    /// <see cref="LogPile"/> must be DISCOVERED by the live <see cref="PickableLooter"/> and lootable on E — the
    /// EXACT path that was broken in the live build and that CI did not gate.
    ///
    /// === The bug this guards (verified, not hypothesis) ===
    /// <see cref="LogPileSpawner.SpawnAt"/> built the pile but NEVER registered it with the looter, and
    /// <see cref="PickableLooter.ResolveNearestPickable"/> only re-scans (via EnsureDiscovered) when its cache is
    /// EMPTY (Count == 0). The live Boot scene ALWAYS serializes ≥1 IPickable (bush + stick + stone), so the
    /// looter's cache is NEVER empty → the spawned pile was never added → the player could never loot the wood a
    /// felled tree dropped (AC2/AC7/AC8 fail in real play). The fix: SpawnAt now calls
    /// <see cref="PickableLooter.RegisterPickable"/> on every spawned pile.
    ///
    /// === Why THIS test catches the bug CLASS, not just the instance (the testing bar) ===
    /// The breaking condition is "the looter's cache is non-empty when the pile spawns." So this test SEEDS the
    /// looter with a real serialized pickable (a <see cref="StickProp"/>, placed OUT of range) and discovers it
    /// FIRST — exactly reproducing the live build's never-empty cache — THEN spawns the pile and drives the REAL
    /// <see cref="PickableLooter.TryLootNearest"/> path (NOT a manual DiscoverPickables() rediscover, which is the
    /// masking the PlayMode test used to do). On today's pre-fix code this FAILS (the pile is never found → wood
    /// stays 0); after the registration fix it PASSES. A "pickup_count > 0" style assert would have passed during
    /// the whole bug era — so we assert the END-TO-END inventory delta through the live discovery path.
    ///
    /// Headless-safe: the looter's RequestLoot/Update seam needs a frame, so we call TryLootNearest() directly
    /// (the same public seam the PlayMode test + shipped-build capture drive) — no Input device, no frame wait.
    /// </summary>
    public class LogPileSpawnerLootTests
    {
        private GameObject _invGo;
        private GameObject _looterGo;
        private GameObject _stickGo;
        private GameObject _spawnerGo;
        private Inventory _inv;
        private PickableLooter _looter;
        private LogPileSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true; // bare rig — ignore incidental component logs

            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            // The player/looter. Player at origin; the looter measures planar XZ range from it.
            _looterGo = new GameObject("Player");
            _looterGo.transform.position = Vector3.zero;
            _looter = _looterGo.AddComponent<PickableLooter>();
            _looter.inventory = _inv;
            _looter.player = _looterGo.transform;

            // A SEEDED serialized pickable (a stick) placed FAR OUT OF RANGE — its only job is to make the
            // looter's cache NON-EMPTY (Count >= 1), reproducing the live Boot scene's always-≥1-pickable state
            // that is the EXACT breaking condition. Out of range so it can never be the looted target.
            _stickGo = new GameObject("SeedStick");
            _stickGo.transform.position = new Vector3(100f, 0f, 100f); // nowhere near the player
            var stick = _stickGo.AddComponent<StickProp>();
            stick.inventory = _inv;

            // The spawner, with its looter ref wired (the #165 fix's editor-time wiring, mirrored here).
            _spawnerGo = new GameObject("LogPileSpawner");
            _spawner = _spawnerGo.AddComponent<LogPileSpawner>();
            _spawner.WoodYield = 6;
            _spawner.DespawnSeconds = 180f;
            _spawner.looter = _looter;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pile in Object.FindObjectsByType<LogPile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(pile.gameObject);
            Object.DestroyImmediate(_invGo);
            Object.DestroyImmediate(_looterGo);
            Object.DestroyImmediate(_stickGo);
            Object.DestroyImmediate(_spawnerGo);
            LogAssert.ignoreFailingMessages = false;
        }

        // === THE GUARD: the EXACT live-build breaking condition — a pile spawned while the looter cache is
        //     ALREADY non-empty must still be looted (FAILS pre-fix, PASSES post-fix) ===
        [Test]
        public void SpawnedPile_IsLootable_EvenWhenLooterCacheAlreadyHasOtherPickables()
        {
            // Seed the looter's cache so it is NON-EMPTY before the pile spawns (the live build's never-empty
            // state). After this, the looter's lazy EnsureDiscovered re-scan will NOT fire (Count != 0) — so the
            // pile reaching the loot set depends ENTIRELY on SpawnAt registering it (the #165 fix).
            _looter.DiscoverPickables();
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood yet");

            // Fell-event analog: the spawner mints a pile AT the player (in range) holding the tree's whole yield.
            var pile = _spawner.SpawnAt(_looterGo.transform.position, _inv, null);
            Assert.IsNotNull(pile, "the spawner mints a pile");
            Assert.AreEqual(6, pile.LogsRemaining, "the pile holds the spawned yield");

            // Drive the REAL looter discovery+resolve+loot path (the seam the live E press / capture use). NO
            // manual DiscoverPickables() here — that would mask the bug exactly as the old PlayMode test did.
            bool looted = _looter.TryLootNearest();

            Assert.IsTrue(looted,
                "#165 — a felled tree's pile must be lootable via the LIVE looter even when the cache already has " +
                "other pickables (the pile registers itself on spawn). PRE-FIX this is FALSE: the looter never " +
                "re-discovers a non-empty cache, so the unregistered pile is never found.");
            Assert.AreEqual(6, _inv.WoodCount,
                "#165 — the WHOLE pile's wood lands in the inventory through the real discovery path (end-to-end " +
                "inventory delta, NOT a pickup-count proxy)");
            Assert.IsFalse(pile.IsAvailable, "the emptied pile is consumed");
        }

        // === RegisterPickable is dedup-safe: a double register (or a register after the pile is already in the
        //     scanned set) never double-loots / double-counts ===
        [Test]
        public void RegisterPickable_IsDedupSafe_NoDoubleLoot()
        {
            _looter.DiscoverPickables();
            var pile = _spawner.SpawnAt(_looterGo.transform.position, _inv, null); // registers once in SpawnAt
            _looter.RegisterPickable(pile);                                        // explicit double-register
            _looter.RegisterPickable(pile);                                        // and again

            bool looted = _looter.TryLootNearest();
            Assert.IsTrue(looted, "the pile loots once");
            Assert.AreEqual(6, _inv.WoodCount,
                "a double-registered pile is looted ONCE (dedup) — the wood is the pile's yield, not a multiple");
            Assert.IsFalse(pile.IsAvailable, "the pile is consumed after the single loot");
        }

        // === The looter measures from the player: a pile OUT of range is not looted until the player is near
        //     (proves the registration didn't break the nearest-in-range gate) ===
        [Test]
        public void RegisteredPile_OutOfRange_IsNotLootedUntilPlayerIsNear()
        {
            _looter.DiscoverPickables();
            // Spawn the pile far from the player — registered, but out of LootRange.
            var pile = _spawner.SpawnAt(new Vector3(50f, 0f, 50f), _inv, null);
            Assert.IsNotNull(pile);

            Assert.IsFalse(_looter.TryLootNearest(), "an out-of-range registered pile is NOT looted");
            Assert.AreEqual(0, _inv.WoodCount, "no wood while out of range");

            // Walk to the pile → now in range → loots.
            _looterGo.transform.position = pile.LootPosition;
            Assert.IsTrue(_looter.TryLootNearest(), "in range, the registered pile loots");
            Assert.AreEqual(6, _inv.WoodCount, "the whole pile lands once the player is near");
        }
    }
}
