using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC6 regression guard for the F7/F9/F10 → DEV-CONSOLE migration (ticket 86caber95): a migrated console
    /// entry drives the SAME live param the old F-key nudge tool did. The console's binding is the pure-C#
    /// registry + entries (no scene, no UIDocument render loop — the headless-time trap, unity-conventions.md),
    /// so the bug CLASS the migration introduces — "the row's setter drives the wrong field, or is a dead knob"
    /// — is pinned here directly against a bare component rig:
    ///   • F7 → OrbitCamera follow gains (followLerp / verticalFollowLerp / airborneFollowLerp / followLeadTime);
    ///   • F9 → CastawayCharacter.groundYOffset (the single-float validation entry) + CastawayArmPose per-axis
    ///          eulers (rightArmEuler / leftArmEuler / runLowerEuler decomposed to pitch/yaw/roll — AC4);
    ///   • F10 → WorldLookTunables seam (fog density + per-channel colour; cloud/mountain/sun scalars) — the seam
    ///           over RenderSettings/skybox-material/collections; the full render effect is the shipped-build
    ///           capture + Sponsor soak, but the SEAM's get/set (dead-knob guard) is pinned here.
    /// The legacy F-key panels stay LIVE in parallel (AC5), so their own PlayMode tests stay green until retired.
    /// </summary>
    public class FKeyMigrationTests
    {
        // Bare in-EditMode component rig (created per-test, torn down in TearDown). No scene, no NavMesh.
        private GameObject _go;

        [TearDown]
        public void Cleanup()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        private T AddComponentOnGo<T>() where T : Component
        {
            if (_go == null) _go = new GameObject("fkey-migration-rig");
            return _go.AddComponent<T>();
        }

        // ===== AC3 (F7) — OrbitCamera follow gains: each row drives the SAME field the F7 tool nudged =====

        [Test]
        public void CameraFollow_RowsDriveOrbitCameraFollowGains_NotADeadKnob()
        {
            var orbit = AddComponentOnGo<OrbitCamera>();
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateCameraFollow(reg, orbit);

            var follow = (FloatSettingEntry)reg.Get(SettingsCatalog.CamFollowLerpId);
            Assert.IsNotNull(follow, "the Cam-follow-lerp row must be registered (F7 migration AC3)");
            follow.SetValue(24f);
            Assert.AreEqual(24f, orbit.followLerp, 1e-4f, "the row drives OrbitCamera.followLerp live (the F7 field)");

            var vert = (FloatSettingEntry)reg.Get(SettingsCatalog.CamVertFollowLerpId);
            vert.SetValue(48f);
            Assert.AreEqual(48f, orbit.verticalFollowLerp, 1e-4f, "the row drives OrbitCamera.verticalFollowLerp");

            var air = (FloatSettingEntry)reg.Get(SettingsCatalog.CamAirborneLerpId);
            air.SetValue(72f);
            Assert.AreEqual(72f, orbit.airborneFollowLerp, 1e-4f, "the row drives OrbitCamera.airborneFollowLerp");

            var lead = (FloatSettingEntry)reg.Get(SettingsCatalog.CamFollowLeadTimeId);
            lead.SetValue(0.1f);
            Assert.AreEqual(0.1f, orbit.followLeadTime, 1e-4f, "the row drives OrbitCamera.followLeadTime");
            // lead-time clamps to the camera's own maxLeadTime (0 = AUTO).
            lead.SetValue(999f);
            Assert.AreEqual(orbit.maxLeadTime, orbit.followLeadTime, 1e-4f, "lead-time clamps to OrbitCamera.maxLeadTime");
        }

        [Test]
        public void CameraFollow_NullOrbit_RegistersNothing_NeverNullRefs()
        {
            var reg = new SettingsRegistry();
            Assert.DoesNotThrow(() => SettingsCatalog.PopulateCameraFollow(reg, null),
                "a null orbit must register nothing (bare rig / camera-less test unaffected)");
            Assert.IsFalse(reg.Has(SettingsCatalog.CamFollowLerpId), "no camera-follow row on a null orbit");
        }

        // ===== AC1 (F9) — ground-Y (single float) + arm-pose per-axis: drive the SAME fields =====

        [Test]
        public void GroundY_RowDrivesCastawayGroundYOffset_TheF9ValidationEntry()
        {
            var castaway = AddComponentOnGo<CastawayCharacter>();
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateArmAndGround(reg, castaway, null);

            var e = (FloatSettingEntry)reg.Get(SettingsCatalog.GroundYOffsetId);
            Assert.IsNotNull(e, "the single-float ground-Y row must register (F9 migration AC1 validation entry)");
            e.SetValue(0.12f);
            Assert.AreEqual(0.12f, castaway.groundYOffset, 1e-4f,
                "the ground-Y row drives CastawayCharacter.groundYOffset live (the SAME field the F9 GROUND-Y target dials)");
            // clamps to the band so a dial can't push a runaway offset.
            Assert.AreEqual(SettingsCatalog.GroundYMax, e.SetValue(9f), 1e-4f, "ground-Y clamps to the band ceiling");
        }

        [Test]
        public void ArmPose_PerAxisRowsDriveTheEulerComponents_AC4Decompose()
        {
            var castaway = AddComponentOnGo<CastawayCharacter>();
            var arm = _go.AddComponent<CastawayArmPose>();
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateArmAndGround(reg, castaway, arm);

            // RIGHT arm — each per-axis row drives its own euler component (AC4 per-axis decompose).
            ((FloatSettingEntry)reg.Get(SettingsCatalog.ArmRightPitchId)).SetValue(-12f);
            ((FloatSettingEntry)reg.Get(SettingsCatalog.ArmRightYawId)).SetValue(-40f);
            ((FloatSettingEntry)reg.Get(SettingsCatalog.ArmRightRollId)).SetValue(7f);
            Assert.AreEqual(new Vector3(-12f, -40f, 7f), arm.rightArmEuler,
                "the R pitch/yaw/roll rows drive the three components of CastawayArmPose.rightArmEuler");

            // LEFT arm.
            ((FloatSettingEntry)reg.Get(SettingsCatalog.ArmLeftPitchId)).SetValue(-5f);
            ((FloatSettingEntry)reg.Get(SettingsCatalog.ArmLeftYawId)).SetValue(22f);
            ((FloatSettingEntry)reg.Get(SettingsCatalog.ArmLeftRollId)).SetValue(3f);
            Assert.AreEqual(new Vector3(-5f, 22f, 3f), arm.leftArmEuler,
                "the L pitch/yaw/roll rows drive the three components of CastawayArmPose.leftArmEuler");

            // RUN arm-lower.
            ((FloatSettingEntry)reg.Get(SettingsCatalog.RunLowerPitchId)).SetValue(-10f);
            ((FloatSettingEntry)reg.Get(SettingsCatalog.RunLowerYawId)).SetValue(12f);
            ((FloatSettingEntry)reg.Get(SettingsCatalog.RunLowerRollId)).SetValue(-42f);
            Assert.AreEqual(new Vector3(-10f, 12f, -42f), arm.runLowerEuler,
                "the run-lower pitch/yaw/roll rows drive the three components of CastawayArmPose.runLowerEuler");

            // The dial contract: writing an arm euler freezes the deg-field seed so a RebuildCached can't clobber
            // it (mirrors the F9 tool). If the seed were left on, RebuildCached would overwrite rightArmEuler.
            Assert.IsFalse(arm.seedEulersFromDegFields,
                "an arm-pose row write clears seedEulersFromDegFields (the F9 dial contract — else RebuildCached clobbers the dial)");
        }

        [Test]
        public void ArmAndGround_NullTargets_SkipOnlyTheirOwnRows_NeverNullRef()
        {
            var reg = new SettingsRegistry();
            // Both null → nothing registered.
            Assert.DoesNotThrow(() => SettingsCatalog.PopulateArmAndGround(reg, null, null));
            Assert.IsFalse(reg.Has(SettingsCatalog.GroundYOffsetId), "no ground-Y row on a null castaway");
            Assert.IsFalse(reg.Has(SettingsCatalog.ArmRightPitchId), "no arm rows on a null arm pose");
        }

        // ===== AC2 (F10) — world-look seam: the row's get/set drives the SAME live look state =====

        [Test]
        public void WorldLook_RowsDriveTheSeam_FogDensityAndColour_SeamKill()
        {
            var seam = AddComponentOnGo<WorldLookTunables>();
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateWorldLook(reg, seam);

            // Fog density — drives RenderSettings.fogDensity via the seam.
            var density = (FloatSettingEntry)reg.Get(SettingsCatalog.FogDensityId);
            Assert.IsNotNull(density, "the fog-density row must register (F10 migration AC2)");
            density.SetValue(0.01f);
            Assert.AreEqual(0.01f, RenderSettings.fogDensity, 1e-5f, "the fog-density row drives RenderSettings.fogDensity (the F10 field)");

            // Fog colour R channel (AC4 per-channel decompose) — drives RenderSettings.fogColor.r.
            var r = (FloatSettingEntry)reg.Get(SettingsCatalog.FogColorRId);
            r.SetValue(0.42f);
            Assert.AreEqual(0.42f, RenderSettings.fogColor.r, 1e-4f, "the fog-colour-R row drives RenderSettings.fogColor.r");
            Assert.IsNotNull(reg.Get(SettingsCatalog.FogColorGId), "fog colour G row registered");
            Assert.IsNotNull(reg.Get(SettingsCatalog.FogColorBId), "fog colour B row registered");
        }

        [Test]
        public void WorldLook_CloudAndMountainAndSun_RowsRegistered_AndSeamGetSetRoundTrips()
        {
            var seam = AddComponentOnGo<WorldLookTunables>();
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateWorldLook(reg, seam);

            // Cloud scale — the seam holds the multiplier (no LP_Cloud in the bare rig, so ApplyClouds is a
            // no-op loop, but the seam's stored value must round-trip so the row isn't a dead knob).
            var cloud = (FloatSettingEntry)reg.Get(SettingsCatalog.CloudScaleId);
            cloud.SetValue(2f);
            Assert.AreEqual(2f, seam.CloudScale, 1e-4f, "cloud-scale row round-trips through the seam");

            var mtnWarmth = (FloatSettingEntry)reg.Get(SettingsCatalog.MtnWarmthId);
            mtnWarmth.SetValue(0.3f);
            Assert.AreEqual(0.3f, seam.MountainWarmth, 1e-4f, "mountain-warmth row round-trips through the seam");

            var mtnBright = (FloatSettingEntry)reg.Get(SettingsCatalog.MtnBrightnessId);
            mtnBright.SetValue(1.4f);
            Assert.AreEqual(1.4f, seam.MountainBrightness, 1e-4f, "mountain-brightness row round-trips through the seam");

            // Every F10 dial is present (the full migration surface).
            Assert.IsNotNull(reg.Get(SettingsCatalog.CloudAltitudeId), "cloud-altitude row registered");
            Assert.IsNotNull(reg.Get(SettingsCatalog.MtnDistanceId), "mountain-distance row registered");
            Assert.IsNotNull(reg.Get(SettingsCatalog.MtnPeakScaleId), "mountain-peak-scale row registered");
            Assert.IsNotNull(reg.Get(SettingsCatalog.SunElevationId), "sun-elevation row registered");
            Assert.IsNotNull(reg.Get(SettingsCatalog.SunSizeId), "sun-size row registered");
            Assert.IsNotNull(reg.Get(SettingsCatalog.SkyHorizonRId), "sky-horizon-R row registered");
        }

        [Test]
        public void WorldLook_NullSeam_RegistersNothing_NeverNullRefs()
        {
            var reg = new SettingsRegistry();
            Assert.DoesNotThrow(() => SettingsCatalog.PopulateWorldLook(reg, null),
                "a null world-look seam must register nothing (bare rig / world-less test unaffected)");
            Assert.IsFalse(reg.Has(SettingsCatalog.FogDensityId), "no world-look rows on a null seam");
        }
    }
}
