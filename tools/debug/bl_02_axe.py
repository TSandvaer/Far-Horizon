import bpy, bmesh, math, os
from mathutils import Vector

mat = bpy.data.materials['WeaponPalette']
for nm in ('wpn_axe_01',):
    o = bpy.data.objects.get(nm)
    if o: bpy.data.objects.remove(o, do_unlink=True)

# ============================================================
# AXE anatomy (matches inspiration 21h08_08):
#   - haft runs vertically along +Z, passing THROUGH the head near the top
#   - head mounted at top of haft: BIT (cutting edge) to -X, POLL/beak to +X
#   - cutting edge = thin off-white bevel strip (physical inset face)
#   - hawk-beak poll with a top notch
# Built blade-up; origin set to grip midpoint at the very end (bl_origin).
# ============================================================
bm = bmesh.new()

# ---------------- HAFT ----------------
HAFT_TOP = 1.02          # haft extends above the head a touch (head wraps it)
HAFT_BOT = 0.0
R_TOP = 0.044
R_BOT = 0.034
SIDES = 6
HX = 0.0                 # haft centered on x=0
rings = []
NSEG = 6
for s in range(NSEG+1):
    t = s/NSEG
    z = HAFT_BOT + t*(HAFT_TOP-HAFT_BOT)
    r = R_BOT + t*(R_TOP-R_BOT)
    bend = math.sin(t*math.pi)*0.010    # subtle organic bend
    ring = []
    for i in range(SIDES):
        a = (i/SIDES)*2*math.pi + math.pi/SIDES
        v = bm.verts.new((HX + bend + r*math.cos(a), r*math.sin(a), z))
        ring.append(v)
    rings.append(ring)
for s in range(NSEG):
    for i in range(SIDES):
        j = (i+1)%SIDES
        bm.faces.new((rings[s][i], rings[s][j], rings[s+1][j], rings[s+1][i]))
bm.faces.new(list(reversed(rings[0])))      # bottom cap
bm.faces.new(rings[NSEG])                    # top cap

# ---------------- HEAD ----------------
# profile in X-Z plane (X across: bit -X, poll +X ; Z up). Head centered ~z=0.86.
# Ordered CCW starting at the top-front (top of cutting edge).
TH = 0.052           # half thickness (Y)
EZ = 0.86            # eye/head vertical center
prof = [
    # ---- bit / cutting edge side (left, -X). These get the bevel strip. ----
    (-0.300, 1.00),   # 0  top of cutting edge (juts up-left)
    (-0.330, 0.90),   # 1  upper cutting edge
    (-0.345, 0.80),   # 2  mid cutting edge (widest)
    (-0.330, 0.70),   # 3  lower cutting edge
    (-0.300, 0.62),   # 4  bottom of cutting edge (juts down-left)
    # ---- underside of bit -> beard concave -> haft ----
    (-0.150, 0.66),   # 5  underside of bit (concave up)
    (-0.045, 0.70),   # 6  bit meets haft front (eye underside front)
    ( 0.050, 0.70),   # 7  eye underside back
    # ---- poll / hawk-beak (right, +X) ----
    ( 0.150, 0.69),   # 8  poll bottom-front
    ( 0.250, 0.715),  # 9  poll underside mid
    ( 0.330, 0.74),   # 10 poll tip bottom (beak points right, slight down)
    ( 0.340, 0.80),   # 11 poll tip front
    ( 0.250, 0.85),   # 12 poll tip top (beak upper)
    ( 0.150, 0.83),   # 13 notch bottom (the dip on top, between peak and beak)
    ( 0.080, 0.93),   # 14 notch up to peak
    # ---- top peak then back to cutting edge top ----
    ( 0.000, 1.00),   # 15 top peak (above eye)
    (-0.130, 1.02),   # 16 top shoulder of bit
]
n = len(prof)
front = [bm.verts.new((x, -TH, z)) for (x,z) in prof]
back  = [bm.verts.new((x,  TH, z)) for (x,z) in prof]
f_front = bm.faces.new(list(reversed(front)))
f_back  = bm.faces.new(back)
rim_faces = []
for i in range(n):
    j = (i+1)%n
    rim_faces.append(bm.faces.new((front[i], front[j], back[j], back[i])))

# cutting-edge rim faces (prof verts 0..4) are identified geometrically in the UV step.
bm.normal_update()
me = bpy.data.meshes.new('wpn_axe_01')
bm.to_mesh(me)
bm.free()
obj = bpy.data.objects.new('wpn_axe_01', me)
bpy.context.collection.objects.link(obj)
obj.data.materials.append(mat)

for p in obj.data.polygons:
    p.use_smooth = True
for e in obj.data.edges:
    e.use_edge_sharp = True

import bmesh as _bm
tmp = _bm.new(); tmp.from_mesh(obj.data)
_bm.ops.triangulate(tmp, faces=tmp.faces[:])
tri = len(tmp.faces); tmp.free()
print('AXE_BUILT verts=%d faces=%d tris=%d' % (len(obj.data.vertices), len(obj.data.polygons), tri))
