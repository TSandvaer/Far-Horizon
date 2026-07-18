using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// A lootable STONE PILE (ticket 86camz9v7 / crafting-redesign ② — boulder-mining) — the ground drop a BROKEN
    /// boulder leaves behind. The direct sibling of <see cref="OrePile"/> (I-2's iron-ore drop), adapted for
    /// <see cref="ItemCatalog.StoneId"/> "stone" (the boulder-mined VOLUME stone source) instead of "iron_ore".
    /// When a boulder's final strike breaks it, <see cref="MineBoulder"/> spawns ONE of these at the boulder's spot
    /// holding the boulder's stone yield (<see cref="StonePileSpawner.StoneYield"/>). The castaway walks up and
    /// presses E to LOOT it — the SAME E-loot verb as the ore pile / log pile / pebbles / berries.
    ///
    /// === stone is a RESOURCE, so it loots into the INVENTORY (exactly like a pebble / wood / ore) ===
    /// "stone" is an <see cref="ItemKind.Resource"/> (inventory-only, stackable — NOT belt-eligible), so looting a
    /// pile adds stone to the inventory GRID via <see cref="InventoryModel.AddItem"/> — the SAME path a pebble
    /// (<see cref="StoneProp"/>) or ore (<see cref="OrePile"/>) takes.
    ///
    /// === IMPLEMENTS <see cref="IPickable"/> — the shared E-loot surface (reuses PickableLooter) ===
    /// A RUNTIME-spawned pile is EXPLICITLY REGISTERED with the looter by <see cref="StonePileSpawner.SpawnAt"/>
    /// (the looter's lazy re-discover only fires on an EMPTY cache, and the live build always has ≥1 serialized
    /// pickable — the #165 lesson). No bespoke pickup input, no second looter.
    ///
    /// === The loot CONTRACT (whole pile or what fits; remainder persists — mirrors OrePile) ===
    /// <see cref="TryLoot"/> grabs the WHOLE pile in one E press — or as much as FITS the pack when it can't all
    /// fit; the un-looted remainder STAYS in the pile (a full pack never loses stone). When the last stone is taken
    /// the pile is consumed. Returns true IFF at least one stone actually landed.
    ///
    /// === Serialization — RUNTIME spawn is correct here (unity-conventions.md §editor-vs-runtime) ===
    /// UNLIKE the editor-time boulder pool, a stone pile is SPAWNED AT RUNTIME (on the break event — no editor-time
    /// counterpart, like a projectile). So it is built in code by <see cref="StonePileSpawner.SpawnAt"/>, NOT
    /// serialized into Boot.unity. The spawner host + its settings ARE editor-time + serialized; only the piles are runtime.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[stonepile-trace]` lines on spawn / first loot / partial / despawn (EDITOR-only; stripped from the
    /// shipped exe). NO MUTABLE STATICS (instance state only) — needs no [RuntimeInitializeOnLoadMethod] reset.
    /// </summary>
    public class StonePile : MonoBehaviour, IPickable
    {
        [Header("Loot (whole pile or what fits)")]
        [Tooltip("Planar (XZ) distance within which the castaway is 'at' the pile and can loot it on E. Arm's-reach " +
                 "(a small ground feature). This is the pile's own IPickable.LootRange.")]
        public float lootRadius = 1.4f;

        private Inventory _inventory;
        private int _stoneRemaining;
        private float _despawnSeconds;
        private float _despawnAt;
        private bool _consumed;
        private StonePileSpawner _spawner;

        private bool _tracedSpawn, _tracedFirstLoot, _tracedPartial, _tracedDespawn;

        /// <summary>Stone still in the pile (the un-looted remainder). 0 once fully looted/despawned.</summary>
        public int StoneRemaining => _stoneRemaining;

        /// <summary>Wall-clock time an uncollected pile despawns.</summary>
        public float DespawnAt => _despawnAt;

        /// <summary>The despawn lifetime (seconds), RE-READ live from the spawner each frame.</summary>
        public float DespawnSeconds => _spawner != null ? Mathf.Max(0f, _spawner.DespawnSeconds) : _despawnSeconds;

        /// <summary>True until the pile is consumed (fully looted) or despawned.</summary>
        public bool IsAvailable => !_consumed && _stoneRemaining > 0;

        /// <summary>
        /// Initialize a runtime-spawned pile (called by <see cref="StonePileSpawner.SpawnAt"/>). Sets the inventory,
        /// the stone count (the boulder's whole yield), the despawn lifetime, the shared spawner, and builds the
        /// low-poly stone-chunk visual on the shared material.
        /// </summary>
        public void Initialize(Inventory inventory, int stone, float despawnSeconds, StonePileSpawner spawner,
                               Material sharedMaterial)
        {
            _inventory = inventory;
            _stoneRemaining = Mathf.Max(0, stone);
            _despawnSeconds = Mathf.Max(0f, despawnSeconds);
            _spawner = spawner;
            _despawnAt = Time.time + _despawnSeconds;
            BuildVisual(sharedMaterial);

            if (!_tracedSpawn)
            {
                _tracedSpawn = true;
                StonePileTrace("spawned at " + transform.position.ToString("F1") + " holding " + _stoneRemaining +
                               " stone; despawn in " + _despawnSeconds.ToString("F0") + "s");
            }
        }

        // ============================================================================================
        // IPickable — the WORLD-ITEM side of the shared E-loot surface.
        // ============================================================================================

        /// <summary>IPickable: loot-able while it still has stone (not consumed/despawned) AND an inventory is wired.</summary>
        public bool CanLoot => !_consumed && _stoneRemaining > 0 && _inventory != null;

        /// <summary>IPickable: the pile's world position (planar XZ distance to this — height-robust).</summary>
        public Vector3 LootPosition => transform.position;

        /// <summary>IPickable: the pile's loot reach — its own <see cref="lootRadius"/>.</summary>
        public float LootRange => lootRadius;

        /// <summary>IPickable: the generic prompt name — a pile yields "stone". "Press E to pick up stone".</summary>
        public string DisplayName => "stone";

        /// <summary>
        /// IPickable.TryLoot — loot the WHOLE pile (or what FITS) in one press. Adds up to <see cref="StoneRemaining"/>
        /// <see cref="ItemCatalog.StoneId"/> to the inventory GRID (stone is a Resource → inventory-only, like a
        /// pebble); whatever doesn't fit STAYS in the pile. When the last stone is taken the pile is consumed. Returns
        /// true IFF at least one stone actually landed (a full pack lands 0 → false, the pile NOT consumed).
        /// </summary>
        public bool TryLoot(Inventory inv)
        {
            if (_inventory == null) _inventory = inv;
            if (_inventory == null || _consumed || _stoneRemaining <= 0) return false;

            var catalog = _inventory.Catalog;
            ItemDef stone = catalog != null ? catalog.ById(ItemCatalog.StoneId) : null;
            if (stone == null) return false;

            int requested = _stoneRemaining;
            int leftover = _inventory.Model.AddItem(stone, requested);
            int added = requested - leftover;
            if (added <= 0) return false; // full pack — nothing landed, pile NOT consumed

            _stoneRemaining = leftover;

            if (_stoneRemaining <= 0)
            {
                _consumed = true;
                if (!_tracedFirstLoot)
                {
                    _tracedFirstLoot = true;
                    StonePileTrace("looted the WHOLE pile (+" + added + " stone) -> stone=" +
                                   _inventory.Model.CountItem(ItemCatalog.StoneId) + "; pile EMPTY -> consumed");
                }
                gameObject.SetActive(false);
            }
            else
            {
                if (!_tracedPartial)
                {
                    _tracedPartial = true;
                    StonePileTrace("PARTIAL loot (+" + added + " stone, " + _stoneRemaining + " stone remain) -> " +
                                   "press E again after freeing room (full pack never loses stone)");
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
                StonePileTrace("DESPAWNED (uncollected for " + _despawnSeconds.ToString("F0") + "s, " +
                               _stoneRemaining + " stone lost) -> pile gone");
            }
            gameObject.SetActive(false);
        }

        // Build a small faceted low-poly stone-chunk cluster on the shared opaque material (lowpoly-quality.md). A few
        // chunky faceted lumps — runtime-built (no Blender asset; v1 acceptable). Flat per-face normals keep facets.
        private void BuildVisual(Material sharedMaterial)
        {
            var meshGo = new GameObject("StonePileVisual");
            meshGo.transform.SetParent(transform, false);

            var mf = meshGo.AddComponent<MeshFilter>();
            var mr = meshGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildStoneClusterMesh();
            mr.sharedMaterial = sharedMaterial != null ? sharedMaterial : BuildFallbackStoneMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // A small cluster of 3 chunky faceted stone lumps resting on the ground — deterministic (no RNG) so every
        // pile reads identically. Distinct verts per face (flat normals). Mirrors OrePile's cluster builder.
        private static Mesh BuildStoneClusterMesh()
        {
            var cm = new CombineInstance[3];
            cm[0] = LumpPart(new Vector3(-0.15f, 0.10f, 0.03f), 0.16f);
            cm[1] = LumpPart(new Vector3(0.16f, 0.09f, -0.06f), 0.14f);
            cm[2] = LumpPart(new Vector3(0.01f, 0.18f, 0.09f), 0.12f);

            var mesh = new Mesh { name = "StonePileCluster" };
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

        // A faceted octahedral lump (a chunky stone nugget), distinct verts per face for flat shading.
        private static Mesh FacetedLumpMesh(float radius)
        {
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

            var mesh = new Mesh { name = "StoneLump" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            return mesh;
        }

        // A warm-grey stone URP/Lit material — the FALLBACK only when no shared material is handed in (a bare rig).
        private static Material BuildFallbackStoneMaterial()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { name = "StonePileFallback" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.50f, 0.48f, 0.45f));
            else mat.color = new Color(0.50f, 0.48f, 0.45f);
            return mat;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void StonePileTrace(string msg) => Debug.Log("[stonepile-trace] " + msg);
    }
}
