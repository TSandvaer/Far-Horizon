using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the FRESHWATER POND drink interaction (ticket 86caamkv7, AC3/AC3a) — the
    /// proximity gate + the drink-scoop → AddWater seam actually firing through FreshwaterPond.Update / its
    /// public seams. This OWNS the AC3a integration test (the drink-scoop → AddWater seam crosses the need +
    /// the pond surfaces; per AC3a it is owned by THIS ticket).
    ///
    /// We drive the player transform + the public DrinkScoop() seam directly, isolating the pond's proximity +
    /// drink logic from pathfinding (NavMesh/click-move is covered by the shipped-build capture). Proves:
    ///   - the player FAR from the pond is NOT in range; a scoop FAR from the pond does NOTHING (proximity is
    ///     load-bearing — Tess silent-killer #3);
    ///   - the player AT the pond IS in range; a scoop raises thirst by exactly the per-scoop amount AND fires
    ///     Changed (the HUD seam);
    ///   - repeatable: N in-range scoops raise N× the per-scoop amount (the "scoop, scoop, scoop" cadence);
    ///   - thirst is NOT an inventory item — a scoop creates/consumes NO inventory entry (Tess silent-killer
    ///     #4: a copy-paste from the berry eat-action that routed water through inventory is a design break);
    ///   - graceful when no ThirstNeed is wired (AC3b — no null-ref).
    /// </summary>
    public class FreshwaterPondPlayModeTests
    {
        private GameObject _thirstGo;
        private GameObject _playerGo;
        private GameObject _pondGo;
        private ThirstNeed _thirst;
        private FreshwaterPond _pond;

        [SetUp]
        public void SetUp()
        {
            _thirstGo = new GameObject("Thirst");
            _thirst = _thirstGo.AddComponent<ThirstNeed>();
            _thirst.max = 100f;
            _thirst.decayPerSecond = 0f;       // freeze decay so the test asserts ONLY the scoop deltas
            _thirst.waterScoopAmount = 14f;
            _thirst.startFull = false;
            _thirst.startFraction01 = 0.3f;    // pressured-with-headroom so a scoop has room to climb

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the pond

            _pondGo = new GameObject("FreshwaterPond");
            _pondGo.transform.position = Vector3.zero;
            _pond = _pondGo.AddComponent<FreshwaterPond>();
            _pond.thirst = _thirst;
            _pond.player = _playerGo.transform;
            _pond.pondSurfaceRadius = 2.6f;
            _pond.drinkRadius = 2.0f; // effective ~4.6u
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_thirstGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_pondGo);
        }

        // === AC3: FAR from the pond -> not in range -> a scoop does NOTHING (proximity is load-bearing) ===
        [UnityTest]
        public IEnumerator FarFromPond_NotInRange_ScoopDoesNothing()
        {
            yield return null; // let Update recompute PlayerInRange (player is far)
            Assert.IsFalse(_pond.PlayerInRange, "the player far from the pond is NOT in range");

            float before = _thirst.Current;
            int changedFires = 0;
            _thirst.Changed += _ => changedFires++;

            bool drank = _pond.DrinkScoop();
            Assert.IsFalse(drank, "a scoop FAR from the pond returns false");
            Assert.AreEqual(before, _thirst.Current, 0.001f, "a scoop FAR from the pond restores NOTHING");
            Assert.AreEqual(0, changedFires, "no Changed fired on an out-of-range scoop (the HUD must not repaint)");
        }

        // === AC3/AC6: AT the pond -> in range -> a scoop raises thirst by the per-scoop amount + fires Changed ===
        [UnityTest]
        public IEnumerator AtPond_InRange_ScoopRaisesThirst_AndFiresChanged()
        {
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // walk onto the pond
            yield return null; // let Update recompute PlayerInRange
            Assert.IsTrue(_pond.PlayerInRange, "the player at the pond IS in range");

            float before = _thirst.Current;
            float lastChanged = -1f;
            _thirst.Changed += v => lastChanged = v;

            bool drank = _pond.DrinkScoop();
            Assert.IsTrue(drank, "a scoop AT the pond succeeds");
            Assert.AreEqual(before + _thirst.waterScoopAmount, _thirst.Current, 0.001f,
                "a scoop raises thirst by exactly the per-scoop amount (the drink->AddWater seam, AC3a)");
            Assert.AreEqual(_thirst.Current01, lastChanged, 0.001f,
                "the scoop fired Changed with the new Current01 (the load-bearing HUD subscribe-never-poll seam)");
        }

        // === AC3: repeatable — N in-range scoops raise N× the per-scoop amount (the scoop-scoop-scoop ritual) ===
        [UnityTest]
        public IEnumerator AtPond_RepeatableScoops_RaiseNxAmount()
        {
            _playerGo.transform.position = new Vector3(1f, 0f, 0f);
            yield return null;
            Assert.IsTrue(_pond.PlayerInRange, "in range");

            float before = _thirst.Current;
            _pond.DrinkScoop();
            _pond.DrinkScoop();
            _pond.DrinkScoop();
            Assert.AreEqual(before + 3f * _thirst.waterScoopAmount, _thirst.Current, 0.001f,
                "three in-range scoops raise thirst by 3× the per-scoop amount (repeatable, no cooldown gate)");
        }

        // === AC3 (Tess silent-killer #4): thirst is NOT berries — a scoop touches NO inventory at all ===
        [UnityTest]
        public IEnumerator Scoop_CreatesNoInventoryEntry_ThirstIsNotBerries()
        {
            // Put an Inventory in the scene; a scoop must leave it completely untouched (water is not an item).
            var invGo = new GameObject("Inventory");
            var inv = invGo.AddComponent<Inventory>();
            try
            {
                int berriesBefore = inv.Model.CountItem(ItemCatalog.BerryId);

                _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
                yield return null;
                Assert.IsTrue(_pond.PlayerInRange, "in range");

                _pond.DrinkScoop();

                Assert.AreEqual(berriesBefore, inv.Model.CountItem(ItemCatalog.BerryId),
                    "drinking creates/consumes NO inventory entry — water is NOT berries (a scoop that routed " +
                    "water through inventory is a design break)");
                // No item kind should have appeared from a scoop — assert every slot count is unchanged total.
                int totalItems = 0;
                foreach (var slot in inv.Model.InventorySlots) if (!slot.IsEmpty) totalItems += slot.Count;
                Assert.AreEqual(0, totalItems, "a scoop adds nothing to any inventory slot (thirst is not an item)");
            }
            finally { Object.Destroy(invGo); }
        }

        // === AC3b graceful no-ThirstNeed: drinking before the thirst need is wired is a clean no-op (no null-ref) ===
        [UnityTest]
        public IEnumerator Scoop_WithoutThirstNeed_IsGracefulNoOp()
        {
            _pond.thirst = null; // no thirst need wired
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            yield return null;
            Assert.IsTrue(_pond.PlayerInRange, "in range");
            Assert.DoesNotThrow(() => _pond.DrinkScoop(),
                "drinking with no ThirstNeed wired must NOT null-ref (AC3b graceful) — it just restores nothing");
        }

        // === Effective range scales with the pond surface radius (drink from the EDGE of a wide pond) ===
        [UnityTest]
        public IEnumerator EffectiveRange_IncludesThePondSurfaceRadius()
        {
            // Stand just outside the surface radius but inside surface+drink — should be in range.
            _pond.pondSurfaceRadius = 3f;
            _pond.drinkRadius = 1f; // effective 4u
            _playerGo.transform.position = new Vector3(3.5f, 0f, 0f); // 3.5u: outside surface, inside eff (4u)
            yield return null;
            Assert.IsTrue(_pond.PlayerInRange, "in range from the pond EDGE (eff radius = surface + drink)");

            _playerGo.transform.position = new Vector3(5f, 0f, 0f); // 5u: outside eff (4u)
            yield return null;
            Assert.IsFalse(_pond.PlayerInRange, "out of range past the effective radius");
        }
    }
}
