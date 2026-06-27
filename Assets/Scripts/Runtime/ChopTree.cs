using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The chop-a-tree mechanic + the player SWING animation (ticket 86caa4c5c). The gameplay-wave
    /// successor to the U2-3 thin chop (86ca8bdd8): a tree the castaway reaches, then — WITH THE AXE
    /// SELECTED in the belt — chops by LEFT-CLICKING (like an attack); each click SWINGS the arm via the
    /// Mixamo melee Attack animation (CastawayCharacter.TriggerChop — change-(b), replacing the rejected
    /// procedural ChopPoseDriver) + yields <c>wood</c> into the inventory; after <see cref="chopsToFell"/>
    /// clicks the tree FELLS to a STUMP, and the stump REGROWS into a tree after a tweakable random delay.
    ///
    /// === CHANGE (a) — EVERY scatter tree is choppable (Sponsor soak, 2026-06-25 — only ONE tree chopped) ===
    /// This component is now a chop-target RESOLVER over MANY tree instances, NOT a single hardcoded tree.
    /// AC5 ("the chop targets the nearest in-range tree") was only ever satisfied for the lone demo tree at
    /// <c>MovementCameraScene.ChopTreePosition</c>; the ~320 world scatter trees (<c>LP_Tree</c> GameObjects
    /// authored by <c>LowPolyZoneGen.BuildTree</c> under the <c>LowPolyScatter</c> root) were plain visual
    /// meshes with no chop behaviour. The fix: on a left-click, RESOLVE THE NEAREST in-range tree across the
    /// whole world and chop THAT instance. Per-tree state (chops landed / felled / regrow timer / fell+regrow
    /// tween) lives in a <see cref="ChoppableTreeState"/> per instance, so each tree deplete→stump→regrows
    /// INDEPENDENTLY.
    ///   • Instance 0 is the editor-authored demo tree (this component's own <see cref="visual"/>) — it keeps
    ///     every serialized ref + the ChopSceneTests scene-presence guard + the ChopVerifyCapture path; the demo
    ///     tree stays choppable exactly as before.
    ///   • The scatter trees are DISCOVERED at runtime (Start) by collecting the <c>LP_Tree</c> children under
    ///     <see cref="scatterRoot"/> (wired editor-time; a name-scan fallback if unwired). This is a READ of the
    ///     seed-42 scatter — it NEVER re-authors / re-rolls it (AC5 / V4: the scatter RNG stream is untouched, so
    ///     the world placement stays byte-identical; ChopScatterInvarianceTests pins this). The chop does NOT touch
    ///     LowPolyZoneGen.ScatterIslandProps's seeded placement stream. (LowPolyZoneGen IS edited on this branch in
    ///     ONE unrelated way — the per-tree NavMesh-carve right-sizing, TrunkObstacleLocalRadius — which does not
    ///     affect the scatter RNG/placement; see the PR body's disclosure.)
    /// The felling tween sinks+tips a tree's OWN transform; then (refinement 2) the felled tree FADES OUT (scales
    /// to nothing) and its renderers disable ~fadeOutDelaySeconds later — the ground is empty until regrow brings it
    /// back at the same spot. A felled tree fades away in place while its neighbours stand, then regrows on its own
    /// random timer.
    ///
    /// === AC1 — THE TRIGGER IS AN ACTIVE LEFT-CLICK (Sponsor soak, 2026-06-25 — CHANGE 1, KEPT) ===
    /// The chop is INITIATED BY THE PLAYER, not auto-fired by proximity. With the axe SELECTED and a tree in
    /// range, a LEFT-CLICK (<c>Input.GetMouseButtonDown(0)</c>) triggers ONE chop strike (one swing + one
    /// wood) on the NEAREST in-range tree — chopping reads like attacking. The proximity-auto trigger is
    /// REPLACED. Three guards keep the world-click clean (the Sponsor's "left-click must only chop in the game
    /// world"):
    ///   • a click while a modal panel is open (inventory pack / settings) is swallowed —
    ///     <see cref="UiInputGate.CaptureWorldInput"/>;
    ///   • a click OVER the inventory/belt UI does NOT chop — <see cref="InventoryUI.IsPointerOverUI"/>;
    ///   • a click while the RIGHT mouse button is held (a camera ORBIT drag) does NOT chop —
    ///     <c>Input.GetMouseButton(1)</c> (the OrbitCamera owns RMB-drag).
    /// The decision is the pure static <see cref="ShouldChopOnClick"/> so the full guard truth-table is
    /// unit-testable headlessly. A programmatic <see cref="RequestChopClick"/> latch is the input-independent
    /// seam (the analog of WasdMovement.RequestJump) so headless PlayMode + the shipped-build capture exercise
    /// the SAME chop path without injecting a real mouse button.
    ///
    /// === AC1 — THE AXE-SELECTED GATE (load-bearing, UNCHANGED) ===
    /// The chop still requires the axe to be the SELECTED belt item (<see cref="Inventory.IsAxeSelectedInBelt"/>),
    /// NOT merely owned (HasAxe), AND the player in range of SOME tree. Each landed chop fires
    /// <see cref="CastawayCharacter.TriggerChop"/> (the Mixamo melee Attack state) so the arm swings; the held
    /// axe (HeldAxeRig, order 100) follows the swung hand automatically.
    ///
    /// === AC2 — wood yield ===
    /// Each chop adds <see cref="DefaultChopYield"/> (the NAMED yield constant — ticket AC2a) via
    /// <see cref="Inventory.AddWood"/> → the canonical <c>ItemCatalog.WoodId</c> = "wood" path (ticket V3).
    /// After <see cref="chopsToFell"/> chops the resolved tree fells.
    ///
    /// === AC3 — regrowth (tweakable, RANDOM within [min,max]) ===
    /// A felled tree fades out (below), then after a RANDOM delay in [<see cref="regrowthMinSeconds"/>,
    /// <see cref="regrowthMaxSeconds"/>] (organic, not uniform — AC3) REGROWS into a standing, choppable tree at
    /// the SAME spot (chop count reset). The min/max are NAMED serialized fields (the live-tunable source — AC5a);
    /// the `tree regrowth time` SETTING that drives them is registered by SettingsCatalog.PopulateChop.
    ///
    /// === AC4 — visual feedback (cute/warm low-poly) — FADE-OUT (Sponsor soak refinement 2, 2026-06-27) ===
    /// On the felling chop the tree SINKS + tips, then ~<see cref="fadeOutDelaySeconds"/> (~10s, NAMED tweakable
    /// field) later FADES OUT — it scales down to nothing + its renderers disable, so the tree DISAPPEARS and the
    /// ground is empty (this REPLACES the old persistent-stump behaviour). The scale-down fade keeps the shared
    /// OPAQUE LowPolyVertexColor material on the ~1-draw-call batch path (a transparent-alpha variant would cost
    /// draw calls). Regrowth (AC3) then re-shows + scales the tree back up. Each chop also swings the arm (AC1).
    ///
    /// === AC5 — world integrity ===
    /// The demo tree (instance 0) is authored editor-time (MovementCameraScene.BuildChopTree); the scatter
    /// trees are READ from the existing seed-42 scatter (no scatter mutation — ticket V4, no byte change to the
    /// world rnd). Chopping never breaks the island scatter or the NavMesh.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The demo tree GameObject + this component + its Inventory/player/visual/character/scatterRoot references
    /// are authored editor-time into Boot.unity, NOT at Awake — an Awake-built interaction/visual could ship
    /// MANGLED/absent (the legs-up class). ChopSceneTests guards the scene presence + that the refs serialize.
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

        /// <summary>The GameObject name LowPolyZoneGen.BuildTree gives every scatter tree. The runtime
        /// resolver collects these under <see cref="scatterRoot"/> so every world tree is choppable
        /// (CHANGE (a)). A READ-only key into the existing seed-42 scatter — never re-authored.</summary>
        public const string ScatterTreeName = "LP_Tree";

        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger chopped wood is added to. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity (with the axe SELECTED) triggers a chop. Wired at " +
                 "bootstrap; falls back to the ClickToMove root, then a scene search.")]
        public Transform player;

        [Tooltip("The DEMO tree's visual root (instance 0), tweened on felling (sink + tip) + on regrowth " +
                 "(rise back). Wired at bootstrap; falls back to this transform so the chop is still felt even " +
                 "if unwired.")]
        public Transform visual;

        [Tooltip("CHANGE (a) — the LowPolyScatter root whose LP_Tree children are the world's scatter trees. " +
                 "Discovered at Start and made choppable alongside the demo tree, so the chop targets the " +
                 "NEAREST in-range tree anywhere on the island. Wired editor-time; a name-scan fallback finds " +
                 "the scatter root if unwired. READ-only — the seed-42 scatter is never re-authored. Null is " +
                 "tolerated (only the demo tree is then choppable — a bare test rig).")]
        public Transform scatterRoot;

        [Tooltip("The castaway (CastawayCharacter) whose TriggerChop() plays the Mixamo melee SWING clip on each " +
                 "landed chop (86caa4c5c change-(b) — the Animator Attack state replaces the rejected procedural " +
                 "swing). Wired at bootstrap; an Awake scene-search fallback. Null is graceful — the chop still " +
                 "yields wood, just without the arm swing.")]
        public CastawayCharacter character;

        [Tooltip("The inventory/belt UI (CHANGE 1 — left-click chop). A left-click OVER this UI (the always-on " +
                 "belt strip, or the open pack) must NOT chop the tree behind it; the chop asks IsPointerOverUI. " +
                 "Wired at bootstrap; an Awake scene-search fallback. Null is tolerated — the over-UI guard is " +
                 "simply skipped (a bare test rig with no UI), but the modal-panel + RMB-orbit guards still apply.")]
        public InventoryUI inventoryUI;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' a tree and (with the selected " +
                 "axe) chops it. Generous enough that arriving near a tree counts. Mirrors CraftSpot.craftRadius.")]
        public float chopRadius = 2.2f;

        [Tooltip("Wood units yielded per chop (seeds from DefaultChopYield). The `tree-chop wood yield` " +
                 "setting (86caa96rd) drives this live; this is the single named source it reaches.")]
        public int woodPerChop = DefaultChopYield;

        [Tooltip("Chops needed to fell a tree. After this many, the tree falls to a STUMP, then regrows " +
                 "(AC3). Small so the loop reads quickly. Shared across every choppable tree.")]
        public int chopsToFell = 3;

        [Tooltip("Minimum seconds between landed chops (CHANGE 1 — left-click trigger). The chop is per " +
                 "LEFT-CLICK (one strike per click, not auto-paced), so this is a small COOLDOWN: a second " +
                 "chop-click within this window is ignored, so a stray double-edge (or a frantic mash) can't " +
                 "out-pace the swing read. A normal click cadence is always slower than this. 0 = no cooldown.")]
        public float chopInterval = 0.25f;

        [Header("Regrowth (AC3 — TWEAKABLE; the `tree regrowth time` setting drives min/max)")]
        [Tooltip("Minimum seconds before a felled STUMP regrows into a tree. The actual regrow time is " +
                 "RANDOM in [min,max] (organic, not uniform — AC3). Default ~10 min. Shared across trees; the " +
                 "`tree regrowth time` setting (SettingsCatalog.PopulateChop) drives these live.")]
        public float regrowthMinSeconds = 480f;   // 8 min

        [Tooltip("Maximum seconds before a felled stump regrows (random within [min,max]). Default ~12 min " +
                 "so the average regrow is ~10 min (the ticket's '~10 min default').")]
        public float regrowthMaxSeconds = 720f;   // 12 min

        [Tooltip("Seconds after a tree is fully chopped (felled) before it FADES OUT and disappears (Sponsor " +
                 "soak refinement 2, 2026-06-27). REPLACES the persistent stump: the felled tree rests this long, " +
                 "then scales down to nothing + its renderers disable → the ground is empty until regrowth (AC3, " +
                 "~10 min) brings it back at the same spot. Tweakable; default ~10 s per the Sponsor. A scale-down " +
                 "fade (NOT material alpha) keeps the shared opaque material on the ~1-draw-call batch path.")]
        public float fadeOutDelaySeconds = 10f;

        [Header("Swing impact (Sponsor soak refinement 3, 2026-06-27 — sync the EFFECT to the swing's down-stroke)")]
        [Tooltip("Seconds from the chop CLICK to the swing's IMPACT frame — the click fires the swing + face-turn " +
                 "IMMEDIATELY, but the chop EFFECT (per-chop depletion + wood gain + the final bring-down/fall + " +
                 "the fade-out scheduling) lands at IMPACT, so the tree doesn't drop before the axe has visually " +
                 "connected. Default ~0.4 s = the Mixamo Attack clip's down-stroke. SCALES with tool-use speed: " +
                 "the effective delay is this ÷ CastawayCharacter.chopSpeed, so a faster swing impacts sooner (the " +
                 "Animator plays the clip faster too). A NAMED tweakable field per the project pattern. (We use a " +
                 "timer, not an AnimationEvent: headless Time.deltaTime≈0 makes clip events non-deterministic to " +
                 "test, and the timer scales cleanly with chopSpeed — the brief's sanctioned fallback.)")]
        public float swingImpactDelaySeconds = 0.4f;

        [Tooltip("Deterministic seed for the regrow-time rolls (so headless tests are reproducible). 0 = use " +
                 "a time-based seed at runtime. Each instance derives its own sub-seed so trees regrow on " +
                 "distinct timers. Mirrors BerryBush.regrowSeed.")]
        public int regrowSeed = 0;

        // The per-tree choppable instances: index 0 is the demo tree (this component's own visual); the rest
        // are the world scatter trees discovered at Start. The resolver picks the nearest in-range instance.
        private readonly List<ChoppableTreeState> _instances = new List<ChoppableTreeState>();

        // CHANGE 1 — programmatic LEFT-CLICK latch (the input-independent seam, the analog of
        // WasdMovement.RequestJump). A headless PlayMode run / the shipped-build capture can't inject a real
        // mouse button, so a chop-click can be REQUESTED via this latch (consumed once on the next Update).
        private bool _chopClickRequested;
        // CHANGE 1 — wall-clock time of the last landed chop, for the chopInterval click cooldown.
        private float _lastChopAt = float.NegativeInfinity;

        // Refinement 3 (impact timing) — a single in-flight PENDING IMPACT: the click fires the swing+face-turn,
        // then the chop EFFECT lands when Time.time >= _impactAt. _pendingTarget is the tree the effect will hit.
        // Single-flight by construction: a second click while _impactPending is true is IGNORED (no double-apply /
        // no stacked effects — one swing = one impact = one chop effect). Cleared when the effect resolves.
        private bool _impactPending;
        private float _impactAt;
        private ChoppableTreeState _pendingTarget;

        private bool _tracedFirstChop; // one-shot trace guard

        /// <summary>True while a chop swing has been triggered but its EFFECT has not yet landed at impact
        /// (refinement 3). Exposed so a PlayMode test can assert the click→swing→(delay)→effect ordering and the
        /// single-flight guard (a second click while this is true must not stack).</summary>
        public bool ImpactPending => _impactPending;

        /// <summary>Chops landed on the DEMO tree (instance 0), 0..chopsToFell. Exposed for PlayMode tests +
        /// the verify capture (which exercises the demo tree). For an arbitrary instance use
        /// <see cref="ChopsOn"/>.</summary>
        public int Chops => _instances.Count > 0 ? _instances[0].Chops : 0;

        /// <summary>True while the DEMO tree (instance 0) is FELLED (a stump, awaiting regrow).</summary>
        public bool IsFelled => _instances.Count > 0 && _instances[0].Felled;

        /// <summary>Wall-clock time the DEMO tree (instance 0) regrows (meaningful only while felled).</summary>
        public float RegrowAt => _instances.Count > 0 ? _instances[0].RegrowAt : 0f;

        /// <summary>True while the DEMO tree (instance 0) has at least one enabled renderer (visible). False once
        /// it has faded out (refinement 2). Exposed for the fade-out PlayMode test.</summary>
        public bool IsTreeVisible => _instances.Count > 0 && _instances[0].IsVisible;

        /// <summary>True once the DEMO tree (instance 0) has fully faded out + its renderers are disabled — the
        /// ground is empty until regrow (refinement 2 — the persistent stump is gone). Exposed for the test.</summary>
        public bool IsTreeRemoved => _instances.Count > 0 && _instances[0].Removed;

        /// <summary>Number of choppable tree instances currently tracked (1 demo tree + N scatter trees).
        /// Exposed for tests/capture so the generalization is auditable.</summary>
        public int InstanceCount => _instances.Count;

        /// <summary>Chops landed on a given tracked instance (0..chopsToFell). For the per-instance
        /// independence tests.</summary>
        public int ChopsOn(int index) =>
            index >= 0 && index < _instances.Count ? _instances[index].Chops : 0;

        /// <summary>True while a given tracked instance is FELLED (a stump). For the per-instance tests.</summary>
        public bool IsFelledOn(int index) =>
            index >= 0 && index < _instances.Count && _instances[index].Felled;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            if (visual == null) visual = transform;
            if (character == null) character = FindObjectOfType<CastawayCharacter>();
            // CHANGE 1 — the inventory/belt UI for the over-UI left-click guard (serialized editor-time; this
            // scene-search is the build-safety net). Null is tolerated — the over-UI guard is then skipped.
            if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();

            // Instance 0 = the demo tree (this component's own visual). It captures its standing pose at spawn
            // (the tree ships standing). Its derived sub-seed is the raw regrowSeed so the demo tree's regrow
            // roll is byte-identical to the pre-CHANGE-(a) single-tree behaviour (existing tests unchanged).
            _instances.Clear();
            _instances.Add(new ChoppableTreeState(visual, DeriveSeed(0)));
        }

        void Start()
        {
            // CHANGE (a) — discover the world's scatter trees and make each choppable (READ-only — the seed-42
            // scatter is never re-authored). Run in Start (not Awake) so the editor-time-authored scatter under
            // LowPolyScatter is fully present. Each scatter tree is its own ChoppableTreeState keyed on its
            // transform, so it deplete→stump→regrows independently of the demo tree and its neighbours.
            Transform root = scatterRoot;
            if (root == null)
            {
                var found = GameObject.Find("LowPolyScatter");
                if (found != null) root = found.transform;
            }
            if (root == null) return; // no scatter (a bare test rig) — only the demo tree is choppable.

            int idx = _instances.Count;
            CollectScatterTrees(root, ref idx);
            ChopTrace("resolver tracking " + _instances.Count + " choppable trees (1 demo + " +
                      (_instances.Count - 1) + " scatter)");
        }

        // Recursively collect every LP_Tree under the scatter root and register a per-instance chop state.
        // A READ of the existing scatter hierarchy — no GameObject is created/moved/destroyed here.
        private void CollectScatterTrees(Transform root, ref int idx)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == ScatterTreeName)
                {
                    _instances.Add(new ChoppableTreeState(child, DeriveSeed(idx)));
                    idx++;
                }
                else if (child.childCount > 0)
                {
                    CollectScatterTrees(child, ref idx);
                }
            }
        }

        // A deterministic per-instance regrow sub-seed: index 0 (the demo tree) uses the raw regrowSeed so its
        // regrow roll is unchanged from the single-tree era; later instances XOR in the index so trees regrow
        // on distinct timers. 0 stays 0 (time-based seed at runtime) for index 0 to preserve the old behaviour.
        private int DeriveSeed(int index)
        {
            if (index == 0) return regrowSeed;
            int baseSeed = regrowSeed != 0 ? regrowSeed : 1;
            // Mix the index in with a golden-ratio odd multiplier (wraps via unchecked) so each scatter tree
            // gets a distinct, deterministic regrow sub-seed; OR 1 so the result is never 0 (which would mean
            // "time-based" in ChoppableTreeState's ctor → non-reproducible in tests).
            unchecked
            {
                int mixed = baseSeed ^ (index * (int)0x9E3779B1);
                return mixed | 1;
            }
        }

        void Update()
        {
            // Tick EVERY instance's one-shot felling / fade / regrow tweens + timers independently of input.
            for (int i = 0; i < _instances.Count; i++)
                _instances[i].Tick();

            // Refinement 3 — resolve a PENDING IMPACT when its scheduled impact time arrives (the chop EFFECT
            // lands at the swing's down-stroke, NOT on the click frame). Done BEFORE reading a new click so a
            // click can't be processed in the same frame an impact resolves.
            if (_impactPending && Time.time >= _impactAt)
            {
                _impactPending = false;
                ChoppableTreeState t = _pendingTarget;
                _pendingTarget = null;
                ApplyChopEffect(t);
            }

            if (inventory == null || player == null) return;

            // CHANGE 1 — the chop is triggered by an ACTIVE LEFT-CLICK (like an attack), NOT proximity-auto.
            // Read the real mouse rising edge OR consume the programmatic latch (the headless / shipped-build
            // seam). Consume the latch unconditionally each frame (one chop per RequestChopClick) so it can't
            // stick across frames.
            bool clickEdge = _chopClickRequested || Input.GetMouseButtonDown(0);
            _chopClickRequested = false;
            if (!clickEdge) return;

            // Refinement 3 — SINGLE-FLIGHT: a second click while a swing's impact is still pending is IGNORED, so
            // a mid-swing click can't double-apply or stack effects (one swing = one impact = one chop effect).
            if (_impactPending) return;

            // The full chop-on-click decision (pure static so the guard truth-table is unit-testable): the
            // axe-SELECTED gate (load-bearing) + the three world-click guards (modal panel open; pointer over
            // the inventory/belt UI; RMB camera-orbit drag). "inRange" is supplied by the NEAREST-tree resolve
            // below: a click only chops when SOME standing tree is within chopRadius.
            bool overUI = inventoryUI != null && inventoryUI.IsPointerOverUI(Input.mousePosition);
            bool rmbHeld = Input.GetMouseButton(1);

            // CHANGE (a) — resolve the NEAREST in-range, still-standing tree (AC5). null = no choppable tree in
            // reach → the click is a harmless world-click (exactly like clicking past every tree).
            ChoppableTreeState target = ResolveNearestChoppable(player.position);
            bool inRange = target != null;

            if (!ShouldChopOnClick(inRange, inventory.IsAxeSelectedInBelt,
                                   UiInputGate.CaptureWorldInput, overUI, rmbHeld))
                return;

            // Click cooldown: ignore a chop that lands within chopInterval of the last (a stray double-edge /
            // mash can't out-pace the swing). A deliberate click cadence is always slower than this.
            if (Time.time - _lastChopAt < Mathf.Max(0f, chopInterval)) return;
            _lastChopAt = Time.time;

            // Refinement 3 — fire the SWING + FACE-TURN NOW (on the click), but SCHEDULE the EFFECT for the
            // swing's IMPACT frame. The tree drops / wood ticks at the down-stroke, not before the axe connects.
            BeginChopSwing(target);
        }

        /// <summary>
        /// Resolve the NEAREST still-standing tree within <see cref="chopRadius"/> of <paramref name="from"/>
        /// (CHANGE (a) — AC5 "the chop targets the nearest in-range tree"). Felled/felling/regrowing instances
        /// are skipped (a stump is not choppable). Returns null when no standing tree is in reach. Planar (XZ)
        /// distance only (height-robust — same as CraftSpot / BerryBush).
        /// </summary>
        private ChoppableTreeState ResolveNearestChoppable(Vector3 from)
        {
            ChoppableTreeState best = null;
            float bestSq = chopRadius * chopRadius;
            Vector2 here = new Vector2(from.x, from.z);
            for (int i = 0; i < _instances.Count; i++)
            {
                ChoppableTreeState s = _instances[i];
                if (!s.IsChoppable) continue; // a stump / mid-tween tree can't be chopped
                Vector3 p = s.Position;
                float dSq = (here - new Vector2(p.x, p.z)).sqrMagnitude;
                if (dSq <= bestSq)
                {
                    bestSq = dSq;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>
        /// PURE chop-on-a-left-click decision (CHANGE 1 — the unit-testable guard truth-table). Given a
        /// left-click edge this frame, decide whether ONE chop should land:
        ///   • <paramref name="inRange"/> — SOME standing tree is within <see cref="chopRadius"/> (the nearest);
        ///   • <paramref name="axeSelected"/> — the axe is the SELECTED belt item (the load-bearing gate);
        ///   • NOT <paramref name="uiPanelOpen"/> — no modal panel owns the screen;
        ///   • NOT <paramref name="pointerOverUI"/> — the click is NOT over the inventory/belt UI;
        ///   • NOT <paramref name="rmbHeld"/> — the right mouse button is NOT held (no camera-orbit drag).
        /// All five must hold. Static + dependency-free so the EditMode guard asserts the whole table with no
        /// scene/Input/UI rig.
        /// </summary>
        public static bool ShouldChopOnClick(bool inRange, bool axeSelected,
                                             bool uiPanelOpen, bool pointerOverUI, bool rmbHeld)
            => inRange && axeSelected && !uiPanelOpen && !pointerOverUI && !rmbHeld;

        /// <summary>
        /// Request ONE chop strike programmatically — the input-independent analog of a left-click (CHANGE 1).
        /// Latched + consumed on the next Update (mirrors the mouse's rising edge — one chop per call), so a
        /// headless PlayMode test + the shipped-build chop capture trigger a chop where a real mouse button
        /// can't be injected. The nearest-in-range + axe-selected + over-UI/RMB guards still apply.
        /// Refinement 3: like a real click this fires the swing+face-turn NOW but lands the EFFECT (wood / fell /
        /// fade) at the swing IMPACT (~swingImpactDelaySeconds later) — a caller polling for wood must advance
        /// time past impact; a re-request while <see cref="ImpactPending"/> is a no-op (single-flight).
        /// </summary>
        public void RequestChopClick() => _chopClickRequested = true;

        /// <summary>
        /// Land one chop on the DEMO tree (instance 0) directly + IMMEDIATELY (swing + effect now, no impact
        /// delay), regardless of range. Public so the existing PlayMode swing test can drive a chop in isolation
        /// (it asserts the swing fires + the demo tree's chop count) without advancing time to impact. The live
        /// click flow uses the impact-delayed path (BeginChopSwing); this is the demo-tree convenience seam.
        /// </summary>
        public void Chop()
        {
            if (_instances.Count == 0) return;
            ChoppableTreeState target = _instances[0];
            if (target == null || !target.IsChoppable) return;
            if (character != null) { character.FaceWorldTarget(target.Position); character.TriggerChop(); }
            ApplyChopEffect(target);
        }

        // Refinement 3 — BEGIN a chop swing on the click: FACE the tree (refinement 1) + SWING the arm (AC1) NOW,
        // and SCHEDULE the chop EFFECT for the swing's IMPACT frame (so the tree doesn't drop before the axe has
        // visually connected). The effective delay scales with tool-use speed (a faster swing — the Animator plays
        // the clip faster — impacts sooner). Single-flight: callers gate on !_impactPending before invoking this.
        private void BeginChopSwing(ChoppableTreeState target)
        {
            if (target == null || !target.IsChoppable) return;

            // FACE THE TREE (refinement 1) — turn the player to face the RESOLVED target tree (Y-yaw only) so the
            // downward strike reads as hitting THAT tree. SWING the arm (AC1) — TriggerChop plays the Mixamo melee
            // Attack state; the held axe (HeldAxeRig) follows the swung hand. Both NOW, on the click. Null-safe.
            if (character != null)
            {
                character.FaceWorldTarget(target.Position);
                character.TriggerChop();
            }

            // SCHEDULE the effect for the swing's down-stroke. Scale the delay by tool-use speed (the same
            // CastawayCharacter.chopSpeed that scales the Animator's ChopSpeed playback rate), so the timer tracks
            // the clip's impact frame across speeds. A null character → unscaled delay (1×).
            float speed = character != null
                ? Mathf.Clamp(character.chopSpeed, CastawayCharacter.ChopSpeedMin, CastawayCharacter.ChopSpeedMax)
                : 1f;
            float delay = Mathf.Max(0f, swingImpactDelaySeconds) / Mathf.Max(0.0001f, speed);
            _pendingTarget = target;
            _impactAt = Time.time + delay;
            _impactPending = true;
        }

        // Refinement 3 — APPLY the chop effect at IMPACT: yield wood (AC2), advance the count, and fell it on the
        // final chop (AC3 — fade-out then regrow). Re-checks IsChoppable (the target could have changed state in
        // the delay window — e.g. an external regrow tick) so a stale pending impact can't double-fell. This is the
        // moment the tree's bring-down/fall + the fade-out scheduling happen — synced to the visual hit.
        private void ApplyChopEffect(ChoppableTreeState target)
        {
            if (target == null || !target.IsChoppable || inventory == null) return;

            inventory.AddWood(Mathf.Max(1, woodPerChop));
            bool felled = target.LandChop(chopsToFell, regrowthMinSeconds, regrowthMaxSeconds, fadeOutDelaySeconds);

            if (!_tracedFirstChop)
            {
                _tracedFirstChop = true;
                ChopTrace("impact " + target.Chops + "/" + chopsToFell + " -> wood=" + inventory.WoodCount +
                          " (swing=" + (character != null) + ")");
            }
            if (felled)
                ChopTrace("tree FELLED after " + target.Chops + " chops (total wood=" + inventory.WoodCount +
                          "); regrow in " + (target.RegrowAt - Time.time).ToString("F0") + "s");
        }

        // [chop-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call
        // (AND its argument evaluation, incl. the string concatenation) from the shipped IL2CPP release exe
        // (unity6-mastery §5 "no Debug.Log in hot paths" / §10 "strip all logging from shipping builds").
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void ChopTrace(string msg) => Debug.Log("[chop-trace] " + msg);
    }

    /// <summary>
    /// Per-tree CHOP STATE (CHANGE (a) — extracted from ChopTree so EVERY world tree is choppable, each with
    /// its OWN chops-landed / felled / fade / regrow lifecycle). A plain class (no MonoBehaviour): ChopTree owns
    /// a list of these — the demo tree + every scatter LP_Tree — and drives them, so a felled tree fades away in
    /// place while its neighbours stand, then regrows at the same spot on its own random timer.
    ///
    /// === FELL → FADE-OUT → REMOVED → REGROW lifecycle (Sponsor soak refinement 2, 2026-06-27) ===
    /// REPLACES the old persistent-stump behaviour. On the felling chop the tree does a brief sink+tip fell tween,
    /// rests for <paramref name="fadeOutDelaySeconds"/> (~10s, a NAMED tweakable field on ChopTree), then SCALES
    /// DOWN to nothing (a batching-safe fade — scale, NOT material alpha, so the shared OPAQUE LowPolyVertexColor
    /// material stays on the ~1-draw-call path; a transparent variant would cost draw calls) and DISABLES its
    /// renderers → the ground is empty. The AC3 REGROWTH (~10 min, tweakable [min,max]) still fires: at _regrowAt
    /// the renderers re-enable + the tree scales back up to standing at the SAME spot, choppable anew.
    ///
    /// Wall-clock paced via Time.unscaledDeltaTime + Time.time (headless deltas are ~0, so a headless run lands
    /// at each tween's end state quickly without affecting the wood/felled/fade/regrow assertions —
    /// unity-conventions.md headless-time discipline). NO mutable statics (instance state only).
    /// </summary>
    public class ChoppableTreeState
    {
        private readonly Transform _visual;     // this tree's tweened root (the LP_Tree transform / demo visual)
        private readonly System.Random _rng;    // this tree's own regrow-time roll (distinct per instance)
        private readonly Renderer[] _renderers; // this tree's renderers (disabled when removed, re-enabled on regrow)

        private int _chops;          // chops landed on THIS tree (resets on regrow)
        private bool _felled;        // true from the felling chop until the tree fully regrows
        private float _regrowAt;     // wall-clock time this tree regrows (meaningful only while felled)
        private float _fadeAt;       // wall-clock time the felled tree begins its fade-out (fell + fadeOutDelay)

        private bool _felling;       // playing the sink+tip fell tween
        private bool _fadingOut;     // playing the scale-down fade-out tween
        private bool _removed;       // faded out + renderers disabled, awaiting regrow (ground empty)
        private bool _regrowing;     // playing the scale-up regrow tween
        private float _tweenT;
        private Vector3 _standPos;
        private Quaternion _standRot;
        private Vector3 _standScale = Vector3.one; // the standing localScale (the fade-out/regrow scale anchor)

        private const float FellDuration = 0.5f;
        private const float FadeOutDuration = 0.8f;   // the scale-down-to-nothing tween length
        private const float RegrowRiseDuration = 0.6f;
        // The felled pose offset from standing — the fell tween's end state (a brief sink+tip before the fade).
        private static readonly Vector3 StumpDrop = Vector3.down * 0.6f;
        private const float StumpTipDeg = 70f;

        public ChoppableTreeState(Transform visual, int seed)
        {
            _visual = visual;
            // Capture the standing pose + scale at construction (each tree ships standing). The renderers are
            // toggled off when the tree is removed (post-fade) and back on when it regrows.
            if (_visual != null)
            {
                _standPos = _visual.position;
                _standRot = _visual.rotation;
                _standScale = _visual.localScale;
                _renderers = _visual.GetComponentsInChildren<Renderer>(true);
            }
            _rng = new System.Random(seed != 0 ? seed : Environment.TickCount);
        }

        public int Chops => _chops;
        public bool Felled => _felled;
        public float RegrowAt => _regrowAt;
        /// <summary>Wall-clock time this felled tree begins fading out (fell-time + fadeOutDelay). Exposed for
        /// the fade-out PlayMode test.</summary>
        public float FadeAt => _fadeAt;
        /// <summary>True once the felled tree has fully faded out + its renderers are disabled (the ground is
        /// empty until regrow). Exposed for the fade-removal test.</summary>
        public bool Removed => _removed;
        /// <summary>True if any of this tree's renderers is currently enabled (the visible/gone discriminator
        /// the fade-out test asserts — false after the fade, true again after regrow).</summary>
        public bool IsVisible
        {
            get
            {
                if (_renderers == null) return false;
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null && _renderers[i].enabled) return true;
                return false;
            }
        }
        /// <summary>This tree's world position (its visual root) — the resolver's nearest-in-range key.</summary>
        public Vector3 Position => _visual != null ? _visual.position : Vector3.zero;
        /// <summary>True only when this tree is STANDING (not felled, not mid-tween) → chop-eligible.</summary>
        public bool IsChoppable => !_felled && !_felling && !_fadingOut && !_removed && !_regrowing;

        // Advance this tree's one-shot tweens + fade/regrow timers one frame. Called every Update by ChopTree.
        public void Tick()
        {
            if (_felling) { StepFelling(); return; }
            if (_fadingOut) { StepFadeOut(); return; }
            if (_regrowing) { StepRegrow(); return; }
            if (_removed)
            {
                // Ground is empty (faded out). Regrow at _regrowAt → re-enable renderers + scale back up.
                if (Time.time >= _regrowAt) BeginRegrow();
                return;
            }
            // Felled + resting (post-fell tween, pre-fade): begin the fade-out once the delay elapses. (A regrow
            // scheduled SOONER than the fade — e.g. a fast-test regrow window — short-circuits straight to regrow
            // so the tree never gets stuck mid-cycle; the normal ~10s fade ≪ ~10min regrow.)
            if (_felled)
            {
                if (Time.time >= _regrowAt) { BeginRegrow(); return; }
                if (Time.time >= _fadeAt) BeginFadeOut();
            }
        }

        /// <summary>
        /// Land one chop on this tree: advance the count, and on the <paramref name="chopsToFell"/>-th chop fell
        /// it — begin the sink+tip fell tween, schedule the fade-out (fell + <paramref name="fadeOutDelaySeconds"/>)
        /// and a random regrow in [min,max]. Returns true on the felling chop. A no-op if the tree isn't currently
        /// choppable (mid-cycle / removed). The CALLER yields the wood + swings the arm (shared across instances).
        /// </summary>
        public bool LandChop(int chopsToFell, float regrowthMinSeconds, float regrowthMaxSeconds,
                             float fadeOutDelaySeconds)
        {
            if (!IsChoppable) return false;
            _chops++;
            if (_chops >= chopsToFell)
            {
                _felled = true;
                BeginFelling();
                _fadeAt = Time.time + Mathf.Max(0f, fadeOutDelaySeconds);
                ScheduleRegrow(regrowthMinSeconds, regrowthMaxSeconds);
                return true;
            }
            return false;
        }

        // Schedule the regrow at a RANDOM time within [min,max] (AC3 — organic, not uniform). Min clamped
        // non-negative; max clamped >= min so a mis-authored max never schedules a regrow in the past.
        private void ScheduleRegrow(float regrowthMinSeconds, float regrowthMaxSeconds)
        {
            float min = Mathf.Max(0f, regrowthMinSeconds);
            float max = Mathf.Max(min, regrowthMaxSeconds);
            float delay = min + (float)_rng.NextDouble() * (max - min);
            _regrowAt = Time.time + delay;
        }

        // Start the thin-but-felt felling tween: capture the standing pose so StepFelling can sink+tip.
        private void BeginFelling()
        {
            if (_visual == null) { _felling = false; return; }
            _standPos = _visual.position;
            _standRot = _visual.rotation;
            _standScale = _visual.localScale;
            _felling = true;
            _tweenT = 0f;
        }

        // One frame of the felling tween — the tree sinks + tips over (the fall), then rests until the fade-out.
        private void StepFelling()
        {
            if (_visual == null) { _felling = false; return; }
            _tweenT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_tweenT / FellDuration);
            float ease = k * k * (3f - 2f * k); // smoothstep
            _visual.position = _standPos + StumpDrop * ease;
            _visual.rotation = _standRot * Quaternion.Euler(StumpTipDeg * ease, 0f, 0f);
            if (k >= 1f) _felling = false; // now resting (fallen) until the fade-out delay elapses
        }

        // Begin the fade-out: scale the fallen tree down to nothing (batching-safe — scale, not material alpha).
        private void BeginFadeOut()
        {
            _fadingOut = true;
            _tweenT = 0f;
        }

        // One frame of the fade-out — ease the visual scale from standing toward ~0, then disable the renderers
        // so the tree DISAPPEARS (the ground is empty until regrow). Scale-down (not a transparent material) keeps
        // the shared opaque LowPolyVertexColor batching intact (~1 draw call).
        private void StepFadeOut()
        {
            if (_visual == null) { _fadingOut = false; _removed = true; return; }
            _tweenT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_tweenT / FadeOutDuration);
            float ease = k * k * (3f - 2f * k); // smoothstep
            _visual.localScale = _standScale * (1f - ease);
            if (k >= 1f)
            {
                _fadingOut = false;
                _removed = true;
                SetRenderersEnabled(false);     // gone — ground empty until regrow
                _visual.localScale = _standScale; // restore the scale under the disabled renderers (regrow anchor)
            }
        }

        // Begin the regrow rise: re-enable the renderers + scale up from ~0 back to standing at the same spot.
        private void BeginRegrow()
        {
            _removed = false;
            _regrowing = true;
            _tweenT = 0f;
            // Restore the standing pose (the fell tween left it sunk+tipped) and re-show, scaling up from ~0.
            if (_visual != null)
            {
                _visual.position = _standPos;
                _visual.rotation = _standRot;
                _visual.localScale = _standScale * 0.001f;
            }
            SetRenderersEnabled(true);
        }

        // One frame of the regrow rise — ease the visual scale from ~0 back to standing. On completion the tree is
        // STANDING + choppable again, chops reset (AC3: the tree regrows at the same spot after the timer).
        private void StepRegrow()
        {
            if (_visual == null) { _regrowing = false; _felled = false; _chops = 0; return; }
            _tweenT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_tweenT / RegrowRiseDuration);
            float ease = k * k * (3f - 2f * k); // smoothstep
            _visual.localScale = _standScale * Mathf.Max(0.001f, ease);
            if (k >= 1f)
            {
                _regrowing = false;
                _felled = false;
                _chops = 0;         // a fresh tree — chop it anew
                _visual.position = _standPos;
                _visual.rotation = _standRot;
                _visual.localScale = _standScale;
            }
        }

        private void SetRenderersEnabled(bool on)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = on;
        }
    }
}
