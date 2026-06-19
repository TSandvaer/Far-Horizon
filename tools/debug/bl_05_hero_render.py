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
# 3/4 from the cutting-edge side + slightly above, so the wedge TAPER reads (the head
# thins to the working edge) and the knapped facets catch raking light.
target = Vector((-0.08, 0.0, 0.50))
cam.location = Vector((-1.05, -2.2, 1.05))
cam.rotation_euler = (target - cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.lens = 78

# NEUTRAL white lighting so the flint reads its TRUE grey (the warm key tinted grey
# facets reddish against the blue bg — a render illusion, not the asset). Cool-neutral
# bg + white suns -> honest palette read for the style soak.
world = scene.world; world.use_nodes = True
bg = world.node_tree.nodes.get('Background')
bg.inputs[0].default_value = (0.42, 0.46, 0.52, 1.0)  # neutral slate
bg.inputs[1].default_value = 1.0
# a stronger key + dimmer fill so the modeled facets cast clear value steps (raking
# light reveals the knapped relief; flat fill alone washed it out).
for ln,energy in (('key_sun',4.0),('fill_sun',0.7)):
    o = bpy.data.objects.get(ln)
    if o:
        o.data.energy = energy
        o.data.color = (1.0,1.0,1.0)  # pure white — no warm tint

scene.render.engine = 'BLENDER_EEVEE'
scene.view_settings.view_transform = 'Standard'
scene.render.resolution_x = 760; scene.render.resolution_y = 1040
scene.render.filepath = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/tools/debug/axe_hero_render.png'
scene.render.image_settings.file_format = 'PNG'
bpy.ops.render.render(write_still=True)
print('HERO_RENDERED')
