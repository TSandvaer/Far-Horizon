using NUnit.Framework;
using UnityEngine;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// STALE-DEFAULT INVALIDATION guards (ticket 86cah90cp — the sun-fidelity defect's cause).
    ///
    /// THE DEFECT CLASS: FloatSettingEntry persists every SetValue to PlayerPrefs, and SettingsPanel.Start's
    /// Registry.LoadAll() re-applies the persisted value at every boot. A value persisted under an OLD baked
    /// default (e.g. fh.settings.sun_elevation=18 from the elev-18 era) therefore silently OVERRODE every
    /// newer bake (the #223 12° bake) on that machine, forever — the round-1 "i cant see the sun" soak:
    /// the shipped GradientSky.mat carried the correct 12° _SunDirection (binary-verified), but the shipped
    /// exe's Player-prev.log showed the F10 tool resolving elevation=18 BEFORE any user input, because
    /// LoadAll had stomped the live material with the stale pref at boot.
    ///
    /// THE FIX: SetValue stamps the registration-time default alongside the value (PrefsKey + ".def");
    /// LoadFromPrefs honours the persisted override ONLY while the row's current default equals the stamp.
    /// Bake moved (or legacy un-stamped key) → the override is discarded and the new baked default shows.
    /// A deliberate re-dial after the new bake re-persists with a fresh stamp, so soak tweaks still survive
    /// relaunches (the original AC5) — they just can't outlive the default they were dialed against.
    /// </summary>
    public class SettingsStaleDefaultTests
    {
        private const string TestId = "__test_stale_default";
        private const string PrefsKey = "fh.settings." + TestId;
        private const string DefKey = PrefsKey + ".def";

        [SetUp]
        public void ClearPrefs()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.DeleteKey(DefKey);
        }

        [TearDown]
        public void CleanupPrefs()
        {
            // These tests write REAL PlayerPrefs (registry on Windows) — never leak keys off the test run
            // (the test-pollution injector class: a leaked key would be re-applied in the Sponsor's game).
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.DeleteKey(DefKey);
        }

        [Test]
        public void SetValue_StampsTheRegistrationDefault_AlongsideTheValue()
        {
            float param = 20f;
            float[] cell = { param };
            var e = new FloatSettingEntry(TestId, "probe", () => cell[0], v => cell[0] = v, 0f, 100f);
            e.SetValue(35f);
            Assert.IsTrue(PlayerPrefs.HasKey(PrefsKey), "SetValue must persist the value");
            Assert.IsTrue(PlayerPrefs.HasKey(DefKey), "SetValue must stamp the registration default (.def)");
            Assert.AreEqual(20f, PlayerPrefs.GetFloat(DefKey), 1e-4f,
                "the .def stamp must be the registration-time default (the baked value the override was dialed under)");
        }

        [Test]
        public void PersistedOverride_SurvivesReload_WhileTheBakedDefaultIsUnchanged()
        {
            // Session 1: default 20, user dials 35 (persists value + stamp).
            float[] s1 = { 20f };
            var e1 = new FloatSettingEntry(TestId, "probe", () => s1[0], v => s1[0] = v, 0f, 100f);
            e1.SetValue(35f);

            // Session 2 (relaunch, SAME bake): fresh entry registers at the same default 20 → override applies.
            float[] s2 = { 20f };
            var e2 = new FloatSettingEntry(TestId, "probe", () => s2[0], v => s2[0] = v, 0f, 100f);
            e2.LoadFromPrefs();
            Assert.AreEqual(35f, s2[0], 1e-4f,
                "a persisted override must survive a relaunch while the baked default is unchanged (the original AC5)");
        }

        [Test]
        public void PersistedOverride_IsDiscarded_WhenTheBakedDefaultMoves()
        {
            // Session 1: default 18 (the old bake), a value 18 persists (e.g. the Sponsor's stale sun_elevation).
            float[] s1 = { 18f };
            var e1 = new FloatSettingEntry(TestId, "probe", () => s1[0], v => s1[0] = v, 0f, 100f);
            e1.SetValue(18f);

            // Session 2: the bake MOVED the default to 8 (the round-2 sun bake). The stale override must be
            // discarded — the freshly-baked default must show, NOT the old 18 (the #223 round-1 defect).
            float[] s2 = { 8f };
            var e2 = new FloatSettingEntry(TestId, "probe", () => s2[0], v => s2[0] = v, 0f, 100f);
            e2.LoadFromPrefs();
            Assert.AreEqual(8f, s2[0], 1e-4f,
                "a persisted override dialed under an OLD baked default must NOT override a NEW bake " +
                "(the sun-fidelity defect: stale sun_elevation=18 stomped the 12° bake at every boot)");
            Assert.IsFalse(PlayerPrefs.HasKey(PrefsKey), "the stale value key must be deleted (not re-applied next boot)");
            Assert.IsFalse(PlayerPrefs.HasKey(DefKey), "the stale stamp key must be deleted");
        }

        [Test]
        public void LegacyKeyWithoutDefaultStamp_IsDiscarded_NotApplied()
        {
            // The Sponsor's machine state at fix time: fh.settings.sun_elevation exists from BEFORE stamping
            // existed — no .def sibling. Provenance unknowable (dialed under which default?) → discard; the
            // baked default wins. A post-fix re-dial re-persists with a stamp and behaves normally.
            PlayerPrefs.SetFloat(PrefsKey, 18f); // raw legacy key, no .def stamp

            float[] s = { 8f };
            var e = new FloatSettingEntry(TestId, "probe", () => s[0], v => s[0] = v, 0f, 100f);
            e.LoadFromPrefs();
            Assert.AreEqual(8f, s[0], 1e-4f,
                "a legacy un-stamped key must be discarded, not applied — its provenance (which default it was " +
                "dialed under) is unknowable, and the observed instance was a stale-default override");
            Assert.IsFalse(PlayerPrefs.HasKey(PrefsKey), "the legacy key must be deleted (one-time self-heal)");
        }

        [Test]
        public void RedialAfterANewBake_PersistsAgain_WithAFreshStamp()
        {
            // Full lifecycle: stale key discarded on load, then the user deliberately re-dials → the new
            // override persists under the NEW default's stamp and survives the next relaunch.
            PlayerPrefs.SetFloat(PrefsKey, 18f); // stale legacy key

            float[] s1 = { 8f };
            var e1 = new FloatSettingEntry(TestId, "probe", () => s1[0], v => s1[0] = v, 0f, 100f);
            e1.LoadFromPrefs();               // discards the stale key
            e1.SetValue(11f);                 // deliberate re-dial under the new default (8)
            Assert.AreEqual(8f, PlayerPrefs.GetFloat(DefKey), 1e-4f, "the fresh stamp must be the NEW default");

            float[] s2 = { 8f };
            var e2 = new FloatSettingEntry(TestId, "probe", () => s2[0], v => s2[0] = v, 0f, 100f);
            e2.LoadFromPrefs();
            Assert.AreEqual(11f, s2[0], 1e-4f, "the re-dialed override must survive the next relaunch (AC5 restored)");
        }
    }
}
