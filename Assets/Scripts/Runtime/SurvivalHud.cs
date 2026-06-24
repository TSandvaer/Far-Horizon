using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The diegetic-light survival HUD for the M-U2 loop (ticket 86ca8bdge, U2-5; hunger bar #101;
    /// THIRST bar + N-bar generalization 86caamkxv). Implements Uma's three-bar spec
    /// (team/uma-ux/hud-three-bar-spec.md) VERBATIM: warmth (ember-gold) + hunger (fed-green) +
    /// thirst (water-blue) segmented glow-bars + a quiet warm-cream inventory ledger, all left-anchored
    /// in the bottom-left corner — clear of BootHud's top-left title plate and top-right build-stamp plate.
    ///
    /// === The three-bar column (86caamkxv) ===
    /// warmth (row 0, bottom, y=-44) · hunger (row 1, middle, y=-80) · thirst (row 2, top, y=-116);
    /// the inventory ledger moves UP to -152 to clear the new top need row. Warmth + hunger anchors,
    /// palettes, and look are UNCHANGED (AC3 no-regression) — the refactor lifts the two near-identical
    /// DrawWarmthBar/DrawHungerBar copies into ONE <see cref="DrawNeedBar"/> called 3× (AC4: one widget,
    /// a 4th need is one more call + one band function). The three needs share the SAME duck-typed read
    /// surface (Current01 / IsCritical / Changed) but NOT a common base type — WarmthNeed is a standalone
    /// MonoBehaviour (the Sponsor-locked original, predates the SurvivalNeed base) while HungerNeed /
    /// ThirstNeed extend <see cref="SurvivalNeed"/>; so the caller reads each need's state and the ONE
    /// widget renders it — the HUD subscribes/reads, never polls, never writes. References are SERIALIZED editor-time by
    /// BootstrapProject (NOT an Awake FindObjectOfType) per the editor-vs-runtime serialization trap
    /// (unity-conventions.md); the Awake fallback is a build-safety net only.
    ///
    /// Rendering primitive: pure IMGUI flat-rect (GUI.DrawTexture(rect, Texture2D.whiteTexture) with
    /// GUI.color per segment), the same build-safe technique BootHud uses — no shader, no mesh, never
    /// strips to magenta (Uma spec §5). Access = serialized scene reference, NO singleton (Tess PR #11).
    ///
    /// === Pinned wiring resolutions (Tess PR #9 nits + PR #11 vocabulary ruling) ===
    ///   - Vocabulary: binds the SurvivalNeed base surface (Current01 / IsCritical / Changed) for all
    ///     three needs + Inventory.HasAxe / .WoodCount for the ledger — the runtime is source of truth.
    ///   - Segment count: EXACTLY 10 (<see cref="SegmentCount"/>) — pins the spec's "~10 segments".
    ///   - Rounding rule: FLOOR — filledSegments = Floor(Current01 * 10), clamped 0..10. A segment
    ///     lights only when its full 1/10th of the need is earned; deterministic, asserted in the tests.
    ///   - Plate alpha: 0.55 — matches BootHud's build-stamp plate (BootHud uses the 0.5/0.55 family).
    /// </summary>
    public class SurvivalHud : MonoBehaviour
    {
        /// <summary>Exact need-bar segment count (pins Uma spec §1 "exactly 10"). Tests assert against this.</summary>
        public const int SegmentCount = 10;

        /// <summary>Plate alpha — BootHud's build-stamp-plate family (0.55).</summary>
        public const float PlateAlpha = 0.55f;

        [Tooltip("The warmth need this HUD reads. Wired editor-time by BootstrapProject (serialized); " +
                 "the Awake fallback is a build-safety net only.")]
        public WarmthNeed warmth;

        [Tooltip("The HUNGER need this HUD reads (#101 — the missing piece that makes the eat loop verifiable: " +
                 "the player SEES hunger deplete + refill on eating). Same read-only surface as warmth " +
                 "(Current01 / IsCritical / Changed). Wired editor-time by BootstrapProject (serialized); the " +
                 "Awake fallback is a build-safety net only. May be null — the hunger bar is simply not drawn.")]
        public HungerNeed hunger;

        [Tooltip("The THIRST need this HUD reads (86caamkxv — the third need bar that makes the DRINK loop " +
                 "verifiable: the player SEES thirst deplete + refill on drinking at the pond). Same read-only " +
                 "surface as warmth/hunger (Current01 / IsCritical / Changed). Wired editor-time by " +
                 "BootstrapProject (serialized); the Awake fallback is a build-safety net only. May be null — " +
                 "the thirst bar is simply not drawn (mirrors the hunger null-guard).")]
        public ThirstNeed thirst;

        [Tooltip("The inventory ledger this HUD reads. Wired editor-time by BootstrapProject (serialized); " +
                 "the Awake fallback is a build-safety net only.")]
        public Inventory inventory;

        // --- WARMTH palette (Uma spec §1 — all sub-1.0 per channel, HDR-clamp-safe, drawn from the world) ---
        // Filled-segment band colors. Band transition is a color shift of the whole filled run: the bar
        // warms toward gold / cools toward coal-red as the need moves. No flash.
        private static readonly Color WarmGold   = new Color(0.91f, 0.70f, 0.36f); // #E8B25C  warm  (>=60%)
        private static readonly Color DuskOrange = new Color(0.85f, 0.54f, 0.31f); // #D98A4E  cooling(30-60%)
        private static readonly Color CoalRed    = new Color(0.71f, 0.34f, 0.24f); // #B5563C  cold  (<30%) — a dying-ember red, NOT alarm red
        private static readonly Color Charcoal   = new Color(0.18f, 0.165f, 0.17f);// #2E2A2B  emptied segments (shared across all three)
        private static readonly Color Cream      = new Color(0.92f, 0.85f, 0.72f); // #EAD9B8  ledger ink (paver-cream)

        // Band cutoffs (spec §1/§2): warm >=0.60, cooling 0.30..0.60, cold <0.30. Shared by all three bars.
        private const float WarmBand = 0.60f;
        private const float CoolBand = 0.30f;

        // --- HUNGER palette (#101 — food/fruit tones: ripe-leaf green fed -> amber -> hungry berry-red).
        // All sub-1.0 per channel (HDR-clamp-safe). Distinct from warmth's ember so the player tells them apart.
        private static readonly Color FedGreen  = new Color(0.55f, 0.72f, 0.36f); // #8CB85C  fed   (>=60%)
        private static readonly Color RipeAmber = new Color(0.85f, 0.62f, 0.30f); // #D99E4D  peckish(30-60%)
        private static readonly Color BerryRed  = new Color(0.74f, 0.30f, 0.30f); // #BD4D4D  hungry(<30%) — a berry red, NOT alarm red

        // --- THIRST palette (86caamkxv, Uma spec §2.1 — the world's own fresh-water blue, the ONE cool note
        // in the cluster: gold/green/blue sit far apart on the wheel so the three bars never blur at a glance).
        // Drinks blue at the pond, watch the blue bar refill. Drains toward dusty grey-blue when parched,
        // never an alarm hue. All sub-1.0 per channel (HDR-clamp-safe).
        private static readonly Color StreamBlue = new Color(0.24f, 0.56f, 0.77f); // #3E8FC4  slaked  (>=60%) — the world's water color
        private static readonly Color PaleTeal   = new Color(0.37f, 0.66f, 0.69f); // #5FA9B0  dry-ish (30-60%)
        private static readonly Color DryGreyBlue= new Color(0.43f, 0.54f, 0.61f); // #6E8A9C  parched (<30%) — dry-throat ache, NEVER alarm red

        // The shared critical-glyph pulse cadence (Uma spec §4): ~1.0s ease-in-out alpha breathe between
        // ~0.55 and 1.0, on the GLYPH ONLY (not the bar, not the row), one shared phase clock across all
        // three needs so a multi-need-critical corner pulses as one calm body.
        private const float CriticalPulsePeriod = 1.0f;
        private const float CriticalPulseMinAlpha = 0.55f;

        private GUIStyle _flameStyle, _ledgerStyle, _berryStyle, _dropStyle;

        void Awake()
        {
            // Serialized refs are the source of truth (BootstrapProject wires them editor-time).
            // FindObjectOfType is only a net for a hand-placed HUD missing its wiring — never the
            // path the shipped build relies on.
            if (warmth == null) warmth = FindObjectOfType<WarmthNeed>();
            if (hunger == null) hunger = FindObjectOfType<HungerNeed>();
            if (thirst == null) thirst = FindObjectOfType<ThirstNeed>();
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
        }

        void OnGUI()
        {
            EnsureStyles();
            DrawInventoryLedger(); // ledger row sits ABOVE the need column (spec §2.3, moved to -152)

            // The three-bar column (spec §3.1): warmth bottom (-44), hunger middle (-80), thirst top (-116).
            // ONE DrawNeedBar widget, called 3× — differs only by (need state, band function, glyph style,
            // glyph, baseline-y, ember-flicker). A 4th need is one more call (AC4). Warmth keeps its
            // ember-flicker (warmth-ONLY — flicker = fire); hunger + thirst do not flicker (spec §1).
            //
            // ⚠ The three needs do NOT share a common base TYPE — WarmthNeed is a standalone MonoBehaviour
            // (the Sponsor-locked original, predates the SurvivalNeed base); HungerNeed/ThirstNeed extend
            // SurvivalNeed. But all three expose the SAME duck-typed read surface (Current01 / IsCritical).
            // So the caller reads those two values per need and passes them in — the widget is uniform
            // without retrofitting an interface onto the locked WarmthNeed (OOS). Null-guarded per need.
            if (thirst != null) DrawNeedBar(thirst.Current01, thirst.IsCritical, ThirstBandColor, _dropStyle,  "◆", Screen.height - 116f, emberFlicker: false); // ◆ droplet
            if (hunger != null) DrawNeedBar(hunger.Current01, hunger.IsCritical, HungerBandColor, _berryStyle, "●", Screen.height - 80f,  emberFlicker: false); // ● berry
            if (warmth != null) DrawNeedBar(warmth.Current01, warmth.IsCritical, BandColor,       _flameStyle, "▲", Screen.height - 44f,  emberFlicker: true);  // ▲ flame
        }

        private void EnsureStyles()
        {
            if (_flameStyle != null) return;
            _flameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _ledgerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _ledgerStyle.normal.textColor = Cream; // warm-cream ledger ink (spec §2.3)
            // The need glyphs left of each bar — language-free labels distinguishing the three needs at a
            // glance (spec §2.2 / §3.1: ▲ flame / ● berry / ◆ droplet).
            _berryStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _dropStyle  = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        }

        // === The ONE generalized need-bar widget (86caamkxv, AC4 — replaces DrawWarmthBar/DrawHungerBar) ===
        // Draws a segmented glow-bar for ANY of the three needs: a low-alpha dark plate, a band-colored glyph
        // left of the bar (dims at empty + slow-breathes when critical), and 10 segments that empty
        // RIGHT-TO-LEFT as the need decays. The band-color function + glyph + baseline-y are the ONLY per-need
        // differences (one family, three glows — spec §3.2). Takes the need's read state (current01 /
        // isCritical) by VALUE — the three needs share the same duck-typed surface but NOT a common base type
        // (WarmthNeed is a standalone MonoBehaviour; the caller reads the two values and the null-guard lives
        // at the call site). The widget reads, never writes.
        //
        // BYTE-IDENTICAL GUARANTEE (AC4 regression trap): for warmth + hunger the geometry below is the
        // SAME plate rect, glyph rect, segment count/gap/size, and band/charcoal fill as the shipped
        // DrawWarmthBar/DrawHungerBar. The ember-flicker stays warmth-ONLY (emberFlicker arg). The new
        // critical glyph-pulse only modulates GLYPH alpha when isCritical — the non-critical path is
        // identical to the shipped draw (the existing SurvivalHudTests + the anchor/look soak guard this).
        private void DrawNeedBar(float current01, bool isCritical, Func<float, Color> bandColor,
                                 GUIStyle glyphStyle, string glyph, float baselineY, bool emberFlicker)
        {
            int filled = FilledSegments(current01);   // shared pinned FLOOR rule
            Color band = bandColor(current01);

            const float x = 16f, w = 260f, h = 28f;
            float y = baselineY;

            // Single low-alpha dark plate behind the bar (spec §1 plate; alpha 0.55, ~6px padding).
            // Three discrete plates (each bar reads as its own glow), one per DrawNeedBar call.
            DrawPlate(x - 6f, y - 6f, w + 12f, h + 12f);

            // Glyph left of the bar — the only label, language-free (spec §2.2/§3.1). Dims to alpha 0.4 at
            // empty exactly like the shipped flame/berry. When the need is CRITICAL the glyph slow-breathes
            // (spec §4: shared ~1.0s pulse, glyph-only, one phase clock across all three). Non-critical →
            // GlyphPulseAlpha returns 1.0, so the alpha is the shipped (filled>0 ? 1 : 0.4) value unchanged.
            float baseAlpha = filled > 0 ? 1f : 0.4f;
            float pulse = GlyphPulseAlpha(isCritical, Time.unscaledTime);
            glyphStyle.normal.textColor = new Color(band.r, band.g, band.b, baseAlpha * pulse);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 3f, 18f, 22f), glyph, glyphStyle);

            // Segment geometry: the 10 segments run after the glyph, with a small gap between each.
            const float glyphW = 22f, gap = 3f;
            float segArea = w - glyphW;
            float segW = (segArea - gap * (SegmentCount - 1)) / SegmentCount;
            float segY = y + 4f, segH = h - 8f;

            for (int i = 0; i < SegmentCount; i++)
            {
                float segX = x + glyphW + i * (segW + gap);
                bool lit = i < filled;
                Color c = lit ? band : Charcoal;

                // Optional life (spec §1): a subtle ember flicker (±6% alpha breathe, ~1.5s cycle) on the
                // rightmost FILLED segment so the WARMTH bar reads as live fire. Pure alpha modulation on one
                // segment, no per-frame allocation. WARMTH-ONLY — hunger + thirst do NOT flicker (flicker =
                // fire; a flickering food/water bar reads wrong). Static bar is the floor; this is charm.
                if (emberFlicker && lit && i == filled - 1)
                {
                    float breathe = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / 1.5f));
                    c = new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * breathe));
                }

                GUI.color = c;
                GUI.DrawTexture(new Rect(segX, segY, segW, segH), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        // === Inventory ledger (spec §2.3) ========================================================
        // One quiet warm-cream line: "axe 1   wood 3" (text-label fallback — sprites are later polish).
        // Absent-when-zero / absent-when-axe-not-owned (no clutter; the quiet case is silence). Same
        // low-alpha dark plate as the need bars. Moved UP to -152 to clear the new thirst row at -116.
        private void DrawInventoryLedger()
        {
            if (inventory == null) return;

            bool hasAxe = inventory.HasAxe;
            int wood = inventory.WoodCount;

            // Absent-when-empty (spec §2.3): nothing held -> draw nothing (the quiet case is silence).
            if (!hasAxe && wood <= 0) return;

            // Build the one-line ledger in acquired order: axe, then wood.
            string ledger = "";
            if (hasAxe) ledger += "axe 1";
            if (wood > 0) ledger += (ledger.Length > 0 ? "    " : "") + "wood " + wood;

            // Ledger row: above the three need bars. The bottom-left now stacks FOUR rows (warmth -44,
            // hunger -80, thirst -116 added in 86caamkxv), so the ledger moves UP to -152 to sit clear above
            // the thirst row (was -116 when warmth+hunger were the only bars). x = 16, height ~28 (spec §2.3).
            const float x = 16f, w = 260f, h = 28f;
            float y = Screen.height - 152f;

            DrawPlate(x - 6f, y - 3f, w + 12f, h + 6f);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, h), ledger, _ledgerStyle);
        }

        // === Pinned segment-fill math (FLOOR rounding) ===========================================
        /// <summary>
        /// The PINNED 0..1 -> filled-segment-count rule: FLOOR. A segment lights only when its full
        /// 1/<see cref="SegmentCount"/> share of the need is earned. Deterministic and clamped 0..N so
        /// the paired EditMode/PlayMode boundary asserts are exact. (Tess PR #9 nit (a).)
        /// </summary>
        public static int FilledSegments(float current01)
        {
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(current01) * SegmentCount), 0, SegmentCount);
        }

        /// <summary>
        /// The WARMTH filled-run band color (spec §1): warm gold >=0.60, dusk orange 0.30..0.60, coal red
        /// &lt;0.30. The whole filled run shifts color together — no separate alarm element, no flash.
        /// Exposed so the paired tests assert the band mapping.
        /// </summary>
        public static Color BandColor(float current01)
        {
            float c = Mathf.Clamp01(current01);
            if (c >= WarmBand) return WarmGold;
            if (c >= CoolBand) return DuskOrange;
            return CoalRed;
        }

        /// <summary>
        /// The HUNGER filled-run band color (#101): fed green >=0.60, ripe amber 0.30..0.60, hungry
        /// berry-red &lt;0.30 — the food/fruit analogue of <see cref="BandColor"/>, using the SAME band
        /// cutoffs so the bars read consistently (only the palette differs). Exposed so the paired tests
        /// assert the hunger band mapping the same way they assert warmth's.
        /// </summary>
        public static Color HungerBandColor(float current01)
        {
            float c = Mathf.Clamp01(current01);
            if (c >= WarmBand) return FedGreen;
            if (c >= CoolBand) return RipeAmber;
            return BerryRed;
        }

        /// <summary>
        /// The THIRST filled-run band color (86caamkxv, Uma spec §2.1): bright stream-blue >=0.60, pale teal
        /// 0.30..0.60, dry grey-blue &lt;0.30 — the water analogue of <see cref="BandColor"/>, the SAME band
        /// cutoffs (consistency), a new water-blue palette. The ONE cool note in the cluster: blue dominates
        /// (b &gt; r, b &gt; g) at the slaked band so it reads instantly as "the water one" and never blurs
        /// into warmth's gold or hunger's green. Drains toward dusty grey-blue when parched, NEVER an alarm
        /// red. Exposed so the paired tests assert the band mapping + the cool-note invariant.
        /// </summary>
        public static Color ThirstBandColor(float current01)
        {
            float c = Mathf.Clamp01(current01);
            if (c >= WarmBand) return StreamBlue;
            if (c >= CoolBand) return PaleTeal;
            return DryGreyBlue;
        }

        /// <summary>
        /// The shared critical-state glyph pulse (86caamkxv, Uma spec §4) — the consistent IsCritical
        /// treatment across all three needs (AC2). Returns a glyph-alpha MULTIPLIER:
        ///   - NOT critical -> 1.0 (no change; the non-critical glyph alpha is the shipped filled>0?1:0.4).
        ///   - critical     -> a ~1.0s ease-in-out breathe between <see cref="CriticalPulseMinAlpha"/> (~0.55)
        ///                      and 1.0, driven by a SINGLE phase clock (the passed <paramref name="time"/>),
        ///                      so two/three simultaneously-critical glyphs breathe in SYNC (one calm body,
        ///                      not competing blinkers). A slow breathe, NOT a blink/flash (console-game
        ///                      language the tonal gate forbids). GLYPH-ONLY — the bar + row never pulse.
        /// Static + deterministic so the paired EditMode test asserts it (non-critical == 1.0; critical
        /// oscillates in [min, 1.0]; same time -> same alpha for any two critical needs).
        /// </summary>
        public static float GlyphPulseAlpha(bool isCritical, float time)
        {
            if (!isCritical) return 1f;
            // 0..1 ease-in-out via a raised-cosine; phase shared across all needs (the one clock).
            float phase01 = 0.5f * (1f + Mathf.Sin(time * (2f * Mathf.PI / CriticalPulsePeriod)));
            return Mathf.Lerp(CriticalPulseMinAlpha, 1f, phase01);
        }

        // Low-alpha dark plate (spec §1/§2.3 plate idiom; alpha 0.55 == BootHud's stamp-plate family).
        private static void DrawPlate(float x, float y, float w, float h)
        {
            GUI.color = new Color(0f, 0f, 0f, PlateAlpha);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
