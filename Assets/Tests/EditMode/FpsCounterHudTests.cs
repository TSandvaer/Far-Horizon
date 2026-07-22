using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Guards for the FPS counter's measurement seam + 2Hz label-cache behavior (ticket 86cahmxmt).
    ///
    /// The counter's whole perf contract is ZERO per-frame GC (the C2a IMGUI discipline, 86cahhfp4):
    /// the label string may only be rebuilt when a 0.5s meter window PUBLISHES (≤2Hz) AND the rounded
    /// readout actually changed. These tests pin that contract at the seam: FpsMeter is pure C# driven
    /// with synthetic deltas (binary-exact 1/16s + 1/8s steps so window boundaries are deterministic —
    /// no float-accumulation flake), and the HUD's cached label is asserted REFERENCE-stable across
    /// sub-window ticks and across publishes whose numbers did not move (a rebuilt-but-equal string
    /// would pass AreEqual and hide the per-frame alloc — ReferenceEquals is the no-alloc proof).
    ///
    /// Headless guard: dt &lt;= 0 must never accumulate or publish (headless runs see Time.deltaTime ≈ 0,
    /// unity-conventions §Headless rituals — a 0-length window would divide by zero).
    /// </summary>
    public class FpsCounterHudTests
    {
        // Binary-exact frame deltas: 1/16 and 1/8 are powers of two, so repeated float addition is EXACT
        // and the 0.5s window closes on a deterministic tick (no 0.4999999-vs-0.5000001 flake).
        private const float Dt16Fps = 0.0625f; // 8 ticks  = exactly 0.5s → Current = 16
        private const float Dt8Fps  = 0.125f;  // 4 ticks  = exactly 0.5s → Current = 8

        private GameObject _go;
        private FpsCounterHud _hud;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("FpsCounterHudTests");
            _hud = _go.AddComponent<FpsCounterHud>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ----- FpsMeter: cadence + math (the pure seam) -----

        [Test]
        public void Meter_PublishesExactlyOncePerHalfSecondWindow_At2Hz()
        {
            var m = new FpsMeter();

            // 7 ticks × 1/16s = 0.4375s — the window is still open; NO publish (the ≤2Hz contract).
            for (int i = 0; i < 7; i++)
                Assert.IsFalse(m.Tick(Dt16Fps), $"tick {i + 1}/7 (0.4375s accumulated) must not publish yet");
            Assert.IsFalse(m.HasSample, "no sample before the first 0.5s window closes");

            // The 8th tick lands the window on exactly 0.5s → ONE publish.
            Assert.IsTrue(m.Tick(Dt16Fps), "the 8th 1/16s tick closes the 0.5s window → publish");
            Assert.AreEqual(16f, m.Current, 1e-4f, "Current = 8 frames / 0.5s = 16 fps");
            Assert.AreEqual(16f, m.Average, 1e-4f, "first window → Average == Current");
            Assert.IsTrue(m.HasSample, "HasSample flips on the first publish");

            // The very next tick opens a FRESH window (the accumulator reset) — no publish.
            Assert.IsFalse(m.Tick(Dt16Fps), "the tick after a publish starts a fresh window — no publish");
        }

        [Test]
        public void Meter_RollingAverage_MeansTheLastWindows()
        {
            var m = new FpsMeter(); // default: 10-window (5s) rolling average

            for (int i = 0; i < 8; i++) m.Tick(Dt16Fps); // window 1 → 16 fps
            for (int i = 0; i < 4; i++) m.Tick(Dt8Fps);  // window 2 → 8 fps

            Assert.AreEqual(8f, m.Current, 1e-4f, "Current reflects the LAST window (4 frames / 0.5s)");
            Assert.AreEqual(12f, m.Average, 1e-4f, "Average = mean(16, 8) = 12 across the two windows");
        }

        [Test]
        public void Meter_RingWraps_EvictingTheOldestWindow()
        {
            var m = new FpsMeter(averageWindows: 2); // tiny ring so eviction is observable

            for (int i = 0; i < 8; i++) m.Tick(Dt16Fps); // window 1 → 16
            for (int i = 0; i < 4; i++) m.Tick(Dt8Fps);  // window 2 → 8   (avg 12)
            Assert.AreEqual(12f, m.Average, 1e-4f, "two windows in the 2-ring: mean(16, 8) = 12");

            for (int i = 0; i < 4; i++) m.Tick(Dt8Fps);  // window 3 → 8 — evicts the 16
            Assert.AreEqual(8f, m.Average, 1e-4f, "the 2-window ring evicted the 16: mean(8, 8) = 8");
        }

        [Test]
        public void Meter_IgnoresZeroAndNegativeDeltas_TheHeadlessGuard()
        {
            var m = new FpsMeter();

            // Headless runs see Time.deltaTime ≈ 0 per frame — the meter must never accumulate a 0-length
            // window (divide-by-zero) or publish nonsense off it.
            for (int i = 0; i < 1000; i++)
                Assert.IsFalse(m.Tick(0f), "a dt=0 tick must be ignored (headless Time.deltaTime≈0 trap)");
            Assert.IsFalse(m.Tick(-0.1f), "a negative dt must be ignored");
            Assert.IsFalse(m.HasSample, "no amount of dt<=0 ticks may produce a sample");
            Assert.AreEqual(0f, m.Current, "Current stays 0 with no real frames");

            // Real frames after the dead ticks behave normally (the dead ticks polluted nothing).
            for (int i = 0; i < 8; i++) m.Tick(Dt16Fps);
            Assert.AreEqual(16f, m.Current, 1e-4f, "real ticks after ignored dt<=0 ticks publish correctly");
        }

        // ----- FpsCounterHud: the 2Hz label cache (the no-per-frame-alloc contract) -----

        [Test]
        public void Label_StartsAsThePlaceholder_UntilTheFirstWindowPublishes()
        {
            Assert.AreEqual(FpsCounterHud.PlaceholderLabel, _hud.Label, "placeholder until the first sample");

            for (int i = 0; i < 7; i++) _hud.Step(Dt16Fps); // window still open
            Assert.AreSame(FpsCounterHud.PlaceholderLabel, _hud.Label,
                "sub-window steps must not touch the label (the SAME string instance — no rebuild)");

            _hud.Step(Dt16Fps); // 8th tick → first publish
            Assert.AreEqual("FPS 16 | avg 16", _hud.Label, "first publish renders current + rolling average");
        }

        [Test]
        public void Label_IsReferenceStable_BetweenPublishes_AndAcrossUnchangedPublishes()
        {
            // First publish: 16 fps.
            for (int i = 0; i < 8; i++) _hud.Step(Dt16Fps);
            string afterFirstPublish = _hud.Label;

            // Sub-window ticks: the label must be the SAME INSTANCE (per-frame path allocates nothing —
            // AreEqual would pass on a rebuilt-but-equal string and hide the alloc; AreSame is the proof).
            for (int i = 0; i < 7; i++) _hud.Step(Dt16Fps);
            Assert.AreSame(afterFirstPublish, _hud.Label, "no label rebuild between publishes (2Hz cache)");

            // Second publish at the SAME rate: the rounded readout did not change → still the same instance
            // (publishing alone must not churn a fresh-but-identical string).
            _hud.Step(Dt16Fps);
            Assert.AreSame(afterFirstPublish, _hud.Label,
                "a publish whose rounded values are unchanged must keep the cached string (no churn)");
        }

        [Test]
        public void Label_Rebuilds_WhenAPublishChangesTheRoundedReadout()
        {
            for (int i = 0; i < 8; i++) _hud.Step(Dt16Fps); // window 1 → 16 (avg 16)
            for (int i = 0; i < 8; i++) _hud.Step(Dt16Fps); // window 2 → 16 (avg 16, no rebuild)
            string steady = _hud.Label;

            for (int i = 0; i < 4; i++) _hud.Step(Dt8Fps);  // window 3 → 8; avg = mean(16,16,8) = 13.33 → 13
            Assert.AreNotSame(steady, _hud.Label, "a changed readout rebuilds the cached label");
            Assert.AreEqual("FPS 8 | avg 13", _hud.Label,
                "current = the last window (8); avg = RoundToInt(mean(16, 16, 8)) = 13");
        }

        // ----- C2a: the IMGUI Layout-pass opt-out (86cahhfp4 discipline for every OnGUI component) -----

        [Test]
        public void Awake_OptsOutOfTheImguiLayoutPass()
        {
            // Awake does not auto-run in EditMode — reflection-invoke it (the ImguiLayoutPassTests /
            // HeldToolRigTests precedent). The HUD draws with explicit Rects only, so the Layout event
            // pass is pure per-frame waste it must opt out of.
            MethodInfo awake = typeof(FpsCounterHud).GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(awake, "FpsCounterHud must declare Awake (the C2a opt-out site)");
            awake.Invoke(_hud, null);

            Assert.IsFalse(_hud.useGUILayout,
                "FpsCounterHud must set useGUILayout = false in Awake (C2a, 86cahhfp4 — explicit Rects only)");
        }
    }
}
