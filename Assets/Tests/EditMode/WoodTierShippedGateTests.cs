using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for ticket 86cav8y74 — the two SHIPPED-BUILD capture gaps that let the soak-3
    /// (wood invisible in-hand) and soak-4 (wood-axe whiff) bug classes reach the Sponsor. Source: Tess's
    /// PR #327 comments 5025894753 + 5031539815.
    ///
    /// SCOPE — deliberately NOT duplicating what is already covered. The wood LOGIC layer is well guarded
    /// already (<c>HeldBeltVisualSyncTests.WoodSelectionIndexFor_MapsEachWoodClass_ToItsWoodIndex</c> /
    /// <c>FamilyContract_WoodIndicesNameTheWoodNodes</c> / <c>WoodTierSelected_SelectionTable_MapsToTheWoodMesh_NotEmptyHands</c>,
    /// <c>CommittedLineupDriftGuardTests.CommittedWeaponSetLineupPrefab_CarriesAll15GeneratorNodes_NoDrift</c>,
    /// PlayMode <c>ChopTreePlayModeTests.WoodAxeSelected_ClickInRange_Chops_TheSoak4Regression</c>). What was
    /// missing was the SHIPPED-BUILD layer. These tests guard the things a Unity test CAN check about the new
    /// shipped gates — the preconditions they rest on and the wiring that makes them actually run — so the
    /// gates cannot rot into decoration:
    ///   1. the CI-wiring / flag-handling chain per gate wrapper (the "-verifySwings class": a gate that
    ///      passes by hand and gates nothing, or a wrapper launching a flag no component handles — which
    ///      unity-conventions.md §CI architecture records as HANGING the capture job to its timeout);
    ///   2. the committed lineup prefab's wood nodes actually carry DISTINCT meshes — the precondition that
    ///      makes -verifyHeldWood's mesh-IDENTITY assert meaningful rather than vacuous;
    ///   3. the wood-axe discriminator triple -verifyChop now asserts, so a widening of the STONE-only
    ///      predicate would red here instead of silently defeating the shipped gate's tier proof.
    /// </summary>
    public class WoodTierShippedGateTests
    {
        private static string RepoRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string GateScriptsDir =>
            Path.Combine(RepoRoot, ".github", "workflows", "scripts");

        private static string CiYmlPath =>
            Path.Combine(RepoRoot, ".github", "workflows", "ci.yml");

        // The `-verify<Flag>` a wrapper actually launches the exe with. Matched off the LAUNCH line shape
        // (leading whitespace then the flag), the same discriminator tests/scripts/test_gate_scripts.sh's
        // launch-mode invariant uses — so a flag merely MENTIONED in a comment is not mistaken for the
        // launched one.
        private static string LaunchedFlag(string scriptPath)
        {
            foreach (var line in File.ReadAllLines(scriptPath))
            {
                var m = Regex.Match(line, @"^\s+(-verify[A-Za-z]+)");
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        private static IEnumerable<string> GateScripts()
            => Directory.Exists(GateScriptsDir)
               ? Directory.GetFiles(GateScriptsDir, "verify_*_gate.sh").OrderBy(p => p)
               : Enumerable.Empty<string>();

        private static string[] RuntimeSources()
            => Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts", "Runtime"),
                                  "*.cs", SearchOption.AllDirectories);

        /// <summary>
        /// THE BUG CLASS: a `-verify*` gate wrapper whose flag NO shipped component handles. unity-conventions.md
        /// §CI architecture records the consequence precisely — "a capture verb whose harness component is not
        /// scene-authored parses as a normal launch: the game runs FOREVER with zero captures and exit-never —
        /// indistinguishable from a hang", i.e. the wired-but-unhandled wrapper HANGS the capture job to its
        /// timeout rather than failing legibly. A C#-side flag rename (or a component deletion) with the wrapper
        /// left behind produces exactly that, and no existing test sees it: the shell-side guard in
        /// tests/scripts/test_gate_scripts.sh knows the wrapper is INVOKED, not that its flag is HANDLED.
        /// This closes the C#↔wrapper half of the chain.
        /// </summary>
        [Test]
        public void EveryVerifyGateScript_LaunchesAFlagAShippedComponentHandles()
        {
            var scripts = GateScripts().ToList();
            Assert.IsNotEmpty(scripts,
                $"staleness guard: no verify_*_gate.sh found under {GateScriptsDir} — this test would pass " +
                "vacuously. If the gate wrappers moved, update GateScriptsDir.");

            var sources = RuntimeSources().Select(File.ReadAllText).ToArray();
            var unhandled = new List<string>();
            foreach (var s in scripts)
            {
                string flag = LaunchedFlag(s);
                if (flag == null)
                {
                    unhandled.Add($"{Path.GetFileName(s)} (no -verify* flag on any launch line)");
                    continue;
                }
                // The handling idiom every *VerifyCapture uses: HasArg("-verifyX").
                string needle = "HasArg(\"" + flag + "\")";
                if (!sources.Any(src => src.Contains(needle)))
                    unhandled.Add($"{Path.GetFileName(s)} launches {flag}, but no Assets/Scripts/Runtime source contains {needle}");
            }
            Assert.IsEmpty(unhandled,
                "these CI gate wrappers launch a -verify flag NO shipped component handles, so the exe would " +
                "launch as a normal game and run forever with zero captures — the capture job hangs to its " +
                "timeout instead of failing legibly (unity-conventions.md §CI architecture):\n  " +
                string.Join("\n  ", unhandled));
        }

        /// <summary>
        /// 86cav8y74's own REGRESSION GUARD (the PR #216 Done clause): the new wood-in-hand gate must stay wired
        /// end to end — the wrapper exists, launches `-verifyHeldWood`, `AxeVerifyCapture` handles that flag, and
        /// ci.yml actually INVOKES the wrapper. Delete any link and this reds. Without it, the gate degrades into
        /// exactly what this ticket exists to fix: a capture that passes by hand and gates nothing (the live
        /// `-verifySwings` precedent — zero hits under .github/ at the time of writing).
        /// </summary>
        [Test]
        public void HeldWoodGate_IsWiredEndToEnd_ScriptFlagComponentAndCi()
        {
            string script = Path.Combine(GateScriptsDir, "verify_heldwood_gate.sh");
            Assert.IsTrue(File.Exists(script),
                $"the wood-in-hand gate wrapper must exist at {script} (86cav8y74)");
            Assert.AreEqual("-verifyHeldWood", LaunchedFlag(script),
                "verify_heldwood_gate.sh must launch the exe with -verifyHeldWood");

            Assert.IsTrue(RuntimeSources().Select(File.ReadAllText)
                              .Any(src => src.Contains("HasArg(\"-verifyHeldWood\")")),
                "a shipped component must handle -verifyHeldWood (AxeVerifyCapture.RunHeldWoodVerification) — " +
                "an unhandled flag launches the game normally and hangs the capture job");

            Assert.IsTrue(File.Exists(CiYmlPath), $"ci.yml must exist at {CiYmlPath}");
            string ci = File.ReadAllText(CiYmlPath);
            Assert.IsTrue(ci.Contains("scripts/verify_heldwood_gate.sh"),
                "ci.yml must INVOKE verify_heldwood_gate.sh — a gate CI never runs gates nothing (the " +
                "-verifySwings class this ticket exists to stop repeating)");
        }

        /// <summary>
        /// The PRECONDITION that makes -verifyHeldWood's mesh-IDENTITY assert non-vacuous. That gate asserts the
        /// held mesh IS the lineup node for the selected wood index; if a wood node carried a NULL mesh
        /// (ApplyCurrent then silently falls back to the AXE mesh — the "stone axe in hand where a wood sword
        /// should be" percept) or two nodes SHARED one mesh, the identity check would be weaker than it reads.
        /// CommittedLineupDriftGuardTests pins that the 15 node NAMES exist; it says nothing about their meshes —
        /// this covers that half.
        ///
        /// ⚠ THE LINEUP PREFAB IS BOOTSTRAP-AUTHORED, so this test follows the BootstrapPrecondition idiom
        /// (ticket 86cacyg63 / PR #119). Measured on this branch's tree at origin/main `fee2604`: the COMMITTED
        /// Assets/Resources/WeaponSetLineup.prefab carries only the 10 stone+iron nodes — all five `wpn_*_wood_01`
        /// nodes are ABSENT (`grep -a -o "wpn_[a-z_0-9]*" … | sort -u` returns 10 names, none containing `_wood_`).
        /// That is the still-open 86catwzhy committed-drift. `BootstrapProject.Run` calls
        /// `WeaponPackAssetGen.PrepareWeaponPack()` (BootstrapProject.cs:94), and ci.yml bootstraps BEFORE both
        /// EditMode and BuildWindows — so CI (and the shipped exe) always sees the re-baked 15-node prefab, while a
        /// BARE local EditMode run reads the stale committed 10. Hence: Inconclusive-with-a-hint on the
        /// no-bootstrap path (an actionable skip, not a red that reads as a regression), real asserts on the
        /// post-bootstrap path CI always takes. Masking-safe per the #119 property: if the GENERATOR ever stopped
        /// emitting a wood node, CI's post-bootstrap tree would hit the same Inconclusive, and Inconclusive is a
        /// HARD CI red (parse_test_results.py requires inconclusive == 0) — so this cannot hide a regression.
        /// </summary>
        [Test]
        public void CommittedLineup_EveryWoodNode_CarriesItsOwnMesh_DistinctFromTheStoneAxe()
        {
            // Read the COMMITTED prefab off disk via the GENERATOR's own path const (never a re-baked instance) —
            // same discipline as CommittedLineupDriftGuardTests. Cross-check it against the RUNTIME resource path
            // the shipped gate loads, so a divergence between the two cannot hide here.
            Assert.AreEqual("Assets/Resources/" + HeldWeaponCycleDebug.LineupResourcePath + ".prefab",
                WeaponPackAssetGen.PrefabPath,
                "the generator's prefab path and the runtime Resources path (HeldWeaponCycleDebug." +
                "LineupResourcePath) must name the SAME asset, or this test reads a different prefab than the " +
                "shipped -verifyHeldWood gate resolves");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPackAssetGen.PrefabPath);
            Assert.IsNotNull(prefab,
                $"the committed weapon lineup prefab must exist at {WeaponPackAssetGen.PrefabPath} — " +
                "-verifyHeldWood resolves every expected wood mesh from it in the shipped build");

            var byNode = new Dictionary<string, Mesh>();
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf != null && mf.sharedMesh != null && !byNode.ContainsKey(mf.name))
                    byNode[mf.name] = mf.sharedMesh;

            int[] woodIndices =
            {
                HeldWeaponCycleDebug.AxeWoodFamilyIndex,
                HeldWeaponCycleDebug.DaggerWoodFamilyIndex,
                HeldWeaponCycleDebug.SwordWoodFamilyIndex,
                HeldWeaponCycleDebug.SpearWoodFamilyIndex,
                HeldWeaponCycleDebug.PickaxeWoodFamilyIndex,
            };
            string stoneAxeNode = HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.AxeFamilyIndex];
            byNode.TryGetValue(stoneAxeNode, out Mesh stoneAxeMesh);

            var seen = new Dictionary<Mesh, string>();
            foreach (int i in woodIndices)
            {
                string node = HeldWeaponCycleDebug.WeaponNodeNames[i];
                Assert.IsTrue(node.Contains("_wood_"),
                    $"index {i} must name a WOOD node (got '{node}') — a WeaponNodeNames reorder would point the " +
                    "shipped wood gate at the wrong tier");
                // Bootstrap-precondition gate (see the summary). A tailored message rather than
                // BootstrapPrecondition.Message because that text names Boot.unity scene-presence assets; the
                // missing thing here is a bootstrap-authored PREFAB node. Same idiom, same masking-safety.
                byNode.TryGetValue(node, out Mesh m);
                if (m == null)
                    Assert.Inconclusive(
                        $"Lineup prefab not re-baked — no MeshFilter node '{node}' with a mesh at " +
                        $"{WeaponPackAssetGen.PrefabPath}. Run FarHorizon.EditorTools.BootstrapProject.Run first " +
                        "(it calls WeaponPackAssetGen.PrepareWeaponPack, BootstrapProject.cs:94); ci.yml bootstraps " +
                        "before EditMode, a bare local '-runTests -testPlatform EditMode' does not, and the " +
                        "COMMITTED prefab is still short the 5 wood nodes (open ticket 86catwzhy). If this fires " +
                        "in CI it is NOT the local artifact — the generator stopped emitting the node, and the " +
                        "shipped -verifyHeldWood gate will show a stone axe where this wood weapon belongs " +
                        "(ApplyCurrent falls back to the axe mesh). See unity-conventions.md \"Run " +
                        "BootstrapProject.Run BEFORE any LOCAL EditMode run\" + BootstrapPrecondition (86cacyg63).");
                if (stoneAxeMesh != null)
                    Assert.AreNotSame(stoneAxeMesh, m,
                        $"'{node}' must not share the STONE AXE's mesh — -verifyHeldWood's not-the-axe-fallback " +
                        "assert would be satisfiable by the very defect it guards");
                Assert.IsFalse(seen.ContainsKey(m),
                    $"'{node}' shares its mesh with '{(seen.TryGetValue(m, out var prev) ? prev : "?")}' — two wood " +
                    "tools rendering the same mesh cannot be told apart by an identity assert");
                seen[m] = node;
            }
        }

        /// <summary>
        /// The DISCRIMINATOR TRIPLE the shipped -verifyChop wood phase asserts before chopping: with a WOOD axe
        /// selected, <c>IsAxeWoodSelectedInBelt</c> and <c>IsAnyAxeSelectedInBelt</c> are true while the STONE-only
        /// <c>IsAxeSelectedInBelt</c> is FALSE. The false term is the load-bearing one — it is what proves the
        /// shipped chop happened on the WOOD tier. If anyone ever widened <c>IsAxeSelectedInBelt</c> to include the
        /// wood id (a plausible "simplification"), the shipped gate would keep passing while its tier proof
        /// silently evaporated; this reds instead. Complements — does not duplicate —
        /// ChopTreePlayModeTests.WoodAxeSelected_ClickInRange_Chops_TheSoak4Regression, which proves the chop
        /// LANDS; this pins the predicates the shipped gate reads to know WHICH TIER landed it.
        /// </summary>
        [Test]
        public void WoodAxeSelected_DiscriminatorTriple_HoldsForTheShippedChopGate()
        {
            var go = new GameObject("Inventory");
            try
            {
                var inv = go.AddComponent<Inventory>();
                var slot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.AxeWoodId));
                Assert.IsTrue(slot.HasValue, "wood axe acquired onto the belt (a belt-eligible Tool)");
                inv.Model.SelectBelt(slot.Value.Index);

                Assert.IsTrue(inv.IsAxeWoodSelectedInBelt, "the WOOD axe is the selected belt item");
                Assert.IsTrue(inv.IsAnyAxeSelectedInBelt,
                    "the all-tier chop gate must see an axe (ChopTree.ShouldChopOnClick's axeSelected term — the " +
                    "soak-4 regression was this reading stone-only)");
                Assert.IsFalse(inv.IsAxeSelectedInBelt,
                    "the STONE-only predicate must stay FALSE with a wood axe selected. -verifyChop asserts this " +
                    "exact term to prove its scatter chop ran on the WOOD tier; widening IsAxeSelectedInBelt to " +
                    "cover wood would leave that gate green with no tier proof left in it.");

                // And the negative direction: the STONE axe must NOT satisfy the wood predicate, so the two
                // tiers stay distinguishable in both directions.
                Assert.IsTrue(inv.PickUpAxe(), "stone axe acquired onto the belt too");
                int stoneSlot = -1;
                var belt = inv.Model.BeltSlots;
                for (int i = 0; i < belt.Count; i++)
                    if (!belt[i].IsEmpty && belt[i].Def != null && belt[i].Def.Id == ItemCatalog.AxeId) stoneSlot = i;
                Assert.GreaterOrEqual(stoneSlot, 0, "the stone axe landed on the belt");
                inv.Model.SelectBelt(stoneSlot);
                Assert.IsTrue(inv.IsAxeSelectedInBelt, "stone axe selected -> the stone predicate is true");
                Assert.IsFalse(inv.IsAxeWoodSelectedInBelt,
                    "stone axe selected -> the WOOD predicate must be FALSE (the tiers must be distinguishable " +
                    "both ways, or the shipped gate's triple could be satisfied by the wrong tool)");
                Assert.IsTrue(inv.IsAnyAxeSelectedInBelt, "either tier satisfies the all-tier chop gate");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
