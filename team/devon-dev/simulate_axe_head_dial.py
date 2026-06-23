"""
Simulate the runtime dial's ApplyAxeHead UNIFORM scale on the restored stone FBX
at the FIXED headJunctionFraction=0.50, to prove the head shrinks UNIFORMLY,
staying stone-shaped (no flatten, no distortion) — the C# math reproduced 1:1:

  longAxis = max bounds extent
  junctionCoord = bmin[la] + span*0.50
  head = verts above junctionCoord
  pivot = bounds-centre with the longAxis comp set to junctionCoord
  head verts: v' = pivot + (v - pivot)*factor   (uniform x==y==z)

Renders front views at factor 1.00 (shipped) and 0.70 (dialed smaller) so a human
confirms the whole head scales coherently about its base.
Run: blender --background --python simulate_axe_head_dial.py -- <fbx>
"""
import bpy, sys, os, math, mathutils

argv = sys.argv
fbx = argv[argv.index("--")+1]
outdir = os.path.abspath(os.path.join(os.path.dirname(fbx), "..","..","..","..","team","devon-dev","axe_head_check","dial"))
os.makedirs(outdir, exist_ok=True)
FRAC = 0.50

def setup_and_get():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    o = [x for x in bpy.data.objects if x.type=='MESH'][0]
    return o

def render(o, base, factor):
    me = o.data
    base_vs = [mathutils.Vector(v) for v in base]
    la = 2
    zmin = min(v[la] for v in base_vs); zmax = max(v[la] for v in base_vs); span = zmax-zmin
    jc = zmin + span*FRAC
    cx = (min(v.x for v in base_vs)+max(v.x for v in base_vs))/2
    cy = (min(v.y for v in base_vs)+max(v.y for v in base_vs))/2
    pivot = mathutils.Vector((cx, cy, jc))
    head_idx = [i for i,v in enumerate(base_vs) if v[la] > jc]
    # apply uniform scale to head verts (the exact C# ApplyAxeHead)
    for i in head_idx:
        nv = pivot + (base_vs[i]-pivot)*factor
        me.vertices[i].co = nv
    me.update()
    # material: head red, haft grey
    mh = bpy.data.materials.new("H"); mh.diffuse_color=(0.9,0.1,0.1,1)
    mg = bpy.data.materials.new("G"); mg.diffuse_color=(0.5,0.5,0.5,1)
    me.materials.clear(); me.materials.append(mh); me.materials.append(mg)
    hset=set(head_idx)
    for p in me.polygons: p.material_index = 0 if all(v in hset for v in p.vertices) else 1
    sc = bpy.context.scene
    sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='FLAT'; sc.display.shading.color_type='MATERIAL'
    sc.render.resolution_x=700; sc.render.resolution_y=900
    vs=[v.co for v in me.vertices]
    ctr=mathutils.Vector(((min(v.x for v in vs)+max(v.x for v in vs))/2,(min(v.y for v in vs)+max(v.y for v in vs))/2,(zmin+zmax)/2))
    rad=max(span, max(v.x for v in vs)-min(v.x for v in vs))*0.5+0.3
    cam_d=bpy.data.cameras.new("c"); cam=bpy.data.objects.new("c",cam_d); sc.collection.objects.link(cam); sc.camera=cam
    d=mathutils.Vector((0,1,0.05)).normalized(); cam.location=ctr+d*rad*3.0
    cam.rotation_euler=(ctr-cam.location).normalized().to_track_quat('-Z','Y').to_euler(); cam_d.lens=60
    p=os.path.join(outdir,f"dial_factor_{factor:.2f}.png"); sc.render.filepath=p; bpy.ops.render.render(write_still=True)
    print(f"[dial] factor={factor:.2f} head={len(head_idx)} pivotZ={jc:.4f} -> {p}")

for fac in (1.00, 0.70):
    o = setup_and_get()
    base = [v.co.copy() for v in o.data.vertices]
    render(o, base, fac)
print(f"[dial] outdir={outdir}")
