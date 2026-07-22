# Castaway v4 RIGHT-HAND source fix — ticket 86cau4za2 (Option A, Blender source edit; NO Mixamo re-rig).
#
# Fixes TWO measured Mixamo auto-rig defects on the RIGHT hand of castaway_v4_rigged.fbx, at source, by making
# the right-hand bone subtree an EXACT mirror of the Sponsor-ACCEPTED left hand (geometry is a proven 0.000mm
# vertex mirror, so the mirror is well-defined):
#   (a) BONE ROLL: the RightHand subtree bind frame was roll-flipped ~176.4 deg off a clean mirror of the left
#       (twist SAME-sign as left instead of mirror-opposite). Fix: mirror each right-hand-subtree edit bone
#       (head/tail negate-X + roll negate) from the corresponding left bone -> armature-space asym 176.4->0.0 deg,
#       twist signs now mirror-opposite (the v3-healthy pattern).
#   (b) SKIN WEIGHTS: the right thumb GEOMETRY was skinned to the INDEX chain (thumb-chain mean weight 0.000 vs
#       0.919 left) -> "a block with a thumb". Fix: mirror the ENTIRE left hand-region per-vertex weights onto the
#       geometrically-mirrored right verts (bone name Left<->Right flip), clearing the wrong weights first. The
#       righthandthumb3 bone EXISTS (Tess correction on 86cau4za2) so weights are simply reassigned — no bone add.
#   LEFT HAND IS READ-ONLY (AC4): only right-side verts/bones are written.
#
# Export settings reproduce the Mixamo convention Unity digests (bakeAxisConversion=0, normalImportMode=Import):
# proven byte-faithful for the un-edited parts (bones 0.000deg rest delta, LEFT census identical, loop-normals
# 0.000deg index-wise). mixamorig: bone-name prefix + 41-bone hierarchy preserved so the 18 without-skin clips
# still bind by transform path.
#
# Run (headless):
#   "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background --python castaway_v4_righthand_fix.py \
#       -- <in.fbx> <out.fbx>
# Verify in Unity: CastawayV4DefectDiag.Run (ReportRound9 AC1/2/3/5 + ReportRound10 re-derived wrist/thumb seeds).

import bpy, math, sys
from mathutils import Vector, Matrix, kdtree

ORIG = sys.argv[-2]
OUT  = sys.argv[-1]

def tok(n):
    n2 = n.lower(); c = n2.rfind(':'); return n2[c+1:] if c >= 0 else n2
def flipLR(name):
    if 'Left' in name:  return name.replace('Left', 'Right')
    if 'Right' in name: return name.replace('Right', 'Left')
    return name

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=ORIG, automatic_bone_orientation=False, ignore_leaf_bones=False)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
mesh = next(o for o in bpy.data.objects if o.type == 'MESH')
bpy.context.view_layer.update()

RIGHT_SUBTREE = ["righthand", "righthandthumb1", "righthandthumb2", "righthandthumb3", "righthandthumb4",
                 "righthandindex1", "righthandindex2", "righthandindex3", "righthandindex4"]

# ---- (a) ROLL FIX: mirror right-hand-subtree edit bones from the left (X-mirror: head/tail negate-X, roll negate) ----
win = bpy.context.window_manager.windows[0]
scr = win.screen
area = next((a for a in scr.areas if a.type == 'VIEW_3D'), scr.areas[0])
region = next((r for r in area.regions if r.type == 'WINDOW'), area.regions[0])
with bpy.context.temp_override(window=win, area=area, region=region, active_object=arm, selected_objects=[arm]):
    bpy.ops.object.mode_set(mode='EDIT')
    byname = {tok(b.name): b for b in arm.data.edit_bones}
    for rt in RIGHT_SUBTREE:
        rb, lb = byname.get(rt), byname.get(flipLR(rt))
        if rb is None or lb is None:
            print(f"  ROLL SKIP: {rt} or its mirror missing"); continue
        rb.head = Vector((-lb.head.x, lb.head.y, lb.head.z))
        rb.tail = Vector((-lb.tail.x, lb.tail.y, lb.tail.z))
        rb.roll = -lb.roll
    bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()

# ---- (b) WEIGHT FIX: mirror the entire left hand-region weights onto the geometrically-mirrored right verts ----
me = mesh.data
vg = mesh.vertex_groups
gname_by_index = {g.index: g.name for g in vg}
left_hand_gidx = set(g.index for g in vg if tok(g.name).startswith("lefthand"))
left_verts = [v.index for v in me.vertices
              if any(g.group in left_hand_gidx and g.weight > 0.001 for g in v.groups)]

co = [v.co.copy() for v in me.vertices]
kd = kdtree.KDTree(len(co))
for i, p in enumerate(co): kd.insert(p, i)
kd.balance()

# READ left weights before WRITE (never mutate the left)
left_weights = {lv: [(gname_by_index[g.group], g.weight) for g in me.vertices[lv].groups if g.weight > 0.0]
                for lv in left_verts}
for lv, ws in left_weights.items():
    for gname, _ in ws:
        if vg.get(flipLR(gname)) is None: vg.new(name=flipLR(gname))

max_match = 0.0
for lv in left_verts:
    _, rv, dist = kd.find(Vector((-co[lv].x, co[lv].y, co[lv].z)))
    max_match = max(max_match, dist)
    for gi in [g.group for g in me.vertices[rv].groups]:
        vg[gi].remove([rv])
    for gname, w in left_weights[lv]:
        vg[flipLR(gname)].add([rv], w, 'REPLACE')
print(f"reassigned {len(left_verts)} right hand-region verts; mirror-match max {max_match*1000:.4f}mm")

# ---- EXPORT (Mixamo-convention faithful round-trip) ----
for o in bpy.data.objects: o.select_set(True)
bpy.context.view_layer.objects.active = mesh
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_NONE',
    axis_forward='-Z', axis_up='Y', bake_space_transform=False, use_armature_deform_only=False,
    add_leaf_bones=False, bake_anim=False, object_types={'ARMATURE', 'MESH'}, mesh_smooth_type='FACE')
print(f"EXPORTED -> {OUT}")
