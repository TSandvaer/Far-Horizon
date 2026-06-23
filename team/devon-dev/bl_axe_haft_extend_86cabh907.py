"""
bl_axe_haft_extend_86cabh907.py — BAKE 3 handle-LENGTH variants + measure each.

COMPARISON ARTIFACTS for the Sponsor to pick handle length by eye. NOT the final
commit. Head is NEVER touched (locked at 0.65x / commit 14d5a41).

Method: extend the HANDLE by scaling the haft verts' long-axis (Z) coordinate
AWAY FROM the head, anchored at the head-junction (Z=junc, 50% span). Head verts
(z > junc) stay fixed; junction verts don't move; grip-end pushes toward -Z.
Diameter (X/Y) is left as-is. Origin stays at (0,0,0) — the HeldTool seat — so the
GRIP-POINT semantics are preserved (the seat OFFSET shifts; flagged for post-pick
CI held-capture re-check).

Source: the baked 0.65x head FBX (14d5a41). For each factor we scale ONLY haft Z.

Run:
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    --python team/devon-dev/bl_axe_haft_extend_86cabh907.py
"""
import bpy
from mathutils import Vector

WT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt'
BAKED = WT + r'/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'   # 14d5a41, 0.65x head
OUTDIR = WT + r'/team/devon-dev'
JUNCTION_FRACTION = 0.50
FACTORS = [1.5, 2.0, 2.5]

def load_single_mesh(fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(meshes) == 1, "expected 1 mesh, got %d" % len(meshes)
    return meshes[0]

def long_axis(co):
    bmin = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    bmax = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    ext = (bmax - bmin) * 0.5
    if ext.x >= ext.y and ext.x >= ext.z: a = 0
    elif ext.y >= ext.z: a = 1
    else: a = 2
    return a, bmin, bmax

def measure(co, la, label):
    bmin = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    bmax = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    span = bmax[la] - bmin[la]
    grip_end = bmin[la]
    head_top = bmax[la]
    # IMPORTANT: junction is the FIXED head-base coord from the ORIGINAL mesh, not a
    # re-derived 50% of the new (longer) span. Caller passes the fixed junc.
    return span, grip_end, head_top

def comp(v, a):
    return v.x if a==0 else (v.y if a==1 else v.z)

# --- derive the FIXED junction + head height ONCE from the source mesh ---
src = load_single_mesh(BAKED)
co0 = [v.co.copy() for v in src.data.vertices]
la, bmin0, bmax0 = long_axis(co0)
axis = "XYZ"[la]
span0 = bmax0[la] - bmin0[la]
grip0 = bmin0[la]
junc = grip0 + span0 * JUNCTION_FRACTION   # FIXED head-base anchor
head_i = [i for i in range(len(co0)) if comp(co0[i], la) > junc]
haft_i = [i for i in range(len(co0)) if comp(co0[i], la) <= junc]
head_lo = min(comp(co0[i], la) for i in head_i)
head_hi = max(comp(co0[i], la) for i in head_i)
head_h = head_hi - head_lo   # head height is INVARIANT across variants
haft_len0 = junc - grip0

print("EXTEND_BAKE_START")
print("source: long_axis=%s span=%.4f grip=%.6f junc=%.6f head_top=%.6f" %
      (axis, span0, grip0, junc, bmax0[la]))
print("source: HEAD_H=%.4f (INVARIANT)  HAFT_LEN0=%.4f  RATIO0=%.4f" %
      (head_h, haft_len0, haft_len0/head_h))
print("source: n_head=%d n_haft=%d verts=%d" % (len(head_i), len(haft_i), len(co0)))

results = []
for f in FACTORS:
    obj = load_single_mesh(BAKED)
    me = obj.data
    # scale ONLY haft verts' long-axis coord about the FIXED junction:
    #   new = junc + (z - junc) * f      (z <= junc for haft -> pushes toward -Z)
    moved = 0
    for v in me.vertices:
        z = comp(v.co, la)
        if z <= junc + 1e-9:
            newz = junc + (z - junc) * f
            if la == 0: v.co.x = newz
            elif la == 1: v.co.y = newz
            else: v.co.z = newz
            moved += 1
    me.update()
    co = [v.co.copy() for v in me.vertices]
    bmin = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    bmax = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    new_span = bmax[la] - bmin[la]
    new_grip = bmin[la]
    new_head_top = bmax[la]
    new_haft_len = junc - new_grip
    new_ratio = new_haft_len / head_h
    # verify head untouched: head_top unchanged, head verts' max long-axis unchanged
    head_top_now = max(comp(co[i], la) for i in head_i)
    head_unchanged = abs(head_top_now - bmax0[la]) < 1e-6
    # diameter unchanged (off-axis)
    off = [i for i in (0,1,2) if i != la]
    haft_lo = new_grip; haft_hi = new_grip + new_haft_len*0.70
    mid = [i for i,c in enumerate(co) if (comp(c,la)<=junc and haft_lo<=comp(c,la)<=haft_hi)]
    def osp(idx):
        a0=[comp(co[i],off[0]) for i in idx]; a1=[comp(co[i],off[1]) for i in idx]
        return (max(a0)-min(a0)),(max(a1)-min(a1))
    da,db = osp(mid if mid else haft_i)
    dia = max(da,db)
    print("--- factor %.1fx: haft verts moved=%d  HAFT_LEN=%.4f  RATIO=%.4f  span=%.4f  grip_end=%.6f  head_top=%.6f(head_unchanged=%s)  haft_dia=%.4f  origin=%s" %
          (f, moved, new_haft_len, new_ratio, new_span, new_grip, head_top_now, head_unchanged, dia, [round(x,5) for x in obj.matrix_world.translation]))
    # export FBX (same settings as the bake: -Y Forward, Z Up, Normals Only)
    tag = ("%.1f" % f).replace(".","p")
    fbx_out = OUTDIR + "/wpn_axe_haft_%sx_86cabh907.fbx" % tag
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=fbx_out, use_selection=True,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Y', axis_up='Z',
        use_space_transform=False, bake_space_transform=False,
        mesh_smooth_type='OFF', use_mesh_modifiers=True,
        add_leaf_bones=False, object_types={'MESH'})
    print("    EXPORTED -> %s" % fbx_out)
    results.append((f, new_haft_len, new_ratio, new_grip, dia, head_unchanged))

print("SUMMARY")
print("variant  haft_len  ratio(haft:head)  grip_end  haft_dia  head_unchanged")
print("current  %.4f   %.4f            %.6f  %.4f   (baseline)" % (haft_len0, haft_len0/head_h, grip0, 0.0713))
for f,hl,r,g,d,hu in results:
    print("%.1fx     %.4f   %.4f            %.6f  %.4f   %s" % (f,hl,r,g,d,hu))
print("EXTEND_BAKE_DONE")
