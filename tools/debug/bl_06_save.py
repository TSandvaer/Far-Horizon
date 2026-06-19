import bpy, os
# Modeling source lives OUTSIDE Assets/ so Unity does not import the .blend (its Blender
# importer needs Blender on PATH + makes a spurious asset). FBX + palette PNG are the
# Unity-imported deliverables; the .blend is for re-edits only.
OUT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/art-src/weapon-pack/weapon_set_src.blend'
os.makedirs(os.path.dirname(OUT), exist_ok=True)
# re-show char ref for the source file
ref = bpy.data.objects.get('char_ref_1m8')
if ref:
    ref.hide_set(False); ref.hide_render = False
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print('SAVED_BLEND', OUT)
