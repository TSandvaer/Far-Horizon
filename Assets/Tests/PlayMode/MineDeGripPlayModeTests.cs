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
    /// 86cay4282 — LIVE-RIG proof of the MINE DE-GRIP. The EditMode <c>MineDeGripTests</c> pin the pure gate + the
    /// ease + the byte-unchanged rest pose; this pins the two claims they CANNOT:
    ///   (a) the shipped controller really reaches <c>AttackPickaxe</c> from the mine trigger, and
    ///   (b) the offset actually SEPARATES the hands on the real skeleton posed by the real clip.
    ///
    /// WHY (b) IS THE LOAD-BEARING ASSERT. The defect is defined by hand SEPARATION (measured: the mine clip locks
    /// the hands 1.09-1.29 shoulder-widths apart across the whole swing, tighter than the approved idle carry's
    /// 1.65-1.89), and the correcting axis is NOT derivable from the rig's documented cheat-sheet — the axis probe
    /// in AttackClipPoseDiag measured that on the LEFT upper arm a NEGATIVE local-X separates the hands (1.08 ->
    /// 1.26 at the tightest frame) while the +X the cheat-sheet calls "outward" pulls them TOGETHER (1.08 -> 0.86),
    /// because the left arm is reaching ACROSS the body in this clip. A sign error is therefore both easy and
    /// invisible to any test that only checks the weight rises. So this asserts the DIRECTION of the geometric
    /// effect, frame by frame, on the live posed skeleton — the bug class, not the value.
    ///
    /// HEADLESS-TICK TRAP (unity-conventions.md §Headless): a -batchmode frame has Time.deltaTime≈0, so an Animator
    /// on the engine clock never advances. Every tick here is an explicit <c>Animator.Update(dt)</c> with a positive
    /// delta. No <c>WaitForEndOfFrame</c> (it does not fire in batchmode — procedural-animation-verbs.md).
    ///
    /// No CastawayCharacter component is built (a bare rig emits the "modelPrefab not wired" error that fails an
    /// undeclared PlayMode test); the composition <c>CastawayArmPose.LateUpdate</c> performs is applied here
    /// directly onto the live clip pose, and the gate is read through the PURE
    /// <c>CastawayCharacter.MineSwingOwnsPoseFor</c> overload fed from the live layer-0 readings — exactly what the
    /// instance property does.
    ///
    /// EDITOR-ONLY (loads the rig + controller via AssetDatabase); Ignores in a player build.
    /// </summary>
    public class MineDeGripPlayModeTests
    {
        // Mirror CharacterAssetGen / MovementCameraScene — the editor asmdef is intentionally NOT referenced by this
        // all-platform PlayTests asmdef (same convention as RunLowerLocomotionGatePlayModeTests).
        private const string ControllerPath = "Assets/Art/Character/Castaway/CastawayAnimator.controller";
        private const string RiggedFbxPath = "Assets/Art/Character/Castaway/v4/castaway_v4_rigged.fbx";
        private static readonly Vector3 CarryRightEuler = new Vector3(-5f, -22f, 0f); // MovementCameraScene.CastawayV4RightArmEuler
        private static readonly Vector3 CarryLeftEuler = new Vector3(-5f, 22f, 0f);   // MovementCameraScene.CastawayV4LeftArmEuler
        private static readonly Vector3 DeGripEuler = new Vector3(-40f, 0f, 20f);     // MovementCameraScene.ArmMineDeGripEuler
        private const float Dt = 1f / 60f;

        private GameObject _root;
        private Animator _animator;
        private Transform _lArm, _rArm, _lHand, _rHand;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedFbxPath);
            Assert.IsNotNull(fbx, "the live hero rig must exist at " + RiggedFbxPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller, "the shipped controller must exist at " + ControllerPath);

            _root = Object.Instantiate(fbx);
            _root.name = "MineDeGripRig";
            _animator = _root.GetComponent<Animator>() ?? _root.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _lArm = Find("mixamorig:LeftArm"); _rArm = Find("mixamorig:RightArm");
            _lHand = Find("mixamorig:LeftHand"); _rHand = Find("mixamorig:RightHand");
            Assert.IsNotNull(_lArm); Assert.IsNotNull(_rArm);
            Assert.IsNotNull(_lHand); Assert.IsNotNull(_rHand);
#endif
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

#if UNITY_EDITOR
        private Transform Find(string bone)
        {
            foreach (var t in _root.GetComponentsInChildren<Transform>(true))
                if (t.name == bone) return t;
            return null;
        }

        private bool MineOwnsPose()
        {
            bool inTr = _animator.IsInTransition(0);
            int next = inTr ? _animator.GetNextAnimatorStateInfo(0).shortNameHash : 0;
            return CastawayCharacter.MineSwingOwnsPoseFor(
                _animator.GetCurrentAnimatorStateInfo(0).shortNameHash, inTr, next);
        }

        /// <summary>Hand separation in SHOULDER WIDTHS (scale-immune) with the given de-grip weight applied on top
        /// of the carry — exactly the composition CastawayArmPose.LateUpdate performs, from the SAME captured clip
        /// pose, so the two variants are measured on one identical animation frame.</summary>
        private float SeparationAt(float weight, Quaternion clipLeft, Quaternion clipRight)
        {
            _rArm.localRotation = clipRight * Quaternion.Euler(CarryRightEuler);
            _lArm.localRotation = clipLeft * Quaternion.Euler(CarryLeftEuler) * Quaternion.Euler(DeGripEuler * weight);
            float sw = (_rArm.position - _lArm.position).magnitude;
            return sw < 1e-5f ? float.NaN : (_lHand.position - _rHand.position).magnitude / sw;
        }

        private void TriggerMineSwing()
        {
            _animator.SetBool(CastawayCharacter.GroundedParam, true);
            _animator.SetBool(CastawayCharacter.MovingParam, false);
            for (int i = 0; i < 20; i++) _animator.Update(0.05f);
            _animator.SetInteger(CastawayCharacter.WeaponClassParam, CastawayCharacter.WeaponClassPickaxe);
            _animator.SetTrigger(CastawayCharacter.ChopParam);
        }
#endif

        [UnityTest]
        public IEnumerator MineTrigger_ReachesTheAttackPickaxeState_AndTheGateEngagesOnTheEntryCrossfade()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the rig + controller via AssetDatabase)");
            yield break;
#else
            yield return null;
            TriggerMineSwing();
            Assert.IsFalse(MineOwnsPose(), "before the trigger is consumed the de-grip must be released");

            _animator.Update(Dt);
            Assert.IsTrue(MineOwnsPose(),
                "the gate must engage on the FIRST ticked frame after the mine trigger — mid-crossfade layer 0 " +
                "still reports the FROM state as current, which is why the gate is transition-paired.");

            bool reached = false;
            for (int f = 0; f < 60 && !reached; f++)
            {
                _animator.Update(Dt);
                reached = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash ==
                          Animator.StringToHash(CastawayCharacter.AttackPickaxeState);
            }
            Assert.IsTrue(reached, "the shipped controller must actually reach AttackPickaxe from the mine trigger " +
                                   "(WeaponClass=" + CastawayCharacter.WeaponClassPickaxe + " + the Chop trigger)");
            yield return null;
#endif
        }

        // THE BUG-CLASS ASSERT: on the live posed skeleton the de-grip must OPEN the hands, never close them. A sign
        // error on the measured axis (+X instead of -X) makes this red — and nothing else in the suite would.
        [UnityTest]
        public IEnumerator DeGrip_IncreasesHandSeparation_AtEveryFrameOfTheMineSwing_NeverCloses()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the rig + controller via AssetDatabase)");
            yield break;
#else
            yield return null;
            TriggerMineSwing();

            float worstDelta = float.MaxValue;
            float minOff = float.MaxValue, minOn = float.MaxValue;
            int frames = 0, engaged = 0;

            for (int f = 0; f < 200; f++)
            {
                _animator.Update(Dt);
                if (!MineOwnsPose()) continue;
                engaged++;

                // Capture the CLIP pose the Animator just wrote, before any offset composes onto it.
                Quaternion clipLeft = _lArm.localRotation, clipRight = _rArm.localRotation;

                float off = SeparationAt(0f, clipLeft, clipRight);   // pre-86cay4282 behaviour
                float on = SeparationAt(1f, clipLeft, clipRight);    // the shipped de-grip, fully engaged
                if (float.IsNaN(off) || float.IsNaN(on)) continue;

                frames++;
                minOff = Mathf.Min(minOff, off);
                minOn = Mathf.Min(minOn, on);
                worstDelta = Mathf.Min(worstDelta, on - off);
            }

            Assert.Greater(engaged, 20, "the gate must stay engaged across the swing (engaged frames: " + engaged + ")");
            Assert.Greater(frames, 20, "need a meaningful sample of posed frames (got " + frames + ")");
            Assert.GreaterOrEqual(worstDelta, 0f,
                $"the de-grip must never CLOSE the hands on any frame (worst frame delta {worstDelta:F3} SW). A " +
                "negative value means the offset's sign is inverted — the exact trap the axis probe caught, where " +
                "the cheat-sheet's '+X spreads outward' actually pulls the hands together on this clip.");
            Assert.Greater(minOn, minOff * 1.10f,
                $"the TIGHTEST frame of the swing — the worst frame for the two-handed read — must open by a " +
                $"material margin: {minOff:F3} -> {minOn:F3} SW (need >10%).");
            yield return null;
#endif
        }

        // The regression guard for the Sponsor's locked pose: outside the mine swing nothing changes at all.
        [UnityTest]
        public IEnumerator OutsideTheMineSwing_TheGateIsReleased_SoTheCarryPoseIsUntouched()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the rig + controller via AssetDatabase)");
            yield break;
#else
            yield return null;
            _animator.SetBool(CastawayCharacter.GroundedParam, true);
            _animator.SetBool(CastawayCharacter.MovingParam, false);
            for (int i = 0; i < 40; i++) _animator.Update(0.05f);
            Assert.IsFalse(MineOwnsPose(), "settled idle must not engage the mine de-grip");

            // And the AXE swing — the sibling clip that measured one-handed — must stay out of the gate too.
            _animator.SetInteger(CastawayCharacter.WeaponClassParam, CastawayCharacter.WeaponClassAxe);
            _animator.SetTrigger(CastawayCharacter.ChopParam);
            bool everEngaged = false;
            for (int f = 0; f < 200; f++)
            {
                _animator.Update(Dt);
                if (MineOwnsPose()) { everEngaged = true; break; }
            }
            Assert.IsFalse(everEngaged,
                "the axe chop must NEVER engage the mine de-grip — it measured one-handed (hands 1.77-2.86 SW " +
                "apart, tool 6.8 deg off the hand line at the strike) and owns its own authored arm pose.");
            yield return null;
#endif
        }
    }
}
