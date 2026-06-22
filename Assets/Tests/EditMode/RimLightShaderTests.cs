using NUnit.Framework;
using UnityEngine;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the Fresnel/rim-light additive term on FarHorizon/LowPolyVertexColor
    /// (ticket 86caamnnj — Erik R&D §C / Rank 4; Daniel Ilett Toon Shaders Pro + Minions Art rim idiom).
    ///
    /// Unlike the _FlatShading toggle (#106), the rim term has NO keyword — it is a plain additive
    /// `finalCol += _RimColor.rgb * rim * _RimIntensity`. So the OFF path is not a stripped shader
    /// variant; it is the pure-arithmetic identity `+= rgb * rim * 0 == finalCol`. That removes the
    /// shader_feature-strip false-green class entirely (the #106 lesson) — there is no ON-variant to
    /// strip. What an EditMode test CAN authoritatively assert is the property CONTRACT:
    ///
    ///   (a) the shader compiles with the new term (no HLSL error);
    ///   (b) the three properties (_RimColor / _RimPower / _RimIntensity) exist as real shader
    ///       properties (declared INSIDE CBUFFER_START(UnityPerMaterial) per §SRP-Batcher — verified by
    ///       HasProperty + the §SRP-Batcher doc audit);
    ///   (c) _RimIntensity DEFAULTS to 0 → the no-op path (AC2/AC4): terrain/canopy/water/prop materials
    ///       that never touch it add an exactly-zero term, so they render byte-identical to before;
    ///   (d) an opt-in prop can DIAL _RimIntensity > 0 (AC3) — the property is settable and distinct
    ///       from the default, so the additive highlight is reachable.
    ///
    /// The OFF-vs-dialed RENDERED look (no-regression WorldLook + the visible silhouette highlight) is
    /// proven by the shipped-build A/B captures in the PR Self-Test Report (editor capture is
    /// necessary-NOT-sufficient — unity-conventions.md §Editor-vs-runtime). This test guards that the
    /// wiring + the default-0 no-op can't silently break.
    /// </summary>
    public class RimLightShaderTests
    {
        private const string ShaderName = "FarHorizon/LowPolyVertexColor";

        private static Shader FindShader()
        {
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader, $"shader '{ShaderName}' must resolve (registered + compiles)");
            return shader;
        }

        [Test]
        public void Shader_Resolves_AndHasNoCompileError()
        {
            var shader = FindShader();
#if UNITY_EDITOR
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader),
                $"shader '{ShaderName}' must compile with NO errors (the new Fresnel/rim term must not " +
                "introduce a compile error)");
#endif
        }

        [Test]
        public void Shader_Declares_RimProperties_ForSrpBatcher()
        {
            // AC1: the three new rim properties must be real shader properties (declared inside
            // CBUFFER_START so they are SRP-Batcher-compliant — the §SRP-Batcher gate). A material built
            // on the shader must expose all three.
            var mat = new Material(FindShader());
            try
            {
                Assert.IsTrue(mat.HasProperty("_RimColor"),
                    "AC1: the shader must declare a `_RimColor` property (inside CBUFFER_START(UnityPerMaterial))");
                Assert.IsTrue(mat.HasProperty("_RimPower"),
                    "AC1: the shader must declare a `_RimPower` property (inside CBUFFER_START(UnityPerMaterial))");
                Assert.IsTrue(mat.HasProperty("_RimIntensity"),
                    "AC1: the shader must declare a `_RimIntensity` property (inside CBUFFER_START(UnityPerMaterial))");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void DefaultMaterial_HasRimIntensityZero_NoOpPath()
        {
            // AC2/AC4: a fresh material (the state terrain/canopy/water/prop ship in — they never touch
            // the rim) must default _RimIntensity to 0, so the additive term is exactly
            // `rgb * rim * 0 == 0` → finalCol unchanged, byte-identical to before this property existed.
            // This is the no-regression contract — the whole no-op rests on this default.
            var mat = new Material(FindShader());
            try
            {
                Assert.AreEqual(0f, mat.GetFloat("_RimIntensity"), 1e-6f,
                    "AC2: `_RimIntensity` must DEFAULT to 0 (OFF) — the additive term is exactly zero, so " +
                    "terrain/canopy/water/prop are unaffected (no regression)");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void DefaultMaterial_HasExpectedRimColorAndPower()
        {
            // AC1: the default rim colour is warm-white (a soft bounce, not a neon edge) and the default
            // power is in the soft-silhouette range (~3). Guards the property defaults from drifting (a
            // black rim colour or 0 power would make even a dialed _RimIntensity a no-op / wrong look).
            var mat = new Material(FindShader());
            try
            {
                var c = mat.GetColor("_RimColor");
                Assert.AreEqual(0.95f, c.r, 1e-3f, "default _RimColor.r (warm-white)");
                Assert.AreEqual(0.92f, c.g, 1e-3f, "default _RimColor.g (warm-white)");
                Assert.AreEqual(0.85f, c.b, 1e-3f, "default _RimColor.b (warm-white)");
                Assert.AreEqual(3f, mat.GetFloat("_RimPower"), 1e-3f,
                    "default _RimPower must be ~3 (soft silhouette highlight, not a thin outline)");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void OptInMaterial_CanDialRimIntensity_AboveZero_Distinct()
        {
            // AC3: an opt-in prop dials _RimIntensity > 0 (no keyword — just a SetFloat). The property must
            // be settable and DISTINCT from the default 0, so the additive silhouette highlight is reachable.
            var off = new Material(FindShader());
            var on  = new Material(FindShader());
            try
            {
                on.SetFloat("_RimIntensity", 0.6f);
                on.SetFloat("_RimPower", 2f); // demonstrate the soft-silhouette power from AC3

                Assert.AreEqual(0f, off.GetFloat("_RimIntensity"), 1e-6f, "OFF material: rim intensity 0 (no-op)");
                Assert.AreEqual(0.6f, on.GetFloat("_RimIntensity"), 1e-6f, "ON material: rim intensity dialed up");
                Assert.AreNotEqual(off.GetFloat("_RimIntensity"), on.GetFloat("_RimIntensity"),
                    "the OFF and dialed materials must be DISTINGUISHABLE by _RimIntensity (the rim is reachable)");
            }
            finally { Object.DestroyImmediate(off); Object.DestroyImmediate(on); }
        }
    }
}
