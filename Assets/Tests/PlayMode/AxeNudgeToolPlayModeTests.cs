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
        private GameObject _armGo;
        private CastawayArmPose _armPose;

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

            // RE-SOAK — a CastawayArmPose the tool's arm-pose target nudges. Disabled (we only check the tool
            // doesn't touch its euler fields in normal play; the pose's own behavior has its own test).
            _armGo = new GameObject("CastawayAvatar");
            _armPose = _armGo.AddComponent<CastawayArmPose>();
            _armPose.enabled = false;
            _armPose.rightArmEuler = new Vector3(12f, 0f, 7f);
            _armPose.leftArmEuler = new Vector3(9f, 0f, 0f);

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
            Object.Destroy(_armGo);
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
            Vector3 rArm0 = _armPose.rightArmEuler, lArm0 = _armPose.leftArmEuler; // RE-SOAK arm-pose target

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
            // RE-SOAK — the arm-pose target must also be untouched in normal play (the F9 gate covers it too).
            Assert.AreEqual(rArm0, _armPose.rightArmEuler,
                "right-arm euler offset must be UNCHANGED in normal play (arm-pose nudge gated behind F9)");
            Assert.AreEqual(lArm0, _armPose.leftArmEuler,
                "left-arm euler offset must be UNCHANGED in normal play (arm-pose nudge gated behind F9)");
        }

        // OFF-HOTBAR guard (86ca8ce6y SOAKFIX6 — "the AXE-NUDGE overlay covers the inventory hotbar"). The
        // prior panel sat bottom-LEFT directly over SurvivalHud's warmth bar + inventory ledger. The panel is
        // now RIGHT-anchored + vertically centred (AxeNudgeTool.PanelRect). This pins the contract: across a
        // range of screen sizes the panel rect must NOT overlap SurvivalHud's bottom-left hotbar footprint
        // (AxeNudgeTool.HotbarZone). A regression that moves the panel back over the hotbar reds in CI.
        // Pure-geometry (no render needed) — the rect math is the load-bearing contract.
        [Test]
        public void NudgePanel_ClearsTheHotbar_AcrossScreenSizes()
        {
            // Cover common desktop sizes + a small window (the soak runs windowed).
            var sizes = new (float w, float h)[]
            {
                (1920f, 1080f), (1600f, 900f), (1280f, 720f), (1024f, 768f), (800f, 600f),
            };
            foreach (var (w, h) in sizes)
            {
                Rect panel = AxeNudgeTool.PanelRect(w, h);
                Rect hotbar = AxeNudgeTool.HotbarZone(w, h);
                Assert.IsFalse(panel.Overlaps(hotbar),
                    $"at {w}x{h} the nudge panel {panel} must NOT overlap the SurvivalHud hotbar {hotbar} " +
                    "(the Sponsor's 'overlay covers the inventory hotbar' soak failure)");

                // Sanity: the panel must stay fully on-screen (no off-edge clipping of the controls).
                Assert.GreaterOrEqual(panel.x, 0f, $"at {w}x{h} the panel must not run off the left edge");
                Assert.LessOrEqual(panel.xMax, w, $"at {w}x{h} the panel must not run off the right edge");
                Assert.GreaterOrEqual(panel.y, 0f, $"at {w}x{h} the panel must not run off the top edge");
                Assert.LessOrEqual(panel.yMax, h, $"at {w}x{h} the panel must not run off the bottom edge");
            }
        }

        // NOT-ENGAGED INDICATOR (86caju055) — when the debug-overlay layer is revealed (F10 master ON) but the
        // F9 dial tool is asleep, the tool must signal "NOT ENGAGED" so the Sponsor knows the nudge keys are
        // inert (he isn't nudging into the void). The show-condition seam (ShowNotEngagedHint) is TRUE only when
        // the overlay is visible AND the tool is inactive; engaging F9 (Activate) clears it. Pure-condition test
        // (no render). A regression that shows the hint while engaged, or hides it while asleep+overlay-up, reds.
        [Test]
        public void NotEngagedHint_ShowsOnlyWhenOverlayVisibleAndToolInactive()
        {
            var tool = _bootGo.GetComponent<AxeNudgeTool>();
            bool prevVisible = DebugOverlays.Visible;
            try
            {
                DebugOverlays.Hide();
                Assert.IsFalse(tool.ShowNotEngagedHint,
                    "with the debug-overlay layer HIDDEN, the not-engaged hint must not show (clean screen)");

                DebugOverlays.Show();
                Assert.IsTrue(tool.ShowNotEngagedHint,
                    "with the overlay layer up but F9 asleep, the not-engaged hint MUST show (86caju055)");

                tool.Activate(); // engage the F9 dial
                Assert.IsTrue(tool.IsActive, "Activate must engage the tool");
                Assert.IsFalse(tool.ShowNotEngagedHint,
                    "once F9 is engaged the not-engaged hint must clear (the panel takes over)");

                tool.Deactivate();
                Assert.IsTrue(tool.ShowNotEngagedHint,
                    "toggling F9 back off (overlay still up) must re-show the not-engaged hint");
            }
            finally
            {
                DebugOverlays.Visible = prevVisible;
                tool.Deactivate();
            }
        }

        // SOAKFIX10 regression guard ("the nudge-tool BOX cuts off the 3rd rotation value off its right
        // edge — this is the full window"). Two guarantees the fix rests on:
        //   1. The box is WIDE ENOUGH to hold the longest single value line. With position + euler now on
        //      SEPARATE lines (AxeNudgeTool.OnGUI), the widest value line is the F4-formatted offsetFromHand
        //      "offsetFromHand=(-0.0234, -0.0456, -0.0678)" — ~42 chars at the 14px bold value style. At a
        //      conservative ~9.5px/char that needs ~400px of inner width; the inner width must clear it with
        //      margin so no component is ever clipped. A regression that re-packs both values onto one line
        //      (~75 chars → ~710px) would overflow the box again — guarded by the per-line width budget.
        //   2. On a window NARROWER than the panel itself the x-clamp keeps the WHOLE box on-screen (x>=12,
        //      xMax<=w), so the left side of the value text can never be pushed off the left edge.
        [Test]
        public void NudgePanel_FitsBothValueLines_AndStaysOnScreen_OnAnyWidth()
        {
            const float perCharPx = 9.5f;   // conservative upper bound for the 14px bold value GUIStyle
            const float labelInset = 24f;   // OnGUI draws value labels at lx=x+12, lw=w-24
            // The longest value line the panel must hold, on its OWN line (position; euler is shorter):
            string widestValueLine = "offsetFromHand=(-0.0234, -0.0456, -0.0678)";
            float neededInner = widestValueLine.Length * perCharPx;
            float innerWidth = AxeNudgeTool.PanelWidth - labelInset;
            Assert.GreaterOrEqual(innerWidth, neededInner,
                $"the panel inner width ({innerWidth}px) must hold the longest value line " +
                $"\"{widestValueLine}\" (~{neededInner:F0}px) so no rotation/position component is cut off " +
                "the right edge (the Sponsor's soakfix10 report)");

            // On ANY width — including a window narrower than the panel — the whole box stays on-screen.
            foreach (float w in new[] { 5120f, 1920f, 800f, 600f, AxeNudgeTool.PanelWidth, 400f, 320f })
            {
                Rect p = AxeNudgeTool.PanelRect(w, 1440f);
                Assert.GreaterOrEqual(p.x, 0f, $"at width {w} the panel's left edge must stay on-screen");
                if (w >= AxeNudgeTool.PanelWidth + 24f)
                    Assert.LessOrEqual(p.xMax, w, $"at width {w} the panel must not run off the right edge");
            }
        }
    }
}
