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
    /// 86cackb3j AC2 — PlayMode state-routing proof for the locomotion + hit-react controller. Drives the Animator
    /// PARAMS and asserts the ACTIVE STATE NAME changes accordingly: Speed 0→walk→run blends through the locomotion
    /// states; the Hit trigger (+ HitRegion) activates the matching hit-react state; the Crouch bool engages the
    /// crouch lane; the Stunned bool holds then recovers via Getting Up.
    ///
    /// THE HEADLESS-TICK TRAP (unity-conventions.md §verification-lessons): a normal PlayMode frame has
    /// Time.deltaTime≈0 in -batchmode, so an Animator driven off the engine clock NEVER advances its state machine
    /// — a documented false-green. The fix here is to call <c>Animator.Update(dt)</c> with an EXPLICIT positive delta
    /// (it advances the state machine independently of Time.deltaTime), so the transitions actually evaluate + settle.
    ///
    /// EDITOR-ONLY: this loads the production controller ASSET via AssetDatabase, so it runs only on the editor
    /// PlayMode path (CI runs -testPlatform PlayMode in -batchmode editor, where AssetDatabase is live). The
    /// FarHorizon.PlayTests asmdef compiles for the player too, so the editor APIs are #if UNITY_EDITOR-guarded
    /// (the test Ignores in a player). This is the runtime companion to the EditMode
    /// CastawayLocomotionHitReactControllerTests (which pins the static wiring and IS the required-gate authority).
    /// </summary>
    public class CastawayLocomotionHitReactPlayModeTests
    {
        // Mirror CharacterAssetGen (the editor asmdef is intentionally NOT referenced by this all-platform
        // PlayTests asmdef; a ParamNamesMatch EditMode test pins the param-name half of the mirror). The controller
        // path is the stable committed-asset path; the blend speeds mirror WalkBlendSpeed/RunBlendSpeed.
        private const string ControllerPath = "Assets/Art/Character/Castaway/CastawayAnimator.controller";
        private const float WalkBlendSpeed = 5.5f;
        private const float RunBlendSpeed = 9.5f;

        private GameObject _go;
        private Animator _animator;

#if UNITY_EDITOR
        // The state names the controller-build authors (86cackb3j) — resolved via GetCurrentAnimatorStateInfo.IsName
        // (StateInfo exposes a name HASH, not the string), no editor enumeration needed at read time.
        private static readonly string[] StateNames =
        {
            "Idle", "Locomotion", "JumpIdle", "JumpRunning", "Attack",
            "CrouchIdle", "CrouchWalk", "Stunned", "GettingUp", "PickingUp",
            "HitToBody", "HeadHit", "BigStomachHit", "StomachHit", "RibHit",
        };
#endif

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller, "the production CastawayAnimator controller must exist at " + ControllerPath);

            _go = new GameObject("AnimRig");
            _animator = _go.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
#endif
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

#if UNITY_EDITOR
        // Advance the state machine by an explicit positive delta N times (headless-safe — does NOT rely on
        // Time.deltaTime, which is ≈0 in -batchmode). Lets transitions evaluate + settle.
        private void Tick(int frames = 30, float dt = 0.05f)
        {
            for (int i = 0; i < frames; i++) _animator.Update(dt);
        }

        private string ActiveStateName()
        {
            var info = _animator.GetCurrentAnimatorStateInfo(0);
            foreach (var n in StateNames) if (info.IsName(n)) return n;
            return "<unresolved>";
        }
#endif

        // AC2 — Speed 0 → walk → run drives the active state from Idle into the Locomotion blend state (the
        // Walk→Run blend happens INSIDE the blend tree on the Speed param), then back to Idle on release.
        [UnityTest]
        public IEnumerator SpeedParam_DrivesIdleToLocomotion_AndBlendsWalkToRun()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only (loads the controller asset via AssetDatabase)");
            yield break;
#else
            yield return null;
            _animator.Update(0.05f);

            _animator.SetBool(CastawayCharacter.MovingParam, false);
            _animator.SetFloat(CastawayCharacter.SpeedParam, 0f);
            Tick();
            Assert.AreEqual("Idle", ActiveStateName(), "at rest (Moving=false, Speed=0) the active state must be Idle");

            _animator.SetBool(CastawayCharacter.MovingParam, true);
            _animator.SetFloat(CastawayCharacter.SpeedParam, WalkBlendSpeed);
            Tick();
            Assert.AreEqual("Locomotion", ActiveStateName(),
                "with Moving=true the active state must switch to the Locomotion blend (Idle→Locomotion on Moving)");

            _animator.SetFloat(CastawayCharacter.SpeedParam, RunBlendSpeed);
            Tick();
            Assert.AreEqual("Locomotion", ActiveStateName(),
                "at run speed the active state stays Locomotion (the Walk→Run blend happens INSIDE the blend tree)");
            Assert.AreEqual(RunBlendSpeed, _animator.GetFloat(CastawayCharacter.SpeedParam), 1e-3f,
                "the Speed param the blend tree reads must equal the run speed we fed it");

            _animator.SetBool(CastawayCharacter.MovingParam, false);
            _animator.SetFloat(CastawayCharacter.SpeedParam, 0f);
            Tick();
            Assert.AreEqual("Idle", ActiveStateName(), "releasing movement must return the active state to Idle");
#endif
        }

        // AC2 — firing the Hit trigger with HitRegion set activates the MATCHING hit-react state (region routing),
        // then it returns to Idle; a different region routes to a different state (the int discriminates).
        [UnityTest]
        public IEnumerator HitTrigger_WithRegion_ActivatesMatchingHitReact_ThenReturns()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only");
            yield break;
#else
            yield return null;
            _animator.Update(0.05f);
            _animator.SetBool(CastawayCharacter.MovingParam, false);
            Tick();
            Assert.AreEqual("Idle", ActiveStateName(), "must start in Idle");

            _animator.SetInteger(CastawayCharacter.HitRegionParam, CastawayCharacter.HitRegionHead);
            _animator.SetTrigger(CastawayCharacter.HitParam);
            for (int i = 0; i < 4; i++) _animator.Update(0.05f);
            Assert.AreEqual("HeadHit", ActiveStateName(),
                "Hit with HitRegion=Head must activate the HeadHit state (region routing)");

            Tick(40);
            Assert.AreEqual("Idle", ActiveStateName(), "the HeadHit one-shot must return to Idle when it ends");

            _animator.SetInteger(CastawayCharacter.HitRegionParam, CastawayCharacter.HitRegionRib);
            _animator.SetTrigger(CastawayCharacter.HitParam);
            for (int i = 0; i < 4; i++) _animator.Update(0.05f);
            Assert.AreEqual("RibHit", ActiveStateName(),
                "Hit with HitRegion=Rib must activate the RibHit state (NOT HeadHit — the region int discriminates)");
#endif
        }

        // AC2 — the Crouch bool engages the crouch lane (CrouchIdle when !Moving), and releasing it returns to Idle.
        [UnityTest]
        public IEnumerator CrouchBool_EngagesCrouchLane_ThenReleases()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only");
            yield break;
#else
            yield return null;
            _animator.Update(0.05f);
            _animator.SetBool(CastawayCharacter.MovingParam, false);
            Tick();
            Assert.AreEqual("Idle", ActiveStateName(), "must start in Idle");

            _animator.SetBool(CastawayCharacter.CrouchParam, true);
            Tick();
            Assert.AreEqual("CrouchIdle", ActiveStateName(), "Crouch=true (Moving=false) must engage the CrouchIdle lane");

            _animator.SetBool(CastawayCharacter.CrouchParam, false);
            Tick();
            Assert.AreEqual("Idle", ActiveStateName(), "releasing Crouch (Moving=false) must return to Idle");
#endif
        }

        // AC2 — the Stunned bool holds the Stunned state (a loop) and, on release, plays Getting Up then returns to Idle.
        [UnityTest]
        public IEnumerator StunnedBool_HoldsThenRecoversViaGettingUp()
        {
#if !UNITY_EDITOR
            Assert.Ignore("editor-only");
            yield break;
#else
            yield return null;
            _animator.Update(0.05f);
            _animator.SetBool(CastawayCharacter.MovingParam, false);
            Tick();

            _animator.SetBool(CastawayCharacter.StunnedParam, true);
            Tick();
            Assert.AreEqual("Stunned", ActiveStateName(), "Stunned=true must activate the Stunned hold state");

            Tick(40);
            Assert.AreEqual("Stunned", ActiveStateName(), "the Stunned hold must persist while the bool stays true (it loops)");

            _animator.SetBool(CastawayCharacter.StunnedParam, false);
            for (int i = 0; i < 4; i++) _animator.Update(0.05f);
            Assert.AreEqual("GettingUp", ActiveStateName(), "releasing Stunned must play the GettingUp recovery");

            Tick(60);
            Assert.AreEqual("Idle", ActiveStateName(), "after GettingUp the character must return to Idle");
#endif
        }
    }
}
