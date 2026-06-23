"""
bl_17_axe_head_bake_065.py — BAKE the stone axe HEAD at a UNIFORM scale factor
(default 0.65x, Sponsor's pick) DIRECTLY into wpn_axe_01.fbx, OFFLINE — because the
runtime head-resize dial is broken across all inputs (86cabh907; keys/mouse/buttons
all no-op for the Sponsor). The smaller head is baked into the FBX so it is
guaranteed + verifiable in the CI capture.

CONTRACT (matches HeldWeaponCycleDebug.ResolveAxeHead/ApplyAxeHead EXACTLY so the
baked head == what dial-factor 0.65 would have produced, and dial default then = 1.0):
  - SOURCE = the restored 4208067 stone wedge FBX (byte-identical; sha256 verified
    by the dispatch). The head shape + knapped-flint WeaponPalette material are
    preserved EXACTLY — this is a PURE UNIFORM SCALE, never a re-author.
  - Long axis = the mesh's longest bounds axis (Z for this mesh; span ~1.146),
    exactly as the dial picks it (ext.x/y/z max).
  - Junction coord = boundsMin[longAxis] + JUNCTION_FRACTION * span, with
    JUNCTION_FRACTION = 0.50 (the dial's headJunctionFraction default; the 0.40..0.55
    plateau cuts the same clean head wedge per the dial tooltip).
  - HEAD = EVERY vert strictly ABOVE junctionCoord along the long axis (NOT a
    component/subset test — the dial scales the whole above-junction set; matching it
    is what makes dial-factor-1.0==this-baked-head true).
  - Pivot = bounds CENTRE on the off-long axes, junctionCoord on the long axis (the
    haft centreline at the junction) — identical to the dial's _axeHeadPivot.
  - Scale = Vector3(f,f,f) uniform about the pivot: v = pivot + (v - pivot) * f.
    Uniform => face planarity + per-face normal DIRECTION preserved (faceted look +
    URP Cull Back intact); we do NOT recalc normals (lowpoly-quality.md §1).
  - The haft, lashing/eye band, grip-point origin (mesh z=0 grip end / obj origin
    (0,0,0)), and +Z forward axis are UNCHANGED (no vert at/below the junction moves).

Re-exports the FBX with the bl_11/bl_14/bl_15 contract (-Y Fwd / Z Up / FBX Unit
Scale / Normals Only / single WeaponPalette slot). Loads the FBX (the source of
truth per the dispatch), NOT the .blend (whose head may diverge from the restored
FBX). Prints head width BEFORE/AFTER + haft-unchanged proof for verification.

Reproducible / re-runnable at a different factor:
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    --python tools/debug/bl_17_axe_head_bake_065.py -- 0.65
(the trailing number after `--` overrides HEAD_FACTOR; default 0.65)

Idempotent w.r.t. the COMMITTED restored FBX: it re-imports the restored FBX fresh
every run, so re-running does NOT compound (unlike bl_14). It overwrites the FBX with
the freshly-baked head. To re-bake at a new factor, FIRST `git checkout` the restored
FBX (or pass a factor relative to the restored head), then run.
"""
import bpy, sys, math
from mathutils import Vector

FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
JUNCTION_FRACTION = 0.50    # == HeldWeaponCycleDebug.headJunctionFraction default

# factor: default 0.65 (Sponsor's pick), overridable via `-- <factor>`
HEAD_FACTOR = 0.65
if "--" in sys.argv:
    extra = sys.argv[sys.argv.index("--") + 1:]
    if extra:
        HEAD_FACTOR = float(extra[0])
print("HEAD_FACTOR = %.4f  JUNCTION_FRACTION = %.4f" % (HEAD_FACTOR, JUNCTION_FRACTION))

# --- import the restored FBX fresh (NOT the .blend) -------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1, "expected exactly 1 mesh in wpn_axe_01.fbx, got %d" % len(meshes)
obj = meshes[0]
me = obj.data
n = len(me.vertices)
print("IMPORTED %s: verts=%d faces=%d" % (obj.name, n, len(me.polygons)))

# preserve the single WeaponPalette material slot (must survive the bake untouched)
mat_names_before = [m.name if m else None for m in me.materials]
print("MATERIAL SLOTS (before):", mat_names_before)
assert len(me.materials) == 1, "expected exactly 1 material slot (WeaponPalette), got %d" % len(me.materials)

# --- replicate the dial's junction + head-vert selection EXACTLY ------------
co = [v.co.copy() for v in me.vertices]
bmin = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
bmax = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
ext = (bmax - bmin) * 0.5
ctr = (bmax + bmin) * 0.5
# longAxis = argmax extent, with the dial's tie-break (x>=y>=z order)
if ext.x >= ext.y and ext.x >= ext.z:
    long_axis = 0
elif ext.y >= ext.z:
    long_axis = 1
else:
    long_axis = 2
axis_name = "XYZ"[long_axis]
haft_min = bmin[long_axis]; haft_max = bmax[long_axis]
haft_span = max(1e-4, haft_max - haft_min)
junction_coord = haft_min + haft_span * JUNCTION_FRACTION
print("LONG AXIS = %s (ext=%s)  span=%.4f  junctionCoord=%.4f (frac %.2f)" %
      (axis_name, [round(e, 4) for e in ext], haft_span, junction_coord, JUNCTION_FRACTION))

def comp(v, a):
    return v.x if a == 0 else (v.y if a == 1 else v.z)

head_idx = [i for i in range(n) if comp(co[i], long_axis) > junction_coord]
print("HEAD verts above junction: %d / %d total (haft/lash/grip = %d below or at)" %
      (len(head_idx), n, n - len(head_idx)))
assert len(head_idx) > 0, "no head verts selected — junction mis-set"

# pivot = bounds centre on off-long axes, junctionCoord on the long axis (haft centreline)
pivot = ctr.copy()
pivot[long_axis] = junction_coord
print("PIVOT (haft centreline @ junction):", [round(c, 4) for c in pivot])

# --- measure HEAD WIDTH (the verification metric) BEFORE --------------------
# Head width = X-span over the HEAD verts (the fanned blade-to-poll spread). Use the
# same head-vert set before/after so the metric is apples-to-apples.
def head_width(coords):
    xs = [coords[i].x for i in head_idx]
    return max(xs) - min(xs)
def head_bounds(coords):
    xs = [coords[i].x for i in head_idx]; zs = [coords[i].z for i in head_idx]
    return (min(xs), max(xs), min(zs), max(zs))
w_before = head_width(co)
hb_before = head_bounds(co)
# haft proof: bounds of the NON-head (at/below junction) verts must be byte-identical after
haft_idx = [i for i in range(n) if i not in set(head_idx)]
def haft_bounds(coords):
    xs = [coords[i].x for i in haft_idx]; ys = [coords[i].y for i in haft_idx]; zs = [coords[i].z for i in haft_idx]
    return (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))
haft_before = haft_bounds(co)
grip_z_before = min(c.z for c in co)   # grip end (mesh z-min) — must stay identical

# --- THE BAKE: uniform scale of the whole head about the pivot --------------
new_co = [c.copy() for c in co]
for i in head_idx:
    new_co[i] = pivot + (co[i] - pivot) * HEAD_FACTOR   # uniform x==y==z (Vector3.one * f)

# write back
for i in range(n):
    me.vertices[i].co = new_co[i]
me.update()

# --- measure AFTER + prove haft unchanged -----------------------------------
w_after = head_width(new_co)
hb_after = head_bounds(new_co)
haft_after = haft_bounds(new_co)
grip_z_after = min(c.z for c in new_co)

print("HEAD WIDTH (X-span over head verts):  BEFORE=%.4f  AFTER=%.4f  ratio=%.4f (expect %.4f)" %
      (w_before, w_after, w_after / w_before, HEAD_FACTOR))
print("HEAD BOUNDS BEFORE: X[%.4f..%.4f] Z[%.4f..%.4f]" % hb_before)
print("HEAD BOUNDS AFTER : X[%.4f..%.4f] Z[%.4f..%.4f]" % hb_after)
print("HAFT/LASH/GRIP bounds BEFORE: X[%.4f..%.4f] Y[%.4f..%.4f] Z[%.4f..%.4f]" % haft_before)
print("HAFT/LASH/GRIP bounds AFTER : X[%.4f..%.4f] Y[%.4f..%.4f] Z[%.4f..%.4f]" % haft_after)
haft_unchanged = all(abs(a - b) < 1e-6 for a, b in zip(haft_before, haft_after))
print("HAFT UNCHANGED: %s" % ("YES" if haft_unchanged else "NO — ERROR"))
print("GRIP END Z (origin): BEFORE=%.6f  AFTER=%.6f  unchanged=%s" %
      (grip_z_before, grip_z_after, "YES" if abs(grip_z_before - grip_z_after) < 1e-6 else "NO — ERROR"))
assert haft_unchanged, "haft moved — bake is wrong"
assert abs(grip_z_before - grip_z_after) < 1e-6, "grip origin moved — bake is wrong"

# faceted shading + outward normals re-assert (a uniform scale preserves them, but
# cheap + safe; we do NOT RecalculateNormals — bake-preserved per-face normals are
# load-bearing for the flat-shaded look + URP Cull Back, lowpoly-quality.md §1).
for p in me.polygons:
    p.use_smooth = True
for e in me.edges:
    e.use_edge_sharp = True
me.update()

# material slot unchanged
mat_names_after = [m.name if m else None for m in me.materials]
print("MATERIAL SLOTS (after):", mat_names_after)
assert mat_names_after == mat_names_before, "material slot changed — ERROR"

# --- re-export FBX (same contract as bl_11/bl_14/bl_15) ---------------------
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
print("EXPORTED wpn_axe_01: verts=%d tris=%d dims=%s -> %s" %
      (len(me.vertices), tris, [round(v, 3) for v in obj.dimensions], FBX))
print("AXE_HEAD_BAKE_DONE factor=%.4f head_width %.4f->%.4f" % (HEAD_FACTOR, w_before, w_after))
