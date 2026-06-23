import bpy, bmesh, math, os, random
from mathutils import Vector

mat = bpy.data.materials['WeaponPalette']
for nm in ('wpn_axe_01',):
    o = bpy.data.objects.get(nm)
    if o: bpy.data.objects.remove(o, do_unlink=True)

random.seed(7)   # deterministic knapping

# ============================================================
# KNAPPED-FLINT AXE (Sponsor 2026-06-19): the head is a chipped flint wedge
# with flake-scar facets MODELLED into the surface (the faceting IS the pattern).
# Built blade-up (+Z); grip-midpoint origin set in finalize.
#   - HEAD: almond/teardrop flint wedge, broad cutting edge to -X, lashed onto
#     the haft near the top. Surface subdivided into irregular facets, each
#     interior vert pushed along the face normal -> knapped/chipped read.
#   - HAFT: 6-sided tapered wood, subtle bend.
#   - LASHING: a short wrapped band where the head meets the haft (binding).
# ============================================================
bm = bmesh.new()

# ---------------- HAFT ----------------
HAFT_TOP = 1.00
HAFT_BOT = 0.0
R_TOP = 0.044; R_BOT = 0.034
SIDES = 6
rings = []; NSEG = 6
for s in range(NSEG+1):
    t = s/NSEG
    z = HAFT_BOT + t*(HAFT_TOP-HAFT_BOT)
    r = R_BOT + t*(R_TOP-R_BOT)
    bend = math.sin(t*math.pi)*0.010
    ring=[]
    for i in range(SIDES):
        a = (i/SIDES)*2*math.pi + math.pi/SIDES
        ring.append(bm.verts.new((bend + r*math.cos(a), r*math.sin(a), z)))
    rings.append(ring)
for s in range(NSEG):
    for i in range(SIDES):
        j=(i+1)%SIDES
        bm.faces.new((rings[s][i],rings[s][j],rings[s+1][j],rings[s+1][i]))
bm.faces.new(list(reversed(rings[0])))
bm.faces.new(rings[NSEG])

bm.normal_update()
me = bpy.data.meshes.new('wpn_axe_01')
bm.to_mesh(me); bm.free()
obj = bpy.data.objects.new('wpn_axe_01', me)
bpy.context.collection.objects.link(obj)
obj.data.materials.append(mat)
obj['n_haft_faces'] = len(me.polygons)  # remember where haft ends for UV
print('HAFT verts=%d faces=%d' % (len(me.vertices), len(me.polygons)))
