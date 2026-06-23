"""
bl_axe_haft_compare_render_86cabh907.py — ONE side-by-side comparison render.

Lays current(1.06:1) + 1.5x + 2.0x + 2.5x in a single scene, SAME scale, each with
its own 1.8m human-height reference bar, and renders one orthographic side view to
team/devon-dev/axe_haft_compare_86cabh907.png. Also renders each variant individually.

All four axes share Z=0 grip origin (as in-game/HeldTool), so the human-height bars
align at a common base and the proportion read is honest.
"""
import bpy
from mathutils import Vector

WT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt'
DEV = WT + r'/team/devon-dev'
OUT = DEV + r'/axe_haft_compare_86cabh907.png'

VARIANTS = [
    ("current 1.06:1", WT + r'/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'),
    ("1.5x 1.59:1",    DEV + r'/wpn_axe_haft_1p5x_86cabh907.fbx'),
    ("2.0x 2.12:1",    DEV + r'/wpn_axe_haft_2p0x_86cabh907.fbx'),
    ("2.5x 2.65:1",    DEV + r'/wpn_axe_haft_2p5x_86cabh907.fbx'),
]

# wood-ish + a red head would need per-face material; keep one flat readable color
def import_axe(fbx, name):
    before = set(o.name for o in bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=fbx)
    new = [o for o in bpy.context.scene.objects if o.name not in before and o.type=='MESH']
    obj = new[0]
    obj.name = name
    return obj

# ---- build a single scene ----
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# flat materials
wood = bpy.data.materials.new("Wood"); wood.use_nodes=False; wood.diffuse_color=(0.55,0.38,0.22,1.0)
refm = bpy.data.materials.new("Ref");  refm.use_nodes=False; refm.diffuse_color=(0.20,0.55,0.85,1.0)

SPACING = 1.0   # X spacing between axes (axes are < 0.5 wide, plus a ref bar each)
axes = []
xcursor = 0.0
maxz = 0.0; minz = 0.0
for i,(label, fbx) in enumerate(VARIANTS):
    obj = import_axe(fbx, "axe_%d"%i)
    # set single flat material
    obj.data.materials.clear(); obj.data.materials.append(wood)
    obj.location.x = xcursor
    # track world z extents for camera framing (axes share Z=0 origin; grip extends -Z)
    ws = [obj.matrix_world @ v.co for v in obj.data.vertices]
    minz = min(minz, min(p.z for p in ws)); maxz = max(maxz, max(p.z for p in ws))
    # 1.8m ref bar beside each axe
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    ref = bpy.context.active_object; ref.name = "ref_%d"%i
    ref.scale = (0.03,0.03,0.9)            # 1.8m tall
    # base of bar at the lowest grip (so a common human baseline reads across all)
    ref.location = (xcursor + 0.30, 0.0, 0.0 - 0.45 + 0.9)  # base at the ORIGINAL grip end -0.45
    ref.data.materials.clear(); ref.data.materials.append(refm)
    axes.append(obj)
    xcursor += SPACING

# camera framing: ortho, look along -Y (front-on side), span across all axes
xmin = -0.4; xmax = xcursor - SPACING + 0.6
xmid = (xmin + xmax)*0.5
zmid = (minz + maxz)*0.5
width = (xmax - xmin)
height = (maxz - minz)
extent = max(width, height) * 1.12

scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_shadows = False
scene.render.resolution_x = 1600
scene.render.resolution_y = 1100
scene.render.film_transparent = False
if scene.world is None: scene.world = bpy.data.worlds.new("W")
scene.world.color = (0.92,0.92,0.94)

cam_data = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam); scene.camera = cam
cam_data.type='ORTHO'; cam_data.ortho_scale = extent
aim = Vector((xmid, 0.0, zmid))
eye = Vector((xmid, -6.0, zmid))
cam.location = eye
cam.rotation_euler = (aim - eye).to_track_quat('-Z','Y').to_euler()

scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("COMPARE_RENDER_DONE -> %s" % OUT)
print("frame: xmin=%.3f xmax=%.3f minz=%.3f maxz=%.3f extent=%.3f" % (xmin,xmax,minz,maxz,extent))

# ---- individual renders (one axe + one ref bar each) ----
for i,(label, fbx) in enumerate(VARIANTS):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    w = bpy.data.materials.new("W"); w.use_nodes=False; w.diffuse_color=(0.55,0.38,0.22,1.0)
    r = bpy.data.materials.new("R"); r.use_nodes=False; r.diffuse_color=(0.20,0.55,0.85,1.0)
    bpy.ops.import_scene.fbx(filepath=fbx)
    o = [x for x in sc.objects if x.type=='MESH'][0]
    o.data.materials.clear(); o.data.materials.append(w)
    ws = [o.matrix_world @ v.co for v in o.data.vertices]
    zmn = min(p.z for p in ws); zmx = max(p.z for p in ws)
    xmn = min(p.x for p in ws); xmx = max(p.x for p in ws)
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    rb = bpy.context.active_object; rb.scale=(0.03,0.03,0.9)
    rb.location = (xmx + 0.35, 0.0, -0.45 + 0.9)
    rb.data.materials.clear(); rb.data.materials.append(r)
    sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'
    sc.display.shading.color_type='MATERIAL'; sc.display.shading.show_shadows=False
    sc.render.resolution_x=700; sc.render.resolution_y=1100
    if sc.world is None: sc.world = bpy.data.worlds.new("W2")
    sc.world.color=(0.92,0.92,0.94)
    ext = max(xmx+0.5-(xmn), zmx-zmn)*1.15
    cd=bpy.data.cameras.new("C"); c=bpy.data.objects.new("C",cd); sc.collection.objects.link(c); sc.camera=c
    cd.type='ORTHO'; cd.ortho_scale=ext
    xm=((xmn)+(xmx+0.5))*0.5; zm=(zmn+zmx)*0.5
    aimv=Vector((xm,0,zm)); eyev=Vector((xm,-6,zm))
    c.location=eyev; c.rotation_euler=(aimv-eyev).to_track_quat('-Z','Y').to_euler()
    tag = ["current","1p5x","2p0x","2p5x"][i]
    sc.render.filepath = DEV + "/axe_haft_%s_86cabh907.png" % tag
    bpy.ops.render.render(write_still=True)
    print("INDIV_RENDER -> %s" % sc.render.filepath)
print("ALL_RENDERS_DONE")
