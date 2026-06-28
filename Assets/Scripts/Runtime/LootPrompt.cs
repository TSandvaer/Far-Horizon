using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The on-screen LOOT PROXIMITY PROMPT (ticket 86cafc6ud AC2/AC3) — a small "Press E to pick up {name}"
    /// tooltip that appears when the player is within loot range of a pickable and HIDES when nothing is in
    /// range. It makes the (now tightened — 86cafc6ud AC1) loot range LEGIBLE: the player sees the prompt
    /// exactly when E would loot, and sees it vanish when they step out of reach.
    ///
    /// === SINGLE SOURCE OF TRUTH (AC3 — the prompt and the actual loot MUST agree) ===
    /// The prompt does NOT run its own proximity scan. Each frame it asks the wired
    /// <see cref="PickableLooter"/> for <see cref="PickableLooter.NearestInRange"/> — the SAME
    /// <c>ResolveNearestPickable(player.position)</c> the E press uses, against the SAME player position. So
    /// the prompt can NEVER name a pickable the looter wouldn't actually loot (or hide while one is reachable):
    /// one resolve, two readers. If the prompt says "Press E to pick up berries", pressing E loots THAT bush.
    ///
    /// === GENERIC item name (AC3 — water/wood slot in with ZERO rework) ===
    /// The name comes from the resolved pickable's own <see cref="IPickable.DisplayName"/> — the prompt knows
    /// NOTHING about berries vs sticks vs stones. When the pond (86cafc6vx) and the tree-chop log-pile
    /// (86caf9u5t) become IPickables, their DisplayName ("water" / "wood") flows through this prompt with no
    /// change here — the load-bearing genericity the ticket requires.
    ///
    /// === Build-safe IMGUI (AC2 — no new shader/mesh) ===
    /// Pure IMGUI flat-rect + label, the SAME technique BootHud / SurvivalHud use (GUI.DrawTexture with
    /// Texture2D.whiteTexture for the plate + GUI.Label for the text). Never strips to magenta in the IL2CPP
    /// release build (no shader/material). Centred low on the screen (clear of the bottom-left need bars + the
    /// top stamp/title plates). The "E" key label is the LITERAL letter (layout-agnostic — a letter is safe on
    /// the Sponsor's Danish keyboard, [[sponsor-danish-keyboard-layout]]).
    ///
    /// === Cheap (unity6-mastery §5/§6 — no per-frame Find) ===
    /// OnGUI can fire multiple times per frame (layout + repaint), so the resolve is done ONCE in Update and
    /// cached; OnGUI only reads the cached label. The resolve itself is the looter's one cached-list pass (no
    /// allocation, no FindObjectsOfType). The per-frame label string is built only when the target CHANGES
    /// (cached), so a steady prompt allocates nothing.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// Authored editor-time onto the player GameObject next to the PickableLooter (MovementCameraScene.
    /// BuildPickableLooter), its looter ref SERIALIZED — NOT an Awake add (the component-in-source-but-not-in-
    /// scene trap). LootPromptSceneTests guards the serialized presence + wiring.
    /// </summary>
    public class LootPrompt : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The looter whose nearest-in-range resolve this prompt reads (the SINGLE source of truth — " +
                 "the prompt and the actual loot agree because both come from this looter). Wired at " +
                 "bootstrap; Awake fallback (same-GameObject GetComponent, then scene-found) is a safety net.")]
        public PickableLooter looter;

        [Tooltip("The loot key the prompt names. Mirrors the looter's lootKey (E). Shown as the literal letter " +
                 "so it is layout-agnostic on the Sponsor's Danish keyboard. Kept in sync from the looter in Awake.")]
        public KeyCode lootKey = KeyCode.E;

        // Plate alpha — the BootHud/SurvivalHud stamp-plate family (0.55).
        private const float PlateAlpha = 0.55f;
        // Warm-cream prompt ink (matches SurvivalHud's ledger Cream so the HUD reads as one family).
        private static readonly Color Cream = new Color(0.92f, 0.85f, 0.72f);

        private GUIStyle _promptStyle;

        // The resolved prompt label this frame ("" = nothing in range -> hidden). Resolved once in Update,
        // read by OnGUI (which can fire several times per frame). Cached target + label so a steady prompt
        // rebuilds the string only when the nearest pickable CHANGES (no per-frame alloc).
        private string _label = "";
        private IPickable _lastTarget;

        void Awake()
        {
            // Serialized ref is the source of truth (wired at bootstrap). Build-safety net only: same-GameObject
            // looter, then a scene search — never a per-frame Find (unity6-mastery §6).
            if (looter == null) looter = GetComponent<PickableLooter>();
            if (looter == null) looter = FindObjectOfType<PickableLooter>();
            if (looter != null) lootKey = looter.lootKey; // name the same key the looter actually loots on
        }

        void Update()
        {
            // ONE resolve per frame against the looter's single source of truth (NearestInRange uses the same
            // ResolveNearestPickable + player position the E press uses). OnGUI then only reads _label.
            IPickable target = looter != null ? looter.NearestInRange() : null;

            if (!ReferenceEquals(target, _lastTarget))
            {
                _lastTarget = target;
                _label = BuildLabel(target, lootKey); // rebuild only when the target changes
            }
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(_label)) return; // nothing in range -> the prompt is HIDDEN (AC2)

            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _promptStyle.normal.textColor = Cream;
            }

            // Centred low (clear of the bottom-left need bars + the top stamp/title plates). A low-alpha dark
            // plate behind the text, same idiom as BootHud's title/stamp plates.
            const float w = 360f, h = 34f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 96f;

            GUI.color = new Color(0f, 0f, 0f, PlateAlpha);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 5f, w, h - 8f), _label, _promptStyle);
        }

        /// <summary>
        /// PURE prompt-text resolution (the unit-testable seam — sibling of <see cref="PickableLooter.ShouldLootOnKey"/>):
        /// given the nearest in-range pickable (or null) and the loot key, return the prompt label, or "" when
        /// there is NOTHING to prompt for (so OnGUI draws nothing — the HIDE case, AC2). Static + dependency-free
        /// so the EditMode test asserts the show/hide + the GENERIC naming with no scene/OnGUI rig:
        ///   • null target            -> "" (prompt hidden — nothing in range);
        ///   • target with no name    -> "" (defensive: never a half-built "Press E to pick up ");
        ///   • target "berries"       -> "Press E to pick up berries" (the default verb flows straight through);
        ///   • the pond ("water")     -> "Press E to collect water" (its GatherVerb override — 86cafc6vx).
        /// The verb comes from the pickable's own <see cref="IPickable.GatherVerb"/> (default "pick up"; the pond
        /// overrides to "collect") so the COPY fits the action with ZERO per-item branch here — the prompt stays
        /// item-agnostic. The key is rendered as the LITERAL letter (E) — layout-agnostic on the Danish keyboard.
        /// </summary>
        public static string BuildLabel(IPickable target, KeyCode lootKey)
        {
            if (target == null) return "";
            string name = target.DisplayName;
            if (string.IsNullOrEmpty(name)) return "";
            string verb = target.GatherVerb;
            if (string.IsNullOrEmpty(verb)) verb = "pick up"; // defensive: a null/blank verb falls back to the default
            return "Press " + lootKey + " to " + verb + " " + name;
        }
    }
}
