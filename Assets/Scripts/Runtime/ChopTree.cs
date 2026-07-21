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
    /// === HOLD-TO-CHOP (86caf7a0p, Sponsor wish 2026-06-27 — on his #140 chop-soak approval) ===
    /// Holding LEFT MOUSE keeps the castaway swinging the axe (repeat swings) on the LOCKED target tree until it
    /// fells — instead of one click per swing. Built on #140's seam VERBATIM (TriggerChop / the Attack state / the
    /// impact-timing / ResolveNearestChoppable / the per-instance state) — NO parallel swing path. The repeat is
    /// driven by SWING COMPLETION (the impact resolving), NOT an input-poll rate, so the one-swing-one-impact-one-
    /// wood invariant (#140 AC1) holds under the hold loop + the cadence rides the tool-use-speed clip (AC3).
    ///   • AC1 HOLD-TO-REPEAT: while LMB is HELD (axe selected + a choppable tree in range) swings repeat — each a
    ///     full Attack-clip cycle landing one chop EFFECT at its impact. On RELEASE the repeat stops; a swing
    ///     already mid-flight completes its committed impact (no new swing starts). A single click (press+release
    ///     within one swing) still produces EXACTLY ONE swing (back-compat — one-click-one-swing).
    ///   • AC2 STOP-ON-FALL: when the locked target fells (depleted → fade-out), the chain STOPS even if the
    ///     button is still held (no swinging at empty ground). Default (this v1): a re-press is required to start a
    ///     NEW chain on the next tree — the chain does NOT auto-re-acquire a different tree mid-hold. A FRESH PRESS
    ///     re-resolves the nearest target (so the next press chops the next tree).
    ///   • AC3 INVARIANT: the single-flight guard (a swing can begin only when no impact is pending) makes the
    ///     repeat cadence = one swing per impact, frame-rate-independent + tool-use-speed-scaled.
    /// The chain locks onto the tree resolved at the START of the hold (<see cref="IsChopChainActive"/> exposes the
    /// lock); <see cref="SetChopHeld"/> is the input-independent held seam (the analog of the held mouse button,
    /// like <see cref="RequestChopClick"/> is the click seam) so headless PlayMode + the shipped capture exercise
    /// the SAME hold path. The optional inter-swing cooldown is <see cref="chopInterval"/> (default 0.25s — a small
    /// gap for rhythmic chopping, NOT machine-gun; 0 would be back-to-back).
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
    /// === AC2 — wood yield is awarded ON FELL as a LOOTABLE LOG PILE (REWORK 86caf9u5t — supersedes #157) ===
    /// Chopping yields NO wood per swing (the per-chop Inventory.AddWood is REMOVED — AC1). Instead, on the
    /// FELLING chop the tree spawns a lootable <see cref="LogPile"/> at its spot holding the WHOLE tree's wood
    /// (the `tree-chop wood yield` setting via <see cref="logPileSpawner"/>, default 10). The player loots the
    /// pile with E (the shared <see cref="PickableLooter"/> path), grabbing the whole pile or what fits — the
    /// remainder persists (AC7). Sponsor's grilled redesign: "I should not get the wood before the tree has
    /// fallen … the yield setting is how much a FALLEN tree yields, not each chop." After chopsToFell chops the
    /// resolved tree fells + drops its pile.
    /// (Legacy doc retained for context:)
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
    /// On the felling chop the tree SINKS + tips, then ~<see cref="fadeOutDelaySeconds"/> (default 2s — the
    /// #165-soak NIT 86caff4ad; LIVE-dialable via the `fallen-tree fade-out` setting) later FADES OUT — it
    /// scales down to nothing + its renderers disable, so the tree DISAPPEARS and the
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

        /// <summary>AC4 — the NAMED default chops-to-fell. The `chops-to-fell` setting (86caf9u5t) drives
        /// <see cref="chopsToFell"/>; default 3, the setting clamps within [<see cref="ChopsToFellMin"/>,
        /// <see cref="ChopsToFellMax"/>]. A named constant so the setting + tests reference one source.</summary>
        public const int ChopsToFellDefault = 3;

        /// <summary>AC4 range floor — the `chops-to-fell` setting clamps within [1, 10] (integer).</summary>
        public const int ChopsToFellMin = 1;

        /// <summary>AC4 range ceiling — the `chops-to-fell` setting clamps within [1, 10] (integer).</summary>
        public const int ChopsToFellMax = 10;

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

        [Tooltip("REWORK 86caf9u5t — the shared LogPileSpawner that mints a lootable LogPile when a tree fells " +
                 "(holding the `tree-chop wood yield` logs, despawning per the `log-pile despawn` setting). Wired " +
                 "at bootstrap; an Awake scene-search fallback. Null is tolerated — felling still completes (the " +
                 "tree falls + fades) but NO pile drops (a bare test rig with no spawner can assert the fell path " +
                 "without the pickable). The whole tree's wood is awarded ONCE, on fell, as this pile — never per " +
                 "chop (AC1/AC2).")]
        public LogPileSpawner logPileSpawner;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' a tree and (with the selected " +
                 "axe) chops it. Generous enough that arriving near a tree counts. Mirrors CraftSpot.craftRadius.")]
        public float chopRadius = 2.2f;

        [Tooltip("DEPRECATED (REWORK 86caf9u5t) — NO LONGER awards wood per chop. The wood is now awarded ONCE " +
                 "on FELL as a lootable LogPile (logPileSpawner.WoodYield logs), NOT per swing (AC1). This field " +
                 "is retained INERT only so legacy callers/tests compile; ApplyChopEffect no longer reads it for " +
                 "an inventory add. Do not reintroduce a per-chop AddWood.")]
        public int woodPerChop = DefaultChopYield;

        [Tooltip("Chops needed to fell a tree (AC4 — the `chops-to-fell` setting drives this live, default 3, " +
                 "range 1–10). After this many, the tree falls + drops its log pile, then regrows (AC3). Shared " +
                 "across every choppable tree (the resolver applies it to whichever tree the chop lands on).")]
        public int chopsToFell = ChopsToFellDefault;

        [Tooltip("AC6 — per-chop SHAKE/RECOIL amount (degrees of the brief tip-impulse each non-felling chop " +
                 "gives the tree). A cheap transform nudge (no new art/audio): the tree recoils a few degrees on " +
                 "the chop impact, then eases back. Soak-tune the amount. 0 disables the shake.")]
        public float chopShakeDegrees = 6f;

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

        /// <summary>AC1 (86caff4ad) — the NAMED default fallen-tree fade-out delay. The `fallen-tree fade-out`
        /// SETTING (SettingsCatalog.PopulateChop) drives <see cref="fadeOutDelaySeconds"/>; default 2s (the
        /// Sponsor's #165-soak NIT: 10s felt too long), clamped within [<see cref="FadeOutDelayMin"/>,
        /// <see cref="FadeOutDelayMax"/>]. A named constant so the setting + tests reference one source.</summary>
        public const float FadeOutDelayDefault = 2f;

        /// <summary>AC2 range floor — the `fallen-tree fade-out` slider band, in seconds (0 = fade immediately).</summary>
        public const float FadeOutDelayMin = 0f;

        /// <summary>AC2 range ceiling — the `fallen-tree fade-out` slider band, in seconds (~0–30s).</summary>
        public const float FadeOutDelayMax = 30f;

        [Tooltip("Seconds after a tree is fully chopped (felled) before it FADES OUT and disappears (Sponsor " +
                 "soak refinement 2, 2026-06-27; default tightened to 2 s per the #165-soak NIT 86caff4ad). " +
                 "REPLACES the persistent stump: the felled tree rests this long, then scales down to nothing + " +
                 "its renderers disable → the ground is empty until regrowth (AC3, ~10 min) brings it back at the " +
                 "same spot. Tweakable LIVE via the `fallen-tree fade-out` settings-panel row (0–30 s). A " +
                 "scale-down fade (NOT material alpha) keeps the shared opaque material on the ~1-draw-call path.")]
        public float fadeOutDelaySeconds = FadeOutDelayDefault;

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

        [Header("Swing CADENCE (86caf7a0p re-iter — the next HOLD swing waits for the clip to FINISH)")]
        [Tooltip("FALLBACK authored length (seconds, at 1× speed) of the chop swing clip (the Mixamo melee " +
                 "one-shot). HOLD-TO-CHOP gates the NEXT swing on the CURRENT swing's clip COMPLETING — the next " +
                 "swing cannot begin until (clip length ÷ tool-use speed) has elapsed since this swing started, so " +
                 "the swing animation plays to COMPLETION and ONE completed swing = exactly ONE chop (the Sponsor's " +
                 "soak-reject fix: 'the animation is not allowed to finish and the tree goes down too fast'). The " +
                 "LIVE cadence prefers CastawayCharacter.MeleeClipLength (read from the actual imported clip); this " +
                 "serialized value is the fallback used only when no Animator/controller/clip is available (a bare " +
                 "headless test rig). ~1.6 s ≈ the authored Mixamo 'Standing Melee Attack Downward' length; the " +
                 "live read overrides it in the shipped build. Must be ≥ swingImpactDelaySeconds (impact lands " +
                 "mid-clip; the clip finishes after).")]
        public float swingClipLengthSeconds = 1.6f;

        /// <summary>86caf9ngh N2 — strictly-positive floor for the <see cref="swingClipLengthSeconds"/> fallback used
        /// in <see cref="ComputeSwingDuration"/>. A degenerate serialized value (0 or negative on a misconfigured
        /// Animator-less bare rig) would collapse the swing duration to 0 and disable the hold-repeat cadence gate;
        /// this floors the fallback so the gate always has a non-zero spacing. Small enough not to perturb a sane
        /// config (the live build reads the real clip length and never touches this floor).</summary>
        public const float SwingClipLengthFloor = 0.05f;

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

        // HOLD-TO-CHOP (86caf7a0p) — programmatic LMB-HELD state (the input-independent seam, the analog of the
        // held-mouse-button for the swing chain). A headless PlayMode run / the shipped capture can't hold a real
        // mouse button, so the held state is set via SetChopHeld(true/false); the live Update OR's it with the
        // real Input.GetMouseButton(0). When held + chain-eligible, the swing REPEATS automatically (one full
        // swing → its impact → the next swing) until the locked target fells (AC2) or the button is released (AC1).
        private bool _chopHeldRequested;

        // Refinement 3 (impact timing) — a single in-flight PENDING IMPACT: the click fires the swing+face-turn,
        // then the chop EFFECT lands when Time.time >= _impactAt. _pendingTarget is the tree the effect will hit.
        // Single-flight by construction: a second click while _impactPending is true is IGNORED (no double-apply /
        // no stacked effects — one swing = one impact = one chop effect). Cleared when the effect resolves.
        private bool _impactPending;
        private float _impactAt;
        private ChoppableTreeState _pendingTarget;

        // HOLD-TO-CHOP cadence (86caf7a0p re-iter — the Sponsor soak-reject fix). The wall-clock time the CURRENT
        // swing's CLIP finishes playing (swing-start + clip length ÷ tool-use speed). The NEXT swing of a held
        // chain cannot begin until Time.time >= this, so the swing ANIMATION plays to COMPLETION before the next
        // swing starts — ONE completed swing = exactly ONE chop (no mid-clip restart cutting the animation off,
        // which was making the tree go down too fast). The impact still lands mid-clip (swingImpactDelaySeconds),
        // but the REPEAT GATE is clip completion, not the impact. NegativeInfinity = no swing in flight (the gate
        // is open). A single click does NOT consult this (it never chains); only the hold-repeat path gates on it.
        private float _swingEndsAt = float.NegativeInfinity;

        // HOLD-TO-CHOP (86caf7a0p) — the LOCKED chain target. A swing chain locks onto the tree resolved at the
        // START of the hold (the first press's nearest-in-range tree) and keeps swinging THAT tree while the
        // button is held — it does NOT re-acquire a different tree mid-hold (AC2: default = stop on fall, require
        // a re-press for the next tree). Non-null only while a chain is active (a swing is pending OR the next
        // swing is about to begin on the held button). Cleared on release, on fell (the locked target stops being
        // choppable → STOP-ON-FALL), or when the resolved target no longer matches (the player walked away).
        private ChoppableTreeState _chainTarget;
        // True for one frame after a fresh press is observed (rising edge) — distinguishes "start a NEW chain on a
        // new tree" from "continue the existing chain". A press always re-resolves the nearest target (AC: a fresh
        // press on a new tree starts a new chain); a held continuation stays locked on _chainTarget.
        private bool _freshPress;
        // Remembers the previous frame's held state so the live Update can detect the rising edge (press) WITHOUT
        // relying solely on Input.GetMouseButtonDown (the programmatic seam has no edge event — it's a level latch).
        private bool _heldLastFrame;
        // STOP-ON-FALL latch (AC2): set true when a chain ends because its locked target felled while the button
        // is STILL held. It suppresses auto-acquiring a neighbour for the rest of THIS continuous hold — the
        // player must RELEASE + re-press to start a new chain on the next tree (the v1 default: stop on fall,
        // re-press for the next tree). Cleared on release (the no-held bail path) so the next press chains anew.
        private bool _suppressUntilRepress;

        private bool _tracedFirstChop; // one-shot trace guard

        /// <summary>True while a chop swing has been triggered but its EFFECT has not yet landed at impact
        /// (refinement 3). Exposed so a PlayMode test can assert the click→swing→(delay)→effect ordering and the
        /// single-flight guard (a second click while this is true must not stack).</summary>
        public bool ImpactPending => _impactPending;

        /// <summary>HOLD-TO-CHOP cadence (86caf7a0p re-iter) — true while the CURRENT swing's CLIP is still playing
        /// (Time.time &lt; the swing-end time). A held chain cannot begin its NEXT swing while this is true — the
        /// swing animation must finish first (the Sponsor soak-reject fix). Note this stays true PAST the impact
        /// (the impact lands mid-clip; the clip keeps playing until completion), which is exactly why the cadence
        /// is now ≥ clip length, not the shorter impact delay. Exposed so a PlayMode test can assert the next swing
        /// is gated on clip completion (no mid-clip restart).</summary>
        public bool SwingInProgress => Now < _swingEndsAt;

        /// <summary>HOLD-TO-CHOP cadence (86caf7a0p re-iter) — the effective per-swing CLIP DURATION (seconds) the
        /// repeat gate uses for the GIVEN tool-use speed: the live clip length (CastawayCharacter.MeleeClipLength)
        /// when available else the serialized <see cref="swingClipLengthSeconds"/> fallback, divided by the clamped
        /// chopSpeed. This is the minimum spacing between two completed swings of a hold chain. Exposed so a
        /// PlayMode/EditMode test can assert the cadence ties to the clip length (≥ it at 1×) and scales with
        /// speed — without reaching into private timing state.</summary>
        public float EffectiveSwingDurationSeconds => ComputeSwingDuration();

        /// <summary>HOLD-TO-CHOP (86caf7a0p) — true while a swing CHAIN is active: the button is held and a tree is
        /// locked as the chain target (a swing is pending, or the next swing is about to begin on the held button).
        /// Goes false when the button is released, when the locked target fells (STOP-ON-FALL, AC2), or when the
        /// player leaves the target's range. Exposed so a PlayMode test can assert the chain starts on hold,
        /// repeats per swing, and stops on release / on fall.</summary>
        public bool IsChopChainActive => _chainTarget != null;

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

#if UNITY_INCLUDE_TESTS
        /// <summary>86camdk1h — TEST-ONLY deterministic clock (public seam, STRIPPED from ship builds via
        /// UNITY_INCLUDE_TESTS; the project has no InternalsVisibleTo, so the codebase's "public for tests" seam
        /// convention applies — mirrors NextIslandPocScatter.BuildTreeForTest / SettingsPanel's focus seams).
        /// Headless <c>-batchmode</c> PlayMode does NOT honor <c>Time.captureDeltaTime</c> (empirically — see the
        /// PR body: the #255 pin is present at HEAD yet the chop cadence tests still over-count in CI), so a
        /// PlayMode test injects this fake clock and advances it a fixed step per frame — a WORKING
        /// captureDeltaTime — making the <c>Time.time</c>-based cadence gates (impact / clip-completion / cooldown)
        /// AND each tree's fade/regrow timers deterministic and clock-INDEPENDENT while keeping the SAME shipped
        /// gate logic LIVE (a real machine-gun / double-apply / too-fast-regrow regression still reds the tests).
        /// Setting it propagates to every already-created <see cref="ChoppableTreeState"/> so the demo tree (built
        /// in Awake, before a test can set this) and the scatter trees (built in Start) all read one clock. Null →
        /// <c>Time.time</c> (the default), so even in a test build an unset clock is production-identical.</summary>
        public System.Func<float> TestClock
        {
            get => _testClock;
            set
            {
                _testClock = value;
                for (int i = 0; i < _instances.Count; i++) _instances[i].TestClock = value;
            }
        }
        private System.Func<float> _testClock;
#endif

        /// <summary>The scheduling/cadence clock the gates read. <c>Time.time</c> in the shipped IL2CPP build (the
        /// TestClock seam above is compiled out, so this is a plain <c>Time.time</c> read — production
        /// byte-identical); a PlayMode test may override it deterministically (86camdk1h).</summary>
        private float Now
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (_testClock != null) return _testClock();
#endif
                return Time.time;
            }
        }

        // Create a per-tree state, propagating the test clock (86camdk1h) so scatter trees discovered in Start
        // (after a PlayMode test injects TestClock) read the SAME deterministic clock as the demo tree. In the
        // shipped build this is a plain `new ChoppableTreeState(...)` (the propagation strips out).
        private ChoppableTreeState NewState(Transform visual, int seed)
        {
            var s = new ChoppableTreeState(visual, seed);
#if UNITY_INCLUDE_TESTS
            s.TestClock = _testClock;
#endif
            return s;
        }

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
            // REWORK 86caf9u5t — the shared log-pile spawner (serialized editor-time; this scene-search is the
            // build-safety net). Null is tolerated — felling completes but no pile drops (a bare test rig).
            if (logPileSpawner == null) logPileSpawner = FindObjectOfType<LogPileSpawner>();

            // Instance 0 = the demo tree (this component's own visual). It captures its standing pose at spawn
            // (the tree ships standing). Its derived sub-seed is the raw regrowSeed so the demo tree's regrow
            // roll is byte-identical to the pre-CHANGE-(a) single-tree behaviour (existing tests unchanged).
            _instances.Clear();
            _instances.Add(NewState(visual, DeriveSeed(0)));
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
                    _instances.Add(NewState(child, DeriveSeed(idx)));
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
            // lands at the swing's down-stroke, NOT on the click frame). Done BEFORE reading new input so the
            // input read below can immediately begin the NEXT swing of a held chain (HOLD-TO-CHOP) in the same
            // frame the impact resolves — no dead frame (the inter-swing cadence is the chopInterval cooldown, 0.25s).
            if (_impactPending && Now >= _impactAt)
            {
                _impactPending = false;
                ChoppableTreeState t = _pendingTarget;
                _pendingTarget = null;
                ApplyChopEffect(t);
                // STOP-ON-FALL (AC2): if the locked chain target just felled (no longer choppable), drop the chain
                // AND latch suppression so the held button does not auto-acquire a neighbour and swing at it — a
                // RELEASE + re-press is required to start a new chain on the next tree (the v1 default).
                if (_chainTarget != null && !_chainTarget.IsChoppable)
                {
                    _chainTarget = null;
                    _suppressUntilRepress = true;
                    // 86caf9ngh N1 — OPEN the clip-completion gate on fell: clear _swingEndsAt so a re-press on a
                    // NEW tree within the just-felled swing's ~1.6s clip window fires IMMEDIATELY (AC1), instead of
                    // being blocked by the stale gate of the swing that brought THIS tree down. Without this, the
                    // SwingInProgress gate (the hold-repeat cadence lock) leaks into the next chain's first click.
                    _swingEndsAt = float.NegativeInfinity;
                }
            }

            if (inventory == null || player == null)
            {
                _heldLastFrame = false; _chainTarget = null; _suppressUntilRepress = false; return;
            }

            // HOLD-TO-CHOP (86caf7a0p) — read the LMB HELD level (real button OR the programmatic seam), and detect
            // the rising edge (a fresh PRESS) off the previous frame's held state. The real GetMouseButtonDown(0)
            // edge also counts as a press (covers the live mouse). A fresh press re-resolves the nearest target +
            // starts a NEW chain; a held continuation stays locked on _chainTarget. The one-shot RequestChopClick
            // latch is still honoured as a single press+release (back-compat — one click = exactly one swing).
            bool held = _chopHeldRequested || Input.GetMouseButton(0);
            bool clickLatch = _chopClickRequested; // a single programmatic strike (consumed below)
            _chopClickRequested = false;
            bool pressEdge = (held && !_heldLastFrame) || Input.GetMouseButtonDown(0) || clickLatch;
            _heldLastFrame = held;
            _freshPress = pressEdge;

            // A one-shot RequestChopClick (no sustained hold) behaves like a single press: it must produce exactly
            // ONE swing, never a chain. Treat the latch as "held for this one swing only" so the chain logic below
            // begins one swing then finds nothing held next frame and stops.
            bool effectiveHeld = held || clickLatch;

            // Refinement 3 — SINGLE-FLIGHT: while a swing's impact is still pending, no new swing begins (one swing
            // = one impact = one chop effect). The held chain resumes on the frame AFTER the impact resolves above.
            if (_impactPending) return;

            // Nothing held + no fresh press → no chain. Drop any lingering lock (the in-flight swing already
            // resolved above) and bail. This is the RELEASE path (AC1): releasing stops the repeat; the swing that
            // was mid-flight has already committed its single impact via the impact-resolve block above. Releasing
            // also CLEARS the STOP-ON-FALL suppression so the NEXT press chains anew (re-press for the next tree).
            // This runs BEFORE the clip-completion gate below so a RELEASE always drops the lock immediately — even
            // if the swing animation is still playing out (the in-flight swing's committed impact already landed).
            // 86caf9ngh N1 — also OPEN the clip-completion gate on RELEASE (clear _swingEndsAt), so a fresh press on
            // a new tree within the released swing's clip window fires immediately (AC1) rather than waiting out the
            // previous swing's SwingInProgress lock. (The in-flight swing already committed its impact above.)
            if (!effectiveHeld) { _chainTarget = null; _suppressUntilRepress = false; _swingEndsAt = float.NegativeInfinity; return; }

            // STOP-ON-FALL suppression (AC2): the locked target felled while the button stayed held → do not start
            // a NEW chain on a neighbour until the player releases + re-presses. A fresh press clears the latch
            // (it IS the re-press); a held continuation stays suppressed (no auto-acquire / no empty swings).
            if (_suppressUntilRepress)
            {
                if (_freshPress) _suppressUntilRepress = false; // the re-press — allow a new chain to start
                else return;                                    // still the same hold — stay stopped
            }

            // 86caf7a0p RE-ITER — CLIP-COMPLETION CADENCE GATE (the Sponsor soak-reject fix: "the animation is not
            // allowed to finish and the tree goes down too fast because 1 hit is not = on finished animation").
            // The impact lands MID-clip (~swingImpactDelaySeconds), but the swing CLIP keeps playing to completion;
            // the NEXT swing must not begin until that clip has FINISHED — else TriggerChop re-fires the Attack
            // state mid-clip (AnyState→Attack canTransitionToSelf), cutting the animation off and over-pacing the
            // chops. So while the current swing's clip is still playing AND the button is still held, no new swing
            // begins this frame (the chain stays LOCKED — _chainTarget is untouched — and simply waits). The repeat
            // cadence is therefore one COMPLETED swing per chop (≥ clip length ÷ tool-use speed), not the shorter
            // impact delay. Placed AFTER the release/suppress checks so a release still drops the lock immediately;
            // a single click is unaffected (it arrives with the gate open — _swingEndsAt = -inf — and fires now).
            if (SwingInProgress) return;

            // The full chop decision (pure static so the guard truth-table is unit-testable): the axe-SELECTED gate
            // (load-bearing) + the three world-click guards (modal panel open; pointer over the inventory/belt UI;
            // RMB camera-orbit drag — a HELD RMB orbit drag must NOT be read as hold-to-chop). Re-evaluated every
            // swing so opening a panel / dragging the camera mid-hold cleanly pauses the chain.
            bool overUI = inventoryUI != null && inventoryUI.IsPointerOverUI(Input.mousePosition);
            bool rmbHeld = Input.GetMouseButton(1);

            // Resolve the chain TARGET. On a fresh press, re-resolve the NEAREST in-range standing tree (a new
            // chain on a new tree — AC: "a fresh press on a new tree starts a new chain"). On a held continuation,
            // STAY LOCKED on _chainTarget (AC2: do not re-acquire a different tree mid-hold) — but only if it is
            // still choppable AND still in range (the player may have walked off); otherwise the chain is over.
            ChoppableTreeState target;
            if (_freshPress || _chainTarget == null)
                target = ResolveNearestChoppable(player.position);
            else
                target = (_chainTarget.IsChoppable && IsInRange(player.position, _chainTarget))
                         ? _chainTarget : null;
            bool inRange = target != null;

            if (!ShouldChopOnClick(inRange, inventory.IsAnyAxeSelectedInBelt,
                                   UiInputGate.CaptureWorldInput, overUI, rmbHeld))
            {
                _chainTarget = null; // a failed gate (panel open / over UI / RMB / out of range) pauses the chain
                return;
            }

            // Inter-swing cooldown: the next swing in a chain begins only after chopInterval since the last landed
            // chop. The default 0.25s inserts a small rhythmic gap between swings (the "rhythmic chopping, NOT
            // machine-gun" cadence bar); 0 would be back-to-back, driven purely by the swing/impact clip. It also
            // still guards a stray double-edge from out-pacing the swing read. (Sponsor-tunable per the DEFAULTS block.)
            if (Now - _lastChopAt < Mathf.Max(0f, chopInterval)) return;
            _lastChopAt = Now;

            // LOCK the target for the chain + fire the SWING + FACE-TURN NOW, scheduling the EFFECT for the swing's
            // IMPACT frame (refinement 3). Next frame, with the button still held + the impact resolved, the chain
            // continues — one swing per impact, never an input-poll-rate firehose (AC3 invariant preserved).
            _chainTarget = target;
            BeginChopSwing(target);
        }

        /// <summary>Planar (XZ) range check between a position and a tracked tree — the same metric the resolver
        /// uses, factored out so the HOLD-TO-CHOP chain can re-test the LOCKED target's range each swing (the
        /// player may walk away mid-hold). Mirrors <see cref="ResolveNearestChoppable"/>'s distance test.</summary>
        private bool IsInRange(Vector3 from, ChoppableTreeState t)
        {
            if (t == null) return false;
            Vector3 p = t.Position;
            float dSq = (new Vector2(from.x, from.z) - new Vector2(p.x, p.z)).sqrMagnitude;
            return dSq <= chopRadius * chopRadius;
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
        /// ARBITRATION (round-4, 86caffwv5) — true when THIS chop verb OWNS the current left-click: an axe (ANY
        /// tier — wood/stone/iron) is the selected belt item AND a standing tree is within chop range of the
        /// player. <see cref="FarHorizon.Combat.MeleeAttack"/> queries this (a STATELESS recompute — no shared
        /// mutable flag, so it is execution-order-independent between the two Updates) to SUPPRESS its whiff swing
        /// when the chop claims the click (the verb-wins-over-whiff rule). Deliberately does NOT re-apply the
        /// world-click guards (panel/UI/RMB) — MeleeAttack applies the SAME guards in its own
        /// <see cref="FarHorizon.Combat.MeleeAttack.ShouldSwingOnClick"/>, so a guarded click fires NEITHER the chop
        /// nor the whiff. Also independent of the hold-chain cadence state (SwingInProgress / cooldown / pending
        /// impact): while the player is positioned to chop, the chop owns the click even between swings — a whiff
        /// mid-chop would be wrong. Null inventory/player → false (a bare rig never claims).
        /// </summary>
        public bool WouldClaimClick()
        {
            if (inventory == null || player == null) return false;
            return inventory.IsAnyAxeSelectedInBelt && ResolveNearestChoppable(player.position) != null;
        }

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
        /// HOLD-TO-CHOP (86caf7a0p) — set the LMB-HELD state programmatically (the input-independent analog of
        /// holding/releasing the left mouse button). The live Update OR's this with Input.GetMouseButton(0), so a
        /// headless PlayMode run + the shipped-build capture exercise the SAME hold-repeat chain a real held mouse
        /// button drives. Call SetChopHeld(true) to begin holding (the first frame is a fresh press → starts a new
        /// chain on the nearest in-range tree); keep it true to repeat swings (one swing per impact); call
        /// SetChopHeld(false) to release (the chain stops; an in-flight swing still lands its committed impact).
        /// The nearest-in-range + axe-selected + over-UI/RMB guards still apply every swing.
        /// </summary>
        public void SetChopHeld(bool held) => _chopHeldRequested = held;

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
            _impactAt = Now + delay;
            _impactPending = true;

            // 86caf7a0p RE-ITER — mark when this swing's CLIP FINISHES (the repeat-cadence gate). The next held
            // swing cannot begin until Time.time >= this, so the swing animation plays to COMPLETION before the
            // next one starts — ONE completed swing = ONE chop. ComputeSwingDuration already speed-scales the clip
            // length (live MeleeClipLength preferred; serialized fallback), so this rides tool-use speed exactly
            // like the impact delay. Clamped ≥ the impact delay so the clip never "finishes" before its own impact.
            _swingEndsAt = Now + Mathf.Max(delay, ComputeSwingDuration());
        }

        /// <summary>
        /// 86caf7a0p RE-ITER — the effective swing CLIP duration (seconds) at the current tool-use speed: the LIVE
        /// authored clip length (<see cref="CastawayCharacter.MeleeClipLength"/>, read from the actual imported
        /// Mixamo melee clip) when available, else the serialized <see cref="swingClipLengthSeconds"/> fallback (a
        /// bare headless rig with no Animator), divided by the clamped chopSpeed. This is the minimum spacing
        /// between two COMPLETED swings of a hold chain — the cadence ties to the ACTUAL clip, not a magic number.
        /// </summary>
        private float ComputeSwingDuration()
        {
            float speed = character != null
                ? Mathf.Clamp(character.chopSpeed, CastawayCharacter.ChopSpeedMin, CastawayCharacter.ChopSpeedMax)
                : 1f;
            float liveLen = character != null ? character.MeleeClipLength : 0f;
            // 86caf9ngh N2 — FLOOR the serialized fallback strictly > 0 (SwingClipLengthFloor), not just ≥ 0. A
            // misconfigured Animator-less bare test rig with swingClipLengthSeconds = 0 (or negative) would
            // otherwise yield a 0-length swing duration → the clip-completion cadence gate (SwingInProgress) would
            // never engage on the hold path, machine-gunning swings. The live build is unaffected (it reads the
            // real MeleeClipLength); this guards only the degenerate fallback.
            float authored = liveLen > 0f ? liveLen : Mathf.Max(SwingClipLengthFloor, swingClipLengthSeconds);
            return authored / Mathf.Max(0.0001f, speed);
        }

        // Refinement 3 — APPLY the chop effect at IMPACT: advance the count + shake the tree (AC6), and fell it on
        // the final chop (AC3 — fade-out then regrow + AC2 — drop a lootable LOG PILE). Re-checks IsChoppable (the
        // target could have changed state in the delay window — e.g. an external regrow tick) so a stale pending
        // impact can't double-fell. This is the moment the tree's bring-down/fall + the fade-out scheduling happen
        // — synced to the visual hit.
        //
        // REWORK 86caf9u5t — NO per-chop wood (AC1): the per-swing inventory.AddWood is GONE. The whole tree's
        // wood is awarded ONCE, on the FELLING chop, as a lootable LogPile (AC2) holding logPileSpawner.WoodYield
        // logs — the player loots it with E. A non-felling chop only advances the count + shakes the tree (AC6).
        private void ApplyChopEffect(ChoppableTreeState target)
        {
            if (target == null || !target.IsChoppable) return;

            bool felled = target.LandChop(chopsToFell, regrowthMinSeconds, regrowthMaxSeconds, fadeOutDelaySeconds);

            // AC6 — per-chop SHAKE/RECOIL: a non-felling chop gives the tree a brief tip-impulse (a cheap
            // transform nudge, no new art/audio). The felling chop runs the full sink+tip fell tween instead, so
            // it does NOT also shake (the fall IS the feedback).
            if (!felled) target.Shake(chopShakeDegrees);

            // AC2 — on FELL, drop a lootable LOG PILE at the tree's spot holding the WHOLE tree's wood (the
            // `tree-chop wood yield` setting). The pile uses the felled tree's own trunk material (the ~1-draw-call
            // shared opaque path) when the spawner has no override. Null spawner/inventory → felling still
            // completes (the tree falls + fades), just without a pile (a bare test rig / unwired safety).
            if (felled && logPileSpawner != null && inventory != null)
            {
                Material trunkMat = target.FirstSharedMaterial();
                LogPile pile = logPileSpawner.SpawnAt(target.Position, inventory, trunkMat);
                ChopTrace("FELL -> spawned log pile holding " + (pile != null ? pile.LogsRemaining : 0) +
                          " logs at " + target.Position.ToString("F1"));
            }

            if (!_tracedFirstChop)
            {
                _tracedFirstChop = true;
                ChopTrace("impact " + target.Chops + "/" + chopsToFell + " (no per-chop wood — wood drops on fell)" +
                          " (swing=" + (character != null) + ")");
            }
            if (felled)
                ChopTrace("tree FELLED after " + target.Chops + " chops; regrow in " +
                          (target.RegrowAt - Now).ToString("F0") + "s");
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
    /// rests for <paramref name="fadeOutDelaySeconds"/> (default 2s, a NAMED tweakable field on ChopTree, LIVE-
    /// dialable via the `fallen-tree fade-out` setting — 86caff4ad), then SCALES
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

        // AC6 — per-chop SHAKE/RECOIL: a brief tip-impulse on each non-felling chop, eased back to standing. A
        // transient rotation overlay (does NOT touch _standRot, so the fell tween still tips from the true
        // standing pose). _shaking is true while the impulse plays; _shakeT is its progress; _shakeDeg its peak.
        private bool _shaking;
        private float _shakeT;
        private float _shakeDeg;

        // 86camdk1h — this Tick's tween STEP delta (set at the top of Tick). Time.unscaledDeltaTime in the shipped
        // build (production unchanged — the game never scales Time.timeScale, so unscaled == scaled); the injected
        // test clock's per-frame advance under a PlayMode test, so the fell/fade/regrow tweens complete in a
        // DETERMINISTIC fixed-step frame count instead of riding the coarse (un-honored-captureDeltaTime) headless
        // wall-clock. The tween Step* methods read this field, never Time.unscaledDeltaTime directly.
        private float _dt;
#if UNITY_INCLUDE_TESTS
        /// <summary>86camdk1h — TEST-ONLY deterministic clock (STRIPPED from ship builds via UNITY_INCLUDE_TESTS),
        /// set by the owning <see cref="ChopTree"/> to its injected fake clock so a PlayMode test's fixed-step
        /// clock drives this tree's fell/fade/regrow SCHEDULING deterministically (headless <c>-batchmode</c> does
        /// NOT honor <c>Time.captureDeltaTime</c>). Null → <c>Time.time</c> (production-identical).</summary>
        public System.Func<float> TestClock;
        private float _prevNow;      // previous Tick's clock sample (to derive the fixed-step tween delta)
        private bool _prevNowSet;
#endif

        /// <summary>The SCHEDULING clock this tree's fell/fade/regrow timers read. <c>Time.time</c> in the shipped
        /// build (the TestClock seam is compiled out → a plain <c>Time.time</c> read, production byte-identical);
        /// a test clock when the owning ChopTree injects one (86camdk1h). Tween STEPPING rides <see cref="_dt"/>.</summary>
        private float Now
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (TestClock != null) return TestClock();
#endif
                return Time.time;
            }
        }

        private const float FellDuration = 0.5f;
        private const float FadeOutDuration = 0.8f;   // the scale-down-to-nothing tween length
        private const float RegrowRiseDuration = 0.6f;
        private const float ShakeDuration = 0.22f;    // the per-chop recoil-and-settle length (quick + felt)
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
            // 86camdk1h — this frame's tween step delta. Time.unscaledDeltaTime in the shipped build (production
            // unchanged — the game never scales Time.timeScale, so unscaled == scaled); the injected test clock's
            // per-frame advance under a PlayMode test, so the tweens complete in a DETERMINISTIC frame count
            // headlessly. Computed EVERY Tick (even standing) so a tween that starts later has a consistent step
            // (no accumulated gap from Ticks that ran no Step*).
            _dt = Time.unscaledDeltaTime;
#if UNITY_INCLUDE_TESTS
            if (TestClock != null)
            {
                float now = TestClock();
                _dt = _prevNowSet ? now - _prevNow : 0f;
                _prevNow = now;
                _prevNowSet = true;
            }
#endif

            if (_felling) { StepFelling(); return; }
            if (_fadingOut) { StepFadeOut(); return; }
            if (_regrowing) { StepRegrow(); return; }
            if (_removed)
            {
                // Ground is empty (faded out). Regrow at _regrowAt → re-enable renderers + scale back up.
                if (Now >= _regrowAt) BeginRegrow();
                return;
            }
            // Felled + resting (post-fell tween, pre-fade): begin the fade-out once the delay elapses. (A regrow
            // scheduled SOONER than the fade — e.g. a fast-test regrow window — short-circuits straight to regrow
            // so the tree never gets stuck mid-cycle; the normal ~10s fade ≪ ~10min regrow.)
            if (_felled)
            {
                if (Now >= _regrowAt) { BeginRegrow(); return; }
                if (Now >= _fadeAt) BeginFadeOut();
                return;
            }
            // AC6 — a STANDING tree's per-chop shake/recoil (a brief tip-impulse that eases back). Only runs while
            // standing (the fell/fade/regrow tweens own the transform when they're active).
            if (_shaking) StepShake();
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
                _shaking = false; // the fell tween owns the transform now; cancel any in-flight shake
                BeginFelling();
                _fadeAt = Now + Mathf.Max(0f, fadeOutDelaySeconds);
                ScheduleRegrow(regrowthMinSeconds, regrowthMaxSeconds);
                return true;
            }
            return false;
        }

        /// <summary>True while a per-chop shake/recoil impulse (AC6) is playing on this STANDING tree. Exposed so
        /// a PlayMode test can assert a non-felling chop triggers the shake (and the felling chop does not — the
        /// fall is the feedback there).</summary>
        public bool Shaking => _shaking;

        /// <summary>
        /// AC6 — kick off a brief SHAKE/RECOIL impulse (a cheap transform nudge, no new art/audio): the tree tips
        /// <paramref name="degrees"/> on the chop impact, then eases back to standing over <see cref="ShakeDuration"/>.
        /// A no-op when the tree isn't standing (a felling/fading tree owns its transform), when degrees ≤ 0, or
        /// when there is no visual. Restarts the impulse if a chop lands mid-shake (the latest hit re-kicks it).
        /// Called by ChopTree.ApplyChopEffect on each NON-felling chop (the felling chop runs the fell tween instead).
        /// </summary>
        public void Shake(float degrees)
        {
            if (!IsChoppable || _visual == null || degrees <= 0f) return;
            _shakeDeg = degrees;
            _shakeT = 0f;
            _shaking = true;
        }

        // One frame of the per-chop recoil — a half-sine tip-and-settle: 0 → peak → 0 over ShakeDuration, applied
        // as a transient X-tip OVERLAY on the standing rotation (so it never drifts the true standing pose). On
        // completion the visual is restored exactly to standing.
        private void StepShake()
        {
            if (_visual == null) { _shaking = false; return; }
            _shakeT += _dt;
            float k = Mathf.Clamp01(_shakeT / ShakeDuration);
            // Half-sine: rises to the peak at k=0.5, back to 0 at k=1 — a clean recoil-and-settle.
            float tip = Mathf.Sin(k * Mathf.PI) * _shakeDeg;
            _visual.rotation = _standRot * Quaternion.Euler(tip, 0f, 0f);
            if (k >= 1f)
            {
                _shaking = false;
                _visual.rotation = _standRot; // settle exactly back to standing
            }
        }

        /// <summary>The first shared material on this tree's visual (its trunk MeshRenderer) — handed to the
        /// spawned LogPile so the logs read as the SAME wood on the SAME ~1-draw-call opaque batch path. Null if
        /// the tree has no renderer (a bare test rig) → the spawner falls back to a built bark material.</summary>
        public Material FirstSharedMaterial()
        {
            if (_renderers == null) return null;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && _renderers[i].sharedMaterial != null)
                    return _renderers[i].sharedMaterial;
            return null;
        }

        // Schedule the regrow at a RANDOM time within [min,max] (AC3 — organic, not uniform). Min clamped
        // non-negative; max clamped >= min so a mis-authored max never schedules a regrow in the past.
        private void ScheduleRegrow(float regrowthMinSeconds, float regrowthMaxSeconds)
        {
            float min = Mathf.Max(0f, regrowthMinSeconds);
            float max = Mathf.Max(min, regrowthMaxSeconds);
            float delay = min + (float)_rng.NextDouble() * (max - min);
            _regrowAt = Now + delay;
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
            _tweenT += _dt;
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
            _tweenT += _dt;
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
            _tweenT += _dt;
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
