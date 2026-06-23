"""Read-only: measure the 2.0x WIP variant — ratio + bend — to confirm before straighten+bake."""
import bpy, math
from mathutils import Vector

FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/team/devon-dev/wpn_axe_haft_2p0x_86cabh907.fbx'
JUNCTION_FRACTION = 0.50

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
co = [v.co.copy() for v in obj.data.vertices]
n = len(co)
zs = [c.z for c in co]
span = max(zs) - min(zs)
grip = min(zs); head_top = max(zs)
# NOTE: after a 2x haft stretch the 50%-of-span junction is NO LONGER the head base.
# The head base is INVARIANT in absolute Z (head verts never moved): use the fixed
# head-base from the source mesh = 0.022674 (the original 50% junction).
JUNC_ABS = 0.022674
head = [c for c in co if c.z > JUNC_ABS]
haft = [c for c in co if c.z <= JUNC_ABS]
head_lo = min(c.z for c in head); head_hi = max(c.z for c in head)
head_h = head_hi - head_lo
haft_len = JUNC_ABS - grip
ratio = haft_len / head_h
print("MEASURE_2P0X_START")
print("verts=%d  span(Z)=%.4f  grip=%.6f  head_top=%.6f" % (n, span, grip, head_top))
print("head_base(abs)=%.6f  head_h=%.4f  HAFT_LEN=%.4f  RATIO_haft:head=%.4f" %
      (JUNC_ABS, head_h, haft_len, ratio))
# bend on the haft (z <= head base)
rings = {}
for c in haft:
    rings.setdefault(round(c.z, 3), []).append(c)
ring_keys = sorted(rings.keys())
cents = []
for z in ring_keys:
    pts = rings[z]
    cx = sum(p.x for p in pts) / len(pts); cy = sum(p.y for p in pts) / len(pts)
    cents.append((z, cx, cy))
    print("  haft ring z=%+.4f centroid=(%+.5f,%+.5f)" % (z, cx, cy))
g = cents[0]; t = cents[-1]
seg = Vector((t[1]-g[1], t[2]-g[2], t[0]-g[0]))
dev = math.degrees(math.atan2(math.hypot(seg.x, seg.y), abs(seg.z)))
print("END_TO_END_AXIS_DEVIATION(2.0x) = %.3f deg" % dev)
print("head_top INVARIANT check: %.6f (expect 0.495347) -> %s" %
      (head_top, "OK" if abs(head_top - 0.495347) < 1e-5 else "CHANGED!"))
print("MEASURE_2P0X_DONE")
