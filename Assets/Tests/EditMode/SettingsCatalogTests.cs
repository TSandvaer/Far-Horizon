using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC3/AC4 guard for the Far-Horizon binding map (ticket 86caa4bqp): the SettingsCatalog wires the
    /// CURRENTLY-AVAILABLE params (zoom range, view-angle range, walk speed) onto the REAL OrbitCamera /
    /// WasdMovement, and leaves the not-yet-built ones (jump height, tool-use speed) as greyed extension
    /// hooks. Drives the real components (no scene/Update needed — the fields are plain public floats) so
    /// this proves the bindings hit the actual gameplay params, and that tightening a range CLAMPS the
    /// live camera (AC4 — OrbitCamera.SetDistance/SetPitch clamp to min/max).
    /// </summary>
    public class SettingsCatalogTests
    {
        private GameObject _camGo, _playerGo;
        private OrbitCamera _orbit;
        private WasdMovement _wasd;

        [SetUp]
        public void SetUp()
        {
            _camGo = new GameObject("Cam");
            _camGo.AddComponent<Camera>();
            _orbit = _camGo.AddComponent<OrbitCamera>();

            _playerGo = new GameObject("Player");
            // WasdMovement [RequireComponent(NavMeshAgent)] — add the agent so AddComponent succeeds.
            _playerGo.AddComponent<UnityEngine.AI.NavMeshAgent>();
            _wasd = _playerGo.AddComponent<WasdMovement>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camGo);
            Object.DestroyImmediate(_playerGo);
        }

        [Test]
        public void Catalog_WiresExpectedSettings_AndAvailability()
        {
            var reg = SettingsCatalog.Build(_orbit, _wasd);

            Assert.IsTrue(reg.Has(SettingsCatalog.ZoomRangeId), "zoom range registered (AC3)");
            Assert.IsTrue(reg.Has(SettingsCatalog.PitchRangeId), "view-angle range registered (AC3)");
            Assert.IsTrue(reg.Has(SettingsCatalog.WalkSpeedId), "walk speed registered (AC3)");

            Assert.IsTrue(reg.Get(SettingsCatalog.ZoomRangeId).Available, "zoom is a LIVE setting");
            Assert.IsTrue(reg.Get(SettingsCatalog.WalkSpeedId).Available, "walk speed is LIVE");
            // Extension hooks: present but greyed, NOT bound to a real param (AC3).
            Assert.IsFalse(reg.Get(SettingsCatalog.JumpHeightId).Available, "jump height is a greyed hook (AC3)");
            Assert.IsFalse(reg.Get(SettingsCatalog.ToolSpeedId).Available, "tool-use speed is a greyed hook (AC3)");
        }

        [Test]
        public void WalkSpeed_DrivesWasdMoveSpeed_Live()
        {
            var reg = SettingsCatalog.Build(_orbit, _wasd);
            var walk = (FloatSettingEntry)reg.Get(SettingsCatalog.WalkSpeedId);

            walk.SetValue(7f);

            Assert.AreEqual(7f, _wasd.moveSpeed, 1e-4f, "the walk-speed slider drives WasdMovement.moveSpeed live (AC3)");
        }

        [Test]
        public void AirControlAccel_DrivesWasdAirControlAccel_Live()
        {
            // 86caambxh regression guard: the `Air-control accel` dev-console row must be registered AND drive
            // WasdMovement.airControlAccel LIVE, so the Sponsor can fine-tune the mid-air A/D nudge in the soak
            // (the direct-tweak handle the ticket asks to keep/verify). BUG CLASS this pins: an unregistered or
            // wrongly-bound row (a knob that doesn't move the airborne steer) leaves the Sponsor guessing again.
            var reg = SettingsCatalog.Build(_orbit, _wasd);
            Assert.IsTrue(reg.Has(SettingsCatalog.AirControlAccelId), "air-control accel row registered (86caambxh)");
            var air = (FloatSettingEntry)reg.Get(SettingsCatalog.AirControlAccelId);
            Assert.IsTrue(air.Available, "air-control accel is a LIVE setting (not a greyed hook)");

            air.SetValue(3f);
            Assert.AreEqual(3f, _wasd.airControlAccel, 1e-4f,
                "the air-control-accel slider must drive WasdMovement.airControlAccel live (86caambxh dial handle)");
        }

        [Test]
        public void ShippedAirControlAccelDefault_IsThe9()
        {
            // 86caambxh: the SHIPPED default is 9 u/s² (Sponsor soak 2026-07-01 raised it 5 → 9 for a snappier
            // mid-air sideways air-steer). A fresh WasdMovement (the component initializer) must report 9 so a
            // normal soak/CI build runs the intended nudge. BUG CLASS this pins: a silent regress of the default
            // back to a lower value (5/8) would quietly weaken the mid-air air-steer the Sponsor soak-locked at 9.
            Assert.AreEqual(9f, _wasd.airControlAccel, 1e-4f,
                "a fresh WasdMovement must default airControlAccel to 9 u/s² (86caambxh, Sponsor soak 2026-07-01).");
        }

        [Test]
        public void ZoomRange_ClampsLiveCameraDistance_AC4()
        {
            _orbit.minDistance = 6f;
            _orbit.maxDistance = 26f;
            _orbit.SetDistance(20f); // live distance 20, inside the range

            var reg = SettingsCatalog.Build(_orbit, _wasd);
            var zoom = (RangeSettingEntry)reg.Get(SettingsCatalog.ZoomRangeId);

            // Tighten the MAX below the live distance — the OrbitCamera clamps its distance to the new range.
            zoom.SetMax(12f);

            Assert.AreEqual(12f, _orbit.maxDistance, 1e-4f, "the range MAX drove orbit.maxDistance (AC4)");
            // SetDistance re-clamps to [minDistance, maxDistance]; assert the live distance can't exceed the new max.
            _orbit.SetDistance(20f);
            Assert.LessOrEqual(_orbit.Distance, 12f, "the live camera distance clamps to the tightened range (AC4)");
        }

        [Test]
        public void PitchRange_ClampsLiveCameraPitch_AC4()
        {
            _orbit.minPitch = 8f;
            _orbit.maxPitch = 70f;
            _orbit.SetPitch(60f);

            var reg = SettingsCatalog.Build(_orbit, _wasd);
            var pitch = (RangeSettingEntry)reg.Get(SettingsCatalog.PitchRangeId);

            pitch.SetMax(40f);

            Assert.AreEqual(40f, _orbit.maxPitch, 1e-4f, "the range MAX drove orbit.maxPitch (AC4)");
            _orbit.SetPitch(60f);
            Assert.LessOrEqual(_orbit.Pitch, 40f, "the live camera pitch clamps to the tightened range (AC4)");
        }

        [Test]
        public void Catalog_TolerantOfNullTargets()
        {
            // A bare rig may pass only some targets; the catalog must never null-ref, just skip those settings.
            var regNoCam = SettingsCatalog.Build(null, _wasd);
            Assert.IsFalse(regNoCam.Has(SettingsCatalog.ZoomRangeId), "no camera → no zoom range");
            Assert.IsTrue(regNoCam.Has(SettingsCatalog.WalkSpeedId), "walk speed still present");

            var regNone = SettingsCatalog.Build(null, null);
            // The unavailable hooks are param-free, so they register regardless of targets.
            Assert.IsTrue(regNone.Has(SettingsCatalog.JumpHeightId), "param-free hooks register without targets");
        }
    }
}
