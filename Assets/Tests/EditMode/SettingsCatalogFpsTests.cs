using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Guard for the FPS-counter toggle in the settings binding map (ticket 86cahmxmt): PopulateFps registers
    /// an `FPS counter` BOOL row LIVE-bound to the FpsCounterHud component's enabled flag (the
    /// BoolSettingEntry hunger.enabled idiom — OFF = no Update, no OnGUI = zero cost). Drives the real
    /// component so this proves the binding hits the actual enabled flag, the DEFAULT ships ON
    /// ("default — Sponsor-soak tunes"), the row is dev-console-registered via its OWN Populate method (the
    /// PopulateCombat/PopulateWorldLook de-collision precedent — NOT part of the Build overload chain), and a
    /// null target adds no dead knob. Mirrors SettingsCatalogHungerTests' shape.
    /// </summary>
    public class SettingsCatalogFpsTests
    {
        private GameObject _fpsGo;
        private FpsCounterHud _fps;

        [SetUp]
        public void SetUp()
        {
            _fpsGo = new GameObject("Fps");
            _fps = _fpsGo.AddComponent<FpsCounterHud>();
            // AddComponent ships enabled=true — the same state BootstrapProject authors into the scene, so
            // the registration-time default captured below is the shipped default (ON).
            Assert.IsTrue(_fps.enabled, "precondition: a fresh FpsCounterHud is enabled (the shipped default)");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_fpsGo);
            // BoolSettingEntry.SetValue persists to PlayerPrefs — scrub the key so a test-dialed OFF can
            // never leak into a later editor play session's LoadAll (fh.settings.<id>).
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.FpsCounterId);
        }

        private SettingsRegistry BuildWithFps()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateFps(reg, _fps);
            return reg;
        }

        [Test]
        public void PopulateFps_RegistersTheRow_LiveWithTheMandatedLabel()
        {
            var reg = BuildWithFps();

            Assert.IsTrue(reg.Has(SettingsCatalog.FpsCounterId), "the FPS-counter row is registered (86cahmxmt)");
            var entry = reg.Get(SettingsCatalog.FpsCounterId);
            Assert.IsTrue(entry.Available, "the FPS-counter row is LIVE (the counter ships; not a greyed hook)");
            Assert.AreEqual("FPS counter", entry.Label, "the row label is 'FPS counter'");
            Assert.IsInstanceOf<BoolSettingEntry>(entry, "the row is the BOOL archetype (an on/off toggle)");
        }

        [Test]
        public void FpsRow_DrivesTheComponentEnabledFlag_Live()
        {
            var reg = BuildWithFps();
            var row = (BoolSettingEntry)reg.Get(SettingsCatalog.FpsCounterId);

            row.SetValue(false);
            Assert.IsFalse(_fps.enabled,
                "OFF disables the component (no Update, no OnGUI — the zero-cost switch, 86cahmxmt)");

            row.SetValue(true);
            Assert.IsTrue(_fps.enabled, "ON re-enables the component live");

            row.Toggle();
            Assert.IsFalse(_fps.enabled, "Toggle flips the live flag (the console row / nudge path)");
        }

        [Test]
        public void FpsRow_DefaultIsOn_TheSponsorSoakTunesIt()
        {
            var reg = BuildWithFps();
            var row = (BoolSettingEntry)reg.Get(SettingsCatalog.FpsCounterId);

            // Registration captured the shipped default: ON (deliberate for this first build — the Sponsor
            // sees the number immediately at soak; "default — Sponsor-soak tunes" via this row).
            Assert.IsTrue(row.Default, "the captured default is ON (the component ships enabled)");
            Assert.IsFalse(row.DiffersFromDefault, "untouched = at default (no badge)");

            row.SetValue(false);
            Assert.IsTrue(row.DiffersFromDefault, "toggled OFF differs from the shipped ON default (badge)");

            row.ResetToDefault();
            Assert.IsTrue(_fps.enabled, "reset-to-defaults returns the counter to ON");
        }

        [Test]
        public void NullFps_RegistersNoRow_NoDeadKnob()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateFps(reg, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.FpsCounterId),
                "a null counter registers NOTHING (the catalog never null-refs, never adds a dead knob)");
        }

        [Test]
        public void FullBuild_StaysFpsFree_TheRowIsPanelStartRegistered()
        {
            // PopulateFps is called from SettingsPanel.Start (the PopulateCombat/PopulateWorldLook
            // precedent) — the Build overload chain must NOT grow it (backward compatibility: existing
            // Build callers stay byte-identical).
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, null, null, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.FpsCounterId),
                "the Build overload chain does not register the FPS row — SettingsPanel.Start's PopulateFps does");
        }
    }
}
