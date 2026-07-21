using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// PER-CLASS WEAPON SWING asset/controller guards (ticket 86caffwv5 — attack animation per weapon). Pins the
    /// contract the runtime swing (CastawayCharacter.TriggerAttack + MeleeAttack) relies on so the bug CLASSES can't
    /// recur silently in headless CI:
    ///
    ///   1. Each of the 5 swing FBXs imports GENERIC (CreateFromThisModel — NOT Humanoid, the 86ca8rdkp cone trap)
    ///      with its renamed NON-looping clip (a swing is a one-shot strike).
    ///   2. The controller carries a WeaponClass INT selector + one AttackX state per class, each motion'd to its
    ///      clip, speed-driven by ChopSpeed, reached by AnyState→AttackX on (Chop && WeaponClass==N), returning to
    ///      Locomotion(Moving)/Idle(!Moving).
    ///   3. DOUBLE-FIRE GUARD (Devon-NIT #1): firing Chop with a given WeaponClass matches EXACTLY ONE state — the
    ///      reserved overhead 'Attack' state is NOT reachable by Chop (its ungated transition was removed), and no
    ///      two Chop-reachable states share a WeaponClass value.
    ///   4. The reserved overhead 'Attack' state (future sword HEAVY) is KEPT + motion'd to CastawayMelee.
    ///   5. The runtime↔editor mirrors (WeaponClass ints + per-class clip names) match — a drift would misroute the
    ///      swing or make MeleeClipLength return 0 (the cadence silently falls back).
    ///   6. The AnimationId→WeaponClass seam maps EVERY WeaponDef.AnimationId (no orphan swing id).
    /// </summary>
    public class AttackSwingControllerTests
    {
        // The 5 per-class swings: (FBX path, clip name, WeaponClass int, controller state name).
        private static readonly (string fbx, string clip, int weaponClass, string state)[] Swings =
        {
            (CharacterAssetGen.AttackAxeFbxPath,     CharacterAssetGen.AxeSwingClip,     CharacterAssetGen.WeaponClassAxe,     "AttackAxe"),
            (CharacterAssetGen.AttackPickaxeFbxPath, CharacterAssetGen.PickaxeSwingClip, CharacterAssetGen.WeaponClassPickaxe, "AttackPickaxe"),
            (CharacterAssetGen.AttackDaggerFbxPath,  CharacterAssetGen.DaggerStabClip,   CharacterAssetGen.WeaponClassDagger,  "AttackDagger"),
            (CharacterAssetGen.AttackSpearFbxPath,   CharacterAssetGen.SpearThrustClip,  CharacterAssetGen.WeaponClassSpear,   "AttackSpear"),
            (CharacterAssetGen.AttackSwordFbxPath,   CharacterAssetGen.SwordSlashClip,   CharacterAssetGen.WeaponClassSword,   "AttackSword"),
        };

        // 1 — each swing FBX imports GENERIC with its renamed NON-looping clip.
        [Test]
        public void EverySwingFbx_ImportsGeneric_WithANonLoopingClip()
        {
            foreach (var (fbx, clip, _, _) in Swings)
            {
                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                Assert.IsNotNull(importer, fbx + " must be importable");
                Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType,
                    fbx + " must import GENERIC (binds by transform path onto the live mesh — NOT Humanoid, the " +
                    "cone-explosion trap 86ca8rdkp)");
                Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, importer.avatarSetup,
                    fbx + " must create its own avatar (the Generic transform-path bind)");

                AnimationClip found = null;
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbx))
                    if (obj is AnimationClip c && !c.name.StartsWith("__preview__") && c.name.Contains(clip)) found = c;
                Assert.IsNotNull(found, fbx + " must contain a clip matching '" + clip +
                    "' (the Mixamo 'mixamo.com' take renamed on import)");
                Assert.IsFalse(found.isLooping, clip + " must NOT loop — a swing is a one-shot strike");
                Assert.Greater(found.length, 0f, clip + " must have a positive authored length");
            }
        }

        // 2 — controller has the WeaponClass int + one AttackX state per class (clip + speed + gated transition + return).
        [Test]
        public void Controller_HasWeaponClassSelector_AndOneAttackStatePerClass()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist at " + CharacterAssetGen.ControllerPath);

            bool hasWeaponClassInt = false;
            foreach (var p in controller.parameters)
                if (p.name == CharacterAssetGen.WeaponClassParam && p.type == AnimatorControllerParameterType.Int)
                    hasWeaponClassInt = true;
            Assert.IsTrue(hasWeaponClassInt, "the controller must have a WeaponClass INT param (the per-class selector)");

            var sm = controller.layers[0].stateMachine;
            AnimatorState idleState = null, locoState = null;
            var byName = new Dictionary<string, AnimatorState>();
            foreach (var cs in sm.states)
            {
                byName[cs.state.name] = cs.state;
                if (cs.state.name == "Idle") idleState = cs.state;
                if (cs.state.motion is BlendTree) locoState = cs.state;
            }
            Assert.IsNotNull(idleState, "Idle state must exist");
            Assert.IsNotNull(locoState, "Locomotion (blend-tree) state must exist");

            foreach (var (_, clip, weaponClass, stateName) in Swings)
            {
                Assert.IsTrue(byName.TryGetValue(stateName, out var state), "state '" + stateName + "' must exist");

                var motion = state.motion as AnimationClip;
                Assert.IsNotNull(motion, stateName + "'s motion must be an AnimationClip");
                Assert.IsTrue(motion.name.Contains(clip),
                    stateName + " must be motion'd to '" + clip + "' (got '" + motion.name + "')");
                Assert.IsTrue(state.speedParameterActive, stateName + "'s speed must be parameter-driven (tool-use speed)");
                Assert.AreEqual(CastawayCharacter.ChopSpeedParam, state.speedParameter,
                    stateName + "'s speedParameter must be ChopSpeed");

                // AnyState→state gated on (Chop AND WeaponClass==weaponClass).
                bool gated = false;
                foreach (var t in sm.anyStateTransitions)
                {
                    if (t.destinationState != state) continue;
                    bool onChop = false, onClass = false;
                    foreach (var c in t.conditions)
                    {
                        if (c.parameter == CastawayCharacter.ChopParam) onChop = true;
                        if (c.parameter == CastawayCharacter.WeaponClassParam &&
                            c.mode == AnimatorConditionMode.Equals &&
                            Mathf.RoundToInt(c.threshold) == weaponClass) onClass = true;
                    }
                    if (onChop && onClass) gated = true;
                }
                Assert.IsTrue(gated, "there must be an AnyState→" + stateName +
                    " gated on (Chop AND WeaponClass==" + weaponClass + ")");

                // Returns to Locomotion(Moving) AND Idle(!Moving) — the no-stall lesson.
                bool toLoco = false, toIdle = false;
                foreach (var t in state.transitions)
                {
                    bool movingIf = false, movingIfNot = false;
                    foreach (var c in t.conditions)
                    {
                        if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.If) movingIf = true;
                        if (c.parameter == "Moving" && c.mode == AnimatorConditionMode.IfNot) movingIfNot = true;
                    }
                    if (t.destinationState == locoState && movingIf) toLoco = true;
                    if (t.destinationState == idleState && movingIfNot) toIdle = true;
                }
                Assert.IsTrue(toLoco, stateName + " must return to Locomotion on (Moving)");
                Assert.IsTrue(toIdle, stateName + " must return to Idle on (!Moving)");
            }
        }

        // 3 — DOUBLE-FIRE GUARD (Devon-NIT #1): firing Chop with any WeaponClass value matches EXACTLY ONE state.
        [Test]
        public void ChopTrigger_MatchesExactlyOneStatePerWeaponClass_NoDoubleFire()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist");
            var sm = controller.layers[0].stateMachine;

            // For each Chop-reachable AnyState transition, record which WeaponClass value(s) it fires on. A transition
            // gated ONLY on Chop (no WeaponClass Equals condition) fires for ALL classes — the ungated double-fire the
            // reserved Attack state used to have. Each WeaponClass value 0..4 must be claimed by EXACTLY ONE state.
            var claimants = new Dictionary<int, List<string>>();
            for (int i = 0; i <= 4; i++) claimants[i] = new List<string>();

            foreach (var t in sm.anyStateTransitions)
            {
                bool onChop = false;
                int? equalsClass = null;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == CastawayCharacter.ChopParam) onChop = true;
                    if (c.parameter == CastawayCharacter.WeaponClassParam && c.mode == AnimatorConditionMode.Equals)
                        equalsClass = Mathf.RoundToInt(c.threshold);
                }
                if (!onChop) continue;
                string dest = t.destinationState != null ? t.destinationState.name : "<null>";
                if (equalsClass.HasValue)
                {
                    if (claimants.ContainsKey(equalsClass.Value)) claimants[equalsClass.Value].Add(dest);
                }
                else
                {
                    // Ungated Chop transition — fires for every class (the double-fire regression). Fail loud.
                    Assert.Fail("An AnyState→" + dest + " transition is gated ONLY on Chop (no WeaponClass Equals) — " +
                        "it fires for EVERY WeaponClass and double-matches the per-class swings (86caffwv5 Devon-NIT #1). " +
                        "The reserved overhead Attack state's ungated Chop transition must be REMOVED.");
                }
            }

            for (int cls = 0; cls <= 4; cls++)
                Assert.AreEqual(1, claimants[cls].Count,
                    "WeaponClass==" + cls + " must be claimed by EXACTLY ONE Chop-reachable state (got [" +
                    string.Join(", ", claimants[cls]) + "]) — else firing Chop with that class double-fires");
        }

        // 4 — the reserved overhead 'Attack' state is kept + motion'd to CastawayMelee (future sword HEAVY).
        [Test]
        public void ReservedOverheadAttackState_IsKept_MotionedToCastawayMelee()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(controller, "the CastawayAnimator controller must exist");
            AnimatorState reserved = null;
            foreach (var cs in controller.layers[0].stateMachine.states)
                if (cs.state.name == "Attack") reserved = cs.state;
            Assert.IsNotNull(reserved, "the reserved overhead 'Attack' state must be KEPT (future sword HEAVY, 86caffwv5 §5)");
            var motion = reserved.motion as AnimationClip;
            Assert.IsNotNull(motion, "the reserved Attack state must still be motion'd to a clip");
            Assert.IsTrue(motion.name.Contains(CharacterAssetGen.MeleeClip),
                "the reserved Attack state must keep the CastawayMelee overhead clip (got '" + motion.name + "')");
        }

        // 5 — runtime↔editor mirrors (WeaponClass ints + per-class clip names) match.
        [Test]
        public void RuntimeAndEditorMirrors_Match_ForWeaponClassAndClipNames()
        {
            Assert.AreEqual(CharacterAssetGen.WeaponClassParam, CastawayCharacter.WeaponClassParam, "WeaponClass param name");
            Assert.AreEqual(CharacterAssetGen.WeaponClassAxe, CastawayCharacter.WeaponClassAxe, "WeaponClassAxe");
            Assert.AreEqual(CharacterAssetGen.WeaponClassPickaxe, CastawayCharacter.WeaponClassPickaxe, "WeaponClassPickaxe");
            Assert.AreEqual(CharacterAssetGen.WeaponClassDagger, CastawayCharacter.WeaponClassDagger, "WeaponClassDagger");
            Assert.AreEqual(CharacterAssetGen.WeaponClassSpear, CastawayCharacter.WeaponClassSpear, "WeaponClassSpear");
            Assert.AreEqual(CharacterAssetGen.WeaponClassSword, CastawayCharacter.WeaponClassSword, "WeaponClassSword");

            Assert.AreEqual(CharacterAssetGen.AxeSwingClip, CastawayCharacter.AxeSwingClipName, "axe swing clip name");
            Assert.AreEqual(CharacterAssetGen.PickaxeSwingClip, CastawayCharacter.PickaxeSwingClipName, "pickaxe swing clip name");
            Assert.AreEqual(CharacterAssetGen.DaggerStabClip, CastawayCharacter.DaggerStabClipName, "dagger stab clip name");
            Assert.AreEqual(CharacterAssetGen.SpearThrustClip, CastawayCharacter.SpearThrustClipName, "spear thrust clip name");
            Assert.AreEqual(CharacterAssetGen.SwordSlashClip, CastawayCharacter.SwordSlashClipName, "sword slash clip name");
        }

        // 5b — the cadence source (MeleeClipLength via AttackClipNameForClass) maps each class to its clip name.
        [Test]
        public void AttackClipNameForClass_MapsEachWeaponClassToItsSwingClip()
        {
            Assert.AreEqual(CastawayCharacter.AxeSwingClipName, CastawayCharacter.AttackClipNameForClass(CastawayCharacter.WeaponClassAxe));
            Assert.AreEqual(CastawayCharacter.PickaxeSwingClipName, CastawayCharacter.AttackClipNameForClass(CastawayCharacter.WeaponClassPickaxe));
            Assert.AreEqual(CastawayCharacter.DaggerStabClipName, CastawayCharacter.AttackClipNameForClass(CastawayCharacter.WeaponClassDagger));
            Assert.AreEqual(CastawayCharacter.SpearThrustClipName, CastawayCharacter.AttackClipNameForClass(CastawayCharacter.WeaponClassSpear));
            Assert.AreEqual(CastawayCharacter.SwordSlashClipName, CastawayCharacter.AttackClipNameForClass(CastawayCharacter.WeaponClassSword));
        }

        // 6 — the AnimationId→WeaponClass seam maps EVERY WeaponDef.AnimationId (no orphan swing id).
        [Test]
        public void EveryWeaponDefAnimationId_MapsToADefinedWeaponClass_NoOrphan()
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
            catalog.BuildDefaults();
            Assert.Greater(catalog.All.Count, 0, "the catalog must mint the weapon defs");
            foreach (var def in catalog.All)
            {
                int cls = WeaponCatalog.WeaponClassForAnimationId(def.AnimationId);
                Assert.That(cls, Is.InRange(0, 4),
                    "WeaponDef '" + def.Id + "' AnimationId '" + def.AnimationId + "' must map to a defined WeaponClass " +
                    "(0..4) — an unmapped swing id means a weapon with no per-class swing");
            }
            Object.DestroyImmediate(catalog);
        }

        // 6b — the specific AnimationId→WeaponClass mappings (the 5 opaque ids).
        [Test]
        public void WeaponClassForAnimationId_MapsEachSwingId()
        {
            Assert.AreEqual(CastawayCharacter.WeaponClassAxe, WeaponCatalog.WeaponClassForAnimationId(WeaponCatalog.AnimIdAxeChop));
            Assert.AreEqual(CastawayCharacter.WeaponClassPickaxe, WeaponCatalog.WeaponClassForAnimationId(WeaponCatalog.AnimIdPickaxeMine));
            Assert.AreEqual(CastawayCharacter.WeaponClassDagger, WeaponCatalog.WeaponClassForAnimationId(WeaponCatalog.AnimIdDaggerStab));
            Assert.AreEqual(CastawayCharacter.WeaponClassSpear, WeaponCatalog.WeaponClassForAnimationId(WeaponCatalog.AnimIdSpearThrust));
            Assert.AreEqual(CastawayCharacter.WeaponClassSword, WeaponCatalog.WeaponClassForAnimationId(WeaponCatalog.AnimIdSwordSlash));
            Assert.AreEqual(-1, WeaponCatalog.WeaponClassForAnimationId("nonexistent_swing"),
                "an unknown AnimationId must map to -1 (caller falls back to axe)");
        }

        // 6c — MeleeAttack.WeaponClassForSwing routes each weapon to its class (and an orphan/null → axe fallback).
        [Test]
        public void MeleeAttackWeaponClassForSwing_RoutesEachWeaponToItsClass()
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
            catalog.BuildDefaults();
            Assert.AreEqual(CastawayCharacter.WeaponClassAxe, MeleeAttack.WeaponClassForSwing(catalog.ById(WeaponCatalog.AxeId)), "axe");
            Assert.AreEqual(CastawayCharacter.WeaponClassSpear, MeleeAttack.WeaponClassForSwing(catalog.ById(WeaponCatalog.SpearId)), "spear");
            Assert.AreEqual(CastawayCharacter.WeaponClassPickaxe, MeleeAttack.WeaponClassForSwing(catalog.ById(WeaponCatalog.PickaxeStoneId)), "pickaxe");
            Assert.AreEqual(CastawayCharacter.WeaponClassDagger, MeleeAttack.WeaponClassForSwing(catalog.ById(WeaponCatalog.DaggerStoneId)), "dagger");
            Assert.AreEqual(CastawayCharacter.WeaponClassSword, MeleeAttack.WeaponClassForSwing(catalog.ById(WeaponCatalog.SwordStoneId)), "sword");
            // A null weapon defensively falls back to the axe class (never a silent wrong swing).
            Assert.AreEqual(CastawayCharacter.WeaponClassAxe, MeleeAttack.WeaponClassForSwing(null), "null → axe fallback");
            Object.DestroyImmediate(catalog);
        }

        // 6d — WOOD-tier weapons route to their class (soak-2 fix #3: the Sponsor's crafted wooden-axe case).
        // Tiers share an AnimationId, so wood routes exactly like stone/iron — a whiff swing plays on left-click.
        [Test]
        public void WoodTierWeapons_RouteToTheirClass_LikeOtherTiers()
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
            catalog.BuildDefaults();
            Assert.AreEqual(CastawayCharacter.WeaponClassAxe, MeleeAttack.WeaponClassForSwing(catalog.ById(WeaponCatalog.AxeWoodId)), "wood axe → axe");
            Assert.AreEqual(CastawayCharacter.WeaponClassPickaxe, MeleeAttack.WeaponClassForSwing(catalog.ById(WeaponCatalog.PickaxeWoodId)), "wood pickaxe");
            Assert.AreEqual(CastawayCharacter.WeaponClassSpear, MeleeAttack.WeaponClassForSwing(catalog.ById(WeaponCatalog.SpearWoodId)), "wood spear");
            Assert.AreEqual(CastawayCharacter.WeaponClassDagger, MeleeAttack.WeaponClassForSwing(catalog.ById(WeaponCatalog.DaggerWoodId)), "wood dagger");
            Assert.AreEqual(CastawayCharacter.WeaponClassSword, MeleeAttack.WeaponClassForSwing(catalog.ById(WeaponCatalog.SwordWoodId)), "wood sword");
            Object.DestroyImmediate(catalog);
        }

        // 7 — WHIFF GATE (soak-2 fix #1): ShouldSwingOnClick fires the swing with a weapon equipped even with NO
        // target (target-in-reach is NOT a condition); the UI-open / over-UI / RMB-drag guards + weapon-required stay.
        // ShouldSwingOnClick(weaponSelected, verbClaimedClick, uiPanelOpen, pointerOverUI, rmbHeld).
        [Test]
        public void ShouldSwingOnClick_TruthTable_WhiffAllowed_GuardsHold()
        {
            // Weapon equipped, NO verb claimed, no modal panel, not over UI, no orbit drag → SWINGS (whiff — soak-2).
            Assert.IsTrue(MeleeAttack.ShouldSwingOnClick(weaponSelected: true, verbClaimedClick: false, uiPanelOpen: false, pointerOverUI: false, rmbHeld: false),
                "a weapon equipped + no verb claim + guards clear must swing even with NO target (one click = one swing, whiff-allowed)");
            // No weapon → no swing.
            Assert.IsFalse(MeleeAttack.ShouldSwingOnClick(false, false, false, false, false), "no weapon equipped → no swing");
            // Each guard independently blocks.
            Assert.IsFalse(MeleeAttack.ShouldSwingOnClick(true, false, true, false, false), "a modal panel open blocks the swing");
            Assert.IsFalse(MeleeAttack.ShouldSwingOnClick(true, false, false, true, false), "a click over the belt/inventory UI blocks the swing");
            Assert.IsFalse(MeleeAttack.ShouldSwingOnClick(true, false, false, false, true), "RMB held (camera-orbit drag) blocks the swing");
        }

        // 7b — VERB-WINS-OVER-WHIFF arbitration truth-table (round-4 regression fix 86caffwv5, AC6). When a verb
        // consumer (chop / boulder-mine / ore-mine) owns the click (its tool selected + a target in range), the
        // VERB swings — MeleeAttack must NOT also fire a whiff on top (the soak-4 "cannot chop, only whiffs"
        // double-consumer regression). The whiff fires ONLY when nothing claimed the click.
        [Test]
        public void ShouldSwingOnClick_VerbClaimedClick_SuppressesTheWhiff()
        {
            // A verb owns the click (e.g. axe selected + a tree in chop range) → NO whiff swing, even with a weapon
            // selected + all world-click guards clear. THE round-4 fix: the chop owns the click, not a spurious swing.
            Assert.IsFalse(
                MeleeAttack.ShouldSwingOnClick(weaponSelected: true, verbClaimedClick: true,
                                               uiPanelOpen: false, pointerOverUI: false, rmbHeld: false),
                "a verb (chop/mine) that owns the click SUPPRESSES the attack whiff (verb-wins-over-whiff)");

            // Nothing claims the click (no tree/rock in range) → the whiff/attack DOES fire (AC3 air whiff preserved).
            Assert.IsTrue(
                MeleeAttack.ShouldSwingOnClick(true, verbClaimedClick: false, uiPanelOpen: false, pointerOverUI: false, rmbHeld: false),
                "with NO verb claiming the click, the attack swings (empty air whiffs / an enemy in reach is hit) — soak-2 kept");

            // A verb claim + a guard both blocking is still no swing (the verb-claim never RE-enables a guarded click).
            Assert.IsFalse(
                MeleeAttack.ShouldSwingOnClick(true, verbClaimedClick: true, uiPanelOpen: true, pointerOverUI: false, rmbHeld: false),
                "verb-claim AND a modal panel open → still no swing");
        }

        // 8 — per-class swing PLAYBACK speed (soak-2 fix #2): spear + pickaxe are faster than axe/dagger/sword.
        [Test]
        public void SwingSpeedForClass_SpearAndPickaxe_FasterThanBaseline()
        {
            float axe = CastawayCharacter.SwingSpeedForClass(CastawayCharacter.WeaponClassAxe);
            Assert.AreEqual(1.0f, axe, 1e-4f, "axe stays at authored cadence (Sponsor: chop looked okay)");
            Assert.AreEqual(1.0f, CastawayCharacter.SwingSpeedForClass(CastawayCharacter.WeaponClassDagger), 1e-4f, "dagger 1.0");
            Assert.AreEqual(1.0f, CastawayCharacter.SwingSpeedForClass(CastawayCharacter.WeaponClassSword), 1e-4f, "sword 1.0");
            Assert.Greater(CastawayCharacter.SwingSpeedForClass(CastawayCharacter.WeaponClassSpear), axe,
                "spear swing must be FASTER than the axe baseline (soak-2: spear too slow)");
            Assert.Greater(CastawayCharacter.SwingSpeedForClass(CastawayCharacter.WeaponClassPickaxe), axe,
                "pickaxe swing must be FASTER than the axe baseline (soak-2: pickaxe too slow)");
        }

        // 9 — CADENCE VALUE GUARD (soak-3 fix #3/#4): the Sponsor judged pickaxe "STILL too slow" at soak-2's 1.2×,
        // so the pickaxe swing playback is raised to 1.5× (a further bump). Pin the exact value so a regression that
        // drops it back reds here.
        [Test]
        public void SwingSpeedPickaxe_IsRaisedToOnePointFive_Soak3()
        {
            Assert.AreEqual(1.5f, CastawayCharacter.SwingSpeedForClass(CastawayCharacter.WeaponClassPickaxe), 1e-4f,
                "soak-3: the pickaxe swing plays at 1.5× (raised from soak-2's 1.2× — Sponsor: pickaxe STILL too slow)");
            Assert.Greater(CastawayCharacter.SwingSpeedForClass(CastawayCharacter.WeaponClassPickaxe), 1.2f,
                "the pickaxe playback must be a FURTHER bump over soak-2's 1.2×");
            // The spear stays at soak-2's 1.2 (Sponsor: "spear is ok") — the soak-3 bump is pickaxe-only.
            Assert.AreEqual(1.2f, CastawayCharacter.SwingSpeedForClass(CastawayCharacter.WeaponClassSpear), 1e-4f,
                "spear stays at 1.2× (Sponsor accepted the spear timing at soak-3 — do NOT change it)");
        }

        // 9b — the EFFECTIVE swing-playback composition the mine HOLD-cadence divides by (soak-3 idle-gap fix). The
        // mine cadence now divides the clip by chopSpeed × the pickaxe multiplier (not raw chopSpeed), so the next
        // hold-swing begins when the sped-up swing visually completes. The axe class stays == chopSpeed (chop cadence
        // unchanged); the band clamp holds at the extremes.
        [Test]
        public void EffectiveSwingPlaybackSpeed_ComposesToolUseSpeedAndClassMultiplier_Clamped()
        {
            // Pickaxe at 1× tool-use speed = 1.5× effective (the mine cadence divides the clip by THIS — no idle gap).
            Assert.AreEqual(1.5f, CastawayCharacter.EffectiveSwingPlaybackSpeed(1f, CastawayCharacter.WeaponClassPickaxe), 1e-4f,
                "pickaxe effective playback at chopSpeed=1 is 1.5× (= SwingSpeedPickaxe) — the mine hold-cadence divisor");
            // Axe at 1× = 1.0× effective → tree-chop cadence is UNCHANGED (the soak-3 fix is pickaxe-only).
            Assert.AreEqual(1.0f, CastawayCharacter.EffectiveSwingPlaybackSpeed(1f, CastawayCharacter.WeaponClassAxe), 1e-4f,
                "axe effective playback == chopSpeed (SwingSpeedAxe=1.0) — tree-chop cadence must NOT change");
            // Tool-use speed composes multiplicatively (2× tool-use × 1.5 pickaxe would be 3.0, at the band ceiling).
            Assert.AreEqual(CastawayCharacter.ChopSpeedMax,
                CastawayCharacter.EffectiveSwingPlaybackSpeed(2f, CastawayCharacter.WeaponClassPickaxe), 1e-4f,
                "chopSpeed 2 × pickaxe 1.5 = 3.0 clamps to ChopSpeedMax (the same clamp TriggerAttack applies)");
            Assert.AreEqual(CastawayCharacter.ChopSpeedMin,
                CastawayCharacter.EffectiveSwingPlaybackSpeed(0.05f, CastawayCharacter.WeaponClassAxe), 1e-4f,
                "a tiny tool-use speed clamps up to ChopSpeedMin (never a stalled/zero playback)");
        }
    }
}
