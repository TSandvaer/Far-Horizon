using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the per-need ON/OFF decay toggle on the SurvivalNeed base (ticket 86cabeqwf —
    /// the dev-tweak-console per-need on/off). The toggle drives <see cref="SurvivalNeed.decayEnabled"/>;
    /// this proves the FIELD actually GATES the decay path (the bug CLASS a "field set but not read" dead
    /// toggle would be — cf. the #218 AC6 dead-knob pattern). Drives decay deterministically via TickSeconds
    /// (no scene/Update/wall-clock — headless Time.deltaTime~=0 trap, unity-conventions.md).
    ///
    /// Uses WarmthNeed as a concrete SurvivalNeed (the base is abstract). The gate lives on the base, so the
    /// same behavior holds for HungerNeed / ThirstNeed — the catalog test drives all three fields, this proves
    /// the mechanism once at the base.
    /// </summary>
    public class SurvivalNeedDecayToggleTests
    {
        private GameObject _go;
        private WarmthNeed _need;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("NeedDecayToggleTest");
            _need = _go.AddComponent<WarmthNeed>();
            _need.max = 100f;
            _need.decayPerSecond = 1f; // 1/sec -> trivially checkable
            _need.floor01 = 0.05f;     // floor at 5, well below the values we tick through
            _need.startFull = true;
            _need.AddWarmth(_need.max); // seed _current = 100 without a Start() lifecycle
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void DecayEnabled_DefaultsTrue()
        {
            Assert.IsTrue(_need.decayEnabled,
                "a need decays by default (the toggle ships ON — a fresh castaway's needs fall)");
        }

        [Test]
        public void ToggleOff_HaltsDecay_BarHolds()
        {
            _need.decayEnabled = false;
            _need.TickSeconds(10f); // would drain 10 if decay ran
            Assert.AreEqual(100f, _need.Current, 0.001f,
                "with the per-need toggle OFF, TickSeconds must NOT drain the bar (86cabeqwf on/off)");
        }

        [Test]
        public void ToggleOn_ResumesDecay_FromCurrentValue_NoCatchUp()
        {
            // Pause across a long window, then resume — the paused window must NOT be applied as catch-up.
            _need.decayEnabled = false;
            _need.TickSeconds(100f); // paused: no drain
            Assert.AreEqual(100f, _need.Current, 0.001f, "paused window drains nothing");

            _need.decayEnabled = true;
            _need.TickSeconds(10f); // 10s * 1/sec = 10 lost from the CURRENT (still-full) value
            Assert.AreEqual(90f, _need.Current, 0.001f,
                "re-enabling resumes decay from the CURRENT value with no catch-up for the paused window");
        }

        [Test]
        public void ToggleOff_DoesNotBlockSatisfaction()
        {
            // Off HALTS decay only — a satisfaction hook (campfire/eat/drink) must still raise the bar.
            _need.decayEnabled = false;
            _need.AddWarmth(-30f); // drop to 70 via the satisfy primitive (negative amount)
            Assert.AreEqual(70f, _need.Current, 0.001f, "sanity: value dropped to 70");
            _need.AddWarmth(20f);  // a satisfaction still applies while decay is paused
            Assert.AreEqual(90f, _need.Current, 0.001f,
                "the on/off toggle gates DECAY only — satisfaction hooks still write while decay is paused");
        }
    }
}
