"""
Headless verification that the runtime head-size dial's fraction-based head-vert
selection (HeldWeaponCycleDebug.ResolveAxeHead) cleanly separates HEAD from HAFT
on the RESTORED 4208067 stone-axe FBX.

Reproduces the C# selection EXACTLY in mesh-LOCAL space:
  longAxis = the largest bounds extent
  junctionCoord = haftMin + haftSpan * headJunctionFraction (0.62)
  head = every vert whose longAxis-component > junctionCoord

NOTE Unity import applies Bake Axis Conversion (Blender Z-up -> Unity Y-up) and
the meta globalScale, but the dial works in MESH-LOCAL vertex space and the
fraction is span-relative, so the junction fraction is invariant to the axis
remap + uniform import scale. We read the verts straight from the FBX mesh.

Prints: bounds, long axis, junction coord, head-vert count vs haft-vert count,
and the Z-histogram so a human can confirm the head is a clean cluster above 0.62.
Run: blender --background --python verify_axe_head_junction.py -- <fbx_path>
"""
import bpy, sys, math

argv = sys.argv
fbx = argv[argv.index("--") + 1] if "--" in argv else None
if not fbx:
    print("ERROR: pass the FBX path after --")
    sys.exit(1)

# clean scene
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print(f"[verify] imported {len(meshes)} mesh objects from {fbx}")
for o in meshes:
    me = o.data
    n = len(me.vertices)
    # local-space verts (the C# dial reads mesh.vertices = local)
    vs = [v.co.copy() for v in me.vertices]
    if n == 0:
        print(f"[verify] {o.name}: 0 verts"); continue
    bmin = [min(v[i] for v in vs) for i in range(3)]
    bmax = [max(v[i] for v in vs) for i in range(3)]
    ext = [(bmax[i] - bmin[i]) for i in range(3)]
    # long axis = max extent (mirror the C# ext.x>=ext.y... tie-break)
    if ext[0] >= ext[1] and ext[0] >= ext[2]:
        la = 0
    elif ext[1] >= ext[2]:
        la = 1
    else:
        la = 2
    axis_name = "XYZ"[la]
    span = max(1e-4, bmax[la] - bmin[la])
    frac = 0.62
    jc = bmin[la] + span * frac
    head = [v for v in vs if v[la] > jc]
    haft = [v for v in vs if v[la] <= jc]
    print(f"[verify] {o.name}: verts={n}")
    print(f"[verify]   bounds min={['%.4f'%b for b in bmin]} max={['%.4f'%b for b in bmax]}")
    print(f"[verify]   ext={['%.4f'%e for e in ext]}  longAxis={axis_name}({la})  span={span:.4f}")
    print(f"[verify]   junctionFraction=0.62 -> junctionCoord={jc:.4f}")
    print(f"[verify]   HEAD verts (>{jc:.4f} on {axis_name}) = {len(head)}/{n}   HAFT verts = {len(haft)}/{n}")
    # histogram of the long-axis coord in 10 bins so we can SEE the head cluster + the gap
    lo, hi = bmin[la], bmax[la]
    bins = [0]*10
    for v in vs:
        t = (v[la]-lo)/span
        b = min(9, max(0, int(t*10)))
        bins[b] += 1
    print(f"[verify]   {axis_name}-histogram (10 bins, lo->hi):")
    for bi in range(10):
        edge_lo = lo + span*bi/10
        edge_hi = lo + span*(bi+1)/10
        marker = " <== junction 0.62 falls here" if (bi/10 <= frac < (bi+1)/10) else ""
        print(f"[verify]     bin{bi} [{edge_lo:7.4f},{edge_hi:7.4f}): {bins[bi]:4d}{marker}")
    # SANITY: head should be a non-empty, plausible minority cluster (an axe head
    # is the top ~30-45% of the long axis). Flag if it grabs nothing or grabs the
    # whole mesh (a mis-detected long axis or a degenerate junction).
    head_frac = len(head)/n
    verdict = "OK" if (0.05 <= head_frac <= 0.85 and len(head) > 0) else "SUSPECT"
    print(f"[verify]   head/total = {head_frac:.3f}  => {verdict}")
