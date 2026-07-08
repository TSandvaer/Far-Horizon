using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The shared ORE-PILE config + factory (ticket 86cakkmr0 / I-2) — the single host the `ore yield` /
    /// `ore-pile despawn` tunables live on, AND the factory <see cref="MineOre"/> calls to SPAWN a lootable
    /// <see cref="OrePile"/> when a node breaks. The direct sibling of <see cref="LogPileSpawner"/> (the felled-
    /// tree wood drop factory), adapted for iron ore. A SETTING binds to ONE live field on this single host, and
    /// every broken node reads through it — so one value retunes EVERY future ore pile's yield / despawn at once.
    ///
    /// === The tunables ===
    ///   • <see cref="OreYield"/> — iron ore per BROKEN node (default 3; range 1–20). The whole node's ore is
    ///     awarded ONCE, on the breaking strike, AS this many ore in the spawned pile — NOT per strike.
    ///   • <see cref="DespawnSeconds"/> — how long an uncollected pile lingers (default 240s = 4 min).
    ///
    /// === The spawn factory ===
    /// <see cref="SpawnAt"/> builds a fresh <see cref="OrePile"/> at a broken node's position holding
    /// <see cref="OreYield"/> ore, wired to the inventory + the shared opaque material, and EXPLICITLY REGISTERS
    /// it with the <see cref="PickableLooter"/> so the looter finds it on the next E press (the #165 lesson: the
    /// looter only re-discovers on an EMPTY cache; the live build always has ≥1 serialized pickable). The pile is a
    /// RUNTIME object (no bootstrap counterpart), so it is built in code here, NOT serialized into Boot.unity.
    ///
    /// === Serialization ===
    /// Authored editor-time + serialized into Boot.unity (MovementCameraScene.BuildOreNodes), NOT at Awake. The
    /// settings + MineOre bind to this one instance. NO mutable statics (StaticStateResetTests) — the defaults are const.
    /// </summary>
    public class OrePileSpawner : MonoBehaviour
    {
        /// <summary>Default iron ore per BROKEN node. A NAMED constant so the tunable + tests reference one source.</summary>
        public const int DefaultOreYield = 3;

        /// <summary>Range floor — ore yield clamps within [1, 20].</summary>
        public const int OreYieldMin = 1;

        /// <summary>Range ceiling — ore yield clamps within [1, 20].</summary>
        public const int OreYieldMax = 20;

        /// <summary>Default uncollected-pile lifetime (4 min). A NAMED constant.</summary>
        public const float DefaultDespawnSeconds = 240f;

        [Header("Ore-pile tunables (TWEAKABLE — ticket 86cakkmr0)")]
        [Tooltip("Iron ore awarded per BROKEN node. The whole node's ore is awarded ONCE, on the breaking strike, " +
                 "as this many ore in the spawned pile — NOT per strike. default 3 — Sponsor-soak tunes; range 1–20.")]
        public int OreYield = DefaultOreYield;

        [Tooltip("Seconds an UNCOLLECTED ore pile lingers before it disappears. A collected pile is gone " +
                 "immediately; this is the timer for the un-looted remainder. default 240s (4 min).")]
        public float DespawnSeconds = DefaultDespawnSeconds;

        [Header("Visual (the shared opaque material — wired editor-time; runtime fallback builds one)")]
        [Tooltip("The shared OPAQUE low-poly material every spawned ore pile uses (keeps piles on the ~1-draw-call " +
                 "batch path). Wired editor-time to the ore-node rock material so the ore reads as the same ore. " +
                 "Null is tolerated: SpawnAt falls back to the broken node's own rock material, then a built material.")]
        public Material oreMaterial;

        [Header("Looter (the player-side E-loot host — wired editor-time; runtime fallback finds it once)")]
        [Tooltip("The PickableLooter every spawned pile REGISTERS itself with so the player can loot it on E (#165 — " +
                 "the looter only auto-re-discovers when its cache is EMPTY, and the live build always has ≥1 " +
                 "serialized pickable, so a runtime-spawned pile MUST be registered or it is never looted). Wired " +
                 "editor-time; null is tolerated: SpawnAt falls back to a ONE-TIME FindObjectOfType<PickableLooter>().")]
        public PickableLooter looter;

        /// <summary>
        /// Spawn a lootable <see cref="OrePile"/> at <paramref name="position"/> holding <see cref="OreYield"/> ore.
        /// The pile is wired to <paramref name="inventory"/> and the shared ore material (or the
        /// <paramref name="fallbackMaterial"/> — typically the broken node's own rock material — when this spawner
        /// has none). Despawn comes from THIS spawner. The pile is EXPLICITLY REGISTERED with the
        /// <see cref="PickableLooter"/> so the player loots it on the next E press (the #165 requirement). Returns
        /// the spawned pile (null only on a null inventory).
        /// </summary>
        public OrePile SpawnAt(Vector3 position, Inventory inventory, Material fallbackMaterial)
        {
            if (inventory == null) return null;

            var go = new GameObject("OrePile");
            go.transform.position = position;

            var pile = go.AddComponent<OrePile>();
            Material mat = oreMaterial != null ? oreMaterial : fallbackMaterial;
            pile.Initialize(inventory, Mathf.Clamp(OreYield, OreYieldMin, OreYieldMax),
                            Mathf.Max(0f, DespawnSeconds), this, mat);

            if (looter == null) looter = FindObjectOfType<PickableLooter>();
            if (looter != null) looter.RegisterPickable(pile);

            return pile;
        }
    }
}
