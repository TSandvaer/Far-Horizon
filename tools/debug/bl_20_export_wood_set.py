"""
bl_20_export_wood_set.py — FBX-export the 5 WOOD-tier weapons (ticket 86catqn5n) from
art-src/weapons_reauthor.blend (wood row y=-0.6) to Assets/Art/Props/WeaponPack/,
matching the stone/iron family import contract (blender-asset-pipeline §7-§8).

SOURCE IS READ-ONLY: this script NEVER calls wm.save_mainfile / wm.save_as_mainfile.
It mutates objects in-memory (zero the lineup-display location so the grip origin maps
to the FBX (0,0,0)) then exports — the .blend on disk is untouched.

The wood row shares the stone siblings' grip-origin scheme EXACTLY (same per-object
z-origin: axe/pickaxe 0.155, knife 0.065, sword 0.085, spear 0.58) — the family-
extension route (§3): wood built from the approved siblings. So the identical export
(zero location, §8 params) yields drop-in FBXs that seat on the shared family seat.

MINIMAL MUTATION (Sponsor PASSED these meshes as-is 2026-07-18): we do NOT weld,
recalc normals, or re-mark sharp/smooth — the authored + approved custom split normals
are exported verbatim (mesh_smooth_type='OFF' writes them as-is). We only zero the
lineup-display transform (already rot=0/scale=1; only location is non-identity).

Run (dispatched persona, headless CLI — blender-asset-pipeline §10):
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    art-src/weapons_reauthor.blend --python tools/debug/bl_20_export_wood_set.py \
    -- <out_weaponpack_dir>
The out dir defaults to the drew worktree WeaponPack if no `-- <dir>` is passed.
"""
import bpy, sys, os

DEFAULT_OUT = r'C:/Trunk/PRIVATE/Far-Horizon-drew-wt/Assets/Art/Props/WeaponPack'
NAMES = ['wpn_axe_wood_01', 'wpn_pickaxe_wood_01', 'wpn_spear_wood_01',
         'wpn_knife_wood_01', 'wpn_sword_wood_01']  # dagger reuses knife naming (§6a)

# out dir from `-- <dir>` if provided
argv = sys.argv
out = DEFAULT_OUT
if '--' in argv:
    extra = argv[argv.index('--') + 1:]
    if extra:
        out = extra[0]
os.makedirs(out, exist_ok=True)

# --- DIAGNOSTIC PRE-PASS: verify origin=grip + no unexpected modifiers -------------
print("=== WOOD-ROW PRE-EXPORT DIAGNOSTIC ===")
ok = True
for n in NAMES:
    o = bpy.data.objects.get(n)
    if not o:
        print("MISSING", n); ok = False; continue
    me = o.data
    mods = [m.type for m in o.modifiers]
    zs = [v.co.z for v in me.vertices]  # LOCAL coords (origin-relative)
    zmin, zmax = min(zs), max(zs)
    # grip origin should sit at/near the low end of the local z-span (haft base region)
    print(f"{n:22s} mods={mods} localZ=[{zmin:+.3f},{zmax:+.3f}] origin@0 "
          f"(span={zmax-zmin:.3f}) mats={[m.name for m in me.materials]}")
    if mods:
        print(f"  !! WARN {n} carries modifiers {mods} — export applies them (use_mesh_modifiers=True)")

# --- EXPORT -------------------------------------------------------------------------
bpy.ops.object.select_all(action='DESELECT')
for n in NAMES:
    o = bpy.data.objects.get(n)
    if not o:
        continue
    # zero the lineup-display transform so the grip origin maps to FBX (0,0,0).
    o.location = (0, 0, 0)
    o.rotation_euler = (0, 0, 0)
    o.scale = (1, 1, 1)

    for x in bpy.context.scene.objects:
        x.select_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o

    path = os.path.join(out, n + '.fbx')
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options='FBX_SCALE_UNITS',   # FBX Unit Scale (avoids 100x bug) §8
        axis_forward='-Y',                        # §8
        axis_up='Z',                              # §8
        use_space_transform=False,                # §8 (Unity Bake Axis Conversion handles it)
        bake_space_transform=False,               # §8 Apply Transform UNCHECKED
        mesh_smooth_type='OFF',                   # §8 Normals Only (custom split normals as-is)
        # use_tspace omitted (default False) to match the stone/iron family export (bl_11): the
        # meshes carry n-gons so tangent space can't be computed anyway (URP/Unlit needs none).
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        object_types={'MESH'},                    # static prop, no armature (§8 Armature OFF)
        use_custom_props=False,
    )
    me = o.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print(f"EXPORTED {n}: verts={len(me.vertices)} tris={tris} "
          f"dims={[round(v,3) for v in o.dimensions]} -> {path}")

print("EXPORT_OK" if ok else "EXPORT_WITH_WARNINGS")
