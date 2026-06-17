using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the PURE soft-ceiling clamp math (ticket 86caa83wn — the "axe swings into the head
    /// when running" fix). HeldAxeRig.SoftClampMax is the unit-testable core: while the Mixamo RUN/JUMP clip
    /// pumps the right-hand bone UP near the head, the followed hand world-Y must be brought DOWN to (or below)
    /// a shoulder-relative ceiling — but a hand-Y already below the ceiling must be left UNTOUCHED (the
    /// follow-the-arm choice is kept; we only cap the vigorous into-head overshoot).
    ///
    /// This is the BUG-CLASS guard (not the instance): it pins the clamp's contract — never exceeds the
    /// ceiling, leaves sub-ceiling values alone, and eases smoothly through the soft knee (no hard pop that
    /// would read jerky). The PlayMode tests prove the BEHAVIOUR on the real rig; this proves the math.
    /// </summary>
    public class HeldAxeClampTests
    {
        [Test]
        public void SoftClampMax_LeavesValuesWellBelowTheCeiling_Untouched()
        {
            // A hand-Y comfortably below the ceiling (the WALK/IDLE grip, or the lower part of the run swing)
            // must pass through UNCHANGED — the Sponsor's follow-the-arm choice is preserved below the cap.
            const float ceiling = 2.0f, softness = 0.12f;
            foreach (float v in new[] { 0.0f, 1.0f, 1.5f, ceiling - softness - 0.001f })
                Assert.AreEqual(v, HeldAxeRig.SoftClampMax(v, ceiling, softness), 1e-5f,
                    $"a hand-Y ({v}) well below the ceiling ({ceiling}) must be left untouched — the clamp only " +
                    "caps the vigorous into-head overshoot, never the normal grip.");
        }

        [Test]
        public void SoftClampMax_NeverReturnsAboveTheCeiling_EvenForAHandPumpedFarAboveTheHead()
        {
            // The RUN arm-pump lifts the hand far above the shoulder ceiling (toward the head). The clamp must
            // NEVER let the result exceed the ceiling — that is what keeps the axe out of the head.
            const float ceiling = 2.0f, softness = 0.12f;
            foreach (float v in new[] { ceiling, ceiling + 0.1f, ceiling + 0.5f, ceiling + 2.0f, 100f })
                Assert.LessOrEqual(HeldAxeRig.SoftClampMax(v, ceiling, softness), ceiling + 1e-5f,
                    $"a hand-Y ({v}) pumped above the shoulder ceiling ({ceiling}) must be clamped to at most the " +
                    "ceiling — the axe must stay below the head during RUN/JUMP (86caa83wn).");
        }

        [Test]
        public void SoftClampMax_IsMonotonicAndSmooth_NoHardPopAtTheKnee()
        {
            // The soft knee must be C1-smooth: as the raw value rises through the knee the result rises
            // monotonically and continuously toward the ceiling (no discontinuous jump that reads as a pop).
            const float ceiling = 2.0f, softness = 0.12f;
            float prev = HeldAxeRig.SoftClampMax(0f, ceiling, softness);
            for (float v = 0.01f; v <= ceiling + 0.5f; v += 0.01f)
            {
                float r = HeldAxeRig.SoftClampMax(v, ceiling, softness);
                Assert.GreaterOrEqual(r, prev - 1e-6f, $"clamp output must be monotonic non-decreasing (v={v})");
                // The output slope must be <= the input slope (the clamp only ever SLOWS the rise, never speeds it).
                float step = r - prev;
                Assert.LessOrEqual(step, 0.01f + 1e-6f, $"clamp output must not rise faster than its input (v={v})");
                prev = r;
            }
        }

        [Test]
        public void SoftClampMax_HardClampWhenSoftnessZero()
        {
            // softness <= 0 → a plain max-clamp (no knee). Below stays, above snaps to the ceiling exactly.
            const float ceiling = 1.0f;
            Assert.AreEqual(0.5f, HeldAxeRig.SoftClampMax(0.5f, ceiling, 0f), 1e-6f);
            Assert.AreEqual(ceiling, HeldAxeRig.SoftClampMax(5f, ceiling, 0f), 1e-6f);
            Assert.AreEqual(ceiling, HeldAxeRig.SoftClampMax(5f, ceiling, -1f), 1e-6f);
        }
    }
}
