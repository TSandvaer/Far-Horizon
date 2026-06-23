"""
_diag_head_axis_methods.py — 86cabh907 length-picker prep.
Measure the CURRENT committed wpn_axe_01.fbx head-vs-haft geometry by SEVERAL axis
methods, to understand the 2.71deg residual bl_18 left and pick the rotation that
drives the head-vs-haft junction to <=0.5deg.

Methods compared (head verts = z > HEAD_BASE_Z):
  A. centroid-line (base-ring centroid -> top-ring centroid)   <- bl_18 used this
  B. half-split (bottom-half centroid -> top-half centroid)
  C. bounding-box principal axis (PCA of head verts)
  D. blade-TIP -> head-base-ring-centroid (the SILHOUETTE line the eye reads)
  E. head overall centroid -> blade tip

Also: the haft +Z straightness, the junction continuity (does head-base ring sit
centred on the haft top), and whether the head's WIDEST cross-section (the blade)
points sideways or doglegs.
"""
import bpy, math, collections
from mathutils import Vector

FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
HEAD_BASE_Z = 0.022674

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = obj.data
co = [v.co.copy() for v in me.vertices]
n = len(co)
print("VERTS=%d" % n)

head_i = [i for i in range(n) if co[i].z > HEAD_BASE_Z + 1e-9]
haft_i = [i for i in range(n) if co[i].z <= HEAD_BASE_Z + 1e-9]
print("N_HEAD=%d N_HAFT=%d" % (len(head_i), len(haft_i)))

def ang(ax):
    ax = ax.normalized()
    return math.degrees(math.acos(max(-1, min(1, abs(ax.z)))))

def ring_centroids(idx):
    rings = collections.defaultdict(list)
    for i in idx:
        rings[round(co[i].z, 3)].append(co[i])
    return [Vector((sum(p.x for p in rings[z])/len(rings[z]),
                    sum(p.y for p in rings[z])/len(rings[z]), z)) for z in sorted(rings)]

# A. centroid-line
cents = ring_centroids(head_i)
axA = (cents[-1] - cents[0]).normalized()
# B. half-split
zs = sorted(co[i].z for i in head_i); med = zs[len(zs)//2]
bot = [co[i] for i in head_i if co[i].z <= med]; top = [co[i] for i in head_i if co[i].z > med]
cb = Vector((sum(p.x for p in bot)/len(bot), sum(p.y for p in bot)/len(bot), sum(p.z for p in bot)/len(bot)))
ct = Vector((sum(p.x for p in top)/len(top), sum(p.y for p in top)/len(top), sum(p.z for p in top)/len(top)))
axB = (ct - cb).normalized()
# C. PCA principal axis of head verts
import numpy as np
P = np.array([[co[i].x, co[i].y, co[i].z] for i in head_i])
P = P - P.mean(axis=0)
_, _, Vt = np.linalg.svd(P, full_matrices=False)
axC = Vector(Vt[0]);
if axC.z < 0: axC = -axC
# D. blade-tip -> head-base ring centroid (silhouette read).  tip = head vert with max |off-axis| from haft centreline AND high z
base_ring = cents[0]
# tip = the head vert farthest from the base ring centre in 3D (the blade corner)
tip = max((co[i] for i in head_i), key=lambda p: (p - base_ring).length)
axD = (tip - base_ring).normalized()
# E. head overall centroid -> tip
hc = Vector((sum(co[i].x for i in head_i)/len(head_i), sum(co[i].y for i in head_i)/len(head_i), sum(co[i].z for i in head_i)/len(head_i)))
axE = (tip - hc).normalized()

print("AXIS A centroid-line  =(%+.4f,%+.4f,%+.4f)  vs+Z=%.3f deg" % (axA.x,axA.y,axA.z, ang(axA)))
print("AXIS B half-split     =(%+.4f,%+.4f,%+.4f)  vs+Z=%.3f deg" % (axB.x,axB.y,axB.z, ang(axB)))
print("AXIS C PCA-principal  =(%+.4f,%+.4f,%+.4f)  vs+Z=%.3f deg" % (axC.x,axC.y,axC.z, ang(axC)))
print("AXIS D tip->base      =(%+.4f,%+.4f,%+.4f)  vs+Z=%.3f deg" % (axD.x,axD.y,axD.z, ang(axD)))
print("AXIS E hc->tip        =(%+.4f,%+.4f,%+.4f)  vs+Z=%.3f deg" % (axE.x,axE.y,axE.z, ang(axE)))

# haft axis
hf = ring_centroids(haft_i)
haft_ax = (hf[-1] - hf[0]).normalized()
print("HAFT axis             =(%+.4f,%+.4f,%+.4f)  vs+Z=%.3f deg" % (haft_ax.x,haft_ax.y,haft_ax.z, ang(haft_ax)))

# junction continuity: head-base ring centroid X/Y offset from haft-top centroid
htop = hf[-1]
print("JUNCTION offset head-base vs haft-top: dX=%+.5f dY=%+.5f (head-base z=%.5f, haft-top z=%.5f)" %
      (base_ring.x - htop.x, base_ring.y - htop.y, base_ring.z, htop.z))

# What's the head-base ring radius vs haft-top radius (does the head sit ON the haft)?
def ring_radius(ring_pts, c):
    return sum((p - c).length for p in ring_pts) / len(ring_pts) if ring_pts else 0
# head min-z ring
hmin = min(co[i].z for i in head_i)
hbr = [co[i] for i in head_i if abs(co[i].z - hmin) < 0.01]
htr = [co[i] for i in haft_i if abs(co[i].z - max(co[i2].z for i2 in haft_i)) < 0.01]
print("head-base lowest-ring verts=%d, haft-top ring verts=%d" % (len(hbr), len(htr)))

# bounding box of head (after measuring) for length-variant planning
hz = [co[i].z for i in head_i]
print("HEAD z-span: %.5f .. %.5f (height %.5f)" % (min(hz), max(hz), max(hz)-min(hz)))
allz = [c.z for c in co]
print("FULL z-span: %.5f .. %.5f (total %.5f)  grip_z=%.5f" % (min(allz), max(allz), max(allz)-min(allz), min(allz)))
haft_len = HEAD_BASE_Z - min(allz)
head_h = max(hz) - HEAD_BASE_Z
print("HAFT_LEN=%.5f  HEAD_H=%.5f  haft:head=%.4f" % (haft_len, head_h, haft_len/head_h))
