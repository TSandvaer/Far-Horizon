using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The item-kind discriminator (ticket 86caa4bya / Drew's item-model contract §2; extended by
    /// 86caf7g6f). An ENUM, not a bool, so a third kind slots in without a wire-format break — the
    /// contract §6 reserved EXACTLY this shape ("when a hunger need lands, add ItemKind.Consumable").
    ///
    /// === The three kinds (the single source of belt-eligibility + stackability) ===
    /// - <see cref="Tool"/>     — belt-eligible, NON-stacking (axe). Held + left-clicked to act (chop).
    /// - <see cref="Resource"/> — inventory-ONLY, stacking (wood / stone). Never sits on the belt.
    /// - <see cref="Consumable"/> — belt-eligible, stacking (water / berry). Can be the SELECTED belt item
    ///   so left-click can CONSUME it (drink / eat) — the consume EFFECT wiring is ticket 86caf7a30.
    ///
    /// Belt-eligibility is the SINGLE rule <see cref="IsBeltEligible"/> (Tool OR Consumable), NOT a per-asset
    /// bool (contract §2 forbids a bool that can disagree with Kind). Adding the value BEFORE Resource would
    /// shift the serialized int of existing assets — Consumable is appended LAST to keep wire compatibility.
    /// </summary>
    public enum ItemKind
    {
        Tool,
        Resource,
        Consumable,
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

        /// <summary>
        /// The CURRENT shared per-slot cap for stackable resources/consumables — the live source <see cref="MaxStack"/>
        /// reads (ticket 86cabfa4e — the #90 AC7 `inventory stack size` setting binds to THIS). Defaults to
        /// <see cref="DefaultResourceStack"/> (20), so an untouched build is byte-identical to before this ticket
        /// (every prior call read the const directly). The dev-console `inventory stack size` slider drives it; the
        /// NEXT add/merge reads the new cap (no model rebuild needed). Tools are UNAFFECTED — their cap is 1, derived
        /// from <see cref="ItemKind.Tool"/> in <see cref="MaxStack"/>, never from this field.
        ///
        /// MUTABLE STATIC: per unity6-mastery §5 (statics survive domain reload under Enter-Play-Mode-Options), the
        /// <see cref="ResetStaticState"/> below re-seeds it on every play-enter so the Configurable-Enter-Play-Mode
        /// static-reset audit (StaticStateResetTests) stays green and a soak-dialed value never leaks into the next
        /// play session as a phantom default.
        /// </summary>
        public static int ResourceStackSize = DefaultResourceStack;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => ResourceStackSize = DefaultResourceStack;

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
        /// never stacks (cap 1); a Resource OR Consumable stacks to the live <see cref="ResourceStackSize"/> (the
        /// tweakable resource stack-size, default-seeded from <see cref="DefaultResourceStack"/> = 20; the #90 AC7
        /// `inventory stack size` dev-console setting drives it — ticket 86cabfa4e). Consumables stack like
        /// resources so a belt slot holds a stack of water units / berries (ticket 86caf7g6f DEFAULT —
        /// Sponsor-soak-tunable at 86caf7a30). Derived so it can NEVER disagree with Kind.
        /// </summary>
        public int MaxStack => _kind == ItemKind.Tool ? 1 : ResourceStackSize;

        // === The two static guards — ONE definition, used everywhere (UI deny-glow AND data-model
        // move-rejection). Tools/Consumables -> belt-allowed; Resources/Consumables -> stackable.
        // Do NOT add a per-asset bool that could disagree with Kind (contract §2). ===

        /// <summary>Belt-eligibility (contract §2 / AC6; extended by 86caf7g6f): TOOLS and CONSUMABLES may sit
        /// on the belt (a consumable must be holdable so left-click can drink/eat it — 86caf7a30); RESOURCES
        /// (wood / stone) stay inventory-ONLY. The single rule the UI deny-glow AND the model move-rejection
        /// both bind to. The load-bearing regression guard (86caf7g6f AC5): relaxing this must NOT leak a pure
        /// Resource onto the belt — only Tool OR Consumable, never Resource.</summary>
        public static bool IsBeltEligible(ItemDef def) =>
            def != null && (def.Kind == ItemKind.Tool || def.Kind == ItemKind.Consumable);

        /// <summary>Stackability (contract §2 / AC7): RESOURCES and CONSUMABLES stack (to MaxStack); TOOLS do
        /// not. The single source — derived from Kind, never a separate bool.</summary>
        public static bool IsStackable(ItemDef def) =>
            def != null && (def.Kind == ItemKind.Resource || def.Kind == ItemKind.Consumable);

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
