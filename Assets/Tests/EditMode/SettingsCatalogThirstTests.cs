using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC5 guard for the THIRST tweakables in the settings binding map (ticket 86caamkv7): the SettingsCatalog
    /// 3-arg Build / PopulateThirst registers `thirst decay rate` + `water scoop amount` LIVE-bound to the real
    /// ThirstNeed (the settings panel host 86caa4bqp / PR #83 is MERGED, so these are LIVE rows, not greyed
    /// extension hooks). Drives the real component (no scene/Update — the fields are plain public floats) so
    /// this proves the bindings hit the actual ThirstNeed params, the labels are the AC-mandated names, and the
    /// 2-arg Build stays thirst-free (backward compatibility — existing SettingsCatalogTests unaffected).
    /// </summary>
    public class SettingsCatalogThirstTests
    {
        private GameObject _camGo, _playerGo, _thirstGo;
        private OrbitCamera _orbit;
        private WasdMovement _wasd;
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

            _thirstGo = new GameObject("Thirst");
            _thirst = _thirstGo.AddComponent<ThirstNeed>();
            _thirst.decayPerSecond = ThirstNeed.ThirstMedDecayPerSecond;
            _thirst.waterScoopAmount = 14f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camGo);
            Object.DestroyImmediate(_playerGo);
            Object.DestroyImmediate(_thirstGo);
        }

        [Test]
        public void ThirstBuild_RegistersThirstSettings_WithAcMandatedNames()
        {
            var reg = SettingsCatalog.Build(_orbit, _wasd, _thirst);

            Assert.IsTrue(reg.Has(SettingsCatalog.ThirstDecayId), "thirst decay rate registered (AC5)");
            Assert.IsTrue(reg.Has(SettingsCatalog.WaterScoopId), "water scoop amount registered (AC5)");
            Assert.IsTrue(reg.Get(SettingsCatalog.ThirstDecayId).Available, "thirst decay rate is a LIVE setting (panel merged)");
            Assert.IsTrue(reg.Get(SettingsCatalog.WaterScoopId).Available, "water scoop amount is LIVE");

            // AC-mandated names (the gameplay-wave/hunger naming convention).
            Assert.AreEqual("Thirst decay rate", reg.Get(SettingsCatalog.ThirstDecayId).Label,
                "the label must be the AC-mandated 'thirst decay rate'");
            Assert.AreEqual("Water scoop amount", reg.Get(SettingsCatalog.WaterScoopId).Label,
                "the label must be the AC-mandated 'water scoop amount'");
        }

        [Test]
        public void ThirstDecayRate_DrivesThirstNeedDecay_Live()
        {
            var reg = SettingsCatalog.Build(_orbit, _wasd, _thirst);
            var decay = (FloatSettingEntry)reg.Get(SettingsCatalog.ThirstDecayId);

            decay.SetValue(0.8f);
            Assert.AreEqual(0.8f, _thirst.decayPerSecond, 1e-4f,
                "the thirst-decay slider drives ThirstNeed.decayPerSecond live (AC5)");
        }

        [Test]
        public void WaterScoopAmount_DrivesThirstNeedScoop_Live()
        {
            var reg = SettingsCatalog.Build(_orbit, _wasd, _thirst);
            var scoop = (FloatSettingEntry)reg.Get(SettingsCatalog.WaterScoopId);

            scoop.SetValue(22f);
            Assert.AreEqual(22f, _thirst.waterScoopAmount, 1e-4f,
                "the water-scoop slider drives ThirstNeed.waterScoopAmount live (AC5)");
        }

        [Test]
        public void TwoArgBuild_StaysThirstFree_BackwardCompatible()
        {
            // The legacy 2-arg Build (used by existing callers / SettingsCatalogTests) must NOT add the thirst
            // rows — backward compatibility. Only the 3-arg overload (with a thirst target) registers them.
            var reg = SettingsCatalog.Build(_orbit, _wasd);
            Assert.IsFalse(reg.Has(SettingsCatalog.ThirstDecayId), "the 2-arg Build does not register thirst settings");
            Assert.IsFalse(reg.Has(SettingsCatalog.WaterScoopId), "the 2-arg Build does not register water scoop");
            // The non-thirst settings are still present (the 2-arg path is unchanged).
            Assert.IsTrue(reg.Has(SettingsCatalog.WalkSpeedId), "walk speed still present in the 2-arg Build");
        }

        [Test]
        public void NullThirst_RegistersNoThirstRows()
        {
            // A bare rig / a thirst-less scene passes null thirst → no thirst rows (the catalog never null-refs).
            var reg = SettingsCatalog.Build(_orbit, _wasd, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.ThirstDecayId), "null thirst → no thirst-decay row");
            Assert.IsFalse(reg.Has(SettingsCatalog.WaterScoopId), "null thirst → no water-scoop row");
        }
    }
}
