"""
Find the EXACT clean head<->haft gap on the restored stone axe FBX, and the
junctionFraction that lands in the middle of it. The 10-bin histogram showed an
EMPTY bin3 [-0.1062, 0.0084) on Z -- the real geometric gap. Pin the verts that
bracket it so we choose a robust fraction (mid-gap) for headJunctionFraction.

Also reports, for a candidate fraction, how many verts land in HEAD vs HAFT and
the min-gap margin (distance from the cut to the nearest vert on each side) so we
know the selection is robust to FBX re-import float jitter.
"""
import bpy, sys

argv = sys.argv
fbx = argv[argv.index("--") + 1] if "--" in argv else None
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
o = [x for x in bpy.data.objects if x.type == 'MESH'][0]
vs = [v.co.copy() for v in o.data.vertices]
n = len(vs)
la = 2  # Z confirmed long axis
zs = sorted(v[la] for v in vs)
zmin, zmax = zs[0], zs[-1]
span = zmax - zmin
print(f"[gap] verts={n} Zmin={zmin:.4f} Zmax={zmax:.4f} span={span:.4f}")

# find the largest gap between consecutive sorted Z values in the lower-middle
# region (where head meets haft) -- that's the head<->haft separation.
gaps = []
for i in range(1, n):
    gaps.append((zs[i] - zs[i-1], zs[i-1], zs[i]))
gaps.sort(reverse=True)
print("[gap] top 5 consecutive-Z gaps (size, lower, upper, mid-fraction):")
for g, lo, hi, in [(g[0], g[1], g[2]) for g in gaps[:5]]:
    mid = (lo + hi) / 2
    frac = (mid - zmin) / span
    print(f"[gap]   gap={g:.4f}  between Z={lo:.4f} and Z={hi:.4f}  midFraction={frac:.4f}")

# the head is the cluster ABOVE the chosen gap. Use the single largest gap in the
# region above the grip (Z > zmin + 0.15*span, to skip the grip-bottom spacing).
region = [g for g in gaps if g[1] > zmin + 0.15*span]
g, lo, hi = region[0][0], region[0][1], region[0][2]
mid = (lo + hi)/2
chosen = (mid - zmin)/span
print(f"[gap] CHOSEN head<->haft gap: size={g:.4f} between Z={lo:.4f}/{hi:.4f} -> junctionFraction={chosen:.4f}")

# report selection at the chosen fraction + a couple of candidates
for frac in (chosen, round(chosen,2), 0.40, 0.42, 0.45):
    jc = zmin + span*frac
    head = [v for v in vs if v[la] > jc]
    haft = [v for v in vs if v[la] <= jc]
    # margin: nearest vert below the cut and above the cut
    below = max((v[la] for v in vs if v[la] <= jc), default=None)
    above = min((v[la] for v in vs if v[la] > jc), default=None)
    margin_lo = (jc - below) if below is not None else float('nan')
    margin_hi = (above - jc) if above is not None else float('nan')
    print(f"[gap] frac={frac:.4f} jc={jc:.4f}  HEAD={len(head)}/{n} HAFT={len(haft)}/{n}  "
          f"margin below={margin_lo:.4f} above={margin_hi:.4f}")
