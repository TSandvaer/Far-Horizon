using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The tool-vs-resource discriminator (ticket 86caa4bya / Drew's item-model contract §2). An ENUM,
    /// not a bool, so a future third kind (consumable/food, equipment) slots in without a wire-format
    /// break — see contract §6 (the berry "eat" extension reserves exactly this shape).
    /// </summary>
    public enum ItemKind
    {
        Tool,
        Resource,
    }

    /// <summary>
    /// The canonical item DEFINITION (ticket 86caa4bya — implements Drew's VOCABULARY CONTRACT
    /// `team/drew-dev/inventory-item-model-contract.md` §1 VERBATIM). ONE ScriptableObject asset per
    /// distinct item kind (axe, wood, stone, berry) — an asset, not a MonoBehaviour field, not JSON
    /// (unity6-mastery.md §6: all tuning/content config = SO assets).
    ///
    /// Sealed per the contract. The downstream world-resource tickets (chop 86caa4c5c / stone 86caa4c96
    /// / sticks 86caa96rd / berries 86caa5zz3) DEFINE their resource items AGAINST this type — they call
    /// <see cref="InventoryModel.AddItem(ItemDef,int)"/> with the catalog's existing ItemDef; they never
    /// mint a parallel item type or a per-resource counter (contract §9 "the three things every parallel
    /// ticket MUST honor").
    ///
    /// === Binding surface (contract §1) ===
    /// Id / DisplayName / Kind / MaxStack are exposed as get-only properties (backing fields
    /// [SerializeField]) read by the UI-Toolkit slot views (which bind imperatively — set
    /// style.backgroundImage / text directly, same idiom as the settings panel). MaxStack is DERIVED
    /// from Kind (§2), never free-authored, so it can never disagree with Kind.
    /// </summary>
    [CreateAssetMenu(menuName = "Far Horizon/Item Def", fileName = "item")]
    public sealed class ItemDef : ScriptableObject
    {
        /// <summary>The per-slot stack cap for a stackable RESOURCE (contract §2: default 20). Tools = 1.</summary>
        public const int DefaultResourceStack = 20;

        [SerializeField,
         Tooltip("Stable string key, lowercase-kebab. The persistence + lookup key. NEVER reuse/reassign. " +
                 "Canonical ids: axe / wood / stone / berry (contract §3).")]
        private string _id = "";

        [SerializeField,
         Tooltip("Player-facing name (e.g. \"Wood\", \"Berries\"). The HUD/tooltip text.")]
        private string _displayName = "";

        [SerializeField,
         Tooltip("The tool-vs-resource discriminator. The SINGLE source of belt-eligibility + stackability.")]
        private ItemKind _kind = ItemKind.Resource;

        [SerializeField,
         Tooltip("The slot icon (IconBaker-rendered prop sprite). Null -> letter-chip fallback in the UI.")]
        private Sprite _icon;

        /// <summary>Stable string key, lowercase-kebab — the persistence + lookup key (contract §1/§3).</summary>
        public string Id => _id;

        /// <summary>Player-facing name — the HUD/tooltip text (contract §1).</summary>
        public string DisplayName => _displayName;

        /// <summary>The tool-vs-resource discriminator — the single source of belt-eligibility + stackability.</summary>
        public ItemKind Kind => _kind;

        /// <summary>The slot icon. Null -> the UI shows a warm-cream letter-chip fallback (direction §6.3).</summary>
        public Sprite Icon => _icon;

        /// <summary>
        /// Per-slot stack cap, DERIVED from <see cref="Kind"/> (contract §1/§2 — NOT free-authored): a Tool
        /// never stacks (cap 1); a Resource stacks to <see cref="DefaultResourceStack"/> (the tweakable
        /// resource stack-size, default 20). Derived so it can NEVER disagree with Kind.
        /// </summary>
        public int MaxStack => _kind == ItemKind.Tool ? 1 : DefaultResourceStack;

        // === The two static guards — ONE definition, used everywhere (UI deny-glow AND data-model
        // move-rejection). Tools -> belt-allowed + non-stacking; Resources -> inventory-only + stackable.
        // Do NOT add a per-asset bool that could disagree with Kind (contract §2). ===

        /// <summary>Belt-eligibility (contract §2 / AC6): TOOLS may sit on the belt; RESOURCES are
        /// inventory-only. The single rule the UI deny-glow AND the model move-rejection both bind to.</summary>
        public static bool IsBeltEligible(ItemDef def) => def != null && def.Kind == ItemKind.Tool;

        /// <summary>Stackability (contract §2 / AC7): RESOURCES stack (to MaxStack); TOOLS do not. The single
        /// source — derived from Kind, never a separate bool.</summary>
        public static bool IsStackable(ItemDef def) => def != null && def.Kind == ItemKind.Resource;

        /// <summary>
        /// Editor/test-time initializer — sets the immutable identity fields. Used by ItemCatalog asset
        /// authoring (bootstrap) and by EditMode tests that build defs in code (no .asset round-trip needed
        /// for a pure-logic model test). Id/Kind are identity; do not mutate after the catalog ships.
        /// </summary>
        public void Init(string id, string displayName, ItemKind kind, Sprite icon = null)
        {
            _id = id;
            _displayName = displayName;
            _kind = kind;
            _icon = icon;
        }
    }
}
