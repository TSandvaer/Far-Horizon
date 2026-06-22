using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The diegetic-light survival HUD for the M-U2 thin loop (ticket 86ca8bdge, U2-5).
    /// Implements Uma's spec (team/uma-ux/u2-5-survival-hud-spec.md) VERBATIM: a segmented ember
    /// warmth glow-bar + a quiet warm-cream inventory ledger, both left-anchored in the bottom-left
    /// corner — clear of BootHud's top-left title plate and top-right build-stamp plate.
    ///
    /// This SUPERSEDES the U2-1 placeholder WarmthReadout and the U2-2 placeholder InventoryReadout
    /// (both removed in this PR). It reads the same public data surfaces those placeholders read —
    /// WarmthNeed (Current01 / IsCritical / Changed) and Inventory (HasAxe / WoodCount / Changed) —
    /// and never mutates either. Both references are SERIALIZED editor-time by BootstrapProject (NOT
    /// an Awake FindObjectOfType) per the editor-vs-runtime serialization trap (unity-conventions.md);
    /// the Awake fallback is a build-safety net only. CraftSceneTests / WarmthNeedSceneTests guard the
    /// serialized scene presence.
    ///
    /// Rendering primitive: pure IMGUI flat-rect (GUI.DrawTexture(rect, Texture2D.whiteTexture) with
    /// GUI.color per segment), the same build-safe technique BootHud uses — no shader, no mesh, never
    /// strips to magenta (Uma spec §6). Access = serialized scene reference, NO singleton
    /// (Tess PR #11 ruling).
    ///
    /// === Pinned wiring resolutions (Tess PR #9 nits + PR #11 vocabulary ruling) ===
    ///   - Vocabulary: binds to WarmthNeed.Current01 / .IsCritical / .Changed and
    ///     Inventory.HasAxe / .WoodCount / .Changed — the runtime is source of truth
    ///     (spec §7's WarmthNormalized01 was amended to Current01 in this PR).
    ///   - Segment count: EXACTLY 10 (<see cref="SegmentCount"/>) — pins the spec's "~10 segments".
    ///   - Rounding rule: FLOOR — filledSegments = Floor(Current01 * 10), clamped 0..10. A segment
    ///     lights only when its full 1/10th of warmth is earned; deterministic and asserted at
    ///     non-boundary points in the paired tests. (Pins Tess PR #9 nit (a).)
    ///   - Plate alpha: 0.55 — matches BootHud's build-stamp plate (amends the spec's 0.45 per
    ///     Tess PR #9 nit (b); BootHud uses the 0.5/0.55 family).
    /// </summary>
    public class SurvivalHud : MonoBehaviour
    {
        /// <summary>Exact warmth-bar segment count (pins Uma spec §3 "~10"). Tests assert against this.</summary>
        public const int SegmentCount = 10;

        /// <summary>Plate alpha — BootHud's build-stamp-plate family (0.55), amends spec's 0.45.</summary>
        public const float PlateAlpha = 0.55f;

        [Tooltip("The warmth need this HUD reads. Wired editor-time by BootstrapProject (serialized); " +
                 "the Awake fallback is a build-safety net only.")]
        public WarmthNeed warmth;

        [Tooltip("The HUNGER need this HUD reads (#101 — the missing piece that makes the eat loop verifiable: " +
                 "the player SEES hunger deplete + refill on eating). Same read-only surface as warmth " +
                 "(Current01 / IsCritical / Changed). Wired editor-time by BootstrapProject (serialized); the " +
                 "Awake fallback is a build-safety net only. May be null — the hunger bar is simply not drawn.")]
        public HungerNeed hunger;

        [Tooltip("The inventory ledger this HUD reads. Wired editor-time by BootstrapProject (serialized); " +
                 "the Awake fallback is a build-safety net only.")]
        public Inventory inventory;

        // --- Palette (Uma spec §3/§4 — all sub-1.0 per channel, HDR-clamp-safe, drawn from the world) ---
        // Filled-segment band colors. Band transition is a color shift of the whole filled run
        // (spec §3): the bar warms toward gold / cools toward coal-red as the need moves. No flash.
        private static readonly Color WarmGold   = new Color(0.91f, 0.70f, 0.36f); // #E8B25C  warm  (>=60%)
        private static readonly Color DuskOrange = new Color(0.85f, 0.54f, 0.31f); // #D98A4E  cooling(30-60%)
        private static readonly Color CoalRed    = new Color(0.71f, 0.34f, 0.24f); // #B5563C  cold  (<30%) — a dying-ember red, NOT alarm red
        private static readonly Color Charcoal   = new Color(0.18f, 0.165f, 0.17f);// #2E2A2B  emptied segments (the cold)
        private static readonly Color Cream      = new Color(0.92f, 0.85f, 0.72f); // #EAD9B8  ledger ink (paver-cream)

        // Band cutoffs (spec §3): warm >=0.60, cooling 0.30..0.60, cold <0.30.
        private const float WarmBand = 0.60f;
        private const float CoolBand = 0.30f;

        // --- HUNGER bar palette (#101 — a SECOND need bar above warmth; distinct from warmth's ember so the
        // player tells them apart at a glance). Food/fruit tones: a ripe berry-leaf green when fed, warming
        // through amber to a hungry berry-red as it empties — material-honest "food" colour, never alarm red.
        // All sub-1.0 per channel (HDR-clamp-safe, the same discipline as the warmth band). The hunger bar
        // empties RIGHT-TO-LEFT like warmth, and shares the FilledSegments FLOOR rule + segment geometry.
        private static readonly Color FedGreen  = new Color(0.55f, 0.72f, 0.36f); // #8CB85C  fed   (>=60%)
        private static readonly Color RipeAmber = new Color(0.85f, 0.62f, 0.30f); // #D99E4D  peckish(30-60%)
        private static readonly Color BerryRed  = new Color(0.74f, 0.30f, 0.30f); // #BD4D4D  hungry(<30%) — a berry red, NOT alarm red

        private GUIStyle _flameStyle, _ledgerStyle, _berryStyle;

        void Awake()
        {
            // Serialized refs are the source of truth (BootstrapProject wires them editor-time).
            // FindObjectOfType is only a net for a hand-placed HUD missing its wiring — never the
            // path the shipped build relies on (the serialized ref already points at the scene's
            // WarmthNeed/Inventory).
            if (warmth == null) warmth = FindObjectOfType<WarmthNeed>();
            if (hunger == null) hunger = FindObjectOfType<HungerNeed>();
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
        }

        void OnGUI()
        {
            EnsureStyles();
            DrawInventoryLedger(); // ledger row sits ABOVE the need bars (spec §2 layout)
            DrawHungerBar();       // hunger bar sits ABOVE the warmth bar (#101 — the loop-verify piece)
            DrawWarmthBar();
        }

        private void EnsureStyles()
        {
            if (_flameStyle != null) return;
            _flameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _ledgerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _ledgerStyle.normal.textColor = Cream; // warm-cream ledger ink (spec §4)
            // The hunger glyph (a berry) left of the hunger bar — the language-free label distinguishing it
            // from the warmth flame glyph (#101 AC2 "tell warmth vs hunger apart at a glance").
            _berryStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        }

        // === Hunger bar (#101 — the second need bar that makes the eat loop VERIFIABLE) ==============
        // The SAME segmented geometry + FLOOR fill rule + right-to-left empty as the warmth bar (one bar
        // idiom), but a DISTINCT food palette (fed-green -> ripe-amber -> hungry berry-red) + a berry glyph
        // so the player tells it apart from warmth. Sits one row ABOVE the warmth bar. Bound to HungerNeed's
        // read-only surface (Current01) — the HUD subscribes/reads, never writes (eating goes through
        // HungerNeed.AddFood elsewhere). Skipped entirely if no hunger need is wired.
        private void DrawHungerBar()
        {
            if (hunger == null) return;

            float current01 = hunger.Current01;
            int filled = FilledSegments(current01);      // shares warmth's pinned FLOOR rule
            Color bandColor = HungerBandColor(current01);

            // One row above the warmth bar (warmth baseline y = Screen.height - 44; hunger at -80, the same
            // slot the inventory ledger used — the ledger moves up to -116 below so all three rows stack).
            const float x = 16f, w = 260f, h = 28f;
            float y = Screen.height - 80f;

            DrawPlate(x - 6f, y - 6f, w + 12f, h + 12f);

            // Berry glyph left of the bar — language-free, dims at empty (mirrors the warmth flame glyph).
            float glyphAlpha = filled > 0 ? 1f : 0.4f;
            _berryStyle.normal.textColor = new Color(bandColor.r, bandColor.g, bandColor.b, glyphAlpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 3f, 18f, 22f), "●", _berryStyle); // ● berry glyph (vs ▲ warmth flame)

            const float glyphW = 22f, gap = 3f;
            float segArea = w - glyphW;
            float segW = (segArea - gap * (SegmentCount - 1)) / SegmentCount;
            float segY = y + 4f, segH = h - 8f;

            for (int i = 0; i < SegmentCount; i++)
            {
                float segX = x + glyphW + i * (segW + gap);
                bool lit = i < filled;
                GUI.color = lit ? bandColor : Charcoal;
                GUI.DrawTexture(new Rect(segX, segY, segW, segH), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        // === Warmth glow-bar (spec §3) ===========================================================
        // Segmented ember bar, bottom-left. Filled segments = banked warmth (band color); emptied
        // segments = cold charcoal. Empties RIGHT-TO-LEFT as warmth decays (fire burning down).
        private void DrawWarmthBar()
        {
            if (warmth == null) return;

            float current01 = warmth.Current01;
            int filled = FilledSegments(current01); // FLOOR rounding (pinned) — testable, deterministic
            Color bandColor = BandColor(current01);

            // Anchor math (spec §2): warmth baseline y = Screen.height - 44, x = 16, box ~260x28.
            const float x = 16f, w = 260f, h = 28f;
            float y = Screen.height - 44f;

            // Single low-alpha dark plate behind the bar (spec §3 plate; alpha 0.55, ~6px padding).
            DrawPlate(x - 6f, y - 6f, w + 12f, h + 12f);

            // Flame glyph left of the bar — the only label, language-free (spec §3). Dims at empty.
            float glyphAlpha = filled > 0 ? 1f : 0.4f;
            _flameStyle.normal.textColor = new Color(bandColor.r, bandColor.g, bandColor.b, glyphAlpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 3f, 18f, 22f), "▲", _flameStyle); // ▲ flame glyph

            // Segment geometry: the 10 segments run after the glyph, with a small gap between each.
            const float glyphW = 22f, gap = 3f;
            float segArea = w - glyphW;
            float segW = (segArea - gap * (SegmentCount - 1)) / SegmentCount;
            float segY = y + 4f, segH = h - 8f;

            for (int i = 0; i < SegmentCount; i++)
            {
                float segX = x + glyphW + i * (segW + gap);
                bool lit = i < filled;
                Color c = lit ? bandColor : Charcoal;

                // Optional life (spec §3): a subtle ember flicker (±6% alpha breathe, ~1.5s cycle)
                // on the rightmost FILLED segment so the bar reads as live fire. Pure alpha modulation
                // on one segment, no per-frame allocation. Static bar is the floor; this is charm.
                if (lit && i == filled - 1)
                {
                    float breathe = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / 1.5f));
                    c = new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * breathe));
                }

                GUI.color = c;
                GUI.DrawTexture(new Rect(segX, segY, segW, segH), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        // === Inventory ledger (spec §4) ==========================================================
        // One quiet warm-cream line: "axe 1   wood 3" (text-label fallback — sprites are later polish,
        // spec §4 Q4). Absent-when-zero / absent-when-axe-not-owned (no clutter; the quiet case is
        // silence). Same low-alpha dark plate as the warmth bar.
        private void DrawInventoryLedger()
        {
            if (inventory == null) return;

            bool hasAxe = inventory.HasAxe;
            int wood = inventory.WoodCount;

            // Absent-when-empty (spec §4): nothing held -> draw nothing (the quiet case is silence).
            if (!hasAxe && wood <= 0) return;

            // Build the one-line ledger in acquired order: axe, then wood (spec §4).
            string ledger = "";
            if (hasAxe) ledger += "axe 1";
            if (wood > 0) ledger += (ledger.Length > 0 ? "    " : "") + "wood " + wood;

            // Inventory ledger row: above the need bars. The bottom-left now stacks three rows (warmth at
            // -44, hunger at -80 added in #101), so the ledger moves UP to -116 to sit clear above hunger
            // (was -80 when warmth was the only bar). x = 16, height ~28 (spec §2).
            const float x = 16f, w = 260f, h = 28f;
            float y = Screen.height - 116f;

            DrawPlate(x - 6f, y - 3f, w + 12f, h + 6f);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, h), ledger, _ledgerStyle);
        }

        // === Pinned segment-fill math (FLOOR rounding) ===========================================
        /// <summary>
        /// The PINNED 0..1 -> filled-segment-count rule: FLOOR. A segment lights only when its full
        /// 1/<see cref="SegmentCount"/> share of warmth is earned. Deterministic and clamped 0..N so
        /// the paired EditMode/PlayMode boundary asserts are exact. (Tess PR #9 nit (a).)
        /// </summary>
        public static int FilledSegments(float current01)
        {
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(current01) * SegmentCount), 0, SegmentCount);
        }

        /// <summary>
        /// The filled-run band color for a given normalized warmth (spec §3): warm gold >=0.60,
        /// dusk orange 0.30..0.60, coal red &lt;0.30. The whole filled run shifts color together —
        /// no separate alarm element, no flash. Exposed so the paired tests assert the band mapping.
        /// </summary>
        public static Color BandColor(float current01)
        {
            float c = Mathf.Clamp01(current01);
            if (c >= WarmBand) return WarmGold;
            if (c >= CoolBand) return DuskOrange;
            return CoalRed;
        }

        /// <summary>
        /// The HUNGER bar's filled-run band color (#101): fed green >=0.60, ripe amber 0.30..0.60, hungry
        /// berry-red &lt;0.30 — the food/fruit analogue of <see cref="BandColor"/>, using the SAME band
        /// cutoffs so the two bars read consistently (only the palette differs). Exposed so the paired
        /// EditMode test asserts the hunger band mapping the same way it asserts warmth's.
        /// </summary>
        public static Color HungerBandColor(float current01)
        {
            float c = Mathf.Clamp01(current01);
            if (c >= WarmBand) return FedGreen;
            if (c >= CoolBand) return RipeAmber;
            return BerryRed;
        }

        // Low-alpha dark plate (spec §3/§4 plate idiom; alpha 0.55 == BootHud's stamp-plate family).
        private static void DrawPlate(float x, float y, float w, float h)
        {
            GUI.color = new Color(0f, 0f, 0f, PlateAlpha);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
