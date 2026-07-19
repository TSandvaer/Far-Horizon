using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Committed-vs-generator drift guard for the WEAPON-SET LINEUP prefab (ticket 86catwzhy — from
    /// Drew's #317 report). SAME family + pattern as CommittedAssetDriftGuardTests (86catvvcd / #316),
    /// deliberately kept in a SEPARATE SIBLING FILE per the orchestration scoping override on this ticket:
    /// PR #317's in-flight branch carries a copy of CommittedAssetDriftGuardTests.cs, so editing that file
    /// on main now would manufacture a merge conflict with the dial-staged v4 PR. Same drift-guard family,
    /// separate file — the ticket's "extend the class" intent is satisfied by the pattern, not the literal
    /// file, for this one PR only. (When #317 lands, this test MAY be folded back into the sibling class.)
    ///
    /// THE DRIFT THIS CATCHES (#304): the wood-tier PR added WeaponPackAssetGen.WoodSet (5 wood FBX) to the
    /// generator's BuildFamilyPrefab, but the COMMITTED Resources/WeaponSetLineup.prefab was NOT re-baked →
    /// the 5 wood child nodes are ABSENT from the committed prefab (only 10 of 15 nodes present). Because
    /// CI's BootstrapProject.Run re-bakes the prefab (WeaponPackAssetGen.PrepareWeaponPack, BootstrapProject
    /// .cs:94) BEFORE build/EditMode, every automated gate stayed green while the committed SOURCE drifted —
    /// the unity-procedural-committed-assets-go-stale class (unity-conventions.md §Headless CI traps; 86cahvntg).
    ///
    /// SCOPE — identical limitation to the #316 GradientSky.mat / meta / post-profile guards (do NOT over-read):
    /// on CI the unity job re-bakes the prefab from the generator BEFORE EditMode, so a COMMITTED-ONLY drift is
    /// overwritten with generator-fresh 15-node bytes before this test reads it → this test is GREEN on CI even
    /// when the committed prefab is stale. It guards committed-state HONESTY (raw-editor reads / reviewer diffs
    /// / local no-rebake runs), NOT build correctness — "CI-green proves the BUILD is correct, never that the
    /// COMMIT matches the generator" (unity-conventions.md, 86cahvntg). Success test: on the CURRENTLY-stale
    /// committed prefab a NO-REBAKE EditMode run reds this test naming the 5 missing wood nodes; a re-bake
    /// (BootstrapProject.Run / PrepareWeaponPack) restores all 15 → green.
    ///
    /// The expected node set traces to the generator's OWN public source-of-truth consts (the 15 *FbxPath
    /// constants), NOT a hand-copied literal list — each expected node name = Path.GetFileNameWithoutExtension
    /// (fbxPath), exactly the AddRow child-naming rule (WeaponPackAssetGen.cs:235). WoodSet/StoneSet/IronSet
    /// are private, so the public *FbxPath consts (the identical FBX set) are the read seam here per the
    /// ticket's route note — no visibility widened on the generator.
    /// </summary>
    public class CommittedLineupDriftGuardTests
    {
        // The 15 generator source-of-truth FBX paths (5 wood + 5 stone + 5 iron), forwarded from the PUBLIC
        // WeaponPackAssetGen consts so the expected set can NEVER drift from the generator without a compile
        // break here. Column order mirrors StoneSet/IronSet/WoodSet (axe/knife/sword/spear/pickaxe) × tier.
        private static readonly string[] ExpectedFbxPaths =
        {
            // wood (the #304 drop)
            WeaponPackAssetGen.AxeWoodFbxPath,
            WeaponPackAssetGen.KnifeWoodFbxPath,
            WeaponPackAssetGen.SwordWoodFbxPath,
            WeaponPackAssetGen.SpearWoodFbxPath,
            WeaponPackAssetGen.PickaxeWoodFbxPath,
            // stone
            WeaponPackAssetGen.AxeFbxPath,
            WeaponPackAssetGen.KnifeFbxPath,
            WeaponPackAssetGen.SwordFbxPath,
            WeaponPackAssetGen.SpearFbxPath,
            WeaponPackAssetGen.PickaxeStoneFbxPath,
            // iron
            WeaponPackAssetGen.IronAxeFbxPath,
            WeaponPackAssetGen.IronKnifeFbxPath,
            WeaponPackAssetGen.IronSwordFbxPath,
            WeaponPackAssetGen.IronSpearFbxPath,
            WeaponPackAssetGen.PickaxeIronFbxPath,
        };

        [Test]
        public void CommittedWeaponSetLineupPrefab_CarriesAll15GeneratorNodes_NoDrift()
        {
            // Read the COMMITTED prefab off disk (never a live scene object / re-baked instance) — the whole
            // point is to catch committed-byte drift; a live/re-baked read couples to session state and masks
            // exactly this class (#231/#256 lesson, cited in the #316 class docstring).
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPackAssetGen.PrefabPath);
            Assert.IsNotNull(root,
                $"the committed weapon lineup prefab must exist at {WeaponPackAssetGen.PrefabPath} " +
                "(WeaponPackAssetGen.PrefabPath — the generator's source-of-truth path)");

            // Expected node names = the AddRow naming rule (WeaponPackAssetGen.cs:235) applied to the 15
            // generator FBX consts. Path.GetFileNameWithoutExtension mirrors item.name = ...(path) exactly.
            var expected = ExpectedFbxPaths.Select(Path.GetFileNameWithoutExtension).ToList();
            Assert.AreEqual(15, expected.Count,
                "sanity: the generator contract is 15 weapon nodes (5 wood + 5 stone + 5 iron)");

            // Actual direct-child node names of the committed prefab root (AddRow parents every weapon +
            // StandLight directly under root). Set-membership only — ignore ordering + the non-weapon
            // StandLight child (the ticket default).
            var actual = new HashSet<string>();
            foreach (Transform child in root.transform)
                actual.Add(child.name);

            // PRIMARY (load-bearing) assertion: all 15 generator nodes present. The failure message NAMES the
            // missing nodes — the #304 drift reds here as "wpn_axe_wood_01, wpn_knife_wood_01, ..." absent.
            var missing = expected.Where(n => !actual.Contains(n)).ToList();
            Assert.IsEmpty(missing,
                $"committed {WeaponPackAssetGen.PrefabPath} is MISSING {missing.Count} generator node(s): " +
                $"[{string.Join(", ", missing)}]. This IS the #304 committed-lineup drift (the generator's " +
                "WoodSet/StoneSet/IronSet rows re-bake to 15 nodes, but the committed prefab drifted). CI's " +
                "BootstrapProject.Run re-bake masks it on the build; re-bake (PrepareWeaponPack) to restore.");

            // SECONDARY anchor (completes name-set equality on the WEAPON nodes, ignoring StandLight): no
            // UNEXPECTED wpn_-prefixed node — catches a renamed/extra weapon node the primary check can't see.
            var unexpectedWeapon = actual.Where(n => n.StartsWith("wpn_") && !expected.Contains(n)).ToList();
            Assert.IsEmpty(unexpectedWeapon,
                $"committed {WeaponPackAssetGen.PrefabPath} has UNEXPECTED weapon node(s) not in the generator " +
                $"contract: [{string.Join(", ", unexpectedWeapon)}] — a renamed/extra node is committed drift too.");
        }
    }
}
