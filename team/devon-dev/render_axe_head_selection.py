"""
Render the restored stone axe with HEAD verts (selected by a candidate
junctionFraction) shown RED and HAFT verts shown as the mesh, so a human can
confirm the fraction grabs the actual head wedge, not part of the haft.

Saves two side-view PNGs (front + side) to team/devon-dev/axe_head_check/.
Run: blender --background --python render_axe_head_selection.py -- <fbx> <frac>
"""
import bpy, sys, os, math, mathutils

argv = sys.argv
tail = argv[argv.index("--")+1:] if "--" in argv else []
fbx = tail[0]
frac = float(tail[1]) if len(tail) > 1 else 0.40
outdir = tail[2] if len(tail) > 2 else os.path.join(os.path.dirname(fbx), "..", "..", "..", "..", "team", "devon-dev", "axe_head_check")
outdir = os.path.abspath(outdir)
os.makedirs(outdir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
o = [x for x in bpy.data.objects if x.type=='MESH'][0]
me = o.data
vs = [v.co.copy() for v in me.vertices]
la = 2
zmin = min(v[la] for v in vs); zmax = max(v[la] for v in vs); span = zmax - zmin
jc = zmin + span*frac
head_idx = set(i for i,v in enumerate(vs) if v[la] > jc)
print(f"[render] frac={frac} jc={jc:.4f} HEAD={len(head_idx)}/{len(vs)}")

# two materials: head=red, haft=grey; assign per-face by whether ALL its verts are head
m_head = bpy.data.materials.new("HEAD"); m_head.use_nodes=False; m_head.diffuse_color=(0.9,0.1,0.1,1)
m_haft = bpy.data.materials.new("HAFT"); m_haft.use_nodes=False; m_haft.diffuse_color=(0.5,0.5,0.5,1)
me.materials.clear(); me.materials.append(m_head); me.materials.append(m_haft)
for p in me.polygons:
    is_head = all(v in head_idx for v in p.vertices)
    p.material_index = 0 if is_head else 1

# camera + light, frame the object from the side (look along -X so Z is up-ish on screen)
bpy.context.scene.render.engine = 'BLENDER_WORKBENCH'
bpy.context.scene.display.shading.light = 'FLAT'
bpy.context.scene.display.shading.color_type = 'MATERIAL'
bpy.context.scene.render.resolution_x = 700
bpy.context.scene.render.resolution_y = 900
bpy.context.scene.render.film_transparent = False

ctr = mathutils.Vector(((min(v.x for v in vs)+max(v.x for v in vs))/2,
                        (min(v.y for v in vs)+max(v.y for v in vs))/2,
                        (zmin+zmax)/2))
rad = max(span, max(v.x for v in vs)-min(v.x for v in vs))*0.5 + 0.3

cam_data = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cam_data)
bpy.context.scene.collection.objects.link(cam); bpy.context.scene.camera = cam

def shoot(name, dir_vec):
    d = mathutils.Vector(dir_vec).normalized()
    cam.location = ctr + d*rad*3.0
    # point at ctr
    look = (ctr - cam.location).normalized()
    cam.rotation_euler = look.to_track_quat('-Z','Y').to_euler()
    cam_data.lens = 60
    p = os.path.join(outdir, name)
    bpy.context.scene.render.filepath = p
    bpy.ops.render.render(write_still=True)
    print(f"[render] wrote {p}")

shoot("axe_head_sel_side.png", (1, 0, 0.05))   # look along -X (side profile of the wedge)
shoot("axe_head_sel_front.png", (0, 1, 0.05))  # look along -Y (front face)
print(f"[render] outdir={outdir}")
