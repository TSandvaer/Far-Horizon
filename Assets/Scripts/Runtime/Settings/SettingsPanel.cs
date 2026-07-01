using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using FarHorizon.Settings;

namespace FarHorizon
{
    /// <summary>
    /// The in-game DEV TWEAK CONSOLE (tickets 86caa4bqp + 86cabeqj9 foundation) — a UI Toolkit workbench
    /// drawer the Sponsor keeps OPEN and tweaks WHILE he plays, to dial live gameplay params, then we BAKE
    /// the chosen values as new defaults (the give-him-the-knob soak-tuning instrument; cf. the F9 axe-nudge
    /// tool). It is a DEV tool, NOT player-facing ([[sponsor-wants-unified-dev-tweak-console]]).
    ///
    /// THE THIN VIEW. The extensible contract lives in <see cref="SettingsRegistry"/> + the typed
    /// <see cref="SettingEntry"/>s (pure C#, unit-tested in EditMode). This MonoBehaviour:
    ///   • builds the registry via <see cref="SettingsCatalog"/> from the serialized live targets (AC3);
    ///   • loads persisted values (AC5) + applies them on Start;
    ///   • builds one UI Toolkit row per entry GENERICALLY off its archetype (AC2) — a new setting needs NO
    ///     change here; it just appears as a row, with a typed field (AC5), nudge selection (AC6), a baked-
    ///     default readout (AC8) and a differs-from-default badge (AC9) for free;
    ///   • OPENS/CLOSES on F1, polled DIRECTLY (86cabeqj9 soak NIT — F1/F2 de-conflict). F1 toggles ONLY the
    ///     console now; the LEGACY IMGUI overlays moved to F2 (DebugOverlayToggle). It previously rode the
    ///     shared DebugOverlays.Visible flag, so one F1 popped the console AND the legacy overlays together —
    ///     decoupled here so each key reveals exactly one layer (AC1);
    ///   • is NON-MODAL (AC2): being open does NOT pause/gate gameplay (no Time.timeScale touch); world input
    ///     is swallowed ONLY while a typed-field holds keyboard focus (AC3 — so a typed number isn't also read
    ///     as movement), via the ref-counted UiInputGate the console OPTS INTO per-field (genuinely-modal
    ///     future panels like inventory Tab still opt into the open-gate).
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

        [Header("Toggle")]
        [Tooltip("Key that opens/closes the console — F1 (86cabeqj9 AC1). The console KEEPS F1 but polls it " +
                 "DIRECTLY (the 86cabeqj9 soak NIT F1/F2 de-conflict): F1 toggles ONLY the console; the LEGACY " +
                 "IMGUI overlays moved to F2 (DebugOverlayToggle). It previously rode the shared " +
                 "DebugOverlays.Visible flag, so one F1 popped the console AND the legacy overlays together. " +
                 "Layout-agnostic + verified non-clashing with WASD/Shift/Space/Tab/F7-F10 " +
                 "([[sponsor-danish-keyboard-layout]]). The Update poll reads this field directly.")]
        public KeyCode toggleKey = KeyCode.F1;

        /// <summary>The registry this panel renders + drives. Built on Start from the catalog (public for tests).</summary>
        public SettingsRegistry Registry { get; private set; }

        /// <summary>Whether the panel is currently open. Other systems gate their input on this (research §E1).</summary>
        public bool IsOpen { get; private set; }

        private VisualElement _scrim;          // the full-screen scrim (the show/hide target — display:None)
        private VisualElement _panel;          // the corner-parked column (the transition target)
        private ScrollView _rows;
        private bool _built;
        private bool _gateTracked;             // whether THIS panel currently holds the UiInputGate open (FOCUS-gate, AC3)
        private int _focusedFields;            // how many typed-fields currently hold keyboard focus (AC3 ref-count)
        private ConsoleCorner _corner;         // the persisted panel corner (AC4)
        private float _uiScale = 1f;           // the persisted console UI scale multiplier (86cabeqj9 soak NIT)
        private float _textScale = 1f;         // the persisted UI TEXT scale multiplier (86cabeqj9 soak NIT — DISTINCT from _uiScale chrome)
        // Every panel text element paired with its BASE font px (the USS-authored size). ApplyTextScale sets
        // each element's inline fontSize = base * _textScale, so the "UI text scale" slider resizes ALL panel
        // text LIVE, independently of the chrome-scaling _uiScale. An inline fontSize wins over the USS selector.
        private readonly System.Collections.Generic.List<TextEl> _textEls = new System.Collections.Generic.List<TextEl>();
        private struct TextEl { public VisualElement El; public float BasePx; }
        private SettingEntry _active;          // the focused/selected entry the nudge keys drive (AC6, one at a time)
        private readonly List<RowHandle> _handles = new List<RowHandle>(); // per-row repaint + active-highlight (AC8/AC9/AC10)

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (orbit == null) orbit = FindObjectOfType<OrbitCamera>();
            if (wasd == null) wasd = FindObjectOfType<WasdMovement>();
            if (thirst == null) thirst = FindObjectOfType<ThirstNeed>();
            if (hunger == null) hunger = FindObjectOfType<HungerNeed>();
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
        }

        void Start()
        {
            // Build the registry from the live targets (AC3), load persisted soak tweaks (AC5), apply them.
            // The thirst overload (86caamkv7 AC5) adds the thirst decay rate + water scoop amount rows; the
            // chop overload (86caa4c5c) flips tool-use speed live + adds the tree regrowth time range row; the
            // hunger overload (86cabd75y) adds the hunger decay rate + berry restore amount rows; the berry
            // overload (86cabn67w) adds the `Berry regrowth time` range row fanning out across every bush; the
            // inventory overload (86cabfa4e) adds `inventory slots` + `belt slots` + `inventory stack size`.
            Registry = SettingsCatalog.Build(orbit, wasd, thirst, chopCharacter, chopTree, stoneRespawner, logPileSpawner, heldWeapon, hunger, berryBushes, inventory);

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
            SetOpen(false); // start hidden — F1 opens it (86cabeqj9 — console-only F1, polled directly)
        }

        void Update()
        {
            // AC1 + 86cabeqj9 F1/F2 DE-CONFLICT (soak NIT). The console KEEPS F1, but now polls F1 DIRECTLY
            // rather than riding the shared DebugOverlays.Visible flag. Before this, the console synced to
            // DebugOverlays.Visible (which DebugOverlayToggle flipped on F1) — so one F1 popped the console AND
            // the legacy IMGUI overlays (axe-shaft length, pond recess/foam) together (the Sponsor's complaint).
            // Decoupled: F1 toggles ONLY the console here; the legacy overlays moved to F2 (DebugOverlayToggle).
            // Layout-agnostic + Danish-safe (an F-key) and verified non-clashing with WASD/Shift/Space/Tab/F7-F10
            // ([[sponsor-danish-keyboard-layout]]). Legacy Input (activeInputHandler=0), like every debug toggle.
            if (Input.GetKeyDown(toggleKey)) SetOpen(!IsOpen);

            // AC6 — NUDGE the focused/selected entry. Only while the console is open AND no typed-field holds
            // keyboard focus (so the nudge keys don't fight a value being typed). PageUp/PageDown are the carried
            // nudge-tool idiom — Danish-keyboard-safe + NOT a locomotion key (WASD/arrows/Shift/Space), so they
            // act without stealing focus. Shift = 5x / Ctrl = 0.2x step (the exact WorldLookNudgeTool convention).
            if (IsOpen && _focusedFields == 0 && _active != null)
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
            _focusedFields = 0;
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
        }

        /// <summary>Open/close the console (AC1/AC2). NON-MODAL: opening NO LONGER swallows world/locomotion
        /// input (#83 was modal) and NEVER touches Time.timeScale — WASD/run/jump/orbit stay live so the
        /// Sponsor tweaks WHILE he plays and sees the effect in real time. Hide via display:None (zero render
        /// cost — unity6-mastery §9), then play the open transition on the now-laid-out panel.</summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
            // AC3 — when CLOSING, drop any focus-gate this panel held (a field can't keep gating once hidden).
            if (!open && _focusedFields > 0)
            {
                _focusedFields = 0;
                UiInputGate.SetPanelOpen(false, ref _gateTracked);
            }
            if (_scrim == null) return;
            _scrim.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                // Apply the persisted UI scale (86cabeqj9 soak NIT) + park the panel in the persisted corner
                // (AC4 — off the player) BEFORE the open transition.
                ApplyConsoleScale();
                ApplyTextScale();
                ConsolePosition.Apply(_scrim, _corner);
                // Snappy slide-up + fade-in (the USS transition is on .settings-panel). Set the start state
                // then the end state next layout so the transition plays.
                if (_panel != null)
                {
                    _panel.style.translate = new Translate(0, 16, 0);
                    _panel.style.opacity = 0f;
                    _panel.schedule.Execute(() =>
                    {
                        _panel.style.translate = new Translate(0, 0, 0);
                        _panel.style.opacity = 1f;
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

        /// <summary>86cabeqj9 soak NIT — apply the UI TEXT scale to every registered panel text element
        /// (fontSize = base * _textScale). DISTINCT from ApplyConsoleScale (chrome transform): this resizes
        /// the FONT only, so the Sponsor makes text bigger/smaller independently. Inline fontSize wins over
        /// the USS selector. Idempotent + null-safe (a no-op before the rows/chrome are built).</summary>
        private void ApplyTextScale()
        {
            for (int i = 0; i < _textEls.Count; i++)
                if (_textEls[i].El != null) _textEls[i].El.style.fontSize = _textEls[i].BasePx * _textScale;
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
                    e.SetValue(e.Value + dir * SliderStep(e) * mul);
                    break;
                }
                case SettingEntry.Archetype.Range:
                {
                    // Nudge MOVES THE WHOLE WINDOW (both ends by the step) — the most useful single-knob nudge
                    // for a range; fine-tuning a single end stays the slider-drag affordance.
                    var e = (RangeSettingEntry)_active;
                    float step = RangeStep(e) * mul * dir;
                    e.SetMin(e.MinValue + step);
                    e.SetMax(e.MaxValue + step);
                    break;
                }
                case SettingEntry.Archetype.Stepper:
                {
                    var e = (IntSettingEntry)_active;
                    // Step modifiers scale the int step (Shift=5x, Ctrl=0.2x → at least 1).
                    int s = Mathf.Max(1, Mathf.RoundToInt(e.Step * mul));
                    e.SetValue(e.Value + dir * s);
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
        }

        private static float NudgeStepMul()
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 0.2f;
            return 1f;
        }

        // Per-archetype base nudge step: 1% of the dialable band (so a nudge is a sensible fine increment on
        // any range, e.g. 0.11 u/s on the 1–12 walk band; Shift/Ctrl scale it). Range uses the wider span.
        private static float SliderStep(FloatSettingEntry e) => Mathf.Max(0.01f, (e.Max - e.Min) * 0.01f);
        private static float RangeStep(RangeSettingEntry e) => Mathf.Max(0.01f, (e.UpperLimit - e.LowerLimit) * 0.01f);

        // ---- View construction (built once; rows come from the registry, generically) -------------------

        private void BuildView()
        {
            if (_built || document == null) return;
            var root = document.rootVisualElement;
            if (root == null) return;

            if (paletteUss != null) root.styleSheets.Add(paletteUss);
            if (panelUss != null) root.styleSheets.Add(panelUss);

            if (panelUxml != null)
            {
                panelUxml.CloneTree(root);
            }
            else
            {
                // Build-safety net: if the UXML asset didn't serialize, build the shell in code so the panel
                // still works (the row build is identical either way).
                BuildShellInCode(root);
            }

            _scrim = root.Q<VisualElement>("settings-scrim");
            _panel = root.Q<VisualElement>("settings-panel");
            _rows = root.Q<ScrollView>("settings-rows");
            if (_rows != null)
            {
                // 86cabeqj9 soak NIT — kill the horizontal scrollbar: vertical-only ScrollView, h-scroller
                // never shown. Combined with the flexible (wrapping) row layout in USS, no h-scroll ever
                // appears at the default console scale; vertical scroll stays.
                _rows.mode = ScrollViewMode.Vertical;
                _rows.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }
            if (_panel != null)
            {
                // 86cabeqj9 soak NIT — SCROLL passthrough: mark the pointer as OVER the console while it hovers
                // the panel rect, so OrbitCamera swallows ONLY the wheel-zoom (WASD/orbit stay live — the
                // intentional non-modal passthrough). UI Toolkit can't stop legacy Input.* polling, hence the flag.
                _panel.RegisterCallback<PointerEnterEvent>(_ => UiInputGate.SetPointerOverConsole(true));
                _panel.RegisterCallback<PointerLeaveEvent>(_ => UiInputGate.SetPointerOverConsole(false));
            }
            var reset = root.Q<Button>("settings-reset");
            // AC10 — reset-to-defaults END-TO-END: revert every live param, then FULLY repaint (readouts +
            // typed fields + sliders re-render to the defaults AND the differs badge clears — RefreshReadouts
            // now repaints all of those, not just the readout text).
            if (reset != null) reset.clicked += () => { Registry.ResetAll(); RefreshReadouts(); };

            // AC4 — the corner POSITION PICKER, parked in the header (mouse-driven, Danish-safe). Cycles
            // TL→TR→BL→BR; persists the chosen corner; re-parks the panel live.
            _corner = ConsolePosition.Load();
            BuildCornerPicker(root);

            BuildRows();
            _built = true;
        }

        // AC4 — a small "⊞ TL" button in the header that cycles the panel corner, persists it, and re-parks
        // the panel immediately. Added to the existing header so no UXML edit is needed (the extensible spirit).
        private Button _cornerBtn;
        private void BuildCornerPicker(VisualElement root)
        {
            var header = root.Q<VisualElement>("settings-header");
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
            if (_rows == null || Registry == null) return;
            _rows.Clear();
            _handles.Clear();
            _active = null;
            foreach (var entry in Registry.Entries)
            {
                VisualElement row = BuildRow(entry);
                if (row != null) _rows.Add(row);
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

            // The archetype control + the generic typed field; each returns its repaint closure.
            System.Action repaintValue;
            switch (entry.Kind)
            {
                case SettingEntry.Archetype.Slider:
                    repaintValue = BuildSliderRow(row, (FloatSettingEntry)entry);
                    break;
                case SettingEntry.Archetype.Range:
                    repaintValue = BuildRangeRow(row, (RangeSettingEntry)entry);
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

            _handles.Add(new RowHandle { Entry = entry, Row = row, Repaint = repaint, ApplyActive = applyActive });
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

        private System.Action BuildSliderRow(VisualElement row, FloatSettingEntry e)
        {
            row.AddToClassList("setting-row--slider");
            var slider = new Slider(e.Min, e.Max) { value = e.Value };
            slider.AddToClassList("setting-row__control");
            slider.SetEnabled(e.Available);
            var field = MakeNumericField(e.Available);          // AC5 typed entry
            var readout = new Label(); readout.AddToClassList("setting-row__readout");
            RegisterText(readout, 13f);
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
            return refresh;
        }

        private System.Action BuildRangeRow(VisualElement row, RangeSettingEntry e)
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
            field.RegisterCallback<FocusInEvent>(_ =>
            {
                if (entry.Available) SetActive(entry);          // typing into a field also makes it the nudge target
                _focusedFields++;
                if (_focusedFields == 1) UiInputGate.SetPanelOpen(true, ref _gateTracked);
            });
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                _focusedFields = Mathf.Max(0, _focusedFields - 1);
                if (_focusedFields == 0) UiInputGate.SetPanelOpen(false, ref _gateTracked);
            });
        }

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

        // Build-safety net only — mirrors SettingsPanel.uxml if the asset reference didn't serialize.
        private void BuildShellInCode(VisualElement root)
        {
            var scrim = new VisualElement { name = "settings-scrim" }; scrim.AddToClassList("settings-scrim");
            var panel = new VisualElement { name = "settings-panel" }; panel.AddToClassList("settings-panel");
            var header = new VisualElement { name = "settings-header" }; header.AddToClassList("settings-panel__header");
            var title = new Label("Settings"); title.AddToClassList("settings-panel__title");
            RegisterText(title, 20f);
            header.Add(title);
            var rows = new ScrollView { name = "settings-rows" }; rows.AddToClassList("settings-panel__rows");
            var footer = new VisualElement { name = "settings-footer" }; footer.AddToClassList("settings-panel__footer");
            var reset = new Button { name = "settings-reset", text = "Reset to defaults" }; reset.AddToClassList("settings-reset");
            footer.Add(reset);
            panel.Add(header); panel.Add(rows); panel.Add(footer);
            scrim.Add(panel);
            root.Add(scrim);
        }
    }
}
