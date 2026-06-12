using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A short-lived ground ring shown where the player clicked to move (PoE feel). Grows
    /// slightly then self-destructs. Ported from the engine-eval spike's ClickMarker.
    /// </summary>
    public class ClickMarker : MonoBehaviour
    {
        public float life = 0.6f;
        private float _t;
        private Vector3 _baseScale;

        void Awake() { _baseScale = transform.localScale; }

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / life);
            transform.localScale = _baseScale * (1f + k * 0.6f);
            if (_t >= life) Destroy(gameObject);
        }
    }
}
