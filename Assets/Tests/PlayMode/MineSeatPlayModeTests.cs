using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// 86cay4282 round 2 — LIVE-RIG proof of the MINE-STATE SEAT (the Sponsor's DIRECTION REVERSAL: "we need to
    /// position the axe for a two hand grip").
    ///
    /// THE ANCHOR (lowpoly-quality.md §0): a two-handed grip is ONE HAFT PASSING THROUGH BOTH HANDS. So the assert is
    /// not "a number changed" — it is that on the REAL skeleton, posed by the REAL clip, with the REAL weapon mesh
    /// seated by the REAL production seat code, BOTH hands end up on the haft LINE and the pre-fix geometry does not.
    ///
    /// WHY THIS IS THE LOAD-BEARING TEST. The EditMode <c>MineSeatTests</c> pin the gate, the ease, the thresholds,
    /// the byte-unchanged rest pose and the pure geometry — none of which can see whether the shipped delta actually
    /// lands the haft in the hands on the live posed rig. The delta is a large 6-DOF rotation+translation solved
    /// against measurements; a sign slip or an axis mix-up would satisfy every EditMode assert while putting the haft
    /// somewhere absurd. This measures the real thing and compares it against the ZERO-delta control on the SAME
    /// animation frames, so the improvement is a measured delta rather than a claim.
    ///
    /// PRODUCTION CODE, NOT A MIRROR: the seat is driven through <see cref="HeldToolRig.ApplySeat"/> (what LateUpdate
    /// calls) and the haft is resolved through <see cref="HeldToolRig.TryGetHaftSegment"/>, then scored by
    /// <see cref="TwoHandGripRead"/> — the same three pieces the F9 panel and the shipped-build gate use. Nothing
    /// re-implements the seat formula, so this cannot go green against a broken production path.
    ///
    /// HEADLESS-TICK TRAP (unity-conventions.md §Headless): a -batchmode frame has Time.deltaTime≈0, so the Animator
    /// never advances on the engine clock and <c>ApplySeat</c> would never build weight. Every tick here is an
    /// explicit <c>Animator.Update(dt)</c> + <c>ApplySeat(dt)</c> with a positive delta. No <c>WaitForEndOfFrame</c>
    /// (it does not fire in batchmode — procedural-animation-verbs.md).
    ///
    /// EDITOR-ONLY (loads the rig / controller / weapon FBX via AssetDatabase); Ignores in a player build.
    /// </summary>
    public class MineSeatPlayModeTests
    {
        // Mirrors of MovementCameraScene constants — the editor asmdef is intentionally NOT referenced by this
        // all-platform PlayTests asmdef (the same convention MineDeGripPlayModeTests follows). Every one of these is
        // PINNED against its real source by MineSeatTests.MirroredConstants_MatchTheShipSource, so a bake-time change
        // reds the EditMode gate instead of silently making this test measure a fiction.
        private const string ControllerPath = "Assets/Art/Character/Castaway/CastawayAnimator.controller";
        private const string RiggedFbxPath = "Assets/Art/Character/Castaway/v4/castaway_v4_rigged.fbx";
        private const string PickaxeFbxPath = "Assets/Art/Props/WeaponPack/wpn_pickaxe_stone_01.fbx";
        private static readonly Vector3 CarryRightEuler = new Vector3(-5f, -22f, 0f);   // CastawayV4RightArmEuler
        private static readonly Vector3 CarryLeftEuler = new Vector3(-5f, 22f, 0f);     // CastawayV4LeftArmEuler
        private static readonly Vector3 SeatOffset = new Vector3(0.0182f, 0.0415f, 0.0492f); // HeldAxeV4LocalOffsetFromHand
        private static readonly Vector3 SeatEuler = new Vector3(-48.9f, -125.0f, -106.3f);   // HeldAxeV4RelEuler
        private static readonly Vector3 MineSeatOffsetDelta = new Vector3(-0.2491f, -0.3928f, -0.3109f);
        private static readonly Vector3 MineSeatEulerDelta = new Vector3(-24.7f, 70.0f, 23.7f);
        // ⚠ LOAD-BEARING. CastawayHandPose (DefaultExecutionOrder 65) right-multiplies these onto the HAND bones —
        // AFTER CastawayArmPose (50) and BEFORE HeldToolRig (100), which seats the tool off hand.ROTATION. An earlier
        // version of this fixture omitted them, and so did the editor fit; both therefore agreed with each other at
        // 0.61 SW while the SHIPPED exe measured 1.22 SW. Two instruments sharing one blind spot look like
        // corroboration — only the shipped-build gate caught it. Never model this chain without order 65.
        private static readonly Vector3 RightWristEuler = new Vector3(-22.0f, 250.0f, -30.0f);  // CastawayV4RightWristEuler
        private static readonly Vector3 LeftWristEuler = new Vector3(-21.8f, 282.6f, 3.7f);     // CastawayV4LeftWristEuler
        private const float HeldScaleUniform = 0.45f;   // HeldAxeLocalScaleUniform
        private const float GripShiftY = 0f;            // HeldAxeGripShiftY
        private const float AvatarScale = 1.8f;
        private const float Dt = 1f / 60f;

        // 86cay4282 round 4 — the LEFT-ARM PIN's own mirrored ship constants + chain. Pinned against their real source
        // by LeftArmHaftPinTests.TheRuntimeFieldDefaults_MatchTheShipSourceConstants, same discipline as the seat's.
        private const float PinU = 0.35f;                  // MovementCameraScene.LeftArmHaftPinU
        private const float PinUCeiling = 0.80f;           // MovementCameraScene.LeftArmHaftPinUCeiling
        private const float ShellFraction = 0.98f;         // MovementCameraScene.LeftArmHaftShellFraction

        private GameObject _player, _tool;
        private Animator _animator;
        private HeldToolRig _rig;
        private Transform _lArm, _rArm, _lHand, _rHand;
        private Transform _lFore, _lKnuckle;
        private CastawayLeftArmHaftIk _leftIk;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedFbxPath);
            Assert.IsNotNull(fbx, "the live hero rig must exist at " + RiggedFbxPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller, "the shipped controller must exist at " + ControllerPath);
            var weapon = AssetDatabase.LoadAssetAtPath<GameObject>(PickaxeFbxPath);
            Assert.IsNotNull(weapon, "the shipped stone pickaxe must exist at " + PickaxeFbxPath);

            // Reproduce the shipped hierarchy: player root -> avatar (scaled 1.8) -> model.
            _player = new GameObject("MineSeatPlayer");
            var avatar = new GameObject("MineSeatAvatar");
            avatar.transform.SetParent(_player.transform, false);
            avatar.transform.localScale = Vector3.one * AvatarScale;
            var model = Object.Instantiate(fbx, avatar.transform, false);
            _animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _lArm = Find(model, "mixamorig:LeftArm"); _rArm = Find(model, "mixamorig:RightArm");
            _lHand = Find(model, "mixamorig:LeftHand"); _rHand = Find(model, "mixamorig:RightHand");
            Assert.IsNotNull(_lArm); Assert.IsNotNull(_rArm);
            Assert.IsNotNull(_lHand); Assert.IsNotNull(_rHand);

            // Reproduce AttachHeroAxeToHand + EnsureWeaponMeshHolder: the tool under the hand bone at the shipped
            // uniform scale, its MESH re-homed onto a WeaponMeshHolder CHILD the rig never touches (#100 BUG-2),
            // carrying the per-CLASS dial (HeldWeaponCycleDebug's shared pickaxe values — read from the Runtime
            // asmdef directly, so this half cannot drift at all).
            _tool = Object.Instantiate(weapon);
            _tool.name = "HeroAxe";
            _tool.transform.SetParent(_rHand, false);
            _tool.transform.localScale = Vector3.one * HeldScaleUniform;
            RehomeMeshOntoHolder(_tool, HeldWeaponCycleDebug.PickaxeStoneFamilyIndex);

            _rig = _tool.AddComponent<HeldToolRig>();
            _rig.hand = _rHand;
            _rig.seatOffsetFromHand = SeatOffset;
            _rig.seatEuler = SeatEuler;
            _rig.followDamp = 0f;
            _rig.mineSeatOffsetDelta = MineSeatOffsetDelta;
            _rig.mineSeatEulerDelta = MineSeatEulerDelta;
            _rig.character = null;   // no CastawayCharacter in this bare rig; the gate is driven explicitly below

            // 86cay4282 round 4 — the LEFT-ARM HAFT PIN, wired exactly as MovementCameraScene.AddLeftArmHaftIk does.
            // ⚠ THE KNUCKLE IS RESOLVED FROM A CANDIDATE LIST, not from 'LeftHandMiddle1': the v4 hero is a FIST-HAND
            // variant whose rig carries only index + thumb finger bones, so the obvious palm proxy does not exist on it
            // (measured — AttackClipPoseDiag prints the whole 18-bone hand subtree). Hard-coding Middle1 here would
            // leave _leftIk permanently inert and this fixture would then "prove" the pin works while measuring the
            // clip's own unpinned hand.
            _lFore = Find(model, "mixamorig:LeftForeArm");
            _lKnuckle = Find(model, "mixamorig:LeftHandMiddle1") ?? Find(model, "mixamorig:LeftHandIndex1");
            Assert.IsNotNull(_lFore, "the live rig must carry mixamorig:LeftForeArm — the IK's mid joint");
            Assert.IsNotNull(_lKnuckle, "the live rig must carry a palm-proxy knuckle bone, or there is no palm centre");
            _leftIk = _player.AddComponent<CastawayLeftArmHaftIk>();
            _leftIk.leftUpperArm = _lArm;
            _leftIk.leftForeArm = _lFore;
            _leftIk.leftHand = _lHand;
            _leftIk.leftPalmKnuckle = _lKnuckle;
            _leftIk.heldRig = _rig;
            _leftIk.character = null;      // gate driven explicitly, same as the seat
            _leftIk.modelFrame = model.transform;
            _leftIk.pinU = PinU;
            _leftIk.pinUCeiling = PinUCeiling;
            _leftIk.shellFraction = ShellFraction;

            // GEOMETRY SANITY — diagnose-via-trace, never assume. The Mixamo/Hyper3D FBX bakes a 100x cm->m scale on
            // the MODEL node (unity-conventions.md §FBX/rigs Bug B). If that survives into this rig the SKELETON is
            // 100x the tool, so every distance-to-haft figure is meaningless while still LOOKING plausible — a
            // shoulder-width-normalised number stays in a believable range because both hands are simply far from a
            // tiny haft. Log the three numbers that discriminate it and assert the tool/skeleton scales agree.
            ApplyPoseChain();
            _rig.ApplySeat(Dt);
            _rig.TryGetHaftSegment(out Vector3 g0, out Vector3 h0);
            float sw0 = (_rArm.position - _lArm.position).magnitude;
            Debug.Log($"[mineseat-rig] modelLocalScale={model.transform.localScale} " +
                      $"handLossy={_rHand.lossyScale} toolLossy={_tool.transform.lossyScale} " +
                      $"shoulderW={sw0:F4}m haftLen={(h0 - g0).magnitude:F4}m " +
                      $"haftInSW={(h0 - g0).magnitude / Mathf.Max(1e-5f, sw0):F2}");
            Assert.AreEqual(1.86f, (h0 - g0).magnitude / Mathf.Max(1e-5f, sw0), 0.20f,
                "the haft must measure ~1.86 shoulder-widths as the editor MINE-SEAT FIT pass measured it on the " +
                "live rig. A wildly different ratio means the rig's scales do not match production (the 100x " +
                "model-node trap) and NO grip figure from this fixture can be trusted.");
#endif
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.DestroyImmediate(_player);
        }

#if UNITY_EDITOR
        private static Transform Find(GameObject root, string bone)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == bone) return t;
            return null;
        }

        private static void RehomeMeshOntoHolder(GameObject weapon, int familyIndex)
        {
            var rootMf = weapon.GetComponent<MeshFilter>();
            var rootMr = weapon.GetComponent<MeshRenderer>();
            Assert.IsNotNull(rootMf, "the single-node weapon FBX must collapse its MeshFilter onto the root");
            var holder = new GameObject("WeaponMeshHolder");
            holder.transform.SetParent(weapon.transform, false);
            holder.transform.localPosition = new Vector3(0f, GripShiftY, 0f)
                                           + HeldWeaponCycleDebug.WeaponMeshLocalOffset[familyIndex];
            holder.transform.localRotation = Quaternion.Euler(HeldWeaponCycleDebug.WeaponMeshLocalEuler[familyIndex]);
            holder.transform.localScale = Vector3.one * HeldWeaponCycleDebug.WeaponMeshScale[familyIndex];
            var mf = holder.AddComponent<MeshFilter>();
            mf.sharedMesh = rootMf.sharedMesh;
            Object.DestroyImmediate(rootMf);
            if (rootMr != null) Object.DestroyImmediate(rootMr);
        }

        /// <summary>Pose the rig exactly as the shipped LateUpdate chain does, IN ORDER: CastawayArmPose (50) on the
        /// UPPER arms, then CastawayHandPose (65) on the HAND bones. Both are additive right-multiplies composed on
        /// the clip pose the Animator just wrote. The MINE de-grip is deliberately omitted — after the round-2
        /// reversal it ships ZERO, so the arms are exactly as authored.
        ///
        /// Order 65 is NOT optional here: HeldToolRig (100) seats the tool off hand.ROTATION, so skipping the wrist
        /// measures the tool against a right hand a quarter-turn from the live one.</summary>
        private void ApplyPoseChain()
        {
            _rArm.localRotation = _rArm.localRotation * Quaternion.Euler(CarryRightEuler);
            _lArm.localRotation = _lArm.localRotation * Quaternion.Euler(CarryLeftEuler);
            _rHand.localRotation = _rHand.localRotation * Quaternion.Euler(RightWristEuler);
            _lHand.localRotation = _lHand.localRotation * Quaternion.Euler(LeftWristEuler);
        }

        private void TriggerMineSwing()
        {
            _animator.SetBool(CastawayCharacter.GroundedParam, true);
            _animator.SetBool(CastawayCharacter.MovingParam, false);
            for (int i = 0; i < 20; i++) _animator.Update(0.05f);
            _animator.SetInteger(CastawayCharacter.WeaponClassParam, CastawayCharacter.WeaponClassPickaxe);
            _animator.SetTrigger(CastawayCharacter.ChopParam);
        }

        /// <summary>The PALM CENTRE, defined exactly as the production driver defines it (midpoint of the wrist bone and
        /// the resolved knuckle) — read from the LIVE bones, so this fixture cannot agree with a solver that is wrong.</summary>
        private Vector3 PalmWorld() => (_lHand.position + _lKnuckle.position) * 0.5f;

        private bool MineOwnsPose()
        {
            bool inTr = _animator.IsInTransition(0);
            int next = inTr ? _animator.GetNextAnimatorStateInfo(0).shortNameHash : 0;
            return CastawayCharacter.MineSwingOwnsPoseFor(
                _animator.GetCurrentAnimatorStateInfo(0).shortNameHash, inTr, next);
        }

        /// <summary>Seat the tool at a CHOSEN MINE weight and read the two-hand grip — all through production code:
        /// <see cref="HeldToolRig.ComposeSeat"/> (the exact composition LateUpdate writes) places the tool, then
        /// <see cref="HeldToolRig.TryGetHaftSegment"/> resolves the haft and <see cref="TwoHandGripRead"/> scores it.
        ///
        /// The weight is passed EXPLICITLY rather than produced by ticking the gate, for two reasons: a bare rig has
        /// no CastawayCharacter so the live gate would hold the weight at 0 (fail-closed — asserted separately), and
        /// forcing it lets the ON and OFF variants be measured on ONE identical animation frame. An A/B across
        /// different frames would be comparing POSES, not seats.</summary>
        private TwoHandGripRead.Read ReadAt(float weight)
        {
            HeldToolRig.ComposeSeat(_rHand.position, _rHand.rotation, SeatOffset, SeatEuler,
                                    MineSeatOffsetDelta, MineSeatEulerDelta, weight,
                                    out Vector3 pos, out Quaternion rot);
            _tool.transform.SetPositionAndRotation(pos, rot);
            if (!_rig.TryGetHaftSegment(out Vector3 grip, out Vector3 head)) return default;
            return TwoHandGripRead.Measure(_lArm.position, _rArm.position, _lHand.position, _rHand.position,
                                           grip, head);
        }
#endif

        /// <summary>
        /// 86cay4282 ROUND 4 — THE LOAD-BEARING TEST OF THIS ROUND, on the live posed skeleton.
        ///
        /// The Sponsor, soaking round 3, verbatim: <c>"R/V only manipulates the right hand, which is great, but what
        /// about the left hand? its not even touching the shaft"</c>. The seat moves the TOOL; only a per-frame solve
        /// moves the LEFT HAND. So the assert has two halves and the CONTROL half comes first, because a fix test whose
        /// control is not established proves nothing:
        ///
        ///   CONTROL — with the pin OFF (weight 0, i.e. the round-3 build) the palm must be measurably OFF the haft,
        ///             past the mesh-derived touching bound. If this ever stops holding, the premise died and the whole
        ///             round should be re-derived rather than the test relaxed.
        ///   FIX     — with the pin ON, at EVERY fully-engaged frame, the palm must be INSIDE that bound.
        ///
        /// Both are measured on the SAME animation frames through PRODUCTION code (<see cref="HeldToolRig.ApplySeat"/>
        /// then <see cref="CastawayLeftArmHaftIk.ApplyPin"/>, in the shipped 100→110 order), and the palm is read off the
        /// LIVE bones afterwards rather than from the solver's own prediction — round 2's lesson was that two
        /// instruments sharing one model agree with each other and disagree with the build.
        /// </summary>
        [UnityTest]
        public IEnumerator LeftArmPin_PutsThePALMOnTheHaft_WhereTheSeatAloneCannot()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the rig / controller / weapon FBX via AssetDatabase)");
            yield break;
#else
            yield return null;
            TriggerMineSwing();

            float worstPalmOff = -1f, worstPalmOn = -1f, worstWristOn = -1f;
            float minElbowOn = 999f, maxElbowOn = -999f;
            int frames = 0, reaching = 0, solved = 0, poleFallback = 0;
            float weight = 0f, sw = 1f;

            for (int f = 0; f < 200; f++)
            {
                _animator.Update(Dt);
                bool owns = MineOwnsPose();
                weight = CastawayArmPose.NextMineDeGripWeight(weight, owns, 12f, Dt);
                if (!owns) continue;
                ApplyPoseChain();                      // orders 50 + 65
                _rig.ApplySeat(Dt);                    // order 100 — the haft is only placed HERE
                if (!_rig.TryGetHaftSegment(out Vector3 grip, out Vector3 head)) continue;
                sw = (_rArm.position - _lArm.position).magnitude;
                if (sw < 1e-5f) continue;
                if (weight < 0.95f) continue;          // still handing over — not yet the judged pose

                // CONTROL: the palm BEFORE the pin runs (this is exactly the round-3 build's geometry).
                float palmOff = TwoHandGripRead.DistanceToSegment(PalmWorld(), grip, head, out _) / sw;

                // FIX — run the REAL driver. Two steps, in this order, both meaningful:
                //   (a) with the gate CLOSED (no character, force off) ApplyPin must write NOTHING. That is the
                //       fail-closed property asserted inline on every frame rather than once in a corner case.
                //   (b) then open the gate via debugForceEngaged (the CastawayFingerCurl.alwaysCurl idiom) and tick the
                //       PRODUCTION ApplyPin until the production ease saturates. Nothing about the strategy or the solve
                //       is substituted — only the animation-state gate, which is what a bare rig cannot supply.
                _leftIk.debugForceEngaged = false;
                _leftIk.ApplyPin(Dt);
                Assert.AreEqual(0f, _leftIk.PinWeight, 1e-6f,
                    "with no character wired and no force, the pin gate must stay CLOSED — a missing wire can never " +
                    "move the arm in the wrong state.");
                Assert.IsFalse(_leftIk.LastSolved, "…and it must not have written a bone");

                _leftIk.debugForceEngaged = true;
                for (int w = 0; w < 60; w++) _leftIk.ApplyPin(Dt);
                Assert.Greater(_leftIk.PinWeight, 0.99f,
                    "the production ease must saturate inside the swing (12/s ~= 0.25 s to 95%)");

                float palmOn = TwoHandGripRead.DistanceToSegment(PalmWorld(), grip, head, out _) / sw;
                float wristOn = TwoHandGripRead.DistanceToSegment(_lHand.position, grip, head, out _) / sw;
                float elbow = Vector3.Angle(_lArm.position - _lFore.position, PalmWorld() - _lFore.position);

                frames++;
                worstPalmOff = Mathf.Max(worstPalmOff, palmOff);
                worstPalmOn = Mathf.Max(worstPalmOn, palmOn);
                worstWristOn = Mathf.Max(worstWristOn, wristOn);
                minElbowOn = Mathf.Min(minElbowOn, elbow);
                maxElbowOn = Mathf.Max(maxElbowOn, elbow);
                if (_leftIk.LastSolved) solved++;
                if (_leftIk.SpanEmpty) reaching++;
                if (_leftIk.PoleFromFallback) poleFallback++;
            }

            Debug.Log($"[left-pin] engaged frames {frames}: worst PALM-to-haft {worstPalmOff:F3} -> {worstPalmOn:F3} SW " +
                      $"({worstPalmOff * sw * 100f:F1} -> {worstPalmOn * sw * 100f:F1} cm), cap " +
                      $"{TwoHandGripRead.LeftHaftPassSW:F3} SW ({TwoHandGripRead.LeftHaftPassSW * sw * 100f:F1} cm); " +
                      $"worst left WRIST after the pin {worstWristOn:F3} SW; elbow {minElbowOn:F0}..{maxElbowOn:F0}deg; " +
                      $"solved {solved}, REACHING {reaching}, pole-fallback {poleFallback}; 1 SW = {sw:F4} m");

            Assert.Greater(frames, 20, "need a meaningful sample of FULLY-ENGAGED swing frames (got " + frames + ")");
            Assert.Greater(solved, frames / 2, "the pin must actually solve on the majority of judged frames");

            // CONTROL — the defect must be present without the pin, or this test proves nothing about a fix.
            Assert.Greater(worstPalmOff, TwoHandGripRead.LeftHaftPassSW,
                $"CONTROL: without the pin the PALM must be measurably OFF the haft (worst {worstPalmOff:F3} SW = " +
                $"{worstPalmOff * sw * 100f:F1} cm, past the {TwoHandGripRead.LeftHaftPassSW:F3} SW touching bound). " +
                "That is the Sponsor's reported defect — 'its not even touching the shaft'.");

            // FIX — every judged frame must be inside the touching bound.
            Assert.LessOrEqual(worstPalmOn, TwoHandGripRead.LeftHaftPassSW,
                $"the PALM must be TOUCHING the haft at every judged frame (worst {worstPalmOn:F3} SW = " +
                $"{worstPalmOn * sw * 100f:F1} cm, cap {TwoHandGripRead.LeftHaftPassSW:F3} SW = " +
                $"{TwoHandGripRead.LeftHaftPassSW * sw * 100f:F1} cm). This is the round-4 anchor: one haft passing " +
                "through both hands means the shaft is inside the closed hand, not a quarter of a metre away.");

            // …and it must never have done so by locking the arm straight — the brief's named ugly failure mode.
            Assert.Less(maxElbowOn, 175f,
                $"the left elbow reached {maxElbowOn:F0}deg. A straight arm (180) reads as locked/dislocated; the reach " +
                "clamp exists to keep it strictly bent even when the haft is beyond reach.");
            yield return null;
#endif
        }

        // THE BUG-CLASS ASSERT: on the live posed skeleton the shipped seat delta must bring the LEFT hand ONTO the
        // haft the clip implies, at EVERY frame of the swing — and must not lift the RIGHT hand off it while doing so.
        [UnityTest]
        public IEnumerator MineSeat_PutsTheHaftThroughBothHands_AtEveryFrameOfTheMineSwing()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the rig / controller / weapon FBX via AssetDatabase)");
            yield break;
#else
            yield return null;
            TriggerMineSwing();

            float worstLeftOn = -1f, worstRightOn = -1f, worstLeftOff = -1f;
            float worstAngOn = -1f, worstAngOff = -1f;
            int frames = 0, easing = 0;
            float weight = 0f;

            for (int f = 0; f < 200; f++)
            {
                _animator.Update(Dt);
                bool owns = MineOwnsPose();
                // The PRODUCTION eased weight (the same policy function HeldToolRig.ApplySeat steps), tracked so the
                // fix is judged WHERE IT IS ENGAGED. This matters and was found by measurement: the gate is
                // transition-PAIRED, so it goes true on the FIRST frame of the AnyState->AttackPickaxe crossfade,
                // where the arms are still a BLEND of idle and the mine pose and the hand line points somewhere the
                // constant seat delta was never fitted to. During that window the eased weight is still ~0, so the
                // tool is correctly still at the approved one-handed seat — asserting the two-hand read there would
                // be asserting against a pose the player is not being shown as a mine swing.
                weight = CastawayArmPose.NextMineDeGripWeight(weight, owns, 12f, Dt);
                if (!owns) continue;
                ApplyPoseChain();

                var off = ReadAt(0f);   // pre-86cay4282 one-handed seat
                var on = ReadAt(1f);    // the shipped MINE seat, fully engaged — SAME animation frame
                if (!off.valid || !on.valid) continue;

                if (weight < 0.95f) { easing++; continue; }   // still handing the tool over — not yet the judged pose

                frames++;
                worstLeftOff = Mathf.Max(worstLeftOff, off.leftHaftSW);
                worstAngOff = Mathf.Max(worstAngOff, off.toolVsHandLineDeg);
                worstLeftOn = Mathf.Max(worstLeftOn, on.leftHaftSW);
                worstRightOn = Mathf.Max(worstRightOn, on.rightHaftSW);
                worstAngOn = Mathf.Max(worstAngOn, on.toolVsHandLineDeg);
            }

            Debug.Log($"[mineseat] engaged frames {frames} (+{easing} still easing in): worst left-to-haft " +
                      $"{worstLeftOff:F3} -> {worstLeftOn:F3} SW, worst right-to-haft {worstRightOn:F3} SW, " +
                      $"worst tool-vs-hand-line {worstAngOff:F1} -> {worstAngOn:F1} deg");
            Assert.Greater(frames, 20, "need a meaningful sample of FULLY-ENGAGED swing frames (got " + frames + ")");

            // 1. The DEFECT must actually be present without the delta — otherwise the test proves nothing about a
            //    fix (and would silently pass on a rig where the clip changed).
            Assert.Greater(worstLeftOff, TwoHandGripRead.LeftHaftPassSW,
                $"CONTROL: with a zero delta the left hand must be OFF the haft (worst {worstLeftOff:F3} SW) — that " +
                "is the Sponsor's reported defect. If this fails, the premise this fix rests on no longer holds.");

            // 2. The SEAT's own contribution: it must move the left hand MATERIALLY closer to the haft.
            //
            // ⚠ ROUND-4 CORRECTION TO THIS ASSERT. Round 3 asserted `worstLeftOn <= LeftHaftPassSW` — i.e. that the seat
            // ALONE landed the left hand inside the pass cap. That was only ever true because the cap (0.80 SW = 36.6 cm)
            // had been calibrated from what a constant seat could achieve. The Sponsor's soak then found the obvious
            // thing: 36.6 cm is not touching. The cap is now the mesh-derived TOUCHING bound measured against the PALM
            // (0.293 SW = 13.4 cm), and the seat alone does NOT reach it — the per-frame left-arm IK is what does, which
            // is asserted by LeftArmPin_PutsThePALMOnTheHaft_WhereTheSeatAloneCannot below. Re-pointing this assert at
            // the seat's OWN measured achievement keeps its regression value (a reverted/inverted delta still reds)
            // without re-stating the false claim that the seat closes the grip.
            const float SeatAloneWorstLeftWristSW = 0.615f;   // AttackClipPoseDiag MINE-SEAT FIT, live re-measure
            Assert.LessOrEqual(worstLeftOn, SeatAloneWorstLeftWristSW * 1.15f,
                $"the SEAT must still deliver its own measured improvement (worst left wrist {worstLeftOn:F3} SW vs the " +
                $"measured {SeatAloneWorstLeftWristSW:F3}); a reverted, inverted or ungated delta blows past this.");
            Assert.Less(worstLeftOn, worstLeftOff * 0.6f,
                $"…and it must be a LARGE improvement over the zero-delta control ({worstLeftOff:F3} -> " +
                $"{worstLeftOn:F3} SW), not a rounding difference.");
            Assert.LessOrEqual(worstRightOn, TwoHandGripRead.RightHaftPassSW,
                $"the RIGHT hand must STAY on the haft (worst {worstRightOn:F3} SW, cap " +
                $"{TwoHandGripRead.RightHaftPassSW:F2}) — the tool is physically seated in that hand, so solving the " +
                "left-hand read by pulling the haft out of the right hand trades the defect for a worse one.");

            // 3. And the tool must stop DISAGREEING with the grip the eye reads (the dominant percept: pre-fix the
            //    tool sat up to 89.7 deg off the line through both hands).
            Assert.Less(worstAngOn, worstAngOff * 0.75f,
                $"the tool must line up with the implied haft materially better: {worstAngOff:F1} -> {worstAngOn:F1} " +
                "deg off the hand line. A sign/axis error would leave this flat or worse while distances happened to " +
                "improve.");
            yield return null;
#endif
        }

        // The regression guard for the Sponsor's approved one-handed seat: outside the mine swing the gate is
        // released, so the seat the other 14 baked held poses share is untouched.
        [UnityTest]
        public IEnumerator OutsideTheMineSwing_TheSeatGateIsReleased_SoTheApprovedSeatIsUntouched()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the rig / controller / weapon FBX via AssetDatabase)");
            yield break;
#else
            yield return null;
            _animator.SetBool(CastawayCharacter.GroundedParam, true);
            _animator.SetBool(CastawayCharacter.MovingParam, false);
            for (int i = 0; i < 40; i++) _animator.Update(0.05f);
            Assert.IsFalse(MineOwnsPose(), "settled idle must not engage the mine seat");

            // The AXE swing — the sibling clip that measured one-handed — must stay out of the gate too, or its
            // Sponsor-approved seat would be yanked onto a hand line it does not have.
            _animator.SetInteger(CastawayCharacter.WeaponClassParam, CastawayCharacter.WeaponClassAxe);
            _animator.SetTrigger(CastawayCharacter.ChopParam);
            bool everEngaged = false;
            for (int f = 0; f < 200; f++)
            {
                _animator.Update(Dt);
                if (MineOwnsPose()) { everEngaged = true; break; }
            }
            Assert.IsFalse(everEngaged,
                "the axe chop must NEVER engage the mine seat delta — it measured one-handed (tool 6.8 deg off its " +
                "own hand line at the strike) and its seat is Sponsor-approved across five soak rounds.");
            yield return null;
#endif
        }

        // A bare rig with no CastawayCharacter must never engage the delta — FAIL-CLOSED toward leaving the approved
        // seat alone. This is the shape that would otherwise ship the delta in every state on a mis-wired scene.
        [UnityTest]
        public IEnumerator WithNoCharacterWired_TheSeatWeightStaysZero_AndTheSeatIsTheApprovedOne()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the rig / controller / weapon FBX via AssetDatabase)");
            yield break;
#else
            yield return null;
            _rig.character = null;
            for (int f = 0; f < 60; f++) { _animator.Update(Dt); _rig.ApplySeat(Dt); }

            Assert.AreEqual(0f, _rig.MineSeatWeight, 1e-5f,
                "with no character to read the AttackPickaxe gate from, the seat weight must stay 0 — fail-closed, " +
                "the same polarity CastawayCharacter.MineSwingOwnsPose uses, because this offset can only ever ADD.");

            // …and the resulting pose must be the un-delta'd approved seat, exactly.
            Vector3 expectPos = _rHand.position + _rHand.rotation * SeatOffset;
            Quaternion expectRot = _rHand.rotation * Quaternion.Euler(SeatEuler);
            Assert.Less((_tool.transform.position - expectPos).magnitude, 1e-4f,
                "the seated POSITION must be the approved one-handed seat with no delta applied");
            Assert.Less(Quaternion.Angle(_tool.transform.rotation, expectRot), 1e-2f,
                "the seated ROTATION must be the approved one-handed seat with no delta applied");
            yield return null;
#endif
        }
    }
}
