using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The single on-screen INTERACTION PROMPT — a small dark pill anchored ABOVE THE CHARACTER'S HEAD that names
    /// the one thing the player can do RIGHT NOW (ticket 86cafc6ud loot prompt, EXPANDED for 86caffwv5 round-7,
    /// TASK 3). Originally a bottom-center "Press E to pick up {name}" loot tooltip; the Sponsor found the
    /// bottom-center location hard to see (it sat on top of the belt strip), so ALL contextual prompts now render
    /// at ONE predictable world-anchored location above the head — no belt overlap, one place to look.
    ///
    /// === What it shows (priority order — the pure static <see cref="ResolveInteractionPrompt"/>) ===
    ///   1. "Mine stone"          — within mine range of a boulder with ANY pickaxe belt-selected;
    ///   2. "Mine iron"           — within range of iron ore with a STONE or IRON pickaxe selected;
    ///   3. "Needs stone pickaxe" — within range of iron ore with a WOOD pickaxe selected (the refusal cue the
    ///                              Sponsor approved — a wood pickaxe mines boulders→stone but NOT ore, spec §5);
    ///   4. "Chop"                — within chop range of a tree with any axe selected (falls out of the same seam);
    ///   5. "Press E to pick up {name}" — the existing loot prompt (nearest in-range pickable), UNCHANGED copy.
    /// The verb prompts (1-4) are LEFT-CLICK actions the player is tool-ready for; the loot prompt (5) is the E
    /// action. When more than one applies, the tool verb the player is holding a tool for wins over loot.
    ///
    /// === SINGLE SOURCE OF TRUTH (the prompt and the action MUST agree) ===
    /// Every sub-prompt reads the SAME ground truth the action itself uses: the loot from
    /// <see cref="PickableLooter.NearestInRange"/> (the SAME resolve the E press uses), and mine/chop from each
    /// verb's own <see cref="MineBoulder.ClickGateDiag"/> / <see cref="MineOre.ClickGateDiag"/> /
    /// <see cref="ChopTree.ClickGateDiag"/> (each verb's OWN resolver — no duplicated distance math), so the prompt
    /// can never name an action the verb wouldn't actually take. The ore tier split reuses the verb's own
    /// tool-selected gate (stone/iron) + <see cref="Inventory.IsPickaxeWoodSelectedInBelt"/> for the wood-refusal.
    ///
    /// === Above-head world anchor, screen-CLAMPED (Sponsor-decided placement) ===
    /// The pill is drawn at <see cref="Camera"/>.WorldToScreenPoint of the player's head (root + a head-height
    /// offset), then CLAMPED to the screen with a margin so it never slides off-frame at close zoom (the Sponsor's
    /// requirement). Hidden while the head projects behind the camera. Pure IMGUI dark plate + label (the same
    /// BootHud/SurvivalHud idiom — never strips to magenta in the IL2CPP release, no shader/material).
    ///
    /// === Cheap (unity6-mastery §5/§6 — no per-frame Find) ===
    /// The sources are resolved ONCE in Awake (serialized looter ref + one-shot scene finds for the verbs/camera —
    /// NOT a per-frame Find). Update does one resolve per source per frame (the verbs' cold-path distance scan +
    /// the looter's cached-list pass) and caches the label; OnGUI only reads the cached label + re-projects the head.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// Authored editor-time onto the player GameObject next to the PickableLooter (MovementCameraScene.
    /// BuildPickableLooter), its looter ref SERIALIZED. The verb/camera refs are resolved by a one-shot Awake
    /// scene-find (scene singletons; NOT new serialized wiring, so the committed Boot.unity needs NO regen —
    /// [[unity-procedural-committed-assets-go-stale]]). LootPromptSceneTests guards the serialized presence + wiring.
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

        [Header("Above-head anchor (86caffwv5 round-7, TASK 3)")]
        [Tooltip("Metres above the player ROOT to anchor the prompt (≈ above the ~1.8m castaway's head). The pill " +
                 "is drawn at the camera projection of (player root + this up-offset), then screen-clamped.")]
        public float headAnchorHeight = 2.2f;

        // Plate alpha — the BootHud/SurvivalHud stamp-plate family (0.55).
        private const float PlateAlpha = 0.55f;
        // Warm-cream prompt ink (matches SurvivalHud's ledger Cream so the HUD reads as one family).
        private static readonly Color Cream = new Color(0.92f, 0.85f, 0.72f);
        // Screen-clamp margin (px) + gap above the head projection + horizontal text padding.
        private const float ScreenMargin = 8f;
        private const float HeadGapPx = 6f;
        private const float PadX = 14f;
        private const float PillH = 30f;

        private GUIStyle _promptStyle;

        // The resolved prompt label this frame ("" = nothing actionable -> hidden). Resolved once in Update,
        // read by OnGUI (which can fire several times per frame). Cached so a steady prompt rebuilds nothing.
        private string _label = "";
        private IPickable _lastLootTarget;
        private string _lootLabel = "";

        // Sources resolved ONCE in Awake (scene singletons; not per-frame Find).
        private Transform _playerT;
        private Camera _cam;
        private MineBoulder _mineBoulder;
        private MineOre _mineOre;
        private ChopTree _chopTree;
        private Inventory _inventory;

        void Awake()
        {
            // No GUILayout.* in this OnGUI (explicit Rects only) — skip IMGUI's Layout event pass (86cahhfp4 C2a).
            useGUILayout = false;

            // Serialized ref is the source of truth (wired at bootstrap). Build-safety net only: same-GameObject
            // looter, then a scene search — never a per-frame Find (unity6-mastery §6).
            if (looter == null) looter = GetComponent<PickableLooter>();
            if (looter == null) looter = FindObjectOfType<PickableLooter>();
            if (looter != null) lootKey = looter.lootKey; // name the same key the looter actually loots on

            // The player transform to anchor above (the wired looter's player, else this GameObject — LootPrompt is
            // authored ON the player). One-shot resolve; cached.
            _playerT = looter != null && looter.player != null ? looter.player : transform;

            // Verb sources for the mine/chop prompts + inventory for the ore tier split. One-shot scene finds
            // (scene singletons; NOT new serialized wiring → the committed Boot.unity needs no regen). Any may be
            // null on a bare rig — the prompt logic treats a null source as "no such prompt".
            _mineBoulder = FindObjectOfType<MineBoulder>();
            _mineOre = FindObjectOfType<MineOre>();
            _chopTree = FindObjectOfType<ChopTree>();
            _inventory = FindObjectOfType<Inventory>();
            _cam = Camera.main;
        }

        void Update()
        {
            // Rebuild the LOOT label only when the nearest pickable CHANGES (no per-frame alloc for a steady prompt).
            IPickable lootTarget = looter != null ? looter.NearestInRange() : null;
            if (!ReferenceEquals(lootTarget, _lastLootTarget))
            {
                _lastLootTarget = lootTarget;
                _lootLabel = BuildLabel(lootTarget, lootKey);
            }

            // Resolve the mine/chop readiness from each verb's OWN ground truth (its ClickGateDiag), + the ore tier
            // split (stone/iron mine vs wood refusal) from the ore verb's tool gate + the wood-pickaxe selection.
            bool boulderMineReady = _mineBoulder != null && _mineBoulder.ClickGateDiag().WouldClaim;

            bool oreMineReady = false, oreNeedsBetterPick = false;
            if (_mineOre != null)
            {
                var oreDiag = _mineOre.ClickGateDiag();       // ToolSelected = STONE/IRON pickaxe only
                if (oreDiag.TargetInRange)
                {
                    if (oreDiag.ToolSelected) oreMineReady = true;                       // stone/iron → "Mine iron"
                    else if (_inventory != null && _inventory.IsPickaxeWoodSelectedInBelt)
                        oreNeedsBetterPick = true;                                       // wood → refusal cue
                }
            }

            bool chopReady = _chopTree != null && _chopTree.ClickGateDiag().WouldClaim;

            _label = ResolveInteractionPrompt(boulderMineReady, oreMineReady, oreNeedsBetterPick, chopReady, _lootLabel);
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(_label)) return;      // nothing actionable -> the prompt is HIDDEN
            if (_playerT == null) return;
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

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

            // Project the player's HEAD to screen space. WorldToScreenPoint: origin BOTTOM-left, z = distance in
            // front of the camera. Hide when the head is behind the camera (z <= 0).
            Vector3 headWorld = _playerT.position + Vector3.up * headAnchorHeight;
            Vector3 sp = _cam.WorldToScreenPoint(headWorld);
            if (sp.z <= 0f) return;

            // Snug pill sized to the text; dark plate (match the existing HUD panel style) + cream label.
            float pillW = _promptStyle.CalcSize(new GUIContent(_label)).x + PadX * 2f;
            float bottomGui = (Screen.height - sp.y) - HeadGapPx;   // just above the head projection (GUI y is top-down)
            float topGui = bottomGui - PillH;

            // Screen-CLAMP so the pill never slides off-frame at close zoom (the Sponsor's requirement).
            float x = Mathf.Clamp(sp.x - pillW * 0.5f, ScreenMargin, Screen.width - ScreenMargin - pillW);
            float y = Mathf.Clamp(topGui, ScreenMargin, Screen.height - ScreenMargin - PillH);

            GUI.color = new Color(0f, 0f, 0f, PlateAlpha);
            GUI.DrawTexture(new Rect(x, y, pillW, PillH), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 5f, pillW, PillH - 8f), _label, _promptStyle);
        }

        /// <summary>
        /// PURE interaction-prompt priority (the unit-testable seam — sibling of <see cref="BuildLabel"/>). Given
        /// the readiness flags for each verb (already resolved against each verb's OWN ground truth) + the loot
        /// label, return the ONE label to show above the head (or "" to hide). Priority (a tool verb the player is
        /// tool-ready for wins over loot):
        ///   • <paramref name="boulderMineReady"/> → "Mine stone";
        ///   • <paramref name="oreMineReady"/>      → "Mine iron";
        ///   • <paramref name="oreNeedsBetterPick"/> → "Needs stone pickaxe" (the wood-pickaxe refusal cue);
        ///   • <paramref name="chopReady"/>         → "Chop";
        ///   • else the loot label (already "Press E to …" or "").
        /// Static + dependency-free so the EditMode guard asserts the whole priority table with no scene/OnGUI rig.
        /// </summary>
        public static string ResolveInteractionPrompt(bool boulderMineReady, bool oreMineReady,
                                                      bool oreNeedsBetterPick, bool chopReady, string lootLabel)
        {
            if (boulderMineReady) return "Mine stone";
            if (oreMineReady) return "Mine iron";
            if (oreNeedsBetterPick) return "Needs stone pickaxe";
            if (chopReady) return "Chop";
            return lootLabel ?? "";
        }

        /// <summary>
        /// PURE loot prompt-text resolution (the unit-testable seam): given the nearest in-range pickable (or null)
        /// and the loot key, return the prompt label, or "" when there is NOTHING to prompt for (so the prompt is
        /// hidden). The verb comes from the pickable's own <see cref="IPickable.GatherVerb"/> (default "pick up";
        /// the pond overrides to "collect") so the copy fits the action with ZERO per-item branch — the prompt stays
        /// item-agnostic. The key is the LITERAL letter (E) — layout-agnostic on the Danish keyboard.
        ///   • null target            -> "" (hidden — nothing in range);
        ///   • target with no name    -> "" (defensive: never a half-built "Press E to pick up ");
        ///   • target "berries"       -> "Press E to pick up berries";
        ///   • the pond ("water")     -> "Press E to collect water" (its GatherVerb override).
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
