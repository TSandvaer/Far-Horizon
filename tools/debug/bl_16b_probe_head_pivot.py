"""
bl_16b_probe_head_pivot.py — DIAGNOSTIC (no mutation). Locate the head component's
seating geometry so bl_17 picks the correct UNIFORM-scale pivot (the head<->haft
junction) — the point about which the head shrinks IN PLACE, staying seated on the
haft and on the +Z axis.

The head is comp 1 (widest X-span island). The pivot for an in-place shrink is the
head's base on the haft centreline: X=0 (centreline), Y=0 (broad-face symmetric),
Z = the head's MIN z (where it meets the haft). We dump the head's base verts (the
lowest-Z ring) + their X,Y to confirm the base centres on the haft axis.

Read-only.
"""
import bpy

FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
me = obj.data


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
def xspan(c):
    xs = [me.vertices[i].co.x for i in c]; return max(xs) - min(xs)
head = max(comps, key=xspan)
hxs = [me.vertices[i].co.x for i in head]
hys = [me.vertices[i].co.y for i in head]
hzs = [me.vertices[i].co.z for i in head]
print("HEAD comp: n=%d  X[%.4f..%.4f] width=%.4f  Y[%.4f..%.4f]  Z[%.4f..%.4f]" %
      (len(head), min(hxs), max(hxs), max(hxs) - min(hxs), min(hys), max(hys), min(hzs), max(hzs)))

zmin_head = min(hzs)
# base ring = head verts within 0.02 of the head's lowest Z
base = [i for i in head if me.vertices[i].co.z <= zmin_head + 0.02]
bxs = [me.vertices[i].co.x for i in base]
bys = [me.vertices[i].co.y for i in base]
print("HEAD BASE ring (z<=%.4f): n=%d  X[%.4f..%.4f] meanX=%.4f  Y[%.4f..%.4f] meanY=%.4f" %
      (zmin_head + 0.02, len(base), min(bxs), max(bxs), sum(bxs) / len(bxs),
       min(bys), max(bys), sum(bys) / len(bys)))

# head width at a few Z slices (for before/after head-width verification in bl_17)
print("HEAD width by Z slice:")
for zf in [0.0, 0.25, 0.5, 0.75, 1.0]:
    z = zmin_head + (max(hzs) - zmin_head) * zf
    sl = [me.vertices[i].co.x for i in head if abs(me.vertices[i].co.z - z) < 0.04]
    if sl:
        print("  z=%.3f (slice frac %.2f): Xwidth=%.4f" % (z, zf, max(sl) - min(sl)))

# CANONICAL head width metric for the report: full head X-span (max-min over all head verts)
print("HEAD_MAX_WIDTH %.4f" % (max(hxs) - min(hxs)))
print("HEAD_MIN_Z %.4f" % zmin_head)
print("HEAD_TOP_Z %.4f" % max(hzs))
print("HEAD_PIVOT_PROBE_DONE")
