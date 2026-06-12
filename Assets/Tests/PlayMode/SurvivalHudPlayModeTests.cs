using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the U2-5 survival HUD (ticket 86ca8bdge).
    ///
    /// Proves the HUD reflects LIVE model state end-to-end through a real scene + Update loop — the
    /// complement to the deterministic EditMode segment/band math. The load-bearing headless guard:
    /// warmth decays over a REAL Time.time window (Time.deltaTime~=0 per frame headless,
    /// unity-conventions.md §headless time), so we sample the wall-clock window, never per-frame deltas.
    /// The HUD's presentation is computed from its WIRED references (hud.warmth / hud.inventory) via the
    /// same pinned static math the bar draws with — so this asserts the WIRING is live, not just that a
    /// number changed somewhere. Catches the bug class the placeholder era hid: a HUD that paints a
    /// stale/blank state because its reference isn't actually tracking the model.
    /// </summary>
    public class SurvivalHudPlayModeTests
    {
        private GameObject _go;
        private WarmthNeed _warmth;
        private Inventory _inventory;
        private SurvivalHud _hud;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SurvivalHudTest");
            _warmth = _go.AddComponent<WarmthNeed>();
            _warmth.max = 100f;
            // Fast decay so the window stays short in CI but still spans real wall-clock seconds
            // (NOT per-frame) and crosses band thresholds: 30/sec drops the bar a band+ in ~2s.
            _warmth.decayPerSecond = 30f;
            _warmth.floor01 = 0.05f;
            _warmth.startFull = true;

            _inventory = _go.AddComponent<Inventory>();

            _hud = _go.AddComponent<SurvivalHud>();
            _hud.warmth = _warmth;       // wire exactly as BootstrapProject does (serialized ref analogue)
            _hud.inventory = _inventory;
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_go);

        [UnityTest]
        public IEnumerator Hud_WarmthSegments_TrackTheLiveNeed_AcrossBands_OverARealWindow()
        {
            yield return null; // Start() seeds _current = max + the tick clock

            // Near-full warmth -> almost all segments lit (one frame of fast decay may have already
            // dropped Current01 a hair under 1.0, and FLOOR keeps the last segment dark until fully
            // earned — that's the pinned rule, asserted exactly at 1.0 in EditMode). The point here is
            // the bar reads NEAR-FULL and warm gold the instant we wire to a fresh full need.
            int litFull = SurvivalHud.FilledSegments(_hud.warmth.Current01);
            Assert.GreaterOrEqual(litFull, SurvivalHud.SegmentCount - 1,
                "at (near-)full warmth the HUD lights ~all segments (computed from the WIRED live need); " +
                $"lit={litFull} Current01={_hud.warmth.Current01:0.000}");
            AssertSameColor(new Color(0.91f, 0.70f, 0.36f),
                SurvivalHud.BandColor(_hud.warmth.Current01),
                "(near-)full warmth reads warm ember gold");

            // Decay over a REAL Time.time window (never per-frame; headless deltaTime~=0).
            float start = Time.time;
            while (Time.time - start < 2f) yield return null;

            int litAfter = SurvivalHud.FilledSegments(_hud.warmth.Current01);
            Assert.Less(litAfter, litFull,
                "after a real decay window the HUD lights FEWER segments — the bar empties right-to-left " +
                $"tracking the live need (lit full={litFull} -> after={litAfter}, Current01={_hud.warmth.Current01:0.00})");

            // The band has cooled off warm gold (30/sec*2s ~= 60 lost of 100 -> ~0.4 -> dusk orange or colder).
            Color band = SurvivalHud.BandColor(_hud.warmth.Current01);
            Assert.AreNotEqual(new Color(0.91f, 0.70f, 0.36f), band,
                "after a substantial decay window the filled-run band has shifted off warm gold (cooling)");
        }

        [UnityTest]
        public IEnumerator Hud_Inventory_ReflectsCraftAndChop_Live()
        {
            yield return null;

            // Empty ledger at start (the quiet case is silence — nothing to assert on screen, but the
            // wired references read the live empty state).
            Assert.IsFalse(_hud.inventory.HasAxe, "ledger starts without the axe");
            Assert.AreEqual(0, _hud.inventory.WoodCount, "ledger starts with no wood");

            // Craft the axe (the loop entry) — the HUD's wired Inventory reflects it immediately.
            _inventory.CraftAxe();
            Assert.IsTrue(_hud.inventory.HasAxe,
                "after CraftAxe the HUD's WIRED inventory reads HasAxe == true (axe slot appears)");

            // Add wood (the chop step, U2-3) — the live count flows straight through the wired ref.
            _inventory.AddWood(3);
            Assert.AreEqual(3, _hud.inventory.WoodCount,
                "after AddWood(3) the HUD's WIRED inventory reads WoodCount == 3 (wood slot tracks live)");

            yield return null;
        }

        private static void AssertSameColor(Color expected, Color actual, string msg)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f, msg + " (R)");
            Assert.AreEqual(expected.g, actual.g, 0.001f, msg + " (G)");
            Assert.AreEqual(expected.b, actual.b, 0.001f, msg + " (B)");
        }
    }
}
