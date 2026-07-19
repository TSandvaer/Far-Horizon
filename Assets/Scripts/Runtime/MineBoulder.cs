using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The mine-a-BOULDER mechanic + the player mining SWING (ticket 86camz9v7 / crafting-redesign ② — boulder
    /// mining). The VOLUME stone source beyond hand-gathered pebbles: LARGER stone boulders the castaway reaches,
    /// then — WITH A PICKAXE SELECTED in the belt (wood tier is the ENTRY gate) — mines by LEFT-CLICKING (exactly
    /// like ore/chop, the sibling verbs); each click SWINGS the arm via the shared melee Attack animation +
    /// advances the boulder's strike count; after <see cref="strikesToBreak"/> strikes the boulder BREAKS and DROPS
    /// a lootable STONE PILE (the StonePile sibling of the ore/log pile), which the player loots with E, then the
    /// boulder depletes → REGROWS after a tweakable random delay. Mining reads as WORK, not a proximity-auto pickup
    /// ([[active-input-not-proximity-auto-for-actions]]).
    ///
    /// === Deliberate MIRROR of MineOre (spec §9-② "boulder-mining is a MineOre SIBLING — reuse it, do NOT invent
    /// a new verb") ===
    /// This is a faithful sibling of <see cref="MineOre"/> (which was itself a faithful sibling of ChopTree) — the
    /// SAME active-left-click trigger, the SAME pure <see cref="MineOre.ShouldMineOnClick"/> guard truth-table +
    /// three world-click guards (REUSED directly — the guard is generic), the SAME impact-delayed single-flight
    /// strike, the SAME hold-to-repeat clip-completion cadence, the SAME nearest-in-range resolver, the SAME
    /// per-node lifecycle (<see cref="MineableNodeState"/>, REUSED directly — the break/fade/regrow state is not
    /// ore-specific), and the SAME editor-time-authored + runtime-discovered pool model. It reuses the EXISTING
    /// swing (CastawayCharacter.TriggerChop), the EXISTING E-loot surface (PickableLooter/IPickable via StonePile),
    /// and the EXISTING deterministic-test-clock seam. It does NOT refactor or touch MineOre — so iron-ore mining
    /// (#287) stays green by construction (the ② regression guard).
    ///
    /// === The tier gate — WOOD-pickaxe MINIMUM, widened up the ladder (spec §5 mining-capability gate) ===
    /// A boulder is mineable with ANY pickaxe (wood is the ENTRY tool: "WOOD pickaxe mines boulders→stone only");
    /// a STONE pickaxe mines "iron-ore (+ stone)" and an IRON pickaxe is fastest — so higher tiers are strictly
    /// MORE capable and also mine boulders. <see cref="IsBoulderPickaxeSelected"/> is the boulder analog of
    /// <see cref="MineOre.IsPickaxeSelected"/>, WIDENED to accept the wood pickaxe (the load-bearing ② gate: the
    /// wood pickaxe you craft at the ① table is what unlocks boulder-stone). The pickaxe must be the SELECTED belt
    /// item, not merely owned — no proximity-auto (the #224 class).
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The manager GameObject + this component + its Inventory/player/character/UI/spawner/boulderRoot references
    /// are authored editor-time into Boot.unity (MovementCameraScene.BuildBoulders), NOT at Awake (the legs-up
    /// class). BoulderSceneTests guards the scene presence + that the refs serialize. The boulder pool is a DISCRETE
    /// scene-author ADD OUTSIDE the seeded LowPolyZoneGen scatter stream — it provably cannot perturb the seed-42
    /// island/scatter/NavMesh ([[world-is-big-round-island]]).
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// `[boulder-trace]` lines on strike / break / regrow, [Conditional("UNITY_EDITOR")] so they strip from the
    /// shipped IL2CPP exe (unity6-mastery §5/§10 — no Debug.Log in the shipped hot path).
    ///
    /// NO MUTABLE STATICS (instance state only) — needs no [RuntimeInitializeOnLoadMethod] reset (StaticStateResetTests).
    /// </summary>
    public class MineBoulder : MonoBehaviour
    {
        /// <summary>The NAMED default strikes-to-break a boulder. default 4 (mirrors MineOre.StrikesToBreakDefault
        /// band 1–10) — a boulder is bigger but the yield is richer, so the strike count matches ore for a
        /// consistent mine feel. default 4 — Sponsor-soak tunes.</summary>
        public const int StrikesToBreakDefault = 4;

        /// <summary>Range floor — strikes-to-break clamps within [1, 10] (mirrors MineOre's band).</summary>
        public const int StrikesToBreakMin = 1;

        /// <summary>Range ceiling — strikes-to-break clamps within [1, 10].</summary>
        public const int StrikesToBreakMax = 10;

        /// <summary>The GameObject name BuildBoulders gives every authored boulder visual. The runtime resolver
        /// collects these under <see cref="boulderRoot"/> so every pool boulder is mineable. A READ-only key.</summary>
        public const string BoulderNodeName = "Boulder";

        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger mined stone is dropped toward (the StonePile loots stone into it). Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform whose proximity (with a pickaxe SELECTED) triggers a mine-strike. Wired at bootstrap.")]
        public Transform player;

        [Tooltip("The root whose Boulder children are the authored boulder pool. Discovered at Start and made " +
                 "mineable. Wired editor-time; a name-scan fallback finds it if unwired. Null is tolerated (no " +
                 "boulders — a bare test rig).")]
        public Transform boulderRoot;

        [Tooltip("The castaway whose TriggerChop() plays the melee SWING on each landed strike (the SAME swing the " +
                 "chop/mine verbs use). Wired at bootstrap; null is graceful (the strike still lands, no arm swing).")]
        public CastawayCharacter character;

        [Tooltip("The inventory/belt UI — a left-click OVER this UI must NOT mine the boulder behind it (the mining " +
                 "analog of the chop over-UI guard). Wired at bootstrap; null is tolerated (guard skipped).")]
        public InventoryUI inventoryUI;

        [Tooltip("The shared StonePileSpawner that mints a lootable StonePile when a boulder breaks (holding the " +
                 "boulder's stone yield). Wired at bootstrap; null is tolerated (breaking completes but no pile drops).")]
        public StonePileSpawner stonePileSpawner;

        [Header("Interaction")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' a boulder and (with a selected pickaxe) " +
                 "mines it. Slightly larger than the ore radius (a boulder is bigger).")]
        public float mineRadius = 2.4f;

        [Tooltip("86catr49m — planar (XZ) no-build radius each STANDING boulder projects into " +
                 "PlacementObstacleRegistry so a crafting-table ghost reads RED over it (a boulder is bigger " +
                 "than an ore node's 0.6). The zone is registered while the boulder is MINEABLE (standing), " +
                 "UNREGISTERED on break / mined-away, and RE-registered on regrow (keyed on IsMineable — NOT a " +
                 "PlacementObstacle component, whose OnEnable/OnDisable never fires across the break→regrow " +
                 "cycle; Drew's #303 trace). Soak-tune (a boulder's rough footprint).")]
        public float placementObstacleRadius = 1.2f;

        [Tooltip("Strikes needed to break a boulder (default 4, range 1–10). After this many it breaks + drops its " +
                 "stone pile, then regrows. Shared across every boulder. default 4 — Sponsor-soak tunes.")]
        public int strikesToBreak = StrikesToBreakDefault;

        [Tooltip("Per-strike SHAKE/RECOIL amount (degrees of the brief tip-impulse each non-breaking strike gives " +
                 "the boulder). A cheap transform nudge. Soak-tune. 0 disables the shake.")]
        public float strikeShakeDegrees = 4f;

        [Tooltip("Minimum seconds between landed strikes (a small COOLDOWN so a stray double-edge can't out-pace the " +
                 "swing read). Mirrors MineOre.strikeInterval. 0 = no cooldown.")]
        public float strikeInterval = 0.25f;

        [Header("Regrowth (TWEAKABLE — organic within [min,max], like ore/tree regrowth)")]
        [Tooltip("Minimum seconds before a broken boulder regrows into a mineable boulder. Actual regrow is RANDOM " +
                 "in [min,max] (organic). Default ~6 min. default — Sponsor-soak tunes.")]
        public float regrowthMinSeconds = 360f;   // 6 min

        [Tooltip("Maximum seconds before a broken boulder regrows (random within [min,max]). Default ~9 min.")]
        public float regrowthMaxSeconds = 540f;    // 9 min

        [Header("Swing impact (sync the strike EFFECT to the swing's down-stroke — mirrors MineOre)")]
        [Tooltip("Seconds from the strike CLICK to the swing's IMPACT frame — the click fires the swing + face-turn " +
                 "immediately, but the strike EFFECT (progress + break + pile drop) lands at IMPACT so the boulder " +
                 "doesn't break before the pickaxe has visually connected. Scales with tool-use speed (÷ chopSpeed).")]
        public float swingImpactDelaySeconds = 0.4f;

        [Tooltip("FALLBACK authored length (seconds, at 1x speed) of the swing clip. HOLD-TO-MINE gates the NEXT " +
                 "swing on the CURRENT swing's clip COMPLETING (one completed swing = one strike). The LIVE cadence " +
                 "prefers CastawayCharacter.MeleeClipLength; this is the fallback for a bare headless rig.")]
        public float swingClipLengthSeconds = 1.6f;

        /// <summary>Strictly-positive floor for the <see cref="swingClipLengthSeconds"/> fallback (mirrors MineOre).</summary>
        public const float SwingClipLengthFloor = 0.05f;

        [Header("Pool")]
        [Tooltip("How many pool boulders are ENABLED. -1 (default) = ALL of the authored pool (boulders are a fixed " +
                 "discrete VOLUME source — no rarity dial like ore). A test/inspector may set a specific count.")]
        public int activeNodeCount = -1;

        [Tooltip("Deterministic seed for the regrow-time rolls (so headless tests are reproducible). 0 = a " +
                 "time-based seed at runtime. Each boulder derives its own sub-seed. Mirrors MineOre.regrowSeed.")]
        public int regrowSeed = 0;

        // The per-boulder mineable instances (the whole authored pool). REUSES MineableNodeState (MineOre.cs) — the
        // break/fade/regrow lifecycle is generic, not ore-specific.
        private readonly List<MineableNodeState> _nodes = new List<MineableNodeState>();

        // 86catr49m — per-boulder PLACEMENT-obstacle bookkeeping, kept in lock-step (same index) with _nodes:
        // each boulder's visual transform + whether its no-build zone is CURRENTLY registered in
        // PlacementObstacleRegistry. SyncPlacementObstacles() drives register/unregister off each node's
        // IsMineable so a mined-away boulder stops blocking placement (Drew's #303 trace: the break→fade→regrow
        // cycle never toggles GameObject.SetActive, so a PlacementObstacle-component OnEnable/OnDisable would
        // wrongly keep the empty spot blocked until regrow). No RNG draws — the seed-42/boulder scatter stream
        // is untouched (world seed LOCKED, 86catr49m constraint 3).
        private readonly List<Transform> _nodeVisuals = new List<Transform>();
        private readonly List<bool> _nodeRegistered = new List<bool>();

        // Programmatic LEFT-CLICK latch (the input-independent seam, the analog of MineOre.RequestMineClick).
        private bool _mineClickRequested;
        private float _lastStrikeAt = float.NegativeInfinity;

        // HOLD-TO-MINE — programmatic LMB-HELD state (OR'd with the real mouse).
        private bool _mineHeldRequested;

        // Impact timing — a single in-flight PENDING IMPACT (single-flight: a 2nd click while pending is ignored).
        private bool _impactPending;
        private float _impactAt;
        private MineableNodeState _pendingTarget;

        // HOLD cadence — the wall-clock time the CURRENT swing's clip finishes (the next held swing waits for it).
        private float _swingEndsAt = float.NegativeInfinity;

        // HOLD — the LOCKED chain target + the press/held-edge bookkeeping (mirrors MineOre exactly).
        private MineableNodeState _chainTarget;
        private bool _freshPress;
        private bool _heldLastFrame;
        private bool _suppressUntilRepress;

        private bool _tracedFirstStrike;
        private bool _startedPool;

        /// <summary>True while a strike swing has been triggered but its EFFECT has not yet landed at impact.</summary>
        public bool ImpactPending => _impactPending;

        /// <summary>True while the CURRENT swing's clip is still playing (a held chain waits for it before the next swing).</summary>
        public bool SwingInProgress => Now < _swingEndsAt;

        /// <summary>The effective per-swing clip DURATION for the current tool-use speed.</summary>
        public float EffectiveSwingDurationSeconds => ComputeSwingDuration();

        /// <summary>True while a swing CHAIN is active (LMB held + a boulder locked as the chain target).</summary>
        public bool IsMineChainActive => _chainTarget != null;

        /// <summary>Number of mineable boulder instances tracked (the whole authored pool).</summary>
        public int NodeCount => _nodes.Count;

        /// <summary>How many pool boulders are currently ENABLED.</summary>
        public int ActiveNodeCount => activeNodeCount < 0 ? _nodes.Count : activeNodeCount;

        /// <summary>Strikes landed on a given tracked boulder (0..strikesToBreak). For per-boulder tests.</summary>
        public int StrikesOn(int index) => index >= 0 && index < _nodes.Count ? _nodes[index].Strikes : 0;

        /// <summary>True while a given tracked boulder is BROKEN (depleted, awaiting regrow). For per-boulder tests.</summary>
        public bool IsBrokenOn(int index) => index >= 0 && index < _nodes.Count && _nodes[index].Broken;

        /// <summary>True while a given tracked boulder is ENABLED.</summary>
        public bool IsNodeEnabled(int index) => index >= 0 && index < _nodes.Count && _nodes[index].Enabled;

#if UNITY_INCLUDE_TESTS
        /// <summary>TEST-ONLY deterministic clock (public seam, STRIPPED from ship builds via UNITY_INCLUDE_TESTS;
        /// mirrors MineOre.TestClock / 86camf3xe). A PlayMode test injects this fake clock + advances it a fixed
        /// step per frame — a WORKING captureDeltaTime — making the Time.time-based cadence gates AND each node's
        /// break/fade/regrow SCHEDULING deterministic + clock-INDEPENDENT while keeping the SAME shipped gate logic
        /// LIVE. Null → Time.time (production-identical).</summary>
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

        /// <summary>The scheduling/cadence clock the gates read. Time.time in the shipped IL2CPP build (the TestClock
        /// seam is compiled out — production byte-identical); a PlayMode test may override it (mirrors MineOre.Now).</summary>
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
            if (stonePileSpawner == null) stonePileSpawner = FindObjectOfType<StonePileSpawner>();
        }

        void Start() => DiscoverAndStartPool();

        // Discover the authored boulder pool (READ-only — the boulders are editor-authored into Boot.unity). Run
        // in Start (not Awake) so the serialized pool is fully present. Each boulder is its own MineableNodeState.
        // Split out of Start so an EditMode test can drive the pool + registration lifecycle synchronously (no
        // MonoBehaviour Start fires in edit mode) via InitializePoolForTest.
        private void DiscoverAndStartPool()
        {
            Transform root = boulderRoot;
            if (root == null)
            {
                var found = GameObject.Find("Boulders");
                if (found != null) root = found.transform;
            }
            _nodes.Clear();
            _nodeVisuals.Clear();
            _nodeRegistered.Clear();
            if (root != null)
            {
                int idx = 0;
                CollectBoulders(root, ref idx);
            }
            _startedPool = true;
            ApplyActiveCount();
            SyncPlacementObstacles(); // register the STANDING pool immediately (before the first player placement)
            BoulderTrace("resolver tracking " + _nodes.Count + " boulders; active=" + ActiveNodeCount);
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>TEST-ONLY (STRIPPED from ship builds via UNITY_INCLUDE_TESTS): run the Start-time pool
        /// discovery + initial registration synchronously so an EditMode test — where Start never auto-fires —
        /// can exercise the boulder placement-obstacle register/unregister/re-register lifecycle (86catr49m).</summary>
        public void InitializePoolForTest() => DiscoverAndStartPool();
#endif

        private void CollectBoulders(Transform root, ref int idx)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == BoulderNodeName)
                {
                    _nodes.Add(NewNodeState(child, DeriveSeed(idx)));
                    _nodeVisuals.Add(child);   // 86catr49m — lock-step with _nodes for placement-obstacle sync
                    _nodeRegistered.Add(false);
                    idx++;
                }
                else if (child.childCount > 0)
                {
                    CollectBoulders(child, ref idx);
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

        /// <summary>Set how many pool boulders are ENABLED (a test/inspector seam; boulders default to ALL enabled).
        /// Clamps to [0, pool size]. Enables the first N, disables the rest.</summary>
        public void SetActiveNodeCount(int count)
        {
            activeNodeCount = count;
            ApplyActiveCount();
        }

        private void ApplyActiveCount()
        {
            if (!_startedPool) return;
            int pool = _nodes.Count;
            // -1 = ALL enabled (boulders are a fixed discrete pool — no rarity dial).
            int active = activeNodeCount < 0 ? pool : (pool == 0 ? 0 : Mathf.Clamp(activeNodeCount, 0, pool));
            for (int i = 0; i < _nodes.Count; i++)
                _nodes[i].SetEnabled(i < active);
        }

        void Update()
        {
            for (int i = 0; i < _nodes.Count; i++)
                _nodes[i].Tick();

            SyncPlacementObstacles(); // 86catr49m — reflect this frame's break/regrow transitions into the registry

            if (_impactPending && Now >= _impactAt)
            {
                _impactPending = false;
                MineableNodeState t = _pendingTarget;
                _pendingTarget = null;
                ApplyStrikeEffect(t);
                if (_chainTarget != null && !_chainTarget.IsMineable)
                {
                    _chainTarget = null;
                    _suppressUntilRepress = true;
                    _swingEndsAt = float.NegativeInfinity;
                }
            }

            if (inventory == null || player == null)
            {
                _heldLastFrame = false; _chainTarget = null; _suppressUntilRepress = false; return;
            }

            bool held = _mineHeldRequested || Input.GetMouseButton(0);
            bool clickLatch = _mineClickRequested;
            _mineClickRequested = false;
            bool pressEdge = (held && !_heldLastFrame) || Input.GetMouseButtonDown(0) || clickLatch;
            _heldLastFrame = held;
            _freshPress = pressEdge;

            bool effectiveHeld = held || clickLatch;

            if (_impactPending) return;

            if (!effectiveHeld) { _chainTarget = null; _suppressUntilRepress = false; _swingEndsAt = float.NegativeInfinity; return; }

            if (_suppressUntilRepress)
            {
                if (_freshPress) _suppressUntilRepress = false;
                else return;
            }

            if (SwingInProgress) return;

            // The full mine decision REUSES the pure MineOre.ShouldMineOnClick guard (generic: inRange +
            // pickaxeSelected + the three world-click guards) — the boulder verb owns the SAME guard, not a fork.
            bool overUI = inventoryUI != null && inventoryUI.IsPointerOverUI(Input.mousePosition);
            bool rmbHeld = Input.GetMouseButton(1);
            bool pickaxeSelected = IsBoulderPickaxeSelected(inventory);

            MineableNodeState target;
            if (_freshPress || _chainTarget == null)
                target = ResolveNearestMineable(player.position);
            else
                target = (_chainTarget.IsMineable && IsInRange(player.position, _chainTarget)) ? _chainTarget : null;
            bool inRange = target != null;

            if (!MineOre.ShouldMineOnClick(inRange, pickaxeSelected, UiInputGate.CaptureWorldInput, overUI, rmbHeld))
            {
                _chainTarget = null;
                return;
            }

            if (Now - _lastStrikeAt < Mathf.Max(0f, strikeInterval)) return;
            _lastStrikeAt = Now;

            _chainTarget = target;
            BeginMineSwing(target);
        }

        /// <summary>True when a PICKAXE (wood, stone, OR iron tier) is the SELECTED belt item — the boulder analog
        /// of MineOre.IsPickaxeSelected, WIDENED to include the WOOD pickaxe (the ② entry gate: a wood pickaxe mines
        /// boulders→stone; stone/iron are strictly more capable and also mine boulders — spec §5). Not merely owned:
        /// the selected belt slot must hold a pickaxe id.</summary>
        public static bool IsBoulderPickaxeSelected(Inventory inv)
        {
            if (inv == null) return false;
            var model = inv.Model;
            if (model == null) return false;
            return model.IsSelectedBeltItem(ItemCatalog.PickaxeWoodId)
                || model.IsSelectedBeltItem(ItemCatalog.PickaxeStoneId)
                || model.IsSelectedBeltItem(ItemCatalog.PickaxeIronId);
        }

        private bool IsInRange(Vector3 from, MineableNodeState n)
        {
            if (n == null) return false;
            Vector3 p = n.Position;
            float dSq = (new Vector2(from.x, from.z) - new Vector2(p.x, p.z)).sqrMagnitude;
            return dSq <= mineRadius * mineRadius;
        }

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

        /// <summary>Request ONE mine-strike programmatically — the input-independent analog of a left-click. Latched
        /// + consumed on the next Update (mirrors MineOre.RequestMineClick). The guards still apply.</summary>
        public void RequestMineClick() => _mineClickRequested = true;

        /// <summary>Set the LMB-HELD state programmatically (the analog of MineOre.SetMineHeld). OR'd with the real
        /// button; drives the hold-repeat chain in headless PlayMode + the shipped capture.</summary>
        public void SetMineHeld(bool held) => _mineHeldRequested = held;

        /// <summary>Land one strike on the FIRST enabled boulder directly + IMMEDIATELY (swing + effect now, no
        /// impact delay), regardless of range. Public so a PlayMode swing test can drive a strike in isolation.</summary>
        public void Mine()
        {
            MineableNodeState target = FirstMineable();
            if (target == null) return;
            if (character != null) { character.FaceWorldTarget(target.Position); character.TriggerChop(); }
            ApplyStrikeEffect(target);
        }

        private MineableNodeState FirstMineable()
        {
            for (int i = 0; i < _nodes.Count; i++)
                if (_nodes[i].IsMineable) return _nodes[i];
            return null;
        }

        private void BeginMineSwing(MineableNodeState target)
        {
            if (target == null || !target.IsMineable) return;

            if (character != null)
            {
                character.FaceWorldTarget(target.Position);
                character.TriggerChop();
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
            float speed = character != null
                ? Mathf.Clamp(character.chopSpeed, CastawayCharacter.ChopSpeedMin, CastawayCharacter.ChopSpeedMax)
                : 1f;
            float liveLen = character != null ? character.MeleeClipLength : 0f;
            float authored = liveLen > 0f ? liveLen : Mathf.Max(SwingClipLengthFloor, swingClipLengthSeconds);
            return authored / Mathf.Max(0.0001f, speed);
        }

        private void ApplyStrikeEffect(MineableNodeState target)
        {
            if (target == null || !target.IsMineable) return;

            bool broke = target.LandStrike(strikesToBreak, regrowthMinSeconds, regrowthMaxSeconds);

            if (!broke) target.Shake(strikeShakeDegrees);

            // On BREAK, drop a lootable STONE PILE at the boulder's spot holding the boulder's stone yield. Null
            // spawner/inventory → breaking still completes (the boulder depletes), just no pile.
            if (broke && stonePileSpawner != null && inventory != null)
            {
                Material stoneMat = target.FirstSharedMaterial();
                StonePile pile = stonePileSpawner.SpawnAt(target.Position, inventory, stoneMat);
                BoulderTrace("BREAK -> spawned stone pile holding " + (pile != null ? pile.StoneRemaining : 0) +
                             " stone at " + target.Position.ToString("F1"));
            }

            if (!_tracedFirstStrike)
            {
                _tracedFirstStrike = true;
                BoulderTrace("impact " + target.Strikes + "/" + strikesToBreak + " (stone drops on break)" +
                             " (swing=" + (character != null) + ")");
            }
            if (broke)
                BoulderTrace("boulder BROKEN after " + target.Strikes + " strikes; regrow in " +
                             (target.RegrowAt - Now).ToString("F0") + "s");
        }

        /// <summary>
        /// 86catr49m — sync each boulder's PlacementObstacleRegistry membership to its CURRENT mineability so a
        /// crafting-table ghost reads RED over a STANDING boulder and GREEN where one has been mined away: a
        /// MINEABLE (standing) boulder registers a circular no-build zone; a broken / mid-tween / mined-away one
        /// UNREGISTERS; a regrown one RE-registers. Only acts on a transition (idempotent + allocation-free), so
        /// it is cheap to call every frame. Called from Update AND directly by EditMode tests (no MonoBehaviour
        /// lifecycle needed).
        ///
        /// WHY key on IsMineable and NOT a PlacementObstacle component (the ticket's "e.g." + #302's registry
        /// seam): Drew's #303 trace proved a boulder's break→fade→regrow cycle NEVER toggles GameObject.SetActive
        /// (MineOre.cs SetEnabled fires only from the never-used pool dial), so a PlacementObstacle's OnEnable/
        /// OnDisable would register ONCE and never unregister a mined-away boulder → it would wrongly keep
        /// blocking placement at that empty spot until regrow. IsMineable is the correct transition signal.
        /// </summary>
        public void SyncPlacementObstacles()
        {
            float radius = Mathf.Max(0f, placementObstacleRadius);
            for (int i = 0; i < _nodes.Count; i++)
            {
                Transform v = i < _nodeVisuals.Count ? _nodeVisuals[i] : null;
                bool registered = i < _nodeRegistered.Count && _nodeRegistered[i];
                if (v == null)
                {
                    if (registered) _nodeRegistered[i] = false; // visual gone — drop the stale flag (auto-pruned by the registry)
                    continue;
                }
                bool mineable = _nodes[i].IsMineable;
                if (mineable && !registered)
                {
                    PlacementObstacleRegistry.Register(v, radius);
                    _nodeRegistered[i] = true;
                }
                else if (!mineable && registered)
                {
                    PlacementObstacleRegistry.Unregister(v);
                    _nodeRegistered[i] = false;
                }
            }
        }

        void OnDisable()
        {
            // 86catr49m — release every no-build zone this pool projected so a disabled / torn-down MineBoulder
            // never leaves stale placement blockers in the STATIC registry (editor play-mode re-entry + hermetic
            // EditMode tests, where no [RuntimeInitializeOnLoadMethod] reset fires).
            for (int i = 0; i < _nodeVisuals.Count; i++)
            {
                if (i < _nodeRegistered.Count && _nodeRegistered[i] && _nodeVisuals[i] != null)
                    PlacementObstacleRegistry.Unregister(_nodeVisuals[i]);
                if (i < _nodeRegistered.Count) _nodeRegistered[i] = false;
            }
        }

        // [boulder-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call + its
        // argument evaluation from the shipped IL2CPP release exe (unity6-mastery §5/§10).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void BoulderTrace(string msg) => Debug.Log("[boulder-trace] " + msg);
    }
}
