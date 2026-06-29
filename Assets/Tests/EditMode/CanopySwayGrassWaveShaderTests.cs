using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the visual-polish wind wiring (tickets 86cabc73q trees + 86cabc737 grass —
    /// Erik lowpoly-trees-grass-sky-research §Trees / §Grass), UPDATED for the #172 SOAK-NIT (2026-06-29):
    /// "moving grass/bushes looks weird; only the trees up in the air should move."
    ///
    /// TREES (KEPT): the canopy sways in the wind, the trunk stays PLANTED. The mechanism is (a) a `_SwayAmp`
    /// canopy-sway term on FarHorizon/LowPolyVertexColor that displaces XZ keyed off world-position + time,
    /// MASKED by vertex-color ALPHA, and (b) the mesh bake — trunk verts carry alpha 0 (planted), canopy verts
    /// carry alpha 1 (sway). This still works after the NIT — the trees are the ONLY thing that moves.
    ///
    /// GRASS (now STATIONARY): the meadow tufts KEEP the move onto the vertex-color shader (the Sponsor approved
    /// the GREEN + batching of that swap), but `_WaveAmp` is now 0 — the grass does NOT move.
    /// BUSHES (now STATIONARY): bushes (body + berries) read the same greens as the canopy and previously SHARED
    /// the canopy material (which carries _SwayAmp = 0.10) + bake alpha 1, so they inherited the canopy sway and
    /// MOVED. They now ride a dedicated bush material (same shader + tint, _SwayAmp = 0) → stationary.
    ///
    /// What an EditMode test CAN authoritatively assert (the RENDERED motion is judged from the SHIPPED exe —
    /// editor-vs-runtime trap, unity-conventions.md §Editor-vs-runtime; captured in the PR Self-Test Report):
    ///   (a) the shader compiles + declares `_SwayAmp/_SwayLen/_SwaySpeed` inside the cbuffer (SRP-Batcher) and
    ///       they DEFAULT to 0 (no-op for every non-canopy material — no regression);
    ///   (b) the MESH bake — TaperedCylinder bakes alpha 0 on EVERY trunk vert; BlobCanopy bakes alpha 1 on
    ///       every canopy vert — so the sway mask is the single source of truth (trunk planted, canopy sways);
    ///   (c) the COMMITTED Boot scene ships a canopy material that dials `_SwayAmp > 0` (trees move) AND grass
    ///       clumps with `_WaveAmp == 0` (STATIONARY, still on the vertex-color shader = green/batch kept) AND
    ///       bushes with `_SwayAmp == 0` (STATIONARY) — the unity-procedural-committed-assets-go-stale lesson
    ///       (the bake/material must reach the BUILT exe, not just the regen code).
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
        public void BootScene_GrassClumps_Stationary_OnShader_NoWave()
        {
            // #172 SOAK-NIT: meadow grass must NOT move. It KEEPS the move onto FarHorizon/LowPolyVertexColor
            // (the Sponsor approved the green + batching of that swap), so it must STILL ride the shader — but
            // every grass material must have _WaveAmp == 0 (and never sway either: _SwayAmp == 0). Assert the
            // COMMITTED asset so the stationary state reaches the build (unity-procedural-committed-assets-go-stale).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var grass = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "Blades" && mf.sharedMesh != null
                             && mf.sharedMesh.name.StartsWith("LP_Grass"))
                .ToArray();
            Assert.Greater(grass.Length, 0, "the Boot scene must ship meadow grass clumps to check");

            bool anyOnShader = false;
            foreach (var mf in grass.Take(8))
            {
                var mat = mf.GetComponent<MeshRenderer>()?.sharedMaterial;
                Assert.IsNotNull(mat, "shipped grass clump must carry a material");
                if (mat.shader != null && mat.shader.name == ShaderName)
                {
                    anyOnShader = true;
                    // STATIONARY: zero vertex displacement from BOTH the wave and the sway terms.
                    if (mat.HasProperty("_WaveAmp"))
                        Assert.AreEqual(0f, mat.GetFloat("_WaveAmp"), 1e-6f,
                            "shipped grass must be STATIONARY — _WaveAmp == 0 (#172 soak-NIT: grass must not move)");
                    if (mat.HasProperty("_SwayAmp"))
                        Assert.AreEqual(0f, mat.GetFloat("_SwayAmp"), 1e-6f,
                            "shipped grass must be STATIONARY — _SwayAmp == 0 (grass never sways either)");
                }
            }
            Assert.IsTrue(anyOnShader,
                "shipped meadow grass must still ride FarHorizon/LowPolyVertexColor (green + batching of the swap kept)");
        }

        [Test]
        public void BootScene_Bushes_Stationary_NoSway_NoWave()
        {
            // #172 SOAK-NIT: bushes (body + berries) must NOT move. They previously SHARED the canopy material
            // (_SwayAmp = 0.10) + bake alpha 1, so they inherited the canopy sway. They now ride a dedicated
            // bush material with NO displacement. Assert EVERY shipped bush mesh's material has _SwayAmp == 0
            // AND _WaveAmp == 0 — regardless of its alpha-1 verts the bush stays planted (the sway/wave branches
            // are no-ops when the amplitudes are 0). Covers BushBody AND Berries (both ride the bush material).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var bushMeshes = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.sharedMesh != null
                             && (mf.sharedMesh.name.StartsWith("LP_BushBlob")
                                 || mf.sharedMesh.name.StartsWith("LP_BerryCluster")))
                .ToArray();
            Assert.Greater(bushMeshes.Length, 0, "the Boot scene must ship bushes (body + berries) to check");

            foreach (var mf in bushMeshes.Take(12))
            {
                var mat = mf.GetComponent<MeshRenderer>()?.sharedMaterial;
                Assert.IsNotNull(mat, $"shipped bush mesh '{mf.sharedMesh.name}' must carry a material");
                if (mat.HasProperty("_SwayAmp"))
                    Assert.AreEqual(0f, mat.GetFloat("_SwayAmp"), 1e-6f,
                        $"shipped bush '{mf.sharedMesh.name}' must be STATIONARY — _SwayAmp == 0 (#172 soak-NIT: bushes must not move)");
                if (mat.HasProperty("_WaveAmp"))
                    Assert.AreEqual(0f, mat.GetFloat("_WaveAmp"), 1e-6f,
                        $"shipped bush '{mf.sharedMesh.name}' must be STATIONARY — _WaveAmp == 0");
            }
        }
    }
}
