"""Read-only baseline measurement of the baked axe (14d5a41) — confirm long axis,
junction, grip-end, haft length, head height, haft:head ratio. No bake, no export."""
import bpy
from mathutils import Vector

BAKED = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
JUNCTION_FRACTION = 0.50

def load_single_mesh(fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(meshes) == 1, "expected 1 mesh, got %d" % len(meshes)
    return meshes[0]

def comp(v, a):
    return v.x if a == 0 else (v.y if a == 1 else v.z)

obj = load_single_mesh(BAKED)
# LOCAL coords (these are the verts the bake will scale); also report world origin
co = [v.co.copy() for v in obj.data.vertices]
n = len(co)
bmin = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
bmax = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
ext = (bmax - bmin) * 0.5
if ext.x >= ext.y and ext.x >= ext.z: la = 0
elif ext.y >= ext.z: la = 1
else: la = 2
axis = "XYZ"[la]
span = bmax[la] - bmin[la]
grip_end = bmin[la]            # long-axis MIN = grip end (away from head)
head_top = bmax[la]           # long-axis MAX = head/blade end
junc = grip_end + span * JUNCTION_FRACTION
off = [i for i in (0,1,2) if i != la]

head_i = [i for i in range(n) if comp(co[i], la) > junc]
haft_i = [i for i in range(n) if comp(co[i], la) <= junc]

haft_len = junc - grip_end
# head height = long-axis span of head verts
head_lo = min(comp(co[i], la) for i in head_i)
head_hi = max(comp(co[i], la) for i in head_i)
head_h = head_hi - head_lo
ratio = haft_len / head_h if head_h > 1e-9 else 0

# haft diameter mid-shaft (lower 70%)
haft_hi70 = grip_end + haft_len*0.70
mid = [i for i in haft_i if grip_end <= comp(co[i], la) <= haft_hi70]
def off_span(idx):
    a0 = [comp(co[i], off[0]) for i in idx]; a1 = [comp(co[i], off[1]) for i in idx]
    return (max(a0)-min(a0)), (max(a1)-min(a1))
ha, hb = off_span(mid if mid else haft_i)
haft_dia = max(ha, hb)

print("BASELINE_MEASURE_START")
print("verts = %d" % n)
print("long_axis = %s   span = %.4f" % (axis, span))
print("grip_end(min %s) = %.6f   head_top(max %s) = %.6f" % (axis, grip_end, axis, head_top))
print("junction(@%.0f%%) = %.6f" % (JUNCTION_FRACTION*100, junc))
print("n_head_verts = %d   n_haft_verts = %d" % (len(head_i), len(haft_i)))
print("HAFT_LEN = %.4f" % haft_len)
print("HEAD_H = %.4f" % head_h)
print("HAFT_DIA = %.4f" % haft_dia)
print("RATIO_haft:head = %.4f" % ratio)
print("origin(world translation) = %s" % [round(x,6) for x in obj.matrix_world.translation])
print("BASELINE_MEASURE_DONE")
