"""
bl_14_axe_head_shrink.py — shrink the in-house wpn_axe_01 HEAD further about its
base, then re-export the FBX (ticket 86cabh907 dial-tool round, Sponsor blocker #1
"axe head STILL too big").

CONTEXT: PR #100 already shrank the head ~0.8x (head X-width 0.696->0.557, Z-top
1.186->1.067; recorded in commit 41b516d). The Sponsor judged the result STILL too
big out of the box. This pass shrinks the head a further HEAD_FACTOR about the
head-base pivot so the DEFAULT reads reasonable BEFORE the runtime dial fine-tunes
it. The runtime ;/' + F9 AXE-HEAD dial (HeldWeaponCycleDebug) keeps 1.000 == "this
new shipped head", so the dial still tunes from the smaller default.

WHAT IT TOUCHES (preserves everything else):
  - Scales ONLY the fanned HEAD verts (blade bit -X + poll +X) — classified as
    |local x| > X_HEAD_THRESH AND z > Z_HEAD_THRESH. On the live mesh that is the
    44 blade/poll verts (the "45 blade verts" of the PR #100 precedent), NOT the
    thin haft shaft (|x| <= 0.044) nor the lashing band (small radius around the
    haft) nor the grip origin (z=0).
  - Scales head verts in X and Z about the head-BASE pivot (0, 0, PIVOT_Z) so the
    blade fan and the overall head height both come down proportionally; Y
    (broad-face thickness) is scaled too so the head stays in proportion (a flat
    biface, not a slab). The haft length, grip-point origin (0,0,0), and +Z
    forward axis are UNCHANGED — mesh Z-bottom stays 0.000.
  - Re-exports Assets/Art/Props/WeaponPack/wpn_axe_01.fbx with the SAME contract as
    bl_11 (-Y Forward / Z Up / FBX Unit Scale / Normals Only / single WeaponPalette
    slot), so the Unity import (no 100x trap) + the shared-palette ~1-draw material
    are unchanged.

Reproducible:
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    art-src/weapon-pack/weapon_set_src.blend --python tools/debug/bl_14_axe_head_shrink.py
The script SAVES the .blend in place (so the smaller head is the new source of
truth) and writes the FBX. Idempotent in the sense that re-running compounds the
shrink — run ONCE per intended factor; the committed .blend + FBX are the result.
"""
import bpy, bmesh, os

# --- tuning ---------------------------------------------------------------
# Additional shrink of the CURRENT (already-0.8x) head. 0.72 -> the new default is
# ~0.58x of the original pre-#100 head: a clearly-smaller, reasonable out-of-box
# head that the runtime dial then fine-tunes. Chosen by eye against the haft/grip.
HEAD_FACTOR   = 0.72
X_HEAD_THRESH = 0.06     # |local x| above this = fanned head (blade/poll), not haft/lash
Z_HEAD_THRESH = 0.55     # z above this = head band (the haft top is ~0.5 thin core)
PIVOT_Z       = 0.594    # head-base pivot z (lowest head vert) — head shrinks toward the neck
OUT_FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
BLEND   = bpy.data.filepath  # save back into the same source .blend
# --------------------------------------------------------------------------

obj = bpy.data.objects.get('wpn_axe_01')
assert obj is not None, "wpn_axe_01 object must exist in the source .blend"
me = obj.data


def measure(tag):
    xs = [v.co.x for v in me.vertices]; zs = [v.co.z for v in me.vertices]
    print("%s: verts=%d  X[%.4f..%.4f] width=%.4f  Z[%.4f..%.4f]" %
          (tag, len(me.vertices), min(xs), max(xs), max(xs) - min(xs), min(zs), max(zs)))


measure("BEFORE")

# classify the head verts (fanned blade/poll) — NOT the haft, lashing, or grip
head_idx = [i for i, v in enumerate(me.vertices)
            if abs(v.co.x) > X_HEAD_THRESH and v.co.z > Z_HEAD_THRESH]
print("HEAD verts to scale: %d (about pivot z=%.3f, factor=%.3f)" %
      (len(head_idx), PIVOT_Z, HEAD_FACTOR))
assert 30 <= len(head_idx) <= 60, \
    "head-vert classification off (got %d; expected ~44) — re-probe before shrinking" % len(head_idx)

pivot = (0.0, 0.0, PIVOT_Z)  # head-base, on the haft centreline
for i in head_idx:
    co = me.vertices[i].co
    co.x = pivot[0] + (co.x - pivot[0]) * HEAD_FACTOR
    co.y = co.y * HEAD_FACTOR                       # thickness scales about y=0 (broad faces symmetric)
    co.z = pivot[2] + (co.z - pivot[2]) * HEAD_FACTOR

me.update()
measure("AFTER ")

# Re-assert faceted shading (unchanged by a vert move, but cheap + safe) + recalc
# normals outward so the shrunk facets stay outward-wound (URP Cull Back).
bm = bmesh.new(); bm.from_mesh(me)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(me); bm.free()
for p in me.polygons: p.use_smooth = True
for e in me.edges: e.use_edge_sharp = True
me.update()

# Save the smaller head back into the source .blend (new source of truth).
if BLEND:
    bpy.ops.wm.save_mainfile()
    print("SAVED_BLEND", BLEND)

# Re-export ONLY the axe, same contract as bl_11_export_set.
for x in bpy.context.scene.objects:
    x.select_set(False)
obj.location = (0, 0, 0); obj.rotation_euler = (0, 0, 0); obj.scale = (1, 1, 1)
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(
    filepath=OUT_FBX,
    use_selection=True,
    apply_scale_options='FBX_SCALE_UNITS',   # FBX Unit Scale (avoids 100x bug)
    axis_forward='-Y',
    axis_up='Z',
    use_space_transform=False,
    bake_space_transform=False,
    mesh_smooth_type='OFF',                    # Normals Only (custom split normals)
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    object_types={'MESH'},
    use_custom_props=False,
)
tris = sum(len(p.vertices) - 2 for p in me.polygons)
print("EXPORTED wpn_axe_01: verts=%d tris=%d dims=%s -> %s" %
      (len(me.vertices), tris, [round(v, 3) for v in obj.dimensions], OUT_FBX))
print("AXE_HEAD_SHRINK_DONE")
