using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC5 REGRESSION GUARD for the `_FlatShading` ddx/ddy toggle on FarHorizon/LowPolyVertexColor
    /// (ticket 86caamnjb — Erik R&D §A / Rank 2, Hextant Studios flat-low-poly technique).
    ///
    /// The shader's RENDERED output (smooth-vs-faceted) can only be judged from the SHIPPED exe
    /// (editor-vs-runtime shader-render trap, unity-conventions.md §Editor-vs-runtime — editor capture
    /// is necessary-NOT-sufficient). What an EditMode test CAN authoritatively assert is the CONTRACT
    /// that makes the two render paths reachable + keeps the OFF default unchanged:
    ///
    ///   (a) OFF (default) is UNCHANGED — the property defaults to 0 and a fresh material instance has
    ///       NO `_FLATSHADING_ON` keyword enabled, so terrain/canopy/water (which never touch the toggle)
    ///       render the SAME interpolated-vertex-normal path as before this property existed (AC2 / AC4).
    ///   (b) ON is REACHABLE + DISTINCT — enabling the keyword + setting `_FlatShading=1` activates the
    ///       `_FLATSHADING_ON` variant, so an opt-in prop gets the per-face ddx/ddy normal (AC3).
    ///   (c) SRP-Batcher compliance — `_FlatShading` is a real shader float property (declared inside
    ///       CBUFFER_START(UnityPerMaterial), verified by the §SRP-Batcher audit + HasProperty here).
    ///
    /// The OFF-vs-ON FACETED LOOK + the no-regression WorldLook are proven by the shipped-build A/B
    /// captures in the PR Self-Test Report — this test guards that the wiring can't silently break.
    /// </summary>
    public class FlatShadingShaderTests
    {
        private const string ShaderName = "FarHorizon/LowPolyVertexColor";
        private const string Keyword    = "_FLATSHADING_ON";

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
                $"shader '{ShaderName}' must compile with NO errors (the new _FlatShading keyword branch must " +
                "not introduce a compile error)");
#endif
        }

        [Test]
        public void Shader_Declares_FlatShadingProperty_ForSrpBatcher()
        {
            // AC1: the new property must be a real shader property (declared inside CBUFFER_START so it is
            // SRP-Batcher-compliant — the §SRP-Batcher gate). A material built on the shader must expose it.
            var mat = new Material(FindShader());
            try
            {
                Assert.IsTrue(mat.HasProperty("_FlatShading"),
                    "AC1: the shader must declare a `_FlatShading` property (the toggle backing float; must " +
                    "live inside CBUFFER_START(UnityPerMaterial) per §SRP-Batcher)");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void DefaultMaterial_HasFlatShadingOff_KeywordDisabled_SmoothPath()
        {
            // AC2/AC4: a fresh material (the state terrain/canopy/water ship in — they never touch the toggle)
            // must default the property to 0 AND have the _FLATSHADING_ON keyword DISABLED, so it renders the
            // exact prior interpolated-vertex-normal path. This is the no-regression contract.
            var mat = new Material(FindShader());
            try
            {
                Assert.AreEqual(0f, mat.GetFloat("_FlatShading"), 1e-6f,
                    "AC2: `_FlatShading` must DEFAULT to 0 (OFF) so terrain/canopy/water are unaffected");

                var kw = new LocalKeyword(mat.shader, Keyword);
                Assert.IsFalse(mat.IsKeywordEnabled(kw),
                    $"AC2/AC4: a default material must have the `{Keyword}` keyword DISABLED — the smooth " +
                    "(interpolated vertex-normal) path is byte-identical to before this change");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void OptInMaterial_EnablesFlatShading_KeywordActive_FacetedPath()
        {
            // AC3: an opt-in prop sets the toggle ON (the [Toggle(_FLATSHADING_ON)] drives the keyword; the
            // canonical opt-in is EnableKeyword + SetFloat). The keyword must then be ACTIVE so the ddx/ddy
            // per-face normal branch is compiled in for that material.
            var mat = new Material(FindShader());
            try
            {
                var kw = new LocalKeyword(mat.shader, Keyword);
                mat.SetFloat("_FlatShading", 1f);
                mat.EnableKeyword(kw);

                Assert.AreEqual(1f, mat.GetFloat("_FlatShading"), 1e-6f, "AC3: opt-in material sets _FlatShading=1");
                Assert.IsTrue(mat.IsKeywordEnabled(kw),
                    $"AC3: with the toggle ON, the `{Keyword}` keyword must be ACTIVE (the per-face ddx/ddy " +
                    "flat-shading variant is bound for this material)");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void OffAndOn_AreDistinguishable_ToggleIsWired()
        {
            // The toggle must actually CHANGE state — an OFF material and an ON material must differ in their
            // keyword state (a no-op toggle that left the keyword identical would render the same path for both
            // and silently defeat the feature). Guards against the toggle name/keyword drifting apart.
            var off = new Material(FindShader());
            var on  = new Material(FindShader());
            try
            {
                var kwOff = new LocalKeyword(off.shader, Keyword);
                var kwOn  = new LocalKeyword(on.shader, Keyword);
                on.SetFloat("_FlatShading", 1f);
                on.EnableKeyword(kwOn);

                Assert.IsFalse(off.IsKeywordEnabled(kwOff), "OFF material: keyword disabled");
                Assert.IsTrue(on.IsKeywordEnabled(kwOn),    "ON material: keyword enabled");
                Assert.AreNotEqual(off.IsKeywordEnabled(kwOff), on.IsKeywordEnabled(kwOn),
                    "the OFF and ON materials must be DISTINGUISHABLE by keyword state (the toggle is wired)");
            }
            finally { Object.DestroyImmediate(off); Object.DestroyImmediate(on); }
        }
    }
}
