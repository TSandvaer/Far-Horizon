using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the U2-1 warmth need (ticket 86ca8bd9m).
    ///
    /// Proves the decay actually FIRES through Update over a REAL Time.time window — the
    /// complement to the deterministic EditMode math. This is the load-bearing headless guard:
    /// Time.deltaTime ~= 0 per frame in headless runs (unity-conventions.md §headless time), so a
    /// deltaTime-based decay would silently never tick. WarmthNeed integrates over accumulated
    /// Time.time instead; this test confirms warmth measurably DROPS across a wall-clock window and
    /// that the campfire's satisfaction hook restores it end-to-end.
    /// </summary>
    public class WarmthNeedPlayModeTests
    {
        private GameObject _go;
        private WarmthNeed _need;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Warmth");
            _need = _go.AddComponent<WarmthNeed>();
            _need.max = 100f;
            // Fast decay so the window stays short in CI but still spans real wall-clock seconds
            // (NOT per-frame) — 10/sec means ~2s of window yields an obvious, well-above-noise drop.
            _need.decayPerSecond = 10f;
            _need.floor01 = 0.05f;
            _need.startFull = true;
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_go);

        [UnityTest]
        public IEnumerator Warmth_Decays_OverARealTimeWindow()
        {
            yield return null; // Start() seeds _current = max + the tick clock
            float initial = _need.Current;
            Assert.AreEqual(100f, initial, 0.5f, "starts full after Start()");

            // Sample over a REAL Time.time window — never per-frame deltas (headless deltaTime~=0).
            float start = Time.time;
            while (Time.time - start < 2f) yield return null;

            float after = _need.Current;
            Assert.Less(after, initial - 5f,
                "warmth must measurably DECAY across a real Time.time window through Update — " +
                "a per-frame-deltaTime decay would never tick headless (deltaTime~=0). " +
                $"initial={initial:0.0} after={after:0.0}");
        }

        [UnityTest]
        public IEnumerator Campfire_SatisfactionHook_RestoresWarmth_AfterDecay()
        {
            yield return null;

            // Let it decay a real window.
            float start = Time.time;
            while (Time.time - start < 2f) yield return null;
            float decayed = _need.Current;
            Assert.Less(decayed, _need.Max, "warmth dropped below max during the window");

            // The campfire (U2-4) calls SatisfyFull — the satisfaction hook this ticket exposes.
            float after = _need.SatisfyFull();
            Assert.AreEqual(1f, after, 0.001f, "satisfaction hook returns full Current01");
            Assert.AreEqual(_need.Max, _need.Current, 0.001f,
                "the campfire's satisfaction hook restores warmth to max end-to-end (closes the loop)");
        }
    }
}
