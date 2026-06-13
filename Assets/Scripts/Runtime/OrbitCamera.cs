using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Mouse-orbit camera with a top-down-ish default angle — the production camera foundation.
    ///
    /// Deliberate clean re-implementation of the engine-eval spike's OrbitCamera
    /// (EmbergraveUnitySlice, READ-ONLY working spec). RMB-drag orbits (yaw + pitch), scroll
    /// wheel zooms the distance, and the rig follows a target (the player) so the world reads
    /// as "small character in a big alive world".
    ///
    /// Carries the spike's hard-won fixes:
    ///  - Default pitch 55 (the Sponsor's preferred top-down-ish framing) — LOCKED, inside the band.
    ///  - Pitch clamp WIDENED to 8-70 (drew/ocean-camera-fix, 2026-06-13). The spike's 35 floor was
    ///    chosen so near-horizontal pitch couldn't graze a side-by-side biome boundary — but in the
    ///    single production play space the Sponsor explicitly wants to tilt DOWN toward the horizon
    ///    for gameplay (and to SEE the beach ocean, which sits seaward of the spawn). The 35 floor
    ///    literally blocked that: a TRACE (OceanCameraDiag, 2026-06-13) showed that at yaw-180 the
    ///    camera CENTRE only reaches the beach (~Z+4) at pitch 35 — the sea (near-edge Z-10.5) never
    ///    enters frame except as a far fogged sliver top-of-frame (the "grey pond" soak report). At
    ///    pitch ~8-15 the centre reaches the coastline and the open sea fills the upper frame to the
    ///    fogged horizon. So the floor drops to 8 (a hair of downward tilt kept so the camera never
    ///    goes fully horizontal/degenerate). maxPitch 70 + defaultPitch 55 are UNCHANGED.
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 1.0f, 0f);

        [Header("Default framing (top-down-ish)")]
        public float defaultYaw = 0f;
        public float defaultPitch = 55f;   // high pitch = looking down; the Sponsor-preferred angle
        public float distance = 14f;

        [Header("Orbit limits")]
        // Pitch band 8-70 (drew/ocean-camera-fix). Default 55 is inside the band and untouched (LOCKED).
        // Floor dropped 35->8 so the Sponsor can tilt down toward the horizon + see the seaward ocean
        // (the 35 floor framed the sea as a far fogged "grey pond" — OceanCameraDiag trace). 8 (not 0)
        // keeps a hair of downward tilt so the view never goes fully horizontal/grazing-degenerate.
        public float minPitch = 8f;
        public float maxPitch = 70f;
        public float yawSpeed = 200f;
        public float pitchSpeed = 120f;

        [Header("Zoom")]
        public float zoomSpeed = 8f;
        public float minDistance = 6f;
        public float maxDistance = 26f;

        [Header("Smoothing")]
        public float followLerp = 12f;

        private float _yaw;
        private float _pitch;
        private Vector3 _followPos;

        void Start()
        {
            _yaw = defaultYaw;
            _pitch = Mathf.Clamp(defaultPitch, minPitch, maxPitch);
            if (target != null) _followPos = target.position + targetOffset;
            Apply(instant: true);
        }

        void LateUpdate()
        {
            // RMB-drag orbit (legacy input, ported from the spike).
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxisRaw("Mouse X") * yawSpeed * Time.deltaTime;
                _pitch -= Input.GetAxisRaw("Mouse Y") * pitchSpeed * Time.deltaTime;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                distance -= scroll * zoomSpeed;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }

            Apply(instant: false);
        }

        private void Apply(bool instant)
        {
            if (target != null)
            {
                Vector3 desired = target.position + targetOffset;
                _followPos = instant ? desired
                    : Vector3.Lerp(_followPos, desired, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
            }

            // Guard the pitch every Apply so a serialized/probe value can never escape the band.
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pos = _followPos - rot * Vector3.forward * distance;
            transform.SetPositionAndRotation(pos, rot);
        }

        // ---- Test / programmatic hooks ----
        /// <summary>Set yaw and re-apply instantly (orbit-from-code; used by camera tests).</summary>
        public void SetYaw(float yaw) { _yaw = yaw; Apply(true); }

        /// <summary>
        /// Drive a pitch request (e.g. an orbit gesture) and re-apply instantly. The value is
        /// clamped to [minPitch, maxPitch] — the AC's guarantee that the camera obeys the clamp.
        /// </summary>
        public void SetPitch(float pitch) { _pitch = Mathf.Clamp(pitch, minPitch, maxPitch); Apply(true); }

        /// <summary>Set zoom distance (clamped) and re-apply instantly.</summary>
        public void SetDistance(float d) { distance = Mathf.Clamp(d, minDistance, maxDistance); Apply(true); }

        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public float Distance => distance;
    }
}
