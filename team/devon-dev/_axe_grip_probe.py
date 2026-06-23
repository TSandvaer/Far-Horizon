"""Read-only: where do the haft verts sit on Z relative to origin(0,0,0)?
Understand the grip seat so the extend-bake preserves HeldTool semantics."""
import bpy
BAKED = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=BAKED)
obj = [o for o in bpy.context.scene.objects if o.type=='MESH'][0]
co = [v.co for v in obj.data.vertices]
zs = sorted(set(round(v.z,4) for v in co))
print("GRIP_PROBE_START")
print("distinct Z rings (local): %s" % zs)
# haft verts only (z <= junction 0.0227)
haft_z = sorted(set(round(v.z,4) for v in co if v.z <= 0.0227))
print("haft Z rings (<=junction): %s" % haft_z)
print("origin local Z = 0.0 ; grip_end = %.4f ; so origin sits %.4f above grip end" % (min(v.z for v in co), 0.0 - min(v.z for v in co)))
print("GRIP_PROBE_DONE")
