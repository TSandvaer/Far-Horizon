using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Guard for the HUNGER tweakables in the settings binding map (ticket 86cabd75y — the 86caamkp8 AC4
    /// settings-registration follow-up): the SettingsCatalog 9-arg Build / PopulateHunger registers
    /// `Hunger decay rate` + `Berry restore amount` LIVE-bound to the real HungerNeed (the settings panel host
    /// 86caa4bqp / PR #83 is MERGED, so these are LIVE rows, not greyed extension hooks). Drives the real
    /// component (no scene/Update — decayPerSecond + berryRestoreAmount are plain public floats) so this proves
    /// the bindings hit the actual HungerNeed params, the labels are the AC-mandated names, the band clamps, and
    /// the prior overloads stay hunger-free (backward compatibility — existing SettingsCatalog*Tests unaffected).
    /// Mirrors SettingsCatalogThirstTests.cs exactly (the PopulateThirst precedent the ticket cites).
    /// </summary>
    public class SettingsCatalogHungerTests
    {
        private GameObject _camGo, _playerGo, _hungerGo;
        private OrbitCamera _orbit;
        private WasdMovement _wasd;
        private HungerNeed _hunger;

        [SetUp]
        public void SetUp()
        {
            _camGo = new GameObject("Cam");
            _camGo.AddComponent<Camera>();
            _orbit = _camGo.AddComponent<OrbitCamera>();

            _playerGo = new GameObject("Player");
            _playerGo.AddComponent<UnityEngine.AI.NavMeshAgent>();
            _wasd = _playerGo.AddComponent<WasdMovement>();

            _hungerGo = new GameObject("Hunger");
            _hunger = _hungerGo.AddComponent<HungerNeed>();
            _hunger.decayPerSecond = HungerNeed.HungerMedDecayPerSecond; // 0.35 default tier
            _hunger.berryRestoreAmount = HungerNeed.BerryRestoreDefault; // 18f default (#183 NIT — named const)
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camGo);
            Object.DestroyImmediate(_playerGo);
            Object.DestroyImmediate(_hungerGo);
        }

        // The full 9-arg Build with only the hunger target wired (everything else null — the catalog null-skips).
        private SettingsRegistry BuildWithHunger()
            => SettingsCatalog.Build(_orbit, _wasd, null, null, null, null, null, null, _hunger);

        [Test]
        public void HungerBuild_RegistersHungerSettings_WithAcMandatedNames()
        {
            var reg = BuildWithHunger();

            Assert.IsTrue(reg.Has(SettingsCatalog.HungerDecayId), "hunger decay rate registered (86cabd75y)");
            Assert.IsTrue(reg.Has(SettingsCatalog.BerryRestoreId), "berry restore amount registered (86cabd75y)");
            Assert.IsTrue(reg.Get(SettingsCatalog.HungerDecayId).Available, "hunger decay rate is a LIVE setting (panel merged)");
            Assert.IsTrue(reg.Get(SettingsCatalog.BerryRestoreId).Available, "berry restore amount is LIVE");

            // AC-mandated names (the gameplay-wave/needs naming convention, matching thirst).
            Assert.AreEqual("Hunger decay rate", reg.Get(SettingsCatalog.HungerDecayId).Label,
                "the label must be the AC-mandated 'Hunger decay rate'");
            Assert.AreEqual("Berry restore amount", reg.Get(SettingsCatalog.BerryRestoreId).Label,
                "the label must be the AC-mandated 'Berry restore amount'");
        }

        [Test]
        public void HungerDecayRate_DrivesHungerNeedDecay_Live()
        {
            var reg = BuildWithHunger();
            var decay = (FloatSettingEntry)reg.Get(SettingsCatalog.HungerDecayId);

            decay.SetValue(0.7f);
            Assert.AreEqual(0.7f, _hunger.decayPerSecond, 1e-4f,
                "the hunger-decay slider drives HungerNeed.decayPerSecond live (86cabd75y)");
        }

        [Test]
        public void BerryRestoreAmount_DrivesHungerNeedRestore_Live()
        {
            var reg = BuildWithHunger();
            var restore = (FloatSettingEntry)reg.Get(SettingsCatalog.BerryRestoreId);

            restore.SetValue(30f);
            Assert.AreEqual(30f, _hunger.berryRestoreAmount, 1e-4f,
                "the berry-restore slider drives HungerNeed.berryRestoreAmount live (86cabd75y)");
        }

        [Test]
        public void HungerSettings_ClampToTheirBands()
        {
            var reg = BuildWithHunger();
            var decay = (FloatSettingEntry)reg.Get(SettingsCatalog.HungerDecayId);
            var restore = (FloatSettingEntry)reg.Get(SettingsCatalog.BerryRestoreId);

            // Dial past each band's edges; the FloatSettingEntry clamps to [Min, Max] and so does the live field.
            decay.SetValue(99f);
            Assert.AreEqual(SettingsCatalog.HungerDecayMax, _hunger.decayPerSecond, 1e-4f,
                "hunger decay rate clamps to HungerDecayMax (the band ceiling)");
            decay.SetValue(-99f);
            Assert.AreEqual(SettingsCatalog.HungerDecayMin, _hunger.decayPerSecond, 1e-4f,
                "hunger decay rate clamps to HungerDecayMin (the band floor)");

            restore.SetValue(999f);
            Assert.AreEqual(SettingsCatalog.BerryRestoreMax, _hunger.berryRestoreAmount, 1e-4f,
                "berry restore amount clamps to BerryRestoreMax");
            restore.SetValue(-999f);
            Assert.AreEqual(SettingsCatalog.BerryRestoreMin, _hunger.berryRestoreAmount, 1e-4f,
                "berry restore amount clamps to BerryRestoreMin");
        }

        [Test]
        public void EightArgBuild_StaysHungerFree_BackwardCompatible()
        {
            // The 8-arg Build (the legacy full overload existing callers used before hunger) must NOT add the
            // hunger rows — backward compatibility. Only the 9-arg overload (with a hunger target) registers them.
            var reg = SettingsCatalog.Build(_orbit, _wasd, null, null, null, null, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.HungerDecayId), "the 8-arg Build does not register hunger settings");
            Assert.IsFalse(reg.Has(SettingsCatalog.BerryRestoreId), "the 8-arg Build does not register berry restore");
            // The non-hunger settings are still present (the 8-arg path is unchanged).
            Assert.IsTrue(reg.Has(SettingsCatalog.WalkSpeedId), "walk speed still present in the 8-arg Build");
        }

        [Test]
        public void NullHunger_RegistersNoHungerRows()
        {
            // A bare rig / a hunger-less scene passes null hunger → no hunger rows (the catalog never null-refs).
            var reg = SettingsCatalog.Build(_orbit, _wasd, null, null, null, null, null, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.HungerDecayId), "null hunger → no hunger-decay row");
            Assert.IsFalse(reg.Has(SettingsCatalog.BerryRestoreId), "null hunger → no berry-restore row");
        }
    }
}
