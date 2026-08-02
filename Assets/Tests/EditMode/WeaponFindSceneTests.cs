using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;
using FarHorizon.Combat;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the FIND-IN-WORLD weapon (ticket 86cah7y5b) is SERIALIZED into the Boot scene the exe
    /// ships — not added at Awake (the editor-vs-runtime trap would mangle or drop an Awake-built component or
    /// visual: the "legs-up" class). Sibling of BoulderSceneTests / MineSceneTests; same regression-guard intent:
    /// drop MovementCameraScene.BuildWeaponFinds (or any of its wiring) and this goes RED in headless CI, rather
    /// than the shipped build silently lacking the whole second acquisition route.
    ///
    /// It also pins the two placement CONSTRAINTS the ticket makes non-negotiable:
    ///   • the find sits on its OWN root ("WeaponFinds"), a DISCRETE scene-author ADD distinct from the seeded
    ///     LowPolyZoneGen scatter, the ore pool and the boulder pool — so the author provably did NOT perturb
    ///     the seed-42 island stream (AC2, bar #1, [[world-is-big-round-island]]);
    ///   • the placement is DETERMINISTIC and NOT a hardcoded raw transform: the same scene re-opened resolves
    ///     the same position, and that position is off-origin + clear of every landmark.
    ///
    /// And the REAL-WORLD ANCHOR as serialized geometry (lowpoly-quality.md §0): the sword's blade sits BELOW
    /// the stump's top face — it is IN the wood, not resting on it and not hovering above it. The shipped-frame
    /// half of that proof is the -verifyWeaponFind side-profile capture; this is the cheap headless half.
    /// </summary>
    public class WeaponFindSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string FindRootName = "WeaponFinds";

        [Test]
        public void BootScene_CarriesTheWeaponFindPool_WiredToItsRoot()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var pool = FindInScene<WeaponFindPool>(scene);
            Assert.IsNotNull(pool,
                "the Boot scene must carry the WeaponFindPool — the find-in-world route + its per-tier " +
                "findability dial (serialized, not Awake-built)");
            Assert.IsNotNull(pool.findRoot,
                "WeaponFindPool.findRoot must be wired editor-time so the pool discovers the authored sites");
            Assert.AreEqual(FindRootName, pool.findRoot.name,
                "the sites live under their OWN root — a DISCRETE scene-author ADD, provably outside the " +
                "seed-42 LowPolyZoneGen scatter stream");
        }

        [Test]
        public void BootScene_CarriesAtLeastOneFind_WiredToInventoryAndPlayer()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var finds = AllFinds();
            Assert.Greater(finds.Length, 0,
                "the Boot scene must carry at least one authored WorldWeaponFind site (BuildWeaponFinds)");

            foreach (var find in finds)
            {
                Assert.IsNotNull(find.inventory, "WorldWeaponFind.inventory must be wired editor-time");
                Assert.IsNotNull(find.player,
                    "WorldWeaponFind.player must be wired editor-time so the pickup arc has a belt to fly to");
                Assert.IsNotNull(find.visual,
                    "WorldWeaponFind.visual must point at the WEAPON child (the thing that bobs + arcs) — if it " +
                    "fell back to the root, the STUMP would bob and fly to the belt too");
                Assert.AreNotEqual(find.transform, find.visual,
                    "the visual must be the weapon CHILD, never the site root (the stump must stay put)");
            }
        }

        [Test]
        public void BootScene_TheFindGrantsTheCanonicalSwordIronId()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            foreach (var find in AllFinds())
                Assert.AreEqual(ItemCatalog.SwordIronId, find.itemId,
                    "the find grants the canonical sword_iron id (Sponsor decision 2026-07-27) — DATA, never a " +
                    "bespoke 'found weapon' type");
        }

        [Test]
        public void BootScene_FindSiteCarriesAStumpAndAWeaponMesh()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            foreach (var find in AllFinds())
            {
                var stump = find.transform.Find("FindStump");
                Assert.IsNotNull(stump, "each find site carries its weathered STUMP host (the story in the world)");
                Assert.IsNotNull(stump.GetComponent<MeshFilter>()?.sharedMesh,
                    "the stump's mesh is authored editor-time (serialized, not Awake-built)");

                var weaponMesh = find.visual.GetComponentInChildren<MeshFilter>(true);
                Assert.IsNotNull(weaponMesh, "each find site carries the iron-sword FBX mesh instance");
                Assert.IsNotNull(weaponMesh.sharedMesh, "…with a real serialized mesh");
            }
        }

        [Test]
        public void BootScene_TheFindUsesTheOneSharedWeaponPaletteMaterial_NoNewMaterial()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var shared = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                FarHorizon.EditorTools.WeaponPackAssetGen.MaterialPath);
            Assert.IsNotNull(shared, "the shared Mat_WeaponPalette must exist (WeaponPackAssetGen.PrepareWeaponPack)");

            foreach (var find in AllFinds())
            foreach (var mr in find.visual.GetComponentsInChildren<MeshRenderer>(true))
                Assert.AreEqual(shared, mr.sharedMaterial,
                    "the found weapon rides the ONE shared palette material — no per-asset atlas and no second " +
                    "weapon material (the ~1-draw-call shared-palette model; ticket OOS 'no new material')");
        }

        [Test]
        public void BootScene_TheFindHasNoMaterialPropertyBlock_SoTheGpuResidentDrawerPathSurvives()
        {
            // AC3's hard constraint: an MPB on the world MeshRenderer disqualifies the instanced path
            // (unity6-mastery.md §2 disqualifier list). The attract cue is transforms-only for exactly this reason.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            foreach (var find in AllFinds())
            foreach (var mr in find.GetComponentsInChildren<MeshRenderer>(true))
                Assert.IsFalse(mr.HasPropertyBlock(),
                    "no MaterialPropertyBlock on any find renderer — it would disqualify the GPU Resident " +
                    "Drawer instanced path (the reason the cue is bob-only, transforms-only)");
        }

        [Test]
        public void BootScene_TheFindIsColliderFree_SoItCannotDisturbTheNavMeshOrTheGroundRaycast()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            foreach (var find in AllFinds())
                Assert.AreEqual(0, find.GetComponentsInChildren<Collider>(true).Length,
                    "the find is collider-free — the player walks up to it; it never blocks the ground raycast " +
                    "and contributes nothing to the seed-42 NavMesh bake");
        }

        [Test]
        public void BootScene_BladeIsInTheWood_AND_GripStandsProud_TheWholeAnchor()
        {
            // THE REAL-WORLD ANCHOR (lowpoly-quality.md §0), BOTH halves: "an iron sword driven POINT-DOWN INTO
            // a weathered stump — blade buried, GRIP UP where a hand would close on it; you pull it UP and OUT."
            //
            // THE FIRST DRAFT OF THIS TEST ASSERTED ONLY THE FIRST HALF, AND IT SHIPPED NONSENSE. The iron-sword
            // FBX has its origin at the grip base with the whole mesh extending +Y, so the point-down flip put
            // EVERY vertex below the pivot and the stump swallowed the sword completely. "Tip below the stump
            // top" is satisfied PERFECTLY by a fully-buried sword — the shipped-build capture showed a bare
            // stump with no weapon in it while the assert stayed green. That is the pond-on-a-hill failure of
            // this feature: the metric cannot tell a sword-in-a-stump from a sword-swallowed-by-a-stump.
            // Both halves are required now, each at the WORST point of the attract bob.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            foreach (var find in AllFinds())
            {
                float lowY = float.MaxValue, highY = float.MinValue, stumpTopY = float.MinValue;
                foreach (var r in find.visual.GetComponentsInChildren<Renderer>(true))
                {
                    lowY = Mathf.Min(lowY, r.bounds.min.y);
                    highY = Mathf.Max(highY, r.bounds.max.y);
                }
                var stump = find.transform.Find("FindStump");
                foreach (var r in stump.GetComponentsInChildren<Renderer>(true))
                    stumpTopY = Mathf.Max(stumpTopY, r.bounds.max.y);
                // The EFFECTIVE amplitude — what the frame can actually do. On the shipped Embedded find the
                // placement gate makes this 0, so the anchor is checked against a seat that genuinely never
                // moves rather than against a bob that no longer exists.
                float bob = Mathf.Abs(find.EffectiveBobAmplitude);

                Assert.Less(lowY + bob, stumpTopY,
                    $"(a) the BLADE must stay INSIDE the stump even at peak bob (tip={lowY:F3} + bob={bob:F3} " +
                    $"vs stumpTop={stumpTopY:F3}) — a sword hovering clear of the wood is not a sword left in a stump");
                Assert.Greater(highY - bob, stumpTopY + 0.1f,
                    $"(b) the GRIP must stand PROUD of the stump even at the bottom of the bob (gripTop={highY:F3} " +
                    $"- bob={bob:F3} vs stumpTop={stumpTopY:F3}) — THIS is the half whose absence let a fully " +
                    "BURIED sword ship to the first capture as a bare stump with nothing in it");
            }
        }

        [Test]
        public void BootScene_AFindWhoseBladeIsInsideItsHost_IsSTILL_ThePlacementRule()
        {
            // AC7 — the Sponsor's rule enforced on the SHIPPED scene: "an item driven into or resting on
            // something is STILL. An item lying loose may bob."
            //
            // ⚠ THE OPERAND CHOICE IS THE POINT (unity-conventions.md §tautological-assert). Asserting
            // `find.placement == Embedded` would be TAUTOLOGICAL: Embedded is the enum's zero value, so a scene
            // in which BuildWeaponFindSite never assigns placement at all still serializes it and the assert
            // still passes. It would detect nothing.
            //
            // So the "is this thing embedded?" half is MEASURED FROM THE GEOMETRY — blade tip below the host's
            // top face AND the weapon planar-centred inside the host's footprint (the same two facts the shipped
            // capture gate checks) — and only the "is it still?" half is read off the component. The two operands
            // now come from independent sources, so flipping the shipped find to Loose, or re-enabling its cue
            // by any other route, turns this RED. Deleting the placement assignment does NOT red it — that case
            // is safe by construction, because the default is the still one.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            int checkedSites = 0;
            foreach (var find in AllFinds())
            {
                var stump = find.transform.Find("FindStump");
                if (stump == null || find.visual == null) continue;

                bool haveW = false, haveS = false;
                Bounds wb = new Bounds(), sb = new Bounds();
                foreach (var r in find.visual.GetComponentsInChildren<Renderer>(true))
                { if (!haveW) { wb = r.bounds; haveW = true; } else wb.Encapsulate(r.bounds); }
                foreach (var r in stump.GetComponentsInChildren<Renderer>(true))
                { if (!haveS) { sb = r.bounds; haveS = true; } else sb.Encapsulate(r.bounds); }
                if (!haveW || !haveS) continue;

                float planar = Vector2.Distance(new Vector2(wb.center.x, wb.center.z),
                                                new Vector2(sb.center.x, sb.center.z));
                bool tipInsideHost = wb.min.y < sb.max.y;
                bool overTheHost = planar <= Mathf.Max(sb.extents.x, sb.extents.z);
                if (!(tipInsideHost && overTheHost)) continue;   // not physically embedded — the rule is silent

                checkedSites++;
                Assert.IsFalse(find.CueMoves,
                    $"this find's blade is MEASURABLY inside its host (tip={wb.min.y:F3} < hostTop={sb.max.y:F3}, " +
                    $"planar={planar:F3}u inside reach) — so it is DRIVEN IN, and a driven-in item is STILL. " +
                    "The 2026-08-02 soak rejected the moving version verbatim: \"the sword is floating, moving " +
                    "in the stump\". Amplitude cannot fix it; only placement can");
                Assert.AreEqual(0f, find.EffectiveBobAmplitude,
                    "…so its live bob amplitude is exactly 0 (the authored value stays non-zero and inert — the " +
                    "gate is in the code, not in this scene's serialized data)");
                Assert.AreEqual(0f, find.EffectiveSwayDegrees,
                    "…and its live sway is exactly 0. Both channels or the sword still wobbles in the wood");
            }

            // Staleness guard: if the scene stops authoring embedded finds, this test must not silently pass on
            // an empty loop and go on reporting the rule as enforced.
            Assert.Greater(checkedSites, 0,
                "at least one authored find is geometrically embedded in its host — otherwise this guard " +
                "measured nothing and its green is meaningless");
        }

        [Test]
        public void BootScene_FindPlacement_IsSeededAndOffOrigin_NotAHardcodedTransform()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var first = SortedSitePositions();
            Assert.Greater(first.Length, 0, "at least one site is authored");
            Debug.Log("[WeaponFindSceneTests] authored site positions: " +
                      string.Join(" | ", first.Select(p => p.ToString("F2"))));

            foreach (var p in first)
            {
                float planar = new Vector2(p.x, p.z).magnitude;
                Assert.Greater(planar, 6f,
                    "a find sits out in the world where a wandering player comes across it — never parked at " +
                    "the origin / on the spawn pad");
                Assert.Less(planar, 30f, "…and inside the proven-walkable loop zone, so it is reachable on NavMesh");
            }

            // Re-open the SAME serialized scene: the placement must be identical. The scatter is deterministic
            // and BAKED into Boot.unity, so a re-gen cannot relocate the find arbitrarily (AC2's destination).
            // Compared as an ORDERED-BY-POSITION SET, not index-by-index: FindObjectsByType(…SortMode.None)
            // gives NO enumeration-order guarantee and demonstrably returns the sites in a different order on a
            // second open (observed 2026-07-28 — the first draft of this test went red on exactly that, with
            // both orderings holding the same two positions). The claim under test is that the PLACEMENT is
            // deterministic, never that Unity's object enumeration is.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var second = SortedSitePositions();
            Assert.AreEqual(first.Length, second.Length, "the same number of sites survives a scene re-open");
            for (int i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], second[i],
                    "the find is at the SAME position after a scene re-open — deterministic seeded placement, " +
                    "not a transform that a re-gen wipes or moves");
        }

        [Test]
        public void BootScene_FindsAreSpreadApart_NotClustered()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var pos = AllFinds().Select(f => f.transform.position).ToArray();
            for (int i = 0; i < pos.Length; i++)
            for (int j = i + 1; j < pos.Length; j++)
            {
                float d = Vector2.Distance(new Vector2(pos[i].x, pos[i].z), new Vector2(pos[j].x, pos[j].z));
                Assert.Greater(d, 4f,
                    "authored find sites are SPREAD across the region (organic, bar #1) — never a cluster of " +
                    "stumps in one clearing, which would read as a loot room rather than a discovery");
            }
        }

        [Test]
        public void BootScene_SettingsPanelIsBackWiredToTheFindPool()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var panel = FindInScene<SettingsPanel>(scene);   // SettingsPanel lives in FarHorizon, not FarHorizon.Settings
            Assert.IsNotNull(panel, "the Boot scene carries the settings console");
            Assert.IsNotNull(panel.weaponFindPool,
                "SettingsPanel.weaponFindPool must be back-wired editor-time (the BuildOreNodes → mineOre " +
                "precedent) or the `Weapon finds` row never registers → a DEAD dial in the soak");
        }

        [Test]
        public void BootScene_CarriesTheWeaponFindVerifyCapture_SoTheVerbCannotHangTheExe()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsNotNull(FindInScene<WeaponFindVerifyCapture>(scene),
                "the Boot scene must carry WeaponFindVerifyCapture — an UNWIRED -verifyX verb NO-OPs and HANGS " +
                "the capture exe (unity-conventions §CI; the #302 lesson)");
        }

        /// <summary>
        /// THE REAL-WORLD ANCHOR, PLANAR HALF — "driven INTO the stump" is a claim about WHERE, not just how
        /// high. This exists because the shipped-build gate went GREEN on a frame showing the sword standing
        /// point-down IN THE BARE GRASS about a metre from an EMPTY stump: every anchor check at the time was
        /// Y-only ("tip below the stump top", "grip above it"), and a sword at exactly the right HEIGHT but
        /// displaced sideways satisfies both perfectly. A metric can be green on nonsense (lowpoly-quality.md
        /// §0). The cause was that the weapon child's TRANSFORM sits on the site origin while the sword MESH is
        /// not centred on its own FBX origin, and the point-down flip mirrors that mesh-space offset through the
        /// pivot — so every transform read 0 and the geometry still landed off-axis. This is the cheap headless
        /// half of the proof; the eyeballed side-profile capture is the other half.
        /// </summary>
        [Test]
        public void BootScene_TheSwordIsPLANARLY_OverItsStump_NotStandingInTheGrassBesideIt()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            foreach (var find in AllFinds())
            {
                var weaponT = find.visual != null ? find.visual : find.transform;
                Assert.IsTrue(TryWorldBounds(weaponT, out var wb), "the find's weapon has renderers to measure");

                var stumpT = find.transform.Find("FindStump");
                Assert.IsNotNull(stumpT, "each find site carries its stump");
                Assert.IsTrue(TryWorldBounds(stumpT, out var sb), "the stump has renderers to measure");

                float planar = Vector2.Distance(new Vector2(wb.center.x, wb.center.z),
                                                new Vector2(sb.center.x, sb.center.z));
                float reach = Mathf.Max(sb.extents.x, sb.extents.z);
                Assert.LessOrEqual(planar, reach,
                    "the sword's planar centre must sit INSIDE the stump's own footprint — otherwise the build " +
                    "shows a sword stuck in the dirt NEXT TO a stump, which is not the thing the anchor " +
                    "sentence describes, however green the height numbers are (measured offset " +
                    planar.ToString("F3") + "u vs stump reach " + reach.ToString("F3") + "u)");
            }
        }

        private static bool TryWorldBounds(Transform t, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>
        /// THE GUARD FOR THE SHIPPED-BUILD DEFECT THIS TICKET ACTUALLY HIT (`find=False`).
        ///
        /// Every other test in this file discovers the sites with <see cref="FindObjectsInactive.Include"/>,
        /// which is the WRONG lens for a runtime bug: it happily returns a site nobody can ever see or loot.
        /// The shipped exe (WeaponFindVerifyCapture's resolve, and the looter's own discovery) uses
        /// <see cref="FindObjectsInactive.Exclude"/>, so an INACTIVE site is invisible to it. The first
        /// -verifyWeaponFind run logged `pool=True find=False` with all four sites correctly authored and
        /// serialized. This test reproduces the RUNTIME lens headlessly and DUMPS the per-site hierarchy state,
        /// so the next reader gets the cause and not just a red assert.
        /// </summary>
        [Test]
        public void BootScene_AtLeastOneFindIsACTIVE_SoTheRuntimeExcludeQueryResolvesIt()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            var included = Object.FindObjectsByType<WorldWeaponFind>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var excluded = Object.FindObjectsByType<WorldWeaponFind>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var f in included)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("[weaponfind-diag] site '").Append(f.name)
                  .Append("' activeSelf=").Append(f.gameObject.activeSelf)
                  .Append(" activeInHierarchy=").Append(f.gameObject.activeInHierarchy)
                  .Append(" componentEnabled=").Append(f.enabled)
                  .Append(" hideFlags=").Append(f.gameObject.hideFlags)
                  .Append(" | ancestors:");
                for (var t = f.transform.parent; t != null; t = t.parent)
                    sb.Append(' ').Append(t.name).Append("(activeSelf=").Append(t.gameObject.activeSelf)
                      .Append(",hideFlags=").Append(t.gameObject.hideFlags).Append(')');
                Debug.Log(sb.ToString());
            }
            Debug.Log("[weaponfind-diag] Include=" + included.Length + " Exclude=" + excluded.Length);

            Assert.Greater(excluded.Length, 0,
                "at least one authored find site must be ACTIVE in the serialized Boot scene. The runtime " +
                "resolves finds with FindObjectsInactive.Exclude, so a site reachable only via Include ships " +
                "as an invisible, un-lootable find — the exact `pool=True find=False` the first " +
                "-verifyWeaponFind run reported (Include=" + included.Length + ", Exclude=" + excluded.Length + ")");
        }

        // Site positions in a STABLE, position-derived order (see the determinism test's note on
        // FindObjectsSortMode.None having no enumeration-order guarantee).
        private static Vector3[] SortedSitePositions()
            => AllFinds().Select(f => f.transform.position)
                         .OrderBy(p => p.x).ThenBy(p => p.z).ToArray();

        private static WorldWeaponFind[] AllFinds()
            => Object.FindObjectsByType<WorldWeaponFind>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }
    }
}
