import bpy, math
from mathutils import Vector
scene = bpy.context.scene
obj = bpy.data.objects['wpn_axe_01']
ref = bpy.data.objects.get('char_ref_1m8')
if ref: ref.hide_render = True

# hero 3/4 render matching the inspiration framing (blade-left, beak-right)
cam = bpy.data.objects.get('cam_hero')
if cam is None:
    cd = bpy.data.cameras.new('cam_hero')
    cam = bpy.data.objects.new('cam_hero', cd)
    scene.collection.objects.link(cam)
scene.camera = cam
# obj origin now at grip (0,0,0.45 was applied to location; geometry unchanged in world)
target = Vector((-0.02, 0.0, 0.62))
cam.location = Vector((-0.70, -2.6, 0.85))
cam.rotation_euler = (target - cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.lens = 85

scene.render.engine = 'BLENDER_EEVEE'
scene.view_settings.view_transform = 'Standard'
scene.render.resolution_x = 760; scene.render.resolution_y = 1040
scene.render.filepath = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/tools/debug/axe_hero_render.png'
scene.render.image_settings.file_format = 'PNG'
bpy.ops.render.render(write_still=True)
print('HERO_RENDERED')
