using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the BEACH OCEAN (drew/beach-water-scene; Uma
    /// beach-water-direction §4 task F — the orbit-to-sea framing check).
    ///
    /// The default orbit framing (yaw 0) looks INLAND (trees / craft / fire — correct for the survival
    /// loop). The ocean sits seaward, BEHIND the spawn — so the standard -captureGate frames cannot
    /// judge the water. Uma §2: "the player must be able to orbit ~180 and find the bright sea + their
    /// landing point behind them." This hook orbits the camera to the seaward yaw, lets it settle, and
    /// captures the frame the Sponsor will judge: the bright teal sea filling the frame, the foam-lined
    /// shore in front of it, the far edge lost in the warm fog haze. The HUD build stamp is visible so
    /// the capture self-identifies its build (the stale-stamp gate).
    ///
    /// Inert unless launched with -verifySea (so the normal game / boot capture is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifySea [-captureDir &lt;dir&gt;]
    /// Captures: sea_inland.png (the default inland view, for contrast) + sea_seaward.png (orbited to
    /// the ocean), then quits. MUST run WINDOWED, not -batchmode (ScreenCapture needs a real swapchain —
    /// the spike iter-4 lesson; editor RenderTexture mis-renders URP, hero-axe PR #21).
    /// </summary>
    public class SeaVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // The seaward orbit yaw — 180 from the inland default points the camera back over the spawn at
        // the sea the castaway washed in from.
        public float seawardYaw = 180f;
        // Drop the pitch toward the horizon (OrbitCamera clamps to minPitch, now widened to 8) and pull
        // back so the seaward view looks OUT to the open sea, not down at the near sand. The trace
        // (OceanCameraDiag, 2026-06-13) proved that at the OLD 35 floor the camera centre only reached
        // the beach (~Z+4) — the sea entered frame as a far fogged sliver (the "grey pond" soak report).
        // Capture at the new pitch FLOOR (8) — this is the "all the way down toward the horizon" view the
        // Sponsor explicitly asked to be able to reach. At pitch 8 the look is near-horizontal so the
        // beach foreground shrinks and the bright teal sea fills the most frame to the fogged horizon.
        public float seawardPitch = 8f;
        public float seawardDistance = 22f;
        public int warmupFrames = 8;
        public int settleFrames = 16;

        void Start()
        {
            if (HasArg("-verifySea"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            Debug.Log("[SeaVerifyCapture] orbit camera found: " + (orbit != null));

            // Warm-up so the first shot has real content (skybox/fog/post all present).
            for (int i = 0; i < warmupFrames; i++) yield return null;

            // 1. The default INLAND view (yaw 0) — captured for contrast (the loop-facing framing).
            ShotTo(Path.Combine(dir, "sea_inland.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 2. Orbit to the SEAWARD yaw, drop the pitch toward the horizon + pull back, let the
            //    follow/lerp settle, then capture the ocean.
            // The pitch is overridable via -seawardPitch <deg> so a diagnostic run can also capture the
            // DEFAULT gameplay pitch (55) — the angle the player actually soaks at — not only the
            // near-horizontal pitch-8 "look out to the open sea" framing (drew shoreline diagnosis).
            float pitch = ArgFloat("-seawardPitch", seawardPitch);
            float dist = ArgFloat("-seawardDistance", seawardDistance);
            if (orbit != null)
            {
                orbit.SetYaw(seawardYaw);
                orbit.SetPitch(pitch);            // clamped to [minPitch,maxPitch] by OrbitCamera
                orbit.SetDistance(dist);          // -seawardDistance overrides (default gameplay dist=14)
                Debug.Log("[SeaVerifyCapture] orbited seaward: yaw=" + orbit.Yaw +
                          " pitch=" + orbit.Pitch + " dist=" + orbit.Distance);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            ShotTo(Path.Combine(dir, "sea_seaward.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[SeaVerifyCapture] verification complete -> " + dir);
            Application.Quit();
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[SeaVerifyCapture] wrote " + file);
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

        // Read a float arg (-flag <value>); falls back to the default if absent/unparseable.
        private float ArgFloat(string flag, float fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag && float.TryParse(args[i + 1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v))
                    return v;
            return fallback;
        }
    }
}
