using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cay4282 round 4 — THE LEFT-HAND HAFT PIN. The Sponsor, soaking round 3, verbatim: <c>"R/V only manipulates the
    /// right hand, which is great, but what about the left hand? its not even touching the shaft"</c>.
    ///
    /// WHAT THIS FILE PINS, and each is a CLASS rather than a value:
    ///   1. THE PIN STRATEGY THE MEASUREMENTS FORCED. The obvious design — pin at a fixed u and blend out when
    ///      unreachable — is REFUTED by measurement (a fixed pin is outside the arm's 54.0 cm reach on ~64% of judged
    ///      frames at EVERY u, so it would ship an inert driver). The shipped strategy clamps the pin into the
    ///      reachable span and, when NONE of the haft is reachable, aims at the haft's closest point rather than
    ///      handing the frame back to the clip. Both branches are asserted, including the fallback, because the
    ///      fallback is the COMMON path (80/166 measured frames) and not an edge case.
    ///   2. THE DIAL IS HONEST. A slide moves the pin by the requested distance along the haft, up-then-down
    ///      round-trips exactly, the ceiling is respected, and a refused slide changes NOTHING.
    ///   3. REST IS BYTE-UNCHANGED. At weight 0 the driver writes no bone at all, so every non-mining state — carry,
    ///      idle, walk, run, jump and the other four swings — is bit-for-bit unaffected.
    ///   4. THE SHIP SOURCE. MovementCameraScene.LeftArmHaft* is what the bootstrap bakes, so a drifting runtime field
    ///      default cannot silently become the shipped value.
    ///   5. THE PANEL DRAWS WHAT A HUMAN JUDGES ON — the fourth measurement row fits the box and the row block does not
    ///      collide with the hint block. This ticket has shipped a computed-but-undrawn quantity TWICE (hand separation
    ///      in round 1, the along-haft position in round 2), and round 3 shipped a row CLIPPED by its own box.
    /// </summary>
    public class LeftArmHaftPinTests
    {
        // The measured left-arm chain (AttackClipPoseDiag `[left-ik]`, castaway v4, 166 judged frames).
        private const float ALen = 0.2819f;      // shoulder -> elbow
        private const float BLen = 0.2582f;      // elbow -> palm centre
        private const float FullExtension = ALen + BLen;   // 0.5401 m

        // =================================================================================================
        // 1 — THE PIN STRATEGY.
        // =================================================================================================

        [Test]
        public void WhenTheHaftIsReachable_ThePinHonoursThePreferredU()
        {
            // A haft laid out so its whole length sits comfortably inside the shell: the preference must pass through
            // untouched, because the reachable span contains it.
            Vector3 shoulder = Vector3.zero;
            Vector3 grip = new Vector3(0.10f, 0f, 0f), head = new Vector3(0.30f, 0f, 0f);
            float u = CastawayLeftArmHaftIk.ResolvePinU(shoulder, grip, head, ALen, BLen,
                                                        preferredU: 0.35f, uCeiling: 0.80f,
                                                        shellFraction: 0.98f, out bool spanEmpty);
            Assert.IsFalse(spanEmpty, "a haft entirely inside the shell must report a non-empty reachable span");
            Assert.AreEqual(0.35f, u, 1e-4f,
                "the Sponsor's preferred pin must pass through UNCHANGED wherever the arm can reach it — a dial that " +
                "silently overrides its own input is indistinguishable from a broken one.");
        }

        [Test]
        public void WhenOnlyPARTOfTheHaftIsReachable_ThePinIsClampedIntoTheReachablePart_NotDropped()
        {
            // The grip end is close, the head end is far beyond reach: the span is a PREFIX of the haft. A preference
            // past its end must clamp to the boundary rather than being abandoned.
            Vector3 shoulder = Vector3.zero;
            Vector3 grip = new Vector3(0.20f, 0f, 0f);
            Vector3 head = new Vector3(2.00f, 0f, 0f);      // far outside a 0.53 m shell
            float u = CastawayLeftArmHaftIk.ResolvePinU(shoulder, grip, head, ALen, BLen,
                                                        preferredU: 0.80f, uCeiling: 0.80f,
                                                        shellFraction: 0.98f, out bool spanEmpty);
            Assert.IsFalse(spanEmpty);
            Assert.Less(u, 0.80f, "the requested u is unreachable, so it must be pulled back to the span's edge");
            // …and the clamped point must genuinely be inside the shell, which is the whole property that keeps the
            // elbow bent. Asserted on the RESULT, not on the arithmetic that produced it.
            Vector3 pinned = Vector3.Lerp(grip, head, u);
            Assert.LessOrEqual((pinned - shoulder).magnitude, FullExtension * 0.98f + 1e-3f,
                "a clamped pin that still sits outside the shell has not been clamped at all");
        }

        [Test]
        public void WhenNOPartOfTheHaftIsReachable_ThePinFallsBackToItsCLOSESTPoint_NotToTheClipPose()
        {
            // THE MEASURED COMMON CASE (80/166 judged frames: the whole haft up to 63.4 cm from a 54.0 cm arm). The
            // decision this asserts is the load-bearing one of the whole round: blending OUT here would hand the frame
            // back to the clip's 20-28 cm gap on roughly half the swing — worse than a reach.
            Vector3 shoulder = Vector3.zero;
            Vector3 grip = new Vector3(1.5f, 0.4f, 0f), head = new Vector3(1.5f, -0.6f, 0f);   // a wall of haft, all far
            float u = CastawayLeftArmHaftIk.ResolvePinU(shoulder, grip, head, ALen, BLen,
                                                        preferredU: 0.35f, uCeiling: 0.80f,
                                                        shellFraction: 0.98f, out bool spanEmpty);
            Assert.IsTrue(spanEmpty, "none of this haft is inside the shell, and the driver must SAY so (the panel and " +
                                     "the shipped gate both surface it as 'REACHING')");

            // The fallback must be the point of the haft NEAREST the shoulder — i.e. the best a reaching arm can do.
            // Verified against an independent brute-force scan, not against the closed form that produced it.
            float best = 0f, bestD = float.MaxValue;
            for (int i = 0; i <= 800; i++)
            {
                float t = 0.80f * i / 800f;
                float d = (Vector3.Lerp(grip, head, t) - shoulder).magnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            Assert.AreEqual(best, u, 2e-3f,
                $"the fallback pin ({u:F3}) must be the haft's CLOSEST point to the shoulder ({best:F3}). Any other " +
                "choice leaves the palm further from the shaft than the arm actually requires.");
        }

        [Test]
        public void ThePin_NeverExceedsTheMeshMeasuredCeiling_SoThePalmIsNeverInsideTheToolHead()
        {
            // u > 0.80 puts the palm inside the pick HEAD mass (AttackClipPoseDiag `[haft-profile]`), which reads worse
            // than the defect being fixed. The ceiling must bind in BOTH branches, so both are exercised here.
            Vector3 shoulder = Vector3.zero;
            // reachable branch: a short, close haft with a greedy request
            float a = CastawayLeftArmHaftIk.ResolvePinU(shoulder, new Vector3(0.10f, 0f, 0f),
                                                        new Vector3(0.30f, 0f, 0f), ALen, BLen,
                                                        preferredU: 1.0f, uCeiling: 0.80f, shellFraction: 0.98f, out _);
            Assert.LessOrEqual(a, 0.80f + 1e-4f);
            // fallback branch: the closest point is genuinely past the ceiling, so the ceiling must still win
            float b = CastawayLeftArmHaftIk.ResolvePinU(shoulder, new Vector3(3f, 0f, 0f),
                                                        new Vector3(0.9f, 0f, 0f), ALen, BLen,
                                                        preferredU: 0.35f, uCeiling: 0.80f, shellFraction: 0.98f,
                                                        out bool empty);
            Assert.IsTrue(empty);
            Assert.LessOrEqual(b, 0.80f + 1e-4f,
                "even the closest-point fallback must respect the head ceiling — otherwise the 'best reach' is a palm " +
                "buried in the pick.");
        }

        [Test]
        public void ADegenerateHaft_ResolvesToZero_WithoutDividingByIt()
        {
            float u = CastawayLeftArmHaftIk.ResolvePinU(Vector3.zero, Vector3.one, Vector3.one, ALen, BLen,
                                                        0.35f, 0.80f, 0.98f, out bool empty);
            Assert.AreEqual(0f, u, "a zero-length haft must resolve to 0, never NaN");
            Assert.IsFalse(float.IsNaN(u));
            Assert.IsTrue(empty, "…and it must report an empty span rather than claiming reachability");
        }

        [Test]
        public void ALOWERShellFraction_ShrinksTheReachableSpan_WhichIsTheTradeTheZXDialExposes()
        {
            // The trade the [Z]/[X] knob exists for, asserted as a real relationship rather than described in a comment:
            // a bendier arm (lower shell) can hold LESS of the haft. Measured consequence over the shipped clip:
            // shell 0.98 -> 80/166 fallback frames and a 10.7 cm worst palm gap; shell 0.90 -> 110/166 and 15.0 cm.
            Vector3 shoulder = Vector3.zero;
            Vector3 grip = new Vector3(0.20f, 0f, 0f), head = new Vector3(0.90f, 0f, 0f);
            float wide = CastawayLeftArmHaftIk.ResolvePinU(shoulder, grip, head, ALen, BLen, 0.80f, 0.80f, 0.98f,
                                                           out bool e1);
            float tight = CastawayLeftArmHaftIk.ResolvePinU(shoulder, grip, head, ALen, BLen, 0.80f, 0.80f, 0.90f,
                                                            out bool e2);
            Assert.IsFalse(e1); Assert.IsFalse(e2);
            Assert.Greater(wide, tight,
                $"a straighter-allowed arm must reach FURTHER up the haft (shell 0.98 -> u {wide:F3}, shell 0.90 -> u " +
                $"{tight:F3}). If these were equal the [Z]/[X] dial would be inert, which is the 'wired but silently " +
                "no-ops' trap this tool has been bitten by three times.");
        }

        // =================================================================================================
        // 2 — THE DIAL.
        // =================================================================================================

        [Test]
        public void SlidingThePin_MovesItByExactlyTheRequestedDistanceAlongTheHaft_AndRoundTripsExactly()
        {
            var ik = BuildRig(out GameObject root, haftLen: 2f);
            try
            {
                ik.pinU = 0.40f;
                ik.pinUCeiling = 1f;
                const float slide = 0.20f;                    // metres, i.e. 0.10 of this 2 m haft
                Assert.IsTrue(ik.TrySlidePinAlongHaft(slide));
                Assert.AreEqual(0.50f, ik.pinU, 1e-3f,
                    "a POSITIVE slide moves the pin UP the haft toward the HEAD by exactly the requested distance — the " +
                    "step keeps its physical meaning (2 cm of stick) across weapons with different haft lengths.");

                Assert.IsTrue(ik.TrySlidePinAlongHaft(-slide));
                Assert.AreEqual(0.40f, ik.pinU, 1e-3f,
                    "up-then-down must return the Sponsor exactly where he started; a dial that drifts on a round-trip " +
                    "cannot be explored — he would be unable to get back to a grip he liked.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SlidingThePin_IsClampedToTheCeilingAndToZero_NeverOffTheStick()
        {
            var ik = BuildRig(out GameObject root, haftLen: 1f);
            try
            {
                ik.pinUCeiling = 0.80f;
                ik.pinU = 0.75f;
                Assert.IsTrue(ik.TrySlidePinAlongHaft(5f));
                Assert.AreEqual(0.80f, ik.pinU, 1e-4f, "the pin must stop at the mesh-measured head ceiling");
                Assert.IsTrue(ik.TrySlidePinAlongHaft(-5f));
                Assert.AreEqual(0f, ik.pinU, 1e-4f, "…and at the butt end, never a negative u");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void WithNoResolvableMesh_ThePinSlideIsREFUSED_AndChangesNothing()
        {
            var hand = new GameObject("Hand");
            var go = new GameObject("NoMeshRig");
            try
            {
                go.transform.SetParent(hand.transform, false);
                var rig = go.AddComponent<HeldToolRig>();
                rig.hand = hand.transform;
                var ik = hand.AddComponent<CastawayLeftArmHaftIk>();
                ik.heldRig = rig;
                float before = ik.pinU;
                Assert.IsFalse(ik.TrySlidePinAlongHaft(0.2f),
                    "with no displayed mesh the haft is unknown; the slide must refuse rather than guess a length");
                Assert.AreEqual(before, ik.pinU, "a refused slide must change NOTHING");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(hand); }
        }

        [Test]
        public void TheReachDial_CannotBePushedToAFullyStraightArm_HoweverHardItIsPressed()
        {
            var ik = BuildRig(out GameObject root, haftLen: 1f);
            try
            {
                for (int i = 0; i < 500; i++) ik.NudgeShellFraction(0.01f);
                Assert.LessOrEqual(ik.shellFraction, TwoBoneIkSolver.StraightArmFraction + 1e-5f,
                    "[Z] held down must not be able to request a locked arm — the ceiling is the solver's, and the dial " +
                    "is clamped to it. This is the 'unclamped write ran to 390deg and wrapped' trap the GRIP-CURL dial " +
                    "already paid for once.");
                for (int i = 0; i < 500; i++) ik.NudgeShellFraction(-0.01f);
                Assert.GreaterOrEqual(ik.shellFraction, 0.5f, "…and [X] must not collapse it to nothing either");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // =================================================================================================
        // 3 — REST IS BYTE-UNCHANGED (the regression guard for every non-mining state).
        // =================================================================================================

        [Test]
        public void AtZeroWeight_NoBoneIsWritten_SoEveryNonMiningStateIsByteUnchanged()
        {
            var ik = BuildRig(out GameObject root, haftLen: 1f);
            try
            {
                // A full chain, positioned so a solve WOULD move the bones if it ran — the control for this assert. With
                // no CastawayCharacter wired the gate reads closed (fail-closed), so the weight stays 0.
                BuildChain(ik, root);
                Quaternion upper0 = ik.leftUpperArm.rotation, lower0 = ik.leftForeArm.rotation;

                ik.ApplyPin(1f / 60f);

                Assert.AreEqual(0f, ik.PinWeight, 1e-6f,
                    "with no character wired the AttackPickaxe gate must read CLOSED — fail-closed toward the clip's " +
                    "own authored left arm, so a missing wire can never move the arm in the wrong state.");
                Assert.IsFalse(ik.LastSolved);
                Assert.AreEqual(upper0, ik.leftUpperArm.rotation,
                    "the upper arm must be BIT-FOR-BIT untouched at weight 0 — this driver is shared with every " +
                    "locomotion state and the other four swings.");
                Assert.AreEqual(lower0, ik.leftForeArm.rotation, "…and the forearm likewise.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TheReachHoldBand_ClearsTheMEASUREDWorstOverReach_OrThePinRunsAtPartialStrength()
        {
            // The shipped gate caught this: with no hold band the blend-out fires on the frames with the LARGEST
            // over-reach and the pin runs at partial strength there, which is what produced round 4's first FAIL
            // (reachWeight 0.65, palm 13.5 cm vs a 13.0 cm bound). The band must clear the measured worst over-reach
            // with margin, or the same defect returns silently.
            const float MeasuredWorstOverReachM = 0.105f;   // AttackClipPoseDiag [left-span] / the shipped gate
            Assert.Greater(MovementCameraScene.LeftArmHaftReachHoldMetres, MeasuredWorstOverReachM * 1.5f,
                $"the hold band ({MovementCameraScene.LeftArmHaftReachHoldMetres:F2} m) must clear the measured worst " +
                $"over-reach ({MeasuredWorstOverReachM:F3} m) with real margin — otherwise the pin eases out exactly " +
                "where it is needed most.");
        }

        [Test]
        public void TheForceEngageFlag_ShipsFALSE_SoGameplayIsGatedPurelyOnTheAnimationState()
        {
            var go = new GameObject("ForceDefault");
            try
            {
                Assert.IsFalse(go.AddComponent<CastawayLeftArmHaftIk>().debugForceEngaged,
                    "debugForceEngaged must ship FALSE. It exists so the F9 panel can show the Sponsor the pin at idle " +
                    "(the CastawayFingerCurl.alwaysCurl idiom — an engagement-weighted dial with no visible effect is " +
                    "indistinguishable from a broken one). Shipping it TRUE would pin the left arm to the haft in EVERY " +
                    "state, which is the stranded-curl bug that idiom already paid for once.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ForcingEngagement_ActuallyRaisesTheWeight_SoThePanelsDialsAreNotInertAtIdle()
        {
            var ik = BuildRig(out GameObject root, haftLen: 1f);
            try
            {
                BuildChain(ik, root);
                // Control: gate closed => weight stays 0 (the fail-closed property).
                for (int i = 0; i < 40; i++) ik.ApplyPin(1f / 60f);
                Assert.AreEqual(0f, ik.PinWeight, 1e-6f);

                ik.debugForceEngaged = true;
                for (int i = 0; i < 60; i++) ik.ApplyPin(1f / 60f);
                Assert.Greater(ik.PinWeight, 0.99f,
                    "forcing the gate must drive the PRODUCTION ease to full weight — otherwise the F9 panel's force " +
                    "does nothing and the dials are still silently inert at idle, i.e. the trap is unfixed.");
                Assert.IsTrue(ik.LastSolved, "…and the driver must actually have written a pose");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void WithTheKnuckleUNWIRED_TheDriverIsInert_NeverPinningTheWristInstead()
        {
            var ik = BuildRig(out GameObject root, haftLen: 1f);
            try
            {
                BuildChain(ik, root);
                ik.leftPalmKnuckle = null;                  // the v4 fist-hand rig's proxy, missing
                Assert.IsFalse(ik.TryGetPalmWorld(out _),
                    "without a knuckle there IS no palm centre, and the driver must say so rather than substituting " +
                    "the wrist — pinning the wrist would drive the haft through the back of the hand by 5.6 cm, i.e. " +
                    "fix the metric while making the percept worse.");
                Quaternion upper0 = ik.leftUpperArm.rotation;
                ik.ApplyPin(1f / 60f);
                Assert.AreEqual(upper0, ik.leftUpperArm.rotation, "…and it must write nothing at all");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // =================================================================================================
        // 4 — THE SHIP SOURCE + 5 — THE PANEL.
        // =================================================================================================

        [Test]
        public void TheRuntimeFieldDefaults_MatchTheShipSourceConstants()
        {
            // The bootstrap bakes MovementCameraScene.LeftArmHaft* into Boot.unity; the C# field defaults are only the
            // rollback path. They are asserted EQUAL so a value edited in one place and not the other cannot ship a
            // build whose runtime fallback silently disagrees with the baked scene (the mirrored-constant drift hole
            // MineSeatTests already closed for the seat).
            var go = new GameObject("PinDefaults");
            try
            {
                var ik = go.AddComponent<CastawayLeftArmHaftIk>();
                Assert.AreEqual(MovementCameraScene.LeftArmHaftPinU, ik.pinU, 1e-4f);
                Assert.AreEqual(MovementCameraScene.LeftArmHaftPinUCeiling, ik.pinUCeiling, 1e-4f);
                Assert.AreEqual(MovementCameraScene.LeftArmHaftShellFraction, ik.shellFraction, 1e-4f);
                Assert.AreEqual(MovementCameraScene.LeftArmHaftReachHoldMetres, ik.reachHoldMetres, 1e-4f);
                Assert.Less((MovementCameraScene.LeftArmHaftPoleFallback - ik.poleFallbackLocal).magnitude, 1e-3f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheShippedPinU_SitsInsideTheMEASUREDReachableWindow()
        {
            // Measured against the shipped seat (AttackClipPoseDiag `[left-span]`): the reachable interval over the bare
            // haft runs lo 0.00..0.52 / hi 0.15..0.80, so the window a pin is honoured in at some point of the swing is
            // u 0.14..0.61. A shipped pin outside it would be clamped away on EVERY frame — a dial pointing at nothing.
            const float WindowLo = 0.14f, WindowHi = 0.61f;
            Assert.GreaterOrEqual(MovementCameraScene.LeftArmHaftPinU, WindowLo);
            Assert.LessOrEqual(MovementCameraScene.LeftArmHaftPinU, WindowHi,
                $"the shipped pin ({MovementCameraScene.LeftArmHaftPinU:F2}) must sit inside the measured reachable " +
                $"window ({WindowLo:F2}..{WindowHi:F2}); outside it the reach clamp overrides the value on every frame.");
            Assert.LessOrEqual(MovementCameraScene.LeftArmHaftPinU, MovementCameraScene.LeftArmHaftPinUCeiling,
                "…and below the mesh-measured head ceiling.");
        }

        [Test]
        public void TheIkStateRow_DrawsTheREQUESTEDAndTheACHIEVEDPin_NotJustOne()
        {
            // A dial whose achieved value can differ from its request MUST show BOTH, or the Sponsor reads a clamped
            // frame as the tool ignoring his input. Third instance of this ticket's recurring class (round 1: hand
            // separation undrawn; round 2: the along-haft position undrawn; round 3: the verdict row clipped).
            string absent = AxeNudgeTool.IkStateLine(null);
            StringAssert.Contains("ABSENT", absent,
                "with no IK in the scene the row must say so — a blank row reads as 'nothing to report' when the actual " +
                "state is 'the whole fix is missing from this build'.");

            var go = new GameObject("PinRow");
            try
            {
                var ik = go.AddComponent<CastawayLeftArmHaftIk>();
                ik.pinU = 0.37f;
                string line = AxeNudgeTool.IkStateLine(ik);
                StringAssert.Contains(0.37f.ToString("F2"), line, "the REQUESTED pin must be drawn");
                StringAssert.Contains("got", line, "…and the ACHIEVED pin must be drawn beside it");
                StringAssert.Contains(ik.shellFraction.ToString("F2"), line,
                    "…and the reach-shell value the [Z]/[X] keys move, or the Sponsor cannot see what he just changed");
                StringAssert.Contains("[Z]", line, "…with the keys named inline, since he reads this in a build");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheMeasurementRowBlock_DoesNotCollideWithTheHintBlock()
        {
            // Round 3 added rows by hand-editing four Y offsets and the header wrapped onto the value row below it —
            // caught only by eyeballing a shipped capture. The block geometry is consts now, so the collision is a test.
            float measBottom = AxeNudgeTool.FirstMeasY +
                               (AxeNudgeTool.MeasRowCount - 1) * AxeNudgeTool.MeasRowStep + 20f;
            Assert.LessOrEqual(measBottom, AxeNudgeTool.FirstHintY,
                $"the {AxeNudgeTool.MeasRowCount} measurement rows end at {measBottom}px but the hint block starts at " +
                $"{AxeNudgeTool.FirstHintY}px — they would overdraw each other, which renders as unreadable text rather " +
                "than as a missing row, so it is invisible in a code review.");

            float hintBottom = AxeNudgeTool.FirstHintY +
                               (AxeNudgeTool.HintRowCount - 1) * AxeNudgeTool.HintRowStep + AxeNudgeTool.HintRowStep;
            Assert.LessOrEqual(hintBottom, AxeNudgeTool.PanelHeight,
                $"the last hint row bottom ({hintBottom}px) must sit inside PanelHeight ({AxeNudgeTool.PanelHeight}px)");
        }

        // =================================================================================================
        // helpers
        // =================================================================================================

        /// <summary>A minimal stand-in for the shipped hierarchy: hand bone -> tool root (HeldToolRig) -> mesh holder
        /// CHILD carrying a thin haft along +Y with its grip end at the mesh origin. That layout is not incidental — it
        /// reproduces the two shipped constraints the haft resolution depends on (the mesh must live on a holder CHILD,
        /// #100 BUG-2; the grip end sits at the mesh origin, blender-asset-pipeline.md §6).</summary>
        private static CastawayLeftArmHaftIk BuildRig(out GameObject root, float haftLen)
        {
            root = new GameObject("PinTestRoot");
            var hand = new GameObject("Hand").transform;
            hand.SetParent(root.transform, false);
            var tool = new GameObject("Tool");
            tool.transform.SetParent(hand, false);
            var rig = tool.AddComponent<HeldToolRig>();
            rig.hand = hand;
            rig.character = null;

            var holder = new GameObject("WeaponMeshHolder").transform;
            holder.SetParent(tool.transform, false);
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

            var ik = root.AddComponent<CastawayLeftArmHaftIk>();
            ik.heldRig = rig;
            ik.character = null;               // gate closed unless a test wires one
            ik.modelFrame = root.transform;
            return ik;
        }

        /// <summary>Add a bent left-arm chain with a palm knuckle, so the driver has something real to solve.</summary>
        private static void BuildChain(CastawayLeftArmHaftIk ik, GameObject root)
        {
            var shoulder = new GameObject("mixamorig:LeftArm").transform;
            shoulder.SetParent(root.transform, false);
            shoulder.localPosition = new Vector3(-0.2f, 1.4f, 0f);
            var elbow = new GameObject("mixamorig:LeftForeArm").transform;
            elbow.SetParent(shoulder, false);
            elbow.localPosition = Vector3.down * ALen;
            var wrist = new GameObject("mixamorig:LeftHand").transform;
            wrist.SetParent(elbow, false);
            wrist.localPosition = Vector3.forward * (BLen * 0.6f);
            var knuckle = new GameObject("mixamorig:LeftHandIndex1").transform;
            knuckle.SetParent(wrist, false);
            knuckle.localPosition = Vector3.forward * (BLen * 0.8f);
            ik.leftUpperArm = shoulder;
            ik.leftForeArm = elbow;
            ik.leftHand = wrist;
            ik.leftPalmKnuckle = knuckle;
        }
    }
}
