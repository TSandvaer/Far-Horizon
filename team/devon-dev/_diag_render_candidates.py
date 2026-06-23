"""
_diag_render_candidates.py — 86cabh907. Render the head under several RIGID uprighting
rotations (about the haft-top junction) so the EYE picks the coaxial one (renders are the
ground truth; the metrics conflict because the head is an asymmetric wedge).

Candidates (head verts rotated rigidly about haft-top point P):
  0. current (committed, 2.71deg centroid / dogleg the Sponsor sees)
  1. mount-line (haft-top -> head-centroid) -> +Z   [centers head mass over haft]
  2. back-edge (head's MIN-X-side vertical edge) -> +Z   [aligns the poll/back of the axe with haft]
  3. blend of mount-line + back-edge (average)
For each, render FRONT (look -Y) so we see the dogleg head-on.
"""
import bpy, math, collections, os
import numpy as np
from mathutils import Vector, Matrix

FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
HEAD_BASE_Z = 0.022674
TARGET = Vector((0,0,1))
OUT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/team/devon-dev/coaxial_diag_86cabh907'
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
obj=[o for o in bpy.context.scene.objects if o.type=='MESH'][0]
bpy.context.view_layer.objects.active=obj; obj.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
me=obj.data
base=[v.co.copy() for v in me.vertices]; n=len(base)
head_i=[i for i in range(n) if base[i].z>HEAD_BASE_Z+1e-9]
haft_i=[i for i in range(n) if base[i].z<=HEAD_BASE_Z+1e-9]

def ring_centroids(idx,coords):
    r=collections.defaultdict(list)
    for i in idx: r[round(coords[i].z,3)].append(coords[i])
    return [Vector((sum(p.x for p in r[z])/len(r[z]),sum(p.y for p in r[z])/len(r[z]),z)) for z in sorted(r)]
def head_centroid(coords):
    return Vector((sum(coords[i].x for i in head_i)/len(head_i),sum(coords[i].y for i in head_i)/len(head_i),sum(coords[i].z for i in head_i)/len(head_i)))
def rot_to_z(axis):
    axis=axis.normalized(); v=axis.cross(TARGET); s=v.length; c=axis.dot(TARGET)
    if s<1e-12: return Matrix.Identity(3)
    return Matrix.Rotation(math.atan2(s,c),3,v.normalized())
P=ring_centroids(haft_i,base)[-1].copy()  # haft-top junction point

def mount_line(coords):
    return (head_centroid(coords)-P).normalized()
def back_edge(coords):
    # the back/poll edge = the head's MIN-X extreme run (the non-blade side). Take the head
    # verts in the lowest-X 25%, fit their (top - bottom) line in z.
    xs=sorted(coords[i].x for i in head_i); xcut=xs[int(len(xs)*0.25)]
    bk=[coords[i] for i in head_i if coords[i].x<=xcut]
    bz=sorted(bk,key=lambda p:p.z)
    lo=bz[:max(2,len(bz)//2)]; hi=bz[len(bz)//2:]
    clo=Vector((sum(p.x for p in lo)/len(lo),sum(p.y for p in lo)/len(lo),sum(p.z for p in lo)/len(lo)))
    chi=Vector((sum(p.x for p in hi)/len(hi),sum(p.y for p in hi)/len(hi),sum(p.z for p in hi)/len(hi)))
    a=(chi-clo).normalized(); return -a if a.z<0 else a

def upright(coords, axis_fn, iters=12):
    cur=[c.copy() for c in coords]
    for _ in range(iters):
        a=axis_fn(cur)
        ang=math.degrees(math.acos(max(-1,min(1,abs(a.normalized().z)))))
        if ang<0.03: break
        R=rot_to_z(a)
        for i in head_i: cur[i]=P+(R@(cur[i]-P))
    return cur

cands={
  "0current": [c.copy() for c in base],
  "1mountline": upright(base, mount_line),
  "2backedge": upright(base, back_edge),
}
# blend: average mount-line & back-edge corrected coords
mb=cands["1mountline"]; be=cands["2backedge"]
cands["3blend"]=[ (Vector(((mb[i].x+be[i].x)/2,(mb[i].y+be[i].y)/2,(mb[i].z+be[i].z)/2)) if i in set(head_i) else base[i].copy()) for i in range(n)]

# render each FRONT
sc=bpy.context.scene
sc.render.engine='BLENDER_WORKBENCH'
sc.display.shading.light='FLAT'; sc.display.shading.color_type='SINGLE'
sc.display.shading.single_color=(0.55,0.40,0.28)
if sc.world is None:
    sc.world=bpy.data.worlds.new("W")
sc.world.color=(1,1,1)
sc.render.resolution_x=500; sc.render.resolution_y=760
allz=[c.z for c in base]; allx=[c.x for c in base]; ally=[c.y for c in base]
cx=(max(allx)+min(allx))/2; cy=(max(ally)+min(ally))/2; cz=(max(allz)+min(allz))/2
span=max(max(allz)-min(allz),0.6)
cd=bpy.data.cameras.new("f"); cd.type='ORTHO'; cd.ortho_scale=span*1.25
cam=bpy.data.objects.new("f",cd); cam.location=(cx,cy-3,cz); cam.rotation_euler=(math.radians(90),0,0)
sc.collection.objects.link(cam); sc.camera=cam

def report(label,coords):
    cents=ring_centroids(head_i,coords); axA=(cents[-1]-cents[0]).normalized()
    hc=head_centroid(coords); cX=hc.x-P.x; cY=hc.y-P.y
    cline=math.degrees(math.acos(max(-1,min(1,abs(axA.z)))))
    print("CAND %s: centroid-line=%.2f deg  head-ctr lateral offset=(%+.4f,%+.4f) dist=%.4f"%(label,cline,cX,cY,math.hypot(cX,cY)))

for label,coords in cands.items():
    for i in range(n): me.vertices[i].co=coords[i]
    me.update()
    for p in me.polygons: p.use_smooth=True
    sc.render.filepath=OUT+"/cand_%s.png"%label
    bpy.ops.render.render(write_still=True)
    report(label,coords)
    print("RENDERED cand_%s.png"%label)
