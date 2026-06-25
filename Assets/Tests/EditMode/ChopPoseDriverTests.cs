using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the procedural chop SWING driver (ticket 86caa4c5c AC1) — the pure, time-
    /// independent surface (the frame-progression / mid-swing displacement is covered in PlayMode, since
    /// EditMode can't advance Time.time). Proves:
    ///   • at rest (before any TriggerSwing) SwingNormT clamps to 1 (not swinging) and the authored default
    ///     curves are identity at t=0/t=1 → swingOverrideEuler returns to Vector3.zero (the locked carry pose
    ///     is byte-unchanged; playbook Step 3 gotcha "swingOverrideEuler must be zero at rest");
    ///   • the CHOP arc shape: windup +Z (raise back), strike −Z (swing down), return 0 — the playbook CHOP
    ///     per-verb curve (LOCAL-Z is the rig strike axis);
    ///   • tool-use SPEED scales EffectiveDuration (AC1: faster speed = shorter swing), clamped to a sane band.
    /// </summary>
    public class ChopPoseDriverTests
    {
        private GameObject _go;
        private ChopPoseDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Castaway");
            _go.AddComponent<CastawayArmPose>();
            _driver = _go.AddComponent<ChopPoseDriver>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void AtRest_NotSwinging_SwingNormTClampsToOne()
        {
            // float.NegativeInfinity start (no swing ever fired) → SwingNormT clamps to 1 = at rest.
            Assert.GreaterOrEqual(_driver.SwingNormT, 1f, "before any chop, the driver is at rest (SwingNormT==1)");
            Assert.IsFalse(_driver.IsSwinging, "at rest, IsSwinging is false");
        }

        [Test]
        public void DefaultCurves_AreIdentityAtRest_NonZeroMidStrike()
        {
            // t=0 (rest start) and t=1 (return) must be ~0 on every axis → identity → zero-cost carry pose.
            Assert.AreEqual(0f, _driver.swingX.Evaluate(0f), 1e-3f, "X is 0 at t=0 (rest)");
            Assert.AreEqual(0f, _driver.swingZ.Evaluate(0f), 1e-3f, "Z is 0 at t=0 (rest)");
            Assert.AreEqual(0f, _driver.swingX.Evaluate(1f), 1e-3f, "X returns to 0 at t=1");
            Assert.AreEqual(0f, _driver.swingZ.Evaluate(1f), 1e-3f, "Z returns to 0 at t=1 (carry pose restored)");
            Assert.AreEqual(0f, _driver.swingY.Evaluate(0.5f), 1e-3f, "Y is flat (the rig's useless twist axis)");
        }

        [Test]
        public void DefaultZCurve_IsTheChopArc_WindupPositive_StrikeNegative()
        {
            // The rig raise axis is LOCAL-Z: +Z raises (windup back/up), −Z lowers (the downward strike).
            // Sample the early windup (positive) and the strike peak (strongly negative) per the playbook arc.
            float windup = _driver.swingZ.Evaluate(0.22f);
            float strike = _driver.swingZ.Evaluate(0.55f);
            Assert.Greater(windup, 5f, "the windup raises the arm back/up (+Z) before the strike");
            Assert.Less(strike, -20f, "the strike swings the arm down hard (−Z) — a strong, readable chop");
            Assert.Less(strike, windup, "the strike is a much deeper offset than the windup (a downward arc)");
        }

        [Test]
        public void EffectiveDuration_ScalesInverselyWithSpeed_Clamped()
        {
            _driver.swingDuration = 0.6f;

            _driver.swingSpeed = 1f;
            Assert.AreEqual(0.6f, _driver.EffectiveDuration, 1e-3f, "1x speed = the authored duration");

            _driver.swingSpeed = 2f;
            Assert.AreEqual(0.3f, _driver.EffectiveDuration, 1e-3f, "2x speed = half the duration (faster chop)");

            _driver.swingSpeed = 0.5f;
            Assert.AreEqual(1.2f, _driver.EffectiveDuration, 1e-3f, "0.5x speed = twice the duration (slower)");

            // Out-of-band speeds clamp to the band (a dial can't stall at 0 or blur huge — AC1).
            _driver.swingSpeed = 0f;
            Assert.AreEqual(0.6f / ChopPoseDriver.SwingSpeedMin, _driver.EffectiveDuration, 1e-3f,
                "a 0 speed clamps to SwingSpeedMin (the swing never stalls)");
            _driver.swingSpeed = 999f;
            Assert.AreEqual(0.6f / ChopPoseDriver.SwingSpeedMax, _driver.EffectiveDuration, 1e-3f,
                "a huge speed clamps to SwingSpeedMax (the swing never blurs to nothing)");
        }

        [Test]
        public void SpeedClampBand_MatchesSettingsCatalogToolSpeedBand()
        {
            // The settings panel's tool-use-speed slider band must match the driver's clamp band so the slider
            // can't request a value the driver then clamps away (a confusing dead zone). Keep them in sync.
            Assert.AreEqual(ChopPoseDriver.SwingSpeedMin, FarHorizon.Settings.SettingsCatalog.ToolSpeedMin, 1e-4f,
                "driver SwingSpeedMin == catalog ToolSpeedMin");
            Assert.AreEqual(ChopPoseDriver.SwingSpeedMax, FarHorizon.Settings.SettingsCatalog.ToolSpeedMax, 1e-4f,
                "driver SwingSpeedMax == catalog ToolSpeedMax");
        }
    }
}
