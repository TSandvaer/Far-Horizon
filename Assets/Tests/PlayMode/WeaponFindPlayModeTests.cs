using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;
using FarHorizon.Combat;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for FIND-IN-WORLD weapon acquisition (ticket 86cah7y5b AC6) — the FULL component seam,
    /// end to end: the find is discovered by the SHARED <see cref="PickableLooter"/>, E loots it exactly once,
    /// the weapon lands on the belt, and selecting it RENDERS IT IN HAND.
    ///
    /// THE CLASS THIS SUITE EXISTS FOR: "a PlayMode `renderer.enabled` assert has already let an
    /// invisible-in-hand weapon ship twice" (soak-3 / soak-4, 86cav8y74). So the in-hand assert here is a
    /// PAIR — <c>Renderer.enabled</c> (visibility) AND the holder's mesh identity (WHICH weapon) — mirroring
    /// HeldBeltWeaponVisualPlayModeTests, because the shipped defect was exactly a TRUE renderer showing the
    /// WRONG (or no) mesh. The third leg — that it is actually on screen in the built exe — is the
    /// -verifyWeaponFind capture gate; a headless PlayMode test cannot supply it, and pretending otherwise is
    /// how this class shipped twice.
    ///
    /// The seeded-scene half of AC6 (the find exists at its expected seeded position and survives a re-gen)
    /// is WeaponFindSceneTests, which opens the real Boot.unity — this suite builds a bare rig so the seam is
    /// tested independently of world content.
    ///
    /// NOTE the PlayMode lane is advisory in this repo ([[advisory-playmode-job-unreliable-soak-is-interaction-gate]]);
    /// the blocking guards are WeaponFindTests + WeaponFindSceneTests (EditMode) and the shipped capture gate.
    /// </summary>
    public class WeaponFindPlayModeTests
    {
        private GameObject _invGo, _playerGo, _seatGo, _findGo;
        private Inventory _inv;
        private PickableLooter _looter;
        private MeshRenderer _seatRenderer;
        private HeldWeaponCycleDebug _cycle;
        private HeldAxe _gate;
        private WorldWeaponFind _find;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = Vector3.zero;
            _looter = _playerGo.AddComponent<PickableLooter>();
            _looter.inventory = _inv;
            _looter.player = _playerGo.transform;

            // The held seat rig (the HeldBeltWeaponVisualPlayModeTests shape). Cycle FIRST so the gate's Awake
            // caches it — do NOT reorder (86cajt6jz: the reverse order exposed a permanently-null-cached gate).
            _seatGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(_seatGo.GetComponent<Collider>());
            _seatRenderer = _seatGo.GetComponent<MeshRenderer>();
            _cycle = _seatGo.AddComponent<HeldWeaponCycleDebug>();
            _gate = _seatGo.AddComponent<HeldAxe>();
            _gate.inventory = _inv;

            // The find, within arm's reach of the player at the origin.
            _findGo = new GameObject("WeaponFind");
            _findGo.transform.position = new Vector3(1.0f, 0f, 0f);
            var visual = new GameObject("FindWeapon");
            visual.transform.SetParent(_findGo.transform, false);
            _find = _findGo.AddComponent<WorldWeaponFind>();
            _find.inventory = _inv;
            _find.player = _playerGo.transform;
            _find.visual = visual.transform;
            _find.itemId = ItemCatalog.SwordIronId;
            _find.arcSeconds = 0.05f;   // keep the eased arc short so the tests do not idle on the beat
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_seatGo);
            Object.Destroy(_findGo);
        }

        private Mesh Holder() => _cycle.MeshHolder != null ? _cycle.MeshHolder.sharedMesh : null;

        private IEnumerator SelectSwordInBelt()
        {
            var belt = _inv.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
                if (!belt[i].IsEmpty && belt[i].Def.Id == ItemCatalog.SwordIronId) { _inv.Model.SelectBelt(i); break; }
            yield return null;
        }

        // ============================================================================================

        [UnityTest]
        public IEnumerator TheLooterDiscoversTheFind_AndTheSharedPromptNamesIt()
        {
            _looter.DiscoverPickables();
            yield return null;

            var nearest = _looter.NearestInRange();
            Assert.AreSame(_find, nearest,
                "the find is an ORDINARY IPickable the EXISTING looter discovers — no second pickup path");
            Assert.AreEqual("Press E to pick up an iron sword", LootPrompt.BuildLabel(nearest, _looter.lootKey),
                "the shared LootPrompt widget names it (no second prompt authored)");
        }

        [UnityTest]
        public IEnumerator StandingInRangeWithoutPressingE_LootsNothing()
        {
            _looter.DiscoverPickables();
            for (int i = 0; i < 5; i++) yield return null;   // stand there, several frames

            Assert.AreEqual(0, _inv.Model.CountItem(ItemCatalog.SwordIronId),
                "walking up is NOT enough — the find must never be picked up on proximity alone " +
                "([[active-input-not-proximity-auto-for-actions]]; the Sponsor's explicit 'press E' preference)");
            Assert.IsTrue(_find.CanLoot, "…and the find is still resting there");
        }

        [UnityTest]
        public IEnumerator ELoot_LandsTheSwordOnTheBelt_AndASecondPressDoesNothing()
        {
            _looter.DiscoverPickables();
            yield return null;

            _looter.RequestLoot();
            yield return null;
            yield return null;

            Assert.AreEqual(1, _inv.Model.CountItem(ItemCatalog.SwordIronId), "E loots exactly ONE sword_iron");
            Assert.IsFalse(_find.CanLoot, "the find is spent");

            _looter.RequestLoot();
            yield return null;
            yield return null;
            Assert.AreEqual(1, _inv.Model.CountItem(ItemCatalog.SwordIronId),
                "a SECOND E consumes nothing (AC6) — the spent find is skipped by the nearest-in-range resolve");
        }

        [UnityTest]
        public IEnumerator AfterLoot_TheSwordIsSelectableInTheBelt_AndRENDERS_IN_HAND()
        {
            // THE AC6 DELIVERABLE + the twice-shipped defect class. Asserts the PAIR (enabled AND which mesh),
            // never `renderer.enabled` alone.
            _looter.DiscoverPickables();
            yield return null;
            Assert.IsFalse(_seatRenderer.enabled, "spawn: nothing owned -> empty hands");

            _looter.RequestLoot();
            yield return null;
            yield return null;
            Assert.AreEqual(1, _inv.Model.CountItem(ItemCatalog.SwordIronId), "precondition: the sword was looted");

            yield return SelectSwordInBelt();
            yield return null;

            Assert.IsTrue(_inv.IsSwordIronSelectedInBelt, "the looted sword is SELECTABLE in the belt");
            Assert.IsTrue(_seatRenderer.enabled,
                "iron sword selected -> the held seat is SHOWN (before this ticket the iron blades satisfied NO " +
                "held-visual predicate, so this was EMPTY HANDS — for the CRAFTED iron sword too)");
            Assert.AreEqual(HeldWeaponCycleDebug.SwordIronFamilyIndex, _cycle.CurrentIndex,
                "…and the belt→held sync landed on the IRON SWORD family index");
            Assert.IsNotNull(Holder(), "…with a real mesh resolved from the committed lineup prefab");
            Assert.AreNotSame(_cycle.AxeOriginalMesh, Holder(),
                "…which is NOT the axe baseline — a renderer-only assert would false-green exactly this case " +
                "(the soak-3 / soak-4 invisible-in-hand class)");
        }

        [UnityTest]
        public IEnumerator DeselectingTheSword_HidesTheSeatAgain()
        {
            _looter.DiscoverPickables();
            yield return null;
            _looter.RequestLoot();
            yield return null;
            yield return null;
            yield return SelectSwordInBelt();
            yield return null;
            Assert.IsTrue(_seatRenderer.enabled, "precondition: the sword is selected + shown");

            // Select a different, EMPTY belt slot.
            var belt = _inv.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
                if (belt[i].IsEmpty) { _inv.Model.SelectBelt(i); break; }
            yield return null;

            Assert.IsFalse(_seatRenderer.enabled,
                "owned but NOT selected -> empty hands (AC4 semantics carry to the iron tier: selection, not " +
                "ownership, drives the held visual)");
        }

        // ============================================================================================
        // AC7 — THE PLACEMENT → MOTION GATE, over REAL FRAMES.
        //
        // The Sponsor's rule (2026-08-02 soak of PR #351): "motion cues are a property of PLACEMENT. An item
        // driven into or resting on something is STILL. An item lying loose may bob."
        //
        // These two tests are a PAIR and must stay one: a still-only test would also pass if the cue were
        // deleted outright, and a moves-only test is what shipped the defect. Together they pin the gate as a
        // gate — the SAME rig, the SAME frames, the SAME assertions, differing only in the placement field.
        // ============================================================================================

        [UnityTest]
        public IEnumerator AnEMBEDDEDFind_DoesNotMoveAtAll_TheSponsorsFloatingSwordDefect()
        {
            // THE REGRESSION GUARD for the soak rejection, verbatim: "the sword is floating, moving in the
            // stump." Note what this test does NOT rely on: it never reads `placement`, `bobAmplitude` or
            // `CueMoves`. It samples the transform the player actually sees, across frames, so it reds no matter
            // HOW the motion comes back — a re-enabled channel, an Update path that bypasses the Effective*
            // accessors, or some future component writing the same transform.
            _find.placement = FindPlacement.Embedded;
            Assert.Greater(_find.bobAmplitude, 0f,
                "PRECONDITION: the authored amplitude is NON-ZERO, so a green below proves the PLACEMENT " +
                "suppressed the motion — not that the test rig quietly had nothing to suppress");
            Assert.Greater(_find.swayDegrees, 0f);

            Vector3 restPos = _find.visual.localPosition;
            Quaternion restRot = _find.visual.localRotation;
            yield return null;   // let Update settle the still pose once

            float maxDrift = 0f, maxTwist = 0f;
            for (int i = 0; i < 60; i++)   // ~1s of real frames, past a full period of BOTH authored channels
            {
                yield return null;
                maxDrift = Mathf.Max(maxDrift, Mathf.Abs(_find.visual.localPosition.y - restPos.y));
                maxTwist = Mathf.Max(maxTwist, Quaternion.Angle(_find.visual.localRotation, restRot));
            }

            Assert.Less(maxDrift, 1e-4f,
                $"an EMBEDDED find must not translate at all (max drift {maxDrift:F6}u over 60 frames). A blade " +
                "buried in solid wood cannot rise and fall relative to it; when it does, the only reading left " +
                "is that it is hovering INSIDE the host — which is exactly what the soak rejected");
            Assert.Less(maxTwist, 1e-2f,
                $"…and must not rotate either (max {maxTwist:F4} deg). Suppressing one channel and leaving the " +
                "other still leaves a sword wobbling in a stump");
        }

        [UnityTest]
        public IEnumerator ALOOSEFind_STILLBobs_TheRuleIsAGateNotADeletion()
        {
            // The other side of the gate. Without this, "make the sword still" could be satisfied by deleting
            // the attract cue entirely — and the rule the Sponsor stated is explicitly not that: "an item lying
            // loose MAY bob." A loose find keeps the game-juice.md §1.5 collectible float-bob.
            _find.placement = FindPlacement.Loose;

            Vector3 startPos = _find.visual.localPosition;
            Quaternion startRot = _find.visual.localRotation;
            yield return null;

            float maxDrift = 0f, maxTwist = 0f;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                maxDrift = Mathf.Max(maxDrift, Mathf.Abs(_find.visual.localPosition.y - startPos.y));
                maxTwist = Mathf.Max(maxTwist, Quaternion.Angle(_find.visual.localRotation, startRot));
            }

            Assert.Greater(maxDrift, 1e-4f,
                "a LOOSE find still bobs — the placement rule GATES the cue on how the item sits, it does not " +
                "delete the cue from the codebase");
            Assert.Greater(maxTwist, 1e-2f, "…and still sways; both channels survive on a loose find");
        }

        [UnityTest]
        public IEnumerator TheCue_NeverDragsTheSiteRootOrTheLootPosition_OnEitherPlacement()
        {
            // AC3: the cue must not drag the loot reach around with it, or the prompt flickers at the boundary.
            // Asserted on the MOVING placement, because that is the only one where a bug could exist — a still
            // find trivially satisfies it, so testing it there would prove nothing.
            _find.placement = FindPlacement.Loose;
            Vector3 rootBefore = _findGo.transform.position;
            Vector3 lootPosBefore = ((IPickable)_find).LootPosition;

            for (int i = 0; i < 30; i++) yield return null;

            Assert.AreEqual(rootBefore, _findGo.transform.position, "the site ROOT never moves");
            Assert.AreEqual(lootPosBefore, ((IPickable)_find).LootPosition,
                "LootPosition is the stationary root — the loot reach does NOT wobble with the cue");
        }

        [UnityTest]
        public IEnumerator AfterTheArc_TheWeaponVisualIsGone_ButTheStumpStorySurvives()
        {
            // AC4: the piece leaves the world on an eased arc. The site root (which carries the stump in the
            // shipped scene) must NOT be destroyed or hidden — an empty stump keeps the story where it happened.
            _looter.DiscoverPickables();
            yield return null;
            _looter.RequestLoot();
            yield return null;
            yield return null;
            Assert.IsTrue(_find.IsArcing || !_find.visual.gameObject.activeSelf,
                "the loot starts the eased pickup arc immediately");

            float guard = 0f;
            while (_find.IsArcing && guard < 2f) { guard += Time.deltaTime; yield return null; }

            Assert.IsFalse(_find.visual.gameObject.activeSelf, "the weapon visual has left the world");
            Assert.IsTrue(_findGo.activeSelf,
                "…but the SITE (the stump host) remains — the find leaves an empty stump behind, not a hole in " +
                "the world where a prop used to be");
        }
    }
}
