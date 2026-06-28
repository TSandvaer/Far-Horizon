using System;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A lootable LOG PILE (ticket 86caf9u5t) — the ground drop a FELLED tree leaves behind. When a tree's
    /// final chop fells it, <see cref="ChopTree"/> spawns ONE of these at the tree's spot holding the whole
    /// tree's wood (the `tree-chop wood yield` setting, default 10). The castaway walks up and presses E to LOOT
    /// it — the SAME E-loot verb as stones / sticks / berries (DECISIONS 2026-06-27). This is the redesign that
    /// supersedes the rejected per-chop wood (#157): no wood is awarded until the tree FALLS, and then it lands
    /// as a LOOTABLE pile on the ground, not silently banked.
    ///
    /// === IMPLEMENTS <see cref="IPickable"/> — the shared E-loot surface (86caf7a6q AC1/AC2) ===
    /// The pile is an <see cref="IPickable"/> on the shared E-loot surface, the SAME idiom as
    /// <see cref="StickProp"/> / <see cref="StoneProp"/> / <see cref="BerryBush"/>: the player-side
    /// <see cref="PickableLooter"/> discovers every IPickable, resolves the nearest in-range one, and calls
    /// <see cref="TryLoot"/> when E is pressed. The pile adds NO bespoke pickup input, NO second looter, NO
    /// parallel pickable interface — it just IS an IPickable; the looter finds it (re-discovering its cache
    /// lazily so a RUNTIME-spawned pile is picked up). Walking into range does NOTHING until E.
    ///
    /// === The loot CONTRACT (AC2/AC7 — WHOLE PILE or what FITS; remainder PERSISTS) ===
    /// <see cref="TryLoot"/> grabs the WHOLE pile in one E press — or as much as FITS the pack when it can't all
    /// fit. The un-looted remainder STAYS in the pile (full pack NEVER loses wood — the Sponsor's chosen full-pack
    /// behavior, AC7): press E again after freeing room to grab the rest. This DIFFERS from the stick/stone
    /// (which yield exactly ONE per press): a pile is a BULK drop, so one E empties it if the pack has room.
    /// When the last log is taken the pile is consumed (disappears). Returns true IFF at least one log actually
    /// landed (a full pack lands 0 → false, a clean no-op the looter moves past; the pile is NOT consumed).
    ///
    /// === DESPAWN (AC5) — the pile disappears after a fixed timer ===
    /// An UNCOLLECTED pile lingers <see cref="DespawnSeconds"/> (the `log-pile despawn` setting, default 180s =
    /// 3 min) then DISAPPEARS — the wood is lost if you don't fetch it in time (a soft pressure to collect, kept
    /// short enough that the ground doesn't litter with piles). A COLLECTED pile (emptied by looting) is gone
    /// IMMEDIATELY. The timer runs from spawn in <see cref="Update"/> (wall-clock Time.time, headless-safe).
    ///
    /// === The visual — a low-poly stacked-log mesh on the shared OPAQUE material (lowpoly-quality.md) ===
    /// The pile builds a small faceted mesh of a few crisscrossed hexagonal-prism logs at runtime (no Blender
    /// asset — a simple procedural stack is acceptable for v1 per the ticket). It uses the SHARED opaque log
    /// material handed in by the spawner (the felled tree's own trunk material), so every pile batches on the
    /// ~1-draw-call path (unity6-mastery §2) — NO per-pile texture, NO transparent variant. Flat per-face
    /// normals (no RecalculateNormals self-smooth) keep the faceted low-poly read (lowpoly-quality.md §1).
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) — RUNTIME spawn is correct here ===
    /// UNLIKE every editor-time pickable, a log pile is SPAWNED AT RUNTIME (on the fell event — there is no
    /// editor-time counterpart, the same as a projectile/particle). So it is built in code by
    /// <see cref="LogPileSpawner.SpawnAt"/>, NOT serialized into Boot.unity. The spawner host + its settings ARE
    /// editor-time + serialized; only the piles are runtime. The mesh/material are deterministic so a pile reads
    /// identically every spawn.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[logpile-trace]` lines on spawn / the first loot / a partial loot / despawn so the pile's
    /// runtime state is readable from the build log (the diagnose-via-trace discipline; sibling of the
    /// [stick-trace] / [stone-trace] / [loot-trace] lines). EDITOR-only (stripped from the shipped IL2CPP exe).
    ///
    /// NO MUTABLE STATICS (instance state only) — needs no [RuntimeInitializeOnLoadMethod] reset
    /// (StaticStateResetTests).
    /// </summary>
    public class LogPile : MonoBehaviour, IPickable
    {
        [Header("Loot (AC2 — whole pile or what fits)")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the pile and can loot it on E. " +
                 "Slightly more generous than a single stick/stone (a pile is a bigger ground feature you stand " +
                 "beside) but still arm's-reach. This is the pile's own IPickable.LootRange.")]
        public float lootRadius = 1.4f;

        // Runtime state (set by Initialize from the spawner). A pile is built at runtime, so these are NOT
        // serialized defaults — Initialize is the single authority.
        private Inventory _inventory;
        private int _logsRemaining;        // logs still in the pile (AC7 — the remainder persists across presses)
        private float _despawnSeconds;     // the uncollected-pile lifetime (AC5 — from the spawner's setting)
        private float _despawnAt;          // wall-clock time the uncollected pile disappears
        private bool _consumed;            // true once emptied by looting (or despawned) — CanLoot then false
        private LogPileSpawner _spawner;   // the shared config (despawn lifetime is RE-READ live each frame)

        private bool _tracedSpawn, _tracedFirstLoot, _tracedPartial, _tracedDespawn; // one-shot trace guards

        /// <summary>Logs still in the pile (AC7 — the un-looted remainder). 0 once fully looted/despawned.
        /// Exposed for PlayMode/EditMode tests + the capture so the whole-pile / partial / remainder behavior
        /// is auditable.</summary>
        public int LogsRemaining => _logsRemaining;

        /// <summary>Wall-clock time an uncollected pile despawns (AC5). Exposed so a test can assert the timer
        /// is scheduled from the spawner's despawn setting.</summary>
        public float DespawnAt => _despawnAt;

        /// <summary>The despawn lifetime (seconds) this pile was spawned with — the `log-pile despawn` setting's
        /// value at spawn, RE-READ live from the spawner each frame so a mid-life setting change retunes the
        /// remaining time. Exposed for the no-dead-knob despawn test.</summary>
        public float DespawnSeconds => _spawner != null ? Mathf.Max(0f, _spawner.DespawnSeconds) : _despawnSeconds;

        /// <summary>True until the pile is consumed (fully looted) or despawned. Exposed for tests + the looter's
        /// resolve (a consumed/despawned pile is skipped — E never targets an empty pile).</summary>
        public bool IsAvailable => !_consumed && _logsRemaining > 0;

        /// <summary>
        /// Initialize a runtime-spawned pile (called by <see cref="LogPileSpawner.SpawnAt"/>). Sets the
        /// inventory, the log count (the tree's whole yield), the despawn lifetime, the shared spawner (so the
        /// despawn timer re-reads the live setting), and builds the low-poly stacked-log visual on the shared
        /// material. Idempotent re-init is not supported (a pile is built once).
        /// </summary>
        public void Initialize(Inventory inventory, int logs, float despawnSeconds, LogPileSpawner spawner,
                               Material sharedMaterial)
        {
            _inventory = inventory;
            _logsRemaining = Mathf.Max(0, logs);
            _despawnSeconds = Mathf.Max(0f, despawnSeconds);
            _spawner = spawner;
            _despawnAt = Time.time + _despawnSeconds;
            BuildVisual(sharedMaterial);

            if (!_tracedSpawn)
            {
                _tracedSpawn = true;
                LogPileTrace("spawned at " + transform.position.ToString("F1") + " holding " + _logsRemaining +
                             " logs; despawn in " + _despawnSeconds.ToString("F0") + "s");
            }
        }

        // ============================================================================================
        // IPickable — the WORLD-ITEM side of the shared E-loot surface (86caf7a6q AC1/AC2).
        // ============================================================================================

        /// <summary>IPickable: the pile is loot-able while it still has logs (not consumed/despawned) AND an
        /// inventory is wired. An empty/despawned pile returns false — the looter's resolve skips it.</summary>
        public bool CanLoot => !_consumed && _logsRemaining > 0 && _inventory != null;

        /// <summary>IPickable: the pile's world position (the looter measures planar XZ distance to this —
        /// height-robust, the same idiom as StickProp / StoneProp / ChopTree).</summary>
        public Vector3 LootPosition => transform.position;

        /// <summary>IPickable: the pile's loot reach — its own <see cref="lootRadius"/>. The looter uses THIS
        /// per-item radius, not one global radius.</summary>
        public float LootRange => lootRadius;

        /// <summary>IPickable: the generic prompt name (86cafc6ud) — a pile yields "wood" (the canonical WoodId
        /// resource). The prompt shows "Press E to pick up wood" — the SAME word the stick returns, so both read
        /// identically with no per-item HUD branch (the LootPrompt's load-bearing genericity).</summary>
        public string DisplayName => "wood";

        /// <summary>
        /// IPickable.TryLoot (86caf9u5t AC2/AC7) — loot the WHOLE pile (or what FITS) in one press. Adds up to
        /// <see cref="LogsRemaining"/> <see cref="ItemCatalog.WoodId"/> "wood" to the inventory; whatever doesn't
        /// fit STAYS in the pile (the remainder persists — full pack never loses wood). When the last log is
        /// taken the pile is consumed (disappears). Returns true IFF at least one log actually landed (a full
        /// pack lands 0 → false, a clean no-op the looter moves past; the pile is NOT consumed). Uses the wired
        /// <see cref="_inventory"/>; <paramref name="inv"/> is the interface fallback for a test/edge pile.
        /// </summary>
        public bool TryLoot(Inventory inv)
        {
            if (_inventory == null) _inventory = inv;
            if (_inventory == null || _consumed || _logsRemaining <= 0) return false;

            var catalog = _inventory.Catalog;
            ItemDef wood = catalog != null ? catalog.ById(ItemCatalog.WoodId) : null;
            if (wood == null) return false;

            // Grab the WHOLE pile in one press — AddItem returns the leftover that did NOT fit (the model is
            // all-or-what-fits per stack). The remainder stays in the pile (AC7). NEVER lose wood.
            int requested = _logsRemaining;
            int leftover = _inventory.Model.AddItem(wood, requested);
            int added = requested - leftover;
            if (added <= 0) return false; // full pack — nothing landed, pile NOT consumed (come back for it)

            _logsRemaining = leftover; // the un-looted remainder persists in the pile

            if (_logsRemaining <= 0)
            {
                // The whole pile was taken — consume it (disappears immediately; a collected pile is gone now).
                _consumed = true;
                if (!_tracedFirstLoot)
                {
                    _tracedFirstLoot = true;
                    LogPileTrace("looted the WHOLE pile (+" + added + " wood) -> wood=" +
                                 _inventory.Model.CountItem(ItemCatalog.WoodId) + "; pile EMPTY -> consumed");
                }
                gameObject.SetActive(false); // gone — the looter's CanLoot is now false; no per-loot Destroy churn
            }
            else
            {
                // A partial loot (full-ish pack): some logs landed, the rest persist for a later press (AC7).
                if (!_tracedPartial)
                {
                    _tracedPartial = true;
                    LogPileTrace("PARTIAL loot (+" + added + " wood, " + _logsRemaining + " logs remain in the " +
                                 "pile) -> press E again after freeing room (full pack never loses wood, AC7)");
                }
            }
            return true;
        }

        void Update()
        {
            // DESPAWN (AC5) — an uncollected pile disappears after its lifetime. Re-read the live despawn setting
            // from the spawner each frame so a mid-life setting change retunes the remaining time (no-dead-knob).
            if (_consumed) return;
            if (_spawner != null)
            {
                // Recompute the despawn deadline off the live setting relative to the original spawn baseline.
                float live = Mathf.Max(0f, _spawner.DespawnSeconds);
                if (!Mathf.Approximately(live, _despawnSeconds))
                {
                    _despawnAt += (live - _despawnSeconds); // shift the deadline by the setting delta
                    _despawnSeconds = live;
                }
            }
            if (Time.time >= _despawnAt) Despawn();
        }

        // The pile times out (uncollected) — disappear, losing the un-fetched wood (AC5).
        private void Despawn()
        {
            _consumed = true;
            if (!_tracedDespawn)
            {
                _tracedDespawn = true;
                LogPileTrace("DESPAWNED (uncollected for " + _despawnSeconds.ToString("F0") + "s, " +
                             _logsRemaining + " logs lost) -> pile gone");
            }
            gameObject.SetActive(false);
        }

        // Build a small faceted low-poly stacked-log mesh on the shared opaque material (lowpoly-quality.md). A
        // few crisscrossed hexagonal-prism logs — runtime-built (no Blender asset; a simple procedural stack is
        // the v1 acceptable per the ticket). Flat per-face normals (no RecalculateNormals) keep the faceted read.
        private void BuildVisual(Material sharedMaterial)
        {
            var meshGo = new GameObject("LogPileVisual");
            meshGo.transform.SetParent(transform, false);

            var mf = meshGo.AddComponent<MeshFilter>();
            var mr = meshGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildStackedLogMesh();
            // The shared OPAQUE material keeps every pile on the ~1-draw-call batch path (unity6-mastery §2).
            // Fall back to a built warm-bark URP/Lit material only if none was handed in (a bare test/edge rig).
            mr.sharedMaterial = sharedMaterial != null ? sharedMaterial : BuildFallbackBarkMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // A small stack of 3 short hexagonal-prism logs crisscrossed on the ground — faceted, low-poly.
        // Deterministic (no RNG) so every pile reads identically. Distinct verts per face (flat normals).
        private static Mesh BuildStackedLogMesh()
        {
            var cm = new CombineInstance[3];
            // Two logs on the ground, one resting on top across them — a tidy little pile.
            cm[0] = LogPart(new Vector3(-0.18f, 0.10f, 0f), Quaternion.Euler(0f, 12f, 90f));
            cm[1] = LogPart(new Vector3(0.18f, 0.10f, 0f), Quaternion.Euler(0f, -10f, 90f));
            cm[2] = LogPart(new Vector3(0f, 0.28f, 0f), Quaternion.Euler(0f, 84f, 90f));

            var mesh = new Mesh { name = "LogPileStack" };
            mesh.CombineMeshes(cm, true, true);
            mesh.RecalculateBounds();
            // Per-face flat normals are baked by the hexagonal-prism builder; do NOT RecalculateNormals (it
            // would self-smooth and lose the facets — lowpoly-quality.md §1).
            return mesh;
        }

        private static CombineInstance LogPart(Vector3 pos, Quaternion rot)
        {
            return new CombineInstance
            {
                mesh = HexLogMesh(0.10f, 0.62f),  // radius 0.10m, length 0.62m — chunky low-poly log
                transform = Matrix4x4.TRS(pos, rot, Vector3.one)
            };
        }

        // A faceted hexagonal prism (a chunky log) lying along its local Y, centred at origin. Distinct verts
        // per face so each face is flat-shaded (the low-poly look). 6 sides + 2 caps.
        private static Mesh HexLogMesh(float radius, float length)
        {
            const int sides = 6;
            float half = length * 0.5f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            // Side quads (two tris each), distinct verts per quad for flat normals.
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 p0 = d0 * radius + Vector3.up * -half;
                Vector3 p1 = d1 * radius + Vector3.up * -half;
                Vector3 p2 = d1 * radius + Vector3.up * half;
                Vector3 p3 = d0 * radius + Vector3.up * half;
                Vector3 faceN = ((d0 + d1) * 0.5f).normalized;
                int b = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
                for (int n = 0; n < 4; n++) norms.Add(faceN);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }

            // Two end caps (fans), distinct verts, axial normals.
            AddCap(verts, norms, tris, radius, half, sides, top: true);
            AddCap(verts, norms, tris, radius, -half, sides, top: false);

            var mesh = new Mesh { name = "HexLog" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            return mesh;
        }

        private static void AddCap(System.Collections.Generic.List<Vector3> verts,
                                   System.Collections.Generic.List<Vector3> norms,
                                   System.Collections.Generic.List<int> tris,
                                   float radius, float y, int sides, bool top)
        {
            Vector3 n = top ? Vector3.up : Vector3.down;
            int centre = verts.Count;
            verts.Add(new Vector3(0f, y, 0f)); norms.Add(n);
            int ring = verts.Count;
            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
                norms.Add(n);
            }
            for (int i = 0; i < sides; i++)
            {
                int a = ring + i;
                int b = ring + (i + 1) % sides;
                if (top) { tris.Add(centre); tris.Add(a); tris.Add(b); }
                else { tris.Add(centre); tris.Add(b); tris.Add(a); }
            }
        }

        // A warm-bark URP/Lit material — the FALLBACK only when no shared material is handed in (a bare
        // test/edge rig). The live game always passes the felled tree's trunk material (the ~1-draw-call path).
        private static Material BuildFallbackBarkMaterial()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { name = "LogPileBarkFallback" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.42f, 0.30f, 0.19f));
            else mat.color = new Color(0.42f, 0.30f, 0.19f);
            return mat;
        }

        // [logpile-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call +
        // its argument evaluation (the string concat) from the shipped IL2CPP release exe (unity6-mastery §5/§10).
        // The first-time guards keep it one-shot. Matches the project dev-log gate convention.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogPileTrace(string msg) => Debug.Log("[logpile-trace] " + msg);
    }
}
