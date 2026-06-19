"""
bl_11_export_set.py — finalize + FBX-export wpn_knife_01 / wpn_sword_01 / wpn_spear_01
to Assets/Art/Props/WeaponPack/ matching the axe's import contract.

Per blender-asset-pipeline doc §7-§8:
  - Apply All Transforms (rotation+scale; KEEP location? origin is grip at (0,0,0) already
    since we built around z=0 — but the objects were moved in X for the lineup, so we
    must zero location BEFORE export OR export with the per-object origin. We zero each
    object's location to (0,0,0) so the grip origin maps to the FBX origin, then apply.)
  - Merge by Distance (0.001) to weld the per-quad duplicate corner verts.
  - Recalc normals outside.
  - Shade Smooth + Mark Sharp ALL (already set in finalize, re-assert after merge).
  - FBX: -Y Forward, Z Up, FBX Unit Scale, Apply Transform UNCHECKED, Smoothing=OFF
    (use_mesh_modifiers irrelevant; we export custom split normals via the mesh's
    use_edge_sharp + Shade Smooth -> 'Normals Only' is the Blender FBX 'OFF' smoothing
    that writes per-vertex normals; we pass smoothing 'OFF' which exports normals only).
"""
import bpy, bmesh, os

OUT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack'
NAMES = ['wpn_knife_01', 'wpn_sword_01', 'wpn_spear_01']


def clean(obj):
    me = obj.data
    # weld duplicate corner verts (tiny threshold; we built quads independently)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0008)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    # re-assert faceted shading after the weld
    for p in me.polygons: p.use_smooth = True
    for e in me.edges: e.use_edge_sharp = True
    me.update()


for n in NAMES:
    o = bpy.data.objects.get(n)
    if not o:
        print("MISSING", n); continue
    # zero location so the grip origin maps to the FBX (0,0,0)
    o.location = (0, 0, 0)
    o.rotation_euler = (0, 0, 0)
    o.scale = (1, 1, 1)
    clean(o)

# export each individually (Selected Objects ON)
bpy.ops.object.select_all(action='DESELECT')
for n in NAMES:
    o = bpy.data.objects.get(n)
    if not o: continue
    for x in bpy.context.scene.objects: x.select_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    path = os.path.join(OUT, n + '.fbx')
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options='FBX_SCALE_UNITS',   # FBX Unit Scale (avoids 100x bug)
        axis_forward='-Y',
        axis_up='Z',
        use_space_transform=False,
        bake_space_transform=False,               # Apply Transform UNCHECKED
        mesh_smooth_type='OFF',                    # Normals Only (writes custom split normals)
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        object_types={'MESH'},
        use_custom_props=False,
    )
    me = o.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print(f"EXPORTED {n}: verts={len(me.vertices)} tris={tris} dims={[round(v,3) for v in o.dimensions]} -> {path}")

print("EXPORT_DONE")
