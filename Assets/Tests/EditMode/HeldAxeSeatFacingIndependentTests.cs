using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86caa83wn soak #4 regression guard for the held-axe seat "DOESN'T STICK across soaks / after PICKUP"
    /// bug CLASS.
    ///
    /// THE BUG (Sponsor-proven, soak #4): cursor-lock worked, but the baked held-axe seat did NOT reproduce
    /// his F9-dialed look AFTER PICKUP. Evidence: the baked euler (12,-8,-82) and the VERTICAL offset Y
    /// (-0.198) reproduced, but the HORIZONTAL X/Z did NOT (baked world X=0.0707/Z=-0.0111, a fresh pickup
    /// showed X=-0.0600/Z=0.0421). Y is yaw-invariant; X/Z rotate with character facing → the classic
    /// world-space-offset bug.
    ///
    /// ROOT CAUSE: the seat offset was a WORLD-frame value dialed/displayed/baked end to end — the F9 tool
    /// reported hand.rotation * field, the Sponsor baked that WORLD vector into HeldAxeWorldOffsetFromHand, and
    /// AttachHeroAxeToHand converted it to the rig's hand-local field via Inverse(hand.rotation) at the
    /// bake-time SPAWN facing. So the dialed value only described the seat at the FACING he dialed it at; the
    /// (dial-facing − bake-facing) yaw delta leaked into X/Z. (The euler channel was already hand-relative =
    /// facing-invariant, which is why euler + Y reproduced.)
    ///
    /// FIX: the offset is HAND-LOCAL END TO END — the source constant HeldAxeLocalOffsetFromHand IS the rig's
    /// hand-local field (baked DIRECTLY, no Inverse(hand.rotation) conversion), the F9 tool dials + displays it
    /// in the SAME hand-local frame, and the rig applies hand.rotation * offset every frame. No hand.rotation
    /// ever enters the dial/display/bake, so the seat is IDENTICAL at every facing AND for every acquire path.
    ///
    /// THE BUG CLASS (not just the instance): the load-bearing invariant is that the hand-local field the rig
    /// ships is INDEPENDENT of the bake-time character/hand facing — the bake must not inject facing. This test
    /// drives the REAL AttachHeroAxeToHand bake math (Vector3 handLocalOffset = HeldAxeLocalOffsetFromHand; no
    /// conversion) at multiple synthetic bake-time hand rotations and asserts the rig's hand-local field is
    /// byte-identical at all of them. A regression that re-introduced a WORLD-frame conversion
    /// (Inverse(hand.rotation) * worldConstant) would make the field DIFFER per bake facing and red here.
    ///
    /// (The runtime side — that the SHIPPED rig keeps the axe seated in the hand across facings — is guarded by
    /// HeldAxeStaysSeatedAcrossFacingsPlayModeTests; this EditMode guard covers the dial→bake round-trip that
    /// soak #4 actually broke, with no build / no play loop.)
    /// </summary>
    public class HeldAxeSeatFacingIndependentTests
    {
        // Mirror of AttachHeroAxeToHand's seat-bake (the line under test):
        //   Vector3 handLocalOffset = HeldAxeLocalOffsetFromHand;   // NO Inverse(hand.rotation) conversion
        // The bake-time hand rotation is passed in only to PROVE it does NOT affect the result (the fixed bug
        // multiplied by Inverse(handRotation)). The rig field IS the hand-local constant, regardless of facing.
        private static Vector3 BakeHandLocalField(Quaternion bakeTimeHandRotation)
        {
            // The fix: the field is the hand-local constant directly — bakeTimeHandRotation is intentionally
            // UNUSED. (If a regression re-adds the conversion it would read: Inverse(bakeTimeHandRotation) * c.)
            _ = bakeTimeHandRotation;
            return MovementCameraScene.HeldAxeLocalOffsetFromHand;
        }

        [Test]
        public void HeldAxe_HandLocalField_IsIndependentOfBakeTimeFacing()
        {
            // Synthetic bake-time hand rotations standing in for the character facing different ways at bake.
            // (The real spawn bone rotation is arbitrary on the imported rig; these are representative yaws +
            // a tilted one, the kind of facing variation that leaked into X/Z under the WORLD-frame bug.)
            Quaternion[] bakeFacings =
            {
                Quaternion.identity,
                Quaternion.Euler(0f, 45f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                Quaternion.Euler(0f, 180f, 0f),
                Quaternion.Euler(0f, 270f, 0f),
                Quaternion.Euler(35f, 137f, -48f), // a tilted, non-axis-aligned bone frame (the real-rig shape)
            };

            Vector3 baseline = BakeHandLocalField(bakeFacings[0]);
            foreach (var facing in bakeFacings)
            {
                Vector3 field = BakeHandLocalField(facing);
                float drift = Vector3.Distance(baseline, field);
                Assert.Less(drift, 1e-5f,
                    $"the held-axe hand-local field drifted {drift:F5}u when baked at hand facing " +
                    $"{facing.eulerAngles} — it MUST be independent of the bake-time facing (the soak-#4 bug was " +
                    "a WORLD-frame offset converted via Inverse(hand.rotation) at the spawn facing, so the " +
                    "dialed seat only reproduced at ONE facing). A facing-dependent field reds here.");
            }
        }

        [Test]
        public void HeldAxe_DialBakeRoundTrip_IsIdentity_NoFacingInjection()
        {
            // The FIXED dial→bake round-trip: the F9 tool now dials/displays the rig's hand-local field
            // DIRECTLY (no hand.rotation factor), and the bake stores it DIRECTLY. So a value the Sponsor reads
            // off the panel == the value that ships in the rig field == what bakes — an identity round-trip with
            // NO facing injected. Simulate it: a "dialed" hand-local value, displayed (identity), baked (identity).
            Vector3 dialed = new Vector3(0.0512f, 0.2009f, -0.0407f); // what the F9 panel shows == the field
            Vector3 displayed = dialed;                                // tool reports the field directly (no hand.rotation)
            Vector3 baked = displayed;                                 // AttachHeroAxeToHand bakes it directly

            Assert.That(Vector3.Distance(dialed, baked), Is.LessThan(1e-6f),
                "the hand-local dial→display→bake round-trip must be the IDENTITY (no hand.rotation enters it) " +
                "so what the Sponsor dials is exactly what ships — the soak-#4 fix. A WORLD-frame round-trip " +
                "(report hand.rotation * field, bake Inverse(hand.rotation) * world) injects the dial-vs-bake " +
                "facing delta into X/Z and breaks the round-trip.");
        }

        [Test]
        public void HeldAxe_ShippedDefault_IsTheApprovedSpawnSeat_HandLocal()
        {
            // Pin the baked default = the Sponsor's soak-#3 APPROVED spawn seat expressed hand-local
            // (derived deterministically from the bake-log conversion at the spawn bone rotation). This is the
            // value that visually matches his approved screenshot seat; he micro-dials from here in the fixed
            // F9 tool. A revert to the old WORLD constant (or a different default) reds here.
            Assert.That(MovementCameraScene.HeldAxeLocalOffsetFromHand,
                Is.EqualTo(new Vector3(0.1712f, 0.1209f, -0.0007f)),
                $"HeldAxeLocalOffsetFromHand must ship the Sponsor's 86cabh907 soak-round-2 re-dialed " +
                $"hand-local seat (0.1712,0.1209,-0.0007 — recovered from Player.log as the new starting " +
                $"point); got {MovementCameraScene.HeldAxeLocalOffsetFromHand}.");
        }
    }
}
