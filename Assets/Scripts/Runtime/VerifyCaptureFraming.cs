using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Pure, deterministic framing math shared by the verify-capture paths (AxeVerifyCapture /
    /// CastawayVerifyCapture) — ticket 86ca8fevz (capture-tooling hardening).
    ///
    /// WHY THIS EXISTS (the bug CLASS, not the instance): the verify captures were unreliable evidence —
    /// the exact class that produced the Sponsor's false-green soak rejection (a green verify capture
    /// while the gameplay view failed). Two concrete failures Tess flagged:
    ///   - AxeVerifyCapture clipped the held hatchet at the frame edge with the torso filling ~80% of the
    ///     shot (PR #29) — so it could not substantiate "reads unmistakably as an axe".
    ///   - CastawayVerifyCapture was NON-DETERMINISTIC (PR #31/#36): a `Mathf.Max(0.5f, ...)` floor over
    ///     INVALID skinned-mesh bounds gave height 0.50 vs 0.80 across identical runs (different framing),
    ///     and a "+Z = front" assumption sometimes shot the BACK of the avatar.
    ///
    /// The fix is to make the framing a PURE FUNCTION of explicit inputs (settled bounds + an explicit
    /// front-facing direction + FOV + a fill fraction) so it is REPEATABLE BY CONSTRUCTION and can be
    /// unit-tested without a running player. The callers settle the bounds via BakeMesh / encapsulated
    /// world renderer bounds (never a `Mathf.Max` floor on invalid bounds) and force a KNOWN facing before
    /// calling in — so two identical runs frame identically.
    /// </summary>
    public static class VerifyCaptureFraming
    {
        /// <summary>
        /// The camera pose for a deterministic, fully-framed close-up of a subject.
        /// </summary>
        public struct Frame
        {
            public Vector3 position;   // world camera position
            public Quaternion rotation; // world camera rotation (looks at the subject)
            public Vector3 lookAt;      // the point the camera looks at
            public float distance;      // camera-to-lookAt distance
        }

        /// <summary>
        /// Compute a camera frame that fully contains a subject of size <paramref name="boundsSize"/>
        /// centred on <paramref name="boundsCenter"/>, viewed from <paramref name="viewDir"/> (the world
        /// direction FROM the subject TO the camera — i.e. the subject's front, so the camera sees the
        /// front). The distance is derived so the subject's LARGER on-screen extent fits the frame with
        /// <paramref name="fill"/> headroom (fill 0.7 = the subject spans ~70% of the frame, leaving a
        /// 30% margin so nothing clips the edge).
        ///
        /// DETERMINISTIC: identical inputs always produce an identical Frame. No floors, no fallbacks, no
        /// dependence on lazy renderer state — the caller is responsible for passing SETTLED bounds and an
        /// EXPLICIT facing (never an assumed axis).
        /// </summary>
        /// <param name="boundsCenter">World centre of the (settled) subject bounds.</param>
        /// <param name="boundsSize">World size of the (settled) subject bounds (must be &gt; 0 on its
        /// dominant axes — the caller asserts validity before calling).</param>
        /// <param name="viewDir">World direction from subject to camera (the subject's FRONT). Need not be
        /// normalized; a near-zero vector falls back to world +Z so the result is still defined.</param>
        /// <param name="fieldOfViewDeg">Camera vertical FOV in degrees.</param>
        /// <param name="aspect">Viewport width/height (so a wide subject is framed by the horizontal FOV,
        /// not just the vertical). Pass the capture's aspect; &lt;=0 defaults to 16:9.</param>
        /// <param name="fill">Fraction of the frame the subject's dominant extent should span (0..1).
        /// Lower = more margin. Clamped to [0.30, 0.95].</param>
        public static Frame ComputeFrame(Vector3 boundsCenter, Vector3 boundsSize, Vector3 viewDir,
            float fieldOfViewDeg, float aspect, float fill)
        {
            if (aspect <= 0.0001f) aspect = 16f / 9f;
            fill = Mathf.Clamp(fill, 0.30f, 0.95f);

            Vector3 dir = viewDir;
            dir.Normalize();
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

            // The subject's vertical + horizontal on-screen extents. Use the full bounds height for the
            // vertical fit; for the horizontal fit use the larger of width/depth (a conservative bound on
            // the silhouette width regardless of yaw) so a turned subject still fits.
            float vExtent = Mathf.Max(boundsSize.y, 0.0001f);
            float hExtent = Mathf.Max(Mathf.Max(boundsSize.x, boundsSize.z), 0.0001f);

            float vFovRad = fieldOfViewDeg * Mathf.Deg2Rad;
            // Horizontal FOV from vertical FOV + aspect.
            float hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * aspect);

            // Distance so each extent fits within `fill` of its respective FOV; take the FARTHER of the two
            // so BOTH the height and the width are fully in-frame (whichever is the binding constraint).
            float distV = (vExtent / fill) / (2f * Mathf.Tan(vFovRad * 0.5f));
            float distH = (hExtent / fill) / (2f * Mathf.Tan(hFovRad * 0.5f));
            float distance = Mathf.Max(distV, distH);

            Vector3 position = boundsCenter + dir * distance;
            Quaternion rotation = Quaternion.LookRotation((boundsCenter - position).normalized, Vector3.up);

            return new Frame
            {
                position = position,
                rotation = rotation,
                lookAt = boundsCenter,
                distance = distance
            };
        }
    }
}
