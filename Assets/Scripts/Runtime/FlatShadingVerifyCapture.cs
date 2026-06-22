using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build A/B capture for the `_FlatShading` ddx/ddy toggle on
    /// FarHorizon/LowPolyVertexColor (ticket 86caamnjb — Erik R&D §A / Rank 2, Hextant Studios).
    /// Sibling of RockVerifyCapture / SeaVerifyCapture: a -verifyFlatShading flag that renders ONE
    /// demonstrator prop TWICE from an IDENTICAL camera — keyword OFF then ON — so a reviewer
    /// (Devon / Tess / Sponsor) re-runs it and judges the SMOOTH-vs-FACETED difference from a SHIPPED
    /// frame, not the editor (editor-vs-runtime shader-render trap — hero-axe PR #21 proved an editor
    /// RenderTexture mis-renders URP multi-pass materials; AC5 makes the shipped frame the final gate).
    ///
    /// WHY a WELDED SMOOTH SPHERE is the demonstrator: the toggle's whole point is that a WELDED,
    /// smooth-normalled mesh renders the FACETED flat look WITHOUT unwelding verts. A subdivided sphere
    /// with averaged normals is the most unambiguous A/B: OFF = a smooth gradient ball; ON = the same
    /// geometry rendered as hard per-face planes (each triangle one flat value). The mesh + material are
    /// IDENTICAL between the two frames — ONLY the `_FLATSHADING_ON` keyword differs — so the frames
    /// isolate the toggle's effect. The prop rides the GAMEPLAY render path (Zone-D post + scene
    /// lighting) so the look is gameplay-representative (the false-green-capture fix).
    ///
    /// This is VERIFICATION-ONLY runtime geometry (it exists only during the -verifyFlatShading run, then
    /// the exe quits), so building it at runtime is correct — it is NOT shipped scene content that must
    /// serialize (the editor-vs-runtime serialize trap applies to geometry the BUILD must carry; this is
    /// a throwaway capture rig). The COMPONENT itself is serialized into Boot.unity editor-time (added in
    /// BootstrapProject) per the component-in-source-not-serialized trap; FlatShadingVerifyCaptureSceneTests
    /// guards its presence. Inert unless launched with -verifyFlatShading:
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyFlatShading [-captureDir &lt;dir&gt;]
    /// Captures flatshading_off.png then flatshading_on.png, then quits. MUST run WINDOWED (ScreenCapture
    /// needs a real swapchain). Quits non-zero if the FarHorizon/LowPolyVertexColor shader does not resolve
    /// (a build-side regression signal: the shader was stripped or renamed).
    /// </summary>
    public class FlatShadingVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        public float viewPitch = 18f;     // a low-ish orbit so the sphere's top + side facets both read
        public float viewDistance = 3.2f; // close enough that the facets are clearly readable
        public int warmupFrames = 8;
        public int settleFrames = 14;

        private const string ShaderName = "FarHorizon/LowPolyVertexColor";
        private const string Keyword    = "_FLATSHADING_ON";

        void Start()
        {
            if (HasArg("-verifyFlatShading"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var shader = Shader.Find(ShaderName);
            Debug.Log("[FlatShadingVerifyCapture] shader resolved: " + (shader != null));
            if (shader == null)
            {
                Debug.LogError("[FlatShadingVerifyCapture] FarHorizon/LowPolyVertexColor did not resolve — " +
                               "the shader was stripped/renamed (build-side regression signal)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            // Demonstrator prop: a welded smooth-normalled sphere (Unity's built-in sphere is welded with
            // averaged normals — exactly the welded-smooth case the toggle is meant to facet). Place it well
            // clear of the world geometry so the frame is the prop + the gradient sky behind it.
            Vector3 propPos = new Vector3(0f, 200f, 0f);
            var prop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            prop.name = "FlatShadingDemoSphere";
            prop.transform.position = propPos;
            prop.transform.localScale = Vector3.one * 1.4f;
            var col = prop.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // ONE material instance; flip ONLY the keyword between frames so the A/B isolates the toggle.
            var mat = new Material(shader) { name = "FlatShadingDemoMat" };
            if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", new Color(0.78f, 0.74f, 0.66f, 1f)); // warm stone
            prop.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var kw = new LocalKeyword(shader, Keyword);

            // A dedicated post-enabled camera framed on the prop (independent of the gameplay orbit). Renders
            // the Zone-D post Volume + skybox so the capture matches the gameplay look (false-green fix).
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null)
            {
                var ocam = orbit.GetComponent<Camera>();
                if (ocam != null) ocam.enabled = false;
            }
            var camGo = new GameObject("FlatShadingVerifyCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 40f;
            var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            Quaternion rot = Quaternion.Euler(viewPitch, 0f, 0f);
            camGo.transform.position = propPos + rot * new Vector3(0f, 0f, -viewDistance);
            camGo.transform.LookAt(propPos);

            for (int i = 0; i < warmupFrames; i++) yield return null;

            // Frame A — OFF (smooth interpolated vertex normals, the default path).
            mat.SetFloat("_FlatShading", 0f);
            mat.DisableKeyword(kw);
            Debug.Log("[FlatShadingVerifyCapture] frame OFF: keyword enabled=" + mat.IsKeywordEnabled(kw));
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            string offFile = Path.Combine(dir, "flatshading_off.png");
            ScreenCapture.CaptureScreenshot(offFile, 1);
            Debug.Log("[FlatShadingVerifyCapture] wrote " + offFile);
            yield return new WaitForEndOfFrame();
            yield return null;

            // Frame B — ON (per-face ddx/ddy normal, the faceted path). SAME mesh + material, ONLY the keyword.
            mat.SetFloat("_FlatShading", 1f);
            mat.EnableKeyword(kw);
            Debug.Log("[FlatShadingVerifyCapture] frame ON: keyword enabled=" + mat.IsKeywordEnabled(kw));
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            string onFile = Path.Combine(dir, "flatshading_on.png");
            ScreenCapture.CaptureScreenshot(onFile, 1);
            Debug.Log("[FlatShadingVerifyCapture] wrote " + onFile);
            yield return new WaitForEndOfFrame();
            yield return null;

            yield return new WaitForSeconds(0.5f);
            Debug.Log("[FlatShadingVerifyCapture] verification complete -> " + dir);
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
