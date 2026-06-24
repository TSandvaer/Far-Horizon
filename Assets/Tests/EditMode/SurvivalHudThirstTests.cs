using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the THIRST bar's presentation math + the shared critical-glyph pulse
    /// (ticket 86caamkxv — the third need bar that makes the DRINK loop VERIFIABLE: the player SEES
    /// thirst deplete + refill on drinking at the pond).
    ///
    /// The thirst bar reuses the warmth/hunger pinned FLOOR segment-fill rule (SurvivalHud.FilledSegments)
    /// so all three bars read consistently; what differs is the PALETTE — a water-blue ramp
    /// (stream-blue -> pale-teal -> dry grey-blue) via SurvivalHud.ThirstBandColor, with the SAME band
    /// cutoffs as warmth/hunger. These assert the thirst band mapping the same way SurvivalHudHungerTests
    /// asserts hunger's, AND the load-bearing design invariants from Uma's spec (hud-three-bar-spec.md):
    ///   - the three palettes are pairwise DISTINCT at every fill (gold ≠ green ≠ blue — tell-apart-at-a-glance);
    ///   - thirst is the ONE COOL note (blue channel dominates at the slaked band) — a soak retune of the
    ///     hexes (Uma §6 Q2) must not silently warm-shift thirst into hunger's space without this flagging it;
    ///   - HDR-clamp safety (all sub-1.0, parched is dusty grey-blue, never an alarm red);
    ///   - the shared IsCritical glyph-pulse seam (GlyphPulseAlpha) — glyph-only, one phase clock (Uma §4).
    /// The COMPLEMENTARY PlayMode test (SurvivalHudPlayModeTests) proves the bar reflects LIVE thirst.
    /// </summary>
    public class SurvivalHudThirstTests
    {
        private static readonly Color StreamBlue  = new Color(0.24f, 0.56f, 0.77f); // #3E8FC4 slaked
        private static readonly Color PaleTeal    = new Color(0.37f, 0.66f, 0.69f); // #5FA9B0 dry-ish
        private static readonly Color DryGreyBlue = new Color(0.43f, 0.54f, 0.61f); // #6E8A9C parched

        // === The thirst bar shares warmth/hunger's pinned FLOOR fill rule (one bar idiom) ============
        [TestCase(0.00f, 0)]
        [TestCase(0.09f, 0)]
        [TestCase(0.10f, 1)]
        [TestCase(0.50f, 5)]   // thirst ships at the 0.50 seed -> ~5 of 10 segments at spawn
        [TestCase(0.99f, 9)]
        [TestCase(1.00f, 10)]
        public void ThirstBar_UsesTheSharedFloorFillRule(float current01, int expectedLit)
        {
            Assert.AreEqual(expectedLit, SurvivalHud.FilledSegments(current01),
                $"the thirst bar shares the pinned FLOOR fill rule — FilledSegments({current01}) = {expectedLit}");
        }

        // === Thirst band mapping: stream-blue >=0.60, pale teal 0.30..0.60, dry grey-blue <0.30 ======
        [TestCase(1.00f)]
        [TestCase(0.60f)]   // band boundary: >= 0.60 is slaked
        public void ThirstBandColor_SlakedBand_IsStreamBlue(float c)
        {
            AssertColor(StreamBlue, SurvivalHud.ThirstBandColor(c), $"thirst {c} must read bright stream-blue (slaked)");
        }

        [TestCase(0.59f)]
        [TestCase(0.30f)]   // band boundary: >= 0.30 (and < 0.60) is dry-ish
        public void ThirstBandColor_DryIshBand_IsPaleTeal(float c)
        {
            AssertColor(PaleTeal, SurvivalHud.ThirstBandColor(c), $"thirst {c} must read pale teal (dry-ish)");
        }

        [TestCase(0.29f)]
        [TestCase(0.00f)]   // parched band: < 0.30 is dry grey-blue
        public void ThirstBandColor_ParchedBand_IsDryGreyBlue(float c)
        {
            AssertColor(DryGreyBlue, SurvivalHud.ThirstBandColor(c),
                $"thirst {c} must read dry grey-blue (parched — the dry-throat ache, NEVER an alarm hue)");
        }

        // === Monotonic: more thirst-fill => more lit segments (empties right-to-left like the others) ==
        [Test]
        public void ThirstBar_FillIsMonotonic()
        {
            int prev = -1;
            for (int i = 0; i <= 20; i++)
            {
                int lit = SurvivalHud.FilledSegments(i / 20f);
                Assert.GreaterOrEqual(lit, prev, "thirst fill must never DECREASE as the need rises");
                prev = lit;
            }
        }

        // === Thirst is the ONE COOL note (Uma §2.1 load-bearing call): blue channel dominates at slaked ==
        // This pins the cool-relative-to-warm invariant (NOT a hex lock — a soak MAY retune the exact hexes,
        // Uma §6 Q2) so a retune can't silently warm-shift thirst into hunger's gold/green space.
        [TestCase(1.00f)]
        [TestCase(0.60f)]
        public void ThirstBand_IsTheCoolNote_BlueChannelDominates(float c)
        {
            Color t = SurvivalHud.ThirstBandColor(c);
            Assert.Greater(t.b, t.r, $"thirst {c} must be the COOL note — blue > red (b={t.b} r={t.r})");
            Assert.Greater(t.b, t.g, $"thirst {c} must be the COOL note — blue > green (b={t.b} g={t.g})");
        }

        // === The three palettes are PAIRWISE DISTINCT at every level (tell-apart-at-a-glance, AC2) ======
        // Catches the copy-paste palette bug (thirst accidentally reusing the hunger ramp) — the headline
        // distinctness trap in the QA plan.
        [Test]
        public void ThreeBands_ArePairwiseDistinctAtEveryLevel()
        {
            foreach (float c in new[] { 1f, 0.6f, 0.45f, 0.2f, 0f })
            {
                Color warmth = SurvivalHud.BandColor(c);
                Color hunger = SurvivalHud.HungerBandColor(c);
                Color thirst = SurvivalHud.ThirstBandColor(c);
                AssertDistinct(warmth, hunger, c, "warmth", "hunger");
                AssertDistinct(warmth, thirst, c, "warmth", "thirst");
                AssertDistinct(hunger, thirst, c, "hunger", "thirst");
            }
        }

        // === HDR-clamp-safe: every thirst band channel sub-1.0, and parched is NOT a saturated alarm hue ==
        [Test]
        public void ThirstBandColors_AreAllSubOne_HdrClampSafe()
        {
            foreach (float c in new[] { 1f, 0.5f, 0.1f })
            {
                Color col = SurvivalHud.ThirstBandColor(c);
                Assert.LessOrEqual(col.r, 1f, "R channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.LessOrEqual(col.g, 1f, "G channel must be sub-1.0 (HDR-clamp-safe)");
                Assert.LessOrEqual(col.b, 1f, "B channel must be sub-1.0 (HDR-clamp-safe)");
                // No pure-saturated alarm red: the parched state desaturates toward grey-blue, never #FF0000.
                Assert.Less(col.r, 0.95f, "no pure-saturated alarm red — parched thirst is a dusty grey-blue");
            }
        }

        // === The shared critical-glyph pulse seam (Uma §4 — consistent IsCritical treatment, AC2) ========
        [Test]
        public void GlyphPulse_NotCritical_IsFullAlpha()
        {
            // Non-critical -> no pulse, alpha multiplier is exactly 1.0 at any time (the shipped glyph alpha
            // is unchanged: filled>0 ? 1 : 0.4). This is the byte-identical guarantee for the non-critical path.
            for (float t = 0f; t < 3f; t += 0.31f)
                Assert.AreEqual(1f, SurvivalHud.GlyphPulseAlpha(false, t), 0.00001f,
                    $"a non-critical need's glyph never pulses — alpha multiplier == 1.0 at t={t}");
        }

        [Test]
        public void GlyphPulse_Critical_OscillatesWithinExpectedRange()
        {
            float min = float.MaxValue, max = float.MinValue;
            for (float t = 0f; t < 4f; t += 0.05f)
            {
                float a = SurvivalHud.GlyphPulseAlpha(true, t);
                Assert.GreaterOrEqual(a, 0.55f - 0.001f, $"critical pulse must not dip below ~0.55 (t={t}, a={a})");
                Assert.LessOrEqual(a, 1f + 0.001f, $"critical pulse must not exceed 1.0 (t={t}, a={a})");
                min = Mathf.Min(min, a);
                max = Mathf.Max(max, a);
            }
            // It's a real BREATHE, not a static value: the range spans most of [0.55, 1.0].
            Assert.Less(min, 0.60f, "the critical breathe must reach near its min (~0.55) — it's a real pulse");
            Assert.Greater(max, 0.95f, "the critical breathe must reach near its max (~1.0) — it's a real pulse");
        }

        [Test]
        public void GlyphPulse_Critical_SharesOnePhaseClock_AcrossNeeds()
        {
            // Two/three simultaneously-critical glyphs breathe in SYNC (one shared phase clock) so the corner
            // pulses as one calm body, not competing blinkers (Uma §4). At any given time the pulse alpha is
            // identical for every critical need (the function is a pure function of time + isCritical).
            foreach (float t in new[] { 0.13f, 0.77f, 1.41f, 2.5f })
            {
                float a1 = SurvivalHud.GlyphPulseAlpha(true, t);
                float a2 = SurvivalHud.GlyphPulseAlpha(true, t);
                Assert.AreEqual(a1, a2, 0.00001f,
                    $"all critical glyphs share ONE phase clock — same time t={t} -> same pulse alpha");
            }
        }

        private static void AssertDistinct(Color a, Color b, float fill, string nameA, string nameB)
        {
            bool same = Mathf.Approximately(a.r, b.r)
                     && Mathf.Approximately(a.g, b.g)
                     && Mathf.Approximately(a.b, b.b);
            Assert.IsFalse(same,
                $"at fill {fill} the {nameA} and {nameB} bands must be visually distinct (tell-apart-at-a-glance)");
        }

        private static void AssertColor(Color expected, Color actual, string msg)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f, msg + " (R)");
            Assert.AreEqual(expected.g, actual.g, 0.001f, msg + " (G)");
            Assert.AreEqual(expected.b, actual.b, 0.001f, msg + " (B)");
        }
    }
}
