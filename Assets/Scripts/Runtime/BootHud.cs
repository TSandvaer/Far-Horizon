using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Minimal IMGUI HUD for the boot scene. Shows the project name + the build stamp
    /// (top-right corner) so every soak / verification screenshot self-identifies its build —
    /// the desktop analogue of the HTML5 BuildInfo-SHA ritual carried from the spike
    /// (FINDINGS.txt iter-3). Build-safe: pure IMGUI, no shaders, never strips to magenta.
    /// </summary>
    public class BootHud : MonoBehaviour
    {
        private GUIStyle _title, _stamp;

        void OnGUI()
        {
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                _title.normal.textColor = Color.white;
                _stamp = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _stamp.normal.textColor = new Color(1f, 0.95f, 0.4f);
            }

            // Title plate, top-left.
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(8, 8, 300, 40), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(18, 14, 290, 30), "Far Horizon", _title);

            // Build stamp plate, top-right (self-identifying for soak screenshots).
            float w = 420f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(Screen.width - w - 8, 8, w, 26), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - w, 11, w - 8, 22), "BUILD " + BuildInfo.Stamp, _stamp);
        }
    }
}
