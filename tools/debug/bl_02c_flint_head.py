import bpy, bmesh, math, random
from mathutils import Vector

random.seed(11)
obj = bpy.data.objects['wpn_axe_01']
me = obj.data

bm = bmesh.new(); bm.from_mesh(me)

# ---------------- KNAPPED FLINT HEAD ----------------
# Flint hand-axe wedge in X-Z plane: a clear ALMOND/TEARDROP. Broad fanned cutting
# edge to -X (the working edge), narrowing to a lashed butt at +X (haft at x~0).
# Taller-than-wide bit so it reads as an AXE, not a round cobble. Irregular outline
# (asymmetric shoulders) = the knapped read.
prof = [
    (-0.40, 0.86),   # 0 cutting-edge tip (broad, reaches well out -X)
    (-0.36, 1.00),   # 1 upper edge
    (-0.22, 1.10),   # 2 upper shoulder (juts up, tall)
    (-0.05, 1.05),   # 3 top over the eye
    ( 0.10, 0.96),   # 4 top of butt
    ( 0.17, 0.86),   # 5 lashed butt (narrow) +X
    ( 0.12, 0.77),   # 6 lower butt
    (-0.04, 0.70),   # 7 underside over haft
    (-0.20, 0.66),   # 8 lower shoulder (juts down, asymmetric, lower than upper)
    (-0.35, 0.72),   # 9 lower edge
]
n = len(prof)
TH = 0.052   # half-thickness at the thick (lashed) end; tapers to the cutting edge

# Per-vert thickness: TAPER toward the cutting edge (-X) so the wedge thins to a
# working edge (flint biface), staying chunky at the lashed butt (+X). This is what
# makes it read as an AXE (a sharpening wedge), not a round cobble.
def th_at(x):
    # x ranges ~ -0.40 (edge) .. +0.17 (butt). thin at edge, full at butt.
    t = (x + 0.40) / (0.17 + 0.40)          # 0 at edge, 1 at butt
    t = max(0.0, min(1.0, t))
    return 0.012 + t*(TH-0.012)             # 0.012 (near-edge) .. 0.052 (butt)
front = [bm.verts.new((x,-th_at(x),z)) for (x,z) in prof]
back  = [bm.verts.new((x, th_at(x),z)) for (x,z) in prof]

# a center spine vert on each side, pushed OUT along Y so the broad faces are a
# shallow pyramid (gives the wedge a raised central ridge -> facets fan from it).
cx = sum(p[0] for p in prof)/n
cz = sum(p[1] for p in prof)/n
# bias the ridge toward the cutting edge (typical biface flint). Modest bulge so the
# head stays a flattish biface wedge (a big bulge reads as a round cobble, not an axe).
ridge_x = cx - 0.05
cfront = bm.verts.new((ridge_x, -TH-0.028, cz+0.02))
cback  = bm.verts.new((ridge_x,  TH+0.028, cz+0.02))

# fan the broad faces from the center spine to each outline edge -> triangular facets.
# Then jitter each rim vert's Y a touch and the spine for irregular knapping.
edge_strip_faces = []
for i in range(n):
    j = (i+1)%n
    # front (toward -Y): wind so normal points -Y-ish
    f1 = bm.faces.new((cfront, front[j], front[i]))
    f2 = bm.faces.new((cback,  back[i],  back[j]))
    # rim quad between front[i],front[j],back[j],back[i] = the chipped EDGE band
    rim = bm.faces.new((front[i], front[j], back[j], back[i]))
    if prof[i][0] < -0.27 and prof[j][0] < -0.27:
        edge_strip_faces.append(rim)

bm.normal_update()

# --- KNAP: displace each broad-face triangle's apex region irregularly ---
# Jitter the outline verts slightly in Y (in/out) and tweak Z so facets are uneven.
for v in front+back:
    v.co.y += random.uniform(-0.012, 0.012)
    v.co.x += random.uniform(-0.010, 0.010)
    v.co.z += random.uniform(-0.010, 0.010)
# subdivide the broad fan faces once and push new midpoints for finer flake scars
broad = [f for f in bm.faces if len(f.verts)==3]
sub_edges = list({e for f in broad for e in f.edges})   # dedupe shared fan edges
res = bmesh.ops.subdivide_edges(bm, edges=sub_edges,
                                cuts=1, use_grid_fill=False)
# push the newly created verts along their normal for chipped relief
new_verts = [g for g in res.get('geom_inner', []) if isinstance(g, bmesh.types.BMVert)]
bm.normal_update()
for v in new_verts:
    # outward on whichever side it sits (sign of y). Moderate, crisp flake-scar relief
    # (not lumpy) — the facets, not big bumps, carry the knapped read.
    s = 1.0 if v.co.y >= 0 else -1.0
    v.co.y += s*random.uniform(0.004, 0.020)
    v.co.x += random.uniform(-0.008, 0.008)
    v.co.z += random.uniform(-0.008, 0.008)

# ---------------- LASHING band ----------------
# a short wrapped binding where the head tail meets the haft (~z 0.74..0.86, around x 0..0.16).
# Model as a slightly-fatter 6-sided sleeve around the haft at that height, 3 bands.
def lash_ring(z, r, squash=1.0):
    ring=[]
    for i in range(SIDES if False else 6):
        a=(i/6)*2*math.pi+math.pi/6
        ring.append(bm.verts.new((0.0 + r*math.cos(a), r*math.sin(a)*squash, z)))
    return ring
SIDES=6
lz0, lz1, lz2, lz3 = 0.70, 0.745, 0.79, 0.835
lr = 0.062
lr_b = 0.052
lrings = [lash_ring(z, r) for z,r in ((lz0,lr_b),(lz1,lr),(lz2,lr),(lz3,lr_b))]
lash_faces=[]
for s in range(len(lrings)-1):
    for i in range(6):
        j=(i+1)%6
        lash_faces.append(bm.faces.new((lrings[s][i],lrings[s][j],lrings[s+1][j],lrings[s+1][i])))

bm.normal_update()
bm.to_mesh(me); bm.free(); me.update()

# shade smooth + all sharp -> faceted
for p in me.polygons: p.use_smooth = True
for e in me.edges: e.use_edge_sharp = True

import bmesh as _bm
tmp=_bm.new(); tmp.from_mesh(me); _bm.ops.triangulate(tmp,faces=tmp.faces[:])
tri=len(tmp.faces); tmp.free()
print('FLINT_AXE verts=%d faces=%d tris=%d' % (len(me.vertices), len(me.polygons), tri))
