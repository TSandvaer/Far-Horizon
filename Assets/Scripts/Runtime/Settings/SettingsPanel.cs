using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using FarHorizon.Settings;

namespace FarHorizon
{
    /// <summary>
    /// The in-game settings UI (tickets 86caa4bqp + 86cabeqj9 foundation; SPLIT into two panels by 86cah8ukr) —
    /// a UI Toolkit workbench drawer. ONE MonoBehaviour drives TWO drawers off ONE registry:
    ///   • F1 = the small PLAYER-facing Settings panel (belt slots, inventory stack size, warmth/hunger/thirst
    ///     decay on-off toggles + their decay-rate sliders — SettingsCategory.IsPlayer);
    ///   • F3 = the full DEV CONSOLE the Sponsor keeps OPEN and tweaks WHILE he plays (world-look, arm-pose,
    ///     camera/zoom, held-weapon, locomotion, resource timers/yields, inventory slots, console scale — every
    ///     row that is NOT player-facing). The console is the give-him-the-knob soak-tuning instrument; we BAKE
    ///     the dialed values as new defaults (cf. the F9 axe-nudge tool).
    /// Both drawers filter the SAME registry by <see cref="SettingsCategory"/> — "route views, don't re-bind":
    /// the registry + every Populate* binding is UNCHANGED; only the destination view differs.
    ///
    /// THE THIN VIEW. The extensible contract lives in <see cref="SettingsRegistry"/> + the typed
    /// <see cref="SettingEntry"/>s (pure C#, unit-tested in EditMode). This MonoBehaviour:
    ///   • builds the registry ONCE via <see cref="SettingsCatalog"/> from the serialized live targets (AC3);
    ///   • loads persisted values (AC5) + applies them on Start;
    ///   • builds one UI Toolkit row per entry GENERICALLY off its archetype (AC2) — a new setting needs NO
    ///     change here; it just appears as a row (routed to F1 or F3 by category), with a typed field (AC5),
    ///     nudge selection (AC6), a baked-default readout (AC8) and a differs-from-default badge (AC9) for free;
    ///   • OPENS/CLOSES the player drawer on F1 + the dev console on F3, each polled DIRECTLY (86cah8ukr). The
    ///     dev/debug IMGUI overlays are on F10 (DebugOverlayMaster, the single overlay master since the legacy
    ///     F2 DebugOverlayToggle was removed in 86cah90cp round-3; F2 is UNBOUND). Each key (F1 player / F3 dev
    ///     console / F10 overlays) reveals exactly one layer (AC1);
    ///   • CONDITIONAL VISIBILITY (86cah8ukr AC1): a per-need decay-rate slider is shown only while its need's
    ///     on/off toggle is ON (live show/hide on toggle change);
    ///   • is NON-MODAL (AC2): being open does NOT pause/gate gameplay (no Time.timeScale touch); world input
    ///     is swallowed ONLY while a typed-field holds keyboard focus (AC3 — so a typed number isn't also read
    ///     as movement), via the ref-counted UiInputGate each drawer OPTS INTO per-field, releasing on close
    ///     AND OnDisable (so world input is never left permanently swallowed).
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): the UIDocument + UXML/USS + the live-target
    /// references are wired editor-time (MovementCameraScene) + serialized into Boot.unity. The Awake
    /// fallbacks are build-safety nets, not the ship path. NO mutable statics (the StaticStateResetTests
    /// audit stays green — §Configurable Enter Play Mode).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SettingsPanel : MonoBehaviour
    {
        [Header("UI Toolkit assets (wired editor-time, serialized)")]
        [Tooltip("The UIDocument hosting the panel. Auto-resolved from this GameObject if unset.")]
        public UIDocument document;
        [Tooltip("The panel shell UXML (SettingsPanel.uxml). Wired editor-time so it serializes.")]
        public VisualTreeAsset panelUxml;
        [Tooltip("The shared carved-wood palette (Palette.uss).")]
        public StyleSheet paletteUss;
        [Tooltip("The settings-panel styling (SettingsPanel.uss).")]
        public StyleSheet panelUss;

        [Header("Live targets the settings bind to (AC3)")]
        [Tooltip("The orbit camera — zoom range (min/maxDistance) + view-angle range (min/maxPitch) bind here.")]
        public OrbitCamera orbit;
        [Tooltip("The WASD locomotion — walk speed (+ run speed) bind here.")]
        public WasdMovement wasd;
        [Tooltip("The thirst need (86caamkv7) — thirst decay rate + water scoop amount bind here (AC5). " +
                 "May be null; the thirst rows then simply don't appear.")]
        public ThirstNeed thirst;
        [Tooltip("The hunger need (86cabd75y) — hunger decay rate + berry restore amount bind here. " +
                 "May be null; the hunger rows then simply don't appear.")]
        public HungerNeed hunger;
        [Tooltip("The warmth need (86cabeqwf/86cah8ukr) — the warmth on/off toggle + warmth decay-rate slider " +
                 "bind here (the PLAYER-facing F1 rows). The hunger/thirst on/off toggles bind to those needs " +
                 "above. May be null; the warmth rows then simply don't appear.")]
        public WarmthNeed warmth;
        [Tooltip("The castaway (86caa4c5c change-(b)) — tool-use speed flips the reserved row live to its " +
                 "chopSpeed (the Mixamo melee Attack-state playback rate). May be null; the tool-use-speed row " +
                 "then stays greyed (extension hook).")]
        public CastawayCharacter chopCharacter;
        [Tooltip("The chop tree (86caa4c5c) — tree regrowth time binds to its regrow min/max range. May be " +
                 "null; the regrowth row then simply doesn't appear.")]
        public ChopTree chopTree;
        [Tooltip("The shared stone respawn config (86caa4c96) — stone respawn time binds to its respawn " +
                 "min/max range. May be null; the stone-respawn row then simply doesn't appear.")]
        public StoneRespawner stoneRespawner;
        [Tooltip("The shared log-pile spawner (REWORK 86caf9u5t) — `tree-chop wood yield` + `log-pile despawn` " +
                 "bind to it; `chops-to-fell` binds to the chop tree above. May be null; the yield/despawn rows " +
                 "then simply don't appear.")]
        public LogPileSpawner logPileSpawner;
        [Tooltip("The held-weapon placement seam (86caffwuz) — the 7 held-weapon in-hand rows (pos X/Y/Z, " +
                 "rot pitch/yaw/roll, scale) bind to the CURRENTLY-held weapon's seat through it. May be null; " +
                 "the held-weapon rows then simply don't appear.")]
        public HeldWeaponPlacement heldWeapon;
        [Tooltip("Every berry bush (86cabn67w) — the `Berry regrowth time` RANGE row fans out across ALL of " +
                 "them (each BerryBush holds its OWN regrow window, unlike the shared ChopTree). Resolved in " +
                 "Awake via FindObjectsByType (a startup Find, not per-frame). Null/empty → the berry row " +
                 "simply doesn't appear.")]
        public BerryBush[] berryBushes;
        [Tooltip("The inventory façade (86cabfa4e) — `inventory slots` + `belt slots` + `inventory stack size` " +
                 "bind through it (the #90 AC1/AC2/AC7 settings-registration follow-up). Slot-count changes " +
                 "rebuild the model (dev re-size). May be null; the inventory rows then simply don't appear.")]
        public Inventory inventory;

        [Header("F-key migration targets (86caber95 — legacy F7/F9/F10 dials → console rows)")]
        [Tooltip("The castaway arm-pose driver (86caber95 AC1 — F9) — the per-axis arm-pose + run-lower euler rows " +
                 "bind to its rightArmEuler / leftArmEuler / runLowerEuler. May be null; those rows then don't appear.")]
        public CastawayArmPose armPose;
        [Tooltip("The world-look seam (86caber95 AC2 — F10) — the fog/sky/cloud/mountain/sun rows bind through it " +
                 "(it resolves the same RenderSettings/skybox-material/vista handles the F10 tool dials). May be " +
                 "null; the world-look rows then simply don't appear. (OrbitCamera follow gains bind to `orbit`; " +
                 "ground-Y binds to `chopCharacter`.)")]
        public WorldLookTunables worldLook;

        [Tooltip("Combat POC 86cah7xxp AC8b — the player Health / HealthRegen / DeathHandler the per-tier " +
                 "difficulty rows (HP max / Damage taken / HP regen rate / Death behavior) bind to. Wired " +
                 "editor-time; any null skips only its own rows (PopulateCombat null-refs safely).")]
        public FarHorizon.Combat.Health combatHealth;
        public FarHorizon.Combat.HealthRegen combatRegen;
        public FarHorizon.Combat.DeathHandler combatDeath;

        [Tooltip("The FPS counter HUD (86cahmxmt) — the `FPS counter` on/off row drives its enabled flag " +
                 "(OFF = no Update, no OnGUI = zero cost). Wired editor-time (the Boot object carries it next " +
                 "to BootHud); the Awake FindObjectOfType stays the bare-scene safety net. May be null; the " +
                 "row then simply doesn't appear.")]
        public FarHorizon.FpsCounterHud fpsHud;

        [Header("Toggle keys (86cah8ukr — F1 player Settings / F3 dev console)")]
        [Tooltip("Key that opens/closes the PLAYER-facing Settings panel (F1) — belt slots, inventory stack " +
                 "size, warmth/hunger/thirst on-off + decay-rate sliders (SettingsCategory.IsPlayer). The panel " +
                 "SPLIT (86cah8ukr): F1 now opens ONLY the small player view; the full dev console moved to F3 " +
                 "(devToggleKey). Layout-agnostic + Danish-safe (an F-key) + verified non-clashing with " +
                 "WASD/Shift/Space/Tab/F7-F10 (F2 UNBOUND) ([[sponsor-danish-keyboard-layout]]). Update polls " +
                 "this directly (SetPlayerOpen(!IsPlayerOpen)). DEFAULT = None (NOT F1): the shipped F1 comes ONLY " +
                 "from MovementCameraScene.BuildSettingsPanel's editor-time assignment, so the scene-presence guard " +
                 "goes genuinely RED if that wiring is dropped (a code default of F1 made the guard tautological). A " +
                 "never-wired panel then just no-ops on Input.GetKeyDown(None) — no crash.")]
        public KeyCode toggleKey = KeyCode.None;
        [Tooltip("Key that opens/closes the DEV CONSOLE (F3, Sponsor-confirmed 2026-07-03) — EVERY other row " +
                 "(world-look, arm-pose, camera/zoom, held-weapon, locomotion incl. walk/run speed, resource " +
                 "timers/yields, inventory slots, console UI + text scale). The dev/debug IMGUI overlays are on " +
                 "F10 (DebugOverlayMaster, the single overlay master since the legacy F2 DebugOverlayToggle was " +
                 "removed in 86cah90cp round-3; F2 is UNBOUND) — F1/F3/F10 are all distinct. Update polls this " +
                 "directly (SetOpen(!IsOpen)). The SHIPPED-BUILD verify-capture drives SetOpen PROGRAMMATICALLY " +
                 "(it can't synthesize a key-down in a windowed capture) — it reads this field only to LOG the " +
                 "dev key, not to open.) DEFAULT = None (NOT F3): the shipped F3 comes ONLY from " +
                 "MovementCameraScene.BuildSettingsPanel's editor-time assignment, so the guard goes genuinely RED " +
                 "if that wiring is dropped (a code default of F3 made it tautological).")]
        public KeyCode devToggleKey = KeyCode.None;

        /// <summary>The registry this panel renders + drives. Built ONCE on Start from the catalog; both the F1
        /// player view and the F3 dev view filter this SAME registry by <see cref="SettingsCategory"/> (public
        /// for tests). "Route views, don't re-bind" — one build, two filtered views.</summary>
        public SettingsRegistry Registry { get; private set; }

        /// <summary>Whether the DEV CONSOLE (F3) is open. Kept named <c>IsOpen</c> (not <c>IsDevOpen</c>) so the
        /// shipped-build capture (SettingsVerifyCapture) + its gate keep driving the console via the unchanged
        /// SetOpen/IsOpen surface. Other systems gate their input on this (research §E1).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Whether the PLAYER Settings panel (F1) is open (86cah8ukr). Independent of <see cref="IsOpen"/>
        /// — the two drawers open/close independently.</summary>
        public bool IsPlayerOpen { get; private set; }

        // --- DEV console drawer (F3) — owns the corner-picker + UI/text scale (its 86cabeqj9 NITs). ---
        private VisualElement _scrim;          // the dev full-screen scrim (the show/hide target — display:None)
        private VisualElement _panel;          // the dev corner-parked column (the transition + scale target)
        private ScrollView _rows;              // the dev rows (SettingsCategory.IsDev entries)
        // --- PLAYER Settings drawer (F1, 86cah8ukr) — the small player-facing view; parked TOP-RIGHT (fixed) so
        //     it never overlaps the dev console's default TOP-LEFT corner if both happen to be open. ---
        private VisualElement _playerScrim;
        private VisualElement _playerPanel;
        private ScrollView _playerRows;        // the player rows (SettingsCategory.IsPlayer entries)
        private const ConsoleCorner PlayerCorner = ConsoleCorner.TopRight;
        // Every row keyed by its entry id, so the conditional-visibility pass (a decay slider shown only while
        // its toggle is ON — 86cah8ukr AC1) can show/hide a row by id live. Populated in BuildRows.
        private readonly Dictionary<string, VisualElement> _rowsById = new Dictionary<string, VisualElement>();
        private bool _built;
        private bool _gateTracked;             // whether THIS panel currently holds the UiInputGate open (FOCUS-gate, AC3)
        // 86cah8ukr FIX — keyboard-focus counts PER DRAWER (not one shared counter). The world-input gate is
        // RE-DERIVED from these two + each drawer's open state (RefreshInputGate), so closing ONE drawer can no
        // longer release the gate the OTHER drawer's still-focused field owns (the shared-single-counter
        // force-clear bug: open F1 → focus a numeric field → open+close F3 → world input stopped being swallowed
        // while the F1 field stayed focused → typed digits/arrows ALSO drove movement/orbit).
        private int _devFocusedFields;         // typed-fields holding keyboard focus in the DEV console (F3) drawer
        private int _playerFocusedFields;      // typed-fields holding keyboard focus in the PLAYER Settings (F1) drawer
        // 86cah8ukr FIX4 (adversarial #247 verify) — the scroll-zoom POINTER gate (UiInputGate.PointerOverConsole)
        // had the SAME shared-single-flag defect the focus gate above did: it was a bare global bool that OpenDrawer
        // force-cleared UNCONDITIONALLY on any drawer close. So: open F1+F3 overlapping under the cursor, close F3 →
        // the flag cleared while F1 stayed hovered, and UI Toolkit does NOT re-fire PointerEnter for a pointer
        // already inside → the wheel zoomed the OrbitCamera THROUGH the open F1 panel until a leave+re-enter. Fixed
        // the SAME way: track pointer-over PER DRAWER + re-derive the gate from which drawers are still open + hovered
        // (RefreshPointerGate mirrors RefreshInputGate).
        private bool _devPointerOver;          // pointer currently over the DEV console (F3) panel rect
        private bool _playerPointerOver;       // pointer currently over the PLAYER Settings (F1) panel rect
        private ConsoleCorner _corner;         // the persisted panel corner (AC4)
        private float _uiScale = 1f;           // the persisted console UI scale multiplier (86cabeqj9 soak NIT)
        private float _textScale = 1f;         // the persisted UI TEXT scale multiplier (86cabeqj9 soak NIT — DISTINCT from _uiScale chrome)
        // Every panel text element paired with its BASE font px (the USS-authored size). ApplyTextScale sets
        // each element's inline fontSize = base * _textScale, so the "UI text scale" slider resizes ALL panel
        // text LIVE, independently of the chrome-scaling _uiScale. An inline fontSize wins over the USS selector.
        private readonly System.Collections.Generic.List<TextEl> _textEls = new System.Collections.Generic.List<TextEl>();
        private struct TextEl { public VisualElement El; public float BasePx; }
        // 86cagvvhv (#208 cosmetic NIT) — the fixed-width value columns (the dim "def …" hint + the current-value
        // readout) carry USS px widths that DON'T scale with the font, so at the 2.0x UI-text-scale cap the doubled
        // glyphs overflow their column and the default-hint runs into the readout (unreadable). We track each such
        // column with its BASE width px and scale that width in lockstep with _textScale in ApplyTextScale, so the
        // hint + readout stay non-overlapping across the whole 1.0x→2.0x band. Only the INNER column widths change;
        // the panel's corner-parking/position (AC4) is unaffected (that's the scrim's justify/align, not these).
        private readonly System.Collections.Generic.List<WidthEl> _widthEls = new System.Collections.Generic.List<WidthEl>();
        private struct WidthEl { public VisualElement El; public float BasePx; }
        private SettingEntry _active;          // the focused/selected entry the nudge keys drive (AC6, one at a time)
        private readonly List<RowHandle> _handles = new List<RowHandle>(); // per-row repaint + active-highlight (AC8/AC9/AC10)

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (orbit == null) orbit = FindObjectOfType<OrbitCamera>();
            if (wasd == null) wasd = FindObjectOfType<WasdMovement>();
            if (thirst == null) thirst = FindObjectOfType<ThirstNeed>();
            if (hunger == null) hunger = FindObjectOfType<HungerNeed>();
            if (warmth == null) warmth = FindObjectOfType<WarmthNeed>();
            if (chopCharacter == null) chopCharacter = FindObjectOfType<CastawayCharacter>();
            if (chopTree == null) chopTree = FindObjectOfType<ChopTree>();
            if (stoneRespawner == null) stoneRespawner = FindObjectOfType<StoneRespawner>();
            if (logPileSpawner == null) logPileSpawner = FindObjectOfType<LogPileSpawner>();
            if (heldWeapon == null) heldWeapon = FindObjectOfType<HeldWeaponPlacement>();
            // Berry-regrowth fans out across EVERY bush (each holds its own regrow window — no shared manager,
            // unlike the ChopTree). Resolve the full set once at startup (a bake-time/startup Find, not per-frame
            // — unity6-mastery §6). The serialized array (if wired editor-time) wins; this fills it only if empty.
            if (berryBushes == null || berryBushes.Length == 0)
                berryBushes = FindObjectsByType<BerryBush>(FindObjectsSortMode.InstanceID);
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            // F-key migration seams (86caber95) — Awake build-safety fallbacks (the ship path wires them
            // editor-time). armPose drives the F9 arm rows; worldLook the F10 rows.
            if (armPose == null) armPose = FindObjectOfType<CastawayArmPose>();
            if (worldLook == null) worldLook = FindObjectOfType<WorldLookTunables>();
            // Combat POC 86cah7xxp AC8b — the per-tier difficulty rows' targets. Serialized editor-time; these
            // Awake fallbacks are the build-safety net (any null simply skips its own combat rows).
            if (combatHealth == null) combatHealth = FindObjectOfType<FarHorizon.Combat.Health>();
            if (combatRegen == null) combatRegen = FindObjectOfType<FarHorizon.Combat.HealthRegen>();
            if (combatDeath == null) combatDeath = FindObjectOfType<FarHorizon.Combat.DeathHandler>();
            // FPS counter (86cahmxmt) — build-safety fallback (the ship path wires it editor-time). Finds a
            // DISABLED component too (FindObjectOfType filters by GameObject activeness, not component enabled
            // state), so a persisted OFF pref still re-binds + re-applies on the next launch.
            if (fpsHud == null) fpsHud = FindObjectOfType<FpsCounterHud>();
        }

        void Start()
        {
            // Build the registry from the live targets (AC3), load persisted soak tweaks (AC5), apply them.
            // The thirst overload (86caamkv7 AC5) adds the thirst decay rate + water scoop amount rows; the
            // chop overload (86caa4c5c) flips tool-use speed live + adds the tree regrowth time range row; the
            // hunger overload (86cabd75y) adds the hunger decay rate + berry restore amount rows; the berry
            // overload (86cabn67w) adds the `Berry regrowth time` range row fanning out across every bush; the
            // inventory overload (86cabfa4e) adds `inventory slots` + `belt slots` + `inventory stack size`.
            // The 12-arg overload appends the per-need on/off toggles + warmth decay-rate slider (86cabeqwf,
            // folded into the F1/F3 split 86cah8ukr) — warmth/hunger/thirst decay ON/OFF + `warmth_decay_rate`.
            Registry = SettingsCatalog.Build(orbit, wasd, thirst, chopCharacter, chopTree, stoneRespawner, logPileSpawner, heldWeapon, hunger, berryBushes, inventory, warmth);

            // F-KEY MIGRATION (86caber95) — fold the standalone F7/F9/F10 live-tune dials into the console as
            // rows (AC1/AC2/AC3), each Tab-cycled target its own row; vectors decomposed per-axis (AC4). These
            // register AFTER Build (the per-feature Populate de-collision precedent) so they append below the
            // existing rows. The legacy F-key panels stay LIVE in parallel until soak-confirmed (AC5).
            SettingsCatalog.PopulateCameraFollow(Registry, orbit);            // F7 → OrbitCamera follow gains (AC3)
            SettingsCatalog.PopulateArmAndGround(Registry, chopCharacter, armPose); // F9 → ground-Y + arm pose (AC1)
            SettingsCatalog.PopulateWorldLook(Registry, worldLook);          // F10 → sky/fog/cloud/mountain/sun (AC2)
            SettingsCatalog.PopulateCombat(Registry, combatHealth, combatRegen, combatDeath); // Combat POC → per-tier HP/damage/regen/death (AC8b)
            SettingsCatalog.PopulateFps(Registry, fpsHud);                   // FPS counter on/off (86cahmxmt — default ON, Sponsor-soak tunes)
            SettingsCatalog.PopulateIron(Registry);                          // iron-progression dials (86cakkmgw — extension hooks; I-2/I-3 flip live)

            // 86cabeqj9 soak NIT — CONSOLE UI SCALE. A FloatSettingEntry the PANEL itself registers (not the
            // catalog: it binds to this panel's own UI scale, a pure-UI concern the catalog has no game target
            // for). It flows through the SAME registry → row → persist → reset machinery as every other entry,
            // so it gets a slider, typed field, baked-default readout, differs badge and reset-to-defaults for
            // free. SetValue drives the field + applies the panel scale LIVE (null-safe before BuildView; the
            // open path re-applies once _panel exists). Default captured at registration = 1.0x (untouched =
            // byte-identical shipped panel, badge off).
            Registry.AddFloat(SettingsCatalog.ConsoleUiScaleId, "Console UI scale",
                () => _uiScale,
                v => { _uiScale = v; ApplyConsoleScale(); },
                SettingsCatalog.ConsoleUiScaleMin, SettingsCatalog.ConsoleUiScaleMax, unit: "x");

            // 86cabeqj9 soak NIT — UI TEXT SCALE. DISTINCT from Console UI scale above: this scales only the
            // panel FONT size (every label / readout / field / badge / title), NOT the chrome transform. Flows
            // through the SAME registry → row → persist → reset machinery, so it gets a slider, typed field,
            // baked-default readout, differs badge and reset-to-defaults for free. Default 1.0x = shipped fonts.
            Registry.AddFloat(SettingsCatalog.ConsoleTextScaleId, "UI text scale",
                () => _textScale,
                v => { _textScale = v; ApplyTextScale(); },
                SettingsCatalog.ConsoleTextScaleMin, SettingsCatalog.ConsoleTextScaleMax, unit: "x");

            Registry.LoadAll();   // survives a relaunch
            Registry.ApplyAll();  // drive the live params with the loaded values on startup

            BuildView();
            ApplyConsoleScale();  // apply the loaded UI scale now that _panel exists (86cabeqj9 soak NIT)
            ApplyTextScale();     // apply the loaded TEXT scale now that the rows exist (86cabeqj9 soak NIT)
            SetOpen(false);       // dev console starts hidden — F3 opens it
            SetPlayerOpen(false); // player Settings starts hidden — F1 opens it (86cah8ukr split)
        }

        void Update()
        {
            // AC1 + 86cabeqj9 F1/overlay DE-CONFLICT (soak NIT). The console KEEPS F1, but now polls F1 DIRECTLY
            // rather than riding the shared DebugOverlays.Visible flag. Before this, the console synced to
            // DebugOverlays.Visible — so one F1 popped the console AND the dev/debug IMGUI overlays (axe-shaft
            // length, pond recess/foam) together (the Sponsor's complaint).
            // Decoupled: F1 toggles ONLY the console here; the dev/debug overlays are on F10 (the single overlay
            // master since the legacy F2 DebugOverlayToggle was removed in 86cah90cp round-3; F2 is UNBOUND).
            // Layout-agnostic + Danish-safe (an F-key) and verified non-clashing with WASD/Shift/Space/Tab/F7-F10
            // ([[sponsor-danish-keyboard-layout]]). Legacy Input (activeInputHandler=0), like every debug toggle.
            // 86cah8ukr SPLIT — F1 toggles the PLAYER Settings drawer; F3 toggles the DEV console. Each polled
            // DIRECTLY (legacy Input, activeInputHandler=0). The dev console keeps the unchanged SetOpen/IsOpen
            // surface (the shipped-build capture drives it); the player drawer uses SetPlayerOpen/IsPlayerOpen.
            if (Input.GetKeyDown(toggleKey)) SetPlayerOpen(!IsPlayerOpen);
            if (Input.GetKeyDown(devToggleKey)) SetOpen(!IsOpen);

            // AC6 — NUDGE the focused/selected entry. Only while EITHER drawer is open AND no typed-field holds
            // keyboard focus (so the nudge keys don't fight a value being typed). PageUp/PageDown are the carried
            // nudge-tool idiom — Danish-keyboard-safe + NOT a locomotion key (WASD/arrows/Shift/Space), so they
            // act without stealing focus. Shift = 5x / Ctrl = 0.2x step (the exact WorldLookNudgeTool convention).
            if ((IsOpen || IsPlayerOpen) && _devFocusedFields + _playerFocusedFields == 0 && _active != null)
            {
                int dir = 0;
                if (Input.GetKeyDown(KeyCode.PageUp)) dir = 1;
                else if (Input.GetKeyDown(KeyCode.PageDown)) dir = -1;
                if (dir != 0) NudgeActive(dir);
            }
        }

        // Safety net: if the panel is disabled/destroyed while a field still held focus, release the input gate
        // so world input can never be left permanently swallowed (mirrors OrbitCamera's OnDisable guard). The
        // gate is the FOCUS gate now (AC3) — the open panel itself no longer gates (AC2), only a focused field.
        void OnDisable()
        {
            _devFocusedFields = 0;
            _playerFocusedFields = 0;
            // FIX4 — also clear pointer-over so a disable/destroy WHILE the cursor is over a drawer can never leave
            // the scroll-zoom gate stuck true (mirrors the focus-gate release; a destroyed panel can't be hovered).
            _devPointerOver = false;
            _playerPointerOver = false;
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            UiInputGate.SetPointerOverConsole(false);
        }

        /// <summary>Open/close the DEV CONSOLE (F3). Kept as the public <c>SetOpen(bool)</c> surface (unchanged
        /// signature) so the shipped-build capture (SettingsVerifyCapture) + its gate keep driving the console.
        /// NON-MODAL: opening NO LONGER swallows world/locomotion input (#83 was modal) and NEVER touches
        /// Time.timeScale — WASD/run/jump/orbit stay live so the Sponsor tweaks WHILE he plays.</summary>
        public void SetOpen(bool open) => OpenDrawer(open, isDev: true);

        /// <summary>Open/close the PLAYER Settings drawer (F1, 86cah8ukr split). Same non-modal focus-gate model
        /// as the dev console (world input swallowed ONLY while a typed field holds focus, released on close).
        /// Parked TOP-RIGHT (fixed) so it never overlaps the dev console's default corner.</summary>
        public void SetPlayerOpen(bool open) => OpenDrawer(open, isDev: false);

        /// <summary>Shared open/close for either drawer (86cah8ukr). <paramref name="isDev"/> selects the DEV
        /// console (corner-picker-parked + UI/text-scaled — its 86cabeqj9 NITs) vs the PLAYER Settings drawer
        /// (fixed TOP-RIGHT corner, no chrome scale). Hide via display:None (zero render cost — unity6-mastery
        /// §9), then play the open transition on the now-laid-out panel.</summary>
        private void OpenDrawer(bool open, bool isDev)
        {
            var scrim = isDev ? _scrim : _playerScrim;
            var panel = isDev ? _panel : _playerPanel;
            if (isDev) IsOpen = open; else IsPlayerOpen = open;

            // AC3 (86cah8ukr FIX / FIX4) — when CLOSING, clear ONLY the CLOSING drawer's focus count AND pointer-over
            // flag: once display:None its fields can no longer be visibly focused nor its rect hovered, and UI Toolkit
            // does not reliably fire FocusOutEvent OR PointerLeaveEvent on a display:None ancestor, so we can't rely on
            // the blur/leave callbacks to zero them. Then RE-DERIVE BOTH gates from the remaining per-drawer state:
            // closing one drawer must NOT release the gate the OTHER still-open drawer's focused field / hovered rect
            // owns. (The old shared single focus-counter + single pointer-bool zeroed BOTH drawers on any close → open
            // F1, focus a field / hover it, open+close F3 stopped swallowing world input / scroll-zoom while F1 stayed
            // focused/hovered.) Both still err toward RELEASE for the true all-closed / all-blurred / all-unhovered
            // case (the Refresh*Gate re-derives gate iff a focused field / hovered rect lives in a still-OPEN drawer).
            if (!open)
            {
                if (isDev) { _devFocusedFields = 0; _devPointerOver = false; }
                else       { _playerFocusedFields = 0; _playerPointerOver = false; }
                // 86cak0uq6 — clear the NUDGE selection (_active) if it belonged to the CLOSING drawer: the third
                // shared single-state of the FIX1 focus / FIX4 pointer class. A row hidden by display:None can no
                // longer be the visible nudge target, and PageUp/PageDown while the OTHER drawer is open must not
                // nudge (nor keep the --active outline on) an entry from the now-closed drawer. Per-drawer like the
                // gates: closing one drawer must NOT drop a selection the OTHER still-open drawer owns — so clear
                // ONLY when _active lives in the closing view (IsPlayer(id) == this is the player drawer). SetActive
                // repaints every row so the stale setting-row--active class clears too.
                if (_active != null && SettingsCategory.IsPlayer(_active.Id) == !isDev)
                    SetActive(null);
                RefreshInputGate();
                RefreshPointerGate();
            }
            if (scrim == null) return;
            scrim.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                // Park + (dev only) scale BEFORE the open transition. Dev uses the persisted corner + chrome
                // scale (AC4 + 86cabeqj9 NITs); player uses its fixed TOP-RIGHT corner (no chrome scale).
                if (isDev) { ApplyConsoleScale(); ApplyTextScale(); ConsolePosition.Apply(scrim, _corner); }
                else { ApplyTextScale(); ConsolePosition.Apply(scrim, PlayerCorner); }
                // Snappy slide-up + fade-in (the USS transition is on .settings-panel). Set the start state
                // then the end state next layout so the transition plays.
                if (panel != null)
                {
                    panel.style.translate = new Translate(0, 16, 0);
                    panel.style.opacity = 0f;
                    panel.schedule.Execute(() =>
                    {
                        panel.style.translate = new Translate(0, 0, 0);
                        panel.style.opacity = 1f;
                    }).StartingIn(0);
                }
                RefreshReadouts();
            }
        }

        /// <summary>86cabeqj9 soak NIT — apply the console UI scale to the panel element's transform.scale, so
        /// the whole console (the walnut plate + every row + all text) resizes together. Scales <see cref="_panel"/>
        /// (NOT the full-screen scrim) so the corner-parking math (which sets the scrim's flex justify/align) is
        /// untouched. Per unity6-mastery §9 we animate transform SCALE, never width/height. Null-safe — a no-op
        /// before BuildView (the open path + Start re-apply once _panel exists). Idempotent.</summary>
        private void ApplyConsoleScale()
        {
            if (_panel == null) return;
            _panel.style.scale = new Scale(new Vector2(_uiScale, _uiScale));
        }

        /// <summary>Register a panel text element + its BASE font px so <see cref="ApplyTextScale"/> can resize
        /// it live (86cabeqj9 soak NIT). Called as each text element is built.</summary>
        private void RegisterText(VisualElement el, float basePx)
        {
            if (el == null) return;
            _textEls.Add(new TextEl { El = el, BasePx = basePx });
            el.style.fontSize = basePx * _textScale;   // apply the current scale immediately
        }

        /// <summary>86cagvvhv (#208 cosmetic NIT) — register a fixed-width VALUE COLUMN (the dim "def …" hint or
        /// the current-value readout) + its BASE width px, so <see cref="ApplyTextScale"/> can grow the column in
        /// lockstep with the font (else the doubled 2.0x glyphs overflow the fixed px column and the hint overlaps
        /// the readout). Called as each such column is built. Sets the current-scaled width immediately.</summary>
        private void RegisterScaledWidth(VisualElement el, float baseWidthPx)
        {
            if (el == null) return;
            _widthEls.Add(new WidthEl { El = el, BasePx = baseWidthPx });
            el.style.width = baseWidthPx * _textScale;   // apply the current scale immediately
        }

        /// <summary>86cabeqj9 soak NIT — apply the UI TEXT scale to every registered panel text element
        /// (fontSize = base * _textScale). DISTINCT from ApplyConsoleScale (chrome transform): this resizes
        /// the FONT only, so the Sponsor makes text bigger/smaller independently. Inline fontSize wins over
        /// the USS selector. Idempotent + null-safe (a no-op before the rows/chrome are built). 86cagvvhv —
        /// ALSO grows the fixed-width value columns (default hint + readouts) so they never overlap at 2.0x.</summary>
        private void ApplyTextScale()
        {
            for (int i = 0; i < _textEls.Count; i++)
                if (_textEls[i].El != null) _textEls[i].El.style.fontSize = _textEls[i].BasePx * _textScale;
            // 86cagvvhv — scale the value-column WIDTHS with the font so the "def …" hint + readout stay
            // non-overlapping across the whole 1.0x→2.0x band (not just at 1.0x).
            for (int i = 0; i < _widthEls.Count; i++)
                if (_widthEls[i].El != null) _widthEls[i].El.style.width = _widthEls[i].BasePx * _textScale;
        }

        // AC6 — apply one nudge step to the active entry, scaled by Shift(5x)/Ctrl(0.2x). Drives the entry's
        // single-write authority (so it applies live + persists + the badge updates), then repaints that row.
        private void NudgeActive(int dir)
        {
            if (_active == null || !_active.Available) return;
            float mul = NudgeStepMul();
            switch (_active.Kind)
            {
                case SettingEntry.Archetype.Slider:
                {
                    var e = (FloatSettingEntry)_active;
                    e.SetValue(e.Value + dir * NudgeStep.ForSlider(e) * mul);
                    break;
                }
                case SettingEntry.Archetype.Range:
                {
                    // Nudge MOVES THE WHOLE WINDOW (both ends by the step) — the most useful single-knob nudge
                    // for a range; fine-tuning a single end stays the slider-drag affordance.
                    var e = (RangeSettingEntry)_active;
                    float step = NudgeStep.ForRange(e) * mul * dir;
                    e.SetMin(e.MinValue + step);
                    e.SetMax(e.MaxValue + step);
                    break;
                }
                case SettingEntry.Archetype.Stepper:
                {
                    var e = (IntSettingEntry)_active;
                    // Step modifiers scale the int step (Shift=5x, Ctrl=0.2x → at least 1) — shared formula.
                    e.SetValue(e.Value + dir * NudgeStep.ForStepper(e, mul));
                    break;
                }
                case SettingEntry.Archetype.Toggle:
                {
                    // A bool nudges as 0/1: up → on, down → off (the generic numeric bridge).
                    var e = (BoolSettingEntry)_active;
                    e.SetFromNumeric(dir > 0 ? 1f : 0f);
                    break;
                }
            }
            RefreshRow(_active);
            ApplyConditionalVisibility();   // 86cah8ukr AC1 — a nudged need toggle may show/hide its decay slider
        }

        // The MODIFIER multiply (Shift=5x / Ctrl=0.2x) reads live Input — stays here (not headless-testable).
        // The base STEP SIZE per archetype is the shared NudgeStep formula (86cagpk72 NIT — single source; the
        // test asserts the SAME formula the panel uses).
        private static float NudgeStepMul()
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 0.2f;
            return 1f;
        }

        // ---- View construction (built once; rows come from the registry, generically) -------------------

        private void BuildView()
        {
            if (_built || document == null) return;
            var root = document.rootVisualElement;
            if (root == null) return;

            if (paletteUss != null) root.styleSheets.Add(paletteUss);
            if (panelUss != null) root.styleSheets.Add(panelUss);

            // 86cah8ukr SPLIT — TWO drawers from the SAME shell UXML: ONE registry, two filtered views ("route
            // views, don't re-bind"). Each shell is cloned into its OWN scoped container so the duplicate element
            // names (settings-scrim / -panel / -rows) resolve PER-DRAWER (a root-level Q would bind only the
            // first clone). doc.visualTreeAsset stays UNASSIGNED — the panel owns the clones (the #83 double-clone
            // guard: SettingsPanelSceneTests.BootScene_UIDocument_HasNoVisualTreeAsset still holds; panelUxml is
            // cloned HERE, now twice).

            // DEV console drawer (F3) — resolves into _scrim/_panel/_rows (the UNCHANGED capture surface). Owns
            // the corner-picker + UI/text scale (its 86cabeqj9 NITs).
            var devContainer = new VisualElement { name = "dev-drawer-root" };
            MakeDrawerOverlay(devContainer);   // #247 — full-screen containing block so the scrim + rows resolve
            root.Add(devContainer);
            CloneShell(devContainer);
            _scrim = devContainer.Q<VisualElement>("settings-scrim");
            _panel = devContainer.Q<VisualElement>("settings-panel");
            _rows = devContainer.Q<ScrollView>("settings-rows");
            SetupDrawerCommon(devContainer, _panel, _rows, "Dev console", isDev: true);
            // AC4 — the corner POSITION PICKER (dev console only). Cycles TL→TR→BL→BR; persists; re-parks live.
            _corner = ConsolePosition.Load();
            BuildCornerPicker(devContainer);

            // PLAYER Settings drawer (F1) — resolves into _playerScrim/_playerPanel/_playerRows. Fixed TOP-RIGHT
            // corner (PlayerCorner), no corner-picker + no chrome scale (a lean player-facing view).
            var playerContainer = new VisualElement { name = "player-drawer-root" };
            MakeDrawerOverlay(playerContainer);   // #247 — full-screen containing block so the scrim + rows resolve
            root.Add(playerContainer);
            CloneShell(playerContainer);
            _playerScrim = playerContainer.Q<VisualElement>("settings-scrim");
            _playerPanel = playerContainer.Q<VisualElement>("settings-panel");
            _playerRows = playerContainer.Q<ScrollView>("settings-rows");
            SetupDrawerCommon(playerContainer, _playerPanel, _playerRows, "Settings", isDev: false);

            BuildRows();
            _built = true;
        }

        // Clone the shell UXML into a scoped container (or the code build-safety net if the asset didn't serialize —
        // BuildShellInCode adds into the given container, not root, so the two drawers stay isolated).
        private void CloneShell(VisualElement container)
        {
            if (panelUxml != null) panelUxml.CloneTree(container);
            else BuildShellInCode(container);
        }

        /// <summary>#247 EMPTY-DRAWERS FIX — make a drawer's scoped container a FULL-SCREEN overlay so the scrim
        /// (position:absolute inset-0) + the panel (max-height:70%) + the rows ScrollView (flex-grow:1) resolve
        /// against a DEFINITE-height containing block. THE BUG: the 86cah8ukr split wraps each shell clone in a
        /// scoped container (so the duplicate element names resolve per-drawer). A plain VisualElement here has
        /// auto height = 0 — its ONLY child, the scrim, is position:absolute → OUT of flow → the container has
        /// ZERO in-flow content → height 0. The scrim's inset-0 + the panel's percentage max-height then resolve
        /// against a 0-height block, so the flex-grow ScrollView gets ZERO extra to grow into and COLLAPSES: the
        /// panel renders its intrinsic-height header + footer but NO rows (registry/handles/live-drive all fine —
        /// the DATA layer built 70 settings; only the VISUAL layer collapsed). The pre-split panel cloned the
        /// scrim STRAIGHT into root, whose definite full-screen height made this work; the intermediate container
        /// silently broke that chain. FIX: give the container root's own full-screen box (absolute, inset-0) — an
        /// overlay identical in size to root, so the scrim/panel/ScrollView resolve exactly as pre-split.
        /// pickingMode = Ignore so the always-present full-screen overlay never eats gameplay MOUSE input while
        /// its scrim is display:None (closed) — picking descends to the scrim's children when it IS open, so the
        /// open/dim/scroll-gate behaviour is byte-identical to pre-split (the InventoryUI scrim=Ignore precedent).
        /// Being absolute, the two drawer containers OVERLAY (don't split root's height like two flex siblings
        /// would) and don't change z-order (hierarchy order is unchanged — only layout).</summary>
        public static void MakeDrawerOverlay(VisualElement container)
        {
            if (container == null) return;
            container.style.position = Position.Absolute;
            container.style.left = 0f;
            container.style.top = 0f;
            container.style.right = 0f;
            container.style.bottom = 0f;
            container.pickingMode = PickingMode.Ignore;
        }

        // Wire the pieces common to BOTH drawers, scoped to the drawer's OWN container (duplicate names resolve
        // per-drawer): the vertical-only scroll (86cabeqj9 — no h-scrollbar), the pointer scroll-gate (both
        // drawers are UI panels, so hovering either swallows ONLY the wheel-zoom), the Reset-to-defaults button
        // (GLOBAL reset — either button reverts the whole registry, matching prior behavior), and the title.
        private void SetupDrawerCommon(VisualElement container, VisualElement panel, ScrollView rows, string title, bool isDev)
        {
            if (rows != null)
            {
                rows.mode = ScrollViewMode.Vertical;
                rows.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }
            if (panel != null)
            {
                // FIX4 — track pointer-over PER DRAWER (isDev) then re-derive, so a close of the sibling drawer can't
                // strand the gate this drawer's hovered rect owns. Both callbacks route through the shared core the
                // UNITY_INCLUDE_TESTS seam also drives (PROD logic, not a copy — mirrors WireFieldFocus/FieldFocus*).
                panel.RegisterCallback<PointerEnterEvent>(_ => PointerEnterDrawer(isDev));
                panel.RegisterCallback<PointerLeaveEvent>(_ => PointerLeaveDrawer(isDev));
            }
            var reset = container.Q<Button>("settings-reset");
            // AC10 — reset-to-defaults END-TO-END: revert every live param, then FULLY repaint (readouts + typed
            // fields + sliders re-render to the defaults, the differs badge clears, AND the conditional decay-slider
            // visibility re-evaluates — RefreshReadouts now covers all of those).
            if (reset != null) reset.clicked += () => { Registry.ResetAll(); RefreshReadouts(); };
            // Per-drawer title ("Settings" vs "Dev console"). Registered for text-scale like the code-shell title.
            var titleLabel = container.Q<Label>(null, "settings-panel__title");
            if (titleLabel != null) { titleLabel.text = title; RegisterText(titleLabel, 20f); }
        }

        // AC4 — a small "⊞ TL" button in the DEV header that cycles the console corner, persists it, and re-parks
        // the panel immediately. Scoped to the dev container's header (the player drawer has no corner-picker).
        private Button _cornerBtn;
        private void BuildCornerPicker(VisualElement container)
        {
            var header = container.Q<VisualElement>("settings-header");
            if (header == null) return;
            _cornerBtn = new Button { name = "settings-corner" };
            _cornerBtn.AddToClassList("settings-corner");
            RegisterText(_cornerBtn, 12f);
            _cornerBtn.text = "⊞ " + ConsolePosition.ShortLabel(_corner);
            _cornerBtn.clicked += CycleCorner;   // single source of truth (the verify-capture drives the same path)
            header.Add(_cornerBtn);
        }

        /// <summary>AC4 — advance the console to the next corner (TL→TR→BL→BR), persist it, repaint the picker
        /// label, and re-park the panel live. PUBLIC so the shipped-build capture exercises the SAME path the
        /// header button uses (rather than synthesizing a UI Toolkit click). Idempotent w.r.t. a missing scrim.</summary>
        public void CycleCorner()
        {
            _corner = ConsolePosition.Next(_corner);
            ConsolePosition.Save(_corner);                       // persists across runs (AC4)
            if (_cornerBtn != null) _cornerBtn.text = "⊞ " + ConsolePosition.ShortLabel(_corner);
            ConsolePosition.Apply(_scrim, _corner);              // re-park live
        }

        /// <summary>Build ONE row per registered entry, generically off its archetype (AC2). A new setting
        /// in the registry shows up here with NO code change — and (86cabeqj9) it gets the typed field (AC5),
        /// nudge selection (AC6), baked-default readout (AC8) and differs badge (AC9) for free.</summary>
        private void BuildRows()
        {
            if (_rows == null || _playerRows == null || Registry == null) return;
            _rows.Clear();
            _playerRows.Clear();
            _handles.Clear();
            _rowsById.Clear();
            _active = null;
            foreach (var entry in Registry.Entries)
            {
                VisualElement row = BuildRow(entry);
                if (row == null) continue;
                _rowsById[entry.Id] = row;
                // 86cah8ukr — ROUTE each row to its view by category (the registry + bindings are UNCHANGED;
                // only the destination ScrollView differs). Player-facing → F1 Settings; everything else → F3 dev.
                if (SettingsCategory.IsPlayer(entry.Id)) _playerRows.Add(row);
                else _rows.Add(row);
            }
            // AC1 — set the initial conditional visibility (a decay slider hidden if its need's toggle starts OFF).
            ApplyConditionalVisibility();
        }

        /// <summary>86cah8ukr AC1 — CONDITIONAL VISIBILITY: each per-need decay-rate slider is shown only while
        /// its on/off toggle is ON; when the toggle flips OFF the slider row hides live (a disabled need has no
        /// rate to tune), and flipping it back ON re-reveals it. Evaluated on build, on every toggle change, and
        /// after a reset. Hides via display:None (no layout, no render — unity6-mastery §9). Cheap (3 lookups),
        /// null-safe. Both endpoints are player-facing, so the show/hide stays within the F1 view.</summary>
        private void ApplyConditionalVisibility()
        {
            if (Registry == null) return;
            foreach (var pair in SettingsCategory.DecaySliderGates)
            {
                if (!_rowsById.TryGetValue(pair.Key, out var sliderRow) || sliderRow == null) continue;
                // Single source of truth: the SAME decision the AC4 test asserts (86cah8ukr) — a wrong impl
                // can't pass a re-implemented proxy. Shows the slider iff its need's on/off toggle is ON.
                bool on = SettingsCategory.IsDecaySliderVisible(Registry, pair.Key);
                sliderRow.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // Per-row repaint + active-highlight closures (86cabeqj9). Replaces #83's readout-only list so a Reset
        // / tweak / open repaints the readout AND the typed field AND the differs badge AND the active outline.
        private struct RowHandle
        {
            public SettingEntry Entry;
            public VisualElement Row;
            public System.Action Repaint;       // re-read live value → readout + typed field + badge
            public System.Action ApplyActive;   // add/remove the active-selection outline class
            // 86cabe3e5 — drive a REAL UI Toolkit ChangeEvent on this row's bound control (set control.value
            // WITH notify), the SAME event a user's drag fires. Populated per-archetype: Slider → DriveFloat,
            // Range → DriveRange. Null for archetypes the capture never drives (Stepper/Toggle). Used by the
            // shipped-build capture to prove input-event → param-change → REPAINTED-frame end-to-end (the
            // entry-setter + RefreshReadouts path drives the param but does NOT repaint the captured frame).
            public System.Func<float, float> DriveFloat;         // Slider: control.value = clamp(v); returns applied
            public System.Action<float, float> DriveRange;       // Range: control.value = (clampedMin, clampedMax)
        }

        private VisualElement BuildRow(SettingEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("setting-row");
            if (!entry.Available) row.AddToClassList("setting-row--disabled");

            var label = new Label(entry.Label);
            label.AddToClassList("setting-row__label");
            RegisterText(label, 14f);   // base 14px (setting-row__label) — scales with UI text scale
            row.Add(label);

            // The archetype control + the generic typed field; each returns its repaint closure and (for the
            // value archetypes) a driver that dispatches a REAL ChangeEvent on the control (86cabe3e5).
            System.Action repaintValue;
            System.Func<float, float> driveFloat = null;   // Slider
            System.Action<float, float> driveRange = null; // Range
            switch (entry.Kind)
            {
                case SettingEntry.Archetype.Slider:
                    repaintValue = BuildSliderRow(row, (FloatSettingEntry)entry, out driveFloat);
                    break;
                case SettingEntry.Archetype.Range:
                    repaintValue = BuildRangeRow(row, (RangeSettingEntry)entry, out driveRange);
                    break;
                case SettingEntry.Archetype.Stepper:
                    repaintValue = BuildStepperRow(row, (IntSettingEntry)entry);
                    break;
                case SettingEntry.Archetype.Toggle:
                    repaintValue = BuildToggleRow(row, (BoolSettingEntry)entry);
                    break;
                default:
                    repaintValue = () => { };
                    break;
            }

            // AC8 — the baked DEFAULT, shown dim next to the current value ("def 5.5") so the Sponsor sees what
            // he's diverged from at a glance. AC9 — a differs-from-default BADGE on any dialed-off-default row.
            var def = new Label(DefaultText(entry));
            def.AddToClassList("setting-row__default");
            RegisterText(def, 11f);
            RegisterScaledWidth(def, 84f);   // 86cagvvhv — grow the "def …" column with the font (USS base 84px)
            row.Add(def);
            var badge = new Label("●");
            badge.AddToClassList("setting-row__badge");
            RegisterText(badge, 14f);
            badge.tooltip = "differs from baked default";
            row.Add(badge);

            if (!entry.Available)
            {
                var soon = new Label("(soon)");
                soon.AddToClassList("setting-row__soon");
                RegisterText(soon, 11f);
                row.Add(soon);
            }

            // The combined repaint: value control + the differs badge visibility (AC9/AC10).
            System.Action repaint = () =>
            {
                repaintValue();
                badge.style.display = entry.DiffersFromDefault ? DisplayStyle.Flex : DisplayStyle.None;
            };
            repaint();

            // AC6 — clicking a row selects it as the active entry the nudge keys drive (one at a time).
            System.Action applyActive = () =>
            {
                if (ReferenceEquals(_active, entry)) row.AddToClassList("setting-row--active");
                else row.RemoveFromClassList("setting-row--active");
            };
            applyActive();
            if (entry.Available)
                row.RegisterCallback<ClickEvent>(_ => SetActive(entry));

            _handles.Add(new RowHandle { Entry = entry, Row = row, Repaint = repaint, ApplyActive = applyActive,
                                         DriveFloat = driveFloat, DriveRange = driveRange });
            return row;
        }

        // AC6 — mark one entry as the nudge target; repaint the active outline across all rows.
        private void SetActive(SettingEntry entry)
        {
            _active = entry;
            for (int i = 0; i < _handles.Count; i++) _handles[i].ApplyActive?.Invoke();
        }

        // Each Build*Row returns a closure that re-reads the entry's LIVE value into its control + typed field
        // (so RefreshReadouts after a nudge / Reset / entry-setter tweak repaints the row — AC10).

        private System.Action BuildSliderRow(VisualElement row, FloatSettingEntry e, out System.Func<float, float> driveChangeEvent)
        {
            row.AddToClassList("setting-row--slider");
            var slider = new Slider(e.Min, e.Max) { value = e.Value };
            slider.AddToClassList("setting-row__control");
            slider.SetEnabled(e.Available);
            var field = MakeNumericField(e.Available);          // AC5 typed entry
            var readout = new Label(); readout.AddToClassList("setting-row__readout");
            RegisterText(readout, 13f);
            RegisterScaledWidth(readout, 92f);                  // 86cagvvhv — grow the readout column with the font
            row.Add(slider); row.Add(field); row.Add(readout);

            System.Action refresh = () =>
            {
                slider.SetValueWithoutNotify(e.Value);
                field.SetValueWithoutNotify(e.Value);
                readout.text = Fmt(e.Value, e.Unit);
            };
            slider.RegisterValueChangedCallback(evt =>
            {
                float applied = e.SetValue(evt.newValue);       // drives the game immediately (AC2) + persists (AC5)
                if (e.Available) SetActive(e);
                RefreshRow(e);
                if (!Mathf.Approximately(applied, evt.newValue)) slider.SetValueWithoutNotify(applied);
            });
            // AC5 — type a number + commit (Enter/blur): applies live + clamps to [Min,Max].
            field.RegisterValueChangedCallback(evt => { e.SetValue(evt.newValue); RefreshRow(e); });
            WireFieldFocus(field, e);
            // 86cabe3e5 — dispatch the SAME ChangeEvent a real drag fires: set the SLIDER'S value WITH notify.
            // That flows through the RegisterValueChangedCallback above (drives the live param + repaints the
            // row) AND schedules a UI Toolkit repaint of the control — so the captured shipped-build frame
            // VISIBLY reflects the tweak. Contrast the entry-setter path (e.SetValue + RefreshReadouts), which
            // changes the live param but does NOT repaint the captured frame (the PR #83 pixel-identical bug).
            // Clamp into the slider band so an out-of-band request still lands on a valid, notifying value.
            driveChangeEvent = v =>
            {
                // 86cajb00b — WITH notify → real ChangeEvent → callback + repaint. DriveValueWithNotify fails LOUD
                // if the clamped target equals the slider's current value (UI Toolkit would suppress the event → no
                // repaint → false-RED verify_settings Check 3); safe today, guards a future same-value target.
                DriveValueWithNotify(slider, Mathf.Clamp(v, e.Min, e.Max));
                return e.Value;                              // the live value actually applied (post-clamp)
            };
            return refresh;
        }

        private System.Action BuildRangeRow(VisualElement row, RangeSettingEntry e, out System.Action<float, float> driveChangeEvent)
        {
            row.AddToClassList("setting-row--range");
            var range = new MinMaxSlider(e.MinValue, e.MaxValue, e.LowerLimit, e.UpperLimit);
            range.AddToClassList("setting-row__control");
            range.SetEnabled(e.Available);
            // AC5 — TWO typed fields for a range (min + max); each commits its own end live + clamped.
            var fieldMin = MakeNumericField(e.Available);
            var fieldMax = MakeNumericField(e.Available);
            var readout = new Label();
            readout.AddToClassList("setting-row__readout");
            readout.AddToClassList("setting-row__readout--minmax");
            RegisterText(readout, 13f);
            RegisterScaledWidth(readout, 120f);                 // 86cagvvhv — minmax readout is 120px; grow with font
            row.Add(range); row.Add(fieldMin); row.Add(fieldMax); row.Add(readout);

            System.Action refresh = () =>
            {
                range.SetValueWithoutNotify(new Vector2(e.MinValue, e.MaxValue));
                fieldMin.SetValueWithoutNotify(e.MinValue);
                fieldMax.SetValueWithoutNotify(e.MaxValue);
                readout.text = Fmt(e.MinValue, e.Unit) + " – " + Fmt(e.MaxValue, e.Unit);
            };
            range.RegisterValueChangedCallback(evt =>
            {
                e.SetMin(evt.newValue.x);                       // AC4 clamp: min ≤ max within hard limits
                e.SetMax(evt.newValue.y);
                if (e.Available) SetActive(e);
                RefreshRow(e);
            });
            fieldMin.RegisterValueChangedCallback(evt => { e.SetMin(evt.newValue); RefreshRow(e); });
            fieldMax.RegisterValueChangedCallback(evt => { e.SetMax(evt.newValue); RefreshRow(e); });
            WireFieldFocus(fieldMin, e);
            WireFieldFocus(fieldMax, e);
            // 86cabe3e5 — dispatch the SAME ChangeEvent a real MinMaxSlider drag fires: set the range control's
            // value WITH notify. Flows through the RegisterValueChangedCallback above (SetMin/SetMax drive the
            // live system + repaint the row) AND schedules a UI Toolkit repaint — so the captured frame reflects
            // the tweak. Clamp both ends into the hard limits so an out-of-band request still notifies.
            driveChangeEvent = (newMin, newMax) =>
            {
                float lo = Mathf.Clamp(newMin, e.LowerLimit, e.UpperLimit);
                float hi = Mathf.Clamp(newMax, e.LowerLimit, e.UpperLimit);
                // 86cajb00b — WITH notify → real ChangeEvent → callback + repaint; fails LOUD if the clamped (lo,hi)
                // equals the range's current value (UI Toolkit would suppress the event → false-RED Check 3).
                DriveValueWithNotify(range, new Vector2(lo, hi));
            };
            return refresh;
        }

        private System.Action BuildStepperRow(VisualElement row, IntSettingEntry e)
        {
            row.AddToClassList("setting-row--stepper");
            var control = new VisualElement { name = "stepper" };
            control.AddToClassList("setting-row__control");
            control.style.flexDirection = FlexDirection.Row;
            control.style.alignItems = Align.Center;

            var dec = new Button { text = "−" }; dec.AddToClassList("stepper__btn");
            var value = new Label(); value.AddToClassList("stepper__value");
            RegisterText(value, 14f);
            var inc = new Button { text = "+" }; inc.AddToClassList("stepper__btn");
            control.Add(dec); control.Add(value); control.Add(inc);
            control.SetEnabled(e.Available);
            row.Add(control);

            var field = MakeIntField(e.Available);              // AC5 typed entry (int)
            var readout = new Label(); readout.AddToClassList("setting-row__readout");
            RegisterText(readout, 13f);
            RegisterScaledWidth(readout, 92f);                  // 86cagvvhv — grow the readout column with the font
            row.Add(field); row.Add(readout);

            System.Action refresh = () =>
            {
                value.text = e.Value.ToString();
                field.SetValueWithoutNotify(e.Value);
                readout.text = Fmt(e.Value, e.Unit);
            };
            dec.clicked += () => { e.Decrement(); if (e.Available) SetActive(e); RefreshRow(e); };
            inc.clicked += () => { e.Increment(); if (e.Available) SetActive(e); RefreshRow(e); };
            field.RegisterValueChangedCallback(evt => { e.SetValue(evt.newValue); RefreshRow(e); });
            WireFieldFocus(field, e);
            return refresh;
        }

        // AC7 — the BOOL/Toggle archetype: an on/off switch. The typed field + nudge come from the generic
        // numeric bridge (0/1) so it gets type/nudge for free like every other archetype.
        private System.Action BuildToggleRow(VisualElement row, BoolSettingEntry e)
        {
            row.AddToClassList("setting-row--toggle");
            var toggle = new Toggle { value = e.Value };
            toggle.AddToClassList("setting-row__control");
            toggle.SetEnabled(e.Available);
            var readout = new Label(); readout.AddToClassList("setting-row__readout");
            RegisterText(readout, 13f);
            RegisterScaledWidth(readout, 92f);                  // 86cagvvhv — grow the readout column with the font
            row.Add(toggle); row.Add(readout);

            System.Action refresh = () =>
            {
                toggle.SetValueWithoutNotify(e.Value);
                readout.text = e.Value ? "On" : "Off";
            };
            toggle.RegisterValueChangedCallback(evt =>
            {
                e.SetValue(evt.newValue);                       // drives the flag live (AC2) + persists (AC5)
                if (e.Available) SetActive(e);
                RefreshRow(e);
                ApplyConditionalVisibility();                   // 86cah8ukr AC1 — a need toggle may show/hide its decay slider
            });
            return refresh;
        }

        // AC5 — a numeric typed field bound to a float value. Narrow, sits right of the control; commits on
        // Enter/blur (FloatField's default). The value-changed callback (wired by the caller) applies + clamps.
        private FloatField MakeNumericField(bool enabled)
        {
            var f = new FloatField { isDelayed = true };        // isDelayed → commit on Enter/blur, not per keystroke
            f.AddToClassList("setting-row__field");
            RegisterText(f, 12f);
            f.SetEnabled(enabled);
            return f;
        }

        private IntegerField MakeIntField(bool enabled)
        {
            var f = new IntegerField { isDelayed = true };
            f.AddToClassList("setting-row__field");
            RegisterText(f, 12f);
            f.SetEnabled(enabled);
            return f;
        }

        // AC3 — INPUT-FOCUS gate: swallow world/locomotion input ONLY while a typed-field holds keyboard focus
        // (so a number typed in isn't ALSO read as movement). Ref-counted so multiple fields (e.g. a range's
        // min+max) compose; the gate releases when the last field blurs. The console itself does NOT gate while
        // merely open (AC2) — only a focused field does.
        private void WireFieldFocus(VisualElement field, SettingEntry entry)
        {
            // Which drawer this field lives in = the SAME routing BuildRows uses (SettingsCategory.IsPlayer is the
            // single source of truth), so per-drawer focus tracking can never desync from the row's destination.
            bool isDev = !SettingsCategory.IsPlayer(entry.Id);
            field.RegisterCallback<FocusInEvent>(_ =>
            {
                if (entry.Available) SetActive(entry);          // typing into a field also makes it the nudge target
                FieldFocusIn(isDev);
            });
            field.RegisterCallback<FocusOutEvent>(_ => FieldFocusOut(isDev));
        }

        // Focus-in / focus-out CORE — shared by the real FocusIn/FocusOut callbacks above AND the EditMode
        // interleaving test seam below, so the two-drawer gate guard exercises PRODUCTION logic, not a parallel
        // copy. Each bumps its drawer's count then re-derives the gate.
        private void FieldFocusIn(bool isDev)
        {
            if (isDev) _devFocusedFields++; else _playerFocusedFields++;
            RefreshInputGate();
        }

        private void FieldFocusOut(bool isDev)
        {
            if (isDev) _devFocusedFields = Mathf.Max(0, _devFocusedFields - 1);
            else _playerFocusedFields = Mathf.Max(0, _playerFocusedFields - 1);
            RefreshInputGate();
        }

        /// <summary>86cah8ukr FIX — RE-DERIVE the world-input gate from the actual focus state: hold it iff SOME
        /// typed-field in SOME still-OPEN drawer holds keyboard focus. Called after every focus in/out and after a
        /// drawer close, so closing one drawer never releases the gate the OTHER drawer's focused field still owns,
        /// while the true all-closed / all-blurred case still releases (world input is never left swallowed). A
        /// hidden drawer's focus count is excluded even if a stale FocusOut never fired (its open flag is false).
        /// Idempotent — <see cref="UiInputGate.SetPanelOpen"/> no-ops when the tracked state already matches.</summary>
        private void RefreshInputGate()
        {
            bool held = (IsOpen && _devFocusedFields > 0) || (IsPlayerOpen && _playerFocusedFields > 0);
            UiInputGate.SetPanelOpen(held, ref _gateTracked);
        }

        // Pointer enter/leave CORE (FIX4) — shared by the real PointerEnter/PointerLeave callbacks in
        // SetupDrawerCommon AND the EditMode interleaving test seam below, so the two-drawer pointer-gate guard
        // exercises PRODUCTION logic, not a parallel copy (mirrors FieldFocusIn/FieldFocusOut). Each sets its
        // drawer's pointer-over flag then re-derives the scroll-zoom gate.
        private void PointerEnterDrawer(bool isDev)
        {
            if (isDev) _devPointerOver = true; else _playerPointerOver = true;
            RefreshPointerGate();
        }

        private void PointerLeaveDrawer(bool isDev)
        {
            if (isDev) _devPointerOver = false; else _playerPointerOver = false;
            RefreshPointerGate();
        }

        /// <summary>86cah8ukr FIX4 — RE-DERIVE the scroll-zoom pointer gate (<see cref="UiInputGate.PointerOverConsole"/>)
        /// from the actual per-drawer hover state: hold it iff the pointer is over SOME still-OPEN drawer's rect.
        /// Called after every pointer enter/leave and after a drawer close, so closing one drawer never releases the
        /// gate the OTHER still-open drawer's hovered rect owns (the shared-single-bool force-clear bug: open F1+F3
        /// overlapping under the cursor, close F3 → the bare SetPointerOverConsole(false) cleared the flag while F1
        /// stayed hovered, and UI Toolkit does NOT re-fire PointerEnter for a pointer already inside → the wheel
        /// zoomed the OrbitCamera THROUGH the open F1 panel until a leave+re-enter). Mirrors <see cref="RefreshInputGate"/>
        /// for the focus gate. A hidden drawer's pointer-over flag is excluded even if a stale PointerLeave never
        /// fired (its open flag is false). Idempotent — SetPointerOverConsole just writes the bool.</summary>
        private void RefreshPointerGate()
        {
            bool over = (IsOpen && _devPointerOver) || (IsPlayerOpen && _playerPointerOver);
            UiInputGate.SetPointerOverConsole(over);
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>EditMode test seam (STRIPPED from ship builds via UNITY_INCLUDE_TESTS) — simulate a typed-field
        /// keyboard focus-in/out on the DEV (<paramref name="isDev"/>=true) or PLAYER drawer WITHOUT a UIDocument
        /// render loop (UI Toolkit focus events are unreliable in EditMode — DevConsoleTests §AC3). Drives the SAME
        /// <see cref="FieldFocusIn"/>/<see cref="FieldFocusOut"/> + gate re-derive the real FocusIn/FocusOut
        /// callbacks use, so the two-drawer interleaving regression guard tests production logic, not a copy.
        /// PUBLIC (matching the codebase's Registry/RefreshReadouts/CycleCorner "public for tests" seams — the
        /// project has no InternalsVisibleTo); the UNITY_INCLUDE_TESTS guard keeps it out of the shipped build.</summary>
        public void SimulateFieldFocusForTest(bool isDev, bool focusIn)
        {
            if (focusIn) FieldFocusIn(isDev); else FieldFocusOut(isDev);
        }

        /// <summary>EditMode test seam (STRIPPED from ship builds via UNITY_INCLUDE_TESTS) — simulate the pointer
        /// entering/leaving the DEV (<paramref name="isDev"/>=true) or PLAYER drawer rect WITHOUT a UIDocument render
        /// loop (UI Toolkit pointer events are unreliable in EditMode — DevConsoleTests §scroll-gate). Drives the SAME
        /// <see cref="PointerEnterDrawer"/>/<see cref="PointerLeaveDrawer"/> + gate re-derive the real callbacks use,
        /// so the FIX4 two-drawer pointer-gate interleaving guard tests production logic, not a copy.</summary>
        public void SimulatePointerOverForTest(bool isDev, bool over)
        {
            if (over) PointerEnterDrawer(isDev); else PointerLeaveDrawer(isDev);
        }

        /// <summary>EditMode test seam (STRIPPED from ship builds via UNITY_INCLUDE_TESTS) — drive the SAME
        /// <see cref="SetActive"/> a row click uses, so the 86cak0uq6 clear-on-close guard tests production logic,
        /// not a copy. Pass null to clear.</summary>
        public void SimulateSetActiveForTest(SettingEntry entry) => SetActive(entry);

        /// <summary>EditMode test seam — the current nudge target (<see cref="_active"/>); null when none selected.</summary>
        public SettingEntry ActiveEntryForTest => _active;
#endif

        /// <summary>Repaint EVERY row from its entry's CURRENT live value — readouts, sliders, typed fields,
        /// AND the differs-from-default badge (AC9/AC10). Called on open + after a Reset (so the UI fully
        /// reverts) + after a nudge/typed tweak. PUBLIC so a harness driving a tweak through the entry setter
        /// directly (e.g. SettingsVerifyCapture) forces the same visible refresh a real drag gets.</summary>
        public void RefreshReadouts()
        {
            for (int i = 0; i < _handles.Count; i++)
            {
                _handles[i].Repaint?.Invoke();
                _handles[i].ApplyActive?.Invoke();
            }
            // 86cah8ukr AC1 — reset-to-defaults / open may have flipped a need toggle; re-evaluate decay-slider
            // visibility so the F1 panel never shows a decay slider for an OFF need after a reset.
            ApplyConditionalVisibility();
        }

        /// <summary>#247 EMPTY-DRAWERS GATE-HARDEN — the resolved ON-SCREEN height of the given drawer's rows
        /// ScrollView VIEWPORT (dev=F3 / player=F1). THIS is the collapse measure: the empty-drawers bug shrank
        /// the flex-grow rows ScrollView to ~0 because its container had no definite height, so the VIEWPORT
        /// went to zero. Note a row's OWN height does NOT go to zero (a ScrollView clips overflow — the rows keep
        /// their intrinsic 44px inside the content container and are simply CLIPPED out of the zero-height
        /// viewport), so a per-row height probe would MISS the bug; the viewport height is what actually
        /// collapses. Populated only under a live panel + layout pass (windowed build) — EditMode has no layout
        /// pass (DevConsoleTests §), so this is a shipped-build-only ground-truth probe. PUBLIC for the capture.</summary>
        public float RowsViewportHeight(bool dev)
        {
            var sv = dev ? _rows : _playerRows;
            return sv != null ? sv.resolvedStyle.height : 0f;
        }

        /// <summary>#247 EMPTY-DRAWERS GATE-HARDEN — the count of rows in the given drawer (dev=F3 / player=F1)
        /// that are ACTUALLY VISIBLE on screen: their world rect overlaps the rows ScrollView's VIEWPORT rect
        /// with real height. Threshold-free + robust to the clip subtlety above — in the collapsed bug the
        /// viewport height is ~0 so NO row overlaps it (returns 0); once the container gets a definite height the
        /// viewport opens and the on-screen rows overlap it (returns > 0). The shipped-build capture logs this so
        /// <c>verify_settings_gate.sh</c> Check 4 FAILS on a zero-row drawer — the generic frame_check only proves
        /// the whole FRAME isn't uniform (the green world passes), never that the PANEL region shows rows, which
        /// is exactly how the empty drawers slipped the gate. PUBLIC for the capture.</summary>
        public int VisibleRowCount(bool dev)
        {
            var sv = dev ? _rows : _playerRows;
            if (sv == null) return 0;
            Rect viewport = sv.worldBound;
            if (viewport.height <= 1f) return 0;   // collapsed viewport → nothing can be visible
            int n = 0;
            for (int i = 0; i < _handles.Count; i++)
            {
                var h = _handles[i];
                if (h.Row == null || h.Entry == null) continue;
                if ((!SettingsCategory.IsPlayer(h.Entry.Id)) != dev) continue;   // wrong drawer for this row
                Rect rb = h.Row.worldBound;
                // Vertically overlaps the viewport (the ScrollView clips to this rect) AND has real height.
                if (rb.height > 1f && rb.yMax > viewport.yMin + 1f && rb.yMin < viewport.yMax - 1f) n++;
            }
            return n;
        }

        /// <summary>#247 — the TOTAL rows routed to the given drawer (dev=F3 / player=F1), regardless of layout
        /// (the denominator for the visible-rows proof; a data-layer count that holds even when the visual layer
        /// collapsed). PUBLIC for the shipped-build capture's ground-truth log.</summary>
        public int RoutedRowCount(bool dev)
        {
            int n = 0;
            for (int i = 0; i < _handles.Count; i++)
            {
                var h = _handles[i];
                if (h.Row == null || h.Entry == null) continue;
                if ((!SettingsCategory.IsPlayer(h.Entry.Id)) != dev) continue;
                n++;
            }
            return n;
        }

        /// <summary>#247 v2 — the SMALLEST resolved width (px) among the [−]/value/[+] cells of ANY stepper row
        /// routed to the given drawer (dev=F3 / player=F1), or -1 if the drawer has no stepper row. THIS is the
        /// F1-cramp measure the Sponsor flagged: the base <c>.setting-row__control</c> is flex-grow:1 with the
        /// DEFAULT flex-shrink:1, so on a squeezed row the stepper control is the only shrinkable child and absorbs
        /// the whole width deficit — collapsing its 28px buttons / 44px value below their glyph widths so
        /// "− 5 [+ 5] 5" jams together and overlaps. A HEALTHY row keeps every cell at/above its design width
        /// (min cell = a 28px button); the #247 v2 USS fix (flex-shrink:0 + min-width on the stepper control +
        /// cells) makes the overflow WRAP instead of crush, so this stays ~28px. Requires a LIVE layout pass
        /// (worldBound/resolvedStyle) — populated only under the windowed shipped build with the drawer OPEN + a
        /// few settle frames (EditMode has no layout pass — DevConsoleTests §), so this is a shipped-build-only
        /// ground-truth probe, the sibling of <see cref="VisibleRowCount"/>. PUBLIC for the capture.</summary>
        public float MinStepperCellWidth(bool dev)
        {
            float min = float.PositiveInfinity;
            for (int i = 0; i < _handles.Count; i++)
            {
                var h = _handles[i];
                if (h.Row == null || h.Entry == null) continue;
                if (h.Entry.Kind != SettingEntry.Archetype.Stepper) continue;
                if ((!SettingsCategory.IsPlayer(h.Entry.Id)) != dev) continue;
                var control = h.Row.Q<VisualElement>("stepper");   // the flexDirection:row [−]/value/[+] container
                if (control == null) continue;
                foreach (var cell in control.Children())
                {
                    float w = cell.resolvedStyle.width;
                    if (w < min) min = w;
                }
            }
            return float.IsInfinity(min) ? -1f : min;   // -1 = no stepper row in this drawer (nothing to crush)
        }

        /// <summary>#247 v2 — the count of stepper rows routed to the given drawer (dev=F3 / player=F1), so the
        /// capture log can name the denominator behind <see cref="MinStepperCellWidth"/> (a -1 with 0 rows is a
        /// legit skip; a -1 with rows would be a probe miss). PUBLIC for the capture.</summary>
        public int StepperRowCount(bool dev)
        {
            int n = 0;
            for (int i = 0; i < _handles.Count; i++)
            {
                var h = _handles[i];
                if (h.Entry == null) continue;
                if (h.Entry.Kind != SettingEntry.Archetype.Stepper) continue;
                if ((!SettingsCategory.IsPlayer(h.Entry.Id)) != dev) continue;
                n++;
            }
            return n;
        }

        /// <summary>86cabe3e5 — drive a REAL UI Toolkit ChangeEvent on the SLIDER row bound to <paramref
        /// name="settingId"/>, the SAME event a user's drag fires (set the control's value WITH notify). Unlike
        /// calling the entry setter + <see cref="RefreshReadouts"/> directly (which drives the live param + writes
        /// PlayerPrefs but BYPASSES the control's RegisterValueChangedCallback, so the row never repaints under
        /// the -verifySettings shipped-build capture → settings_tweaked.png came out PIXEL-IDENTICAL to
        /// settings_open.png, the PR #83 re-QA bug), the ChangeEvent flows through the panel binding → the
        /// callback drives the live param AND UI Toolkit schedules a repaint, so the captured frame VISIBLY
        /// reflects the tweak. Returns the value actually applied (post-clamp), or NaN if no such Slider row.
        /// PUBLIC for the shipped-build capture (SettingsVerifyCapture) so the gate proves input-event →
        /// param-change → repainted-frame end-to-end, not a synthetic setter.</summary>
        public float DriveFloatChangeEventForCapture(string settingId, float newValue)
        {
            for (int i = 0; i < _handles.Count; i++)
                if (_handles[i].Entry != null && _handles[i].Entry.Id == settingId && _handles[i].DriveFloat != null)
                    return _handles[i].DriveFloat(newValue);
            return float.NaN;
        }

        /// <summary>86cabe3e5 — drive a REAL ChangeEvent on the RANGE (MinMaxSlider) row bound to <paramref
        /// name="settingId"/> (set the control's value WITH notify). Same rationale as
        /// <see cref="DriveFloatChangeEventForCapture"/> for the dual-thumb range archetype. Returns true if a
        /// matching Range row was driven, false otherwise. PUBLIC for the shipped-build capture.</summary>
        public bool DriveRangeChangeEventForCapture(string settingId, float newMin, float newMax)
        {
            for (int i = 0; i < _handles.Count; i++)
                if (_handles[i].Entry != null && _handles[i].Entry.Id == settingId && _handles[i].DriveRange != null)
                {
                    _handles[i].DriveRange(newMin, newMax);
                    return true;
                }
            return false;
        }

        /// <summary>86cajb00b (#244 NIT) — set a bound control's value WITH notify for the capture drive, failing
        /// LOUD if the target EQUALS the control's current value. UI Toolkit's <c>BaseField&lt;T&gt;.value</c> setter
        /// early-outs (dispatches NO ChangeEvent) when the new value equals the current one
        /// (<c>EqualityComparer&lt;T&gt;.Default</c>) — so a capture drive to the current value would SILENTLY skip
        /// the RegisterValueChangedCallback + the row repaint, leaving <c>settings_tweaked.png</c> pixel-identical to
        /// <c>settings_open.png</c> and FALSE-REDding <c>verify_settings_gate.sh</c> Check 3 with no clue why (the exact
        /// symptom #244 un-quarantined). Safe TODAY (every drive target differs from its default), but a future
        /// same-value tweak target is a no-op that can never produce a visible diff — so surface it as a NAMED failure
        /// telling the author to pick a target that actually moves the control, rather than chasing a mystery
        /// pixel-identical red. Capture-only surface (reached solely via the Drive*ChangeEventForCapture helpers) —
        /// ZERO player-facing behaviour. Works for both archetypes: <see cref="Slider"/> (BaseField&lt;float&gt;) and
        /// <see cref="MinMaxSlider"/> (BaseField&lt;Vector2&gt;).</summary>
        public static void DriveValueWithNotify<T>(BaseField<T> control, T target)
        {
            if (EqualityComparer<T>.Default.Equals(control.value, target))
                throw new System.InvalidOperationException(
                    $"SettingsPanel capture-drive target ({target}) equals the control's CURRENT value — UI Toolkit " +
                    "suppresses the ChangeEvent for an unchanged value, so the -verifySettings capture would NOT " +
                    "repaint (settings_tweaked.png == settings_open.png → false-RED verify_settings_gate Check 3). " +
                    "Pick a capture tweak target that DIFFERS from the row's current value. (86cajb00b)");
            control.value = target; // differs → the setter dispatches the real ChangeEvent → callback + repaint
        }

        // Repaint a single row (after a nudge / typed commit / slider drag on that entry) — cheaper than the
        // whole-panel RefreshReadouts and keeps the badge in sync with the value just applied.
        private void RefreshRow(SettingEntry entry)
        {
            for (int i = 0; i < _handles.Count; i++)
                if (ReferenceEquals(_handles[i].Entry, entry)) { _handles[i].Repaint?.Invoke(); _handles[i].ApplyActive?.Invoke(); return; }
        }

        // AC8 — the baked-default text shown dim on each row ("def 5.5", "def 6 – 26", "def On").
        private static string DefaultText(SettingEntry entry)
        {
            switch (entry)
            {
                case FloatSettingEntry f: return "def " + Fmt(f.Default, f.Unit);
                case IntSettingEntry i:   return "def " + Fmt(i.Default, i.Unit);
                case RangeSettingEntry r: return "def " + Fmt(r.DefaultMin, r.Unit) + " – " + Fmt(r.DefaultMax, r.Unit);
                case BoolSettingEntry b:  return "def " + (b.Default ? "On" : "Off");
                default: return "";
            }
        }

        private static string Fmt(float v, string unit)
        {
            // Fixed-ish width so the readout doesn't jitter as the value changes (Uma §2.2). Whole numbers
            // show no decimal; otherwise one decimal.
            string num = Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0");
            return string.IsNullOrEmpty(unit) ? num : num + " " + unit;
        }

        // Build-safety net only — mirrors SettingsPanel.uxml if the asset reference didn't serialize. Adds into
        // the given CONTAINER (86cah8ukr — CloneShell calls this per-drawer, so the two shells stay isolated).
        // The title text + its text-scale registration are set by SetupDrawerCommon (uniform across the UXML and
        // code paths — it Q's "settings-panel__title"), so this only tags the class; it does NOT RegisterText it.
        private void BuildShellInCode(VisualElement container)
        {
            var scrim = new VisualElement { name = "settings-scrim" }; scrim.AddToClassList("settings-scrim");
            var panel = new VisualElement { name = "settings-panel" }; panel.AddToClassList("settings-panel");
            var header = new VisualElement { name = "settings-header" }; header.AddToClassList("settings-panel__header");
            var title = new Label("Settings"); title.AddToClassList("settings-panel__title");
            header.Add(title);
            var rows = new ScrollView { name = "settings-rows" }; rows.AddToClassList("settings-panel__rows");
            var footer = new VisualElement { name = "settings-footer" }; footer.AddToClassList("settings-panel__footer");
            var reset = new Button { name = "settings-reset", text = "Reset to defaults" }; reset.AddToClassList("settings-reset");
            footer.Add(reset);
            panel.Add(header); panel.Add(rows); panel.Add(footer);
            scrim.Add(panel);
            container.Add(scrim);
        }
    }
}
