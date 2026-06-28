using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The shared LOG-PILE config + factory (ticket 86caf9u5t) — the single "log-pile component" the
    /// `tree-chop wood yield` and `log-pile despawn` SETTINGS bind to, AND the factory <see cref="ChopTree"/>
    /// calls to SPAWN a lootable <see cref="LogPile"/> when a tree fells. Sibling of <see cref="StoneRespawner"/>
    /// (the shared stone-respawn config the `stone respawn time` setting binds to): a SETTING binds to ONE live
    /// field-pair on this single host, and every felled tree reads through it — so ONE slider in the dev settings
    /// panel retunes EVERY future log pile's yield / despawn at once, rather than fanning the setting out across
    /// the hundreds of choppable trees.
    ///
    /// === The two live tunables (AC3 yield, AC5 despawn) ===
    ///   • <see cref="WoodYield"/> — logs per FALLEN tree (default 10, the `tree-chop wood yield` setting drives
    ///     it live; range 1–50). The whole tree's wood is awarded ONCE, on the felling chop, AS this many logs
    ///     in the spawned pile — NOT per chop (AC1/AC2: no wood until the tree falls).
    ///   • <see cref="DespawnSeconds"/> — how long an uncollected pile lingers before it disappears (default
    ///     180s = 3 min, the `log-pile despawn` setting drives it live). A collected pile is gone immediately;
    ///     this is the timer for the UN-collected remainder (AC5).
    /// (The third new setting, chops-to-fell, lives on <see cref="ChopTree.chopsToFell"/> — it is shared across
    /// every tree, not a per-pile value, so it stays on the tree resolver, NOT here.)
    ///
    /// === The spawn factory (AC2) ===
    /// <see cref="SpawnAt"/> builds a fresh <see cref="LogPile"/> GameObject at a felled tree's position holding
    /// <see cref="WoodYield"/> logs, wired to the inventory + the shared opaque log material, and EXPLICITLY
    /// REGISTERS it with the <see cref="PickableLooter"/> (<see cref="PickableLooter.RegisterPickable"/>) so the
    /// looter finds it on the next E press. The registration is REQUIRED, not optional: the looter only
    /// re-discovers lazily when its cache is EMPTY, and the live build ALWAYS has ≥1 serialized pickable
    /// (bush/stick/stone), so a spawned pile would NEVER be auto-discovered without this call (#165). The pile is
    /// a RUNTIME object (it did not exist at bootstrap), so it is built in code here, NOT serialized into
    /// Boot.unity — the one place a pickable is authored at runtime rather than editor-time (the chop fell is a
    /// runtime event), which is exactly why the looter cannot rely on its serialized-set discovery for it.
    ///
    /// === Why a runtime spawn is editor-vs-runtime SAFE (unity-conventions.md §editor-vs-runtime) ===
    /// The usual rule is "author interactions editor-time, never at Awake" — because an Awake-built visual can
    /// ship MANGLED/absent (the legs-up class). That rule is about COMPONENTS THAT MUST BE IN THE SCENE AT BOOT.
    /// A log pile is the opposite: it does not exist until a tree fells (a runtime event with no editor-time
    /// counterpart), so it MUST be spawned at runtime — like a projectile or a particle. The spawner ITSELF (the
    /// config + factory host) IS authored editor-time + serialized (so the settings bind to a stable instance);
    /// only the piles it mints are runtime. The mesh/material are built deterministically in code so a pile reads
    /// identically every spawn (no per-pile asset churn).
    ///
    /// === No mutable statics (StaticStateResetTests) ===
    /// Pure instance state (the two serialized tunables + an optional material ref). The defaults are
    /// <c>const</c>. So this type has NO mutable runtime static and needs NO SubsystemRegistration reset —
    /// the sibling of <see cref="StoneRespawner"/> / WarmthNeed (instance-only state).
    ///
    /// === Serialization ===
    /// Authored editor-time + serialized into Boot.unity (MovementCameraScene.BuildLogPileSpawner, after the
    /// chop tree + inventory exist, before WireChopScatterRoot). NOT added at Awake. The settings panel + the
    /// ChopTree bind to this one instance.
    /// </summary>
    public class LogPileSpawner : MonoBehaviour
    {
        /// <summary>AC2/AC3 "default 10" — the default logs-per-fallen-tree. A NAMED constant (not a magic
        /// literal) so the setting + tests reference one source.</summary>
        public const int DefaultWoodYield = 10;

        /// <summary>AC3 range floor — the `tree-chop wood yield` setting clamps within [1, 50].</summary>
        public const int WoodYieldMin = 1;

        /// <summary>AC3 range ceiling — the `tree-chop wood yield` setting clamps within [1, 50].</summary>
        public const int WoodYieldMax = 50;

        /// <summary>AC5 "default 180s (3 min)" — the default uncollected-pile lifetime. A NAMED constant.</summary>
        public const float DefaultDespawnSeconds = 180f;

        [Header("Log-pile tunables (TWEAKABLE; the chop settings drive these — ticket 86caf9u5t)")]
        [Tooltip("Logs awarded per FALLEN tree (AC2/AC3). The whole tree's wood is awarded ONCE, on the felling " +
                 "chop, as this many logs in the spawned pile — NOT per chop. The `tree-chop wood yield` setting " +
                 "(SettingsCatalog.PopulateChop) drives this live; default 10, range 1–50.")]
        public int WoodYield = DefaultWoodYield;

        [Tooltip("Seconds an UNCOLLECTED log pile lingers before it disappears (AC5). A collected pile is gone " +
                 "immediately; this is the timer for the un-looted remainder. The `log-pile despawn` setting " +
                 "(SettingsCatalog.PopulateChop) drives this live; default 180s (3 min).")]
        public float DespawnSeconds = DefaultDespawnSeconds;

        [Header("Visual (the shared opaque log material — wired editor-time; runtime fallback builds one)")]
        [Tooltip("The shared OPAQUE low-poly material every spawned log pile uses (keeps the piles on the " +
                 "~1-draw-call batch path — unity6-mastery §2). Wired editor-time to the chop tree's trunk " +
                 "material so the logs read as the same wood. Null is tolerated: SpawnAt falls back to the " +
                 "felled tree's own trunk material, then a built warm-bark URP/Lit material.")]
        public Material logMaterial;

        [Header("Looter (the player-side E-loot host — wired editor-time; runtime fallback finds it once)")]
        [Tooltip("The PickableLooter every spawned pile REGISTERS itself with so the player can loot it on E " +
                 "(#165 — the looter only auto-re-discovers when its cache is EMPTY, and the live build always " +
                 "has ≥1 serialized pickable, so a runtime-spawned pile MUST be registered explicitly or it is " +
                 "never looted). Wired editor-time in MovementCameraScene.BuildChopTree (asserted by the #164 " +
                 "capture-gate wiring-guard CaptureGateDepsSceneTests). Null is tolerated: SpawnAt falls back to " +
                 "a ONE-TIME FindObjectOfType<PickableLooter>() (the bare-test / dropped-wiring safety net).")]
        public PickableLooter looter;

        /// <summary>
        /// Spawn a lootable <see cref="LogPile"/> at <paramref name="position"/> holding <see cref="WoodYield"/>
        /// logs (AC2). The pile is wired to <paramref name="inventory"/> and the shared log material (or the
        /// <paramref name="fallbackMaterial"/> — typically the felled tree's own trunk material — when this
        /// spawner has none). Despawn lifetime comes from THIS spawner (so the setting retunes every pile). The
        /// spawned pile is EXPLICITLY REGISTERED with the <see cref="PickableLooter"/>
        /// (<see cref="PickableLooter.RegisterPickable"/>) so the player loots it on the next E press — this is
        /// REQUIRED, not optional: the looter only re-discovers when its cache is EMPTY, and the live build always
        /// has ≥1 serialized pickable, so an unregistered pile would never be found (#165). The <see cref="looter"/>
        /// ref is wired editor-time; a one-time <c>FindObjectOfType</c> is the bare-test / dropped-wiring fallback.
        /// Returns the spawned pile (null only on a null inventory).
        /// </summary>
        public LogPile SpawnAt(Vector3 position, Inventory inventory, Material fallbackMaterial)
        {
            if (inventory == null) return null;

            var go = new GameObject("LogPile");
            go.transform.position = position;

            var pile = go.AddComponent<LogPile>();
            Material mat = logMaterial != null ? logMaterial : fallbackMaterial;
            pile.Initialize(inventory, Mathf.Clamp(WoodYield, WoodYieldMin, WoodYieldMax),
                            Mathf.Max(0f, DespawnSeconds), this, mat);

            // #165 FIX — register the runtime-spawned pile so the LIVE looter finds it on the next E press. The
            // looter's lazy re-discover only fires on an EMPTY cache, and the live build always has ≥1 serialized
            // pickable (bush/stick/stone) → an UNREGISTERED pile is never looted. Wired editor-time; a one-time
            // scene search is the bare-test / dropped-wiring safety net (mirrors ChopTree.Start's spawner self-find).
            if (looter == null) looter = FindObjectOfType<PickableLooter>();
            if (looter != null) looter.RegisterPickable(pile);

            return pile;
        }
    }
}
