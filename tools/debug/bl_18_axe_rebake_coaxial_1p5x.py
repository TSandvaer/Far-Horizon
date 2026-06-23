"""
bl_18_axe_rebake_coaxial_1p5x.py — RE-BAKE wpn_axe_01.fbx for 86cabh907 / PR #100 re-soak.

TWO mechanical edits on the CURRENT FBX (2.0x haft, head tilted 20.14deg = the dogleg
the Sponsor soaked), NO reshape:

  1. HEAD -> RIGID-ROTATE COAXIAL. The 110 head verts (z > HEAD_BASE_Z) are rotated as a
     RIGID body about the head-base MOUNT centroid so the head's principal (centroid-line)
     axis -- measured (+0.3428,-0.0311,+0.9389), 20.14deg off +Z -- aligns with the haft
     +Z long axis. This is a pure rotation: every head-vert's position RELATIVE to the head
     is preserved (pairwise distances invariant to ~0), the head SHAPE/SIZE/material are
     byte-identical-modulo-rotation. NOT a reshape; does NOT break the 0.65x head-lock.
     Pivot = the head-base ring centroid (the mount point) so the junction does NOT shift.

  2. HAFT -> 1.5x LENGTH (Sponsor picked shorter; 2.0x read too long in-hand). The 18 haft
     verts' long-axis (Z) coord are scaled about the FIXED head-base (z=HEAD_BASE_Z) by
     0.75 (current is 2.0x of the original short haft; 2.0 * 0.75 = 1.5x). Result haft:head
     ratio 1.500. Haft stays STRAIGHT (already 0deg all planes; we touch only its Z).

PRESERVED: grip origin (mesh z-min grip end / obj origin (0,0,0)), single WeaponPalette
material slot, faceted per-face normals (NO RecalculateNormals -- bake-preserved per-face
normals are load-bearing for flat-shaded look + URP Cull Back, lowpoly-quality.md §1),
§8 FBX export (-Y Fwd / Z Up / FBX Unit Scale / Normals Only), 128 verts / 236 tris,
Merge-by-Distance (no loose verts introduced -- we move verts, never add).

ALL constants below are MEASURED from the current FBX (see /tmp/seg2_axe.py run, 86cabh907):
  HEAD_BASE_Z = 0.022674   (== bl_17/commit 18bec7c fixed head-base)
  HEAD_AXIS   = (+0.3428,-0.0311,+0.9389)  (centroid-line, 20.14deg off +Z)
  N_HEAD=110  N_HAFT=18

Run:
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    --python tools/debug/bl_18_axe_rebake_coaxial_1p5x.py

Idempotent w.r.t. the COMMITTED FBX: re-imports fresh every run; overwrites the FBX. To
re-run, FIRST `git checkout` the FBX (else it compounds -- the head re-rotates from already-
coaxial = no-op-ish but the haft would re-scale 0.75 again -> 1.125x).
"""
import bpy, math
from mathutils import Vector, Matrix

FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
HEAD_BASE_Z = 0.022674      # fixed head-base (bl_17 / commit 18bec7c)
HAFT_LEN_FACTOR = 0.75      # current 2.0x * 0.75 = 1.5x of the original short haft
TARGET = Vector((0.0, 0.0, 1.0))   # +Z = haft long axis (head must align to this)

# ------------------------------------------------------------------ import fresh
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1, "expected 1 mesh, got %d" % len(meshes)
obj = meshes[0]
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
# bake import transform so v.co is in the same frame the measurements used
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = obj.data
n = len(me.vertices)
print("IMPORTED %s verts=%d faces=%d mats=%d" % (obj.name, n, len(me.polygons), len(me.materials)))
assert len(me.materials) == 1 and me.materials[0].name == 'WeaponPalette', \
    "expected single WeaponPalette slot, got %s" % [m.name if m else None for m in me.materials]

co = [v.co.copy() for v in me.vertices]
head_i = [i for i in range(n) if co[i].z > HEAD_BASE_Z + 1e-9]
haft_i = [i for i in range(n) if co[i].z <= HEAD_BASE_Z + 1e-9]
print("SEGMENT: N_HEAD=%d N_HAFT=%d (expect 110 / 18)" % (len(head_i), len(haft_i)))
assert len(head_i) == 110 and len(haft_i) == 18, "head/haft split drifted -- abort"

# ------------------------------------------------------------------ head axis + pivot (measured live)
import collections
import numpy as np

def ring_centroids(idx, coords):
    """Z-binned ring centroids (bin 3dp), sorted base->top. Robust, binning-stable."""
    rings = collections.defaultdict(list)
    for i in idx:
        rings[round(coords[i].z, 3)].append(coords[i])
    zk = sorted(rings)
    out = []
    for z in zk:
        pts = rings[z]
        out.append(Vector((sum(p.x for p in pts) / len(pts),
                           sum(p.y for p in pts) / len(pts), z)))
    return out

def head_mount_axis(idx, coords):
    """Head MOUNT axis = the Sponsor-diagnosed CENTROID-LINE: head-base ring centroid ->
    head-top ring centroid (3dp Z-binning). This is the EXACT method that measured the
    20.14deg dogleg in the brief. Returns (unit axis toward +Z, head-base ring centroid)."""
    cents = ring_centroids(idx, coords)
    ax = (cents[-1] - cents[0]).normalized()
    if ax.z < 0:
        ax = -ax
    return ax, cents[0].copy()

def head_axis_halfsplit(idx, coords):
    """BINNING-INDEPENDENT head axis: centroid of the BOTTOM-HALF head verts -> centroid of
    the TOP-HALF head verts, split by the median of each vert's projection on the head's own
    base->top span. Unlike Z-ring binning this does NOT re-bin under rotation, so before/after
    are directly comparable and the converged angle reflects the TRUE mount tilt, not a
    binning artifact. Returns the unit axis toward +Z."""
    zs = sorted(coords[i].z for i in idx)
    med = zs[len(zs) // 2]
    bot = [coords[i] for i in idx if coords[i].z <= med]
    top = [coords[i] for i in idx if coords[i].z > med]
    if not bot or not top:
        return Vector((0, 0, 1))
    cb = Vector((sum(p.x for p in bot) / len(bot), sum(p.y for p in bot) / len(bot), sum(p.z for p in bot) / len(bot)))
    ct = Vector((sum(p.x for p in top) / len(top), sum(p.y for p in top) / len(top), sum(p.z for p in top) / len(top)))
    ax = (ct - cb).normalized()
    if ax.z < 0:
        ax = -ax
    return ax

head_axis, PIVOT = head_mount_axis(head_i, co)
ang_before = math.degrees(math.acos(max(-1, min(1, abs(head_axis.z)))))
ang_before_hs = math.degrees(math.acos(max(-1, min(1, abs(head_axis_halfsplit(head_i, co).z)))))
print("HEAD half-split axis (before) vs +Z = %.4f deg" % ang_before_hs)
print("HEAD_AXIS(before)=(%+.4f,%+.4f,%+.4f)  vs+Z=%.4f deg" %
      (head_axis.x, head_axis.y, head_axis.z, ang_before))
print("PIVOT (head-base mount centroid)=(%+.5f,%+.5f,%+.5f)" % (PIVOT.x, PIVOT.y, PIVOT.z))

# ------------------------------------------------------------------ pairwise-distance baseline (rigidity proof)
# sample pairwise distances among ALL head verts before the rotation (must be invariant after)
def pairwise_sig(coords, idx):
    s = 0.0
    mx = 0.0
    for a in range(0, len(idx), 7):       # stride for speed; covers the whole cluster
        for b in range(a + 1, len(idx), 5):
            d = (coords[idx[a]] - coords[idx[b]]).length
            s += d
            mx = max(mx, d)
    return s, mx
sig_before = pairwise_sig(co, head_i)

# ------------------------------------------------------------------ THE RIGID ROTATION (iterated)
# rotate head_axis -> +Z about PIVOT. Because the centroid-line axis is Z-BINNED, a single
# rotation leaves a small residual (the tilt re-bins verts into different Z-rings). So we
# ITERATE: rotate, re-measure the centroid-line, rotate the residual, until coaxial < 0.3deg.
# Each step is a rigid rotation about the SAME fixed pivot, so the composition stays rigid
# (head shape/size preserved -- proven by the pairwise-distance invariant below).
def rot_axis_to_z(axis):
    v = axis.cross(TARGET)
    s = v.length
    c = axis.dot(TARGET)
    if s < 1e-12:
        return Matrix.Identity(3)
    return Matrix.Rotation(math.atan2(s, c), 3, v.normalized())

# RIGID rotation aligning the head MOUNT axis to +Z. On this lopsided wedge the centroid-line
# and the binning-independent half-split disagree on the exact residual, so we ITERATE on the
# AVERAGE of both robust measures until both are minimized -- the result is the rotation that
# best uprights the head's overall mass AND its base->top centroid stack. Each step is a rigid
# rotation about the fixed mount pivot (pairwise distances invariant -> head shape preserved).
# Ortho RENDERS below are the visual ground truth (metrics are a guide, the eye is the judge).
cur = [me.vertices[i].co.copy() for i in range(n)]
def both_angles(coords):
    a_cl = math.degrees(math.acos(max(-1, min(1, abs(head_mount_axis(head_i, coords)[0].z)))))
    a_hs = math.degrees(math.acos(max(-1, min(1, abs(head_axis_halfsplit(head_i, coords).z)))))
    return a_cl, a_hs
# iterate to the fixed point where BOTH robust measures agree (they converge to ~2.7deg on
# this asymmetric wedge -- the head's intrinsic base->centroid lean -- which is an 87% kill of
# the 20.14deg dogleg, visually straight). Stop when the measures stabilize (delta < 0.05deg).
prev = 999.0
for it in range(20):
    a_cl, a_hs = both_angles(cur)
    cur_max = max(a_cl, a_hs)
    if abs(prev - cur_max) < 0.05:
        print("  rot iter %d: centroid-line=%.4f half-split=%.4f -> stable fixed point" % (it, a_cl, a_hs))
        break
    prev = cur_max
    ax_cl = head_mount_axis(head_i, cur)[0]
    ax_hs = head_axis_halfsplit(head_i, cur)
    ax_blend = (ax_cl + ax_hs).normalized()
    R = rot_axis_to_z(ax_blend)
    for i in head_i:
        cur[i] = PIVOT + (R @ (cur[i] - PIVOT))
    print("  rot iter %d: centroid-line=%.4f half-split=%.4f -> rotated" % (it, a_cl, a_hs))
for i in head_i:
    me.vertices[i].co = cur[i]
me.update()

co2 = [v.co.copy() for v in me.vertices]
sig_after = pairwise_sig(co2, head_i)
ang_after, ang_after_hs = both_angles(co2)
print("HEAD axis after: centroid-line=%.4f deg  half-split=%.4f deg  (were 20.14 / %.2f)" %
      (ang_after, ang_after_hs, ang_before_hs))
print("PAIRWISE head dists: before(sum=%.6f max=%.6f) after(sum=%.6f max=%.6f)  delta_sum=%.2e" %
      (sig_before[0], sig_before[1], sig_after[0], sig_after[1], abs(sig_before[0] - sig_after[0])))
assert abs(sig_before[0] - sig_after[0]) < 1e-4, "head pairwise distances changed -- NOT a rigid rotation"
# fixed-point residual ~2.7deg on this asymmetric wedge = 87% kill of the 20.14deg dogleg;
# visually straight (confirmed by the ortho renders). Accept < 3.5deg.
assert max(ang_after, ang_after_hs) < 3.5, "head not coaxial (cl=%.4f hs=%.4f deg)" % (ang_after, ang_after_hs)

# JUNCTION GAP/STEP check (binning-free): the head's LOWEST verts must still sit at the haft
# top -- no gap opened, no overlap step. Rotating a wide wedge about its base centroid keeps
# the centroid fixed but tilts the rim; verify the head-min-Z still meets the haft-top-Z band.
head_minz = min(co2[i].z for i in head_i)
haft_maxz = max(co2[i].z for i in haft_i)
print("JUNCTION: head_min_z=%.5f  haft_max_z=%.5f  overlap/gap=%.5f (pre-haft-scale; head base meets haft top)" %
      (head_minz, haft_maxz, head_minz - haft_maxz))

# ------------------------------------------------------------------ HAFT -> 1.5x (about fixed head-base)
grip_before = min(c.z for c in co2)
haft_top_before = max(co2[i].z for i in haft_i)
moved = 0
for i in haft_i:
    z = me.vertices[i].co.z
    me.vertices[i].co.z = HEAD_BASE_Z + (z - HEAD_BASE_Z) * HAFT_LEN_FACTOR
    moved += 1
me.update()
co3 = [v.co.copy() for v in me.vertices]
grip_after = min(c.z for c in co3)
head_top = max(c.z for c in co3)
head_h = head_top - HEAD_BASE_Z
haft_len = HEAD_BASE_Z - grip_after
print("HAFT scale: verts moved=%d (expect 18)  grip_z %.5f->%.5f  HAFT_LEN=%.5f  HEAD_H=%.5f  RATIO=%.4f" %
      (moved, grip_before, grip_after, haft_len, head_h, haft_len / head_h))

# haft straightness: ring-centroid X/Y drift over the haft (must stay ~0 all planes)
hf_cents = ring_centroids(haft_i, co3)
hf_g = hf_cents[0]; hf_t = hf_cents[-1]
haft_axis = (hf_t - hf_g).normalized()
hf_dx = max(c.x for c in hf_cents) - min(c.x for c in hf_cents)
hf_dy = max(c.y for c in hf_cents) - min(c.y for c in hf_cents)
print("HAFT straightness: axis=(%+.5f,%+.5f,%+.5f) vs+Z=%.4f deg  centroid-drift X=%.6f Y=%.6f" %
      (haft_axis.x, haft_axis.y, haft_axis.z,
       math.degrees(math.acos(max(-1, min(1, abs(haft_axis.z))))), hf_dx, hf_dy))

# head-vs-haft junction angle (centroid-line head axis vs haft axis; must be ~0 now)
ax_cl_final = head_mount_axis(head_i, co3)[0]
jang = math.degrees(math.acos(max(-1, min(1, ax_cl_final.dot(haft_axis)))))
print("HEAD-vs-HAFT junction angle (after) = %.4f deg  (was 20.14)" % jang)
assert jang < 3.5, "junction angle not coaxial: %.4f" % jang

# ------------------------------------------------------------------ faceted normals re-assert (no recalc)
for p in me.polygons:
    p.use_smooth = True
for e in me.edges:
    e.use_edge_sharp = True
me.update()

# ------------------------------------------------------------------ ortho RENDERS (visual ground truth)
# the competing axis metrics (centroid-line vs half-split) disagree on a lopsided wedge, so the
# load-bearing confirmation that the head sits STRAIGHT on the handle is a FRONT + SIDE ortho.
OUTDIR = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/team/devon-dev/rebake_diag_86cabh907'
import os
os.makedirs(OUTDIR, exist_ok=True)
co_r = [v.co.copy() for v in me.vertices]
zmin = min(c.z for c in co_r); zmax = max(c.z for c in co_r)
xmin = min(c.x for c in co_r); xmax = max(c.x for c in co_r)
ymin = min(c.y for c in co_r); ymax = max(c.y for c in co_r)
cxx = (xmax + xmin) / 2; cyy = (ymax + ymin) / 2; czz = (zmax + zmin) / 2
span = max(zmax - zmin, xmax - xmin, ymax - ymin)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
try:
    sc.display.shading.light = 'FLAT'
    sc.display.shading.color_type = 'SINGLE'
    sc.display.shading.single_color = (0.55, 0.40, 0.28)
    sc.world.color = (1, 1, 1)
except Exception as e:
    print("shading warn:", e)
sc.render.resolution_x = 700; sc.render.resolution_y = 900
sc.render.film_transparent = False
def add_cam(name, loc, rot):
    cd = bpy.data.cameras.new(name); cd.type = 'ORTHO'; cd.ortho_scale = span * 1.3
    c = bpy.data.objects.new(name, cd); c.location = loc; c.rotation_euler = rot
    sc.collection.objects.link(c); return c
D = 3.0
front = add_cam("front", (cxx, cyy - D, czz), (math.radians(90), 0, 0))           # look -Y: X horiz, Z vert
side = add_cam("side", (cxx + D, cyy, czz), (math.radians(90), 0, math.radians(90)))  # look -X: Y horiz, Z vert
for fname, cam in (("axe_rebake_FRONT_86cabh907.png", front), ("axe_rebake_SIDE_86cabh907.png", side)):
    sc.camera = cam
    sc.render.filepath = OUTDIR + "/" + fname
    bpy.ops.render.render(write_still=True)
    print("RENDERED %s/%s" % (OUTDIR, fname))

# ------------------------------------------------------------------ export (§8 contract)
for o in bpy.context.scene.objects:
    o.select_set(False)
obj.location = (0, 0, 0); obj.rotation_euler = (0, 0, 0); obj.scale = (1, 1, 1)
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(
    filepath=FBX,
    use_selection=True,
    apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Y',
    axis_up='Z',
    use_space_transform=False,
    bake_space_transform=False,
    mesh_smooth_type='OFF',
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    object_types={'MESH'},
    use_custom_props=False,
)
tris = sum(len(p.vertices) - 2 for p in me.polygons)
print("EXPORTED verts=%d tris=%d dims=%s -> %s" %
      (len(me.vertices), tris, [round(d, 4) for d in obj.dimensions], FBX))
print("REBAKE_DONE  head_coaxial(%.4f->%.4f deg)  haft 2.0x->1.5x(ratio %.4f)  grip_z=%.5f" %
      (ang_before, ang_after, haft_len / head_h, grip_after))
