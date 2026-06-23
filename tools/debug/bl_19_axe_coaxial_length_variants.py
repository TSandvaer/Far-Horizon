"""
bl_19_axe_coaxial_length_variants.py — 86cabh907 SHAFT-LENGTH PICKER bake (supersedes bl_18).

WHY: bl_18 left a 2.71deg residual because it zeroed the head's CENTROID-LINE (base-ring ->
top-ring centroid) pivoting about the HEAD-BASE centroid. That axis only measures the head's
internal stack lean (near-straight); it does NOT capture the visible dogleg. The FRONT ortho
render of the committed FBX shows the head's MASS swung off to the +X side of the haft — the
"still bend before the head" the Sponsor sees. Dry-run candidate renders (team/devon-dev/
coaxial_diag_86cabh907/cand_*.png) proved the coaxial read = the HAFT-TOP -> HEAD-CENTROID
"mount line" aligned to +Z, pivoting about the HAFT-TOP junction point: that BOTH re-centers
the head over the haft AND uprights it (cand_1mountline.png is balanced/straight; the current,
back-edge, and blend candidates all dogleg). Mount-line -> +Z is what this bake applies.

This produces FOUR shaft-length variants for the in-hand picker (head LOCKED + coaxial, haft
straight): 1.1x / 1.2x / 1.3x / 1.4x of the ORIGINAL SHORT haft. (The committed FBX haft is
1.5x; we divide by 1.5 to recover the original, then scale each variant about the FIXED
head-base junction.) The answer is between the rejected stubby 1.06x and the too-long 1.5x.

RIGID-ROTATION GUARANTEE: the head is rotated as a rigid body about the haft-top pivot —
pairwise head-vert distances invariant (asserted < 1e-4), so head SHAPE + SIZE + material are
byte-identical-modulo-rotation. NOT a reshape; preserves the 0.65x head-lock. Per the §9
rotation corollary, the head's long-axis Z-PROJECTION changes under rotation, so the Unity
import-normalize constants (HeroAxeTargetHeadHeightU / HeroAxeHeadHeightFromTipU) MUST be
re-derived from the printed post-rotation head-height-from-tip (this script prints it).

PRESERVED: grip origin (mesh z-min grip end, obj origin (0,0,0)), single WeaponPalette material
slot, faceted per-face normals (NO RecalculateNormals — load-bearing for flat-shaded URP Cull
Back), §8 FBX export (-Y Fwd / Z Up / FBX Unit Scale / Normals Only), 128 verts / 236 tris.

OUTPUTS (overwrites): four FBX in Assets/Art/Props/WeaponPack/:
  wpn_axe_01_len11.fbx  wpn_axe_01_len12.fbx  wpn_axe_01_len13.fbx  wpn_axe_01_len14.fbx
plus FRONT ortho renders in team/devon-dev/coaxial_diag_86cabh907/variant_len1X.png.
The DEFAULT shipped wpn_axe_01.fbx is left UNTOUCHED by this script (the picker swaps to the
variant meshes; the Sponsor picks one, then a FINAL bake writes the chosen length into
wpn_axe_01.fbx).

Run (from a CLEAN-committed wpn_axe_01.fbx — re-imports fresh, does NOT mutate the source):
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    --python tools/debug/bl_19_axe_coaxial_length_variants.py
"""
import bpy, math, collections, os
import numpy as np
from mathutils import Vector, Matrix

DIR = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack'
SRC = DIR + r'/wpn_axe_01.fbx'
OUT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/team/devon-dev/coaxial_diag_86cabh907'
HEAD_BASE_Z = 0.022674          # fixed head-base junction (bl_17 / commit 18bec7c); INVARIANT
CURRENT_HAFT_FACTOR = 1.5       # the committed FBX haft is 1.5x the original short haft
VARIANTS = [1.1, 1.2, 1.3, 1.4] # of the ORIGINAL short haft
TARGET = Vector((0, 0, 1))
os.makedirs(OUT, exist_ok=True)

# ----------------------------------------------------------------- import fresh
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = obj; obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = obj.data
co = [v.co.copy() for v in me.vertices]
n = len(co)
assert len(me.materials) == 1 and me.materials[0].name == 'WeaponPalette', \
    "expected single WeaponPalette slot, got %s" % [m.name if m else None for m in me.materials]
head_i = [i for i in range(n) if co[i].z > HEAD_BASE_Z + 1e-9]
haft_i = [i for i in range(n) if co[i].z <= HEAD_BASE_Z + 1e-9]
print("IMPORTED verts=%d N_HEAD=%d N_HAFT=%d" % (n, len(head_i), len(haft_i)))
assert len(head_i) == 110 and len(haft_i) == 18, "head/haft split drifted -- abort"

def ring_centroids(idx, coords):
    r = collections.defaultdict(list)
    for i in idx: r[round(coords[i].z, 3)].append(coords[i])
    return [Vector((sum(p.x for p in r[z])/len(r[z]), sum(p.y for p in r[z])/len(r[z]), z)) for z in sorted(r)]
def head_centroid(coords):
    return Vector((sum(coords[i].x for i in head_i)/len(head_i),
                   sum(coords[i].y for i in head_i)/len(head_i),
                   sum(coords[i].z for i in head_i)/len(head_i)))
def ang_z(ax):
    return math.degrees(math.acos(max(-1, min(1, abs(ax.normalized().z)))))
def rot_to_z(axis):
    axis = axis.normalized(); v = axis.cross(TARGET); s = v.length; c = axis.dot(TARGET)
    if s < 1e-12: return Matrix.Identity(3)
    return Matrix.Rotation(math.atan2(s, c), 3, v.normalized())

# ----------------------------------------------------------------- pairwise rigidity baseline
def pairwise_sum(coords):
    s = 0.0
    for a in range(0, len(head_i), 5):
        for b in range(a+1, len(head_i), 3):
            s += (coords[head_i[a]] - coords[head_i[b]]).length
    return s
sig_before = pairwise_sum(co)

# ----------------------------------------------------------------- RIGID rotate: mount-line -> +Z about haft-top
P = ring_centroids(haft_i, co)[-1].copy()   # haft-top junction point (the pivot)
ang0 = ang_z((head_centroid(co) - P))
cur = [c.copy() for c in co]
for it in range(20):
    axM = (head_centroid(cur) - P).normalized()
    a = ang_z(axM)
    if a < 0.05:
        print("  mount-line converged iter %d (%.4f deg)" % (it, a)); break
    R = rot_to_z(axM)
    for i in head_i: cur[i] = P + (R @ (cur[i] - P))
for i in head_i: me.vertices[i].co = cur[i]
me.update()
co = [v.co.copy() for v in me.vertices]

sig_after = pairwise_sum(co)
print("RIGIDITY: head pairwise-dist sum before=%.6f after=%.6f delta=%.2e" %
      (sig_before, sig_after, abs(sig_before - sig_after)))
assert abs(sig_before - sig_after) < 1e-4, "head pairwise distances changed -- NOT rigid"

# junction angle: head mount-line vs haft +Z (MUST be <=0.5 now)
haft_ax = (ring_centroids(haft_i, co)[-1] - ring_centroids(haft_i, co)[0]).normalized()
mount_now = (head_centroid(co) - P).normalized()
jang = math.degrees(math.acos(max(-1, min(1, abs(mount_now.dot(haft_ax))))))
hc = head_centroid(co); ctr_off = math.hypot(hc.x - P.x, hc.y - P.y)
print("COAXIAL: mount-line vs +Z before=%.3f after=%.4f deg  (haft vs+Z=%.4f)  head-ctr lateral offset=%.5f" %
      (ang0, ang_z(mount_now), ang_z(haft_ax), ctr_off))
assert jang <= 0.5, "head-vs-haft junction NOT coaxial: %.4f deg (need <=0.5)" % jang
print("COAXIAL OK: head-vs-haft junction angle = %.4f deg (<=0.5)" % jang)

# ----------------------------------------------------------------- head-height-from-tip (re-derive const)
# After uprighting: blade TIP is the head's max-z; head-base junction is HEAD_BASE_Z. The
# importer reads head verts within HeroAxeHeadHeightFromTipU of the tip. Print so the Unity
# const is re-derived (§9 rotation corollary).
head_z = [co[i].z for i in head_i]
tip_z = max(head_z); head_base_z = min(head_z)
head_h_from_tip = tip_z - HEAD_BASE_Z   # tip down to the fixed junction
head_span = tip_z - head_base_z
print("HEAD-HEIGHT (post-coaxial): tip_z=%.5f head_base_z=%.5f HEAD_BASE_Z(junction)=%.5f" %
      (tip_z, head_base_z, HEAD_BASE_Z))
print("HEAD_HEIGHT_FROM_TIP=%.5f  HEAD_SPAN(base..tip)=%.5f" % (head_h_from_tip, head_span))

# re-assert faceted normals
for p in me.polygons: p.use_smooth = True
for e in me.edges: e.use_edge_sharp = True
me.update()

# snapshot the COAXIAL head + original-length geometry as the base for variants
coax = [v.co.copy() for v in me.vertices]
# recover ORIGINAL short haft: current haft is 1.5x -> divide haft long-axis about junction by 1.5
orig = [c.copy() for c in coax]
for i in haft_i:
    z = coax[i].z
    orig[i] = Vector((coax[i].x, coax[i].y, HEAD_BASE_Z + (z - HEAD_BASE_Z) / CURRENT_HAFT_FACTOR))
orig_grip = min(orig[i].z for i in haft_i)
orig_haft_len = HEAD_BASE_Z - orig_grip
head_h = tip_z - HEAD_BASE_Z
print("ORIGINAL short haft recovered: grip_z=%.5f HAFT_LEN0=%.5f HEAD_H=%.5f RATIO0=%.4f" %
      (orig_grip, orig_haft_len, head_h, orig_haft_len / head_h))

# ----------------------------------------------------------------- render + export each variant
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'SINGLE'
sc.display.shading.single_color = (0.55, 0.40, 0.28)
if sc.world is None: sc.world = bpy.data.worlds.new("W")
sc.world.color = (1, 1, 1)
sc.render.resolution_x = 460; sc.render.resolution_y = 820

def render_front(coords, fname):
    for i in range(n): me.vertices[i].co = coords[i]
    me.update()
    for p in me.polygons: p.use_smooth = True
    allz = [c.z for c in coords]; allx = [c.x for c in coords]; ally = [c.y for c in coords]
    cx = (max(allx)+min(allx))/2; cy = (max(ally)+min(ally))/2; cz = (max(allz)+min(allz))/2
    span = max(allz)-min(allz)
    for c in list(sc.collection.objects):
        if c.type == 'CAMERA': bpy.data.objects.remove(c, do_unlink=True)
    cd = bpy.data.cameras.new("f"); cd.type='ORTHO'; cd.ortho_scale = span*1.2
    cam = bpy.data.objects.new("f", cd); cam.location=(cx, cy-3, cz); cam.rotation_euler=(math.radians(90),0,0)
    sc.collection.objects.link(cam); sc.camera = cam
    sc.render.filepath = OUT + "/" + fname
    bpy.ops.render.render(write_still=True)

EXPORT_KW = dict(use_selection=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Y',
                 axis_up='Z', use_space_transform=False, bake_space_transform=False,
                 mesh_smooth_type='OFF', use_mesh_modifiers=True, add_leaf_bones=False,
                 object_types={'MESH'}, use_custom_props=False)

results = []
for f in VARIANTS:
    variant = [c.copy() for c in orig]
    for i in haft_i:
        z = orig[i].z
        variant[i] = Vector((orig[i].x, orig[i].y, HEAD_BASE_Z + (z - HEAD_BASE_Z) * f))
    grip_z = min(variant[i].z for i in haft_i)
    haft_len = HEAD_BASE_Z - grip_z
    ratio = haft_len / head_h
    # head untouched check
    head_top_now = max(variant[i].z for i in head_i)
    assert abs(head_top_now - tip_z) < 1e-6, "head moved during haft scale -- abort"
    tag = ("%.1f" % f).replace(".", "")  # 1.1 -> 11
    # render FRONT for eye confirmation
    render_front(variant, "variant_len%s.png" % tag)
    # export FBX
    for i in range(n): me.vertices[i].co = variant[i]
    me.update()
    for p in me.polygons: p.use_smooth = True
    for o in bpy.context.scene.objects: o.select_set(False)
    obj.location=(0,0,0); obj.rotation_euler=(0,0,0); obj.scale=(1,1,1)
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    fbx_out = DIR + "/wpn_axe_01_len%s.fbx" % tag
    bpy.ops.export_scene.fbx(filepath=fbx_out, **EXPORT_KW)
    tris = sum(len(p.vertices)-2 for p in me.polygons)
    results.append((f, haft_len, ratio, grip_z, fbx_out, tris))
    print("VARIANT %.1fx: HAFT_LEN=%.5f RATIO=%.4f grip_z=%.5f tris=%d -> %s" %
          (f, haft_len, ratio, grip_z, tris, os.path.basename(fbx_out)))

print("\n=== BL_19 SUMMARY (86cabh907) ===")
print("coaxial junction angle = %.4f deg (<=0.5)" % jang)
print("HEAD locked: tip_z=%.5f head_base junction=%.5f HEAD_H=%.5f" % (tip_z, HEAD_BASE_Z, head_h))
print("re-derive Unity consts -> HeroAxeHeadHeightFromTipU=%.5f ; head world-height invariant (rotation corollary)" % head_h_from_tip)
for f, hl, r, gz, path, tris in results:
    print("  %.1fx  haft:head=%.4f  haft_len=%.5f  -> %s" % (f, r, hl, os.path.basename(path)))
print("BL_19_DONE")
