"""
bl_16_probe_axe_fbx.py — DIAGNOSTIC PROBE (no mutation). Imports the restored
wpn_axe_01.fbx (byte-identical to commit 4208067, the stone wedge) and dumps the
ground-truth geometry so the 0.65x head bake (bl_17) cuts at the REAL head<->haft
junction, not an assumed z-fraction.

Per diagnose-via-trace: we do NOT trust the brief's "~0.50 z-cut fraction" blindly —
we measure the actual Z distribution + X-span by Z-band to LOCATE where the thin
haft column transitions into the fanned head, then bl_17 uses the measured junction.

Run:
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
    --python tools/debug/bl_16_probe_axe_fbx.py
Read-only: imports the FBX, prints, exits. Writes NOTHING.
"""
import bpy

FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'

# clean slate
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
print("IMPORTED objects:", [o.name for o in meshes])
assert len(meshes) >= 1, "no mesh imported from FBX"
obj = meshes[0]
me = obj.data

# NOTE: FBX axis conversion (-Y Fwd / Z Up) means the imported object may carry a
# rotation. Report BOTH local (mesh) coords and world coords so we know which frame
# the head/haft Z-axis lives in.
print("OBJ matrix_world:")
for row in obj.matrix_world:
    print("   ", [round(c, 4) for c in row])
print("OBJ scale:", [round(c, 4) for c in obj.scale],
      " rotation_euler(deg):", [round(__import__('math').degrees(a), 2) for a in obj.rotation_euler])

# local-space bounds
xs = [v.co.x for v in me.vertices]
ys = [v.co.y for v in me.vertices]
zs = [v.co.z for v in me.vertices]
print("LOCAL bounds: X[%.4f..%.4f] Y[%.4f..%.4f] Z[%.4f..%.4f]  verts=%d faces=%d" %
      (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs), len(me.vertices), len(me.polygons)))
print("OBJ dimensions (world):", [round(d, 4) for d in obj.dimensions])

# connected components (head should be the widest-X-span island; haft the thin column)
def components(mesh):
    adj = {i: set() for i in range(len(mesh.vertices))}
    for e in mesh.edges:
        a, b = e.vertices; adj[a].add(b); adj[b].add(a)
    seen = set(); comps = []
    for i in range(len(mesh.vertices)):
        if i in seen:
            continue
        stack = [i]; comp = []
        while stack:
            x = stack.pop()
            if x in seen:
                continue
            seen.add(x); comp.append(x); stack.extend(adj[x] - seen)
        comps.append(comp)
    return comps

comps = components(me)
print("CONNECTED COMPONENTS: %d" % len(comps))
for ci, c in enumerate(comps):
    cxs = [me.vertices[i].co.x for i in c]
    czs = [me.vertices[i].co.z for i in c]
    print("  comp %d: n=%2d  Xspan=%.4f X[%.3f..%.3f]  Z[%.3f..%.3f]" %
          (ci, len(c), max(cxs) - min(cxs), min(cxs), max(cxs), min(czs), max(czs)))

# Z-band X-span scan (LOCAL Z): find where the thin haft (small X-span) widens into
# the head fan (large X-span). The junction is the lowest Z at which X-span jumps.
zmin, zmax = min(zs), max(zs)
print("Z-BAND X-SPAN SCAN (local Z, 20 bands) — junction = where span jumps up:")
NB = 20
for b in range(NB):
    z0 = zmin + (zmax - zmin) * b / NB
    z1 = zmin + (zmax - zmin) * (b + 1) / NB
    band = [v.co.x for v in me.vertices if z0 <= v.co.z < z1 + (1e-9 if b == NB - 1 else 0)]
    if band:
        span = max(band) - min(band)
        zfrac = (z0 - zmin) / (zmax - zmin)
        print("  z[%.3f..%.3f] frac=%.2f  Xspan=%.4f  n=%d" % (z0, z1, zfrac, span, len(band)))

print("AXE_PROBE_DONE")
