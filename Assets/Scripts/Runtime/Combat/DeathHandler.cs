using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon.Combat
{
    /// <summary>
    /// TIERED DEATH (Combat POC 86cah7xxp, AC2) — the 3 death behaviors ARE the 3 difficulty tiers (LOCKED
    /// decision). Subscribes to the player <see cref="Health.Died"/> event and runs the tier's behavior:
    ///   • EASY   — FAINT & recover IN PLACE. No travel setback; HP restores to full where the castaway
    ///              stands. (Enemy disengage is the enemy lane's concern, 86caaz4vn — this handler just
    ///              revives in place.)
    ///   • MEDIUM — RESPAWN at the last campfire (fallback = the world spawn / "start beach"). Inventory KEPT.
    ///   • HARD   — RESPAWN at camp AND the inventory DROPS at the death spot (a reclaimable drop). Inventory
    ///              cleared on the player; the dropped counts are captured for reclaim.
    ///
    /// === Reuse, don't reinvent (AC2) ===
    /// Respawn location REUSES the CAMPFIRE transform (do NOT invent a checkpoint concept): when a lit
    /// campfire exists, respawn there; else the world spawn captured at Start ("start beach"). The inventory
    /// DROP REUSES the existing <see cref="FarHorizon.Inventory"/> surface (do NOT re-implement inventory) —
    /// the POC captures the dropped item counts + clears them off the player; a full pickable-drop-prop is a
    /// later ticket, so the reclaimable drop is modeled as the captured <see cref="LastDrop"/> counts here.
    ///
    /// Active tier from the difficulty preset (AC8) via <see cref="SurvivalNeed.DifficultyTier"/>.
    ///
    /// === Player move (AC2 respawn) ===
    /// The player root carries the <see cref="NavMeshAgent"/>; a respawn uses <see cref="NavMeshAgent.Warp"/>
    /// (NOT a raw transform.position write — Warp re-plants the agent on the NavMesh; a bare position write
    /// desyncs the agent's internal sim). A rig with no agent falls back to a transform move (a bare test rig).
    ///
    /// NO MUTABLE STATICS (instance state only) — StaticStateResetTests needs no reset here.
    /// </summary>
    public sealed class DeathHandler : MonoBehaviour
    {
        [Tooltip("The player Health whose Died event triggers the tiered death. Serialized editor-time; " +
                 "Awake grabs a sibling Health.")]
        public Health health;

        [Tooltip("The player ROOT transform to respawn (the one carrying the NavMeshAgent). Serialized " +
                 "editor-time; falls back to this GameObject's transform.")]
        public Transform playerRoot;

        [Tooltip("The campfire used as the respawn point when lit (AC2 — reuse the campfire, no checkpoint " +
                 "concept). Wired editor-time; when null OR unlit, respawn falls back to the world spawn.")]
        public Campfire campfire;

        [Tooltip("The inventory dropped (HARD) / kept (MEDIUM) across a respawn (AC2). Wired editor-time; " +
                 "an Awake scene-search fallback. Null → the drop/keep step is skipped (a bare test rig).")]
        public Inventory inventory;

        [Tooltip("The active difficulty tier — selects the death behavior (AC2/AC8). Set from the difficulty " +
                 "preset; default Medium (campfire respawn, inventory kept).")]
        public SurvivalNeed.DifficultyTier tier = SurvivalNeed.DifficultyTier.Medium;

        // The world spawn ("start beach") captured at Start — the respawn fallback when no lit campfire.
        private Vector3 _worldSpawn;
        private bool _spawnCaptured;

        // === Observable outcomes (AC2/AC10 — the tests assert these without a full drop-prop) ===
        /// <summary>How many times a death has been HANDLED (the Died event resolved to a tier behavior).</summary>
        public int DeathCount { get; private set; }
        /// <summary>The tier the LAST death used (for the test to assert the right branch ran).</summary>
        public SurvivalNeed.DifficultyTier LastDeathTier { get; private set; }
        /// <summary>True if the LAST death FAINTED IN PLACE (easy — no respawn move).</summary>
        public bool LastFaintedInPlace { get; private set; }
        /// <summary>The world position the LAST death respawned the player to (== the faint spot on easy).</summary>
        public Vector3 LastRespawnPosition { get; private set; }
        /// <summary>True if the LAST death DROPPED the inventory (hard). False on easy/medium (kept).</summary>
        public bool LastDroppedInventory { get; private set; }
        /// <summary>The (wood, stone, berry) counts DROPPED at the death spot on the LAST hard death — the
        /// reclaimable drop (AC2). Zero on easy/medium. The world position of the drop is
        /// <see cref="LastDropPosition"/>. Modeled as captured counts (a full pickable prop is a later ticket).</summary>
        public int LastDropWood { get; private set; }
        public int LastDropStone { get; private set; }
        public int LastDropBerry { get; private set; }
        /// <summary>Where the LAST hard-death inventory drop landed (the death spot). Meaningful when
        /// <see cref="LastDroppedInventory"/> is true.</summary>
        public Vector3 LastDropPosition { get; private set; }

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            if (playerRoot == null) playerRoot = transform;
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
        }

        private void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void Start()
        {
            CaptureSpawn();
        }

        /// <summary>Capture the world spawn ("start beach") — the respawn fallback when no lit campfire.
        /// Called from Start() in a scene; public + idempotent so an EditMode rig can capture the spawn at
        /// build time (BEFORE moving the player to a death spot) without SendMessage (which asserts in
        /// EditMode). A no-op after the first capture.</summary>
        public void CaptureSpawn()
        {
            if (_spawnCaptured) return;
            _spawnCaptured = true;
            _worldSpawn = playerRoot != null ? playerRoot.position : transform.position;
        }

        /// <summary>Set the active difficulty tier (AC2/AC8) — the difficulty preset calls this. The NEXT
        /// death uses the new tier's behavior.</summary>
        public void SetTier(SurvivalNeed.DifficultyTier t) => tier = t;

        // The Died event handler — runs the active tier's death behavior (AC2).
        private void OnDied()
        {
            HandleDeath(tier);
        }

        /// <summary>
        /// Run the death behavior for <paramref name="deathTier"/> (AC2). Public + tier-parameterized so the
        /// EditMode test drives each branch deterministically (drive tier from the preset). Easy faints in
        /// place; medium respawns at the campfire (fallback world spawn) keeping inventory; hard respawns at
        /// camp AND drops the inventory at the death spot. Always restores HP to full (a revive) and records
        /// the observable outcome. Idempotent per death (the caller/Died event fires it once).
        /// </summary>
        public void HandleDeath(SurvivalNeed.DifficultyTier deathTier)
        {
            CaptureSpawn(); // in case death fires before Start (a bare EditMode rig)

            Vector3 deathSpot = playerRoot != null ? playerRoot.position : transform.position;
            DeathCount++;
            LastDeathTier = deathTier;
            LastFaintedInPlace = false;
            LastDroppedInventory = false;
            LastDropWood = LastDropStone = LastDropBerry = 0;

            switch (deathTier)
            {
                case SurvivalNeed.DifficultyTier.Easy:
                    // FAINT & recover IN PLACE — no travel setback, revive where we stand.
                    LastFaintedInPlace = true;
                    LastRespawnPosition = deathSpot;
                    // No move; just revive.
                    break;

                case SurvivalNeed.DifficultyTier.Hard:
                    // Respawn at camp AND DROP the inventory at the death spot (reclaimable).
                    CaptureAndClearInventoryDrop(deathSpot);
                    LastDroppedInventory = true;
                    RespawnAtCampOrSpawn();
                    break;

                default: // Medium
                    // Respawn at the last campfire (fallback world spawn); inventory KEPT (no drop).
                    RespawnAtCampOrSpawn();
                    break;
            }

            // Revive at full HP (the respawn/faint brings the castaway back — HP is not a need, so this is a
            // direct restore, not a satisfy). RestoreFull re-seeds Health to max + fires Changed for the HUD.
            if (health != null) health.RestoreFull();
        }

        // Capture the reclaimable drop (wood/stone/berry counts) + CLEAR them off the player (HARD tier). The
        // POC models the reclaimable drop as the captured counts + position (a full pickable drop-prop is a
        // later ticket) — reusing the existing Inventory surface (SpendWood-style all-or-nothing drains), NOT
        // a re-implemented inventory. The AXE (a tool) is KEPT even on hard (dropping the tool would soft-lock
        // the loop — the drop is the gathered RESOURCES). If a later ticket drops the tool too, extend here.
        private void CaptureAndClearInventoryDrop(Vector3 deathSpot)
        {
            LastDropPosition = deathSpot;
            if (inventory == null) return;

            LastDropWood = inventory.WoodCount;
            LastDropStone = inventory.Model != null ? inventory.Model.CountItem(ItemCatalog.StoneId) : 0;
            LastDropBerry = inventory.Model != null ? inventory.Model.CountItem(ItemCatalog.BerryId) : 0;

            // Clear the dropped resources off the player (all-or-nothing drains, reusing the model's
            // RemoveItem seam — the same seam SpendWood uses). The captured counts above ARE the drop.
            if (LastDropWood > 0) inventory.SpendWood(LastDropWood);
            if (inventory.Model != null)
            {
                if (LastDropStone > 0) inventory.Model.RemoveItem(ItemCatalog.StoneId, LastDropStone);
                if (LastDropBerry > 0) inventory.Model.RemoveItem(ItemCatalog.BerryId, LastDropBerry);
            }
        }

        // Move the player to the last campfire when lit (AC2 — reuse the campfire, no checkpoint concept),
        // else the captured world spawn ("start beach"). Uses NavMeshAgent.Warp when the root has an agent.
        private void RespawnAtCampOrSpawn()
        {
            Vector3 target = (campfire != null && campfire.IsLit)
                ? campfire.transform.position
                : _worldSpawn;
            LastRespawnPosition = target;
            MovePlayerTo(target);
        }

        private void MovePlayerTo(Vector3 target)
        {
            if (playerRoot == null) return;
            var agent = playerRoot.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.Warp(target);       // re-plant on the NavMesh (a raw position write desyncs the sim)
            else
                playerRoot.position = target; // bare rig (no agent) — a plain move
        }
    }
}
