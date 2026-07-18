using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The shared STONE-PILE config + factory (ticket 86camz9v7 / crafting-redesign ② — boulder-mining). The direct
    /// sibling of <see cref="OrePileSpawner"/> (I-2's iron-ore-drop factory), adapted for the boulder-mined STONE
    /// drop: the single host the `stone yield` / `stone-pile despawn` tunables live on, AND the factory
    /// <see cref="MineBoulder"/> calls to SPAWN a lootable <see cref="StonePile"/> when a boulder breaks. A SETTING
    /// binds to ONE live field on this single host, so one value retunes EVERY future stone pile at once.
    ///
    /// === The tunables (§ boulder-mining defaults) ===
    ///   • <see cref="StoneYield"/> — stone per BROKEN boulder (default 5 — the VOLUME source, richer than a single
    ///     hand-gathered pebble; range 1–20). Awarded ONCE, on the breaking strike, as this many stone in the pile.
    ///   • <see cref="DespawnSeconds"/> — how long an uncollected pile lingers (default 240s = 4 min, the OrePile default).
    ///
    /// === Serialization ===
    /// Authored editor-time + serialized into Boot.unity (MovementCameraScene.BuildBoulders), NOT at Awake. The
    /// settings + MineBoulder bind to this one instance. NO mutable statics (StaticStateResetTests) — defaults are const.
    /// </summary>
    public class StonePileSpawner : MonoBehaviour
    {
        /// <summary>Default stone per BROKEN boulder (5 — a boulder is the VOLUME source, richer than 1 pebble).
        /// A NAMED constant so the tunable + tests reference one source. default 5 — Sponsor-soak tunes.</summary>
        public const int DefaultStoneYield = 5;

        /// <summary>Range floor — stone yield clamps within [1, 20] (mirrors OrePileSpawner's ore-yield band).</summary>
        public const int StoneYieldMin = 1;

        /// <summary>Range ceiling — stone yield clamps within [1, 20].</summary>
        public const int StoneYieldMax = 20;

        /// <summary>Default uncollected-pile lifetime (4 min — the OrePile default). A NAMED constant.</summary>
        public const float DefaultDespawnSeconds = 240f;

        [Header("Stone-pile tunables (TWEAKABLE — ticket 86camz9v7)")]
        [Tooltip("Stone awarded per BROKEN boulder. The whole boulder's stone is awarded ONCE, on the breaking " +
                 "strike, as this many stone in the spawned pile — NOT per strike. default 5 — Sponsor-soak tunes; range 1–20.")]
        public int StoneYield = DefaultStoneYield;

        [Tooltip("Seconds an UNCOLLECTED stone pile lingers before it disappears. A collected pile is gone " +
                 "immediately; this is the timer for the un-looted remainder. default 240s (4 min).")]
        public float DespawnSeconds = DefaultDespawnSeconds;

        [Header("Visual (the shared opaque material — wired editor-time; runtime fallback builds one)")]
        [Tooltip("The shared OPAQUE low-poly material every spawned stone pile uses (keeps piles on the ~1-draw-call " +
                 "batch path). Wired editor-time to the boulder rock material so the stone reads as the same stone. " +
                 "Null is tolerated: SpawnAt falls back to the broken boulder's own rock material, then a built material.")]
        public Material stoneMaterial;

        [Header("Looter (the player-side E-loot host — wired editor-time; runtime fallback finds it once)")]
        [Tooltip("The PickableLooter every spawned pile REGISTERS itself with so the player can loot it on E (#165 — " +
                 "the looter only auto-re-discovers when its cache is EMPTY, and the live build always has ≥1 " +
                 "serialized pickable, so a runtime-spawned pile MUST be registered or it is never looted). Wired " +
                 "editor-time; null is tolerated: SpawnAt falls back to a ONE-TIME FindObjectOfType<PickableLooter>().")]
        public PickableLooter looter;

        /// <summary>
        /// Spawn a lootable <see cref="StonePile"/> at <paramref name="position"/> holding <see cref="StoneYield"/>
        /// stone. Wired to <paramref name="inventory"/> and the shared stone material (or the
        /// <paramref name="fallbackMaterial"/> — typically the broken boulder's own rock material — when this
        /// spawner has none). Despawn comes from THIS spawner. The pile is EXPLICITLY REGISTERED with the
        /// <see cref="PickableLooter"/> so the player loots it on the next E press (the #165 requirement). Returns
        /// the spawned pile (null only on a null inventory). Mirrors <see cref="OrePileSpawner.SpawnAt"/>.
        /// </summary>
        public StonePile SpawnAt(Vector3 position, Inventory inventory, Material fallbackMaterial)
        {
            if (inventory == null) return null;

            var go = new GameObject("StonePile");
            go.transform.position = position;

            var pile = go.AddComponent<StonePile>();
            Material mat = stoneMaterial != null ? stoneMaterial : fallbackMaterial;
            pile.Initialize(inventory, Mathf.Clamp(StoneYield, StoneYieldMin, StoneYieldMax),
                            Mathf.Max(0f, DespawnSeconds), this, mat);

            if (looter == null) looter = FindObjectOfType<PickableLooter>();
            if (looter != null) looter.RegisterPickable(pile);

            return pile;
        }
    }
}
