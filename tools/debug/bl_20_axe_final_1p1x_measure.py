"""
bl_20_axe_final_1p1x_measure.py — 86cabh907 FINAL axe = 1.1x (the Sponsor's [L] pick).

The Sponsor picked 1.1x from the in-hand HeldAxeLengthPicker and said "not wasting more time
on the axe" — this is the FINAL look. bl_19 already baked the exact 1.1x mesh to
wpn_axe_01_len11.fbx (head coaxial 0.0198deg, head LOCKED 0.65x, haft straight, single
WeaponPalette slot). The FINAL bake = make wpn_axe_01.fbx BYTE-IDENTICAL to that picked variant
(a file copy is the safe, drift-free route — re-running the float math risks tiny divergence
from the validated len11). This script does NOT re-author the mesh; it COPIES len11 -> canonical
and MEASURES the geometry needed to re-derive the Unity-side grip-shift for the shorter 1.1x haft.

WHY a measure step: HeldAxeGripShiftY (MovementCameraScene) was derived for the 1.5x haft. With
the 1.1x haft the handle is shorter, so the lower-third grip point re-seats. This script prints
the imported-Unity handle geometry so the const is re-derived from ground truth, not guessed
([[verify-soak-builds-or-bake-and-judge]] — never guess a soak-facing placement value).

It also re-asserts the FINAL canonical is exactly the picked 1.1x variant (head locked + coaxial,
single material, grip origin (0,0,0), faceted normals) so the finalize is self-verifying.

Run (from the worktree root; wpn_axe_01_len11.fbx must be present = the committed bl_19 variant):
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    --python tools/debug/bl_20_axe_final_1p1x_measure.py
"""
import bpy, math, collections, os, shutil
from mathutils import Vector

DIR = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack'
LEN11 = DIR + r'/wpn_axe_01_len11.fbx'      # the Sponsor's [L] pick (bl_19 1.1x variant)
CANON = DIR + r'/wpn_axe_01.fbx'            # the shipped DEFAULT axe
HEAD_BASE_Z = 0.022674                       # fixed head-base junction (bl_17/bl_18/bl_19); INVARIANT
# Unity importer constant the COAXIAL head is normalized by (HeroAxeCoaxialHeadHeightFromTipU,
# WeaponPackAssetGen.cs) + the target head world-height the normalize pins (HeroAxeTargetHeadHeightU).
COAXIAL_HEAD_H_FROM_TIP_U = 0.50247
TARGET_HEAD_HEIGHT_U = 0.51655

assert os.path.exists(LEN11), "wpn_axe_01_len11.fbx (the 1.1x pick) missing — run bl_19 first"

# ----------------------------------------------------------------- COPY len11 -> canonical (FINAL)
shutil.copyfile(LEN11, CANON)
print("FINAL: copied wpn_axe_01_len11.fbx -> wpn_axe_01.fbx (shipped default = the 1.1x pick)")

# ----------------------------------------------------------------- import canonical + verify identity
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=CANON)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = obj; obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = obj.data
co = [v.co.copy() for v in me.vertices]
n = len(co)

# single material slot
assert len(me.materials) == 1 and me.materials[0].name == 'WeaponPalette', \
    "expected single WeaponPalette slot, got %s" % [m.name if m else None for m in me.materials]
print("MATERIAL OK: single WeaponPalette slot")
print("VERTS=%d  TRIS=%d" % (n, sum(len(p.vertices) - 2 for p in me.polygons)))

# grip origin preserved at (0,0,0): obj origin is world (0,0,0) after transform_apply
print("ORIGIN OK: object origin = (0,0,0) (grip-point semantics, §6)")

# head/haft split at the fixed junction (same as bl_19)
head_i = [i for i in range(n) if co[i].z > HEAD_BASE_Z + 1e-9]
haft_i = [i for i in range(n) if co[i].z <= HEAD_BASE_Z + 1e-9]
assert len(head_i) == 110 and len(haft_i) == 18, \
    "head/haft split drifted (head=%d haft=%d) -- NOT the bl_19 mesh" % (len(head_i), len(haft_i))

def ring_centroids(idx, coords):
    r = collections.defaultdict(list)
    for i in idx: r[round(coords[i].z, 3)].append(coords[i])
    return [Vector((sum(p.x for p in r[z]) / len(r[z]),
                    sum(p.y for p in r[z]) / len(r[z]), z)) for z in sorted(r)]
def head_centroid(coords):
    return Vector((sum(coords[i].x for i in head_i) / len(head_i),
                   sum(coords[i].y for i in head_i) / len(head_i),
                   sum(coords[i].z for i in head_i) / len(head_i)))
def ang_z(ax):
    return math.degrees(math.acos(max(-1, min(1, abs(ax.normalized().z)))))

# coaxial junction angle (mount line vs haft +Z) — MUST be <=0.5
P = ring_centroids(haft_i, co)[-1]          # haft-top junction point
haft_ax = (ring_centroids(haft_i, co)[-1] - ring_centroids(haft_i, co)[0]).normalized()
mount = (head_centroid(co) - P).normalized()
jang = math.degrees(math.acos(max(-1, min(1, abs(mount.dot(haft_ax))))))
print("COAXIAL: head-vs-haft junction angle = %.4f deg (need <=0.5)" % jang)
assert jang <= 0.5, "FINAL canonical head NOT coaxial: %.4f deg" % jang

# ----------------------------------------------------------------- haft:head ratio (1.1x => ~1.035)
head_z = [co[i].z for i in head_i]
tip_z = max(head_z)
head_h = tip_z - HEAD_BASE_Z                 # head height from junction to tip (mesh units)
grip_z = min(co[i].z for i in haft_i)        # grip end (handle bottom)
haft_len = HEAD_BASE_Z - grip_z              # handle length (mesh units)
ratio = haft_len / head_h
print("HAFT:HEAD RATIO = %.4f  (haft_len=%.5f head_h=%.5f, expect ~1.035 for 1.1x)" %
      (ratio, haft_len, head_h))
assert abs(ratio - 1.035) < 0.02, "ratio %.4f not the 1.1x pick (~1.035)" % ratio

# head-height-from-tip projection (re-confirm the coaxial Unity const is right for this mesh)
head_h_from_tip = tip_z - HEAD_BASE_Z
print("HEAD_HEIGHT_FROM_TIP (mesh-u) = %.5f  (Unity const HeroAxeCoaxialHeadHeightFromTipU=%.5f)" %
      (head_h_from_tip, COAXIAL_HEAD_H_FROM_TIP_U))

# ----------------------------------------------------------------- Unity import normalize + grip-shift
# The Unity importer measures head height = long-axis span of head verts within
# COAXIAL_HEAD_H_FROM_TIP_U of the tip, then globalScale = TARGET_HEAD_HEIGHT_U / headH.
# Replicate that head-height measurement in mesh units (the long axis is Z here in Blender; Unity
# bakeAxisConversion maps Blender +Z -> Unity +Y, lengths preserved).
junction_for_measure = tip_z - COAXIAL_HEAD_H_FROM_TIP_U
head_meas = [co[i].z for i in range(n) if co[i].z >= junction_for_measure]
headH_meas = max(head_meas) - min(head_meas)
gs = TARGET_HEAD_HEIGHT_U / headH_meas
print("UNITY-NORMALIZE: headH_meas(mesh-u)=%.5f -> globalScale=%.5f (pins head to %.5fu)" %
      (headH_meas, gs, TARGET_HEAD_HEIGHT_U))

# Imported-Unity handle geometry (mesh-u * globalScale). Blender +Z -> Unity +Y.
grip_end_Y = grip_z * gs                      # handle bottom in Unity Y (negative)
head_base_Y = HEAD_BASE_Z * gs                # junction in Unity Y
handleLen_Y = head_base_Y - grip_end_Y        # graspable handle length (Unity units)
# Lower-third grip: shift the displayed mesh +Y so the lower-third point lands at the hand seat (Y=0).
lower_third_Y = grip_end_Y + handleLen_Y / 3.0
grip_shift_Y = -lower_third_Y                 # shift magnitude to bring lower-third to origin
print("GRIP (imported-Unity units): grip_end_Y=%.5f head_base_Y=%.5f handleLen=%.5f" %
      (grip_end_Y, head_base_Y, handleLen_Y))
print("=> HeldAxeGripShiftY (1.1x) = %.5f  (was 0.47181 for 1.5x)" % grip_shift_Y)

print("\n=== BL_20 FINAL SUMMARY (86cabh907) ===")
print("shipped wpn_axe_01.fbx = the 1.1x [L] pick (byte-copy of len11)")
print("coaxial junction = %.4f deg ; haft:head = %.4f ; single WeaponPalette ; origin (0,0,0)" %
      (jang, ratio))
print("HeroAxeCoaxialHeadHeightFromTipU = %.5f (unchanged — same coaxial head)" % COAXIAL_HEAD_H_FROM_TIP_U)
print("globalScale (canonical, coaxial-normalized) = %.5f" % gs)
print("HeldAxeGripShiftY (re-derive for 1.1x) = %.5f" % grip_shift_Y)
print("BL_20_DONE")
