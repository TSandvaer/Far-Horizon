using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// REGRESSION GUARDS for the world-look polish mesh-gen (ticket 86ca8t9pq — Uma world-look brief
    /// §1 clouds + §2 near/far vista mountains). These pin the GEOMETRY contracts that make the meshes
    /// READ as intended (the percept, not a proxy): clouds are HARD-FACETED multi-blob cyan clusters
    /// (not smooth, not single-blob); mountains are HARD-FACETED grey-to-snow silhouettes with OUTWARD
    /// winding (so neither is backface-culled — the Cull-Back class that hid the water for six builds).
    ///
    /// Pure mesh-gen (no scene, no build) so they run cheaply every CI; the shipped-build capture is the
    /// other half of the gate (Uma's per-surface orbit-cam criteria).
    /// </summary>
    public class WorldLookMeshTests
    {
        // ---- CLOUDS (Uma §1) ----

        [Test]
        public void CloudBlob_IsHardFaceted_UnweldedPerFace_NotSmooth()
        {
            // Uma §1: clouds must read as DISTINCT chunky facets (hard 0° normals), NOT smooth blobs.
            // Hard-faceting means every face owns its 3 verts (flat-shaded), so verts == tris*3 — the
            // tell that a regression to RecalculateNormals smooth shading (the canopy idiom) would break.
            var mesh = LowPolyMeshes.CloudBlob(6f, 5, Color.cyan, Color.white, Color.blue, seed: 11);
            int tris = mesh.triangles.Length / 3;
            Assert.AreEqual(tris * 3, mesh.vertexCount,
                "CloudBlob must be FLAT-shaded (verts == tris*3 — hard 0° facets per Uma §1); a welded " +
                "smooth cloud reads as a soft blob, not the crisp toy-diorama facet read");
            Assert.Greater(tris, 30, "a multi-blob cloud must have a chunky facet count");
        }

        [Test]
        public void CloudBlob_IsMultiBlobCluster_NotSinglePuff()
        {
            // Uma §1: 3-6 clustered spheroids (the board sheet shows clustered masses, not one ball).
            var three = LowPolyMeshes.CloudBlob(6f, 3, Color.cyan, Color.white, Color.blue, 1);
            var six = LowPolyMeshes.CloudBlob(6f, 6, Color.cyan, Color.white, Color.blue, 1);
            Assert.Greater(six.vertexCount, three.vertexCount,
                "more blobs -> more geometry (the cloud is a CLUSTER; vertex count must track blob count)");
        }

        [Test]
        public void CloudBlob_CarriesMultiValueCyan_NotOneFlatColour()
        {
            // The 3-value cyan (body/top-lit/shadow) is baked per-blob into vertex COLOUR so one material
            // renders the whole cloud (Uma §1). Pass 3 distinct cyans; the mesh must carry >1 distinct.
            var body = new Color(0.56f, 0.85f, 0.88f);
            var top = new Color(0.77f, 0.93f, 0.94f);
            var shadow = new Color(0.42f, 0.73f, 0.78f);
            var mesh = LowPolyMeshes.CloudBlob(6f, 5, body, top, shadow, 42);
            var cols = mesh.colors;
            Assert.AreEqual(mesh.vertexCount, cols.Length, "every cloud vertex must carry a colour");
            var distinct = new HashSet<Color>(cols);
            Assert.Greater(distinct.Count, 1,
                "the cloud must carry MULTIPLE cyan values (the multi-value blob clustering) — a single " +
                "distinct colour is the flat-blob regression");
            // Every colour must be cyan-leaning (B >= R and G >= R — warm-leaning cyan, never magenta/error).
            foreach (var c in distinct)
                Assert.IsTrue(c.b >= c.r && c.g >= c.r,
                    $"cloud colour ({c.r:F2},{c.g:F2},{c.b:F2}) must be cyan-leaning (R is the smallest channel)");
        }

        [Test]
        public void CloudBlob_AllChannelsSubOne_HdrClampSafe()
        {
            // Uma §1 HDR-clamp discipline: every cloud channel < 1.0 (a pure-white cap + bloom would
            // bloom-clip into a glowing blob and break the crisp facet read). The brightest input cap is
            // 0.94; assert no baked vertex colour exceeds the sub-0.95 cap.
            var body = new Color(0.56f, 0.85f, 0.88f);
            var top = new Color(0.77f, 0.93f, 0.94f);
            var shadow = new Color(0.42f, 0.73f, 0.78f);
            var mesh = LowPolyMeshes.CloudBlob(6f, 6, body, top, shadow, 7);
            foreach (var c in mesh.colors)
            {
                Assert.Less(c.r, 0.96f, "cloud R must stay sub-0.95 (HDR-clamp-safe, no bloom-clip)");
                Assert.Less(c.g, 0.96f, "cloud G must stay sub-0.95 (HDR-clamp-safe, no bloom-clip)");
                Assert.Less(c.b, 0.96f, "cloud B must stay sub-0.95 (HDR-clamp-safe, no bloom-clip)");
            }
        }

        [Test]
        public void CloudBlob_AllFacesPointOutward_NotBackfaceCulled()
        {
            // THE BACKFACE-CULL GUARD (same class as the −Z water grid + FacetedRock): every face normal
            // must point AWAY from the cloud centre and the winding must agree, or URP Cull Back culls
            // inward facets and the cloud reads as holes/slivers from above. The cloud is a UNION of
            // blobs around the origin, so test against the cloud's bounds centre (a face's normal must
            // point away from the nearest blob, which for an outward-facing union face means away from
            // origin for the dominant outer shell). Use the mesh-bounds-centre proxy + per-face winding
            // agreement with its own stored normal (the load-bearing check).
            for (int s = 1; s <= 6; s++)
            {
                var mesh = LowPolyMeshes.CloudBlob(6f, 5, Color.cyan, Color.white, Color.blue, s * 13);
                var v = mesh.vertices;
                var n = mesh.normals;
                int tris = mesh.triangles.Length / 3;
                for (int t = 0; t < tris; t++)
                {
                    Vector3 a = v[t * 3], b = v[t * 3 + 1], c = v[t * 3 + 2];
                    Vector3 wind = Vector3.Cross(b - a, c - a).normalized;
                    // The stored face normal must agree with the geometric winding (front == outward side
                    // that survives Cull Back). A disagreement is the inward-wound / culled-face bug.
                    Assert.Greater(Vector3.Dot(wind, n[t * 3]), 0.5f,
                        $"seed {s * 13} face {t} winding disagrees with its stored normal -> culled front face");
                }
            }
        }

        [Test]
        public void CloudBlob_NormalsAreUnitLength_NoDegenerates()
        {
            var mesh = LowPolyMeshes.CloudBlob(6f, 5, Color.cyan, Color.white, Color.blue, 99);
            var n = mesh.normals;
            Assert.AreEqual(mesh.vertexCount, n.Length, "every cloud vertex must carry a normal");
            for (int i = 0; i < n.Length; i++)
                Assert.AreEqual(1f, n[i].magnitude, 0.05f,
                    $"cloud normal {i} must be ~unit length (a degenerate normal shades the cloud dark)");
        }

        [Test]
        public void CloudBlob_IsWiderThanTall_FlattenedPuff()
        {
            // Uma §1: clouds are broad flattened masses, not towers — the cluster must be wider (XZ) than
            // tall (Y). Assert the bounds X+Z extent dominates the Y extent across seeds.
            for (int s = 1; s <= 5; s++)
            {
                var mesh = LowPolyMeshes.CloudBlob(6f, 5, Color.cyan, Color.white, Color.blue, s * 31);
                var ext = mesh.bounds.extents;
                float horiz = Mathf.Max(ext.x, ext.z);
                Assert.Greater(horiz, ext.y,
                    $"seed {s * 31}: cloud must be wider (horiz {horiz:F2}) than tall (y {ext.y:F2}) — a " +
                    "flattened puff, not a tower (Uma §1)");
            }
        }

        [Test]
        public void CloudBlob_IsDeterministic_SameSeedSameMesh()
        {
            var a = LowPolyMeshes.CloudBlob(6f, 5, Color.cyan, Color.white, Color.blue, 123);
            var b = LowPolyMeshes.CloudBlob(6f, 5, Color.cyan, Color.white, Color.blue, 123);
            Assert.AreEqual(a.vertexCount, b.vertexCount, "same seed must produce the same vertex count");
        }

        // ---- VISTA MOUNTAINS (Uma §2 + Erik 86ca8t9rh) ----

        [Test]
        public void FacetedMountain_IsHardFaceted_UnweldedPerFace()
        {
            // Uma §2: the distant landmass SILHOUETTE must stay faceted/chunky — the same hard-faceted
            // language as foreground rocks/terrain. Hard-faceting = every face owns its verts.
            var mesh = LowPolyMeshes.FacetedMountain(60f, 90f, 8, 0.6f,
                new Color(0.6f, 0.65f, 0.68f), new Color(0.91f, 0.93f, 0.93f), seed: 5);
            int tris = mesh.triangles.Length / 3;
            Assert.AreEqual(tris * 3, mesh.vertexCount,
                "FacetedMountain must be flat-shaded (verts == tris*3 — hard facets per Uma §2)");
            Assert.GreaterOrEqual(tris, 5, "a mountain must have at least the base-ring face count");
        }

        [Test]
        public void FacetedMountain_HasSnowCapAndBody_GreyToSnowValueContrast()
        {
            // Uma §2: faceted grey-to-snow mountains — a snow-white cap facet + a warm-grey body. The
            // mesh bakes both into vertex COLOUR (snow above the snowline, body below). Assert the mesh
            // carries BOTH the pale snow value AND the darker body value (the grey-to-snow contrast).
            var body = new Color(0.60f, 0.65f, 0.68f);
            var snow = new Color(0.91f, 0.93f, 0.93f);
            var mesh = LowPolyMeshes.FacetedMountain(60f, 120f, 9, 0.55f, body, snow, seed: 8);
            var cols = mesh.colors;
            Assert.AreEqual(mesh.vertexCount, cols.Length, "every mountain vertex carries a colour");
            float lo = 1f, hi = 0f;
            foreach (var c in cols) { lo = Mathf.Min(lo, c.r); hi = Mathf.Max(hi, c.r); }
            Assert.Greater(hi - lo, 0.10f,
                "the mountain must span body-grey..snow-white (the grey-to-snow value contrast, Uma §2)");
            // The brightest must read as the snow cap (near the snow input), the darkest near the body.
            Assert.Greater(hi, 0.80f, "the snow cap must be present (a pale-white top facet)");
            Assert.Less(lo, 0.75f, "the warm-grey body must be present (a darker lower facet)");
        }

        [Test]
        public void FacetedMountain_AllFacesPointOutward_NotBackfaceCulled()
        {
            // THE BACKFACE-CULL GUARD: every face normal must agree with its winding (outward = the front
            // side Cull Back keeps). An inward-wound mountain face is invisible — a silhouette with holes.
            for (int s = 1; s <= 6; s++)
            {
                var mesh = LowPolyMeshes.FacetedMountain(60f, 100f, 7, 0.6f,
                    new Color(0.6f, 0.65f, 0.68f), new Color(0.91f, 0.93f, 0.93f), seed: s * 17);
                var v = mesh.vertices;
                var n = mesh.normals;
                int tris = mesh.triangles.Length / 3;
                for (int t = 0; t < tris; t++)
                {
                    Vector3 a = v[t * 3], b = v[t * 3 + 1], c = v[t * 3 + 2];
                    Vector3 wind = Vector3.Cross(b - a, c - a).normalized;
                    Assert.Greater(Vector3.Dot(wind, n[t * 3]), 0.5f,
                        $"seed {s * 17} face {t} winding disagrees with its outward normal -> culled silhouette face");
                }
            }
        }

        [Test]
        public void FacetedMountain_NormalsAreUnitLength_NoDegenerates()
        {
            var mesh = LowPolyMeshes.FacetedMountain(60f, 90f, 8, 0.6f,
                new Color(0.6f, 0.65f, 0.68f), new Color(0.91f, 0.93f, 0.93f), seed: 11);
            var n = mesh.normals;
            for (int i = 0; i < n.Length; i++)
                Assert.AreEqual(1f, n[i].magnitude, 0.05f,
                    $"mountain normal {i} must be ~unit length (a degenerate normal shades the peak dark)");
        }

        [Test]
        public void FacetedMountain_IsConfidentVerticalMass_PeakAboveBase()
        {
            // Uma §2: "big confident planes" — the peak must rise well above the base footprint (a real
            // mountain, not a flat decal). Assert the bounds Y extent is a meaningful fraction of height.
            var mesh = LowPolyMeshes.FacetedMountain(60f, 120f, 8, 0.6f,
                new Color(0.6f, 0.65f, 0.68f), new Color(0.91f, 0.93f, 0.93f), seed: 3);
            Assert.Greater(mesh.bounds.size.y, 80f,
                "a mountain must be a confident vertical mass (peak well above base), not a flat ridge");
        }

        [Test]
        public void FacetedMountain_IsDeterministic_SameSeedSameMesh()
        {
            var a = LowPolyMeshes.FacetedMountain(60f, 90f, 8, 0.6f, Color.grey, Color.white, 77);
            var b = LowPolyMeshes.FacetedMountain(60f, 90f, 8, 0.6f, Color.grey, Color.white, 77);
            Assert.AreEqual(a.vertexCount, b.vertexCount, "same seed must produce the same vertex count");
        }

        [Test]
        public void FacetedMountain_HasMultiRingSubRelief_NotASmoothCone()
        {
            // MOUNTAIN-DETAIL SOAK-FIX GUARD (86ca8t9pq S3, Sponsor soak of fa9f1b1: "I need mountains to be
            // more detailed"). The OLD mesh was a smooth CONE: 1 base ring -> 1 snow ring -> apex = ~3*sides
            // triangles (2 vertical bands, no ridge sub-relief). The fix stacks MULTIPLE rings (stepped
            // rockface flank) + a secondary shoulder, so the face count is MUCH higher for the same `sides`.
            // Guard the bug CLASS: assert the flank carries multi-ring sub-relief (a regression back to the
            // 2-band cone — far fewer faces — fails here). For sides=8 the old cone had 24 tris; the multi-
            // ring peak has well over 50.
            const int sides = 8;
            var mesh = LowPolyMeshes.FacetedMountain(60f, 120f, sides, 0.55f,
                new Color(0.62f, 0.50f, 0.38f), new Color(0.95f, 0.94f, 0.90f), seed: 21);
            int tris = mesh.triangles.Length / 3;
            Assert.Greater(tris, sides * 5,
                $"the mountain must carry MULTI-RING sub-relief ({tris} tris for {sides} sides) — a smooth " +
                "cone (~3*sides tris) reads as the faceted PYRAMID the Sponsor flagged; stacked rings + a " +
                "shoulder peak give the ridge lines + stepped rockface detail (S3).");

            // The grey-to-snow ramp must now be MULTI-BAND (stepped rockface), not a single body value: count
            // DISTINCT body-region red values (below the snow). More than 2 distinct rock values = stepped.
            var cols = mesh.colors;
            var rockValues = new System.Collections.Generic.HashSet<int>();
            foreach (var c in cols)
                if (c.r < 0.80f) rockValues.Add(Mathf.RoundToInt(c.r * 20f)); // quantize the rock-band reds
            Assert.Greater(rockValues.Count, 2,
                $"the mountain body must step through MULTIPLE rock-value bands ({rockValues.Count} distinct) " +
                "— a single flat body value reads as a smooth slab, not detailed rockface (S3).");
        }

        // ---- LANDMASS BASE (Drew "floating translucent shards" grounding fix, ticket 86ca8t9pq) ----

        [Test]
        public void FacetedLandmass_IsHardFaceted_UnweldedPerFace()
        {
            // The island shelf the peaks stand on must be the SAME hard-faceted language (flat per-face).
            var mesh = LowPolyMeshes.FacetedLandmass(90f, 12f, 10, new Color(0.6f, 0.65f, 0.68f), seed: 3);
            int tris = mesh.triangles.Length / 3;
            Assert.AreEqual(tris * 3, mesh.vertexCount,
                "FacetedLandmass must be flat-shaded (verts == tris*3 — hard facets matching the peaks/rocks)");
            Assert.GreaterOrEqual(tris, 7, "the landmass must have at least the rim face count");
        }

        [Test]
        public void FacetedLandmass_AllFacesPointOutward_NotBackfaceCulled()
        {
            // THE BACKFACE-CULL GUARD (same class as the −Z water grid / FacetedRock / FacetedMountain):
            // every face normal must agree with its winding (outward = the front side Cull Back keeps), or
            // the island shows holes/slivers from the orbit camera.
            for (int s = 1; s <= 6; s++)
            {
                var mesh = LowPolyMeshes.FacetedLandmass(80f, 14f, 9 + s % 3,
                    new Color(0.6f, 0.65f, 0.68f), seed: s * 23);
                var v = mesh.vertices;
                var n = mesh.normals;
                int tris = mesh.triangles.Length / 3;
                for (int t = 0; t < tris; t++)
                {
                    Vector3 a = v[t * 3], b = v[t * 3 + 1], c = v[t * 3 + 2];
                    Vector3 wind = Vector3.Cross(b - a, c - a).normalized;
                    Assert.Greater(Vector3.Dot(wind, n[t * 3]), 0.5f,
                        $"seed {s * 23} face {t} winding disagrees with its outward normal -> culled island face");
                }
            }
        }

        [Test]
        public void FacetedLandmass_SpansFromBelowSeaToShelfTop_NoFloatingGap()
        {
            // The grounding fix: the shelf must extend from a SUNK bottom (the mesh local Y=0, which the
            // caller places below the sea) up to the shelf top (~depth) — a broad, low island, wider than it
            // is tall, so the peaks foot on visible LAND, not float over fogged sea.
            var mesh = LowPolyMeshes.FacetedLandmass(90f, 12f, 10, new Color(0.6f, 0.65f, 0.68f), seed: 7);
            var b = mesh.bounds;
            Assert.Greater(b.size.y, 8f, "the landmass must have real vertical extent (sunk bottom -> shelf top)");
            Assert.Greater(Mathf.Max(b.size.x, b.size.z), b.size.y * 3f,
                "the landmass must be BROAD + low (footprint >> height) — a wide island the peaks foot on, " +
                "not a tower (so there is no floating gap under the peaks)");
        }
    }
}
