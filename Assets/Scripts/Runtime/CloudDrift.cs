using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Slow lateral CLOUD DRIFT (world-look polish, ticket 86ca8t9pq — Uma world-look brief §1).
    ///
    /// Drifts a faceted cloud blob laterally along a single shared wind direction at a slow per-cloud
    /// speed, wrapping back to the upwind edge of the sky band when it passes the downwind edge — so the
    /// sky reads alive without ever pulling focus (Uma §1: "very slow lateral drift, ~0.2-0.5 u/s, single
    /// shared wind direction, slight per-cloud speed variance; looping/wrapping at the dome edge"). No
    /// rotation — clouds translate, they don't tumble (Uma: "a tumbling cloud reads as debris").
    ///
    /// These values are SERIALIZED into the scene editor-time (WorldBootstrap sets them, NOT Awake) per
    /// the editor-vs-runtime serialization trap (unity-conventions.md) — the component ships configured;
    /// only the per-frame translate runs at runtime. Drift is the ONLY animated part (like the water swell).
    ///
    /// DIAGNOSTIC TRACE (the no-new-class-without-trace discipline): on the FIRST drift frame this logs a
    /// one-shot [world-trace] CloudDrift line (name + wind + speed + wrap span + start pos) so a future
    /// "clouds aren't moving / wrapped wrong / drifting off-dome" report is diagnosed from the trace, not
    /// re-hypothesized. Inert (no per-frame spam) after the first frame.
    /// </summary>
    public class CloudDrift : MonoBehaviour
    {
        [Tooltip("Shared wind direction (unit, world XZ). Normalised on Start.")]
        public Vector3 windDir = new Vector3(1f, 0f, 0.15f);

        [Tooltip("Drift speed in world-units/sec (Uma §1: 0.2-0.5 u/s, slight per-cloud variance).")]
        public float speed = 0.3f;

        [Tooltip("Half-span of the drift band along the wind axis: the cloud wraps when its signed " +
                 "distance from the band centre exceeds this (looping at the dome edge).")]
        public float wrapHalfSpan = 70f;

        [Tooltip("Centre of the drift band along the wind axis (world). The cloud wraps relative to this.")]
        public Vector3 bandCentre = Vector3.zero;

        private Vector3 _wind;
        private bool _traced;

        void Start()
        {
            _wind = windDir.sqrMagnitude > 1e-6f ? windDir.normalized : Vector3.right;
        }

        void Update()
        {
            transform.position += _wind * speed * Time.deltaTime;

            // Wrap: project the offset from the band centre onto the wind axis; when it passes the
            // downwind edge, teleport back a full span to the upwind edge (a seamless loop — the cloud
            // re-enters from the other side). Only the along-wind component is wrapped; the cloud keeps
            // its cross-wind position + altitude.
            Vector3 off = transform.position - bandCentre;
            float along = Vector3.Dot(off, _wind);
            if (along > wrapHalfSpan)
                transform.position -= _wind * (wrapHalfSpan * 2f);

            if (!_traced)
            {
                _traced = true;
                Debug.Log($"[world-trace] CloudDrift {name}.pos={transform.position} wind={_wind} " +
                          $"speed={speed:F2} wrapHalfSpan={wrapHalfSpan:F1} bandCentre={bandCentre}");
            }
        }
    }
}
