"""Read-only: inspect the verts NEAR the head<->haft junction on the 2.0x variant to
decide the straighten boundary. We must straighten the HAFT (the wooden handle) WITHOUT
introducing a kink at the head base. Strategy options:
  (A) Straighten only z <= head_base verts by per-ring X re-centering. Risk: a step at the
      junction if the head-base ring also carries the +X drift (it would, since the head
      sits on the bent haft top).
  (B) Straighten the WHOLE mesh's X-drift as a function of Z by the SAME per-Z correction,
      so head + haft both de-bend coherently -> NO junction step. The head's SHAPE is
      preserved (every head vert shifts by the same X correction for its Z), and head_top
      stays at the corrected X. BUT the brief LOCKS head verts byte-identical (head_top
      Z=+0.495347 invariant) — an X shift would change head verts. So (B) is OUT.
  => Need (A) but verify continuity: dump the X spread of verts in Z bands across the
     junction so we choose a correction that lands the haft-top ring on the head-base X.
"""
import bpy
FBX = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/team/devon-dev/wpn_axe_haft_2p0x_86cabh907.fbx'
JUNC_ABS = 0.022674
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
co = [v.co.copy() for v in obj.data.vertices]
print("JUNC_PROBE_START")
# all distinct Z rings across the whole mesh, with X centroid + X spread
rings = {}
for c in co:
    rings.setdefault(round(c.z, 3), []).append(c)
for z in sorted(rings.keys()):
    pts = rings[z]
    cx = sum(p.x for p in pts) / len(pts)
    cy = sum(p.y for p in pts) / len(pts)
    xmin = min(p.x for p in pts); xmax = max(p.x for p in pts)
    side = "HAFT" if z <= JUNC_ABS else "head"
    print("  z=%+.4f  n=%2d  Xcentroid=%+.5f  Ycentroid=%+.5f  Xrange=[%+.4f..%+.4f]  %s"
          % (z, len(pts), cx, cy, xmin, xmax, side))
print("JUNC_PROBE_DONE")
