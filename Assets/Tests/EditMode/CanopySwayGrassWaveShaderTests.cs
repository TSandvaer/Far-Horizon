using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the visual-polish wave wiring (tickets 86cabc73q trees + 86cabc737 grass —
    /// Erik lowpoly-trees-grass-sky-research §Trees / §Grass).
    ///
    /// TREES: the canopy sways in the wind, the trunk stays PLANTED. The mechanism is (a) a new `_SwayAmp`
    /// canopy-sway term on FarHorizon/LowPolyVertexColor that displaces XZ keyed off world-position + time,
    /// MASKED by vertex-color ALPHA, and (b) the mesh bake — trunk verts carry alpha 0 (planted), canopy verts
    /// carry alpha 1 (sway). GRASS: the meadow tufts ride the SAME shader with a small `_WaveAmp` so the clump
    /// rocks gently (the wave term already existed; grass just switches off the inert URP/Lit material onto it).
    ///
    /// What an EditMode test CAN authoritatively assert (the RENDERED sway/wind look is judged from the SHIPPED
    /// exe — editor-vs-runtime trap, unity-conventions.md §Editor-vs-runtime; the sway/wave motion is a Sponsor
    /// SOAK call, captured in the PR Self-Test Report):
    ///   (a) the shader compiles with the new term and declares `_SwayAmp/_SwayLen/_SwaySpeed` inside the
    ///       cbuffer (SRP-Batcher) and they DEFAULT to 0 (no-op for every non-canopy material — no regression);
    ///   (b) the MESH bake — TaperedCylinder bakes alpha 0 on EVERY trunk vert; BlobCanopy bakes alpha 1 on
    ///       every canopy vert — so the sway mask is the single source of truth (trunk planted, canopy sways);
    ///   (c) the COMMITTED Boot scene ships a canopy material that dials `_SwayAmp > 0` AND grass clumps that
    ///       ride FarHorizon/LowPolyVertexColor with `_WaveAmp > 0` (the unity-procedural-committed-assets-go-
    ///       stale lesson — the bake/material must reach the BUILT exe, not just the regen code).
    /// </summary>
    public class CanopySwayGrassWaveShaderTests
    {
        private const string ShaderName = "FarHorizon/LowPolyVertexColor";
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        private static Shader FindShader()
        {
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader, $"shader '{ShaderName}' must resolve (registered + compiles)");
            return shader;
        }

        // ---- SHADER: the canopy-sway term wiring + SRP-Batcher + no-op default ----

        [Test]
        public void Shader_Resolves_AndHasNoCompileError()
        {
            var shader = FindShader();
#if UNITY_EDITOR
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader),
                $"shader '{ShaderName}' must compile with NO errors (the new canopy-sway term must not " +
                "introduce a compile error)");
#endif
        }

        [Test]
        public void Shader_Declares_SwayProperties_ForSrpBatcher()
        {
            // The three sway floats must be real shader properties (declared inside CBUFFER_START so they are
            // SRP-Batcher-compliant — the §SRP-Batcher gate). A material built on the shader must expose them.
            var mat = new Material(FindShader());
            try
            {
                Assert.IsTrue(mat.HasProperty("_SwayAmp"), "shader must declare `_SwayAmp` (in CBUFFER_START)");
                Assert.IsTrue(mat.HasProperty("_SwayLen"), "shader must declare `_SwayLen` (in CBUFFER_START)");
                Assert.IsTrue(mat.HasProperty("_SwaySpeed"), "shader must declare `_SwaySpeed` (in CBUFFER_START)");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void DefaultMaterial_HasSwayAmpZero_NoOpPath()
        {
            // A fresh material (the state terrain/water/rock ship in — they never touch sway) must default
            // _SwayAmp to 0, so the sway block is skipped (`if (_SwayAmp > 0.0)`) → posOS unchanged → byte-
            // identical to before this term existed. The no-op rests on the amplitude default.
            var mat = new Material(FindShader());
            try
            {
                Assert.AreEqual(0f, mat.GetFloat("_SwayAmp"), 1e-6f,
                    "`_SwayAmp` must DEFAULT to 0 (OFF) — terrain/water/rock/grass/trunk are unaffected (no regression)");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void OptInMaterial_CanDialSwayAmp_AboveZero_Distinct()
        {
            var off = new Material(FindShader());
            var on  = new Material(FindShader());
            try
            {
                on.SetFloat("_SwayAmp", 0.10f);
                Assert.AreEqual(0f, off.GetFloat("_SwayAmp"), 1e-6f, "OFF material: sway 0 (no-op)");
                Assert.AreEqual(0.10f, on.GetFloat("_SwayAmp"), 1e-6f, "ON material: sway dialed up");
                Assert.AreNotEqual(off.GetFloat("_SwayAmp"), on.GetFloat("_SwayAmp"),
                    "OFF and dialed materials must be DISTINGUISHABLE by _SwayAmp (the sway is reachable)");
            }
            finally { Object.DestroyImmediate(off); Object.DestroyImmediate(on); }
        }

        // ---- MESH BAKE: trunk alpha 0 (planted), canopy alpha 1 (sways) ----

        [Test]
        public void TaperedCylinder_BakesSwayMaskAlphaZero_OnEveryTrunkVert()
        {
            // The trunk is PLANTED — every vert carries vertex-color alpha 0 (the "do NOT sway" mask). RGB is
            // white so a vertex-color material renders _Tint unmodified.
            var trunk = LowPolyMeshes.TaperedCylinder(0.18f, 0.12f, 1.6f, 6);
            var cols = trunk.colors;
            Assert.AreEqual(trunk.vertexCount, cols.Length,
                "every trunk vert must carry a colour (the sway-mask alpha)");
            foreach (var c in cols)
            {
                Assert.AreEqual(0f, c.a, 1e-6f, "every trunk vert must have alpha 0 (planted, no sway)");
                Assert.AreEqual(1f, c.r, 1e-6f, "trunk RGB must be white so _Tint renders unmodified (r)");
                Assert.AreEqual(1f, c.g, 1e-6f, "trunk RGB must be white so _Tint renders unmodified (g)");
                Assert.AreEqual(1f, c.b, 1e-6f, "trunk RGB must be white so _Tint renders unmodified (b)");
            }
        }

        [Test]
        public void TaperedCylinder_VertexCountAndNormals_Unchanged_ByAlphaBake()
        {
            // Adding the colour stream must NOT add verts or break the welded smooth normals (the prior
            // TaperedCylinder_HasCleanNormals contract): still 6-sided welded with unit normals.
            var trunk = LowPolyMeshes.TaperedCylinder(0.18f, 0.12f, 1.6f, 6);
            var normals = trunk.normals;
            Assert.Greater(trunk.vertexCount, 0, "trunk mesh must have verts");
            Assert.AreEqual(trunk.vertexCount, normals.Length, "every trunk vert keeps a normal");
            for (int i = 0; i < normals.Length; i++)
                Assert.AreEqual(1f, normals[i].magnitude, 0.05f, $"trunk vertex {i} normal must be ~unit length");
        }

        [Test]
        public void BlobCanopy_BakesSwayMaskAlphaOne_OnEveryCanopyVert()
        {
            // The canopy SWAYS — every vert carries vertex-color alpha 1 (the "full sway" mask), distinct from
            // the trunk's alpha 0. (alpha 1 is also the AO no-op; _AOStrength is 0 on the canopy material, so it
            // only ever acts as the sway mask.)
            var canopy = LowPolyMeshes.BlobCanopy(1.15f, 5, Color.green, Color.green, Color.green, 42);
            var cols = canopy.colors;
            Assert.AreEqual(canopy.vertexCount, cols.Length, "every canopy vert must carry a colour");
            foreach (var c in cols)
                Assert.AreEqual(1f, c.a, 1e-6f, "every canopy vert must have alpha 1 (full sway mask)");
        }

        // ---- COMMITTED Boot scene: the bake + material reach the BUILT exe (not just regen code) ----

        [Test]
        public void BootScene_Canopies_RideSwayShader_WithSwayDialedAndAlphaOne()
        {
            // The exe-shipped canopies must (1) ride FarHorizon/LowPolyVertexColor with _SwayAmp > 0 and
            // (2) carry alpha-1 verts — otherwise the sway never reaches the Sponsor's soak (unity-procedural-
            // committed-assets-go-stale: the build ships the COMMITTED scene snapshot, so assert the on-disk asset).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var canopies = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "Canopy" && mf.sharedMesh != null)
                .ToArray();
            Assert.Greater(canopies.Length, 0, "the Boot scene must ship tree canopies to check");

            bool anySwayDialed = false, anyAlphaOne = false;
            foreach (var mf in canopies.Take(8))
            {
                var mat = mf.GetComponent<MeshRenderer>()?.sharedMaterial;
                if (mat != null && mat.shader != null && mat.shader.name == ShaderName
                    && mat.HasProperty("_SwayAmp") && mat.GetFloat("_SwayAmp") > 0f)
                    anySwayDialed = true;

                var cols = mf.sharedMesh.colors;
                if (cols.Length > 0 && cols.All(c => c.a > 0.999f)) anyAlphaOne = true;
            }
            Assert.IsTrue(anySwayDialed,
                "shipped canopies must ride the sway shader with _SwayAmp > 0 (the sway reaches the build)");
            Assert.IsTrue(anyAlphaOne,
                "shipped canopy meshes must carry alpha-1 verts (the sway mask source)");
        }

        [Test]
        public void BootScene_Trunks_CarrySwayMaskAlphaZero()
        {
            // The shipped trunks must carry alpha-0 verts so they stay planted even if rendered by the sway
            // shader (the mask is the single source of truth, not the material split alone).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var trunks = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "Trunk" && mf.sharedMesh != null
                             && mf.sharedMesh.name.StartsWith("LP_Trunk"))
                .ToArray();
            Assert.Greater(trunks.Length, 0, "the Boot scene must ship tree trunks to check");

            foreach (var mf in trunks.Take(8))
            {
                var cols = mf.sharedMesh.colors;
                Assert.Greater(cols.Length, 0, "shipped trunk must carry the sway-mask colour stream");
                Assert.IsTrue(cols.All(c => c.a < 0.001f),
                    "every shipped trunk vert must have alpha 0 (planted — the sway mask)");
            }
        }

        [Test]
        public void BootScene_GrassClumps_RideWaveShader_WithWaveDialed()
        {
            // The exe-shipped meadow grass must ride FarHorizon/LowPolyVertexColor with _WaveAmp > 0 — the prior
            // flat URP/Lit material IGNORED the wave (the inert-material trap). Assert the committed asset so the
            // wind reaches the build (unity-procedural-committed-assets-go-stale).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var grass = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "Blades" && mf.sharedMesh != null
                             && mf.sharedMesh.name.StartsWith("LP_Grass"))
                .ToArray();
            Assert.Greater(grass.Length, 0, "the Boot scene must ship meadow grass clumps to check");

            bool anyWaveDialed = false;
            foreach (var mf in grass.Take(8))
            {
                var mat = mf.GetComponent<MeshRenderer>()?.sharedMaterial;
                if (mat != null && mat.shader != null && mat.shader.name == ShaderName
                    && mat.HasProperty("_WaveAmp") && mat.GetFloat("_WaveAmp") > 0f)
                    anyWaveDialed = true;
            }
            Assert.IsTrue(anyWaveDialed,
                "shipped meadow grass must ride the vertex-color shader with _WaveAmp > 0 (the wind reaches the build)");
        }
    }
}
