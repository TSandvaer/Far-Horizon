using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FarHorizon
{
    /// <summary>
    /// Shipped-build capture of the in-house weapon SET (ticket 86cabh907, Route A) — the 3-tier
    /// 15-node weapon set (wood/stone/iron tiers x axe/pickaxe/knife/sword/spear) lined up, all on the
    /// shared Mat_WeaponPalette. Sibling
    /// of AxeVerifyCapture — standalone: it does NOT touch the held-tool rig. It loads a self-contained
    /// "WeaponSetLineup" prefab from Resources (the 3-tier 15-node set in wood/stone/iron rows, shared material), parks a
    /// post-enabled capture camera framing the whole family via the deterministic VerifyCaptureFraming
    /// math, and captures weapon_set.png from the BUILT exe.
    ///
    /// This is the shipped-build evidence the capture gate requires for the family look (faceted shading
    /// + shared palette + edge-bevel + flint reading in-engine as ONE family, not just a Blender render).
    ///
    /// Inert unless launched with -verifyWeaponSet (legacy -verifyWeaponAxe still accepted). MUST run
    /// WINDOWED (ScreenCapture needs a real swapchain — spike iter-4 / unity-conventions.md).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyWeaponSet -captureDir &lt;dir&gt;
    /// Captures weapon_set.png. Quits non-zero if the prefab is missing or bounds never settle.
    /// </summary>
    public class WeaponSetVerifyCapture : MonoBehaviour
    {
        public const string ResourcePath = "WeaponSetLineup"; // Assets/Resources/WeaponSetLineup.prefab
        public string subDir = "Captures";
        public float frameFill = 0.78f;

        void Start()
        {
            if (HasArg("-verifyWeaponSet") || HasArg("-verifyWeaponAxe"))
                StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[WeaponSetVerifyCapture] Resources/" + ResourcePath +
                               " missing — WeaponPackAssetGen must build it before the build step");
                yield return null; Application.Quit(1); yield break;
            }

            // Spawn the lineup FAR from the gameplay scene (high up) so the capture frames ONLY the
            // weapon family against the clear colour — the live world (grass, the held axe) sits at the
            // origin and would otherwise bleed into the shot. Distance + a tight frame + the camera's own
            // clear is enough.
            var inst = Object.Instantiate(prefab);
            Vector3 farOrigin = new Vector3(0f, 500f, 0f);
            inst.transform.position = farOrigin;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) r.enabled = true;

            // Settle a valid bounds (one frame for transforms/renderers to go live).
            Bounds wb = new Bounds(); bool valid = false;
            for (int attempt = 0; attempt < 30 && !valid; attempt++)
            {
                yield return null;
                wb = Encapsulate(inst.GetComponentsInChildren<Renderer>(true));
                valid = wb.size.magnitude > 0.05f;
            }
            if (!valid)
            {
                Debug.LogError("[WeaponSetVerifyCapture] axe bounds never settled (last=" + wb.size + ")");
                Application.Quit(1); yield break;
            }

            // Three-quarter front view so all the tier-row silhouettes + the faceted shading + the edge-bevel
            // + flint reads sit in one frame. Fixed viewDir (deterministic).
            Vector3 viewDir = new Vector3(-0.30f, 0.32f, -1.0f);
            float aspect = Screen.width > 0 && Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            var frame = VerifyCaptureFraming.ComputeFrame(wb.center, wb.size, viewDir, 38f, aspect, frameFill);

            // Disable any existing cameras so ONLY the capture camera renders the screen (the gameplay
            // cameras would otherwise composite the live world behind/around the axe).
            foreach (var existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.enabled = false;

            var camGo = new GameObject("WeaponAxeCaptureCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.46f, 0.52f); // neutral slate — honest flint read
            cam.fieldOfView = 38f;
            cam.depth = 100f;        // render last / on top
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;  // axe is ~1m; tight far-clip keeps the distant world out
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.SetPositionAndRotation(frame.position, frame.rotation);
            Debug.Log($"[WeaponSetVerifyCapture] cam {frame.position} -> {frame.lookAt} " +
                      $"(bounds size={wb.size} dist={frame.distance:F2}) stamp={BuildInfo.Stamp}");

            for (int i = 0; i < 8; i++) yield return null;
            yield return new WaitForEndOfFrame();
            string file = Path.Combine(dir, "weapon_set.png");
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[WeaponSetVerifyCapture] wrote " + file);
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[WeaponSetVerifyCapture] complete -> " + dir);
            Application.Quit(0);
        }

        private static Bounds Encapsulate(Renderer[] rs)
        {
            Bounds b = new Bounds(); bool init = false;
            foreach (var r in rs)
            {
                if (r == null || !(r is MeshRenderer) || !r.enabled) continue;
                if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
            }
            return b;
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
