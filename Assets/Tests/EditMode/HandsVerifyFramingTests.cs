using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the `-verifyHands` shipped-build capture framing (ticket 86cavaxk7 — the PR #330
    /// AC6 follow-up, Devon review 4753044764 NIT 2).
    ///
    /// THE BUG CLASS THESE CATCH: `HandsVerifyCapture` used to frame on the wrist BONE ORIGIN plus a fixed
    /// world-DOWN nudge inside a HARDCODED 0.26m box — both fitted to the v3 hand. On the v4 blocky rig the
    /// rendered hand sits 0.0545u (= 29% of the hand's own extent) off that anchor and is only 0.1884u across,
    /// so the hand framed off-centre and under-filled and the capture could not substantiate a hand/finger
    /// claim — "frames the world, not the hand". A hardcoded framing constant silently de-tunes on EVERY rig
    /// swap (v2/v3/v4 all live behind CharacterAssetGen toggles), and nothing mechanical caught it: the tool
    /// exited 0 and wrote six PNGs. These tests make the framing itself falsifiable in headless CI:
    ///   1. the rendered hand geometry is MEASURABLE from the live rig (and lies inside the trusted world AABB);
    ///   2. every corner of the measured hand is INSIDE the frustum of the frame the tool computes (centring);
    ///   3. the hand SPANS a large share of that frame (fill) — so it can't silently shrink to a speck.
    /// (2)+(3) are the real gate: they'd red on any future rig/scale/driver change that de-centres or shrinks
    /// the hand, which is exactly what a fixed constant could not.
    ///
    /// NOTE the review's stated MECHANISM ("the wrist bone world pos diverges from the rendered mesh on the
    /// 100x rig") is REFUTED and deliberately NOT encoded here: the shipped-exe trace has the bone at a sane
    /// (0.37, 1.14, 6.12) and it measures INSIDE smr.bounds. The bone is fine; the fixed offset+box were the bug.
    /// </summary>
    public class HandsVerifyFramingTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        // The capture's own framing inputs (mirrors HandsVerifyCapture: fieldOfView 35, FrameFill 0.72) plus the
        // 16:9 aspect the shipped capture runs at (1600x900 windowed).
        private const float Fov = 35f;
        private const float Fill = 0.72f;
        private const float Aspect = 16f / 9f;
        private const float ProbeNudge = 0.13f; // == the serialized handHalfExtent (probe nudge distance)

        private static SkinnedMeshRenderer OpenBootAndFindAvatar()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            GameObject player = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var ctm = root.GetComponentInChildren<ClickToMove>(true);
                if (ctm != null) { player = ctm.gameObject; break; }
            }
            Assert.IsNotNull(player, "Boot scene must contain a player with ClickToMove");
            var castaway = player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway, "the player must carry a CastawayCharacter avatar");
            var smr = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr, "the castaway avatar must have a serialized SkinnedMeshRenderer");
            smr.updateWhenOffscreen = true;
            return smr;
        }

        // (1) The rendered hand must be MEASURABLE — and land inside the renderer's trusted world AABB. This is
        // the guard on the measurement itself: a rig whose wrist bone drives no skin (an optimizeBones /
        // re-rig / bone-rename regression) fails here instead of silently shipping a wrong crop.
        [Test]
        public void RenderedHandBounds_AreMeasurable_AndInsideTheAvatarWorldAabb_86cavaxk7()
        {
            var smr = OpenBootAndFindAvatar();
            Bounds avatarWorld = smr.bounds; // world AABB — the trusted reference (CastawayVerifyCapture)
            // The conservative animation-max AABB is a superset of any single pose, so a small tolerance only
            // absorbs float noise, not a real divergence.
            avatarWorld.Expand(0.05f);

            foreach (string token in new[] { "righthand", "lefthand" })
            {
                var hand = HandsVerifyCapture.FindBoneByExactToken(smr, token);
                Assert.IsNotNull(hand, $"the rig must expose a '{token}' wrist bone in the SMR bone array");

                Assert.IsTrue(
                    HandsVerifyCapture.TryComputeRenderedHandBounds(smr, hand, ProbeNudge, out Bounds hb, out int nv),
                    $"the rendered geometry driven by '{hand.name}' must be measurable — a failure here means the " +
                    "wrist bone drives no skin (rig regression) and -verifyHands can no longer frame the hand");

                TestContext.WriteLine($"[86cavaxk7] {token}: bone={hand.position:F4} measured centre={hb.center:F4} " +
                                      $"size={hb.size:F4} verts={nv} | legacy anchor=" +
                                      $"{(hand.position + Vector3.down * (ProbeNudge * 0.4f)):F4} off by " +
                                      $"{Vector3.Distance(hand.position + Vector3.down * (ProbeNudge * 0.4f), hb.center):F4}u");

                Assert.Greater(nv, 40, $"{token}: the displacement census must isolate a real hand vertex set " +
                                       "(the live v4 rig measures 224) — a handful of verts means the threshold " +
                                       "no longer separates the hand from the forearm weight-bleed");
                // A hand-sized box on a ~1.8u character: generous band so a legitimate rig swap passes, tight
                // enough that a whole-arm / whole-avatar / degenerate selection reds.
                foreach (var axis in new[] { ("x", hb.size.x), ("y", hb.size.y), ("z", hb.size.z) })
                    Assert.That(axis.Item2, Is.InRange(0.03f, 0.50f),
                        $"{token}: measured hand extent {axis.Item1}={axis.Item2:F4} must read hand-sized " +
                        $"(full size {hb.size:F4}) — outside this band the census grabbed the arm or nothing");

                Assert.IsTrue(avatarWorld.Contains(hb.min) && avatarWorld.Contains(hb.max),
                    $"{token}: the measured hand AABB {hb} must lie inside the avatar world AABB {smr.bounds} — " +
                    "outside means the local->world matrix is wrong (the bake(false) x localToWorldMatrix " +
                    "double-apply of the FBX 100x node scale, walk-float Bug B)");
            }
        }

        // (2)+(3) THE FRAMING GATE: for every view angle the capture shoots, the frame computed from the
        // MEASURED hand must fully CONTAIN the hand (centring) and let it SPAN most of the frame (fill).
        // This is what a hardcoded box+offset could not guarantee across a rig swap.
        [Test]
        public void ComputedFrame_ContainsTheWholeRenderedHand_AndItSpansTheFrame_86cavaxk7()
        {
            var smr = OpenBootAndFindAvatar();

            // The exact viewDirs HandsVerifyCapture shoots (front-outer, fingertips-from-below, rear-orbit).
            var views = new List<(string name, string token, Vector3 dir)>
            {
                ("hands_right",      "righthand", new Vector3(0.7f, 0.35f, 1.0f)),
                ("hands_right_tips", "righthand", new Vector3(0.15f, -0.9f, 0.5f)),
                ("hands_right_rear", "righthand", new Vector3(0.4f, 0.5f, -1.0f)),
                ("hands_left",       "lefthand",  new Vector3(-0.7f, 0.35f, 1.0f)),
                ("hands_left_tips",  "lefthand",  new Vector3(-0.15f, -0.9f, 0.5f)),
                ("hands_left_rear",  "lefthand",  new Vector3(-0.4f, 0.5f, -1.0f)),
            };

            foreach (var v in views)
            {
                var hand = HandsVerifyCapture.FindBoneByExactToken(smr, v.token);
                Assert.IsNotNull(hand, $"missing wrist bone '{v.token}'");
                Assert.IsTrue(
                    HandsVerifyCapture.TryComputeRenderedHandBounds(smr, hand, ProbeNudge, out Bounds hb, out _),
                    $"{v.name}: the rendered hand must be measurable");

                // Exercise the SHIPPED framing entry point (HandsVerifyCapture.FitFrameToBox), not the raw
                // planar VerifyCaptureFraming.ComputeFrame — testing the other path would false-green: the
                // planar estimate is exactly what put the hand's lowest corner at ndc y = −1.150 on this rig.
                // The projection below is re-derived independently here, so a disagreement reds.
                var frame = HandsVerifyCapture.FitFrameToBox(hb, v.dir, Fov, Aspect, Fill);
                Matrix4x4 camWorld = Matrix4x4.TRS(frame.position, frame.rotation, Vector3.one);
                Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * camWorld.inverse; // Unity view matrix
                Matrix4x4 vp = Matrix4x4.Perspective(Fov, Aspect, 0.01f, 200f) * view;

                float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                foreach (Vector3 corner in Corners(hb))
                {
                    Vector4 clip = vp * new Vector4(corner.x, corner.y, corner.z, 1f);
                    Assert.Greater(clip.w, 0.001f,
                        $"{v.name}: hand corner {corner:F3} must be IN FRONT of the capture camera " +
                        $"(camPos={frame.position:F3}) — behind means the frame is not on the hand at all");
                    float nx = clip.x / clip.w, ny = clip.y / clip.w;
                    minX = Mathf.Min(minX, nx); maxX = Mathf.Max(maxX, nx);
                    minY = Mathf.Min(minY, ny); maxY = Mathf.Max(maxY, ny);
                }

                TestContext.WriteLine($"[86cavaxk7] {v.name}: dist={frame.distance:F3} ndc x=[{minX:F3},{maxX:F3}] " +
                                      $"y=[{minY:F3},{maxY:F3}] (frame is [-1,1] on both axes)");

                // CENTRING: every corner inside the viewport. NDC [-1,1]; 0.98 leaves a hair of edge margin so
                // a corner exactly on the boundary is treated as clipped.
                Assert.That(minX, Is.GreaterThan(-0.98f), $"{v.name}: the hand is CLIPPED at the left edge (ndc minX={minX:F3})");
                Assert.That(maxX, Is.LessThan(0.98f), $"{v.name}: the hand is CLIPPED at the right edge (ndc maxX={maxX:F3})");
                Assert.That(minY, Is.GreaterThan(-0.98f), $"{v.name}: the hand is CLIPPED at the bottom edge (ndc minY={minY:F3})");
                Assert.That(maxY, Is.LessThan(0.98f), $"{v.name}: the hand is CLIPPED at the top edge (ndc maxY={maxY:F3})");

                // FILL: the hand must DOMINATE the frame. The binding extent is framed to Fill (0.72) of the
                // frame = 1.44 of the 2.0 NDC range; require >= 1.0 (50%) so the assert survives the AABB's
                // pose-dependent anisotropy without tolerating a speck-in-a-world-shot.
                float span = Mathf.Max(maxX - minX, maxY - minY);
                Assert.That(span, Is.GreaterThan(1.0f),
                    $"{v.name}: the hand must SPAN most of the frame (ndc span {span:F3} of 2.0 = " +
                    $"{span / 2f * 100f:F0}%) — a small span is the 'frames the world, not the hand' defect");
            }
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
                yield return c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                             (i & 2) == 0 ? -e.y : e.y,
                                             (i & 4) == 0 ? -e.z : e.z);
        }
    }
}
