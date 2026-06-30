using NUnit.Framework;
using UnityEngine;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC4 regression guard for the dev-console PANEL POSITION (ticket 86cabeqj9). The corner enum + the
    /// PlayerPrefs load/save are pure C# (the VisualElement Apply is exercised by the shipped-build capture,
    /// not headlessly — UI Toolkit layout is unreliable in EditMode). Pins: the DEFAULT corner is OFF the
    /// player (never screen-center), the chosen corner PERSISTS across runs, and the cycle order is stable.
    /// </summary>
    public class ConsolePositionTests
    {
        [SetUp]
        [TearDown]
        public void ClearPref() => PlayerPrefs.DeleteKey(ConsolePosition.PrefsKey);

        [Test]
        public void Default_IsACorner_NotScreenCenter()
        {
            // AC4: "Default position is a corner that does NOT cover the player (not screen-center)."
            Assert.AreEqual(ConsoleCorner.TopLeft, ConsolePosition.Default,
                "the default console corner is top-left — off the player, never centered");
            Assert.AreEqual(ConsoleCorner.TopLeft, ConsolePosition.Load(),
                "with no persisted value, Load returns the off-the-player default (AC4)");
        }

        [Test]
        public void Save_Persists_AcrossRuns()
        {
            ConsolePosition.Save(ConsoleCorner.BottomRight);
            Assert.AreEqual(ConsoleCorner.BottomRight, ConsolePosition.Load(),
                "the chosen corner persists (PlayerPrefs) so it survives a relaunch (AC4)");

            ConsolePosition.Save(ConsoleCorner.TopRight);
            Assert.AreEqual(ConsoleCorner.TopRight, ConsolePosition.Load(),
                "a re-chosen corner overwrites the persisted value");
        }

        [Test]
        public void Load_OutOfRangePref_FallsBackToDefault()
        {
            // A corrupt/old persisted int must not crash or pick an invalid corner — fall back to the default.
            PlayerPrefs.SetInt(ConsolePosition.PrefsKey, 99);
            Assert.AreEqual(ConsolePosition.Default, ConsolePosition.Load(),
                "an out-of-range persisted corner falls back to the default (robustness)");
            PlayerPrefs.SetInt(ConsolePosition.PrefsKey, -1);
            Assert.AreEqual(ConsolePosition.Default, ConsolePosition.Load(),
                "a negative persisted corner falls back to the default");
        }

        [Test]
        public void Next_CyclesAllFourCorners_InOrder()
        {
            Assert.AreEqual(ConsoleCorner.TopRight, ConsolePosition.Next(ConsoleCorner.TopLeft));
            Assert.AreEqual(ConsoleCorner.BottomLeft, ConsolePosition.Next(ConsoleCorner.TopRight));
            Assert.AreEqual(ConsoleCorner.BottomRight, ConsolePosition.Next(ConsoleCorner.BottomLeft));
            Assert.AreEqual(ConsoleCorner.TopLeft, ConsolePosition.Next(ConsoleCorner.BottomRight),
                "the cycle wraps back to top-left after the fourth corner");
        }

        [Test]
        public void JustifyAndAlign_MapCornersToFlexAnchors()
        {
            // The four corners must map to the right flex anchors on a column-laid scrim (top↔FlexStart justify,
            // left↔FlexStart align). A swap here would park the panel in the wrong corner.
            Assert.AreEqual(UnityEngine.UIElements.Justify.FlexStart, ConsolePosition.JustifyFor(ConsoleCorner.TopLeft));
            Assert.AreEqual(UnityEngine.UIElements.Align.FlexStart, ConsolePosition.AlignFor(ConsoleCorner.TopLeft));
            Assert.AreEqual(UnityEngine.UIElements.Justify.FlexEnd, ConsolePosition.JustifyFor(ConsoleCorner.BottomRight));
            Assert.AreEqual(UnityEngine.UIElements.Align.FlexEnd, ConsolePosition.AlignFor(ConsoleCorner.BottomRight));
            Assert.AreEqual(UnityEngine.UIElements.Align.FlexEnd, ConsolePosition.AlignFor(ConsoleCorner.TopRight));
            Assert.AreEqual(UnityEngine.UIElements.Justify.FlexStart, ConsolePosition.JustifyFor(ConsoleCorner.TopRight));
        }
    }
}
