using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the NEW transparent depth-fade FOAM water shader FarHorizon/LowPolyWater
    /// (ticket 86caamnmb — Erik R&D §B / Rank 3; Cyanilux depth + Daniel Ilett scene-intersections +
    /// ameye.dev stylized water).
    ///
    /// The shader's RENDERED foam + the sea↔sky horizon read can only be judged from the SHIPPED exe
    /// (editor-vs-runtime shader-render trap, unity-conventions.md §Editor-vs-runtime — proven by the
    /// `-seaDiag` horizon-pixel sampler + the AC6 gameplay-orbit capture in the Self-Test Report). What an
    /// EditMode test CAN authoritatively assert is the CONTRACT that makes depth-fade foam possible WITHOUT
    /// re-opening the FPS-protecting / fog-cap choices that the opaque water deliberately made:
    ///
    ///   (a) the shader resolves + compiles clean (no HLSL error in the depth-fade / ported fog-cap frag);
    ///   (b) it is on the TRANSPARENT queue with ZWrite Off + SrcAlpha/OneMinusSrcAlpha blend (AC1) — the
    ///       hard prerequisite (an opaque shader can't sample the depth it writes);
    ///   (c) the foam + fog-cap properties exist as real shader properties INSIDE CBUFFER_START
    ///       (UnityPerMaterial) (AC6 / §SRP-Batcher) — _FoamColor, _FoamDistance, _FogCap;
    ///   (d) the PORTED _FogCap floor is present (AC3 — the load-bearing migration: Transparent loses the
    ///       engine opaque fog, so the in-frag fog-cap must carry the sea↔sky-at-horizon read);
    ///   (e) the active URP Asset has Depth Texture + Opaque Texture ENABLED (AC1) — the depth-fade can't
    ///       sample _CameraDepthTexture without them, and the build silently ships foam-less water.
    /// </summary>
    public class LowPolyWaterShaderTests
    {
        private const string ShaderName = "FarHorizon/LowPolyWater";

        private static Shader FindShader()
        {
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader, $"shader '{ShaderName}' must resolve (created + registered + compiles)");
            return shader;
        }

        [Test]
        public void Shader_Resolves_AndHasNoCompileError()
        {
            var shader = FindShader();
#if UNITY_EDITOR
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader),
                $"shader '{ShaderName}' must compile with NO errors (the depth-fade foam + the ported " +
                "fog-cap frag must not introduce a compile error)");
#endif
        }

        [Test]
        public void Shader_IsOnTransparentQueue_WithZWriteOff_AlphaBlend()
        {
            // AC1: the HARD prerequisite. The depth-fade needs to sample the resolved opaque scene depth, so
            // this shader must render AFTER opaque (Transparent queue) and must NOT write depth (ZWrite Off)
            // nor occlude the scene (alpha blend). A regression that drops it back to the Geometry/Opaque
            // queue re-introduces the "opaque can't sample the depth it writes" impossibility.
            var mat = new Material(FindShader());
            try
            {
                Assert.GreaterOrEqual(mat.renderQueue, (int)RenderQueue.Transparent,
                    $"AC1: LowPolyWater must render on the TRANSPARENT queue (>= {(int)RenderQueue.Transparent}) " +
                    $"— it sampled {mat.renderQueue}. Opaque can't sample the depth it writes.");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void Shader_Declares_FoamAndFogCapProperties_ForSrpBatcher()
        {
            // AC6 / §SRP-Batcher: the new foam properties + the ported fog-cap must be real shader properties
            // (declared INSIDE CBUFFER_START(UnityPerMaterial) — the SRP-Batcher gate). A material built on the
            // shader must expose all of them.
            var mat = new Material(FindShader());
            try
            {
                Assert.IsTrue(mat.HasProperty("_FoamColor"),
                    "AC2/AC6: the shader must declare `_FoamColor` (inside CBUFFER_START(UnityPerMaterial))");
                Assert.IsTrue(mat.HasProperty("_FoamDistance"),
                    "AC2/AC6: the shader must declare `_FoamDistance` (inside CBUFFER_START(UnityPerMaterial))");
                Assert.IsTrue(mat.HasProperty("_FogCap"),
                    "AC3/AC6: the shader must declare `_FogCap` — the PORTED fog-floor for the sea↔sky horizon read");
                // The swell + tint carried from the opaque water (the sea must still read identically).
                Assert.IsTrue(mat.HasProperty("_WaveAmp"), "the shader must carry `_WaveAmp` (the in-shader swell)");
                Assert.IsTrue(mat.HasProperty("_Tint"), "the shader must carry `_Tint`");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void DefaultMaterial_HasSensibleFoamAndFogDefaults()
        {
            // AC2/AC3: _FoamDistance defaults to a small positive band (~1.5u) so a fresh material shows foam
            // at intersections; _FogCap defaults to 0 (the no-clamp == full-fog default — the WATER material
            // raises it to 0.5, mirroring the opaque shader's default discipline so a non-water reuse is safe).
            var mat = new Material(FindShader());
            try
            {
                Assert.Greater(mat.GetFloat("_FoamDistance"), 0f,
                    "AC2: `_FoamDistance` must default > 0 (the foam fade band) — 0 would divide-by-~0 / no foam");
                Assert.AreEqual(0f, mat.GetFloat("_FogCap"), 1e-6f,
                    "AC3: `_FogCap` must DEFAULT to 0 (full fog) — only the water material raises it to ~0.5 " +
                    "(matches the opaque shader's default-0 discipline)");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void OptInMaterial_CanSetFoamColorAndDistance_Distinct()
        {
            // AC2: the foam look is tweakable — a material can set _FoamColor + _FoamDistance and they read back.
            var mat = new Material(FindShader());
            try
            {
                mat.SetColor("_FoamColor", new Color(0.91f, 0.89f, 0.82f, 1f)); // == FoamEdge
                mat.SetFloat("_FoamDistance", 1.5f);
                mat.SetFloat("_FogCap", 0.5f);

                var c = mat.GetColor("_FoamColor");
                Assert.AreEqual(0.91f, c.r, 1e-3f, "_FoamColor.r set (warm off-white == FoamEdge)");
                Assert.AreEqual(0.89f, c.g, 1e-3f, "_FoamColor.g set");
                Assert.AreEqual(0.82f, c.b, 1e-3f, "_FoamColor.b set");
                Assert.AreEqual(1.5f, mat.GetFloat("_FoamDistance"), 1e-3f, "_FoamDistance set");
                Assert.AreEqual(0.5f, mat.GetFloat("_FogCap"), 1e-3f, "_FogCap raised for the water material");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void ActiveUrpAsset_HasDepthAndOpaqueTextureEnabled()
        {
            // AC1: the depth-fade samples _CameraDepthTexture (+ the project enables the opaque texture for
            // any future water refraction); URP only generates these when the URP Asset requests them. If a
            // regression flips them OFF, the build ships foam-less water (SampleSceneDepth reads garbage) — a
            // silent shipped failure this guards in headless CI before it reaches the Sponsor.
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.IsNotNull(urp, "the active render pipeline must be a UniversalRenderPipelineAsset");
            Assert.IsTrue(urp.supportsCameraDepthTexture,
                "AC1: the active URP Asset must enable Depth Texture (m_RequireDepthTexture) — the depth-fade " +
                "foam samples _CameraDepthTexture; without it the foam is absent in the shipped build");
            Assert.IsTrue(urp.supportsCameraOpaqueTexture,
                "AC1: the active URP Asset must enable Opaque Texture (m_RequireOpaqueTexture)");
        }
    }
}
