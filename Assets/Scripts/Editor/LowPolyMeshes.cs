using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Procedural low-poly primitive MESHES for the smooth-shaded production environment. Ported from
    /// the eval spike (EmbergraveUnitySlice, Assets/Scripts/Editor/LowPolyMeshes.cs, iter-5/8 —
    /// READ-ONLY working spec per ticket 86ca86fux). Each mesh is built WELDED (vertices shared at
    /// face seams where smoothness is wanted) so Mesh.RecalculateNormals averages the normals into a
    /// SMOOTH gradient over the angular facets — the technical "low-poly with smooth shading" look
    /// (averaged vertex normals, not per-face flat normals). Low vertex counts keep the silhouette
    /// readable as "low-poly".
    ///
    /// The GRASS-CLUMP DARK-SHARD FIX (iter-8) is carried forward verbatim — it is the spike's
    /// hard-won foliage-normals pattern (unity-conventions.md "Thin double-sided foliage"): distinct
    /// verts per face + up-biased normals on BOTH faces, so a thin blade never shades near-black.
    /// </summary>
    public static class LowPolyMeshes
    {
        // A tapered cylinder (trunk) — `sides` segments, welded ring verts so the side wall shades
        // smoothly around the trunk. Capped top/bottom. Base sits at local y=0.
        public static Mesh TaperedCylinder(float botR, float topR, float height, int sides)
        {
            sides = Mathf.Max(3, sides);
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // bottom ring (welded), top ring (welded)
            int botStart = verts.Count;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * botR, 0f, Mathf.Sin(a) * botR));
            }
            int topStart = verts.Count;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * topR, height, Mathf.Sin(a) * topR));
            }
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                int b0 = botStart + i, b1 = botStart + ni, t0 = topStart + i, t1 = topStart + ni;
                tris.Add(b0); tris.Add(t0); tris.Add(b1);
                tris.Add(b1); tris.Add(t0); tris.Add(t1);
            }
            // top cap (fan) — separate center vert
            int topC = verts.Count; verts.Add(new Vector3(0f, height, 0f));
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                tris.Add(topStart + i); tris.Add(topC); tris.Add(topStart + ni);
            }

            return Finish(verts, tris, "LP_Trunk");
        }

        // A faceted sphere from a subdivided octahedron, with per-vertex radial JITTER so it reads
        // as an organic boulder/canopy lump rather than a perfect ball. `subdiv` 0 = coarse (boulder),
        // 1 = a bit rounder (canopy). Welded so normals average -> smooth shading over the facets.
        // A HAIR SKULL-CAP (86ca8ca1m soak-fix) — a faceted dome that sits on the chibi's crown to read
        // as sandy-ginger HAIR once the original cap meshes are hidden. It is the UPPER part of a faceted
        // sphere (verts below y=cut dropped + the rim closed), slightly flattened in Y and pulled a touch
        // forward+down at the front (a boyish forward fringe per the v4 identity sheets). Welded +
        // smooth-normalled (the low-poly smooth-shaded look). radius ~ the skull half-width; cut in
        // [-1..1] sphere-space (0 = exact hemisphere, negative dips below the equator to cover more skull).
        public static Mesh HairCap(float radius, float yScale, float cut, int subdiv)
        {
            var baseVerts = new List<Vector3>
            {
                new Vector3(0,  1, 0), new Vector3(0, -1, 0),
                new Vector3( 1, 0, 0), new Vector3(-1, 0, 0),
                new Vector3(0, 0,  1), new Vector3(0, 0, -1),
            };
            var baseTris = new List<int>
            {
                0,2,4, 0,4,3, 0,3,5, 0,5,2,
                1,4,2, 1,3,4, 1,5,3, 1,2,5,
            };
            for (int s = 0; s < subdiv; s++)
            {
                var newTris = new List<int>();
                var midCache = new Dictionary<long, int>();
                for (int t = 0; t < baseTris.Count; t += 3)
                {
                    int a = baseTris[t], b = baseTris[t + 1], c = baseTris[t + 2];
                    int ab = Midpoint(baseVerts, midCache, a, b);
                    int bc = Midpoint(baseVerts, midCache, b, c);
                    int ca = Midpoint(baseVerts, midCache, c, a);
                    newTris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                baseTris = newTris;
            }

            // Project to the sphere, then keep only triangles whose verts are all above the cut plane
            // (the cap). Build a compacted vert list. yScale flattens the dome; a small forward+down
            // pull at the front gives a boyish fringe.
            var sphere = new Vector3[baseVerts.Count];
            for (int i = 0; i < baseVerts.Count; i++) sphere[i] = baseVerts[i].normalized;

            var remap = new Dictionary<int, int>();
            var verts = new List<Vector3>();
            int Keep(int idx)
            {
                if (remap.TryGetValue(idx, out int r)) return r;
                Vector3 n = sphere[idx];
                var p = new Vector3(n.x * radius, n.y * radius * yScale, n.z * radius);
                // forward fringe: where the dome dips toward the front (-Z is the face per the rig),
                // pull it down + forward a touch so hair frames the brow rather than a bald rim.
                if (n.z < -0.2f) { p.y -= radius * 0.12f; p.z -= radius * 0.06f; }
                int ni = verts.Count; verts.Add(p); remap[idx] = ni; return ni;
            }
            var tris = new List<int>();
            for (int t = 0; t < baseTris.Count; t += 3)
            {
                int a = baseTris[t], b = baseTris[t + 1], c = baseTris[t + 2];
                if (sphere[a].y < cut && sphere[b].y < cut && sphere[c].y < cut) continue; // below cap
                tris.Add(Keep(a)); tris.Add(Keep(b)); tris.Add(Keep(c));
            }
            return Finish(verts, tris, "LP_HairCap");
        }

        // A MESSY HAIR CAP (86ca8ca1m SOAKFIX2) — the soak-fix for two Sponsor notes on the smooth dome:
        //   (P2 hair-spike) a "weird BROWN SPIKE on top" — the octahedron's single APEX pole vertex (0,1,0)
        //         stood proud of the rounded surface; invisible top-down, a poke as the cam tilts down.
        //   (P3 hair-messy) the hair read as a smooth skull-cap dome, not natural/tufted hair.
        // Built on the same cut-dome as HairCap, then:
        //   - per-vertex outward+lateral JITTER (deterministic, seeded) breaks the smooth dome into faceted
        //     tufts/clumps so it reads as messy hair (the low-poly chunky idiom — clumps, not a helmet);
        //   - the APEX region (y near the top) is FLATTENED + jittered DOWN/sideways so there is NO single
        //     sharp pole spike — the top reads as tousled clumps, not a point.
        // Welded + RecalculateNormals (smooth-shaded facets). Same params as HairCap + jitter + seed.
        public static Mesh MessyHairCap(float radius, float yScale, float cut, int subdiv, float jitter, int seed)
        {
            var baseVerts = new List<Vector3>
            {
                new Vector3(0,  1, 0), new Vector3(0, -1, 0),
                new Vector3( 1, 0, 0), new Vector3(-1, 0, 0),
                new Vector3(0, 0,  1), new Vector3(0, 0, -1),
            };
            var baseTris = new List<int>
            {
                0,2,4, 0,4,3, 0,3,5, 0,5,2,
                1,4,2, 1,3,4, 1,5,3, 1,2,5,
            };
            for (int s = 0; s < subdiv; s++)
            {
                var newTris = new List<int>();
                var midCache = new Dictionary<long, int>();
                for (int t = 0; t < baseTris.Count; t += 3)
                {
                    int a = baseTris[t], b = baseTris[t + 1], c = baseTris[t + 2];
                    int ab = Midpoint(baseVerts, midCache, a, b);
                    int bc = Midpoint(baseVerts, midCache, b, c);
                    int ca = Midpoint(baseVerts, midCache, c, a);
                    newTris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                baseTris = newTris;
            }

            var sphere = new Vector3[baseVerts.Count];
            for (int i = 0; i < baseVerts.Count; i++) sphere[i] = baseVerts[i].normalized;

            var rnd = new System.Random(seed);
            var remap = new Dictionary<int, int>();
            var verts = new List<Vector3>();
            // CROWN GATE (86ca8ce6y SOAKFIX4 — the deepened de-spike): the spike is visible from the DEFAULT
            // over-the-shoulder GAMEPLAY camera (looking DOWN at the crown, pitch 55-70°), NOT from the front-
            // tilt the prior -verifyHair checked — so SOAKFIX3's soft-clamp (0.30 residual + outward radial
            // jitter on the top verts) still left tufts standing PROUD of the dome surface when viewed from
            // ABOVE (a top-down silhouette pokes wherever a vert exceeds its neighbours' height, not just the
            // ring spread the prior guard measured). FIX: HARD-split the cap into a flat crown PLATEAU and a
            // tousled fringe. Verts ABOVE crownGate (n.y) get NO outward radial jitter, NO lateral wobble, and
            // are HARD-clamped to a single ceiling Y (zero residual) so the entire top reads as ONE smooth
            // rounded surface with no proud apex from any angle (the zero-spike HARD requirement). Jitter is
            // KEPT on the lower fringe/sides (n.y <= crownGate) so the hair still reads tousled, not a helmet
            // (messiness is the secondary goal). Pinned by the tightened crown-flat guard + the over-shoulder
            // -verifyHair capture.
            const float crownGate = 0.62f;  // sphere-space n.y above this is "top crown" -> flat plateau, no jitter
            const float frontGate = -0.2f;  // sphere-space n.z below this is the FRONT fringe (the face side, -Z)
            int Keep(int idx)
            {
                if (remap.TryGetValue(idx, out int r)) return r;
                Vector3 n = sphere[idx];
                bool topCrown = n.y > crownGate;
                bool front = n.z < frontGate;

                // FRONT FRINGE TAME (86ca8ce6y SOAKFIX5 — the 4th-attempt ORANGE TUFT). HairTrace proved the
                // crown plateau is genuinely flat (clamp lands 32 verts on one ceiling, ZERO above it), so the
                // tuft the Sponsor sees from the over-the-shoulder DOWN-looking cam (pitch 55-70°) is NOT a
                // proud crown vertex — it is the FRONT FRINGE jutting FORWARD (trace: frontmost vert local
                // z=-1.216, ~0.22u beyond the nominal radius, because front verts kept the full outward radial
                // jitter rJit up to ~1.20 AND a forward -z lateral wobble). A forward-jutting fringe lobe
                // projects ABOVE the brow/crown silhouette from a camera looking down the -Z face axis (it is
                // closer to + higher in screen-space than the crown behind it) and catches the key light on its
                // forward facets -> reads as a bright "orange" tuft vs the shadowed brown crown top. FIX: the
                // FRONT fringe gets NO outward radial jut (sits at/inside the nominal radius — a tidy brow, not
                // a forward lobe) and NO forward (-z) wobble; only a little sideways x-wobble for messiness. The
                // tousled read is KEPT on the sides/back (where the lobe can't project over the crown).
                float rJit;
                if (topCrown) rJit = 1f;                 // flat plateau: no jut
                else if (front) rJit = 1f;               // front brow: no forward jut (kills the projected tuft)
                else rJit = 1f + ((float)rnd.NextDouble() - 0.4f) * jitter; // sides/back: tousled
                var p = new Vector3(n.x * radius * rJit, n.y * radius * yScale * rJit, n.z * radius * rJit);
                // Lateral wobble (messy, not a helmet) — SIDES/BACK only. The top crown stays smooth; the FRONT
                // gets only a small sideways (x) wobble — NO z wobble, so the fringe can't push further forward.
                if (!topCrown && !front)
                {
                    p.x += ((float)rnd.NextDouble() - 0.5f) * radius * jitter * 0.5f;
                    p.z += ((float)rnd.NextDouble() - 0.5f) * radius * jitter * 0.5f;
                }
                else if (front)
                {
                    p.x += ((float)rnd.NextDouble() - 0.5f) * radius * jitter * 0.35f; // sideways messiness only
                }

                // CROWN PLATEAU (the HARD de-spike): every top-crown vert is clamped to ONE ceiling Y with NO
                // residual, so the crown is a flat-ish rounded plateau — no single vertex (the former "brown
                // spike") can stand above the dome surface from the over-shoulder gameplay cam. The ceiling is
                // a hair below the un-jittered dome top so the plateau sits flush, not raised.
                float crownCeil = radius * yScale * 0.705f;
                if (p.y > crownCeil) p.y = crownCeil;
                // FORWARD FRINGE (boyish brow): pull the front DOWN + slightly forward so hair frames the brow.
                // SOAKFIX5: the pull-down is now HEIGHT-GRADED — the HIGHER a front vert sits (closer to the
                // crown), the HARDER it is pulled down, so NO front vert lands near the crown line where it
                // could project above the brow from the down-looking cam. A front vert at the crown height drops
                // ~0.30u (well clear of the crown); a low front vert keeps the gentle 0.10u boyish pull. The
                // forward (-z) nudge is trimmed (0.06 -> 0.03) so the fringe sits closer to the face, not jutting.
                if (front)
                {
                    float hk = Mathf.InverseLerp(-0.2f, crownGate, n.y); // 0 low-front .. 1 high-front(near crown)
                    p.y -= radius * Mathf.Lerp(0.10f, 0.30f, Mathf.Clamp01(hk));
                    p.z -= radius * 0.03f;
                }
                int ni = verts.Count; verts.Add(p); remap[idx] = ni; return ni;
            }
            var tris = new List<int>();
            for (int t = 0; t < baseTris.Count; t += 3)
            {
                int a = baseTris[t], b = baseTris[t + 1], c = baseTris[t + 2];
                if (sphere[a].y < cut && sphere[b].y < cut && sphere[c].y < cut) continue; // below cap
                tris.Add(Keep(a)); tris.Add(Keep(b)); tris.Add(Keep(c));
            }
            return Finish(verts, tris, "LP_MessyHairCap");
        }

        public static Mesh FacetedSphere(float radius, int subdiv, float jitter, int seed)
        {
            // octahedron base
            var baseVerts = new List<Vector3>
            {
                new Vector3(0,  1, 0), new Vector3(0, -1, 0),
                new Vector3( 1, 0, 0), new Vector3(-1, 0, 0),
                new Vector3(0, 0,  1), new Vector3(0, 0, -1),
            };
            var baseTris = new List<int>
            {
                0,2,4, 0,4,3, 0,3,5, 0,5,2,
                1,4,2, 1,3,4, 1,5,3, 1,2,5,
            };

            // subdivide with a midpoint-weld cache so shared edges stay welded (smooth)
            for (int s = 0; s < subdiv; s++)
            {
                var newTris = new List<int>();
                var midCache = new Dictionary<long, int>();
                for (int t = 0; t < baseTris.Count; t += 3)
                {
                    int a = baseTris[t], b = baseTris[t + 1], c = baseTris[t + 2];
                    int ab = Midpoint(baseVerts, midCache, a, b);
                    int bc = Midpoint(baseVerts, midCache, b, c);
                    int ca = Midpoint(baseVerts, midCache, c, a);
                    newTris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                baseTris = newTris;
            }

            var rnd = new System.Random(seed);
            var verts = new List<Vector3>(baseVerts.Count);
            foreach (var v in baseVerts)
            {
                Vector3 n = v.normalized;
                float r = radius * (1f - jitter * 0.5f + (float)rnd.NextDouble() * jitter);
                verts.Add(n * r);
            }
            return Finish(verts, baseTris, "LP_Sphere");
        }

        // A BLOB CANOPY (board-v2 tree language, ticket 86ca8ce7j) — a CLUSTER of a few overlapping
        // faceted spheroids, NOT a single smooth dome. Per inspiration/2026-06-12_21h11_03.png +
        // 21h10_44 + style-guide-v2 §4: each tree canopy is several low-poly blobs welded into one
        // mesh, so the silhouette reads as clustered foliage lumps (the "blob canopy" the board
        // shows 4 variants of). SOLID volumes (welded, RecalculateNormals smooth) — deliberately NOT
        // thin double-sided cards, which sidesteps the iter-8 thin-foliage near-black-shard normal
        // trap entirely (unity-conventions.md §Low-poly mesh patterns: "prefer solid blob volumes").
        //
        // The multi-VALUE green (the board's "3-4 greens per tree" rule) is NOT in the mesh — it is
        // a per-blob vertex COLOR baked here so a single inline material renders all blobs (no per-blob
        // material churn). The caller passes the body/top-lit/shadow greens; each blob is tinted by its
        // vertical position in the cluster (lower blobs = shadow green, upper = top-lit) plus a small
        // per-blob jitter, so one welded mesh carries the multi-value read.
        //
        //   radius      — overall canopy radius (the cluster roughly fits a sphere of this radius)
        //   blobs       — how many spheroids cluster (4-6 reads like the board; min 3)
        //   bodyGreen / topGreen / shadowGreen — the 3-value palette (style-guide §6 anchors)
        //   seed        — deterministic cluster layout + per-blob jitter
        public static Mesh BlobCanopy(float radius, int blobs, Color bodyGreen, Color topGreen,
            Color shadowGreen, int seed)
        {
            blobs = Mathf.Max(3, blobs);
            var rnd = new System.Random(seed);
            var allVerts = new List<Vector3>();
            var allCols = new List<Color>();
            var allTris = new List<int>();

            // Lay the blobs in a loose cluster: a couple low + wide (the canopy body), the rest
            // stacked up + in toward a slightly-taller crown, so the silhouette is a clustered lump
            // rising to a top, not a flat ring. Each blob is a coarse faceted spheroid (subdiv 0/1).
            for (int b = 0; b < blobs; b++)
            {
                // Cluster placement: first blob centered+low (the trunk-top body); others offset
                // around + above it within the canopy radius.
                float t = b / (float)blobs;
                float ang = t * Mathf.PI * 2f + (float)rnd.NextDouble() * 1.2f;
                float ringR = (b == 0) ? 0f : radius * (0.30f + (float)rnd.NextDouble() * 0.38f);
                float upBias = (b == 0) ? radius * 0.10f
                                        : radius * (0.18f + (float)rnd.NextDouble() * 0.55f);
                Vector3 center = new Vector3(Mathf.Cos(ang) * ringR,
                                             upBias,
                                             Mathf.Sin(ang) * ringR);
                float blobR = radius * (0.48f + (float)rnd.NextDouble() * 0.30f);

                // Per-blob green: blend shadow->top by the blob's height in the cluster, plus a small
                // value jitter so adjacent blobs differ (the multi-value clustering that reads as
                // foliage, not one green ball — style-guide §4 "3-4 green values per tree").
                float heightK = Mathf.Clamp01((center.y + blobR) / (radius * 1.4f));
                Color lo = Color.Lerp(shadowGreen, bodyGreen, Mathf.Clamp01(heightK * 1.6f));
                Color blobCol = Color.Lerp(lo, topGreen, Mathf.Clamp01((heightK - 0.45f) * 1.8f));
                float vj = (float)(rnd.NextDouble() - 0.5) * 0.06f;
                blobCol = new Color(Mathf.Clamp01(blobCol.r + vj),
                                    Mathf.Clamp01(blobCol.g + vj),
                                    Mathf.Clamp01(blobCol.b + vj), 1f);

                AppendBlob(allVerts, allCols, allTris, center, blobR,
                           (b % 2 == 0) ? 1 : 0, jitter: 0.22f, color: blobCol, seed: rnd.Next());
            }

            var mesh = new Mesh { name = "LP_BlobCanopy" };
            mesh.indexFormat = allVerts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(allVerts);
            mesh.SetColors(allCols);
            mesh.SetTriangles(allTris, 0);
            mesh.RecalculateNormals(); // welded-per-blob -> smooth-shaded faceted lumps (solid volume)
            mesh.RecalculateBounds();
            return mesh;
        }

        // Append one faceted spheroid blob (subdivided octahedron, radial jitter) into the shared
        // vert/color/tri lists at `center`, tinted `color`. Each blob's own verts are welded WITHIN
        // the blob (shared edges -> smooth shading), but blobs do NOT weld to each other (distinct
        // index ranges) — the cluster reads as overlapping lumps with crisp inter-blob facet seams,
        // exactly the board's blob-canopy look.
        static void AppendBlob(List<Vector3> verts, List<Color> cols, List<int> tris,
            Vector3 center, float radius, int subdiv, float jitter, Color color, int seed)
        {
            var baseVerts = new List<Vector3>
            {
                new Vector3(0,  1, 0), new Vector3(0, -1, 0),
                new Vector3( 1, 0, 0), new Vector3(-1, 0, 0),
                new Vector3(0, 0,  1), new Vector3(0, 0, -1),
            };
            var baseTris = new List<int>
            {
                0,2,4, 0,4,3, 0,3,5, 0,5,2,
                1,4,2, 1,3,4, 1,5,3, 1,2,5,
            };
            for (int s = 0; s < subdiv; s++)
            {
                var newTris = new List<int>();
                var midCache = new Dictionary<long, int>();
                for (int t = 0; t < baseTris.Count; t += 3)
                {
                    int a = baseTris[t], b = baseTris[t + 1], c = baseTris[t + 2];
                    int ab = Midpoint(baseVerts, midCache, a, b);
                    int bc = Midpoint(baseVerts, midCache, b, c);
                    int ca = Midpoint(baseVerts, midCache, c, a);
                    newTris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                baseTris = newTris;
            }

            var rnd = new System.Random(seed);
            int idxOffset = verts.Count;
            foreach (var v in baseVerts)
            {
                Vector3 n = v.normalized;
                float r = radius * (1f - jitter * 0.5f + (float)rnd.NextDouble() * jitter);
                verts.Add(center + n * r);
                cols.Add(color);
            }
            foreach (var ti in baseTris) tris.Add(idxOffset + ti);
        }

        // A grass clump: a handful of broad blades fanning out from the base. Reads as a low-poly
        // tuft, NOT dark angular shards. Base at y=0.
        //
        // iter-8 ROOT-CAUSE FIX (Sponsor soak iter7: "what are the things sticking up from the ground?"
        // — the clumps read as dark angular shards, not grass). Two defects, both verified by reading
        // the iter-5/7 geometry (not hypothesized):
        //
        //   (1) NORMAL CANCELLATION — the old blade was ONE triangle drawn twice on the SAME 3 verts
        //       with OPPOSITE winding (front: i0,i0+2,i0+1 ; "back": i0,i0+1,i0+2). On a WELDED mesh,
        //       RecalculateNormals averages the per-face normals at each shared vertex; two coincident
        //       opposite-wound faces produce face normals that point in EXACTLY opposite directions, so
        //       the averaged vertex normal is ~ZERO. A near-zero normal under URP/Lit receives ~no
        //       diffuse light -> the blade shades near-black from every angle -> the "dark shard" read.
        //       Drawing the back face on the SAME verts never made a real double-sided blade; it
        //       destroyed the lighting. Fix: build the back face from its OWN duplicated verts so each
        //       side keeps a clean, non-cancelling outward normal (a real two-sided blade).
        //
        //   (2) TOO THIN — single 0.12u-wide spikes read as shards regardless of shading. Fix: wider
        //       blades (base ~0.13u half-width) that taper to a soft tip, plus more blades, so the
        //       silhouette reads as a leafy tuft. A small forward "cup" (the two base corners pulled
        //       slightly toward +/- the blade normal) gives each blade a gentle curve so the tuft has
        //       volume instead of reading as flat cards.
        //
        // Palette is set by the caller (LowPolyZoneGen.BuildGrassClump -> grass-green from the GrassLo/
        // GrassHi terrain ramp), so this only fixes geometry/shading; the green is already correct.
        public static Mesh GrassClump(float height, int blades, int seed)
        {
            blades = Mathf.Max(5, blades);
            var rnd = new System.Random(seed);
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();   // explicit per-blade normals — do NOT RecalculateNormals
            var tris = new List<int>();
            for (int b = 0; b < blades; b++)
            {
                float a = (b / (float)blades) * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.8f;
                float spread = 0.05f + (float)rnd.NextDouble() * 0.20f;
                float bx = Mathf.Cos(a) * spread, bz = Mathf.Sin(a) * spread;
                float h = height * (0.7f + (float)rnd.NextDouble() * 0.6f);
                float w = 0.11f + (float)rnd.NextDouble() * 0.05f; // WIDER half-width (was 0.06) -> reads leafy
                float lean = 0.10f + (float)rnd.NextDouble() * 0.22f;

                // Blade-local frame: outward = the fan direction (cos a, sin a); the blade plane is
                // spanned by a side axis (perpendicular, in XZ) and up.
                Vector3 side = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)); // perpendicular to fan dir, in XZ
                Vector3 baseL = new Vector3(bx, 0f, bz) - side * w;
                Vector3 baseR = new Vector3(bx, 0f, bz) + side * w;
                Vector3 tip   = new Vector3(bx + Mathf.Cos(a) * lean, h, bz + Mathf.Sin(a) * lean);

                // NORMAL CHOICE (iter-8, EMPIRICAL — verified via a normal-distribution probe, not
                // hypothesized): the FIRST fix attempt gave the two faces opposite geometric normals
                // (+nFront / -nFront). The probe showed 27/42 verts then had N·L<0.15 against the warm
                // key (sun travels DOWN at keyDir.y~-0.74) — every back face (and many fronts) pointed
                // away from the sun and shaded near-black: STILL dark shards. Real low-poly grass is
                // shaded as if it catches light from ABOVE on BOTH sides (it's thin foliage, not opaque
                // solid geometry). So bias BOTH faces' normals strongly toward +up (with a little of the
                // blade's own outward tilt for shape) — every blade face then reads lit by the key + the
                // cool sky-fill from any camera angle. No face can shade dark because no normal points
                // down/away from the overhead key. Both sides share this up-biased normal; the two faces
                // differ only in WINDING (so the tuft is still solid from both sides, no culling holes).
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 nUp = (Vector3.up * 0.85f + outward * 0.15f).normalized;

                // FRONT face — its own 3 verts (front winding).
                int f0 = verts.Count;
                verts.Add(baseL); verts.Add(tip); verts.Add(baseR);
                normals.Add(nUp); normals.Add(nUp); normals.Add(nUp);
                tris.Add(f0); tris.Add(f0 + 1); tris.Add(f0 + 2);

                // BACK face — DUPLICATE verts (not shared, so nothing averages to zero), reversed winding
                // so the blade is solid from the other side too, SAME up-biased normal so the back also
                // reads lit (never the dark-shard back face). Distinct verts keep the explicit normals
                // intact (no RecalculateNormals to re-cancel them).
                int b0 = verts.Count;
                verts.Add(baseL); verts.Add(tip); verts.Add(baseR);
                normals.Add(nUp); normals.Add(nUp); normals.Add(nUp);
                tris.Add(b0); tris.Add(b0 + 2); tris.Add(b0 + 1);
            }
            return FinishWithNormals(verts, normals, tris, "LP_Grass");
        }

        // A faceted cone (campfire flame / tent / simple spike), apex up. `sides` ring segments, welded
        // base ring so the side wall shades smoothly around it. Base sits at local y=0, apex at y=height.
        // Used for the U2-4 campfire flame (a warm low-poly tongue of fire) — RIDES the same welded
        // smooth-shaded idiom as the trunk/canopy, not a fresh prop style (the art-direction gate).
        public static Mesh Cone(float baseR, float height, int sides)
        {
            sides = Mathf.Max(3, sides);
            var verts = new List<Vector3>();
            var tris = new List<int>();

            int ringStart = verts.Count;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * baseR, 0f, Mathf.Sin(a) * baseR));
            }
            int apex = verts.Count; verts.Add(new Vector3(0f, height, 0f));
            // side faces (apex fan)
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                tris.Add(ringStart + i); tris.Add(apex); tris.Add(ringStart + ni);
            }
            // base cap (so the cone isn't hollow from below) — separate center vert
            int baseC = verts.Count; verts.Add(Vector3.zero);
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                tris.Add(ringStart + i); tris.Add(ringStart + ni); tris.Add(baseC);
            }
            return Finish(verts, tris, "LP_Cone");
        }

        // A flat ground-decal DISC for the castaway's contact/BLOB SHADOW (ticket 86ca8ca1m — "blob
        // shadow fit to its footprint" AC). Lies in the local XZ plane (y=0), a fan of `sides` triangles
        // from a center vertex. Soft falloff is baked into VERTEX COLOR ALPHA: the center vertex is
        // opaque (`color` with alpha `centerAlpha`), the rim ring is fully transparent (alpha 0) — so a
        // vertex-color transparent material renders a soft round shadow that fades at the edge, no
        // texture asset (churn-free, ships in the stripped build via the always-included unlit shader).
        //
        // FIT to the chibi footprint: the caller passes `radius` sized to the chibi's blocky stance, so
        // the shadow grounds the toy-chunky silhouette. RGB carries the shadow tone; alpha the falloff.
        public static Mesh BlobShadowDisc(float radius, int sides, Color color, float centerAlpha)
        {
            sides = Mathf.Max(6, sides);
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            int center = verts.Count;
            verts.Add(Vector3.zero);
            cols.Add(new Color(color.r, color.g, color.b, centerAlpha)); // opaque core

            int ringStart = verts.Count;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
                cols.Add(new Color(color.r, color.g, color.b, 0f)); // transparent rim -> soft falloff
            }
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                // Wind so the disc faces UP (+Y) for a ground decal viewed from the orbit camera above.
                tris.Add(center); tris.Add(ringStart + ni); tris.Add(ringStart + i);
            }

            var mesh = new Mesh { name = "LP_BlobShadow" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            // Flat disc: a single up normal on every vert (unlit material ignores it, but keep it sane).
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int idx)) return idx;
            Vector3 m = (verts[a] + verts[b]) * 0.5f;
            int newIdx = verts.Count;
            verts.Add(m);
            cache[key] = newIdx;
            return newIdx;
        }

        static Mesh Finish(List<Vector3> verts, List<int> tris, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); // welded verts -> averaged normals -> smooth shading
            mesh.RecalculateBounds();
            return mesh;
        }

        // Like Finish, but uses EXPLICIT per-vertex normals instead of RecalculateNormals. Required for
        // the grass blades (iter-8): a two-sided blade has front + back verts at the same positions with
        // up-biased normals — RecalculateNormals would average coincident-position contributions back
        // toward zero (the dark-shard bug). Setting normals directly keeps each side correctly lit.
        static Mesh FinishWithNormals(List<Vector3> verts, List<Vector3> normals, List<int> tris, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
