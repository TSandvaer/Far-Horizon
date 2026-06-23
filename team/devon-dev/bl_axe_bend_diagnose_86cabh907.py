"""
bl_axe_bend_diagnose_86cabh907.py — DIAGNOSE-ONLY for ticket 86cabh907 / PR #100.

The Sponsor soaked the e0babe1 build (the "straight 2.0x haft") and reported the
handle STILL BENDS. The final-bake script measured residual_bend 0.0000deg. Root-cause
the contradiction. NO re-bake, NO export, NO FBX edit — pure measurement + render.

Method:
  1. Import the SHIPPED wpn_axe_01.fbx at HEAD (1ff3510).
  2. ORTHOGRAPHIC renders (NO perspective) from FRONT (-Y look), SIDE (+X look),
     TOP (-Z look). Perspective can make a straight handle LOOK bent; ortho is truth.
  3. Per-plane straightness of the HAFT (z <= junction): bin verts into Z rings,
     report the centroid drift in X, in Y, AND in Z separately (the prior measure
     fixed the +X lean — a bend in the UNCHECKED plane would survive).
  4. Per-plane straightness of the WHOLE length (grip -> head_top) — does the head
     ring-centroid line continue the haft's axis, or kink off it?
  5. Head-junction angle: principal axis of the HEAD verts vs the HAFT axis vs +Z.

Run:
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    --python team/devon-dev/bl_axe_bend_diagnose_86cabh907.py
"""
import bpy, math
from mathutils import Vector

WT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt'
FBX = WT + r'/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
OUTDIR = WT + r'/team/devon-dev/bend_diag_86cabh907'
JUNCTION = 0.022674   # head base z from the baseline measure (head = z > this)

# ---------------------------------------------------------------- load
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
# bake the import transform into local coords so v.co is world-aligned
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = obj.data
co = [v.co.copy() for v in me.vertices]
n = len(co)

zmin_all = min(c.z for c in co)
zmax_all = max(c.z for c in co)
print("DIAG_START verts=%d  full_z=[%.5f .. %.5f]  junction=%.5f" %
      (n, zmin_all, zmax_all, JUNCTION))

# ---------------------------------------------------------------- ORTHO renders
def setup_palette_view():
    for m in obj.data.materials:
        pass
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    try:
        sc.display.shading.light = 'FLAT'
        sc.display.shading.color_type = 'SINGLE'
        sc.display.shading.single_color = (0.55, 0.40, 0.28)
    except Exception as e:
        print("shading cfg warn:", e)
    sc.render.resolution_x = 900
    sc.render.resolution_y = 900
    sc.render.film_transparent = False
    try:
        sc.world.color = (1, 1, 1)
    except Exception:
        pass

def add_ortho_cam(name, location, rot_euler):
    cam_data = bpy.data.cameras.new(name)
    cam_data.type = 'ORTHO'
    span = max(zmax_all - zmin_all,
               max(c.x for c in co) - min(c.x for c in co),
               max(c.y for c in co) - min(c.y for c in co))
    cam_data.ortho_scale = span * 1.25
    cam = bpy.data.objects.new(name, cam_data)
    cam.location = location
    cam.rotation_euler = rot_euler
    bpy.context.scene.collection.objects.link(cam)
    return cam

setup_palette_view()
cx = (max(c.x for c in co) + min(c.x for c in co)) / 2
cy = (max(c.y for c in co) + min(c.y for c in co)) / 2
cz = (zmax_all + zmin_all) / 2
D = 3.0

# FRONT: camera looks along -Y (down +Y axis toward -Y). Shows X (horizontal) vs Z (vertical).
front = add_ortho_cam("front", (cx, cy - D, cz), (math.radians(90), 0, 0))
# SIDE: camera looks along -X. Shows Y (horizontal) vs Z (vertical).
side  = add_ortho_cam("side",  (cx + D, cy, cz), (math.radians(90), 0, math.radians(90)))
# TOP: camera looks straight down -Z. Shows X vs Y (the cross-section of a straight column = a dot/point).
top   = add_ortho_cam("top",   (cx, cy, cz + D), (0, 0, 0))

renders = {
    "axe_ortho_FRONT_86cabh907.png": front,
    "axe_ortho_SIDE_86cabh907.png":  side,
    "axe_ortho_TOP_86cabh907.png":   top,
}
for fname, cam in renders.items():
    bpy.context.scene.camera = cam
    bpy.context.scene.render.filepath = OUTDIR + "/" + fname
    bpy.ops.render.render(write_still=True)
    print("RENDERED %s" % (OUTDIR + "/" + fname))

# ---------------------------------------------------------------- per-plane straightness
def ring_centroids(verts, zkeys_round=3):
    rings = {}
    for c in verts:
        rings.setdefault(round(c.z, zkeys_round), []).append(c)
    out = []
    for z in sorted(rings.keys()):
        pts = rings[z]
        out.append((z,
                    sum(p.x for p in pts) / len(pts),
                    sum(p.y for p in pts) / len(pts),
                    len(pts)))
    return out

haft = [c for c in co if c.z <= JUNCTION + 1e-9]
head = [c for c in co if c.z >  JUNCTION + 1e-9]

print("\n=== HAFT per-ring centroids (z <= junction) ===")
hc = ring_centroids(haft)
for z, x, y, k in hc:
    print("  haft z=%+.4f  C=(%+.6f, %+.6f)  n=%d" % (z, x, y, k))

print("\n=== HEAD per-ring centroids (z > junction) ===")
hdc = ring_centroids(head)
for z, x, y, k in hdc:
    print("  head z=%+.4f  C=(%+.6f, %+.6f)  n=%d" % (z, x, y, k))

print("\n=== WHOLE-LENGTH per-ring centroids (grip -> head top) ===")
allc = ring_centroids(co)
for z, x, y, k in allc:
    print("  all  z=%+.4f  C=(%+.6f, %+.6f)  n=%d" % (z, x, y, k))

def plane_spans(cents):
    xs = [c[1] for c in cents]; ys = [c[2] for c in cents]; zs = [c[0] for c in cents]
    return (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))

def axis_deviation_deg(cents):
    """end-to-end centroid line vs pure +Z, broken into the X-plane and Y-plane tilt."""
    g = cents[0]; t = cents[-1]
    dz = abs(t[0] - g[0]) or 1e-9
    dx = t[1] - g[1]; dy = t[2] - g[2]
    devx = math.degrees(math.atan2(dx, dz))   # lean in the X-Z plane (front view)
    devy = math.degrees(math.atan2(dy, dz))   # lean in the Y-Z plane (side view)
    devtot = math.degrees(math.atan2(math.hypot(dx, dy), dz))
    return devx, devy, devtot

def max_perp_dev(cents):
    """max perpendicular distance of any ring centroid from the straight grip->top line,
    split per plane."""
    g = Vector((cents[0][1], cents[0][2], cents[0][0]))
    t = Vector((cents[-1][1], cents[-1][2], cents[-1][0]))
    ab = t - g
    mx = my = mtot = 0.0
    for c in cents:
        p = Vector((c[1], c[2], c[0]))
        tt = (p - g).dot(ab) / ab.dot(ab)
        proj = g + ab * tt
        d = p - proj
        mx = max(mx, abs(d.x)); my = max(my, abs(d.y)); mtot = max(mtot, d.length)
    return mx, my, mtot

print("\n=== PER-PLANE DEFLECTION ===")
for label, cents in (("HAFT", hc), ("WHOLE", allc)):
    sx, sy, sz = plane_spans(cents)
    devx, devy, devtot = axis_deviation_deg(cents)
    px, py, ptot = max_perp_dev(cents)
    print("[%s] centroid SPAN  X=%.6f  Y=%.6f  Z=%.6f" % (label, sx, sy, sz))
    print("[%s] end-to-end AXIS lean  X-plane(front)=%.4f deg  Y-plane(side)=%.4f deg  total=%.4f deg"
          % (label, devx, devy, devtot))
    print("[%s] MAX perp drift from straight line  X=%.6f  Y=%.6f  total=%.6f"
          % (label, px, py, ptot))

# ---------------------------------------------------------------- head-junction angle
# HAFT principal axis (grip-end ring centroid -> junction-end ring centroid)
haft_g = Vector((hc[0][1], hc[0][2], hc[0][0]))
haft_t = Vector((hc[-1][1], hc[-1][2], hc[-1][0]))
haft_axis = (haft_t - haft_g).normalized()
# HEAD principal axis (head-base ring centroid -> head-top ring centroid)
head_g = Vector((hdc[0][1], hdc[0][2], hdc[0][0]))
head_t = Vector((hdc[-1][1], hdc[-1][2], hdc[-1][0]))
head_axis = (head_t - head_g).normalized()
junction_angle = math.degrees(math.acos(max(-1, min(1, haft_axis.dot(head_axis)))))
# also: angle of head axis vs +Z, split per plane
hz = abs(head_axis.z) or 1e-9
head_vs_z_x = math.degrees(math.atan2(head_axis.x, hz))
head_vs_z_y = math.degrees(math.atan2(head_axis.y, hz))

print("\n=== HEAD-JUNCTION ANGLE ===")
print("haft_axis = (%+.4f, %+.4f, %+.4f)" % (haft_axis.x, haft_axis.y, haft_axis.z))
print("head_axis = (%+.4f, %+.4f, %+.4f)" % (head_axis.x, head_axis.y, head_axis.z))
print("HEAD-vs-HAFT angle = %.4f deg" % junction_angle)
print("HEAD axis vs +Z:  X-plane=%.4f deg  Y-plane=%.4f deg" % (head_vs_z_x, head_vs_z_y))

# ---------------------------------------------------------------- centroid-offset at junction
# Does the head sit OFFSET sideways from the haft's top? (a step/kink even if axes parallel)
haft_top_c = Vector((hc[-1][1], hc[-1][2]))
head_base_c = Vector((hdc[0][1], hdc[0][2]))
junction_offset = (head_base_c - haft_top_c).length
print("JUNCTION centroid offset (haft-top -> head-base) = %.6f  (dX=%+.6f dY=%+.6f)"
      % (junction_offset, head_base_c.x - haft_top_c.x, head_base_c.y - haft_top_c.y))

print("DIAG_DONE")
