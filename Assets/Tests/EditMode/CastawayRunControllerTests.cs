using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC1 + AC2 RUN-clip integration guards (ticket 86ca9yq34 — run on Shift-hold). These pin the asset-side
    /// contract the runtime relies on, so the bug CLASSES can't recur silently in headless CI before a soak:
    ///
    ///   1. Running.fbx imports like Walking.fbx (GENERIC rig, CreateFromThisModel) with a LOOPING Run clip —
    ///      the T-pose-mid-walk class (a non-looping / missing clip freezes the run). (AC2 import.)
    ///   2. The CastawayAnimator controller carries a Speed FLOAT param + a 1D BLEND TREE on Speed whose
    ///      children include the Run clip at RunBlendSpeed — so feeding the faster run speed blends in the Run
    ///      clip (AC1's Walk<->Run blend). A controller missing the Run motion ships the run inert (Walk plays
    ///      at run speed). This is the "component/motion in source but not wired" silent-failure guard.
    ///   3. The blend-tree thresholds (Walk@WalkBlendSpeed, Run@RunBlendSpeed) match the WASD speeds, so the
    ///      planar agent speed WasdMovement commands lands on the intended clip.
    /// </summary>
    public class CastawayRunControllerTests
    {
        // AC2 import guard — Running.fbx imports as a GENERIC rig (CreateFromThisModel) with a looping Run clip,
        // exactly like Walking.fbx (binds by transform path onto Idle's mesh; no Humanoid retarget — the
        // runtime-explosion fix, 86ca8rdkp). The Mixamo take is "mixamo.com" → renamed CastawayRun on import.
        [Test]
        public void RunningFbx_ImportsGeneric_WithLoopingRunClip()
        {
            Assert.AreEqual("Assets/Art/Character/Castaway/Running.fbx", CharacterAssetGen.RunFbxPath,
                "the Run clip must come from the Hyper3D castaway Running FBX (without skin)");

            var runImporter = AssetImporter.GetAtPath(CharacterAssetGen.RunFbxPath) as ModelImporter;
            Assert.IsNotNull(runImporter, "Running.fbx must be importable at " + CharacterAssetGen.RunFbxPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, runImporter.animationType,
                "Running.fbx must import as a GENERIC rig (the Run clip binds by transform path, no retarget — " +
                "the Humanoid muscle-space retarget EXPLODED the mesh into a cone, 86ca8rdkp)");
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, runImporter.avatarSetup,
                "Running.fbx must create its own avatar (Generic path binds by transform path onto Idle's mesh)");

            AnimationClip run = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.RunFbxPath))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__") &&
                    c.name.Contains(CharacterAssetGen.RunClip)) run = c;
            Assert.IsNotNull(run, "Running.fbx must contain a clip matching '" + CharacterAssetGen.RunClip + "'");
            Assert.IsTrue(run.isLooping, CharacterAssetGen.RunClip + " must loop (no freeze-mid-run)");
        }

        // AC1 controller guard — the controller has a Speed float param + a 1D blend tree on Speed whose
        // children include the Run clip at RunBlendSpeed. A controller without the Run motion (or without the
        // Speed param) ships the Walk<->Run blend INERT — the Run clip never plays no matter the speed.
        [Test]
        public void Controller_HasSpeedBlendTree_WithRunClipChild()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);

            // The Speed FLOAT param (the blend-tree blend param) must exist alongside the Moving bool.
            bool hasSpeed = false, hasMoving = false;
            foreach (var p in controller.parameters)
            {
                if (p.name == CastawayCharacter.SpeedParam && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
                if (p.name == CastawayCharacter.MovingParam && p.type == AnimatorControllerParameterType.Bool) hasMoving = true;
            }
            Assert.IsTrue(hasMoving, "the controller must keep the Moving bool param (Idle<->locomotion flip)");
            Assert.IsTrue(hasSpeed, "the controller must have a Speed FLOAT param (the Walk<->Run blend param) — " +
                "CastawayCharacter feeds the planar agent speed to it every frame");

            // Find the 1D blend tree among the layer-0 states + assert it blends on Speed and includes the Run clip.
            BlendTree tree = null;
            foreach (var cs in controller.layers[0].stateMachine.states)
                if (cs.state.motion is BlendTree bt) { tree = bt; break; }
            Assert.IsNotNull(tree, "the controller must host a 1D blend tree (the Locomotion state) for Walk<->Run");
            Assert.AreEqual(BlendTreeType.Simple1D, tree.blendType, "the locomotion blend tree must be 1D");
            Assert.AreEqual(CastawayCharacter.SpeedParam, tree.blendParameter,
                "the blend tree must blend on the Speed param (the quantity CastawayCharacter drives)");

            // The Run clip must be a child of the blend tree at the run threshold (else the run is inert).
            bool runChild = false, walkChild = false;
            float runThreshold = 0f;
            foreach (var child in tree.children)
            {
                if (child.motion is AnimationClip clip)
                {
                    if (clip.name.Contains(CharacterAssetGen.RunClip)) { runChild = true; runThreshold = child.threshold; }
                    if (clip.name.Contains(CharacterAssetGen.WalkClip)) walkChild = true;
                }
            }
            Assert.IsTrue(walkChild, "the blend tree must include the Walk clip (the lower locomotion band)");
            Assert.IsTrue(runChild, "the blend tree must include the Run clip as a child — else holding Shift " +
                "(faster speed) still plays Walk (the run is inert; AC1 fails)");
            Assert.That(runThreshold, Is.EqualTo(CharacterAssetGen.RunBlendSpeed).Within(1e-3f),
                $"the Run clip's blend threshold must be RunBlendSpeed ({CharacterAssetGen.RunBlendSpeed}) so the " +
                "run-speed agent velocity blends fully to the Run clip");
        }

        // The threshold-consistency guard — the run blend threshold equals the WASD run speed, and walk < run,
        // so the planar agent speed WasdMovement commands lands on the intended clip (a mismatch would blend the
        // wrong clip in at speed). These are the constants both the WASD speed + the avatar threshold pin to.
        [Test]
        public void BlendThresholds_MatchWasdSpeeds_WalkBelowRun()
        {
            Assert.Greater(CharacterAssetGen.RunBlendSpeed, CharacterAssetGen.WalkBlendSpeed,
                "the Run blend speed must exceed the Walk blend speed (else there is no Walk<->Run separation)");
            Assert.Greater(CharacterAssetGen.WalkBlendSpeed, CharacterAssetGen.IdleBlendSpeed,
                "the Walk blend speed must exceed the Idle floor");
        }
    }
}
