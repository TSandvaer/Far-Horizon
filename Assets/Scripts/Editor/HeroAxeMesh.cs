using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Procedural mesh for the M-U2 hero axe (ticket 86ca8ce6y) — the FIRST style-wave anchor that
    /// establishes the board's tool language (style-guide-v2.md §3 + inspiration/2026-06-12_21h08_08.png).
    /// Built to read like the reference: a faceted barn-red wedge HEAD with a distinct near-white
    /// edge-BEVEL plane along the cutting edge (the single identity-defining detail of the prop family),
    /// a small horn/poll at the top, and a chunky, gently-bent, faceted brown HAFT.
    ///
    /// === Why a THREE-SUBMESH mesh (not three separate meshes) ===
    /// The guide's tool language is "one crisp edge-highlight plane per hero edge" — the bevel is GEOMETRY
    /// catching light, a chamfer facet, NOT a painted texture line (style-guide-v2.md §1.3, §3.2). To make
    /// the head / bevel / haft each carry their own flat anchor color while staying ONE prop GameObject,
    /// the geometry is built as a single Mesh with THREE submeshes:
    ///   submesh 0 = HEAD   (barn red   #A33B30)
    ///   submesh 1 = BEVEL  (pale steel #E4E2DC) — the signature near-white cutting-edge plane
    ///   submesh 2 = HAFT   (warm brown #7A5230)
    /// The authoring side assigns a 3-material array in matching order (see MovementCameraScene.BuildHeroAxe),
    /// which serializes INLINE into Boot.unity (no .mat churn) exactly like the LowPolyZoneGen scatter props.
    ///
    /// === Shading: HARD-FACETED, not smooth ===
    /// The head + bevel read as CRISP planar facets in the reference — so this mesh uses per-face (flat)
    /// normals via distinct (non-welded) verts per face, NOT the welded+RecalculateNormals smooth idiom
    /// LowPolyMeshes uses for organic trunks/canopies. style-guide-v2.md §1.1 places tools in the
    /// "smooth-shaded over coarse facets" family, but the bevel's whole job is to read as a SEPARATE plane
    /// catching light — averaging its normal into the red cheek would wash the highlight out. Flat normals
    /// keep the bevel a distinct bright plane (the identity detail) and keep the wedge facets legible.
    ///
    /// Anchor colors live on the AUTHORING side (the guide is the spec there); this builder is geometry-only
    /// and color-agnostic, so the mesh is reusable and the QA-pinned anchors have a single source of truth.
    /// </summary>
    public static class HeroAxeMesh
    {
        // Submesh indices — the authoring material array must match this order.
        public const int SUBMESH_HEAD = 0;
        public const int SUBMESH_BEVEL = 1;
        public const int SUBMESH_HAFT = 2;
        public const int SUBMESH_COUNT = 3;

        // Proportions (local units; the axe stands ~as tall as the haft is long — chunky, a little
        // oversized, per style-guide-v2.md §3 "toy proportion"). Origin at the haft BUTT (y=0), head up top.
        const float HaftLen = 1.30f;     // butt (y=0) to where the head sits near the top
        const float HaftHalf = 0.055f;   // haft half-thickness (chunky, not a thin dowel)
        const float HaftBend = 0.10f;    // gentle hand-made forward bend at the head end (+X)
        const float HeadY = 1.06f;       // head center height along the haft
        const float HeadDepth = 0.085f;  // head half-thickness (Z) — the cheeks
        const float BevelDepth = 0.020f; // how far the bevel plane stands proud of the cheek in Z

        /// <summary>
        /// Build the hero axe as a single 3-submesh mesh (HEAD / BEVEL / HAFT), origin at the haft butt,
        /// blade facing -X (the cutting edge), the small horn/poll at +X. Hard-faceted (flat normals).
        /// </summary>
        public static Mesh Build()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var headTris = new List<int>();
            var bevelTris = new List<int>();
            var haftTris = new List<int>();

            BuildHaft(verts, normals, haftTris);
            BuildHead(verts, normals, headTris, bevelTris);

            var mesh = new Mesh { name = "HeroAxe" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = SUBMESH_COUNT;
            mesh.SetTriangles(headTris, SUBMESH_HEAD);
            mesh.SetTriangles(bevelTris, SUBMESH_BEVEL);
            mesh.SetTriangles(haftTris, SUBMESH_HAFT);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- HAFT: a chunky 4-sided tapered prism with a gentle forward bend near the head ----
        // Built as stacked rings; the top rings drift +X so the haft bends toward the blade like a
        // hand-carved handle (style-guide-v2.md §3.3 "slightly-bent, not a straight machined dowel").
        static void BuildHaft(List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            const int rings = 5;
            var ringVerts = new Vector3[rings][];
            for (int r = 0; r < rings; r++)
            {
                float t = r / (float)(rings - 1);              // 0 butt .. 1 head end
                float y = t * HaftLen;
                // taper: a touch wider at the butt, slightly narrower up top
                float half = Mathf.Lerp(HaftHalf * 1.15f, HaftHalf * 0.92f, t);
                // gentle bend: ease the top toward +X (toward the head's back)
                float bend = HaftBend * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t));
                ringVerts[r] = new[]
                {
                    new Vector3(bend - half, y, -half),
                    new Vector3(bend + half, y, -half),
                    new Vector3(bend + half, y,  half),
                    new Vector3(bend - half, y,  half),
                };
            }

            // side walls (4 quads per ring gap), flat normals per quad
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < 4; s++)
                {
                    int ns = (s + 1) % 4;
                    AddQuad(verts, normals, tris,
                        ringVerts[r][s], ringVerts[r][ns], ringVerts[r + 1][ns], ringVerts[r + 1][s]);
                }

            // butt cap (bottom) + top cap (so the prism is closed)
            AddQuad(verts, normals, tris,
                ringVerts[0][0], ringVerts[0][3], ringVerts[0][2], ringVerts[0][1]); // facing -Y
            int top = rings - 1;
            AddQuad(verts, normals, tris,
                ringVerts[top][0], ringVerts[top][1], ringVerts[top][2], ringVerts[top][3]); // facing +Y
        }

        // ---- HEAD: a faceted barn-red wedge with a poll/horn at +X, plus the near-white BEVEL plane ----
        // The head straddles the top of the haft (centered at HeadY, drifted +X with the haft bend). The
        // cutting edge faces -X; the bevel is a thin chamfer running down the -X edge on BOTH cheeks.
        static void BuildHead(List<Vector3> verts, List<Vector3> normals, List<int> headTris, List<int> bevelTris)
        {
            float bend = HaftBend; // head sits on the bent top of the haft
            float cx = bend;

            // Silhouette (XY) read like inspiration/2026-06-12_21h08_08.png: a TALL flat wedge whose
            // cutting edge bulges out to -X (the bit, a long near-vertical curved edge), a clear hooked
            // HORN at the top-back (+X, pointing up), and the haft socket at the lower-back. Mild
            // hand-made asymmetry (carved, not CNC'd). The blade flares both up AND down past the haft so
            // the bit reads as a broad cutting arc, not a narrow sliver.
            float bladeTopY  = HeadY + 0.40f;   // the bit's upper corner (tall blade)
            float bladeBotY  = HeadY - 0.42f;   // the bit's lower corner (sweeps down)
            float edgeTopX   = cx - 0.40f;      // cutting edge, upper
            float edgeMidX   = cx - 0.52f;      // cutting edge bulges furthest out here (the belly of the bit)
            float edgeBotX   = cx - 0.34f;      // cutting edge, lower
            float pollLowX   = cx + 0.26f;      // lower back of the head (above the socket)
            float hornBaseX  = cx + 0.30f;
            float hornTipX   = cx + 0.46f;      // the hooked horn juts up-and-back
            float hornTipY   = HeadY + 0.50f;
            float topBackX   = cx + 0.06f;      // the top edge between blade-top and horn

            // Outer silhouette ring (closed polygon, CCW viewed from +Z front cheek). Walk the cutting
            // edge top->belly->bottom (-X), across the underside to the socket, up the back to the horn,
            // then across the top back to the blade top.
            var sil = new List<Vector2>
            {
                new Vector2(edgeTopX,  bladeTopY),         // 0 cutting edge, upper corner
                new Vector2(edgeMidX,  HeadY + 0.02f),     // 1 cutting edge, belly (furthest -X)
                new Vector2(edgeBotX,  bladeBotY),         // 2 cutting edge, lower corner (the bit tip)
                new Vector2(cx + 0.04f, bladeBotY + 0.04f),// 3 underside, just past the haft socket
                new Vector2(pollLowX,  HeadY - 0.12f),     // 4 lower back (socket shoulder)
                new Vector2(hornBaseX, HeadY + 0.18f),     // 5 back, base of the horn
                new Vector2(hornTipX,  hornTipY),          // 6 horn tip (hooked, up-and-back)
                new Vector2(topBackX,  HeadY + 0.42f),     // 7 top edge, back of the blade crown
            };

            float zFront = HeadDepth;
            float zBack = -HeadDepth;

            // Front + back cheeks (two fans), with mild asymmetry: the back cheek's poll is a half-step
            // thinner so the head isn't perfectly symmetric (style-guide §3 "carved, not CNC'd").
            AddPolygonFan(verts, normals, headTris, sil, zFront, faceFront: true);
            AddPolygonFan(verts, normals, headTris, sil, zBack, faceFront: false);

            // The cutting edge spans sil[0]->sil[1]->sil[2] (top corner -> belly -> bottom corner). Those
            // two segments are replaced by the BEVEL (the near-white chamfer); every OTHER silhouette
            // segment gets a rim wall connecting the two cheeks.
            const int EDGE_SEGMENTS = 2; // segments 0->1 and 1->2 are the cutting edge
            int n = sil.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (i < EDGE_SEGMENTS) continue; // the bevel covers the two cutting-edge segments
                AddQuad(verts, normals, headTris,
                    new Vector3(sil[i].x, sil[i].y, zFront),
                    new Vector3(sil[j].x, sil[j].y, zFront),
                    new Vector3(sil[j].x, sil[j].y, zBack),
                    new Vector3(sil[i].x, sil[i].y, zBack));
            }

            // ---- The BEVEL: the signature near-white chamfer plane down the full cutting edge ----
            // For each cutting-edge segment, push the edge OUT to a sharp lip (proud in -X) on the z=0
            // mid-plane, then build a shallow chamfer facet from that lip to each cheek's edge vert. The
            // facet catches the key light as a distinct bright plane — GEOMETRY, not a texture line
            // (style-guide §1.3 / §3.2). Two facets per segment (front + back cheek) = a clean V-edge.
            for (int seg = 0; seg < EDGE_SEGMENTS; seg++)
            {
                Vector2 a = sil[seg], b = sil[seg + 1];
                float lipAx = a.x - 0.04f, lipBx = b.x - 0.04f; // the sharp lip, a touch proud in -X
                Vector3 lipA = new Vector3(lipAx, a.y, 0f);
                Vector3 lipB = new Vector3(lipBx, b.y, 0f);

                Vector3 af = new Vector3(a.x, a.y, zFront), bf = new Vector3(b.x, b.y, zFront);
                Vector3 ab = new Vector3(a.x, a.y, zBack),  bb = new Vector3(b.x, b.y, zBack);

                // Front chamfer facet (lip -> front cheek edge): tilts toward +Z and -X, catches the key.
                AddQuad(verts, normals, bevelTris, lipA, lipB, bf, af);
                // Back chamfer facet (lip -> back cheek edge): mirror, winding so its normal points -Z.
                AddQuad(verts, normals, bevelTris, lipB, lipA, ab, bb);
            }
        }

        // ---- helpers: every face gets its OWN verts + a flat per-face normal (hard-faceted look) ----
        static void AddQuad(List<Vector3> verts, List<Vector3> normals, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
            int i0 = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            for (int k = 0; k < 4; k++) normals.Add(nrm);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
        }

        // A polygon fan in the XY plane at depth z. faceFront=true winds so the normal points +Z (toward
        // the camera/front cheek); false winds the other way (-Z back cheek). Flat normal per triangle.
        static void AddPolygonFan(List<Vector3> verts, List<Vector3> normals, List<int> tris,
            List<Vector2> poly, float z, bool faceFront)
        {
            // centroid
            Vector2 c = Vector2.zero;
            foreach (var p in poly) c += p;
            c /= poly.Count;
            Vector3 center = new Vector3(c.x, c.y, z);
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = new Vector3(poly[i].x, poly[i].y, z);
                Vector3 b = new Vector3(poly[j].x, poly[j].y, z);
                int i0 = verts.Count;
                if (faceFront)
                {
                    verts.Add(center); verts.Add(a); verts.Add(b);
                    Vector3 nrm = Vector3.Cross(a - center, b - center).normalized;
                    normals.Add(nrm); normals.Add(nrm); normals.Add(nrm);
                }
                else
                {
                    verts.Add(center); verts.Add(b); verts.Add(a);
                    Vector3 nrm = Vector3.Cross(b - center, a - center).normalized;
                    normals.Add(nrm); normals.Add(nrm); normals.Add(nrm);
                }
                tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            }
        }
    }
}
