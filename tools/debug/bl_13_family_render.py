"""
bl_13_family_render.py — framed FAMILY LINEUP render (axe + knife + sword + spear)
for the Sponsor's quality-pass confirm (ticket 86cabh907, #100 follow-up).

Reuses the hero-render lighting contract (neutral slate bg, white key+fill suns,
EEVEE Standard) so the steel/stone palette reads honestly. Frames all four
broadside, grips aligned to a common base line, so the relative SCALE + the new
faceted DETAIL (spine ridge, white edge rim, crossguard, wrapped grip, knapped
stone point) read against the axe.

Reproducible: python tools/debug/blender_mcp_send.py code tools/debug/bl_13_family_render.py
"""
import bpy, math
from mathutils import Vector

scene = bpy.context.scene
ref = bpy.data.objects.get('char_ref_1m8')
if ref:
    ref.hide_render = True

names = ['wpn_axe_01', 'wpn_knife_01', 'wpn_sword_01', 'wpn_spear_01']

# stand each upright on a common base line (origins are grip-mid; put each object's
# LOWEST geometry at z=0 so they line up like a rack). Space by x.
xs = [0.0, 0.55, 1.10, 1.75]
for nm, x in zip(names, xs):
    o = bpy.data.objects.get(nm)
    if not o:
        continue
    o.rotation_euler = (0, 0, 0)
    o.scale = (1, 1, 1)
    # world-space min z of geometry
    minz = min((o.matrix_world @ v.co).z for v in o.data.vertices)
    o.location.x = x
    o.location.y = 0.0
    o.location.z -= minz   # drop so lowest vert sits at z=0
    o.hide_render = False

# camera framing the whole rack broadside, slight 3/4 so facets catch raking light
cam = bpy.data.objects.get('cam_family')
if cam is None:
    cd = bpy.data.cameras.new('cam_family')
    cam = bpy.data.objects.new('cam_family', cd)
    scene.collection.objects.link(cam)
scene.camera = cam
target = Vector((0.85, 0.0, 1.05))
cam.location = Vector((0.70, -4.6, 1.35))
cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.lens = 55

# lighting (honest palette read)
world = scene.world; world.use_nodes = True
bg = world.node_tree.nodes.get('Background')
bg.inputs[0].default_value = (0.42, 0.46, 0.52, 1.0)
bg.inputs[1].default_value = 1.0
for ln, energy in (('key_sun', 4.0), ('fill_sun', 0.8)):
    o = bpy.data.objects.get(ln)
    if o:
        o.data.energy = energy
        o.data.color = (1.0, 1.0, 1.0)

scene.render.engine = 'BLENDER_EEVEE'
scene.view_settings.view_transform = 'Standard'
scene.render.resolution_x = 1280
scene.render.resolution_y = 960
scene.render.filepath = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/tools/debug/weapon_family_render.png'
scene.render.image_settings.file_format = 'PNG'
bpy.ops.render.render(write_still=True)
print('FAMILY_RENDERED')
