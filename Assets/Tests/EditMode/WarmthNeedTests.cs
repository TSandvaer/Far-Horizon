using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the U2-1 warmth need model (ticket 86ca8bd9m).
    ///
    /// Drives decay DETERMINISTICALLY via WarmthNeed.TickSeconds — no scene/Update/wall-clock — so
    /// the math (decay-over-a-window, floor, clamp, satisfaction hook, Changed event) is proven
    /// fast in headless CI. The COMPLEMENTARY PlayMode test (WarmthNeedPlayModeTests) proves the
    /// same decay actually fires through Update over a REAL Time.time window (Time.deltaTime~=0 trap,
    /// unity-conventions.md). Together they catch the bug CLASS: a decay that integrates wrong,
    /// or one that silently never ticks in a build.
    /// </summary>
    public class WarmthNeedTests
    {
        private GameObject _go;
        private WarmthNeed _need;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("WarmthTest");
            _need = _go.AddComponent<WarmthNeed>();
            // Deterministic config independent of inspector tuning drift.
            _need.max = 100f;
            _need.decayPerSecond = 1f; // 1 warmth/sec -> trivially checkable
            _need.floor01 = 0.1f;      // floor at 10
            _need.criticalThreshold01 = 0.25f;
            _need.startFull = true;
            // Seed _current = max WITHOUT running Start() (EditMode has no lifecycle); do it explicitly.
            _need.AddWarmth(_need.max); // _current starts 0; add max -> 100, also exercises clamp
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void Decay_OverAWindow_ReducesWarmth_AtTheConfiguredRate()
        {
            Assert.AreEqual(100f, _need.Current, 0.001f, "starts full");
            _need.TickSeconds(10f); // 10s * 1/sec = 10 lost
            Assert.AreEqual(90f, _need.Current, 0.001f,
                "warmth must decay decayPerSecond*seconds over a window, not per-frame");
            Assert.AreEqual(0.9f, _need.Current01, 0.001f, "Current01 tracks the raw value normalized");
        }

        [Test]
        public void Decay_StopsAtFloor_NeverBelow()
        {
            _need.TickSeconds(1000f); // way past empty
            Assert.AreEqual(10f, _need.Current, 0.001f,
                "decay must rest at floor01*max (10), a simple floor — NOT a fail-state");
            // Further ticks do nothing.
            _need.TickSeconds(1000f);
            Assert.AreEqual(10f, _need.Current, 0.001f, "floor holds across repeated ticks");
        }

        [Test]
        public void Satisfaction_AddWarmth_RestoresAndClampsToMax()
        {
            _need.TickSeconds(60f); // 100 -> 40
            Assert.AreEqual(40f, _need.Current, 0.001f);

            _need.AddWarmth(30f); // campfire partial warm
            Assert.AreEqual(70f, _need.Current, 0.001f, "AddWarmth raises warmth");

            _need.AddWarmth(9999f); // over-satisfy
            Assert.AreEqual(100f, _need.Current, 0.001f, "AddWarmth clamps to max");
        }

        [Test]
        public void Satisfaction_SatisfyFull_RestoresToMax()
        {
            _need.TickSeconds(80f); // 100 -> 20
            float after = _need.SatisfyFull();
            Assert.AreEqual(100f, _need.Current, 0.001f, "SatisfyFull restores warmth fully (campfire)");
            Assert.AreEqual(1f, after, 0.001f, "SatisfyFull returns Current01 == 1");
        }

        [Test]
        public void Changed_Fires_OnDecayAndSatisfaction_WithCurrent01()
        {
            float last = -1f;
            int count = 0;
            _need.Changed += v => { last = v; count++; };

            _need.TickSeconds(50f); // 100 -> 50
            Assert.AreEqual(0.5f, last, 0.001f, "Changed reports Current01 on decay");
            int afterDecay = count;
            Assert.Greater(afterDecay, 0, "decay must fire Changed (the HUD subscribes, never polls)");

            _need.SatisfyFull(); // 50 -> 100
            Assert.AreEqual(1f, last, 0.001f, "Changed reports Current01 on satisfaction");
            Assert.Greater(count, afterDecay, "satisfaction must fire Changed");
        }

        [Test]
        public void Changed_DoesNotFire_OnNoOpChange()
        {
            _need.SatisfyFull(); // already full -> no value change
            int count = 0;
            _need.Changed += _ => count++;
            _need.AddWarmth(0f);        // no-op
            _need.TickSeconds(0f);      // no-op
            _need.AddWarmth(9999f);     // already at max -> clamps to same value
            Assert.AreEqual(0, count, "Changed must not fire when the clamped value is unchanged");
        }

        [Test]
        public void IsCritical_TracksThreshold()
        {
            Assert.IsFalse(_need.IsCritical, "full warmth is not critical");
            _need.TickSeconds(80f); // 100 -> 20 == 0.2 <= 0.25 threshold
            Assert.IsTrue(_need.IsCritical, "warmth at/below criticalThreshold01 reads critical");
        }
    }
}
