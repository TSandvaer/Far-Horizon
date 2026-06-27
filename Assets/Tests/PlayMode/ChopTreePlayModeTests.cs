using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.AI;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the chop mechanic (ticket 86caa4c5c — the gameplay-wave successor to the U2-3
    /// thin chop 86ca8bdd8). Proves, driving the player transform directly + the chop-click via the
    /// programmatic RequestChopClick() seam (CHANGE 1 — the chop is now per LEFT-CLICK, not proximity-auto;
    /// a headless run can't inject a real mouse button) to isolate ChopTree's range + gate logic from
    /// pathfinding/input:
    ///
    ///   AC1 (the TRIGGER, CHANGE 1) — the chop is INITIATED by a left-click, NOT proximity-auto:
    ///          • at the tree WITHOUT a click → NO chop (standing at the tree no longer auto-chops);
    ///          • ONE click in range → exactly ONE chop (one strike per click).
    ///   AC1 (the load-bearing gate) — a click still requires the axe to be the SELECTED belt item
    ///        (Inventory.IsAxeSelectedInBelt), NOT merely OWNED. Three cases:
    ///          • axe selected + click at the tree → chops (positive);
    ///          • NO axe at all + click at the tree → nothing (negative, the "chopping without the axe does nothing");
    ///          • axe OWNED but a different belt slot selected → nothing (the NEW selection-gate case — this
    ///            is what supersedes the old HasAxe gate; HasAxe would have wrongly chopped here).
    ///   AC1 (swing) — each landed chop fires the Mixamo melee swing (CastawayCharacter.TriggerChop latches
    ///        ChopTriggered; the Animator can't be observed headlessly — deltaTime≈0).
    ///   AC2 — chopping yields WOOD (ItemCatalog.WoodId) into the inventory; it stacks/ticks per chop-click.
    ///   AC3 — after chopsToFell click-chops the tree FELLS to a stump, then REGROWS after the (tweakable)
    ///        timer into a standing, choppable tree (chop count reset). The stump persists through the window.
    ///   AC6 — these tests ARE the AC6 regression guard (click → wood; deplete → stump → regrow).
    ///
    /// The pure CHANGE-1 guard truth-table (no chop while a panel is open / pointer over UI / RMB held) is
    /// unit-asserted headlessly in EditMode (ChopClickGateTests over ChopTree.ShouldChopOnClick); these
    /// PlayMode tests cover the live click-drives-a-chop loop.
    ///
    /// Headless time discipline (unity-conventions.md / playbook E6/E7): all waits are real Time.time /
    /// WaitForSeconds windows — NEVER WaitForEndOfFrame (does not fire in -batchmode), NEVER per-frame
    /// deltaTime assertions (deltaTime ≈ 0 headless). The swing + regrow are anchored on Time.time.
    /// </summary>
    public class ChopTreePlayModeTests
    {
        private GameObject _invGo;
        private GameObject _playerGo;
        private GameObject _treeGo;
        private GameObject _charGo;
        private Inventory _inv;
        private ChopTree _tree;
        private CastawayCharacter _character;

        [SetUp]
        public void SetUp()
        {
            // CastawayCharacter.Awake logs "modelPrefab not wired" in a bare rig (no scene FBX); ignore it — these
            // tests only exercise the chop MECHANIC + the TriggerChop latch, not the rendered model.
            LogAssert.ignoreFailingMessages = true;

            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the tree

            // A castaway so the swing-fires assertion exercises the real TriggerChop seam (change-(b)). The
            // headless Animator never ticks (deltaTime≈0), so the swing is proven via ChopTriggered, not a clip.
            _charGo = new GameObject("CastawayAvatar");
            _character = _charGo.AddComponent<CastawayCharacter>();

            _treeGo = new GameObject("ChopTree");
            _treeGo.transform.position = Vector3.zero;
            _tree = _treeGo.AddComponent<ChopTree>();
            _tree.inventory = _inv;
            _tree.player = _playerGo.transform;
            _tree.visual = _treeGo.transform;
            _tree.character = _character;
            _tree.chopRadius = 2.2f;
            _tree.woodPerChop = 1;
            _tree.chopsToFell = 3;
            _tree.chopInterval = 0f;          // no click cooldown in the test (each requested click chops)
            _tree.inventoryUI = null;         // no UI rig → the over-UI guard is skipped (modal/RMB are false)
            _tree.regrowthMinSeconds = 0.4f;  // short so the regrow window is testable in headless wall-clock
            _tree.regrowthMaxSeconds = 0.6f;
            _tree.regrowSeed = 12345;         // deterministic regrow roll
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_treeGo);
            Object.Destroy(_charGo);
            LogAssert.ignoreFailingMessages = false;
        }

        // Place the axe in the belt AND make it the selected belt item (CraftAxe puts it in slot 0, which is
        // the default selected slot — so on a fresh inventory this is already true; assert to be explicit).
        private void SelectAxe()
        {
            _inv.CraftAxe();
            Assert.IsTrue(_inv.IsAxeSelectedInBelt, "precondition: the axe is the SELECTED belt item");
        }

        private void StandAtTree() => _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);

        // CHANGE 1 — request ONE chop-click (the input-independent seam) then advance a frame so ChopTree.Update
        // consumes the latch + lands (or rejects) the chop. UiInputGate is forced closed (no modal panel) so the
        // gate decision in a headless test is range + axe-selected only.
        private IEnumerator ClickChop()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked); // ensure no modal-panel gate in the test
            _tree.RequestChopClick();
            yield return null;
        }
        private bool _gateTracked;

        // === CHANGE 1 — at the tree WITHOUT a click does NOTHING (the proximity-auto trigger is REMOVED) ===
        [UnityTest]
        public IEnumerator AtTreeWithSelectedAxe_NoClick_DoesNotChop()
        {
            SelectAxe();
            StandAtTree();

            // Stand in range, axe selected, but NEVER request a click — the old proximity-auto would have
            // chopped here; the new left-click trigger must NOT.
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            Assert.AreEqual(0, _inv.WoodCount, "standing at the tree WITHOUT a click must not chop (CHANGE 1)");
            Assert.AreEqual(0, _tree.Chops, "no click -> no chops land (proximity-auto is removed)");
            Assert.IsFalse(_tree.IsFelled, "no click -> the tree is never felled");
        }

        // === CHANGE 1 — ONE click in range with the selected axe lands EXACTLY ONE chop (one strike/click) ===
        [UnityTest]
        public IEnumerator OneClickInRange_LandsExactlyOneChop()
        {
            SelectAxe();
            StandAtTree();
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood yet");

            yield return ClickChop();

            Assert.AreEqual(1, _tree.Chops, "ONE click -> exactly ONE chop (one strike per click)");
            Assert.AreEqual(_tree.woodPerChop, _inv.WoodCount, "one chop -> woodPerChop wood");

            // A second frame WITHOUT a new click does not chop again (the click does not repeat).
            yield return null;
            Assert.AreEqual(1, _tree.Chops, "no further chop without a NEW click (one chop per click)");
        }

        // === AC1 negative — NO axe at all + a click at the tree does nothing (the success-test's classic case) ===
        [UnityTest]
        public IEnumerator ClickAtTreeWithoutAxe_YieldsNoWood_NeverFells()
        {
            Assert.IsFalse(_inv.HasAxe, "precondition: no axe owned");
            Assert.IsFalse(_inv.IsAxeSelectedInBelt, "precondition: no axe selected");

            StandAtTree();
            for (int i = 0; i < 5; i++) yield return ClickChop();

            Assert.AreEqual(0, _inv.WoodCount, "no axe -> no wood, even clicking at the tree");
            Assert.AreEqual(0, _tree.Chops, "no axe -> no chops land");
            Assert.IsFalse(_tree.IsFelled, "no axe -> the tree is never felled");
        }

        // === AC1 negative (the NEW selection gate) — axe OWNED but NOT the selected belt item does nothing.
        //     This is the case the old HasAxe gate got WRONG (it would have chopped). ===
        [UnityTest]
        public IEnumerator ClickAtTreeWithAxeOwnedButNotSelected_YieldsNoWood_NeverFells()
        {
            _inv.CraftAxe();
            Assert.IsTrue(_inv.HasAxe, "precondition: axe is OWNED");
            // Deselect the axe by selecting a different (empty) belt slot.
            _inv.Model.SelectBelt(1);
            Assert.IsFalse(_inv.IsAxeSelectedInBelt, "precondition: axe owned but NOT the selected belt item");

            StandAtTree();
            for (int i = 0; i < 5; i++) yield return ClickChop();

            Assert.AreEqual(0, _inv.WoodCount, "axe not SELECTED -> no wood (the selection gate, not just owned)");
            Assert.AreEqual(0, _tree.Chops, "axe not selected -> no chops land");
            Assert.IsFalse(_tree.IsFelled, "axe not selected -> the tree is never felled");
        }

        // === AC1 positive + AC2 — with the axe SELECTED, clicking in range yields wood and (after enough
        //     clicks) fells the tree ===
        [UnityTest]
        public IEnumerator ClickAtTreeWithSelectedAxe_YieldsWood_AndFells()
        {
            SelectAxe();
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood yet");

            StandAtTree();
            // One click per chop needed to fell (chopsToFell clicks).
            for (int i = 0; i < _tree.chopsToFell; i++) yield return ClickChop();

            Assert.IsTrue(_tree.IsFelled, "with the selected axe, chopsToFell clicks in range fell the tree");
            Assert.AreEqual(_tree.chopsToFell, _tree.Chops, "fells after exactly chopsToFell click-chops");
            Assert.AreEqual(_tree.chopsToFell * _tree.woodPerChop, _inv.WoodCount,
                "total wood == chopsToFell * woodPerChop (wood ticks up per click into the WoodId stack)");
        }

        // === AC1 (swing) — a landed chop FIRES the Mixamo melee swing (change-(b): CastawayCharacter.TriggerChop
        //     latches ChopTriggered). The Animator can't be observed headlessly (deltaTime≈0), so the proof is the
        //     trigger fired — exactly one trigger per chop (the analog of jump's JumpTraceActive). ===
        [UnityTest]
        public IEnumerator Chop_FiresTheSwing()
        {
            SelectAxe();
            Assert.IsFalse(_character.ConsumeChopTriggered(), "precondition: no chop swing fired yet");

            // Drive a single chop directly (isolates the swing-trigger seam from the chop pacing).
            _tree.Chop();
            Assert.AreEqual(1, _tree.Chops, "the direct chop landed");

            // The chop must have fired the melee swing trigger on the castaway (one chop → one swing request).
            Assert.IsTrue(_character.ChopTriggered, "a landed chop must fire CastawayCharacter.TriggerChop (the swing)");
            Assert.IsTrue(_character.ConsumeChopTriggered(), "consuming the latch reports the fired trigger");
            Assert.IsFalse(_character.ChopTriggered, "the latch is one-shot per chop (cleared after consume)");

            // A second chop fires the swing again (each chop = one swing).
            _tree.Chop();
            Assert.IsTrue(_character.ChopTriggered, "a second chop fires the swing again (one swing per chop)");
            yield return null;
        }

        // === AC1 (tool-use speed) — chopSpeed clamps to the sane band so a dial can't stall (0) or blur the swing ===
        [Test]
        public void ChopSpeed_DefaultsToOne_AndDrivesTheAttackPlaybackRate()
        {
            // The serialized default is 1× (authored swing duration). The settings `tool-use speed` row binds to
            // this field (SettingsCatalogChopTests proves the binding); here we pin the band constants the slider
            // and the TriggerChop clamp share, so a dialed value can't land in a dead zone.
            Assert.AreEqual(1f, _character.chopSpeed, 1e-4f, "chopSpeed defaults to 1× (authored swing speed)");
            Assert.AreEqual(CastawayCharacter.ChopSpeedMin, FarHorizon.Settings.SettingsCatalog.ToolSpeedMin, 1e-4f,
                "chop ChopSpeedMin == catalog ToolSpeedMin (no slider dead zone)");
            Assert.AreEqual(CastawayCharacter.ChopSpeedMax, FarHorizon.Settings.SettingsCatalog.ToolSpeedMax, 1e-4f,
                "chop ChopSpeedMax == catalog ToolSpeedMax (no slider dead zone)");
        }

        // === AC3 — a felled stump REGROWS after the (tweakable) timer into a standing, choppable tree ===
        [UnityTest]
        public IEnumerator FelledStump_RegrowsAfterTimer_IntoAChoppableTree()
        {
            SelectAxe();
            StandAtTree();

            // Fell it by clicking chopsToFell times.
            for (int i = 0; i < _tree.chopsToFell; i++) yield return ClickChop();
            Assert.IsTrue(_tree.IsFelled, "tree felled (now a stump)");
            int woodAtFell = _inv.WoodCount;

            // The stump persists — clicking it while felled yields NO wood (a stump is not choppable).
            for (int i = 0; i < 3; i++) yield return ClickChop();
            Assert.IsTrue(_tree.IsFelled, "the stump persists through the regrow window (AC4)");
            Assert.AreEqual(woodAtFell, _inv.WoodCount, "a felled stump yields no wood (clicking it does nothing)");

            // Wait past the max regrow time + the rise tween — the stump regrows into a standing tree.
            float start = Time.time;
            while (Time.time - start < _tree.regrowthMaxSeconds + 1.5f && _tree.IsFelled) yield return null;
            Assert.IsFalse(_tree.IsFelled, "the stump regrew into a standing tree after the timer (AC3)");
            Assert.AreEqual(0, _tree.Chops, "a regrown tree resets its chop count — it can be chopped anew");

            // And the regrown tree is choppable again — a click in range (axe selected) yields fresh wood.
            yield return ClickChop();
            Assert.Greater(_inv.WoodCount, woodAtFell, "the regrown tree is choppable again — a click yields wood");
        }

        // === Out of range with the axe selected — a click never chops (proximity is required too) ===
        [UnityTest]
        public IEnumerator ClickOutOfRangeWithSelectedAxe_DoesNotChop()
        {
            SelectAxe();
            // Player stays far away (SetUp put it at (20,0,20)); click anyway.
            for (int i = 0; i < 5; i++) yield return ClickChop();

            Assert.AreEqual(0, _inv.WoodCount, "out of range -> a click yields no wood even with the axe selected");
            Assert.AreEqual(0, _tree.Chops, "out of range -> no chops");
            Assert.IsFalse(_tree.IsFelled, "out of range -> not felled");
        }

        // ============================================================================================
        // CHANGE (a) — EVERY scatter tree is choppable; the chop resolves the NEAREST in-range tree, and
        // each tree deplete→stump→regrows INDEPENDENTLY (AC5; the Sponsor soak-reject: only ONE tree chopped).
        // A "LowPolyScatter" root with named LP_Tree children stands in for the world scatter (ChopTree.Start
        // discovers it the same way the shipped scene does — GameObject.Find then the LP_Tree name-scan).
        // ============================================================================================

        // Build a scatter root with N LP_Tree children at the given XZ positions, then re-init the ChopTree so
        // its Start() discovers them (a fresh ChopTree on a NEW GO — Awake adds instance 0 = its own visual,
        // Start collects the scatter trees). Returns the new tree component + the scatter tree GOs.
        private ChopTree _genTree;
        private GameObject _genTreeGo;
        private GameObject _scatterRootGo;
        private GameObject[] _scatterTrees;

        private void BuildScatterWorld(params Vector3[] positions)
        {
            _scatterRootGo = new GameObject("LowPolyScatter");
            _scatterTrees = new GameObject[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var t = new GameObject(ChopTree.ScatterTreeName); // "LP_Tree"
                t.transform.SetParent(_scatterRootGo.transform, false);
                t.transform.position = positions[i];
                _scatterTrees[i] = t;
            }

            // A fresh ChopTree whose own (demo) visual is far away, so only the scatter trees are reachable.
            _genTreeGo = new GameObject("DemoChopTree");
            _genTreeGo.transform.position = new Vector3(100f, 0f, 100f);
            _genTree = _genTreeGo.AddComponent<ChopTree>();
            _genTree.inventory = _inv;
            _genTree.player = _playerGo.transform;
            _genTree.visual = _genTreeGo.transform;
            _genTree.character = _character;
            _genTree.scatterRoot = _scatterRootGo.transform;
            _genTree.chopRadius = 2.2f;
            _genTree.woodPerChop = 1;
            _genTree.chopsToFell = 3;
            _genTree.chopInterval = 0f;
            _genTree.inventoryUI = null;
            _genTree.regrowthMinSeconds = 0.4f;
            _genTree.regrowthMaxSeconds = 0.6f;
            _genTree.regrowSeed = 999;
        }

        [TearDown]
        public void TearDownGen()
        {
            if (_genTreeGo != null) Object.Destroy(_genTreeGo);
            if (_scatterRootGo != null) Object.Destroy(_scatterRootGo);
        }

        private IEnumerator ClickChopGen()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _genTree.RequestChopClick();
            yield return null;
        }

        // === CHANGE (a) — the resolver tracks the demo tree + every scatter tree ===
        [UnityTest]
        public IEnumerator Resolver_TracksDemoTreePlusEveryScatterTree()
        {
            BuildScatterWorld(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(-10f, 0f, 0f));
            yield return null; // let Start() run + discover the scatter trees

            Assert.AreEqual(4, _genTree.InstanceCount,
                "the resolver must track 1 demo tree + 3 scatter LP_Tree instances (CHANGE (a))");
        }

        // === CHANGE (a) — a scatter tree is choppable: stand near tree A, click → A depletes, yields wood ===
        [UnityTest]
        public IEnumerator ClickNearAScatterTree_ChopsThatTree_YieldsWood()
        {
            BuildScatterWorld(new Vector3(0f, 0f, 0f), new Vector3(20f, 0f, 0f));
            yield return null;
            SelectAxe();
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // at scatter tree 0

            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood");
            yield return ClickChopGen();

            Assert.AreEqual(1, _inv.WoodCount, "a click near a SCATTER tree yields wood (CHANGE (a) — not only the demo tree)");
            // Instance 1 = the first scatter tree (instance 0 is the demo tree, far away).
            Assert.AreEqual(1, _genTree.ChopsOn(1), "the near scatter tree took the chop");
            Assert.AreEqual(0, _genTree.ChopsOn(2), "the FAR scatter tree was untouched");
        }

        // === AC5 — the chop targets the NEAREST in-range tree (two in range → the nearer one chops) ===
        [UnityTest]
        public IEnumerator TwoTreesInRange_ChopsTheNearer()
        {
            // Two scatter trees both within chopRadius (2.2) of the player, at different distances.
            BuildScatterWorld(new Vector3(2.0f, 0f, 0f),   // instance 1 — 2.0u away (FAR-er of the two)
                              new Vector3(0.5f, 0f, 0f));  // instance 2 — 0.5u away (NEARER)
            yield return null;
            SelectAxe();
            _playerGo.transform.position = Vector3.zero;

            yield return ClickChopGen();

            Assert.AreEqual(0, _genTree.ChopsOn(1), "the farther in-range tree must NOT be chopped");
            Assert.AreEqual(1, _genTree.ChopsOn(2), "the NEAREST in-range tree is the chop target (AC5)");
        }

        // === CHANGE (a) — each tree deplete→stump→regrows INDEPENDENTLY (fell A; B still standing & choppable) ===
        [UnityTest]
        public IEnumerator TreesDepleteAndRegrowIndependently()
        {
            BuildScatterWorld(new Vector3(0f, 0f, 0f),    // instance 1 — the one we fell
                              new Vector3(30f, 0f, 0f));  // instance 2 — far away, never touched
            yield return null;
            SelectAxe();
            _playerGo.transform.position = new Vector3(0.3f, 0f, 0.3f); // at instance 1

            // Fell instance 1 (chopsToFell clicks).
            for (int i = 0; i < _genTree.chopsToFell; i++) yield return ClickChopGen();
            Assert.IsTrue(_genTree.IsFelledOn(1), "the chopped scatter tree felled to a stump");
            Assert.IsFalse(_genTree.IsFelledOn(2), "the far scatter tree is UNAFFECTED — independent state");
            Assert.AreEqual(_genTree.chopsToFell, _inv.WoodCount, "wood == chopsToFell from felling tree 1");

            // The felled stump is no longer choppable — further clicks at it yield no wood.
            int woodAtFell = _inv.WoodCount;
            for (int i = 0; i < 3; i++) yield return ClickChopGen();
            Assert.AreEqual(woodAtFell, _inv.WoodCount, "a felled stump yields no wood (it's not choppable)");
            Assert.IsTrue(_genTree.IsFelledOn(1), "the stump persists through its own regrow window (AC4)");

            // Wait past instance 1's regrow window — it regrows into a standing, choppable tree, independently.
            float start = Time.time;
            while (Time.time - start < _genTree.regrowthMaxSeconds + 1.5f && _genTree.IsFelledOn(1))
                yield return null;
            Assert.IsFalse(_genTree.IsFelledOn(1), "the stump regrew into a standing tree after its own timer (AC3)");
            Assert.AreEqual(0, _genTree.ChopsOn(1), "the regrown tree reset its chop count");

            // And it's choppable anew — a click in range yields fresh wood.
            yield return ClickChopGen();
            Assert.Greater(_inv.WoodCount, woodAtFell, "the regrown scatter tree is choppable again");
        }

        // ============================================================================================
        // CHANGE (a) GATE-HARDENING (86caa4c5c follow-up) — load the REAL committed Boot scene (NOT the
        // synthetic BuildScatterWorld rig the tests above use) and assert the SHIPPED ChopTree is wired to
        // the seed-42 scatter AND discovers MORE than one choppable tree at runtime. This pins two regression
        // classes the synthetic-rig + EditMode + demo-tree-only verify gates were ALL blind to:
        //   • a LowPolyScatter / LP_Tree RENAME drift (the scatter trees silently stop being discovered →
        //     only the demo tree chops again — the Sponsor-rejected "only ONE tree chops");
        //   • a scatterRoot-not-serialized regression (BuildChopTree / WireChopScatterRoot stops wiring the
        //     ref → the runtime name-scan is the only thing that saves it; this asserts the SERIALIZED ref).
        // It mirrors the RockScatter/BeachDebris/WorldLook PlayMode pattern (SceneManager.LoadScene("Boot") →
        // step a frame → assert against the loaded runtime scene), and the InstanceCount assert is RUNTIME-only
        // state (ChopTree._instances is populated in Start(), which never runs in an EditMode OpenScene), so
        // this cannot live in EditMode's ChopSceneTests — it has to run in PlayMode against the loaded scene.
        // ============================================================================================
        [UnityTest]
        public IEnumerator BootScene_ChopTree_WiredToScatter_AndDiscoversManyTrees_AtRuntime()
        {
            // Tear down the SetUp synthetic rig BEFORE loading the real scene so the FindObjectsByType below
            // resolves the SHIPPED ChopTree, not a leftover test GO. LoadScene(Single) would destroy them too,
            // but explicit teardown keeps the assert unambiguous.
            if (_treeGo != null) Object.Destroy(_treeGo);
            if (_invGo != null) Object.Destroy(_invGo);
            if (_playerGo != null) Object.Destroy(_playerGo);
            if (_charGo != null) Object.Destroy(_charGo);
            yield return null;

            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null; // activate the loaded scene
            yield return null; // step one real frame so ChopTree.Awake + Start run (scatter discovery is in Start)

            var trees = Object.FindObjectsByType<ChopTree>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, trees.Length,
                "the loaded Boot scene must carry exactly ONE ChopTree resolver (the demo tree + the scatter " +
                "trees are tracked by this single component, not one component per tree)");
            ChopTree shipped = trees[0];

            // The SERIALIZED scatterRoot ref must be wired editor-time (BuildChopTree + WireChopScatterRoot) —
            // an unwired ref would fall back to the GameObject.Find name-scan, but the SHIPPED contract is the
            // serialized ref (editor-vs-runtime trap, unity-conventions.md). It must point at LowPolyScatter.
            Assert.IsNotNull(shipped.scatterRoot,
                "the shipped ChopTree.scatterRoot must be WIRED editor-time to the seed-42 scatter root " +
                "(CHANGE (a) — WireChopScatterRoot); a null ref means the scatter-root serialization regressed " +
                "and only the runtime name-scan fallback would save scatter choppability");
            Assert.AreEqual("LowPolyScatter", shipped.scatterRoot.name,
                "ChopTree.scatterRoot must point at the LowPolyScatter root (a rename here silently strands " +
                "the scatter trees as un-choppable)");

            // RUNTIME assert: the resolver must have discovered MORE than one choppable tree (the demo tree +
            // the seed-42 scatter LP_Trees). InstanceCount == 1 is the EXACT Sponsor-rejected regression
            // ("only ONE tree chops") — a LP_Tree rename or a broken scatter discovery would land here.
            Assert.Greater(shipped.InstanceCount, 1,
                $"the shipped ChopTree must discover MANY choppable trees at runtime (found InstanceCount=" +
                $"{shipped.InstanceCount}) — 1 means ONLY the demo tree is choppable (the twice-Sponsor-rejected " +
                "regression: a renamed LP_Tree / broken scatter discovery strands every scatter tree)");

            // Stronger: the demo tree is instance 0, so the scatter contributed InstanceCount-1 trees — assert
            // the seed-42 scatter is genuinely dense (the world has ~320 LP_Trees), not a single stray match.
            Assert.Greater(shipped.InstanceCount, 10,
                $"the shipped scatter must contribute MANY choppable trees (InstanceCount={shipped.InstanceCount}) " +
                "— a handful would signal the LP_Tree discovery is only partially finding the seed-42 scatter");
        }
    }
}
