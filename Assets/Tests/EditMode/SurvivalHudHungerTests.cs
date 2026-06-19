using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the HUNGER bar's presentation math (ticket 86caa5zz3 / #101 — folds in part of
    /// the need-meter HUD 86caamkxv: the hunger bar that makes the eat loop VERIFIABLE).
    ///
    /// The hunger bar reuses the warmth bar's pinned FLOOR segment-fill rule (SurvivalHud.FilledSegments) so
    /// the two bars read consistently; what differs is the PALETTE — a distinct food/fruit ramp
    /// (fed-green -> ripe-amber -> hungry berry-red) via SurvivalHud.HungerBandColor, with the SAME band
    /// cutoffs as warmth. These tests assert the hunger band mapping the same way SurvivalHudTests asserts
    /// warmth's, AND that the two palettes are DISTINCT (so a player tells hunger vs warmth apart — #101 AC2).
    /// The COMPLEMENTARY PlayMode test (SurvivalHudPlayModeTests) proves the bar reflects LIVE hunger.
    /// </summary>
    public class SurvivalHudHungerTests
    {
        private static readonly Color FedGreen  = new Color(0.55f, 0.72f, 0.36f);
        private static readonly Color RipeAmber = new Color(0.85f, 0.62f, 0.30f);
        private static readonly Color BerryRed  = new Color(0.74f, 0.30f, 0.30f);

        // === The hunger bar shares warmth's pinned FLOOR fill rule (one bar idiom) ===================
        [TestCase(0.00f, 0)]
        [TestCase(0.09f, 0)]
        [TestCase(0.10f, 1)]
        [TestCase(0.55f, 5)]
        [TestCase(0.99f, 9)]
        [TestCase(1.00f, 10)]
        public void HungerBar_UsesTheSharedFloorFillRule(float current01, int expectedLit)
        {
            Assert.AreEqual(expectedLit, SurvivalHud.FilledSegments(current01),
                $"the hunger bar shares the pinned FLOOR fill rule — FilledSegments({current01}) = {expectedLit}");
        }

        // === Hunger band mapping: fed green >=0.60, ripe amber 0.30..0.60, hungry berry-red <0.30 ====
        [TestCase(1.00f)]
        [TestCase(0.60f)]   // band boundary: >= 0.60 is fed
        public void HungerBandColor_FedBand_IsGreen(float c)
        {
            AssertColor(FedGreen, SurvivalHud.HungerBandColor(c), $"hunger {c} must read fed green");
        }

        [TestCase(0.59f)]
        [TestCase(0.30f)]   // band boundary: >= 0.30 (and < 0.60) is peckish
        public void HungerBandColor_PeckishBand_IsRipeAmber(float c)
        {
            AssertColor(RipeAmber, SurvivalHud.HungerBandColor(c), $"hunger {c} must read ripe amber (peckish)");
        }

        [TestCase(0.29f)]
        [TestCase(0.00f)]   // hungry band: < 0.30 is berry-red
        public void HungerBandColor_HungryBand_IsBerryRed(float c)
        {
            AssertColor(BerryRed, SurvivalHud.HungerBandColor(c), $"hunger {c} must read hungry berry-red");
        }

        // === Monotonic: more hunger-fill => more lit segments (empties right-to-left like warmth) =====
        [Test]
        public void HungerBar_FillIsMonotonic()
        {
            int prev = -1;
            for (int i = 0; i <= 20; i++)
            {
                int lit = SurvivalHud.FilledSegments(i / 20f);
                Assert.GreaterOrEqual(lit, prev, "hunger fill must never DECREASE as the need rises");
                prev = lit;
            }
        }

        // === The two palettes are DISTINCT so the player tells hunger vs warmth apart (#101 AC2) =======
        [Test]
        public void HungerAndWarmthBands_AreDistinctAtEveryLevel()
        {
            foreach (float c in new[] { 1f, 0.6f, 0.45f, 0.2f, 0f })
            {
                Color hunger = SurvivalHud.HungerBandColor(c);
                Color warmth = SurvivalHud.BandColor(c);
                bool same = Mathf.Approximately(hunger.r, warmth.r)
                         && Mathf.Approximately(hunger.g, warmth.g)
                         && Mathf.Approximately(hunger.b, warmth.b);
                Assert.IsFalse(same,
                    $"at fill {c} the hunger and warmth bands must be visually distinct (tell-apart-at-a-glance)");
            }
        }

        // === HDR-clamp-safe: every hunger band channel is sub-1.0, and the hungry state is NOT alarm red =
        [Test]
        public void HungerBandColors_AreAllSubOne_HdrClampSafe()
        {
            foreach (float c in new[] { 1f, 0.5f, 0.1f })
            {
                Color col = SurvivalHud.HungerBandColor(c);
                Assert.LessOrEqual(col.r, 1f, "R channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.LessOrEqual(col.g, 1f, "G channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.LessOrEqual(col.b, 1f, "B channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.Less(col.r, 0.95f, "no pure-saturated alarm red — the hungry state is a muted berry red");
            }
        }

        private static void AssertColor(Color expected, Color actual, string msg)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f, msg + " (R)");
            Assert.AreEqual(expected.g, actual.g, 0.001f, msg + " (G)");
            Assert.AreEqual(expected.b, actual.b, 0.001f, msg + " (B)");
        }
    }
}
