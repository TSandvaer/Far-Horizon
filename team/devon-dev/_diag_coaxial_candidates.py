"""
_diag_coaxial_candidates.py — 86cabh907. DRY RUN (measure only, no export).
The FRONT render shows the head doglegs RIGHT: its bulk sits off the haft +Z and the
head-base ring is laterally offset from the haft top. Find the RIGID rotation (about the
haft-top junction point) that makes the head read COAXIAL: the haft-top -> head-centroid
line == +Z, AND re-check every axis metric + the junction lateral offset afterward.

We rotate the head about the HAFT-TOP point P (the true mount/junction, where haft meets
head) so that pivoting also fixes the lateral junction offset (the head swings back over
the haft). Test target = align (head-centroid - P) to +Z.
"""
import bpy, math, collections
import numpy as np
from mathutils import Vector, Matrix

FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
HEAD_BASE_Z = 0.022674
TARGET = Vector((0, 0, 1))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = obj; obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = obj.data
co = [v.co.copy() for v in me.vertices]
n = len(co)
head_i = [i for i in range(n) if co[i].z > HEAD_BASE_Z + 1e-9]
haft_i = [i for i in range(n) if co[i].z <= HEAD_BASE_Z + 1e-9]

def ang_z(ax):
    return math.degrees(math.acos(max(-1, min(1, abs(ax.normalized().z)))))
def ring_centroids(idx, coords):
    r = collections.defaultdict(list)
    for i in idx: r[round(coords[i].z,3)].append(coords[i])
    return [Vector((sum(p.x for p in r[z])/len(r[z]), sum(p.y for p in r[z])/len(r[z]), z)) for z in sorted(r)]
def head_centroid(coords):
    return Vector((sum(coords[i].x for i in head_i)/len(head_i),
                   sum(coords[i].y for i in head_i)/len(head_i),
                   sum(coords[i].z for i in head_i)/len(head_i)))
def pca_axis(coords):
    P = np.array([[coords[i].x,coords[i].y,coords[i].z] for i in head_i]); P=P-P.mean(0)
    _,_,Vt=np.linalg.svd(P,full_matrices=False); a=Vector(Vt[0]); return -a if a.z<0 else a
def metrics(coords, label):
    cents = ring_centroids(head_i, coords)
    axA = (cents[-1]-cents[0]).normalized()
    axC = pca_axis(coords)
    hc = head_centroid(coords)
    hf = ring_centroids(haft_i, coords); htop = hf[-1]
    axM = (hc - htop).normalized()  # haft-top -> head-centroid (the mount line)
    dX = cents[0].x - htop.x; dY = cents[0].y - htop.y
    # head-centroid lateral offset from haft-top
    cX = hc.x - htop.x; cY = hc.y - htop.y
    print("  [%s] centroid-line=%.3f  PCA=%.3f  mount(htop->hc)=%.3f deg | base-ring offset dX=%+.4f dY=%+.4f | head-ctr offset cX=%+.4f cY=%+.4f"
          % (label, ang_z(axA), ang_z(axC), ang_z(axM), dX, dY, cX, cY))
    return htop, hc, axM, axC

print("BEFORE:")
htop, hc, axM, axC = metrics(co, "current")

# Pivot about HAFT-TOP point (true junction). Rotate head so mount line (htop->hc) -> +Z.
P = htop.copy()
def rot_to_z(axis):
    axis = axis.normalized()
    v = axis.cross(TARGET); s=v.length; c=axis.dot(TARGET)
    if s<1e-12: return Matrix.Identity(3)
    return Matrix.Rotation(math.atan2(s,c),3,v.normalized())

# iterate mount-line -> +Z about P (re-measure centroid after each rot; converges fast)
cur = [c.copy() for c in co]
for it in range(12):
    hc = head_centroid(cur)
    hf = ring_centroids(haft_i, cur); htop2 = hf[-1]
    axM = (hc - htop2).normalized()
    a = ang_z(axM)
    if a < 0.05:
        print("MOUNT-LINE converged at iter %d (%.4f deg)" % (it, a)); break
    R = rot_to_z(axM)
    for i in head_i: cur[i] = P + (R @ (cur[i]-P))
print("AFTER mount-line->+Z about haft-top:")
metrics(cur, "mount->+Z")

# Also test: PCA principal -> +Z about haft-top
cur2 = [c.copy() for c in co]
for it in range(12):
    a = pca_axis(cur2)
    if ang_z(a) < 0.05:
        print("PCA converged at iter %d" % it); break
    R = rot_to_z(a)
    for i in head_i: cur2[i] = P + (R @ (cur2[i]-P))
print("AFTER PCA->+Z about haft-top:")
metrics(cur2, "PCA->+Z")

# junction gap check for mount-line result
hmin = min(cur[i].z for i in head_i); hfmax = max(cur[i].z for i in haft_i)
print("MOUNT result junction: head_min_z=%.5f haft_max_z=%.5f gap=%.5f" % (hmin, hfmax, hmin-hfmax))
