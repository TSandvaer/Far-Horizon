"""
bl_15_axe_head_clean.py — REPLACE the chipped/distorted axe head with a CLEAN head
that is a distinct rigid SUB-OBJECT, then scale the WHOLE head UNIFORMLY about the
head<->haft junction (ticket 86cabh907, Sponsor blocker: "everytime you make the
axe head smaller it looks worse. its like youre chipping off the axe head instead
of just resizing it").

ROOT CAUSE of the "chipping": bl_14 (and the runtime ;/' dial) classify a SUBSET of
verts (off-centreline "blade" verts in the upper haft) and scale THAT subset toward
a pivot. The head-base/junction verts that DON'T pass the subset test stay put while
the classified verts pull inward -> the head is SQUISHED/FLATTENED into a sliver
(the poll +X side was crushed from the original +0.34 down to +0.107 in the
committed source). That directional, subset, non-uniform scale IS the chipping.

THE FIX (this script + the runtime rewrite):
  - The axe HEAD is rebuilt CLEAN from the original bl_02 profile (un-distorted
    biface, bit -X / poll +X) as its OWN mesh island, cut cleanly at the head<->haft
    junction. The whole blade/head is ONE rigid unit, NOT a vertex subset.
  - The head is scaled UNIFORMLY (x==y==z, one factor) about the junction point on
    the haft centreline at the head base. Shrinking keeps the head's full
    shape/proportions (a coherent whole-head resize), never a directional squish.
  - The DEFAULT is a CLEAN head at a sensible size (HEAD_DEFAULT_FACTOR uniform of
    the original full-size head). The Sponsor dials it DOWN further by eye (the
    runtime dial is ALSO uniform now), so it never chips again.
  - The haft (42 verts), the lashing/eye band (24 verts), the grip-point origin
    (mesh z=0 grip end / obj origin), and the +Z forward axis are UNCHANGED. The
    STUMP axe shares this FBX so it inherits the clean head automatically.
  - Re-exports Assets/Art/Props/WeaponPack/wpn_axe_01.fbx with the SAME contract as
    bl_11/bl_14 (-Y Forward / Z Up / FBX Unit Scale / Normals Only / single
    WeaponPalette slot).

The script ALSO writes the junction Z + head bounds to stdout so the runtime dial
and the EditMode/PlayMode tests pin the same head definition (everything above the
junction Z on the haft = the whole head; uniform-scale it about (0,0,junctionZ)).

Reproducible:
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    art-src/weapon-pack/weapon_set_src.blend --python tools/debug/bl_15_axe_head_clean.py
SAVES the .blend in place (the clean head is the new source of truth) + writes the FBX.
Idempotent: it DELETES whatever head component is present and rebuilds the clean one,
so re-running does NOT compound (unlike bl_14). Run once; the committed .blend + FBX
are the result.
"""
import bpy, bmesh, math
from mathutils import Vector

OUT_FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'

# --- the head's clean DEFAULT size -----------------------------------------
# The original bl_02 head is the full-size un-distorted biface. The Sponsor judged
# the FULL-size head too big out of the box; a uniform 0.8x is a sensible clean
# default he then dials DOWN further. This is a UNIFORM factor (x==y==z) — the whole
# head resizes, it does NOT chip.
HEAD_DEFAULT_FACTOR = 0.80

# Half-thickness (Y) of the head biface — from bl_02 (TH=0.052). The clean head
# preserves the original proportions exactly.
TH = 0.052

# Original bl_02 head profile in the X-Z plane (bit -X cutting edge, poll +X
# hawk-beak; Z up). The clean head is rebuilt from THIS (the un-chipped shape).
PROF = [
    (-0.300, 1.00), (-0.330, 0.90), (-0.345, 0.80), (-0.330, 0.70), (-0.300, 0.62),
    (-0.150, 0.66), (-0.045, 0.70), ( 0.050, 0.70),
    ( 0.150, 0.69), ( 0.250, 0.715), ( 0.330, 0.74), ( 0.340, 0.80), ( 0.250, 0.85),
    ( 0.150, 0.83), ( 0.080, 0.93), ( 0.000, 1.00), (-0.130, 1.02),
]
# Junction Z: the head's base on the haft centreline (lowest head-profile Z). The
# whole head scales UNIFORMLY about (0, 0, JUNCTION_Z). At the original profile the
# head base sits at z=0.62 (bottom of the cutting edge / underside). We pin the
# junction to the profile minimum so the uniform scale shrinks the head IN PLACE on
# the haft (the head's base stays seated; the head shrinks toward the neck).
JUNCTION_Z = min(z for (_x, z) in PROF)   # 0.62
# --------------------------------------------------------------------------

obj = bpy.data.objects.get('wpn_axe_01')
assert obj is not None, "wpn_axe_01 must exist in the source .blend"
me = obj.data
mat = me.materials[0] if me.materials else bpy.data.materials.get('WeaponPalette')


def components(mesh):
    adj = {i: set() for i in range(len(mesh.vertices))}
    for e in mesh.edges:
        a, b = e.vertices; adj[a].add(b); adj[b].add(a)
    seen = set(); comps = []
    for i in range(len(mesh.vertices)):
        if i in seen:
            continue
        stack = [i]; comp = []
        while stack:
            x = stack.pop()
            if x in seen:
                continue
            seen.add(x); comp.append(x); stack.extend(adj[x] - seen)
        comps.append(comp)
    return comps


# ---- 1. identify + DELETE the existing (distorted) head component ----------
comps = components(me)
# The head is the component with the WIDEST X-span (the blade fan). The haft is the
# thin tall column; the lash band is the small mid ring. Pick by X-span, not by a
# fixed n (so a re-author with a different head vert count still works).
def xspan(c):
    xs = [me.vertices[i].co.x for i in c]; return max(xs) - min(xs)
head_comp = max(comps, key=xspan)
print("DELETING old head component: n=%d xspan=%.3f" % (len(head_comp), xspan(head_comp)))

bm = bmesh.new(); bm.from_mesh(me)
bm.verts.ensure_lookup_table()
del_verts = [bm.verts[i] for i in head_comp]
bmesh.ops.delete(bm, geom=del_verts, context='VERTS')  # removes the head verts + their faces
bm.to_mesh(me); bm.free()
me.update()
print("AFTER delete: verts=%d faces=%d (haft + lash remain)" % (len(me.vertices), len(me.polygons)))

# ---- 2. build a CLEAN head from the original profile, as its own island ----
# Build the head into a fresh bmesh at FULL original size, then UNIFORM-scale the
# whole island about the junction, then merge it into the axe mesh.
hb = bmesh.new()
n = len(PROF)
front = [hb.verts.new((x, -TH, z)) for (x, z) in PROF]
back  = [hb.verts.new((x,  TH, z)) for (x, z) in PROF]
hb.faces.new(list(reversed(front)))   # front cap
hb.faces.new(back)                    # back cap
for i in range(n):
    j = (i + 1) % n
    hb.faces.new((front[i], front[j], back[j], back[i]))   # rim
hb.normal_update()

# ---- 3. UNIFORM scale the WHOLE head about the junction (the resize, not chip) --
pivot = Vector((0.0, 0.0, JUNCTION_Z))
for v in hb.verts:
    v.co = pivot + (v.co - pivot) * HEAD_DEFAULT_FACTOR   # x==y==z uniform — coherent whole-head resize
hb.normal_update()

# measure the clean default head
hxs = [v.co.x for v in hb.verts]; hzs = [v.co.z for v in hb.verts]
print("CLEAN HEAD default (factor=%.2f about z=%.3f): X[%.3f..%.3f] width=%.3f Z[%.3f..%.3f]" %
      (HEAD_DEFAULT_FACTOR, JUNCTION_Z, min(hxs), max(hxs), max(hxs) - min(hxs), min(hzs), max(hzs)))

# ---- 4. merge the clean head island into the axe mesh ----------------------
head_me = bpy.data.meshes.new('_clean_head_tmp')
hb.to_mesh(head_me); hb.free()
# join via a temp object
head_obj = bpy.data.objects.new('_clean_head_tmp', head_me)
bpy.context.collection.objects.link(head_obj)
for o in bpy.context.scene.objects:
    o.select_set(False)
obj.select_set(True); head_obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.join()   # head_obj merged into obj; head_obj removed
me = obj.data
print("AFTER join: verts=%d faces=%d" % (len(me.vertices), len(me.polygons)))

# ensure the single WeaponPalette material slot is intact + assigned to all faces
me.materials.clear()
me.materials.append(mat)
for p in me.polygons:
    p.material_index = 0

# ---- 5. faceted shading + outward normals (URP Cull Back) ------------------
bm = bmesh.new(); bm.from_mesh(me)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0008)   # weld any coincident at the junction
bm.to_mesh(me); bm.free()
for p in me.polygons:
    p.use_smooth = True
for e in me.edges:
    e.use_edge_sharp = True
me.update()

# report final head bounds + junction for the runtime/tests to pin the same definition
zs_all = [v.co.z for v in me.vertices]
head_verts_above = [v for v in me.vertices if v.co.z > JUNCTION_Z + 1e-4]
print("HEAD_JUNCTION_Z %.4f" % JUNCTION_Z)
print("HEAD_VERTS_ABOVE_JUNCTION %d / %d total" % (len(head_verts_above), len(me.vertices)))
print("AXE Z[%.3f..%.3f] (grip z=0 preserved: %s)" %
      (min(zs_all), max(zs_all), "YES" if abs(min(zs_all)) < 1e-4 else "NO"))

# ---- 6. save .blend + export FBX (same contract as bl_11/bl_14) ------------
if bpy.data.filepath:
    bpy.ops.wm.save_mainfile()
    print("SAVED_BLEND", bpy.data.filepath)

for o in bpy.context.scene.objects:
    o.select_set(False)
obj.location = (0, 0, 0); obj.rotation_euler = (0, 0, 0); obj.scale = (1, 1, 1)
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(
    filepath=OUT_FBX,
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
      (len(me.vertices), tris, [round(v, 3) for v in obj.dimensions], OUT_FBX))
print("AXE_HEAD_CLEAN_DONE")
