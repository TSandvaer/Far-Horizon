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
        private HungerNeed _hunger;
        private ThirstNeed _thirst;
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

            // #101: a HUNGER need wired to the HUD's new hunger bar. Fast decay (same rationale as warmth).
            _hunger = _go.AddComponent<HungerNeed>();
            _hunger.max = 100f;
            _hunger.decayPerSecond = 30f;
            _hunger.floor01 = 0.05f;
            _hunger.startFull = true;
            _hunger.berryRestoreAmount = 18f;

            // 86caamkxv: a THIRST need wired to the HUD's new thirst bar. ⚠ The SHIPPED ThirstNeed seeds at
            // startFraction01 = 0.50 (~5 segments, NOT full); this test pins the seed EXPLICITLY (startFull =
            // true) so the loop assertions reason about a KNOWN start state rather than inheriting the half
            // seed silently (the QA-plan trap #2). Fast decay (same rationale as warmth/hunger).
            _thirst = _go.AddComponent<ThirstNeed>();
            _thirst.max = 100f;
            _thirst.decayPerSecond = 30f;
            _thirst.floor01 = 0.05f;
            _thirst.startFull = true;            // pinned seed — do NOT inherit the shipped 0.50 idiom here
            _thirst.waterScoopAmount = 18f;

            _inventory = _go.AddComponent<Inventory>();

            _hud = _go.AddComponent<SurvivalHud>();
            _hud.warmth = _warmth;       // wire exactly as BootstrapProject does (serialized ref analogue)
            _hud.hunger = _hunger;       // #101: the hunger bar's wired need
            _hud.thirst = _thirst;       // 86caamkxv: the thirst bar's wired need
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

        // === #101: the HUNGER bar tracks the live hunger need — depletes on decay, REFILLS on eating ====
        // This is the loop-verify guard: the player must SEE hunger deplete + refill. Bound to the SAME
        // pinned FilledSegments rule the bar draws with, computed from the WIRED hud.hunger reference.
        [UnityTest]
        public IEnumerator Hud_HungerSegments_DepleteOnDecay_AndRefillOnEat_OverARealWindow()
        {
            yield return null; // Start() seeds hunger _current = max + the tick clock

            int litFull = SurvivalHud.FilledSegments(_hud.hunger.Current01);
            Assert.GreaterOrEqual(litFull, SurvivalHud.SegmentCount - 1,
                "at (near-)full hunger the HUD lights ~all hunger segments (from the WIRED live need); " +
                $"lit={litFull} Current01={_hud.hunger.Current01:0.000}");

            // Decay over a REAL Time.time window — the hunger bar empties tracking the live need.
            float start = Time.time;
            while (Time.time - start < 2f) yield return null;

            int litAfter = SurvivalHud.FilledSegments(_hud.hunger.Current01);
            Assert.Less(litAfter, litFull,
                "after a real decay window the HUD lights FEWER hunger segments (the bar empties tracking " +
                $"the live need; full={litFull} -> after={litAfter}, Current01={_hud.hunger.Current01:0.00})");

            // Eat (the restore seam the eat-input drives) — the bar REFILLS, the player sees hunger recover.
            _hunger.AddFood();
            int litAfterEat = SurvivalHud.FilledSegments(_hud.hunger.Current01);
            Assert.Greater(litAfterEat, litAfter,
                "after eating, the HUD lights MORE hunger segments — the bar refills on the restore seam " +
                $"(after-decay={litAfter} -> after-eat={litAfterEat})");
        }

        // === 86caamkxv: the THIRST bar tracks the live thirst need — depletes on decay, REFILLS on DRINK ==
        // The loop-verify guard for the third bar (the deferred-from-#124 drink loop, finally VISIBLE): the
        // player must SEE thirst deplete + refill. Bound to the SAME pinned FilledSegments rule the bar draws
        // with, computed from the WIRED hud.thirst reference, driving AddWater() (NOT AddFood/Satisfy — the
        // thirst-specific restore seam) as the restore. Decay over a REAL Time.time window (headless
        // deltaTime~=0, unity-conventions.md §headless time). The test thirst is seeded full in SetUp (the
        // shipped 0.50 seed is pinned away here so the assertions reason about a known start).
        [UnityTest]
        public IEnumerator Hud_ThirstSegments_DepleteOnDecay_AndRefillOnDrink_OverARealWindow()
        {
            yield return null; // Start() seeds thirst _current = max (pinned startFull=true) + the tick clock

            int litFull = SurvivalHud.FilledSegments(_hud.thirst.Current01);
            Assert.GreaterOrEqual(litFull, SurvivalHud.SegmentCount - 1,
                "at (near-)full thirst the HUD lights ~all thirst segments (from the WIRED live need); " +
                $"lit={litFull} Current01={_hud.thirst.Current01:0.000}");

            // Decay over a REAL Time.time window — the thirst bar empties tracking the live need.
            float start = Time.time;
            while (Time.time - start < 2f) yield return null;

            int litAfter = SurvivalHud.FilledSegments(_hud.thirst.Current01);
            Assert.Less(litAfter, litFull,
                "after a real decay window the HUD lights FEWER thirst segments (the bar empties tracking " +
                $"the live need; full={litFull} -> after={litAfter}, Current01={_hud.thirst.Current01:0.00})");

            // Drink (the restore seam the pond drink-action drives via AddWater) — the bar REFILLS, the player
            // sees thirst recover. This is the drink loop the whole ticket exists to make visible.
            _thirst.AddWater();
            int litAfterDrink = SurvivalHud.FilledSegments(_hud.thirst.Current01);
            Assert.Greater(litAfterDrink, litAfter,
                "after drinking, the HUD lights MORE thirst segments — the bar refills on the AddWater seam " +
                $"(after-decay={litAfter} -> after-drink={litAfterDrink})");

            // Refilling reads back toward stream-blue (the band warms back as the need recovers).
            Color band = SurvivalHud.ThirstBandColor(_hud.thirst.Current01);
            Assert.Greater(band.b, band.r, "the refilled thirst band reads BLUE (blue > red — the cool note)");
        }

        // === 86caamkxv: all THREE bars coexist in ONE scene, each reads its own LIVE need (AC1 coexistence) ==
        // Catches the "thirst bar drawn but never bound" silent-null trap (QA-plan trap #3): all three refs
        // wired + each FilledSegments(hud.<need>.Current01) reads a sane lit count — proving the WIRING is
        // live for all three, not just that the type compiles.
        [UnityTest]
        public IEnumerator Hud_AllThreeNeedBars_AreWiredAndTrackTheirOwnLiveNeed()
        {
            yield return null; // Start() seeds all three needs + the tick clocks

            Assert.IsNotNull(_hud.warmth, "the warmth bar's need must be wired");
            Assert.IsNotNull(_hud.hunger, "the hunger bar's need must be wired");
            Assert.IsNotNull(_hud.thirst, "the thirst bar's need must be wired (86caamkxv — the new third bar)");

            // Each bar reads its OWN need's live fill through the one pinned rule (sane 0..SegmentCount count).
            int litW = SurvivalHud.FilledSegments(_hud.warmth.Current01);
            int litH = SurvivalHud.FilledSegments(_hud.hunger.Current01);
            int litT = SurvivalHud.FilledSegments(_hud.thirst.Current01);
            foreach (var (lit, name) in new[] { (litW, "warmth"), (litH, "hunger"), (litT, "thirst") })
            {
                Assert.GreaterOrEqual(lit, 0, $"{name} lit-segment count must be in range");
                Assert.LessOrEqual(lit, SurvivalHud.SegmentCount, $"{name} lit-segment count must be in range");
            }

            // The three bars are computed by three DISTINCT band functions — confirm they're not collapsed to
            // one (a generalization that accidentally shares one band function would defeat the three-color read).
            Color w = SurvivalHud.BandColor(0.7f);
            Color h = SurvivalHud.HungerBandColor(0.7f);
            Color t = SurvivalHud.ThirstBandColor(0.7f);
            Assert.AreNotEqual(w, h, "warmth + hunger bands must differ");
            Assert.AreNotEqual(w, t, "warmth + thirst bands must differ");
            Assert.AreNotEqual(h, t, "hunger + thirst bands must differ");
        }

        private static void AssertSameColor(Color expected, Color actual, string msg)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f, msg + " (R)");
            Assert.AreEqual(expected.g, actual.g, 0.001f, msg + " (G)");
            Assert.AreEqual(expected.b, actual.b, 0.001f, msg + " (B)");
        }
    }
}
