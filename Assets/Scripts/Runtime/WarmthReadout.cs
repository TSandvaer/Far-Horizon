using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// PLACEHOLDER warmth readout for the M-U2 thin loop (ticket 86ca8bd9m).
    ///
    /// A minimal IMGUI bar (top-left, under the title plate) so the warmth decay is VISIBLE in the
    /// shipped exe — the AC's "decay visible to the player". This is DELIBERATELY a placeholder:
    /// U2-5 (Uma + Devon) owns the real diegetic survival HUD and consumes WarmthNeed's data surface
    /// (subscribe to Changed, read Current01). When U2-5's HUD lands, this readout is removed/replaced.
    ///
    /// Pure IMGUI (no shaders, never strips to magenta) — same build-safe approach as BootHud. Reads
    /// only the public surface (Current01 / IsCritical), never mutates the need.
    /// </summary>
    public class WarmthReadout : MonoBehaviour
    {
        [Tooltip("The need this readout displays. Wired at bootstrap; falls back to a scene search.")]
        public WarmthNeed need;

        private GUIStyle _label;

        void Awake()
        {
            if (need == null) need = FindObjectOfType<WarmthNeed>();
        }

        void OnGUI()
        {
            if (need == null) return;

            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _label.normal.textColor = Color.white;
            }

            float fill = need.Current01;

            // Bar geometry: under BootHud's top-left title plate (which occupies y 8..48).
            const float x = 18f, y = 56f, w = 220f, h = 18f;

            // Track (dark backing).
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 4f, y - 2f, w + 8f, h + 20f), Texture2D.whiteTexture);

            // Fill — warm amber when comfortable, shifting to cold blue as it drops to critical.
            Color warm = new Color(0.95f, 0.62f, 0.25f); // ember
            Color cold = new Color(0.45f, 0.62f, 0.95f); // chilled
            GUI.color = need.IsCritical ? cold : Color.Lerp(cold, warm, fill);
            GUI.DrawTexture(new Rect(x, y, w * fill, h), Texture2D.whiteTexture);

            // Empty remainder (subtle).
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(x + w * fill, y, w * (1f - fill), h), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + h, w, 18f),
                $"WARMTH {Mathf.RoundToInt(fill * 100f)}%" + (need.IsCritical ? "  (cold)" : ""), _label);
        }
    }
}
