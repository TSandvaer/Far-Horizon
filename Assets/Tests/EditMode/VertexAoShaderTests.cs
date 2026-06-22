using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARD for the vertex-color AO term on FarHorizon/LowPolyVertexColor (ticket 86caamnra —
    /// Erik R&D §E / Rank 6; Delt06/vertex-ao + sundaysundae low-poly idiom).
    ///
    /// Like the rim term (#107), the AO term has NO keyword — it is a plain `finalCol *= lerp(1, IN.color.a,
    /// _AOStrength)`. So the OFF path is not a stripped shader variant; it is the pure-arithmetic identity
    /// `lerp(1, a, 0) == 1` → finalCol unchanged. That removes the shader_feature-strip false-green class
    /// (the #106 lesson) — there is no ON-variant to strip. What an EditMode test CAN authoritatively assert:
    ///
    ///   (a) the shader compiles with the new term (no HLSL error);
    ///   (b) `_AOStrength` exists as a real shader property (declared INSIDE CBUFFER_START(UnityPerMaterial)
    ///       per §SRP-Batcher — verified by HasProperty + the §SRP-Batcher doc audit);
    ///   (c) _AOStrength DEFAULTS to 0 → the no-op path (AC6a): terrain/canopy/water/prop materials that
    ///       never touch it multiply by exactly 1.0, byte-identical to before — REGARDLESS of their vertex
    ///       alpha (the no-op rests on the strength, not on the alpha being 1);
    ///   (d) an opt-in prop (the ROCK) bakes REAL AO into vertex alpha AND dials _AOStrength > 0 — so the
    ///       crevice-depth darkening is reachable (AC6a "rock with baked AO + _AOStrength ~0.5 shows depth").
    ///
    /// The OFF-vs-dialed RENDERED look (no-regression WorldLook + the visible contact-shadow depth at the
    /// rock base) is proven by the shipped-build A/B captures in the PR Self-Test Report (editor capture is
    /// necessary-NOT-sufficient — unity-conventions.md §Editor-vs-runtime). This test guards the wiring + the
    /// default-0 no-op + the mesh-side AO bake against silent breakage.
    /// </summary>
    public class VertexAoShaderTests
    {
        private const string ShaderName = "FarHorizon/LowPolyVertexColor";
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

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
                $"shader '{ShaderName}' must compile with NO errors (the new vertex-AO term must not " +
                "introduce a compile error)");
#endif
        }

        [Test]
        public void Shader_Declares_AOStrengthProperty_ForSrpBatcher()
        {
            // AC6a: the AO strength must be a real shader property (declared inside CBUFFER_START so it is
            // SRP-Batcher-compliant — the §SRP-Batcher gate). A material built on the shader must expose it.
            var mat = new Material(FindShader());
            try
            {
                Assert.IsTrue(mat.HasProperty("_AOStrength"),
                    "AC6a: the shader must declare an `_AOStrength` property (inside CBUFFER_START(UnityPerMaterial))");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void DefaultMaterial_HasAOStrengthZero_NoOpPath()
        {
            // AC6a: a fresh material (the state terrain/canopy/water ship in — they never touch AO) must
            // default _AOStrength to 0, so the factor is exactly `lerp(1, a, 0) == 1` → finalCol unchanged.
            // The no-op rests on the STRENGTH default, not on the alpha being 1 — so terrain/canopy/water are
            // byte-identical even though they bake alpha = 1 anyway.
            var mat = new Material(FindShader());
            try
            {
                Assert.AreEqual(0f, mat.GetFloat("_AOStrength"), 1e-6f,
                    "AC6a: `_AOStrength` must DEFAULT to 0 (OFF) — the AO factor is exactly 1.0, so " +
                    "terrain/canopy/water/prop are unaffected (no regression)");
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void OptInMaterial_CanDialAOStrength_AboveZero_Distinct()
        {
            // AC6a: an opt-in prop dials _AOStrength > 0 (no keyword — just a SetFloat). The property must be
            // settable and DISTINCT from the default 0, so the contact-shadow darkening is reachable.
            var off = new Material(FindShader());
            var on  = new Material(FindShader());
            try
            {
                on.SetFloat("_AOStrength", 0.5f);
                Assert.AreEqual(0f, off.GetFloat("_AOStrength"), 1e-6f, "OFF material: AO strength 0 (no-op)");
                Assert.AreEqual(0.5f, on.GetFloat("_AOStrength"), 1e-6f, "ON material: AO strength dialed up");
                Assert.AreNotEqual(off.GetFloat("_AOStrength"), on.GetFloat("_AOStrength"),
                    "the OFF and dialed materials must be DISTINGUISHABLE by _AOStrength (the AO is reachable)");
            }
            finally { Object.DestroyImmediate(off); Object.DestroyImmediate(on); }
        }

        // ---- MESH-side AO BAKE: FacetedRock bakes a real geometric AO proxy into vertex-color ALPHA ----

        [Test]
        public void FacetedRock_BakesAO_IntoVertexAlpha_LowDownFacetsMoreOccluded()
        {
            // AC6a: the rock mesh must carry a REAL baked AO in vertex ALPHA (so a dialed _AOStrength actually
            // darkens crevices — a flat alpha=1 would make even a dialed strength a no-op). Assert (1) the
            // alpha SPANS a real range (occluded crevices vs open tops), and (2) the LOW/DOWN facets are more
            // occluded (lower alpha) than the high/up facets — the contact-shadow direction.
            var rock = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 314);
            var cols = rock.colors;
            var verts = rock.vertices;
            Assert.AreEqual(rock.vertexCount, cols.Length, "every rock vertex must carry a colour (with AO in alpha)");

            float loA = float.MaxValue, hiA = float.MinValue;
            foreach (var c in cols) { loA = Mathf.Min(loA, c.a); hiA = Mathf.Max(hiA, c.a); }
            Assert.Greater(hiA - loA, 0.05f,
                $"the rock must carry a REAL baked AO range in vertex alpha (got {loA:F2}..{hiA:F2}) — a flat " +
                "alpha makes a dialed _AOStrength a no-op (the crevice depth would never surface)");
            Assert.LessOrEqual(hiA, 1.0001f, "AO alpha must stay <= 1 (1.0 = fully open / unoccluded)");
            Assert.GreaterOrEqual(loA, 0.5f, "AO alpha must keep a floor (>=~0.55) so crevices darken, never crush to black");

            // Direction check: the mean alpha of the LOWER-half facets must be LESS than the upper-half
            // (low/contact facets more occluded). Average alpha per face (all 3 verts share it) by face centre Y.
            float minY = verts.Min(v => v.y), maxY = verts.Max(v => v.y);
            float midY = (minY + maxY) * 0.5f;
            float lowSum = 0f, highSum = 0f; int lowN = 0, highN = 0;
            int faces = rock.triangles.Length / 3;
            var tris = rock.triangles;
            for (int f = 0; f < faces; f++)
            {
                float cy = (verts[tris[f * 3]].y + verts[tris[f * 3 + 1]].y + verts[tris[f * 3 + 2]].y) / 3f;
                float a = cols[tris[f * 3]].a;
                if (cy < midY) { lowSum += a; lowN++; } else { highSum += a; highN++; }
            }
            Assert.Greater(lowN, 0); Assert.Greater(highN, 0);
            Assert.Less(lowSum / lowN, highSum / highN,
                $"the LOWER facets must be MORE occluded (mean alpha {lowSum / lowN:F2}) than the UPPER facets " +
                $"(mean alpha {highSum / highN:F2}) — the contact-shadow gradient toward the ground (AC6a)");
        }

        [Test]
        public void FacetedRock_AOBake_IsDeterministic_StableAcrossRebuild()
        {
            // The baked scene must be reproducible on rebase-regenerate (binary-scene regenerate-on-rebase) —
            // the AO alpha must be identical for the same seed.
            var a = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 4242);
            var b = LowPolyMeshes.FacetedRock(0.55f, jitter: 0.38f, seed: 4242);
            Assert.AreEqual(a.colors.Length, b.colors.Length, "same seed -> same vertex count");
            Assert.AreEqual(a.colors[0].a, b.colors[0].a, 1e-6f, "same seed -> identical baked AO alpha");
        }

        // ---- SCENE-presence: the shipped rocks carry the AO bake + a dialed _AOStrength material ----

        [Test]
        public void BootScene_Rocks_CarryAOAlpha_AndMaterialDialsAOStrength()
        {
            // The exe-shipped rocks must (1) carry the baked AO in vertex alpha and (2) ride a material that
            // dials _AOStrength > 0 — otherwise the crevice depth never reaches the Sponsor's soak. Terrain/
            // canopy/water keep _AOStrength = 0 (proven by their no-op default; not re-checked here).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var rocks = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.gameObject.name == "RockMesh" && mf.sharedMesh != null)
                .ToArray();
            Assert.Greater(rocks.Length, 0, "must have shipped rocks to check");

            bool anyAOAlpha = false, anyDialed = false;
            foreach (var mf in rocks.Take(8)) // sample a few (don't loop all for speed)
            {
                var cols = mf.sharedMesh.colors;
                if (cols.Length > 0 && cols.Any(c => c.a < 0.999f)) anyAOAlpha = true;

                var mat = mf.GetComponent<MeshRenderer>()?.sharedMaterial;
                if (mat != null && mat.HasProperty("_AOStrength") && mat.GetFloat("_AOStrength") > 0f)
                    anyDialed = true;
            }
            Assert.IsTrue(anyAOAlpha,
                "shipped rocks must carry baked AO in vertex alpha (some alpha < 1) — the crevice-depth source");
            Assert.IsTrue(anyDialed,
                "the shipped rock material must dial _AOStrength > 0 so the baked AO surfaces in the build (AC6a)");
        }
    }
}
