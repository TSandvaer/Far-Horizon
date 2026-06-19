using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the THIRST need (ticket 86caamkv7).
    ///
    /// Proves the decay actually FIRES through Update over a REAL Time.time window — the complement to
    /// the deterministic EditMode math. This is the load-bearing headless guard: Time.deltaTime ~= 0
    /// per frame in headless runs (unity-conventions.md §headless time), so a deltaTime-based decay
    /// would silently never tick. SurvivalNeed integrates over accumulated Time.time instead; this test
    /// confirms thirst measurably DROPS across a wall-clock window and that the drink-from-hand seam
    /// restores it end-to-end. Mirrors HungerNeedPlayModeTests.
    /// </summary>
    public class ThirstNeedPlayModeTests
    {
        private GameObject _go;
        private ThirstNeed _need;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Thirst");
            _need = _go.AddComponent<ThirstNeed>();
            _need.max = 100f;
            // Fast decay so the window stays short in CI but still spans real wall-clock seconds
            // (NOT per-frame) — 10/sec means ~2s of window yields an obvious, well-above-noise drop.
            _need.decayPerSecond = 10f;
            _need.floor01 = 0.05f;
            _need.waterScoopAmount = 12f;
            _need.startFull = true;
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.Destroy(_go);

        [UnityTest]
        public IEnumerator Thirst_Decays_OverARealTimeWindow()
        {
            yield return null; // Start() seeds _current = max + the tick clock
            float initial = _need.Current;
            Assert.AreEqual(100f, initial, 0.5f, "starts full after Start()");

            // Sample over a REAL Time.time window — never per-frame deltas (headless deltaTime~=0).
            float start = Time.time;
            while (Time.time - start < 2f) yield return null;

            float after = _need.Current;
            Assert.Less(after, initial - 5f,
                "thirst must measurably DECAY across a real Time.time window through Update — " +
                "a per-frame-deltaTime decay would never tick headless (deltaTime~=0). " +
                $"initial={initial:0.0} after={after:0.0}");
        }

        [UnityTest]
        public IEnumerator Drink_Seam_RestoresThirst_AfterDecay()
        {
            yield return null;

            // Let it decay a real window.
            float start = Time.time;
            while (Time.time - start < 2f) yield return null;
            float decayed = _need.Current;
            Assert.Less(decayed, _need.Max, "thirst dropped below max during the window");

            // The drink-action (PondDrink) calls TryDrink with the in-reach predicate. Here we pass true
            // (the castaway is at the pond) — the seam contract is identical to the live interaction.
            bool drank = _need.TryDrink(true);
            Assert.IsTrue(drank, "drinking in pond reach succeeds end-to-end");
            Assert.AreEqual(decayed + _need.waterScoopAmount, _need.Current, 0.5f,
                "the drink seam restores exactly the per-scoop amount end-to-end (closes the loop)");
        }
    }
}
