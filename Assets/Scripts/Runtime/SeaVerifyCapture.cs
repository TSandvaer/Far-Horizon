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

            // -camDiag (INERT read-only): the pale-frame shoreline diagnosis (86ca8t9pq). At the gameplay
            // pitch (55) the seaward frame came back as flat pale sky-blue (#CBE1EF) where the beach should
            // be — refuting BOTH the ticket's water-Y framing AND the prior camera-framing diagnosis. Dump
            // the ACTUAL camera world transform + where the centre ray hits the ground plane + a per-object
            // raycast so the pale region is diagnosed from ground truth, not re-hypothesized. No mutation.
            if (HasArg("-camDiag"))
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 cp = cam.transform.position, fwd = cam.transform.forward;
                    string hitDesc = "(no ray hit)";
                    if (Mathf.Abs(fwd.y) > 1e-4f)
                    {
                        float t = (-0.20f - cp.y) / fwd.y; // hit the WaterY plane
                        Vector3 h = cp + fwd * t;
                        hitDesc = $"groundPlaneHit(Y=-0.20) @ {h.ToString("F2")} (t={t:F1})";
                    }
                    Debug.Log($"[camDiag] camPos={cp.ToString("F2")} fwd={fwd.ToString("F3")} " +
                              $"fov={cam.fieldOfView} far={cam.farClipPlane} clear={cam.clearFlags} {hitDesc}");
                    // Physics raycast down the centre ray — what does the camera ACTUALLY see at frame centre?
                    if (Physics.Raycast(cp, fwd, out RaycastHit rh, cam.farClipPlane))
                        Debug.Log($"[camDiag] centre PHYSICS ray hit '{rh.collider.name}' @ {rh.point.ToString("F2")} dist={rh.distance:F1}");
                    else
                        Debug.Log("[camDiag] centre PHYSICS ray hit NOTHING (no collider in the centre look direction)");

                    // The terrain renders pale sky-blue (ambient-washed) though the centre ray hits Ground_Play.
                    // Dump the terrain MESH vertex colours + the MATERIAL shader/tint so the wash is pinned to
                    // mesh-colours-lost vs material-fallback vs tint (the SAME shader renders trees/water fine).
                    foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                    {
                        string nm = mf.name ?? "";
                        if (nm.StartsWith("Ground_") || nm.StartsWith("Water_") || nm.Contains("Canopy") || nm.Contains("Trunk"))
                        {
                            var m = mf.sharedMesh;
                            var mr2 = mf.GetComponent<MeshRenderer>();
                            var col = m != null ? m.colors : null;
                            string c0 = (col != null && col.Length > 0) ? col[0].ToString("F2") : "<none>";
                            var sm = mr2 != null ? mr2.sharedMaterial : null;
                            // Dump the mesh's vertex-attribute layout: a missing/wrong-format COLOR stream is the
                            // SRP-batcher trap (IN.color defaults to white -> ambient washes the mesh sky-blue).
                            string attrs = "";
                            if (m != null)
                                foreach (var a in m.GetVertexAttributes())
                                    attrs += a.attribute + ":" + a.format + "x" + a.dimension + " ";
                            Debug.Log($"[camDiag] MESH '{nm}' verts={(m!=null?m.vertexCount:0)} meshColors={(col!=null?col.Length:0)} " +
                                      $"col0={c0} mat='{(sm!=null?sm.name:"null")}' shader='{(sm!=null?sm.shader.name:"null")}' " +
                                      $"attrs=[{attrs.Trim()}]");
                        }
                    }
                }
            }
            // -hideVista (diagnostic): disable every Vista mesh renderer (peaks + landmass bases) so we can
            // tell whether the pale terrain wash is CAUSED by the vista (drawing over the terrain) or is
            // independent terrain shading. If terrain returns to sand with the vista hidden -> vista is the
            // cause; if it stays sky-blue -> the wash is terrain-intrinsic.
            if (HasArg("-hideVista"))
            {
                int hidden = 0;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.name == "Vista" || (t.parent != null && (t.parent.name == "Vista" ||
                        (t.parent.parent != null && t.parent.parent.name == "Vista"))))
                        foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) { r.enabled = false; hidden++; }
                }
                Debug.Log($"[camDiag] -hideVista: disabled {hidden} vista renderers");
                yield return null;
            }
            // -clearMagenta (diagnostic): force the camera to clear to MAGENTA instead of the skybox, so a
            // 'pale void' region resolves unambiguously: magenta == no geometry there (the rays miss all
            // meshes); anything non-magenta == real geometry. Disambiguates skybox-vs-washed-mesh.
            if (HasArg("-clearMagenta"))
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.magenta;
                    Debug.Log("[camDiag] cleared to MAGENTA (geometry shows real colour; void shows magenta)");
                    yield return null;
                }
            }
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
