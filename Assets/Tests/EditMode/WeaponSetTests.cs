using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for the in-house weapon SET (ticket 86cabh907, Route A) — the matched
    /// axe/knife/sword/spear, the SHARED palette material, and the family lineup prefab.
    ///
    /// Regression-guard intent (the bug CLASS, not the instance):
    ///   - A new family item must SHARE Mat_WeaponPalette (URP/Unlit) — if a future weapon ships its own
    ///     baked atlas (the CC-BY-axe outlier mistake Route A retired), the shared-material assert reds.
    ///   - The set must stay chunky low-poly — a tri-count blow-out (e.g. an accidental Sub-D / smooth
    ///     mesh) reds the budget assert.
    ///   - The lineup prefab (the shipped-build capture subject) must carry all four meshes.
    /// </summary>
    public class WeaponSetTests
    {
        private static readonly string[] FbxPaths =
        {
            WeaponPackAssetGen.AxeFbxPath,
            WeaponPackAssetGen.KnifeFbxPath,
            WeaponPackAssetGen.SwordFbxPath,
            WeaponPackAssetGen.SpearFbxPath,
        };

        [Test]
        public void AllFourWeapons_FbxImport_AsRealMeshes()
        {
            foreach (var path in FbxPaths)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(fbx, $"weapon FBX must import at {path} (the in-house pipeline export)");
                var mf = fbx.GetComponentInChildren<MeshFilter>(true);
                Assert.IsNotNull(mf, $"{path} must carry a MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, $"{path} mesh must import");
                Assert.Greater(mf.sharedMesh.vertexCount, 8,
                    $"{path} must be a real modeled weapon, not a placeholder primitive");
            }
        }

        [Test]
        public void EveryWeapon_StaysChunkyLowPoly_WithinTriBudget()
        {
            // Per the style spec §3 (Erik §E5) the family is chunky low-poly. Generous upper bounds catch
            // an accidental Sub-D/smooth blow-out; the real values are far lower (axe ~236, others <100).
            var budgets = new System.Collections.Generic.Dictionary<string, int>
            {
                { WeaponPackAssetGen.AxeFbxPath,   600 },
                { WeaponPackAssetGen.KnifeFbxPath, 400 },
                { WeaponPackAssetGen.SwordFbxPath, 700 },
                { WeaponPackAssetGen.SpearFbxPath, 500 },
            };
            foreach (var kv in budgets)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Key);
                Assert.IsNotNull(fbx, kv.Key);
                int tris = 0;
                foreach (var mf in fbx.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                Assert.That(tris, Is.GreaterThan(8).And.LessThanOrEqualTo(kv.Value),
                    $"{kv.Key} tri count {tris} must be chunky low-poly (<= {kv.Value}); a blow-out means " +
                    "a Sub-D / smoothing modifier slipped into the export.");
            }
        }

        [Test]
        public void SharedPaletteMaterial_IsUrpUnlit_OnThePaletteTexture()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WeaponPackAssetGen.MaterialPath);
            Assert.IsNotNull(mat, $"the shared palette material must exist at {WeaponPackAssetGen.MaterialPath}");
            Assert.IsNotNull(mat.shader, "Mat_WeaponPalette must have a shader");
            Assert.AreEqual("Universal Render Pipeline/Unlit", mat.shader.name,
                "Mat_WeaponPalette must be URP/Unlit (flat-shaded palette read — NOT a lit/baked-atlas " +
                "material; the baked atlas is the outlier Route A retired).");
            var tex = mat.GetTexture("_BaseMap");
            Assert.IsNotNull(tex, "Mat_WeaponPalette must bind the weapon_palette.png as Base Map");
        }

        [Test]
        public void LineupPrefab_CarriesAllFourWeapons_OnTheSharedMaterial()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPackAssetGen.PrefabPath);
            Assert.IsNotNull(prefab,
                $"the family lineup prefab (the shipped-build capture subject) must exist at " +
                $"{WeaponPackAssetGen.PrefabPath}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WeaponPackAssetGen.MaterialPath);

            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(4),
                $"the lineup must carry at least four weapon meshes (got {renderers.Length})");
            // Every weapon renderer must use the ONE shared material (so the SRP Batcher folds them).
            foreach (var r in renderers)
                Assert.AreEqual(mat, r.sharedMaterial,
                    $"lineup renderer '{r.name}' must use the shared Mat_WeaponPalette — a per-asset material " +
                    "breaks the ~1-draw-call family contract.");
        }
    }
}
