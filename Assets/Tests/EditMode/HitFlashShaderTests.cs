using NUnit.Framework;
using UnityEngine;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the `_HitFlash` term on FarHorizon/LowPolyVertexColor (ticket 86caxjwb3 AC2 + the
    /// AC7 cross-lane no-op proof).
    ///
    /// === WHAT THIS FILE CAN AND CANNOT PROVE ===
    /// It CAN prove the wiring + the inert default + that the world's other consumers are untouched. It CANNOT
    /// prove the flash DECAYS — that is the ticket's [DFC-1] class and it is invisible to EditMode BY
    /// CONSTRUCTION (the shader clock `_Time.y` is Time.timeSinceLevelLoad, which differs from Time.time only
    /// by the level's load time, and that is zero in the editor). The decay is asserted in PlayMode against the
    /// live material (HitFeedbackPlayModeTests) and in the shipped exe by -verifyHitFeedback's after-0.5s frame.
    /// Do NOT add a "flash fired" assertion here and read it as coverage of the latch class — it is not.
    ///
    /// === WHY DEFAULT 0 IS RIGHT HERE, WHEN IT WOULD HAVE BEEN A BUG FOR THE OTHER SHAPE ===
    /// `_HitFlash` carries an AMPLITUDE, so 0 is an exact no-op (`lerp(a, b, 0) == a`) and matches the three
    /// existing opt-in floats (`_RimIntensity` / `_AOStrength` / `_MeadowPatchAmp`). Had the term instead carried
    /// a TIMESTAMP, a 0 default would flash EVERY consumer of this shared world shader on scene load — terrain,
    /// canopy, water, rock, every prop — because `_Time.y ≈ 0` there, and the correct default would have been
    /// very NEGATIVE (-1000). That distinction is the ticket's [DFC-1 / Claim 2], and the shipped shape is the
    /// amplitude one, so this test asserts 0 deliberately rather than by copying the neighbours.
    /// </summary>
    public class HitFlashShaderTests
    {
        private const string ShaderName = "FarHorizon/LowPolyVertexColor";

        private static Shader FindShader()
        {
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader, $"shader '{ShaderName}' must resolve (registered + compiles)");
            return shader;
        }

        [Test]
        public void Shader_Resolves_AndHasNoCompileError_WithTheHitFlashTerm()
        {
            var shader = FindShader();
#if UNITY_EDITOR
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader),
                $"'{ShaderName}' must compile with NO errors — the new _HitFlash term must not break the " +
                "shared world shader every terrain/canopy/water/prop material rides");
#endif
        }

        [Test]
        public void Shader_Declares_HitFlashProperties_ForSrpBatcher()
        {
            // Both must be REAL shader properties declared INSIDE CBUFFER_START(UnityPerMaterial) — a float
            // declared outside the cbuffer silently drops the renderer out of the SRP batch, which is a FPS
            // regression no test would otherwise catch (unity-conventions.md §SRP-Batcher). The no-MPB rule in
            // AC2 rests on this: distinct material instances batch fine, an MPB does not.
            var mat = new Material(FindShader());
            try
            {
                Assert.IsTrue(mat.HasProperty(FarHorizon.Combat.EnemyHitFeedback.HitFlashProperty),
                    "AC2: the shader must declare `_HitFlash` (the C#-driven eased amplitude)");
                Assert.IsTrue(mat.HasProperty(FarHorizon.Combat.EnemyHitFeedback.HitFlashColorProperty),
                    "AC2: the shader must declare `_HitFlashColor` (the warm-white pulse tone)");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void HitFlash_DefaultsToZero_SoEveryOtherConsumerIsUnchanged()
        {
            // The AC7 shader-no-op proof. A fresh material on this shader — which is what terrain, canopy,
            // trunks, water, rock, grass and every prop material IS — must carry _HitFlash == 0, making the
            // frag term exactly `lerp(finalCol, x, 0) == finalCol`.
            var mat = new Material(FindShader());
            try
            {
                Assert.AreEqual(0f, mat.GetFloat(FarHorizon.Combat.EnemyHitFeedback.HitFlashProperty), 1e-6f,
                    "AC2/AC7: `_HitFlash` must DEFAULT to 0 — this is the SHARED world shader, and a non-zero " +
                    "default would tint the whole island (terrain/canopy/water/rock/props) on scene load");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void HitFlashColor_IsWarmWhite_EveryChannelSubOne_NeverRed()
        {
            // Two tone constraints in one assert, both 🔒 in AC2:
            //  * every channel sub-1.0 (style-guide-v2 §5 HDR clamp) so the pulse cannot bloom-blow-out;
            //  * WARM WHITE, never red — red on a creature reads as GORE and breaks the kid-safe tone
            //    (game-juice.md §0, brief §2.5 "dust-brown, never red"). "Warm" = R >= G >= B, and "white" =
            //    the channels stay close together (a saturated tone would fail the spread bound).
            var mat = new Material(FindShader());
            try
            {
                Color c = mat.GetColor(FarHorizon.Combat.EnemyHitFeedback.HitFlashColorProperty);
                Assert.Less(c.r, 1f, "flash R must be sub-1.0 (HDR clamp — no bloom blowout)");
                Assert.Less(c.g, 1f, "flash G must be sub-1.0");
                Assert.Less(c.b, 1f, "flash B must be sub-1.0");
                Assert.GreaterOrEqual(c.r, c.g, "warm ramp: R >= G");
                Assert.GreaterOrEqual(c.g, c.b, "warm ramp: G >= B");
                Assert.Less(c.r - c.b, 0.25f,
                    "the flash must read as warm WHITE, not a saturated tone — a wide R-vs-B spread is the " +
                    "direction that ends up reading as red/gore on a creature (AC2, kid-safe tone)");
                Assert.Greater(c.r, 0.6f, "the flash must actually be bright enough to READ as a flash");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void TheThreeExistingOptInTerms_StillDefaultToTheirNoOp_CrossLaneGuard()
        {
            // AC7 cross-lane check: this is the shared shader's FOURTH opt-in term. Adding it must not disturb
            // the three that were already there — if a future edit reorders the cbuffer or retypes a property,
            // a silently-changed default would tint terrain / canopy / water / rock / props with no other
            // failure. Named explicitly because the adjacent surfaces (the -seaDiag water fog-cap read, the
            // meadow-patch A/B, _FlatShading variant shipping) are judged by eye, not by a metric.
            var mat = new Material(FindShader());
            try
            {
                Assert.AreEqual(0f, mat.GetFloat("_RimIntensity"), 1e-6f, "_RimIntensity default-0 no-op intact");
                Assert.AreEqual(0f, mat.GetFloat("_AOStrength"), 1e-6f, "_AOStrength default-0 no-op intact");
                Assert.AreEqual(0f, mat.GetFloat("_MeadowPatchAmp"), 1e-6f, "_MeadowPatchAmp default-0 no-op intact");
                Assert.AreEqual(0f, mat.GetFloat("_FlatShading"), 1e-6f, "_FlatShading default-OFF intact");
                Assert.AreEqual(0f, mat.GetFloat("_WaveAmp"), 1e-6f, "_WaveAmp default-0 no-op intact");
                Assert.AreEqual(0f, mat.GetFloat("_SwayAmp"), 1e-6f, "_SwayAmp default-0 no-op intact");
                Assert.AreEqual(0f, mat.GetFloat("_FogCap"), 1e-6f, "_FogCap default-0 (full fog) intact");
            }
            finally { Object.DestroyImmediate(mat); }
        }
    }
}
