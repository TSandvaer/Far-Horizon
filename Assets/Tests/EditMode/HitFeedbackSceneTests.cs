using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon.Combat;
using FarHorizon.Juice;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Scene-presence guards for enemy hit feedback (ticket 86caxjwb3 AC1/AC4/AC7). The failure class these
    /// exist for is the project's named one: a MonoBehaviour can be committed, compile clean and pass every
    /// script test while the SCENE simply never carries it — the feature then ships silently INERT (the
    /// CaptureGate/#6 precedent). Everything below is asserted against the SHIPPED, SERIALIZED Boot.unity.
    ///
    /// Three of these guard defects that leave every other check green:
    ///  • <b>stopAction != Callback</b> ([DFC-4c]) — OnParticleSystemStopped is then never delivered, the pool
    ///    never recovers an instance, and the "pooled" claim is silently false while emitting keeps working.
    ///  • <b>a null/empty chunk mesh</b> ([DFC-5]) — the puff renders nothing. It cannot be built at runtime:
    ///    LowPolyMeshes is in the Editor-only asmdef, so the mesh MUST be baked at scene-author time and
    ///    serialized here. A test that only asserts "the ParticleSystem exists" misses this entirely.
    ///  • <b>a template left ACTIVE</b> — it would play on scene load and the pool would clone a live system.
    /// </summary>
    public class HitFeedbackSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        private static UnityEngine.SceneManagement.Scene OpenBoot()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            return scene;
        }

        private static GameObject FindRoot(string name)
        {
            foreach (var root in OpenBoot().GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        [Test]
        public void BootScene_BothEnemies_CarryTheSharedDriver_Wired()
        {
            var scene = OpenBoot();
            GameObject boar = null, snake = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Boar") boar = root;
                if (root.name == "Snake") snake = root;
            }
            BootstrapPrecondition.Require(boar, "the Boar root");
            BootstrapPrecondition.Require(snake, "the Snake root");

            foreach (var go in new[] { boar, snake })
            {
                var fb = go.GetComponent<EnemyHitFeedback>();
                BootstrapPrecondition.Require(fb, "EnemyHitFeedback on " + go.name +
                                                  " (MovementCameraScene.BuildHitFeedback)");
                Assert.IsNotNull(fb.puff, go.name + ": the pooled dust-puff emitter must be wired (AC4)");
                Assert.IsNotNull(fb.contactBias, go.name + ": contactBias wired (the puff reads AT the contact point)");
                Assert.IsNotNull(fb.deathHandler, go.name + ": deathHandler wired — the LIVE per-tier stagger surface");
                Assert.IsTrue(fb.feedbackEnabled, go.name + ": the master switch ships ON (AC5 default)");
                Assert.IsNotNull(go.GetComponent<Health>(), go.name + ": the shared Health seam the driver listens to");
            }

            // The ONE emitter is shared — a second pool implementation is exactly what AC4's precedent forbids.
            Assert.AreSame(boar.GetComponent<EnemyHitFeedback>().puff,
                           snake.GetComponent<EnemyHitFeedback>().puff,
                           "both creatures share ONE pooled emitter (AC4: keep the pool general, never fork it)");
        }

        [Test]
        public void BootScene_CarriesTheProjectsFirstPool_AndItsAuthoredTemplate()
        {
            var root = FindRoot(MovementCameraScene.HitFeedbackRootName);
            BootstrapPrecondition.Require(root, "the " + MovementCameraScene.HitFeedbackRootName + " root");

            var emitter = root.GetComponent<PooledBurstEmitter>();
            BootstrapPrecondition.Require(emitter, "PooledBurstEmitter (the project's FIRST object pool)");
            Assert.IsNotNull(emitter.template, "the pool's authored template must be serialized");
            Assert.AreEqual(MovementCameraScene.DustPuffTemplateName, emitter.template.name,
                "the template is the authored dust puff");
            Assert.IsFalse(emitter.template.gameObject.activeSelf,
                "the template must be INACTIVE — an active one plays on scene load and the pool would clone a " +
                "running system");
            Assert.Greater(emitter.maxPoolSize, 0, "the pool must have a retained-instance ceiling");
            Assert.LessOrEqual(emitter.maxParticlesPerBurst, 12,
                "brief §1.2 caps a burst at 12 particles — the emitter's own clamp must honour it");
        }

        [Test]
        public void BootScene_PuffTemplate_HasTheCallbackStopAction_OrThePoolLeaksSilently()
        {
            // [DFC-4c]. This is the single most easily-lost line in the whole feature: without it the release
            // callback never fires, the pool never recycles, and EVERY other assertion in this file still passes.
            var root = FindRoot(MovementCameraScene.HitFeedbackRootName);
            BootstrapPrecondition.Require(root, "the hit-feedback root");
            var emitter = root.GetComponent<PooledBurstEmitter>();
            BootstrapPrecondition.Require(emitter, "PooledBurstEmitter");

            Assert.AreEqual(ParticleSystemStopAction.Callback, emitter.template.main.stopAction,
                "[DFC-4c] the template's main.stopAction MUST be Callback — it is the ONLY thing that delivers " +
                "OnParticleSystemStopped, and therefore the only thing that returns an instance to the pool");
            Assert.IsTrue(emitter.TemplateStopActionIsCallback, "the emitter's own read of the same contract agrees");
            Assert.IsFalse(emitter.template.main.playOnAwake, "the template must not play on awake");
            Assert.IsFalse(emitter.template.main.loop, "a burst is one-shot, never looping");
            Assert.AreEqual(0f, emitter.template.emission.rateOverTime.constant, 1e-6f,
                "bursts ONLY — never an ambient trickle (game-juice.md §1: bursts at reward/impact moments)");
        }

        [Test]
        public void BootScene_PuffTemplate_CarriesTheBakedChunkMesh_AndASeparateParticleMaterial()
        {
            var root = FindRoot(MovementCameraScene.HitFeedbackRootName);
            BootstrapPrecondition.Require(root, "the hit-feedback root");
            var emitter = root.GetComponent<PooledBurstEmitter>();
            var psr = emitter.template.GetComponent<ParticleSystemRenderer>();
            BootstrapPrecondition.Require(psr, "the template's ParticleSystemRenderer");

            // [DFC-5] route (i): baked at scene-author time and SERIALIZED. A runtime path cannot call
            // LowPolyMeshes at all (Editor-only asmdef), so a null here means the chunk was never baked.
            Assert.AreEqual(ParticleSystemRenderMode.Mesh, psr.renderMode,
                "chunky faceted debris, not a billboard sprite (lowpoly-quality: the world is faceted)");
            Assert.IsNotNull(psr.mesh, "[DFC-5] the chunk mesh must be BAKED editor-time and serialized");
            Assert.Greater(psr.mesh.vertexCount, 0, "the serialized chunk mesh must carry geometry");
            Assert.Less(psr.mesh.bounds.extents.magnitude, 0.5f,
                "it is DEBRIS, not a boulder — a chunk that reads as a rock would look like the world is " +
                "shedding scenery, not dust");

            // ⚠ THE INVISIBLE-PUFF GUARD, and it is here because the first shipped build HAD this defect.
            // In MESH render mode `startSize` MULTIPLIES the mesh; it is not an absolute world size the way it
            // is for a billboard. Authored at a sprite-shaped 0.05-0.11 it produced 4-10 MILLIMETRE debris —
            // well under one pixel at the gameplay framing. Every gate was GREEN on it (the burst really did
            // fire, `puffed=True`), and only eyeballing the frame caught it. So the guard asserts the RENDERED
            // world size, not that a ParticleSystem exists.
            // The bound, with its plane stated (game-juice.md §2b): the chunk's extent is near-isotropic so no
            // projection factor applies; at the default framing (pitch 55° / dist 14u / FOV 45° / 720p) the
            // frame-plane scale is 62.080 px/m, so a 0.04u floor is ~2.5 px — the point below which a moving
            // cluster of 7 stops being a puff and becomes noise.
            float minStart = emitter.template.main.startSize.constantMin;
            float maxStart = emitter.template.main.startSize.constantMax;
            float chunkDiameter = psr.mesh.bounds.extents.magnitude * 2f;
            float smallestRendered = chunkDiameter * Mathf.Min(minStart, maxStart);
            float largestRendered = chunkDiameter * Mathf.Max(minStart, maxStart);
            Assert.Greater(smallestRendered, 0.04f,
                "the SMALLEST rendered chunk must be visible at gameplay framing — measured " +
                smallestRendered.ToString("0.0000") + "u (~" + (smallestRendered * 62.080f).ToString("0.0") +
                " px at pitch 55°/dist 14u/FOV 45°/720p). Remember startSize MULTIPLIES the mesh in Mesh mode.");
            Assert.Less(largestRendered, 0.5f,
                "…and the LARGEST must stay debris-scale on a 1.1 m boar — measured " +
                largestRendered.ToString("0.0000") + "u. Over-sized chunks read as the world shedding scenery.");

            // [DFC-4a] a SEPARATE material on the URP PARTICLE shader. `Unlit/Particle` (game-juice.md §3) is a
            // built-in-RP name that does not exist in URP — using it yields MAGENTA.
            var mat = psr.sharedMaterial;
            Assert.IsNotNull(mat, "the template must carry its own particle material");
            Assert.IsNotNull(mat.shader, "…whose shader resolves");
            StringAssert.DoesNotContain("Unlit/Particle", mat.shader.name,
                "[DFC-4a] `Unlit/Particle` is a BUILT-IN-RP shader name that does not exist in URP -> magenta");
            StringAssert.Contains("Universal Render Pipeline", mat.shader.name,
                "[DFC-4a] the particle material must ride a URP particle shader");
            Assert.AreNotEqual("FarHorizon/LowPolyVertexColor", mat.shader.name,
                "brief §1.2: the puff must NOT ride the world palette material — that is the ~1-draw-call batch");
        }

        [Test]
        public void BootScene_PuffColour_IsDustBrown_SubOne_NeverRed()
        {
            // brief §2.5 🔒. Enforced on the SHIPPED drivers, not on the C# defaults, because the scene is what
            // the Sponsor actually sees and a scene-authored override would silently win.
            var scene = OpenBoot();
            int checkedDrivers = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var fb in root.GetComponentsInChildren<EnemyHitFeedback>(true))
                {
                    Color c = fb.puffColor;
                    Assert.Less(c.r, 1f, "puff R sub-1.0"); Assert.Less(c.g, 1f, "puff G sub-1.0");
                    Assert.Less(c.b, 1f, "puff B sub-1.0");
                    Assert.Greater(c.r, c.g, "dust-BROWN: a warm ramp R > G");
                    Assert.Greater(c.g, c.b, "dust-BROWN: a warm ramp G > B");
                    Assert.Less(c.r - c.g, 0.35f,
                        "an R that runs far ahead of G is the direction that reads as BLOOD — forbidden, not " +
                        "deferred (brief §2.5, game-juice.md §0 kid-safe tone)");
                    Assert.LessOrEqual(fb.puffCount, 12, "brief §1.2: <= 12 particles per burst");
                    Assert.LessOrEqual(fb.deathPuffCount, 12, "…including the death puff");
                    Assert.Greater(fb.deathPuffCount, 0,
                        "AC4 🔒: the puff ALSO fires on death — without it the soak's death moment has NO " +
                        "feedback at all and 'is it nearly down?' is only half-testable");
                    checkedDrivers++;
                }
            }
            Assert.AreEqual(2, checkedDrivers,
                "both shipped creatures must carry a driver (a staleness assert: if this ever reads 0 the scan " +
                "found nothing and every assertion above was vacuous)");
        }

        [Test]
        public void BootScene_CarriesHitFeedbackVerifyCapture_Serialized()
        {
            var scene = OpenBoot();
            FarHorizon.HitFeedbackVerifyCapture cap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cap = root.GetComponentInChildren<FarHorizon.HitFeedbackVerifyCapture>(true);
                if (cap != null) break;
            }
            BootstrapPrecondition.Require(cap, "HitFeedbackVerifyCapture on the Boot object");
            Assert.IsNotNull(cap.player, "HitFeedbackVerifyCapture.player wired (it walks the REAL player)");
        }
    }
}
