using System.Collections;
using System.IO;
using UnityEngine;
using FarHorizon.Settings;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the SETTINGS PANEL (ticket 86caa4bqp).
    ///
    /// The testing bar's shipped-build capture gate (unity-conventions.md §editor-vs-runtime) requires
    /// proving the UX-visible panel OPENS and a tweak TAKES EFFECT LIVE in the BUILT exe — not just the
    /// editor (UI Toolkit panels are a UIDocument-render surface; editor evidence is necessary, never
    /// sufficient). A real Esc keystroke + slider drag can't be injected into a scripted/windowed build, so
    /// this drives the panel programmatically. The OPEN/corner paths use SetOpen/CycleCorner; the VALUE tweaks
    /// (walk/zoom/scale/text) dispatch REAL UI Toolkit ChangeEvents on the bound controls via SettingsPanel.
    /// DriveFloat/DriveRangeChangeEventForCapture — the SAME event a user's slider drag fires (86cabe3e5). That
    /// flows through the panel binding so the live param changes AND the captured frame REPAINTS. (The earlier
    /// entry-setter + RefreshReadouts drive changed the live param but did NOT repaint the frame →
    /// settings_tweaked.png was pixel-identical to settings_open.png, the quarantined PR #83 re-QA bug this
    /// ticket fixes.) Proves, from GROUND TRUTH, input-event → param-change → repainted-frame end-to-end.
    ///
    /// CAPTURES the following frames (dev-console F3 first, then the player F1 drawer), then quits:
    ///   settings_closed.png — the gameplay frame BEFORE opening (the world, panel hidden).
    ///   settings_open.png   — the console OPEN at the shipped 1.0x scale, parked in a CORNER off the player
    ///                         (86cabeqj9 AC1/AC4).
    ///   settings_scaled.png — the console open AFTER dialing the `Console UI scale` row to its 0.5x min
    ///                         (86cabeqj9 soak NIT 1): the whole console (plate + text) reads visibly SMALLER
    ///                         vs settings_open.png — the live panel transform.scale changed.
    ///   settings_tweaked.png— the console open (back at 1.0x) AFTER driving the walk-speed slider to its max —
    ///                         the row readout reflects the new value, the live WasdMovement.moveSpeed changed
    ///                         (AC2), AND the differs-from-default badge shows on the dialed row (AC9).
    ///   settings_reset.png  — the console open AFTER reset-to-defaults: the live params reverted, the
    ///                         readouts/fields re-rendered to the defaults, the differs badge cleared (AC10).
    ///   settings_player_open.png       — the PLAYER Settings drawer (F1, 86cah8ukr split) OPEN: the built-exe
    ///                         evidence for the F1 render every DEV-console (F3) frame above skipped (belt/stack +
    ///                         warmth/hunger/thirst on/off + decay-rate rows).
    ///   settings_player_decayhidden.png— the F1 drawer AFTER flipping the WARMTH on/off toggle OFF: the warmth
    ///                         decay-rate slider row HIDES live (86cah8ukr AC1 conditional visibility) — proven
    ///                         from ground truth via SettingsCategory.IsDecaySliderVisible in the shipped build.
    /// and LOGS the 86cabeqj9 NIT proofs: NIT 1 — the `Console UI scale` row drives the panel scale live; NIT 2 —
    /// the F1/F2 de-conflict (console toggle key=F1, legacy-overlay master=F2, distinct; opening the console does
    /// NOT flip the legacy DebugOverlays.Visible flag → the two layers no longer share state).
    /// and LOGS the live-effect proof: the walk-speed param BEFORE vs AFTER the tweak (must differ), the
    /// zoom-range MIN/MAX clamping OrbitCamera.minDistance/maxDistance, the differs-from-default flag flipping
    /// on tweak + clearing on reset (AC9/AC10), and the registered entry count + archetypes (the extensible
    /// registry materialized in the shipped build).
    ///
    /// Inert unless launched with -verifySettings (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifySettings -captureDir &lt;dir&gt;
    /// </summary>
    public class SettingsVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifySettings")) StartCoroutine(Verify());
        }

        private IEnumerator Verify()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Let the scene + the panel's Start build the registry/view.
            for (int i = 0; i < 10; i++) yield return null;

            var panel = Object.FindAnyObjectByType<SettingsPanel>();
            var wasd = Object.FindAnyObjectByType<WasdMovement>();
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (panel == null)
            {
                Debug.LogError("[SettingsVerifyCapture] no SettingsPanel in the scene — the panel did not ship " +
                               "(component-not-serialized trap). Capture aborted.");
                Application.Quit();
                yield break;
            }

            // 1. CLOSED — the world before opening.
            panel.SetOpen(false);
            for (int i = 0; i < 5; i++) yield return null;
            ShotTo(Path.Combine(dir, "settings_closed.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 2. OPEN — the console renders over the dimmed world (AC1), parked in a CORNER off the player.
            //    AC4: cycle the corner once via the picker so the captured open frame proves the panel is NOT
            //    screen-centered (the #83 default that covered the player) — it sits in a corner now.
            panel.SetOpen(true);
            for (int i = 0; i < 8; i++) yield return null; // let the open transition play
            CycleCornerOnce(panel);                        // AC4 — reposition off the player
            for (int i = 0; i < 5; i++) yield return null; // let the re-park lay out
            var reg = panel.Registry;
            int count = reg != null ? reg.Count : 0;
            // AC2/AC3: the OPEN console alone must NOT gate world input (it is non-modal); only a focused typed
            // field gates. No field is focused here, so the gate must read FALSE — the live-tweak-while-playing
            // contract. (A #83-modal panel would log True here.)
            Debug.Log($"[SettingsVerifyCapture] console OPEN (non-modal) — registry has {count} settings; " +
                      $"worldInputGated={UiInputGate.CaptureWorldInput} (AC2/AC3: must be False — open alone " +
                      $"does NOT swallow locomotion/orbit; only a focused field does).");

            // 86cabeqj9 NIT 2 + 86cah8ukr SPLIT — F1/F2/F3 DE-CONFLICT (ground truth in the shipped build). The
            // verify-capture can't synthesize a key-down, but the DECOUPLE is machine-checkable: the DEV console
            // now opens on F3 (devToggleKey), the PLAYER Settings drawer on F1 (toggleKey), the legacy-overlay
            // master on F2 (all distinct), AND opening the console (SetOpen above) did NOT flip the legacy
            // DebugOverlays.Visible flag — proving F3↔console and F2↔legacy no longer share state.
            var legacyToggle = Object.FindAnyObjectByType<DebugOverlayToggle>();
            KeyCode consoleKey = panel.devToggleKey;
            KeyCode playerKey = panel.toggleKey;
            KeyCode legacyKey = legacyToggle != null ? legacyToggle.toggleKey : KeyCode.None;
            bool keysDistinct = consoleKey != legacyKey && consoleKey != playerKey && playerKey != legacyKey;
            Debug.Log($"[SettingsVerifyCapture] F1/F2/F3 DE-CONFLICT (NIT 2 + 86cah8ukr): devConsoleKey={consoleKey} " +
                      $"playerSettingsKey={playerKey} legacyOverlayKey={legacyKey} keysDistinct={keysDistinct} " +
                      $"consoleOpen={panel.IsOpen} legacyOverlaysVisible={DebugOverlays.Visible} " +
                      $"decoupled={(panel.IsOpen && !DebugOverlays.Visible)} (must be: devConsoleKey=F3, " +
                      $"playerSettingsKey=F1, legacyKey=F2, distinct=True, decoupled=True — opening the console " +
                      $"does NOT reveal the legacy overlays).");

            // #247 EMPTY-DRAWERS GUARD — the PR #247 build passed this gate while both drawers rendered header +
            // footer but ZERO rows (the flex-grow ScrollView collapsed against a zero-height drawer container).
            // frame_check.py only proves the whole FRAME isn't uniform (the green gameplay world passes), never
            // that the PANEL region has rows — so an empty drawer slipped straight through. Probe GROUND TRUTH:
            // the count of DEV rows actually VISIBLE (world rect overlapping the ScrollView viewport) + the
            // viewport's resolved height (the collapse measure — ~0 when empty), now that the console is open +
            // settled. verify_settings_gate.sh Check 4 FAILS if the visible count is 0.
            int devVisible = panel.VisibleRowCount(true);
            int devRouted = panel.RoutedRowCount(true);
            float devViewport = panel.RowsViewportHeight(true);
            Debug.Log($"[SettingsVerifyCapture] DEV rows visible: {devVisible} / {devRouted} routed " +
                      $"(viewportHeight={devViewport:F1}px; #247 empty-drawers guard — MUST be > 0; a collapsed " +
                      $"flex-grow ScrollView renders the header + footer but ZERO rows even though the " +
                      $"registry/handles built + drove live).");

            ShotTo(Path.Combine(dir, "settings_open.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 86cabeqj9 NIT 1 — CONSOLE UI SCALE. Dial the scale row DOWN (to its 0.5x min) and prove the
            // bound _uiScale changed from ground truth, then capture settings_scaled.png so the side-by-side vs
            // settings_open.png shows the whole console (plate + text) visibly SMALLER. Snapshot+restore the
            // scale PlayerPrefs key (soak hygiene — the next launch must boot at the shipped 1.0x default).
            var scaleEntry = reg?.Get(SettingsCatalog.ConsoleUiScaleId) as FloatSettingEntry;
            var scaleSnapshot = new System.Collections.Generic.List<PrefSnapshot>();
            if (scaleEntry != null) SnapshotFloat(scaleSnapshot, scaleEntry.PrefsKey);
            try
            {
                if (scaleEntry != null)
                {
                    float scaleBefore = scaleEntry.Value;
                    // 86cabe3e5 — drive a REAL ChangeEvent on the slider (NOT the entry setter + RefreshReadouts):
                    // the callback applies the live scale AND UI Toolkit repaints the captured frame.
                    float scaleApplied = panel.DriveFloatChangeEventForCapture(
                        SettingsCatalog.ConsoleUiScaleId, SettingsCatalog.ConsoleUiScaleMin); // 0.5x — visibly smaller
                    Debug.Log($"[SettingsVerifyCapture] CONSOLE UI SCALE tweak (NIT 1): before={Fmt(scaleBefore)} " +
                              $"setTo={Fmt(scaleApplied)} liveScale={Fmt(scaleEntry.Value)} " +
                              $"changedLive={(!Mathf.Approximately(scaleBefore, scaleEntry.Value))} differs=" +
                              $"{scaleEntry.DiffersFromDefault} (must change the panel transform.scale live).");
                }
                else
                {
                    Debug.LogError("[SettingsVerifyCapture] CONSOLE UI SCALE row MISSING from the registry — the " +
                                   "86cabeqj9 NIT 1 scale setting did not ship (the panel never registered it).");
                }
            }
            finally
            {
                RestorePrefs(scaleSnapshot); // leave the scale key as this run found it (no soak pollution)
            }
            for (int i = 0; i < 6; i++) yield return null; // let UI Toolkit re-layout at the new scale
            ShotTo(Path.Combine(dir, "settings_scaled.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // Restore the panel to 1.0x for the remaining frames (tweak/reset) so they read at the shipped scale.
            // Drive via the real ChangeEvent (86cabe3e5) so the slider + readout + transform all reset together;
            // then restore PlayerPrefs (the ChangeEvent write-through re-dirtied the scale key — no soak pollution).
            if (scaleEntry != null) { panel.DriveFloatChangeEventForCapture(SettingsCatalog.ConsoleUiScaleId, 1f); RestorePrefs(scaleSnapshot); }
            for (int i = 0; i < 4; i++) yield return null;

            // 86cabeqj9 NIT 3 — UI TEXT SCALE. DISTINCT from Console UI scale (chrome transform): dial the
            // `UI text scale` row UP (to its 2.0x max) and prove the bound _textScale changed from ground truth,
            // then capture settings_textscaled.png so the side-by-side vs settings_open.png shows the panel TEXT
            // visibly LARGER (the font resized, independent of the chrome). Snapshot+restore its PlayerPrefs key.
            var textEntry = reg?.Get(SettingsCatalog.ConsoleTextScaleId) as FloatSettingEntry;
            var textSnapshot = new System.Collections.Generic.List<PrefSnapshot>();
            if (textEntry != null) SnapshotFloat(textSnapshot, textEntry.PrefsKey);
            try
            {
                if (textEntry != null)
                {
                    float textBefore = textEntry.Value;
                    // 86cabe3e5 — REAL ChangeEvent (the callback resizes the fonts + repaints the captured frame).
                    float textApplied = panel.DriveFloatChangeEventForCapture(
                        SettingsCatalog.ConsoleTextScaleId, SettingsCatalog.ConsoleTextScaleMax); // 2.0x — visibly bigger text
                    Debug.Log($"[SettingsVerifyCapture] UI TEXT SCALE tweak (NIT 3): before={Fmt(textBefore)} " +
                              $"setTo={Fmt(textApplied)} liveScale={Fmt(textEntry.Value)} " +
                              $"changedLive={(!Mathf.Approximately(textBefore, textEntry.Value))} differs=" +
                              $"{textEntry.DiffersFromDefault} distinctFromUiScaleId=" +
                              $"{(SettingsCatalog.ConsoleTextScaleId != SettingsCatalog.ConsoleUiScaleId)} " +
                              $"(must resize the panel FONT live, SEPARATE from Console UI scale).");
                }
                else
                {
                    Debug.LogError("[SettingsVerifyCapture] UI TEXT SCALE row MISSING from the registry — the " +
                                   "86cabeqj9 NIT 3 text-scale setting did not ship (the panel never registered it).");
                }
            }
            finally
            {
                RestorePrefs(textSnapshot);
            }
            for (int i = 0; i < 6; i++) yield return null; // let UI Toolkit re-layout at the new font size
            ShotTo(Path.Combine(dir, "settings_textscaled.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // Restore text scale to 1.0x for the remaining frames (no soak pollution). Real ChangeEvent (86cabe3e5).
            if (textEntry != null) { panel.DriveFloatChangeEventForCapture(SettingsCatalog.ConsoleTextScaleId, 1f); RestorePrefs(textSnapshot); }
            for (int i = 0; i < 4; i++) yield return null;

            // 3. TWEAK walk speed to its slider max and prove the LIVE param changed (AC2).
            //
            // PLAYERPREFS HYGIENE (codereview #83): the SetValue/SetMax calls below write-through to PlayerPrefs
            // (the single persist authority — FloatSettingEntry/RangeSettingEntry). On the shared self-hosted /
            // soak machine those tweaked values would otherwise survive to the NEXT launch and pollute the
            // Sponsor's soak (it must start from SHIPPED defaults, not the verify run's max-walk / 18u-zoom).
            // Snapshot the exact keys this run touches, then RESTORE them before Quit (restore beats a blind
            // DeleteKey — it also preserves any value the machine genuinely had persisted before this run).
            var walk = reg?.Get(SettingsCatalog.WalkSpeedId) as FloatSettingEntry;
            var zoom = reg?.Get(SettingsCatalog.ZoomRangeId) as RangeSettingEntry;
            var prefsSnapshot = new System.Collections.Generic.List<PrefSnapshot>();
            if (walk != null) SnapshotFloat(prefsSnapshot, walk.PrefsKey);
            if (zoom != null) { SnapshotFloat(prefsSnapshot, zoom.PrefsKey + ".min"); SnapshotFloat(prefsSnapshot, zoom.PrefsKey + ".max"); }

            float walkBefore = wasd != null ? wasd.moveSpeed : float.NaN;
            float applied = float.NaN;

            // try/finally (codereview #83, Drew NIT): an exception ANYWHERE between the snapshot above and the
            // restore below would otherwise leak this run's max-walk / 18u-zoom tweaks into PlayerPrefs and
            // pollute the Sponsor's next soak (it must boot from SHIPPED defaults). finally guarantees the
            // snapshot is restored on any exit path. (Note: a coroutine body cannot `yield` inside try/finally,
            // so the post-tweak settle yields stay OUTSIDE the block — they touch no PlayerPrefs and the frame
            // they capture is unaffected by an early restore.)
            try
            {
                // 86cabe3e5 — drive the walk-speed tweak via a REAL UI Toolkit ChangeEvent on the bound slider
                // (the SAME event a user's drag fires), NOT the entry setter + RefreshReadouts. The ChangeEvent
                // flows through the panel binding → the callback drives WasdMovement.moveSpeed live (AC2) AND
                // UI Toolkit repaints the row, so settings_tweaked.png VISIBLY reflects the new value instead of
                // coming out pixel-identical to settings_open.png (the PR #83 re-QA bug this ticket fixes).
                if (walk != null) applied = panel.DriveFloatChangeEventForCapture(SettingsCatalog.WalkSpeedId, walk.Max);
                float walkAfter = wasd != null ? wasd.moveSpeed : float.NaN;
                Debug.Log($"[SettingsVerifyCapture] WALK SPEED tweak: before={Fmt(walkBefore)} " +
                          $"setTo={Fmt(applied)} liveAfter={Fmt(walkAfter)} " +
                          $"changedLive={(!float.IsNaN(walkAfter) && !Mathf.Approximately(walkBefore, walkAfter))} (AC2)");

                // Also drive + log the ZOOM range clamping the live camera (AC4) — likewise a REAL ChangeEvent on
                // the MinMaxSlider (86cabe3e5), preserving the current min end and moving the max down.
                if (zoom != null && orbit != null)
                {
                    float newMax = Mathf.Min(zoom.UpperLimit, 18f);
                    panel.DriveRangeChangeEventForCapture(SettingsCatalog.ZoomRangeId, zoom.MinValue, newMax);
                    float setMax = zoom.MaxValue;
                    Debug.Log($"[SettingsVerifyCapture] ZOOM range: setMax={Fmt(setMax)} -> orbit.maxDistance=" +
                              $"{Fmt(orbit.maxDistance)} (live system clamps to the range, AC4)");
                }

                // AC9 — the dialed-off-default rows now report DiffersFromDefault=true (the badge shows). Log
                // ground truth so the gate proves the badge state, not just the live value.
                if (walk != null)
                    Debug.Log($"[SettingsVerifyCapture] DIFFERS-FROM-DEFAULT after tweak: walk={walk.DiffersFromDefault}" +
                              (zoom != null ? $" zoom={zoom.DiffersFromDefault}" : "") + " (AC9 — must be True; the badge shows)");
            }
            finally
            {
                // Restore PlayerPrefs so the next launch (the Sponsor soak) loads shipped defaults, not this run's
                // tweaks — runs even if SetValue/SetMax threw between snapshot and here.
                RestorePrefs(prefsSnapshot);
            }

            // 86cabe3e5 — settings_tweaked.png VISIBLY shows the changed value because the walk/zoom tweaks above
            // were driven via REAL UI Toolkit ChangeEvents on the bound controls (DriveFloat/DriveRangeChangeEvent),
            // which repaint the row as a side effect of the dispatched event — no post-hoc RefreshReadouts crutch
            // needed. The PREVIOUS approach drove the entry setter directly + called RefreshReadouts to force a
            // repaint, but under -verifySettings that synthetic path did NOT repaint the captured frame, so
            // settings_tweaked.png came out pixel-identical to settings_open.png (the PR #83 re-QA bug; the gate's
            // visible-diff sub-check was quarantined for it). RestorePrefs() above only rewrites PlayerPrefs keys
            // (it does NOT re-apply to the live params), so the live walk/zoom values stay at the tweaked numbers
            // the ChangeEvent applied. Just settle frames so UI Toolkit lays out + renders before the screenshot.
            for (int i = 0; i < 5; i++) yield return null;
            ShotTo(Path.Combine(dir, "settings_tweaked.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            // 4. RESET-TO-DEFAULTS END-TO-END (86cabeqj9 AC10). The footer button calls Registry.ResetAll()
            //    then RefreshReadouts(); do exactly that here and prove FROM GROUND TRUTH that the live param
            //    reverted, the readouts/fields re-rendered to the defaults, AND the differs badge cleared.
            //    ResetAll writes the default VALUES back through the entry setters → re-dirties the SAME
            //    PlayerPrefs keys the finally above restored, so re-restore the snapshot after (no soak pollution).
            try
            {
                if (reg != null) reg.ResetAll();   // reverts every live param to its registration-time default
                float walkAfterReset = wasd != null ? wasd.moveSpeed : float.NaN;
                Debug.Log($"[SettingsVerifyCapture] RESET-TO-DEFAULTS: walk liveAfterReset={Fmt(walkAfterReset)} " +
                          (walk != null ? $"revertedToDefault={Mathf.Approximately(walkAfterReset, walk.Default)} " : "") +
                          (walk != null ? $"walkDiffers={walk.DiffersFromDefault}" : "") +
                          (zoom != null ? $" zoomDiffers={zoom.DiffersFromDefault}" : "") +
                          " (AC10 — live reverted + differs flags must be False)");
            }
            finally
            {
                RestorePrefs(prefsSnapshot); // ResetAll re-wrote the keys → leave them as this run found them
            }

            panel.RefreshReadouts();           // re-render readouts/fields to the defaults + clear the badges (AC10)
            for (int i = 0; i < 5; i++) yield return null;
            ShotTo(Path.Combine(dir, "settings_reset.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            // 5. PLAYER SETTINGS DRAWER (F1, 86cah8ukr split) — every frame above rendered the DEV console (F3).
            //    The split moved the player-facing rows (belt/stack + warmth/hunger/thirst on/off + decay sliders)
            //    into a SEPARATE F1 drawer the dev-console capture NEVER covered, so the shipped-build gate had a
            //    blind spot on the F1 render (Devon's own #247 capture NIT). Close the dev console, open the player
            //    drawer, and capture it — the automated backstop to the Sponsor soak's F1 pass (a UIDocument-render
            //    surface needs BUILT-EXE evidence, not editor-only; unity-conventions.md §editor-vs-runtime).
            panel.SetOpen(false);              // dev console away → a clean player-only frame
            panel.SetPlayerOpen(true);
            for (int i = 0; i < 8; i++) yield return null;  // let the open transition play + lay out
            // #247 EMPTY-DRAWERS GUARD (F1 half) — same ground-truth row-visibility probe for the PLAYER drawer.
            // The Sponsor soak showed F1 rendering ONLY "Settings" + "Reset to defaults" with zero rows (belt/stack
            // + warmth/hunger/thirst on/off + decay sliders all missing); this asserts the F1 rows are visible.
            int playerVisible = panel.VisibleRowCount(false);
            int playerRouted = panel.RoutedRowCount(false);
            float playerViewport = panel.RowsViewportHeight(false);
            Debug.Log($"[SettingsVerifyCapture] PLAYER rows visible: {playerVisible} / {playerRouted} routed " +
                      $"(viewportHeight={playerViewport:F1}px; #247 empty-drawers guard — MUST be > 0; the F1 " +
                      $"drawer showed header + footer but ZERO rows).");
            Debug.Log($"[SettingsVerifyCapture] PLAYER drawer OPEN (F1, 86cah8ukr): playerOpen={panel.IsPlayerOpen} " +
                      $"devOpen={panel.IsOpen} worldInputGated={UiInputGate.CaptureWorldInput} (must be: playerOpen=" +
                      $"True, devOpen=False, gated=False — open alone is non-modal, only a focused field gates).");
            ShotTo(Path.Combine(dir, "settings_player_open.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 5b. CONDITIONAL VISIBILITY (86cah8ukr AC1) — a per-need decay-rate slider shows ONLY while its need's
            //     on/off toggle is ON; flip the toggle OFF and the slider row hides LIVE (display:None). Drive the
            //     WARMTH need's decayEnabled flag DIRECTLY on the live component (NOT the entry setter → no
            //     PlayerPrefs write-through → no soak pollution, so no snapshot/restore dance needed), then
            //     RefreshReadouts (which runs ApplyConditionalVisibility + repaints the toggle to Off). Capture the
            //     hidden frame + log the visibility flip from GROUND TRUTH via the SAME single-source decision the
            //     panel uses (SettingsCategory.IsDecaySliderVisible). Restore the live flag after (never touched Prefs).
            var warmthNeed = Object.FindAnyObjectByType<WarmthNeed>();
            if (warmthNeed != null && panel.Registry != null)
            {
                bool warmthBefore = warmthNeed.decayEnabled;
                warmthNeed.decayEnabled = true;   // ensure ON so the decay slider starts visible
                panel.RefreshReadouts();
                bool visibleWhenOn = SettingsCategory.IsDecaySliderVisible(panel.Registry, SettingsCatalog.WarmthDecayId);
                warmthNeed.decayEnabled = false;  // flip OFF → the warmth decay-rate slider must hide LIVE (AC1)
                panel.RefreshReadouts();
                bool visibleWhenOff = SettingsCategory.IsDecaySliderVisible(panel.Registry, SettingsCatalog.WarmthDecayId);
                for (int i = 0; i < 5; i++) yield return null;   // let the display:None re-layout land
                Debug.Log($"[SettingsVerifyCapture] DECAY-SLIDER conditional visibility (86cah8ukr AC1): " +
                          $"warmthOn->visible={visibleWhenOn} warmthOff->visible={visibleWhenOff} " +
                          $"hidesOnToggleOff={(visibleWhenOn && !visibleWhenOff)} (must be: On->True, Off->False, " +
                          $"hides=True — a disabled need has no rate to tune).");
                ShotTo(Path.Combine(dir, "settings_player_decayhidden.png"));
                yield return new WaitForEndOfFrame();
                yield return null;
                warmthNeed.decayEnabled = warmthBefore;   // restore the live flag (no PlayerPrefs was ever written)
                panel.RefreshReadouts();
            }
            else
            {
                Debug.LogError("[SettingsVerifyCapture] cannot verify the F1 decay-slider hide-on-toggle-off " +
                               "(86cah8ukr AC1) — WarmthNeed missing or registry null in the shipped scene.");
            }
            panel.SetPlayerOpen(false);        // leave the player drawer closed (the shipped default)
            for (int i = 0; i < 3; i++) yield return null;

            Debug.Log("[SettingsVerifyCapture] verification complete (PlayerPrefs restored) -> " + dir);
            Application.Quit();
        }

        // AC4 — cycle the console corner once via the SAME public path the picker button uses (cycle + persist
        // + re-park), so the captured open frame proves the panel sits in a corner OFF the player, not screen-
        // center. The persisted-corner key is restored to its prior state afterward so a soak isn't left in a
        // surprise corner (PlayerPrefs-hygiene, like the value-snapshot/restore above).
        private void CycleCornerOnce(SettingsPanel panel)
        {
            if (panel == null) return;
            bool hadCorner = PlayerPrefs.HasKey(ConsolePosition.PrefsKey);
            int priorCorner = hadCorner ? PlayerPrefs.GetInt(ConsolePosition.PrefsKey) : 0;

            panel.CycleCorner();   // the real AC4 path: next corner, persist, re-park live

            if (hadCorner) PlayerPrefs.SetInt(ConsolePosition.PrefsKey, priorCorner);
            else PlayerPrefs.DeleteKey(ConsolePosition.PrefsKey);
            PlayerPrefs.Save();
        }

        private static string Fmt(float v) => float.IsNaN(v) ? "N/A" : v.ToString("F2");

        // A captured PlayerPrefs float key + its prior presence/value, so the verify run can leave PlayerPrefs
        // exactly as it found them (codereview #83 — no soak-defaults pollution).
        private struct PrefSnapshot { public string Key; public bool Existed; public float Value; }

        private static void SnapshotFloat(System.Collections.Generic.List<PrefSnapshot> into, string key)
        {
            bool existed = PlayerPrefs.HasKey(key);
            into.Add(new PrefSnapshot { Key = key, Existed = existed, Value = existed ? PlayerPrefs.GetFloat(key) : 0f });
        }

        private static void RestorePrefs(System.Collections.Generic.List<PrefSnapshot> snapshot)
        {
            foreach (var s in snapshot)
            {
                if (s.Existed) PlayerPrefs.SetFloat(s.Key, s.Value); // had a genuine persisted value → put it back
                else PlayerPrefs.DeleteKey(s.Key);                   // verify run created it → remove it
            }
            PlayerPrefs.Save();
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[SettingsVerifyCapture] wrote " + file);
        }

        private string ResolveDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return Path.GetFullPath(args[i + 1]);
            string baseDir = Application.isEditor
                ? Path.Combine(Application.dataPath, "..", subDir)
                : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", subDir);
            return Path.GetFullPath(baseDir);
        }

        private bool HasArg(string flag)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == flag) return true;
            return false;
        }
    }
}
