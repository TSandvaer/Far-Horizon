"""
bl_axe_final_bake_86cabh907.py — THE FINAL BAKE for ticket 86cabh907 / PR #100.

Produces Assets/Art/Props/WeaponPack/wpn_axe_01.fbx with:
  1. HANDLE promoted to 2.0x length (haft:head ~= 2.12:1) — scale ONLY the haft verts'
     long-axis (Z) coord about the FIXED head-base (z = HEAD_BASE_Z) so the head is
     byte-untouched and the grip-end pushes toward -Z. (Same method as the 2.0x WIP.)
  2. HANDLE fully STRAIGHTENED — remove the ~1.5deg intentional lean. The original haft
     leans in +X only (Y already 0): ring centroids drift 0.000 -> +0.00500 -> +0.00866
     up the haft, CONTINUING into the head-base ring at X = +0.01000. To straighten the
     haft WITHOUT a junction kink AND WITHOUT touching the head, we re-center each haft
     ring's (X,Y) onto the HEAD-BASE ring's centroid (X = HEAD_BASE_X, Y = 0): the haft
     becomes a perfectly vertical column coaxial with where the head meets it -> residual
     bend ~0deg, zero new step at the junction. The cross-section SHAPE of each ring is
     preserved (we translate the ring, never rescale it).
  3. HEAD byte-LOCKED — every vert with z > HEAD_BASE_Z is left EXACTLY as in 14d5a41
     (head_top Z = +0.495347 invariant). Verified after the bake.

Source: the committed 0.65x-head FBX (14d5a41) at Assets/.../wpn_axe_01.fbx.
Single mesh, 128 verts, long axis Z, grip-MIDPOINT-ish origin at (0,0,0).

Export: -Y Forward / Z Up / Normals Only (the §8 weapon FBX settings) — IN PLACE over
the committed asset.

Run:
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    --python team/devon-dev/bl_axe_final_bake_86cabh907.py
"""
import bpy, math
from mathutils import Vector

WT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt'
SRC = WT + r'/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'   # 14d5a41, 0.65x head, 1x haft
OUT = SRC                                                    # bake IN PLACE
HAFT_FACTOR = 2.0
HEAD_BASE_Z = 0.022674      # the 50% junction from _axe_measure_baseline (head base, INVARIANT abs Z)

def load_single_mesh(fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(meshes) == 1, "expected 1 mesh, got %d" % len(meshes)
    return meshes[0]

obj = load_single_mesh(SRC)
me = obj.data
verts = me.vertices
n = len(verts)

# --- snapshot ORIGINAL head verts (z > HEAD_BASE_Z) for the byte-lock verify ---
orig = [v.co.copy() for v in verts]
head_idx = [i for i in range(n) if orig[i].z > HEAD_BASE_Z]
haft_idx = [i for i in range(n) if orig[i].z <= HEAD_BASE_Z]
orig_head = {i: orig[i].copy() for i in head_idx}
head_top0 = max(orig[i].z for i in head_idx)

# --- head-base ring centroid (the lowest head ring) -> the X the straight haft aligns to ---
# lowest head ring = head verts within a small z-band above the junction
head_zs = sorted(orig[i].z for i in head_idx)
base_z = head_zs[0]
base_ring = [i for i in head_idx if abs(orig[i].z - base_z) < 1e-3]
head_base_x = sum(orig[i].x for i in base_ring) / len(base_ring)
head_base_y = sum(orig[i].y for i in base_ring) / len(base_ring)

print("FINAL_BAKE_START")
print("verts=%d  head=%d  haft=%d  head_top0=%.6f" % (n, len(head_idx), len(haft_idx), head_top0))
print("head-base ring: z=%.4f n=%d centroid=(%.5f, %.5f)" %
      (base_z, len(base_ring), head_base_x, head_base_y))

# --- STEP 1: extend the haft 2.0x about the FIXED head base (scale Z only) ---
# new_z = HEAD_BASE_Z + (z - HEAD_BASE_Z) * f   (z <= HEAD_BASE_Z -> pushes toward -Z)
moved = 0
for i in haft_idx:
    z = verts[i].co.z
    verts[i].co.z = HEAD_BASE_Z + (z - HEAD_BASE_Z) * HAFT_FACTOR
    moved += 1

# --- STEP 2: straighten the haft — re-center each haft RING onto the head-base centroid.
# The lean is a per-ring (X,Y) centroid drift; translate each ring rigidly so its centroid
# sits at (head_base_x, head_base_y). Ring = haft verts sharing a Z (after the stretch).
ring_map = {}
for i in haft_idx:
    key = round(verts[i].co.z, 3)
    ring_map.setdefault(key, []).append(i)
straight_log = []
for key, idxs in sorted(ring_map.items()):
    cx = sum(verts[i].co.x for i in idxs) / len(idxs)
    cy = sum(verts[i].co.y for i in idxs) / len(idxs)
    ddx = head_base_x - cx
    ddy = head_base_y - cy
    for i in idxs:
        verts[i].co.x += ddx
        verts[i].co.y += ddy
    straight_log.append((key, cx, cy, ddx, ddy))
for key, cx, cy, ddx, ddy in straight_log:
    print("  straighten haft ring z=%+.4f  oldC=(%+.5f,%+.5f)  shift=(%+.5f,%+.5f) -> (%+.5f,%+.5f)" %
          (key, cx, cy, ddx, ddy, cx + ddx, cy + ddy))

me.update()

# --- VERIFY: head byte-unchanged + new ratio + residual bend ---
co = [v.co.copy() for v in verts]
head_unchanged = all((co[i] - orig_head[i]).length < 1e-7 for i in head_idx)
head_top_now = max(co[i].z for i in head_idx)
grip = min(c.z for c in co)
haft_len = HEAD_BASE_Z - grip
head_lo = min(co[i].z for i in head_idx); head_hi = max(co[i].z for i in head_idx)
head_h = head_hi - head_lo
ratio = haft_len / head_h
# residual bend: end-to-end centroid deviation over the (now straight) haft
hk = sorted(ring_map.keys())
def ring_cent(key):
    idxs = ring_map[key]
    return (sum(co[i].x for i in idxs)/len(idxs), sum(co[i].y for i in idxs)/len(idxs), key)
g = ring_cent(hk[0]); t = ring_cent(hk[-1])
seg = Vector((t[0]-g[0], t[1]-g[1], t[2]-g[2]))
resid = math.degrees(math.atan2(math.hypot(seg.x, seg.y), abs(seg.z)))

print("VERIFY head_unchanged=%s  head_top_now=%.6f (expect %.6f)" % (head_unchanged, head_top_now, head_top0))
print("VERIFY HAFT_LEN=%.4f  HEAD_H=%.4f  RATIO_haft:head=%.4f  (target ~2.12)" % (haft_len, head_h, ratio))
print("VERIFY residual_bend=%.4f deg  grip_end_z=%.6f  head_top_z=%.6f" % (resid, grip, head_hi))
assert head_unchanged, "HEAD VERTS CHANGED — abort, do not export"
assert abs(head_top_now - head_top0) < 1e-6, "head_top moved — abort"

# --- Re-export with the EXACT bl_17 / §8 weapon FBX settings, IN PLACE over the committed
# asset. NO remove_doubles: we added/removed ZERO geometry (a pure translate + a Z-only haft
# scale), the source was already 0 loose verts, and a weld could touch the head's coincident
# blade-tip verts -> we must guarantee head byte-identity, so we do NOT weld. We mirror bl_17:
# all polys use_smooth=True, reset the object transform, use_custom_props=False, smooth=OFF
# (preserves the bake-baked per-face normals; we never RecalculateNormals — lowpoly-quality §1).
for p in me.polygons:
    p.use_smooth = True
obj.location = (0, 0, 0); obj.rotation_euler = (0, 0, 0); obj.scale = (1, 1, 1)
me.update()

bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(
    filepath=OUT, use_selection=True,
    apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Y', axis_up='Z',
    use_space_transform=False, bake_space_transform=False,
    mesh_smooth_type='OFF', use_mesh_modifiers=True,
    add_leaf_bones=False, object_types={'MESH'},
    use_custom_props=False)
tris = sum(len(p.vertices) - 2 for p in me.polygons)
print("EXPORTED verts=%d tris=%d -> %s" % (len(me.vertices), tris, OUT))
print("FINAL_BAKE_DONE")
