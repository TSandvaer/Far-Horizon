using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode regression guards for 86caffwv5 ROUND-5 (soak-5 fixes):
    ///   • item 2 — the SWORD swing speed bump (Sponsor: "sword swing is way too slow, dagger works fine").
    ///   • item 3b — the F9 nudge GIMBAL fix: <see cref="AxeNudgeTool.ComposeLocalRot"/> makes every orientation
    ///     reachable at high pitch (the Sponsor's pickaxe -362° yaw hunt through a full circle without landing).
    /// </summary>
    public class SwingsRound5Tests
    {
        // ============================ item 2 — sword swing speed ============================

        [Test]
        public void SwordSwing_ClassMultiplier_BumpedTo1_5_Soak5()
        {
            Assert.AreEqual(1.5f, CastawayCharacter.SwingSpeedForClass(CastawayCharacter.WeaponClassSword), 1e-4f,
                "soak-5: the sword swing CLASS multiplier is bumped 1.0→1.5 (mirrors the pickaxe soak-3 seam) — " +
                "the Sponsor's 'sword swing is way too slow' fix.");
        }

        [Test]
        public void SwordSwing_EffectivePlayback_ReadsCloseToTheDagger()
        {
            // Wood sword attackSpeed 1.15 × 1.5 = 1.725 effective playback (was 1.15 at SwingSpeed 1.0 — the drag).
            float sword = CastawayCharacter.EffectiveSwingPlaybackSpeed(1.15f, CastawayCharacter.WeaponClassSword);
            Assert.Greater(sword, 1.5f, "the wood sword now plays >1.5× (was 1.15× — 'way too slow')");

            // The dagger reads 'fast enough' at attackSpeed 1.8 × SwingSpeed 1.0 = 1.8. The sped-up sword lands
            // close to it — the Sponsor's cadence bar ('dagger works fine and is fast enough').
            float dagger = CastawayCharacter.EffectiveSwingPlaybackSpeed(1.8f, CastawayCharacter.WeaponClassDagger);
            Assert.That(sword, Is.EqualTo(dagger).Within(0.15f),
                "the sword's effective swing cadence now reads close to the dagger's responsiveness");
        }

        // ============================ item 3b — F9 gimbal fix ============================

        [Test]
        public void ComposeLocalRot_ReproducesTheLocalFrameQuaternionCompose()
        {
            Vector3 cur = new Vector3(-68f, 12f, 34f); // the pickaxe high-pitch zone
            Vector3 delta = new Vector3(0f, 2f, 0f);   // one yaw nudge
            Vector3 result = AxeNudgeTool.ComposeLocalRot(cur, delta);
            Quaternion expected = Quaternion.Euler(cur) * Quaternion.Euler(delta);
            float ang = Quaternion.Angle(Quaternion.Euler(result), expected);
            Assert.Less(ang, 0.5f,
                "ComposeLocalRot's stored euler reproduces the local-frame quaternion composition EXACTLY (baked value is exact)");
        }

        [Test]
        public void ComposeLocalRot_ZeroDelta_ReturnsInputUntouched()
        {
            Vector3 cur = new Vector3(-70f, 20f, 0f);
            Assert.AreEqual(cur, AxeNudgeTool.ComposeLocalRot(cur, Vector3.zero),
                "a pure position nudge (zero rotation delta) must NOT round-trip the euler (no drift on a move-only keypress)");
        }

        [Test]
        public void ComposeLocalRot_ReachesEveryOrientation_AtHighPitch_NoGimbalDeadZone()
        {
            // The DEAD ZONE this guards: at pitch ~-70..-80, adding to the yaw euler COMPONENT (the old
            // `_liveEuler += dr`) is degenerate — the Sponsor's pickaxe -362° yaw hunt circled the full range
            // without ever reaching his target. Local-frame quaternion composition rotates by exactly the step
            // about the weapon's OWN axis each keypress, so a yaw sweep at high pitch spans DISTINCT orientations
            // across a wide arc — every orientation reachable.
            Vector3 e = new Vector3(-74f, 0f, 40f); // high pitch (worse than the pickaxe's -68)
            Quaternion start = Quaternion.Euler(e);
            float maxAngle = 0f;
            for (int i = 0; i < 90; i++) // 90 × 2° = a 180° local-yaw sweep
            {
                e = AxeNudgeTool.ComposeLocalRot(e, new Vector3(0f, 2f, 0f));
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(start, Quaternion.Euler(e)));
            }
            Assert.Greater(maxAngle, 150f,
                "a 180° local-yaw sweep at high pitch reaches orientations >150° from the start — the gimbal dead zone is gone");
        }
    }
}
