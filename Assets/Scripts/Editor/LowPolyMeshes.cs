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
        //
        // SWAY-MASK BAKE (tickets 86cabc73q trees — Erik §Trees): every vert carries vertex-color ALPHA = 0,
        // the "do NOT sway" mask the FarHorizon/LowPolyVertexColor canopy-sway term reads (alpha 0 = planted,
        // alpha 1 = canopy). The trunk is a STATIC stem (and the same mesh is reused for sticks/branches/legs,
        // all static), so alpha 0 is the correct mask for them all. RGB is 1 (white) so a vertex-color material
        // renders the trunk's _Tint unmodified, and _AOStrength is 0 on every trunk/stick material (URP/Lit or
        // flat) so the alpha-0 NEVER darkens anything — it is ONLY consumed as the sway mask when a material
        // raises _SwayAmp (no trunk material does). This makes the alpha mask the single source of truth for
        // "sways vs planted" regardless of which material renders the mesh.
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

            // SWAY-MASK alpha = 0 on every trunk vert (planted; see the doc comment above). RGB white so a
            // vertex-color material renders _Tint unmodified.
            var cols = new List<Color>(verts.Count);
            for (int i = 0; i < verts.Count; i++) cols.Add(new Color(1f, 1f, 1f, 0f));
            return Finish(verts, tris, cols, "LP_Trunk");
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

        // A FACETED STONE ROCK (86ca8m5zu SOAKFIX2 — the "doesn't read as a rock" redo). Both prior
        // procedural attempts FAILED the same way and the Sponsor rejected both: subdiv-0 FacetedSphere
        // = a bare octahedron = an angular dark SPIKE; subdiv-2 FacetedSphere = a smooth ball whose
        // RecalculateNormals weld AVERAGES every facet into a continuous gradient -> reads as a soft dark
        // MOUND, not stone. The board reference (inspiration/2026-06-12_21h10_44.png, bottom row) shows
        // what stone actually IS: CHUNKY ANGULAR polygonal chunks with DISTINCT FLAT planes, each facet
        // catching the key light at a DIFFERENT value (light top faces, dark side faces). That hard
        // per-facet value contrast is the entire "reads as rock" signal — and a smooth-welded sphere
        // destroys it by construction. (Blender-MCP sourcing of a low-poly rock asset was the dispatch's
        // PRIMARY route but the MCP was unreachable — no Blender app / no :9876 listener — so this is the
        // FALLBACK: a reworked ANGULAR FLAT-SHADED procedural mesh, NOT a smooth sphere, NOT a subdiv-0
        // spike.)
        //
        // Construction:
        //   (1) Start from a subdiv-1 octahedron (18 unique verts / 32 faces) — enough facets to read
        //       chunky (not an 8-face spike) but coarse enough to stay low-poly-readable.
        //   (2) ANISOTROPIC, IRREGULAR displacement per unique vert: different jitter per axis + a few
        //       verts pulled IN to carve flat planes/notches, so the silhouette is a lumpy angular ROCK,
        //       not a ball. Squashed a touch in Y but kept CHUNKY (taller than a flat mound; minYScale
        //       ~0.7) — a boulder sits, it doesn't pancake.
        //   (3) FLAT SHADING: every triangle gets its OWN 3 verts with the FACE normal (NOT welded, NOT
        //       RecalculateNormals-averaged) — so each facet is a distinct flat stone plane that catches
        //       light on its own. This is the load-bearing difference from FacetedSphere: flat per-face
        //       planes read as carved stone; smooth-averaged normals read as a soft mound.
        //   (4) Per-face VERTEX-COLOR value step: top-facing faces get a light value, downward/side faces
        //       a darker value, baked into vertex colour so a single vertex-colour material renders the
        //       stone's facet-to-facet value contrast (multiplied onto the warm-grey base in the caller).
        //
        //   radius   — rough half-extent of the rock
        //   jitter   — angular lumpiness (0.30-0.45 reads chunky-irregular; higher = more jagged)
        //   seed     — deterministic shape (the baked scene must be reproducible on rebase-regenerate)
        public static Mesh FacetedRock(float radius, float jitter, int seed)
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
            // ONE subdivision: 8 faces -> 32 faces, 6 -> 18 verts. Chunky, not a spike, not a smooth ball.
            for (int s = 0; s < 1; s++)
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

            // CHUNKY displacement (v2 verify-capture diagnosis: the first try was too aggressive — per-axis
            // stretch up to 1.40 + Y-flatten to 0.70 + deep 0.62x pull-ins produced thin BLADE/sliver shards
            // that read as torn paper, not stone). The board rocks (21h10_44) are roughly EQUIDIMENSIONAL
            // chunky blobs with MILD facet variation — so keep all three axes near 1.0 (gentle stretch, NO
            // Y-pancake) and a SMALL per-vert radial wobble (no deep notches that collapse a facet to a
            // sliver). The chunk stays a fat lump; the facets give it the carved-stone read, not the
            // silhouette extremes.
            float sx = 0.92f + (float)rnd.NextDouble() * 0.20f; // 0.92..1.12 — gentle stretch, stays chunky
            float sz = 0.92f + (float)rnd.NextDouble() * 0.20f;
            float sy = 0.85f + (float)rnd.NextDouble() * 0.18f; // 0.85..1.03 — squat but NOT pancaked
            var displaced = new Vector3[baseVerts.Count];
            for (int i = 0; i < baseVerts.Count; i++)
            {
                Vector3 n = baseVerts[i].normalized;
                // MILD radial wobble: each vert in/out by up to +/- jitter*0.5 — keeps the chunk fat, just
                // lumpy. No deep pull-in (that carved slivers); the lumpiness comes from many small offsets.
                float rj = 1f + ((float)rnd.NextDouble() - 0.5f) * jitter;
                Vector3 p = new Vector3(n.x * sx, n.y * sy, n.z * sz) * (radius * rj);
                // small absolute wobble so verts don't sit on a clean ellipsoid (breaks the smooth silhouette
                // into facets) — kept SMALL/isotropic so no facet collapses to a thin blade.
                p += new Vector3(((float)rnd.NextDouble() - 0.5f),
                                 ((float)rnd.NextDouble() - 0.5f),
                                 ((float)rnd.NextDouble() - 0.5f)) * (radius * jitter * 0.22f);
                displaced[i] = p;
            }

            // VERTEX-COLOR AO BAKE (ticket 86caamnra — Erik R&D §E / Rank 6). Our rocks are PROCEDURAL (not
            // Blender FBX), so the "Blender AO bake → vertex alpha" route is done CODE-side here: a cheap
            // GEOMETRIC occlusion proxy baked into each facet's vertex-color ALPHA. The proxy: a facet is more
            // occluded (darker AO, lower alpha) the LOWER it sits (near the ground-contact crevices the board's
            // rocks shade dark) and the more it faces DOWN/SIDEWAYS (a downward/concave facet sees less sky).
            // Exposed top facets keep alpha ~1 (unoccluded). The LowPolyVertexColor shader multiplies the lit
            // colour by lerp(1, alpha, _AOStrength), so the rock material (which sets _AOStrength ~0.5) gets the
            // contact-shadow depth while a default-0 material is byte-identical (AO is in ALPHA; the RGB per-
            // facet VALUE step is untouched — they are ADDITIVE reads). Pre-scan the displaced Y extents so AO
            // keys off the rock's own height range (scale-independent, deterministic from the same seed).
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < displaced.Length; i++)
            {
                if (displaced[i].y < minY) minY = displaced[i].y;
                if (displaced[i].y > maxY) maxY = displaced[i].y;
            }
            float yRange = Mathf.Max(maxY - minY, 1e-4f);

            // FLAT SHADING: emit every triangle with its OWN 3 verts + the face normal, and bake a per-face
            // value into vertex colour (light for up-facing facets, dark for side/down) — the facet-to-facet
            // value contrast that makes it read as carved stone.
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var cols = new List<Color>();
            for (int t = 0; t < baseTris.Count; t += 3)
            {
                Vector3 v0 = displaced[baseTris[t]];
                Vector3 v1 = displaced[baseTris[t + 1]];
                Vector3 v2 = displaced[baseTris[t + 2]];
                Vector3 fn = Vector3.Cross(v1 - v0, v2 - v0);
                if (fn.sqrMagnitude < 1e-10f) continue; // skip degenerate (a fully collapsed notch)
                fn.Normalize();
                // OUTWARD-CONSISTENCY (v2 verify-capture diagnosis — the "thin sliver" render): the meshes
                // are NOT thin (ROCKDIAG: chunky bounds, 0 thin faces) — they were BACKFACE-CULLED. After
                // displacement a face can wind CW-as-seen-from-outside, so its computed normal points INWARD;
                // the default URP Cull Back then culls it (and an inward normal shades it dark) -> only the
                // few correctly-wound facets survive -> the rock reads as scattered thin shards. Fix: force
                // every face normal to point AWAY from the rock centre (verts are centred on the origin), and
                // FLIP the winding to match so the visible (front) side is the outward side -> the whole chunk
                // renders, no culled holes. (Same backface-culling class as the −Z water grid, unity-conv §.)
                Vector3 faceCentre = (v0 + v1 + v2) / 3f;
                if (Vector3.Dot(fn, faceCentre) < 0f)
                {
                    fn = -fn;
                    var tmp = v1; v1 = v2; v2 = tmp; // flip winding so the outward normal is the front face
                }
                // Per-facet value: up-facing facets read light, side/down facets a touch darker. The big
                // facet-to-facet contrast comes from the FLAT-SHADING LIGHTING (each facet's own N·L against
                // the warm key), so the BAKED value only needs a GENTLE lift toward the tops — a high floor
                // (0.80) keeps side/down facets MID-grey, never the near-BLACK shards the first capture showed
                // (a side facet gets ~ambient-only light, so a low baked value × that = black). The shipped
                // LowPolyVertexColor shader multiplies this onto the warm-grey _Tint.
                float up = Mathf.Clamp01(fn.y * 0.5f + 0.5f);       // 0 (down) .. 1 (up)
                float val = Mathf.Lerp(0.80f, 1.0f, up);            // mid-grey sides .. light tops (never black)
                val += ((float)rnd.NextDouble() - 0.5f) * 0.05f;    // tiny per-face break so planes differ
                val = Mathf.Clamp(val, 0.74f, 1.05f);

                // AO ALPHA (ticket 86caamnra): bake a geometric occlusion proxy into the facet's vertex
                // ALPHA. Two cheap signals combine: (1) HEIGHT — facets low on the rock (near the ground
                // contact) are more occluded; facets at the top see open sky → alpha ~1. (2) DOWN-FACING —
                // a facet pointing down/sideways (a crevice underside) is more occluded than an up-facing
                // top. ao = 1 (no occlusion) .. _aoFloor (most occluded, at the low downward facets). A
                // FLOOR of 0.55 keeps even the most-occluded crevice readable (× the _Tint × value × light,
                // it darkens for depth, never crushes to black). Stored in alpha ONLY — RGB (the value step
                // above) is unchanged, so the default-_AOStrength=0 material is byte-identical.
                float faceCY = faceCentre.y;
                float heightT = Mathf.Clamp01((faceCY - minY) / yRange);     // 0 low (occluded) .. 1 top (open)
                float downT = Mathf.Clamp01(0.5f - fn.y * 0.5f);             // 0 up-facing .. 1 down-facing
                // occlusion grows toward the low + down facets; bias toward height (the dominant contact cue).
                float occ = (1f - heightT) * 0.7f + downT * 0.3f;            // 0 (open) .. 1 (most occluded)
                const float aoFloor = 0.55f;
                float ao = Mathf.Lerp(1f, aoFloor, Mathf.Clamp01(occ));
                Color fc = new Color(val, val, val, ao);

                verts.Add(v0); verts.Add(v1); verts.Add(v2);
                normals.Add(fn); normals.Add(fn); normals.Add(fn);
                cols.Add(fc); cols.Add(fc); cols.Add(fc);
            }
            // Every face owns its 3 verts (flat shading), so the triangle list is simply sequential.
            var tris = new List<int>(verts.Count);
            for (int i = 0; i < verts.Count; i++) tris.Add(i);

            var mesh = new Mesh { name = "LP_Rock" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);   // EXPLICIT flat face normals — do NOT RecalculateNormals (would re-smooth)
            mesh.SetColors(cols);       // per-facet value contrast (stone read)
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
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
                // ALPHA = 1 (the last arg) is the SWAY MASK (tickets 86cabc73q — Erik §Trees): the canopy is
                // the part that sways, so every canopy vert carries alpha 1 (vs the trunk's alpha 0). The
                // FarHorizon/LowPolyVertexColor canopy-sway term multiplies the lateral displacement by this
                // alpha, so the canopy moves full while the trunk stays planted. (alpha 1 is also the AO no-op,
                // and _AOStrength is 0 on the canopy material, so it only ever acts as the sway mask here.)
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

        // A low-poly BUSH BODY (ticket 86caa5zz3) — the SAME clustered-spheroid construction language as
        // BlobCanopy (so bushes read as ONE idiom with the world's trees, per the art-direction gate), but
        // LOW + WIDE: a squat dome of overlapping faceted green spheroids sitting on the ground, NOT a tall
        // crown on a trunk. The board nature sheets (inspiration/21h10_44 / 21h12_49) show bushes as small
        // rounded leafy clumps — this rides that read. Multi-value greens baked per-blob into vertex COLOR
        // (one shared vertex-color material renders the whole bush, no per-blob churn — same as the canopy).
        // Welded-per-blob -> smooth-shaded faceted lumps (solid volume, sidesteps the thin-foliage normal
        // trap, unity-conventions.md §Low-poly mesh patterns). Base at y=0 so it grounds like the stones.
        //
        //   radius   — overall bush radius (the squat cluster roughly fits a flattened sphere of this radius)
        //   blobs    — how many spheroids cluster (4-6 reads like the board's leafy clumps; min 3)
        //   bodyGreen / topGreen / shadowGreen — the 3-value green palette (same family as the tree canopy)
        //   seed     — deterministic cluster layout + per-blob jitter (reproducible baked scene)
        public static Mesh BushBlob(float radius, int blobs, Color bodyGreen, Color topGreen,
            Color shadowGreen, int seed)
        {
            blobs = Mathf.Max(3, blobs);
            var rnd = new System.Random(seed);
            var allVerts = new List<Vector3>();
            var allCols = new List<Color>();
            var allTris = new List<int>();

            // Lay the blobs LOW + WIDE: a broad squat dome (bushes hug the ground), the first blob centred
            // + low (the body), the rest ringed around it at a small up-bias so the silhouette is a rounded
            // leafy mound, not a rising crown. upBias capped low (vs BlobCanopy's tall crown) so it reads
            // as a bush, not a small tree.
            for (int b = 0; b < blobs; b++)
            {
                float t = b / (float)blobs;
                float ang = t * Mathf.PI * 2f + (float)rnd.NextDouble() * 1.2f;
                float ringR = (b == 0) ? 0f : radius * (0.38f + (float)rnd.NextDouble() * 0.40f);
                // LOW up-bias (squat dome): the crown rises only a little above the body.
                float upBias = (b == 0) ? radius * 0.18f
                                        : radius * (0.10f + (float)rnd.NextDouble() * 0.28f);
                Vector3 center = new Vector3(Mathf.Cos(ang) * ringR, upBias, Mathf.Sin(ang) * ringR);
                float blobR = radius * (0.50f + (float)rnd.NextDouble() * 0.28f);

                // Per-blob green: blend shadow->top by the blob's height in the cluster + a small jitter so
                // adjacent blobs differ (the multi-value clustering — style-guide §4).
                float heightK = Mathf.Clamp01((center.y + blobR) / (radius * 1.1f));
                Color lo = Color.Lerp(shadowGreen, bodyGreen, Mathf.Clamp01(heightK * 1.6f));
                Color blobCol = Color.Lerp(lo, topGreen, Mathf.Clamp01((heightK - 0.45f) * 1.8f));
                float vj = (float)(rnd.NextDouble() - 0.5) * 0.06f;
                blobCol = new Color(Mathf.Clamp01(blobCol.r + vj),
                                    Mathf.Clamp01(blobCol.g + vj),
                                    Mathf.Clamp01(blobCol.b + vj), 1f);

                AppendBlob(allVerts, allCols, allTris, center, blobR,
                           (b % 2 == 0) ? 1 : 0, jitter: 0.24f, color: blobCol, seed: rnd.Next());
            }

            var mesh = new Mesh { name = "LP_BushBlob" };
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

        // The BERRIES on a berry bush (ticket 86caa5zz3) — a SCATTER of SMALL, DENSE, MANY red faceted
        // spheres nestled over the bush dome, the visible "this bush has food" cue. A SEPARATE mesh from the
        // bush body so the berry bush authoring can show/hide JUST the berries on harvest/regrow (the bush
        // persists, only the berries toggle — AC4) by toggling the berries child's active state. Material-
        // honest warm RED (the berry colour reads as the fruit; no arbitrary tint — weapon/asset material-
        // honest memory). The berry value is baked into vertex COLOR (per-berry top-lit/shadow) so the shared
        // vertex-color material renders them; welded-per-berry -> smooth faceted spheres.
        //
        // === SOAK-FIX (#101, Sponsor: "they look like FLOWERS") ===
        // The prior cluster read as flowers, not berries: TOO FEW berries (~6-12) that were TOO LARGE
        // (berryR 0.16*bushRadius ≈ 0.15u — a 30cm "berry"), each built as TWO overlapping blobs so it bulged
        // even larger, spread over a WIDE thin ring. A few big rounded lumps on a dome = a flower head. Berries
        // read as berries when they are SMALL, DENSE, and MANY (a tight studding of little dots). FIX:
        //   - berryR ≈ 0.055*bushRadius (≈ 5cm — a real berry dot, ~3× smaller than before);
        //   - ONE blob per berry (a small sphere is already a clear dot; the 2-blob top/dark split only bulked
        //     it up — the 2-value read is kept by alternating per-berry top-lit / shadow COLOUR instead);
        //   - MANY berries (default ~24, callers pass 20-30) packed into a TIGHT spread (smaller ring + height
        //     band) so the dome reads STUDDED with fruit, not dotted with flowers.
        //
        //   bushRadius — the bush body radius the berries nestle over (they stud its upper dome)
        //   count      — how many berries (20-30 reads as a dense studding of fruit; min 3)
        //   berry      — the berry colour (a darker shade is derived for shadowed berries / undersides)
        //   seed       — deterministic placement (reproducible baked scene)
        public static Mesh BerryCluster(float bushRadius, int count, Color berry, int seed)
        {
            count = Mathf.Max(3, count);
            var rnd = new System.Random(seed);
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            // 2-value berry read: a top-lit highlight + a shadowed dark, alternated per berry so the studding
            // reads with depth rather than as one flat red mass (replaces the old per-berry 2-blob bulge).
            Color berryTop  = Color.Lerp(berry, Color.white, 0.14f);
            Color berryDark = new Color(berry.r * 0.55f, berry.g * 0.45f, berry.b * 0.50f, 1f);
            float berryR = bushRadius * 0.055f; // SMALL dot (~5cm on a ~0.95u bush) — a berry, not a flower head

            for (int i = 0; i < count; i++)
            {
                // STUD the upper dome densely: a tight radial + height band (was a wide thin ring), with a
                // golden-angle phase so MANY berries pack evenly without clumping into a few visible lumps.
                float ang = i * 2.39996323f + (float)rnd.NextDouble() * 0.5f; // golden angle, light jitter
                float ringR = bushRadius * (0.20f + (float)rnd.NextDouble() * 0.62f); // fills the dome, not a rim
                float up = bushRadius * (0.50f + (float)rnd.NextDouble() * 0.42f);
                Vector3 center = new Vector3(Mathf.Cos(ang) * ringR, up, Mathf.Sin(ang) * ringR);
                // Alternate top-lit / shadowed berries so the dense studding reads with 2-value depth. A small
                // per-berry radius wobble keeps the dots from looking machine-uniform.
                Color c = (i % 3 == 0) ? berryDark : berryTop;
                float r = berryR * (0.85f + (float)rnd.NextDouble() * 0.35f);
                AppendBlob(verts, cols, tris, center, r, 0, jitter: 0.12f, color: c, seed: rnd.Next());
            }

            var mesh = new Mesh { name = "LP_BerryCluster" };
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A FACETED CLOUD BLOB (world-look polish, ticket 86ca8t9pq — Uma world-look brief §1). The
        // SAME clustered-spheroid construction language as BlobCanopy (so clouds and tree canopies are
        // ONE idiom), but with two deliberate differences that make it read as a chunky cyan CLOUD, not
        // a green tree:
        //   (1) FLAT (hard) NORMALS — Uma §1 "hard normals (smoothing angle 0°) so the facets read".
        //       BlobCanopy uses RecalculateNormals (welded -> smooth) for soft foliage; a cloud must
        //       read as DISTINCT chunky facets (the board cloud sheet 21h10_44 shows hard facet planes),
        //       so every triangle gets its OWN 3 verts with the FACE normal (flat-shaded), exactly like
        //       FacetedRock. This is what gives the cloud its crisp toy-diorama facet read at orbit
        //       distance (Uma: "if they read as smooth blobs, smoothing isn't hard-0°").
        //   (2) WIDER, FLATTER cluster — clouds are broad puffy masses, not a rising crown, so the blobs
        //       lay out wider + lower than a canopy (a flattened spheroid cluster) per the asset sheet.
        //
        // The 3-value cyan palette (body / top-lit / shadow) is baked per-blob into vertex COLOR exactly
        // like the canopy greens, so a single shared vertex-color material renders the whole cloud (no
        // per-blob material churn): lower blobs read as the shadow facet, upper blobs catch the top-light.
        // SOLID volumes (closed spheroids) so the thin-foliage near-black-shard normal trap is sidestepped
        // (unity-conventions.md §Low-poly mesh patterns) and OUTWARD winding is enforced (so the flat
        // faces are never backface-culled — the same Cull-Back class as the −Z water grid / FacetedRock).
        //
        //   radius      — overall cloud radius (the cluster roughly fits a flattened sphere of this radius)
        //   blobs       — how many spheroids cluster (3-6 reads like the board sheet; min 3)
        //   bodyCyan / topCyan / shadowCyan — the 3-value cyan palette (Uma §1 anchor swatches)
        //   seed        — deterministic cluster layout + per-blob jitter (reproducible baked scene)
        // CLD-1 flat-base floor as a fraction of the cloud radius (the base plane sits at -0.25×radius in
        // cloud-local space — just below the y=0 blob-centre band, so only the undersides flatten and the
        // puffy tops read unchanged). Dispatch spec: max(y, -0.25×radius).
        const float CloudBaseFloorFraction = 0.25f;

        public static Mesh CloudBlob(float radius, int blobs, Color bodyCyan, Color topCyan,
            Color shadowCyan, int seed)
        {
            blobs = Mathf.Max(3, blobs);
            var rnd = new System.Random(seed);
            // Lay the blob centres + radii first (a wide, flattish cluster), then flat-shade the union.
            var centres = new System.Collections.Generic.List<Vector3>();
            var radii = new System.Collections.Generic.List<float>();
            var blobCols = new System.Collections.Generic.List<Color>();
            for (int b = 0; b < blobs; b++)
            {
                float t = b / (float)blobs;
                float ang = t * Mathf.PI * 2f + (float)rnd.NextDouble() * 1.4f;
                // WIDE + FLAT: ring spread is generous (broad puff), vertical bias is small + squashed
                // (a cloud is wider than it is tall — the board puffs are flattened masses, not towers).
                float ringR = (b == 0) ? 0f : radius * (0.35f + (float)rnd.NextDouble() * 0.55f);
                float upBias = (b == 0) ? 0f
                                        : radius * ((float)rnd.NextDouble() - 0.35f) * 0.45f;
                var centre = new Vector3(Mathf.Cos(ang) * ringR, upBias, Mathf.Sin(ang) * ringR);
                float blobR = radius * (0.50f + (float)rnd.NextDouble() * 0.32f);

                // Per-blob cyan: blend shadow->top by the blob's height in the cluster + a small value
                // jitter so adjacent blobs differ (the multi-value read the canopy uses, in cyan).
                float heightK = Mathf.Clamp01((centre.y + blobR) / (radius * 1.1f));
                Color lo = Color.Lerp(shadowCyan, bodyCyan, Mathf.Clamp01(heightK * 1.7f));
                Color blobCol = Color.Lerp(lo, topCyan, Mathf.Clamp01((heightK - 0.5f) * 1.9f));
                float vj = (float)(rnd.NextDouble() - 0.5) * 0.04f;
                blobCol = new Color(Mathf.Clamp01(blobCol.r + vj), Mathf.Clamp01(blobCol.g + vj),
                                    Mathf.Clamp01(blobCol.b + vj), 1f);
                centres.Add(centre); radii.Add(blobR); blobCols.Add(blobCol);
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var cols = new List<Color>();
            // FLAT CLOUD BASE (CLD-1, ticket 86cahhfkc — plan §5 Tier-1 item 5). The board clouds
            // (21h10_44 / 21h16_13) are flat-bottomed CUMULUS toys, not spheroid potatoes: a rounded puffy
            // top sitting on a near-flat base. Clamp every blob vertex's cloud-local Y up to a single floor
            // plane at floorY so all blobs share ONE flat bottom (a per-blob floor would step the base). The
            // floor is a fraction of the cloud radius (below the y=0 blob-centre band, so the puffy tops are
            // untouched and only the undersides flatten). Same vert budget, clouds stay non-casters (perf
            // guardrail). Applied AFTER the yScale flatten, INSIDE the flat-shade so the clamped base faces
            // get their own recomputed near-horizontal face normals for free.
            float floorY = -CloudBaseFloorFraction * radius;
            for (int b = 0; b < centres.Count; b++)
                AppendFlatBlob(verts, normals, cols, centres[b], radii[b], subdiv: 1,
                               jitter: 0.20f, color: blobCols[b], yScale: 0.78f, floorY: floorY, seed: rnd.Next());

            var tris = new List<int>(verts.Count);
            for (int i = 0; i < verts.Count; i++) tris.Add(i); // flat-shaded: every face owns its verts

            var mesh = new Mesh { name = "LP_CloudBlob" };
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals); // EXPLICIT flat face normals — hard 0° smoothing (Uma §1)
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Append one FLAT-SHADED (hard-normal) spheroid blob into the shared vert/normal/color lists.
        // Like AppendBlob but: (a) every triangle gets its OWN 3 verts + the FACE normal (hard 0°
        // smoothing — the crisp-facet cloud read), (b) face normals are forced OUTWARD + winding flipped
        // to match (no backface-cull — the Cull-Back class), (c) a yScale flattens the blob (clouds are
        // wider than tall). Used by CloudBlob; kept distinct from AppendBlob (smooth canopy) so the two
        // idioms don't entangle.
        static void AppendFlatBlob(List<Vector3> verts, List<Vector3> normals, List<Color> cols,
            Vector3 center, float radius, int subdiv, float jitter, Color color, float yScale, float floorY, int seed)
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
            // Displace each base vert radially (lumpy) + flatten in Y, in blob-local space. Then CLAMP the
            // final blob-local Y up to floorY (CLD-1) so the cloud's underside reads as a flat cumulus base
            // instead of a spheroid potato bottom — every blob shares the one cloud floor plane. Verts above
            // the floor are untouched (the puffy top is unchanged); only the undersides snap up onto the base.
            var displaced = new Vector3[baseVerts.Count];
            for (int i = 0; i < baseVerts.Count; i++)
            {
                Vector3 n = baseVerts[i].normalized;
                float r = radius * (1f - jitter * 0.5f + (float)rnd.NextDouble() * jitter);
                Vector3 p = center + new Vector3(n.x * r, n.y * r * yScale, n.z * r);
                p.y = Mathf.Max(p.y, floorY);
                displaced[i] = p;
            }

            // Flat-shade: emit each face with its own 3 verts + outward face normal (winding flipped to
            // match the outward normal so Cull Back keeps the front faces).
            for (int t = 0; t < baseTris.Count; t += 3)
            {
                Vector3 v0 = displaced[baseTris[t]];
                Vector3 v1 = displaced[baseTris[t + 1]];
                Vector3 v2 = displaced[baseTris[t + 2]];
                Vector3 fn = Vector3.Cross(v1 - v0, v2 - v0);
                if (fn.sqrMagnitude < 1e-10f) continue;
                fn.Normalize();
                Vector3 outward = ((v0 + v1 + v2) / 3f) - center; // from blob centre to face
                if (Vector3.Dot(fn, outward) < 0f)
                {
                    fn = -fn;
                    var tmp = v1; v1 = v2; v2 = tmp; // flip winding so the outward normal is the front face
                }
                verts.Add(v0); verts.Add(v1); verts.Add(v2);
                normals.Add(fn); normals.Add(fn); normals.Add(fn);
                cols.Add(color); cols.Add(color); cols.Add(color);
            }
        }

        // A FACETED MOUNTAIN SILHOUETTE (world-look polish, ticket 86ca8t9pq — Uma world-look brief §2).
        // The NEAR-VISTA distant landmass: a big confident faceted grey-to-snow peak in the SAME hard-
        // faceted language as the foreground rocks/terrain, just scaled up + atmosphere-faded (Uma §2:
        // "the SAME hard-faceted language as foreground terrain/rocks, just scaled up"). Built as a
        // chunky polygonal cone-ish mass:
        //   - a ring of base verts (irregular radius so the silhouette is an asymmetric ridge, not a
        //     symmetric tent) at y=0, rising to a slightly off-centre apex (a believable peak, not a
        //     perfect cone);
        //   - FLAT-shaded per-face (hard normals) so each mountain plane catches the key light at its
        //     own value — the carved-rock read scaled up (Uma: "the SILHOUETTE must stay faceted/chunky");
        //   - a SNOW CAP baked into vertex colour: faces whose apex-region verts sit above the snowline
        //     get the pale snow value, lower faces the warm-grey body — the 21h16_13 grey-to-snow read.
        // Outward winding enforced (no backface-cull). The ATMOSPHERIC FADE (lighter/desaturated/warmer
        // as it recedes) is applied by the CALLER via the per-range tint + the distance fog (fog tint ==
        // horizon stop, Uma §3) — this mesh just carries the silhouette + the snow-cap value contrast.
        //
        //   baseRadius — half-extent of the mountain footprint
        //   height     — peak height (a confident vertical mass)
        //   sides      — base-ring segments (6-9 reads chunky-faceted, not smooth)
        //   snowline   — 0..1 fraction of the height above which faces read as snow cap
        //   bodyGrey / snowWhite — the grey-to-snow palette (per-range, atmosphere-faded by the caller)
        //   seed       — deterministic ridge shape (reproducible baked scene)
        public static Mesh FacetedMountain(float baseRadius, float height, int sides, float snowline,
            Color bodyGrey, Color snowWhite, int seed)
        {
            sides = Mathf.Max(5, sides);
            var rnd = new System.Random(seed);

            // ---- MOUNTAIN-DETAIL SOAK-FIX (86ca8t9pq S3, Sponsor soak of fa9f1b1: "I need mountains to be
            // more detailed"). The old mesh was a SMOOTH CONE: ONE base ring -> ONE snow ring -> apex (2
            // vertical bands), reading as a smooth tan/grey faceted pyramid — exactly the Sponsor's complaint.
            // The board mountains (21h16_13 / 21h12_49) have GEOMETRIC RICHNESS: jagged ridge lines down the
            // flanks, stacked rockface sub-relief (several value bands, not one slope), a banded snowline, and
            // an asymmetric silhouette with a secondary shoulder/sub-peak. This is MESH geometry (the F9/F10
            // colour dial can't add it). FIX — rebuild as a MULTI-RING ridged peak:
            //   (1) STACK several rings base->apex (RingCount, not just base+snow) so the flank has stepped
            //       sub-relief instead of one clean slope;
            //   (2) per-ring RIDGE displacement — each ring's per-vert radius zig-zags (a stable per-COLUMN
            //       ridge phase) so vertical ridge lines run down the flanks (carved spurs, not a smooth cone);
            //   (3) MULTI-BAND value ramp — rock bands darken/vary with height below the snowline, then a
            //       bright snow cap above it (rockface stepping, not one flat grey);
            //   (4) a SECONDARY SHOULDER peak offset from the apex so the silhouette is asymmetric (a range,
            //       not a single symmetric cone). All flat-shaded per-face (verts==tris*3), outward-wound. ----
            const int RingCount = 5;                 // base + 3 rock rings + snow ring (was effectively 2 bands)
            float sn = Mathf.Clamp(snowline, 0.2f, 0.85f);

            // Apex: off-centre + a touch of height jitter so ranges differ.
            var apex = new Vector3(
                (float)(rnd.NextDouble() - 0.5) * baseRadius * 0.30f,
                height * (0.92f + (float)rnd.NextDouble() * 0.16f),
                (float)(rnd.NextDouble() - 0.5) * baseRadius * 0.30f);

            // Per-COLUMN ridge phase + amplitude (STABLE across all rings of a column) so the radius wobble
            // lines up vertically into continuous RIDGE LINES / spurs down the flank — not random noise.
            var ridgePhase = new float[sides];
            var ridgeAmp = new float[sides];
            var colYWobble = new float[sides];
            for (int i = 0; i < sides; i++)
            {
                ridgePhase[i] = (float)rnd.NextDouble();                         // 0..1 — alternating in/out spur
                ridgeAmp[i] = 0.10f + (float)rnd.NextDouble() * 0.16f;           // how pronounced this column's ridge is
                colYWobble[i] = ((float)rnd.NextDouble() - 0.5f) * height * 0.05f; // lumpy ridgeline height
            }

            // Build the stacked rings. Ring 0 = base footprint; ring RingCount-1 contracts to near the apex.
            // Each ring's verts ride toward the apex by its height fraction, with the per-column ridge
            // displacement applied to the radius so the spurs carry up the mountain.
            var rings = new Vector3[RingCount][];
            for (int r = 0; r < RingCount; r++)
            {
                float hf = r / (float)(RingCount - 1);              // 0 base .. 1 apex
                // Non-linear height stack: rings cluster a touch more toward the top so the upper peak reads
                // craggy (more bands where the snow cap + ridges matter).
                float climb = Mathf.Pow(hf, 0.85f);
                rings[r] = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    // Base radius for this column (irregular footprint), contracting toward the apex with height.
                    float baseColR = baseRadius * (0.74f + (float)rnd.NextDouble() * 0.10f + ridgePhase[i] * 0.40f);
                    float contract = Mathf.Lerp(1f, 0.06f, climb);  // ring shrinks to a small top ring near apex
                    // RIDGE displacement: alternate columns push OUT (spur) / pull IN (gully), scaled by the
                    // column's amp + tapering toward the apex so spurs read strongest on the lower flanks.
                    float ridge = Mathf.Sin(ridgePhase[i] * Mathf.PI * 2f) * ridgeAmp[i] * (1f - climb * 0.7f);
                    float rad = baseColR * contract * (1f + ridge);
                    Vector3 ringXZ = new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
                    // Lerp toward the apex XZ as we climb so the rings stack into a peak, not a cylinder.
                    Vector3 xz = Vector3.Lerp(ringXZ, new Vector3(apex.x, 0f, apex.z), climb * 0.85f);
                    float y = Mathf.Lerp(0f, apex.y, climb) + colYWobble[i] * (1f - climb);
                    rings[r][i] = new Vector3(xz.x, y, xz.z);
                }
            }

            // SECONDARY SHOULDER peak: a lower offset sub-apex so the silhouette is a RANGE, not a lone cone.
            float shoulderH = apex.y * (0.55f + (float)rnd.NextDouble() * 0.18f);
            float shAng = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float shDist = baseRadius * (0.45f + (float)rnd.NextDouble() * 0.25f);
            var shoulder = new Vector3(Mathf.Cos(shAng) * shDist, shoulderH, Mathf.Sin(shAng) * shDist);

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var cols = new List<Color>();
            var center = new Vector3(0f, height * 0.4f, 0f); // rough mass centre for outward test

            // Multi-band rock/snow colour by height fraction: below the snowline a STACK of rock values
            // (stepped rockface), above it the bright snow cap. The per-face jitter adds the chunky facet step.
            Color ColourAt(float heightFrac, System.Random faceRnd)
            {
                Color band;
                if (heightFrac >= sn)
                    band = snowWhite;                                       // snow cap
                else
                {
                    // Stepped rock bands: darker low rock -> lighter high rock just under the snow (3 steps).
                    float rt = Mathf.InverseLerp(0f, sn, heightFrac);       // 0 foot .. 1 snowline
                    float step = Mathf.Floor(rt * 3f) / 3f;                 // quantize to 3 rock bands
                    Color rockLow = new Color(bodyGrey.r * 0.82f, bodyGrey.g * 0.82f, bodyGrey.b * 0.82f);
                    Color rockHi  = new Color(Mathf.Min(1f, bodyGrey.r * 1.10f),
                                              Mathf.Min(1f, bodyGrey.g * 1.10f),
                                              Mathf.Min(1f, bodyGrey.b * 1.10f));
                    band = Color.Lerp(rockLow, rockHi, step);
                }
                float vj = ((float)faceRnd.NextDouble() - 0.5f) * 0.16f;    // chunky per-facet value step
                return new Color(Mathf.Clamp01(band.r + vj), Mathf.Clamp01(band.g + vj),
                                 Mathf.Clamp01(band.b + vj), 1f);
            }

            void EmitFace(Vector3 v0, Vector3 v1, Vector3 v2)
            {
                Vector3 fn = Vector3.Cross(v1 - v0, v2 - v0);
                if (fn.sqrMagnitude < 1e-10f) return;
                fn.Normalize();
                Vector3 outward = ((v0 + v1 + v2) / 3f) - center;
                if (Vector3.Dot(fn, outward) < 0f) { fn = -fn; var tmp = v1; v1 = v2; v2 = tmp; }
                // Colour by the face centroid's height fraction (so the rock/snow band follows real height).
                float hf = Mathf.InverseLerp(0f, apex.y, (v0.y + v1.y + v2.y) / 3f);
                Color fc = ColourAt(hf, rnd);
                verts.Add(v0); verts.Add(v1); verts.Add(v2);
                normals.Add(fn); normals.Add(fn); normals.Add(fn);
                cols.Add(fc); cols.Add(fc); cols.Add(fc);
            }

            // FLANK: stack the rings (two triangles per segment per ring gap) — the stepped sub-relief flank.
            for (int r = 0; r < RingCount - 1; r++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int ni = (i + 1) % sides;
                    Vector3 a0 = rings[r][i], a1 = rings[r][ni], b0 = rings[r + 1][i], b1 = rings[r + 1][ni];
                    EmitFace(a0, a1, b1);
                    EmitFace(a0, b1, b0);
                }
            }
            // CAP: top ring -> apex (one triangle per segment, the snow-capped summit).
            var topRing = rings[RingCount - 1];
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                EmitFace(topRing[i], topRing[ni], apex);
            }
            // SECONDARY SHOULDER: tie a few mid-ring columns up to the shoulder sub-apex so a second peak
            // breaks the silhouette (a small fan of faces from a mid ring to the shoulder point).
            int midRing = RingCount / 2;
            int shCol = Mathf.RoundToInt(shAng / (Mathf.PI * 2f) * sides) % sides;
            for (int k = -1; k <= 1; k++)
            {
                int i = ((shCol + k) % sides + sides) % sides;
                int ni = (i + 1) % sides;
                EmitFace(rings[midRing][i], rings[midRing][ni], shoulder);
            }

            var tris = new List<int>(verts.Count);
            for (int i = 0; i < verts.Count; i++) tris.Add(i);

            var mesh = new Mesh { name = "LP_Mountain" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);  // explicit flat face normals (hard-faceted silhouette)
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // A LANDMASS BASE / ISLAND SHELF the mountain peaks stand on (Drew "floating translucent shards"
        // grounding fix, ticket 86ca8t9pq). The shipped mountains read as floating because each peak's
        // base sat on a thin +2-6u shelf over open sea — with the sea fogged out beneath, the peak looked
        // like it hovered. This builds a broad, low faceted island: a top rim ring (slightly domed so the
        // peaks have a believable foot) and faceted SIDES dropping to a sunk bottom ring well below the sea
        // surface, so the visible coast IS the waterline and there is no gap under the peaks.
        //
        //   radius   — island footprint half-extent (must cover the peak spread)
        //   depth    — vertical extent from the sunk bottom up to the shelf top (top - seaSink)
        //   sides    — rim segments (9-11 reads chunky-faceted)
        //   bodyGrey — the island body colour (== the cluster's mountain body, so it recedes in lockstep)
        //   seed     — deterministic irregular rim (reproducible baked scene)
        //
        // FLAT-shaded per-face (explicit hard normals) with OUTWARD winding enforced (same EmitFace idiom
        // as FacetedMountain) so the island is never backface-culled — the −Z-grid / cull-back class guard.
        public static Mesh FacetedLandmass(float radius, float depth, int sides,
            Color bodyGrey, Color capGreen, int seed)
        {
            sides = Mathf.Max(7, sides);
            var rnd = new System.Random(seed);

            // Top rim: irregular radius, slight outward dome (the shelf the peaks foot on). y measured from
            // the mesh local origin (which the caller places at seaSink), so top is at `depth`.
            var topRing = new Vector3[sides];
            var botRing = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                float rTop = radius * (0.82f + (float)rnd.NextDouble() * 0.36f);  // irregular coast
                float rBot = rTop * (1.05f + (float)rnd.NextDouble() * 0.15f);    // splayed foot (wider below)
                float yTop = depth * (0.92f + (float)rnd.NextDouble() * 0.08f);   // near-flat domed top
                topRing[i] = new Vector3(Mathf.Cos(a) * rTop, yTop, Mathf.Sin(a) * rTop);
                botRing[i] = new Vector3(Mathf.Cos(a) * rBot, 0f, Mathf.Sin(a) * rBot);
            }
            var apexTop = new Vector3(0f, depth * 1.0f, 0f); // shallow dome centre

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var cols = new List<Color>();
            var center = new Vector3(0f, depth * 0.45f, 0f); // mass centre for the outward test

            void EmitFace(Vector3 v0, Vector3 v1, Vector3 v2, Color baseCol)
            {
                Vector3 fn = Vector3.Cross(v1 - v0, v2 - v0);
                if (fn.sqrMagnitude < 1e-10f) return;
                fn.Normalize();
                Vector3 outward = ((v0 + v1 + v2) / 3f) - center;
                if (Vector3.Dot(fn, outward) < 0f) { fn = -fn; var t = v1; v1 = v2; v2 = t; }
                float vj = ((float)rnd.NextDouble() - 0.5f) * 0.035f;
                Color fc = new Color(Mathf.Clamp01(baseCol.r + vj), Mathf.Clamp01(baseCol.g + vj),
                                     Mathf.Clamp01(baseCol.b + vj), 1f);
                verts.Add(v0); verts.Add(v1); verts.Add(v2);
                normals.Add(fn); normals.Add(fn); normals.Add(fn);
                cols.Add(fc); cols.Add(fc); cols.Add(fc);
            }

            // SIDE band: bottom ring -> top ring (the faceted island flanks).
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                EmitFace(botRing[i], botRing[ni], topRing[ni], bodyGrey);
                EmitFace(botRing[i], topRing[ni], topRing[i], bodyGrey);
            }
            // TOP dome: top ring -> apex (so the shelf is gently domed, peaks foot believably).
            // VIS-1 (ticket 86cahhfkc — plan §5 Tier-1 item 6): tint the top-dome faces a muted canopy GREEN
            // (a forested shelf cap) instead of bare grey, so the distant isles read as green-topped land,
            // not a bare "asteroid" rock. Only the SHELF TOP greens — the faceted FLANKS stay grey rock
            // (bodyGrey above). The caller passes a green already lerped toward the cluster tint, and the
            // per-cluster atmospheric _Tint multiplies on top, so the cap recedes in LOCKSTEP with its
            // flanks + peaks (no seam drift — same fade path as the rest of the mass).
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                EmitFace(topRing[i], topRing[ni], apexTop, capGreen);
            }

            var tris = new List<int>(verts.Count);
            for (int i = 0; i < verts.Count; i++) tris.Add(i);

            var mesh = new Mesh { name = "LP_Landmass" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
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

        // Like Finish, but also carries a per-vertex COLOR stream (used by TaperedCylinder to bake the
        // sway-mask alpha — tickets 86cabc73q). Normals are still averaged (welded smooth shading); only the
        // colour stream is added, so the trunk vertex count + normals are byte-identical to the no-colour path.
        static Mesh Finish(List<Vector3> verts, List<int> tris, List<Color> cols, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
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
