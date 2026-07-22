using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A lootable IRON-ORE PILE (ticket 86cakkmr0 / I-2) — the ground drop a BROKEN ore node leaves behind. When
    /// a node's final strike breaks it, <see cref="MineOre"/> spawns ONE of these at the node's spot holding the
    /// node's ore yield (the `ore yield` field on <see cref="OrePileSpawner"/>, default 3). The castaway walks up
    /// and presses E to LOOT it — the SAME E-loot verb as the log pile / stones / sticks / berries (DECISIONS
    /// 2026-06-27). The direct sibling of <see cref="LogPile"/> (the felled-tree wood drop), adapted for
    /// <see cref="ItemCatalog.IronOreId"/> "iron_ore" instead of "wood".
    ///
    /// === iron_ore is a RESOURCE (I-0), so it loots into the INVENTORY, exactly like wood ===
    /// I-0 (#272) minted iron_ore as an <see cref="ItemKind.Resource"/> (inventory-only, stackable — the wood/
    /// stone sibling, NOT belt-eligible; belt = Tool/Consumable only). So looting a pile adds iron_ore to the
    /// inventory GRID via <see cref="InventoryModel.AddItem"/> — the SAME path wood takes from a LogPile. (The
    /// ticket title's "into the belt" is colloquial for "into your kit"; the spec's binding constraint is "mirror
    /// the wood log-pile path", which is inventory-grid for a Resource. See the PR body's terminology note.)
    ///
    /// === IMPLEMENTS <see cref="IPickable"/> — the shared E-loot surface (reuses PickableLooter) ===
    /// The pile is an <see cref="IPickable"/> on the shared E-loot surface, the SAME idiom as
    /// <see cref="LogPile"/> / <see cref="StickProp"/> / <see cref="StoneProp"/>: the player-side
    /// <see cref="PickableLooter"/> discovers every IPickable, resolves the nearest in-range one, and calls
    /// <see cref="TryLoot"/> when E is pressed. A RUNTIME-spawned pile is EXPLICITLY REGISTERED with the looter by
    /// <see cref="OrePileSpawner.SpawnAt"/> (the looter's lazy re-discover only fires on an EMPTY cache, and the
    /// live build always has ≥1 serialized pickable — the #165 lesson). No bespoke pickup input, no second looter.
    ///
    /// === The loot CONTRACT (whole pile or what fits; remainder persists — mirrors LogPile) ===
    /// <see cref="TryLoot"/> grabs the WHOLE pile in one E press — or as much as FITS the pack when it can't all
    /// fit; the un-looted remainder STAYS in the pile (full pack never loses ore). When the last ore is taken the
    /// pile is consumed (disappears). Returns true IFF at least one ore actually landed.
    ///
    /// === DESPAWN — an uncollected pile disappears after a fixed timer ===
    /// An UNCOLLECTED pile lingers <see cref="DespawnSeconds"/> (default 240s = 4 min, RE-READ live from the
    /// spawner) then disappears (a soft pressure to collect). A collected pile is gone immediately.
    ///
    /// === The visual — a low-poly faceted ore-chunk cluster on the shared OPAQUE material (lowpoly-quality.md) ===
    /// The pile builds a small faceted mesh of a few chunky ore lumps at runtime (no Blender asset — a simple
    /// procedural cluster is v1). It uses the SHARED opaque material handed in by the spawner (the broken node's
    /// own rock material) so every pile batches on the ~1-draw-call path (unity6-mastery §2). Flat per-face normals
    /// (no RecalculateNormals) keep the faceted low-poly read (lowpoly-quality.md §1).
    ///
    /// === Serialization — RUNTIME spawn is correct here (unity-conventions.md §editor-vs-runtime) ===
    /// UNLIKE the editor-time ore-node pool, an ore pile is SPAWNED AT RUNTIME (on the break event — no editor-time
    /// counterpart, like a projectile). So it is built in code by <see cref="OrePileSpawner.SpawnAt"/>, NOT
    /// serialized into Boot.unity. The spawner host + its settings ARE editor-time + serialized; only the piles are runtime.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[orepile-trace]` lines on spawn / first loot / partial / despawn (EDITOR-only; stripped from the
    /// shipped exe). NO MUTABLE STATICS (instance state only) — needs no [RuntimeInitializeOnLoadMethod] reset.
    /// </summary>
    public class OrePile : MonoBehaviour, IPickable
    {
        [Header("Loot (whole pile or what fits)")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the pile and can loot it on E. Arm's-reach " +
                 "(a small ground feature). This is the pile's own IPickable.LootRange.")]
        public float lootRadius = 1.4f;

        private Inventory _inventory;
        private int _oreRemaining;
        private float _despawnSeconds;
        private float _despawnAt;
        private bool _consumed;
        private OrePileSpawner _spawner;

        private bool _tracedSpawn, _tracedFirstLoot, _tracedPartial, _tracedDespawn;

        /// <summary>Ore still in the pile (the un-looted remainder). 0 once fully looted/despawned.</summary>
        public int OreRemaining => _oreRemaining;

        /// <summary>Wall-clock time an uncollected pile despawns.</summary>
        public float DespawnAt => _despawnAt;

        /// <summary>The despawn lifetime (seconds), RE-READ live from the spawner each frame.</summary>
        public float DespawnSeconds => _spawner != null ? Mathf.Max(0f, _spawner.DespawnSeconds) : _despawnSeconds;

        /// <summary>True until the pile is consumed (fully looted) or despawned.</summary>
        public bool IsAvailable => !_consumed && _oreRemaining > 0;

        /// <summary>
        /// Initialize a runtime-spawned pile (called by <see cref="OrePileSpawner.SpawnAt"/>). Sets the inventory,
        /// the ore count (the node's whole yield), the despawn lifetime, the shared spawner, and builds the low-poly
        /// ore-chunk visual on the shared material.
        /// </summary>
        public void Initialize(Inventory inventory, int ore, float despawnSeconds, OrePileSpawner spawner,
                               Material sharedMaterial)
        {
            _inventory = inventory;
            _oreRemaining = Mathf.Max(0, ore);
            _despawnSeconds = Mathf.Max(0f, despawnSeconds);
            _spawner = spawner;
            _despawnAt = Time.time + _despawnSeconds;
            BuildVisual(sharedMaterial);

            if (!_tracedSpawn)
            {
                _tracedSpawn = true;
                OrePileTrace("spawned at " + transform.position.ToString("F1") + " holding " + _oreRemaining +
                             " ore; despawn in " + _despawnSeconds.ToString("F0") + "s");
            }
        }

        // ============================================================================================
        // IPickable — the WORLD-ITEM side of the shared E-loot surface.
        // ============================================================================================

        /// <summary>IPickable: loot-able while it still has ore (not consumed/despawned) AND an inventory is wired.</summary>
        public bool CanLoot => !_consumed && _oreRemaining > 0 && _inventory != null;

        /// <summary>IPickable: the pile's world position (planar XZ distance to this — height-robust).</summary>
        public Vector3 LootPosition => transform.position;

        /// <summary>IPickable: the pile's loot reach — its own <see cref="lootRadius"/>.</summary>
        public float LootRange => lootRadius;

        /// <summary>IPickable: the generic prompt name — a pile yields "iron ore". "Press E to pick up iron ore".</summary>
        public string DisplayName => "iron ore";

        /// <summary>
        /// IPickable.TryLoot — loot the WHOLE pile (or what FITS) in one press. Adds up to <see cref="OreRemaining"/>
        /// <see cref="ItemCatalog.IronOreId"/> to the inventory GRID (iron_ore is a Resource → inventory-only, like
        /// wood); whatever doesn't fit STAYS in the pile. When the last ore is taken the pile is consumed. Returns
        /// true IFF at least one ore actually landed (a full pack lands 0 → false, the pile NOT consumed).
        /// </summary>
        public bool TryLoot(Inventory inv)
        {
            if (_inventory == null) _inventory = inv;
            if (_inventory == null || _consumed || _oreRemaining <= 0) return false;

            var catalog = _inventory.Catalog;
            ItemDef ore = catalog != null ? catalog.ById(ItemCatalog.IronOreId) : null;
            if (ore == null) return false;

            int requested = _oreRemaining;
            int leftover = _inventory.Model.AddItem(ore, requested);
            int added = requested - leftover;
            if (added <= 0) return false; // full pack — nothing landed, pile NOT consumed

            _oreRemaining = leftover;

            if (_oreRemaining <= 0)
            {
                _consumed = true;
                if (!_tracedFirstLoot)
                {
                    _tracedFirstLoot = true;
                    OrePileTrace("looted the WHOLE pile (+" + added + " iron_ore) -> iron_ore=" +
                                 _inventory.Model.CountItem(ItemCatalog.IronOreId) + "; pile EMPTY -> consumed");
                }
                gameObject.SetActive(false);
            }
            else
            {
                if (!_tracedPartial)
                {
                    _tracedPartial = true;
                    OrePileTrace("PARTIAL loot (+" + added + " iron_ore, " + _oreRemaining + " ore remain) -> " +
                                 "press E again after freeing room (full pack never loses ore)");
                }
            }
            return true;
        }

        void Update()
        {
            if (_consumed) return;
            if (_spawner != null)
            {
                float live = Mathf.Max(0f, _spawner.DespawnSeconds);
                if (!Mathf.Approximately(live, _despawnSeconds))
                {
                    _despawnAt += (live - _despawnSeconds);
                    _despawnSeconds = live;
                }
            }
            if (Time.time >= _despawnAt) Despawn();
        }

        private void Despawn()
        {
            _consumed = true;
            if (!_tracedDespawn)
            {
                _tracedDespawn = true;
                OrePileTrace("DESPAWNED (uncollected for " + _despawnSeconds.ToString("F0") + "s, " +
                             _oreRemaining + " ore lost) -> pile gone");
            }
            gameObject.SetActive(false);
        }

        // Build a small faceted low-poly ore-chunk cluster on the shared opaque material (lowpoly-quality.md). A few
        // chunky faceted lumps — runtime-built (no Blender asset; v1 acceptable). Flat per-face normals keep facets.
        private void BuildVisual(Material sharedMaterial)
        {
            var meshGo = new GameObject("OrePileVisual");
            meshGo.transform.SetParent(transform, false);

            var mf = meshGo.AddComponent<MeshFilter>();
            var mr = meshGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildOreClusterMesh();
            mr.sharedMaterial = sharedMaterial != null ? sharedMaterial : BuildFallbackOreMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // A small cluster of 3 chunky faceted ore lumps resting on the ground — deterministic (no RNG) so every
        // pile reads identically. Distinct verts per face (flat normals).
        private static Mesh BuildOreClusterMesh()
        {
            var cm = new CombineInstance[3];
            cm[0] = LumpPart(new Vector3(-0.14f, 0.09f, 0.02f), 0.15f);
            cm[1] = LumpPart(new Vector3(0.15f, 0.08f, -0.05f), 0.13f);
            cm[2] = LumpPart(new Vector3(0.01f, 0.17f, 0.08f), 0.11f);

            var mesh = new Mesh { name = "OrePileCluster" };
            mesh.CombineMeshes(cm, true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static CombineInstance LumpPart(Vector3 pos, float radius)
        {
            return new CombineInstance
            {
                mesh = FacetedLumpMesh(radius),
                transform = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one)
            };
        }

        // A faceted octahedral lump (a chunky ore nugget), distinct verts per face for flat shading.
        private static Mesh FacetedLumpMesh(float radius)
        {
            // Octahedron verts (6) — scaled per-axis for a chunky, non-round lump.
            var pts = new Vector3[]
            {
                new Vector3(0f, radius * 1.1f, 0f),
                new Vector3(0f, -radius * 0.9f, 0f),
                new Vector3(radius, 0f, 0f),
                new Vector3(-radius * 0.85f, 0.02f, 0f),
                new Vector3(0f, 0.01f, radius * 0.95f),
                new Vector3(0.02f, 0f, -radius),
            };
            int[] faces =
            {
                0,2,4, 0,4,3, 0,3,5, 0,5,2,
                1,4,2, 1,3,4, 1,5,3, 1,2,5,
            };

            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = pts[faces[f]], b = pts[faces[f + 1]], c = pts[faces[f + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int bi = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                norms.Add(n); norms.Add(n); norms.Add(n);
                tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
            }

            var mesh = new Mesh { name = "OreLump" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            return mesh;
        }

        // A warm rusty-ore URP/Lit material — the FALLBACK only when no shared material is handed in (a bare rig).
        private static Material BuildFallbackOreMaterial()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { name = "OrePileFallback" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.46f, 0.34f, 0.28f));
            else mat.color = new Color(0.46f, 0.34f, 0.28f);
            return mat;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void OrePileTrace(string msg) => Debug.Log("[orepile-trace] " + msg);
    }
}
