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
    /// this drives the panel + registry programmatically (the SAME SetOpen / SettingEntry.SetValue paths a
    /// real interaction drives) and proves, from GROUND TRUTH, that the live param actually changed.
    ///
    /// CAPTURES three frames, then quits:
    ///   settings_closed.png — the gameplay frame BEFORE opening (the world, panel hidden).
    ///   settings_open.png   — the panel OPEN over the dimmed world (AC1 — it renders, on-tone).
    ///   settings_tweaked.png— the panel open AFTER driving the walk-speed slider to its max (AC2 — the row
    ///                         readout reflects the new value; the live WasdMovement.moveSpeed changed).
    /// and LOGS the live-effect proof: the walk-speed param BEFORE vs AFTER the tweak (must differ), the
    /// zoom-range MIN/MAX clamping OrbitCamera.minDistance/maxDistance (AC4), and the registered entry count
    /// + archetypes (the extensible registry materialized in the shipped build).
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

            // 2. OPEN — the panel renders over the dimmed world (AC1).
            panel.SetOpen(true);
            for (int i = 0; i < 8; i++) yield return null; // let the open transition play
            var reg = panel.Registry;
            int count = reg != null ? reg.Count : 0;
            Debug.Log($"[SettingsVerifyCapture] panel OPEN — registry has {count} settings; " +
                      $"worldInputGated={UiInputGate.CaptureWorldInput} (locomotion/orbit swallowed while open).");
            ShotTo(Path.Combine(dir, "settings_open.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 3. TWEAK walk speed to its slider max and prove the LIVE param changed (AC2).
            float walkBefore = wasd != null ? wasd.moveSpeed : float.NaN;
            var walk = reg?.Get(SettingsCatalog.WalkSpeedId) as FloatSettingEntry;
            float applied = float.NaN;
            if (walk != null) applied = walk.SetValue(walk.Max);
            float walkAfter = wasd != null ? wasd.moveSpeed : float.NaN;
            Debug.Log($"[SettingsVerifyCapture] WALK SPEED tweak: before={Fmt(walkBefore)} " +
                      $"setTo={Fmt(applied)} liveAfter={Fmt(walkAfter)} " +
                      $"changedLive={(!float.IsNaN(walkAfter) && !Mathf.Approximately(walkBefore, walkAfter))} (AC2)");

            // Also drive + log the ZOOM range clamping the live camera (AC4).
            var zoom = reg?.Get(SettingsCatalog.ZoomRangeId) as RangeSettingEntry;
            if (zoom != null && orbit != null)
            {
                float newMax = Mathf.Min(zoom.UpperLimit, 18f);
                float setMax = zoom.SetMax(newMax);
                Debug.Log($"[SettingsVerifyCapture] ZOOM range: setMax={Fmt(setMax)} -> orbit.maxDistance=" +
                          $"{Fmt(orbit.maxDistance)} (live system clamps to the range, AC4)");
            }

            for (int i = 0; i < 5; i++) yield return null;
            ShotTo(Path.Combine(dir, "settings_tweaked.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[SettingsVerifyCapture] verification complete -> " + dir);
            Application.Quit();
        }

        private static string Fmt(float v) => float.IsNaN(v) ? "N/A" : v.ToString("F2");

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
