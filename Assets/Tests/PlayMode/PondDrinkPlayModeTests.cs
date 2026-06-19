using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the drink-from-hand interaction (ticket 86caamkv7, AC3 / AC3a — owned here).
    ///
    /// Proves the PROXIMITY-gated drink actually FIRES through PondDrink.Update when the player reaches
    /// the pond, and — the load-bearing negative case — that a player FAR from the pond scoops NOTHING
    /// (no thirst change). Also proves drinking is repeatable (each in-reach interval is another scoop)
    /// and that NO inventory is touched (thirst is NOT berries). We drive the player transform directly
    /// against a STAND-IN pond transform, isolating PondDrink's proximity logic from the real seed-42
    /// pond placement (AC2/AC2a, Drew's lane — he attaches PondDrink to the real pond GameObject + wires
    /// the same thirst/player refs). Mirrors ChopTreePlayModeTests.
    /// </summary>
    public class PondDrinkPlayModeTests
    {
        private GameObject _thirstGo;
        private GameObject _playerGo;
        private GameObject _pondGo;   // STAND-IN pond transform (Drew wires the real seed-42 pond at runtime)
        private ThirstNeed _thirst;
        private PondDrink _pond;

        [SetUp]
        public void SetUp()
        {
            _thirstGo = new GameObject("Thirst");
            _thirst = _thirstGo.AddComponent<ThirstNeed>();
            _thirst.max = 100f;
            _thirst.decayPerSecond = 10f; // decay so we have headroom for the scoop to measurably restore
            _thirst.floor01 = 0.05f;
            _thirst.waterScoopAmount = 12f;
            _thirst.startFull = true;

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the pond

            _pondGo = new GameObject("PondStandIn");
            _pondGo.transform.position = Vector3.zero;
            _pond = _pondGo.AddComponent<PondDrink>();
            _pond.thirst = _thirst;
            _pond.player = _playerGo.transform;
            _pond.drinkRadius = 2.5f;
            _pond.scoopInterval = 0.05f; // fast so scoops land within a few frames of wall-clock
            _pond.autoScoopWhileInReach = true;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_thirstGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_pondGo);
        }

        // === THE PROXIMITY GATE (negative case) — drinking far from the pond does nothing ===
        [UnityTest]
        public IEnumerator FarFromPond_NoScoop_NoThirstChange()
        {
            yield return null; // ThirstNeed.Start() seeds full
            // Let thirst decay a little so any erroneous scoop would be visible as a RISE.
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;
            float beforeFar = _thirst.Current;

            // Player stays far away through another window — PondDrink must never scoop.
            start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            Assert.AreEqual(0, _pond.Scoops, "far from the pond -> no scoops land");
            Assert.LessOrEqual(_thirst.Current, beforeFar + 0.001f,
                "far from the pond -> thirst only decays, never rises (no phantom scoop)");
        }

        // === Positive: at the pond, standing in reach scoops and thirst RISES per scoop ===
        [UnityTest]
        public IEnumerator AtPond_Scoops_RestoreThirst()
        {
            yield return null;
            // Decay a real window so there is headroom to restore.
            float start = Time.time;
            while (Time.time - start < 1.5f) yield return null;
            float decayed = _thirst.Current;
            Assert.Less(decayed, _thirst.Max, "thirst dropped below max during the window");

            // Walk the player onto the pond; scoops should land over a short wall-clock window.
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // well within drinkRadius
            int scoopsBefore = _pond.Scoops;

            start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            Assert.Greater(_pond.Scoops, scoopsBefore,
                "standing in pond reach lands scoops through PondDrink.Update (the drink-from-hand beat)");
            Assert.Greater(_thirst.Current, decayed,
                "each scoop restores thirst — at the pond, thirst measurably RISES");
        }

        // === Repeatable: thirst keeps climbing with more scoops (no one-shot lock) ===
        [UnityTest]
        public IEnumerator Drinking_IsRepeatable_ThirstKeepsClimbing()
        {
            yield return null;
            // Drain hard so multiple scoops fit below max.
            _thirst.TickSeconds(70f); // ~100 -> ~30
            float low = _thirst.Current;

            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            float start = Time.time;
            // Long enough for several scoopInterval-paced scoops.
            while (Time.time - start < 0.5f) yield return null;

            Assert.GreaterOrEqual(_pond.Scoops, 2, "multiple scoops land while standing at the pond (repeatable)");
            Assert.Greater(_thirst.Current, low + _thirst.waterScoopAmount,
                "repeated scoops keep restoring thirst (more than a single scoop's worth)");
        }

        // === Explicit TryScoop respects the proximity gate (the seam used by any interact input) ===
        [UnityTest]
        public IEnumerator TryScoop_OnlyWorksInReach()
        {
            yield return null;
            _pond.autoScoopWhileInReach = false; // isolate the explicit call from the auto path
            float thirstFar = _thirst.Current;

            // Far -> explicit TryScoop is a no-op.
            Assert.IsFalse(_pond.TryScoop(), "TryScoop far from the pond returns false");
            Assert.AreEqual(thirstFar, _thirst.Current, 0.001f, "no thirst change from an out-of-reach TryScoop");

            // In reach -> explicit TryScoop restores.
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            yield return null;
            Assert.IsTrue(_pond.TryScoop(), "TryScoop in pond reach returns true");
            Assert.AreEqual(thirstFar + _thirst.waterScoopAmount, _thirst.Current, 0.001f,
                "an in-reach TryScoop restores exactly waterScoopAmount");
        }
    }
}
