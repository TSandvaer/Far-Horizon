using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the stylized HERO AXE (ticket 86ca8ce6y — the style-wave anchor) is
    /// SERIALIZED into the Boot scene the exe ships, with its three guide anchor colors intact — not
    /// added at Awake (the editor-vs-runtime serialization trap, unity-conventions.md, would mangle/drop
    /// an Awake-built prop — the "legs-up" class). Sibling of CraftSceneTests; same regression-guard
    /// intent: drop the BuildHeroAxe authoring (or break its material assignment / submesh→color mapping)
    /// and this goes RED in headless CI, rather than the shipped build silently lacking the loop's hero
    /// tool or rendering it in wrong colors.
    ///
    /// Binary scenes can't be GUID-grepped, so this EditMode reader is the authoritative check that the
    /// axe MESH + the per-submesh anchor MATERIALS actually live in Boot.unity (the
    /// component-in-source-but-not-serialized failure class, unity-conventions.md).
    /// </summary>
    public class HeroAxeSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        // Tolerance for the shipped material color vs the guide anchor. The authoring side sets the exact
        // anchor Color; this guards against a drift/regression (wrong channel, a stray recolor), not a
        // pixel-exact match — small slack covers float round-trip through serialization.
        private const float ColorTol = 0.02f;

        [Test]
        public void BootScene_CarriesHeroAxe_UnderTheCraftSpot()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe,
                $"the Boot scene must carry the '{MovementCameraScene.HeroAxeObjectName}' GameObject under " +
                "the CraftSpot — the loop's hero tool, serialized into the scene (not Awake-built; " +
                "unity-conventions.md editor-vs-runtime trap)");

            var mf = axe.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "the hero axe must have a MeshFilter");
            Assert.IsNotNull(mf.sharedMesh, "the hero axe's mesh must be serialized into the scene");
            Assert.AreEqual(HeroAxeMesh.SUBMESH_COUNT, mf.sharedMesh.subMeshCount,
                "the shipped axe mesh must keep its 3 submeshes (HEAD/BEVEL/HAFT) so the anchor colors map");
        }

        [Test]
        public void BootScene_HeroAxe_CarriesThreeAnchorMaterials_HeadBevelHaft()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present (see BootScene_CarriesHeroAxe_UnderTheCraftSpot)");

            var mr = axe.GetComponent<MeshRenderer>();
            Assert.IsNotNull(mr, "the hero axe must have a MeshRenderer");
            var mats = mr.sharedMaterials;
            Assert.AreEqual(HeroAxeMesh.SUBMESH_COUNT, mats.Length,
                "the hero axe must carry a 3-material array (one per submesh) serialized into the scene");

            AssertColorNear(mats[HeroAxeMesh.SUBMESH_HEAD], MovementCameraScene.AxeHeadColor,
                "HEAD (barn red #A33B30)");
            AssertColorNear(mats[HeroAxeMesh.SUBMESH_BEVEL], MovementCameraScene.AxeBevelColor,
                "EDGE BEVEL (pale steel #E4E2DC) — the signature near-white cutting-edge plane");
            AssertColorNear(mats[HeroAxeMesh.SUBMESH_HAFT], MovementCameraScene.AxeHaftColor,
                "HAFT (warm brown #7A5230)");
        }

        [Test]
        public void BootScene_CarriesAxeVerifyCapture_OnTheBootObject()
        {
            // Regression guard for Tess's PR #21 NIT fix: the committed -verifyAxe bevel-closeup capture
            // path must be SERIALIZED onto the Boot object (sibling of the craft/chop/movement captures),
            // else the edge-bevel claim loses its committed, repeatable shipped-build evidence path. Drop
            // the WireAxeVerifyCapture authoring and this goes RED — the NIT path silently vanishing from
            // the shipped scene is exactly the failure class this guards.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var boot = GameObject.Find("Boot");
            Assert.IsNotNull(boot, "the Boot scene must carry the 'Boot' object (host of the verify captures)");
            Assert.IsNotNull(boot.GetComponent<AxeVerifyCapture>(),
                "the Boot object must carry the AxeVerifyCapture component (the committed -verifyAxe " +
                "bevel-closeup capture path), serialized into the scene — not Awake-added");
        }

        private static void AssertColorNear(Material mat, Color anchor, string label)
        {
            Assert.IsNotNull(mat, $"the {label} material slot must be assigned");
            Color c = mat.GetColor("_BaseColor");
            Assert.AreEqual(anchor.r, c.r, ColorTol, $"{label}: red channel must match the guide anchor");
            Assert.AreEqual(anchor.g, c.g, ColorTol, $"{label}: green channel must match the guide anchor");
            Assert.AreEqual(anchor.b, c.b, ColorTol, $"{label}: blue channel must match the guide anchor");
        }

        private static GameObject FindHeroAxe()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindByName(root.transform, MovementCameraScene.HeroAxeObjectName);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static Transform FindByName(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindByName(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
