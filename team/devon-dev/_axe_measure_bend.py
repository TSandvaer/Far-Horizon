"""Read-only: measure the HAFT BEND of the baked axe (14d5a41).

The haft runs along Z (long axis). A perfectly STRAIGHT haft has all haft rings
centred on the SAME (X,Y) as you walk up Z. A bent haft drifts its ring centre in
(X,Y) as Z increases. We:
  1. Bin the HAFT verts (z <= junction 0.022674) into Z rings.
  2. Compute each ring's centroid (X,Y).
  3. Fit the centroid drift vs Z -> the lateral deflection of the haft.
  4. Report the bend ANGLE = atan2(lateral span, haft length) and the per-ring drift.

NO bake, NO export — pure measurement so we KNOW the original bend before straightening.
"""
import bpy, math
from mathutils import Vector

BAKED = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
JUNCTION = 0.022674   # 50% junction from the baseline measure

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=BAKED)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
co = [v.co.copy() for v in obj.data.vertices]

# long axis = Z (confirmed by baseline). Haft = z <= junction.
haft = [c for c in co if c.z <= JUNCTION + 1e-9]
zmin = min(c.z for c in haft)
zmax = max(c.z for c in haft)
print("BEND_MEASURE_START")
print("haft verts=%d  z range [%.4f .. %.4f]  haft_len=%.4f" % (len(haft), zmin, zmax, zmax - zmin))

# Bin into rings by rounding Z to 4 decimals (the mesh has discrete rings).
rings = {}
for c in haft:
    key = round(c.z, 3)
    rings.setdefault(key, []).append(c)
ring_keys = sorted(rings.keys())
print("distinct haft Z rings: %d" % len(ring_keys))

centroids = []
for z in ring_keys:
    pts = rings[z]
    cx = sum(p.x for p in pts) / len(pts)
    cy = sum(p.y for p in pts) / len(pts)
    centroids.append((z, cx, cy, len(pts)))
    print("  ring z=%+.4f  centroid=(%+.5f, %+.5f)  n=%d" % (z, cx, cy, len(pts)))

# Lateral deflection: how far the centroid (X,Y) wanders across the haft span.
xs = [c[1] for c in centroids]
ys = [c[2] for c in centroids]
dx = max(xs) - min(xs)
dy = max(ys) - min(ys)
lateral = math.hypot(dx, dy)
haft_len = zmax - zmin
# Bend angle: the angle the haft axis (grip-end centroid -> top centroid) makes vs pure +Z.
g = centroids[0]; t = centroids[-1]  # grip-end ring and top ring
seg = Vector((t[1] - g[1], t[2] - g[2], t[0] - g[0]))  # (dX, dY, dZ) end-to-end
axis_dev_deg = math.degrees(math.atan2(math.hypot(seg.x, seg.y), abs(seg.z)))
# Also the MAX per-ring deviation from the straight grip->top line.
import math as _m
def point_line_dist(p, a, b):
    ap = Vector((p[1]-a[1], p[2]-a[2], p[0]-a[0]))
    ab = Vector((b[1]-a[1], b[2]-a[2], b[0]-a[0]))
    t = ap.dot(ab) / ab.dot(ab)
    proj = a_vec = Vector((a[1],a[2],a[0])) + ab * t
    pv = Vector((p[1], p[2], p[0]))
    return (pv - proj).length
maxdev = max(point_line_dist(c, g, t) for c in centroids)

print("LATERAL_CENTROID_DRIFT  dX=%.5f  dY=%.5f  lateral=%.5f  (over haft_len=%.4f)" % (dx, dy, lateral, haft_len))
print("END_TO_END_AXIS_DEVIATION = %.3f deg  (grip-end centroid -> top centroid vs +Z)" % axis_dev_deg)
print("MAX_PER_RING_DEVIATION from straight grip->top line = %.5f  (≈ %.3f deg over haft)"
      % (maxdev, math.degrees(math.atan2(maxdev, haft_len))))
print("BEND_MEASURE_DONE")
