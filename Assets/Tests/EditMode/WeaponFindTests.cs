using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guards for FIND-IN-WORLD weapon acquisition (ticket 86cah7y5b AC6) — the SECOND acquisition
    /// route beside craft. Blocking lane; the component-level renderer/scene table lives in
    /// WeaponFindPlayModeTests and the shipped-frame proof in the -verifyWeaponFind capture gate.
    ///
    /// These bite on:
    ///   • the loot contract — CanLoot present/spent, ONE canonical id per loot, a second E is a no-op;
    ///   • the id being a REAL catalog weapon in BOTH catalogs (no orphan id — the WeaponSetTests shape);
    ///   • the IRON-BLADE held-visual map, including an explicit RE-STATEMENT OF THE PRE-FIX DEFECT (the
    ///     stone/iron AND wood tables both return -1 for sword_iron, so the iron fallback is demonstrably
    ///     what maps it — the same proof shape the wood soak-3 test uses);
    ///   • the shared looter truth-table still refusing with a modal panel open (extended, NOT forked);
    ///   • the pure cue/feel maths (bob + eased arc) so nobody can quietly regress the ease to a lerp;
    ///   • the per-tier findability dial + its DEAD-KNOB guard (the PopulateBoar class).
    /// </summary>
    public class WeaponFindTests
    {
        private static Inventory NewInventory(out GameObject go)
        {
            go = new GameObject("Inventory");
            return go.AddComponent<Inventory>();
        }

        // Build a bare find + its inventory. No stump/mesh — the loot contract is geometry-free.
        private static WorldWeaponFind NewFind(out GameObject go, out Inventory inv, out GameObject invGo)
        {
            inv = NewInventory(out invGo);
            go = new GameObject("WeaponFind");
            var visual = new GameObject("FindWeapon");
            visual.transform.SetParent(go.transform, false);
            var find = go.AddComponent<WorldWeaponFind>();
            find.inventory = inv;
            find.visual = visual.transform;
            find.itemId = ItemCatalog.SwordIronId;
            return find;
        }

        // ============================================================================================
        // AC6 — the loot contract.
        // ============================================================================================

        [Test]
        public void Find_CanLoot_IsTrueWhilePresent_AndFalseOnceLooted()
        {
            var find = NewFind(out var go, out var inv, out var invGo);
            try
            {
                Assert.IsTrue(find.CanLoot, "a resting find with an inventory wired is loot-able");
                Assert.IsTrue(find.IsAvailable, "…and reports itself available");

                Assert.IsTrue(find.TryLoot(inv), "E loots it");

                Assert.IsFalse(find.CanLoot,
                    "a looted find is NOT loot-able — the looter's nearest-in-range resolve SKIPS it, so a " +
                    "second E finds nothing here (never 'loots nothing' off a spent find)");
                Assert.IsFalse(find.IsAvailable);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(invGo); }
        }

        [Test]
        public void Find_TryLoot_AddsTheCanonicalItemCatalogId_ExactlyOnce()
        {
            var find = NewFind(out var go, out var inv, out var invGo);
            try
            {
                Assert.AreEqual(0, inv.Model.CountItem(ItemCatalog.SwordIronId), "precondition: no sword owned");

                Assert.IsTrue(find.TryLoot(inv), "the first E loots the find");
                Assert.AreEqual(1, inv.Model.CountItem(ItemCatalog.SwordIronId),
                    "exactly ONE canonical sword_iron entered the inventory — never a parallel 'found_sword' id");

                Assert.IsFalse(find.TryLoot(inv), "a SECOND E on the same find is a clean no-op (returns false)");
                Assert.AreEqual(1, inv.Model.CountItem(ItemCatalog.SwordIronId),
                    "…and adds NOTHING (AC6: 'a second E does nothing')");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(invGo); }
        }

        [Test]
        public void Find_TryLoot_LandsTheWeaponOnTheBelt_LikeACraftedWeapon()
        {
            var find = NewFind(out var go, out var inv, out var invGo);
            try
            {
                Assert.IsTrue(find.TryLoot(inv));
                bool onBelt = false;
                var belt = inv.Model.BeltSlots;
                for (int i = 0; i < belt.Count; i++)
                    if (!belt[i].IsEmpty && belt[i].Def.Id == ItemCatalog.SwordIronId) { onBelt = true; break; }
                Assert.IsTrue(onBelt,
                    "the found weapon lands on the BELT (AddToolToBelt — the SAME seam PickUpAxe/PickUpSpear " +
                    "and the craft output use), so it equips exactly like a crafted weapon (AC1)");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(invGo); }
        }

        [Test]
        public void Find_TryLoot_AlreadyOwned_IsDeclined_AndTheWeaponStaysInTheWorld()
        {
            var find = NewFind(out var go, out var inv, out var invGo);
            try
            {
                Assert.IsTrue(inv.PickUpWeapon(ItemCatalog.SwordIronId), "the player already crafted an iron sword");

                Assert.IsFalse(find.TryLoot(inv), "the find declines — one sword is already owned (idempotent)");
                Assert.IsTrue(find.CanLoot,
                    "a DECLINED loot must NOT consume the find — it stays resting in the stump so the player " +
                    "can come back for it (the StickProp declined-loot contract)");
                Assert.AreEqual(1, inv.Model.CountItem(ItemCatalog.SwordIronId), "no duplicate was minted");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(invGo); }
        }

        [Test]
        public void Find_TryLoot_UnknownId_IsDeclined_NeverMintsAParallelItem()
        {
            var find = NewFind(out var go, out var inv, out var invGo);
            try
            {
                find.itemId = "definitely_not_a_catalog_id";
                Assert.IsFalse(find.TryLoot(inv), "an id that is not in the catalog is declined, not invented");
                Assert.IsTrue(find.CanLoot, "…and the find is not consumed");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(invGo); }
        }

        [Test]
        public void Find_DefaultItemId_ResolvesInBothCatalogs_NoOrphanId()
        {
            // The WeaponSetTests shape: the find's id must resolve to a real ItemDef AND a real WeaponDef, or the
            // looted piece would land in the belt but resolve no weapon (no swing, no held mesh).
            var invGo = new GameObject("Inventory");
            var findGo = new GameObject("WeaponFind");
            var weapons = ScriptableObject.CreateInstance<WeaponCatalog>();
            try
            {
                var inv = invGo.AddComponent<Inventory>();
                var find = findGo.AddComponent<WorldWeaponFind>();

                Assert.AreEqual(ItemCatalog.SwordIronId, find.itemId,
                    "the shipped default find is sword_iron (Sponsor decision 2026-07-27)");
                Assert.AreEqual(WeaponCatalog.SwordIronId, ItemCatalog.SwordIronId,
                    "the ItemCatalog and WeaponCatalog ids for the iron sword are the SAME string");

                Assert.IsNotNull(inv.Catalog.ById(find.itemId), "the find's id resolves to a real ItemDef");

                weapons.BuildDefaults();
                var def = weapons.ById(find.itemId);
                Assert.IsNotNull(def, "the find's id resolves to a real WeaponDef (no orphan id)");
                Assert.Greater(def.Damage, 0f, "…with real combat attributes behind it");
            }
            finally
            {
                Object.DestroyImmediate(findGo);
                Object.DestroyImmediate(invGo);
                Object.DestroyImmediate(weapons);
            }
        }

        [Test]
        public void Find_PromptLabel_ComesFromTheSharedLootPromptWidget()
        {
            var find = NewFind(out var go, out var inv, out var invGo);
            try
            {
                find.displayName = WorldWeaponFind.DefaultDisplayName;
                Assert.AreEqual("Press E to pick up an iron sword",
                    LootPrompt.BuildLabel(find, KeyCode.E),
                    "the find rides the EXISTING LootPrompt seam (BuildLabel + the default 'pick up' GatherVerb) " +
                    "— no second prompt widget is authored");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(invGo); }
        }

        [Test]
        public void Find_LootRange_IsInTheLogPileBerryBand()
        {
            var go = new GameObject("WeaponFind");
            try
            {
                var find = go.AddComponent<WorldWeaponFind>();
                Assert.GreaterOrEqual(find.LootRange, 1.2f, "AC3 band floor — you walk up to it");
                Assert.LessOrEqual(find.LootRange, 2.0f, "AC3 band ceiling — never loot-able from across the clearing");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ============================================================================================
        // AC6 — the shared looter truth-table still refuses with a modal panel open (EXTENDED, not forked).
        // ============================================================================================

        [Test]
        public void Looter_TruthTable_StillRefusesTheFind_WithAModalPanelOpen()
        {
            // The find adds NO second pickup path, so it inherits PickableLooter.ShouldLootOnKey verbatim.
            Assert.IsTrue(PickableLooter.ShouldLootOnKey(inRange: true, uiPanelOpen: false),
                "in range + no modal panel -> the find loots on E");
            Assert.IsFalse(PickableLooter.ShouldLootOnKey(inRange: true, uiPanelOpen: true),
                "in range but a modal gameplay-UI panel owns the screen -> E must NOT loot the find");
            Assert.IsFalse(PickableLooter.ShouldLootOnKey(inRange: false, uiPanelOpen: false),
                "nothing in range -> E is a harmless no-op (never proximity-auto, never a phantom loot)");
        }

        // ============================================================================================
        // The IRON-BLADE held-visual map — the shipped defect this ticket had to close to satisfy AC6.
        // ============================================================================================

        [Test]
        public void IronSelectionIndexFor_MapsEachIronBlade_ToItsIronIndex()
        {
            Assert.AreEqual(HeldWeaponCycleDebug.AxeIronFamilyIndex,
                HeldWeaponCycleDebug.IronSelectionIndexFor(true, false, false, false), "iron axe");
            Assert.AreEqual(HeldWeaponCycleDebug.DaggerIronFamilyIndex,
                HeldWeaponCycleDebug.IronSelectionIndexFor(false, true, false, false), "iron dagger");
            Assert.AreEqual(HeldWeaponCycleDebug.SwordIronFamilyIndex,
                HeldWeaponCycleDebug.IronSelectionIndexFor(false, false, true, false), "iron sword — the find");
            Assert.AreEqual(HeldWeaponCycleDebug.SpearIronFamilyIndex,
                HeldWeaponCycleDebug.IronSelectionIndexFor(false, false, false, true), "iron spear");
            Assert.AreEqual(-1, HeldWeaponCycleDebug.IronSelectionIndexFor(false, false, false, false),
                "no iron blade selected -> -1 (the stone/wood paths or the gate hide the seat)");
        }

        [Test]
        public void IronSelectionIndexFor_Priority_IsPinned()
        {
            // Only one belt slot is ever selected in play; pin the deterministic tie-break anyway.
            Assert.AreEqual(HeldWeaponCycleDebug.AxeIronFamilyIndex,
                HeldWeaponCycleDebug.IronSelectionIndexFor(true, true, true, true), "iron axe wins");
            Assert.AreEqual(HeldWeaponCycleDebug.DaggerIronFamilyIndex,
                HeldWeaponCycleDebug.IronSelectionIndexFor(false, true, true, true), "iron dagger next");
            Assert.AreEqual(HeldWeaponCycleDebug.SwordIronFamilyIndex,
                HeldWeaponCycleDebug.IronSelectionIndexFor(false, false, true, true), "iron sword next");
        }

        [Test]
        public void FamilyContract_IronIndicesNameTheIronNodes()
        {
            // A WeaponNodeNames reorder without re-pinning these would render the WRONG weapon for a selected
            // iron blade — the crossed-visual class the spear/pickaxe/wood pins already guard.
            Assert.AreEqual("wpn_axe_iron_01",   HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.AxeIronFamilyIndex]);
            Assert.AreEqual("wpn_knife_iron_01", HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.DaggerIronFamilyIndex]);
            Assert.AreEqual("wpn_sword_iron_01", HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.SwordIronFamilyIndex]);
            Assert.AreEqual("wpn_spear_iron_01", HeldWeaponCycleDebug.WeaponNodeNames[HeldWeaponCycleDebug.SpearIronFamilyIndex]);
        }

        [Test]
        public void IronBladeSelected_UsedToBeEmptyHands_NowMapsToItsIronMesh()
        {
            // THE SHIPPED DEFECT, restated as an executable proof (the soak-3 wood test's shape). For each iron
            // BLADE: the stone/pickaxe table returns -1 AND the wood table returns -1 — that pair IS the
            // pre-fix empty-hands path — and only the iron fallback maps it. This also covers the CRAFTED iron
            // blades, which have shipped un-renderable since #294 ③.
            var cases = new (string id, int index, string label)[]
            {
                (ItemCatalog.AxeIronId,    HeldWeaponCycleDebug.AxeIronFamilyIndex,    "iron axe"),
                (ItemCatalog.DaggerIronId, HeldWeaponCycleDebug.DaggerIronFamilyIndex, "iron dagger"),
                (ItemCatalog.SwordIronId,  HeldWeaponCycleDebug.SwordIronFamilyIndex,  "iron sword"),
                (ItemCatalog.SpearIronId,  HeldWeaponCycleDebug.SpearIronFamilyIndex,  "iron spear"),
            };
            foreach (var (id, index, label) in cases)
            {
                var inv = NewInventory(out var go);
                try
                {
                    var slot = inv.Model.AddToolToBelt(inv.Catalog.ById(id));
                    Assert.IsTrue(slot.HasValue, label + " acquired onto the belt (a belt-eligible Tool)");
                    inv.Model.SelectBelt(slot.Value.Index);

                    int stoneTable = HeldWeaponCycleDebug.SelectionIndexFor(
                        inv.IsAxeSelectedInBelt, inv.IsSpearSelectedInBelt,
                        inv.IsPickaxeStoneSelectedInBelt, inv.IsPickaxeIronSelectedInBelt);
                    Assert.AreEqual(-1, stoneTable,
                        label + ": the stone/pickaxe SelectionIndexFor alone returns -1 (half the pre-fix path)");
                    Assert.AreEqual(-1, HeldWeaponCycleDebug.WoodSelectionIndexFor(inv),
                        label + ": the WOOD table also returns -1 — together these two ARE the EMPTY-HANDS defect");

                    Assert.AreEqual(index, HeldWeaponCycleDebug.IronSelectionIndexFor(inv),
                        label + " selected -> its IRON mesh index (this used to be nothing in hand)");
                    Assert.IsTrue(HeldWeaponCycleDebug.IsHeldVisualWeaponSelected(inv),
                        label + ": the SHARED held-visual predicate is now TRUE, so HeldAxe.ShouldShow shows the " +
                        "seat and CastawayFingerCurl closes the grip (they read the same predicate)");

                    inv.Model.SelectBelt((slot.Value.Index + 1) % inv.BeltSlotCount); // deselect
                    Assert.IsFalse(HeldWeaponCycleDebug.IsHeldVisualWeaponSelected(inv),
                        label + " owned but NOT selected -> the seat stays hidden (ownership is not selection)");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void IronBladeMap_DoesNotDisturbTheSoakedStoneWoodDecisions()
        {
            // The iron map is ADDITIVE: it must not change what the already-soaked tiers resolve to.
            Assert.AreEqual(HeldWeaponCycleDebug.AxeFamilyIndex,
                HeldWeaponCycleDebug.SelectionIndexFor(true, false, false, false), "stone axe unchanged");
            Assert.AreEqual(HeldWeaponCycleDebug.SpearFamilyIndex,
                HeldWeaponCycleDebug.SelectionIndexFor(false, true, false, false), "stone spear unchanged");
            Assert.AreEqual(HeldWeaponCycleDebug.PickaxeIronFamilyIndex,
                HeldWeaponCycleDebug.SelectionIndexFor(false, false, false, true),
                "the IRON PICKAXE still resolves through the ORIGINAL table — it was never part of the defect");
            Assert.AreEqual(HeldWeaponCycleDebug.SwordWoodFamilyIndex,
                HeldWeaponCycleDebug.WoodSelectionIndexFor(false, false, true, false, false), "wood sword unchanged");
        }

        // ============================================================================================
        // AC3/AC4 — the pure cue + feel maths.
        // ============================================================================================

        [Test]
        public void BobOffset_StaysWithinAmplitude_AndActuallyOscillates()
        {
            float amp = WorldWeaponFind.DefaultBobAmplitude, hz = WorldWeaponFind.DefaultBobHz;
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i <= 200; i++)
            {
                float t = i * (1f / hz) / 200f;                 // one full period
                float v = WorldWeaponFind.BobOffset(t, amp, hz, 0f);
                Assert.LessOrEqual(Mathf.Abs(v), amp + 1e-4f, "the bob never exceeds its amplitude");
                min = Mathf.Min(min, v); max = Mathf.Max(max, v);
            }
            Assert.Greater(max, amp * 0.9f, "the bob actually reaches near +amplitude (a live cue, not a flat line)");
            Assert.Less(min, -amp * 0.9f, "…and near -amplitude");
        }

        [Test]
        public void BobOffset_SeededPhase_DesynchronisesInstances()
        {
            // game-juice.md §1.5: a pool of collectibles must NOT pulse in sync.
            float a = WorldWeaponFind.BobOffset(0f, 0.05f, 0.8f, 0f);
            float b = WorldWeaponFind.BobOffset(0f, 0.05f, 0.8f, Mathf.PI * 0.5f);
            Assert.Greater(Mathf.Abs(a - b), 1e-4f,
                "two finds with different seeded phases are at different bob points (never a synchronised pulse)");
        }

        [Test]
        public void BobOffset_ZeroAmplitude_IsExactlyOff()
            => Assert.AreEqual(0f, WorldWeaponFind.BobOffset(1.234f, 0f, 0.8f, 0.7f),
                "amplitude 0 dials the cue fully OFF (exactly zero, not a rounding wobble)");

        // ---- AC3 CHANNEL 2: the sway. A cue must not rest on a SINGLE channel (quality bar) ----

        [Test]
        public void SwayOffset_StaysWithinAmplitude_AndActuallyOscillates()
        {
            float minD = float.MaxValue, maxD = float.MinValue;
            for (int i = 0; i <= 400; i++)
            {
                float d = WorldWeaponFind.SwayOffset(i * 0.01f, WorldWeaponFind.DefaultSwayDegrees,
                                                     WorldWeaponFind.DefaultSwayHz, 0f);
                Assert.LessOrEqual(Mathf.Abs(d), WorldWeaponFind.DefaultSwayDegrees + 1e-4f,
                    "the sway never exceeds its authored amplitude — a few degrees of play in the split, " +
                    "never a spinning collectible (that would break the driven-into-the-stump anchor)");
                minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d);
            }
            Assert.Greater(maxD - minD, WorldWeaponFind.DefaultSwayDegrees,
                "channel 2 must actually MOVE across a period — a cue channel that never varies is not a channel");
        }

        [Test]
        public void SwayOffset_ZeroAmplitude_IsExactlyOff()
        {
            Assert.AreEqual(0f, WorldWeaponFind.SwayOffset(1.23f, 0f, WorldWeaponFind.DefaultSwayHz, 0.4f),
                "channel 2 can be dialled fully off without disturbing channel 1");
            Assert.AreEqual(0f, WorldWeaponFind.SwayOffset(1.23f, WorldWeaponFind.DefaultSwayDegrees, 0f, 0.4f));
        }

        [Test]
        public void TheTwoCueChannels_AreIndependent_NotOneMotionRestated()
        {
            // THE POINT OF HAVING TWO CHANNELS: if bob and sway ran at the same frequency they would rise and
            // fall together and the player would read ONE motion — a single channel wearing two hats, which
            // fails the same bar a bob-only cue fails. Non-harmonic frequencies make them drift in and out of
            // phase, so at least one channel is always mid-stroke when the other is at a turning point.
            Assert.AreNotEqual(WorldWeaponFind.DefaultBobHz, WorldWeaponFind.DefaultSwayHz,
                "the two channels must not share a frequency");
            float ratio = WorldWeaponFind.DefaultBobHz / WorldWeaponFind.DefaultSwayHz;
            Assert.Greater(Mathf.Abs(ratio - Mathf.Round(ratio)), 0.15f,
                "…and must not be a simple integer harmonic either (" + ratio.ToString("F3") +
                "), or they re-fuse into one perceived pulse every cycle");

            // Empirical: over a long window the two channels' normalised outputs must genuinely diverge.
            float maxDivergence = 0f;
            for (int i = 0; i <= 600; i++)
            {
                float t = i * 0.01f;
                float bob = WorldWeaponFind.BobOffset(t, WorldWeaponFind.DefaultBobAmplitude,
                                                      WorldWeaponFind.DefaultBobHz, 0f)
                            / WorldWeaponFind.DefaultBobAmplitude;
                float sway = WorldWeaponFind.SwayOffset(t, WorldWeaponFind.DefaultSwayDegrees,
                                                        WorldWeaponFind.DefaultSwayHz, 0f)
                             / WorldWeaponFind.DefaultSwayDegrees;
                maxDivergence = Mathf.Max(maxDivergence, Mathf.Abs(bob - sway));
            }
            Assert.Greater(maxDivergence, 1.0f,
                "the channels must reach genuinely opposed phase at some point in the cycle — proof they are " +
                "two channels and not one motion applied twice");
        }

        [Test]
        public void SwayAmplitude_IsSmallEnoughToKeepTheDrivenIntoTheStumpAnchor()
        {
            Assert.Greater(WorldWeaponFind.DefaultSwayDegrees, 0f, "channel 2 ships ON by default");
            Assert.LessOrEqual(WorldWeaponFind.DefaultSwayDegrees, 10f,
                "a sword DRIVEN INTO a stump has a little play, not a swivel. Beyond ~10 degrees it reads as a " +
                "spinning pickup and the real-world anchor (lowpoly-quality.md §0) breaks");
        }

        [Test]
        public void ArcEase01_IsAnEaseOut_NotALinearLerp()
        {
            Assert.AreEqual(0f, WorldWeaponFind.ArcEase01(0f), 1e-5f, "starts at 0");
            Assert.AreEqual(1f, WorldWeaponFind.ArcEase01(1f), 1e-5f, "ends at 1");

            // The load-bearing property (game-juice.md §1.1 — "leaving a lerp linear is the single most common
            // 'feels cheap' defect"): an ease-OUT is strictly ABOVE the linear line in the interior.
            for (float t = 0.1f; t < 0.95f; t += 0.1f)
                Assert.Greater(WorldWeaponFind.ArcEase01(t), t + 1e-3f,
                    $"ArcEase01({t:0.0}) must sit above the linear line — a regression to a plain lerp turns this red");

            // …and monotonically increasing (never backtracks mid-flight).
            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float v = WorldWeaponFind.ArcEase01(i / 20f);
                Assert.Greater(v, prev - 1e-6f, "the arc never moves backwards");
                prev = v;
            }
        }

        [Test]
        public void ArcLift01_IsZeroAtBothEnds_AndPeaksInTheMiddle()
        {
            Assert.AreEqual(0f, WorldWeaponFind.ArcLift01(0f), 1e-5f, "no lift at the stump (endpoint preserved)");
            Assert.AreEqual(0f, WorldWeaponFind.ArcLift01(1f), 1e-5f, "no lift at the belt (endpoint preserved)");
            Assert.AreEqual(1f, WorldWeaponFind.ArcLift01(0.5f), 1e-5f,
                "peak lift at mid-flight — the piece rises OUT of the wood before it travels (the anchor: you " +
                "pull a buried blade UP first, you do not slide it sideways out of a stump)");
        }

        [Test]
        public void BobAmplitude_IsFarSmallerThanTheAuthoredEmbedDepth()
        {
            // The real-world anchor as a NUMBER: the bob must not be able to lift the blade clear of the stump.
            // (The scene's authored embed depth is 0.26u — MovementCameraScene.FindBladeEmbedDepth. The shipped
            // frame proof is the -verifyWeaponFind geometric assert + the side-profile capture; this is the
            // cheap early warning if someone dials the cue up without thinking about the wood.)
            Assert.Less(WorldWeaponFind.DefaultBobAmplitude, 0.26f * 0.5f,
                "the default bob is less than half the embed depth — the tip NEVER leaves the wood, so the find " +
                "reads as a sword stuck in a stump, not a hovering pickup");
        }

        // ============================================================================================
        // AC2/AC5 — the pool + the per-tier findability dial.
        // ============================================================================================

        [Test]
        public void Pool_DefaultCount_IsOnePerRegion_OnEveryTier()
        {
            var go = new GameObject("WeaponFindPool");
            try
            {
                var pool = go.AddComponent<WeaponFindPool>();
                Assert.AreEqual(1, WeaponFindPool.DefaultFindCount,
                    "AC2 default: ONE find per island region (Sponsor decision 2026-07-27)");
                Assert.AreEqual(WeaponFindPool.DefaultFindCount, pool.CountForTier(SurvivalNeed.DifficultyTier.Easy));
                Assert.AreEqual(WeaponFindPool.DefaultFindCount, pool.CountForTier(SurvivalNeed.DifficultyTier.Medium));
                Assert.AreEqual(WeaponFindPool.DefaultFindCount, pool.CountForTier(SurvivalNeed.DifficultyTier.Hard),
                    "AC5 default: the SAME count on all three tiers until the Sponsor asks for a rarity split");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Pool_ApplyDifficulty_ReadsTheActiveTiersMapEntry()
        {
            var go = new GameObject("WeaponFindPool");
            try
            {
                var pool = go.AddComponent<WeaponFindPool>();
                pool.easyFindCount = 3; pool.medFindCount = 2; pool.hardFindCount = 0;

                pool.ApplyDifficulty(SurvivalNeed.DifficultyTier.Easy);
                Assert.AreEqual(3, pool.ActiveFindCount, "easy reads the easy map entry");
                pool.ApplyDifficulty(SurvivalNeed.DifficultyTier.Medium);
                Assert.AreEqual(2, pool.ActiveFindCount, "medium reads the medium map entry");
                pool.ApplyDifficulty(SurvivalNeed.DifficultyTier.Hard);
                Assert.AreEqual(0, pool.ActiveFindCount, "hard reads the hard map entry (0 finds is legitimate)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SettingsRow_WeaponFindCount_IsRegisteredLive_AndIsDevConsole()
        {
            var go = new GameObject("WeaponFindPool");
            try
            {
                var pool = go.AddComponent<WeaponFindPool>();
                var reg = new SettingsRegistry();
                SettingsCatalog.PopulateWeaponFind(reg, pool);

                Assert.IsTrue(reg.Has(SettingsCatalog.WeaponFindCountId), "the findability row is registered (AC5)");
                Assert.IsTrue(reg.Get(SettingsCatalog.WeaponFindCountId).Available,
                    "it is LIVE (not a greyed '(soon)' hook) — it binds to a real WeaponFindPool");
                Assert.AreEqual("weapon_find_count", SettingsCatalog.WeaponFindCountId,
                    "the id follows the live convention (iron_ore_rarity is the closest sibling)");
                Assert.IsTrue(SettingsCategory.IsDev(SettingsCatalog.WeaponFindCountId),
                    "a difficulty-tuning dial is dev-console by default, like the ore-rarity sibling");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SettingsRow_WeaponFindCount_IsNotOnTheBasePopulate()
        {
            // The de-collision precedent (AC5 constraint): each feature adds its OWN Populate method; the base
            // Populate signature is NEVER grown.
            var reg = new SettingsRegistry();
            SettingsCatalog.Populate(reg, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.WeaponFindCountId),
                "the base Populate must NOT register the find row — PopulateWeaponFind owns it");
        }

        [Test]
        public void SettingsRow_WeaponFindCount_WritesBothActiveFieldAndActiveTierMap_NoDeadKnob()
        {
            // THE DEAD-KNOB GUARD (AC5's explicit constraint + the PopulateBoar note): if the setter wrote only
            // the active field, the next ApplyDifficulty would clobber the dialled value with the baked default.
            var poolGo = new GameObject("WeaponFindPool");
            var deathGo = new GameObject("DeathHandler");
            try
            {
                var death = deathGo.AddComponent<DeathHandler>();
                death.tier = SurvivalNeed.DifficultyTier.Hard;

                var pool = poolGo.AddComponent<WeaponFindPool>();
                pool.deathHandler = death;
                Assert.AreEqual(SurvivalNeed.DifficultyTier.Hard, pool.ActiveTier, "precondition: hard is active");

                var reg = new SettingsRegistry();
                SettingsCatalog.PopulateWeaponFind(reg, pool);
                var row = reg.Get(SettingsCatalog.WeaponFindCountId) as IntSettingEntry;
                Assert.IsNotNull(row, "the find row is an int stepper");

                row.SetValue(3);
                Assert.AreEqual(3, pool.hardFindCount,
                    "the dial wrote the ACTIVE TIER's per-tier map entry (hard), so the value BAKES into that preset");
                Assert.AreEqual(WeaponFindPool.DefaultFindCount, pool.medFindCount,
                    "…and left the other tiers alone");

                pool.ApplyDifficulty(SurvivalNeed.DifficultyTier.Hard);
                Assert.AreEqual(3, pool.ActiveFindCount,
                    "re-applying the tier reads back the DIALLED value — NOT a dead knob clobbered by the default");
            }
            finally { Object.DestroyImmediate(poolGo); Object.DestroyImmediate(deathGo); }
        }

        [Test]
        public void SettingsRow_WeaponFindCount_NullPool_RegistersNothing()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateWeaponFind(reg, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.WeaponFindCountId),
                "a find-less rig / bare test never null-refs (the catalog's standing null contract)");
        }

        // ============================================================================================
        // THE SHIPPED-BUILD DEFECT THIS TICKET ACTUALLY HIT — `pool=True find=False`.
        // ============================================================================================
        //
        // GROUND TRUTH (ci-out/verify-weaponfind.log line 40, the first -verifyWeaponFind run):
        //   [WeaponFindVerifyCapture] pool=True find=False looter=True prompt=True heldSeat=True
        // The ticket's own framing for that line ("the find-in-world wiring is absent — a build-side
        // regression signal") was WRONG. The wiring was perfect: the bootstrap authored 4 sites
        // (ci-out/bootstrap.log:3082 "authored 4 weapon-find site(s)"), the GameObjects shipped in the built
        // scene, and an EditMode probe of the serialized Boot.unity showed all four ACTIVE
        // (Include=4 Exclude=4). The finds were disabled at RUNTIME, by this feature's own AC5 settings row.
        //
        // THE CHAIN:
        //   • WeaponFindPool.activeFindCount ships as -1, a SENTINEL meaning "seed me from the tier map".
        //   • SettingsPanel.Start() registers the row (IntSettingEntry's ctor captures
        //     _default = Clamp(_get(), 0, 4)) and then calls Registry.ApplyAll() → Apply() → _set(Clamp(_get())).
        //   • Start order between SettingsPanel and WeaponFindPool is UNDEFINED. When the panel wins, _get()
        //     returns the raw -1 sentinel, Clamp(-1, 0, 4) == 0, and the setter writes 0 back through
        //     SetActiveFindCount — permanently DESTROYING the sentinel.
        //   • WeaponFindPool.Start() then sees activeFindCount == 0, which is NOT < 0, so it SKIPS the tier
        //     seed and applies 0 — disabling every authored site. FindObjectsInactive.Exclude sees none.
        //
        // A clamp band whose floor is 0 turns "unset" into "none of them". These two guards pin the fix:
        // the sentinel must never be OBSERVABLE raw, and the row's baked default must be the tier count.

        [Test]
        public void Pool_ActiveFindCount_NeverReportsTheRawSentinel_ToAnEarlyReader()
        {
            var go = new GameObject("WeaponFindPool");
            try
            {
                var pool = go.AddComponent<WeaponFindPool>();
                pool.activeFindCount = -1; // the SERIALIZED boot state, before Awake/Start resolves it

                Assert.AreEqual(WeaponFindPool.DefaultFindCount, pool.ActiveFindCount,
                    "ActiveFindCount must resolve the -1 sentinel to the active tier's count for ANY reader, " +
                    "including one that runs before this component's own Start. Leaking the raw -1 to the " +
                    "settings row is what clamped it to 0 and disabled every find in the shipped build " +
                    "(ci-out/verify-weaponfind.log: pool=True find=False)");
                Assert.GreaterOrEqual(pool.ActiveFindCount, WeaponFindPool.FindCountMin,
                    "a resolved count is always inside the dial band — never a negative that a clamp turns to 0");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SettingsRow_WeaponFindCount_BakedDefaultIsTheTierCount_NotZero_EvenWhenRegisteredBeforeBoot()
        {
            // Reproduces the losing race directly: register + ApplyAll while the pool is still un-booted.
            var go = new GameObject("WeaponFindPool");
            try
            {
                var pool = go.AddComponent<WeaponFindPool>();
                pool.activeFindCount = -1; // un-booted, exactly as the scene serializes it

                var reg = new SettingsRegistry();
                SettingsCatalog.PopulateWeaponFind(reg, pool);
                var row = reg.Get(SettingsCatalog.WeaponFindCountId) as IntSettingEntry;
                Assert.IsNotNull(row, "the find row is an int stepper");

                Assert.AreEqual(WeaponFindPool.DefaultFindCount, row.Default,
                    "the row's registration-time baked default must be the tier's find count. A default of 0 " +
                    "means the differs-badge lies AND `Reset to defaults` hands the Sponsor a world with no " +
                    "find in it — the same root cause as the shipped-build defect");

                reg.ApplyAll(); // SettingsPanel.Start does exactly this, and may run first

                Assert.AreEqual(WeaponFindPool.DefaultFindCount, pool.ActiveFindCount,
                    "an early ApplyAll must NOT be able to write the find count to 0. This is the assertion " +
                    "that would have caught `find=False` before the build");
                Assert.AreNotEqual(0, pool.ActiveFindCount,
                    "…and specifically never 0, which disables every authored site");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Pool_EarlySettingsApply_ThenBoot_StillLeavesAFindSTANDING_InTheWorld()
        {
            // The DELIVERABLE-level guard, in the exact shipped order: the settings row registers + applies
            // FIRST (SettingsPanel.Start winning the race), THEN the pool boots. The bug's signature was that
            // this sequence left ZERO active sites, so FindObjectsInactive.Exclude — the lens the looter and
            // the capture gate both use — found nothing at all. Asserting on the COUNT alone is not enough;
            // this asserts on the thing the player can actually walk up to.
            var root = new GameObject("WeaponFinds");
            var poolGo = new GameObject("WeaponFindPool");
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var site = new GameObject("WeaponFind");
                    site.transform.SetParent(root.transform, false);
                    site.AddComponent<WorldWeaponFind>();
                }

                var pool = poolGo.AddComponent<WeaponFindPool>();
                pool.findRoot = root.transform;
                pool.activeFindCount = -1; // un-booted, exactly as Boot.unity serializes it

                var reg = new SettingsRegistry();
                SettingsCatalog.PopulateWeaponFind(reg, pool);
                reg.ApplyAll();            // the losing race: the panel applies before the pool has resolved

                pool.InitialiseFindability(); // …and only now does the pool boot (Awake does this in the player)

                int active = 0;
                foreach (var site in pool.Sites)
                    if (site != null && site.gameObject.activeSelf) active++;

                Assert.AreEqual(WeaponFindPool.DefaultFindCount, active,
                    "after an early settings ApplyAll the pool must STILL leave the tier's find count standing " +
                    "in the world. Zero here is the shipped defect: every authored site switched off, so the " +
                    "looter and -verifyWeaponFind (both FindObjectsInactive.Exclude) resolve nothing — " +
                    "`pool=True find=False`");
                Assert.Greater(active, 0,
                    "…at least one find must survive, or the whole second acquisition route is invisible");
            }
            finally { Object.DestroyImmediate(poolGo); Object.DestroyImmediate(root); }
        }

        [Test]
        public void SettingsBand_CeilingMatchesTheAuthoredSitePool()
        {
            Assert.AreEqual(WeaponFindPool.FindCountMin, SettingsCatalog.WeaponFindCountMin);
            Assert.AreEqual(WeaponFindPool.FindCountMax, SettingsCatalog.WeaponFindCountMax,
                "the console ceiling IS the authored site-pool size — the row can never ask for more finds " +
                "than the seeded scatter authored places for");
        }
    }
}
