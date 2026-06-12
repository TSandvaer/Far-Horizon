using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// PLACEHOLDER inventory readout for the M-U2 thin loop (ticket 86ca8bdaq, U2-2).
    ///
    /// A minimal IMGUI ledger row so the crafted axe (and later wood) is VISIBLE in the shipped exe —
    /// the AC's "inventory state readable" + the success test "sees it in the readout". DELIBERATELY a
    /// placeholder, the sibling of WarmthReadout: U2-5 (Uma + Devon) owns the real diegetic survival HUD
    /// and consumes Inventory's data surface (HasAxe / WoodCount). When U2-5's HUD lands, this is removed.
    ///
    /// Tonally pre-aligned with Uma's U2-5 spec (team/uma-ux/u2-5-survival-hud-spec.md §4): bottom-left,
    /// warm-cream text, absent-when-zero/absent-axe (no "axe x0" / "wood 0" clutter — the quiet ledger).
    /// Text-label fallback (not sprite glyphs) per the spec's Q4 fallback — sprites are later polish.
    ///
    /// Pure IMGUI (no shaders, never strips to magenta) — same build-safe approach as BootHud/WarmthReadout.
    /// Reads only the public surface (HasAxe / WoodCount), never mutates the ledger.
    /// </summary>
    public class InventoryReadout : MonoBehaviour
    {
        [Tooltip("The ledger this readout displays. Wired at bootstrap; falls back to a scene search.")]
        public Inventory inventory;

        private GUIStyle _label;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
        }

        void OnGUI()
        {
            if (inventory == null) return;

            // Absent-when-empty (Uma spec §4 Q4): nothing held -> draw nothing, the quiet case is silence.
            if (!inventory.HasAxe && inventory.WoodCount <= 0) return;

            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                // Warm paver-cream from the world palette (Uma spec §4) — the ledger belongs to the world.
                _label.normal.textColor = new Color(0.92f, 0.85f, 0.72f);
            }

            // Build the one-line ledger in acquired order: axe, then wood. Text-label fallback form.
            string ledger = "";
            if (inventory.HasAxe) ledger += "axe 1";
            if (inventory.WoodCount > 0)
                ledger += (ledger.Length > 0 ? "    " : "") + "wood " + inventory.WoodCount;

            // Bottom-left cluster, just above where U2-5's warmth glow-bar will sit (spec §2 layout).
            const float x = 16f, w = 260f, h = 22f;
            float y = Screen.height - 80f;

            // Same low-alpha dark plate idiom as BootHud/WarmthReadout for legibility over varied ground.
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(x - 6f, y - 3f, w + 12f, h + 6f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, h), ledger, _label);
        }
    }
}
