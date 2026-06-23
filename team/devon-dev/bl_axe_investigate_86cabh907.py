"""
bl_axe_investigate_86cabh907.py — INVESTIGATION + RENDER (no bake, no re-author).

Answers ticket 86cabh907's "what happened to the axe shaft?" question:
  1. Vertex-by-vertex DIFF of the restored stone source (4208067 == db8990b, byte-
     identical) vs the 0.65x baked head (14d5a41). Reports which verts moved and by
     how much — the literal answer to "did the haft change?".
  2. Proportion MEASUREMENTS on the baked FBX (14d5a41): haft length, haft diameter,
     head height/width, haft-length:head-height ratio. Uses the SAME long-axis +
     junction logic as the bake (bl_17) so "head" and "haft" mean the same thing.
  3. Controlled RENDERS of the baked axe: front / side / 3-4 views, each with a 1.8m
     human-height reference bar beside the axe, saved as PNGs.

Read-only w.r.t. the committed FBX — never re-exports anything.

Run:
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    --python team/devon-dev/bl_axe_investigate_86cabh907.py
"""
import bpy, math
from mathutils import Vector

WT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt'
RESTORED = WT + r'/team/devon-dev/_axe_cmp/wpn_axe_restored_db8990b.fbx'   # == 4208067 stone source
BAKED    = WT + r'/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'   # == 14d5a41
OUTDIR   = WT + r'/team/devon-dev/axe_renders_86cabh907'
JUNCTION_FRACTION = 0.50   # == HeldWeaponCycleDebug.headJunctionFraction / bl_17

import os
os.makedirs(OUTDIR, exist_ok=True)


def load_single_mesh(fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(meshes) == 1, "expected 1 mesh, got %d in %s" % (len(meshes), fbx)
    return meshes[0]


def long_axis_of(co):
    bmin = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    bmax = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    ext = (bmax - bmin) * 0.5
    if ext.x >= ext.y and ext.x >= ext.z:
        a = 0
    elif ext.y >= ext.z:
        a = 1
    else:
        a = 2
    return a, bmin, bmax, ext


def comp(v, a):
    return v.x if a == 0 else (v.y if a == 1 else v.z)


# ============================================================================
# PART 1 — vertex diff: restored (source) vs baked
# ============================================================================
print("\n" + "=" * 70)
print("PART 1 — VERTEX DIFF: restored 4208067 source  vs  0.65x baked 14d5a41")
print("=" * 70)

obj_r = load_single_mesh(RESTORED)
co_r = [v.co.copy() for v in obj_r.data.vertices]
n_r = len(co_r)

obj_b = load_single_mesh(BAKED)
co_b = [v.co.copy() for v in obj_b.data.vertices]
n_b = len(co_b)

print("restored verts = %d   baked verts = %d   (counts %s)" %
      (n_r, n_b, "MATCH" if n_r == n_b else "DIFFER — topology changed!"))

la, bmin_r, bmax_r, ext_r = long_axis_of(co_r)
axis_name = "XYZ"[la]
span_r = bmax_r[la] - bmin_r[la]
junction = bmin_r[la] + span_r * JUNCTION_FRACTION
print("long axis = %s   span = %.4f   junctionCoord = %.4f (frac %.2f)" %
      (axis_name, span_r, junction, JUNCTION_FRACTION))

if n_r == n_b:
    moved = []
    haft_moved = []
    head_moved = []
    EPS = 1e-6
    for i in range(n_r):
        d = (co_b[i] - co_r[i]).length
        if d > EPS:
            moved.append((i, d))
            if comp(co_r[i], la) > junction:
                head_moved.append(i)
            else:
                haft_moved.append(i)
    n_head = sum(1 for i in range(n_r) if comp(co_r[i], la) > junction)
    n_haft = n_r - n_head
    print("verts MOVED (>%g): %d / %d" % (EPS, len(moved), n_r))
    print("  of which ABOVE junction (head region): %d / %d head verts" % (len(head_moved), n_head))
    print("  of which AT/BELOW junction (HAFT/lash/grip region): %d / %d haft verts" % (len(haft_moved), n_haft))
    if haft_moved:
        maxd = max((co_b[i] - co_r[i]).length for i in haft_moved)
        print("  *** HAFT VERTS MOVED: %d, max displacement %.6f ***" % (len(haft_moved), maxd))
    else:
        print("  *** HAFT UNCHANGED: 0 haft/lash/grip verts moved (byte/vertex-identical) ***")
    # grip origin (mesh z-min on long axis) check
    grip_r = min(comp(c, la) for c in co_r)
    grip_b = min(comp(c, la) for c in co_b)
    print("grip end (long-axis min): restored=%.6f baked=%.6f  %s" %
          (grip_r, grip_b, "IDENTICAL" if abs(grip_r - grip_b) < EPS else "MOVED"))
    # object origin (obj_b only; obj_r was freed by the second factory-reset import)
    print("baked object origin (matrix_world translation): %s" %
          ([round(x, 4) for x in obj_b.matrix_world.translation]))

# ============================================================================
# PART 2 — proportion measurements on the BAKED axe (14d5a41)
# ============================================================================
print("\n" + "=" * 70)
print("PART 2 — PROPORTIONS (baked 14d5a41 FBX, model units)")
print("=" * 70)

co = co_b
la2, bmin, bmax, ext = long_axis_of(co)
span = bmax[la2] - bmin[la2]
junc = bmin[la2] + span * JUNCTION_FRACTION

# off-axis indices
off = [i for i in (0, 1, 2) if i != la2]

# head verts (above junction) / haft verts (at/below)
head_i = [i for i in range(len(co)) if comp(co[i], la2) > junc]
haft_i = [i for i in range(len(co)) if comp(co[i], la2) <= junc]

# overall mesh long-axis length
print("OVERALL mesh long-axis (%s) length: %.4f" % ("XYZ"[la2], span))

# haft length = junction - grip end (the wooden handle from grip to where head starts)
grip_end = min(comp(c, la2) for c in co)
haft_len = junc - grip_end
print("HAFT length (grip end -> junction @ %.0f%% span): %.4f" % (JUNCTION_FRACTION * 100, haft_len))

# haft diameter = max off-axis span over the haft verts EXCLUDING the topmost band
# (near junction the wood may flare toward the head). Measure mid-haft.
# Use the lower 70% of the haft for a clean shaft diameter.
haft_lo = grip_end
haft_hi = grip_end + haft_len * 0.70
mid_haft = [i for i in haft_i if haft_lo <= comp(co[i], la2) <= haft_hi]
def off_span(idx):
    a0 = [comp(co[i], off[0]) for i in idx]
    a1 = [comp(co[i], off[1]) for i in idx]
    return (max(a0) - min(a0)), (max(a1) - min(a1))
ha, hb = off_span(mid_haft if mid_haft else haft_i)
haft_dia = max(ha, hb)
print("HAFT diameter (mid-haft, lower 70%%, off-axis %s/%s span): %.4f x %.4f -> max %.4f" %
      ("XYZ"[off[0]], "XYZ"[off[1]], ha, hb, haft_dia))

# head height = long-axis span of head verts; head width = max off-axis span of head verts
head_lo = min(comp(c, la2) for c in [co[i] for i in head_i])
head_hi = max(comp(c, la2) for c in [co[i] for i in head_i])
head_h = head_hi - head_lo
hwa, hwb = off_span(head_i)
head_w = max(hwa, hwb)
print("HEAD height (long-axis span of head verts): %.4f" % head_h)
print("HEAD width  (max off-axis span of head verts): %.4f  (X=%.4f off2=%.4f)" % (head_w, hwa, hwb))

ratio = haft_len / head_h if head_h > 1e-6 else 0
print("HAFT-LENGTH : HEAD-HEIGHT ratio = %.2f : 1   (a proper hatchet handle is ~3-4 : 1)" % ratio)
ratio_full = span / head_h if head_h > 1e-6 else 0
print("FULL-LENGTH : HEAD-HEIGHT ratio = %.2f : 1   (overall axe length / head height)" % ratio_full)

# ============================================================================
# PART 3 — RENDERS: front / side / 3-4 with a 1.8m reference bar
# ============================================================================
print("\n" + "=" * 70)
print("PART 3 — RENDERS (front / side / 3-4) with 1.8m human-height reference")
print("=" * 70)

# fresh scene with the baked axe
obj = load_single_mesh(BAKED)
me = obj.data

# flat material so the silhouette reads (workbench MATERIAL color)
mat = bpy.data.materials.new("AxeFlat")
mat.use_nodes = False
mat.diffuse_color = (0.55, 0.38, 0.22, 1.0)   # warm wood-ish so it reads
if me.materials:
    me.materials[0] = mat
else:
    me.materials.append(mat)

# axe world bounds (after import, matrix_world applied)
ws = [obj.matrix_world @ v.co for v in me.vertices]
wmin = Vector((min(p.x for p in ws), min(p.y for p in ws), min(p.z for p in ws)))
wmax = Vector((max(p.x for p in ws), max(p.y for p in ws), max(p.z for p in ws)))
wctr = (wmin + wmax) * 0.5
wsize = (wmax - wmin)
print("AXE world bounds: min=%s max=%s size=%s" %
      ([round(x, 3) for x in wmin], [round(x, 3) for x in wmax], [round(x, 3) for x in wsize]))

# 1.8m reference bar: a thin tall box, placed beside the axe along world X, base at axe base
bpy.ops.mesh.primitive_cube_add(size=1.0)
ref = bpy.context.active_object
ref.name = "Ref_1p8m"
ref.scale = (0.04, 0.04, 0.9)   # 1.8m tall (0.9 half-extent * 2), thin
# place its base at the axe's lowest point, offset to +X side of the axe
ref_base_z = wmin.z
ref.location = (wmax.x + 0.4, wctr.y, ref_base_z + 0.9)
refmat = bpy.data.materials.new("RefMat")
refmat.use_nodes = False
refmat.diffuse_color = (0.15, 0.55, 0.85, 1.0)   # blue, clearly the scale bar
ref.data.materials.append(refmat)
print("1.8m reference bar at X=%.3f (axe long-axis is %s; bar is 1.8m tall)" % (ref.location.x, "XYZ"[la2]))

# group center for camera aim = midpoint of axe+bar
aim = Vector(((wctr.x + ref.location.x) * 0.5, wctr.y, ref_base_z + 0.9))
extent = max(wsize.x, wsize.y, wsize.z, 1.8) * 1.6

# world / render settings
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.render.resolution_x = 900
scene.render.resolution_y = 1200
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new("W") if scene.world is None else scene.world
scene.world.color = (0.9, 0.9, 0.92)

# a sun for the workbench studio is implicit; add a camera
def make_cam():
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = extent
    return cam

cam = make_cam()

def look_at(cam, target, eye):
    cam.location = eye
    d = (target - eye)
    rot = d.to_track_quat('-Z', 'Y')
    cam.rotation_euler = rot.to_euler()

R = extent * 2.0
views = {
    "front": Vector((aim.x, aim.y - R, aim.z)),          # look along +Y (front)
    "side":  Vector((aim.x + R, aim.y, aim.z)),           # look along -X (side)
    "three_quarter": Vector((aim.x + R * 0.7, aim.y - R * 0.7, aim.z + R * 0.25)),
}

for name, eye in views.items():
    look_at(cam, aim, eye)
    scene.render.filepath = OUTDIR + "/axe_%s.png" % name
    bpy.ops.render.render(write_still=True)
    print("RENDERED %s -> %s" % (name, scene.render.filepath))

print("\nRENDERS_DONE  ->  %s" % OUTDIR)
print("INVESTIGATION_COMPLETE")
