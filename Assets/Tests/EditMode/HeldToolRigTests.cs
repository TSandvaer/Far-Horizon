using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the shared HeldTool rig generalization (ticket 86cabh907). The held-axe rig +
    /// visibility gate were generalized into HeldToolRig / HeldTool so ANY family item mounts via the SAME
    /// locked seat the axe uses — WITHOUT regressing the soak-locked axe seat.
    ///
    /// Regression-guard intent:
    ///   - The axe rig/gate MUST stay subclasses of the shared base (so "one shared rig" holds + a future
    ///     weapon reuses it). A refactor that detaches them reds here.
    ///   - The shared base must seat a tool at hand.position + hand.rotation*offset / hand.rotation*Euler —
    ///     the exact raw-hand-follow contract the axe soak locked.
    ///   - The axe's soak-locked seat constants (MovementCameraScene) MUST be byte-unchanged — the brief's
    ///     "generalize, don't regress the axe seat" gate. A drift in any locked value reds here.
    /// </summary>
    public class HeldToolRigTests
    {
        [Test]
        public void HeldAxeRig_IsA_HeldToolRig_SharedRig()
        {
            Assert.IsTrue(typeof(HeldToolRig).IsAssignableFrom(typeof(HeldAxeRig)),
                "HeldAxeRig must derive from the shared HeldToolRig — the seat math lives once in the base " +
                "so any family item (knife/sword/spear) mounts via the SAME locked seat.");
        }

        [Test]
        public void HeldAxe_IsA_HeldTool_SharedGate()
        {
            Assert.IsTrue(typeof(HeldTool).IsAssignableFrom(typeof(HeldAxe)),
                "HeldAxe must derive from the shared HeldTool visibility gate.");
        }

        [Test]
        public void SharedRig_SeatsTool_AtHandLocalOffset_AndHandRelativeEuler()
        {
            // Build a hand bone with a non-trivial pose + a tool child driven by the shared rig; pump
            // LateUpdate via reflection (it's protected) and assert the seat formula holds exactly.
            var hand = new GameObject("hand").transform;
            hand.position = new Vector3(1f, 2f, 3f);
            hand.rotation = Quaternion.Euler(20f, -35f, 14f);

            var toolGo = new GameObject("tool");
            var rig = toolGo.AddComponent<HeldToolRig>();
            rig.hand = hand;
            rig.seatOffsetFromHand = new Vector3(0.13f, 0.14f, 0.06f);
            rig.seatEuler = new Vector3(12f, -8f, -82f);
            rig.followDamp = 0f;

            InvokeLateUpdate(rig);

            Vector3 expectedPos = hand.position + hand.rotation * rig.seatOffsetFromHand;
            Quaternion expectedRot = hand.rotation * Quaternion.Euler(rig.seatEuler);
            Assert.That((toolGo.transform.position - expectedPos).magnitude, Is.LessThan(1e-4f),
                "the shared rig must seat the tool at hand.position + hand.rotation * seatOffsetFromHand " +
                "(hand-LOCAL offset rotated by the raw hand — the facing-invariant seat).");
            Assert.That(Quaternion.Angle(toolGo.transform.rotation, expectedRot), Is.LessThan(0.05f),
                "the shared rig must seat the tool at hand.rotation * Euler(seatEuler) — hand-relative.");

            Object.DestroyImmediate(toolGo);
            Object.DestroyImmediate(hand.gameObject);
        }

        [Test]
        public void AxeSubclass_DrivesViaSharedBase_PreservesItsSerializedSeatFields()
        {
            // The axe keeps worldOffsetFromHand/relEuler (serialized + F9-nudged); the subclass syncs them
            // into the base seat before driving, so an F9 runtime nudge still moves the axe. Assert the
            // subclass produces the SAME seat as the base from its own field names.
            var hand = new GameObject("hand2").transform;
            hand.position = new Vector3(-2f, 1f, 0.5f);
            hand.rotation = Quaternion.Euler(5f, 120f, -10f);

            var axeGo = new GameObject("axe");
            var rig = axeGo.AddComponent<HeldAxeRig>();
            rig.hand = hand;
            rig.worldOffsetFromHand = new Vector3(0.1312f, 0.1409f, 0.0593f);
            rig.relEuler = new Vector3(12f, -8f, -82f);

            InvokeLateUpdate(rig);

            Vector3 expectedPos = hand.position + hand.rotation * rig.worldOffsetFromHand;
            Quaternion expectedRot = hand.rotation * Quaternion.Euler(rig.relEuler);
            Assert.That((axeGo.transform.position - expectedPos).magnitude, Is.LessThan(1e-4f),
                "HeldAxeRig must seat the axe from its own worldOffsetFromHand (synced into the base seat) — " +
                "an F9 runtime nudge must still move the axe.");
            Assert.That(Quaternion.Angle(axeGo.transform.rotation, expectedRot), Is.LessThan(0.05f),
                "HeldAxeRig rotation must come from its own relEuler.");

            Object.DestroyImmediate(axeGo);
            Object.DestroyImmediate(hand.gameObject);
        }

        [Test]
        public void AxeSeatConstants_ShipTheSoakRound2StartingPoint()
        {
            // 86cabh907 soak ROUND 2 (PR #100): the Sponsor re-dialed the held-axe seat in the shipped build;
            // the recovered Player.log values are the NEW STARTING POINT (re-confirmed in the re-soak, then a
            // later pass bakes the lock). Pin them so an accidental revert to the prior soak-#5 lock reds here.
            Assert.AreEqual(new Vector3(0.1712f, 0.1209f, -0.0007f), MovementCameraScene.HeldAxeLocalOffsetFromHand,
                "HeldAxeLocalOffsetFromHand must ship the soak-round-2 re-dialed hand-local seat offset.");
            Assert.AreEqual(new Vector3(-186f, -168f, -84f), MovementCameraScene.HeldAxeRelEuler,
                "HeldAxeRelEuler must ship the soak-round-2 re-dialed hand-relative grip.");
            Assert.AreEqual(0.45f, MovementCameraScene.HeldAxeLocalScaleUniform,
                "HeldAxeLocalScaleUniform must stay the established in-hand reference scale.");
            Assert.AreEqual(0f, MovementCameraScene.HeldAxeFollowDamp,
                "HeldAxeFollowDamp must ship at 0 (the per-step arm-swing is visible — the Sponsor's choice).");
        }

        private static void InvokeLateUpdate(HeldToolRig rig)
        {
            // Awake (protected) wires the hand fallback; LateUpdate (protected) drives the seat.
            var awake = typeof(HeldToolRig).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(rig, null);
            // call the MOST-DERIVED LateUpdate (HeldAxeRig overrides it to sync its alias fields first).
            var lu = rig.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            lu.Invoke(rig, null);
        }
    }
}
