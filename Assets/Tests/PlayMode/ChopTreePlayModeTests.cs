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
    /// Headless time discipline (unity-conventions.md / playbook E6/E7): NEVER WaitForEndOfFrame (does not fire in
    /// -batchmode), NEVER per-frame deltaTime assertions (deltaTime ≈ 0 headless). The cadence/fade/regrow gates are
    /// anchored on an OWNED deterministic clock (86camdk1h) the test advances a fixed step per frame via
    /// Step()/StepUntil — because headless -batchmode does NOT honor Time.captureDeltaTime (the #255 pin was
    /// ineffective in CI), a wall-clock / captureDeltaTime window is non-deterministic and over-counts. See the
    /// StableStep block below.
    /// </summary>
    public class ChopTreePlayModeTests
    {
        private GameObject _invGo;
        private GameObject _playerGo;
        private GameObject _treeGo;
        private GameObject _charGo;
        private GameObject _spawnerGo;
        private GameObject _looterGo;
        private Inventory _inv;
        private ChopTree _tree;
        private CastawayCharacter _character;
        private LogPileSpawner _spawner;
        private PickableLooter _looter;

        // 86camdk1h — OWNED DETERMINISTIC CLOCK (supersedes the #255 Time.captureDeltaTime pin, which was
        // INEFFECTIVE in CI). The hold-to-chop cadence gates (swingImpactDelaySeconds + the clip-completion window)
        // and each tree's fade/regrow timers are TIME-SPACED across many frames off Time.time. Under a bare
        // headless -batchmode run Time.deltaTime≈0 while Time.time leaps in coarse/variable wall-clock jumps, so a
        // SINGLE frame's Time.time delta can exceed a whole cadence window: the impact resolves (one chop) AND the
        // next swing begins in the same Update, collapsing the cadence and OVER-COUNTING (the reds: 2 vs 1, 4 vs 1,
        // 5 vs 3, 12 vs 5; ChoppedTree fading / trees regrowing before their windows). #255 pinned
        // Time.captureDeltaTime to force a fixed virtual step — but headless -batchmode PlayMode does NOT honor
        // captureDeltaTime (empirically: the pin is present at HEAD yet the over-count PERSISTS in CI — PR body).
        // So instead the TEST OWNS the clock ChopTree reads (via the behavior-neutral, UNITY_INCLUDE_TESTS-stripped
        // ChopTree.TestClock seam) and ADVANCES it a fixed StableStep every frame it steps — a WORKING
        // captureDeltaTime. Each cadence/fade/regrow window then spans a DETERMINISTIC frame count regardless of
        // the (un-honored) engine capture clock or the coarse headless wall-clock. The gates hold exactly as they
        // do at 60fps, so these tests still GUARD the machine-gun-chop / too-fast-regrow bug class (a real
        // regression still reds them) instead of being [Ignore]-quarantined. Step 0.01s (100Hz) keeps every "tick
        // N frames" assertion (max 20 frames = 0.2s) comfortably inside the smallest impact window (0.4s). The
        // production gate logic is UNCHANGED — only the clock SOURCE is injected (null → Time.time in the ship build).
        private const float StableStep = 0.01f;

        // The fake clock ChopTree.Now reads via TestClock. Advanced a fixed StableStep per frame by Step() below,
        // so the Time.time-based cadence + fade/regrow gates advance deterministically (86camdk1h).
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f; // reset the owned clock each test (86camdk1h)

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

            // REWORK 86caf9u5t — the shared log-pile spawner (the felled tree drops a lootable pile holding its
            // whole yield) + a looter (E loots the pile). Yield small so the loot/pile assertions are exact.
            _spawnerGo = new GameObject("LogPileSpawner");
            _spawner = _spawnerGo.AddComponent<LogPileSpawner>();
            _spawner.WoodYield = 5;
            _spawner.DespawnSeconds = 180f;

            _looterGo = new GameObject("PickableLooter");
            _looter = _looterGo.AddComponent<PickableLooter>();
            _looter.inventory = _inv;
            _looter.player = _playerGo.transform;

            _treeGo = new GameObject("ChopTree");
            _treeGo.transform.position = Vector3.zero;
            // The demo tree needs a Renderer BEFORE ChopTree.Awake constructs its ChoppableTreeState, because the
            // state captures GetComponentsInChildren<Renderer> at construction (in the real Boot scene the tree is
            // a real mesh) — the fade-out/regrow test reads IsTreeVisible off those captured renderers. Adding it
            // in the test body (post-construction) was too late → IsVisible saw an empty renderer set (86camdk1h).
            _treeGo.AddComponent<MeshRenderer>();
            _tree = _treeGo.AddComponent<ChopTree>();
            _tree.inventory = _inv;
            _tree.player = _playerGo.transform;
            _tree.visual = _treeGo.transform;
            _tree.character = _character;
            _tree.logPileSpawner = _spawner;
            _tree.chopRadius = 2.2f;
            _tree.woodPerChop = 1;
            _tree.chopsToFell = 3;
            _tree.chopInterval = 0f;          // no click cooldown in the test (each requested click chops)
            _tree.inventoryUI = null;         // no UI rig → the over-UI guard is skipped (modal/RMB are false)
            _tree.regrowthMinSeconds = 0.4f;  // short so the regrow window is testable in headless wall-clock
            _tree.regrowthMaxSeconds = 0.6f;
            _tree.regrowSeed = 12345;         // deterministic regrow roll
            _tree.swingImpactDelaySeconds = 0.1f; // refinement 3: short impact delay so ClickChop can wait it out
            // 86caf7a0p re-iter — the bare test rig has no Animator (no model FBX), so CastawayCharacter.MeleeClipLength
            // returns 0 and ChopTree falls back to THIS serialized clip length for the hold-repeat cadence gate. Keep
            // it short (but ≥ the impact delay) so the headless hold tests advance quickly in wall-clock time while
            // still exercising the "next swing waits for the clip to finish" gate (cadence ≥ this, not the 0.1 impact).
            _tree.swingClipLengthSeconds = 0.3f;

            // 86camdk1h — inject the OWNED deterministic clock. The setter propagates to instance 0 (the demo tree,
            // already built in Awake during AddComponent above); scatter trees built later in Start inherit it too.
            _tree.TestClock = () => _now;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_treeGo);
            Object.Destroy(_charGo);
            if (_spawnerGo != null) Object.Destroy(_spawnerGo);
            if (_looterGo != null) Object.Destroy(_looterGo);
            // Clean up any log piles the fell tests spawned at runtime (they are not parented to the rig GOs).
            foreach (var pile in Object.FindObjectsByType<LogPile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(pile.gameObject);
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

        // 86camdk1h — advance the OWNED clock one fixed StableStep + tick one frame (the deterministic analog of a
        // rendered capture frame). EVERY wait that expects the production cadence/fade/regrow to progress steps via
        // these — a bare `yield return null` no longer advances the clock ChopTree reads, so bare yields are used
        // ONLY where the assertion is that NOTHING happens (no click / no chop over N frames).
        private IEnumerator Step()
        {
            _now += StableStep;
            yield return null;
        }
        private IEnumerator StepFrames(int n) { for (int i = 0; i < n; i++) yield return Step(); }
        // Advance until a condition holds, bounded by a DETERMINISTIC frame budget (never a wall-clock timeout — the
        // owned clock is decoupled from real time) so a stuck condition fails fast instead of hanging the run.
        private IEnumerator StepUntil(System.Func<bool> done, int maxFrames = 6000)
        {
            int f = 0;
            while (!done() && f++ < maxFrames) yield return Step();
        }

        // CHANGE 1 + refinement 3 — request ONE chop-click (the input-independent seam) then advance the owned clock
        // until the chop EFFECT has LANDED AT IMPACT (the click fires the swing now; the effect lands ~impactDelay
        // later — refinement 3). One frame consumes the latch + schedules the impact; StepUntil then advances the
        // clock across the impact window so the effect (deplete / fell) has applied before the caller asserts. A
        // rejected click (out of range / no axe / over-UI) schedules nothing → ImpactPending stays false → StepUntil
        // returns immediately (a ~2-frame no-op). UiInputGate is forced closed so the gate is range + axe-selected.
        private IEnumerator ClickChop()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked); // ensure no modal-panel gate in the test
            _tree.RequestChopClick();
            yield return Step();                              // consume the latch → schedule the impact (or reject)
            yield return StepUntil(() => !_tree.ImpactPending); // advance across the impact window → effect lands
            yield return Step();                              // one more frame for the impact-resolve Update to apply
        }
        private bool _gateTracked;

        // === 86camdk1h AC(a) — EMPIRICAL PROBE: is Time.captureDeltaTime honored in headless -batchmode PlayMode? ===
        // The ROOT-CAUSE evidence for why the #255 pin was ineffective. It sets captureDeltaTime to a fixed 0.01s
        // virtual step, ticks 10 bare frames, and LOGS the measured per-frame Time.time advance + Time.deltaTime to
        // playmode.log (grep `[clock-probe]`). If captureDeltaTime were honored the 10-frame advance would be ~0.10s
        // and deltaTime ~0.01; in CI headless -batchmode it is NOT — which is exactly why the cadence gates
        // over-counted WITH the pin present (see the PR body: the pin is at HEAD yet CI still reds). Restores
        // captureDeltaTime to 0. Does NOT ASSERT the (env-dependent) honoring — it is pure evidence; the real cadence
        // tests are clock-INDEPENDENT (they use the owned TestClock), so they pass regardless of this measurement.
        [UnityTest]
        public IEnumerator Probe_CaptureDeltaTime_Honoring_InHeadlessBatchmode()
        {
            const float pin = 0.01f;
            const int frames = 10;
            Time.captureDeltaTime = pin;
            float t0 = Time.time;
            for (int i = 0; i < frames; i++) yield return null; // BARE frames — measuring the ENGINE clock, not TestClock
            float advance = Time.time - t0;
            float lastDelta = Time.deltaTime;
            bool honored = Mathf.Abs(advance - frames * pin) < frames * pin * 0.25f; // within 25% of the ideal 0.10s
            Debug.Log($"[clock-probe] captureDeltaTime pin={pin} frames={frames} " +
                      $"expectedAdvance={frames * pin:F4} actualAdvance={advance:F4} " +
                      $"perFrame~{advance / frames:F5} lastDeltaTime={lastDelta:F5} captureDeltaTimeHonored={honored}");
            Time.captureDeltaTime = 0f;

            // Never-flaky sanity: Time.time is monotonic, so the probe ran + measured. The finding lives in the log.
            Assert.GreaterOrEqual(advance, 0f, "[clock-probe] the probe measured a real (monotonic) engine clock");
        }

        // === CHANGE 1 — at the tree WITHOUT a click does NOTHING (the proximity-auto trigger is REMOVED) ===
        [UnityTest]
        public IEnumerator AtTreeWithSelectedAxe_NoClick_DoesNotChop()
        {
            SelectAxe();
            StandAtTree();

            // Stand in range, axe selected, but NEVER request a click — the old proximity-auto would have
            // chopped here; the new left-click trigger must NOT. Advance the owned clock so a time-based
            // proximity-auto regression would fire during this window (keeps the test LIVE, not clock-frozen).
            yield return StepFrames(50);

            Assert.AreEqual(0, _inv.WoodCount, "standing at the tree WITHOUT a click must not chop (CHANGE 1)");
            Assert.AreEqual(0, _tree.Chops, "no click -> no chops land (proximity-auto is removed)");
            Assert.IsFalse(_tree.IsFelled, "no click -> the tree is never felled");
        }

        // === CHANGE 1 — ONE click in range with the selected axe lands EXACTLY ONE chop (one strike/click) ===
        // REWORK 86caf9u5t (AC1): a non-felling chop yields NO wood (the wood drops on FELL, not per swing).
        [UnityTest]
        public IEnumerator OneClickInRange_LandsExactlyOneChop_ButYieldsNoWoodUntilFell()
        {
            SelectAxe();
            StandAtTree();
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood yet");

            yield return ClickChop();

            Assert.AreEqual(1, _tree.Chops, "ONE click -> exactly ONE chop (one strike per click)");
            Assert.AreEqual(0, _inv.WoodCount,
                "AC1 — a non-felling chop yields NO wood; the wood is awarded on FELL as a lootable pile, not per swing");
            Assert.IsFalse(_tree.IsFelled, "one chop of three does not fell the tree");

            // A second frame WITHOUT a new click does not chop again (the click does not repeat).
            yield return null;
            Assert.AreEqual(1, _tree.Chops, "no further chop without a NEW click (one chop per click)");
            Assert.AreEqual(0, _inv.WoodCount, "still no wood — chopping never banks wood (REWORK AC1)");
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

        // === AC1 positive + AC2 — with the axe SELECTED, clicking in range fells the tree and the wood is
        //     awarded ON FELL as a lootable LOG PILE (REWORK 86caf9u5t — NOT per chop) ===
        [UnityTest]
        public IEnumerator ClickAtTreeWithSelectedAxe_FellsTree_AndDropsLootablePile_NoWoodUntilLooted()
        {
            SelectAxe();
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood yet");

            StandAtTree();
            // One click per chop needed to fell (chopsToFell clicks).
            for (int i = 0; i < _tree.chopsToFell; i++) yield return ClickChop();

            Assert.IsTrue(_tree.IsFelled, "with the selected axe, chopsToFell clicks in range fell the tree");
            Assert.AreEqual(_tree.chopsToFell, _tree.Chops, "fells after exactly chopsToFell click-chops");
            Assert.AreEqual(0, _inv.WoodCount,
                "AC1/AC2 — chopping never banks wood; the wood is in the dropped LOG PILE until the player loots it");

            // AC2 — a lootable LOG PILE spawned at the tree, holding the WHOLE tree's yield (spawner.WoodYield).
            var pile = Object.FindObjectOfType<LogPile>();
            Assert.IsNotNull(pile, "a log pile spawned on fell (AC2)");
            Assert.AreEqual(_spawner.WoodYield, pile.LogsRemaining,
                "the pile holds the WHOLE tree's wood-yield (logs == the `tree-chop wood yield` setting), not a per-chop tally");

            // E loots the WHOLE pile -> the wood lands in the inventory (the pickable path). NO manual
            // DiscoverPickables() here (#165 de-mask): SpawnAt now REGISTERS the pile with the looter, so the REAL
            // discovery path must surface it. (The masking DiscoverPickables() call hid that SpawnAt never
            // registered the pile — the looter's lazy re-discover only fires on an EMPTY cache, which the live
            // build never has; the EditMode LogPileSpawnerLootTests guard exercises that exact breaking condition.)
            _looter.RequestLoot();
            yield return null;
            Assert.AreEqual(_spawner.WoodYield, _inv.WoodCount,
                "AC2 — one E grabs the WHOLE pile -> the tree's full yield lands in the inventory (via the pile's " +
                "registration with the looter, NOT a manual rediscover)");
            Assert.IsFalse(pile.IsAvailable, "the emptied pile is consumed (gone immediately when collected)");
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
            Assert.AreEqual(90f, Mathf.DeltaAngle(0f, _character.BodyYaw), 5f,
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
            yield return Step(); // consume the click → schedule impact
            Assert.IsTrue(_tree.ImpactPending, "the swing fired + the impact is SCHEDULED (not yet applied)");
            Assert.AreEqual(0, _tree.Chops, "the chop EFFECT has NOT landed on the click frame (no depletion yet)");
            Assert.AreEqual(0, _inv.WoodCount, "no wood on the click frame — wood lands at IMPACT, not on click");
            Assert.IsTrue(_character.ConsumeChopTriggered(), "the SWING fired immediately on the click");

            // Advance across the impact window — now the effect lands (one chop).
            yield return StepUntil(() => !_tree.ImpactPending);
            yield return Step();
            Assert.AreEqual(1, _tree.Chops, "the chop EFFECT lands AT IMPACT (one chop after the down-stroke)");
            // REWORK 86caf9u5t — a non-felling chop banks NO wood (the chop count, landed at impact, is the signal).
            Assert.AreEqual(0, _inv.WoodCount, "a non-felling chop banks no wood — the count is the impact signal (AC1)");
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
            yield return Step();
            Assert.IsTrue(_tree.ImpactPending, "first click scheduled an impact");

            // A SECOND click while the impact is pending must be ignored (no stack) — request several.
            for (int i = 0; i < 4; i++) { _tree.RequestChopClick(); yield return Step(); }
            Assert.IsTrue(_tree.ImpactPending, "still exactly ONE impact pending (the extra clicks were ignored)");

            // Let the single impact land — exactly ONE chop (not 1 + 4); the chop count is the single-flight signal.
            yield return StepUntil(() => !_tree.ImpactPending);
            yield return Step();
            Assert.AreEqual(1, _tree.Chops, "one swing = one impact = ONE chop (the mid-swing clicks didn't stack)");
            Assert.AreEqual(0, _inv.WoodCount, "a non-felling chop banks no wood — no double-apply (REWORK AC1)");
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

            // Advance past the fade delay + the fade-out tween — the tree must FADE OUT + its renderers disable
            // (gone; the ground is empty), but it has NOT yet regrown (regrow 1.6–1.8 ≫ fade completion ~1.3).
            yield return StepUntil(() => !_tree.IsTreeVisible);
            Assert.IsFalse(_tree.IsTreeVisible, "after the fade-out delay the tree disappears (renderers disabled)");
            Assert.IsTrue(_tree.IsTreeRemoved, "the faded tree is REMOVED (ground empty) — the persistent stump is gone");
            Assert.IsTrue(_tree.IsFelled, "still felled (awaiting regrow) — removal is not regrow");

            // Advance past the regrow window — the tree regrows at the SAME spot, visible + choppable again (AC3).
            yield return StepUntil(() => !_tree.IsFelled);
            Assert.IsFalse(_tree.IsFelled, "the tree regrew after the timer (AC3 still applies post-fade)");
            Assert.IsTrue(_tree.IsTreeVisible, "the regrown tree is visible again (renderers re-enabled)");
            Assert.AreEqual(0, _tree.Chops, "a regrown tree resets its chop count — choppable anew");

            // And it's choppable again — a click LANDS A CHOP on the regrown tree (the chop count is the signal;
            // REWORK 86caf9u5t — a single chop banks no wood, so the count, not WoodCount, proves choppability).
            int chopsBefore = _tree.Chops;
            yield return ClickChop();
            Assert.Greater(_tree.Chops, chopsBefore, "the regrown tree is choppable again — a click lands a chop");
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
            int chopsAtFell = _tree.Chops;

            // A felled tree is not choppable — clicking it while felled lands NO further chop (here regrow < fade,
            // so it regrows directly; the fade-out path is covered by ChoppedTree_FadesOutAndIsRemoved_AfterDelay).
            for (int i = 0; i < 3; i++) yield return ClickChop();
            Assert.IsTrue(_tree.IsFelled, "a felled tree stays felled through its regrow window");
            Assert.AreEqual(chopsAtFell, _tree.Chops, "a felled tree takes no further chops (clicking it does nothing)");

            // Advance past the max regrow time + the rise tween — the stump regrows into a standing tree.
            yield return StepUntil(() => !_tree.IsFelled);
            Assert.IsFalse(_tree.IsFelled, "the stump regrew into a standing tree after the timer (AC3)");
            Assert.AreEqual(0, _tree.Chops, "a regrown tree resets its chop count — it can be chopped anew");

            // And the regrown tree is choppable again — a click in range (axe selected) lands a fresh chop.
            yield return ClickChop();
            Assert.AreEqual(1, _tree.Chops, "the regrown tree is choppable again — a click lands a chop");
        }

        // ============================================================================================
        // HOLD-TO-CHOP (86caf7a0p) — holding LMB keeps swinging (repeat swings) on the locked target tree
        // until it fells (AC2) or the button is released (AC1); each swing lands exactly one impact / one wood
        // (AC3 invariant). The hold is driven via the SetChopHeld(true/false) programmatic seam (a headless run
        // can't hold a real mouse button — the analog of RequestChopClick for the click case). The chain cadence
        // is one swing per IMPACT, so we step frames past each impact window between swings.
        // ============================================================================================

        // Step ONE swing of a held chain: from a held state with no impact pending AND no clip in progress, one
        // frame begins the swing (schedules the impact), then we wait out the impact window so the effect lands,
        // THEN wait out the CLIP-COMPLETION cadence gate (86caf7a0p re-iter — the next swing can't begin until the
        // current swing's clip finishes). Returns when the swing's effect has applied AND the cadence gate is open
        // for the next swing (or after a bounded wait if no swing began — e.g. felled / out of range).
        // 86camdk1h — advance the OWNED clock across exactly ONE more COMPLETED held swing (its impact = one chop).
        // With the deterministic clock a HELD chain re-arms the next swing the instant the current clip completes,
        // so "wait until SwingInProgress is false" never sees a stable edge — the robust primitive is to advance
        // until the chop COUNT increments by one. The production single-flight (one impact per swing) + clip-
        // completion (next swing waits for the clip) gates guarantee that's exactly one swing at a time; a
        // machine-gun / over-pacing regression is caught by the count-window tests (HoldChain_OneImpactPerSwing /
        // HoldChain_NextSwingWaitsForClipToFinish), not by this stepper. Bounded; called only when a chop is due.
        private IEnumerator StepHeldSwing()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            int before = _tree.Chops;
            yield return StepUntil(() => _tree.Chops > before);
        }

        // === AC1 — HOLDING LMB repeats swings: N swings land N chops, no double-apply (REWORK: wood drops on fell,
        //     so the CHOP COUNT — not WoodCount — is the per-swing signal for these non-felling swings) ===
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
            Assert.AreEqual(0, _inv.WoodCount, "non-felling swings bank NO wood — the wood drops on fell (REWORK AC1)");
            Assert.IsTrue(_tree.IsChopChainActive, "the chain is still active while LMB is held + the tree stands");
            Assert.IsFalse(_tree.IsFelled, "not yet felled (chopsToFell=5, only 3 swings)");

            // RELEASE — the chain stops; no further swings even across many frames.
            _tree.SetChopHeld(false);
            yield return Step(); // the release-frame drops the lock
            Assert.IsFalse(_tree.IsChopChainActive, "releasing LMB stops the chain (no lock)");
            yield return StepFrames(10);
            Assert.AreEqual(3, _tree.Chops, "after release, NO further chops land (the repeat stopped)");
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
            Assert.AreEqual(0, _inv.WoodCount, "one click → one chop, NO wood (wood drops on fell — REWORK AC1)");
            Assert.IsFalse(_tree.IsChopChainActive, "a single click never leaves a chain running");

            // Many idle frames (owned clock advancing) → no repeat.
            yield return StepFrames(12);
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

            // STILL HOLDING, but the tree is felled — no further swing at the empty/falling tree (the chop count
            // is the signal; wood drops on fell as the pile, not banked here — REWORK 86caf9u5t).
            yield return StepFrames(12);
            Assert.AreEqual(_tree.chopsToFell, _tree.Chops, "no chops at a felled tree even while still holding (AC2)");
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
            _tree.swingClipLengthSeconds = 0.5f;   // clip > impact → after the impact the clip-gate blocks the next swing

            _tree.SetChopHeld(true);
            yield return Step(); // begin the first swing (schedules impact)
            Assert.IsTrue(_tree.ImpactPending, "the first held swing scheduled exactly one impact");

            // Tick MANY frames while still holding + impact pending — NOT ONE extra swing/impact may begin (the
            // single-flight guard makes the cadence one-swing-per-impact, independent of frame rate / poll count).
            yield return StepFrames(20); // 20 * 0.01 = 0.2s, still comfortably inside the 0.4s impact window
            Assert.AreEqual(0, _tree.Chops, "no impact has landed yet (still within the one impact window)");

            // Let the single impact resolve → exactly ONE chop (not 1-per-frame), then RELEASE so the chain adds no more.
            yield return StepUntil(() => _tree.Chops >= 1);
            _tree.SetChopHeld(false);
            yield return Step();
            Assert.AreEqual(1, _tree.Chops, "exactly ONE chop per swing — the 20 idle frames did NOT stack impacts");
        }

        // ============================================================================================
        // 86caf7a0p RE-ITER — the Sponsor soak-reject: "the animation is not allowed to finish and the tree goes
        // down too fast because 1 hit is not = on finished animation." The next held swing must wait for the swing
        // CLIP to FINISH (cadence ≥ clip length ÷ speed), so ONE completed swing = exactly ONE chop. These tests
        // catch the BUG CLASS (mid-clip restart over-pacing the chops), not just one instance.
        // ============================================================================================

        // === RE-ITER 1 — the NEXT held swing is GATED on the swing CLIP completing (cadence ≥ clip length), NOT
        //     the shorter impact delay. While the clip is still playing, no second swing/chop begins — the exact
        //     "animation not allowed to finish" guard. ===
        [UnityTest]
        public IEnumerator HoldChain_NextSwingWaitsForClipToFinish_NotImpactDelay()
        {
            SelectAxe();
            StandAtTree();
            _tree.chopsToFell = 10;
            _tree.swingImpactDelaySeconds = 0.1f;  // impact lands EARLY (mid-clip)...
            _tree.swingClipLengthSeconds = 0.5f;   // ...but the CLIP runs much longer — the cadence must track THIS

            _tree.SetChopHeld(true);
            yield return Step();                   // begin the first swing
            Assert.IsTrue(_tree.ImpactPending, "the first swing scheduled its impact");

            // Let the FIRST swing's impact resolve (one chop), but the CLIP is still playing afterward (clip > impact).
            yield return StepUntil(() => _tree.Chops >= 1);
            float firstChopAt = _now;
            Assert.AreEqual(1, _tree.Chops, "the first completed swing landed exactly one chop at impact");
            Assert.IsTrue(_tree.SwingInProgress,
                "the swing CLIP is still playing AFTER its impact (cadence is gated on clip completion, not impact)");

            // The regression guard for the over-pacing bug ("animation not allowed to finish → tree goes down too
            // fast"): advance a window LONGER than the impact delay (0.1s) but SHORTER than the clip (0.5s) and prove
            // NO second chop lands — the next swing must WAIT for the CLIP, not fire again at the impact delay. A
            // machine-gun regression (cadence gated on impact, not clip) would land a 2nd chop inside this window.
            yield return StepFrames(20); // 0.2s: past the 0.1s impact delay but well inside the 0.5s clip
            Assert.AreEqual(1, _tree.Chops, "NO 2nd chop within the impact-delay window — the next swing waits for the CLIP");
            Assert.IsTrue(_tree.SwingInProgress, "the clip is STILL playing 0.2s after the impact (cadence ≥ clip length)");

            // Once the clip finishes, the next swing begins — exactly ONE more chop, and it lands ≥ one CLIP length
            // after the first (cadence rides clip completion, not the shorter impact delay).
            yield return StepUntil(() => _tree.Chops >= 2);
            float secondChopAt = _now;
            Assert.AreEqual(2, _tree.Chops, "after the clip FINISHED, exactly ONE more chop landed (the next swing)");
            Assert.GreaterOrEqual(secondChopAt - firstChopAt, _tree.swingClipLengthSeconds - 0.05f,
                "the 2nd chop lands ≥ one CLIP length after the 1st — the cadence tracks clip completion, not the impact delay");

            _tree.SetChopHeld(false);
            yield return Step();
        }

        // === RE-ITER 2 — ONE completed swing = exactly ONE chop across a hold: N completed swings yield N chops,
        //     NO double-apply (the tree does not deplete faster than the completed swings). REWORK 86caf9u5t: the
        //     CHOP COUNT is the per-swing signal (non-felling swings bank no wood; the wood drops on fell). ===
        [UnityTest]
        public IEnumerator HoldChain_OneCompletedSwing_IsExactlyOneChop_NoDoubleApply()
        {
            SelectAxe();
            StandAtTree();
            _tree.chopsToFell = 12;                // headroom for several completed swings before fell
            _tree.swingImpactDelaySeconds = 0.1f;
            _tree.swingClipLengthSeconds = 0.3f;

            _tree.SetChopHeld(true);
            const int swings = 5;
            for (int i = 0; i < swings; i++) yield return StepHeldSwing();

            Assert.AreEqual(swings, _tree.Chops, "exactly one chop per COMPLETED swing (no mid-clip extra chops)");
            Assert.AreEqual(0, _inv.WoodCount,
                "non-felling completed swings bank NO wood — no double-apply / no over-paced depletion (REWORK AC1)");
            Assert.IsFalse(_tree.IsFelled, "not yet felled (chopsToFell=12, only 5 completed swings)");

            _tree.SetChopHeld(false);
            yield return null;
        }

        // === RE-ITER 3 — the tree FALLS only after exactly N COMPLETED swings (chopsToFell). It must NOT fell early
        //     because swings cut each other off — the soak symptom "the tree goes down too fast." ===
        [UnityTest]
        public IEnumerator HoldChain_TreeFallsOnlyAfterNCompletedSwings()
        {
            SelectAxe();
            StandAtTree();
            _tree.chopsToFell = 4;
            _tree.swingImpactDelaySeconds = 0.1f;
            _tree.swingClipLengthSeconds = 0.3f;
            _tree.fadeOutDelaySeconds = 30f;       // stay felled through the assertion
            _tree.regrowthMinSeconds = 60f;
            _tree.regrowthMaxSeconds = 60f;

            _tree.SetChopHeld(true);
            // After each of the first N-1 completed swings the tree must STILL be standing.
            for (int i = 1; i < _tree.chopsToFell; i++)
            {
                yield return StepHeldSwing();
                Assert.AreEqual(i, _tree.Chops, $"completed swing #{i} landed exactly one chop");
                Assert.IsFalse(_tree.IsFelled,
                    $"the tree is NOT felled after only {i} completed swings (needs {_tree.chopsToFell}) — it must not fall too fast");
            }
            // The N-th completed swing fells it.
            yield return StepHeldSwing();
            Assert.AreEqual(_tree.chopsToFell, _tree.Chops, "the felling swing is exactly the N-th completed swing");
            Assert.IsTrue(_tree.IsFelled, "the tree fells on the N-th completed swing — not before");

            _tree.SetChopHeld(false);
            yield return null;
        }

        // === RE-ITER 4 — the cadence SCALES with tool-use speed: a faster chopSpeed → a shorter effective swing
        //     duration (clip length ÷ speed). The cadence is derived from the clip, not a fixed interval. ===
        [UnityTest]
        public IEnumerator HoldChain_CadenceScalesWithToolUseSpeed()
        {
            SelectAxe();
            StandAtTree();
            _tree.swingClipLengthSeconds = 1.0f;

            // The bare rig has no Animator → MeleeClipLength is 0 → the serialized clip length is the source.
            Assert.AreEqual(0f, _character.MeleeClipLength,
                "precondition: the bare test rig has no Animator clip, so the serialized fallback drives cadence");

            _character.chopSpeed = 1f;
            float at1x = _tree.EffectiveSwingDurationSeconds;
            Assert.AreEqual(1.0f, at1x, 0.0001f, "at 1x the effective swing duration equals the authored clip length");

            _character.chopSpeed = 2f;
            float at2x = _tree.EffectiveSwingDurationSeconds;
            Assert.AreEqual(0.5f, at2x, 0.0001f, "at 2x speed the swing finishes in half the time (clip length ÷ speed)");

            Assert.Greater(at1x, at2x, "a faster tool-use speed shortens the per-swing cadence (clip-derived, not fixed)");
            yield return null;
        }

        // === 86caf9ngh N2 — the serialized swingClipLengthSeconds fallback is FLOORED strictly > 0. A degenerate
        //     0 (or negative) on a misconfigured Animator-less rig must NOT collapse the swing duration to 0 (which
        //     would disable the clip-completion cadence gate → machine-gun swings). The live build is unaffected
        //     (it reads the real MeleeClipLength); this guards only the bare-rig fallback. ===
        [UnityTest]
        public IEnumerator DegenerateZeroFallback_FloorsSwingDurationStrictlyPositive()
        {
            SelectAxe();
            StandAtTree();
            _character.chopSpeed = 1f;

            // The bare rig has no Animator → MeleeClipLength is 0 → the serialized fallback is the source.
            Assert.AreEqual(0f, _character.MeleeClipLength,
                "precondition: the bare test rig has no Animator clip, so the serialized fallback drives cadence");

            // A misconfigured fallback of 0 must NOT yield a 0-length swing duration (the gate would never engage).
            _tree.swingClipLengthSeconds = 0f;
            Assert.Greater(_tree.EffectiveSwingDurationSeconds, 0f,
                "a 0 fallback is floored strictly > 0 (the SwingClipLengthFloor) so the cadence gate still engages");
            Assert.AreEqual(ChopTree.SwingClipLengthFloor, _tree.EffectiveSwingDurationSeconds, 0.0001f,
                "the 0 fallback floors to exactly SwingClipLengthFloor at 1x speed");

            // A negative fallback is likewise floored (Mathf.Max with the floor).
            _tree.swingClipLengthSeconds = -5f;
            Assert.AreEqual(ChopTree.SwingClipLengthFloor, _tree.EffectiveSwingDurationSeconds, 0.0001f,
                "a negative fallback also floors to SwingClipLengthFloor (never negative/zero duration)");

            // A SANE fallback above the floor is untouched (the floor only catches the degenerate case).
            _tree.swingClipLengthSeconds = 1.6f;
            Assert.AreEqual(1.6f, _tree.EffectiveSwingDurationSeconds, 0.0001f,
                "a sane fallback above the floor is unchanged — the floor only catches degenerate 0/negative values");
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
            yield return StepFrames(12);
            int chopsOnStandingAfter = _genTree.IsFelledOn(1) ? _genTree.ChopsOn(2) : _genTree.ChopsOn(1);
            Assert.AreEqual(chopsOnStandingBefore, chopsOnStandingAfter,
                "the standing neighbour is NOT auto-chopped while the button stays held (re-press required, AC2)");

            // RE-PRESS (release then hold) — a fresh press starts a NEW chain on the standing neighbour.
            _genTree.SetChopHeld(false);
            yield return Step();
            _genTree.SetChopHeld(true);
            yield return StepHeldSwingGen();
            int chopsOnStandingAfterRepress = _genTree.IsFelledOn(1) ? _genTree.ChopsOn(2) : _genTree.ChopsOn(1);
            Assert.Greater(chopsOnStandingAfterRepress, chopsOnStandingAfter,
                "a FRESH press starts a new chain on the next nearest standing tree (re-acquisition on re-press)");
            _genTree.SetChopHeld(false);
            yield return null;
        }

        // === 86caf9ngh N1 — a RE-PRESS on a NEW tree WITHIN the just-felled swing's clip window fires IMMEDIATELY
        //     (AC1: "single click fires immediately"). The bug: _swingEndsAt was never reset on fell/release, so the
        //     felling swing's SwingInProgress lock leaked into the next chain's first press and BLOCKED it for the
        //     ~clip-length window. The pre-existing AfterFall test masks this because StepHeldSwingGen WAITS OUT
        //     SwingInProgress before the re-press; here we re-press IMMEDIATELY (clip still in flight) to expose it.
        //     This FAILS without the fell+release _swingEndsAt reset (the re-press's swing never schedules in window). ===
        [UnityTest]
        public IEnumerator RePressImmediatelyAfterFell_WithinClipWindow_FiresAFreshSwingImmediately()
        {
            BuildScatterWorld(new Vector3(0f, 0f, 0f),    // instance 1 — felled first
                              new Vector3(1.2f, 0f, 0f)); // instance 2 — a neighbour also IN RANGE
            yield return null;
            SelectAxe();
            _playerGo.transform.position = new Vector3(0.3f, 0f, 0.3f); // in range of BOTH
            _genTree.chopsToFell = 1;                 // ONE swing fells instance 1 → straight to the re-press case
            _genTree.swingImpactDelaySeconds = 0.1f;  // impact lands EARLY (mid-clip)
            _genTree.swingClipLengthSeconds = 1.0f;   // ...but the CLIP runs LONG — a wide window for the bug to bite
            _genTree.fadeOutDelaySeconds = 30f;
            _genTree.regrowthMinSeconds = 60f;
            _genTree.regrowthMaxSeconds = 60f;

            // HOLD → the chain locks on the nearest (instance 1 at ~0.42 vs instance 2 at ~0.96). Fell it in one swing.
            _genTree.SetChopHeld(true);
            yield return Step();                      // begin the felling swing (schedules impact)
            yield return StepUntil(() => !_genTree.ImpactPending);
            yield return Step();                      // apply the fell at impact
            Assert.IsTrue(_genTree.IsFelledOn(1), "the locked tree felled on its single swing");
            // The felling swing's CLIP is still playing — without the N1 reset, SwingInProgress would still be true
            // and would block the re-press below. With the reset (fell OPENS the gate), it is already clear.
            Assert.IsFalse(_genTree.SwingInProgress,
                "fell RESET the clip-completion gate (N1) — the next press is not blocked by the felled swing's clip");

            // RE-PRESS IMMEDIATELY (release + hold) on the standing neighbour, well WITHIN the 1.0s clip window. The
            // fresh swing must schedule an impact on the very next frames — NOT wait out the prior swing's clip.
            int chopsBefore = _genTree.IsFelledOn(1) ? _genTree.ChopsOn(2) : _genTree.ChopsOn(1);
            _genTree.SetChopHeld(false);
            yield return Step();
            _genTree.SetChopHeld(true);
            yield return Step();                      // the fresh press — must begin a swing THIS frame (gate open)
            Assert.IsTrue(_genTree.ImpactPending,
                "a re-press within the clip window fires a FRESH swing immediately (AC1) — not blocked by the stale gate");

            // ...and that swing lands its chop on the neighbour (proving the immediate fire actually connected).
            yield return StepUntil(() => !_genTree.ImpactPending);
            yield return Step();
            int chopsAfter = _genTree.IsFelledOn(1) ? _genTree.ChopsOn(2) : _genTree.ChopsOn(1);
            Assert.Greater(chopsAfter, chopsBefore,
                "the immediate re-press swing chopped the standing neighbour within the felled swing's clip window");

            _genTree.SetChopHeld(false);
            yield return Step();
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
            _genTree.logPileSpawner = _spawner;   // REWORK 86caf9u5t — a felled scatter tree drops a lootable pile
            _genTree.chopRadius = 2.2f;
            _genTree.woodPerChop = 1;
            _genTree.chopsToFell = 3;
            _genTree.chopInterval = 0f;
            _genTree.inventoryUI = null;
            _genTree.regrowthMinSeconds = 0.4f;
            _genTree.regrowthMaxSeconds = 0.6f;
            _genTree.regrowSeed = 999;
            _genTree.swingImpactDelaySeconds = 0.1f; // refinement 3: short impact delay for the scatter tests
            _genTree.swingClipLengthSeconds = 0.3f;  // 86caf7a0p re-iter — short clip-completion cadence (no Animator)

            // 86camdk1h — the scatter rig shares the OWNED clock, so its cadence/fade/regrow are deterministic too.
            // Set BEFORE the `yield return null` that runs Start(): the setter propagates to instance 0 now, and the
            // scatter LP_Tree instances built in Start inherit it (ChopTree.NewState), so all read one clock.
            _genTree.TestClock = () => _now;
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
            yield return Step();                                 // schedule the impact (refinement 3)
            yield return StepUntil(() => !_genTree.ImpactPending); // advance across the impact window
            yield return Step();                                 // apply the effect at impact
        }

        // HOLD-TO-CHOP (86caf7a0p) — step ONE swing of a HELD chain on the scatter rig (the SetChopHeld seam must
        // already be true). Mirrors StepHeldSwing for the _genTree scatter component, incl. the 86caf7a0p re-iter
        // clip-completion wait so the next StepHeldSwingGen can actually begin a swing.
        // Total chops across every scatter instance — the chain locks ONE tree, so this increments by exactly one
        // per completed held swing (the demo instance-0 is far away + untouched in the scatter tests).
        private int GenTotalChops()
        {
            int t = 0;
            for (int i = 0; i < _genTree.InstanceCount; i++) t += _genTree.ChopsOn(i);
            return t;
        }

        // Advance the owned clock across exactly ONE more COMPLETED held swing on the LOCKED scatter tree (the
        // chop-count form — see StepHeldSwing for why "wait for SwingInProgress false" can't be used on a held chain).
        private IEnumerator StepHeldSwingGen()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            int before = GenTotalChops();
            yield return StepUntil(() => GenTotalChops() > before);
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

        // === CHANGE (a) — a scatter tree is choppable: stand near tree A, click → A depletes (lands a chop) ===
        // REWORK 86caf9u5t: a single chop banks no wood (wood drops on fell), so the CHOP COUNT is the signal.
        [UnityTest]
        public IEnumerator ClickNearAScatterTree_ChopsThatTree()
        {
            BuildScatterWorld(new Vector3(0f, 0f, 0f), new Vector3(20f, 0f, 0f));
            yield return null;
            SelectAxe();
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // at scatter tree 0

            yield return ClickChopGen();

            // Instance 1 = the first scatter tree (instance 0 is the demo tree, far away).
            Assert.AreEqual(1, _genTree.ChopsOn(1), "a click near a SCATTER tree lands a chop on it (CHANGE (a) — not only the demo tree)");
            Assert.AreEqual(0, _genTree.ChopsOn(2), "the FAR scatter tree was untouched");
            Assert.AreEqual(0, _inv.WoodCount, "a non-felling chop banks no wood (the wood drops on fell — REWORK AC1)");
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
            // REWORK 86caf9u5t — felling tree 1 dropped a lootable pile (not banked wood); the inventory is still 0.
            Assert.AreEqual(0, _inv.WoodCount, "felling banks no wood — the wood is in the dropped pile until looted");
            Assert.IsNotNull(Object.FindObjectOfType<LogPile>(), "a log pile spawned at the felled scatter tree (AC2)");

            // The felled stump is no longer choppable — further clicks at it land no chop.
            for (int i = 0; i < 3; i++) yield return ClickChopGen();
            Assert.AreEqual(_genTree.chopsToFell, _genTree.ChopsOn(1), "a felled stump takes no further chops (not choppable)");
            Assert.IsTrue(_genTree.IsFelledOn(1), "the stump persists through its own regrow window (AC4)");

            // Advance past instance 1's regrow window — it regrows into a standing, choppable tree, independently.
            yield return StepUntil(() => !_genTree.IsFelledOn(1));
            Assert.IsFalse(_genTree.IsFelledOn(1), "the stump regrew into a standing tree after its own timer (AC3)");
            Assert.AreEqual(0, _genTree.ChopsOn(1), "the regrown tree reset its chop count");

            // And it's choppable anew — a click in range lands a fresh chop.
            yield return ClickChopGen();
            Assert.AreEqual(1, _genTree.ChopsOn(1), "the regrown scatter tree is choppable again — a click lands a chop");
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
