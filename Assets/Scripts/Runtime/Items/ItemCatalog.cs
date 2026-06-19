using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The item lookup/export site (ticket 86caa4bya / item-model contract §1). Holds the canonical
    /// <see cref="ItemDef"/> set + <see cref="ById"/> / <see cref="All"/> lookup. Consumers reference the
    /// catalog, never a hard ItemDef field array — so the world-resource tickets do
    /// <c>AddItem(catalog.ById("wood"/"stone"/"berry"), amount)</c> (contract §9).
    ///
    /// The catalog is a ScriptableObject so it ships as `Assets/Data/Items/ItemCatalog.asset` (authored at
    /// bootstrap with the four canonical defs). For pure-logic tests it can also be built in code via
    /// <see cref="BuildDefaults"/> — no .asset round-trip needed to drive the model (AC8).
    /// </summary>
    [CreateAssetMenu(menuName = "Far Horizon/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        // === Canonical ids (contract §3) — the single registry; two tickets never mint two "wood". ===
        public const string AxeId = "axe";
        public const string WoodId = "wood";
        public const string StoneId = "stone";
        public const string BerryId = "berry";

        [SerializeField,
         Tooltip("The canonical item defs (axe / wood / stone / berry). Authored at bootstrap; the world-" +
                 "resource tickets look these up by id, never mint their own.")]
        private List<ItemDef> _all = new List<ItemDef>();

        private Dictionary<string, ItemDef> _byId;

        /// <summary>All registered defs, in registration order.</summary>
        public IReadOnlyList<ItemDef> All => _all;

        /// <summary>Look up a def by its canonical id (null if not present). The downstream-pickup seam.</summary>
        public ItemDef ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndex();
            return _byId.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>True if a def with this id is registered.</summary>
        public bool Has(string id) => ById(id) != null;

        private void EnsureIndex()
        {
            if (_byId != null && _byId.Count == _all.Count) return;
            _byId = new Dictionary<string, ItemDef>();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && !string.IsNullOrEmpty(_all[i].Id))
                    _byId[_all[i].Id] = _all[i];
        }

        /// <summary>Replace the catalog's def set (bootstrap authoring + tests). Rebuilds the index.</summary>
        public void SetAll(IEnumerable<ItemDef> defs)
        {
            _all = new List<ItemDef>(defs);
            _byId = null;
            EnsureIndex();
        }

        /// <summary>
        /// Build the four canonical defs IN CODE and populate the catalog (bootstrap + EditMode tests).
        /// axe = Tool; wood / stone / berry = stackable Resources. Idempotent: clears + rebuilds. Icons
        /// are optional (the UI falls back to a letter-chip when null — direction §6.3).
        /// </summary>
        public void BuildDefaults(Sprite axeIcon = null, Sprite woodIcon = null,
                                  Sprite stoneIcon = null, Sprite berryIcon = null)
        {
            var axe = CreateInstance<ItemDef>(); axe.name = "axe";
            axe.Init(AxeId, "Axe", ItemKind.Tool, axeIcon);

            // BUG 3 (#90): chopped wood shipped with a NULL icon → the slot rendered only a bare "W" letter-
            // chip (model + paint were correct — invDiag trace `0=woodx3` / `chip='W'`), which the Sponsor
            // did not read as "wood obtained". AC5 calls for a recognizable wood ICON. Until a 3D render-the-
            // prop IconBaker lands (Uma's direction), bake a small procedural wood-bundle sprite IN CODE so
            // obtained wood reads as a wood item. Reproducible-from-code (no hand-edited PNG that silently
            // reverts on re-import — unity-conventions.md §recolor-must-be-reproducible). A caller-supplied
            // icon (a future baked sprite) still wins.
            var wood = CreateInstance<ItemDef>(); wood.name = "wood";
            wood.Init(WoodId, "Wood", ItemKind.Resource, woodIcon ?? ItemIconGen.WoodBundle());

            var stone = CreateInstance<ItemDef>(); stone.name = "stone";
            stone.Init(StoneId, "Stone", ItemKind.Resource, stoneIcon ?? ItemIconGen.StonePile());

            var berry = CreateInstance<ItemDef>(); berry.name = "berry";
            berry.Init(BerryId, "Berries", ItemKind.Resource, berryIcon ?? ItemIconGen.BerryCluster());

            SetAll(new[] { axe, wood, stone, berry });
        }
    }
}
