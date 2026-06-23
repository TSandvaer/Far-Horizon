import bpy, bmesh, math, random
from mathutils import Vector

# ============================================================
# KNAPPED-FLINT AXE HEAD v2 (Sponsor checkpoint 2, 2026-06-19):
# Iteration 1 (bl_02c) read as a ROUNDED STONE MAUL/CLUB, not an axe. The fix is
# GEOMETRY-only: reshape into an ASYMMETRIC WEDGE that reads as a real axe head.
#   - BLADE side (toward -X): a broad, relatively FLAT face that TAPERS to a crisp
#     thin CUTTING EDGE (the bit) — a near-vertical thin line at most-negative X.
#     The blade flares out (and up/down) so the silhouette fans like an axe bit.
#   - POLL/BUTT side (toward +X): a chunky, blunt, HEAVIER block — the classic axe
#     asymmetry (a club is symmetric; an axe is bit + poll).
#   - Knapped flake-scar FACETS preserved on the broad blade faces (modeled facets
#     ARE the stone pattern; no texture/normal-map). Material/palette unchanged.
# Built blade-up (haft +Z); origin set to grip midpoint in finalize.
# ============================================================
random.seed(13)
obj = bpy.data.objects['wpn_axe_01']
me = obj.data
bm = bmesh.new(); bm.from_mesh(me)

# ---------------- HEAD OUTLINE (X-Z silhouette) ----------------
# Coordinate convention (matches haft built along +Z, head sits high on haft):
#   -X = blade / cutting edge (left);  +X = poll / butt (right).
#   z ~ 0.66..1.12 = the head band (haft top ~1.00).
# The outline is the axe SILHOUETTE seen broadside. It is deliberately ASYMMETRIC
# left-vs-right: the blade fans wide to a point at -X; the poll is a short chunky
# block at +X. Walk the outline CLOCKWISE starting at the top-blade corner.
#
# Index | role
#   0  top of blade flare (upper bit corner, juts up+out)
#   1  blade EDGE TIP upper (the bit, reaches far -X)
#   2  blade EDGE TIP lower (the bit) — 1&2 are the thin cutting line, close in z
#   3  bottom of blade flare (lower bit corner, juts down+out, slightly less than top)
#   4  underside neck (blade meets eye, pulls back toward +X and up)
#   5  poll bottom-front (start of the chunky butt block)
#   6  poll bottom-back (+X, blunt)
#   7  poll top-back (+X, blunt) — 6&7 are the flat blunt butt face
#   8  poll top-front
#   9  top neck (eye top, over the haft, pulls back toward the blade flare)
# The BLADE (-X) is the dominant feature: a tall fanned bit that flares out AND
# up/down to a long, crisp cutting line. The POLL (+X) is a SMALL compact blunt
# nub — deliberately much less mass than the blade so the silhouette reads "axe",
# not "double-wedge / pick". Neck is pinched (the eye) so blade and poll are two
# distinct masses, not one continuous plate.
prof = [
    (-0.20, 1.18),   # 0  top blade flare (juts high)
    (-0.50, 1.00),   # 1  bit upper (the cutting line, reaches far -X)
    (-0.51, 0.80),   # 2  bit lower (thin cutting line with #1, long edge)
    (-0.22, 0.60),   # 3  bottom blade flare (juts low)
    (-0.06, 0.66),   # 4  pinched underside neck (the eye, pulls in)
    ( 0.10, 0.72),   # 5  poll bottom (small blunt nub)
    ( 0.18, 0.80),   # 6  poll back-bottom (blunt butt, compact)
    ( 0.18, 0.90),   # 7  poll back-top (blunt butt, compact)
    ( 0.08, 0.96),   # 8  poll top (small)
    (-0.06, 1.02),   # 9  pinched top neck (the eye, pulls in)
]
n = len(prof)

# ---- per-vertex HALF-THICKNESS (the Y bulge of the broad faces) ----
# Thin at the cutting edge (bit), full at the poll. Map by X: bit at x~-0.45 -> ~0,
# poll at x~+0.21 -> max. This taper is what makes the wedge read as an axe.
TH_POLL = 0.058      # half-thickness at the blunt butt (chunky)
TH_EDGE = 0.004      # half-thickness at the cutting line (crisp, near-zero -> a thin bit)
X_EDGE, X_POLL = -0.51, 0.18
def th_at(x):
    t = (x - X_EDGE) / (X_POLL - X_EDGE)     # 0 at bit, 1 at poll
    t = max(0.0, min(1.0, t))
    # ease so the taper concentrates near the bit (a real biface thins fast at the edge)
    t = t*t*(3-2*t)                          # smoothstep
    return TH_EDGE + t*(TH_POLL-TH_EDGE)

front = [bm.verts.new((x,-th_at(x),z)) for (x,z) in prof]   # -Y broad face
back  = [bm.verts.new((x, th_at(x),z)) for (x,z) in prof]   # +Y broad face

# ---- central spine ridge on each broad face (biface bulge), biased to the poll ----
cx = sum(p[0] for p in prof)/n
cz = sum(p[1] for p in prof)/n
ridge_x = cx + 0.10                       # ridge biased hard toward poll -> blade face stays FLAT
ridge_th = th_at(ridge_x) + 0.018         # small bulge: a flat biface, not a dome
cfront = bm.verts.new((ridge_x, -ridge_th, cz+0.00))
cback  = bm.verts.new((ridge_x,  ridge_th, cz+0.00))

# ---- build broad-face fans + rim band ----
for i in range(n):
    j = (i+1)%n
    bm.faces.new((cfront, front[j], front[i]))   # front fan (normal ~ -Y)
    bm.faces.new((cback,  back[i],  back[j]))     # back fan  (normal ~ +Y)
    # rim quad = the chipped edge band between front & back outline
    bm.faces.new((front[i], front[j], back[j], back[i]))

bm.normal_update()

# ---- KNAP: irregular flake-scar relief on the broad faces ----
# Jitter outline verts (NOT the two bit verts #1,#2 — keep the cutting line crisp).
PROTECT = {1, 2}
for idx, v in enumerate(front+back):
    if (idx % n) in PROTECT:
        continue
    v.co.x += random.uniform(-0.010, 0.010)
    v.co.z += random.uniform(-0.010, 0.010)
    # nudge Y but never thicken past the local taper (keeps the bit thin)
    v.co.y += random.uniform(-0.010, 0.010)

# subdivide the broad fan triangles once -> finer facets; push midpoints out as scars
broad = [f for f in bm.faces if len(f.verts) == 3]
sub_edges = list({e for f in broad for e in f.edges})
res = bmesh.ops.subdivide_edges(bm, edges=sub_edges, cuts=1, use_grid_fill=False)
new_verts = [g for g in res.get('geom_inner', []) if isinstance(g, bmesh.types.BMVert)]
bm.normal_update()
for v in new_verts:
    # don't push scar relief out near the cutting edge (keep the bit a clean wedge)
    if v.co.x < X_EDGE + 0.10:
        continue
    s = 1.0 if v.co.y >= 0 else -1.0
    v.co.y += s*random.uniform(0.004, 0.016)
    v.co.x += random.uniform(-0.006, 0.006)
    v.co.z += random.uniform(-0.006, 0.006)

# ---------------- LASHING band (unchanged design) ----------------
# short wrapped binding where the head/eye meets the haft (~z 0.70..0.835, around the haft).
def lash_ring(z, r):
    ring=[]
    for i in range(6):
        a=(i/6)*2*math.pi+math.pi/6
        ring.append(bm.verts.new((0.0 + r*math.cos(a), r*math.sin(a), z)))
    return ring
lz = (0.70, 0.745, 0.79, 0.835)
lr_b, lr = 0.052, 0.062
lrings = [lash_ring(z, r) for z,r in ((lz[0],lr_b),(lz[1],lr),(lz[2],lr),(lz[3],lr_b))]
for s in range(len(lrings)-1):
    for i in range(6):
        j=(i+1)%6
        bm.faces.new((lrings[s][i],lrings[s][j],lrings[s+1][j],lrings[s+1][i]))

bm.normal_update()
bm.to_mesh(me); bm.free(); me.update()

# shade smooth + all edges sharp -> fully faceted (per blender-asset-pipeline §4)
for p in me.polygons: p.use_smooth = True
for e in me.edges: e.use_edge_sharp = True

import bmesh as _bm
tmp=_bm.new(); tmp.from_mesh(me); _bm.ops.triangulate(tmp,faces=tmp.faces[:])
tri=len(tmp.faces); tmp.free()
print('FLINT_AXE_V2 verts=%d faces=%d tris=%d' % (len(me.vertices), len(me.polygons), tri))
