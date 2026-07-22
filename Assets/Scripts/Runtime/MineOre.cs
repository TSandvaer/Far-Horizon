using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The mine-an-ore-node mechanic + the player mining SWING (ticket 86cakkmr0 / I-2 of the iron chain).
    /// The peaceful "earn iron through WORK" beat: iron-ore rock nodes the castaway reaches, then — WITH A
    /// PICKAXE SELECTED in the belt — mines by LEFT-CLICKING (exactly like chopping a tree, the sibling verb);
    /// each click SWINGS the arm via the same Mixamo melee Attack animation (CastawayCharacter.TriggerChop) +
    /// advances the node's strike count; after <see cref="strikesToBreak"/> strikes the node BREAKS and DROPS
    /// a lootable iron-ORE PILE (the OrePile sibling of the tree's LogPile), which the player loots with E, then
    /// the node depletes → REGROWS after a tweakable random delay. Mining reads as WORK, not a proximity-auto
    /// pickup ([[active-input-not-proximity-auto-for-actions]]).
    ///
    /// === Deliberate MIRROR of ChopTree (spec I-2 "mirror the chop-verb pattern; do NOT invent a new route") ===
    /// This is a faithful sibling of <see cref="ChopTree"/> — the SAME active-left-click trigger, the SAME pure
    /// <see cref="ShouldMineOnClick"/> guard truth-table + three world-click guards, the SAME impact-delayed
    /// single-flight strike, the SAME hold-to-repeat clip-completion cadence, the SAME nearest-in-range resolver,
    /// and the SAME editor-time-authored + runtime-discovered instances model. It reuses the EXISTING swing
    /// (CastawayCharacter.TriggerChop — the pickaxe swings on the shared Attack state; per-weapon distinct swings
    /// are a separate Sponsor-gated ticket 86caffwv5), the EXISTING E-loot surface (PickableLooter/IPickable via
    /// OrePile), and the EXISTING SettingsCatalog tweakable pattern. It does NOT refactor any chop internals.
    ///
    /// === The AXE-SELECTED analog — the PICKAXE-SELECTED gate (load-bearing) ===
    /// The mine requires a PICKAXE (stone OR iron tier) to be the SELECTED belt item — NOT merely owned — AND the
    /// player in range of SOME node. The mining analog of <see cref="Inventory.IsAxeSelectedInBelt"/> is
    /// <see cref="IsPickaxeSelected"/> (checks the two pickaxe ids against the selected belt slot). The FOLDED
    /// review NIT (Tess PR #268, NIT 2) is the explicit NEGATIVE: pickaxe selected + in range + NO click ⇒ NO
    /// strike/no ore — locking out the #224 proximity-auto regression class.
    ///
    /// === Ore-rarity LIVE dial (spec I-2 — the I-0 extension hook goes live here) ===
    /// The nodes are an editor-time-authored POOL (BuildOreNodes → serialized into Boot.unity, NOT Awake — the
    /// legs-up class). <see cref="ActiveNodeCount"/> (bound LIVE to the `iron_ore_rarity` setting via
    /// SettingsCatalog.PopulateIronLive) gates how many pool nodes are ENABLED (visible + mineable) — the ore-
    /// RARITY difficulty dial (easy common / hard sparse). Changing it enables/disables pool nodes live (no
    /// runtime GameObject creation, so no legs-up; no dead knob). Clamped to the authored pool size.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The manager GameObject + this component + its Inventory/player/character/inventoryUI/orePileSpawner/
    /// nodeRoot references are authored editor-time into Boot.unity, NOT at Awake (the legs-up class). MineSceneTests
    /// guards the scene presence + that the refs serialize.
    ///
    /// === Trace instrumentation (no-new-mob/verb-without-trace discipline) ===
    /// `[mine-trace]` lines on strike / break / regrow, [Conditional("UNITY_EDITOR")] so they strip from the
    /// shipped IL2CPP exe (unity6-mastery §5/§10 — no Debug.Log in the shipped hot path).
    ///
    /// NO MUTABLE STATICS (instance state only) — needs no [RuntimeInitializeOnLoadMethod] reset (StaticStateResetTests).
    /// </summary>
    public class MineOre : MonoBehaviour
    {
        /// <summary>The NAMED default strikes-to-break a node. The `iron ore strikes` extension is OOS for I-2;
        /// default 4 (mirrors ChopTree.ChopsToFellDefault band), clamped within
        /// [<see cref="StrikesToBreakMin"/>, <see cref="StrikesToBreakMax"/>]. default 4 — Sponsor-soak tunes.</summary>
        public const int StrikesToBreakDefault = 4;

        /// <summary>Range floor — strikes-to-break clamps within [1, 10] (mirrors ChopTree's chops-to-fell band).</summary>
        public const int StrikesToBreakMin = 1;

        /// <summary>Range ceiling — strikes-to-break clamps within [1, 10].</summary>
        public const int StrikesToBreakMax = 10;

        /// <summary>The GameObject name BuildOreNodes gives every authored ore-node visual. The runtime resolver
        /// collects these under <see cref="nodeRoot"/> so every pool node is mineable. A READ-only key.</summary>
        public const string OreNodeName = "OreNode";

        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger mined ore is dropped toward (the OrePile loots iron_ore into it). Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity (with a pickaxe SELECTED) triggers a mine-strike. Wired at bootstrap.")]
        public Transform player;

        [Tooltip("The root whose OreNode children are the authored ore-node pool. Discovered at Start and made " +
                 "mineable; ActiveNodeCount enables the first N (the ore-rarity dial). Wired editor-time; a name-scan " +
                 "fallback finds it if unwired. Null is tolerated (no nodes — a bare test rig).")]
        public Transform nodeRoot;

        [Tooltip("The castaway whose TriggerChop() plays the melee SWING on each landed strike (the SAME swing the " +
                 "chop verb uses — per-weapon distinct swings are a separate ticket 86caffwv5). Wired at bootstrap; " +
                 "null is graceful (the strike still lands, just without the arm swing).")]
        public CastawayCharacter character;

        [Tooltip("The inventory/belt UI — a left-click OVER this UI must NOT mine the node behind it (the mining " +
                 "analog of the chop over-UI guard). Wired at bootstrap; null is tolerated (guard skipped).")]
        public InventoryUI inventoryUI;

        [Tooltip("The shared OrePileSpawner that mints a lootable OrePile when a node breaks (holding the node's " +
                 "ore yield). Wired at bootstrap; null is tolerated (breaking still completes but no pile drops — a bare rig).")]
        public OrePileSpawner orePileSpawner;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' a node and (with a selected pickaxe) mines " +
                 "it. Mirrors ChopTree.chopRadius.")]
        public float mineRadius = 2.2f;

        [Tooltip("Strikes needed to break a node (default 4, range 1–10). After this many the node breaks + drops " +
                 "its ore pile, then regrows. Shared across every node.")]
        public int strikesToBreak = StrikesToBreakDefault;

        [Tooltip("Per-strike SHAKE/RECOIL amount (degrees of the brief tip-impulse each non-breaking strike gives " +
                 "the node). A cheap transform nudge. Soak-tune. 0 disables the shake.")]
        public float strikeShakeDegrees = 5f;

        [Tooltip("Minimum seconds between landed strikes (a small COOLDOWN so a stray double-edge can't out-pace the " +
                 "swing read). Mirrors ChopTree.chopInterval. 0 = no cooldown.")]
        public float strikeInterval = 0.25f;

        [Header("Regrowth (TWEAKABLE — organic within [min,max], like tree regrowth)")]
        [Tooltip("Minimum seconds before a broken node regrows into a mineable node. Actual regrow is RANDOM in " +
                 "[min,max] (organic). Default ~6 min.")]
        public float regrowthMinSeconds = 360f;   // 6 min

        [Tooltip("Maximum seconds before a broken node regrows (random within [min,max]). Default ~9 min.")]
        public float regrowthMaxSeconds = 540f;    // 9 min

        [Header("Swing impact (sync the strike EFFECT to the swing's down-stroke — mirrors ChopTree)")]
        [Tooltip("Seconds from the strike CLICK to the swing's IMPACT frame — the click fires the swing + face-turn " +
                 "immediately, but the strike EFFECT (progress + break + pile drop) lands at IMPACT so the node " +
                 "doesn't break before the pickaxe has visually connected. Scales with tool-use speed (÷ chopSpeed).")]
        public float swingImpactDelaySeconds = 0.4f;

        [Tooltip("FALLBACK authored length (seconds, at 1x speed) of the swing clip. HOLD-TO-MINE gates the NEXT " +
                 "swing on the CURRENT swing's clip COMPLETING (one completed swing = one strike). The LIVE cadence " +
                 "prefers CastawayCharacter.MeleeClipLength; this is the fallback for a bare headless rig.")]
        public float swingClipLengthSeconds = 1.6f;

        /// <summary>Strictly-positive floor for the <see cref="swingClipLengthSeconds"/> fallback used in
        /// <see cref="ComputeSwingDuration"/> (mirrors ChopTree.SwingClipLengthFloor). A degenerate 0/negative on a
        /// misconfigured bare rig would collapse the cadence gate; this floors it. The live build reads the real
        /// clip length and never touches this floor.</summary>
        public const float SwingClipLengthFloor = 0.05f;

        [Header("Ore-rarity dial (the `iron_ore_rarity` setting flips this LIVE — I-2 flips the I-0 hook)")]
        [Tooltip("How many pool nodes are ENABLED (visible + mineable) — the ore-RARITY difficulty dial (easy " +
                 "common / hard sparse). Default seeds from IronDifficultyPresets.Medium.OreNodeCount (14). Clamped " +
                 "to the authored pool size. Changing it live enables/disables pool nodes (no runtime spawn → no " +
                 "legs-up; no dead knob). default from the Medium preset — Sponsor-soak tunes.")]
        public int activeNodeCount = -1; // -1 = seed from the Medium preset in Awake (so a bare test can override first)

        [Tooltip("Deterministic seed for the regrow-time rolls (so headless tests are reproducible). 0 = a " +
                 "time-based seed at runtime. Each node derives its own sub-seed. Mirrors ChopTree.regrowSeed.")]
        public int regrowSeed = 0;

        // The per-node mineable instances (the whole authored pool; ActiveNodeCount gates which are enabled).
        private readonly List<MineableNodeState> _nodes = new List<MineableNodeState>();

        // Programmatic LEFT-CLICK latch (the input-independent seam, the analog of ChopTree.RequestChopClick).
        private bool _mineClickRequested;
        private float _lastStrikeAt = float.NegativeInfinity;

        // HOLD-TO-MINE — programmatic LMB-HELD state (the analog of ChopTree.SetChopHeld). OR'd with the real mouse.
        private bool _mineHeldRequested;

        // Impact timing — a single in-flight PENDING IMPACT (single-flight: a 2nd click while pending is ignored).
        private bool _impactPending;
        private float _impactAt;
        private MineableNodeState _pendingTarget;

        // HOLD cadence — the wall-clock time the CURRENT swing's clip finishes (the next held swing waits for it).
        private float _swingEndsAt = float.NegativeInfinity;

        // HOLD — the LOCKED chain target + the press/held-edge bookkeeping (mirrors ChopTree exactly).
        private MineableNodeState _chainTarget;
        private bool _freshPress;
        private bool _heldLastFrame;
        private bool _suppressUntilRepress;

        private bool _tracedFirstStrike;
        private bool _startedPool; // Start() has discovered + applied the pool

        /// <summary>True while a strike swing has been triggered but its EFFECT has not yet landed at impact.</summary>
        public bool ImpactPending => _impactPending;

        /// <summary>True while the CURRENT swing's clip is still playing (a held chain waits for it before the next swing).</summary>
        public bool SwingInProgress => Now < _swingEndsAt;

        /// <summary>The effective per-swing clip DURATION for the current tool-use speed (live clip length else the
        /// serialized fallback, ÷ chopSpeed) — the minimum spacing between two completed hold swings.</summary>
        public float EffectiveSwingDurationSeconds => ComputeSwingDuration();

        /// <summary>True while a swing CHAIN is active (LMB held + a node locked as the chain target).</summary>
        public bool IsMineChainActive => _chainTarget != null;

        /// <summary>Number of mineable node instances tracked (the whole authored pool — enabled + disabled).</summary>
        public int NodeCount => _nodes.Count;

        /// <summary>How many pool nodes are currently ENABLED (visible + mineable) — the live ore-rarity readout.</summary>
        public int ActiveNodeCount => activeNodeCount;

        /// <summary>Strikes landed on a given tracked node (0..strikesToBreak). For per-node tests.</summary>
        public int StrikesOn(int index) => index >= 0 && index < _nodes.Count ? _nodes[index].Strikes : 0;

        /// <summary>True while a given tracked node is BROKEN (depleted, awaiting regrow). For per-node tests.</summary>
        public bool IsBrokenOn(int index) => index >= 0 && index < _nodes.Count && _nodes[index].Broken;

        /// <summary>True while a given tracked node is ENABLED (active in the pool per the rarity dial).</summary>
        public bool IsNodeEnabled(int index) => index >= 0 && index < _nodes.Count && _nodes[index].Enabled;

#if UNITY_INCLUDE_TESTS
        /// <summary>86camf3xe — TEST-ONLY deterministic clock (public seam, STRIPPED from ship builds via
        /// UNITY_INCLUDE_TESTS; the project has no InternalsVisibleTo, so the "public for tests" seam convention
        /// applies — mirrors ChopTree.TestClock, PR #288 / 86camdk1h). A PlayMode test injects this fake clock and
        /// advances it a fixed step per frame — a WORKING captureDeltaTime — making the <c>Time.time</c>-based
        /// cadence gates (impact / clip-completion / cooldown) AND each node's break/fade/regrow SCHEDULING
        /// deterministic and clock-INDEPENDENT while keeping the SAME shipped gate logic LIVE (a real machine-gun /
        /// double-apply / too-fast-regrow regression still reds the tests). Setting it propagates to every
        /// already-created <see cref="MineableNodeState"/>; nodes discovered later in Start inherit it via
        /// <see cref="NewNodeState"/>. Null → <c>Time.time</c> (the default), so even in a test build an unset
        /// clock is production-identical.</summary>
        public System.Func<float> TestClock
        {
            get => _testClock;
            set
            {
                _testClock = value;
                for (int i = 0; i < _nodes.Count; i++) _nodes[i].TestClock = value;
            }
        }
        private System.Func<float> _testClock;
#endif

        /// <summary>The scheduling/cadence clock the gates read. <c>Time.time</c> in the shipped IL2CPP build (the
        /// TestClock seam above is compiled out, so this is a plain <c>Time.time</c> read — production
        /// byte-identical); a PlayMode test may override it deterministically (86camf3xe, mirrors ChopTree.Now).</summary>
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

        // Create a per-node state, propagating the test clock (86camf3xe) so nodes discovered in Start (after a
        // PlayMode test injects TestClock in SetUp) read the SAME deterministic clock. In the shipped build this is
        // a plain `new MineableNodeState(...)` (the propagation strips out — production byte-identical).
        private MineableNodeState NewNodeState(Transform visual, int seed)
        {
            var s = new MineableNodeState(visual, seed);
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
            if (character == null) character = FindObjectOfType<CastawayCharacter>();
            if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();
            if (orePileSpawner == null) orePileSpawner = FindObjectOfType<OrePileSpawner>();

            // Seed the active count from the Medium preset unless a test/inspector already set it (>= 0).
            if (activeNodeCount < 0) activeNodeCount = IronDifficultyPresets.Medium.OreNodeCount;
        }

        void Start() => DiscoverAndStartPool();

        // Discover the authored ore-node pool (READ-only — the nodes are editor-authored into Boot.unity). Run in
        // Start (not Awake) so the serialized pool is fully present. Each node is its own MineableNodeState. Split
        // out of Start (mirrors MineBoulder.DiscoverAndStartPool) so an EditMode test can drive the pool synchronously.
        private void DiscoverAndStartPool()
        {
            Transform root = nodeRoot;
            if (root == null)
            {
                var found = GameObject.Find("OreNodes");
                if (found != null) root = found.transform;
            }
            _nodes.Clear();
            if (root != null)
            {
                int idx = 0;
                CollectOreNodes(root, ref idx);
            }
            _startedPool = true;
            ApplyActiveCount(); // enable the first ActiveNodeCount pool nodes; disable the rest (the rarity dial)
            MineTrace("resolver tracking " + _nodes.Count + " ore nodes; active=" + activeNodeCount);
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>86cav8xu8 — TEST-ONLY (STRIPPED via UNITY_INCLUDE_TESTS): run the Start-time pool discovery
        /// synchronously so an EditMode test — where Start never auto-fires — can rig ore nodes for the
        /// resolver-vs-diagnostic equivalence guard. Mirrors MineBoulder.InitializePoolForTest.</summary>
        public void InitializePoolForTest() => DiscoverAndStartPool();
#endif

        // Recursively collect every OreNode under the pool root and register a per-node mine state (READ of the
        // authored hierarchy — no GameObject is created/moved/destroyed here).
        private void CollectOreNodes(Transform root, ref int idx)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == OreNodeName)
                {
                    _nodes.Add(NewNodeState(child, DeriveSeed(idx)));
                    idx++;
                }
                else if (child.childCount > 0)
                {
                    CollectOreNodes(child, ref idx);
                }
            }
        }

        private int DeriveSeed(int index)
        {
            int baseSeed = regrowSeed != 0 ? regrowSeed : 1;
            unchecked
            {
                int mixed = baseSeed ^ ((index + 1) * (int)0x9E3779B1);
                return mixed | 1;
            }
        }

        /// <summary>
        /// Set how many pool nodes are ENABLED (the ore-rarity dial's live setter). Clamps to [1, pool size] (or 0
        /// if the pool is empty). Enables the first N nodes (visible + mineable), disables the rest. LIVE — the
        /// `iron_ore_rarity` setting binds here (SettingsCatalog.PopulateIronLive). No dead knob.
        /// </summary>
        public void SetActiveNodeCount(int count)
        {
            activeNodeCount = count;
            ApplyActiveCount();
        }

        // Enable the first activeNodeCount nodes; disable the rest. Clamps the count to the authored pool size so
        // the dev dial's ceiling (which may exceed the pool) never over-reaches. A disabled node hides its visual
        // and is skipped by the resolver (not mineable).
        private void ApplyActiveCount()
        {
            if (!_startedPool) return; // the pool isn't discovered yet (Start applies it once)
            int pool = _nodes.Count;
            int active = pool == 0 ? 0 : Mathf.Clamp(activeNodeCount, 0, pool);
            activeNodeCount = active; // keep the field honest (clamped to what the pool can actually show)
            for (int i = 0; i < _nodes.Count; i++)
                _nodes[i].SetEnabled(i < active);
        }

        void Update()
        {
            // Tick EVERY node's one-shot break / fade / regrow tweens + timers independently of input.
            for (int i = 0; i < _nodes.Count; i++)
                _nodes[i].Tick();

            // Resolve a PENDING IMPACT when its scheduled time arrives (the strike EFFECT lands at the swing's
            // down-stroke, not on the click frame). Before reading new input so a held chain can begin the next
            // swing the same frame the impact resolves.
            if (_impactPending && Now >= _impactAt)
            {
                _impactPending = false;
                MineableNodeState t = _pendingTarget;
                _pendingTarget = null;
                ApplyStrikeEffect(t);
                // STOP-ON-BREAK: if the locked chain target just broke (no longer mineable), drop the chain AND
                // latch suppression so the held button does not auto-acquire a neighbour — a re-press is required.
                if (_chainTarget != null && !_chainTarget.IsMineable)
                {
                    _chainTarget = null;
                    _suppressUntilRepress = true;
                    _swingEndsAt = float.NegativeInfinity; // OPEN the gate so a re-press on a new node fires now
                }
            }

            if (inventory == null || player == null)
            {
                _heldLastFrame = false; _chainTarget = null; _suppressUntilRepress = false; return;
            }

            // Read the LMB HELD level (real button OR the programmatic seam) + detect the rising edge (a fresh PRESS).
            bool held = _mineHeldRequested || Input.GetMouseButton(0);
            bool clickLatch = _mineClickRequested;
            _mineClickRequested = false;
            bool pressEdge = (held && !_heldLastFrame) || Input.GetMouseButtonDown(0) || clickLatch;
            _heldLastFrame = held;
            _freshPress = pressEdge;

            bool effectiveHeld = held || clickLatch;

            // SINGLE-FLIGHT: while a swing's impact is pending, no new swing begins.
            if (_impactPending) return;

            // RELEASE path: nothing held + no fresh press → drop the lock + open the cadence gate + clear suppression.
            if (!effectiveHeld) { _chainTarget = null; _suppressUntilRepress = false; _swingEndsAt = float.NegativeInfinity; return; }

            // STOP-ON-BREAK suppression: the locked target broke while held → require a re-press before a new chain.
            if (_suppressUntilRepress)
            {
                if (_freshPress) _suppressUntilRepress = false;
                else return;
            }

            // CLIP-COMPLETION CADENCE GATE: while the current swing's clip is still playing + held, no new swing
            // begins (one completed swing = one strike; no mid-clip restart over-pacing the strikes).
            if (SwingInProgress) return;

            // The full mine decision (pure static so the guard truth-table is unit-testable): the pickaxe-SELECTED
            // gate + the three world-click guards (modal panel open; pointer over inventory/belt UI; RMB orbit drag).
            bool overUI = inventoryUI != null && inventoryUI.IsPointerOverUI(Input.mousePosition);
            bool rmbHeld = Input.GetMouseButton(1);
            bool pickaxeSelected = IsPickaxeSelected(inventory);

            // Resolve the chain TARGET (fresh press → nearest in-range; held continuation → stay locked if still valid).
            MineableNodeState target;
            if (_freshPress || _chainTarget == null)
                target = ResolveNearestMineable(player.position);
            else
                target = (_chainTarget.IsMineable && IsInRange(player.position, _chainTarget)) ? _chainTarget : null;
            bool inRange = target != null;

            if (!ShouldMineOnClick(inRange, pickaxeSelected, UiInputGate.CaptureWorldInput, overUI, rmbHeld))
            {
                _chainTarget = null;
                return;
            }

            // Inter-swing cooldown.
            if (Now - _lastStrikeAt < Mathf.Max(0f, strikeInterval)) return;
            _lastStrikeAt = Now;

            _chainTarget = target;
            BeginMineSwing(target);
        }

        /// <summary>True when a PICKAXE (stone OR iron tier) is the SELECTED belt item — the mining analog of
        /// Inventory.IsAxeSelectedInBelt. Not merely owned: the selected belt slot must hold a pickaxe id.</summary>
        public static bool IsPickaxeSelected(Inventory inv)
        {
            if (inv == null) return false;
            var model = inv.Model;
            if (model == null) return false;
            return model.IsSelectedBeltItem(ItemCatalog.PickaxeStoneId)
                || model.IsSelectedBeltItem(ItemCatalog.PickaxeIronId);
        }

        /// <summary>
        /// ARBITRATION (round-4, 86caffwv5) — true when THIS ore-mine verb OWNS the current left-click: a stone/iron
        /// pickaxe is the selected belt item (a WOOD pickaxe does NOT mine ORE — spec §5, so it is intentionally
        /// excluded here, matching <see cref="IsPickaxeSelected"/>) AND a mineable ore node is within range. The
        /// chop-sibling of <see cref="ChopTree.WouldClaimClick"/>; <see cref="FarHorizon.Combat.MeleeAttack"/> queries
        /// it (a stateless, order-independent recompute) to SUPPRESS its whiff swing when ore-mining claims the click.
        /// Guards (panel/UI/RMB) are applied by MeleeAttack's own gate. Null inventory/player → false.
        /// </summary>
        public bool WouldClaimClick()
        {
            if (inventory == null || player == null) return false;
            return IsPickaxeSelected(inventory) && ResolveNearestMineable(player.position) != null;
        }

        /// <summary>86caffwv5 diagnostic (PR #327 — the ClickGateDiagnostic instrument, read-only, NOT a fix): this
        /// verb's left-click gate ground truth — the pickaxe-selected gate (STONE/IRON only — a wood pickaxe does NOT
        /// mine ore) + the planar-XZ distance to the NEAREST mineable ore node (ignoring range) vs
        /// <see cref="mineRadius"/>. Uses this verb's OWN state. Null inventory/player → tool unselected + no target.</summary>
        public VerbGateDiag ClickGateDiag()
        {
            var d = new VerbGateDiag { Range = mineRadius, NearestDist = -1f };
            d.ToolSelected = IsPickaxeSelected(inventory);
            if (player != null) d.NearestDist = NearestMineableDistance(player.position);
            return d;
        }

        // Planar (XZ) distance to the nearest mineable ENABLED ore node, ignoring mineRadius (so the diagnostic shows
        // "in range" vs "just out of reach"). -1 if none mineable. A read-only cold-path scan.
        private float NearestMineableDistance(Vector3 from)
        {
            float best = -1f;
            Vector2 here = new Vector2(from.x, from.z);
            for (int i = 0; i < _nodes.Count; i++)
            {
                MineableNodeState s = _nodes[i];
                if (!s.IsMineable) continue;
                Vector3 p = s.Position;
                float dist = (here - new Vector2(p.x, p.z)).magnitude;
                if (best < 0f || dist < best) best = dist;
            }
            return best;
        }

        /// <summary>Planar (XZ) range check between a position and a tracked node (mirrors ResolveNearestMineable).</summary>
        private bool IsInRange(Vector3 from, MineableNodeState n)
        {
            if (n == null) return false;
            Vector3 p = n.Position;
            float dSq = (new Vector2(from.x, from.z) - new Vector2(p.x, p.z)).sqrMagnitude;
            return dSq <= mineRadius * mineRadius;
        }

        /// <summary>Resolve the NEAREST still-standing ENABLED node within <see cref="mineRadius"/> of
        /// <paramref name="from"/>. Broken/disabled/mid-tween nodes are skipped. Planar (XZ) distance only.</summary>
        private MineableNodeState ResolveNearestMineable(Vector3 from)
        {
            MineableNodeState best = null;
            float bestSq = mineRadius * mineRadius;
            Vector2 here = new Vector2(from.x, from.z);
            for (int i = 0; i < _nodes.Count; i++)
            {
                MineableNodeState s = _nodes[i];
                if (!s.IsMineable) continue;
                Vector3 p = s.Position;
                float dSq = (here - new Vector2(p.x, p.z)).sqrMagnitude;
                if (dSq <= bestSq) { bestSq = dSq; best = s; }
            }
            return best;
        }

        /// <summary>
        /// PURE mine-on-a-left-click decision (the unit-testable guard truth-table — the sibling of
        /// ChopTree.ShouldChopOnClick). Given a left-click edge this frame, decide whether ONE strike lands:
        ///   • <paramref name="inRange"/> — SOME enabled node is within <see cref="mineRadius"/> (the nearest);
        ///   • <paramref name="pickaxeSelected"/> — a pickaxe is the SELECTED belt item (the load-bearing gate);
        ///   • NOT <paramref name="uiPanelOpen"/> — no modal panel owns the screen;
        ///   • NOT <paramref name="pointerOverUI"/> — the click is NOT over the inventory/belt UI;
        ///   • NOT <paramref name="rmbHeld"/> — the right mouse button is NOT held (no camera-orbit drag).
        /// All five must hold. Static + dependency-free so the EditMode guard asserts the whole table.
        /// </summary>
        public static bool ShouldMineOnClick(bool inRange, bool pickaxeSelected,
                                             bool uiPanelOpen, bool pointerOverUI, bool rmbHeld)
            => inRange && pickaxeSelected && !uiPanelOpen && !pointerOverUI && !rmbHeld;

        /// <summary>Request ONE mine-strike programmatically — the input-independent analog of a left-click. Latched
        /// + consumed on the next Update (mirrors ChopTree.RequestChopClick). The guards still apply.</summary>
        public void RequestMineClick() => _mineClickRequested = true;

        /// <summary>Set the LMB-HELD state programmatically (the analog of ChopTree.SetChopHeld). OR'd with the real
        /// button; drives the hold-repeat chain in headless PlayMode + the shipped capture.</summary>
        public void SetMineHeld(bool held) => _mineHeldRequested = held;

        /// <summary>Land one strike on the FIRST enabled node directly + IMMEDIATELY (swing + effect now, no impact
        /// delay), regardless of range. Public so a PlayMode swing test can drive a strike in isolation. Mirrors
        /// ChopTree.Chop().</summary>
        public void Mine()
        {
            MineableNodeState target = FirstMineable();
            if (target == null) return;
            if (character != null) { character.FaceWorldTarget(target.Position); character.TriggerMine(); } // 86caffwv5 — pickaxe swing
            ApplyStrikeEffect(target);
        }

        private MineableNodeState FirstMineable()
        {
            for (int i = 0; i < _nodes.Count; i++)
                if (_nodes[i].IsMineable) return _nodes[i];
            return null;
        }

        // BEGIN a mine swing on the click: FACE the node + SWING the arm NOW, SCHEDULE the strike EFFECT for the
        // swing's IMPACT frame (so the node doesn't break before the pickaxe visually connects). The effective delay
        // scales with tool-use speed. Single-flight: callers gate on !_impactPending. Mirrors ChopTree.BeginChopSwing.
        private void BeginMineSwing(MineableNodeState target)
        {
            if (target == null || !target.IsMineable) return;

            if (character != null)
            {
                character.FaceWorldTarget(target.Position);
                character.TriggerMine(); // 86caffwv5 — the mine strike plays the PICKAXE swing (WeaponClass=pickaxe)
            }

            float speed = character != null
                ? Mathf.Clamp(character.chopSpeed, CastawayCharacter.ChopSpeedMin, CastawayCharacter.ChopSpeedMax)
                : 1f;
            float delay = Mathf.Max(0f, swingImpactDelaySeconds) / Mathf.Max(0.0001f, speed);
            _pendingTarget = target;
            _impactAt = Now + delay;
            _impactPending = true;

            _swingEndsAt = Now + Mathf.Max(delay, ComputeSwingDuration());
        }

        private float ComputeSwingDuration()
        {
            // 86caffwv5 soak-3 — divide the clip by the pickaxe swing's EFFECTIVE playback speed (chopSpeed ×
            // SwingSpeedPickaxe), NOT raw chopSpeed. TriggerMine (called in BeginMineSwing before this) sets the
            // class to pickaxe, so CurrentSwingPlaybackSpeed here reflects the sped-up 1.5× pickaxe swing. Before this
            // fix the cadence divided by chopSpeed only, so the sped-up swing finished then sat IDLE until the full
            // un-sped clip length elapsed — the Sponsor's "long waiting from idle to next swing when holding" (soak-3).
            float speed = character != null ? character.CurrentSwingPlaybackSpeed : 1f;
            float liveLen = character != null ? character.MeleeClipLength : 0f;
            float authored = liveLen > 0f ? liveLen : Mathf.Max(SwingClipLengthFloor, swingClipLengthSeconds);
            return authored / Mathf.Max(0.0001f, speed);
        }

        // APPLY the strike effect at IMPACT: advance the count + shake the node (non-breaking), and BREAK it on the
        // final strike (drop a lootable ORE PILE + schedule fade + regrow). Re-checks IsMineable so a stale pending
        // impact can't double-break. Mirrors ChopTree.ApplyChopEffect.
        private void ApplyStrikeEffect(MineableNodeState target)
        {
            if (target == null || !target.IsMineable) return;

            bool broke = target.LandStrike(strikesToBreak, regrowthMinSeconds, regrowthMaxSeconds);

            if (!broke) target.Shake(strikeShakeDegrees);

            // On BREAK, drop a lootable ORE PILE at the node's spot holding the node's ore yield (the ore-loot side
            // of the chain). Null spawner/inventory → breaking still completes (the node depletes), just no pile.
            if (broke && orePileSpawner != null && inventory != null)
            {
                Material oreMat = target.FirstSharedMaterial();
                OrePile pile = orePileSpawner.SpawnAt(target.Position, inventory, oreMat);
                MineTrace("BREAK -> spawned ore pile holding " + (pile != null ? pile.OreRemaining : 0) +
                          " ore at " + target.Position.ToString("F1"));
            }

            if (!_tracedFirstStrike)
            {
                _tracedFirstStrike = true;
                MineTrace("impact " + target.Strikes + "/" + strikesToBreak + " (ore drops on break)" +
                          " (swing=" + (character != null) + ")");
            }
            if (broke)
                MineTrace("node BROKEN after " + target.Strikes + " strikes; regrow in " +
                          (target.RegrowAt - Now).ToString("F0") + "s");
        }

        // [mine-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call + its
        // argument evaluation from the shipped IL2CPP release exe (unity6-mastery §5/§10).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void MineTrace(string msg) => Debug.Log("[mine-trace] " + msg);
    }

    /// <summary>
    /// Per-node MINE STATE (the sibling of <see cref="ChoppableTreeState"/>) — each authored ore node's own
    /// strikes-landed / broken / fade / regrow / enabled lifecycle. A plain class (no MonoBehaviour): MineOre owns
    /// a list of these and drives them, so a broken node fades away in place while its neighbours stand, then
    /// regrows at the same spot on its own random timer. <see cref="Enabled"/> gates whether the node participates
    /// (the ore-rarity dial disables pool nodes beyond ActiveNodeCount — a disabled node hides + is not mineable).
    ///
    /// Tween-STEPPED via <see cref="_dt"/> (Time.unscaledDeltaTime in the ship build; the injected test clock's
    /// per-frame advance under a PlayMode test) and SCHEDULED via <see cref="Now"/> (Time.time in the ship build;
    /// the injected test clock under a test). The test seam (86camf3xe, mirrors ChoppableTreeState / PR #288) makes
    /// the break/fade/regrow timers deterministic headlessly instead of freezing on Time.unscaledDeltaTime≈0 — the
    /// shipped build is byte-identical (the seam compiles out). NO mutable statics.
    /// </summary>
    public class MineableNodeState
    {
        private readonly Transform _visual;
        private readonly System.Random _rng;
        private readonly Renderer[] _renderers;

        private int _strikes;
        private bool _broken;
        private float _regrowAt;

        private bool _enabled = true;   // the ore-rarity dial can disable a pool node (hidden + not mineable)

        private bool _breaking;         // playing the sink+crumble break tween
        private bool _fadingOut;        // playing the scale-down fade-out tween
        private bool _removed;          // faded out + renderers disabled, awaiting regrow (ground empty)
        private bool _regrowing;        // playing the scale-up regrow tween
        private float _tweenT;
        private float _fadeAt;
        private Vector3 _standPos;
        private Quaternion _standRot;
        private Vector3 _standScale = Vector3.one;

        private bool _shaking;
        private float _shakeT;
        private float _shakeDeg;

        // 86caffwv5 round-7 (TASK 2 — "block movement like trees do") — the MOVEMENT carve. The world is
        // DELIBERATELY collider-free and the player is a NavMeshAgent, so a physics COLLIDER would NOT stop the
        // player (a NavMeshAgent walks the baked navmesh, ignoring colliders — PlacementObstacleRegistry.cs
        // documents the collider-free world). Trees block by CARVING the navmesh with a NavMeshObstacle
        // (LowPolyZoneGen.BuildTree); boulders + ore nodes did NOT, so the player could walk INSIDE the big
        // minable boulders (the Sponsor's screenshot). This carve is the SAME mechanism trees use — a snug capsule
        // hugging the visual footprint so the player is blocked at the surface yet can still stand within mine
        // range (carve + agentRadius << mineRadius). It is toggled OFF while the node is broken/removed/regrowing
        // so a mined-away spot is never an invisible wall (a rarity-disabled node's GameObject is inactive, so its
        // obstacle is inert automatically). RUNTIME carve: no re-bake, no committed-scene change — carving cuts the
        // baked navmesh at runtime exactly like trees.
        private UnityEngine.AI.NavMeshObstacle _carve;
        private bool _carveOn;
        private float _carveFootprint;   // 86caffwv5 round-8 — the measured world planar footprint the carve was sized from (guard/log)
        private float _carveMaxExtent;   // 86caffwv5 round-8 — the circumscribing (max) world half-extent (guard/log)

        // 86caffwv5 round-8 (TASK 2 fix — the Sponsor's soak-7 verdict: "im blocked but already at this distance in
        // the screenshot, not at the edge of the boulder" — blocked ~a body-length OUT, invisible-wall feel). The
        // round-7 carve used the CIRCUMSCRIBING world-AABB half-extent (Mathf.Max(extents.x, extents.z)); for a lumpy
        // FacetedRock that captures the widest facet SPIKE, so the carve blocked at the spike radius while the visual
        // surface undulates well inside it. Worse, the baked navmesh erodes the walkable boundary by ~the agent
        // radius AROUND a carve (documented in LowPolyZoneGen's tree-barrier fix 86caa4c5c: "the NavMesh bake erodes
        // the walkable boundary by the agent radius (0.4u)"), so the player STANDS ~agentRadius BEYOND the carve
        // boundary. Trees get away with that standoff because the trunk is thin (nothing big sits in the gap); a BIG
        // boulder turns the same standoff into a visible ground gap that reads as an invisible wall. FIX: size the
        // carve so the player's STAND position (carve + agentRadius) lands at the boulder's visual surface + a small
        // clearance — carve = footprint + clearance − agentStandoff — using the SNUG (min) planar half-extent so a
        // circle carve hugs the narrow silhouette (a circle cannot hug tighter without a gap on that axis; on the
        // wider axis the player just touches / lightly overlaps, which reads as touching — the Sponsor's accepted
        // direction). Because the +agentRadius the player picks up at stand-time CANCELS the −agentStandoff here, the
        // blocked distance ≈ footprint + clearance regardless of the exact erosion, so the formula is robust to an
        // imperfect standoff estimate. Same MineableNodeState path → applies to boulders AND ore nodes.
        public const float CarveClearance = 0.15f;      // small hug past the visual surface (the tree idiom, LowPolyZoneGen.TrunkObstacleClearance)
        public const float CarveAgentStandoff = 0.40f;  // the navmesh erodes ~the agent radius (0.4u, MovementCameraScene) around a carve — subtract it so the player stands AT the surface, not agentRadius out
        public const float CarveFloorRadius = 0.20f;    // never degenerate: a small node's carve still reliably cuts the 0.16u-voxel navmesh

        /// <summary>86caffwv5 round-8 (TASK 2 fix) — PURE snug-carve WORLD radius from a node's planar footprint
        /// half-extent: <c>footprint + clearance − agentStandoff</c>, floored so it never degenerates. Static +
        /// dependency-free so the EditMode guard asserts the tighten invariant (carve &lt; footprint; carve grows
        /// with the footprint; blocked = carve + agentStandoff ≈ footprint + clearance) with no scene rig — the
        /// boulder sibling of <c>LowPolyZoneGen.TrunkObstacleLocalRadius</c>.</summary>
        public static float MovementCarveWorldRadius(float footprintRadius) =>
            Mathf.Max(CarveFloorRadius, footprintRadius + CarveClearance - CarveAgentStandoff);

        // 86camf3xe — this Tick's tween STEP delta (set at the top of Tick). Time.unscaledDeltaTime in the shipped
        // build (production unchanged — the game never scales Time.timeScale, so unscaled == scaled); the injected
        // test clock's per-frame advance under a PlayMode test, so the break/fade/regrow tweens complete in a
        // DETERMINISTIC fixed-step frame count instead of FREEZING on the headless -batchmode Time.unscaledDeltaTime≈0
        // (the historical regrow red: the tween never advanced → the node never regrew). The tween Step* methods read
        // this field, never Time.unscaledDeltaTime directly. Mirrors ChoppableTreeState._dt (PR #288 / 86camdk1h).
        private float _dt;
#if UNITY_INCLUDE_TESTS
        /// <summary>86camf3xe — TEST-ONLY deterministic clock (STRIPPED from ship builds via UNITY_INCLUDE_TESTS),
        /// set by the owning <see cref="MineOre"/> to its injected fake clock so a PlayMode test's fixed-step clock
        /// drives this node's break/fade/regrow SCHEDULING (Now) AND its tween STEPPING (_dt) deterministically.
        /// Null → <c>Time.time</c> (production-identical). Mirrors ChoppableTreeState.TestClock.</summary>
        public System.Func<float> TestClock;
        private float _prevNow;      // previous Tick's clock sample (to derive the fixed-step tween delta)
        private bool _prevNowSet;
#endif

        /// <summary>The SCHEDULING clock this node's break/fade/regrow timers read. <c>Time.time</c> in the shipped
        /// build (the TestClock seam is compiled out → a plain <c>Time.time</c> read, production byte-identical);
        /// a test clock when the owning MineOre injects one (86camf3xe). Tween STEPPING rides <see cref="_dt"/>.</summary>
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

        private const float BreakDuration = 0.4f;
        private const float FadeOutDuration = 0.7f;
        private const float RegrowRiseDuration = 0.6f;
        private const float ShakeDuration = 0.2f;
        private const float FadeOutDelaySeconds = 1.2f; // brief rest between break-crumble and fade
        private static readonly Vector3 BreakDrop = Vector3.down * 0.35f;
        private const float BreakTipDeg = 24f;

        public MineableNodeState(Transform visual, int seed)
        {
            _visual = visual;
            if (_visual != null)
            {
                _standPos = _visual.position;
                _standRot = _visual.rotation;
                _standScale = _visual.localScale;
                _renderers = _visual.GetComponentsInChildren<Renderer>(true);
                BuildMovementCarve(); // 86caffwv5 round-7 — block the NavMeshAgent player like trees do (TASK 2)
            }
            _rng = new System.Random(seed != 0 ? seed : Environment.TickCount);
        }

        // 86caffwv5 round-7 (TASK 2) — add the carving NavMeshObstacle that blocks the NavMeshAgent player from
        // walking into the mineable, sized to the visual's actual planar footprint (measured from the combined
        // renderer bounds) so it hugs the rock. Radius/center/height are LOCAL values (NavMeshObstacle scales them
        // by the visual's lossyScale), so the world footprint is divided out of the transform scale — a snug collar
        // at any scale (the same fix trees use, LowPolyZoneGen.TrunkObstacleLocalRadius). Null-safe: a bare rig with
        // no renderer gets no carve (harmless). The carve starts ON iff the node is currently mineable (standing).
        private void BuildMovementCarve()
        {
            if (_visual == null || _renderers == null || _renderers.Length == 0) return;
            if (!TryPlanarFootprint(out Vector3 worldCenter, out float footprintRadius, out float maxExtent, out float height)) return;
            if (footprintRadius <= 1e-3f) return;

            var carve = _visual.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (carve == null) carve = _visual.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();

            Vector3 ls = _visual.lossyScale;
            float sxz = Mathf.Max(1e-4f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)));
            float sy = Mathf.Max(1e-4f, Mathf.Abs(ls.y));

            // 86caffwv5 round-8 — the SNUG world carve (footprint + clearance − agentStandoff; see the constants +
            // MovementCarveWorldRadius above). The player stands at carve + agentRadius ≈ footprint + clearance = the
            // visual surface + a hair, so the block reads as touching (not the round-7 body-length gap).
            float worldCarve = MovementCarveWorldRadius(footprintRadius);

            carve.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
            carve.center = _visual.InverseTransformPoint(worldCenter);
            carve.radius = worldCarve / sxz;                          // LOCAL; world footprint == worldCarve (snug)
            carve.height = Mathf.Max(height, worldCarve * 2f) / sy;   // span the vertical extent (reaches the navmesh)
            carve.carving = true;
            carve.enabled = IsMineable;   // standing → carve on; Tick keeps it in lock-step with mineability
            _carve = carve;
            _carveOn = carve.enabled;
            _carveFootprint = footprintRadius; // 86caffwv5 round-8 — remember the measured footprint for the guard/log
            _carveMaxExtent = maxExtent;
        }

        // Combined WORLD planar footprint of this node's renderers (the carve size source). Returns false if the
        // node has no valid renderer bounds (a bare rig).
        //   footprintRadius = the SMALLER (snug) XZ half-extent — a circle carve hugs the narrow silhouette; the
        //     wider axis just touches / lightly overlaps (reads as touching). 86caffwv5 round-8: this replaced the
        //     round-7 Mathf.MAX (circumscribing) footprint that blocked at the widest facet spike → the Sponsor's
        //     "a body-length from the visual edge" gap.
        //   maxExtent = the LARGER XZ half-extent (the circumscribing spike) — kept for the carve-tracks-bounds
        //     EditMode guard + the BLOCKED-AT-SURFACE capture log (so the visual-edge interplay is inspectable).
        //   height    = the full vertical extent.
        private bool TryPlanarFootprint(out Vector3 worldCenter, out float footprintRadius, out float maxExtent, out float height)
        {
            worldCenter = Vector3.zero; footprintRadius = 0f; maxExtent = 0f; height = 0f;
            bool any = false;
            Bounds b = default;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                if (!any) { b = _renderers[i].bounds; any = true; }
                else b.Encapsulate(_renderers[i].bounds);
            }
            if (!any) return false;
            worldCenter = b.center;
            footprintRadius = Mathf.Min(b.extents.x, b.extents.z);
            maxExtent = Mathf.Max(b.extents.x, b.extents.z);
            height = b.size.y;
            return true;
        }

        public int Strikes => _strikes;
        public bool Broken => _broken;
        public bool Enabled => _enabled;
        public float RegrowAt => _regrowAt;
        public Vector3 Position => _visual != null ? _visual.position : Vector3.zero;

        /// <summary>86caffwv5 round-7 (TASK 2) — true once this node carries a movement-blocking NavMeshObstacle
        /// carve (a bare rig with no renderer has none). For the EditMode carve guard.</summary>
        public bool HasMovementCarve => _carve != null;

        /// <summary>86caffwv5 round-7 (TASK 2) — true while this node's movement carve is actively blocking the
        /// player (enabled + carving). Follows mineability: on while standing, off while broken/removed/regrowing.
        /// For the EditMode carve-lifecycle guard.</summary>
        public bool MovementCarveActive => _carve != null && _carve.enabled && _carve.carving;

        /// <summary>86caffwv5 round-7 (TASK 2) — the movement carve's LOCAL radius (0 if none). For the EditMode
        /// guard that pins the carve is sized to the footprint (so mine range still reaches over it). NOTE: at
        /// localScale 1 (the authored boulders/ore nodes) LOCAL == WORLD radius.</summary>
        public float MovementCarveRadius => _carve != null ? _carve.radius : 0f;

        /// <summary>86caffwv5 round-8 (TASK 2 fix) — the SNUG world planar half-extent the carve was sized from
        /// (min of the XZ bounds extents), 0 if no carve. For the carve-tracks-bounds guard.</summary>
        public float MovementCarveFootprint => _carveFootprint;

        /// <summary>86caffwv5 round-8 (TASK 2 fix) — the circumscribing (max) world planar half-extent = the visual
        /// spike the round-7 carve used, 0 if no carve. For the guard proving the carve is now TIGHTER than this.</summary>
        public float MovementCarveMaxExtent => _carveMaxExtent;

        /// <summary>True only when this node is ENABLED and STANDING (not broken, not mid-tween) → strike-eligible.</summary>
        public bool IsMineable => _enabled && !_broken && !_breaking && !_fadingOut && !_removed && !_regrowing;

        /// <summary>True if any of this node's renderers is currently enabled (visible).</summary>
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

        public bool Shaking => _shaking;

        /// <summary>Enable/disable this pool node (the ore-rarity dial). A disabled node hides its GameObject (so it
        /// is neither visible nor mineable); re-enabling restores it to standing (reset strikes/tweens). Idempotent.</summary>
        public void SetEnabled(bool on)
        {
            if (_enabled == on) return;
            _enabled = on;
            if (_visual != null)
            {
                if (!on)
                {
                    _visual.gameObject.SetActive(false);
                }
                else
                {
                    // Restore to a fresh standing node.
                    _broken = _breaking = _fadingOut = _removed = _regrowing = _shaking = false;
                    _strikes = 0;
                    _visual.position = _standPos;
                    _visual.rotation = _standRot;
                    _visual.localScale = _standScale;
                    _visual.gameObject.SetActive(true);
                    SetRenderersEnabled(true);
                }
            }
        }

        public void Tick()
        {
            // 86camf3xe — this frame's tween step delta. Time.unscaledDeltaTime in the shipped build (production
            // unchanged — the game never scales Time.timeScale, so unscaled == scaled); the injected test clock's
            // per-frame advance under a PlayMode test, so the tweens complete in a DETERMINISTIC frame count
            // headlessly instead of freezing on Time.unscaledDeltaTime≈0. Computed EVERY Tick (even while disabled)
            // so a tween that starts later has a consistent step (no accumulated gap from Ticks that ran no Step*).
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

            // 86caffwv5 round-7 (TASK 2) — keep the movement carve in lock-step with mineability so a broken /
            // removed / regrowing node stops blocking the player at an empty spot (and a regrown one blocks again).
            // Cheap: writes only on a transition. Runs even while disabled (IsMineable is false then → carve off),
            // but a rarity-disabled node's GameObject is inactive so its obstacle is inert regardless.
            if (_carve != null && _carveOn != IsMineable)
            {
                _carveOn = IsMineable;
                _carve.enabled = _carveOn;
            }

            if (!_enabled) return;
            if (_breaking) { StepBreaking(); return; }
            if (_fadingOut) { StepFadeOut(); return; }
            if (_regrowing) { StepRegrow(); return; }
            if (_removed)
            {
                if (Now >= _regrowAt) BeginRegrow();
                return;
            }
            if (_broken)
            {
                if (Now >= _regrowAt) { BeginRegrow(); return; }
                if (Now >= _fadeAt) BeginFadeOut();
                return;
            }
            if (_shaking) StepShake();
        }

        /// <summary>Land one strike: advance the count, and on the <paramref name="strikesToBreak"/>-th BREAK the
        /// node (crumble tween, schedule fade + a random regrow in [min,max]). Returns true on the breaking strike.
        /// A no-op if not currently mineable. The CALLER drops the ore pile + swings the arm.</summary>
        public bool LandStrike(int strikesToBreak, float regrowthMinSeconds, float regrowthMaxSeconds)
        {
            if (!IsMineable) return false;
            _strikes++;
            if (_strikes >= strikesToBreak)
            {
                _broken = true;
                _shaking = false;
                BeginBreaking();
                _fadeAt = Now + FadeOutDelaySeconds;
                ScheduleRegrow(regrowthMinSeconds, regrowthMaxSeconds);
                return true;
            }
            return false;
        }

        public void Shake(float degrees)
        {
            if (!IsMineable || _visual == null || degrees <= 0f) return;
            _shakeDeg = degrees;
            _shakeT = 0f;
            _shaking = true;
        }

        private void StepShake()
        {
            if (_visual == null) { _shaking = false; return; }
            _shakeT += _dt;
            float k = Mathf.Clamp01(_shakeT / ShakeDuration);
            float tip = Mathf.Sin(k * Mathf.PI) * _shakeDeg;
            _visual.rotation = _standRot * Quaternion.Euler(tip, 0f, 0f);
            if (k >= 1f) { _shaking = false; _visual.rotation = _standRot; }
        }

        /// <summary>The first shared material on this node's visual (its rock MeshRenderer) — handed to the spawned
        /// OrePile so the ore chunks read as the same ore. Null if the node has no renderer (a bare rig).</summary>
        public Material FirstSharedMaterial()
        {
            if (_renderers == null) return null;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && _renderers[i].sharedMaterial != null)
                    return _renderers[i].sharedMaterial;
            return null;
        }

        private void ScheduleRegrow(float regrowthMinSeconds, float regrowthMaxSeconds)
        {
            float min = Mathf.Max(0f, regrowthMinSeconds);
            float max = Mathf.Max(min, regrowthMaxSeconds);
            float delay = min + (float)_rng.NextDouble() * (max - min);
            _regrowAt = Now + delay;
        }

        private void BeginBreaking()
        {
            if (_visual == null) { _breaking = false; return; }
            _standPos = _visual.position;
            _standRot = _visual.rotation;
            _standScale = _visual.localScale;
            _breaking = true;
            _tweenT = 0f;
        }

        private void StepBreaking()
        {
            if (_visual == null) { _breaking = false; return; }
            _tweenT += _dt;
            float k = Mathf.Clamp01(_tweenT / BreakDuration);
            float ease = k * k * (3f - 2f * k);
            _visual.position = _standPos + BreakDrop * ease;
            _visual.rotation = _standRot * Quaternion.Euler(BreakTipDeg * ease, 0f, 0f);
            if (k >= 1f) _breaking = false; // now resting (crumbled) until the fade-out delay
        }

        private void BeginFadeOut() { _fadingOut = true; _tweenT = 0f; }

        private void StepFadeOut()
        {
            if (_visual == null) { _fadingOut = false; _removed = true; return; }
            _tweenT += _dt;
            float k = Mathf.Clamp01(_tweenT / FadeOutDuration);
            float ease = k * k * (3f - 2f * k);
            _visual.localScale = _standScale * (1f - ease);
            if (k >= 1f)
            {
                _fadingOut = false;
                _removed = true;
                SetRenderersEnabled(false);
                _visual.localScale = _standScale;
            }
        }

        private void BeginRegrow()
        {
            _removed = false;
            _regrowing = true;
            _tweenT = 0f;
            if (_visual != null)
            {
                _visual.position = _standPos;
                _visual.rotation = _standRot;
                _visual.localScale = _standScale * 0.001f;
            }
            SetRenderersEnabled(true);
        }

        private void StepRegrow()
        {
            if (_visual == null) { _regrowing = false; _broken = false; _strikes = 0; return; }
            _tweenT += _dt;
            float k = Mathf.Clamp01(_tweenT / RegrowRiseDuration);
            float ease = k * k * (3f - 2f * k);
            _visual.localScale = _standScale * Mathf.Max(0.001f, ease);
            if (k >= 1f)
            {
                _regrowing = false;
                _broken = false;
                _strikes = 0;
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
