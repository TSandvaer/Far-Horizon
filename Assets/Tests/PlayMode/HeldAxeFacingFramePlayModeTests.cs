using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// 86ca9xz00 regression guard for the held-axe FACING-FOLLOW — KEPT through the 86ca9zcjn follow-the-arm
    /// change. The axe now rides the RAW hand bone (the swing-stabilizer / grip-anchor is REMOVED), so facing
    /// passes through the raw hand directly: when the castaway turns, the hand bone's world rotation turns and
    /// the axe (riding it) turns with it. This test pins that the facing-follow contract STILL HOLDS under the
    /// raw-hand driver.
    ///
    /// ROOT TOPOLOGY (the real hierarchy): CastawayCharacter applies facing to its MODEL CHILD
    /// (_model.localRotation = Euler(0, bodyYaw, 0)), NOT to its root ("the visual owns facing"). The hand bone
    /// lives under _model, so a facing yaw on _model rotates the hand bone, and the raw-follow axe rotates with it.
    ///
    /// VERIFICATION DISCIPLINE (load-bearing — unity-conventions.md §Editor-vs-runtime / 86ca9xz00): a
    /// facing-tracking prop MUST be checked by a DYNAMIC facing change (rotate the character through headings
    /// over MULTIPLE frames), NEVER static per-facing snapshots — a static snapshot could let a stabilizer
    /// settle to each pinned facing → false-green (the trap that shipped the prior facing-lag bug). This test
    /// drives facing DYNAMICALLY (a continuous sweep) and asserts (a) the axe's hand-local seat is invariant
    /// across the sweep, and (b) the axe's WORLD rotation actually tracks facing (a frozen axe is also wrong).
    /// </summary>
    public class HeldAxeFacingFramePlayModeTests
    {
        private GameObject _root;    // the CastawayCharacter root — NEVER yaws (the agent owns position, not rotation)
        private Transform _model;    // the FBX model child — YAWS with facing (the real _model.localRotation path)
        private Transform _hand;     // the hand bone under the model — arm-swings in the model-local frame
        private Transform _axe;
        private HeldAxeRig _rig;

        // Representative shipped values (HeldAxeRelEuler / HeldAxeWorldOffsetFromHand class).
        private static readonly Vector3 Offset = new Vector3(0.08f, -0.14f, -0.04f);

        // Rest hand pose in the MODEL-LOCAL frame (a non-trivial bone offset off the body center-line).
        private static readonly Vector3 HandRestLocal = new Vector3(0.3f, 1.2f, 0.1f);

        private void BuildRig()
        {
            // GAMEPLAY TOPOLOGY:
            //   root (CastawayCharacter — never yaws)
            //     └ model (FBX child — yaws with facing: model.localRotation = Euler(0, bodyYaw, 0))
            //         └ hand (bone — arm-swings in the model-local frame)
            //             └ axe (HeldAxeRig — rides the RAW hand)
            _root = new GameObject("CastawayRoot");

            var modelGo = new GameObject("Model");
            modelGo.transform.SetParent(_root.transform, false);
            _model = modelGo.transform;

            var handGo = new GameObject("mixamorig:RightHand");
            handGo.transform.SetParent(_model, false);
            handGo.transform.localPosition = HandRestLocal;
            handGo.transform.localRotation = Quaternion.Euler(35f, 12f, -48f); // a rotated bone frame (§FBX trap)
            handGo.transform.localScale = Vector3.one * 1.8f;                  // avatar-root scale (Hyper3D rig)
            _hand = handGo.transform;

            var axeGo = new GameObject("HeroAxe");
            axeGo.transform.SetParent(_hand, false);
            axeGo.transform.localScale = Vector3.one * 0.45f;
            _rig = axeGo.AddComponent<HeldAxeRig>();
            _rig.hand = _hand;
            _rig.worldOffsetFromHand = Offset; // hand-local units
            _rig.relEuler = new Vector3(16f, 2f, -82f);
            _rig.followDamp = 0f;              // RAW follow (the shipped value)
            _axe = axeGo.transform;
        }

        // Apply facing exactly as CastawayCharacter does: yaw the MODEL CHILD (not the root).
        private void FaceModelYaw(float yawDeg) => _model.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }

        // DYNAMIC facing sweep: rotate facing continuously through a full turn over MANY frames (never static
        // snapshots — the §Editor-vs-runtime verification rule). The axe pivot in the HAND's LOCAL frame must be
        // INVARIANT throughout (the axe stays seated in the grip) AND the axe's world rotation must track facing.
        [UnityTest]
        public IEnumerator FacingOnModel_DynamicSweep_AxeSeatInvariant_AndWorldRotationTracksFacing()
        {
            BuildRig();
            FaceModelYaw(0f);
            for (int i = 0; i < 10; i++) yield return null; // let the rig drive the seated pose
            Vector3 handLocalBaseline = _hand.InverseTransformPoint(_axe.position);
            Quaternion axeWorldAt0 = _axe.rotation;

            // Sweep facing CONTINUOUSLY over a full turn (a different yaw every frame — the dynamic case the
            // memory rule requires). Track the max hand-local seat drift + whether the world rotation moves.
            float maxSeatDrift = 0f;
            float maxWorldTurn = 0f;
            const int frames = 120;
            for (int i = 1; i <= frames; i++)
            {
                float yaw = 360f * i / frames;
                FaceModelYaw(yaw);
                yield return null; // HeldAxeRig.LateUpdate re-applies the seat from the live hand THIS frame

                Vector3 handLocalNow = _hand.InverseTransformPoint(_axe.position);
                maxSeatDrift = Mathf.Max(maxSeatDrift, Vector3.Distance(handLocalBaseline, handLocalNow));
                maxWorldTurn = Mathf.Max(maxWorldTurn, Quaternion.Angle(axeWorldAt0, _axe.rotation));
            }
            Debug.Log($"[AxeFacingTest] dynamic sweep maxSeatDrift={maxSeatDrift:F5} maxWorldTurn={maxWorldTurn:F2}");

            // (a) the seat must be INVARIANT across the whole dynamic sweep — the axe stays in the grip as the
            // character turns (riding the raw hand, the hand-local offset is fixed by construction).
            Assert.Less(maxSeatDrift, 0.01f,
                $"during a dynamic facing sweep the held axe's pivot in the HAND-LOCAL frame drifted up to " +
                $"{maxSeatDrift:F4}u — it must be INVARIANT (the axe stays seated in the grip through every " +
                "facing; 86ca9xz00 / 86ca9zcjn). A facing-eating stabilizer would drift here.");

            // (b) the axe's WORLD rotation must actually TRACK facing across the sweep (a frozen axe is the bug).
            Assert.Greater(maxWorldTurn, 45f,
                $"the held axe's world rotation moved only {maxWorldTurn:F1}° across a FULL facing turn — it must " +
                "TURN WITH facing (riding the raw hand, facing passes through; a frozen axe reads detached).");
        }

        // A frozen-rotation regression deliberately reproduced (pin the rotation to a fixed world heading) must
        // make the hand-local seat DRIFT under the same dynamic sweep — proving this test discriminates a
        // facing-following axe from a frozen one (it can't silently pass on the regression).
        [UnityTest]
        public IEnumerator FrozenWorldRotation_MakesTheSeatDrift_ProvingTheTestDiscriminates()
        {
            BuildRig();
            FaceModelYaw(0f);
            for (int i = 0; i < 10; i++) yield return null;
            _rig.enabled = false; // stop the rig; drive the OLD frozen-world-rotation behavior by hand

            // Freeze the axe at the facing-0 world pose, then sweep facing — the hand-local seat must SWING.
            FaceModelYaw(0f);
            yield return null;
            _axe.position = _hand.position + _hand.rotation * Offset;
            _axe.rotation = _hand.rotation * Quaternion.Euler(_rig.relEuler);
            Vector3 frozenWorldPos = _axe.position;
            Quaternion frozenWorldRot = _axe.rotation;
            Vector3 seatAt0 = _hand.InverseTransformPoint(_axe.position);

            FaceModelYaw(180f);
            yield return null;
            _axe.position = frozenWorldPos;   // FROZEN in world (the bug) while the hand turned away
            _axe.rotation = frozenWorldRot;
            Vector3 seatAfterTurn = _hand.InverseTransformPoint(_axe.position);

            float drift = Vector3.Distance(seatAt0, seatAfterTurn);
            Assert.Greater(drift, 0.03f,
                $"a frozen-in-world axe's hand-local seat must DRIFT after a half-turn (got {drift:F4}u) — proving " +
                "the invariance test above actually discriminates a facing-following axe from a frozen one.");
        }
    }
}
