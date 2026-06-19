using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the HUNGER need (ticket 86caamkp8).
    ///
    /// Proves the decay actually FIRES through Update over a REAL Time.time window — the complement to
    /// the deterministic EditMode math. This is the load-bearing headless guard: Time.deltaTime ~= 0
    /// per frame in headless runs (unity-conventions.md §headless time), so a deltaTime-based decay
    /// would silently never tick. SurvivalNeed integrates over accumulated Time.time instead; this test
    /// confirms hunger measurably DROPS across a wall-clock window and that the eat-a-berry seam
    /// restores it end-to-end. Mirrors WarmthNeedPlayModeTests.
    /// </summary>
    public class HungerNeedPlayModeTests
    {
        private GameObject _go;
        private HungerNeed _need;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Hunger");
            _need = _go.AddComponent<HungerNeed>();
            _need.max = 100f;
            // Fast decay so the window stays short in CI but still spans real wall-clock seconds
            // (NOT per-frame) — 10/sec means ~2s of window yields an obvious, well-above-noise drop.
            _need.decayPerSecond = 10f;
            _need.floor01 = 0.05f;
            _need.berryRestoreAmount = 18f;
            _need.startFull = true;
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.Destroy(_go);

        [UnityTest]
        public IEnumerator Hunger_Decays_OverARealTimeWindow()
        {
            yield return null; // Start() seeds _current = max + the tick clock
            float initial = _need.Current;
            Assert.AreEqual(100f, initial, 0.5f, "starts full after Start()");

            // Sample over a REAL Time.time window — never per-frame deltas (headless deltaTime~=0).
            float start = Time.time;
            while (Time.time - start < 2f) yield return null;

            float after = _need.Current;
            Assert.Less(after, initial - 5f,
                "hunger must measurably DECAY across a real Time.time window through Update — " +
                "a per-frame-deltaTime decay would never tick headless (deltaTime~=0). " +
                $"initial={initial:0.0} after={after:0.0}");
        }

        [UnityTest]
        public IEnumerator EatBerry_Seam_RestoresHunger_AfterDecay()
        {
            yield return null;

            // Let it decay a real window.
            float start = Time.time;
            while (Time.time - start < 2f) yield return null;
            float decayed = _need.Current;
            Assert.Less(decayed, _need.Max, "hunger dropped below max during the window");

            // The eat-action (86caa5zz3) calls TryEatBerry with an all-or-nothing consume delegate. Here
            // a one-berry store stands in for the inventory consume side — the seam contract is identical.
            int berries = 1;
            Func<bool> consume = () => { if (berries <= 0) return false; berries--; return true; };

            bool ate = _need.TryEatBerry(consume);
            Assert.IsTrue(ate, "eating a held berry succeeds end-to-end");
            Assert.AreEqual(0, berries, "the berry was consumed");
            Assert.AreEqual(decayed + _need.berryRestoreAmount, _need.Current, 0.5f,
                "the eat seam restores exactly the per-berry amount end-to-end (closes the loop)");
        }
    }
}
