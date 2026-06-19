using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FarHorizon
{
    /// <summary>
    /// STYLE-CHECKPOINT shipped-build capture of the in-house re-made hero axe (ticket 86cabh907,
    /// Route A weapon set). Sibling of AxeVerifyCapture — but standalone: it does NOT touch the
    /// held-axe rig (that generalization is the NEXT dispatch). It loads a self-contained
    /// "WeaponAxeStand" prefab from Resources (the flint axe + a simple stand, with the shared
    /// Mat_WeaponPalette applied), parks a post-enabled capture camera framing the whole axe via
    /// the deterministic VerifyCaptureFraming math, and captures wpn_axe_01.png from the BUILT exe.
    ///
    /// This is the shipped-build evidence the capture gate requires for the knapped-flint look
    /// (geometry + the 2 flint palette shades reading in-engine, not just a Blender render).
    ///
    /// Inert unless launched with -verifyWeaponAxe. MUST run WINDOWED (ScreenCapture needs a real
    /// swapchain — spike iter-4 / unity-conventions.md).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyWeaponAxe -captureDir &lt;dir&gt;
    /// Captures wpn_axe_01.png. Quits non-zero if the prefab is missing or bounds never settle.
    /// </summary>
    public class WeaponSetVerifyCapture : MonoBehaviour
    {
        public const string ResourcePath = "WeaponAxeStand"; // Assets/Resources/WeaponAxeStand.prefab
        public string subDir = "Captures";
        public float frameFill = 0.66f;

        void Start()
        {
            if (HasArg("-verifyWeaponAxe"))
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

            var inst = Object.Instantiate(prefab);
            inst.transform.position = Vector3.zero;
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

            // Three-quarter view from the cutting-edge side so the knapped facets + the wedge
            // taper + the lashing all read. Fixed viewDir (deterministic).
            Vector3 viewDir = new Vector3(-0.85f, 0.40f, -0.95f);
            float aspect = Screen.width > 0 && Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            var frame = VerifyCaptureFraming.ComputeFrame(wb.center, wb.size, viewDir, 38f, aspect, frameFill);

            var camGo = new GameObject("WeaponAxeCaptureCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.46f, 0.52f); // neutral slate — honest flint read
            cam.fieldOfView = 38f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.SetPositionAndRotation(frame.position, frame.rotation);
            Debug.Log($"[WeaponSetVerifyCapture] cam {frame.position} -> {frame.lookAt} " +
                      $"(bounds size={wb.size} dist={frame.distance:F2}) stamp={BuildInfo.Stamp}");

            for (int i = 0; i < 8; i++) yield return null;
            yield return new WaitForEndOfFrame();
            string file = Path.Combine(dir, "wpn_axe_01.png");
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
