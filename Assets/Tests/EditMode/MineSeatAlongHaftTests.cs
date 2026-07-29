using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cay4282 round 3 — THE ALONG-HAFT AXIS AND ITS READOUT.
    ///
    /// THE DEFECT THIS FILE EXISTS FOR (Sponsor, soaking round 2 at 748e585, verbatim): "how can i dial that the left
    /// hand is not on the bottom of the axe". The panel read <c>L-&gt;haft 0,470 / R-&gt;haft 0,057 SW PASS ✓</c> while
    /// the left hand was clamped at the BUTT end of the haft. Two independent causes, both pinned here:
    ///
    ///   1. <see cref="TwoHandGripRead.Pass"/> scores each hand's PERPENDICULAR distance to the haft LINE, so the
    ///      along-haft position is unscored — a butt-end grip and a mid-haft grip are the SAME number to it.
    ///      <c>Read.leftU</c>/<c>rightU</c> already carried the answer and were rendered NOWHERE.
    ///   2. There was no single dial for "slide the grip along the stick": that one physical intent was hand-local X/Z
    ///      (arrows) plus Y (PgUp/PgDn) composed through a ~(-25, 70, 24) seat rotation.
    ///
    /// THE BUG CLASS, NOT THE INSTANCE. This is the SECOND round of the same shape — round 1's missing number was hand
    /// SEPARATION, round 2's was the along-haft position, both computed and both undrawn. So the first test below is
    /// deliberately written against the CLASS: every field of the read a human would judge on must be rendered by a
    /// formatter, or this file reds. A third instance should be a test failure, not a soak failure.
    ///
    /// CULTURE NOTE (would otherwise be a CI-vs-local split): the panel formats with the CURRENT culture, and this
    /// machine is da-DK, so the live panel prints "0,470" while CI may print "0.470". Every expectation below is built
    /// by calling the SAME ToString(format) rather than by hard-coding a decimal separator.
    /// </summary>
    public class MineSeatAlongHaftTests
    {
        // ==============================================================================================================
        // 1 — THE CLASS: every judgeable field of the read must reach the screen.
        // ==============================================================================================================

        [Test]
        public void EveryJudgeableFieldOfTheGripRead_IsRenderedBySomePanelRow()
        {
            // Distinctive values, so a row that prints the WRONG field cannot accidentally satisfy another's assert.
            var r = new TwoHandGripRead.Read
            {
                valid = true,
                leftHaftSW = 0.123f,
                rightHaftSW = 0.234f,
                leftU = 0.345f,
                rightU = 0.456f,
                toolVsHandLineDeg = 32.7f,
                handSepSW = 1.234f,
                shoulderWidth = 0.458f,
            };

            string rows = AxeNudgeTool.GripDistanceLine(r, 1f) + " | " +
                          AxeNudgeTool.AlongHaftLine(r) + " | " +
                          AxeNudgeTool.GripContextLine(r);

            // field -> the text the panel must contain for it, formatted the way the panel formats it.
            var expected = new Dictionary<string, string>
            {
                { "leftHaftSW (is the left hand ON the haft line?)", r.leftHaftSW.ToString("F3") },
                { "rightHaftSW (is the right hand still on its own haft?)", r.rightHaftSW.ToString("F3") },
                { "leftU (WHERE ALONG the haft the left hand sits — the round-3 defect)", r.leftU.ToString("F2") },
                { "rightU (where along the haft the right hand sits)", r.rightU.ToString("F2") },
                { "toolVsHandLineDeg (does the tool agree with the grip the eye reads?)", r.toolVsHandLineDeg.ToString("F1") },
                { "handSepSW (round 1's metric; it EXPLAINS the residual)", r.handSepSW.ToString("F2") },
                { "shoulderWidth (the normaliser every SW figure above is in)", r.shoulderWidth.ToString("F3") },
            };

            foreach (var kv in expected)
                Assert.IsTrue(rows.Contains(kv.Value),
                    $"the F9 MINE panel must RENDER {kv.Key}. Expected the text \"{kv.Value}\" somewhere in:\n{rows}\n" +
                    "This ticket has already shipped this exact defect twice — a quantity the code computes, that the " +
                    "whole judgement rests on, and that the panel never draws. If a new field is added to " +
                    "TwoHandGripRead.Read and a human would judge on it, draw it and extend this list.");
        }

        [Test]
        public void AnUnmeasurableRead_RendersAsUnavailable_OnEveryRow_NeverAsAPlausibleNumber()
        {
            var bad = default(TwoHandGripRead.Read);   // valid == false
            Assert.AreEqual(AxeNudgeTool.GripUnavailableLine, AxeNudgeTool.GripDistanceLine(bad, 1f));
            Assert.AreEqual(AxeNudgeTool.GripUnavailableLine, AxeNudgeTool.AlongHaftLine(bad),
                "an unmeasurable rig must NOT render as u=0.00 — a zero there reads as 'the hand is at the butt', " +
                "which is a specific (and wrong) claim rather than 'we do not know'.");
            Assert.AreEqual(AxeNudgeTool.GripUnavailableLine, AxeNudgeTool.GripContextLine(bad));
        }

        // ==============================================================================================================
        // 2 — OFF-THE-END MUST BE UNMISTAKABLE (the brief: "not a quiet negative number").
        // ==============================================================================================================

        [Test]
        public void TheAlongHaftRow_CarriesItsOwnLegend_SoTheNumberNeedsNoSourceLookup()
        {
            var r = MakeRead(0.30f, 0.80f);
            string line = AxeNudgeTool.AlongHaftLine(r);
            StringAssert.Contains("BUTT", line);
            StringAssert.Contains("HEAD", line);
            Assert.IsTrue(line.Contains("0") && line.Contains("1"),
                "the row must state the 0/1 convention inline — the Sponsor reads this in a build, with no source.");
        }

        [Test]
        public void AHandOffTheButtEnd_IsFlaggedLoudly_NotLeftAsANegativeNumber()
        {
            string line = AxeNudgeTool.AlongHaftLine(MakeRead(-0.08f, 0.62f));
            StringAssert.Contains("!!OFF-BUTT", line,
                "a left hand that has slid PAST the butt end must be flagged in words. A bare '-0.08' is exactly the " +
                "'quiet negative' the brief calls out: it looks like a small number, not like a hand off the tool.");
            Assert.IsFalse(line.Contains("!!OFF-HEAD"), "…and must not also claim the head end");
        }

        [Test]
        public void AHandOffTheHeadEnd_IsFlaggedLoudly_Too()
        {
            string line = AxeNudgeTool.AlongHaftLine(MakeRead(0.42f, 1.12f));
            StringAssert.Contains("!!OFF-HEAD", line,
                "a right hand past the HEAD end is the failure mode a naive 'choke up harder' fit produces — the palm " +
                "ends up beyond the pick. It must be legible as such, not as 'u = 1.12'.");
        }

        [Test]
        public void TheLeftHandsRow_StatesHowMuchHaftIsBelowIt_TheSponsorsOwnQuantity()
        {
            // "the left hand is not on the bottom of the axe" is a question about the haft BELOW the hand. The row
            // states that directly so he never converts a fraction in his head mid-soak.
            string line = AxeNudgeTool.AlongHaftLine(MakeRead(0.30f, 0.80f));
            StringAssert.Contains("below it", line);
            StringAssert.Contains("30", line, "0.30 of the haft below the left hand must read as 30%");
        }

        // ==============================================================================================================
        // 3 — u IS REPORTED, NOT GATED (the Sponsor's explicit call this round).
        // ==============================================================================================================

        [Test]
        public void Pass_IsCompletelyIndifferentToTheAlongHaftPosition_ThisRound()
        {
            // Identical perpendicular distances, wildly different grip positions — including a hand right off the butt.
            // Both must PASS, because the along-haft window is the Sponsor's to choose at the soak. A round-4 change
            // that folds u into Pass() will red THIS test, which is the intended signal to re-read the decision rather
            // than a regression: the gate tightening must be deliberate.
            var atButt = MakeRead(0.02f, 0.72f, leftHaft: 0.40f, rightHaft: 0.01f);
            var midHaft = MakeRead(0.45f, 0.95f, leftHaft: 0.40f, rightHaft: 0.01f);
            var offEnd = MakeRead(-0.30f, 0.40f, leftHaft: 0.40f, rightHaft: 0.01f);

            Assert.IsTrue(TwoHandGripRead.Pass(atButt));
            Assert.IsTrue(TwoHandGripRead.Pass(midHaft));
            Assert.IsTrue(TwoHandGripRead.Pass(offEnd),
                "u is REPORT-ONLY this round (Sponsor's call: the right window depends on the grip he picks, and a " +
                "threshold invented for him would gate the build against a guess). That is exactly why the panel and " +
                "the shipped gate must SHOW it — a pass verdict cannot be the thing that catches this defect.");
        }

        // ==============================================================================================================
        // 4 — THE SLIDE AXIS: one key pair, the right direction, on the haft's OWN axis.
        // ==============================================================================================================

        [Test]
        public void SlidingTheSeatUpTheHaft_MovesTheHandsTowardTheHead_ByExactlyTheRequestedDistance()
        {
            var rig = BuildSyntheticRig(out GameObject root, out Transform hand, haftLen: 2f);
            try
            {
                // Deliberately AWKWARD seat + hand rotations: an implementation that quietly assumes an identity frame,
                // or slides in the HAND's axes instead of the HAFT's, passes at identity and fails here.
                rig.seatOffsetFromHand = Vector3.zero;
                rig.seatEuler = new Vector3(11f, -37f, 63f);
                rig.mineSeatEulerDelta = new Vector3(-24.7f, 70f, 23.7f);
                rig.mineSeatOffsetDelta = Vector3.zero;
                hand.SetPositionAndRotation(new Vector3(3f, 1.4f, -2f), Quaternion.Euler(17f, 130f, -44f));

                SeatAtFullMineWeight(rig, hand);
                Assert.IsTrue(rig.TryGetHaftSegment(out Vector3 g0, out Vector3 h0));
                float haftLen = (h0 - g0).magnitude;
                Assert.AreEqual(2f, haftLen, 1e-3f, "the synthetic haft must measure its authored length");

                // A FIXED WORLD POINT standing in for a hand: the hands are posed by the clip and do NOT move when the
                // seat slides, so measuring against a fixed point is the honest model of what the Sponsor sees.
                Vector3 handPoint = g0 + (h0 - g0) * 0.30f;
                TwoHandGripRead.DistanceToSegment(handPoint, g0, h0, out float uBefore);
                Assert.AreEqual(0.30f, uBefore, 1e-4f);

                const float slide = 0.20f;                     // metres, i.e. 0.10 of this 2 m haft
                Assert.IsTrue(rig.TrySlideMineSeatAlongHaft(slide));
                SeatAtFullMineWeight(rig, hand);
                Assert.IsTrue(rig.TryGetHaftSegment(out Vector3 g1, out Vector3 h1));
                TwoHandGripRead.DistanceToSegment(handPoint, g1, h1, out float uAfter);

                Assert.AreEqual(uBefore + slide / haftLen, uAfter, 1e-3f,
                    $"a POSITIVE slide must move the hands UP the haft toward the HEAD by exactly the requested " +
                    $"distance (u {uBefore:F3} -> {uAfter:F3}, expected {uBefore + slide / haftLen:F3}). The sign is " +
                    "the whole trap: the hands do not move, the TOOL does, so the tool must translate BUTT-FIRST to " +
                    "make the hands read as higher up. An inverted sign gives a dial that does the opposite of its " +
                    "own on-screen hint.");

                // …and the haft must not have TURNED or CHANGED LENGTH — a slide is a pure translation along one axis.
                Assert.AreEqual(haftLen, (h1 - g1).magnitude, 1e-3f, "the slide must not scale the haft");
                Assert.Less(Vector3.Angle(h0 - g0, h1 - g1), 1e-2f,
                    "the slide must not ROTATE the haft — it is a translation along the haft's own axis, so the " +
                    "carefully-fitted orientation the two-hand read depends on must survive it untouched.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SlidingDown_IsTheExactInverseOfSlidingUp_SoTheDialIsNonDestructive()
        {
            var rig = BuildSyntheticRig(out GameObject root, out Transform hand, haftLen: 2f);
            try
            {
                rig.seatEuler = new Vector3(11f, -37f, 63f);
                rig.mineSeatEulerDelta = new Vector3(-24.7f, 70f, 23.7f);
                rig.mineSeatOffsetDelta = new Vector3(-0.2491f, -0.3928f, -0.3109f);
                hand.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(17f, 130f, -44f));
                SeatAtFullMineWeight(rig, hand);

                Vector3 before = rig.mineSeatOffsetDelta;
                Assert.IsTrue(rig.TrySlideMineSeatAlongHaft(0.13f));
                SeatAtFullMineWeight(rig, hand);
                Assert.IsTrue(rig.TrySlideMineSeatAlongHaft(-0.13f));

                Assert.Less((rig.mineSeatOffsetDelta - before).magnitude, 1e-4f,
                    "up-then-down must return the Sponsor to exactly where he started. A dial that drifts on a " +
                    "round-trip cannot be explored — he would be unable to get back to a grip he liked.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void WithNoResolvableMesh_TheSlideIsREFUSED_RatherThanGuessingAnAxis()
        {
            var root = new GameObject("NoMeshTool");
            try
            {
                var hand = new GameObject("Hand").transform;
                root.transform.SetParent(hand, false);
                var rig = root.AddComponent<HeldToolRig>();
                rig.hand = hand;
                Vector3 before = rig.mineSeatOffsetDelta;

                Assert.IsFalse(rig.TrySlideMineSeatAlongHaft(0.2f),
                    "with no displayed mesh the haft axis is unknown; the slide must refuse.");
                Assert.AreEqual(before, rig.mineSeatOffsetDelta,
                    "a refused slide must change NOTHING. Falling back to a guessed axis is the documented " +
                    "bakeAxisConversion trap (a weapon authored along Blender +Z arrives on Unity +Y) — it would move " +
                    "the tool somewhere plausible-looking and wrong.");
                Object.DestroyImmediate(hand.gameObject);
            }
            finally { if (root != null) Object.DestroyImmediate(root); }
        }

        [Test]
        public void TheHaftAxis_IsEvaluatedAtFullMineWeight_SoTheDialBehavesTheSameAtRestAndMidSwing()
        {
            // The live haft direction depends on the CURRENT eased weight (the rotation delta is scaled by it). If the
            // slide resolved its axis from the live pose, the same keypress would move the grip along a DIFFERENT axis
            // depending on where in the ~0.25 s ease it was pressed — an engagement-weighted dial that behaves
            // differently for no visible reason, which is the trap that burned the Sponsor twice on run-lower.
            Vector3 seatEuler = new Vector3(11f, -37f, 63f);
            Vector3 mineDelta = new Vector3(-24.7f, 70f, 23.7f);

            // The SAME tool orientation + haft segment, but asked for the axis with the seat euler alone vs with the
            // mine delta composed: the two must DIFFER, which is what makes evaluating at full weight a real choice
            // rather than a no-op claim.
            Quaternion toolRot = Quaternion.Euler(5f, 200f, -33f);
            Vector3 seg = toolRot * new Vector3(0f, 2f, 0f);
            Assert.IsTrue(HeldToolRig.TryMineHaftAxisHandLocal(toolRot, seg, seatEuler, mineDelta, out Vector3 atFull));
            Assert.IsTrue(HeldToolRig.TryMineHaftAxisHandLocal(toolRot, seg, seatEuler, Vector3.zero, out Vector3 atZero));
            Assert.Greater(Vector3.Angle(atFull, atZero), 10f,
                "the mine rotation delta materially changes the haft's direction, so WHICH weight the axis is " +
                "evaluated at is load-bearing, not a detail.");

            Assert.AreEqual(1f, atFull.magnitude, 1e-4f, "the axis must be a unit vector so a slide in metres is metres");

            // …and it must not depend on the tool's current orientation at all (the holder is rigidly parented, so the
            // haft direction in the TOOL's frame is frame-invariant). This is what makes the dial facing-independent,
            // like every other dial on this seat.
            Quaternion other = Quaternion.Euler(-70f, 12f, 145f);
            Assert.IsTrue(HeldToolRig.TryMineHaftAxisHandLocal(other, other * new Vector3(0f, 2f, 0f),
                                                              seatEuler, mineDelta, out Vector3 axisElsewhere));
            Assert.Less(Vector3.Angle(atFull, axisElsewhere), 1e-2f,
                "the resolved hand-local axis must be identical whichever way the tool happens to be facing this " +
                "frame — otherwise a nudge pressed mid-turn would slide the grip along a different axis.");
        }

        [Test]
        public void AZeroLengthHaft_IsRefused_NotDividedBy()
        {
            Assert.IsFalse(HeldToolRig.TryMineHaftAxisHandLocal(Quaternion.identity, Vector3.zero,
                                                               Vector3.zero, Vector3.zero, out Vector3 axis));
            Assert.AreEqual(Vector3.zero, axis, "a refused axis must be zero, never a NaN that silently poisons the seat");
        }

        // ==============================================================================================================
        // 5 — THE KEYS: layout-agnostic, and not already taken (the Danish-keyboard rule).
        // ==============================================================================================================

        [Test]
        public void TheAlongHaftKeys_AreLayoutAgnostic_AndCollideWithNothingTheToolAlreadyBinds()
        {
            var go = new GameObject("AlongHaftKeyProbe");
            var weaponGo = new GameObject("WeaponCycleProbe");
            var camGo = new GameObject("OrbitCamProbe");
            try
            {
                var tool = go.AddComponent<AxeNudgeTool>();
                var cycle = weaponGo.AddComponent<HeldWeaponCycleDebug>();
                var cam = camGo.AddComponent<OrbitCamera>();

                foreach (var key in new[] { tool.haftUpKey, tool.haftDownKey })
                {
                    // Legacy Input reads keys by US PHYSICAL POSITION and the Sponsor is on a Danish layout, where
                    // ; ' [ ] = - land on different physical keys (or nowhere). Letters, arrows, PgUp/PgDn, F-keys,
                    // Space and Tab are the safe set (unity-conventions.md §Input System).
                    Assert.IsTrue(key >= KeyCode.A && key <= KeyCode.Z,
                        $"{key} must be a LETTER key: the Sponsor's Danish layout shifts every punctuation key, and a " +
                        "soak-facing control he physically cannot press is a dead dial (the ';'/'\\'' axe-head dial).");
                }

                Assert.AreNotEqual(tool.haftUpKey, tool.haftDownKey);
                var taken = new List<KeyCode>
                {
                    tool.toggleKey, tool.cycleKey, tool.armSwitchKey,
                    KeyCode.T, KeyCode.G, KeyCode.Y, KeyCode.H, KeyCode.U, KeyCode.J,   // the rotation dials
                    KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,                          // locomotion
                    KeyCode.E, KeyCode.Q, KeyCode.C,                                     // loot / drink / build menu
                    cycle.cycleKey, cycle.scaleUpKeyDanish, cycle.scaleDownKeyDanish,     // [B] / [O] / [I]
                    cam.frontSnapKey,                                                     // [F] front-view snap
                };
                foreach (var key in new[] { tool.haftUpKey, tool.haftDownKey })
                    Assert.IsFalse(taken.Contains(key),
                        $"{key} is already bound elsewhere — a cross-firing key is the [B] arm-switch/weapon-cycle " +
                        "collision and the Tab inventory collision this tool has already been bitten by twice.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(weaponGo);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void TheShippedBootScene_CarriesTheAlongHaftKeys_AsRAndV_NotKeyCodeNone()
        {
            // WHY A SCENE TEST FOR TWO KeyCode FIELDS. Boot.unity is BINARY, so the serialized value of a new field
            // cannot be grepped — and the AxeNudgeTool in it is SERIALIZED, not Awake-built. A new public field on a
            // scene-baked component is exactly where a value silently arrives as the enum's zero (KeyCode.None = 0)
            // instead of its C# initializer, which would hand the Sponsor two keys that do nothing. This tool has
            // already shipped a dead key to him twice (the ';'/'\'' axe-head dial on his Danish layout, and PgUp/PgDn
            // silently no-oping behind an unstated precondition), so the shipped value gets read, not reasoned about.
            EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity", OpenSceneMode.Single);
            AxeNudgeTool tool = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                tool = root.GetComponentInChildren<AxeNudgeTool>(true);
                if (tool != null) break;
            }
            Assert.IsNotNull(tool, "the Boot scene must carry the AxeNudgeTool — it is the instrument this round ships");
            Assert.AreEqual(KeyCode.R, tool.haftUpKey,
                "the SHIPPED along-haft UP key must be [R]. KeyCode.None here means the field deserialised to the " +
                "enum's zero and the soak brief would quote a key that moves nothing.");
            Assert.AreEqual(KeyCode.V, tool.haftDownKey, "…and the DOWN key must be [V].");
        }

        // ==============================================================================================================
        // 6 — THE ROWS MUST FIT THE BOX. An IMGUI label wider than its Rect is CLIPPED, and a clipped measurement is
        //     another way to compute a number and not show it — which is what was happening to the round-2 one-line
        //     verdict at the old 532px width.
        // ==============================================================================================================

        [Test]
        public void EveryMeasurementRow_FitsThePanelsInnerWidth_EvenInItsLongestForm()
        {
            // Conservative upper bound for the 12px bold measurement style. The 14px style's budget is ~9.5px/char
            // (AxeNudgeToolPlayModeTests), so 12px scales to ~8.2.
            const float perCharPx = 8.2f;
            float innerWidth = AxeNudgeTool.PanelWidth - AxeNudgeTool.LabelInset;

            // The LONGEST form of each row: widest plausible magnitudes, and for the along-haft row BOTH hands flagged
            // off an end at once (the case a naive over-choked fit produces).
            var worst = new Dictionary<string, string>
            {
                { "distance row", AxeNudgeTool.GripDistanceLine(MakeRead(0.5f, 0.5f, 1.234f, 0.987f), 1f) },
                { "along-haft row (both flagged)", AxeNudgeTool.AlongHaftLine(MakeRead(-0.88f, 1.99f)) },
                { "along-haft row (in range)", AxeNudgeTool.AlongHaftLine(MakeRead(0.456f, 0.789f)) },
                { "context row", AxeNudgeTool.GripContextLine(MakeRead(0.3f, 0.8f)) },
                { "unavailable notice", AxeNudgeTool.GripUnavailableLine },
            };

            foreach (var kv in worst)
                Assert.LessOrEqual(kv.Value.Length * perCharPx, innerWidth,
                    $"the {kv.Key} must fit the panel's {innerWidth}px inner width. It is " +
                    $"{kv.Value.Length} chars (~{kv.Value.Length * perCharPx:F0}px):\n\"{kv.Value}\"\n" +
                    "IMGUI CLIPS an over-long label, so an overflowing row is a number the Sponsor is not shown — the " +
                    "exact failure this round exists to close.");
        }

        [Test]
        public void TheHintBlock_FitsInsideThePanel_SoNoRowIsDrawnOutsideTheBox()
        {
            float lastRowBottom = AxeNudgeTool.FirstHintY +
                                  (AxeNudgeTool.HintRowCount - 1) * AxeNudgeTool.HintRowStep + AxeNudgeTool.HintRowStep;
            Assert.LessOrEqual(lastRowBottom, AxeNudgeTool.PanelHeight,
                $"the last hint row bottom ({lastRowBottom}px) must sit inside PanelHeight " +
                $"({AxeNudgeTool.PanelHeight}px). Round 3 added two measurement rows AND a second header line; without " +
                "this contract the next row added silently spills below the box, which is invisible in a code review " +
                "and only shows up in a capture.");
        }

        [Test]
        public void TheMineSeatHeader_FitsOneLine_SoItCannotWrapOntoTheValueRowBelow()
        {
            const float perCharPx = 9.5f;           // the 14px bold value style, per AxeNudgeToolPlayModeTests
            float innerWidth = AxeNudgeTool.PanelWidth - AxeNudgeTool.LabelInset;
            string drawn = "Editing: " + AxeNudgeTool.MineSeatHeader;
            Assert.LessOrEqual(drawn.Length * perCharPx, innerWidth,
                $"\"{drawn}\" is {drawn.Length} chars (~{drawn.Length * perCharPx:F0}px) against a {innerWidth}px inner " +
                "width. IMGUI WORD-WRAPS by default, and round 2's longer version wrapped onto the SeatOffsetDelta row " +
                "below it — visible only in the shipped panel capture.");
        }

        // ==============================================================================================================
        // helpers
        // ==============================================================================================================

        /// <summary>A grip read with the given along-haft positions and (by default) comfortably passing distances, so
        /// each test varies only the quantity it is about.</summary>
        private static TwoHandGripRead.Read MakeRead(float leftU, float rightU,
                                                     float leftHaft = 0.445f, float rightHaft = 0.012f) =>
            new TwoHandGripRead.Read
            {
                valid = true,
                leftHaftSW = leftHaft,
                rightHaftSW = rightHaft,
                leftU = leftU,
                rightU = rightU,
                toolVsHandLineDeg = 32.7f,
                handSepSW = 1.33f,
                shoulderWidth = 0.458f,
            };

        /// <summary>
        /// A minimal stand-in for the shipped held-tool hierarchy: hand bone -> tool root (HeldToolRig) -> mesh holder
        /// CHILD carrying a thin <paramref name="haftLen"/>-long stick along +Y, with its grip end at the mesh origin.
        ///
        /// That layout is not incidental — it reproduces the two shipped constraints the axis maths depends on: the mesh
        /// must live on a holder CHILD (the rig stomps its own transform every frame, #100 BUG-2) and the grip end sits
        /// at the mesh origin (blender-asset-pipeline.md §6), which is how TryGetHaftSegment decides which end is u=0.
        /// </summary>
        private static HeldToolRig BuildSyntheticRig(out GameObject root, out Transform hand, float haftLen)
        {
            hand = new GameObject("SyntheticHand").transform;
            root = new GameObject("SyntheticTool");
            root.transform.SetParent(hand, false);
            var rig = root.AddComponent<HeldToolRig>();
            rig.hand = hand;
            rig.character = null;              // no gate: the tests drive the weight explicitly

            var holder = new GameObject("WeaponMeshHolder").transform;
            holder.SetParent(root.transform, false);
            var mesh = new Mesh { name = "SyntheticHaft" };
            const float rad = 0.01f;
            mesh.vertices = new[]
            {
                new Vector3(-rad, 0f, -rad), new Vector3(rad, 0f, -rad),
                new Vector3(-rad, 0f, rad),  new Vector3(rad, 0f, rad),
                new Vector3(-rad, haftLen, -rad), new Vector3(rad, haftLen, -rad),
                new Vector3(-rad, haftLen, rad),  new Vector3(rad, haftLen, rad),
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3, 4, 6, 5, 5, 6, 7 };
            mesh.RecalculateBounds();
            holder.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            return rig;
        }

        /// <summary>Place the tool exactly where the production seat puts it at FULL mine weight, through the shipped
        /// <see cref="HeldToolRig.ComposeSeat"/> — never a mirrored formula beside it (the tautological-assert trap).</summary>
        private static void SeatAtFullMineWeight(HeldToolRig rig, Transform hand)
        {
            HeldToolRig.ComposeSeat(hand.position, hand.rotation, rig.seatOffsetFromHand, rig.seatEuler,
                                    rig.mineSeatOffsetDelta, rig.mineSeatEulerDelta, 1f,
                                    out Vector3 pos, out Quaternion rot);
            rig.transform.SetPositionAndRotation(pos, rot);
        }
    }
}
