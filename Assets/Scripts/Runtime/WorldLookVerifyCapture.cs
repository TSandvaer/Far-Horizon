using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the WORLD-LOOK POLISH (ticket 86ca8t9pq — Uma world-
    /// look brief §1 clouds + §2 vista + §3 sky-tint). Uma's per-surface capture gate wants orbit-cam
    /// frames at the DEFAULT gameplay pitch (clouds in the upper third) AND a LOW horizon-ward pitch
    /// (the vista layers + the sky dissolve), from the GAMEPLAY orbit camera with the REAL scene
    /// lighting/post (the false-green-capture lesson, unity-conventions.md 86ca8ca1m — an isolated hero
    /// frame lies).
    ///
    /// Captures (then quits):
    ///   worldlook_default.png — default orbit pitch (55), inland: clouds drifting in the upper third.
    ///   worldlook_drift.png   — SAME framing after a real-time drift window: the same clouds have
    ///                            visibly moved (proves the slow lateral drift across two frames, Uma §1).
    ///   worldlook_horizon.png — low pitch toward the horizon: the layered vista (near band + far rings)
    ///                            dissolving into the warm gradient sky with NO seam (Uma §2/§3).
    ///   worldlook_horizon2.png— a second horizon yaw (the seaward arc) so the wrap-around vista + sky
    ///                            are judged from more than one angle (the angle-mismatch false-green class).
    ///
    /// Inert unless launched with -verifyWorldLook (the normal game / boot capture is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyWorldLook [-captureDir &lt;dir&gt;]
    /// MUST run WINDOWED, not -batchmode (ScreenCapture needs a real swapchain — spike iter-4 lesson;
    /// editor RenderTexture mis-renders URP, hero-axe PR #21).
    /// </summary>
    public class WorldLookVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        public float defaultPitch = 55f;   // the locked gameplay default — clouds in the upper third
        public float defaultDistance = 16f;
        public float horizonPitch = 10f;   // low toward the horizon (clamped to OrbitCamera.minPitch)
        public float horizonDistance = 22f;
        public float horizonYawA = 90f;    // FACE the inland mountain cluster (azimuth ~90deg, +Z forward) —
                                           // so the grounded faceted islands are judged head-on (Drew soak-rework)
        public float horizonYawB = 150f;   // a second arc so the vista is judged from >1 angle
        public int warmupFrames = 10;
        public int settleFrames = 16;
        // The drift window between the two cloud frames: long enough that a 0.2-0.5 u/s cloud moves a
        // visible amount (>= ~1u) so the drift is provable across the pair.
        public float driftWindowSeconds = 3.0f;

        void Start()
        {
            if (HasArg("-verifyWorldLook"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // DIAGNOSTIC (-wlDiag, INERT read-only): dump the actual runtime sky/fog/ambient state so a
            // washed / wrong-look frame is diagnosed from ground truth, not re-hypothesized (the diagnose-
            // via-trace discipline). This earned its keep: the first impl's gradient skybox drew OVER the
            // geometry (washing the whole frame to the horizon colour) — the -wlDiag dump + a per-pixel
            // probe localised it to the skybox render state (the fix: standard skybox-pass state, not a
            // positionCS.xyww depth force). No mutation — purely a state dump.
            if (HasArg("-wlDiag"))
            {
                var camD = Camera.main;
                Debug.Log($"[wlDiag] fog={RenderSettings.fog} mode={RenderSettings.fogMode} " +
                          $"density={RenderSettings.fogDensity} fogColor={RenderSettings.fogColor} " +
                          $"ambientMode={RenderSettings.ambientMode} " +
                          $"skybox={(RenderSettings.skybox != null ? RenderSettings.skybox.shader.name : "null")} " +
                          $"camFar={(camD != null ? camD.farClipPlane : -1)}");
            }

            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            Debug.Log("[WorldLookVerifyCapture] orbit camera found: " + (orbit != null));
            var clouds = Object.FindObjectsByType<CloudDrift>(FindObjectsSortMode.None);
            Debug.Log("[WorldLookVerifyCapture] clouds in scene: " + clouds.Length);

            for (int i = 0; i < warmupFrames; i++) yield return null;

            // 1. DEFAULT gameplay pitch (clouds in the upper third). Log a cloud's start pos so the drift
            //    is provable from the trace too (not just the pixels).
            if (orbit != null)
            {
                orbit.SetYaw(0f);
                orbit.SetPitch(defaultPitch);
                orbit.SetDistance(defaultDistance);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            Vector3 cloud0 = clouds.Length > 0 ? clouds[0].transform.position : Vector3.zero;
            Debug.Log("[WorldLookVerifyCapture] cloud0 before drift: " + cloud0);
            ShotTo(Path.Combine(dir, "worldlook_default.png"));
            yield return new WaitForEndOfFrame();

            // 2. DRIFT window — let real time pass so the clouds move, then re-shoot the same framing.
            yield return new WaitForSeconds(driftWindowSeconds);
            Vector3 cloud1 = clouds.Length > 0 ? clouds[0].transform.position : Vector3.zero;
            Debug.Log("[WorldLookVerifyCapture] cloud0 after drift: " + cloud1 +
                      " (moved " + (cloud1 - cloud0).magnitude.ToString("F2") + "u)");
            ShotTo(Path.Combine(dir, "worldlook_drift.png"));
            yield return new WaitForEndOfFrame();

            // 3. LOW horizon pitch, inland arc — the layered vista dissolving into the warm sky.
            if (orbit != null)
            {
                orbit.SetYaw(horizonYawA);
                orbit.SetPitch(horizonPitch);
                orbit.SetDistance(horizonDistance);
                Debug.Log("[WorldLookVerifyCapture] horizon A: yaw=" + orbit.Yaw + " pitch=" + orbit.Pitch);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            ShotTo(Path.Combine(dir, "worldlook_horizon.png"));
            yield return new WaitForEndOfFrame();

            // 4. LOW horizon pitch, second arc — the wrap-around vista + sky from another angle.
            if (orbit != null)
            {
                orbit.SetYaw(horizonYawB);
                orbit.SetPitch(horizonPitch);
                orbit.SetDistance(horizonDistance);
                Debug.Log("[WorldLookVerifyCapture] horizon B: yaw=" + orbit.Yaw + " pitch=" + orbit.Pitch);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            ShotTo(Path.Combine(dir, "worldlook_horizon2.png"));
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[WorldLookVerifyCapture] verification complete -> " + dir);
            Application.Quit();
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[WorldLookVerifyCapture] wrote " + file);
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
