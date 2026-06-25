using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The chop-a-tree mechanic + the player SWING animation (ticket 86caa4c5c). The gameplay-wave
    /// successor to the U2-3 thin chop (86ca8bdd8): a tree the castaway reaches, then — WITH THE AXE
    /// SELECTED in the belt — chops by LEFT-CLICKING (like an attack); each click SWINGS the arm
    /// (ChopPoseDriver) + yields <c>wood</c> into the inventory; after <see cref="chopsToFell"/> clicks the
    /// tree FELLS to a STUMP, and the stump REGROWS into a tree after a tweakable random delay.
    ///
    /// === AC1 — THE TRIGGER IS AN ACTIVE LEFT-CLICK (Sponsor soak, 2026-06-25 — CHANGE 1) ===
    /// The chop is INITIATED BY THE PLAYER, not auto-fired by proximity. With the axe SELECTED and a tree in
    /// range, a LEFT-CLICK (<c>Input.GetMouseButtonDown(0)</c>) triggers ONE chop strike (one swing + one
    /// wood) — chopping reads like attacking. The proximity-auto trigger (the prior behaviour: stand at the
    /// tree → it chops itself on a timer) is REPLACED. Three guards keep the world-click clean (the Sponsor's
    /// "left-click must only chop in the game world"):
    ///   • a click while a modal panel is open (inventory pack / settings) is swallowed —
    ///     <see cref="UiInputGate.CaptureWorldInput"/>;
    ///   • a click OVER the inventory/belt UI (the always-on belt strip, or the open pack) does NOT chop —
    ///     <see cref="InventoryUI.IsPointerOverUI"/> (UI Toolkit does NOT block legacy Input.* world polling,
    ///     research §E1, so the world consumer must ask the panel);
    ///   • a click while the RIGHT mouse button is held (a camera ORBIT drag) does NOT chop —
    ///     <c>Input.GetMouseButton(1)</c> (the OrbitCamera owns RMB-drag).
    /// The decision is the pure static <see cref="ShouldChopOnClick"/> so the full guard truth-table is
    /// unit-testable headlessly. A programmatic <see cref="RequestChopClick"/> latch is the input-independent
    /// seam (the analog of WasdMovement.RequestJump) so headless PlayMode + the shipped-build capture exercise
    /// the SAME chop path without injecting a real mouse button.
    ///
    /// === AC1 — THE AXE-SELECTED GATE (load-bearing, UNCHANGED) ===
    /// The chop still requires the axe to be the SELECTED belt item (<see cref="Inventory.IsAxeSelectedInBelt"/>),
    /// NOT merely owned (HasAxe), AND the player in range — only the TRIGGER changed (proximity-auto → click).
    /// This MATCHES the held-axe visual gate (HeldAxe.ShouldShow) + the finger grip — you chop with the axe
    /// you're HOLDING, the one shown in-hand. Axe owned but a different belt slot selected → a click does
    /// nothing, no wood, the tree stays standing (success test: "chopping without the [selected] axe does
    /// nothing"). This SUPERSEDES the old HasAxe gate (ticket AC1 correction, 2026-06-25): before the belt
    /// existed owns==holds, so HasAxe was right; with the belt, SELECTION is the signal (item-model contract
    /// §5). Each landed chop fires <see cref="ChopPoseDriver.TriggerSwing"/> so the arm swings; the held axe
    /// (HeldAxeRig, order 100) follows the swung hand automatically.
    ///
    /// === AC2 — wood yield ===
    /// Each chop adds <see cref="WoodPerChop"/> (the NAMED yield constant — ticket AC2a: the chop OWNS the
    /// per-chop amount; the `tree-chop wood yield` SETTING that drives it is owned by the sticks ticket
    /// 86caa96rd, NOT here — this is the single named source that setting can drive, no dead literal). Wood
    /// goes in via <see cref="Inventory.AddWood"/> → the canonical <c>ItemCatalog.WoodId</c> = "wood" path
    /// (ticket V3 — never a "chopped_wood" id). After <see cref="chopsToFell"/> chops the tree fells.
    ///
    /// === AC3 — regrowth (tweakable, RANDOM within [min,max]) ===
    /// A felled tree becomes a STUMP (the visual sinks + tips). After a RANDOM delay in
    /// [<see cref="regrowthMinSeconds"/>, <see cref="regrowthMaxSeconds"/>] (organic, not uniform — AC3) the
    /// stump REGROWS into a standing, choppable tree (the visual returns to its standing pose, chop count
    /// reset). The min/max are NAMED serialized fields (the live-tunable source — AC5a); the `tree regrowth
    /// time` SETTING that drives them is registered by SettingsCatalog.PopulateChop (this ticket owns the
    /// fields, the catalog owns the row). Mirrors BerryBush's regrow idiom (seeded System.Random roll).
    ///
    /// === AC4 — visual feedback (cute/warm low-poly) ===
    /// On the felling chop the tree SINKS + tips (the existing thin-but-felt tween, kept); the STUMP (the
    /// sunk/tipped visual) persists through the regrowth window; regrowth eases the visual back up to
    /// standing. Each chop also swings the arm (AC1). Richer particles are OOS (Uma's feel pass).
    ///
    /// === AC5 — world integrity ===
    /// This tree is the WIRED choppable tree authored editor-time (MovementCameraScene.BuildChopTree); it
    /// READS the existing seed-42 scatter only (no scatter mutation — ticket V4, no byte change to the world
    /// rnd). Chopping never breaks the island scatter or the NavMesh (the tree has no collider).
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The tree GameObject + this component + its Inventory/player/visual/poseDriver references are authored
    /// editor-time into Boot.unity (MovementCameraScene.BuildChopTree), NOT at Awake — an Awake-built
    /// interaction/visual could ship MANGLED/absent (the legs-up class). ChopSceneTests guards the scene
    /// presence + that the refs serialize.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// `[chop-trace]` lines on chop / fell / regrow, [Conditional("UNITY_EDITOR")] so they strip from the
    /// shipped IL2CPP exe (unity6-mastery §5/§10 — no Debug.Log in the shipped hot path).
    ///
    /// NO MUTABLE STATICS (instance state only) — the Configurable-Enter-Play-Mode static-reset audit
    /// (StaticStateResetTests) needs no [RuntimeInitializeOnLoadMethod] reset for this class.
    /// </summary>
    public class ChopTree : MonoBehaviour
    {
        /// <summary>AC2a — the NAMED default per-chop wood yield. The single source AC2 reads; the
        /// `tree-chop wood yield` SETTING (owned by 86caa96rd) drives <see cref="woodPerChop"/>, which seeds
        /// from this. NOT a magic literal scattered through the code (no "dead knob").</summary>
        public const int DefaultChopYield = 1;

        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger chopped wood is added to. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity (with the axe SELECTED) triggers a chop. Wired at " +
                 "bootstrap; falls back to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Tooltip("The tree's visual root, tweened on felling (sink + tip) + on regrowth (rise back). Wired " +
                 "at bootstrap; falls back to this transform so the chop is still felt even if unwired.")]
        public Transform visual;

        [Tooltip("The player SWING driver (ChopPoseDriver on the castaway). Each landed chop calls its " +
                 "TriggerSwing() so the arm swings (AC1). Wired at bootstrap; an Awake scene-search fallback. " +
                 "Null is graceful — the chop still yields wood, just without the arm swing.")]
        public ChopPoseDriver poseDriver;

        [Tooltip("The inventory/belt UI (CHANGE 1 — left-click chop). A left-click OVER this UI (the always-on " +
                 "belt strip, or the open pack) must NOT chop the tree behind it; the chop asks IsPointerOverUI. " +
                 "Wired at bootstrap; an Awake scene-search fallback. Null is tolerated — the over-UI guard is " +
                 "simply skipped (a bare test rig with no UI), but the modal-panel + RMB-orbit guards still apply.")]
        public InventoryUI inventoryUI;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the tree and (with the selected " +
                 "axe) chops. Generous enough that arriving near the tree counts. Mirrors CraftSpot.craftRadius.")]
        public float chopRadius = 2.2f;

        [Tooltip("Wood units yielded per chop (seeds from DefaultChopYield). The `tree-chop wood yield` " +
                 "setting (86caa96rd) drives this live; this is the single named source it reaches.")]
        public int woodPerChop = DefaultChopYield;

        [Tooltip("Chops needed to fell the tree. After this many, the tree falls to a STUMP, then regrows " +
                 "(AC3). Small so the loop reads quickly.")]
        public int chopsToFell = 3;

        [Tooltip("Minimum seconds between landed chops (CHANGE 1 — left-click trigger). The chop is now per " +
                 "LEFT-CLICK (one strike per click, not auto-paced), so this is a small COOLDOWN: a second " +
                 "chop-click within this window is ignored, so a stray double-edge (or a frantic mash) can't " +
                 "out-pace the swing read. A normal click cadence is always slower than this, so it never " +
                 "throttles deliberate chopping. 0 = no cooldown (every click chops).")]
        public float chopInterval = 0.25f;

        [Header("Regrowth (AC3 — TWEAKABLE; the `tree regrowth time` setting drives min/max)")]
        [Tooltip("Minimum seconds before a felled STUMP regrows into a tree. The actual regrow time is " +
                 "RANDOM in [min,max] (organic, not uniform — AC3). Default ~10 min (the ticket default). The " +
                 "`tree regrowth time` setting (SettingsCatalog.PopulateChop) drives these live.")]
        public float regrowthMinSeconds = 480f;   // 8 min

        [Tooltip("Maximum seconds before a felled stump regrows (random within [min,max]). Default ~12 min " +
                 "so the average regrow is ~10 min (the ticket's '~10 min default').")]
        public float regrowthMaxSeconds = 720f;   // 12 min

        [Tooltip("Deterministic seed for the regrow-time roll (so headless tests are reproducible). 0 = use " +
                 "a time-based seed at runtime. Mirrors BerryBush.regrowSeed.")]
        public int regrowSeed = 0;

        // Runtime state.
        private int _chops;          // chops landed on the CURRENT tree (resets on regrow)
        private bool _felled;        // true while the tree is a stump (felled, awaiting regrow)
        private float _regrowAt;     // wall-clock time the stump regrows (only meaningful while felled)
        private System.Random _rng;

        // CHANGE 1 — programmatic LEFT-CLICK latch (the input-independent seam, the analog of
        // WasdMovement.RequestJump). A headless PlayMode run / the shipped-build capture can't inject a real
        // mouse button, so a chop-click can be REQUESTED via this latch (consumed once on the next Update,
        // mirroring the mouse's rising edge — one chop per request). The range + axe-selected + over-UI/RMB
        // guards still apply to a requested click (it goes through the SAME ShouldChopOnClick decision).
        private bool _chopClickRequested;
        // CHANGE 1 — wall-clock time of the last landed chop, for the chopInterval click cooldown (a stray
        // double-edge / frantic mash can't out-pace the swing read). float.NegativeInfinity = "never chopped".
        private float _lastChopAt = float.NegativeInfinity;

        // Felling/regrow tween state (runtime-only transform animation on the serialized visual).
        private bool _felling;       // playing the sink+tip tween
        private bool _regrowing;     // playing the rise-back tween
        private float _tweenT;
        private Vector3 _standPos;
        private Quaternion _standRot;
        private const float FellDuration = 0.5f;
        private const float RegrowRiseDuration = 0.6f;
        // The felled (stump) pose offset from standing — the tween's end state, captured once we know the
        // standing pose, so regrow can ease back from it.
        private static readonly Vector3 StumpDrop = Vector3.down * 0.6f;
        private const float StumpTipDeg = 70f;

        private bool _tracedFirstChop; // one-shot trace guard

        /// <summary>Chops landed on the current tree (0..chopsToFell). Exposed for PlayMode tests + capture.</summary>
        public int Chops => _chops;

        /// <summary>True while the tree is FELLED (a stump, awaiting regrow). After regrow it is false
        /// again (standing + choppable). Exposed for tests + the verify capture.</summary>
        public bool IsFelled => _felled;

        /// <summary>Wall-clock time the stump regrows (only meaningful while <see cref="IsFelled"/>).</summary>
        public float RegrowAt => _regrowAt;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            if (visual == null) visual = transform;
            if (poseDriver == null) poseDriver = FindObjectOfType<ChopPoseDriver>();
            // CHANGE 1 — the inventory/belt UI for the over-UI left-click guard (serialized editor-time; this
            // scene-search is the build-safety net). Null is tolerated — the over-UI guard is then skipped.
            if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();
            // Capture the standing pose at spawn so felling/regrow tweens have an anchor. The tree ships
            // standing, so transform-at-Awake == the standing pose.
            _standPos = visual.position;
            _standRot = visual.rotation;
            _rng = new System.Random(regrowSeed != 0 ? regrowSeed : Environment.TickCount);
        }

        void Update()
        {
            // Drive the one-shot felling / regrow tweens independently of input.
            if (_felling) { StepFelling(); return; }
            if (_regrowing) { StepRegrow(); return; }

            // While FELLED (a stump), wait for the regrow timer, then regrow into a standing tree (AC3).
            if (_felled)
            {
                if (Time.time >= _regrowAt) BeginRegrow();
                return;
            }

            if (inventory == null || player == null) return;

            // CHANGE 1 — the chop is triggered by an ACTIVE LEFT-CLICK (like an attack), NOT proximity-auto.
            // Read the real mouse rising edge OR consume the programmatic latch (the headless / shipped-build
            // seam). Consume the latch unconditionally each frame (one chop per RequestChopClick, matching the
            // mouse's one-edge-per-press) so it can't stick across frames.
            bool clickEdge = _chopClickRequested || Input.GetMouseButtonDown(0);
            _chopClickRequested = false;
            if (!clickEdge) return;

            // Planar distance only — ignore any Y offset between the tree origin and the player root ground
            // point (height-robust, same as CraftSpot / BerryBush).
            Vector2 tree = new Vector2(transform.position.x, transform.position.z);
            Vector2 here = new Vector2(player.position.x, player.position.z);
            bool inRange = Vector2.Distance(tree, here) <= chopRadius;

            // The full chop-on-click decision (pure static so the guard truth-table is unit-testable): range +
            // the axe-SELECTED gate (load-bearing, unchanged) + the three world-click guards (modal panel open;
            // pointer over the inventory/belt UI; RMB camera-orbit drag). Only when ALL hold does ONE chop land.
            bool overUI = inventoryUI != null && inventoryUI.IsPointerOverUI(Input.mousePosition);
            bool rmbHeld = Input.GetMouseButton(1);
            if (ShouldChopOnClick(inRange, inventory.IsAxeSelectedInBelt,
                                  UiInputGate.CaptureWorldInput, overUI, rmbHeld))
            {
                // Click cooldown: ignore a chop that lands within chopInterval of the last (a stray double-edge
                // / mash can't out-pace the swing). A deliberate click cadence is always slower than this.
                if (Time.time - _lastChopAt < Mathf.Max(0f, chopInterval)) return;
                _lastChopAt = Time.time;
                Chop();
            }
        }

        /// <summary>
        /// PURE chop-on-a-left-click decision (CHANGE 1 — the unit-testable guard truth-table). Given that a
        /// left-click edge fired this frame, decide whether ONE chop should land:
        ///   • <paramref name="inRange"/> — the player is within <see cref="chopRadius"/> of the tree;
        ///   • <paramref name="axeSelected"/> — the axe is the SELECTED belt item (the load-bearing gate);
        ///   • NOT <paramref name="uiPanelOpen"/> — no modal panel (inventory pack / settings) owns the screen;
        ///   • NOT <paramref name="pointerOverUI"/> — the click is NOT over the inventory/belt UI;
        ///   • NOT <paramref name="rmbHeld"/> — the right mouse button is NOT held (no camera-orbit drag).
        /// All five must hold. Static + dependency-free so the EditMode guard asserts the whole table
        /// (no-chop-without-each-precondition; chop only when all hold) with no scene/Input/UI rig.
        /// </summary>
        public static bool ShouldChopOnClick(bool inRange, bool axeSelected,
                                             bool uiPanelOpen, bool pointerOverUI, bool rmbHeld)
            => inRange && axeSelected && !uiPanelOpen && !pointerOverUI && !rmbHeld;

        /// <summary>
        /// Request ONE chop strike programmatically — the input-independent analog of a left-click (CHANGE 1).
        /// Latched + consumed on the next Update (mirrors the mouse's rising edge — one chop per call), so a
        /// headless PlayMode test + the shipped-build chop capture trigger a chop where a real mouse button
        /// can't be injected. The range + axe-selected + over-UI/RMB guards still apply (it runs the SAME
        /// ShouldChopOnClick decision) — a request out of range / without the selected axe is harmlessly
        /// ignored, exactly like a real off-target click.
        /// </summary>
        public void RequestChopClick() => _chopClickRequested = true;

        // Land one chop: SWING the arm (AC1), yield wood (AC2), advance the count, and fell the tree on the
        // final chop. Public so PlayMode tests can drive it directly (isolating it from pathfinding).
        public void Chop()
        {
            if (_felled || _felling || _regrowing || inventory == null) return;

            // Swing the arm (AC1) — the held axe (HeldAxeRig) follows the swung hand automatically. Null is
            // graceful (the wood still yields; just no swing).
            if (poseDriver != null) poseDriver.TriggerSwing();

            inventory.AddWood(Mathf.Max(1, woodPerChop));
            _chops++;

            if (!_tracedFirstChop)
            {
                _tracedFirstChop = true;
                ChopTrace("chop " + _chops + "/" + chopsToFell + " -> wood=" + inventory.WoodCount +
                          " (swing=" + (poseDriver != null) + ")");
            }

            if (_chops >= chopsToFell)
            {
                _felled = true;
                BeginFelling();
                ScheduleRegrow();
                ChopTrace("tree FELLED after " + _chops + " chops (total wood=" + inventory.WoodCount +
                          "); regrow in " + (_regrowAt - Time.time).ToString("F0") + "s");
            }
        }

        // Schedule the regrow at a RANDOM time within [min,max] (AC3 — organic, not uniform). Min clamped
        // non-negative; max clamped >= min so a mis-authored max never schedules a regrow in the past.
        // Mirrors BerryBush.ScheduleRegrow.
        private void ScheduleRegrow()
        {
            if (_rng == null) _rng = new System.Random(regrowSeed != 0 ? regrowSeed : Environment.TickCount);
            float min = Mathf.Max(0f, regrowthMinSeconds);
            float max = Mathf.Max(min, regrowthMaxSeconds);
            float delay = min + (float)_rng.NextDouble() * (max - min);
            _regrowAt = Time.time + delay;
        }

        // Start the thin-but-felt felling tween: capture the standing pose so StepFelling can sink+tip.
        private void BeginFelling()
        {
            if (visual == null) visual = transform;
            _standPos = visual.position;
            _standRot = visual.rotation;
            _felling = true;
            _tweenT = 0f;
        }

        // One frame of the felling tween — the tree sinks into the ground and tips over (becoming the
        // stump), then stops. Pure transform animation on the already-serialized visual (no Awake-built
        // hierarchy to ship mangled). Wall-clock paced via unscaledDeltaTime (headless deltas are ~0, so a
        // headless run lands at the end state quickly without affecting the wood/felled/regrow assertions).
        private void StepFelling()
        {
            if (visual == null) { _felling = false; return; }
            _tweenT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_tweenT / FellDuration);
            float ease = k * k * (3f - 2f * k); // smoothstep
            visual.position = _standPos + StumpDrop * ease;
            visual.rotation = _standRot * Quaternion.Euler(StumpTipDeg * ease, 0f, 0f);
            if (k >= 1f) _felling = false; // now resting as the STUMP (sunk + tipped) until regrow
        }

        // Begin the regrow rise: the stump eases back UP to the standing pose, then the tree is choppable
        // again (chop count reset). The standing pose was captured at Awake / before felling.
        private void BeginRegrow()
        {
            if (visual == null) visual = transform;
            _regrowing = true;
            _tweenT = 0f;
            ChopTrace("stump REGROWING -> standing tree (chop count reset)");
        }

        // One frame of the regrow rise — ease the visual from the stump pose back to standing. On completion
        // the tree is STANDING + choppable again, chops reset to 0 (AC3: stump regrows into a tree).
        private void StepRegrow()
        {
            if (visual == null) { _regrowing = false; _felled = false; _chops = 0; return; }
            _tweenT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_tweenT / RegrowRiseDuration);
            float ease = k * k * (3f - 2f * k); // smoothstep
            // Ease from the stump pose (full drop+tip) back to standing (no drop/tip).
            visual.position = _standPos + StumpDrop * (1f - ease);
            visual.rotation = _standRot * Quaternion.Euler(StumpTipDeg * (1f - ease), 0f, 0f);
            if (k >= 1f)
            {
                _regrowing = false;
                _felled = false;
                _chops = 0;         // a fresh tree — chop it anew (the next left-click in range lands a chop)
                visual.position = _standPos;
                visual.rotation = _standRot;
            }
        }

        // [chop-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call
        // (AND its argument evaluation, incl. the string concatenation) from the shipped IL2CPP release exe
        // (unity6-mastery §5 "no Debug.Log in hot paths" / §10 "strip all logging from shipping builds").
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void ChopTrace(string msg) => Debug.Log("[chop-trace] " + msg);
    }
}
