using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for 86cattf8y — MineBoulder's placement-obstacle RE-REGISTRATION driven through the REAL
    /// break -> fade -> removed -> REGROW CLOCK PATH (Tess PR #308 QA note 2, comment 5014462325).
    ///
    /// === WHY this exists (the coverage gap #308 shipped) ===
    /// #308's EditMode <c>MinedAway_StopsBlocking_ThenRegrowReRegisters</c> proved re-registration only via the
    /// POOL-DIAL ANALOG (<c>SetActiveNodeCount(0) -> (-1)</c>), which resets a node to standing WITHOUT ever
    /// entering the <c>_regrowing</c> tween. So a regression in the ACTUAL regrow path (a broken boulder's
    /// <c>_regrowing</c> -> <c>IsMineable</c> flip -> the Update-driven <see cref="MineBoulder.SyncPlacementObstacles"/>
    /// re-register) would red NO gate. This test closes that gap: it breaks a boulder, then advances the OWNED
    /// deterministic clock across the full break/fade/regrow sequence and asserts the registry RE-BLOCKS the
    /// regrown boulder's footprint — driven entirely by <see cref="MineBoulder.Update"/>'s real Tick +
    /// SyncPlacementObstacles wire (no pool-dial, no manual Sync call).
    ///
    /// NON-TAUTOLOGY (the ticket's success test): the final "regrown -> blocked again" assertion depends on
    /// SyncPlacementObstacles being invoked from Update AND its register-on-IsMineable branch. Remove either and
    /// the footprint stays GREEN after regrow (it was unregistered on break) -> this test REDS.
    ///
    /// === Headless time discipline (#288/#291 precedent — mirrors MineOrePlayModeTests / 86camf3xe) ===
    /// The break/fade/regrow timers + tweens are TIME-SPACED across many frames and FREEZE on the headless
    /// -batchmode <c>Time.unscaledDeltaTime</c> ~= 0 (the historical regrow red). So the TEST OWNS the clock
    /// MineBoulder reads (the behavior-neutral, UNITY_INCLUDE_TESTS-stripped <see cref="MineBoulder.TestClock"/>
    /// seam, propagated to each MineableNodeState) and ADVANCES it a fixed <see cref="StableStepSeconds"/> per
    /// stepped frame — a WORKING captureDeltaTime that also drives the tween <c>_dt</c>. No wall-clock waits, no
    /// ordering hacks. Production gate logic is UNCHANGED (null clock -> Time.time in the ship build).
    ///
    /// === DESIGNED TRANSIENT (AC2 — recorded, NOT changed here; a design call gated on a Sponsor soak) ===
    /// Registration keys on IsMineable, so a STILL-VISIBLE boulder reads GREEN to the placement ghost during the
    /// break-fade (~2.3s: break-crumble 0.4s + rest 0.8s + fade-out 0.7s, self-healing) AND during the regrow-RISE
    /// (0.6s: renderers re-enabled at BeginRegrow but IsMineable is still false while <c>_regrowing</c>). IsVisible
    /// (any renderer enabled) is the visually-honest alternative signal IF the Sponsor ever flags the transient at
    /// a soak. <see cref="StillVisibleAfterBreak_ReadsGreen_TheDesignedTransient"/> pins the observable half of that
    /// transient. Do NOT re-key IsMineable -> IsVisible in this ticket — that is OUT OF SCOPE (86cattf8y OOS), a
    /// soak-gated design decision.
    /// </summary>
    public class MineBoulderRegrowPlacementPlayModeTests
    {
        private GameObject _mineGo, _boulderRoot;
        private MineBoulder _mine;
        private Transform _boulder;
        private MeshRenderer _boulderRenderer;

        // 86camf3xe / #288 — OWNED DETERMINISTIC CLOCK step. 0.01s (100Hz) advances every break/fade/regrow window
        // across many frames so the shipped Time.time-gated logic runs LIVE + deterministic under the fake clock.
        private const float StableStepSeconds = 0.01f;
        private float _now; // the fake clock MineBoulder.Now reads via TestClock; advanced a fixed step per frame.

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
            LogAssert.ignoreFailingMessages = true; // bare-rig [boulder-trace] Debug.Log lines are not failures

            _boulderRoot = new GameObject("Boulders");
            var b = new GameObject(MineBoulder.BoulderNodeName);
            b.transform.SetParent(_boulderRoot.transform, false);
            b.transform.position = new Vector3(12f, 0f, 0f); // away from origin — no other obstacle nearby
            var mesh = new GameObject("BoulderMesh");
            mesh.transform.SetParent(b.transform, false);
            _boulderRenderer = mesh.AddComponent<MeshRenderer>(); // so IsVisible + the fade have a renderer to toggle
            _boulder = b.transform;

            _mineGo = new GameObject("MineBoulder");
            _mine = _mineGo.AddComponent<MineBoulder>();
            _mine.boulderRoot = _boulderRoot.transform;
            _mine.strikesToBreak = 2;
            _mine.strikeInterval = 0f;
            _mine.placementObstacleRadius = 1.2f;
            // regrow min == max -> a seed-INDEPENDENT, deterministic 3.0s delay, LONGER than the 1.2s fade delay so
            // the FULL break -> fade -> removed -> regrow sequence runs (the honest real path), not a regrow-straight-
            // from-broken shortcut.
            _mine.regrowthMinSeconds = 3.0f;
            _mine.regrowthMaxSeconds = 3.0f;
            _mine.regrowSeed = 91577;
            _mine.activeNodeCount = -1;
            // Inject the OWNED deterministic clock BEFORE Start so DiscoverAndStartPool's NewNodeState propagates it
            // to the MineableNodeState (mirrors MineOrePlayModeTests SetUp).
            _mine.TestClock = () => _now;
        }

        [TearDown]
        public void TearDown()
        {
            // Keep the STATIC PlacementObstacleRegistry hermetic across PlayMode tests — its SubsystemRegistration
            // reset only fires on play-entry, NOT between tests, and Object.Destroy's OnDisable is deferred. So
            // unregister explicitly + destroy.
            if (_boulder != null) PlacementObstacleRegistry.Unregister(_boulder);
            Object.Destroy(_mineGo);
            Object.Destroy(_boulderRoot);
            LogAssert.ignoreFailingMessages = false;
        }

        // Advance the OWNED clock one fixed StableStep + tick one frame (the deterministic analog of a rendered
        // frame). MineBoulder.Update() ticks every node + calls SyncPlacementObstacles each frame, so stepping drives
        // the REAL break/fade/regrow state machine AND the registry sync — no manual Sync, no pool-dial.
        private IEnumerator Step()
        {
            _now += StableStepSeconds;
            yield return null;
        }

        // Advance until a condition holds, bounded by a DETERMINISTIC frame budget (never a wall-clock timeout — the
        // owned clock is decoupled from real time) so a stuck condition fails fast instead of hanging the run.
        private IEnumerator StepUntil(System.Func<bool> done, int maxFrames = 6000)
        {
            int f = 0;
            while (!done() && f++ < maxFrames) yield return Step();
        }

        // The boulder is registered iff a small table footprint on it reads RED. Same 0.55 footprint the #308
        // EditMode guard uses. Because the query center + the registered zone share ONE transform, this is true
        // exactly while the boulder is registered.
        private bool Blocked() => PlacementObstacleRegistry.IsFootprintBlocked(_boulder.position, 0.55f);

        // === AC1 — the REAL clock path: break -> (fade -> removed ->) REGROW -> IsMineable flip -> RE-REGISTER ===
        [UnityTest]
        public IEnumerator BrokenBoulder_RegrowsViaRealClock_ReRegistersNoBuildZone()
        {
            yield return null; // Start discovers the pool + registers the standing boulder

            Assert.AreEqual(1, _mine.NodeCount, "the 1-boulder pool was discovered");
            Assert.IsTrue(Blocked(), "a STANDING boulder registers its no-build zone -> a table footprint reads RED");

            // Break it (strikesToBreak=2 immediate strikes) -> _broken -> the REAL break/fade/regrow state machine.
            _mine.Mine();
            _mine.Mine();
            Assert.IsTrue(_mine.IsBrokenOn(0), "two strikes break the boulder");

            // One frame: Update ticks the now-broken node + Syncs -> IsMineable false -> UNREGISTER -> footprint GREEN.
            yield return Step();
            Assert.IsFalse(Blocked(),
                "a just-broken boulder STOPS blocking placement (Update's Sync unregistered it on the IsMineable flip)");

            // Advance the OWNED clock across the full break -> fade -> removed -> REGROW sequence. Update's real Tick
            // advances each tween (the injected clock drives _dt) and re-Syncs every frame — NO pool-dial, NO manual
            // Sync. Bounded frame budget covers the ~3.6s sequence at 0.01s/frame (~360 frames).
            yield return StepUntil(() => !_mine.IsBrokenOn(0));
            Assert.IsFalse(_mine.IsBrokenOn(0), "the boulder regrew via the REAL clock (_regrowing completed)");
            Assert.AreEqual(0, _mine.StrikesOn(0), "a regrown boulder reset its strike count");

            // THE CORE AC1 / non-tautology assertion: the boulder regrown via the real clock RE-REGISTERS its no-build
            // zone — driven purely by MineBoulder.Update()'s SyncPlacementObstacles on the _regrowing -> IsMineable ->
            // true transition. REDS if the Update Sync call OR the register-on-IsMineable branch is removed (the
            // footprint would stay GREEN because break already unregistered it).
            Assert.IsTrue(Blocked(),
                "a boulder regrown via the REAL clock path RE-REGISTERS its no-build zone -> the footprint reads RED " +
                "again (86cattf8y: #308 proved re-registration ONLY via the pool-dial analog, which never runs _regrowing)");
        }

        // === AC2 — the DESIGNED transient, pinned as a LIVE observable (not merely a comment) ===
        // During the break-fade the boulder is STILL VISIBLE (renderer enabled) yet reads GREEN to the ghost, because
        // registration keys on IsMineable, not IsVisible. This is INTENDED + self-healing (~2.3s). Recording it here
        // documents the exact signal the Sponsor would flag at a soak; the IsMineable -> IsVisible re-key is OUT OF
        // SCOPE for 86cattf8y (a soak-gated design call — do NOT change the keying here).
        [UnityTest]
        public IEnumerator StillVisibleAfterBreak_ReadsGreen_TheDesignedTransient()
        {
            yield return null; // Start registers the standing boulder
            Assert.IsTrue(Blocked(), "precondition: the standing boulder blocks");
            Assert.IsTrue(_boulderRenderer.enabled, "precondition: the standing boulder is visible");

            _mine.Mine();
            _mine.Mine();
            Assert.IsTrue(_mine.IsBrokenOn(0), "the boulder is broken");

            yield return Step(); // Update Syncs the break -> unregister; renderers stay ON until the fade completes
            Assert.IsTrue(_boulderRenderer.enabled,
                "the just-broken boulder is STILL VISIBLE during the break-fade (renderers disable only at fade end)");
            Assert.IsFalse(Blocked(),
                "yet the STILL-VISIBLE boulder reads GREEN — the DESIGNED transient: registration keys on IsMineable, " +
                "NOT IsVisible (86cattf8y AC2, recorded not changed; IsVisible is the visually-honest re-key if the " +
                "Sponsor flags the transient at a soak)");
        }
    }
}
