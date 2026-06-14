using System.Text;
using UnityEditor;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// DIAGNOSE-VIA-TRACE instrument for the hair helmet/seam class (86ca8ce6y SOAKFIX6). The recurring
    /// false-green was structural: the prior MessyHairCap guards measured per-vertex spikes / crown
    /// flatness, which a SMOOTH DOME passes — and a smooth dome IS the helmet. This trace measures the
    /// actual class the eyes judge: the SILHOUETTE-BREAK from the DEFAULT GAMEPLAY ORBIT CAM.
    ///
    /// What it computes (pure geometry, deterministic — no scene render, so it runs headless + in CI):
    ///   - Loads the shipped hair mesh from Boot.unity (the bytes the exe ships), in WORLD space.
    ///   - Builds the gameplay orbit camera basis (default pitch 55°, the Sponsor's over-shoulder view;
    ///     also samples top-down 70° + a side yaw) and projects every hair vert onto the camera's
    ///     view plane around the hair centroid.
    ///   - Measures the SILHOUETTE radial profile: bin the projected verts by screen-angle and take the
    ///     outer-radius per bin. A SMOOTH DOME -> a near-constant outer radius (low bin-to-bin variance =
    ///     a clean arc = helmet). CLUMPED HAIR -> the outer radius jumps bin-to-bin as tuft lobes poke
    ///     past the dome (high variance + multiple local maxima = distinct clumps = messy hair).
    ///   - Reports: silhouette roughness (coefficient of variation of the outer radius), the count of
    ///     distinct silhouette protrusions (local maxima above the median), and the tuft-protrusion
    ///     fraction (how much of the rim is a clump vs the base arc).
    ///
    /// Run headless:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod FarHorizon.EditorTools.HairSilhouetteTrace.Run
    /// The numbers it prints are the EXACT class the HairReadsClumped guard pins (see CastawayCharacterTests).
    /// </summary>
    public static class HairSilhouetteTrace
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[hair-silhouette] ===== SOAKFIX6 SILHOUETTE TRACE =====");

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            // Find the shipped hair mesh in WORLD space (rides the head bone with the 267× lossy scale).
            Transform hair = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == MovementCameraScene.HairObjectName) { hair = t; break; }
            if (hair == null) { sb.AppendLine("[hair-silhouette] CastawayHair NOT FOUND"); Done(sb); return; }

            var mf = hair.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { sb.AppendLine("[hair-silhouette] no hair mesh"); Done(sb); return; }

            var local = mf.sharedMesh.vertices;
            var world = new Vector3[local.Length];
            for (int i = 0; i < local.Length; i++) world[i] = hair.TransformPoint(local[i]);

            // Hair world centroid + size (sanity).
            Vector3 c = Vector3.zero; foreach (var w in world) c += w; c /= world.Length;
            sb.AppendLine($"[hair-silhouette] hair verts={world.Length} worldCentroid={c} (mesh '{mf.sharedMesh.name}')");

            // Sample the silhouette from the gameplay angles the Sponsor judges from.
            Report(sb, world, c, yawDeg: 25f, pitchDeg: 55f, label: "over-shoulder(55,yaw25)"); // the load-bearing view
            Report(sb, world, c, yawDeg: 25f, pitchDeg: 70f, label: "top-down(70,yaw25)");
            Report(sb, world, c, yawDeg: 90f, pitchDeg: 55f, label: "profile(55,yaw90)");

            sb.AppendLine("[hair-silhouette] ===== END SILHOUETTE TRACE =====");
            Done(sb);
        }

        // Project the hair verts onto the camera view plane for the given orbit (yaw,pitch) and report the
        // silhouette-break metrics. Same metric the HairReadsClumped guard uses (kept in sync via
        // MeasureSilhouetteRoughness so the trace and the guard never drift).
        private static void Report(StringBuilder sb, Vector3[] world, Vector3 center,
            float yawDeg, float pitchDeg, string label)
        {
            var m = MeasureSilhouetteRoughness(world, center, yawDeg, pitchDeg, bins: 36);
            sb.AppendLine($"[hair-silhouette] {label}: roughness(CoV)={m.roughness:F3} protrusions={m.protrusions} " +
                          $"clumpFrac={m.clumpFraction:F3} outerR[min={m.minR:F3} med={m.medianR:F3} max={m.maxR:F3}]");
        }

        public struct SilhouetteMetric
        {
            public float roughness;      // coefficient of variation of the per-bin outer radius (dome=low, clumps=high)
            public int protrusions;      // count of distinct silhouette lobes (local maxima above the median)
            public float clumpFraction;  // fraction of bins whose outer radius is a real clump above the base arc
            public float minR, medianR, maxR;
        }

        /// <summary>
        /// Pure-geometry silhouette-roughness measure (shared by the trace AND the EditMode guard so they
        /// can never drift). Projects `world` verts onto the camera view plane for an orbit at (yaw,pitch)
        /// looking at `center`, bins them by screen-angle, takes the outer radius per bin, and returns the
        /// roughness (CoV of the outer radius), the protrusion count (local maxima above median), and the
        /// clump fraction. A smooth dome -> low roughness + ~0 protrusions; clumped tufts -> high roughness
        /// + several protrusions.
        /// </summary>
        public static SilhouetteMetric MeasureSilhouetteRoughness(Vector3[] world, Vector3 center,
            float yawDeg, float pitchDeg, int bins)
        {
            // Build the orbit camera basis: position the eye at (yaw,pitch) around center, look at center.
            float yaw = yawDeg * Mathf.Deg2Rad, pitch = pitchDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(
                Mathf.Cos(pitch) * Mathf.Sin(yaw),
                Mathf.Sin(pitch),
                Mathf.Cos(pitch) * Mathf.Cos(yaw));
            Vector3 eye = center + dir * 5f;            // distance is irrelevant to the silhouette angle profile
            Vector3 fwd = (center - eye).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right; // degenerate top-down guard
            Vector3 up = Vector3.Cross(fwd, right).normalized;

            // Project each vert onto the (right,up) view plane, in screen-space relative to the centroid.
            var pr = new Vector2[world.Length];
            for (int i = 0; i < world.Length; i++)
            {
                Vector3 rel = world[i] - center;
                pr[i] = new Vector2(Vector3.Dot(rel, right), Vector3.Dot(rel, up));
            }

            // Bin by screen-angle; outer radius per bin = the farthest projected vert in that angular wedge.
            var outer = new float[bins];
            for (int b = 0; b < bins; b++) outer[b] = 0f;
            foreach (var p in pr)
            {
                float ang = Mathf.Atan2(p.y, p.x); if (ang < 0) ang += Mathf.PI * 2f;
                int b = Mathf.Clamp((int)(ang / (Mathf.PI * 2f) * bins), 0, bins - 1);
                float r = p.magnitude;
                if (r > outer[b]) outer[b] = r;
            }

            // Mean + std of the outer radius -> coefficient of variation (roughness).
            float mean = 0f; foreach (var r in outer) mean += r; mean /= bins;
            float var = 0f; foreach (var r in outer) var += (r - mean) * (r - mean); var /= bins;
            float std = Mathf.Sqrt(var);
            float roughness = mean > 1e-6f ? std / mean : 0f;

            // Median + min/max.
            var sorted = (float[])outer.Clone(); System.Array.Sort(sorted);
            float median = sorted[bins / 2];
            float minR = sorted[0], maxR = sorted[bins - 1];

            // Distinct protrusions: local maxima (a bin strictly higher than both neighbours, wrapping) that
            // sit a real margin above the median — each is a tuft clump breaking the silhouette.
            int protrusions = 0; int clumps = 0;
            float clumpThresh = median * 1.10f; // a clump pokes >=10% past the base arc
            for (int b = 0; b < bins; b++)
            {
                float prev = outer[(b - 1 + bins) % bins], cur = outer[b], next = outer[(b + 1) % bins];
                if (cur > clumpThresh) clumps++;
                if (cur > median * 1.06f && cur >= prev && cur > next) protrusions++;
            }

            return new SilhouetteMetric
            {
                roughness = roughness,
                protrusions = protrusions,
                clumpFraction = (float)clumps / bins,
                minR = minR, medianR = median, maxR = maxR
            };
        }

        private static void Done(StringBuilder sb)
        {
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
