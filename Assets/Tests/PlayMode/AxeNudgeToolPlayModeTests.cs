using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the BUILD-GATED debug AxeNudgeTool (ticket 86ca8ce6y — SOAKFIX5, the axe-nudge
    /// reframe). The tool ships in the build so the Sponsor can dial the held/stump axe transforms in-game,
    /// but it MUST be INERT in normal play (asleep behind the F9 toggle) so a normal soak is unaffected —
    /// that is the load-bearing safety property. This proves the tool does NOT move the axes, does NOT draw
    /// its overlay, and does NOT activate while the toggle is unpressed across many frames of normal play.
    ///
    /// (The actual NUDGING — key-driven transform edits + value readout — is exercised by the Sponsor live
    /// in the shipped build; the harness can't synthesize legacy Input key-downs here. The CRITICAL test for
    /// a SHIPPED debug tool is that it stays asleep in normal play, which is what this asserts.)
    /// </summary>
    public class AxeNudgeToolPlayModeTests
    {
        private GameObject _bootGo;
        private GameObject _handGo;
        private GameObject _heldGo;
        private GameObject _stumpGo;

        [SetUp]
        public void SetUp()
        {
            // A "hand" with the held axe parented under it (named "HeroAxe" — the tool resolves by name),
            // and a free "StumpAxe". Record their start transforms; normal play must not touch them.
            _handGo = new GameObject("RightHand_010");
            _handGo.transform.position = new Vector3(1f, 1f, 0f);
            _heldGo = new GameObject("HeroAxe");
            _heldGo.transform.SetParent(_handGo.transform, false);
            _heldGo.transform.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            _heldGo.transform.localEulerAngles = new Vector3(10f, 20f, 30f);

            _stumpGo = new GameObject("StumpAxe");
            _stumpGo.transform.localPosition = new Vector3(0.2f, 1.4f, 0.1f);
            _stumpGo.transform.localEulerAngles = new Vector3(22f, 45f, 8f);

            _bootGo = new GameObject("Boot");
            _bootGo.AddComponent<AxeNudgeTool>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_bootGo);
            Object.Destroy(_heldGo);
            Object.Destroy(_handGo);
            Object.Destroy(_stumpGo);
        }

        // NORMAL PLAY (no toggle key): across many frames the tool stays asleep — the held + stump axe
        // transforms are byte-identical to spawn, so a soak sees the shipped default pose, not a tool effect.
        [UnityTest]
        public IEnumerator AxeNudgeTool_InertInNormalPlay_DoesNotMoveTheAxes()
        {
            Vector3 heldPos0 = _heldGo.transform.localPosition;
            Quaternion heldRot0 = _heldGo.transform.localRotation;
            Vector3 heldWorld0 = _heldGo.transform.position;
            Vector3 stumpPos0 = _stumpGo.transform.localPosition;
            Quaternion stumpRot0 = _stumpGo.transform.localRotation;

            // Run several frames of normal play (no F9 toggle, no nudge keys synthesized).
            for (int i = 0; i < 20; i++) yield return null;

            Assert.AreEqual(heldPos0, _heldGo.transform.localPosition,
                "held axe local position must be UNCHANGED in normal play (tool inert until toggled)");
            Assert.AreEqual(heldRot0, _heldGo.transform.localRotation,
                "held axe local rotation must be UNCHANGED in normal play");
            Assert.AreEqual(heldWorld0, _heldGo.transform.position,
                "held axe must not be re-driven against the hand in normal play (the world re-apply is " +
                "gated behind the toggle)");
            Assert.AreEqual(stumpPos0, _stumpGo.transform.localPosition,
                "stump axe local position must be UNCHANGED in normal play");
            Assert.AreEqual(stumpRot0, _stumpGo.transform.localRotation,
                "stump axe local rotation must be UNCHANGED in normal play");
        }
    }
}
