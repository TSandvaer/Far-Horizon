using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;
using FarHorizon.Combat;

namespace FarHorizon.EditTests
{
    using Consumer = ClickGateDiagnostic.ClickConsumer;

    /// <summary>
    /// EditMode coverage for the LEFT-CLICK VERB-GATE DIAGNOSTIC (ticket 86caffwv5, PR #327 — the
    /// [[soak-fail-test-pass-instrument-runtime]] instrument for the live mine failure). Round-5 verified the mine
    /// LOGIC via teleport-gates, but the Sponsor's real-input path still fails; this pins the diagnostic's two
    /// testable seams headlessly so a regression can't silently mis-report the live click:
    ///
    ///   1. THE PURE CLASSIFIER — <see cref="ClickGateDiagnostic.ClassifyClick"/> must reproduce the SAME precedence
    ///      the live Update chain produces (guards suppress ALL → verb-wins-over-whiff chop&gt;boulder&gt;ore → melee
    ///      target/whiff). Asserted as a guard TRUTH-TABLE (the ShouldChopOnClick / ShouldSwingOnClick style) so a
    ///      wrong WIN read (which would send the diagnosis off in the wrong direction) reds here.
    ///   2. THE BELT-OVERFLOW HYPOTHESIS (round-5 hyp b) — the all-wood 5-tool craft + the starting axe = 6 belt-
    ///      eligible tools vs the default 5 belt slots (<see cref="InventoryModel.AddToolToBelt"/> overflow, model
    ///      lines 143-166). This documents WHAT overflows WHERE + proves a tool CAN land in the pack UNSELECTABLE —
    ///      i.e. it can never be the selected belt item, so the verb that gates on it silently never fires. (A
    ///      diagnostic-round documentation test — NOT a fix; the live confirmation is the [ClickGateDiag] soak line.)
    ///   3. SCENE PRESENCE — the diagnostic must SERIALIZE onto Boot (the component-in-source-but-not-in-scene trap)
    ///      or the [ClickGateDiag] line never ships in the soak and the whole round is moot. (CI bootstraps before
    ///      EditMode; a bare LOCAL run against a stale committed Boot.unity may red here until the scene is
    ///      regenerated — unity-conventions.md §"Run BootstrapProject.Run BEFORE any LOCAL EditMode run".)
    /// </summary>
    public class ClickGateDiagnosticTests
    {
        // ===================================================================================================
        // 1 — the pure click-consumer classifier truth-table
        // ===================================================================================================

        // Convenience: classify with all guards OPEN (the common "in the world" case).
        private static Consumer Classify(bool chop, bool boulder, bool ore, bool weapon, bool tgt)
            => ClickGateDiagnostic.ClassifyClick(chop, boulder, ore, weapon, tgt, false, false, false);

        [Test]
        public void Classify_ChopClaims_WinsOverBoulderOreAndMelee()
        {
            // Verb-wins-over-whiff + chop is first in precedence (only one tool is ever selected in play, so at most
            // one verb claims; the order is the deterministic tie-break the live VerbClaimsClick applies).
            Assert.AreEqual(Consumer.ChopTree, Classify(true, false, false, true, true));
            Assert.AreEqual(Consumer.ChopTree, Classify(true, true, true, true, true), "chop wins the (unreachable-in-play) all-claim tie");
        }

        [Test]
        public void Classify_BoulderThenOre_Precedence()
        {
            Assert.AreEqual(Consumer.MineBoulder, Classify(false, true, false, false, false));
            Assert.AreEqual(Consumer.MineBoulder, Classify(false, true, true, false, false), "boulder before ore");
            Assert.AreEqual(Consumer.MineOre, Classify(false, false, true, false, false));
        }

        [Test]
        public void Classify_NoVerb_WeaponSelected_TargetInReach_LandsMeleeTarget()
        {
            Assert.AreEqual(Consumer.MeleeAttackTarget, Classify(false, false, false, true, true));
        }

        [Test]
        public void Classify_NoVerb_WeaponSelected_NoTarget_Whiffs()
        {
            // The soak-2 behaviour: one click = one swing, TARGET OR NOT — a click at empty air whiffs (a valid swing).
            Assert.AreEqual(Consumer.MeleeAttackWhiff, Classify(false, false, false, true, false));
        }

        [Test]
        public void Classify_NothingSelected_IsNone()
        {
            Assert.AreEqual(Consumer.None, Classify(false, false, false, false, false));
            Assert.AreEqual(Consumer.None, Classify(false, false, false, false, true), "no weapon selected → no swing even with a target");
        }

        [Test]
        public void Classify_AnyGuard_SuppressesEveryConsumer()
        {
            // A guarded click fires NOTHING — not even the chop (each consumer re-applies the SAME three guards, so a
            // click over the belt / with a panel open / mid-orbit-drag consumes nothing). This is the load-bearing
            // model of hypothesis (a): a boulder in range + pickaxe selected is STILL suppressed by overUI.
            Assert.AreEqual(Consumer.None, ClickGateDiagnostic.ClassifyClick(true, true, true, true, true, true, false, false),
                "panel open suppresses all");
            Assert.AreEqual(Consumer.None, ClickGateDiagnostic.ClassifyClick(true, true, true, true, true, false, true, false),
                "pointer-over-UI suppresses all (the over-belt-strip case)");
            Assert.AreEqual(Consumer.None, ClickGateDiagnostic.ClassifyClick(true, true, true, true, true, false, false, true),
                "RMB orbit-drag suppresses all");
        }

        [Test]
        public void VerbGateDiag_TargetInRange_AndWouldClaim_ComposeCorrectly()
        {
            // No candidate target exists → NearestDist < 0 → never in range, never claims (even with the tool selected).
            var none = new VerbGateDiag { ToolSelected = true, NearestDist = -1f, Range = 2.4f };
            Assert.IsFalse(none.TargetInRange, "no candidate → not in range");
            Assert.IsFalse(none.WouldClaim, "no candidate → no claim");

            // Tool selected + a target within range → claims. (hyp a: the boulder WAS reachable.)
            var inRange = new VerbGateDiag { ToolSelected = true, NearestDist = 1.2f, Range = 2.4f };
            Assert.IsTrue(inRange.TargetInRange);
            Assert.IsTrue(inRange.WouldClaim);

            // Target just out of reach → in-range false → no claim (the "walk closer" case, distinct from a UI eat).
            var far = new VerbGateDiag { ToolSelected = true, NearestDist = 3.0f, Range = 2.4f };
            Assert.IsFalse(far.TargetInRange);
            Assert.IsFalse(far.WouldClaim);

            // Target in range but the WRONG tool selected → no claim (hyp b: pickaxe not the selected belt item).
            var wrongTool = new VerbGateDiag { ToolSelected = false, NearestDist = 1.0f, Range = 2.4f };
            Assert.IsTrue(wrongTool.TargetInRange, "a boulder is reachable");
            Assert.IsFalse(wrongTool.WouldClaim, "…but the pickaxe is not selected → no claim");
        }

        // 86cav8xu8 — CROSS-CHECK ClassifyClick against the REAL MeleeAttack arbitration, not itself. ClassifyClick
        // RE-IMPLEMENTS the click precedence; if it drifts from MeleeAttack's actual rule the instrument LIES (the
        // soak-fail-test-pass-instrument trap). Over the full 256-combo guard×claim×weapon×target truth-table, the
        // diagnostic's "a verb won" is pinned to MeleeAttack.AnyVerbClaims (under the shared guards) and its "melee
        // won" to MeleeAttack.ShouldSwingOnClick — both PRODUCTION statics the live Update chain uses, so a change to
        // the real suppression rule (incl. the chop→boulder→ore verb order) reds here instead of silently mis-labeling.
        [Test]
        public void ClassifyClick_CrossChecksTheRealMeleeAttackArbitration_NotItself()
        {
            for (int mask = 0; mask < 256; mask++)
            {
                bool chop   = (mask & 1) != 0, boulder = (mask & 2) != 0, ore = (mask & 4) != 0;
                bool weapon = (mask & 8) != 0, tgt = (mask & 16) != 0;
                bool panel  = (mask & 32) != 0, overUI = (mask & 64) != 0, rmb = (mask & 128) != 0;

                var win = ClickGateDiagnostic.ClassifyClick(chop, boulder, ore, weapon, tgt, panel, overUI, rmb);
                bool guarded = panel || overUI || rmb;
                bool anyVerb = MeleeAttack.AnyVerbClaims(chop, boulder, ore); // the REAL suppression predicate
                bool diagVerbWon  = win == Consumer.ChopTree || win == Consumer.MineBoulder || win == Consumer.MineOre;
                bool diagMeleeWon = win == Consumer.MeleeAttackTarget || win == Consumer.MeleeAttackWhiff;

                Assert.AreEqual(!guarded && anyVerb, diagVerbWon,
                    "verb-win must match MeleeAttack.AnyVerbClaims under the shared guards (mask " + mask + ")");
                Assert.AreEqual(MeleeAttack.ShouldSwingOnClick(weapon, anyVerb, panel, overUI, rmb), diagMeleeWon,
                    "melee-win must match the REAL MeleeAttack.ShouldSwingOnClick gate (mask " + mask + ")");
            }
        }

        // ===================================================================================================
        // 1b — 86cav8xu8: the ACCESSOR-vs-RESOLVER equivalence guard (instrument-of-record integrity)
        // ===================================================================================================
        // Each verb's ClickGateDiag() reports TargetInRange from its distance ACCESSOR (NearestXDistance ≤ Range);
        // the live click consumes via its RESOLVER (ResolveNearestX WITHIN Range). If the two drift (magnitude↔
        // sqrMagnitude, ≤↔<, a different node filter) the diagnostic lies about reachability — the soak-fail trap.
        // With the matching tool SELECTED, WouldClaimClick() == (ResolveNearest != null), so asserting
        // WouldClaimClick() == ClickGateDiag().TargetInRange across the range boundary pins the equivalence against
        // the verb's OWN resolver (ChopTree.cs / MineBoulder.cs / MineOre.cs NearestXDistance).

        // Move the player to inside / just-outside / far of a node at the origin and assert the diagnostic's
        // TargetInRange tracks the verb's real resolver (WouldClaimClick, tool selected) at every distance.
        private static void AssertDiagTargetInRangeTracksResolver(float range, Transform node, Transform player,
            System.Func<bool> wouldClaim, System.Func<bool> diagInRange, string label)
        {
            node.position = Vector3.zero;

            player.position = new Vector3(range * 0.5f, 0f, 0f);   // comfortably inside
            Assert.IsTrue(diagInRange(), label + ": a node at 0.5×range must read IN range");
            Assert.AreEqual(wouldClaim(), diagInRange(), label + ": inside — diag TargetInRange must match the resolver");

            player.position = new Vector3(range * 1.25f, 0f, 0f);  // just outside
            Assert.IsFalse(diagInRange(), label + ": a node at 1.25×range must read OUT of range");
            Assert.AreEqual(wouldClaim(), diagInRange(), label + ": outside — diag must match the resolver");

            player.position = new Vector3(range * 6f, 0f, 0f);     // far
            Assert.AreEqual(wouldClaim(), diagInRange(), label + ": far — diag must match the resolver");
        }

        [Test]
        public void MineBoulder_DiagTargetInRange_MatchesResolver_AcrossRangeBoundary()
        {
            var invGo = new GameObject("Inv"); var playerGo = new GameObject("Player");
            var rootGo = new GameObject("Boulders"); var mineGo = new GameObject("MineBoulder");
            try
            {
                var inv = invGo.AddComponent<Inventory>();
                var slot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.PickaxeWoodId));
                inv.Model.SelectBelt(slot.Value.Index);
                var node = new GameObject(MineBoulder.BoulderNodeName);
                node.transform.SetParent(rootGo.transform, false);
                var mine = mineGo.AddComponent<MineBoulder>();
                mine.inventory = inv; mine.player = playerGo.transform; mine.boulderRoot = rootGo.transform;
                mine.mineRadius = 2.4f;
                mine.InitializePoolForTest();
                AssertDiagTargetInRangeTracksResolver(mine.mineRadius, node.transform, playerGo.transform,
                    () => mine.WouldClaimClick(), () => mine.ClickGateDiag().TargetInRange, "boulder");
            }
            finally
            {
                Object.DestroyImmediate(mineGo); Object.DestroyImmediate(rootGo);
                Object.DestroyImmediate(playerGo); Object.DestroyImmediate(invGo);
            }
        }

        [Test]
        public void MineOre_DiagTargetInRange_MatchesResolver_AcrossRangeBoundary()
        {
            var invGo = new GameObject("Inv"); var playerGo = new GameObject("Player");
            var rootGo = new GameObject("OreNodes"); var mineGo = new GameObject("MineOre");
            try
            {
                var inv = invGo.AddComponent<Inventory>();
                var slot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.PickaxeStoneId)); // ore needs stone/iron
                inv.Model.SelectBelt(slot.Value.Index);
                var node = new GameObject(MineOre.OreNodeName);
                node.transform.SetParent(rootGo.transform, false);
                var mine = mineGo.AddComponent<MineOre>();
                mine.inventory = inv; mine.player = playerGo.transform; mine.nodeRoot = rootGo.transform;
                mine.activeNodeCount = 1; // enable the one node (Awake's preset-seed never fires in EditMode)
                mine.InitializePoolForTest();
                AssertDiagTargetInRangeTracksResolver(mine.mineRadius, node.transform, playerGo.transform,
                    () => mine.WouldClaimClick(), () => mine.ClickGateDiag().TargetInRange, "ore");
            }
            finally
            {
                Object.DestroyImmediate(mineGo); Object.DestroyImmediate(rootGo);
                Object.DestroyImmediate(playerGo); Object.DestroyImmediate(invGo);
            }
        }

        [Test]
        public void ChopTree_DiagTargetInRange_MatchesResolver_AcrossRangeBoundary()
        {
            var invGo = new GameObject("Inv"); var playerGo = new GameObject("Player");
            var treeGo = new GameObject("DemoTree"); var chopGo = new GameObject("ChopTree");
            try
            {
                var inv = invGo.AddComponent<Inventory>();
                var slot = inv.Model.AddToolToBelt(inv.Catalog.ById(ItemCatalog.AxeId));
                inv.Model.SelectBelt(slot.Value.Index);
                var chop = chopGo.AddComponent<ChopTree>();
                chop.inventory = inv; chop.player = playerGo.transform;
                chop.visual = treeGo.transform;          // instance-0 (demo tree) is the visual's position
                chop.chopRadius = 2.2f;
                chop.RegisterDemoTreeForTest();
                AssertDiagTargetInRangeTracksResolver(chop.chopRadius, treeGo.transform, playerGo.transform,
                    () => chop.WouldClaimClick(), () => chop.ClickGateDiag().TargetInRange, "chop");
            }
            finally
            {
                Object.DestroyImmediate(chopGo); Object.DestroyImmediate(treeGo);
                Object.DestroyImmediate(playerGo); Object.DestroyImmediate(invGo);
            }
        }

        // ===================================================================================================
        // 2 — the belt-overflow hypothesis (round-5 hyp b): 6 tools vs a 5-slot belt
        // ===================================================================================================

        [Test]
        public void SixTools_FiveBeltSlots_SixthOverflowsToPack_AndIsUnselectable()
        {
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            catalog.BuildDefaults();
            try
            {
                var model = new InventoryModel(20, InventoryModel.DefaultBeltSlots); // 20 pack, 5 belt (the shipped default)

                // The starting stone axe + the 5 all-wood craftables = SIX belt-eligible tools. Add the pickaxe LAST
                // so it is the one that overflows (the round-5 hyp: the pickaxe lands unselectable in the pack → the
                // mine verb, which gates on the SELECTED belt pickaxe, can never fire even standing on a boulder).
                var startingAxe = catalog.ById(ItemCatalog.AxeId);         // belt slot 0
                var axeWood     = catalog.ById(ItemCatalog.AxeWoodId);     // belt slot 1
                var spearWood   = catalog.ById(ItemCatalog.SpearWoodId);   // belt slot 2
                var daggerWood  = catalog.ById(ItemCatalog.DaggerWoodId);  // belt slot 3
                var swordWood   = catalog.ById(ItemCatalog.SwordWoodId);   // belt slot 4  ← belt now FULL
                var pickaxeWood = catalog.ById(ItemCatalog.PickaxeWoodId); // OVERFLOW → the pack

                Assert.IsNotNull(pickaxeWood, "the wood pickaxe id must resolve in the catalog");

                foreach (var t in new[] { startingAxe, axeWood, spearWood, daggerWood, swordWood })
                {
                    var placed = model.AddToolToBelt(t);
                    Assert.IsTrue(placed.HasValue && placed.Value.Area == SlotArea.Belt,
                        t.Id + " should fill a belt slot (belt not yet full)");
                }

                // The SIXTH tool: belt full → AddToolToBelt falls back to the pack.
                var overflow = model.AddToolToBelt(pickaxeWood);
                Assert.IsTrue(overflow.HasValue, "the 6th tool is not silently dropped");
                Assert.AreEqual(SlotArea.Inventory, overflow.Value.Area,
                    "the 6th belt-eligible tool OVERFLOWS to the pack (belt full, 5 slots) — NOT onto the belt");

                // Count-conservation: 5 tools on the belt, exactly 1 in the pack.
                int beltTools = 0, packTools = 0;
                for (int i = 0; i < model.BeltSlots.Count; i++) if (!model.BeltSlots[i].IsEmpty) beltTools++;
                for (int i = 0; i < model.InventorySlots.Count; i++) if (!model.InventorySlots[i].IsEmpty) packTools++;
                Assert.AreEqual(5, beltTools, "the belt is full (5 tools)");
                Assert.AreEqual(1, packTools, "exactly one tool overflowed to the pack");

                // THE LOAD-BEARING CLAIM (hyp b): the overflowed pickaxe is in the PACK, so it can NEVER be the
                // selected belt item — no belt-slot selection makes IsSelectedBeltItem(pickaxe_wood) true. The mine
                // verb gates on IsBoulderPickaxeSelected (= a SELECTED BELT pickaxe id) → mining silently never fires
                // even standing on a boulder. A tool in the pack is unusable by any verb that reads the belt selection.
                for (int slot = 0; slot < model.BeltSlots.Count; slot++)
                {
                    model.SelectBelt(slot);
                    Assert.IsFalse(model.IsSelectedBeltItem(ItemCatalog.PickaxeWoodId),
                        "the overflowed pickaxe is in the PACK — selecting belt slot " + slot +
                        " can never make it the selected belt item (so MineBoulder.IsBoulderPickaxeSelected stays false)");
                }
            }
            finally
            {
                foreach (var d in catalog.All) if (d != null) Object.DestroyImmediate(d);
                Object.DestroyImmediate(catalog);
            }
        }

        // ===================================================================================================
        // 3 — scene presence (the diagnostic must ship in the soak)
        // ===================================================================================================

        [Test]
        public void BootScene_HasClickGateDiagnostic_OnBoot()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity", OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            ClickGateDiagnostic diag = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                diag = root.GetComponentInChildren<ClickGateDiagnostic>(true);
                if (diag != null) break;
            }
            Assert.IsNotNull(diag,
                "the Boot scene must carry the ClickGateDiagnostic (86caffwv5 PR #327) — a missing component ships the " +
                "[ClickGateDiag] line inert, so the live mine diagnostic never appears in the soak (the component-in-" +
                "source-but-not-in-scene trap; this scene-presence assert is the authoritative reader for the binary scene).");
        }
    }
}
