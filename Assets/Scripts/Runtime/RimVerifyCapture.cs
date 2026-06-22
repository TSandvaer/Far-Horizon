using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build A/B capture for the Fresnel/rim-light term on
    /// FarHorizon/LowPolyVertexColor (ticket 86caamnnj — Erik R&D §C / Rank 4; Daniel Ilett / Minions Art
    /// rim idiom). Sibling of FlatShadingVerifyCapture / RockVerifyCapture: a -verifyRim flag that renders
    /// ONE demonstrator prop TWICE from an IDENTICAL camera — _RimIntensity = 0 (OFF) then _RimIntensity > 0
    /// (dialed) — so a reviewer (Devon / Tess / Sponsor) re-runs it and judges the silhouette-highlight
    /// difference from a SHIPPED frame, not the editor (editor-vs-runtime shader-render trap — editor capture
    /// is necessary-NOT-sufficient; AC4 makes the shipped frame the final gate).
    ///
    /// WHY a WELDED SMOOTH SPHERE is the demonstrator: a sphere's silhouette grazes the view all around its
    /// edge, so the rim (maximal at the grazing angle) reads as an unambiguous wrap-around edge glow. The mesh
    /// + material are IDENTICAL between the two frames — ONLY `_RimIntensity` differs (0 → dialed) — so the
    /// frames isolate the rim's effect. _RimPower is set to ~2 for the soft-silhouette highlight AC3 calls for.
    /// The prop rides the GAMEPLAY render path (Zone-D post + scene lighting) so the look is
    /// gameplay-representative (the false-green-capture fix).
    ///
    /// This is VERIFICATION-ONLY runtime geometry (it exists only during the -verifyRim run, then the exe
    /// quits), so building it at runtime is correct — it is NOT shipped scene content that must serialize. The
    /// COMPONENT itself is serialized into Boot.unity editor-time (added in BootstrapProject) per the
    /// component-in-source-not-serialized trap; CaptureGateSceneTests guards its presence. Inert unless launched
    /// with -verifyRim:
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyRim [-captureDir &lt;dir&gt;]
    /// Captures rim_off.png then rim_on.png, then quits. MUST run WINDOWED (ScreenCapture needs a real
    /// swapchain). Quits non-zero if the FarHorizon/LowPolyVertexColor shader does not resolve (a build-side
    /// regression signal: the shader was stripped or renamed).
    /// </summary>
    public class RimVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        public float viewPitch = 18f;       // a low-ish orbit so the sphere's full silhouette ring reads
        public float viewDistance = 3.2f;   // close enough that the rim edge is clearly readable
        public float demoRimIntensity = 0.9f; // dialed-up so the rim is unmistakable in the A/B
        public float demoRimPower = 2f;     // AC3: ~2 = soft silhouette highlight
        public int warmupFrames = 8;
        public int settleFrames = 14;

        private const string ShaderName = "FarHorizon/LowPolyVertexColor";

        void Start()
        {
            if (HasArg("-verifyRim"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var shader = Shader.Find(ShaderName);
            Debug.Log("[RimVerifyCapture] shader resolved: " + (shader != null));
            if (shader == null)
            {
                Debug.LogError("[RimVerifyCapture] FarHorizon/LowPolyVertexColor did not resolve — " +
                               "the shader was stripped/renamed (build-side regression signal)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            // Demonstrator prop: a welded smooth-normalled sphere (Unity's built-in sphere). Place it well
            // clear of the world geometry so the frame is the prop + the gradient sky behind it.
            Vector3 propPos = new Vector3(0f, 200f, 0f);
            var prop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            prop.name = "RimDemoSphere";
            prop.transform.position = propPos;
            prop.transform.localScale = Vector3.one * 1.4f;
            var col = prop.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // ONE material instance; flip ONLY _RimIntensity between frames so the A/B isolates the rim.
            var mat = new Material(shader) { name = "RimDemoMat" };
            if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", new Color(0.45f, 0.50f, 0.58f, 1f)); // cool stone so the warm rim pops
            if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", demoRimPower);
            prop.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // A dedicated post-enabled camera framed on the prop (independent of the gameplay orbit). Renders
            // the Zone-D post Volume + skybox so the capture matches the gameplay look (false-green fix).
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null)
            {
                var ocam = orbit.GetComponent<Camera>();
                if (ocam != null) ocam.enabled = false;
            }
            var camGo = new GameObject("RimVerifyCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 40f;
            var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            Quaternion rot = Quaternion.Euler(viewPitch, 0f, 0f);
            camGo.transform.position = propPos + rot * new Vector3(0f, 0f, -viewDistance);
            camGo.transform.LookAt(propPos);

            for (int i = 0; i < warmupFrames; i++) yield return null;

            // Frame A — OFF (rim intensity 0, the default no-op path).
            mat.SetFloat("_RimIntensity", 0f);
            Debug.Log("[RimVerifyCapture] frame OFF: _RimIntensity=" + mat.GetFloat("_RimIntensity"));
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            string offFile = Path.Combine(dir, "rim_off.png");
            ScreenCapture.CaptureScreenshot(offFile, 1);
            Debug.Log("[RimVerifyCapture] wrote " + offFile);
            yield return new WaitForEndOfFrame();
            yield return null;

            // Frame B — ON (rim dialed up). SAME mesh + material, ONLY _RimIntensity differs.
            mat.SetFloat("_RimIntensity", demoRimIntensity);
            Debug.Log("[RimVerifyCapture] frame ON: _RimIntensity=" + mat.GetFloat("_RimIntensity"));
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            string onFile = Path.Combine(dir, "rim_on.png");
            ScreenCapture.CaptureScreenshot(onFile, 1);
            Debug.Log("[RimVerifyCapture] wrote " + onFile);
            yield return new WaitForEndOfFrame();
            yield return null;

            yield return new WaitForSeconds(0.5f);
            Debug.Log("[RimVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
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
