using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build CLOSE-UP capture of the hero axe (ticket 86ca8ce6y).
    ///
    /// Why this exists (Tess's review NIT on PR #21): the -verifyCraft committed path frames the
    /// axe top-down at gameplay orbit distance, where the signature pale-steel EDGE-BEVEL plane faces
    /// AWAY from the camera — so the committed reproducible path does NOT actually show the bevel, and
    /// the PR-body bevel claim rode an UNCOMMITTED throwaway shot. A future reviewer could not reproduce
    /// the bevel judgment from a committed path. This component IS that committed path: a -verifyAxe flag
    /// that drives a close-up of the cutting edge so the edge-bevel claim rides repeatable committed
    /// shipped-build evidence (sibling of CraftVerifyCapture / MovementVerifyCapture).
    ///
    /// It does NOT touch gameplay: it finds the HeroAxe in the scene, parks a dedicated capture camera
    /// in front of the cutting edge (the -X bit flare, where the bevel plane lives), frames it close, and
    /// captures axe_closeup.png. The orbit gameplay camera is left untouched (we add our own camera so the
    /// shot is deterministic regardless of orbit follow state). Inert unless launched with -verifyAxe.
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyAxe -captureDir &lt;dir&gt;
    /// Captures: axe_closeup.png (the bevel edge, framed close). Quits non-zero if the axe was not found
    /// in the scene (the build-side failure signal — the serialized hero-axe geometry is missing).
    /// </summary>
    public class AxeVerifyCapture : MonoBehaviour
    {
        // Name of the serialized hero-axe GameObject (matches MovementCameraScene.HeroAxeObjectName).
        public const string HeroAxeName = "HeroAxe";
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifyAxe"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Find the serialized hero axe (built editor-time into Boot.unity). Search inactive too so a
            // missing axe is a hard failure, not a silent black frame.
            GameObject axe = FindHeroAxe();
            bool found = axe != null;
            Debug.Log("[AxeVerifyCapture] hero axe found in scene: " + found);
            if (!found)
            {
                Debug.LogError("[AxeVerifyCapture] HeroAxe not in scene — the serialized hero-axe geometry " +
                               "is missing from Boot.unity (the build-side regression signal)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            // Park a dedicated capture camera looking at the HEAD's cutting edge. The axe is authored
            // standing head-up and canted (a pose-dependent transform), with the head high on the haft and
            // the bit/edge + the near-white bevel plane running the head's LOCAL -X edge. We frame from the
            // axe's OWN local space (pose-robust): aim at the head, approach from the cutting-edge side
            // (local -X) + slightly toward the camera-facing cheek (local +Z) + a touch above, so the bevel
            // plane is caught edge-on while the key light rakes it. Local->world via TransformPoint so the
            // shot stays correct regardless of the display rotation/scale.
            Transform axeT = axe.transform;
            // Head sits near the top of the prop; the renderer bounds give the world head region without
            // depending on the mesh's private layout constants. Aim at the upper-head where the edge lives.
            var mr0 = axe.GetComponent<MeshRenderer>();
            Vector3 headWorld = mr0 != null
                ? new Vector3(mr0.bounds.center.x, mr0.bounds.center.y + mr0.bounds.extents.y * 0.45f, mr0.bounds.center.z)
                : axeT.TransformPoint(new Vector3(0f, 1.06f, 0f));
            Vector3 lookAt = headWorld;

            var camGo = new GameObject("AxeCloseupCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.13f, 0.18f); // same deep-dusk neutral as the game cam
            cam.fieldOfView = 38f;                                // frame the whole head so the bevel edge reads
            // Camera offset expressed in the AXE's local axes then taken to world: pull out toward the
            // cutting-edge side (local -X, where the bevel plane faces the viewer) + out the front cheek
            // (local +Z) + a touch above (local +Y), at a distance that frames the WHOLE head so the
            // near-white bevel chamfer runs the cutting edge in-shot (not a single cheek filling the frame).
            Vector3 camLocalDir = (axeT.TransformDirection(new Vector3(-1.0f, 0.30f, 0.65f))).normalized;
            Vector3 camPos = lookAt + camLocalDir * 2.3f;
            camGo.transform.position = camPos;
            camGo.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            Debug.Log("[AxeVerifyCapture] capture cam at " + camPos + " looking at head " + lookAt);

            // Let the frame settle (lighting/post a few frames) before the shot.
            for (int i = 0; i < 8; i++) yield return null;
            yield return new WaitForEndOfFrame();

            string file = Path.Combine(dir, "axe_closeup.png");
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[AxeVerifyCapture] wrote " + file + " (bevel-edge close-up)");
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[AxeVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
        }

        private GameObject FindHeroAxe()
        {
            // Include inactive so a present-but-disabled axe still resolves (and a truly-absent one fails).
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == HeroAxeName) return t.gameObject;
            return null;
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
