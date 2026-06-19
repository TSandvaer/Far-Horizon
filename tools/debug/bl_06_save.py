import bpy, os
OUT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/weapon_set_src.blend'
# re-show char ref for the source file
ref = bpy.data.objects.get('char_ref_1m8')
if ref:
    ref.hide_set(False); ref.hide_render = False
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print('SAVED_BLEND', OUT)
