using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC4 guard for the two-drawer SettingsPanel split (ticket 86cah8ukr): F1 = the small PLAYER-facing
    /// Settings panel / F3 = the full DEV console. Both drawers filter the ONE registry by
    /// <see cref="SettingsCategory"/> — "route views, don't re-bind". This class pins the two decisions that
    /// split relies on, and it does so against the EXACT functions the panel calls (single source of truth):
    ///
    ///   (a) DRAWER ROUTING — <see cref="SettingsPanel.BuildRows"/> routes each row with
    ///       <c>SettingsCategory.IsPlayer(entry.Id)</c> (player → F1) / else → F3. So asserting
    ///       <see cref="SettingsCategory.IsPlayer"/> over a REAL built registry tests the panel's own routing
    ///       predicate. The bug class this pins: a dev-only knob LEAKING into the player panel (extra id) OR a
    ///       player dial BURIED in the dev console (missing id) — caught by an exact set-equality assert.
    ///
    ///   (b) CONDITIONAL DECAY-SLIDER VISIBILITY — <see cref="SettingsPanel.ApplyConditionalVisibility"/> shows
    ///       each per-need decay-rate slider iff its on/off toggle is ON, via
    ///       <see cref="SettingsCategory.IsDecaySliderVisible"/>. The test drives that SAME function (not a
    ///       re-implemented proxy) for all three needs (warmth / hunger / thirst), ON and OFF.
    ///
    /// Pure C# + a scene-free registry built from bare real components (the SettingsCatalog*Tests precedent) —
    /// no scene / UIDocument / Update needed, so this is a fast EditMode guard.
    /// </summary>
    public class SettingsCategoryTests
    {
        private GameObject _camGo, _playerGo, _warmthGo, _hungerGo, _thirstGo, _invGo;
        private OrbitCamera _orbit;
        private WasdMovement _wasd;
        private WarmthNeed _warmth;
        private HungerNeed _hunger;
        private ThirstNeed _thirst;
        private Inventory _inv;

        // The Sponsor-confirmed (2026-07-03 walkthrough) player-facing set — belt slots + inventory stack size,
        // and the three needs' on/off toggles + their decay-rate sliders. EVERYTHING else is dev console (F3).
        private static readonly string[] ExpectedPlayerIds =
        {
            SettingsCatalog.BeltSlotsId,       // belt_slots
            SettingsCatalog.StackSizeId,       // inventory_stack_size
            SettingsCatalog.WarmthEnabledId,   // warmth_enabled
            SettingsCatalog.HungerEnabledId,   // hunger_enabled
            SettingsCatalog.ThirstEnabledId,   // thirst_enabled
            SettingsCatalog.WarmthDecayId,     // warmth_decay_rate
            SettingsCatalog.HungerDecayId,     // hunger_decay_rate
            SettingsCatalog.ThirstDecayId,     // thirst_decay_rate
        };

        [SetUp]
        public void SetUp()
        {
            _camGo = new GameObject("Cam");
            _camGo.AddComponent<Camera>();
            _orbit = _camGo.AddComponent<OrbitCamera>();

            _playerGo = new GameObject("Player");
            _playerGo.AddComponent<UnityEngine.AI.NavMeshAgent>(); // WasdMovement [RequireComponent(NavMeshAgent)]
            _wasd = _playerGo.AddComponent<WasdMovement>();

            _warmthGo = new GameObject("Warmth"); _warmth = _warmthGo.AddComponent<WarmthNeed>();
            _hungerGo = new GameObject("Hunger"); _hunger = _hungerGo.AddComponent<HungerNeed>();
            _thirstGo = new GameObject("Thirst"); _thirst = _thirstGo.AddComponent<ThirstNeed>();

            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();
            _ = _inv.Model; // touch so EnsureModel built it at the default counts (belt/stack rows read live)
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camGo);
            Object.DestroyImmediate(_playerGo);
            Object.DestroyImmediate(_warmthGo);
            Object.DestroyImmediate(_hungerGo);
            Object.DestroyImmediate(_thirstGo);
            Object.DestroyImmediate(_invGo);
            // The bool toggles persist to PlayerPrefs on SetValue — never leak a dialed state into a sibling test.
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.WarmthEnabledId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.HungerEnabledId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.ThirstEnabledId);
        }

        // The 12-arg Build with the player-facing targets wired (warmth/hunger/thirst/inventory + orbit/wasd);
        // chop/stone/held/berry left null (the catalog null-skips). Registers ALL EIGHT player ids plus a broad
        // set of dev rows (zoom/pitch/walk/run/air-control, water-scoop, berry-restore, inventory-slots, …).
        private SettingsRegistry BuildFull()
            => SettingsCatalog.Build(_orbit, _wasd, _thirst, null, null, null, null, null, _hunger, null, _inv, _warmth);

        // ---- (a) DRAWER ROUTING --------------------------------------------------------------------------

        [Test]
        public void PlayerFacingSet_IsExactlyTheEightSponsorConfirmedIds()
        {
            // The classification map itself: EXACTLY the 8 Sponsor-confirmed player ids, no more, no fewer.
            Assert.AreEqual(ExpectedPlayerIds.Length, SettingsCategory.PlayerCount,
                "the player-facing allowlist must hold exactly the 8 Sponsor-confirmed ids (2026-07-03)");
            foreach (var id in ExpectedPlayerIds)
            {
                Assert.IsTrue(SettingsCategory.IsPlayer(id), $"'{id}' must be PLAYER-facing (F1)");
                Assert.IsFalse(SettingsCategory.IsDev(id), $"'{id}' must NOT be dev-console (F3)");
            }
        }

        [Test]
        public void BuiltRegistry_RoutesExactlyThePlayerIdsToF1_EverythingElseToF3()
        {
            // The panel's BuildRows routes each row with SettingsCategory.IsPlayer(entry.Id). Over a REAL built
            // registry, the set of player-routed ids must EQUAL the expected 8 — this single assert catches both
            // a dev knob LEAKING into the player panel (a surplus id) AND a player dial BURIED in dev (a missing
            // id). Every other registered row must route to F3 (IsDev).
            var reg = BuildFull();

            var playerRouted = new HashSet<string>();
            foreach (var entry in reg.Entries)
            {
                bool isPlayer = SettingsCategory.IsPlayer(entry.Id);
                bool isDev = SettingsCategory.IsDev(entry.Id);
                Assert.AreNotEqual(isPlayer, isDev,
                    $"'{entry.Id}' must route to EXACTLY one drawer (IsPlayer and IsDev are mutually exclusive)");
                if (isPlayer) playerRouted.Add(entry.Id);
            }

            CollectionAssert.AreEquivalent(ExpectedPlayerIds, playerRouted,
                "the F1 player drawer must receive EXACTLY the 8 Sponsor-confirmed ids from a real built registry " +
                "(no dev knob leaking in, no player dial buried in the dev console)");
        }

        [Test]
        public void SponsorFlaggedDevDefaults_FallToTheDevConsole()
        {
            // The three Sponsor-flagged 2026-07-03 defaults (inventory_slots, walk/run speed, UI text scale) plus
            // representative dev rows must be F3 (dev) — they fall to dev by being ABSENT from the player allowlist.
            // Pure classification (id → drawer), so no built registry is needed here.
            foreach (var id in new[]
            {
                SettingsCatalog.InventorySlotsId,   // Sponsor-flagged → DEV (NOT belt/stack)
                SettingsCatalog.WalkSpeedId,        // Sponsor-flagged → DEV
                SettingsCatalog.RunSpeedId,         // Sponsor-flagged → DEV
                SettingsCatalog.ConsoleTextScaleId, // Sponsor-flagged → DEV
                SettingsCatalog.WaterScoopId,       // a need's non-difficulty knob → DEV
                SettingsCatalog.BerryRestoreId,     // a need's non-difficulty knob → DEV
                SettingsCatalog.ZoomRangeId,        // camera → DEV
            })
                Assert.IsTrue(SettingsCategory.IsDev(id), $"'{id}' must be DEV-console (F3), not player-facing");

            // inventory_slots is DEV while belt_slots + stack_size are PLAYER — the split the walkthrough locked.
            Assert.IsFalse(SettingsCategory.IsPlayer(SettingsCatalog.InventorySlotsId),
                "inventory_slots is dev-console; only belt slots + stack size are player-facing");
        }

        // ---- (b) CONDITIONAL DECAY-SLIDER VISIBILITY -----------------------------------------------------

        [Test]
        public void DecaySliderGates_MapEachNeedSliderToItsOwnToggle()
        {
            Assert.AreEqual(SettingsCatalog.WarmthEnabledId, SettingsCategory.GateToggleFor(SettingsCatalog.WarmthDecayId),
                "the warmth decay slider is gated by the warmth on/off toggle");
            Assert.AreEqual(SettingsCatalog.HungerEnabledId, SettingsCategory.GateToggleFor(SettingsCatalog.HungerDecayId),
                "the hunger decay slider is gated by the hunger on/off toggle");
            Assert.AreEqual(SettingsCatalog.ThirstEnabledId, SettingsCategory.GateToggleFor(SettingsCatalog.ThirstDecayId),
                "the thirst decay slider is gated by the thirst on/off toggle");
        }

        [Test]
        public void BothEndpointsOfEachDecayGate_ArePlayerFacing()
        {
            // The show/hide stays entirely within the F1 view — both the decay slider AND its gating toggle must
            // be player-facing, else the conditional visibility would span drawers (a mis-route).
            foreach (var pair in SettingsCategory.DecaySliderGates)
            {
                Assert.IsTrue(SettingsCategory.IsPlayer(pair.Key), $"decay slider '{pair.Key}' must be player-facing");
                Assert.IsTrue(SettingsCategory.IsPlayer(pair.Value), $"gating toggle '{pair.Value}' must be player-facing");
            }
        }

        [Test]
        public void EachDecaySlider_ShownWhenItsNeedIsOn_HiddenWhenOff()
        {
            // Drives the SAME SettingsCategory.IsDecaySliderVisible the panel's ApplyConditionalVisibility calls,
            // for all three needs, ON and OFF. The bug class this pins: a decay slider that stays visible for a
            // disabled need (dead knob) OR one that hides for the wrong need (crossed gates).
            var reg = BuildFull();

            var cases = new (string sliderId, string toggleId)[]
            {
                (SettingsCatalog.WarmthDecayId, SettingsCatalog.WarmthEnabledId),
                (SettingsCatalog.HungerDecayId, SettingsCatalog.HungerEnabledId),
                (SettingsCatalog.ThirstDecayId, SettingsCatalog.ThirstEnabledId),
            };

            foreach (var (sliderId, toggleId) in cases)
            {
                var toggle = (BoolSettingEntry)reg.Get(toggleId);
                Assert.IsNotNull(toggle, $"the '{toggleId}' toggle must be registered");

                toggle.SetValue(true);
                Assert.IsTrue(SettingsCategory.IsDecaySliderVisible(reg, sliderId),
                    $"'{sliderId}' must be SHOWN while '{toggleId}' is ON");

                toggle.SetValue(false);
                Assert.IsFalse(SettingsCategory.IsDecaySliderVisible(reg, sliderId),
                    $"'{sliderId}' must be HIDDEN while '{toggleId}' is OFF (a disabled need has no rate to tune)");

                // Flipping it back ON re-reveals the slider (live show/hide on toggle change).
                toggle.SetValue(true);
                Assert.IsTrue(SettingsCategory.IsDecaySliderVisible(reg, sliderId),
                    $"'{sliderId}' must re-appear when '{toggleId}' flips back ON");
            }
        }

        [Test]
        public void ToggleFlip_OnlyMovesItsOwnDecaySlider_NotTheOtherNeeds()
        {
            // Cross-talk guard: turning warmth OFF must hide ONLY the warmth decay slider — hunger + thirst
            // sliders stay shown (their toggles are still ON). Catches a crossed-gate wiring bug.
            var reg = BuildFull();
            ((BoolSettingEntry)reg.Get(SettingsCatalog.WarmthEnabledId)).SetValue(false);
            ((BoolSettingEntry)reg.Get(SettingsCatalog.HungerEnabledId)).SetValue(true);
            ((BoolSettingEntry)reg.Get(SettingsCatalog.ThirstEnabledId)).SetValue(true);

            Assert.IsFalse(SettingsCategory.IsDecaySliderVisible(reg, SettingsCatalog.WarmthDecayId),
                "warmth OFF hides the warmth decay slider");
            Assert.IsTrue(SettingsCategory.IsDecaySliderVisible(reg, SettingsCatalog.HungerDecayId),
                "hunger stays ON → its decay slider stays shown (no cross-talk from warmth)");
            Assert.IsTrue(SettingsCategory.IsDecaySliderVisible(reg, SettingsCatalog.ThirstDecayId),
                "thirst stays ON → its decay slider stays shown (no cross-talk from warmth)");
        }

        [Test]
        public void NonDecaySlider_IsNeverGateHidden()
        {
            // A row that is NOT a conditionally-visible decay slider (e.g. walk speed) has no gate → always shown.
            var reg = BuildFull();
            Assert.IsNull(SettingsCategory.GateToggleFor(SettingsCatalog.WalkSpeedId),
                "walk speed is not a gated decay slider (no gate mapping)");
            Assert.IsTrue(SettingsCategory.IsDecaySliderVisible(reg, SettingsCatalog.WalkSpeedId),
                "a non-decay-slider row is always visible (never gate-hidden)");
        }

        [Test]
        public void DecaySlider_WithNoRegisteredToggle_IsHidden_SafeDefault()
        {
            // If the gating toggle isn't registered (e.g. a warmth-less rig), the decay slider hides — the safe
            // default (a slider whose gate isn't live can't be tuned meaningfully). Build with NO warmth target.
            var reg = SettingsCatalog.Build(_orbit, _wasd, _thirst, null, null, null, null, null, _hunger, null, _inv, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.WarmthEnabledId),
                "no warmth target → the warmth toggle is not registered (precondition for this guard)");
            Assert.IsFalse(SettingsCategory.IsDecaySliderVisible(reg, SettingsCatalog.WarmthDecayId),
                "a decay slider whose gating toggle is unregistered must hide (safe default, no dead knob)");
        }
    }
}
