using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// 86ca9xz00 regression guard for the held-axe FACING-FRAME bug — the swing-stabilizer ate the facing yaw.
    ///
    /// THE BUG (Sponsor-proven, soak ec947e6): the held axe was "seated only one direction" — turning/walking
    /// did NOT move it with the hand. The prior hand-local POSITION fix (86ca9qwvd) and ROTATION fix (soakfix8)
    /// were both correct; the remaining bug was in the SWING STABILIZER FRAME.
    ///
    /// ROOT CAUSE: CastawayCharacter applies facing to its MODEL CHILD (_model.localRotation =
    /// Euler(0, bodyYaw, 0)), NOT to its root ("the visual owns facing"). HeldAxeRig.stabilizeFrame was wired
    /// to the CastawayCharacter ROOT, which NEVER yaws. So when the character turned, the hand's facing yaw
    /// landed in the ROOT-LOCAL grip-anchor pose, where the slow anchor (anchorTrackPerSec ≈ 0.12/s ≈ 8s time
    /// constant) EASED IT AWAY over ~8s → the axe rotation LAGGED facing → "only seated one direction."
    ///
    /// FIX (86ca9xz00): wire stabilizeFrame to the FACING-CARRYING model child (CastawayCharacter.ModelTransform).
    /// The anchor frame then yaws WITH facing, so there is no facing component left to ease away — facing passes
    /// through IMMEDIATELY; only the per-step arm-swing (hand relative to _model) is still damped.
    ///
    /// WHY THE OLD AC3 WAS A FALSE-GREEN (HeldAxeStaysSeatedAcrossFacingsPlayModeTests): it rotated the test's
    /// ROOT and ran with swingStabilize=0 (which bypasses the stabilizeFrame anchor entirely — the raw-hand
    /// path). It never exercised the model-child facing path NOR the anchor where the bug lives, so it passed
    /// while live play failed. THIS test reproduces the REAL gameplay topology — root (never yaws) → model
    /// child (yaws with facing, the _model path) → hand (arm-swings in the model frame) — drives facing on the
    /// model child over MULTIPLE frames with swingStabilize=1 (the SHIPPED value, where the anchor + the bug
    /// live), and asserts the axe's hand-local seat is invariant across facings. It is RED with
    /// stabilizeFrame=root (the bug — the anchor eases the facing yaw away) and GREEN with stabilizeFrame=model.
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

        private void BuildRig(bool stabilizeOnModel)
        {
            // GAMEPLAY TOPOLOGY (the real hierarchy, not the old false-green's flat root rotation):
            //   root (CastawayCharacter — never yaws)
            //     └ model (FBX child — yaws with facing: model.localRotation = Euler(0, bodyYaw, 0))
            //         └ hand (bone — arm-swings in the model-local frame)
            //             └ axe (HeldAxeRig)
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
            _rig.swingStabilize = 1f;          // the SHIPPED value — exercises the anchor where the facing bug lives
            _rig.anchorTrackPerSec = 0.12f;    // the SHIPPED slow anchor (≈8s time constant — what eased facing away)
            // THE FRAME UNDER TEST: the model child (the fix) vs the root (the bug).
            _rig.stabilizeFrame = stabilizeOnModel ? _model : _root.transform;
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

        // The axe pivot expressed in the HAND's LOCAL frame must be invariant across facings driven on the MODEL
        // child, even after MANY frames at each facing (so the slow anchor would have eased a root-frame facing
        // yaw away). With stabilizeFrame=model (the fix) the facing passes through and the seat holds.
        [UnityTest]
        public IEnumerator FacingOnModel_AxeSeatInHandLocalFrame_IsInvariant_WhenStabilizedOnModel()
        {
            BuildRig(stabilizeOnModel: true);
            FaceModelYaw(0f);
            // Seat the anchor at facing 0 (many frames — the slow anchor must fully settle).
            for (int i = 0; i < 40; i++) yield return null;
            Vector3 handLocalBaseline = _hand.InverseTransformPoint(_axe.position);

            float[] facings = { 0f, 45f, 90f, 137f, 180f, 233f, 270f, 315f };
            foreach (float yaw in facings)
            {
                FaceModelYaw(yaw);
                // Tick SEVERAL frames at the new facing so a stabilizer LAG would manifest (the bug eases the
                // facing yaw away over ~8s — multiple frames give it time to drift if the frame is wrong).
                for (int i = 0; i < 20; i++) yield return null;

                Vector3 handLocalNow = _hand.InverseTransformPoint(_axe.position);
                float drift = Vector3.Distance(handLocalBaseline, handLocalNow);
                Assert.Less(drift, 0.01f,
                    $"at facing {yaw}° (driven on the MODEL child) the held axe's pivot in the HAND-LOCAL frame " +
                    $"drifted {drift:F4}u from baseline over 20 frames — it must be INVARIANT. With " +
                    "stabilizeFrame=model the facing passes through the grip anchor immediately; a root frame " +
                    "(the 86ca9xz00 bug) would let the slow anchor ease the facing yaw away and the axe lags.");
            }
        }

        // THE DELIBERATE-BREAK / RED-PROOF: with stabilizeFrame=ROOT (the bug) and facing driven on the MODEL
        // child, the hand-local seat MUST drift as the character turns — proving this test discriminates the
        // buggy frame from the fixed one (so it can't silently pass on the regression). This is the assertion
        // that goes RED against the pre-fix wiring and GREEN against the fix.
        [UnityTest]
        public IEnumerator FacingOnModel_AxeSeat_DRIFTS_WhenStabilizedOnRoot_ProvingTheBugAndTheDiscriminator()
        {
            BuildRig(stabilizeOnModel: false); // the BUG wiring (root frame)
            FaceModelYaw(0f);
            for (int i = 0; i < 40; i++) yield return null; // settle the anchor at facing 0
            Vector3 handLocalBaseline = _hand.InverseTransformPoint(_axe.position);

            // A large facing change + many frames so the slow root-frame anchor has visibly eased the facing
            // yaw away (the lag the Sponsor saw). The hand-local seat must DRIFT well past the fix's tolerance.
            FaceModelYaw(180f);
            for (int i = 0; i < 60; i++) yield return null;
            Vector3 handLocalAfterTurn = _hand.InverseTransformPoint(_axe.position);

            float drift = Vector3.Distance(handLocalBaseline, handLocalAfterTurn);
            Assert.Greater(drift, 0.03f,
                $"with stabilizeFrame=ROOT (the 86ca9xz00 bug) the held axe's hand-local seat must DRIFT after a " +
                $"half-turn driven on the model child (got {drift:F4}u) — proving the anchor eats the facing yaw " +
                "(the axe lags facing) AND that the invariance test above actually discriminates the buggy frame " +
                "from the fixed one (else it could pass even with the bug present).");
        }

        // The soakfix8 contract must SURVIVE the facing-frame fix: the axe still TURNS with facing. Drive the
        // model child through distinct yaws and assert the axe's WORLD rotation tracks facing (a frozen-in-world
        // axe is also wrong). This guards that the fix didn't accidentally freeze the axe.
        [UnityTest]
        public IEnumerator FacingOnModel_AxeStillTurnsWithFacing_WhenStabilizedOnModel()
        {
            BuildRig(stabilizeOnModel: true);
            FaceModelYaw(0f);
            for (int i = 0; i < 40; i++) yield return null;
            Quaternion axeAt0 = _axe.rotation;

            FaceModelYaw(90f);
            for (int i = 0; i < 40; i++) yield return null; // let the anchor frame re-orient with facing
            Quaternion axeAt90 = _axe.rotation;

            float turn = Quaternion.Angle(axeAt0, axeAt90);
            Assert.Greater(turn, 45f,
                $"the held axe's world rotation barely moved ({turn:F1}°) when facing yawed 90° on the model " +
                "child — the axe must still TURN WITH facing (the soakfix8 rotation-tracks-hand contract must " +
                "survive the facing-frame fix; the anchor frame yaws with facing, so facing passes through).");
        }
    }
}
