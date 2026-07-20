using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// GENERIC committed-vs-generator drift guard (ticket 86catvvcd — Devon's #315 review finding).
    ///
    /// The pre-existing committed-matches-generator guards are ASSET-SPECIFIC — FKeyMigrationTests
    /// (CommittedGradientSkyMat_HorizonColor_MatchesGeneratorConstant_NoDrift) +
    /// ZoneDLookTests (CommittedGradientSkyMat_MatchesBootGeneratorConstants_NotPollutedBySiblingBuild)
    /// both cover ONLY GradientSky.mat. No guard covered the OTHER drift-prone committed-generated
    /// classes, and TWO real drifts came from exactly that unguarded space:
    ///   (1) Melee_Attack.fbx.meta lost its generator-authored CastawayMelee clip entry (clipAnimations
    ///       went []), shipped stale, fixed by #315 (d95f81a) / ticket 86cahxeek.
    ///   (2) ZoneD_PostProfile.asset Vignette drifted (fixed earlier; #315 confirmed the generator values).
    ///
    /// This class re-runs each generator's OWN expectations against the COMMITTED asset bytes on disk for
    /// that drift-prone set (clip-bearing FBX metas + post-profiles) and reds on divergence, so
    /// committed-state drift is caught at PR time rather than by accident during an unrelated re-bake.
    ///
    /// SCOPE — do NOT over-read (same limitation as the GradientSky.mat guards, #256 NIT 2 / #231 lesson):
    /// the CI `unity` job runs BootstrapProject.Run (which RE-BAKES both assets from the generator) BEFORE
    /// EditMode, so a COMMITTED-ONLY drift is overwritten with generator-fresh bytes before these tests read
    /// it on CI. This guards committed-state HONESTY (raw-editor reads / reviewer diffs / local no-rebake
    /// runs), NOT build correctness — "CI-green proves the BUILD is correct, never that the COMMIT matches
    /// the generator" (unity-conventions.md, ticket 86cahvntg). Success test: reverting #315's meta fix
    /// locally (without a re-bake) reds CommittedMeleeFbxMeta_CarriesRenamedCastawayMeleeClip_NoDrift.
    ///
    /// To EXTEND to a newly-drift-prone committed-generated asset: add ONE focused test that loads the
    /// COMMITTED asset off disk (AssetImporter.GetAtPath / AssetDatabase.LoadAssetAtPath — never a live
    /// scene object) and compares to the generator's own source constant. Keep it TARGETED to classes that
    /// have actually drifted; do not sweep every generated asset (the ticket's explicit "do not boil the ocean").
    /// </summary>
    public class CommittedAssetDriftGuardTests
    {
        // ===== Clip-bearing FBX meta: Melee_Attack.fbx.meta (the #315 / 86cahxeek drift target) =====

        [Test]
        public void CommittedMeleeFbxMeta_CarriesRenamedCastawayMeleeClip_NoDrift()
        {
            // CharacterAssetGen.ConfigureMeleeFbx -> RenameNonLooping renames the Mixamo source take ("mixamo.com")
            // to CharacterAssetGen.MeleeClip ("CastawayMelee") as a NON-looping one-shot and writes that clip into
            // Melee_Attack.fbx.meta's clipAnimations. The committed meta MUST carry that entry: it drifted to
            // clipAnimations:[] (the renamed clip absent — only the raw take survives) and shipped stale until
            // #315 (d95f81a) re-baked it. Reverting #315 (without a re-bake) empties clipAnimations -> this reds.
            var importer = AssetImporter.GetAtPath(CharacterAssetGen.MeleeFbxPath) as ModelImporter;
            Assert.IsNotNull(importer, CharacterAssetGen.MeleeFbxPath + " must be importable as a ModelImporter");

            // importer.clipAnimations is the USER-authored (committed) clip list. When the generator's rename
            // never ran / was reverted it is EMPTY here (RenameNonLooping's defaultClipAnimations fallback lives
            // in the generator, not on this property) — so an absent CastawayMelee entry IS the committed drift.
            bool found = false;
            bool loops = true;
            foreach (var c in importer.clipAnimations)
                if (c.name == CharacterAssetGen.MeleeClip) { found = true; loops = c.loopTime; break; }

            Assert.IsTrue(found,
                $"committed {CharacterAssetGen.MeleeFbxPath}.meta must carry the generator-renamed " +
                $"'{CharacterAssetGen.MeleeClip}' clipAnimation (CharacterAssetGen.ConfigureMeleeFbx). An empty " +
                "clipAnimations here IS the 86cahxeek/#315 drift: the committed meta lost the renamed clip the " +
                "generator authors, while CI's re-bake masked it.");
            Assert.IsFalse(loops,
                $"committed '{CharacterAssetGen.MeleeClip}' must be a NON-looping one-shot (RenameNonLooping sets " +
                "loopTime=false — a chop is a single strike, not a loop)");
        }

        // ===== Per-class weapon SWING FBX metas (86caffwv5 — same drift class as Melee_Attack.fbx) =====

        [Test]
        public void CommittedSwingFbxMetas_CarryRenamedNonLoopingClips_NoDrift()
        {
            // CharacterAssetGen.ConfigureGenericClipFbx -> RenameNonLooping renames each swing FBX's Mixamo source
            // take to its per-class clip name (CastawayAxeSwing / …) as a NON-looping one-shot, written into the
            // committed .fbx.meta clipAnimations. The SAME drift class as Melee_Attack.fbx (clipAnimations went []):
            // if a committed swing meta loses its renamed clip, the AttackX state ships a T-pose. Reds on that drift.
            var expected = new (string fbxPath, string clip)[]
            {
                (CharacterAssetGen.AttackAxeFbxPath,     CharacterAssetGen.AxeSwingClip),
                (CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip),
                (CharacterAssetGen.AttackDaggerFbxPath,  CharacterAssetGen.DaggerStabClip),
                (CharacterAssetGen.AttackSpearFbxPath,   CharacterAssetGen.SpearThrustClip),
                (CharacterAssetGen.AttackSwordFbxPath,   CharacterAssetGen.SwordSlashClip),
            };
            foreach (var (fbxPath, clip) in expected)
            {
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                Assert.IsNotNull(importer, fbxPath + " must be importable as a ModelImporter");
                bool found = false, loops = true;
                foreach (var c in importer.clipAnimations)
                    if (c.name == clip) { found = true; loops = c.loopTime; break; }
                Assert.IsTrue(found, $"committed {fbxPath}.meta must carry the generator-renamed '{clip}' clip " +
                    "(CharacterAssetGen.ConfigureGenericClipFbx). An empty clipAnimations here IS the drift class the " +
                    "Melee_Attack.fbx meta hit (#315) — the committed meta lost the renamed clip, shipping a T-pose state.");
                Assert.IsFalse(loops, $"committed '{clip}' must be a NON-looping one-shot (a swing is a single strike)");
            }
        }

        // ===== Post-profile: ZoneD_PostProfile.asset (the ticket's 2nd named drift target) =====

        [Test]
        public void CommittedZoneDPostProfile_VignetteAndStack_MatchGeneratorConstants_NoDrift()
        {
            // QualityPassGen.BuildGlobalPostVolume builds the committed ZoneD_PostProfile.asset with a fixed
            // Bloom + ColorAdjustments + WhiteBalance + Vignette + Tonemapping stack. Vignette (intensity 0.28,
            // smoothness 0.5 — QualityPassGen.cs:232-233) was the ticket's named drift target. Read the COMMITTED
            // profile off disk (not the live scene Volume — that couples to open-scene session state) and compare
            // to the generator's own literals. (QualityPassGen exposes these as inline literals, not consts, so
            // the expected values are forwarded here with QualityPassGen.cs line-refs — the same hand-synced
            // convention ZoneDLookTests uses for the sun defaults.)
            const string path = "Assets/Settings/ZoneD_PostProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.IsNotNull(profile, $"the committed global post profile must exist at {path}");

            // Full generator component set present — a DROPPED component is the silent-vanish class the
            // AddObjectToAsset persistence exists to prevent (QualityPassGen.cs:246-250). ZoneDLookTests guards
            // the live SCENE Volume's set; this guards the COMMITTED profile bytes.
            Assert.IsTrue(profile.Has<Bloom>(), "committed profile must include Bloom");
            Assert.IsTrue(profile.Has<ColorAdjustments>(), "committed profile must include ColorAdjustments");
            Assert.IsTrue(profile.Has<WhiteBalance>(), "committed profile must include WhiteBalance");
            Assert.IsTrue(profile.Has<Vignette>(), "committed profile must include Vignette");
            Assert.IsTrue(profile.Has<Tonemapping>(), "committed profile must include Tonemapping");

            // Vignette — the named drift target (QualityPassGen.cs:231-233).
            Assert.IsTrue(profile.TryGet(out Vignette vig), "committed profile must expose a Vignette component");
            Assert.AreEqual(0.28f, vig.intensity.value, 1e-3f,
                "committed Vignette.intensity must == the QualityPassGen constant 0.28 (QualityPassGen.cs:232)");
            Assert.AreEqual(0.5f, vig.smoothness.value, 1e-3f,
                "committed Vignette.smoothness must == the QualityPassGen constant 0.5 (QualityPassGen.cs:233)");

            // Second numeric anchor so a value-drift on Bloom is caught too (QualityPassGen.cs:206-207).
            Assert.IsTrue(profile.TryGet(out Bloom bloom), "committed profile must expose a Bloom component");
            Assert.AreEqual(0.25f, bloom.intensity.value, 1e-3f,
                "committed Bloom.intensity must == the QualityPassGen constant 0.25 (QualityPassGen.cs:206)");
            Assert.AreEqual(1.02f, bloom.threshold.value, 1e-3f,
                "committed Bloom.threshold must == the QualityPassGen constant 1.02 (QualityPassGen.cs:207)");

            // WhiteBalance temperature anchor (QualityPassGen.cs:227).
            Assert.IsTrue(profile.TryGet(out WhiteBalance wb), "committed profile must expose a WhiteBalance component");
            Assert.AreEqual(8f, wb.temperature.value, 1e-3f,
                "committed WhiteBalance.temperature must == the QualityPassGen constant 8 (QualityPassGen.cs:227)");
        }
    }
}
