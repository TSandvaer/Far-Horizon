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
    ///  - Pitch clamp 35-70 (FINDINGS iter-3). At near-horizontal pitch the flat ground between
    ///    the player and a biome boundary compresses to ~0 screen height, making the standing
    ///    point misread (the Sponsor's grass->sand illusion). 35-70 keeps a readable top-down-ish
    ///    view and removes the grazing extremes the Sponsor never preferred.
    ///  - Default pitch 55 (the Sponsor's preferred top-down-ish framing) — inside the clamp band.
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
        // FINDINGS iter-3: clamp pitch to a PoE-like band so the camera can't graze the ground.
        // Default 55 is inside the band and untouched.
        public float minPitch = 35f;
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
