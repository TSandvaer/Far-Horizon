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
            _tree.swingImpactDelaySeconds = 0.1f; // refinement 3: short impact delay so ClickChop can wait it out
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

        // CHANGE 1 + refinement 3 — request ONE chop-click (the input-independent seam) then advance frames until
        // the chop EFFECT has LANDED AT IMPACT (the click fires the swing now; the effect lands ~impactDelay
        // later — refinement 3). One frame consumes the latch + schedules the impact; we then wait out the impact
        // window so the effect (wood / deplete / fell) has applied before the caller asserts. A rejected click
        // (out of range / no axe / over-UI) schedules nothing → ImpactPending stays false → the wait is one frame.
        // UiInputGate is forced closed (no modal panel) so the headless gate decision is range + axe-selected only.
        private IEnumerator ClickChop()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked); // ensure no modal-panel gate in the test
            _tree.RequestChopClick();
            yield return null;                 // consume the latch → schedule the impact (or reject the click)
            // Wait out the impact window so the effect has applied (bounded so a rejected click can't hang).
            float start = Time.time;
            while (_tree.ImpactPending && Time.time - start < 1f) yield return null;
            yield return null;                 // one more frame for the impact-resolve Update to apply the effect
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

        // === Refinement 1 (Sponsor soak 2026-06-27) — the chop TURNS THE PLAYER TO FACE the target tree ===
        [UnityTest]
        public IEnumerator Chop_TurnsPlayerToFaceTheTargetTree()
        {
            SelectAxe();
            // Put the tree DUE EAST of the player (+X) and the player facing NORTH-ish to start, so a correct
            // face-turn must yaw to ~+90° (atan2(dx=+1, dz=0) = 90°). The player stands in range of the tree.
            _treeGo.transform.position = new Vector3(2f, 0f, 0f);
            _playerGo.transform.position = Vector3.zero;
            _character.FaceWorldYawInstant(0f); // start facing +Z (north) — clearly NOT toward the +X tree
            Assert.AreEqual(0f, _character.BodyYaw, 1e-3f, "precondition: facing north, not the tree");

            // One chop-click in range → the chop faces the resolved target tree before the swing.
            yield return ClickChop();
            Assert.AreEqual(1, _tree.Chops, "the chop landed (precondition for the face-turn)");

            // The body yaw must now point at the tree (+X is yaw +90°). A snap or a fast lerp both land here on
            // the chop frame (FaceWorldTarget applies the yaw immediately).
            Assert.AreEqual(90f, Mathf.DeltaAngle(0f, _character.BodyYaw) + 0f, 5f,
                "after a chop the player faces the target tree (+X → yaw ~90°), not its prior north facing");
        }

        // === Refinement 3 (Sponsor soak 2026-06-27) — the chop EFFECT lands at the swing's IMPACT, NOT on the
        //     click frame: the click fires the swing immediately but wood/deplete are deferred ~impactDelay. ===
        [UnityTest]
        public IEnumerator ChopEffect_LandsAtImpact_NotOnClickFrame()
        {
            SelectAxe();
            StandAtTree();
            _tree.swingImpactDelaySeconds = 0.4f; // a clear window so we can observe the pre-impact gap
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood");

            // Request the click + step ONE frame: the swing fires + the impact is scheduled, but the EFFECT has
            // NOT landed yet (no wood, no chop count, the tree is unchanged) — the down-stroke hasn't arrived.
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _tree.RequestChopClick();
            yield return null; // consume the click → schedule impact
            Assert.IsTrue(_tree.ImpactPending, "the swing fired + the impact is SCHEDULED (not yet applied)");
            Assert.AreEqual(0, _tree.Chops, "the chop EFFECT has NOT landed on the click frame (no depletion yet)");
            Assert.AreEqual(0, _inv.WoodCount, "no wood on the click frame — wood lands at IMPACT, not on click");
            Assert.IsTrue(_character.ConsumeChopTriggered(), "the SWING fired immediately on the click");

            // Wait out the impact window — now the effect lands (wood + one chop).
            float start = Time.time;
            while (_tree.ImpactPending && Time.time - start < 1f) yield return null;
            yield return null;
            Assert.AreEqual(1, _tree.Chops, "the chop EFFECT lands AT IMPACT (one chop after the down-stroke)");
            Assert.AreEqual(_tree.woodPerChop, _inv.WoodCount, "wood yields AT IMPACT (synced to the visual hit)");
        }

        // === Refinement 3 (single-flight) — a 2nd click while a swing's impact is PENDING must NOT double-apply ===
        [UnityTest]
        public IEnumerator SecondClickMidSwing_DoesNotDoubleApply()
        {
            SelectAxe();
            StandAtTree();
            _tree.swingImpactDelaySeconds = 0.4f;

            // First click → schedules an impact.
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _tree.RequestChopClick();
            yield return null;
            Assert.IsTrue(_tree.ImpactPending, "first click scheduled an impact");

            // A SECOND click while the impact is pending must be ignored (no stack) — request several.
            for (int i = 0; i < 4; i++) { _tree.RequestChopClick(); yield return null; }
            Assert.IsTrue(_tree.ImpactPending, "still exactly ONE impact pending (the extra clicks were ignored)");

            // Let the single impact land — exactly ONE chop + woodPerChop wood (not 1 + 4).
            float start = Time.time;
            while (_tree.ImpactPending && Time.time - start < 1f) yield return null;
            yield return null;
            Assert.AreEqual(1, _tree.Chops, "one swing = one impact = ONE chop (the mid-swing clicks didn't stack)");
            Assert.AreEqual(_tree.woodPerChop, _inv.WoodCount, "exactly woodPerChop wood — no double-apply");
        }

        // === Refinement 2 (Sponsor soak 2026-06-27) — a fully-chopped tree FADES OUT + is REMOVED after the delay,
        //     then REGROWS at the same spot (AC3 still applies). REPLACES the persistent stump. ===
        [UnityTest]
        public IEnumerator ChoppedTree_FadesOutAndIsRemoved_AfterDelay_ThenRegrows()
        {
            SelectAxe();
            StandAtTree();
            // A short fade delay so the fade fires in headless wall-clock; regrow LATER so the fade is the next
            // event (the real game is fade ~10s ≪ regrow ~10min — same ordering, scaled for the test).
            _tree.fadeOutDelaySeconds = 0.3f;
            _tree.regrowthMinSeconds = 1.6f;
            _tree.regrowthMaxSeconds = 1.8f;
            // The demo tree (instance 0) is this _treeGo; give it a renderer so the removal is observable.
            if (_treeGo.GetComponent<MeshRenderer>() == null) _treeGo.AddComponent<MeshRenderer>();

            // Fell it.
            for (int i = 0; i < _tree.chopsToFell; i++) yield return ClickChop();
            Assert.IsTrue(_tree.IsFelled, "tree felled (now fading)");
            Assert.IsTrue(_tree.IsTreeVisible, "right after felling, the tree is still visible (mid-fell, pre-fade)");

            // Wait past the fade delay + the fade-out tween — the tree must FADE OUT + its renderers disable
            // (gone; the ground is empty), but it has NOT yet regrown.
            float start = Time.time;
            while (Time.time - start < 1.4f && _tree.IsTreeVisible) yield return null;
            Assert.IsFalse(_tree.IsTreeVisible, "after the fade-out delay the tree disappears (renderers disabled)");
            Assert.IsTrue(_tree.IsTreeRemoved, "the faded tree is REMOVED (ground empty) — the persistent stump is gone");
            Assert.IsTrue(_tree.IsFelled, "still felled (awaiting regrow) — removal is not regrow");

            // Wait past the regrow window — the tree regrows at the SAME spot, visible + choppable again (AC3).
            start = Time.time;
            while (Time.time - start < _tree.regrowthMaxSeconds + 1.5f && _tree.IsFelled) yield return null;
            Assert.IsFalse(_tree.IsFelled, "the tree regrew after the timer (AC3 still applies post-fade)");
            Assert.IsTrue(_tree.IsTreeVisible, "the regrown tree is visible again (renderers re-enabled)");
            Assert.AreEqual(0, _tree.Chops, "a regrown tree resets its chop count — choppable anew");

            // And it's choppable again — a click yields fresh wood.
            int woodBefore = _inv.WoodCount;
            yield return ClickChop();
            Assert.Greater(_inv.WoodCount, woodBefore, "the regrown tree is choppable again — a click yields wood");
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

            // A felled tree is not choppable — clicking it while felled yields NO wood (here regrow < fade, so it
            // regrows directly; the fade-out path is covered by ChoppedTree_FadesOutAndIsRemoved_AfterDelay).
            for (int i = 0; i < 3; i++) yield return ClickChop();
            Assert.IsTrue(_tree.IsFelled, "a felled tree stays felled through its regrow window");
            Assert.AreEqual(woodAtFell, _inv.WoodCount, "a felled tree yields no wood (clicking it does nothing)");

            // Wait past the max regrow time + the rise tween — the stump regrows into a standing tree.
            float start = Time.time;
            while (Time.time - start < _tree.regrowthMaxSeconds + 1.5f && _tree.IsFelled) yield return null;
            Assert.IsFalse(_tree.IsFelled, "the stump regrew into a standing tree after the timer (AC3)");
            Assert.AreEqual(0, _tree.Chops, "a regrown tree resets its chop count — it can be chopped anew");

            // And the regrown tree is choppable again — a click in range (axe selected) yields fresh wood.
            yield return ClickChop();
            Assert.Greater(_inv.WoodCount, woodAtFell, "the regrown tree is choppable again — a click yields wood");
        }

        // ============================================================================================
        // HOLD-TO-CHOP (86caf7a0p) — holding LMB keeps swinging (repeat swings) on the locked target tree
        // until it fells (AC2) or the button is released (AC1); each swing lands exactly one impact / one wood
        // (AC3 invariant). The hold is driven via the SetChopHeld(true/false) programmatic seam (a headless run
        // can't hold a real mouse button — the analog of RequestChopClick for the click case). The chain cadence
        // is one swing per IMPACT, so we step frames past each impact window between swings.
        // ============================================================================================

        // Step ONE swing of a held chain: from a held state with no impact pending, one frame begins the swing
        // (schedules the impact), then we wait out the impact window so the effect lands. Returns when the swing's
        // effect has applied (or after a bounded wait if no swing began — e.g. felled / out of range).
        private IEnumerator StepHeldSwing()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            // One frame to begin the swing (the held chain logic schedules the impact this frame if eligible).
            yield return null;
            // Wait out the impact window so the chop EFFECT lands before the caller asserts (bounded).
            float start = Time.time;
            while (_tree.ImpactPending && Time.time - start < 1f) yield return null;
            yield return null; // one more frame for the impact-resolve Update to apply the effect
        }

        // === AC1 — HOLDING LMB repeats swings: N swings land N chops (one wood each), no double-apply ===
        [UnityTest]
        public IEnumerator HoldingLmb_RepeatsSwings_UntilReleased()
        {
            SelectAxe();
            StandAtTree();
            _tree.chopsToFell = 5;                 // enough headroom to observe several repeat swings before fell
            _tree.swingImpactDelaySeconds = 0.1f;  // short impact window so the chain advances quickly headless
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood");

            // BEGIN HOLD — the first frame is a fresh press → starts a chain on the nearest tree.
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _tree.SetChopHeld(true);

            // Three swings WITHOUT a single re-press — the hold alone repeats them.
            for (int i = 0; i < 3; i++) yield return StepHeldSwing();

            Assert.AreEqual(3, _tree.Chops, "holding LMB repeats swings — 3 chops landed with NO extra presses");
            Assert.AreEqual(3 * _tree.woodPerChop, _inv.WoodCount, "each repeat swing yields exactly one wood (no double-apply)");
            Assert.IsTrue(_tree.IsChopChainActive, "the chain is still active while LMB is held + the tree stands");
            Assert.IsFalse(_tree.IsFelled, "not yet felled (chopsToFell=5, only 3 swings)");

            // RELEASE — the chain stops; no further swings even across many frames.
            _tree.SetChopHeld(false);
            yield return null; // the release-frame drops the lock
            Assert.IsFalse(_tree.IsChopChainActive, "releasing LMB stops the chain (no lock)");
            int woodAtRelease = _inv.WoodCount;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(3, _tree.Chops, "after release, NO further chops land (the repeat stopped)");
            Assert.AreEqual(woodAtRelease, _inv.WoodCount, "no wood after release — the swing loop is over");
        }

        // === AC1 — a single click (press+release within one swing) still produces EXACTLY ONE swing ===
        [UnityTest]
        public IEnumerator SingleClick_StillProducesExactlyOneSwing_BackCompat()
        {
            SelectAxe();
            StandAtTree();
            _tree.swingImpactDelaySeconds = 0.2f;

            // The classic single-click seam (press+release inside one swing) — must NOT start a chain.
            yield return ClickChop();
            Assert.AreEqual(1, _tree.Chops, "a single click lands exactly ONE chop (back-compat — one click one swing)");
            Assert.AreEqual(_tree.woodPerChop, _inv.WoodCount, "one click → one wood");
            Assert.IsFalse(_tree.IsChopChainActive, "a single click never leaves a chain running");

            // Many idle frames → no repeat.
            for (int i = 0; i < 12; i++) yield return null;
            Assert.AreEqual(1, _tree.Chops, "a single click does NOT repeat — exactly one swing");
        }

        // === AC2 — STOP-ON-FALL: holding LMB swings until the tree fells, then the chain STOPS (no empty swings) ===
        [UnityTest]
        public IEnumerator HoldingLmb_StopsOnFall_NoSwingingAtEmptyGround()
        {
            SelectAxe();
            StandAtTree();
            _tree.chopsToFell = 3;
            _tree.swingImpactDelaySeconds = 0.1f;
            _tree.fadeOutDelaySeconds = 30f;       // keep it felled (not yet faded/regrown) through this assertion
            _tree.regrowthMinSeconds = 60f;
            _tree.regrowthMaxSeconds = 60f;

            // Hold and let it chop to the fell point WITHOUT releasing.
            _tree.SetChopHeld(true);
            for (int i = 0; i < _tree.chopsToFell; i++) yield return StepHeldSwing();

            Assert.IsTrue(_tree.IsFelled, "holding chopped the tree all the way to felled (no per-swing re-press)");
            Assert.AreEqual(_tree.chopsToFell, _tree.Chops, "exactly chopsToFell chops landed (one per swing)");
            Assert.IsFalse(_tree.IsChopChainActive, "STOP-ON-FALL — the chain dropped when the locked tree felled");

            // STILL HOLDING, but the tree is felled — no further swing at the empty/falling tree.
            int woodAtFell = _inv.WoodCount;
            for (int i = 0; i < 12; i++) yield return null;
            Assert.AreEqual(_tree.chopsToFell, _tree.Chops, "no chops at a felled tree even while still holding (AC2)");
            Assert.AreEqual(woodAtFell, _inv.WoodCount, "no wood gained swinging at empty ground");
            Assert.IsFalse(_tree.IsChopChainActive, "the chain stays stopped while held over the felled tree");
        }

        // === AC3 — the repeat cadence is driven by SWING COMPLETION (impact), NOT input-poll rate: while a swing's
        //     impact is PENDING, no extra swing/impact can fire no matter how many frames tick (frame-rate-safe) ===
        [UnityTest]
        public IEnumerator HoldChain_OneImpactPerSwing_NotInputPollRate()
        {
            SelectAxe();
            StandAtTree();
            _tree.chopsToFell = 10;
            _tree.swingImpactDelaySeconds = 0.4f;  // a wide impact window so many frames tick during ONE swing

            _tree.SetChopHeld(true);
            yield return null; // begin the first swing (schedules impact)
            Assert.IsTrue(_tree.ImpactPending, "the first held swing scheduled exactly one impact");

            // Tick MANY frames while still holding + impact pending — NOT ONE extra swing/impact may begin (the
            // single-flight guard makes the cadence one-swing-per-impact, independent of frame rate / poll count).
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(0, _tree.Chops, "no impact has landed yet (still within the one impact window)");

            // Let the single impact resolve → exactly ONE chop (not 1-per-frame).
            float start = Time.time;
            while (_tree.ImpactPending && Time.time - start < 1f) yield return null;
            yield return null;
            Assert.AreEqual(1, _tree.Chops, "exactly ONE chop per swing — the 20 idle frames did NOT stack impacts");

            _tree.SetChopHeld(false);
            yield return null;
        }

        // === AC2 (re-press for the next tree) — after a tree fells under a held button, the chain does NOT
        //     auto-acquire a neighbour; a fresh PRESS starts a new chain on the next nearest tree ===
        [UnityTest]
        public IEnumerator AfterFall_HeldButton_DoesNotAutoAcquire_FreshPressStartsNewChain()
        {
            BuildScatterWorld(new Vector3(0f, 0f, 0f),    // instance 1 — felled first
                              new Vector3(1.2f, 0f, 0f)); // instance 2 — a neighbour also IN RANGE
            yield return null;
            SelectAxe();
            _playerGo.transform.position = new Vector3(0.3f, 0f, 0.3f); // in range of BOTH
            _genTree.chopsToFell = 2;
            _genTree.swingImpactDelaySeconds = 0.1f;
            _genTree.fadeOutDelaySeconds = 30f;
            _genTree.regrowthMinSeconds = 60f;
            _genTree.regrowthMaxSeconds = 60f;

            // HOLD — chain locks onto the nearest (instance 2 at 1.2 vs instance 1 at ~0.42 → nearest is inst 1).
            _genTree.SetChopHeld(true);
            // Fell the locked tree.
            for (int i = 0; i < _genTree.chopsToFell; i++) yield return StepHeldSwingGen();
            // Exactly one of the two trees should be felled (the locked one), the other untouched (no auto-acquire).
            int felledCount = (_genTree.IsFelledOn(1) ? 1 : 0) + (_genTree.IsFelledOn(2) ? 1 : 0);
            Assert.AreEqual(1, felledCount, "exactly ONE tree felled under the held button (no auto-acquire of the neighbour)");
            Assert.IsFalse(_genTree.IsChopChainActive, "STOP-ON-FALL — the chain dropped on fell, even still held");

            // STILL HOLDING — the standing neighbour must NOT be auto-chopped (AC2 default: re-press required).
            int chopsOnStandingBefore = _genTree.IsFelledOn(1) ? _genTree.ChopsOn(2) : _genTree.ChopsOn(1);
            for (int i = 0; i < 12; i++) yield return null;
            int chopsOnStandingAfter = _genTree.IsFelledOn(1) ? _genTree.ChopsOn(2) : _genTree.ChopsOn(1);
            Assert.AreEqual(chopsOnStandingBefore, chopsOnStandingAfter,
                "the standing neighbour is NOT auto-chopped while the button stays held (re-press required, AC2)");

            // RE-PRESS (release then hold) — a fresh press starts a NEW chain on the standing neighbour.
            _genTree.SetChopHeld(false);
            yield return null;
            _genTree.SetChopHeld(true);
            yield return StepHeldSwingGen();
            int chopsOnStandingAfterRepress = _genTree.IsFelledOn(1) ? _genTree.ChopsOn(2) : _genTree.ChopsOn(1);
            Assert.Greater(chopsOnStandingAfterRepress, chopsOnStandingAfter,
                "a FRESH press starts a new chain on the next nearest standing tree (re-acquisition on re-press)");
            _genTree.SetChopHeld(false);
            yield return null;
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
            _genTree.swingImpactDelaySeconds = 0.1f; // refinement 3: short impact delay for the scatter tests
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
            yield return null;                 // schedule the impact (refinement 3)
            float start = Time.time;
            while (_genTree.ImpactPending && Time.time - start < 1f) yield return null;
            yield return null;                 // apply the effect at impact
        }

        // HOLD-TO-CHOP (86caf7a0p) — step ONE swing of a HELD chain on the scatter rig (the SetChopHeld seam must
        // already be true). Mirrors StepHeldSwing for the _genTree scatter component.
        private IEnumerator StepHeldSwingGen()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            yield return null;                 // begin the swing (schedule impact this frame if eligible)
            float start = Time.time;
            while (_genTree.ImpactPending && Time.time - start < 1f) yield return null;
            yield return null;                 // apply the effect at impact
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
