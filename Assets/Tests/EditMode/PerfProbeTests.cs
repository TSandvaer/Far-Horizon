using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode pins for PerfProbe's pure seams (ticket 86cahhfp4, S2a). The probe itself is a runtime
    /// instrument (ProfilerRecorder sampling + a WasdMovement-driven island walk — shipped-exe territory,
    /// verified by the capture runs on the -development build); what EditMode CAN pin is the launch-flag
    /// parse (the inert-by-default contract — the probe must NEVER arm in a normal launch) and the walk
    /// heading math (always unit length, forward at t=0 — a broken heading silently turns the "walking
    /// capture" into a stationary one, the run-1 lesson generalized: instruments must fail loud).
    /// </summary>
    public class PerfProbeTests
    {
        [Test]
        public void WantsProbe_WithoutFlag_False_TheProbeIsInertByDefault()
        {
            string[] normalLaunch = { "FarHorizon.exe" };
            Assert.IsFalse(PerfProbe.WantsProbe(normalLaunch),
                "a normal launch must NOT arm the probe — it is a diagnosis instrument, not shipped behavior");
            Assert.IsFalse(PerfProbe.WantsProbe(null), "null args must resolve to inert, not throw");
        }

        [Test]
        public void WantsProbe_WithFlag_True()
        {
            string[] probeLaunch = { "FarHorizon.exe", PerfProbe.Arg, "-logFile", "x.log" };
            Assert.IsTrue(PerfProbe.WantsProbe(probeLaunch),
                "-perfProbe on the command line must arm the probe (the S2a capture entry point)");
        }

        [Test]
        public void WalkHeading_StartsForward_AndStaysUnitLength()
        {
            Vector2 start = PerfProbe.WalkHeading(0f);
            Assert.That(start.x, Is.EqualTo(0f).Within(1e-4f), "t=0 heading must be straight ahead (x=0)");
            Assert.That(start.y, Is.EqualTo(1f).Within(1e-4f), "t=0 heading must be straight ahead (y=1)");

            // Unit length across the sample window — the override must always read as a fully-held key
            // (a shrinking magnitude would walk slower and sample a different load than the plan asks for).
            for (float t = 0f; t <= PerfProbe.SampleSeconds; t += 2.5f)
                Assert.That(PerfProbe.WalkHeading(t).magnitude, Is.EqualTo(1f).Within(1e-4f),
                    $"heading at t={t} must be unit length");
        }

        [Test]
        public void WalkHeading_SweepsAnArc_SoTheCaptureCrossesMultipleViewLoads()
        {
            // Over the 20s window at one revolution per 60s the heading sweeps ~120° — the t=SampleSeconds
            // heading must differ clearly from the t=0 heading (a constant heading = one straight line =
            // a single view load, not the forest/coast/interior sweep the capture is for).
            float dot = Vector2.Dot(PerfProbe.WalkHeading(0f), PerfProbe.WalkHeading(PerfProbe.SampleSeconds));
            Assert.Less(dot, 0.5f,
                "the heading must sweep a substantial arc over the sample window (~120° for 20s/60s rev)");
        }
    }
}
