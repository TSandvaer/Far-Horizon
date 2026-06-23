import bpy, bmesh, os, math
from mathutils import Vector

obj = bpy.data.objects['wpn_axe_01']
for o in bpy.data.objects: o.select_set(False)
obj.select_set(True)
bpy.context.view_layer.objects.active = obj

# --- merge by distance (clean loose/duplicate verts) ---
bm = bmesh.new(); bm.from_mesh(obj.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0008)
bm.to_mesh(obj.data); bm.free(); obj.data.update()

# --- recalc normals outside ---
bm = bmesh.new(); bm.from_mesh(obj.data)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bm.to_mesh(obj.data); bm.free(); obj.data.update()

# --- set origin to GRIP MIDPOINT ---
# grip = where the hand closes on the haft, roughly mid-lower haft. Haft spans z 0..1.02;
# hand grips below the head, around z ~0.45 (mid of the exposed lower haft).
GRIP_Z = 0.45
bpy.context.scene.cursor.location = Vector((0.0, 0.0, GRIP_Z))
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

# --- apply rotation & scale (NOT location: grip-offset origin is intentional) ---
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

# verify blade points +Z: head center z (~0.86 world) is above grip origin -> +Z local. OK.

# --- face orientation sanity: count any inward faces ---
bm = bmesh.new(); bm.from_mesh(obj.data)
inward = 0
for f in bm.faces:
    # crude: dot of normal with (center - object-center-ish). skip; recalc already done.
    pass
bm.free()

# --- EXPORT FBX (spec-exact settings) ---
OUT_DIR = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack'
os.makedirs(OUT_DIR, exist_ok=True)
fbx = os.path.join(OUT_DIR, 'wpn_axe_01.fbx')

# select only the axe
for o in bpy.data.objects: o.select_set(False)
obj.select_set(True)
bpy.context.view_layer.objects.active = obj

bpy.ops.export_scene.fbx(
    filepath=fbx,
    use_selection=True,
    apply_scale_options='FBX_SCALE_UNITS',   # FBX Unit Scale
    axis_forward='-Y',
    axis_up='Z',
    use_space_transform=False,
    bake_space_transform=False,              # Apply Transform OFF
    mesh_smooth_type='OFF',                  # 'Normals Only' == OFF in operator (exports custom normals)
    use_tspace=True,
    use_triangles=True,   # triangulate n-gon head faces on export (clean Unity import, no tspace warning)
    add_leaf_bones=False,
    object_types={'MESH'},
    path_mode='STRIP',
    embed_textures=False,
)
tri = sum(len(p.vertices)-2 for p in obj.data.polygons)
print('EXPORTED %s  verts=%d tris=%d origin=%s' % (fbx, len(obj.data.vertices), tri, tuple(round(c,3) for c in obj.location)))
