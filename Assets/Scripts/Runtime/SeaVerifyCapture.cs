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
        // At pitch ~12 the centre reaches the coastline (~Z+0.4) so the bright teal sea fills the upper
        // frame to the fogged horizon. 12 (a touch above the new 8 floor) keeps a steady, non-grazing
        // horizon-ward framing.
        public float seawardPitch = 12f;
        public float seawardDistance = 24f;
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
            if (orbit != null)
            {
                orbit.SetYaw(seawardYaw);
                orbit.SetPitch(seawardPitch);     // clamped to minPitch by OrbitCamera
                orbit.SetDistance(seawardDistance);
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
    }
}
