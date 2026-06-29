using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Binding-map guard for the HELD-WEAPON PLACEMENT settings (ticket 86caffwuz). Proves
    /// SettingsCatalog.PopulateHeldWeapon:
    ///   • registers the SEVEN held-weapon rows (pos X/Y/Z, rot pitch/yaw/roll, scale) as LIVE sliders bound to
    ///     the CURRENTLY-held weapon's seat through the <see cref="HeldWeaponPlacement"/> seam;
    ///   • the rows READ and WRITE the real axe rig fields (worldOffsetFromHand / relEuler) when the axe is the
    ///     current held weapon — so the unified console dials the SAME seat the equip path uses (no parallel
    ///     attach mechanism — the ticket integration constraint);
    ///   • is a NO-OP with a null seam (the rows are absent — a held-weapon-less rig / bare test never null-refs,
    ///     no dead knob — the PopulateThirst/PopulateStones de-collision precedent);
    ///   • is registered via the SEPARATE method (NOT by appending to Populate).
    ///
    /// The axe path is the one weapon that exists today, so it is exercised directly here; the knife/sword/spear
    /// routing (which needs the runtime weapon-cycle index) is exercised in PlayMode (HeldWeaponDialPlayModeTests).
    /// Drives the real components (plain public fields) so this proves the bindings hit the actual seat.
    /// </summary>
    public class SettingsCatalogHeldWeaponTests
    {
        private GameObject _axeGo;
        private HeldAxeRig _rig;
        private HeldWeaponCycleDebug _cycle;
        private HeldWeaponPlacement _placement;

        [SetUp]
        public void SetUp()
        {
            // The seat object as authored: the axe rig + the weapon-cycle + the placement seam on one GameObject
            // (mirrors MovementCameraScene.AttachHeroAxeToHand). The cycle is NOT started (no Awake play loop), so
            // CurrentIndex defaults to 0 (the axe) and the placement routes to the rig — exactly the axe path.
            _axeGo = new GameObject("HeroAxe");
            _rig = _axeGo.AddComponent<HeldAxeRig>();
            _rig.worldOffsetFromHand = new Vector3(0.1712f, 0.1209f, -0.0007f); // the committed axe seat defaults
            _rig.relEuler = new Vector3(-186f, -168f, -84f);
            _cycle = _axeGo.AddComponent<HeldWeaponCycleDebug>();
            _placement = _axeGo.AddComponent<HeldWeaponPlacement>();
            _placement.axeRig = _rig;
            _placement.weaponCycle = _cycle;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_axeGo);
            foreach (var id in new[]
            {
                SettingsCatalog.HeldPosXId, SettingsCatalog.HeldPosYId, SettingsCatalog.HeldPosZId,
                SettingsCatalog.HeldPitchId, SettingsCatalog.HeldYawId, SettingsCatalog.HeldRollId,
                SettingsCatalog.HeldScaleId,
            })
                PlayerPrefs.DeleteKey("fh.settings." + id);
        }

        [Test]
        public void PopulateHeldWeapon_RegistersSevenLiveRows()
        {
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, _placement);

            foreach (var id in new[]
            {
                SettingsCatalog.HeldPosXId, SettingsCatalog.HeldPosYId, SettingsCatalog.HeldPosZId,
                SettingsCatalog.HeldPitchId, SettingsCatalog.HeldYawId, SettingsCatalog.HeldRollId,
                SettingsCatalog.HeldScaleId,
            })
            {
                Assert.IsTrue(reg.Has(id), $"held-weapon row '{id}' is registered (86caffwuz)");
                Assert.IsTrue(reg.Get(id).Available, $"held-weapon row '{id}' is LIVE (the settings panel is merged)");
                Assert.AreEqual(SettingEntry.Archetype.Slider, reg.Get(id).Kind,
                    $"held-weapon row '{id}' is a mouse SLIDER (Danish-keyboard-safe — no key punctuation)");
            }
        }

        [Test]
        public void HeldRows_ReadAndWriteTheAxeRigSeat_WhenAxeIsCurrent()
        {
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, _placement);

            // READ: the position rows reflect the live axe rig offset (the committed seat).
            var posX = (FloatSettingEntry)reg.Get(SettingsCatalog.HeldPosXId);
            var posY = (FloatSettingEntry)reg.Get(SettingsCatalog.HeldPosYId);
            var posZ = (FloatSettingEntry)reg.Get(SettingsCatalog.HeldPosZId);
            Assert.AreEqual(0.1712f, posX.Value, 1e-3f, "pos X reads HeldAxeRig.worldOffsetFromHand.x");
            Assert.AreEqual(0.1209f, posY.Value, 1e-3f, "pos Y reads HeldAxeRig.worldOffsetFromHand.y");
            Assert.AreEqual(-0.0007f, posZ.Value, 1e-3f, "pos Z reads HeldAxeRig.worldOffsetFromHand.z");

            // WRITE: dialing a position row drives the SAME rig field the equip path uses (no parallel seat).
            posY.SetValue(0.2f);
            Assert.AreEqual(0.2f, _rig.worldOffsetFromHand.y, 1e-4f,
                "the held pos-Y slider drives HeldAxeRig.worldOffsetFromHand.y live (one shared seat)");

            // ROTATION rows read + write relEuler (full ±360 band — the axe relEuler accumulates raw past ±180).
            var roll = (FloatSettingEntry)reg.Get(SettingsCatalog.HeldRollId);
            Assert.AreEqual(-84f, roll.Value, 1e-2f, "roll reads HeldAxeRig.relEuler.z");
            roll.SetValue(-90f);
            Assert.AreEqual(-90f, _rig.relEuler.z, 1e-3f, "the held roll slider drives HeldAxeRig.relEuler.z live");
        }

        [Test]
        public void AxeScaleRow_DefaultsToOne_AndLeavesSeatLockedWhenUntouched()
        {
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, _placement);
            var scale = (FloatSettingEntry)reg.Get(SettingsCatalog.HeldScaleId);

            // The axe scale is the LOCKED channel: the row reports 1.0 (a multiplier of the locked baseline), so
            // leaving it untouched never regresses the praised axe grip (bar #6).
            Assert.AreEqual(1f, scale.Value, 1e-3f,
                "the axe held-scale row defaults to 1.0x (multiplier of the locked mesh-holder baseline)");
        }

        [Test]
        public void PopulateHeldWeapon_NullSeam_NoHeldRows()
        {
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.HeldPosXId),
                "with no HeldWeaponPlacement seam, the held-weapon rows are absent (no dead knob, no null-ref)");
            Assert.IsFalse(reg.Has(SettingsCatalog.HeldScaleId), "no seam → no held scale row");
        }

        [Test]
        public void PopulateHeldWeapon_DirectCall_AddsRowsOnce_NotViaPopulate()
        {
            // The de-collision precedent: the rows are registered by PopulateHeldWeapon, NOT by Populate.
            var reg = new SettingsRegistry();
            SettingsCatalog.Populate(reg, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.HeldPosXId),
                "Populate alone must NOT add held-weapon rows (they live in PopulateHeldWeapon)");

            SettingsCatalog.PopulateHeldWeapon(reg, _placement);
            int count = 0;
            foreach (var e in reg.Entries) if (e.Id == SettingsCatalog.HeldPosXId) count++;
            Assert.AreEqual(1, count, "PopulateHeldWeapon adds exactly one pos-X row (no duplicate)");
        }
    }
}
