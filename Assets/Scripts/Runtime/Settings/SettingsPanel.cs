using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using FarHorizon.Settings;

namespace FarHorizon
{
    /// <summary>
    /// The in-game tweakable SETTINGS PANEL (ticket 86caa4bqp) — a UI Toolkit workbench drawer the Sponsor
    /// pulls open mid-soak (Esc) to dial live gameplay params, then we BAKE the chosen values as new
    /// defaults (the give-him-the-knob soak-tuning instrument; cf. the F9 axe-nudge tool).
    ///
    /// THE THIN VIEW. The extensible contract lives in <see cref="SettingsRegistry"/> + the typed
    /// <see cref="SettingEntry"/>s (pure C#, unit-tested in EditMode). This MonoBehaviour:
    ///   • builds the registry via <see cref="SettingsCatalog"/> from the serialized live targets (AC3);
    ///   • loads persisted values (AC5) + applies them on Start;
    ///   • builds one UI Toolkit row per entry GENERICALLY off its archetype (AC1/AC2) — a new setting
    ///     needs NO change here; it just appears as a row;
    ///   • toggles the panel on Esc (AC1, non-clashing per Uma §8) and gates locomotion/belt input while
    ///     open (research §E1 — Input.* polling bleeds through an open panel).
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

        [Header("Toggle")]
        [Tooltip("Key that opens/closes the panel. Esc per Uma §8 (free — no clash with WASD/Shift/Ctrl/Space/Tab/1-5).")]
        public KeyCode toggleKey = KeyCode.Escape;

        /// <summary>The registry this panel renders + drives. Built on Start from the catalog (public for tests).</summary>
        public SettingsRegistry Registry { get; private set; }

        /// <summary>Whether the panel is currently open. Other systems gate their input on this (research §E1).</summary>
        public bool IsOpen { get; private set; }

        private VisualElement _scrim;          // the full-screen scrim (the show/hide target — display:None)
        private VisualElement _panel;          // the centered column (the transition target)
        private ScrollView _rows;
        private bool _built;
        private bool _gateTracked;             // whether THIS panel currently holds the UiInputGate open

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
        }

        void Start()
        {
            // Build the registry from the live targets (AC3), load persisted soak tweaks (AC5), apply them.
            // The thirst overload (86caamkv7 AC5) adds the thirst decay rate + water scoop amount rows; the
            // chop overload (86caa4c5c) flips tool-use speed live + adds the tree regrowth time range row; the
            // hunger overload (86cabd75y) adds the hunger decay rate + berry restore amount rows; the berry
            // overload (86cabn67w) adds the `Berry regrowth time` range row fanning out across every bush.
            Registry = SettingsCatalog.Build(orbit, wasd, thirst, chopCharacter, chopTree, stoneRespawner, logPileSpawner, heldWeapon, hunger, berryBushes);
            Registry.LoadAll();   // survives a relaunch
            Registry.ApplyAll();  // drive the live params with the loaded values on startup

            BuildView();
            SetOpen(false); // start hidden — Esc opens it
        }

        void Update()
        {
            // Esc toggles (legacy Input — the project is activeInputHandler=0; unity-conventions.md §Input).
            if (Input.GetKeyDown(toggleKey)) SetOpen(!IsOpen);
        }

        // Safety net: if the panel is disabled/destroyed while open, release the input gate so world input
        // can never be left permanently swallowed (mirrors OrbitCamera's OnDisable cursor-restore guard).
        void OnDisable()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
        }

        /// <summary>Open/close the panel. Hide via display:None (zero render cost — unity6-mastery §9), then
        /// play the open transition on the now-laid-out panel.</summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
            // Swallow world/locomotion input while open (research §E1 — Input.* bleeds through a UI panel).
            UiInputGate.SetPanelOpen(open, ref _gateTracked);
            if (_scrim == null) return;
            _scrim.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
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
            var reset = root.Q<Button>("settings-reset");
            if (reset != null) reset.clicked += () => { Registry.ResetAll(); RefreshReadouts(); };

            BuildRows();
            _built = true;
        }

        /// <summary>Build ONE row per registered entry, generically off its archetype (AC2). A new setting
        /// in the registry shows up here with NO code change.</summary>
        private void BuildRows()
        {
            if (_rows == null || Registry == null) return;
            _rows.Clear();
            _readouts.Clear();
            foreach (var entry in Registry.Entries)
            {
                VisualElement row = BuildRow(entry);
                if (row != null) _rows.Add(row);
            }
        }

        // Tracks the readout-refresh closures so a Reset / open can repaint every row's value text.
        private readonly List<System.Action> _readouts = new List<System.Action>();

        private VisualElement BuildRow(SettingEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("setting-row");
            if (!entry.Available) row.AddToClassList("setting-row--disabled");

            var label = new Label(entry.Label);
            label.AddToClassList("setting-row__label");
            row.Add(label);

            switch (entry.Kind)
            {
                case SettingEntry.Archetype.Slider:
                    BuildSliderRow(row, (FloatSettingEntry)entry);
                    break;
                case SettingEntry.Archetype.Range:
                    BuildRangeRow(row, (RangeSettingEntry)entry);
                    break;
                case SettingEntry.Archetype.Stepper:
                    BuildStepperRow(row, (IntSettingEntry)entry);
                    break;
            }

            if (!entry.Available)
            {
                var soon = new Label("(soon)");
                soon.AddToClassList("setting-row__soon");
                row.Add(soon);
            }
            return row;
        }

        private void BuildSliderRow(VisualElement row, FloatSettingEntry e)
        {
            row.AddToClassList("setting-row--slider");
            var slider = new Slider(e.Min, e.Max) { value = e.Value };
            slider.AddToClassList("setting-row__control");
            slider.SetEnabled(e.Available);
            var readout = new Label();
            readout.AddToClassList("setting-row__readout");
            row.Add(slider);
            row.Add(readout);

            System.Action refresh = () => readout.text = Fmt(e.Value, e.Unit);
            refresh();
            _readouts.Add(refresh);
            slider.RegisterValueChangedCallback(evt =>
            {
                float applied = e.SetValue(evt.newValue);  // drives the game immediately (AC2) + persists (AC5)
                if (!Mathf.Approximately(applied, evt.newValue)) slider.SetValueWithoutNotify(applied);
                readout.text = Fmt(applied, e.Unit);
            });
        }

        private void BuildRangeRow(VisualElement row, RangeSettingEntry e)
        {
            row.AddToClassList("setting-row--range");
            var range = new MinMaxSlider(e.MinValue, e.MaxValue, e.LowerLimit, e.UpperLimit);
            range.AddToClassList("setting-row__control");
            range.SetEnabled(e.Available);
            var readout = new Label();
            readout.AddToClassList("setting-row__readout");
            readout.AddToClassList("setting-row__readout--minmax");
            row.Add(range);
            row.Add(readout);

            System.Action refresh = () => readout.text = Fmt(e.MinValue, e.Unit) + " – " + Fmt(e.MaxValue, e.Unit);
            refresh();
            _readouts.Add(refresh);
            range.RegisterValueChangedCallback(evt =>
            {
                // Apply both ends through the entry (AC4 clamp: min ≤ max, within hard limits). The entry's
                // clamp may move an end; re-read for the readout + snap the control to the clamped value.
                float min = e.SetMin(evt.newValue.x);
                float max = e.SetMax(evt.newValue.y);
                range.SetValueWithoutNotify(new Vector2(min, max));
                readout.text = Fmt(min, e.Unit) + " – " + Fmt(max, e.Unit);
            });
        }

        private void BuildStepperRow(VisualElement row, IntSettingEntry e)
        {
            row.AddToClassList("setting-row--stepper");
            var control = new VisualElement { name = "stepper" };
            control.AddToClassList("setting-row__control");
            control.style.flexDirection = FlexDirection.Row;
            control.style.alignItems = Align.Center;

            var dec = new Button { text = "−" }; dec.AddToClassList("stepper__btn");
            var value = new Label(); value.AddToClassList("stepper__value");
            var inc = new Button { text = "+" }; inc.AddToClassList("stepper__btn");
            control.Add(dec); control.Add(value); control.Add(inc);
            control.SetEnabled(e.Available);
            row.Add(control);

            var readout = new Label(); readout.AddToClassList("setting-row__readout");
            row.Add(readout);

            System.Action refresh = () => { value.text = e.Value.ToString(); readout.text = Fmt(e.Value, e.Unit); };
            refresh();
            _readouts.Add(refresh);
            dec.clicked += () => { e.Decrement(); refresh(); };
            inc.clicked += () => { e.Increment(); refresh(); };
        }

        /// <summary>Repaint every row's value text from its entry's CURRENT live value. Called on open + after
        /// a Reset so the readouts always match the live params. PUBLIC so a harness that drives a tweak through
        /// the entry setter directly (bypassing the slider callback, e.g. SettingsVerifyCapture) can force the
        /// view to repaint the changed value before a capture — a real slider drag repaints via the slider's
        /// RegisterValueChangedCallback; an entry-setter tweak must call this to get the same visible refresh.</summary>
        public void RefreshReadouts()
        {
            for (int i = 0; i < _readouts.Count; i++) _readouts[i]?.Invoke();
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
