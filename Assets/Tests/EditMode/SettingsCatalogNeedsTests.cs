using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Guard for the PER-NEED entries in the settings binding map (ticket 86cabeqwf — the dev-tweak-console
    /// per-need on/off + decay-rate rows). The 12-arg Build / PopulateNeeds registers, LIVE-bound to the real
    /// need components:
    ///   • a per-need ON/OFF Toggle (Bool archetype from the 86cabeqj9 foundation) for warmth + hunger + thirst,
    ///     driving <see cref="SurvivalNeed.decayEnabled"/>;
    ///   • the WARMTH decay-rate slider (hunger/thirst decay-rate sliders already exist via PopulateHunger /
    ///     PopulateThirst — re-adding them would throw a duplicate-id, so PopulateNeeds does NOT).
    ///
    /// ALL THREE need components exist on main, so all three toggles are LIVE (Available=true), NOT extension
    /// hooks (the ticket AC2/AC3 hedge assumed hunger gated / thirst unbuilt; both are built). Drives the real
    /// components (no scene/Update — decayEnabled + decayPerSecond are plain public fields) so this proves the
    /// bindings hit the ACTUAL need params. The dead-knob guards (a toggle that sets a field the decay path
    /// doesn't read; a slider bound to nothing) mirror the #218 AC6 pattern — a dead knob leaves the Sponsor
    /// guessing. Backward-compat: the 11-arg Build stays free of these ids.
    /// </summary>
    public class SettingsCatalogNeedsTests
    {
        private GameObject _camGo, _playerGo, _warmthGo, _hungerGo, _thirstGo;
        private OrbitCamera _orbit;
        private WasdMovement _wasd;
        private WarmthNeed _warmth;
        private HungerNeed _hunger;
        private ThirstNeed _thirst;

        [SetUp]
        public void SetUp()
        {
            _camGo = new GameObject("Cam");
            _camGo.AddComponent<Camera>();
            _orbit = _camGo.AddComponent<OrbitCamera>();

            _playerGo = new GameObject("Player");
            _playerGo.AddComponent<UnityEngine.AI.NavMeshAgent>();
            _wasd = _playerGo.AddComponent<WasdMovement>();

            _warmthGo = new GameObject("Warmth");
            _warmth = _warmthGo.AddComponent<WarmthNeed>();
            _hungerGo = new GameObject("Hunger");
            _hunger = _hungerGo.AddComponent<HungerNeed>();
            _thirstGo = new GameObject("Thirst");
            _thirst = _thirstGo.AddComponent<ThirstNeed>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camGo);
            Object.DestroyImmediate(_playerGo);
            Object.DestroyImmediate(_warmthGo);
            Object.DestroyImmediate(_hungerGo);
            Object.DestroyImmediate(_thirstGo);
        }

        // The full 12-arg Build with all three needs wired (everything else null — the catalog null-skips).
        private SettingsRegistry BuildWithNeeds()
            => SettingsCatalog.Build(_orbit, _wasd, _thirst, null, null, null, null, null, _hunger, null, null, _warmth);

        [Test]
        public void NeedsBuild_RegistersPerNeedRows_WithAcMandatedNames_AllLive()
        {
            var reg = BuildWithNeeds();

            // Three on/off toggles + the warmth decay-rate slider.
            Assert.IsTrue(reg.Has(SettingsCatalog.WarmthEnabledId), "warmth on/off toggle registered (86cabeqwf)");
            Assert.IsTrue(reg.Has(SettingsCatalog.HungerEnabledId), "hunger on/off toggle registered (86cabeqwf)");
            Assert.IsTrue(reg.Has(SettingsCatalog.ThirstEnabledId), "thirst on/off toggle registered (86cabeqwf)");
            Assert.IsTrue(reg.Has(SettingsCatalog.WarmthDecayId), "warmth decay-rate slider registered (86cabeqwf)");

            // All LIVE — the components exist on main (NOT extension hooks; the AC2/AC3 hedge is overtaken).
            Assert.IsTrue(reg.Get(SettingsCatalog.WarmthEnabledId).Available, "warmth toggle is LIVE (component exists)");
            Assert.IsTrue(reg.Get(SettingsCatalog.HungerEnabledId).Available, "hunger toggle is LIVE (component exists)");
            Assert.IsTrue(reg.Get(SettingsCatalog.ThirstEnabledId).Available, "thirst toggle is LIVE (component exists)");
            Assert.IsTrue(reg.Get(SettingsCatalog.WarmthDecayId).Available, "warmth decay-rate is LIVE");

            // The toggles are the Toggle (Bool) archetype from the foundation — NOT a re-invented archetype.
            Assert.AreEqual(SettingEntry.Archetype.Toggle, reg.Get(SettingsCatalog.WarmthEnabledId).Kind,
                "the on/off row uses the Bool/Toggle archetype from the 86cabeqj9 foundation");
            Assert.AreEqual(SettingEntry.Archetype.Slider, reg.Get(SettingsCatalog.WarmthDecayId).Kind,
                "the warmth decay-rate row is a Slider");

            // AC-mandated labels.
            Assert.AreEqual("Warmth decay on", reg.Get(SettingsCatalog.WarmthEnabledId).Label);
            Assert.AreEqual("Hunger decay on", reg.Get(SettingsCatalog.HungerEnabledId).Label);
            Assert.AreEqual("Thirst decay on", reg.Get(SettingsCatalog.ThirstEnabledId).Label);
            Assert.AreEqual("Warmth decay rate", reg.Get(SettingsCatalog.WarmthDecayId).Label);
        }

        [Test]
        public void WarmthToggle_DrivesWarmthDecayEnabled_Live()
        {
            var reg = BuildWithNeeds();
            var toggle = (BoolSettingEntry)reg.Get(SettingsCatalog.WarmthEnabledId);

            Assert.IsTrue(_warmth.decayEnabled, "starts on");
            toggle.SetValue(false);
            Assert.IsFalse(_warmth.decayEnabled, "the warmth toggle drives WarmthNeed.decayEnabled live (dead-knob guard)");
            toggle.SetValue(true);
            Assert.IsTrue(_warmth.decayEnabled, "toggling back on re-enables warmth decay live");
        }

        [Test]
        public void HungerToggle_DrivesHungerDecayEnabled_Live()
        {
            var reg = BuildWithNeeds();
            var toggle = (BoolSettingEntry)reg.Get(SettingsCatalog.HungerEnabledId);

            toggle.SetValue(false);
            Assert.IsFalse(_hunger.decayEnabled, "the hunger toggle drives HungerNeed.decayEnabled live (dead-knob guard)");
            toggle.SetValue(true);
            Assert.IsTrue(_hunger.decayEnabled, "toggling back on re-enables hunger decay live");
        }

        [Test]
        public void ThirstToggle_DrivesThirstDecayEnabled_Live()
        {
            var reg = BuildWithNeeds();
            var toggle = (BoolSettingEntry)reg.Get(SettingsCatalog.ThirstEnabledId);

            toggle.SetValue(false);
            Assert.IsFalse(_thirst.decayEnabled, "the thirst toggle drives ThirstNeed.decayEnabled live (dead-knob guard)");
            toggle.SetValue(true);
            Assert.IsTrue(_thirst.decayEnabled, "toggling back on re-enables thirst decay live");
        }

        [Test]
        public void WarmthToggleOff_ActuallyHaltsDecay_EndToEnd()
        {
            // The end-to-end bug-class guard: the toggle must HALT the observable decay, not merely flip a field
            // the decay path ignores. Drive the toggle THROUGH the registry, then tick the real need.
            var reg = BuildWithNeeds();
            var toggle = (BoolSettingEntry)reg.Get(SettingsCatalog.WarmthEnabledId);
            _warmth.max = 100f;
            _warmth.decayPerSecond = 1f;
            _warmth.floor01 = 0.05f;
            _warmth.AddWarmth(100f); // seed current = 100

            toggle.SetValue(false);
            _warmth.TickSeconds(10f);
            Assert.AreEqual(100f, _warmth.Current, 0.001f,
                "toggling warmth OFF via the console must HALT decay end-to-end (not a dead field)");

            toggle.SetValue(true);
            _warmth.TickSeconds(10f);
            Assert.AreEqual(90f, _warmth.Current, 0.001f,
                "toggling warmth back ON resumes decay live (10s * 1/sec from the held-at-100 value)");
        }

        [Test]
        public void WarmthDecayRate_DrivesWarmthNeedDecay_Live_AndClamps()
        {
            var reg = BuildWithNeeds();
            var decay = (FloatSettingEntry)reg.Get(SettingsCatalog.WarmthDecayId);

            decay.SetValue(0.9f);
            Assert.AreEqual(0.9f, _warmth.decayPerSecond, 1e-4f,
                "the warmth-decay slider drives WarmthNeed.decayPerSecond live (86cabeqwf)");

            // Clamp to the band (the FloatSettingEntry clamps to [Min, Max]).
            decay.SetValue(99f);
            Assert.AreEqual(SettingsCatalog.WarmthDecayMax, _warmth.decayPerSecond, 1e-4f,
                "warmth decay rate clamps to WarmthDecayMax");
            decay.SetValue(-99f);
            Assert.AreEqual(SettingsCatalog.WarmthDecayMin, _warmth.decayPerSecond, 1e-4f,
                "warmth decay rate clamps to WarmthDecayMin");
        }

        [Test]
        public void ElevenArgBuild_StaysPerNeedFree_BackwardCompatible()
        {
            // The 11-arg Build (existing callers) must NOT add the per-need rows — only the 12-arg overload does.
            var reg = SettingsCatalog.Build(_orbit, _wasd, _thirst, null, null, null, null, null, _hunger, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.WarmthEnabledId), "11-arg Build adds no warmth toggle");
            Assert.IsFalse(reg.Has(SettingsCatalog.HungerEnabledId), "11-arg Build adds no hunger toggle");
            Assert.IsFalse(reg.Has(SettingsCatalog.ThirstEnabledId), "11-arg Build adds no thirst toggle");
            Assert.IsFalse(reg.Has(SettingsCatalog.WarmthDecayId), "11-arg Build adds no warmth decay-rate row");
            // The hunger/thirst decay-rate sliders (their own tickets) ARE still present in the 11-arg path.
            Assert.IsTrue(reg.Has(SettingsCatalog.HungerDecayId), "hunger decay rate still present (its own ticket)");
            Assert.IsTrue(reg.Has(SettingsCatalog.ThirstDecayId), "thirst decay rate still present (its own ticket)");
        }

        [Test]
        public void NullNeeds_RegisterOnlyTheWiredNeedsRows()
        {
            // Each need is independently nullable — a bare rig passing only warmth gets only the warmth rows.
            var reg = SettingsCatalog.Build(_orbit, _wasd, null, null, null, null, null, null, null, null, null, _warmth);
            Assert.IsTrue(reg.Has(SettingsCatalog.WarmthEnabledId), "warmth wired → warmth toggle present");
            Assert.IsTrue(reg.Has(SettingsCatalog.WarmthDecayId), "warmth wired → warmth decay-rate present");
            Assert.IsFalse(reg.Has(SettingsCatalog.HungerEnabledId), "null hunger → no hunger toggle");
            Assert.IsFalse(reg.Has(SettingsCatalog.ThirstEnabledId), "null thirst → no thirst toggle");
        }

        [Test]
        public void PopulateNeeds_IsNullRegistrySafe()
        {
            Assert.DoesNotThrow(() => SettingsCatalog.PopulateNeeds(null, _warmth, _hunger, _thirst),
                "PopulateNeeds must no-op on a null registry (the catalog never null-refs)");
        }
    }
}
