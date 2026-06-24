using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the in-game DRINK INPUT (ticket 86caamkv7, AC3) — the player call-site for the
    /// drink seam. <see cref="DrinkAction"/> binds Q to one hand-scoop at the <see cref="FreshwaterPond"/>.
    /// These tests drive the action's public seam <see cref="DrinkAction.TryDrinkOneScoop"/> directly
    /// (isolating the drink LOGIC from the key device — the keypath itself is one Input.GetKeyDown line + a
    /// shipped-build capture) and prove the input integration END TO END:
    ///   - drink AT the pond -> thirst restored by exactly the per-scoop amount (the input fires the seam);
    ///   - drink AWAY from the pond -> clean no-op (false, no thirst change — proximity is load-bearing);
    ///   - drink with no pond/ThirstNeed wired -> graceful no-op (no null-ref).
    ///
    /// Sibling of EatBerryActionPlayModeTests (the eat-INPUT integration seam) — the thirst counterpart.
    /// </summary>
    public class DrinkActionPlayModeTests
    {
        private GameObject _go;          // holds the thirst + drink action (the Survival-object analogue)
        private GameObject _playerGo;
        private GameObject _pondGo;
        private ThirstNeed _thirst;
        private FreshwaterPond _pond;
        private DrinkAction _drink;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("DrinkActionTest");
            _thirst = _go.AddComponent<ThirstNeed>();
            _thirst.max = 100f;
            _thirst.decayPerSecond = 0f;     // freeze decay so the test asserts ONLY the scoop deltas
            _thirst.waterScoopAmount = 14f;
            _thirst.startFull = false;
            _thirst.startFraction01 = 0.3f;

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the pond

            _pondGo = new GameObject("FreshwaterPond");
            _pondGo.transform.position = Vector3.zero;
            _pond = _pondGo.AddComponent<FreshwaterPond>();
            _pond.thirst = _thirst;
            _pond.player = _playerGo.transform;
            _pond.pondSurfaceRadius = 2.6f;
            _pond.drinkRadius = 2.0f;

            _drink = _go.AddComponent<DrinkAction>();
            _drink.pond = _pond;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_go);
            Object.Destroy(_playerGo);
            Object.Destroy(_pondGo);
        }

        // === Drink AT the pond -> thirst restored end-to-end (the input fires the seam) ================
        [UnityTest]
        public IEnumerator DrinkInput_AtPond_RestoresThirst()
        {
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // walk onto the pond
            yield return null; // let FreshwaterPond.Update recompute PlayerInRange
            Assert.IsTrue(_pond.PlayerInRange, "precondition: at the pond, in range");

            float before = _thirst.Current;
            float before01 = _thirst.Current01;
            float lastChanged = -1f;
            _thirst.Changed += v => lastChanged = v;     // the HUD subscribe-never-poll seam

            bool drank = _drink.TryDrinkOneScoop();

            Assert.IsTrue(drank, "drinking at the pond succeeds (the drink input fires the seam)");
            Assert.AreEqual(before + _thirst.waterScoopAmount, _thirst.Current, 0.01f,
                "the drink input restores exactly waterScoopAmount thirst (end-to-end through the pond seam)");
            // Tie the Q-input path to the HUD-VISIBLE effect (Drew NIT): the visible Current01 rises AND the
            // scoop fired Changed with the new Current01 — the surface the thirst bar (86caamkxv) will bind.
            // Mirrors FreshwaterPondPlayModeTests.AtPond_InRange_ScoopRaisesThirst_AndFiresChanged.
            Assert.Greater(_thirst.Current01, before01,
                "the drink input raises the VISIBLE Current01 (the HUD-bound surface), not just the raw Current");
            Assert.AreEqual(_thirst.Current01, lastChanged, 0.001f,
                "the drink input fired Changed with the new Current01 (the load-bearing HUD subscribe-never-poll seam)");
        }

        // === Drink AWAY from the pond -> clean no-op (proximity is load-bearing) =======================
        [UnityTest]
        public IEnumerator DrinkInput_AwayFromPond_IsNoOp()
        {
            yield return null; // player is far -> not in range
            Assert.IsFalse(_pond.PlayerInRange, "precondition: away from the pond, NOT in range");

            float before = _thirst.Current;
            bool drank = _drink.TryDrinkOneScoop();

            Assert.IsFalse(drank, "drinking away from the pond returns false");
            Assert.AreEqual(before, _thirst.Current, 0.0001f,
                "no thirst change away from the pond — the proximity gate is inseparable from the restore");
        }

        // === Repeated drinks at the pond drain into thirst one scoop at a time (the scoop ritual) ======
        [UnityTest]
        public IEnumerator DrinkInput_RepeatedScoops_RaiseThirstStepwise()
        {
            _playerGo.transform.position = new Vector3(1f, 0f, 0f);
            yield return null;
            Assert.IsTrue(_pond.PlayerInRange, "in range");

            float before = _thirst.Current;
            _drink.TryDrinkOneScoop();
            _drink.TryDrinkOneScoop();
            Assert.AreEqual(before + 2f * _thirst.waterScoopAmount, _thirst.Current, 0.01f,
                "two drink-input presses raise thirst by 2× the per-scoop amount (repeatable ritual)");
        }

        // === Graceful with no pond wired -> safe no-op (build-safety: never null-ref) ==================
        [Test]
        public void DrinkInput_WithNoPond_IsSafeNoOp()
        {
            _drink.pond = null;
            bool drank = false;
            Assert.DoesNotThrow(() => drank = _drink.TryDrinkOneScoop(),
                "drinking with no pond wired must be a safe no-op, never a null-ref");
            Assert.IsFalse(drank, "no pond -> nothing drunk");
        }

        // === Graceful with no ThirstNeed wired -> drinks resolve to no-op (no null-ref, AC3b) ==========
        [UnityTest]
        public IEnumerator DrinkInput_WithoutThirstNeed_IsGracefulNoOp()
        {
            _pond.thirst = null;
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            yield return null;
            Assert.IsTrue(_pond.PlayerInRange, "in range");
            bool drank = false;
            Assert.DoesNotThrow(() => drank = _drink.TryDrinkOneScoop(),
                "drinking with no ThirstNeed wired must NOT null-ref (AC3b graceful)");
            Assert.IsFalse(drank, "no ThirstNeed -> nothing restored (a scoop still requires being in range)");
        }
    }
}
